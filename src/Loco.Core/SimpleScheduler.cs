using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core
{
    /// <summary>
    /// Lightweight interval-based scheduler for SimpleLightEngine
    /// </summary>
    public class SimpleScheduler : IDisposable
    {
        private readonly ConcurrentDictionary<string, ScheduledJob> _jobs;
        private readonly Timer _timer;
        private readonly ILogger? _logger;
        private bool _disposed;

        public SimpleScheduler(ILogger? logger = null)
        {
            _logger = logger;
            _jobs = new ConcurrentDictionary<string, ScheduledJob>();
            _timer = new Timer(CheckJobs, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Schedule a job to run at specified interval
        /// </summary>
        public void ScheduleInterval(string jobId, TimeSpan interval, Func<Task> action)
        {
            var job = new ScheduledJob
            {
                JobId = jobId,
                Interval = interval,
                Action = action,
                NextRun = DateTime.UtcNow.Add(interval)
            };

            _jobs[jobId] = job;
            _logger?.LogInformation("Scheduled job: {JobId} every {Interval}", jobId, interval);
        }

        /// <summary>
        /// Schedule a job to run once at specified time
        /// </summary>
        public void ScheduleOnce(string jobId, DateTime runAt, Func<Task> action)
        {
            var job = new ScheduledJob
            {
                JobId = jobId,
                Interval = TimeSpan.Zero,
                Action = action,
                NextRun = runAt,
                RunOnce = true
            };

            _jobs[jobId] = job;
            _logger?.LogInformation("Scheduled one-time job: {JobId} at {RunAt}", jobId, runAt);
        }

        /// <summary>
        /// Remove a scheduled job
        /// </summary>
        public bool RemoveJob(string jobId)
        {
            return _jobs.TryRemove(jobId, out _);
        }

        private void CheckJobs(object? state)
        {
            var now = DateTime.UtcNow;

            foreach (var kvp in _jobs)
            {
                var job = kvp.Value;

                if (job.NextRun <= now && !job.IsRunning)
                {
                    job.IsRunning = true;
                    // Direct Task.Run without unnecessary async wrapper
                    _ = Task.Run(() => ExecuteJobAsync(job));
                }
            }
        }

        private async Task ExecuteJobAsync(ScheduledJob job)
        {
            try
            {
                await job.Action().ConfigureAwait(false);
                job.LastRun = DateTime.UtcNow;

                if (job.RunOnce)
                {
                    _jobs.TryRemove(job.JobId, out _);
                    _logger?.LogInformation("One-time job completed and removed: {JobId}", job.JobId);
                }
                else
                {
                    job.NextRun = DateTime.UtcNow.Add(job.Interval);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Job execution failed: {JobId}", job.JobId);
            }
            finally
            {
                job.IsRunning = false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _timer?.Dispose();
            _jobs.Clear();
        }

        private class ScheduledJob
        {
            public string JobId { get; set; } = "";
            public TimeSpan Interval { get; set; }
            public Func<Task> Action { get; set; } = null!;
            public DateTime NextRun { get; set; }
            public DateTime? LastRun { get; set; }
            public bool IsRunning { get; set; }
            public bool RunOnce { get; set; }
        }
    }
}
