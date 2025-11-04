#nullable enable

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Async;

/// <summary>
/// Async/Await best practices and patterns for high-performance asynchronous code
/// </summary>
public static class AsyncBestPractices
{
    /// <summary>
    /// Async execution context
    /// </summary>
    public class AsyncExecutionContext
    {
        public string? OperationId { get; set; }
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Metadata { get; set; } = new();

        public TimeSpan ElapsedTime => DateTime.UtcNow - StartTime;
    }

    /// <summary>
    /// Async guard helper to ensure proper async implementation
    /// </summary>
    public static class AsyncGuard
    {
        /// <summary>
        /// Validates that a method is truly asynchronous (has ConfigureAwait, etc.)
        /// </summary>
        public static void ValidateAsync()
        {
            // Compile-time validation would go here
            // This is a placeholder for runtime checks
        }
    }
}

/// <summary>
/// Safe async wrapper with ConfigureAwait best practices
/// CRITICAL: Always use ConfigureAwait(false) in library code to avoid UI thread deadlock
/// </summary>
public static class SafeAsyncExtensions
{
    /// <summary>
    /// Wraps Task with ConfigureAwait(false) for library code
    /// Prevents deadlock when called from UI context
    /// </summary>
    public static ConfiguredTaskAwaitable SafeAwait(this Task task)
    {
        return task.ConfigureAwait(false);
    }

    /// <summary>
    /// Wraps Task<T> with ConfigureAwait(false)
    /// </summary>
    public static ConfiguredTaskAwaitable<T> SafeAwait<T>(this Task<T> task)
    {
        return task.ConfigureAwait(false);
    }

    /// <summary>
    /// Wraps ValueTask with ConfigureAwait(false)
    /// </summary>
    public static ConfiguredValueTaskAwaitable SafeAwait(this ValueTask task)
    {
        return task.ConfigureAwait(false);
    }

    /// <summary>
    /// Wraps ValueTask<T> with ConfigureAwait(false)
    /// </summary>
    public static ConfiguredValueTaskAwaitable<T> SafeAwait<T>(this ValueTask<T> task)
    {
        return task.ConfigureAwait(false);
    }
}

/// <summary>
/// Async timeout helper - safe way to add timeout to async operations
/// </summary>
public static class AsyncTimeoutExtensions
{
    /// <summary>
    /// Adds timeout to async operation with proper cancellation
    /// </summary>
    public static async Task<T> WithTimeoutAsync<T>(
        this Task<T> task,
        TimeSpan timeout,
        ILogger? logger = null)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            logger?.LogError("Operation timed out after {Timeout}ms", timeout.TotalMilliseconds);
            throw new TimeoutException($"Operation timed out after {timeout.TotalMilliseconds}ms");
        }
    }

    /// <summary>
    /// Adds timeout to Task (non-generic)
    /// </summary>
    public static async Task WithTimeoutAsync(
        this Task task,
        TimeSpan timeout,
        ILogger? logger = null)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            logger?.LogError("Operation timed out after {Timeout}ms", timeout.TotalMilliseconds);
            throw new TimeoutException($"Operation timed out after {timeout.TotalMilliseconds}ms");
        }
    }
}

/// <summary>
/// Retry helper for async operations with exponential backoff
/// </summary>
public static class AsyncRetryExtensions
{
    /// <summary>
    /// Retries async operation with exponential backoff
    /// </summary>
    public static async Task<T> RetryAsync<T>(
        this Func<Task<T>> operation,
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        ILogger? logger = null)
    {
        var delay = initialDelay ?? TimeSpan.FromMilliseconds(100);
        var lastException = default(Exception);

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < maxRetries - 1)
            {
                lastException = ex;
                logger?.LogWarning(
                    ex,
                    "Attempt {Attempt} failed, retrying after {Delay}ms",
                    attempt + 1,
                    delay.TotalMilliseconds);

                await Task.Delay(delay).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
            }
            catch
            {
                throw; // Last attempt, re-throw
            }
        }

        throw lastException ?? new InvalidOperationException("Retry operation failed");
    }

    /// <summary>
    /// Retries async operation for Task (non-generic)
    /// </summary>
    public static async Task RetryAsync(
        this Func<Task> operation,
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        ILogger? logger = null)
    {
        var delay = initialDelay ?? TimeSpan.FromMilliseconds(100);
        var lastException = default(Exception);

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await operation().ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries - 1)
            {
                lastException = ex;
                logger?.LogWarning(
                    ex,
                    "Attempt {Attempt} failed, retrying after {Delay}ms",
                    attempt + 1,
                    delay.TotalMilliseconds);

                await Task.Delay(delay).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
            catch
            {
                throw;
            }
        }

        throw lastException ?? new InvalidOperationException("Retry operation failed");
    }

    /// <summary>
    /// Retries with custom predicate for determining retry eligibility
    /// </summary>
    public static async Task<T> RetryAsync<T>(
        this Func<Task<T>> operation,
        Func<Exception, bool> shouldRetry,
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        ILogger? logger = null)
    {
        var delay = initialDelay ?? TimeSpan.FromMilliseconds(100);
        var lastException = default(Exception);

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex) when (shouldRetry(ex) && attempt < maxRetries - 1)
            {
                lastException = ex;
                logger?.LogWarning(
                    ex,
                    "Attempt {Attempt} failed with retryable error, retrying after {Delay}ms",
                    attempt + 1,
                    delay.TotalMilliseconds);

                await Task.Delay(delay).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
            catch
            {
                throw;
            }
        }

        throw lastException ?? new InvalidOperationException("Retry operation failed");
    }
}

/// <summary>
/// Async initialization helper for async constructors pattern
/// </summary>
public abstract class AsyncInitializable
{
    private readonly Lazy<Task> _initializationTask;

    protected AsyncInitializable()
    {
        _initializationTask = new Lazy<Task>(InitializeAsync);
    }

    /// <summary>
    /// Initialize asynchronously
    /// </summary>
    protected abstract Task InitializeAsync();

    /// <summary>
    /// Ensures initialization is complete before operation
    /// </summary>
    protected async Task EnsureInitializedAsync()
    {
        await _initializationTask.Value.ConfigureAwait(false);
    }

    /// <summary>
    /// Static factory for creating initialized instances
    /// </summary>
    /// <remarks>
    /// Usage: var instance = await YourClass.CreateAsync();
    /// </remarks>
    public static async Task<T> CreateAsync<T>() where T : AsyncInitializable, new()
    {
        var instance = new T();
        await instance.EnsureInitializedAsync().ConfigureAwait(false);
        return instance;
    }
}

/// <summary>
/// Async resource management for IAsyncDisposable pattern
/// </summary>
public abstract class AsyncDisposableResource : IAsyncDisposable
{
    private bool _disposed;

    /// <summary>
    /// Performs async cleanup
    /// </summary>
    protected abstract ValueTask DisposeAsyncCore();

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        try
        {
            await DisposeAsyncCore().ConfigureAwait(false);
        }
        finally
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    protected void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }
}

/// <summary>
/// Async collection processing with concurrency control
/// </summary>
public static class AsyncCollectionExtensions
{
    /// <summary>
    /// Processes items in parallel with max degree of concurrency
    /// SAFE VERSION: Properly uses ConfigureAwait and cancellation
    /// </summary>
    public static async Task ForEachAsync<T>(
        this IEnumerable<T> items,
        Func<T, Task> operation,
        int maxConcurrency = 10,
        CancellationToken cancellationToken = default)
    {
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = items.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await operation(item).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Projects items asynchronously with concurrency control
    /// </summary>
    public static async Task<List<TResult>> SelectAsync<T, TResult>(
        this IEnumerable<T> items,
        Func<T, Task<TResult>> selector,
        int maxConcurrency = 10,
        CancellationToken cancellationToken = default)
    {
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = items.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await selector(item).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return (await Task.WhenAll(tasks).ConfigureAwait(false)).ToList();
    }

    /// <summary>
    /// Batches items for async processing
    /// </summary>
    public static async Task BatchAsync<T>(
        this IAsyncEnumerable<T> items,
        Func<List<T>, Task> processor,
        int batchSize = 100,
        CancellationToken cancellationToken = default)
    {
        var batch = new List<T>(batchSize);

        await foreach (var item in items.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            batch.Add(item);

            if (batch.Count >= batchSize)
            {
                await processor(batch).ConfigureAwait(false);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await processor(batch).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Async lazy initialization with double-check locking
/// </summary>
public class AsyncLazy<T>
{
    private readonly Lazy<Task<T>> _instance;

    public AsyncLazy(Func<Task<T>> factory)
    {
        _instance = new Lazy<Task<T>>(() => factory());
    }

    public Task<T> Value => _instance.Value;

    public bool IsValueCreated => _instance.IsValueCreated;
}

/// <summary>
/// Async event helper for fire-and-forget operations (safe version)
/// </summary>
public static class AsyncEventExtensions
{
    /// <summary>
    /// Safely invokes async event handlers without awaiting
    /// Logs any exceptions instead of letting them crash
    /// </summary>
    public static void SafeInvokeAsync<T>(
        this Func<T, Task>? handlers,
        T arg,
        ILogger? logger = null)
    {
        if (handlers == null)
            return;

        var task = handlers(arg);
        _ = task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                logger?.LogError(t.Exception, "Error in async event handler");
            }
        }, TaskScheduler.Default);
    }

    /// <summary>
    /// Async event handler delegate
    /// </summary>
    public delegate Task AsyncEventHandler(object? sender, EventArgs e);

    /// <summary>
    /// Safely invokes async event with proper error handling
    /// </summary>
    public static async Task SafeInvokeAsync(
        this AsyncEventHandler? handler,
        object? sender,
        EventArgs e,
        ILogger? logger = null)
    {
        if (handler == null)
            return;

        var invocationList = handler.GetInvocationList().Cast<AsyncEventHandler>();
        var tasks = invocationList.Select(h => h(sender, e));

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error in async event handlers");
            throw;
        }
    }
}

/// <summary>
/// Channel-based producer-consumer pattern for high-throughput async processing
/// </summary>
public class AsyncProducerConsumer<T>
{
    private readonly System.Threading.Channels.Channel<T> _channel;
    private readonly ILogger<AsyncProducerConsumer<T>> _logger;

    public AsyncProducerConsumer(
        int maxQueueSize = 100,
        ILogger<AsyncProducerConsumer<T>>? logger = null)
    {
        _logger = logger;
        var options = new System.Threading.Channels.BoundedChannelOptions(maxQueueSize)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait
        };

        _channel = System.Threading.Channels.Channel.CreateBounded<T>(options);
    }

    /// <summary>
    /// Produces item to channel
    /// </summary>
    public async ValueTask ProduceAsync(T item, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        _logger?.LogDebug("Produced item to channel");
    }

    /// <summary>
    /// Consumes items from channel
    /// </summary>
    public async IAsyncEnumerable<T> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            _logger?.LogDebug("Consumed item from channel");
            yield return item;
        }
    }

    /// <summary>
    /// Signals completion (no more items)
    /// </summary>
    public void CompleteProduction()
    {
        _channel.Writer.TryComplete();
    }
}

/// <summary>
/// Example async patterns
/// </summary>
public class AsyncPatternExamples
{
    /// <summary>
    /// CORRECT: Async method with ConfigureAwait(false)
    /// Safe for library code
    /// </summary>
    public async Task<string> GetDataAsync()
    {
        await Task.Delay(100).ConfigureAwait(false);
        return "Data";
    }

    /// <summary>
    /// CORRECT: Async initialization pattern
    /// </summary>
    public class DatabaseConnection : AsyncInitializable
    {
        public string? ConnectionString { get; private set; }

        protected override async Task InitializeAsync()
        {
            // Simulate async connection
            await Task.Delay(100).ConfigureAwait(false);
            ConnectionString = "initialized";
        }

        // Usage: var conn = await DatabaseConnection.CreateAsync();
    }

    /// <summary>
    /// CORRECT: Async resource management
    /// </summary>
    public class FileStream : AsyncDisposableResource
    {
        private string? _buffer;

        public async Task WriteAsync(string data)
        {
            ThrowIfDisposed();
            _buffer = data;
            await Task.Delay(50).ConfigureAwait(false);
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            // Flush and close
            await Task.Delay(50).ConfigureAwait(false);
            _buffer = null;
        }

        // Usage:
        // await using var stream = new FileStream();
        // await stream.WriteAsync("data");
    }

    /// <summary>
    /// CORRECT: Retry pattern
    /// </summary>
    public async Task RetryExample(ILogger logger)
    {
        var result = await (async () =>
        {
            // Simulated operation that might fail
            await Task.Delay(10).ConfigureAwait(false);
            return "success";
        }).RetryAsync(3, TimeSpan.FromMilliseconds(100), logger);
    }

    /// <summary>
    /// CORRECT: Parallel processing with concurrency control
    /// </summary>
    public async Task ParallelProcessing(IEnumerable<int> items)
    {
        await items.ForEachAsync(
            async item =>
            {
                await Task.Delay(10).ConfigureAwait(false);
            },
            maxConcurrency: 5);
    }
}

/// <summary>
/// Async patterns best practices guide
/// </summary>
public static class AsyncBestPracticesGuide
{
    public const string BEST_PRACTICES = @"
ASYNC/AWAIT BEST PRACTICES:

1. ALWAYS use ConfigureAwait(false) in library code
   - Prevents UI thread deadlock
   - Library code has no UI context dependency
   - Exception: ASP.NET Core controllers (already on ThreadPool)

2. AVOID async void
   - Only use for event handlers
   - Can't be awaited or catch exceptions
   - Use Task for all other cases

3. AVOID blocking on async code
   - DON'T: result.Wait() or result.Result
   - DONT: Task.Run(async () => ...).Result
   - DO: await the task directly

4. Use ValueTask for hot paths
   - When allocation matters (millions of calls)
   - Completes synchronously in common case
   - Complex async only in exception cases

5. Use IAsyncEnumerable for streaming
   - Large datasets that don't fit in memory
   - Progressive lazy evaluation
   - Cancellation token support

6. Manage cancellation properly
   - Pass CancellationToken to all async methods
   - Check token before long-running operations
   - Use timeout for external services

7. Retry with exponential backoff
   - Transient failure handling
   - Never use Thread.Sleep, use Task.Delay
   - Include jitter to prevent thundering herd

8. Parallel processing with concurrency limits
   - Don't Task.WhenAll unlimited items
   - Use SemaphoreSlim for rate limiting
   - Consider memory pressure

9. Clean up resources
   - Implement IAsyncDisposable
   - Use await using for async cleanup
   - Suppress finalizer after disposal

10. Async initialization
    - Use async factory method (CreateAsync)
    - Never make constructor async
    - Use Lazy<Task<T>> for lazy initialization
";
}
