# Loco Workflow Examples

This directory contains example workflow definitions demonstrating Loco's cross-platform automation capabilities.

## Available Examples

### Android Examples
- **[android-morning-routine.json](android-morning-routine.json)** - Automated morning routine
  - Triggers: Time-based (weekday mornings at 7 AM)
  - Actions: WiFi toggle, volume control, brightness, notifications
  - Constraints: Battery level, location

### iOS Examples
- **[ios-focus-mode.json](ios-focus-mode.json)** - Work focus mode automation
  - Triggers: Time-based and location-based
  - Actions: Focus mode activation, shortcuts integration
  - Demonstrates iOS-specific features

### Windows Examples
- **[windows-file-backup.json](windows-file-backup.json)** - Automated file backup
  - Triggers: Scheduled and file system events
  - Actions: File compression, HTTP upload, fallback to local backup
  - Demonstrates complex error handling and retry policies

### Mac Examples
- **[mac-productivity-mode.json](mac-productivity-mode.json)** - Deep work mode
  - Triggers: Hotkey (Cmd+Shift+F9)
  - Actions: AppleScript for app control, Spotify integration, notifications
  - Demonstrates Mac-specific automation features

### Cross-Platform Examples
- **[cross-platform-notification.json](cross-platform-notification.json)** - Daily reminder
  - Works on: Android, iOS, Windows, Mac, Linux
  - Uses only universally supported features
  - Demonstrates API integration with fallback

## Workflow Schema

All workflows follow the Loco cross-platform schema:

```json
{
  "version": "1.0",
  "id": "unique-workflow-id",
  "name": "Workflow Name",
  "description": "What this workflow does",
  "platforms": ["android", "ios", "windows", "mac", "linux"],
  "enabled": true,
  "triggers": [...],
  "constraints": [...],
  "actions": [...]
}
```

### Key Components

#### Triggers
Triggers define when a workflow should execute:
- `time` - Schedule-based (cron syntax)
- `location` - GPS/geofencing
- `file_system` - File/folder changes
- `hotkey` - Keyboard shortcuts
- `app_launch` - Application events
- And many more...

#### Constraints
Constraints are conditions that must be met:
- `battery` - Battery level checks
- `network` - Network connectivity
- `time` - Time range restrictions
- `location` - Location requirements
- `idle` - System idle time

#### Actions
Actions define what the workflow does:
- `notification` - Display notifications
- `http_request` - Make API calls
- `file_operation` - File/folder operations
- `run_program` - Execute programs
- `applescript` - Mac-specific scripting
- `shell` - Shell commands
- Platform-specific actions (WiFi, Bluetooth, etc.)

## Error Handling

Workflows support robust error handling:

```json
{
  "onError": {
    "strategy": "fallback",
    "fallbackAction": {
      "type": "notification",
      "parameters": {
        "message": "Workflow failed, using fallback"
      }
    },
    "logError": true
  }
}
```

Strategies:
- `stop` - Stop workflow on error
- `continue` - Continue to next action
- `fallback` - Execute alternative action

## Retry Policies

Actions can have retry policies for transient failures:

```json
{
  "retry": {
    "maxAttempts": 3,
    "delayMs": 1000,
    "backoffStrategy": "exponential"
  }
}
```

Backoff strategies:
- `fixed` - Same delay between retries
- `linear` - Linearly increasing delay
- `exponential` - Exponentially increasing delay

## Platform Support

| Trigger Type | Android | iOS | Windows | Mac | Linux |
|-------------|---------|-----|---------|-----|-------|
| time | ✓ | ✓ | ✓ | ✓ | ✓ |
| location | ✓ | ✓ | ✗ | ✗ | ✗ |
| file_system | ✗ | ✗ | ✓ | ✓ | ✓ |
| hotkey | ✗ | ✗ | ✓ | ✓ | ✓ |
| app_launch | ✓ | ✓ | ✓ | ✓ | ✓ |
| battery | ✓ | ✗ | ✓ | ✓ | ✓ |
| wifi | ✓ | ✓ | ✓ | ✓ | ✓ |
| bluetooth | ✓ | ✓ | ✓ | ✓ | ✓ |

| Action Type | Android | iOS | Windows | Mac | Linux |
|------------|---------|-----|---------|-----|-------|
| notification | ✓ | ✓ | ✓ | ✓ | ✓ |
| http_request | ✓ | ✓ | ✓ | ✓ | ✓ |
| file_operation | ✗ | ✗ | ✓ | ✓ | ✓ |
| run_program | ✗ | ✗ | ✓ | ✓ | ✓ |
| applescript | ✗ | ✗ | ✗ | ✓ | ✗ |
| shell | ✗ | ✗ | ✓ | ✓ | ✓ |
| wifi_toggle | ✓ | ✓ | ✓ | ✓ | ✓ |
| bluetooth_toggle | ✓ | ✓ | ✓ | ✓ | ✓ |
| volume | ✓ | ✗ | ✓ | ✓ | ✓ |
| brightness | ✓ | ✗ | ✓ | ✓ | ✓ |

## Using These Examples

### 1. Validate a Workflow

```csharp
using Loco.Core.Workflow;

var parser = new WorkflowParser();
var (workflow, validation) = await parser.ParseAndValidateFileAsync("android-morning-routine.json");

if (!validation.IsValid)
{
    Console.WriteLine(validation.ToString());
}
```

### 2. Load and Modify

```csharp
var workflow = await parser.ParseFileAsync("ios-focus-mode.json");
workflow.Enabled = false;
workflow.UpdatedAt = DateTime.UtcNow;
await parser.SaveFileAsync(workflow, "ios-focus-mode-disabled.json");
```

### 3. Create New Workflow

```csharp
var workflow = new WorkflowDefinition
{
    Name = "My Custom Workflow",
    Platforms = new List<string> { "windows", "mac" },
    Triggers = new List<WorkflowTrigger>
    {
        new WorkflowTrigger
        {
            Type = "time",
            Parameters = new Dictionary<string, object>
            {
                ["schedule"] = "0 9 * * *"
            }
        }
    },
    Actions = new List<WorkflowAction>
    {
        new WorkflowAction
        {
            Type = "notification",
            Parameters = new Dictionary<string, object>
            {
                ["title"] = "Good Morning",
                ["message"] = "Have a great day!"
            }
        }
    }
};

await parser.SaveFileAsync(workflow, "my-workflow.json");
```

## Best Practices

1. **Platform Compatibility**: Use the `PlatformCapabilities` class to check feature support before creating workflows
2. **Error Handling**: Always include error handling strategies for critical actions
3. **Retry Policies**: Use exponential backoff for network operations
4. **Constraints**: Add constraints to avoid running workflows in inappropriate conditions
5. **Testing**: Validate workflows before deployment using `WorkflowValidator`
6. **Documentation**: Include clear descriptions for triggers, constraints, and actions

## Next Steps

- See [../resilience-example.cs](../resilience-example.cs) for resilience patterns
- See [../observability-example.cs](../observability-example.cs) for monitoring
- Read the main [README.md](../../README.md) for architecture overview
- Check [CHANGELOG.md](../../CHANGELOG.md) for latest features

## Contributing

To contribute new workflow examples:

1. Create a new JSON file following the schema
2. Validate it using `WorkflowValidator`
3. Test on target platforms
4. Update this README with your example
5. Submit a pull request

---

**Need Help?**
- Documentation: See [docs/](../../docs/)
- Issues: [GitHub Issues](https://github.com/your-org/loco/issues)
- Community: [Discussions](https://github.com/your-org/loco/discussions)
