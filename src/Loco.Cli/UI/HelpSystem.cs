using System;
using System.Linq;
using Loco.Cli.UI.Models;
using Loco.Cli.UI.Providers;

namespace Loco.Cli.UI;

/// <summary>
/// Main help system that orchestrates command help providers and formatters
/// Provides unified interface for help display, search, and suggestions
/// </summary>
public class HelpSystem
{
    private readonly CommandHelpProvider _provider;
    private readonly CommandHelpFormatter _formatter;
    private readonly CommandSearcher _searcher;

    public HelpSystem()
    {
        _provider = new CommandHelpProvider();
        _formatter = new CommandHelpFormatter();
        _searcher = new CommandSearcher(_provider);
    }

    /// <summary>
    /// Displays help information
    /// </summary>
    public void ShowHelp(string? commandName = null)
    {
        _formatter.ShowHelp(commandName, _provider);
    }

    /// <summary>
    /// Searches for commands matching the query
    /// </summary>
    public string[] SearchCommands(string query)
    {
        return _searcher.SearchCommands(query);
    }

    /// <summary>
    /// Gets smart suggestion for user input
    /// </summary>
    public CommandSuggestion GetSmartSuggestion(string userInput, string? context = null)
    {
        return _searcher.GetSmartSuggestion(userInput, context);
    }

    /// <summary>
    /// Shows usage tips
    /// </summary>
    public void ShowUsageTips()
    {
        _formatter.ShowUsageTips();
    }

    /// <summary>
    /// Adds a custom command to the help system
    /// </summary>
    public void AddCommand(string name, CommandHelp help)
    {
        _provider.AddCommand(name, help);
    }

    /// <summary>
    /// Adds an alias for a command
    /// </summary>
    public void AddAlias(string alias, string commandName)
    {
        _provider.AddAlias(alias, commandName);
    }

    /// <summary>
    /// Gets the canonical command name from a name or alias
    /// </summary>
    public string? GetCommandName(string nameOrAlias)
    {
        return _provider.GetCommandName(nameOrAlias);
    }
}
