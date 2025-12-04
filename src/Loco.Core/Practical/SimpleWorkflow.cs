// John Carmack: "Workflow should be obvious, not clever"
// Rob Pike: "Clear is better than clever"

using System.Collections.Concurrent;

namespace Loco.Core.Practical;

/// <summary>
/// Simple workflow - Chain steps, handle errors, track progress
/// No complex orchestration, just simple sequential or parallel execution
/// </summary>
public class SimpleWorkflow
{
    private readonly List<WorkflowStep> _steps = new();
    private readonly SimpleLogger _logger;
    private readonly Dictionary<string, object> _context = new();

    public string Id { get; } = Guid.NewGuid().ToString();
    public WorkflowStatus Status { get; private set; } = WorkflowStatus.NotStarted;
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? Error { get; private set; }

    public SimpleWorkflow(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleWorkflow));
    }

    // Add step
    public SimpleWorkflow AddStep(string name, Func<Dictionary<string, object>, Task<bool>> action)
    {
        _steps.Add(new WorkflowStep
        {
            Name = name,
            Action = action
        });
        return this;
    }

    // Add step with retry
    public SimpleWorkflow AddStepWithRetry(string name, Func<Dictionary<string, object>, Task<bool>> action, int maxRetries = 3)
    {
        _steps.Add(new WorkflowStep
        {
            Name = name,
            Action = action,
            MaxRetries = maxRetries
        });
        return this;
    }

    // Execute workflow
    public async Task<bool> ExecuteAsync()
    {
        Status = WorkflowStatus.Running;
        StartedAt = DateTime.UtcNow;

        _logger.Info($"Workflow {Id} started");

        try
        {
            foreach (var step in _steps)
            {
                step.Status = StepStatus.Running;
                step.StartedAt = DateTime.UtcNow;

                _logger.Info($"Step '{step.Name}' started");

                var success = await ExecuteStepWithRetryAsync(step);

                step.CompletedAt = DateTime.UtcNow;

                if (success)
                {
                    step.Status = StepStatus.Completed;
                    _logger.Info($"Step '{step.Name}' completed");
                }
                else
                {
                    step.Status = StepStatus.Failed;
                    Status = WorkflowStatus.Failed;
                    Error = $"Step '{step.Name}' failed";
                    _logger.Error($"Step '{step.Name}' failed");
                    return false;
                }
            }

            Status = WorkflowStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            _logger.Info($"Workflow {Id} completed");
            return true;
        }
        catch (Exception ex)
        {
            Status = WorkflowStatus.Failed;
            Error = ex.Message;
            CompletedAt = DateTime.UtcNow;
            _logger.Error($"Workflow {Id} failed", ex);
            return false;
        }
    }

    private async Task<bool> ExecuteStepWithRetryAsync(WorkflowStep step)
    {
        for (int attempt = 0; attempt <= step.MaxRetries; attempt++)
        {
            try
            {
                var success = await step.Action(_context);
                if (success) return true;

                if (attempt < step.MaxRetries)
                {
                    _logger.Warning($"Step '{step.Name}' failed, retrying ({attempt + 1}/{step.MaxRetries})");
                    await Task.Delay(1000 * (attempt + 1)); // Exponential backoff
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Step '{step.Name}' error (attempt {attempt + 1})", ex);
                if (attempt >= step.MaxRetries) throw;
                await Task.Delay(1000 * (attempt + 1));
            }
        }
        return false;
    }

    // Set context value
    public void SetContext(string key, object value)
    {
        _context[key] = value;
    }

    // Get context value
    public T? GetContext<T>(string key)
    {
        return _context.TryGetValue(key, out var value) ? (T)value : default;
    }

    // Get all steps
    public List<WorkflowStep> GetSteps() => _steps.ToList();

    public class WorkflowStep
    {
        public string Name { get; set; } = "";
        public Func<Dictionary<string, object>, Task<bool>> Action { get; set; } = null!;
        public int MaxRetries { get; set; } = 0;
        public StepStatus Status { get; set; } = StepStatus.Pending;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}

public enum WorkflowStatus
{
    NotStarted,
    Running,
    Completed,
    Failed,
    Cancelled
}

public enum StepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped
}

public class WorkflowStep
{
    public string Name { get; set; } = "";
    public Func<Dictionary<string, object>, Task<bool>> Action { get; set; } = null!;
    public int MaxRetries { get; set; } = 0;
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Parallel workflow - Execute steps in parallel
/// </summary>
public class ParallelWorkflow
{
    private readonly List<Func<Task<bool>>> _tasks = new();
    private readonly SimpleLogger _logger;

    public ParallelWorkflow(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(ParallelWorkflow));
    }

    public ParallelWorkflow AddTask(string name, Func<Task<bool>> task)
    {
        _tasks.Add(async () =>
        {
            _logger.Info($"Task '{name}' started");
            var result = await task();
            _logger.Info($"Task '{name}' {(result ? "completed" : "failed")}");
            return result;
        });
        return this;
    }

    public async Task<bool> ExecuteAsync()
    {
        var results = await Task.WhenAll(_tasks.Select(t => t()));
        return results.All(r => r);
    }
}

/// <summary>
/// Workflow builder
/// </summary>
public class WorkflowBuilder
{
    private readonly SimpleWorkflow _workflow;

    public WorkflowBuilder()
    {
        _workflow = new SimpleWorkflow();
    }

    public WorkflowBuilder Step(string name, Func<Task<bool>> action)
    {
        _workflow.AddStep(name, async ctx => await action());
        return this;
    }

    public WorkflowBuilder Step(string name, Func<Dictionary<string, object>, Task<bool>> action)
    {
        _workflow.AddStep(name, action);
        return this;
    }

    public WorkflowBuilder Retry(string name, Func<Task<bool>> action, int maxRetries = 3)
    {
        _workflow.AddStepWithRetry(name, async ctx => await action(), maxRetries);
        return this;
    }

    public SimpleWorkflow Build() => _workflow;
}

/// <summary>
/// Workflow executor with queue
/// </summary>
public class WorkflowExecutor
{
    private readonly SimpleMessageQueue<SimpleWorkflow> _queue;
    private readonly ConcurrentDictionary<string, SimpleWorkflow> _running = new();
    private readonly SimpleLogger _logger;

    public WorkflowExecutor(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(WorkflowExecutor));
        _queue = new SimpleMessageQueue<SimpleWorkflow>(capacity: 100, logger: _logger);

        _queue.StartConsumer(async workflow =>
        {
            _running[workflow.Id] = workflow;
            await workflow.ExecuteAsync();
            _running.TryRemove(workflow.Id, out _);
        }, consumerCount: 4);
    }

    public async Task<string> SubmitAsync(SimpleWorkflow workflow)
    {
        await _queue.EnqueueAsync(workflow);
        _logger.Info($"Workflow {workflow.Id} queued");
        return workflow.Id;
    }

    public SimpleWorkflow? GetWorkflow(string id)
    {
        return _running.TryGetValue(id, out var workflow) ? workflow : null;
    }

    public List<SimpleWorkflow> GetRunningWorkflows()
    {
        return _running.Values.ToList();
    }

    public async Task StopAsync()
    {
        await _queue.StopAsync();
    }

    public void Dispose()
    {
        _queue.Dispose();
    }
}

/// <summary>
/// Example workflows
/// </summary>
public class WorkflowExamples
{
    public static async Task Examples()
    {
        // Simple sequential workflow
        var workflow = new SimpleWorkflow();

        workflow
            .AddStep("Download", async ctx =>
            {
                await Task.Delay(100);
                ctx["file"] = "data.csv";
                return true;
            })
            .AddStep("Process", async ctx =>
            {
                var file = ctx["file"] as string;
                await Task.Delay(100);
                ctx["rows"] = 1000;
                return true;
            })
            .AddStep("Upload", async ctx =>
            {
                var rows = (int)ctx["rows"];
                await Task.Delay(100);
                return true;
            });

        var success = await workflow.ExecuteAsync();
        Console.WriteLine($"Workflow {(success ? "succeeded" : "failed")}");

        // Using builder
        var workflow2 = new WorkflowBuilder()
            .Step("Validate", async () =>
            {
                await Task.Delay(50);
                return true;
            })
            .Retry("SaveToDb", async () =>
            {
                await Task.Delay(50);
                return true;
            }, maxRetries: 3)
            .Step("SendNotification", async () =>
            {
                await Task.Delay(50);
                return true;
            })
            .Build();

        await workflow2.ExecuteAsync();

        // Parallel execution
        var parallel = new ParallelWorkflow();
        parallel
            .AddTask("Task1", async () =>
            {
                await Task.Delay(100);
                return true;
            })
            .AddTask("Task2", async () =>
            {
                await Task.Delay(100);
                return true;
            })
            .AddTask("Task3", async () =>
            {
                await Task.Delay(100);
                return true;
            });

        await parallel.ExecuteAsync();

        // Workflow executor
        var executor = new WorkflowExecutor();

        var workflowId = await executor.SubmitAsync(workflow);
        Console.WriteLine($"Submitted workflow: {workflowId}");

        // Check status
        await Task.Delay(1000);
        var running = executor.GetWorkflow(workflowId);
        if (running != null)
        {
            Console.WriteLine($"Status: {running.Status}");
        }

        await executor.StopAsync();
        executor.Dispose();
    }

    // Example: Data processing workflow
    public static async Task DataProcessingWorkflow()
    {
        var workflow = new WorkflowBuilder()
            .Step("ExtractData", async () =>
            {
                // Extract from source
                await Task.Delay(100);
                return true;
            })
            .Step("TransformData", async () =>
            {
                // Clean and transform
                await Task.Delay(100);
                return true;
            })
            .Step("ValidateData", async () =>
            {
                // Validate quality
                await Task.Delay(100);
                return true;
            })
            .Retry("LoadData", async () =>
            {
                // Load to destination
                await Task.Delay(100);
                return true;
            }, maxRetries: 3)
            .Build();

        await workflow.ExecuteAsync();
    }
}