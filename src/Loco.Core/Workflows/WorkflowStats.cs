using System.Text;

namespace Loco.Core.Workflows;

/// <summary>
/// Tracks and displays workflow execution statistics.
/// </summary>
public class WorkflowExecutionStats
{
    public string WorkflowId { get; set; } = "";
    public string WorkflowName { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration => (EndTime ?? DateTime.Now) - StartTime;

    public int TotalSteps { get; set; }
    public int CompletedSteps { get; set; }
    public int FailedSteps { get; set; }
    public int SkippedSteps { get; set; }
    public int RetryCount { get; set; }

    public Dictionary<string, StepExecutionInfo> StepDetails { get; set; } = new();

    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Generates formatted statistics reports.
/// </summary>
public static class WorkflowStatsFormatter
{
    /// <summary>
    /// Generates a detailed statistics report.
    /// </summary>
    public static string GenerateDetailedReport(WorkflowExecutionStats stats)
    {
        var sb = new StringBuilder();

        sb.AppendLine("╔════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║              WORKFLOW EXECUTION STATISTICS                         ║");
        sb.AppendLine("╠════════════════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║  Workflow: {TruncateText(stats.WorkflowName, 54).PadRight(54)} ║");
        sb.AppendLine($"║  ID: {stats.WorkflowId.PadRight(62)} ║");
        sb.AppendLine("╠════════════════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║  Start:    {stats.StartTime:yyyy-MM-dd HH:mm:ss}                          ║");

        if (stats.EndTime.HasValue)
        {
            sb.AppendLine($"║  End:      {stats.EndTime.Value:yyyy-MM-dd HH:mm:ss}                          ║");
        }

        sb.AppendLine($"║  Duration: {FormatDuration(stats.Duration).PadRight(56)} ║");
        sb.AppendLine("╠════════════════════════════════════════════════════════════════════╣");

        var statusIcon = stats.IsSuccess ? "✓" : "✗";
        var statusText = stats.IsSuccess ? "SUCCESS" : "FAILED";
        sb.AppendLine($"║  Status:   {statusText} {statusIcon}                                            ║");
        sb.AppendLine("╠════════════════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║  Total Steps:     {stats.TotalSteps.ToString().PadRight(49)} ║");
        sb.AppendLine($"║  Completed:       {stats.CompletedSteps.ToString().PadRight(49)} ║");
        sb.AppendLine($"║  Failed:          {stats.FailedSteps.ToString().PadRight(49)} ║");
        sb.AppendLine($"║  Skipped:         {stats.SkippedSteps.ToString().PadRight(49)} ║");

        if (stats.RetryCount > 0)
        {
            sb.AppendLine($"║  Total Retries:   {stats.RetryCount.ToString().PadRight(49)} ║");
        }

        sb.AppendLine("╚════════════════════════════════════════════════════════════════════╝");

        if (stats.StepDetails.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Step Details:");
            sb.AppendLine();

            foreach (var step in stats.StepDetails.Values)
            {
                var icon = step.Skipped ? "⊘" : (step.Success ? "✓" : "✗");
                var status = step.Skipped ? "SKIP" : (step.Success ? "OK" : "FAIL");

                sb.AppendLine($"  {icon} [{status}] {step.StepName} - {FormatDuration(step.Duration)}");

                if (!string.IsNullOrEmpty(step.ErrorMessage))
                {
                    sb.AppendLine($"      Error: {step.ErrorMessage}");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a compact summary.
    /// </summary>
    public static string GenerateCompactSummary(WorkflowExecutionStats stats)
    {
        var statusIcon = stats.IsSuccess ? "✓" : "✗";
        var duration = FormatDuration(stats.Duration);

        return $"{statusIcon} {stats.WorkflowName} - {stats.CompletedSteps}/{stats.TotalSteps} steps completed in {duration}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{duration.TotalHours:F1}h";
        }
        else if (duration.TotalMinutes >= 1)
        {
            return $"{duration.TotalMinutes:F1}m";
        }
        else
        {
            return $"{duration.TotalSeconds:F1}s";
        }
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength - 3) + "...";
    }
}
