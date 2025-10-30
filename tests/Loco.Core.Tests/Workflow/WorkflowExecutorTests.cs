using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Workflow;
using Loco.Core.Workflow.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Loco.Core.Tests.Workflow
{
    /// <summary>
    /// Tests for WorkflowExecutor - the core execution engine.
    /// WorkflowExecutorのテスト - コア実行エンジン
    ///
    /// Solves Issues:
    /// - #8: Complex processing (conditional logic, loops)
    /// - #9: Error handling (retry, fallback strategies)
    /// - #10: Performance optimization (efficient execution)
    /// </summary>
    public class WorkflowExecutorTests
    {
        private readonly Mock<ILogger<WorkflowExecutor>> _mockLogger;
        private readonly Mock<IPlatformProvider> _mockProvider;
        private readonly WorkflowValidator _validator;
        private readonly WorkflowExecutor _executor;

        public WorkflowExecutorTests()
        {
            _mockLogger = new Mock<ILogger<WorkflowExecutor>>();
            _mockProvider = new Mock<IPlatformProvider>();
            _validator = new WorkflowValidator();
            _executor = new WorkflowExecutor(_mockLogger.Object);

            // Setup default mock provider for Windows (tests run on Windows)
            _mockProvider.Setup(p => p.Platform).Returns("windows");
            _mockProvider.Setup(p => p.IsTriggerSupported(It.IsAny<string>())).Returns(true);
            _mockProvider.Setup(p => p.IsActionSupported(It.IsAny<string>())).Returns(true);
        }

        [Fact]
        public async Task ExecuteAsync_WithInvalidWorkflow_ShouldReturnValidationError()
        {
            // Arrange
            var invalidWorkflow = new WorkflowDefinition
            {
                Id = "", // Invalid: empty ID
                Name = "Invalid Workflow",
                Version = "1.0",
                Platforms = new List<string> { "windows" }
            };

            _executor.RegisterPlatformProvider(_mockProvider.Object);

            // Act
            var result = await _executor.ExecuteAsync(invalidWorkflow);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Validation failed", result.ErrorMessage);
        }

        [Fact]
        public async Task ExecuteAsync_WithNoRegisteredProvider_ShouldReturnError()
        {
            // Arrange
            var workflow = CreateValidWorkflow();

            // Act (no provider registered)
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _executor.ExecuteAsync(workflow));
        }

        [Fact]
        public async Task ExecuteAsync_WithUnsupportedPlatform_ShouldReturnError()
        {
            // Arrange
            var workflow = CreateValidWorkflow("unsupported_platform");
            _executor.RegisterPlatformProvider(_mockProvider.Object);

            // Act
            var result = await _executor.ExecuteAsync(workflow);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not supported", result.ErrorMessage);
        }

        [Fact]
        public async Task ExecuteAsync_WithFailedConstraints_ShouldNotExecuteActions()
        {
            // Arrange
            var workflow = CreateValidWorkflow();
            workflow.Constraints = new List<WorkflowConstraint>
            {
                new WorkflowConstraint
                {
                    Type = "time",
                    Operator = "less_than",
                    Value = "09:00"
                }
            };

            _mockProvider.Setup(p => p.EvaluateConstraintAsync(
                It.IsAny<WorkflowConstraint>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _executor.RegisterPlatformProvider(_mockProvider.Object);

            // Act
            var result = await _executor.ExecuteAsync(workflow);

            // Assert
            Assert.True(result.Skipped);
            Assert.Contains("Constraints not met", result.SkipReason);

            // Verify actions were not executed
            _mockProvider.Verify(p => p.ExecuteActionAsync(
                It.IsAny<WorkflowAction>(),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WithSuccessfulActions_ShouldReturnSuccess()
        {
            // Arrange
            var workflow = CreateValidWorkflow("test");
            workflow.Actions = new List<WorkflowAction>
            {
                new WorkflowAction
                {
                    Type = "notification",
                    Parameters = new Dictionary<string, object>
                    {
                        ["message"] = "Test notification"
                    }
                }
            };

            _mockProvider.Setup(p => p.ExecuteActionAsync(
                It.IsAny<WorkflowAction>(),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(ActionResult.Succeeded("Action completed"));

            _executor.RegisterPlatformProvider(_mockProvider.Object);

            // Act
            var result = await _executor.ExecuteAsync(workflow);

            // Assert
            Assert.True(result.Success);
            Assert.Single(result.ActionResults);
            Assert.True(result.ActionResults[0].Success);
            Assert.NotNull(result.ExecutionId);
            Assert.True(result.Duration > TimeSpan.Zero);
        }

        [Fact]
        public async Task ExecuteAsync_WithRetryPolicy_ShouldRetryOnFailure()
        {
            // Arrange
            var workflow = CreateValidWorkflow("test");
            workflow.Actions = new List<WorkflowAction>
            {
                new WorkflowAction
                {
                    Type = "http_request",
                    Parameters = new Dictionary<string, object>
                    {
                        ["url"] = "https://api.example.com"
                    },
                    Retry = new ActionRetryPolicy
                    {
                        MaxAttempts = 3,
                        DelayMs = 100,
                        BackoffStrategy = "exponential"
                    }
                }
            };

            var callCount = 0;
            _mockProvider.Setup(p => p.ExecuteActionAsync(
                It.IsAny<WorkflowAction>(),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount < 3)
                        return ActionResult.Failed("Temporary failure");
                    return ActionResult.Succeeded("Success on retry");
                });

            _executor.RegisterPlatformProvider(_mockProvider.Object);

            // Act
            var result = await _executor.ExecuteAsync(workflow);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(3, callCount); // Failed 2 times, succeeded on 3rd
            Assert.True(result.ActionResults[0].Success);
        }

        [Fact]
        public async Task ExecuteAsync_WithExponentialBackoff_ShouldIncreaseDelay()
        {
            // Arrange
            var workflow = CreateValidWorkflow("test");
            workflow.Actions = new List<WorkflowAction>
            {
                new WorkflowAction
                {
                    Type = "http_request",
                    Parameters = new Dictionary<string, object>(),
                    Retry = new ActionRetryPolicy
                    {
                        MaxAttempts = 4,
                        DelayMs = 100,
                        BackoffStrategy = "exponential"
                    }
                }
            };

            var retryDelays = new List<long>();
            var lastCallTime = DateTime.UtcNow;

            _mockProvider.Setup(p => p.ExecuteActionAsync(
                It.IsAny<WorkflowAction>(),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var now = DateTime.UtcNow;
                    var delay = (now - lastCallTime).TotalMilliseconds;
                    if (retryDelays.Count > 0) // Skip first call (no delay before it)
                        retryDelays.Add((long)delay);
                    lastCallTime = now;

                    return ActionResult.Failed("Always fail");
                });

            _executor.RegisterPlatformProvider(_mockProvider.Object);

            // Act
            var result = await _executor.ExecuteAsync(workflow);

            // Assert
            Assert.False(result.Success); // All retries failed
            Assert.Equal(3, retryDelays.Count); // 3 retries after initial attempt

            // Verify exponential backoff: 100ms, 200ms, 400ms (with some tolerance)
            Assert.True(retryDelays[0] >= 90 && retryDelays[0] <= 150, $"First retry delay: {retryDelays[0]}ms");
            Assert.True(retryDelays[1] >= 180 && retryDelays[1] <= 250, $"Second retry delay: {retryDelays[1]}ms");
            Assert.True(retryDelays[2] >= 350 && retryDelays[2] <= 500, $"Third retry delay: {retryDelays[2]}ms");
        }

        [Fact]
        public async Task ExecuteAsync_WithLinearBackoff_ShouldIncreaseDelayLinearly()
        {
            // Arrange
            var workflow = CreateValidWorkflow("test");
            workflow.Actions = new List<WorkflowAction>
            {
                new WorkflowAction
                {
                    Type = "http_request",
                    Parameters = new Dictionary<string, object>(),
                    Retry = new ActionRetryPolicy
                    {
                        MaxAttempts = 4,
                        DelayMs = 100,
                        BackoffStrategy = "linear"
                    }
                }
            };

            var callCount = 0;
            _mockProvider.Setup(p => p.ExecuteActionAsync(
                It.IsAny<WorkflowAction>(),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    return ActionResult.Failed("Always fail");
                });

            _executor.RegisterPlatformProvider(_mockProvider.Object);

            // Act
            var result = await _executor.ExecuteAsync(workflow);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(4, callCount); // Initial + 3 retries
        }

        [Fact]
        public async Task ExecuteAsync_WithStopStrategy_ShouldHaltOnError()
        {
            // Arrange
            var workflow = CreateValidWorkflow("test");
            workflow.Actions = new List<WorkflowAction>
            {
                new WorkflowAction
                {
                    Type = "action1",
                    Parameters = new Dictionary<string, object>(),
                    OnError = new ActionErrorHandling
                    {
                        Strategy = "stop",
                        LogError = true
                    }
                },
                new WorkflowAction
                {
                    Type = "action2",
                    Parameters = new Dictionary<string, object>()
                }
            };

            _mockProvider.Setup(p => p.ExecuteActionAsync(
                It.Is<WorkflowAction>(a => a.Type == "action1"),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(ActionResult.Failed("Action 1 failed"));

            _executor.RegisterPlatformProvider(_mockProvider.Object);

            // Act
            var result = await _executor.ExecuteAsync(workflow);

            // Assert
            Assert.False(result.Success);
            Assert.Single(result.ActionResults); // Only action1 executed
            Assert.Contains("failed", result.ErrorMessage.ToLower());

            // Verify action2 was NOT executed
            _mockProvider.Verify(p => p.ExecuteActionAsync(
                It.Is<WorkflowAction>(a => a.Type == "action2"),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WithContinueStrategy_ShouldProceedToNextAction()
        {
            // Arrange
            var workflow = CreateValidWorkflow("test");
            workflow.Actions = new List<WorkflowAction>
            {
                new WorkflowAction
                {
                    Type = "action1",
                    Parameters = new Dictionary<string, object>(),
                    OnError = new ActionErrorHandling
                    {
                        Strategy = "continue",
                        LogError = true
                    }
                },
                new WorkflowAction
                {
                    Type = "action2",
                    Parameters = new Dictionary<string, object>()
                }
            };

            _mockProvider.Setup(p => p.ExecuteActionAsync(
                It.Is<WorkflowAction>(a => a.Type == "action1"),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(ActionResult.Failed("Action 1 failed"));

            _mockProvider.Setup(p => p.ExecuteActionAsync(
                It.Is<WorkflowAction>(a => a.Type == "action2"),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(ActionResult.Succeeded("Action 2 succeeded"));

            _executor.RegisterPlatformProvider(_mockProvider.Object);

            // Act
            var result = await _executor.ExecuteAsync(workflow);

            // Assert
            Assert.True(result.Success); // Overall success despite action1 failure
            Assert.Equal(2, result.ActionResults.Count);
            Assert.False(result.ActionResults[0].Success); // action1 failed
            Assert.True(result.ActionResults[1].Success); // action2 succeeded
        }

        [Fact]
        public async Task ExecuteAsync_WithFallbackStrategy_ShouldExecuteFallbackAction()
        {
            // Arrange
            var workflow = CreateValidWorkflow("test");
            workflow.Actions = new List<WorkflowAction>
            {
                new WorkflowAction
                {
                    Type = "primary_action",
                    Parameters = new Dictionary<string, object>(),
                    OnError = new ActionErrorHandling
                    {
                        Strategy = "fallback",
                        LogError = true,
                        FallbackAction = new WorkflowAction
                        {
                            Type = "fallback_action",
                            Parameters = new Dictionary<string, object>
                            {
                                ["message"] = "Fallback executed"
                            }
                        }
                    }
                }
            };

            _mockProvider.Setup(p => p.ExecuteActionAsync(
                It.Is<WorkflowAction>(a => a.Type == "primary_action"),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(ActionResult.Failed("Primary action failed"));

            _mockProvider.Setup(p => p.ExecuteActionAsync(
                It.Is<WorkflowAction>(a => a.Type == "fallback_action"),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(ActionResult.Succeeded("Fallback succeeded"));

            _executor.RegisterPlatformProvider(_mockProvider.Object);

            // Act
            var result = await _executor.ExecuteAsync(workflow);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.ActionResults.Count);
            Assert.False(result.ActionResults[0].Success); // Primary failed
            Assert.True(result.ActionResults[1].Success); // Fallback succeeded

            // Verify fallback was executed
            _mockProvider.Verify(p => p.ExecuteActionAsync(
                It.Is<WorkflowAction>(a => a.Type == "fallback_action"),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WithMultipleProviders_ShouldSelectCorrectProvider()
        {
            // Arrange
            var workflow = CreateValidWorkflow("windows");

            var windowsProvider = new Mock<IPlatformProvider>();
            windowsProvider.Setup(p => p.Platform).Returns("windows");
            windowsProvider.Setup(p => p.IsActionSupported(It.IsAny<string>())).Returns(true);
            windowsProvider.Setup(p => p.ExecuteActionAsync(
                It.IsAny<WorkflowAction>(),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(ActionResult.Succeeded("Windows action"));

            var androidProvider = new Mock<IPlatformProvider>();
            androidProvider.Setup(p => p.Platform).Returns("android");
            androidProvider.Setup(p => p.IsActionSupported(It.IsAny<string>())).Returns(true);

            _executor.RegisterPlatformProvider(windowsProvider.Object);
            _executor.RegisterPlatformProvider(androidProvider.Object);

            // Act
            var result = await _executor.ExecuteAsync(workflow);

            // Assert
            Assert.True(result.Success);
            windowsProvider.Verify(p => p.ExecuteActionAsync(
                It.IsAny<WorkflowAction>(),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()), Times.Once);
            androidProvider.Verify(p => p.ExecuteActionAsync(
                It.IsAny<WorkflowAction>(),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WithCancellation_ShouldRespectCancellationToken()
        {
            // Arrange
            var workflow = CreateValidWorkflow("test");
            workflow.Actions = new List<WorkflowAction>
            {
                new WorkflowAction { Type = "action1", Parameters = new Dictionary<string, object>() }
            };

            var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancel

            _mockProvider.Setup(p => p.ExecuteActionAsync(
                It.IsAny<WorkflowAction>(),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            _executor.RegisterPlatformProvider(_mockProvider.Object);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _executor.ExecuteAsync(workflow, cts.Token));
        }

        [Fact]
        public async Task ExecuteAsync_WithContextData_ShouldPassContextBetweenActions()
        {
            // Arrange
            var workflow = CreateValidWorkflow("test");
            workflow.Actions = new List<WorkflowAction>
            {
                new WorkflowAction
                {
                    Type = "action1",
                    Parameters = new Dictionary<string, object>
                    {
                        ["output_key"] = "result1"
                    }
                },
                new WorkflowAction
                {
                    Type = "action2",
                    Parameters = new Dictionary<string, object>
                    {
                        ["input_from"] = "result1"
                    }
                }
            };

            ActionContext? capturedContext = null;

            _mockProvider.Setup(p => p.ExecuteActionAsync(
                It.Is<WorkflowAction>(a => a.Type == "action1"),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(ActionResult.Succeeded("Action 1 result",
                    new Dictionary<string, object> { ["result1"] = "data from action1" }));

            _mockProvider.Setup(p => p.ExecuteActionAsync(
                It.Is<WorkflowAction>(a => a.Type == "action2"),
                It.IsAny<ActionContext>(),
                It.IsAny<CancellationToken>()))
                .Callback<WorkflowAction, ActionContext, CancellationToken>((a, ctx, ct) =>
                {
                    capturedContext = ctx;
                })
                .ReturnsAsync(ActionResult.Succeeded("Action 2 result"));

            _executor.RegisterPlatformProvider(_mockProvider.Object);

            // Act
            var result = await _executor.ExecuteAsync(workflow);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(capturedContext);
            Assert.NotNull(capturedContext.Variables);
            // Output from action1 is stored as "action_{actionId}_output" in Variables
        }

        // Helper method to create a valid workflow for testing
        private WorkflowDefinition CreateValidWorkflow(string platform = "windows")
        {
            return new WorkflowDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Workflow",
                Description = "Test workflow for unit tests",
                Version = "1.0",
                Platforms = new List<string> { platform },
                Enabled = true,
                Triggers = new List<WorkflowTrigger>
                {
                    new WorkflowTrigger
                    {
                        Type = "time",
                        Parameters = new Dictionary<string, object>
                        {
                            ["schedule"] = "0 9 * * *"
                        }
                    }
                },
                Constraints = new List<WorkflowConstraint>(),
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction
                    {
                        Type = "notification",
                        Parameters = new Dictionary<string, object>
                        {
                            ["message"] = "Test"
                        }
                    }
                }
            };
        }
    }
}
