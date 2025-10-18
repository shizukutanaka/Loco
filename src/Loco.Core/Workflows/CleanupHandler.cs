namespace Loco.Core.Workflows;

/// <summary>
/// Represents a cleanup action to run when a step fails.
/// </summary>
public class CleanupHandler
{
    /// <summary>
    /// Type of cleanup action (log, process, delete-file, etc.).
    /// </summary>
    public string Type { get; set; } = "log";

    /// <summary>
    /// Message for log cleanup actions.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Command for process cleanup actions.
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// File path for delete-file cleanup actions.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Whether to continue workflow after cleanup (default: false = stop workflow).
    /// </summary>
    public bool ContinueAfterCleanup { get; set; } = false;

    /// <summary>
    /// Timeout for cleanup action in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// List of cleanup actions to execute (for workflow-level cleanup).
    /// </summary>
    public List<RollbackAction>? Actions { get; set; }

    /// <summary>
    /// Whether to run cleanup on success (for workflow-level cleanup).
    /// </summary>
    public bool RunOnSuccess { get; set; } = true;

    /// <summary>
    /// Whether to run cleanup on failure (for workflow-level cleanup).
    /// </summary>
    public bool RunOnFailure { get; set; } = true;
}

/// <summary>
/// Partial class extension for WorkflowStep to support cleanup handlers.
/// </summary>
public partial class WorkflowStep
{
    /// <summary>
    /// Cleanup handler to run if this step fails.
    /// </summary>
    public CleanupHandler? OnFailure { get; set; }

    /// <summary>
    /// Cleanup handler to run after this step completes (success or failure).
    /// </summary>
    public CleanupHandler? OnComplete { get; set; }
}

/// <summary>
/// Partial class extension for WorkflowDefinition to support cleanup.
/// </summary>
public partial class WorkflowDefinition
{
    /// <summary>
    /// Cleanup handler to run after workflow completes.
    /// </summary>
    public CleanupHandler? Cleanup { get; set; }
}
