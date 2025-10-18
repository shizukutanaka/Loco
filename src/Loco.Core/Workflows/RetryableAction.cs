using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Workflows
{
    /// <summary>
    /// Wraps an action to add retry and timeout capabilities.
    /// </summary>
    public class RetryableAction : IAction
    {
        private readonly IAction _innerAction;
        private readonly int _retryCount;
        private readonly TimeSpan _retryDelay;
        private readonly TimeSpan? _timeout;

        public string Id => _innerAction.Id;
        public string Name => _innerAction.Name;

        public RetryableAction(
            IAction innerAction,
            int retryCount = 0,
            TimeSpan? retryDelay = null,
            TimeSpan? timeout = null)
        {
            _innerAction = innerAction;
            _retryCount = Math.Max(0, retryCount);
            _retryDelay = retryDelay ?? TimeSpan.FromSeconds(2);
            _timeout = timeout;
        }

        public async Task<bool> ExecuteAsync(IActionContext context)
        {
            var attempt = 0;
            var maxAttempts = _retryCount + 1; // Original attempt + retries

            while (attempt < maxAttempts)
            {
                attempt++;

                try
                {
                    if (attempt > 1)
                    {
                        Console.WriteLine($"  ⟳ Retry attempt {attempt - 1}/{_retryCount}");
                        context.Logger?.LogInformation("Retrying action {ActionId}, attempt {Attempt}/{Max}",
                            Id, attempt - 1, _retryCount);
                    }

                    bool success;

                    // Execute with timeout if specified
                    if (_timeout.HasValue)
                    {
                        using var cts = new CancellationTokenSource(_timeout.Value);
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                            context.CancellationToken, cts.Token);

                        var originalToken = context.CancellationToken;

                        try
                        {
                            // Temporarily replace the cancellation token
                            var newContext = new ActionContext
                            {
                                Variables = context.Variables,
                                Logger = context.Logger,
                                CancellationToken = linkedCts.Token,
                                FlowId = context.FlowId,
                                ActionId = context.ActionId
                            };

                            var sw = Stopwatch.StartNew();
                            success = await _innerAction.ExecuteAsync(newContext);
                            sw.Stop();

                            if (sw.Elapsed > _timeout.Value)
                            {
                                Console.WriteLine($"  ⚠ Action exceeded timeout ({_timeout.Value.TotalSeconds}s)");
                            }
                        }
                        catch (OperationCanceledException) when (cts.IsCancellationRequested)
                        {
                            Console.WriteLine($"  ✗ Action timed out after {_timeout.Value.TotalSeconds}s");
                            context.Logger?.LogWarning("Action {ActionId} timed out after {Timeout}s",
                                Id, _timeout.Value.TotalSeconds);
                            success = false;
                        }
                    }
                    else
                    {
                        success = await _innerAction.ExecuteAsync(context);
                    }

                    if (success)
                    {
                        if (attempt > 1)
                        {
                            Console.WriteLine($"  ✓ Action succeeded on retry attempt {attempt - 1}");
                        }
                        return true;
                    }

                    // If not successful and we have more retries, wait before next attempt
                    if (attempt < maxAttempts)
                    {
                        // Exponential backoff
                        var delay = TimeSpan.FromMilliseconds(_retryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                        Console.WriteLine($"  ⏱ Waiting {delay.TotalSeconds:F1}s before retry...");
                        await Task.Delay(delay, context.CancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; // Don't retry on cancellation
                }
                catch (Exception ex)
                {
                    context.Logger?.LogError(ex, "Action {ActionId} failed on attempt {Attempt}", Id, attempt);

                    if (attempt >= maxAttempts)
                    {
                        Console.WriteLine($"  ✗ Action failed after {maxAttempts} attempts: {ex.Message}");
                        throw;
                    }
                }
            }

            Console.WriteLine($"  ✗ Action failed after {maxAttempts} attempts");
            return false;
        }
    }

    /// <summary>
    /// Simple action context implementation.
    /// </summary>
    public class ActionContext : IActionContext
    {
        public Dictionary<string, object?> Variables { get; set; } = new();
        public ILogger? Logger { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public string? FlowId { get; set; }
        public string? ActionId { get; set; }
    }
}
