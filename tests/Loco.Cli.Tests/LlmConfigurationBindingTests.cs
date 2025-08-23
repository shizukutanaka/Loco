using System;
using System.Threading.Tasks;
using Loco.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loco.Cli.Tests
{
    public class LlmConfigurationBindingTests
    {
        private static IHost BuildHost()
        {
            return Host.CreateDefaultBuilder(Array.Empty<string>())
                .ConfigureAppConfiguration(cfg =>
                {
                    cfg.AddEnvironmentVariables(prefix: "LOCO_");
                })
                .ConfigureServices((ctx, services) =>
                {
                    services.AddOptions<LlmConfiguration>()
                        .Bind(ctx.Configuration.GetSection("Llm"));

                    // Legacy env var fallbacks to match CLI/UI/Web behavior
                    services.PostConfigure<LlmConfiguration>(options =>
                    {
                        if (string.IsNullOrWhiteSpace(options.ApiKey))
                        {
                            var apiKey = Environment.GetEnvironmentVariable("LOCO_LLM_API_KEY")
                                         ?? Environment.GetEnvironmentVariable("LOCO_LLM_APIKEY");
                            if (!string.IsNullOrWhiteSpace(apiKey)) options.ApiKey = apiKey;
                        }
                        if (string.IsNullOrWhiteSpace(options.ApiEndpoint))
                        {
                            var endpoint = Environment.GetEnvironmentVariable("LOCO_LLM_API_ENDPOINT")
                                           ?? Environment.GetEnvironmentVariable("LOCO_LLM_APIENDPOINT");
                            if (!string.IsNullOrWhiteSpace(endpoint)) options.ApiEndpoint = endpoint;
                        }
                        if (options.MaxTokens <= 0)
                        {
                            var maxTokens = Environment.GetEnvironmentVariable("LOCO_LLM_MAX_TOKENS")
                                            ?? Environment.GetEnvironmentVariable("LOCO_LLM_MAXTOKENS");
                            if (int.TryParse(maxTokens, out var mt)) options.MaxTokens = mt;
                        }
                        if (options.HttpTimeoutMs <= 0)
                        {
                            var httpTimeout = Environment.GetEnvironmentVariable("LOCO_LLM_HTTPTIMEOUTMS")
                                              ?? Environment.GetEnvironmentVariable("LOCO_LLM_HTTP_TIMEOUT_MS");
                            if (int.TryParse(httpTimeout, out var toMs)) options.HttpTimeoutMs = toMs;
                        }
                    });
                })
                .Build();
        }

        [Fact]
        public async Task Binds_DoubleUnderscore_HttpTimeoutMs()
        {
            var varName = "LOCO_LLM__HTTPTIMEOUTMS";
            var old = Environment.GetEnvironmentVariable(varName);
            try
            {
                Environment.SetEnvironmentVariable(varName, "1234");
                using var host = BuildHost();
                await host.StartAsync();
                var cfg = host.Services.GetRequiredService<IOptions<LlmConfiguration>>().Value;
                Assert.Equal(1234, cfg.HttpTimeoutMs);
                await host.StopAsync();
            }
            finally
            {
                Environment.SetEnvironmentVariable(varName, old);
            }
        }

        [Fact]
        public async Task Binds_LegacySingleUnderscore_HttpTimeoutMs()
        {
            var primary = "LOCO_LLM__HTTPTIMEOUTMS";
            var fallback = "LOCO_LLM_HTTPTIMEOUTMS";
            var oldPrimary = Environment.GetEnvironmentVariable(primary);
            var oldFallback = Environment.GetEnvironmentVariable(fallback);
            try
            {
                Environment.SetEnvironmentVariable(primary, null);
                Environment.SetEnvironmentVariable(fallback, "4321");
                using var host = BuildHost();
                await host.StartAsync();
                var cfg = host.Services.GetRequiredService<IOptions<LlmConfiguration>>().Value;
                Assert.Equal(4321, cfg.HttpTimeoutMs);
                await host.StopAsync();
            }
            finally
            {
                Environment.SetEnvironmentVariable(primary, oldPrimary);
                Environment.SetEnvironmentVariable(fallback, oldFallback);
            }
        }
    }
}
