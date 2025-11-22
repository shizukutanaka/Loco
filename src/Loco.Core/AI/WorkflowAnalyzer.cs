// Phase 5: ML-Based Workflow Analyzer
// Predictive execution time estimation, anomaly detection, and bottleneck identification
// Uses historical execution data to optimize future workflow performance

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AI;

/// <summary>
/// Execution prediction result
/// </summary>
public class ExecutionPrediction
{
    public string WorkflowId { get; set; } = string.Empty;
    public double PredictedDurationSeconds { get; set; }
    public double ConfidenceScore { get; set; } // 0.0 - 1.0
    public List<StepPrediction> StepPredictions { get; set; } = new();
    public string? BottleneckStepId { get; set; }
    public double? BottleneckDurationSeconds { get; set; }
}

/// <summary>
/// Per-step execution prediction
/// </summary>
public class StepPrediction
{
    public string StepId { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public double PredictedDurationMs { get; set; }
    public double P95DurationMs { get; set; }
    public double P99DurationMs { get; set; }
    public int HistoricalExecutions { get; set; }
    public double FailureRate { get; set; }
}

/// <summary>
/// Anomaly detection result
/// </summary>
public class AnomalyDetectionResult
{
    public bool IsAnomaly { get; set; }
    public double AnomalyScore { get; set; } // 0.0 - 1.0
    public string? AnomalyType { get; set; } // 'slow', 'failed', 'unusual_pattern'
    public string? Description { get; set; }
    public List<string> AffectedSteps { get; set; } = new();
    public double? ExpectedValue { get; set; }
    public double? ActualValue { get; set; }
}

/// <summary>
/// Workflow optimization recommendation
/// </summary>
public class OptimizationRecommendation
{
    public string WorkflowId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // 'parallelization', 'caching', 'retry_policy', 'timeout'
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double PotentialImprovement { get; set; } // 0.0 - 1.0 (percentage improvement)
    public string? ImplementationSteps { get; set; }
    public int Priority { get; set; } // 1 (high) to 5 (low)
}

/// <summary>
/// Workflow analyzer interface
/// </summary>
public interface IWorkflowAnalyzer
{
    Task<ExecutionPrediction> PredictExecutionAsync(
        string workflowId,
        Dictionary<string, object> input,
        CancellationToken ct = default);

    Task<AnomalyDetectionResult> DetectAnomaliesAsync(
        string executionId,
        double actualDurationSeconds,
        CancellationToken ct = default);

    Task<List<OptimizationRecommendation>> GetOptimizationsAsync(
        string workflowId,
        CancellationToken ct = default);

    Task RecordExecutionAsync(
        string workflowId,
        double durationSeconds,
        int stepsExecuted,
        int stepsFailed,
        CancellationToken ct = default);
}

/// <summary>
/// ML-based workflow analyzer implementation
/// </summary>
public class WorkflowAnalyzer : IWorkflowAnalyzer
{
    private readonly ILogger<WorkflowAnalyzer> _logger;
    private readonly Dictionary<string, List<ExecutionRecord>> _executionHistory;
    private readonly Dictionary<string, WorkflowStatistics> _workflowStats;

    // Constants for anomaly detection
    private const double StandardDeviationThreshold = 2.5; // > 2.5 sigma = anomaly
    private const double FailureRateThreshold = 0.10; // > 10% = anomaly

    public WorkflowAnalyzer(ILogger<WorkflowAnalyzer> logger)
    {
        _logger = logger;
        _executionHistory = new Dictionary<string, List<ExecutionRecord>>();
        _workflowStats = new Dictionary<string, WorkflowStatistics>();
    }

    /// <summary>
    /// Predict execution time using historical data
    /// </summary>
    public async Task<ExecutionPrediction> PredictExecutionAsync(
        string workflowId,
        Dictionary<string, object> input,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate analysis

        if (!_executionHistory.TryGetValue(workflowId, out var history) || history.Count < 3)
        {
            // Insufficient data - return default prediction
            _logger.LogDebug("Insufficient historical data for {WorkflowId}", workflowId);
            return new ExecutionPrediction
            {
                WorkflowId = workflowId,
                PredictedDurationSeconds = 10.0, // Default estimate
                ConfidenceScore = 0.3, // Low confidence
            };
        }

        // Calculate statistics
        var stats = CalculateStatistics(history);
        var prediction = new ExecutionPrediction
        {
            WorkflowId = workflowId,
            PredictedDurationSeconds = stats.MeanDuration,
            ConfidenceScore = Math.Min(1.0, history.Count / 100.0), // Confidence increases with data
        };

        // Predict per-step execution
        foreach (var record in history.Take(1))
        {
            foreach (var stepStat in record.StepStatistics)
            {
                var stepPrediction = new StepPrediction
                {
                    StepId = stepStat.Key,
                    StepName = $"Step {stepStat.Key}",
                    PredictedDurationMs = stepStat.Value.MeanDuration * 1000,
                    P95DurationMs = stepStat.Value.P95Duration * 1000,
                    P99DurationMs = stepStat.Value.P99Duration * 1000,
                    HistoricalExecutions = history.Count,
                    FailureRate = stepStat.Value.FailureRate,
                };

                prediction.StepPredictions.Add(stepPrediction);
            }
        }

        // Identify bottleneck
        var bottleneck = prediction.StepPredictions.OrderByDescending(s => s.PredictedDurationMs).FirstOrDefault();
        if (bottleneck != null)
        {
            prediction.BottleneckStepId = bottleneck.StepId;
            prediction.BottleneckDurationSeconds = bottleneck.PredictedDurationMs / 1000.0;
        }

        _logger.LogInformation(
            "Prediction for {WorkflowId}: {Duration}s (confidence: {Confidence})",
            workflowId, prediction.PredictedDurationSeconds, prediction.ConfidenceScore);

        return prediction;
    }

    /// <summary>
    /// Detect anomalies in execution
    /// </summary>
    public async Task<AnomalyDetectionResult> DetectAnomaliesAsync(
        string executionId,
        double actualDurationSeconds,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate analysis

        var result = new AnomalyDetectionResult
        {
            IsAnomaly = false,
            AnomalyScore = 0.0,
        };

        // In production, would extract workflowId from executionId and check against stats
        var workflowId = executionId.Split('-')[0];

        if (!_workflowStats.TryGetValue(workflowId, out var stats) || stats.ExecutionCount < 5)
        {
            return result; // Insufficient data
        }

        // Check if duration is statistical anomaly
        var standardDeviations = (actualDurationSeconds - stats.MeanDuration) / stats.StandardDeviation;
        if (Math.Abs(standardDeviations) > StandardDeviationThreshold)
        {
            result.IsAnomaly = true;
            result.AnomalyScore = Math.Min(1.0, Math.Abs(standardDeviations) / 5.0);
            result.AnomalyType = actualDurationSeconds > stats.MeanDuration ? "slow" : "fast";
            result.Description = $"Execution duration {result.AnomalyType}er than expected by {Math.Abs(standardDeviations):F2} standard deviations";
            result.ExpectedValue = stats.MeanDuration;
            result.ActualValue = actualDurationSeconds;

            _logger.LogWarning(
                "Anomaly detected in {ExecutionId}: {Type} ({Score:P})",
                executionId, result.AnomalyType, result.AnomalyScore);
        }

        return result;
    }

    /// <summary>
    /// Get optimization recommendations
    /// </summary>
    public async Task<List<OptimizationRecommendation>> GetOptimizationsAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.Delay(200, ct); // Simulate analysis

        var recommendations = new List<OptimizationRecommendation>();

        if (!_workflowStats.TryGetValue(workflowId, out var stats) || stats.ExecutionCount < 10)
        {
            return recommendations; // Insufficient data
        }

        // Recommendation 1: Parallelization
        if (stats.SequentialDuration > 5.0 && stats.MostExpensiveSteps.Count > 1)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                WorkflowId = workflowId,
                Category = "parallelization",
                Title = "Parallelize Independent Steps",
                Description = "Steps identified that can run in parallel to reduce total execution time",
                PotentialImprovement = 0.30, // 30% improvement
                ImplementationSteps = "Identify independent steps and configure them as parallel execution",
                Priority = 1,
            });
        }

        // Recommendation 2: Caching
        if (stats.FailureRate < 0.05 && stats.ExecutionCount > 20)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                WorkflowId = workflowId,
                Category = "caching",
                Title = "Add Result Caching",
                Description = "Cache frequently accessed data to reduce API calls and database queries",
                PotentialImprovement = 0.40, // 40% improvement
                ImplementationSteps = "Implement Redis caching for external API calls and database queries",
                Priority = 2,
            });
        }

        // Recommendation 3: Retry Policy
        if (stats.FailureRate > 0.05 && stats.FailureRate < 0.20)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                WorkflowId = workflowId,
                Category = "retry_policy",
                Title = "Implement Exponential Backoff Retry",
                Description = "Add retry logic with exponential backoff to handle transient failures",
                PotentialImprovement = 0.20, // 20% improvement
                ImplementationSteps = "Configure retry policy with exponential backoff for flaky steps",
                Priority = 2,
            });
        }

        // Recommendation 4: Timeout Adjustment
        if (stats.TimeoutViolations > 0)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                WorkflowId = workflowId,
                Category = "timeout",
                Title = "Adjust Step Timeouts",
                Description = $"Steps are timing out {stats.TimeoutViolations} times - consider increasing timeout",
                PotentialImprovement = 0.15, // 15% improvement
                ImplementationSteps = "Increase timeout values for slow steps based on P99 duration",
                Priority = 3,
            });
        }

        _logger.LogInformation(
            "Generated {Count} optimization recommendations for {WorkflowId}",
            recommendations.Count, workflowId);

        return recommendations;
    }

    /// <summary>
    /// Record execution for future analysis
    /// </summary>
    public async Task RecordExecutionAsync(
        string workflowId,
        double durationSeconds,
        int stepsExecuted,
        int stepsFailed,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var record = new ExecutionRecord
        {
            WorkflowId = workflowId,
            DurationSeconds = durationSeconds,
            StepsExecuted = stepsExecuted,
            StepsFailed = stepsFailed,
            ExecutedAt = DateTime.UtcNow,
            Success = stepsFailed == 0,
        };

        if (!_executionHistory.ContainsKey(workflowId))
        {
            _executionHistory[workflowId] = new List<ExecutionRecord>();
        }

        _executionHistory[workflowId].Add(record);

        // Update statistics
        UpdateStatistics(workflowId);

        _logger.LogDebug(
            "Recorded execution for {WorkflowId}: {Duration}s, {Steps} steps",
            workflowId, durationSeconds, stepsExecuted);
    }

    // Private helper methods
    private void UpdateStatistics(string workflowId)
    {
        if (!_executionHistory.TryGetValue(workflowId, out var history))
            return;

        var stats = CalculateStatistics(history);
        _workflowStats[workflowId] = stats;
    }

    private WorkflowStatistics CalculateStatistics(List<ExecutionRecord> history)
    {
        var stats = new WorkflowStatistics();
        stats.ExecutionCount = history.Count;

        // Calculate mean and std dev
        var durations = history.Select(h => h.DurationSeconds).ToList();
        stats.MeanDuration = durations.Average();
        stats.StandardDeviation = CalculateStandardDeviation(durations);
        stats.MinDuration = durations.Min();
        stats.MaxDuration = durations.Max();

        // Calculate percentiles
        var sorted = durations.OrderBy(d => d).ToList();
        stats.P50Duration = sorted[(int)(sorted.Count * 0.50)];
        stats.P95Duration = sorted[(int)(sorted.Count * 0.95)];
        stats.P99Duration = sorted[(int)(sorted.Count * 0.99)];

        // Calculate failure metrics
        stats.SuccessCount = history.Count(h => h.Success);
        stats.FailureRate = 1.0 - (stats.SuccessCount / (double)stats.ExecutionCount);

        // Most expensive steps (mock)
        stats.MostExpensiveSteps = new List<string> { "step-1", "step-2" };
        stats.SequentialDuration = stats.MeanDuration;

        return stats;
    }

    private double CalculateStandardDeviation(List<double> values)
    {
        if (values.Count < 2)
            return 0;

        var mean = values.Average();
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        return Math.Sqrt(variance);
    }

    // Internal classes
    private class ExecutionRecord
    {
        public string WorkflowId { get; set; } = string.Empty;
        public double DurationSeconds { get; set; }
        public int StepsExecuted { get; set; }
        public int StepsFailed { get; set; }
        public DateTime ExecutedAt { get; set; }
        public bool Success { get; set; }
        public Dictionary<string, StepStatistic> StepStatistics { get; set; } = new();
    }

    private class StepStatistic
    {
        public double MeanDuration { get; set; }
        public double P95Duration { get; set; }
        public double P99Duration { get; set; }
        public double FailureRate { get; set; }
    }

    private class WorkflowStatistics
    {
        public int ExecutionCount { get; set; }
        public double MeanDuration { get; set; }
        public double StandardDeviation { get; set; }
        public double MinDuration { get; set; }
        public double MaxDuration { get; set; }
        public double P50Duration { get; set; }
        public double P95Duration { get; set; }
        public double P99Duration { get; set; }
        public int SuccessCount { get; set; }
        public double FailureRate { get; set; }
        public List<string> MostExpensiveSteps { get; set; } = new();
        public double SequentialDuration { get; set; }
        public int TimeoutViolations { get; set; }
    }
}
