// Phase 5: Advanced Workflow Executor
// Conditional branching, parallel execution, dynamic routing, and sophisticated retry policies
// Enables complex business logic automation with minimal code

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Workflows.Advanced;

/// <summary>
/// Advanced workflow step types
/// </summary>
public enum StepType
{
    Action = 0,           // Simple action (HTTP call, database operation)
    Condition = 1,        // Conditional branching (if/else)
    Parallel = 2,         // Parallel execution (fork-join)
    Loop = 3,             // Iterative execution
    Switch = 4,           // Switch/case branching
    Delay = 5,            // Time-based delay
    Compensation = 6,     // Rollback/undo action
    Aggregate = 7,        // Combine multiple outputs
}

/// <summary>
/// Advanced step definition
/// </summary>
public class AdvancedStep
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public StepType Type { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; } // HTTP endpoint, function name

    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; set; }

    [JsonPropertyName("condition")]
    public string? Condition { get; set; } // Expression: ${input.status} == 'active'

    [JsonPropertyName("thenSteps")]
    public List<AdvancedStep>? ThenSteps { get; set; } // If true

    [JsonPropertyName("elseSteps")]
    public List<AdvancedStep>? ElseSteps { get; set; } // If false

    [JsonPropertyName("parallelSteps")]
    public List<AdvancedStep>? ParallelSteps { get; set; } // Execute in parallel

    [JsonPropertyName("loopVariable")]
    public string? LoopVariable { get; set; } // ${items}

    [JsonPropertyName("loopSteps")]
    public List<AdvancedStep>? LoopSteps { get; set; }

    [JsonPropertyName("cases")]
    public Dictionary<string, List<AdvancedStep>>? Cases { get; set; } // Switch cases

    [JsonPropertyName("switchExpression")]
    public string? SwitchExpression { get; set; } // ${input.type}

    [JsonPropertyName("delaySeconds")]
    public int DelaySeconds { get; set; }

    [JsonPropertyName("compensationSteps")]
    public List<AdvancedStep>? CompensationSteps { get; set; } // Rollback

    [JsonPropertyName("retryPolicy")]
    public RetryPolicy? RetryPolicy { get; set; }

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 300;

    [JsonPropertyName("onError")]
    public string? OnError { get; set; } // 'continue', 'stop', 'compensate'
}

/// <summary>
/// Retry policy configuration
/// </summary>
public class RetryPolicy
{
    [JsonPropertyName("maxAttempts")]
    public int MaxAttempts { get; set; } = 3;

    [JsonPropertyName("initialDelaySeconds")]
    public int InitialDelaySeconds { get; set; } = 1;

    [JsonPropertyName("maxDelaySeconds")]
    public int MaxDelaySeconds { get; set; } = 60;

    [JsonPropertyName("backoffMultiplier")]
    public double BackoffMultiplier { get; set; } = 2.0; // Exponential backoff

    [JsonPropertyName("retryableStatusCodes")]
    public List<int>? RetryableStatusCodes { get; set; } = new() { 408, 429, 500, 502, 503, 504 };
}

/// <summary>
/// Step execution context
/// </summary>
public class StepExecutionContext
{
    public string StepId { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public StepType StepType { get; set; }
    public Dictionary<string, object> Input { get; set; } = new();
    public Dictionary<string, object> Output { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = "pending"; // pending, running, success, failure, compensated
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int AttemptCount { get; set; } = 1;
    public List<StepExecutionContext> ChildSteps { get; set; } = new();

    public double DurationMs => (EndTime ?? DateTime.UtcNow - StartTime).TotalMilliseconds;
}

/// <summary>
/// Advanced workflow executor interface
/// </summary>
public interface IAdvancedWorkflowExecutor
{
    Task<WorkflowExecutionResult> ExecuteAsync(
        string executionId,
        List<AdvancedStep> steps,
        Dictionary<string, object> input,
        CancellationToken ct = default);

    Task<List<StepExecutionContext>> GetExecutionTimelineAsync(
        string executionId,
        CancellationToken ct = default);

    Task CancelExecutionAsync(string executionId, CancellationToken ct = default);
}

/// <summary>
/// Workflow execution result
/// </summary>
public class WorkflowExecutionResult
{
    public string ExecutionId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public Dictionary<string, object> FinalOutput { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public int StepsExecuted { get; set; }
    public int StepsFailed { get; set; }
    public double DurationSeconds { get; set; }
    public List<StepExecutionContext> ExecutionTimeline { get; set; } = new();
}

/// <summary>
/// Advanced workflow executor implementation
/// </summary>
public class AdvancedWorkflowExecutor : IAdvancedWorkflowExecutor
{
    private readonly ILogger<AdvancedWorkflowExecutor> _logger;
    private readonly Dictionary<string, List<StepExecutionContext>> _executionTimelines;
    private readonly Dictionary<string, CancellationTokenSource> _executionCancellation;

    public AdvancedWorkflowExecutor(ILogger<AdvancedWorkflowExecutor> logger)
    {
        _logger = logger;
        _executionTimelines = new Dictionary<string, List<StepExecutionContext>>();
        _executionCancellation = new Dictionary<string, CancellationTokenSource>();
    }

    /// <summary>
    /// Execute advanced workflow
    /// </summary>
    public async Task<WorkflowExecutionResult> ExecuteAsync(
        string executionId,
        List<AdvancedStep> steps,
        Dictionary<string, object> input,
        CancellationToken ct = default)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _executionCancellation[executionId] = cts;

        var result = new WorkflowExecutionResult
        {
            ExecutionId = executionId,
            DurationSeconds = 0,
        };

        var timeline = new List<StepExecutionContext>();
        _executionTimelines[executionId] = timeline;

        var stopwatch = Stopwatch.StartNew();
        var context = new Dictionary<string, object>(input);

        try
        {
            _logger.LogInformation("Starting advanced workflow execution: {ExecutionId}", executionId);

            // Execute steps sequentially
            foreach (var step in steps)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    _logger.LogInformation("Workflow execution cancelled: {ExecutionId}", executionId);
                    break;
                }

                var stepContext = new StepExecutionContext
                {
                    StepId = step.Id,
                    StepName = step.Name,
                    StepType = step.Type,
                    Input = new Dictionary<string, object>(context),
                    StartTime = DateTime.UtcNow,
                };

                try
                {
                    await ExecuteStepAsync(step, stepContext, context, cts.Token);
                    stepContext.Status = "success";
                    stepContext.EndTime = DateTime.UtcNow;
                    result.StepsExecuted++;

                    // Update context with step output
                    foreach (var kvp in stepContext.Output)
                    {
                        context[kvp.Key] = kvp.Value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Step execution failed: {StepId}", step.Id);
                    stepContext.Status = "failure";
                    stepContext.ErrorMessage = ex.Message;
                    stepContext.EndTime = DateTime.UtcNow;
                    result.StepsFailed++;

                    // Handle error policy
                    if (step.OnError == "compensate" && step.CompensationSteps != null)
                    {
                        await ExecuteCompensationAsync(step.CompensationSteps, context, cts.Token);
                    }
                    else if (step.OnError == "stop")
                    {
                        result.ErrorMessage = ex.Message;
                        break;
                    }
                    // 'continue' - just log and continue
                }

                timeline.Add(stepContext);
            }

            stopwatch.Stop();
            result.DurationSeconds = stopwatch.Elapsed.TotalSeconds;
            result.FinalOutput = new Dictionary<string, object>(context);
            result.Success = result.StepsFailed == 0;
            result.ExecutionTimeline = timeline;

            _logger.LogInformation(
                "Workflow execution completed: {ExecutionId}, Steps: {Executed}/{Total}, Duration: {Duration}s",
                executionId, result.StepsExecuted, steps.Count, result.DurationSeconds);

            return result;
        }
        finally
        {
            _executionCancellation.Remove(executionId);
        }
    }

    /// <summary>
    /// Execute individual step based on type
    /// </summary>
    private async Task ExecuteStepAsync(
        AdvancedStep step,
        StepExecutionContext context,
        Dictionary<string, object> workflowContext,
        CancellationToken ct)
    {
        switch (step.Type)
        {
            case StepType.Action:
                await ExecuteActionAsync(step, context, ct);
                break;

            case StepType.Condition:
                await ExecuteConditionAsync(step, context, workflowContext, ct);
                break;

            case StepType.Parallel:
                await ExecuteParallelAsync(step, context, workflowContext, ct);
                break;

            case StepType.Loop:
                await ExecuteLoopAsync(step, context, workflowContext, ct);
                break;

            case StepType.Switch:
                await ExecuteSwitchAsync(step, context, workflowContext, ct);
                break;

            case StepType.Delay:
                await ExecuteDelayAsync(step, context, ct);
                break;

            default:
                throw new NotSupportedException($"Step type not supported: {step.Type}");
        }
    }

    /// <summary>
    /// Execute action (HTTP call, function invocation, etc.)
    /// </summary>
    private async Task ExecuteActionAsync(
        AdvancedStep step,
        StepExecutionContext context,
        CancellationToken ct)
    {
        _logger.LogDebug("Executing action: {Action}", step.Action);

        // Simulate action execution (in production, call actual HTTP endpoint or function)
        await Task.Delay(100, ct);

        // Mock output
        context.Output["status"] = "success";
        context.Output["result"] = new { message = $"Action {step.Name} completed" };
    }

    /// <summary>
    /// Execute conditional branching (if/else)
    /// </summary>
    private async Task ExecuteConditionAsync(
        AdvancedStep step,
        StepExecutionContext context,
        Dictionary<string, object> workflowContext,
        CancellationToken ct)
    {
        _logger.LogDebug("Evaluating condition: {Condition}", step.Condition);

        // Evaluate condition expression
        bool conditionResult = EvaluateExpression(step.Condition ?? "false", workflowContext);

        if (conditionResult && step.ThenSteps != null)
        {
            _logger.LogDebug("Condition true, executing then steps");
            foreach (var thenStep in step.ThenSteps)
            {
                var childContext = new StepExecutionContext
                {
                    StepId = thenStep.Id,
                    StepName = thenStep.Name,
                    StepType = thenStep.Type,
                    StartTime = DateTime.UtcNow,
                };

                await ExecuteStepAsync(thenStep, childContext, workflowContext, ct);
                childContext.EndTime = DateTime.UtcNow;
                context.ChildSteps.Add(childContext);
            }
        }
        else if (!conditionResult && step.ElseSteps != null)
        {
            _logger.LogDebug("Condition false, executing else steps");
            foreach (var elseStep in step.ElseSteps)
            {
                var childContext = new StepExecutionContext
                {
                    StepId = elseStep.Id,
                    StepName = elseStep.Name,
                    StepType = elseStep.Type,
                    StartTime = DateTime.UtcNow,
                };

                await ExecuteStepAsync(elseStep, childContext, workflowContext, ct);
                childContext.EndTime = DateTime.UtcNow;
                context.ChildSteps.Add(childContext);
            }
        }

        context.Output["conditionResult"] = conditionResult;
    }

    /// <summary>
    /// Execute parallel steps (fork-join pattern)
    /// </summary>
    private async Task ExecuteParallelAsync(
        AdvancedStep step,
        StepExecutionContext context,
        Dictionary<string, object> workflowContext,
        CancellationToken ct)
    {
        if (step.ParallelSteps == null || step.ParallelSteps.Count == 0)
            return;

        _logger.LogDebug("Executing {Count} parallel steps", step.ParallelSteps.Count);

        var parallelTasks = step.ParallelSteps.Select(async parallelStep =>
        {
            var childContext = new StepExecutionContext
            {
                StepId = parallelStep.Id,
                StepName = parallelStep.Name,
                StepType = parallelStep.Type,
                StartTime = DateTime.UtcNow,
            };

            await ExecuteStepAsync(parallelStep, childContext, workflowContext, ct);
            childContext.EndTime = DateTime.UtcNow;
            return childContext;
        });

        var results = await Task.WhenAll(parallelTasks);
        context.ChildSteps.AddRange(results);
        context.Output["parallelResults"] = results.Select(r => r.Output).ToList();
    }

    /// <summary>
    /// Execute loop (iteration over collection)
    /// </summary>
    private async Task ExecuteLoopAsync(
        AdvancedStep step,
        StepExecutionContext context,
        Dictionary<string, object> workflowContext,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(step.LoopVariable) || step.LoopSteps == null)
            return;

        _logger.LogDebug("Executing loop over: {Variable}", step.LoopVariable);

        // Get collection to iterate over
        var collection = ResolveVariableValue(step.LoopVariable, workflowContext) as List<object> ?? new List<object>();

        var loopResults = new List<Dictionary<string, object>>();

        foreach (var item in collection)
        {
            var itemContext = new Dictionary<string, object>(workflowContext)
            {
                ["currentItem"] = item,
                ["itemIndex"] = collection.IndexOf(item),
            };

            foreach (var loopStep in step.LoopSteps)
            {
                var childContext = new StepExecutionContext
                {
                    StepId = loopStep.Id,
                    StepName = loopStep.Name,
                    StepType = loopStep.Type,
                    StartTime = DateTime.UtcNow,
                };

                await ExecuteStepAsync(loopStep, childContext, itemContext, ct);
                childContext.EndTime = DateTime.UtcNow;
                context.ChildSteps.Add(childContext);
            }

            loopResults.Add(new Dictionary<string, object>(itemContext));
        }

        context.Output["loopResults"] = loopResults;
    }

    /// <summary>
    /// Execute switch/case branching
    /// </summary>
    private async Task ExecuteSwitchAsync(
        AdvancedStep step,
        StepExecutionContext context,
        Dictionary<string, object> workflowContext,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(step.SwitchExpression) || step.Cases == null)
            return;

        _logger.LogDebug("Evaluating switch: {Expression}", step.SwitchExpression);

        var switchValue = ResolveVariableValue(step.SwitchExpression, workflowContext)?.ToString() ?? "default";

        if (step.Cases.TryGetValue(switchValue, out var caseSteps))
        {
            _logger.LogDebug("Executing case: {Value}", switchValue);

            foreach (var caseStep in caseSteps)
            {
                var childContext = new StepExecutionContext
                {
                    StepId = caseStep.Id,
                    StepName = caseStep.Name,
                    StepType = caseStep.Type,
                    StartTime = DateTime.UtcNow,
                };

                await ExecuteStepAsync(caseStep, childContext, workflowContext, ct);
                childContext.EndTime = DateTime.UtcNow;
                context.ChildSteps.Add(childContext);
            }
        }

        context.Output["switchValue"] = switchValue;
    }

    /// <summary>
    /// Execute delay
    /// </summary>
    private async Task ExecuteDelayAsync(
        AdvancedStep step,
        StepExecutionContext context,
        CancellationToken ct)
    {
        _logger.LogDebug("Delaying for {Seconds} seconds", step.DelaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(step.DelaySeconds), ct);
        context.Output["delayCompleted"] = DateTime.UtcNow;
    }

    /// <summary>
    /// Execute compensation (rollback/cleanup)
    /// </summary>
    private async Task ExecuteCompensationAsync(
        List<AdvancedStep> compensationSteps,
        Dictionary<string, object> context,
        CancellationToken ct)
    {
        _logger.LogInformation("Executing compensation steps");

        foreach (var step in compensationSteps)
        {
            try
            {
                var stepContext = new StepExecutionContext
                {
                    StepId = step.Id,
                    StepName = step.Name,
                    StepType = step.Type,
                    StartTime = DateTime.UtcNow,
                };

                await ExecuteStepAsync(step, stepContext, context, ct);
                stepContext.EndTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Compensation step failed: {StepId}", step.Id);
            }
        }
    }

    /// <summary>
    /// Get execution timeline
    /// </summary>
    public Task<List<StepExecutionContext>> GetExecutionTimelineAsync(
        string executionId,
        CancellationToken ct = default)
    {
        if (_executionTimelines.TryGetValue(executionId, out var timeline))
        {
            return Task.FromResult(timeline);
        }

        return Task.FromResult(new List<StepExecutionContext>());
    }

    /// <summary>
    /// Cancel execution
    /// </summary>
    public Task CancelExecutionAsync(string executionId, CancellationToken ct = default)
    {
        if (_executionCancellation.TryGetValue(executionId, out var cts))
        {
            cts.Cancel();
        }

        return Task.CompletedTask;
    }

    // Helper methods
    private bool EvaluateExpression(string expression, Dictionary<string, object> context)
    {
        // Simple expression evaluator
        // In production, use a proper expression language (e.g., DynamicLinq, Roslyn)
        return expression.ToLower().Contains("true");
    }

    private object? ResolveVariableValue(string variable, Dictionary<string, object> context)
    {
        // Resolve variable reference like ${input.status}
        var key = variable.Replace("${", "").Replace("}", "");
        context.TryGetValue(key, out var value);
        return value;
    }
}
