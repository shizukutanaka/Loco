// John Carmack: "Simple interfaces enable complex systems"
// Rob Pike: "Data dominates. If you've chosen the right data structures, the algorithms will flow"

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loco.Core.Integrations.Core;

/// <summary>
/// Base interface for all integration connectors
/// Defines the contract for HTTP APIs, databases, messaging, storage, etc.
/// </summary>
public interface IConnector
{
    /// <summary>Unique identifier (e.g., "http", "postgresql", "slack")</summary>
    string Id { get; }

    /// <summary>Display name (e.g., "HTTP/REST API", "PostgreSQL", "Slack")</summary>
    string Name { get; }

    /// <summary>Description of the connector</summary>
    string Description { get; }

    /// <summary>Version (SemVer)</summary>
    string Version { get; }

    /// <summary>Category (Database, API, Communication, Storage, etc.)</summary>
    ConnectorCategory Category { get; }

    /// <summary>Icon URL for UI display</summary>
    string IconUrl { get; }

    /// <summary>Connector capabilities</summary>
    ConnectorCapabilities Capabilities { get; }

    /// <summary>Available actions this connector can perform</summary>
    IReadOnlyList<ConnectorAction> Actions { get; }

    /// <summary>Available triggers for event-driven workflows</summary>
    IReadOnlyList<ConnectorTrigger> Triggers { get; }

    /// <summary>Authentication configuration</summary>
    AuthenticationConfig AuthConfig { get; }

    /// <summary>Configuration parameters for this connector</summary>
    IReadOnlyList<ConfigParameter> ConfigParameters { get; }

    /// <summary>Test connection with given configuration</summary>
    Task<ConnectionTestResult> TestConnectionAsync(
        ConnectorConfiguration config,
        CancellationToken ct = default);

    /// <summary>Initialize the connector with configuration</summary>
    Task InitializeAsync(
        ConnectorConfiguration config,
        CancellationToken ct = default);

    /// <summary>Execute an action</summary>
    Task<ActionResult> ExecuteAsync(
        string actionName,
        ActionParameters parameters,
        ExecutionContext context,
        CancellationToken ct = default);

    /// <summary>Register a trigger for event notifications</summary>
    Task<TriggerRegistration> RegisterTriggerAsync(
        string triggerName,
        TriggerConfiguration config,
        CancellationToken ct = default);

    /// <summary>Unregister a trigger</summary>
    Task UnregisterTriggerAsync(
        string registrationId,
        CancellationToken ct = default);

    /// <summary>Cleanup resources</summary>
    Task CleanupAsync(CancellationToken ct = default);
}

/// <summary>
/// Connector category
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConnectorCategory
{
    Api,
    Database,
    Communication,
    Storage,
    Productivity,
    Payment,
    DevOps,
    Analytics,
    Security,
    Other
}

/// <summary>
/// Connector capabilities
/// </summary>
public sealed class ConnectorCapabilities
{
    public bool SupportsActions { get; init; } = true;
    public bool SupportsTriggers { get; init; } = false;
    public bool SupportsWebhooks { get; init; } = false;
    public bool SupportsPolling { get; init; } = false;
    public bool SupportsBatching { get; init; } = false;
    public bool SupportsStreaming { get; init; } = false;
    public bool SupportsTransactions { get; init; } = false;
    public int MaxConcurrentConnections { get; init; } = 10;
    public int RateLimitPerMinute { get; init; } = 60;
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public static ConnectorCapabilities Default => new();

    public static ConnectorCapabilities ForApi() => new()
    {
        SupportsActions = true,
        SupportsTriggers = true,
        SupportsWebhooks = true,
        RateLimitPerMinute = 100
    };

    public static ConnectorCapabilities ForDatabase() => new()
    {
        SupportsActions = true,
        SupportsTransactions = true,
        SupportsBatching = true,
        MaxConcurrentConnections = 20,
        DefaultTimeout = TimeSpan.FromSeconds(60)
    };

    public static ConnectorCapabilities ForMessaging() => new()
    {
        SupportsActions = true,
        SupportsTriggers = true,
        SupportsWebhooks = true,
        SupportsStreaming = true
    };
}

/// <summary>
/// Connector action definition
/// </summary>
public sealed class ConnectorAction
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public IReadOnlyList<ActionParameter> Parameters { get; init; } = Array.Empty<ActionParameter>();
    public ActionOutputSchema? OutputSchema { get; init; }
    public bool RequiresConfirmation { get; init; } = false;
    public RetryConfig? RetryConfig { get; init; }
}

/// <summary>
/// Action parameter definition
/// </summary>
public sealed class ActionParameter
{
    public required string Name { get; init; }
    public required ParameterType Type { get; init; }
    public string Description { get; init; } = "";
    public bool Required { get; init; } = false;
    public object? DefaultValue { get; init; }
    public IReadOnlyList<SelectOption>? Options { get; init; }
    public ParameterValidation? Validation { get; init; }
}

/// <summary>
/// Parameter types
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ParameterType
{
    String,
    Number,
    Boolean,
    Select,
    MultiSelect,
    Json,
    Code,
    Password,
    File,
    Date,
    DateTime
}

/// <summary>
/// Select option for dropdowns
/// </summary>
public sealed class SelectOption
{
    public required string Label { get; init; }
    public required object Value { get; init; }
}

/// <summary>
/// Parameter validation rules
/// </summary>
public sealed class ParameterValidation
{
    public string? Pattern { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public string? CustomValidator { get; init; }
}

/// <summary>
/// Action output schema
/// </summary>
public sealed class ActionOutputSchema
{
    public required ParameterType Type { get; init; }
    public string? Description { get; init; }
    public Dictionary<string, ActionOutputSchema>? Properties { get; init; }
}

/// <summary>
/// Retry configuration
/// </summary>
public sealed class RetryConfig
{
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);
    public bool UseExponentialBackoff { get; init; } = true;
    public double BackoffMultiplier { get; init; } = 2.0;
    public int[] RetryableStatusCodes { get; init; } = [408, 429, 500, 502, 503, 504];
}

/// <summary>
/// Connector trigger definition
/// </summary>
public sealed class ConnectorTrigger
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public TriggerType Type { get; init; } = TriggerType.Webhook;
    public IReadOnlyList<ActionParameter> ConfigParameters { get; init; } = Array.Empty<ActionParameter>();
    public ActionOutputSchema? OutputSchema { get; init; }
}

/// <summary>
/// Trigger type
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TriggerType
{
    Webhook,
    Polling,
    Stream
}

/// <summary>
/// Authentication configuration
/// </summary>
public sealed class AuthenticationConfig
{
    public required AuthenticationType Type { get; init; }
    public IReadOnlyList<CredentialField> RequiredCredentials { get; init; } = Array.Empty<CredentialField>();

    // OAuth2 specific
    public string? AuthorizationUrl { get; init; }
    public string? TokenUrl { get; init; }
    public string[]? Scopes { get; init; }
    public bool SupportsRefreshToken { get; init; } = true;

    public static AuthenticationConfig None => new() { Type = AuthenticationType.None };

    public static AuthenticationConfig ApiKey(string headerName = "X-Api-Key") => new()
    {
        Type = AuthenticationType.ApiKey,
        RequiredCredentials = [new() { Name = "apiKey", Label = "API Key", Type = ParameterType.Password }]
    };

    public static AuthenticationConfig Basic() => new()
    {
        Type = AuthenticationType.Basic,
        RequiredCredentials =
        [
            new() { Name = "username", Label = "Username", Type = ParameterType.String },
            new() { Name = "password", Label = "Password", Type = ParameterType.Password }
        ]
    };

    public static AuthenticationConfig OAuth2(string authUrl, string tokenUrl, params string[] scopes) => new()
    {
        Type = AuthenticationType.OAuth2,
        AuthorizationUrl = authUrl,
        TokenUrl = tokenUrl,
        Scopes = scopes,
        RequiredCredentials =
        [
            new() { Name = "clientId", Label = "Client ID", Type = ParameterType.String },
            new() { Name = "clientSecret", Label = "Client Secret", Type = ParameterType.Password }
        ]
    };

    public static AuthenticationConfig ConnectionString() => new()
    {
        Type = AuthenticationType.ConnectionString,
        RequiredCredentials = [new() { Name = "connectionString", Label = "Connection String", Type = ParameterType.Password }]
    };
}

/// <summary>
/// Authentication type
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthenticationType
{
    None,
    ApiKey,
    Basic,
    Bearer,
    OAuth2,
    ConnectionString,
    Custom
}

/// <summary>
/// Credential field definition
/// </summary>
public sealed class CredentialField
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public ParameterType Type { get; init; } = ParameterType.String;
    public string? Description { get; init; }
    public bool Required { get; init; } = true;
}

/// <summary>
/// Configuration parameter
/// </summary>
public sealed class ConfigParameter
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public ParameterType Type { get; init; } = ParameterType.String;
    public string? Description { get; init; }
    public bool Required { get; init; } = false;
    public object? DefaultValue { get; init; }
    public IReadOnlyList<SelectOption>? Options { get; init; }
}

/// <summary>
/// Connector configuration
/// </summary>
public sealed class ConnectorConfiguration
{
    public Dictionary<string, object?> Credentials { get; init; } = new();
    public Dictionary<string, object?> Settings { get; init; } = new();

    public T? GetCredential<T>(string name) =>
        Credentials.TryGetValue(name, out var value) ? (T?)value : default;

    public T? GetSetting<T>(string name) =>
        Settings.TryGetValue(name, out var value) ? (T?)value : default;

    public string? GetCredentialString(string name) => GetCredential<string>(name);
    public string? GetSettingString(string name) => GetSetting<string>(name);
}

/// <summary>
/// Connection test result
/// </summary>
public sealed class ConnectionTestResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public TimeSpan ResponseTime { get; init; }
    public Dictionary<string, object>? Details { get; init; }

    public static ConnectionTestResult Ok(string message = "Connection successful", TimeSpan? responseTime = null) => new()
    {
        Success = true,
        Message = message,
        ResponseTime = responseTime ?? TimeSpan.Zero
    };

    public static ConnectionTestResult Fail(string message, Exception? ex = null) => new()
    {
        Success = false,
        Message = ex != null ? $"{message}: {ex.Message}" : message
    };
}

/// <summary>
/// Action parameters container
/// </summary>
public sealed class ActionParameters
{
    private readonly Dictionary<string, object?> _values;

    public ActionParameters() => _values = new(StringComparer.OrdinalIgnoreCase);
    public ActionParameters(Dictionary<string, object?> values) => _values = new(values, StringComparer.OrdinalIgnoreCase);
    public ActionParameters(object anonymousObject)
    {
        _values = new(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in anonymousObject.GetType().GetProperties())
        {
            _values[prop.Name] = prop.GetValue(anonymousObject);
        }
    }

    public T? Get<T>(string name) =>
        _values.TryGetValue(name, out var value) ? ConvertValue<T>(value) : default;

    public string? GetString(string name) => Get<string>(name);
    public int GetInt(string name, int defaultValue = 0) => Get<int?>(name) ?? defaultValue;
    public bool GetBool(string name, bool defaultValue = false) => Get<bool?>(name) ?? defaultValue;

    public void Set(string name, object? value) => _values[name] = value;

    public bool Contains(string name) => _values.ContainsKey(name);

    /// <summary>
    /// Alias for <see cref="Contains"/>. Added so connector code that used
    /// the conventional `Has` name compiles; both mean "does this parameter exist".
    /// </summary>
    public bool Has(string name) => Contains(name);

    public Dictionary<string, object?> ToDictionary() => new(_values);

    private static T? ConvertValue<T>(object? value)
    {
        if (value == null) return default;
        if (value is T typed) return typed;
        if (value is JsonElement json) return json.Deserialize<T>();

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }
}

/// <summary>
/// Execution context
/// </summary>
public sealed class ExecutionContext
{
    public required string WorkflowId { get; init; }
    public required string ExecutionId { get; init; }
    public required string NodeId { get; init; }
    public string? CorrelationId { get; init; }
    public Dictionary<string, object?> Variables { get; init; } = new();
    public Dictionary<string, object?> PreviousOutputs { get; init; } = new();
    public CancellationToken CancellationToken { get; init; }

    public T? GetVariable<T>(string name) =>
        Variables.TryGetValue(name, out var value) ? (T?)value : default;

    public T? GetPreviousOutput<T>(string nodeId, string key)
    {
        if (PreviousOutputs.TryGetValue(nodeId, out var output) && output is Dictionary<string, object?> dict)
        {
            return dict.TryGetValue(key, out var value) ? (T?)value : default;
        }
        return default;
    }
}

/// <summary>
/// Action result (record for immutability and with-expressions)
/// </summary>
public sealed record ActionResult
{
    public bool Success { get; init; }
    public object? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
    public TimeSpan Duration { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }

    public static ActionResult Ok(object? data = null, TimeSpan? duration = null) => new()
    {
        Success = true,
        Data = data,
        Duration = duration ?? TimeSpan.Zero
    };

    public static ActionResult Fail(string message, string? errorCode = null) => new()
    {
        Success = false,
        ErrorMessage = message,
        ErrorCode = errorCode
    };

    public T? GetData<T>() => Data is T typed ? typed : default;
}

/// <summary>
/// Trigger configuration
/// </summary>
public sealed class TriggerConfiguration
{
    public required string WebhookUrl { get; init; }
    public Dictionary<string, object?> Parameters { get; init; } = new();
    public TimeSpan? PollingInterval { get; init; }
}

/// <summary>
/// Trigger registration result
/// </summary>
public sealed class TriggerRegistration
{
    public required string RegistrationId { get; init; }
    public required string TriggerId { get; init; }
    public string? WebhookEndpoint { get; init; }
    public DateTime RegisteredAt { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }
}
