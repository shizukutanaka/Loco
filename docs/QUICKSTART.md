# Quick Start Guide

Get started with Loco in 5 minutes.

## Installation

### Using .NET CLI

```bash
# Clone the repository
git clone https://github.com/yourusername/loco.git
cd loco

# Build the project
dotnet build

# Run the CLI
dotnet run --project src/Loco.Cli -- health
```

### Using Docker

```bash
# Pull the image
docker pull loco/cli:latest

# Run health check
docker run loco/cli:latest health
```

## First Steps

### 1. Check System Health

```bash
loco health
```

Expected output:
```
✓ System: Healthy
✓ Engine: Running
✓ Configuration: Valid
✓ Database: Connected
```

### 2. Create Your First Workflow

Create a file `my-workflow.json`:

```json
{
  "name": "my-first-workflow",
  "steps": [
    {
      "type": "log",
      "message": "Hello, Loco!"
    },
    {
      "type": "file",
      "action": "create",
      "path": "output.txt",
      "content": "Workflow completed successfully"
    }
  ]
}
```

Run the workflow:

```bash
loco workflow run my-workflow.json
```

### 3. Explore Commands

```bash
# List all commands
loco --help

# Get help for specific command
loco workflow --help

# View system version
loco version

# Run diagnostics
loco diag
```

## Configuration

### Basic Configuration

Create `loco.config.json`:

```json
{
  "maxConcurrentFlows": 10,
  "enableTelemetry": true,
  "logLevel": "Information",
  "workingDirectory": "./workspace"
}
```

### Environment Variables

```bash
# Set log level
export LOCO_LOG_LEVEL=Debug

# Set working directory
export LOCO_WORKSPACE=/path/to/workspace

# Enable verbose mode
export LOCO_VERBOSE=true
```

## Common Tasks

### File Automation

```bash
# Search files
loco files search "*.txt" --path ./documents

# Get file statistics
loco files stats --path ./data

# Clean temporary files
loco files clean --pattern "*.tmp"
```

### Resource Management

```bash
# List resources
loco resource list

# Create resource
loco resource create database-backup --type backup

# Delete resource
loco resource delete old-backup
```

### Scheduled Tasks

```bash
# Create scheduled workflow
loco workflow schedule daily-backup \
  --cron "0 2 * * *" \
  --workflow backup.json
```

## Next Steps

- Read the [User Manual](USER_MANUAL.md)
- Explore [API Documentation](API.md)
- Check [Configuration Guide](CONFIGURATION.md)
- Review [Examples](../examples/)

## Troubleshooting

### Common Issues

**Issue**: Command not found
```bash
# Add to PATH or use full path
export PATH=$PATH:./src/Loco.Cli/bin/Release/net8.0
```

**Issue**: Permission denied
```bash
# Grant execute permission
chmod +x loco
```

**Issue**: Configuration not found
```bash
# Create default configuration
loco config init
```

## Getting Help

- GitHub Issues: [Report bugs](https://github.com/yourusername/loco/issues)
- Documentation: [Full docs](/)
- Community: [Discussions](https://github.com/yourusername/loco/discussions)