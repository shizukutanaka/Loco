#nullable enable

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Observability;

public class TraceSpan
{
    [JsonPropertyName("traceId")]
    public string TraceId { get; set; } = string.Empty;

    [JsonPropertyName("spanId")]
    public string SpanId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("parentSpanId")]
    public string? ParentSpanId { get; set; }

    [JsonPropertyName("operationName")]
    public string OperationName { get; set; } = string.Empty;

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("endTime")]
    public DateTime EndTime { get; set; }

    [JsonPropertyName("durationMs")]
    public double DurationMs => (EndTime - StartTime).TotalMilliseconds;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "ok";

    [JsonPropertyName("tags")]
    public Dictionary<string, string> Tags { get; set; } = new();

    [JsonPropertyName("logs")]
    public List<(DateTime timestamp, string message)> Logs { get; set; } = new();
}

public class DistributedTrace
{
    [JsonPropertyName("traceId")]
    public string TraceId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("rootSpanId")]
    public string RootSpanId { get; set; } = string.Empty;

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyName("spans")]
    public List<TraceSpan> Spans { get; set; } = new();

    [JsonPropertyName("totalDurationMs")]
    public double TotalDurationMs => Spans.Any() ? Spans.Max(s => s.EndTime).Subtract(Spans.Min(s => s.StartTime)).TotalMilliseconds : 0;

    [JsonPropertyName("spanCount")]
    public int SpanCount => Spans.Count;

    [JsonPropertyName("errorCount")]
    public int ErrorCount => Spans.Count(s => s.Status != "ok");
}

public class TracingEngine
{
    private readonly ConcurrentDictionary<string, DistributedTrace> _traces = new();
    private readonly ILogger<TracingEngine> _logger;

    public TracingEngine(ILogger<TracingEngine> logger) => _logger = logger;

    public async Task<TraceSpan> StartSpanAsync(string traceId, string operationName, string? parentSpanId = null)
    {
        var span = new TraceSpan
        {
            TraceId = traceId,
            OperationName = operationName,
            ParentSpanId = parentSpanId
        };

        _logger.LogDebug("Started span: {TraceId} {Operation}", traceId, operationName);
        return span;
    }

    public async Task CompleteSpanAsync(TraceSpan span)
    {
        span.EndTime = DateTime.UtcNow;

        if (_traces.TryGetValue(span.TraceId, out var trace))
        {
            trace.Spans.Add(span);
        }
        else
        {
            var newTrace = new DistributedTrace { TraceId = span.TraceId, RootSpanId = span.SpanId };
            newTrace.Spans.Add(span);
            _traces[span.TraceId] = newTrace;
        }
    }

    public Dictionary<string, object> GetStats() => new()
    {
        ["traces"] = _traces.Count,
        ["totalSpans"] = _traces.Values.Sum(t => t.SpanCount),
        ["errorsDetected"] = _traces.Values.Sum(t => t.ErrorCount)
    };
}

public static class TracingExtensions
{
    public static IServiceCollection AddAdvancedTracing(this IServiceCollection services)
    {
        services.AddSingleton<TracingEngine>();
        return services;
    }
}
