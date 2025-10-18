namespace Loco.Core.Workflows;

/// <summary>
/// Represents an external workflow or step sequence to include.
/// </summary>
public class WorkflowInclude
{
    /// <summary>
    /// Path to the workflow file to include (relative or absolute).
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// Optional ID to reference this include.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Position where steps should be inserted (0 = beginning, -1 = end).
    /// Default is -1 (append).
    /// </summary>
    public int Position { get; set; } = -1;

    /// <summary>
    /// Variables to pass to the included workflow.
    /// </summary>
    public Dictionary<string, string>? Variables { get; set; }

    /// <summary>
    /// Whether to continue if the include fails to load.
    /// Default is false (fail on error).
    /// </summary>
    public bool ContinueOnError { get; set; } = false;

    /// <summary>
    /// Optional: specific step IDs to include (if null, includes all).
    /// </summary>
    public List<string>? Steps { get; set; }

    /// <summary>
    /// Optional: prefix to add to step IDs to avoid conflicts.
    /// </summary>
    public string? Prefix { get; set; }
}

/// <summary>
/// Partial class extension for WorkflowDefinition to support includes.
/// </summary>
public partial class WorkflowDefinition
{
    /// <summary>
    /// External workflows or step sequences to include.
    /// </summary>
    public List<WorkflowInclude>? Includes { get; set; }
}
