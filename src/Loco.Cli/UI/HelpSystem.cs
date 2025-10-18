using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Loco.Cli.UI
{
    /// <summary>
    /// Enhanced help system with contextual help, examples, and search
    /// </summary>
    public class HelpSystem
    {
        private readonly Dictionary<string, CommandHelp> _commands;
        private readonly Dictionary<string, string> _aliases;

        public HelpSystem()
        {
            _commands = new Dictionary<string, CommandHelp>();
            _aliases = new Dictionary<string, string>();
            InitializeCommands();
        }

        private void InitializeCommands()
        {
            // Setup command
            AddCommand("setup", new CommandHelp
            {
                Name = "setup",
                Category = "Setup & Core",
                ShortDescription = "Run interactive setup wizard",
                LongDescription = "Guides you through initial configuration with automatic system detection",
                Usage = "Loco.Cli.exe setup",
                Examples = new[]
                {
                    "Loco.Cli.exe setup"
                },
                SeeAlso = new[] { "config", "info" }
            });

            // Start command
            AddCommand("start", new CommandHelp
            {
                Name = "start",
                Category = "Setup & Core",
                ShortDescription = "Start the automation engine",
                LongDescription = "Starts the automation engine and runs all enabled rules",
                Usage = "Loco.Cli.exe start [--rules-path <path>]",
                Options = new Dictionary<string, string>
                {
                    ["--rules-path"] = "Path to rules file (default: auto-detect)"
                },
                Examples = new[]
                {
                    "Loco.Cli.exe start",
                    "Loco.Cli.exe start --rules-path C:\\rules.json"
                },
                SeeAlso = new[] { "test", "health" }
            });

            // Health command
            AddCommand("health", new CommandHelp
            {
                Name = "health",
                Category = "Monitoring",
                ShortDescription = "Check system health status",
                LongDescription = "Performs comprehensive health checks on engine, memory, disk, and configuration",
                Usage = "Loco.Cli.exe health [--json] [--rules-path <path>]",
                Options = new Dictionary<string, string>
                {
                    ["--json"] = "Output results in JSON format",
                    ["--rules-path"] = "Path to rules file"
                },
                Examples = new[]
                {
                    "Loco.Cli.exe health",
                    "Loco.Cli.exe health --json",
                    "Loco.Cli.exe health --json > health.json"
                },
                SeeAlso = new[] { "diag", "info", "test" }
            });

            // Diagnostic command
            AddCommand("diag", new CommandHelp
            {
                Name = "diag",
                Aliases = new[] { "diagnostics" },
                Category = "Monitoring",
                ShortDescription = "Generate comprehensive diagnostics report",
                LongDescription = "Creates detailed diagnostic report with system info, logs, and configuration",
                Usage = "Loco.Cli.exe diag [output_path]",
                Examples = new[]
                {
                    "Loco.Cli.exe diag",
                    "Loco.Cli.exe diag C:\\reports\\diagnostic.txt"
                },
                SeeAlso = new[] { "health", "info", "logs" }
            });

            // Rule command
            AddCommand("rule", new CommandHelp
            {
                Name = "rule",
                Category = "Automation",
                ShortDescription = "Manage automation rules",
                LongDescription = "Create, list, enable, disable, or delete automation rules",
                Usage = "Loco.Cli.exe rule <list|enable|disable|delete> [options]",
                SubCommands = new Dictionary<string, string>
                {
                    ["list"] = "List all rules",
                    ["enable"] = "Enable a rule",
                    ["disable"] = "Disable a rule",
                    ["delete"] = "Delete a rule"
                },
                Options = new Dictionary<string, string>
                {
                    ["--json"] = "Output in JSON format (list only)",
                    ["--rules-path"] = "Path to rules file"
                },
                Examples = new[]
                {
                    "Loco.Cli.exe rule list",
                    "Loco.Cli.exe rule list --json",
                    "Loco.Cli.exe rule enable rule-123",
                    "Loco.Cli.exe rule disable rule-123",
                    "Loco.Cli.exe rule delete rule-123"
                },
                SeeAlso = new[] { "preset", "start" }
            });

            // Preset command
            AddCommand("preset", new CommandHelp
            {
                Name = "preset",
                Category = "Automation",
                ShortDescription = "Run preset automation workflows",
                LongDescription = "Execute pre-configured automation workflows for common tasks",
                Usage = "Loco.Cli.exe preset <list|system|daily|cleanup> [--rules-path <path>]",
                SubCommands = new Dictionary<string, string>
                {
                    ["list"] = "List available presets",
                    ["system"] = "System monitoring (memory, disk)",
                    ["daily"] = "Daily maintenance routine",
                    ["cleanup"] = "Temporary file cleanup"
                },
                Examples = new[]
                {
                    "Loco.Cli.exe preset list",
                    "Loco.Cli.exe preset system",
                    "Loco.Cli.exe preset daily",
                    "Loco.Cli.exe preset cleanup"
                },
                SeeAlso = new[] { "rule", "schedule", "workflow" }
            });

            // Workflow command
            AddCommand("workflow", new CommandHelp
            {
                Name = "workflow",
                Aliases = new[] { "wf" },
                Category = "Automation",
                ShortDescription = "Execute automation workflows from JSON files",
                LongDescription = "Loads and executes workflow definitions from JSON files. Workflows consist of sequential steps that are executed in order. Supports log, delay, file, process, and http actions.",
                Usage = "Loco.Cli.exe workflow <list|stats|info|<file_path>> [options]",
                SubCommands = new Dictionary<string, string>
                {
                    ["list"] = "List all available workflows",
                    ["stats"] = "Show execution statistics",
                    ["info <file>"] = "Show workflow details"
                },
                Options = new Dictionary<string, string>
                {
                    ["--dry-run"] = "Validate without executing",
                    ["--verbose, -v"] = "Show detailed logs",
                    ["--var name=value"] = "Set workflow variable",
                    ["--output <file>"] = "Save execution summary to file",
                    ["--report"] = "Show detailed execution report"
                },
                Examples = new[]
                {
                    "Loco.Cli.exe workflow list",
                    "Loco.Cli.exe workflow stats",
                    "Loco.Cli.exe workflow info workflows/hello-world.json",
                    "Loco.Cli.exe workflow workflows/hello-world.json",
                    "Loco.Cli.exe workflow workflows/hello-world.json --dry-run",
                    "Loco.Cli.exe workflow workflows/backup.json --var source=C:\\data --var dest=C:\\backup",
                    "Loco.Cli.exe workflow workflows/hello-world.json --output execution.log",
                    "Loco.Cli.exe workflow workflows/hello-world.json --report",
                    "Loco.Cli.exe wf workflows/process-test.json -v"
                },
                SeeAlso = new[] { "preset", "rule", "start" }
            });

            // Files command
            AddCommand("files", new CommandHelp
            {
                Name = "files",
                Category = "File Operations",
                ShortDescription = "File operations and search",
                LongDescription = "Search for files, show directory statistics, and manage file operations",
                Usage = "Loco.Cli.exe files <search|stats> [options]",
                SubCommands = new Dictionary<string, string>
                {
                    ["search"] = "Search for files by pattern",
                    ["stats"] = "Show directory statistics"
                },
                Examples = new[]
                {
                    "Loco.Cli.exe files search \"*.txt\"",
                    "Loco.Cli.exe files search \"*.cs\" src/",
                    "Loco.Cli.exe files stats Downloads/"
                },
                SeeAlso = new[] { "backup", "watch" }
            });

            // Logs command
            AddCommand("logs", new CommandHelp
            {
                Name = "logs",
                Category = "Monitoring",
                ShortDescription = "Log management and viewing",
                LongDescription = "View, search, analyze, and clear application logs",
                Usage = "Loco.Cli.exe logs <view|stats|search|clear> [options]",
                SubCommands = new Dictionary<string, string>
                {
                    ["view"] = "View recent log entries",
                    ["stats"] = "Show log statistics",
                    ["search"] = "Search logs for pattern",
                    ["clear"] = "Clear all log files"
                },
                Examples = new[]
                {
                    "Loco.Cli.exe logs view 100",
                    "Loco.Cli.exe logs stats",
                    "Loco.Cli.exe logs search ERROR",
                    "Loco.Cli.exe logs clear --confirm"
                },
                SeeAlso = new[] { "diag", "health" }
            });

            // Interactive mode
            AddCommand("interactive", new CommandHelp
            {
                Name = "interactive",
                Aliases = new[] { "i" },
                Category = "Setup & Core",
                ShortDescription = "Enter interactive mode",
                LongDescription = "Starts an interactive shell with command history and auto-completion",
                Usage = "Loco.Cli.exe interactive [--rules-path <path>]",
                Examples = new[]
                {
                    "Loco.Cli.exe interactive",
                    "Loco.Cli.exe i"
                },
                SeeAlso = new[] { "help" }
            });

            // Update command
            AddCommand("update", new CommandHelp
            {
                Name = "update",
                Aliases = new[] { "check-update" },
                Category = "Enterprise",
                ShortDescription = "Check for available updates",
                LongDescription = "Checks for software updates (privacy-safe, offline-friendly). Returns exit code 2 for critical updates.",
                Usage = "Loco.Cli.exe update",
                Examples = new[]
                {
                    "Loco.Cli.exe update",
                    "Loco.Cli.exe check-update"
                },
                SeeAlso = new[] { "version", "health" }
            });

            // Resource command
            AddCommand("resource", new CommandHelp
            {
                Name = "resource",
                Aliases = new[] { "resources" },
                Category = "Enterprise",
                ShortDescription = "Monitor system resources",
                LongDescription = "Real-time resource monitoring (memory, CPU, threads, handles). Use 'watch' for continuous monitoring.",
                Usage = "Loco.Cli.exe resource [watch] [interval_seconds]",
                SubCommands = new Dictionary<string, string>
                {
                    ["watch"] = "Continuous monitoring mode (default 5s interval)"
                },
                Examples = new[]
                {
                    "Loco.Cli.exe resource",
                    "Loco.Cli.exe resource watch",
                    "Loco.Cli.exe resource watch 10"
                },
                SeeAlso = new[] { "health", "diag", "monitor" }
            });

            // Backup-config command
            AddCommand("backup-config", new CommandHelp
            {
                Name = "backup-config",
                Aliases = new[] { "config-backup" },
                Category = "Enterprise",
                ShortDescription = "Manage configuration backups",
                LongDescription = "Create, restore, and manage configuration backups (ZIP format, max 10 backups, 24h auto-backup).",
                Usage = "Loco.Cli.exe backup-config <create|list|restore|delete|clear|auto> [options]",
                SubCommands = new Dictionary<string, string>
                {
                    ["create"] = "Create a new backup",
                    ["list"] = "List all backups",
                    ["restore"] = "Restore from backup (with pre-restore backup)",
                    ["delete"] = "Delete a specific backup",
                    ["clear"] = "Delete all backups",
                    ["auto"] = "Automatic backup (24h interval check)"
                },
                Examples = new[]
                {
                    "Loco.Cli.exe backup-config create \"Before upgrade\"",
                    "Loco.Cli.exe backup-config list",
                    "Loco.Cli.exe backup-config restore 1",
                    "Loco.Cli.exe backup-config auto"
                },
                SeeAlso = new[] { "config", "backup" }
            });

            // Version command
            AddCommand("version", new CommandHelp
            {
                Name = "version",
                Category = "Setup & Core",
                ShortDescription = "Show version and system information",
                LongDescription = "Displays comprehensive version, platform, quality metrics, and compliance information.",
                Usage = "Loco.Cli.exe version",
                Examples = new[]
                {
                    "Loco.Cli.exe version"
                },
                SeeAlso = new[] { "update", "info" }
            });

            // Add aliases
            AddAlias("i", "interactive");
            AddAlias("diagnostics", "diag");
            AddAlias("check-update", "update");
            AddAlias("resources", "resource");
            AddAlias("config-backup", "backup-config");
            AddAlias("wf", "workflow");
        }

        public void AddCommand(string name, CommandHelp help)
        {
            _commands[name.ToLower()] = help;

            // Add aliases
            if (help.Aliases != null)
            {
                foreach (var alias in help.Aliases)
                {
                    AddAlias(alias, name);
                }
            }
        }

        public void AddAlias(string alias, string commandName)
        {
            _aliases[alias.ToLower()] = commandName.ToLower();
        }

        public string? GetCommandName(string nameOrAlias)
        {
            var key = nameOrAlias.ToLower();
            if (_commands.ContainsKey(key))
                return key;
            if (_aliases.ContainsKey(key))
                return _aliases[key];
            return null;
        }

        public void ShowHelp(string? commandName = null)
        {
            if (string.IsNullOrEmpty(commandName))
            {
                ShowAllCommands();
            }
            else
            {
                var resolvedName = GetCommandName(commandName);
                if (resolvedName != null && _commands.TryGetValue(resolvedName, out var help))
                {
                    ShowCommandHelp(help);
                }
                else
                {
                    ConsoleUI.Error($"Unknown command: {commandName}");
                    Console.WriteLine("\nRun 'Loco.Cli.exe help' to see all available commands.");
                }
            }
        }

        private void ShowAllCommands()
        {
            ConsoleUI.SectionHeader("Loco CLI - Enterprise Automation Platform", '═');

            Console.WriteLine("\nUsage: Loco.Cli.exe <command> [options]");
            Console.WriteLine();

            var categories = _commands.Values
                .GroupBy(c => c.Category)
                .OrderBy(g => g.Key);

            foreach (var category in categories)
            {
                Console.ForegroundColor = ConsoleUI.Colors.Primary;
                Console.WriteLine($"{category.Key.ToUpper()}:");
                Console.ResetColor();

                foreach (var cmd in category.OrderBy(c => c.Name))
                {
                    Console.ForegroundColor = ConsoleUI.Colors.Highlight;
                    Console.Write($"  {cmd.Name,-15}");
                    Console.ResetColor();
                    Console.WriteLine($" {cmd.ShortDescription}");

                    if (cmd.Aliases != null && cmd.Aliases.Length > 0)
                    {
                        Console.ForegroundColor = ConsoleUI.Colors.Muted;
                        Console.WriteLine($"    Aliases: {string.Join(", ", cmd.Aliases)}");
                        Console.ResetColor();
                    }
                }
                Console.WriteLine();
            }

            Console.WriteLine("For detailed help on a specific command:");
            Console.WriteLine("  Loco.Cli.exe help <command>");
            Console.WriteLine();

            Console.WriteLine("Quick Start:");
            Console.ForegroundColor = ConsoleUI.Colors.Info;
            Console.WriteLine("  1. Loco.Cli.exe setup        # First-time setup");
            Console.WriteLine("  2. Loco.Cli.exe health       # Verify installation");
            Console.WriteLine("  3. Loco.Cli.exe preset system # Run system check");
            Console.WriteLine("  4. Loco.Cli.exe interactive  # Explore interactively");
            Console.ResetColor();
            Console.WriteLine();
        }

        private void ShowCommandHelp(CommandHelp help)
        {
            ConsoleUI.SectionHeader($"Command: {help.Name}", '═');

            Console.WriteLine();
            Console.WriteLine(help.LongDescription);
            Console.WriteLine();

            // Usage
            Console.ForegroundColor = ConsoleUI.Colors.Primary;
            Console.WriteLine("USAGE:");
            Console.ResetColor();
            Console.WriteLine($"  {help.Usage}");
            Console.WriteLine();

            // Aliases
            if (help.Aliases != null && help.Aliases.Length > 0)
            {
                Console.ForegroundColor = ConsoleUI.Colors.Primary;
                Console.WriteLine("ALIASES:");
                Console.ResetColor();
                Console.WriteLine($"  {string.Join(", ", help.Aliases)}");
                Console.WriteLine();
            }

            // Sub-commands
            if (help.SubCommands != null && help.SubCommands.Count > 0)
            {
                Console.ForegroundColor = ConsoleUI.Colors.Primary;
                Console.WriteLine("SUB-COMMANDS:");
                Console.ResetColor();
                foreach (var sub in help.SubCommands)
                {
                    Console.WriteLine($"  {sub.Key,-15} {sub.Value}");
                }
                Console.WriteLine();
            }

            // Options
            if (help.Options != null && help.Options.Count > 0)
            {
                Console.ForegroundColor = ConsoleUI.Colors.Primary;
                Console.WriteLine("OPTIONS:");
                Console.ResetColor();
                foreach (var opt in help.Options)
                {
                    Console.WriteLine($"  {opt.Key,-20} {opt.Value}");
                }
                Console.WriteLine();
            }

            // Examples
            if (help.Examples != null && help.Examples.Length > 0)
            {
                Console.ForegroundColor = ConsoleUI.Colors.Primary;
                Console.WriteLine("EXAMPLES:");
                Console.ResetColor();
                foreach (var example in help.Examples)
                {
                    Console.ForegroundColor = ConsoleUI.Colors.Muted;
                    Console.WriteLine($"  {example}");
                    Console.ResetColor();
                }
                Console.WriteLine();
            }

            // See also
            if (help.SeeAlso != null && help.SeeAlso.Length > 0)
            {
                Console.ForegroundColor = ConsoleUI.Colors.Primary;
                Console.WriteLine("SEE ALSO:");
                Console.ResetColor();
                Console.WriteLine($"  {string.Join(", ", help.SeeAlso)}");
                Console.WriteLine();
            }
        }

        public string[] SearchCommands(string query)
        {
            return _commands.Values
                .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           c.ShortDescription.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           c.LongDescription.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Name)
                .ToArray();
        }

        /// <summary>
        /// インテリジェントなコマンド提案を提供
        /// </summary>
        public CommandSuggestion GetSmartSuggestion(string userInput, string? context = null)
        {
            var suggestion = new CommandSuggestion();
            var input = userInput.ToLower().Trim();

            // 空の入力の場合
            if (string.IsNullOrEmpty(input))
            {
                suggestion.PrimaryCommand = "help";
                suggestion.Message = "Type 'help' to see all available commands";
                suggestion.MessageJA = "すべての利用可能なコマンドを見るには 'help' と入力してください";
                suggestion.Confidence = 1.0;
                return suggestion;
            }

            // 完全一致の場合
            var resolved = GetCommandName(input);
            if (resolved != null && _commands.TryGetValue(resolved, out var cmd))
            {
                suggestion.PrimaryCommand = cmd.Name;
                suggestion.Message = $"Did you mean '{cmd.Name}'? {cmd.ShortDescription}";
                suggestion.MessageJA = $"「{cmd.Name}」のことですか？ {cmd.ShortDescription}";
                suggestion.Confidence = 1.0;
                return suggestion;
            }

            // 部分一致で最適なものを探す
            var candidates = _commands.Values
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
                // コンテキストに基づく提案
                suggestion = GetContextualSuggestion(input, context);
            }

            return suggestion;
        }

        private double CalculateMatchScore(string input, CommandHelp command)
        {
            double score = 0;

            // コマンド名の一致度
            if (command.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                score += 0.8;
            else if (command.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
                score += 0.6;

            // エイリアスの一致度
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

            // 説明の一致度
            if (command.ShortDescription.Contains(input, StringComparison.OrdinalIgnoreCase))
                score += 0.3;
            if (command.LongDescription.Contains(input, StringComparison.OrdinalIgnoreCase))
                score += 0.2;

            // カテゴリによるブースト
            if (command.Category.Contains(input, StringComparison.OrdinalIgnoreCase))
                score += 0.1;

            return score;
        }

        private CommandSuggestion GetContextualSuggestion(string input, string? context)
        {
            var suggestion = new CommandSuggestion();

            // コンテキストに基づく提案
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
                // 一般的な提案
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

        /// <summary>
        /// コマンド使用のヒントを表示
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

    /// <summary>
    /// スマートなコマンド提案
    /// </summary>
    public class CommandSuggestion
    {
        public string PrimaryCommand { get; set; } = "";
        public string Message { get; set; } = "";
        public string MessageJA { get; set; } = "";
        public double Confidence { get; set; } = 0.0;
        public string[] Alternatives { get; set; } = Array.Empty<string>();
    }
    }

    public class CommandHelp
    {
        public string Name { get; set; } = "";
        public string[] Aliases { get; set; } = Array.Empty<string>();
        public string Category { get; set; } = "General";
        public string ShortDescription { get; set; } = "";
        public string LongDescription { get; set; } = "";
        public string Usage { get; set; } = "";
        public Dictionary<string, string>? SubCommands { get; set; }
        public Dictionary<string, string>? Options { get; set; }
        public string[] Examples { get; set; } = Array.Empty<string>();
        public string[] SeeAlso { get; set; } = Array.Empty<string>();
    }
