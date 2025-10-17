using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Loco.Core.Resilience;

/// <summary>
/// 高度なエラーハンドラー
/// Advanced Error Handler with Retry Logic and Dead Letter Queue (2025 best practices)
///
/// 機能: リトライロジック、指数バックオフ、デッドレターキュー、サーキットブレーカー
/// Features: Retry logic, exponential backoff, dead letter queue, circuit breaker
///
/// 市場調査結果: リトライは一時的障害のみ、DLQで障害メッセージを分離
/// Market research: Retry only for transient failures, DLQ isolates problematic messages
/// </summary>
public class AdvancedErrorHandler
{
    private readonly ConcurrentQueue<FailedMessage> _deadLetterQueue;
    private readonly ConcurrentDictionary<string, RetryStatistics> _retryStats;
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitBreakers;
    private readonly RetryPolicy _defaultPolicy;

    public AdvancedErrorHandler(RetryPolicy? defaultPolicy = null)
    {
        _deadLetterQueue = new ConcurrentQueue<FailedMessage>();
        _retryStats = new ConcurrentDictionary<string, RetryStatistics>();
        _circuitBreakers = new ConcurrentDictionary<string, CircuitBreakerState>();
        _defaultPolicy = defaultPolicy ?? RetryPolicy.Default();
    }

    /// <summary>
    /// リトライポリシー
    /// Retry policy configuration
    /// </summary>
    public class RetryPolicy
    {
        public int MaxRetries { get; set; } = 3;
        public RetryStrategy Strategy { get; set; } = RetryStrategy.ExponentialBackoff;
        public int InitialDelayMs { get; set; } = 1000;
        public int MaxDelayMs { get; set; } = 30000;
        public double BackoffMultiplier { get; set; } = 2.0;
        public bool JitterEnabled { get; set; } = true;
        public List<Type> RetriableExceptions { get; set; } = new();
        public Func<Exception, bool>? CustomRetryCondition { get; set; }

        public static RetryPolicy Default() => new RetryPolicy
        {
            RetriableExceptions = new List<Type>
            {
                typeof(TimeoutException),
                typeof(TaskCanceledException),
                typeof(System.Net.Http.HttpRequestException)
            }
        };

        public static RetryPolicy Immediate(int maxRetries = 3) => new RetryPolicy
        {
            MaxRetries = maxRetries,
            Strategy = RetryStrategy.Immediate
        };

        public static RetryPolicy FixedDelay(int delayMs, int maxRetries = 3) => new RetryPolicy
        {
            MaxRetries = maxRetries,
            Strategy = RetryStrategy.FixedDelay,
            InitialDelayMs = delayMs
        };

        public static RetryPolicy ExponentialBackoff(int initialDelayMs = 1000, int maxRetries = 5) => new RetryPolicy
        {
            MaxRetries = maxRetries,
            Strategy = RetryStrategy.ExponentialBackoff,
            InitialDelayMs = initialDelayMs
        };
    }

    public enum RetryStrategy
    {
        Immediate,          // 即座にリトライ
        FixedDelay,         // 固定遅延
        ExponentialBackoff, // 指数バックオフ
        LinearBackoff       // 線形バックオフ
    }

    /// <summary>
    /// 失敗したメッセージ
    /// Failed message in Dead Letter Queue
    /// </summary>
    public class FailedMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime FirstFailedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastFailedAt { get; set; } = DateTime.UtcNow;
        public int FailureCount { get; set; }
        public string WorkflowId { get; set; } = string.Empty;
        public string StepId { get; set; } = string.Empty;
        public object? Payload { get; set; }
        public List<FailureRecord> Failures { get; set; } = new();
        public DLQStatus Status { get; set; } = DLQStatus.Pending;
        public string? Resolution { get; set; }
    }

    public enum DLQStatus
    {
        Pending,    // 未処理
        Analyzing,  // 分析中
        Resolved,   // 解決済み
        Requeued,   // 再キュー
        Discarded   // 破棄
    }

    /// <summary>
    /// 失敗記録
    /// Individual failure record
    /// </summary>
    public class FailureRecord
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string ExceptionType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public int AttemptNumber { get; set; }
    }

    /// <summary>
    /// リトライ統計
    /// Retry statistics
    /// </summary>
    public class RetryStatistics
    {
        public string WorkflowId { get; set; } = string.Empty;
        public int TotalAttempts { get; set; }
        public int SuccessfulRetries { get; set; }
        public int FailedRetries { get; set; }
        public DateTime FirstAttempt { get; set; } = DateTime.UtcNow;
        public DateTime LastAttempt { get; set; } = DateTime.UtcNow;
        public double SuccessRate => TotalAttempts > 0 ? (double)SuccessfulRetries / TotalAttempts * 100 : 0;
    }

    /// <summary>
    /// サーキットブレーカーの状態
    /// Circuit breaker state
    /// </summary>
    public class CircuitBreakerState
    {
        public string ServiceId { get; set; } = string.Empty;
        public CircuitState State { get; set; } = CircuitState.Closed;
        public int FailureCount { get; set; }
        public int FailureThreshold { get; set; } = 5;
        public DateTime? OpenedAt { get; set; }
        public TimeSpan ResetTimeout { get; set; } = TimeSpan.FromMinutes(1);
        public DateTime? LastFailure { get; set; }
        public DateTime? LastSuccess { get; set; }
    }

    public enum CircuitState
    {
        Closed,     // 正常動作
        Open,       // サービス停止（リクエストブロック）
        HalfOpen    // 復旧テスト中
    }

    /// <summary>
    /// リトライ付きで実行
    /// Execute with retry logic
    /// </summary>
    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string workflowId,
        RetryPolicy? policy = null)
    {
        policy ??= _defaultPolicy;
        var stats = _retryStats.GetOrAdd(workflowId, _ => new RetryStatistics { WorkflowId = workflowId });

        for (int attempt = 1; attempt <= policy.MaxRetries + 1; attempt++)
        {
            try
            {
                stats.TotalAttempts++;
                stats.LastAttempt = DateTime.UtcNow;

                var result = await operation().ConfigureAwait(false);

                if (attempt > 1)
                {
                    stats.SuccessfulRetries++;
                }

                return result;
            }
            catch (Exception ex)
            {
                stats.FailedRetries++;

                // Check if exception is retriable
                if (!IsRetriable(ex, policy))
                {
                    // Non-retriable error - send to DLQ immediately
                    await SendToDeadLetterQueueAsync(workflowId, "", null, ex, attempt);
                    throw;
                }

                // Last attempt - send to DLQ
                if (attempt > policy.MaxRetries)
                {
                    await SendToDeadLetterQueueAsync(workflowId, "", null, ex, attempt);
                    throw;
                }

                // Calculate delay
                var delay = CalculateDelay(attempt, policy);
                await Task.Delay(delay).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Should not reach here");
    }

    /// <summary>
    /// 例外がリトライ可能かチェック
    /// Check if exception is retriable
    /// </summary>
    private bool IsRetriable(Exception ex, RetryPolicy policy)
    {
        // Custom condition
        if (policy.CustomRetryCondition != null)
        {
            return policy.CustomRetryCondition(ex);
        }

        // Check against retriable exceptions list
        var exceptionType = ex.GetType();
        return policy.RetriableExceptions.Any(t => t.IsAssignableFrom(exceptionType));
    }

    /// <summary>
    /// リトライ遅延を計算
    /// Calculate retry delay
    /// </summary>
    private int CalculateDelay(int attempt, RetryPolicy policy)
    {
        int delay = policy.Strategy switch
        {
            RetryStrategy.Immediate => 0,
            RetryStrategy.FixedDelay => policy.InitialDelayMs,
            RetryStrategy.ExponentialBackoff => (int)(policy.InitialDelayMs * Math.Pow(policy.BackoffMultiplier, attempt - 1)),
            RetryStrategy.LinearBackoff => policy.InitialDelayMs * attempt,
            _ => policy.InitialDelayMs
        };

        // Cap at max delay
        delay = Math.Min(delay, policy.MaxDelayMs);

        // Add jitter to prevent thundering herd
        if (policy.JitterEnabled)
        {
            var jitter = Random.Shared.Next(0, (int)(delay * 0.2)); // +/- 20% jitter
            delay += jitter - (int)(delay * 0.1);
        }

        return Math.Max(0, delay);
    }

    /// <summary>
    /// デッドレターキューに送信
    /// Send to Dead Letter Queue
    /// </summary>
    public async Task SendToDeadLetterQueueAsync(
        string workflowId,
        string stepId,
        object? payload,
        Exception exception,
        int attemptNumber)
    {
        var failedMessage = new FailedMessage
        {
            WorkflowId = workflowId,
            StepId = stepId,
            Payload = payload,
            FailureCount = attemptNumber,
            Failures = new List<FailureRecord>
            {
                new FailureRecord
                {
                    ExceptionType = exception.GetType().Name,
                    Message = exception.Message,
                    StackTrace = exception.StackTrace,
                    AttemptNumber = attemptNumber
                }
            }
        };

        _deadLetterQueue.Enqueue(failedMessage);

        // In real implementation, persist to database and send alerts
        await Task.CompletedTask;
    }

    /// <summary>
    /// サーキットブレーカーを使用して実行
    /// Execute with circuit breaker
    /// </summary>
    public async Task<T> ExecuteWithCircuitBreakerAsync<T>(
        Func<Task<T>> operation,
        string serviceId,
        int failureThreshold = 5,
        TimeSpan? resetTimeout = null)
    {
        var breaker = _circuitBreakers.GetOrAdd(serviceId, _ => new CircuitBreakerState
        {
            ServiceId = serviceId,
            FailureThreshold = failureThreshold,
            ResetTimeout = resetTimeout ?? TimeSpan.FromMinutes(1)
        });

        // Check circuit state
        switch (breaker.State)
        {
            case CircuitState.Open:
                // Check if reset timeout has passed
                if (breaker.OpenedAt.HasValue &&
                    DateTime.UtcNow - breaker.OpenedAt.Value > breaker.ResetTimeout)
                {
                    breaker.State = CircuitState.HalfOpen;
                }
                else
                {
                    throw new CircuitBreakerOpenException($"Circuit breaker is open for {serviceId}");
                }
                break;

            case CircuitState.HalfOpen:
                // Allow one test request
                break;

            case CircuitState.Closed:
                // Normal operation
                break;
        }

        try
        {
            var result = await operation().ConfigureAwait(false);

            // Success - reset failure count
            breaker.FailureCount = 0;
            breaker.LastSuccess = DateTime.UtcNow;

            if (breaker.State == CircuitState.HalfOpen)
            {
                breaker.State = CircuitState.Closed;
            }

            return result;
        }
        catch (Exception)
        {
            breaker.FailureCount++;
            breaker.LastFailure = DateTime.UtcNow;

            if (breaker.State == CircuitState.HalfOpen)
            {
                // Failed during test - reopen circuit
                breaker.State = CircuitState.Open;
                breaker.OpenedAt = DateTime.UtcNow;
            }
            else if (breaker.FailureCount >= breaker.FailureThreshold)
            {
                // Too many failures - open circuit
                breaker.State = CircuitState.Open;
                breaker.OpenedAt = DateTime.UtcNow;
            }

            throw;
        }
    }

    /// <summary>
    /// DLQメッセージを再処理
    /// Reprocess DLQ message
    /// </summary>
    public async Task<bool> ReprocessDLQMessageAsync(string messageId, Func<object?, Task<bool>> processor)
    {
        var message = _deadLetterQueue.FirstOrDefault(m => m.Id == messageId);
        if (message == null)
        {
            return false;
        }

        try
        {
            var success = await processor(message.Payload).ConfigureAwait(false);

            if (success)
            {
                message.Status = DLQStatus.Resolved;
                message.Resolution = "Successfully reprocessed";
            }

            return success;
        }
        catch (Exception ex)
        {
            message.Failures.Add(new FailureRecord
            {
                ExceptionType = ex.GetType().Name,
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                AttemptNumber = message.FailureCount + 1
            });
            message.FailureCount++;
            message.LastFailedAt = DateTime.UtcNow;

            return false;
        }
    }

    /// <summary>
    /// DLQ統計を取得
    /// Get DLQ statistics
    /// </summary>
    public DLQStatistics GetDLQStatistics()
    {
        var messages = _deadLetterQueue.ToList();

        return new DLQStatistics
        {
            TotalMessages = messages.Count,
            PendingMessages = messages.Count(m => m.Status == DLQStatus.Pending),
            ResolvedMessages = messages.Count(m => m.Status == DLQStatus.Resolved),
            DiscardedMessages = messages.Count(m => m.Status == DLQStatus.Discarded),
            OldestMessage = messages.OrderBy(m => m.FirstFailedAt).FirstOrDefault()?.FirstFailedAt,
            MostCommonError = messages
                .SelectMany(m => m.Failures)
                .GroupBy(f => f.ExceptionType)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key
        };
    }

    public class DLQStatistics
    {
        public int TotalMessages { get; set; }
        public int PendingMessages { get; set; }
        public int ResolvedMessages { get; set; }
        public int DiscardedMessages { get; set; }
        public DateTime? OldestMessage { get; set; }
        public string? MostCommonError { get; set; }
    }

    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message) : base(message) { }
    }
}
