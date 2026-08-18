using System.Globalization;
using Loco.Core.Workflows;

namespace Loco.Api.Execution;

/// <summary>
/// Translates the engine's <see cref="WorkflowExecutionContext"/> into the
/// frontend's <c>WorkflowExecutionResponse</c> discriminated union
/// (src/Loco.VisualEditor/src/api/types.ts):
///
///   status: pending|running        -> { executionId, status, startedAt, logs? }
///   status: completed              -> + { completedAt, output }
///   status: failed|cancelled       -> + { completedAt, error{nodeId,message} }
/// </summary>
public static class ExecutionResponseFactory
{
    public static object Create(ExecutionRegistry.Entry entry) =>
        Create(entry.ExecutionId, entry.StartedAt, entry.Context);

    /// <summary>
    /// Same rendering from a persisted record, so an execution served from
    /// history after a restart is indistinguishable from a live one.
    /// </summary>
    public static object Create(PersistedExecution execution) =>
        Create(execution.ExecutionId, execution.StartedAt, execution.Context);

    private static object Create(
        string executionId, DateTime startedAtUtc, WorkflowExecutionContext context)
    {
        var status = ToFrontendStatus(context.Status);
        var startedAt = startedAtUtc.ToString("O");
        var logs = ParseLogs(startedAtUtc, context.ExecutionLog)
            .Select(entry => new { timestamp = entry.Timestamp, level = entry.Level, message = entry.Message })
            .ToList();

        switch (status)
        {
            case "completed":
            {
                // Node results keyed by node id; values reduced to their Data payloads.
                var output = context.NodeResults.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.Data ?? (object)new { });

                return new
                {
                    executionId,
                    status,
                    startedAt,
                    completedAt = (context.EndTime ?? DateTime.UtcNow).ToString("O"),
                    output,
                    logs,
                };
            }

            case "failed":
            case "cancelled":
            {
                var failedNode = context.NodeResults.Values.FirstOrDefault(r => !r.Success);
                return new
                {
                    executionId,
                    status,
                    startedAt,
                    completedAt = (context.EndTime ?? DateTime.UtcNow).ToString("O"),
                    error = new
                    {
                        nodeId = failedNode?.NodeId ?? "",
                        message = context.Error ?? failedNode?.Error ?? "Execution failed",
                    },
                    logs,
                };
            }

            default:
                return new
                {
                    executionId,
                    status,
                    startedAt,
                    logs,
                };
        }
    }

    /// <summary>One rendered log line: an ISO timestamp, a level, and the bare text.</summary>
    internal readonly record struct LogEntry(string Timestamp, string Level, string Message);

    /// <summary>
    /// Recovers the real time and severity of each engine log line.
    ///
    /// The engine writes <c>"[HH:mm:ss] {message}"</c> (VisualWorkflowEngine.Log),
    /// so the information was always there - it was just left inside the text.
    /// Every entry was previously stamped with the execution's start time and the
    /// level "info", which made the log viewer's clock useless (all rows showed
    /// the same second) and its level filter worse than useless: a failure was
    /// indistinguishable from a status line.
    ///
    /// The prefix carries a time of day but no date, so the date comes from the
    /// execution: entries start on <paramref name="startedAtUtc"/>'s day and roll
    /// forward when the clock goes backwards, which is what a workflow running
    /// across midnight looks like. A line without the prefix (nothing writes one
    /// today, but the log is a plain list of strings anyone can append to) keeps
    /// the previous entry's time rather than inventing one.
    /// </summary>
    internal static List<LogEntry> ParseLogs(DateTime startedAtUtc, IEnumerable<string> lines)
    {
        var entries = new List<LogEntry>();

        // Seeded a second early so the first line is not mistaken for a rollover:
        // the engine truncates to whole seconds, so its stamp can read slightly
        // before a start time that carries milliseconds.
        var previous = startedAtUtc.AddSeconds(-1);

        foreach (var line in lines)
        {
            var text = line ?? string.Empty;
            var at = previous;

            if (TryReadTimePrefix(text, out var timeOfDay, out var message))
            {
                at = previous.Date + timeOfDay;
                if (at < previous)
                {
                    at = at.AddDays(1);
                }

                text = message;
            }

            previous = at;
            entries.Add(new LogEntry(
                DateTime.SpecifyKind(at, DateTimeKind.Utc).ToString("O"),
                InferLevel(text),
                text));
        }

        return entries;
    }

    /// <summary>
    /// Splits <c>"[HH:mm:ss] rest"</c>. Anything else leaves the line untouched -
    /// a message that merely starts with a bracket must not lose its first word.
    /// </summary>
    private static bool TryReadTimePrefix(string line, out TimeSpan timeOfDay, out string message)
    {
        timeOfDay = default;
        message = line;

        const int PrefixLength = 10; // "[HH:mm:ss]"
        if (line.Length < PrefixLength || line[0] != '[' || line[PrefixLength - 1] != ']')
        {
            return false;
        }

        if (!TimeSpan.TryParseExact(
                line.Substring(1, PrefixLength - 2),
                @"hh\:mm\:ss",
                CultureInfo.InvariantCulture,
                out timeOfDay))
        {
            return false;
        }

        message = line.Substring(PrefixLength).TrimStart();
        return true;
    }

    /// <summary>
    /// Maps a message to one of the frontend's levels
    /// (<c>ExecutionLog.level</c> in src/Loco.VisualEditor/src/api/types.ts).
    ///
    /// The engine's own wording is matched first so its lines are classified
    /// exactly; the looser checks after it are there so a message added later
    /// still lands somewhere sensible instead of silently reading as "info".
    /// </summary>
    private static string InferLevel(string message)
    {
        if (Starts(message, "Workflow failed") || Starts(message, "Node failed"))
        {
            return "error";
        }

        if (Starts(message, "Workflow cancelled") ||
            Starts(message, "Skipping disabled node") ||
            Starts(message, "Retrying node"))
        {
            return "warn";
        }

        if (Contains(message, "failed") || Contains(message, "error"))
        {
            return "error";
        }

        if (Contains(message, "cancelled") ||
            Contains(message, "canceled") ||
            Contains(message, "retry") ||
            Contains(message, "skipping"))
        {
            return "warn";
        }

        return "info";

        static bool Starts(string value, string prefix) =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        static bool Contains(string value, string needle) =>
            value.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    public static string ToFrontendStatus(WorkflowExecutionStatus status) => status switch
    {
        WorkflowExecutionStatus.Pending => "pending",
        WorkflowExecutionStatus.Running => "running",
        WorkflowExecutionStatus.Success => "completed",
        WorkflowExecutionStatus.Failed => "failed",
        WorkflowExecutionStatus.Cancelled => "cancelled",
        WorkflowExecutionStatus.Paused => "running",
        _ => "pending",
    };
}
