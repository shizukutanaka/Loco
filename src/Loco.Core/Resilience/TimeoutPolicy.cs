using System;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Resilience
{
    /// <summary>
    /// Provides timeout enforcement for operations to prevent hung processes.
    /// Essential for government-grade reliability requirements.
    /// </summary>
    public class TimeoutPolicy
    {
        private readonly TimeSpan _timeout;
        private readonly string _operationName;

        public TimeoutPolicy(TimeSpan timeout, string operationName = "Operation")
        {
            _timeout = timeout;
            _operationName = operationName;
        }

        /// <summary>
        /// Executes an async operation with timeout enforcement.
        /// </summary>
        /// <typeparam name="TResult">Return type of the operation</typeparam>
        /// <param name="operation">The operation to execute</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The result of the operation</returns>
        /// <exception cref="TimeoutException">Thrown when operation exceeds timeout</exception>
        public async Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            try
            {
                return await operation(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"{_operationName} exceeded timeout of {_timeout.TotalSeconds:F1} seconds");
            }
        }

        /// <summary>
        /// Executes an async operation with timeout enforcement (no return value).
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <exception cref="TimeoutException">Thrown when operation exceeds timeout</exception>
        public async Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            try
            {
                await operation(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"{_operationName} exceeded timeout of {_timeout.TotalSeconds:F1} seconds");
            }
        }

        /// <summary>
        /// Creates a new timeout policy with specified timeout.
        /// </summary>
        public static TimeoutPolicy Create(TimeSpan timeout, string operationName = "Operation")
        {
            return new TimeoutPolicy(timeout, operationName);
        }

        /// <summary>
        /// Creates a new timeout policy with timeout in seconds.
        /// </summary>
        public static TimeoutPolicy CreateSeconds(int seconds, string operationName = "Operation")
        {
            return new TimeoutPolicy(TimeSpan.FromSeconds(seconds), operationName);
        }
    }
}
