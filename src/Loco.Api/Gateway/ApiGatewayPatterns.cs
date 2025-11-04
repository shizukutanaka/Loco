#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Api.Gateway;

/// <summary>
/// API Gateway Patterns - Kong, Tyk, Rate Limiting, Authentication, Routing
/// </summary>

/// <summary>
/// API route definition
/// </summary>
public class ApiRoute
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("paths")]
    public List<string> Paths { get; set; } = new();

    [JsonPropertyName("methods")]
    public List<string> Methods { get; set; } = new() { "GET", "POST", "PUT", "DELETE" };

    [JsonPropertyName("service")]
    public ServiceEndpoint Service { get; set; } = new();

    [JsonPropertyName("plugins")]
    public List<string> Plugins { get; set; } = new();

    [JsonPropertyName("stripPath")]
    public bool StripPath { get; set; } = true;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Service endpoint
/// </summary>
public class ServiceEndpoint
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "http";

    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; } = 80;

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 60000; // milliseconds
}

/// <summary>
/// Rate limiting configuration
/// </summary>
public class RateLimitPolicy
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "Token Bucket"; // Token Bucket, Sliding Window, Fixed Window

    [JsonPropertyName("requests")]
    public int Requests { get; set; } = 100;

    [JsonPropertyName("windowSeconds")]
    public int WindowSeconds { get; set; } = 60;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "Consumer"; // Consumer, Route, Service, Global

    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = new();
}

/// <summary>
/// Consumer/API key holder
/// </summary>
public class ApiConsumer
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("customId")]
    public string? CustomId { get; set; }

    [JsonPropertyName("apiKeys")]
    public List<string> ApiKeys { get; set; } = new();

    [JsonPropertyName("oauth2Clients")]
    public List<string> Oauth2Clients { get; set; } = new();

    [JsonPropertyName("rateLimit")]
    public RateLimitPolicy? RateLimit { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Gateway plugin base
/// </summary>
public abstract class GatewayPlugin
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("config")]
    public Dictionary<string, object> Config { get; set; } = new();

    public abstract Task<bool> ProcessRequestAsync(HttpRequest request);
    public abstract Task<bool> ProcessResponseAsync(HttpResponse response);
}

/// <summary>
/// Rate limiting plugin
/// </summary>
public class RateLimitingPlugin : GatewayPlugin
{
    private readonly Dictionary<string, RateLimitBucket> _buckets = new();
    private readonly ILogger<RateLimitingPlugin> _logger;

    public RateLimitingPlugin(ILogger<RateLimitingPlugin> logger)
    {
        _logger = logger;
        Name = "Rate Limiting";
    }

    public override async Task<bool> ProcessRequestAsync(HttpRequest request)
    {
        var consumerId = request.Headers["X-Consumer-ID"].ToString();
        if (string.IsNullOrEmpty(consumerId))
            return true; // Not rate limited

        if (!_buckets.TryGetValue(consumerId, out var bucket))
        {
            bucket = new RateLimitBucket
            {
                RequestsRemaining = 100,
                ResetTime = DateTime.UtcNow.AddMinutes(1)
            };
        }

        if (bucket.IsExpired)
        {
            bucket = new RateLimitBucket
            {
                RequestsRemaining = 100,
                ResetTime = DateTime.UtcNow.AddMinutes(1)
            };
        }

        if (bucket.RequestsRemaining <= 0)
        {
            _logger.LogWarning("Rate limit exceeded for consumer {ConsumerId}", consumerId);
            return false; // Block request
        }

        bucket.RequestsRemaining--;
        _buckets[consumerId] = bucket;

        return true; // Allow request
    }

    public override async Task<bool> ProcessResponseAsync(HttpResponse response)
    {
        return true;
    }
}

/// <summary>
/// Rate limit bucket (token bucket algorithm)
/// </summary>
public class RateLimitBucket
{
    public int RequestsRemaining { get; set; }
    public DateTime ResetTime { get; set; }

    public bool IsExpired => DateTime.UtcNow > ResetTime;
}

/// <summary>
/// Authentication plugin
/// </summary>
public class AuthenticationPlugin : GatewayPlugin
{
    private readonly Dictionary<string, ApiConsumer> _consumers = new();
    private readonly ILogger<AuthenticationPlugin> _logger;

    public AuthenticationPlugin(ILogger<AuthenticationPlugin> logger)
    {
        _logger = logger;
        Name = "Authentication";
    }

    public void RegisterConsumer(ApiConsumer consumer)
    {
        _consumers[consumer.Id] = consumer;
    }

    public override async Task<bool> ProcessRequestAsync(HttpRequest request)
    {
        // Check API key
        var apiKey = request.Headers["X-API-Key"].ToString();
        if (!string.IsNullOrEmpty(apiKey))
        {
            var consumer = _consumers.Values.FirstOrDefault(c => c.ApiKeys.Contains(apiKey));
            if (consumer != null)
            {
                request.HttpContext.Items["ConsumerId"] = consumer.Id;
                request.HttpContext.Items["Consumer"] = consumer;
                return true;
            }
        }

        // Check OAuth2 token
        var authHeader = request.Headers["Authorization"].ToString();
        if (authHeader.StartsWith("Bearer "))
        {
            var token = authHeader.Substring(7);
            // Validate token
            var consumer = _consumers.Values.FirstOrDefault(c => c.Oauth2Clients.Contains(token));
            if (consumer != null)
            {
                request.HttpContext.Items["ConsumerId"] = consumer.Id;
                return true;
            }
        }

        _logger.LogWarning("Authentication failed for request");
        return false;
    }

    public override async Task<bool> ProcessResponseAsync(HttpResponse response)
    {
        return true;
    }
}

/// <summary>
/// Logging plugin
/// </summary>
public class LoggingPlugin : GatewayPlugin
{
    private readonly ILogger<LoggingPlugin> _logger;

    public LoggingPlugin(ILogger<LoggingPlugin> logger)
    {
        _logger = logger;
        Name = "Logging";
    }

    public override async Task<bool> ProcessRequestAsync(HttpRequest request)
    {
        _logger.LogInformation(
            "API Request: {Method} {Path} from {RemoteIp}",
            request.Method,
            request.Path,
            request.HttpContext.Connection.RemoteIpAddress);

        return true;
    }

    public override async Task<bool> ProcessResponseAsync(HttpResponse response)
    {
        _logger.LogInformation(
            "API Response: Status {Status}",
            response.StatusCode);

        return true;
    }
}

/// <summary>
/// API Gateway
/// </summary>
public class ApiGateway
{
    private readonly Dictionary<string, ApiRoute> _routes = new();
    private readonly Dictionary<string, ApiConsumer> _consumers = new();
    private readonly Dictionary<string, GatewayPlugin> _plugins = new();
    private readonly ILogger<ApiGateway> _logger;

    public ApiGateway(ILogger<ApiGateway> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register API route
    /// </summary>
    public async Task RegisterRouteAsync(ApiRoute route)
    {
        _routes[route.Id] = route;

        _logger.LogInformation(
            "Registered API route: {Name} ({Methods}) → {Service}",
            route.Name,
            string.Join(",", route.Methods),
            route.Service.Url);
    }

    /// <summary>
    /// Register consumer
    /// </summary>
    public async Task RegisterConsumerAsync(ApiConsumer consumer)
    {
        _consumers[consumer.Id] = consumer;

        _logger.LogInformation(
            "Registered consumer: {Username}",
            consumer.Username);
    }

    /// <summary>
    /// Register plugin
    /// </summary>
    public async Task RegisterPluginAsync(GatewayPlugin plugin)
    {
        _plugins[plugin.Id] = plugin;

        _logger.LogInformation(
            "Registered plugin: {Name}",
            plugin.Name);
    }

    /// <summary>
    /// Route request
    /// </summary>
    public async Task<(bool allowed, ApiRoute? route)> RouteRequestAsync(
        HttpRequest request,
        string method,
        string path)
    {
        // Execute plugins
        foreach (var plugin in _plugins.Values.Where(p => p.Enabled))
        {
            if (!await plugin.ProcessRequestAsync(request))
            {
                return (false, null);
            }
        }

        // Find matching route
        var route = _routes.Values.FirstOrDefault(r =>
            r.Enabled &&
            r.Methods.Contains(method) &&
            r.Paths.Any(p => path.StartsWith(p)));

        if (route == null)
        {
            _logger.LogWarning("No matching route for {Method} {Path}", method, path);
            return (false, null);
        }

        _logger.LogInformation(
            "Routed request: {Method} {Path} → {Service}",
            method,
            path,
            route.Service.Url);

        return (true, route);
    }

    /// <summary>
    /// Get gateway stats
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        return new()
        {
            ["routesCount"] = _routes.Count,
            ["consumersCount"] = _consumers.Count,
            ["pluginsCount"] = _plugins.Count,
            ["enabledRoutes"] = _routes.Values.Count(r => r.Enabled)
        };
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class ApiGatewayExtensions
{
    public static IServiceCollection AddApiGateway(this IServiceCollection services)
    {
        services.AddSingleton<ApiGateway>();
        services.AddSingleton<RateLimitingPlugin>();
        services.AddSingleton<AuthenticationPlugin>();
        services.AddSingleton<LoggingPlugin>();
        return services;
    }
}
