using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Loco.Core.Performance;

namespace Loco.Core.Tests.Performance
{
    public class PerformanceOptimizerTests
    {
        private readonly PerformanceOptimizer _optimizer;
        private readonly Mock<ILogger<PerformanceOptimizer>> _loggerMock;
        private readonly IMemoryCache _cache;

        public PerformanceOptimizerTests()
        {
            _loggerMock = new Mock<ILogger<PerformanceOptimizer>>();
            _cache = new MemoryCache(new MemoryCacheOptions());
            _optimizer = new PerformanceOptimizer(_loggerMock.Object, _cache);
        }

        [Fact]
        public async Task GetOrCreateAsync_CachesValue()
        {
            var key = "test-key";
            var callCount = 0;
            Func<Task<string>> factory = async () =>
            {
                callCount++;
                await Task.Delay(10);
                return "cached-value";
            };

            // First call should execute factory
            var result1 = await _optimizer.GetOrCreateAsync(key, factory);
            Assert.Equal("cached-value", result1);
            Assert.Equal(1, callCount);

            // Second call should return cached value
            var result2 = await _optimizer.GetOrCreateAsync(key, factory);
            Assert.Equal("cached-value", result2);
            Assert.Equal(1, callCount); // Factory not called again
        }

        [Fact]
        public async Task GetOrCreateAsync_PreventsCacheStampede()
        {
            var key = "stampede-test";
            var callCount = 0;
            var factory = new Func<Task<string>>(async () =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(100);
                return "value";
            });

            // Start multiple concurrent requests
            var tasks = Enumerable.Range(0, 10)
                .Select(_ => _optimizer.GetOrCreateAsync(key, factory))
                .ToList();

            var results = await Task.WhenAll(tasks);

            // All should get same value
            Assert.All(results, r => Assert.Equal("value", r));
            // Factory should only be called once
            Assert.Equal(1, callCount);
        }

        [Fact]
        public async Task ProcessBatchAsync_ProcessesAllItems()
        {
            var items = Enumerable.Range(1, 100).ToList();
            var processedItems = new List<int>();

            var results = await _optimizer.ProcessBatchAsync(
                items,
                async item =>
                {
                    await Task.Delay(1);
                    return item * 2;
                },
                batchSize: 10,
                maxConcurrency: 5);

            Assert.Equal(100, results.Count);
            Assert.All(Enumerable.Range(1, 100), i => Assert.Contains(i * 2, results));
        }

        [Fact]
        public void GetFromPool_ReusesObjects()
        {
            var creationCount = 0;
            Func<TestPoolObject> factory = () =>
            {
                creationCount++;
                return new TestPoolObject { Id = creationCount };
            };

            // Get and return objects
            var obj1 = _optimizer.GetFromPool(factory);
            Assert.Equal(1, creationCount);

            _optimizer.ReturnToPool(obj1);

            var obj2 = _optimizer.GetFromPool(factory);
            Assert.Equal(1, creationCount); // Should reuse, not create new
            Assert.Same(obj1, obj2);
        }

        [Fact]
        public void ObjectPool_RentAndReturn()
        {
            var pool = new PerformanceOptimizer.ObjectPool<TestPoolObject>(
                () => new TestPoolObject(),
                obj => obj.Reset(),
                maxSize: 5);

            var obj1 = pool.Rent();
            obj1.Value = 42;

            pool.Return(obj1);

            var obj2 = pool.Rent();
            Assert.Equal(0, obj2.Value); // Should be reset
        }

        [Fact]
        public void OptimizeQuery_AppliesPagination()
        {
            var data = Enumerable.Range(1, 100).AsQueryable();

            var page1 = _optimizer.OptimizeQuery(data, pageSize: 10, pageNumber: 1);
            var result1 = page1.ToList();
            Assert.Equal(10, result1.Count);
            Assert.Equal(1, result1.First());
            Assert.Equal(10, result1.Last());

            var page2 = _optimizer.OptimizeQuery(data, pageSize: 10, pageNumber: 2);
            var result2 = page2.ToList();
            Assert.Equal(10, result2.Count);
            Assert.Equal(11, result2.First());
            Assert.Equal(20, result2.Last());
        }

        [Fact]
        public void CompressData_DecompressData_RoundTrip()
        {
            var originalData = System.Text.Encoding.UTF8.GetBytes("This is test data for compression");

            var compressed = _optimizer.CompressData(originalData);
            Assert.NotNull(compressed);
            Assert.True(compressed.Length > 0);

            var decompressed = _optimizer.DecompressData(compressed);
            Assert.Equal(originalData, decompressed);
        }

        [Fact]
        public async Task ThrottleAsync_EnforcesMinInterval()
        {
            var key = "throttle-test";
            var minInterval = TimeSpan.FromMilliseconds(100);
            var executionTimes = new List<DateTime>();

            for (int i = 0; i < 3; i++)
            {
                await _optimizer.ThrottleAsync(key, async () =>
                {
                    executionTimes.Add(DateTime.UtcNow);
                    await Task.CompletedTask;
                    return i;
                }, minInterval);
            }

            // Check intervals between executions
            for (int i = 1; i < executionTimes.Count; i++)
            {
                var interval = executionTimes[i] - executionTimes[i - 1];
                Assert.True(interval >= minInterval.Subtract(TimeSpan.FromMilliseconds(10))); // Allow small tolerance
            }
        }

        [Fact]
        public async Task DebounceAsync_OnlyExecutesLast()
        {
            var key = "debounce-test";
            var delay = TimeSpan.FromMilliseconds(100);
            var executionCount = 0;

            // Fire multiple operations quickly
            for (int i = 0; i < 5; i++)
            {
                _ = _optimizer.DebounceAsync(key, async () =>
                {
                    Interlocked.Increment(ref executionCount);
                    await Task.CompletedTask;
                }, delay);
                
                if (i < 4) // Don't delay after last
                    await Task.Delay(20);
            }

            // Wait for debounce to complete
            await Task.Delay(delay.Add(TimeSpan.FromMilliseconds(50)));

            // Only last operation should execute
            Assert.Equal(1, executionCount);
        }

        [Fact]
        public void CreateLazy_CreatesValueOnlyOnce()
        {
            var createCount = 0;
            var lazy = _optimizer.CreateLazy(() =>
            {
                createCount++;
                return "lazy-value";
            });

            Assert.Equal(0, createCount); // Not created yet

            var value1 = lazy.Value;
            Assert.Equal("lazy-value", value1);
            Assert.Equal(1, createCount);

            var value2 = lazy.Value;
            Assert.Equal("lazy-value", value2);
            Assert.Equal(1, createCount); // Still only created once
        }

        [Fact]
        public void OptimizeMemory_RunsWithoutError()
        {
            // Should not throw
            _optimizer.OptimizeMemory();
            
            // Verify logging occurred
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Memory optimization")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void GetPerformanceReport_ReturnsMetrics()
        {
            // Record some metrics
            using (_optimizer.MeasurePerformance("test-operation"))
            {
                Thread.Sleep(10);
            }

            var report = _optimizer.GetPerformanceReport();

            Assert.NotNull(report);
            Assert.NotNull(report.Metrics);
            Assert.True(report.Metrics.Any(m => m.Name.Contains("test-operation")));
        }

        [Fact]
        public async Task ExecuteWithPerformanceAsync_MeasuresPerformance()
        {
            var result = await _optimizer.ExecuteWithPerformanceAsync(
                "measured-operation",
                async () =>
                {
                    await Task.Delay(10);
                    return "result";
                });

            Assert.Equal("result", result);

            var report = _optimizer.GetPerformanceReport();
            Assert.Contains(report.Metrics, m => m.Name.Contains("measured-operation"));
        }

        private class TestPoolObject
        {
            public int Id { get; set; }
            public int Value { get; set; }

            public void Reset()
            {
                Value = 0;
            }
        }
    }
}