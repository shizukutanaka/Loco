using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loco.CQRS.EventSourcing;
using Microsoft.Extensions.Logging;

namespace Loco.CQRS.Aggregates;

/// <summary>
/// Aggregate root interface
/// </summary>
public interface IAggregateRoot
{
    Guid Id { get; }
    int Version { get; }
    IEnumerable<IEvent> GetUncommittedEvents();
    void MarkEventsAsCommitted();
    void LoadFromHistory(IEnumerable<IEvent> history);
}

/// <summary>
/// Base aggregate root implementation
/// </summary>
public abstract class AggregateRoot : IAggregateRoot
{
    private readonly List<IEvent> _changes = new();
    
    public Guid Id { get; protected set; }
    public int Version { get; protected set; } = -1;

    public IEnumerable<IEvent> GetUncommittedEvents()
    {
        return _changes;
    }

    public void MarkEventsAsCommitted()
    {
        _changes.Clear();
    }

    public void LoadFromHistory(IEnumerable<IEvent> history)
    {
        foreach (var @event in history)
        {
            ApplyChange(@event, false);
        }
    }

    protected void ApplyChange(IEvent @event)
    {
        ApplyChange(@event, true);
    }

    private void ApplyChange(IEvent @event, bool isNew)
    {
        // Apply the event to the aggregate
        Apply(@event);
        
        // Update version
        Version = @event.Version;
        
        // Add to uncommitted changes if new
        if (isNew)
        {
            _changes.Add(@event);
        }
    }

    protected abstract void Apply(IEvent @event);
}

/// <summary>
/// Repository interface for aggregates
/// </summary>
public interface IRepository<T> where T : IAggregateRoot
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(T aggregate, int expectedVersion, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Event sourced repository implementation
/// </summary>
public class EventSourcedRepository<T> : IRepository<T> where T : IAggregateRoot, new()
{
    private readonly IEventStore _eventStore;
    private readonly ILogger<EventSourcedRepository<T>> _logger;
    private readonly ISnapshotStrategy _snapshotStrategy;

    public EventSourcedRepository(
        IEventStore eventStore,
        ILogger<EventSourcedRepository<T>> logger,
        ISnapshotStrategy? snapshotStrategy = null)
    {
        _eventStore = eventStore;
        _logger = logger;
        _snapshotStrategy = snapshotStrategy ?? new NoSnapshotStrategy();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading aggregate {AggregateType} with ID {AggregateId}", typeof(T).Name, id);

        var aggregate = new T();
        
        // Try to load from snapshot
        var snapshot = await _eventStore.GetSnapshotAsync(id);
        var fromVersion = 0;
        
        if (snapshot != null && _snapshotStrategy.ShouldUseSnapshot(snapshot))
        {
            aggregate = DeserializeSnapshot(snapshot);
            fromVersion = snapshot.Version;
            _logger.LogDebug("Loaded aggregate from snapshot at version {Version}", fromVersion);
        }

        // Load events after snapshot
        var events = await _eventStore.GetEventsAsync(id, fromVersion);
        var eventsList = events.ToList();
        
        if (!eventsList.Any() && snapshot == null)
        {
            _logger.LogDebug("Aggregate {AggregateId} not found", id);
            return default;
        }

        // Apply events to aggregate
        aggregate.LoadFromHistory(eventsList);
        
        _logger.LogDebug("Loaded aggregate {AggregateType} with ID {AggregateId} at version {Version}",
            typeof(T).Name, id, aggregate.Version);

        return aggregate;
    }

    public async Task SaveAsync(T aggregate, int expectedVersion, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Saving aggregate {AggregateType} with ID {AggregateId}",
            typeof(T).Name, aggregate.Id);

        var events = aggregate.GetUncommittedEvents().ToList();
        
        if (!events.Any())
        {
            _logger.LogDebug("No uncommitted events for aggregate {AggregateId}", aggregate.Id);
            return;
        }

        // Save events
        await _eventStore.SaveEventsAsync(aggregate.Id, events, expectedVersion);
        
        // Mark events as committed
        aggregate.MarkEventsAsCommitted();
        
        // Take snapshot if needed
        if (_snapshotStrategy.ShouldTakeSnapshot(aggregate))
        {
            var snapshot = CreateSnapshot(aggregate);
            await _eventStore.SaveSnapshotAsync(snapshot);
            _logger.LogDebug("Snapshot created for aggregate {AggregateId} at version {Version}",
                aggregate.Id, aggregate.Version);
        }

        _logger.LogInformation("Saved {EventCount} events for aggregate {AggregateType} with ID {AggregateId}",
            events.Count, typeof(T).Name, aggregate.Id);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var events = await _eventStore.GetEventsAsync(id, 0);
        return events.Any();
    }

    private Snapshot CreateSnapshot(T aggregate)
    {
        // In a real implementation, this would serialize the aggregate state
        return new Snapshot
        {
            AggregateId = aggregate.Id,
            Version = aggregate.Version,
            Data = System.Text.Json.JsonSerializer.Serialize(aggregate),
            Timestamp = DateTime.UtcNow
        };
    }

    private T DeserializeSnapshot(Snapshot snapshot)
    {
        // In a real implementation, this would deserialize the aggregate state
        return System.Text.Json.JsonSerializer.Deserialize<T>(snapshot.Data) ?? new T();
    }
}

/// <summary>
/// Snapshot strategy interface
/// </summary>
public interface ISnapshotStrategy
{
    bool ShouldTakeSnapshot(IAggregateRoot aggregate);
    bool ShouldUseSnapshot(Snapshot snapshot);
}

/// <summary>
/// No snapshot strategy
/// </summary>
public class NoSnapshotStrategy : ISnapshotStrategy
{
    public bool ShouldTakeSnapshot(IAggregateRoot aggregate) => false;
    public bool ShouldUseSnapshot(Snapshot snapshot) => false;
}

/// <summary>
/// Interval-based snapshot strategy
/// </summary>
public class IntervalSnapshotStrategy : ISnapshotStrategy
{
    private readonly int _snapshotInterval;
    private readonly TimeSpan _maxSnapshotAge;

    public IntervalSnapshotStrategy(int snapshotInterval = 10, TimeSpan? maxSnapshotAge = null)
    {
        _snapshotInterval = snapshotInterval;
        _maxSnapshotAge = maxSnapshotAge ?? TimeSpan.FromHours(1);
    }

    public bool ShouldTakeSnapshot(IAggregateRoot aggregate)
    {
        return aggregate.Version % _snapshotInterval == 0;
    }

    public bool ShouldUseSnapshot(Snapshot snapshot)
    {
        return DateTime.UtcNow - snapshot.Timestamp < _maxSnapshotAge;
    }
}

/// <summary>
/// Domain event dispatcher
/// </summary>
public interface IEventDispatcher
{
    Task DispatchAsync(IEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// Event dispatcher implementation
/// </summary>
public class EventDispatcher : IEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventDispatcher> _logger;

    public EventDispatcher(
        IServiceProvider serviceProvider,
        ILogger<EventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Dispatching event {EventType} for aggregate {AggregateId}",
            @event.GetType().Name, @event.AggregateId);

        // Get all handlers for this event type
        var handlerType = typeof(IEventHandler<>).MakeGenericType(@event.GetType());
        var handlers = _serviceProvider.GetServices(handlerType);

        var tasks = new List<Task>();
        
        foreach (var handler in handlers)
        {
            var handleMethod = handlerType.GetMethod("HandleAsync");
            if (handleMethod != null)
            {
                var task = (Task?)handleMethod.Invoke(handler, new object[] { @event, cancellationToken });
                if (task != null)
                {
                    tasks.Add(task);
                }
            }
        }

        await Task.WhenAll(tasks);
        
        _logger.LogDebug("Event {EventType} dispatched to {HandlerCount} handlers",
            @event.GetType().Name, tasks.Count);
    }
}

/// <summary>
/// Event handler interface
/// </summary>
public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// Saga interface for long-running processes
/// </summary>
public interface ISaga
{
    Guid Id { get; }
    bool IsCompleted { get; }
    Task HandleAsync(IEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// Base saga implementation
/// </summary>
public abstract class SagaBase : ISaga
{
    private readonly List<ICommand> _commands = new();
    
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public bool IsCompleted { get; protected set; }
    
    public abstract Task HandleAsync(IEvent @event, CancellationToken cancellationToken = default);
    
    protected void PublishCommand(ICommand command)
    {
        _commands.Add(command);
    }
    
    public IEnumerable<ICommand> GetUnpublishedCommands()
    {
        return _commands;
    }
    
    public void MarkCommandsAsPublished()
    {
        _commands.Clear();
    }
}

/// <summary>
/// Command interface for sagas
/// </summary>
public interface ICommand
{
    Guid CommandId { get; }
    DateTime Timestamp { get; }
}

/// <summary>
/// Saga manager
/// </summary>
public interface ISagaManager
{
    Task<ISaga?> GetSagaAsync(Guid sagaId, CancellationToken cancellationToken = default);
    Task SaveSagaAsync(ISaga saga, CancellationToken cancellationToken = default);
    Task HandleEventAsync(IEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory saga manager implementation
/// </summary>
public class InMemorySagaManager : ISagaManager
{
    private readonly Dictionary<Guid, ISaga> _sagas = new();
    private readonly ILogger<InMemorySagaManager> _logger;
    private readonly object _lock = new();

    public InMemorySagaManager(ILogger<InMemorySagaManager> logger)
    {
        _logger = logger;
    }

    public Task<ISaga?> GetSagaAsync(Guid sagaId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _sagas.TryGetValue(sagaId, out var saga);
            return Task.FromResult(saga);
        }
    }

    public Task SaveSagaAsync(ISaga saga, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _sagas[saga.Id] = saga;
            _logger.LogDebug("Saved saga {SagaId}", saga.Id);
        }
        
        return Task.CompletedTask;
    }

    public async Task HandleEventAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        // Find sagas that can handle this event
        List<ISaga> interestedSagas;
        
        lock (_lock)
        {
            interestedSagas = _sagas.Values
                .Where(s => !s.IsCompleted)
                .ToList();
        }

        foreach (var saga in interestedSagas)
        {
            await saga.HandleAsync(@event, cancellationToken);
            
            if (saga.IsCompleted)
            {
                lock (_lock)
                {
                    _sagas.Remove(saga.Id);
                    _logger.LogInformation("Saga {SagaId} completed and removed", saga.Id);
                }
            }
        }
    }
}
