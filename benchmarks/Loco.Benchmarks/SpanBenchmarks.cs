using System;
using System.Buffers;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace Loco.Benchmarks;

/// <summary>
/// Span&lt;T&gt; と Memory&lt;T&gt; のパフォーマンスベンチマーク
/// 
/// 実行方法:
/// dotnet run -c Release --project benchmarks/Loco.Benchmarks
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class SpanBenchmarks
{
    private byte[] _data = null!;
    private string _jsonString = null!;
    private const int DataSize = 1024 * 10; // 10KB

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[DataSize];
        Random.Shared.NextBytes(_data);
        
        var testObject = new TestData
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test Workflow",
            Value = 12345,
            Timestamp = DateTimeOffset.UtcNow
        };
        _jsonString = JsonSerializer.Serialize(testObject);
    }

    [Benchmark(Baseline = true)]
    public string TraditionalArrayCopy()
    {
        var buffer = new byte[DataSize];
        Array.Copy(_data, buffer, DataSize);
        return System.Text.Encoding.UTF8.GetString(buffer);
    }

    [Benchmark]
    public string SpanCopy()
    {
        Span<byte> buffer = stackalloc byte[DataSize];
        _data.AsSpan().CopyTo(buffer);
        return System.Text.Encoding.UTF8.GetString(buffer);
    }

    [Benchmark]
    public string ArrayPoolCopy()
    {
        var pool = ArrayPool<byte>.Shared;
        var buffer = pool.Rent(DataSize);
        try
        {
            _data.AsSpan().CopyTo(buffer);
            return System.Text.Encoding.UTF8.GetString(buffer, 0, DataSize);
        }
        finally
        {
            pool.Return(buffer);
        }
    }

    [Benchmark]
    public TestData? TraditionalJsonDeserialization()
    {
        return JsonSerializer.Deserialize<TestData>(_jsonString);
    }

    [Benchmark]
    public TestData? SpanJsonDeserialization()
    {
        var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(_jsonString);
        return JsonSerializer.Deserialize<TestData>(utf8Bytes.AsSpan());
    }
}

public class TestData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Value { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<SpanBenchmarks>();
        Console.WriteLine(summary);
    }
}
