using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using FluentAssertions;
using Loco.Core.Memory;
using Xunit;

namespace Loco.Tests.Core.Memory;

public class MemoryPoolTests
{
    [Fact]
    public void Rent_ShouldReturnNewObject_WhenPoolIsEmpty()
    {
        // Arrange
        var pool = new MemoryPool<TestObject>();
        
        // Act
        var obj = pool.Rent();
        
        // Assert
        obj.Should().NotBeNull();
        obj.Should().BeOfType<TestObject>();
    }
    
    [Fact]
    public void Return_ShouldAddObjectToPool()
    {
        // Arrange
        var pool = new MemoryPool<TestObject>(maxSize: 10);
        var obj = new TestObject { Value = 42 };
        
        // Act
        pool.Return(obj);
        var rented = pool.Rent();
        
        // Assert
        rented.Should().BeSameAs(obj);
    }
    
    [Fact]
    public void Return_ShouldResetObject_WhenResetActionProvided()
    {
        // Arrange
        var resetCalled = false;
        var pool = new MemoryPool<TestObject>(
            maxSize: 10,
            reset: obj => 
            {
                obj.Value = 0;
                resetCalled = true;
            }
        );
        var obj = new TestObject { Value = 42 };
        
        // Act
        pool.Return(obj);
        
        // Assert
        resetCalled.Should().BeTrue();
    }
    
    [Fact]
    public void Pool_ShouldNotExceedMaxSize()
    {
        // Arrange
        var pool = new MemoryPool<TestObject>(maxSize: 2);
        var objects = new[] 
        {
            new TestObject(),
            new TestObject(),
            new TestObject()
        };
        
        // Act
        foreach (var obj in objects)
        {
            pool.Return(obj);
        }
        
        // Assert - only 2 objects should be pooled
        var rented1 = pool.Rent();
        var rented2 = pool.Rent();
        var rented3 = pool.Rent();
        
        objects.Should().Contain(rented1);
        objects.Should().Contain(rented2);
        rented3.Should().NotBeNull();
        rented3.Should().NotBeSameAs(rented1);
        rented3.Should().NotBeSameAs(rented2);
    }
    
    [Fact]
    public async Task Pool_ShouldBeThreadSafe()
    {
        // Arrange
        var pool = new MemoryPool<TestObject>(maxSize: 100);
        var bag = new ConcurrentBag<TestObject>();
        var tasks = new Task[10];
        
        // Act
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    var obj = pool.Rent();
                    bag.Add(obj);
                    pool.Return(obj);
                }
            });
        }
        
        await Task.WhenAll(tasks);
        
        // Assert
        bag.Should().HaveCount(1000);
    }
    
    private class TestObject
    {
        public int Value { get; set; }
    }
}

public class StringBuilderPoolTests
{
    [Fact]
    public void Rent_ShouldReturnClearedStringBuilder()
    {
        // Arrange & Act
        var sb1 = StringBuilderPool.Rent();
        sb1.Append("test");
        StringBuilderPool.Return(sb1);
        
        var sb2 = StringBuilderPool.Rent();
        
        // Assert
        sb2.Length.Should().Be(0);
    }
    
    [Fact]
    public void GetStringAndReturn_ShouldReturnStringAndReturnBuilder()
    {
        // Arrange
        var sb = StringBuilderPool.Rent();
        sb.Append("Hello");
        sb.Append(" ");
        sb.Append("World");
        
        // Act
        var result = StringBuilderPool.GetStringAndReturn(sb);
        
        // Assert
        result.Should().Be("Hello World");
    }
}

public class BufferPoolTests
{
    [Fact]
    public void Rent_ShouldReturnBufferOfAtLeastRequestedSize()
    {
        // Arrange & Act
        var buffer = BufferPool.Rent(1024);
        
        // Assert
        buffer.Should().NotBeNull();
        buffer.Length.Should().BeGreaterThanOrEqualTo(1024);
        
        // Cleanup
        BufferPool.Return(buffer);
    }
    
    [Fact]
    public void Return_WithClear_ShouldZeroBuffer()
    {
        // Arrange
        var buffer = BufferPool.Rent(10);
        for (int i = 0; i < buffer.Length && i < 10; i++)
        {
            buffer[i] = (byte)i;
        }
        
        // Act
        BufferPool.Return(buffer, clearArray: true);
        var newBuffer = BufferPool.Rent(10);
        
        // Assert
        if (ReferenceEquals(buffer, newBuffer))
        {
            for (int i = 0; i < 10; i++)
            {
                newBuffer[i].Should().Be(0);
            }
        }
        
        // Cleanup
        BufferPool.Return(newBuffer);
    }
}
