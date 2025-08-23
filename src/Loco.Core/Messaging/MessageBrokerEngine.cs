using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.IO;
using System.Diagnostics;

namespace Loco.Core.Messaging;

/// <summary>
/// Enterprise message broker with pub/sub, queuing, and streaming capabilities
/// Implements reliable messaging patterns with persistence and replay
/// </summary>
public sealed class MessageBrokerEngine : IDisposable
{
    private readonly ILogger<MessageBrokerEngine> _logger;
    private readonly ConcurrentDictionary<string, Topic> _topics;
    private readonly ConcurrentDictionary<string, ConsumerGroup> _consumerGroups;
    private readonly ConcurrentDictionary<string, IMessageStore> _messageStores;
    private readonly MessageRouter _router;
    private readonly DeadLetterQueue _deadLetterQueue;
    private readonly MetricsCollector _metrics;
    private readonly BrokerConfiguration _config;
    private bool _disposed;

    // Background services
    private readonly Timer _cleanupTimer;
    private readonly Timer _metricsTimer;
    private readonly CancellationTokenSource _shutdownCts;

    public MessageBrokerEngine(BrokerConfiguration config = null, ILogger<MessageBrokerEngine> logger = null)
    {
        _logger = logger;
        _config = config ?? BrokerConfiguration.Default();
        _topics = new ConcurrentDictionary<string, Topic>();
        _consumerGroups = new ConcurrentDictionary<string, ConsumerGroup>();
        _messageStores = new ConcurrentDictionary<string, IMessageStore>();
        _router = new MessageRouter();
        _deadLetterQueue = new DeadLetterQueue(_config.DeadLetterRetention);
        _metrics = new MetricsCollector();
        _shutdownCts = new CancellationTokenSource();
        
        // Initialize timers
        _cleanupTimer = new Timer(CleanupCallback, null, 
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        _metricsTimer = new Timer(MetricsCallback, null,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        
        InitializeDefaultTopics();
    }

    private void InitializeDefaultTopics()
    {
        // Create system topics
        CreateTopic("_system.events", new TopicOptions { Partitions = 1, ReplicationFactor = 1 });
        CreateTopic("_system.metrics", new TopicOptions { Partitions = 1, ReplicationFactor = 1 });
        CreateTopic("_dead.letter", new TopicOptions { Partitions = 1, ReplicationFactor = 1 });
    }

    /// <summary>
    /// Create a new topic
    /// </summary>
    public Topic CreateTopic(string topicName, TopicOptions options = null)
    {
        options ??= new TopicOptions();
        
        var topic = _topics.GetOrAdd(topicName, name =>
        {
            var newTopic = new Topic
            {
                Name = name,
                Partitions = CreatePartitions(options.Partitions),
                Options = options,
                CreatedAt = DateTime.UtcNow
            };
            
            // Initialize message store if persistence is enabled
            if (options.EnablePersistence)
            {
                _messageStores[name] = CreateMessageStore(name, options);
            }
            
            _logger?.LogInformation("Created topic {TopicName} with {Partitions} partitions", 
                name, options.Partitions);
            
            return newTopic;
        });
        
        return topic;
    }

    /// <summary>
    /// Publish message to topic
    /// </summary>
    public async Task<PublishResult> PublishAsync<T>(
        string topicName,
        T message,
        PublishOptions options = null)
    {
        options ??= new PublishOptions();
        
        var topic = GetOrCreateTopic(topicName);
        var messageEnvelope = new MessageEnvelope
        {
            Id = options.MessageId ?? Guid.NewGuid().ToString(),
            Topic = topicName,
            Payload = JsonSerializer.Serialize(message),
            Headers = options.Headers ?? new Dictionary<string, string>(),
            Timestamp = DateTime.UtcNow,
            Key = options.Key
        };
        
        // Determine partition
        var partitionId = DeterminePartition(topic, messageEnvelope);
        var partition = topic.Partitions[partitionId];
        
        try
        {
            // Apply message transformations
            if (_router.HasTransformations(topicName))
            {
                messageEnvelope = await _router.TransformMessageAsync(messageEnvelope);
            }
            
            // Write to partition
            await partition.WriteAsync(messageEnvelope);
            
            // Persist if enabled
            if (_messageStores.TryGetValue(topicName, out var store))
            {
                await store.AppendAsync(messageEnvelope);
            }
            
            // Notify subscribers
            await NotifySubscribersAsync(topicName, partitionId, messageEnvelope);
            
            _metrics.RecordPublish(topicName);
            
            return new PublishResult
            {
                Success = true,
                MessageId = messageEnvelope.Id,
                Partition = partitionId,
                Offset = partition.GetCurrentOffset(),
                Timestamp = messageEnvelope.Timestamp
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to publish message to {Topic}", topicName);
            
            if (options.SendToDeadLetterOnFailure)
            {
                await _deadLetterQueue.EnqueueAsync(messageEnvelope, ex.Message);
            }
            
            return new PublishResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Subscribe to topic with consumer group
    /// </summary>
    public async Task<IMessageSubscription> SubscribeAsync<T>(
        string topicName,
        string consumerGroup,
        Func<T, MessageContext, Task> handler,
        SubscriptionOptions options = null)
    {
        options ??= new SubscriptionOptions();
        
        var topic = GetOrCreateTopic(topicName);
        var group = GetOrCreateConsumerGroup(consumerGroup);
        
        var subscription = new MessageSubscription<T>
        {
            Id = Guid.NewGuid().ToString(),
            TopicName = topicName,
            ConsumerGroup = consumerGroup,
            Handler = handler,
            Options = options,
            CancellationTokenSource = new CancellationTokenSource()
        };
        
        // Start consumption based on mode
        if (options.Mode == ConsumptionMode.Push)
        {
            _ = Task.Run(() => StartPushConsumptionAsync(subscription), subscription.CancellationTokenSource.Token);
        }
        
        group.AddSubscription(subscription);
        _logger?.LogInformation("Created subscription {SubscriptionId} for topic {Topic} in group {Group}",
            subscription.Id, topicName, consumerGroup);
        
        return subscription;
    }

    /// <summary>
    /// Pull messages from topic
    /// </summary>
    public async Task<IEnumerable<Message<T>>> PullAsync<T>(
        string topicName,
        string consumerGroup,
        PullOptions options = null)
    {
        options ??= new PullOptions();
        
        var topic = GetOrCreateTopic(topicName);
        var group = GetOrCreateConsumerGroup(consumerGroup);
        var messages = new List<Message<T>>();
        
        // Get assigned partitions for this consumer
        var assignedPartitions = group.GetAssignedPartitions(topic);
        
        foreach (var partitionId in assignedPartitions)
        {
            var partition = topic.Partitions[partitionId];
            var offset = group.GetOffset(topicName, partitionId);
            
            // Read messages from partition
            var envelopes = await partition.ReadAsync(offset, options.MaxMessages);
            
            foreach (var envelope in envelopes)
            {
                try
                {
                    var message = new Message<T>
                    {
                        Value = JsonSerializer.Deserialize<T>(envelope.Payload),
                        Key = envelope.Key,
                        Headers = envelope.Headers,
                        Topic = topicName,
                        Partition = partitionId,
                        Offset = envelope.Offset,
                        Timestamp = envelope.Timestamp
                    };
                    
                    messages.Add(message);
                    
                    // Update offset if auto-commit is enabled
                    if (options.AutoCommit)
                    {
                        await group.CommitOffsetAsync(topicName, partitionId, envelope.Offset + 1);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to deserialize message");
                    
                    if (options.SendToDeadLetterOnError)
                    {
                        await _deadLetterQueue.EnqueueAsync(envelope, ex.Message);
                    }
                }
            }
        }
        
        _metrics.RecordPull(topicName, messages.Count);
        return messages;
    }

    /// <summary>
    /// Create message stream for real-time processing
    /// </summary>
    public IAsyncEnumerable<Message<T>> CreateStream<T>(
        string topicName,
        StreamOptions options = null)
    {
        options ??= new StreamOptions();
        var topic = GetOrCreateTopic(topicName);
        
        return CreateStreamInternal<T>(topic, options);
    }

    private async IAsyncEnumerable<Message<T>> CreateStreamInternal<T>(
        Topic topic,
        StreamOptions options)
    {
        var startOffset = options.StartOffset;
        var cancellationToken = options.CancellationToken;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var partition in topic.Partitions)
            {
                var envelopes = await partition.ReadAsync(startOffset, options.BatchSize);
                
                foreach (var envelope in envelopes)
                {
                    Message<T> message = null;
                    
                    try
                    {
                        message = new Message<T>
                        {
                            Value = JsonSerializer.Deserialize<T>(envelope.Payload),
                            Key = envelope.Key,
                            Headers = envelope.Headers,
                            Topic = topic.Name,
                            Partition = partition.Id,
                            Offset = envelope.Offset,
                            Timestamp = envelope.Timestamp
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to deserialize message in stream");
                        
                        if (options.SkipDeserializationErrors)
                            continue;
                        else
                            throw;
                    }
                    
                    yield return message;
                    startOffset = envelope.Offset + 1;
                }
            }
            
            // Wait before next poll if no messages
            if (!envelopes.Any())
            {
                await Task.Delay(options.PollInterval, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Replay messages from a specific time or offset
    /// </summary>
    public async Task<ReplayResult> ReplayAsync(
        string topicName,
        ReplayOptions options)
    {
        if (!_messageStores.TryGetValue(topicName, out var store))
        {
            throw new InvalidOperationException($"Topic {topicName} does not have persistence enabled");
        }
        
        var messages = await store.ReadRangeAsync(options.StartTime, options.EndTime);
        var replayedCount = 0;
        var errors = new List<string>();
        
        foreach (var message in messages)
        {
            try
            {
                if (options.TargetTopic != null)
                {
                    // Replay to different topic
                    await PublishAsync(options.TargetTopic, message, new PublishOptions
                    {
                        Headers = new Dictionary<string, string>
                        {
                            ["replayed-from"] = topicName,
                            ["replay-time"] = DateTime.UtcNow.ToString("O")
                        }
                    });
                }
                else
                {
                    // Replay to same topic
                    await PublishAsync(topicName, message);
                }
                
                replayedCount++;
                
                if (options.RateLimitPerSecond > 0)
                {
                    await Task.Delay(1000 / options.RateLimitPerSecond);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to replay message {message.Id}: {ex.Message}");
            }
        }
        
        return new ReplayResult
        {
            MessagesReplayed = replayedCount,
            Errors = errors,
            Duration = DateTime.UtcNow - options.StartTime
        };
    }

    /// <summary>
    /// Configure message routing rules
    /// </summary>
    public void ConfigureRouting(Action<MessageRouter> configure)
    {
        configure(_router);
    }

    /// <summary>
    /// Get broker metrics
    /// </summary>
    public BrokerMetrics GetMetrics()
    {
        return new BrokerMetrics
        {
            TopicCount = _topics.Count,
            ConsumerGroupCount = _consumerGroups.Count,
            TotalMessagesPublished = _metrics.TotalPublished,
            TotalMessagesPulled = _metrics.TotalPulled,
            MessagesPerSecond = _metrics.GetMessagesPerSecond(),
            ActiveSubscriptions = _consumerGroups.Values.Sum(g => g.GetActiveSubscriptionCount()),
            DeadLetterQueueSize = _deadLetterQueue.GetSize()
        };
    }

    /// <summary>
    /// Delete topic
    /// </summary>
    public async Task<bool> DeleteTopicAsync(string topicName)
    {
        if (_topics.TryRemove(topicName, out var topic))
        {
            // Clean up partitions
            foreach (var partition in topic.Partitions)
            {
                partition.Dispose();
            }
            
            // Clean up message store
            if (_messageStores.TryRemove(topicName, out var store))
            {
                await store.DeleteAsync();
                store.Dispose();
            }
            
            _logger?.LogInformation("Deleted topic {TopicName}", topicName);
            return true;
        }
        
        return false;
    }

    // Helper methods
    private Topic GetOrCreateTopic(string topicName)
    {
        return _topics.GetOrAdd(topicName, name => 
            CreateTopic(name, new TopicOptions()));
    }

    private ConsumerGroup GetOrCreateConsumerGroup(string groupId)
    {
        return _consumerGroups.GetOrAdd(groupId, id => 
            new ConsumerGroup { Id = id });
    }

    private List<Partition> CreatePartitions(int count)
    {
        var partitions = new List<Partition>();
        for (int i = 0; i < count; i++)
        {
            partitions.Add(new Partition { Id = i });
        }
        return partitions;
    }

    private IMessageStore CreateMessageStore(string topicName, TopicOptions options)
    {
        return options.StorageType switch
        {
            StorageType.Memory => new InMemoryMessageStore(),
            StorageType.File => new FileMessageStore(Path.Combine(_config.DataPath, topicName)),
            _ => new InMemoryMessageStore()
        };
    }

    private int DeterminePartition(Topic topic, MessageEnvelope message)
    {
        if (message.Key != null)
        {
            // Use key-based partitioning
            var hash = message.Key.GetHashCode();
            return Math.Abs(hash) % topic.Partitions.Count;
        }
        else
        {
            // Round-robin partitioning
            return (int)(message.Timestamp.Ticks % topic.Partitions.Count);
        }
    }

    private async Task NotifySubscribersAsync(string topicName, int partitionId, MessageEnvelope message)
    {
        foreach (var group in _consumerGroups.Values)
        {
            await group.NotifySubscribersAsync(topicName, partitionId, message);
        }
    }

    private async Task StartPushConsumptionAsync<T>(MessageSubscription<T> subscription)
    {
        var topic = _topics[subscription.TopicName];
        var group = _consumerGroups[subscription.ConsumerGroup];
        
        while (!subscription.CancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                var messages = await PullAsync<T>(
                    subscription.TopicName,
                    subscription.ConsumerGroup,
                    new PullOptions { MaxMessages = subscription.Options.BatchSize });
                
                foreach (var message in messages)
                {
                    var context = new MessageContext
                    {
                        Topic = subscription.TopicName,
                        ConsumerGroup = subscription.ConsumerGroup,
                        Partition = message.Partition,
                        Offset = message.Offset
                    };
                    
                    await subscription.Handler(message.Value, context);
                }
                
                if (!messages.Any())
                {
                    await Task.Delay(subscription.Options.PollInterval);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in push consumption for subscription {Id}", subscription.Id);
                await Task.Delay(TimeSpan.FromSeconds(5)); // Back off on error
            }
        }
    }

    private void CleanupCallback(object state)
    {
        try
        {
            // Clean up expired messages
            _deadLetterQueue.CleanupExpired();
            
            // Clean up inactive consumer groups
            var inactiveGroups = _consumerGroups
                .Where(kvp => kvp.Value.GetLastActivityTime() < DateTime.UtcNow.AddMinutes(-30))
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var groupId in inactiveGroups)
            {
                if (_consumerGroups.TryRemove(groupId, out var group))
                {
                    group.Dispose();
                    _logger?.LogInformation("Removed inactive consumer group {GroupId}", groupId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during cleanup");
        }
    }

    private void MetricsCallback(object state)
    {
        _metrics.UpdateMetrics();
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _shutdownCts?.Cancel();
        _cleanupTimer?.Dispose();
        _metricsTimer?.Dispose();
        
        foreach (var topic in _topics.Values)
        {
            foreach (var partition in topic.Partitions)
            {
                partition.Dispose();
            }
        }
        
        foreach (var store in _messageStores.Values)
        {
            store.Dispose();
        }
        
        foreach (var group in _consumerGroups.Values)
        {
            group.Dispose();
        }
        
        _deadLetterQueue?.Dispose();
        
        _disposed = true;
    }
}

// Supporting classes
public class BrokerConfiguration
{
    public string DataPath { get; set; } = "./data";
    public TimeSpan DeadLetterRetention { get; set; } = TimeSpan.FromDays(7);
    public int DefaultPartitions { get; set; } = 4;
    public int DefaultReplicationFactor { get; set; } = 1;
    
    public static BrokerConfiguration Default() => new();
}

public class Topic
{
    public string Name { get; set; }
    public List<Partition> Partitions { get; set; }
    public TopicOptions Options { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TopicOptions
{
    public int Partitions { get; set; } = 4;
    public int ReplicationFactor { get; set; } = 1;
    public bool EnablePersistence { get; set; } = false;
    public StorageType StorageType { get; set; } = StorageType.Memory;
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);
}

public enum StorageType
{
    Memory,
    File,
    Database
}

public class Partition : IDisposable
{
    public int Id { get; set; }
    private readonly Channel<MessageEnvelope> _channel;
    private long _currentOffset;
    private readonly List<MessageEnvelope> _messages;
    
    public Partition()
    {
        _channel = Channel.CreateUnbounded<MessageEnvelope>();
        _messages = new List<MessageEnvelope>();
    }
    
    public async Task WriteAsync(MessageEnvelope message)
    {
        message.Offset = Interlocked.Increment(ref _currentOffset);
        _messages.Add(message);
        await _channel.Writer.WriteAsync(message);
    }
    
    public async Task<IEnumerable<MessageEnvelope>> ReadAsync(long offset, int maxMessages)
    {
        return _messages.Where(m => m.Offset >= offset).Take(maxMessages);
    }
    
    public long GetCurrentOffset() => _currentOffset;
    
    public void Dispose()
    {
        _channel.Writer.TryComplete();
    }
}

public class MessageEnvelope
{
    public string Id { get; set; }
    public string Topic { get; set; }
    public string Payload { get; set; }
    public string Key { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public DateTime Timestamp { get; set; }
    public long Offset { get; set; }
}

public class Message<T>
{
    public T Value { get; set; }
    public string Key { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public string Topic { get; set; }
    public int Partition { get; set; }
    public long Offset { get; set; }
    public DateTime Timestamp { get; set; }
}

public class PublishOptions
{
    public string MessageId { get; set; }
    public string Key { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public bool SendToDeadLetterOnFailure { get; set; } = true;
}

public class PublishResult
{
    public bool Success { get; set; }
    public string MessageId { get; set; }
    public int Partition { get; set; }
    public long Offset { get; set; }
    public DateTime Timestamp { get; set; }
    public string Error { get; set; }
}

public interface IMessageSubscription
{
    string Id { get; }
    Task UnsubscribeAsync();
}

public class MessageSubscription<T> : IMessageSubscription
{
    public string Id { get; set; }
    public string TopicName { get; set; }
    public string ConsumerGroup { get; set; }
    public Func<T, MessageContext, Task> Handler { get; set; }
    public SubscriptionOptions Options { get; set; }
    public CancellationTokenSource CancellationTokenSource { get; set; }
    
    public async Task UnsubscribeAsync()
    {
        CancellationTokenSource?.Cancel();
        await Task.CompletedTask;
    }
}

public class SubscriptionOptions
{
    public ConsumptionMode Mode { get; set; } = ConsumptionMode.Push;
    public int BatchSize { get; set; } = 10;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
}

public enum ConsumptionMode
{
    Push,
    Pull
}

public class MessageContext
{
    public string Topic { get; set; }
    public string ConsumerGroup { get; set; }
    public int Partition { get; set; }
    public long Offset { get; set; }
}

public class PullOptions
{
    public int MaxMessages { get; set; } = 10;
    public bool AutoCommit { get; set; } = true;
    public bool SendToDeadLetterOnError { get; set; } = true;
}

public class StreamOptions
{
    public long StartOffset { get; set; } = 0;
    public int BatchSize { get; set; } = 100;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    public bool SkipDeserializationErrors { get; set; } = true;
    public CancellationToken CancellationToken { get; set; }
}

public class ReplayOptions
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string TargetTopic { get; set; }
    public int RateLimitPerSecond { get; set; }
}

public class ReplayResult
{
    public int MessagesReplayed { get; set; }
    public List<string> Errors { get; set; }
    public TimeSpan Duration { get; set; }
}

// Consumer groups
public class ConsumerGroup : IDisposable
{
    public string Id { get; set; }
    private readonly ConcurrentDictionary<string, List<IMessageSubscription>> _subscriptions = new();
    private readonly ConcurrentDictionary<string, long> _offsets = new();
    private DateTime _lastActivity = DateTime.UtcNow;
    
    public void AddSubscription(IMessageSubscription subscription)
    {
        _subscriptions.AddOrUpdate(subscription.Id,
            new List<IMessageSubscription> { subscription },
            (k, list) => { list.Add(subscription); return list; });
        _lastActivity = DateTime.UtcNow;
    }
    
    public List<int> GetAssignedPartitions(Topic topic)
    {
        // Simplified partition assignment
        return Enumerable.Range(0, topic.Partitions.Count).ToList();
    }
    
    public long GetOffset(string topic, int partition)
    {
        var key = $"{topic}-{partition}";
        return _offsets.GetOrAdd(key, 0);
    }
    
    public async Task CommitOffsetAsync(string topic, int partition, long offset)
    {
        var key = $"{topic}-{partition}";
        _offsets[key] = offset;
        _lastActivity = DateTime.UtcNow;
        await Task.CompletedTask;
    }
    
    public async Task NotifySubscribersAsync(string topic, int partition, MessageEnvelope message)
    {
        foreach (var subscriptions in _subscriptions.Values)
        {
            foreach (var subscription in subscriptions)
            {
                // Notify relevant subscriptions
            }
        }
        await Task.CompletedTask;
    }
    
    public int GetActiveSubscriptionCount()
    {
        return _subscriptions.Values.Sum(list => list.Count);
    }
    
    public DateTime GetLastActivityTime() => _lastActivity;
    
    public void Dispose()
    {
        foreach (var subscriptions in _subscriptions.Values)
        {
            foreach (var subscription in subscriptions)
            {
                _ = subscription.UnsubscribeAsync();
            }
        }
    }
}

// Message routing
public class MessageRouter
{
    private readonly Dictionary<string, List<Func<MessageEnvelope, Task<MessageEnvelope>>>> _transformations = new();
    
    public void AddTransformation(string topic, Func<MessageEnvelope, Task<MessageEnvelope>> transformation)
    {
        if (!_transformations.ContainsKey(topic))
        {
            _transformations[topic] = new List<Func<MessageEnvelope, Task<MessageEnvelope>>>();
        }
        _transformations[topic].Add(transformation);
    }
    
    public bool HasTransformations(string topic) => _transformations.ContainsKey(topic);
    
    public async Task<MessageEnvelope> TransformMessageAsync(MessageEnvelope message)
    {
        if (_transformations.TryGetValue(message.Topic, out var transformations))
        {
            foreach (var transformation in transformations)
            {
                message = await transformation(message);
            }
        }
        return message;
    }
}

// Dead letter queue
public class DeadLetterQueue : IDisposable
{
    private readonly ConcurrentQueue<DeadLetterMessage> _queue = new();
    private readonly TimeSpan _retention;
    
    public DeadLetterQueue(TimeSpan retention)
    {
        _retention = retention;
    }
    
    public async Task EnqueueAsync(MessageEnvelope message, string reason)
    {
        _queue.Enqueue(new DeadLetterMessage
        {
            Message = message,
            Reason = reason,
            EnqueuedAt = DateTime.UtcNow
        });
        await Task.CompletedTask;
    }
    
    public int GetSize() => _queue.Count;
    
    public void CleanupExpired()
    {
        var cutoff = DateTime.UtcNow - _retention;
        while (_queue.TryPeek(out var message) && message.EnqueuedAt < cutoff)
        {
            _queue.TryDequeue(out _);
        }
    }
    
    public void Dispose()
    {
        // Cleanup
    }
}

public class DeadLetterMessage
{
    public MessageEnvelope Message { get; set; }
    public string Reason { get; set; }
    public DateTime EnqueuedAt { get; set; }
}

// Metrics
public class MetricsCollector
{
    public long TotalPublished;
    public long TotalPulled;
    private DateTime _lastUpdate = DateTime.UtcNow;
    private long _lastPublished;
    
    public void RecordPublish(string topic)
    {
        Interlocked.Increment(ref TotalPublished);
    }
    
    public void RecordPull(string topic, int count)
    {
        Interlocked.Add(ref TotalPulled, count);
    }
    
    public double GetMessagesPerSecond()
    {
        var elapsed = (DateTime.UtcNow - _lastUpdate).TotalSeconds;
        if (elapsed <= 0) return 0;
        
        var published = TotalPublished - _lastPublished;
        return published / elapsed;
    }
    
    public void UpdateMetrics()
    {
        _lastPublished = TotalPublished;
        _lastUpdate = DateTime.UtcNow;
    }
}

public class BrokerMetrics
{
    public int TopicCount { get; set; }
    public int ConsumerGroupCount { get; set; }
    public long TotalMessagesPublished { get; set; }
    public long TotalMessagesPulled { get; set; }
    public double MessagesPerSecond { get; set; }
    public int ActiveSubscriptions { get; set; }
    public int DeadLetterQueueSize { get; set; }
}

// Message stores
public interface IMessageStore : IDisposable
{
    Task AppendAsync(MessageEnvelope message);
    Task<IEnumerable<MessageEnvelope>> ReadRangeAsync(DateTime start, DateTime end);
    Task DeleteAsync();
}

public class InMemoryMessageStore : IMessageStore
{
    private readonly List<MessageEnvelope> _messages = new();
    
    public async Task AppendAsync(MessageEnvelope message)
    {
        _messages.Add(message);
        await Task.CompletedTask;
    }
    
    public async Task<IEnumerable<MessageEnvelope>> ReadRangeAsync(DateTime start, DateTime end)
    {
        return await Task.FromResult(_messages
            .Where(m => m.Timestamp >= start && m.Timestamp <= end));
    }
    
    public async Task DeleteAsync()
    {
        _messages.Clear();
        await Task.CompletedTask;
    }
    
    public void Dispose()
    {
        _messages.Clear();
    }
}

public class FileMessageStore : IMessageStore
{
    private readonly string _path;
    
    public FileMessageStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(path);
    }
    
    public async Task AppendAsync(MessageEnvelope message)
    {
        var file = Path.Combine(_path, $"{message.Timestamp:yyyyMMdd}.log");
        var json = JsonSerializer.Serialize(message);
        await File.AppendAllTextAsync(file, json + Environment.NewLine);
    }
    
    public async Task<IEnumerable<MessageEnvelope>> ReadRangeAsync(DateTime start, DateTime end)
    {
        var messages = new List<MessageEnvelope>();
        // Simplified file reading
        return messages;
    }
    
    public async Task DeleteAsync()
    {
        if (Directory.Exists(_path))
        {
            Directory.Delete(_path, true);
        }
        await Task.CompletedTask;
    }
    
    public void Dispose()
    {
        // Cleanup
    }
}