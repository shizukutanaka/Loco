using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.DurableExecution;

/// <summary>
/// イベントソーシングベースのイベントストア
/// ワークフローの状態変化を永続化
/// </summary>
public class EventStore
{
    private readonly ILogger<EventStore> _logger;
    private readonly Dictionary<string, List<WorkflowEvent>> _events = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public EventStore(ILogger<EventStore> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// イベントを追加
    /// </summary>
    public async Task AppendEventAsync(
        string workflowId,
        string executionId,
        WorkflowEvent @event,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var key = $"{workflowId}:{executionId}";
            if (!_events.ContainsKey(key))
            {
                _events[key] = new List<WorkflowEvent>();
            }

            @event.SequenceNumber = _events[key].Count + 1;
            @event.Timestamp = DateTimeOffset.UtcNow;
            _events[key].Add(@event);

            _logger.LogDebug(
                "Event appended: {EventType} for {ExecutionId} (Sequence: {Sequence})",
                @event.GetType().Name,
                executionId,
                @event.SequenceNumber);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// イベントを取得
    /// </summary>
    public async Task<List<WorkflowEvent>> GetEventsAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var matchingEvents = new List<WorkflowEvent>();
            foreach (var kvp in _events)
            {
                if (kvp.Key.EndsWith($":{executionId}"))
                {
                    matchingEvents.AddRange(kvp.Value);
                }
            }
            return matchingEvents;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 状態を再構築
    /// </summary>
    public async Task<WorkflowState> ReconstructStateAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        var events = await GetEventsAsync(executionId, cancellationToken);
        var state = new WorkflowState
        {
            ExecutionId = executionId
        };

        foreach (var @event in events)
        {
            state.ApplyEvent(@event);
        }

        return state;
    }
}

/// <summary>
/// ワークフローイベント基底クラス
/// </summary>
public abstract class WorkflowEvent
{
    public long SequenceNumber { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// ワークフロー開始イベント
/// </summary>
public class WorkflowStartedEvent : WorkflowEvent
{
    public object? Input { get; set; }
    public DateTimeOffset StartedAt { get; set; }
}

/// <summary>
/// ワークフロー完了イベント
/// </summary>
public class WorkflowCompletedEvent : WorkflowEvent
{
    public object? Output { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
}

/// <summary>
/// ワークフロー失敗イベント
/// </summary>
public class WorkflowFailedEvent : WorkflowEvent
{
    public string? Error { get; set; }
    public string? StackTrace { get; set; }
    public DateTimeOffset FailedAt { get; set; }
}

/// <summary>
/// アクティビティ開始イベント
/// </summary>
public class ActivityStartedEvent : WorkflowEvent
{
    public string ActivityName { get; set; } = string.Empty;
    public object? Input { get; set; }
}

/// <summary>
/// アクティビティ完了イベント
/// </summary>
public class ActivityCompletedEvent : WorkflowEvent
{
    public string ActivityName { get; set; } = string.Empty;
    public object? Output { get; set; }
}

/// <summary>
/// アクティビティ失敗イベント
/// </summary>
public class ActivityFailedEvent : WorkflowEvent
{
    public string ActivityName { get; set; } = string.Empty;
    public string? Error { get; set; }
    public int AttemptNumber { get; set; }
}

/// <summary>
/// タイマー開始イベント
/// </summary>
public class TimerStartedEvent : WorkflowEvent
{
    public TimeSpan Duration { get; set; }
    public DateTimeOffset FireAt { get; set; }
}

/// <summary>
/// タイマー完了イベント
/// </summary>
public class TimerFiredEvent : WorkflowEvent
{
    public DateTimeOffset FiredAt { get; set; }
}

/// <summary>
/// 外部イベント受信
/// </summary>
public class ExternalEventReceivedEvent : WorkflowEvent
{
    public string EventName { get; set; } = string.Empty;
    public object? EventData { get; set; }
}

/// <summary>
/// ワークフロー状態
/// イベントから再構築される
/// </summary>
public class WorkflowState
{
    public string ExecutionId { get; set; } = string.Empty;
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Pending;
    public object? Input { get; set; }
    public object? Output { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public List<string> CompletedActivities { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();

    public void ApplyEvent(WorkflowEvent @event)
    {
        switch (@event)
        {
            case WorkflowStartedEvent started:
                Status = WorkflowStatus.Running;
                Input = started.Input;
                StartedAt = started.StartedAt;
                break;

            case WorkflowCompletedEvent completed:
                Status = WorkflowStatus.Completed;
                Output = completed.Output;
                CompletedAt = completed.CompletedAt;
                break;

            case WorkflowFailedEvent failed:
                Status = WorkflowStatus.Failed;
                Error = failed.Error;
                CompletedAt = failed.FailedAt;
                break;

            case ActivityCompletedEvent activityCompleted:
                CompletedActivities.Add(activityCompleted.ActivityName);
                if (activityCompleted.Output != null)
                {
                    Variables[$"activity_{activityCompleted.ActivityName}_output"] = activityCompleted.Output;
                }
                break;
        }
    }
}

/// <summary>
/// ワークフロー状態
/// </summary>
public enum WorkflowStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
