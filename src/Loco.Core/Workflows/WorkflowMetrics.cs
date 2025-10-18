using System.Collections.Concurrent;
using System.Text;

namespace Loco.Core.Workflows;

/// <summary>
/// Metric type.
/// </summary>
public enum MetricType
{
    Counter,
    Gauge,
    Histogram,
    Timer
}

/// <summary>
/// A single metric measurement.
/// </summary>
public class Metric
{
    public string Name { get; set; } = "";
    public MetricType Type { get; set; }
    public double Value { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Tags { get; set; } = new();
}

/// <summary>
/// Aggregated metric statistics.
/// </summary>
public class MetricStatistics
{
    public string Name { get; set; } = "";
    public MetricType Type { get; set; }
    public int Count { get; set; }
    public double Sum { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Average => Count > 0 ? Sum / Count : 0;
    public double? P50 { get; set; }
    public double? P95 { get; set; }
    public double? P99 { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
}

/// <summary>
/// Collects and aggregates workflow execution metrics.
/// </summary>
public class WorkflowMetricsCollector
{
    private readonly ConcurrentDictionary<string, List<Metric>> _metrics = new();
    private readonly ConcurrentDictionary<string, MetricStatistics> _statistics = new();
    private readonly int _maxMetricsPerType;
    private readonly object _statsLock = new();

    public WorkflowMetricsCollector(int maxMetricsPerType = 10000)
    {
        _maxMetricsPerType = maxMetricsPerType;
    }

    /// <summary>
    /// Records a counter metric (incremental value).
    /// </summary>
    public void RecordCounter(string name, double value = 1, Dictionary<string, string>? tags = null)
    {
        RecordMetric(new Metric
        {
            Name = name,
            Type = MetricType.Counter,
            Value = value,
            Tags = tags ?? new Dictionary<string, string>()
        });
    }

    /// <summary>
    /// Records a gauge metric (point-in-time value).
    /// </summary>
    public void RecordGauge(string name, double value, Dictionary<string, string>? tags = null)
    {
        RecordMetric(new Metric
        {
            Name = name,
            Type = MetricType.Gauge,
            Value = value,
            Tags = tags ?? new Dictionary<string, string>()
        });
    }

    /// <summary>
    /// Records a histogram metric (distribution of values).
    /// </summary>
    public void RecordHistogram(string name, double value, Dictionary<string, string>? tags = null)
    {
        RecordMetric(new Metric
        {
            Name = name,
            Type = MetricType.Histogram,
            Value = value,
            Tags = tags ?? new Dictionary<string, string>()
        });
    }

    /// <summary>
    /// Records a timer metric (duration measurement).
    /// </summary>
    public void RecordTimer(string name, TimeSpan duration, Dictionary<string, string>? tags = null)
    {
        RecordMetric(new Metric
        {
            Name = name,
            Type = MetricType.Timer,
            Value = duration.TotalMilliseconds,
            Tags = tags ?? new Dictionary<string, string>()
        });
    }

    /// <summary>
    /// Records a metric.
    /// </summary>
    private void RecordMetric(Metric metric)
    {
        var key = GetMetricKey(metric.Name, metric.Type);

        // Add to raw metrics
        var metrics = _metrics.GetOrAdd(key, _ => new List<Metric>());
        lock (metrics)
        {
            metrics.Add(metric);

            // Trim if needed
            if (metrics.Count > _maxMetricsPerType)
            {
                metrics.RemoveRange(0, metrics.Count - _maxMetricsPerType);
            }
        }

        // Update statistics
        UpdateStatistics(key, metric);
    }

    /// <summary>
    /// Updates aggregated statistics for a metric.
    /// </summary>
    private void UpdateStatistics(string key, Metric metric)
    {
        lock (_statsLock)
        {
            if (!_statistics.TryGetValue(key, out var stats))
            {
                stats = new MetricStatistics
                {
                    Name = metric.Name,
                    Type = metric.Type,
                    Min = metric.Value,
                    Max = metric.Value,
                    FirstSeen = metric.Timestamp
                };
                _statistics[key] = stats;
            }

            stats.Count++;
            stats.Sum += metric.Value;
            stats.Min = Math.Min(stats.Min, metric.Value);
            stats.Max = Math.Max(stats.Max, metric.Value);
            stats.LastSeen = metric.Timestamp;

            // Calculate percentiles for histograms and timers
            if (metric.Type == MetricType.Histogram || metric.Type == MetricType.Timer)
            {
                CalculatePercentiles(key, stats);
            }
        }
    }

    /// <summary>
    /// Calculates percentiles for a metric.
    /// </summary>
    private void CalculatePercentiles(string key, MetricStatistics stats)
    {
        if (!_metrics.TryGetValue(key, out var metrics))
            return;

        List<double> values;
        lock (metrics)
        {
            values = metrics.Select(m => m.Value).OrderBy(v => v).ToList();
        }

        if (values.Count == 0)
            return;

        stats.P50 = GetPercentile(values, 0.50);
        stats.P95 = GetPercentile(values, 0.95);
        stats.P99 = GetPercentile(values, 0.99);
    }

    /// <summary>
    /// Calculates a percentile value.
    /// </summary>
    private static double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
            return 0;

        var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        index = Math.Max(0, Math.Min(sortedValues.Count - 1, index));
        return sortedValues[index];
    }

    /// <summary>
    /// Gets statistics for a specific metric.
    /// </summary>
    public MetricStatistics? GetStatistics(string name, MetricType type)
    {
        var key = GetMetricKey(name, type);
        lock (_statsLock)
        {
            return _statistics.TryGetValue(key, out var stats) ? stats : null;
        }
    }

    /// <summary>
    /// Gets all statistics.
    /// </summary>
    public List<MetricStatistics> GetAllStatistics()
    {
        lock (_statsLock)
        {
            return _statistics.Values.ToList();
        }
    }

    /// <summary>
    /// Gets recent metrics.
    /// </summary>
    public List<Metric> GetRecentMetrics(string name, MetricType type, int limit = 100)
    {
        var key = GetMetricKey(name, type);
        if (!_metrics.TryGetValue(key, out var metrics))
            return new List<Metric>();

        lock (metrics)
        {
            return metrics.TakeLast(limit).ToList();
        }
    }

    /// <summary>
    /// Clears all metrics.
    /// </summary>
    public void Clear()
    {
        _metrics.Clear();
        lock (_statsLock)
        {
            _statistics.Clear();
        }
    }

    /// <summary>
    /// Generates a metrics report.
    /// </summary>
    public string GenerateMetricsReport()
    {
        var sb = new StringBuilder();

        sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║ WORKFLOW METRICS REPORT                                                       ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        var allStats = GetAllStatistics();
        if (allStats.Count == 0)
        {
            sb.AppendLine("No metrics collected yet.");
            sb.AppendLine();
            return sb.ToString();
        }

        // Group by type
        var groupedStats = allStats.GroupBy(s => s.Type).OrderBy(g => g.Key);

        foreach (var group in groupedStats)
        {
            sb.AppendLine($"═══ {group.Key} Metrics ═══");
            sb.AppendLine();

            foreach (var stats in group.OrderBy(s => s.Name))
            {
                sb.AppendLine($"📊 {stats.Name}");
                sb.AppendLine($"   Count: {stats.Count:N0}");
                sb.AppendLine($"   Sum: {stats.Sum:N2}");
                sb.AppendLine($"   Average: {stats.Average:N2}");
                sb.AppendLine($"   Min: {stats.Min:N2}");
                sb.AppendLine($"   Max: {stats.Max:N2}");

                if (stats.P50.HasValue)
                {
                    sb.AppendLine($"   P50: {stats.P50.Value:N2}");
                    sb.AppendLine($"   P95: {stats.P95!.Value:N2}");
                    sb.AppendLine($"   P99: {stats.P99!.Value:N2}");
                }

                sb.AppendLine($"   First seen: {stats.FirstSeen:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"   Last seen: {stats.LastSeen:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a summary of key metrics.
    /// </summary>
    public string GenerateMetricsSummary()
    {
        var sb = new StringBuilder();

        // Workflow execution metrics
        var executionsStarted = GetStatistics("workflow.executions.started", MetricType.Counter);
        var executionsCompleted = GetStatistics("workflow.executions.completed", MetricType.Counter);
        var executionsFailed = GetStatistics("workflow.executions.failed", MetricType.Counter);
        var executionDuration = GetStatistics("workflow.execution.duration", MetricType.Timer);

        sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║ METRICS SUMMARY                                                               ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        sb.AppendLine("Workflow Executions:");
        sb.AppendLine($"  Started: {executionsStarted?.Sum ?? 0:N0}");
        sb.AppendLine($"  Completed: {executionsCompleted?.Sum ?? 0:N0}");
        sb.AppendLine($"  Failed: {executionsFailed?.Sum ?? 0:N0}");

        if (executionsStarted != null && executionsStarted.Sum > 0)
        {
            var completionRate = (executionsCompleted?.Sum ?? 0) / executionsStarted.Sum * 100;
            sb.AppendLine($"  Success Rate: {completionRate:F1}%");
        }

        sb.AppendLine();

        if (executionDuration != null)
        {
            sb.AppendLine("Execution Duration:");
            sb.AppendLine($"  Average: {executionDuration.Average:N0}ms");
            sb.AppendLine($"  Min: {executionDuration.Min:N0}ms");
            sb.AppendLine($"  Max: {executionDuration.Max:N0}ms");
            if (executionDuration.P50.HasValue)
            {
                sb.AppendLine($"  P50: {executionDuration.P50.Value:N0}ms");
                sb.AppendLine($"  P95: {executionDuration.P95!.Value:N0}ms");
                sb.AppendLine($"  P99: {executionDuration.P99!.Value:N0}ms");
            }
            sb.AppendLine();
        }

        // Step metrics
        var stepsExecuted = GetStatistics("workflow.steps.executed", MetricType.Counter);
        var stepsFailed = GetStatistics("workflow.steps.failed", MetricType.Counter);
        var stepDuration = GetStatistics("workflow.step.duration", MetricType.Timer);

        if (stepsExecuted != null)
        {
            sb.AppendLine("Step Execution:");
            sb.AppendLine($"  Total Steps: {stepsExecuted.Sum:N0}");
            sb.AppendLine($"  Failed Steps: {stepsFailed?.Sum ?? 0:N0}");

            if (stepDuration != null)
            {
                sb.AppendLine($"  Avg Duration: {stepDuration.Average:N0}ms");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string GetMetricKey(string name, MetricType type)
    {
        return $"{type}:{name}";
    }
}

/// <summary>
/// Standard workflow metrics names.
/// </summary>
public static class WorkflowMetrics
{
    // Execution metrics
    public const string ExecutionsStarted = "workflow.executions.started";
    public const string ExecutionsCompleted = "workflow.executions.completed";
    public const string ExecutionsFailed = "workflow.executions.failed";
    public const string ExecutionsCancelled = "workflow.executions.cancelled";
    public const string ExecutionDuration = "workflow.execution.duration";

    // Step metrics
    public const string StepsExecuted = "workflow.steps.executed";
    public const string StepsFailed = "workflow.steps.failed";
    public const string StepsRetried = "workflow.steps.retried";
    public const string StepDuration = "workflow.step.duration";

    // Performance metrics
    public const string ActiveExecutions = "workflow.active_executions";
    public const string QueuedExecutions = "workflow.queued_executions";
    public const string MemoryUsage = "workflow.memory_usage";
    public const string CpuUsage = "workflow.cpu_usage";

    // Error metrics
    public const string ValidationErrors = "workflow.validation_errors";
    public const string RuntimeErrors = "workflow.runtime_errors";
    public const string TimeoutErrors = "workflow.timeout_errors";
}
