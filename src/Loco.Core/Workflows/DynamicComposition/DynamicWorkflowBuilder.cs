// Phase 9: Dynamic Workflow Composition Engine
// Runtime workflow building, composition, and declarative orchestration
// Enables programmatic workflow creation with fluent builder patterns

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Workflows.DynamicComposition;

/// <summary>
/// Workflow step definition
/// </summary>
public class WorkflowStepDefinition
{
    public string StepId { get; set; } = Guid.NewGuid().ToString();
    public string StepName { get; set; } = string.Empty;
    public string StepType { get; set; } = string.Empty; // http, script, transform, conditional, parallel
    public string? Description { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
    public List<string> InputVariables { get; set; } = new();
    public List<string> OutputVariables { get; set; } = new();
    public int? TimeoutSeconds { get; set; }
    public int? RetryCount { get; set; }
    public string? ErrorHandler { get; set; }
    public List<string> DependsOn { get; set; } = new();
}

/// <summary>
/// Conditional branch definition
/// </summary>
public class ConditionalBranch
{
    public string BranchId { get; set; } = Guid.NewGuid().ToString();
    public string Condition { get; set; } = string.Empty; // JavaScript or JSONPath expression
    public List<WorkflowStepDefinition> ThenSteps { get; set; } = new();
    public List<WorkflowStepDefinition>? ElseSteps { get; set; }
}

/// <summary>
/// Parallel execution group
/// </summary>
public class ParallelExecutionGroup
{
    public string GroupId { get; set; } = Guid.NewGuid().ToString();
    public string? Description { get; set; }
    public List<WorkflowStepDefinition> ParallelSteps { get; set; } = new();
    public int MaxConcurrency { get; set; } = 10;
    public bool FailFast { get; set; } = false;
    public string? AggregationStrategy { get; set; } // all, first, majority
}

/// <summary>
/// Loop definition
/// </summary>
public class LoopDefinition
{
    public string LoopId { get; set; } = Guid.NewGuid().ToString();
    public string ItemsExpression { get; set; } = string.Empty; // Variable or JSONPath
    public string ItemVariableName { get; set; } = "item";
    public string? IndexVariableName { get; set; } = "index";
    public int? MaxIterations { get; set; } = 1000;
    public List<WorkflowStepDefinition> BodySteps { get; set; } = new();
    public string? BreakCondition { get; set; }
}

/// <summary>
/// Dynamic workflow definition
/// </summary>
public class DynamicWorkflowDefinition
{
    public string WorkflowId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Version { get; set; } = "1.0.0";

    // Structure
    public List<WorkflowStepDefinition> Steps { get; set; } = new();
    public List<ConditionalBranch> ConditionalBranches { get; set; } = new();
    public List<ParallelExecutionGroup> ParallelGroups { get; set; } = new();
    public List<LoopDefinition> Loops { get; set; } = new();

    // Metadata
    public Dictionary<string, object> Variables { get; set; } = new();
    public List<string> InputParameters { get; set; } = new();
    public List<string> OutputParameters { get; set; } = new();
    public Dictionary<string, string>? GlobalErrorHandlers { get; set; }

    // Configuration
    public int? DefaultTimeoutSeconds { get; set; } = 300;
    public int? DefaultRetryCount { get; set; } = 3;
    public bool EnableParallel { get; set; } = true;
    public bool EnableDynamicSteps { get; set; } = true;

    // Lifecycle
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsPublished { get; set; } = false;
}

/// <summary>
/// Composition blueprint
/// </summary>
public class CompositionBlueprint
{
    public string BlueprintId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string BlueprintName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> BaseWorkflowIds { get; set; } = new();
    public List<string> IntegratedWorkflowIds { get; set; } = new();
    public Dictionary<string, object> MappingRules { get; set; } = new();
    public string CompositionStrategy { get; set; } = string.Empty; // sequential, parallel, conditional
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Dynamic workflow builder interface
/// </summary>
public interface IDynamicWorkflowBuilder
{
    // Workflow creation
    Task<DynamicWorkflowDefinition> CreateWorkflowAsync(
        string tenantId,
        string workflowName,
        CancellationToken ct = default);

    Task<DynamicWorkflowDefinition?> GetWorkflowDefinitionAsync(
        string workflowId,
        CancellationToken ct = default);

    Task<List<DynamicWorkflowDefinition>> GetWorkflowsAsync(
        string tenantId,
        bool publishedOnly = false,
        CancellationToken ct = default);

    // Step management
    Task<WorkflowStepDefinition> AddStepAsync(
        string workflowId,
        WorkflowStepDefinition step,
        CancellationToken ct = default);

    Task<bool> RemoveStepAsync(
        string workflowId,
        string stepId,
        CancellationToken ct = default);

    Task<bool> UpdateStepAsync(
        string workflowId,
        string stepId,
        WorkflowStepDefinition step,
        CancellationToken ct = default);

    // Control flow
    Task<ConditionalBranch> AddConditionalBranchAsync(
        string workflowId,
        ConditionalBranch branch,
        CancellationToken ct = default);

    Task<ParallelExecutionGroup> AddParallelGroupAsync(
        string workflowId,
        ParallelExecutionGroup group,
        CancellationToken ct = default);

    Task<LoopDefinition> AddLoopAsync(
        string workflowId,
        LoopDefinition loop,
        CancellationToken ct = default);

    // Composition
    Task<CompositionBlueprint> ComposeWorkflowsAsync(
        string tenantId,
        List<string> workflowIds,
        string strategy,
        CancellationToken ct = default);

    Task<DynamicWorkflowDefinition> MergeWorkflowsAsync(
        string tenantId,
        List<string> workflowIds,
        string mergedName,
        CancellationToken ct = default);

    // Validation and publishing
    Task<Dictionary<string, object>> ValidateWorkflowAsync(
        string workflowId,
        CancellationToken ct = default);

    Task<bool> PublishWorkflowAsync(
        string workflowId,
        CancellationToken ct = default);

    // Analytics
    Task<Dictionary<string, object>> GetCompositionAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default);
}

/// <summary>
/// Dynamic workflow builder implementation
/// </summary>
public class DynamicWorkflowBuilder : IDynamicWorkflowBuilder
{
    private readonly ILogger<DynamicWorkflowBuilder> _logger;
    private readonly Dictionary<string, DynamicWorkflowDefinition> _workflows;
    private readonly Dictionary<string, CompositionBlueprint> _blueprints;
    private readonly Dictionary<string, List<string>> _compositionHistory;

    public DynamicWorkflowBuilder(ILogger<DynamicWorkflowBuilder> logger)
    {
        _logger = logger;
        _workflows = new Dictionary<string, DynamicWorkflowDefinition>();
        _blueprints = new Dictionary<string, CompositionBlueprint>();
        _compositionHistory = new Dictionary<string, List<string>>();
    }

    // Workflow creation
    public async Task<DynamicWorkflowDefinition> CreateWorkflowAsync(
        string tenantId,
        string workflowName,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var workflow = new DynamicWorkflowDefinition
        {
            TenantId = tenantId,
            WorkflowName = workflowName,
            CreatedBy = "system",
        };

        _workflows[workflow.WorkflowId] = workflow;

        _logger.LogInformation(
            "Dynamic workflow created: {WorkflowId}, Tenant: {TenantId}, Name: {WorkflowName}",
            workflow.WorkflowId, tenantId, workflowName);

        return workflow;
    }

    public async Task<DynamicWorkflowDefinition?> GetWorkflowDefinitionAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _workflows.TryGetValue(workflowId, out var workflow);
        return workflow;
    }

    public async Task<List<DynamicWorkflowDefinition>> GetWorkflowsAsync(
        string tenantId,
        bool publishedOnly = false,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return _workflows.Values
            .Where(w => w.TenantId == tenantId)
            .Where(w => !publishedOnly || w.IsPublished)
            .OrderByDescending(w => w.CreatedAt)
            .ToList();
    }

    // Step management
    public async Task<WorkflowStepDefinition> AddStepAsync(
        string workflowId,
        WorkflowStepDefinition step,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var workflow = await GetWorkflowDefinitionAsync(workflowId, ct);
        if (workflow == null)
        {
            throw new KeyNotFoundException($"Workflow not found: {workflowId}");
        }

        workflow.Steps.Add(step);

        _logger.LogInformation(
            "Step added to workflow: {WorkflowId}, Step: {StepName}, Type: {StepType}",
            workflowId, step.StepName, step.StepType);

        return step;
    }

    public async Task<bool> RemoveStepAsync(
        string workflowId,
        string stepId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var workflow = await GetWorkflowDefinitionAsync(workflowId, ct);
        if (workflow == null)
            return false;

        var step = workflow.Steps.FirstOrDefault(s => s.StepId == stepId);
        if (step == null)
            return false;

        workflow.Steps.Remove(step);

        _logger.LogInformation(
            "Step removed from workflow: {WorkflowId}, Step: {StepId}",
            workflowId, stepId);

        return true;
    }

    public async Task<bool> UpdateStepAsync(
        string workflowId,
        string stepId,
        WorkflowStepDefinition step,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var workflow = await GetWorkflowDefinitionAsync(workflowId, ct);
        if (workflow == null)
            return false;

        var existingStep = workflow.Steps.FirstOrDefault(s => s.StepId == stepId);
        if (existingStep == null)
            return false;

        step.StepId = stepId;
        var index = workflow.Steps.IndexOf(existingStep);
        workflow.Steps[index] = step;

        _logger.LogInformation(
            "Step updated in workflow: {WorkflowId}, Step: {StepId}",
            workflowId, stepId);

        return true;
    }

    // Control flow
    public async Task<ConditionalBranch> AddConditionalBranchAsync(
        string workflowId,
        ConditionalBranch branch,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var workflow = await GetWorkflowDefinitionAsync(workflowId, ct);
        if (workflow == null)
        {
            throw new KeyNotFoundException($"Workflow not found: {workflowId}");
        }

        workflow.ConditionalBranches.Add(branch);

        _logger.LogInformation(
            "Conditional branch added to workflow: {WorkflowId}, Condition: {Condition}",
            workflowId, branch.Condition);

        return branch;
    }

    public async Task<ParallelExecutionGroup> AddParallelGroupAsync(
        string workflowId,
        ParallelExecutionGroup group,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var workflow = await GetWorkflowDefinitionAsync(workflowId, ct);
        if (workflow == null)
        {
            throw new KeyNotFoundException($"Workflow not found: {workflowId}");
        }

        if (!workflow.EnableParallel)
        {
            throw new InvalidOperationException("Workflow does not support parallel execution");
        }

        workflow.ParallelGroups.Add(group);

        _logger.LogInformation(
            "Parallel group added to workflow: {WorkflowId}, Steps: {StepCount}, MaxConcurrency: {MaxConcurrency}",
            workflowId, group.ParallelSteps.Count, group.MaxConcurrency);

        return group;
    }

    public async Task<LoopDefinition> AddLoopAsync(
        string workflowId,
        LoopDefinition loop,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var workflow = await GetWorkflowDefinitionAsync(workflowId, ct);
        if (workflow == null)
        {
            throw new KeyNotFoundException($"Workflow not found: {workflowId}");
        }

        if (loop.MaxIterations > 10000)
        {
            throw new ArgumentException("Loop iterations cannot exceed 10,000");
        }

        workflow.Loops.Add(loop);

        _logger.LogInformation(
            "Loop added to workflow: {WorkflowId}, ItemsExpression: {ItemsExpression}, MaxIterations: {MaxIterations}",
            workflowId, loop.ItemsExpression, loop.MaxIterations);

        return loop;
    }

    // Composition
    public async Task<CompositionBlueprint> ComposeWorkflowsAsync(
        string tenantId,
        List<string> workflowIds,
        string strategy,
        CancellationToken ct = default)
    {
        await Task.Delay(150, ct); // Simulate composition

        var blueprint = new CompositionBlueprint
        {
            TenantId = tenantId,
            BlueprintName = $"Composition_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
            BaseWorkflowIds = workflowIds,
            CompositionStrategy = strategy,
        };

        // Validate workflows exist
        var workflows = new List<DynamicWorkflowDefinition>();
        foreach (var wfId in workflowIds)
        {
            var wf = await GetWorkflowDefinitionAsync(wfId, ct);
            if (wf == null)
                throw new KeyNotFoundException($"Workflow not found: {wfId}");
            workflows.Add(wf);
        }

        // Build mapping rules based on strategy
        switch (strategy.ToLower())
        {
            case "sequential":
                BuildSequentialMapping(blueprint, workflows);
                break;
            case "parallel":
                BuildParallelMapping(blueprint, workflows);
                break;
            case "conditional":
                BuildConditionalMapping(blueprint, workflows);
                break;
        }

        _blueprints[blueprint.BlueprintId] = blueprint;

        if (!_compositionHistory.ContainsKey(tenantId))
        {
            _compositionHistory[tenantId] = new List<string>();
        }
        _compositionHistory[tenantId].Add(blueprint.BlueprintId);

        _logger.LogInformation(
            "Workflows composed: Tenant={TenantId}, Count={Count}, Strategy={Strategy}",
            tenantId, workflowIds.Count, strategy);

        return blueprint;
    }

    public async Task<DynamicWorkflowDefinition> MergeWorkflowsAsync(
        string tenantId,
        List<string> workflowIds,
        string mergedName,
        CancellationToken ct = default)
    {
        await Task.Delay(200, ct); // Simulate merge

        var mergedWorkflow = new DynamicWorkflowDefinition
        {
            TenantId = tenantId,
            WorkflowName = mergedName,
            Description = $"Merged from {workflowIds.Count} workflows",
        };

        var allSteps = new List<WorkflowStepDefinition>();
        var mergedVariables = new Dictionary<string, object>();

        foreach (var wfId in workflowIds)
        {
            var wf = await GetWorkflowDefinitionAsync(wfId, ct);
            if (wf != null)
            {
                allSteps.AddRange(wf.Steps);
                foreach (var kvp in wf.Variables)
                {
                    if (!mergedVariables.ContainsKey(kvp.Key))
                        mergedVariables[kvp.Key] = kvp.Value;
                }
            }
        }

        mergedWorkflow.Steps = allSteps;
        mergedWorkflow.Variables = mergedVariables;

        _workflows[mergedWorkflow.WorkflowId] = mergedWorkflow;

        _logger.LogInformation(
            "Workflows merged: Tenant={TenantId}, Count={Count}, Steps={StepCount}, Name={MergedName}",
            tenantId, workflowIds.Count, allSteps.Count, mergedName);

        return mergedWorkflow;
    }

    // Validation and publishing
    public async Task<Dictionary<string, object>> ValidateWorkflowAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate validation

        var workflow = await GetWorkflowDefinitionAsync(workflowId, ct);
        if (workflow == null)
        {
            throw new KeyNotFoundException($"Workflow not found: {workflowId}");
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        // Check for empty workflow
        if (workflow.Steps.Count == 0)
        {
            errors.Add("Workflow must contain at least one step");
        }

        // Check for circular dependencies
        if (HasCircularDependency(workflow))
        {
            errors.Add("Workflow contains circular dependencies");
        }

        // Check for unreachable steps
        var unreachable = FindUnreachableSteps(workflow);
        if (unreachable.Count > 0)
        {
            warnings.Add($"Found {unreachable.Count} unreachable steps");
        }

        // Check loop limits
        foreach (var loop in workflow.Loops)
        {
            if (!loop.MaxIterations.HasValue || loop.MaxIterations.Value > 10000)
            {
                warnings.Add($"Loop {loop.LoopId} may exceed safe iteration limits");
            }
        }

        return new Dictionary<string, object>
        {
            ["isValid"] = errors.Count == 0,
            ["errorCount"] = errors.Count,
            ["warningCount"] = warnings.Count,
            ["errors"] = errors,
            ["warnings"] = warnings,
            ["stepCount"] = workflow.Steps.Count,
            ["hasLoops"] = workflow.Loops.Count > 0,
            ["hasParallel"] = workflow.ParallelGroups.Count > 0,
            ["hasConditionals"] = workflow.ConditionalBranches.Count > 0,
        };
    }

    public async Task<bool> PublishWorkflowAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var workflow = await GetWorkflowDefinitionAsync(workflowId, ct);
        if (workflow == null)
            return false;

        // Validate before publishing
        var validation = await ValidateWorkflowAsync(workflowId, ct);
        if (!(bool)validation["isValid"])
        {
            _logger.LogError(
                "Cannot publish workflow: {WorkflowId} has validation errors",
                workflowId);
            return false;
        }

        workflow.IsPublished = true;
        workflow.ModifiedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Workflow published: {WorkflowId}, Name: {WorkflowName}, Version: {Version}",
            workflowId, workflow.WorkflowName, workflow.Version);

        return true;
    }

    // Analytics
    public async Task<Dictionary<string, object>> GetCompositionAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var workflows = await GetWorkflowsAsync(tenantId, ct: ct);
        var blueprints = _blueprints.Values
            .Where(b => b.TenantId == tenantId)
            .ToList();

        var totalSteps = workflows.Sum(w => w.Steps.Count);
        var totalParallel = workflows.Sum(w => w.ParallelGroups.Count);
        var totalLoops = workflows.Sum(w => w.Loops.Count);

        return new Dictionary<string, object>
        {
            ["total_workflows"] = workflows.Count,
            ["published_workflows"] = workflows.Count(w => w.IsPublished),
            ["total_steps"] = totalSteps,
            ["average_steps_per_workflow"] = workflows.Count > 0 ? totalSteps / workflows.Count : 0,
            ["parallel_groups"] = totalParallel,
            ["loops"] = totalLoops,
            ["conditional_branches"] = workflows.Sum(w => w.ConditionalBranches.Count),
            ["composition_blueprints"] = blueprints.Count,
            ["composition_history"] = _compositionHistory.TryGetValue(tenantId, out var history) ? history.Count : 0,
        };
    }

    // Helpers
    private void BuildSequentialMapping(CompositionBlueprint blueprint, List<DynamicWorkflowDefinition> workflows)
    {
        var stepOrder = 0;
        foreach (var workflow in workflows)
        {
            blueprint.MappingRules[$"workflow_{stepOrder}"] = workflow.WorkflowId;
            stepOrder++;
        }
    }

    private void BuildParallelMapping(CompositionBlueprint blueprint, List<DynamicWorkflowDefinition> workflows)
    {
        for (int i = 0; i < workflows.Count; i++)
        {
            blueprint.MappingRules[$"parallel_group_{i}"] = new
            {
                workflowId = workflows[i].WorkflowId,
                parallelIndex = i
            };
        }
    }

    private void BuildConditionalMapping(CompositionBlueprint blueprint, List<DynamicWorkflowDefinition> workflows)
    {
        for (int i = 0; i < workflows.Count; i++)
        {
            blueprint.MappingRules[$"branch_{i}"] = new
            {
                workflowId = workflows[i].WorkflowId,
                condition = $"step_{i}_success"
            };
        }
    }

    private bool HasCircularDependency(DynamicWorkflowDefinition workflow)
    {
        foreach (var step in workflow.Steps)
        {
            if (HasCircularPath(step, workflow, new HashSet<string>()))
                return true;
        }
        return false;
    }

    private bool HasCircularPath(WorkflowStepDefinition step, DynamicWorkflowDefinition workflow, HashSet<string> visited)
    {
        if (visited.Contains(step.StepId))
            return true;

        visited.Add(step.StepId);

        foreach (var depId in step.DependsOn)
        {
            var depStep = workflow.Steps.FirstOrDefault(s => s.StepId == depId);
            if (depStep != null && HasCircularPath(depStep, workflow, new HashSet<string>(visited)))
                return true;
        }

        return false;
    }

    private List<WorkflowStepDefinition> FindUnreachableSteps(DynamicWorkflowDefinition workflow)
    {
        var reachable = new HashSet<string>();

        // Find entry points (steps with no dependencies)
        var entryPoints = workflow.Steps.Where(s => s.DependsOn.Count == 0).ToList();

        // BFS to find all reachable steps
        var queue = new Queue<string>(entryPoints.Select(s => s.StepId));
        while (queue.Count > 0)
        {
            var stepId = queue.Dequeue();
            if (reachable.Contains(stepId))
                continue;

            reachable.Add(stepId);

            var dependents = workflow.Steps
                .Where(s => s.DependsOn.Contains(stepId))
                .Select(s => s.StepId);

            foreach (var dep in dependents)
                queue.Enqueue(dep);
        }

        return workflow.Steps
            .Where(s => !reachable.Contains(s.StepId))
            .ToList();
    }
}
