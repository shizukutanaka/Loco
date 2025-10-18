namespace Loco.Core.Workflows;

/// <summary>
/// Represents a dependency on another step.
/// </summary>
public class StepDependency
{
    /// <summary>
    /// ID of the step that must complete before this step can run.
    /// </summary>
    public string StepId { get; set; } = "";

    /// <summary>
    /// Whether the dependency step must succeed (true) or just complete (false).
    /// </summary>
    public bool RequireSuccess { get; set; } = true;

    /// <summary>
    /// Optional condition that must be met (e.g., "output_size>1000").
    /// </summary>
    public string? Condition { get; set; }
}

/// <summary>
/// Partial class extension for WorkflowStep to support dependencies.
/// </summary>
public partial class WorkflowStep
{
    /// <summary>
    /// Steps that must complete before this step can run.
    /// </summary>
    public List<string>? DependsOn { get; set; }

    /// <summary>
    /// Advanced dependency configuration.
    /// </summary>
    public List<StepDependency>? Dependencies { get; set; }

    /// <summary>
    /// Whether to run this step in parallel with other steps (if dependencies allow).
    /// </summary>
    public bool AllowParallel { get; set; } = false;
}

/// <summary>
/// Validates and analyzes workflow step dependencies.
/// </summary>
public class DependencyAnalyzer
{
    private readonly List<WorkflowStep> _steps;
    private readonly Dictionary<string, WorkflowStep> _stepMap;

    public DependencyAnalyzer(List<WorkflowStep> steps)
    {
        _steps = steps;
        _stepMap = steps.ToDictionary(s => s.Id);
    }

    /// <summary>
    /// Validates that dependencies form a valid DAG (no cycles).
    /// </summary>
    public (bool isValid, List<string> errors) ValidateDependencies()
    {
        var errors = new List<string>();

        // Check all dependencies exist
        foreach (var step in _steps)
        {
            var deps = GetAllDependencyIds(step);
            foreach (var depId in deps)
            {
                if (!_stepMap.ContainsKey(depId))
                {
                    errors.Add($"Step '{step.Id}' depends on non-existent step '{depId}'");
                }
            }
        }

        // Check for cycles
        foreach (var step in _steps)
        {
            if (HasCycle(step.Id, new HashSet<string>(), new HashSet<string>()))
            {
                errors.Add($"Circular dependency detected involving step '{step.Id}'");
            }
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// Gets the execution order of steps based on dependencies.
    /// Returns groups of steps that can run in parallel.
    /// </summary>
    public List<List<string>> GetExecutionOrder()
    {
        var result = new List<List<string>>();
        var remaining = new HashSet<string>(_steps.Select(s => s.Id));
        var completed = new HashSet<string>();

        while (remaining.Count > 0)
        {
            // Find steps whose dependencies are all completed
            var ready = remaining
                .Where(stepId =>
                {
                    var step = _stepMap[stepId];
                    var deps = GetAllDependencyIds(step);
                    return deps.All(d => completed.Contains(d));
                })
                .ToList();

            if (ready.Count == 0)
            {
                // Deadlock - this shouldn't happen if validation passed
                break;
            }

            result.Add(ready);

            foreach (var stepId in ready)
            {
                remaining.Remove(stepId);
                completed.Add(stepId);
            }
        }

        return result;
    }

    /// <summary>
    /// Generates a visual representation of the dependency graph.
    /// </summary>
    public string GenerateDependencyGraph()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("Dependency Graph:");
        sb.AppendLine();

        foreach (var step in _steps)
        {
            var deps = GetAllDependencyIds(step);

            if (deps.Count > 0)
            {
                sb.AppendLine($"  {step.Name} (ID: {step.Id})");

                foreach (var depId in deps)
                {
                    if (_stepMap.TryGetValue(depId, out var depStep))
                    {
                        sb.AppendLine($"    ↑ depends on: {depStep.Name} (ID: {depId})");
                    }
                }

                sb.AppendLine();
            }
        }

        // Show execution order
        var executionOrder = GetExecutionOrder();

        sb.AppendLine("Execution Order (parallel groups):");
        sb.AppendLine();

        for (int i = 0; i < executionOrder.Count; i++)
        {
            var group = executionOrder[i];

            sb.AppendLine($"  Group {i + 1}:");

            foreach (var stepId in group)
            {
                if (_stepMap.TryGetValue(stepId, out var step))
                {
                    sb.AppendLine($"    - {step.Name} (ID: {stepId})");
                }
            }

            if (i < executionOrder.Count - 1)
            {
                sb.AppendLine("    ↓");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private List<string> GetAllDependencyIds(WorkflowStep step)
    {
        var deps = new List<string>();

        if (step.DependsOn != null)
        {
            deps.AddRange(step.DependsOn);
        }

        if (step.Dependencies != null)
        {
            deps.AddRange(step.Dependencies.Select(d => d.StepId));
        }

        return deps.Distinct().ToList();
    }

    private bool HasCycle(string stepId, HashSet<string> visited, HashSet<string> recursionStack)
    {
        if (!_stepMap.ContainsKey(stepId))
            return false;

        if (recursionStack.Contains(stepId))
            return true;

        if (visited.Contains(stepId))
            return false;

        visited.Add(stepId);
        recursionStack.Add(stepId);

        var step = _stepMap[stepId];
        var deps = GetAllDependencyIds(step);

        foreach (var depId in deps)
        {
            if (HasCycle(depId, visited, recursionStack))
                return true;
        }

        recursionStack.Remove(stepId);

        return false;
    }
}
