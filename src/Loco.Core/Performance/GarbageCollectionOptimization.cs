namespace Loco.Core.Performance;

/// <summary>
/// Garbage Collection Optimization for Container Environments
/// Based on multilingual research (French: DATAS, Korean: GC optimization, etc.)
/// 
/// Features:
/// - Dynamic adaptation to application sizes (DATAS)
/// - Tiered JIT compilation
/// - Quick JIT for cold paths
/// - Server GC mode for containers
/// </summary>
public static class GarbageCollectionOptimization
{
    /// <summary>
    /// Environment variable names for GC configuration
    /// These should be set in Docker/Kubernetes environments
    /// </summary>
    public static class EnvironmentVariables
    {
        // GC Server Mode - Use for server applications in containers
        public const string GCServer = "DOTNET_COMPlus_GCServer";
        
        // DATAS (Dynamic Adaptation To Application Sizes) - French research
        // Automatically adapts memory allocation based on application needs
        public const string GCDATASEnableAdaptive = "DOTNET_COMPlus_GCDATASEnableAdaptive";
        
        // Maximum percentage increase for adaptive GC
        public const string GCDATASMaxPercentageIncrease = "DOTNET_COMPlus_GCDATASMaxPercentageIncrease";
        
        // Tiered Compilation - JIT optimizes hot paths
        public const string TieredCompilation = "DOTNET_TieredCompilation";
        
        // Quick JIT for method entry points
        public const string TieredCompilationQuickJit = "DOTNET_TieredCompilation_QuickJit";
        
        // Quick JIT for loops
        public const string TieredCompilationQuickJitForLoops = "DOTNET_TieredCompilation_QuickJitForLoops";
        
        // Heap count - Set to CPU core count for best performance
        public const string GCHeapCount = "DOTNET_COMPlus_GCHeapCount";
        
        // Heap affinity - Pin heaps to specific cores
        public const string GCHeapAffinitizeMode = "DOTNET_COMPlus_GCHeapAffinitizeMode";
    }

    /// <summary>
    /// Recommended configuration for container deployments
    /// Returns a dictionary of environment variables to be set
    /// </summary>
    public static Dictionary<string, string> GetContainerOptimization(
        int cpuCoreCount = -1)
    {
        if (cpuCoreCount <= 0)
        {
            cpuCoreCount = Environment.ProcessorCount;
        }

        return new Dictionary<string, string>
        {
            // Enable server GC mode (better for multi-core containers)
            [EnvironmentVariables.GCServer] = "1",

            // Enable DATAS for dynamic memory adaptation
            [EnvironmentVariables.GCDATASEnableAdaptive] = "1",

            // Allow up to 200% memory increase (adaptive)
            [EnvironmentVariables.GCDATASMaxPercentageIncrease] = "200",

            // Enable tiered compilation for better performance
            [EnvironmentVariables.TieredCompilation] = "1",
            [EnvironmentVariables.TieredCompilationQuickJit] = "1",
            [EnvironmentVariables.TieredCompilationQuickJitForLoops] = "1",

            // Set heap count to CPU core count
            [EnvironmentVariables.GCHeapCount] = cpuCoreCount.ToString(),

            // Affinitize heaps to specific cores
            [EnvironmentVariables.GCHeapAffinitizeMode] = "1",
        };
    }

    /// <summary>
    /// Configuration optimized for high-throughput scenarios
    /// (Many concurrent requests, large working set)
    /// </summary>
    public static Dictionary<string, string> GetHighThroughputOptimization()
    {
        var config = GetContainerOptimization();
        
        // Increase adaptive memory ceiling for high-throughput
        config[EnvironmentVariables.GCDATASMaxPercentageIncrease] = "300";
        
        return config;
    }

    /// <summary>
    /// Configuration optimized for low-latency scenarios
    /// (Real-time requirements, predictable pause times)
    /// </summary>
    public static Dictionary<string, string> GetLowLatencyOptimization()
    {
        var config = GetContainerOptimization();
        
        // More conservative adaptive memory for predictable behavior
        config[EnvironmentVariables.GCDATASMaxPercentageIncrease] = "150";
        
        return config;
    }

    /// <summary>
    /// Apply environment variables at runtime
    /// WARNING: This only works if called before first GC
    /// Better to set via environment or Docker/K8s configuration
    /// </summary>
    public static void ApplyOptimizations(
        Dictionary<string, string> optimizations)
    {
        foreach (var (key, value) in optimizations)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    /// <summary>
    /// Get current GC statistics for monitoring
    /// Useful for health checks and observability
    /// </summary>
    public static GCStatistics GetCurrentStatistics()
    {
        var info = GC.GetGCMemoryInfo();
        
        return new GCStatistics
        {
            TotalMemory = GC.GetTotalMemory(false),
            HeapSize = info.HeapSizeBytes,
            FragmentedBytes = info.FragmentedBytes,
            TotalCommittedBytes = info.TotalCommittedBytes,
            Index = info.Index,
            Generation = info.Generation,
            Pause = info.Pause
        };
    }
}

/// <summary>
/// GC Statistics for monitoring and diagnostics
/// </summary>
public record GCStatistics(
    long TotalMemory,
    long HeapSize,
    long FragmentedBytes,
    long TotalCommittedBytes,
    uint Index,
    int Generation,
    uint Pause);

/// <summary>
/// Extension methods for GC optimization in ASP.NET Core
/// Usage in Program.cs:
/// builder.Services.AddGCOptimization();
/// </summary>
public static class GCOptimizationExtensions
{
    public static IServiceCollection AddGCOptimization(
        this IServiceCollection services,
        GCOptimizationMode mode = GCOptimizationMode.Container)
    {
        var optimizations = mode switch
        {
            GCOptimizationMode.Container => 
                GarbageCollectionOptimization.GetContainerOptimization(),
            GCOptimizationMode.HighThroughput => 
                GarbageCollectionOptimization.GetHighThroughputOptimization(),
            GCOptimizationMode.LowLatency => 
                GarbageCollectionOptimization.GetLowLatencyOptimization(),
            _ => GarbageCollectionOptimization.GetContainerOptimization()
        };

        // Log applied optimizations
        var logger = services.BuildServiceProvider()
            .GetRequiredService<ILogger<GarbageCollectionOptimization>>();
        
        logger.LogInformation(
            "GC Optimizations applied: {@Optimizations}",
            optimizations);

        return services;
    }
}

/// <summary>
/// GC Optimization modes based on workload characteristics
/// </summary>
public enum GCOptimizationMode
{
    /// <summary>Standard container configuration (default)</summary>
    Container = 0,

    /// <summary>High-throughput workloads (many requests, large dataset)</summary>
    HighThroughput = 1,

    /// <summary>Low-latency workloads (real-time requirements)</summary>
    LowLatency = 2
}
