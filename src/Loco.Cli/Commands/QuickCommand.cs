using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using Loco.Core.FlowComposer;
using Microsoft.Extensions.Logging;

namespace Loco.Cli.Commands;

public class QuickCommand : Command
{
    public QuickCommand()
        : base("quick", "クイックビルド (例: loco quick timer 7:00 notify 'おはよう')")
    {
        var quickArgsOption = new Argument<string[]>("args", "クイックビルド引数");
        AddArgument(quickArgsOption);

        this.SetHandler(async (FlowComposerBuilder composerBuilder, ILogger<QuickCommand> logger, string[] args) =>
        {
            await HandleAsync(composerBuilder, logger, args);
        }, quickArgsOption);
    }

    private static async Task HandleAsync(FlowComposerBuilder composerBuilder, ILogger<QuickCommand> logger, string[] args)
    {
        try
        {
            if (args.Length < 2)
            {
                logger.LogWarning("Usage: loco quick <component> <params> [<component> <params>...]");
                logger.LogWarning("Example: loco quick timer 7:00 notify 'Good morning'");
                return;
            }

            var flowBuilder = composerBuilder.StartFlow("Quick Flow");

            // Parse quick commands
            var currentType = "";
            var currentParams = new Dictionary<string, object>();

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i].ToLower();

                // Detect component type
                if (arg == "timer" || arg == "time")
                {
                    if (!string.IsNullOrEmpty(currentType))
                    {
                        AddQuickComponent(flowBuilder, currentType, currentParams);
                        currentParams.Clear();
                    }
                    currentType = "time.schedule";
                    if (i + 1 < args.Length)
                    {
                        var time = args[++i].Split(':');
                        if (time.Length >= 1) currentParams["hour"] = int.Parse(time[0]);
                        if (time.Length >= 2) currentParams["minute"] = int.Parse(time[1]);
                    }
                }
                else if (arg == "notify" || arg == "notification")
                {
                    if (!string.IsNullOrEmpty(currentType))
                    {
                        AddQuickComponent(flowBuilder, currentType, currentParams);
                        currentParams.Clear();
                    }
                    currentType = "notification.show";
                    if (i + 1 < args.Length)
                    {
                        currentParams["title"] = "Loco通知";
                        currentParams["message"] = args[++i];
                    }
                }
                else if (arg == "run" || arg == "exec")
                {
                    if (!string.IsNullOrEmpty(currentType))
                    {
                        AddQuickComponent(flowBuilder, currentType, currentParams);
                        currentParams.Clear();
                    }
                    currentType = "app.run";
                    if (i + 1 < args.Length)
                    {
                        currentParams["path"] = args[++i];
                    }
                }
            }

            // Add last component
            if (!string.IsNullOrEmpty(currentType))
            {
                AddQuickComponent(flowBuilder, currentType, currentParams);
            }

            // Build and save
            var flow = flowBuilder.Build();
            var json = flowBuilder.ToJson();
            var filename = $"quick_flow_{DateTime.Now:yyyyMMdd_HHmmss}.json";

            await File.WriteAllTextAsync(filename, json);
            logger.LogInformation("Quick flow created: {FileName}", filename);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during quick build.");
        }
    }

    private static void AddQuickComponent(FlowBuilder builder, string componentId, Dictionary<string, object> parameters)
    {
        if (componentId.Contains("time") || componentId.Contains("file") || componentId.Contains("app") || componentId.Contains("system") || componentId.Contains("webhook"))
        {
            builder.AddTrigger(componentId, parameters);
        }
        else if (componentId.Contains("condition") || componentId.Contains("check") || componentId.Contains("compare"))
        {
            builder.AddCondition(componentId, parameters);
        }
        else
        {
            builder.AddAction(componentId, parameters);
        }
    }
}
