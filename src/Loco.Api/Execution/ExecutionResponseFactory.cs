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
        var logs = context.ExecutionLog
            .Select(line => new { timestamp = startedAt, level = "info", message = line })
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
