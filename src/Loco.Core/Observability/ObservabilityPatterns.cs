#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Observability;

/// <summary>
/// Observability Patterns - OpenTelemetry, Prometheus, Loki, Tempo
/// Three pillars: Metrics, Logs, Traces
/// </summary>

/// <summary>
/// OpenTelemetry metric (Prometheus format)
/// </summary>
public class OpenTelemetryMetric
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // Counter, Gauge, Histogram, Summary

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("attributes")]
    public Dictionary<string, object> Attributes { get; set; } = new();

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// OpenTelemetry trace (Jaeger format)
/// </summary>
public class OpenTelemetryTrace
{
    [JsonPropertyName("traceId")]
    public string TraceId { get; set; } = Guid.NewGuid().ToString().Replace("-", "");

    [JsonPropertyName("spans")]
    public List<Span> Spans { get; set; } = new();

    [JsonPropertyName("duration")]
    public long DurationMs { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "OK"; // OK, ERROR

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Trace span
/// </summary>
public class Span
{
    [JsonPropertyName("spanId")]
    public string SpanId { get; set; } = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 16);

    [JsonPropertyName("traceId")]
    public string TraceId { get; set; } = string.Empty;

    [JsonPropertyName("parentSpanId")]
    public string? ParentSpanId { get; set; }

    [JsonPropertyName("operationName")]
    public string OperationName { get; set; } = string.Empty;

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("duration")]
    public long DurationMs { get; set; }

    [JsonPropertyName("tags")]
    public Dictionary<string, object> Tags { get; set; } = new();

    [JsonPropertyName("logs")]
    public List<SpanLog> Logs { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = "OK";
}

/// <summary>
/// Span log
/// </summary>
public class SpanLog
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public Dictionary<string, object> Fields { get; set; } = new();
}

/// <summary>
/// Loki log entry
/// </summary>
public class LogEntry
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("level")]
    public string Level { get; set; } = "INFO"; // DEBUG, INFO, WARN, ERROR

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("labels")]
    public Dictionary<string, string> Labels { get; set; } = new();

    [JsonPropertyName("fields")]
    public Dictionary<string, object> Fields { get; set; } = new();

    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; }

    [JsonPropertyName("spanId")]
    public string? SpanId { get; set; }
}

/// <summary>
/// Metrics collector (Prometheus format)
/// </summary>
public class MetricsCollector
{
    private readonly ConcurrentDictionary<string, OpenTelemetryMetric> _metrics = new();
    private readonly ILogger<MetricsCollector> _logger;

    public MetricsCollector(ILogger<MetricsCollector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Record counter (monotonically increasing)
    /// </summary>
    public void RecordCounter(string name, double value, Dictionary<string, object>? attributes = null)
    {
        var key = $"{name}:{string.Join(":", attributes?.Values ?? new object[] { })}";

        if (!_metrics.TryGetValue(key, out var metric))
        {
            metric = new OpenTelemetryMetric
            {
                Name = name,
                Type = "Counter",
                Value = 0
            };
        }

        metric.Value += value;
        metric.Attributes = attributes ?? new();
        metric.Timestamp = DateTime.UtcNow;

        _metrics[key] = metric;
    }

    /// <summary>
    /// Record gauge (can increase or decrease)
    /// </summary>
    public void RecordGauge(string name, double value, Dictionary<string, object>? attributes = null)
    {
        var key = $"{name}:{string.Join(":", attributes?.Values ?? new object[] { })}";

        var metric = new OpenTelemetryMetric
        {
            Name = name,
            Type = "Gauge",
            Value = value,
            Attributes = attributes ?? new(),
            Timestamp = DateTime.UtcNow
        };

        _metrics[key] = metric;
    }

    /// <summary>
    /// Record histogram (distribution of values)
    /// </summary>
    public void RecordHistogram(string name, double value, Dictionary<string, object>? attributes = null)
    {
        var key = $"{name}:{string.Join(":", attributes?.Values ?? new object[] { })}";

        var metric = new OpenTelemetryMetric
        {
            Name = name,
            Type = "Histogram",
            Value = value,
            Attributes = attributes ?? new(),
            Timestamp = DateTime.UtcNow
        };

        _metrics[key] = metric;
    }

    /// <summary>
    /// Get all metrics in Prometheus format
    /// </summary>
    public string ExportPrometheus()
    {
        var lines = new List<string>();

        foreach (var kvp in _metrics)
        {
            var metric = kvp.Value;
            var labels = metric.Attributes.Count > 0
                ? "{" + string.Join(",", metric.Attributes.Select(a => $"{a.Key}=\"{a.Value}\"")) + "}"
                : "";

            lines.Add($"# HELP {metric.Name} {metric.Description}");
            lines.Add($"# TYPE {metric.Name} {metric.Type.ToLower()}");
            lines.Add($"{metric.Name}{labels} {metric.Value}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Get metrics count
    /// </summary>
    public int GetMetricsCount() => _metrics.Count;
}

/// <summary>
/// Trace collector (Jaeger/Tempo format)
/// </summary>
public class TraceCollector
{
    private readonly ConcurrentDictionary<string, OpenTelemetryTrace> _traces = new();
    private readonly ILogger<TraceCollector> _logger;

    public TraceCollector(ILogger<TraceCollector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Start span
    /// </summary>
    public Span StartSpan(
        string traceId,
        string operationName,
        string serviceName,
        string? parentSpanId = null)
    {
        var span = new Span
        {
            TraceId = traceId,
            ParentSpanId = parentSpanId,
            OperationName = operationName,
            ServiceName = serviceName,
            StartTime = DateTime.UtcNow
        };

        _logger.LogDebug(
            "Started span: {OperationName} in {ServiceName} (trace={TraceId})",
            operationName,
            serviceName,
            traceId);

        return span;
    }

    /// <summary>
    /// Finish span and record
    /// </summary>
    public void FinishSpan(Span span, long durationMs = 0)
    {
        span.DurationMs = durationMs == 0
            ? (long)(DateTime.UtcNow - span.StartTime).TotalMilliseconds
            : durationMs;

        // Get or create trace
        if (!_traces.TryGetValue(span.TraceId, out var trace))
        {
            trace = new OpenTelemetryTrace { TraceId = span.TraceId };
        }

        trace.Spans.Add(span);
        trace.DurationMs = (long)trace.Spans.Sum(s => s.DurationMs);

        _traces[span.TraceId] = trace;

        _logger.LogDebug(
            "Finished span: {OperationName} ({Duration}ms)",
            span.OperationName,
            span.DurationMs);
    }

    /// <summary>
    /// Get trace
    /// </summary>
    public OpenTelemetryTrace? GetTrace(string traceId)
    {
        _traces.TryGetValue(traceId, out var trace);
        return trace;
    }

    /// <summary>
    /// Get traces count
    /// </summary>
    public int GetTracesCount() => _traces.Count;
}

/// <summary>
/// Log collector (Loki format)
/// </summary>
public class LogCollector
{
    private readonly ConcurrentDictionary<string, List<LogEntry>> _logs = new(); // Stream key -> entries
    private readonly ILogger<LogCollector> _logger;

    public LogCollector(ILogger<LogCollector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Record log entry
    /// </summary>
    public void RecordLog(LogEntry log)
    {
        // Create stream key from labels (job, instance, pod, namespace)
        var streamKey = GenerateStreamKey(log.Labels);

        if (!_logs.ContainsKey(streamKey))
        {
            _logs[streamKey] = new();
        }

        _logs[streamKey].Add(log);

        _logger.LogDebug(
            "Recorded log: {Level} {Message}",
            log.Level,
            log.Message[..Math.Min(50, log.Message.Length)]);
    }

    /// <summary>
    /// Query logs by labels
    /// </summary>
    public List<LogEntry> QueryLogs(Dictionary<string, string> labels)
    {
        var results = new List<LogEntry>();

        foreach (var kvp in _logs)
        {
            foreach (var entry in kvp.Value)
            {
                var matches = true;

                foreach (var labelFilter in labels)
                {
                    if (!entry.Labels.TryGetValue(labelFilter.Key, out var value) ||
                        value != labelFilter.Value)
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    results.Add(entry);
                }
            }
        }

        return results.OrderByDescending(l => l.Timestamp).ToList();
    }

    /// <summary>
    /// Query logs with trace ID (correlate logs to traces)
    /// </summary>
    public List<LogEntry> QueryLogsByTraceId(string traceId)
    {
        return _logs.Values
            .SelectMany(entries => entries)
            .Where(log => log.TraceId == traceId)
            .OrderBy(log => log.Timestamp)
            .ToList();
    }

    private string GenerateStreamKey(Dictionary<string, string> labels)
    {
        var keys = labels.Keys.OrderBy(k => k).ToList();
        return string.Join(",", keys.Select(k => $"{k}={labels[k]}"));
    }

    /// <summary>
    /// Get logs count
    /// </summary>
    public int GetLogsCount() => _logs.Values.Sum(l => l.Count);
}

/// <summary>
/// Observability correlation - links metrics, traces, and logs
/// </summary>
public class ObservabilityCorrelation
{
    private readonly MetricsCollector _metricsCollector;
    private readonly TraceCollector _traceCollector;
    private readonly LogCollector _logCollector;
    private readonly ILogger<ObservabilityCorrelation> _logger;

    public ObservabilityCorrelation(
        MetricsCollector metricsCollector,
        TraceCollector traceCollector,
        LogCollector logCollector,
        ILogger<ObservabilityCorrelation> logger)
    {
        _metricsCollector = metricsCollector;
        _traceCollector = traceCollector;
        _logCollector = logCollector;
        _logger = logger;
    }

    /// <summary>
    /// Get correlated telemetry for trace
    /// </summary>
    public Dictionary<string, object> GetCorrelatedTelemetry(string traceId)
    {
        var trace = _traceCollector.GetTrace(traceId);
        var logs = _logCollector.QueryLogsByTraceId(traceId);

        return new()
        {
            ["trace"] = trace,
            ["logs"] = logs,
            ["spanCount"] = trace?.Spans.Count ?? 0,
            ["logCount"] = logs.Count,
            ["totalDuration"] = trace?.DurationMs ?? 0
        };
    }

    /// <summary>
    /// Get observability stats
    /// </summary>
    public Dictionary<string, object> GetObservabilityStats()
    {
        return new()
        {
            ["metricsCount"] = _metricsCollector.GetMetricsCount(),
            ["tracesCount"] = _traceCollector.GetTracesCount(),
            ["logsCount"] = _logCollector.GetLogsCount()
        };
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class ObservabilityExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        services.AddSingleton<MetricsCollector>();
        services.AddSingleton<TraceCollector>();
        services.AddSingleton<LogCollector>();
        services.AddSingleton<ObservabilityCorrelation>();
        return services;
    }
}
