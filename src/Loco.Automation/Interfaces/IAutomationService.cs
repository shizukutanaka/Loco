using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json.Nodes;
using Loco.Core.Interfaces;
using Loco.Core.Models;

namespace Loco.Automation.Interfaces;

/// <summary>
/// Main automation service interface - Rob Pike simplicity
/// </summary>
public interface IAutomationService : IDisposable
{
    /// <summary>
    /// Starts the automation service asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if the service started successfully, otherwise false.</returns>
    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the automation service asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if the service stopped successfully, otherwise false.</returns>
    Task<bool> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a flow with the automation service.
    /// </summary>
    /// <param name="flow">The flow to register.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if the flow was registered successfully, otherwise false.</returns>
    Task<bool> RegisterFlowAsync(IFlow flow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters a flow from the automation service.
    /// </summary>
    /// <param name="flowId">The ID of the flow to unregister.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if the flow was unregistered successfully, otherwise false.</returns>
    Task<bool> UnregisterFlowAsync(string flowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a collection of all active flows.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable collection of active flows.</returns>
    Task<IEnumerable<IFlow>> GetActiveFlowsAsync(CancellationToken cancellationToken = default);


    /// <summary>
    /// Validates an automation rule provided as a JSON string.
    /// </summary>
    /// <param name="json">The JSON string representing the rule.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the validation result.</returns>
    Task<RuleValidationResult> ValidateRuleJsonAsync(string json, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an automation rule provided as a JsonNode.
    /// </summary>
    /// <param name="node">The JsonNode representing the rule.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the validation result.</returns>
    Task<RuleValidationResult> ValidateRuleJsonAsync(JsonNode node, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new automation rule from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string representing the rule.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if the rule was added successfully, otherwise false.</returns>
    Task<bool> AddRuleFromJsonAsync(string json, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new automation rule from a JsonNode.
    /// </summary>
    /// <param name="node">The JsonNode representing the rule.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if the rule was added successfully, otherwise false.</returns>
    Task<bool> AddRuleFromJsonAsync(JsonNode node, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an automation rule by its ID.
    /// </summary>
    /// <param name="ruleId">The ID of the rule to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if the rule was deleted successfully, otherwise false.</returns>
    Task<bool> DeleteRuleAsync(string ruleId, CancellationToken cancellationToken = default);
}