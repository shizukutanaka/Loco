using System;
using System.Net.Http;
using FluentAssertions;
using Loco.Llm;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loco.Core.Tests.Llm;

public class LlmConfigurationEnvTests
{
    private static void SetEnv(string key, string? value, out string? prev)
    {
        prev = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    [Fact]
    public void ApplyEnvironmentVariables_PrefersDoubleUnderscoreOverLegacy()
    {
        SetEnv("LOCO_LLM__MODEL", "double", out var prevDouble);
        SetEnv("LOCO_LLM_MODEL", "legacy", out var prevLegacy);
        try
        {
            var opts = new LlmConfiguration();
            LlmConfigurationEnv.ApplyEnvironmentVariables(opts);
            opts.Model.Should().Be("double");
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCO_LLM__MODEL", prevDouble);
            Environment.SetEnvironmentVariable("LOCO_LLM_MODEL", prevLegacy);
        }
    }

    [Fact]
    public void PrimeEnvironmentFromPreset_FillsMissingValues()
    {
        SetEnv("LOCO_LLM__PRESET", "OLLAMA", out var prevPreset);
        SetEnv("LOCO_LLM__PROVIDER", null, out var prevProvider);
        SetEnv("LOCO_LLM__MODEL", null, out var prevModel);
        SetEnv("LOCO_LLM__APIENDPOINT", null, out var prevEndpoint);
        try
        {
            LlmConfigurationEnv.PrimeEnvironmentFromPreset();
            Environment.GetEnvironmentVariable("LOCO_LLM__PROVIDER").Should().Be("ollama");
            Environment.GetEnvironmentVariable("LOCO_LLM__MODEL").Should().Be("llama3.1");
            Environment.GetEnvironmentVariable("LOCO_LLM__APIENDPOINT").Should().Be("http://localhost:11434/api/generate");
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", prevPreset);
            Environment.SetEnvironmentVariable("LOCO_LLM__PROVIDER", prevProvider);
            Environment.SetEnvironmentVariable("LOCO_LLM__MODEL", prevModel);
            Environment.SetEnvironmentVariable("LOCO_LLM__APIENDPOINT", prevEndpoint);
        }
    }

    [Fact]
    public void ApplyPresetDefaults_DoesNotOverrideExplicitValues()
    {
        SetEnv("LOCO_LLM__PRESET", "OPENAI", out var prevPreset);
        try
        {
            var opts = new LlmConfiguration
            {
                Model = "gpt-explicit",
                Provider = string.Empty,
                ApiEndpoint = string.Empty
            };
            LlmConfigurationEnv.ApplyEnvironmentVariables(opts);
            opts.Model.Should().Be("gpt-explicit");
            opts.Provider.Should().Be("openai");
            opts.ApiEndpoint.Should().Be("https://api.openai.com/v1/completions");
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", prevPreset);
        }
    }

    [Fact]
    public void LlmService_TimeoutClampedInConstructor_LowAndHigh()
    {
        // Low value -> clamp to 1000 ms
        SetEnv("LOCO_LLM__HTTPTIMEOUTMS", "10", out var prevLow);
        try
        {
            var cfgLow = new LlmConfiguration();
            LlmConfigurationEnv.ApplyEnvironmentVariables(cfgLow);
            var httpLow = new HttpClient();
            var svcLow = new Loco.Llm.LlmService(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Loco.Llm.LlmService>.Instance,
                Options.Create(cfgLow),
                httpLow);
            httpLow.Timeout.Should().Be(TimeSpan.FromMilliseconds(1000));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCO_LLM__HTTPTIMEOUTMS", prevLow);
        }

        // High value -> clamp to 600000 ms
        SetEnv("LOCO_LLM__HTTPTIMEOUTMS", "10000000", out var prevHigh);
        try
        {
            var cfgHigh = new LlmConfiguration();
            LlmConfigurationEnv.ApplyEnvironmentVariables(cfgHigh);
            var httpHigh = new HttpClient();
            var svcHigh = new Loco.Llm.LlmService(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Loco.Llm.LlmService>.Instance,
                Options.Create(cfgHigh),
                httpHigh);
            httpHigh.Timeout.Should().Be(TimeSpan.FromMilliseconds(600000));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCO_LLM__HTTPTIMEOUTMS", prevHigh);
        }
    }
}
