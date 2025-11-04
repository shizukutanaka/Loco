#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.ServiceMesh;

/// <summary>
/// Service Mesh Patterns - Istio, Ambient Mesh, Traffic Management
/// Provides infrastructure layer for service-to-service communication
/// </summary>

/// <summary>
/// Virtual Service - traffic routing rules
/// </summary>
public class VirtualService
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = "default";

    [JsonPropertyName("hosts")]
    public List<string> Hosts { get; set; } = new();

    [JsonPropertyName("http")]
    public List<HttpRoute> Http { get; set; } = new();

    [JsonPropertyName("tcp")]
    public List<TcpRoute> Tcp { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// HTTP route with traffic rules
/// </summary>
public class HttpRoute
{
    [JsonPropertyName("match")]
    public List<HttpRouteMatch> Match { get; set; } = new();

    [JsonPropertyName("route")]
    public List<HttpRouteDestination> Route { get; set; } = new();

    [JsonPropertyName("timeout")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    [JsonPropertyName("retries")]
    public RetryPolicy? Retries { get; set; }

    [JsonPropertyName("corsPolicy")]
    public CorsPolicy? CorsPolicy { get; set; }
}

/// <summary>
/// HTTP route match conditions
/// </summary>
public class HttpRouteMatch
{
    [JsonPropertyName("uri")]
    public StringMatch? Uri { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, StringMatch>? Headers { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("sourceLabels")]
    public Dictionary<string, string>? SourceLabels { get; set; }
}

/// <summary>
/// String matching with prefix, exact, regex
/// </summary>
public class StringMatch
{
    [JsonPropertyName("exact")]
    public string? Exact { get; set; }

    [JsonPropertyName("prefix")]
    public string? Prefix { get; set; }

    [JsonPropertyName("regex")]
    public string? Regex { get; set; }
}

/// <summary>
/// HTTP route destination with weight-based routing
/// </summary>
public class HttpRouteDestination
{
    [JsonPropertyName("destination")]
    public Destination Destination { get; set; } = new();

    [JsonPropertyName("weight")]
    public int Weight { get; set; } = 100; // 0-100 for traffic splitting

    [JsonPropertyName("timeout")]
    public TimeSpan? Timeout { get; set; }
}

/// <summary>
/// Destination service
/// </summary>
public class Destination
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("subset")]
    public string? Subset { get; set; } // For canary: v1, v2, etc

    [JsonPropertyName("port")]
    public int? Port { get; set; }
}

/// <summary>
/// Destination Rule - load balancing, connection pool settings
/// </summary>
public class DestinationRule
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("trafficPolicy")]
    public TrafficPolicy? TrafficPolicy { get; set; }

    [JsonPropertyName("subsets")]
    public List<Subset> Subsets { get; set; } = new();
}

/// <summary>
/// Traffic policy - load balancing, outlier detection
/// </summary>
public class TrafficPolicy
{
    [JsonPropertyName("connectionPool")]
    public ConnectionPool? ConnectionPool { get; set; }

    [JsonPropertyName("loadBalancer")]
    public LoadBalancer? LoadBalancer { get; set; }

    [JsonPropertyName("outlierDetection")]
    public OutlierDetection? OutlierDetection { get; set; }
}

/// <summary>
/// Connection pool settings
/// </summary>
public class ConnectionPool
{
    [JsonPropertyName("maxConnections")]
    public int MaxConnections { get; set; } = 100;

    [JsonPropertyName("http")]
    public HttpConnectionPool? Http { get; set; }
}

/// <summary>
/// HTTP connection pool
/// </summary>
public class HttpConnectionPool
{
    [JsonPropertyName("http1MaxPendingRequests")]
    public int Http1MaxPendingRequests { get; set; } = 100;

    [JsonPropertyName("http2MaxRequests")]
    public int Http2MaxRequests { get; set; } = 1000;

    [JsonPropertyName("maxRequestsPerConnection")]
    public int MaxRequestsPerConnection { get; set; } = 2;
}

/// <summary>
/// Load balancer configuration
/// </summary>
public class LoadBalancer
{
    [JsonPropertyName("simple")]
    public string Simple { get; set; } = "ROUND_ROBIN"; // ROUND_ROBIN, LEAST_REQUEST, RANDOM, PASSTHROUGH

    [JsonPropertyName("consistentHash")]
    public ConsistentHashLb? ConsistentHash { get; set; }
}

/// <summary>
/// Consistent hash load balancing
/// </summary>
public class ConsistentHashLb
{
    [JsonPropertyName("httpHeaderName")]
    public string? HttpHeaderName { get; set; }

    [JsonPropertyName("httpCookie")]
    public HttpCookie? HttpCookie { get; set; }

    [JsonPropertyName("minimumRingSize")]
    public int MinimumRingSize { get; set; } = 1024;
}

/// <summary>
/// HTTP cookie for consistent hashing
/// </summary>
public class HttpCookie
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = "/";

    [JsonPropertyName("ttl")]
    public TimeSpan Ttl { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// Outlier detection for circuit breaking
/// </summary>
public class OutlierDetection
{
    [JsonPropertyName("consecutiveErrors")]
    public int ConsecutiveErrors { get; set; } = 5;

    [JsonPropertyName("interval")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    [JsonPropertyName("baseEjectionTime")]
    public TimeSpan BaseEjectionTime { get; set; } = TimeSpan.FromSeconds(30);

    [JsonPropertyName("maxEjectionPercent")]
    public int MaxEjectionPercent { get; set; } = 50;

    [JsonPropertyName("minEjectionDuration")]
    public TimeSpan MinEjectionDuration { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Subset for canary deployments
/// </summary>
public class Subset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("labels")]
    public Dictionary<string, string> Labels { get; set; } = new();
}

/// <summary>
/// CORS policy
/// </summary>
public class CorsPolicy
{
    [JsonPropertyName("allowOrigins")]
    public List<string> AllowOrigins { get; set; } = new();

    [JsonPropertyName("allowMethods")]
    public List<string> AllowMethods { get; set; } = new() { "GET", "POST", "PUT", "DELETE" };

    [JsonPropertyName("allowHeaders")]
    public List<string> AllowHeaders { get; set; } = new();

    [JsonPropertyName("maxAge")]
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>
/// Retry policy
/// </summary>
public class RetryPolicy
{
    [JsonPropertyName("attempts")]
    public int Attempts { get; set; } = 3;

    [JsonPropertyName("perTryTimeout")]
    public TimeSpan PerTryTimeout { get; set; } = TimeSpan.FromSeconds(10);

    [JsonPropertyName("retryOn")]
    public string RetryOn { get; set; } = "5xx,reset,connect-failure,retriable-4xx";
}

/// <summary>
/// Peer Authentication - mTLS enforcement
/// </summary>
public class PeerAuthentication
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = "default";

    [JsonPropertyName("mtls")]
    public MtlsMode Mtls { get; set; } = MtlsMode.Strict;

    [JsonPropertyName("portLevelMtls")]
    public Dictionary<int, MtlsMode> PortLevelMtls { get; set; } = new();
}

/// <summary>
/// mTLS mode
/// </summary>
public enum MtlsMode
{
    Unset = 0,
    Disable = 1,
    Permissive = 2,
    Strict = 3
}

/// <summary>
/// Service mesh traffic manager
/// </summary>
public class ServiceMeshTrafficManager
{
    private readonly Dictionary<string, VirtualService> _virtualServices = new();
    private readonly Dictionary<string, DestinationRule> _destinationRules = new();
    private readonly Dictionary<string, PeerAuthentication> _peerAuthentications = new();
    private readonly ILogger<ServiceMeshTrafficManager> _logger;

    public ServiceMeshTrafficManager(ILogger<ServiceMeshTrafficManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Create virtual service for traffic routing
    /// </summary>
    public async Task CreateVirtualServiceAsync(VirtualService virtualService)
    {
        _virtualServices[virtualService.Name] = virtualService;

        _logger.LogInformation(
            "Created virtual service: {Name} with {RoutesCount} HTTP routes",
            virtualService.Name,
            virtualService.Http.Count);
    }

    /// <summary>
    /// Create destination rule with load balancing
    /// </summary>
    public async Task CreateDestinationRuleAsync(DestinationRule destinationRule)
    {
        _destinationRules[destinationRule.Name] = destinationRule;

        _logger.LogInformation(
            "Created destination rule: {Name} with {SubsetsCount} subsets",
            destinationRule.Name,
            destinationRule.Subsets.Count);
    }

    /// <summary>
    /// Enable mTLS for namespace
    /// </summary>
    public async Task EnableMtlsAsync(PeerAuthentication peerAuth)
    {
        _peerAuthentications[peerAuth.Name] = peerAuth;

        _logger.LogInformation(
            "Enabled mTLS in namespace {Namespace} with mode {Mode}",
            peerAuth.Namespace,
            peerAuth.Mtls);
    }

    /// <summary>
    /// Route traffic with weight-based canary
    /// </summary>
    public async Task<string> RouteCanaryTrafficAsync(
        string serviceName,
        string sourceNamespace,
        int canaryWeight = 10)
    {
        if (!_virtualServices.TryGetValue(serviceName, out var virtualService))
        {
            return "error: virtual service not found";
        }

        var route = virtualService.Http.FirstOrDefault();
        if (route == null)
        {
            return "error: no routes defined";
        }

        // Clear existing routes
        route.Route.Clear();

        // Add stable version (90% traffic)
        route.Route.Add(new HttpRouteDestination
        {
            Destination = new() { Host = serviceName, Subset = "stable" },
            Weight = 100 - canaryWeight
        });

        // Add canary version (10% traffic)
        route.Route.Add(new HttpRouteDestination
        {
            Destination = new() { Host = serviceName, Subset = "canary" },
            Weight = canaryWeight
        });

        _logger.LogInformation(
            "Configured canary routing: {Service} - stable:{Stable}% canary:{Canary}%",
            serviceName,
            100 - canaryWeight,
            canaryWeight);

        return "success";
    }

    /// <summary>
    /// Get virtual service metrics
    /// </summary>
    public Dictionary<string, object> GetServiceMeshStats()
    {
        return new()
        {
            ["virtualServicesCount"] = _virtualServices.Count,
            ["destinationRulesCount"] = _destinationRules.Count,
            ["mTlsEnabledNamespaces"] = _peerAuthentications.Count
        };
    }
}

/// <summary>
/// Ambient Mesh - sidecar-less service mesh
/// Simplified data plane without sidecars
/// </summary>
public class AmbientMeshController
{
    private readonly HashSet<string> _enrolledNamespaces = new();
    private readonly Dictionary<string, string> _waypoints = new(); // namespace -> waypoint
    private readonly ILogger<AmbientMeshController> _logger;

    public AmbientMeshController(ILogger<AmbientMeshController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Enroll namespace in ambient mesh
    /// </summary>
    public async Task EnrollNamespaceAsync(string @namespace)
    {
        _enrolledNamespaces.Add(@namespace);

        _logger.LogInformation(
            "Enrolled namespace in ambient mesh: {Namespace}",
            @namespace);
    }

    /// <summary>
    /// Create waypoint proxy (lightweight traffic management)
    /// </summary>
    public async Task CreateWaypointAsync(string @namespace, string waypointName)
    {
        _waypoints[@namespace] = waypointName;

        _logger.LogInformation(
            "Created waypoint proxy: {Namespace}/{Waypoint}",
            @namespace,
            waypointName);
    }

    /// <summary>
    /// Check if namespace is in ambient mesh
    /// </summary>
    public bool IsNamespaceEnrolled(string @namespace)
    {
        return _enrolledNamespaces.Contains(@namespace);
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class ServiceMeshExtensions
{
    public static IServiceCollection AddServiceMesh(this IServiceCollection services)
    {
        services.AddSingleton<ServiceMeshTrafficManager>();
        services.AddSingleton<AmbientMeshController>();
        return services;
    }
}
