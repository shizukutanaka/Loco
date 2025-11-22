// Phase 3: Performance Benchmarking Suite
// Comprehensive performance validation for Phase 2/3 optimizations

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Loco.Core.Performance;

/// <summary>
/// Benchmark result for a single operation
/// </summary>
public class BenchmarkResult
{
    /// <summary>
    /// Benchmark name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Number of iterations
    /// </summary>
    public int Iterations { get; set; }

    /// <summary>
    /// Individual operation timings (ms)
    /// </summary>
    public long[] TimingsMs { get; set; } = Array.Empty<long>();

    /// <summary>
    /// Minimum execution time (ms)
    /// </summary>
    public double MinMs { get; set; }

    /// <summary>
    /// Maximum execution time (ms)
    /// </summary>
    public double MaxMs { get; set; }

    /// <summary>
    /// Average execution time (ms)
    /// </summary>
    public double AvgMs { get; set; }

    /// <summary>
    /// Median execution time (ms)
    /// </summary>
    public double MedianMs { get; set; }

    /// <summary>
    /// 95th percentile (ms)
    /// </summary>
    public double P95Ms { get; set; }

    /// <summary>
    /// 99th percentile (ms)
    /// </summary>
    public double P99Ms { get; set; }

    /// <summary>
    /// Standard deviation (ms)
    /// </summary>
    public double StdDevMs { get; set; }

    /// <summary>
    /// Operations per second
    /// </summary>
    public double OpsPerSecond { get; set; }

    /// <summary>
    /// Benchmark was successful
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Error message if benchmark failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Memory used (MB)
    /// </summary>
    public double MemoryUsedMb { get; set; }
}

/// <summary>
/// Benchmark comparison result
/// </summary>
public class BenchmarkComparison
{
    /// <summary>
    /// Baseline benchmark
    /// </summary>
    public BenchmarkResult Baseline { get; set; } = new();

    /// <summary>
    /// Optimized benchmark
    /// </summary>
    public BenchmarkResult Optimized { get; set; } = new();

    /// <summary>
    /// Performance improvement percentage
    /// Positive = faster, Negative = slower
    /// </summary>
    public double ImprovementPercent { get; set; }

    /// <summary>
    /// Speedup factor (baseline / optimized)
    /// </summary>
    public double SpeedupFactor { get; set; }
}

/// <summary>
/// Performance benchmarking runner
/// </summary>
public class PerformanceBenchmark
{
    private readonly ILogger<PerformanceBenchmark> _logger;

    public PerformanceBenchmark(ILogger<PerformanceBenchmark> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Run synchronous benchmark
    /// </summary>
    public BenchmarkResult Benchmark(
        string name,
        Action action,
        int iterations = 1000,
        int warmupIterations = 100)
    {
        var result = new BenchmarkResult { Name = name, Iterations = iterations };

        try
        {
            // Warmup
            for (int i = 0; i < warmupIterations; i++)
            {
                action();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();

            var timings = new List<long>();
            var memBefore = GC.GetTotalMemory(true);

            for (int i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                action();
                sw.Stop();
                timings.Add(sw.ElapsedMilliseconds);
            }

            var memAfter = GC.GetTotalMemory(false);

            result.TimingsMs = timings.ToArray();
            result.MinMs = timings.Min();
            result.MaxMs = timings.Max();
            result.AvgMs = timings.Average();
            result.MedianMs = timings.OrderBy(x => x).ElementAt(timings.Count / 2);
            result.P95Ms = CalculatePercentile(timings, 0.95);
            result.P99Ms = CalculatePercentile(timings, 0.99);
            result.StdDevMs = CalculateStdDev(timings, result.AvgMs);
            result.OpsPerSecond = 1000.0 / result.AvgMs;
            result.MemoryUsedMb = (memAfter - memBefore) / (1024.0 * 1024.0);
            result.Success = true;

            _logger.LogInformation(
                "Benchmark '{Name}' completed: {Avg:F3}ms avg, {Ops:F0} ops/sec, {Memory:F2}MB",
                name,
                result.AvgMs,
                result.OpsPerSecond,
                result.MemoryUsedMb);

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "Benchmark '{Name}' failed", name);
            return result;
        }
    }

    /// <summary>
    /// Run asynchronous benchmark
    /// </summary>
    public async Task<BenchmarkResult> BenchmarkAsync(
        string name,
        Func<Task> action,
        int iterations = 1000,
        int warmupIterations = 100)
    {
        var result = new BenchmarkResult { Name = name, Iterations = iterations };

        try
        {
            // Warmup
            for (int i = 0; i < warmupIterations; i++)
            {
                await action();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();

            var timings = new List<long>();
            var memBefore = GC.GetTotalMemory(true);

            for (int i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                await action();
                sw.Stop();
                timings.Add(sw.ElapsedMilliseconds);
            }

            var memAfter = GC.GetTotalMemory(false);

            result.TimingsMs = timings.ToArray();
            result.MinMs = timings.Min();
            result.MaxMs = timings.Max();
            result.AvgMs = timings.Average();
            result.MedianMs = timings.OrderBy(x => x).ElementAt(timings.Count / 2);
            result.P95Ms = CalculatePercentile(timings, 0.95);
            result.P99Ms = CalculatePercentile(timings, 0.99);
            result.StdDevMs = CalculateStdDev(timings, result.AvgMs);
            result.OpsPerSecond = 1000.0 / result.AvgMs;
            result.MemoryUsedMb = (memAfter - memBefore) / (1024.0 * 1024.0);
            result.Success = true;

            _logger.LogInformation(
                "Async benchmark '{Name}' completed: {Avg:F3}ms avg, {Ops:F0} ops/sec",
                name,
                result.AvgMs,
                result.OpsPerSecond);

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "Async benchmark '{Name}' failed", name);
            return result;
        }
    }

    /// <summary>
    /// Compare baseline vs optimized implementation
    /// </summary>
    public BenchmarkComparison Compare(
        string name,
        Action baseline,
        Action optimized,
        int iterations = 1000)
    {
        var baselineResult = Benchmark($"{name} (Baseline)", baseline, iterations);
        var optimizedResult = Benchmark($"{name} (Optimized)", optimized, iterations);

        var speedup = baselineResult.AvgMs / optimizedResult.AvgMs;
        var improvement = ((baselineResult.AvgMs - optimizedResult.AvgMs) / baselineResult.AvgMs) * 100;

        return new BenchmarkComparison
        {
            Baseline = baselineResult,
            Optimized = optimizedResult,
            ImprovementPercent = improvement,
            SpeedupFactor = speedup,
        };
    }

    /// <summary>
    /// Run all benchmarks and generate report
    /// </summary>
    public async Task<BenchmarkReport> RunFullSuiteAsync()
    {
        var report = new BenchmarkReport
        {
            RunTime = DateTime.UtcNow,
            MachineName = Environment.MachineName,
            ProcessorCount = Environment.ProcessorCount,
        };

        _logger.LogInformation("Starting comprehensive performance benchmark suite...");

        // JSON Serialization Benchmarks
        var jsonBench = BenchmarkJsonSerialization();
        report.Benchmarks.Add("JSON Serialization", jsonBench);

        // Memory Allocation Benchmarks
        var memBench = BenchmarkMemoryAllocation();
        report.Benchmarks.Add("Memory Allocation", memBench);

        // Collection Operation Benchmarks
        var collBench = BenchmarkCollectionOperations();
        report.Benchmarks.Add("Collection Operations", collBench);

        _logger.LogInformation("Performance benchmark suite completed");
        return report;
    }

    /// <summary>
    /// Benchmark JSON serialization performance
    /// </summary>
    private List<BenchmarkResult> BenchmarkJsonSerialization()
    {
        var results = new List<BenchmarkResult>();

        var testObject = new { Id = "123", Name = "Test", Values = new[] { 1, 2, 3, 4, 5 } };

        // Standard serialization
        results.Add(Benchmark(
            "JSON Serialize (Standard)",
            () => System.Text.Json.JsonSerializer.Serialize(testObject),
            iterations: 5000));

        // Deserialization
        var json = System.Text.Json.JsonSerializer.Serialize(testObject);
        results.Add(Benchmark(
            "JSON Deserialize",
            () => System.Text.Json.JsonSerializer.Deserialize<dynamic>(json),
            iterations: 5000));

        return results;
    }

    /// <summary>
    /// Benchmark memory allocation patterns
    /// </summary>
    private List<BenchmarkResult> BenchmarkMemoryAllocation()
    {
        var results = new List<BenchmarkResult>();

        // Stack allocation
        results.Add(Benchmark(
            "Stack Allocation (Span<T>)",
            () =>
            {
                Span<byte> buffer = stackalloc byte[256];
                _ = buffer.Length;
            },
            iterations: 10000));

        // Heap allocation
        results.Add(Benchmark(
            "Heap Allocation (new byte[])",
            () => { var buffer = new byte[256]; },
            iterations: 10000));

        // Pool allocation
        results.Add(Benchmark(
            "ArrayPool Allocation",
            () =>
            {
                var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(256);
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            },
            iterations: 10000));

        return results;
    }

    /// <summary>
    /// Benchmark collection operations
    /// </summary>
    private List<BenchmarkResult> BenchmarkCollectionOperations()
    {
        var results = new List<BenchmarkResult>();

        var list = Enumerable.Range(0, 100).ToList();
        var dict = list.ToDictionary(x => x);

        // List iteration
        results.Add(Benchmark(
            "List Iteration",
            () => { foreach (var item in list) { _ = item; } },
            iterations: 1000));

        // Dictionary lookup
        results.Add(Benchmark(
            "Dictionary Lookup",
            () => { var value = dict[50]; },
            iterations: 10000));

        // Frozen dictionary lookup
        var frozenDict = System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(dict);
        results.Add(Benchmark(
            "FrozenDictionary Lookup",
            () => { var value = frozenDict[50]; },
            iterations: 10000));

        // LINQ Where + FirstOrDefault
        results.Add(Benchmark(
            "LINQ Where + FirstOrDefault",
            () => { var item = list.Where(x => x == 50).FirstOrDefault(); },
            iterations: 1000));

        return results;
    }

    /// <summary>
    /// Calculate percentile value
    /// </summary>
    private static double CalculatePercentile(IEnumerable<long> values, double percentile)
    {
        var sorted = values.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }

    /// <summary>
    /// Calculate standard deviation
    /// </summary>
    private static double CalculateStdDev(IEnumerable<long> values, double mean)
    {
        var squared = values.Select(x => Math.Pow(x - mean, 2));
        var variance = squared.Average();
        return Math.Sqrt(variance);
    }
}

/// <summary>
/// Comprehensive benchmark report
/// </summary>
public class BenchmarkReport
{
    /// <summary>
    /// Benchmark run timestamp
    /// </summary>
    public DateTime RunTime { get; set; }

    /// <summary>
    /// Machine name
    /// </summary>
    public string MachineName { get; set; } = Environment.MachineName;

    /// <summary>
    /// Number of processors
    /// </summary>
    public int ProcessorCount { get; set; }

    /// <summary>
    /// Grouped benchmark results
    /// </summary>
    public Dictionary<string, List<BenchmarkResult>> Benchmarks { get; set; } = new();

    /// <summary>
    /// Generate summary report
    /// </summary>
    public string GenerateSummary()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("========================================");
        sb.AppendLine("Performance Benchmark Report");
        sb.AppendLine("========================================");
        sb.AppendLine($"Run Time: {RunTime:u}");
        sb.AppendLine($"Machine: {MachineName} ({ProcessorCount} cores)");
        sb.AppendLine();

        foreach (var group in Benchmarks)
        {
            sb.AppendLine($"### {group.Key}");
            sb.AppendLine();

            foreach (var result in group.Value)
            {
                if (result.Success)
                {
                    sb.AppendLine($"- **{result.Name}**: {result.AvgMs:F3}ms avg " +
                        $"(min: {result.MinMs}ms, max: {result.MaxMs}ms, p95: {result.P95Ms:F3}ms) " +
                        $"| {result.OpsPerSecond:F0} ops/sec | Memory: {result.MemoryUsedMb:F2}MB");
                }
                else
                {
                    sb.AppendLine($"- **{result.Name}**: FAILED - {result.Error}");
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
