using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Caching;
using Loco.Core.Memory;

namespace Loco.Core.Performance;

/// <summary>
/// Performance monitoring and optimization system
/// Following John Carmack's measurement-driven optimization
/// </summary>
public sealed class PerformanceMonitor : IDisposable
{
    private readonly ConcurrentDictionary<string, MetricData> _metrics = new();
    private readonly Timer _reportTimer;
    private readonly ILogger<PerformanceMonitor> _logger;
    private readonly StreamWriter _metricsWriter;
    private long _totalAllocations;
    private long _gcGen0;
    private long _gcGen1;
    private long _gcGen2;
    
    public PerformanceMonitor(ILogger<PerformanceMonitor> logger = null, string metricsFile = null)
    {
        _logger = logger;
        
        if (!string.IsNullOrEmpty(metricsFile))
        {
            var dir = Path.GetDirectoryName(metricsFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            _metricsWriter = new StreamWriter(metricsFile, append: true) { AutoFlush = true };
        }
        
        // Report metrics every minute
        _reportTimer = new Timer(_ => ReportMetrics(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        
        // Initialize GC counters
        _gcGen0 = GC.CollectionCount(0);
        _gcGen1 = GC.CollectionCount(1);
        _gcGen2 = GC.CollectionCount(2);
    }
    
    /// <summary>
    /// Start measuring a named operation
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IDisposable Measure(string operation)
    {
        return new MeasurementScope(this, operation);
    }
    
    /// <summary>
    /// Record a custom metric
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordMetric(string name, double value)
    {
        var data = _metrics.GetOrAdd(name, _ => new MetricData());
        data.Record(value);
    }
    
    /// <summary>
    /// Track memory allocation
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrackAllocation(long bytes)
    {
        Interlocked.Add(ref _totalAllocations, bytes);
    }
    
    /// <summary>
    /// Get current performance statistics
    /// </summary>
    public PerformanceStats GetStats()
    {
        var stats = new PerformanceStats
        {
            TotalAllocations = _totalAllocations,
            WorkingSetMB = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024),
            GCGen0Collections = GC.CollectionCount(0) - _gcGen0,
            GCGen1Collections = GC.CollectionCount(1) - _gcGen1,
            GCGen2Collections = GC.CollectionCount(2) - _gcGen2,
            Metrics = new Dictionary<string, MetricSummary>()
        };
        
        foreach (var kvp in _metrics)
        {
            stats.Metrics[kvp.Key] = kvp.Value.GetSummary();
        }
        
        return stats;
    }
    
    private void ReportMetrics()
    {
        try
        {
            var stats = GetStats();
            var report = FormatReport(stats);
            
            _logger?.LogInformation("Performance Report:\n{Report}", report);
            _metricsWriter?.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]\n{report}");
        }
        catch
        {
            // Metrics reporting should never fail
        }
    }
    
    private static string FormatReport(PerformanceStats stats)
    {
        var sb = StringBuilderPool.Rent();
        
        sb.AppendLine("=== Performance Metrics ===");
        sb.AppendLine($"Memory: {stats.WorkingSetMB} MB");
        sb.AppendLine($"Allocations: {stats.TotalAllocations / (1024 * 1024)} MB");
        sb.AppendLine($"GC Gen0: {stats.GCGen0Collections}, Gen1: {stats.GCGen1Collections}, Gen2: {stats.GCGen2Collections}");
        sb.AppendLine("\nOperation Metrics:");
        
        foreach (var metric in stats.Metrics)
        {
            sb.AppendLine($"  {metric.Key}:");
            sb.AppendLine($"    Count: {metric.Value.Count}");
            sb.AppendLine($"    Avg: {metric.Value.Average:F2}ms");
            sb.AppendLine($"    Min: {metric.Value.Min:F2}ms");
            sb.AppendLine($"    Max: {metric.Value.Max:F2}ms");
            sb.AppendLine($"    P95: {metric.Value.P95:F2}ms");
        }
        
        return StringBuilderPool.GetStringAndReturn(sb);
    }
    
    public void Dispose()
    {
        _reportTimer?.Dispose();
        _metricsWriter?.Dispose();
    }
    
    private sealed class MeasurementScope : IDisposable
    {
        private readonly PerformanceMonitor _monitor;
        private readonly string _operation;
        private readonly Stopwatch _stopwatch;
        private readonly long _startAlloc;
        
        public MeasurementScope(PerformanceMonitor monitor, string operation)
        {
            _monitor = monitor;
            _operation = operation;
            _stopwatch = Stopwatch.StartNew();
            _startAlloc = GC.GetTotalMemory(false);
        }
        
        public void Dispose()
        {
            _stopwatch.Stop();
            var allocBytes = GC.GetTotalMemory(false) - _startAlloc;
            
            _monitor.RecordMetric(_operation, _stopwatch.Elapsed.TotalMilliseconds);
            if (allocBytes > 0)
                _monitor.TrackAllocation(allocBytes);
        }
    }
    
    private sealed class MetricData
    {
        private readonly List<double> _values = new();
        private readonly object _lock = new();
        
        public void Record(double value)
        {
            lock (_lock)
            {
                _values.Add(value);
                
                // Keep only last 1000 values
                if (_values.Count > 1000)
                    _values.RemoveAt(0);
            }
        }
        
        public MetricSummary GetSummary()
        {
            lock (_lock)
            {
                if (_values.Count == 0)
                    return new MetricSummary();
                
                var sorted = new List<double>(_values);
                sorted.Sort();
                
                return new MetricSummary
                {
                    Count = sorted.Count,
                    Min = sorted[0],
                    Max = sorted[^1],
                    Average = CalculateAverage(sorted),
                    P95 = CalculatePercentile(sorted, 0.95)
                };
            }
        }
        
        private static double CalculateAverage(List<double> sorted)
        {
            double sum = 0;
            foreach (var v in sorted)
                sum += v;
            return sum / sorted.Count;
        }
        
        private static double CalculatePercentile(List<double> sorted, double percentile)
        {
            var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
            return sorted[Math.Min(index, sorted.Count - 1)];
        }
    }
}

public sealed class PerformanceStats
{
    public long TotalAllocations { get; set; }
    public long WorkingSetMB { get; set; }
    public int GCGen0Collections { get; set; }
    public int GCGen1Collections { get; set; }
    public int GCGen2Collections { get; set; }
    public Dictionary<string, MetricSummary> Metrics { get; set; }
}

public struct MetricSummary
{
    public int Count { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Average { get; set; }
    public double P95 { get; set; }
}

/// <summary>
/// Performance optimization utilities
/// </summary>
public static class PerformanceOptimizer
{
    private static readonly PerformanceMonitor _globalMonitor = new();
    
    /// <summary>
    /// Global performance monitor instance
    /// </summary>
    public static PerformanceMonitor GlobalMonitor => _globalMonitor;
    
    /// <summary>
    /// Optimize for low latency
    /// </summary>
    public static void OptimizeForLatency()
    {
        // Force GC to generation 2 and compact LOH
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // Set GC to low latency mode
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        
        // Increase thread pool size for better parallelism
        ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
        ThreadPool.SetMinThreads(
            Math.Max(workerThreads, Environment.ProcessorCount * 2),
            Math.Max(completionPortThreads, Environment.ProcessorCount * 2)
        );
    }
    
    /// <summary>
    /// Optimize for throughput
    /// </summary>
    public static void OptimizeForThroughput()
    {
        // Set GC to batch mode for better throughput
        GCSettings.LatencyMode = GCLatencyMode.Batch;
        
        // Set thread pool for throughput
        ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
        ThreadPool.SetMinThreads(
            Math.Max(workerThreads, Environment.ProcessorCount * 4),
            Math.Max(completionPortThreads, Environment.ProcessorCount * 4)
        );
    }
    
    /// <summary>
    /// Warm up JIT compilation
    /// </summary>
    public static async Task WarmUpAsync()
    {
        using (_globalMonitor.Measure("WarmUp"))
        {
            // Pre-JIT common code paths
            var tasks = new List<Task>();
            
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    // Warm up memory pools
                    var pool = new MemoryPool<object>(100);
                    for (int j = 0; j < 100; j++)
                    {
                        var obj = pool.Rent();
                        pool.Return(obj);
                    }
                    
                    // Warm up caches
                    var cache = new FastCache<int, string>(100);
                    for (int j = 0; j < 100; j++)
                    {
                        cache.Set(j, $"value_{j}");
                        cache.TryGet(j, out _);
                    }
                    
                    // Warm up string operations
                    for (int j = 0; j < 100; j++)
                    {
                        var sb = StringBuilderPool.Rent();
                        sb.Append("test");
                        StringBuilderPool.Return(sb);
                    }
                }));
            }
            
            await Task.WhenAll(tasks);
        }
    }
}
