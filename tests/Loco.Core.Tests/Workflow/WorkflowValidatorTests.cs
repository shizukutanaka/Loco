using System.Collections.Generic;
using Loco.Core.Workflow;
using Xunit;

namespace Loco.Core.Tests.Workflow
{
    public class WorkflowValidatorTests
    {
        private readonly WorkflowValidator _validator = new();

        [Fact]
        public void Validate_ValidWorkflow_ShouldPass()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Id = "test-001",
                Name = "Test Workflow",
                Version = "1.0",
                Platforms = new List<string> { "windows" },
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
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction
                    {
                        Type = "notification",
                        Parameters = new Dictionary<string, object>
                        {
                            ["message"] = "Hello"
                        }
                    }
                }
            };

            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Validate_MissingId_ShouldFail()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Id = "",
                Name = "Test",
                Platforms = new List<string> { "windows" },
                Triggers = new List<WorkflowTrigger>
                {
                    new WorkflowTrigger { Type = "time" }
                },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction { Type = "notification" }
                }
            };

            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Workflow ID is required"));
        }

        [Fact]
        public void Validate_MissingName_ShouldFail()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Name = "",
                Platforms = new List<string> { "windows" },
                Triggers = new List<WorkflowTrigger>
                {
                    new WorkflowTrigger { Type = "time" }
                },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction { Type = "notification" }
                }
            };

            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Workflow name is required"));
        }

        [Fact]
        public void Validate_InvalidVersion_ShouldFail()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Version = "invalid",
                Name = "Test",
                Platforms = new List<string> { "windows" },
                Triggers = new List<WorkflowTrigger>
                {
                    new WorkflowTrigger { Type = "time" }
                },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction { Type = "notification" }
                }
            };

            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Invalid version format"));
        }

        [Fact]
        public void Validate_NoPlatforms_ShouldFail()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Name = "Test",
                Platforms = new List<string>(),
                Triggers = new List<WorkflowTrigger>
                {
                    new WorkflowTrigger { Type = "time" }
                },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction { Type = "notification" }
                }
            };

            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("At least one platform must be specified"));
        }

        [Fact]
        public void Validate_InvalidPlatform_ShouldFail()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Name = "Test",
                Platforms = new List<string> { "invalid_platform" },
                Triggers = new List<WorkflowTrigger>
                {
                    new WorkflowTrigger { Type = "time" }
                },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction { Type = "notification" }
                }
            };

            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Invalid platform"));
        }

        [Fact]
        public void Validate_NoTriggers_ShouldFail()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Name = "Test",
                Platforms = new List<string> { "windows" },
                Triggers = new List<WorkflowTrigger>(),
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction { Type = "notification" }
                }
            };

            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("At least one trigger is required"));
        }

        [Fact]
        public void Validate_NoActions_ShouldFail()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Name = "Test",
                Platforms = new List<string> { "windows" },
                Triggers = new List<WorkflowTrigger>
                {
                    new WorkflowTrigger { Type = "time" }
                },
                Actions = new List<WorkflowAction>()
            };

            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("At least one action is required"));
        }

        [Fact]
        public void Validate_UnsupportedTrigger_ShouldWarn()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Name = "Test",
                Platforms = new List<string> { "android" },
                Triggers = new List<WorkflowTrigger>
                {
                    new WorkflowTrigger { Type = "file_system" } // Not supported on Android
                },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction { Type = "notification" }
                }
            };

            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Contains("not supported on platform"));
        }

        [Fact]
        public void Validate_UnsupportedAction_ShouldWarn()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Name = "Test",
                Platforms = new List<string> { "android" },
                Triggers = new List<WorkflowTrigger>
                {
                    new WorkflowTrigger { Type = "time" }
                },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction { Type = "applescript" } // Not supported on Android
                }
            };

            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Contains("not supported on platform"));
        }

        [Fact]
        public void Validate_InvalidConstraintOperator_ShouldFail()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Name = "Test",
                Platforms = new List<string> { "windows" },
                Triggers = new List<WorkflowTrigger>
                {
                    new WorkflowTrigger { Type = "time" }
                },
                Constraints = new List<WorkflowConstraint>
                {
                    new WorkflowConstraint
                    {
                        Type = "battery",
                        Operator = "invalid_operator"
                    }
                },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction { Type = "notification" }
                }
            };

            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Invalid operator"));
        }

        [Fact]
        public void Validate_InvalidRetryAttempts_ShouldFail()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Name = "Test",
                Platforms = new List<string> { "windows" },
                Triggers = new List<WorkflowTrigger>
                {
                    new WorkflowTrigger { Type = "time" }
                },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction
                    {
                        Type = "notification",
                        Retry = new ActionRetryPolicy
                        {
                            MaxAttempts = 20 // Too many
                        }
                    }
                }
            };

            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Retry maxAttempts must be between 1 and 10"));
        }

        [Fact]
        public void Validate_FallbackStrategyWithoutAction_ShouldFail()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Name = "Test",
                Platforms = new List<string> { "windows" },
                Triggers = new List<WorkflowTrigger>
                {
                    new WorkflowTrigger { Type = "time" }
                },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction
                    {
                        Type = "notification",
                        OnError = new ActionErrorHandling
                        {
                            Strategy = "fallback",
                            FallbackAction = null
                        }
                    }
                }
            };

            // Act
            var result = _validator.Validate(workflow);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("Fallback strategy requires a fallback action"));
        }

        [Fact]
        public void ValidationResult_ToString_ShouldFormatCorrectly()
        {
            // Arrange
            var result = new ValidationResult();
            result.AddError("Error 1");
            result.AddError("Error 2");
            result.AddWarning("Warning 1");

            // Act
            var formatted = result.ToString();

            // Assert
            Assert.Contains("Validation failed", formatted);
            Assert.Contains("Error 1", formatted);
            Assert.Contains("Error 2", formatted);
            Assert.Contains("Warning 1", formatted);
        }
    }
}
