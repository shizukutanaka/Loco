using System;
using System.IO;
using System.Text;

namespace Loco.Core.Workflows
{
    /// <summary>
    /// Simple utility to save workflow execution summary to a file.
    /// </summary>
    public static class WorkflowOutputLogger
    {
        /// <summary>
        /// Saves workflow execution summary to a file.
        /// </summary>
        public static void SaveExecutionSummary(
            string outputFile,
            string workflowName,
            string workflowId,
            bool success,
            TimeSpan duration,
            int totalSteps,
            string? errorMessage = null)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== Workflow Execution Summary ===");
                sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Workflow: {workflowName}");
                sb.AppendLine($"ID: {workflowId}");
                sb.AppendLine($"Steps: {totalSteps}");
                sb.AppendLine($"Status: {(success ? "SUCCESS" : "FAILED")}");
                sb.AppendLine($"Duration: {duration.TotalSeconds:F2}s");

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    sb.AppendLine($"Error: {errorMessage}");
                }

                sb.AppendLine("=== End of Summary ===");

                File.WriteAllText(outputFile, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                // Silently fail if we can't write the log
            }
        }
    }
}
