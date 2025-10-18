namespace Loco.Core.Workflows;

/// <summary>
/// Represents a group of workflow steps that can run in parallel.
/// </summary>
public class ParallelStepGroup
{
    /// <summary>
    /// Unique identifier for this parallel group.
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// List of step IDs that should run in parallel.
    /// </summary>
    public List<string> StepIds { get; set; } = new();

    /// <summary>
    /// Whether to wait for all steps to complete before continuing.
    /// Default is true.
    /// </summary>
    public bool WaitForAll { get; set; } = true;

    /// <summary>
    /// Whether to fail the entire group if any step fails.
    /// Default is true.
    /// </summary>
    public bool FailOnAnyError { get; set; } = true;

    /// <summary>
    /// Maximum number of steps to run concurrently. 0 means unlimited.
    /// </summary>
    public int MaxConcurrency { get; set; } = 0;
}

/// <summary>
/// Workflow definition that supports parallel execution groups.
/// </summary>
public partial class WorkflowDefinition
{
    /// <summary>
    /// Groups of steps that can execute in parallel.
    /// </summary>
    public List<ParallelStepGroup>? ParallelGroups { get; set; }
}
