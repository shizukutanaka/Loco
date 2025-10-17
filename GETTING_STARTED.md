# Getting Started with Loco

This guide will help you quickly get started with the Loco automation platform.

## Prerequisites

- **Windows**: Windows 10/11 or Windows Server 2019+
- **.NET Runtime**: .NET 8.0 or later (or use standalone build)
- **Disk Space**: Minimum 1GB free space
- **Permissions**: Administrator rights for system monitoring features

## Installation Steps

### Step 1: Build or Download

**Option A: Build from Source**
```powershell
# Windows
cd Loco
scripts\build-production.bat

# The build creates two versions:
# - publish-production (requires .NET 8 Runtime)
# - publish-production-standalone (no .NET required, larger size)
```

**Option B: Use Pre-built Release**
- Download the latest release
- Extract to your preferred location
- Choose framework-dependent or standalone version

### Step 2: Configuration

1. Navigate to your installation directory
2. Create a `config` folder if it doesn't exist
3. Copy `config/loco.config.sample.json` to `config/loco.config.json`
4. Edit the configuration file:

```json
{
  "workingDirectory": "./data/working",
  "logDirectory": "./data/logs",
  "enableFileLogging": true,
  "enableConsoleLogging": true,
  "logLevel": "Information"
}
```

**Important Configuration Settings**:
- `allowedPaths`: Directories Loco can access (security)
- `forbiddenPaths`: Directories Loco must not access
- `maxConcurrentFlows`: Maximum parallel executions
- `enableAuditLogging`: Enable for production environments

### Step 3: Verify Installation

```powershell
# Check version
Loco.Cli.exe version

# Check system health
Loco.Cli.exe health

# View system information
Loco.Cli.exe info
```

## First Automation Task

### Example 1: System Health Check

```powershell
# Run built-in system monitoring preset
Loco.Cli.exe preset system

# This checks:
# - Memory usage
# - Disk space
# - System information
```

### Example 2: File Cleanup

```powershell
# Clean temporary files older than 7 days
Loco.Cli.exe preset cleanup

# View what would be deleted (dry run)
# Edit workflows/daily-maintenance.json and add "dryRun": "true"
```

### Example 3: Network Diagnostics

```powershell
# Check network connectivity
# Use the sample workflow
workflows\network-diagnostics.json

# This checks:
# - DNS resolution
# - Ping connectivity
# - HTTP/HTTPS availability
```

## Common Operations

### View Logs
```powershell
# View recent logs
Loco.Cli.exe logs view 50

# Search for errors
Loco.Cli.exe logs search "ERROR"

# View log statistics
Loco.Cli.exe logs stats
```

### File Operations
```powershell
# Search for log files
Loco.Cli.exe files search "*.log"

# View directory statistics
Loco.Cli.exe files stats "C:\Data"
```

### System Monitoring
```powershell
# Quick system statistics
Loco.Cli.exe quick stats

# Log a custom message
Loco.Cli.exe quick log "System maintenance started"
```

## Creating Custom Workflows

Create a JSON file in the `workflows` folder:

```json
{
  "name": "My Custom Workflow",
  "description": "Custom automation task",
  "actions": [
    {
      "id": "log-start",
      "type": "log",
      "parameters": {
        "message": "Starting custom workflow"
      }
    },
    {
      "id": "check-memory",
      "type": "monitor",
      "parameters": {
        "type": "memory",
        "threshold": "512"
      }
    },
    {
      "id": "cleanup-temp",
      "type": "cleanup",
      "parameters": {
        "target": "temp",
        "olderThanDays": "7"
      }
    }
  ]
}
```

## Best Practices

### Security
1. **Restrict Paths**: Configure `allowedPaths` and `forbiddenPaths`
2. **Enable Audit Logging**: Set `enableAuditLogging: true`
3. **Review Logs**: Regularly check logs for suspicious activity
4. **Secure Configuration**: Protect `loco.config.json` with appropriate permissions

### Performance
1. **Adjust Concurrent Flows**: Set `maxConcurrentFlows` based on your hardware
2. **Enable Memory Optimization**: Set `enableMemoryOptimization: true`
3. **Monitor Resources**: Use `Loco.Cli.exe quick stats` regularly

### Reliability
1. **Configure Timeouts**: Set appropriate `defaultTimeoutSeconds`
2. **Enable Circuit Breaker**: Set `enableCircuitBreaker: true`
3. **Set Retry Policy**: Configure `defaultRetryCount`
4. **Regular Backups**: Enable `enableAutoBackup: true`

## Troubleshooting

### Loco won't start
- Check .NET runtime installation: `dotnet --version`
- Verify configuration file syntax
- Check log files in the log directory
- Ensure sufficient disk space

### Commands not working
- Run `Loco.Cli.exe health` to check system status
- Check `Loco.Cli.exe info` for configuration issues
- Review path warnings in configuration
- Verify file permissions

### High memory usage
- Reduce `maxConcurrentFlows` in configuration
- Enable `enableMemoryOptimization`
- Check for long-running workflows
- Review log retention settings

### Cannot access files
- Check `allowedPaths` in configuration
- Verify path is not in `forbiddenPaths`
- Ensure proper file permissions
- Use absolute paths when possible

## Next Steps

1. **Read the Documentation**:
   - [User Manual](docs/USER_MANUAL.md) - Complete guide
   - [API Reference](docs/API.md) - API documentation
   - [Developer Guide](docs/DEVELOPER.md) - For developers

2. **Explore Sample Workflows**:
   - `workflows/system-health-check.json`
   - `workflows/daily-maintenance.json`
   - `workflows/network-diagnostics.json`

3. **Configure for Production**:
   - Review security settings
   - Set up scheduled tasks (Windows Task Scheduler)
   - Configure monitoring and alerting
   - Implement backup procedures

4. **Customize for Your Needs**:
   - Create custom workflows
   - Adjust configuration settings
   - Integrate with existing systems
   - Add custom automation rules

## Support

For questions or issues:
- Check the documentation in `docs/` directory
- Review sample workflows in `workflows/` directory
- Check configuration examples in `config/` directory
- Use `Loco.Cli.exe info` for diagnostics

---

**Welcome to Loco Automation Platform!**