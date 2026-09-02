// John Carmack: "Code should be written to be read by humans"
// Rob Pike: "The bigger the interface, the weaker the abstraction"

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

    public WorkflowConnectorBridge(ConnectorRegistry registry, VisualWorkflowEngine engine)
    {
        _registry = registry;
        _engine = engine;
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
                //
                // context.CancellationToken, NOT the `ct` this method was
                // called with. Connectors are registered once at startup, so
                // that token is ConnectorStartupService's host-startup token -
                // it is cancelled when the SERVER shuts down, never when a run
                // is cancelled. POST /executions/{id}/cancel therefore stopped
                // a workflow only between nodes: an HTTP call already in flight
                // inside a connector ran to completion, and a slow one kept the
                // execution alive long after the user cancelled it. The engine
                // puts the execution's own token on the context precisely so a
                // handler can reach it (VisualWorkflowEngine.CancellationToken).
                return await ExecuteConnectorActionAsync(
                    ResolveConnector(connector, node),
                    action,
                    node,
                    context,
                    context.CancellationToken);
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
                //
                // context.CancellationToken, NOT the `ct` this method was
                // called with. Connectors are registered once at startup, so
                // that token is ConnectorStartupService's host-startup token -
                // it is cancelled when the SERVER shuts down, never when a run
                // is cancelled. POST /executions/{id}/cancel therefore stopped
                // a workflow only between nodes: an HTTP call already in flight
                // inside a connector ran to completion, and a slow one kept the
                // execution alive long after the user cancelled it. The engine
                // puts the execution's own token on the context precisely so a
                // handler can reach it (VisualWorkflowEngine.CancellationToken).
                return await ExecuteConnectorActionAsync(
                    ResolveConnector(connector, node),
                    action,
                    node,
                    context,
                    context.CancellationToken);
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
        // Node parameters arrive already resolved: VisualWorkflowEngine applies
        // WorkflowVariableResolver at its single dispatch point, so connector
        // actions and the engine's built-ins resolve identically. Resolving
        // again here would expand a {{...}} that a variable's own VALUE
        // contained, which is data, not a reference.
        var parameters = new ActionParameters();
        foreach (var param in node.Parameters)
        {
            parameters.Set(param.Key, param.Value);
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
