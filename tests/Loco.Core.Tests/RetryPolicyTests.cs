using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using FluentAssertions;
using Loco.Core.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Loco.Core.Tests
{
    public class RetryPolicyTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldCancel_WhenTokenCancelledBeforeOperation()
        {
            var policy = new RetryPolicyBuilder()
                .WithMaxRetries(3)
                .WithConstantDelay()
                .Build(NullLogger.Instance);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Func<CancellationToken, Task<int>> operation = _ => Task.FromResult(0);

            await policy.Invoking(p => p.ExecuteAsync(operation, "op", cts.Token))
                .Should()
                .ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task ExecuteAsync_ShouldCancelDuringDelay_WhenTokenCancelledBetweenAttempts()
        {
            var policy = new RetryPolicyBuilder()
                .WithMaxRetries(3)
                .WithDelay(TimeSpan.FromMilliseconds(50))
                .WithConstantDelay()
                .Build();

            using var cts = new CancellationTokenSource();
            var attempt = 0;

            Func<CancellationToken, Task<int>> operation = token =>
            {
                attempt++;
                if (attempt == 1)
                {
                    cts.Cancel();
                    throw new InvalidOperationException();
                }

                token.ThrowIfCancellationRequested();
                return Task.FromResult(42);
            };

            await policy.Invoking(p => p.ExecuteAsync(operation, "op", cts.Token))
                .Should()
                .ThrowAsync<OperationCanceledException>();
        }

        [Theory]
        [InlineData(-1, 1, 2)]
        [InlineData(1, -1, 2)]
        [InlineData(1, 2, -1)]
        public void RetryPolicyConfig_ShouldValidate_InvalidValues(int maxRetries, double initialSeconds, double maxSeconds)
        {
            var config = new RetryPolicyConfig
            {
                MaxRetries = maxRetries,
                InitialDelay = TimeSpan.FromSeconds(initialSeconds),
                MaxDelay = TimeSpan.FromSeconds(maxSeconds)
            };

            Action act = () => new RetryPolicy(config);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public async Task RetryPolicyBuilder_Build_ShouldCloneConfiguration()
        {
            var builder = new RetryPolicyBuilder()
                .WithMaxRetries(2)
                .WithDelay(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(50))
                .HandleException<TimeoutException>();

            var policy1 = builder.Build();

            var attemptsPolicy1 = 0;
            Func<Task<int>> action = () =>
            {
                attemptsPolicy1++;
                throw new ArgumentException("fail");
            };

            await policy1.Invoking(p => p.ExecuteAsync(action, "policy1"))
                .Should()
                .ThrowAsync<RetryException>();

            attemptsPolicy1.Should().Be(1);

            builder.HandleException<ArgumentException>().WithMaxRetries(1);

            var policy2 = builder.Build();

            var attemptsPolicy2 = 0;
            Func<Task<int>> action2 = () =>
            {
                attemptsPolicy2++;
                throw new ArgumentException("fail");
            };

            await policy2.Invoking(p => p.ExecuteAsync(action2, "policy2"))
                .Should()
                .ThrowAsync<RetryException>();

            attemptsPolicy2.Should().Be(2);

            policy1.Should().NotBeSameAs(policy2);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldInvokeOnRetryCallback()
        {
            var contexts = new List<RetryAttemptContext>();

            var policy = new RetryPolicyBuilder()
                .WithMaxRetries(2)
                .WithDelay(TimeSpan.FromMilliseconds(1))
                .OnRetryAsync((context, token) =>
                {
                    contexts.Add(context);
                    context.NextDelay = TimeSpan.Zero;
                    return Task.CompletedTask;
                })
                .Build();

            var attempts = 0;

            var result = await policy.ExecuteAsync(() =>
            {
                attempts++;
                if (attempts < 2)
                {
                    throw new InvalidOperationException("fail");
                }

                return Task.FromResult(42);
            }, "op");

            result.Should().Be(42);
            attempts.Should().Be(2);

            contexts.Should().HaveCount(1);
            var context = contexts.Single();
            context.FailedAttemptNumber.Should().Be(1);
            context.NextAttemptNumber.Should().Be(2);
            context.MaxAttempts.Should().Be(3);
            context.IsFinalAttempt.Should().BeFalse();
            context.LastException.Should().BeOfType<InvalidOperationException>();
            context.OperationName.Should().Be("op");
            context.NextDelay.Should().Be(TimeSpan.Zero);
        }
    }
}
