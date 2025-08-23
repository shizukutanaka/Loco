using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using Loco.Automation.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Loco.Cli.Commands;

public class ValidateCommand : Command
{
    public ValidateCommand()
        : base("validate", "フロー定義を検証")
    {
        var flowFileOption = new Option<string>(
            name: "--file",
            description: "フロー定義ファイルのパス",
            getDefaultValue: () => "flow.json")
        {
            
        };
        AddOption(flowFileOption);

        // Host-aware handler (uses ApplicationStopping token)
        this.SetHandler(async (IHost host, IAutomationService automationService, ILogger<ValidateCommand> logger, string file) =>
        {
            await HandleAsyncWithHost(host, automationService, logger, file);
        }, flowFileOption);
    }

    internal static async Task HandleAsync(IAutomationService automationService, ILogger<ValidateCommand> logger, string file)
    {
        try
        {
            if (!File.Exists(file))
            {
                logger.LogError("Flow file not found: {File}", Path.GetFullPath(file));
                return;
            }

            var ruleJson = await File.ReadAllTextAsync(file);

            var result = await automationService.ValidateRuleJsonAsync(ruleJson);

            if (result.IsValid)
            {
                logger.LogInformation("Flow definition in '{File}' is valid.", file);
            }
            else
            {
                var errors = result.Errors is null ? "unknown error" : string.Join(", ", result.Errors);
                logger.LogError("Flow definition in '{File}' is invalid: {Errors}", file, errors);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during flow validation.");
        }
    }

    // Host-aware variant used by CLI runtime to support cooperative cancellation
    private static async Task HandleAsyncWithHost(IHost host, IAutomationService automationService, ILogger<ValidateCommand> logger, string file)
    {
        try
        {
            if (!File.Exists(file))
            {
                logger.LogError("Flow file not found: {File}", Path.GetFullPath(file));
                return;
            }

            var appLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            var stopToken = appLifetime.ApplicationStopping;

            var ruleJson = await File.ReadAllTextAsync(file, stopToken);
            var result = await automationService.ValidateRuleJsonAsync(ruleJson, stopToken);

            if (result.IsValid)
            {
                logger.LogInformation("Flow definition in '{File}' is valid.", file);
            }
            else
            {
                var errors = result.Errors is null ? "unknown error" : string.Join(", ", result.Errors);
                logger.LogError("Flow definition in '{File}' is invalid: {Errors}", file, errors);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during flow validation.");
        }
    }
}
