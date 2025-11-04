#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Autoscaling;

/// <summary>
/// Advanced Autoscaling Patterns
/// Horizontal Pod Autoscaler (HPA), Vertical Pod Autoscaler (VPA), KEDA, Predictive scaling
/// </summary>

/// <summary>
/// HPA configuration - basic CPU/memory scaling
/// </summary>
public class HorizontalPodAutoscaler
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("minReplicas")]
    public int MinReplicas { get; set; } = 1;

    [JsonPropertyName("maxReplicas")]
    public int MaxReplicas { get; set; } = 10;

    [JsonPropertyName("targetCpuUtilizationPercent")]
    public int? TargetCpuUtilizationPercent { get; set; } = 70;

    [JsonPropertyName("targetMemoryUtilizationPercent")]
    public int? TargetMemoryUtilizationPercent { get; set; } = 80;

    [JsonPropertyName("scaleDownStabilization")]
    public TimeSpan ScaleDownStabilization { get; set; } = TimeSpan.FromMinutes(5);

    [JsonPropertyName("scaleUpBehavior")]
    public ScalingBehavior? ScaleUpBehavior { get; set; }

    [JsonPropertyName("scaleDownBehavior")]
    public ScalingBehavior? ScaleDownBehavior { get; set; }
}

/// <summary>
/// Scaling behavior - controls how fast to scale
/// </summary>
public class ScalingBehavior
{
    [JsonPropertyName("stabilizationWindow")]
    public TimeSpan StabilizationWindow { get; set; } = TimeSpan.FromSeconds(300);

    [JsonPropertyName("policies")]
    public List<ScalingPolicy> Policies { get; set; } = new();
}

/// <summary>
/// Scaling policy
/// </summary>
public class ScalingPolicy
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // Percent, Pods

    [JsonPropertyName("value")]
    public int Value { get; set; }

    [JsonPropertyName("periodSeconds")]
    public int PeriodSeconds { get; set; } = 60;
}

/// <summary>
/// Custom metric for scaling
/// </summary>
public class CustomMetric
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty; // Prometheus query

    [JsonPropertyName("targetAverageValue")]
    public double TargetAverageValue { get; set; }

    [JsonPropertyName("targetValue")]
    public double TargetValue { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "Pods"; // Resource, Pods, Object
}

/// <summary>
/// VPA (Vertical Pod Autoscaler) configuration
/// Adjusts CPU/memory requests
/// </summary>
public class VerticalPodAutoscaler
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("updatePolicy")]
    public UpdatePolicy UpdatePolicy { get; set; } = new();

    [JsonPropertyName("resourcePolicy")]
    public ResourcePolicy? ResourcePolicy { get; set; }

    [JsonPropertyName("minAllowed")]
    public ResourceRequirements MinAllowed { get; set; } = new();

    [JsonPropertyName("maxAllowed")]
    public ResourceRequirements MaxAllowed { get; set; } = new();
}

/// <summary>
/// VPA update policy
/// </summary>
public class UpdatePolicy
{
    [JsonPropertyName("updateMode")]
    public string UpdateMode { get; set; } = "Auto"; // Off, Initial, Recreate, Auto

    [JsonPropertyName("minReplicas")]
    public int MinReplicas { get; set; } = 1;
}

/// <summary>
/// Resource policy for VPA
/// </summary>
public class ResourcePolicy
{
    [JsonPropertyName("containerPolicies")]
    public List<ContainerResourcePolicy> ContainerPolicies { get; set; } = new();
}

/// <summary>
/// Container resource policy
/// </summary>
public class ContainerResourcePolicy
{
    [JsonPropertyName("containerName")]
    public string ContainerName { get; set; } = string.Empty;

    [JsonPropertyName("minAllowed")]
    public ResourceRequirements? MinAllowed { get; set; }

    [JsonPropertyName("maxAllowed")]
    public ResourceRequirements? MaxAllowed { get; set; }
}

/// <summary>
/// Resource requirements
/// </summary>
public class ResourceRequirements
{
    [JsonPropertyName("cpu")]
    public string Cpu { get; set; } = "100m";

    [JsonPropertyName("memory")]
    public string Memory { get; set; } = "128Mi";
}

/// <summary>
/// KEDA (Kubernetes Event-driven Autoscaler) trigger
/// </summary>
public class KedaTrigger
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // kafka, rabbitmq, aws-sqs, http, prometheus

    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();

    [JsonPropertyName("metricType")]
    public string MetricType { get; set; } = "AverageValue";

    [JsonPropertyName("metricStatPeriod")]
    public string MetricStatPeriod { get; set; } = "60s";
}

/// <summary>
/// Predictive autoscaling using time-series forecasting
/// </summary>
public class PredictiveAutoscaler
{
    private readonly ConcurrentQueue<(DateTime timestamp, double cpuUsage, int replicas)> _metrics = new();
    private readonly ILogger<PredictiveAutoscaler> _logger;

    public PredictiveAutoscaler(ILogger<PredictiveAutoscaler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Record metric for prediction
    /// </summary>
    public void RecordMetric(double cpuUsage, int currentReplicas)
    {
        _metrics.Enqueue((DateTime.UtcNow, cpuUsage, currentReplicas));

        // Keep only last 100 data points
        while (_metrics.Count > 100)
        {
            _metrics.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Predict required replicas using simple forecasting
    /// </summary>
    public int PredictRequiredReplicas(
        double targetCpuUtilization = 70,
        int minReplicas = 1,
        int maxReplicas = 10)
    {
        if (_metrics.Count < 5)
        {
            return minReplicas; // Not enough data
        }

        var recentMetrics = _metrics.TakeLast(10).ToList();

        // Calculate average CPU usage
        var avgCpu = recentMetrics.Average(m => m.cpuUsage);

        // Simple linear regression to predict trend
        var trend = CalculateTrend(recentMetrics);

        // Predict CPU in next 5 minutes
        var predictedCpu = avgCpu + (trend * 5); // trend per minute * 5 minutes

        // Calculate required replicas
        var currentReplicas = recentMetrics.Last().replicas;
        var requiredReplicas = (int)Math.Ceiling((predictedCpu / targetCpuUtilization) * currentReplicas);

        // Clamp to min/max
        requiredReplicas = Math.Max(minReplicas, Math.Min(maxReplicas, requiredReplicas));

        _logger.LogInformation(
            "Predicted autoscaling: avgCpu={AvgCpu:F1}% trend={Trend:F2} → {RequiredReplicas} replicas",
            avgCpu,
            trend,
            requiredReplicas);

        return requiredReplicas;
    }

    /// <summary>
    /// Calculate trend in metrics
    /// </summary>
    private double CalculateTrend(List<(DateTime timestamp, double cpuUsage, int replicas)> metrics)
    {
        if (metrics.Count < 2)
            return 0;

        var first = metrics.First();
        var last = metrics.Last();

        var timeDiff = (last.timestamp - first.timestamp).TotalMinutes;
        if (timeDiff == 0)
            return 0;

        var cpuDiff = last.cpuUsage - first.cpuUsage;
        return cpuDiff / timeDiff; // CPU change per minute
    }

    /// <summary>
    /// Get prediction stats
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        if (_metrics.Count == 0)
        {
            return new() { ["dataPoints"] = 0 };
        }

        var recentMetrics = _metrics.TakeLast(20).ToList();

        return new()
        {
            ["dataPoints"] = _metrics.Count,
            ["avgCpuUsage"] = recentMetrics.Average(m => m.cpuUsage),
            ["avgReplicas"] = recentMetrics.Average(m => m.replicas),
            ["trend"] = CalculateTrend(recentMetrics)
        };
    }
}

/// <summary>
/// Autoscaler controller - coordinates all scaling strategies
/// </summary>
public class AutoscalerController
{
    private readonly HorizontalPodAutoscaler _hpa = new();
    private readonly VerticalPodAutoscaler _vpa = new();
    private readonly List<KedaTrigger> _kedaTriggers = new();
    private readonly PredictiveAutoscaler _predictor;
    private readonly ILogger<AutoscalerController> _logger;

    public AutoscalerController(
        PredictiveAutoscaler predictor,
        ILogger<AutoscalerController> logger)
    {
        _predictor = predictor;
        _logger = logger;
    }

    /// <summary>
    /// Make scaling decision using multiple strategies
    /// </summary>
    public async Task<int> DecideScalingAsync(
        int currentReplicas,
        Dictionary<string, double> metrics)
    {
        var decisions = new List<int>();

        // HPA decision: based on CPU/memory
        if (metrics.TryGetValue("cpuUsage", out var cpu))
        {
            _predictor.RecordMetric(cpu, currentReplicas);

            var hpaReplicas = CalculateHpaReplicas(cpu, currentReplicas);
            decisions.Add(hpaReplicas);
        }

        // Predictive scaling
        var predictiveReplicas = _predictor.PredictRequiredReplicas();
        decisions.Add(predictiveReplicas);

        // KEDA decision: based on external metrics
        if (metrics.TryGetValue("kafkaLag", out var kafkaLag))
        {
            var kedaReplicas = (int)Math.Ceiling((kafkaLag / 1000) + 1); // 1 replica per 1000 lag
            decisions.Add(Math.Min(kedaReplicas, _hpa.MaxReplicas));
        }

        // Take maximum to be conservative on scale-up
        var finalReplicas = decisions.Any() ? decisions.Max() : currentReplicas;

        _logger.LogInformation(
            "Autoscaling decision: {Current} → {Final} (decisions: {Decisions})",
            currentReplicas,
            finalReplicas,
            string.Join(", ", decisions));

        return finalReplicas;
    }

    /// <summary>
    /// Calculate HPA scaling decision
    /// </summary>
    private int CalculateHpaReplicas(double cpuUsage, int currentReplicas)
    {
        var targetCpu = _hpa.TargetCpuUtilizationPercent ?? 70;

        if (cpuUsage == 0)
            return _hpa.MinReplicas;

        var desiredReplicas = (int)Math.Ceiling((cpuUsage / targetCpu) * currentReplicas);

        return Math.Max(_hpa.MinReplicas, Math.Min(_hpa.MaxReplicas, desiredReplicas));
    }

    /// <summary>
    /// Register KEDA trigger
    /// </summary>
    public void RegisterKedaTrigger(KedaTrigger trigger)
    {
        _kedaTriggers.Add(trigger);

        _logger.LogInformation(
            "Registered KEDA trigger: {Type}",
            trigger.Type);
    }

    /// <summary>
    /// Get autoscaling stats
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        return new()
        {
            ["hpaMinReplicas"] = _hpa.MinReplicas,
            ["hpaMaxReplicas"] = _hpa.MaxReplicas,
            ["targetCpu"] = _hpa.TargetCpuUtilizationPercent,
            ["kedaTriggersCount"] = _kedaTriggers.Count,
            ["predictorStats"] = _predictor.GetStats()
        };
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class AutoscalingExtensions
{
    public static IServiceCollection AddAdvancedAutoscaling(this IServiceCollection services)
    {
        services.AddSingleton<PredictiveAutoscaler>();
        services.AddSingleton<AutoscalerController>();
        return services;
    }
}
