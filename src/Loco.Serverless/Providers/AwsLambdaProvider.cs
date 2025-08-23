using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Loco.Serverless.Core;

namespace Loco.Serverless.Providers;

/// <summary>
/// AWS Lambda serverless provider implementation
/// </summary>
public class AwsLambdaProvider : IServerlessProvider
{
    private readonly AmazonLambdaClient _lambdaClient;
    private readonly ILogger<AwsLambdaProvider> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _accountId;
    private readonly RegionEndpoint _region;

    public string ProviderName => "AWS Lambda";

    public AwsLambdaProvider(IConfiguration configuration, ILogger<AwsLambdaProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var accessKey = configuration["AWS:AccessKey"] ?? Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var secretKey = configuration["AWS:SecretKey"] ?? Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        var regionName = configuration["AWS:Region"] ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ?? "us-east-1";
        
        _accountId = configuration["AWS:AccountId"] ?? Environment.GetEnvironmentVariable("AWS_ACCOUNT_ID") ?? string.Empty;
        _region = RegionEndpoint.GetBySystemName(regionName);

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        _lambdaClient = new AmazonLambdaClient(credentials, _region);

        _logger.LogInformation("AWS Lambda provider initialized for region {Region}", regionName);
    }

    public async Task<string> DeployFunctionAsync(ServerlessFunctionConfig config, byte[] codePackage)
    {
        try
        {
            // Check if function already exists
            var existingFunction = await GetFunctionAsync(config.FunctionName);
            
            if (existingFunction != null)
            {
                // Update existing function
                await UpdateFunctionCodeAsync(config.FunctionName, codePackage);
                await UpdateFunctionConfigAsync(config.FunctionName, config);
                _logger.LogInformation("Updated existing Lambda function {FunctionName}", config.FunctionName);
                return existingFunction.FunctionArn;
            }

            // Create new function
            var createRequest = new CreateFunctionRequest
            {
                FunctionName = config.FunctionName,
                Runtime = MapRuntime(config.Runtime),
                Role = config.Role ?? GetDefaultRole(),
                Handler = config.Handler,
                Code = new FunctionCode { ZipFile = new MemoryStream(codePackage) },
                Description = config.Description,
                Timeout = config.Timeout,
                MemorySize = config.MemorySize,
                Environment = new Amazon.Lambda.Model.Environment
                {
                    Variables = config.EnvironmentVariables
                },
                Tags = config.Tags.ToDictionary(t => t, t => t)
            };

            // Add dead letter queue if configured
            if (config.DeadLetterQueue != null)
            {
                createRequest.DeadLetterConfig = new DeadLetterConfig
                {
                    TargetArn = GetQueueArn(config.DeadLetterQueue.QueueName)
                };
            }

            var response = await _lambdaClient.CreateFunctionAsync(createRequest);
            
            // Configure trigger if specified
            if (config.Trigger != null)
            {
                await ConfigureTriggerAsync(config.FunctionName, config.Trigger);
            }

            _logger.LogInformation("Created Lambda function {FunctionName} with ARN {FunctionArn}", 
                config.FunctionName, response.FunctionArn);
            
            return response.FunctionArn;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy Lambda function {FunctionName}", config.FunctionName);
            throw;
        }
    }

    public async Task<bool> DeleteFunctionAsync(string functionName)
    {
        try
        {
            var deleteRequest = new DeleteFunctionRequest
            {
                FunctionName = functionName
            };

            await _lambdaClient.DeleteFunctionAsync(deleteRequest);
            _logger.LogInformation("Deleted Lambda function {FunctionName}", functionName);
            return true;
        }
        catch (ResourceNotFoundException)
        {
            _logger.LogWarning("Lambda function {FunctionName} not found", functionName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Lambda function {FunctionName}", functionName);
            return false;
        }
    }

    public async Task<FunctionResult> InvokeFunctionAsync(string functionName, object payload)
    {
        try
        {
            var payloadJson = JsonSerializer.Serialize(payload);
            var invokeRequest = new InvokeRequest
            {
                FunctionName = functionName,
                InvocationType = InvocationType.RequestResponse,
                Payload = payloadJson
            };

            var response = await _lambdaClient.InvokeAsync(invokeRequest);
            
            var responsePayload = Encoding.UTF8.GetString(response.Payload.ToArray());
            
            if (response.FunctionError != null)
            {
                return new FunctionResult
                {
                    Success = false,
                    Error = response.FunctionError,
                    Data = responsePayload,
                    Metadata = new Dictionary<string, string>
                    {
                        ["StatusCode"] = response.StatusCode.ToString(),
                        ["ExecutedVersion"] = response.ExecutedVersion
                    }
                };
            }

            return new FunctionResult
            {
                Success = response.StatusCode == 200,
                Data = JsonSerializer.Deserialize<object>(responsePayload),
                Metadata = new Dictionary<string, string>
                {
                    ["StatusCode"] = response.StatusCode.ToString(),
                    ["ExecutedVersion"] = response.ExecutedVersion,
                    ["LogResult"] = response.LogResult
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke Lambda function {FunctionName}", functionName);
            return new FunctionResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<ServerlessFunctionConfig?> GetFunctionConfigAsync(string functionName)
    {
        try
        {
            var function = await GetFunctionAsync(functionName);
            if (function == null) return null;

            return new ServerlessFunctionConfig
            {
                FunctionName = function.FunctionName,
                Runtime = function.Runtime.Value,
                Handler = function.Handler,
                MemorySize = function.MemorySize,
                Timeout = function.Timeout,
                Description = function.Description,
                Role = function.Role,
                EnvironmentVariables = function.Environment?.Variables ?? new Dictionary<string, string>(),
                Tags = function.Tags?.Keys.ToList() ?? new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get configuration for Lambda function {FunctionName}", functionName);
            return null;
        }
    }

    public async Task<IEnumerable<string>> ListFunctionsAsync()
    {
        try
        {
            var functions = new List<string>();
            string? nextMarker = null;

            do
            {
                var request = new ListFunctionsRequest
                {
                    Marker = nextMarker,
                    MaxItems = 50
                };

                var response = await _lambdaClient.ListFunctionsAsync(request);
                functions.AddRange(response.Functions.Select(f => f.FunctionName));
                nextMarker = response.NextMarker;

            } while (!string.IsNullOrEmpty(nextMarker));

            return functions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list Lambda functions");
            return Array.Empty<string>();
        }
    }

    public async Task<bool> UpdateFunctionCodeAsync(string functionName, byte[] codePackage)
    {
        try
        {
            var updateRequest = new UpdateFunctionCodeRequest
            {
                FunctionName = functionName,
                ZipFile = new MemoryStream(codePackage),
                Publish = true
            };

            await _lambdaClient.UpdateFunctionCodeAsync(updateRequest);
            _logger.LogInformation("Updated code for Lambda function {FunctionName}", functionName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update code for Lambda function {FunctionName}", functionName);
            return false;
        }
    }

    public async Task<bool> UpdateFunctionConfigAsync(string functionName, ServerlessFunctionConfig config)
    {
        try
        {
            var updateRequest = new UpdateFunctionConfigurationRequest
            {
                FunctionName = functionName,
                Runtime = MapRuntime(config.Runtime),
                Handler = config.Handler,
                Description = config.Description,
                Timeout = config.Timeout,
                MemorySize = config.MemorySize,
                Environment = new Amazon.Lambda.Model.Environment
                {
                    Variables = config.EnvironmentVariables
                }
            };

            if (config.DeadLetterQueue != null)
            {
                updateRequest.DeadLetterConfig = new DeadLetterConfig
                {
                    TargetArn = GetQueueArn(config.DeadLetterQueue.QueueName)
                };
            }

            await _lambdaClient.UpdateFunctionConfigurationAsync(updateRequest);
            _logger.LogInformation("Updated configuration for Lambda function {FunctionName}", functionName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update configuration for Lambda function {FunctionName}", functionName);
            return false;
        }
    }

    public async Task<Dictionary<string, object>> GetFunctionMetricsAsync(string functionName, DateTime startTime, DateTime endTime)
    {
        // This would typically use CloudWatch to get metrics
        // For now, returning basic metrics from function configuration
        var metrics = new Dictionary<string, object>();

        try
        {
            var function = await GetFunctionAsync(functionName);
            if (function != null)
            {
                metrics["CodeSize"] = function.CodeSize;
                metrics["LastModified"] = function.LastModified;
                metrics["Version"] = function.Version;
                metrics["State"] = function.State.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metrics for Lambda function {FunctionName}", functionName);
        }

        return metrics;
    }

    private async Task<GetFunctionResponse?> GetFunctionAsync(string functionName)
    {
        try
        {
            var request = new GetFunctionRequest { FunctionName = functionName };
            return await _lambdaClient.GetFunctionAsync(request);
        }
        catch (ResourceNotFoundException)
        {
            return null;
        }
    }

    private async Task ConfigureTriggerAsync(string functionName, TriggerConfig trigger)
    {
        try
        {
            // Add permission for the trigger source to invoke the function
            var permissionRequest = new AddPermissionRequest
            {
                FunctionName = functionName,
                StatementId = $"{functionName}-{trigger.Type}-trigger",
                Action = "lambda:InvokeFunction",
                Principal = GetTriggerPrincipal(trigger.Type)
            };

            // Configure source ARN based on trigger type
            if (trigger.Properties.TryGetValue("SourceArn", out var sourceArn))
            {
                permissionRequest.SourceArn = sourceArn.ToString();
            }

            await _lambdaClient.AddPermissionAsync(permissionRequest);
            _logger.LogInformation("Configured {TriggerType} trigger for Lambda function {FunctionName}", 
                trigger.Type, functionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure trigger for Lambda function {FunctionName}", functionName);
        }
    }

    private string GetTriggerPrincipal(TriggerType triggerType)
    {
        return triggerType switch
        {
            TriggerType.Http => "apigateway.amazonaws.com",
            TriggerType.Timer => "events.amazonaws.com",
            TriggerType.Queue => "sqs.amazonaws.com",
            TriggerType.Storage => "s3.amazonaws.com",
            TriggerType.EventGrid => "events.amazonaws.com",
            _ => "lambda.amazonaws.com"
        };
    }

    private Runtime MapRuntime(string runtime)
    {
        return runtime.ToLowerInvariant() switch
        {
            "dotnet8" => Runtime.Dotnet8,
            "dotnet6" => Runtime.Dotnet6,
            "python3.11" => Runtime.Python311,
            "python3.10" => Runtime.Python310,
            "python3.9" => Runtime.Python39,
            "nodejs18" => Runtime.Nodejs18X,
            "nodejs16" => Runtime.Nodejs16X,
            "java17" => Runtime.Java17,
            "java11" => Runtime.Java11,
            _ => Runtime.Dotnet8
        };
    }

    private string GetDefaultRole()
    {
        // Return a default Lambda execution role ARN
        return $"arn:aws:iam::{_accountId}:role/lambda-execution-role";
    }

    private string GetQueueArn(string queueName)
    {
        // Construct SQS queue ARN
        return $"arn:aws:sqs:{_region.SystemName}:{_accountId}:{queueName}";
    }
}
