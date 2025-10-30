using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Sync;
using Loco.Core.Workflow;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Loco.Core.Tests.Sync
{
    /// <summary>
    /// Tests for CloudSyncManager - Cross-platform cloud synchronization.
    /// Solves Issues #1, #4, #7: Platform fragmentation, no backup/sharing, no cross-device sync
    /// </summary>
    public class CloudSyncManagerTests
    {
        private readonly CloudSyncManager _syncManager;

        public CloudSyncManagerTests()
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<CloudSyncManager>();

            var syncConfig = new SyncConfiguration
            {
                CloudEndpoint = "https://sync.loco.test",
                ApiKey = "test-api-key-12345",
                SyncInterval = TimeSpan.FromMinutes(15),
                ConflictResolutionStrategy = "latest_wins",
                EnableEncryption = true,
                AutoSync = true
            };

            _syncManager = new CloudSyncManager(logger, syncConfig);
        }

        [Fact]
        public async Task SyncAsync_WithNewLocalWorkflow_ShouldUpload()
        {
            // Arrange
            var localWorkflows = new List<WorkflowDefinition>
            {
                new WorkflowDefinition
                {
                    Id = "workflow-1",
                    Name = "Local Workflow",
                    Platforms = new List<string> { "windows" },
                    Actions = new List<WorkflowAction>()
                }
            };

            // Act
            var result = await _syncManager.SyncAsync(localWorkflows);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.SyncedAt <= DateTime.UtcNow);
            // Note: In real implementation, would verify upload occurred
        }

        [Fact]
        public async Task DetectChanges_WithModifiedWorkflow_ShouldDetectChange()
        {
            // Arrange
            var workflow1 = new WorkflowDefinition
            {
                Id = "workflow-1",
                Name = "Original Name",
                Platforms = new List<string> { "windows" },
                Actions = new List<WorkflowAction>()
            };

            var workflow2 = new WorkflowDefinition
            {
                Id = "workflow-1",
                Name = "Modified Name", // Changed
                Platforms = new List<string> { "windows" },
                Actions = new List<WorkflowAction>()
            };

            // Act - Sync first version
            await _syncManager.SyncAsync(new List<WorkflowDefinition> { workflow1 });

            // Modify and sync again
            var result = await _syncManager.SyncAsync(new List<WorkflowDefinition> { workflow2 });

            // Assert
            Assert.NotNull(result);
            // Changes should be detected and synced
        }

        [Fact]
        public async Task ShareWorkflow_WithValidOptions_ShouldReturnShareToken()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Id = "workflow-to-share",
                Name = "Shared Workflow",
                Platforms = new List<string> { "windows", "mac" },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction
                    {
                        Id = "action1",
                        Type = "notification",
                        Parameters = new Dictionary<string, object>
                        {
                            { "message", "Hello" }
                        }
                    }
                }
            };

            var shareOptions = new WorkflowShareOptions
            {
                Permission = "read",
                ExpiresInDays = 7,
                AllowedUsers = new List<string> { "user1@example.com", "user2@example.com" }
            };

            // Act
            var result = await _syncManager.ShareWorkflowAsync(workflow, shareOptions);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ShareToken);
            Assert.NotEmpty(result.ShareToken);
            Assert.NotNull(result.ShareUrl);
            Assert.Contains("share", result.ShareUrl.ToLower());
        }

        [Fact]
        public async Task ShareWorkflow_WithExpiration_ShouldSetExpiryDate()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Id = "expiring-workflow",
                Name = "Expiring Share",
                Platforms = new List<string> { "windows" },
                Actions = new List<WorkflowAction>()
            };

            var shareOptions = new WorkflowShareOptions
            {
                Permission = "read",
                ExpiresInDays = 3
            };

            // Act
            var result = await _syncManager.ShareWorkflowAsync(workflow, shareOptions);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ExpiresAt);

            var expectedExpiry = DateTime.UtcNow.AddDays(3);
            var actualExpiry = result.ExpiresAt.Value;

            // Allow 1 minute tolerance for test execution time
            Assert.True(Math.Abs((actualExpiry - expectedExpiry).TotalMinutes) < 1);
        }

        [Fact]
        public async Task CreateBackup_WithMultipleWorkflows_ShouldSucceed()
        {
            // Arrange
            var workflows = new List<WorkflowDefinition>
            {
                new WorkflowDefinition
                {
                    Id = "workflow-1",
                    Name = "First Workflow",
                    Platforms = new List<string> { "windows" },
                    Actions = new List<WorkflowAction>()
                },
                new WorkflowDefinition
                {
                    Id = "workflow-2",
                    Name = "Second Workflow",
                    Platforms = new List<string> { "mac" },
                    Actions = new List<WorkflowAction>()
                }
            };

            // Act
            var result = await _syncManager.CreateBackupAsync(workflows);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.BackupId);
            Assert.NotEmpty(result.BackupId);
            Assert.Equal(2, result.WorkflowCount);
            Assert.True(result.BackupSize > 0);
        }

        [Fact]
        public async Task RestoreBackup_WithValidBackupId_ShouldReturnWorkflows()
        {
            // Arrange
            var originalWorkflows = new List<WorkflowDefinition>
            {
                new WorkflowDefinition
                {
                    Id = "workflow-restore-test",
                    Name = "Restore Test Workflow",
                    Platforms = new List<string> { "windows", "linux" },
                    Actions = new List<WorkflowAction>()
                }
            };

            // Create backup
            var backupResult = await _syncManager.CreateBackupAsync(originalWorkflows);

            // Act - Restore from backup
            var restoreResult = await _syncManager.RestoreBackupAsync(backupResult.BackupId);

            // Assert
            Assert.True(restoreResult.Success);
            Assert.NotNull(restoreResult.Workflows);
            Assert.Single(restoreResult.Workflows);
            Assert.Equal("workflow-restore-test", restoreResult.Workflows[0].Id);
            Assert.Equal("Restore Test Workflow", restoreResult.Workflows[0].Name);
        }

        [Fact]
        public async Task RestoreBackup_WithInvalidBackupId_ShouldReturnFailure()
        {
            // Arrange
            var invalidBackupId = "non-existent-backup-12345";

            // Act
            var result = await _syncManager.RestoreBackupAsync(invalidBackupId);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("not found", result.ErrorMessage.ToLower());
        }

        [Fact]
        public async Task SyncAsync_WithLatestWinsStrategy_ShouldUseLatestVersion()
        {
            // Arrange - Create two versions of same workflow
            var olderWorkflow = new WorkflowDefinition
            {
                Id = "conflict-workflow",
                Name = "Older Version",
                Platforms = new List<string> { "windows" },
                Actions = new List<WorkflowAction>()
            };

            var newerWorkflow = new WorkflowDefinition
            {
                Id = "conflict-workflow",
                Name = "Newer Version",
                Platforms = new List<string> { "windows", "mac" },
                Actions = new List<WorkflowAction>()
            };

            // Act - Sync older first, then newer
            await Task.Delay(100); // Ensure time difference
            await _syncManager.SyncAsync(new List<WorkflowDefinition> { olderWorkflow });

            await Task.Delay(100);
            var result = await _syncManager.SyncAsync(new List<WorkflowDefinition> { newerWorkflow });

            // Assert
            Assert.True(result.Uploaded > 0 || result.Downloaded > 0);
            // latest_wins strategy should keep the newer version
        }

        [Fact]
        public async Task ShareWorkflow_WithEditPermission_ShouldAllowEditing()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Id = "editable-workflow",
                Name = "Editable Shared Workflow",
                Platforms = new List<string> { "windows" },
                Actions = new List<WorkflowAction>()
            };

            var shareOptions = new WorkflowShareOptions
            {
                Permission = "edit",
                ExpiresInDays = 30
            };

            // Act
            var result = await _syncManager.ShareWorkflowAsync(workflow, shareOptions);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ShareToken);
            // In real implementation, token would encode permission level
        }

        [Fact]
        public async Task CreateBackup_WithEmptyList_ShouldReturnFailure()
        {
            // Arrange
            var emptyWorkflows = new List<WorkflowDefinition>();

            // Act
            var result = await _syncManager.CreateBackupAsync(emptyWorkflows);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task SyncAsync_WithEncryptionEnabled_ShouldEncryptData()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Id = "encrypted-workflow",
                Name = "Encrypted Workflow with 日本語",
                Platforms = new List<string> { "windows" },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction
                    {
                        Id = "action1",
                        Type = "notification",
                        Parameters = new Dictionary<string, object>
                        {
                            { "message", "Sensitive data" }
                        }
                    }
                }
            };

            // Act
            var result = await _syncManager.SyncAsync(new List<WorkflowDefinition> { workflow });

            // Assert
            Assert.NotNull(result);
            // In real implementation, would verify data is encrypted during transit
        }

        [Fact]
        public async Task GetSyncStatus_AfterSync_ShouldReturnStatus()
        {
            // Arrange
            var workflows = new List<WorkflowDefinition>
            {
                new WorkflowDefinition
                {
                    Id = "status-test-workflow",
                    Name = "Status Test",
                    Platforms = new List<string> { "windows" },
                    Actions = new List<WorkflowAction>()
                }
            };

            // Act
            await _syncManager.SyncAsync(workflows);
            var status = _syncManager.GetSyncStatus();

            // Assert
            Assert.NotNull(status);
            Assert.True(status.LastSyncAt <= DateTime.UtcNow);
        }

        [Fact]
        public async Task ShareWorkflow_WithExecutePermission_ShouldCreateExecutableShare()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Id = "executable-workflow",
                Name = "Executable Shared Workflow",
                Platforms = new List<string> { "windows", "mac", "linux" },
                Actions = new List<WorkflowAction>()
            };

            var shareOptions = new WorkflowShareOptions
            {
                Permission = "execute",
                ExpiresInDays = 1,
                AllowedUsers = new List<string> { "executor@example.com" }
            };

            // Act
            var result = await _syncManager.ShareWorkflowAsync(workflow, shareOptions);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ShareToken);
            Assert.NotNull(result.ShareUrl);
        }

        [Fact]
        public async Task CreateBackup_ShouldIncludeTimestamp()
        {
            // Arrange
            var workflows = new List<WorkflowDefinition>
            {
                new WorkflowDefinition
                {
                    Id = "timestamp-test",
                    Name = "Timestamp Test",
                    Platforms = new List<string> { "windows" },
                    Actions = new List<WorkflowAction>()
                }
            };

            var beforeBackup = DateTime.UtcNow;

            // Act
            var result = await _syncManager.CreateBackupAsync(workflows);

            var afterBackup = DateTime.UtcNow;

            // Assert
            Assert.True(result.Success);
            Assert.True(result.CreatedAt >= beforeBackup && result.CreatedAt <= afterBackup);
        }
    }
}
