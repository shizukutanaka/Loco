#nullable enable

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.EventStreaming;

/// <summary>
/// Event Streaming Architecture Patterns
/// Kafka, Pulsar, RabbitMQ, stream processing
/// </summary>

public class StreamMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("partition")]
    public int Partition { get; set; }

    [JsonPropertyName("offset")]
    public long Offset { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = new();
}

public class TopicConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("partitions")]
    public int Partitions { get; set; } = 3;

    [JsonPropertyName("replicationFactor")]
    public int ReplicationFactor { get; set; } = 3;

    [JsonPropertyName("retentionDays")]
    public int RetentionDays { get; set; } = 7;

    [JsonPropertyName("compressionType")]
    public string CompressionType { get; set; } = "snappy";

    [JsonPropertyName("cleanupPolicy")]
    public string CleanupPolicy { get; set; } = "delete";
}

public class ConsumerGroupConfig
{
    [JsonPropertyName("groupId")]
    public string GroupId { get; set; } = string.Empty;

    [JsonPropertyName("topics")]
    public List<string> Topics { get; set; } = new();

    [JsonPropertyName("processingGuarantee")]
    public string ProcessingGuarantee { get; set; } = "at-least-once";
}

public class StreamConsumer
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("groupId")]
    public string GroupId { get; set; } = string.Empty;

    [JsonPropertyName("topics")]
    public List<string> Topics { get; set; } = new();

    [JsonPropertyName("messagesProcessed")]
    public long MessagesProcessed { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = "active";
}

public class StreamProducer
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonPropertyName("acks")]
    public string Acks { get; set; } = "all";

    [JsonPropertyName("messagesSent")]
    public long MessagesSent { get; set; }
}

public class StreamProcessingTopology
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sourceTopics")]
    public List<string> SourceTopics { get; set; } = new();

    [JsonPropertyName("sinkTopics")]
    public List<string> SinkTopics { get; set; } = new();

    [JsonPropertyName("parallelism")]
    public int Parallelism { get; set; } = 1;
}

public class EventStreamingPlatform
{
    private readonly ConcurrentDictionary<string, TopicConfig> _topics = new();
    private readonly ConcurrentDictionary<string, StreamConsumer> _consumers = new();
    private readonly ConcurrentDictionary<string, StreamProducer> _producers = new();
    private readonly ConcurrentDictionary<string, StreamMessage> _messages = new();
    private readonly List<StreamProcessingTopology> _topologies = new();
    private readonly ILogger<EventStreamingPlatform> _logger;
    private long _globalOffset = 0;

    public EventStreamingPlatform(ILogger<EventStreamingPlatform> logger)
    {
        _logger = logger;
    }

    public async Task CreateTopicAsync(TopicConfig config)
    {
        _topics[config.Name] = config;
        _logger.LogInformation("Created topic: {Name} ({Partitions} partitions)", config.Name, config.Partitions);
    }

    public async Task RegisterProducerAsync(StreamProducer producer)
    {
        _producers[producer.Id] = producer;
        _logger.LogInformation("Registered producer: {Name} → {Topic}", producer.Name, producer.Topic);
    }

    public async Task RegisterConsumerGroupAsync(ConsumerGroupConfig config)
    {
        var consumer = new StreamConsumer
        {
            GroupId = config.GroupId,
            Topics = config.Topics
        };
        _consumers[consumer.Id] = consumer;
        _logger.LogInformation("Registered consumer group: {GroupId}", config.GroupId);
    }

    public async Task<StreamMessage> PublishMessageAsync(string topic, string key, string value)
    {
        if (!_topics.TryGetValue(topic, out _))
            throw new InvalidOperationException($"Topic '{topic}' does not exist");

        var message = new StreamMessage
        {
            Topic = topic,
            Key = key,
            Value = value,
            Partition = Math.Abs(key.GetHashCode()) % 3,
            Offset = _globalOffset++
        };

        _messages[message.Id] = message;
        return message;
    }

    public async Task CreateProcessingTopologyAsync(StreamProcessingTopology topology)
    {
        _topologies.Add(topology);
        _logger.LogInformation("Created processing topology: {Name}", topology.Name);
    }

    public Dictionary<string, object> GetStats()
    {
        return new()
        {
            ["topics"] = _topics.Count,
            ["producers"] = _producers.Count,
            ["consumerGroups"] = _consumers.Values.DistinctBy(c => c.GroupId).Count(),
            ["totalMessages"] = _messages.Count,
            ["topologies"] = _topologies.Count
        };
    }
}

public static class EventStreamingExtensions
{
    public static IServiceCollection AddEventStreaming(this IServiceCollection services)
    {
        services.AddSingleton<EventStreamingPlatform>();
        return services;
    }
}
