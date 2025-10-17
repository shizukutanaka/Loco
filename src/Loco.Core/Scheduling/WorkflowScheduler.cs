using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Scheduling
{
    /// <summary>
    /// Advanced workflow scheduler with cron expressions, time zones, and execution windows.
    /// Based on n8n Schedule Trigger, Zapier Scheduled Tasks, and Temporal.io cron workflows.
    /// Supports: Cron expressions, Timezone handling, Execution windows, Missed run policies.
    /// </summary>
    public class WorkflowScheduler
    {
        private readonly Dictionary<string, ScheduledWorkflow> _schedules = new();
        private readonly Dictionary<string, List<ScheduleExecution>> _executionHistory = new();
        private readonly SchedulerConfiguration _config;
        private Timer? _schedulerTimer;
        private readonly SemaphoreSlim _executionLock = new(1, 1);

        public WorkflowScheduler(SchedulerConfiguration? config = null)
        {
            _config = config ?? SchedulerConfiguration.Default();
        }

        #region Schedule Management

        public async Task<ScheduledWorkflow> CreateScheduleAsync(
            string scheduleId, string workflowId, string cronExpression,
            TimeZoneInfo? timezone = null, ScheduleOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(5, cancellationToken);

            if (_schedules.ContainsKey(scheduleId))
                throw new InvalidOperationException($"Schedule {scheduleId} already exists");

            var schedule = new ScheduledWorkflow
            {
                ScheduleId = scheduleId,
                WorkflowId = workflowId,
                CronExpression = cronExpression,
                Timezone = timezone ?? TimeZoneInfo.Local,
                Options = options ?? ScheduleOptions.Default(),
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                Status = ScheduleStatus.Active
            };

            // Validate cron expression
            if (!IsValidCronExpression(cronExpression))
                throw new ArgumentException($"Invalid cron expression: {cronExpression}");

            // Calculate next run
            schedule.NextRunAt = CalculateNextRun(schedule);

            _schedules[scheduleId] = schedule;

            return schedule;
        }

        public async Task<ScheduledWorkflow?> GetScheduleAsync(
            string scheduleId, CancellationToken cancellationToken = default)
        {
            await Task.Delay(2, cancellationToken);
            return _schedules.TryGetValue(scheduleId, out var schedule) ? schedule : null;
        }

        public async Task<List<ScheduledWorkflow>> ListSchedulesAsync(
            string? workflowId = null, bool? isEnabled = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(5, cancellationToken);

            var schedules = _schedules.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(workflowId))
                schedules = schedules.Where(s => s.WorkflowId == workflowId);

            if (isEnabled.HasValue)
                schedules = schedules.Where(s => s.IsEnabled == isEnabled.Value);

            return schedules.OrderBy(s => s.NextRunAt).ToList();
        }

        public async Task EnableScheduleAsync(
            string scheduleId, CancellationToken cancellationToken = default)
        {
            await Task.Delay(2, cancellationToken);

            if (!_schedules.TryGetValue(scheduleId, out var schedule))
                throw new InvalidOperationException($"Schedule {scheduleId} not found");

            schedule.IsEnabled = true;
            schedule.Status = ScheduleStatus.Active;
            schedule.NextRunAt = CalculateNextRun(schedule);
        }

        public async Task DisableScheduleAsync(
            string scheduleId, CancellationToken cancellationToken = default)
        {
            await Task.Delay(2, cancellationToken);

            if (!_schedules.TryGetValue(scheduleId, out var schedule))
                throw new InvalidOperationException($"Schedule {scheduleId} not found");

            schedule.IsEnabled = false;
            schedule.Status = ScheduleStatus.Paused;
            schedule.NextRunAt = null;
        }

        public async Task DeleteScheduleAsync(
            string scheduleId, CancellationToken cancellationToken = default)
        {
            await Task.Delay(2, cancellationToken);

            _schedules.Remove(scheduleId);
            _executionHistory.Remove(scheduleId);
        }

        #endregion

        #region Scheduler Engine

        public void Start()
        {
            if (_schedulerTimer != null)
                throw new InvalidOperationException("Scheduler already started");

            _schedulerTimer = new Timer(
                async _ => await CheckAndExecuteSchedulesAsync(CancellationToken.None),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(_config.CheckIntervalSeconds));
        }

        public void Stop()
        {
            _schedulerTimer?.Dispose();
            _schedulerTimer = null;
        }

        private async Task CheckAndExecuteSchedulesAsync(CancellationToken cancellationToken)
        {
            await _executionLock.WaitAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                var dueSchedules = _schedules.Values
                    .Where(s => s.IsEnabled && s.NextRunAt.HasValue && s.NextRunAt.Value <= now)
                    .ToList();

                foreach (var schedule in dueSchedules)
                {
                    await ExecuteScheduleAsync(schedule, cancellationToken);
                }
            }
            finally
            {
                _executionLock.Release();
            }
        }

        private async Task ExecuteScheduleAsync(ScheduledWorkflow schedule, CancellationToken cancellationToken)
        {
            var execution = new ScheduleExecution
            {
                ExecutionId = Guid.NewGuid().ToString(),
                ScheduleId = schedule.ScheduleId,
                WorkflowId = schedule.WorkflowId,
                ScheduledTime = schedule.NextRunAt!.Value,
                ActualStartTime = DateTime.UtcNow,
                Status = ExecutionStatus.Running
            };

            // Check execution window
            if (!IsWithinExecutionWindow(schedule))
            {
                execution.Status = ExecutionStatus.Skipped;
                execution.SkipReason = "Outside execution window";
                await RecordExecutionAsync(execution, cancellationToken);

                schedule.NextRunAt = CalculateNextRun(schedule);
                return;
            }

            // Check max concurrent executions
            if (schedule.Options.MaxConcurrentExecutions > 0)
            {
                var runningCount = GetRunningExecutionCount(schedule.ScheduleId);
                if (runningCount >= schedule.Options.MaxConcurrentExecutions)
                {
                    execution.Status = ExecutionStatus.Skipped;
                    execution.SkipReason = "Max concurrent executions reached";
                    await RecordExecutionAsync(execution, cancellationToken);

                    schedule.NextRunAt = CalculateNextRun(schedule);
                    return;
                }
            }

            try
            {
                // Execute workflow (simulated)
                await Task.Delay(100, cancellationToken);

                execution.Status = ExecutionStatus.Completed;
                execution.ActualEndTime = DateTime.UtcNow;
                execution.Duration = execution.ActualEndTime.Value - execution.ActualStartTime;

                schedule.LastRunAt = DateTime.UtcNow;
                schedule.RunCount++;
                schedule.SuccessCount++;
            }
            catch (Exception ex)
            {
                execution.Status = ExecutionStatus.Failed;
                execution.Error = ex.Message;
                execution.ActualEndTime = DateTime.UtcNow;
                execution.Duration = execution.ActualEndTime.Value - execution.ActualStartTime;

                schedule.FailureCount++;

                // Handle failure based on policy
                await HandleExecutionFailureAsync(schedule, execution, cancellationToken);
            }

            await RecordExecutionAsync(execution, cancellationToken);

            // Calculate next run
            schedule.NextRunAt = CalculateNextRun(schedule);
        }

        private async Task HandleExecutionFailureAsync(
            ScheduledWorkflow schedule, ScheduleExecution execution, CancellationToken cancellationToken)
        {
            await Task.Delay(2, cancellationToken);

            switch (schedule.Options.FailurePolicy)
            {
                case FailurePolicy.Continue:
                    // Continue with next scheduled run
                    break;

                case FailurePolicy.Pause:
                    schedule.IsEnabled = false;
                    schedule.Status = ScheduleStatus.Paused;
                    break;

                case FailurePolicy.RetryOnce:
                    if (execution.RetryCount < 1)
                    {
                        execution.RetryCount++;
                        await Task.Delay(TimeSpan.FromSeconds(schedule.Options.RetryDelaySeconds), cancellationToken);
                        await ExecuteScheduleAsync(schedule, cancellationToken);
                    }
                    break;

                case FailurePolicy.DisableAfterThreshold:
                    if (schedule.FailureCount >= schedule.Options.FailureThreshold)
                    {
                        schedule.IsEnabled = false;
                        schedule.Status = ScheduleStatus.Error;
                    }
                    break;
            }
        }

        #endregion

        #region Missed Run Handling

        public async Task HandleMissedRunsAsync(
            string scheduleId, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);

            if (!_schedules.TryGetValue(scheduleId, out var schedule))
                throw new InvalidOperationException($"Schedule {scheduleId} not found");

            if (!schedule.LastRunAt.HasValue)
                return;

            var missedRuns = CalculateMissedRuns(schedule);

            switch (schedule.Options.MissedRunPolicy)
            {
                case MissedRunPolicy.Skip:
                    // Do nothing
                    break;

                case MissedRunPolicy.RunOnce:
                    if (missedRuns.Any())
                    {
                        await ExecuteScheduleAsync(schedule, cancellationToken);
                    }
                    break;

                case MissedRunPolicy.RunAll:
                    foreach (var missedTime in missedRuns)
                    {
                        var execution = new ScheduleExecution
                        {
                            ExecutionId = Guid.NewGuid().ToString(),
                            ScheduleId = schedule.ScheduleId,
                            WorkflowId = schedule.WorkflowId,
                            ScheduledTime = missedTime,
                            ActualStartTime = DateTime.UtcNow,
                            IsMissedRunCatchup = true
                        };

                        await ExecuteScheduleAsync(schedule, cancellationToken);
                    }
                    break;
            }
        }

        private List<DateTime> CalculateMissedRuns(ScheduledWorkflow schedule)
        {
            var missedRuns = new List<DateTime>();
            if (!schedule.LastRunAt.HasValue || !schedule.NextRunAt.HasValue)
                return missedRuns;

            var currentTime = schedule.NextRunAt.Value;
            var now = DateTime.UtcNow;

            while (currentTime < now)
            {
                missedRuns.Add(currentTime);
                currentTime = GetNextCronOccurrence(schedule.CronExpression, currentTime, schedule.Timezone);

                if (missedRuns.Count >= schedule.Options.MaxMissedRunsCatchup)
                    break;
            }

            return missedRuns;
        }

        #endregion

        #region Cron Expression Handling

        private bool IsValidCronExpression(string cronExpression)
        {
            // Simplified validation - supports standard 5-field cron
            var parts = cronExpression.Split(' ');
            return parts.Length == 5;
        }

        private DateTime? CalculateNextRun(ScheduledWorkflow schedule)
        {
            if (!schedule.IsEnabled)
                return null;

            var baseTime = schedule.LastRunAt ?? DateTime.UtcNow;
            return GetNextCronOccurrence(schedule.CronExpression, baseTime, schedule.Timezone);
        }

        private DateTime GetNextCronOccurrence(string cronExpression, DateTime from, TimeZoneInfo timezone)
        {
            // Simplified cron parsing - supports common patterns
            var parts = cronExpression.Split(' ');
            if (parts.Length != 5)
                return from.AddMinutes(1);

            var minute = parts[0];
            var hour = parts[1];
            var dayOfMonth = parts[2];
            var month = parts[3];
            var dayOfWeek = parts[4];

            var next = from.AddMinutes(1);

            // Handle simple cases
            if (minute == "*" && hour == "*")
            {
                // Every minute
                return next;
            }
            else if (minute != "*" && hour == "*")
            {
                // Every hour at specific minute
                var targetMinute = int.Parse(minute);
                if (next.Minute < targetMinute)
                    return new DateTime(next.Year, next.Month, next.Day, next.Hour, targetMinute, 0);
                else
                    return new DateTime(next.Year, next.Month, next.Day, next.Hour, targetMinute, 0).AddHours(1);
            }
            else if (minute != "*" && hour != "*")
            {
                // Daily at specific time
                var targetHour = int.Parse(hour);
                var targetMinute = int.Parse(minute);
                var targetTime = new DateTime(next.Year, next.Month, next.Day, targetHour, targetMinute, 0);

                if (next > targetTime)
                    targetTime = targetTime.AddDays(1);

                return targetTime;
            }

            return next.AddMinutes(1);
        }

        #endregion

        #region Execution Window

        private bool IsWithinExecutionWindow(ScheduledWorkflow schedule)
        {
            if (!schedule.Options.ExecutionWindowStart.HasValue || !schedule.Options.ExecutionWindowEnd.HasValue)
                return true;

            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, schedule.Timezone);
            var currentTime = now.TimeOfDay;

            var start = schedule.Options.ExecutionWindowStart.Value;
            var end = schedule.Options.ExecutionWindowEnd.Value;

            if (start < end)
            {
                return currentTime >= start && currentTime <= end;
            }
            else
            {
                // Window crosses midnight
                return currentTime >= start || currentTime <= end;
            }
        }

        #endregion

        #region Execution History

        private async Task RecordExecutionAsync(ScheduleExecution execution, CancellationToken cancellationToken)
        {
            await Task.Delay(2, cancellationToken);

            if (!_executionHistory.ContainsKey(execution.ScheduleId))
            {
                _executionHistory[execution.ScheduleId] = new List<ScheduleExecution>();
            }

            _executionHistory[execution.ScheduleId].Add(execution);

            // Enforce history limit
            if (_executionHistory[execution.ScheduleId].Count > _config.MaxExecutionHistoryPerSchedule)
            {
                _executionHistory[execution.ScheduleId].RemoveAt(0);
            }
        }

        public async Task<List<ScheduleExecution>> GetExecutionHistoryAsync(
            string scheduleId, int limit = 100, CancellationToken cancellationToken = default)
        {
            await Task.Delay(5, cancellationToken);

            if (!_executionHistory.TryGetValue(scheduleId, out var history))
                return new List<ScheduleExecution>();

            return history.OrderByDescending(e => e.ScheduledTime).Take(limit).ToList();
        }

        private int GetRunningExecutionCount(string scheduleId)
        {
            if (!_executionHistory.TryGetValue(scheduleId, out var history))
                return 0;

            return history.Count(e => e.Status == ExecutionStatus.Running);
        }

        #endregion

        #region Statistics

        public async Task<SchedulerStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);

            return new SchedulerStatistics
            {
                TotalSchedules = _schedules.Count,
                ActiveSchedules = _schedules.Values.Count(s => s.IsEnabled),
                PausedSchedules = _schedules.Values.Count(s => !s.IsEnabled),
                TotalExecutions = _executionHistory.Values.Sum(h => h.Count),
                SuccessfulExecutions = _schedules.Values.Sum(s => s.SuccessCount),
                FailedExecutions = _schedules.Values.Sum(s => s.FailureCount),
                NextScheduledRuns = _schedules.Values
                    .Where(s => s.NextRunAt.HasValue)
                    .OrderBy(s => s.NextRunAt)
                    .Take(10)
                    .Select(s => new { s.ScheduleId, s.NextRunAt })
                    .ToList()
            };
        }

        #endregion
    }

    #region Models

    public class ScheduledWorkflow
    {
        public string ScheduleId { get; set; } = "";
        public string WorkflowId { get; set; } = "";
        public string CronExpression { get; set; } = "";
        public TimeZoneInfo Timezone { get; set; } = TimeZoneInfo.Utc;
        public ScheduleOptions Options { get; set; } = ScheduleOptions.Default();
        public bool IsEnabled { get; set; }
        public ScheduleStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? NextRunAt { get; set; }
        public DateTime? LastRunAt { get; set; }
        public long RunCount { get; set; }
        public long SuccessCount { get; set; }
        public long FailureCount { get; set; }
    }

    public class ScheduleOptions
    {
        public MissedRunPolicy MissedRunPolicy { get; set; } = MissedRunPolicy.Skip;
        public FailurePolicy FailurePolicy { get; set; } = FailurePolicy.Continue;
        public int MaxConcurrentExecutions { get; set; } = 1;
        public int MaxMissedRunsCatchup { get; set; } = 10;
        public int FailureThreshold { get; set; } = 5;
        public int RetryDelaySeconds { get; set; } = 60;
        public TimeSpan? ExecutionWindowStart { get; set; }
        public TimeSpan? ExecutionWindowEnd { get; set; }

        public static ScheduleOptions Default() => new();
    }

    public class ScheduleExecution
    {
        public string ExecutionId { get; set; } = "";
        public string ScheduleId { get; set; } = "";
        public string WorkflowId { get; set; } = "";
        public DateTime ScheduledTime { get; set; }
        public DateTime ActualStartTime { get; set; }
        public DateTime? ActualEndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public ExecutionStatus Status { get; set; }
        public string? Error { get; set; }
        public string? SkipReason { get; set; }
        public bool IsMissedRunCatchup { get; set; }
        public int RetryCount { get; set; }
    }

    public class SchedulerStatistics
    {
        public int TotalSchedules { get; set; }
        public int ActiveSchedules { get; set; }
        public int PausedSchedules { get; set; }
        public int TotalExecutions { get; set; }
        public long SuccessfulExecutions { get; set; }
        public long FailedExecutions { get; set; }
        public object? NextScheduledRuns { get; set; }
    }

    public class SchedulerConfiguration
    {
        public int CheckIntervalSeconds { get; set; } = 10;
        public int MaxExecutionHistoryPerSchedule { get; set; } = 1000;

        public static SchedulerConfiguration Default() => new();
    }

    #endregion

    #region Enums

    public enum ScheduleStatus
    {
        Active,
        Paused,
        Error
    }

    public enum ExecutionStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Skipped
    }

    public enum MissedRunPolicy
    {
        Skip,
        RunOnce,
        RunAll
    }

    public enum FailurePolicy
    {
        Continue,
        Pause,
        RetryOnce,
        DisableAfterThreshold
    }

    #endregion
}
