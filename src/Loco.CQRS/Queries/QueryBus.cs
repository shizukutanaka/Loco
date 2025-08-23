using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loco.CQRS.Queries;

/// <summary>
/// Base query interface
/// </summary>
public interface IQuery<TResult>
{
    Guid QueryId { get; }
    DateTime Timestamp { get; }
    string? UserId { get; }
}

/// <summary>
/// Base query implementation
/// </summary>
public abstract class QueryBase<TResult> : IQuery<TResult>
{
    public Guid QueryId { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public string? UserId { get; set; }
}

/// <summary>
/// Query handler interface
/// </summary>
public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Query bus interface
/// </summary>
public interface IQueryBus
{
    Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Query bus implementation
/// </summary>
public class QueryBus : IQueryBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueryBus> _logger;
    private readonly List<IQueryMiddleware> _middlewares;

    public QueryBus(
        IServiceProvider serviceProvider,
        ILogger<QueryBus> logger,
        IEnumerable<IQueryMiddleware>? middlewares = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _middlewares = middlewares?.ToList() ?? new List<IQueryMiddleware>();
    }

    public async Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending query {QueryType} with ID {QueryId}",
            query.GetType().Name, query.QueryId);

        TResult? result = default;

        // Create handler execution function
        Func<Task<object?>> handlerExecution = async () =>
        {
            // Get handler
            var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
            var handler = _serviceProvider.GetService(handlerType);

            if (handler == null)
            {
                throw new InvalidOperationException(
                    $"No handler registered for query type {query.GetType().Name}");
            }

            // Execute handler
            var handleMethod = handlerType.GetMethod("HandleAsync");
            if (handleMethod != null)
            {
                var task = handleMethod.Invoke(handler, new object[] { query, cancellationToken });
                if (task != null)
                {
                    var taskType = task.GetType();
                    var resultProperty = taskType.GetProperty("Result");
                    if (resultProperty != null)
                    {
                        await (Task)task;
                        return resultProperty.GetValue(task);
                    }
                }
            }

            return null;
        };

        // Apply middlewares in reverse order (like a pipeline)
        var pipeline = _middlewares
            .Reverse<IQueryMiddleware>()
            .Aggregate(handlerExecution, (next, middleware) =>
                async () => await middleware.ExecuteAsync(query, next, cancellationToken));

        var pipelineResult = await pipeline();
        result = (TResult?)pipelineResult;

        _logger.LogDebug("Query {QueryType} with ID {QueryId} handled successfully",
            query.GetType().Name, query.QueryId);

        return result!;
    }
}

/// <summary>
/// Query middleware interface
/// </summary>
public interface IQueryMiddleware
{
    Task<object?> ExecuteAsync<TResult>(
        IQuery<TResult> query, 
        Func<Task<object?>> next, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Caching middleware for queries
/// </summary>
public class CachingQueryMiddleware : IQueryMiddleware
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachingQueryMiddleware> _logger;
    private readonly TimeSpan _defaultExpiration;

    public CachingQueryMiddleware(
        IMemoryCache cache,
        ILogger<CachingQueryMiddleware> logger,
        TimeSpan? defaultExpiration = null)
    {
        _cache = cache;
        _logger = logger;
        _defaultExpiration = defaultExpiration ?? TimeSpan.FromMinutes(5);
    }

    public async Task<object?> ExecuteAsync<TResult>(
        IQuery<TResult> query, 
        Func<Task<object?>> next, 
        CancellationToken cancellationToken = default)
    {
        // Check if query is cacheable
        if (query is ICacheableQuery cacheable)
        {
            var cacheKey = cacheable.GetCacheKey();
            
            // Try to get from cache
            if (_cache.TryGetValue(cacheKey, out TResult? cachedResult))
            {
                _logger.LogDebug("Query {QueryType} result retrieved from cache with key {CacheKey}",
                    query.GetType().Name, cacheKey);
                return cachedResult;
            }

            // Execute query
            var result = await next();

            // Cache the result
            var expiration = cacheable.CacheDuration ?? _defaultExpiration;
            _cache.Set(cacheKey, result, expiration);
            
            _logger.LogDebug("Query {QueryType} result cached with key {CacheKey} for {Duration}",
                query.GetType().Name, cacheKey, expiration);

            return result;
        }

        // Not cacheable, just execute
        return await next();
    }
}

/// <summary>
/// Cacheable query interface
/// </summary>
public interface ICacheableQuery
{
    string GetCacheKey();
    TimeSpan? CacheDuration { get; }
}

/// <summary>
/// Logging middleware for queries
/// </summary>
public class LoggingQueryMiddleware : IQueryMiddleware
{
    private readonly ILogger<LoggingQueryMiddleware> _logger;

    public LoggingQueryMiddleware(ILogger<LoggingQueryMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task<object?> ExecuteAsync<TResult>(
        IQuery<TResult> query, 
        Func<Task<object?>> next, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing query {QueryType} by user {UserId}",
            query.GetType().Name, query.UserId ?? "anonymous");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var result = await next();
            
            _logger.LogInformation("Query {QueryType} executed successfully in {ElapsedMs}ms",
                query.GetType().Name, stopwatch.ElapsedMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Query {QueryType} failed after {ElapsedMs}ms",
                query.GetType().Name, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

/// <summary>
/// Performance monitoring middleware for queries
/// </summary>
public class PerformanceQueryMiddleware : IQueryMiddleware
{
    private readonly ILogger<PerformanceQueryMiddleware> _logger;
    private readonly TimeSpan _threshold;

    public PerformanceQueryMiddleware(
        ILogger<PerformanceQueryMiddleware> logger,
        TimeSpan? threshold = null)
    {
        _logger = logger;
        _threshold = threshold ?? TimeSpan.FromSeconds(1);
    }

    public async Task<object?> ExecuteAsync<TResult>(
        IQuery<TResult> query, 
        Func<Task<object?>> next, 
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var result = await next();
        
        stopwatch.Stop();

        if (stopwatch.Elapsed > _threshold)
        {
            _logger.LogWarning("Query {QueryType} took {ElapsedMs}ms which exceeds threshold of {ThresholdMs}ms",
                query.GetType().Name, 
                stopwatch.ElapsedMilliseconds,
                _threshold.TotalMilliseconds);
        }

        return result;
    }
}

/// <summary>
/// Read model interface for CQRS read side
/// </summary>
public interface IReadModel
{
    Task RebuildAsync(CancellationToken cancellationToken = default);
    Task<bool> IsStaleAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Base read model implementation
/// </summary>
public abstract class ReadModelBase : IReadModel
{
    protected DateTime LastUpdated { get; set; }
    protected abstract TimeSpan StalenessThreshold { get; }

    public abstract Task RebuildAsync(CancellationToken cancellationToken = default);

    public Task<bool> IsStaleAsync(CancellationToken cancellationToken = default)
    {
        var isStale = DateTime.UtcNow - LastUpdated > StalenessThreshold;
        return Task.FromResult(isStale);
    }
}

/// <summary>
/// Projection interface for event sourcing
/// </summary>
public interface IProjection
{
    Task ProjectAsync(IEvent @event, CancellationToken cancellationToken = default);
    Task RebuildAsync(IEnumerable<IEvent> events, CancellationToken cancellationToken = default);
}

/// <summary>
/// Base projection implementation
/// </summary>
public abstract class ProjectionBase : IProjection
{
    protected readonly ILogger Logger;

    protected ProjectionBase(ILogger logger)
    {
        Logger = logger;
    }

    public abstract Task ProjectAsync(IEvent @event, CancellationToken cancellationToken = default);

    public virtual async Task RebuildAsync(IEnumerable<IEvent> events, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Rebuilding projection {ProjectionType}", GetType().Name);
        
        foreach (var @event in events)
        {
            await ProjectAsync(@event, cancellationToken);
        }
        
        Logger.LogInformation("Projection {ProjectionType} rebuilt successfully", GetType().Name);
    }
}

/// <summary>
/// Event interface for projections
/// </summary>
public interface IEvent
{
    Guid EventId { get; }
    Guid AggregateId { get; }
    DateTime Timestamp { get; }
    int Version { get; }
}

/// <summary>
/// Query specification pattern support
/// </summary>
public interface ISpecification<T>
{
    bool IsSatisfiedBy(T entity);
}

/// <summary>
/// Base specification implementation
/// </summary>
public abstract class Specification<T> : ISpecification<T>
{
    public abstract bool IsSatisfiedBy(T entity);

    public Specification<T> And(ISpecification<T> specification)
    {
        return new AndSpecification<T>(this, specification);
    }

    public Specification<T> Or(ISpecification<T> specification)
    {
        return new OrSpecification<T>(this, specification);
    }

    public Specification<T> Not()
    {
        return new NotSpecification<T>(this);
    }
}

/// <summary>
/// AND specification combinator
/// </summary>
public class AndSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;

    public AndSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override bool IsSatisfiedBy(T entity)
    {
        return _left.IsSatisfiedBy(entity) && _right.IsSatisfiedBy(entity);
    }
}

/// <summary>
/// OR specification combinator
/// </summary>
public class OrSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;

    public OrSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override bool IsSatisfiedBy(T entity)
    {
        return _left.IsSatisfiedBy(entity) || _right.IsSatisfiedBy(entity);
    }
}

/// <summary>
/// NOT specification combinator
/// </summary>
public class NotSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _specification;

    public NotSpecification(ISpecification<T> specification)
    {
        _specification = specification;
    }

    public override bool IsSatisfiedBy(T entity)
    {
        return !_specification.IsSatisfiedBy(entity);
    }
}
