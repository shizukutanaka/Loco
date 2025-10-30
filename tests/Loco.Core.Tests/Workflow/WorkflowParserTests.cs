using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Loco.Core.Workflow;
using Xunit;

namespace Loco.Core.Tests.Workflow
{
    public class WorkflowParserTests
    {
        private readonly WorkflowParser _parser = new();

        [Fact]
        public void ParseJson_ValidJson_ShouldSucceed()
        {
            // Arrange
            var json = @"{
                ""version"": ""1.0"",
                ""id"": ""test-001"",
                ""name"": ""Test Workflow"",
                ""platforms"": [""windows""],
                ""enabled"": true,
                ""triggers"": [
                    {
                        ""type"": ""time"",
                        ""parameters"": {
                            ""schedule"": ""0 9 * * *""
                        }
                    }
                ],
                ""actions"": [
                    {
                        ""type"": ""notification"",
                        ""parameters"": {
                            ""message"": ""Hello""
                        }
                    }
                ]
            }";

            // Act
            var workflow = _parser.ParseJson(json);

            // Assert
            Assert.NotNull(workflow);
            Assert.Equal("test-001", workflow.Id);
            Assert.Equal("Test Workflow", workflow.Name);
            Assert.Equal("1.0", workflow.Version);
            Assert.Single(workflow.Platforms);
            Assert.Equal("windows", workflow.Platforms[0]);
            Assert.Single(workflow.Triggers);
            Assert.Single(workflow.Actions);
        }

        [Fact]
        public void ParseJson_InvalidJson_ShouldThrow()
        {
            // Arrange
            var invalidJson = "{ invalid json }";

            // Act & Assert
            Assert.Throws<WorkflowParseException>(() => _parser.ParseJson(invalidJson));
        }

        [Fact]
        public void ParseJson_NullResult_ShouldThrow()
        {
            // Arrange
            var json = "null";

            // Act & Assert
            var ex = Assert.Throws<WorkflowParseException>(() => _parser.ParseJson(json));
            Assert.Contains("result was null", ex.Message);
        }

        [Fact]
        public void ToJson_ValidWorkflow_ShouldSerialize()
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
            var json = _parser.ToJson(workflow);

            // Assert
            Assert.NotNull(json);
            Assert.Contains("test-001", json);
            Assert.Contains("Test Workflow", json);
            Assert.Contains("windows", json);
        }

        [Fact]
        public void ParseAndValidate_ValidWorkflow_ShouldPass()
        {
            // Arrange
            var json = @"{
                ""version"": ""1.0"",
                ""id"": ""test-001"",
                ""name"": ""Test Workflow"",
                ""platforms"": [""windows""],
                ""triggers"": [
                    {
                        ""type"": ""time"",
                        ""parameters"": { ""schedule"": ""0 9 * * *"" }
                    }
                ],
                ""actions"": [
                    {
                        ""type"": ""notification"",
                        ""parameters"": { ""message"": ""Hello"" }
                    }
                ]
            }";

            // Act
            var (workflow, validation) = _parser.ParseAndValidate(json);

            // Assert
            Assert.NotNull(workflow);
            Assert.True(validation.IsValid);
            Assert.Empty(validation.Errors);
        }

        [Fact]
        public void ParseAndValidate_InvalidWorkflow_ShouldReturnErrors()
        {
            // Arrange
            var json = @"{
                ""version"": ""1.0"",
                ""id"": """",
                ""name"": """",
                ""platforms"": [],
                ""triggers"": [],
                ""actions"": []
            }";

            // Act
            var (workflow, validation) = _parser.ParseAndValidate(json);

            // Assert
            Assert.NotNull(workflow);
            Assert.False(validation.IsValid);
            Assert.NotEmpty(validation.Errors);
        }

        [Fact]
        public async Task ParseFileAsync_NonExistentFile_ShouldThrow()
        {
            // Arrange
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                _parser.ParseFileAsync(nonExistentPath));
        }

        [Fact]
        public async Task SaveAndParseFile_ShouldRoundTrip()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Id = "test-roundtrip",
                Name = "Round Trip Test",
                Version = "1.0",
                Platforms = new List<string> { "windows", "mac" },
                Triggers = new List<WorkflowTrigger>
                {
                    new WorkflowTrigger
                    {
                        Type = "time",
                        Parameters = new Dictionary<string, object>
                        {
                            ["schedule"] = "0 12 * * *"
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
                            ["title"] = "Reminder",
                            ["message"] = "Check your tasks"
                        }
                    }
                }
            };

            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");

            try
            {
                // Act
                await _parser.SaveFileAsync(workflow, tempFile);
                var loaded = await _parser.ParseFileAsync(tempFile);

                // Assert
                Assert.Equal(workflow.Id, loaded.Id);
                Assert.Equal(workflow.Name, loaded.Name);
                Assert.Equal(workflow.Version, loaded.Version);
                Assert.Equal(workflow.Platforms.Count, loaded.Platforms.Count);
                Assert.Equal(workflow.Triggers.Count, loaded.Triggers.Count);
                Assert.Equal(workflow.Actions.Count, loaded.Actions.Count);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void ParseJson_WithConstraints_ShouldDeserialize()
        {
            // Arrange
            var json = @"{
                ""version"": ""1.0"",
                ""id"": ""test-constraints"",
                ""name"": ""Test"",
                ""platforms"": [""android""],
                ""triggers"": [
                    { ""type"": ""time"" }
                ],
                ""constraints"": [
                    {
                        ""type"": ""battery"",
                        ""operator"": ""greater_than"",
                        ""value"": 20
                    }
                ],
                ""actions"": [
                    { ""type"": ""notification"" }
                ]
            }";

            // Act
            var workflow = _parser.ParseJson(json);

            // Assert
            Assert.NotNull(workflow.Constraints);
            Assert.Single(workflow.Constraints);
            Assert.Equal("battery", workflow.Constraints[0].Type);
            Assert.Equal("greater_than", workflow.Constraints[0].Operator);
            // JsonElement deserialized - need to extract actual value
            var valueElement = (System.Text.Json.JsonElement)workflow.Constraints[0].Value!;
            Assert.Equal(20, valueElement.GetInt32());
        }

        [Fact]
        public void ParseJson_WithErrorHandling_ShouldDeserialize()
        {
            // Arrange
            var json = @"{
                ""version"": ""1.0"",
                ""id"": ""test-error"",
                ""name"": ""Test"",
                ""platforms"": [""windows""],
                ""triggers"": [
                    { ""type"": ""time"" }
                ],
                ""actions"": [
                    {
                        ""type"": ""http_request"",
                        ""onError"": {
                            ""strategy"": ""fallback"",
                            ""logError"": true,
                            ""fallbackAction"": {
                                ""type"": ""notification"",
                                ""parameters"": {
                                    ""message"": ""API failed""
                                }
                            }
                        }
                    }
                ]
            }";

            // Act
            var workflow = _parser.ParseJson(json);

            // Assert
            Assert.NotNull(workflow.Actions);
            Assert.Single(workflow.Actions);
            Assert.NotNull(workflow.Actions[0].OnError);
            Assert.Equal("fallback", workflow.Actions[0].OnError.Strategy);
            Assert.True(workflow.Actions[0].OnError.LogError);
            Assert.NotNull(workflow.Actions[0].OnError.FallbackAction);
        }

        [Fact]
        public void ParseJson_WithRetryPolicy_ShouldDeserialize()
        {
            // Arrange
            var json = @"{
                ""version"": ""1.0"",
                ""id"": ""test-retry"",
                ""name"": ""Test"",
                ""platforms"": [""windows""],
                ""triggers"": [
                    { ""type"": ""time"" }
                ],
                ""actions"": [
                    {
                        ""type"": ""http_request"",
                        ""retry"": {
                            ""maxAttempts"": 3,
                            ""delayMs"": 1000,
                            ""backoffStrategy"": ""exponential""
                        }
                    }
                ]
            }";

            // Act
            var workflow = _parser.ParseJson(json);

            // Assert
            Assert.NotNull(workflow.Actions);
            Assert.Single(workflow.Actions);
            Assert.NotNull(workflow.Actions[0].Retry);
            Assert.Equal(3, workflow.Actions[0].Retry.MaxAttempts);
            Assert.Equal(1000, workflow.Actions[0].Retry.DelayMs);
            Assert.Equal("exponential", workflow.Actions[0].Retry.BackoffStrategy);
        }
    }
}
