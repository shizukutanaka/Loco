using System.Collections.Concurrent;
using Loco.Core.Workflows;

namespace Loco.Api.Execution;

/// <summary>
/// In-process registry of workflow executions, backing
/// GET /api/v1/executions/{id} and POST /api/v1/executions/{id}/cancel -
/// two endpoints the frontend has always called but that never existed
/// server-side (the execution-polling hook got a 404 on every tick).
///
/// Executions are intentionally in-memory only for now: the engine's execution
/// context is runtime state, and the store's durability story (see
/// docs) is limited to workflow definitions. A process restart forgets
/// execution history - documented as a known limitation in the README.
/// </summary>
public sealed class ExecutionRegistry
{
    public sealed record Entry(
        string ExecutionId,
        string WorkflowId,
        DateTime StartedAt,
        WorkflowExecutionContext Context,
        CancellationTokenSource Cancellation,
        Task Completion);

    private readonly ConcurrentDictionary<string, Entry> _executions = new();
    private readonly JsonFileExecutionStore? _store;

    /// <summary>
    /// <paramref name="store"/> keeps finished executions across restarts and
    /// past eviction. Optional so tests can construct a registry without a
    /// filesystem.
    /// </summary>
    public ExecutionRegistry(JsonFileExecutionStore? store = null) => _store = store;

    // Bound memory: keep at most this many finished executions, evicting oldest.
    private const int MaxRetained = 500;

    public void Register(Entry entry)
    {
        _executions[entry.ExecutionId] = entry;
        EvictIfNeeded();

        if (_store is null) return;

        // Persist once the run finishes. Continuation rather than await: Register
        // is called on the request path and must not block on the execution.
        _ = entry.Completion.ContinueWith(
            _ => _store.SaveAsync(new PersistedExecution(
                entry.ExecutionId, entry.WorkflowId, entry.StartedAt, entry.Context)),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public Entry? Get(string executionId) =>
        _executions.TryGetValue(executionId, out var entry) ? entry : null;

    /// <summary>Requests cancellation. Returns false when the id is unknown.</summary>
    public bool Cancel(string executionId)
    {
        if (!_executions.TryGetValue(executionId, out var entry))
        {
            return false;
        }

        try
        {
            entry.Cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already finished and cleaned up; treat as success (idempotent cancel).
        }

        return true;
    }

    private void EvictIfNeeded()
    {
        if (_executions.Count <= MaxRetained)
        {
            return;
        }

        // Evict the oldest finished executions first; never evict running ones.
        var evictable = _executions.Values
            .Where(e => e.Completion.IsCompleted)
            .OrderBy(e => e.StartedAt)
            .Take(_executions.Count - MaxRetained)
            .ToList();

        foreach (var entry in evictable)
        {
            _executions.TryRemove(entry.ExecutionId, out _);
        }
    }
}
