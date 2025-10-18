using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Workflows;

/// <summary>
/// Processes workflow includes and merges them into a single workflow.
/// </summary>
public class WorkflowIncludeProcessor
{
    private readonly ILogger? _logger;
    private readonly string _baseDirectory;
    private readonly HashSet<string> _processedFiles = new();

    public WorkflowIncludeProcessor(string baseDirectory, ILogger? logger = null)
    {
        _baseDirectory = baseDirectory;
        _logger = logger;
    }

    /// <summary>
    /// Processes includes and returns a merged workflow.
    /// </summary>
    public async Task<WorkflowDefinition> ProcessIncludesAsync(WorkflowDefinition workflow)
    {
        _processedFiles.Clear();
        return await ProcessIncludesRecursiveAsync(workflow, Path.GetDirectoryName(workflow.Id) ?? _baseDirectory);
    }

    /// <summary>
    /// Recursively processes includes.
    /// </summary>
    private async Task<WorkflowDefinition> ProcessIncludesRecursiveAsync(
        WorkflowDefinition workflow,
        string currentDirectory)
    {
        if (workflow.Includes == null || !workflow.Includes.Any())
            return workflow;

        var mergedSteps = new List<WorkflowStep>(workflow.Steps);
        var mergedVariables = new Dictionary<string, string>(workflow.Variables ?? new());

        foreach (var include in workflow.Includes)
        {
            try
            {
                // Resolve include path
                var includePath = Path.IsPathRooted(include.Path)
                    ? include.Path
                    : Path.Combine(currentDirectory, include.Path);

                includePath = Path.GetFullPath(includePath);

                // Check for circular includes
                if (_processedFiles.Contains(includePath))
                {
                    _logger?.LogWarning("Circular include detected: {Path}", includePath);
                    continue;
                }

                _processedFiles.Add(includePath);

                if (!File.Exists(includePath))
                {
                    _logger?.LogError("Include file not found: {Path}", includePath);
                    continue;
                }

                // Load included workflow
                var includedJson = await File.ReadAllTextAsync(includePath);
                var includedWorkflow = JsonSerializer.Deserialize<WorkflowDefinition>(
                    includedJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (includedWorkflow == null)
                {
                    _logger?.LogError("Failed to parse included workflow: {Path}", includePath);
                    continue;
                }

                // Recursively process includes in the included workflow
                includedWorkflow = await ProcessIncludesRecursiveAsync(
                    includedWorkflow,
                    Path.GetDirectoryName(includePath) ?? currentDirectory);

                // Filter steps if specific steps are requested
                var stepsToInclude = include.Steps != null && include.Steps.Any()
                    ? includedWorkflow.Steps.Where(s => include.Steps.Contains(s.Id)).ToList()
                    : includedWorkflow.Steps;

                // Apply prefix if specified
                if (!string.IsNullOrEmpty(include.Prefix))
                {
                    stepsToInclude = stepsToInclude.Select(step =>
                    {
                        var clonedStep = CloneStep(step);
                        clonedStep.Id = include.Prefix + clonedStep.Id;

                        // Update dependencies with prefix
                        if (clonedStep.DependsOn != null && clonedStep.DependsOn.Any())
                        {
                            clonedStep.DependsOn = clonedStep.DependsOn
                                .Select(dep => include.Prefix + dep)
                                .ToList();
                        }

                        return clonedStep;
                    }).ToList();
                }

                // Merge steps
                mergedSteps.AddRange(stepsToInclude);

                // Merge variables
                if (includedWorkflow.Variables != null)
                {
                    foreach (var kvp in includedWorkflow.Variables)
                    {
                        if (!mergedVariables.ContainsKey(kvp.Key))
                            mergedVariables[kvp.Key] = kvp.Value;
                    }
                }

                // Apply passed variables
                if (include.Variables != null)
                {
                    foreach (var kvp in include.Variables)
                    {
                        mergedVariables[kvp.Key] = kvp.Value;
                    }
                }

                _logger?.LogInformation("Included workflow: {Path} ({StepCount} steps)", includePath, stepsToInclude.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing include: {Path}", include.Path);
            }
        }

        // Create merged workflow
        var mergedWorkflow = new WorkflowDefinition
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Description = workflow.Description,
            Steps = mergedSteps,
            Variables = mergedVariables,
            Environments = workflow.Environments,
            Schedule = workflow.Schedule,
            Hooks = workflow.Hooks,
            Includes = null // Clear includes after processing
        };

        return mergedWorkflow;
    }

    /// <summary>
    /// Creates a deep copy of a workflow step.
    /// </summary>
    private WorkflowStep CloneStep(WorkflowStep step)
    {
        // Simple clone using JSON serialization
        var json = JsonSerializer.Serialize(step);
        return JsonSerializer.Deserialize<WorkflowStep>(json)
            ?? new WorkflowStep { Id = step.Id, Name = step.Name, Type = step.Type };
    }

    /// <summary>
    /// Validates that all included workflows exist and are valid.
    /// </summary>
    public async Task<List<string>> ValidateIncludesAsync(WorkflowDefinition workflow, string baseDirectory)
    {
        var errors = new List<string>();

        if (workflow.Includes == null || !workflow.Includes.Any())
            return errors;

        foreach (var include in workflow.Includes)
        {
            try
            {
                var includePath = Path.IsPathRooted(include.Path)
                    ? include.Path
                    : Path.Combine(baseDirectory, include.Path);

                includePath = Path.GetFullPath(includePath);

                if (!File.Exists(includePath))
                {
                    errors.Add($"Include file not found: {include.Path}");
                    continue;
                }

                // Try to parse the included file
                var includedJson = await File.ReadAllTextAsync(includePath);
                var includedWorkflow = JsonSerializer.Deserialize<WorkflowDefinition>(
                    includedJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (includedWorkflow == null)
                {
                    errors.Add($"Failed to parse included workflow: {include.Path}");
                    continue;
                }

                // Validate that requested steps exist
                if (include.Steps != null && include.Steps.Any())
                {
                    var missingSteps = include.Steps
                        .Where(stepId => !includedWorkflow.Steps.Any(s => s.Id == stepId))
                        .ToList();

                    if (missingSteps.Any())
                    {
                        errors.Add($"Include {include.Path} missing requested steps: {string.Join(", ", missingSteps)}");
                    }
                }

                // Recursively validate nested includes
                var nestedErrors = await ValidateIncludesAsync(
                    includedWorkflow,
                    Path.GetDirectoryName(includePath) ?? baseDirectory);
                errors.AddRange(nestedErrors);
            }
            catch (Exception ex)
            {
                errors.Add($"Error validating include {include.Path}: {ex.Message}");
            }
        }

        return errors;
    }

    /// <summary>
    /// Generates a visual representation of the include hierarchy.
    /// </summary>
    public static string GenerateIncludeTree(WorkflowDefinition workflow, int depth = 0)
    {
        var sb = new System.Text.StringBuilder();
        var indent = new string(' ', depth * 2);

        sb.AppendLine($"{indent}📄 {workflow.Name} ({workflow.Id})");
        sb.AppendLine($"{indent}   Steps: {workflow.Steps.Count}");

        if (workflow.Includes != null && workflow.Includes.Any())
        {
            sb.AppendLine($"{indent}   Includes:");
            foreach (var include in workflow.Includes)
            {
                var prefix = !string.IsNullOrEmpty(include.Prefix) ? $" (prefix: {include.Prefix})" : "";
                var steps = include.Steps != null && include.Steps.Any()
                    ? $" [steps: {string.Join(", ", include.Steps)}]"
                    : "";

                sb.AppendLine($"{indent}   ├─ {include.Path}{prefix}{steps}");
            }
        }

        return sb.ToString();
    }
}
