using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Loco.Gateway;

/// <summary>
/// Enterprise-grade API Gateway with advanced routing, load balancing, and resilience
/// </summary>
public class ApiGatewayService
{
    private readonly ILogger<ApiGatewayService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, ServiceEndpoint> _services;
    private readonly ConcurrentDictionary<string, IAsyncPolicy<HttpResponseMessage>> _policies;
    private readonly LoadBalancer _loadBalancer;
    private readonly RateLimiter _rateLimiter;
    private readonly MetricsCollector _metrics;
    private readonly AuthenticationService _authService;

    public ApiGatewayService(
        ILogger<ApiGatewayService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient("gateway");
        _services = new ConcurrentDictionary<string, ServiceEndpoint>();
        _policies = new ConcurrentDictionary<string, IAsyncPolicy<HttpResponseMessage>>();
        _loadBalancer = new LoadBalancer();
        _rateLimiter = new RateLimiter();
        _metrics = new MetricsCollector();
        _authService = new AuthenticationService(configuration);

        InitializeServices();
        InitializePolicies();
    }

    /// <summary>
    /// Route incoming request to appropriate microservice
    /// </summary>
    public async Task<HttpResponseMessage> RouteAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Extract route information
            var path = context.Request.Path.Value;
            var serviceName = ExtractServiceName(path);
            
            if (!_services.TryGetValue(serviceName, out var service))
            {
                _logger.LogWarning("Service not found: {ServiceName}", serviceName);
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            }

            // Apply rate limiting
            if (!await _rateLimiter.AllowRequestAsync(context))
            {
                _metrics.RecordRateLimitHit(serviceName);
                return new HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests);
            }

            // Authenticate request
            if (service.RequiresAuth && !await _authService.AuthenticateAsync(context))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
            }

            // Select endpoint using load balancing
            var endpoint = _loadBalancer.SelectEndpoint(service.Endpoints);
            
            // Get resilience policy for service
            var policy = _policies.GetOrAdd(serviceName, CreatePolicy);

            // Forward request with resilience
            var response = await policy.ExecuteAsync(async () =>
            {
                var request = CreateForwardRequest(context, endpoint);
                return await _httpClient.SendAsync(request);
            });

            // Record metrics
            _metrics.RecordRequest(serviceName, response.StatusCode, DateTime.UtcNow - startTime);

            return response;
        }
        catch (BrokenCircuitException)
        {
            _logger.LogError("Circuit breaker open for service: {ServiceName}", 
                ExtractServiceName(context.Request.Path.Value));
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing request");
            return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Register a new microservice
    /// </summary>
    public void RegisterService(string name, ServiceEndpoint service)
    {
        _services.AddOrUpdate(name, service, (k, v) => service);
        _logger.LogInformation("Service registered: {ServiceName}", name);
    }

    /// <summary>
    /// Health check for registered services
    /// </summary>
    public async Task<Dictionary<string, HealthStatus>> CheckHealthAsync()
    {
        var healthStatuses = new Dictionary<string, HealthStatus>();

        var tasks = _services.Select(async kvp =>
        {
            var serviceName = kvp.Key;
            var service = kvp.Value;
            
            var healthyEndpoints = new List<string>();
            var unhealthyEndpoints = new List<string>();

            foreach (var endpoint in service.Endpoints)
            {
                try
                {
                    var response = await _httpClient.GetAsync($"{endpoint}/health");
                    if (response.IsSuccessStatusCode)
                        healthyEndpoints.Add(endpoint);
                    else
                        unhealthyEndpoints.Add(endpoint);
                }
                catch
                {
                    unhealthyEndpoints.Add(endpoint);
                }
            }

            return (serviceName, new HealthStatus
            {
                IsHealthy = healthyEndpoints.Any(),
                HealthyEndpoints = healthyEndpoints,
                UnhealthyEndpoints = unhealthyEndpoints
            });
        });

        var results = await Task.WhenAll(tasks);
        
        foreach (var (name, status) in results)
        {
            healthStatuses[name] = status;
        }

        return healthStatuses;
    }

    private void InitializeServices()
    {
        // Load service configuration
        var servicesConfig = _configuration.GetSection("Gateway:Services");
        
        foreach (var serviceConfig in servicesConfig.GetChildren())
        {
            var service = new ServiceEndpoint
            {
                Name = serviceConfig.Key,
                Endpoints = serviceConfig.GetSection("Endpoints").Get<string[]>() ?? Array.Empty<string>(),
                RequiresAuth = serviceConfig.GetValue<bool>("RequiresAuth"),
                Timeout = TimeSpan.FromSeconds(serviceConfig.GetValue<int>("TimeoutSeconds", 30))
            };
            
            RegisterService(service.Name, service);
        }
    }

    private void InitializePolicies()
    {
        // Default policy configuration
        var defaultRetryCount = _configuration.GetValue<int>("Gateway:DefaultRetryCount", 3);
        var defaultTimeoutSeconds = _configuration.GetValue<int>("Gateway:DefaultTimeoutSeconds", 30);
    }

    private IAsyncPolicy<HttpResponseMessage> CreatePolicy(string serviceName)
    {
        var retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {RetryCount} after {Delay}ms for service {ServiceName}",
                        retryCount, timespan.TotalMilliseconds, serviceName);
                });

        var circuitBreakerPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .CircuitBreakerAsync(
                5,
                TimeSpan.FromSeconds(30),
                onBreak: (result, duration) =>
                {
                    _logger.LogWarning("Circuit breaker opened for service {ServiceName} for {Duration}",
                        serviceName, duration);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset for service {ServiceName}", serviceName);
                });

        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(30);

        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy, timeoutPolicy);
    }

    private string ExtractServiceName(string path)
    {
        var segments = path.Trim('/').Split('/');
        return segments.FirstOrDefault() ?? "default";
    }

    private HttpRequestMessage CreateForwardRequest(HttpContext context, string endpoint)
    {
        var request = new HttpRequestMessage
        {
            Method = new HttpMethod(context.Request.Method),
            RequestUri = new Uri($"{endpoint}{context.Request.Path}{context.Request.QueryString}")
        };

        // Copy headers
        foreach (var header in context.Request.Headers)
        {
            if (!header.Key.StartsWith("Host", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        // Copy body if present
        if (context.Request.ContentLength > 0)
        {
            request.Content = new StreamContent(context.Request.Body);
            if (context.Request.Headers.ContainsKey("Content-Type"))
            {
                request.Content.Headers.ContentType = 
                    new System.Net.Http.Headers.MediaTypeHeaderValue(context.Request.ContentType);
            }
        }

        return request;
    }
}

/// <summary>
/// Load balancer for distributing requests
/// </summary>
public class LoadBalancer
{
    private readonly ConcurrentDictionary<string, int> _roundRobinCounters;
    private readonly Random _random;

    public LoadBalancer()
    {
        _roundRobinCounters = new ConcurrentDictionary<string, int>();
        _random = new Random();
    }

    public string SelectEndpoint(string[] endpoints, LoadBalancingStrategy strategy = LoadBalancingStrategy.RoundRobin)
    {
        if (endpoints == null || endpoints.Length == 0)
            throw new InvalidOperationException("No endpoints available");

        return strategy switch
        {
            LoadBalancingStrategy.Random => endpoints[_random.Next(endpoints.Length)],
            LoadBalancingStrategy.RoundRobin => SelectRoundRobin(endpoints),
            LoadBalancingStrategy.LeastConnections => SelectLeastConnections(endpoints),
            _ => endpoints[0]
        };
    }

    private string SelectRoundRobin(string[] endpoints)
    {
        var key = string.Join(",", endpoints);
        var counter = _roundRobinCounters.AddOrUpdate(key, 0, (k, v) => (v + 1) % endpoints.Length);
        return endpoints[counter];
    }

    private string SelectLeastConnections(string[] endpoints)
    {
        // Simplified implementation - would need connection tracking in production
        return endpoints[_random.Next(endpoints.Length)];
    }
}

/// <summary>
/// Rate limiter for API throttling
/// </summary>
public class RateLimiter
{
    private readonly ConcurrentDictionary<string, RateLimitCounter> _counters;
    private readonly int _requestsPerMinute;

    public RateLimiter(int requestsPerMinute = 100)
    {
        _counters = new ConcurrentDictionary<string, RateLimitCounter>();
        _requestsPerMinute = requestsPerMinute;
    }

    public async Task<bool> AllowRequestAsync(HttpContext context)
    {
        var clientId = GetClientIdentifier(context);
        var counter = _counters.GetOrAdd(clientId, _ => new RateLimitCounter());
        
        return await counter.AllowRequestAsync(_requestsPerMinute);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Use IP address or authenticated user ID
        if (context.User?.Identity?.IsAuthenticated == true)
            return context.User.Identity.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        
        return context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
    }
}

/// <summary>
/// Metrics collector for monitoring
/// </summary>
public class MetricsCollector
{
    private readonly ConcurrentDictionary<string, ServiceMetrics> _metrics;

    public MetricsCollector()
    {
        _metrics = new ConcurrentDictionary<string, ServiceMetrics>();
    }

    public void RecordRequest(string serviceName, System.Net.HttpStatusCode statusCode, TimeSpan duration)
    {
        var metrics = _metrics.GetOrAdd(serviceName, _ => new ServiceMetrics());
        metrics.RecordRequest(statusCode, duration);
    }

    public void RecordRateLimitHit(string serviceName)
    {
        var metrics = _metrics.GetOrAdd(serviceName, _ => new ServiceMetrics());
        metrics.RateLimitHits++;
    }

    public Dictionary<string, ServiceMetrics> GetMetrics()
    {
        return new Dictionary<string, ServiceMetrics>(_metrics);
    }
}

/// <summary>
/// Authentication service for JWT validation
/// </summary>
public class AuthenticationService
{
    private readonly IConfiguration _configuration;
    private readonly byte[] _secretKey;

    public AuthenticationService(IConfiguration configuration)
    {
        _configuration = configuration;
        var secret = configuration["Gateway:JwtSecret"] ?? GenerateSecret();
        _secretKey = Encoding.UTF8.GetBytes(secret);
    }

    public async Task<bool> AuthenticateAsync(HttpContext context)
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return false;

        var token = authHeader.Substring("Bearer ".Length);
        
        // Simplified JWT validation - use proper JWT library in production
        return await ValidateTokenAsync(token);
    }

    private async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            // Implement proper JWT validation
            await Task.Delay(1); // Simulate async operation
            return !string.IsNullOrEmpty(token);
        }
        catch
        {
            return false;
        }
    }

    private string GenerateSecret()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[64];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}

// Supporting classes
public class ServiceEndpoint
{
    public string Name { get; set; } = string.Empty;
    public string[] Endpoints { get; set; } = Array.Empty<string>();
    public bool RequiresAuth { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

public class HealthStatus
{
    public bool IsHealthy { get; set; }
    public List<string> HealthyEndpoints { get; set; } = new();
    public List<string> UnhealthyEndpoints { get; set; } = new();
}

public class RateLimitCounter
{
    private readonly Queue<DateTime> _requestTimes = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<bool> AllowRequestAsync(int requestsPerMinute)
    {
        await _semaphore.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;
            var windowStart = now.AddMinutes(-1);

            // Remove old requests
            while (_requestTimes.Count > 0 && _requestTimes.Peek() < windowStart)
            {
                _requestTimes.Dequeue();
            }

            if (_requestTimes.Count >= requestsPerMinute)
                return false;

            _requestTimes.Enqueue(now);
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

public class ServiceMetrics
{
    private long _totalRequests;
    private long _successfulRequests;
    private long _failedRequests;
    private long _rateLimitHits;
    private double _totalDuration;

    public long TotalRequests => _totalRequests;
    public long SuccessfulRequests => _successfulRequests;
    public long FailedRequests => _failedRequests;
    public long RateLimitHits { get => _rateLimitHits; set => _rateLimitHits = value; }
    public double AverageResponseTime => _totalRequests > 0 ? _totalDuration / _totalRequests : 0;

    public void RecordRequest(System.Net.HttpStatusCode statusCode, TimeSpan duration)
    {
        Interlocked.Increment(ref _totalRequests);
        
        if ((int)statusCode >= 200 && (int)statusCode < 300)
            Interlocked.Increment(ref _successfulRequests);
        else
            Interlocked.Increment(ref _failedRequests);

        var currentTotal = _totalDuration;
        var newTotal = currentTotal + duration.TotalMilliseconds;
        while (Interlocked.CompareExchange(ref _totalDuration, newTotal, currentTotal) != currentTotal)
        {
            currentTotal = _totalDuration;
            newTotal = currentTotal + duration.TotalMilliseconds;
        }
    }
}

public enum LoadBalancingStrategy
{
    RoundRobin,
    Random,
    LeastConnections
}
