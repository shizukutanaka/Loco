# Extension Development Guide

Welcome to Loco's extension development guide! This document will help you create powerful extensions to enhance Loco's functionality.

## Table of Contents

- [Quick Start](#quick-start)
- [Extension Architecture](#extension-architecture)
- [Creating Your First Extension](#creating-your-first-extension)
- [Hooks System](#hooks-system)
- [Event System](#event-system)
- [Best Practices](#best-practices)
- [Publishing Extensions](#publishing-extensions)

## Quick Start

### Prerequisites

- .NET 8.0 SDK or later
- Loco installed and running
- Basic C# knowledge

### Project Structure

```
MyLocoExtension/
├── MyLocoExtension.csproj
├── MyExtension.cs
├── README.md
└── manifest.json
```

## Extension Architecture

Loco's extension system is built on these core principles:

1. **Minimal Core**: The core is intentionally small, with most functionality extensible
2. **Isolation**: Extensions run in isolated contexts with their own data directories
3. **Lifecycle Management**: Full control over initialization and shutdown
4. **Event-Driven**: Pub/sub messaging for loose coupling
5. **Hook-Based**: Intercept and modify Loco's behavior at key points

### Extension Interface

Every extension implements `IExtension`:

```csharp
public interface IExtension
{
    string Id { get; }                    // Unique identifier
    string Name { get; }                  // Display name
    string Version { get; }               // Semantic version
    string Description { get; }           // What it does
    string Author { get; }                // Creator
    string License { get; }               // License type
    IEnumerable<string> Tags { get; }    // Capabilities
    IEnumerable<string> Dependencies { get; } // Required extensions

    Task InitializeAsync(IExtensionContext context);
    Task ShutdownAsync();
    Task<ExtensionHealth> CheckHealthAsync();
}
```

## Creating Your First Extension

### Step 1: Create a New Project

```bash
dotnet new classlib -n MyLocoExtension
cd MyLocoExtension
dotnet add reference /path/to/Loco.Core.dll
```

### Step 2: Implement IExtension

```csharp
using Loco.Core.Extensibility;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

[Extension("my-extension", "My Extension")]
public class MyExtension : ExtensionBase
{
    public override string Id => "my-extension";
    public override string Name => "My Extension";
    public override string Version => "1.0.0";
    public override string Description => "A sample extension that demonstrates Loco extensibility";
    public override string Author => "Your Name";
    public override string License => "MIT";
    public override IEnumerable<string> Tags => new[] { "example", "demo" };

    public override async Task InitializeAsync(IExtensionContext context, CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(context, cancellationToken);

        // Your initialization code here
        Console.WriteLine($"{Name} initialized!");

        // Subscribe to events
        context.SubscribeToEvent("workflow.started", async (data) =>
        {
            Console.WriteLine("Workflow started!");
        });

        // Register hooks
        context.RegisterHook(new MyWorkflowHook());
    }

    public override async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        // Cleanup code here
        Console.WriteLine($"{Name} shutting down...");
        await base.ShutdownAsync(cancellationToken);
    }
}
```

### Step 3: Build and Deploy

```bash
dotnet build -c Release
cp bin/Release/net8.0/MyLocoExtension.dll ~/.loco/extensions/
```

### Step 4: Load the Extension

```bash
loco extensions load
loco extensions list
```

## Hooks System

Hooks allow you to intercept and modify Loco's behavior at key points.

### Workflow Hooks

Modify workflow execution:

```csharp
using Loco.Core.Extensibility.Hooks;

public class MyWorkflowHook : IWorkflowHook
{
    public async Task<bool> OnBeforeExecuteAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        // Validate or modify workflow before execution
        Console.WriteLine($"Workflow {context.WorkflowName} is about to execute");

        // Add custom variables
        context.Variables["customData"] = "Hello from extension!";

        // Return false to cancel execution
        return true;
    }

    public async Task OnAfterExecuteAsync(WorkflowContext context, WorkflowResult result, CancellationToken cancellationToken)
    {
        // React to workflow completion
        Console.WriteLine($"Workflow completed in {result.Duration.TotalSeconds}s");
    }

    public async Task<bool> OnErrorAsync(WorkflowContext context, Exception exception, CancellationToken cancellationToken)
    {
        // Handle errors
        Console.WriteLine($"Workflow error: {exception.Message}");

        // Return true to mark as handled
        return false;
    }
}
```

### Command Hooks

Intercept command execution:

```csharp
public class MyCommandHook : ICommandHook
{
    public async Task<CommandHookResult> OnBeforeCommandAsync(
        string commandName,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken)
    {
        // Validate or modify command arguments
        if (commandName == "deploy" && !arguments.ContainsKey("environment"))
        {
            arguments["environment"] = "development"; // Set default
        }

        return new CommandHookResult
        {
            Continue = true,
            ModifiedArguments = arguments
        };
    }

    public async Task OnAfterCommandAsync(string commandName, object? result, CancellationToken cancellationToken)
    {
        // Log command execution
        Console.WriteLine($"Command {commandName} executed");
    }
}
```

### File Operation Hooks

Transform file content:

```csharp
public class MyFileHook : IFileOperationHook
{
    public async Task<string> OnAfterReadAsync(string filePath, string content, CancellationToken cancellationToken)
    {
        // Transform content after reading
        if (filePath.EndsWith(".json"))
        {
            // Decrypt, decompress, or transform JSON
            return TransformContent(content);
        }
        return content;
    }

    public async Task<string> OnBeforeWriteAsync(string filePath, string content, CancellationToken cancellationToken)
    {
        // Transform content before writing
        if (filePath.EndsWith(".json"))
        {
            // Encrypt, compress, or transform JSON
            return TransformContent(content);
        }
        return content;
    }

    public async Task OnBeforeReadAsync(string filePath, CancellationToken cancellationToken)
    {
        // Audit file access
    }

    public async Task OnAfterWriteAsync(string filePath, CancellationToken cancellationToken)
    {
        // Audit file writes
    }
}
```

### Available Hook Interfaces

- `IWorkflowHook` - Workflow execution
- `ICommandHook` - Command execution
- `IFileOperationHook` - File read/write operations
- `ILogHook` - Logging operations
- `IConfigurationHook` - Configuration changes
- `ISecurityHook` - Authentication and authorization
- `IHttpHook` - HTTP requests (when API is enabled)

## Event System

Extensions can communicate via events:

### Publishing Events

```csharp
// Emit an event
await Context.EmitEventAsync("my-extension.data-processed", new
{
    ItemCount = 100,
    ProcessedAt = DateTime.UtcNow
});
```

### Subscribing to Events

```csharp
// Subscribe to events
var subscription = Context.SubscribeToEvent("workflow.completed", async (data) =>
{
    Console.WriteLine("Workflow completed!");
    // Process event data
});

// Unsubscribe when done
subscription.Dispose();
```

### Built-in Events

- `workflow.started` - Workflow execution begins
- `workflow.completed` - Workflow execution completes
- `workflow.failed` - Workflow execution fails
- `command.executed` - Command completes
- `config.changed` - Configuration changes
- `file.created` - File created
- `file.modified` - File modified
- `file.deleted` - File deleted

## Best Practices

### 1. Use Semantic Versioning

Follow [SemVer](https://semver.org/):
- `1.0.0` - Initial release
- `1.1.0` - New features (backward compatible)
- `1.0.1` - Bug fixes
- `2.0.0` - Breaking changes

### 2. Handle Errors Gracefully

```csharp
public override async Task InitializeAsync(IExtensionContext context, CancellationToken cancellationToken)
{
    try
    {
        await base.InitializeAsync(context, cancellationToken);
        // Your initialization
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to initialize extension");
        throw; // Re-throw to signal failure
    }
}
```

### 3. Implement Health Checks

```csharp
public override async Task<ExtensionHealth> CheckHealthAsync(CancellationToken cancellationToken)
{
    try
    {
        // Check dependencies, connections, etc.
        var isHealthy = await CheckDatabaseConnection();

        return new ExtensionHealth
        {
            Status = isHealthy ? HealthStatus.Healthy : HealthStatus.Degraded,
            Message = isHealthy ? "All systems operational" : "Database connection slow",
            Data = new Dictionary<string, object>
            {
                ["connectionTime"] = 150,
                ["queueSize"] = 42
            }
        };
    }
    catch (Exception ex)
    {
        return new ExtensionHealth
        {
            Status = HealthStatus.Unhealthy,
            Message = ex.Message
        };
    }
}
```

### 4. Use Dependency Injection

Access Loco's services:

```csharp
public override async Task InitializeAsync(IExtensionContext context, CancellationToken cancellationToken)
{
    await base.InitializeAsync(context, cancellationToken);

    // Get services from DI container
    var logger = context.Services.GetService<ILogger<MyExtension>>();
    var config = context.Services.GetService<IConfiguration>();
}
```

### 5. Store Data in Dedicated Directories

```csharp
// Use provided directories
var dataFile = Path.Combine(Context.DataDirectory, "mydata.json");
var logFile = Path.Combine(Context.LogDirectory, "extension.log");
```

### 6. Document Your Extension

Create a comprehensive README.md:

````markdown
# My Extension

Brief description of what your extension does.

## Features

- Feature 1
- Feature 2

## Installation

```bash
loco extensions install my-extension
```

## Configuration

```json
{
  "myExtension": {
    "setting1": "value1"
  }
}
```

## Usage

```bash
loco my-command --option value
```

## License

MIT
````

## Publishing Extensions

### 1. Package Your Extension

Create a `manifest.json`:

```json
{
  "id": "my-extension",
  "name": "My Extension",
  "version": "1.0.0",
  "description": "A powerful extension for Loco",
  "author": "Your Name",
  "license": "MIT",
  "homepage": "https://github.com/you/my-extension",
  "repository": "https://github.com/you/my-extension",
  "tags": ["automation", "workflow"],
  "minimumLocoVersion": "1.0.0",
  "dependencies": []
}
```

### 2. Create a Release

```bash
# Build release
dotnet publish -c Release -o dist/

# Create package
cd dist
zip -r my-extension-1.0.0.zip *
```

### 3. Share Your Extension

- Publish to GitHub Releases
- Submit to Loco Extension Registry (coming soon)
- Share on Loco community forums

## Example Extensions

### Hello World Extension

```csharp
[Extension("hello-world", "Hello World")]
public class HelloWorldExtension : ExtensionBase
{
    public override string Id => "hello-world";
    public override string Name => "Hello World";
    public override string Version => "1.0.0";
    public override string Description => "A simple hello world extension";
    public override string Author => "Loco Team";

    public override async Task InitializeAsync(IExtensionContext context, CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(context, cancellationToken);

        context.SubscribeToEvent("workflow.started", async (data) =>
        {
            Console.WriteLine("Hello from Hello World extension!");
        });
    }
}
```

### Logging Extension

```csharp
[Extension("advanced-logger", "Advanced Logger")]
public class AdvancedLoggerExtension : ExtensionBase
{
    public override string Id => "advanced-logger";
    public override string Name => "Advanced Logger";
    public override string Version => "1.0.0";
    public override string Description => "Enhanced logging with custom formatters";
    public override string Author => "Loco Team";

    public override async Task InitializeAsync(IExtensionContext context, CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(context, cancellationToken);

        context.RegisterHook(new AdvancedLogHook());
    }

    private class AdvancedLogHook : ILogHook
    {
        public async Task<LogHookResult> OnLogAsync(
            LogLevel level,
            string message,
            Exception? exception,
            CancellationToken cancellationToken)
        {
            // Add emoji based on log level
            var emoji = level switch
            {
                LogLevel.Error => "❌",
                LogLevel.Warning => "⚠️",
                LogLevel.Information => "ℹ️",
                _ => "📝"
            };

            return new LogHookResult
            {
                ShouldLog = true,
                ModifiedMessage = $"{emoji} {message}"
            };
        }
    }
}
```

## Support

- Documentation: https://docs.loco.dev
- Issues: https://github.com/loco/loco/issues
- Discord: https://discord.gg/loco
- Email: extensions@loco.dev

## License

Extensions can use any OSI-approved license. We recommend MIT or Apache 2.0 for maximum compatibility.
