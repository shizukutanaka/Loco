using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Loco.Core.Actions;
using Loco.Core.Models;
using Xunit;

namespace Loco.Core.Tests
{
    public class HttpRequestActionTests
    {
        private sealed class DelayedHandler : HttpMessageHandler
        {
            private readonly TimeSpan _delay;
            public DelayedHandler(TimeSpan delay) => _delay = delay;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                // Simulate a slow network/endpoint; honor cancellation
                await Task.Delay(_delay, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("delayed ok")
                };
            }
        }

        [Fact]
        public async Task ExecuteAsync_Cancels_WhenTimeoutMs_IsExceeded()
        {
            // Arrange: handler delays 1s; action timeout set to 10ms
            var httpClient = new HttpClient(new DelayedHandler(TimeSpan.FromSeconds(1)));
            var action = new HttpRequestAction(httpClient);

            var ctx = new ActionContext
            {
                Parameters = new Dictionary<string, object>
                {
                    ["url"] = "http://localhost/test",
                    ["method"] = "GET",
                    ["timeoutMs"] = "10"
                },
                Variables = new Dictionary<string, object>()
            };

            // Act
            var result = await action.ExecuteAsync(ctx, CancellationToken.None);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("timed out").Or.Contain("canceled");
        }
    }
}
