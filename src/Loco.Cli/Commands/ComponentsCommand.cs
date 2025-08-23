using System;
using System.CommandLine;
using System.Linq;
using Loco.Core.FlowComposer;
using Microsoft.Extensions.Logging;

namespace Loco.Cli.Commands;

public class ComponentsCommand : Command
{
    public ComponentsCommand()
        : base("components", "利用可能なコンポーネントを表示")
    {
        this.SetHandler((FlowComposerBuilder composerBuilder, ILogger<ComponentsCommand> logger) =>
        {
            Handle(composerBuilder, logger);
        });
    }

    private static void Handle(FlowComposerBuilder composerBuilder, ILogger<ComponentsCommand> logger)
    {
        logger.LogInformation("📦 Available Components");
        logger.LogInformation("=====================================");

        foreach (var category in composerBuilder.GetCategories())
        {
            logger.LogInformation("\n📁 {Icon} {Name} - {Description}", category.Icon, category.Name, category.Description);

            foreach (var component in category.Components.OrderBy(c => c.Name))
            {
                logger.LogInformation("  - {Icon} {Name} ({Id})", component.Icon, component.Name, component.Id);
                logger.LogInformation("    {Description}", component.Description);
            }
        }

        logger.LogInformation("\n=====================================");
    }
}
