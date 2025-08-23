using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Loco.Core.BackgroundJobs
{
    public interface IBackgroundJobService
    {
        string EnqueueJob<T>(Func<T, Task> job, T parameters, JobOptions options = null) where T : class;
        string EnqueueJob(Func<Task> job, JobOptions options = null);
        string ScheduleJob<T>(Func<T, Task> job, T parameters, DateTime scheduledTime, JobOptions options = null) where T : class;
        string ScheduleRecurringJob(string jobId, Func<Task> job, string cronExpression, JobOptions options = null);
        Task<bool> CancelJobAsync(string jobId);
        Task<JobStatus> GetJobStatusAsync(string jobId);
        Task<List<JobInfo>> GetAllJobsAsync();
        Task<List<JobInfo>> GetJobsByStatusAsync(JobState state);
        Task<JobMetrics> GetMetricsAsync();
        void Start();
        void Stop();
    }

    public class BackgroundJobService : IBackgroundJobService, IHostedService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<BackgroundJobService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, JobWrapper> _jobs;
        private readonly ConcurrentQueue<JobWrapper> _jobQueue;
        private readonly ConcurrentDictionary<string, RecurringJobInfo> _recurringJobs;
        private readonly SemaphoreSlim _semaphore;
        private readonly Timer _schedulerTimer;
        private readonly Timer _cleanupTimer;
        private readonly JobMetrics _metrics;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _processingTask;
        private bool _isRunning;
        private readonly int _maxConcurrentJobs;
        private readonly int _maxRetries;
        private readonly TimeSpan _jobTimeout;

        public BackgroundJobService(
            IConfiguration configuration,
            ILogger<BackgroundJobService> logger,
            IServiceProvider serviceProvider)
        {
            _configuration = configuration;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _jobs = new ConcurrentDictionary<string, JobWrapper>();
            _jobQueue = new ConcurrentQueue<JobWrapper>();
            _recurringJobs = new ConcurrentDictionary<string, RecurringJobInfo>();
            _metrics = new JobMetrics();
            
            _maxConcurrentJobs = _configuration.GetValue<int>("BackgroundJobs:MaxConcurrentJobs", 5);
            _maxRetries = _configuration.GetValue<int>("BackgroundJobs:MaxRetries", 3);
            _jobTimeout = TimeSpan.FromSeconds(_configuration.GetValue<int>("BackgroundJobs:JobTimeoutSeconds", 300));
            
            _semaphore = new SemaphoreSlim(_maxConcurrentJobs, _maxConcurrentJobs);
            _schedulerTimer = new Timer(ProcessScheduledJobs, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            _cleanupTimer = new Timer(CleanupCompletedJobs, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stop();
            return Task.CompletedTask;
        }

        public void Start()
        {
            if (_isRunning) return;

            _cancellationTokenSource = new CancellationTokenSource();
            _processingTask = ProcessJobsAsync(_cancellationTokenSource.Token);
            _isRunning = true;
            
            _logger.LogInformation("Background job service started with {MaxConcurrentJobs} workers", _maxConcurrentJobs);
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _cancellationTokenSource?.Cancel();
            _processingTask?.Wait(TimeSpan.FromSeconds(10));
            _isRunning = false;
            
            _logger.LogInformation("Background job service stopped");
        }

        public string EnqueueJob<T>(Func<T, Task> job, T parameters, JobOptions options = null) where T : class
        {
            var jobId = Guid.NewGuid().ToString();
            var wrapper = new JobWrapper
            {
                Id = jobId,
                Job = async () => await job(parameters),
                Options = options ?? new JobOptions(),
                State = JobState.Queued,
                CreatedAt = DateTime.UtcNow,
                Parameters = parameters
            };

            _jobs[jobId] = wrapper;
            _jobQueue.Enqueue(wrapper);
            _metrics.IncrementEnqueued();
            
            _logger.LogInformation("Job {JobId} enqueued with priority {Priority}", jobId, wrapper.Options.Priority);
            return jobId;
        }

        public string EnqueueJob(Func<Task> job, JobOptions options = null)
        {
            var jobId = Guid.NewGuid().ToString();
            var wrapper = new JobWrapper
            {
                Id = jobId,
                Job = job,
                Options = options ?? new JobOptions(),
                State = JobState.Queued,
                CreatedAt = DateTime.UtcNow
            };

            _jobs[jobId] = wrapper;
            _jobQueue.Enqueue(wrapper);
            _metrics.IncrementEnqueued();
            
            _logger.LogInformation("Job {JobId} enqueued", jobId);
            return jobId;
        }

        public string ScheduleJob<T>(Func<T, Task> job, T parameters, DateTime scheduledTime, JobOptions options = null) where T : class
        {
            var jobId = Guid.NewGuid().ToString();
            var wrapper = new JobWrapper
            {
                Id = jobId,
                Job = async () => await job(parameters),
                Options = options ?? new JobOptions(),
                State = JobState.Scheduled,
                CreatedAt = DateTime.UtcNow,
                ScheduledAt = scheduledTime,
                Parameters = parameters
            };

            _jobs[jobId] = wrapper;
            _metrics.IncrementScheduled();
            
            _logger.LogInformation("Job {JobId} scheduled for {ScheduledTime}", jobId, scheduledTime);
            return jobId;
        }

        public string ScheduleRecurringJob(string jobId, Func<Task> job, string cronExpression, JobOptions options = null)
        {
            var recurringJob = new RecurringJobInfo
            {
                Id = jobId,
                Job = job,
                CronExpression = cronExpression,
                Options = options ?? new JobOptions(),
                NextExecution = CalculateNextExecution(cronExpression),
                IsActive = true
            };

            _recurringJobs[jobId] = recurringJob;
            _metrics.IncrementRecurring();
            
            _logger.LogInformation("Recurring job {JobId} scheduled with expression {CronExpression}", jobId, cronExpression);
            return jobId;
        }

        public async Task<bool> CancelJobAsync(string jobId)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                if (job.State == JobState.Queued || job.State == JobState.Scheduled)
                {
                    job.State = JobState.Cancelled;
                    job.CancellationTokenSource?.Cancel();
                    _metrics.IncrementCancelled();
                    
                    _logger.LogInformation("Job {JobId} cancelled", jobId);
                    return true;
                }
            }

            if (_recurringJobs.TryGetValue(jobId, out var recurringJob))
            {
                recurringJob.IsActive = false;
                _logger.LogInformation("Recurring job {JobId} cancelled", jobId);
                return true;
            }

            return false;
        }

        public async Task<JobStatus> GetJobStatusAsync(string jobId)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                return new JobStatus
                {
                    JobId = jobId,
                    State = job.State,
                    CreatedAt = job.CreatedAt,
                    StartedAt = job.StartedAt,
                    CompletedAt = job.CompletedAt,
                    Progress = job.Progress,
                    Message = job.LastMessage,
                    Error = job.Error,
                    RetryCount = job.RetryCount
                };
            }

            return null;
        }

        public async Task<List<JobInfo>> GetAllJobsAsync()
        {
            return _jobs.Values.Select(j => new JobInfo
            {
                Id = j.Id,
                State = j.State,
                Priority = j.Options.Priority,
                CreatedAt = j.CreatedAt,
                StartedAt = j.StartedAt,
                CompletedAt = j.CompletedAt,
                RetryCount = j.RetryCount
            }).ToList();
        }

        public async Task<List<JobInfo>> GetJobsByStatusAsync(JobState state)
        {
            return _jobs.Values
                .Where(j => j.State == state)
                .Select(j => new JobInfo
                {
                    Id = j.Id,
                    State = j.State,
                    Priority = j.Options.Priority,
                    CreatedAt = j.CreatedAt,
                    StartedAt = j.StartedAt,
                    CompletedAt = j.CompletedAt,
                    RetryCount = j.RetryCount
                })
                .ToList();
        }

        public async Task<JobMetrics> GetMetricsAsync()
        {
            return _metrics.Clone();
        }

        private async Task ProcessJobsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_jobQueue.TryDequeue(out var job))
                    {
                        if (job.State == JobState.Cancelled)
                            continue;

                        await _semaphore.WaitAsync(cancellationToken);
                        
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await ExecuteJobAsync(job, cancellationToken);
                            }
                            finally
                            {
                                _semaphore.Release();
                            }
                        }, cancellationToken);
                    }
                    else
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in job processing loop");
                }
            }
        }

        private async Task ExecuteJobAsync(JobWrapper job, CancellationToken cancellationToken)
        {
            job.State = JobState.Running;
            job.StartedAt = DateTime.UtcNow;
            _metrics.IncrementRunning();
            
            _logger.LogInformation("Executing job {JobId}", job.Id);

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    job.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    
                    var timeoutCts = new CancellationTokenSource(_jobTimeout);
                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        job.CancellationTokenSource.Token, timeoutCts.Token))
                    {
                        await job.Job();
                    }
                }

                job.State = JobState.Completed;
                job.CompletedAt = DateTime.UtcNow;
                _metrics.IncrementCompleted();
                _metrics.RecordExecutionTime((job.CompletedAt.Value - job.StartedAt.Value).TotalMilliseconds);
                
                _logger.LogInformation("Job {JobId} completed successfully", job.Id);
            }
            catch (OperationCanceledException)
            {
                job.State = JobState.Cancelled;
                _metrics.IncrementCancelled();
                _logger.LogWarning("Job {JobId} was cancelled", job.Id);
            }
            catch (Exception ex)
            {
                job.Error = ex.Message;
                job.RetryCount++;
                
                if (job.RetryCount < _maxRetries && job.Options.EnableRetry)
                {
                    job.State = JobState.Queued;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, job.RetryCount));
                    
                    _logger.LogWarning(ex, "Job {JobId} failed, retrying in {Delay}s (attempt {RetryCount}/{MaxRetries})",
                        job.Id, delay.TotalSeconds, job.RetryCount, _maxRetries);
                    
                    await Task.Delay(delay, cancellationToken);
                    _jobQueue.Enqueue(job);
                }
                else
                {
                    job.State = JobState.Failed;
                    job.CompletedAt = DateTime.UtcNow;
                    _metrics.IncrementFailed();
                    
                    _logger.LogError(ex, "Job {JobId} failed after {RetryCount} attempts", job.Id, job.RetryCount);
                }
            }
        }

        private void ProcessScheduledJobs(object state)
        {
            try
            {
                var now = DateTime.UtcNow;
                
                var scheduledJobs = _jobs.Values
                    .Where(j => j.State == JobState.Scheduled && j.ScheduledAt <= now)
                    .OrderBy(j => j.Options.Priority)
                    .ThenBy(j => j.ScheduledAt);

                foreach (var job in scheduledJobs)
                {
                    job.State = JobState.Queued;
                    _jobQueue.Enqueue(job);
                    _logger.LogInformation("Scheduled job {JobId} moved to queue", job.Id);
                }

                foreach (var recurringJob in _recurringJobs.Values.Where(j => j.IsActive && j.NextExecution <= now))
                {
                    var jobId = $"{recurringJob.Id}_{DateTime.UtcNow.Ticks}";
                    var wrapper = new JobWrapper
                    {
                        Id = jobId,
                        Job = recurringJob.Job,
                        Options = recurringJob.Options,
                        State = JobState.Queued,
                        CreatedAt = DateTime.UtcNow
                    };

                    _jobs[jobId] = wrapper;
                    _jobQueue.Enqueue(wrapper);
                    
                    recurringJob.NextExecution = CalculateNextExecution(recurringJob.CronExpression);
                    recurringJob.LastExecution = now;
                    
                    _logger.LogInformation("Recurring job {JobId} instance {InstanceId} enqueued", 
                        recurringJob.Id, jobId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scheduled jobs");
            }
        }

        private void CleanupCompletedJobs(object state)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddHours(-1);
                var jobsToRemove = _jobs.Values
                    .Where(j => (j.State == JobState.Completed || j.State == JobState.Failed) && 
                           j.CompletedAt < cutoff)
                    .Select(j => j.Id)
                    .ToList();

                foreach (var jobId in jobsToRemove)
                {
                    _jobs.TryRemove(jobId, out _);
                }

                if (jobsToRemove.Any())
                {
                    _logger.LogInformation("Cleaned up {Count} completed jobs", jobsToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up completed jobs");
            }
        }

        private DateTime CalculateNextExecution(string cronExpression)
        {
            return DateTime.UtcNow.AddMinutes(1);
        }

        public void Dispose()
        {
            Stop();
            _schedulerTimer?.Dispose();
            _cleanupTimer?.Dispose();
            _semaphore?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }

    public class JobWrapper
    {
        public string Id { get; set; }
        public Func<Task> Job { get; set; }
        public JobOptions Options { get; set; }
        public JobState State { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public int RetryCount { get; set; }
        public double Progress { get; set; }
        public string LastMessage { get; set; }
        public string Error { get; set; }
        public object Parameters { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; }
    }

    public class RecurringJobInfo
    {
        public string Id { get; set; }
        public Func<Task> Job { get; set; }
        public string CronExpression { get; set; }
        public JobOptions Options { get; set; }
        public DateTime NextExecution { get; set; }
        public DateTime? LastExecution { get; set; }
        public bool IsActive { get; set; }
    }

    public class JobOptions
    {
        public int Priority { get; set; } = 0;
        public bool EnableRetry { get; set; } = true;
        public string Queue { get; set; } = "default";
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    public class JobStatus
    {
        public string JobId { get; set; }
        public JobState State { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public double Progress { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public int RetryCount { get; set; }
    }

    public class JobInfo
    {
        public string Id { get; set; }
        public JobState State { get; set; }
        public int Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int RetryCount { get; set; }
    }

    public class JobMetrics
    {
        private long _enqueuedCount;
        private long _scheduledCount;
        private long _recurringCount;
        private long _runningCount;
        private long _completedCount;
        private long _failedCount;
        private long _cancelledCount;
        private double _totalExecutionTime;
        private readonly object _lock = new object();

        public long EnqueuedCount => _enqueuedCount;
        public long ScheduledCount => _scheduledCount;
        public long RecurringCount => _recurringCount;
        public long RunningCount => _runningCount;
        public long CompletedCount => _completedCount;
        public long FailedCount => _failedCount;
        public long CancelledCount => _cancelledCount;
        public double AverageExecutionTime => _completedCount > 0 ? _totalExecutionTime / _completedCount : 0;

        public void IncrementEnqueued() => Interlocked.Increment(ref _enqueuedCount);
        public void IncrementScheduled() => Interlocked.Increment(ref _scheduledCount);
        public void IncrementRecurring() => Interlocked.Increment(ref _recurringCount);
        public void IncrementRunning() => Interlocked.Increment(ref _runningCount);
        public void IncrementCompleted() => Interlocked.Increment(ref _completedCount);
        public void IncrementFailed() => Interlocked.Increment(ref _failedCount);
        public void IncrementCancelled() => Interlocked.Increment(ref _cancelledCount);

        public void RecordExecutionTime(double milliseconds)
        {
            lock (_lock)
            {
                _totalExecutionTime += milliseconds;
            }
        }

        public JobMetrics Clone()
        {
            lock (_lock)
            {
                return new JobMetrics
                {
                    _enqueuedCount = _enqueuedCount,
                    _scheduledCount = _scheduledCount,
                    _recurringCount = _recurringCount,
                    _runningCount = _runningCount,
                    _completedCount = _completedCount,
                    _failedCount = _failedCount,
                    _cancelledCount = _cancelledCount,
                    _totalExecutionTime = _totalExecutionTime
                };
            }
        }
    }

    public enum JobState
    {
        Queued,
        Scheduled,
        Running,
        Completed,
        Failed,
        Cancelled
    }
}