# Example 1: Basic File Automation

This example demonstrates how to use Loco to automate file operations with rules.

## Overview

This example shows:
- Creating a SimpleLightEngine instance
- Defining file operations using rules
- Executing rules programmatically
- Checking engine status

## Code Example

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core;
using Loco.Core.Models;
using Loco.Core.Configuration;

namespace LocoExamples
{
    class BasicFileAutomation
    {
        static async Task Main(string[] args)
        {
            // 1. Create a logger (optional but recommended)
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            var logger = loggerFactory.CreateLogger<SimpleLightEngine>();

            // 2. Create configuration
            var config = new LocoConfig();

            // 3. Create the automation engine
            using var engine = new SimpleLightEngine(logger, config);

            // 4. Start the engine
            await engine.StartAsync();
            Console.WriteLine("Automation engine started!\n");

            // 5. Prepare test directory and file
            var testDir = Path.Combine(Path.GetTempPath(), "loco-example-01");
            Directory.CreateDirectory(testDir);
            var testFile = Path.Combine(testDir, "important.txt");
            File.WriteAllText(testFile, "Important data for backup");
            Console.WriteLine($"Created test file: {testFile}\n");

            // 6. Create a rule to check if file exists
            var checkRuleId = engine.CreateRule(
                name: "Check File Exists",
                trigger: new LightTrigger { Type = "manual" },
                actions: new[]
                {
                    new LightAction
                    {
                        Type = "file",
                        Parameters = new Dictionary<string, object>
                        {
                            ["operation"] = "exists",
                            ["target"] = testFile
                        }
                    }
                }
            );

            Console.WriteLine($"Created rule: Check File Exists (ID: {checkRuleId})");

            // 7. Create a rule to list files in directory
            var listRuleId = engine.CreateRule(
                name: "List Files",
                trigger: new LightTrigger { Type = "manual" },
                actions: new[]
                {
                    new LightAction
                    {
                        Type = "file",
                        Parameters = new Dictionary<string, object>
                        {
                            ["operation"] = "list",
                            ["path"] = testDir
                        }
                    }
                }
            );

            Console.WriteLine($"Created rule: List Files (ID: {listRuleId})\n");

            // 8. Create a rule to count files
            var countRuleId = engine.CreateRule(
                name: "Count Files",
                trigger: new LightTrigger { Type = "manual" },
                actions: new[]
                {
                    new LightAction
                    {
                        Type = "file",
                        Parameters = new Dictionary<string, object>
                        {
                            ["operation"] = "count",
                            ["path"] = testDir,
                            ["recursive"] = "false"
                        }
                    }
                }
            );

            Console.WriteLine($"Created rule: Count Files (ID: {countRuleId})\n");

            // 9. Execute the rules
            Console.WriteLine("=== Executing Rules ===\n");

            Console.WriteLine("1. Checking if file exists...");
            await engine.ExecuteRuleAsync(checkRuleId);
            Console.WriteLine();

            Console.WriteLine("2. Listing files in directory...");
            await engine.ExecuteRuleAsync(listRuleId);
            Console.WriteLine();

            Console.WriteLine("3. Counting files...");
            await engine.ExecuteRuleAsync(countRuleId);
            Console.WriteLine();

            // 10. Check engine status
            var status = engine.GetEngineStatus();
            Console.WriteLine($"=== Engine Status ===");
            Console.WriteLine($"Total Rules: {status.RuleCount}");
            Console.WriteLine($"Total Executions: {status.TotalExecutions}");
            Console.WriteLine($"Successful: {status.SuccessfulExecutions}");
            Console.WriteLine($"Success Rate: {status.SuccessRate:F1}%\n");

            // 11. Clean up test files
            try
            {
                Directory.Delete(testDir, true);
                Console.WriteLine($"Cleaned up test directory: {testDir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not clean up test directory: {ex.Message}");
            }

            // 12. Stop the engine
            await engine.StopAsync();
            Console.WriteLine("\nAutomation engine stopped.");
        }
    }
}
```

## Step-by-Step Explanation

### 1. Create a Logger (Optional)
```csharp
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
var logger = loggerFactory.CreateLogger<SimpleLightEngine>();
```
- Logging helps track what the engine is doing
- Console logging shows output in real-time
- You can set different log levels (Debug, Information, Warning, Error)

### 2. Create Configuration
```csharp
var config = new LocoConfig();
```
- Uses default settings (5 concurrent flows, etc.)
- Can be customized with specific values
- See Example 5 for configuration details

### 3. Create the Engine
```csharp
using var engine = new SimpleLightEngine(logger, config);
```
- Creates the automation engine instance
- `using` ensures proper cleanup when done

### 4. Start the Engine
```csharp
await engine.StartAsync();
```
- Initializes the engine
- Required before executing any rules

### 5. Prepare Test Files
```csharp
var testDir = Path.Combine(Path.GetTempPath(), "loco-example-01");
Directory.CreateDirectory(testDir);
var testFile = Path.Combine(testDir, "important.txt");
File.WriteAllText(testFile, "Important data for backup");
```
- Creates a temporary directory for testing
- Creates a test file to work with
- Uses temp directory for safe experimentation

### 6-8. Create Rules
```csharp
var ruleId = engine.CreateRule(
    name: "Check File Exists",
    trigger: new LightTrigger { Type = "manual" },
    actions: new[] { ... }
);
```
- A rule is a named automation with a trigger and actions
- "manual" trigger means we control when it runs
- Returns a unique rule ID for execution

### File Action Types
The file action supports these operations:
- **exists**: Check if file or directory exists
- **list**: List files in a directory (top 20)
- **count**: Count files and directories
- **size**: Calculate total size of files

### 9. Execute the Rules
```csharp
await engine.ExecuteRuleAsync(ruleId);
```
- Executes all actions in the rule
- Returns true if successful, false if failed
- Actions run in the order defined

### 10. Check Status
```csharp
var status = engine.GetEngineStatus();
```
- Get execution statistics
- Useful for monitoring and debugging

### 11-12. Cleanup and Stop
```csharp
Directory.Delete(testDir, true);
await engine.StopAsync();
```
- Clean up test files
- Clean shutdown of the engine
- Always call StopAsync() before exiting

## Running the Example

### Prerequisites
- .NET 8.0 SDK installed
- Loco.Core library referenced

### Compile and Run
```bash
# Create a new console app
dotnet new console -n LocoFileAutomation
cd LocoFileAutomation

# Add reference to Loco.Core
dotnet add reference path/to/Loco.Core/Loco.Core.csproj

# Copy the example code to Program.cs
# Then run:
dotnet run
```

## Expected Output

```
Automation engine started!

Created test file: C:\Users\...\Temp\loco-example-01\important.txt

Created rule: Check File Exists (ID: a1b2c3d4-e5f6-7890-abcd-ef1234567890)
Created rule: List Files (ID: b2c3d4e5-f678-9012-bcde-f12345678901)
Created rule: Count Files (ID: c3d4e5f6-7890-1234-cdef-123456789012)

=== Executing Rules ===

1. Checking if file exists...
info: Loco.Core.SimpleLightEngine[0]
      Executing rule: Check File Exists
[FILE] 2025-01-16 14:30:15 - C:\Users\...\Temp\loco-example-01\important.txt: EXISTS

2. Listing files in directory...
info: Loco.Core.SimpleLightEngine[0]
      Executing rule: List Files
[FILE] 2025-01-16 14:30:15 - Files in C:\Users\...\Temp\loco-example-01: 1
  important.txt (0 KB, 2025-01-16 14:30)

3. Counting files...
info: Loco.Core.SimpleLightEngine[0]
      Executing rule: Count Files
[FILE] 2025-01-16 14:30:15 - Path: C:\Users\...\Temp\loco-example-01 (top-level only)
[FILE] Files: 1, Directories: 0

=== Engine Status ===
Total Rules: 3
Total Executions: 3
Successful: 3
Success Rate: 100.0%

Cleaned up test directory: C:\Users\...\Temp\loco-example-01

Automation engine stopped.
```

## Advanced Examples

### Multiple Actions in One Rule
```csharp
var multiActionRule = engine.CreateRule(
    name: "Multi-Step File Check",
    trigger: new LightTrigger { Type = "manual" },
    actions: new[]
    {
        new LightAction
        {
            Type = "log",
            Parameters = new Dictionary<string, object>
            {
                ["message"] = "Starting file checks..."
            }
        },
        new LightAction
        {
            Type = "file",
            Parameters = new Dictionary<string, object>
            {
                ["operation"] = "exists",
                ["target"] = "C:\\data\\file.txt"
            }
        },
        new LightAction
        {
            Type = "file",
            Parameters = new Dictionary<string, object>
            {
                ["operation"] = "list",
                ["path"] = "C:\\data"
            }
        },
        new LightAction
        {
            Type = "log",
            Parameters = new Dictionary<string, object>
            {
                ["message"] = "File checks completed!"
            }
        }
    }
);

await engine.ExecuteRuleAsync(multiActionRule);
```

### Recursive Directory Size
```csharp
var sizeRule = engine.CreateRule(
    name: "Calculate Directory Size",
    trigger: new LightTrigger { Type = "manual" },
    actions: new[]
    {
        new LightAction
        {
            Type = "file",
            Parameters = new Dictionary<string, object>
            {
                ["operation"] = "size",
                ["path"] = @"C:\Users\Public\Documents",
                ["recursive"] = "true"
            }
        }
    }
);

await engine.ExecuteRuleAsync(sizeRule);
```

## Common Issues and Solutions

### Issue: Path not safe error
**Error**: `[FILE] Error: The specified path is not safe for operation`

**Solution**: Only use paths you have permission to access:
```csharp
// Use temp directory
var safePath = Path.GetTempPath();

// Or user documents
var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
```

### Issue: Access denied
**Error**: `[FILE] Access denied to: C:\...`

**Solution**:
- Run with administrator privileges, or
- Use paths in your user directory
- Check file/folder permissions

### Issue: Directory not found
**Error**: `[FILE] Directory not found: C:\...`

**Solution**: Create directory first:
```csharp
var path = @"C:\Users\Public\MyFolder";
Directory.CreateDirectory(path);
```

## Next Steps

- **Example 2**: Learn about scheduled automation
- **Example 3**: Explore rule persistence across restarts
- **Example 4**: Execute processes and commands
- **Example 5**: Advanced configuration options

## Related Documentation

- [Loco.Core API Documentation](../docs/API.md)
- [SimpleLightEngine Reference](../docs/API.md#simplelightengine)
- [LocoConfig Reference](../docs/API.md#lococonfig)

---

**Example Type**: Beginner
**Complexity**: ⭐☆☆☆☆
**Estimated Time**: 5 minutes
**Last Updated**: 2025-01-16
