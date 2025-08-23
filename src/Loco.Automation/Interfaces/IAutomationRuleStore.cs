using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Automation.Interfaces;

/// <summary>
/// Persistence abstraction for automation rules.
/// Minimal interface to support add/update and bulk load.
/// </summary>
public interface IAutomationRuleStore
{
    /// <summary>
    /// Loads all persisted rules.
    /// </summary>
    Task<IReadOnlyList<AutomationDsl.Rule>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates a rule by Id.
    /// </summary>
    Task SaveOrUpdateAsync(AutomationDsl.Rule rule, CancellationToken cancellationToken = default);
}
