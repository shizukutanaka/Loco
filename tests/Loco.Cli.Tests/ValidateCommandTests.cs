using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Loco.Automation.Interfaces;
using Loco.Cli.Commands;
using Loco.Core.Models;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Loco.Cli.Tests;

public class ValidateCommandTests
{
    [Fact]
    public async Task ValidateCommand_FileNotFound_NoValidation()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var missingFile = Path.Combine(tempRoot, "missing.json");

        var fakeAutomation = new FakeAutomationService();
        var logger = LoggerFactory.Create(b => { }).CreateLogger<ValidateCommand>();

        await ValidateCommand.HandleAsync(fakeAutomation, logger, missingFile);

        Assert.False(fakeAutomation.ValidateCalled);
    }

    [Fact]
    public async Task ValidateCommand_Valid_RunsValidation()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var file = Path.Combine(tempRoot, "flow.json");
        var json = "{" +
                   "\n  \"id\": \"v1\"," +
                   "\n  \"name\": \"ok\"," +
                   "\n  \"triggers\": [ { \"type\": \"manual\" } ]," +
                   "\n  \"actions\": [ { \"type\": \"llmQuery\", \"prompt\": \"hi\" } ]" +
                   "\n}";
        await File.WriteAllTextAsync(file, json);

        var fakeAutomation = new FakeAutomationService(_ => RuleValidationResult.Ok());
        var logger = LoggerFactory.Create(b => { }).CreateLogger<ValidateCommand>();

        await ValidateCommand.HandleAsync(fakeAutomation, logger, file);

        Assert.True(fakeAutomation.ValidateCalled);
    }

    [Fact]
    public async Task ValidateCommand_MalformedJson_RunsValidation()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var file = Path.Combine(tempRoot, "flow.json");
        var malformed = "{ \"id\": \"x\", \"name\": \"bad\", "; // intentionally truncated JSON
        await File.WriteAllTextAsync(file, malformed);

        var fakeAutomation = new FakeAutomationService(_ => RuleValidationResult.Fail("json error"));
        var logger = LoggerFactory.Create(b => { }).CreateLogger<ValidateCommand>();

        await ValidateCommand.HandleAsync(fakeAutomation, logger, file);

        Assert.True(fakeAutomation.ValidateCalled);
    }

    [Fact]
    public async Task ValidateCommand_Invalid_RunsValidation()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LocoCliTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var file = Path.Combine(tempRoot, "flow.json");
        var json = "{" +
                   "\n  \"id\": \"v2\"," +
                   "\n  \"name\": \"bad\"," +
                   "\n  \"triggers\": [ { \"type\": \"manual\" } ]," +
                   "\n  \"actions\": [ { \"type\": \"llmQuery\", \"prompt\": \"hi\" } ]" +
                   "\n}";
        await File.WriteAllTextAsync(file, json);

        var fakeAutomation = new FakeAutomationService(_ => RuleValidationResult.Fail("boom"));
        var logger = LoggerFactory.Create(b => { }).CreateLogger<ValidateCommand>();

        await ValidateCommand.HandleAsync(fakeAutomation, logger, file);

        Assert.True(fakeAutomation.ValidateCalled);
    }

    private sealed class FakeAutomationService : IAutomationService
    {
        public bool ValidateCalled { get; private set; }
        private readonly Func<string, RuleValidationResult> _validator;

        public FakeAutomationService(Func<string, RuleValidationResult> validator = null)
        {
            _validator = validator;
        }

        public Task<bool> StartAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> StopAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> RegisterFlowAsync(Loco.Core.Interfaces.IFlow flow, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> UnregisterFlowAsync(string flowId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<System.Collections.Generic.IEnumerable<Loco.Core.Interfaces.IFlow>> GetActiveFlowsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<System.Collections.Generic.IEnumerable<Loco.Core.Interfaces.IFlow>>(Array.Empty<Loco.Core.Interfaces.IFlow>());

        public Task<RuleValidationResult> ValidateRuleJsonAsync(string json, CancellationToken cancellationToken = default)
        {
            ValidateCalled = true;
            if (_validator != null) return Task.FromResult(_validator(json));
            return Task.FromResult(RuleValidationResult.Ok());
        }

        public Task<RuleValidationResult> ValidateRuleJsonAsync(System.Text.Json.Nodes.JsonNode node, CancellationToken cancellationToken = default)
        {
            ValidateCalled = true;
            return Task.FromResult(RuleValidationResult.Ok());
        }

        public Task<bool> AddRuleFromJsonAsync(string json, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> AddRuleFromJsonAsync(System.Text.Json.Nodes.JsonNode node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> DeleteRuleAsync(string ruleId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public void Dispose() { }
    }
}
