// Phase 3: SQL-based Workflow Execution Event Repository
// Production-ready event persistence with transaction support

using Microsoft.EntityFrameworkCore;
using Loco.Core.Workflows.DurableExecution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Loco.Core.DataAccess;

/// <summary>
/// Workflow Execution Event Repository interface
/// Abstraction for event storage with potential for different backends
/// </summary>
public interface IWorkflowExecutionEventRepository
{
    /// <summary>
    /// Append single event with transaction
    /// </summary>
    Task<WorkflowExecutionEventEntity> AppendEventAsync(
        WorkflowExecutionEventEntity @event,
        CancellationToken ct = default);

    /// <summary>
    /// Append multiple events in single transaction (atomic)
    /// </summary>
    Task<IEnumerable<WorkflowExecutionEventEntity>> AppendEventsAsync(
        IEnumerable<WorkflowExecutionEventEntity> events,
        CancellationToken ct = default);

    /// <summary>
    /// Get events by execution ID with optional filtering
    /// </summary>
    Task<IEnumerable<WorkflowExecutionEventEntity>> GetByExecutionIdAsync(
        string executionId,
        int? fromSequence = null,
        int? toSequence = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get events by workflow ID (all executions)
    /// </summary>
    Task<IEnumerable<WorkflowExecutionEventEntity>> GetByWorkflowIdAsync(
        string workflowId,
        int? limit = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get latest events across all workflows
    /// </summary>
    Task<IEnumerable<WorkflowExecutionEventEntity>> GetLatestEventsAsync(
        int limit = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Get events since timestamp
    /// </summary>
    Task<IEnumerable<WorkflowExecutionEventEntity>> GetEventsSinceAsync(
        DateTime since,
        CancellationToken ct = default);

    /// <summary>
    /// Get event by ID
    /// </summary>
    Task<WorkflowExecutionEventEntity?> GetByIdAsync(
        string eventId,
        CancellationToken ct = default);

    /// <summary>
    /// Count events for execution
    /// </summary>
    Task<int> CountByExecutionAsync(
        string executionId,
        CancellationToken ct = default);

    /// <summary>
    /// Create snapshot
    /// </summary>
    Task<WorkflowExecutionSnapshot> CreateSnapshotAsync(
        WorkflowExecutionSnapshot snapshot,
        CancellationToken ct = default);

    /// <summary>
    /// Get latest snapshot for execution
    /// </summary>
    Task<WorkflowExecutionSnapshot?> GetLatestSnapshotAsync(
        string executionId,
        CancellationToken ct = default);

    /// <summary>
    /// Delete events for execution (soft delete via archival)
    /// </summary>
    Task<bool> ArchiveExecutionAsync(
        string executionId,
        CancellationToken ct = default);

    /// <summary>
    /// Cleanup old snapshots
    /// </summary>
    Task<int> CleanupOldSnapshotsAsync(
        string executionId,
        int keepCount = 5,
        CancellationToken ct = default);
}

/// <summary>
/// EF Core implementation of Workflow Execution Event Repository
/// Production-ready with transaction support and optimization
/// </summary>
public class WorkflowExecutionEventRepository : IWorkflowExecutionEventRepository
{
    private readonly LocoDbContext _context;
    private readonly ILogger<WorkflowExecutionEventRepository> _logger;

    public WorkflowExecutionEventRepository(
        LocoDbContext context,
        ILogger<WorkflowExecutionEventRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<WorkflowExecutionEventEntity> AppendEventAsync(
        WorkflowExecutionEventEntity @event,
        CancellationToken ct = default)
    {
        try
        {
            @event.Id = string.IsNullOrEmpty(@event.Id) ? Guid.NewGuid().ToString() : @event.Id;
            @event.Timestamp = DateTime.UtcNow;

            // Get next sequence number
            @event.SequenceNumber = await GetNextSequenceNumberAsync(@event.ExecutionId, ct);

            _context.WorkflowExecutionEvents.Add(@event);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Event appended: {EventType} ({ExecutionId}, Seq: {SequenceNumber})",
                @event.EventType,
                @event.ExecutionId,
                @event.SequenceNumber);

            return @event;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error appending event: {ExecutionId}", @event.ExecutionId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error appending event: {ExecutionId}", @event.ExecutionId);
            throw;
        }
    }

    public async Task<IEnumerable<WorkflowExecutionEventEntity>> AppendEventsAsync(
        IEnumerable<WorkflowExecutionEventEntity> events,
        CancellationToken ct = default)
    {
        var eventList = events.ToList();
        if (!eventList.Any())
            return Enumerable.Empty<WorkflowExecutionEventEntity>();

        try
        {
            // Use transaction for atomic batch insert
            using var transaction = await _context.Database.BeginTransactionAsync(ct);

            try
            {
                var executionId = eventList.First().ExecutionId;
                var startSequence = await GetNextSequenceNumberAsync(executionId, ct);

                for (int i = 0; i < eventList.Count; i++)
                {
                    var @event = eventList[i];
                    @event.Id = string.IsNullOrEmpty(@event.Id) ? Guid.NewGuid().ToString() : @event.Id;
                    @event.Timestamp = DateTime.UtcNow;
                    @event.SequenceNumber = startSequence + i;
                    @event.IsCommitted = true;
                }

                _context.WorkflowExecutionEvents.AddRange(eventList);
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation(
                    "Batch appended: {Count} events ({ExecutionId})",
                    eventList.Count,
                    executionId);

                return eventList;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Error during batch append, transaction rolled back");
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error appending batch events");
            throw;
        }
    }

    public async Task<IEnumerable<WorkflowExecutionEventEntity>> GetByExecutionIdAsync(
        string executionId,
        int? fromSequence = null,
        int? toSequence = null,
        CancellationToken ct = default)
    {
        try
        {
            var query = _context.WorkflowExecutionEvents
                .AsNoTracking()
                .Where(e => e.ExecutionId == executionId && e.IsCommitted);

            if (fromSequence.HasValue)
                query = query.Where(e => e.SequenceNumber >= fromSequence.Value);

            if (toSequence.HasValue)
                query = query.Where(e => e.SequenceNumber <= toSequence.Value);

            return await query
                .OrderBy(e => e.SequenceNumber)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting events for execution: {ExecutionId}", executionId);
            throw;
        }
    }

    public async Task<IEnumerable<WorkflowExecutionEventEntity>> GetByWorkflowIdAsync(
        string workflowId,
        int? limit = null,
        CancellationToken ct = default)
    {
        try
        {
            var query = _context.WorkflowExecutionEvents
                .AsNoTracking()
                .Where(e => e.WorkflowId == workflowId && e.IsCommitted)
                .OrderByDescending(e => e.Timestamp);

            if (limit.HasValue)
                query = query.Take(limit.Value);

            return await query.ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting events for workflow: {WorkflowId}", workflowId);
            throw;
        }
    }

    public async Task<IEnumerable<WorkflowExecutionEventEntity>> GetLatestEventsAsync(
        int limit = 100,
        CancellationToken ct = default)
    {
        try
        {
            return await _context.WorkflowExecutionEvents
                .AsNoTracking()
                .Where(e => e.IsCommitted)
                .OrderByDescending(e => e.Timestamp)
                .Take(Math.Max(1, Math.Min(limit, 1000))) // Limit to 1000
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest events");
            throw;
        }
    }

    public async Task<IEnumerable<WorkflowExecutionEventEntity>> GetEventsSinceAsync(
        DateTime since,
        CancellationToken ct = default)
    {
        try
        {
            return await _context.WorkflowExecutionEvents
                .AsNoTracking()
                .Where(e => e.Timestamp >= since && e.IsCommitted)
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting events since: {Since}", since);
            throw;
        }
    }

    public async Task<WorkflowExecutionEventEntity?> GetByIdAsync(
        string eventId,
        CancellationToken ct = default)
    {
        try
        {
            return await _context.WorkflowExecutionEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == eventId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting event by ID: {EventId}", eventId);
            throw;
        }
    }

    public async Task<int> CountByExecutionAsync(
        string executionId,
        CancellationToken ct = default)
    {
        try
        {
            return await _context.WorkflowExecutionEvents
                .Where(e => e.ExecutionId == executionId && e.IsCommitted)
                .CountAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting events for execution: {ExecutionId}", executionId);
            throw;
        }
    }

    public async Task<WorkflowExecutionSnapshot> CreateSnapshotAsync(
        WorkflowExecutionSnapshot snapshot,
        CancellationToken ct = default)
    {
        try
        {
            snapshot.Id = string.IsNullOrEmpty(snapshot.Id) ? Guid.NewGuid().ToString() : snapshot.Id;
            snapshot.CreatedAt = DateTime.UtcNow;

            _context.WorkflowExecutionSnapshots.Add(snapshot);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Snapshot created: {ExecutionId}, Seq: {SequenceNumber}",
                snapshot.ExecutionId,
                snapshot.SequenceNumber);

            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating snapshot: {ExecutionId}", snapshot.ExecutionId);
            throw;
        }
    }

    public async Task<WorkflowExecutionSnapshot?> GetLatestSnapshotAsync(
        string executionId,
        CancellationToken ct = default)
    {
        try
        {
            return await _context.WorkflowExecutionSnapshots
                .AsNoTracking()
                .Where(s => s.ExecutionId == executionId)
                .OrderByDescending(s => s.SequenceNumber)
                .FirstOrDefaultAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest snapshot: {ExecutionId}", executionId);
            throw;
        }
    }

    public async Task<bool> ArchiveExecutionAsync(
        string executionId,
        CancellationToken ct = default)
    {
        try
        {
            // Mark events as archived (keep in DB but separate from active queries)
            var events = await _context.WorkflowExecutionEvents
                .Where(e => e.ExecutionId == executionId)
                .ToListAsync(ct);

            if (!events.Any())
                return false;

            foreach (var @event in events)
            {
                @event.IsCommitted = false; // Mark as archived
            }

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Archived {Count} events for execution: {ExecutionId}",
                events.Count,
                executionId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving execution: {ExecutionId}", executionId);
            throw;
        }
    }

    public async Task<int> CleanupOldSnapshotsAsync(
        string executionId,
        int keepCount = 5,
        CancellationToken ct = default)
    {
        try
        {
            var snapshots = await _context.WorkflowExecutionSnapshots
                .Where(s => s.ExecutionId == executionId)
                .OrderByDescending(s => s.SequenceNumber)
                .ToListAsync(ct);

            if (snapshots.Count <= keepCount)
                return 0;

            var toDelete = snapshots.Skip(keepCount).ToList();
            _context.WorkflowExecutionSnapshots.RemoveRange(toDelete);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Cleaned up {Count} old snapshots for execution: {ExecutionId}",
                toDelete.Count,
                executionId);

            return toDelete.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up snapshots: {ExecutionId}", executionId);
            throw;
        }
    }

    /// <summary>
    /// Get next sequence number for execution
    /// </summary>
    private async Task<long> GetNextSequenceNumberAsync(string executionId, CancellationToken ct)
    {
        var lastEvent = await _context.WorkflowExecutionEvents
            .AsNoTracking()
            .Where(e => e.ExecutionId == executionId)
            .OrderByDescending(e => e.SequenceNumber)
            .FirstOrDefaultAsync(ct);

        return (lastEvent?.SequenceNumber ?? -1) + 1;
    }
}
