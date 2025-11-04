#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.EventStreaming;

/// <summary>
/// Event Streaming Patterns - Kafka, Pulsar, AI-Driven Streaming
/// High-throughput, reliable event processing for microservices
/// </summary>

/// <summary>
/// Stream event with metadata
/// </summary>
public class StreamEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("partition")]
    public int Partition { get; set; }

    [JsonPropertyName("offset")]
    public long Offset { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = new();

    [JsonPropertyName("schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = "application/json";
}

/// <summary>
/// Stream topic configuration
/// </summary>
public class StreamTopic
{
    public string Name { get; set; } = string.Empty;
    public int Partitions { get; set; } = 3;
    public short ReplicationFactor { get; set; } = 2;
    public Dictionary<string, string> Config { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Retention policy
    /// </summary>
    public TimeSpan RetentionTime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Compression (gzip, snappy, lz4, zstd)
    /// </summary>
    public string CompressionType { get; set; } = "snappy";

    /// <summary>
    /// Required acknowledgments (0, 1, -1/all)
    /// </summary>
    public short Acks { get; set; } = -1; // All replicas
}

/// <summary>
/// Stream consumer group
/// </summary>
public class StreamConsumerGroup
{
    public string GroupId { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public Dictionary<int, long> PartitionOffsets { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string BalanceStrategy { get; set; } = "RoundRobin";

    /// <summary>
    /// Session timeout - time consumer can be inactive
    /// </summary>
    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Heartbeat interval
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Stream consumer
/// </summary>
public interface IStreamConsumer : IAsyncDisposable
{
    Task<IEnumerable<StreamEvent>> PollAsync(TimeSpan timeout);
    Task CommitAsync();
    Task CommitAsync(int partition, long offset);
    Task SeekAsync(int partition, long offset);
    Task<long> GetPositionAsync(int partition);
}

/// <summary>
/// Stream producer
/// </summary>
public interface IStreamProducer : IAsyncDisposable
{
    Task<ProduceResult> ProduceAsync(StreamEvent @event);
    Task<IEnumerable<ProduceResult>> ProduceBatchAsync(IEnumerable<StreamEvent> events);
    Task FlushAsync();
}

/// <summary>
/// Produce result
/// </summary>
public class ProduceResult
{
    public string EventId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Topic { get; set; } = string.Empty;
    public int Partition { get; set; }
    public long Offset { get; set; }
    public string? Error { get; set; }
    public long ExecutionTimeMs { get; set; }
}

/// <summary>
/// In-memory event streaming implementation
/// Production: Use Apache Kafka, Pulsar, or Confluent Cloud
/// </summary>
public class InMemoryEventStream : IStreamProducer, IStreamConsumer
{
    private readonly StreamTopic _topic;
    private readonly StreamConsumerGroup _consumerGroup;
    private readonly ILogger<InMemoryEventStream> _logger;

    private readonly List<StreamEvent>[] _partitions;
    private long[] _offsets;
    private readonly Dictionary<int, long> _consumerOffsets = new();

    public InMemoryEventStream(
        StreamTopic topic,
        StreamConsumerGroup consumerGroup,
        ILogger<InMemoryEventStream> logger)
    {
        _topic = topic;
        _consumerGroup = consumerGroup;
        _logger = logger;

        _partitions = new List<StreamEvent>[topic.Partitions];
        _offsets = new long[topic.Partitions];

        for (int i = 0; i < topic.Partitions; i++)
        {
            _partitions[i] = new();
            _consumerOffsets[i] = consumerGroup.PartitionOffsets.GetValueOrDefault(i, 0);
        }
    }

    /// <summary>
    /// Produce event to stream
    /// </summary>
    public async Task<ProduceResult> ProduceAsync(StreamEvent @event)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            @event.Topic = _topic.Name;

            // Determine partition (use key if available, else round-robin)
            int partition;
            if (!string.IsNullOrEmpty(@event.Key))
            {
                partition = Math.Abs(@event.Key.GetHashCode()) % _topic.Partitions;
            }
            else
            {
                partition = (int)(_offsets.Sum() % _topic.Partitions);
            }

            @event.Partition = partition;
            @event.Offset = _offsets[partition]++;

            _partitions[partition].Add(@event);

            stopwatch.Stop();

            _logger.LogInformation(
                "Produced event: topic={Topic}, partition={Partition}, offset={Offset}, time={Time}ms",
                _topic.Name,
                partition,
                @event.Offset,
                stopwatch.ElapsedMilliseconds);

            return new ProduceResult
            {
                EventId = @event.Id,
                Success = true,
                Topic = _topic.Name,
                Partition = partition,
                Offset = @event.Offset,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Failed to produce event");

            return new ProduceResult
            {
                EventId = @event.Id,
                Success = false,
                Error = ex.Message,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Produce batch of events
    /// </summary>
    public async Task<IEnumerable<ProduceResult>> ProduceBatchAsync(IEnumerable<StreamEvent> events)
    {
        var results = new List<ProduceResult>();

        foreach (var @event in events)
        {
            var result = await ProduceAsync(@event);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Flush pending events
    /// </summary>
    public async Task FlushAsync()
    {
        _logger.LogInformation("Flushing stream");
        // In-memory implementation: no async flushing needed
    }

    /// <summary>
    /// Poll for events
    /// </summary>
    public async Task<IEnumerable<StreamEvent>> PollAsync(TimeSpan timeout)
    {
        var events = new List<StreamEvent>();

        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            bool foundAny = false;

            for (int partition = 0; partition < _topic.Partitions; partition++)
            {
                long currentOffset = _consumerOffsets[partition];

                if (currentOffset < _partitions[partition].Count)
                {
                    events.Add(_partitions[partition][(int)currentOffset]);
                    _consumerOffsets[partition]++;
                    foundAny = true;
                }
            }

            if (foundAny || events.Count > 0)
                break;

            await Task.Delay(10);
        }

        _logger.LogInformation(
            "Polled {Count} events from consumer group {Group}",
            events.Count,
            _consumerGroup.GroupId);

        return events;
    }

    /// <summary>
    /// Commit offsets
    /// </summary>
    public async Task CommitAsync()
    {
        _logger.LogInformation(
            "Committed offsets for consumer group {Group}",
            _consumerGroup.GroupId);
    }

    /// <summary>
    /// Commit specific partition offset
    /// </summary>
    public async Task CommitAsync(int partition, long offset)
    {
        if (partition >= 0 && partition < _topic.Partitions)
        {
            _consumerOffsets[partition] = offset + 1;

            _logger.LogInformation(
                "Committed partition {Partition} at offset {Offset}",
                partition,
                offset);
        }
    }

    /// <summary>
    /// Seek to offset
    /// </summary>
    public async Task SeekAsync(int partition, long offset)
    {
        if (partition >= 0 && partition < _topic.Partitions)
        {
            _consumerOffsets[partition] = offset;

            _logger.LogInformation(
                "Seeked partition {Partition} to offset {Offset}",
                partition,
                offset);
        }
    }

    /// <summary>
    /// Get current position
    /// </summary>
    public async Task<long> GetPositionAsync(int partition)
    {
        if (partition >= 0 && partition < _topic.Partitions)
        {
            return _consumerOffsets[partition];
        }

        return -1;
    }

    /// <summary>
    /// Cleanup
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing event stream");
    }
}

/// <summary>
/// Stream processor - transforms and filters events
/// </summary>
public abstract class StreamProcessor
{
    protected readonly ILogger<StreamProcessor> Logger;

    public StreamProcessor(ILogger<StreamProcessor> logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Process event
    /// </summary>
    public abstract Task<StreamEvent?> ProcessAsync(StreamEvent @event);

    /// <summary>
    /// Process batch
    /// </summary>
    public virtual async Task<IEnumerable<StreamEvent>> ProcessBatchAsync(IEnumerable<StreamEvent> events)
    {
        var results = new List<StreamEvent>();

        foreach (var @event in events)
        {
            var result = await ProcessAsync(@event);
            if (result != null)
            {
                results.Add(result);
            }
        }

        return results;
    }
}

/// <summary>
/// AI-driven event enrichment processor
/// </summary>
public class AiEnrichmentProcessor : StreamProcessor
{
    private readonly Dictionary<string, object> _mlModels = new();

    public AiEnrichmentProcessor(ILogger<AiEnrichmentProcessor> logger)
        : base(logger)
    {
    }

    /// <summary>
    /// Enrich event with AI predictions
    /// </summary>
    public override async Task<StreamEvent?> ProcessAsync(StreamEvent @event)
    {
        try
        {
            // Simulate AI enrichment
            var enriched = new StreamEvent
            {
                Id = @event.Id,
                Topic = @event.Topic,
                Key = @event.Key,
                Value = @event.Value,
                Partition = @event.Partition,
                Offset = @event.Offset,
                Timestamp = @event.Timestamp,
                Headers = new(@event.Headers),
                ContentType = @event.ContentType
            };

            // Add AI predictions as headers
            enriched.Headers["ai-confidence"] = "0.95";
            enriched.Headers["ai-category"] = "important";
            enriched.Headers["ai-sentiment"] = "positive";

            Logger.LogInformation(
                "Enriched event {Id} with AI predictions",
                @event.Id);

            return enriched;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to enrich event {Id}", @event.Id);
            return null;
        }
    }
}

/// <summary>
/// Stream join processor - correlates events across streams
/// </summary>
public class StreamJoinProcessor : StreamProcessor
{
    private readonly Dictionary<string, StreamEvent> _leftBuffer = new();
    private readonly Dictionary<string, StreamEvent> _rightBuffer = new();
    private readonly TimeSpan _joinWindow = TimeSpan.FromSeconds(30);

    public StreamJoinProcessor(ILogger<StreamJoinProcessor> logger)
        : base(logger)
    {
    }

    /// <summary>
    /// Join events from left and right streams
    /// </summary>
    public override async Task<StreamEvent?> ProcessAsync(StreamEvent @event)
    {
        // Simplified join logic
        var key = @event.Key ?? "default";

        if (@event.Topic.Contains("left"))
        {
            _leftBuffer[key] = @event;
        }
        else
        {
            _rightBuffer[key] = @event;
        }

        // Check if both sides have matching key within time window
        if (_leftBuffer.ContainsKey(key) && _rightBuffer.ContainsKey(key))
        {
            var leftEvent = _leftBuffer[key];
            var rightEvent = _rightBuffer[key];

            if (Math.Abs((rightEvent.Timestamp - leftEvent.Timestamp).TotalSeconds) <= _joinWindow.TotalSeconds)
            {
                var joined = new StreamEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    Topic = "joined-stream",
                    Key = key,
                    Value = $"{{\"left\": {leftEvent.Value}, \"right\": {rightEvent.Value}}}",
                    Headers = new(leftEvent.Headers)
                };

                Logger.LogInformation(
                    "Joined events for key {Key}",
                    key);

                return joined;
            }
        }

        return null;
    }
}

/// <summary>
/// Stream aggregation processor - groups and aggregates events
/// </summary>
public class StreamAggregationProcessor : StreamProcessor
{
    private readonly Dictionary<string, List<StreamEvent>> _groups = new();
    private readonly int _windowSize;

    public StreamAggregationProcessor(int windowSize, ILogger<StreamAggregationProcessor> logger)
        : base(logger)
    {
        _windowSize = windowSize;
    }

    /// <summary>
    /// Aggregate events in tumbling window
    /// </summary>
    public override async Task<StreamEvent?> ProcessAsync(StreamEvent @event)
    {
        var key = @event.Key ?? "default";

        if (!_groups.ContainsKey(key))
        {
            _groups[key] = new();
        }

        _groups[key].Add(@event);

        // Emit aggregation when window is full
        if (_groups[key].Count >= _windowSize)
        {
            var aggregated = new StreamEvent
            {
                Id = Guid.NewGuid().ToString(),
                Topic = "aggregated-stream",
                Key = key,
                Value = JsonSerializer.Serialize(new
                {
                    count = _groups[key].Count,
                    events = _groups[key].Select(e => e.Value)
                }),
                Headers = new()
                {
                    ["aggregation-type"] = "tumbling-window",
                    ["window-size"] = _windowSize.ToString()
                }
            };

            _groups[key].Clear();

            Logger.LogInformation(
                "Aggregated {Count} events for key {Key}",
                _windowSize,
                key);

            return aggregated;
        }

        return null;
    }
}

/// <summary>
/// Stream topology - defines processing pipeline
/// </summary>
public class StreamTopology
{
    private readonly List<(string name, StreamProcessor processor)> _processors = new();
    private readonly ILogger<StreamTopology> _logger;

    public StreamTopology(ILogger<StreamTopology> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Add processor to topology
    /// </summary>
    public StreamTopology AddProcessor(string name, StreamProcessor processor)
    {
        _processors.Add((name, processor));

        _logger.LogInformation(
            "Added processor {Name} to topology",
            name);

        return this;
    }

    /// <summary>
    /// Process event through topology
    /// </summary>
    public async Task<StreamEvent?> ProcessAsync(StreamEvent @event)
    {
        var current = @event;

        foreach (var (name, processor) in _processors)
        {
            if (current == null)
                break;

            current = await processor.ProcessAsync(current);

            if (current != null)
            {
                _logger.LogDebug(
                    "Event {Id} processed by {Processor}",
                    @event.Id,
                    name);
            }
        }

        return current;
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class EventStreamingExtensions
{
    public static IServiceCollection AddEventStreaming(this IServiceCollection services)
    {
        services.AddSingleton<AiEnrichmentProcessor>();
        services.AddSingleton<StreamJoinProcessor>();
        return services;
    }
}
