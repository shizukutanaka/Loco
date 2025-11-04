# Serilog Structured Logging Guide

## Overview

Serilog is a modern structured logging library for .NET that provides:

- **Structured logging** with named properties
- **Multiple sinks** (console, file, JSON, etc.)
- **Log enrichment** with contextual information
- **Exception handling** with detailed stack traces
- **Correlation ID tracking** for request tracing
- **Performance optimized** with async I/O

## Installation & Configuration

### NuGet Packages

Already installed in the project:
- `Serilog` v3.1.0
- `Serilog.AspNetCore` v8.0.0
- `Serilog.Sinks.Console` v5.0.0
- `Serilog.Enrichers.Context` v4.3.0
- `Serilog.Exceptions` v8.4.1

### Basic Setup in Program.cs

```csharp
using Loco.Core.Logging;

var builder = WebApplicationBuilder.CreateBuilder(args);

// Add Serilog
builder.AddSerilogLogging();

// Add other services
builder.Services.AddControllers();

var app = builder.Build();

// Use Serilog request logging
app.UseSerilogRequestLogging();

// Other middleware
app.UseRouting();
app.MapControllers();

await app.RunAsync();
```

## Configuration Details

### Log Levels

Serilog supports 6 log levels:

1. **Verbose/Trace** - Most detailed, diagnostic information
2. **Debug** - Debug information for development
3. **Information** - General informational messages
4. **Warning** - Warning messages for potentially harmful situations
5. **Error** - Error messages for errors
6. **Fatal** - Fatal messages for critical failures

### Default Configuration

The `AddSerilogLogging()` method configures:

**Development Environment:**
- Minimum level: `Debug`
- Console output with colors
- Includes thread ID and timing information
- Detailed exception information

**Production Environment:**
- Minimum level: `Information`
- File-based JSON logging
- Rolling daily files (100 MB limit)
- 30-day retention

### Output Targets (Sinks)

#### Console Sink
```
[14:32:45 INF] HTTP GET /api/workflows started
[14:32:46 INF] HTTP GET /api/workflows completed with status 200 in 1234ms
```

#### Text File Sink
```
[2025-11-04 14:32:45.123 +00:00] [INF] HTTP GET /api/workflows started
[2025-11-04 14:32:46.357 +00:00] [INF] HTTP GET /api/workflows completed with status 200 in 1234ms
```

#### JSON File Sink
```json
{
  "Timestamp": "2025-11-04T14:32:45.1234567Z",
  "Level": "Information",
  "MessageTemplate": "HTTP {Method} {Path} started",
  "Properties": {
    "Method": "GET",
    "Path": "/api/workflows",
    "CorrelationId": "0HMVLPG1GB0J8:00000001",
    "MachineName": "DESKTOP-ABC123",
    "ThreadId": 5,
    "ProcessId": 1234
  }
}
```

## Context and Enrichment

### Setting Context

Context properties are automatically included in all logs:

```csharp
using Serilog.Context;

// Set correlation ID
using (LogContext.PushProperty("CorrelationId", correlationId))
{
    _logger.Information("Processing request");
    // CorrelationId will be in all logs within this scope
}
```

Or use the helper methods:

```csharp
using (SerilogContext.SetCorrelationId(correlationId))
using (SerilogContext.SetUserId(userId))
{
    _logger.Information("Processing operation");
}
```

### Automatic Enrichment

The configuration automatically enriches every log with:
- `Timestamp` - When the log occurred
- `MachineName` - Server name
- `ThreadId` - Thread identifier
- `ProcessId` - Process identifier
- `EnvironmentName` - Development/Production/etc
- `Application` - "Loco"
- `Version` - "1.0.0"

### Custom Enrichment

Add custom enrichers:

```csharp
var config = new LoggerConfiguration()
    .Enrich.WithProperty("UserId", userId)
    .Enrich.WithProperty("TenantId", tenantId)
    .Enrich.FromLogContext()
    .CreateLogger();
```

## Structured Logging Patterns

### Pattern 1: Information Logging

```csharp
// Bad - concatenated strings
_logger.Information("User " + userId + " logged in");

// Good - named properties
_logger.Information("User logged in", userId);

// Best - structured properties
_logger.Information("User logged in - {UserId}", userId);
```

### Pattern 2: Object Logging

```csharp
var user = new { Id = 123, Email = "user@example.com" };

// Logs entire object as structured data
_logger.Information("Creating user {@User}", user);
```

### Pattern 3: Exception Logging

```csharp
try
{
    // Operation
}
catch (Exception ex)
{
    // Exception and context are automatically captured
    _logger.Error(ex, "Operation failed for {EntityId}", entityId);
}
```

### Pattern 4: Performance Metrics

```csharp
var stopwatch = Stopwatch.StartNew();

// Operation
await ProcessWorkflowAsync(workflow);

stopwatch.Stop();

_logger.Information(
    "Workflow processed in {ElapsedMs}ms",
    stopwatch.ElapsedMilliseconds);
```

### Pattern 5: Request/Response Logging

```csharp
_logger.Information(
    "HTTP {Method} {Path} returned {StatusCode} in {ElapsedMs}ms",
    request.Method,
    request.Path,
    response.StatusCode,
    stopwatch.ElapsedMilliseconds);
```

## Helper Extension Methods

### LogSecurityEvent
```csharp
_logger.LogSecurityEvent(
    @event: "LoginAttempt",
    userId: "user123",
    details: "Successful login from new IP",
    additionalProperties: new() { { "IpAddress", "192.168.1.1" } });
```

Logs to Warning level with security context.

### LogPerformance
```csharp
_logger.LogPerformance(
    operationName: "WorkflowExecution",
    elapsedMilliseconds: 2500,
    success: true);
```

Automatically uses Warning level if over 5 seconds.

### LogApiCall
```csharp
_logger.LogApiCall(
    method: "POST",
    path: "/api/workflows",
    statusCode: 201,
    elapsedMilliseconds: 1234);
```

Logs at appropriate level based on status code.

### LogBusinessEvent
```csharp
_logger.LogBusinessEvent(
    eventType: "WorkflowStarted",
    entityType: "Workflow",
    entityId: "wf-123",
    details: "Scheduled execution started");
```

Logs business domain events.

### LogOperation
```csharp
using var operation = _logger.LogOperation("UploadFile");

// Perform operation
await UploadFileAsync(file);

// Automatically logs completion time on dispose
```

Creates a disposable operation logger.

## Request/Response Logging

The `SerilogRequestLoggingMiddleware` automatically logs:

### On Request Start
```
[INF] HTTP GET /api/workflows started - ClientIP: 192.168.1.100
```

### On Request Complete
```
[INF] HTTP GET /api/workflows completed with status 200 in 1234ms
```

### On Error
```
[ERR] HTTP POST /api/workflows failed with exception after 5000ms
```

### Features
- Correlation ID extraction/generation
- Request body logging (for JSON/form data)
- Response body logging (for errors)
- Automatic timing
- Client IP tracking
- Status code-based logging levels

## Configuration Examples

### Custom Development Configuration

```csharp
var loggerConfig = SerilogScenarios.DevelopmentConfiguration();
Log.Logger = loggerConfig.CreateLogger();
```

### Custom Production Configuration

```csharp
var loggerConfig = SerilogScenarios.ProductionConfiguration("/var/log/loco");
Log.Logger = loggerConfig.CreateLogger();
```

### Custom Testing Configuration

```csharp
var loggerConfig = SerilogScenarios.TestingConfiguration();
Log.Logger = loggerConfig.CreateLogger();
```

## Log File Organization

### Default Log Paths
```
Logs/
├── loco-20251104.txt           # Daily text logs
├── loco-20251103.txt
├── loco-json-20251104.txt      # Daily JSON logs
└── loco-json-20251103.txt
```

### Log Rotation
- **Interval**: Daily (rolls at midnight)
- **File size**: 100 MB limit per file
- **Retention**: 30 days of files
- **Naming**: Automatic date stamping

## Querying and Analysis

### Using Log Files for Analysis

**Count errors in current log:**
```bash
grep "ERROR\|ERR" Logs/loco-*.txt | wc -l
```

**Find requests for specific correlation ID:**
```bash
grep "CorrelationId=\"0HMVLPG1GB0J8:00000001\"" Logs/loco-json-*.txt
```

**Parse JSON logs for analysis:**
```powershell
Get-Content Logs/loco-json-*.txt | ConvertFrom-Json |
  Where-Object { $_.Level -eq "Error" } |
  Select-Object Timestamp, MessageTemplate, Exception
```

### Centralizing Logs

For production, integrate with log aggregation services:

**Datadog:**
```csharp
.WriteTo.DatadogLogs(apiKey: "your-api-key")
```

**Application Insights:**
```csharp
.WriteTo.ApplicationInsights(telemetryClient, TelemetryConverter.Traces)
```

**Seq (recommended for development):**
```csharp
.WriteTo.Seq("http://localhost:5341")
```

**ELK Stack (Elasticsearch):**
```csharp
.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(nodes))
```

## Best Practices

### 1. Use Named Properties
```csharp
// Good
_logger.Information("User {UserId} completed action {Action}", userId, action);

// Avoid
_logger.Information($"User {userId} completed action {action}");
```

### 2. Include Context
```csharp
// Good - includes CorrelationId, UserId automatically
using (SerilogContext.SetCorrelationId(correlationId))
using (SerilogContext.SetUserId(userId))
{
    _logger.Information("Processing request");
}

// Less useful
_logger.Information("Processing request");
```

### 3. Use Appropriate Log Levels
```csharp
// Don't over-log
_logger.Information("Step 1 started");  // Avoid for every step
_logger.Information("Processing batch of {Count}", items.Count);  // Better

// Use Warning for recoverable issues
_logger.Warning("Retry attempt {Attempt} for {Operation}", attempt, operation);

// Use Error for failures
_logger.Error("Operation failed after {Attempts} attempts", maxRetries);
```

### 4. Log Exceptions with Context
```csharp
// Good
_logger.Error(ex, "Failed to process {EntityId}", entityId);

// Avoid
_logger.Error("Error: {Message}", ex.Message);
```

### 5. Secure Sensitive Information
```csharp
// Avoid logging passwords, tokens, PII
// Bad:
_logger.Information("Authenticating user {Email}", email);  // Avoid for PII

// Good:
_logger.Information("Authentication attempt for user {UserId}", userId);
```

### 6. Use Destructuring for Objects
```csharp
var request = new { Method = "POST", Path = "/api/data" };

// Good - structured
_logger.Information("Request: {@Request}", request);

// String conversion
_logger.Information("Request: {Request}", request.ToString());  // Less structured
```

## Performance Considerations

### Asynchronous Logging
All sinks are configured for async operations:
- File operations don't block request handling
- Console writes are optimized
- Batching for JSON logs

### Filtering
Unnecessary logs are filtered at configuration level:
- Microsoft.* components at Information level
- System.* at Warning level
- Reduces I/O and storage

### Sampling (Advanced)
For high-volume logging, implement sampling:

```csharp
.Filter.ByExcluding(logEvent =>
    logEvent.Level == LogEventLevel.Debug &&
    DateTime.UtcNow.Millisecond % 10 != 0)  // Sample 10% of Debug logs
```

## Troubleshooting

### Logs Not Appearing

1. **Check log level:**
   ```csharp
   // Ensure minimum level allows your log
   Log.Logger.Information("Test");  // Should always appear
   Log.Logger.Debug("Test");         // Only if Debug+ enabled
   ```

2. **Check file permissions:**
   ```bash
   # Ensure application has write permissions to Logs/ directory
   chmod 755 Logs/
   ```

3. **Verify middleware registration:**
   ```csharp
   // Must call before MapControllers
   app.UseSerilogRequestLogging();
   app.UseRouting();
   app.MapControllers();
   ```

### Performance Issues

1. **Check I/O:**
   - Move logs to faster disk
   - Use async sinks only
   - Implement file buffering

2. **Reduce verbosity:**
   - Increase minimum log level in production
   - Reduce detail in logs
   - Implement sampling

### File Corruption

1. **Verify rolling configuration:**
   - Check `rollingInterval` settings
   - Verify file size limits
   - Check directory permissions

2. **Monitor disk space:**
   ```bash
   du -sh Logs/
   ```

## Summary

Serilog provides:

✅ Structured logging with named properties
✅ Multiple output targets (Console, Files, JSON)
✅ Automatic context enrichment
✅ Correlation ID tracking
✅ Exception details capture
✅ Performance-optimized async operations
✅ Easy integration with existing Microsoft.Extensions.Logging
✅ Production-ready configuration

This creates a robust, queryable, and maintainable logging system for operational insights and debugging.
