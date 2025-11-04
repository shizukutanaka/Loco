#nullable enable

using System.Collections.Concurrent;
using System.Net;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Kubernetes;

/// <summary>
/// Kubernetes Networking & Service Discovery Patterns
/// Network policies, service discovery, advanced routing
/// </summary>

/// <summary>
/// Kubernetes service definition
/// </summary>
public class KubernetesService
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = "default";

    [JsonPropertyName("clusterIp")]
    public string? ClusterIp { get; set; }

    [JsonPropertyName("type")]
    public ServiceType Type { get; set; } = ServiceType.ClusterIP;

    [JsonPropertyName("selector")]
    public Dictionary<string, string> Selector { get; set; } = new();

    [JsonPropertyName("ports")]
    public List<ServicePort> Ports { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Kubernetes service type
/// </summary>
public enum ServiceType
{
    /// <summary>
    /// Exposes service on cluster-internal IP (default)
    /// </summary>
    ClusterIP,

    /// <summary>
    /// Exposes service on static port of each node
    /// </summary>
    NodePort,

    /// <summary>
    /// Exposes service via cloud provider load balancer
    /// </summary>
    LoadBalancer,

    /// <summary>
    /// Maps service to external DNS name
    /// </summary>
    ExternalName
}

/// <summary>
/// Service port definition
/// </summary>
public class ServicePort
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "TCP";

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("targetPort")]
    public int TargetPort { get; set; }

    [JsonPropertyName("nodePort")]
    public int? NodePort { get; set; }
}

/// <summary>
/// Kubernetes endpoint - actual pod IPs behind service
/// </summary>
public class Endpoint
{
    [JsonPropertyName("addresses")]
    public List<string> Addresses { get; set; } = new();

    [JsonPropertyName("ports")]
    public List<ServicePort> Ports { get; set; } = new();

    [JsonPropertyName("readyAddresses")]
    public List<string> ReadyAddresses { get; set; } = new();

    [JsonPropertyName("notReadyAddresses")]
    public List<string> NotReadyAddresses { get; set; } = new();
}

/// <summary>
/// Kubernetes network policy - security at IP/port level
/// </summary>
public class KubernetesNetworkPolicy
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = "default";

    [JsonPropertyName("podSelector")]
    public Dictionary<string, string> PodSelector { get; set; } = new();

    [JsonPropertyName("policyTypes")]
    public List<string> PolicyTypes { get; set; } = new(); // Ingress, Egress

    [JsonPropertyName("ingress")]
    public List<IngressRule> Ingress { get; set; } = new();

    [JsonPropertyName("egress")]
    public List<EgressRule> Egress { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Ingress rule for network policy
/// </summary>
public class IngressRule
{
    [JsonPropertyName("from")]
    public List<PeerSelector> From { get; set; } = new();

    [JsonPropertyName("ports")]
    public List<PolicyPort> Ports { get; set; } = new();
}

/// <summary>
/// Egress rule for network policy
/// </summary>
public class EgressRule
{
    [JsonPropertyName("to")]
    public List<PeerSelector> To { get; set; } = new();

    [JsonPropertyName("ports")]
    public List<PolicyPort> Ports { get; set; } = new();
}

/// <summary>
/// Peer selector for network policy rules
/// </summary>
public class PeerSelector
{
    [JsonPropertyName("podSelector")]
    public Dictionary<string, string>? PodSelector { get; set; }

    [JsonPropertyName("namespaceSelector")]
    public Dictionary<string, string>? NamespaceSelector { get; set; }

    [JsonPropertyName("ipBlock")]
    public IpBlock? IpBlock { get; set; }
}

/// <summary>
/// IP block for network policy
/// </summary>
public class IpBlock
{
    [JsonPropertyName("cidr")]
    public string Cidr { get; set; } = string.Empty;

    [JsonPropertyName("except")]
    public List<string> Except { get; set; } = new();
}

/// <summary>
/// Policy port definition
/// </summary>
public class PolicyPort
{
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "TCP";

    [JsonPropertyName("port")]
    public int? Port { get; set; }
}

/// <summary>
/// Service discovery manager
/// Maintains mapping of services to endpoints
/// </summary>
public class ServiceDiscoveryManager
{
    private readonly ConcurrentDictionary<string, KubernetesService> _services = new();
    private readonly ConcurrentDictionary<string, Endpoint> _endpoints = new();
    private readonly ILogger<ServiceDiscoveryManager> _logger;

    public ServiceDiscoveryManager(ILogger<ServiceDiscoveryManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register service
    /// </summary>
    public async Task RegisterServiceAsync(KubernetesService service)
    {
        _services[$"{service.Namespace}/{service.Name}"] = service;

        // Assign cluster IP if not provided
        if (string.IsNullOrEmpty(service.ClusterIp))
        {
            service.ClusterIp = GenerateClusterIp();
        }

        _logger.LogInformation(
            "Registered service {Name} in namespace {Namespace}: {ClusterIp}",
            service.Name,
            service.Namespace,
            service.ClusterIp);
    }

    /// <summary>
    /// Register endpoints for service
    /// </summary>
    public async Task RegisterEndpointsAsync(string serviceName, string @namespace, Endpoint endpoint)
    {
        var key = $"{@namespace}/{serviceName}";
        _endpoints[key] = endpoint;

        _logger.LogInformation(
            "Registered {Count} endpoints for service {Service}: {Addresses}",
            endpoint.ReadyAddresses.Count,
            serviceName,
            string.Join(", ", endpoint.ReadyAddresses));
    }

    /// <summary>
    /// Discover service - returns service and endpoints
    /// </summary>
    public async Task<(KubernetesService?, Endpoint?)> DiscoverServiceAsync(string serviceName, string @namespace)
    {
        var serviceKey = $"{@namespace}/{serviceName}";
        var endpointKey = serviceKey;

        _services.TryGetValue(serviceKey, out var service);
        _endpoints.TryGetValue(endpointKey, out var endpoint);

        if (service != null && endpoint != null)
        {
            _logger.LogInformation(
                "Discovered service {Service}: {Ready} ready, {NotReady} not ready",
                serviceName,
                endpoint.ReadyAddresses.Count,
                endpoint.NotReadyAddresses.Count);
        }

        return (service, endpoint);
    }

    /// <summary>
    /// Get service DNS name (Kubernetes DNS format)
    /// </summary>
    public string GetServiceDnsName(string serviceName, string @namespace, string? clusterDomain = null)
    {
        clusterDomain ??= "cluster.local";
        return $"{serviceName}.{@namespace}.svc.{clusterDomain}";
    }

    /// <summary>
    /// List all services
    /// </summary>
    public List<KubernetesService> ListServices(string? @namespace = null)
    {
        if (@namespace != null)
        {
            return _services
                .Where(kvp => kvp.Key.StartsWith($"{@namespace}/"))
                .Select(kvp => kvp.Value)
                .ToList();
        }

        return _services.Values.ToList();
    }

    /// <summary>
    /// Get endpoint slices for load balancing
    /// EndpointSlices allow scaling to large numbers of backends
    /// </summary>
    public List<string> GetEndpointSlices(string serviceName, string @namespace, int sliceSize = 100)
    {
        var endpointKey = $"{@namespace}/{serviceName}";

        if (_endpoints.TryGetValue(endpointKey, out var endpoint))
        {
            var slices = endpoint.ReadyAddresses
                .Chunk(sliceSize)
                .Select(chunk => string.Join(",", chunk))
                .ToList();

            _logger.LogInformation(
                "Generated {Count} endpoint slices for service {Service}",
                slices.Count,
                serviceName);

            return slices;
        }

        return new();
    }

    private string GenerateClusterIp()
    {
        // Simplified: generate IP in 10.0.0.0/24 range
        var random = new Random();
        return $"10.0.0.{random.Next(1, 255)}";
    }
}

/// <summary>
/// Network policy enforcement engine
/// </summary>
public class NetworkPolicyEnforcer
{
    private readonly ConcurrentDictionary<string, KubernetesNetworkPolicy> _policies = new();
    private readonly ILogger<NetworkPolicyEnforcer> _logger;

    public NetworkPolicyEnforcer(ILogger<NetworkPolicyEnforcer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Create network policy
    /// </summary>
    public async Task CreatePolicyAsync(KubernetesNetworkPolicy policy)
    {
        _policies[$"{policy.Namespace}/{policy.Name}"] = policy;

        _logger.LogInformation(
            "Created network policy {Name} in namespace {Namespace}",
            policy.Name,
            policy.Namespace);
    }

    /// <summary>
    /// Evaluate if traffic is allowed
    /// </summary>
    public async Task<bool> IsTrafficAllowedAsync(
        string sourcePod,
        string sourceName space,
        string destinationPod,
        string destinationNamespace,
        int port,
        string protocol = "TCP")
    {
        // Get policies for destination pod
        var applicablePolicies = _policies.Values
            .Where(p => p.Namespace == destinationNamespace && MatchesSelector(destinationPod, p.PodSelector))
            .ToList();

        // If no policies, traffic is allowed (default allow)
        if (!applicablePolicies.Any())
        {
            return true;
        }

        // Check if any policy allows the traffic
        foreach (var policy in applicablePolicies)
        {
            if (policy.PolicyTypes.Contains("Ingress"))
            {
                foreach (var rule in policy.Ingress)
                {
                    if (MatchesIngressRule(
                        sourcePod,
                        sourceNamespace,
                        port,
                        protocol,
                        rule))
                    {
                        _logger.LogInformation(
                            "Traffic allowed: {Source}/{SourcePod} -> {Dest}/{DestPod}:{Port}",
                            sourceNamespace,
                            sourcePod,
                            destinationNamespace,
                            destinationPod,
                            port);

                        return true;
                    }
                }
            }
        }

        _logger.LogWarning(
            "Traffic denied: {Source}/{SourcePod} -> {Dest}/{DestPod}:{Port}",
            sourceNamespace,
            sourcePod,
            destinationNamespace,
            destinationPod,
            port);

        return false;
    }

    /// <summary>
    /// Check if pod matches selector
    /// </summary>
    private bool MatchesSelector(string podName, Dictionary<string, string> selector)
    {
        // Simplified: match based on pod name containing selector values
        return selector.All(kvp => podName.Contains(kvp.Value));
    }

    /// <summary>
    /// Check if traffic matches ingress rule
    /// </summary>
    private bool MatchesIngressRule(
        string sourcePod,
        string sourceNamespace,
        int port,
        string protocol,
        IngressRule rule)
    {
        // Check port
        if (rule.Ports.Any())
        {
            var portMatch = rule.Ports.Any(p =>
                (p.Port == null || p.Port == port) &&
                p.Protocol == protocol);

            if (!portMatch)
                return false;
        }

        // Check source
        foreach (var peer in rule.From)
        {
            if (peer.PodSelector != null && MatchesSelector(sourcePod, peer.PodSelector))
            {
                return true;
            }

            if (peer.IpBlock != null && IsIpInCidr("127.0.0.1", peer.IpBlock.Cidr))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if IP is in CIDR block
    /// </summary>
    private bool IsIpInCidr(string ip, string cidr)
    {
        // Simplified CIDR check
        return cidr.Contains(ip.Split('.')[0]);
    }
}

/// <summary>
/// Advanced routing - Gateway API
/// </summary>
public class KubernetesGateway
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = "default";

    [JsonPropertyName("gatewayClassName")]
    public string GatewayClassName { get; set; } = string.Empty;

    [JsonPropertyName("listeners")]
    public List<GatewayListener> Listeners { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Gateway listener
/// </summary>
public class GatewayListener
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "HTTP"; // HTTP, HTTPS, TCP, UDP

    [JsonPropertyName("port")]
    public int Port { get; set; } = 80;

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("allowedRoutes")]
    public AllowedRoutes? AllowedRoutes { get; set; }
}

/// <summary>
/// Allowed routes configuration
/// </summary>
public class AllowedRoutes
{
    [JsonPropertyName("namespaces")]
    public Dictionary<string, string>? Namespaces { get; set; }
}

/// <summary>
/// Extension methods
/// </summary>
public static class KubernetesNetworkingExtensions
{
    public static IServiceCollection AddKubernetesNetworking(this IServiceCollection services)
    {
        services.AddSingleton<ServiceDiscoveryManager>();
        services.AddSingleton<NetworkPolicyEnforcer>();
        return services;
    }
}
