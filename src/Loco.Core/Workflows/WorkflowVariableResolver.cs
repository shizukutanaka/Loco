using System.Text.Json;

namespace Loco.Core.Workflows;

/// <summary>
/// Resolves <c>{{...}}</c> references in node parameters against an execution's
/// variables and node results.
///
/// This lived inside WorkflowConnectorBridge and ran only for connector-backed
/// action nodes. The engine's own built-in handlers - condition, loop,
/// transform, delay, variable - read <see cref="WorkflowNode.Parameters"/>
/// directly, so a reference written into one of those was compared as the
/// literal text "{{amount}}".
///
/// That mattered most for the condition node, whose entire purpose is to
/// compare something produced upstream. Without resolution it could only
/// compare two constants, which is a comparison whose answer is already known -
/// and the PropertyPanel told the user "Supports {{variable}} references"
/// while it did not.
///
/// It lives here, and <see cref="VisualWorkflowEngine"/> applies it once at the
/// single point where every node handler is dispatched, so connector actions
/// and built-ins resolve identically and no value is resolved twice.
///
/// Supported forms: <c>{{variableName}}</c>, <c>{{nodeId.data.property}}</c>,
/// <c>{{previous.property}}</c>, and any of those inline within a longer
/// string.
/// </summary>
public static class WorkflowVariableResolver
{
    /// <summary>
    /// A copy of <paramref name="node"/> whose parameters have been resolved.
    ///
    /// A copy, not a mutation: the same node object is re-executed on retry and
    /// shared across an execution, so resolving in place would substitute a
    /// first attempt's values into every later one.
    /// </summary>
    public static WorkflowNode WithResolvedParameters(
        WorkflowNode node,
        WorkflowExecutionContext context)
    {
        if (node.Parameters.Count == 0)
            return node;

        var resolved = new Dictionary<string, object>(node.Parameters.Count);
        foreach (var parameter in node.Parameters)
        {
            var value = Resolve(parameter.Value, context);
            // Parameters is Dictionary<string, object>, so a reference that
            // resolves to nothing is dropped rather than stored as null. A
            // missing key and a key holding null mean the same thing to every
            // handler here - both come back from GetValueOrDefault as null.
            if (value is not null)
                resolved[parameter.Key] = value;
        }

        // Every property of WorkflowNode, not just the ones a handler happens
        // to read today: silently dropping RetryConfig or Disabled here would
        // turn a resolution step into a behaviour change. A reflection test
        // fails if a property is added to WorkflowNode and not copied.
        return new WorkflowNode
        {
            Id = node.Id,
            Name = node.Name,
            Type = node.Type,
            Integration = node.Integration,
            Action = node.Action,
            CredentialId = node.CredentialId,
            Parameters = resolved,
            Position = node.Position,
            Disabled = node.Disabled,
            Notes = node.Notes,
            RetryConfig = node.RetryConfig,
        };
    }

    /// <summary>Resolves one value; non-strings pass through unchanged.</summary>
    public static object? Resolve(object? value, WorkflowExecutionContext context)
    {
        if (value is not string strValue)
            return value;

        // A value that is nothing but one reference keeps that reference's own
        // type - a number stays a number, so `greater_than` can compare it
        // rather than comparing its rendering.
        if (strValue.StartsWith("{{") && strValue.EndsWith("}}"))
        {
            var path = strValue[2..^2].Trim();
            return ResolveVariablePath(path, context);
        }

        var result = strValue;
        var startIdx = 0;

        while (true)
        {
            var openIdx = result.IndexOf("{{", startIdx, StringComparison.Ordinal);
            if (openIdx < 0) break;

            var closeIdx = result.IndexOf("}}", openIdx, StringComparison.Ordinal);
            if (closeIdx < 0) break;

            var path = result.Substring(openIdx + 2, closeIdx - openIdx - 2).Trim();
            var resolved = ResolveVariablePath(path, context)?.ToString() ?? "";

            result = result[..openIdx] + resolved + result[(closeIdx + 2)..];
            startIdx = openIdx + resolved.Length;
        }

        return result;
    }

    private static object? ResolveVariablePath(string path, WorkflowExecutionContext context)
    {
        var parts = path.Split('.');

        if (parts.Length == 0)
            return null;

        // Workflow variables first
        if (context.Variables.TryGetValue(parts[0], out var variable))
        {
            if (parts.Length == 1)
                return variable;

            return NavigateObject(variable, parts[1..]);
        }

        // Node results
        if (context.NodeResults.TryGetValue(parts[0], out var nodeResult))
        {
            if (parts.Length == 1)
                return nodeResult.Data;

            if (parts[1] == "data" && parts.Length > 2)
            {
                return NavigateObject(nodeResult.Data, parts[2..]);
            }

            return NavigateObject(nodeResult.Data, parts[1..]);
        }

        // "previous" - the last node that ran
        if (parts[0] == "previous")
        {
            var lastResult = context.NodeResults.Values.LastOrDefault();
            if (lastResult == null) return null;

            if (parts.Length == 1)
                return lastResult.Data;

            return NavigateObject(lastResult.Data, parts[1..]);
        }

        return null;
    }

    private static object? NavigateObject(object? obj, string[] path)
    {
        if (obj == null || path.Length == 0)
            return obj;

        var current = obj;

        foreach (var part in path)
        {
            if (current == null)
                return null;

            if (current is JsonElement json)
            {
                if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty(part, out var prop))
                {
                    current = prop.ValueKind switch
                    {
                        JsonValueKind.String => prop.GetString(),
                        JsonValueKind.Number => prop.TryGetInt64(out var l) ? l : prop.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => (object)prop
                    };
                    continue;
                }
                return null;
            }

            if (current is IDictionary<string, object?> dict)
            {
                if (dict.TryGetValue(part, out var value))
                {
                    current = value;
                    continue;
                }
                return null;
            }

            var property = current.GetType().GetProperty(part);
            if (property != null)
            {
                current = property.GetValue(current);
                continue;
            }

            return null;
        }

        return current;
    }
}
