using System;
using Loco.Core.Triggers;
using Xunit;
using FluentAssertions;

namespace Loco.Core.Tests.Triggers;

/// <summary>
/// Tests that cron schedules are evaluated in their configured timezone, not UTC.
/// The scheduler previously computed everything from DateTime.UtcNow and ignored
/// CronSchedule.Timezone, so "0 9 * * *" fired at 09:00 UTC everywhere and DST
/// was never applied.
///
/// These assert on the UTC instant returned by GetNextExecution for a fixed
/// "09:00 local, daily" schedule, verified against a timezone with a known,
/// stable offset. Timezone-database ids differ between Windows and Linux, so the
/// zone is resolved defensively and the test no-ops if the runner lacks it.
///
/// NOTE: authored where dotnet test cannot run (NuGet egress blocked by
/// organization policy). They DO run - scripts/run-tests-offline.sh executes
/// them against the harness in scripts/offline-test-harness/.
/// </summary>
public class CronSchedulerTimezoneTests
{
    private static TimeZoneInfo? TryZone(params string[] ids)
    {
        foreach (var id in ids)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { }
        }
        return null;
    }

    [Fact]
    public void GetNextExecution_UtcSchedule_MatchesUtcWallClock()
    {
        using var scheduler = new CronScheduler();
        scheduler.AddSchedule("wf", new CronSchedule
        {
            Expression = "0 9 * * *", // 09:00 daily
            Timezone = "UTC",
        });

        var next = scheduler.GetNextExecution("wf");

        next.Should().NotBeNull();
        next!.Value.Hour.Should().Be(9, "a UTC schedule fires at 09:00 UTC");
    }

    [Fact]
    public void GetNextExecution_NonUtcSchedule_FiresAtLocalWallClock_NotUtc()
    {
        // Tokyo is UTC+9 year-round (no DST): 09:00 JST == 00:00 UTC.
        var tokyo = TryZone("Asia/Tokyo", "Tokyo Standard Time");
        if (tokyo is null) return; // runner lacks the zone db entry; skip

        using var scheduler = new CronScheduler();
        scheduler.AddSchedule("wf", new CronSchedule
        {
            Expression = "0 9 * * *", // 09:00 local (Tokyo)
            Timezone = tokyo.Id,
        });

        var next = scheduler.GetNextExecution("wf");

        next.Should().NotBeNull();
        // The returned instant is UTC; 09:00 Tokyo is 00:00 UTC.
        var asUtc = next!.Value.Kind == DateTimeKind.Utc ? next.Value : next.Value.ToUniversalTime();
        asUtc.Hour.Should().Be(0,
            "09:00 in UTC+9 Tokyo is 00:00 UTC - the old UTC-only logic would have returned 09:00 UTC");
    }

    [Fact]
    public void GetNextExecution_UnknownTimezone_FallsBackToUtc_DoesNotThrow()
    {
        using var scheduler = new CronScheduler();
        scheduler.AddSchedule("wf", new CronSchedule
        {
            Expression = "0 9 * * *",
            Timezone = "Not/ARealZone",
        });

        var act = () => scheduler.GetNextExecution("wf");

        act.Should().NotThrow();
        scheduler.GetNextExecution("wf")!.Value.Hour.Should().Be(9, "unknown zone falls back to UTC");
    }

    [Fact]
    public void GetNextExecution_DstZone_ProducesCorrectUtcOffsetAcrossTheYear()
    {
        // US Eastern observes DST: EST = UTC-5, EDT = UTC-4. A 09:00-local schedule
        // is 14:00 UTC in winter and 13:00 UTC in summer. Whichever side of a DST
        // boundary "next" lands on, the UTC hour must be one of those two - never a
        // fixed 09:00 UTC (which is what the timezone-blind code produced).
        var eastern = TryZone("America/New_York", "Eastern Standard Time");
        if (eastern is null) return;

        using var scheduler = new CronScheduler();
        scheduler.AddSchedule("wf", new CronSchedule
        {
            Expression = "0 9 * * *",
            Timezone = eastern.Id,
        });

        var next = scheduler.GetNextExecution("wf");
        next.Should().NotBeNull();

        var asUtc = next!.Value.Kind == DateTimeKind.Utc ? next.Value : next.Value.ToUniversalTime();
        // The collection overload, not the params one: BeOneOf(13, 14, "why")
        // binds to BeOneOf(params int[]) and the reason cannot convert to int.
        asUtc.Hour.Should().BeOneOf(new[] { 13, 14 },
            "09:00 US-Eastern is 13:00 UTC (EDT) or 14:00 UTC (EST), never 09:00 UTC");
    }
}
