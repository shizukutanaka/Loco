using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Loco.Core.Interfaces;
using Loco.Llm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Sdk;

namespace Loco.Core.Tests;

public class LlmServiceIntegrationTests
{
    private static bool HasRequiredEnv()
    {
        // Minimal set for real call
        var apiKey = Environment.GetEnvironmentVariable("LOCO_LLM__APIKEY")
                     ?? Environment.GetEnvironmentVariable("LOCO_LLM_API_KEY");
        var endpoint = Environment.GetEnvironmentVariable("LOCO_LLM__APIENDPOINT")
                       ?? Environment.GetEnvironmentVariable("LOCO_LLM_API_ENDPOINT");
        var model = Environment.GetEnvironmentVariable("LOCO_LLM__MODEL")
                     ?? Environment.GetEnvironmentVariable("LOCO_LLM_MODEL");
        return !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(model);
    }

    private static ServiceProvider BuildProviderFromEnv()
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));

        services.AddOptions<LlmConfiguration>().Configure(options =>
        {
            var provider = Environment.GetEnvironmentVariable("LOCO_LLM__PROVIDER")
                           ?? Environment.GetEnvironmentVariable("LOCO_LLM_PROVIDER");
            if (!string.IsNullOrWhiteSpace(provider)) options.Provider = provider;

            var model = Environment.GetEnvironmentVariable("LOCO_LLM__MODEL")
                        ?? Environment.GetEnvironmentVariable("LOCO_LLM_MODEL");
            if (!string.IsNullOrWhiteSpace(model)) options.Model = model;

            var apiKey = Environment.GetEnvironmentVariable("LOCO_LLM__APIKEY")
                         ?? Environment.GetEnvironmentVariable("LOCO_LLM_API_KEY");
            if (!string.IsNullOrWhiteSpace(apiKey)) options.ApiKey = apiKey;

            var endpoint = Environment.GetEnvironmentVariable("LOCO_LLM__APIENDPOINT")
                           ?? Environment.GetEnvironmentVariable("LOCO_LLM_API_ENDPOINT");
            if (!string.IsNullOrWhiteSpace(endpoint)) options.ApiEndpoint = endpoint;

            var maxTok = Environment.GetEnvironmentVariable("LOCO_LLM__MAXTOKENS")
                         ?? Environment.GetEnvironmentVariable("LOCO_LLM_MAX_TOKENS");
            if (int.TryParse(maxTok, out var mt)) options.MaxTokens = mt;

            var temp = Environment.GetEnvironmentVariable("LOCO_LLM__TEMPERATURE")
                       ?? Environment.GetEnvironmentVariable("LOCO_LLM_TEMPERATURE");
            if (double.TryParse(temp, out var tp)) options.Temperature = tp;

            var timeout = Environment.GetEnvironmentVariable("LOCO_LLM__HTTPTIMEOUTMS")
                          ?? Environment.GetEnvironmentVariable("LOCO_LLM_HTTPTIMEOUTMS")
                          ?? Environment.GetEnvironmentVariable("LOCO_LLM_HTTP_TIMEOUT_MS");
            if (int.TryParse(timeout, out var toMs)) options.HttpTimeoutMs = toMs;
        });

        services.AddHttpClient<ILlmService, LlmService>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task GenerateTextAsync_WithRealService_Succeeds_WhenEnvConfigured()
    {
        if (!HasRequiredEnv())
            throw new SkipException("Real LLM integration test skipped: required env vars not set.");

        using var provider = BuildProviderFromEnv();
        var llm = provider.GetRequiredService<ILlmService>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var text = await llm.GenerateTextAsync(
            prompt: "Return one short word.",
            modelOverride: null,
            maxTokensOverride: 8,
            temperatureOverride: 0,
            cancellationToken: cts.Token);

        text.Should().NotBeNull();
        text.Trim().Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateTextAsync_Cancels_WhenTimeoutExceeded()
    {
        if (!HasRequiredEnv())
            throw new SkipException("Real LLM integration test skipped: required env vars not set.");

        using var provider = BuildProviderFromEnv();
        var llm = provider.GetRequiredService<ILlmService>();

        using var cts = new CancellationTokenSource(millisecondsDelay: 1);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await llm.GenerateTextAsync(
                prompt: "This call should be canceled.",
                modelOverride: null,
                maxTokensOverride: 16,
                temperatureOverride: 0.1,
                cancellationToken: cts.Token);
        });
    }
}
