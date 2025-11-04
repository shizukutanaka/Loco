#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Serverless;

/// <summary>
/// Serverless Architecture Patterns
/// AWS Lambda, Azure Functions, Cold Start Optimization, Provisioned Concurrency
/// </summary>

/// <summary>
/// Lambda function configuration
/// </summary>
public class LambdaFunctionConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("runtime")]
    public string Runtime { get; set; } = "dotnet8"; // nodejs18, python3.11, dotnet8, go1.21

    [JsonPropertyName("handler")]
    public string Handler { get; set; } = string.Empty;

    [JsonPropertyName("timeout")]
    public int TimeoutSeconds { get; set; } = 30;

    [JsonPropertyName("memorySize")]
    public int MemorySizeMB { get; set; } = 128; // 128-10240 MB

    [JsonPropertyName("ephemeralStorage")]
    public int EphemeralStorageMB { get; set; } = 512; // 512-10240 MB

    [JsonPropertyName("environment")]
    public Dictionary<string, string> Environment { get; set; } = new();

    [JsonPropertyName("layers")]
    public List<string> Layers { get; set; } = new(); // ARNs of lambda layers

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public Dictionary<string, string> Tags { get; set; } = new();
}

/// <summary>
/// Cold start optimization configuration
/// </summary>
public class ColdStartOptimization
{
    [JsonPropertyName("strategy")]
    public string Strategy { get; set; } = "Provisioned"; // Provisioned, Warmer, Lightweight

    [JsonPropertyName("provisionedConcurrentExecutions")]
    public int ProvisionedConcurrentExecutions { get; set; } = 5;

    [JsonPropertyName("reservedConcurrentExecutions")]
    public int ReservedConcurrentExecutions { get; set; } = 100;

    [JsonPropertyName("containerImageUri")]
    public string ContainerImageUri { get; set; } = string.Empty;

    [JsonPropertyName("ephemeralPersistenceSize")]
    public int EphemeralPersistenceSizeMB { get; set; } = 512;

    [JsonPropertyName("useZipDeployment")]
    public bool UseZipDeployment { get; set; } = true;

    [JsonPropertyName("enableSnapStart")]
    public bool EnableSnapStart { get; set; } = true; // Java only, reduces cold start by 10x
}

/// <summary>
/// Lambda concurrency model
/// </summary>
public class ConcurrencyConfig
{
    [JsonPropertyName("reservedConcurrentExecutions")]
    public int? ReservedConcurrentExecutions { get; set; }

    [JsonPropertyName("provisionedConcurrentExecutions")]
    public int? ProvisionedConcurrentExecutions { get; set; }

    [JsonPropertyName("maximumEventAge")]
    public int MaximumEventAgeSeconds { get; set; } = 3600;

    [JsonPropertyName("maximumRetryAttempts")]
    public int MaximumRetryAttempts { get; set; } = 2;

    [JsonPropertyName("parallelizationFactor")]
    public int ParallelizationFactor { get; set; } = 1; // For stream sources
}

/// <summary>
/// Event source mapping for Lambda
/// </summary>
public class EventSourceMapping
{
    [JsonPropertyName("eventSourceArn")]
    public string EventSourceArn { get; set; } = string.Empty;

    [JsonPropertyName("eventSourceType")]
    public string EventSourceType { get; set; } = string.Empty; // SQS, SNS, DynamoDB, Kinesis, S3, API Gateway

    [JsonPropertyName("batchSize")]
    public int BatchSize { get; set; } = 10;

    [JsonPropertyName("batchWindow")]
    public int BatchWindowSeconds { get; set; } = 0;

    [JsonPropertyName("startingPosition")]
    public string StartingPosition { get; set; } = "LATEST"; // TRIM_HORIZON, LATEST

    [JsonPropertyName("bisectBatchOnError")]
    public bool BisectBatchOnError { get; set; } = true;

    [JsonPropertyName("maximumRetryAttempts")]
    public int MaximumRetryAttempts { get; set; } = 2;

    [JsonPropertyName("parallelizationFactor")]
    public int ParallelizationFactor { get; set; } = 1;

    [JsonPropertyName("functionResponse")]
    public string FunctionResponse { get; set; } = "ReportBatchItemFailures"; // ReportBatchItemFailures
}

/// <summary>
/// Azure Function configuration
/// </summary>
public class AzureFunctionConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("runtime")]
    public string Runtime { get; set; } = "dotnet-isolated"; // node, python, java, powershell, dotnet

    [JsonPropertyName("runtimeVersion")]
    public string RuntimeVersion { get; set; } = "8.0";

    [JsonPropertyName("trigger")]
    public string Trigger { get; set; } = "HttpTrigger"; // HttpTrigger, TimerTrigger, QueueTrigger, BlobTrigger

    [JsonPropertyName("bindings")]
    public List<FunctionBinding> Bindings { get; set; } = new();

    [JsonPropertyName("hostJson")]
    public Dictionary<string, object> HostJson { get; set; } = new();

    [JsonPropertyName("auth")]
    public string Auth { get; set; } = "Function"; // Anonymous, Function, Admin

    [JsonPropertyName("environment")]
    public Dictionary<string, string> Environment { get; set; } = new();
}

/// <summary>
/// Function binding (input/output)
/// </summary>
public class FunctionBinding
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // HttpTrigger, BlobTrigger, QueueTrigger

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "in"; // in, out, inout

    [JsonPropertyName("dataType")]
    public string DataType { get; set; } = "String";

    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Serverless function deployment package
/// </summary>
public class DeploymentPackage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("functionName")]
    public string FunctionName { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("s3Bucket")]
    public string S3Bucket { get; set; } = string.Empty;

    [JsonPropertyName("s3Key")]
    public string S3Key { get; set; } = string.Empty;

    [JsonPropertyName("packageSize")]
    public long PackageSizeBytes { get; set; }

    [JsonPropertyName("codeHash")]
    public string CodeHash { get; set; } = string.Empty;

    [JsonPropertyName("buildTime")]
    public DateTime BuildTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Serverless architecture manager
/// </summary>
public class ServerlessArchitecture
{
    private readonly Dictionary<string, LambdaFunctionConfig> _lambdaFunctions = new();
    private readonly Dictionary<string, AzureFunctionConfig> _azureFunctions = new();
    private readonly Dictionary<string, ColdStartOptimization> _optimizations = new();
    private readonly Dictionary<string, DeploymentPackage> _deployments = new();
    private readonly ILogger<ServerlessArchitecture> _logger;

    public ServerlessArchitecture(ILogger<ServerlessArchitecture> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register Lambda function
    /// </summary>
    public async Task RegisterLambdaFunctionAsync(LambdaFunctionConfig config)
    {
        _lambdaFunctions[config.Name] = config;

        _logger.LogInformation(
            "Registered Lambda function: {Name} ({Runtime}, {Memory}MB, {Timeout}s)",
            config.Name,
            config.Runtime,
            config.MemorySizeMB,
            config.TimeoutSeconds);
    }

    /// <summary>
    /// Register Azure function
    /// </summary>
    public async Task RegisterAzureFunctionAsync(AzureFunctionConfig config)
    {
        _azureFunctions[config.Name] = config;

        _logger.LogInformation(
            "Registered Azure function: {Name} ({Runtime}, {Trigger})",
            config.Name,
            config.Runtime,
            config.Trigger);
    }

    /// <summary>
    /// Configure cold start optimization
    /// </summary>
    public async Task ConfigureColdStartOptimizationAsync(
        string functionName,
        ColdStartOptimization optimization)
    {
        _optimizations[functionName] = optimization;

        _logger.LogInformation(
            "Configured cold start optimization for {Function}: {Strategy}",
            functionName,
            optimization.Strategy);

        if (optimization.Strategy == "Provisioned")
        {
            _logger.LogInformation(
                "  - Provisioned concurrent executions: {Count}",
                optimization.ProvisionedConcurrentExecutions);
        }
    }

    /// <summary>
    /// Deploy function
    /// </summary>
    public async Task<DeploymentPackage> DeployFunctionAsync(
        string functionName,
        string codeZip,
        string version = "1.0.0")
    {
        var package = new DeploymentPackage
        {
            FunctionName = functionName,
            Version = version,
            PackageSizeBytes = codeZip.Length,
            CodeHash = ComputeHash(codeZip)
        };

        _deployments[functionName] = package;

        _logger.LogInformation(
            "Deployed function {Name} v{Version} ({Size} bytes, hash={Hash})",
            functionName,
            version,
            package.PackageSizeBytes,
            package.CodeHash.Substring(0, 8));

        return package;
    }

    /// <summary>
    /// Estimate cold start time
    /// </summary>
    public TimeSpan EstimateColdStartTime(string functionName)
    {
        if (!_lambdaFunctions.TryGetValue(functionName, out var config))
            return TimeSpan.Zero;

        var baseTime = config.Runtime switch
        {
            "nodejs18" => 50,   // milliseconds
            "python3.11" => 100,
            "dotnet8" => 800,
            "java21" => 1500,
            "go1.21" => 80,
            _ => 100
        };

        var memoryBoost = (double)(config.MemorySizeMB - 128) / 128 * 0.1; // 10% per 128MB
        var adjustedTime = baseTime * (1 - memoryBoost);

        if (_optimizations.TryGetValue(functionName, out var opt))
        {
            if (opt.Strategy == "Provisioned")
                adjustedTime = 10; // Warm start

            if (opt.EnableSnapStart && config.Runtime == "java21")
                adjustedTime /= 10; // SnapStart reduces Java cold start by 10x
        }

        return TimeSpan.FromMilliseconds(Math.Max(10, adjustedTime));
    }

    /// <summary>
    /// Calculate function costs
    /// </summary>
    public Dictionary<string, object> CalculateFunctionCosts(
        string functionName,
        long requestCount,
        double averageDurationMs)
    {
        if (!_lambdaFunctions.TryGetValue(functionName, out var config))
            return new();

        var gbSeconds = (config.MemorySizeMB / 1024.0) * (averageDurationMs / 1000.0) * requestCount;
        var computeCost = gbSeconds * 0.0000166667; // $0.0000166667 per GB-second

        var requestCost = requestCount * 0.0000002; // $0.0000002 per request

        var totalCost = computeCost + requestCost;

        if (_optimizations.TryGetValue(functionName, out var opt) &&
            opt.Strategy == "Provisioned")
        {
            var provisionedHours = 730; // monthly
            var provisionedCost = opt.ProvisionedConcurrentExecutions * 0.015 * provisionedHours;
            totalCost += provisionedCost;
        }

        return new()
        {
            ["gbSeconds"] = Math.Round(gbSeconds, 2),
            ["computeCost"] = Math.Round(computeCost, 4),
            ["requestCost"] = Math.Round(requestCost, 4),
            ["provisionedCost"] = _optimizations.TryGetValue(functionName, out var o) && o.Strategy == "Provisioned"
                ? Math.Round(o.ProvisionedConcurrentExecutions * 0.015 * 730, 4)
                : 0,
            ["totalMonthlyCost"] = Math.Round(totalCost, 4)
        };
    }

    /// <summary>
    /// Get Lambda function
    /// </summary>
    public LambdaFunctionConfig? GetLambdaFunction(string name)
    {
        _lambdaFunctions.TryGetValue(name, out var func);
        return func;
    }

    /// <summary>
    /// Get Azure function
    /// </summary>
    public AzureFunctionConfig? GetAzureFunction(string name)
    {
        _azureFunctions.TryGetValue(name, out var func);
        return func;
    }

    /// <summary>
    /// List all functions
    /// </summary>
    public List<string> ListFunctions()
    {
        var all = new List<string>();
        all.AddRange(_lambdaFunctions.Keys);
        all.AddRange(_azureFunctions.Keys);
        return all;
    }

    /// <summary>
    /// Get architecture stats
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        return new()
        {
            ["lambdaFunctions"] = _lambdaFunctions.Count,
            ["azureFunctions"] = _azureFunctions.Count,
            ["deployments"] = _deployments.Count,
            ["optimizedFunctions"] = _optimizations.Count(x => x.Value.Strategy == "Provisioned"),
            ["totalMemoryMB"] = _lambdaFunctions.Values.Sum(f => f.MemorySizeMB) +
                                _azureFunctions.Count * 256
        };
    }

    private string ComputeHash(string content)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class ServerlessExtensions
{
    public static IServiceCollection AddServerlessPatterns(this IServiceCollection services)
    {
        services.AddSingleton<ServerlessArchitecture>();
        return services;
    }
}
