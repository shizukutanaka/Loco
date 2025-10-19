# Loco CLI - Quick Start Guide

Get up and running with Loco CLI in minutes!

## Installation

### Prerequisites
- .NET 8.0 SDK or later
- Windows, macOS, or Linux

### Build from Source

```bash
# Clone the repository
git clone https://github.com/yourusername/loco.git
cd loco

# Build the project
dotnet build -c Release

# Run Loco CLI
./src/Loco.Cli/bin/Release/net8.0/Loco.Cli.exe
```

## First Steps

### 1. Check System Health

Verify your installation and system status:

```bash
Loco.Cli.exe health
```

Expected output:
```
System Health Check
════════════════════════════════════════════════════════

Overall Status: ✓ Healthy

Health Checks:
  ✓ Memory Usage         [X] MB
  ✓ CPU Cores            [N]
  ✓ Disk Space           [X] GB free
  ✓ .NET Runtime         8.0.21
  ✓ Working Directory    Accessible
```

### 2. Explore Available Commands

See all available commands:

```bash
Loco.Cli.exe help
```

Get help for a specific command:

```bash
Loco.Cli.exe help <command>
```

### 3. Try Interactive Mode

Enter interactive mode for guided exploration:

```bash
Loco.Cli.exe interactive
```

## Common Tasks

### Monitoring & Diagnostics

**Check system health:**
```bash
Loco.Cli.exe health --json
```

**Generate diagnostics report:**
```bash
Loco.Cli.exe diag --verbose
```

**View recent logs:**
```bash
Loco.Cli.exe logs view 50
```

**Search logs:**
```bash
Loco.Cli.exe logs search "ERROR"
```

**Monitor system resources:**
```bash
Loco.Cli.exe resource monitor
```

### Automation Workflows

**List preset workflows:**
```bash
Loco.Cli.exe preset list
```

**Run system monitoring preset:**
```bash
Loco.Cli.exe preset system
```

**Execute custom workflow:**
```bash
Loco.Cli.exe workflow examples/workflows/system-monitoring.json
```

### Infrastructure as Code

**Validate IaC configuration:**
```bash
Loco.Cli.exe iac validate examples/iac/infrastructure.yaml
```

**Deploy infrastructure (dry-run):**
```bash
Loco.Cli.exe iac deploy examples/iac/web-application.yaml --dry-run
```

**Deploy infrastructure:**
```bash
Loco.Cli.exe iac deploy examples/iac/infrastructure.yaml
```

### File Operations

**Search for files:**
```bash
Loco.Cli.exe files search "*.cs" src/
```

**Show directory statistics:**
```bash
Loco.Cli.exe files stats
```

**Clean temporary files:**
```bash
Loco.Cli.exe files clean --dry-run
```

### Testing

**List test projects:**
```bash
Loco.Cli.exe test list
```

**Run all tests:**
```bash
Loco.Cli.exe test run
```

**Run specific tests:**
```bash
Loco.Cli.exe test run --filter "ResourceMonitor"
```

**Generate code coverage:**
```bash
Loco.Cli.exe test coverage --format html
```

### Configuration Management

**Backup configuration:**
```bash
Loco.Cli.exe backup-config create "Before upgrade"
```

**List backups:**
```bash
Loco.Cli.exe backup-config list
```

**Restore configuration:**
```bash
Loco.Cli.exe backup-config restore 1
```

### Updates

**Check for updates:**
```bash
Loco.Cli.exe update check
```

**Show version information:**
```bash
Loco.Cli.exe version
```

## Example Workflows

### Daily System Maintenance

```bash
# 1. Check system health
Loco.Cli.exe health

# 2. Backup configuration
Loco.Cli.exe backup-config create "Daily backup $(date)"

# 3. Run cleanup preset
Loco.Cli.exe preset cleanup

# 4. View logs for any errors
Loco.Cli.exe logs search "ERROR" 100
```

### Infrastructure Deployment

```bash
# 1. Validate configuration
Loco.Cli.exe iac validate infrastructure.yaml

# 2. Dry-run deployment
Loco.Cli.exe iac deploy infrastructure.yaml --dry-run

# 3. Deploy infrastructure
Loco.Cli.exe iac deploy infrastructure.yaml

# 4. Monitor resources
Loco.Cli.exe resource stats
```

### Development Workflow

```bash
# 1. Run tests
Loco.Cli.exe test run --verbose

# 2. Generate coverage report
Loco.Cli.exe test coverage --format html

# 3. Search code files
Loco.Cli.exe files search "*.cs" src/

# 4. Check diagnostics
Loco.Cli.exe diag --json > diagnostics.json
```

## Sample Files

Sample workflow and infrastructure files are available in:

- `examples/workflows/` - Workflow JSON files
  - `system-monitoring.json` - System monitoring workflow
  - `daily-backup.json` - Daily backup and cleanup
  - `parallel-processing.json` - Parallel task execution

- `examples/iac/` - Infrastructure as Code YAML files
  - `infrastructure.yaml` - Basic infrastructure
  - `web-application.yaml` - Complete web app stack
  - `microservices.yaml` - Microservices platform

## Tips & Tricks

### Aliases

Loco CLI supports command aliases for faster typing:

```bash
Loco.Cli.exe i              # interactive mode
Loco.Cli.exe diag           # diagnostics
Loco.Cli.exe wf             # workflow
Loco.Cli.exe tests          # test
```

### JSON Output

Many commands support `--json` for programmatic usage:

```bash
Loco.Cli.exe health --json > health.json
Loco.Cli.exe diag --json > diagnostics.json
```

### Piping and Filtering

Combine with standard tools:

```bash
# Windows PowerShell
Loco.Cli.exe logs view 1000 | Select-String "ERROR"

# Linux/macOS
./Loco.Cli.exe logs view 1000 | grep ERROR
```

### Help is Always Available

```bash
Loco.Cli.exe help               # All commands
Loco.Cli.exe help <command>     # Specific command
Loco.Cli.exe <command> --help   # Alternative syntax
```

## Next Steps

1. **Explore Examples**: Review sample files in `examples/`
2. **Read Documentation**: Check `docs/` for detailed guides
3. **Create Workflows**: Build custom automation workflows
4. **Customize**: Configure Loco for your environment
5. **Contribute**: Submit issues and pull requests

## Getting Help

- **Documentation**: See `docs/` directory
- **Examples**: Check `examples/` directory
- **Issues**: Report bugs on GitHub
- **Help Command**: `Loco.Cli.exe help <command>`

## Common Issues

### Command Not Found

Ensure you're in the correct directory or add Loco to your PATH.

### Permission Denied

On Linux/macOS, you may need to mark the executable:
```bash
chmod +x ./Loco.Cli.exe
```

### .NET Runtime Missing

Install .NET 8.0 SDK from: https://dotnet.microsoft.com/download

---

**Happy Automating!** 🚀

For more information, see the full documentation in the `docs/` directory.
