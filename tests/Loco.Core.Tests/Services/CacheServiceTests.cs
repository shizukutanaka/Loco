using System;
using System.Threading.Tasks;
using Loco.Core.Services;
using Xunit;

namespace Loco.Core.Tests.Services
{
    public class CacheServiceTests : IDisposable
    {
        private readonly CacheService _cacheService;

        public CacheServiceTests()
        {
            _cacheService = new CacheService();
        }

        public void Dispose()
        {
            _cacheService.Dispose();
        }

        [Fact]
        public void Set_And_TryGet_ShouldWork()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";

            // Act
            _cacheService.Set(key, value);
            var result = _cacheService.TryGet(key, out string retrievedValue);

            // Assert
            Assert.True(result);
            Assert.Equal(value, retrievedValue);
        }

        [Fact]
        public async Task GetOrAddAsync_Should_AddItem_When_NotExists()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";
            var factoryCalled = false;

            // Act
            var result = await _cacheService.GetOrAddAsync(key, () => 
            {
                factoryCalled = true;
                return Task.FromResult(value);
            });

            // Assert
            Assert.Equal(value, result);
            Assert.True(factoryCalled);
        }

        [Fact]
        public async Task GetOrAddAsync_Should_NotCallFactory_When_Exists()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";
            _cacheService.Set(key, value);
            var factoryCalled = false;

            // Act
            var result = await _cacheService.GetOrAddAsync(key, () =>
            {
                factoryCalled = true;
                return Task.FromResult("new_value");
            });

            // Assert
            Assert.Equal(value, result);
            Assert.False(factoryCalled);
        }

        [Fact]
        public async Task Item_Should_Expire()
        {
            // Arrange
            var key = "expiring_key";
            var value = "expiring_value";
            var expiration = TimeSpan.FromMilliseconds(10);

            // Act
            _cacheService.Set(key, value, expiration);
            await Task.Delay(20);
            var result = _cacheService.TryGet(key, out string _);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Remove_Should_RemoveItem()
        {
            // Arrange
            var key = "key_to_remove";
            var value = "value_to_remove";
            _cacheService.Set(key, value);

            // Act
            var removed = _cacheService.Remove(key);
            var result = _cacheService.TryGet(key, out string _);

            // Assert
            Assert.True(removed);
            Assert.False(result);
        }

        [Fact]
        public void Clear_Should_RemoveAllItems()
        {
            // Arrange
            _cacheService.Set("key1", "value1");
            _cacheService.Set("key2", "value2");

            // Act
            _cacheService.Clear();
            var result1 = _cacheService.TryGet("key1", out string _);
            var result2 = _cacheService.TryGet("key2", out string _);

            // Assert
            Assert.False(result1);
            Assert.False(result2);
        }

        [Fact]
        public void Dispose_Should_PreventFurtherOperations()
        {
            // Arrange
            _cacheService.Dispose();

            // Assert
            Assert.Throws<ObjectDisposedException>(() => _cacheService.Set("key", "value"));
            Assert.False(_cacheService.TryGet("key", out string _));
        }

        [Fact]
        public async Task GetOrAddAsync_Should_BeThreadSafe()
        {
            // Arrange
            var key = "concurrent_key";
            var factoryCallCount = 0;
            var tasks = new System.Collections.Generic.List<Task<string>>();
            var numTasks = 100;

            // Act
            for (int i = 0; i < numTasks; i++)
            {
                tasks.Add(_cacheService.GetOrAddAsync(key, async () => 
                {
                    System.Threading.Interlocked.Increment(ref factoryCallCount);
                    await Task.Delay(10); // Simulate work
                    return "concurrent_value";
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(1, factoryCallCount); // Factory should only be called once
            foreach (var result in results)
            {
                Assert.Equal("concurrent_value", result); // All tasks should get the same value
            }
        }
    }
}
