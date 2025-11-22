// Phase 2 optimization: Dynamic memory limit adjustment
// Optimizes memory usage in containerized environments

namespace Loco.Core.Memory;

/// <summary>
/// Dynamic Memory Optimizer - Adjusts GC memory limits based on system constraints
/// Phase 2: Critical for container environments with memory limits
///
/// Features:
/// - Automatic memory limit detection
/// - Periodic GC pressure monitoring
/// - Heap size optimization
/// - Container-aware configuration (Docker, Kubernetes)
/// </summary>
public class DynamicMemoryOptimizer
{
    private readonly ILogger<DynamicMemoryOptimizer> _logger;
    private readonly MemoryOptimizerConfig _config;
    private Timer? _optimizationTimer;
    private DateTime _lastOptimization = DateTime.UtcNow;

    public DynamicMemoryOptimizer(
        ILogger<DynamicMemoryOptimizer> logger,
        MemoryOptimizerConfig? config = null)
    {
        _logger = logger;
        _config = config ?? MemoryOptimizerConfig.Default;
    }

    /// <summary>
    /// Start monitoring and optimizing memory
    /// </summary>
    public void Start()
    {
        _logger.LogInformation("Starting DynamicMemoryOptimizer with interval {IntervalSeconds}s",
            _config.OptimizationIntervalSeconds);

        // Set initial memory limit
        AdjustMemoryLimit();

        // Start periodic optimization
        _optimizationTimer = new Timer(
            _ => AdjustMemoryLimit(),
            null,
            TimeSpan.FromSeconds(_config.OptimizationIntervalSeconds),
            TimeSpan.FromSeconds(_config.OptimizationIntervalSeconds));
    }

    /// <summary>
    /// Stop monitoring
    /// </summary>
    public void Stop()
    {
        _optimizationTimer?.Dispose();
        _logger.LogInformation("DynamicMemoryOptimizer stopped");
    }

    /// <summary>
    /// Adjust memory limit based on current system state
    /// </summary>
    public void AdjustMemoryLimit()
    {
        // Prevent too-frequent adjustments
        var timeSinceLastOptimization = DateTime.UtcNow - _lastOptimization;
        if (timeSinceLastOptimization.TotalSeconds < _config.OptimizationIntervalSeconds * 0.5)
            return;

        try
        {
            var memoryInfo = GC.GetGCMemoryInfo();
            var currentHeapSize = memoryInfo.HeapSizeBytes;
            var memoryLimit = GetEffectiveMemoryLimit();

            _logger.LogDebug(
                "Memory check: Heap={HeapMB:F1}MB, Limit={LimitMB:F1}MB, Gen2Count={Gen2Count}",
                currentHeapSize / (1024.0 * 1024),
                memoryLimit / (1024.0 * 1024),
                GC.CollectionCount(2));

            // Calculate target limit (90% of system limit)
            var targetLimit = (long)(memoryLimit * _config.TargetMemoryUsagePercentage);

            // Only adjust if significantly different (reduce thrashing)
            if (Math.Abs(currentHeapSize - targetLimit) > targetLimit * 0.1)
            {
                // Use GC.RefreshMemoryLimit in .NET 6+
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        // Refresh memory limit from cgroup/container constraints
                        GC.RefreshMemoryLimit();
                        _logger.LogInformation("Memory limit refreshed, target: {TargetMB:F1}MB",
                            targetLimit / (1024.0 * 1024));
                    }
                    catch (PlatformNotSupportedException)
                    {
                        _logger.LogDebug("GC.RefreshMemoryLimit not supported on this platform");
                    }
                }

                // Force collection if heap is growing too large
                if (currentHeapSize > targetLimit * 1.2)
                {
                    _logger.LogWarning(
                        "Heap size {HeapMB:F1}MB exceeds target {TargetMB:F1}MB, triggering collection",
                        currentHeapSize / (1024.0 * 1024),
                        targetLimit / (1024.0 * 1024));

                    GC.Collect(2, GCCollectionMode.Optimized);
                    GC.WaitForPendingFinalizers();
                }
            }

            _lastOptimization = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adjusting memory limit");
        }
    }

    /// <summary>
    /// Get effective memory limit (container or system)
    /// </summary>
    private long GetEffectiveMemoryLimit()
    {
        // Try to get container memory limit (Docker/Kubernetes)
        // Method 1: Check cgroup v2 (modern container runtimes)
        var cgroupLimit = TryGetCgroupV2MemoryLimit();
        if (cgroupLimit > 0)
        {
            _logger.LogDebug("Using cgroup v2 memory limit: {LimitMB:F1}MB",
                cgroupLimit / (1024.0 * 1024));
            return cgroupLimit;
        }

        // Method 2: Check cgroup v1
        var cgroupV1Limit = TryGetCgroupV1MemoryLimit();
        if (cgroupV1Limit > 0)
        {
            _logger.LogDebug("Using cgroup v1 memory limit: {LimitMB:F1}MB",
                cgroupV1Limit / (1024.0 * 1024));
            return cgroupV1Limit;
        }

        // Method 3: Check environment variable (explicit configuration)
        if (long.TryParse(Environment.GetEnvironmentVariable("MEMORY_LIMIT"), out var envLimit) &&
            envLimit > 0)
        {
            _logger.LogDebug("Using MEMORY_LIMIT env var: {LimitMB:F1}MB",
                envLimit / (1024.0 * 1024));
            return envLimit;
        }

        // Fallback: Use 75% of total system memory
        var totalMemory = GC.GetGCMemoryInfo().TotalCommittedBytes;
        if (totalMemory > 0)
        {
            var safeLimit = (long)(totalMemory * 0.75);
            _logger.LogDebug("Using system total memory (75%): {LimitMB:F1}MB",
                safeLimit / (1024.0 * 1024));
            return safeLimit;
        }

        // Ultimate fallback: 512MB
        _logger.LogWarning("Could not determine memory limit, using default 512MB");
        return 512 * 1024 * 1024;
    }

    /// <summary>
    /// Try to read memory limit from cgroup v2
    /// </summary>
    private long TryGetCgroupV2MemoryLimit()
    {
        try
        {
            const string cgroupV2Path = "/sys/fs/cgroup/memory.max";
            if (File.Exists(cgroupV2Path))
            {
                var content = File.ReadAllText(cgroupV2Path).Trim();
                if (content == "max")
                    return 0; // Unlimited

                if (long.TryParse(content, out var limit))
                    return limit;
            }
        }
        catch
        {
            // Ignore errors reading cgroup
        }

        return 0;
    }

    /// <summary>
    /// Try to read memory limit from cgroup v1
    /// </summary>
    private long TryGetCgroupV1MemoryLimit()
    {
        try
        {
            const string cgroupV1Path = "/sys/fs/cgroup/memory/memory.limit_in_bytes";
            if (File.Exists(cgroupV1Path))
            {
                var content = File.ReadAllText(cgroupV1Path).Trim();
                if (long.TryParse(content, out var limit) && limit > 0)
                {
                    // Check if it's a physical limit (not a huge number)
                    if (limit < long.MaxValue / 2)
                        return limit;
                }
            }
        }
        catch
        {
            // Ignore errors reading cgroup
        }

        return 0;
    }

    /// <summary>
    /// Get current memory metrics
    /// </summary>
    public MemoryMetrics GetMetrics()
    {
        var memoryInfo = GC.GetGCMemoryInfo();
        var process = System.Diagnostics.Process.GetCurrentProcess();

        return new MemoryMetrics
        {
            HeapSizeBytes = memoryInfo.HeapSizeBytes,
            TotalCommittedBytes = memoryInfo.TotalCommittedBytes,
            WorkingSetBytes = process.WorkingSet64,
            EffectiveLimitBytes = GetEffectiveMemoryLimit(),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Configuration for memory optimizer
/// </summary>
public class MemoryOptimizerConfig
{
    /// <summary>
    /// How often to check and adjust memory (seconds)
    /// </summary>
    public int OptimizationIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Target memory usage as percentage of limit
    /// </summary>
    public double TargetMemoryUsagePercentage { get; set; } = 0.90;

    /// <summary>
    /// Default configuration
    /// </summary>
    public static MemoryOptimizerConfig Default => new();
}

/// <summary>
/// Memory metrics snapshot
/// </summary>
public class MemoryMetrics
{
    public long HeapSizeBytes { get; set; }
    public long TotalCommittedBytes { get; set; }
    public long WorkingSetBytes { get; set; }
    public long EffectiveLimitBytes { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public DateTime Timestamp { get; set; }

    public double HeapSizeMB => HeapSizeBytes / (1024.0 * 1024);
    public double EffectiveLimitMB => EffectiveLimitBytes / (1024.0 * 1024);
    public double MemoryUtilizationPercent =>
        EffectiveLimitBytes > 0 ? (HeapSizeBytes / (double)EffectiveLimitBytes) * 100 : 0;

    public override string ToString()
    {
        return $"Memory: {HeapSizeMB:F1}MB / {EffectiveLimitMB:F1}MB ({MemoryUtilizationPercent:F1}%) | " +
               $"GC: Gen0={Gen0Collections} Gen1={Gen1Collections} Gen2={Gen2Collections}";
    }
}
