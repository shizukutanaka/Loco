using FluentAssertions;
using Loco.Api.Execution;

namespace Loco.Api.Tests;

/// <summary>
/// Tests for the log half of ExecutionResponseFactory.
///
/// The engine records the time and the severity of every line it writes, but the
/// response used to throw both away: each entry went out stamped with the
/// execution's start time and the level "info". Nothing failed - the UI simply
/// showed a column of identical clocks and a level filter that could never
/// separate a failure from a status line. That is the kind of defect no
/// compiler and no end-to-end test notices, so it is pinned here.
///
/// NOTE: authored in an environment where dotnet test could not run (NuGet
/// egress blocked by organization policy); the first CI run executes these.
/// </summary>
public class ExecutionResponseFactoryTests
{
    private static readonly DateTime Started =
        new(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);

    private static List<ExecutionResponseFactory.LogEntry> Parse(params string[] lines) =>
        ExecutionResponseFactory.ParseLogs(Started, lines);

    [Fact]
    public void Reads_the_time_the_engine_wrote_not_the_execution_start()
    {
        var entry = Parse("[12:00:07] Executing node: Call API (action)").Single();

        DateTime.Parse(entry.Timestamp).ToUniversalTime()
            .Should().Be(new DateTime(2026, 8, 18, 12, 0, 7, DateTimeKind.Utc));
    }

    [Fact]
    public void Strips_the_prefix_so_the_time_is_not_shown_twice()
    {
        var entry = Parse("[12:00:07] Workflow completed successfully").Single();

        entry.Message.Should().Be("Workflow completed successfully");
    }

    [Theory]
    [InlineData("Workflow failed: connector returned 500", "error")]
    [InlineData("Node failed: Call API - timed out", "error")]
    [InlineData("Workflow cancelled", "warn")]
    [InlineData("Skipping disabled node: Notify", "warn")]
    [InlineData("Retrying node Call API (attempt 2/3) after 2s", "warn")]
    [InlineData("Starting workflow: Daily report (wf-1)", "info")]
    [InlineData("Workflow completed successfully", "info")]
    [InlineData("Node succeeded: Call API", "info")]
    public void Infers_the_level_from_the_engines_own_wording(string message, string expected)
    {
        Parse($"[12:00:07] {message}").Single().Level.Should().Be(expected);
    }

    [Fact]
    public void Classifies_a_message_the_engine_does_not_write_yet()
    {
        // The point of the loose fallback: a line added later should not read as
        // a routine status update just because its wording is new.
        Parse("[12:00:07] Credential refresh error for connection c-1")
            .Single().Level.Should().Be("error");
    }

    [Fact]
    public void Keeps_a_line_that_has_no_prefix_intact()
    {
        // ExecutionLog is a plain List<string>; anything appended without the
        // engine's prefix must survive whole rather than lose its first word.
        var entry = Parse("[not a time] something happened").Single();

        entry.Message.Should().Be("[not a time] something happened");
    }

    [Fact]
    public void Dates_an_unprefixed_line_from_the_line_before_it()
    {
        var entries = Parse(
            "[12:00:07] Executing node: Call API (action)",
            "raw continuation line");

        entries[1].Timestamp.Should().Be(entries[0].Timestamp);
    }

    [Fact]
    public void Dates_the_first_line_from_the_execution_even_without_a_prefix()
    {
        Parse("raw line").Single().Timestamp
            .Should().Be(Started.AddSeconds(-1).ToString("O"));
    }

    [Fact]
    public void Rolls_over_midnight_instead_of_going_backwards()
    {
        var entries = ExecutionResponseFactory.ParseLogs(
            new DateTime(2026, 8, 18, 23, 59, 58, DateTimeKind.Utc),
            new[]
            {
                "[23:59:59] Starting workflow: Nightly (wf-1)",
                "[00:00:04] Workflow completed successfully",
            });

        DateTime.Parse(entries[0].Timestamp).ToUniversalTime()
            .Should().Be(new DateTime(2026, 8, 18, 23, 59, 59, DateTimeKind.Utc));
        DateTime.Parse(entries[1].Timestamp).ToUniversalTime()
            .Should().Be(new DateTime(2026, 8, 19, 0, 0, 4, DateTimeKind.Utc));
    }

    [Fact]
    public void Does_not_mistake_second_truncation_for_a_midnight_rollover()
    {
        // The engine truncates to whole seconds, so its first stamp can read a
        // fraction earlier than a start time carrying milliseconds. Treating
        // that as a rollover would push the whole log a day into the future.
        var entries = ExecutionResponseFactory.ParseLogs(
            new DateTime(2026, 8, 18, 12, 0, 0, 900, DateTimeKind.Utc),
            new[] { "[12:00:00] Starting workflow: Daily report (wf-1)" });

        DateTime.Parse(entries[0].Timestamp).ToUniversalTime()
            .Should().Be(new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Keeps_the_entries_in_order()
    {
        var entries = Parse(
            "[12:00:01] Starting workflow: Daily report (wf-1)",
            "[12:00:03] Executing node: Call API (action)",
            "[12:00:09] Workflow failed: connector returned 500");

        entries.Select(e => e.Level).Should().Equal("info", "info", "error");
        entries.Select(e => DateTime.Parse(e.Timestamp)).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Emits_nothing_for_an_empty_log()
    {
        Parse().Should().BeEmpty();
    }
}
