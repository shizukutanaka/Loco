using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Performance;

/// <summary>
/// 最適化された同期プリミティブ
///
/// パフォーマンス比較:
/// - lock (同期): 約205ms
/// - SemaphoreSlim (同期): 約390ms
/// - SemaphoreSlim (非同期): 約0.25μs レイテンシ
///
/// 使い分け:
/// - 同期コード: lock を使用 (2倍高速)
/// - 非同期コード: SemaphoreSlim.WaitAsync を使用 (lockは使用不可)
///
/// 参考: https://www.milanjovanovic.tech/blog/introduction-to-locking-and-concurrency-control-in-dotnet-6
/// </summary>

#region Lock-based Synchronization

/// <summary>
/// 高速ロックベース同期 (同期コード用)
/// </summary>
public sealed class FastLock
{
    private readonly object _lock = new();

    /// <summary>
    /// クリティカルセクションを実行
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Execute(Action action)
    {
        lock (_lock)
        {
            action();
        }
    }

    /// <summary>
    /// クリティカルセクションを実行して結果を返す
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Execute<T>(Func<T> func)
    {
        lock (_lock)
        {
            return func();
        }
    }

    /// <summary>
    /// Monitor.TryEnter を使用したタイムアウト付きロック
    /// </summary>
    public bool TryExecute(Action action, TimeSpan timeout)
    {
        if (Monitor.TryEnter(_lock, timeout))
        {
            try
            {
                action();
                return true;
            }
            finally
            {
                Monitor.Exit(_lock);
            }
        }
        return false;
    }

    /// <summary>
    /// Monitor.TryEnter を使用したタイムアウト付きロック (結果返却版)
    /// </summary>
    public bool TryExecute<T>(Func<T> func, TimeSpan timeout, out T? result)
    {
        result = default;
        if (Monitor.TryEnter(_lock, timeout))
        {
            try
            {
                result = func();
                return true;
            }
            finally
            {
                Monitor.Exit(_lock);
            }
        }
        return false;
    }
}

#endregion

#region Async-friendly Synchronization

/// <summary>
/// 非同期対応セマフォ (非同期コード用)
/// </summary>
public sealed class AsyncLock : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// 非同期クリティカルセクションを実行
    /// </summary>
    public async ValueTask<T> ExecuteAsync<T>(
        Func<CancellationToken, ValueTask<T>> func,
        CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await func(ct).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 非同期クリティカルセクションを実行 (結果なし)
    /// </summary>
    public async ValueTask ExecuteAsync(
        Func<CancellationToken, ValueTask> func,
        CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await func(ct).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// タイムアウト付き非同期クリティカルセクション
    /// </summary>
    public async ValueTask<(bool Success, T? Result)> TryExecuteAsync<T>(
        Func<CancellationToken, ValueTask<T>> func,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        if (await _semaphore.WaitAsync(timeout, ct).ConfigureAwait(false))
        {
            try
            {
                var result = await func(ct).ConfigureAwait(false);
                return (true, result);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        return (false, default);
    }

    /// <summary>
    /// ロックを取得して手動で管理
    /// using (await asyncLock.AcquireAsync()) { ... }
    /// </summary>
    public async ValueTask<IDisposable> AcquireAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        return new ReleaseHandle(_semaphore);
    }

    private sealed class ReleaseHandle : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public ReleaseHandle(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _semaphore.Dispose();
    }
}

#endregion

#region Reader-Writer Lock

/// <summary>
/// 読み取り優先のリーダーライターロック
/// 複数の読み取りスレッドと単一の書き込みスレッドを管理
/// </summary>
public sealed class AsyncReaderWriterLock : IDisposable
{
    private readonly SemaphoreSlim _readSemaphore = new(1, 1);
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private int _readerCount;
    private bool _disposed;

    /// <summary>
    /// 読み取りロックを取得
    /// </summary>
    public async ValueTask<IDisposable> AcquireReaderLockAsync(CancellationToken ct = default)
    {
        await _readSemaphore.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (Interlocked.Increment(ref _readerCount) == 1)
            {
                // 最初の読み取り者が書き込みをブロック
                await _writeSemaphore.WaitAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _readSemaphore.Release();
        }

        return new ReaderReleaseHandle(this);
    }

    /// <summary>
    /// 書き込みロックを取得
    /// </summary>
    public async ValueTask<IDisposable> AcquireWriterLockAsync(CancellationToken ct = default)
    {
        await _writeSemaphore.WaitAsync(ct).ConfigureAwait(false);
        return new WriterReleaseHandle(_writeSemaphore);
    }

    private void ReleaseReaderLock()
    {
        _readSemaphore.Wait();
        try
        {
            if (Interlocked.Decrement(ref _readerCount) == 0)
            {
                // 最後の読み取り者が書き込みを許可
                _writeSemaphore.Release();
            }
        }
        finally
        {
            _readSemaphore.Release();
        }
    }

    private sealed class ReaderReleaseHandle : IDisposable
    {
        private readonly AsyncReaderWriterLock _parent;
        private bool _disposed;

        public ReaderReleaseHandle(AsyncReaderWriterLock parent)
        {
            _parent = parent;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _parent.ReleaseReaderLock();
        }
    }

    private sealed class WriterReleaseHandle : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public WriterReleaseHandle(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _readSemaphore.Dispose();
        _writeSemaphore.Dispose();
    }
}

#endregion

#region Throttle/Rate Limiter

/// <summary>
/// 並列実行数を制限するスロットル
/// </summary>
public sealed class ConcurrencyThrottle : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    public ConcurrencyThrottle(int maxConcurrency)
    {
        if (maxConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Must be at least 1");
        }
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    /// <summary>
    /// スロットルされた実行
    /// </summary>
    public async ValueTask<T> ExecuteAsync<T>(
        Func<CancellationToken, ValueTask<T>> func,
        CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await func(ct).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 現在の利用可能スロット数
    /// </summary>
    public int AvailableSlots => _semaphore.CurrentCount;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _semaphore.Dispose();
    }
}

#endregion

#region Spin Lock Alternative

/// <summary>
/// スピンロック (短時間のロックに最適)
/// </summary>
public struct LightweightSpinLock
{
    private int _lockState;

    /// <summary>
    /// ロックを取得 (スピン待機)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enter()
    {
        while (Interlocked.CompareExchange(ref _lockState, 1, 0) != 0)
        {
            Thread.SpinWait(1);
        }
    }

    /// <summary>
    /// ロックを試行
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnter()
    {
        return Interlocked.CompareExchange(ref _lockState, 1, 0) == 0;
    }

    /// <summary>
    /// ロックを解放
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Exit()
    {
        Volatile.Write(ref _lockState, 0);
    }

    /// <summary>
    /// ロック取得と自動解放
    /// using (spinLock.EnterScope()) { ... }
    /// </summary>
    public LockScope EnterScope()
    {
        Enter();
        return new LockScope(this);
    }

    public ref struct LockScope
    {
        private LightweightSpinLock _lock;

        public LockScope(LightweightSpinLock spinLock)
        {
            _lock = spinLock;
        }

        public void Dispose()
        {
            _lock.Exit();
        }
    }
}

#endregion

#region Lazy Initialization

/// <summary>
/// 遅延初期化 (スレッドセーフ、高性能)
/// </summary>
public sealed class FastLazy<T>
{
    private readonly Func<T> _valueFactory;
    private T? _value;
    private volatile bool _isInitialized;
    private readonly object _lock = new();

    public FastLazy(Func<T> valueFactory)
    {
        _valueFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
    }

    /// <summary>
    /// 値を取得 (必要に応じて初期化)
    /// </summary>
    public T Value
    {
        get
        {
            if (_isInitialized)
            {
                return _value!;
            }

            return InitializeValue();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private T InitializeValue()
    {
        lock (_lock)
        {
            if (!_isInitialized)
            {
                _value = _valueFactory();
                _isInitialized = true;
            }
            return _value!;
        }
    }

    /// <summary>
    /// 既に初期化されているかチェック
    /// </summary>
    public bool IsValueCreated => _isInitialized;
}

#endregion

#region Performance Comparison

/// <summary>
/// 同期プリミティブのパフォーマンス情報
/// </summary>
public static class SynchronizationPerformanceInfo
{
    /// <summary>
    /// パフォーマンス比較情報を取得
    /// </summary>
    public static string GetPerformanceComparison()
    {
        return """
            Synchronization Performance Comparison:

            Primitive              | Latency (μs) | Use Case
            -----------------------|--------------|--------------------------------
            SpinLock (uncontended) | 0.01-0.05    | Very short critical sections
            lock (uncontended)     | 0.02-0.10    | Synchronous code, short locks
            Interlocked operations | 0.01-0.02    | Simple atomic operations
            SemaphoreSlim (sync)   | 0.25-1.00    | Async compatibility needed
            SemaphoreSlim (async)  | 0.25-1.00    | Async/await code
            Semaphore (OS)         | 5.00-50.00   | Cross-process synchronization

            Recommendations:
            - Synchronous code: Use 'lock' (2x faster than SemaphoreSlim)
            - Async/await code: Use SemaphoreSlim.WaitAsync()
            - Hot loops (<10μs): Use SpinLock or Interlocked
            - Reader-heavy: Use ReaderWriterLockSlim or AsyncReaderWriterLock
            - Rate limiting: Use SemaphoreSlim with max count

            NEVER use 'lock' inside async methods!
            """;
    }
}

#endregion
