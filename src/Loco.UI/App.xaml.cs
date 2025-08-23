using System;
using System.Windows;
using System.Globalization;
using System.Windows.Markup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Loco.Core;
using Loco.Core.Models;
using Loco.Core.Services;
using Loco.Core.Repository;
using Loco.Automation.Services;
using Loco.Automation.Interfaces;
using Loco.UI.Themes;
using Loco.Llm;
using Loco.Core.Utilities;

namespace Loco.UI;

/// <summary>
/// WPF Application entry point
/// </summary>
public partial class App : Application
{
    private IServiceProvider _serviceProvider;
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load .env if present and prime env from preset to minimize required config
        DotEnvLoader.Load();
        LlmConfigurationEnv.PrimeEnvironmentFromPreset();

        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        
        // Apply saved theme preference early
        try
        {
            var settings = _serviceProvider.GetRequiredService<Services.SettingsService>();
            var themeName = settings.Theme;
            ThemeManager.CurrentTheme = string.Equals(themeName, nameof(Theme.Light), StringComparison.OrdinalIgnoreCase)
                ? Theme.Light
                : Theme.Dark;
        }
        catch
        {
            // Fallback to default if settings not available
            ThemeManager.CurrentTheme = Theme.Dark;
        }
        
        // Initialize localization
        var loc = _serviceProvider.GetRequiredService<ILocalizationService>();
        var requestedLang = Environment.GetEnvironmentVariable("LOCO_LANG")
                              ?? CultureInfo.CurrentUICulture.Name;
        loc.SetLanguageAsync(requestedLang).GetAwaiter().GetResult();
        // Reflect WPF language metadata
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(loc.CurrentCulture.IetfLanguageTag)));
        
        // Show main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
    
    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        // Core services
        services.AddSingleton<IFlowEngine, FlowEngine>();
        services.AddSingleton<IAutomationRuleEngine, AutomationRuleEngine>();
        services.AddSingleton<IRuleStore, DatabaseRuleStore>();
        services.AddSingleton<IAutomationService, AutomationService>(sp =>
            new AutomationService(
                sp.GetRequiredService<ILogger<AutomationService>>(),
                sp.GetRequiredService<IAutomationRuleEngine>(),
                sp.GetRequiredService<IRuleStore>()
            ));
        services.AddSingleton<ILocalizationService>(sp => new LocalizationService());
        services.AddSingleton<SandboxExecutor>();
        services.AddSingleton<INaturalLanguageRuleService, NaturalLanguageRuleService>();
        services.AddSingleton<LlmModelManager>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<LlmModelManager>>();
            var httpClient = new System.Net.Http.HttpClient();
            var modelsPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Loco",
                "models");
            return new LlmModelManager(logger, httpClient, modelsPath);
        });
        services.AddSingleton<NaturalLanguageToDslConverter>();

        services.AddScoped<IUnitOfWork>(sp => 
        {
            var settingsService = sp.GetRequiredService<Services.SettingsService>();
            return new UnitOfWork(
                settingsService.CurrentSettings.DatabaseConnectionString, 
                sp.GetRequiredService<ILoggerFactory>()
            );
        });

        // LLM options from environment variables for UI host (centralized helper)
        services.AddOptions<LlmConfiguration>().Configure(options =>
        {
            LlmConfigurationEnv.ApplyEnvironmentVariables(options);
        });

        // ILlmService typed client
        services.AddHttpClient<ILlmService, LlmService>();
        
        // UI
        services.AddSingleton<MainWindow>();
        services.AddSingleton<Services.IValidationService, Services.ValidationService>();
        services.AddSingleton<Services.SettingsService>();
    }
    
    protected override void OnExit(ExitEventArgs e)
    {
        // Clean up
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        base.OnExit(e);
    }
}