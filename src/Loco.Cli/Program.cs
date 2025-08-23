using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using Loco.Automation.Interfaces;
using Loco.Automation.Services;
using Loco.Cli.Commands;
using Loco.Core;
using Loco.Core.Interfaces;
using Loco.Core.Plugins;
using Loco.Core.FlowComposer;
using Loco.Core.Services;
using Loco.Core.Repository;
using Loco.Llm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Loco.Core.Utilities;

namespace Loco.Cli;

/// <summary>
/// Loco CLI - 自動化コマンドラインインターフェース
/// Rob Pike's simplicity principle with pragmatic functionality
/// </summary>
public class Program
{
    private static ILocalizationService _loc = new LocalizationService();

    public static Option<string> PluginsPathOption { get; private set; }
    public static Option<string> ModelIdOption { get; private set; }


    static int Main(string[] args)
    {
        // Initialize localization
        // Language initialization needs to be synchronous or handled differently.
        // For now, we'll proceed without it to test command handling.
        // var requestedLang = GetLangFromArgs(args)
        //     ?? Environment.GetEnvironmentVariable("LOCO_LANG")
        //     ?? CultureInfo.CurrentUICulture.Name;
        // _loc.SetLanguageAsync(requestedLang).GetAwaiter().GetResult();

        var rootCommand = new RootCommand("Loco - AI自動化プラットフォーム CLI");

        var rulesOption = new Option<string>(
            new[] { "--rules", "-r" },
            "Path to a rules file or directory containing rules files.");
        rootCommand.AddOption(rulesOption); // Still a local option for root

        PluginsPathOption = new Option<string>(
            name: "--plugins-path",
            description: $"Path to a directory containing plugin assemblies. If omitted, uses {PluginPaths.PluginsPathEnvVarName} when set; otherwise defaults to %APPDATA%/Loco/Plugins. / プラグインDLLのディレクトリ。省略時は環境変数 {PluginPaths.PluginsPathEnvVarName} が設定されていればそれを、なければ %APPDATA%/Loco/Plugins を使用します。");
        PluginsPathOption.AddAlias("-p");
        rootCommand.AddGlobalOption(PluginsPathOption);

        ModelIdOption = new Option<string>(
            name: "--model-id",
            description: "Stable LLM model ID to inject into llmQuery actions / llmQuery に注入する安定モデルID");
        ModelIdOption.AddAlias("-m");
        rootCommand.AddGlobalOption(ModelIdOption);


        rootCommand.AddCommand(new TestPluginCommand());

        var langOption = new Option<string>(new[] { "--lang", "-l" }, "言語コード (例: ja, en, fr)");
        rootCommand.AddGlobalOption(langOption);
        
        rootCommand.AddCommand(new StartCommand());

        // Plugins-path command - 有効なプラグインディレクトリを表示
        rootCommand.AddCommand(new PluginsPathCommand());

        // Build command - フロービルダー
        rootCommand.AddCommand(new BuildCommand());
        
        // Quick build command - 素早くフローを作成
        rootCommand.AddCommand(new QuickCommand());
        
        // Execute command
        rootCommand.AddCommand(new ExecuteCommand());
        
        // Convert natural language command
        rootCommand.AddCommand(new ConvertCommand());
        
        // Validate command
        rootCommand.AddCommand(new ValidateCommand());
        
        // List command
        rootCommand.AddCommand(new ListCommand());
        
        // Components command - コンポーネント一覧
        rootCommand.AddCommand(new ComponentsCommand());
        
        // Template command - テンプレート管理
        rootCommand.AddCommand(new TemplateCommand());
        
        // marketplace: removed in OSS edition
        
        // share/import/export: removed in OSS edition
        
        // Version command
        rootCommand.AddCommand(new VersionCommand());
        
        // LLM config command - 設定の表示など
        rootCommand.AddCommand(new LlmConfigCommand());

        var builder = new CommandLineBuilder(rootCommand);
        builder.UseHost(CreateHostBuilder, (host) => {
            host.InitializeLocalization(_loc);
        });
        builder.UseDefaults();
        var parser = builder.Build();

        return parser.Invoke(args);
    }
    
        public static IHostBuilder CreateHostBuilder(string[]? args, string? pluginsPath = null)
    {
        // Load .env if present and prime env from preset to minimize required config
        DotEnvLoader.Load();
        LlmConfigurationEnv.PrimeEnvironmentFromPreset();
        return Host.CreateDefaultBuilder(args ?? Array.Empty<string>())
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables(prefix: "LOCO_");
                if (args != null)
                {
                    config.AddCommandLine(args);
                }
            })
            .ConfigureServices((hostContext, services) =>
            {
                var configuration = hostContext.Configuration;

                // Core services
                services.AddSingleton<Core.Factories.ITriggerFactory, Core.Factories.TriggerFactory>();
                services.AddSingleton<Automation.Interfaces.IRuleStore, Automation.Services.DatabaseRuleStore>();
                services.AddSingleton<IAutomationRuleEngine, AutomationRuleEngine>();
                services.AddSingleton<IAutomationService, AutomationService>();
                services.AddSingleton<IFlowEngine, FlowEngine>();
                services.AddSingleton<ILocalizationService>(_loc);
                services.AddSingleton<NaturalLanguageToDslConverter>();
                services.AddSingleton<INaturalLanguageRuleService, NaturalLanguageRuleService>();
                services.AddSingleton<IRuleManipulationService, RuleManipulationService>();

                services.AddSingleton<FlowComposerBuilder>();

                services.AddScoped<IUnitOfWork>(sp =>
                {
                    var connectionString = configuration.GetConnectionString("DefaultConnection");
                    if (string.IsNullOrWhiteSpace(connectionString))
                    {
                        connectionString = $"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Loco", "loco.db")}";
                    }

                    return new UnitOfWork(
                        connectionString,
                        sp.GetRequiredService<ILoggerFactory>()
                    );
                });


                // Plugin System (modern)
                // Honor explicit CLI option --plugins-path/-p by reading from configuration
                // (command-line args were added via config.AddCommandLine(args) above).
                // Precedence: explicit CLI value > env var > default.
                var providedPluginsPath =
                    configuration["plugins-path"]
                    ?? configuration["p"]
                    ?? pluginsPath; // legacy param fallback (normally null)
                var effectivePluginsPath = PluginPaths.GetEffectivePluginsDirectory(providedPluginsPath);
                services.AddSingleton(sp =>
                    new PluginManager(
                        sp.GetRequiredService<ILogger<PluginManager>>(),
                        pluginsDirectory: effectivePluginsPath
                    ));

                services.AddLogging(builder =>
                {
                    builder.AddConfiguration(configuration.GetSection("Logging"));
                    builder.AddConsole();
                });

                // LLM options from configuration
                services.AddOptions<LlmConfiguration>()
                    .Bind(configuration.GetSection("Llm"));
                // Legacy env var fallbacks centralized
                services.PostConfigure<LlmConfiguration>(options =>
                {
                    LlmConfigurationEnv.ApplyEnvironmentVariables(options);
                });

                // ILlmService with HttpClient
                services.AddHttpClient<ILlmService, LlmService>();
            });
    }

    




    
    
    
    
    
    
}
