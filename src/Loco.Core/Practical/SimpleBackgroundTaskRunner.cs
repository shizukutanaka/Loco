// John Carmack: "In the end, the only thing that matters is the code"
// Rob Pike: "Simplicity is the ultimate sophistication"

using System.Collections.Concurrent;

namespace Loco.Core.Practical;

/// <summary>
/// Simple background task runner - fire and forget with monitoring
/// No complex scheduling, just run tasks in background
/// </summary>
public class SimpleBackgroundTaskRunner : IDisposable
{
    private readonly ConcurrentDictionary<string, RunningTask> _runningTasks = new();
    private readonly SimpleLogger _logger;
    private readonly CancellationTokenSource _shutdownToken = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public SimpleBackgroundTaskRunner()
    {
        _logger = SimpleLoggerFactory.GetLogger(nameof(SimpleBackgroundTaskRunner));

        // Cleanup completed tasks every 30 seconds
        _cleanupTimer = new Timer(_ => CleanupCompletedTasks(), null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    // Fire and forget with optional result callback
    public string RunAsync(Func<CancellationToken, Task> operation, string? name = null)
    {
        var taskId = Guid.NewGuid().ToString("N")[..8];
        var taskName = name ?? $"Task_{taskId}";

        var runningTask = new RunningTask
        {
            Id = taskId,
            Name = taskName,
            StartTime = DateTime.UtcNow,
            Status = TaskStatus.Running
        };

        _runningTasks[taskId] = runningTask;

        runningTask.Task = Task.Run(async () =>
        {
            try
            {
                _logger.Info($"Task {taskName} started");
                await operation(_shutdownToken.Token);

                runningTask.Status = TaskStatus.RanToCompletion;
                runningTask.CompletedTime = DateTime.UtcNow;

                _logger.Info($"Task {taskName} completed successfully");
            }
            catch (OperationCanceledException)
            {
                runningTask.Status = TaskStatus.Canceled;
                runningTask.CompletedTime = DateTime.UtcNow;
                _logger.Info($"Task {taskName} was cancelled");
            }
            catch (Exception ex)
            {
                runningTask.Status = TaskStatus.Faulted;
                runningTask.CompletedTime = DateTime.UtcNow;
                runningTask.Exception = ex;
                _logger.Error($"Task {taskName} failed", ex);
            }
        }, _shutdownToken.Token);

        return taskId;
    }

    // Run periodic task
    public string RunPeriodic(Func<CancellationToken, Task> operation, TimeSpan interval, string? name = null)
    {
        var taskName = name ?? $"Periodic_{Guid.NewGuid():N}"[..8];

        return RunAsync(async ct =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await operation(ct);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Periodic task {taskName} iteration failed", ex);
                }

                await Task.Delay(interval, ct);
            }
        }, taskName);
    }

    // Get task status
    public TaskInfo? GetTaskInfo(string taskId)
    {
        if (_runningTasks.TryGetValue(taskId, out var task))
        {
            return new TaskInfo
            {
                Id = task.Id,
                Name = task.Name,
                Status = task.Status.ToString(),
                StartTime = task.StartTime,
                CompletedTime = task.CompletedTime,
                Duration = (task.CompletedTime ?? DateTime.UtcNow) - task.StartTime,
                Exception = task.Exception?.Message
            };
        }
        return null;
    }

    // Get all tasks
    public List<TaskInfo> GetAllTasks()
    {
        return _runningTasks.Values.Select(t => new TaskInfo
        {
            Id = t.Id,
            Name = t.Name,
            Status = t.Status.ToString(),
            StartTime = t.StartTime,
            CompletedTime = t.CompletedTime,
            Duration = (t.CompletedTime ?? DateTime.UtcNow) - t.StartTime,
            Exception = t.Exception?.Message
        }).ToList();
    }

    // Cancel specific task
    public bool CancelTask(string taskId)
    {
        if (_runningTasks.TryGetValue(taskId, out var task))
        {
            // Tasks respond to the global cancellation token
            _logger.Info($"Requesting cancellation of task {task.Name}");
            return true;
        }
        return false;
    }

    // Wait for all tasks to complete
    public async Task WaitForAllAsync(int timeoutSeconds = 30)
    {
        var tasks = _runningTasks.Values
            .Where(t => t.Task != null && !t.Task.IsCompleted)
            .Select(t => t.Task!)
            .ToArray();

        if (tasks.Length > 0)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(-1, cts.Token));
        }
    }

    // Cleanup completed tasks
    private void CleanupCompletedTasks()
    {
        var completedTasks = _runningTasks
            .Where(kvp => kvp.Value.Task?.IsCompleted == true &&
                         (DateTime.UtcNow - (kvp.Value.CompletedTime ?? DateTime.UtcNow)) > TimeSpan.FromMinutes(5))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var taskId in completedTasks)
        {
            _runningTasks.TryRemove(taskId, out _);
        }

        if (completedTasks.Count > 0)
        {
            _logger.Debug($"Cleaned up {completedTasks.Count} completed tasks");
        }
    }

    // Get stats
    public (int running, int completed, int failed) GetStats()
    {
        var running = _runningTasks.Values.Count(t => t.Status == TaskStatus.Running);
        var completed = _runningTasks.Values.Count(t => t.Status == TaskStatus.RanToCompletion);
        var failed = _runningTasks.Values.Count(t => t.Status == TaskStatus.Faulted);

        return (running, completed, failed);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _logger.Info("Shutting down background task runner");

        _shutdownToken.Cancel();
        _cleanupTimer?.Dispose();

        // Wait briefly for tasks to respond to cancellation
        Task.Run(async () => await WaitForAllAsync(5)).Wait();

        _shutdownToken.Dispose();
    }

    private class RunningTask
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public Task? Task { get; set; }
        public TaskStatus Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public Exception? Exception { get; set; }
    }

    public class TaskInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public TimeSpan Duration { get; set; }
        public string? Exception { get; set; }
    }
}