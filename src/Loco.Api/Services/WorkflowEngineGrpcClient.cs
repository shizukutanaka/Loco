using Loco.Workflow;
using Grpc.Net.Client;
using Google.Protobuf;
using System.Runtime.CompilerServices;

namespace Loco.Api.Services;

/// <summary>
/// gRPC client for communicating with WorkflowEngine service
/// Used by distributed systems and inter-service communication
/// </summary>
public class WorkflowEngineGrpcClient
{
    private readonly WorkflowEngine.WorkflowEngineClient _client;
    private readonly ILogger<WorkflowEngineGrpcClient> _logger;

    public WorkflowEngineGrpcClient(string address, ILogger<WorkflowEngineGrpcClient> logger)
    {
        _logger = logger;
        var channel = GrpcChannel.ForAddress(address);
        _client = new WorkflowEngine.WorkflowEngineClient(channel);
    }

    /// <summary>
    /// Execute a workflow asynchronously via gRPC
    /// </summary>
    public async Task<string> ExecuteAsync(
        string workflowId,
        byte[] input,
        Dictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ExecuteRequest
            {
                WorkflowId = workflowId,
                Input = ByteString.CopyFrom(input)
            };

            // Add variables if provided
            if (variables != null)
            {
                foreach (var kvp in variables)
                {
                    request.Variables[kvp.Key] = kvp.Value;
                }
            }

            _logger.LogInformation("gRPC Client: Executing workflow {WorkflowId}", workflowId);

            var response = await _client.ExecuteWorkflowAsync(request, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "gRPC Client: Workflow {WorkflowId} execution completed with status {Status}",
                workflowId, response.Status);

            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                _logger.LogError(
                    "gRPC Client: Workflow {WorkflowId} error: {Error}",
                    workflowId, response.ErrorMessage);
            }

            return response.ExecutionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC Client: Failed to execute workflow {WorkflowId}", workflowId);
            throw;
        }
    }

    /// <summary>
    /// Get execution status via gRPC
    /// </summary>
    public async Task<ExecutionStatusDto> GetStatusAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new StatusRequest { ExecutionId = executionId };
            var response = await _client.GetExecutionStatusAsync(request, cancellationToken: cancellationToken);

            return new ExecutionStatusDto
            {
                ExecutionId = response.ExecutionId,
                Status = response.Status,
                Progress = response.Progress,
                CurrentStep = response.CurrentStep,
                StartedAt = DateTime.FromFileTimeUtc(response.StartedAt),
                UpdatedAt = DateTime.FromFileTimeUtc(response.UpdatedAt)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC Client: Failed to get status for execution {ExecutionId}", executionId);
            throw;
        }
    }

    /// <summary>
    /// Stream execution logs via gRPC server-streaming
    /// This provides real-time visibility into workflow execution
    /// </summary>
    public async IAsyncEnumerable<LogEntryDto> StreamLogsAsync(
        string executionId,
        int skip = 0,
        int limit = 0,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new LogRequest
            {
                ExecutionId = executionId,
                Skip = skip,
                Limit = limit
            };

            _logger.LogInformation(
                "gRPC Client: Streaming logs for execution {ExecutionId}",
                executionId);

            using var call = _client.StreamExecutionLogs(request, cancellationToken: cancellationToken);
            var responseStream = call.ResponseStream;

            var logCount = 0;
            await foreach (var logEntry in responseStream.ReadAllAsync(cancellationToken))
            {
                logCount++;

                var dto = new LogEntryDto
                {
                    Timestamp = DateTime.Parse(logEntry.Timestamp),
                    Level = logEntry.Level,
                    Message = logEntry.Message,
                    Context = logEntry.Context.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                };

                yield return dto;

                if (logCount % 100 == 0)
                {
                    _logger.LogDebug(
                        "gRPC Client: Streamed {LogCount} log entries for execution {ExecutionId}",
                        logCount, executionId);
                }
            }

            _logger.LogInformation(
                "gRPC Client: Completed streaming {LogCount} log entries for execution {ExecutionId}",
                logCount, executionId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("gRPC Client: Log streaming cancelled for execution {ExecutionId}", executionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC Client: Failed to stream logs for execution {ExecutionId}", executionId);
            throw;
        }
    }
}

/// <summary>
/// Data transfer object for execution status
/// </summary>
public class ExecutionStatusDto
{
    public string ExecutionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public float Progress { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Data transfer object for log entry
/// </summary>
public class LogEntryDto
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string> Context { get; set; } = new();
}
