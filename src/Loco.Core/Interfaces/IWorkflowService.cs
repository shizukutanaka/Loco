using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Interfaces;

public interface IWorkflowService
{
    Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
        string workflowId,
        byte[] input,
        IDictionary<string, string> parameters,
        CancellationToken cancellationToken = default);
}

public class WorkflowExecutionResult
{
    public string ExecutionId { get; set; } = string.Empty;
    public byte[] Output { get; set; } = Array.Empty<byte>();
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public long DurationMs { get; set; }
}
