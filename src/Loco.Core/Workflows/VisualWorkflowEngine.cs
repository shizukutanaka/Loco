// Loco Visual Workflow Engine
// JSON-based workflow definitions for visual builder compatibility
// Inspired by n8n, Zapier, and Node-RED - addressing competitive gap #3

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loco.Core.Workflows;

/// <summary>
/// Visual workflow definition - JSON-serializable for visual editors
/// </summary>
public class VisualWorkflow
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public List<WorkflowNode> Nodes { get; set; } = new();
    public List<WorkflowConnection> Connections { get; set; } = new();
    public Dictionary<string, object> Settings { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string Author { get; set; } = "";
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Workflow node - represents a single step/action in the workflow
/// </summary>
public class WorkflowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // trigger, action, condition, loop, transform
    public string Integration { get; set; } = ""; // http, database, email, slack, etc.
    public string Action { get; set; } = ""; // get, post, query, send, etc.
    public Dictionary<string, object> Parameters { get; set; } = new();
    public NodePosition Position { get; set; } = new();
    public bool Disabled { get; set; } = false;
    public string Notes { get; set; } = "";
    public RetryConfig? RetryConfig { get; set; }

    /// <summary>
    /// Id of the connection supplying this node's credentials. The engine never
    /// reads it - the caller resolves it and initializes the connector before
    /// execution - but it must survive mapping so the caller can find it.
    /// </summary>
    public string? CredentialId { get; set; }
}

public class NodePosition
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class RetryConfig
{
    public int MaxAttempts { get; set; } = 3;
    public int DelaySeconds { get; set; } = 1;
    public bool ExponentialBackoff { get; set; } = true;
}

/// <summary>
/// Connection between nodes - defines workflow execution flow
/// </summary>
public class WorkflowConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceNodeId { get; set; } = "";
    public string TargetNodeId { get; set; } = "";
    public string SourceOutput { get; set; } = "default"; // output port name
    public string TargetInput { get; set; } = "default"; // input port name
    public string? Condition { get; set; } // optional: "success", "error", "custom:expression"
}

/// <summary>
/// Workflow execution context - tracks state during execution
/// </summary>
public class WorkflowExecutionContext
{
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = "";
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public WorkflowExecutionStatus Status { get; set; } = WorkflowExecutionStatus.Running;
    public Dictionary<string, NodeExecutionResult> NodeResults { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public string? Error { get; set; }
    public List<string> ExecutionLog { get; set; } = new();

    /// <summary>
    /// The execution's cancellation token, set by the engine at start. Node handlers
    /// (whose delegate signature has no CancellationToken parameter) can observe it
    /// so long-running work like the built-in delay node stops promptly on cancel.
    /// Serialization-invisible (JsonIgnore) - it is runtime state, not data.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public CancellationToken CancellationToken { get; set; }
}

public enum WorkflowExecutionStatus
{
    Pending,
    Running,
    Success,
    Failed,
    Cancelled,
    Paused
}

/// <summary>
/// Result of executing a single node
/// </summary>
public class NodeExecutionResult
{
    public string NodeId { get; set; } = "";
    public string NodeName { get; set; } = "";
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public int RetryCount { get; set; }
}

/// <summary>
/// Visual workflow execution engine
/// </summary>
public class VisualWorkflowEngine
{
    private readonly Dictionary<string, Func<WorkflowNode, WorkflowExecutionContext, Task<object?>>> _nodeHandlers = new();
    private readonly Action<string>? _logger;

    public VisualWorkflowEngine(Action<string>? logger = null)
    {
        _logger = logger;
        RegisterDefaultHandlers();
    }

    /// <summary>
    /// Register a custom node handler for specific integration+action
    /// </summary>
    public void RegisterNodeHandler(string integrationAction, Func<WorkflowNode, WorkflowExecutionContext, Task<object?>> handler)
    {
        _nodeHandlers[integrationAction] = handler;
    }

    /// <summary>
    /// Execute a visual workflow
    /// </summary>
    public async Task<WorkflowExecutionContext> ExecuteAsync(VisualWorkflow workflow, Dictionary<string, object>? initialVariables = null, CancellationToken ct = default)
    {
        var context = new WorkflowExecutionContext
        {
            WorkflowId = workflow.Id,
            Variables = initialVariables ?? new(),
            CancellationToken = ct
        };

        Log(context, $"Starting workflow: {workflow.Name} ({workflow.Id})");

        try
        {
            // Find trigger nodes (nodes with no incoming connections)
            var triggerNodes = workflow.Nodes
                .Where(n => !workflow.Connections.Any(c => c.TargetNodeId == n.Id))
                .Where(n => !n.Disabled)
                .ToList();

            if (!triggerNodes.Any())
            {
                throw new InvalidOperationException("No trigger nodes found. Workflow must have at least one starting node.");
            }

            // Execute from each trigger node
            foreach (var triggerNode in triggerNodes)
            {
                await ExecuteNodeAsync(workflow, triggerNode, context, ct);
            }

            context.Status = WorkflowExecutionStatus.Success;
            Log(context, $"Workflow completed successfully");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // A caller-requested cancel (e.g. POST /executions/{id}/cancel) is not a
            // failure - report it as Cancelled so clients can distinguish the two.
            context.Status = WorkflowExecutionStatus.Cancelled;
            context.Error = "Execution was cancelled";
            Log(context, "Workflow cancelled");
        }
        catch (Exception ex)
        {
            context.Status = WorkflowExecutionStatus.Failed;
            context.Error = ex.Message;
            Log(context, $"Workflow failed: {ex.Message}");
        }
        finally
        {
            context.EndTime = DateTime.UtcNow;
        }

        return context;
    }

    /// <summary>
    /// Execute a single node and its downstream connections
    /// </summary>
    private async Task ExecuteNodeAsync(VisualWorkflow workflow, WorkflowNode node, WorkflowExecutionContext context, CancellationToken ct)
    {
        // Honor cancellation at node boundaries; without this a long chain keeps
        // running to completion even after the caller cancelled.
        ct.ThrowIfCancellationRequested();

        if (node.Disabled)
        {
            Log(context, $"Skipping disabled node: {node.Name}");
            return;
        }

        var startTime = DateTime.UtcNow;
        Log(context, $"Executing node: {node.Name} ({node.Type})");

        var result = new NodeExecutionResult
        {
            NodeId = node.Id,
            NodeName = node.Name,
            StartTime = startTime
        };

        try
        {
            // Execute node with retry logic
            var data = await ExecuteWithRetryAsync(node, context, ct);

            result.Success = true;
            result.Data = data;

            // Store result in context for downstream nodes
            context.NodeResults[node.Id] = result;

            Log(context, $"Node succeeded: {node.Name}");

            // Execute downstream nodes
            var downstreamConnections = workflow.Connections
                .Where(c => c.SourceNodeId == node.Id)
                .Where(c => ShouldFollowConnection(c, result))
                .ToList();

            // Execute connected nodes (sequential for now, can be parallelized later)
            foreach (var connection in downstreamConnections)
            {
                var nextNode = workflow.Nodes.FirstOrDefault(n => n.Id == connection.TargetNodeId);
                if (nextNode != null)
                {
                    await ExecuteNodeAsync(workflow, nextNode, context, ct);
                }
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            context.NodeResults[node.Id] = result;

            Log(context, $"Node failed: {node.Name} - {ex.Message}");

            // Follow error connections if they exist
            var errorConnections = workflow.Connections
                .Where(c => c.SourceNodeId == node.Id && c.Condition == "error")
                .ToList();

            foreach (var connection in errorConnections)
            {
                var errorNode = workflow.Nodes.FirstOrDefault(n => n.Id == connection.TargetNodeId);
                if (errorNode != null)
                {
                    await ExecuteNodeAsync(workflow, errorNode, context, ct);
                }
            }

            // Re-throw if no error handler
            if (!errorConnections.Any())
                throw;
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
        }
    }

    private async Task<object?> ExecuteWithRetryAsync(WorkflowNode node, WorkflowExecutionContext context, CancellationToken ct)
    {
        var retryConfig = node.RetryConfig ?? new RetryConfig { MaxAttempts = 1 };
        Exception? lastException = null;

        for (int attempt = 0; attempt < retryConfig.MaxAttempts; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    var delay = retryConfig.ExponentialBackoff
                        ? TimeSpan.FromSeconds(retryConfig.DelaySeconds * Math.Pow(2, attempt - 1))
                        : TimeSpan.FromSeconds(retryConfig.DelaySeconds);

                    Log(context, $"Retrying node {node.Name} (attempt {attempt + 1}/{retryConfig.MaxAttempts}) after {delay.TotalSeconds}s");
                    await Task.Delay(delay, ct);
                }

                return await ExecuteNodeHandlerAsync(node, context, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // A caller-requested cancel is not a transient fault - never retry it.
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt == retryConfig.MaxAttempts - 1)
                    throw;
            }
        }

        throw lastException ?? new Exception("Node execution failed");
    }

    private async Task<object?> ExecuteNodeHandlerAsync(WorkflowNode node, WorkflowExecutionContext context, CancellationToken ct)
    {
        var handlerKey = $"{node.Integration}:{node.Action}";

        // Resolve {{...}} references once, here, because this is the only place
        // every handler is dispatched from. It used to happen inside
        // WorkflowConnectorBridge, which meant it happened for connector
        // actions and not for the engine's own built-ins - so a condition node
        // comparing {{amount}} compared that literal text, and could therefore
        // only ever compare two constants.
        var resolved = WorkflowVariableResolver.WithResolvedParameters(node, context);

        if (_nodeHandlers.TryGetValue(handlerKey, out var handler))
        {
            return await handler(resolved, context);
        }

        // Fallback: try generic handlers
        if (_nodeHandlers.TryGetValue(node.Type, out var typeHandler))
        {
            return await typeHandler(resolved, context);
        }

        throw new NotImplementedException($"No handler registered for {handlerKey}");
    }

    /// <summary>
    /// Whether an outgoing edge should be followed after its source node ran.
    ///
    /// Two independent things decide this, and they are not the same:
    ///
    ///   SourceOutput - which HANDLE the edge leaves from. The editor's
    ///   condition node draws two, "true" and "false" (ConditionNode.tsx), and
    ///   it is the only node type that names its handles at all; every other
    ///   node has one unnamed output, which maps to "default".
    ///
    ///   Condition - the edge's own success/error routing, set in the edge
    ///   panel and used to send failures down a recovery path.
    ///
    /// The branch handles used to be ignored completely. WorkflowMapper carried
    /// SourceOutput faithfully from the editor, the engine declared the property
    /// and never read it, and the condition node's verdict went into NodeResults
    /// where nothing consulted it either. So a condition node did not branch: it
    /// succeeded, both of its edges saw Success, and BOTH sides ran. Every
    /// if/else workflow this product has ever executed took both paths, and
    /// nothing anywhere reported it.
    /// </summary>
    private bool ShouldFollowConnection(WorkflowConnection connection, NodeExecutionResult result)
    {
        // A named branch handle answers first: a false branch must not run just
        // because the node that evaluated the condition did not throw.
        if (connection.SourceOutput is "true" or "false")
        {
            var verdict = ReadConditionVerdict(result.Data);
            if (verdict is null)
            {
                // The edge claims a branch its source did not produce a verdict
                // for. Following it - or its sibling - would be a guess, and
                // guessing here silently sends work down the wrong path.
                throw new InvalidOperationException(
                    $"Node '{result.NodeName}' has a '{connection.SourceOutput}' branch edge " +
                    "but produced no condition verdict. Only a condition node has true/false " +
                    "outputs; connect this edge to the node's default output instead.");
            }

            if (verdict.Value != (connection.SourceOutput == "true"))
            {
                return false;
            }
        }

        if (connection.Condition == null || connection.Condition == "default")
            return result.Success;

        if (connection.Condition == "success")
            return result.Success;

        if (connection.Condition == "error")
            return !result.Success;

        // "Always" is a real routing choice - a cleanup step that must run
        // whether or not the step before it failed - and the edge panel offers
        // it by name. It used to work only by accident, falling through the
        // "anything else" branch below.
        if (connection.Condition == "always")
            return true;

        // An expression the engine cannot evaluate. This used to `return true`,
        // which meant a custom condition always fired - the one outcome that
        // looks like it works while ignoring what was written. Refusing is the
        // honest answer until expressions are actually implemented.
        throw new NotSupportedException(
            $"Edge condition '{connection.Condition}' is not supported. Use 'success', " +
            "'error' or 'always', or put the comparison in a condition node.");
    }

    /// <summary>
    /// The boolean a condition node produced, or null when the node produced
    /// none. Reads the shape the built-in handler returns and the shapes it
    /// survives a round-trip through JSON as, since a node result may have been
    /// deserialized rather than freshly computed.
    /// </summary>
    private static bool? ReadConditionVerdict(object? data)
    {
        switch (data)
        {
            case null:
                return null;
            case bool direct:
                return direct;
            case IDictionary<string, object?> map:
                return map.TryGetValue("condition", out var boxed) && boxed is bool value ? value : null;
            case JsonElement json when json.ValueKind == JsonValueKind.Object:
                return json.TryGetProperty("condition", out var property)
                       && property.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? property.GetBoolean()
                    : null;
        }

        // The built-in handler returns an anonymous type, which has no shared
        // interface to read it through.
        var member = data.GetType().GetProperty("condition")
                     ?? data.GetType().GetProperty("Condition");

        return member?.GetValue(data) as bool?;
    }

    private void Log(WorkflowExecutionContext context, string message)
    {
        var logMessage = $"[{DateTime.UtcNow:HH:mm:ss}] {message}";
        context.ExecutionLog.Add(logMessage);
        _logger?.Invoke(logMessage);
    }

    private void RegisterDefaultHandlers()
    {
        // Trigger node - the workflow's entry point.
        //
        // Execution starts from every node with no incoming connection, and those
        // are normally trigger nodes. Without a handler registered under the
        // "trigger" type, ExecuteNodeHandlerAsync fell through to
        // NotImplementedException("No handler registered for {integration}:"),
        // so EVERY workflow built the natural way - starting from a trigger -
        // failed on its first node. At execution time the trigger has already
        // fired; the node's job is only to hand the run's starting data to the
        // nodes downstream.
        RegisterNodeHandler("trigger", (node, context) =>
        {
            var payload = context.Variables.GetValueOrDefault("input");
            return Task.FromResult<object?>(payload ?? new
            {
                triggered = true,
                executionId = context.ExecutionId,
                node = node.Id,
            });
        });

        // Transform node - manipulate data
        RegisterNodeHandler("transform", async (node, context) =>
        {
            var input = node.Parameters.GetValueOrDefault("input");
            var transformType = node.Parameters.GetValueOrDefault("type")?.ToString() ?? "json";

            if (transformType == "json")
            {
                var json = node.Parameters.GetValueOrDefault("json")?.ToString() ?? "{}";
                return JsonSerializer.Deserialize<object>(json);
            }

            return input;
        });

        // Variable node - set/get workflow variables
        RegisterNodeHandler("variable:set", async (node, context) =>
        {
            var name = node.Parameters.GetValueOrDefault("name")?.ToString() ?? "";
            var value = node.Parameters.GetValueOrDefault("value");

            if (!string.IsNullOrEmpty(name))
            {
                context.Variables[name] = value!;
            }

            return value;
        });

        RegisterNodeHandler("variable:get", async (node, context) =>
        {
            var name = node.Parameters.GetValueOrDefault("name")?.ToString() ?? "";
            return context.Variables.GetValueOrDefault(name);
        });

        // Delay node - wait for specified time (observes the execution's cancel token
        // so POST /executions/{id}/cancel interrupts the wait instead of sitting it out)
        RegisterNodeHandler("delay", async (node, context) =>
        {
            var seconds = Convert.ToInt32(node.Parameters.GetValueOrDefault("seconds", 1));
            await Task.Delay(TimeSpan.FromSeconds(seconds), context.CancellationToken);
            return new { delayed = seconds };
        });

        // Condition node - branching logic
        RegisterNodeHandler("condition", async (node, context) =>
        {
            var left = node.Parameters.GetValueOrDefault("left");
            var right = node.Parameters.GetValueOrDefault("right");
            var operation = node.Parameters.GetValueOrDefault("operation")?.ToString() ?? "equals";

            var result = operation switch
            {
                "equals" => Equals(left, right),
                "not_equals" => !Equals(left, right),
                "greater_than" => Convert.ToDouble(left) > Convert.ToDouble(right),
                "less_than" => Convert.ToDouble(left) < Convert.ToDouble(right),
                "contains" => left?.ToString()?.Contains(right?.ToString() ?? "") ?? false,
                _ => false
            };

            return new { condition = result };
        });

        // Loop node - iterate over collection
        RegisterNodeHandler("loop", async (node, context) =>
        {
            var items = node.Parameters.GetValueOrDefault("items") as IEnumerable<object>;
            var results = new List<object?>();

            if (items != null)
            {
                foreach (var item in items)
                {
                    context.Variables["currentItem"] = item;
                    results.Add(item);
                }
            }

            return results;
        });
    }
}

/// <summary>
/// Validates a <see cref="VisualWorkflow"/>.
///
/// Named VisualWorkflowValidator, not WorkflowValidator, because
/// Loco.Core.Workflow (singular) also defines a WorkflowValidator - one that
/// validates a WorkflowDefinition instead. Two identically-named types in
/// namespaces differing by one letter is a trap: which one a file gets depends
/// entirely on its usings, and picking the wrong one fails only at the call
/// site's argument type.
/// </summary>
public class VisualWorkflowValidator
{
    public VisualWorkflowValidationResult Validate(VisualWorkflow workflow)
    {
        var result = new VisualWorkflowValidationResult();

        // Check basic requirements
        if (string.IsNullOrEmpty(workflow.Name))
            result.Errors.Add("Workflow name is required");

        if (!workflow.Nodes.Any())
            result.Errors.Add("Workflow must have at least one node");

        // Check for trigger nodes
        var triggerNodes = workflow.Nodes
            .Where(n => !workflow.Connections.Any(c => c.TargetNodeId == n.Id))
            .ToList();

        if (!triggerNodes.Any())
            result.Errors.Add("Workflow must have at least one trigger node (node with no incoming connections)");

        // Check for orphaned nodes
        var connectedNodeIds = workflow.Connections
            .SelectMany(c => new[] { c.SourceNodeId, c.TargetNodeId })
            .Distinct()
            .ToHashSet();

        var orphanedNodes = workflow.Nodes
            .Where(n => !connectedNodeIds.Contains(n.Id) && !triggerNodes.Any(t => t.Id == n.Id))
            .ToList();

        foreach (var orphan in orphanedNodes)
        {
            result.Warnings.Add($"Node '{orphan.Name}' is not connected to any other nodes");
        }

        // Check for invalid connections
        foreach (var connection in workflow.Connections)
        {
            if (!workflow.Nodes.Any(n => n.Id == connection.SourceNodeId))
                result.Errors.Add($"Connection references non-existent source node: {connection.SourceNodeId}");

            if (!workflow.Nodes.Any(n => n.Id == connection.TargetNodeId))
                result.Errors.Add($"Connection references non-existent target node: {connection.TargetNodeId}");
        }

        // Check for circular dependencies
        if (HasCircularDependency(workflow))
        {
            result.Errors.Add("Workflow contains circular dependencies");
        }

        return result;
    }

    private bool HasCircularDependency(VisualWorkflow workflow)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var node in workflow.Nodes)
        {
            if (HasCircularDependencyDFS(node.Id, workflow, visited, recursionStack))
                return true;
        }

        return false;
    }

    private bool HasCircularDependencyDFS(string nodeId, VisualWorkflow workflow, HashSet<string> visited, HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(nodeId))
            return true;

        if (visited.Contains(nodeId))
            return false;

        visited.Add(nodeId);
        recursionStack.Add(nodeId);

        var children = workflow.Connections
            .Where(c => c.SourceNodeId == nodeId)
            .Select(c => c.TargetNodeId);

        foreach (var child in children)
        {
            if (HasCircularDependencyDFS(child, workflow, visited, recursionStack))
                return true;
        }

        recursionStack.Remove(nodeId);
        return false;
    }
}

public class VisualWorkflowValidationResult
{
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool IsValid => !Errors.Any();
}
