#nullable enable

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Loco.Core.EventSourcing;

/// <summary>
/// CQRS: Command Query Responsibility Segregation
/// Separates read (query) and write (command) operations
/// Event Sourcing: Stores all changes as immutable events
/// </summary>

/// <summary>
/// Base event class for all domain events
/// </summary>
public abstract class DomainEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int Version { get; set; }
    public string? CorrelationId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    public abstract string EventType { get; }
}

/// <summary>
/// Base command for write operations
/// </summary>
public abstract class Command
{
    public string CommandId { get; set; } = Guid.NewGuid().ToString();
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Base query for read operations
/// </summary>
public abstract class Query<TResult>
{
    public string QueryId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Command handler interface
/// </summary>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : Command
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Query handler interface
/// </summary>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : Query<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Event store - persistence layer for events
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Appends event to stream
    /// </summary>
    Task AppendAsync(string streamId, DomainEvent @event, int expectedVersion = -1);

    /// <summary>
    /// Gets all events for stream
    /// </summary>
    Task<List<DomainEvent>> GetEventsAsync(string streamId, int fromVersion = 0);

    /// <summary>
    /// Gets all events across all streams
    /// </summary>
    Task<List<DomainEvent>> GetAllEventsAsync(int fromVersion = 0);

    /// <summary>
    /// Subscribes to events
    /// </summary>
    IAsyncEnumerable<DomainEvent> SubscribeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory event store implementation (for demo/testing)
/// Production: Use EventStoreDB or similar
/// </summary>
public class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<string, List<DomainEvent>> _streams = new();
    private readonly List<DomainEvent> _allEvents = new();
    private readonly ILogger<InMemoryEventStore> _logger;

    public InMemoryEventStore(ILogger<InMemoryEventStore> logger)
    {
        _logger = logger;
    }

    public Task AppendAsync(string streamId, DomainEvent @event, int expectedVersion = -1)
    {
        _streams.AddOrUpdate(streamId, new List<DomainEvent> { @event }, (_, events) =>
        {
            if (expectedVersion >= 0 && events.Count != expectedVersion)
            {
                throw new ConcurrencyException(
                    $"Concurrency conflict: expected version {expectedVersion}, got {events.Count}");
            }

            events.Add(@event);
            return events;
        });

        _allEvents.Add(@event);
        _logger.LogInformation("Event appended: {EventType} to stream {StreamId}", @event.EventType, streamId);
        return Task.CompletedTask;
    }

    public Task<List<DomainEvent>> GetEventsAsync(string streamId, int fromVersion = 0)
    {
        if (_streams.TryGetValue(streamId, out var events))
        {
            return Task.FromResult(events.Skip(fromVersion).ToList());
        }

        return Task.FromResult(new List<DomainEvent>());
    }

    public Task<List<DomainEvent>> GetAllEventsAsync(int fromVersion = 0)
    {
        return Task.FromResult(_allEvents.Skip(fromVersion).ToList());
    }

    public async IAsyncEnumerable<DomainEvent> SubscribeAsync(CancellationToken cancellationToken = default)
    {
        var lastIndex = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (lastIndex < _allEvents.Count)
            {
                var newEvents = _allEvents.Skip(lastIndex).ToList();
                foreach (var @event in newEvents)
                {
                    yield return @event;
                }
                lastIndex = _allEvents.Count;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Aggregate root - entity that coordinates changes via events
/// </summary>
public abstract class AggregateRoot
{
    protected List<DomainEvent> UncommittedEvents { get; } = new();
    public string Id { get; protected set; } = string.Empty;
    public int Version { get; protected set; }

    protected void RaiseEvent(DomainEvent @event)
    {
        @event.Version = Version + 1;
        ApplyEvent(@event);
        UncommittedEvents.Add(@event);
    }

    protected abstract void ApplyEvent(DomainEvent @event);

    public void LoadFromHistory(IEnumerable<DomainEvent> history)
    {
        foreach (var @event in history)
        {
            ApplyEvent(@event);
            Version = @event.Version;
        }
    }

    public List<DomainEvent> GetUncommittedEvents()
    {
        var events = UncommittedEvents.ToList();
        UncommittedEvents.Clear();
        return events;
    }
}

/// <summary>
/// Example: Workflow aggregate with event sourcing
/// </summary>
public class WorkflowAggregate : AggregateRoot
{
    private bool _isActive;
    private string? _name;

    // Domain events
    public class WorkflowCreated : DomainEvent
    {
        public string? Name { get; set; }
        public override string EventType => "WorkflowCreated";
    }

    public class WorkflowStarted : DomainEvent
    {
        public override string EventType => "WorkflowStarted";
    }

    public class WorkflowCompleted : DomainEvent
    {
        public string? Status { get; set; }
        public override string EventType => "WorkflowCompleted";
    }

    // Commands
    public void CreateWorkflow(string id, string name)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("ID required");

        RaiseEvent(new WorkflowCreated
        {
            Version = 0,
            Timestamp = DateTime.UtcNow,
            Name = name
        });
    }

    public void StartWorkflow()
    {
        if (!_isActive)
            throw new InvalidOperationException("Workflow not active");

        RaiseEvent(new WorkflowStarted
        {
            Timestamp = DateTime.UtcNow
        });
    }

    public void CompleteWorkflow(string status)
    {
        RaiseEvent(new WorkflowCompleted
        {
            Status = status,
            Timestamp = DateTime.UtcNow
        });
    }

    // Event handlers (Apply logic)
    protected override void ApplyEvent(DomainEvent @event)
    {
        switch (@event)
        {
            case WorkflowCreated created:
                Id = created.EventId;
                _name = created.Name;
                _isActive = true;
                break;

            case WorkflowStarted:
                // Workflow started
                break;

            case WorkflowCompleted completed:
                _isActive = false;
                break;
        }
    }
}

/// <summary>
/// Repository with event sourcing
/// </summary>
public class EventSourcedRepository<TAggregate>
    where TAggregate : AggregateRoot, new()
{
    private readonly IEventStore _eventStore;
    private readonly ILogger<EventSourcedRepository<TAggregate>> _logger;

    public EventSourcedRepository(
        IEventStore eventStore,
        ILogger<EventSourcedRepository<TAggregate>> logger)
    {
        _eventStore = eventStore;
        _logger = logger;
    }

    public async Task<TAggregate> GetAsync(string id)
    {
        var events = await _eventStore.GetEventsAsync(id).ConfigureAwait(false);

        if (events.Count == 0)
            throw new AggregateNotFoundException($"Aggregate {id} not found");

        var aggregate = new TAggregate();
        aggregate.LoadFromHistory(events);
        return aggregate;
    }

    public async Task SaveAsync(TAggregate aggregate)
    {
        var events = aggregate.GetUncommittedEvents();

        foreach (var @event in events)
        {
            await _eventStore.AppendAsync(aggregate.Id, @event).ConfigureAwait(false);
        }

        _logger.LogInformation("Aggregate saved: {AggregateId}, Events: {EventCount}",
            aggregate.Id, events.Count);
    }
}

/// <summary>
/// Projection: Read model built from events
/// </summary>
public interface IProjection
{
    Task HandleAsync(DomainEvent @event);
}

/// <summary>
/// Example projection: Workflow summary for queries
/// </summary>
public class WorkflowSummaryProjection : IProjection
{
    private readonly Dictionary<string, WorkflowSummaryModel> _projections = new();
    private readonly ILogger<WorkflowSummaryProjection> _logger;

    public WorkflowSummaryProjection(ILogger<WorkflowSummaryProjection> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent @event)
    {
        switch (@event)
        {
            case WorkflowAggregate.WorkflowCreated created:
                _projections[created.EventId] = new WorkflowSummaryModel
                {
                    Id = created.EventId,
                    Name = created.Name,
                    CreatedAt = created.Timestamp,
                    Status = "Created"
                };
                _logger.LogInformation("Projection updated: WorkflowCreated {Id}", created.EventId);
                break;

            case WorkflowAggregate.WorkflowStarted started:
                if (_projections.TryGetValue(started.EventId, out var summary))
                {
                    summary.Status = "Started";
                    summary.StartedAt = started.Timestamp;
                }
                break;

            case WorkflowAggregate.WorkflowCompleted completed:
                if (_projections.TryGetValue(completed.EventId, out var summary2))
                {
                    summary2.Status = completed.Status ?? "Completed";
                    summary2.CompletedAt = completed.Timestamp;
                }
                break;
        }

        return Task.CompletedTask;
    }

    public WorkflowSummaryModel? GetSummary(string id)
    {
        _projections.TryGetValue(id, out var summary);
        return summary;
    }

    public List<WorkflowSummaryModel> GetAll()
    {
        return _projections.Values.ToList();
    }
}

/// <summary>
/// Read model (projection target)
/// </summary>
public class WorkflowSummaryModel
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Projection synchronizer - keeps projections in sync with events
/// </summary>
public class ProjectionSynchronizer
{
    private readonly IEventStore _eventStore;
    private readonly IProjection[] _projections;
    private readonly ILogger<ProjectionSynchronizer> _logger;

    public ProjectionSynchronizer(
        IEventStore eventStore,
        IProjection[] projections,
        ILogger<ProjectionSynchronizer> logger)
    {
        _eventStore = eventStore;
        _projections = projections;
        _logger = logger;
    }

    public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        // Initial sync from all events
        var allEvents = await _eventStore.GetAllEventsAsync().ConfigureAwait(false);
        foreach (var @event in allEvents)
        {
            foreach (var projection in _projections)
            {
                await projection.HandleAsync(@event).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("Projections synchronized from {EventCount} events", allEvents.Count);

        // Listen for new events
        await foreach (var @event in _eventStore.SubscribeAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            foreach (var projection in _projections)
            {
                await projection.HandleAsync(@event).ConfigureAwait(false);
            }
        }
    }
}

/// <summary>
/// Command dispatcher
/// </summary>
public class CommandDispatcher
{
    private readonly Dictionary<Type, Delegate> _handlers = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(IServiceProvider serviceProvider, ILogger<CommandDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void Register<TCommand, TResult>(
        ICommandHandler<TCommand, TResult> handler)
        where TCommand : Command
    {
        _handlers[typeof(TCommand)] = handler;
        _logger.LogDebug("Registered handler for command: {Command}", typeof(TCommand).Name);
    }

    public async Task<TResult> DispatchAsync<TCommand, TResult>(TCommand command)
        where TCommand : Command
    {
        if (!_handlers.TryGetValue(typeof(TCommand), out var handlerDelegate))
        {
            throw new NoHandlerRegisteredException($"No handler for {typeof(TCommand).Name}");
        }

        var handler = (ICommandHandler<TCommand, TResult>)handlerDelegate;
        _logger.LogInformation("Dispatching command: {Command} ({CommandId})",
            typeof(TCommand).Name, command.CommandId);

        return await handler.HandleAsync(command).ConfigureAwait(false);
    }
}

/// <summary>
/// Query dispatcher
/// </summary>
public class QueryDispatcher
{
    private readonly Dictionary<Type, Delegate> _handlers = new();
    private readonly ILogger<QueryDispatcher> _logger;

    public QueryDispatcher(ILogger<QueryDispatcher> logger)
    {
        _logger = logger;
    }

    public void Register<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
        where TQuery : Query<TResult>
    {
        _handlers[typeof(TQuery)] = handler;
        _logger.LogDebug("Registered handler for query: {Query}", typeof(TQuery).Name);
    }

    public async Task<TResult> DispatchAsync<TQuery, TResult>(TQuery query)
        where TQuery : Query<TResult>
    {
        if (!_handlers.TryGetValue(typeof(TQuery), out var handlerDelegate))
        {
            throw new NoHandlerRegisteredException($"No handler for {typeof(TQuery).Name}");
        }

        var handler = (IQueryHandler<TQuery, TResult>)handlerDelegate;
        _logger.LogInformation("Dispatching query: {Query} ({QueryId})",
            typeof(TQuery).Name, query.QueryId);

        return await handler.HandleAsync(query).ConfigureAwait(false);
    }
}

/// <summary>
/// Sagas: Long-running transactions across aggregates
/// </summary>
public abstract class Saga
{
    public string SagaId { get; set; } = Guid.NewGuid().ToString();
    public SagaStatus Status { get; protected set; } = SagaStatus.Started;

    public abstract Task HandleAsync(DomainEvent @event);
}

public enum SagaStatus
{
    Started,
    InProgress,
    Completed,
    Failed,
    Compensating,
    Compensated
}

/// <summary>
/// Example: Workflow execution saga
/// Coordinates multiple aggregates through events
/// </summary>
public class WorkflowExecutionSaga : Saga
{
    private readonly CommandDispatcher _commandDispatcher;
    private List<string> _CompensatingCommands { get; } = new();

    public WorkflowExecutionSaga(CommandDispatcher commandDispatcher)
    {
        _commandDispatcher = commandDispatcher;
    }

    public override async Task HandleAsync(DomainEvent @event)
    {
        switch (@event)
        {
            case WorkflowAggregate.WorkflowCreated created:
                Status = SagaStatus.InProgress;
                // Chain of commands through saga
                break;

            case WorkflowAggregate.WorkflowStarted:
                // Continue saga
                break;

            case WorkflowAggregate.WorkflowCompleted completed:
                Status = SagaStatus.Completed;
                break;
        }

        await Task.CompletedTask;
    }

    public async Task CompensateAsync()
    {
        Status = SagaStatus.Compensating;
        // Execute compensating commands in reverse order
        Status = SagaStatus.Compensated;
    }
}

/// <summary>
/// Exceptions
/// </summary>
public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
}

public class AggregateNotFoundException : Exception
{
    public AggregateNotFoundException(string message) : base(message) { }
}

public class NoHandlerRegisteredException : Exception
{
    public NoHandlerRegisteredException(string message) : base(message) { }
}

/// <summary>
/// Extension methods for CQRS/Event Sourcing setup
/// </summary>
public static class CqrsEventSourcingExtensions
{
    public static IServiceCollection AddEventSourcing(this IServiceCollection services)
    {
        services.AddSingleton<IEventStore, InMemoryEventStore>();
        services.AddSingleton(typeof(EventSourcedRepository<>));
        services.AddSingleton<CommandDispatcher>();
        services.AddSingleton<QueryDispatcher>();
        services.AddSingleton<ProjectionSynchronizer>();

        return services;
    }

    public static IServiceCollection AddProjection<TProjection>(
        this IServiceCollection services)
        where TProjection : class, IProjection
    {
        services.AddSingleton<IProjection, TProjection>();
        return services;
    }
}

/// <summary>
/// Example handlers
/// </summary>
public class CreateWorkflowCommand : Command
{
    public string? Name { get; set; }
}

public class CreateWorkflowCommandHandler : ICommandHandler<CreateWorkflowCommand, string>
{
    private readonly EventSourcedRepository<WorkflowAggregate> _repository;

    public CreateWorkflowCommandHandler(EventSourcedRepository<WorkflowAggregate> repository)
    {
        _repository = repository;
    }

    public async Task<string> HandleAsync(CreateWorkflowCommand command, CancellationToken cancellationToken = default)
    {
        var workflow = new WorkflowAggregate();
        workflow.CreateWorkflow(Guid.NewGuid().ToString(), command.Name ?? "Unnamed");
        await _repository.SaveAsync(workflow).ConfigureAwait(false);
        return workflow.Id;
    }
}

public class GetWorkflowSummaryQuery : Query<WorkflowSummaryModel?>
{
    public string? WorkflowId { get; set; }
}

public class GetWorkflowSummaryQueryHandler : IQueryHandler<GetWorkflowSummaryQuery, WorkflowSummaryModel?>
{
    private readonly WorkflowSummaryProjection _projection;

    public GetWorkflowSummaryQueryHandler(WorkflowSummaryProjection projection)
    {
        _projection = projection;
    }

    public Task<WorkflowSummaryModel?> HandleAsync(GetWorkflowSummaryQuery query, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_projection.GetSummary(query.WorkflowId ?? ""));
    }
}
