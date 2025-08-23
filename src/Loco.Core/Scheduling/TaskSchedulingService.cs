using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;

namespace Loco.Core.Scheduling
{
    /// <summary>
    /// Advanced task scheduling service with cron-like support
    /// Implements efficient scheduling with minimal overhead
    /// </summary>
    public sealed class TaskSchedulingService : IDisposable
    {
        private readonly ILogger<TaskSchedulingService> _logger;
        private readonly ConcurrentDictionary<string, ScheduledTask> _scheduledTasks;
        private readonly ConcurrentDictionary<string, Timer> _taskTimers;
        private readonly SemaphoreSlim _executionSemaphore;
        private readonly TaskExecutionHistory _history;
        private readonly int _maxConcurrentTasks;
        private bool _disposed;

        public TaskSchedulingService(ILogger<TaskSchedulingService> logger = null, int maxConcurrentTasks = 10)
        {
            _logger = logger;
            _maxConcurrentTasks = maxConcurrentTasks;
            _scheduledTasks = new ConcurrentDictionary<string, ScheduledTask>();
            _taskTimers = new ConcurrentDictionary<string, Timer>();
            _executionSemaphore = new SemaphoreSlim(maxConcurrentTasks, maxConcurrentTasks);
            _history = new TaskExecutionHistory(maxItems: 100);
        }

        /// <summary>
        /// Schedule a task with cron expression
        /// </summary>
        public ScheduleResult ScheduleTask(
            string taskId,
            string cronExpression,
            Func<CancellationToken, Task> taskAction,
            ScheduleOptions options = null)
        {
            try
            {
                options ??= ScheduleOptions.Default;
                
                // Parse cron expression
                var cron = new SimpleCronParser(cronExpression);

                var scheduledTask = new ScheduledTask
                {
                    Id = taskId,
                    CronExpression = cronExpression,
                    CronParser = cron,
                    TaskAction = taskAction,
                    Options = options,
                    CreatedAt = DateTime.UtcNow,
                    IsEnabled = true
                };

                // Calculate next run time
                scheduledTask.NextRunTime = cron.GetNextOccurrence(DateTime.UtcNow);

                // Add or update task
                _scheduledTasks.AddOrUpdate(taskId, scheduledTask, (_, __) => scheduledTask);

                // Create timer for the task
                CreateTaskTimer(scheduledTask);

                _logger?.LogInformation("Scheduled task {TaskId} with cron: {Cron}, next run: {NextRun}",
                    taskId, cronExpression, scheduledTask.NextRunTime);

                return new ScheduleResult
                {
                    Success = true,
                    TaskId = taskId,
                    NextRunTime = scheduledTask.NextRunTime,
                    Message = $"Task scheduled successfully. Next run: {scheduledTask.NextRunTime:yyyy-MM-dd HH:mm:ss}"
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to schedule task {TaskId}", taskId);
                return new ScheduleResult
                {
                    Success = false,
                    TaskId = taskId,
                    Message = $"Failed to schedule task: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Schedule a one-time task
        /// </summary>
        public ScheduleResult ScheduleOneTimeTask(
            string taskId,
            DateTime runTime,
            Func<CancellationToken, Task> taskAction,
            ScheduleOptions options = null)
        {
            try
            {
                options ??= ScheduleOptions.Default;

                var scheduledTask = new ScheduledTask
                {
                    Id = taskId,
                    TaskAction = taskAction,
                    Options = options,
                    CreatedAt = DateTime.UtcNow,
                    NextRunTime = runTime.ToUniversalTime(),
                    IsOneTime = true,
                    IsEnabled = true
                };

                _scheduledTasks.AddOrUpdate(taskId, scheduledTask, (_, __) => scheduledTask);
                CreateTaskTimer(scheduledTask);

                return new ScheduleResult
                {
                    Success = true,
                    TaskId = taskId,
                    NextRunTime = scheduledTask.NextRunTime,
                    Message = $"One-time task scheduled for {scheduledTask.NextRunTime:yyyy-MM-dd HH:mm:ss}"
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to schedule one-time task {TaskId}", taskId);
                return new ScheduleResult
                {
                    Success = false,
                    TaskId = taskId,
                    Message = $"Failed to schedule task: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Schedule a recurring task with interval
        /// </summary>
        public ScheduleResult ScheduleRecurringTask(
            string taskId,
            TimeSpan interval,
            Func<CancellationToken, Task> taskAction,
            ScheduleOptions options = null)
        {
            try
            {
                options ??= ScheduleOptions.Default;

                var scheduledTask = new ScheduledTask
                {
                    Id = taskId,
                    Interval = interval,
                    TaskAction = taskAction,
                    Options = options,
                    CreatedAt = DateTime.UtcNow,
                    NextRunTime = DateTime.UtcNow.Add(interval),
                    IsInterval = true,
                    IsEnabled = true
                };

                _scheduledTasks.AddOrUpdate(taskId, scheduledTask, (_, __) => scheduledTask);
                CreateTaskTimer(scheduledTask);

                return new ScheduleResult
                {
                    Success = true,
                    TaskId = taskId,
                    NextRunTime = scheduledTask.NextRunTime,
                    Message = $"Recurring task scheduled with interval: {interval}"
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to schedule recurring task {TaskId}", taskId);
                return new ScheduleResult
                {
                    Success = false,
                    TaskId = taskId,
                    Message = $"Failed to schedule task: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Cancel a scheduled task
        /// </summary>
        public bool CancelTask(string taskId)
        {
            try
            {
                if (_taskTimers.TryRemove(taskId, out var timer))
                {
                    timer?.Dispose();
                }

                if (_scheduledTasks.TryRemove(taskId, out var task))
                {
                    _logger?.LogInformation("Cancelled task {TaskId}", taskId);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to cancel task {TaskId}", taskId);
                return false;
            }
        }

        /// <summary>
        /// Pause a scheduled task
        /// </summary>
        public bool PauseTask(string taskId)
        {
            if (_scheduledTasks.TryGetValue(taskId, out var task))
            {
                task.IsEnabled = false;
                
                if (_taskTimers.TryGetValue(taskId, out var timer))
                {
                    timer?.Change(Timeout.Infinite, Timeout.Infinite);
                }

                _logger?.LogInformation("Paused task {TaskId}", taskId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resume a paused task
        /// </summary>
        public bool ResumeTask(string taskId)
        {
            if (_scheduledTasks.TryGetValue(taskId, out var task))
            {
                task.IsEnabled = true;
                task.NextRunTime = CalculateNextRunTime(task);
                CreateTaskTimer(task);

                _logger?.LogInformation("Resumed task {TaskId}, next run: {NextRun}",
                    taskId, task.NextRunTime);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Execute a task immediately
        /// </summary>
        public async Task<ExecutionResult> ExecuteTaskNowAsync(string taskId)
        {
            if (!_scheduledTasks.TryGetValue(taskId, out var task))
            {
                return new ExecutionResult
                {
                    Success = false,
                    Message = "Task not found"
                };
            }

            using var cts = new CancellationTokenSource();
            return await ExecuteTaskAsync(task, cts.Token);
        }

        /// <summary>
        /// Get all scheduled tasks
        /// </summary>
        public List<ScheduledTaskInfo> GetScheduledTasks()
        {
            return _scheduledTasks.Values
                .Select(t => new ScheduledTaskInfo
                {
                    Id = t.Id,
                    CronExpression = t.CronExpression,
                    NextRunTime = t.NextRunTime,
                    LastRunTime = t.LastRunTime,
                    IsEnabled = t.IsEnabled,
                    IsOneTime = t.IsOneTime,
                    IsInterval = t.IsInterval,
                    Interval = t.Interval,
                    ExecutionCount = t.ExecutionCount,
                    LastExecutionStatus = t.LastExecutionStatus,
                    AverageExecutionTime = t.TotalExecutionTime.TotalMilliseconds / Math.Max(1, t.ExecutionCount)
                })
                .OrderBy(t => t.NextRunTime)
                .ToList();
        }

        /// <summary>
        /// Get task execution history
        /// </summary>
        public List<TaskExecutionRecord> GetExecutionHistory(string taskId = null, int count = 50)
        {
            return _history.GetRecords(taskId, count);
        }

        /// <summary>
        /// Get scheduling statistics
        /// </summary>
        public SchedulingStatistics GetStatistics()
        {
            var tasks = _scheduledTasks.Values.ToList();
            
            return new SchedulingStatistics
            {
                TotalTasks = tasks.Count,
                EnabledTasks = tasks.Count(t => t.IsEnabled),
                DisabledTasks = tasks.Count(t => !t.IsEnabled),
                OneTimeTasks = tasks.Count(t => t.IsOneTime),
                RecurringTasks = tasks.Count(t => !t.IsOneTime),
                TotalExecutions = tasks.Sum(t => t.ExecutionCount),
                SuccessfulExecutions = _history.SuccessCount,
                FailedExecutions = _history.FailedCount,
                AverageExecutionTime = _history.AverageExecutionTime,
                NextScheduledTask = tasks
                    .Where(t => t.IsEnabled && t.NextRunTime.HasValue)
                    .OrderBy(t => t.NextRunTime)
                    .FirstOrDefault()?.Id
            };
        }

        // Private methods
        private void CreateTaskTimer(ScheduledTask task)
        {
            // Cancel existing timer if any
            if (_taskTimers.TryRemove(task.Id, out var existingTimer))
            {
                existingTimer?.Dispose();
            }

            if (!task.IsEnabled || !task.NextRunTime.HasValue)
                return;

            var delay = task.NextRunTime.Value - DateTime.UtcNow;
            if (delay.TotalMilliseconds <= 0)
            {
                // Execute immediately if past due
                _ = Task.Run(async () => await ExecuteScheduledTaskAsync(task));
                return;
            }

            var timer = new Timer(
                async _ => await ExecuteScheduledTaskAsync(task),
                null,
                delay,
                Timeout.InfiniteTimeSpan);

            _taskTimers[task.Id] = timer;
        }

        private async Task ExecuteScheduledTaskAsync(ScheduledTask task)
        {
            if (!task.IsEnabled)
                return;

            using var cts = new CancellationTokenSource();
            if (task.Options.Timeout.HasValue)
            {
                cts.CancelAfter(task.Options.Timeout.Value);
            }

            var result = await ExecuteTaskAsync(task, cts.Token);

            // Schedule next run if not one-time
            if (!task.IsOneTime)
            {
                task.NextRunTime = CalculateNextRunTime(task);
                if (task.NextRunTime.HasValue)
                {
                    CreateTaskTimer(task);
                }
            }
            else
            {
                // Remove one-time task after execution
                CancelTask(task.Id);
            }
        }

        private async Task<ExecutionResult> ExecuteTaskAsync(ScheduledTask task, CancellationToken cancellationToken)
        {
            await _executionSemaphore.WaitAsync();
            var startTime = DateTime.UtcNow;

            try
            {
                _logger?.LogDebug("Executing task {TaskId}", task.Id);
                
                task.LastRunTime = startTime;
                task.ExecutionCount++;

                // Execute with retry logic
                var retryCount = 0;
                Exception lastException = null;

                while (retryCount <= task.Options.MaxRetries)
                {
                    try
                    {
                        await task.TaskAction(cancellationToken);
                        
                        var executionTime = DateTime.UtcNow - startTime;
                        task.TotalExecutionTime += executionTime;
                        task.LastExecutionStatus = "Success";

                        _history.AddRecord(new TaskExecutionRecord
                        {
                            TaskId = task.Id,
                            ExecutionTime = startTime,
                            Duration = executionTime,
                            Success = true,
                            Message = "Task completed successfully"
                        });

                        _logger?.LogInformation("Task {TaskId} executed successfully in {Duration}ms",
                            task.Id, executionTime.TotalMilliseconds);

                        return new ExecutionResult
                        {
                            Success = true,
                            ExecutionTime = executionTime,
                            Message = "Task executed successfully"
                        };
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        retryCount++;

                        if (retryCount <= task.Options.MaxRetries)
                        {
                            _logger?.LogWarning("Task {TaskId} failed, retry {Retry}/{MaxRetries}",
                                task.Id, retryCount, task.Options.MaxRetries);
                            
                            await Task.Delay(task.Options.RetryDelay, cancellationToken);
                        }
                    }
                }

                // All retries failed
                var failedExecutionTime = DateTime.UtcNow - startTime;
                task.LastExecutionStatus = $"Failed: {lastException?.Message}";

                _history.AddRecord(new TaskExecutionRecord
                {
                    TaskId = task.Id,
                    ExecutionTime = startTime,
                    Duration = failedExecutionTime,
                    Success = false,
                    Message = lastException?.Message ?? "Unknown error"
                });

                _logger?.LogError(lastException, "Task {TaskId} failed after {Retries} retries",
                    task.Id, task.Options.MaxRetries);

                return new ExecutionResult
                {
                    Success = false,
                    ExecutionTime = failedExecutionTime,
                    Message = $"Task failed: {lastException?.Message}",
                    Exception = lastException
                };
            }
            finally
            {
                _executionSemaphore.Release();
            }
        }

        private DateTime? CalculateNextRunTime(ScheduledTask task)
        {
            if (task.IsOneTime)
                return null;

            if (task.IsInterval && task.Interval.HasValue)
            {
                return DateTime.UtcNow.Add(task.Interval.Value);
            }

            if (task.CronParser != null)
            {
                return task.CronParser.GetNextOccurrence(DateTime.UtcNow);
            }

            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Cancel all tasks
            foreach (var taskId in _scheduledTasks.Keys.ToList())
            {
                CancelTask(taskId);
            }

            _executionSemaphore?.Dispose();
        }
    }

    /// <summary>
    /// Simple cron expression parser
    /// Supports: minute hour day month dayOfWeek
    /// </summary>
    public class SimpleCronParser
    {
        private readonly string _expression;
        private readonly string[] _fields;

        public SimpleCronParser(string expression)
        {
            _expression = expression;
            _fields = expression.Split(' ');
            
            if (_fields.Length < 5)
            {
                throw new ArgumentException("Invalid cron expression. Expected at least 5 fields.");
            }
        }

        public DateTime GetNextOccurrence(DateTime baseTime)
        {
            var next = baseTime.AddMinutes(1);
            next = new DateTime(next.Year, next.Month, next.Day, next.Hour, next.Minute, 0);

            for (int attempts = 0; attempts < 366 * 24 * 60; attempts++)
            {
                if (IsMatch(next))
                {
                    return next;
                }
                next = next.AddMinutes(1);
            }

            throw new InvalidOperationException("Unable to find next occurrence");
        }

        private bool IsMatch(DateTime time)
        {
            return MatchField(_fields[0], time.Minute) &&      // minute
                   MatchField(_fields[1], time.Hour) &&        // hour
                   MatchField(_fields[2], time.Day) &&         // day
                   MatchField(_fields[3], time.Month) &&       // month
                   MatchDayOfWeek(_fields[4], time.DayOfWeek); // day of week
        }

        private bool MatchField(string field, int value)
        {
            if (field == "*") return true;

            // Handle ranges (e.g., "10-20")
            if (field.Contains("-"))
            {
                var parts = field.Split('-');
                if (parts.Length == 2 && 
                    int.TryParse(parts[0], out var start) && 
                    int.TryParse(parts[1], out var end))
                {
                    return value >= start && value <= end;
                }
            }

            // Handle lists (e.g., "1,15,30")
            if (field.Contains(","))
            {
                var values = field.Split(',');
                return values.Any(v => int.TryParse(v, out var parsed) && parsed == value);
            }

            // Handle step values (e.g., "*/5")
            if (field.StartsWith("*/"))
            {
                if (int.TryParse(field.Substring(2), out var step))
                {
                    return value % step == 0;
                }
            }

            // Direct value match
            return int.TryParse(field, out var fieldValue) && fieldValue == value;
        }

        private bool MatchDayOfWeek(string field, DayOfWeek dayOfWeek)
        {
            if (field == "*") return true;

            int dayValue = (int)dayOfWeek;
            
            // Handle both numeric and text representations
            var dayMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["SUN"] = 0, ["MON"] = 1, ["TUE"] = 2, ["WED"] = 3,
                ["THU"] = 4, ["FRI"] = 5, ["SAT"] = 6
            };

            // Replace text days with numbers
            foreach (var kvp in dayMap)
            {
                field = field.Replace(kvp.Key, kvp.Value.ToString());
            }

            return MatchField(field, dayValue);
        }
    }

    // Supporting classes
    public class ScheduledTask
    {
        public string Id { get; set; }
        public string CronExpression { get; set; }
        public SimpleCronParser CronParser { get; set; }
        public TimeSpan? Interval { get; set; }
        public Func<CancellationToken, Task> TaskAction { get; set; }
        public ScheduleOptions Options { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? NextRunTime { get; set; }
        public DateTime? LastRunTime { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsOneTime { get; set; }
        public bool IsInterval { get; set; }
        public int ExecutionCount { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public string LastExecutionStatus { get; set; }
    }

    public class ScheduleOptions
    {
        public int MaxRetries { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan? Timeout { get; set; } = TimeSpan.FromMinutes(5);
        public bool CatchUpMissedRuns { get; set; } = false;

        public static ScheduleOptions Default => new ScheduleOptions();
        
        public static ScheduleOptions NoRetry => new ScheduleOptions
        {
            MaxRetries = 0
        };
        
        public static ScheduleOptions LongRunning => new ScheduleOptions
        {
            Timeout = TimeSpan.FromHours(1),
            MaxRetries = 1
        };
    }

    public class ScheduleResult
    {
        public bool Success { get; set; }
        public string TaskId { get; set; }
        public DateTime? NextRunTime { get; set; }
        public string Message { get; set; }
    }

    public class ExecutionResult
    {
        public bool Success { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
    }

    public class ScheduledTaskInfo
    {
        public string Id { get; set; }
        public string CronExpression { get; set; }
        public DateTime? NextRunTime { get; set; }
        public DateTime? LastRunTime { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsOneTime { get; set; }
        public bool IsInterval { get; set; }
        public TimeSpan? Interval { get; set; }
        public int ExecutionCount { get; set; }
        public string LastExecutionStatus { get; set; }
        public double AverageExecutionTime { get; set; }
    }

    public class TaskExecutionRecord
    {
        public string TaskId { get; set; }
        public DateTime ExecutionTime { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class TaskExecutionHistory
    {
        private readonly Queue<TaskExecutionRecord> _records;
        private readonly int _maxItems;
        private int _successCount;
        private int _failedCount;
        private double _totalExecutionTime;

        public TaskExecutionHistory(int maxItems = 100)
        {
            _maxItems = maxItems;
            _records = new Queue<TaskExecutionRecord>(maxItems);
        }

        public void AddRecord(TaskExecutionRecord record)
        {
            lock (_records)
            {
                _records.Enqueue(record);
                if (_records.Count > _maxItems)
                {
                    _records.Dequeue();
                }

                if (record.Success)
                    _successCount++;
                else
                    _failedCount++;

                _totalExecutionTime += record.Duration.TotalMilliseconds;
            }
        }

        public List<TaskExecutionRecord> GetRecords(string taskId = null, int count = 50)
        {
            lock (_records)
            {
                var query = _records.AsEnumerable();
                
                if (!string.IsNullOrEmpty(taskId))
                {
                    query = query.Where(r => r.TaskId == taskId);
                }

                return query.TakeLast(count).ToList();
            }
        }

        public int SuccessCount => _successCount;
        public int FailedCount => _failedCount;
        public double AverageExecutionTime => (_successCount + _failedCount) > 0 
            ? _totalExecutionTime / (_successCount + _failedCount) 
            : 0;
    }

    public class SchedulingStatistics
    {
        public int TotalTasks { get; set; }
        public int EnabledTasks { get; set; }
        public int DisabledTasks { get; set; }
        public int OneTimeTasks { get; set; }
        public int RecurringTasks { get; set; }
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public double AverageExecutionTime { get; set; }
        public string NextScheduledTask { get; set; }
    }
}
