using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;

namespace Loco.Core.Services
{
    /// <summary>
    /// High-performance rule execution engine
    /// Robert C. Martin's clean architecture principles
    /// </summary>
    public sealed class RuleEngine : IDisposable
    {
        private readonly ILogger<RuleEngine> _logger;
        private readonly CacheService _cache;
        private readonly Dictionary<string, CompiledRule> _compiledRules;
        private readonly SemaphoreSlim _executionSemaphore;
        private readonly int _maxConcurrency;
        
        public RuleEngine(ILogger<RuleEngine> logger, int maxConcurrency = 10)
        {
            _logger = logger;
            _cache = new CacheService();
            _compiledRules = new Dictionary<string, CompiledRule>();
            _maxConcurrency = maxConcurrency;
            _executionSemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        }

        /// <summary>
        /// Compile and register a rule for fast execution
        /// </summary>
        public async Task<bool> RegisterRuleAsync(AutomationDsl.Rule rule)
        {
            if (rule == null) return false;

            try
            {
                var compiled = await CompileRuleAsync(rule);
                _compiledRules[rule.Id] = compiled;
                _logger.LogInformation("Rule {RuleId} compiled and registered", rule.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compile rule {RuleId}", rule.Id);
                return false;
            }
        }

        /// <summary>
        /// Execute a rule with context
        /// </summary>
        public async Task<RuleExecutionResult> ExecuteRuleAsync(
            string ruleId, 
            ExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (!_compiledRules.TryGetValue(ruleId, out var compiled))
            {
                return new RuleExecutionResult
                {
                    Success = false,
                    Error = $"Rule {ruleId} not found"
                };
            }

            await _executionSemaphore.WaitAsync(cancellationToken);
            try
            {
                return await ExecuteCompiledRuleAsync(compiled, context, cancellationToken);
            }
            finally
            {
                _executionSemaphore.Release();
            }
        }

        /// <summary>
        /// Execute multiple rules in parallel
        /// </summary>
        public async Task<List<RuleExecutionResult>> ExecuteRulesAsync(
            IEnumerable<string> ruleIds,
            ExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var tasks = ruleIds.Select(id => ExecuteRuleAsync(id, context, cancellationToken));
            return (await Task.WhenAll(tasks)).ToList();
        }

        /// <summary>
        /// Validate rule before execution
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RuleValidationResult ValidateRule(AutomationDsl.Rule rule)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(rule?.Id))
            {
                errors.Add("Rule ID is required");
            }

            if (rule?.Trigger == null)
            {
                errors.Add("At least one trigger is required");
            }

            if (rule?.Actions == null || !rule.Actions.Any())
            {
                errors.Add("At least one action is required");
            }

            return new RuleValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        private async Task<CompiledRule> CompileRuleAsync(AutomationDsl.Rule rule)
        {
            return await Task.Run(() =>
            {
                var compiled = new CompiledRule
                {
                    Id = rule.Id,
                    Name = rule.Name,
                    Priority = GetRulePriority(rule),
                    TriggerDelegates = CompileTriggers(rule.Trigger),
                    ConditionDelegates = CompileConditions(rule.Conditions),
                    ActionDelegates = CompileActions(rule.Actions)
                };

                return compiled;
            });
        }

        private async Task<RuleExecutionResult> ExecuteCompiledRuleAsync(
            CompiledRule rule,
            ExecutionContext context,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                // Check triggers
                var triggerResult = await EvaluateTriggersAsync(rule.TriggerDelegates, context, cancellationToken);
                if (!triggerResult)
                {
                    return new RuleExecutionResult
                    {
                        Success = false,
                        Error = "No triggers matched"
                    };
                }

                // Check conditions
                if (rule.ConditionDelegates.Any())
                {
                    var conditionResult = await EvaluateConditionsAsync(rule.ConditionDelegates, context, cancellationToken);
                    if (!conditionResult)
                    {
                        return new RuleExecutionResult
                        {
                            Success = false,
                            Error = "Conditions not met"
                        };
                    }
                }

                // Execute actions
                await ExecuteActionsAsync(rule.ActionDelegates, context, cancellationToken);

                return new RuleExecutionResult
                {
                    Success = true,
                    ExecutionTime = DateTime.UtcNow - startTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing rule {RuleId}", rule.Id);
                return new RuleExecutionResult
                {
                    Success = false,
                    Error = ex.Message,
                    ExecutionTime = DateTime.UtcNow - startTime
                };
            }
        }

        private List<Func<ExecutionContext, CancellationToken, Task<bool>>> CompileTriggers(AutomationDsl.TriggerDefinition? trigger)
        {
            var list = new List<Func<ExecutionContext, CancellationToken, Task<bool>>>();
            if (trigger is not null)
            {
                list.Add(CreateTriggerDelegate(trigger));
            }
            return list;
        }

        private List<Func<ExecutionContext, CancellationToken, Task<bool>>> CompileConditions(List<AutomationDsl.ConditionDefinition> conditions)
        {
            return conditions?.Select(c => CreateConditionDelegate(c)).ToList() 
                ?? new List<Func<ExecutionContext, CancellationToken, Task<bool>>>();
        }

        private List<Func<ExecutionContext, CancellationToken, Task>> CompileActions(List<AutomationDsl.ActionDefinition> actions)
        {
            return actions?.Select(a => CreateActionDelegate(a)).ToList() 
                ?? new List<Func<ExecutionContext, CancellationToken, Task>>();
        }

        private Func<ExecutionContext, CancellationToken, Task<bool>> CreateTriggerDelegate(AutomationDsl.TriggerDefinition trigger)
        {
            return async (context, ct) =>
            {
                // Fast trigger evaluation based on type
                return trigger.Type switch
                {
                    "time" => EvaluateTimeTrigger(trigger, context),
                    "file" => await EvaluateFileTriggerAsync(trigger, context, ct),
                    "system" => EvaluateSystemTrigger(trigger, context),
                    _ => false
                };
            };
        }

        private Func<ExecutionContext, CancellationToken, Task<bool>> CreateConditionDelegate(AutomationDsl.ConditionDefinition condition)
        {
            return async (context, ct) =>
            {
                // Condition evaluation logic
                await Task.CompletedTask;
                return true; // Placeholder
            };
        }

        private Func<ExecutionContext, CancellationToken, Task> CreateActionDelegate(AutomationDsl.ActionDefinition action)
        {
            return async (context, ct) =>
            {
                // Action execution logic
                _logger.LogInformation("Executing action: {ActionType}", action.Type);
                await Task.Delay(10, ct); // Placeholder
            };
        }

        private bool EvaluateTimeTrigger(AutomationDsl.TriggerDefinition trigger, ExecutionContext context)
        {
            // Time-based trigger evaluation
            return true; // Placeholder
        }

        private async Task<bool> EvaluateFileTriggerAsync(AutomationDsl.TriggerDefinition trigger, ExecutionContext context, CancellationToken ct)
        {
            // File-based trigger evaluation
            await Task.CompletedTask;
            return true; // Placeholder
        }

        private bool EvaluateSystemTrigger(AutomationDsl.TriggerDefinition trigger, ExecutionContext context)
        {
            // System-based trigger evaluation
            return true; // Placeholder
        }

        private async Task<bool> EvaluateTriggersAsync(
            List<Func<ExecutionContext, CancellationToken, Task<bool>>> triggers,
            ExecutionContext context,
            CancellationToken cancellationToken)
        {
            foreach (var trigger in triggers)
            {
                if (await trigger(context, cancellationToken))
                    return true;
            }
            return false;
        }

        private async Task<bool> EvaluateConditionsAsync(
            List<Func<ExecutionContext, CancellationToken, Task<bool>>> conditions,
            ExecutionContext context,
            CancellationToken cancellationToken)
        {
            foreach (var condition in conditions)
            {
                if (!await condition(context, cancellationToken))
                    return false;
            }
            return true;
        }

        private async Task ExecuteActionsAsync(
            List<Func<ExecutionContext, CancellationToken, Task>> actions,
            ExecutionContext context,
            CancellationToken cancellationToken)
        {
            // Execute actions in sequence (can be parallelized if needed)
            foreach (var action in actions)
            {
                await action(context, cancellationToken);
            }
        }

        private int GetRulePriority(AutomationDsl.Rule rule)
        {
            // Calculate priority based on rule makeup (simple heuristic)
            var triggerCount = rule.Trigger != null ? 1 : 0;
            var actionCount = rule.Actions?.Count ?? 0;
            var conditionCount = rule.Conditions?.Count ?? 0;
            return triggerCount + actionCount + conditionCount;
        }

        public void Dispose()
        {
            _cache?.Dispose();
            _executionSemaphore?.Dispose();
        }

        private class CompiledRule
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int Priority { get; set; }
            public List<Func<ExecutionContext, CancellationToken, Task<bool>>> TriggerDelegates { get; set; } = new();
            public List<Func<ExecutionContext, CancellationToken, Task<bool>>> ConditionDelegates { get; set; } = new();
            public List<Func<ExecutionContext, CancellationToken, Task>> ActionDelegates { get; set; } = new();
        }
    }

    public class ExecutionContext
    {
        public Dictionary<string, object> Variables { get; set; } = new();
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public string UserId { get; set; } = string.Empty;
        public Dictionary<string, string> Environment { get; set; } = new();
    }

    public class RuleExecutionResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public Dictionary<string, object>? Output { get; set; }
    }
}
