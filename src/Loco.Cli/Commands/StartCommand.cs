using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using Loco.Automation.Interfaces;
using Loco.Core.Plugins;
using Loco.Llm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loco.Cli.Commands;

public class StartCommand : Command
{
    private readonly Option<string> _rulesOption = new(
        new[] { "--rules", "-r" },
        "Path to a rules file or directory containing rules files.");

    public StartCommand()
        : base("start", "Start the automation service / 自動化サービスを開始します")
    {
        AddOption(_rulesOption);

        this.SetHandler(async (IHost host, ILogger<StartCommand> logger, IRuleManipulationService ruleService, string? rulesPath, string? modelId) =>
        {
            await HandleAsync(host, logger, ruleService, rulesPath, modelId);
        }, _rulesOption, Program.ModelIdOption);
    }

    internal static async Task HandleAsync(IHost host, ILogger<StartCommand> logger, IRuleManipulationService ruleService, string? rulesPath, string? modelIdArg)
    {
        var appLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var automationService = host.Services.GetRequiredService<IAutomationService>();
        var pluginManager = host.Services.GetRequiredService<PluginManager>();
        var llmOptions = host.Services.GetRequiredService<IOptions<LlmConfiguration>>();

        try
        {
            logger.LogInformation("Starting automation service...");

            await pluginManager.LoadPluginsAsync();
            logger.LogInformation("Plugins loaded from {Path}", pluginManager.PluginsDirectory);

            var effectiveModelId = modelIdArg 
                                 ?? llmOptions.Value.Model; // Get from config as fallback

            // Start the automation service (loads any saved rules from the store)
            var started = await automationService.StartAsync(appLifetime.ApplicationStopping);
            if (!started)
            {
                logger.LogCritical("Failed to start automation service. Aborting.");
                return;
            }

            if (!string.IsNullOrEmpty(rulesPath))
            {
                logger.LogInformation("Loading rules from path: {RulesPath}", rulesPath);
                var addedCount = 0;
                var failedCount = 0;
                if (File.Exists(rulesPath))
                {
                    var ruleJson = await File.ReadAllTextAsync(rulesPath, appLifetime.ApplicationStopping);
                    if (!string.IsNullOrWhiteSpace(effectiveModelId))
                    {
                        ruleJson = ruleService.InjectModelId(ruleJson, effectiveModelId, logger);
                    }
                    var validation = await automationService.ValidateRuleJsonAsync(ruleJson, appLifetime.ApplicationStopping);
                    if (!validation.IsValid)
                    {
                        failedCount++;
                        var errors = validation.Errors is null ? "unknown error" : string.Join(", ", validation.Errors);
                        logger.LogError("Invalid rule at '{Path}': {Errors}", rulesPath, errors);
                    }
                    else if (await automationService.AddRuleFromJsonAsync(ruleJson, appLifetime.ApplicationStopping))
                    {
                        addedCount++;
                    }
                    else
                    {
                        failedCount++;
                        logger.LogError("Failed to add rule from '{Path}'", rulesPath);
                    }
                }
                else if (Directory.Exists(rulesPath))
                {
                    var ruleFiles = Directory.GetFiles(rulesPath, "*.json");
                    foreach (var file in ruleFiles)
                    {
                        var ruleJson = await File.ReadAllTextAsync(file, appLifetime.ApplicationStopping);
                        if (!string.IsNullOrWhiteSpace(effectiveModelId))
                        {
                            ruleJson = ruleService.InjectModelId(ruleJson, effectiveModelId, logger);
                        }
                        var validation = await automationService.ValidateRuleJsonAsync(ruleJson, appLifetime.ApplicationStopping);
                        if (!validation.IsValid)
                        {
                            failedCount++;
                            var errors = validation.Errors is null ? "unknown error" : string.Join(", ", validation.Errors);
                            logger.LogError("Invalid rule at '{Path}': {Errors}", file, errors);
                            continue;
                        }
                        if (await automationService.AddRuleFromJsonAsync(ruleJson, appLifetime.ApplicationStopping))
                        {
                            addedCount++;
                        }
                        else
                        {
                            failedCount++;
                            logger.LogError("Failed to add rule from '{Path}'", file);
                        }
                    }
                }
                else
                {
                    logger.LogWarning("Specified rules path does not exist: {RulesPath}", rulesPath);
                }

                logger.LogInformation("Rules processed. Added={Added}, Failed={Failed}", addedCount, failedCount);
            }

            logger.LogInformation("Service started. Press Ctrl+C to exit.");

            // The host is already running, just wait for shutdown signal
            await host.WaitForShutdownAsync(appLifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "An unhandled exception occurred during service startup.");
            Environment.Exit(1);
        }
    }
}
