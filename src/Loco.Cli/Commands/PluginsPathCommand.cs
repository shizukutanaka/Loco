using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using Loco.Core.Plugins;

namespace Loco.Cli.Commands;

public class PluginsPathCommand : Command
{
    public PluginsPathCommand()
        : base("plugins-path", "Show effective plugins directory / 有効なプラグインディレクトリを表示します")
    {
        var verboseOption = new Option<bool>(new[] { "--verbose", "-v" }, () => false, "Print path source (explicit/env/default) / パスの由来（explicit/env/default）を表示");
        AddOption(verboseOption);

        this.SetHandler((InvocationContext ctx) =>
        {
            var provided = ctx.ParseResult.GetValueForOption(Program.PluginsPathOption);
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
            var (path, source) = PluginPaths.GetEffectivePluginsDirectoryWithSource(provided);
            // Ensure exists for convenience
            PluginPaths.EnsureDirectory(path);
            if (verbose)
            {
                Console.WriteLine($"{path} (source={source})");
            }
            else
            {
                Console.WriteLine(path);
            }
        });
    }
}
