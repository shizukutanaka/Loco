# Loco API Reference

Complete API reference for extending and integrating with Loco.

## Core Interfaces

### IExtension

The base interface all extensions must implement.

```csharp
public interface IExtension
{
    // Metadata
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string Description { get; }
    string Author { get; }
    string License { get; }
    string? Url { get; }
    IEnumerable<string> Tags { get; }
    string? MinimumLocoVersion { get; }
    IEnumerable<string> Dependencies { get; }

    // Lifecycle
    Task InitializeAsync(IExtensionContext context, CancellationToken cancellationToken = default);
    Task ShutdownAsync(CancellationToken cancellationToken = default);
    Task<ExtensionHealth> CheckHealthAsync(CancellationToken cancellationToken = default);
}
```

**Properties:**
- **Id**: Unique identifier (kebab-case recommended, e.g., "my-extension")
- **Name**: Human-readable display name
- **Version**: Semantic version string (e.g., "1.0.0")
- **Description**: Brief description of functionality
- **Author**: Extension creator/organization
- **License**: SPDX license identifier
- **Url**: Homepage or repository URL (optional)
- **Tags**: Keywords for discovery and categorization
- **MinimumLocoVersion**: Minimum required Loco version (optional)
- **Dependencies**: Extension IDs this extension depends on

**Methods:**
- **InitializeAsync**: Called when extension is loaded. Register hooks and subscriptions here.
- **ShutdownAsync**: Called before extension is unloaded. Cleanup resources here.
- **CheckHealthAsync**: Called periodically to verify extension health.

### IExtensionContext

Context provided to extensions during initialization.

```csharp
public interface IExtensionContext
{
    IServiceProvider Services { get; }
    IReadOnlyDictionary<string, object> Configuration { get; }
    string DataDirectory { get; }
    string LogDirectory { get; }

    void RegisterHook<THook>(THook hook) where THook : class;
    Task EmitEventAsync(string eventName, object? data = null, CancellationToken cancellationToken = default);
    IDisposable SubscribeToEvent(string eventName, Func<object?, Task> handler);
}
```

**Properties:**
- **Services**: DI container for accessing Loco services
- **Configuration**: Extension-specific configuration values
- **DataDirectory**: Directory for extension data storage
- **LogDirectory**: Directory for extension logs

**Methods:**
- **RegisterHook**: Register a hook to intercept Loco behavior
- **EmitEventAsync**: Publish an event for other extensions
- **SubscribeToEvent**: Subscribe to events from Loco or other extensions

## Hook Interfaces

### IWorkflowHook

Intercept workflow execution.

```csharp
public interface IWorkflowHook
{
    Task<bool> OnBeforeExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default);
    Task OnAfterExecuteAsync(WorkflowContext context, WorkflowResult result, CancellationToken cancellationToken = default);
    Task<bool> OnErrorAsync(WorkflowContext context, Exception exception, CancellationToken cancellationToken = default);
}
```

**OnBeforeExecuteAsync:**
- Called before workflow execution
- Return `false` to cancel execution
- Can modify `context.Variables` and `context.Metadata`

**OnAfterExecuteAsync:**
- Called after successful workflow completion
- Cannot cancel execution
- Can inspect results and perform logging

**OnErrorAsync:**
- Called when workflow errors
- Return `true` to mark error as handled
- Can implement retry logic or error recovery

### ICommandHook

Intercept command execution.

```csharp
public interface ICommandHook
{
    Task<CommandHookResult> OnBeforeCommandAsync(
        string commandName,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default);

    Task OnAfterCommandAsync(
        string commandName,
        object? result,
        CancellationToken cancellationToken = default);
}
```

**CommandHookResult:**
```csharp
public class CommandHookResult
{
    public bool Continue { get; set; } = true;
    public Dictionary<string, object>? ModifiedArguments { get; set; }
    public string? CancellationReason { get; set; }
}
```

### IFileOperationHook

Intercept file read/write operations.

```csharp
public interface IFileOperationHook
{
    Task OnBeforeReadAsync(string filePath, CancellationToken cancellationToken = default);
    Task<string> OnAfterReadAsync(string filePath, string content, CancellationToken cancellationToken = default);
    Task<string> OnBeforeWriteAsync(string filePath, string content, CancellationToken cancellationToken = default);
    Task OnAfterWriteAsync(string filePath, CancellationToken cancellationToken = default);
}
```

**Use Cases:**
- Encryption/decryption
- Compression/decompression
- Content transformation
- Audit logging
- Validation

### ILogHook

Intercept logging operations.

```csharp
public interface ILogHook
{
    Task<LogHookResult> OnLogAsync(
        LogLevel level,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default);
}
```

**LogHookResult:**
```csharp
public class LogHookResult
{
    public bool ShouldLog { get; set; } = true;
    public LogLevel? ModifiedLevel { get; set; }
    public string? ModifiedMessage { get; set; }
}
```

### IConfigurationHook

Intercept configuration operations.

```csharp
public interface IConfigurationHook
{
    Task OnBeforeLoadAsync(CancellationToken cancellationToken = default);
    Task<Dictionary<string, object>> OnAfterLoadAsync(
        Dictionary<string, object> configuration,
        CancellationToken cancellationToken = default);
    Task OnConfigurationChangedAsync(
        string key,
        object? oldValue,
        object? newValue,
        CancellationToken cancellationToken = default);
}
```

### ISecurityHook

Intercept security operations.

```csharp
public interface ISecurityHook
{
    Task<bool> OnValidateAccessAsync(
        string resource,
        string action,
        Dictionary<string, object> context,
        CancellationToken cancellationToken = default);

    Task OnAuthenticationAsync(
        AuthenticationContext context,
        CancellationToken cancellationToken = default);

    Task<bool> OnAuthorizationAsync(
        AuthorizationContext context,
        CancellationToken cancellationToken = default);
}
```

### IHttpHook

Intercept HTTP requests (when API is enabled).

```csharp
public interface IHttpHook
{
    Task OnBeforeRequestAsync(
        HttpRequestContext context,
        CancellationToken cancellationToken = default);

    Task OnAfterRequestAsync(
        HttpRequestContext context,
        HttpResponseContext response,
        CancellationToken cancellationToken = default);
}
```

## ExtensionBase

Abstract base class providing default implementations.

```csharp
public abstract class ExtensionBase : IExtension
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Version { get; }
    public abstract string Description { get; }
    public virtual string Author => "Unknown";
    public virtual string License => "MIT";
    public virtual string? Url => null;
    public virtual IEnumerable<string> Tags => Array.Empty<string>();
    public virtual string? MinimumLocoVersion => null;
    public virtual IEnumerable<string> Dependencies => Array.Empty<string>();

    protected IExtensionContext? Context { get; private set; }

    public virtual Task InitializeAsync(IExtensionContext context, CancellationToken cancellationToken = default);
    public virtual Task ShutdownAsync(CancellationToken cancellationToken = default);
    public virtual Task<ExtensionHealth> CheckHealthAsync(CancellationToken cancellationToken = default);
}
```

**Recommendations:**
- Inherit from `ExtensionBase` instead of implementing `IExtension` directly
- Override only what you need
- Call `base` methods in overrides
- Store `Context` for later use

## Extension Manager

Manages extension lifecycle.

```csharp
public class ExtensionManager : IDisposable
{
    // Loading
    public Task<int> LoadExtensionsAsync(CancellationToken cancellationToken = default);
    public Task<IExtension?> LoadExtensionFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    // Unloading
    public Task<bool> UnloadExtensionAsync(string extensionId, CancellationToken cancellationToken = default);

    // Querying
    public IEnumerable<IExtension> GetLoadedExtensions();
    public IExtension? GetExtension(string extensionId);

    // Health
    public Task<Dictionary<string, ExtensionHealth>> CheckHealthAsync(CancellationToken cancellationToken = default);

    // Hooks
    public void RegisterHook<THook>(THook hook) where THook : class;
    public IEnumerable<THook> GetHooks<THook>() where THook : class;

    // Events
    public Task EmitEventAsync(string eventName, object? data = null, CancellationToken cancellationToken = default);
    public IDisposable SubscribeToEvent(string eventName, Func<object?, Task> handler);
}
```

## Built-in Events

### Workflow Events

- **workflow.started**: `{ workflowId, workflowName, timestamp }`
- **workflow.completed**: `{ workflowId, workflowName, duration, success }`
- **workflow.failed**: `{ workflowId, workflowName, error }`
- **workflow.step.started**: `{ workflowId, stepName, stepIndex }`
- **workflow.step.completed**: `{ workflowId, stepName, stepIndex, duration }`

### Command Events

- **command.executed**: `{ commandName, arguments, result, duration }`
- **command.failed**: `{ commandName, arguments, error }`

### File Events

- **file.created**: `{ filePath, size, timestamp }`
- **file.modified**: `{ filePath, size, timestamp }`
- **file.deleted**: `{ filePath, timestamp }`

### Configuration Events

- **config.loaded**: `{ source, values }`
- **config.changed**: `{ key, oldValue, newValue }`
- **config.saved**: `{ destination }`

### System Events

- **system.started**: `{ version, startTime }`
- **system.shutdown**: `{ uptime, timestamp }`
- **extension.loaded**: `{ extensionId, extensionName }`
- **extension.unloaded**: `{ extensionId, extensionName }`

## Helper Attributes

### ExtensionAttribute

Mark extension classes for discovery.

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ExtensionAttribute : Attribute
{
    public string Id { get; }
    public string Name { get; }

    public ExtensionAttribute(string id, string name);
}
```

**Usage:**
```csharp
[Extension("my-extension", "My Extension")]
public class MyExtension : ExtensionBase
{
    // ...
}
```

## Health Status

```csharp
public class ExtensionHealth
{
    public HealthStatus Status { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, object> Data { get; set; }
}

public enum HealthStatus
{
    Healthy,    // Extension is operating normally
    Degraded,   // Extension is working but with issues
    Unhealthy,  // Extension has critical issues
    Unknown     // Health status cannot be determined
}
```

## Best Practices

### Error Handling

Always handle errors gracefully:

```csharp
public override async Task InitializeAsync(IExtensionContext context, CancellationToken cancellationToken)
{
    try
    {
        await base.InitializeAsync(context, cancellationToken);
        // Your initialization code
    }
    catch (Exception ex)
    {
        // Log error
        Console.WriteLine($"Failed to initialize: {ex.Message}");
        throw; // Re-throw to signal failure
    }
}
```

### Async Operations

Use async/await correctly:

```csharp
// ✅ Good
public async Task OnBeforeExecuteAsync(WorkflowContext context, CancellationToken cancellationToken)
{
    await SomeAsyncOperationAsync(cancellationToken);
}

// ❌ Bad
public async Task OnBeforeExecuteAsync(WorkflowContext context, CancellationToken cancellationToken)
{
    SomeAsyncOperationAsync(cancellationToken).Wait(); // Blocks thread!
}
```

### Resource Cleanup

Always dispose resources:

```csharp
private IDisposable? _subscription;

public override async Task InitializeAsync(IExtensionContext context, CancellationToken cancellationToken)
{
    await base.InitializeAsync(context, cancellationToken);
    _subscription = context.SubscribeToEvent("some.event", HandleEvent);
}

public override async Task ShutdownAsync(CancellationToken cancellationToken)
{
    _subscription?.Dispose(); // Clean up!
    await base.ShutdownAsync(cancellationToken);
}
```

## See Also

- [Extension Development Guide](./EXTENSION_DEVELOPMENT.md)
- [Example Extensions](../examples/extensions/)
- [Contributing Guide](../CONTRIBUTING.md)
