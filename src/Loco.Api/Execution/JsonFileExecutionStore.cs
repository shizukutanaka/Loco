using System.Text.Json;
using Loco.Core.Workflows;

namespace Loco.Api.Execution;

/// <summary>
/// A finished execution, in the shape the response factory needs to render it.
///
/// Only the serializable parts of <see cref="ExecutionRegistry.Entry"/>: the
/// CancellationTokenSource and the completion Task are runtime handles with no
/// meaning after the process that owned them exits.
/// </summary>
public sealed record PersistedExecution(
    string ExecutionId,
    string WorkflowId,
    DateTime StartedAt,
    WorkflowExecutionContext Context);

/// <summary>
/// Keeps finished executions across restarts.
///
/// ExecutionRegistry holds executions in memory and evicts the oldest past 500,
/// so history was lost both on eviction and on every API restart - a client
/// polling GET /executions/{id} across a deploy got a 404 for a run that had
/// actually succeeded.
///
/// Only COMPLETED executions are written. A run still in flight is meaningless
/// to persist: the process that owns it is the only thing that can advance or
/// cancel it, and on restart it is gone regardless.
///
/// Durability follows JsonFileWorkflowStore: one file per execution, written to
/// .tmp and moved into place, so a crash mid-write cannot leave a torn record.
/// </summary>
public sealed class JsonFileExecutionStore
{
    private readonly string _directory;
    private readonly ILogger<JsonFileExecutionStore> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public JsonFileExecutionStore(string dataDirectory, ILogger<JsonFileExecutionStore> logger)
    {
        _directory = Path.Combine(dataDirectory, "executions");
        Directory.CreateDirectory(_directory);
        _logger = logger;
    }

    /// <summary>
    /// Execution ids become filenames, so anything that could escape the
    /// directory is rejected outright rather than sanitized.
    /// </summary>
    public static bool IsValidId(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        id.Length <= 128 &&
        id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');

    public async Task SaveAsync(PersistedExecution execution, CancellationToken ct = default)
    {
        if (!IsValidId(execution.ExecutionId)) return;

        await _semaphore.WaitAsync(ct);
        try
        {
            var path = Path.Combine(_directory, execution.ExecutionId + ".json");
            var tmp = path + ".tmp";

            var json = JsonSerializer.Serialize(execution, JsonOptions);
            await File.WriteAllTextAsync(tmp, json, ct);
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            // History is useful, not load-bearing: a workflow that ran correctly
            // must not be reported as failed because its record could not be
            // written.
            _logger.LogWarning(ex,
                "Could not persist execution {ExecutionId}", execution.ExecutionId);
        }
        finally { _semaphore.Release(); }
    }

    public async Task<PersistedExecution?> GetAsync(string executionId, CancellationToken ct = default)
    {
        if (!IsValidId(executionId)) return null;

        var path = Path.Combine(_directory, executionId + ".json");
        if (!File.Exists(path)) return null;

        await _semaphore.WaitAsync(ct);
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<PersistedExecution>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            // A corrupt record reads as "no history", which is what the caller
            // would have seen anyway before this store existed.
            _logger.LogWarning(ex, "Could not read execution {ExecutionId}", executionId);
            return null;
        }
        finally { _semaphore.Release(); }
    }
}
