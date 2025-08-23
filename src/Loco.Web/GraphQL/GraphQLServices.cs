using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Loco.Core.Models;
using Loco.Web.GraphQL.Schema;
using Microsoft.Extensions.Logging;

namespace Loco.Web.GraphQL.Services;

/// <summary>
/// Service for handling flow events in GraphQL subscriptions
/// </summary>
public class FlowEventService : IFlowEventService
{
    private readonly ILogger<FlowEventService> _logger;
    private readonly Subject<FlowEvent> _flowChanges;
    private readonly Subject<FlowExecutionEvent> _flowExecutions;
    private readonly Subject<SystemEvent> _systemEvents;

    public FlowEventService(ILogger<FlowEventService> logger)
    {
        _logger = logger;
        _flowChanges = new Subject<FlowEvent>();
        _flowExecutions = new Subject<FlowExecutionEvent>();
        _systemEvents = new Subject<SystemEvent>();
    }

    /// <summary>
    /// Observable stream of flow changes
    /// </summary>
    public IObservable<FlowEvent> FlowChanges()
    {
        _logger.LogDebug("Subscribing to flow changes");
        return _flowChanges.AsObservable();
    }

    /// <summary>
    /// Observable stream of flow executions
    /// </summary>
    public IObservable<FlowExecutionEvent> FlowExecutions(string? flowId = null)
    {
        _logger.LogDebug("Subscribing to flow executions for flow: {FlowId}", flowId ?? "all");
        
        var stream = _flowExecutions.AsObservable();
        
        if (!string.IsNullOrEmpty(flowId))
        {
            stream = stream.Where(e => e.FlowId == flowId);
        }
        
        return stream;
    }

    /// <summary>
    /// Observable stream of system events
    /// </summary>
    public IObservable<SystemEvent> SystemEvents()
    {
        _logger.LogDebug("Subscribing to system events");
        return _systemEvents.AsObservable();
    }

    /// <summary>
    /// Publish a flow change event
    /// </summary>
    public async Task PublishFlowChanged(string type, FlowDefinition flow)
    {
        try
        {
            var flowEvent = new FlowEvent
            {
                Type = type,
                Flow = flow,
                Timestamp = DateTime.UtcNow
            };

            _flowChanges.OnNext(flowEvent);
            _logger.LogInformation("Published flow change event: {Type} for flow {FlowId}", type, flow.Id);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing flow change event");
            throw;
        }
    }

    /// <summary>
    /// Publish a flow execution event
    /// </summary>
    public async Task PublishFlowExecuted(FlowExecutionEvent executionEvent)
    {
        try
        {
            executionEvent.Timestamp = DateTime.UtcNow;
            _flowExecutions.OnNext(executionEvent);
            
            _logger.LogInformation("Published flow execution event: {Status} for flow {FlowId}, execution {ExecutionId}", 
                executionEvent.Status, executionEvent.FlowId, executionEvent.ExecutionId);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing flow execution event");
            throw;
        }
    }

    /// <summary>
    /// Publish a system event
    /// </summary>
    public async Task PublishSystemEvent(SystemEvent systemEvent)
    {
        try
        {
            systemEvent.Timestamp = DateTime.UtcNow;
            _systemEvents.OnNext(systemEvent);
            
            _logger.LogInformation("Published system event: {Type} - {Message} [{Severity}]", 
                systemEvent.Type, systemEvent.Message, systemEvent.Severity);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing system event");
            throw;
        }
    }

    /// <summary>
    /// Cleanup resources
    /// </summary>
    public void Dispose()
    {
        _flowChanges?.Dispose();
        _flowExecutions?.Dispose();
        _systemEvents?.Dispose();
    }
}

/// <summary>
/// GraphQL DataLoader for batch loading flows
/// </summary>
public class FlowDataLoader : DataLoaderBase<string, FlowDefinition>
{
    private readonly IFlowRepository _flowRepository;

    public FlowDataLoader(IFlowRepository flowRepository)
    {
        _flowRepository = flowRepository;
    }

    protected override async Task<IEnumerable<FlowDefinition>> FetchAsync(IEnumerable<string> keys)
    {
        var flows = new List<FlowDefinition>();
        
        foreach (var key in keys)
        {
            var flow = await _flowRepository.GetByIdAsync(key);
            if (flow != null)
            {
                flows.Add(flow);
            }
        }
        
        return flows;
    }
}

/// <summary>
/// GraphQL authorization service for flow operations
/// </summary>
public class FlowAuthorizationService
{
    private readonly ILogger<FlowAuthorizationService> _logger;

    public FlowAuthorizationService(ILogger<FlowAuthorizationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check if user can read a flow
    /// </summary>
    public async Task<bool> CanReadFlow(string userId, string flowId)
    {
        // Implement authorization logic
        _logger.LogDebug("Checking read permission for user {UserId} on flow {FlowId}", userId, flowId);
        await Task.CompletedTask;
        return true; // For now, allow all reads
    }

    /// <summary>
    /// Check if user can write to a flow
    /// </summary>
    public async Task<bool> CanWriteFlow(string userId, string flowId)
    {
        // Implement authorization logic
        _logger.LogDebug("Checking write permission for user {UserId} on flow {FlowId}", userId, flowId);
        await Task.CompletedTask;
        return true; // For now, allow all writes
    }

    /// <summary>
    /// Check if user can delete a flow
    /// </summary>
    public async Task<bool> CanDeleteFlow(string userId, string flowId)
    {
        // Implement authorization logic
        _logger.LogDebug("Checking delete permission for user {UserId} on flow {FlowId}", userId, flowId);
        await Task.CompletedTask;
        return true; // For now, allow all deletes
    }

    /// <summary>
    /// Check if user can execute a flow
    /// </summary>
    public async Task<bool> CanExecuteFlow(string userId, string flowId)
    {
        // Implement authorization logic
        _logger.LogDebug("Checking execute permission for user {UserId} on flow {FlowId}", userId, flowId);
        await Task.CompletedTask;
        return true; // For now, allow all executions
    }
}

/// <summary>
/// GraphQL performance monitoring service
/// </summary>
public class GraphQLMetricsService
{
    private readonly ILogger<GraphQLMetricsService> _logger;
    private readonly Dictionary<string, QueryMetrics> _queryMetrics;
    private readonly object _lock = new();

    public GraphQLMetricsService(ILogger<GraphQLMetricsService> logger)
    {
        _logger = logger;
        _queryMetrics = new Dictionary<string, QueryMetrics>();
    }

    /// <summary>
    /// Record query execution
    /// </summary>
    public void RecordQuery(string queryName, TimeSpan duration, bool success)
    {
        lock (_lock)
        {
            if (!_queryMetrics.TryGetValue(queryName, out var metrics))
            {
                metrics = new QueryMetrics { QueryName = queryName };
                _queryMetrics[queryName] = metrics;
            }

            metrics.TotalExecutions++;
            metrics.TotalDuration += duration;
            
            if (success)
                metrics.SuccessCount++;
            else
                metrics.ErrorCount++;

            if (duration > metrics.MaxDuration)
                metrics.MaxDuration = duration;
            
            if (metrics.MinDuration == TimeSpan.Zero || duration < metrics.MinDuration)
                metrics.MinDuration = duration;

            _logger.LogDebug("Recorded query {QueryName}: Duration={Duration}ms, Success={Success}", 
                queryName, duration.TotalMilliseconds, success);
        }
    }

    /// <summary>
    /// Get query metrics
    /// </summary>
    public QueryMetrics? GetQueryMetrics(string queryName)
    {
        lock (_lock)
        {
            return _queryMetrics.TryGetValue(queryName, out var metrics) ? metrics : null;
        }
    }

    /// <summary>
    /// Get all query metrics
    /// </summary>
    public Dictionary<string, QueryMetrics> GetAllMetrics()
    {
        lock (_lock)
        {
            return new Dictionary<string, QueryMetrics>(_queryMetrics);
        }
    }

    /// <summary>
    /// Reset metrics
    /// </summary>
    public void ResetMetrics()
    {
        lock (_lock)
        {
            _queryMetrics.Clear();
            _logger.LogInformation("Query metrics reset");
        }
    }
}

/// <summary>
/// Query metrics data
/// </summary>
public class QueryMetrics
{
    public string QueryName { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan MinDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    
    public double AverageDuration => 
        TotalExecutions > 0 ? TotalDuration.TotalMilliseconds / TotalExecutions : 0;
    
    public double SuccessRate => 
        TotalExecutions > 0 ? (double)SuccessCount / TotalExecutions * 100 : 0;
}

/// <summary>
/// Abstract base class for DataLoader
/// </summary>
public abstract class DataLoaderBase<TKey, TValue>
{
    private readonly Dictionary<TKey, TaskCompletionSource<TValue>> _cache = new();

    public async Task<TValue> LoadAsync(TKey key)
    {
        if (_cache.TryGetValue(key, out var tcs))
        {
            return await tcs.Task;
        }

        tcs = new TaskCompletionSource<TValue>();
        _cache[key] = tcs;

        try
        {
            var values = await FetchAsync(new[] { key });
            var value = values.FirstOrDefault();
            
            if (value != null)
            {
                tcs.SetResult(value);
            }
            else
            {
                tcs.SetException(new KeyNotFoundException($"Key not found: {key}"));
            }
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return await tcs.Task;
    }

    protected abstract Task<IEnumerable<TValue>> FetchAsync(IEnumerable<TKey> keys);
}

/// <summary>
/// Interface for flow repository (to be implemented elsewhere)
/// </summary>
public interface IFlowRepository
{
    Task<FlowDefinition?> GetByIdAsync(string id);
    Task<IEnumerable<FlowDefinition>> GetAllAsync();
    Task<FlowDefinition> CreateAsync(FlowDefinition flow);
    Task<FlowDefinition> UpdateAsync(FlowDefinition flow);
    Task<bool> DeleteAsync(string id);
}
