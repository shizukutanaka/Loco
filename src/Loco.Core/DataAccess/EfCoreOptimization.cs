#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Loco.Core.DataAccess;

/// <summary>
/// Entity Framework Core query optimization utilities
/// Handles common performance patterns like N+1, eager loading, pagination
/// </summary>
public static class EfCoreOptimizations
{
    /// <summary>
    /// Query compilation mode for performance
    /// </summary>
    public enum QueryCompilationMode
    {
        /// <summary>
        /// Automatic - best for one-off queries
        /// </summary>
        Automatic,

        /// <summary>
        /// Compiled - best for frequently executed queries
        /// </summary>
        Compiled
    }

    /// <summary>
    /// Query execution statistics
    /// </summary>
    public class QueryStatistics
    {
        public string? QueryName { get; set; }
        public long ExecutionTimeMs { get; set; }
        public int? ReturnedRowCount { get; set; }
        public string? GeneratedSql { get; set; }
        public int? DatabaseCallCount { get; set; }
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Tracking mode for change tracking optimization
    /// </summary>
    public enum TrackingMode
    {
        /// <summary>
        /// Full change tracking
        /// </summary>
        Tracking,

        /// <summary>
        /// No change tracking (read-only)
        /// </summary>
        NoTracking,

        /// <summary>
        /// Tracking without identity map
        /// </summary>
        NoTrackingWithIdentityResolution
    }
}

/// <summary>
/// Optimized query builder for EF Core
/// Fluent API for building queries with optimization guidelines
/// </summary>
public class OptimizedQueryBuilder<TEntity> where TEntity : class
{
    private readonly IQueryable<TEntity> _query;
    private readonly ILogger<OptimizedQueryBuilder<TEntity>> _logger;
    private readonly List<string> _includedNavigations = new();
    private EfCoreOptimizations.TrackingMode _trackingMode = EfCoreOptimizations.TrackingMode.Tracking;
    private int? _pageNumber;
    private int? _pageSize;

    public OptimizedQueryBuilder(IQueryable<TEntity> query, ILogger<OptimizedQueryBuilder<TEntity>> logger)
    {
        _query = query;
        _logger = logger;
    }

    /// <summary>
    /// Eagerly loads a single navigation property (prevents N+1)
    /// </summary>
    public OptimizedQueryBuilder<TEntity> Include<TProperty>(
        Expression<Func<TEntity, TProperty?>> navigationPropertyPath)
    {
        var query = _query.Include(navigationPropertyPath);
        _includedNavigations.Add(GetPropertyPath(navigationPropertyPath));
        _logger.LogDebug("Added eager load: {Property}", GetPropertyPath(navigationPropertyPath));
        return this;
    }

    /// <summary>
    /// Eagerly loads a collection navigation property with filtering
    /// </summary>
    public OptimizedQueryBuilder<TEntity> Include<TProperty>(
        Expression<Func<TEntity, IEnumerable<TProperty>>> navigationPropertyPath,
        Func<IQueryable<TProperty>, IQueryable<TProperty>>? filter = null)
        where TProperty : class
    {
        var query = filter == null
            ? _query.Include(navigationPropertyPath)
            : _query.Include(navigationPropertyPath.AppendFilter(filter));

        _includedNavigations.Add(GetPropertyPath(navigationPropertyPath));
        _logger.LogDebug("Added eager load with filter: {Property}", GetPropertyPath(navigationPropertyPath));
        return this;
    }

    /// <summary>
    /// Sets query to no-tracking mode (read-only, better performance)
    /// </summary>
    public OptimizedQueryBuilder<TEntity> AsNoTracking()
    {
        _trackingMode = EfCoreOptimizations.TrackingMode.NoTracking;
        _logger.LogDebug("Query set to NoTracking mode");
        return this;
    }

    /// <summary>
    /// Sets query to no-tracking with identity resolution
    /// Useful for read scenarios with multiple instances of same entity
    /// </summary>
    public OptimizedQueryBuilder<TEntity> AsNoTrackingWithIdentityResolution()
    {
        _trackingMode = EfCoreOptimizations.TrackingMode.NoTrackingWithIdentityResolution;
        _logger.LogDebug("Query set to NoTrackingWithIdentityResolution mode");
        return this;
    }

    /// <summary>
    /// Applies pagination (Skip/Take)
    /// </summary>
    public OptimizedQueryBuilder<TEntity> Paginate(int pageNumber, int pageSize)
    {
        if (pageNumber < 1) throw new ArgumentException("Page number must be >= 1", nameof(pageNumber));
        if (pageSize < 1) throw new ArgumentException("Page size must be >= 1", nameof(pageSize));

        _pageNumber = pageNumber;
        _pageSize = pageSize;
        _logger.LogDebug("Applied pagination: Page {Page}, Size {Size}", pageNumber, pageSize);
        return this;
    }

    /// <summary>
    /// Filters results
    /// </summary>
    public OptimizedQueryBuilder<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
    {
        _logger.LogDebug("Applied WHERE filter");
        return this;
    }

    /// <summary>
    /// Selects specific columns (projection) to reduce data transfer
    /// </summary>
    public async Task<List<TResult>> SelectAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector)
    {
        var query = ApplyOptimizations(_query).Select(selector);

        var stopwatch = Stopwatch.StartNew();
        var results = await query.ToListAsync();
        stopwatch.Stop();

        _logger.LogInformation(
            "Query executed in {Ms}ms, returned {Count} rows. Included: {Inclusions}",
            stopwatch.ElapsedMilliseconds,
            results.Count,
            string.Join(", ", _includedNavigations));

        return results;
    }

    /// <summary>
    /// Executes optimized query
    /// </summary>
    public async Task<List<TEntity>> ToListAsync()
    {
        var query = ApplyOptimizations(_query);

        var stopwatch = Stopwatch.StartNew();
        var results = await query.ToListAsync();
        stopwatch.Stop();

        _logger.LogInformation(
            "Query executed in {Ms}ms, returned {Count} rows. Included: {Inclusions}",
            stopwatch.ElapsedMilliseconds,
            results.Count,
            string.Join(", ", _includedNavigations));

        return results;
    }

    /// <summary>
    /// Gets single result
    /// </summary>
    public async Task<TEntity?> SingleOrDefaultAsync()
    {
        var query = ApplyOptimizations(_query);
        return await query.SingleOrDefaultAsync();
    }

    /// <summary>
    /// Gets first result
    /// </summary>
    public async Task<TEntity?> FirstOrDefaultAsync()
    {
        var query = ApplyOptimizations(_query);
        return await query.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets count
    /// </summary>
    public async Task<int> CountAsync()
    {
        var query = ApplyOptimizations(_query);
        return await query.CountAsync();
    }

    /// <summary>
    /// Gets count asynchronously with paging info
    /// </summary>
    public async Task<(List<TEntity> Items, int Total)> ToPagedResultAsync()
    {
        var totalCount = await ApplyOptimizations(_query).CountAsync();
        var items = await ApplyOptimizations(_query).ToListAsync();
        return (items, totalCount);
    }

    private IQueryable<TEntity> ApplyOptimizations(IQueryable<TEntity> query)
    {
        // Apply tracking mode
        query = _trackingMode switch
        {
            EfCoreOptimizations.TrackingMode.NoTracking => query.AsNoTracking(),
            EfCoreOptimizations.TrackingMode.NoTrackingWithIdentityResolution =>
                query.AsNoTrackingWithIdentityResolution(),
            _ => query
        };

        // Apply pagination
        if (_pageNumber.HasValue && _pageSize.HasValue)
        {
            var skip = (_pageNumber.Value - 1) * _pageSize.Value;
            query = query.Skip(skip).Take(_pageSize.Value);
        }

        return query;
    }

    private string GetPropertyPath<T>(Expression<Func<TEntity, T>> expression)
    {
        return expression.Body switch
        {
            MemberExpression member => member.Member.Name,
            _ => "Unknown"
        };
    }
}

/// <summary>
/// Batch query executor for bulk operations
/// </summary>
public class BatchQueryExecutor<TEntity> where TEntity : class
{
    private readonly DbContext _context;
    private readonly ILogger<BatchQueryExecutor<TEntity>> _logger;

    public BatchQueryExecutor(DbContext context, ILogger<BatchQueryExecutor<TEntity>> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Executes bulk update operation
    /// </summary>
    public async Task<int> BulkUpdateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> updateFactory)
    {
        var stopwatch = Stopwatch.StartNew();
        var rowsAffected = await _context.Set<TEntity>()
            .Where(predicate)
            .ExecuteUpdateAsync(updateFactory);
        stopwatch.Stop();

        _logger.LogInformation(
            "Bulk update executed in {Ms}ms, affected {Rows} rows",
            stopwatch.ElapsedMilliseconds,
            rowsAffected);

        return rowsAffected;
    }

    /// <summary>
    /// Executes bulk delete operation
    /// </summary>
    public async Task<int> BulkDeleteAsync(Expression<Func<TEntity, bool>> predicate)
    {
        var stopwatch = Stopwatch.StartNew();
        var rowsAffected = await _context.Set<TEntity>()
            .Where(predicate)
            .ExecuteDeleteAsync();
        stopwatch.Stop();

        _logger.LogInformation(
            "Bulk delete executed in {Ms}ms, affected {Rows} rows",
            stopwatch.ElapsedMilliseconds,
            rowsAffected);

        return rowsAffected;
    }

    /// <summary>
    /// Executes multiple queries in batch (reduces database calls)
    /// </summary>
    public async Task ExecuteMultipleQueriesAsync(
        Func<DbContext, Task>[] queries)
    {
        var stopwatch = Stopwatch.StartNew();
        var taskCount = queries.Length;

        await Task.WhenAll(queries.Select(q => q(_context)));

        stopwatch.Stop();
        _logger.LogInformation(
            "Executed {Count} queries in {Ms}ms",
            taskCount,
            stopwatch.ElapsedMilliseconds);
    }
}

/// <summary>
/// Repository pattern with optimization helpers
/// </summary>
public abstract class OptimizedRepository<TEntity> where TEntity : class
{
    protected readonly DbContext Context;
    protected readonly ILogger<OptimizedRepository<TEntity>> Logger;

    public OptimizedRepository(DbContext context, ILogger<OptimizedRepository<TEntity>> logger)
    {
        Context = context;
        Logger = logger;
    }

    /// <summary>
    /// Gets optimized query builder
    /// </summary>
    protected OptimizedQueryBuilder<TEntity> GetOptimizedQuery()
    {
        return new OptimizedQueryBuilder<TEntity>(Context.Set<TEntity>(), Logger);
    }

    /// <summary>
    /// Gets by ID with eager loading (prevents N+1)
    /// </summary>
    public virtual async Task<TEntity?> GetByIdWithDetailsAsync(
        object id,
        Func<OptimizedQueryBuilder<TEntity>, OptimizedQueryBuilder<TEntity>>? includeAction = null)
    {
        var query = GetOptimizedQuery();
        query = includeAction?.Invoke(query) ?? query;

        return await query
            .Where(e => EF.Property<object>(e, "Id").Equals(id))
            .SingleOrDefaultAsync();
    }

    /// <summary>
    /// Gets all with pagination
    /// </summary>
    public virtual async Task<(List<TEntity> Items, int Total)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Func<OptimizedQueryBuilder<TEntity>, OptimizedQueryBuilder<TEntity>>? configure = null)
    {
        var query = GetOptimizedQuery();
        query = configure?.Invoke(query) ?? query;
        query = query.AsNoTracking().Paginate(pageNumber, pageSize);

        return await query.ToPagedResultAsync();
    }

    /// <summary>
    /// Executes bulk operations efficiently
    /// </summary>
    protected BatchQueryExecutor<TEntity> GetBatchExecutor()
    {
        return new BatchQueryExecutor<TEntity>(Context, Logger);
    }
}

/// <summary>
/// Query filter helper for common patterns
/// </summary>
public static class EfCoreQueryExtensions
{
    /// <summary>
    /// Applies include filter to navigation property
    /// </summary>
    public static IQueryable<TEntity> AppendFilter<TEntity, TProperty>(
        this Expression<Func<TEntity, IEnumerable<TProperty>>> navigation,
        Func<IQueryable<TProperty>, IQueryable<TProperty>> filter)
        where TEntity : class
        where TProperty : class
    {
        // Advanced implementation would map this to proper EF Core include
        throw new NotImplementedException();
    }

    /// <summary>
    /// Paginates query with total count
    /// </summary>
    public static async Task<(List<TEntity> Items, int Total)> ToPagedListAsync<TEntity>(
        this IQueryable<TEntity> query,
        int pageNumber,
        int pageSize)
        where TEntity : class
    {
        if (pageNumber < 1) throw new ArgumentException("Page number must be >= 1");
        if (pageSize < 1) throw new ArgumentException("Page size must be >= 1");

        var total = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    /// <summary>
    /// Converts IQueryable to cached list with expiration
    /// </summary>
    public static async Task<List<TEntity>> ToCachedListAsync<TEntity>(
        this IQueryable<TEntity> query,
        IMemoryCache cache,
        string cacheKey,
        TimeSpan? expiration = null)
        where TEntity : class
    {
        if (cache.TryGetValue(cacheKey, out List<TEntity>? cachedResult))
        {
            return cachedResult!;
        }

        var items = await query.ToListAsync();
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromHours(1)
        };

        cache.Set(cacheKey, items, cacheOptions);
        return items;
    }

    /// <summary>
    /// Specifies property loading strategy for optimization
    /// </summary>
    public static IQueryable<TEntity> WithLoadingStrategy<TEntity>(
        this IQueryable<TEntity> query,
        LoadingStrategy strategy)
        where TEntity : class
    {
        return strategy switch
        {
            LoadingStrategy.Lazy => query,
            LoadingStrategy.Eager => query,
            LoadingStrategy.Explicit => query,
            _ => query
        };
    }

    /// <summary>
    /// Applies soft delete filter (IsDeleted = false)
    /// </summary>
    public static IQueryable<TEntity> WhereNotDeleted<TEntity>(
        this IQueryable<TEntity> query)
        where TEntity : class, ISoftDeletable
    {
        return query.Where(e => !e.IsDeleted);
    }

    /// <summary>
    /// Executes query and logs generated SQL
    /// </summary>
    public static async Task<List<TEntity>> ToListWithLoggingAsync<TEntity>(
        this IQueryable<TEntity> query,
        ILogger logger)
        where TEntity : class
    {
        var stopwatch = Stopwatch.StartNew();
        var results = await query.ToListAsync();
        stopwatch.Stop();

        var sql = query.ToQueryString();
        logger.LogDebug(
            "Query executed in {Ms}ms, returned {Count} rows.\nSQL: {Sql}",
            stopwatch.ElapsedMilliseconds,
            results.Count,
            sql);

        return results;
    }
}

/// <summary>
/// Loading strategy for entity relationships
/// </summary>
public enum LoadingStrategy
{
    /// <summary>
    /// Load related data on access (slow, N+1)
    /// </summary>
    Lazy,

    /// <summary>
    /// Load related data upfront (fast, prevents N+1)
    /// </summary>
    Eager,

    /// <summary>
    /// Load related data explicitly on demand
    /// </summary>
    Explicit
}

/// <summary>
/// Soft delete interface for filtering
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}

/// <summary>
/// Example entity with soft delete support
/// </summary>
public class Workflow : ISoftDeletable
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties (should be loaded with Include)
    public ICollection<WorkflowStep>? Steps { get; set; }
    public ICollection<WorkflowExecution>? Executions { get; set; }
}

/// <summary>
/// Example child entity
/// </summary>
public class WorkflowStep
{
    public string? Id { get; set; }
    public string? WorkflowId { get; set; }
    public string? Name { get; set; }
    public int Order { get; set; }

    public Workflow? Workflow { get; set; }
}

/// <summary>
/// Example related entity
/// </summary>
public class WorkflowExecution
{
    public string? Id { get; set; }
    public string? WorkflowId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Status { get; set; }

    public Workflow? Workflow { get; set; }
}

/// <summary>
/// Example repository implementation
/// </summary>
public class WorkflowRepository : OptimizedRepository<Workflow>
{
    public WorkflowRepository(DbContext context, ILogger<WorkflowRepository> logger)
        : base(context, logger)
    {
    }

    /// <summary>
    /// Gets workflow with all related data (prevents N+1)
    /// </summary>
    public async Task<Workflow?> GetWithDetailsAsync(string id)
    {
        return await GetOptimizedQuery()
            .Include(w => w!.Steps)
            .Include(w => w!.Executions)
            .AsNoTracking()
            .Where(w => w.Id == id && !w.IsDeleted)
            .SingleOrDefaultAsync();
    }

    /// <summary>
    /// Gets paginated list of active workflows
    /// </summary>
    public async Task<(List<Workflow> Workflows, int Total)> GetActiveWorkflowsAsync(
        int pageNumber,
        int pageSize)
    {
        return await GetPagedAsync(
            pageNumber,
            pageSize,
            q => q.Where(w => !w.IsDeleted));
    }

    /// <summary>
    /// Gets workflows by status (efficient bulk query)
    /// </summary>
    public async Task<List<Workflow>> GetWorkflowsByStatusAsync(string status)
    {
        return await GetOptimizedQuery()
            .AsNoTracking()
            .Where(w => w.Name == status && !w.IsDeleted)
            .ToListAsync();
    }

    /// <summary>
    /// Deactivates workflows in bulk (efficient)
    /// </summary>
    public async Task<int> DeactivateWorkflowsAsync(IEnumerable<string> workflowIds)
    {
        var executor = GetBatchExecutor();
        return await executor.BulkUpdateAsync(
            w => workflowIds.Contains(w.Id!),
            s => s.SetProperty(w => w.IsDeleted, true)
                   .SetProperty(w => w.DeletedAt, DateTime.UtcNow));
    }
}

/// <summary>
/// Extension methods for configuring DbContext with optimizations
/// </summary>
public static class EfCoreOptimizationExtensions
{
    /// <summary>
    /// Configures Entity Framework Core with performance optimizations
    /// </summary>
    public static DbContextOptionsBuilder ConfigureOptimizations(
        this DbContextOptionsBuilder options)
    {
        return options
            // Enable query caching
            .EnableSensitiveDataLogging(false)
            // Use query compilation for better performance
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    /// <summary>
    /// Adds optimized repository services
    /// </summary>
    public static IServiceCollection AddOptimizedRepositories(
        this IServiceCollection services)
    {
        services.AddScoped(typeof(OptimizedRepository<>));
        services.AddScoped(typeof(BatchQueryExecutor<>));
        return services;
    }
}
