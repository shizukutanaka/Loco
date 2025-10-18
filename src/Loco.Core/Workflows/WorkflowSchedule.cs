namespace Loco.Core.Workflows;

/// <summary>
/// Represents a schedule configuration for workflow execution.
/// </summary>
public class WorkflowSchedule
{
    /// <summary>
    /// Cron expression for scheduling (e.g., "0 0 * * *" for daily at midnight).
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Interval in seconds for periodic execution.
    /// </summary>
    public int? IntervalSeconds { get; set; }

    /// <summary>
    /// Specific date/time to run the workflow (one-time execution).
    /// </summary>
    public DateTime? RunAt { get; set; }

    /// <summary>
    /// Days of week to run (e.g., "Monday,Friday").
    /// </summary>
    public string? DaysOfWeek { get; set; }

    /// <summary>
    /// Time of day to run (e.g., "09:00", "14:30").
    /// </summary>
    public string? TimeOfDay { get; set; }

    /// <summary>
    /// Maximum number of times to run (0 = unlimited).
    /// </summary>
    public int MaxExecutions { get; set; } = 0;

    /// <summary>
    /// Whether the schedule is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Represents timing constraints for workflow execution.
/// </summary>
public class WorkflowTiming
{
    /// <summary>
    /// Maximum allowed duration for the entire workflow.
    /// </summary>
    public int? MaxDurationSeconds { get; set; }

    /// <summary>
    /// Delay before starting the workflow (in seconds).
    /// </summary>
    public int? StartDelaySeconds { get; set; }

    /// <summary>
    /// Delay between steps (in seconds).
    /// </summary>
    public int? StepDelaySeconds { get; set; }

    /// <summary>
    /// Earliest time of day to allow execution (e.g., "08:00").
    /// </summary>
    public string? EarliestStartTime { get; set; }

    /// <summary>
    /// Latest time of day to allow execution (e.g., "18:00").
    /// </summary>
    public string? LatestStartTime { get; set; }

    /// <summary>
    /// Whether to skip execution if outside allowed time window.
    /// </summary>
    public bool SkipOutsideWindow { get; set; } = true;
}

/// <summary>
/// Partial class extension for WorkflowDefinition to support scheduling.
/// </summary>
public partial class WorkflowDefinition
{
    /// <summary>
    /// Schedule configuration for this workflow.
    /// </summary>
    public WorkflowSchedule? Schedule { get; set; }

    /// <summary>
    /// Timing constraints for this workflow.
    /// </summary>
    public WorkflowTiming? Timing { get; set; }
}

/// <summary>
/// Utility for checking schedule and timing constraints.
/// </summary>
public static class ScheduleChecker
{
    /// <summary>
    /// Checks if a workflow should run based on timing constraints.
    /// </summary>
    public static bool IsWithinAllowedWindow(WorkflowTiming timing)
    {
        if (timing == null)
            return true;

        var now = DateTime.Now.TimeOfDay;

        if (!string.IsNullOrEmpty(timing.EarliestStartTime))
        {
            if (TimeSpan.TryParse(timing.EarliestStartTime, out var earliest))
            {
                if (now < earliest)
                {
                    Console.WriteLine($"  ⏰ Current time {now:hh\\:mm} is before earliest start time {earliest:hh\\:mm}");
                    return !timing.SkipOutsideWindow;
                }
            }
        }

        if (!string.IsNullOrEmpty(timing.LatestStartTime))
        {
            if (TimeSpan.TryParse(timing.LatestStartTime, out var latest))
            {
                if (now > latest)
                {
                    Console.WriteLine($"  ⏰ Current time {now:hh\\:mm} is after latest start time {latest:hh\\:mm}");
                    return !timing.SkipOutsideWindow;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the next scheduled run time based on schedule configuration.
    /// </summary>
    public static DateTime? GetNextRunTime(WorkflowSchedule schedule)
    {
        if (schedule == null || !schedule.Enabled)
            return null;

        var now = DateTime.Now;

        // One-time execution
        if (schedule.RunAt.HasValue)
        {
            return schedule.RunAt.Value > now ? schedule.RunAt.Value : null;
        }

        // Interval-based
        if (schedule.IntervalSeconds.HasValue && schedule.IntervalSeconds.Value > 0)
        {
            return now.AddSeconds(schedule.IntervalSeconds.Value);
        }

        // Time of day based
        if (!string.IsNullOrEmpty(schedule.TimeOfDay))
        {
            if (TimeSpan.TryParse(schedule.TimeOfDay, out var timeOfDay))
            {
                var nextRun = now.Date.Add(timeOfDay);

                if (nextRun <= now)
                {
                    nextRun = nextRun.AddDays(1);
                }

                // Check days of week
                if (!string.IsNullOrEmpty(schedule.DaysOfWeek))
                {
                    var allowedDays = schedule.DaysOfWeek.Split(',')
                        .Select(d => Enum.TryParse<DayOfWeek>(d.Trim(), true, out var day) ? day : (DayOfWeek?)null)
                        .Where(d => d.HasValue)
                        .Select(d => d!.Value)
                        .ToList();

                    while (!allowedDays.Contains(nextRun.DayOfWeek))
                    {
                        nextRun = nextRun.AddDays(1);
                    }
                }

                return nextRun;
            }
        }

        return null;
    }

    /// <summary>
    /// Formats the next run time as a human-readable string.
    /// </summary>
    public static string FormatNextRunTime(DateTime? nextRun)
    {
        if (!nextRun.HasValue)
            return "Not scheduled";

        var now = DateTime.Now;
        var timeUntil = nextRun.Value - now;

        if (timeUntil.TotalDays >= 1)
        {
            return $"{nextRun.Value:yyyy-MM-dd HH:mm} (in {timeUntil.TotalDays:F1} days)";
        }
        else if (timeUntil.TotalHours >= 1)
        {
            return $"{nextRun.Value:HH:mm} (in {timeUntil.TotalHours:F1} hours)";
        }
        else
        {
            return $"{nextRun.Value:HH:mm} (in {timeUntil.TotalMinutes:F0} minutes)";
        }
    }
}
