using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Performance;

/// <summary>
/// Proactive memory optimizer for enterprise-grade memory management
/// Monitors GC, triggers optimizations, and prevents memory leaks
/// </summary>
public class MemoryOptimizer : IDisposable
{
    private readonly ILogger? _logger;
    private readonly Timer _monitorTimer;
    private readonly long _memoryLimitBytes;
    private readonly int _warningThresholdPercent = 80;
    private readonly int _criticalThresholdPercent = 90;
    private readonly List<MemorySnapshot> _snapshots;
    private readonly object _lock = new();
    private bool _isOptimizing;

    public MemoryOptimizer(long memoryLimitMB, ILogger? logger = null)
    {
        if (memoryLimitMB <= 0)
            throw new ArgumentOutOfRangeException(nameof(memoryLimitMB));

        _memoryLimitBytes = memoryLimitMB * 1024 * 1024;
        _logger = logger;
        _snapshots = new List<MemorySnapshot>();

        // Enable server GC settings for better performance
        GCSettings.LatencyMode = GCLatencyMode.Batch;

        // Monitor memory every 30 seconds
        _monitorTimer = new Timer(MonitorMemory, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        _logger?.LogInformation("Memory optimizer initialized with {LimitMB}MB limit", memoryLimitMB);
    }

    /// <summary>
    /// Get current memory usage
    /// </summary>
    public MemoryUsage GetCurrentUsage()
    {
        var process = Process.GetCurrentProcess();
        var gcMemory = GC.GetTotalMemory(false);

        return new MemoryUsage
        {
            WorkingSetMB = process.WorkingSet64 / 1024.0 / 1024.0,
            PrivateMemoryMB = process.PrivateMemorySize64 / 1024.0 / 1024.0,
            ManagedMemoryMB = gcMemory / 1024.0 / 1024.0,
            LimitMB = _memoryLimitBytes / 1024.0 / 1024.0,
            UsagePercent = (gcMemory / (double)_memoryLimitBytes) * 100,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };
    }

    /// <summary>
    /// Force garbage collection and memory optimization
    /// </summary>
    public async Task<OptimizationResult> OptimizeAsync()
    {
        lock (_lock)
        {
            if (_isOptimizing)
            {
                _logger?.LogDebug("Optimization already in progress, skipping");
                return new OptimizationResult { Success = false, Reason = "Already optimizing" };
            }
            _isOptimizing = true;
        }

        try
        {
            var beforeUsage = GetCurrentUsage();
            var stopwatch = Stopwatch.StartNew();

            _logger?.LogInformation("Starting memory optimization (Current: {MemoryMB:F1}MB, {Percent:F1}%)",
                beforeUsage.ManagedMemoryMB, beforeUsage.UsagePercent);

            // Compact large object heap
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

            // Force full blocking GC
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

            // Wait for finalization
            GC.WaitForPendingFinalizers();

            // One more collection to clean up finalized objects
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);

            // Small delay to let things settle
            await Task.Delay(100);

            var afterUsage = GetCurrentUsage();
            stopwatch.Stop();

            var freedMB = beforeUsage.ManagedMemoryMB - afterUsage.ManagedMemoryMB;

            _logger?.LogInformation("Memory optimization completed in {Ms}ms: {FreedMB:F1}MB freed (Before: {BeforeMB:F1}MB, After: {AfterMB:F1}MB)",
                stopwatch.ElapsedMilliseconds, freedMB, beforeUsage.ManagedMemoryMB, afterUsage.ManagedMemoryMB);

            return new OptimizationResult
            {
                Success = true,
                FreedMB = freedMB,
                DurationMs = stopwatch.ElapsedMilliseconds,
                BeforeUsageMB = beforeUsage.ManagedMemoryMB,
                AfterUsageMB = afterUsage.ManagedMemoryMB
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during memory optimization");
            return new OptimizationResult { Success = false, Reason = ex.Message };
        }
        finally
        {
            lock (_lock)
            {
                _isOptimizing = false;
            }
        }
    }

    /// <summary>
    /// Get memory usage trend
    /// </summary>
    public MemoryTrend GetTrend(int minutes = 10)
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-minutes);
            var recentSnapshots = _snapshots.FindAll(s => s.Timestamp >= cutoff);

            if (recentSnapshots.Count < 2)
            {
                return new MemoryTrend
                {
                    IsIncreasing = false,
                    RateMBPerMinute = 0,
                    DataPoints = recentSnapshots.Count
                };
            }

            var oldest = recentSnapshots[0];
            var newest = recentSnapshots[^1];
            var timeDiff = (newest.Timestamp - oldest.Timestamp).TotalMinutes;
            var memoryDiff = newest.ManagedMemoryMB - oldest.ManagedMemoryMB;

            return new MemoryTrend
            {
                IsIncreasing = memoryDiff > 0,
                RateMBPerMinute = timeDiff > 0 ? memoryDiff / timeDiff : 0,
                DataPoints = recentSnapshots.Count,
                CurrentMB = newest.ManagedMemoryMB,
                StartMB = oldest.ManagedMemoryMB
            };
        }
    }

    /// <summary>
    /// Check if memory is under pressure
    /// </summary>
    public MemoryPressureLevel GetPressureLevel()
    {
        var usage = GetCurrentUsage();

        if (usage.UsagePercent >= _criticalThresholdPercent)
            return MemoryPressureLevel.Critical;

        if (usage.UsagePercent >= _warningThresholdPercent)
            return MemoryPressureLevel.Warning;

        return MemoryPressureLevel.Normal;
    }

    private void MonitorMemory(object? state)
    {
        try
        {
            var usage = GetCurrentUsage();
            var pressureLevel = GetPressureLevel();

            // Record snapshot
            lock (_lock)
            {
                _snapshots.Add(new MemorySnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    ManagedMemoryMB = usage.ManagedMemoryMB,
                    WorkingSetMB = usage.WorkingSetMB,
                    UsagePercent = usage.UsagePercent
                });

                // Keep only last 100 snapshots (50 minutes of history)
                if (_snapshots.Count > 100)
                {
                    _snapshots.RemoveAt(0);
                }
            }

            // Log warnings and trigger optimization if needed
            switch (pressureLevel)
            {
                case MemoryPressureLevel.Critical:
                    _logger?.LogWarning("CRITICAL memory pressure: {UsageMB:F1}MB ({Percent:F1}%) - Triggering optimization",
                        usage.ManagedMemoryMB, usage.UsagePercent);

                    // Trigger async optimization without waiting - direct Task.Run
                    _ = Task.Run(OptimizeAsync);
                    break;

                case MemoryPressureLevel.Warning:
                    _logger?.LogWarning("Memory pressure warning: {UsageMB:F1}MB ({Percent:F1}%)",
                        usage.ManagedMemoryMB, usage.UsagePercent);

                    // Check trend
                    var trend = GetTrend();
                    if (trend.IsIncreasing && trend.RateMBPerMinute > 1)
                    {
                        _logger?.LogWarning("Memory usage increasing rapidly: {Rate:F2}MB/min - Consider optimization",
                            trend.RateMBPerMinute);
                    }
                    break;

                case MemoryPressureLevel.Normal:
                    _logger?.LogDebug("Memory usage normal: {UsageMB:F1}MB ({Percent:F1}%)",
                        usage.ManagedMemoryMB, usage.UsagePercent);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error monitoring memory");
        }
    }

    public void Dispose()
    {
        _monitorTimer?.Dispose();
    }
}

/// <summary>
/// Current memory usage snapshot
/// </summary>
public class MemoryUsage
{
    public double WorkingSetMB { get; set; }
    public double PrivateMemoryMB { get; set; }
    public double ManagedMemoryMB { get; set; }
    public double LimitMB { get; set; }
    public double UsagePercent { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
}

/// <summary>
/// Result of memory optimization
/// </summary>
public class OptimizationResult
{
    public bool Success { get; set; }
    public double FreedMB { get; set; }
    public long DurationMs { get; set; }
    public double BeforeUsageMB { get; set; }
    public double AfterUsageMB { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Memory usage trend over time
/// </summary>
public class MemoryTrend
{
    public bool IsIncreasing { get; set; }
    public double RateMBPerMinute { get; set; }
    public int DataPoints { get; set; }
    public double CurrentMB { get; set; }
    public double StartMB { get; set; }
}

/// <summary>
/// Memory pressure level
/// </summary>
public enum MemoryPressureLevel
{
    Normal,
    Warning,
    Critical
}

internal class MemorySnapshot
{
    public DateTime Timestamp { get; set; }
    public double ManagedMemoryMB { get; set; }
    public double WorkingSetMB { get; set; }
    public double UsagePercent { get; set; }
}
