using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Loco.Core.Workflow;

namespace Loco.Core.Debugging;

/// <summary>
/// Visual workflow debugger with breakpoints, step execution, and variable inspection
/// Based on 2024/2025 research:
/// - FlowWright Visual Debugger for enterprise workflows
/// - VS Code integrated debugging with AI assistance (30% reduction in debug time)
/// - BrowserStack visual debugging with screenshots and video
/// - Make.com/Node-RED flowchart-like canvas visualization
/// - Sentry AI-powered error tracking and performance monitoring
/// Solves Issue #26: Debugging tools for workflow development
/// </summary>
public class VisualWorkflowDebugger
{
    private readonly ILogger<VisualWorkflowDebugger> _logger;
    private readonly ConcurrentDictionary<string, DebugSession> _sessions;
    private readonly ConcurrentDictionary<string, List<Breakpoint>> _breakpoints;
    private readonly ConcurrentDictionary<string, WorkflowExecutionTrace> _traces;

    public VisualWorkflowDebugger(ILogger<VisualWorkflowDebugger> logger)
    {
        _logger = logger;
        _sessions = new ConcurrentDictionary<string, DebugSession>();
        _breakpoints = new ConcurrentDictionary<string, List<Breakpoint>>();
        _traces = new ConcurrentDictionary<string, WorkflowExecutionTrace>();
    }

    /// <summary>
    /// Start a new debug session for a workflow
    /// </summary>
    public DebugSession StartDebugSession(string workflowId, WorkflowDefinition workflow)
    {
        var session = new DebugSession
        {
            SessionId = Guid.NewGuid().ToString(),
            WorkflowId = workflowId,
            WorkflowName = workflow.Name,
            Status = DebugStatus.Ready,
            StartTime = DateTime.UtcNow
        };

        _sessions[session.SessionId] = session;
        _logger.LogInformation("Started debug session {SessionId} for workflow {WorkflowId}",
            session.SessionId, workflowId);

        return session;
    }

    /// <summary>
    /// Set a breakpoint at a specific action
    /// </summary>
    public void SetBreakpoint(string sessionId, string actionId, BreakpointCondition? condition = null)
    {
        var breakpoint = new Breakpoint
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            ActionId = actionId,
            Condition = condition,
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

        var breakpointList = _breakpoints.GetOrAdd(sessionId, _ => new List<Breakpoint>());
        breakpointList.Add(breakpoint);

        _logger.LogInformation("Breakpoint set at action {ActionId} in session {SessionId}",
            actionId, sessionId);
    }

    /// <summary>
    /// Record execution step for visual debugging
    /// </summary>
    public async Task RecordExecutionStepAsync(
        string sessionId,
        string actionId,
        string actionType,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        var trace = _traces.GetOrAdd(sessionId, _ => new WorkflowExecutionTrace
        {
            SessionId = sessionId,
            StartTime = DateTime.UtcNow
        });

        var step = new ExecutionStep
        {
            StepNumber = trace.Steps.Count + 1,
            ActionId = actionId,
            ActionType = actionType,
            Timestamp = DateTime.UtcNow,
            Variables = new Dictionary<string, object>(variables),
            Status = ExecutionStepStatus.InProgress
        };

        trace.Steps.Add(step);

        // Check for breakpoints
        if (_breakpoints.TryGetValue(sessionId, out var breakpoints))
        {
            var breakpoint = breakpoints.FirstOrDefault(bp =>
                bp.Enabled && bp.ActionId == actionId);

            if (breakpoint != null)
            {
                // Evaluate condition if present
                bool shouldBreak = true;
                if (breakpoint.Condition != null)
                {
                    shouldBreak = await EvaluateBreakpointConditionAsync(
                        breakpoint.Condition, variables, cancellationToken);
                }

                if (shouldBreak)
                {
                    step.HitBreakpoint = true;
                    step.BreakpointId = breakpoint.Id;

                    if (_sessions.TryGetValue(sessionId, out var session))
                    {
                        session.Status = DebugStatus.BreakpointHit;
                        session.CurrentActionId = actionId;
                        session.Variables = new Dictionary<string, object>(variables);
                    }

                    _logger.LogInformation("Breakpoint hit at action {ActionId} in session {SessionId}",
                        actionId, sessionId);
                }
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Complete execution step with result
    /// </summary>
    public void CompleteExecutionStep(
        string sessionId,
        string actionId,
        bool success,
        string? errorMessage = null,
        Dictionary<string, object>? outputData = null)
    {
        if (_traces.TryGetValue(sessionId, out var trace))
        {
            var step = trace.Steps.LastOrDefault(s => s.ActionId == actionId);
            if (step != null)
            {
                step.Status = success ? ExecutionStepStatus.Completed : ExecutionStepStatus.Failed;
                step.ErrorMessage = errorMessage;
                step.OutputData = outputData;
                step.Duration = DateTime.UtcNow - step.Timestamp;

                _logger.LogInformation("Execution step {ActionId} completed with status {Status}",
                    actionId, step.Status);
            }
        }
    }

    /// <summary>
    /// Continue execution from breakpoint (step over)
    /// </summary>
    public void StepOver(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.Status = DebugStatus.StepOver;
            session.StepMode = StepMode.Over;
            _logger.LogInformation("Step over in session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Step into action (detailed execution)
    /// </summary>
    public void StepInto(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.Status = DebugStatus.StepInto;
            session.StepMode = StepMode.Into;
            _logger.LogInformation("Step into in session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Continue execution until next breakpoint
    /// </summary>
    public void Continue(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.Status = DebugStatus.Running;
            session.StepMode = StepMode.None;
            _logger.LogInformation("Continue execution in session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Get current variable values for inspection
    /// </summary>
    public Dictionary<string, object> InspectVariables(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            return new Dictionary<string, object>(session.Variables);
        }
        return new Dictionary<string, object>();
    }

    /// <summary>
    /// Get execution trace for visualization
    /// Similar to FlowWright's visual debugger with flowchart representation
    /// </summary>
    public WorkflowExecutionTrace GetExecutionTrace(string sessionId)
    {
        if (_traces.TryGetValue(sessionId, out var trace))
        {
            return trace;
        }
        return new WorkflowExecutionTrace { SessionId = sessionId };
    }

    /// <summary>
    /// Generate visual flowchart representation (ASCII art for CLI)
    /// Based on Make.com/Node-RED flowchart-like canvas
    /// </summary>
    public string GenerateFlowchartVisualization(string sessionId)
    {
        if (!_traces.TryGetValue(sessionId, out var trace))
        {
            return "No execution trace found";
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== Workflow Execution Flowchart ===");
        sb.AppendLine();

        for (int i = 0; i < trace.Steps.Count; i++)
        {
            var step = trace.Steps[i];
            var icon = step.Status switch
            {
                ExecutionStepStatus.Completed => "✓",
                ExecutionStepStatus.Failed => "✗",
                ExecutionStepStatus.InProgress => "⋯",
                ExecutionStepStatus.Skipped => "⊝",
                _ => "?"
            };

            var breakpointMarker = step.HitBreakpoint ? " 🔴" : "";
            var durationText = step.Duration.HasValue ? $" ({step.Duration.Value.TotalMilliseconds:F0}ms)" : "";

            sb.AppendLine($"  [{step.StepNumber}] {icon} {step.ActionType} ({step.ActionId}){breakpointMarker}{durationText}");

            if (step.ErrorMessage != null)
            {
                sb.AppendLine($"      ⚠️  Error: {step.ErrorMessage}");
            }

            if (i < trace.Steps.Count - 1)
            {
                sb.AppendLine("      ↓");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Total Steps: {trace.Steps.Count}");
        sb.AppendLine($"Completed: {trace.Steps.Count(s => s.Status == ExecutionStepStatus.Completed)}");
        sb.AppendLine($"Failed: {trace.Steps.Count(s => s.Status == ExecutionStepStatus.Failed)}");
        sb.AppendLine($"Duration: {(DateTime.UtcNow - trace.StartTime).TotalSeconds:F2}s");

        return sb.ToString();
    }

    /// <summary>
    /// Export execution trace to JSON for external visualization tools
    /// Compatible with Sentry, BrowserStack-style analysis
    /// </summary>
    public string ExportTraceAsJson(string sessionId)
    {
        if (_traces.TryGetValue(sessionId, out var trace))
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Serialize(trace, options);
        }
        return "{}";
    }

    /// <summary>
    /// Get performance metrics for the workflow execution
    /// Based on Sentry AI-powered performance monitoring
    /// </summary>
    public PerformanceMetrics GetPerformanceMetrics(string sessionId)
    {
        if (!_traces.TryGetValue(sessionId, out var trace))
        {
            return new PerformanceMetrics();
        }

        var completedSteps = trace.Steps.Where(s => s.Duration.HasValue).ToList();
        var durations = completedSteps.Select(s => s.Duration!.Value.TotalMilliseconds).ToList();

        return new PerformanceMetrics
        {
            TotalSteps = trace.Steps.Count,
            CompletedSteps = trace.Steps.Count(s => s.Status == ExecutionStepStatus.Completed),
            FailedSteps = trace.Steps.Count(s => s.Status == ExecutionStepStatus.Failed),
            TotalDuration = (DateTime.UtcNow - trace.StartTime).TotalMilliseconds,
            AverageStepDuration = durations.Any() ? durations.Average() : 0,
            MinStepDuration = durations.Any() ? durations.Min() : 0,
            MaxStepDuration = durations.Any() ? durations.Max() : 0,
            Bottlenecks = IdentifyBottlenecks(completedSteps)
        };
    }

    /// <summary>
    /// Identify performance bottlenecks (steps taking >2x average time)
    /// AI-assisted performance analysis pattern from 2024 research
    /// </summary>
    private List<PerformanceBottleneck> IdentifyBottlenecks(List<ExecutionStep> steps)
    {
        var bottlenecks = new List<PerformanceBottleneck>();

        if (steps.Count == 0) return bottlenecks;

        var avgDuration = steps.Average(s => s.Duration!.Value.TotalMilliseconds);
        var threshold = avgDuration * 2; // Bottleneck = 2x average

        foreach (var step in steps.Where(s => s.Duration!.Value.TotalMilliseconds > threshold))
        {
            bottlenecks.Add(new PerformanceBottleneck
            {
                ActionId = step.ActionId,
                ActionType = step.ActionType,
                Duration = step.Duration!.Value.TotalMilliseconds,
                AverageDuration = avgDuration,
                SlowdownFactor = step.Duration!.Value.TotalMilliseconds / avgDuration,
                Recommendation = GenerateBottleneckRecommendation(step)
            });
        }

        return bottlenecks;
    }

    private string GenerateBottleneckRecommendation(ExecutionStep step)
    {
        // AI-assisted recommendations based on action type
        return step.ActionType switch
        {
            "http_request" => "Consider caching HTTP responses or using batch requests",
            "file_operation" => "Consider using async file I/O or parallel processing",
            "notification" => "Check if notification service is experiencing delays",
            _ => "Consider optimizing this action or running it in parallel"
        };
    }

    private async Task<bool> EvaluateBreakpointConditionAsync(
        BreakpointCondition condition,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async condition evaluation

        // Simple variable-based condition evaluation
        if (!string.IsNullOrEmpty(condition.VariableName))
        {
            if (variables.TryGetValue(condition.VariableName, out var value))
            {
                // Simple equality check for now
                return value?.ToString() == condition.ExpectedValue;
            }
            return false;
        }

        return true; // No condition specified
    }

    /// <summary>
    /// Stop debug session and clean up
    /// </summary>
    public void StopDebugSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.Status = DebugStatus.Stopped;
            session.EndTime = DateTime.UtcNow;
            _logger.LogInformation("Debug session {SessionId} stopped", sessionId);
        }

        _breakpoints.TryRemove(sessionId, out _);
    }
}

public class DebugSession
{
    public string SessionId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public DebugStatus Status { get; set; }
    public StepMode StepMode { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string CurrentActionId { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
}

public class Breakpoint
{
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
    public BreakpointCondition? Condition { get; set; }
    public bool Enabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public int HitCount { get; set; }
}

public class BreakpointCondition
{
    public string VariableName { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string Operator { get; set; } = "equals"; // equals, contains, greater_than, etc.
}

public class WorkflowExecutionTrace
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public List<ExecutionStep> Steps { get; set; } = new();
}

public class ExecutionStep
{
    public int StepNumber { get; set; }
    public string ActionId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public TimeSpan? Duration { get; set; }
    public ExecutionStepStatus Status { get; set; }
    public Dictionary<string, object> Variables { get; set; } = new();
    public Dictionary<string, object>? OutputData { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HitBreakpoint { get; set; }
    public string? BreakpointId { get; set; }
}

public class PerformanceMetrics
{
    public int TotalSteps { get; set; }
    public int CompletedSteps { get; set; }
    public int FailedSteps { get; set; }
    public double TotalDuration { get; set; }
    public double AverageStepDuration { get; set; }
    public double MinStepDuration { get; set; }
    public double MaxStepDuration { get; set; }
    public List<PerformanceBottleneck> Bottlenecks { get; set; } = new();
}

public class PerformanceBottleneck
{
    public string ActionId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public double Duration { get; set; }
    public double AverageDuration { get; set; }
    public double SlowdownFactor { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}

public enum DebugStatus
{
    Ready,
    Running,
    BreakpointHit,
    StepOver,
    StepInto,
    Paused,
    Stopped,
    Error
}

public enum StepMode
{
    None,
    Over,
    Into,
    Out
}

public enum ExecutionStepStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}
