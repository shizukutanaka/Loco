using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;
using Loco.Core.Interfaces;
using Loco.Core.Configuration;
using Loco.Core.OCR;

namespace Loco.Core
{
    /// <summary>
    /// Lightweight automation engine with basic rule and flow execution support.
    /// Implements IAutomationEngine interface for consistency.
    /// </summary>
    public class SimpleLightEngine : IAutomationEngine, IDisposable
    {
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, SimpleFlow> _flows = new();
        private readonly ConcurrentDictionary<string, SimpleRule> _rules = new();
        private readonly EngineStatus _status = new();
        private readonly SemaphoreSlim _executionSemaphore;
        private readonly LocoConfig _config;
        private readonly SimpleScheduler _scheduler;
        private readonly IRuleStore? _ruleStore;
        private readonly IOcrService? _ocrService;
        private bool _isRunning;
        private bool _disposed;

        public SimpleLightEngine(ILogger? logger = null, LocoConfig? config = null, IRuleStore? ruleStore = null, IOcrService? ocrService = null)
        {
            _logger = logger;
            _config = config ?? new LocoConfig();
            _isRunning = false;
            _executionSemaphore = new SemaphoreSlim(_config.MaxConcurrentFlows, _config.MaxConcurrentFlows);
            _scheduler = new SimpleScheduler(logger);
            _ruleStore = ruleStore;
            _ocrService = ocrService;
        }

        /// <summary>
        /// Schedules a rule to execute at regular intervals.
        /// </summary>
        /// <param name="ruleId">Unique identifier of the rule to schedule</param>
        /// <param name="interval">Time interval between executions</param>
        /// <exception cref="ArgumentException">Thrown when ruleId is null or empty</exception>
        /// <exception cref="ObjectDisposedException">Thrown if engine has been disposed</exception>
        public void ScheduleRule(string ruleId, TimeSpan interval)
        {
            _scheduler.ScheduleInterval(ruleId, interval, async () => await ExecuteRuleAsync(ruleId));
        }

        /// <summary>
        /// Schedules a rule to execute once at a specified time.
        /// </summary>
        /// <param name="ruleId">Unique identifier of the rule to schedule</param>
        /// <param name="runAt">The specific date and time to execute the rule</param>
        /// <exception cref="ArgumentException">Thrown when ruleId is null or empty</exception>
        /// <exception cref="ObjectDisposedException">Thrown if engine has been disposed</exception>
        public void ScheduleRuleOnce(string ruleId, DateTime runAt)
        {
            _scheduler.ScheduleOnce(ruleId, runAt, async () => await ExecuteRuleAsync(ruleId));
        }

        /// <summary>
        /// Cancels a scheduled rule execution.
        /// </summary>
        /// <param name="ruleId">Unique identifier of the scheduled rule to cancel</param>
        /// <returns>True if the rule was successfully cancelled, false if not found</returns>
        public bool CancelScheduledRule(string ruleId)
        {
            return _scheduler.RemoveJob(ruleId);
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            _isRunning = true;
            _logger?.LogInformation("SimpleLightEngine started (Max concurrent flows: {MaxFlows})", _config.MaxConcurrentFlows);

            // ルールストアから永続化されたルールを読み込み
            if (_ruleStore != null)
            {
                try
                {
                    var persistedRules = await _ruleStore.GetRulesAsync();
                    foreach (var rule in persistedRules)
                    {
                        _rules[rule.Id] = rule;
                    }
                    _status.RuleCount = _rules.Count;
                    _logger?.LogInformation("Loaded {RuleCount} rules from persistent storage", persistedRules.Count);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to load rules from persistent storage");
                }
            }

            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isRunning) return;
            _isRunning = false;
            _logger?.LogInformation("SimpleLightEngine stopped");
            await Task.CompletedTask;
        }

        public Task<bool> ExecuteFlowAsync(string flowId, CancellationToken cancellationToken = default)
        {
            return ExecuteFlowAsync(flowId);
        }

        public void AddFlow(IFlow flow)
        {
            if (flow is SimpleFlow simpleFlow)
            {
                _flows[flow.Id] = simpleFlow;
                _status.FlowCount = _flows.Count;
            }
            else
            {
                throw new ArgumentException(
                    $"Invalid flow type: Expected SimpleFlow but got {flow.GetType().Name}. " +
                    "Ensure the flow is created using SimpleLightEngine.CreateFlow() or similar factory methods.",
                    nameof(flow));
            }
        }

        public async Task<bool> IsHealthyAsync()
        {
            await Task.CompletedTask;
            return _isRunning;
        }

        public EngineStatus GetEngineStatus()
        {
            _status.FlowCount = _flows.Count;
            _status.RuleCount = _rules.Count;
            return _status;
        }

        /// <summary>
        /// Creates a new automation rule with the specified trigger and actions.
        /// </summary>
        /// <param name="name">Human-readable name for the rule</param>
        /// <param name="trigger">The trigger that activates this rule</param>
        /// <param name="actions">Array of actions to execute when triggered</param>
        /// <returns>Unique identifier for the created rule</returns>
        /// <exception cref="ArgumentNullException">Thrown when actions array is null</exception>
        /// <example>
        /// <code>
        /// var ruleId = engine.CreateRule("Log on startup",
        ///     new LightTrigger { Type = "manual" },
        ///     new[] { new LightAction { Type = "log", Parameters = new() { ["message"] = "Started" } } });
        /// </code>
        /// </example>
        public string CreateRule(string name, LightTrigger trigger, LightAction[] actions)
        {
            var ruleId = Guid.NewGuid().ToString();
            var rule = new SimpleRule(ruleId, name, trigger, actions);
            _rules[ruleId] = rule;
            _status.RuleCount = _rules.Count;

            // ルールを永続化ストアに保存
            if (_ruleStore != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _ruleStore.UpsertRuleAsync(rule);
                        _logger?.LogInformation("Persisted rule to storage: {RuleName} (ID: {RuleId})", rule.Name, ruleId);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to persist rule: {RuleId}", ruleId);
                    }
                });
            }

            return ruleId;
        }

        /// <summary>
        /// Executes an automation rule by its unique identifier.
        /// </summary>
        /// <param name="ruleId">The unique identifier of the rule to execute</param>
        /// <returns>True if the rule was executed successfully, false otherwise</returns>
        /// <exception cref="ObjectDisposedException">Thrown if engine has been disposed</exception>
        public async Task<bool> ExecuteRuleAsync(string ruleId)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(ruleId))
            {
                _logger?.LogWarning("Rule ID is null or empty");
                _status.TotalExecutions++;
                return false;
            }

            if (!_rules.TryGetValue(ruleId, out var rule))
            {
                _logger?.LogWarning("Rule not found: {RuleId}", ruleId);
                _status.TotalExecutions++;
                return false;
            }

            if (!rule.IsEnabled)
            {
                _logger?.LogInformation("Skipping disabled rule: {RuleName} (ID: {RuleId})", rule.Name, ruleId);
                _status.TotalExecutions++;
                return true;
            }

            if (rule.Actions == null || rule.Actions.Length == 0)
            {
                _logger?.LogWarning("Rule has no actions: {RuleName} (ID: {RuleId})", rule.Name, ruleId);
                _status.TotalExecutions++;
                _status.SuccessfulExecutions++;
                return true;
            }

            var semaphoreAcquired = false;
            try
            {
                await _executionSemaphore.WaitAsync();
                semaphoreAcquired = true;

                _status.TotalExecutions++;
                _logger?.LogInformation("Executing rule: {RuleName} (ID: {RuleId}) with {ActionCount} actions",
                    rule.Name, ruleId, rule.Actions.Length);

                var startTime = DateTime.UtcNow;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.DefaultTimeoutSeconds));
                var actionCount = rule.Actions.Length;
                var actionResults = new bool[actionCount];
                var tasks = new Task[actionCount];

                for (int i = 0; i < actionCount; i++)
                {
                    if (cts.Token.IsCancellationRequested)
                    {
                        _logger?.LogWarning("Rule execution cancelled due to timeout: {RuleName}", rule.Name);
                        break;
                    }

                    var actionIndex = i;
                    tasks[i] = ExecuteActionWithResultAsync(rule.Actions[i], actionResults, actionIndex, cts.Token);
                }

                await Task.WhenAll(tasks.Where(t => t != null));

                var duration = DateTime.UtcNow - startTime;
                var successCount = actionResults.Count(r => r);
                var failCount = actionResults.Length - successCount;

                if (failCount == 0)
                {
                    _status.SuccessfulExecutions++;
                    _logger?.LogInformation("Rule executed successfully: {RuleName} (ID: {RuleId}) - {SuccessCount}/{TotalCount} actions succeeded in {Duration}ms",
                        rule.Name, ruleId, successCount, actionResults.Length, duration.TotalMilliseconds);
                }
                else
                {
                    _logger?.LogWarning("Rule completed with errors: {RuleName} (ID: {RuleId}) - {SuccessCount}/{TotalCount} actions succeeded, {FailCount} failed in {Duration}ms",
                        rule.Name, ruleId, successCount, actionResults.Length, failCount, duration.TotalMilliseconds);
                }

                // Let .NET runtime handle garbage collection automatically
                // Manual GC calls can hurt performance more than help

                return failCount == 0;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Rule execution timeout after {Timeout}s: {RuleName} (ID: {RuleId})",
                    _config.DefaultTimeoutSeconds, rule.Name, ruleId);
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to execute rule: {RuleId} ({RuleName}) - {ErrorType}: {ErrorMessage}",
                    ruleId, rule.Name, ex.GetType().Name, ex.Message);
                return false;
            }
            finally
            {
                if (semaphoreAcquired)
                    _executionSemaphore.Release();
            }
        }

        private async Task ExecuteActionWithResultAsync(LightAction action, bool[] results, int index, CancellationToken cancellationToken)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    results[index] = false;
                    return;
                }

                var success = await ExecuteActionWithReturnAsync(action, cancellationToken).ConfigureAwait(false);
                results[index] = success;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Action execution failed: {ActionType}", action?.Type ?? "Unknown");
                results[index] = false;
            }
        }

        private async Task<bool> ExecuteActionWithReturnAsync(LightAction action, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(action?.Type))
            {
                _logger?.LogWarning("Action type is null or empty");
                return false;
            }

            var retryCount = 0;
            var maxRetries = _config.DefaultRetryCount;

            while (retryCount <= maxRetries)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogInformation("Action execution cancelled: {ActionType}", action.Type);
                    return false;
                }

                try
                {
                    // Execute action directly if it implements IAction interface
                    if (action is IAction actionImpl)
                    {
                        var context = new ActionContext
                        {
                            Logger = _logger,
                            CancellationToken = cancellationToken,
                            Variables = new Dictionary<string, object?>()
                        };
                        var result = await actionImpl.ExecuteAsync(context);
                        return result;
                    }

                    // Handle built-in action types
                    switch (action.Type.ToLowerInvariant())
                    {
                        case "ocr":
                        case "extracttext":
                            return await ExecuteOcrActionAsync(action, cancellationToken);
                        case "log":
                            return await ExecuteLogActionAsync(action, cancellationToken);
                        default:
                            _logger?.LogWarning("Unknown action type: {ActionType}", action.Type);
                            return false;
                    }
                }
                catch (Exception ex) when (retryCount < maxRetries)
                {
                    retryCount++;
                    var delay = TimeSpan.FromMilliseconds(Math.Pow(2, retryCount) * 100);
                    _logger?.LogWarning(ex, "Action failed (retry {RetryCount}/{MaxRetries}): {ActionType}. Retrying in {Delay}ms",
                        retryCount, maxRetries, action.Type, delay.TotalMilliseconds);

                    try
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogInformation("Action retry cancelled: {ActionType}", action.Type);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Action failed after {RetryCount} retries: {ActionType} - {ErrorMessage}",
                        retryCount, action.Type, ex.Message);
                    return false;
                }
            }

            return false;
        }


        public async Task<bool> ExecuteFlowAsync(string flowId)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(flowId))
            {
                _logger?.LogWarning("Flow ID is null or empty");
                _status.TotalExecutions++;
                return false;
            }

            if (!_flows.TryGetValue(flowId, out var flow))
            {
                _logger?.LogWarning("Flow not found: {FlowId}", flowId);
                _status.TotalExecutions++;
                return false;
            }

            var semaphoreAcquired = false;
            try
            {
                await _executionSemaphore.WaitAsync();
                semaphoreAcquired = true;

                _status.TotalExecutions++;
                var startTime = DateTime.UtcNow;
                _logger?.LogInformation("Executing flow: {FlowId}", flowId);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.DefaultTimeoutSeconds));

                var context = new ActionContext
                {
                    Variables = new Dictionary<string, object?>(),
                    Logger = _logger,
                    CancellationToken = cts.Token,
                    FlowId = flowId
                };

                await flow.ExecuteAsync(context);

                var duration = DateTime.UtcNow - startTime;
                _status.SuccessfulExecutions++;
                _logger?.LogInformation("Flow executed successfully: {FlowId} - Completed in {Duration}ms",
                    flowId, duration.TotalMilliseconds);

                // Let .NET runtime handle garbage collection automatically

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Flow execution timeout after {Timeout}s: {FlowId}",
                    _config.DefaultTimeoutSeconds, flowId);
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to execute flow: {FlowId} - {ErrorType}: {ErrorMessage}",
                    flowId, ex.GetType().Name, ex.Message);
                if (ex.InnerException != null)
                {
                    _logger?.LogError("Inner exception: {InnerExceptionType}: {InnerExceptionMessage}",
                        ex.InnerException.GetType().Name, ex.InnerException.Message);
                }
                return false;
            }
            finally
            {
                if (semaphoreAcquired)
                    _executionSemaphore.Release();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SimpleLightEngine));
        }

        private async Task<bool> ExecuteOcrActionAsync(LightAction action, CancellationToken cancellationToken)
        {
            if (_ocrService == null)
            {
                _logger?.LogError("OCR service not available for action: {ActionType}", action.Type);
                return false;
            }

            try
            {
                var imagePath = action.Parameters?["imagePath"] as string;
                var outputVariable = action.Parameters?["outputVariable"] as string;

                if (string.IsNullOrEmpty(imagePath))
                {
                    _logger?.LogError("Image path not specified for OCR action");
                    return false;
                }

                _logger?.LogInformation("Extracting text from image: {ImagePath}", imagePath);

                var options = new OcrOptions();
                if (action.Parameters?.ContainsKey("language") == true)
                    options.Language = action.Parameters["language"] as string ?? "auto";
                if (action.Parameters?.ContainsKey("confidenceThreshold") == true)
                    options.ConfidenceThreshold = Convert.ToInt32(action.Parameters["confidenceThreshold"]);

                var result = await _ocrService.ExtractTextAsync(imagePath, options, cancellationToken);

                if (result.Success)
                {
                    _logger?.LogInformation("OCR extraction successful. Confidence: {Confidence}%, Text length: {TextLength}",
                        result.Confidence, result.ExtractedText?.Length ?? 0);

                    // Store result in context variables if outputVariable specified
                    if (!string.IsNullOrEmpty(outputVariable))
                    {
                        // Note: In a real implementation, you'd need access to the context
                        // For now, just log the result
                        _logger?.LogInformation("OCR result: {ExtractedText}", result.ExtractedText);
                    }

                    return true;
                }
                else
                {
                    _logger?.LogError("OCR extraction failed: {ErrorMessage}", result.ErrorMessage);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "OCR action execution failed");
                return false;
            }
        }

        private async Task<bool> ExecuteLogActionAsync(LightAction action, CancellationToken cancellationToken)
        {
            try
            {
                var message = action.Parameters?["message"] as string ?? "Log action executed";
                var level = action.Parameters?["level"] as string ?? "info";

                switch (level.ToLowerInvariant())
                {
                    case "error":
                        _logger?.LogError("Log action: {Message}", message);
                        break;
                    case "warning":
                        _logger?.LogWarning("Log action: {Message}", message);
                        break;
                    case "debug":
                        _logger?.LogDebug("Log action: {Message}", message);
                        break;
                    default:
                        _logger?.LogInformation("Log action: {Message}", message);
                        break;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Log action execution failed");
                return false;
            }
        }