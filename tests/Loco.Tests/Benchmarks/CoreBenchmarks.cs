using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Loco.Core;
using Loco.Core.Caching;
using Loco.Core.Memory;
using Loco.Core.Models;
using Loco.Core.NaturalLanguage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loco.Tests.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class CoreBenchmarks
{
    private FlowEngine _flowEngine;
    private FastCache<string, string> _cache;
    private MemoryPool<TestObject> _memoryPool;
    private FastNaturalLanguageProcessor _nlpProcessor;
    private SimpleFlow _simpleFlow;
    private ComplexFlow _complexFlow;
    
    [GlobalSetup]
    public void Setup()
    {
        _flowEngine = new FlowEngine(NullLogger<FlowEngine>.Instance);
        _cache = new FastCache<string, string>(1000);
        _memoryPool = new MemoryPool<TestObject>(100);
        _nlpProcessor = new FastNaturalLanguageProcessor();
        
        _simpleFlow = new SimpleFlow();
        _complexFlow = new ComplexFlow();
        
        // Pre-populate cache
        for (int i = 0; i < 100; i++)
        {
            _cache.Set($"key{i}", $"value{i}");
        }
        
        // Register flows
        _flowEngine.RegisterFlowAsync(_simpleFlow).Wait();
        _flowEngine.RegisterFlowAsync(_complexFlow).Wait();
    }
    
    [Benchmark]
    public async Task ExecuteSimpleFlow()
    {
        await _flowEngine.ExecuteFlowAsync(_simpleFlow.Id, new FlowContext());
    }
    
    [Benchmark]
    public async Task ExecuteComplexFlow()
    {
        await _flowEngine.ExecuteFlowAsync(_complexFlow.Id, new FlowContext());
    }
    
    [Benchmark]
    public void CacheHit()
    {
        _cache.TryGet("key50", out _);
    }
    
    [Benchmark]
    public void CacheMiss()
    {
        _cache.TryGet("nonexistent", out _);
    }
    
    [Benchmark]
    public void CacheSetAndGet()
    {
        var key = Guid.NewGuid().ToString();
        _cache.Set(key, "value");
        _cache.TryGet(key, out _);
    }
    
    [Benchmark]
    public void MemoryPoolRentReturn()
    {
        var obj = _memoryPool.Rent();
        obj.Value = 42;
        _memoryPool.Return(obj);
    }
    
    [Benchmark]
    public void StringBuilderPoolUsage()
    {
        var sb = StringBuilderPool.Rent();
        sb.Append("Hello");
        sb.Append(" ");
        sb.Append("World");
        var result = StringBuilderPool.GetStringAndReturn(sb);
    }
    
    [Benchmark]
    public void BufferPoolUsage()
    {
        var buffer = BufferPool.Rent(1024);
        // Simulate some work
        for (int i = 0; i < Math.Min(100, buffer.Length); i++)
        {
            buffer[i] = (byte)(i % 256);
        }
        BufferPool.Return(buffer);
    }
    
    [Benchmark]
    public async Task NaturalLanguageProcessing()
    {
        await _nlpProcessor.ProcessAsync("remind me every morning at 7am to exercise");
    }
    
    [Benchmark]
    public async Task NaturalLanguageProcessingCached()
    {
        // This should hit the cache after first run
        await _nlpProcessor.ProcessAsync("open chrome and navigate to google");
    }
    
    private class TestObject
    {
        public int Value { get; set; }
        public string Data { get; set; }
    }
    
    private class SimpleFlow : IFlow
    {
        public string Id => "simple_flow";
        public string Name => "Simple Flow";
        
        public Task ExecuteAsync(FlowContext context, CancellationToken cancellationToken = default)
        {
            // Simulate simple flow execution
            context.Variables["result"] = "completed";
            return Task.CompletedTask;
        }
        
        public Task<RuleValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RuleValidationResult { IsValid = true });
        }
    }
    
    private class ComplexFlow : IFlow
    {
        public string Id => "complex_flow";
        public string Name => "Complex Flow";
        
        public async Task ExecuteAsync(FlowContext context, CancellationToken cancellationToken = default)
        {
            // Simulate complex flow with multiple steps
            for (int i = 0; i < 5; i++)
            {
                await Task.Yield();
                context.Variables[$"step{i}"] = i * 2;
            }
            
            // Simulate some computation
            var sum = 0;
            for (int i = 0; i < 100; i++)
            {
                sum += i;
            }
            context.Variables["sum"] = sum;
        }
        
        public Task<RuleValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RuleValidationResult { IsValid = true });
        }
    }
}

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class AllocationBenchmarks
{
    private readonly List<object> _keepAlive = new();
    
    [Benchmark(Baseline = true)]
    public void AllocateWithoutPool()
    {
        var objects = new List<TestObject>(100);
        for (int i = 0; i < 100; i++)
        {
            objects.Add(new TestObject { Value = i });
        }
        _keepAlive.Add(objects);
    }
    
    [Benchmark]
    public void AllocateWithPool()
    {
        var pool = new MemoryPool<TestObject>(100);
        var objects = new List<TestObject>(100);
        
        for (int i = 0; i < 100; i++)
        {
            var obj = pool.Rent();
            obj.Value = i;
            objects.Add(obj);
        }
        
        foreach (var obj in objects)
        {
            pool.Return(obj);
        }
        
        _keepAlive.Add(objects);
    }
    
    [Benchmark]
    public void StringConcatenation()
    {
        string result = "";
        for (int i = 0; i < 100; i++)
        {
            result += i.ToString();
        }
        _keepAlive.Add(result);
    }
    
    [Benchmark]
    public void StringBuilderWithoutPool()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 100; i++)
        {
            sb.Append(i);
        }
        var result = sb.ToString();
        _keepAlive.Add(result);
    }
    
    [Benchmark]
    public void StringBuilderWithPool()
    {
        var sb = StringBuilderPool.Rent();
        for (int i = 0; i < 100; i++)
        {
            sb.Append(i);
        }
        var result = StringBuilderPool.GetStringAndReturn(sb);
        _keepAlive.Add(result);
    }
    
    private class TestObject
    {
        public int Value { get; set; }
        public string Data { get; set; } = "";
    }
}

public class BenchmarkRunner
{
    public static void Main(string[] args)
    {
        var summary1 = BenchmarkRunner.Run<CoreBenchmarks>();
        var summary2 = BenchmarkRunner.Run<AllocationBenchmarks>();
    }
}
