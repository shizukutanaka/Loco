// Phase 3: Event Sourcing for Complete Audit Trail
// All workflow events are immutable records for perfect replay and auditing

namespace Loco.Core.Workflows.DurableExecution;

/// <summary>
/// Base class for all workflow execution events
/// Part of event sourcing pattern for complete audit trail
/// </summary>
public abstract class WorkflowExecutionEvent
{
    public string ExecutionId { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string EventType => GetType().Name;
}

/// <summary>
/// Workflow execution started
/// </summary>
public class WorkflowExecutionStartedEvent : WorkflowExecutionEvent
{
    public string WorkflowId { get; set; } = "";
    public Dictionary<string, object>? Input { get; set; }
}

/// <summary>
/// Workflow execution completed successfully
/// </summary>
public class WorkflowExecutionCompletedEvent : WorkflowExecutionEvent
{
    public string WorkflowId { get; set; } = "";
    public Dictionary<string, object?>? Output { get; set; }
}

/// <summary>
/// Workflow execution failed
/// </summary>
public class WorkflowExecutionFailedEvent : WorkflowExecutionEvent
{
    public string WorkflowId { get; set; } = "";
    public string? FailedStepId { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Step execution started
/// </summary>
public class StepExecutionStartedEvent : WorkflowExecutionEvent
{
    public string StepId { get; set; } = "";
    public int Attempt { get; set; }
}

/// <summary>
/// Step execution completed
/// </summary>
public class StepExecutionCompletedEvent : WorkflowExecutionEvent
{
    public string StepId { get; set; } = "";
    public object? Output { get; set; }
    public double DurationMs { get; set; }
    public int Attempt { get; set; }
}

/// <summary>
/// Step execution retry (for tracking retry attempts)
/// </summary>
public class StepExecutionRetryEvent : WorkflowExecutionEvent
{
    public string StepId { get; set; } = "";
    public int Attempt { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Compensation (Saga) started
/// </summary>
public class CompensationStartedEvent : WorkflowExecutionEvent
{
    public string StepId { get; set; } = "";
}

/// <summary>
/// Compensation (Saga) completed
/// </summary>
public class CompensationCompletedEvent : WorkflowExecutionEvent
{
    public string StepId { get; set; } = "";
}

/// <summary>
/// Compensation (Saga) failed
/// </summary>
public class CompensationFailedEvent : WorkflowExecutionEvent
{
    public string StepId { get; set; } = "";
    public string? Error { get; set; }
}

/// <summary>
/// Event store interface for persistence
/// </summary>
public interface IWorkflowExecutionEventStore
{
    /// <summary>
    /// Append event to execution history
    /// </summary>
    Task AppendEventAsync(
        WorkflowExecutionEvent @event,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all events for an execution
    /// </summary>
    Task<List<WorkflowExecutionEvent>> GetEventsAsync(
        string executionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get events since a specific point in time
    /// </summary>
    Task<List<WorkflowExecutionEvent>> GetEventsSinceAsync(
        string executionId,
        DateTime since,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get events for workflow
    /// </summary>
    Task<List<WorkflowExecutionEvent>> GetWorkflowEventsAsync(
        string workflowId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory event store implementation (for testing/development)
/// </summary>
public class InMemoryWorkflowExecutionEventStore : IWorkflowExecutionEventStore
{
    private readonly Dictionary<string, List<WorkflowExecutionEvent>> _events = new();
    private readonly object _lock = new();

    public Task AppendEventAsync(
        WorkflowExecutionEvent @event,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_events.ContainsKey(@event.ExecutionId))
            {
                _events[@event.ExecutionId] = new List<WorkflowExecutionEvent>();
            }

            _events[@event.ExecutionId].Add(@event);
        }

        return Task.CompletedTask;
    }

    public Task<List<WorkflowExecutionEvent>> GetEventsAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_events.TryGetValue(executionId, out var events))
            {
                return Task.FromResult(new List<WorkflowExecutionEvent>(events));
            }

            return Task.FromResult(new List<WorkflowExecutionEvent>());
        }
    }

    public Task<List<WorkflowExecutionEvent>> GetEventsSinceAsync(
        string executionId,
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_events.TryGetValue(executionId, out var events))
            {
                var filtered = events.Where(e => e.Timestamp > since).ToList();
                return Task.FromResult(filtered);
            }

            return Task.FromResult(new List<WorkflowExecutionEvent>());
        }
    }

    public Task<List<WorkflowExecutionEvent>> GetWorkflowEventsAsync(
        string workflowId,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var allEvents = _events.Values
                .SelectMany(e => e)
                .Where(e =>
                {
                    return e switch
                    {
                        WorkflowExecutionStartedEvent startEvent => startEvent.WorkflowId == workflowId,
                        WorkflowExecutionCompletedEvent completedEvent => completedEvent.WorkflowId == workflowId,
                        WorkflowExecutionFailedEvent failedEvent => failedEvent.WorkflowId == workflowId,
                        _ => false
                    };
                })
                .ToList();

            return Task.FromResult(allEvents);
        }
    }
}
