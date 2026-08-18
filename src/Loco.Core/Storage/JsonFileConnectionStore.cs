using System.Text.Json;
using Loco.Core.Integrations.Core;
using Loco.Core.Security;

namespace Loco.Core.Storage;

/// <summary>
/// One stored set of connector credentials - what the editor calls a
/// "connection". Secret VALUES are never held on this type: they live in
/// <see cref="SecretsManager"/> under a derived key, and only the field names
/// are recorded here. Listing connections therefore cannot leak a secret, which
/// is what lets the API return this shape directly.
/// </summary>
public sealed class StoredConnection
{
    public string Id { get; set; } = string.Empty;
    public string ConnectorId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Names of the credential fields that were supplied - never their values.</summary>
    public List<string> ConfiguredFields { get; set; } = new();

    public string CreatedAt { get; set; } = string.Empty;
    public string? UpdatedAt { get; set; }
    public string? LastUsedAt { get; set; }
}

/// <summary>
/// Persists connector credentials for the editor's connection manager, and
/// resolves them into the <see cref="ConnectorConfiguration"/> the engine needs
/// at execution time.
///
/// This closes the product's largest gap. <c>WorkflowConnectorBridge.
/// ConfigureConnector</c> had no caller anywhere, so every connector ran without
/// ever being initialized - all 28 of them failed at execution with a null
/// HttpClient. There was no store, no API and no UI for credentials; this is the
/// store.
///
/// Design follows the two rules already established on the client side
/// (src/Loco.VisualEditor/src/api/connections.ts):
///  1. Secrets travel one way. Values go in through Save; they come back out
///     only via <see cref="BuildConfigurationAsync"/>, which hands them straight
///     to a connector. No read path returns them to a caller.
///  2. Workflows reference a connection by id, never by embedding a secret, so
///     an exported workflow JSON is safe to share.
///
/// Metadata uses the same durability pattern as JsonFileWorkflowStore
/// (SemaphoreSlim + cache + .tmp then File.Move). Values are delegated to
/// SecretsManager, which encrypts them with AES-256-GCM.
/// </summary>
public sealed class JsonFileConnectionStore
{
    private const string SecretKeyPrefix = "CONNECTION";

    private readonly string _filePath;
    private readonly SecretsManager _secrets;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private Dictionary<string, StoredConnection>? _cache;

    public JsonFileConnectionStore(string dataDirectory, SecretsManager? secrets = null)
    {
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "connections.json");
        _secrets = secrets ?? new SecretsManager(Path.Combine(dataDirectory, "secrets"));
    }

    /// <summary>
    /// Ids are used to build secret keys and must not be able to escape that
    /// namespace, so they are constrained to the same shape JsonFileWorkflowStore
    /// enforces on workflow ids.
    /// </summary>
    public static bool IsValidId(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        id.Length <= 128 &&
        id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');

    // ------------------------------------------------------------------ reads

    public async Task<IReadOnlyList<StoredConnection>> ListAsync(
        string? connectorId = null, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var all = Load().Values
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return connectorId is null
                ? all
                : all.Where(c => c.ConnectorId == connectorId).ToList();
        }
        finally { _semaphore.Release(); }
    }

    public async Task<StoredConnection?> GetAsync(string id, CancellationToken ct = default)
    {
        if (!IsValidId(id)) return null;

        await _semaphore.WaitAsync(ct);
        try
        {
            return Load().GetValueOrDefault(id);
        }
        finally { _semaphore.Release(); }
    }

    // ----------------------------------------------------------------- writes

    /// <summary>
    /// Creates or replaces a connection. When <paramref name="secrets"/> is null
    /// the stored values are kept as-is, which is what lets a caller rename a
    /// connection without resubmitting credentials. When it is supplied it
    /// REPLACES the whole set, so "which fields are set" stays unambiguous.
    /// </summary>
    public async Task<StoredConnection> SaveAsync(
        string id,
        string connectorId,
        string name,
        IReadOnlyDictionary<string, string>? secrets,
        CancellationToken ct = default)
    {
        if (!IsValidId(id))
            throw new ArgumentException($"Invalid connection id: '{id}'", nameof(id));

        await _semaphore.WaitAsync(ct);
        try
        {
            var store = Load();
            var now = DateTime.UtcNow.ToString("O");
            store.TryGetValue(id, out var existing);

            var record = new StoredConnection
            {
                Id = id,
                ConnectorId = connectorId,
                Name = name,
                ConfiguredFields = existing?.ConfiguredFields ?? new List<string>(),
                CreatedAt = existing?.CreatedAt ?? now,
                UpdatedAt = existing is null ? null : now,
                LastUsedAt = existing?.LastUsedAt,
            };

            if (secrets is not null)
            {
                // Replacing the set: drop the previous fields first so a removed
                // field does not linger in the secret store.
                foreach (var field in record.ConfiguredFields)
                {
                    _secrets.DeleteSecret(SecretKey(id, field));
                }

                foreach (var (field, value) in secrets)
                {
                    _secrets.StoreSecret(
                        SecretKey(id, field),
                        value,
                        $"Credential '{field}' for connection '{name}' ({connectorId})");
                }

                record.ConfiguredFields = secrets.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            }

            store[id] = record;
            Save(store);
            return record;
        }
        finally { _semaphore.Release(); }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        if (!IsValidId(id)) return false;

        await _semaphore.WaitAsync(ct);
        try
        {
            var store = Load();
            if (!store.TryGetValue(id, out var existing)) return false;

            foreach (var field in existing.ConfiguredFields)
            {
                _secrets.DeleteSecret(SecretKey(id, field));
            }

            store.Remove(id);
            Save(store);
            return true;
        }
        finally { _semaphore.Release(); }
    }

    // ------------------------------------------------------------- resolution

    /// <summary>
    /// Builds the configuration the connector is initialized with. This is the
    /// only path that decrypts values, and it hands them directly to the engine
    /// rather than returning them to an API caller.
    ///
    /// Returns null when the connection does not exist, so the caller can report
    /// a missing credential instead of running the connector uninitialized.
    /// </summary>
    public async Task<ConnectorConfiguration?> BuildConfigurationAsync(
        string connectionId, CancellationToken ct = default)
    {
        if (!IsValidId(connectionId)) return null;

        await _semaphore.WaitAsync(ct);
        try
        {
            var store = Load();
            if (!store.TryGetValue(connectionId, out var record)) return null;

            var config = new ConnectorConfiguration();
            foreach (var field in record.ConfiguredFields)
            {
                var value = _secrets.GetSecret(SecretKey(connectionId, field));
                if (value is not null)
                {
                    config.Credentials[field] = value;
                }
            }

            record.LastUsedAt = DateTime.UtcNow.ToString("O");
            Save(store);

            return config;
        }
        finally { _semaphore.Release(); }
    }

    // ------------------------------------------------------------ persistence

    /// <summary>
    /// Namespaces a secret under its connection. Normalizing rather than
    /// interpolating raw keeps two different connections from ever colliding on
    /// a key, and keeps the key readable in the secrets store.
    /// </summary>
    private static string SecretKey(string connectionId, string field) =>
        $"{SecretKeyPrefix}_{Normalize(connectionId)}_{Normalize(field)}";

    private static string Normalize(string value) =>
        string.Concat(value.Select(c => char.IsAsciiLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_'));

    private Dictionary<string, StoredConnection> Load()
    {
        if (_cache is not null) return _cache;

        if (!File.Exists(_filePath))
        {
            _cache = new Dictionary<string, StoredConnection>(StringComparer.Ordinal);
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var loaded = JsonSerializer.Deserialize<List<StoredConnection>>(json, JsonOptions)
                         ?? new List<StoredConnection>();

            _cache = loaded.ToDictionary(c => c.Id, StringComparer.Ordinal);
            return _cache;
        }
        catch (JsonException ex)
        {
            // Refuse rather than silently starting empty over the top of the
            // file - that would orphan every stored secret.
            throw new InvalidOperationException(
                $"Connection store at {_filePath} is not valid JSON. " +
                "Restore it from a backup or delete it to start over.", ex);
        }
    }

    private void Save(Dictionary<string, StoredConnection> store)
    {
        _cache = store;

        var json = JsonSerializer.Serialize(store.Values.ToList(), JsonOptions);
        var tmpPath = _filePath + ".tmp";

        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, _filePath, overwrite: true);
    }
}
