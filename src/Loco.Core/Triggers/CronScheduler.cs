using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Triggers;

/// <summary>
/// Cron schedule configuration.
/// </summary>
public class CronSchedule
{
    /// <summary>
    /// Cron expression (e.g., "0 0 * * *" for daily at midnight).
    /// Format: minute hour day month dayOfWeek
    /// </summary>
    public string Expression { get; set; } = "";

    /// <summary>
    /// Timezone for schedule (default: UTC).
    /// </summary>
    public string Timezone { get; set; } = "UTC";

    /// <summary>
    /// Whether the schedule is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of executions (null = unlimited).
    /// </summary>
    public int? MaxExecutions { get; set; }

    /// <summary>
    /// Start date/time (schedule won't trigger before this).
    /// </summary>
    public DateTime? StartAfter { get; set; }

    /// <summary>
    /// End date/time (schedule won't trigger after this).
    /// </summary>
    public DateTime? EndBefore { get; set; }
}

/// <summary>
/// Cron expression parser and evaluator.
/// </summary>
public class CronExpression
{
    private readonly int[] _minutes;
    private readonly int[] _hours;
    private readonly int[] _daysOfMonth;
    private readonly int[] _months;
    private readonly int[] _daysOfWeek;

    public CronExpression(string expression)
    {
        var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
        {
            throw new ArgumentException(
                "Invalid cron expression. Expected format: minute hour day month dayOfWeek");
        }

        _minutes = ParseField(parts[0], 0, 59);
        _hours = ParseField(parts[1], 0, 23);
        _daysOfMonth = ParseField(parts[2], 1, 31);
        _months = ParseField(parts[3], 1, 12);
        _daysOfWeek = ParseField(parts[4], 0, 6);
    }

    /// <summary>
    /// Gets the next occurrence after the specified time.
    /// </summary>
    public DateTime GetNextOccurrence(DateTime after)
    {
        var next = new DateTime(
            after.Year, after.Month, after.Day,
            after.Hour, after.Minute, 0, after.Kind).AddMinutes(1);

        // Safety limit to prevent infinite loops
        var maxIterations = 366 * 24 * 60; // 1 year in minutes
        var iterations = 0;

        while (iterations++ < maxIterations)
        {
            if (Matches(next))
                return next;

            next = next.AddMinutes(1);
        }

        throw new InvalidOperationException("Could not find next occurrence within 1 year");
    }

    /// <summary>
    /// Checks if a given time matches the cron expression.
    /// </summary>
    public bool Matches(DateTime time)
    {
        return _minutes.Contains(time.Minute)
            && _hours.Contains(time.Hour)
            && _daysOfMonth.Contains(time.Day)
            && _months.Contains(time.Month)
            && _daysOfWeek.Contains((int)time.DayOfWeek);
    }

    private int[] ParseField(string field, int min, int max)
    {
        // Handle wildcard
        if (field == "*")
            return Enumerable.Range(min, max - min + 1).ToArray();

        var values = new List<int>();

        foreach (var part in field.Split(','))
        {
            // Handle range (e.g., "1-5")
            if (part.Contains('-'))
            {
                var rangeParts = part.Split('-');
                var start = int.Parse(rangeParts[0]);
                var end = int.Parse(rangeParts[1]);
                values.AddRange(Enumerable.Range(start, end - start + 1));
            }
            // Handle step (e.g., "*/5")
            else if (part.Contains('/'))
            {
                var stepParts = part.Split('/');
                var step = int.Parse(stepParts[1]);
                var start = stepParts[0] == "*" ? min : int.Parse(stepParts[0]);

                for (int i = start; i <= max; i += step)
                    values.Add(i);
            }
            // Handle single value
            else
            {
                values.Add(int.Parse(part));
            }
        }

        return values.Distinct().Where(v => v >= min && v <= max).OrderBy(v => v).ToArray();
    }

    /// <summary>
    /// Gets a human-readable description of the cron expression.
    /// </summary>
    public string ToDescription()
    {
        var sb = new StringBuilder();

        // Minutes
        if (_minutes.Length == 60)
            sb.Append("every minute");
        else if (_minutes.Length == 1)
            sb.Append($"at minute {_minutes[0]}");
        else
            sb.Append($"at minutes {string.Join(", ", _minutes.Take(5))}{(_minutes.Length > 5 ? "..." : "")}");

        // Hours
        if (_hours.Length == 1)
            sb.Append($" past hour {_hours[0]}");
        else if (_hours.Length < 24)
            sb.Append($" past hours {string.Join(", ", _hours.Take(3))}{(_hours.Length > 3 ? "..." : "")}");

        // Days
        if (_daysOfMonth.Length < 31)
            sb.Append($" on day(s) {string.Join(", ", _daysOfMonth.Take(3))}{(_daysOfMonth.Length > 3 ? "..." : "")}");

        // Months
        if (_months.Length < 12)
        {
            var monthNames = _months.Take(3).Select(m => new DateTime(2000, m, 1).ToString("MMM"));
            sb.Append($" in {string.Join(", ", monthNames)}{(_months.Length > 3 ? "..." : "")}");
        }

        return sb.ToString().Trim();
    }
}

/// <summary>
/// Scheduled execution information.
/// </summary>
public class ScheduledExecution
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = "";
    public DateTime ScheduledTime { get; set; }
    public DateTime? ExecutedTime { get; set; }
    public bool Executed { get; set; }
    public bool Cancelled { get; set; }
    public double DurationSeconds { get; set; }
}

/// <summary>
/// Cron-based workflow scheduler.
/// </summary>
public class CronScheduler : IDisposable
{
    private readonly ILogger? _logger;
    private readonly Timer _timer;
    private readonly Dictionary<string, (CronSchedule Schedule, CronExpression Expression, int ExecutionCount)> _schedules = new();
    private readonly List<ScheduledExecution> _upcoming = new();
    private readonly object _lock = new();
    private bool _disposed;

    public event Func<string, DateTime, Task>? OnScheduledExecution;

    public CronScheduler(ILogger? logger = null, int checkIntervalSeconds = 30)
    {
        _logger = logger;
        _timer = new Timer(
            _ => CheckSchedules(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(checkIntervalSeconds));
    }

    /// <summary>
    /// Adds a cron schedule for a workflow.
    /// </summary>
    public void AddSchedule(string workflowId, CronSchedule schedule)
    {
        lock (_lock)
        {
            var expression = new CronExpression(schedule.Expression);
            _schedules[workflowId] = (schedule, expression, 0);

            _logger?.LogInformation(
                "Added cron schedule for workflow {WorkflowId}: {Expression} ({Description})",
                workflowId,
                schedule.Expression,
                expression.ToDescription());
        }
    }

    /// <summary>
    /// Removes a schedule for a workflow.
    /// </summary>
    public void RemoveSchedule(string workflowId)
    {
        lock (_lock)
        {
            if (_schedules.Remove(workflowId))
            {
                _logger?.LogInformation("Removed schedule for workflow {WorkflowId}", workflowId);
            }
        }
    }

    /// <summary>
    /// Gets the next scheduled execution time for a workflow.
    /// </summary>
    public DateTime? GetNextExecution(string workflowId)
    {
        lock (_lock)
        {
            if (!_schedules.TryGetValue(workflowId, out var entry))
                return null;

            var (schedule, expression, _) = entry;

            if (!schedule.Enabled)
                return null;

            var now = DateTime.UtcNow;

            if (schedule.StartAfter.HasValue && now < schedule.StartAfter.Value)
                return NextOccurrenceUtc(schedule, expression, schedule.StartAfter.Value);

            if (schedule.EndBefore.HasValue && now >= schedule.EndBefore.Value)
                return null;

            return NextOccurrenceUtc(schedule, expression, now);
        }
    }

    /// <summary>
    /// Resolves a schedule's IANA/Windows timezone id to a <see cref="TimeZoneInfo"/>,
    /// falling back to UTC (with a warning) for unknown ids.
    /// </summary>
    private TimeZoneInfo ResolveTimeZone(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone) ||
            string.Equals(timezone, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _logger?.LogWarning("Unknown timezone '{Timezone}'; falling back to UTC", timezone);
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// Computes the next occurrence AT OR AFTER <paramref name="utcAfter"/> and returns it in
    /// UTC, honoring the schedule's timezone. The cron fields ("0 9 * * *") are matched against
    /// wall-clock time in that zone, so a "9am daily" schedule fires at 9am local across DST
    /// transitions. Previously every schedule was evaluated in UTC, ignoring Timezone entirely.
    /// </summary>
    private DateTime NextOccurrenceUtc(CronSchedule schedule, CronExpression expression, DateTime utcAfter)
    {
        var tz = ResolveTimeZone(schedule.Timezone);
        if (tz == TimeZoneInfo.Utc)
        {
            return expression.GetNextOccurrence(DateTime.SpecifyKind(utcAfter, DateTimeKind.Utc));
        }

        var localAfter = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcAfter, DateTimeKind.Utc), tz);
        var localNext = expression.GetNextOccurrence(
            DateTime.SpecifyKind(localAfter, DateTimeKind.Unspecified));
        // ConvertTimeToUtc applies the correct offset for that specific date (incl. DST).
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localNext, DateTimeKind.Unspecified), tz);
    }

    /// <summary>Current wall-clock time in the schedule's timezone (Kind=Unspecified).</summary>
    private DateTime LocalNow(CronSchedule schedule)
    {
        var tz = ResolveTimeZone(schedule.Timezone);
        return tz == TimeZoneInfo.Utc
            ? DateTime.UtcNow
            : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
    }

    /// <summary>
    /// Gets all upcoming executions within a time window.
    /// </summary>
    public List<ScheduledExecution> GetUpcomingExecutions(TimeSpan window)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var until = now.Add(window);
            var upcoming = new List<ScheduledExecution>();

            foreach (var (workflowId, (schedule, expression, _)) in _schedules)
            {
                if (!schedule.Enabled)
                    continue;

                var next = now;
                for (int i = 0; i < 100; i++) // Limit iterations
                {
                    try
                    {
                        // Returns the following occurrence in UTC, honoring the schedule's
                        // timezone; passing it back in advances to the next one.
                        next = NextOccurrenceUtc(schedule, expression, next);

                        if (next > until)
                            break;

                        if (schedule.EndBefore.HasValue && next >= schedule.EndBefore.Value)
                            break;

                        upcoming.Add(new ScheduledExecution
                        {
                            WorkflowId = workflowId,
                            ScheduledTime = next
                        });
                    }
                    catch
                    {
                        break;
                    }
                }
            }

            return upcoming.OrderBy(e => e.ScheduledTime).ToList();
        }
    }

    private void CheckSchedules()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            foreach (var (workflowId, (schedule, expression, executionCount)) in _schedules.ToList())
            {
                try
                {
                    if (!schedule.Enabled)
                        continue;

                    // Check max executions
                    if (schedule.MaxExecutions.HasValue && executionCount >= schedule.MaxExecutions.Value)
                    {
                        _logger?.LogInformation(
                            "Schedule for {WorkflowId} reached max executions ({Count})",
                            workflowId,
                            executionCount);
                        schedule.Enabled = false;
                        continue;
                    }

                    // Time-window checks use absolute UTC instants (StartAfter/EndBefore).
                    if (schedule.StartAfter.HasValue && now < schedule.StartAfter.Value)
                        continue;

                    if (schedule.EndBefore.HasValue && now >= schedule.EndBefore.Value)
                    {
                        schedule.Enabled = false;
                        continue;
                    }

                    // Cron fields are matched against wall-clock time in the schedule's
                    // timezone, not UTC - so "0 9 * * *" fires at 9am local (DST-aware).
                    if (expression.Matches(LocalNow(schedule)))
                    {
                        // Update execution count
                        _schedules[workflowId] = (schedule, expression, executionCount + 1);

                        _logger?.LogInformation(
                            "Triggering scheduled execution for workflow {WorkflowId}",
                            workflowId);

                        // Trigger async
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                if (OnScheduledExecution != null)
                                {
                                    await OnScheduledExecution.Invoke(workflowId, now);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "Error executing scheduled workflow {WorkflowId}", workflowId);
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error checking schedule for workflow {WorkflowId}", workflowId);
                }
            }
        }
    }

    /// <summary>
    /// Gets scheduler statistics.
    /// </summary>
    public CronSchedulerStats GetStats()
    {
        lock (_lock)
        {
            return new CronSchedulerStats
            {
                ActiveSchedules = _schedules.Count(s => s.Value.Item1.Enabled),
                TotalSchedules = _schedules.Count,
                TotalExecutions = _schedules.Sum(s => s.Value.ExecutionCount)
            };
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _timer?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Scheduler statistics.
/// </summary>
public class CronSchedulerStats
{
    public int ActiveSchedules { get; set; }
    public int TotalSchedules { get; set; }
    public int TotalExecutions { get; set; }
}

