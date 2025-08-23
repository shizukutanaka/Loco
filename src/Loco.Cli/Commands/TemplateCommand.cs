using System;
using System.CommandLine;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Cli.Commands;

public class TemplateCommand : Command
{
    public TemplateCommand()
        : base("template", "テンプレート管理")
    {
        var listCommand = new Command("list", "List available templates");
        listCommand.SetHandler((ILogger<TemplateCommand> logger) => HandleListAsync(logger));
        AddCommand(listCommand);

        var applyCommand = new Command("apply", "Apply a template");
        var nameOption = new Option<string>("--name", "The name of the template") { IsRequired = true };
        applyCommand.AddOption(nameOption);
        applyCommand.SetHandler(async (string name, ILogger<TemplateCommand> logger) =>
        {
            await HandleApplyAsync(name, logger);
        }, nameOption);
        AddCommand(applyCommand);
    }

    private static Task HandleListAsync(ILogger<TemplateCommand> logger)
    {
        logger.LogInformation("Available Templates");
        logger.LogInformation("=====================================");
        logger.LogInformation("1. Morning Routine - Automate your morning");
        logger.LogInformation("2. File Backup - Regular backups");
        logger.LogInformation("3. System Monitor - Resource monitoring and notifications");
        logger.LogInformation("4. News Summary - AI-powered news reading");
        logger.LogInformation("5. Smart Home - Control your home devices");

        return Task.CompletedTask;
    }

    private static async Task HandleApplyAsync(string name, ILogger<TemplateCommand> logger)
    {
        try
        {
            logger.LogInformation("Applying template '{TemplateName}'...", name);
            // This is a mock implementation
            await Task.Delay(1000);
            logger.LogInformation("Template '{TemplateName}' applied successfully.", name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying template '{TemplateName}'.", name);
        }
    }
}
