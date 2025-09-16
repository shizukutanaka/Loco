using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Loco.Core.Components.Actions;
using Loco.Core.Models;
using Loco.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Loco.Core.Triggers;
using Loco.Core.Interfaces;
using Loco.Core.Factories;

namespace Loco.Core;

/// <summary>
/// Main automation rule engine - Combines triggers, conditions, and actions
/// John Carmack performance with Rob Pike simplicity
/// </summary>
public class AutomationRuleEngine : IAutomationRuleEngine, IDisposable
{
    private readonly ILogger<AutomationRuleEngine> _logger;
    private readonly Dictionary<string, AutomationRule> _rules = new();
    private readonly Dictionary<string, IRuntimeTrigger> _triggers = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _actionTypes = new();
    private readonly SandboxExecutor _sandboxExecutor;
    private readonly NaturalLanguageToDslConverter _nlConverter;
    private readonly ITriggerFactory _triggerFactory;
    private readonly SemaphoreSlim _executionLock = new(5); // Max 5 concurrent executions
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ConcurrentDictionary<string, (string RuleId, DateTime StartedAt)> _runningExecutions = new();

    public AutomationRuleEngine(
        ILogger<AutomationRuleEngine> logger,
        IServiceProvider serviceProvider,
        SandboxExecutor sandboxExecutor,
        NaturalLanguageToDslConverter nlConverter,
        ITriggerFactory triggerFactory)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _sandboxExecutor = sandboxExecutor;
        _nlConverter = nlConverter;
        _triggerFactory = triggerFactory;
        InitializeBuiltInActionTypes();
    }

    /// <summary>
    /// Load and register a rule from DSL
    /// </summary>
    public async Task<bool> LoadRuleAsync(AutomationDsl.Rule dslRule)
    {
        try
        {
            var totalSw = Stopwatch.StartNew();
            _logger.LogInformation("LoadRuleAsync(start): ruleId={RuleId} name={RuleName}", dslRule.Id, dslRule.Name);
            
            // Validate rule
            var validateSw = Stopwatch.StartNew();
            var validationResult = ValidateRule(dslRule);
            validateSw.Stop();
            _logger.LogInformation("LoadRuleAsync(validate): isValid={IsValid} durationMs={Ms}", validationResult.IsValid, validateSw.ElapsedMilliseconds);
            if (!validationResult.IsValid)
            {
                _logger.LogError("Rule validation failed: {Errors}", 
                    string.Join(", ", validationResult.Errors));
                return false;
            }
            
            // Create automation rule
            var rule = new AutomationRule
            {
                Id = dslRule.Id,
                Name = dslRule.Name,
                Description = dslRule.Description,
                Enabled = dslRule.Enabled,
                DslDefinition = dslRule,
                Permissions = dslRule.Permissions
            };
            
            // Create and register trigger
            var createTrigSw = Stopwatch.StartNew();
            var trigger = _triggerFactory.CreateTrigger(dslRule.Trigger);
            createTrigSw.Stop();
            _logger.LogDebug("LoadRuleAsync(createTrigger): durationMs={Ms} type={Type}", createTrigSw.ElapsedMilliseconds, dslRule.Trigger?.Type);
            if (trigger != null)
            {
                _logger.LogDebug("Creating trigger {TriggerType} with id {TriggerId} for rule {RuleName}", trigger.Type, trigger.Id, dslRule.Name);
                trigger.Triggered += async (sender, args) =>
                {
                    await OnTriggerFiredAsync(rule, args).ConfigureAwait(false);
                };
                
                _triggers[trigger.Id] = trigger;
                rule.TriggerId = trigger.Id;
                
                if (rule.Enabled)
                {
                    _logger.LogDebug("Starting trigger {TriggerType} ({TriggerId}) for rule {RuleName}", trigger.Type, trigger.Id, dslRule.Name);
                    var startAt = DateTime.UtcNow;
                    var startTask = trigger.StartAsync(_cancellationTokenSource.Token);
                    const int triggerStartTimeoutMs = 5000;
                    var completed = await Task.WhenAny(startTask, Task.Delay(triggerStartTimeoutMs, CancellationToken.None)).ConfigureAwait(false);
                    if (completed != startTask)
                    {
                        _logger.LogWarning("StartAsync timed out after {TimeoutMs} ms for trigger {TriggerType} ({TriggerId}) on rule {RuleName}. Continuing without awaiting completion.", triggerStartTimeoutMs, trigger.Type, trigger.Id, dslRule.Name);
                    }
                    else
                    {
                        await startTask.ConfigureAwait(false);
                        var durMs = (int)(DateTime.UtcNow - startAt).TotalMilliseconds;
                        _logger.LogDebug("Started trigger {TriggerType} ({TriggerId}) for rule {RuleName} in {DurationMs} ms", trigger.Type, trigger.Id, dslRule.Name, durMs);
                    }
                }
            }
            
            // Store rule
            _rules[rule.Id] = rule;
            
            totalSw.Stop();
            _logger.LogInformation("LoadRuleAsync(total): Rule loaded successfully: {RuleName} durationMs={Ms}", rule.Name, totalSw.ElapsedMilliseconds);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading rule");
            return false;
        }
    }

    /// <summary>
    /// Add a rule with cancellation support. Alias to LoadRuleAsync with guards.
    /// </summary>
    public async Task<bool> AddRuleAsync(AutomationDsl.Rule rule, CancellationToken cancellationToken = default)
    {
        var totalSw = Stopwatch.StartNew();
        cancellationToken.ThrowIfCancellationRequested();

        if (rule == null)
        {
            _logger.LogError("AddRuleAsync: rule is null");
            return false;
        }

        // Basic guards to prevent duplicates
        if (string.IsNullOrWhiteSpace(rule.Id))
        {
            rule.Id = Guid.NewGuid().ToString();
        }

        if (_rules.ContainsKey(rule.Id))
        {
            _logger.LogWarning("AddRuleAsync: rule with id {RuleId} already exists; replacing", rule.Id);
            await DeleteRuleAsync(rule.Id).ConfigureAwait(false);
        }

        var result = await LoadRuleAsync(rule).ConfigureAwait(false);
        totalSw.Stop();
        _logger.LogInformation("AddRuleAsync(total): result={Result} durationMs={Ms} ruleId={RuleId} name={Name}", result, totalSw.ElapsedMilliseconds, rule.Id, rule.Name);
        return result;
    }

    /// <summary>
    /// Load rule from natural language
    /// </summary>
    public async Task<bool> LoadRuleFromNaturalLanguageAsync(string naturalLanguage, string modelId = null)
    {
        try
        {
            _logger.LogInformation("Converting natural language to rule: {Input}", naturalLanguage);
            
            var conversionResult = await _nlConverter.ConvertAsync(naturalLanguage, modelId).ConfigureAwait(false);
            
            if (!conversionResult.Success || conversionResult.Rules?.Length == 0)
            {
                _logger.LogError("Failed to convert natural language to rule");
                return false;
            }
            
            // Load the first rule (could be extended to handle multiple)
            var rule = conversionResult.Rules[0];
            return await LoadRuleAsync(rule).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading rule from natural language");
            return false;
        }
    }

    /// <summary>
    /// Validate rule definition
    /// </summary>
    private RuleValidationResult ValidateRule(AutomationDsl.Rule rule)
    {
        var errors = new List<string>();
        
        if (string.IsNullOrEmpty(rule.Id))
            errors.Add("Rule ID is required");
        
        if (string.IsNullOrEmpty(rule.Name))
            errors.Add("Rule name is required");
        
        if (rule.Trigger == null)
            errors.Add("Trigger is required");
        
        if (rule.Actions == null || rule.Actions.Count == 0)
            errors.Add("At least one action is required");
        
        // Validate permissions match required features
        if (rule.Actions.Any(a => a.Type == "httpRequest") && !rule.Permissions.Network)
            errors.Add("Network permission required for HTTP requests");
        
        if (rule.Actions.Any(a => a.Type == "file") && !rule.Permissions.FileSystem)
            errors.Add("File system permission required for file operations");
        
        if (rule.Actions.Any(a => a.Type == "llmQuery") && !rule.Permissions.Llm)
            errors.Add("LLM permission required for AI queries");
        
        return new RuleValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }


    /// <summary>
    /// Handle trigger fired event
    /// </summary>
    private async Task OnTriggerFiredAsync(AutomationRule rule, TriggerEventArgs args)
    {
        if (!rule.Enabled)
            return;
        await ExecuteRuleAsync(rule, args.Context, _cancellationTokenSource.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Execute a rule with a provided trigger context and cancellation token.
    /// </summary>
    private async Task ExecuteRuleAsync(AutomationRule rule, IDictionary<string, object> triggerContext, CancellationToken cancellationToken)
    {
        var execId = Guid.NewGuid().ToString("N");
        var waitStart = DateTime.UtcNow;
        _logger.LogInformation("[Exec:{ExecId}] Waiting for slot. CurrentCount={Count}", execId, _executionLock.CurrentCount);

        await _executionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var waitedMs = (int)(DateTime.UtcNow - waitStart).TotalMilliseconds;
        _logger.LogInformation("[Exec:{ExecId}] Acquired slot after {WaitMs} ms. Running={Running} CurrentCount={Count}", execId, waitedMs, _runningExecutions.Count + 1, _executionLock.CurrentCount);
        _runningExecutions[execId] = (rule.Id, DateTime.UtcNow);
        var startedAt = DateTime.UtcNow;
        try
        {
            _logger.LogInformation("[Exec:{ExecId}] Start rule '{RuleName}' ({RuleId})", execId, rule.Name, rule.Id);

            // Create execution context
            var context = new ActionContext
            {
                Variables = new Dictionary<string, object>(rule.DslDefinition.Variables),
                TriggerContext = triggerContext ?? new Dictionary<string, object>(),
                Logger = _logger,
                ExecutionId = execId
            };

            // Add timestamp variable
            context.Variables["timestamp"] = DateTime.UtcNow.Ticks;

            // Check conditions
            if (!await CheckConditionsAsync(rule, context).ConfigureAwait(false))
            {
                _logger.LogInformation("[Exec:{ExecId}] Conditions not met for rule: {RuleName}", execId, rule.Name);
                return;
            }

            // Execute actions in sequence
            for (int i = 0; i < rule.DslDefinition.Actions.Count; i++)
            {
                var actionDef = rule.DslDefinition.Actions[i];
                _logger.LogInformation("[Exec:{ExecId}] Action {Index}/{Total} Type={Type}", execId, i + 1, rule.DslDefinition.Actions.Count, actionDef.Type);

                var actionStart = DateTime.UtcNow;
                var result = await ExecuteActionAsync(actionDef, context, rule.Permissions, cancellationToken).ConfigureAwait(false);
                var actionDuration = DateTime.UtcNow - actionStart;

                _logger.LogInformation("[Exec:{ExecId}] Action {Index} completed. Success={Success} DurationMs={Duration}", execId, i + 1, result.Success, (int)actionDuration.TotalMilliseconds);

                if (!result.Success && !actionDef.ContinueOnError)
                {
                    _logger.LogError("[Exec:{ExecId}] Action failed, stopping rule execution: {Error}", execId, result.Message);
                    break;
                }

                // Add output variables to context
                foreach (var kvp in result.OutputVariables)
                {
                    context.Variables[kvp.Key] = kvp.Value;
                }
            }

            rule.LastExecutedAt = DateTime.UtcNow;
            rule.ExecutionCount++;
            var total = DateTime.UtcNow - startedAt;
            _logger.LogInformation("[Exec:{ExecId}] Rule execution completed: {RuleName}. DurationMs={Duration}", execId, rule.Name, (int)total.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[Exec:{ExecId}] Rule execution canceled: {RuleName}", execId, rule.Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Exec:{ExecId}] Error executing rule: {RuleName}", execId, rule.Name);
        }
        finally
        {
            _executionLock.Release();
            _runningExecutions.TryRemove(execId, out _);
            _logger.LogInformation("[Exec:{ExecId}] Released slot. Running={Running} CurrentCount={Count}", execId, _runningExecutions.Count, _executionLock.CurrentCount);
        }
    }

    /// <summary>
    /// Check rule conditions
    /// </summary>
    private async Task<bool> CheckConditionsAsync(AutomationRule rule, ActionContext context)
    {
        if (rule.DslDefinition.Conditions == null || rule.DslDefinition.Conditions.Count == 0)
            return true;
        
        foreach (var condition in rule.DslDefinition.Conditions)
        {
            var met = await EvaluateConditionAsync(condition, context).ConfigureAwait(false);
            if (!met)
                return false;
        }
        
        return true;
    }

    /// <summary>
    /// Evaluate a single condition
    /// </summary>
    private Task<bool> EvaluateConditionAsync(AutomationDsl.ConditionDefinition condition, ActionContext context)
    {
        // Simple condition evaluation - can be extended
        var result = condition.Type?.ToLower() switch
        {
            "equals" => EvaluateEqualsCondition(condition, context),
            "contains" => EvaluateContainsCondition(condition, context),
            "greaterthan" => EvaluateGreaterThanCondition(condition, context),
            "lessthan" => EvaluateLessThanCondition(condition, context),
            _ => true
        };
        
        if (condition.Negate)
            result = !result;
        
        return Task.FromResult(result);
    }

    /// <summary>
    /// Evaluate equals condition
    /// </summary>
    private bool EvaluateEqualsCondition(AutomationDsl.ConditionDefinition condition, ActionContext context)
    {
        if (condition.Parameters.TryGetValue("field", out var field))
        {
            var fieldValue = GetFieldValue(field.ToString(), context);
            return fieldValue?.Equals(condition.Value) ?? false;
        }
        
        return false;
    }

    /// <summary>
    /// Evaluate contains condition
    /// </summary>
    private bool EvaluateContainsCondition(AutomationDsl.ConditionDefinition condition, ActionContext context)
    {
        if (condition.Parameters.TryGetValue("field", out var field))
        {
            var fieldValue = GetFieldValue(field.ToString(), context)?.ToString();
            var searchValue = condition.Value?.ToString();
            
            if (!string.IsNullOrEmpty(fieldValue) && !string.IsNullOrEmpty(searchValue))
            {
                return fieldValue.Contains(searchValue, StringComparison.OrdinalIgnoreCase);
            }
        }
        
        return false;
    }

    /// <summary>
    /// Evaluate greater than condition
    /// </summary>
    private bool EvaluateGreaterThanCondition(AutomationDsl.ConditionDefinition condition, ActionContext context)
    {
        if (condition.Parameters.TryGetValue("field", out var field))
        {
            var fieldValue = GetFieldValue(field.ToString(), context);
            
            if (fieldValue is IComparable comparable && condition.Value is IComparable value)
            {
                return comparable.CompareTo(value) > 0;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Evaluate less than condition
    /// </summary>
    private bool EvaluateLessThanCondition(AutomationDsl.ConditionDefinition condition, ActionContext context)
    {
        if (condition.Parameters.TryGetValue("field", out var field))
        {
            var fieldValue = GetFieldValue(field.ToString(), context);
            
            if (fieldValue is IComparable comparable && condition.Value is IComparable value)
            {
                return comparable.CompareTo(value) < 0;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Get field value from context
    /// </summary>
    private object GetFieldValue(string field, ActionContext context)
    {
        // Check variables
        if (context.Variables.TryGetValue(field, out var varValue))
            return varValue;
        
        // Check trigger context
        if (context.TriggerContext.TryGetValue(field, out var triggerValue))
            return triggerValue;
        
        return null;
    }

    /// <summary>
    /// Execute action with sandbox
    /// </summary>
    private async Task<ActionResult> ExecuteActionAsync(
        AutomationDsl.ActionDefinition actionDef,
        ActionContext context,
        AutomationDsl.PermissionSet permissions,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if action requires sandbox execution
            if (RequiresSandbox(actionDef.Type))
            {
                // Respect both external cancellation and per-action timeout via request.ResourceLimits
                return await ExecuteInSandboxAsync(actionDef, context, permissions, cancellationToken).ConfigureAwait(false);
            }
            
            // Execute using registered action
            if (_actionTypes.TryGetValue(actionDef.Type, out var actionType))
            {
                context.Parameters = actionDef.Parameters;

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (actionDef.TimeoutMs > 0)
                {
                    linkedCts.CancelAfter(actionDef.TimeoutMs.Value);
                }

                // Try fast activation via parameterless constructor first (avoids DI entirely for simple actions like 'log')
                IAction action = null;
                try
                {
                    var ctor = actionType.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                    {
                        _logger.LogDebug("[Exec:{ExecId}] Attempting fast activation (parameterless) for action type={ActionType}", context.ExecutionId, actionType.Name);
                        action = Activator.CreateInstance(actionType) as IAction;
                        if (action != null)
                        {
                            _logger.LogDebug("[Exec:{ExecId}] Fast activation succeeded for action type={ActionType}", context.ExecutionId, actionType.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[Exec:{ExecId}] Fast activation failed; will fallback to DI for action type={ActionType}", context.ExecutionId, actionType.Name);
                }

                // If fast activation did not work, fallback to DI scope + ActivatorUtilities
                if (action == null)
                {
                    _logger.LogDebug("[Exec:{ExecId}] Preparing to execute action type={ActionType} with DI scope", context.ExecutionId, actionType.Name);
                    using var scope = _serviceProvider.CreateScope();
                    _logger.LogDebug("[Exec:{ExecId}] Created DI scope for action type={ActionType}", context.ExecutionId, actionType.Name);
                    // Use ActivatorUtilities to create an instance of the action, even if it's not registered in the container.
                    _logger.LogDebug("[Exec:{ExecId}] Creating action instance via ActivatorUtilities for type={ActionType}", context.ExecutionId, actionType.Name);
                    action = ActivatorUtilities.CreateInstance(scope.ServiceProvider, actionType) as IAction;
                    _logger.LogDebug("[Exec:{ExecId}] Created action instance type={ActionType}: {InstanceNull}", context.ExecutionId, actionType.Name, action == null ? "null" : "ok");

                    if (action == null)
                    {
                        return new ActionResult
                        {
                            Success = false,
                            Message = $"Could not resolve action of type {actionType.Name} from DI container."
                        };
                    }

                    // Execute with DI-created instance
                    _logger.LogDebug("[Exec:{ExecId}] Invoking action.ExecuteAsync for type={ActionType}", context.ExecutionId, actionType.Name);
                    var actionExecStart = DateTime.UtcNow;
                    var execTask = action.ExecuteAsync(context, linkedCts.Token);
                    // Watchdog: log if exceeding timeout window
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var warnAfter = TimeSpan.FromMilliseconds((actionDef.TimeoutMs ?? 30000) + 2000);
                            await Task.Delay(warnAfter, CancellationToken.None).ConfigureAwait(false);
                            if (!execTask.IsCompleted)
                            {
                                _logger.LogWarning("[Exec:{ExecId}] Action type={ActionType} exceeded expected timeout window ({WarnMs} ms) without completing", context.ExecutionId, actionType.Name, (int)warnAfter.TotalMilliseconds);
                            }
                        }
                        catch { /* best-effort watchdog */ }
                    });
                    // Hard timeout guard
                    var timeoutMs = actionDef.TimeoutMs ?? 10000; // Reduce default from 30s to 10s for faster feedback
                    var completedTask = await Task.WhenAny(execTask, Task.Delay(timeoutMs + 200, CancellationToken.None)).ConfigureAwait(false);
                    if (completedTask != execTask)
                    {
                        try
                        {
                            // Ensure the underlying action observes cancellation
                            linkedCts.Cancel();
                        }
                        catch { /* best-effort cancel */ }
                        _logger.LogError("[Exec:{ExecId}] Action type={ActionType} timed out after {TimeoutMs} ms", context.ExecutionId, actionType.Name, timeoutMs);
                        return new ActionResult
                        {
                            Success = false,
                            Message = $"Action {actionType.Name} timed out after {timeoutMs} ms"
                        };
                    }
                    var actionResult = await execTask.ConfigureAwait(false);
                    var actionExecMs = (int)(DateTime.UtcNow - actionExecStart).TotalMilliseconds;
                    _logger.LogDebug("[Exec:{ExecId}] action.ExecuteAsync completed for type={ActionType} in {DurationMs} ms; Success={Success}", context.ExecutionId, actionType.Name, actionExecMs, actionResult.Success);
                    return actionResult;
                }

                // Execute with fast-activated instance
                _logger.LogDebug("[Exec:{ExecId}] Invoking action.ExecuteAsync (fast-activated) for type={ActionType}", context.ExecutionId, actionType.Name);
                var fastStart = DateTime.UtcNow;
                var fastTask = action.ExecuteAsync(context, linkedCts.Token);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var warnAfter = TimeSpan.FromMilliseconds((actionDef.TimeoutMs ?? 30000) + 2000);
                        await Task.Delay(warnAfter, CancellationToken.None).ConfigureAwait(false);
                        if (!fastTask.IsCompleted)
                        {
                            _logger.LogWarning("[Exec:{ExecId}] Action(type={ActionType}) (fast) exceeded expected timeout window ({WarnMs} ms) without completing", context.ExecutionId, actionType.Name, (int)warnAfter.TotalMilliseconds);
                        }
                    }
                    catch { /* best-effort watchdog */ }
                });
                // Hard timeout guard
                var fastTimeoutMs = actionDef.TimeoutMs ?? 10000;
                var fastCompleted = await Task.WhenAny(fastTask, Task.Delay(fastTimeoutMs + 500, CancellationToken.None)).ConfigureAwait(false);
                if (fastCompleted != fastTask)
                {
                    try
                    {
                        // Ensure the underlying action observes cancellation
                        linkedCts.Cancel();
                    }
                    catch { /* best-effort cancel */ }
                    _logger.LogError("[Exec:{ExecId}] Action(type={ActionType}) (fast) timed out after {TimeoutMs} ms", context.ExecutionId, actionType.Name, fastTimeoutMs);
                    return new ActionResult
                    {
                        Success = false,
                        Message = $"Action {actionType.Name} timed out after {fastTimeoutMs} ms"
                    };
                }
                var fastResult = await fastTask.ConfigureAwait(false);
                var fastMs = (int)(DateTime.UtcNow - fastStart).TotalMilliseconds;
                _logger.LogDebug("[Exec:{ExecId}] action.ExecuteAsync (fast) completed for type={ActionType} in {DurationMs} ms; Success={Success}", context.ExecutionId, actionType.Name, fastMs, fastResult.Success);
                return fastResult;
            }
            
            return new ActionResult
            {
                Success = false,
                Message = $"Unknown action type: {actionDef.Type}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing action: {Type}", actionDef.Type);
            return new ActionResult
            {
                Success = false,
                Message = $"Action execution failed: {ex.Message}",
                Exception = ex
            };
        }
    }

    /// <summary>
    /// Check if action requires sandbox
    /// </summary>
    private bool RequiresSandbox(string actionType)
    {
        return actionType?.ToLower() switch
        {
            "shell" => true,
            "script" => true,
            "process" => true,
            _ => false
        };
    }

    /// <summary>
    /// Execute in sandbox
    /// </summary>
    private async Task<ActionResult> ExecuteInSandboxAsync(
        AutomationDsl.ActionDefinition actionDef,
        ActionContext context,
        AutomationDsl.PermissionSet permissions,
        CancellationToken cancellationToken)
    {
        var request = new ExecutionRequest
        {
            Type = ExecutionType.Process,
            Command = actionDef.Parameters.GetValueOrDefault("command")?.ToString(),
            Arguments = actionDef.Parameters.GetValueOrDefault("arguments")?.ToString(),
            ExecutionId = context.ExecutionId,
            Permissions = new ExecutionPermissions
            {
                Network = permissions.Network,
                FileSystem = permissions.FileSystem,
                Shell = permissions.Shell,
                Llm = permissions.Llm,
                AllowedDomains = permissions.AllowedDomains,
                AllowedPaths = permissions.AllowedPaths
            },
            ResourceLimits = new ResourceLimits
            {
                TimeoutMs = actionDef.TimeoutMs ?? 30000
            }
        };
        
        var result = await _sandboxExecutor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        
        return new ActionResult
        {
            Success = result.Success,
            Message = result.Success ? "Sandbox execution completed" : result.Error,
            Data = result.Output,
            OutputVariables = new Dictionary<string, object>
            {
                ["output"] = result.Output ?? "",
                ["error"] = result.Error ?? "",
                ["exitCode"] = result.ExitCode ?? -1
            }
        };
    }

    /// <summary>
    /// Initialize built-in action types
    /// </summary>
    private void InitializeBuiltInActionTypes()
    {
        RegisterActionType("notification", typeof(NotificationAction));
        RegisterActionType("httpRequest", typeof(HttpRequestAction));
        RegisterActionType("file", typeof(FileAction));
        RegisterActionType("tts", typeof(TtsAction));
        RegisterActionType("launchApp", typeof(LaunchAppAction));
        RegisterActionType("llmQuery", typeof(LlmQueryAction));
        RegisterActionType("log", typeof(LogAction));
    }

    /// <summary>
    /// Registers an action type with the engine.
    /// </summary>
    public void RegisterActionType(string name, Type type)
    {
        if (!typeof(IAction).IsAssignableFrom(type))
        {
            throw new ArgumentException($"Type {type.Name} must implement IAction");
        }
        _actionTypes[name] = type;
        _logger.LogInformation("Registered action type: {Name} -> {Type}", name, type.Name);
    }

    /// <summary>
    /// Get all rules
    /// </summary>
    public IEnumerable<AutomationRule> GetRules()
    {
        return _rules.Values;
    }

    /// <summary>
    /// Enable/disable rule
    /// </summary>
    public async Task<bool> SetRuleEnabledAsync(string ruleId, bool enabled)
    {
        if (!_rules.TryGetValue(ruleId, out var rule))
            return false;
        
        rule.Enabled = enabled;
        
        if (_triggers.TryGetValue(rule.TriggerId, out var trigger))
        {
            if (enabled)
            {
                var startAt = DateTime.UtcNow;
                var startTask = trigger.StartAsync(_cancellationTokenSource.Token);
                const int triggerStartTimeoutMs = 5000;
                var completed = await Task.WhenAny(startTask, Task.Delay(triggerStartTimeoutMs, CancellationToken.None)).ConfigureAwait(false);
                if (completed != startTask)
                {
                    _logger.LogWarning("SetRuleEnabledAsync: StartAsync timed out after {TimeoutMs} ms for trigger {TriggerType} ({TriggerId}) on rule {RuleId}", triggerStartTimeoutMs, trigger.Type, trigger.Id, ruleId);
                }
                else
                {
                    await startTask.ConfigureAwait(false);
                    var durMs = (int)(DateTime.UtcNow - startAt).TotalMilliseconds;
                    _logger.LogDebug("SetRuleEnabledAsync: Started trigger {TriggerType} ({TriggerId}) for rule {RuleId} in {DurationMs} ms", trigger.Type, trigger.Id, ruleId, durMs);
                }
            }
            else
            {
                await trigger.StopAsync().ConfigureAwait(false);
            }
        }
        
        return true;
    }

    /// <summary>
    /// Delete rule
    /// </summary>
    public async Task<bool> DeleteRuleAsync(string ruleId)
    {
        if (!_rules.TryGetValue(ruleId, out var rule))
            return false;
        
        // Stop and remove trigger
        if (_triggers.TryGetValue(rule.TriggerId, out var trigger))
        {
            await trigger.StopAsync().ConfigureAwait(false);
            _triggers.Remove(rule.TriggerId);
        }
        
        // Remove rule
        _rules.Remove(ruleId);
        
        return true;
    }

    /// <summary>
    /// Manually trigger a rule by id with a context payload.
    /// </summary>
    public async Task<bool> TriggerRuleAsync(string ruleId, IDictionary<string, object> context, CancellationToken cancellationToken = default)
    {
        if (!_rules.TryGetValue(ruleId, out var rule))
        {
            _logger.LogWarning("TriggerRuleAsync: Rule not found: {RuleId}", ruleId);
            return false;
        }

        if (!rule.Enabled)
        {
            _logger.LogInformation("TriggerRuleAsync: Rule disabled: {RuleName}", rule.Name);
            return false;
        }

        _logger.LogDebug("TriggerRuleAsync: Invoking ExecuteRuleAsync for {RuleName} ({RuleId})", rule.Name, rule.Id);
        await ExecuteRuleAsync(rule, context ?? new Dictionary<string, object>(), cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("TriggerRuleAsync: ExecuteRuleAsync completed for {RuleName} ({RuleId})", rule.Name, rule.Id);
        return true;
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        
        // Stop all triggers (best-effort, non-blocking to avoid deadlocks)
        foreach (var trigger in _triggers.Values)
        {
            try
            {
                var stopTask = trigger.StopAsync();
                if (!stopTask.IsCompleted)
                {
                    _ = Task.Run(async () =>
                    {
                        try { await stopTask.ConfigureAwait(false); }
                        catch { /* swallow during dispose */ }
                    });
                }
            }
            catch { /* swallow during dispose */ }
        }
        
        if (_runningExecutions.Count > 0)
        {
            try
            {
                foreach (var kv in _runningExecutions)
                {
                    _logger.LogWarning("[Exec:{ExecId}] Still running at dispose. RuleId={RuleId} StartedAtUtc={StartedAt}", kv.Key, kv.Value.RuleId, kv.Value.StartedAt);
                }
            }
            catch { /* best-effort logging */ }
        }

        _sandboxExecutor?.Dispose();
        _executionLock?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}

/// <summary>
/// Automation rule
/// </summary>
public class AutomationRule
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool Enabled { get; set; }
    public string TriggerId { get; set; }
    public AutomationDsl.Rule DslDefinition { get; set; }
    public AutomationDsl.PermissionSet Permissions { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastExecutedAt { get; set; }
    public int ExecutionCount { get; set; }
}