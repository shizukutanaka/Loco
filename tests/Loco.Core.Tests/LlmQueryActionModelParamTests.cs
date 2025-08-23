using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Loco.Core.Actions;
using Loco.Core.Interfaces;
using Loco.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loco.Core.Tests
{
    public class LlmQueryActionModelParamTests
    {
        private class FakeLlmService : ILlmService
        {
            public Task<string> GenerateTextAsync(string prompt, CancellationToken cancellationToken = default)
                => Task.FromResult($"fake:{prompt}");

            public Task<string> GenerateTextAsync(string prompt, string? modelOverride, int? maxTokensOverride, double? temperatureOverride, CancellationToken cancellationToken = default)
                => Task.FromResult($"fake:{prompt}");
        }

        private class ListLogger<T> : ILogger<T>, IDisposable
        {
            public readonly List<string> Messages = new();

            public IDisposable BeginScope<TState>(TState state) => this;
            public void Dispose() { }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                try
                {
                    var msg = formatter?.Invoke(state, exception) ?? state?.ToString() ?? string.Empty;
                    Messages.Add($"{logLevel}: {msg}");
                }
                catch
                {
                    // best effort
                }
            }
        }

        [Fact]
        public async Task LlmQueryAction_Logs_Model_Params_When_Provided()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<ILlmService, FakeLlmService>();
            var sp = services.BuildServiceProvider();
            var action = ActivatorUtilities.CreateInstance(sp, typeof(LlmQueryAction)) as IAction;

            var logger = new ListLogger<LlmQueryAction>();
            var ctx = new ActionContext
            {
                Parameters = new Dictionary<string, object>
                {
                    ["prompt"] = "Test prompt",
                    ["modelId"] = "model-stable-xyz",
                    ["temperature"] = 0.15,
                    ["maxTokens"] = 256
                },
                Variables = new Dictionary<string, object>(),
                Logger = logger
            };

            // Act
            var result = await action!.ExecuteAsync(ctx, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            logger.Messages.Should().ContainSingle(m => m.Contains("LLM query executing with modelId=")
                                                        && m.Contains("model-stable-xyz")
                                                        && m.Contains("0.15")
                                                        && m.Contains("256"));
        }
    }
}
