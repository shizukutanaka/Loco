namespace Loco.Core.Workflows;

/// <summary>
/// Extends WorkflowDefinition with variables support.
/// </summary>
public partial class WorkflowDefinition
{
    /// <summary>
    /// Variables defined for this workflow.
    /// </summary>
    public Dictionary<string, string>? Variables { get; set; }
}

/// <summary>
/// Extends WorkflowStep with description support.
/// </summary>
public partial class WorkflowStep
{
    /// <summary>
    /// Description of this step.
    /// </summary>
    public string? Description { get; set; }
}
