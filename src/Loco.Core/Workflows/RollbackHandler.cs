using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Workflows;

/// <summary>
/// Rollback action configuration.
/// </summary>
public class RollbackAction
{
    /// <summary>
    /// Unique identifier for this rollback action.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Name of the rollback action.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Type of action (process, http, log, etc.).
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// Action-specific parameters.
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>
    /// Step ID this rollback is associated with.
    /// </summary>
    public string? ForStep { get; set; }

    /// <summary>
    /// Whether to continue rollback even if this action fails.
    /// </summary>
    public bool ContinueOnError { get; set; } = true;
}

/// <summary>
/// Extended workflow definition with rollback support.
/// </summary>
public partial class WorkflowDefinition
{
    /// <summary>
    /// Rollback actions to execute if workflow fails.
    /// </summary>
    public List<RollbackAction>? RollbackActions { get; set; }
}

/// <summary>
/// Extended workflow step with rollback support.
/// </summary>
public partial class WorkflowStep
{
    /// <summary>
    /// Rollback action for this specific step.
    /// </summary>
    public RollbackAction? Rollback { get; set; }
}

/// <summary>
/// Result of rollback execution.
/// </summary>
public class RollbackResult
{
    public bool Success { get; set; }
    public int ActionsExecuted { get; set; }
    public int ActionsFailed { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> CompletedActions { get; set; } = new();
}

/// <summary>
/// Handles workflow rollback and cleanup operations.
/// </summary>
public class RollbackHandler
{
    private readonly ILogger? _logger;
    private readonly Stack<RollbackAction> _rollbackStack = new();

    public RollbackHandler(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers a rollback action for later execution.
    /// </summary>
    public void RegisterRollback(RollbackAction action)
    {
        _rollbackStack.Push(action);
        _logger?.LogDebug("Registered rollback action: {ActionId} ({ActionName})", action.Id, action.Name);
    }

    /// <summary>
    /// Executes all registered rollback actions in reverse order (LIFO).
    /// </summary>
    public async Task<RollbackResult> ExecuteRollbackAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new RollbackResult { Success = true };

        _logger?.LogWarning("Starting rollback execution ({ActionCount} actions)", _rollbackStack.Count);

        while (_rollbackStack.Count > 0)
        {
            var action = _rollbackStack.Pop();
            result.ActionsExecuted++;

            try
            {
                _logger?.LogInformation("Executing rollback: {ActionId} ({ActionName})", action.Id, action.Name);

                await ExecuteRollbackActionAsync(action, cancellationToken);

                result.CompletedActions.Add(action.Id);
                _logger?.LogInformation("Rollback completed: {ActionId}", action.Id);
            }
            catch (Exception ex)
            {
                result.ActionsFailed++;
                result.Errors.Add($"{action.Id}: {ex.Message}");
                _logger?.LogError(ex, "Rollback action failed: {ActionId}", action.Id);

                if (!action.ContinueOnError)
                {
                    result.Success = false;
                    _logger?.LogError("Rollback halted due to error in action: {ActionId}", action.Id);
                    break;
                }
            }
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        result.Success = result.Success && result.ActionsFailed == 0;

        _logger?.LogWarning(
            "Rollback execution completed: {Status} ({Executed} executed, {Failed} failed, {Duration:F2}s)",
            result.Success ? "SUCCESS" : "PARTIAL",
            result.ActionsExecuted,
            result.ActionsFailed,
            result.Duration.TotalSeconds);

        return result;
    }

    /// <summary>
    /// Executes cleanup actions.
    /// </summary>
    public async Task<RollbackResult> ExecuteCleanupAsync(
        CleanupHandler cleanup,
        bool workflowSucceeded,
        CancellationToken cancellationToken = default)
    {
        // Check if cleanup should run
        if (workflowSucceeded && !cleanup.RunOnSuccess)
        {
            _logger?.LogInformation("Skipping cleanup (workflow succeeded, RunOnSuccess=false)");
            return new RollbackResult { Success = true };
        }

        if (!workflowSucceeded && !cleanup.RunOnFailure)
        {
            _logger?.LogInformation("Skipping cleanup (workflow failed, RunOnFailure=false)");
            return new RollbackResult { Success = true };
        }

        var stopwatch = Stopwatch.StartNew();
        var result = new RollbackResult { Success = true };

        _logger?.LogInformation("Starting cleanup execution ({ActionCount} actions)", cleanup.Actions.Count);

        foreach (var action in cleanup.Actions)
        {
            result.ActionsExecuted++;

            try
            {
                _logger?.LogInformation("Executing cleanup: {ActionId} ({ActionName})", action.Id, action.Name);

                await ExecuteRollbackActionAsync(action, cancellationToken);

                result.CompletedActions.Add(action.Id);
                _logger?.LogInformation("Cleanup completed: {ActionId}", action.Id);
            }
            catch (Exception ex)
            {
                result.ActionsFailed++;
                result.Errors.Add($"{action.Id}: {ex.Message}");
                _logger?.LogError(ex, "Cleanup action failed: {ActionId}", action.Id);

                if (!action.ContinueOnError)
                {
                    result.Success = false;
                    _logger?.LogError("Cleanup halted due to error in action: {ActionId}", action.Id);
                    break;
                }
            }
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        result.Success = result.Success && result.ActionsFailed == 0;

        _logger?.LogInformation(
            "Cleanup execution completed: {Status} ({Executed} executed, {Failed} failed, {Duration:F2}s)",
            result.Success ? "SUCCESS" : "PARTIAL",
            result.ActionsExecuted,
            result.ActionsFailed,
            result.Duration.TotalSeconds);

        return result;
    }

    /// <summary>
    /// Executes a single rollback action.
    /// </summary>
    private async Task ExecuteRollbackActionAsync(RollbackAction action, CancellationToken cancellationToken)
    {
        switch (action.Type.ToLowerInvariant())
        {
            case "process":
                await ExecuteProcessRollbackAsync(action, cancellationToken);
                break;

            case "log":
                ExecuteLogRollback(action);
                break;

            case "delay":
                await ExecuteDelayRollbackAsync(action, cancellationToken);
                break;

            default:
                _logger?.LogWarning("Unknown rollback action type: {Type}", action.Type);
                // Simulate execution
                await Task.Delay(50, cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Executes a process-type rollback action.
    /// </summary>
    private async Task ExecuteProcessRollbackAsync(RollbackAction action, CancellationToken cancellationToken)
    {
        if (!action.Parameters.TryGetValue("command", out var command))
        {
            throw new InvalidOperationException("Process rollback action missing 'command' parameter");
        }

        action.Parameters.TryGetValue("arguments", out var arguments);
        action.Parameters.TryGetValue("workingDirectory", out var workingDirectory);

        _logger?.LogInformation("Executing rollback process: {Command} {Arguments}", command, arguments ?? "");

        // Simulate process execution (replace with actual process execution in production)
        await Task.Delay(100, cancellationToken);
    }

    /// <summary>
    /// Executes a log-type rollback action.
    /// </summary>
    private void ExecuteLogRollback(RollbackAction action)
    {
        if (action.Parameters.TryGetValue("message", out var message))
        {
            _logger?.LogInformation("[ROLLBACK LOG] {Message}", message);
        }
    }

    /// <summary>
    /// Executes a delay-type rollback action.
    /// </summary>
    private async Task ExecuteDelayRollbackAsync(RollbackAction action, CancellationToken cancellationToken)
    {
        if (action.Parameters.TryGetValue("duration", out var durationStr))
        {
            if (TimeSpan.TryParse(durationStr, out var duration))
            {
                _logger?.LogInformation("Rollback delay: {Duration}", duration);
                await Task.Delay(duration, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Clears all registered rollback actions.
    /// </summary>
    public void ClearRollbacks()
    {
        _rollbackStack.Clear();
        _logger?.LogDebug("Cleared all registered rollback actions");
    }

    /// <summary>
    /// Gets the count of registered rollback actions.
    /// </summary>
    public int GetRollbackCount() => _rollbackStack.Count;

    /// <summary>
    /// Generates a rollback execution report.
    /// </summary>
    public static string GenerateRollbackReport(RollbackResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║ ROLLBACK EXECUTION REPORT                                                     ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        var statusIcon = result.Success ? "✅" : "⚠️";
        var statusText = result.Success ? "SUCCESS" : "PARTIAL";

        sb.AppendLine($"Status: {statusIcon} {statusText}");
        sb.AppendLine($"Duration: {result.Duration.TotalSeconds:F2}s");
        sb.AppendLine($"Actions Executed: {result.ActionsExecuted}");
        sb.AppendLine($"Actions Failed: {result.ActionsFailed}");
        sb.AppendLine();

        if (result.CompletedActions.Any())
        {
            sb.AppendLine("Completed Actions:");
            foreach (var actionId in result.CompletedActions)
            {
                sb.AppendLine($"  ✅ {actionId}");
            }
            sb.AppendLine();
        }

        if (result.Errors.Any())
        {
            sb.AppendLine("Errors:");
            foreach (var error in result.Errors)
            {
                sb.AppendLine($"  ❌ {error}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
