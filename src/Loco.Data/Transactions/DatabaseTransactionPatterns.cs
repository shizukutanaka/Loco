#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Data.Transactions;

/// <summary>
/// Database Transaction Isolation & MVCC Patterns
/// ACID properties, isolation levels, multi-version concurrency control
/// </summary>

/// <summary>
/// Transaction isolation level
/// From loosest to strictest: READ_UNCOMMITTED < READ_COMMITTED < REPEATABLE_READ < SERIALIZABLE
/// </summary>
public enum IsolationLevel
{
    /// <summary>
    /// Dirty reads allowed (no locks)
    /// </summary>
    ReadUncommitted = 0,

    /// <summary>
    /// Prevents dirty reads (default for most databases)
    /// </summary>
    ReadCommitted = 1,

    /// <summary>
    /// Prevents dirty reads and non-repeatable reads
    /// </summary>
    RepeatableRead = 2,

    /// <summary>
    /// Strictest: Serializable snapshot isolation (SSI)
    /// </summary>
    Serializable = 3
}

/// <summary>
/// Transaction state
/// </summary>
public enum TransactionState
{
    Active,
    Committed,
    RolledBack,
    AbortedDueToConflict
}

/// <summary>
/// Database row version for MVCC
/// </summary>
public class RowVersion
{
    [JsonPropertyName("versionId")]
    public long VersionId { get; set; }

    [JsonPropertyName("transactionId")]
    public long TransactionId { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("deletedAt")]
    public DateTime? DeletedAt { get; set; }

    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// Transaction context
/// </summary>
public class TransactionContext
{
    [JsonPropertyName("transactionId")]
    public long TransactionId { get; set; }

    [JsonPropertyName("isolationLevel")]
    public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

    [JsonPropertyName("state")]
    public TransactionState State { get; set; } = TransactionState.Active;

    [JsonPropertyName("startTimestamp")]
    public DateTime StartTimestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("readSet")]
    public HashSet<long> ReadSet { get; set; } = new(); // Row IDs read

    [JsonPropertyName("writeSet")]
    public HashSet<long> WriteSet { get; set; } = new(); // Row IDs written

    [JsonPropertyName("locks")]
    public Dictionary<long, LockType> Locks { get; set; } = new(); // Row ID -> Lock type

    [JsonPropertyName("snapshot")]
    public TransactionSnapshot? Snapshot { get; set; }
}

/// <summary>
/// Lock type for row-level locking
/// </summary>
public enum LockType
{
    Shared, // Multiple transactions can read
    Exclusive // Only one transaction can write
}

/// <summary>
/// Transaction snapshot (MVCC)
/// Consistent view of data at transaction start
/// </summary>
public class TransactionSnapshot
{
    [JsonPropertyName("snapshotId")]
    public long SnapshotId { get; set; }

    [JsonPropertyName("activeTransactions")]
    public HashSet<long> ActiveTransactions { get; set; } = new();

    [JsonPropertyName("nextTransactionId")]
    public long NextTransactionId { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// MVCC (Multi-Version Concurrency Control) engine
/// Allows readers and writers to coexist without blocking
/// </summary>
public class MvccEngine
{
    private long _nextTransactionId = 1;
    private long _nextVersionId = 1;
    private readonly HashSet<long> _activeTransactions = new();
    private readonly Dictionary<long, RowVersion[]> _versions = new(); // Row ID -> versions
    private readonly Dictionary<long, TransactionContext> _transactions = new();
    private readonly ILogger<MvccEngine> _logger;

    public MvccEngine(ILogger<MvccEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Begin transaction
    /// </summary>
    public TransactionContext BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        var txnId = Interlocked.Increment(ref _nextTransactionId) - 1;
        var context = new TransactionContext
        {
            TransactionId = txnId,
            IsolationLevel = isolationLevel,
            Snapshot = new TransactionSnapshot
            {
                SnapshotId = txnId,
                ActiveTransactions = new(_activeTransactions),
                NextTransactionId = _nextTransactionId
            }
        };

        _activeTransactions.Add(txnId);
        _transactions[txnId] = context;

        _logger.LogInformation(
            "Started transaction {TransactionId} with isolation level {IsolationLevel}",
            txnId,
            isolationLevel);

        return context;
    }

    /// <summary>
    /// Read data from transaction
    /// </summary>
    public Dictionary<string, object>? Read(TransactionContext context, long rowId)
    {
        if (!_versions.TryGetValue(rowId, out var versions))
        {
            return null;
        }

        context.ReadSet.Add(rowId);

        // MVCC: Find visible version for this transaction
        var visibleVersion = GetVisibleVersion(context, versions);

        if (visibleVersion == null || !visibleVersion.IsVisible)
        {
            return null;
        }

        _logger.LogDebug(
            "Read row {RowId} version {VersionId} in transaction {TransactionId}",
            rowId,
            visibleVersion.VersionId,
            context.TransactionId);

        return visibleVersion.Data;
    }

    /// <summary>
    /// Write data in transaction
    /// </summary>
    public void Write(TransactionContext context, long rowId, Dictionary<string, object> data)
    {
        if (context.State != TransactionState.Active)
        {
            throw new InvalidOperationException("Transaction is not active");
        }

        // Acquire exclusive lock
        if (!AcquireLock(context, rowId, LockType.Exclusive))
        {
            throw new InvalidOperationException($"Cannot acquire exclusive lock on row {rowId}");
        }

        context.WriteSet.Add(rowId);

        // Create new version
        var versionId = Interlocked.Increment(ref _nextVersionId) - 1;
        var newVersion = new RowVersion
        {
            VersionId = versionId,
            TransactionId = context.TransactionId,
            Data = new(data)
        };

        if (!_versions.ContainsKey(rowId))
        {
            _versions[rowId] = new[] { newVersion };
        }
        else
        {
            var currentVersions = _versions[rowId].ToList();
            currentVersions.Add(newVersion);
            _versions[rowId] = currentVersions.ToArray();
        }

        _logger.LogDebug(
            "Wrote row {RowId} version {VersionId} in transaction {TransactionId}",
            rowId,
            versionId,
            context.TransactionId);
    }

    /// <summary>
    /// Commit transaction
    /// </summary>
    public bool Commit(TransactionContext context)
    {
        if (context.State != TransactionState.Active)
        {
            _logger.LogWarning(
                "Cannot commit transaction {TransactionId}: state is {State}",
                context.TransactionId,
                context.State);

            return false;
        }

        try
        {
            // Validate no conflicts for Serializable isolation level
            if (context.IsolationLevel == IsolationLevel.Serializable)
            {
                if (!ValidateSerializable(context))
                {
                    RollBack(context);
                    return false;
                }
            }

            // Release all locks
            ReleaseLocks(context);

            // Mark transaction as completed
            context.State = TransactionState.Committed;
            _activeTransactions.Remove(context.TransactionId);

            _logger.LogInformation(
                "Committed transaction {TransactionId}",
                context.TransactionId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit transaction {TransactionId}", context.TransactionId);
            RollBack(context);
            return false;
        }
    }

    /// <summary>
    /// Rollback transaction
    /// </summary>
    public void RollBack(TransactionContext context)
    {
        if (context.State == TransactionState.Committed)
        {
            _logger.LogWarning("Cannot rollback committed transaction {TransactionId}", context.TransactionId);
            return;
        }

        // Remove uncommitted versions created by this transaction
        foreach (var rowId in context.WriteSet)
        {
            if (_versions.TryGetValue(rowId, out var versions))
            {
                var filtered = versions
                    .Where(v => v.TransactionId != context.TransactionId)
                    .ToArray();

                if (filtered.Length > 0)
                {
                    _versions[rowId] = filtered;
                }
            }
        }

        // Release all locks
        ReleaseLocks(context);

        context.State = TransactionState.RolledBack;
        _activeTransactions.Remove(context.TransactionId);

        _logger.LogInformation(
            "Rolled back transaction {TransactionId}",
            context.TransactionId);
    }

    /// <summary>
    /// Get visible version for transaction (MVCC)
    /// </summary>
    private RowVersion? GetVisibleVersion(TransactionContext context, RowVersion[] versions)
    {
        // For MVCC: return version visible to this transaction
        // Implementation depends on isolation level

        return context.IsolationLevel switch
        {
            IsolationLevel.ReadUncommitted =>
                // Dirty reads: return latest version (including uncommitted)
                versions.LastOrDefault(v => v.IsVisible),

            IsolationLevel.ReadCommitted =>
                // Non-dirty reads: return latest committed version
                versions.LastOrDefault(v =>
                    v.IsVisible && !_activeTransactions.Contains(v.TransactionId)),

            IsolationLevel.RepeatableRead or IsolationLevel.Serializable =>
                // Snapshot isolation: return version as of transaction start
                versions.LastOrDefault(v =>
                    v.TransactionId < context.Snapshot!.NextTransactionId &&
                    v.IsVisible),

            _ => null
        };
    }

    /// <summary>
    /// Acquire row lock
    /// </summary>
    private bool AcquireLock(TransactionContext context, long rowId, LockType lockType)
    {
        // Simplified locking: in real implementation would handle deadlock detection
        if (!context.Locks.ContainsKey(rowId))
        {
            context.Locks[rowId] = lockType;
            return true;
        }

        // Upgrade shared lock to exclusive if needed
        if (context.Locks[rowId] == LockType.Shared && lockType == LockType.Exclusive)
        {
            context.Locks[rowId] = LockType.Exclusive;
            return true;
        }

        return true;
    }

    /// <summary>
    /// Release transaction locks
    /// </summary>
    private void ReleaseLocks(TransactionContext context)
    {
        context.Locks.Clear();
    }

    /// <summary>
    /// Validate serializable isolation level
    /// </summary>
    private bool ValidateSerializable(TransactionContext context)
    {
        // Check for conflicts with other active transactions
        // Simplified: in real implementation would use conflict detection algorithms

        foreach (var otherTxnId in _activeTransactions)
        {
            if (otherTxnId == context.TransactionId)
                continue;

            if (_transactions.TryGetValue(otherTxnId, out var otherContext))
            {
                // Check read-write conflicts
                var readWriteConflict = context.WriteSet.Overlaps(otherContext.ReadSet) ||
                                       context.ReadSet.Overlaps(otherContext.WriteSet);

                if (readWriteConflict)
                {
                    _logger.LogWarning(
                        "Serializable conflict detected between transactions {Txn1} and {Txn2}",
                        context.TransactionId,
                        otherTxnId);

                    context.State = TransactionState.AbortedDueToConflict;
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Get transaction statistics
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        return new()
        {
            ["activeTransactions"] = _activeTransactions.Count,
            ["totalVersions"] = _versions.Values.Sum(v => v.Length),
            ["rowCount"] = _versions.Count
        };
    }
}

/// <summary>
/// Deadlock detection (wait-for graph)
/// </summary>
public class DeadlockDetector
{
    private readonly Dictionary<long, HashSet<long>> _waitForGraph = new();
    private readonly ILogger<DeadlockDetector> _logger;

    public DeadlockDetector(ILogger<DeadlockDetector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check for deadlock
    /// </summary>
    public bool DetectDeadlock(long transactionId)
    {
        if (!_waitForGraph.ContainsKey(transactionId))
        {
            return false;
        }

        return HasCycle(transactionId, new HashSet<long>());
    }

    private bool HasCycle(long nodeId, HashSet<long> visited)
    {
        if (visited.Contains(nodeId))
        {
            return true; // Cycle detected
        }

        if (!_waitForGraph.TryGetValue(nodeId, out var neighbors))
        {
            return false;
        }

        visited.Add(nodeId);

        foreach (var neighbor in neighbors)
        {
            if (HasCycle(neighbor, visited))
            {
                return true;
            }
        }

        visited.Remove(nodeId);
        return false;
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class TransactionExtensions
{
    public static IServiceCollection AddDatabaseTransactions(this IServiceCollection services)
    {
        services.AddSingleton<MvccEngine>();
        services.AddSingleton<DeadlockDetector>();
        return services;
    }
}
