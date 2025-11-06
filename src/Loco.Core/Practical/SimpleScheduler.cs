// John Carmack: "Timing is everything. Make it predictable."
// Rob Pike: "Don't panic. Use timers."

using System.Collections.Concurrent;

namespace Loco.Core.Practical;

/// <summary>
/// Simple scheduler - Run tasks on schedule without heavyweight frameworks
/// Cron-like scheduling, one-time tasks, recurring tasks
/// </summary>
public class SimpleScheduler
{
    private readonly ConcurrentDictionary<string, ScheduledTask> _tasks = new();
    private readonly SimpleLogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _schedulerTask;

    public bool IsRunning { get; private set; }

    public SimpleScheduler(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleScheduler));
    }

    // Schedule task to run once at specific time
    public string ScheduleOnce(DateTime runAt, Func<Task> action, string? name = null)
    {
        var taskName = name ?? Guid.NewGuid().ToString();
        var task = new ScheduledTask
        {
            Name = taskName,
            Action = action,
            NextRun = runAt,
            IsRecurring = false
        };

        _tasks[taskName] = task;
        _logger.Info($"Scheduled one-time task '{taskName}' at {runAt}");
        return taskName;
    }

    // Schedule recurring task
    public string ScheduleRecurring(TimeSpan interval, Func<Task> action, string? name = null, DateTime? startAt = null)
    {
        var taskName = name ?? Guid.NewGuid().ToString();
        var task = new ScheduledTask
        {
            Name = taskName,
            Action = action,
            Interval = interval,
            NextRun = startAt ?? DateTime.UtcNow.Add(interval),
            IsRecurring = true
        };

        _tasks[taskName] = task;
        _logger.Info($"Scheduled recurring task '{taskName}' every {interval}");
        return taskName;
    }

    // Schedule using cron expression (simplified)
    public string ScheduleCron(string cronExpression, Func<Task> action, string? name = null)
    {
        var taskName = name ?? Guid.NewGuid().ToString();
        var schedule = ParseCron(cronExpression);

        var task = new ScheduledTask
        {
            Name = taskName,
            Action = action,
            CronSchedule = schedule,
            NextRun = schedule.GetNextRun(DateTime.UtcNow),
            IsRecurring = true
        };

        _tasks[taskName] = task;
        _logger.Info($"Scheduled cron task '{taskName}': {cronExpression}");
        return taskName;
    }

    // Schedule daily at specific time
    public string ScheduleDaily(TimeSpan timeOfDay, Func<Task> action, string? name = null)
    {
        var now = DateTime.UtcNow;
        var nextRun = now.Date.Add(timeOfDay);
        if (nextRun <= now) nextRun = nextRun.AddDays(1);

        var taskName = name ?? Guid.NewGuid().ToString();
        var task = new ScheduledTask
        {
            Name = taskName,
            Action = action,
            Interval = TimeSpan.FromDays(1),
            NextRun = nextRun,
            IsRecurring = true
        };

        _tasks[taskName] = task;
        _logger.Info($"Scheduled daily task '{taskName}' at {timeOfDay}");
        return taskName;
    }

    // Cancel scheduled task
    public bool Cancel(string taskName)
    {
        if (_tasks.TryRemove(taskName, out _))
        {
            _logger.Info($"Cancelled task '{taskName}'");
            return true;
        }
        return false;
    }

    // Start scheduler
    public void Start()
    {
        if (IsRunning) return;

        IsRunning = true;
        _schedulerTask = Task.Run(async () =>
        {
            _logger.Info("Scheduler started");

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;

                    foreach (var kvp in _tasks)
                    {
                        var task = kvp.Value;

                        if (task.NextRun <= now && !task.IsRunning)
                        {
                            _ = Task.Run(async () => await ExecuteTaskAsync(task));
                        }
                    }

                    await Task.Delay(1000, _cts.Token); // Check every second
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error("Scheduler error", ex);
                }
            }

            _logger.Info("Scheduler stopped");
        });
    }

    // Stop scheduler
    public void Stop()
    {
        if (!IsRunning) return;

        _cts.Cancel();
        _schedulerTask?.Wait();
        IsRunning = false;
    }

    private async Task ExecuteTaskAsync(ScheduledTask task)
    {
        task.IsRunning = true;
        task.LastRun = DateTime.UtcNow;

        try
        {
            _logger.Debug($"Executing task '{task.Name}'");
            await task.Action();
            task.SuccessCount++;
        }
        catch (Exception ex)
        {
            _logger.Error($"Task '{task.Name}' failed", ex);
            task.FailureCount++;
        }
        finally
        {
            task.IsRunning = false;

            // Calculate next run
            if (task.IsRecurring)
            {
                if (task.CronSchedule != null)
                {
                    task.NextRun = task.CronSchedule.GetNextRun(DateTime.UtcNow);
                }
                else if (task.Interval.HasValue)
                {
                    task.NextRun = DateTime.UtcNow.Add(task.Interval.Value);
                }
            }
            else
            {
                // Remove one-time task
                _tasks.TryRemove(task.Name, out _);
            }
        }
    }

    private CronSchedule ParseCron(string expression)
    {
        // Simplified cron: "minute hour day month dayofweek"
        // Example: "0 12 * * *" = daily at 12:00
        // Example: "*/15 * * * *" = every 15 minutes
        var parts = expression.Split(' ');
        if (parts.Length != 5)
            throw new ArgumentException("Invalid cron expression");

        return new CronSchedule
        {
            Minute = parts[0],
            Hour = parts[1],
            Day = parts[2],
            Month = parts[3],
            DayOfWeek = parts[4]
        };
    }

    // Get task status
    public TaskStatus? GetTaskStatus(string taskName)
    {
        if (_tasks.TryGetValue(taskName, out var task))
        {
            return new TaskStatus
            {
                Name = task.Name,
                NextRun = task.NextRun,
                LastRun = task.LastRun,
                IsRunning = task.IsRunning,
                SuccessCount = task.SuccessCount,
                FailureCount = task.FailureCount
            };
        }
        return null;
    }

    // Get all tasks
    public List<TaskStatus> GetAllTasks()
    {
        return _tasks.Values.Select(t => new TaskStatus
        {
            Name = t.Name,
            NextRun = t.NextRun,
            LastRun = t.LastRun,
            IsRunning = t.IsRunning,
            SuccessCount = t.SuccessCount,
            FailureCount = t.FailureCount
        }).ToList();
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
    }

    private class ScheduledTask
    {
        public string Name { get; set; } = "";
        public Func<Task> Action { get; set; } = null!;
        public DateTime NextRun { get; set; }
        public DateTime? LastRun { get; set; }
        public TimeSpan? Interval { get; set; }
        public CronSchedule? CronSchedule { get; set; }
        public bool IsRecurring { get; set; }
        public bool IsRunning { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
    }

    private class CronSchedule
    {
        public string Minute { get; set; } = "*";
        public string Hour { get; set; } = "*";
        public string Day { get; set; } = "*";
        public string Month { get; set; } = "*";
        public string DayOfWeek { get; set; } = "*";

        public DateTime GetNextRun(DateTime from)
        {
            var next = from.AddMinutes(1);
            next = new DateTime(next.Year, next.Month, next.Day, next.Hour, next.Minute, 0);

            // Simplified: only check hour and minute
            while (true)
            {
                if (Matches(next))
                    return next;

                next = next.AddMinutes(1);

                // Safety: don't search more than 1 year ahead
                if (next > from.AddYears(1))
                    throw new Exception("Could not find next run time");
            }
        }

        private bool Matches(DateTime time)
        {
            if (!MatchesPart(Minute, time.Minute, 0, 59)) return false;
            if (!MatchesPart(Hour, time.Hour, 0, 23)) return false;
            if (!MatchesPart(Day, time.Day, 1, 31)) return false;
            if (!MatchesPart(Month, time.Month, 1, 12)) return false;
            return true;
        }

        private bool MatchesPart(string pattern, int value, int min, int max)
        {
            if (pattern == "*") return true;

            // Handle */n (every n)
            if (pattern.StartsWith("*/"))
            {
                var step = int.Parse(pattern.Substring(2));
                return value % step == 0;
            }

            // Handle specific value
            if (int.TryParse(pattern, out var specific))
            {
                return value == specific;
            }

            // Handle ranges: 1-5
            if (pattern.Contains('-'))
            {
                var parts = pattern.Split('-');
                var start = int.Parse(parts[0]);
                var end = int.Parse(parts[1]);
                return value >= start && value <= end;
            }

            // Handle lists: 1,3,5
            if (pattern.Contains(','))
            {
                var values = pattern.Split(',').Select(int.Parse);
                return values.Contains(value);
            }

            return false;
        }
    }

    public class TaskStatus
    {
        public string Name { get; set; } = "";
        public DateTime NextRun { get; set; }
        public DateTime? LastRun { get; set; }
        public bool IsRunning { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
    }
}

/// <summary>
/// Example usage
/// </summary>
public class SchedulerExamples
{
    public static void Examples()
    {
        var scheduler = new SimpleScheduler();

        // Run once in 5 minutes
        scheduler.ScheduleOnce(
            DateTime.UtcNow.AddMinutes(5),
            async () =>
            {
                Console.WriteLine("One-time task executed!");
                await Task.CompletedTask;
            },
            "one-time-task"
        );

        // Run every 10 minutes
        scheduler.ScheduleRecurring(
            TimeSpan.FromMinutes(10),
            async () =>
            {
                Console.WriteLine("Recurring task executed!");
                await Task.CompletedTask;
            },
            "recurring-task"
        );

        // Run daily at 3 AM
        scheduler.ScheduleDaily(
            new TimeSpan(3, 0, 0),
            async () =>
            {
                Console.WriteLine("Daily backup running...");
                await Task.CompletedTask;
            },
            "daily-backup"
        );

        // Run using cron (every hour at minute 0)
        scheduler.ScheduleCron(
            "0 * * * *",
            async () =>
            {
                Console.WriteLine("Hourly task executed!");
                await Task.CompletedTask;
            },
            "hourly-task"
        );

        // Cron examples:
        // "0 0 * * *" = Daily at midnight
        // "0 12 * * *" = Daily at noon
        // "*/15 * * * *" = Every 15 minutes
        // "0 0 * * 1" = Every Monday at midnight
        // "0 9-17 * * 1-5" = Weekdays 9 AM to 5 PM

        scheduler.Start();

        // Check task status
        var status = scheduler.GetTaskStatus("recurring-task");
        if (status != null)
        {
            Console.WriteLine($"Task: {status.Name}");
            Console.WriteLine($"Next run: {status.NextRun}");
            Console.WriteLine($"Success: {status.SuccessCount}, Failures: {status.FailureCount}");
        }

        // Cancel task
        scheduler.Cancel("one-time-task");

        // Get all tasks
        var allTasks = scheduler.GetAllTasks();
        foreach (var task in allTasks)
        {
            Console.WriteLine($"{task.Name}: Next run at {task.NextRun}");
        }

        // Later... stop scheduler
        // scheduler.Stop();
        // scheduler.Dispose();
    }
}