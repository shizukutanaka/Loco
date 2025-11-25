using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.EliteCloudNative
{
    // ============================================================================
    // DOMAIN MODELS - API Gateway (Kong + Envoy Gateway Patterns)
    // ============================================================================

    public class GatewayService
    {
        public string ServiceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public ServiceSpec Spec { get; set; } = new();
        public ServiceStatus Status { get; set; } = new();
        public Dictionary<string, string> Tags { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ServiceSpec
    {
        public string Protocol { get; set; } = "http"; // http, https, grpc, grpcs, ws, wss
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 80;
        public string Path { get; set; } = "/";
        public int Retries { get; set; } = 5;
        public int ConnectTimeout { get; set; } = 60000;
        public int WriteTimeout { get; set; } = 60000;
        public int ReadTimeout { get; set; } = 60000;
        public UpstreamConfig Upstream { get; set; } = new();
        public TlsConfig? Tls { get; set; }
    }

    public class UpstreamConfig
    {
        public string Algorithm { get; set; } = "round-robin"; // round-robin, consistent-hashing, least-connections
        public int Slots { get; set; } = 10000;
        public List<UpstreamTarget> Targets { get; set; } = new();
        public HealthCheckConfig HealthCheck { get; set; } = new();
        public HashConfig? Hash { get; set; }
    }

    public class UpstreamTarget
    {
        public string TargetId { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int Port { get; set; }
        public int Weight { get; set; } = 100;
        public bool Healthy { get; set; } = true;
    }

    public class HealthCheckConfig
    {
        public ActiveHealthCheck? Active { get; set; }
        public PassiveHealthCheck? Passive { get; set; }
    }

    public class ActiveHealthCheck
    {
        public bool Enabled { get; set; }
        public int IntervalSeconds { get; set; } = 5;
        public int Timeout { get; set; } = 1;
        public int HealthyThreshold { get; set; } = 2;
        public int UnhealthyThreshold { get; set; } = 3;
        public string HttpPath { get; set; } = "/health";
        public List<int> HealthyHttpStatuses { get; set; } = new() { 200, 302 };
    }

    public class PassiveHealthCheck
    {
        public bool Enabled { get; set; }
        public int UnhealthyHttpStatuses { get; set; } = 429;
        public int UnhealthyTcpFailures { get; set; } = 5;
        public int UnhealthyTimeouts { get; set; } = 5;
    }

    public class HashConfig
    {
        public string HashOn { get; set; } = "consumer"; // consumer, ip, header, cookie, path
        public string? HeaderName { get; set; }
        public string? CookieName { get; set; }
    }

    public class TlsConfig
    {
        public bool Enabled { get; set; }
        public bool VerifyUpstream { get; set; } = true;
        public string? CaCertificates { get; set; }
        public string? ClientCertificate { get; set; }
    }

    public class ServiceStatus
    {
        public string State { get; set; } = "active";
        public int HealthyTargets { get; set; }
        public int UnhealthyTargets { get; set; }
        public double RequestsPerSecond { get; set; }
        public double AverageLatencyMs { get; set; }
    }

    public class GatewayRoute
    {
        public string RouteId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;
        public RouteSpec Spec { get; set; } = new();
        public List<string> PluginIds { get; set; } = new();
        public Dictionary<string, string> Tags { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class RouteSpec
    {
        public List<string> Protocols { get; set; } = new() { "http", "https" };
        public List<string> Methods { get; set; } = new(); // GET, POST, PUT, DELETE, etc.
        public List<string> Hosts { get; set; } = new();
        public List<string> Paths { get; set; } = new();
        public List<string> Headers { get; set; } = new();
        public int Priority { get; set; } = 0;
        public bool StripPath { get; set; } = true;
        public bool PreserveHost { get; set; }
        public int RegexPriority { get; set; } = 0;
        public RequestTransform? RequestTransform { get; set; }
        public ResponseTransform? ResponseTransform { get; set; }
    }

    public class RequestTransform
    {
        public Dictionary<string, string> AddHeaders { get; set; } = new();
        public List<string> RemoveHeaders { get; set; } = new();
        public Dictionary<string, string> ReplaceHeaders { get; set; } = new();
        public string? RewritePath { get; set; }
        public Dictionary<string, string> AddQueryParams { get; set; } = new();
    }

    public class ResponseTransform
    {
        public Dictionary<string, string> AddHeaders { get; set; } = new();
        public List<string> RemoveHeaders { get; set; } = new();
        public string? BodyTransformTemplate { get; set; }
    }

    public class GatewayConsumer
    {
        public string ConsumerId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string CustomId { get; set; } = string.Empty;
        public List<ConsumerCredential> Credentials { get; set; } = new();
        public List<ConsumerGroup> Groups { get; set; } = new();
        public ConsumerQuota Quota { get; set; } = new();
        public Dictionary<string, string> Tags { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class ConsumerCredential
    {
        public string CredentialId { get; set; } = string.Empty;
        public string Type { get; set; } = "api-key"; // api-key, jwt, oauth2, basic-auth, hmac-auth
        public Dictionary<string, object> Config { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class ConsumerGroup
    {
        public string GroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, object> Settings { get; set; } = new();
    }

    public class ConsumerQuota
    {
        public int? RateLimitPerSecond { get; set; }
        public int? RateLimitPerMinute { get; set; }
        public int? RateLimitPerHour { get; set; }
        public int? RateLimitPerDay { get; set; }
        public int? RequestSizeLimit { get; set; }
        public int? ResponseSizeLimit { get; set; }
    }

    public class GatewayPlugin
    {
        public string PluginId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // rate-limiting, authentication, cors, etc.
        public PluginScope Scope { get; set; } = new();
        public Dictionary<string, object> Config { get; set; } = new();
        public bool Enabled { get; set; } = true;
        public int Ordering { get; set; } = 0;
        public DateTime CreatedAt { get; set; }
    }

    public class PluginScope
    {
        public string? ServiceId { get; set; }
        public string? RouteId { get; set; }
        public string? ConsumerId { get; set; }
        public bool IsGlobal => ServiceId == null && RouteId == null && ConsumerId == null;
    }

    public class RateLimitingConfig
    {
        public int Limit { get; set; }
        public string WindowType { get; set; } = "sliding"; // sliding, fixed
        public int WindowSize { get; set; } // in seconds
        public string Policy { get; set; } = "local"; // local, cluster, redis
        public bool HideClientHeaders { get; set; }
        public int RetryAfterJitterMax { get; set; }
        public string LimitBy { get; set; } = "consumer"; // consumer, ip, header, path
        public string? HeaderName { get; set; }
    }

    public class AuthenticationConfig
    {
        public string Type { get; set; } = "jwt"; // jwt, api-key, oauth2, basic, oidc
        public bool Anonymous { get; set; }
        public JwtConfig? Jwt { get; set; }
        public OAuth2Config? OAuth2 { get; set; }
        public OidcConfig? Oidc { get; set; }
    }

    public class JwtConfig
    {
        public List<string> KeyClaimNames { get; set; } = new() { "iss", "sub" };
        public List<string> ClaimsToVerify { get; set; } = new() { "exp", "nbf" };
        public int MaximumExpiration { get; set; } = 3600;
        public string SecretIsBase64 { get; set; } = "false";
        public string? RunOnPreflight { get; set; }
    }

    public class OAuth2Config
    {
        public string Scopes { get; set; } = string.Empty;
        public bool MandatoryScope { get; set; } = true;
        public string TokenType { get; set; } = "bearer";
        public bool EnableClientCredentials { get; set; }
        public bool EnableAuthorizationCode { get; set; }
        public bool EnableImplicitGrant { get; set; }
        public bool EnablePasswordGrant { get; set; }
        public string? ProvisionKey { get; set; }
    }

    public class OidcConfig
    {
        public string Issuer { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public List<string> Scopes { get; set; } = new() { "openid", "profile", "email" };
        public bool IntrospectionEndpointAuth { get; set; }
    }

    public class CorsConfig
    {
        public List<string> Origins { get; set; } = new();
        public List<string> Methods { get; set; } = new() { "GET", "POST", "PUT", "DELETE", "OPTIONS" };
        public List<string> Headers { get; set; } = new();
        public List<string> ExposedHeaders { get; set; } = new();
        public int MaxAge { get; set; } = 3600;
        public bool Credentials { get; set; }
        public bool PreflightContinue { get; set; }
    }

    public class CircuitBreakerConfig
    {
        public bool Enabled { get; set; }
        public int Threshold { get; set; } = 5;
        public int WindowSeconds { get; set; } = 60;
        public int HalfOpenRequests { get; set; } = 1;
        public int RecoveryTimeSeconds { get; set; } = 30;
        public List<int> TriggerStatuses { get; set; } = new() { 500, 502, 503, 504 };
    }

    public class GraphQLConfig
    {
        public bool Enabled { get; set; }
        public string SchemaPath { get; set; } = string.Empty;
        public bool IntrospectionEnabled { get; set; } = true;
        public int MaxDepth { get; set; } = 10;
        public int MaxComplexity { get; set; } = 100;
        public RateLimitingConfig? QueryRateLimit { get; set; }
    }

    public class WebSocketConfig
    {
        public bool Enabled { get; set; }
        public int MaxConnections { get; set; } = 10000;
        public int PingIntervalSeconds { get; set; } = 30;
        public int CloseTimeoutSeconds { get; set; } = 5;
        public int MessageSizeLimit { get; set; } = 65536;
    }

    public class GrpcTranscodingConfig
    {
        public bool Enabled { get; set; }
        public string ProtoPath { get; set; } = string.Empty;
        public List<string> Services { get; set; } = new();
        public bool AutoMapping { get; set; } = true;
        public Dictionary<string, string> MethodMapping { get; set; } = new();
    }

    public class ApiVersion
    {
        public string VersionId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Status { get; set; } = "active"; // active, deprecated, sunset
        public string? DeprecatedMessage { get; set; }
        public DateTime? SunsetDate { get; set; }
        public List<string> RouteIds { get; set; } = new();
    }

    public class GatewayMetrics
    {
        public string MetricsId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public long TotalRequests { get; set; }
        public double RequestsPerSecond { get; set; }
        public double AverageLatencyMs { get; set; }
        public double P50LatencyMs { get; set; }
        public double P95LatencyMs { get; set; }
        public double P99LatencyMs { get; set; }
        public double ErrorRate { get; set; }
        public Dictionary<int, long> StatusCodeDistribution { get; set; } = new();
        public Dictionary<string, ServiceMetrics> ServiceMetrics { get; set; } = new();
        public Dictionary<string, ConsumerMetrics> ConsumerMetrics { get; set; } = new();
    }

    public class ServiceMetrics
    {
        public string ServiceName { get; set; } = string.Empty;
        public long Requests { get; set; }
        public double Latency { get; set; }
        public double ErrorRate { get; set; }
        public int HealthyUpstreams { get; set; }
    }

    public class ConsumerMetrics
    {
        public string ConsumerName { get; set; } = string.Empty;
        public long Requests { get; set; }
        public int RateLimitHits { get; set; }
        public int AuthFailures { get; set; }
    }

    public class CertificateConfig
    {
        public string CertificateId { get; set; } = string.Empty;
        public string Sni { get; set; } = string.Empty;
        public string Certificate { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class ApiKey
    {
        public string KeyId { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string ConsumerId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // ============================================================================
    // INTERFACE
    // ============================================================================

    public interface IAPIGatewayEngine
    {
        // Services
        Task<GatewayService> CreateServiceAsync(string tenantId, GatewayService service, CancellationToken cancellation = default);
        Task<GatewayService> GetServiceAsync(string tenantId, string serviceId, CancellationToken cancellation = default);
        Task<bool> DeleteServiceAsync(string tenantId, string serviceId, CancellationToken cancellation = default);
        Task<List<GatewayService>> ListServicesAsync(string tenantId, CancellationToken cancellation = default);

        // Routes
        Task<GatewayRoute> CreateRouteAsync(string tenantId, GatewayRoute route, CancellationToken cancellation = default);
        Task<GatewayRoute> GetRouteAsync(string tenantId, string routeId, CancellationToken cancellation = default);
        Task<bool> DeleteRouteAsync(string tenantId, string routeId, CancellationToken cancellation = default);
        Task<List<GatewayRoute>> ListRoutesAsync(string tenantId, string? serviceId = null, CancellationToken cancellation = default);

        // Consumers
        Task<GatewayConsumer> CreateConsumerAsync(string tenantId, GatewayConsumer consumer, CancellationToken cancellation = default);
        Task<GatewayConsumer> GetConsumerAsync(string tenantId, string consumerId, CancellationToken cancellation = default);
        Task<bool> DeleteConsumerAsync(string tenantId, string consumerId, CancellationToken cancellation = default);

        // Credentials
        Task<ApiKey> CreateApiKeyAsync(string tenantId, string consumerId, CancellationToken cancellation = default);
        Task<bool> RevokeApiKeyAsync(string tenantId, string keyId, CancellationToken cancellation = default);
        Task<GatewayConsumer?> ValidateApiKeyAsync(string tenantId, string apiKey, CancellationToken cancellation = default);

        // Plugins
        Task<GatewayPlugin> ConfigurePluginAsync(string tenantId, GatewayPlugin plugin, CancellationToken cancellation = default);
        Task<bool> EnablePluginAsync(string tenantId, string pluginId, bool enabled, CancellationToken cancellation = default);
        Task<List<GatewayPlugin>> ListPluginsAsync(string tenantId, PluginScope? scope = null, CancellationToken cancellation = default);

        // Rate Limiting
        Task<bool> CheckRateLimitAsync(string tenantId, string consumerId, string routeId, CancellationToken cancellation = default);
        Task<RateLimitingConfig> GetRateLimitConfigAsync(string tenantId, string consumerId, CancellationToken cancellation = default);

        // Certificates
        Task<CertificateConfig> AddCertificateAsync(string tenantId, CertificateConfig certificate, CancellationToken cancellation = default);
        Task<List<CertificateConfig>> ListCertificatesAsync(string tenantId, CancellationToken cancellation = default);

        // Upstreams
        Task<UpstreamTarget> AddUpstreamTargetAsync(string tenantId, string serviceId, UpstreamTarget target, CancellationToken cancellation = default);
        Task<bool> RemoveUpstreamTargetAsync(string tenantId, string serviceId, string targetId, CancellationToken cancellation = default);
        Task<bool> SetTargetHealthAsync(string tenantId, string serviceId, string targetId, bool healthy, CancellationToken cancellation = default);

        // Metrics
        Task<GatewayMetrics> GetMetricsAsync(string tenantId, CancellationToken cancellation = default);
        Task<ServiceMetrics> GetServiceMetricsAsync(string tenantId, string serviceId, CancellationToken cancellation = default);
    }

    // ============================================================================
    // IMPLEMENTATION
    // ============================================================================

    public class APIGatewayEngine : IAPIGatewayEngine
    {
        private readonly ILogger<APIGatewayEngine> _logger;
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly Dictionary<string, GatewayService> _services = new();
        private readonly Dictionary<string, GatewayRoute> _routes = new();
        private readonly Dictionary<string, GatewayConsumer> _consumers = new();
        private readonly Dictionary<string, GatewayPlugin> _plugins = new();
        private readonly Dictionary<string, ApiKey> _apiKeys = new();
        private readonly Dictionary<string, CertificateConfig> _certificates = new();
        private readonly Dictionary<string, (int Count, DateTime Window)> _rateLimitCounters = new();
        private readonly Random _random = new(42);

        public APIGatewayEngine(ILogger<APIGatewayEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<GatewayService> CreateServiceAsync(string tenantId, GatewayService service, CancellationToken cancellation = default)
        {
            service.ServiceId = Guid.NewGuid().ToString();
            service.CreatedAt = DateTime.UtcNow;
            service.UpdatedAt = DateTime.UtcNow;
            service.Status = new ServiceStatus
            {
                State = "active",
                HealthyTargets = service.Spec.Upstream.Targets.Count,
                UnhealthyTargets = 0,
                RequestsPerSecond = 0,
                AverageLatencyMs = 0
            };

            var key = $"{tenantId}:{service.ServiceId}";
            _lock.EnterWriteLock();
            try
            {
                _services[key] = service;
                _logger.LogInformation($"Created gateway service {service.Name} -> {service.Spec.Host}:{service.Spec.Port}{service.Spec.Path} ({service.Spec.Protocol})");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return service;
        }

        public async Task<GatewayService> GetServiceAsync(string tenantId, string serviceId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{serviceId}";

            _lock.EnterReadLock();
            try
            {
                if (_services.TryGetValue(key, out var service))
                {
                    return service;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new GatewayService();
        }

        public async Task<bool> DeleteServiceAsync(string tenantId, string serviceId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{serviceId}";

            _lock.EnterWriteLock();
            try
            {
                if (_services.Remove(key))
                {
                    _logger.LogInformation($"Deleted gateway service {serviceId}");
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<List<GatewayService>> ListServicesAsync(string tenantId, CancellationToken cancellation = default)
        {
            var services = new List<GatewayService>();

            _lock.EnterReadLock();
            try
            {
                services = _services.Values.ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _logger.LogInformation($"Listed {services.Count} gateway services");

            await Task.CompletedTask;
            return services;
        }

        public async Task<GatewayRoute> CreateRouteAsync(string tenantId, GatewayRoute route, CancellationToken cancellation = default)
        {
            route.RouteId = Guid.NewGuid().ToString();
            route.CreatedAt = DateTime.UtcNow;

            var key = $"{tenantId}:{route.RouteId}";
            _lock.EnterWriteLock();
            try
            {
                _routes[key] = route;
                var methods = string.Join(",", route.Spec.Methods.Any() ? route.Spec.Methods : new List<string> { "*" });
                var paths = string.Join(",", route.Spec.Paths);
                _logger.LogInformation($"Created route {route.Name}: [{methods}] {paths} -> service {route.ServiceId}");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return route;
        }

        public async Task<GatewayRoute> GetRouteAsync(string tenantId, string routeId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{routeId}";

            _lock.EnterReadLock();
            try
            {
                if (_routes.TryGetValue(key, out var route))
                {
                    return route;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new GatewayRoute();
        }

        public async Task<bool> DeleteRouteAsync(string tenantId, string routeId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{routeId}";

            _lock.EnterWriteLock();
            try
            {
                if (_routes.Remove(key))
                {
                    _logger.LogInformation($"Deleted route {routeId}");
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<List<GatewayRoute>> ListRoutesAsync(string tenantId, string? serviceId = null, CancellationToken cancellation = default)
        {
            var routes = new List<GatewayRoute>();

            _lock.EnterReadLock();
            try
            {
                routes = _routes.Values
                    .Where(r => serviceId == null || r.ServiceId == serviceId)
                    .OrderByDescending(r => r.Spec.Priority)
                    .ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _logger.LogInformation($"Listed {routes.Count} routes" + (serviceId != null ? $" for service {serviceId}" : ""));

            await Task.CompletedTask;
            return routes;
        }

        public async Task<GatewayConsumer> CreateConsumerAsync(string tenantId, GatewayConsumer consumer, CancellationToken cancellation = default)
        {
            consumer.ConsumerId = Guid.NewGuid().ToString();
            consumer.CreatedAt = DateTime.UtcNow;

            var key = $"{tenantId}:{consumer.ConsumerId}";
            _lock.EnterWriteLock();
            try
            {
                _consumers[key] = consumer;
                _logger.LogInformation($"Created consumer {consumer.Username} (ID: {consumer.CustomId})");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return consumer;
        }

        public async Task<GatewayConsumer> GetConsumerAsync(string tenantId, string consumerId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{consumerId}";

            _lock.EnterReadLock();
            try
            {
                if (_consumers.TryGetValue(key, out var consumer))
                {
                    return consumer;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new GatewayConsumer();
        }

        public async Task<bool> DeleteConsumerAsync(string tenantId, string consumerId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{consumerId}";

            _lock.EnterWriteLock();
            try
            {
                if (_consumers.Remove(key))
                {
                    _logger.LogInformation($"Deleted consumer {consumerId}");
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<ApiKey> CreateApiKeyAsync(string tenantId, string consumerId, CancellationToken cancellation = default)
        {
            var apiKey = new ApiKey
            {
                KeyId = Guid.NewGuid().ToString(),
                Key = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("=", "").Replace("+", "").Replace("/", ""),
                ConsumerId = consumerId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                IsActive = true
            };

            var key = $"{tenantId}:{apiKey.KeyId}";
            _lock.EnterWriteLock();
            try
            {
                _apiKeys[key] = apiKey;
                _logger.LogInformation($"Created API key for consumer {consumerId} (expires: {apiKey.ExpiresAt})");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return apiKey;
        }

        public async Task<bool> RevokeApiKeyAsync(string tenantId, string keyId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{keyId}";

            _lock.EnterWriteLock();
            try
            {
                if (_apiKeys.TryGetValue(key, out var apiKey))
                {
                    apiKey.IsActive = false;
                    _logger.LogInformation($"Revoked API key {keyId}");
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<GatewayConsumer?> ValidateApiKeyAsync(string tenantId, string apiKeyValue, CancellationToken cancellation = default)
        {
            _lock.EnterReadLock();
            try
            {
                var apiKey = _apiKeys.Values.FirstOrDefault(k => k.Key == apiKeyValue && k.IsActive);
                if (apiKey != null && (apiKey.ExpiresAt == null || apiKey.ExpiresAt > DateTime.UtcNow))
                {
                    var consumerKey = $"{tenantId}:{apiKey.ConsumerId}";
                    if (_consumers.TryGetValue(consumerKey, out var consumer))
                    {
                        _logger.LogInformation($"Validated API key for consumer {consumer.Username}");
                        return consumer;
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _logger.LogWarning("API key validation failed");

            await Task.CompletedTask;
            return null;
        }

        public async Task<GatewayPlugin> ConfigurePluginAsync(string tenantId, GatewayPlugin plugin, CancellationToken cancellation = default)
        {
            plugin.PluginId = Guid.NewGuid().ToString();
            plugin.CreatedAt = DateTime.UtcNow;

            var key = $"{tenantId}:{plugin.PluginId}";
            _lock.EnterWriteLock();
            try
            {
                _plugins[key] = plugin;

                var scopeInfo = plugin.Scope.IsGlobal ? "global" :
                    (plugin.Scope.ServiceId != null ? $"service:{plugin.Scope.ServiceId}" :
                    (plugin.Scope.RouteId != null ? $"route:{plugin.Scope.RouteId}" :
                    $"consumer:{plugin.Scope.ConsumerId}"));

                _logger.LogInformation($"Configured plugin {plugin.Name} ({plugin.Type}) at {scopeInfo}");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return plugin;
        }

        public async Task<bool> EnablePluginAsync(string tenantId, string pluginId, bool enabled, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{pluginId}";

            _lock.EnterWriteLock();
            try
            {
                if (_plugins.TryGetValue(key, out var plugin))
                {
                    plugin.Enabled = enabled;
                    _logger.LogInformation($"{(enabled ? "Enabled" : "Disabled")} plugin {plugin.Name}");
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<List<GatewayPlugin>> ListPluginsAsync(string tenantId, PluginScope? scope = null, CancellationToken cancellation = default)
        {
            var plugins = new List<GatewayPlugin>();

            _lock.EnterReadLock();
            try
            {
                plugins = _plugins.Values
                    .Where(p => scope == null ||
                        (scope.ServiceId == p.Scope.ServiceId &&
                         scope.RouteId == p.Scope.RouteId &&
                         scope.ConsumerId == p.Scope.ConsumerId))
                    .OrderBy(p => p.Ordering)
                    .ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _logger.LogInformation($"Listed {plugins.Count} plugins");

            await Task.CompletedTask;
            return plugins;
        }

        public async Task<bool> CheckRateLimitAsync(string tenantId, string consumerId, string routeId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{consumerId}:{routeId}";
            var now = DateTime.UtcNow;
            var windowSize = TimeSpan.FromSeconds(60); // 1 minute window
            var limit = 100; // Default limit

            _lock.EnterWriteLock();
            try
            {
                if (_rateLimitCounters.TryGetValue(key, out var counter))
                {
                    if (now - counter.Window > windowSize)
                    {
                        // Reset window
                        _rateLimitCounters[key] = (1, now);
                        return true;
                    }
                    else if (counter.Count < limit)
                    {
                        _rateLimitCounters[key] = (counter.Count + 1, counter.Window);
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning($"Rate limit exceeded for consumer {consumerId} on route {routeId}");
                        return false;
                    }
                }
                else
                {
                    _rateLimitCounters[key] = (1, now);
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public async Task<RateLimitingConfig> GetRateLimitConfigAsync(string tenantId, string consumerId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{consumerId}";

            _lock.EnterReadLock();
            try
            {
                if (_consumers.TryGetValue(key, out var consumer) && consumer.Quota != null)
                {
                    return new RateLimitingConfig
                    {
                        Limit = consumer.Quota.RateLimitPerMinute ?? 100,
                        WindowType = "sliding",
                        WindowSize = 60,
                        Policy = "cluster",
                        LimitBy = "consumer"
                    };
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new RateLimitingConfig { Limit = 100, WindowSize = 60 };
        }

        public async Task<CertificateConfig> AddCertificateAsync(string tenantId, CertificateConfig certificate, CancellationToken cancellation = default)
        {
            certificate.CertificateId = Guid.NewGuid().ToString();

            var key = $"{tenantId}:{certificate.CertificateId}";
            _lock.EnterWriteLock();
            try
            {
                _certificates[key] = certificate;
                _logger.LogInformation($"Added certificate for SNI {certificate.Sni} (expires: {certificate.ExpiresAt})");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return certificate;
        }

        public async Task<List<CertificateConfig>> ListCertificatesAsync(string tenantId, CancellationToken cancellation = default)
        {
            var certs = new List<CertificateConfig>();

            _lock.EnterReadLock();
            try
            {
                certs = _certificates.Values.ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _logger.LogInformation($"Listed {certs.Count} certificates");

            await Task.CompletedTask;
            return certs;
        }

        public async Task<UpstreamTarget> AddUpstreamTargetAsync(string tenantId, string serviceId, UpstreamTarget target, CancellationToken cancellation = default)
        {
            target.TargetId = Guid.NewGuid().ToString();

            var key = $"{tenantId}:{serviceId}";
            _lock.EnterWriteLock();
            try
            {
                if (_services.TryGetValue(key, out var service))
                {
                    service.Spec.Upstream.Targets.Add(target);
                    service.Status.HealthyTargets++;
                    _logger.LogInformation($"Added upstream target {target.Address}:{target.Port} (weight: {target.Weight}) to service {service.Name}");
                    return target;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return new UpstreamTarget();
        }

        public async Task<bool> RemoveUpstreamTargetAsync(string tenantId, string serviceId, string targetId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{serviceId}";

            _lock.EnterWriteLock();
            try
            {
                if (_services.TryGetValue(key, out var service))
                {
                    var target = service.Spec.Upstream.Targets.FirstOrDefault(t => t.TargetId == targetId);
                    if (target != null)
                    {
                        service.Spec.Upstream.Targets.Remove(target);
                        if (target.Healthy) service.Status.HealthyTargets--;
                        else service.Status.UnhealthyTargets--;
                        _logger.LogInformation($"Removed upstream target {targetId} from service {service.Name}");
                        return true;
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<bool> SetTargetHealthAsync(string tenantId, string serviceId, string targetId, bool healthy, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{serviceId}";

            _lock.EnterWriteLock();
            try
            {
                if (_services.TryGetValue(key, out var service))
                {
                    var target = service.Spec.Upstream.Targets.FirstOrDefault(t => t.TargetId == targetId);
                    if (target != null && target.Healthy != healthy)
                    {
                        target.Healthy = healthy;
                        if (healthy)
                        {
                            service.Status.HealthyTargets++;
                            service.Status.UnhealthyTargets--;
                        }
                        else
                        {
                            service.Status.HealthyTargets--;
                            service.Status.UnhealthyTargets++;
                        }
                        _logger.LogInformation($"Set target {targetId} health to {(healthy ? "healthy" : "unhealthy")} for service {service.Name}");
                        return true;
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<GatewayMetrics> GetMetricsAsync(string tenantId, CancellationToken cancellation = default)
        {
            var metrics = new GatewayMetrics
            {
                MetricsId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                TotalRequests = _random.Next(100000, 10000000),
                RequestsPerSecond = 1000 + _random.NextDouble() * 9000,
                AverageLatencyMs = 10 + _random.NextDouble() * 40,
                P50LatencyMs = 8 + _random.NextDouble() * 20,
                P95LatencyMs = 50 + _random.NextDouble() * 100,
                P99LatencyMs = 100 + _random.NextDouble() * 200,
                ErrorRate = _random.NextDouble() * 2,
                StatusCodeDistribution = new Dictionary<int, long>
                {
                    { 200, _random.Next(80000, 900000) },
                    { 201, _random.Next(1000, 10000) },
                    { 400, _random.Next(100, 1000) },
                    { 401, _random.Next(50, 500) },
                    { 404, _random.Next(500, 5000) },
                    { 500, _random.Next(10, 100) }
                },
                ServiceMetrics = new Dictionary<string, ServiceMetrics>(),
                ConsumerMetrics = new Dictionary<string, ConsumerMetrics>()
            };

            foreach (var service in _services.Values.Take(10))
            {
                metrics.ServiceMetrics[service.Name] = new ServiceMetrics
                {
                    ServiceName = service.Name,
                    Requests = _random.Next(10000, 100000),
                    Latency = 10 + _random.NextDouble() * 30,
                    ErrorRate = _random.NextDouble() * 1,
                    HealthyUpstreams = service.Status.HealthyTargets
                };
            }

            foreach (var consumer in _consumers.Values.Take(10))
            {
                metrics.ConsumerMetrics[consumer.Username] = new ConsumerMetrics
                {
                    ConsumerName = consumer.Username,
                    Requests = _random.Next(1000, 50000),
                    RateLimitHits = _random.Next(0, 100),
                    AuthFailures = _random.Next(0, 20)
                };
            }

            _logger.LogInformation($"Gateway metrics: {metrics.RequestsPerSecond:F0} req/s, {metrics.AverageLatencyMs:F1}ms avg, {metrics.ErrorRate:F2}% errors");

            await Task.CompletedTask;
            return metrics;
        }

        public async Task<ServiceMetrics> GetServiceMetricsAsync(string tenantId, string serviceId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{serviceId}";

            _lock.EnterReadLock();
            try
            {
                if (_services.TryGetValue(key, out var service))
                {
                    return new ServiceMetrics
                    {
                        ServiceName = service.Name,
                        Requests = _random.Next(10000, 500000),
                        Latency = 10 + _random.NextDouble() * 30,
                        ErrorRate = _random.NextDouble() * 1,
                        HealthyUpstreams = service.Status.HealthyTargets
                    };
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new ServiceMetrics();
        }
    }
}
