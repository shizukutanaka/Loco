using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

namespace Loco.Core.Processing
{
    public interface IDistributedProcessingService
    {
        Task<string> SubmitJobAsync<T>(IProcessingJob<T> job, ProcessingOptions options = null);
        Task<JobResult<T>> GetJobResultAsync<T>(string jobId);
        Task<JobStatus> GetJobStatusAsync(string jobId);
        Task<bool> CancelJobAsync(string jobId);
        Task<ProcessingStats> GetStatsAsync();
        Task<List<JobInfo>> GetActiveJobsAsync();
        Task<List<JobInfo>> GetCompletedJobsAsync(int count = 100);
        void RegisterWorker<T>(IJobWorker<T> worker);
        Task StartAsync();
        Task StopAsync();
        Task ScaleWorkersAsync(int desiredWorkerCount);
    }

    public interface IProcessingJob<T>
    {
        string Id { get; }
        string Type { get; }
        T Data { get; }
        ProcessingPriority Priority { get; }
        TimeSpan Timeout { get; }
        Dictionary<string, object> Metadata { get; }
        Task<object> ExecuteAsync(CancellationToken cancellationToken);
    }

    public interface IJobWorker<T>
    {
        string WorkerType { get; }
        Task<object> ProcessAsync(T data, CancellationToken cancellationToken);
        bool CanHandle(string jobType);
    }

    public enum ProcessingPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    public enum JobStatus
    {
        Queued,
        Running,
        Completed,
        Failed,
        Cancelled,
        Timeout
    }

    public class ProcessingOptions
    {
        public ProcessingPriority Priority { get; set; } = ProcessingPriority.Normal;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);
        public int MaxRetries { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);
        public bool PersistResult { get; set; } = true;
        public Dictionary<string, object> Metadata { get; set; } = new();
        public string[] RequiredWorkerTags { get; set; } = Array.Empty<string>();
        public bool AllowParallelExecution { get; set; } = true;
    }

    public class JobInfo
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public JobStatus Status { get; set; }
        public ProcessingPriority Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue ? CompletedAt - StartedAt : null;
        public string WorkerId { get; set; }
        public string ErrorMessage { get; set; }
        public int RetryCount { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public long DataSize { get; set; }
        public double Progress { get; set; }
    }

    public class JobResult<T>
    {
        public string JobId { get; set; }
        public JobStatus Status { get; set; }
        public T Result { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class ProcessingStats
    {
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public int TotalJobs { get; set; }
        public int QueuedJobs { get; set; }
        public int RunningJobs { get; set; }
        public int CompletedJobs { get; set; }
        public int FailedJobs { get; set; }
        public int CancelledJobs { get; set; }
        public int ActiveWorkers { get; set; }
        public double AverageProcessingTime { get; set; }
        public Dictionary<string, int> JobsByType { get; set; } = new();
        public Dictionary<string, int> JobsByPriority { get; set; } = new();
        public Dictionary<string, WorkerStats> WorkerStats { get; set; } = new();
        public long TotalDataProcessed { get; set; }
        public double ThroughputPerSecond { get; set; }
        public TimeSpan Uptime { get; set; }
    }

    public class WorkerStats
    {
        public string WorkerId { get; set; }
        public string WorkerType { get; set; }
        public int JobsProcessed { get; set; }
        public int JobsSuccessful { get; set; }
        public int JobsFailed { get; set; }
        public double AverageProcessingTime { get; set; }
        public DateTime LastJobStarted { get; set; }
        public JobStatus CurrentJobStatus { get; set; }
        public string CurrentJobId { get; set; }
        public bool IsHealthy { get; set; } = true;
        public DateTime LastHeartbeat { get; set; }
    }

    public class ProcessingServiceOptions
    {
        public int MaxConcurrentJobs { get; set; } = Environment.ProcessorCount * 2;
        public int MaxQueueSize { get; set; } = 10000;
        public TimeSpan JobTimeout { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan WorkerHealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan JobCleanupInterval { get; set; } = TimeSpan.FromHours(1);
        public int CompletedJobRetentionHours { get; set; } = 24;
        public bool EnablePersistence { get; set; } = true;
        public string PersistencePath { get; set; } = "jobs";
        public bool EnableMetrics { get; set; } = true;
        public bool EnableLoadBalancing { get; set; } = true;
        public double WorkerLoadThreshold { get; set; } = 0.8;
        public int ScaleUpThreshold { get; set; } = 80; // 80% queue full
        public int ScaleDownThreshold { get; set; } = 20; // 20% queue full
    }

    public class DistributedProcessingService : IDistributedProcessingService, IHostedService, IDisposable
    {
        private readonly ILogger<DistributedProcessingService> _logger;
        private readonly ProcessingServiceOptions _options;
        
        // Job management
        private readonly ConcurrentDictionary<string, JobInfo> _jobs;
        private readonly ConcurrentQueue<JobInfo> _jobQueue;
        private readonly ConcurrentDictionary<string, object> _jobResults;
        private readonly ConcurrentDictionary<string, IProcessingJob<object>> _jobData;
        
        // Worker management  
        private readonly ConcurrentDictionary<string, IJobWorker<object>> _workers;
        private readonly ConcurrentDictionary<string, WorkerStats> _workerStats;
        private readonly SemaphoreSlim _concurrencyLimiter;
        
        // Background services
        private readonly Timer _healthCheckTimer;
        private readonly Timer _cleanupTimer;
        private readonly Timer _metricsTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        // Statistics
        private readonly ProcessingStats _stats;
        private readonly object _statsLock = new object();
        private readonly DateTime _startTime;
        
        private volatile bool _isRunning;
        private volatile bool _disposed;

        public DistributedProcessingService(
            ILogger<DistributedProcessingService> logger,
            IOptions<ProcessingServiceOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new ProcessingServiceOptions();
            
            _jobs = new ConcurrentDictionary<string, JobInfo>();
            _jobQueue = new ConcurrentQueue<JobInfo>();
            _jobResults = new ConcurrentDictionary<string, object>();
            _jobData = new ConcurrentDictionary<string, IProcessingJob<object>>();
            
            _workers = new ConcurrentDictionary<string, IJobWorker<object>>();
            _workerStats = new ConcurrentDictionary<string, WorkerStats>();
            _concurrencyLimiter = new SemaphoreSlim(_options.MaxConcurrentJobs, _options.MaxConcurrentJobs);
            
            _cancellationTokenSource = new CancellationTokenSource();
            _stats = new ProcessingStats();
            _startTime = DateTime.UtcNow;
            
            // Initialize timers
            _healthCheckTimer = new Timer(HealthCheckCallback, null, Timeout.Infinite, Timeout.Infinite);
            _cleanupTimer = new Timer(CleanupCallback, null, Timeout.Infinite, Timeout.Infinite);
            _metricsTimer = new Timer(MetricsCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await StartAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await StopAsync();
        }

        public async Task StartAsync()
        {
            _isRunning = true;
            
            // Start background timers
            _healthCheckTimer.Change(_options.WorkerHealthCheckInterval, _options.WorkerHealthCheckInterval);
            _cleanupTimer.Change(_options.JobCleanupInterval, _options.JobCleanupInterval);
            _metricsTimer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            
            // Start job processing loops
            for (int i = 0; i < _options.MaxConcurrentJobs; i++)
            {
                _ = Task.Run(ProcessJobsAsync, _cancellationTokenSource.Token);
            }
            
            _logger.LogInformation("Distributed processing service started with {MaxJobs} concurrent job slots", 
                _options.MaxConcurrentJobs);
            
            await Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            _isRunning = false;
            _cancellationTokenSource.Cancel();
            
            // Stop timers
            _healthCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _metricsTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            // Wait for running jobs to complete (with timeout)
            var timeout = TimeSpan.FromSeconds(30);
            var stopwatch = Stopwatch.StartNew();
            
            while (_stats.RunningJobs > 0 && stopwatch.Elapsed < timeout)
            {
                await Task.Delay(1000);
            }
            
            _logger.LogInformation("Distributed processing service stopped");
        }

        public async Task<string> SubmitJobAsync<T>(IProcessingJob<T> job, ProcessingOptions options = null)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            if (_jobQueue.Count >= _options.MaxQueueSize)
            {
                throw new InvalidOperationException($"Job queue is full (max: {_options.MaxQueueSize})");
            }

            var jobInfo = new JobInfo
            {
                Id = job.Id,
                Type = job.Type,
                Status = JobStatus.Queued,
                Priority = options?.Priority ?? job.Priority,
                CreatedAt = DateTime.UtcNow,
                Metadata = job.Metadata ?? new Dictionary<string, object>()
            };

            _jobs[job.Id] = jobInfo;
            _jobData[job.Id] = job as IProcessingJob<object>;
            _jobQueue.Enqueue(jobInfo);

            UpdateStats(stats =>
            {
                stats.TotalJobs++;
                stats.QueuedJobs++;
                stats.JobsByType[job.Type] = stats.JobsByType.GetValueOrDefault(job.Type, 0) + 1;
                stats.JobsByPriority[jobInfo.Priority.ToString()] = stats.JobsByPriority.GetValueOrDefault(jobInfo.Priority.ToString(), 0) + 1;
            });

            _logger.LogDebug("Job {JobId} of type {JobType} submitted with priority {Priority}", 
                job.Id, job.Type, jobInfo.Priority);

            return job.Id;
        }

        public async Task<JobResult<T>> GetJobResultAsync<T>(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
                return null;

            if (!_jobs.TryGetValue(jobId, out var jobInfo))
                return null;

            var result = new JobResult<T>
            {
                JobId = jobId,
                Status = jobInfo.Status,
                CompletedAt = jobInfo.CompletedAt ?? DateTime.MinValue,
                Duration = jobInfo.Duration ?? TimeSpan.Zero,
                ErrorMessage = jobInfo.ErrorMessage,
                Metadata = jobInfo.Metadata
            };

            if (jobInfo.Status == JobStatus.Completed && _jobResults.TryGetValue(jobId, out var resultData))
            {
                result.Result = (T)resultData;
            }

            return result;
        }

        public async Task<JobStatus> GetJobStatusAsync(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
                return JobStatus.Failed;

            return _jobs.TryGetValue(jobId, out var jobInfo) ? jobInfo.Status : JobStatus.Failed;
        }

        public async Task<bool> CancelJobAsync(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId) || !_jobs.TryGetValue(jobId, out var jobInfo))
                return false;

            if (jobInfo.Status == JobStatus.Completed || jobInfo.Status == JobStatus.Failed)
                return false;

            jobInfo.Status = JobStatus.Cancelled;
            jobInfo.CompletedAt = DateTime.UtcNow;

            UpdateStats(stats =>
            {
                if (jobInfo.Status == JobStatus.Queued)
                    stats.QueuedJobs--;
                else if (jobInfo.Status == JobStatus.Running)
                    stats.RunningJobs--;
                
                stats.CancelledJobs++;
            });

            _logger.LogInformation("Job {JobId} cancelled", jobId);
            return true;
        }

        public async Task<ProcessingStats> GetStatsAsync()
        {
            return await Task.Run(() =>
            {
                lock (_statsLock)
                {
                    var stats = new ProcessingStats
                    {
                        TotalJobs = _stats.TotalJobs,
                        QueuedJobs = _stats.QueuedJobs,
                        RunningJobs = _stats.RunningJobs,
                        CompletedJobs = _stats.CompletedJobs,
                        FailedJobs = _stats.FailedJobs,
                        CancelledJobs = _stats.CancelledJobs,
                        ActiveWorkers = _workers.Count,
                        JobsByType = new Dictionary<string, int>(_stats.JobsByType),
                        JobsByPriority = new Dictionary<string, int>(_stats.JobsByPriority),
                        WorkerStats = new Dictionary<string, WorkerStats>(_workerStats),
                        Uptime = DateTime.UtcNow - _startTime
                    };

                    // Calculate averages
                    var completedJobs = _jobs.Values.Where(j => j.Status == JobStatus.Completed).ToList();
                    if (completedJobs.Any())
                    {
                        stats.AverageProcessingTime = completedJobs.Average(j => j.Duration?.TotalSeconds ?? 0);
                        stats.ThroughputPerSecond = completedJobs.Count / Math.Max(1, stats.Uptime.TotalSeconds);
                    }

                    return stats;
                }
            });
        }

        public async Task<List<JobInfo>> GetActiveJobsAsync()
        {
            return await Task.Run(() =>
            {
                return _jobs.Values
                    .Where(j => j.Status == JobStatus.Running || j.Status == JobStatus.Queued)
                    .OrderByDescending(j => j.Priority)
                    .ThenBy(j => j.CreatedAt)
                    .ToList();
            });
        }

        public async Task<List<JobInfo>> GetCompletedJobsAsync(int count = 100)
        {
            return await Task.Run(() =>
            {
                return _jobs.Values
                    .Where(j => j.Status == JobStatus.Completed || j.Status == JobStatus.Failed || j.Status == JobStatus.Cancelled)
                    .OrderByDescending(j => j.CompletedAt)
                    .Take(count)
                    .ToList();
            });
        }

        public void RegisterWorker<T>(IJobWorker<T> worker)
        {
            if (worker == null)
                throw new ArgumentNullException(nameof(worker));

            var workerId = $"{worker.WorkerType}_{Guid.NewGuid():N}";
            _workers[workerId] = worker as IJobWorker<object>;
            
            _workerStats[workerId] = new WorkerStats
            {
                WorkerId = workerId,
                WorkerType = worker.WorkerType,
                LastHeartbeat = DateTime.UtcNow,
                IsHealthy = true
            };

            _logger.LogInformation("Registered worker {WorkerId} of type {WorkerType}", workerId, worker.WorkerType);
        }

        public async Task ScaleWorkersAsync(int desiredWorkerCount)
        {
            // In a real implementation, this would scale workers up/down
            // For now, just log the scaling request
            _logger.LogInformation("Scaling request: {Current} -> {Desired} workers", 
                _workers.Count, desiredWorkerCount);
            
            await Task.CompletedTask;
        }

        private async Task ProcessJobsAsync()
        {
            while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await _concurrencyLimiter.WaitAsync(_cancellationTokenSource.Token);
                    
                    try
                    {
                        var job = await GetNextJobAsync();
                        if (job != null)
                        {
                            await ProcessJobAsync(job);
                        }
                        else
                        {
                            // No jobs available, wait a bit
                            await Task.Delay(1000, _cancellationTokenSource.Token);
                        }
                    }
                    finally
                    {
                        _concurrencyLimiter.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in job processing loop");
                    await Task.Delay(5000, _cancellationTokenSource.Token); // Back off on error
                }
            }
        }

        private async Task<JobInfo> GetNextJobAsync()
        {
            // Dequeue jobs with priority consideration
            var candidates = new List<JobInfo>();
            var tempQueue = new List<JobInfo>();
            
            // Collect up to 10 jobs to consider
            for (int i = 0; i < 10 && _jobQueue.TryDequeue(out var job); i++)
            {
                if (job.Status == JobStatus.Queued)
                {
                    candidates.Add(job);
                }
                else
                {
                    tempQueue.Add(job); // Re-queue non-queued jobs
                }
            }

            // Re-queue jobs we're not processing
            foreach (var job in tempQueue)
            {
                _jobQueue.Enqueue(job);
            }

            if (!candidates.Any())
                return null;

            // Select highest priority job, then oldest if same priority
            return candidates
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.CreatedAt)
                .First();
        }

        private async Task ProcessJobAsync(JobInfo jobInfo)
        {
            if (!_jobData.TryGetValue(jobInfo.Id, out var jobData))
            {
                _logger.LogWarning("Job data not found for job {JobId}", jobInfo.Id);
                return;
            }

            // Find appropriate worker
            var worker = FindWorkerForJob(jobInfo.Type);
            if (worker == null)
            {
                _logger.LogWarning("No worker available for job type {JobType}", jobInfo.Type);
                jobInfo.Status = JobStatus.Failed;
                jobInfo.ErrorMessage = "No worker available";
                return;
            }

            var workerId = _workers.FirstOrDefault(w => w.Value == worker).Key;
            var workerStats = _workerStats[workerId];

            jobInfo.Status = JobStatus.Running;
            jobInfo.StartedAt = DateTime.UtcNow;
            jobInfo.WorkerId = workerId;

            workerStats.LastJobStarted = DateTime.UtcNow;
            workerStats.CurrentJobId = jobInfo.Id;
            workerStats.CurrentJobStatus = JobStatus.Running;

            UpdateStats(stats =>
            {
                stats.QueuedJobs--;
                stats.RunningJobs++;
            });

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogDebug("Starting job {JobId} on worker {WorkerId}", jobInfo.Id, workerId);

                var result = await jobData.ExecuteAsync(_cancellationTokenSource.Token);
                
                stopwatch.Stop();
                
                jobInfo.Status = JobStatus.Completed;
                jobInfo.CompletedAt = DateTime.UtcNow;
                
                if (_options.EnablePersistence)
                {
                    _jobResults[jobInfo.Id] = result;
                }

                workerStats.JobsProcessed++;
                workerStats.JobsSuccessful++;
                workerStats.AverageProcessingTime = (workerStats.AverageProcessingTime * (workerStats.JobsProcessed - 1) + stopwatch.Elapsed.TotalSeconds) / workerStats.JobsProcessed;

                UpdateStats(stats =>
                {
                    stats.RunningJobs--;
                    stats.CompletedJobs++;
                });

                _logger.LogInformation("Job {JobId} completed successfully in {Duration}ms", 
                    jobInfo.Id, stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                
                jobInfo.Status = JobStatus.Cancelled;
                jobInfo.CompletedAt = DateTime.UtcNow;
                jobInfo.ErrorMessage = "Job was cancelled";

                workerStats.JobsProcessed++;

                UpdateStats(stats =>
                {
                    stats.RunningJobs--;
                    stats.CancelledJobs++;
                });

                _logger.LogInformation("Job {JobId} was cancelled", jobInfo.Id);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                jobInfo.Status = JobStatus.Failed;
                jobInfo.CompletedAt = DateTime.UtcNow;
                jobInfo.ErrorMessage = ex.Message;

                workerStats.JobsProcessed++;
                workerStats.JobsFailed++;

                UpdateStats(stats =>
                {
                    stats.RunningJobs--;
                    stats.FailedJobs++;
                });

                _logger.LogError(ex, "Job {JobId} failed after {Duration}ms", 
                    jobInfo.Id, stopwatch.ElapsedMilliseconds);
            }
            finally
            {
                workerStats.CurrentJobId = null;
                workerStats.CurrentJobStatus = JobStatus.Completed;
                workerStats.LastHeartbeat = DateTime.UtcNow;
            }
        }

        private IJobWorker<object> FindWorkerForJob(string jobType)
        {
            return _workers.Values.FirstOrDefault(w => w.CanHandle(jobType));
        }

        private void HealthCheckCallback(object state)
        {
            try
            {
                var unhealthyWorkers = _workerStats.Values
                    .Where(w => DateTime.UtcNow - w.LastHeartbeat > TimeSpan.FromMinutes(5))
                    .ToList();

                foreach (var worker in unhealthyWorkers)
                {
                    worker.IsHealthy = false;
                    _logger.LogWarning("Worker {WorkerId} appears unhealthy - last heartbeat: {LastHeartbeat}",
                        worker.WorkerId, worker.LastHeartbeat);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during worker health check");
            }
        }

        private void CleanupCallback(object state)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-_options.CompletedJobRetentionHours);
                var jobsToCleanup = _jobs.Values
                    .Where(j => (j.Status == JobStatus.Completed || j.Status == JobStatus.Failed || j.Status == JobStatus.Cancelled) &&
                               j.CompletedAt.HasValue && j.CompletedAt.Value < cutoffTime)
                    .ToList();

                foreach (var job in jobsToCleanup)
                {
                    _jobs.TryRemove(job.Id, out _);
                    _jobData.TryRemove(job.Id, out _);
                    _jobResults.TryRemove(job.Id, out _);
                }

                if (jobsToCleanup.Any())
                {
                    _logger.LogDebug("Cleaned up {Count} old completed jobs", jobsToCleanup.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during job cleanup");
            }
        }

        private void MetricsCallback(object state)
        {
            try
            {
                var stats = GetStatsAsync().Result;
                
                _logger.LogInformation("Processing Stats - Total: {Total}, Queued: {Queued}, Running: {Running}, Completed: {Completed}, Failed: {Failed}",
                    stats.TotalJobs, stats.QueuedJobs, stats.RunningJobs, stats.CompletedJobs, stats.FailedJobs);
                
                // Check for auto-scaling
                if (_options.EnableLoadBalancing)
                {
                    var queueUtilization = (double)stats.QueuedJobs / _options.MaxQueueSize * 100;
                    
                    if (queueUtilization > _options.ScaleUpThreshold)
                    {
                        _logger.LogInformation("Queue utilization {Utilization:F1}% - consider scaling up workers", queueUtilization);
                    }
                    else if (queueUtilization < _options.ScaleDownThreshold)
                    {
                        _logger.LogDebug("Queue utilization {Utilization:F1}% - consider scaling down workers", queueUtilization);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during metrics collection");
            }
        }

        private void UpdateStats(Action<ProcessingStats> statsAction)
        {
            if (!_options.EnableMetrics)
                return;

            lock (_statsLock)
            {
                statsAction(_stats);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            
            _healthCheckTimer?.Dispose();
            _cleanupTimer?.Dispose();
            _metricsTimer?.Dispose();
            _concurrencyLimiter?.Dispose();

            _logger.LogInformation("Distributed processing service disposed");
        }
    }

    // Example job implementation
    public abstract class BaseProcessingJob<T> : IProcessingJob<T>
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public abstract string Type { get; }
        public T Data { get; set; }
        public ProcessingPriority Priority { get; set; } = ProcessingPriority.Normal;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);
        public Dictionary<string, object> Metadata { get; set; } = new();

        public abstract Task<object> ExecuteAsync(CancellationToken cancellationToken);
    }

    // Example worker implementation  
    public abstract class BaseJobWorker<T> : IJobWorker<T>
    {
        public abstract string WorkerType { get; }
        public abstract Task<object> ProcessAsync(T data, CancellationToken cancellationToken);
        public abstract bool CanHandle(string jobType);
    }
}