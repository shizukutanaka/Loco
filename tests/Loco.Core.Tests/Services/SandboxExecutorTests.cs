using System;
using System.Threading.Tasks;
using Loco.Core.Models;
using Loco.Core.Services;
using Xunit;

namespace Loco.Core.Tests.Services
{
    public class SandboxExecutorTests : IDisposable
    {
        private readonly SandboxExecutor _executor;

        public SandboxExecutorTests()
        {
            // Note: For real logging, inject a mock ILogger
            _executor = new SandboxExecutor(null);
        }

        public void Dispose()
        {
            _executor.Dispose();
        }

        [Fact]
        public async Task ExecuteAsync_Process_Successful()
        {
            // Arrange
            var request = new ExecutionRequest
            {
                Type = ExecutionType.Process,
                Command = "dotnet",
                Arguments = "--version"
            };

            // Act
            var result = await _executor.ExecuteAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(0, result.ExitCode);
            Assert.NotEmpty(result.Output);
            Assert.Empty(result.Error);
        }

        [Fact]
        public async Task ExecuteAsync_Process_Timeout()
        {
            // Arrange
            var request = new ExecutionRequest
            {
                Type = ExecutionType.Process,
                Command = "powershell",
                Arguments = "-Command Start-Sleep -Seconds 5",
                ResourceLimits = new ResourceLimits { TimeoutMs = 100 }
            };

            // Act
            var result = await _executor.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("timeout", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("rm -rf /")]
        [InlineData("del C:\\Windows")]
        public async Task ExecuteAsync_DangerousCommand_IsBlocked(string command)
        {
            // Arrange
            var request = new ExecutionRequest
            {
                Type = ExecutionType.Shell,
                Command = command,
                Permissions = new Permissions { Shell = true }
            };

            // Act
            var result = await _executor.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Dangerous command blocked", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExecuteAsync_Shell_PermissionDenied()
        {
            // Arrange
            var request = new ExecutionRequest
            {
                Type = ExecutionType.Shell,
                Command = "echo 'test'",
                Permissions = new Permissions { Shell = false } // Explicitly deny shell
            };

            // Act
            var result = await _executor.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Permission denied for requested operation", result.Error);
        }

        [Fact]
        public async Task ExecuteAsync_Script_Successful()
        {
            // Arrange
            var request = new ExecutionRequest
            {
                Type = ExecutionType.Script,
                Command = "Write-Host 'Success from script'"
            };

            // Act
            var result = await _executor.ExecuteAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("Success from script", result.Output);
            Assert.Empty(result.Error);
        }

        [Fact]
        public async Task ExecuteAsync_Script_With_Error()
        {
            // Arrange
            var request = new ExecutionRequest
            {
                Type = ExecutionType.Script,
                Command = "Write-Error 'This is a test error'; exit 1"
            };

            // Act
            var result = await _executor.ExecuteAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("This is a test error", result.Error);
        }
    }
}
