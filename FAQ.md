# Frequently Asked Questions (FAQ)

## General Questions

### What is Loco?

Loco is a government-grade automation platform designed for Windows environments. It provides secure, reliable automation for mission-critical operations with comprehensive audit logging, security controls, and operational monitoring.

### Who should use Loco?

- **Government agencies** requiring secure, auditable automation
- **Enterprises** needing reliable workflow automation
- **IT operations teams** managing system administration tasks
- **DevOps teams** automating deployment and maintenance
- **Power users** automating repetitive tasks

### Is Loco free?

Yes, Loco has a free **Personal edition** for individual use. Additional editions:
- **Small Business**: Paid, for teams of 5-20 people
- **Enterprise**: Paid, for large organizations with full features and support

### What platforms does Loco support?

- Windows 10/11 (64-bit)
- Windows Server 2019+ (64-bit)
- Windows Server Core
- Windows Containers

Linux and macOS support is planned for future releases.

---

## Installation & Setup

### How do I install Loco?

1. Download the latest release
2. Extract to your preferred directory
3. Run `Loco.Cli.exe setup`
4. Follow the interactive setup wizard

See [QUICK_START.md](QUICK_START.md) for detailed instructions.

### Do I need administrator privileges?

No, Loco can run as a standard user. However, some operations (like installing as a Windows Service) require administrator privileges.

### Where are configuration files stored?

Default locations:
- Configuration: `%LOCALAPPDATA%\Loco\loco.config.json`
- Rules: `%LOCALAPPDATA%\Loco\rules.json`
- Logs: `%LOCALAPPDATA%\Loco\logs\`
- Audit: `%LOCALAPPDATA%\Loco\audit\`

You can customize these locations in the configuration or via environment variables.

### How do I upgrade Loco?

1. Stop the current instance
2. Backup your configuration and rules
3. Replace the binaries with the new version
4. Restart Loco
5. Verify with `Loco.Cli.exe health`

See [CHANGELOG.md](CHANGELOG.md) for version-specific migration notes.

---

## Configuration

### How do I configure Loco?

Three ways:

1. **Configuration file** (`loco.config.json`):
```json
{
  "LogDirectory": "C:\\Logs",
  "MaxConcurrentFlows": 10
}
```

2. **Environment variables**:
```powershell
$env:LOCO_LogDirectory = "C:\Logs"
```

3. **Command-line arguments**:
```powershell
.\Loco.Cli.exe start --log-directory "C:\Logs"
```

Priority: CLI args > Environment variables > Config file > Defaults

### What configuration options are available?

Key options:
- `MaxConcurrentFlows`: Maximum parallel executions (default: 10)
- `DefaultTimeoutSeconds`: Operation timeout (default: 30)
- `MemoryLimitMB`: Memory usage limit (default: 512)
- `LogDirectory`: Log file location
- `AllowedPaths`: Permitted file paths (whitelist)
- `ForbiddenPaths`: Blocked file paths (blacklist)
- `CommandWhitelist`: Allowed commands for execution

See [docs/CONFIGURATION.md](docs/CONFIGURATION.md) for complete reference.

### How do I configure security settings?

Edit `loco.config.json`:

```json
{
  "AllowedPaths": [
    "C:\\Data",
    "C:\\Projects"
  ],
  "ForbiddenPaths": [
    "C:\\Windows",
    "C:\\Program Files"
  ],
  "CommandWhitelist": [
    "powershell.exe",
    "cmd.exe"
  ],
  "RateLimitPerMinute": 60,
  "EnableAuditLogging": true
}
```

See [docs/SECURITY_GUIDE.md](docs/SECURITY_GUIDE.md) for detailed guidance.

---

## Usage

### How do I create an automation rule?

Using presets (easiest):
```powershell
.\Loco.Cli.exe preset system     # System monitoring
.\Loco.Cli.exe preset cleanup    # File cleanup
.\Loco.Cli.exe preset daily      # Daily maintenance
```

Using JSON file:
```json
{
  "name": "My Automation",
  "trigger": {
    "type": "interval",
    "parameters": { "minutes": "15" }
  },
  "actions": [
    {
      "type": "log",
      "parameters": { "message": "Hello World" }
    }
  ]
}
```

Then import:
```powershell
.\Loco.Cli.exe rule import my-automation.json
```

### How do I schedule a rule to run at specific times?

Use cron-style scheduling:

```json
{
  "trigger": {
    "type": "cron",
    "parameters": {
      "schedule": "0 3 * * *"  // Daily at 3 AM
    }
  }
}
```

Common schedules:
- `0 * * * *` - Every hour
- `*/15 * * * *` - Every 15 minutes
- `0 9 * * 1-5` - 9 AM on weekdays
- `0 0 * * 0` - Midnight on Sundays

### How do I view logs?

```powershell
# View recent logs
.\Loco.Cli.exe logs view 50

# Filter by level
.\Loco.Cli.exe logs view 100 --level error

# Follow logs in real-time
.\Loco.Cli.exe logs tail

# Export logs
.\Loco.Cli.exe logs export --output logs.zip
```

### How do I check if Loco is running correctly?

```powershell
# Quick health check
.\Loco.Cli.exe health

# Detailed JSON output
.\Loco.Cli.exe health --json

# Run diagnostics
.\Loco.Cli.exe diag
```

---

## Security

### Is Loco secure?

Yes, Loco is designed with government-grade security:
- ✅ OWASP Top 10 (2021) compliance
- ✅ NIST 800-53 controls implemented
- ✅ Input validation and sanitization
- ✅ Path traversal prevention
- ✅ Command injection prevention
- ✅ Audit logging (SOC 2, HIPAA, GDPR)
- ✅ Encryption (AES-256, PBKDF2 600k)

See [SECURITY_IMPROVEMENTS.md](SECURITY_IMPROVEMENTS.md) for details.

### How does Loco protect against malicious input?

Multiple layers of protection:
1. **Input sanitization**: XSS, SQL injection, command injection patterns removed
2. **Path validation**: Whitelist/blacklist enforcement, traversal prevention
3. **Command whitelisting**: Only approved commands can execute
4. **Rate limiting**: Prevents DoS attacks
5. **Timeout enforcement**: Prevents hung processes
6. **Audit logging**: All operations logged

### Can I use Loco in a regulated environment?

Yes, Loco is designed for regulated environments:
- **SOC 2 Type II**: Security, availability, confidentiality controls
- **HIPAA**: Healthcare data protection (with proper configuration)
- **GDPR**: Data protection and privacy compliance
- **ISO 27001**: Information security management
- **PCI DSS**: Payment card data security

Ensure you configure appropriate security settings and conduct your own security review.

### How are secrets handled?

Best practices for secrets:
1. **Never hardcode**: Use environment variables or secure vaults
2. **Encrypt at rest**: Use encrypted storage for sensitive configuration
3. **Secure deletion**: Secrets are securely overwritten (DoD 5220.22-M)
4. **Audit logging**: Secret access is logged (without exposing values)

Example:
```json
{
  "api_key": "${env:API_KEY}"  // Reference environment variable
}
```

---

## Performance

### What are the system requirements?

**Minimum**:
- CPU: 1 core
- RAM: 256MB
- Disk: 100MB + logs
- OS: Windows 10+

**Recommended**:
- CPU: 2+ cores
- RAM: 512MB - 2GB
- Disk: 1GB + logs
- OS: Windows Server 2019+

### How many concurrent flows can Loco handle?

Default limit is 10 concurrent flows. You can adjust based on your resources:

```json
{
  "MaxConcurrentFlows": 50  // Increase if you have resources
}
```

Practical limits:
- **Personal**: 5-10 flows
- **Small Business**: 20-50 flows
- **Enterprise**: 100+ flows

### How do I optimize performance?

1. **Increase concurrent limit** (if you have resources):
```json
{"MaxConcurrentFlows": 20}
```

2. **Enable caching**:
```json
{"EnableCache": true, "CacheTTLMinutes": 5}
```

3. **Optimize rule design**:
- Minimize file I/O
- Use conditions to skip unnecessary work
- Parallelize independent actions
- Increase intervals for polling

4. **Monitor resources**:
```powershell
.\Loco.Cli.exe monitor memory
.\Loco.Cli.exe monitor cpu
```

### Why is Loco using a lot of memory?

Common causes:
1. **Too many concurrent flows**: Reduce `MaxConcurrentFlows`
2. **Memory leak**: Update to latest version
3. **Large files**: Process files in chunks
4. **Caching**: Adjust cache settings

Check current usage:
```powershell
.\Loco.Cli.exe health --json
# Look at Memory_MB metric
```

---

## Troubleshooting

### Loco won't start. What should I do?

1. Check system requirements:
```powershell
dotnet --version  # Should be 8.0+
```

2. Run diagnostics:
```powershell
.\Loco.Cli.exe diag
```

3. Check logs:
```powershell
.\Loco.Cli.exe logs view 50 --level error
```

4. Verify configuration:
```powershell
.\Loco.Cli.exe config validate
```

See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for detailed help.

### Why is my rule not executing?

1. **Check if rule is enabled**:
```powershell
.\Loco.Cli.exe rule show <rule-id>
```

2. **Verify trigger configuration**:
```powershell
.\Loco.Cli.exe rule validate <rule-id>
```

3. **Check execution history**:
```powershell
.\Loco.Cli.exe history show <rule-id>
```

4. **View error logs**:
```powershell
.\Loco.Cli.exe logs view 50 --level error
```

### How do I reset Loco to defaults?

```powershell
# Reset configuration
.\Loco.Cli.exe config reset

# Clear all rules (WARNING: Irreversible!)
.\Loco.Cli.exe rule clear --confirm

# Fresh setup
.\Loco.Cli.exe setup --reset
```

**Important**: Backup your data first!

### Where can I get help?

1. **Documentation**: [docs/](docs/) folder
2. **Troubleshooting Guide**: [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
3. **GitHub Issues**: Bug reports and feature requests
4. **GitHub Discussions**: Questions and community support

---

## Advanced Usage

### Can I use Loco as a library in my .NET application?

Yes! Reference the `Loco.Core` NuGet package:

```csharp
using Loco.Core;

var engine = new SimpleLightEngine(logger);
await engine.StartAsync();

var ruleId = engine.CreateRule("My Rule", trigger, actions);
await engine.ExecuteRuleAsync(ruleId);
```

See [docs/API.md](docs/API.md) for complete API reference.

### Does Loco support IoT/Smart Home integration?

Yes, Loco supports:
- **MQTT**: Message publishing for IoT devices
- **Webhooks**: HTTP GET/POST requests
- **IFTTT**: Trigger IFTTT applets
- **Home Assistant**: Control smart home devices
- **System Triggers**: Battery, network, time-based automation

See [examples/iot-automation-examples.md](examples/iot-automation-examples.md).

### Can I run Loco as a Windows Service?

Yes, using NSSM (Non-Sucking Service Manager):

```powershell
# Install service
nssm install Loco "C:\Program Files\Loco\Loco.Cli.exe" start

# Configure service
nssm set Loco AppDirectory "C:\Program Files\Loco"
nssm set Loco DisplayName "Loco Automation"

# Start service
nssm start Loco
```

See [docs/PRODUCTION_DEPLOYMENT.md](docs/PRODUCTION_DEPLOYMENT.md).

### Can I extend Loco with custom actions?

Yes, Loco supports plugins. Create a .NET class library:

```csharp
public class MyCustomAction : IActionExecutor
{
    public async Task ExecuteAsync(LightAction action, ILogger? logger)
    {
        // Your custom logic here
    }
}
```

Place DLL in plugins directory and register in configuration.

See [docs/DEVELOPER.md](docs/DEVELOPER.md) for plugin development guide.

### How do I backup my Loco configuration?

```powershell
# Manual backup
Copy-Item "$env:LOCALAPPDATA\Loco" -Destination "C:\Backup\Loco" -Recurse

# Using CLI
.\Loco.Cli.exe backup create --output loco-backup.zip

# Restore
.\Loco.Cli.exe backup restore --input loco-backup.zip
```

Recommended: Automate backups with a Loco rule!

---

## Licensing & Support

### What's included in each edition?

| Feature | Personal | Small Business | Enterprise |
|---------|----------|----------------|------------|
| **Price** | FREE | Paid | Paid |
| **Max Flows** | 5 | 20 | 100 |
| **Memory Limit** | 128MB | 512MB | 2GB |
| **Audit Logs** | ❌ | ✅ | ✅ |
| **Priority Support** | ❌ | Email | 24/7 |
| **SLA** | No | Yes | Yes |

### How do I upgrade to a paid edition?

Contact your sales representative or visit the pricing page for upgrade options.

### Is there community support?

Yes! Community support is available through:
- **GitHub Discussions**: Ask questions, share ideas
- **GitHub Issues**: Bug reports, feature requests
- **Documentation**: Comprehensive guides and examples

### Can I contribute to Loco?

Yes! We welcome contributions:
- **Code**: Submit pull requests
- **Documentation**: Improve guides and examples
- **Bug Reports**: Help us identify issues
- **Feature Requests**: Suggest improvements

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

---

## Still Have Questions?

- **Check Documentation**: [docs/](docs/)
- **Search Issues**: Existing answers on GitHub
- **Ask Community**: GitHub Discussions
- **Report Bug**: GitHub Issues

**Need immediate help?** Run diagnostics and check logs:
```powershell
.\Loco.Cli.exe diag --full
.\Loco.Cli.exe logs view 100 --level error
```
