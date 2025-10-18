using System.Text.Json;

namespace Loco.Core.Workflows;

/// <summary>
/// Generates workflow templates for common scenarios.
/// </summary>
public static class WorkflowTemplateGenerator
{
    /// <summary>
    /// Generates a basic workflow template.
    /// </summary>
    public static string GenerateBasicTemplate(string workflowName, string workflowId)
    {
        var workflow = new WorkflowDefinition
        {
            Id = workflowId,
            Name = workflowName,
            Description = "Description of your workflow",
            Steps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    Id = "start",
                    Name = "Start workflow",
                    Type = "log",
                    Message = "=== Workflow Started ==="
                },
                new WorkflowStep
                {
                    Id = "step1",
                    Name = "Your first step",
                    Type = "log",
                    Message = "Executing step 1..."
                },
                new WorkflowStep
                {
                    Id = "complete",
                    Name = "Complete workflow",
                    Type = "log",
                    Message = "=== Workflow Completed ==="
                }
            }
        };

        return SerializeWorkflow(workflow);
    }

    /// <summary>
    /// Generates a deployment workflow template.
    /// </summary>
    public static string GenerateDeploymentTemplate(string workflowName, string workflowId)
    {
        var workflow = new WorkflowDefinition
        {
            Id = workflowId,
            Name = workflowName,
            Description = "Automated deployment workflow",
            Environments = new List<EnvironmentPreset>
            {
                new EnvironmentPreset
                {
                    Name = "dev",
                    Description = "Development environment",
                    IsDefault = true,
                    Variables = new Dictionary<string, string>
                    {
                        { "api_url", "https://api.dev.example.com" },
                        { "deploy_path", "/opt/app/dev" }
                    }
                },
                new EnvironmentPreset
                {
                    Name = "production",
                    Description = "Production environment",
                    Variables = new Dictionary<string, string>
                    {
                        { "api_url", "https://api.example.com" },
                        { "deploy_path", "/opt/app/prod" }
                    }
                }
            },
            Steps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    Id = "start",
                    Name = "Start deployment",
                    Type = "log",
                    Message = "=== Starting deployment to ${var:api_url} ==="
                },
                new WorkflowStep
                {
                    Id = "backup",
                    Name = "Create backup",
                    Type = "log",
                    Message = "Creating backup..."
                },
                new WorkflowStep
                {
                    Id = "deploy",
                    Name = "Deploy application",
                    Type = "log",
                    Message = "Deploying to ${var:deploy_path}..."
                },
                new WorkflowStep
                {
                    Id = "verify",
                    Name = "Verify deployment",
                    Type = "http",
                    Url = "${var:api_url}/health",
                    Method = "GET",
                    RetryCount = 3,
                    TimeoutSeconds = 30
                },
                new WorkflowStep
                {
                    Id = "complete",
                    Name = "Deployment complete",
                    Type = "log",
                    Message = "=== Deployment completed successfully ==="
                }
            }
        };

        return SerializeWorkflow(workflow);
    }

    /// <summary>
    /// Generates a health check workflow template.
    /// </summary>
    public static string GenerateHealthCheckTemplate(string workflowName, string workflowId)
    {
        var workflow = new WorkflowDefinition
        {
            Id = workflowId,
            Name = workflowName,
            Description = "System health check workflow",
            Steps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    Id = "start",
                    Name = "Start health check",
                    Type = "log",
                    Message = "=== Running Health Checks ==="
                },
                new WorkflowStep
                {
                    Id = "check-api",
                    Name = "Check API endpoint",
                    Type = "http",
                    Url = "https://httpbin.org/status/200",
                    Method = "GET",
                    RetryCount = 2,
                    TimeoutSeconds = 10,
                    SaveOutput = "api_status"
                },
                new WorkflowStep
                {
                    Id = "check-disk",
                    Name = "Check disk space",
                    Type = "process",
                    Command = "df -h",
                    SaveOutput = "disk_info"
                },
                new WorkflowStep
                {
                    Id = "check-memory",
                    Name = "Check memory usage",
                    Type = "process",
                    Command = "free -h",
                    SaveOutput = "memory_info"
                },
                new WorkflowStep
                {
                    Id = "complete",
                    Name = "Health check complete",
                    Type = "log",
                    Message = "=== All Health Checks Passed ==="
                }
            }
        };

        return SerializeWorkflow(workflow);
    }

    /// <summary>
    /// Generates a backup workflow template.
    /// </summary>
    public static string GenerateBackupTemplate(string workflowName, string workflowId)
    {
        var workflow = new WorkflowDefinition
        {
            Id = workflowId,
            Name = workflowName,
            Description = "Automated backup workflow",
            Steps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    Id = "start",
                    Name = "Start backup",
                    Type = "log",
                    Message = "=== Starting Backup at ${datetime:yyyy-MM-dd HH:mm:ss} ==="
                },
                new WorkflowStep
                {
                    Id = "create-backup-dir",
                    Name = "Create backup directory",
                    Type = "process",
                    Command = "mkdir -p /backup/${date:yyyy-MM-dd}"
                },
                new WorkflowStep
                {
                    Id = "backup-files",
                    Name = "Backup files",
                    Type = "process",
                    Command = "tar -czf /backup/${date:yyyy-MM-dd}/backup.tar.gz /data",
                    TimeoutSeconds = 600
                },
                new WorkflowStep
                {
                    Id = "verify-backup",
                    Name = "Verify backup",
                    Type = "process",
                    Command = "tar -tzf /backup/${date:yyyy-MM-dd}/backup.tar.gz",
                    ContinueOnError = true
                },
                new WorkflowStep
                {
                    Id = "complete",
                    Name = "Backup complete",
                    Type = "log",
                    Message = "=== Backup Completed Successfully ==="
                }
            }
        };

        return SerializeWorkflow(workflow);
    }

    private static string SerializeWorkflow(WorkflowDefinition workflow)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(workflow, options);
    }
}
