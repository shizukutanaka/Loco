using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Loco.Automation.Interfaces;
using Loco.Automation.Services;
using Loco.Cli.Commands;
using Loco.Core.Interfaces;
using Loco.Core.Models;
using Loco.Core.Plugins;
using Loco.Llm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loco.Cli.Tests;

public class ExecuteCommandTests
{
    [Fact]
    public async Task ExecuteCommand_FileNotFound_NoStart_NoTrigger()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        Directory.CreateDirectory(pluginsDir);

        var fakeAutomation = new FakeAutomationService();
        var fakeRuleEngine = new FakeRuleEngine();

        var services = new ServiceCollection();
        services.AddSingleton<IAutomationService>(fakeAutomation);
        var loggerFactory = LoggerFactory.Create(builder => { });
        services.AddSingleton<ILogger<ExecuteCommand>>(sp => loggerFactory.CreateLogger<ExecuteCommand>());
        services.AddSingleton<IRuleManipulationService, RuleManipulationService>();
        services.AddSingleton(new PluginManager(null, pluginsDir));
        services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration { Model = "opts-model" }));
        var provider = services.BuildServiceProvider();

        var host = new FakeHost(provider);
        var logger = provider.GetRequiredService<ILogger<ExecuteCommand>>();
        var ruleService = provider.GetRequiredService<IRuleManipulationService>();
        var pluginManager = provider.GetRequiredService<PluginManager>();
        var llmOptions = provider.GetRequiredService<IOptions<LlmConfiguration>>();

        var missingFile = Path.Combine(tempRoot, "nope.json");

        await ExecuteCommand.HandleAsync(host, logger, fakeAutomation, fakeRuleEngine, ruleService, pluginManager, llmOptions, missingFile, modelIdArg: null, timeoutSeconds: 5, inputsJson: null);

        Assert.False(fakeAutomation.StartCalled);
        Assert.False(fakeRuleEngine.TriggerCalled);
    }

    [Fact]
    public async Task ExecuteCommand_ValidInputsJson_PassedToEngine()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        Directory.CreateDirectory(pluginsDir);

        var rulePath = Path.Combine(tempRoot, "flow.json");
        var ruleJson = "{" +
                       "\n  \"id\": \"r1\"," +
                       "\n  \"name\": \"flow\"," +
                       "\n  \"triggers\": [ { \"type\": \"manual\" } ]," +
                       "\n  \"actions\": [ { \"type\": \"llmQuery\", \"prompt\": \"hi\" } ]" +
                       "\n}";
        await File.WriteAllTextAsync(rulePath, ruleJson);

        var fakeAutomation = new FakeAutomationService();
        var fakeRuleEngine = new FakeRuleEngine();

        var services = new ServiceCollection();
        services.AddSingleton<IAutomationService>(fakeAutomation);
        var loggerFactory = LoggerFactory.Create(builder => { });
        services.AddSingleton<ILogger<ExecuteCommand>>(sp => loggerFactory.CreateLogger<ExecuteCommand>());
        services.AddSingleton<IRuleManipulationService, RuleManipulationService>();
        services.AddSingleton(new PluginManager(null, pluginsDir));
        services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration { Model = "opts-model" }));
        var provider = services.BuildServiceProvider();

        var host = new FakeHost(provider);
        var logger = provider.GetRequiredService<ILogger<ExecuteCommand>>();
        var ruleService = provider.GetRequiredService<IRuleManipulationService>();
        var pluginManager = provider.GetRequiredService<PluginManager>();
        var llmOptions = provider.GetRequiredService<IOptions<LlmConfiguration>>();

        var inputs = "{ \"k1\": \"v1\", \"n\": 2 }";
        await ExecuteCommand.HandleAsync(host, logger, fakeAutomation, fakeRuleEngine, ruleService, pluginManager, llmOptions, rulePath, modelIdArg: null, timeoutSeconds: 5, inputsJson: inputs );

        Assert.True(fakeRuleEngine.TriggerCalled);
        Assert.NotNull(fakeRuleEngine.LastInputs);
        Assert.True(fakeRuleEngine.LastInputs.ContainsKey("k1"));
        Assert.Equal("v1", fakeRuleEngine.LastInputs["k1"]?.ToString());
        Assert.True(fakeRuleEngine.LastInputs.ContainsKey("n"));
        Assert.Equal("2", fakeRuleEngine.LastInputs["n"]?.ToString());
    }

    [Fact]
    public async Task ExecuteCommand_AddRuleFails_NoTrigger()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        Directory.CreateDirectory(pluginsDir);

        var rulePath = Path.Combine(tempRoot, "flow.json");
        var ruleJson = "{" +
                       "\n  \"id\": \"r1\"," +
                       "\n  \"name\": \"flow\"," +
                       "\n  \"triggers\": [ { \"type\": \"manual\" } ]," +
                       "\n  \"actions\": [ { \"type\": \"llmQuery\", \"prompt\": \"hi\" } ]" +
                       "\n}";
        await File.WriteAllTextAsync(rulePath, ruleJson);

        var fakeAutomation = new FakeAutomationService(addResult: false);
        var fakeRuleEngine = new FakeRuleEngine();

        var services = new ServiceCollection();
        services.AddSingleton<IAutomationService>(fakeAutomation);
        var loggerFactory = LoggerFactory.Create(builder => { });
        services.AddSingleton<ILogger<ExecuteCommand>>(sp => loggerFactory.CreateLogger<ExecuteCommand>());
        services.AddSingleton<IRuleManipulationService, RuleManipulationService>();
        services.AddSingleton(new PluginManager(null, pluginsDir));
        services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration { Model = "opts-model" }));
        var provider = services.BuildServiceProvider();

        var host = new FakeHost(provider);
        var logger = provider.GetRequiredService<ILogger<ExecuteCommand>>();
        var ruleService = provider.GetRequiredService<IRuleManipulationService>();
        var pluginManager = provider.GetRequiredService<PluginManager>();
        var llmOptions = provider.GetRequiredService<IOptions<LlmConfiguration>>();

        await ExecuteCommand.HandleAsync(host, logger, fakeAutomation, fakeRuleEngine, ruleService, pluginManager, llmOptions, rulePath, modelIdArg: null, timeoutSeconds: 5, inputsJson: "{}" );

        Assert.True(fakeAutomation.StartCalled);
        Assert.True(fakeAutomation.ValidateCalled);
        Assert.True(fakeAutomation.AddCalled);
        Assert.False(fakeRuleEngine.TriggerCalled);
    }

    [Fact]
    public async Task ExecuteCommand_InjectsModelId_FromArg_And_Triggers()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        Directory.CreateDirectory(pluginsDir);

        var rulePath = Path.Combine(tempRoot, "flow.json");
        var ruleJson = "{" +
                       "\n  \"id\": \"r1\"," +
                       "\n  \"name\": \"flow\"," +
                       "\n  \"triggers\": [ { \"type\": \"manual\" } ]," +
                       "\n  \"actions\": [ { \"type\": \"llmQuery\", \"prompt\": \"hi\" } ]" +
                       "\n}";
        await File.WriteAllTextAsync(rulePath, ruleJson);

        var fakeAutomation = new FakeAutomationService();
        var fakeRuleEngine = new FakeRuleEngine();

        var services = new ServiceCollection();
        services.AddSingleton<IAutomationService>(fakeAutomation);
        var loggerFactory = LoggerFactory.Create(builder => { });
        services.AddSingleton<ILogger<ExecuteCommand>>(sp => loggerFactory.CreateLogger<ExecuteCommand>());
        services.AddSingleton<IRuleManipulationService, RuleManipulationService>();
        services.AddSingleton(new PluginManager(null, pluginsDir));
        services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration { Model = "opts-model" }));
        var provider = services.BuildServiceProvider();

        var host = new FakeHost(provider);
        var logger = provider.GetRequiredService<ILogger<ExecuteCommand>>();
        var ruleService = provider.GetRequiredService<IRuleManipulationService>();
        var pluginManager = provider.GetRequiredService<PluginManager>();
        var llmOptions = provider.GetRequiredService<IOptions<LlmConfiguration>>();

        await ExecuteCommand.HandleAsync(host, logger, fakeAutomation, fakeRuleEngine, ruleService, pluginManager, llmOptions, rulePath, modelIdArg: "cli-model", timeoutSeconds: 5, inputsJson: "{}" );

        Assert.True(fakeAutomation.StartCalled);
        Assert.True(fakeAutomation.ValidateCalled);
        Assert.True(fakeAutomation.AddCalled);
        Assert.Contains("\"modelId\": \"cli-model\"", fakeAutomation.LastAddedRuleJson);
        Assert.True(fakeRuleEngine.TriggerCalled);
        Assert.Equal("r1", fakeRuleEngine.LastTriggeredRuleId);
    }

    [Fact]
    public async Task ExecuteCommand_InjectsModelId_FromOptions_WhenArgMissing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        Directory.CreateDirectory(pluginsDir);

        var rulePath = Path.Combine(tempRoot, "flow.json");
        var ruleJson = "{" +
                       "\n  \"id\": \"r1\"," +
                       "\n  \"name\": \"flow\"," +
                       "\n  \"triggers\": [ { \"type\": \"manual\" } ]," +
                       "\n  \"actions\": [ { \"type\": \"llmQuery\", \"prompt\": \"hi\" } ]" +
                       "\n}";
        await File.WriteAllTextAsync(rulePath, ruleJson);

        var fakeAutomation = new FakeAutomationService();
        var fakeRuleEngine = new FakeRuleEngine();

        var services = new ServiceCollection();
        services.AddSingleton<IAutomationService>(fakeAutomation);
        var loggerFactory = LoggerFactory.Create(builder => { });
        services.AddSingleton<ILogger<ExecuteCommand>>(sp => loggerFactory.CreateLogger<ExecuteCommand>());
        services.AddSingleton<IRuleManipulationService, RuleManipulationService>();
        services.AddSingleton(new PluginManager(null, pluginsDir));
        services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration { Model = "opts-model" }));
        var provider = services.BuildServiceProvider();

        var host = new FakeHost(provider);
        var logger = provider.GetRequiredService<ILogger<ExecuteCommand>>();
        var ruleService = provider.GetRequiredService<IRuleManipulationService>();
        var pluginManager = provider.GetRequiredService<PluginManager>();
        var llmOptions = provider.GetRequiredService<IOptions<LlmConfiguration>>();

        await ExecuteCommand.HandleAsync(host, logger, fakeAutomation, fakeRuleEngine, ruleService, pluginManager, llmOptions, rulePath, modelIdArg: null, timeoutSeconds: 5, inputsJson: "{}" );

        Assert.True(fakeAutomation.AddCalled);
        Assert.Contains("\"modelId\": \"opts-model\"", fakeAutomation.LastAddedRuleJson);
    }

    [Fact]
    public async Task ExecuteCommand_ValidationFailure_NoAdd_NoTrigger()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        Directory.CreateDirectory(pluginsDir);

        var rulePath = Path.Combine(tempRoot, "flow.json");
        var ruleJson = "{" +
                       "\n  \"id\": \"r1\"," +
                       "\n  \"name\": \"flow\"," +
                       "\n  \"triggers\": [ { \"type\": \"manual\" } ]," +
                       "\n  \"actions\": [ { \"type\": \"llmQuery\", \"prompt\": \"hi\" } ]" +
                       "\n}";
        await File.WriteAllTextAsync(rulePath, ruleJson);

        var fakeAutomation = new FakeAutomationService(json => RuleValidationResult.Fail("bad"));
        var fakeRuleEngine = new FakeRuleEngine();

        var services = new ServiceCollection();
        services.AddSingleton<IAutomationService>(fakeAutomation);
        var loggerFactory = LoggerFactory.Create(builder => { });
        services.AddSingleton<ILogger<ExecuteCommand>>(sp => loggerFactory.CreateLogger<ExecuteCommand>());
        services.AddSingleton<IRuleManipulationService, RuleManipulationService>();
        services.AddSingleton(new PluginManager(null, pluginsDir));
        services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration { Model = "opts-model" }));
        var provider = services.BuildServiceProvider();

        var host = new FakeHost(provider);
        var logger = provider.GetRequiredService<ILogger<ExecuteCommand>>();
        var ruleService = provider.GetRequiredService<IRuleManipulationService>();
        var pluginManager = provider.GetRequiredService<PluginManager>();
        var llmOptions = provider.GetRequiredService<IOptions<LlmConfiguration>>();

        await ExecuteCommand.HandleAsync(host, logger, fakeAutomation, fakeRuleEngine, ruleService, pluginManager, llmOptions, rulePath, modelIdArg: null, timeoutSeconds: 5, inputsJson: null );

        Assert.True(fakeAutomation.StartCalled);
        Assert.True(fakeAutomation.ValidateCalled);
        Assert.False(fakeAutomation.AddCalled);
        Assert.False(fakeRuleEngine.TriggerCalled);
    }

    [Fact]
    public async Task ExecuteCommand_InvalidInputsJson_NoTrigger()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        Directory.CreateDirectory(pluginsDir);

        var rulePath = Path.Combine(tempRoot, "flow.json");
        var ruleJson = "{" +
                       "\n  \"id\": \"r1\"," +
                       "\n  \"name\": \"flow\"," +
                       "\n  \"triggers\": [ { \"type\": \"manual\" } ]," +
                       "\n  \"actions\": [ { \"type\": \"llmQuery\", \"prompt\": \"hi\" } ]" +
                       "\n}";
        await File.WriteAllTextAsync(rulePath, ruleJson);

        var fakeAutomation = new FakeAutomationService();
        var fakeRuleEngine = new FakeRuleEngine();

        var services = new ServiceCollection();
        services.AddSingleton<IAutomationService>(fakeAutomation);
        var loggerFactory = LoggerFactory.Create(builder => { });
        services.AddSingleton<ILogger<ExecuteCommand>>(sp => loggerFactory.CreateLogger<ExecuteCommand>());
        services.AddSingleton<IRuleManipulationService, RuleManipulationService>();
        services.AddSingleton(new PluginManager(null, pluginsDir));
        services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration { Model = "opts-model" }));
        var provider = services.BuildServiceProvider();

        var host = new FakeHost(provider);
        var logger = provider.GetRequiredService<ILogger<ExecuteCommand>>();
        var ruleService = provider.GetRequiredService<IRuleManipulationService>();
        var pluginManager = provider.GetRequiredService<PluginManager>();
        var llmOptions = provider.GetRequiredService<IOptions<LlmConfiguration>>();

        await ExecuteCommand.HandleAsync(host, logger, fakeAutomation, fakeRuleEngine, ruleService, pluginManager, llmOptions, rulePath, modelIdArg: null, timeoutSeconds: 5, inputsJson: "not json" );

        Assert.True(fakeAutomation.StartCalled);
        Assert.True(fakeAutomation.ValidateCalled);
        Assert.True(fakeAutomation.AddCalled);
        Assert.False(fakeRuleEngine.TriggerCalled);
    }

    [Fact]
    public async Task ExecuteCommand_Timeout_IsHandled()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        Directory.CreateDirectory(pluginsDir);

        var rulePath = Path.Combine(tempRoot, "flow.json");
        var ruleJson = "{" +
                       "\n  \"id\": \"r1\"," +
                       "\n  \"name\": \"flow\"," +
                       "\n  \"triggers\": [ { \"type\": \"manual\" } ]," +
                       "\n  \"actions\": [ { \"type\": \"llmQuery\", \"prompt\": \"hi\" } ]" +
                       "\n}";
        await File.WriteAllTextAsync(rulePath, ruleJson);

        var fakeAutomation = new FakeAutomationService();
        var fakeRuleEngine = new FakeRuleEngine(simulateTimeout: true);

        var services = new ServiceCollection();
        services.AddSingleton<IAutomationService>(fakeAutomation);
        var loggerFactory = LoggerFactory.Create(builder => { });
        services.AddSingleton<ILogger<ExecuteCommand>>(sp => loggerFactory.CreateLogger<ExecuteCommand>());
        services.AddSingleton<IRuleManipulationService, RuleManipulationService>();
        services.AddSingleton(new PluginManager(null, pluginsDir));
        services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration { Model = "opts-model" }));
        var provider = services.BuildServiceProvider();

        var host = new FakeHost(provider);
        var logger = provider.GetRequiredService<ILogger<ExecuteCommand>>();
        var ruleService = provider.GetRequiredService<IRuleManipulationService>();
        var pluginManager = provider.GetRequiredService<PluginManager>();
        var llmOptions = provider.GetRequiredService<IOptions<LlmConfiguration>>();

        await ExecuteCommand.HandleAsync(host, logger, fakeAutomation, fakeRuleEngine, ruleService, pluginManager, llmOptions, rulePath, modelIdArg: null, timeoutSeconds: 1, inputsJson: "{}" );

        Assert.True(fakeRuleEngine.TriggerCalled);
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

    private sealed class FakeAutomationService : IAutomationService
    {
        public bool StartCalled { get; private set; }
        public bool ValidateCalled { get; private set; }
        public bool AddCalled { get; private set; }
        public string LastAddedRuleJson { get; private set; } = string.Empty;
        private readonly Func<string, RuleValidationResult> _validateFunc;
        private readonly bool _addResult;

        public FakeAutomationService(Func<string, RuleValidationResult> validateFunc = null, bool addResult = true)
        {
            _validateFunc = validateFunc;
            _addResult = addResult;
        }

        public Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalled = true;
            return Task.FromResult(true);
        }

        public Task<bool> StopAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> RegisterFlowAsync(IFlow flow, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> UnregisterFlowAsync(string flowId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<IEnumerable<IFlow>> GetActiveFlowsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<IFlow>>(Array.Empty<IFlow>());

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
            LastAddedRuleJson = json;
            return Task.FromResult(_addResult);
        }

        public Task<bool> AddRuleFromJsonAsync(System.Text.Json.Nodes.JsonNode node, CancellationToken cancellationToken = default)
        {
            AddCalled = true;
            LastAddedRuleJson = node.ToJsonString();
            return Task.FromResult(_addResult);
        }

        public Task<bool> DeleteRuleAsync(string ruleId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public void Dispose() { }
    }

    private sealed class FakeRuleEngine : IAutomationRuleEngine
    {
        public bool TriggerCalled { get; private set; }
        public string LastTriggeredRuleId { get; private set; } = string.Empty;
        private readonly bool _simulateTimeout;
        public IDictionary<string, object> LastInputs { get; private set; }

        public FakeRuleEngine(bool simulateTimeout = false)
        {
            _simulateTimeout = simulateTimeout;
        }

        public Task<bool> AddRuleAsync(AutomationDsl.Rule rule, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> LoadRuleAsync(AutomationDsl.Rule dslRule) => Task.FromResult(true);

        public Task<bool> LoadRuleFromNaturalLanguageAsync(string naturalLanguage, string modelId = null) => Task.FromResult(true);

        public void RegisterActionType(string name, Type type) { }

        public IEnumerable<AutomationRule> GetRules() => Array.Empty<AutomationRule>();

        public Task<bool> SetRuleEnabledAsync(string ruleId, bool enabled) => Task.FromResult(true);

        public Task<bool> DeleteRuleAsync(string ruleId) => Task.FromResult(true);

        public async Task<bool> TriggerRuleAsync(string ruleId, IDictionary<string, object> context, CancellationToken cancellationToken = default)
        {
            TriggerCalled = true;
            LastTriggeredRuleId = ruleId;
            LastInputs = context;
            if (_simulateTimeout)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            return true;
        }

        public void Dispose() { }
    }
}
