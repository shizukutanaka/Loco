using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Loco.Automation.Interfaces;
using Loco.Automation.Services;
using Loco.Cli.Commands;
using Loco.Core.Models;
using Loco.Core.Plugins;
using Loco.Llm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loco.Cli.Tests;

public class StartCommandTests
{
    [Fact]
    public async Task StartCommand_InjectsModelId_And_AddsRuleFromFile()
    {
        // Arrange: temp plugin dir and rule file
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        Directory.CreateDirectory(pluginsDir);

        var rulePath = Path.Combine(tempRoot, "rule.json");
        var ruleJson = "{" +
                       "\n  \"id\": \"t1\"," +
                       "\n  \"name\": \"test\"," +
                       "\n  \"triggers\": [ { \"type\": \"manual\" } ]," +
                       "\n  \"actions\": [ { \"type\": \"llmQuery\", \"prompt\": \"hi\" } ]" +
                       "\n}";
        await File.WriteAllTextAsync(rulePath, ruleJson);

        var fakeAutomation = new FakeAutomationService();
        var fakeLifetime = new FakeAppLifetime();

        // DI container used by FakeHost.Services
        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationLifetime>(fakeLifetime);
        services.AddSingleton<IAutomationService>(fakeAutomation);
        services.AddSingleton(new PluginManager(null, pluginsDir));
        services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration { Model = "fallback-model" }));
        services.AddSingleton<IRuleManipulationService, RuleManipulationService>();
        var loggerFactory = LoggerFactory.Create(builder => { /* no sinks */ });
        services.AddSingleton<ILogger<StartCommand>>(sp => loggerFactory.CreateLogger<StartCommand>());
        var provider = services.BuildServiceProvider();

        var host = new FakeHost(provider);
        var ruleService = provider.GetRequiredService<IRuleManipulationService>();
        var logger = provider.GetRequiredService<ILogger<StartCommand>>();

        // Act
        await StartCommand.HandleAsync(host, logger, ruleService, rulePath, modelIdArg: "cli-model");

        // Assert
        Assert.True(fakeAutomation.StartCalled);
        Assert.True(fakeAutomation.ValidateCalled);
        Assert.True(fakeAutomation.AddCalled);
        Assert.Contains("\"modelId\": \"cli-model\"", fakeAutomation.LastAddedRuleJson);
    }

    [Fact]
    public async Task StartCommand_LoadsRules_FromDirectory_AddsOnlyValid()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        var rulesDir = Path.Combine(tempRoot, "rules");
        Directory.CreateDirectory(pluginsDir);
        Directory.CreateDirectory(rulesDir);

        var validPath = Path.Combine(rulesDir, "valid.json");
        var invalidPath = Path.Combine(rulesDir, "invalid.json");

        var validJson = "{" +
                        "\n  \"id\": \"ok\"," +
                        "\n  \"name\": \"ok\"," +
                        "\n  \"triggers\": [ { \"type\": \"manual\" } ]," +
                        "\n  \"actions\": [ { \"type\": \"llmQuery\", \"prompt\": \"hello\" } ]" +
                        "\n}";
        var invalidJson = "{" +
                           "\n  \"id\": \"bad\"," +
                           "\n  \"name\": \"bad\"," +
                           "\n  \"triggers\": [ { \"type\": \"manual\" } ]," +
                           "\n  \"actions\": [ { \"type\": \"llmQuery\", \"prompt\": \"nope\" } ]" +
                           "\n}";
        await File.WriteAllTextAsync(validPath, validJson);
        await File.WriteAllTextAsync(invalidPath, invalidJson);

        // Fake validation: if contains '"id": "bad"' then invalid
        var fakeAutomation = new FakeAutomationService(json =>
            json.Contains("\"id\": \"bad\"") ? RuleValidationResult.Fail("invalid") : RuleValidationResult.Ok());
        var fakeLifetime = new FakeAppLifetime();

        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationLifetime>(fakeLifetime);
        services.AddSingleton<IAutomationService>(fakeAutomation);
        services.AddSingleton(new PluginManager(null, pluginsDir));
        services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration { Model = "fallback-model" }));
        services.AddSingleton<IRuleManipulationService, RuleManipulationService>();
        var loggerFactory = LoggerFactory.Create(builder => { });
        services.AddSingleton<ILogger<StartCommand>>(sp => loggerFactory.CreateLogger<StartCommand>());
        var provider = services.BuildServiceProvider();

        var host = new FakeHost(provider);
        var ruleService = provider.GetRequiredService<IRuleManipulationService>();
        var logger = provider.GetRequiredService<ILogger<StartCommand>>();

        // Act
        await StartCommand.HandleAsync(host, logger, ruleService, rulesDir, modelIdArg: "cli-model");

        // Assert: only one valid rule added
        Assert.True(fakeAutomation.StartCalled);
        Assert.Equal(1, fakeAutomation.AddCount);
        Assert.Contains("\"modelId\": \"cli-model\"", fakeAutomation.LastAddedRuleJson);
    }

    [Fact]
    public async Task StartCommand_DoesNotOverwrite_ExistingModelId()
    {
        // Arrange: rule already has modelId
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        Directory.CreateDirectory(pluginsDir);

        var rulePath = Path.Combine(tempRoot, "rule-with-model.json");
        var ruleJson = "{" +
                       "\n  \"id\": \"hasModel\"," +
                       "\n  \"name\": \"test\"," +
                       "\n  \"triggers\": [ { \"type\": \"manual\" } ]," +
                       "\n  \"actions\": [ { \"type\": \"llmQuery\", \"prompt\": \"hi\", \"modelId\": \"pre\" } ]" +
                       "\n}";
        await File.WriteAllTextAsync(rulePath, ruleJson);

        var fakeAutomation = new FakeAutomationService();
        var fakeLifetime = new FakeAppLifetime();

        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationLifetime>(fakeLifetime);
        services.AddSingleton<IAutomationService>(fakeAutomation);
        services.AddSingleton(new PluginManager(null, pluginsDir));
        services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration { Model = "fallback-model" }));
        services.AddSingleton<IRuleManipulationService, RuleManipulationService>();
        var loggerFactory = LoggerFactory.Create(builder => { });
        services.AddSingleton<ILogger<StartCommand>>(sp => loggerFactory.CreateLogger<StartCommand>());
        var provider = services.BuildServiceProvider();

        var host = new FakeHost(provider);
        var ruleService = provider.GetRequiredService<IRuleManipulationService>();
        var logger = provider.GetRequiredService<ILogger<StartCommand>>();

        // Act
        await StartCommand.HandleAsync(host, logger, ruleService, rulePath, modelIdArg: "cli-model");

        // Assert: modelId remains 'pre'
        Assert.True(fakeAutomation.AddCalled);
        Assert.Contains("\"modelId\": \"pre\"", fakeAutomation.LastAddedRuleJson);
        Assert.DoesNotContain("\"modelId\": \"cli-model\"", fakeAutomation.LastAddedRuleJson);
    }

    [Fact]
    public async Task StartCommand_EmptyDirectory_NoJson_NoWork()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        var emptyDir = Path.Combine(tempRoot, "emptyRules");
        Directory.CreateDirectory(pluginsDir);
        Directory.CreateDirectory(emptyDir);

        var fakeAutomation = new FakeAutomationService();
        var fakeLifetime = new FakeAppLifetime();

        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationLifetime>(fakeLifetime);
        services.AddSingleton<IAutomationService>(fakeAutomation);
        services.AddSingleton(new PluginManager(null, pluginsDir));
        services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration { Model = "fallback-model" }));
        services.AddSingleton<IRuleManipulationService, RuleManipulationService>();
        var loggerFactory = LoggerFactory.Create(builder => { });
        services.AddSingleton<ILogger<StartCommand>>(sp => loggerFactory.CreateLogger<StartCommand>());
        var provider = services.BuildServiceProvider();

        var host = new FakeHost(provider);
        var ruleService = provider.GetRequiredService<IRuleManipulationService>();
        var logger = provider.GetRequiredService<ILogger<StartCommand>>();

        // Act
        await StartCommand.HandleAsync(host, logger, ruleService, emptyDir, modelIdArg: "cli-model");

        // Assert
        Assert.True(fakeAutomation.StartCalled);
        Assert.False(fakeAutomation.ValidateCalled);
        Assert.False(fakeAutomation.AddCalled);
        Assert.Equal(0, fakeAutomation.AddCount);
    }

    [Fact]
    public async Task StartCommand_Directory_IgnoresNonJsonFiles()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        var dir = Path.Combine(tempRoot, "mixed");
        Directory.CreateDirectory(pluginsDir);
        Directory.CreateDirectory(dir);

        // Place a .txt file; should be ignored
        var txtPath = Path.Combine(dir, "note.txt");
        await File.WriteAllTextAsync(txtPath, "not a rule");

        var fakeAutomation = new FakeAutomationService();
        var fakeLifetime = new FakeAppLifetime();

        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationLifetime>(fakeLifetime);
        services.AddSingleton<IAutomationService>(fakeAutomation);
        services.AddSingleton(new PluginManager(null, pluginsDir));
        services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration { Model = "fallback-model" }));
        services.AddSingleton<IRuleManipulationService, RuleManipulationService>();
        var loggerFactory = LoggerFactory.Create(builder => { });
        services.AddSingleton<ILogger<StartCommand>>(sp => loggerFactory.CreateLogger<StartCommand>());
        var provider = services.BuildServiceProvider();

        var host = new FakeHost(provider);
        var ruleService = provider.GetRequiredService<IRuleManipulationService>();
        var logger = provider.GetRequiredService<ILogger<StartCommand>>();

        // Act
        await StartCommand.HandleAsync(host, logger, ruleService, dir, modelIdArg: "cli-model");

        // Assert
        Assert.True(fakeAutomation.StartCalled);
        Assert.False(fakeAutomation.ValidateCalled);
        Assert.False(fakeAutomation.AddCalled);
        Assert.Equal(0, fakeAutomation.AddCount);
    }

    [Fact]
    public async Task StartCommand_Aborts_WhenServiceStartFails()
    {
        // Arrange: create a valid rule file but service start will fail
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        Directory.CreateDirectory(pluginsDir);

        var rulePath = Path.Combine(tempRoot, "rule.json");
        var ruleJson = "{" +
                       "\n  \"id\": \"t3\"," +
                       "\n  \"name\": \"test\"," +
                       "\n  \"triggers\": [ { \"type\": \"manual\" } ]," +
                       "\n  \"actions\": [ { \"type\": \"llmQuery\", \"prompt\": \"hello\" } ]" +
                       "\n}";
        await File.WriteAllTextAsync(rulePath, ruleJson);

        var fakeAutomation = new FakeAutomationService(startResult: false);
        var fakeLifetime = new FakeAppLifetime();

        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationLifetime>(fakeLifetime);
        services.AddSingleton<IAutomationService>(fakeAutomation);
        services.AddSingleton(new PluginManager(null, pluginsDir));
        services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration { Model = "fallback-model" }));
        services.AddSingleton<IRuleManipulationService, RuleManipulationService>();
        var loggerFactory = LoggerFactory.Create(builder => { });
        services.AddSingleton<ILogger<StartCommand>>(sp => loggerFactory.CreateLogger<StartCommand>());
        var provider = services.BuildServiceProvider();

        var host = new FakeHost(provider);
        var ruleService = provider.GetRequiredService<IRuleManipulationService>();
        var logger = provider.GetRequiredService<ILogger<StartCommand>>();

        // Act
        await StartCommand.HandleAsync(host, logger, ruleService, rulePath, modelIdArg: "cli-model");

        // Assert: start attempted, but no validation or additions performed
        Assert.True(fakeAutomation.StartCalled);
        Assert.False(fakeAutomation.ValidateCalled);
        Assert.False(fakeAutomation.AddCalled);
        Assert.Equal(0, fakeAutomation.AddCount);
    }

    [Fact]
    public async Task StartCommand_NoRulesPath_NoAdditions()
    {
        // Arrange: non-existent path
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        Directory.CreateDirectory(pluginsDir);

        var missingRulesPath = Path.Combine(tempRoot, "no-such", "rules.json");

        var fakeAutomation = new FakeAutomationService();
        var fakeLifetime = new FakeAppLifetime();

        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationLifetime>(fakeLifetime);
        services.AddSingleton<IAutomationService>(fakeAutomation);
        services.AddSingleton(new PluginManager(null, pluginsDir));
        services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration { Model = "fallback-model" }));
        services.AddSingleton<IRuleManipulationService, RuleManipulationService>();
        var loggerFactory = LoggerFactory.Create(builder => { });
        services.AddSingleton<ILogger<StartCommand>>(sp => loggerFactory.CreateLogger<StartCommand>());
        var provider = services.BuildServiceProvider();

        var host = new FakeHost(provider);
        var ruleService = provider.GetRequiredService<IRuleManipulationService>();
        var logger = provider.GetRequiredService<ILogger<StartCommand>>();

        // Act
        await StartCommand.HandleAsync(host, logger, ruleService, missingRulesPath, modelIdArg: "cli-model");

        // Assert: started but no rules added/validated
        Assert.True(fakeAutomation.StartCalled);
        Assert.False(fakeAutomation.AddCalled);
        Assert.Equal(0, fakeAutomation.AddCount);
        Assert.False(fakeAutomation.ValidateCalled);
    }

    [Fact]
    public async Task StartCommand_InjectsModelId_FromOptions_WhenArgMissing()
    {
        // Arrange: rule without modelId, CLI arg omitted, should use options.Model
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        Directory.CreateDirectory(pluginsDir);

        var rulePath = Path.Combine(tempRoot, "rule.json");
        var ruleJson = "{" +
                       "\n  \"id\": \"t2\"," +
                       "\n  \"name\": \"test\"," +
                       "\n  \"triggers\": [ { \"type\": \"manual\" } ]," +
                       "\n  \"actions\": [ { \"type\": \"llmQuery\", \"prompt\": \"hello\" } ]" +
                       "\n}";
        await File.WriteAllTextAsync(rulePath, ruleJson);

        var fakeAutomation = new FakeAutomationService();
        var fakeLifetime = new FakeAppLifetime();

        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationLifetime>(fakeLifetime);
        services.AddSingleton<IAutomationService>(fakeAutomation);
        services.AddSingleton(new PluginManager(null, pluginsDir));
        services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration { Model = "opts-model" }));
        services.AddSingleton<IRuleManipulationService, RuleManipulationService>();
        var loggerFactory = LoggerFactory.Create(builder => { });
        services.AddSingleton<ILogger<StartCommand>>(sp => loggerFactory.CreateLogger<StartCommand>());
        var provider = services.BuildServiceProvider();

        var host = new FakeHost(provider);
        var ruleService = provider.GetRequiredService<IRuleManipulationService>();
        var logger = provider.GetRequiredService<ILogger<StartCommand>>();

        // Act (modelIdArg omitted)
        await StartCommand.HandleAsync(host, logger, ruleService, rulePath, modelIdArg: null);

        // Assert: injected opts-model
        Assert.True(fakeAutomation.AddCalled);
        Assert.Contains("\"modelId\": \"opts-model\"", fakeAutomation.LastAddedRuleJson);
        Assert.DoesNotContain("\"modelId\": \"cli-model\"", fakeAutomation.LastAddedRuleJson);
    }

    private sealed class FakeHost : IHost
    {
        public IServiceProvider Services { get; }
        public FakeHost(IServiceProvider services) => Services = services;
        public void Dispose() { }
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task WaitForShutdownAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task WaitForShutdownAsync() => Task.CompletedTask;
        public void WaitForShutdown() { }
    }

    private sealed class FakeAppLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _cts = new();
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => _cts.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => _cts.Cancel();
    }

    private sealed class FakeAutomationService : IAutomationService
    {
        public bool StartCalled { get; private set; }
        public bool ValidateCalled { get; private set; }
        public bool AddCalled { get; private set; }
        public int AddCount { get; private set; }
        public string LastAddedRuleJson { get; private set; } = string.Empty;
        private readonly Func<string, RuleValidationResult> _validateFunc;
        private readonly bool _startResult;

        public FakeAutomationService(Func<string, RuleValidationResult> validateFunc = null, bool startResult = true)
        {
            _validateFunc = validateFunc;
            _startResult = startResult;
        }

        public Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalled = true;
            return Task.FromResult(_startResult);
        }

        public Task<bool> StopAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> RegisterFlowAsync(Loco.Core.Interfaces.IFlow flow, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> UnregisterFlowAsync(string flowId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<System.Collections.Generic.IEnumerable<Loco.Core.Interfaces.IFlow>> GetActiveFlowsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<System.Collections.Generic.IEnumerable<Loco.Core.Interfaces.IFlow>>(Array.Empty<Loco.Core.Interfaces.IFlow>());

        public Task<RuleValidationResult> ValidateRuleJsonAsync(string json, CancellationToken cancellationToken = default)
        {
            ValidateCalled = true;
            if (_validateFunc != null)
            {
                return Task.FromResult(_validateFunc(json));
            }
            return Task.FromResult(RuleValidationResult.Ok());
        }

        public Task<RuleValidationResult> ValidateRuleJsonAsync(System.Text.Json.Nodes.JsonNode node, CancellationToken cancellationToken = default)
        {
            ValidateCalled = true;
            return Task.FromResult(RuleValidationResult.Ok());
        }

        public Task<bool> AddRuleFromJsonAsync(string json, CancellationToken cancellationToken = default)
        {
            AddCalled = true;
            AddCount++;
            LastAddedRuleJson = json;
            return Task.FromResult(true);
        }

        public Task<bool> AddRuleFromJsonAsync(System.Text.Json.Nodes.JsonNode node, CancellationToken cancellationToken = default)
        {
            AddCalled = true;
            AddCount++;
            LastAddedRuleJson = node.ToJsonString();
            return Task.FromResult(true);
        }

        public Task<bool> DeleteRuleAsync(string ruleId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public void Dispose() { }
    }
}
