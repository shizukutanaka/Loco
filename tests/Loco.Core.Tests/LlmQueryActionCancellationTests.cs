using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Loco.Core.Actions;
using Loco.Core.Interfaces;
using Loco.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Loco.Core.Tests;

public class LlmQueryActionCancellationTests
{
    private class CancelAwareLlmService : ILlmService
    {
        public Task<string> GenerateTextAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);
            return Task.FromResult("ok");
        }

        public Task<string> GenerateTextAsync(string prompt, string? modelOverride, int? maxTokensOverride, double? temperatureOverride, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);
            return Task.FromResult("ok");
        }

        public Task<string> TranslateFlowToCodeAsync(string flowDescription, string targetLanguage = "csharp", CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);
            return Task.FromResult("ok");
        }
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_WhenTokenIsCanceled()
    {
        var action = new LlmQueryAction(new CancelAwareLlmService());
        var ctx = new ActionContext
        {
            Logger = NullLogger.Instance,
            Parameters = new Dictionary<string, object>
            {
                ["prompt"] = "hello"
            },
            Variables = new Dictionary<string, object>()
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await action.ExecuteAsync(ctx, cts.Token);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("LLM query canceled");
    }
}
