using System;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Performance;

/// <summary>
/// シンプルなリジリエンスパターン実装
/// Polly v8 のコンセプトに基づく軽量版
///
/// パターン:
/// - Retry: 一時的な障害からの回復
/// - Circuit Breaker: カスケード障害の防止
/// - Timeout: 長時間実行の制限
///
/// 参考: https://github.com/App-vNext/Polly
/// </summary>
public static class SimpleResilience
{
    /// <summary>
    /// 指数バックオフ付きリトライを実行
    /// </summary>
    public static async ValueTask<T> RetryAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        Func<Exception, bool>? shouldRetry = null,
        Action<Exception, int, TimeSpan>? onRetry = null,
        CancellationToken ct = default)
    {
        var delay = initialDelay ?? TimeSpan.FromMilliseconds(100);
        shouldRetry ??= IsTransient;

        Exception? lastException = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < maxRetries && shouldRetry(ex))
            {
                lastException = ex;
                var waitTime = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * Math.Pow(2, attempt));

                onRetry?.Invoke(ex, attempt + 1, waitTime);

                await Task.Delay(waitTime, ct).ConfigureAwait(false);
            }
        }

        throw new RetryExhaustedException(
            $"Operation failed after {maxRetries + 1} attempts",
            lastException);
    }

    /// <summary>
    /// タイムアウト付きで実行
    /// </summary>
    public static async ValueTask<T> WithTimeoutAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await operation(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Operation timed out after {timeout.TotalSeconds:F1}s");
        }
    }

    /// <summary>
    /// フォールバック付きで実行
    /// </summary>
    public static async ValueTask<T> WithFallbackAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        Func<Exception, CancellationToken, ValueTask<T>> fallback,
        CancellationToken ct = default)
    {
        try
        {
            return await operation(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await fallback(ex, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 一時的な障害かどうかを判定
    /// </summary>
    public static bool IsTransient(Exception ex)
    {
        return ex is TimeoutException
            or HttpRequestException
            or System.Net.Sockets.SocketException
            or System.IO.IOException;
    }
}

/// <summary>
/// サーキットブレーカー
/// 連続した障害を検出してサービスを保護
/// </summary>
public sealed class CircuitBreaker : IDisposable
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly object _lock = new();

    private CircuitState _state = CircuitState.Closed;
    private int _failureCount;
    private DateTime _openedAt;
    private bool _disposed;

    public CircuitBreaker(int failureThreshold = 5, TimeSpan? openDuration = null)
    {
        _failureThreshold = failureThreshold;
        _openDuration = openDuration ?? TimeSpan.FromSeconds(30);
    }

    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                if (_state == CircuitState.Open &&
                    DateTime.UtcNow - _openedAt >= _openDuration)
                {
                    _state = CircuitState.HalfOpen;
                }
                return _state;
            }
        }
    }

    /// <summary>
    /// サーキットブレーカーを通して操作を実行
    /// </summary>
    public async ValueTask<T> ExecuteAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken ct = default)
    {
        var currentState = State;

        if (currentState == CircuitState.Open)
        {
            throw new CircuitBrokenException(
                $"Circuit is open. Will reset after {(_openedAt + _openDuration - DateTime.UtcNow).TotalSeconds:F0}s");
        }

        try
        {
            var result = await operation(ct).ConfigureAwait(false);
            OnSuccess();
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            OnFailure();
            throw;
        }
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
        }
    }

    private void OnFailure()
    {
        lock (_lock)
        {
            _failureCount++;

            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitState.Open;
                _openedAt = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// 強制的にサーキットをリセット
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

/// <summary>
/// サーキットの状態
/// </summary>
public enum CircuitState
{
    /// <summary>正常動作中</summary>
    Closed,
    /// <summary>障害検出、リクエストをブロック</summary>
    Open,
    /// <summary>回復テスト中</summary>
    HalfOpen
}

/// <summary>
/// リジリエンスパイプライン
/// 複数の戦略を組み合わせて実行
/// </summary>
public sealed class ResiliencePipeline
{
    private readonly int _maxRetries;
    private readonly TimeSpan _retryDelay;
    private readonly TimeSpan _timeout;
    private readonly CircuitBreaker? _circuitBreaker;

    private ResiliencePipeline(
        int maxRetries,
        TimeSpan retryDelay,
        TimeSpan timeout,
        CircuitBreaker? circuitBreaker)
    {
        _maxRetries = maxRetries;
        _retryDelay = retryDelay;
        _timeout = timeout;
        _circuitBreaker = circuitBreaker;
    }

    /// <summary>
    /// パイプラインを通して操作を実行
    /// </summary>
    public async ValueTask<T> ExecuteAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken ct = default)
    {
        // タイムアウト付き
        async ValueTask<T> WithTimeout(CancellationToken innerCt)
        {
            return await SimpleResilience.WithTimeoutAsync(
                operation, _timeout, innerCt).ConfigureAwait(false);
        }

        // リトライ付き
        async ValueTask<T> WithRetry(CancellationToken innerCt)
        {
            return await SimpleResilience.RetryAsync(
                WithTimeout, _maxRetries, _retryDelay, ct: innerCt).ConfigureAwait(false);
        }

        // サーキットブレーカー付き（あれば）
        if (_circuitBreaker != null)
        {
            return await _circuitBreaker.ExecuteAsync(WithRetry, ct).ConfigureAwait(false);
        }

        return await WithRetry(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// ビルダーを作成
    /// </summary>
    public static ResiliencePipelineBuilder Create()
    {
        return new ResiliencePipelineBuilder();
    }

    public sealed class ResiliencePipelineBuilder
    {
        private int _maxRetries = 3;
        private TimeSpan _retryDelay = TimeSpan.FromMilliseconds(100);
        private TimeSpan _timeout = TimeSpan.FromSeconds(30);
        private CircuitBreaker? _circuitBreaker;

        public ResiliencePipelineBuilder WithRetry(int maxRetries, TimeSpan? delay = null)
        {
            _maxRetries = maxRetries;
            _retryDelay = delay ?? _retryDelay;
            return this;
        }

        public ResiliencePipelineBuilder WithTimeout(TimeSpan timeout)
        {
            _timeout = timeout;
            return this;
        }

        public ResiliencePipelineBuilder WithCircuitBreaker(int failureThreshold = 5, TimeSpan? openDuration = null)
        {
            _circuitBreaker = new CircuitBreaker(failureThreshold, openDuration);
            return this;
        }

        public ResiliencePipeline Build()
        {
            return new ResiliencePipeline(_maxRetries, _retryDelay, _timeout, _circuitBreaker);
        }
    }
}

/// <summary>
/// リトライ回数を使い果たした例外
/// </summary>
public sealed class RetryExhaustedException : Exception
{
    public RetryExhaustedException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}

/// <summary>
/// サーキットが開いている例外
/// </summary>
public sealed class CircuitBrokenException : Exception
{
    public CircuitBrokenException(string message) : base(message) { }
}
