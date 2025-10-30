using System;
using Loco.Core.Configuration;
using Xunit;
using FluentAssertions;

namespace Loco.Core.Tests.Configuration;

/// <summary>
/// Tests for LocoConfig - Configuration management
/// </summary>
public class LocoConfigTests
{
    #region Initialization Tests

    [Fact]
    public void LocoConfig_DefaultConstructor_InitializesWithDefaults()
    {
        // Act
        var config = new LocoConfig();

        // Assert
        config.Should().NotBeNull();
        config.MaxConcurrentFlows.Should().Be(5);
        config.EnableAutoBackup.Should().BeFalse();
    }

    #endregion

    #region Property Access Tests

    [Fact]
    public void MaxConcurrentFlows_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();

        // Act
        config.MaxConcurrentFlows = 10;

        // Assert
        config.MaxConcurrentFlows.Should().Be(10);
    }

    [Fact]
    public void WorkingDirectory_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();
        var testPath = "/test/path";

        // Act
        config.WorkingDirectory = testPath;

        // Assert
        config.WorkingDirectory.Should().Be(testPath);
    }

    [Fact]
    public void LogLevel_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();

        // Act
        config.LogLevel = "Debug";

        // Assert
        config.LogLevel.Should().Be("Debug");
    }

    [Fact]
    public void DefaultTimeoutSeconds_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();

        // Act
        config.DefaultTimeoutSeconds = 60;

        // Assert
        config.DefaultTimeoutSeconds.Should().Be(60);
    }

    #endregion

    #region Logging Configuration Tests

    [Fact]
    public void EnableFileLogging_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();

        // Act
        config.EnableFileLogging = true;

        // Assert
        config.EnableFileLogging.Should().BeTrue();
    }

    [Fact]
    public void EnableConsoleLogging_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();

        // Act
        config.EnableConsoleLogging = true;

        // Assert
        config.EnableConsoleLogging.Should().BeTrue();
    }

    [Fact]
    public void LogRetentionDays_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();

        // Act
        config.LogRetentionDays = 30;

        // Assert
        config.LogRetentionDays.Should().Be(30);
    }

    #endregion

    #region Security Configuration Tests

    [Fact]
    public void AllowedPaths_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();
        var paths = new[] { "/home/user", "/var/data" };

        // Act
        config.AllowedPaths = paths;

        // Assert
        config.AllowedPaths.Should().Equal(paths);
    }

    [Fact]
    public void ForbiddenPaths_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();
        var paths = new[] { "/etc", "/sys" };

        // Act
        config.ForbiddenPaths = paths;

        // Assert
        config.ForbiddenPaths.Should().Equal(paths);
    }

    [Fact]
    public void EnableInputValidation_DefaultsToTrue()
    {
        // Act
        var config = new LocoConfig();

        // Assert
        config.EnableInputValidation.Should().BeTrue();
    }

    #endregion

    #region Performance Configuration Tests

    [Fact]
    public void MemoryLimitMB_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();

        // Act
        config.MemoryLimitMB = 512;

        // Assert
        config.MemoryLimitMB.Should().Be(512);
    }

    [Fact]
    public void CacheSizeMB_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();

        // Act
        config.CacheSizeMB = 64;

        // Assert
        config.CacheSizeMB.Should().Be(64);
    }

    [Fact]
    public void MaxFileSizeBytes_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();
        long maxSize = 1024 * 1024 * 500; // 500 MB

        // Act
        config.MaxFileSizeBytes = maxSize;

        // Assert
        config.MaxFileSizeBytes.Should().Be(maxSize);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void ValidateConfiguration_WithValidConfig_DoesNotThrow()
    {
        // Arrange
        var config = new LocoConfig
        {
            MaxConcurrentFlows = 5,
            MemoryLimitMB = 256
        };

        // Act & Assert
        config.ValidateConfiguration();
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidMaxConcurrent_Throws()
    {
        // Arrange
        var config = new LocoConfig
        {
            MaxConcurrentFlows = 0 // Invalid: must be > 0
        };

        // Act & Assert
        config.Invoking(c => c.ValidateConfiguration())
            .Should().Throw<Loco.Core.Exceptions.LocoConfigurationException>();
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidMemoryLimit_Throws()
    {
        // Arrange
        var config = new LocoConfig
        {
            MemoryLimitMB = 0 // Invalid: must be > 0
        };

        // Act & Assert
        config.Invoking(c => c.ValidateConfiguration())
            .Should().Throw<Loco.Core.Exceptions.LocoConfigurationException>();
    }

    #endregion

    #region Diagnostic Tests

    [Fact]
    public void GetDiagnosticSnapshot_ReturnsSnapshot()
    {
        // Arrange
        var config = new LocoConfig();

        // Act
        var snapshot = config.GetDiagnosticSnapshot();

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetPathResolutionWarningsSnapshot_ReturnsWarnings()
    {
        // Arrange
        var config = new LocoConfig();

        // Act
        var warnings = config.GetPathResolutionWarningsSnapshot();

        // Assert
        warnings.Should().NotBeNull();
        warnings.Should().BeAssignableTo<System.Collections.Generic.IReadOnlyList<string>>();
    }

    [Fact]
    public void ClearPathResolutionWarnings_ClearsWarnings()
    {
        // Arrange
        var config = new LocoConfig();

        // Act
        config.ClearPathResolutionWarnings();

        // Assert
        config.HasPathResolutionWarnings.Should().BeFalse();
    }

    #endregion

    #region Circuit Breaker Configuration Tests

    [Fact]
    public void EnableCircuitBreaker_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();

        // Act
        config.EnableCircuitBreaker = true;

        // Assert
        config.EnableCircuitBreaker.Should().BeTrue();
    }

    [Fact]
    public void CircuitBreakerThreshold_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();

        // Act
        config.CircuitBreakerThreshold = 10;

        // Assert
        config.CircuitBreakerThreshold.Should().Be(10);
    }

    #endregion

    #region Security Features Tests

    [Fact]
    public void EnableSecureProcessExecution_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();

        // Act
        config.EnableSecureProcessExecution = true;

        // Assert
        config.EnableSecureProcessExecution.Should().BeTrue();
    }

    [Fact]
    public void MaxProcessMemoryMB_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();

        // Act
        config.MaxProcessMemoryMB = 1024;

        // Assert
        config.MaxProcessMemoryMB.Should().Be(1024);
    }

    [Fact]
    public void MaxProcessExecutionTime_CanBeSet()
    {
        // Arrange
        var config = new LocoConfig();
        var timeout = TimeSpan.FromMinutes(10);

        // Act
        config.MaxProcessExecutionTime = timeout;

        // Assert
        config.MaxProcessExecutionTime.Should().Be(timeout);
    }

    #endregion
}
