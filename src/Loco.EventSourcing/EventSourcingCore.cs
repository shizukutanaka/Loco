using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.EventSourcing;

/// <summary>
/// Event sourcing and CQRS implementation
/// </summary>
public interface IEventStore
{
    Task<Guid> SaveEventAsync<T>(T @event, Guid aggregateId, int expectedVersion, CancellationToken cancellationToken = default) where T : IDomainEvent;
    Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid aggregateId, int fromVersion = 0, CancellationToken cancellationToken = default);
    Task<T?> GetAggregateAsync<T>(Guid aggregateId, CancellationToken cancellationToken = default) where T : AggregateRoot, new();
    Task<EventStream> GetEventStreamAsync(string streamName, int fromPosition = 0, int maxCount = 100, CancellationToken cancellationToken = default);
    Task<long> GetGlobalPositionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory event store implementation
/// </summary>
public class InMemoryEventStore : IEventStore
{
    private readonly ILogger<InMemoryEventStore> _logger;
    private readonly Dictionary<Guid, List<EventData>> _events = new();
    private readonly List<EventData> _globalStream = new();
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
    private long _globalPosition = 0;

    public InMemoryEventStore(ILogger<InMemoryEventStore> logger)
    {
        _logger = logger;
    }

    public Task<Guid> SaveEventAsync<T>(T @event, Guid aggregateId, int expectedVersion, CancellationToken cancellationToken = default) where T : IDomainEvent
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_events.TryGetValue(aggregateId, out var eventList))
            {
                eventList = new List<EventData>();
                _events[aggregateId] = eventList;
            }

            var currentVersion = eventList.Count;
            if (currentVersion != expectedVersion)
            {
                throw new ConcurrencyException($"Expected version {expectedVersion} but current version is {currentVersion}");
            }

            var eventData = new EventData
            {
                EventId = Guid.NewGuid(),
                AggregateId = aggregateId,
                EventType = @event.GetType().Name,
                EventData = JsonSerializer.Serialize(@event),
                EventVersion = currentVersion + 1,
                Timestamp = DateTime.UtcNow,
                GlobalPosition = ++_globalPosition
            };

            eventList.Add(eventData);
            _globalStream.Add(eventData);

            _logger.LogDebug("Saved event {EventType} for aggregate {AggregateId} at version {Version}",
                eventData.EventType, aggregateId, eventData.EventVersion);

            return Task.FromResult(eventData.EventId);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid aggregateId, int fromVersion = 0, CancellationToken cancellationToken = default)
    {
        _lock.EnterReadLock();
        try
        {
            if (!_events.TryGetValue(aggregateId, out var eventList))
            {
                return Task.FromResult(Enumerable.Empty<IDomainEvent>());
            }

            var events = eventList
                .Where(e => e.EventVersion > fromVersion)
                .OrderBy(e => e.EventVersion)
                .Select(e => DeserializeEvent(e))
                .Where(e => e != null)
                .Cast<IDomainEvent>();

            return Task.FromResult(events);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public async Task<T?> GetAggregateAsync<T>(Guid aggregateId, CancellationToken cancellationToken = default) where T : AggregateRoot, new()
    {
        var events = await GetEventsAsync(aggregateId, 0, cancellationToken);
        
        if (!events.Any())
        {
            return null;
        }

        var aggregate = new T();
        aggregate.LoadFromHistory(events);
        return aggregate;
    }

    public Task<EventStream> GetEventStreamAsync(string streamName, int fromPosition = 0, int maxCount = 100, CancellationToken cancellationToken = default)
    {
        _lock.EnterReadLock();
        try
        {
            var events = _globalStream
                .Where(e => (int)e.GlobalPosition > fromPosition)
                .Take(maxCount)
                .ToList();

            return Task.FromResult(new EventStream
            {
                StreamName = streamName,
                Events = events,
                FromPosition = fromPosition,
                ToPosition = events.LastOrDefault()?.GlobalPosition ?? fromPosition
            });
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<long> GetGlobalPositionAsync(CancellationToken cancellationToken = default)
    {
        _lock.EnterReadLock();
        try
        {
            return Task.FromResult(_globalPosition);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private IDomainEvent? DeserializeEvent(EventData eventData)
    {
        try
        {
            var eventType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == eventData.EventType && typeof(IDomainEvent).IsAssignableFrom(t));

            if (eventType == null)
            {
                _logger.LogWarning("Event type {EventType} not found", eventData.EventType);
                return null;
            }

            var @event = JsonSerializer.Deserialize(eventData.EventData, eventType) as IDomainEvent;
            return @event;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize event {EventType}", eventData.EventType);
            return null;
        }
    }
}

/// <summary>
/// Base class for aggregate roots
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _changes = new();
    
    public Guid Id { get; protected set; }
    public int Version { get; private set; } = 0;

    public IEnumerable<IDomainEvent> GetUncommittedChanges()
    {
        return _changes;
    }

    public void MarkChangesAsCommitted()
    {
        _changes.Clear();
    }

    public void LoadFromHistory(IEnumerable<IDomainEvent> history)
    {
        foreach (var @event in history)
        {
            ApplyChange(@event, false);
        }
    }

    protected void ApplyChange(IDomainEvent @event)
    {
        ApplyChange(@event, true);
    }

    private void ApplyChange(IDomainEvent @event, bool isNew)
    {
        var method = GetType().GetMethod("Apply", new[] { @event.GetType() });
        
        if (method == null)
        {
            throw new InvalidOperationException($"Apply method not found for event {@event.GetType().Name}");
        }

        method.Invoke(this, new object[] { @event });
        
        if (isNew)
        {
            _changes.Add(@event);
        }

        Version++;
    }
}

/// <summary>
/// Command handler interface
/// </summary>
public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task<CommandResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Query handler interface
/// </summary>
public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Command dispatcher
/// </summary>
public class CommandDispatcher : ICommandDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(IServiceProvider serviceProvider, ILogger<CommandDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<CommandResult> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand
    {
        _logger.LogDebug("Dispatching command {CommandType}", typeof(TCommand).Name);

        var handlerType = typeof(ICommandHandler<>).MakeGenericType(typeof(TCommand));
        var handler = _serviceProvider.GetService(handlerType);

        if (handler == null)
        {
            throw new InvalidOperationException($"No handler registered for command {typeof(TCommand).Name}");
        }

        var handleMethod = handlerType.GetMethod("HandleAsync");
        var task = (Task<CommandResult>)handleMethod!.Invoke(handler, new object[] { command, cancellationToken })!;
        
        return await task;
    }
}

/// <summary>
/// Query dispatcher
/// </summary>
public class QueryDispatcher : IQueryDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueryDispatcher> _logger;

    public QueryDispatcher(IServiceProvider serviceProvider, ILogger<QueryDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Dispatching query {QueryType}", query.GetType().Name);

        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        var handler = _serviceProvider.GetService(handlerType);

        if (handler == null)
        {
            throw new InvalidOperationException($"No handler registered for query {query.GetType().Name}");
        }

        var handleMethod = handlerType.GetMethod("HandleAsync");
        var task = (Task<TResult>)handleMethod!.Invoke(handler, new object[] { query, cancellationToken })!;
        
        return await task;
    }
}

/// <summary>
/// Event processor for projections
/// </summary>
public class EventProcessor : IEventProcessor
{
    private readonly IEventStore _eventStore;
    private readonly ILogger<EventProcessor> _logger;
    private readonly List<IProjection> _projections = new();
    private long _lastProcessedPosition = 0;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _processingTask;

    public EventProcessor(IEventStore eventStore, ILogger<EventProcessor> logger)
    {
        _eventStore = eventStore;
        _logger = logger;
    }

    public void RegisterProjection(IProjection projection)
    {
        _projections.Add(projection);
        _logger.LogInformation("Registered projection {ProjectionType}", projection.GetType().Name);
    }

    public void Start()
    {
        _processingTask = Task.Run(async () => await ProcessEventsAsync(_cancellationTokenSource.Token));
        _logger.LogInformation("Event processor started");
    }

    public async Task StopAsync()
    {
        _cancellationTokenSource.Cancel();
        
        if (_processingTask != null)
        {
            await _processingTask;
        }
        
        _logger.LogInformation("Event processor stopped");
    }

    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var stream = await _eventStore.GetEventStreamAsync("$all", (int)_lastProcessedPosition, 100, cancellationToken);
                
                foreach (var eventData in stream.Events)
                {
                    await ProcessEventAsync(eventData, cancellationToken);
                    _lastProcessedPosition = eventData.GlobalPosition;
                }

                if (!stream.Events.Any())
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing events");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private async Task ProcessEventAsync(EventData eventData, CancellationToken cancellationToken)
    {
        foreach (var projection in _projections)
        {
            try
            {
                await projection.HandleAsync(eventData, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event {EventType} in projection {ProjectionType}",
                    eventData.EventType, projection.GetType().Name);
            }
        }
    }
}

/// <summary>
/// Snapshot store for aggregate snapshots
/// </summary>
public class SnapshotStore : ISnapshotStore
{
    private readonly Dictionary<Guid, Snapshot> _snapshots = new();
    private readonly ReaderWriterLockSlim _lock = new();

    public Task SaveSnapshotAsync<T>(T aggregate, CancellationToken cancellationToken = default) where T : AggregateRoot
    {
        _lock.EnterWriteLock();
        try
        {
            var snapshot = new Snapshot
            {
                AggregateId = aggregate.Id,
                AggregateType = typeof(T).Name,
                Data = JsonSerializer.Serialize(aggregate),
                Version = aggregate.Version,
                Timestamp = DateTime.UtcNow
            };

            _snapshots[aggregate.Id] = snapshot;
            return Task.CompletedTask;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task<T?> GetSnapshotAsync<T>(Guid aggregateId, CancellationToken cancellationToken = default) where T : AggregateRoot
    {
        _lock.EnterReadLock();
        try
        {
            if (!_snapshots.TryGetValue(aggregateId, out var snapshot))
            {
                return Task.FromResult<T?>(null);
            }

            var aggregate = JsonSerializer.Deserialize<T>(snapshot.Data);
            return Task.FromResult(aggregate);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}

// Supporting interfaces and classes
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

public interface ICommand
{
    Guid CommandId { get; }
}

public interface IQuery<TResult>
{
}

public interface ICommandDispatcher
{
    Task<CommandResult> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand;
}

public interface IQueryDispatcher
{
    Task<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}

public interface IEventProcessor
{
    void RegisterProjection(IProjection projection);
    void Start();
    Task StopAsync();
}

public interface IProjection
{
    Task HandleAsync(EventData eventData, CancellationToken cancellationToken = default);
}

public interface ISnapshotStore
{
    Task SaveSnapshotAsync<T>(T aggregate, CancellationToken cancellationToken = default) where T : AggregateRoot;
    Task<T?> GetSnapshotAsync<T>(Guid aggregateId, CancellationToken cancellationToken = default) where T : AggregateRoot;
}

public class EventData
{
    public Guid EventId { get; set; }
    public Guid AggregateId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EventData { get; set; } = string.Empty;
    public int EventVersion { get; set; }
    public DateTime Timestamp { get; set; }
    public long GlobalPosition { get; set; }
}

public class EventStream
{
    public string StreamName { get; set; } = string.Empty;
    public List<EventData> Events { get; set; } = new();
    public int FromPosition { get; set; }
    public long ToPosition { get; set; }
}

public class Snapshot
{
    public Guid AggregateId { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime Timestamp { get; set; }
}

public class CommandResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public object? Data { get; set; }
    public List<string> Errors { get; set; } = new();

    public static CommandResult Ok(string? message = null, object? data = null)
    {
        return new CommandResult { Success = true, Message = message, Data = data };
    }

    public static CommandResult Fail(string error)
    {
        return new CommandResult { Success = false, Errors = new List<string> { error } };
    }

    public static CommandResult Fail(List<string> errors)
    {
        return new CommandResult { Success = false, Errors = errors };
    }
}

public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message)
    {
    }
}

/// <summary>
/// Sample implementation - Flow aggregate
/// </summary>
public class FlowAggregate : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ModifiedAt { get; private set; }

    public void Create(Guid id, string name, string description)
    {
        ApplyChange(new FlowCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            FlowId = id,
            Name = name,
            Description = description
        });
    }

    public void Enable()
    {
        if (IsEnabled)
            return;

        ApplyChange(new FlowEnabledEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            FlowId = Id
        });
    }

    public void Disable()
    {
        if (!IsEnabled)
            return;

        ApplyChange(new FlowDisabledEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            FlowId = Id
        });
    }

    public void UpdateDescription(string description)
    {
        ApplyChange(new FlowDescriptionUpdatedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            FlowId = Id,
            Description = description
        });
    }

    // Event handlers
    public void Apply(FlowCreatedEvent @event)
    {
        Id = @event.FlowId;
        Name = @event.Name;
        Description = @event.Description;
        IsEnabled = true;
        CreatedAt = @event.OccurredAt;
    }

    public void Apply(FlowEnabledEvent @event)
    {
        IsEnabled = true;
        ModifiedAt = @event.OccurredAt;
    }

    public void Apply(FlowDisabledEvent @event)
    {
        IsEnabled = false;
        ModifiedAt = @event.OccurredAt;
    }

    public void Apply(FlowDescriptionUpdatedEvent @event)
    {
        Description = @event.Description;
        ModifiedAt = @event.OccurredAt;
    }
}

// Domain events
public class FlowCreatedEvent : IDomainEvent
{
    public Guid EventId { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid FlowId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class FlowEnabledEvent : IDomainEvent
{
    public Guid EventId { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid FlowId { get; set; }
}

public class FlowDisabledEvent : IDomainEvent
{
    public Guid EventId { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid FlowId { get; set; }
}

public class FlowDescriptionUpdatedEvent : IDomainEvent
{
    public Guid EventId { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid FlowId { get; set; }
    public string Description { get; set; } = string.Empty;
}

// Sample commands
public class CreateFlowCommand : ICommand
{
    public Guid CommandId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class EnableFlowCommand : ICommand
{
    public Guid CommandId { get; set; } = Guid.NewGuid();
    public Guid FlowId { get; set; }
}

// Sample command handler
public class FlowCommandHandler : 
    ICommandHandler<CreateFlowCommand>,
    ICommandHandler<EnableFlowCommand>
{
    private readonly IEventStore _eventStore;
    private readonly ILogger<FlowCommandHandler> _logger;

    public FlowCommandHandler(IEventStore eventStore, ILogger<FlowCommandHandler> logger)
    {
        _eventStore = eventStore;
        _logger = logger;
    }

    public async Task<CommandResult> HandleAsync(CreateFlowCommand command, CancellationToken cancellationToken = default)
    {
        var flowId = Guid.NewGuid();
        var flow = new FlowAggregate();
        flow.Create(flowId, command.Name, command.Description);

        foreach (var @event in flow.GetUncommittedChanges())
        {
            await _eventStore.SaveEventAsync(@event, flowId, flow.Version - 1, cancellationToken);
        }

        return CommandResult.Ok("Flow created successfully", new { FlowId = flowId });
    }

    public async Task<CommandResult> HandleAsync(EnableFlowCommand command, CancellationToken cancellationToken = default)
    {
        var flow = await _eventStore.GetAggregateAsync<FlowAggregate>(command.FlowId, cancellationToken);
        
        if (flow == null)
        {
            return CommandResult.Fail("Flow not found");
        }

        flow.Enable();

        foreach (var @event in flow.GetUncommittedChanges())
        {
            await _eventStore.SaveEventAsync(@event, command.FlowId, flow.Version - 1, cancellationToken);
        }

        return CommandResult.Ok("Flow enabled successfully");
    }
}
