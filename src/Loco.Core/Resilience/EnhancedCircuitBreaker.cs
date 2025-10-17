using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Resilience;

/// <summary>
/// Enhanced circuit breaker with half-open state and automatic recovery
/// Based on 2025 best practices: Netflix Hystrix, Polly patterns
/// サーキットブレーカー強化版 - 半開状態と自動回復
/// </summary>
public class EnhancedCircuitBreaker
{
    private readonly CircuitBreakerConfiguration _config;
    private readonly ILogger<EnhancedCircuitBreaker>? _logger;
    private readonly object _lock = new object();

    private CircuitState _state = CircuitState.Closed;
    private int _consecutiveFailures = 0;
    private int _successCount = 0;
    private DateTime? _openedAt;
    private DateTime? _lastAttemptAt;
    private Exception? _lastException;

    // Statistics
    private long _totalCalls = 0;
    private long _totalFailures = 0;
    private long _totalSuccesses = 0;
    private long _totalRejections = 0;

    public EnhancedCircuitBreaker(CircuitBreakerConfiguration? config = null, ILogger<EnhancedCircuitBreaker>? logger = null)
    {
        _config = config ?? CircuitBreakerConfiguration.Default;
        _logger = logger;
    }

    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    public CircuitBreakerStatistics GetStatistics()
    {
        lock (_lock)
        {
            return new CircuitBreakerStatistics
            {
                State = _state,
                ConsecutiveFailures = _consecutiveFailures,
                SuccessCount = _successCount,
                OpenedAt = _openedAt,
                LastAttemptAt = _lastAttemptAt,
                TotalCalls = _totalCalls,
                TotalFailures = _totalFailures,
                TotalSuccesses = _totalSuccesses,
                TotalRejections = _totalRejections,
                FailureRate = _totalCalls > 0 ? (double)_totalFailures / _totalCalls : 0,
                LastException = _lastException
            };
        }
    }

    /// <summary>
    /// Execute an action through the circuit breaker
    /// サーキットブレーカーを通してアクションを実行
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default,
        string? operationName = null)
    {
        BeforeExecution();

        try
        {
            var result = await action(cancellationToken);
            OnSuccess();
            return result;
        }
        catch (Exception ex)
        {
            OnFailure(ex, operationName);
            throw;
        }
    }

    /// <summary>
    /// Execute an action through the circuit breaker (no return value)
    /// サーキットブレーカーを通してアクションを実行（戻り値なし）
    /// </summary>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default,
        string? operationName = null)
    {
        await ExecuteAsync(async ct =>
        {
            await action(ct);
            return true;
        }, cancellationToken, operationName);
    }

    /// <summary>
    /// Execute an action through the circuit breaker (synchronous)
    /// サーキットブレーカーを通してアクションを実行（同期版）
    /// </summary>
    public T Execute<T>(Func<T> action, string? operationName = null)
    {
        return ExecuteAsync(_ => Task.FromResult(action()), default, operationName).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Reset the circuit breaker to closed state
    /// サーキットブレーカーを閉状態にリセット
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _state = CircuitState.Closed;
            _consecutiveFailures = 0;
            _successCount = 0;
            _openedAt = null;
            _lastException = null;

            _logger?.LogInformation("Circuit breaker reset to Closed state");
        }
    }

    private void BeforeExecution()
    {
        lock (_lock)
        {
            _totalCalls++;
            _lastAttemptAt = DateTime.UtcNow;

            switch (_state)
            {
                case CircuitState.Open:
                    // Check if we should transition to half-open
                    if (_openedAt.HasValue && DateTime.UtcNow - _openedAt.Value >= _config.OpenDuration)
                    {
                        TransitionTo(CircuitState.HalfOpen);
                        _logger?.LogInformation("Circuit breaker transitioning from Open to HalfOpen");
                    }
                    else
                    {
                        _totalRejections++;
                        _logger?.LogWarning("Circuit breaker is Open. Rejecting call.");
                        throw new CircuitBreakerOpenException("Circuit breaker is open", _lastException);
                    }
                    break;

                case CircuitState.HalfOpen:
                    // Allow limited calls in half-open state
                    if (_successCount >= _config.HalfOpenSuccessThreshold)
                    {
                        // Too many calls in half-open, reject
                        _totalRejections++;
                        throw new CircuitBreakerOpenException("Circuit breaker is in half-open state and threshold reached", _lastException);
                    }
                    break;

                case CircuitState.Closed:
                    // Normal operation
                    break;
            }
        }
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            _totalSuccesses++;

            switch (_state)
            {
                case CircuitState.HalfOpen:
                    _successCount++;
                    if (_successCount >= _config.HalfOpenSuccessThreshold)
                    {
                        // Enough successes, close the circuit
                        TransitionTo(CircuitState.Closed);
                        _consecutiveFailures = 0;
                        _successCount = 0;
                        _logger?.LogInformation("Circuit breaker closing after {SuccessCount} successful calls", _successCount);
                    }
                    break;

                case CircuitState.Closed:
                    // Reset failure counter on success
                    if (_consecutiveFailures > 0)
                    {
                        _consecutiveFailures = 0;
                    }
                    break;
            }
        }
    }

    private void OnFailure(Exception ex, string? operationName)
    {
        lock (_lock)
        {
            _totalFailures++;
            _consecutiveFailures++;
            _lastException = ex;

            _logger?.LogWarning(ex, "Circuit breaker detected failure for operation {Operation}", operationName ?? "unknown");

            switch (_state)
            {
                case CircuitState.HalfOpen:
                    // Failure in half-open, reopen immediately
                    TransitionTo(CircuitState.Open);
                    _openedAt = DateTime.UtcNow;
                    _successCount = 0;
                    _logger?.LogWarning("Circuit breaker reopening due to failure in HalfOpen state");
                    break;

                case CircuitState.Closed:
                    // Check if we should open
                    if (_consecutiveFailures >= _config.FailureThreshold)
                    {
                        TransitionTo(CircuitState.Open);
                        _openedAt = DateTime.UtcNow;
                        _logger?.LogError(
                            "Circuit breaker opening after {FailureCount} consecutive failures. Will retry in {Duration}",
                            _consecutiveFailures,
                            _config.OpenDuration);
                    }
                    break;
            }
        }
    }

    private void TransitionTo(CircuitState newState)
    {
        var oldState = _state;
        _state = newState;

        _config.OnStateChange?.Invoke(oldState, newState);
    }
}

/// <summary>
/// Circuit breaker configuration
/// サーキットブレーカー設定
/// </summary>
public class CircuitBreakerConfiguration
{
    /// <summary>
    /// Number of consecutive failures before opening the circuit
    /// サーキットを開くまでの連続失敗回数
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Duration to keep circuit open before transitioning to half-open
    /// サーキットを開いたままにする期間（半開状態に移行するまで）
    /// </summary>
    public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Number of successful calls in half-open state before closing
    /// 半開状態でサーキットを閉じるまでの成功呼び出し回数
    /// </summary>
    public int HalfOpenSuccessThreshold { get; set; } = 2;

    /// <summary>
    /// Timeout for calls in half-open state
    /// 半開状態での呼び出しタイムアウト
    /// </summary>
    public TimeSpan HalfOpenTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Callback when circuit state changes
    /// サーキット状態が変化した時のコールバック
    /// </summary>
    public Action<CircuitState, CircuitState>? OnStateChange { get; set; }

    public static CircuitBreakerConfiguration Default => new();

    public static CircuitBreakerConfiguration Aggressive => new()
    {
        FailureThreshold = 3,
        OpenDuration = TimeSpan.FromSeconds(30),
        HalfOpenSuccessThreshold = 1
    };

    public static CircuitBreakerConfiguration Conservative => new()
    {
        FailureThreshold = 10,
        OpenDuration = TimeSpan.FromMinutes(2),
        HalfOpenSuccessThreshold = 5
    };
}

/// <summary>
/// Circuit breaker state
/// サーキットブレーカーの状態
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is closed, calls are allowed
    /// サーキット閉 - 呼び出し許可
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open, calls are rejected
    /// サーキット開 - 呼び出し拒否
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is testing recovery, limited calls allowed
    /// サーキット半開 - 制限付きで呼び出し許可（回復テスト中）
    /// </summary>
    HalfOpen
}

/// <summary>
/// Exception thrown when circuit breaker is open
/// サーキットブレーカーが開いている時にスローされる例外
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Circuit breaker statistics
/// サーキットブレーカー統計
/// </summary>
public class CircuitBreakerStatistics
{
    public CircuitState State { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int SuccessCount { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public long TotalCalls { get; set; }
    public long TotalFailures { get; set; }
    public long TotalSuccesses { get; set; }
    public long TotalRejections { get; set; }
    public double FailureRate { get; set; }
    public Exception? LastException { get; set; }

    public TimeSpan? TimeSinceOpened => OpenedAt.HasValue ? DateTime.UtcNow - OpenedAt.Value : null;
    public TimeSpan? TimeSinceLastAttempt => LastAttemptAt.HasValue ? DateTime.UtcNow - LastAttemptAt.Value : null;
}
