using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Loco.Core.IaC;

/// <summary>
/// Infrastructure as Code (IaC) support for Loco
/// Based on 2025 best practices: Terraform, Pulumi, Ansible patterns
/// Infrastructure as Code (IaC) サポート
/// </summary>
public class InfrastructureAsCode
{
    private readonly ILogger<InfrastructureAsCode>? _logger;
    private readonly ISerializer _yamlSerializer;
    private readonly IDeserializer _yamlDeserializer;
    private readonly JsonSerializerOptions _jsonOptions;

    public InfrastructureAsCode(ILogger<InfrastructureAsCode>? logger = null)
    {
        _logger = logger;

        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Load infrastructure definition from YAML file
    /// YAMLファイルからインフラ定義を読み込み
    /// </summary>
    public async Task<LocoInfrastructure> LoadFromYamlAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Infrastructure file not found: {filePath}");
        }

        var yaml = await File.ReadAllTextAsync(filePath, cancellationToken);
        var infrastructure = _yamlDeserializer.Deserialize<LocoInfrastructure>(yaml);

        if (infrastructure == null)
        {
            throw new InvalidOperationException($"Failed to parse infrastructure file: {filePath}");
        }

        infrastructure.SourceFile = filePath;
        _logger?.LogInformation("Loaded infrastructure from {FilePath}", filePath);

        return infrastructure;
    }

    /// <summary>
    /// Save infrastructure definition to YAML file
    /// インフラ定義をYAMLファイルに保存
    /// </summary>
    public async Task SaveToYamlAsync(
        LocoInfrastructure infrastructure,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var yaml = _yamlSerializer.Serialize(infrastructure);
        await File.WriteAllTextAsync(filePath, yaml, cancellationToken);

        _logger?.LogInformation("Saved infrastructure to {FilePath}", filePath);
    }

    /// <summary>
    /// Load infrastructure definition from JSON file
    /// JSONファイルからインフラ定義を読み込み
    /// </summary>
    public async Task<LocoInfrastructure> LoadFromJsonAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Infrastructure file not found: {filePath}");
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var infrastructure = JsonSerializer.Deserialize<LocoInfrastructure>(json, _jsonOptions);

        if (infrastructure == null)
        {
            throw new InvalidOperationException($"Failed to parse infrastructure file: {filePath}");
        }

        infrastructure.SourceFile = filePath;
        _logger?.LogInformation("Loaded infrastructure from {FilePath}", filePath);

        return infrastructure;
    }

    /// <summary>
    /// Validate infrastructure definition
    /// インフラ定義を検証
    /// </summary>
    public ValidationResult Validate(LocoInfrastructure infrastructure)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate version
        if (string.IsNullOrEmpty(infrastructure.Version))
        {
            errors.Add("Version is required");
        }

        // Validate workflows
        if (infrastructure.Workflows == null || infrastructure.Workflows.Count == 0)
        {
            warnings.Add("No workflows defined");
        }
        else
        {
            foreach (var workflow in infrastructure.Workflows)
            {
                if (string.IsNullOrEmpty(workflow.Name))
                {
                    errors.Add("Workflow name is required");
                }

                if (workflow.Steps == null || workflow.Steps.Count == 0)
                {
                    warnings.Add($"Workflow '{workflow.Name}' has no steps");
                }
                else
                {
                    foreach (var step in workflow.Steps)
                    {
                        if (string.IsNullOrEmpty(step.Type))
                        {
                            errors.Add($"Step type is required in workflow '{workflow.Name}'");
                        }
                    }
                }
            }
        }

        // Validate secrets
        if (infrastructure.Secrets != null)
        {
            foreach (var secret in infrastructure.Secrets)
            {
                if (string.IsNullOrEmpty(secret.Name))
                {
                    errors.Add("Secret name is required");
                }

                if (string.IsNullOrEmpty(secret.Source))
                {
                    errors.Add($"Secret '{secret.Name}' must have a source");
                }
            }
        }

        // Validate monitoring
        if (infrastructure.Monitoring?.Enabled == true)
        {
            if (infrastructure.Monitoring.Alerts == null || infrastructure.Monitoring.Alerts.Count == 0)
            {
                warnings.Add("Monitoring is enabled but no alerts are configured");
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Apply infrastructure definition (deploy)
    /// インフラ定義を適用（デプロイ）
    /// </summary>
    public async Task<DeploymentResult> ApplyAsync(
        LocoInfrastructure infrastructure,
        CancellationToken cancellationToken = default)
    {
        var result = new DeploymentResult
        {
            StartTime = DateTime.UtcNow
        };

        _logger?.LogInformation("Starting infrastructure deployment");

        try
        {
            // Validate first
            var validation = Validate(infrastructure);
            if (!validation.IsValid)
            {
                result.Success = false;
                result.Errors.AddRange(validation.Errors);
                return result;
            }

            // Deploy workflows
            foreach (var workflow in infrastructure.Workflows ?? new())
            {
                _logger?.LogInformation("Deploying workflow: {WorkflowName}", workflow.Name);

                var workflowDeployed = await DeployWorkflowAsync(workflow, cancellationToken);
                if (workflowDeployed)
                {
                    result.DeployedResources.Add($"Workflow: {workflow.Name}");
                    _logger?.LogInformation("Successfully deployed workflow: {WorkflowName}", workflow.Name);
                }
                else
                {
                    result.Errors.Add($"Failed to deploy workflow: {workflow.Name}");
                }
            }

            // Configure secrets
            foreach (var secret in infrastructure.Secrets ?? new())
            {
                _logger?.LogInformation("Configuring secret: {SecretName}", secret.Name);

                var secretConfigured = await ConfigureSecretAsync(secret, cancellationToken);
                if (secretConfigured)
                {
                    result.DeployedResources.Add($"Secret: {secret.Name}");
                    _logger?.LogInformation("Successfully configured secret: {SecretName}", secret.Name);
                }
                else
                {
                    result.Errors.Add($"Failed to configure secret: {secret.Name}");
                }
            }

            // Setup monitoring
            if (infrastructure.Monitoring?.Enabled == true)
            {
                _logger?.LogInformation("Setting up monitoring");
                result.DeployedResources.Add("Monitoring configuration");
            }

            result.Success = true;
            _logger?.LogInformation("Infrastructure deployment completed successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Infrastructure deployment failed");
            result.Success = false;
            result.Errors.Add(ex.Message);
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
        }

        return result;
    }

    /// <summary>
    /// Generate infrastructure definition from existing configuration
    /// 既存の設定からインフラ定義を生成
    /// </summary>
    public LocoInfrastructure GenerateFromExisting(string workingDirectory)
    {
        var infrastructure = new LocoInfrastructure
        {
            Version = "1.0",
            Workflows = new List<IaCWorkflow>()
        };

        // Scan for existing workflow files
        if (Directory.Exists(workingDirectory))
        {
            var workflowFiles = Directory.GetFiles(workingDirectory, "*.json", SearchOption.TopDirectoryOnly);

            foreach (var file in workflowFiles)
            {
                try
                {
                    var workflow = new IaCWorkflow
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Steps = new List<IaCStep>()
                    };

                    infrastructure.Workflows.Add(workflow);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to process workflow file: {File}", file);
                }
            }
        }

        return infrastructure;
    }

    /// <summary>
    /// Deploy a single workflow
    /// ワークフローをデプロイ
    /// </summary>
    private async Task<bool> DeployWorkflowAsync(
        IaCWorkflow workflow,
        CancellationToken cancellationToken)
    {
        try
        {
            // Create workflow directory if needed
            var workflowDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Loco",
                "workflows"
            );

            Directory.CreateDirectory(workflowDir);

            // Convert IaC workflow to Loco workflow format
            var locoWorkflow = new
            {
                id = workflow.Name,
                description = workflow.Description,
                schedule = workflow.Schedule,
                steps = workflow.Steps.Select(s => new
                {
                    type = s.Type,
                    name = s.Name,
                    config = s.Config,
                    condition = s.If,
                    retry = s.Retry != null ? new
                    {
                        maxRetries = s.Retry.MaxRetries,
                        initialDelayMs = s.Retry.InitialDelayMs,
                        strategy = s.Retry.Strategy
                    } : null
                }).ToList(),
                environment = workflow.Environment,
                retry = workflow.Retry != null ? new
                {
                    maxRetries = workflow.Retry.MaxRetries,
                    initialDelayMs = workflow.Retry.InitialDelayMs,
                    strategy = workflow.Retry.Strategy
                } : null
            };

            // Save workflow definition
            var workflowFile = Path.Combine(workflowDir, $"{workflow.Name}.json");
            var json = JsonSerializer.Serialize(locoWorkflow, _jsonOptions);
            await File.WriteAllTextAsync(workflowFile, json, cancellationToken);

            _logger?.LogInformation("Workflow definition saved to: {FilePath}", workflowFile);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to deploy workflow: {WorkflowName}", workflow.Name);
            return false;
        }
    }

    /// <summary>
    /// Configure a secret
    /// シークレットを設定
    /// </summary>
    private async Task<bool> ConfigureSecretAsync(
        IaCSecret secret,
        CancellationToken cancellationToken)
    {
        try
        {
            // Create secrets directory if needed
            var secretsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Loco",
                "secrets"
            );

            Directory.CreateDirectory(secretsDir);

            // Parse secret source
            var secretValue = await ResolveSecretValueAsync(secret.Source, cancellationToken);

            if (string.IsNullOrEmpty(secretValue))
            {
                _logger?.LogWarning("Secret value is empty for: {SecretName}", secret.Name);
                return false;
            }

            // Store secret metadata (not the actual value for security)
            var secretMetadata = new
            {
                name = secret.Name,
                description = secret.Description,
                source = secret.Source,
                configuredAt = DateTime.UtcNow
            };

            var metadataFile = Path.Combine(secretsDir, $"{secret.Name}.meta.json");
            var json = JsonSerializer.Serialize(secretMetadata, _jsonOptions);
            await File.WriteAllTextAsync(metadataFile, json, cancellationToken);

            // Set environment variable for runtime access
            Environment.SetEnvironmentVariable($"LOCO_SECRET_{secret.Name.ToUpperInvariant()}", secretValue);

            _logger?.LogInformation("Secret configured: {SecretName}", secret.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to configure secret: {SecretName}", secret.Name);
            return false;
        }
    }

    /// <summary>
    /// Resolve secret value from source
    /// ソースからシークレット値を解決
    /// </summary>
    private async Task<string> ResolveSecretValueAsync(
        string source,
        CancellationToken cancellationToken)
    {
        if (source.StartsWith("env:"))
        {
            // Load from environment variable
            var envVarName = source.Substring(4);
            var value = Environment.GetEnvironmentVariable(envVarName);
            return value ?? string.Empty;
        }
        else if (source.StartsWith("file:"))
        {
            // Load from file
            var filePath = source.Substring(5);
            if (File.Exists(filePath))
            {
                return await File.ReadAllTextAsync(filePath, cancellationToken);
            }
            else
            {
                _logger?.LogWarning("Secret file not found: {FilePath}", filePath);
                return string.Empty;
            }
        }
        else if (source.StartsWith("plain:"))
        {
            // Plain text (not recommended for production)
            _logger?.LogWarning("Using plain text secret (not recommended for production)");
            return source.Substring(6);
        }
        else
        {
            _logger?.LogError("Unsupported secret source format: {Source}", source);
            return string.Empty;
        }
    }
}

#region Infrastructure Models

/// <summary>
/// Loco infrastructure definition
/// Loco インフラ定義
/// </summary>
public class LocoInfrastructure
{
    public string Version { get; set; } = "1.0";
    public List<IaCWorkflow>? Workflows { get; set; }
    public List<IaCSecret>? Secrets { get; set; }
    public IaCMonitoring? Monitoring { get; set; }
    public Dictionary<string, object>? Variables { get; set; }

    [JsonIgnore]
    [YamlIgnore]
    public string? SourceFile { get; set; }
}

public class IaCWorkflow
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Schedule { get; set; }
    public List<IaCStep> Steps { get; set; } = new();
    public Dictionary<string, object>? Environment { get; set; }
    public IaCRetryPolicy? Retry { get; set; }
}

public class IaCStep
{
    public string Type { get; set; } = string.Empty;
    public string? Name { get; set; }
    public Dictionary<string, object>? Config { get; set; }
    public string? If { get; set; } // Conditional execution
    public IaCRetryPolicy? Retry { get; set; }
}

public class IaCSecret
{
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // e.g., "env:API_KEY", "file:/path/to/secret"
    public string? Description { get; set; }
}

public class IaCMonitoring
{
    public bool Enabled { get; set; } = true;
    public List<IaCAlert>? Alerts { get; set; }
    public IaCHealthCheck? HealthCheck { get; set; }
}

public class IaCAlert
{
    public string Type { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public Dictionary<string, object>? Config { get; set; }
}

public class IaCHealthCheck
{
    public int IntervalSeconds { get; set; } = 60;
    public string? Endpoint { get; set; }
}

public class IaCRetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public int InitialDelayMs { get; set; } = 1000;
    public string Strategy { get; set; } = "exponential"; // exponential, linear, constant
}

#endregion

#region Results

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class DeploymentResult
{
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<string> DeployedResources { get; set; } = new();
    public List<string> Errors { get; set; } = new();

    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;
}

#endregion
