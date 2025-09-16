using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Interfaces;
using Loco.Core.Models;

namespace Loco.Core
{
    /// <summary>
    /// Simple automation rule engine
    /// </summary>
    public class AutomationRuleEngine : ISimpleAutomationEngine
    {
        private readonly ILogger<AutomationRuleEngine> _logger;
        private readonly SimpleFlowEngine _flowEngine;
        private readonly Dictionary<string, IFlow> _rules = new();

        public AutomationRuleEngine(ILogger<AutomationRuleEngine> logger = null)
        {
            _logger = logger;
            _flowEngine = new SimpleFlowEngine(logger as ILogger<SimpleFlowEngine>);
        }

        public async Task<bool> ExecuteRuleAsync(string ruleId, Dictionary<string, object> context = null)
        {
            try
            {
                return await _flowEngine.ExecuteFlowAsync(ruleId, context);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to execute rule {RuleId}", ruleId);
                return false;
            }
        }

        public void RegisterRule(IFlow rule)
        {
            if (rule == null) return;
            _rules[rule.Id] = rule;
            _flowEngine.RegisterFlow(rule);
            _logger?.LogInformation("Rule {RuleId} registered", rule.Id);
        }

        public void UnregisterRule(string ruleId)
        {
            if (_rules.Remove(ruleId))
            {
                _flowEngine.UnregisterFlow(ruleId);
                _logger?.LogInformation("Rule {RuleId} unregistered", ruleId);
            }
        }

        public IEnumerable<string> GetRegisteredRules()
        {
            return _rules.Keys;
        }

        public void Dispose()
        {
            _flowEngine?.Dispose();
            _rules.Clear();
        }
    }
}