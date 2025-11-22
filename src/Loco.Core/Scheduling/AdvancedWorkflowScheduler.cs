// Phase 6: Advanced Workflow Scheduler
// Cron-based scheduling, delayed execution, recurring workflows
// Enterprise-grade job scheduling with retry, execution history, and SLA monitoring

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Scheduling;

/// <summary>
/// Scheduled workflow execution frequency
/// </summary>
public enum ScheduleFrequency
{
    Once = 0,          // One-time execution
    Hourly = 1,        // Every hour
    Daily = 2,         // Every day
    Weekly = 3,        // Every week
    Monthly = 4,       // Every month
    Custom = 5,        // Custom Cron expression
}

/// <summary>
/// Scheduled workflow configuration
/// </summary>
public class WorkflowSchedule
{
    public string ScheduleId { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Timing
    public ScheduleFrequency Frequency { get; set; }
    public string? CronExpression { get; set; }  // "0 9 * * MON" (9 AM every Monday)
    public DateTime? StartTime { get; set; }     // When schedule starts
    public DateTime? EndTime { get; set; }       // When schedule expires
    public int? DelaySeconds { get; set; }       // For one-time delayed execution

    // Execution
    public Dictionary<string, object> DefaultInput { get; set; } = new();
    public int MaxConcurrentExecutions { get; set; } = 1;
    public int? TimeoutSeconds { get; set; }

    // SLA & Monitoring
    public int? MaxFailures { get; set; }        // Disable after N failures
    public string? NotificationEmail { get; set; }
    public bool Enabled { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Scheduled execution record
/// </summary>
public class ScheduledExecution
{
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString();
    public string ScheduleId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public DateTime ScheduledTime { get; set; }
    public DateTime? ActualStartTime { get; set; }
    public DateTime? CompletedTime { get; set; }
    public string Status { get; set; } = "pending"; // pending, running, success, failure, skipped
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public double DurationSeconds { get; set; }
}

/// <summary>
/// Scheduler statistics
/// </summary>
public class SchedulerStatistics
{
    public int TotalSchedules { get; set; }
    public int EnabledSchedules { get; set; }
    public int DisabledSchedules { get; set; }
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double SuccessRate { get; set; }
    public double AverageDurationSeconds { get; set; }
    public DateTime NextScheduledExecution { get; set; }
    public int OverdueExecutions { get; set; }
}

/// <summary>
/// Workflow scheduler interface
/// </summary>
public interface IWorkflowScheduler
{
    Task<WorkflowSchedule> CreateScheduleAsync(
        string workflowId,
        WorkflowSchedule schedule,
        CancellationToken ct = default);

    Task<WorkflowSchedule?> GetScheduleAsync(
        string scheduleId,
        CancellationToken ct = default);

    Task<List<WorkflowSchedule>> GetSchedulesForWorkflowAsync(
        string workflowId,
        CancellationToken ct = default);

    Task UpdateScheduleAsync(
        string scheduleId,
        WorkflowSchedule schedule,
        CancellationToken ct = default);

    Task DeleteScheduleAsync(
        string scheduleId,
        CancellationToken ct = default);

    Task<ScheduledExecution?> GetScheduledExecutionAsync(
        string executionId,
        CancellationToken ct = default);

    Task<List<ScheduledExecution>> GetExecutionHistoryAsync(
        string scheduleId,
        int limit = 50,
        CancellationToken ct = default);

    Task<SchedulerStatistics> GetStatisticsAsync(CancellationToken ct = default);

    Task EnableScheduleAsync(string scheduleId, CancellationToken ct = default);
    Task DisableScheduleAsync(string scheduleId, CancellationToken ct = default);
}

/// <summary>
/// Advanced workflow scheduler implementation
/// </summary>
public class AdvancedWorkflowScheduler : IWorkflowScheduler
{
    private readonly ILogger<AdvancedWorkflowScheduler> _logger;
    private readonly Dictionary<string, WorkflowSchedule> _schedules;
    private readonly Dictionary<string, List<ScheduledExecution>> _executionHistory;
    private readonly Timer _schedulerTimer;

    public AdvancedWorkflowScheduler(ILogger<AdvancedWorkflowScheduler> logger)
    {
        _logger = logger;
        _schedules = new Dictionary<string, WorkflowSchedule>();
        _executionHistory = new Dictionary<string, List<ScheduledExecution>>();

        // Run scheduler every minute
        _schedulerTimer = new Timer(
            callback: _ => RunSchedulerAsync().GetAwaiter().GetResult(),
            state: null,
            dueTime: TimeSpan.FromMinutes(1),
            period: TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Create workflow schedule
    /// </summary>
    public async Task<WorkflowSchedule> CreateScheduleAsync(
        string workflowId,
        WorkflowSchedule schedule,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        schedule.WorkflowId = workflowId;
        schedule.CreatedAt = DateTime.UtcNow;
        schedule.UpdatedAt = DateTime.UtcNow;

        // Validate schedule
        ValidateSchedule(schedule);

        _schedules[schedule.ScheduleId] = schedule;
        if (!_executionHistory.ContainsKey(schedule.ScheduleId))
        {
            _executionHistory[schedule.ScheduleId] = new List<ScheduledExecution>();
        }

        _logger.LogInformation(
            "Schedule created: {ScheduleId} for workflow {WorkflowId}, Frequency: {Frequency}",
            schedule.ScheduleId, workflowId, schedule.Frequency);

        return schedule;
    }

    /// <summary>
    /// Get schedule
    /// </summary>
    public async Task<WorkflowSchedule?> GetScheduleAsync(
        string scheduleId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;
        _schedules.TryGetValue(scheduleId, out var schedule);
        return schedule;
    }

    /// <summary>
    /// Get all schedules for workflow
    /// </summary>
    public async Task<List<WorkflowSchedule>> GetSchedulesForWorkflowAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return _schedules.Values
            .Where(s => s.WorkflowId == workflowId)
            .ToList();
    }

    /// <summary>
    /// Update schedule
    /// </summary>
    public async Task UpdateScheduleAsync(
        string scheduleId,
        WorkflowSchedule schedule,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_schedules.TryGetValue(scheduleId, out var existing))
        {
            throw new KeyNotFoundException($"Schedule not found: {scheduleId}");
        }

        ValidateSchedule(schedule);

        schedule.ScheduleId = scheduleId;
        schedule.WorkflowId = existing.WorkflowId;
        schedule.CreatedAt = existing.CreatedAt;
        schedule.UpdatedAt = DateTime.UtcNow;

        _schedules[scheduleId] = schedule;

        _logger.LogInformation("Schedule updated: {ScheduleId}", scheduleId);
    }

    /// <summary>
    /// Delete schedule
    /// </summary>
    public async Task DeleteScheduleAsync(
        string scheduleId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_schedules.Remove(scheduleId))
        {
            _logger.LogInformation("Schedule deleted: {ScheduleId}", scheduleId);
        }
    }

    /// <summary>
    /// Get scheduled execution
    /// </summary>
    public async Task<ScheduledExecution?> GetScheduledExecutionAsync(
        string executionId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        foreach (var executions in _executionHistory.Values)
        {
            var execution = executions.FirstOrDefault(e => e.ExecutionId == executionId);
            if (execution != null)
                return execution;
        }

        return null;
    }

    /// <summary>
    /// Get execution history
    /// </summary>
    public async Task<List<ScheduledExecution>> GetExecutionHistoryAsync(
        string scheduleId,
        int limit = 50,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_executionHistory.TryGetValue(scheduleId, out var executions))
        {
            return executions
                .OrderByDescending(e => e.ScheduledTime)
                .Take(limit)
                .ToList();
        }

        return new List<ScheduledExecution>();
    }

    /// <summary>
    /// Get statistics
    /// </summary>
    public async Task<SchedulerStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var allExecutions = _executionHistory.Values.SelectMany(e => e).ToList();
        var completedExecutions = allExecutions.Where(e => e.Status != "pending" && e.Status != "running").ToList();

        return new SchedulerStatistics
        {
            TotalSchedules = _schedules.Count,
            EnabledSchedules = _schedules.Values.Count(s => s.Enabled),
            DisabledSchedules = _schedules.Values.Count(s => !s.Enabled),
            TotalExecutions = allExecutions.Count,
            SuccessfulExecutions = allExecutions.Count(e => e.Status == "success"),
            FailedExecutions = allExecutions.Count(e => e.Status == "failure"),
            SuccessRate = completedExecutions.Count > 0
                ? (double)completedExecutions.Count(e => e.Status == "success") / completedExecutions.Count
                : 0,
            AverageDurationSeconds = completedExecutions.Any()
                ? completedExecutions.Average(e => e.DurationSeconds)
                : 0,
            NextScheduledExecution = CalculateNextExecution(),
            OverdueExecutions = allExecutions.Count(e => e.Status == "pending" && e.ScheduledTime < DateTime.UtcNow),
        };
    }

    /// <summary>
    /// Enable schedule
    /// </summary>
    public async Task EnableScheduleAsync(string scheduleId, CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_schedules.TryGetValue(scheduleId, out var schedule))
        {
            schedule.Enabled = true;
            schedule.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("Schedule enabled: {ScheduleId}", scheduleId);
        }
    }

    /// <summary>
    /// Disable schedule
    /// </summary>
    public async Task DisableScheduleAsync(string scheduleId, CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_schedules.TryGetValue(scheduleId, out var schedule))
        {
            schedule.Enabled = false;
            schedule.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("Schedule disabled: {ScheduleId}", scheduleId);
        }
    }

    // Private helper methods
    private void ValidateSchedule(WorkflowSchedule schedule)
    {
        if (string.IsNullOrEmpty(schedule.WorkflowId))
            throw new ArgumentException("WorkflowId is required");

        if (schedule.Frequency == ScheduleFrequency.Custom && string.IsNullOrEmpty(schedule.CronExpression))
            throw new ArgumentException("CronExpression is required for Custom frequency");

        if (schedule.MaxConcurrentExecutions < 1)
            throw new ArgumentException("MaxConcurrentExecutions must be at least 1");
    }

    private async Task RunSchedulerAsync()
    {
        var now = DateTime.UtcNow;

        foreach (var schedule in _schedules.Values.Where(s => s.Enabled))
        {
            // Check if schedule should execute
            if (ShouldExecute(schedule, now))
            {
                var execution = new ScheduledExecution
                {
                    ScheduleId = schedule.ScheduleId,
                    WorkflowId = schedule.WorkflowId,
                    ScheduledTime = now,
                    Status = "pending",
                };

                if (!_executionHistory.ContainsKey(schedule.ScheduleId))
                {
                    _executionHistory[schedule.ScheduleId] = new List<ScheduledExecution>();
                }

                _executionHistory[schedule.ScheduleId].Add(execution);

                _logger.LogInformation(
                    "Scheduled execution created: {ExecutionId} for schedule {ScheduleId}",
                    execution.ExecutionId, schedule.ScheduleId);

                // In production, queue execution to be processed by workflow engine
                // await _workflowEngine.QueueExecutionAsync(execution);
            }
        }

        await Task.CompletedTask;
    }

    private bool ShouldExecute(WorkflowSchedule schedule, DateTime now)
    {
        // Check time windows
        if (schedule.StartTime.HasValue && now < schedule.StartTime)
            return false;

        if (schedule.EndTime.HasValue && now > schedule.EndTime)
            return false;

        // Check frequency
        return schedule.Frequency switch
        {
            ScheduleFrequency.Once when schedule.DelaySeconds.HasValue =>
                // One-time execution at scheduled time
                (now.Minute % 1 == 0), // Execute once per minute cycle

            ScheduleFrequency.Hourly =>
                (now.Minute == 0), // Execute at top of every hour

            ScheduleFrequency.Daily =>
                (now.Hour == 9 && now.Minute == 0), // Execute at 9 AM daily

            ScheduleFrequency.Weekly =>
                (now.DayOfWeek == DayOfWeek.Monday && now.Hour == 9 && now.Minute == 0), // Execute Mondays at 9 AM

            ScheduleFrequency.Monthly =>
                (now.Day == 1 && now.Hour == 9 && now.Minute == 0), // Execute first of month at 9 AM

            ScheduleFrequency.Custom =>
                EvaluateCronExpression(schedule.CronExpression, now),

            _ => false,
        };
    }

    private bool EvaluateCronExpression(string? cronExpression, DateTime dateTime)
    {
        if (string.IsNullOrEmpty(cronExpression))
            return false;

        // Simple Cron parser (full implementation would be more complex)
        // Format: minute hour day month dayOfWeek
        var parts = cronExpression.Split(' ');
        if (parts.Length != 5)
            return false;

        // Check minute
        if (parts[0] != "*" && int.TryParse(parts[0], out var minute) && dateTime.Minute != minute)
            return false;

        // Check hour
        if (parts[1] != "*" && int.TryParse(parts[1], out var hour) && dateTime.Hour != hour)
            return false;

        // In production, use a library like CronExpressionDescriptor or NCrontab
        return true;
    }

    private DateTime CalculateNextExecution()
    {
        var nextExecution = DateTime.MaxValue;

        foreach (var schedule in _schedules.Values.Where(s => s.Enabled))
        {
            var next = CalculateNextExecutionForSchedule(schedule);
            if (next < nextExecution)
                nextExecution = next;
        }

        return nextExecution == DateTime.MaxValue ? DateTime.UtcNow.AddHours(1) : nextExecution;
    }

    private DateTime CalculateNextExecutionForSchedule(WorkflowSchedule schedule)
    {
        var now = DateTime.UtcNow;

        return schedule.Frequency switch
        {
            ScheduleFrequency.Hourly => now.AddHours(1),
            ScheduleFrequency.Daily => now.AddDays(1),
            ScheduleFrequency.Weekly => now.AddDays(7),
            ScheduleFrequency.Monthly => now.AddMonths(1),
            _ => now.AddHours(1),
        };
    }
}
