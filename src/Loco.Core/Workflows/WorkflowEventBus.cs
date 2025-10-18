using System.Collections.Concurrent;

namespace Loco.Core.Workflows;

/// <summary>
/// Workflow event types.
/// </summary>
public enum WorkflowEventType
{
    WorkflowStarted,
    WorkflowCompleted,
    WorkflowFailed,
    WorkflowCancelled,
    StepStarted,
    StepCompleted,
    StepFailed,
    StepRetried,
    VariableChanged,
    StateChanged,
    Custom
}

/// <summary>
/// Workflow event.
/// </summary>
public class WorkflowEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public WorkflowEventType Type { get; set; }
    public string WorkflowId { get; set; } = "";
    public string? ExecutionId { get; set; }
    public string? StepId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Data { get; set; } = new();
    public string? Message { get; set; }
}

/// <summary>
/// Event handler delegate.
/// </summary>
public delegate Task WorkflowEventHandler(WorkflowEvent @event);

/// <summary>
/// Event subscription.
/// </summary>
public class EventSubscription
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public WorkflowEventType? EventType { get; set; }
    public string? WorkflowIdFilter { get; set; }
    public WorkflowEventHandler Handler { get; set; } = null!;
    public bool Enabled { get; set; } = true;
    public int CallCount { get; set; }
    public DateTime LastCalled { get; set; }
}

/// <summary>
/// Event bus for workflow events with pub/sub pattern.
/// </summary>
public class WorkflowEventBus
{
    private readonly ConcurrentDictionary<string, EventSubscription> _subscriptions = new();
    private readonly List<WorkflowEvent> _eventHistory = new();
    private readonly int _maxHistorySize;
    private readonly object _historyLock = new();
    private readonly SemaphoreSlim _publishSemaphore = new(10); // Limit concurrent handlers

    public WorkflowEventBus(int maxHistorySize = 10000)
    {
        _maxHistorySize = maxHistorySize;
    }

    /// <summary>
    /// Subscribes to workflow events.
    /// </summary>
    public string Subscribe(
        string name,
        WorkflowEventHandler handler,
        WorkflowEventType? eventType = null,
        string? workflowIdFilter = null)
    {
        var subscription = new EventSubscription
        {
            Name = name,
            EventType = eventType,
            WorkflowIdFilter = workflowIdFilter,
            Handler = handler
        };

        _subscriptions[subscription.Id] = subscription;
        return subscription.Id;
    }

    /// <summary>
    /// Unsubscribes from events.
    /// </summary>
    public bool Unsubscribe(string subscriptionId)
    {
        return _subscriptions.TryRemove(subscriptionId, out _);
    }

    /// <summary>
    /// Gets all subscriptions.
    /// </summary>
    public List<EventSubscription> GetSubscriptions()
    {
        return _subscriptions.Values.ToList();
    }

    /// <summary>
    /// Publishes an event to all matching subscribers.
    /// </summary>
    public async Task PublishAsync(WorkflowEvent @event)
    {
        // Add to history
        AddToHistory(@event);

        // Find matching subscriptions
        var matchingSubscriptions = _subscriptions.Values
            .Where(s => s.Enabled && MatchesSubscription(@event, s))
            .ToList();

        if (matchingSubscriptions.Count == 0)
            return;

        // Execute handlers
        var tasks = matchingSubscriptions.Select(async subscription =>
        {
            await _publishSemaphore.WaitAsync();
            try
            {
                await subscription.Handler(@event);
                subscription.CallCount++;
                subscription.LastCalled = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                // Log error but don't fail other handlers
                Console.Error.WriteLine($"Event handler '{subscription.Name}' failed: {ex.Message}");
            }
            finally
            {
                _publishSemaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Publishes an event without waiting for handlers.
    /// </summary>
    public void PublishFireAndForget(WorkflowEvent @event)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await PublishAsync(@event);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fire-and-forget event publish failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Checks if event matches subscription criteria.
    /// </summary>
    private bool MatchesSubscription(WorkflowEvent @event, EventSubscription subscription)
    {
        // Check event type filter
        if (subscription.EventType.HasValue && subscription.EventType.Value != @event.Type)
            return false;

        // Check workflow ID filter
        if (!string.IsNullOrWhiteSpace(subscription.WorkflowIdFilter) &&
            !subscription.WorkflowIdFilter.Equals(@event.WorkflowId, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <summary>
    /// Adds event to history.
    /// </summary>
    private void AddToHistory(WorkflowEvent @event)
    {
        lock (_historyLock)
        {
            _eventHistory.Add(@event);

            if (_eventHistory.Count > _maxHistorySize)
            {
                _eventHistory.RemoveRange(0, _eventHistory.Count - _maxHistorySize);
            }
        }
    }

    /// <summary>
    /// Gets event history.
    /// </summary>
    public List<WorkflowEvent> GetEventHistory(int limit = 100)
    {
        lock (_historyLock)
        {
            return _eventHistory.TakeLast(limit).ToList();
        }
    }

    /// <summary>
    /// Gets events for a specific workflow.
    /// </summary>
    public List<WorkflowEvent> GetWorkflowEvents(string workflowId, int limit = 100)
    {
        lock (_historyLock)
        {
            return _eventHistory
                .Where(e => e.WorkflowId == workflowId)
                .TakeLast(limit)
                .ToList();
        }
    }

    /// <summary>
    /// Gets events by type.
    /// </summary>
    public List<WorkflowEvent> GetEventsByType(WorkflowEventType type, int limit = 100)
    {
        lock (_historyLock)
        {
            return _eventHistory
                .Where(e => e.Type == type)
                .TakeLast(limit)
                .ToList();
        }
    }

    /// <summary>
    /// Clears event history.
    /// </summary>
    public void ClearHistory()
    {
        lock (_historyLock)
        {
            _eventHistory.Clear();
        }
    }

    /// <summary>
    /// Generates an event bus status report.
    /// </summary>
    public string GenerateStatusReport()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║ EVENT BUS STATUS                                                              ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        var subscriptions = GetSubscriptions();

        sb.AppendLine($"Active Subscriptions: {subscriptions.Count(s => s.Enabled)}");
        sb.AppendLine($"Total Subscriptions: {subscriptions.Count}");
        sb.AppendLine();

        if (subscriptions.Count > 0)
        {
            sb.AppendLine("Subscriptions:");
            foreach (var sub in subscriptions.OrderBy(s => s.Name))
            {
                var status = sub.Enabled ? "✅" : "❌";
                sb.AppendLine($"{status} {sub.Name} ({sub.Id})");

                if (sub.EventType.HasValue)
                {
                    sb.AppendLine($"   Event Type: {sub.EventType.Value}");
                }

                if (!string.IsNullOrWhiteSpace(sub.WorkflowIdFilter))
                {
                    sb.AppendLine($"   Workflow Filter: {sub.WorkflowIdFilter}");
                }

                sb.AppendLine($"   Calls: {sub.CallCount}");

                if (sub.CallCount > 0)
                {
                    sb.AppendLine($"   Last Called: {sub.LastCalled:yyyy-MM-dd HH:mm:ss}");
                }

                sb.AppendLine();
            }
        }

        // Event statistics
        lock (_historyLock)
        {
            if (_eventHistory.Count > 0)
            {
                sb.AppendLine("Event Statistics:");
                sb.AppendLine($"  Total Events: {_eventHistory.Count}");

                var byType = _eventHistory.GroupBy(e => e.Type).OrderByDescending(g => g.Count());
                sb.AppendLine("  By Type:");
                foreach (var group in byType)
                {
                    sb.AppendLine($"    {group.Key}: {group.Count()}");
                }

                var recentEvent = _eventHistory.LastOrDefault();
                if (recentEvent != null)
                {
                    sb.AppendLine($"  Last Event: {recentEvent.Timestamp:yyyy-MM-dd HH:mm:ss} ({recentEvent.Type})");
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates an event activity report.
    /// </summary>
    public string GenerateActivityReport(int limit = 50)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║ EVENT ACTIVITY                                                                ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        var events = GetEventHistory(limit);

        if (events.Count == 0)
        {
            sb.AppendLine("No events recorded.");
            sb.AppendLine();
            return sb.ToString();
        }

        sb.AppendLine($"Showing last {events.Count} events:");
        sb.AppendLine();

        foreach (var @event in events.OrderByDescending(e => e.Timestamp))
        {
            var icon = GetEventIcon(@event.Type);
            sb.AppendLine($"{icon} [{@event.Timestamp:HH:mm:ss}] {@event.Type}");
            sb.AppendLine($"   Workflow: {@event.WorkflowId}");

            if (!string.IsNullOrWhiteSpace(@event.ExecutionId))
            {
                sb.AppendLine($"   Execution: {@event.ExecutionId}");
            }

            if (!string.IsNullOrWhiteSpace(@event.StepId))
            {
                sb.AppendLine($"   Step: {@event.StepId}");
            }

            if (!string.IsNullOrWhiteSpace(@event.Message))
            {
                sb.AppendLine($"   Message: {@event.Message}");
            }

            if (@event.Data.Count > 0)
            {
                sb.AppendLine($"   Data: {string.Join(", ", @event.Data.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string GetEventIcon(WorkflowEventType type)
    {
        return type switch
        {
            WorkflowEventType.WorkflowStarted => "▶️",
            WorkflowEventType.WorkflowCompleted => "✅",
            WorkflowEventType.WorkflowFailed => "❌",
            WorkflowEventType.WorkflowCancelled => "🚫",
            WorkflowEventType.StepStarted => "▶️",
            WorkflowEventType.StepCompleted => "✓",
            WorkflowEventType.StepFailed => "✗",
            WorkflowEventType.StepRetried => "🔄",
            WorkflowEventType.VariableChanged => "📝",
            WorkflowEventType.StateChanged => "🔄",
            WorkflowEventType.Custom => "📌",
            _ => "•"
        };
    }
}

/// <summary>
/// Helper extensions for publishing common events.
/// </summary>
public static class WorkflowEventBusExtensions
{
    public static async Task PublishWorkflowStartedAsync(
        this WorkflowEventBus bus,
        string workflowId,
        string executionId,
        Dictionary<string, object>? data = null)
    {
        await bus.PublishAsync(new WorkflowEvent
        {
            Type = WorkflowEventType.WorkflowStarted,
            WorkflowId = workflowId,
            ExecutionId = executionId,
            Data = data ?? new Dictionary<string, object>()
        });
    }

    public static async Task PublishWorkflowCompletedAsync(
        this WorkflowEventBus bus,
        string workflowId,
        string executionId,
        TimeSpan duration,
        Dictionary<string, object>? data = null)
    {
        var eventData = data ?? new Dictionary<string, object>();
        eventData["duration"] = duration.TotalSeconds;

        await bus.PublishAsync(new WorkflowEvent
        {
            Type = WorkflowEventType.WorkflowCompleted,
            WorkflowId = workflowId,
            ExecutionId = executionId,
            Data = eventData
        });
    }

    public static async Task PublishWorkflowFailedAsync(
        this WorkflowEventBus bus,
        string workflowId,
        string executionId,
        string errorMessage,
        Dictionary<string, object>? data = null)
    {
        var eventData = data ?? new Dictionary<string, object>();
        eventData["error"] = errorMessage;

        await bus.PublishAsync(new WorkflowEvent
        {
            Type = WorkflowEventType.WorkflowFailed,
            WorkflowId = workflowId,
            ExecutionId = executionId,
            Message = errorMessage,
            Data = eventData
        });
    }

    public static async Task PublishStepStartedAsync(
        this WorkflowEventBus bus,
        string workflowId,
        string executionId,
        string stepId,
        string stepName)
    {
        await bus.PublishAsync(new WorkflowEvent
        {
            Type = WorkflowEventType.StepStarted,
            WorkflowId = workflowId,
            ExecutionId = executionId,
            StepId = stepId,
            Message = stepName
        });
    }

    public static async Task PublishStepCompletedAsync(
        this WorkflowEventBus bus,
        string workflowId,
        string executionId,
        string stepId,
        TimeSpan duration)
    {
        await bus.PublishAsync(new WorkflowEvent
        {
            Type = WorkflowEventType.StepCompleted,
            WorkflowId = workflowId,
            ExecutionId = executionId,
            StepId = stepId,
            Data = new Dictionary<string, object> { { "duration", duration.TotalSeconds } }
        });
    }

    public static async Task PublishStepFailedAsync(
        this WorkflowEventBus bus,
        string workflowId,
        string executionId,
        string stepId,
        string errorMessage)
    {
        await bus.PublishAsync(new WorkflowEvent
        {
            Type = WorkflowEventType.StepFailed,
            WorkflowId = workflowId,
            ExecutionId = executionId,
            StepId = stepId,
            Message = errorMessage,
            Data = new Dictionary<string, object> { { "error", errorMessage } }
        });
    }
}
