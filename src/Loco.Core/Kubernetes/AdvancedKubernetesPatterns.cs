#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Kubernetes;

public class AdmissionPolicy
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // ValidatingWebhook, MutatingWebhook

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "Namespaced"; // Namespaced, Cluster

    [JsonPropertyName("rules")]
    public List<string> Rules { get; set; } = new();

    [JsonPropertyName("failurePolicy")]
    public string FailurePolicy { get; set; } = "Fail"; // Fail, Ignore

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 5;

    [JsonPropertyName("sideEffects")]
    public string SideEffects { get; set; } = "None";
}

public class ResourceQuota
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    [JsonPropertyName("hardLimits")]
    public Dictionary<string, string> HardLimits { get; set; } = new();

    [JsonPropertyName("scope")]
    public List<string> Scope { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AdvancedKubernetesEngine
{
    private readonly List<AdmissionPolicy> _policies = new();
    private readonly List<ResourceQuota> _quotas = new();
    private readonly ILogger<AdvancedKubernetesEngine> _logger;

    public AdvancedKubernetesEngine(ILogger<AdvancedKubernetesEngine> logger) => _logger = logger;

    public async Task RegisterAdmissionPolicyAsync(AdmissionPolicy policy)
    {
        _policies.Add(policy);
        _logger.LogInformation("Registered admission policy: {Name} ({Type})", policy.Name, policy.Type);
    }

    public async Task CreateResourceQuotaAsync(ResourceQuota quota)
    {
        _quotas.Add(quota);
        _logger.LogInformation("Created resource quota: {Name} in {Namespace}", quota.Name, quota.Namespace);
    }

    public Dictionary<string, object> GetStats() => new()
    {
        ["admissionPolicies"] = _policies.Count,
        ["resourceQuotas"] = _quotas.Count
    };
}

public static class KubernetesExtensions
{
    public static IServiceCollection AddAdvancedKubernetes(this IServiceCollection services)
    {
        services.AddSingleton<AdvancedKubernetesEngine>();
        return services;
    }
}
