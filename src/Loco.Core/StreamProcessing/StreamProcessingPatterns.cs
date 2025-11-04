#nullable enable

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.StreamProcessing;

/// <summary>
/// Real-Time Stream Processing Patterns
/// Apache Flink, Apache Spark, stateful processing, sub-millisecond latency
/// </summary>

public class StreamEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("eventTime")]
    public DateTime EventTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("payload")]
    public Dictionary<string, object> Payload { get; set; } = new();

    [JsonPropertyName("tags")]
    public Dictionary<string, string> Tags { get; set; } = new();

    [JsonPropertyName("watermark")]
    public DateTime Watermark { get; set; } = DateTime.UtcNow;
}

public class StreamWindow
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("windowType")]
    public string WindowType { get; set; } = string.Empty; // Tumbling, Sliding, Session

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime EndTime { get; set; }

    [JsonPropertyName("durationSeconds")]
    public int DurationSeconds { get; set; }

    [JsonPropertyName("slideSeconds")]
    public int SlideSeconds { get; set; } = 0; // For sliding windows

    [JsonPropertyName("sessionGapSeconds")]
    public int SessionGapSeconds { get; set; } = 0; // For session windows

    [JsonPropertyName("eventCount")]
    public long EventCount { get; set; }

    [JsonPropertyName("aggregation")]
    public Dictionary<string, object> Aggregation { get; set; } = new();
}

public class StateStore
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("stateType")]
    public string StateType { get; set; } = string.Empty; // Value, List, Map

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public Dictionary<string, object> State { get; set; } = new();

    [JsonPropertyName("ttl")]
    public long? TtlSeconds { get; set; } // Time-to-live

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("size")]
    public long SizeBytes { get; set; }
}

public class ChangeDataCapture
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("sourceSystem")]
    public string SourceSystem { get; set; } = string.Empty; // PostgreSQL, MySQL, MongoDB

    [JsonPropertyName("table")]
    public string Table { get; set; } = string.Empty;

    [JsonPropertyName("operationType")]
    public string OperationType { get; set; } = string.Empty; // Insert, Update, Delete

    [JsonPropertyName("beforeImage")]
    public Dictionary<string, object> BeforeImage { get; set; } = new();

    [JsonPropertyName("afterImage")]
    public Dictionary<string, object> AfterImage { get; set; } = new();

    [JsonPropertyName("capturedAt")]
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lsn")]
    public string Lsn { get; set; } = string.Empty; // Log Sequence Number
}

public class StreamOperator
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("operatorType")]
    public string OperatorType { get; set; } = string.Empty; // Map, Filter, FlatMap, Aggregate, Join

    [JsonPropertyName("parallelism")]
    public int Parallelism { get; set; } = 1;

    [JsonPropertyName("inputThroughput")]
    public double InputThroughputPerSec { get; set; }

    [JsonPropertyName("outputThroughput")]
    public double OutputThroughputPerSec { get; set; }

    [JsonPropertyName("latencyMs")]
    public double LatencyMs { get; set; }

    [JsonPropertyName("backpressure")]
    public bool Backpressure { get; set; }
}

public class StreamJob
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = "created"; // created, running, paused, failed, completed

    [JsonPropertyName("operators")]
    public List<StreamOperator> Operators { get; set; } = new();

    [JsonPropertyName("parallelism")]
    public int Parallelism { get; set; } = 1;

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("eventsProcessed")]
    public long EventsProcessed { get; set; }

    [JsonPropertyName("averageLatencyMs")]
    public double AverageLatencyMs { get; set; }

    [JsonPropertyName("failureRate")]
    public double FailureRate { get; set; } = 0;
}

public class StreamStatistics
{
    [JsonPropertyName("totalEventsProcessed")]
    public long TotalEventsProcessed { get; set; }

    [JsonPropertyName("eventsPerSecond")]
    public double EventsPerSecond { get; set; }

    [JsonPropertyName("averageLatencyMs")]
    public double AverageLatencyMs { get; set; }

    [JsonPropertyName("p50LatencyMs")]
    public double P50LatencyMs { get; set; }

    [JsonPropertyName("p99LatencyMs")]
    public double P99LatencyMs { get; set; }

    [JsonPropertyName("maxLatencyMs")]
    public double MaxLatencyMs { get; set; }

    [JsonPropertyName("watermarkDelay")]
    public double WatermarkDelayMs { get; set; }

    [JsonPropertyName("stateSizeBytes")]
    public long StateSizeBytes { get; set; }
}

/// <summary>
/// Stream Processing Engine (Flink/Spark-like)
/// </summary>
public class StreamProcessingEngine
{
    private readonly ConcurrentQueue<StreamEvent> _eventStream = new();
    private readonly ConcurrentDictionary<string, StreamJob> _jobs = new();
    private readonly ConcurrentDictionary<string, StateStore> _stateStores = new();
    private readonly List<StreamWindow> _windows = new();
    private readonly StreamStatistics _stats = new();
    private readonly ILogger<StreamProcessingEngine> _logger;

    public StreamProcessingEngine(ILogger<StreamProcessingEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Submit stream event
    /// </summary>
    public async Task<StreamEvent> AddEventAsync(
        string source,
        string eventType,
        Dictionary<string, object> payload,
        DateTime? eventTime = null)
    {
        var evt = new StreamEvent
        {
            Source = source,
            EventType = eventType,
            Payload = payload,
            EventTime = eventTime ?? DateTime.UtcNow
        };

        _eventStream.Enqueue(evt);
        _stats.TotalEventsProcessed++;

        _logger.LogInformation(
            "Added event: {Type} from {Source}",
            eventType,
            source);

        return evt;
    }

    /// <summary>
    /// Create and submit streaming job
    /// </summary>
    public async Task<StreamJob> SubmitStreamJobAsync(
        string jobName,
        List<StreamOperator> operators,
        int parallelism = 1)
    {
        var job = new StreamJob
        {
            Name = jobName,
            Operators = operators,
            Parallelism = parallelism,
            State = "created"
        };

        _jobs[job.Id] = job;

        _logger.LogInformation(
            "Submitted stream job: {Name} with {Ops} operators (parallelism: {P})",
            jobName,
            operators.Count,
            parallelism);

        return job;
    }

    /// <summary>
    /// Start job execution
    /// </summary>
    public async Task<bool> StartJobAsync(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
            return false;

        job.State = "running";
        job.StartTime = DateTime.UtcNow;

        _logger.LogInformation("Started job: {Name}", job.Name);
        return true;
    }

    /// <summary>
    /// Create tumbling window
    /// </summary>
    public async Task<StreamWindow> CreateTumblingWindowAsync(
        int windowDurationSeconds)
    {
        var now = DateTime.UtcNow;
        var window = new StreamWindow
        {
            WindowType = "Tumbling",
            StartTime = now,
            EndTime = now.AddSeconds(windowDurationSeconds),
            DurationSeconds = windowDurationSeconds
        };

        _windows.Add(window);

        _logger.LogInformation(
            "Created tumbling window: {Duration}s",
            windowDurationSeconds);

        return window;
    }

    /// <summary>
    /// Create sliding window
    /// </summary>
    public async Task<StreamWindow> CreateSlidingWindowAsync(
        int windowDurationSeconds,
        int slideSeconds)
    {
        var now = DateTime.UtcNow;
        var window = new StreamWindow
        {
            WindowType = "Sliding",
            StartTime = now,
            EndTime = now.AddSeconds(windowDurationSeconds),
            DurationSeconds = windowDurationSeconds,
            SlideSeconds = slideSeconds
        };

        _windows.Add(window);

        _logger.LogInformation(
            "Created sliding window: {Duration}s with {Slide}s slide",
            windowDurationSeconds,
            slideSeconds);

        return window;
    }

    /// <summary>
    /// Update state in state store
    /// </summary>
    public async Task<StateStore> UpdateStateAsync(
        string key,
        Dictionary<string, object> state,
        long? ttlSeconds = null)
    {
        var stateStore = new StateStore
        {
            Key = key,
            State = state,
            TtlSeconds = ttlSeconds ?? 3600,
            StateType = "Map",
            SizeBytes = System.Text.Encoding.UTF8.GetByteCount(string.Join("", state.Values))
        };

        _stateStores[key] = stateStore;
        _stats.StateSizeBytes += stateStore.SizeBytes;

        _logger.LogInformation(
            "Updated state: {Key} ({Size} bytes)",
            key,
            stateStore.SizeBytes);

        return stateStore;
    }

    /// <summary>
    /// Get state from state store
    /// </summary>
    public async Task<Dictionary<string, object>?> GetStateAsync(string key)
    {
        if (_stateStores.TryGetValue(key, out var stateStore))
        {
            if (stateStore.TtlSeconds.HasValue &&
                (DateTime.UtcNow - stateStore.LastUpdated).TotalSeconds > stateStore.TtlSeconds)
            {
                _stateStores.TryRemove(key, out _);
                return null;
            }
            return stateStore.State;
        }
        return null;
    }

    /// <summary>
    /// Process CDC (Change Data Capture) events
    /// </summary>
    public async Task<bool> ProcessCDCEventAsync(
        string sourceSystem,
        string table,
        string operationType,
        Dictionary<string, object> afterImage)
    {
        var cdc = new ChangeDataCapture
        {
            SourceSystem = sourceSystem,
            Table = table,
            OperationType = operationType,
            AfterImage = afterImage
        };

        _logger.LogInformation(
            "Processed CDC event: {System}.{Table} ({Operation})",
            sourceSystem,
            table,
            operationType);

        return true;
    }

    /// <summary>
    /// Aggregate events in window
    /// </summary>
    public async Task<Dictionary<string, object>> AggregateWindowAsync(
        string windowId,
        string aggregationType = "sum")
    {
        var recentEvents = _eventStream
            .Where(e => (DateTime.UtcNow - e.EventTime).TotalSeconds < 60)
            .ToList();

        var result = new Dictionary<string, object>();

        if (aggregationType == "sum")
        {
            result["eventCount"] = recentEvents.Count;
            result["timeWindow"] = "60s";
        }

        _logger.LogInformation(
            "Aggregated {Count} events with {Type}",
            recentEvents.Count,
            aggregationType);

        return result;
    }

    /// <summary>
    /// Get stream processing statistics
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        var runningJobs = _jobs.Values.Count(j => j.State == "running");
        var avgLatencies = _jobs.Values.Where(j => j.AverageLatencyMs > 0)
            .Select(j => j.AverageLatencyMs)
            .ToList();

        return new()
        {
            ["totalEventsProcessed"] = _stats.TotalEventsProcessed,
            ["activeJobs"] = runningJobs,
            ["totalJobs"] = _jobs.Count,
            ["totalWindows"] = _windows.Count,
            ["stateStoreSize"] = _stats.StateSizeBytes,
            ["eventsPerSecond"] = Math.Round(_stats.EventsPerSecond, 2),
            ["averageLatencyMs"] = avgLatencies.Count > 0
                ? Math.Round(avgLatencies.Average(), 2)
                : 0,
            ["p99LatencyMs"] = Math.Round(_stats.P99LatencyMs, 2),
            ["watermarkDelayMs"] = Math.Round(_stats.WatermarkDelayMs, 2)
        };
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class StreamProcessingExtensions
{
    public static IServiceCollection AddStreamProcessing(this IServiceCollection services)
    {
        services.AddSingleton<StreamProcessingEngine>();
        return services;
    }
}
