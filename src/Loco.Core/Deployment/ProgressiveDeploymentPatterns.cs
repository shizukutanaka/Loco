#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Deployment;

/// <summary>
/// Progressive Deployment Patterns
/// Canary, Blue-Green, Rolling, A/B Testing with Flagger integration
/// </summary>

/// <summary>
/// Deployment revision
/// </summary>
public class DeploymentRevision
{
    [JsonPropertyName("revision")]
    public int Revision { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("image")]
    public string Image { get; set; } = string.Empty;

    [JsonPropertyName("replicas")]
    public int Replicas { get; set; } = 1;

    [JsonPropertyName("labels")]
    public Dictionary<string, string> Labels { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Pending"; // Pending, Progressing, Complete, Failed
}

/// <summary>
/// Canary deployment strategy
/// Gradually shift traffic from stable to canary
/// </summary>
public class CanaryDeployment
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("stableVersion")]
    public DeploymentRevision StableVersion { get; set; } = new();

    [JsonPropertyName("canaryVersion")]
    public DeploymentRevision CanaryVersion { get; set; } = new();

    [JsonPropertyName("traffic")]
    public TrafficShift Traffic { get; set; } = new();

    [JsonPropertyName("analysis")]
    public CanaryAnalysis Analysis { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Waiting"; // Waiting, Progressing, Succeeded, Failed, Finalizing
}

/// <summary>
/// Traffic shifting strategy
/// </summary>
public class TrafficShift
{
    [JsonPropertyName("weight")]
    public int Weight { get; set; } = 0; // Percentage to canary (0-100)

    [JsonPropertyName("stepWeight")]
    public int StepWeight { get; set; } = 10; // Percentage to increase per step

    [JsonPropertyName("stepDuration")]
    public TimeSpan StepDuration { get; set; } = TimeSpan.FromMinutes(5);

    [JsonPropertyName("maxWeight")]
    public int MaxWeight { get; set; } = 50; // Max traffic before manual approval

    [JsonPropertyName("lastTransitionTime")]
    public DateTime? LastTransitionTime { get; set; }
}

/// <summary>
/// Canary analysis metrics
/// </summary>
public class CanaryAnalysis
{
    [JsonPropertyName("threshold")]
    public int Threshold { get; set; } = 95; // Minimum success rate %

    [JsonPropertyName("interval")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    [JsonPropertyName("iterations")]
    public int Iterations { get; set; } = 10; // Number of analysis runs

    [JsonPropertyName("metrics")]
    public List<CanaryMetric> Metrics { get; set; } = new();

    [JsonPropertyName("webhooks")]
    public List<string> Webhooks { get; set; } = new(); // URLs for validation
}

/// <summary>
/// Canary metric for analysis
/// </summary>
public class CanaryMetric
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty; // Prometheus query

    [JsonPropertyName("successCriteria")]
    public string SuccessCriteria { get; set; } = string.Empty; // > 0.95, < 100

    [JsonPropertyName("interval")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(60);

    [JsonPropertyName("thresholdRange")]
    public Range? ThresholdRange { get; set; }
}

/// <summary>
/// Numeric range
/// </summary>
public class Range
{
    [JsonPropertyName("min")]
    public double Min { get; set; }

    [JsonPropertyName("max")]
    public double Max { get; set; }
}

/// <summary>
/// Blue-Green deployment strategy
/// Switch all traffic at once with fast rollback
/// </summary>
public class BlueGreenDeployment
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("blueVersion")]
    public DeploymentRevision BlueVersion { get; set; } = new(); // Current

    [JsonPropertyName("greenVersion")]
    public DeploymentRevision GreenVersion { get; set; } = new(); // New

    [JsonPropertyName("activeSlot")]
    public string ActiveSlot { get; set; } = "Blue";

    [JsonPropertyName("verificationWaitTime")]
    public TimeSpan VerificationWaitTime { get; set; } = TimeSpan.FromMinutes(5);

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Waiting"; // Waiting, Testing, Complete, Rollback
}

/// <summary>
/// Rolling deployment strategy
/// Default Kubernetes deployment
/// </summary>
public class RollingDeployment
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("maxSurge")]
    public string MaxSurge { get; set; } = "25%";

    [JsonPropertyName("maxUnavailable")]
    public string MaxUnavailable { get; set; } = "25%";

    [JsonPropertyName("progressDeadlineSeconds")]
    public int ProgressDeadlineSeconds { get; set; } = 600;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Progressing"; // Progressing, Complete, Failed
}

/// <summary>
/// Deployment controller - manages different deployment strategies
/// </summary>
public class DeploymentController
{
    private readonly Dictionary<string, CanaryDeployment> _canaries = new();
    private readonly Dictionary<string, BlueGreenDeployment> _blueGreens = new();
    private readonly Dictionary<string, RollingDeployment> _rollings = new();
    private readonly ILogger<DeploymentController> _logger;

    public DeploymentController(ILogger<DeploymentController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Start canary deployment
    /// </summary>
    public async Task<CanaryDeployment> StartCanaryDeploymentAsync(
        string name,
        DeploymentRevision stableVersion,
        DeploymentRevision canaryVersion)
    {
        var canary = new CanaryDeployment
        {
            Name = name,
            StableVersion = stableVersion,
            CanaryVersion = canaryVersion,
            Status = "Progressing"
        };

        _canaries[name] = canary;

        _logger.LogInformation(
            "Started canary deployment: {Name} ({StableVersion} → {CanaryVersion})",
            name,
            stableVersion.Version,
            canaryVersion.Version);

        return canary;
    }

    /// <summary>
    /// Advance canary traffic
    /// </summary>
    public async Task<CanaryDeployment?> AdvanceCanaryAsync(string name)
    {
        if (!_canaries.TryGetValue(name, out var canary))
        {
            return null;
        }

        var newWeight = canary.Traffic.Weight + canary.Traffic.StepWeight;

        if (newWeight >= 100)
        {
            // Promotion to stable complete
            canary.StableVersion = canary.CanaryVersion;
            canary.Status = "Succeeded";

            _logger.LogInformation(
                "Canary deployment succeeded: {Name}",
                name);
        }
        else if (newWeight > canary.Traffic.MaxWeight)
        {
            // Wait for manual approval
            canary.Status = "Waiting";

            _logger.LogInformation(
                "Canary waiting for manual approval: {Name} at {Weight}%",
                name,
                canary.Traffic.Weight);
        }
        else
        {
            canary.Traffic.Weight = newWeight;
            canary.Traffic.LastTransitionTime = DateTime.UtcNow;

            _logger.LogInformation(
                "Advanced canary traffic: {Name} to {Weight}%",
                name,
                newWeight);
        }

        return canary;
    }

    /// <summary>
    /// Rollback canary deployment
    /// </summary>
    public async Task RollbackCanaryAsync(string name, string reason)
    {
        if (_canaries.TryGetValue(name, out var canary))
        {
            canary.Status = "Failed";

            _logger.LogWarning(
                "Rolled back canary deployment: {Name} ({Reason})",
                name,
                reason);
        }
    }

    /// <summary>
    /// Start blue-green deployment
    /// </summary>
    public async Task<BlueGreenDeployment> StartBlueGreenDeploymentAsync(
        string name,
        DeploymentRevision blueVersion,
        DeploymentRevision greenVersion)
    {
        var blueGreen = new BlueGreenDeployment
        {
            Name = name,
            BlueVersion = blueVersion,
            GreenVersion = greenVersion,
            Status = "Testing"
        };

        _blueGreens[name] = blueGreen;

        _logger.LogInformation(
            "Started blue-green deployment: {Name} (blue:{BlueVersion} green:{GreenVersion})",
            name,
            blueVersion.Version,
            greenVersion.Version);

        return blueGreen;
    }

    /// <summary>
    /// Promote green to active
    /// </summary>
    public async Task<BlueGreenDeployment?> PromoteGreenAsync(string name)
    {
        if (!_blueGreens.TryGetValue(name, out var blueGreen))
        {
            return null;
        }

        blueGreen.ActiveSlot = blueGreen.ActiveSlot == "Blue" ? "Green" : "Blue";
        blueGreen.Status = "Complete";

        _logger.LogInformation(
            "Promoted blue-green deployment: {Name} active slot is now {Slot}",
            name,
            blueGreen.ActiveSlot);

        return blueGreen;
    }

    /// <summary>
    /// Analyze canary metrics
    /// </summary>
    public async Task<(bool passed, string reason)> AnalyzeCanaryMetricsAsync(
        string name,
        Dictionary<string, double> metrics)
    {
        if (!_canaries.TryGetValue(name, out var canary))
        {
            return (false, "Canary not found");
        }

        var allMetricsPassed = true;
        var failedMetrics = new List<string>();

        foreach (var metric in canary.Analysis.Metrics)
        {
            if (!metrics.TryGetValue(metric.Name, out var value))
            {
                continue;
            }

            // Simple threshold validation
            if (metric.ThresholdRange != null)
            {
                if (value < metric.ThresholdRange.Min || value > metric.ThresholdRange.Max)
                {
                    allMetricsPassed = false;
                    failedMetrics.Add($"{metric.Name}={value:F2}");
                }
            }
        }

        var reason = allMetricsPassed ? "All metrics passed" : $"Failed metrics: {string.Join(", ", failedMetrics)}";

        _logger.LogInformation(
            "Canary analysis result: {Name} {Result}",
            name,
            reason);

        return (allMetricsPassed, reason);
    }

    /// <summary>
    /// Get deployment status
    /// </summary>
    public Dictionary<string, object> GetDeploymentStatus()
    {
        return new()
        {
            ["canaryCount"] = _canaries.Count,
            ["blueGreenCount"] = _blueGreens.Count,
            ["rollingCount"] = _rollings.Count,
            ["canaryProgressing"] = _canaries.Values.Count(c => c.Status == "Progressing"),
            ["blueGreenTesting"] = _blueGreens.Values.Count(b => b.Status == "Testing")
        };
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class ProgressiveDeploymentExtensions
{
    public static IServiceCollection AddProgressiveDeployment(this IServiceCollection services)
    {
        services.AddSingleton<DeploymentController>();
        return services;
    }
}
