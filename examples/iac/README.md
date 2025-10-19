# Loco Infrastructure as Code (IaC) Examples

This directory contains examples of using Loco's Infrastructure as Code capabilities to define and deploy workflows, secrets, and monitoring configurations.

## Overview

Loco IaC allows you to define your entire workflow infrastructure using declarative YAML or JSON files. This approach provides:

- **Version Control**: Track infrastructure changes in git
- **Reproducibility**: Deploy the same configuration across environments
- **Documentation**: Self-documenting infrastructure
- **Automation**: Automate deployment and updates

## File Formats

### YAML Format
```yaml
version: "1.0"
workflows:
  - name: my-workflow
    steps:
      - type: shell
        config:
          command: echo "Hello World"
```

### JSON Format
```json
{
  "version": "1.0",
  "workflows": [
    {
      "name": "my-workflow",
      "steps": [
        {
          "type": "shell",
          "config": {
            "command": "echo \"Hello World\""
          }
        }
      ]
    }
  ]
}
```

## Configuration Sections

### 1. Workflows

Define automated workflows with steps, schedules, and retry policies.

```yaml
workflows:
  - name: deploy-application
    description: Deploy to production
    schedule: "0 2 * * *"  # Cron expression
    retry:
      maxRetries: 3
      initialDelayMs: 5000
      strategy: exponential
    steps:
      - type: shell
        name: build
        config:
          command: dotnet build
```

### 2. Secrets

Manage sensitive configuration securely.

```yaml
secrets:
  - name: DATABASE_URL
    source: env:DATABASE_URL  # Load from environment variable
    description: Database connection string

  - name: API_KEY
    source: file:/path/to/secret  # Load from file
    description: API authentication key
```

Secret sources:
- `env:VAR_NAME` - Load from environment variable
- `file:/path/to/file` - Load from file
- `plain:value` - Plain text (not recommended for production)

### 3. Monitoring

Configure health checks and alerts.

```yaml
monitoring:
  enabled: true
  healthCheck:
    intervalSeconds: 60
    endpoint: http://localhost:8080/health
  alerts:
    - type: email
      condition: "failure_rate > 0.1"
      config:
        recipients:
          - ops@example.com
```

### 4. Variables

Define reusable variables accessible across workflows.

```yaml
variables:
  environment: production
  region: us-east-1

workflows:
  - name: deploy
    steps:
      - type: shell
        config:
          command: deploy --env {{variables.environment}}
```

## Usage Examples

### Loading and Validating

```csharp
var iac = new InfrastructureAsCode(logger);

// Load from YAML
var infrastructure = await iac.LoadFromYamlAsync("infrastructure.yaml");

// Validate
var validation = iac.Validate(infrastructure);
if (!validation.IsValid)
{
    foreach (var error in validation.Errors)
        Console.WriteLine($"Error: {error}");
}
```

### Deploying Infrastructure

```csharp
// Deploy infrastructure
var result = await iac.ApplyAsync(infrastructure);

if (result.Success)
{
    Console.WriteLine($"Deployed {result.DeployedResources.Count} resources");
    foreach (var resource in result.DeployedResources)
        Console.WriteLine($"  - {resource}");
}
else
{
    foreach (var error in result.Errors)
        Console.WriteLine($"Error: {error}");
}
```

### Generating from Existing Configuration

```csharp
// Generate IaC definition from existing workflows
var infrastructure = iac.GenerateFromExisting("/path/to/workflows");

// Save to YAML
await iac.SaveToYamlAsync(infrastructure, "infrastructure.yaml");
```

## Step Types

Supported step types:

- **shell**: Execute shell commands
- **http**: Make HTTP requests
- **delay**: Wait for specified duration
- **parallel**: Execute steps in parallel
- **conditional**: Conditional execution based on conditions

## Retry Strategies

- **exponential**: Delay doubles each retry (1s, 2s, 4s, 8s...)
- **linear**: Delay increases linearly (1s, 2s, 3s, 4s...)
- **constant**: Same delay for each retry (1s, 1s, 1s, 1s...)

## Schedule Format

Uses cron expression format:

```
┌───────────── minute (0 - 59)
│ ┌───────────── hour (0 - 23)
│ │ ┌───────────── day of month (1 - 31)
│ │ │ ┌───────────── month (1 - 12)
│ │ │ │ ┌───────────── day of week (0 - 6) (Sunday to Saturday)
│ │ │ │ │
* * * * *
```

Examples:
- `0 2 * * *` - Daily at 2 AM
- `*/15 * * * *` - Every 15 minutes
- `0 0 * * 0` - Weekly on Sunday at midnight
- `0 9 1 * *` - Monthly on the 1st at 9 AM

## Best Practices

1. **Version Control**: Always store IaC files in git
2. **Environment-Specific Configs**: Use separate files for dev, staging, production
3. **Secret Management**: Never store secrets in plain text, use env or file sources
4. **Validation**: Always validate before deploying
5. **Testing**: Test workflows in non-production environments first
6. **Documentation**: Add descriptions to all workflows and steps
7. **Monitoring**: Configure alerts for critical workflows

## Example Files

- `infrastructure.yaml` - Complete example in YAML format
- `infrastructure.json` - Complete example in JSON format

## See Also

- [Loco Documentation](../../README.md)
- [Workflow System](../workflows/)
- [Scheduling Guide](../scheduling/)
