#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Temporal;

/// <summary>
/// Temporal.io Workflow Orchestration Patterns
/// Durable execution, saga orchestration, failure recovery
/// </summary>

/// <summary>
/// Workflow state machine
/// </summary>
public enum WorkflowState
{
    Running,
    Completed,
    Failed,
    Terminated,
    TimedOut
}

/// <summary>
/// Activity result
/// </summary>
public class ActivityResult
{
    [JsonPropertyName("activityId")]
    public string ActivityId { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Activity definition
/// </summary>
public class Activity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // e.g., "PaymentService.ProcessPayment"

    [JsonPropertyName("input")]
    public Dictionary<string, object> Input { get; set; } = new();

    [JsonPropertyName("timeout")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    [JsonPropertyName("retryPolicy")]
    public RetryPolicy RetryPolicy { get; set; } = new();

    [JsonPropertyName("compensatingActivity")]
    public Activity? CompensatingActivity { get; set; }
}

/// <summary>
/// Retry policy for activity
/// </summary>
public class RetryPolicy
{
    [JsonPropertyName("maxAttempts")]
    public int MaxAttempts { get; set; } = 3;

    [JsonPropertyName("initialBackoff")]
    public TimeSpan InitialBackoff { get; set; } = TimeSpan.FromSeconds(1);

    [JsonPropertyName("backoffMultiplier")]
    public double BackoffMultiplier { get; set; } = 2.0;

    [JsonPropertyName("maxBackoff")]
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromMinutes(10);
}

/// <summary>
/// Workflow definition
/// </summary>
public abstract class TemporalWorkflow
{
    protected readonly ILogger Logger;

    public TemporalWorkflow(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Define workflow logic
    /// </summary>
    public abstract Task<object?> ExecuteAsync(Dictionary<string, object> input);

    /// <summary>
    /// Get workflow name
    /// </summary>
    public virtual string GetWorkflowName()
    {
        return GetType().Name.Replace("Workflow", "");
    }
}

/// <summary>
/// Saga workflow - distributed transaction with compensation
/// </summary>
public class SagaWorkflow : TemporalWorkflow
{
    private readonly List<(Activity activity, Activity? compensation)> _steps = new();
    private readonly Stack<Activity> _completedSteps = new();

    public SagaWorkflow(ILogger logger) : base(logger) { }

    /// <summary>
    /// Add step with optional compensation
    /// </summary>
    public void AddStep(Activity activity, Activity? compensation = null)
    {
        _steps.Add((activity, compensation));
    }

    /// <summary>
    /// Execute saga workflow
    /// </summary>
    public override async Task<object?> ExecuteAsync(Dictionary<string, object> input)
    {
        Logger.LogInformation("Starting saga workflow with {StepCount} steps", _steps.Count);

        try
        {
            // Execute all steps forward
            foreach (var (activity, compensation) in _steps)
            {
                try
                {
                    var result = await ExecuteActivityAsync(activity);

                    if (!result.Success)
                    {
                        throw new Exception($"Activity {activity.Name} failed: {result.Error}");
                    }

                    _completedSteps.Push(activity);

                    Logger.LogInformation(
                        "Completed activity: {Activity} ({Id})",
                        activity.Name,
                        activity.Id);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Activity failed: {Activity}", activity.Name);

                    // Compensation phase - undo completed steps in reverse order
                    await CompensateAsync();

                    throw;
                }
            }

            Logger.LogInformation("Saga workflow completed successfully");
            return new { status = "completed", stepsCompleted = _completedSteps.Count };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Saga workflow failed");
            return new { status = "failed", error = ex.Message };
        }
    }

    /// <summary>
    /// Execute compensation activities
    /// </summary>
    private async Task CompensateAsync()
    {
        Logger.LogWarning("Starting compensation phase");

        while (_completedSteps.Count > 0)
        {
            var activityToCompensate = _completedSteps.Pop();
            var originalStep = _steps.FirstOrDefault(s => s.activity.Id == activityToCompensate.Id);

            if (originalStep.compensation != null)
            {
                try
                {
                    var compensationResult = await ExecuteActivityAsync(originalStep.compensation);

                    Logger.LogInformation(
                        "Compensated activity: {Activity}",
                        originalStep.compensation.Name);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Compensation failed for activity: {Activity}", activityToCompensate.Name);
                    // Log for manual intervention
                }
            }
        }
    }

    private async Task<ActivityResult> ExecuteActivityAsync(Activity activity)
    {
        // Simulated activity execution
        await Task.Delay(10);

        return new ActivityResult
        {
            ActivityId = activity.Id,
            Success = true,
            Result = $"Completed {activity.Name}"
        };
    }
}

/// <summary>
/// Temporal workflow execution context
/// </summary>
public class WorkflowExecutionContext
{
    [JsonPropertyName("workflowId")]
    public string WorkflowId { get; set; } = string.Empty;

    [JsonPropertyName("runId")]
    public string RunId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("workflowType")]
    public string WorkflowType { get; set; } = string.Empty;

    [JsonPropertyName("input")]
    public Dictionary<string, object> Input { get; set; } = new();

    [JsonPropertyName("state")]
    public WorkflowState State { get; set; } = WorkflowState.Running;

    [JsonPropertyName("history")]
    public List<WorkflowHistoryEvent> History { get; set; } = new();

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Workflow history event
/// </summary>
public class WorkflowHistoryEvent
{
    [JsonPropertyName("eventId")]
    public long EventId { get; set; }

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty; // ActivityScheduled, ActivityCompleted, etc.

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("details")]
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// Temporal worker - executes workflows and activities
/// </summary>
public class TemporalWorker
{
    private readonly Dictionary<string, Type> _workflowTypes = new();
    private readonly Dictionary<string, Func<Dictionary<string, object>, Task<object?>>> _activityHandlers = new();
    private readonly ILogger<TemporalWorker> _logger;

    public TemporalWorker(ILogger<TemporalWorker> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register workflow type
    /// </summary>
    public void RegisterWorkflow<T>(string workflowType) where T : TemporalWorkflow
    {
        _workflowTypes[workflowType] = typeof(T);

        _logger.LogInformation(
            "Registered workflow type: {WorkflowType} -> {TypeName}",
            workflowType,
            typeof(T).Name);
    }

    /// <summary>
    /// Register activity handler
    /// </summary>
    public void RegisterActivity(
        string activityType,
        Func<Dictionary<string, object>, Task<object?>> handler)
    {
        _activityHandlers[activityType] = handler;

        _logger.LogInformation(
            "Registered activity type: {ActivityType}",
            activityType);
    }

    /// <summary>
    /// Start workflow execution
    /// </summary>
    public async Task<WorkflowExecutionContext> StartWorkflowAsync(
        string workflowId,
        string workflowType,
        Dictionary<string, object> input)
    {
        var context = new WorkflowExecutionContext
        {
            WorkflowId = workflowId,
            WorkflowType = workflowType,
            Input = input
        };

        if (!_workflowTypes.TryGetValue(workflowType, out var workflowTypeClass))
        {
            context.State = WorkflowState.Failed;
            context.Error = $"Workflow type {workflowType} not registered";

            _logger.LogError("Workflow type not found: {WorkflowType}", workflowType);
            return context;
        }

        try
        {
            // Record workflow started event
            context.History.Add(new WorkflowHistoryEvent
            {
                EventId = 1,
                EventType = "WorkflowExecutionStarted",
                Details = new() { ["workflowType"] = workflowType }
            });

            _logger.LogInformation(
                "Started workflow execution: {WorkflowId} ({WorkflowType})",
                workflowId,
                workflowType);
        }
        catch (Exception ex)
        {
            context.State = WorkflowState.Failed;
            context.Error = ex.Message;

            _logger.LogError(ex, "Failed to start workflow: {WorkflowId}", workflowId);
        }

        return context;
    }

    /// <summary>
    /// Execute activity
    /// </summary>
    public async Task<ActivityResult> ExecuteActivityAsync(
        WorkflowExecutionContext context,
        Activity activity)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ActivityResult { ActivityId = activity.Id };

        try
        {
            if (!_activityHandlers.TryGetValue(activity.Type, out var handler))
            {
                result.Success = false;
                result.Error = $"Activity type {activity.Type} not registered";

                _logger.LogError("Activity type not found: {ActivityType}", activity.Type);
                return result;
            }

            // Execute activity with retry logic
            var attemptCount = 0;
            Exception? lastException = null;

            while (attemptCount < activity.RetryPolicy.MaxAttempts)
            {
                try
                {
                    var activityResult = await handler(activity.Input);

                    stopwatch.Stop();

                    result.Success = true;
                    result.Result = activityResult;
                    result.RetryCount = attemptCount;

                    // Record activity completed event
                    context.History.Add(new WorkflowHistoryEvent
                    {
                        EventId = context.History.Count + 1,
                        EventType = "ActivityTaskCompleted",
                        Details = new()
                        {
                            ["activityId"] = activity.Id,
                            ["result"] = activityResult
                        }
                    });

                    _logger.LogInformation(
                        "Activity completed: {Activity} ({ActivityId}) in {Time}ms",
                        activity.Name,
                        activity.Id,
                        stopwatch.ElapsedMilliseconds);

                    return result;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attemptCount++;

                    if (attemptCount < activity.RetryPolicy.MaxAttempts)
                    {
                        var backoff = TimeSpan.FromSeconds(
                            Math.Min(
                                Math.Pow(activity.RetryPolicy.BackoffMultiplier, attemptCount - 1),
                                activity.RetryPolicy.MaxBackoff.TotalSeconds));

                        _logger.LogWarning(
                            "Activity failed (attempt {Attempt}/{Max}): {Activity}, retrying in {Backoff}ms",
                            attemptCount,
                            activity.RetryPolicy.MaxAttempts,
                            activity.Name,
                            backoff.TotalMilliseconds);

                        await Task.Delay(backoff);
                    }
                }
            }

            result.Success = false;
            result.Error = lastException?.Message ?? "Activity failed after all retries";
            result.RetryCount = attemptCount;

            // Record activity failed event
            context.History.Add(new WorkflowHistoryEvent
            {
                EventId = context.History.Count + 1,
                EventType = "ActivityTaskFailed",
                Details = new()
                {
                    ["activityId"] = activity.Id,
                    ["error"] = result.Error,
                    ["attempts"] = attemptCount
                }
            });

            _logger.LogError(
                "Activity failed after {Attempts} attempts: {Activity}",
                attemptCount,
                activity.Name);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            result.Success = false;
            result.Error = ex.Message;

            _logger.LogError(ex, "Unexpected error executing activity: {Activity}", activity.Name);
        }

        return result;
    }

    /// <summary>
    /// Get workflow execution status
    /// </summary>
    public WorkflowExecutionContext? GetWorkflowStatus(string workflowId)
    {
        // In real implementation, this would query persistent storage
        return null;
    }
}

/// <summary>
/// Workflow state recovery - handles idempotent replay
/// </summary>
public class WorkflowStateRecovery
{
    private readonly ILogger<WorkflowStateRecovery> _logger;

    public WorkflowStateRecovery(ILogger<WorkflowStateRecovery> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Replay workflow from history
    /// Deterministic execution for recovery
    /// </summary>
    public async Task<WorkflowExecutionContext> ReplayWorkflowAsync(
        WorkflowExecutionContext context,
        TemporalWorker worker)
    {
        _logger.LogInformation(
            "Replaying workflow from history: {WorkflowId} with {EventCount} events",
            context.WorkflowId,
            context.History.Count);

        // Replay all history events in order
        foreach (var historyEvent in context.History)
        {
            switch (historyEvent.EventType)
            {
                case "WorkflowExecutionStarted":
                    _logger.LogDebug("Replayed WorkflowExecutionStarted");
                    break;

                case "ActivityTaskCompleted":
                    _logger.LogDebug("Replayed ActivityTaskCompleted: {ActivityId}",
                        historyEvent.Details.GetValueOrDefault("activityId"));
                    break;

                case "ActivityTaskFailed":
                    _logger.LogDebug("Replayed ActivityTaskFailed: {ActivityId}",
                        historyEvent.Details.GetValueOrDefault("activityId"));
                    break;
            }
        }

        return context;
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class TemporalExtensions
{
    public static IServiceCollection AddTemporalWorkflows(this IServiceCollection services)
    {
        services.AddSingleton<TemporalWorker>();
        services.AddSingleton<WorkflowStateRecovery>();
        return services;
    }
}
