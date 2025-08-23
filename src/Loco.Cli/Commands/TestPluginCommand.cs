using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using Loco.Core.Plugins;
using Loco.Automation.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Loco.Llm;

namespace Loco.Cli.Commands;

public class TestPluginCommand : Command
{
    public TestPluginCommand()
        : base("test-plugin", "プラグインのテストルールを実行")
    {
        var rulePathOption = new Option<string>("--rule-path", () => "examples/rules/plugin-test-rule.json", "テストするルールファイルのパス");
        AddOption(rulePathOption);

        this.SetHandler(async (IHost host, ILogger<TestPluginCommand> logger, IRuleManipulationService ruleService, string rulePath, string modelId) =>
        {
            await HandleAsync(host, logger, ruleService, rulePath, modelId);
        }, rulePathOption, Program.ModelIdOption);
    }

    private static async Task HandleAsync(IHost host, ILogger<TestPluginCommand> logger, IRuleManipulationService ruleService, string rulePath, string modelIdArg)
    {
        var services = host.Services;
        var llmOptions = services.GetRequiredService<IOptions<LlmConfiguration>>();
        var pluginManager = services.GetRequiredService<PluginManager>();
        var automationService = services.GetRequiredService<IAutomationService>();
        var ruleEngine = services.GetRequiredService<IAutomationRuleEngine>();
        var appLifetime = services.GetRequiredService<IHostApplicationLifetime>();
        var stopToken = appLifetime.ApplicationStopping;

        try
        {
            logger.LogInformation("--- Running Plugin Test ---");
            logger.LogInformation("Using rule from: {RulePath}", rulePath);
            logger.LogInformation("Using plugins from: {PluginsPath}", pluginManager.PluginsDirectory);

            await pluginManager.LoadPluginsAsync();

            if (!File.Exists(rulePath))
            {
                logger.LogError("Test rule file not found at {Path}", Path.GetFullPath(rulePath));
                return;
            }

            var ruleJson = await File.ReadAllTextAsync(rulePath, stopToken);
            // Inject stable modelId if provided via option or environment
            var effectiveModelId = modelIdArg ?? llmOptions.Value.Model;

            if (!string.IsNullOrWhiteSpace(effectiveModelId))
            {
                ruleJson = ruleService.InjectModelId(ruleJson, effectiveModelId, logger);
            }
            // Validate before adding
            var validation = await automationService.ValidateRuleJsonAsync(ruleJson, stopToken);
            if (!validation.IsValid)
            {
                var errors = validation.Errors is null ? "unknown error" : string.Join(", ", validation.Errors);
                logger.LogError("Invalid test rule at '{Path}': {Errors}", rulePath, errors);
                return;
            }

            var added = await automationService.AddRuleFromJsonAsync(ruleJson, stopToken);

            if (added)
            {
                logger.LogInformation("Test rule '{RulePath}' added successfully.", rulePath);
                var rule = JsonSerializer.Deserialize<AutomationDsl.Rule>(ruleJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (rule != null)
                {
                    await ruleEngine.TriggerRuleAsync(rule.Id, new Dictionary<string, object>(), stopToken);
                    logger.LogInformation("Test rule '{RuleId}' triggered. Check for 'plugin-test.log' in the plugin's data directory.", rule.Id);
                }
            }
            else
            {
                logger.LogError("Failed to add test rule from '{RulePath}'.", rulePath);
            }

            logger.LogInformation("--- Plugin Test Finished ---");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unhandled exception occurred during plugin test.");
        }
        finally
        {
            // Ensure logs are written before exit
            await Task.Delay(200);
        }
    }

}
