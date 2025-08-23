using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.CQRS.EventSourcing;

/// <summary>
/// Event store for persisting domain events
/// </summary>
public interface IEventStore
{
    Task<IEnumerable<IEvent>> GetEventsAsync(Guid aggregateId, int fromVersion = 0);
    Task<IEnumerable<IEvent>> GetAllEventsAsync(DateTime? from = null, DateTime? to = null);
    Task SaveEventsAsync(Guid aggregateId, IEnumerable<IEvent> events, int expectedVersion);
    Task<EventStream> GetEventStreamAsync(Guid streamId);
    Task<IEnumerable<EventStream>> GetEventStreamsAsync(string category);
    IAsyncEnumerable<IEvent> SubscribeToEvents(string? category = null, CancellationToken cancellationToken = default);
    Task<Snapshot?> GetSnapshotAsync(Guid aggregateId);
    Task SaveSnapshotAsync(Snapshot snapshot);
}

/// <summary>
/// In-memory event store implementation
/// </summary>
public class InMemoryEventStore : IEventStore
{
    private readonly Dictionary<Guid, List<EventData>> _events = new();
    private readonly Dictionary<Guid, Snapshot> _snapshots = new();
    private readonly List<EventSubscription> _subscriptions = new();
    private readonly ILogger<InMemoryEventStore> _logger;
    private readonly object _lock = new();

    public InMemoryEventStore(ILogger<InMemoryEventStore> logger)
    {
        _logger = logger;
    }

    public Task<IEnumerable<IEvent>> GetEventsAsync(Guid aggregateId, int fromVersion = 0)
    {
        lock (_lock)
        {
            if (!_events.TryGetValue(aggregateId, out var events))
            {
                return Task.FromResult(Enumerable.Empty<IEvent>());
            }

            var result = events
                .Where(e => e.Version > fromVersion)
                .OrderBy(e => e.Version)
                .Select(e => e.Event);

            return Task.FromResult(result);
        }
    }

    public Task<IEnumerable<IEvent>> GetAllEventsAsync(DateTime? from = null, DateTime? to = null)
    {
        lock (_lock)
        {
            var allEvents = _events.Values
                .SelectMany(list => list)
                .Where(e => (!from.HasValue || e.Timestamp >= from.Value) &&
                           (!to.HasValue || e.Timestamp <= to.Value))
                .OrderBy(e => e.Timestamp)
                .Select(e => e.Event);

            return Task.FromResult(allEvents);
        }
    }

    public Task SaveEventsAsync(Guid aggregateId, IEnumerable<IEvent> events, int expectedVersion)
    {
        lock (_lock)
        {
            if (!_events.TryGetValue(aggregateId, out var aggregateEvents))
            {
                aggregateEvents = new List<EventData>();
                _events[aggregateId] = aggregateEvents;
            }

            var currentVersion = aggregateEvents.Count > 0 
                ? aggregateEvents.Max(e => e.Version) 
                : 0;

            if (currentVersion != expectedVersion)
            {
                throw new EventStoreConcurrencyException(
                    $"Expected version {expectedVersion} but current version is {currentVersion}");
            }

            var version = currentVersion;
            foreach (var @event in events)
            {
                version++;
                @event.Version = version;
                @event.Timestamp = DateTime.UtcNow;

                var eventData = new EventData
                {
                    AggregateId = aggregateId,
                    Event = @event,
                    Version = version,
                    Timestamp = @event.Timestamp
                };

                aggregateEvents.Add(eventData);
                
                // Notify subscribers
                NotifySubscribers(@event);
                
                _logger.LogDebug("Saved event {EventType} for aggregate {AggregateId} at version {Version}",
                    @event.GetType().Name, aggregateId, version);
            }
        }

        return Task.CompletedTask;
    }

    public Task<EventStream> GetEventStreamAsync(Guid streamId)
    {
        lock (_lock)
        {
            if (!_events.TryGetValue(streamId, out var events))
            {
                return Task.FromResult(new EventStream
                {
                    StreamId = streamId,
                    Events = Enumerable.Empty<IEvent>(),
                    Version = 0
                });
            }

            return Task.FromResult(new EventStream
            {
                StreamId = streamId,
                Events = events.Select(e => e.Event),
                Version = events.Max(e => e.Version)
            });
        }
    }

    public Task<IEnumerable<EventStream>> GetEventStreamsAsync(string category)
    {
        lock (_lock)
        {
            var streams = _events
                .Where(kvp => kvp.Value.Any(e => e.Event.Category == category))
                .Select(kvp => new EventStream
                {
                    StreamId = kvp.Key,
                    Events = kvp.Value.Select(e => e.Event),
                    Version = kvp.Value.Max(e => e.Version)
                });

            return Task.FromResult(streams);
        }
    }

    public async IAsyncEnumerable<IEvent> SubscribeToEvents(
        string? category = null, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var subscription = new EventSubscription
        {
            Id = Guid.NewGuid(),
            Category = category,
            Channel = System.Threading.Channels.Channel.CreateUnbounded<IEvent>()
        };

        lock (_lock)
        {
            _subscriptions.Add(subscription);
        }

        try
        {
            await foreach (var @event in subscription.Channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return @event;
            }
        }
        finally
        {
            lock (_lock)
            {
                _subscriptions.Remove(subscription);
            }
        }
    }

    public Task<Snapshot?> GetSnapshotAsync(Guid aggregateId)
    {
        lock (_lock)
        {
            _snapshots.TryGetValue(aggregateId, out var snapshot);
            return Task.FromResult(snapshot);
        }
    }

    public Task SaveSnapshotAsync(Snapshot snapshot)
    {
        lock (_lock)
        {
            _snapshots[snapshot.AggregateId] = snapshot;
            _logger.LogDebug("Saved snapshot for aggregate {AggregateId} at version {Version}",
                snapshot.AggregateId, snapshot.Version);
        }

        return Task.CompletedTask;
    }

    private void NotifySubscribers(IEvent @event)
    {
        foreach (var subscription in _subscriptions)
        {
            if (subscription.Category == null || subscription.Category == @event.Category)
            {
                subscription.Channel.Writer.TryWrite(@event);
            }
        }
    }

    private class EventData
    {
        public Guid AggregateId { get; set; }
        public IEvent Event { get; set; } = null!;
        public int Version { get; set; }
        public DateTime Timestamp { get; set; }
    }

    private class EventSubscription
    {
        public Guid Id { get; set; }
        public string? Category { get; set; }
        public System.Threading.Channels.Channel<IEvent> Channel { get; set; } = null!;
    }
}

/// <summary>
/// Base event interface
/// </summary>
public interface IEvent
{
    Guid EventId { get; set; }
    Guid AggregateId { get; set; }
    int Version { get; set; }
    DateTime Timestamp { get; set; }
    string UserId { get; set; }
    string Category { get; }
    Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Base event implementation
/// </summary>
public abstract class EventBase : IEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid AggregateId { get; set; }
    public int Version { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
    public abstract string Category { get; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Event stream
/// </summary>
public class EventStream
{
    public Guid StreamId { get; set; }
    public IEnumerable<IEvent> Events { get; set; } = Enumerable.Empty<IEvent>();
    public int Version { get; set; }
}

/// <summary>
/// Snapshot for aggregate state
/// </summary>
public class Snapshot
{
    public Guid AggregateId { get; set; }
    public int Version { get; set; }
    public string Data { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Event store concurrency exception
/// </summary>
public class EventStoreConcurrencyException : Exception
{
    public EventStoreConcurrencyException(string message) : base(message) { }
    public EventStoreConcurrencyException(string message, Exception innerException) 
        : base(message, innerException) { }
}
