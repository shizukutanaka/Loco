using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Loco.Core.Services;
using Loco.Core.Models;
using Loco.Core.Validation;

namespace Loco.Tests.Core.Services
{
    /// <summary>
    /// Tests for automation rule validation service
    /// </summary>
    public class ValidationServiceTests
    {
        private readonly ComprehensiveValidator _validator;
        private readonly Mock<ILogger<ComprehensiveValidator>> _loggerMock;

        public ValidationServiceTests()
        {
            _loggerMock = new Mock<ILogger<ComprehensiveValidator>>();
            _validator = new ComprehensiveValidator(_loggerMock.Object);
        }

        [Fact]
        public async Task ValidateRule_WithValidRule_ShouldReturnValid()
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule-1",
                Name = "Test Rule",
                Description = "A valid test rule",
                Enabled = true,
                Trigger = new AutomationDsl.TriggerDefinition
                {
                    Type = "manual",
                    Parameters = new Dictionary<string, object>()
                },
                Actions = new List<AutomationDsl.ActionDefinition>
                {
                    new AutomationDsl.ActionDefinition
                    {
                        Type = "log",
                        Parameters = new Dictionary<string, object>
                        {
                            ["message"] = "Test message"
                        }
                    }
                }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public async Task ValidateRule_WithNullRule_ShouldReturnInvalid()
        {
            // Act
            var result = await _validator.ValidateAutomationRuleAsync(null);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().Contain(e => e.Field == "Rule");
        }

        [Fact]
        public async Task ValidateRule_WithEmptyName_ShouldReturnInvalid()
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule-2",
                Name = "",
                Trigger = new AutomationDsl.TriggerDefinition { Type = "manual" }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Field == "Name");
        }

        [Fact]
        public async Task ValidateRule_WithoutTrigger_ShouldReturnInvalid()
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule-3",
                Name = "Test Rule",
                Trigger = null
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Field == "Trigger");
        }

        [Fact]
        public async Task ValidateRule_WithInvalidTriggerType_ShouldReturnInvalid()
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule-4",
                Name = "Test Rule",
                Trigger = new AutomationDsl.TriggerDefinition
                {
                    Type = "" // Empty trigger type
                }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Field == "Trigger.Type");
        }

        [Fact]
        public async Task ValidateRule_WithMultipleErrors_ShouldReturnAllErrors()
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "", // Invalid ID
                Name = "", // Invalid Name
                Trigger = null // Missing Trigger
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().HaveCountGreaterThan(2);
            result.Errors.Should().Contain(e => e.Field == "Id");
            result.Errors.Should().Contain(e => e.Field == "Name");
            result.Errors.Should().Contain(e => e.Field == "Trigger");
        }

        [Theory]
        [InlineData("very_long_name_that_exceeds_the_maximum_allowed_length_for_a_rule_name_and_should_fail_validation_because_it_is_way_too_long_to_be_practical_or_useful_in_any_real_world_scenario_and_would_cause_display_issues_in_the_user_interface_making_it_completely_unusable_for_normal_operations")]
        [InlineData("a")]
        public async Task ValidateRule_WithInvalidNameLength_ShouldReturnInvalid(string name)
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule-5",
                Name = name,
                Trigger = new AutomationDsl.TriggerDefinition { Type = "manual" }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Field == "Name");
        }

        [Fact]
        public async Task ValidateRule_WithCircularDependency_ShouldReturnInvalid()
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule-6",
                Name = "Circular Rule",
                Trigger = new AutomationDsl.TriggerDefinition
                {
                    Type = "ruleComplete",
                    Parameters = new Dictionary<string, object>
                    {
                        ["ruleId"] = "test-rule-6" // References itself
                    }
                }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Message.Contains("circular", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ValidateInputString_WithValidString_ShouldReturnValid()
        {
            // Act
            var result = await _validator.ValidateInputStringAsync("ValidString123", "TestField");

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateInputString_WithSqlInjection_ShouldReturnInvalid()
        {
            // Act
            var result = await _validator.ValidateInputStringAsync("'; DROP TABLE users; --", "TestField");

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Type == ValidationErrorType.Security);
        }

        [Fact]
        public async Task ValidateInputString_WithXssAttack_ShouldReturnInvalid()
        {
            // Act
            var result = await _validator.ValidateInputStringAsync("<script>alert('XSS')</script>", "TestField");

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Type == ValidationErrorType.Security);
        }

        [Fact]
        public async Task ValidateFilePath_WithValidPath_ShouldReturnValid()
        {
            // Act
            var result = await _validator.ValidateFilePathAsync(@"C:\Users\test\document.txt");

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateFilePath_WithPathTraversal_ShouldReturnInvalid()
        {
            // Act
            var result = await _validator.ValidateFilePathAsync(@"..\..\..\..\windows\system32\config");

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Type == ValidationErrorType.Security);
        }

        [Fact]
        public async Task ValidateEmail_WithValidEmail_ShouldReturnValid()
        {
            // Act
            var result = await _validator.ValidateEmailAsync("user@example.com");

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateEmail_WithInvalidEmail_ShouldReturnInvalid()
        {
            // Act
            var result = await _validator.ValidateEmailAsync("not-an-email");

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Type == ValidationErrorType.Format);
        }

        [Fact]
        public async Task ValidateUrl_WithValidUrl_ShouldReturnValid()
        {
            // Act
            var result = await _validator.ValidateUrlAsync("https://www.example.com/path");

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateUrl_WithMaliciousUrl_ShouldReturnInvalid()
        {
            // Act
            var result = await _validator.ValidateUrlAsync("javascript:alert('XSS')");

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Type == ValidationErrorType.Security);
        }

        [Fact]
        public async Task ValidateJson_WithValidJson_ShouldReturnValid()
        {
            // Act
            var result = await _validator.ValidateJsonAsync("{\"key\": \"value\"}");

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateJson_WithInvalidJson_ShouldReturnInvalid()
        {
            // Act
            var result = await _validator.ValidateJsonAsync("{invalid json}");

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Type == ValidationErrorType.Format);
        }

        [Theory]
        [InlineData(0, 100, 50, true)]
        [InlineData(0, 100, -1, false)]
        [InlineData(0, 100, 101, false)]
        [InlineData(0, 100, 0, true)]
        [InlineData(0, 100, 100, true)]
        public async Task ValidateNumberRange_ShouldValidateCorrectly(int min, int max, int value, bool expectedValid)
        {
            // Act
            var result = await _validator.ValidateNumberRangeAsync(value, min, max, "TestField");

            // Assert
            result.IsValid.Should().Be(expectedValid);
        }

        [Fact]
        public async Task ValidateBatch_WithMixedValidation_ShouldReturnCorrectResults()
        {
            // Arrange
            var validations = new List<Func<Task<ValidationResult>>>
            {
                () => _validator.ValidateInputStringAsync("valid", "Field1"),
                () => _validator.ValidateEmailAsync("invalid-email"),
                () => _validator.ValidateUrlAsync("https://valid.com")
            };

            // Act
            var results = await Task.WhenAll(validations.Select(v => v()));

            // Assert
            results.Should().HaveCount(3);
            results[0].IsValid.Should().BeTrue();
            results[1].IsValid.Should().BeFalse();
            results[2].IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateComplexRule_WithNestedConditions_ShouldValidateAllLevels()
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "complex-rule",
                Name = "Complex Rule",
                Trigger = new AutomationDsl.TriggerDefinition
                {
                    Type = "composite",
                    Parameters = new Dictionary<string, object>
                    {
                        ["conditions"] = new List<object>
                        {
                            new Dictionary<string, object> { ["type"] = "time", ["value"] = "10:00" },
                            new Dictionary<string, object> { ["type"] = "file", ["path"] = @"C:\test.txt" }
                        }
                    }
                },
                Conditions = new List<AutomationDsl.ConditionDefinition>
                {
                    new AutomationDsl.ConditionDefinition
                    {
                        Type = "fileExists",
                        Operator = "equals",
                        Value = true
                    }
                },
                Actions = new List<AutomationDsl.ActionDefinition>
                {
                    new AutomationDsl.ActionDefinition
                    {
                        Type = "email",
                        Parameters = new Dictionary<string, object>
                        {
                            ["to"] = "user@example.com",
                            ["subject"] = "Test",
                            ["body"] = "Message"
                        }
                    }
                }
            };

            // Act
            var result = await _validator.ValidateAutomationRuleAsync(rule);

            // Assert
            result.Should().NotBeNull();
            // Complex rules with all required fields should be valid
            result.IsValid.Should().BeTrue();
        }
    }
}
