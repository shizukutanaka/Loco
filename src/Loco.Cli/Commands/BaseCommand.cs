using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Loco.Cli.Commands;

/// <summary>
/// Base class for all CLI commands
/// </summary>
public abstract class BaseCommand
{
    protected readonly LocalizationManager Localization;

    protected BaseCommand()
    {
        Localization = new LocalizationManager();
    }

    /// <summary>
    /// Execute the command
    /// </summary>
    public abstract Task<int> ExecuteAsync(string[] args);

    /// <summary>
    /// Get command help information
    /// </summary>
    public abstract CommandHelp GetHelp();

    /// <summary>
    /// Try to consume an option from arguments
    /// </summary>
    protected static bool TryConsumeOption(List<string> args, string optionName, out string? value)
    {
        value = null;
        for (int i = 0; i < args.Count; i++)
        {
            var current = args[i];
            if (current.Equals(optionName, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Count)
                {
                    Console.WriteLine($"Option {optionName} requires a value.");
                    return false;
                }

                value = args[i + 1];
                args.RemoveAt(i + 1);
                args.RemoveAt(i);
                return true;
            }

            if (current.StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = current.Substring(optionName.Length + 1);
                args.RemoveAt(i);
                return true;
            }
        }

        return true;
    }

    /// <summary>
    /// Consume a flag from arguments
    /// </summary>
    protected static bool ConsumeFlag(List<string> args, string flag)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
            {
                args.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Command help information
    /// </summary>
    public class CommandHelp
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Usage { get; set; } = string.Empty;
        public string[]? Examples { get; set; }
        public string[]? Options { get; set; }
        public string[]? Subcommands { get; set; }
    }
}
