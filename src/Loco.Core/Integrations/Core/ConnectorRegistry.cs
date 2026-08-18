// Rob Pike: "Clear is better than clever"
// John Carmack: "Focus on the data and the structure will follow"

using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;
using Loco.Core.Practical;

namespace Loco.Core.Integrations.Core;

/// <summary>
/// Registry for discovering and managing connectors
/// Provides connector discovery, registration, and lifecycle management
/// </summary>
public sealed class ConnectorRegistry : IDisposable
{
    private readonly ConcurrentDictionary<string, ConnectorRegistration> _connectors = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IConnector> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly SimpleLogger _logger;
    private FrozenDictionary<string, ConnectorMetadata>? _metadataCache;

    public ConnectorRegistry(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(ConnectorRegistry));
    }

    /// <summary>
    /// Register a connector type
    /// </summary>
    public void Register<TConnector>() where TConnector : IConnector, new()
    {
        var instance = new TConnector();
        Register(instance);
    }

    /// <summary>
    /// Register a connector instance
    /// </summary>
    public void Register(IConnector connector)
    {
        var registration = new ConnectorRegistration
        {
            Id = connector.Id,
            Type = connector.GetType(),
            Metadata = ConnectorMetadata.FromConnector(connector)
        };

        if (_connectors.TryAdd(connector.Id, registration))
        {
            _metadataCache = null; // Invalidate cache
            _logger.Info($"Registered connector: {connector.Id} ({connector.Name})");
        }
        else
        {
            _logger.Warning($"Connector {connector.Id} already registered");
        }
    }

    /// <summary>
    /// Unregister a connector
    /// </summary>
    public void Unregister(string connectorId)
    {
        if (_connectors.TryRemove(connectorId, out _))
        {
            // Cleanup any existing instance
            if (_instances.TryRemove(connectorId, out var instance))
            {
                instance.CleanupAsync().GetAwaiter().GetResult();
                if (instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _metadataCache = null; // Invalidate cache
            _logger.Info($"Unregistered connector: {connectorId}");
        }
    }

    /// <summary>
    /// Get a connector by ID
    /// </summary>
    public IConnector? GetConnector(string connectorId)
    {
        if (_connectors.TryGetValue(connectorId, out var registration))
        {
            return _instances.GetOrAdd(connectorId, _ =>
            {
                var instance = (IConnector)Activator.CreateInstance(registration.Type)!;
                _logger.Debug($"Created connector instance: {connectorId}");
                return instance;
            });
        }

        return null;
    }

    /// <summary>
    /// Creates a fresh, uncached connector instance.
    /// </summary>
    /// <remarks>
    /// <see cref="GetConnector"/> caches one instance per id, and initializing a
    /// connector replaces its configuration - so two connections for the same
    /// connector cannot both be live on the shared instance. Callers that need
    /// one instance per credential ask for their own here; the registry does not
    /// track it, so the caller owns its lifetime.
    /// </remarks>
    public IConnector? CreateConnector(string connectorId)
    {
        if (!_connectors.TryGetValue(connectorId, out var registration))
        {
            return null;
        }

        return (IConnector)Activator.CreateInstance(registration.Type)!;
    }

    /// <summary>
    /// Get a connector by ID (throws if not found)
    /// </summary>
    public IConnector GetRequiredConnector(string connectorId)
    {
        return GetConnector(connectorId)
            ?? throw new KeyNotFoundException($"Connector '{connectorId}' not found");
    }

    /// <summary>
    /// Get all registered connector IDs
    /// </summary>
    public IReadOnlyList<string> GetConnectorIds() =>
        _connectors.Keys.ToList();

    /// <summary>
    /// Get all connector metadata (cached)
    /// </summary>
    public FrozenDictionary<string, ConnectorMetadata> GetAllMetadata()
    {
        return _metadataCache ??= _connectors.Values
            .ToFrozenDictionary(r => r.Id, r => r.Metadata, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Get connector metadata by ID
    /// </summary>
    public ConnectorMetadata? GetMetadata(string connectorId)
    {
        var all = GetAllMetadata();
        return all.TryGetValue(connectorId, out var metadata) ? metadata : null;
    }

    /// <summary>
    /// Get connectors by category
    /// </summary>
    public IReadOnlyList<ConnectorMetadata> GetByCategory(ConnectorCategory category)
    {
        return GetAllMetadata().Values
            .Where(m => m.Category == category)
            .OrderBy(m => m.Name)
            .ToList();
    }

    /// <summary>
    /// Search connectors by name or description
    /// </summary>
    public IReadOnlyList<ConnectorMetadata> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return GetAllMetadata().Values.OrderBy(m => m.Name).ToList();
        }

        var lowerQuery = query.ToLowerInvariant();
        return GetAllMetadata().Values
            .Where(m =>
                m.Name.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                m.Description.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                m.Id.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.Name)
            .ToList();
    }

    /// <summary>
    /// Auto-discover and register all connectors from loaded assemblies
    /// </summary>
    public int AutoDiscover(params Assembly[] assemblies)
    {
        var targetAssemblies = assemblies.Length > 0
            ? assemblies
            : AppDomain.CurrentDomain.GetAssemblies();

        var count = 0;
        var connectorType = typeof(IConnector);

        foreach (var assembly in targetAssemblies)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => t.IsClass &&
                                !t.IsAbstract &&
                                connectorType.IsAssignableFrom(t) &&
                                t.GetConstructor(Type.EmptyTypes) != null);

                foreach (var type in types)
                {
                    try
                    {
                        var instance = (IConnector)Activator.CreateInstance(type)!;
                        Register(instance);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning($"Failed to instantiate connector {type.Name}: {ex.Message}");
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                _logger.Debug($"Skipping assembly {assembly.FullName}: {ex.Message}");
            }
        }

        _logger.Info($"Auto-discovered {count} connectors");
        return count;
    }

    /// <summary>
    /// Get initialized connector (creates and initializes if needed)
    /// </summary>
    public async Task<IConnector> GetInitializedConnectorAsync(
        string connectorId,
        ConnectorConfiguration config,
        CancellationToken ct = default)
    {
        var connector = GetRequiredConnector(connectorId);
        await connector.InitializeAsync(config, ct);
        return connector;
    }

    /// <summary>
    /// Test connector connection
    /// </summary>
    public async Task<ConnectionTestResult> TestConnectionAsync(
        string connectorId,
        ConnectorConfiguration config,
        CancellationToken ct = default)
    {
        var connector = GetRequiredConnector(connectorId);
        return await connector.TestConnectionAsync(config, ct);
    }

    public void Dispose()
    {
        foreach (var instance in _instances.Values)
        {
            instance.CleanupAsync().GetAwaiter().GetResult();
            if (instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _instances.Clear();
        _connectors.Clear();
    }
}

/// <summary>
/// Connector registration info
/// </summary>
internal sealed class ConnectorRegistration
{
    public required string Id { get; init; }
    public required Type Type { get; init; }
    public required ConnectorMetadata Metadata { get; init; }
}

/// <summary>
/// Connector metadata for UI display and discovery
/// </summary>
public sealed class ConnectorMetadata
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Version { get; init; }
    public required ConnectorCategory Category { get; init; }
    public required string IconUrl { get; init; }
    public required ConnectorCapabilities Capabilities { get; init; }
    public required AuthenticationType AuthType { get; init; }
    public required IReadOnlyList<string> ActionIds { get; init; }
    public required IReadOnlyList<string> TriggerIds { get; init; }

    public static ConnectorMetadata FromConnector(IConnector connector) => new()
    {
        Id = connector.Id,
        Name = connector.Name,
        Description = connector.Description,
        Version = connector.Version,
        Category = connector.Category,
        IconUrl = connector.IconUrl,
        Capabilities = connector.Capabilities,
        AuthType = connector.AuthConfig.Type,
        ActionIds = connector.Actions.Select(a => a.Id).ToList(),
        TriggerIds = connector.Triggers.Select(t => t.Id).ToList()
    };
}

/// <summary>
/// Extension methods for connector execution
/// </summary>
public static class ConnectorExtensions
{
    /// <summary>
    /// Execute a connector action with configuration
    /// </summary>
    public static async Task<ActionResult> ExecuteAsync(
        this ConnectorRegistry registry,
        string connectorId,
        string actionName,
        ConnectorConfiguration config,
        ActionParameters parameters,
        ExecutionContext context,
        CancellationToken ct = default)
    {
        var connector = await registry.GetInitializedConnectorAsync(connectorId, config, ct);
        return await connector.ExecuteAsync(actionName, parameters, context, ct);
    }

    /// <summary>
    /// Quick execute with anonymous object parameters
    /// </summary>
    public static async Task<ActionResult> ExecuteAsync(
        this IConnector connector,
        string actionName,
        object parameters,
        string workflowId = "workflow",
        string executionId = "exec",
        string nodeId = "node",
        CancellationToken ct = default)
    {
        var actionParams = new ActionParameters(parameters);
        var context = new ExecutionContext
        {
            WorkflowId = workflowId,
            ExecutionId = executionId,
            NodeId = nodeId,
            CancellationToken = ct
        };

        return await connector.ExecuteAsync(actionName, actionParams, context, ct);
    }
}
