using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Loco.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loco.Core.Tests
{
    public class LlmServiceTimeoutTests
    {
        private sealed class DelayedHandler : HttpMessageHandler
        {
            private readonly TimeSpan _delay;
            public DelayedHandler(TimeSpan delay) => _delay = delay;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                await Task.Delay(_delay, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"text\":\"ok\",\"tokensUsed\":1}")
                };
            }
        }

        [Fact]
        public async Task GenerateTextAsync_ThrowsCanceled_WhenHttpTimeoutMs_ShorterThanHandlerDelay()
        {
            // Arrange: handler delays 1s; timeout set to 10ms
            var handler = new DelayedHandler(TimeSpan.FromSeconds(1));
            var httpClient = new HttpClient(handler);

            var cfg = new LlmConfiguration
            {
                ApiKey = "test",
                ApiEndpoint = "http://localhost/test",
                Model = "test-model",
                MaxTokens = 8,
                Temperature = 0,
                HttpTimeoutMs = 10
            };

            var service = new LlmService(
                NullLogger<LlmService>.Instance,
                Options.Create(cfg),
                httpClient);

            // Act + Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await service.GenerateTextAsync("hello", CancellationToken.None);
            });
        }

        [Fact]
        public async Task GenerateTextAsync_Respects_ExternalCancellation_BeforeTimeout()
        {
            // Arrange: long handler delay; generous HttpTimeoutMs; cancel externally first
            var handler = new DelayedHandler(TimeSpan.FromSeconds(2));
            var httpClient = new HttpClient(handler);

            var cfg = new LlmConfiguration
            {
                ApiKey = "test",
                ApiEndpoint = "http://localhost/test",
                Model = "test-model",
                MaxTokens = 8,
                Temperature = 0,
                HttpTimeoutMs = 5000
            };

            var service = new LlmService(
                NullLogger<LlmService>.Instance,
                Options.Create(cfg),
                httpClient);

            using var externalCts = new CancellationTokenSource();
            externalCts.CancelAfter(20); // cancel before 5s timeout

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await service.GenerateTextAsync("hello", externalCts.Token);
            });
        }
    }
}
