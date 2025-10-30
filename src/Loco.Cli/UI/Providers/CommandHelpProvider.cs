using System;
using System.Collections.Generic;
using Loco.Cli.UI.Models;

namespace Loco.Cli.UI.Providers;

/// <summary>
/// Manages command metadata and initialization
/// Provides a registry of all available commands with their help information
/// </summary>
public class CommandHelpProvider
{
    private readonly Dictionary<string, CommandHelp> _commands;
    private readonly Dictionary<string, string> _aliases;

    public CommandHelpProvider()
    {
        _commands = new Dictionary<string, CommandHelp>();
        _aliases = new Dictionary<string, string>();
        InitializeCommands();
    }

    /// <summary>
    /// Gets all registered commands
    /// </summary>
    public IReadOnlyDictionary<string, CommandHelp> Commands => _commands;

    /// <summary>
    /// Adds a command to the registry
    /// </summary>
    public void AddCommand(string name, CommandHelp help)
    {
        _commands[name.ToLowerInvariant()] = help;

        // Register aliases
        foreach (var alias in help.Aliases)
        {
            AddAlias(alias, name);
        }
    }

    /// <summary>
    /// Adds an alias for a command
    /// </summary>
    public void AddAlias(string alias, string commandName)
    {
        _aliases[alias.ToLowerInvariant()] = commandName.ToLowerInvariant();
    }

    /// <summary>
    /// Resolves a command name from name or alias
    /// </summary>
    public string? GetCommandName(string nameOrAlias)
    {
        var key = nameOrAlias.ToLowerInvariant();

        // Check if it's a direct command name
        if (_commands.ContainsKey(key))
        {
            return key;
        }

        // Check if it's an alias
        if (_aliases.TryGetValue(key, out var commandName))
        {
            return commandName;
        }

        return null;
    }

    /// <summary>
    /// Gets help for a specific command
    /// </summary>
    public CommandHelp? GetCommandHelp(string nameOrAlias)
    {
        var commandName = GetCommandName(nameOrAlias);
        if (commandName != null && _commands.TryGetValue(commandName, out var help))
        {
            return help;
        }
        return null;
    }

    /// <summary>
    /// Initialize all available commands
    /// </summary>
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
            Examples = new[] { "Loco.Cli.exe setup" },
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
            Examples = new[] { "Loco.Cli.exe start", "Loco.Cli.exe start --rules-path C:\\rules.json" },
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
            Examples = new[] { "Loco.Cli.exe health", "Loco.Cli.exe health --json", "Loco.Cli.exe health --json > health.json" },
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
            Examples = new[] { "Loco.Cli.exe diag", "Loco.Cli.exe diag C:\\reports\\diagnostic.txt" },
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
            Options = new Dictionary<string, string>
            {
                ["list"] = "List all rules",
                ["enable"] = "Enable a rule",
                ["disable"] = "Disable a rule",
                ["delete"] = "Delete a rule",
                ["--json"] = "Output in JSON format (list only)",
                ["--rules-path"] = "Path to rules file"
            },
            Examples = new[] { "Loco.Cli.exe rule list", "Loco.Cli.exe rule list --json", "Loco.Cli.exe rule enable rule-123", "Loco.Cli.exe rule disable rule-123", "Loco.Cli.exe rule delete rule-123" },
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
            Options = new Dictionary<string, string>
            {
                ["list"] = "List available presets",
                ["system"] = "System monitoring (memory, disk)",
                ["daily"] = "Daily maintenance routine",
                ["cleanup"] = "Temporary file cleanup"
            },
            Examples = new[] { "Loco.Cli.exe preset list", "Loco.Cli.exe preset system", "Loco.Cli.exe preset daily", "Loco.Cli.exe preset cleanup" },
            SeeAlso = new[] { "rule", "start" }
        });

        // Files command
        AddCommand("files", new CommandHelp
        {
            Name = "files",
            Category = "File Management",
            ShortDescription = "Manage files and folders",
            LongDescription = "Monitor, organize, and manage files with advanced filtering and actions",
            Usage = "Loco.Cli.exe files <list|watch|organize> [options]",
            Options = new Dictionary<string, string>
            {
                ["list"] = "List files matching criteria",
                ["watch"] = "Watch directory for changes",
                ["organize"] = "Organize files by rules"
            },
            Examples = new[] { "Loco.Cli.exe files list C:\\Downloads", "Loco.Cli.exe files watch", "Loco.Cli.exe files organize" },
            SeeAlso = new[] { "rule", "preset" }
        });

        // Logs command
        AddCommand("logs", new CommandHelp
        {
            Name = "logs",
            Category = "Monitoring & Diagnostics",
            ShortDescription = "View and manage application logs",
            LongDescription = "Display, filter, and analyze application and automation logs",
            Usage = "Loco.Cli.exe logs [level] [--since <time>] [--tail <count>]",
            Options = new Dictionary<string, string>
            {
                ["level"] = "Log level: All, Trace, Debug, Information, Warning, Error, Fatal (default: All)",
                ["--since"] = "Show logs since (1h, 1d, etc.)",
                ["--tail"] = "Show last N log entries"
            },
            Examples = new[] { "Loco.Cli.exe logs", "Loco.Cli.exe logs Error", "Loco.Cli.exe logs --tail 50" },
            SeeAlso = new[] { "diag", "health" }
        });

        // Update command
        AddCommand("update", new CommandHelp
        {
            Name = "update",
            Aliases = new[] { "check-update" },
            Category = "Application",
            ShortDescription = "Check for and install updates",
            LongDescription = "Checks for new versions and optionally installs updates automatically",
            Usage = "Loco.Cli.exe update [--auto-install] [--channel <stable|preview>]",
            Options = new Dictionary<string, string>
            {
                ["--auto-install"] = "Install updates without confirmation",
                ["--channel"] = "Update channel: stable (default) or preview"
            },
            Examples = new[] { "Loco.Cli.exe update", "Loco.Cli.exe update --auto-install" },
            SeeAlso = new[] { "version", "help" }
        });

        // Resource command
        AddCommand("resource", new CommandHelp
        {
            Name = "resource",
            Aliases = new[] { "resources" },
            Category = "System",
            ShortDescription = "Monitor system resources",
            LongDescription = "Display CPU, memory, disk, and network resource usage",
            Usage = "Loco.Cli.exe resource [--continuous] [--interval <ms>]",
            Options = new Dictionary<string, string>
            {
                ["--continuous"] = "Continuously monitor resources",
                ["--interval"] = "Update interval in milliseconds (default: 1000)"
            },
            Examples = new[] { "Loco.Cli.exe resource", "Loco.Cli.exe resource --continuous" },
            SeeAlso = new[] { "health", "diag" }
        });

        // Backup-config command
        AddCommand("backup-config", new CommandHelp
        {
            Name = "backup-config",
            Category = "Configuration",
            ShortDescription = "Backup configuration and rules",
            LongDescription = "Create a backup of all configuration, rules, and state files",
            Usage = "Loco.Cli.exe backup-config [--output <path>] [--compress]",
            Options = new Dictionary<string, string>
            {
                ["--output"] = "Backup destination directory",
                ["--compress"] = "Compress backup to ZIP"
            },
            Examples = new[] { "Loco.Cli.exe backup-config", "Loco.Cli.exe backup-config --compress" },
            SeeAlso = new[] { "setup", "diag" }
        });

        // Version command
        AddCommand("version", new CommandHelp
        {
            Name = "version",
            Category = "Application",
            ShortDescription = "Display application version",
            LongDescription = "Shows version, build info, and system details",
            Usage = "Loco.Cli.exe version [--json]",
            Options = new Dictionary<string, string>
            {
                ["--json"] = "Output as JSON"
            },
            Examples = new[] { "Loco.Cli.exe version", "Loco.Cli.exe version --json" },
            SeeAlso = new[] { "update", "help" }
        });

        // Test command
        AddCommand("test", new CommandHelp
        {
            Name = "test",
            Aliases = new[] { "tests" },
            Category = "Development",
            ShortDescription = "Run tests and validations",
            LongDescription = "Execute built-in tests on rules, configuration, and automation logic",
            Usage = "Loco.Cli.exe test [--verbose] [--rules-path <path>]",
            Options = new Dictionary<string, string>
            {
                ["--verbose"] = "Detailed test output",
                ["--rules-path"] = "Path to rules file to test"
            },
            Examples = new[] { "Loco.Cli.exe test", "Loco.Cli.exe test --verbose" },
            SeeAlso = new[] { "start", "diag" }
        });

        // IAC command
        AddCommand("iac", new CommandHelp
        {
            Name = "iac",
            Aliases = new[] { "infrastructure" },
            Category = "DevOps",
            ShortDescription = "Infrastructure-as-Code automation",
            LongDescription = "Deploy and manage infrastructure using declarative automation",
            Usage = "Loco.Cli.exe iac <plan|apply|validate> [--file <path>]",
            Options = new Dictionary<string, string>
            {
                ["plan"] = "Preview changes",
                ["apply"] = "Apply infrastructure changes",
                ["validate"] = "Validate configuration"
            },
            Examples = new[] { "Loco.Cli.exe iac plan", "Loco.Cli.exe iac apply" },
            SeeAlso = new[] { "setup", "diag" }
        });

        // Workflow command
        AddCommand("workflow", new CommandHelp
        {
            Name = "workflow",
            Aliases = new[] { "wf" },
            Category = "Automation",
            ShortDescription = "Create and execute workflows",
            LongDescription = "Define, test, and run complex multi-step automation workflows",
            Usage = "Loco.Cli.exe workflow <create|list|run|delete> [options]",
            Options = new Dictionary<string, string>
            {
                ["create"] = "Create new workflow",
                ["list"] = "List workflows",
                ["run"] = "Run workflow",
                ["delete"] = "Delete workflow"
            },
            Examples = new[] { "Loco.Cli.exe workflow list", "Loco.Cli.exe workflow run backup-daily" },
            SeeAlso = new[] { "rule", "preset" }
        });

        // Interactive command
        AddCommand("interactive", new CommandHelp
        {
            Name = "interactive",
            Aliases = new[] { "i" },
            Category = "Application",
            ShortDescription = "Interactive CLI mode",
            LongDescription = "Launch interactive command-line interface for advanced operations",
            Usage = "Loco.Cli.exe interactive",
            Examples = new[] { "Loco.Cli.exe interactive", "Loco.Cli.exe i" },
            SeeAlso = new[] { "help", "version" }
        });
    }
}
