using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loco.Core;
using Loco.Core.Models;
using System.Threading;

namespace Loco.Core.Interfaces
{
    /// <summary>
    /// Explicit contract for the Automation Rule Engine.
    /// Keeps the public surface area minimal and clear for services and plugins.
    /// </summary>
    public interface IAutomationRuleEngine : IDisposable
    {
        /// <summary>
        /// Add a rule instance to the engine (validates, creates trigger, and starts it if enabled).
        /// </summary>
        Task<bool> AddRuleAsync(AutomationDsl.Rule rule, CancellationToken cancellationToken = default);

        /// <summary>
        /// Load and register a rule from DSL.
        /// </summary>
        Task<bool> LoadRuleAsync(AutomationDsl.Rule dslRule);

        /// <summary>
        /// Convert natural language to DSL and load the first resulting rule.
        /// </summary>
        Task<bool> LoadRuleFromNaturalLanguageAsync(string naturalLanguage, string modelId = null);

        /// <summary>
        /// Register a custom action type that can be referenced by rules.
        /// </summary>
        void RegisterActionType(string name, Type type);

        /// <summary>
        /// Enumerate currently loaded rules.
        /// </summary>
        IEnumerable<AutomationRule> GetRules();

        /// <summary>
        /// Enable or disable a rule by id.
        /// </summary>
        Task<bool> SetRuleEnabledAsync(string ruleId, bool enabled);

        /// <summary>
        /// Delete a rule by id and stop its trigger.
        /// </summary>
        Task<bool> DeleteRuleAsync(string ruleId);

        /// <summary>
        /// Manually trigger a rule by id with a context payload.
        /// Returns false if rule not found or disabled.
        /// </summary>
        Task<bool> TriggerRuleAsync(string ruleId, IDictionary<string, object> context, CancellationToken cancellationToken = default);
    }
}

