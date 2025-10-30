using System;
using System.Collections.Generic;
using System.Linq;
using Loco.Cli.UI.Models;

namespace Loco.Cli.UI.Providers;

/// <summary>
/// Formats and displays help information for commands
/// Handles all output formatting and presentation logic
/// </summary>
public class CommandHelpFormatter
{
    /// <summary>
    /// Shows help for all commands or a specific command
    /// </summary>
    public void ShowHelp(string? commandName, CommandHelpProvider provider)
    {
        if (string.IsNullOrEmpty(commandName))
        {
            ShowAllCommands(provider);
        }
        else
        {
            var help = provider.GetCommandHelp(commandName);
            if (help != null)
            {
                ShowCommandHelp(help);
            }
            else
            {
                ConsoleUI.Error(
                    $"Command '{commandName}' not found",
                    $"コマンド「{commandName}」が見つかりません"
                );
            }
        }
    }

    /// <summary>
    /// Shows all available commands organized by category
    /// </summary>
    private void ShowAllCommands(CommandHelpProvider provider)
    {
        Console.WriteLine();
        ConsoleUI.SectionHeader("Available Commands / 利用可能なコマンド", '═');

        var groupedByCategory = provider.Commands.Values
            .GroupBy(c => c.Category)
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var categoryGroup in groupedByCategory)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleUI.Colors.Primary;
            Console.WriteLine($"  {categoryGroup.Key}");
            Console.ResetColor();

            foreach (var cmd in categoryGroup.OrderBy(c => c.Name))
            {
                var aliasInfo = cmd.Aliases.Length > 0
                    ? $" (aliases: {string.Join(", ", cmd.Aliases)})"
                    : "";

                Console.ForegroundColor = ConsoleUI.Colors.Highlight;
                Console.Write($"    {cmd.Name,-16}");
                Console.ResetColor();
                Console.WriteLine($" - {cmd.ShortDescription}{aliasInfo}");
            }
        }

        Console.WriteLine();
        ConsoleUI.Tip(
            "Use 'help <command>' for detailed information",
            "詳細情報は「help <command>」を使用してください"
        );
        Console.WriteLine();
    }

    /// <summary>
    /// Shows detailed help for a specific command
    /// </summary>
    private void ShowCommandHelp(CommandHelp help)
    {
        Console.WriteLine();
        ConsoleUI.SectionHeader($"Command: {help.Name}", '═');

        // Category and aliases
        Console.ForegroundColor = ConsoleUI.Colors.Muted;
        Console.Write("Category:  ");
        Console.ResetColor();
        Console.WriteLine(help.Category);

        if (help.Aliases.Length > 0)
        {
            Console.ForegroundColor = ConsoleUI.Colors.Muted;
            Console.Write("Aliases:   ");
            Console.ResetColor();
            Console.WriteLine(string.Join(", ", help.Aliases));
        }

        // Description
        Console.WriteLine();
        Console.ForegroundColor = ConsoleUI.Colors.Highlight;
        Console.WriteLine("Description:");
        Console.ResetColor();
        Console.WriteLine($"  {help.LongDescription}");

        // Usage
        Console.WriteLine();
        Console.ForegroundColor = ConsoleUI.Colors.Highlight;
        Console.WriteLine("Usage:");
        Console.ResetColor();
        Console.WriteLine($"  {help.Usage}");

        // Options
        if (help.Options != null && help.Options.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleUI.Colors.Highlight;
            Console.WriteLine("Options:");
            Console.ResetColor();
            foreach (var option in help.Options)
            {
                Console.ForegroundColor = ConsoleUI.Colors.Info;
                Console.Write($"  {option.Key,-20}");
                Console.ResetColor();
                Console.WriteLine($" - {option.Value}");
            }
        }

        // Examples
        if (help.Examples != null && help.Examples.Length > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleUI.Colors.Highlight;
            Console.WriteLine("Examples:");
            Console.ResetColor();
            foreach (var example in help.Examples)
            {
                Console.ForegroundColor = ConsoleUI.Colors.Success;
                Console.WriteLine($"  $ {example}");
                Console.ResetColor();
            }
        }

        // See also
        if (help.SeeAlso != null && help.SeeAlso.Length > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleUI.Colors.Muted;
            Console.Write("See also:  ");
            Console.ResetColor();
            Console.WriteLine(string.Join(", ", help.SeeAlso));
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Shows quick tips for common tasks
    /// </summary>
    public void ShowUsageTips()
    {
        Console.WriteLine();
        ConsoleUI.SectionHeader("Quick Tips / クイックヒント", '─');

        var tips = new[]
        {
            ("First time? Run 'setup' for guided configuration", "初めてですか？「setup」でガイド付き設定を実行してください"),
            ("Need help? Use 'help <command>' for detailed info", "ヘルプが必要ですか？「help <command>」で詳細情報を表示できます"),
            ("Check system health with 'health --json'", "「health --json」でシステムの健全性を確認できます"),
            ("View logs with 'logs view 50' (last 50 entries)", "「logs view 50」で最新50件のログを表示できます"),
            ("Search files with 'files search \"*.txt\"'", "「files search \"*.txt\"」でテキストファイルを検索できます"),
            ("Backup config with 'backup-config create \"Before changes\"'", "「backup-config create \"Before changes\"」で設定をバックアップできます")
        };

        foreach (var (en, ja) in tips)
        {
            ConsoleUI.Tip(en, ja);
        }

        Console.WriteLine();
    }
}
