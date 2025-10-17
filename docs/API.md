# Loco API Reference

This document provides comprehensive API reference for the Loco automation platform.

## Overview

Loco provides both RESTful HTTP APIs and programmatic .NET APIs for automation workflows.

## RESTful API / REST API

The web API described in earlier drafts is not included in the current build. Please use the CLI (`src/Loco.Cli`) or the in-process automation APIs in `Loco.Core` for all interactions.

以前の草稿で説明されていた Web API は現行ビルドには含まれていません。操作は CLI（`src/Loco.Cli`）または `Loco.Core` 内のオートメーション API を利用してください。


## Programmatic API

### .NET Core API

#### SimpleLightEngine
Main automation engine class.

```csharp
using Loco.Core;
using Microsoft.Extensions.Logging;

// Create engine with optional logger
var logger = LoggerFactory.Create(config => config.AddConsole()).CreateLogger<SimpleLightEngine>();
var engine = new SimpleLightEngine(logger);

// Start the engine
await engine.StartAsync();

// Create a rule
var ruleId = engine.CreateRule(
    "Health Check Rule",
    new LightTrigger
    {
        Type = "interval",
        Parameters = { ["minutes"] = "5" }
    },
    new[]
    {
        new LightAction
        {
            Type = "monitor",
            Parameters = { ["type"] = "health" }
        }
    }
);

// Execute a rule
var success = await engine.ExecuteRuleAsync(ruleId);

// Check engine health
var isHealthy = await engine.IsHealthyAsync();

// Get engine status
var status = engine.GetEngineStatus();
```

#### SimpleFlow
Flow definition and execution.

```csharp
using Loco.Core.Models;

// Create a flow
var flow = new SimpleFlow(
    "Data Processing Flow",
    "Process daily data files"
);

// Add actions to flow
flow.Actions.Add(new LogAction("log1", "Start Processing", "Beginning data processing"));
flow.Actions.Add(new FileAction("file1", "List Files", "list", "C:/data"));
flow.Actions.Add(new LogAction("log2", "End Processing", "Data processing completed"));

// Execute flow
var context = new ActionContext
{
    Variables = new Dictionary<string, object>(),
    Logger = logger
};

await flow.ExecuteAsync(context);
```

#### Configuration API
```csharp
using Loco.Core.Configuration;

// Create configuration
var config = new LocoConfig();

// Access configuration properties
Console.WriteLine($"Max Flows: {config.MaxConcurrentFlows}");
Console.WriteLine($"Log Directory: {config.LogDirectory}");
Console.WriteLine($"Memory Limit: {config.MemoryLimitMB} MB");

// Configuration is automatically loaded from:
// 1. LOCO_CONFIG_PATH environment variable
// 2. config/loco.config.json in the application directory
// 3. Default values
```

## Action Types

### Log Actions
Log messages and information.

**Parameters:**
- `message`: Message to log (required)

**Example:**
```json
{
  "type": "log",
  "parameters": {
    "message": "System health check completed"
  }
}
```

### File Actions
Perform file operations.

**Parameters:**
- `operation`: Operation type (list, search, stats, clean, organize)
- `path`: Target path (required for most operations)
- `pattern`: Search pattern (for search operation)
- `olderThanDays`: Days threshold (for clean operation)

**Examples:**
```json
{
  "type": "file",
  "parameters": {
    "operation": "list",
    "path": "C:/logs"
  }
}
```

### Monitor Actions
Monitor system resources.

**Parameters:**
- `type`: Monitor type (memory, disk, system, health)
- `threshold`: Threshold value (for memory/disk)
- `path": Target path (for disk monitoring)

**Examples:**
```json
{
  "type": "monitor",
  "parameters": {
    "type": "memory",
    "threshold": "512"
  }
}
```

### Process Actions
Execute system commands.

**Parameters:**
- `command`: Command to execute (required)
- `arguments`: Command arguments
- `workingDirectory`: Working directory
- `timeoutSeconds`: Command timeout

**Example:**
```json
{
  "type": "process",
  "parameters": {
    "command": "ping",
    "arguments": "google.com -c 4",
    "timeoutSeconds": "30"
  }
}
```

### Backup Actions
Backup files and directories.

**Parameters:**
- `source`: Source path (required)
- `destination`: Destination path (required)
- `compression`: Compression type (none, zip, gzip)
- `includeSubdirectories`: Include subdirectories (true/false)

**Example:**
```json
{
  "type": "backup",
  "parameters": {
    "source": "C:/data",
    "destination": "C:/backups/data.bak",
    "compression": "zip"
  }
}
```

### Cleanup Actions
Clean temporary files and logs.

**Parameters:**
- `target`: Target type (temp, logs, cache)
- `olderThanDays`: Remove files older than X days (required)
- `path`: Specific path to clean

**Example:**
```json
{
  "type": "cleanup",
  "parameters": {
    "target": "temp",
    "olderThanDays": "7"
  }
}
```

## Trigger Types

### Manual Triggers
Execute on demand.

**Parameters:**
- None required

### Interval Triggers
Execute at regular intervals.

**Parameters:**
- `minutes`: Interval in minutes (required)
- `startTime`: Start time (optional)
- `endTime`: End time (optional)

### Schedule Triggers
Execute at specific times.

**Parameters:**
- `time`: Time to execute (HH:mm format)
- `days`: Days of week (comma-separated: mon,tue,wed)

### File Triggers
Execute when files change.

**Parameters:**
- `path`: Path to monitor (required)
- `pattern`: File pattern to match
- `changeType`: Type of change (created, modified, deleted)

## Error Codes

### HTTP Status Codes
- `200`: Success
- `400`: Bad Request - Invalid input
- `404`: Not Found - Resource not found
- `500`: Internal Server Error - Server error
- `503`: Service Unavailable - Service unhealthy

### Common Error Messages
- `"Invalid workflow configuration"`
- `"Action type not supported"`
- `"Insufficient permissions"`
- `"Resource not found"`
- `"Service temporarily unavailable"`

## Rate Limiting

API endpoints are rate limited to prevent abuse:
- 100 requests per minute per endpoint
- Rate limit headers included in responses:
  - `X-RateLimit-Limit`: Maximum requests per minute
  - `X-RateLimit-Remaining`: Remaining requests
  - `X-RateLimit-Reset`: Time until limit resets

## Examples

### Complete Workflow Example
```json
{
  "name": "Daily System Check",
  "steps": [
    {
      "type": "log",
      "message": "Starting daily system check"
    },
    {
      "type": "monitor",
      "parameters": {
        "type": "memory",
        "threshold": "80"
      }
    },
    {
      "type": "monitor",
      "parameters": {
        "type": "disk",
        "path": "C:",
        "threshold": "90"
      }
    },
    {
      "type": "cleanup",
      "parameters": {
        "target": "temp",
        "olderThanDays": "7"
      }
    },
    {
      "type": "log",
      "message": "Daily system check completed"
    }
  ]
}
```

### Scheduled Backup Example
```json
{
  "workflowName": "Weekly Backup",
  "scheduleType": "weekly",
  "time": "02:00",
  "days": "sun"
}
```

This API reference covers all available endpoints and programmatic interfaces for the Loco automation platform.
