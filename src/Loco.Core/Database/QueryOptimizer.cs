using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Database;

/// <summary>
/// Query optimizer for Entity Framework Core
/// Implements query plan caching and optimization strategies
/// </summary>
public sealed class QueryOptimizer<TContext> where TContext : DbContext
{
    private readonly ILogger<QueryOptimizer<TContext>> _logger;
    private readonly Dictionary<string, CompiledQuery> _compiledQueries;
    private readonly Dictionary<string, QueryPlan> _queryPlans;
    private readonly SemaphoreSlim _compileLock;
    private readonly QueryStatistics _statistics;
    
    private class CompiledQuery
    {
        public Delegate CompiledDelegate { get; set; }
        public string SqlQuery { get; set; }
        public DateTime CompiledAt { get; set; }
        public long ExecutionCount { get; set; }
        public long TotalExecutionTime { get; set; }
    }
    
    private class QueryPlan
    {
        public string Query { get; set; }
        public List<string> Indexes { get; set; }
        public double EstimatedCost { get; set; }
        public string ExecutionPlan { get; set; }
    }
    
    public class QueryStatistics
    {
        public long TotalQueries { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public long TotalExecutionTime { get; set; }
        public double AverageExecutionTime => TotalQueries > 0 ? (double)TotalExecutionTime / TotalQueries : 0;
        public double CacheHitRate => TotalQueries > 0 ? (double)CacheHits / TotalQueries : 0;
        public Dictionary<string, QueryMetrics> TopQueries { get; set; } = new();
    }
    
    public class QueryMetrics
    {
        public string Query { get; set; }
        public long ExecutionCount { get; set; }
        public long TotalTime { get; set; }
        public double AverageTime => ExecutionCount > 0 ? (double)TotalTime / ExecutionCount : 0;
        public double Cost { get; set; }
    }
    
    public QueryOptimizer(ILogger<QueryOptimizer<TContext>> logger)
    {
        _logger = logger;
        _compiledQueries = new Dictionary<string, CompiledQuery>();
        _queryPlans = new Dictionary<string, QueryPlan>();
        _compileLock = new SemaphoreSlim(1, 1);
        _statistics = new QueryStatistics();
    }
    
    /// <summary>
    /// Execute an optimized query with caching
    /// </summary>
    public async Task<TResult> ExecuteOptimizedAsync<TResult>(
        TContext context,
        Expression<Func<TContext, TResult>> queryExpression,
        CancellationToken cancellationToken = default)
    {
        var queryKey = GetQueryKey(queryExpression);
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Check cache
            if (_compiledQueries.TryGetValue(queryKey, out var compiled))
            {
                Interlocked.Increment(ref _statistics.CacheHits);
                Interlocked.Increment(ref compiled.ExecutionCount);
                
                var result = (TResult)compiled.CompiledDelegate.DynamicInvoke(context);
                
                stopwatch.Stop();
                UpdateStatistics(queryKey, stopwatch.ElapsedMilliseconds);
                
                return result;
            }
            
            // Compile and cache
            Interlocked.Increment(ref _statistics.CacheMisses);
            
            await _compileLock.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring lock
                if (_compiledQueries.TryGetValue(queryKey, out compiled))
                {
                    var result = (TResult)compiled.CompiledDelegate.DynamicInvoke(context);
                    stopwatch.Stop();
                    UpdateStatistics(queryKey, stopwatch.ElapsedMilliseconds);
                    return result;
                }
                
                // Compile query
                var compiledQuery = queryExpression.Compile();
                
                _compiledQueries[queryKey] = new CompiledQuery
                {
                    CompiledDelegate = compiledQuery,
                    SqlQuery = GetSqlQuery(context, queryExpression),
                    CompiledAt = DateTime.UtcNow,
                    ExecutionCount = 1
                };
                
                var queryResult = compiledQuery(context);
                
                stopwatch.Stop();
                UpdateStatistics(queryKey, stopwatch.ElapsedMilliseconds);
                
                _logger.LogDebug("Compiled and cached query: {Key}", queryKey);
                
                return queryResult;
            }
            finally
            {
                _compileLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing optimized query");
            throw;
        }
        finally
        {
            Interlocked.Add(ref _statistics.TotalExecutionTime, stopwatch.ElapsedMilliseconds);
            Interlocked.Increment(ref _statistics.TotalQueries);
        }
    }
    
    /// <summary>
    /// Execute a batch of queries with optimizations
    /// </summary>
    public async Task<List<T>> ExecuteBatchAsync<T>(
        TContext context,
        IQueryable<T> baseQuery,
        int batchSize = 1000,
        CancellationToken cancellationToken = default)
    {
        var results = new List<T>();
        var skip = 0;
        
        while (true)
        {
            var batch = await baseQuery
                .Skip(skip)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
            
            if (batch.Count == 0)
                break;
            
            results.AddRange(batch);
            skip += batchSize;
            
            if (batch.Count < batchSize)
                break;
        }
        
        return results;
    }
    
    /// <summary>
    /// Optimize a query by analyzing and suggesting improvements
    /// </summary>
    public async Task<QueryOptimizationResult> AnalyzeQueryAsync<T>(
        TContext context,
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        var result = new QueryOptimizationResult();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Get SQL query
            var sql = query.ToQueryString();
            result.OriginalQuery = sql;
            
            // Analyze query plan
            var plan = await GetQueryPlanAsync(context, sql, cancellationToken);
            result.ExecutionPlan = plan;
            
            // Suggest optimizations
            result.Suggestions = AnalyzeQueryPlan(plan);
            
            // Check for missing indexes
            result.MissingIndexes = await DetectMissingIndexesAsync(context, sql, cancellationToken);
            
            // Estimate cost
            result.EstimatedCost = EstimateQueryCost(plan);
            
            stopwatch.Stop();
            result.AnalysisTime = stopwatch.ElapsedMilliseconds;
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing query");
            result.Error = ex.Message;
            return result;
        }
    }
    
    /// <summary>
    /// Create optimized indexes based on query patterns
    /// </summary>
    public async Task<IndexCreationResult> CreateOptimizedIndexesAsync(
        TContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new IndexCreationResult();
        
        try
        {
            // Analyze top queries
            var topQueries = _statistics.TopQueries
                .OrderByDescending(q => q.Value.ExecutionCount)
                .Take(10);
            
            foreach (var query in topQueries)
            {
                var missingIndexes = await DetectMissingIndexesAsync(
                    context, 
                    query.Value.Query, 
                    cancellationToken);
                
                foreach (var index in missingIndexes)
                {
                    try
                    {
                        await CreateIndexAsync(context, index, cancellationToken);
                        result.CreatedIndexes.Add(index);
                    }
                    catch (Exception ex)
                    {
                        result.FailedIndexes.Add(new FailedIndex
                        {
                            IndexName = index,
                            Error = ex.Message
                        });
                    }
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating optimized indexes");
            result.Error = ex.Message;
            return result;
        }
    }
    
    /// <summary>
    /// Clear query cache
    /// </summary>
    public void ClearCache()
    {
        _compiledQueries.Clear();
        _queryPlans.Clear();
        _logger.LogInformation("Query cache cleared");
    }
    
    /// <summary>
    /// Get query statistics
    /// </summary>
    public QueryStatistics GetStatistics()
    {
        return new QueryStatistics
        {
            TotalQueries = _statistics.TotalQueries,
            CacheHits = _statistics.CacheHits,
            CacheMisses = _statistics.CacheMisses,
            TotalExecutionTime = _statistics.TotalExecutionTime,
            TopQueries = _statistics.TopQueries.ToDictionary(
                kvp => kvp.Key,
                kvp => new QueryMetrics
                {
                    Query = kvp.Value.Query,
                    ExecutionCount = kvp.Value.ExecutionCount,
                    TotalTime = kvp.Value.TotalTime,
                    Cost = kvp.Value.Cost
                })
        };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetQueryKey<TResult>(Expression<Func<TContext, TResult>> expression)
    {
        return expression.ToString();
    }
    
    private string GetSqlQuery<TResult>(TContext context, Expression<Func<TContext, TResult>> expression)
    {
        try
        {
            // This is simplified - actual implementation would vary by provider
            return expression.ToString();
        }
        catch
        {
            return "Unknown";
        }
    }
    
    private async Task<string> GetQueryPlanAsync(DbContext context, string sql, CancellationToken cancellationToken)
    {
        // Get execution plan (provider-specific)
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        
        try
        {
            using var command = connection.CreateCommand();
            
            // SQL Server example
            if (connection.GetType().Name.Contains("SqlConnection"))
            {
                command.CommandText = $"SET SHOWPLAN_TEXT ON; {sql}; SET SHOWPLAN_TEXT OFF";
            }
            // PostgreSQL example
            else if (connection.GetType().Name.Contains("NpgsqlConnection"))
            {
                command.CommandText = $"EXPLAIN ANALYZE {sql}";
            }
            else
            {
                return "Query plan not available for this provider";
            }
            
            var planBuilder = new StringBuilder();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            while (await reader.ReadAsync(cancellationToken))
            {
                planBuilder.AppendLine(reader.GetString(0));
            }
            
            return planBuilder.ToString();
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
    
    private List<string> AnalyzeQueryPlan(string plan)
    {
        var suggestions = new List<string>();
        
        if (string.IsNullOrEmpty(plan))
            return suggestions;
        
        // Analyze for common issues
        if (plan.Contains("Table Scan", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("Consider adding indexes to avoid table scans");
        }
        
        if (plan.Contains("Sort", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("Consider adding indexes on ORDER BY columns");
        }
        
        if (plan.Contains("Hash Join", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("Consider adding indexes on JOIN columns");
        }
        
        if (plan.Contains("Key Lookup", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("Consider creating covering indexes to avoid key lookups");
        }
        
        return suggestions;
    }
    
    private async Task<List<string>> DetectMissingIndexesAsync(
        DbContext context,
        string sql,
        CancellationToken cancellationToken)
    {
        var missingIndexes = new List<string>();
        
        // This would be provider-specific
        // Example for SQL Server
        var connection = context.Database.GetDbConnection();
        if (connection.GetType().Name.Contains("SqlConnection"))
        {
            await connection.OpenAsync(cancellationToken);
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT TOP 10
                        'CREATE INDEX IX_' + OBJECT_NAME(d.object_id) + '_' + 
                        REPLACE(REPLACE(REPLACE(ISNULL(d.equality_columns,'') + 
                        ISNULL(d.inequality_columns,''), '[', ''), ']', ''), ', ', '_') +
                        ' ON ' + OBJECT_SCHEMA_NAME(d.object_id) + '.' + OBJECT_NAME(d.object_id) +
                        ' (' + ISNULL(d.equality_columns,'') + 
                        CASE WHEN d.equality_columns IS NOT NULL AND d.inequality_columns IS NOT NULL 
                        THEN ',' ELSE '' END + ISNULL(d.inequality_columns, '') + ')' +
                        ISNULL(' INCLUDE (' + d.included_columns + ')', '') AS CreateIndexStatement
                    FROM sys.dm_db_missing_index_details d
                    INNER JOIN sys.dm_db_missing_index_groups g ON d.index_handle = g.index_handle
                    ORDER BY g.index_group_handle DESC";
                
                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    missingIndexes.Add(reader.GetString(0));
                }
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
        
        return missingIndexes;
    }
    
    private double EstimateQueryCost(string plan)
    {
        // Simplified cost estimation
        double cost = 0;
        
        if (plan.Contains("Table Scan")) cost += 100;
        if (plan.Contains("Index Scan")) cost += 50;
        if (plan.Contains("Index Seek")) cost += 10;
        if (plan.Contains("Sort")) cost += 30;
        if (plan.Contains("Hash Join")) cost += 40;
        if (plan.Contains("Nested Loop")) cost += 20;
        
        return cost;
    }
    
    private async Task CreateIndexAsync(DbContext context, string indexSql, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlRawAsync(indexSql, cancellationToken);
    }
    
    private void UpdateStatistics(string queryKey, long executionTime)
    {
        if (!_statistics.TopQueries.TryGetValue(queryKey, out var metrics))
        {
            metrics = new QueryMetrics { Query = queryKey };
            _statistics.TopQueries[queryKey] = metrics;
        }
        
        Interlocked.Increment(ref metrics.ExecutionCount);
        Interlocked.Add(ref metrics.TotalTime, executionTime);
    }
}

// Result classes
public class QueryOptimizationResult
{
    public string OriginalQuery { get; set; }
    public string OptimizedQuery { get; set; }
    public string ExecutionPlan { get; set; }
    public List<string> Suggestions { get; set; } = new();
    public List<string> MissingIndexes { get; set; } = new();
    public double EstimatedCost { get; set; }
    public long AnalysisTime { get; set; }
    public string Error { get; set; }
}

public class IndexCreationResult
{
    public List<string> CreatedIndexes { get; set; } = new();
    public List<FailedIndex> FailedIndexes { get; set; } = new();
    public string Error { get; set; }
}

public class FailedIndex
{
    public string IndexName { get; set; }
    public string Error { get; set; }
}
