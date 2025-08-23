using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Loco.Core.Caching;
using Microsoft.Extensions.Logging;
using Moq;

namespace Loco.Core.Tests.Caching
{
    public class FastCacheTests
    {
        private readonly Mock<ILogger<FastCache>> _loggerMock;
        private readonly FastCache _cache;

        public FastCacheTests()
        {
            _loggerMock = new Mock<ILogger<FastCache>>();
            _cache = new FastCache(_loggerMock.Object);
        }

        [Fact]
        public async Task Set_And_Get_Should_Work_Correctly()
        {
            // Arrange
            var key = "test-key";
            var value = "test-value";

            // Act
            await _cache.SetAsync(key, value);
            var retrievedValue = await _cache.GetAsync<string>(key);

            // Assert
            retrievedValue.Should().Be(value);
        }

        [Fact]
        public async Task Get_Should_Return_Default_For_NonExistent_Key()
        {
            // Arrange
            var key = "non-existent-key";

            // Act
            var value = await _cache.GetAsync<string>(key);

            // Assert
            value.Should().BeNull();
        }

        [Fact]
        public async Task Remove_Should_Remove_Item_From_Cache()
        {
            // Arrange
            var key = "test-key";
            var value = "test-value";
            await _cache.SetAsync(key, value);

            // Act
            await _cache.RemoveAsync(key);
            var retrievedValue = await _cache.GetAsync<string>(key);

            // Assert
            retrievedValue.Should().BeNull();
        }

        [Fact]
        public async Task Clear_Should_Remove_All_Items()
        {
            // Arrange
            await _cache.SetAsync("key1", "value1");
            await _cache.SetAsync("key2", "value2");
            await _cache.SetAsync("key3", "value3");

            // Act
            await _cache.ClearAsync();

            // Assert
            var value1 = await _cache.GetAsync<string>("key1");
            var value2 = await _cache.GetAsync<string>("key2");
            var value3 = await _cache.GetAsync<string>("key3");

            value1.Should().BeNull();
            value2.Should().BeNull();
            value3.Should().BeNull();
        }

        [Fact]
        public async Task Set_With_Expiration_Should_Expire_Item()
        {
            // Arrange
            var key = "expiring-key";
            var value = "expiring-value";
            var expiration = TimeSpan.FromMilliseconds(100);

            // Act
            await _cache.SetAsync(key, value, expiration);
            var immediateValue = await _cache.GetAsync<string>(key);
            await Task.Delay(150);
            var expiredValue = await _cache.GetAsync<string>(key);

            // Assert
            immediateValue.Should().Be(value);
            expiredValue.Should().BeNull();
        }

        [Fact]
        public async Task GetOrCreate_Should_Create_If_Not_Exists()
        {
            // Arrange
            var key = "create-key";
            var expectedValue = "created-value";
            Func<Task<string>> factory = async () =>
            {
                await Task.Delay(10);
                return expectedValue;
            };

            // Act
            var value = await _cache.GetOrCreateAsync(key, factory);

            // Assert
            value.Should().Be(expectedValue);
            var cachedValue = await _cache.GetAsync<string>(key);
            cachedValue.Should().Be(expectedValue);
        }

        [Fact]
        public async Task GetOrCreate_Should_Return_Existing_Value()
        {
            // Arrange
            var key = "existing-key";
            var existingValue = "existing-value";
            await _cache.SetAsync(key, existingValue);

            Func<Task<string>> factory = async () =>
            {
                await Task.Delay(10);
                return "new-value";
            };

            // Act
            var value = await _cache.GetOrCreateAsync(key, factory);

            // Assert
            value.Should().Be(existingValue);
        }

        [Fact]
        public async Task Cache_Should_Handle_Complex_Objects()
        {
            // Arrange
            var key = "complex-key";
            var complexObject = new
            {
                Id = 123,
                Name = "Test Object",
                Date = DateTime.Now,
                Nested = new
                {
                    Property = "Nested Value"
                }
            };

            // Act
            await _cache.SetAsync(key, complexObject);
            var retrieved = await _cache.GetAsync<dynamic>(key);

            // Assert
            retrieved.Should().NotBeNull();
            ((object)retrieved.Id).Should().Be(123);
            ((object)retrieved.Name).Should().Be("Test Object");
        }

        [Fact]
        public async Task Cache_Should_Be_Thread_Safe()
        {
            // Arrange
            var tasks = new Task[100];
            var key = "concurrent-key";

            // Act
            for (int i = 0; i < tasks.Length; i++)
            {
                var index = i;
                tasks[i] = Task.Run(async () =>
                {
                    await _cache.SetAsync($"{key}-{index}", index);
                    var value = await _cache.GetAsync<int>($"{key}-{index}");
                    value.Should().Be(index);
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            for (int i = 0; i < tasks.Length; i++)
            {
                var value = await _cache.GetAsync<int>($"{key}-{i}");
                value.Should().Be(i);
            }
        }
    }
}
