using System;
using System.Collections.Generic;
using System.CommandLine;
using Loco.Automation.Interfaces;
using Loco.Llm;
using Microsoft.Extensions.Options;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Interfaces;
using Loco.Core.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Loco.Core.ErrorHandling;
using Loco.Core.Validation;

namespace Loco.Cli.Commands;

public class ExecuteCommand : Command
{
    public ExecuteCommand()
        : base("execute", "フローを実行")
    {
        var flowFileOption = new Option<string>(
            name: "--file",
            description: "フロー定義ファイルのパス",
            getDefaultValue: () => "flow.json")
        {
        };
        AddOption(flowFileOption);

        var timeoutOption = new Option<int>(
            name: "--timeout-seconds",
            description: "実行のタイムアウト秒数 / Timeout in seconds",
            getDefaultValue: () => 60);
        AddOption(timeoutOption);

        var inputsOption = new Option<string>(
            name: "--inputs-json",
            description: "トリガー入力(JSONオブジェクト) / Trigger inputs as JSON object");
        AddOption(inputsOption);

        this.SetHandler(async (IHost host, ILogger<ExecuteCommand> logger, IAutomationService automationService, IAutomationRuleEngine ruleEngine, IRuleManipulationService ruleService, PluginManager pluginManager, IOptions<LlmConfiguration> llmOptions, string file, string modelId, int timeoutSeconds, string inputsJson) =>
        {
            await HandleAsync(host, logger, automationService, ruleEngine, ruleService, pluginManager, llmOptions, file, modelId, timeoutSeconds, inputsJson);
        }, flowFileOption, Program.ModelIdOption, timeoutOption, inputsOption);
    }

    internal static async Task HandleAsync(IHost host, ILogger<ExecuteCommand> logger, IAutomationService automationService, IAutomationRuleEngine ruleEngine, IRuleManipulationService ruleService, PluginManager pluginManager, IOptions<LlmConfiguration> llmOptions, string file, string modelIdArg, int timeoutSeconds, string? inputsJson)
    {
        var errorHandler = host.Services.GetService<ErrorHandler>() ?? new ErrorHandler(logger);

        try
        {
            // Validate file path
            var configValidator = new ConfigurationValidator(logger);
            var pathValidation = configValidator.ValidatePath(file, PathValidationType.FileMustExist);

            if (!pathValidation.IsValid)
            {
                foreach (var error in pathValidation.Errors)
                {
                    logger.LogError("{Field}: {Message}", error.Field, error.Message);
                }
                return;
            }

            // Load plugins with retry
            await errorHandler.HandleWithRetryAsync(
                async () => await pluginManager.LoadPluginsAsync(),
                maxAttempts: 3,
                delay: TimeSpan.FromSeconds(1)
            );

            // Ensure automation service is started (loads any saved rules and readies the engine)
            var appLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            var stopToken = appLifetime.ApplicationStopping;

            await errorHandler.HandleWithRetryAsync(
                async () => await automationService.StartAsync(stopToken),
                maxAttempts: 2,
                delay: TimeSpan.FromSeconds(2)
            );

            var ruleJson = await File.ReadAllTextAsync(file, stopToken);
            var effectiveModelId = modelIdArg ?? llmOptions.Value.Model;

            if (!string.IsNullOrWhiteSpace(effectiveModelId))
            {
                ruleJson = ruleService.InjectModelId(ruleJson, effectiveModelId, logger);
            }

            // Enhanced validation
            var validationResult = await automationService.ValidateRuleJsonAsync(ruleJson, stopToken);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors is null ? "unknown error" : string.Join(", ", validationResult.Errors);
                logger.LogError("Flow validation failed: {Errors}", errors);

                // Log detailed validation errors
                if (validationResult.Errors != null)
                {
                    foreach (var error in validationResult.Errors)
                    {
                        logger.LogError("  - {Error}", error);
                    }
                }
                return;
            }

            var added = await errorHandler.HandleWithRetryAsync(
                async () => await automationService.AddRuleFromJsonAsync(ruleJson, stopToken),
                maxAttempts: 2
            );

            if (!added)
            {
                throw new InvalidOperationException("Failed to add flow to the engine after retries.");
            }

            AutomationDsl.Rule rule;
            try
            {
                rule = JsonSerializer.Deserialize<AutomationDsl.Rule>(ruleJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new JsonException("Deserialization resulted in null rule");
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to deserialize rule. Check JSON format.");
                return;
            }

            logger.LogInformation("Executing flow '{FlowName}' from file {File}...", rule.Name, file);

            var inputs = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(inputsJson))
            {
                try
                {
                    inputs = JsonSerializer.Deserialize<Dictionary<string, object>>(inputsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                             ?? new Dictionary<string, object>();

                    logger.LogDebug("Parsed {Count} input parameters", inputs.Count);
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Failed to parse --inputs-json. Provide a valid JSON object.");
                    logger.LogInformation("Example: --inputs-json '{\"key\":\"value\"}'");
                    return;
                }
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
            var timeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds));
            linkedCts.CancelAfter(timeout);

            logger.LogInformation("Executing with timeout: {Timeout}s", timeoutSeconds);

            try
            {
                await ruleEngine.TriggerRuleAsync(rule.Id, inputs, linkedCts.Token);
                logger.LogInformation("Flow execution completed successfully.");
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !stopToken.IsCancellationRequested)
            {
                logger.LogWarning("Flow execution timed out after {Timeout} seconds.", timeoutSeconds);
                throw new TimeoutException($"Flow execution exceeded timeout of {timeoutSeconds} seconds");
            }
        }
        catch (TimeoutException tex)
        {
            var result = await errorHandler.HandleAsync(tex, new ErrorContext
            {
                AdditionalData = new Dictionary<string, object>
                {
                    ["File"] = file,
                    ["Timeout"] = timeoutSeconds
                }
            });

            logger.LogWarning(result.UserMessage);
        }
        catch (Exception ex)
        {
            var result = await errorHandler.HandleAsync(ex, new ErrorContext
            {
                AdditionalData = new Dictionary<string, object>
                {
                    ["File"] = file,
                    ["Command"] = "execute"
                }
            });

            if (!result.Handled)
            {
                throw;
            }

            logger.LogError("Error ID: {ErrorId}", result.ErrorId);
        }
    }
}
