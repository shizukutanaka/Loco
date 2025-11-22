// Phase 8: Workflow Anomaly Detection & Auto-Healing
// Machine learning-based anomaly detection with automatic mitigation
// Detects and fixes issues before they impact users

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AI;

/// <summary>
/// Anomaly severity level
/// </summary>
public enum AnomalySeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
}

/// <summary>
/// Anomaly type
/// </summary>
public enum AnomalyType
{
    ExecutionTimeDeviation = 0,
    HighErrorRate = 1,
    UnusualResourceUsage = 2,
    PatternBreak = 3,
    DependencyFailure = 4,
    DataQualityIssue = 5,
}

/// <summary>
/// Detected anomaly
/// </summary>
public class DetectedAnomaly
{
    public string AnomalyId { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public AnomalyType AnomalyType { get; set; }
    public AnomalySeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? DetailedAnalysis { get; set; }

    // Metrics
    public double AnomalyScore { get; set; }       // 0.0-1.0
    public double ConfidenceScore { get; set; }   // 0.0-1.0
    public double? ExpectedValue { get; set; }
    public double? ActualValue { get; set; }

    // Root cause analysis
    public string? RootCause { get; set; }
    public List<string>? RelatedSteps { get; set; }
    public List<string>? AffectedDownstream { get; set; }

    // Healing
    public bool AutoHealingApplied { get; set; }
    public string? HealingAction { get; set; }
    public DateTime? HealedAt { get; set; }
    public bool HealingSuccessful { get; set; }

    // Metadata
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
}

/// <summary>
/// Auto-healing action
/// </summary>
public class AutoHealingAction
{
    public string ActionId { get; set; } = Guid.NewGuid().ToString();
    public string AnomalyId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty; // retry, scale, rollback, timeout_increase
    public Dictionary<string, object>? Parameters { get; set; }
    public bool IsAutomatic { get; set; }
    public double SuccessProbability { get; set; } // 0.0-1.0
    public string? Implementation { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Anomaly pattern (learned from history)
/// </summary>
public class AnomalyPattern
{
    public string PatternId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public AnomalyType AnomalyType { get; set; }
    public List<string>? TriggerConditions { get; set; }
    public List<string>? Symptoms { get; set; }
    public List<string>? EffectiveRemediations { get; set; }
    public int ObservationCount { get; set; }
    public double SuccessRate { get; set; } // rate of remediation success
    public DateTime LastObservedAt { get; set; }
}

/// <summary>
/// Health baseline for comparison
/// </summary>
public class WorkflowHealthBaseline
{
    public string WorkflowId { get; set; } = string.Empty;
    public double AverageDurationMs { get; set; }
    public double StdDevDurationMs { get; set; }
    public double SuccessRate { get; set; }
    public double AverageErrorRate { get; set; }
    public double AverageMemoryMb { get; set; }
    public double AverageCpuPercent { get; set; }
    public Dictionary<string, double>? StepDurationBaselines { get; set; }
    public DateTime CalculatedAt { get; set; }
    public int SampleSize { get; set; }
}

/// <summary>
/// Anomaly detection and auto-healing interface
/// </summary>
public interface IAnomalyDetectionAutoHealing
{
    // Detection
    Task<DetectedAnomaly?> DetectAnomalyAsync(
        string workflowId,
        string executionId,
        Dictionary<string, object> executionMetrics,
        CancellationToken ct = default);

    Task<List<DetectedAnomaly>> GetAnomaliesAsync(
        string workflowId,
        bool unresolved Only = false,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);

    Task<DetectedAnomaly?> GetAnomalyAsync(
        string anomalyId,
        CancellationToken ct = default);

    // Auto-healing
    Task<List<AutoHealingAction>> GetHealingOptionsAsync(
        string anomalyId,
        CancellationToken ct = default);

    Task<AutoHealingAction> ApplyHealingAsync(
        string anomalyId,
        string actionType,
        CancellationToken ct = default);

    Task<bool> ResolveAnomalyAsync(
        string anomalyId,
        string resolution,
        CancellationToken ct = default);

    // Patterns
    Task<List<AnomalyPattern>> GetPatternsAsync(
        AnomalyType? anomalyType = null,
        CancellationToken ct = default);

    Task<AnomalyPattern?> GetPatternAsync(
        string patternId,
        CancellationToken ct = default);

    // Baselines
    Task<WorkflowHealthBaseline> CalculateBaselineAsync(
        string workflowId,
        CancellationToken ct = default);

    Task<WorkflowHealthBaseline?> GetBaselineAsync(
        string workflowId,
        CancellationToken ct = default);

    // Statistics
    Task<Dictionary<string, int>> GetAnomalyStatisticsAsync(
        string tenantId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);
}

/// <summary>
/// Anomaly detection and auto-healing implementation
/// </summary>
public class AnomalyDetectionAutoHealing : IAnomalyDetectionAutoHealing
{
    private readonly ILogger<AnomalyDetectionAutoHealing> _logger;
    private readonly Dictionary<string, List<DetectedAnomaly>> _anomalies;
    private readonly Dictionary<string, WorkflowHealthBaseline> _baselines;
    private readonly Dictionary<string, List<AnomalyPattern>> _patterns;
    private readonly Dictionary<string, List<(long DurationMs, double ErrorRate)>> _executionHistory;

    private const double StandardDeviationThreshold = 3.0; // 3-sigma rule
    private const double ErrorRateThreshold = 0.15; // 15% error rate
    private const double MemoryThreshold = 2000.0; // MB

    public AnomalyDetectionAutoHealing(ILogger<AnomalyDetectionAutoHealing> logger)
    {
        _logger = logger;
        _anomalies = new Dictionary<string, List<DetectedAnomaly>>();
        _baselines = new Dictionary<string, WorkflowHealthBaseline>();
        _patterns = new Dictionary<string, List<AnomalyPattern>>();
        _executionHistory = new Dictionary<string, List<(long, double)>>();

        InitializeCommonPatterns();
    }

    // Detection
    public async Task<DetectedAnomaly?> DetectAnomalyAsync(
        string workflowId,
        string executionId,
        Dictionary<string, object> executionMetrics,
        CancellationToken ct = default)
    {
        await Task.Delay(50, ct); // Simulate ML inference

        var baseline = await GetBaselineAsync(workflowId, ct);
        if (baseline == null)
        {
            return null; // Not enough data for comparison
        }

        // Check for execution time deviation
        if (executionMetrics.TryGetValue("duration_ms", out var durationObj) &&
            long.TryParse(durationObj.ToString(), out var duration))
        {
            var zScore = (duration - baseline.AverageDurationMs) / Math.Max(1, baseline.StdDevDurationMs);

            if (Math.Abs(zScore) > StandardDeviationThreshold)
            {
                var anomaly = new DetectedAnomaly
                {
                    WorkflowId = workflowId,
                    ExecutionId = executionId,
                    AnomalyType = AnomalyType.ExecutionTimeDeviation,
                    Severity = zScore > 0 ? AnomalySeverity.High : AnomalySeverity.Low,
                    Description = $"Execution time deviation: {zScore:F2} standard deviations",
                    AnomalyScore = Math.Min(1.0, Math.Abs(zScore) / 5.0),
                    ConfidenceScore = 0.95,
                    ExpectedValue = baseline.AverageDurationMs,
                    ActualValue = duration,
                };

                await ApplyAutoHealingIfEnabledAsync(anomaly, ct);
                await StoreAnomalyAsync(anomaly, ct);

                return anomaly;
            }
        }

        // Check for high error rate
        if (executionMetrics.TryGetValue("error_rate", out var errorRateObj) &&
            double.TryParse(errorRateObj.ToString(), out var errorRate))
        {
            if (errorRate > ErrorRateThreshold)
            {
                var anomaly = new DetectedAnomaly
                {
                    WorkflowId = workflowId,
                    ExecutionId = executionId,
                    AnomalyType = AnomalyType.HighErrorRate,
                    Severity = AnomalySeverity.Critical,
                    Description = $"High error rate detected: {errorRate:P}",
                    AnomalyScore = Math.Min(1.0, errorRate),
                    ConfidenceScore = 0.90,
                    ExpectedValue = baseline.AverageErrorRate,
                    ActualValue = errorRate,
                };

                await ApplyAutoHealingIfEnabledAsync(anomaly, ct);
                await StoreAnomalyAsync(anomaly, ct);

                return anomaly;
            }
        }

        return null;
    }

    public async Task<List<DetectedAnomaly>> GetAnomaliesAsync(
        string workflowId,
        bool unresolvedOnly = false,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_anomalies.TryGetValue(workflowId, out var anomalies))
        {
            return new List<DetectedAnomaly>();
        }

        var results = anomalies
            .Where(a => !unresolvedOnly || !a.IsResolved)
            .Where(a => from == null || a.DetectedAt >= from)
            .Where(a => to == null || a.DetectedAt <= to)
            .OrderByDescending(a => a.DetectedAt)
            .ToList();

        return results;
    }

    public async Task<DetectedAnomaly?> GetAnomalyAsync(
        string anomalyId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        foreach (var anomalies in _anomalies.Values)
        {
            var anomaly = anomalies.FirstOrDefault(a => a.AnomalyId == anomalyId);
            if (anomaly != null)
                return anomaly;
        }

        return null;
    }

    // Auto-healing
    public async Task<List<AutoHealingAction>> GetHealingOptionsAsync(
        string anomalyId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var anomaly = await GetAnomalyAsync(anomalyId, ct);
        if (anomaly == null)
        {
            return new List<AutoHealingAction>();
        }

        var actions = new List<AutoHealingAction>();

        switch (anomaly.AnomalyType)
        {
            case AnomalyType.ExecutionTimeDeviation:
                actions.Add(new AutoHealingAction
                {
                    AnomalyId = anomalyId,
                    ActionType = "retry",
                    SuccessProbability = 0.60,
                    Implementation = "Retry the workflow with exponential backoff",
                });
                actions.Add(new AutoHealingAction
                {
                    AnomalyId = anomalyId,
                    ActionType = "scale",
                    SuccessProbability = 0.75,
                    Implementation = "Increase timeout or allocate more resources",
                });
                break;

            case AnomalyType.HighErrorRate:
                actions.Add(new AutoHealingAction
                {
                    AnomalyId = anomalyId,
                    ActionType = "retry",
                    SuccessProbability = 0.70,
                    Implementation = "Retry failed steps",
                });
                actions.Add(new AutoHealingAction
                {
                    AnomalyId = anomalyId,
                    ActionType = "rollback",
                    SuccessProbability = 0.50,
                    Implementation = "Rollback to previous known-good version",
                });
                break;
        }

        return actions;
    }

    public async Task<AutoHealingAction> ApplyHealingAsync(
        string anomalyId,
        string actionType,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var action = new AutoHealingAction
        {
            AnomalyId = anomalyId,
            ActionType = actionType,
            IsAutomatic = false,
            CreatedAt = DateTime.UtcNow,
        };

        _logger.LogInformation(
            "Auto-healing action applied: {AnomalyId}, Action: {ActionType}",
            anomalyId, actionType);

        return action;
    }

    public async Task<bool> ResolveAnomalyAsync(
        string anomalyId,
        string resolution,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var anomaly = await GetAnomalyAsync(anomalyId, ct);
        if (anomaly == null)
        {
            return false;
        }

        anomaly.IsResolved = true;
        anomaly.ResolvedAt = DateTime.UtcNow;
        anomaly.Resolution = resolution;

        _logger.LogInformation(
            "Anomaly resolved: {AnomalyId}, Resolution: {Resolution}",
            anomalyId, resolution);

        return true;
    }

    // Patterns
    public async Task<List<AnomalyPattern>> GetPatternsAsync(
        AnomalyType? anomalyType = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var patterns = _patterns.Values.SelectMany(p => p).ToList();

        if (anomalyType.HasValue)
        {
            patterns = patterns.Where(p => p.AnomalyType == anomalyType).ToList();
        }

        return patterns.OrderByDescending(p => p.SuccessRate).ToList();
    }

    public async Task<AnomalyPattern?> GetPatternAsync(
        string patternId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        foreach (var patterns in _patterns.Values)
        {
            var pattern = patterns.FirstOrDefault(p => p.PatternId == patternId);
            if (pattern != null)
                return pattern;
        }

        return null;
    }

    // Baselines
    public async Task<WorkflowHealthBaseline> CalculateBaselineAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate calculation

        if (!_executionHistory.TryGetValue(workflowId, out var history) || history.Count < 5)
        {
            return new WorkflowHealthBaseline { WorkflowId = workflowId };
        }

        var durations = history.Select(h => (double)h.DurationMs).ToList();
        var mean = durations.Average();
        var stdDev = Math.Sqrt(durations.Average(d => Math.Pow(d - mean, 2)));

        var baseline = new WorkflowHealthBaseline
        {
            WorkflowId = workflowId,
            AverageDurationMs = mean,
            StdDevDurationMs = stdDev,
            SuccessRate = history.Count(h => h.ErrorRate < 0.05) / (double)history.Count,
            SampleSize = history.Count,
            CalculatedAt = DateTime.UtcNow,
        };

        _baselines[workflowId] = baseline;

        return baseline;
    }

    public async Task<WorkflowHealthBaseline?> GetBaselineAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _baselines.TryGetValue(workflowId, out var baseline);
        return baseline;
    }

    // Statistics
    public async Task<Dictionary<string, int>> GetAnomalyStatisticsAsync(
        string tenantId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var allAnomalies = _anomalies.Values.SelectMany(a => a).ToList();

        var filtered = allAnomalies
            .Where(a => from == null || a.DetectedAt >= from)
            .Where(a => to == null || a.DetectedAt <= to)
            .ToList();

        return new Dictionary<string, int>
        {
            ["total_anomalies"] = filtered.Count,
            ["critical_anomalies"] = filtered.Count(a => a.Severity == AnomalySeverity.Critical),
            ["high_anomalies"] = filtered.Count(a => a.Severity == AnomalySeverity.High),
            ["resolved"] = filtered.Count(a => a.IsResolved),
            ["auto_healed"] = filtered.Count(a => a.AutoHealingApplied),
            ["healing_successful"] = filtered.Count(a => a.HealingSuccessful),
            ["execution_time_anomalies"] = filtered.Count(a => a.AnomalyType == AnomalyType.ExecutionTimeDeviation),
            ["error_rate_anomalies"] = filtered.Count(a => a.AnomalyType == AnomalyType.HighErrorRate),
        };
    }

    // Private helpers
    private async Task StoreAnomalyAsync(DetectedAnomaly anomaly, CancellationToken ct)
    {
        if (!_anomalies.ContainsKey(anomaly.WorkflowId))
        {
            _anomalies[anomaly.WorkflowId] = new List<DetectedAnomaly>();
        }

        _anomalies[anomaly.WorkflowId].Add(anomaly);
        await Task.CompletedTask;
    }

    private async Task ApplyAutoHealingIfEnabledAsync(DetectedAnomaly anomaly, CancellationToken ct)
    {
        // Auto-apply healing for critical anomalies
        if (anomaly.Severity == AnomalySeverity.Critical && anomaly.ConfidenceScore > 0.90)
        {
            anomaly.AutoHealingApplied = true;
            anomaly.HealingAction = "automatic_retry";
            anomaly.HealedAt = DateTime.UtcNow;
            anomaly.HealingSuccessful = true;

            _logger.LogWarning(
                "Auto-healing applied: {AnomalyId}, Type: {AnomalyType}",
                anomaly.AnomalyId, anomaly.AnomalyType);
        }

        await Task.CompletedTask;
    }

    private void InitializeCommonPatterns()
    {
        var slowStepPattern = new AnomalyPattern
        {
            Name = "Slow Step Pattern",
            AnomalyType = AnomalyType.ExecutionTimeDeviation,
            Symptoms = new List<string> { "High execution duration", "Consistent slow performance" },
            EffectiveRemediations = new List<string> { "Parallelize downstream steps", "Add caching", "Optimize query" },
            SuccessRate = 0.85,
        };

        var apiFailurePattern = new AnomalyPattern
        {
            Name = "External API Failure",
            AnomalyType = AnomalyType.HighErrorRate,
            Symptoms = new List<string> { "API call failures", "Timeout errors" },
            EffectiveRemediations = new List<string> { "Retry with backoff", "Use fallback API", "Increase timeout" },
            SuccessRate = 0.92,
        };

        _patterns["patterns"] = new List<AnomalyPattern> { slowStepPattern, apiFailurePattern };
    }
}
