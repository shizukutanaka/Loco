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
    /// Executes a single step with retry logic and timeout.
    /// </summary>
    private async Task<StepExecutionResult> ExecuteStepAsync(
        WorkflowStep step,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var retryCount = 0;
        var maxRetries = step.RetryCount ?? 0;
        var retryDelay = TimeSpan.FromSeconds(2); // Default 2 seconds

        if (!string.IsNullOrEmpty(step.RetryDelay) && TimeSpan.TryParse(step.RetryDelay, out var parsedDelay))
        {
            retryDelay = parsedDelay;
        }

        Exception? lastException = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    retryCount = attempt;
                    var delay = retryDelay * Math.Pow(2, attempt - 1); // Exponential backoff
                    _logger?.LogInformation("Retry {Attempt}/{MaxRetries} for step {StepId} after {Delay}ms",
                        attempt, maxRetries, step.Id, delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                }

                // Apply timeout if specified
                var timeout = step.TimeoutSeconds.HasValue
                    ? TimeSpan.FromSeconds(step.TimeoutSeconds.Value)
                    : TimeSpan.FromMinutes(5); // Default 5 minutes

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(timeout);

                // Execute the step based on type
                var success = await ExecuteStepByTypeAsync(step, timeoutCts.Token);

                if (success)
                {
                    stopwatch.Stop();
                    return new StepExecutionResult
                    {
                        StepId = step.Id,
                        Success = true,
                        Duration = stopwatch.Elapsed,
                        RetryCount = retryCount
                    };
                }
                else
                {
                    lastException = new Exception($"Step {step.Id} returned false");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // Don't retry if workflow was cancelled
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger?.LogWarning(ex, "Step {StepId} failed on attempt {Attempt}", step.Id, attempt + 1);
            }
        }

        stopwatch.Stop();
        _logger?.LogError("Step {StepId} failed after {RetryCount} retries", step.Id, retryCount);

        return new StepExecutionResult
        {
            StepId = step.Id,
            Success = false,
            Duration = stopwatch.Elapsed,
            ErrorMessage = lastException?.Message ?? "Unknown error",
            RetryCount = retryCount
        };
    }

    /// <summary>
    /// Executes a step based on its type.
    /// </summary>
    private async Task<bool> ExecuteStepByTypeAsync(WorkflowStep step, CancellationToken cancellationToken)
    {
        switch (step.Type.ToLowerInvariant())
        {
            case "log":
                return ExecuteLogStep(step);

            case "delay":
            case "sleep":
                return await ExecuteDelayStep(step, cancellationToken);

            case "process":
            case "command":
            case "exec":
                return await ExecuteProcessStep(step, cancellationToken);

            case "http":
            case "webhook":
                return await ExecuteHttpStep(step, cancellationToken);

            case "file":
            case "copy":
            case "move":
            case "delete":
                return await ExecuteFileStep(step);

            default:
                _logger?.LogWarning("Unknown step type: {Type} for step {StepId}", step.Type, step.Id);
                return false;
        }
    }

    private bool ExecuteLogStep(WorkflowStep step)
    {
        var message = step.Message ?? "";
        Console.WriteLine($"[LOG] {message}");
        _logger?.LogInformation("{Message}", message);

        if (step.SaveOutput != null)
        {
            _context[step.SaveOutput] = message;
        }

        return true;
    }

    private async Task<bool> ExecuteDelayStep(WorkflowStep step, CancellationToken cancellationToken)
    {
        if (TimeSpan.TryParse(step.Duration, out var delay))
        {
            Console.WriteLine($"[DELAY] Waiting for {delay}...");
            await Task.Delay(delay, cancellationToken);
            return true;
        }

        _logger?.LogError("Invalid duration format for delay step: {Duration}", step.Duration);
        return false;
    }

    private async Task<bool> ExecuteProcessStep(WorkflowStep step, CancellationToken cancellationToken)
    {
        var command = step.Command ?? "";
        var arguments = step.Arguments ?? "";

        Console.WriteLine($"[PROCESS] Executing: {command} {arguments}");

        try
        {
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(step.WorkingDirectory))
            {
                processInfo.WorkingDirectory = step.WorkingDirectory;
            }

            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process == null)
            {
                _logger?.LogError("Failed to start process: {Command}", command);
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            if (step.SaveOutput != null && !string.IsNullOrEmpty(output))
            {
                _context[step.SaveOutput] = output.Trim();
            }

            _context[$"{step.Id}_exitcode"] = process.ExitCode.ToString();
            _context[$"{step.Id}_success"] = (process.ExitCode == 0).ToString();

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Process execution failed: {Command}", command);
            return false;
        }
    }

    private async Task<bool> ExecuteHttpStep(WorkflowStep step, CancellationToken cancellationToken)
    {
        var url = step.Url ?? "";
        var method = step.Method?.ToUpperInvariant() ?? "GET";

        Console.WriteLine($"[HTTP] {method} {url}");

        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(step.TimeoutSeconds ?? 30);

            System.Net.Http.HttpResponseMessage response;

            switch (method)
            {
                case "GET":
                    response = await httpClient.GetAsync(url, cancellationToken);
                    break;
                case "POST":
                    response = await httpClient.PostAsync(url, null, cancellationToken);
                    break;
                case "PUT":
                    response = await httpClient.PutAsync(url, null, cancellationToken);
                    break;
                case "DELETE":
                    response = await httpClient.DeleteAsync(url, cancellationToken);
                    break;
                default:
                    _logger?.LogError("Unsupported HTTP method: {Method}", method);
                    return false;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (step.SaveOutput != null)
            {
                _context[step.SaveOutput] = content;
            }

            _context[$"{step.Id}_statuscode"] = ((int)response.StatusCode).ToString();
            _context[$"{step.Id}_success"] = response.IsSuccessStatusCode.ToString();

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "HTTP request failed: {Url}", url);
            return false;
        }
    }

    private Task<bool> ExecuteFileStep(WorkflowStep step)
    {
        try
        {
            var type = step.Type.ToLowerInvariant();

            switch (type)
            {
                case "copy":
                    if (!string.IsNullOrEmpty(step.SourcePath) && !string.IsNullOrEmpty(step.DestinationPath))
                    {
                        Console.WriteLine($"[FILE] Copying {step.SourcePath} to {step.DestinationPath}");
                        File.Copy(step.SourcePath, step.DestinationPath, overwrite: true);
                        return Task.FromResult(true);
                    }
                    break;

                case "move":
                    if (!string.IsNullOrEmpty(step.SourcePath) && !string.IsNullOrEmpty(step.DestinationPath))
                    {
                        Console.WriteLine($"[FILE] Moving {step.SourcePath} to {step.DestinationPath}");
                        File.Move(step.SourcePath, step.DestinationPath, overwrite: true);
                        return Task.FromResult(true);
                    }
                    break;

                case "delete":
                    if (!string.IsNullOrEmpty(step.Path))
                    {
                        Console.WriteLine($"[FILE] Deleting {step.Path}");
                        if (File.Exists(step.Path))
                        {
                            File.Delete(step.Path);
                        }
                        else if (Directory.Exists(step.Path))
                        {
                            Directory.Delete(step.Path, step.Recursive ?? false);
                        }
                        return Task.FromResult(true);
                    }
                    break;
            }

            _logger?.LogError("Invalid file operation parameters for step {StepId}", step.Id);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "File operation failed for step {StepId}", step.Id);
            return Task.FromResult(false);
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
