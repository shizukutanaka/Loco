using System;
using System.CommandLine;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Loco.Core.Models;
using Microsoft.Extensions.Logging;

namespace Loco.Cli.Commands;

public class ListCommand : Command
{
    public ListCommand()
        : base("list", "利用可能なフローを一覧表示")
    {
        this.SetHandler(async (ILogger<ListCommand> logger) =>
        {
            await HandleAsync(logger);
        });
    }

    private static async Task HandleAsync(ILogger<ListCommand> logger)
    {
        try
        {
            var flowsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Loco", "Flows");

            if (!Directory.Exists(flowsDir))
            {
                logger.LogWarning("Default flows directory not found at {FlowsDir}. Falling back to current directory.", flowsDir);
                flowsDir = Environment.CurrentDirectory;
            }

            var flowFiles = Directory.GetFiles(flowsDir, "*.json");

            if (flowFiles.Length == 0)
            {
                logger.LogInformation("No flows found.");
                return;
            }

            logger.LogInformation("Available Flows:");
            logger.LogInformation("=====================================");

            foreach (var file in flowFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var flow = JsonSerializer.Deserialize<FlowDefinition>(json);

                    if (flow != null)
                    {
                        logger.LogInformation("\n{FlowName}", flow.Name);
                        if (!string.IsNullOrEmpty(flow.Description))
                        {
                            logger.LogInformation("   {FlowDescription}", flow.Description);
                        }
                        logger.LogInformation("   File: {FileName}", Path.GetFileName(file));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "\nError parsing file '{FileName}'.", Path.GetFileName(file));
                }
            }

            logger.LogInformation("\n=====================================");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while listing flows.");
        }
    }
}
