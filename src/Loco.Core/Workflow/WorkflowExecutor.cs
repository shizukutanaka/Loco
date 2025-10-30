using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Workflow
{
    /// <summary>
    /// Executes cross-platform workflows with error handling, retry logic, and observability.
    /// クロスプラットフォームワークフローエグゼキューター
    ///
    /// Solves Issues:
    /// - #8: Complex processing support (条件分岐、ループ対応)
    /// - #9: Error handling and retry (エラーハンドリング、リトライ)
    /// - #10: Performance issues (最適化済み実行)
    /// </summary>
    public class WorkflowExecutor
    {
        private readonly ILogger<WorkflowExecutor> _logger;
        private readonly WorkflowValidator _validator;
        private readonly Dictionary<string, IPlatformProvider> _platformProviders;

        public WorkflowExecutor(ILogger<WorkflowExecutor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _validator = new WorkflowValidator();
            _platformProviders = new Dictionary<string, IPlatformProvider>();
        }

        /// <summary>
        /// Registers a platform provider for workflow execution.
        /// </summary>
        public void RegisterPlatformProvider(IPlatformProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            _platformProviders[provider.Platform] = provider;
            _logger.LogInformation("Registered platform provider: {Platform}", provider.Platform);
        }

        /// <summary>
        /// Validates and executes a workflow.
        /// </summary>
        public async Task<WorkflowExecutionResult> ExecuteAsync(
            WorkflowDefinition workflow,
            CancellationToken cancellationToken = default)
        {
            var executionId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Starting workflow execution: {WorkflowId} (ExecutionId: {ExecutionId})",
                workflow.Id, executionId);

            try
            {
                // 1. Validation
                var validation = _validator.Validate(workflow);
                if (!validation.IsValid)
                {
                    _logger.LogError("Workflow validation failed: {Errors}",
                        string.Join(", ", validation.Errors));

                    return new WorkflowExecutionResult
                    {
                        ExecutionId = executionId,
                        WorkflowId = workflow.Id,
                        Success = false,
                        ErrorMessage = $"Validation failed: {string.Join(", ", validation.Errors)}",
                        Duration = stopwatch.Elapsed
                    };
                }

                if (validation.Warnings.Count > 0)
                {
                    _logger.LogWarning("Workflow has warnings: {Warnings}",
                        string.Join(", ", validation.Warnings));
                }

                // 2. Check if workflow is enabled
                if (!workflow.Enabled)
                {
                    _logger.LogInformation("Workflow is disabled, skipping execution: {WorkflowId}",
                        workflow.Id);

                    return new WorkflowExecutionResult
                    {
                        ExecutionId = executionId,
                        WorkflowId = workflow.Id,
                        Success = true,
                        Skipped = true,
                        SkipReason = "Workflow is disabled",
                        Duration = stopwatch.Elapsed
                    };
                }

                // 3. Get current platform provider
                var currentPlatform = GetCurrentPlatform();
                if (!_platformProviders.TryGetValue(currentPlatform, out var provider))
                {
                    throw new InvalidOperationException(
                        $"No platform provider registered for: {currentPlatform}");
                }

                // 4. Check if workflow supports current platform
                if (!workflow.Platforms.Any(p =>
                    string.Equals(p, currentPlatform, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning(
                        "Workflow does not support current platform: {Platform}. Supported: {Supported}",
                        currentPlatform, string.Join(", ", workflow.Platforms));

                    return new WorkflowExecutionResult
                    {
                        ExecutionId = executionId,
                        WorkflowId = workflow.Id,
                        Success = false,
                        ErrorMessage = $"Workflow does not support platform: {currentPlatform}",
                        Duration = stopwatch.Elapsed
                    };
                }

                // 5. Evaluate constraints
                var constraintsPassed = await EvaluateConstraintsAsync(
                    workflow, provider, cancellationToken);

                if (!constraintsPassed)
                {
                    _logger.LogInformation(
                        "Workflow constraints not met, skipping execution: {WorkflowId}",
                        workflow.Id);

                    return new WorkflowExecutionResult
                    {
                        ExecutionId = executionId,
                        WorkflowId = workflow.Id,
                        Success = true,
                        Skipped = true,
                        SkipReason = "Constraints not met",
                        Duration = stopwatch.Elapsed
                    };
                }

                // 6. Execute actions
                var actionResults = new List<ActionExecutionResult>();
                var context = new ActionContext
                {
                    WorkflowId = workflow.Id,
                    ExecutionId = executionId,
                    Variables = new Dictionary<string, object>()
                };

                foreach (var action in workflow.Actions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var actionResult = await ExecuteActionWithRetryAsync(
                        action, provider, context, cancellationToken);

                    actionResults.Add(actionResult);

                    // Store action output in context for next actions
                    if (actionResult.Success && actionResult.OutputData != null)
                    {
                        context.Variables[$"action_{action.Id}_output"] = actionResult.OutputData;
                    }

                    // Handle action failure based on error strategy
                    if (!actionResult.Success && action.OnError != null)
                    {
                        switch (action.OnError.Strategy)
                        {
                            case "stop":
                                _logger.LogError(
                                    "Action failed with 'stop' strategy, halting workflow: {ActionId}",
                                    action.Id);

                                stopwatch.Stop();
                                return new WorkflowExecutionResult
                                {
                                    ExecutionId = executionId,
                                    WorkflowId = workflow.Id,
                                    Success = false,
                                    ErrorMessage = $"Action {action.Id} failed: {actionResult.ErrorMessage}",
                                    ActionResults = actionResults,
                                    Duration = stopwatch.Elapsed
                                };

                            case "continue":
                                _logger.LogWarning(
                                    "Action failed with 'continue' strategy, proceeding to next action: {ActionId}",
                                    action.Id);
                                break;

                            case "fallback":
                                if (action.OnError.FallbackAction != null)
                                {
                                    _logger.LogInformation(
                                        "Executing fallback action for: {ActionId}",
                                        action.Id);

                                    var fallbackResult = await ExecuteActionWithRetryAsync(
                                        action.OnError.FallbackAction,
                                        provider,
                                        context,
                                        cancellationToken);

                                    actionResults.Add(fallbackResult);
                                }
                                break;
                        }
                    }
                }

                stopwatch.Stop();

                var allSuccessful = actionResults.All(r => r.Success);

                _logger.LogInformation(
                    "Workflow execution completed: {WorkflowId}, Success: {Success}, Duration: {Duration}ms",
                    workflow.Id, allSuccessful, stopwatch.ElapsedMilliseconds);

                return new WorkflowExecutionResult
                {
                    ExecutionId = executionId,
                    WorkflowId = workflow.Id,
                    Success = allSuccessful,
                    ActionResults = actionResults,
                    Duration = stopwatch.Elapsed
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Workflow execution cancelled: {WorkflowId}", workflow.Id);
                stopwatch.Stop();

                return new WorkflowExecutionResult
                {
                    ExecutionId = executionId,
                    WorkflowId = workflow.Id,
                    Success = false,
                    ErrorMessage = "Execution cancelled",
                    Duration = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Workflow execution failed: {WorkflowId}", workflow.Id);
                stopwatch.Stop();

                return new WorkflowExecutionResult
                {
                    ExecutionId = executionId,
                    WorkflowId = workflow.Id,
                    Success = false,
                    ErrorMessage = ex.Message,
                    Exception = ex,
                    Duration = stopwatch.Elapsed
                };
            }
        }

        private async Task<bool> EvaluateConstraintsAsync(
            WorkflowDefinition workflow,
            IPlatformProvider provider,
            CancellationToken cancellationToken)
        {
            if (workflow.Constraints == null || workflow.Constraints.Count == 0)
            {
                return true; // No constraints = always pass
            }

            foreach (var constraint in workflow.Constraints)
            {
                var result = await provider.EvaluateConstraintAsync(constraint, cancellationToken);
                if (!result)
                {
                    _logger.LogDebug("Constraint not met: {ConstraintType}", constraint.Type);
                    return false;
                }
            }

            return true;
        }

        private async Task<ActionExecutionResult> ExecuteActionWithRetryAsync(
            WorkflowAction action,
            IPlatformProvider provider,
            ActionContext context,
            CancellationToken cancellationToken)
        {
            var retry = action.Retry ?? new ActionRetryPolicy { MaxAttempts = 1 };
            var attempt = 0;
            Exception? lastException = null;

            while (attempt < retry.MaxAttempts)
            {
                attempt++;

                try
                {
                    _logger.LogDebug(
                        "Executing action: {ActionId} (Attempt {Attempt}/{MaxAttempts})",
                        action.Id, attempt, retry.MaxAttempts);

                    var result = await provider.ExecuteActionAsync(action, context, cancellationToken);

                    if (result.Success)
                    {
                        _logger.LogDebug("Action succeeded: {ActionId}", action.Id);
                        return new ActionExecutionResult
                        {
                            ActionId = action.Id,
                            Success = true,
                            OutputData = result.OutputData,
                            Duration = result.Duration,
                            Attempt = attempt
                        };
                    }

                    lastException = result.Error;

                    if (attempt < retry.MaxAttempts)
                    {
                        var delay = CalculateRetryDelay(retry, attempt);
                        _logger.LogWarning(
                            "Action failed, retrying in {Delay}ms: {ActionId}, Error: {Error}",
                            delay, action.Id, result.Message);

                        await Task.Delay(delay, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    if (attempt < retry.MaxAttempts)
                    {
                        var delay = CalculateRetryDelay(retry, attempt);
                        _logger.LogWarning(ex,
                            "Action threw exception, retrying in {Delay}ms: {ActionId}",
                            delay, action.Id);

                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }

            // All attempts failed
            _logger.LogError(
                "Action failed after {MaxAttempts} attempts: {ActionId}",
                retry.MaxAttempts, action.Id);

            return new ActionExecutionResult
            {
                ActionId = action.Id,
                Success = false,
                ErrorMessage = lastException?.Message ?? "Action failed",
                Exception = lastException,
                Attempt = attempt
            };
        }

        private int CalculateRetryDelay(ActionRetryPolicy retry, int attempt)
        {
            return retry.BackoffStrategy switch
            {
                "exponential" => retry.DelayMs * (int)Math.Pow(2, attempt - 1),
                "linear" => retry.DelayMs * attempt,
                _ => retry.DelayMs // fixed
            };
        }

        private string GetCurrentPlatform()
        {
            if (OperatingSystem.IsWindows()) return "windows";
            if (OperatingSystem.IsMacOS()) return "mac";
            if (OperatingSystem.IsLinux()) return "linux";
            if (OperatingSystem.IsAndroid()) return "android";
            if (OperatingSystem.IsIOS()) return "ios";

            throw new PlatformNotSupportedException("Unknown platform");
        }
    }

    /// <summary>
    /// Result of workflow execution.
    /// </summary>
    public class WorkflowExecutionResult
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string WorkflowId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public bool Skipped { get; set; }
        public string? SkipReason { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }
        public List<ActionExecutionResult> ActionResults { get; set; } = new();
        public TimeSpan Duration { get; set; }
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Result of individual action execution.
    /// </summary>
    public class ActionExecutionResult
    {
        public string ActionId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }
        public Dictionary<string, object>? OutputData { get; set; }
        public TimeSpan Duration { get; set; }
        public int Attempt { get; set; }
    }
}
