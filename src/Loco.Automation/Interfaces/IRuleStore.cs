using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace Loco.Automation.Interfaces;

/// <summary>
/// Defines the contract for a store that persists and retrieves automation rules.
/// </summary>
public interface IRuleStore
{
    /// <summary>
    /// Loads all rule definitions from the persistence layer.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A collection of JSON strings, each representing a rule.</returns>
    Task<IEnumerable<string>> LoadAllRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a rule definition to the persistence layer.
    /// </summary>
    /// <param name="ruleId">The unique identifier for the rule.</param>
    /// <param name="ruleJson">The JSON string representing the rule.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveRuleAsync(string ruleId, string ruleJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a rule definition from the persistence layer.
    /// </summary>
    /// <param name="ruleId">The unique identifier of the rule to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteRuleAsync(string ruleId, CancellationToken cancellationToken = default);
}
