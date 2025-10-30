using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Workflow;
using Loco.Core.Workflow.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Loco.Core.Tests.Workflow.Providers
{
    /// <summary>
    /// Tests for WindowsPlatformProvider - Windows-specific implementation.
    /// WindowsPlatformProviderのテスト - Windows固有実装
    ///
    /// Solves Issues:
    /// - #1: Cross-platform support (Windows実装検証)
    /// - #8: Complex processing (各アクション種別の動作確認)
    /// - #10: Performance optimization (実行時間計測)
    /// </summary>
    public class WindowsPlatformProviderTests
    {
        private readonly Mock<ILogger<WindowsPlatformProvider>> _mockLogger;
        private readonly WindowsPlatformProvider _provider;

        public WindowsPlatformProviderTests()
        {
            _mockLogger = new Mock<ILogger<WindowsPlatformProvider>>();
            _provider = new WindowsPlatformProvider(_mockLogger.Object);
        }

        [Fact]
        public void Platform_ShouldReturnWindows()
        {
            // Act
            var platform = _provider.Platform;

            // Assert
            Assert.Equal("windows", platform);
        }

        [Theory]
        [InlineData("time", true)]
        [InlineData("file_system", true)]
        [InlineData("network", true)]
        [InlineData("http_request", true)]
        [InlineData("location", false)] // Not supported on Windows
        [InlineData("nfc", false)] // Not supported on Windows
        public void IsTriggerSupported_ShouldReturnCorrectSupport(string triggerType, bool expected)
        {
            // Act
            var isSupported = _provider.IsTriggerSupported(triggerType);

            // Assert
            Assert.Equal(expected, isSupported);
        }

        [Theory]
        [InlineData("notification", true)]
        [InlineData("run_program", true)]
        [InlineData("file_operation", true)]
        [InlineData("http_request", true)]
        [InlineData("clipboard", true)]
        [InlineData("powershell", true)]
        [InlineData("cmd", true)]
        [InlineData("applescript", false)] // Not supported on Windows
        [InlineData("wifi_toggle", false)] // Not supported on Windows
        public void IsActionSupported_ShouldReturnCorrectSupport(string actionType, bool expected)
        {
            // Act
            var isSupported = _provider.IsActionSupported(actionType);

            // Assert
            Assert.Equal(expected, isSupported);
        }

        [Fact]
        public async Task RegisterTriggerAsync_ShouldReturnActiveTriggerHandle()
        {
            // Arrange
            var trigger = new WorkflowTrigger
            {
                Type = "time",
                Parameters = new Dictionary<string, object>
                {
                    ["schedule"] = "0 9 * * *"
                }
            };

            Task callback(TriggerContext ctx) => Task.CompletedTask;

            // Act
            var handle = await _provider.RegisterTriggerAsync(trigger, callback);

            // Assert
            Assert.NotNull(handle);
            Assert.NotNull(handle.TriggerId);
            Assert.True(handle.IsActive);
        }

        [Fact]
        public async Task EvaluateConstraintAsync_WithTimeConstraint_ShouldEvaluateCorrectly()
        {
            // Arrange
            var now = DateTime.Now;
            var startTime = now.AddHours(-1).ToString("HH:mm");
            var endTime = now.AddHours(1).ToString("HH:mm");

            var constraint = new WorkflowConstraint
            {
                Type = "time",
                Value = $"{startTime}-{endTime}" // Current time is within range
            };

            // Act
            var result = await _provider.EvaluateConstraintAsync(constraint);

            // Assert
            Assert.True(result, $"Expected time range {startTime}-{endTime} to include current time {now:HH:mm}");
        }

        [Fact]
        public async Task EvaluateConstraintAsync_WithTimeConstraintOutOfRange_ShouldReturnFalse()
        {
            // Arrange
            var constraint = new WorkflowConstraint
            {
                Type = "time",
                Value = "01:00-02:00" // Unless running at 1-2 AM, this should fail
            };

            var now = DateTime.Now.TimeOfDay;
            var isInRange = now >= TimeSpan.Parse("01:00") && now <= TimeSpan.Parse("02:00");

            // Act
            var result = await _provider.EvaluateConstraintAsync(constraint);

            // Assert
            Assert.Equal(isInRange, result);
        }

        [Fact]
        public async Task EvaluateConstraintAsync_WithFileExistsConstraint_ShouldCheckFileExists()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                var constraint = new WorkflowConstraint
                {
                    Type = "file_exists",
                    Value = tempFile
                };

                // Act
                var result = await _provider.EvaluateConstraintAsync(constraint);

                // Assert
                Assert.True(result);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task EvaluateConstraintAsync_WithNonExistentFile_ShouldReturnFalse()
        {
            // Arrange
            var constraint = new WorkflowConstraint
            {
                Type = "file_exists",
                Value = "C:\\NonExistent\\File\\Path\\file.txt"
            };

            // Act
            var result = await _provider.EvaluateConstraintAsync(constraint);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task EvaluateConstraintAsync_WithUnknownConstraintType_ShouldReturnTrue()
        {
            // Arrange
            var constraint = new WorkflowConstraint
            {
                Type = "unknown_constraint_type",
                Value = "some_value"
            };

            // Act
            var result = await _provider.EvaluateConstraintAsync(constraint);

            // Assert
            Assert.True(result); // Unknown constraints pass by default
        }

        [Fact]
        public async Task ExecuteActionAsync_WithNotificationAction_ShouldSucceed()
        {
            // Arrange
            var action = new WorkflowAction
            {
                Type = "notification",
                Parameters = new Dictionary<string, object>
                {
                    ["title"] = "Test Title",
                    ["message"] = "Test Message"
                }
            };

            var context = new ActionContext
            {
                WorkflowId = "test-workflow",
                ExecutionId = "test-execution"
            };

            // Act
            var result = await _provider.ExecuteActionAsync(action, context);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("Notification sent", result.Message);
            Assert.True(result.Duration > TimeSpan.Zero);
        }

        [Fact]
        public async Task ExecuteActionAsync_WithRunProgramAction_ShouldExecuteProgram()
        {
            // Arrange - use cmd.exe to echo a message
            var action = new WorkflowAction
            {
                Type = "run_program",
                Parameters = new Dictionary<string, object>
                {
                    ["program"] = "cmd.exe",
                    ["arguments"] = "/c echo Hello from test",
                    ["waitForExit"] = true
                }
            };

            var context = new ActionContext
            {
                WorkflowId = "test-workflow",
                ExecutionId = "test-execution"
            };

            // Act
            var result = await _provider.ExecuteActionAsync(action, context);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("Process exited with code", result.Message);
            Assert.NotNull(result.OutputData);
            Assert.True(result.OutputData.ContainsKey("exit_code"));
            Assert.True(result.OutputData.ContainsKey("output"));
        }

        [Fact]
        public async Task ExecuteActionAsync_WithRunProgramActionMissingProgram_ShouldFail()
        {
            // Arrange
            var action = new WorkflowAction
            {
                Type = "run_program",
                Parameters = new Dictionary<string, object>
                {
                    ["arguments"] = "some args"
                }
            };

            var context = new ActionContext();

            // Act
            var result = await _provider.ExecuteActionAsync(action, context);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Program path is required", result.Message);
        }

        [Fact]
        public async Task ExecuteActionAsync_WithFileOperationCopy_ShouldCopyFile()
        {
            // Arrange
            var sourceFile = Path.GetTempFileName();
            var destFile = Path.Combine(Path.GetTempPath(), $"test-copy-{Guid.NewGuid()}.tmp");

            try
            {
                File.WriteAllText(sourceFile, "Test content");

                var action = new WorkflowAction
                {
                    Type = "file_operation",
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "copy",
                        ["source"] = sourceFile,
                        ["destination"] = destFile
                    }
                };

                var context = new ActionContext();

                // Act
                var result = await _provider.ExecuteActionAsync(action, context);

                // Assert
                Assert.True(result.Success);
                Assert.Contains("Copied", result.Message);
                Assert.True(File.Exists(destFile));
                Assert.Equal("Test content", File.ReadAllText(destFile));
            }
            finally
            {
                if (File.Exists(sourceFile)) File.Delete(sourceFile);
                if (File.Exists(destFile)) File.Delete(destFile);
            }
        }

        [Fact]
        public async Task ExecuteActionAsync_WithFileOperationMove_ShouldMoveFile()
        {
            // Arrange
            var sourceFile = Path.GetTempFileName();
            var destFile = Path.Combine(Path.GetTempPath(), $"test-move-{Guid.NewGuid()}.tmp");

            try
            {
                File.WriteAllText(sourceFile, "Test content");

                var action = new WorkflowAction
                {
                    Type = "file_operation",
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "move",
                        ["source"] = sourceFile,
                        ["destination"] = destFile
                    }
                };

                var context = new ActionContext();

                // Act
                var result = await _provider.ExecuteActionAsync(action, context);

                // Assert
                Assert.True(result.Success);
                Assert.Contains("Moved", result.Message);
                Assert.False(File.Exists(sourceFile)); // Source should no longer exist
                Assert.True(File.Exists(destFile));
                Assert.Equal("Test content", File.ReadAllText(destFile));
            }
            finally
            {
                if (File.Exists(sourceFile)) File.Delete(sourceFile);
                if (File.Exists(destFile)) File.Delete(destFile);
            }
        }

        [Fact]
        public async Task ExecuteActionAsync_WithFileOperationDelete_ShouldDeleteFile()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "Test content");

            var action = new WorkflowAction
            {
                Type = "file_operation",
                Parameters = new Dictionary<string, object>
                {
                    ["operation"] = "delete",
                    ["path"] = tempFile
                }
            };

            var context = new ActionContext();

            // Act
            var result = await _provider.ExecuteActionAsync(action, context);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("Deleted file", result.Message);
            Assert.False(File.Exists(tempFile));
        }

        [Fact]
        public async Task ExecuteActionAsync_WithHttpRequestGet_ShouldMakeRequest()
        {
            // Arrange
            var action = new WorkflowAction
            {
                Type = "http_request",
                Parameters = new Dictionary<string, object>
                {
                    ["method"] = "GET",
                    ["url"] = "https://httpbin.org/get"
                }
            };

            var context = new ActionContext();

            // Act
            var result = await _provider.ExecuteActionAsync(action, context);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("HTTP GET completed", result.Message);
            Assert.NotNull(result.OutputData);
            Assert.True(result.OutputData.ContainsKey("status_code"));
            Assert.True(result.OutputData.ContainsKey("response_body"));
            Assert.True(result.OutputData.ContainsKey("success"));
        }

        [Fact]
        public async Task ExecuteActionAsync_WithHttpRequestMissingUrl_ShouldFail()
        {
            // Arrange
            var action = new WorkflowAction
            {
                Type = "http_request",
                Parameters = new Dictionary<string, object>
                {
                    ["method"] = "GET"
                }
            };

            var context = new ActionContext();

            // Act
            var result = await _provider.ExecuteActionAsync(action, context);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("URL is required", result.Message);
        }

        [Fact]
        public async Task ExecuteActionAsync_WithClipboardAction_ShouldSucceed()
        {
            // Arrange
            var action = new WorkflowAction
            {
                Type = "clipboard",
                Parameters = new Dictionary<string, object>
                {
                    ["text"] = "Test clipboard content"
                }
            };

            var context = new ActionContext();

            // Act
            var result = await _provider.ExecuteActionAsync(action, context);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("Clipboard updated", result.Message);
        }

        [Fact]
        public async Task ExecuteActionAsync_WithPowerShellAction_ShouldExecuteScript()
        {
            // Arrange
            var action = new WorkflowAction
            {
                Type = "powershell",
                Parameters = new Dictionary<string, object>
                {
                    ["script"] = "Write-Output 'Hello from PowerShell'"
                }
            };

            var context = new ActionContext();

            // Act
            var result = await _provider.ExecuteActionAsync(action, context);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("PowerShell executed", result.Message);
            Assert.NotNull(result.OutputData);
            Assert.True(result.OutputData.ContainsKey("exit_code"));
            Assert.True(result.OutputData.ContainsKey("output"));

            var output = result.OutputData["output"].ToString();
            Assert.Contains("Hello from PowerShell", output);
        }

        [Fact]
        public async Task ExecuteActionAsync_WithPowerShellActionMissingScript_ShouldFail()
        {
            // Arrange
            var action = new WorkflowAction
            {
                Type = "powershell",
                Parameters = new Dictionary<string, object>()
            };

            var context = new ActionContext();

            // Act
            var result = await _provider.ExecuteActionAsync(action, context);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("PowerShell script is required", result.Message);
        }

        [Fact]
        public async Task ExecuteActionAsync_WithCmdAction_ShouldExecuteCommand()
        {
            // Arrange
            var action = new WorkflowAction
            {
                Type = "cmd",
                Parameters = new Dictionary<string, object>
                {
                    ["command"] = "echo Hello from CMD"
                }
            };

            var context = new ActionContext();

            // Act
            var result = await _provider.ExecuteActionAsync(action, context);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("Command executed", result.Message);
            Assert.NotNull(result.OutputData);
            Assert.True(result.OutputData.ContainsKey("exit_code"));
            Assert.True(result.OutputData.ContainsKey("output"));

            var output = result.OutputData["output"].ToString();
            Assert.Contains("Hello from CMD", output);
        }

        [Fact]
        public async Task ExecuteActionAsync_WithCmdActionMissingCommand_ShouldFail()
        {
            // Arrange
            var action = new WorkflowAction
            {
                Type = "cmd",
                Parameters = new Dictionary<string, object>()
            };

            var context = new ActionContext();

            // Act
            var result = await _provider.ExecuteActionAsync(action, context);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Command is required", result.Message);
        }

        [Fact]
        public async Task ExecuteActionAsync_WithUnsupportedActionType_ShouldFail()
        {
            // Arrange
            var action = new WorkflowAction
            {
                Type = "unsupported_action_type",
                Parameters = new Dictionary<string, object>()
            };

            var context = new ActionContext();

            // Act
            var result = await _provider.ExecuteActionAsync(action, context);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Unsupported action type", result.Message);
        }

        [Fact]
        public void GetPlatformInfo_ShouldReturnWindowsInfo()
        {
            // Act
            var info = _provider.GetPlatformInfo();

            // Assert
            Assert.Equal("windows", info.Platform);
            Assert.NotNull(info.Version);
            Assert.NotNull(info.Architecture);
            Assert.NotNull(info.Capabilities);
            Assert.NotNull(info.Metadata);

            // Check capabilities
            Assert.True(info.Capabilities["notification"]);
            Assert.True(info.Capabilities["run_program"]);
            Assert.True(info.Capabilities["file_operation"]);
            Assert.True(info.Capabilities["http_request"]);
            Assert.True(info.Capabilities["clipboard"]);
            Assert.True(info.Capabilities["powershell"]);
            Assert.True(info.Capabilities["cmd"]);
            Assert.False(info.Capabilities["hotkey"]); // Not implemented yet
            Assert.False(info.Capabilities["window_control"]); // Not implemented yet

            // Check metadata
            Assert.Equal("Windows", info.Metadata["os_name"]);
            Assert.Equal(".NET 8.0", info.Metadata["framework"]);
            Assert.Equal(Environment.MachineName, info.Metadata["machine_name"]);
        }

        [Fact]
        public async Task ExecuteActionAsync_ShouldMeasureDuration()
        {
            // Arrange
            var action = new WorkflowAction
            {
                Type = "notification",
                Parameters = new Dictionary<string, object>
                {
                    ["message"] = "Test"
                }
            };

            var context = new ActionContext();

            // Act
            var result = await _provider.ExecuteActionAsync(action, context);

            // Assert
            Assert.True(result.Duration > TimeSpan.Zero);
        }

        [Fact]
        public async Task TriggerHandle_StopAsync_ShouldDeactivateTrigger()
        {
            // Arrange
            var trigger = new WorkflowTrigger
            {
                Type = "time",
                Parameters = new Dictionary<string, object>
                {
                    ["schedule"] = "0 9 * * *"
                }
            };

            Task callback(TriggerContext ctx) => Task.CompletedTask;

            var handle = await _provider.RegisterTriggerAsync(trigger, callback);

            // Act
            await handle.StopAsync();

            // Assert
            Assert.False(handle.IsActive);
        }

        [Fact]
        public async Task TriggerHandle_Dispose_ShouldDeactivateTrigger()
        {
            // Arrange
            var trigger = new WorkflowTrigger
            {
                Type = "time",
                Parameters = new Dictionary<string, object>
                {
                    ["schedule"] = "0 9 * * *"
                }
            };

            Task callback(TriggerContext ctx) => Task.CompletedTask;

            var handle = await _provider.RegisterTriggerAsync(trigger, callback);

            // Act
            handle.Dispose();

            // Assert
            Assert.False(handle.IsActive);
        }
    }
}
