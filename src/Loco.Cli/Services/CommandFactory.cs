using System;
using System.Threading.Tasks;
using Loco.Cli.Commands;

namespace Loco.Cli.Services;

/// <summary>
/// Factory for creating and executing commands using dependency injection
/// Provides centralized command instantiation and execution
/// </summary>
public class CommandFactory
{
    private readonly ServiceContainer _container;

    public CommandFactory(ServiceContainer container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }

    /// <summary>
    /// Executes a command by name with the given arguments
    /// </summary>
    /// <param name="commandName">Name of the command to execute</param>
    /// <param name="args">Arguments to pass to the command</param>
    /// <returns>Exit code from the command</returns>
    public async Task<int> ExecuteAsync(string commandName, string[] args)
    {
        return commandName switch
        {
            "start" => await _container.GetService<StartCommand>().InvokeAsync(args),
            "health" => await _container.GetService<HealthCommand>().InvokeAsync(args),
            "diag" or "diagnostics" => await _container.GetService<DiagCommand>().InvokeAsync(args),
            "rule" => await _container.GetService<RuleCommand>().InvokeAsync(args),
            "preset" => await _container.GetService<PresetCommand>().InvokeAsync(args),
            "files" => await _container.GetService<FilesCommand>().InvokeAsync(args),
            "logs" => await _container.GetService<LogsCommand>().InvokeAsync(args),
            "update" or "check-update" => await _container.GetService<UpdateCommand>().InvokeAsync(args),
            "resource" or "resources" => await _container.GetService<ResourceCommand>().InvokeAsync(args),
            "backup-config" or "config-backup" => await BackupConfigCommand.ExecuteAsync(args),
            "setup" => await _container.GetService<SetupCommand>().ExecuteAsync(args),
            "version" => await _container.GetService<VersionCommand>().ExecuteAsync(args),
            "test" or "tests" => await _container.GetService<TestsCommand>().InvokeAsync(args),
            "iac" or "infrastructure" => await _container.GetService<IacCommand>().InvokeAsync(args),
            "workflow" or "wf" => await _container.GetService<WorkflowCommand>().InvokeAsync(args),
            "interactive" or "i" => await _container.GetService<InteractiveCommand>().InvokeAsync(args),
            _ => throw new CommandNotFoundException($"Unknown command: {commandName}")
        };
    }
}

/// <summary>
/// Exception thrown when a command is not found
/// </summary>
public class CommandNotFoundException : Exception
{
    public CommandNotFoundException(string message) : base(message)
    {
    }
}
