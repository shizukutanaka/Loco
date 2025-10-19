using Loco.Core.IaC;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Loco.Core.Tests.IaC;

public class InfrastructureAsCodeTests
{
    private readonly InfrastructureAsCode _iac;
    private readonly ILogger<InfrastructureAsCode> _logger;

    public InfrastructureAsCodeTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<InfrastructureAsCode>();
        _iac = new InfrastructureAsCode(_logger);
    }

    [Fact]
    public void Validate_ValidInfrastructure_ReturnsValid()
    {
        // Arrange
        var infrastructure = new LocoInfrastructure
        {
            Version = "1.0",
            Workflows = new List<IaCWorkflow>
            {
                new IaCWorkflow
                {
                    Name = "test-workflow",
                    Steps = new List<IaCStep>
                    {
                        new IaCStep { Type = "shell" }
                    }
                }
            }
        };

        // Act
        var result = _iac.Validate(infrastructure);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_MissingVersion_ReturnsInvalid()
    {
        // Arrange
        var infrastructure = new LocoInfrastructure
        {
            Version = string.Empty,
            Workflows = new List<IaCWorkflow>
            {
                new IaCWorkflow
                {
                    Name = "test-workflow",
                    Steps = new List<IaCStep> { new IaCStep { Type = "shell" } }
                }
            }
        };

        // Act
        var result = _iac.Validate(infrastructure);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Version"));
    }

    [Fact]
    public void Validate_WorkflowWithoutName_ReturnsInvalid()
    {
        // Arrange
        var infrastructure = new LocoInfrastructure
        {
            Version = "1.0",
            Workflows = new List<IaCWorkflow>
            {
                new IaCWorkflow
                {
                    Name = string.Empty,
                    Steps = new List<IaCStep> { new IaCStep { Type = "shell" } }
                }
            }
        };

        // Act
        var result = _iac.Validate(infrastructure);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("name"));
    }

    [Fact]
    public void Validate_WorkflowWithoutSteps_ReturnsWarning()
    {
        // Arrange
        var infrastructure = new LocoInfrastructure
        {
            Version = "1.0",
            Workflows = new List<IaCWorkflow>
            {
                new IaCWorkflow
                {
                    Name = "test-workflow",
                    Steps = new List<IaCStep>()
                }
            }
        };

        // Act
        var result = _iac.Validate(infrastructure);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Validate_StepWithoutType_ReturnsInvalid()
    {
        // Arrange
        var infrastructure = new LocoInfrastructure
        {
            Version = "1.0",
            Workflows = new List<IaCWorkflow>
            {
                new IaCWorkflow
                {
                    Name = "test-workflow",
                    Steps = new List<IaCStep>
                    {
                        new IaCStep { Type = string.Empty }
                    }
                }
            }
        };

        // Act
        var result = _iac.Validate(infrastructure);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("type"));
    }

    [Fact]
    public void Validate_SecretWithoutName_ReturnsInvalid()
    {
        // Arrange
        var infrastructure = new LocoInfrastructure
        {
            Version = "1.0",
            Workflows = new List<IaCWorkflow>
            {
                new IaCWorkflow
                {
                    Name = "test-workflow",
                    Steps = new List<IaCStep> { new IaCStep { Type = "shell" } }
                }
            },
            Secrets = new List<IaCSecret>
            {
                new IaCSecret { Name = string.Empty, Source = "env:TEST" }
            }
        };

        // Act
        var result = _iac.Validate(infrastructure);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Secret name"));
    }

    [Fact]
    public void Validate_SecretWithoutSource_ReturnsInvalid()
    {
        // Arrange
        var infrastructure = new LocoInfrastructure
        {
            Version = "1.0",
            Workflows = new List<IaCWorkflow>
            {
                new IaCWorkflow
                {
                    Name = "test-workflow",
                    Steps = new List<IaCStep> { new IaCStep { Type = "shell" } }
                }
            },
            Secrets = new List<IaCSecret>
            {
                new IaCSecret { Name = "TEST_SECRET", Source = string.Empty }
            }
        };

        // Act
        var result = _iac.Validate(infrastructure);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("source"));
    }

    [Fact]
    public void Validate_MonitoringWithoutAlerts_ReturnsWarning()
    {
        // Arrange
        var infrastructure = new LocoInfrastructure
        {
            Version = "1.0",
            Workflows = new List<IaCWorkflow>
            {
                new IaCWorkflow
                {
                    Name = "test-workflow",
                    Steps = new List<IaCStep> { new IaCStep { Type = "shell" } }
                }
            },
            Monitoring = new IaCMonitoring
            {
                Enabled = true,
                Alerts = new List<IaCAlert>()
            }
        };

        // Act
        var result = _iac.Validate(infrastructure);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("alerts"));
    }

    [Fact]
    public async Task ApplyAsync_ValidInfrastructure_DeploysSuccessfully()
    {
        // Arrange
        var infrastructure = new LocoInfrastructure
        {
            Version = "1.0",
            Workflows = new List<IaCWorkflow>
            {
                new IaCWorkflow
                {
                    Name = "test-workflow",
                    Description = "Test workflow",
                    Steps = new List<IaCStep>
                    {
                        new IaCStep
                        {
                            Type = "shell",
                            Name = "test-step",
                            Config = new Dictionary<string, object>
                            {
                                { "command", "echo test" }
                            }
                        }
                    }
                }
            }
        };

        // Act
        var result = await _iac.ApplyAsync(infrastructure);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.DeployedResources);
        Assert.Contains(result.DeployedResources, r => r.Contains("test-workflow"));
    }

    [Fact]
    public async Task ApplyAsync_InvalidInfrastructure_FailsValidation()
    {
        // Arrange
        var infrastructure = new LocoInfrastructure
        {
            Version = string.Empty,  // Invalid
            Workflows = new List<IaCWorkflow>()
        };

        // Act
        var result = await _iac.ApplyAsync(infrastructure);

        // Assert
        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void GenerateFromExisting_CreatesInfrastructure()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create a test workflow file
            var workflowFile = Path.Combine(tempDir, "test-workflow.json");
            File.WriteAllText(workflowFile, "{}");

            // Act
            var infrastructure = _iac.GenerateFromExisting(tempDir);

            // Assert
            Assert.NotNull(infrastructure);
            Assert.Equal("1.0", infrastructure.Version);
            Assert.NotNull(infrastructure.Workflows);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SaveAndLoadYaml_RoundTrip_PreservesData()
    {
        // Arrange
        var infrastructure = new LocoInfrastructure
        {
            Version = "1.0",
            Workflows = new List<IaCWorkflow>
            {
                new IaCWorkflow
                {
                    Name = "test-workflow",
                    Description = "Test description",
                    Steps = new List<IaCStep>
                    {
                        new IaCStep { Type = "shell", Name = "step1" }
                    }
                }
            }
        };

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.yaml");

        try
        {
            // Act
            await _iac.SaveToYamlAsync(infrastructure, tempFile);
            var loaded = await _iac.LoadFromYamlAsync(tempFile);

            // Assert
            Assert.Equal(infrastructure.Version, loaded.Version);
            Assert.Equal(infrastructure.Workflows.Count, loaded.Workflows?.Count);
            Assert.Equal(infrastructure.Workflows[0].Name, loaded.Workflows?[0].Name);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadFromJson_ValidFile_LoadsSuccessfully()
    {
        // Arrange
        var json = @"{
            ""version"": ""1.0"",
            ""workflows"": [
                {
                    ""name"": ""test-workflow"",
                    ""steps"": [
                        { ""type"": ""shell"" }
                    ]
                }
            ]
        }";

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        await File.WriteAllTextAsync(tempFile, json);

        try
        {
            // Act
            var infrastructure = await _iac.LoadFromJsonAsync(tempFile);

            // Assert
            Assert.Equal("1.0", infrastructure.Version);
            Assert.NotNull(infrastructure.Workflows);
            Assert.Single(infrastructure.Workflows);
            Assert.Equal("test-workflow", infrastructure.Workflows[0].Name);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadFromYaml_FileNotFound_ThrowsException()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.yaml");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _iac.LoadFromYamlAsync(nonExistentFile));
    }

    [Fact]
    public async Task LoadFromJson_FileNotFound_ThrowsException()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _iac.LoadFromJsonAsync(nonExistentFile));
    }
}
