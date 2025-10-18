namespace Loco.Core.Workflows;

/// <summary>
/// Represents an environment configuration preset.
/// </summary>
public class EnvironmentPreset
{
    /// <summary>
    /// Name of the environment (e.g., dev, staging, production).
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Description of this environment.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Variables specific to this environment.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = new();

    /// <summary>
    /// Whether this is the default environment.
    /// </summary>
    public bool IsDefault { get; set; }
}

/// <summary>
/// Workflow definition with environment presets support.
/// </summary>
public partial class WorkflowDefinition
{
    /// <summary>
    /// Environment presets for this workflow.
    /// </summary>
    public List<EnvironmentPreset>? Environments { get; set; }
}
