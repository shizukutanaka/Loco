using System.Text.Json;

namespace Loco.Core.Workflows;

/// <summary>
/// Maps the persisted/editor shape (<see cref="StoredWorkflow"/>, which mirrors the
/// Visual Editor's Workflow TS interface) into the execution engine's
/// <see cref="VisualWorkflow"/> at execute/validate time.
///
/// Lives in Loco.Core (not Loco.Api, where it was originally written) so both the
/// API and the CLI (`loco workflow run-visual`) can share one mapping - the CLI
/// project does not reference Loco.Api.
///
/// CRUD never maps - it stores and returns the editor shape losslessly. Mapping
/// only happens on the execution boundary, so a field the engine doesn't know
/// about can never be lost by a save/load cycle.
///
/// Field mapping (per the editor's PropertyPanel conventions):
///   node.data.integration      -> WorkflowNode.Integration
///   node.data.config["action"] -> WorkflowNode.Action
///   node.data.config[*]        -> WorkflowNode.Parameters (minus "action")
///   node.type                  -> WorkflowNode.Type
///   edge.data.condition        -> WorkflowConnection.Condition
///   edge.sourceHandle/targetHandle -> SourceOutput/TargetInput
/// </summary>
public static class WorkflowMapper
{
    public static VisualWorkflow ToVisualWorkflow(StoredWorkflow stored)
    {
        var workflow = new VisualWorkflow
        {
            Id = stored.Id,
            Name = stored.Name,
            Description = stored.Description ?? "",
            Version = stored.Metadata.Version,
            Author = stored.Metadata.Author ?? "",
            Tags = stored.Metadata.Tags ?? new List<string>(),
        };

        foreach (var node in stored.Nodes)
        {
            var parameters = new Dictionary<string, object>();
            string action = "";

            foreach (var (key, value) in node.Data.Config)
            {
                if (key == "action")
                {
                    action = value.ValueKind == JsonValueKind.String
                        ? value.GetString() ?? ""
                        : value.ToString();
                    continue;
                }

                // The editor nests action parameters under config.parameters
                // (PropertyPanel writes them there, and validation reads them
                // there), which keeps them from colliding with "action". Copying
                // that object across verbatim would hand the connector a single
                // parameter literally named "parameters", so every real argument
                // - url, channel, to, ... - would arrive as null. Flatten it.
                if (key == "parameters" && value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var nested in value.EnumerateObject())
                    {
                        parameters[nested.Name] = ToPlainObject(nested.Value);
                    }
                    continue;
                }

                // Top-level config entries are still accepted, so workflows that
                // were authored flat keep working.
                parameters[key] = ToPlainObject(value);
            }

            workflow.Nodes.Add(new WorkflowNode
            {
                Id = node.Id,
                Name = node.Data.Label,
                Type = node.Type,
                Integration = node.Data.Integration ?? "",
                Action = action,
                CredentialId = node.Data.CredentialId,
                Parameters = parameters,
                Position = new NodePosition
                {
                    X = (int)node.Position.X,
                    Y = (int)node.Position.Y,
                },
            });
        }

        foreach (var edge in stored.Edges)
        {
            workflow.Connections.Add(new WorkflowConnection
            {
                Id = edge.Id,
                SourceNodeId = edge.Source,
                TargetNodeId = edge.Target,
                SourceOutput = edge.SourceHandle ?? "default",
                TargetInput = edge.TargetHandle ?? "default",
                Condition = edge.Data?.Condition,
            });
        }

        return workflow;
    }

    /// <summary>
    /// Converts a JsonElement into the plain CLR object shapes the engine's node
    /// handlers expect (string/bool/number/list/dictionary), instead of handing
    /// them raw JsonElements that Convert.ToInt32 etc. cannot digest.
    /// </summary>
    public static object ToPlainObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? "",
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.Array => element.EnumerateArray().Select(ToPlainObject).Cast<object>().ToList(),
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(p => p.Name, p => ToPlainObject(p.Value)),
        _ => "",
    };
}
