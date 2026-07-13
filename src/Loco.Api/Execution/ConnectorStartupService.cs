using Loco.Core.Integrations.Core;

namespace Loco.Api.Execution;

/// <summary>
/// Startup wiring that finally makes the connector catalog reachable from a
/// running binary: discovers every IConnector in Loco.Core and registers each
/// connector action as a "{connectorId}:{actionId}" node handler on the
/// singleton VisualWorkflowEngine (via WorkflowConnectorBridge).
///
/// Connectors are registered WITHOUT credentials here - the bridge only calls
/// InitializeAsync for connectors that have a ConnectorConfiguration, so
/// unconfigured connector nodes fail at execution time with a clear error
/// while log/transform/condition/delay/variable nodes work out of the box.
/// </summary>
public sealed class ConnectorStartupService : IHostedService
{
    private readonly ConnectorRegistry _registry;
    private readonly WorkflowConnectorBridge _bridge;
    private readonly ILogger<ConnectorStartupService> _logger;

    public ConnectorStartupService(
        ConnectorRegistry registry,
        WorkflowConnectorBridge bridge,
        ILogger<ConnectorStartupService> logger)
    {
        _registry = registry;
        _bridge = bridge;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var discovered = _registry.AutoDiscover(typeof(ConnectorRegistry).Assembly);
        await _bridge.RegisterAllConnectorsAsync(cancellationToken);
        _logger.LogInformation(
            "Registered {Count} connectors as workflow node handlers", discovered);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
