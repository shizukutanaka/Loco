// Phase 3: Advanced Workflow Metrics Collector
// Comprehensive OpenTelemetry metrics with workflow-specific tracking

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;

namespace Loco.Core.Diagnostics;

/// <summary>
/// Advanced metrics collector for Loco workflows
/// Tracks execution patterns, performance, and reliability metrics
/// </summary>
public class WorkflowMetricsCollector
{
    private readonly string _serviceName = "Loco.Workflow";
    private readonly Meter _meter;
    private readonly ILogger<WorkflowMetricsCollector> _logger;

    // Counters
    private readonly Counter<int> _executionStartedCounter;
    private readonly Counter<int> _executionSuccessCounter;
    private readonly Counter<int> _executionFailureCounter;
    private readonly Counter<int> _stepExecutedCounter;
    private readonly Counter<int> _stepFailureCounter;
    private readonly Counter<int> _compensationExecutedCounter;

    // Histograms
    private readonly Histogram<double> _executionDurationMs;
    private readonly Histogram<double> _stepDurationMs;
    private readonly Histogram<double> _queueDepth;
    private readonly Histogram<double> _retryAttempts;

    // Gauge
    private readonly ObservableGauge<int> _activeExecutionsGauge;
    private readonly UpDownCounter<int> _activeExecutionsCounter;

    // Tracking collections
    private readonly ConcurrentDictionary<string, ExecutionMetrics> _executionMetrics;
    private readonly ConcurrentDictionary<string, WorkflowMetrics> _workflowMetrics;
    private readonly ConcurrentDictionary<string, long> _activeExecutions;

    public WorkflowMetricsCollector(ILogger<WorkflowMetricsCollector> logger)
    {
        _logger = logger;
        _meter = new Meter(_serviceName, "1.0.0");

        _executionMetrics = new ConcurrentDictionary<string, ExecutionMetrics>();
        _workflowMetrics = new ConcurrentDictionary<string, WorkflowMetrics>();
        _activeExecutions = new ConcurrentDictionary<string, long>();

        // Initialize counters
        _executionStartedCounter = _meter.CreateCounter<int>(
            "workflow.executions.started",
            unit: "{execution}",
            description: "Total workflow executions started");

        _executionSuccessCounter = _meter.CreateCounter<int>(
            "workflow.executions.success",
            unit: "{execution}",
            description: "Total workflow executions completed successfully");

        _executionFailureCounter = _meter.CreateCounter<int>(
            "workflow.executions.failure",
            unit: "{execution}",
            description: "Total workflow executions failed");

        _stepExecutedCounter = _meter.CreateCounter<int>(
            "workflow.steps.executed",
            unit: "{step}",
            description: "Total workflow steps executed");

        _stepFailureCounter = _meter.CreateCounter<int>(
            "workflow.steps.failure",
            unit: "{step}",
            description: "Total workflow steps failed");

        _compensationExecutedCounter = _meter.CreateCounter<int>(
            "workflow.compensations.executed",
            unit: "{compensation}",
            description: "Total compensations executed");

        // Initialize histograms
        _executionDurationMs = _meter.CreateHistogram<double>(
            "workflow.execution.duration",
            unit: "ms",
            description: "Workflow execution duration in milliseconds");

        _stepDurationMs = _meter.CreateHistogram<double>(
            "workflow.step.duration",
            unit: "ms",
            description: "Individual step execution duration");

        _queueDepth = _meter.CreateHistogram<double>(
            "workflow.queue.depth",
            unit: "{item}",
            description: "Number of queued workflow executions");

        _retryAttempts = _meter.CreateHistogram<double>(
            "workflow.retry.attempts",
            unit: "{attempt}",
            description: "Number of retry attempts per execution");

        // Initialize gauge
        _activeExecutionsCounter = _meter.CreateUpDownCounter<int>(
            "workflow.executions.active",
            unit: "{execution}",
            description: "Currently active workflow executions");

        _activeExecutionsGauge = _meter.CreateObservableGauge<int>(
            "workflow.executions.active.gauge",
            () => _activeExecutions.Count,
            unit: "{execution}",
            description: "Observable gauge of active executions");

        _logger.LogInformation("Workflow metrics collector initialized");
    }

    /// <summary>
    /// Record workflow execution started
    /// </summary>
    public void RecordExecutionStarted(
        string executionId,
        string workflowId,
        string? userId = null,
        string? triggerType = null)
    {
        _executionStartedCounter.Add(1, new KeyValuePair<string, object?>[]
        {
            new("workflow.id", workflowId),
            new("execution.id", executionId),
            new("trigger.type", triggerType ?? "manual"),
        });

        _activeExecutionsCounter.Add(1);
        _activeExecutions.TryAdd(executionId, Stopwatch.GetTimestamp());

        var metrics = new ExecutionMetrics
        {
            ExecutionId = executionId,
            WorkflowId = workflowId,
            UserId = userId,
            TriggerType = triggerType ?? "manual",
            StartedAt = DateTime.UtcNow,
            StartTicks = Stopwatch.GetTimestamp(),
        };

        _executionMetrics.TryAdd(executionId, metrics);

        _workflowMetrics.AddOrUpdate(
            workflowId,
            new WorkflowMetrics { WorkflowId = workflowId },
            (key, existing) => existing);
    }

    /// <summary>
    /// Record workflow execution completed
    /// </summary>
    public void RecordExecutionCompleted(
        string executionId,
        string workflowId,
        bool success,
        string? errorType = null,
        int? retryCount = null)
    {
        if (_executionMetrics.TryGetValue(executionId, out var metrics))
        {
            metrics.CompletedAt = DateTime.UtcNow;
            metrics.Success = success;
            metrics.ErrorType = errorType;
            metrics.RetryCount = retryCount ?? 0;

            var durationMs = metrics.GetDurationMs();
            _executionDurationMs.Record(durationMs, new KeyValuePair<string, object?>[]
            {
                new("workflow.id", workflowId),
                new("status", success ? "success" : "failure"),
                new("error.type", errorType ?? "none"),
            });

            if (retryCount.HasValue && retryCount > 0)
            {
                _retryAttempts.Record(retryCount.Value, new KeyValuePair<string, object?>[]
                {
                    new("workflow.id", workflowId),
                });
            }
        }

        if (success)
        {
            _executionSuccessCounter.Add(1, new KeyValuePair<string, object?>[]
            {
                new("workflow.id", workflowId),
            });

            if (_workflowMetrics.TryGetValue(workflowId, out var workflowMetrics))
            {
                workflowMetrics.TotalExecutions++;
                workflowMetrics.SuccessfulExecutions++;
            }
        }
        else
        {
            _executionFailureCounter.Add(1, new KeyValuePair<string, object?>[]
            {
                new("workflow.id", workflowId),
                new("error.type", errorType ?? "unknown"),
            });

            if (_workflowMetrics.TryGetValue(workflowId, out var workflowMetrics))
            {
                workflowMetrics.TotalExecutions++;
                workflowMetrics.FailedExecutions++;
            }
        }

        _activeExecutionsCounter.Add(-1);
        _activeExecutions.TryRemove(executionId, out _);
    }

    /// <summary>
    /// Record step execution
    /// </summary>
    public void RecordStepExecuted(
        string executionId,
        string workflowId,
        string stepId,
        string stepName,
        double durationMs,
        bool success,
        string? errorType = null)
    {
        _stepExecutedCounter.Add(1, new KeyValuePair<string, object?>[]
        {
            new("workflow.id", workflowId),
            new("step.id", stepId),
            new("step.name", stepName),
        });

        _stepDurationMs.Record(durationMs, new KeyValuePair<string, object?>[]
        {
            new("workflow.id", workflowId),
            new("step.id", stepId),
            new("status", success ? "success" : "failure"),
        });

        if (!success)
        {
            _stepFailureCounter.Add(1, new KeyValuePair<string, object?>[]
            {
                new("workflow.id", workflowId),
                new("step.id", stepId),
                new("error.type", errorType ?? "unknown"),
            });
        }

        if (_executionMetrics.TryGetValue(executionId, out var metrics))
        {
            metrics.ExecutedSteps++;
            if (!success)
                metrics.FailedSteps++;
        }
    }

    /// <summary>
    /// Record compensation execution
    /// </summary>
    public void RecordCompensationExecuted(
        string executionId,
        string workflowId,
        string stepId,
        double durationMs,
        bool success)
    {
        _compensationExecutedCounter.Add(1, new KeyValuePair<string, object?>[]
        {
            new("workflow.id", workflowId),
            new("step.id", stepId),
            new("status", success ? "success" : "failure"),
        });

        if (_executionMetrics.TryGetValue(executionId, out var metrics))
        {
            metrics.CompensationsExecuted++;
        }
    }

    /// <summary>
    /// Get metrics for a specific execution
    /// </summary>
    public ExecutionMetrics? GetExecutionMetrics(string executionId)
    {
        _executionMetrics.TryGetValue(executionId, out var metrics);
        return metrics;
    }

    /// <summary>
    /// Get metrics for a specific workflow
    /// </summary>
    public WorkflowMetrics? GetWorkflowMetrics(string workflowId)
    {
        _workflowMetrics.TryGetValue(workflowId, out var metrics);
        return metrics;
    }

    /// <summary>
    /// Get all metrics summary
    /// </summary>
    public MetricsSummary GetMetricsSummary()
    {
        var completedExecutions = _executionMetrics
            .Values
            .Where(m => m.CompletedAt.HasValue)
            .ToList();

        return new MetricsSummary
        {
            ActiveExecutions = _activeExecutions.Count,
            TotalExecutions = _executionMetrics.Count,
            CompletedExecutions = completedExecutions.Count,
            SuccessfulExecutions = completedExecutions.Count(m => m.Success),
            FailedExecutions = completedExecutions.Count(m => !m.Success),
            SuccessRate = completedExecutions.Count > 0
                ? (double)completedExecutions.Count(m => m.Success) / completedExecutions.Count
                : 0,
            AverageDurationMs = completedExecutions.Any()
                ? completedExecutions.Average(m => m.GetDurationMs())
                : 0,
            AverageStepsPerExecution = completedExecutions.Any()
                ? completedExecutions.Average(m => m.ExecutedSteps)
                : 0,
            AverageRetryAttempts = completedExecutions.Any()
                ? completedExecutions.Average(m => m.RetryCount)
                : 0,
            TotalWorkflows = _workflowMetrics.Count,
            TopWorkflows = _workflowMetrics
                .Values
                .OrderByDescending(m => m.TotalExecutions)
                .Take(5)
                .Select(m => new WorkflowMetricsSummary
                {
                    WorkflowId = m.WorkflowId,
                    TotalExecutions = m.TotalExecutions,
                    SuccessfulExecutions = m.SuccessfulExecutions,
                    FailedExecutions = m.FailedExecutions,
                    SuccessRate = m.TotalExecutions > 0
                        ? (double)m.SuccessfulExecutions / m.TotalExecutions
                        : 0,
                })
                .ToList(),
        };
    }

    /// <summary>
    /// Clean old metrics (keep only recent data)
    /// </summary>
    public int CleanupOldMetrics(TimeSpan olderThan)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        var oldKeys = _executionMetrics
            .Where(kvp => kvp.Value.CompletedAt.HasValue && kvp.Value.CompletedAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        int removed = 0;
        foreach (var key in oldKeys)
        {
            if (_executionMetrics.TryRemove(key, out _))
                removed++;
        }

        if (removed > 0)
        {
            _logger.LogInformation("Cleaned up {Count} old execution metrics", removed);
        }

        return removed;
    }

    /// <summary>
    /// Export meter for OpenTelemetry
    /// </summary>
    public Meter GetMeter() => _meter;

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        _meter?.Dispose();
    }
}

/// <summary>
/// Metrics for a single execution
/// </summary>
public class ExecutionMetrics
{
    public string ExecutionId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long StartTicks { get; set; }
    public bool Success { get; set; }
    public string? ErrorType { get; set; }
    public int RetryCount { get; set; }
    public int ExecutedSteps { get; set; }
    public int FailedSteps { get; set; }
    public int CompensationsExecuted { get; set; }

    public double GetDurationMs()
    {
        if (!CompletedAt.HasValue)
            return (double)(Stopwatch.GetTimestamp() - StartTicks) / Stopwatch.Frequency * 1000;

        return (CompletedAt.Value - StartedAt).TotalMilliseconds;
    }
}

/// <summary>
/// Metrics for a workflow
/// </summary>
public class WorkflowMetrics
{
    public string WorkflowId { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }

    public double SuccessRate =>
        TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions : 0;
}

/// <summary>
/// Overall metrics summary
/// </summary>
public class MetricsSummary
{
    public int ActiveExecutions { get; set; }
    public int TotalExecutions { get; set; }
    public int CompletedExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double SuccessRate { get; set; }
    public double AverageDurationMs { get; set; }
    public double AverageStepsPerExecution { get; set; }
    public double AverageRetryAttempts { get; set; }
    public int TotalWorkflows { get; set; }
    public List<WorkflowMetricsSummary> TopWorkflows { get; set; } = new();
}

/// <summary>
/// Workflow metrics summary
/// </summary>
public class WorkflowMetricsSummary
{
    public string WorkflowId { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double SuccessRate { get; set; }
}
