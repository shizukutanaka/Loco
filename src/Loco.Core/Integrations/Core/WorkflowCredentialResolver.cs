using Loco.Core.Storage;
using Loco.Core.Workflows;

namespace Loco.Core.Integrations.Core;

/// <summary>
/// Initializes every connector a workflow references with its stored
/// credentials.
///
/// This is the step whose absence made connectors useless: ConfigureConnector
/// had no caller, so every connector executed uninitialized and failed on a
/// null HttpClient. The API grew this logic first; it lives here because the
/// CLI needs exactly the same thing, and a second copy would drift - the CLI
/// running workflows against uninitialized connectors is precisely the bug
/// this fixes.
/// </summary>
public static class WorkflowCredentialResolver
{
    /// <summary>
    /// The connections a workflow needs, one entry per (connector, connection).
    /// </summary>
    /// <remarks>
    /// Separated from applying it because this is the decision that can be
    /// wrong invisibly. Two connections for one connector must produce two
    /// entries: they used to be refused outright, since ConnectorRegistry
    /// caches a single instance per connector id and InitializeAsync replaces
    /// its configuration - so both nodes ran against whichever credential was
    /// applied last, posting to the wrong account with nothing reporting it.
    /// WorkflowConnectorBridge now keys instances by connection, and the node
    /// handler picks one from the node's own CredentialId at execution time.
    ///
    /// A node's Name is carried along so an unresolvable reference can say
    /// which node asked for it.
    /// </remarks>
    public static IReadOnlyList<ConnectionRequirement> PlanConnections(VisualWorkflow visual) =>
        visual.Nodes
            .Where(n => !string.IsNullOrEmpty(n.CredentialId) && !string.IsNullOrEmpty(n.Integration))
            .GroupBy(n => (n.Integration, CredentialId: n.CredentialId!))
            .Select(g => new ConnectionRequirement(
                g.Key.Integration, g.Key.CredentialId, g.First().Name))
            .ToList();

    /// <summary>
    /// Resolves and applies every connection the workflow needs.
    /// </summary>
    /// <returns>
    /// A description of each unresolvable reference, empty when everything
    /// resolved. Returned rather than thrown because a caller wants to report
    /// all of them at once, not the first.
    /// </returns>
    public static async Task<List<string>> ConfigureAsync(
        VisualWorkflow visual,
        JsonFileConnectionStore connections,
        WorkflowConnectorBridge bridge,
        CancellationToken cancellationToken = default)
    {
        var problems = new List<string>();

        foreach (var requirement in PlanConnections(visual))
        {
            var config = await connections.BuildConfigurationAsync(
                requirement.CredentialId, cancellationToken);

            if (config is null)
            {
                problems.Add(
                    $"node '{requirement.NodeName}' references connection " +
                    $"'{requirement.CredentialId}', which does not exist");
                continue;
            }

            await bridge.ConfigureConnectionAsync(
                requirement.Integration, requirement.CredentialId, config, cancellationToken);
        }

        return problems;
    }
}

/// <summary>One connection a workflow needs, and a node that asked for it.</summary>
public readonly record struct ConnectionRequirement(
    string Integration, string CredentialId, string NodeName);
