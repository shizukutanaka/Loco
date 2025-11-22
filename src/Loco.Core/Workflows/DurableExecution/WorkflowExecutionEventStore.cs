// Phase 3: Persistent Event Store for Durable Execution
// SQL-based event sourcing with complete audit trail

using Microsoft.EntityFrameworkCore;
using Loco.Core.DataAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Loco.Core.Workflows.DurableExecution;

/// <summary>
/// Workflow Execution Event base entity
/// Immutable event for event sourcing pattern
/// </summary>
public class WorkflowExecutionEventEntity
{
    /// <summary>
    /// Unique event identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Workflow execution ID (aggregate root)
    /// </summary>
    public string ExecutionId { get; set; } = string.Empty;

    /// <summary>
    /// Workflow ID being executed
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Event type identifier (ExecutionStarted, StepStarted, etc.)
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Event version for schema evolution
    /// </summary>
    public int EventVersion { get; set; } = 1;

    /// <summary>
    /// Event data (JSON serialized)
    /// </summary>
    public string EventData { get; set; } = string.Empty;

    /// <summary>
    /// Event timestamp (UTC)
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Sequential event number within execution
    /// </summary>
    public long SequenceNumber { get; set; }

    /// <summary>
    /// Correlation ID for distributed tracing
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// User who triggered the event
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Event is committed (immutable after this)
    /// </summary>
    public bool IsCommitted { get; set; } = true;
}

/// <summary>
/// Event store interface for persistence
/// </summary>
public interface IWorkflowExecutionEventStore
{
    /// <summary>
    /// Append a single event
    /// </summary>
    Task<WorkflowExecutionEventEntity> AppendEventAsync(
        string executionId,
        string workflowId,
        string eventType,
        object eventData,
        string? correlationId = null,
        string? userId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Append multiple events (batch)
    /// </summary>
    Task AppendEventsAsync(
        string executionId,
        string workflowId,
        IEnumerable<(string eventType, object eventData)> events,
        string? correlationId = null,
        string? userId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get all events for a specific execution
    /// </summary>
    Task<IEnumerable<WorkflowExecutionEventEntity>> GetEventsByExecutionAsync(
        string executionId,
        CancellationToken ct = default);

    /// <summary>
    /// Get events since a specific timestamp
    /// </summary>
    Task<IEnumerable<WorkflowExecutionEventEntity>> GetEventsSinceAsync(
        string workflowId,
        DateTime since,
        CancellationToken ct = default);

    /// <summary>
    /// Get events for a specific workflow (all executions)
    /// </summary>
    Task<IEnumerable<WorkflowExecutionEventEntity>> GetEventsByWorkflowAsync(
        string workflowId,
        CancellationToken ct = default);

    /// <summary>
    /// Get latest N events
    /// </summary>
    Task<IEnumerable<WorkflowExecutionEventEntity>> GetLatestEventsAsync(
        int limit = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Get event stream for replay
    /// </summary>
    Task<IEnumerable<WorkflowExecutionEventEntity>> GetEventStreamAsync(
        string executionId,
        int? fromSequence = null,
        CancellationToken ct = default);

    /// <summary>
    /// Snapshot creation for optimization
    /// </summary>
    Task<WorkflowExecutionSnapshot> CreateSnapshotAsync(
        string executionId,
        object state,
        long sequenceNumber,
        CancellationToken ct = default);

    /// <summary>
    /// Get snapshot for execution
    /// </summary>
    Task<WorkflowExecutionSnapshot?> GetSnapshotAsync(
        string executionId,
        CancellationToken ct = default);

    /// <summary>
    /// Count events for an execution
    /// </summary>
    Task<int> GetEventCountAsync(
        string executionId,
        CancellationToken ct = default);
}

/// <summary>
/// EF Core implementation of Event Store
/// </summary>
public class EFCoreWorkflowExecutionEventStore : IWorkflowExecutionEventStore
{
    private readonly LocoDbContext _context;
    private readonly ILogger<EFCoreWorkflowExecutionEventStore> _logger;

    public EFCoreWorkflowExecutionEventStore(
        LocoDbContext context,
        ILogger<EFCoreWorkflowExecutionEventStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<WorkflowExecutionEventEntity> AppendEventAsync(
        string executionId,
        string workflowId,
        string eventType,
        object eventData,
        string? correlationId = null,
        string? userId = null,
        CancellationToken ct = default)
    {
        try
        {
            var sequenceNumber = await GetNextSequenceNumberAsync(executionId, ct);

            var @event = new WorkflowExecutionEventEntity
            {
                Id = Guid.NewGuid().ToString(),
                ExecutionId = executionId,
                WorkflowId = workflowId,
                EventType = eventType,
                EventData = JsonSerializer.Serialize(eventData),
                Timestamp = DateTime.UtcNow,
                SequenceNumber = sequenceNumber,
                CorrelationId = correlationId,
                UserId = userId,
                IsCommitted = true,
                EventVersion = 1,
            };

            _context.WorkflowExecutionEvents.Add(@event);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Event appended: {EventType} ({ExecutionId}, Seq: {SequenceNumber})",
                eventType,
                executionId,
                sequenceNumber);

            return @event;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error appending event: {ExecutionId}", executionId);
            throw;
        }
    }

    public async Task AppendEventsAsync(
        string executionId,
        string workflowId,
        IEnumerable<(string eventType, object eventData)> events,
        string? correlationId = null,
        string? userId = null,
        CancellationToken ct = default)
    {
        try
        {
            var eventList = events.ToList();
            var startSequence = await GetNextSequenceNumberAsync(executionId, ct);

            var eventEntities = eventList
                .Select((evt, index) => new WorkflowExecutionEventEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    ExecutionId = executionId,
                    WorkflowId = workflowId,
                    EventType = evt.eventType,
                    EventData = JsonSerializer.Serialize(evt.eventData),
                    Timestamp = DateTime.UtcNow,
                    SequenceNumber = startSequence + index,
                    CorrelationId = correlationId,
                    UserId = userId,
                    IsCommitted = true,
                    EventVersion = 1,
                })
                .ToList();

            _context.WorkflowExecutionEvents.AddRange(eventEntities);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Batch appended: {Count} events ({ExecutionId})",
                eventList.Count,
                executionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error appending batch events: {ExecutionId}", executionId);
            throw;
        }
    }

    public async Task<IEnumerable<WorkflowExecutionEventEntity>> GetEventsByExecutionAsync(
        string executionId,
        CancellationToken ct = default)
    {
        try
        {
            return await _context.WorkflowExecutionEvents
                .AsNoTracking()
                .Where(e => e.ExecutionId == executionId && e.IsCommitted)
                .OrderBy(e => e.SequenceNumber)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting events for execution: {ExecutionId}", executionId);
            throw;
        }
    }

    public async Task<IEnumerable<WorkflowExecutionEventEntity>> GetEventsSinceAsync(
        string workflowId,
        DateTime since,
        CancellationToken ct = default)
    {
        try
        {
            return await _context.WorkflowExecutionEvents
                .AsNoTracking()
                .Where(e => e.WorkflowId == workflowId && e.Timestamp >= since && e.IsCommitted)
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting events since: {WorkflowId}, {Since}", workflowId, since);
            throw;
        }
    }

    public async Task<IEnumerable<WorkflowExecutionEventEntity>> GetEventsByWorkflowAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        try
        {
            return await _context.WorkflowExecutionEvents
                .AsNoTracking()
                .Where(e => e.WorkflowId == workflowId && e.IsCommitted)
                .OrderByDescending(e => e.Timestamp)
                .Take(1000) // Limit for performance
                .ToListAsync(ct);
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
                .Take(limit)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest events");
            throw;
        }
    }

    public async Task<IEnumerable<WorkflowExecutionEventEntity>> GetEventStreamAsync(
        string executionId,
        int? fromSequence = null,
        CancellationToken ct = default)
    {
        try
        {
            var query = _context.WorkflowExecutionEvents
                .AsNoTracking()
                .Where(e => e.ExecutionId == executionId && e.IsCommitted);

            if (fromSequence.HasValue)
            {
                query = query.Where(e => e.SequenceNumber >= fromSequence.Value);
            }

            return await query
                .OrderBy(e => e.SequenceNumber)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting event stream: {ExecutionId}", executionId);
            throw;
        }
    }

    public async Task<WorkflowExecutionSnapshot> CreateSnapshotAsync(
        string executionId,
        object state,
        long sequenceNumber,
        CancellationToken ct = default)
    {
        try
        {
            var snapshot = new WorkflowExecutionSnapshot
            {
                Id = Guid.NewGuid().ToString(),
                ExecutionId = executionId,
                State = JsonSerializer.Serialize(state),
                SequenceNumber = sequenceNumber,
                CreatedAt = DateTime.UtcNow,
            };

            _context.WorkflowExecutionSnapshots.Add(snapshot);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Snapshot created: {ExecutionId}, Seq: {SequenceNumber}",
                executionId,
                sequenceNumber);

            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating snapshot: {ExecutionId}", executionId);
            throw;
        }
    }

    public async Task<WorkflowExecutionSnapshot?> GetSnapshotAsync(
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
            _logger.LogError(ex, "Error getting snapshot: {ExecutionId}", executionId);
            throw;
        }
    }

    public async Task<int> GetEventCountAsync(
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
            _logger.LogError(ex, "Error counting events: {ExecutionId}", executionId);
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

/// <summary>
/// Workflow Execution Snapshot for optimization
/// Prevents replaying entire event history
/// </summary>
public class WorkflowExecutionSnapshot
{
    /// <summary>
    /// Snapshot identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Execution ID (aggregate root)
    /// </summary>
    public string ExecutionId { get; set; } = string.Empty;

    /// <summary>
    /// Serialized execution state at snapshot point
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Sequence number of last applied event
    /// </summary>
    public long SequenceNumber { get; set; }

    /// <summary>
    /// Snapshot creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
