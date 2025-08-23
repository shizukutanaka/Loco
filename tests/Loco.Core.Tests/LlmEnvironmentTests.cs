using System;
using System.IO;
using System.Net.Http;
using FluentAssertions;
using Loco.Core.Utilities;
using Loco.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loco.Core.Tests;

[CollectionDefinition("EnvVarDependent", DisableParallelization = true)]
public class EnvVarDependentCollection : ICollectionFixture<EnvVarScopeFixture> { }

public class EnvVarScopeFixture : IDisposable
{
    public void Dispose()
    {
        // Ensure we clean known variables between collections
        foreach (var key in new[]
        {
            "FOO", "LOCO_LLM__MODEL", "LOCO_LLM__PROVIDER", "LOCO_LLM__APIENDPOINT",
            "LOCO_LLM__APIKEY", "LOCO_LLM__PRESET", "LOCO_LLM_PRESET", "LOCO_LLM__HTTPTIMEOUTMS"
        })
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }
}

[Collection("EnvVarDependent")]
public class LlmEnvironmentTests
{
    [Fact]
    public void DotEnvLoader_Load_DoesNotOverrideExistingEnvironment()
    {
        using var temp = new TempEnvFile("FOO=bar\nLOCO_LLM__MODEL=fromEnvFile\n");
        Environment.SetEnvironmentVariable("LOCO_LLM__MODEL", "preExisting");

        DotEnvLoader.Load(temp.Path);

        Environment.GetEnvironmentVariable("FOO").Should().Be("bar");
        Environment.GetEnvironmentVariable("LOCO_LLM__MODEL").Should().Be("preExisting");
    }

    [Fact]
    public void DotEnvLoader_Load_SetsMissingEnvironment()
    {
        using var temp = new TempEnvFile("FOO=bar\nLOCO_LLM__MODEL=fromEnvFile\n");
        Environment.SetEnvironmentVariable("LOCO_LLM__MODEL", null);

        DotEnvLoader.Load(temp.Path);

        Environment.GetEnvironmentVariable("FOO").Should().Be("bar");
        Environment.GetEnvironmentVariable("LOCO_LLM__MODEL").Should().Be("fromEnvFile");
    }

    [Fact]
    public void LlmConfigurationEnv_Preset_PrimesDefaults_OLLAMA()
    {
        using var scope = new EnvVars(
            ("LOCO_LLM__PRESET", "OLLAMA")
        );
        var cfg = new LlmConfiguration(); // defaults are openai/gpt-4

        LlmConfigurationEnv.ApplyEnvironmentVariables(cfg);

        cfg.Provider.Should().Be("ollama");
        cfg.Model.Should().NotBeNullOrEmpty();
        cfg.ApiEndpoint.Should().Contain("11434");
    }

    [Fact]
    public void LlmConfigurationEnv_DoesNotOverride_ExplicitValues()
    {
        using var scope = new EnvVars(
            ("LOCO_LLM__PRESET", "OPENAI"),
            ("LOCO_LLM__MODEL", "custom-model"),
            ("LOCO_LLM__PROVIDER", "custom-provider"),
            ("LOCO_LLM__APIENDPOINT", "http://example.com/v1")
        );
        var cfg = new LlmConfiguration
        {
            Provider = string.Empty,
            Model = string.Empty,
            ApiEndpoint = string.Empty
        };

        LlmConfigurationEnv.ApplyEnvironmentVariables(cfg);

        cfg.Provider.Should().Be("custom-provider");
        cfg.Model.Should().Be("custom-model");
        cfg.ApiEndpoint.Should().Be("http://example.com/v1");
    }

    [Theory]
    [InlineData(500, 1000)]
    [InlineData(700000, 600000)]
    [InlineData(30000, 30000)]
    public void LlmService_TimeoutClamped(int configured, int expected)
    {
        var cfg = Options.Create(new LlmConfiguration
        {
            HttpTimeoutMs = configured,
            ApiKey = "key",
            ApiEndpoint = "http://localhost/echo",
            Model = "m"
        });
        using var http = new HttpClient(new HttpClientHandler());
        var svc = new LlmService(NullLogger<LlmService>.Instance, cfg, http);

        http.Timeout.Should().Be(TimeSpan.FromMilliseconds(expected));
    }

    private sealed class TempEnvFile : IDisposable
    {
        public string Path { get; }
        public TempEnvFile(string content)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $".env-test-{Guid.NewGuid():N}");
            File.WriteAllText(Path, content);
        }
        public void Dispose()
        {
            try { if (File.Exists(Path)) File.Delete(Path); } catch { }
        }
    }

    private sealed class EnvVars : IDisposable
    {
        private readonly (string key, string? prev)[] _prev;
        public EnvVars(params (string key, string? value)[] vars)
        {
            _prev = new (string key, string? prev)[vars.Length];
            for (int i = 0; i < vars.Length; i++)
            {
                _prev[i] = (vars[i].key, Environment.GetEnvironmentVariable(vars[i].key));
                Environment.SetEnvironmentVariable(vars[i].key, vars[i].value);
            }
        }
        public void Dispose()
        {
            foreach (var (key, prev) in _prev)
            {
                Environment.SetEnvironmentVariable(key, prev);
            }
        }
    }
}
