using System.Collections.Generic;
using System.Threading.Tasks;
using Loco.Core.Models;
using Loco.Core.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Loco.Core.Tests.Validation
{
    public class ComprehensiveValidatorTests
    { 
        private readonly ComprehensiveValidator _validator;

        public ComprehensiveValidatorTests()
        {
            _validator = new ComprehensiveValidator(new NullLogger<ComprehensiveValidator>());
        }

        [Fact]
        public async Task ValidateAutomationRuleAsync_WithValidRule_ReturnsValid()
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule",
                Name = "Test Rule",
                Trigger = new AutomationDsl.TriggerDefinition { Type = "manual" },
                Actions = new List<AutomationDsl.ActionDefinition>
                {
                    new AutomationDsl.ActionDefinition { Type = "log", Parameters = new Dictionary<string, object> { { "message", "Hello" } } }
                }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task ValidateAutomationRuleAsync_WithNullRule_ReturnsInvalid()
        {
            // Arrange
            AutomationDsl.Rule rule = null;

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Equal("Rule", result.Errors[0].Field);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task ValidateAutomationRuleAsync_WithMissingId_ReturnsInvalid(string id)
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = id,
                Name = "Test Rule",
                Trigger = new AutomationDsl.TriggerDefinition { Type = "manual" },
                Actions = new List<AutomationDsl.ActionDefinition>
                {
                    new AutomationDsl.ActionDefinition { Type = "log" }
                }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Field == "Id");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task ValidateAutomationRuleAsync_WithMissingName_ReturnsInvalid(string name)
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule",
                Name = name,
                Trigger = new AutomationDsl.TriggerDefinition { Type = "manual" },
                Actions = new List<AutomationDsl.ActionDefinition>
                {
                    new AutomationDsl.ActionDefinition { Type = "log" }
                }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Field == "Name");
        }

        [Fact]
        public async Task ValidateAutomationRuleAsync_WithMissingTrigger_ReturnsInvalid()
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule",
                Name = "Test Rule",
                Trigger = null,
                Actions = new List<AutomationDsl.ActionDefinition>
                {
                    new AutomationDsl.ActionDefinition { Type = "log" }
                }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Field == "Trigger");
        }

        [Fact]
        public async Task ValidateAutomationRuleAsync_WithMissingActions_ReturnsInvalid()
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule",
                Name = "Test Rule",
                Trigger = new AutomationDsl.TriggerDefinition { Type = "manual" },
                Actions = new List<AutomationDsl.ActionDefinition>()
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Field == "Actions");
        }

        [Theory]
        [InlineData("* * * * *", true)]
        [InlineData("0 0 1 * *", true)]
        [InlineData("invalid-cron", false)]
        [InlineData("* * * *", false)] // Too few parts
        public async Task ValidateAutomationRuleAsync_TimeTrigger_WithCron_Validation(string cron, bool isValid)
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule",
                Name = "Test Rule",
                Trigger = new AutomationDsl.TriggerDefinition 
                {
                    Type = "time",
                    Parameters = new Dictionary<string, object> { { "cron", cron } }
                },
                Actions = new List<AutomationDsl.ActionDefinition> { new AutomationDsl.ActionDefinition { Type = "log" } }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            Assert.Equal(isValid, result.IsValid);
            if (!isValid)
            {
                Assert.Contains(result.Errors, e => e.Field == "Trigger.Parameters.cron");
            }
        }

        [Theory]
        [InlineData(1000, true)]
        [InlineData(86400000, true)]
        [InlineData(999, false)]
        [InlineData(86400001, false)]
        [InlineData("not-a-number", false)]
        public async Task ValidateAutomationRuleAsync_TimeTrigger_WithInterval_Validation(object interval, bool isValid)
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule",
                Name = "Test Rule",
                Trigger = new AutomationDsl.TriggerDefinition
                {
                    Type = "time",
                    Parameters = new Dictionary<string, object> { { "interval", interval } }
                },
                Actions = new List<AutomationDsl.ActionDefinition> { new AutomationDsl.ActionDefinition { Type = "log" } }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            Assert.Equal(isValid, result.IsValid);
            if (!isValid)
            {
                Assert.Contains(result.Errors, e => e.Field.StartsWith("Trigger.Parameters.interval"));
            }
        }

        [Theory]
        [InlineData("http://example.com", true)]
        [InlineData("https://example.com", true)]
        [InlineData("ftp://example.com", false)]
        [InlineData("not-a-url", false)]
        public async Task ValidateAutomationRuleAsync_HttpTrigger_WithUrl_Validation(string url, bool isValid)
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule",
                Name = "Test Rule",
                Trigger = new AutomationDsl.TriggerDefinition
                {
                    Type = "http",
                    Parameters = new Dictionary<string, object> { { "url", url } }
                },
                Actions = new List<AutomationDsl.ActionDefinition> { new AutomationDsl.ActionDefinition { Type = "log" } }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            Assert.Equal(isValid, result.IsValid);
            if (!isValid)
            {
                Assert.Contains(result.Errors, e => e.Field == "Trigger.Parameters.url");
            }
        }

        [Theory]
        [InlineData("GET", true)]
        [InlineData("POST", true)]
        [InlineData("INVALID", false)]
        public async Task ValidateAutomationRuleAsync_HttpTrigger_WithMethod_Validation(string method, bool isValid)
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule",
                Name = "Test Rule",
                Trigger = new AutomationDsl.TriggerDefinition
                {
                    Type = "http",
                    Parameters = new Dictionary<string, object> 
                    { 
                        { "url", "http://example.com" },
                        { "method", method }
                    }
                },
                Actions = new List<AutomationDsl.ActionDefinition> { new AutomationDsl.ActionDefinition { Type = "log" } }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            Assert.Equal(isValid, result.IsValid);
            if (!isValid)
            {
                Assert.Contains(result.Errors, e => e.Field == "Trigger.Parameters.method");
            }
        }

        [Fact]
        public async Task ValidateAutomationRuleAsync_FileTrigger_MissingPath_ReturnsInvalid()
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule",
                Name = "Test Rule",
                Trigger = new AutomationDsl.TriggerDefinition
                {
                    Type = "file",
                    Parameters = new Dictionary<string, object> { { "event", "created" } }
                },
                Actions = new List<AutomationDsl.ActionDefinition> { new AutomationDsl.ActionDefinition { Type = "log" } }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Field == "Trigger.Parameters.path");
        }

        [Theory]
        [InlineData("C:/Users/../test.txt")]
        [InlineData("~/test.txt")]
        public async Task ValidateAutomationRuleAsync_FileTrigger_PathTraversal_ReturnsInvalid(string path)
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule",
                Name = "Test Rule",
                Trigger = new AutomationDsl.TriggerDefinition
                {
                    Type = "file",
                    Parameters = new Dictionary<string, object> { { "path", path } }
                },
                Actions = new List<AutomationDsl.ActionDefinition> { new AutomationDsl.ActionDefinition { Type = "log" } }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Field == "Trigger.Parameters.path" && e.Message.Contains("dangerous"));
        }

        [Fact]
        public async Task ValidateAutomationRuleAsync_FileTrigger_InvalidEvent_ReturnsInvalid()
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule",
                Name = "Test Rule",
                Trigger = new AutomationDsl.TriggerDefinition
                {
                    Type = "file",
                    Parameters = new Dictionary<string, object> 
                    { 
                        { "path", "C:/temp/test.txt" },
                        { "event", "invalid-event" }
                    }
                },
                Actions = new List<AutomationDsl.ActionDefinition> { new AutomationDsl.ActionDefinition { Type = "log" } }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Field == "Trigger.Parameters.event");
        }

        [Theory]
        [InlineData("SELECT * FROM Users", true)]
        [InlineData("DROP TABLE Users;", true)]
        [InlineData("Just a normal message", false)]
        public async Task ValidateAutomationRuleAsync_Action_SqlInjection_ReturnsInvalid(string message, bool shouldBeInvalid)
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule",
                Name = "Test Rule",
                Trigger = new AutomationDsl.TriggerDefinition { Type = "manual" },
                Actions = new List<AutomationDsl.ActionDefinition>
                {
                    new AutomationDsl.ActionDefinition 
                    {
                        Type = "log", 
                        Parameters = new Dictionary<string, object> { { "message", message } }
                    }
                }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            Assert.Equal(!shouldBeInvalid, result.IsValid);
            if (shouldBeInvalid)
            {
                Assert.Contains(result.Errors, e => e.Field == "Action.Parameters.message" && e.Message.Contains("SQL"));
            }
        }

        [Theory]
        [InlineData("<script>alert('xss')</script>", true)]
        [InlineData("javascript:doSomething()", true)]
        [InlineData("Normal text with < and > characters", false)]
        public async Task ValidateAutomationRuleAsync_Action_ScriptInjection_ReturnsInvalid(string message, bool shouldBeInvalid)
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule",
                Name = "Test Rule",
                Trigger = new AutomationDsl.TriggerDefinition { Type = "manual" },
                Actions = new List<AutomationDsl.ActionDefinition>
                {
                    new AutomationDsl.ActionDefinition
                    {
                        Type = "log",
                        Parameters = new Dictionary<string, object> { { "message", message } }
                    }
                }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            Assert.Equal(!shouldBeInvalid, result.IsValid);
            if (shouldBeInvalid)
            {
                Assert.Contains(result.Errors, e => e.Field == "Action.Parameters.message" && e.Message.Contains("script"));
            }
        }
    }
}
