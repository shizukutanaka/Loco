using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Loco.Core.Actions;
using Loco.Core.Interfaces;
using Loco.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loco.Core.Tests
{
    public class ActionInstantiationTests
    {
        private class FakeLlmService : ILlmService
        {
            public Task<string> GenerateTextAsync(string prompt, CancellationToken cancellationToken = default)
                => Task.FromResult($"fake:{prompt}");

            public Task<string> GenerateTextAsync(string prompt, string? modelOverride, int? maxTokensOverride, double? temperatureOverride, CancellationToken cancellationToken = default)
                => Task.FromResult($"fake:{prompt}");
        }

        public static IEnumerable<object[]> ActionTypes => new List<object[]>
        {
            new object[] { typeof(NotificationAction) },
            new object[] { typeof(HttpRequestAction) },
            new object[] { typeof(FileAction) },
            new object[] { typeof(TtsAction) },
            new object[] { typeof(LaunchAppAction) },
            new object[] { typeof(LlmQueryAction) },
            new object[] { typeof(LogAction) }
        };

        [Theory]
        [MemberData(nameof(ActionTypes))]
        public void Actions_Can_Be_Instantiated_Via_ActivatorUtilities(Type actionType)
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpClient();
            services.AddSingleton<ILlmService, FakeLlmService>();
            var sp = services.BuildServiceProvider();

            // Act
            var instance = ActivatorUtilities.CreateInstance(sp, actionType) as IAction;

            // Assert
            instance.Should().NotBeNull();
            instance!.Id.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task LogAction_Execute_Works()
        {
            // Arrange
            var sc = new ServiceCollection();
            sc.AddSingleton<ILlmService, FakeLlmService>();
            var sp = sc.BuildServiceProvider();
            var action = ActivatorUtilities.CreateInstance(sp, typeof(LogAction)) as IAction;

            var context = new ActionContext
            {
                Parameters = new Dictionary<string, object>
                {
                    ["message"] = "Hello from test",
                    ["level"] = "Information"
                },
                Variables = new Dictionary<string, object>()
            };

            // Act
            var result = await action!.ExecuteAsync(context, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
        }

        [Fact]
        public async Task NotificationAction_Execute_Works()
        {
            // Arrange
            var sc = new ServiceCollection();
            sc.AddSingleton<ILlmService, FakeLlmService>();
            var sp = sc.BuildServiceProvider();
            var action = ActivatorUtilities.CreateInstance(sp, typeof(NotificationAction)) as IAction;

            var context = new ActionContext
            {
                Parameters = new Dictionary<string, object>
                {
                    ["title"] = "Test",
                    ["message"] = "World"
                },
                Variables = new Dictionary<string, object>()
            };

            // Act
            var result = await action!.ExecuteAsync(context, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
        }

        [Fact]
        public async Task FileAction_Write_And_Delete_Works()
        {
            // Arrange
            var sc = new ServiceCollection();
            sc.AddSingleton<ILlmService, FakeLlmService>();
            var sp = sc.BuildServiceProvider();
            var action = ActivatorUtilities.CreateInstance(sp, typeof(FileAction)) as IAction;
            var tempFile = Path.Combine(Path.GetTempPath(), $"loco_test_{Guid.NewGuid():N}.txt");

            try
            {
                // Write
                var writeCtx = new ActionContext
                {
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "write",
                        ["path"] = tempFile,
                        ["content"] = "hello"
                    },
                    Variables = new Dictionary<string, object>()
                };
                var writeRes = await action!.ExecuteAsync(writeCtx, CancellationToken.None);
                writeRes.Success.Should().BeTrue();
                File.Exists(tempFile).Should().BeTrue();

                // Read
                var readCtx = new ActionContext
                {
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "read",
                        ["path"] = tempFile
                    },
                    Variables = new Dictionary<string, object>()
                };
                var readRes = await action.ExecuteAsync(readCtx, CancellationToken.None);
                readRes.Success.Should().BeTrue();
                readRes.Data.Should().NotBeNull();
            }
            finally
            {
                // Delete (best effort)
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch { /* ignore */ }
            }
        }

        [Fact]
        public async Task LlmQueryAction_Execute_Works()
        {
            // Arrange
            var sc = new ServiceCollection();
            sc.AddSingleton<ILlmService, FakeLlmService>();
            var sp = sc.BuildServiceProvider();
            var action = ActivatorUtilities.CreateInstance(sp, typeof(LlmQueryAction)) as IAction;

            var context = new ActionContext
            {
                Parameters = new Dictionary<string, object>
                {
                    ["prompt"] = "Say hello"
                },
                Variables = new Dictionary<string, object>()
            };

            // Act
            var result = await action!.ExecuteAsync(context, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
        }
    }
}
