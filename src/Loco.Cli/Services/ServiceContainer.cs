using System;
using Microsoft.Extensions.DependencyInjection;
using Loco.Cli.Commands;
using Loco.Cli.UI;

namespace Loco.Cli.Services;

/// <summary>
/// Dependency injection container for Loco CLI services
/// Registers and provides instances of command handlers and UI services
/// </summary>
public class ServiceContainer
{
    private readonly IServiceProvider _serviceProvider;

    public ServiceContainer()
    {
        var services = new ServiceCollection();

        // Register UI services
        services.AddSingleton<HelpSystem>();
        services.AddSingleton<LocalizationManager>();
        // NOTE: ConsoleUI is a static class and cannot be injected
        // It should be accessed statically where needed

        // Register command services
        services.AddTransient<StartCommand>();
        services.AddTransient<HealthCommand>();
        services.AddTransient<DiagCommand>();
        services.AddTransient<RuleCommand>();
        services.AddTransient<PresetCommand>();
        services.AddTransient<FilesCommand>();
        services.AddTransient<LogsCommand>();
        services.AddTransient<UpdateCommand>();
        services.AddTransient<ResourceCommand>();
        // NOTE: BackupConfigCommand is a static class and cannot be injected
        // It should be accessed statically where needed
        services.AddTransient<SetupCommand>();
        services.AddTransient<VersionCommand>();
        services.AddTransient<TestsCommand>();
        services.AddTransient<IacCommand>();
        services.AddTransient<WorkflowCommand>();
        services.AddTransient<InteractiveCommand>();

        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Gets a service instance by type
    /// </summary>
    /// <typeparam name="T">Service type to retrieve</typeparam>
    /// <returns>Service instance</returns>
    public T GetService<T>() where T : notnull
    {
        var service = _serviceProvider.GetService<T>();
        if (service == null)
        {
            throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered");
        }
        return service;
    }

    /// <summary>
    /// Gets a service instance by type (nullable)
    /// </summary>
    /// <typeparam name="T">Service type to retrieve</typeparam>
    /// <returns>Service instance or null if not registered</returns>
    public T? GetServiceOrNull<T>() where T : class
    {
        return _serviceProvider.GetService<T>();
    }
}
