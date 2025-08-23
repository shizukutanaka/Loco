using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Serverless.Core;

/// <summary>
/// Represents a serverless function deployment configuration
/// </summary>
public class ServerlessFunctionConfig
{
    public string FunctionName { get; set; } = string.Empty;
    public string Runtime { get; set; } = "dotnet8";
    public string Handler { get; set; } = string.Empty;
    public int MemorySize { get; set; } = 512;
    public int Timeout { get; set; } = 30;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Role { get; set; }
    public TriggerConfig? Trigger { get; set; }
    public RetryPolicy? RetryPolicy { get; set; }
    public DeadLetterConfig? DeadLetterQueue { get; set; }
}

/// <summary>
/// Trigger configuration for serverless functions
/// </summary>
public class TriggerConfig
{
    public TriggerType Type { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}

public enum TriggerType
{
    Http,
    Timer,
    Queue,
    Storage,
    EventGrid,
    ServiceBus,
    CosmosDB,
    EventHub
}

/// <summary>
/// Retry policy configuration
/// </summary>
public class RetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;
    public bool ExponentialBackoff { get; set; } = true;
}

/// <summary>
/// Dead letter queue configuration
/// </summary>
public class DeadLetterConfig
{
    public string QueueName { get; set; } = string.Empty;
    public int MaxReceiveCount { get; set; } = 3;
}

/// <summary>
/// Function execution context
/// </summary>
public class FunctionContext
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public string FunctionName { get; set; } = string.Empty;
    public string FunctionVersion { get; set; } = string.Empty;
    public int MemoryLimitMB { get; set; }
    public int RemainingTimeMs { get; set; }
    public ILogger Logger { get; set; } = null!;
    public Dictionary<string, string> Environment { get; set; } = new();
}

/// <summary>
/// Function execution result
/// </summary>
public class FunctionResult
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public TimeSpan ExecutionTime { get; set; }
}

/// <summary>
/// Interface for serverless function providers
/// </summary>
public interface IServerlessProvider
{
    string ProviderName { get; }
    Task<string> DeployFunctionAsync(ServerlessFunctionConfig config, byte[] codePackage);
    Task<bool> DeleteFunctionAsync(string functionName);
    Task<FunctionResult> InvokeFunctionAsync(string functionName, object payload);
    Task<ServerlessFunctionConfig?> GetFunctionConfigAsync(string functionName);
    Task<IEnumerable<string>> ListFunctionsAsync();
    Task<bool> UpdateFunctionCodeAsync(string functionName, byte[] codePackage);
    Task<bool> UpdateFunctionConfigAsync(string functionName, ServerlessFunctionConfig config);
    Task<Dictionary<string, object>> GetFunctionMetricsAsync(string functionName, DateTime startTime, DateTime endTime);
}

/// <summary>
/// Base class for serverless functions
/// </summary>
public abstract class ServerlessFunction
{
    protected ILogger Logger { get; }
    protected FunctionContext Context { get; }

    protected ServerlessFunction(FunctionContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Logger = context.Logger;
    }

    /// <summary>
    /// Execute the function logic
    /// </summary>
    public abstract Task<FunctionResult> ExecuteAsync(object input);

    /// <summary>
    /// Validate input before execution
    /// </summary>
    protected virtual bool ValidateInput(object input)
    {
        return input != null;
    }

    /// <summary>
    /// Handle function errors
    /// </summary>
    protected virtual FunctionResult HandleError(Exception ex)
    {
        Logger.LogError(ex, "Function execution failed");
        return new FunctionResult
        {
            Success = false,
            Error = ex.Message,
            Metadata = new Dictionary<string, string>
            {
                ["ErrorType"] = ex.GetType().Name,
                ["StackTrace"] = ex.StackTrace ?? string.Empty
            }
        };
    }

    /// <summary>
    /// Log metrics after execution
    /// </summary>
    protected virtual void LogMetrics(FunctionResult result)
    {
        Logger.LogInformation("Function execution completed. Success: {Success}, ExecutionTime: {ExecutionTime}ms",
            result.Success, result.ExecutionTime.TotalMilliseconds);
    }
}

/// <summary>
/// Factory for creating serverless providers
/// </summary>
public class ServerlessProviderFactory
{
    private readonly Dictionary<string, Func<IServerlessProvider>> _providers = new();
    private readonly ILogger<ServerlessProviderFactory> _logger;

    public ServerlessProviderFactory(ILogger<ServerlessProviderFactory> logger)
    {
        _logger = logger;
    }

    public void RegisterProvider(string name, Func<IServerlessProvider> factory)
    {
        _providers[name.ToLowerInvariant()] = factory;
        _logger.LogInformation("Registered serverless provider: {Provider}", name);
    }

    public IServerlessProvider? GetProvider(string name)
    {
        if (_providers.TryGetValue(name.ToLowerInvariant(), out var factory))
        {
            return factory();
        }
        
        _logger.LogWarning("Serverless provider not found: {Provider}", name);
        return null;
    }

    public IEnumerable<string> GetAvailableProviders()
    {
        return _providers.Keys;
    }
}

/// <summary>
/// Serverless function manager for orchestrating deployments
/// </summary>
public class ServerlessFunctionManager
{
    private readonly ServerlessProviderFactory _providerFactory;
    private readonly ILogger<ServerlessFunctionManager> _logger;
    private readonly Dictionary<string, IServerlessProvider> _activeProviders = new();

    public ServerlessFunctionManager(
        ServerlessProviderFactory providerFactory,
        ILogger<ServerlessFunctionManager> logger)
    {
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async Task<string> DeployFunctionAsync(string provider, ServerlessFunctionConfig config, byte[] codePackage)
    {
        var serverlessProvider = GetOrCreateProvider(provider);
        if (serverlessProvider == null)
        {
            throw new InvalidOperationException($"Provider '{provider}' not available");
        }

        _logger.LogInformation("Deploying function {FunctionName} to {Provider}", config.FunctionName, provider);
        
        try
        {
            var functionId = await serverlessProvider.DeployFunctionAsync(config, codePackage);
            _logger.LogInformation("Successfully deployed function {FunctionName} with ID {FunctionId}", 
                config.FunctionName, functionId);
            return functionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy function {FunctionName} to {Provider}", 
                config.FunctionName, provider);
            throw;
        }
    }

    public async Task<FunctionResult> InvokeFunctionAsync(string provider, string functionName, object payload)
    {
        var serverlessProvider = GetOrCreateProvider(provider);
        if (serverlessProvider == null)
        {
            return new FunctionResult
            {
                Success = false,
                Error = $"Provider '{provider}' not available"
            };
        }

        _logger.LogInformation("Invoking function {FunctionName} on {Provider}", functionName, provider);
        
        var startTime = DateTime.UtcNow;
        try
        {
            var result = await serverlessProvider.InvokeFunctionAsync(functionName, payload);
            result.ExecutionTime = DateTime.UtcNow - startTime;
            
            _logger.LogInformation("Function {FunctionName} executed in {ExecutionTime}ms", 
                functionName, result.ExecutionTime.TotalMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke function {FunctionName} on {Provider}", 
                functionName, provider);
            
            return new FunctionResult
            {
                Success = false,
                Error = ex.Message,
                ExecutionTime = DateTime.UtcNow - startTime
            };
        }
    }

    public async Task<bool> DeleteFunctionAsync(string provider, string functionName)
    {
        var serverlessProvider = GetOrCreateProvider(provider);
        if (serverlessProvider == null)
        {
            return false;
        }

        _logger.LogInformation("Deleting function {FunctionName} from {Provider}", functionName, provider);
        
        try
        {
            var result = await serverlessProvider.DeleteFunctionAsync(functionName);
            if (result)
            {
                _logger.LogInformation("Successfully deleted function {FunctionName}", functionName);
            }
            else
            {
                _logger.LogWarning("Failed to delete function {FunctionName}", functionName);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting function {FunctionName} from {Provider}", 
                functionName, provider);
            return false;
        }
    }

    public async Task<Dictionary<string, IEnumerable<string>>> ListAllFunctionsAsync()
    {
        var result = new Dictionary<string, IEnumerable<string>>();
        
        foreach (var providerName in _providerFactory.GetAvailableProviders())
        {
            var provider = GetOrCreateProvider(providerName);
            if (provider != null)
            {
                try
                {
                    var functions = await provider.ListFunctionsAsync();
                    result[providerName] = functions;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error listing functions from {Provider}", providerName);
                    result[providerName] = Array.Empty<string>();
                }
            }
        }
        
        return result;
    }

    private IServerlessProvider? GetOrCreateProvider(string provider)
    {
        if (_activeProviders.TryGetValue(provider, out var existing))
        {
            return existing;
        }

        var newProvider = _providerFactory.GetProvider(provider);
        if (newProvider != null)
        {
            _activeProviders[provider] = newProvider;
        }
        
        return newProvider;
    }
}
