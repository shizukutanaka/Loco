// Rob Pike: "Concurrency is not parallelism"
// John Carmack: "Low latency is more important than high throughput"

using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Loco.Core.Practical;

/// <summary>
/// Fast, lock-free queue for real-world message passing
/// No complex features, just speed and reliability
/// </summary>
public class FastQueue<T>
{
    private readonly Channel<T> _channel;
    private long _totalEnqueued;
    private long _totalDequeued;

    public FastQueue(int capacity = 1000)
    {
        // Bounded channel with simple drop oldest policy
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<T>(options);
    }

    // Fire and forget enqueue
    public async ValueTask EnqueueAsync(T item)
    {
        await _channel.Writer.WriteAsync(item);
        Interlocked.Increment(ref _totalEnqueued);
    }

    // Simple dequeue with timeout
    public async ValueTask<T?> DequeueAsync(int timeoutMs = 1000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            var item = await _channel.Reader.ReadAsync(cts.Token);
            Interlocked.Increment(ref _totalDequeued);
            return item;
        }
        catch (OperationCanceledException)
        {
            return default;
        }
    }

    // Try dequeue without waiting
    public bool TryDequeue(out T? item)
    {
        if (_channel.Reader.TryRead(out item!))
        {
            Interlocked.Increment(ref _totalDequeued);
            return true;
        }
        return false;
    }

    // Simple stats
    public (long enqueued, long dequeued, int pending) GetStats()
    {
        var pending = (int)(_totalEnqueued - _totalDequeued);
        return (_totalEnqueued, _totalDequeued, Math.Max(0, pending));
    }

    // Clean shutdown
    public void Complete() => _channel.Writer.TryComplete();
}