using Loco.Core.Practical;
using Xunit;

namespace Loco.Tests.Practical;

public class SimpleCacheTests
{
    [Fact]
    public void Get_ReturnsValue_WhenKeyExists()
    {
        // Arrange
        var cache = new SimpleCache<string>();
        cache.Set("key", "value");

        // Act
        var result = cache.Get("key");

        // Assert
        Assert.Equal("value", result);
    }

    [Fact]
    public void Get_ReturnsNull_WhenKeyDoesNotExist()
    {
        // Arrange
        var cache = new SimpleCache<string>();

        // Act
        var result = cache.Get("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Get_ReturnsNull_AfterExpiration()
    {
        // Arrange
        var cache = new SimpleCache<string>(TimeSpan.FromMilliseconds(50));
        cache.Set("key", "value");

        // Act
        await Task.Delay(100);
        var result = cache.Get("key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Remove_RemovesItem()
    {
        // Arrange
        var cache = new SimpleCache<string>();
        cache.Set("key", "value");

        // Act
        var removed = cache.Remove("key");
        var result = cache.Get("key");

        // Assert
        Assert.True(removed);
        Assert.Null(result);
    }

    [Fact]
    public void GetStats_ReturnsCorrectCount()
    {
        // Arrange
        var cache = new SimpleCache<string>();
        cache.Set("key1", "value1");
        cache.Set("key2", "value2");

        // Act
        var (count, _) = cache.GetStats();

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public void Cache_IsThreadSafe()
    {
        // Arrange
        var cache = new SimpleCache<int>();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                cache.Set($"key{index}", index);
                var value = cache.Get($"key{index}");
                Assert.Equal(index, value);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        var (count, _) = cache.GetStats();
        Assert.Equal(100, count);
    }
}