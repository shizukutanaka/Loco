using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Workflows;

/// <summary>
/// Execution result for a single step.
/// </summary>
public class StepExecutionResult
{
    public string StepId { get; set; } = "";
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> Outputs { get; set; } = new();
    public int RetryCount { get; set; }
}

/// <summary>
/// Result of parallel workflow execution.
/// </summary>
public class ParallelExecutionResult
{
    public bool Success { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public int StepsExecuted { get; set; }
    public int StepsFailed { get; set; }
    public int StepsSkipped { get; set; }
    public List<StepExecutionResult> StepResults { get; set; } = new();
    public Dictionary<string, string> Context { get; set; } = new();
}

/// <summary>
/// Engine for executing workflow steps in parallel based on dependency graph.
/// </summary>
public class ParallelExecutionEngine
{
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, StepExecutionResult> _results = new();
    private readonly ConcurrentDictionary<string, string> _context = new();
    private readonly SemaphoreSlim _maxParallelism;

    public ParallelExecutionEngine(ILogger? logger = null, int maxDegreeOfParallelism = 4)
    {
        _logger = logger;
        _maxParallelism = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);
    }

    /// <summary>
    /// Executes workflow steps in parallel based on dependency graph.
    /// </summary>
    public async Task<ParallelExecutionResult> ExecuteAsync(
        WorkflowDefinition workflow,
        CancellationToken cancellationToken = default)
    {
        var overallStopwatch = Stopwatch.StartNew();
        _results.Clear();
        _context.Clear();

        try
        {
            // Build dependency graph
            var dependencyGraph = BuildDependencyGraph(workflow);

            // Execute steps in topological order with parallelism
            await ExecuteWithDependenciesAsync(workflow, dependencyGraph, cancellationToken);

            overallStopwatch.Stop();

            // Collect results
            var stepResults = _results.Values.ToList();
            var success = stepResults.All(r => r.Success);

            return new ParallelExecutionResult
            {
                Success = success,
                TotalDuration = overallStopwatch.Elapsed,
                StepsExecuted = stepResults.Count(r => r.Success),
                StepsFailed = stepResults.Count(r => !r.Success),
                StepsSkipped = workflow.Steps.Count - stepResults.Count,
                StepResults = stepResults,
                Context = new Dictionary<string, string>(_context)
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Parallel execution failed");
            overallStopwatch.Stop();

            return new ParallelExecutionResult
            {
                Success = false,
                TotalDuration = overallStopwatch.Elapsed,
                StepsExecuted = _results.Count(r => r.Value.Success),
                StepsFailed = _results.Count(r => !r.Value.Success) + 1,
                StepsSkipped = workflow.Steps.Count - _results.Count,
                StepResults = _results.Values.ToList(),
                Context = new Dictionary<string, string>(_context)
            };
        }
    }

    /// <summary>
    /// Builds dependency graph from workflow steps.
    /// </summary>
    private Dictionary<string, List<string>> BuildDependencyGraph(WorkflowDefinition workflow)
    {
        var graph = new Dictionary<string, List<string>>();

        foreach (var step in workflow.Steps)
        {
            if (!graph.ContainsKey(step.Id))
                graph[step.Id] = new List<string>();

            if (step.DependsOn != null && step.DependsOn.Any())
            {
                graph[step.Id].AddRange(step.DependsOn);
            }
        }

        return graph;
    }

    /// <summary>
    /// Executes steps respecting dependencies with parallel execution.
    /// </summary>
    private async Task ExecuteWithDependenciesAsync(
        WorkflowDefinition workflow,
        Dictionary<string, List<string>> dependencyGraph,
        CancellationToken cancellationToken)
    {
        var executed = new ConcurrentDictionary<string, bool>();
        var executing = new ConcurrentDictionary<string, Task>();

        async Task ExecuteStepAndDependenciesAsync(string stepId)
        {
            // Check if already executed or executing
            if (executed.ContainsKey(stepId) || executing.ContainsKey(stepId))
                return;

            var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId);
            if (step == null)
                return;

            // Create task placeholder
            var tcs = new TaskCompletionSource<bool>();
            if (!executing.TryAdd(stepId, tcs.Task))
                return; // Another thread is handling this

            try
            {
                // Execute dependencies first
                var dependencies = dependencyGraph.ContainsKey(stepId)
                    ? dependencyGraph[stepId]
                    : new List<string>();

                if (dependencies.Any())
                {
                    var dependencyTasks = dependencies
                        .Select(depId => ExecuteStepAndDependenciesAsync(depId))
                        .ToList();

                    await Task.WhenAll(dependencyTasks);

                    // Check if any dependency failed
                    var dependencyFailed = dependencies.Any(depId =>
                    {
                        _results.TryGetValue(depId, out var result);
                        return result != null && !result.Success;
                    });

                    if (dependencyFailed)
                    {
                        _logger?.LogWarning("Skipping step {StepId} due to failed dependency", stepId);
                        _results[stepId] = new StepExecutionResult
                        {
                            StepId = stepId,
                            Success = false,
                            ErrorMessage = "Dependency failed"
                        };
                        executed[stepId] = false;
                        tcs.SetResult(false);
                        return;
                    }
                }

                // Acquire semaphore for parallel execution limit
                await _maxParallelism.WaitAsync(cancellationToken);

                try
                {
                    // Execute the step
                    _logger?.LogInformation("Executing step: {StepId} ({StepName})", step.Id, step.Name);
                    var result = await ExecuteStepAsync(step, cancellationToken);
                    _results[stepId] = result;
                    executed[stepId] = result.Success;
                    tcs.SetResult(result.Success);
                }
                finally
                {
                    _maxParallelism.Release();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing step {StepId}", stepId);
                _results[stepId] = new StepExecutionResult
                {
                    StepId = stepId,
                    Success = false,
                    ErrorMessage = ex.Message
                };
                executed[stepId] = false;
                tcs.SetException(ex);
            }
        }

        // Start execution from all root nodes (steps with no dependencies)
        var rootSteps = workflow.Steps
            .Where(s => !dependencyGraph.ContainsKey(s.Id) || !dependencyGraph[s.Id].Any())
            .Select(s => s.Id)
            .ToList();

        if (!rootSteps.Any())
        {
            // No dependencies defined, execute all in parallel
            rootSteps = workflow.Steps.Select(s => s.Id).ToList();
        }

        // Execute all steps (dependencies will be handled recursively)
        var allTasks = workflow.Steps
            .Select(s => ExecuteStepAndDependenciesAsync(s.Id))
            .ToList();

        await Task.WhenAll(allTasks);
    }

    /// <summary>
    /// Executes a single step.
    /// </summary>
    private async Task<StepExecutionResult> ExecuteStepAsync(
        WorkflowStep step,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var retryCount = 0;

        try
        {
            // Simulate step execution (replace with actual execution logic)
            await Task.Delay(100, cancellationToken); // Placeholder

            // For now, just log the step
            _logger?.LogInformation("Step {StepId} completed successfully", step.Id);

            // Save outputs to context
            if (step.SaveOutput != null)
            {
                _context[step.SaveOutput] = $"output-{step.Id}";
            }

            stopwatch.Stop();

            return new StepExecutionResult
            {
                StepId = step.Id,
                Success = true,
                Duration = stopwatch.Elapsed,
                RetryCount = retryCount
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Step {StepId} failed", step.Id);

            return new StepExecutionResult
            {
                StepId = step.Id,
                Success = false,
                Duration = stopwatch.Elapsed,
                ErrorMessage = ex.Message,
                RetryCount = retryCount
            };
        }
    }

    /// <summary>
    /// Generates a parallel execution report.
    /// </summary>
    public static string GenerateExecutionReport(ParallelExecutionResult result)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║ PARALLEL EXECUTION REPORT                                                     ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        var statusIcon = result.Success ? "✅" : "❌";
        var statusText = result.Success ? "SUCCESS" : "FAILED";

        sb.AppendLine($"Status: {statusIcon} {statusText}");
        sb.AppendLine($"Duration: {result.TotalDuration.TotalSeconds:F2}s");
        sb.AppendLine($"Steps Executed: {result.StepsExecuted}");
        sb.AppendLine($"Steps Failed: {result.StepsFailed}");
        sb.AppendLine($"Steps Skipped: {result.StepsSkipped}");
        sb.AppendLine();

        if (result.StepResults.Any())
        {
            sb.AppendLine("Step Results:");
            sb.AppendLine();

            foreach (var stepResult in result.StepResults.OrderBy(r => r.StepId))
            {
                var icon = stepResult.Success ? "✅" : "❌";
                sb.AppendLine($"{icon} {stepResult.StepId}");
                sb.AppendLine($"   Duration: {stepResult.Duration.TotalSeconds:F2}s");

                if (stepResult.RetryCount > 0)
                    sb.AppendLine($"   Retries: {stepResult.RetryCount}");

                if (!string.IsNullOrEmpty(stepResult.ErrorMessage))
                    sb.AppendLine($"   Error: {stepResult.ErrorMessage}");

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
