using System;
using System.Collections.Generic;
using System.Linq;
using Loco.Cli.UI.Models;

namespace Loco.Cli.UI.Providers;

/// <summary>
/// Handles command search and intelligent suggestions
/// Provides fuzzy matching and contextual command recommendations
/// </summary>
public class CommandSearcher
{
    private readonly CommandHelpProvider _provider;

    public CommandSearcher(CommandHelpProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>
    /// Searches for commands matching the query
    /// </summary>
    public string[] SearchCommands(string query)
    {
        return _provider.Commands.Values
            .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                       c.ShortDescription.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                       c.LongDescription.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Name)
            .ToArray();
    }

    /// <summary>
    /// Provides intelligent command suggestion based on user input
    /// </summary>
    public CommandSuggestion GetSmartSuggestion(string userInput, string? context = null)
    {
        var suggestion = new CommandSuggestion();
        var input = userInput.ToLower().Trim();

        // Empty input case
        if (string.IsNullOrEmpty(input))
        {
            suggestion.PrimaryCommand = "help";
            suggestion.Message = "Type 'help' to see all available commands";
            suggestion.MessageJA = "すべての利用可能なコマンドを見るには 'help' と入力してください";
            suggestion.Confidence = 1.0;
            return suggestion;
        }

        // Exact match case
        var resolved = _provider.GetCommandName(input);
        if (resolved != null)
        {
            var cmd = _provider.GetCommandHelp(resolved);
            if (cmd != null)
            {
                suggestion.PrimaryCommand = cmd.Name;
                suggestion.Message = $"Did you mean '{cmd.Name}'? {cmd.ShortDescription}";
                suggestion.MessageJA = $"「{cmd.Name}」のことですか？ {cmd.ShortDescription}";
                suggestion.Confidence = 1.0;
                return suggestion;
            }
        }

        // Partial match - find best candidates
        var candidates = _provider.Commands.Values
            .Select(c => new
            {
                Command = c,
                Score = CalculateMatchScore(input, c)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(3)
            .ToList();

        if (candidates.Any())
        {
            var best = candidates.First();
            suggestion.PrimaryCommand = best.Command.Name;
            suggestion.Message = $"Did you mean '{best.Command.Name}'? {best.Command.ShortDescription}";
            suggestion.MessageJA = $"「{best.Command.Name}」のことですか？ {best.Command.ShortDescription}";
            suggestion.Confidence = best.Score;

            if (candidates.Count > 1)
            {
                suggestion.Alternatives = candidates.Skip(1).Select(c => c.Command.Name).ToArray();
            }
        }
        else
        {
            // Context-based suggestion
            suggestion = GetContextualSuggestion(input, context);
        }

        return suggestion;
    }

    /// <summary>
    /// Calculates match score for a command against user input
    /// </summary>
    private double CalculateMatchScore(string input, CommandHelp command)
    {
        double score = 0;

        // Command name matching
        if (command.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            score += 0.8;
        else if (command.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            score += 0.6;

        // Alias matching
        if (command.Aliases != null)
        {
            foreach (var alias in command.Aliases)
            {
                if (alias.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                    score += 0.7;
                else if (alias.Contains(input, StringComparison.OrdinalIgnoreCase))
                    score += 0.5;
            }
        }

        // Description matching
        if (command.ShortDescription.Contains(input, StringComparison.OrdinalIgnoreCase))
            score += 0.3;
        if (command.LongDescription.Contains(input, StringComparison.OrdinalIgnoreCase))
            score += 0.2;

        // Category boost
        if (command.Category.Contains(input, StringComparison.OrdinalIgnoreCase))
            score += 0.1;

        return score;
    }

    /// <summary>
    /// Provides contextual suggestions based on context and input
    /// </summary>
    private CommandSuggestion GetContextualSuggestion(string input, string? context)
    {
        var suggestion = new CommandSuggestion();

        if (context == "file")
        {
            if (input.Contains("search") || input.Contains("find"))
            {
                suggestion.PrimaryCommand = "files search";
                suggestion.Message = "For file searching, use 'files search <pattern>'";
                suggestion.MessageJA = "ファイル検索には「files search <pattern>」を使用してください";
            }
            else if (input.Contains("stat") || input.Contains("size"))
            {
                suggestion.PrimaryCommand = "files stats";
                suggestion.Message = "For directory statistics, use 'files stats [path]'";
                suggestion.MessageJA = "ディレクトリ統計には「files stats [path]」を使用してください";
            }
        }
        else if (context == "log")
        {
            if (input.Contains("view") || input.Contains("show") || input.Contains("read"))
            {
                suggestion.PrimaryCommand = "logs view";
                suggestion.Message = "To view logs, use 'logs view [count]'";
                suggestion.MessageJA = "ログを表示するには「logs view [count]」を使用してください";
            }
            else if (input.Contains("search") || input.Contains("find"))
            {
                suggestion.PrimaryCommand = "logs search";
                suggestion.Message = "To search logs, use 'logs search <pattern>'";
                suggestion.MessageJA = "ログを検索するには「logs search <pattern>」を使用してください";
            }
        }
        else if (context == "config")
        {
            if (input.Contains("show") || input.Contains("view") || input.Contains("display"))
            {
                suggestion.PrimaryCommand = "config show";
                suggestion.Message = "To view configuration, use 'config show [--json]'";
                suggestion.MessageJA = "設定を表示するには「config show [--json]」を使用してください";
            }
            else if (input.Contains("check") || input.Contains("verify") || input.Contains("validate"))
            {
                suggestion.PrimaryCommand = "config verify";
                suggestion.Message = "To verify configuration, use 'config verify [--json]'";
                suggestion.MessageJA = "設定を検証するには「config verify [--json]」を使用してください";
            }
        }
        else
        {
            // General suggestions
            if (input.Contains("help"))
            {
                suggestion.PrimaryCommand = "help";
                suggestion.Message = "For help, use 'help [command]' or just 'help'";
                suggestion.MessageJA = "ヘルプを表示するには「help [command]」または「help」を使用してください";
            }
            else if (input.Contains("start") || input.Contains("run"))
            {
                suggestion.PrimaryCommand = "start";
                suggestion.Message = "To start the automation engine, use 'start [--rules-path <path>]'";
                suggestion.MessageJA = "オートメーションエンジンを開始するには「start [--rules-path <path>]」を使用してください";
            }
            else if (input.Contains("health") || input.Contains("check") || input.Contains("status"))
            {
                suggestion.PrimaryCommand = "health";
                suggestion.Message = "To check system health, use 'health [--json]'";
                suggestion.MessageJA = "システムの健全性を確認するには「health [--json]」を使用してください";
            }
            else
            {
                suggestion.PrimaryCommand = "help";
                suggestion.Message = "Unknown command. Try 'help' to see all available commands.";
                suggestion.MessageJA = "不明なコマンドです。「help」で利用可能なすべてのコマンドを表示してください。";
                suggestion.Confidence = 0.1;
            }
        }

        suggestion.Confidence = suggestion.Confidence == 0 ? 0.5 : suggestion.Confidence;
        return suggestion;
    }
}
