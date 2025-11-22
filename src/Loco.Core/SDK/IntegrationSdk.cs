// Phase 6: Custom Integration SDK
// Comprehensive SDK for third-party developers to build Loco workflow integrations
// Includes authentication, step execution, state management, and testing utilities

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.SDK;

/// <summary>
/// Integration execution context
/// </summary>
public class IntegrationContext
{
    public string ExecutionId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public Dictionary<string, object> WorkflowInput { get; set; } = new();
    public Dictionary<string, object> StepInput { get; set; } = new();
    public Dictionary<string, object> ExecutionState { get; set; } = new();
    public IIntegrationLogger Logger { get; set; } = new DefaultIntegrationLogger();
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    public object? GetInputValue(string key) => StepInput.TryGetValue(key, out var value) ? value : null;
    public T? GetInputValue<T>(string key) => StepInput.TryGetValue(key, out var value) ? (T?)value : default;
    public void SetOutputValue(string key, object value) => ExecutionState[key] = value;
    public object? GetState(string key) => ExecutionState.TryGetValue(key, out var value) ? value : null;
}

/// <summary>
/// Integration logging interface
/// </summary>
public interface IIntegrationLogger
{
    void LogDebug(string message, params object[] args);
    void LogInfo(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(string message, Exception? ex = null, params object[] args);
}

/// <summary>
/// Default integration logger implementation
/// </summary>
public class DefaultIntegrationLogger : IIntegrationLogger
{
    public void LogDebug(string message, params object[] args) => Console.WriteLine($"[DEBUG] {string.Format(message, args)}");
    public void LogInfo(string message, params object[] args) => Console.WriteLine($"[INFO] {string.Format(message, args)}");
    public void LogWarning(string message, params object[] args) => Console.WriteLine($"[WARN] {string.Format(message, args)}");
    public void LogError(string message, Exception? ex = null, params object[] args)
    {
        Console.WriteLine($"[ERROR] {string.Format(message, args)}");
        if (ex != null) Console.WriteLine($"Exception: {ex}");
    }
}

/// <summary>
/// Step execution result
/// </summary>
public class StepExecutionResult
{
    public bool Success { get; set; } = true;
    public Dictionary<string, object> Output { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }

    public static StepExecutionResult CreateSuccess(Dictionary<string, object>? output = null) =>
        new StepExecutionResult { Success = true, Output = output ?? new Dictionary<string, object>() };

    public static StepExecutionResult CreateFailure(string errorMessage, Exception? ex = null) =>
        new StepExecutionResult { Success = false, ErrorMessage = errorMessage, Exception = ex };
}

/// <summary>
/// Configuration requirement for integration
/// </summary>
public class ConfigurationRequirement
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ConfigurationFieldType FieldType { get; set; }
    public bool IsRequired { get; set; } = true;
    public bool IsSecret { get; set; }
    public object? DefaultValue { get; set; }
    public List<string>? AllowedValues { get; set; }
    public string? ValidationPattern { get; set; }
}

/// <summary>
/// Configuration field type
/// </summary>
public enum ConfigurationFieldType
{
    String = 0,
    Integer = 1,
    Double = 2,
    Boolean = 3,
    SecureString = 4,
    Json = 5,
    Dropdown = 6,
}

/// <summary>
/// Base integration class
/// </summary>
public abstract class IntegrationBase
{
    public virtual string Name { get; } = "Custom Integration";
    public virtual string Description { get; } = string.Empty;
    public virtual string Version { get; } = "1.0.0";
    public virtual string Author { get; } = "Unknown";
    public virtual string IconUrl { get; } = string.Empty;

    /// <summary>
    /// Get configuration requirements
    /// </summary>
    public abstract List<ConfigurationRequirement> GetConfigurationRequirements();

    /// <summary>
    /// Validate configuration
    /// </summary>
    public virtual (bool IsValid, List<string> Errors) ValidateConfiguration(Dictionary<string, object> config)
    {
        var errors = new List<string>();
        var requirements = GetConfigurationRequirements();

        foreach (var req in requirements)
        {
            if (req.IsRequired && (!config.ContainsKey(req.Key) || config[req.Key] == null))
            {
                errors.Add($"Required field '{req.DisplayName}' is missing");
            }
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// Initialize integration with configuration
    /// </summary>
    public abstract Task InitializeAsync(
        Dictionary<string, object> configuration,
        CancellationToken ct = default);

    /// <summary>
    /// Test connection to external service
    /// </summary>
    public abstract Task<(bool IsConnected, string? ErrorMessage)> TestConnectionAsync(
        Dictionary<string, object> configuration,
        CancellationToken ct = default);

    /// <summary>
    /// Execute integration step
    /// </summary>
    public abstract Task<StepExecutionResult> ExecuteAsync(
        IntegrationContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Cleanup resources
    /// </summary>
    public virtual Task CleanupAsync(CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>
    /// Get available actions/operations
    /// </summary>
    public virtual Task<List<string>> GetAvailableActionsAsync(
        Dictionary<string, object> configuration,
        CancellationToken ct = default) => Task.FromResult(new List<string>());

    /// <summary>
    /// Get action schema for auto-completion
    /// </summary>
    public virtual Task<Dictionary<string, object>?> GetActionSchemaAsync(
        string action,
        Dictionary<string, object> configuration,
        CancellationToken ct = default) => Task.FromResult<Dictionary<string, object>?>(null);
}

/// <summary>
/// HTTP-based integration helper
/// </summary>
public abstract class HttpIntegrationBase : IntegrationBase
{
    protected HttpClient? _httpClient;
    protected Dictionary<string, string>? _baseHeaders;

    protected void SetupHttpClient(Dictionary<string, string>? defaultHeaders = null)
    {
        _httpClient = new HttpClient();
        _baseHeaders = defaultHeaders ?? new Dictionary<string, string>();
    }

    protected async Task<T?> GetAsync<T>(string url, CancellationToken ct = default)
    {
        if (_httpClient == null) throw new InvalidOperationException("HttpClient not initialized");

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            // In production, use proper JSON deserialization
            return default;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"HTTP GET failed: {url}", ex);
        }
    }

    protected async Task<T?> PostAsync<T>(string url, object? data, CancellationToken ct = default)
    {
        if (_httpClient == null) throw new InvalidOperationException("HttpClient not initialized");

        try
        {
            var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(data));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            var response = await _httpClient.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            // In production, use proper JSON deserialization
            return default;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"HTTP POST failed: {url}", ex);
        }
    }

    public override Task CleanupAsync(CancellationToken ct = default)
    {
        _httpClient?.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Database integration helper
/// </summary>
public abstract class DatabaseIntegrationBase : IntegrationBase
{
    protected string? _connectionString;

    protected void SetConnectionString(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected string? GetConnectionString() => _connectionString;
}

/// <summary>
/// Event-based integration helper
/// </summary>
public interface IEventIntegration
{
    Task SubscribeAsync(string eventName, Func<Dictionary<string, object>, Task> handler, CancellationToken ct = default);
    Task UnsubscribeAsync(string eventName, CancellationToken ct = default);
    Task PublishAsync(string eventName, Dictionary<string, object> data, CancellationToken ct = default);
}

/// <summary>
/// Integration metadata
/// </summary>
public class IntegrationMetadata
{
    public string IntegrationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<ConfigurationRequirement> ConfigurationRequirements { get; set; } = new();
    public List<string> SupportedActions { get; set; } = new();
    public List<string> Dependencies { get; set; } = new();
    public Dictionary<string, object> Capabilities { get; set; } = new();
}

/// <summary>
/// Integration factory for instantiation
/// </summary>
public interface IIntegrationFactory
{
    T CreateIntegration<T>() where T : IntegrationBase;
    IntegrationBase CreateIntegration(string typeName);
    IntegrationMetadata GetMetadata<T>() where T : IntegrationBase;
}

/// <summary>
/// Testing utilities for integration testing
/// </summary>
public class IntegrationTestHelper
{
    public static IntegrationContext CreateTestContext(
        string executionId = "test-exec-1",
        string workflowId = "test-workflow",
        string stepId = "test-step",
        Dictionary<string, object>? input = null)
    {
        return new IntegrationContext
        {
            ExecutionId = executionId,
            WorkflowId = workflowId,
            StepId = stepId,
            StepInput = input ?? new Dictionary<string, object>(),
            Logger = new DefaultIntegrationLogger(),
            CancellationToken = CancellationToken.None,
        };
    }

    public static async Task<StepExecutionResult> ExecuteIntegrationAsync<T>(
        T integration,
        IntegrationContext context,
        Dictionary<string, object> config,
        CancellationToken ct = default) where T : IntegrationBase
    {
        var validation = integration.ValidateConfiguration(config);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Configuration invalid: {string.Join(", ", validation.Errors)}");
        }

        await integration.InitializeAsync(config, ct);
        try
        {
            return await integration.ExecuteAsync(context, ct);
        }
        finally
        {
            await integration.CleanupAsync(ct);
        }
    }
}

/// <summary>
/// Example integration implementations
/// </summary>
public static class IntegrationExamples
{
    /// <summary>
    /// Simple HTTP API integration example
    /// </summary>
    public class HttpApiIntegration : HttpIntegrationBase
    {
        private string? _apiKey;
        private string? _baseUrl;

        public override string Name => "HTTP API Integration";
        public override string Description => "Call external HTTP APIs from workflows";
        public override string Version => "1.0.0";

        public override List<ConfigurationRequirement> GetConfigurationRequirements()
        {
            return new List<ConfigurationRequirement>
            {
                new ConfigurationRequirement
                {
                    Key = "base_url",
                    DisplayName = "Base URL",
                    Description = "Base URL of the HTTP API",
                    FieldType = ConfigurationFieldType.String,
                    IsRequired = true,
                    ValidationPattern = @"^https?://",
                },
                new ConfigurationRequirement
                {
                    Key = "api_key",
                    DisplayName = "API Key",
                    Description = "Authentication API key",
                    FieldType = ConfigurationFieldType.SecureString,
                    IsRequired = false,
                },
                new ConfigurationRequirement
                {
                    Key = "timeout_seconds",
                    DisplayName = "Timeout (seconds)",
                    FieldType = ConfigurationFieldType.Integer,
                    IsRequired = false,
                    DefaultValue = 30,
                },
            };
        }

        public override async Task InitializeAsync(
            Dictionary<string, object> configuration,
            CancellationToken ct = default)
        {
            _baseUrl = configuration["base_url"]?.ToString();
            _apiKey = configuration.ContainsKey("api_key") ? configuration["api_key"]?.ToString() : null;

            SetupHttpClient(new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {_apiKey}" },
            });

            await Task.CompletedTask;
        }

        public override async Task<(bool IsConnected, string? ErrorMessage)> TestConnectionAsync(
            Dictionary<string, object> configuration,
            CancellationToken ct = default)
        {
            try
            {
                var baseUrl = configuration["base_url"]?.ToString();
                using var client = new HttpClient();
                var response = await client.GetAsync($"{baseUrl}/health", ct);
                return (response.IsSuccessStatusCode, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public override async Task<StepExecutionResult> ExecuteAsync(
            IntegrationContext context,
            CancellationToken ct = default)
        {
            try
            {
                var method = context.GetInputValue<string>("method") ?? "GET";
                var endpoint = context.GetInputValue<string>("endpoint");
                var data = context.GetInputValue<Dictionary<string, object>>("data");

                if (string.IsNullOrEmpty(endpoint))
                {
                    return StepExecutionResult.CreateFailure("Endpoint is required");
                }

                var url = $"{_baseUrl}/{endpoint.TrimStart('/')}";

                // Simulated HTTP call
                context.Logger.LogInfo($"Calling {method} {url}");

                return StepExecutionResult.CreateSuccess(new Dictionary<string, object>
                {
                    { "statusCode", 200 },
                    { "response", new { message = "Success" } },
                });
            }
            catch (Exception ex)
            {
                return StepExecutionResult.CreateFailure(ex.Message, ex);
            }
            finally
            {
                await Task.CompletedTask;
            }
        }

        public override async Task<List<string>> GetAvailableActionsAsync(
            Dictionary<string, object> configuration,
            CancellationToken ct = default)
        {
            return await Task.FromResult(new List<string>
            {
                "GET",
                "POST",
                "PUT",
                "DELETE",
                "PATCH",
            });
        }
    }

    /// <summary>
    /// Message queue integration example
    /// </summary>
    public class MessageQueueIntegration : IntegrationBase
    {
        public override string Name => "Message Queue Integration";
        public override string Description => "Publish and consume messages from queues";
        public override string Version => "1.0.0";

        public override List<ConfigurationRequirement> GetConfigurationRequirements()
        {
            return new List<ConfigurationRequirement>
            {
                new ConfigurationRequirement
                {
                    Key = "queue_type",
                    DisplayName = "Queue Type",
                    FieldType = ConfigurationFieldType.Dropdown,
                    IsRequired = true,
                    AllowedValues = new List<string> { "RabbitMQ", "Azure Service Bus", "AWS SQS" },
                },
                new ConfigurationRequirement
                {
                    Key = "connection_string",
                    DisplayName = "Connection String",
                    FieldType = ConfigurationFieldType.SecureString,
                    IsRequired = true,
                },
            };
        }

        public override async Task InitializeAsync(
            Dictionary<string, object> configuration,
            CancellationToken ct = default)
        {
            var queueType = configuration["queue_type"]?.ToString();
            var connectionString = configuration["connection_string"]?.ToString();

            // Initialize queue client based on type
            await Task.CompletedTask;
        }

        public override async Task<(bool IsConnected, string? ErrorMessage)> TestConnectionAsync(
            Dictionary<string, object> configuration,
            CancellationToken ct = default)
        {
            // Test queue connection
            return await Task.FromResult((true, null as string));
        }

        public override async Task<StepExecutionResult> ExecuteAsync(
            IntegrationContext context,
            CancellationToken ct = default)
        {
            var action = context.GetInputValue<string>("action");
            var queueName = context.GetInputValue<string>("queue_name");
            var message = context.GetInputValue<object>("message");

            if (action == "publish")
            {
                context.Logger.LogInfo($"Publishing message to {queueName}");
                return StepExecutionResult.CreateSuccess(new Dictionary<string, object>
                {
                    { "messageId", Guid.NewGuid().ToString() },
                });
            }

            return StepExecutionResult.CreateFailure("Unknown action");
        }
    }
}
