// John Carmack: "Code should be written to be read by humans"
// Rob Pike: "The bigger the interface, the weaker the abstraction"

using System.Text.Json;
using Loco.Core.Workflows;

namespace Loco.Core.Integrations.Core;

/// <summary>
/// Bridges connector system with the Visual Workflow Engine
/// Registers all connector actions as workflow node handlers
/// </summary>
public sealed class WorkflowConnectorBridge : IDisposable
{
    private readonly ConnectorRegistry _registry;
    private readonly VisualWorkflowEngine _engine;
    private readonly WebhookReceiver _webhookReceiver;
    private readonly Dictionary<string, ConnectorConfiguration> _connectorConfigs = new();

    /// <summary>
    /// One connector instance per (connectorId, credentialId), so a workflow can
    /// use two accounts of the same service at once.
    ///
    /// ConnectorRegistry caches a single instance per connector id, and
    /// InitializeAsync replaces that instance's configuration outright. Two
    /// Slack nodes on different workspaces therefore both ran against whichever
    /// credential was applied last - posting to the wrong workspace, with
    /// nothing anywhere reporting it. The API refused such workflows rather than
    /// guess; this is what makes them work instead.
    ///
    /// Bounded by the number of connections in use, which is small. The stored
    /// configuration is kept alongside so a repeat call with an unchanged
    /// credential does not re-initialize: re-initializing disposes the
    /// connector's HttpClient, and two overlapping runs of the same workflow
    /// would otherwise pull it out from under each other.
    /// </summary>
    private readonly Dictionary<(string ConnectorId, string CredentialId),
        (IConnector Connector, ConnectorConfiguration Config)> _credentialed = new();

    private readonly SemaphoreSlim _credentialedLock = new(1, 1);
    private bool _disposed;

    public WorkflowConnectorBridge(
        ConnectorRegistry registry,
        VisualWorkflowEngine engine,
        WebhookReceiver? webhookReceiver = null)
    {
        _registry = registry;
        _engine = engine;
        _webhookReceiver = webhookReceiver ?? new WebhookReceiver();
    }

    /// <summary>
    /// Configure credentials for a connector
    /// </summary>
    public void ConfigureConnector(string connectorId, ConnectorConfiguration config)
    {
        _connectorConfigs[connectorId] = config;
    }

    /// <summary>
    /// Configure a connector's credentials AND initialize it now.
    /// </summary>
    /// <remarks>
    /// <see cref="ConfigureConnector"/> only records the configuration; it takes
    /// effect solely if <see cref="RegisterConnectorAsync"/> runs afterwards.
    /// Connectors are registered once at startup, before any credential exists,
    /// so configuring one later had no effect and the connector kept executing
    /// uninitialized - the reason every connector action failed on a null
    /// HttpClient.
    ///
    /// Callers resolving credentials per execution use this instead. Repeated
    /// calls are expected (each run re-applies its workflow's connections);
    /// InitializeAsync implementations dispose any previous HttpClient before
    /// replacing it, so re-initializing does not leak.
    /// </remarks>
    public async Task ConfigureConnectorAsync(
        string connectorId, ConnectorConfiguration config, CancellationToken ct = default)
    {
        _connectorConfigs[connectorId] = config;

        var connector = _registry.GetConnector(connectorId);
        if (connector is null)
        {
            // Not every node is connector-backed (transform/condition/delay/loop
            // are engine built-ins), so an unknown id is not an error here.
            return;
        }

        await connector.InitializeAsync(config, ct);
    }

    /// <summary>
    /// Configures the connector instance belonging to one connection, and
    /// initializes it now.
    /// </summary>
    /// <remarks>
    /// Nodes carrying this <paramref name="credentialId"/> execute against this
    /// instance, so two connections for the same connector stay independent
    /// within a single workflow.
    ///
    /// Repeated calls are expected - every run re-applies its workflow's
    /// connections - so an unchanged configuration is left alone rather than
    /// re-initialized. That matters because InitializeAsync disposes the
    /// connector's HttpClient, which would break a concurrent run mid-action.
    /// </remarks>
    public async Task ConfigureConnectionAsync(
        string connectorId,
        string credentialId,
        ConnectorConfiguration config,
        CancellationToken ct = default)
    {
        var key = (connectorId, credentialId);

        await _credentialedLock.WaitAsync(ct);
        IConnector connector;
        try
        {
            if (_credentialed.TryGetValue(key, out var existing))
            {
                if (SameCredentials(existing.Config, config))
                {
                    return;
                }

                connector = existing.Connector;
            }
            else
            {
                var created = _registry.CreateConnector(connectorId);
                if (created is null)
                {
                    // Engine built-ins carry no connector; not an error here.
                    return;
                }

                connector = created;
            }

            _credentialed[key] = (connector, config);
        }
        finally
        {
            _credentialedLock.Release();
        }

        await connector.InitializeAsync(config, ct);
    }

    /// <summary>
    /// Whether two configurations carry the same credential values. Compared by
    /// value because a connection re-read from the store is a different object
    /// holding the same secrets, and re-initializing on that would defeat the
    /// point of caching the instance.
    /// </summary>
    private static bool SameCredentials(ConnectorConfiguration a, ConnectorConfiguration b)
    {
        if (a.Credentials.Count != b.Credentials.Count) return false;

        foreach (var (name, value) in a.Credentials)
        {
            if (!b.Credentials.TryGetValue(name, out var other)) return false;
            if (!Equals(value, other)) return false;
        }

        return true;
    }

    /// <summary>
    /// Removes the instance a connection owns, after the connection itself is
    /// gone. Without this a deleted connection's credentials would stay live in
    /// memory for as long as the process runs.
    /// </summary>
    public async Task ReleaseConnectionAsync(string credentialId, CancellationToken ct = default)
    {
        List<(string ConnectorId, string CredentialId)> keys;

        await _credentialedLock.WaitAsync(ct);
        try
        {
            keys = _credentialed.Keys
                .Where(k => string.Equals(k.CredentialId, credentialId, StringComparison.Ordinal))
                .ToList();

            foreach (var key in keys)
            {
                if (_credentialed.Remove(key, out var entry) && entry.Connector is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
        finally
        {
            _credentialedLock.Release();
        }
    }

    /// <summary>
    /// Register all connectors with the workflow engine
    /// Automatically creates handlers for each connector action
    /// </summary>
    public async Task RegisterAllConnectorsAsync(CancellationToken ct = default)
    {
        var metadata = _registry.GetAllMetadata();

        foreach (var (connectorId, connectorMeta) in metadata)
        {
            await RegisterConnectorAsync(connectorId, ct);
        }
    }

    /// <summary>
    /// Register a single connector with the workflow engine
    /// </summary>
    public async Task RegisterConnectorAsync(string connectorId, CancellationToken ct = default)
    {
        var connector = _registry.GetConnector(connectorId);
        if (connector == null)
        {
            throw new InvalidOperationException($"Connector not found: {connectorId}");
        }

        // Initialize connector with configuration if available
        if (_connectorConfigs.TryGetValue(connectorId, out var config))
        {
            await connector.InitializeAsync(config, ct);
        }

        // Register each action as a workflow node handler
        foreach (var action in connector.Actions)
        {
            var handlerKey = $"{connectorId}:{action.Id}";

            _engine.RegisterNodeHandler(handlerKey, async (node, context) =>
            {
                // Resolved per node, not captured: a node naming a connection
                // must run against that connection's own instance, and which
                // connection that is only becomes known at execution time.
                return await ExecuteConnectorActionAsync(
                    ResolveConnector(connector, node),
                    action,
                    node,
                    context,
                    ct);
            });
        }

        // Also register by integration:action pattern used in visual workflows
        foreach (var action in connector.Actions)
        {
            var handlerKey = $"{connector.Id.ToLowerInvariant()}:{action.Id.ToLowerInvariant()}";

            _engine.RegisterNodeHandler(handlerKey, async (node, context) =>
            {
                // Resolved per node, not captured: a node naming a connection
                // must run against that connection's own instance, and which
                // connection that is only becomes known at execution time.
                return await ExecuteConnectorActionAsync(
                    ResolveConnector(connector, node),
                    action,
                    node,
                    context,
                    ct);
            });
        }
    }

    /// <summary>
    /// The connector instance a node should run against.
    ///
    /// A node naming a connection gets that connection's own instance;
    /// everything else gets the registry's shared one, which is what a workflow
    /// with a single connection per connector has always used.
    ///
    /// Falling back rather than failing is deliberate: a node can name a
    /// connection the caller never configured (a workflow saved before the
    /// connection was deleted, say), and the shared instance then produces the
    /// connector's own "not initialized" error, which says more than a lookup
    /// miss here would.
    /// </summary>
    private IConnector ResolveConnector(IConnector shared, WorkflowNode node)
    {
        if (string.IsNullOrEmpty(node.CredentialId)) return shared;

        _credentialedLock.Wait();
        try
        {
            return _credentialed.TryGetValue((shared.Id, node.CredentialId), out var entry)
                ? entry.Connector
                : shared;
        }
        finally
        {
            _credentialedLock.Release();
        }
    }

    /// <summary>
    /// Execute a connector action within a workflow node
    /// </summary>
    private async Task<object?> ExecuteConnectorActionAsync(
        IConnector connector,
        ConnectorAction action,
        WorkflowNode node,
        WorkflowExecutionContext workflowContext,
        CancellationToken ct)
    {
        // Build action parameters from node parameters
        var parameters = new ActionParameters();
        foreach (var param in node.Parameters)
        {
            // Resolve variable references {{varName}}
            var value = ResolveValue(param.Value, workflowContext);
            parameters.Set(param.Key, value);
        }

        // Create execution context
        var executionContext = new ExecutionContext
        {
            WorkflowId = workflowContext.WorkflowId,
            ExecutionId = workflowContext.ExecutionId,
            NodeId = node.Id,
            Variables = workflowContext.Variables.ToDictionary(
                kvp => kvp.Key,
                kvp => (object?)kvp.Value),
            PreviousOutputs = workflowContext.NodeResults.ToDictionary(
                kvp => kvp.Key,
                kvp => (object?)kvp.Value.Data)
        };

        // Execute the action
        var result = await connector.ExecuteAsync(action.Id, parameters, executionContext, ct);

        if (!result.Success)
        {
            throw new ConnectorActionException(
                connector.Id,
                action.Id,
                result.ErrorMessage ?? "Action failed",
                result.ErrorCode);
        }

        return result.Data;
    }

    /// <summary>
    /// Resolve variable references in values
    /// Supports: {{variableName}}, {{nodeId.data.property}}, {{previous.property}}
    /// </summary>
    private object? ResolveValue(object? value, WorkflowExecutionContext context)
    {
        if (value is not string strValue)
            return value;

        // Check for full variable reference
        if (strValue.StartsWith("{{") && strValue.EndsWith("}}"))
        {
            var path = strValue[2..^2].Trim();
            return ResolveVariablePath(path, context);
        }

        // Check for inline variable references
        var result = strValue;
        var startIdx = 0;

        while (true)
        {
            var openIdx = result.IndexOf("{{", startIdx);
            if (openIdx < 0) break;

            var closeIdx = result.IndexOf("}}", openIdx);
            if (closeIdx < 0) break;

            var path = result.Substring(openIdx + 2, closeIdx - openIdx - 2).Trim();
            var resolved = ResolveVariablePath(path, context)?.ToString() ?? "";

            result = result[..openIdx] + resolved + result[(closeIdx + 2)..];
            startIdx = openIdx + resolved.Length;
        }

        return result;
    }

    private object? ResolveVariablePath(string path, WorkflowExecutionContext context)
    {
        var parts = path.Split('.');

        if (parts.Length == 0)
            return null;

        // Check workflow variables first
        if (context.Variables.TryGetValue(parts[0], out var variable))
        {
            if (parts.Length == 1)
                return variable;

            return NavigateObject(variable, parts[1..]);
        }

        // Check node results
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

        // "previous" keyword for last executed node
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

    private object? NavigateObject(object? obj, string[] path)
    {
        if (obj == null || path.Length == 0)
            return obj;

        var current = obj;

        foreach (var part in path)
        {
            if (current == null)
                return null;

            // Handle JsonElement
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

            // Handle Dictionary
            if (current is IDictionary<string, object?> dict)
            {
                if (dict.TryGetValue(part, out var value))
                {
                    current = value;
                    continue;
                }
                return null;
            }

            // Handle anonymous types and regular objects
            var prop2 = current.GetType().GetProperty(part);
            if (prop2 != null)
            {
                current = prop2.GetValue(current);
                continue;
            }

            return null;
        }

        return current;
    }

    /// <summary>
    /// Create a workflow execution service that handles trigger events
    /// </summary>
    public WorkflowTriggerService CreateTriggerService(string baseWebhookUrl)
    {
        return new WorkflowTriggerService(this, _webhookReceiver, baseWebhookUrl);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var (connector, _) in _credentialed.Values)
        {
            if (connector is IDisposable disposable) disposable.Dispose();
        }
        _credentialed.Clear();

        _credentialedLock.Dispose();
        _webhookReceiver.Dispose();
    }
}

/// <summary>
/// Service for managing workflow triggers and webhook-initiated executions
/// </summary>
public sealed class WorkflowTriggerService
{
    private readonly WorkflowConnectorBridge _bridge;
    private readonly WebhookReceiver _webhookReceiver;
    private readonly string _baseWebhookUrl;
    private readonly Dictionary<string, VisualWorkflow> _triggerWorkflows = new();

    public WorkflowTriggerService(
        WorkflowConnectorBridge bridge,
        WebhookReceiver webhookReceiver,
        string baseWebhookUrl)
    {
        _bridge = bridge;
        _webhookReceiver = webhookReceiver;
        _baseWebhookUrl = baseWebhookUrl;

        // Subscribe to webhook events
        _webhookReceiver.OnWebhookReceived += HandleWebhookAsync;
    }

    /// <summary>
    /// Register a workflow to be triggered by webhooks
    /// </summary>
    public WebhookEndpoint RegisterWorkflowTrigger(
        VisualWorkflow workflow,
        string connectorId,
        string triggerId,
        Dictionary<string, string>? filters = null)
    {
        var endpoint = _webhookReceiver.RegisterWebhook(new WebhookRegistrationRequest
        {
            ConnectorId = connectorId,
            TriggerId = triggerId,
            WorkflowId = workflow.Id,
            Filters = filters
        });

        _triggerWorkflows[endpoint.Id] = workflow;

        return endpoint;
    }

    /// <summary>
    /// Unregister a workflow trigger
    /// </summary>
    public bool UnregisterWorkflowTrigger(string webhookId)
    {
        _triggerWorkflows.Remove(webhookId);
        return _webhookReceiver.UnregisterWebhook(webhookId);
    }

    /// <summary>
    /// Get the full webhook URL for an endpoint
    /// </summary>
    public string GetWebhookUrl(WebhookEndpoint endpoint)
    {
        return endpoint.GetFullUrl(_baseWebhookUrl);
    }

    private async Task HandleWebhookAsync(WebhookEvent evt, CancellationToken ct)
    {
        if (!_triggerWorkflows.TryGetValue(evt.WebhookId, out var workflow))
            return;

        // Create initial variables from webhook payload
        var initialVariables = new Dictionary<string, object>
        {
            ["trigger"] = new Dictionary<string, object?>
            {
                ["eventId"] = evt.Id,
                ["webhookId"] = evt.WebhookId,
                ["connectorId"] = evt.ConnectorId,
                ["triggerId"] = evt.TriggerId,
                ["receivedAt"] = evt.ReceivedAt,
                ["method"] = evt.Method,
                ["headers"] = evt.Headers,
                ["payload"] = evt.Payload?.ToString()
            }
        };

        // Add payload properties as top-level variables
        if (evt.Payload.HasValue && evt.Payload.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in evt.Payload.Value.EnumerateObject())
            {
                initialVariables[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString()!,
                    JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => prop.Value.GetRawText()
                };
            }
        }

        // Execute the workflow - fire and forget, or you can track executions
        // TODO: Add execution tracking, retry logic, etc.
        _ = Task.Run(async () =>
        {
            try
            {
                // Get or create engine instance for this workflow
                var engine = new VisualWorkflowEngine();
                await engine.ExecuteAsync(workflow, initialVariables, ct);
            }
            catch (Exception ex)
            {
                // Log error - in production, use proper logging
                Console.Error.WriteLine($"Workflow execution failed: {ex.Message}");
            }
        }, ct);
    }
}

/// <summary>
/// Exception thrown when a connector action fails
/// </summary>
public sealed class ConnectorActionException : Exception
{
    public string ConnectorId { get; }
    public string ActionId { get; }
    public string? ErrorCode { get; }

    public ConnectorActionException(
        string connectorId,
        string actionId,
        string message,
        string? errorCode = null)
        : base(message)
    {
        ConnectorId = connectorId;
        ActionId = actionId;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Extension methods for easy workflow-connector integration setup
/// </summary>
public static class WorkflowConnectorExtensions
{
    /// <summary>
    /// Create a workflow engine with all registered connectors
    /// </summary>
    public static async Task<(VisualWorkflowEngine Engine, WorkflowConnectorBridge Bridge)> CreateConnectorEnabledEngineAsync(
        this ConnectorRegistry registry,
        Dictionary<string, ConnectorConfiguration>? configs = null,
        Action<string>? logger = null,
        CancellationToken ct = default)
    {
        var engine = new VisualWorkflowEngine(logger);
        var bridge = new WorkflowConnectorBridge(registry, engine);

        // Apply configurations
        if (configs != null)
        {
            foreach (var (connectorId, config) in configs)
            {
                bridge.ConfigureConnector(connectorId, config);
            }
        }

        // Register all connectors
        await bridge.RegisterAllConnectorsAsync(ct);

        return (engine, bridge);
    }

    /// <summary>
    /// Build a visual workflow that uses connector actions
    /// </summary>
    public static VisualWorkflowBuilder AddConnectorNode(
        this VisualWorkflowBuilder builder,
        string name,
        string connectorId,
        string actionId,
        Dictionary<string, object>? parameters = null)
    {
        return builder.AddNode(name, "action", connectorId, actionId, parameters);
    }

    /// <summary>
    /// Add a trigger node that responds to webhooks
    /// </summary>
    public static VisualWorkflowBuilder AddTriggerNode(
        this VisualWorkflowBuilder builder,
        string name,
        string connectorId,
        string triggerId,
        Dictionary<string, object>? parameters = null)
    {
        return builder.AddNode(name, "trigger", connectorId, triggerId, parameters);
    }
}

/// <summary>
/// Pre-built workflow templates using connectors
/// </summary>
public static class ConnectorWorkflowTemplates
{
    /// <summary>
    /// GitHub PR -> Slack notification workflow
    /// </summary>
    public static VisualWorkflow GitHubPrToSlack(string slackChannel)
    {
        return new VisualWorkflowBuilder()
            .WithName("GitHub PR to Slack")
            .WithDescription("Notify Slack when a GitHub PR is opened")
            .AddTriggerNode("PR Opened", "github", "onPullRequest")
            .AddConnectorNode("Notify Slack", "slack", "sendMessage", new Dictionary<string, object>
            {
                ["channel"] = slackChannel,
                ["message"] = "New PR: {{trigger.payload.pull_request.title}} by {{trigger.payload.pull_request.user.login}}\n{{trigger.payload.pull_request.html_url}}"
            })
            .Connect("PR Opened", "Notify Slack")
            .Build();
    }

    /// <summary>
    /// HTTP Webhook -> Database insert workflow
    /// </summary>
    public static VisualWorkflow WebhookToDatabase(string tableName)
    {
        return new VisualWorkflowBuilder()
            .WithName("Webhook to Database")
            .WithDescription("Insert webhook data into database")
            .AddTriggerNode("Webhook", "http", "onWebhook")
            .AddConnectorNode("Insert Data", "postgresql", "execute", new Dictionary<string, object>
            {
                ["sql"] = $"INSERT INTO {tableName} (data, created_at) VALUES (@data, NOW())",
                ["parameters"] = new Dictionary<string, object>
                {
                    ["data"] = "{{trigger.payload}}"
                }
            })
            .Connect("Webhook", "Insert Data")
            .Build();
    }

    /// <summary>
    /// Email -> Twilio SMS notification workflow
    /// </summary>
    public static VisualWorkflow EmailToSms(string phoneNumber)
    {
        return new VisualWorkflowBuilder()
            .WithName("Email to SMS")
            .WithDescription("Send SMS when important email arrives")
            .AddTriggerNode("Email Received", "email", "onReceive")
            .AddNode("Check Important", "condition", "condition", "check", new Dictionary<string, object>
            {
                ["left"] = "{{trigger.payload.subject}}",
                ["operation"] = "contains",
                ["right"] = "URGENT"
            })
            .AddConnectorNode("Send SMS", "twilio", "sendSms", new Dictionary<string, object>
            {
                ["to"] = phoneNumber,
                ["body"] = "Urgent email from {{trigger.payload.from}}: {{trigger.payload.subject}}"
            })
            .Connect("Email Received", "Check Important")
            .Connect("Check Important", "Send SMS", "success")
            .Build();
    }

    /// <summary>
    /// Scheduled database backup to S3 workflow
    /// </summary>
    public static VisualWorkflow DatabaseBackupToS3(string bucket)
    {
        return new VisualWorkflowBuilder()
            .WithName("Database Backup to S3")
            .WithDescription("Export database query results to S3")
            .AddTriggerNode("Schedule", "schedule", "cron", new Dictionary<string, object>
            {
                ["expression"] = "0 0 * * *" // Daily at midnight
            })
            .AddConnectorNode("Query Data", "postgresql", "query", new Dictionary<string, object>
            {
                ["sql"] = "SELECT * FROM important_data WHERE updated_at > NOW() - INTERVAL '1 day'"
            })
            .AddNode("Transform", "transform", "transform", "json", new Dictionary<string, object>
            {
                ["input"] = "{{previous.rows}}",
                ["type"] = "json"
            })
            .AddConnectorNode("Upload to S3", "aws-s3", "uploadContent", new Dictionary<string, object>
            {
                ["bucket"] = bucket,
                ["key"] = "backups/{{trigger.timestamp}}.json",
                ["content"] = "{{previous}}",
                ["contentType"] = "application/json"
            })
            .Connect("Schedule", "Query Data")
            .Connect("Query Data", "Transform")
            .Connect("Transform", "Upload to S3")
            .Build();
    }

    /// <summary>
    /// Redis cache warming workflow
    /// </summary>
    public static VisualWorkflow CacheWarmingWorkflow()
    {
        return new VisualWorkflowBuilder()
            .WithName("Cache Warming")
            .WithDescription("Pre-warm Redis cache from database")
            .AddTriggerNode("Start", "schedule", "interval", new Dictionary<string, object>
            {
                ["minutes"] = 15
            })
            .AddConnectorNode("Get Hot Data", "postgresql", "query", new Dictionary<string, object>
            {
                ["sql"] = "SELECT id, data FROM hot_items ORDER BY access_count DESC LIMIT 100"
            })
            .AddNode("Loop Items", "loop", "loop", "iterate", new Dictionary<string, object>
            {
                ["items"] = "{{previous.rows}}"
            })
            .AddConnectorNode("Cache Item", "redis", "set", new Dictionary<string, object>
            {
                ["key"] = "item:{{currentItem.id}}",
                ["value"] = "{{currentItem.data}}",
                ["ttl"] = 900 // 15 minutes
            })
            .Connect("Start", "Get Hot Data")
            .Connect("Get Hot Data", "Loop Items")
            .Connect("Loop Items", "Cache Item")
            .Build();
    }
}
