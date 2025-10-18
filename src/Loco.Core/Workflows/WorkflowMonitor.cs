using System.Diagnostics;
using System.Text;
using System.Collections.Concurrent;

namespace Loco.Core.Workflows;

/// <summary>
/// Execution state of a workflow.
/// </summary>
public enum ExecutionState
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
    Paused
}

/// <summary>
/// Real-time execution information.
/// </summary>
public class ExecutionInfo
{
    public string ExecutionId { get; set; } = "";
    public string WorkflowId { get; set; } = "";
    public string WorkflowName { get; set; } = "";
    public ExecutionState State { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Elapsed => EndTime.HasValue ? EndTime.Value - StartTime : DateTime.UtcNow - StartTime;
    public int TotalSteps { get; set; }
    public int CompletedSteps { get; set; }
    public int FailedSteps { get; set; }
    public string? CurrentStepId { get; set; }
    public string? CurrentStepName { get; set; }
    public double ProgressPercentage => TotalSteps > 0 ? (CompletedSteps / (double)TotalSteps) * 100 : 0;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Information about a step execution (for monitoring).
/// </summary>
public class MonitoredStepInfo
{
    public string StepId { get; set; } = "";
    public string StepName { get; set; } = "";
    public ExecutionState State { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : DateTime.UtcNow - StartTime;
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> Outputs { get; set; } = new();
}

/// <summary>
/// Monitors workflow executions in real-time.
/// </summary>
public class WorkflowMonitor
{
    private readonly ConcurrentDictionary<string, ExecutionInfo> _activeExecutions = new();
    private readonly ConcurrentDictionary<string, List<MonitoredStepInfo>> _stepExecutions = new();
    private readonly List<ExecutionInfo> _executionHistory = new();
    private readonly int _maxHistorySize;
    private readonly object _historyLock = new();

    public WorkflowMonitor(int maxHistorySize = 1000)
    {
        _maxHistorySize = maxHistorySize;
    }

    /// <summary>
    /// Starts monitoring a new workflow execution.
    /// </summary>
    public string StartExecution(string workflowId, string workflowName, int totalSteps)
    {
        var executionId = Guid.NewGuid().ToString("N");
        var execution = new ExecutionInfo
        {
            ExecutionId = executionId,
            WorkflowId = workflowId,
            WorkflowName = workflowName,
            State = ExecutionState.Running,
            StartTime = DateTime.UtcNow,
            TotalSteps = totalSteps
        };

        _activeExecutions[executionId] = execution;
        _stepExecutions[executionId] = new List<MonitoredStepInfo>();

        return executionId;
    }

    /// <summary>
    /// Updates the current step being executed.
    /// </summary>
    public void UpdateCurrentStep(string executionId, string stepId, string stepName)
    {
        if (_activeExecutions.TryGetValue(executionId, out var execution))
        {
            execution.CurrentStepId = stepId;
            execution.CurrentStepName = stepName;

            // Add step execution info
            var stepInfo = new MonitoredStepInfo
            {
                StepId = stepId,
                StepName = stepName,
                State = ExecutionState.Running,
                StartTime = DateTime.UtcNow
            };

            _stepExecutions[executionId].Add(stepInfo);
        }
    }

    /// <summary>
    /// Marks a step as completed.
    /// </summary>
    public void CompleteStep(string executionId, string stepId, Dictionary<string, string>? outputs = null)
    {
        if (_activeExecutions.TryGetValue(executionId, out var execution))
        {
            execution.CompletedSteps++;

            // Update step info
            if (_stepExecutions.TryGetValue(executionId, out var steps))
            {
                var stepInfo = steps.LastOrDefault(s => s.StepId == stepId);
                if (stepInfo != null)
                {
                    stepInfo.State = ExecutionState.Completed;
                    stepInfo.EndTime = DateTime.UtcNow;
                    if (outputs != null)
                    {
                        stepInfo.Outputs = outputs;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Marks a step as failed.
    /// </summary>
    public void FailStep(string executionId, string stepId, string errorMessage, int retryCount = 0)
    {
        if (_activeExecutions.TryGetValue(executionId, out var execution))
        {
            execution.FailedSteps++;
            execution.Errors.Add($"Step {stepId}: {errorMessage}");

            // Update step info
            if (_stepExecutions.TryGetValue(executionId, out var steps))
            {
                var stepInfo = steps.LastOrDefault(s => s.StepId == stepId);
                if (stepInfo != null)
                {
                    stepInfo.State = ExecutionState.Failed;
                    stepInfo.EndTime = DateTime.UtcNow;
                    stepInfo.ErrorMessage = errorMessage;
                    stepInfo.RetryCount = retryCount;
                }
            }
        }
    }

    /// <summary>
    /// Completes a workflow execution.
    /// </summary>
    public void CompleteExecution(string executionId, bool success)
    {
        if (_activeExecutions.TryRemove(executionId, out var execution))
        {
            execution.EndTime = DateTime.UtcNow;
            execution.State = success ? ExecutionState.Completed : ExecutionState.Failed;

            // Add to history
            AddToHistory(execution);
        }
    }

    /// <summary>
    /// Cancels a workflow execution.
    /// </summary>
    public void CancelExecution(string executionId)
    {
        if (_activeExecutions.TryRemove(executionId, out var execution))
        {
            execution.EndTime = DateTime.UtcNow;
            execution.State = ExecutionState.Cancelled;

            AddToHistory(execution);
        }
    }

    /// <summary>
    /// Gets information about an active execution.
    /// </summary>
    public ExecutionInfo? GetExecutionInfo(string executionId)
    {
        return _activeExecutions.TryGetValue(executionId, out var execution) ? execution : null;
    }

    /// <summary>
    /// Gets all active executions.
    /// </summary>
    public List<ExecutionInfo> GetActiveExecutions()
    {
        return _activeExecutions.Values.ToList();
    }

    /// <summary>
    /// Gets step execution information.
    /// </summary>
    public List<MonitoredStepInfo> GetStepExecutions(string executionId)
    {
        return _stepExecutions.TryGetValue(executionId, out var steps) ? steps.ToList() : new List<MonitoredStepInfo>();
    }

    /// <summary>
    /// Gets execution history.
    /// </summary>
    public List<ExecutionInfo> GetExecutionHistory(int limit = 100)
    {
        lock (_historyLock)
        {
            return _executionHistory.TakeLast(limit).ToList();
        }
    }

    /// <summary>
    /// Gets execution history for a specific workflow.
    /// </summary>
    public List<ExecutionInfo> GetWorkflowHistory(string workflowId, int limit = 100)
    {
        lock (_historyLock)
        {
            return _executionHistory
                .Where(e => e.WorkflowId == workflowId)
                .TakeLast(limit)
                .ToList();
        }
    }

    /// <summary>
    /// Adds execution to history.
    /// </summary>
    private void AddToHistory(ExecutionInfo execution)
    {
        lock (_historyLock)
        {
            _executionHistory.Add(execution);

            // Trim history if needed
            if (_executionHistory.Count > _maxHistorySize)
            {
                _executionHistory.RemoveRange(0, _executionHistory.Count - _maxHistorySize);
            }
        }
    }

    /// <summary>
    /// Generates a real-time status display.
    /// </summary>
    public string GenerateStatusDisplay()
    {
        var sb = new StringBuilder();

        sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║ WORKFLOW EXECUTION MONITOR                                                    ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        var activeExecutions = GetActiveExecutions();

        if (activeExecutions.Count == 0)
        {
            sb.AppendLine("No active executions.");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"Active Executions: {activeExecutions.Count}");
            sb.AppendLine();

            foreach (var execution in activeExecutions.OrderBy(e => e.StartTime))
            {
                sb.AppendLine($"┌─ {execution.WorkflowName} ({execution.WorkflowId})");
                sb.AppendLine($"│  Execution ID: {execution.ExecutionId}");
                sb.AppendLine($"│  State: {GetStateIcon(execution.State)} {execution.State}");
                sb.AppendLine($"│  Progress: {execution.CompletedSteps}/{execution.TotalSteps} steps ({execution.ProgressPercentage:F1}%)");
                sb.AppendLine($"│  {GenerateProgressBar(execution.ProgressPercentage)}");
                sb.AppendLine($"│  Elapsed: {FormatDuration(execution.Elapsed)}");

                if (!string.IsNullOrEmpty(execution.CurrentStepName))
                {
                    sb.AppendLine($"│  Current: {execution.CurrentStepName} ({execution.CurrentStepId})");
                }

                if (execution.FailedSteps > 0)
                {
                    sb.AppendLine($"│  Failed: {execution.FailedSteps} steps");
                }

                sb.AppendLine("└─");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a detailed execution report.
    /// </summary>
    public string GenerateExecutionReport(string executionId)
    {
        var execution = GetExecutionInfo(executionId);
        if (execution == null)
        {
            // Check history
            lock (_historyLock)
            {
                execution = _executionHistory.FirstOrDefault(e => e.ExecutionId == executionId);
            }

            if (execution == null)
            {
                return $"Execution {executionId} not found.";
            }
        }

        var sb = new StringBuilder();

        sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║ WORKFLOW EXECUTION REPORT                                                     ║");
        sb.AppendLine("╠═══════════════════════════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║ Workflow: {execution.WorkflowName,-67} ║");
        sb.AppendLine($"║ ID: {execution.WorkflowId,-73} ║");
        sb.AppendLine($"║ Execution ID: {execution.ExecutionId,-63} ║");
        sb.AppendLine("╠═══════════════════════════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║ State: {GetStateIcon(execution.State)} {execution.State,-68} ║");
        sb.AppendLine($"║ Started: {execution.StartTime:yyyy-MM-dd HH:mm:ss UTC}                                       ║");

        if (execution.EndTime.HasValue)
        {
            sb.AppendLine($"║ Ended: {execution.EndTime.Value:yyyy-MM-dd HH:mm:ss UTC}                                         ║");
            sb.AppendLine($"║ Duration: {FormatDuration(execution.Elapsed),-68} ║");
        }
        else
        {
            sb.AppendLine($"║ Elapsed: {FormatDuration(execution.Elapsed),-68} ║");
        }

        sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        // Progress
        sb.AppendLine($"Progress: {execution.CompletedSteps}/{execution.TotalSteps} steps ({execution.ProgressPercentage:F1}%)");
        sb.AppendLine(GenerateProgressBar(execution.ProgressPercentage));
        sb.AppendLine();

        // Step details
        var steps = GetStepExecutions(executionId);
        if (steps.Count > 0)
        {
            sb.AppendLine("Step Execution Details:");
            sb.AppendLine();

            foreach (var step in steps)
            {
                var icon = GetStateIcon(step.State);
                sb.AppendLine($"{icon} {step.StepName} ({step.StepId})");
                sb.AppendLine($"   State: {step.State}");
                sb.AppendLine($"   Duration: {FormatDuration(step.Duration)}");

                if (step.RetryCount > 0)
                {
                    sb.AppendLine($"   Retries: {step.RetryCount}");
                }

                if (!string.IsNullOrEmpty(step.ErrorMessage))
                {
                    sb.AppendLine($"   Error: {step.ErrorMessage}");
                }

                if (step.Outputs.Count > 0)
                {
                    sb.AppendLine($"   Outputs: {string.Join(", ", step.Outputs.Select(kv => $"{kv.Key}={kv.Value}"))}");
                }

                sb.AppendLine();
            }
        }

        // Errors
        if (execution.Errors.Count > 0)
        {
            sb.AppendLine("Errors:");
            foreach (var error in execution.Errors)
            {
                sb.AppendLine($"  • {error}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates an execution history summary.
    /// </summary>
    public string GenerateHistorySummary(int limit = 20)
    {
        var history = GetExecutionHistory(limit);
        var sb = new StringBuilder();

        sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║ EXECUTION HISTORY                                                             ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        if (history.Count == 0)
        {
            sb.AppendLine("No execution history available.");
            sb.AppendLine();
            return sb.ToString();
        }

        sb.AppendLine($"Last {history.Count} executions:");
        sb.AppendLine();

        // Statistics
        var completed = history.Count(e => e.State == ExecutionState.Completed);
        var failed = history.Count(e => e.State == ExecutionState.Failed);
        var cancelled = history.Count(e => e.State == ExecutionState.Cancelled);
        var successRate = history.Count > 0 ? (completed / (double)history.Count) * 100 : 0;

        sb.AppendLine("Statistics:");
        sb.AppendLine($"  ✅ Completed: {completed}");
        sb.AppendLine($"  ❌ Failed: {failed}");
        sb.AppendLine($"  🚫 Cancelled: {cancelled}");
        sb.AppendLine($"  📊 Success Rate: {successRate:F1}%");
        sb.AppendLine();

        sb.AppendLine("Recent Executions:");
        sb.AppendLine();

        foreach (var execution in history.OrderByDescending(e => e.StartTime))
        {
            var icon = GetStateIcon(execution.State);
            var duration = FormatDuration(execution.Elapsed);
            sb.AppendLine($"{icon} {execution.WorkflowName} - {execution.State}");
            sb.AppendLine($"   Started: {execution.StartTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"   Duration: {duration}");
            sb.AppendLine($"   Steps: {execution.CompletedSteps}/{execution.TotalSteps}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string GetStateIcon(ExecutionState state)
    {
        return state switch
        {
            ExecutionState.Queued => "⏳",
            ExecutionState.Running => "▶️",
            ExecutionState.Completed => "✅",
            ExecutionState.Failed => "❌",
            ExecutionState.Cancelled => "🚫",
            ExecutionState.Paused => "⏸️",
            _ => "❓"
        };
    }

    private static string GenerateProgressBar(double percentage, int width = 40)
    {
        var filled = (int)((percentage / 100.0) * width);
        var empty = width - filled;
        return $"[{new string('█', filled)}{new string('░', empty)}] {percentage:F1}%";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{duration.TotalHours:F1}h";
        if (duration.TotalMinutes >= 1)
            return $"{duration.TotalMinutes:F1}m";
        if (duration.TotalSeconds >= 1)
            return $"{duration.TotalSeconds:F1}s";
        return $"{duration.TotalMilliseconds:F0}ms";
    }
}
