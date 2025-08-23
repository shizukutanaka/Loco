using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Loco.Cli.Commands;
using Loco.Llm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loco.Cli.Tests;

public class LlmConfigCommandTests
{
    [Fact]
    public void LlmConfigCommand_JsonOutput_MatchesShapeAndRedaction()
    {
        // Arrange
        var prevPreset = Environment.GetEnvironmentVariable("LOCO_LLM__PRESET");
        try
        {
            Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", "TEST_PRESET");

            var services = new ServiceCollection();
            services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration
            {
                Provider = "OPENAI",
                Model = "gpt-4o",
                ApiEndpoint = "https://api.openai.com",
                MaxTokens = 1024,
                Temperature = 0.5,
                HttpTimeoutMs = 45000,
                ApiKey = "abcd1234"
            }));
            var provider = services.BuildServiceProvider();
            var host = new FakeHost(provider);

            using var sw = new StringWriter();

            // Act
            LlmConfigCommand.Handle(host, json: true, sw);

            // Assert
            using var doc = JsonDocument.Parse(sw.ToString());
            var root = doc.RootElement;
            Assert.Equal("OPENAI", root.GetProperty("provider").GetString());
            Assert.Equal("gpt-4o", root.GetProperty("model").GetString());
            Assert.Equal("https://api.openai.com", root.GetProperty("apiEndpoint").GetString());
            Assert.Equal(1024, root.GetProperty("maxTokens").GetInt32());
            Assert.Equal(0.5, root.GetProperty("temperature").GetDouble());
            Assert.Equal(45000, root.GetProperty("httpTimeoutMs").GetInt32());
            Assert.Equal("redacted", root.GetProperty("apiKey").GetString());
            Assert.True(root.GetProperty("hasApiKey").GetBoolean());
            Assert.Equal("TEST_PRESET", root.GetProperty("preset").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", prevPreset);
        }
    }

    [Fact]
    public void LlmConfigCommand_JsonOutput_NoApiKey_EmptyAndHasApiKeyFalse()
    {
        // Arrange
        var prevPreset = Environment.GetEnvironmentVariable("LOCO_LLM__PRESET");
        try
        {
            Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", "PRESET2");

            var services = new ServiceCollection();
            services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration
            {
                Provider = "OLLAMA",
                Model = "llama3",
                ApiEndpoint = "http://localhost:11434",
                MaxTokens = 2048,
                Temperature = 0.7,
                HttpTimeoutMs = 60000,
                ApiKey = null
            }));
            var provider = services.BuildServiceProvider();
            var host = new FakeHost(provider);

            using var sw = new StringWriter();

            // Act
            LlmConfigCommand.Handle(host, json: true, sw);

            // Assert
            using var doc = JsonDocument.Parse(sw.ToString());
            var root = doc.RootElement;
            Assert.Equal("", root.GetProperty("apiKey").GetString());
            Assert.False(root.GetProperty("hasApiKey").GetBoolean());
            Assert.Equal(60000, root.GetProperty("httpTimeoutMs").GetInt32());
            Assert.Equal("PRESET2", root.GetProperty("preset").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", prevPreset);
        }
    }

    [Fact]
    public void LlmConfigCommand_TextOutput_MaskedKeyAndPreset()
    {
        // Arrange
        var prevPreset = Environment.GetEnvironmentVariable("LOCO_LLM__PRESET");
        try
        {
            Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", "P_TEXT");

            var services = new ServiceCollection();
            services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration
            {
                Provider = "OPENAI",
                Model = "gpt-4o-mini",
                ApiEndpoint = "https://api.openai.com",
                MaxTokens = 4096,
                Temperature = 0.2,
                HttpTimeoutMs = 30000,
                ApiKey = "abcd1234"
            }));
            var provider = services.BuildServiceProvider();
            var host = new FakeHost(provider);

            using var sw = new StringWriter();

            // Act
            LlmConfigCommand.Handle(host, json: false, sw);

            // Assert
            var output = sw.ToString();
            Assert.Contains("LLM Configuration (effective):", output);
            Assert.Contains("Provider     : OPENAI", output);
            Assert.Contains("Model        : gpt-4o-mini", output);
            Assert.Contains("ApiEndpoint  : https://api.openai.com", output);
            Assert.Contains("MaxTokens    : 4096", output);
            Assert.Contains("Temperature  : 0.2", output);
            Assert.Contains("HttpTimeoutMs: 30000", output);
            Assert.Contains("ApiKey       : ****1234", output); // last 4 visible
            Assert.Contains("HasApiKey    : True", output);
            Assert.Contains("Preset       : P_TEXT", output);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", prevPreset);
        }
    }

    [Fact]
    public void LlmConfigCommand_JsonOutput_PresetNullWhenUnset()
    {
        // Arrange
        var prevPreset = Environment.GetEnvironmentVariable("LOCO_LLM__PRESET");
        try
        {
            Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", null);

            var services = new ServiceCollection();
            services.AddSingleton<IOptions<LlmConfiguration>>(Options.Create(new LlmConfiguration
            {
                Provider = "OPENAI",
                Model = "gpt-4o",
                ApiEndpoint = "https://api.openai.com",
                MaxTokens = 1000,
                Temperature = 0.7,
                HttpTimeoutMs = 30000,
                ApiKey = null
            }));
            var provider = services.BuildServiceProvider();
            var host = new FakeHost(provider);

            using var sw = new StringWriter();

            // Act
            LlmConfigCommand.Handle(host, json: true, sw);

            // Assert
            using var doc = JsonDocument.Parse(sw.ToString());
            var root = doc.RootElement;
            Assert.Equal(JsonValueKind.Null, root.GetProperty("preset").ValueKind);
            Assert.Equal("", root.GetProperty("apiKey").GetString());
            Assert.False(root.GetProperty("hasApiKey").GetBoolean());
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", prevPreset);
        }
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
}
