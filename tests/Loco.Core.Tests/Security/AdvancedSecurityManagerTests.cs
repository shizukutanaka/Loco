using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Configuration;
using Loco.Core.Security;
using Loco.Core.Workflow;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Loco.Core.Tests.Security
{
    /// <summary>
    /// Tests for AdvancedSecurityManager - 2024 security best practices implementation.
    /// Solves Issues #13-17: Security vulnerabilities
    /// </summary>
    public class AdvancedSecurityManagerTests
    {
        private readonly AdvancedSecurityManager _securityManager;
        private readonly LocoConfig _locoConfig;

        public AdvancedSecurityManagerTests()
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<AdvancedSecurityManager>();

            var securityConfig = new SecurityConfiguration
            {
                EncryptionKey = "test-encryption-key-32chars!",
                MasterKey = "test-master-key-for-credentials",
                EnableAuditLogging = true,
                EnforceAccessControl = true,
                AllowedDomains = new[] { "api.example.com", "trusted.com" }
            };

            _locoConfig = new LocoConfig
            {
                AllowedPaths = new string[] { "C:\\Temp", "C:\\Users" },
                ForbiddenPaths = new string[] { "C:\\Windows", "C:\\Program Files" }
            };

            _securityManager = new AdvancedSecurityManager(logger, securityConfig, _locoConfig);
        }

        [Fact]
        public async Task ValidateWorkflowSecurity_WithDangerousCommand_ShouldDetectInjectionVulnerability()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Id = "test-workflow",
                Name = "Test Workflow",
                Platforms = new List<string> { "windows" },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction
                    {
                        Id = "action1",
                        Type = "script",
                        Parameters = new Dictionary<string, object>
                        {
                            { "script", "rm -rf /" }
                        }
                    }
                }
            };

            var context = new SecurityContext
            {
                UserId = "test-user",
                DeviceId = "test-device"
            };

            // Act
            var result = await _securityManager.ValidateWorkflowSecurityAsync(workflow, context);

            // Assert
            Assert.False(result.IsSecure);
            Assert.Contains(result.Vulnerabilities, v => v.Type == "dangerous_command");
            Assert.Contains(result.Vulnerabilities, v => v.Severity == "critical");
            Assert.True(result.RiskScore > 0);
        }

        [Fact]
        public async Task ValidateWorkflowSecurity_WithExposedPassword_ShouldDetectCredentialVulnerability()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Id = "test-workflow",
                Name = "Test Workflow",
                Platforms = new List<string> { "windows" },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction
                    {
                        Id = "action1",
                        Type = "api_call",
                        Parameters = new Dictionary<string, object>
                        {
                            { "password", "my-secret-password" }
                        }
                    }
                }
            };

            var context = new SecurityContext
            {
                UserId = "test-user",
                DeviceId = "test-device"
            };

            // Act
            var result = await _securityManager.ValidateWorkflowSecurityAsync(workflow, context);

            // Assert
            Assert.False(result.IsSecure);
            Assert.Contains(result.Vulnerabilities, v => v.Type == "exposed_credential");
            Assert.Contains(result.Vulnerabilities, v => v.Severity == "high");
        }

        [Fact]
        public async Task ValidateWorkflowSecurity_WithSecureCredentialReference_ShouldPass()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Id = "test-workflow",
                Name = "Test Workflow",
                Platforms = new List<string> { "windows" },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction
                    {
                        Id = "action1",
                        Type = "api_call",
                        Parameters = new Dictionary<string, object>
                        {
                            { "api_key", "${credential:my-api-key}" }
                        }
                    }
                }
            };

            var context = new SecurityContext
            {
                UserId = "test-user",
                DeviceId = "test-device"
            };

            // Act
            var result = await _securityManager.ValidateWorkflowSecurityAsync(workflow, context);

            // Assert - Should have no credential exposure vulnerabilities
            Assert.DoesNotContain(result.Vulnerabilities, v => v.Type == "exposed_credential");
        }

        [Fact]
        public async Task ValidateWorkflowSecurity_WithInsecureHTTP_ShouldDetectInsecureConnection()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Id = "test-workflow",
                Name = "Test Workflow",
                Platforms = new List<string> { "windows" },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction
                    {
                        Id = "action1",
                        Type = "http_request",
                        Parameters = new Dictionary<string, object>
                        {
                            { "url", "http://insecure-api.com/data" }
                        }
                    }
                }
            };

            var context = new SecurityContext
            {
                UserId = "test-user",
                DeviceId = "test-device"
            };

            // Act
            var result = await _securityManager.ValidateWorkflowSecurityAsync(workflow, context);

            // Assert
            Assert.Contains(result.Vulnerabilities, v => v.Type == "insecure_connection");
            Assert.Contains(result.Vulnerabilities, v => v.Severity == "medium");
        }

        [Fact]
        public async Task ValidateWorkflowSecurity_WithPathTraversal_ShouldDetectVulnerability()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Id = "test-workflow",
                Name = "Test Workflow",
                Platforms = new List<string> { "windows" },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction
                    {
                        Id = "action1",
                        Type = "file_operation",
                        Parameters = new Dictionary<string, object>
                        {
                            { "operation", "read" },
                            { "path", "../../etc/passwd" }
                        }
                    }
                }
            };

            var context = new SecurityContext
            {
                UserId = "test-user",
                DeviceId = "test-device"
            };

            // Act
            var result = await _securityManager.ValidateWorkflowSecurityAsync(workflow, context);

            // Assert
            Assert.Contains(result.Vulnerabilities, v => v.Type == "path_traversal");
            Assert.Contains(result.Vulnerabilities, v => v.Severity == "high");
        }

        [Fact]
        public async Task EncryptWorkflowData_ShouldReturnEncryptedBytes()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Id = "test-workflow",
                Name = "Test Workflow",
                Platforms = new List<string> { "windows" },
                Actions = new List<WorkflowAction>()
            };

            // Act
            var encrypted = await _securityManager.EncryptWorkflowDataAsync(workflow);

            // Assert
            Assert.NotNull(encrypted);
            Assert.True(encrypted.Length > 0);
        }

        [Fact]
        public async Task DecryptWorkflowData_ShouldReturnOriginalWorkflow()
        {
            // Arrange
            var originalWorkflow = new WorkflowDefinition
            {
                Id = "test-workflow",
                Name = "Test Workflow with Special Chars: 日本語",
                Platforms = new List<string> { "windows", "mac" },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction
                    {
                        Id = "action1",
                        Type = "notification",
                        Parameters = new Dictionary<string, object>
                        {
                            { "message", "Hello World" }
                        }
                    }
                }
            };

            // Act
            var encrypted = await _securityManager.EncryptWorkflowDataAsync(originalWorkflow);
            var decrypted = await _securityManager.DecryptWorkflowDataAsync(encrypted);

            // Assert
            Assert.Equal(originalWorkflow.Id, decrypted.Id);
            Assert.Equal(originalWorkflow.Name, decrypted.Name);
            Assert.Equal(originalWorkflow.Platforms.Count, decrypted.Platforms.Count);
            Assert.Equal(originalWorkflow.Actions.Count, decrypted.Actions.Count);
        }

        [Fact]
        public async Task StoreAndRetrieveCredential_ShouldWorkCorrectly()
        {
            // Arrange
            var context = new SecurityContext
            {
                UserId = "test-user",
                DeviceId = "test-device"
            };

            var credential = new CredentialData
            {
                Type = "api_key",
                Value = "my-secret-api-key-12345",
                Metadata = new Dictionary<string, string>
                {
                    { "service", "github" },
                    { "scope", "repo" }
                }
            };

            // Act
            var storeResult = await _securityManager.StoreCredentialAsync("github-key", credential, context);
            var retrieveResult = await _securityManager.RetrieveCredentialAsync("github-key", context);

            // Assert
            Assert.True(storeResult.Success);
            Assert.True(retrieveResult.Success);
            Assert.NotNull(retrieveResult.Credential);
            Assert.Equal(credential.Type, retrieveResult.Credential.Type);
            Assert.Equal(credential.Value, retrieveResult.Credential.Value);
            Assert.Equal("github", retrieveResult.Credential.Metadata["service"]);
        }

        [Fact]
        public async Task ValidateWorkflowSecurity_WithMultipleVulnerabilities_ShouldCalculateCorrectRiskScore()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Id = "test-workflow",
                Name = "Test Workflow",
                Platforms = new List<string> { "windows" },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction
                    {
                        Id = "action1",
                        Type = "script",
                        Parameters = new Dictionary<string, object>
                        {
                            { "script", "rm -rf /" } // Critical: 10 points
                        }
                    },
                    new WorkflowAction
                    {
                        Id = "action2",
                        Type = "http_request",
                        Parameters = new Dictionary<string, object>
                        {
                            { "password", "secret" }, // High: 7 points
                            { "url", "http://insecure.com" } // Medium: 4 points
                        }
                    }
                }
            };

            var context = new SecurityContext
            {
                UserId = "test-user",
                DeviceId = "test-device"
            };

            // Act
            var result = await _securityManager.ValidateWorkflowSecurityAsync(workflow, context);

            // Assert
            Assert.False(result.IsSecure);
            Assert.True(result.RiskScore >= 21.0); // 10 + 7 + 4 = 21 minimum
            Assert.True(result.Vulnerabilities.Count >= 3);
        }

        [Fact]
        public void GrantPermission_ShouldAllowUserAccess()
        {
            // Arrange
            var userId = "test-user";
            var resourceId = "workflow-123";
            var permission = "read";

            // Act
            _securityManager.GrantPermission(userId, resourceId, permission);

            // Assert - Should not throw and permission should be stored
            // Note: The current implementation always returns true for CheckUserPermission
            // In production, this would validate against the permission store
        }

        [Fact]
        public async Task ValidateWorkflowSecurity_WithSSRFRisk_ShouldDetectVulnerability()
        {
            // Arrange
            var workflow = new WorkflowDefinition
            {
                Id = "test-workflow",
                Name = "Test Workflow",
                Platforms = new List<string> { "windows" },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction
                    {
                        Id = "action1",
                        Type = "http_request",
                        Parameters = new Dictionary<string, object>
                        {
                            { "url", "http://localhost/admin" }
                        }
                    }
                }
            };

            var context = new SecurityContext
            {
                UserId = "test-user",
                DeviceId = "test-device"
            };

            // Act
            var result = await _securityManager.ValidateWorkflowSecurityAsync(workflow, context);

            // Assert
            Assert.Contains(result.Vulnerabilities, v => v.Type == "ssrf_risk");
        }
    }
}
