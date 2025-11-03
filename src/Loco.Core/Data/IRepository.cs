namespace Loco.Core.Data;

/// <summary>
/// Generic repository interface for data access abstraction
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Gets an entity by ID
    /// </summary>
    Task<T?> GetByIdAsync(string id);

    /// <summary>
    /// Gets all entities
    /// </summary>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// Finds entities matching the predicate
    /// </summary>
    Task<IEnumerable<T>> FindAsync(Func<T, bool> predicate);

    /// <summary>
    /// Adds an entity
    /// </summary>
    Task AddAsync(T entity);

    /// <summary>
    /// Adds multiple entities
    /// </summary>
    Task AddRangeAsync(IEnumerable<T> entities);

    /// <summary>
    /// Updates an entity
    /// </summary>
    Task UpdateAsync(T entity);

    /// <summary>
    /// Removes an entity
    /// </summary>
    Task RemoveAsync(T entity);

    /// <summary>
    /// Removes multiple entities
    /// </summary>
    Task RemoveRangeAsync(IEnumerable<T> entities);

    /// <summary>
    /// Checks if any entity matches the predicate
    /// </summary>
    Task<bool> AnyAsync(Func<T, bool> predicate);

    /// <summary>
    /// Counts entities matching the predicate
    /// </summary>
    Task<int> CountAsync(Func<T, bool>? predicate = null);

    /// <summary>
    /// Saves all changes
    /// </summary>
    Task<int> SaveChangesAsync();
}

/// <summary>
/// Workflow repository interface
/// </summary>
public interface IWorkflowRepository : IRepository<WorkflowEntity>
{
    /// <summary>
    /// Gets workflows by name
    /// </summary>
    Task<IEnumerable<WorkflowEntity>> GetByNameAsync(string name);

    /// <summary>
    /// Gets active workflows
    /// </summary>
    Task<IEnumerable<WorkflowEntity>> GetActiveAsync();
}

/// <summary>
/// Execution history repository interface
/// </summary>
public interface IExecutionHistoryRepository : IRepository<ExecutionHistoryEntity>
{
    /// <summary>
    /// Gets executions for a workflow
    /// </summary>
    Task<IEnumerable<ExecutionHistoryEntity>> GetByWorkflowIdAsync(string workflowId);

    /// <summary>
    /// Gets recent executions
    /// </summary>
    Task<IEnumerable<ExecutionHistoryEntity>> GetRecentAsync(int limit = 100);

    /// <summary>
    /// Gets failed executions
    /// </summary>
    Task<IEnumerable<ExecutionHistoryEntity>> GetFailedAsync();
}

/// <summary>
/// Workflow entity
/// </summary>
public class WorkflowEntity
{
    /// <summary>
    /// Workflow ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Workflow name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Workflow description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Workflow definition (JSON)
    /// </summary>
    public string Definition { get; set; } = string.Empty;

    /// <summary>
    /// Created date
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last modified date
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Version
    /// </summary>
    public int Version { get; set; }
}

/// <summary>
/// Execution history entity
/// </summary>
public class ExecutionHistoryEntity
{
    /// <summary>
    /// Execution ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Workflow ID
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Execution status
    /// </summary>
    public ExecutionStatus Status { get; set; }

    /// <summary>
    /// Start time
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// End time
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Execution result
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Error message
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Execution parameters (JSON)
    /// </summary>
    public string? Parameters { get; set; }
}

/// <summary>
/// Execution status
/// </summary>
public enum ExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
