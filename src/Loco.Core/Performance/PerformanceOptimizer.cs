using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Performance;

/// <summary>
/// High-performance optimization service
/// Implements Carmack's principle: measure everything, optimize critical paths
/// </summary>
public class PerformanceOptimizer : IDisposable
{
    private readonly ILogger<PerformanceOptimizer> _logger;
    private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics;
    private readonly ArrayPool<byte> _arrayPool;
    private readonly Timer _gcTimer;
    private readonly SemaphoreSlim _optimizationLock;
    private long _totalOptimizations;
    private bool _disposed;

    public PerformanceOptimizer(ILogger<PerformanceOptimizer> logger)
    {
        _logger = logger;
        _metrics = new ConcurrentDictionary<string, PerformanceMetric>();
        _arrayPool = ArrayPool<byte>.Create();
        _optimizationLock = new SemaphoreSlim(1, 1);
        
        // Periodic GC optimization
        _gcTimer = new Timer(_ => OptimizeGarbageCollection(), null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Start measuring performance for a code block
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PerformanceScope MeasureScope(string name, [CallerMemberName] string caller = "")
    {
        return new PerformanceScope(this, $"{caller}.{name}");
    }

    /// <summary>
    /// Optimize memory allocation patterns
    /// </summary>
    public async Task OptimizeMemoryAsync()
    {
        await _optimizationLock.WaitAsync();
        try
        {
            var before = GC.GetTotalMemory(false);
            
            // Force full GC collection
            GC.Collect(2, GCCollectionMode.Optimized, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Optimized, true, true);
            
            var after = GC.GetTotalMemory(false);
            var freed = before - after;
            
            if (freed > 0)
            {
                _logger.LogDebug("Memory optimized: {Freed:N0} bytes freed", freed);
            }
            
            // Compact large object heap
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            
            Interlocked.Increment(ref _totalOptimizations);
        }
        finally
        {
            _optimizationLock.Release();
        }
    }

    /// <summary>
    /// Optimize CPU-bound operations
    /// </summary>
    public void OptimizeCpuOperations()
    {
        // Set thread priority for performance-critical operations
        Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
        
        // Configure thread pool for optimal performance
        ThreadPool.GetMinThreads(out var minWorker, out var minIO);
        ThreadPool.GetMaxThreads(out var maxWorker, out var maxIO);
        
        var processorCount = Environment.ProcessorCount;
        var optimalWorkerThreads = processorCount * 2;
        var optimalIOThreads = processorCount * 4;
        
        if (minWorker < optimalWorkerThreads)
        {
            ThreadPool.SetMinThreads(optimalWorkerThreads, optimalIOThreads);
            _logger.LogDebug("Thread pool optimized: {Workers} worker threads, {IO} I/O threads", 
                optimalWorkerThreads, optimalIOThreads);
        }
    }

    /// <summary>
    /// Get or rent a byte array from pool
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] RentBuffer(int minimumSize)
    {
        return _arrayPool.Rent(minimumSize);
    }

    /// <summary>
    /// Return a byte array to pool
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReturnBuffer(byte[] buffer, bool clearArray = false)
    {
        if (buffer != null)
        {
            _arrayPool.Return(buffer, clearArray);
        }
    }

    /// <summary>
    /// Execute action with optimized context
    /// </summary>
    public async Task<T> ExecuteOptimizedAsync<T>(Func<Task<T>> action, OptimizationOptions options = null)
    {
        options ??= OptimizationOptions.Default;
        
        var originalPriority = Thread.CurrentThread.Priority;
        
        try
        {
            if (options.HighPriority)
            {
                Thread.CurrentThread.Priority = ThreadPriority.Highest;
            }
            
            if (options.PreAllocateMemory > 0)
            {
                // Pre-allocate memory to avoid fragmentation
                var buffer = RentBuffer(options.PreAllocateMemory);
                try
                {
                    return await action();
                }
                finally
                {
                    ReturnBuffer(buffer);
                }
            }
            
            return await action();
        }
        finally
        {
            Thread.CurrentThread.Priority = originalPriority;
        }
    }

    /// <summary>
    /// Batch operations for better performance
    /// </summary>
    public async Task<List<TResult>> BatchExecuteAsync<TInput, TResult>(
        IEnumerable<TInput> items,
        Func<TInput, Task<TResult>> operation,
        int batchSize = 100)
    {
        var results = new ConcurrentBag<TResult>();
        var batches = items.Chunk(batchSize);
        
        await Parallel.ForEachAsync(batches, async (batch, ct) =>
        {
            foreach (var item in batch)
            {
                var result = await operation(item);
                results.Add(result);
            }
        });
        
        return results.ToList();
    }

    /// <summary>
    /// Cache frequently accessed data
    /// </summary>
    public T GetOrCompute<T>(string key, Func<T> factory, TimeSpan? expiration = null)
    {
        var metric = _metrics.GetOrAdd(key, k => new PerformanceMetric(k));
        
        if (metric.CachedValue != null && metric.CacheExpiration > DateTime.UtcNow)
        {
            metric.CacheHits++;
            return (T)metric.CachedValue;
        }
        
        metric.CacheMisses++;
        var value = factory();
        metric.CachedValue = value;
        metric.CacheExpiration = DateTime.UtcNow + (expiration ?? TimeSpan.FromMinutes(5));
        
        return value;
    }

    /// <summary>
    /// Get performance statistics
    /// </summary>
    public PerformanceStatistics GetStatistics()
    {
        var stats = new PerformanceStatistics
        {
            TotalOptimizations = Interlocked.Read(ref _totalOptimizations),
            MemoryUsageMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            ThreadCount = Process.GetCurrentProcess().Threads.Count,
            CpuUsagePercent = GetCpuUsage()
        };
        
        foreach (var metric in _metrics.Values)
        {
            stats.Metrics.Add(new MetricSummary
            {
                Name = metric.Name,
                TotalCalls = metric.TotalCalls,
                TotalDurationMs = metric.TotalDuration.TotalMilliseconds,
                AverageDurationMs = metric.TotalCalls > 0 
                    ? metric.TotalDuration.TotalMilliseconds / metric.TotalCalls 
                    : 0,
                CacheHitRate = metric.CacheHits + metric.CacheMisses > 0
                    ? (double)metric.CacheHits / (metric.CacheHits + metric.CacheMisses)
                    : 0
            });
        }
        
        return stats;
    }

    /// <summary>
    /// Analyze and suggest optimizations
    /// </summary>
    public List<OptimizationSuggestion> AnalyzePerformance()
    {
        var suggestions = new List<OptimizationSuggestion>();
        var stats = GetStatistics();
        
        // Memory suggestions
        if (stats.MemoryUsageMB > 500)
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Type = OptimizationType.Memory,
                Priority = Priority.High,
                Description = "High memory usage detected",
                Action = "Consider increasing GC frequency or reducing object allocations"
            });
        }
        
        if (stats.Gen2Collections > 10)
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Type = OptimizationType.GarbageCollection,
                Priority = Priority.Medium,
                Description = "Frequent Gen2 collections detected",
                Action = "Review object lifecycle and consider object pooling"
            });
        }
        
        // CPU suggestions
        if (stats.CpuUsagePercent > 80)
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Type = OptimizationType.CPU,
                Priority = Priority.High,
                Description = "High CPU usage detected",
                Action = "Profile code to identify hot paths and optimize algorithms"
            });
        }
        
        // Cache suggestions
        foreach (var metric in stats.Metrics.Where(m => m.CacheHitRate < 0.7))
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Type = OptimizationType.Cache,
                Priority = Priority.Low,
                Description = $"Low cache hit rate for {metric.Name}",
                Action = "Consider adjusting cache expiration or preloading data"
            });
        }
        
        return suggestions;
    }

    private void OptimizeGarbageCollection()
    {
        try
        {
            // Optimize GC settings based on current performance
            var memoryInfo = GC.GetMemoryInfo();
            
            if (memoryInfo.HeapSizeBytes > 100_000_000) // > 100MB
            {
                // Switch to server GC for better throughput
                if (!GCSettings.IsServerGC)
                {
                    _logger.LogInformation("Switching to server GC mode for better performance");
                }
                
                // Enable background GC
                GCSettings.LatencyMode = GCLatencyMode.Interactive;
            }
            else
            {
                // Use workstation GC for lower memory footprint
                GCSettings.LatencyMode = GCLatencyMode.LowLatency;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize garbage collection");
        }
    }

    private double GetCpuUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var startTime = DateTime.UtcNow;
            var startCpuUsage = process.TotalProcessorTime;
            
            Thread.Sleep(100);
            
            var endTime = DateTime.UtcNow;
            var endCpuUsage = process.TotalProcessorTime;
            
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            
            return Math.Round(cpuUsageTotal * 100, 2);
        }
        catch
        {
            return 0;
        }
    }

    internal void RecordMetric(string name, TimeSpan duration)
    {
        var metric = _metrics.GetOrAdd(name, k => new PerformanceMetric(k));
        metric.Record(duration);
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _gcTimer?.Dispose();
        _optimizationLock?.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Performance measurement scope
    /// </summary>
    public sealed class PerformanceScope : IDisposable
    {
        private readonly PerformanceOptimizer _optimizer;
        private readonly string _name;
        private readonly Stopwatch _stopwatch;

        public PerformanceScope(PerformanceOptimizer optimizer, string name)
        {
            _optimizer = optimizer;
            _name = name;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _optimizer.RecordMetric(_name, _stopwatch.Elapsed);
        }
    }

    private class PerformanceMetric
    {
        public string Name { get; }
        public long TotalCalls { get; private set; }
        public TimeSpan TotalDuration { get; private set; }
        public object CachedValue { get; set; }
        public DateTime CacheExpiration { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }

        public PerformanceMetric(string name)
        {
            Name = name;
        }

        public void Record(TimeSpan duration)
        {
            Interlocked.Increment(ref TotalCalls);
            TotalDuration += duration;
        }
    }
}

/// <summary>
/// Optimization options
/// </summary>
public class OptimizationOptions
{
    public bool HighPriority { get; set; }
    public int PreAllocateMemory { get; set; }
    public bool DisableGC { get; set; }
    
    public static OptimizationOptions Default => new();
    
    public static OptimizationOptions HighPerformance => new()
    {
        HighPriority = true,
        PreAllocateMemory = 1024 * 1024, // 1MB
        DisableGC = false
    };
}

/// <summary>
/// Performance statistics
/// </summary>
public class PerformanceStatistics
{
    public long TotalOptimizations { get; set; }
    public double MemoryUsageMB { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public int ThreadCount { get; set; }
    public double CpuUsagePercent { get; set; }
    public List<MetricSummary> Metrics { get; set; } = new();
}

/// <summary>
/// Metric summary
/// </summary>
public class MetricSummary
{
    public string Name { get; set; }
    public long TotalCalls { get; set; }
    public double TotalDurationMs { get; set; }
    public double AverageDurationMs { get; set; }
    public double CacheHitRate { get; set; }
}

/// <summary>
/// Optimization suggestion
/// </summary>
public class OptimizationSuggestion
{
    public OptimizationType Type { get; set; }
    public Priority Priority { get; set; }
    public string Description { get; set; }
    public string Action { get; set; }
}

public enum OptimizationType
{
    Memory,
    CPU,
    GarbageCollection,
    Cache,
    Threading,
    IO
}

public enum Priority
{
    Low,
    Medium,
    High,
    Critical
}