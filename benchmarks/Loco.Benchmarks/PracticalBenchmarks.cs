using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Loco.Core.Practical;

namespace Loco.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class PracticalBenchmarks
{
    private SimpleCache<string> _cache = null!;
    private FastQueue<int> _queue = null!;
    private SimpleMetrics _metrics = null!;
    private SimpleLogger _logger = null!;

    [GlobalSetup]
    public void Setup()
    {
        _cache = new SimpleCache<string>();
        _queue = new FastQueue<int>(1000);
        _metrics = new SimpleMetrics();
        _logger = new SimpleLogger("Benchmark", SimpleLogger.Level.Error); // Only errors

        // Pre-populate cache
        for (int i = 0; i < 100; i++)
        {
            _cache.Set($"key{i}", $"value{i}");
        }
    }

    [Benchmark]
    public string? CacheGet()
    {
        return _cache.Get("key50");
    }

    [Benchmark]
    public void CacheSet()
    {
        _cache.Set("newkey", "newvalue");
    }

    [Benchmark]
    public async Task QueueEnqueue()
    {
        await _queue.EnqueueAsync(42);
    }

    [Benchmark]
    public bool QueueTryDequeue()
    {
        return _queue.TryDequeue(out _);
    }

    [Benchmark]
    public void MetricsIncrement()
    {
        _metrics.IncrementCounter("test");
    }

    [Benchmark]
    public void MetricsRecordTiming()
    {
        _metrics.RecordTiming("test", 123.45);
    }

    [Benchmark]
    public void LogInfo()
    {
        _logger.Info("Test message");
    }

    [Benchmark]
    public async Task<int> RetryExecute()
    {
        return await SimpleRetry.ExecuteAsync(
            async () => await Task.FromResult(42),
            maxAttempts: 1,
            baseDelayMs: 0
        );
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<PracticalBenchmarks>();
    }
}