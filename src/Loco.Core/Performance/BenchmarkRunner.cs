// Phase 3: Benchmark Runner
// Console application for running comprehensive performance benchmarks

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json;
using System.IO;

namespace Loco.Core.Performance;

/// <summary>
/// Main benchmark runner for performance validation
/// </summary>
public class BenchmarkRunner
{
    private readonly PerformanceBenchmark _benchmark;
    private readonly ILogger<BenchmarkRunner> _logger;

    public BenchmarkRunner(ILogger<BenchmarkRunner> logger)
    {
        _logger = logger;
        _benchmark = new PerformanceBenchmark(logger);
    }

    /// <summary>
    /// Run specific Phase 2/3 optimization benchmarks
    /// </summary>
    public async Task RunOptimizationBenchmarksAsync()
    {
        _logger.LogInformation("Starting Phase 2/3 Optimization Benchmarks");
        var report = new BenchmarkReport { RunTime = DateTime.UtcNow };

        // JSON Serialization Optimization
        _logger.LogInformation("Benchmarking JSON serialization optimizations...");
        var jsonResults = new List<BenchmarkResult>();

        // Standard JSON
        jsonResults.Add(_benchmark.Benchmark(
            "JSON Serialize (Standard 16KB buffer)",
            () =>
            {
                var options = new JsonSerializerOptions { DefaultBufferSize = 16384 };
                var obj = new { Id = "123", Name = "Test", Data = Guid.NewGuid().ToString() };
                _ = JsonSerializer.Serialize(obj, options);
            },
            iterations: 5000));

        // Optimized JSON (Phase 2)
        jsonResults.Add(_benchmark.Benchmark(
            "JSON Serialize (Optimized 4KB buffer)",
            () =>
            {
                var options = new JsonSerializerOptions
                {
                    DefaultBufferSize = 4096,
                    WriteIndented = false,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
                var obj = new { Id = "123", Name = "Test", Data = Guid.NewGuid().ToString() };
                _ = JsonSerializer.Serialize(obj, options);
            },
            iterations: 5000));

        report.Benchmarks.Add("JSON Serialization", jsonResults);

        // Memory Allocation Optimization
        _logger.LogInformation("Benchmarking memory allocation optimizations...");
        var memResults = new List<BenchmarkResult>();

        // Heap allocation
        memResults.Add(_benchmark.Benchmark(
            "Heap Allocation (new byte[4096])",
            () => { var buffer = new byte[4096]; _ = buffer.Length; },
            iterations: 10000));

        // Stack allocation (Span<T>)
        memResults.Add(_benchmark.Benchmark(
            "Stack Allocation (stackalloc byte[4096])",
            () =>
            {
                Span<byte> buffer = stackalloc byte[4096];
                _ = buffer.Length;
            },
            iterations: 10000));

        report.Benchmarks.Add("Memory Allocation", memResults);

        // Query Optimization (NoTracking)
        _logger.LogInformation("Benchmarking query optimizations...");
        var queryResults = new List<BenchmarkResult>();

        var testData = Enumerable.Range(0, 1000).Select(i => new { Id = i, Name = $"Item{i}" }).ToList();

        // Tracking iteration
        queryResults.Add(_benchmark.Benchmark(
            "Iteration (With Tracking)",
            () => { foreach (var item in testData) { _ = item.Id; } },
            iterations: 1000));

        // NoTracking equivalent
        queryResults.Add(_benchmark.Benchmark(
            "Iteration (NoTracking equivalent)",
            () =>
            {
                // Simulate readonly iteration
                var items = testData.AsEnumerable();
                foreach (var item in items) { _ = item.Id; }
            },
            iterations: 1000));

        report.Benchmarks.Add("Query Optimization", queryResults);

        // Collection Performance (FrozenDictionary)
        _logger.LogInformation("Benchmarking collection optimizations...");
        var collResults = new List<BenchmarkResult>();

        var dict = Enumerable.Range(0, 10000).ToDictionary(x => x.ToString());

        // Regular Dictionary
        collResults.Add(_benchmark.Benchmark(
            "Dictionary<string, int> Lookup (10k entries)",
            () => { var value = dict.TryGetValue("5000", out _); },
            iterations: 100000));

        // FrozenDictionary
        var frozenDict = System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(dict);
        collResults.Add(_benchmark.Benchmark(
            "FrozenDictionary<string, int> Lookup (10k entries)",
            () => { var value = frozenDict.TryGetValue("5000", out _); },
            iterations: 100000));

        report.Benchmarks.Add("Collection Performance", collResults);

        // Async Operation Performance
        _logger.LogInformation("Benchmarking async operations...");
        var asyncResults = new List<BenchmarkResult>();

        asyncResults.Add(await _benchmark.BenchmarkAsync(
            "Async Task Creation (no actual async work)",
            async () => await Task.Delay(0),
            iterations: 5000));

        asyncResults.Add(await _benchmark.BenchmarkAsync(
            "Async Task with ValueTask",
            async () => await new ValueTask(Task.Delay(0)),
            iterations: 5000));

        report.Benchmarks.Add("Async Operations", asyncResults);

        // Print Report
        var summary = report.GenerateSummary();
        _logger.LogInformation(summary);

        // Save to file
        SaveReportToFile(report);
    }

    /// <summary>
    /// Run WCAG accessibility compliance benchmarks
    /// </summary>
    public void RunAccessibilityBenchmarks()
    {
        _logger.LogInformation("Starting WCAG Accessibility Compliance Benchmarks");
        var report = new BenchmarkReport { RunTime = DateTime.UtcNow };

        var results = new List<BenchmarkResult>();

        // ID generation performance
        results.Add(_benchmark.Benchmark(
            "Unique ID Generation",
            () => { var id = Guid.NewGuid().ToString(); },
            iterations: 100000));

        // ARIA attribute management
        results.Add(_benchmark.Benchmark(
            "ARIA Attribute Lookup",
            () =>
            {
                var dict = new Dictionary<string, string>
                {
                    { "aria-label", "Test" },
                    { "aria-describedby", "desc-1" },
                    { "aria-invalid", "false" }
                };
                _ = dict.TryGetValue("aria-label", out _);
            },
            iterations: 100000));

        report.Benchmarks.Add("WCAG Compliance", results);
        _logger.LogInformation(report.GenerateSummary());
    }

    /// <summary>
    /// Compare performance before/after optimization
    /// </summary>
    public BenchmarkComparison ComparePhase2Optimization()
    {
        _logger.LogInformation("Comparing Phase 2 JSON Optimization Impact");

        var comparison = _benchmark.Compare(
            "JSON Serialization",
            baseline: () =>
            {
                var options = new JsonSerializerOptions { DefaultBufferSize = 16384, WriteIndented = true };
                var obj = new { Id = "123", Name = "Test", Data = Guid.NewGuid().ToString() };
                _ = JsonSerializer.Serialize(obj, options);
            },
            optimized: () =>
            {
                var options = new JsonSerializerOptions
                {
                    DefaultBufferSize = 4096,
                    WriteIndented = false,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
                var obj = new { Id = "123", Name = "Test", Data = Guid.NewGuid().ToString() };
                _ = JsonSerializer.Serialize(obj, options);
            },
            iterations: 5000);

        _logger.LogInformation(
            "JSON Optimization: {Improvement:F1}% improvement ({Speedup:F2}x faster)",
            comparison.ImprovementPercent,
            comparison.SpeedupFactor);

        return comparison;
    }

    /// <summary>
    /// Save benchmark report to JSON file
    /// </summary>
    private void SaveReportToFile(BenchmarkReport report)
    {
        try
        {
            var filename = $"benchmark-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.json";
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filename, json);

            _logger.LogInformation("Benchmark report saved to: {Filename}", Path.GetFullPath(filename));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving benchmark report");
        }
    }

    /// <summary>
    /// Run comprehensive performance validation
    /// </summary>
    public async Task<BenchmarkReport> RunFullValidationAsync()
    {
        _logger.LogInformation("========================================");
        _logger.LogInformation("Starting Comprehensive Performance Validation");
        _logger.LogInformation("========================================");

        var stopwatch = Stopwatch.StartNew();

        // Run optimization benchmarks
        await RunOptimizationBenchmarksAsync();

        // Run accessibility benchmarks
        RunAccessibilityBenchmarks();

        // Run full suite
        var fullReport = await _benchmark.RunFullSuiteAsync();

        stopwatch.Stop();
        _logger.LogInformation(
            "Performance validation completed in {Elapsed}ms",
            stopwatch.ElapsedMilliseconds);

        return fullReport;
    }
}

/// <summary>
/// Benchmark result with performance metrics
/// </summary>
public class PerformanceMetrics
{
    /// <summary>
    /// Operation name
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Baseline time (ms)
    /// </summary>
    public double BaselineMs { get; set; }

    /// <summary>
    /// Current time (ms)
    /// </summary>
    public double CurrentMs { get; set; }

    /// <summary>
    /// Improvement percentage
    /// </summary>
    public double ImprovementPercent => ((BaselineMs - CurrentMs) / BaselineMs) * 100;

    /// <summary>
    /// Speedup factor
    /// </summary>
    public double SpeedupFactor => BaselineMs / CurrentMs;
}

/// <summary>
/// Performance threshold validation
/// </summary>
public class PerformanceValidation
{
    /// <summary>
    /// Minimum acceptable ops/sec
    /// </summary>
    public double MinOpsPerSecond { get; set; } = 100;

    /// <summary>
    /// Maximum acceptable latency (ms)
    /// </summary>
    public double MaxLatencyMs { get; set; } = 100;

    /// <summary>
    /// Maximum acceptable memory (MB)
    /// </summary>
    public double MaxMemoryMb { get; set; } = 500;

    /// <summary>
    /// Validate benchmark result against thresholds
    /// </summary>
    public bool Validate(BenchmarkResult result)
    {
        var opsPerSecValid = result.OpsPerSecond >= MinOpsPerSecond;
        var latencyValid = result.AvgMs <= MaxLatencyMs;
        var memoryValid = result.MemoryUsedMb <= MaxMemoryMb;

        return opsPerSecValid && latencyValid && memoryValid;
    }
}
