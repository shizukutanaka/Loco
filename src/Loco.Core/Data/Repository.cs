using Microsoft.Extensions.Logging;

namespace Loco.Core.Data;

/// <summary>
/// Base generic repository implementation
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public abstract class Repository<T> : IRepository<T> where T : class
{
    protected readonly ILogger<Repository<T>> _logger;

    protected Repository(ILogger<Repository<T>> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public abstract Task<T?> GetByIdAsync(string id);

    /// <inheritdoc />
    public abstract Task<IEnumerable<T>> GetAllAsync();

    /// <inheritdoc />
    public abstract Task<IEnumerable<T>> FindAsync(Func<T, bool> predicate);

    /// <inheritdoc />
    public abstract Task AddAsync(T entity);

    /// <inheritdoc />
    public abstract Task AddRangeAsync(IEnumerable<T> entities);

    /// <inheritdoc />
    public abstract Task UpdateAsync(T entity);

    /// <inheritdoc />
    public abstract Task RemoveAsync(T entity);

    /// <inheritdoc />
    public abstract Task RemoveRangeAsync(IEnumerable<T> entities);

    /// <inheritdoc />
    public abstract Task<bool> AnyAsync(Func<T, bool> predicate);

    /// <inheritdoc />
    public abstract Task<int> CountAsync(Func<T, bool>? predicate = null);

    /// <inheritdoc />
    public abstract Task<int> SaveChangesAsync();
}

/// <summary>
/// In-memory implementation of workflow repository
/// </summary>
public class InMemoryWorkflowRepository : Repository<WorkflowEntity>, IWorkflowRepository
{
    private readonly Dictionary<string, WorkflowEntity> _workflows = new();
    private readonly object _lock = new();

    public InMemoryWorkflowRepository(ILogger<Repository<WorkflowEntity>> logger)
        : base(logger)
    {
    }

    public override async Task<WorkflowEntity?> GetByIdAsync(string id)
    {
        lock (_lock)
        {
            _workflows.TryGetValue(id, out var workflow);
            return workflow;
        }
    }

    public override async Task<IEnumerable<WorkflowEntity>> GetAllAsync()
    {
        lock (_lock)
        {
            return _workflows.Values.ToList();
        }
    }

    public override async Task<IEnumerable<WorkflowEntity>> FindAsync(Func<WorkflowEntity, bool> predicate)
    {
        lock (_lock)
        {
            return _workflows.Values.Where(predicate).ToList();
        }
    }

    public override async Task AddAsync(WorkflowEntity entity)
    {
        lock (_lock)
        {
            if (_workflows.ContainsKey(entity.Id))
            {
                throw new InvalidOperationException($"Workflow with ID {entity.Id} already exists");
            }

            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            _workflows[entity.Id] = entity;

            _logger.LogInformation("Workflow added: {WorkflowId}", entity.Id);
        }
    }

    public override async Task AddRangeAsync(IEnumerable<WorkflowEntity> entities)
    {
        foreach (var entity in entities)
        {
            await AddAsync(entity);
        }
    }

    public override async Task UpdateAsync(WorkflowEntity entity)
    {
        lock (_lock)
        {
            if (!_workflows.ContainsKey(entity.Id))
            {
                throw new InvalidOperationException($"Workflow with ID {entity.Id} not found");
            }

            entity.UpdatedAt = DateTime.UtcNow;
            _workflows[entity.Id] = entity;

            _logger.LogInformation("Workflow updated: {WorkflowId}", entity.Id);
        }
    }

    public override async Task RemoveAsync(WorkflowEntity entity)
    {
        lock (_lock)
        {
            if (_workflows.Remove(entity.Id))
            {
                _logger.LogInformation("Workflow removed: {WorkflowId}", entity.Id);
            }
        }
    }

    public override async Task RemoveRangeAsync(IEnumerable<WorkflowEntity> entities)
    {
        foreach (var entity in entities)
        {
            await RemoveAsync(entity);
        }
    }

    public override async Task<bool> AnyAsync(Func<WorkflowEntity, bool> predicate)
    {
        lock (_lock)
        {
            return _workflows.Values.Any(predicate);
        }
    }

    public override async Task<int> CountAsync(Func<WorkflowEntity, bool>? predicate = null)
    {
        lock (_lock)
        {
            return predicate == null
                ? _workflows.Count
                : _workflows.Values.Count(predicate);
        }
    }

    public override async Task<int> SaveChangesAsync()
    {
        // In-memory repository doesn't need explicit save
        return 0;
    }

    public async Task<IEnumerable<WorkflowEntity>> GetByNameAsync(string name)
    {
        return await FindAsync(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IEnumerable<WorkflowEntity>> GetActiveAsync()
    {
        return await FindAsync(w => w.IsActive);
    }
}

/// <summary>
/// In-memory implementation of execution history repository
/// </summary>
public class InMemoryExecutionHistoryRepository : Repository<ExecutionHistoryEntity>, IExecutionHistoryRepository
{
    private readonly Dictionary<string, ExecutionHistoryEntity> _executions = new();
    private readonly object _lock = new();

    public InMemoryExecutionHistoryRepository(ILogger<Repository<ExecutionHistoryEntity>> logger)
        : base(logger)
    {
    }

    public override async Task<ExecutionHistoryEntity?> GetByIdAsync(string id)
    {
        lock (_lock)
        {
            _executions.TryGetValue(id, out var execution);
            return execution;
        }
    }

    public override async Task<IEnumerable<ExecutionHistoryEntity>> GetAllAsync()
    {
        lock (_lock)
        {
            return _executions.Values.ToList();
        }
    }

    public override async Task<IEnumerable<ExecutionHistoryEntity>> FindAsync(Func<ExecutionHistoryEntity, bool> predicate)
    {
        lock (_lock)
        {
            return _executions.Values.Where(predicate).ToList();
        }
    }

    public override async Task AddAsync(ExecutionHistoryEntity entity)
    {
        lock (_lock)
        {
            entity.StartedAt = DateTime.UtcNow;
            _executions[entity.Id] = entity;

            _logger.LogInformation("Execution recorded: {ExecutionId}", entity.Id);
        }
    }

    public override async Task AddRangeAsync(IEnumerable<ExecutionHistoryEntity> entities)
    {
        foreach (var entity in entities)
        {
            await AddAsync(entity);
        }
    }

    public override async Task UpdateAsync(ExecutionHistoryEntity entity)
    {
        lock (_lock)
        {
            if (_executions.ContainsKey(entity.Id))
            {
                _executions[entity.Id] = entity;
                _logger.LogInformation("Execution updated: {ExecutionId}", entity.Id);
            }
        }
    }

    public override async Task RemoveAsync(ExecutionHistoryEntity entity)
    {
        lock (_lock)
        {
            _executions.Remove(entity.Id);
        }
    }

    public override async Task RemoveRangeAsync(IEnumerable<ExecutionHistoryEntity> entities)
    {
        foreach (var entity in entities)
        {
            await RemoveAsync(entity);
        }
    }

    public override async Task<bool> AnyAsync(Func<ExecutionHistoryEntity, bool> predicate)
    {
        lock (_lock)
        {
            return _executions.Values.Any(predicate);
        }
    }

    public override async Task<int> CountAsync(Func<ExecutionHistoryEntity, bool>? predicate = null)
    {
        lock (_lock)
        {
            return predicate == null
                ? _executions.Count
                : _executions.Values.Count(predicate);
        }
    }

    public override async Task<int> SaveChangesAsync()
    {
        return 0;
    }

    public async Task<IEnumerable<ExecutionHistoryEntity>> GetByWorkflowIdAsync(string workflowId)
    {
        return await FindAsync(e => e.WorkflowId == workflowId);
    }

    public async Task<IEnumerable<ExecutionHistoryEntity>> GetRecentAsync(int limit = 100)
    {
        lock (_lock)
        {
            return _executions.Values
                .OrderByDescending(e => e.StartedAt)
                .Take(limit)
                .ToList();
        }
    }

    public async Task<IEnumerable<ExecutionHistoryEntity>> GetFailedAsync()
    {
        return await FindAsync(e => e.Status == ExecutionStatus.Failed);
    }
}
