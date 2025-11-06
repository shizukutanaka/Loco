// Rob Pike: "Concurrency is not parallelism, but it enables parallelism"
// John Carmack: "Make background work simple and reliable"

using System.Collections.Concurrent;

namespace Loco.Core.Practical;

/// <summary>
/// Simple job system - Background jobs, retry, scheduling
/// Fire-and-forget, delayed, recurring jobs without heavy frameworks
/// </summary>
public class SimpleJobSystem
{
    private readonly ConcurrentDictionary<string, Job> _jobs = new();
    private readonly SimpleScheduler _scheduler;
    private readonly SimpleLogger _logger;
    private readonly SimpleMetrics _metrics;

    public SimpleJobSystem(SimpleLogger? logger = null, SimpleMetrics? metrics = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleJobSystem));
        _metrics = metrics ?? new SimpleMetrics();
        _scheduler = new SimpleScheduler(_logger);
        _scheduler.Start();
    }

    // Fire and forget
    public string Enqueue(string name, Func<Task> action)
    {
        var job = new Job
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Action = action,
            Status = JobStatus.Queued,
            QueuedAt = DateTime.UtcNow
        };

        _jobs[job.Id] = job;
        _metrics.IncrementCounter("jobs.enqueued");

        _ = Task.Run(async () => await ExecuteJobAsync(job));

        _logger.Info($"Job '{name}' enqueued: {job.Id}");
        return job.Id;
    }

    // Schedule for later
    public string Schedule(string name, Func<Task> action, DateTime runAt)
    {
        var job = new Job
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Action = action,
            Status = JobStatus.Scheduled,
            ScheduledFor = runAt,
            QueuedAt = DateTime.UtcNow
        };

        _jobs[job.Id] = job;

        _scheduler.ScheduleOnce(runAt, async () =>
        {
            await ExecuteJobAsync(job);
        }, $"job-{job.Id}");

        _logger.Info($"Job '{name}' scheduled for {runAt}: {job.Id}");
        return job.Id;
    }

    // Schedule recurring
    public string ScheduleRecurring(string name, Func<Task> action, TimeSpan interval)
    {
        var jobId = Guid.NewGuid().ToString();

        _scheduler.ScheduleRecurring(interval, async () =>
        {
            var job = new Job
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Action = action,
                Status = JobStatus.Queued,
                QueuedAt = DateTime.UtcNow,
                ParentJobId = jobId
            };

            _jobs[job.Id] = job;
            await ExecuteJobAsync(job);
        }, $"recurring-{jobId}");

        _logger.Info($"Recurring job '{name}' scheduled every {interval}: {jobId}");
        return jobId;
    }

    // Schedule with cron
    public string ScheduleCron(string name, Func<Task> action, string cronExpression)
    {
        var jobId = Guid.NewGuid().ToString();

        _scheduler.ScheduleCron(cronExpression, async () =>
        {
            var job = new Job
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Action = action,
                Status = JobStatus.Queued,
                QueuedAt = DateTime.UtcNow,
                ParentJobId = jobId
            };

            _jobs[job.Id] = job;
            await ExecuteJobAsync(job);
        }, $"cron-{jobId}");

        _logger.Info($"Cron job '{name}' scheduled: {cronExpression}");
        return jobId;
    }

    // Execute with retry
    public string EnqueueWithRetry(string name, Func<Task> action, int maxRetries = 3)
    {
        var job = new Job
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Action = action,
            Status = JobStatus.Queued,
            QueuedAt = DateTime.UtcNow,
            MaxRetries = maxRetries
        };

        _jobs[job.Id] = job;

        _ = Task.Run(async () => await ExecuteJobWithRetryAsync(job));

        return job.Id;
    }

    private async Task ExecuteJobAsync(Job job)
    {
        job.Status = JobStatus.Running;
        job.StartedAt = DateTime.UtcNow;

        _logger.Info($"Job '{job.Name}' started: {job.Id}");
        _metrics.IncrementCounter("jobs.started");

        try
        {
            await job.Action();

            job.Status = JobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;

            _logger.Info($"Job '{job.Name}' completed: {job.Id}");
            _metrics.IncrementCounter("jobs.completed");
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.Error = ex.Message;
            job.CompletedAt = DateTime.UtcNow;

            _logger.Error($"Job '{job.Name}' failed: {job.Id}", ex);
            _metrics.IncrementCounter("jobs.failed");
        }
    }

    private async Task ExecuteJobWithRetryAsync(Job job)
    {
        for (int attempt = 0; attempt <= job.MaxRetries; attempt++)
        {
            job.Status = JobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            job.Attempts++;

            try
            {
                await job.Action();

                job.Status = JobStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;

                _logger.Info($"Job '{job.Name}' completed (attempt {attempt + 1}): {job.Id}");
                _metrics.IncrementCounter("jobs.completed");
                return;
            }
            catch (Exception ex)
            {
                _logger.Warning($"Job '{job.Name}' failed attempt {attempt + 1}/{job.MaxRetries + 1}: {ex.Message}");

                if (attempt >= job.MaxRetries)
                {
                    job.Status = JobStatus.Failed;
                    job.Error = ex.Message;
                    job.CompletedAt = DateTime.UtcNow;

                    _logger.Error($"Job '{job.Name}' failed after {job.MaxRetries + 1} attempts: {job.Id}", ex);
                    _metrics.IncrementCounter("jobs.failed");
                    return;
                }

                await Task.Delay(1000 * (int)Math.Pow(2, attempt)); // Exponential backoff
            }
        }
    }

    // Get job status
    public Job? GetJob(string id)
    {
        _jobs.TryGetValue(id, out var job);
        return job;
    }

    // Get all jobs
    public List<Job> GetAllJobs()
    {
        return _jobs.Values.ToList();
    }

    // Get jobs by status
    public List<Job> GetJobsByStatus(JobStatus status)
    {
        return _jobs.Values.Where(j => j.Status == status).ToList();
    }

    // Cancel job
    public bool CancelJob(string id)
    {
        if (_jobs.TryGetValue(id, out var job) && job.Status == JobStatus.Queued)
        {
            job.Status = JobStatus.Cancelled;
            _logger.Info($"Job cancelled: {id}");
            return true;
        }
        return false;
    }

    // Cleanup completed jobs
    public int CleanupCompletedJobs(TimeSpan olderThan)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        var toRemove = _jobs.Values
            .Where(j => j.Status == JobStatus.Completed && j.CompletedAt < cutoff)
            .Select(j => j.Id)
            .ToList();

        foreach (var id in toRemove)
        {
            _jobs.TryRemove(id, out _);
        }

        _logger.Info($"Cleaned up {toRemove.Count} completed jobs");
        return toRemove.Count;
    }

    public void Dispose()
    {
        _scheduler.Dispose();
    }
}

public enum JobStatus
{
    Queued,
    Scheduled,
    Running,
    Completed,
    Failed,
    Cancelled
}

public class Job
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Func<Task> Action { get; set; } = null!;
    public JobStatus Status { get; set; }
    public DateTime QueuedAt { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
    public int MaxRetries { get; set; }
    public int Attempts { get; set; }
    public string? ParentJobId { get; set; }

    public TimeSpan? Duration => StartedAt.HasValue && CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;
}

/// <summary>
/// Job with typed result
/// </summary>
public class Job<T>
{
    private readonly TaskCompletionSource<T> _tcs = new();

    public string Id { get; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public JobStatus Status { get; set; }
    public T? Result { get; set; }
    public string? Error { get; set; }

    public Task<T> Task => _tcs.Task;

    public void SetResult(T result)
    {
        Result = result;
        Status = JobStatus.Completed;
        _tcs.SetResult(result);
    }

    public void SetError(Exception ex)
    {
        Error = ex.Message;
        Status = JobStatus.Failed;
        _tcs.SetException(ex);
    }
}

/// <summary>
/// Job builder for complex jobs
/// </summary>
public class JobBuilder
{
    private string _name = "";
    private Func<Task>? _action;
    private int _maxRetries = 0;
    private DateTime? _scheduledFor;
    private TimeSpan? _recurring;

    public JobBuilder Named(string name)
    {
        _name = name;
        return this;
    }

    public JobBuilder Do(Func<Task> action)
    {
        _action = action;
        return this;
    }

    public JobBuilder WithRetries(int maxRetries)
    {
        _maxRetries = maxRetries;
        return this;
    }

    public JobBuilder ScheduleFor(DateTime runAt)
    {
        _scheduledFor = runAt;
        return this;
    }

    public JobBuilder Every(TimeSpan interval)
    {
        _recurring = interval;
        return this;
    }

    public string Submit(SimpleJobSystem jobSystem)
    {
        if (_action == null)
            throw new InvalidOperationException("Job action not specified");

        if (_recurring.HasValue)
        {
            return jobSystem.ScheduleRecurring(_name, _action, _recurring.Value);
        }
        else if (_scheduledFor.HasValue)
        {
            return jobSystem.Schedule(_name, _action, _scheduledFor.Value);
        }
        else if (_maxRetries > 0)
        {
            return jobSystem.EnqueueWithRetry(_name, _action, _maxRetries);
        }
        else
        {
            return jobSystem.Enqueue(_name, _action);
        }
    }
}

/// <summary>
/// Example jobs
/// </summary>
public class JobExamples
{
    public static async Task Examples()
    {
        var jobSystem = new SimpleJobSystem();

        // Fire and forget
        var jobId1 = jobSystem.Enqueue("SendEmail", async () =>
        {
            await Task.Delay(100);
            Console.WriteLine("Email sent");
        });

        // Schedule for later
        var jobId2 = jobSystem.Schedule("BackupDatabase", async () =>
        {
            await Task.Delay(100);
            Console.WriteLine("Database backed up");
        }, DateTime.UtcNow.AddMinutes(5));

        // Recurring job
        var jobId3 = jobSystem.ScheduleRecurring("CleanupTemp", async () =>
        {
            await Task.Delay(100);
            Console.WriteLine("Temp files cleaned");
        }, TimeSpan.FromHours(1));

        // Cron job (daily at 3 AM)
        var jobId4 = jobSystem.ScheduleCron("DailyReport", async () =>
        {
            await Task.Delay(100);
            Console.WriteLine("Daily report generated");
        }, "0 3 * * *");

        // With retry
        var jobId5 = jobSystem.EnqueueWithRetry("ProcessPayment", async () =>
        {
            await Task.Delay(100);
            // May fail and retry
            Console.WriteLine("Payment processed");
        }, maxRetries: 3);

        // Using builder
        var jobId6 = new JobBuilder()
            .Named("ComplexJob")
            .Do(async () =>
            {
                await Task.Delay(100);
                Console.WriteLine("Complex job done");
            })
            .WithRetries(3)
            .Submit(jobSystem);

        // Check status
        await Task.Delay(1000);
        var job = jobSystem.GetJob(jobId1);
        if (job != null)
        {
            Console.WriteLine($"Job status: {job.Status}");
            if (job.Duration.HasValue)
            {
                Console.WriteLine($"Duration: {job.Duration.Value.TotalMilliseconds}ms");
            }
        }

        // Get all running jobs
        var runningJobs = jobSystem.GetJobsByStatus(JobStatus.Running);
        Console.WriteLine($"Running jobs: {runningJobs.Count}");

        // Cleanup old jobs
        jobSystem.CleanupCompletedJobs(TimeSpan.FromHours(24));

        jobSystem.Dispose();
    }
}