using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Loco.Core.Interfaces
{
    /// <summary>
    /// Simplified automation engine interface
    /// </summary>
    public interface ISimpleAutomationEngine : IDisposable
    {
        Task<bool> ExecuteRuleAsync(string ruleId, Dictionary<string, object> context = null);
        void RegisterRule(IFlow rule);
        void UnregisterRule(string ruleId);
        IEnumerable<string> GetRegisteredRules();
    }
}