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
        try
        {
            if (!File.Exists(file))
            {
                logger.LogError("Flow file not found: {File}", Path.GetFullPath(file));
                return;
            }

            await pluginManager.LoadPluginsAsync();

            // Ensure automation service is started (loads any saved rules and readies the engine)
            var appLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            var stopToken = appLifetime.ApplicationStopping;
            await automationService.StartAsync(stopToken);

            var ruleJson = await File.ReadAllTextAsync(file, stopToken);
            var effectiveModelId = modelIdArg ?? llmOptions.Value.Model;

            if (!string.IsNullOrWhiteSpace(effectiveModelId))
            {
                ruleJson = ruleService.InjectModelId(ruleJson, effectiveModelId, logger);
            }

            var validationResult = await automationService.ValidateRuleJsonAsync(ruleJson, stopToken);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors is null ? "unknown error" : string.Join(", ", validationResult.Errors);
                logger.LogError("Flow validation failed: {Errors}", errors);
                return;
            }

            var added = await automationService.AddRuleFromJsonAsync(ruleJson, stopToken);
            if (!added)
            {
                logger.LogError("Failed to add flow to the engine.");
                return;
            }

            var rule = JsonSerializer.Deserialize<AutomationDsl.Rule>(ruleJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (rule == null)
            {
                logger.LogError("Failed to deserialize rule to trigger.");
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
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to parse --inputs-json. Provide a JSON object.");
                    return;
                }
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));
            await ruleEngine.TriggerRuleAsync(rule.Id, inputs, linkedCts.Token);

            logger.LogInformation("Flow execution finished.");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Flow execution timed out after {Timeout} seconds.", timeoutSeconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during flow execution.");
        }
    }
}
