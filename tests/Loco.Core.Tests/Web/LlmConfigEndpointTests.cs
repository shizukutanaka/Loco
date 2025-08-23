using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Loco.Core.Tests.Web;

public class LlmConfigEndpointTests
{
    private static void SetEnv(string key, string? value, out string? prev)
    {
        prev = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    [Fact]
    public async Task GetConfig_IncludesHasApiKeyAndPreset_WithRedactedApiKey()
    {
        // Arrange
        SetEnv("LOCO_LLM__PRESET", "OLLAMA", out var prevPreset);
        SetEnv("LOCO_LLM__APIKEY", "secret-123456", out var prevKey);

        try
        {
            await using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();

            // Act
            var resp = await client.GetAsync("/api/llm/config");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            root.GetProperty("hasApiKey").GetBoolean().Should().BeTrue();
            root.GetProperty("apiKey").GetString().Should().Be("redacted");
            root.GetProperty("preset").GetString().Should().Be("OLLAMA");
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", prevPreset);
            Environment.SetEnvironmentVariable("LOCO_LLM__APIKEY", prevKey);
        }
    }

    [Fact]
    public async Task GetConfig_PresetDoesNotOverrideExplicitModel()
    {
        // Arrange
        SetEnv("LOCO_LLM__PRESET", "OPENAI", out var prevPreset);
        SetEnv("LOCO_LLM__MODEL", "gpt-4o", out var prevModel);

        try
        {
            await using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();

            // Act
            var resp = await client.GetAsync("/api/llm/config");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            root.GetProperty("preset").GetString().Should().Be("OPENAI");
            root.GetProperty("model").GetString().Should().Be("gpt-4o");
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", prevPreset);
            Environment.SetEnvironmentVariable("LOCO_LLM__MODEL", prevModel);
        }
    }

    [Fact]
    public async Task GetConfig_NoApiKey_ReturnsHasApiKeyFalseAndEmptyApiKey()
    {
        // Arrange: ensure no API key is set and no preset
        SetEnv("LOCO_LLM__APIKEY", null, out var prevKey);
        SetEnv("LOCO_LLM__PRESET", null, out var prevPreset);

        try
        {
            await using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();

            // Act
            var resp = await client.GetAsync("/api/llm/config");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            root.GetProperty("hasApiKey").GetBoolean().Should().BeFalse();
            root.GetProperty("apiKey").GetString().Should().Be("");
            root.GetProperty("preset").ValueKind.Should().Be(JsonValueKind.Null);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCO_LLM__APIKEY", prevKey);
            Environment.SetEnvironmentVariable("LOCO_LLM__PRESET", prevPreset);
        }
    }
}
