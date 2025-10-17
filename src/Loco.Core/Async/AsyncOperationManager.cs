using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Async
{
    /// <summary>
    /// 高性能非同期操作マネージャー
    /// タイムアウト処理、キャンセル処理、リソース管理を提供
    /// </summary>
    public class AsyncOperationManager : IDisposable
    {
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, AsyncOperation> _operations = new();
        private readonly Timer _cleanupTimer;
        private readonly CancellationTokenSource _globalCancellationTokenSource = new();
        private bool _disposed;

        /// <summary>
        /// 実行中の操作数
        /// </summary>
        public int ActiveOperationCount => _operations.Count;

        /// <summary>
        /// コンストラクター
        /// </summary>
        /// <param name="logger">ロガー</param>
        public AsyncOperationManager(ILogger? logger = null)
        {
            _logger = logger;
            _cleanupTimer = new Timer(CleanupCompletedOperations, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            _logger?.LogInformation("AsyncOperationManager initialized");
        }

        /// <summary>
        /// 非同期操作を開始
        /// </summary>
        /// <typeparam name="T">操作の結果の型</typeparam>
        /// <param name="operationId">操作ID</param>
        /// <param name="operation">実行する操作</param>
        /// <param name="timeout">タイムアウト時間</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>操作結果</returns>
        public async Task<T> StartOperationAsync<T>(
            string operationId,
            Func<CancellationToken, Task<T>> operation,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(operationId))
                throw new ArgumentException("Operation ID cannot be null or empty", nameof(operationId));
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                _globalCancellationTokenSource.Token, cancellationToken);

            if (timeout.HasValue)
            {
                linkedTokenSource.CancelAfter(timeout.Value);
            }

            var asyncOperation = new AsyncOperation
            {
                Id = operationId,
                StartTime = DateTime.UtcNow,
                Status = OperationStatus.Running,
                CancellationTokenSource = linkedTokenSource
            };

            if (!_operations.TryAdd(operationId, asyncOperation))
            {
                throw new InvalidOperationException($"Operation with ID '{operationId}' is already running");
            }

            try
            {
                _logger?.LogDebug("Starting async operation: {OperationId}", operationId);

                var result = await operation(linkedTokenSource.Token);

                asyncOperation.Status = OperationStatus.Completed;
                asyncOperation.EndTime = DateTime.UtcNow;
                asyncOperation.Result = result;

                _logger?.LogDebug("Async operation completed: {OperationId}", operationId);

                return result;
            }
            catch (OperationCanceledException) when (linkedTokenSource.IsCancellationRequested)
            {
                asyncOperation.Status = OperationStatus.Cancelled;
                asyncOperation.EndTime = DateTime.UtcNow;

                _logger?.LogWarning("Async operation cancelled: {OperationId}", operationId);
                throw;
            }
            catch (Exception ex)
            {
                asyncOperation.Status = OperationStatus.Faulted;
                asyncOperation.EndTime = DateTime.UtcNow;
                asyncOperation.Exception = ex;

                _logger?.LogError(ex, "Async operation faulted: {OperationId}", operationId);
                throw;
            }
            finally
            {
                linkedTokenSource.Dispose();
            }
        }

        /// <summary>
        /// 非同期操作を開始（戻り値なし）
        /// </summary>
        /// <param name="operationId">操作ID</param>
        /// <param name="operation">実行する操作</param>
        /// <param name="timeout">タイムアウト時間</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async Task StartOperationAsync(
            string operationId,
            Func<CancellationToken, Task> operation,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            await StartOperationAsync(operationId, async (token) =>
            {
                await operation(token);
                return true;
            }, timeout, cancellationToken);
        }

        /// <summary>
        /// 操作をキャンセル
        /// </summary>
        /// <param name="operationId">操作ID</param>
        /// <returns>キャンセルが成功したかどうか</returns>
        public bool CancelOperation(string operationId)
        {
            if (_operations.TryGetValue(operationId, out var operation))
            {
                operation.CancellationTokenSource?.Cancel();
                _logger?.LogInformation("Cancelled async operation: {OperationId}", operationId);
                return true;
            }

            _logger?.LogWarning("Failed to cancel operation - not found: {OperationId}", operationId);
            return false;
        }

        /// <summary>
        /// すべての操作をキャンセル
        /// </summary>
        public void CancelAllOperations()
        {
            foreach (var operation in _operations.Values)
            {
                operation.CancellationTokenSource?.Cancel();
            }

            _logger?.LogInformation("Cancelled all async operations");
        }

        /// <summary>
        /// 操作のステータスを取得
        /// </summary>
        /// <param name="operationId">操作ID</param>
        /// <returns>操作情報</returns>
        public AsyncOperation? GetOperation(string operationId)
        {
            return _operations.TryGetValue(operationId, out var operation) ? operation : null;
        }

        /// <summary>
        /// すべての操作を取得
        /// </summary>
        /// <returns>すべての操作のリスト</returns>
        public IReadOnlyList<AsyncOperation> GetAllOperations()
        {
            return _operations.Values.ToList();
        }

        /// <summary>
        /// 完了した操作を削除
        /// </summary>
        /// <param name="operationId">操作ID</param>
        /// <returns>削除が成功したかどうか</returns>
        public bool RemoveCompletedOperation(string operationId)
        {
            if (_operations.TryGetValue(operationId, out var operation) &&
                operation.Status != OperationStatus.Running)
            {
                return _operations.TryRemove(operationId, out _);
            }

            return false;
        }

        /// <summary>
        /// 指定された時間以上経過した完了操作を削除
        /// </summary>
        /// <param name="maxAge">最大保持時間</param>
        /// <returns>削除された操作数</returns>
        public int CleanupOperationsOlderThan(TimeSpan maxAge)
        {
            var cutoffTime = DateTime.UtcNow - maxAge;
            var toRemove = _operations.Values
                .Where(op => op.Status != OperationStatus.Running && op.EndTime < cutoffTime)
                .Select(op => op.Id)
                .ToList();

            var removedCount = 0;
            foreach (var operationId in toRemove)
            {
                if (_operations.TryRemove(operationId, out _))
                {
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                _logger?.LogDebug("Cleaned up {RemovedCount} completed operations older than {MaxAge}",
                    removedCount, maxAge);
            }

            return removedCount;
        }

        /// <summary>
        /// 操作の統計情報を取得
        /// </summary>
        /// <returns>統計情報</returns>
        public AsyncOperationStats GetStats()
        {
            var operations = _operations.Values.ToList();

            return new AsyncOperationStats
            {
                TotalOperations = operations.Count,
                RunningOperations = operations.Count(op => op.Status == OperationStatus.Running),
                CompletedOperations = operations.Count(op => op.Status == OperationStatus.Completed),
                CancelledOperations = operations.Count(op => op.Status == OperationStatus.Cancelled),
                FaultedOperations = operations.Count(op => op.Status == OperationStatus.Faulted),
                AverageExecutionTime = operations
                    .Where(op => op.Status == OperationStatus.Completed && op.EndTime.HasValue)
                    .Select(op => op.EndTime.Value - op.StartTime)
                    .DefaultIfEmpty(TimeSpan.Zero)
                    .Average(ts => ts.TotalMilliseconds),
                LongestRunningOperation = operations
                    .Where(op => op.Status == OperationStatus.Running)
                    .OrderByDescending(op => DateTime.UtcNow - op.StartTime)
                    .FirstOrDefault()
            };
        }

        private void CleanupCompletedOperations(object? state)
        {
            try
            {
                CleanupOperationsOlderThan(TimeSpan.FromHours(1));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during operation cleanup");
            }
        }

        /// <summary>
        /// リソースを解放
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _globalCancellationTokenSource.Cancel();
            CancelAllOperations();
            _cleanupTimer.Dispose();
            _globalCancellationTokenSource.Dispose();
            _disposed = true;

            _logger?.LogInformation("AsyncOperationManager disposed");
        }
    }

    /// <summary>
    /// 非同期操作の情報
    /// </summary>
    public class AsyncOperation
    {
        public string Id { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public OperationStatus Status { get; set; }
        public object? Result { get; set; }
        public Exception? Exception { get; set; }
        public CancellationTokenSource? CancellationTokenSource { get; set; }
    }

    /// <summary>
    /// 操作ステータス
    /// </summary>
    public enum OperationStatus
    {
        Running,
        Completed,
        Cancelled,
        Faulted
    }

    /// <summary>
    /// 非同期操作の統計情報
    /// </summary>
    public class AsyncOperationStats
    {
        public int TotalOperations { get; set; }
        public int RunningOperations { get; set; }
        public int CompletedOperations { get; set; }
        public int CancelledOperations { get; set; }
        public int FaultedOperations { get; set; }
        public double AverageExecutionTime { get; set; }
        public AsyncOperation? LongestRunningOperation { get; set; }
    }
}
