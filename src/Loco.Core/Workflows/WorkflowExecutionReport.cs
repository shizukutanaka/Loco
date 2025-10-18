using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Loco.Core.Workflows
{
    /// <summary>
    /// Represents a detailed execution report for a workflow.
    /// </summary>
    public class WorkflowExecutionReport
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string WorkflowName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public bool Success { get; set; }
        public int TotalSteps { get; set; }
        public int ExecutedSteps { get; set; }
        public int SkippedSteps { get; set; }
        public int FailedSteps { get; set; }
        public List<StepExecutionInfo> StepResults { get; set; } = new();

        /// <summary>
        /// Generates a formatted text report.
        /// </summary>
        public string GenerateTextReport()
        {
            var sb = new StringBuilder();

            sb.AppendLine("╔════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║              WORKFLOW EXECUTION REPORT                             ║");
            sb.AppendLine("╠════════════════════════════════════════════════════════════════════╣");
            sb.AppendLine($"║ Workflow: {WorkflowName,-56} ║");
            sb.AppendLine($"║ ID: {WorkflowId,-62} ║");
            sb.AppendLine("╠════════════════════════════════════════════════════════════════════╣");
            sb.AppendLine($"║ Start Time:  {StartTime:yyyy-MM-dd HH:mm:ss}                                ║");
            sb.AppendLine($"║ End Time:    {EndTime:yyyy-MM-dd HH:mm:ss}                                ║");
            sb.AppendLine($"║ Duration:    {Duration.TotalSeconds:F2}s{new string(' ', 52 - Duration.TotalSeconds.ToString("F2").Length)} ║");
            sb.AppendLine("╠════════════════════════════════════════════════════════════════════╣");
            sb.AppendLine($"║ Status:      {(Success ? "SUCCESS ✓" : "FAILED ✗"),-56} ║");
            sb.AppendLine($"║ Total Steps: {TotalSteps,-56} ║");
            sb.AppendLine($"║ Executed:    {ExecutedSteps,-56} ║");
            sb.AppendLine($"║ Skipped:     {SkippedSteps,-56} ║");
            sb.AppendLine($"║ Failed:      {FailedSteps,-56} ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════════╝");

            if (StepResults.Any())
            {
                sb.AppendLine();
                sb.AppendLine("Step Details:");
                sb.AppendLine("─────────────────────────────────────────────────────────────────────");

                foreach (var step in StepResults)
                {
                    var statusIcon = step.Skipped ? "⊘" : (step.Success ? "✓" : "✗");
                    var statusText = step.Skipped ? "SKIPPED" : (step.Success ? "SUCCESS" : "FAILED");

                    sb.AppendLine($"{statusIcon} {step.StepName}");
                    sb.AppendLine($"   ID: {step.StepId}");
                    sb.AppendLine($"   Status: {statusText}");
                    sb.AppendLine($"   Duration: {step.Duration.TotalSeconds:F3}s");

                    if (!string.IsNullOrEmpty(step.ErrorMessage))
                    {
                        sb.AppendLine($"   Error: {step.ErrorMessage}");
                    }

                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates a compact summary.
        /// </summary>
        public string GenerateSummary()
        {
            var successRate = TotalSteps > 0 ? (ExecutedSteps * 100.0 / TotalSteps) : 0;
            return $"Workflow '{WorkflowName}' completed in {Duration.TotalSeconds:F2}s - " +
                   $"{ExecutedSteps}/{TotalSteps} steps executed ({successRate:F1}% success rate) - " +
                   $"Status: {(Success ? "SUCCESS" : "FAILED")}";
        }
    }

    /// <summary>
    /// Information about a single step execution.
    /// </summary>
    public class StepExecutionInfo
    {
        public string StepId { get; set; } = string.Empty;
        public string StepName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public bool Skipped { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
