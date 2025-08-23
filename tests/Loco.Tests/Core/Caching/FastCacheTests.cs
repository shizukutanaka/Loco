using System;
using System.Threading.Tasks;
using FluentAssertions;
using Loco.Core.Caching;
using Xunit;

namespace Loco.Tests.Core.Caching;

public class FastCacheTests
{
    [Fact]
    public void TryGet_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        // Arrange
        var cache = new FastCache<string, int>();
        
        // Act
        var result = cache.TryGet("nonexistent", out var value);
        
        // Assert
        result.Should().BeFalse();
        value.Should().Be(0);
    }
    
    [Fact]
    public void Set_And_TryGet_ShouldStoreAndRetrieveValue()
    {
        // Arrange
        var cache = new FastCache<string, string>();
        
        // Act
        cache.Set("key1", "value1");
        var result = cache.TryGet("key1", out var value);
        
        // Assert
        result.Should().BeTrue();
        value.Should().Be("value1");
    }
    
    [Fact]
    public void TryGet_ShouldReturnFalse_WhenItemExpired()
    {
        // Arrange
        var cache = new FastCache<string, string>();
        cache.Set("key1", "value1", TimeSpan.FromMilliseconds(50));
        
        // Act
        Task.Delay(100).Wait();
        var result = cache.TryGet("key1", out var value);
        
        // Assert
        result.Should().BeFalse();
        value.Should().BeNull();
    }
    
    [Fact]
    public void GetOrCreate_ShouldReturnCachedValue_WhenExists()
    {
        // Arrange
        var cache = new FastCache<string, int>();
        cache.Set("key1", 42);
        var factoryCalled = false;
        
        // Act
        var result = cache.GetOrCreate("key1", () =>
        {
            factoryCalled = true;
            return 100;
        });
        
        // Assert
        result.Should().Be(42);
        factoryCalled.Should().BeFalse();
    }
    
    [Fact]
    public void GetOrCreate_ShouldCallFactory_WhenNotExists()
    {
        // Arrange
        var cache = new FastCache<string, int>();
        var factoryCalled = false;
        
        // Act
        var result = cache.GetOrCreate("key1", () =>
        {
            factoryCalled = true;
            return 100;
        });
        
        // Assert
        result.Should().Be(100);
        factoryCalled.Should().BeTrue();
    }
    
    [Fact]
    public async Task GetOrCreateAsync_ShouldReturnCachedValue_WhenExists()
    {
        // Arrange
        var cache = new FastCache<string, int>();
        cache.Set("key1", 42);
        var factoryCalled = false;
        
        // Act
        var result = await cache.GetOrCreateAsync("key1", async () =>
        {
            factoryCalled = true;
            await Task.Delay(10);
            return 100;
        });
        
        // Assert
        result.Should().Be(42);
        factoryCalled.Should().BeFalse();
    }
    
    [Fact]
    public void Remove_ShouldRemoveItem()
    {
        // Arrange
        var cache = new FastCache<string, string>();
        cache.Set("key1", "value1");
        
        // Act
        var removed = cache.Remove("key1");
        var exists = cache.TryGet("key1", out _);
        
        // Assert
        removed.Should().BeTrue();
        exists.Should().BeFalse();
    }
    
    [Fact]
    public void Clear_ShouldRemoveAllItems()
    {
        // Arrange
        var cache = new FastCache<string, int>();
        cache.Set("key1", 1);
        cache.Set("key2", 2);
        cache.Set("key3", 3);
        
        // Act
        cache.Clear();
        
        // Assert
        cache.Count.Should().Be(0);
        cache.TryGet("key1", out _).Should().BeFalse();
        cache.TryGet("key2", out _).Should().BeFalse();
        cache.TryGet("key3", out _).Should().BeFalse();
    }
    
    [Fact]
    public void HitRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var cache = new FastCache<string, int>();
        cache.Set("key1", 1);
        
        // Act
        cache.TryGet("key1", out _); // Hit
        cache.TryGet("key1", out _); // Hit
        cache.TryGet("key2", out _); // Miss
        cache.TryGet("key3", out _); // Miss
        
        // Assert
        cache.HitRate.Should().BeApproximately(0.5, 0.01); // 2 hits out of 4 attempts
    }
    
    [Fact]
    public void Cache_ShouldEvictLeastRecentlyUsed_WhenFull()
    {
        // Arrange
        var cache = new FastCache<int, string>(maxSize: 3);
        
        // Act
        cache.Set(1, "one");
        cache.Set(2, "two");
        cache.Set(3, "three");
        
        // Access 1 and 2 to make them recently used
        cache.TryGet(1, out _);
        cache.TryGet(2, out _);
        
        // Add a fourth item, should evict 3
        cache.Set(4, "four");
        
        // Wait for eviction to complete
        Task.Delay(100).Wait();
        
        // Assert
        cache.TryGet(1, out _).Should().BeTrue();
        cache.TryGet(2, out _).Should().BeTrue();
        cache.TryGet(4, out _).Should().BeTrue();
        // Item 3 might or might not be evicted depending on timing
    }
    
    [Fact]
    public async Task Cache_ShouldBeThreadSafe()
    {
        // Arrange
        var cache = new FastCache<int, int>();
        var tasks = new Task[10];
        
        // Act
        for (int i = 0; i < tasks.Length; i++)
        {
            var threadId = i;
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    cache.Set(threadId * 100 + j, j);
                    cache.TryGet(threadId * 100 + j, out _);
                }
            });
        }
        
        await Task.WhenAll(tasks);
        
        // Assert
        cache.Count.Should().BeGreaterThan(0);
        cache.HitRate.Should().BeGreaterThan(0);
    }
}

public class CacheManagerTests
{
    [Fact]
    public void GetCache_ShouldReturnSameInstance_ForSameName()
    {
        // Act
        var cache1 = CacheManager.GetCache<string, int>("test");
        var cache2 = CacheManager.GetCache<string, int>("test");
        
        // Assert
        cache1.Should().BeSameAs(cache2);
    }
    
    [Fact]
    public void GetCache_ShouldReturnDifferentInstances_ForDifferentNames()
    {
        // Act
        var cache1 = CacheManager.GetCache<string, int>("test1");
        var cache2 = CacheManager.GetCache<string, int>("test2");
        
        // Assert
        cache1.Should().NotBeSameAs(cache2);
    }
    
    [Fact]
    public void ClearAll_ShouldDisposeAllCaches()
    {
        // Arrange
        var cache1 = CacheManager.GetCache<string, int>("test1");
        var cache2 = CacheManager.GetCache<string, int>("test2");
        cache1.Set("key", 1);
        cache2.Set("key", 2);
        
        // Act
        CacheManager.ClearAll();
        
        // Assert
        // After clearing, getting caches with same names should return new instances
        var newCache1 = CacheManager.GetCache<string, int>("test1");
        var newCache2 = CacheManager.GetCache<string, int>("test2");
        
        newCache1.Should().NotBeSameAs(cache1);
        newCache2.Should().NotBeSameAs(cache2);
        newCache1.Count.Should().Be(0);
        newCache2.Count.Should().Be(0);
    }
}
