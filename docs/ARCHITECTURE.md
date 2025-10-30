# Loco Architecture

## Overview

Loco is built on a modular, extensible architecture that emphasizes:

- **Minimal Core**: Keep the non-extensible core as small as possible
- **Plugin-First**: Most functionality implemented as extensions
- **Event-Driven**: Loose coupling through pub/sub messaging
- **Dependency Injection**: Full IoC container integration
- **Clean Architecture**: Separation of concerns with clear boundaries

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Loco Platform                            │
├─────────────────────────────────────────────────────────────┤
│  CLI Layer                                                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │   Commands   │  │  Interactive │  │  UI/Console  │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
├─────────────────────────────────────────────────────────────┤
│  API Layer (Optional)                                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │  REST API    │  │   GraphQL    │  │   WebSocket  │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
├─────────────────────────────────────────────────────────────┤
│  Extension System                                            │
│  ┌─────────────────────────────────────────────────────┐   │
│  │           Extension Manager                          │   │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐         │   │
│  │  │Extension1│  │Extension2│  │Extension3│  ...    │   │
│  │  └──────────┘  └──────────┘  └──────────┘         │   │
│  └─────────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────────┤
│  Hook System                                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │  Workflow    │  │   Command    │  │  File Ops    │     │
│  │    Hooks     │  │    Hooks     │  │    Hooks     │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
├─────────────────────────────────────────────────────────────┤
│  Core Services                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │  Workflow    │  │    Config    │  │   Logging    │     │
│  │   Engine     │  │   Manager    │  │   Service    │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │   Security   │  │  File Utils  │  │  Diagnostics │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
├─────────────────────────────────────────────────────────────┤
│  Infrastructure                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │   Database   │  │    Cache     │  │  Message Q   │     │
│  │   (Optional) │  │   (Redis)    │  │  (RabbitMQ)  │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
└─────────────────────────────────────────────────────────────┘
```

## Extension Architecture

### Extension Lifecycle

```
┌──────────────┐
│  Discovery   │  Scan extensions directory for .dll files
└──────┬───────┘
       │
       ▼
┌──────────────┐
│   Loading    │  Load assembly and create extension instance
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ Validation   │  Check dependencies and version compatibility
└──────┬───────┘
       │
       ▼
┌──────────────┐
│Initialization│  Call InitializeAsync with context
└──────┬───────┘
       │
       ▼
┌──────────────┐
│   Running    │  Extension is active, hooks registered
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  Shutdown    │  Call ShutdownAsync, cleanup resources
└──────────────┘
```

### Extension Isolation

Extensions run in isolated contexts:

```
┌─────────────────────────────────────────────┐
│ Extension A                                  │
│ ┌─────────────────────────────────────────┐ │
│ │ Context                                  │ │
│ │ • Data Directory: /data/extension-a/    │ │
│ │ • Log Directory: /logs/extension-a/     │ │
│ │ • Configuration: { ... }                │ │
│ │ • Service Provider: IServiceProvider    │ │
│ └─────────────────────────────────────────┘ │
└─────────────────────────────────────────────┘

┌─────────────────────────────────────────────┐
│ Extension B                                  │
│ ┌─────────────────────────────────────────┐ │
│ │ Context                                  │ │
│ │ • Data Directory: /data/extension-b/    │ │
│ │ • Log Directory: /logs/extension-b/     │ │
│ │ • Configuration: { ... }                │ │
│ │ • Service Provider: IServiceProvider    │ │
│ └─────────────────────────────────────────┘ │
└─────────────────────────────────────────────┘
```

### Hook System Architecture

Hooks provide interception points in Loco's execution:

```
┌──────────────────────────────────────────────────┐
│          Workflow Execution                       │
│                                                   │
│  Start ──> OnBeforeExecute ──> Execute           │
│                  ▲                  │             │
│                  │                  ▼             │
│           [Hook Invocation]   OnAfterExecute     │
│                  │                  │             │
│            ┌─────┴─────┐            ▼             │
│            │Extension A│         Complete         │
│            │Extension B│                          │
│            │Extension C│                          │
│            └───────────┘                          │
└──────────────────────────────────────────────────┘
```

### Event System Architecture

Extensions communicate through events:

```
┌──────────────────────────────────────────────────┐
│           Event Aggregator                        │
│                                                   │
│  Publishers                  Subscribers          │
│  ┌────────────┐             ┌────────────┐       │
│  │Extension A │─────┐       │Extension B │       │
│  └────────────┘     │       └────────────┘       │
│                     │              ▲              │
│  ┌────────────┐     │              │              │
│  │ Core Loco  │─────┼─────────────┘              │
│  └────────────┘     │                            │
│                     ▼                            │
│               Event: "workflow.started"          │
│               Data: { workflowId, name }         │
└──────────────────────────────────────────────────┘
```

## Core Components

### Extension Manager

**Responsibilities:**
- Discover and load extensions
- Manage extension lifecycle
- Validate dependencies
- Provide extension contexts
- Coordinate hook invocations
- Facilitate event pub/sub

**Key Methods:**
```csharp
Task<int> LoadExtensionsAsync()
Task<IExtension> LoadExtensionFromFileAsync(string path)
Task<bool> UnloadExtensionAsync(string extensionId)
IEnumerable<IExtension> GetLoadedExtensions()
void RegisterHook<THook>(THook hook)
IEnumerable<THook> GetHooks<THook>()
Task EmitEventAsync(string eventName, object data)
IDisposable SubscribeToEvent(string eventName, Func<object, Task> handler)
```

### Extension Context

**Provides:**
- Dependency injection container access
- Extension-specific configuration
- Isolated data and log directories
- Hook registration
- Event pub/sub

**Interface:**
```csharp
public interface IExtensionContext
{
    IServiceProvider Services { get; }
    IReadOnlyDictionary<string, object> Configuration { get; }
    string DataDirectory { get; }
    string LogDirectory { get; }
    void RegisterHook<THook>(THook hook);
    Task EmitEventAsync(string eventName, object data);
    IDisposable SubscribeToEvent(string eventName, Func<object, Task> handler);
}
```

## Data Flow

### Workflow Execution with Extensions

```
1. User executes workflow command
   ├─> CLI parses command
   └─> Creates workflow context

2. Before execution
   ├─> Emit "workflow.started" event
   ├─> Invoke IWorkflowHook.OnBeforeExecuteAsync
   │   ├─> Extension A modifies context
   │   ├─> Extension B validates input
   │   └─> Extension C logs execution
   └─> Check if execution should continue

3. Execute workflow
   ├─> Run workflow steps
   ├─> Apply file operation hooks
   ├─> Apply command hooks
   └─> Apply logging hooks

4. After execution
   ├─> Invoke IWorkflowHook.OnAfterExecuteAsync
   │   ├─> Extension A collects metrics
   │   ├─> Extension B updates database
   │   └─> Extension C sends notification
   └─> Emit "workflow.completed" event

5. Error handling (if error occurs)
   ├─> Invoke IWorkflowHook.OnErrorAsync
   │   ├─> Extension A logs error
   │   ├─> Extension B sends alert
   │   └─> Extension C attempts recovery
   └─> Emit "workflow.failed" event
```

## Security Architecture

### Extension Sandboxing

Extensions are sandboxed to prevent:
- Accessing other extensions' data
- Reading arbitrary file system locations
- Executing dangerous operations without permission

**Sandboxing Mechanisms:**
1. **Directory Isolation**: Each extension gets its own data/log directory
2. **File Path Validation**: All file operations are validated for path traversal
3. **Permission System**: Extensions declare required permissions
4. **Resource Limits**: CPU/memory limits (future)

### Security Layers

```
┌─────────────────────────────────────────────┐
│  Extension Code                              │
│  ├─> Restricted API surface                 │
│  └─> No direct file system access           │
├─────────────────────────────────────────────┤
│  Extension Context (Sandbox)                │
│  ├─> Validates file paths                   │
│  ├─> Enforces permissions                   │
│  └─> Logs security events                   │
├─────────────────────────────────────────────┤
│  Core Loco (Trusted)                        │
│  ├─> File system operations                 │
│  ├─> Database access                        │
│  └─> Network operations                     │
└─────────────────────────────────────────────┘
```

## Performance Considerations

### Extension Loading

- **Lazy Loading**: Extensions loaded on-demand
- **Parallel Loading**: Independent extensions load concurrently
- **Dependency Resolution**: Dependency graph computed before loading

### Hook Performance

- **Async Execution**: All hooks are async
- **Cancellation Support**: Hooks respect cancellation tokens
- **Timeout Protection**: Long-running hooks can be cancelled
- **Error Isolation**: Hook errors don't crash Loco

### Event Performance

- **Async Dispatch**: Events dispatched asynchronously
- **Fire-and-Forget**: Publishers don't wait for subscribers
- **Buffering**: High-volume events can be buffered

## Scalability

### Horizontal Scaling

Loco supports horizontal scaling through:
- **Stateless Design**: Core services are stateless
- **Distributed Caching**: Redis for shared state
- **Message Queuing**: RabbitMQ for async processing
- **Load Balancing**: Multiple instances behind load balancer

### Extension Scaling

Extensions can scale independently:
- **Per-Instance Extensions**: Some extensions run on all instances
- **Centralized Extensions**: Some extensions run on dedicated instances
- **Event-Driven**: Extensions communicate via message queue

## Monitoring and Observability

### Extension Metrics

- Extension load time
- Hook execution time
- Event processing time
- Health check status
- Error rates

### Tracing

Distributed tracing with OpenTelemetry:
- Workflow execution spans
- Hook invocation spans
- Extension initialization spans
- Cross-extension correlation

### Logging

Structured logging with context:
- Extension ID in all logs
- Correlation IDs for requests
- Workflow/command context
- Performance metrics

## Future Enhancements

### Planned Features

1. **Extension Marketplace**: Central repository for extensions
2. **Version Management**: Automatic extension updates
3. **Resource Limits**: CPU/memory limits per extension
4. **Remote Extensions**: Extensions running in separate processes
5. **Extension API Versioning**: Backward compatibility guarantees
6. **Hot Configuration Reload**: Update extension config without restart
7. **Extension Dependencies**: NuGet package management
8. **Extension Testing Framework**: Tools for testing extensions

### Research Areas

- **WebAssembly Extensions**: Run extensions in WASM sandbox
- **Language Interop**: Extensions in Python, JavaScript, etc.
- **Distributed Extensions**: Extensions across multiple nodes
- **Edge Computing**: Extensions on IoT devices

## Best Practices

### For Core Development

1. Keep core minimal - move functionality to extensions
2. Design with extensibility in mind
3. Document all extension points
4. Maintain backward compatibility
5. Provide migration guides for breaking changes

### For Extension Development

1. Follow single responsibility principle
2. Handle errors gracefully
3. Implement health checks
4. Use async/await correctly
5. Clean up resources in ShutdownAsync
6. Document configuration options
7. Provide examples and tests

## References

- [Extension Development Guide](./EXTENSION_DEVELOPMENT.md)
- [API Reference](./API_REFERENCE.md)
- [Security Policy](../SECURITY.md)
- [Contributing Guidelines](../CONTRIBUTING.md)
