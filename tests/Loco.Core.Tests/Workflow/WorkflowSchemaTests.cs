using System;
using System.Collections.Generic;
using System.Linq;
using Loco.Core.Workflow;
using Xunit;

namespace Loco.Core.Tests.Workflow
{
    public class WorkflowSchemaTests
    {
        [Fact]
        public void WorkflowDefinition_ShouldInitializeWithDefaults()
        {
            // Arrange & Act
            var workflow = new WorkflowDefinition();

            // Assert
            Assert.NotNull(workflow.Id);
            Assert.Equal("1.0", workflow.Version);
            Assert.True(workflow.Enabled);
            Assert.NotNull(workflow.Platforms);
            Assert.NotNull(workflow.Triggers);
            Assert.NotNull(workflow.Constraints);
            Assert.NotNull(workflow.Actions);
        }

        [Fact]
        public void WorkflowTrigger_ShouldStoreTypeAndParameters()
        {
            // Arrange
            var trigger = new WorkflowTrigger
            {
                Type = "time",
                Parameters = new Dictionary<string, object>
                {
                    ["schedule"] = "0 9 * * *"
                },
                Description = "Daily at 9 AM"
            };

            // Assert
            Assert.Equal("time", trigger.Type);
            Assert.Equal("0 9 * * *", trigger.Parameters["schedule"]);
            Assert.Equal("Daily at 9 AM", trigger.Description);
        }

        [Fact]
        public void WorkflowConstraint_ShouldSupportOperators()
        {
            // Arrange
            var constraint = new WorkflowConstraint
            {
                Type = "battery",
                Operator = "greater_than",
                Value = 20
            };

            // Assert
            Assert.Equal("battery", constraint.Type);
            Assert.Equal("greater_than", constraint.Operator);
            Assert.Equal(20, constraint.Value);
        }

        [Fact]
        public void WorkflowAction_ShouldHaveUniqueId()
        {
            // Arrange & Act
            var action1 = new WorkflowAction { Type = "notification" };
            var action2 = new WorkflowAction { Type = "notification" };

            // Assert
            Assert.NotEqual(action1.Id, action2.Id);
        }

        [Fact]
        public void ActionErrorHandling_ShouldSupportStrategies()
        {
            // Arrange
            var errorHandling = new ActionErrorHandling
            {
                Strategy = "fallback",
                LogError = true,
                FallbackAction = new WorkflowAction
                {
                    Type = "notification",
                    Parameters = new Dictionary<string, object>
                    {
                        ["message"] = "Fallback executed"
                    }
                }
            };

            // Assert
            Assert.Equal("fallback", errorHandling.Strategy);
            Assert.True(errorHandling.LogError);
            Assert.NotNull(errorHandling.FallbackAction);
        }

        [Fact]
        public void ActionRetryPolicy_ShouldSupportBackoffStrategies()
        {
            // Arrange
            var retry = new ActionRetryPolicy
            {
                MaxAttempts = 3,
                DelayMs = 1000,
                BackoffStrategy = "exponential"
            };

            // Assert
            Assert.Equal(3, retry.MaxAttempts);
            Assert.Equal(1000, retry.DelayMs);
            Assert.Equal("exponential", retry.BackoffStrategy);
        }

        [Theory]
        [InlineData("android", "time", true)]
        [InlineData("android", "location", true)]
        [InlineData("android", "file_system", false)]
        [InlineData("ios", "time", true)]
        [InlineData("ios", "location", true)]
        [InlineData("ios", "file_system", false)]
        [InlineData("windows", "time", true)]
        [InlineData("windows", "file_system", true)]
        [InlineData("windows", "location", false)]
        public void PlatformCapabilities_ShouldCheckTriggerSupport(string platform, string triggerType, bool expected)
        {
            // Act
            var isSupported = PlatformCapabilities.IsTriggerSupported(platform, triggerType);

            // Assert
            Assert.Equal(expected, isSupported);
        }

        [Theory]
        [InlineData("android", "notification", true)]
        [InlineData("android", "wifi_toggle", true)]
        [InlineData("android", "applescript", false)]
        [InlineData("ios", "notification", true)]
        [InlineData("ios", "applescript", false)]
        [InlineData("mac", "notification", true)]
        [InlineData("mac", "applescript", true)]
        [InlineData("windows", "notification", true)]
        [InlineData("windows", "applescript", false)]
        public void PlatformCapabilities_ShouldCheckActionSupport(string platform, string actionType, bool expected)
        {
            // Act
            var isSupported = PlatformCapabilities.IsActionSupported(platform, actionType);

            // Assert
            Assert.Equal(expected, isSupported);
        }

        [Fact]
        public void PlatformCapabilities_ShouldIncludeAllMajorPlatforms()
        {
            // Arrange
            var expectedPlatforms = new[] { "android", "ios", "windows", "mac", "linux" };

            // Assert
            foreach (var platform in expectedPlatforms)
            {
                Assert.True(PlatformCapabilities.SupportedTriggers.ContainsKey(platform),
                    $"Platform '{platform}' should have supported triggers");
                Assert.True(PlatformCapabilities.SupportedActions.ContainsKey(platform),
                    $"Platform '{platform}' should have supported actions");
            }
        }

        [Fact]
        public void PlatformCapabilities_ShouldSupportCommonTriggers()
        {
            // Arrange
            var commonTriggers = new[] { "time", "http_request", "notification" };

            // Act & Assert
            foreach (var platform in new[] { "android", "ios", "windows", "mac", "linux" })
            {
                Assert.True(PlatformCapabilities.IsTriggerSupported(platform, "time"),
                    $"All platforms should support 'time' trigger");
            }
        }

        [Fact]
        public void PlatformCapabilities_ShouldSupportCommonActions()
        {
            // Arrange
            var platforms = new[] { "android", "ios", "windows", "mac", "linux" };

            // Act & Assert
            foreach (var platform in platforms)
            {
                Assert.True(PlatformCapabilities.IsActionSupported(platform, "notification"),
                    $"Platform '{platform}' should support 'notification' action");
                Assert.True(PlatformCapabilities.IsActionSupported(platform, "http_request"),
                    $"Platform '{platform}' should support 'http_request' action");
            }
        }
    }
}
