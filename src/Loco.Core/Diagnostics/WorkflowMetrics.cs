// Phase 2 optimization: Custom OpenTelemetry metrics for workflow observability
// Provides detailed performance insights for workflow execution

using System.Diagnostics.Metrics;

namespace Loco.Core.Diagnostics;

/// <summary>
/// Workflow-specific metrics for OpenTelemetry
/// Phase 2: Enhanced observability with custom metrics
///
/// Tracks:
/// - Workflow execution count (success/failure breakdown)
/// - Execution duration distribution
/// - Active concurrent executions
/// - Per-workflow performance metrics
/// </summary>
public class WorkflowMetrics
{
    private const string MeterName = "Loco.Workflows";
    private const string MeterVersion = "1.0.0";

    private readonly Meter _meter;

    // Metrics instruments
    private readonly Counter<int> _executionCount;
    private readonly Histogram<double> _executionDuration;
    private readonly UpDownCounter<int> _activeExecutions;

    // Track per-workflow metrics
    private readonly Dictionary<string, WorkflowMetricStats> _workflowStats = new();
    private readonly object _statsLock = new();

    public WorkflowMetrics()
    {
        _meter = new Meter(MeterName, MeterVersion);

        // Create metrics instruments
        _executionCount = _meter.CreateCounter<int>(
            name: "loco.workflows.executions.total",
            unit: "{execution}",
            description: "Total number of workflow executions"
        );

        _executionDuration = _meter.CreateHistogram<double>(
            name: "loco.workflows.executions.duration",
            unit: "ms",
            description: "Workflow execution duration in milliseconds"
        );

        _activeExecutions = _meter.CreateUpDownCounter<int>(
            name: "loco.workflows.executions.active",
            unit: "{execution}",
            description: "Number of currently active workflow executions"
        );

        // Optional: Per-workflow gauges (requires additional setup in Program.cs)
        _meter.CreateObservableGauge(
            name: "loco.workflows.success.rate",
            observeValue: GetSuccessRateCallback,
            unit: "%",
            description: "Success rate percentage of workflow executions"
        );
    }

    /// <summary>
    /// Record workflow execution start
    /// </summary>
    public void RecordExecutionStart(string workflowId)
    {
        _activeExecutions.Add(1, new KeyValuePair<string, object?>("workflow_id", workflowId));

        lock (_statsLock)
        {
            if (!_workflowStats.ContainsKey(workflowId))
            {
                _workflowStats[workflowId] = new WorkflowMetricStats(workflowId);
            }
            _workflowStats[workflowId].ActiveCount++;
        }
    }

    /// <summary>
    /// Record successful workflow execution completion
    /// </summary>
    public void RecordExecutionSuccess(string workflowId, double durationMs)
    {
        RecordExecution(workflowId, durationMs, success: true);
    }

    /// <summary>
    /// Record failed workflow execution
    /// </summary>
    public void RecordExecutionFailure(string workflowId, double durationMs, string? errorType = null)
    {
        RecordExecution(workflowId, durationMs, success: false, errorType: errorType);
    }

    /// <summary>
    /// Generic execution recording
    /// </summary>
    private void RecordExecution(string workflowId, double durationMs, bool success, string? errorType = null)
    {
        // Record metrics
        _executionCount.Add(1,
            new KeyValuePair<string, object?>("workflow_id", workflowId),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"),
            new KeyValuePair<string, object?>("error_type", errorType ?? "none")
        );

        _executionDuration.Record(durationMs,
            new KeyValuePair<string, object?>("workflow_id", workflowId),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure")
        );

        _activeExecutions.Add(-1, new KeyValuePair<string, object?>("workflow_id", workflowId));

        // Update per-workflow statistics
        lock (_statsLock)
        {
            if (_workflowStats.TryGetValue(workflowId, out var stats))
            {
                stats.ActiveCount--;
                stats.TotalExecutions++;
                stats.TotalDuration += durationMs;

                if (success)
                {
                    stats.SuccessfulExecutions++;
                }
                else
                {
                    stats.FailedExecutions++;
                    if (errorType != null)
                    {
                        if (!stats.ErrorTypes.ContainsKey(errorType))
                            stats.ErrorTypes[errorType] = 0;
                        stats.ErrorTypes[errorType]++;
                    }
                }

                // Update min/max durations
                if (durationMs < stats.MinDurationMs || stats.MinDurationMs == 0)
                    stats.MinDurationMs = durationMs;
                if (durationMs > stats.MaxDurationMs)
                    stats.MaxDurationMs = durationMs;
            }
        }
    }

    /// <summary>
    /// Get success rate for observable gauge
    /// </summary>
    private double GetSuccessRateCallback()
    {
        lock (_statsLock)
        {
            var totalExecutions = _workflowStats.Values.Sum(s => s.TotalExecutions);
            if (totalExecutions == 0) return 0;

            var successfulExecutions = _workflowStats.Values.Sum(s => s.SuccessfulExecutions);
            return (successfulExecutions / (double)totalExecutions) * 100;
        }
    }

    /// <summary>
    /// Get current metrics snapshot for all workflows
    /// </summary>
    public WorkflowMetricsSnapshot GetSnapshot()
    {
        lock (_statsLock)
        {
            var workflows = _workflowStats.Values.Select(s => new WorkflowMetricsSummary
            {
                WorkflowId = s.WorkflowId,
                TotalExecutions = s.TotalExecutions,
                SuccessfulExecutions = s.SuccessfulExecutions,
                FailedExecutions = s.FailedExecutions,
                SuccessRate = s.TotalExecutions > 0
                    ? (s.SuccessfulExecutions / (double)s.TotalExecutions) * 100
                    : 0,
                AverageDurationMs = s.TotalExecutions > 0
                    ? s.TotalDuration / s.TotalExecutions
                    : 0,
                MinDurationMs = s.MinDurationMs,
                MaxDurationMs = s.MaxDurationMs,
                ActiveCount = s.ActiveCount,
                ErrorTypeBreakdown = new Dictionary<string, int>(s.ErrorTypes)
            }).ToList();

            return new WorkflowMetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                WorkflowMetrics = workflows,
                TotalActiveExecutions = _workflowStats.Values.Sum(s => s.ActiveCount),
                OverallSuccessRate = GetSuccessRateCallback()
            };
        }
    }

    /// <summary>
    /// Reset metrics for a specific workflow
    /// </summary>
    public void ResetWorkflowMetrics(string workflowId)
    {
        lock (_statsLock)
        {
            _workflowStats.Remove(workflowId);
        }
    }

    /// <summary>
    /// Reset all metrics
    /// </summary>
    public void ResetAllMetrics()
    {
        lock (_statsLock)
        {
            _workflowStats.Clear();
        }
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        _meter?.Dispose();
    }

    /// <summary>
    /// Internal statistics tracking
    /// </summary>
    private class WorkflowMetricStats
    {
        public string WorkflowId { get; }
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public int ActiveCount { get; set; }
        public double TotalDuration { get; set; }
        public double MinDurationMs { get; set; }
        public double MaxDurationMs { get; set; }
        public Dictionary<string, int> ErrorTypes { get; } = new();

        public WorkflowMetricStats(string workflowId)
        {
            WorkflowId = workflowId;
        }
    }
}

/// <summary>
/// Metrics snapshot for monitoring
/// </summary>
public class WorkflowMetricsSnapshot
{
    public DateTime Timestamp { get; set; }
    public List<WorkflowMetricsSummary> WorkflowMetrics { get; set; } = new();
    public int TotalActiveExecutions { get; set; }
    public double OverallSuccessRate { get; set; }
}

/// <summary>
/// Individual workflow metrics summary
/// </summary>
public class WorkflowMetricsSummary
{
    public string WorkflowId { get; set; } = "";
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double SuccessRate { get; set; }
    public double AverageDurationMs { get; set; }
    public double MinDurationMs { get; set; }
    public double MaxDurationMs { get; set; }
    public int ActiveCount { get; set; }
    public Dictionary<string, int> ErrorTypeBreakdown { get; set; } = new();
}
