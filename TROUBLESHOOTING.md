# Troubleshooting Guide

This guide helps you diagnose and resolve common issues with Loco Automation Platform.

## Quick Diagnostics

### Run Built-in Diagnostics

```powershell
# Comprehensive system check
.\Loco.Cli.exe diag

# Health check with JSON output
.\Loco.Cli.exe health --json

# View recent error logs
.\Loco.Cli.exe logs view 100 --level error
```

## Common Issues

### Installation Issues

#### Issue: "dotnet command not found"

**Symptoms:**
```
'dotnet' is not recognized as an internal or external command
```

**Solution:**
1. Download .NET 8 SDK or Runtime
2. Install from Microsoft's official site
3. Restart your terminal
4. Verify installation:
```powershell
dotnet --version
# Should show: 8.0.x
```

#### Issue: "Access denied" during installation

**Symptoms:**
```
Access to the path 'C:\Program Files\Loco' is denied
```

**Solution:**
1. Run PowerShell as Administrator:
```powershell
Start-Process powershell -Verb RunAs
```

2. Or install to user directory:
```powershell
# Install to user profile
$env:LOCALAPPDATA\Loco
```

#### Issue: Setup wizard fails

**Symptoms:**
```
Setup failed: Unable to create directory
```

**Solution:**
1. Check disk space:
```powershell
Get-PSDrive C | Select-Object Used,Free
```

2. Verify permissions:
```powershell
# Test write access
New-Item -Path "$env:LOCALAPPDATA\Loco" -ItemType Directory -Force
```

3. Run setup with verbose logging:
```powershell
.\Loco.Cli.exe setup --verbose
```

---

### Configuration Issues

#### Issue: Configuration not loaded

**Symptoms:**
```
Warning: Using default configuration
```

**Solution:**
1. Check configuration file exists:
```powershell
# Default location
Test-Path "$env:LOCALAPPDATA\Loco\loco.config.json"
```

2. Verify JSON syntax:
```powershell
# Validate JSON
Get-Content "$env:LOCALAPPDATA\Loco\loco.config.json" | ConvertFrom-Json
```

3. Check for syntax errors:
```json
{
  "LogDirectory": "C:\\Logs",  // ← Need double backslashes
  "MaxConcurrentFlows": 10      // ← No trailing comma on last item
}
```

#### Issue: Environment variables not working

**Symptoms:**
```
Configuration value not applied
```

**Solution:**
1. Verify environment variable syntax:
```powershell
# Correct format
$env:LOCO_LogDirectory = "C:\MyLogs"

# Check it's set
$env:LOCO_LogDirectory
```

2. Restart application after setting variables

3. Use `--` prefix for CLI overrides:
```powershell
.\Loco.Cli.exe start --log-directory "C:\MyLogs"
```

#### Issue: Invalid configuration values

**Symptoms:**
```
Configuration validation failed
```

**Solution:**
1. Check value ranges:
```json
{
  "MaxConcurrentFlows": 10,        // Must be > 0
  "DefaultTimeoutSeconds": 30,     // Must be > 0
  "MemoryLimitMB": 512,           // Must be > 0
  "RateLimitPerMinute": 60        // Must be > 0
}
```

2. Verify path format:
```json
{
  "AllowedPaths": [
    "C:\\Data",           // ✓ Correct: double backslash
    "C:/Data"             // ✓ Also correct: forward slash
  ]
}
```

---

### Runtime Issues

#### Issue: High memory usage

**Symptoms:**
```
Warning: Memory usage 950MB / 1024MB limit
```

**Solution:**
1. Check current memory usage:
```powershell
.\Loco.Cli.exe health --json
# Look at Memory_MB metric
```

2. Increase memory limit:
```json
{
  "MemoryLimitMB": 2048  // Increase limit
}
```

3. Reduce concurrent flows:
```json
{
  "MaxConcurrentFlows": 5  // Reduce from 10
}
```

4. Check for memory leaks:
```powershell
# Monitor over time
.\Loco.Cli.exe monitor memory --interval 10
```

#### Issue: High CPU usage

**Symptoms:**
```
CPU usage consistently above 80%
```

**Solution:**
1. Check running rules:
```powershell
.\Loco.Cli.exe rule list --status running
```

2. Identify resource-intensive rules:
```powershell
.\Loco.Cli.exe history stats
```

3. Disable problematic rules:
```powershell
.\Loco.Cli.exe rule disable <rule-id>
```

4. Adjust execution frequency:
```json
{
  "trigger": {
    "type": "interval",
    "parameters": {
      "minutes": "60"  // Reduce frequency
    }
  }
}
```

#### Issue: Disk space low

**Symptoms:**
```
Critical: Disk space 500MB free
```

**Solution:**
1. Check log size:
```powershell
Get-ChildItem "$env:LOCALAPPDATA\Loco\logs" -Recurse |
  Measure-Object -Property Length -Sum
```

2. Configure log rotation:
```json
{
  "LogRetentionDays": 7,  // Keep only 7 days
  "MaxLogSizeMB": 100     // Limit log size
}
```

3. Clean old logs manually:
```powershell
.\Loco.Cli.exe logs clean --older-than 7
```

4. Move logs to larger drive:
```json
{
  "LogDirectory": "D:\\Loco\\Logs"
}
```

---

### Execution Issues

#### Issue: Rule execution fails

**Symptoms:**
```
Error: Rule execution failed
```

**Solution:**
1. Check rule details:
```powershell
.\Loco.Cli.exe rule show <rule-id>
```

2. View error logs:
```powershell
.\Loco.Cli.exe logs view 50 --level error
```

3. Test rule manually:
```powershell
.\Loco.Cli.exe rule execute <rule-id> --verbose
```

4. Validate rule configuration:
```powershell
.\Loco.Cli.exe rule validate <rule-id>
```

#### Issue: Timeout errors

**Symptoms:**
```
Error: Operation exceeded timeout of 30 seconds
```

**Solution:**
1. Increase timeout globally:
```json
{
  "DefaultTimeoutSeconds": 60
}
```

2. Or per-rule:
```json
{
  "actions": [{
    "type": "process",
    "parameters": {
      "timeout": "120"  // 2 minutes
    }
  }]
}
```

3. Check if operation is actually hung:
```powershell
# Monitor process
Get-Process | Where-Object {$_.ProcessName -like "*Loco*"}
```

#### Issue: Permission denied errors

**Symptoms:**
```
Error: Access to path 'C:\Data\file.txt' is denied
```

**Solution:**
1. Check path is in allowed list:
```json
{
  "AllowedPaths": [
    "C:\\Data"  // Add this path
  ]
}
```

2. Verify file permissions:
```powershell
Get-Acl "C:\Data\file.txt" | Format-List
```

3. Run with appropriate account:
```powershell
# Check current user
whoami

# Grant permissions if needed
icacls "C:\Data" /grant "Username:(OI)(CI)F"
```

#### Issue: Command not whitelisted

**Symptoms:**
```
Error: Command 'xyz.exe' not in whitelist
```

**Solution:**
1. Add command to whitelist:
```json
{
  "CommandWhitelist": [
    "powershell.exe",
    "cmd.exe",
    "xyz.exe"  // Add your command
  ]
}
```

2. Or use absolute paths:
```json
{
  "CommandWhitelist": [
    "C:\\Tools\\xyz.exe"
  ]
}
```

3. Verify command location:
```powershell
Get-Command xyz.exe
```

---

### Performance Issues

#### Issue: Slow execution

**Symptoms:**
```
Workflow taking longer than expected
```

**Solution:**
1. Profile execution:
```powershell
.\Loco.Cli.exe history stats --detailed
```

2. Check concurrent limit:
```json
{
  "MaxConcurrentFlows": 20  // Increase if you have resources
}
```

3. Optimize action order:
- Put fast actions first
- Parallelize independent actions
- Add conditions to skip unnecessary work

4. Enable caching:
```json
{
  "EnableCache": true,
  "CacheTTLMinutes": 5
}
```

#### Issue: Database locks

**Symptoms:**
```
Error: Database is locked
```

**Solution:**
1. Reduce concurrent access:
```json
{
  "MaxConcurrentFlows": 5  // Reduce concurrency
}
```

2. Check for long-running transactions:
```powershell
.\Loco.Cli.exe diag --database
```

3. Restart engine to clear locks:
```powershell
.\Loco.Cli.exe restart
```

---

### Network Issues

#### Issue: IoT webhook timeout

**Symptoms:**
```
Error: Webhook request timeout
```

**Solution:**
1. Check network connectivity:
```powershell
Test-NetConnection mqtt.local -Port 1883
```

2. Increase timeout:
```json
{
  "actions": [{
    "type": "webhook",
    "parameters": {
      "url": "https://example.com/hook",
      "timeout": "30"
    }
  }]
}
```

3. Test webhook manually:
```powershell
Invoke-WebRequest -Uri "https://example.com/hook" -Method POST
```

4. Check firewall:
```powershell
# Test if port is blocked
Test-NetConnection -ComputerName example.com -Port 443
```

#### Issue: MQTT connection failed

**Symptoms:**
```
Error: Failed to connect to MQTT broker
```

**Solution:**
1. Verify broker is running:
```powershell
Test-NetConnection -ComputerName mqtt.local -Port 1883
```

2. Check credentials:
```json
{
  "broker_url": "http://mqtt.local:1883",
  "username": "your-username",
  "password": "your-password"
}
```

3. Test with MQTT client:
```powershell
# Install MQTT client
# Test connection
mqtt sub -h mqtt.local -p 1883 -t test/topic
```

---

### Security Issues

#### Issue: Audit log errors

**Symptoms:**
```
Warning: Failed to write audit log
```

**Solution:**
1. Check audit log directory:
```powershell
Test-Path "$env:LOCALAPPDATA\Loco\audit"
```

2. Verify disk space:
```powershell
Get-PSDrive C | Select-Object Free
```

3. Check permissions:
```powershell
Get-Acl "$env:LOCALAPPDATA\Loco\audit"
```

4. Configure audit location:
```json
{
  "AuditLogDirectory": "C:\\Audit\\Loco"
}
```

#### Issue: Encryption key error

**Symptoms:**
```
Error: Failed to decrypt configuration
```

**Solution:**
1. Regenerate encryption key:
```powershell
.\Loco.Cli.exe security regenerate-key
```

2. Re-encrypt sensitive data:
```powershell
.\Loco.Cli.exe security encrypt-config
```

3. Check key file permissions:
```powershell
Get-Acl "$env:LOCALAPPDATA\Loco\.key"
```

---

## Advanced Diagnostics

### Collecting Diagnostic Information

```powershell
# Full diagnostic report
.\Loco.Cli.exe diag --full --output diag-report.json

# Include logs
.\Loco.Cli.exe logs export --output logs.zip

# System information
.\Loco.Cli.exe info --detailed
```

### Enabling Debug Logging

```json
{
  "LogLevel": "Debug",  // Verbose logging
  "EnablePerformanceMetrics": true
}
```

### Performance Profiling

```powershell
# Start profiling
.\Loco.Cli.exe profile start

# Run operations
.\Loco.Cli.exe rule execute <rule-id>

# Stop and view results
.\Loco.Cli.exe profile stop --output profile.json
```

### Memory Analysis

```powershell
# Take memory snapshot
.\Loco.Cli.exe memory snapshot --output mem-snapshot.bin

# Analyze memory usage
.\Loco.Cli.exe memory analyze mem-snapshot.bin
```

---

## Getting Help

### Before Asking for Help

1. **Run diagnostics**:
```powershell
.\Loco.Cli.exe diag --full
```

2. **Check logs**:
```powershell
.\Loco.Cli.exe logs view 100 --level error
```

3. **Verify configuration**:
```powershell
.\Loco.Cli.exe config validate
```

4. **Search existing issues**:
- Check GitHub Issues
- Review documentation

### Reporting Issues

Include the following information:

1. **System Information**:
```powershell
.\Loco.Cli.exe info --detailed
```

2. **Error Messages**:
- Full error text
- Stack trace (if available)
- Timestamp

3. **Steps to Reproduce**:
- What you did
- What you expected
- What actually happened

4. **Configuration**:
- Sanitized config file (remove secrets)
- Environment variables (remove secrets)

5. **Logs**:
```powershell
.\Loco.Cli.exe logs export --output logs.zip
```

### Support Channels

- **Documentation**: [docs/](docs/)
- **GitHub Issues**: Bug reports
- **GitHub Discussions**: Questions
- **FAQ**: [FAQ.md](FAQ.md)

---

## Recovery Procedures

### Emergency Stop

```powershell
# Stop all operations
.\Loco.Cli.exe emergency-stop

# Or kill process
Stop-Process -Name Loco.Cli -Force
```

### Restore from Backup

```powershell
# List backups
.\Loco.Cli.exe backup list

# Restore configuration
.\Loco.Cli.exe backup restore --id <backup-id>

# Restore rules
.\Loco.Cli.exe rule restore --from backup-rules.json
```

### Reset to Defaults

```powershell
# Reset configuration
.\Loco.Cli.exe config reset

# Clear all rules
.\Loco.Cli.exe rule clear --confirm

# Fresh start
.\Loco.Cli.exe setup --reset
```

---

**Still having issues?** Check the [FAQ](FAQ.md) or create an issue on GitHub with diagnostic information.
