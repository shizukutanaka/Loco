# Example 3: Rule Persistence

This example demonstrates how to persist automation rules across engine restarts using JSON file storage.

## Overview

This example shows:
- Creating rules with persistent storage
- Rules surviving engine restarts
- Updating and managing persistent rules
- Using JsonFileRuleStore

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
using Loco.Core.Storage;

namespace LocoExamples
{
    class RulePersistence
    {
        static async Task Main(string[] args)
        {
            // 1. Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            var engineLogger = loggerFactory.CreateLogger<SimpleLightEngine>();
            var storeLogger = loggerFactory.CreateLogger<JsonFileRuleStore>();

            // 2. Define the rule storage file path
            var rulesFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Loco",
                "rules.json"
            );
            Console.WriteLine($"Rules will be stored in: {rulesFilePath}\n");

            // === FIRST RUN: Create and persist rules ===
            Console.WriteLine("=== FIRST RUN: Creating rules ===\n");
            await FirstRun(engineLogger, storeLogger, rulesFilePath);

            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("Press any key to simulate engine restart...");
            Console.ReadKey();
            Console.WriteLine(new string('=', 60) + "\n");

            // === SECOND RUN: Load persisted rules ===
            Console.WriteLine("=== SECOND RUN: Loading persisted rules ===\n");
            await SecondRun(engineLogger, storeLogger, rulesFilePath);

            Console.WriteLine("\nExample completed!");
        }

        static async Task FirstRun(ILogger engineLogger, ILogger storeLogger, string rulesFilePath)
        {
            // 1. Create rule store
            var ruleStore = new JsonFileRuleStore(rulesFilePath, storeLogger);

            // 2. Create engine with rule store
            using var engine = new SimpleLightEngine(engineLogger, null, ruleStore);
            await engine.StartAsync();

            // 3. Create several rules
            Console.WriteLine("Creating rules...");

            var rule1 = engine.CreateRule(
                name: "Morning Backup",
                trigger: new LightTrigger { Type = "scheduled", Schedule = "daily-8am" },
                actions: new[]
                {
                    new LightAction
                    {
                        Type = "log",
                        Parameters = new Dictionary<string, object>
                        {
                            ["message"] = "Running morning backup..."
                        }
                    }
                }
            );
            Console.WriteLine($"  ✓ Created: Morning Backup (ID: {rule1})");

            var rule2 = engine.CreateRule(
                name: "Hourly Health Check",
                trigger: new LightTrigger { Type = "interval", Interval = "1h" },
                actions: new[]
                {
                    new LightAction
                    {
                        Type = "log",
                        Parameters = new Dictionary<string, object>
                        {
                            ["message"] = "Health check: System OK"
                        }
                    }
                }
            );
            Console.WriteLine($"  ✓ Created: Hourly Health Check (ID: {rule2})");

            var rule3 = engine.CreateRule(
                name: "Evening Cleanup",
                trigger: new LightTrigger { Type = "scheduled", Schedule = "daily-6pm" },
                actions: new[]
                {
                    new LightAction
                    {
                        Type = "log",
                        Parameters = new Dictionary<string, object>
                        {
                            ["message"] = "Running evening cleanup..."
                        }
                    }
                }
            );
            Console.WriteLine($"  ✓ Created: Evening Cleanup (ID: {rule3})");

            // 4. Wait a moment for async persistence to complete
            await Task.Delay(200);

            // 5. Show engine status
            var status = engine.GetEngineStatus();
            Console.WriteLine($"\nEngine Status:");
            Console.WriteLine($"  Total Rules: {status.RuleCount}");

            // 6. Verify rules were persisted
            var allRules = await ruleStore.GetRulesAsync();
            Console.WriteLine($"\nPersisted to storage: {allRules.Count} rules");
            foreach (var rule in allRules)
            {
                Console.WriteLine($"  - {rule.Name} (Enabled: {rule.IsEnabled})");
            }

            // 7. Stop engine (rules remain in storage)
            await engine.StopAsync();
            Console.WriteLine("\nEngine stopped. Rules saved to disk.");
        }

        static async Task SecondRun(ILogger engineLogger, ILogger storeLogger, string rulesFilePath)
        {
            // 1. Create rule store (same file path)
            var ruleStore = new JsonFileRuleStore(rulesFilePath, storeLogger);

            // 2. Check what's in storage BEFORE starting engine
            var storedRules = await ruleStore.GetRulesAsync();
            Console.WriteLine($"Found {storedRules.Count} rules in storage:");
            foreach (var rule in storedRules)
            {
                Console.WriteLine($"  - {rule.Name} (ID: {rule.Id})");
            }

            // 3. Create engine with rule store
            using var engine = new SimpleLightEngine(engineLogger, null, ruleStore);

            // 4. Start engine - this automatically loads rules from storage
            Console.WriteLine("\nStarting engine...");
            await engine.StartAsync();

            // 5. Check engine status
            var status = engine.GetEngineStatus();
            Console.WriteLine($"\nEngine Status:");
            Console.WriteLine($"  Total Rules: {status.RuleCount}");
            Console.WriteLine($"  ✓ All rules successfully restored from storage!");

            // 6. Execute one of the restored rules
            var firstRule = storedRules[0];
            Console.WriteLine($"\nExecuting restored rule: {firstRule.Name}");
            var result = await engine.ExecuteRuleAsync(firstRule.Id);
            Console.WriteLine($"Execution result: {(result ? "Success" : "Failed")}");

            // 7. Demonstrate updating a rule
            Console.WriteLine("\nUpdating rule in storage...");
            firstRule.IsEnabled = false;
            await ruleStore.UpsertRuleAsync(firstRule);
            Console.WriteLine($"  ✓ Disabled rule: {firstRule.Name}");

            // 8. Demonstrate deleting a rule
            if (storedRules.Count > 2)
            {
                var ruleToDelete = storedRules[2];
                Console.WriteLine($"\nDeleting rule from storage: {ruleToDelete.Name}");
                await ruleStore.DeleteRuleAsync(ruleToDelete.Id);

                var remaining = await ruleStore.GetRulesAsync();
                Console.WriteLine($"  ✓ Remaining rules: {remaining.Count}");
            }

            // 9. Stop engine
            await engine.StopAsync();
            Console.WriteLine("\nEngine stopped. Changes saved to disk.");
        }
    }
}
```

## Step-by-Step Explanation

### 1. Setup Rule Storage
```csharp
var rulesFilePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "Loco",
    "rules.json"
);
var ruleStore = new JsonFileRuleStore(rulesFilePath, storeLogger);
```
- Choose a file location for storing rules
- `JsonFileRuleStore` handles all file I/O
- Directory is created automatically if it doesn't exist

### 2. Create Engine with Rule Store
```csharp
using var engine = new SimpleLightEngine(engineLogger, null, ruleStore);
```
- Pass the rule store as the third parameter
- Engine will automatically load/save rules

### 3. Create Rules
```csharp
var ruleId = engine.CreateRule(name, trigger, actions);
```
- Rules are automatically persisted when created
- Persistence happens asynchronously
- No manual save needed

### 4. Automatic Loading on Start
```csharp
await engine.StartAsync();
```
- Engine automatically loads rules from storage
- All previously created rules are restored
- Ready to use immediately

### 5. Manual Rule Operations
```csharp
// Get all rules
var rules = await ruleStore.GetRulesAsync();

// Get specific rule
var rule = await ruleStore.GetRuleAsync(ruleId);

// Update rule
rule.IsEnabled = false;
await ruleStore.UpsertRuleAsync(rule);

// Delete rule
await ruleStore.DeleteRuleAsync(ruleId);

// Check if rule exists
var exists = await ruleStore.RuleExistsAsync(ruleId);
```

## Storage Format

Rules are stored in JSON format:

```json
[
  {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "name": "Morning Backup",
    "trigger": {
      "type": "scheduled",
      "schedule": "daily-8am"
    },
    "actions": [
      {
        "type": "log",
        "parameters": {
          "message": "Running morning backup..."
        }
      }
    ],
    "isEnabled": true,
    "createdUtc": "2025-01-16T10:30:00.000Z",
    "lastUpdatedUtc": "2025-01-16T10:30:00.000Z"
  }
]
```

## Advanced Usage

### Custom Storage Location
```csharp
// Use application directory
var appDir = AppDomain.CurrentDomain.BaseDirectory;
var rulesPath = Path.Combine(appDir, "data", "rules.json");

// Use user documents
var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
var rulesPath = Path.Combine(docs, "MyApp", "rules.json");

// Use temporary directory (for testing)
var tempPath = Path.Combine(Path.GetTempPath(), "loco-rules.json");
```

### Multiple Rule Stores
```csharp
// Separate stores for different rule types
var systemRules = new JsonFileRuleStore("system-rules.json");
var userRules = new JsonFileRuleStore("user-rules.json");

// Use different stores for different engines
var systemEngine = new SimpleLightEngine(logger, config, systemRules);
var userEngine = new SimpleLightEngine(logger, config, userRules);
```

### Bulk Operations
```csharp
// Get only enabled rules
var enabledRules = await ruleStore.GetEnabledRulesAsync();

// Clear all rules
await ruleStore.ClearRulesAsync();

// Batch update
foreach (var rule in await ruleStore.GetRulesAsync())
{
    rule.IsEnabled = true;
    await ruleStore.UpsertRuleAsync(rule);
}
```

## Running the Example

```bash
dotnet new console -n LocoRulePersistence
cd LocoRulePersistence
dotnet add reference path/to/Loco.Core/Loco.Core.csproj
# Copy code to Program.cs
dotnet run
```

## Expected Output

```
Rules will be stored in: C:\Users\YourName\AppData\Roaming\Loco\rules.json

=== FIRST RUN: Creating rules ===

info: Loco.Core.Storage.JsonFileRuleStore[0]
      Initialized new rule store: C:\Users\...\rules.json
Creating rules...
info: Loco.Core.Storage.JsonFileRuleStore[0]
      Added new rule: a1b2c3d4-... - Morning Backup
  ✓ Created: Morning Backup (ID: a1b2c3d4-e5f6-7890-abcd-ef1234567890)
info: Loco.Core.Storage.JsonFileRuleStore[0]
      Added new rule: b2c3d4e5-... - Hourly Health Check
  ✓ Created: Hourly Health Check (ID: b2c3d4e5-f678-9012-bcde-f12345678901)
info: Loco.Core.Storage.JsonFileRuleStore[0]
      Added new rule: c3d4e5f6-... - Evening Cleanup
  ✓ Created: Evening Cleanup (ID: c3d4e5f6-7890-1234-cdef-123456789012)

Engine Status:
  Total Rules: 3

Persisted to storage: 3 rules
  - Morning Backup (Enabled: True)
  - Hourly Health Check (Enabled: True)
  - Evening Cleanup (Enabled: True)

Engine stopped. Rules saved to disk.

============================================================
Press any key to simulate engine restart...
============================================================

=== SECOND RUN: Loading persisted rules ===

Found 3 rules in storage:
  - Morning Backup (ID: a1b2c3d4-e5f6-7890-abcd-ef1234567890)
  - Hourly Health Check (ID: b2c3d4e5-f678-9012-bcde-f12345678901)
  - Evening Cleanup (ID: c3d4e5f6-7890-1234-cdef-123456789012)

Starting engine...
info: Loco.Core.SimpleLightEngine[0]
      Loaded 3 rules from persistent storage

Engine Status:
  Total Rules: 3
  ✓ All rules successfully restored from storage!

Executing restored rule: Morning Backup
info: Loco.Core.SimpleLightEngine[0]
      Executing rule: Morning Backup
info: Loco.Core.SimpleLightEngine[0]
      LogAction: Running morning backup...
Execution result: Success

Updating rule in storage...
info: Loco.Core.Storage.JsonFileRuleStore[0]
      Updated rule: a1b2c3d4-... - Morning Backup
  ✓ Disabled rule: Morning Backup

Deleting rule from storage: Evening Cleanup
info: Loco.Core.Storage.JsonFileRuleStore[0]
      Deleted rule: c3d4e5f6-...
  ✓ Remaining rules: 2

Engine stopped. Changes saved to disk.

Example completed!
```

## Use Cases

### 1. Long-Running Services
```csharp
// Service that runs 24/7, rules persist across restarts
var ruleStore = new JsonFileRuleStore("/etc/myservice/rules.json");
using var engine = new SimpleLightEngine(logger, config, ruleStore);
await engine.StartAsync();
// Rules automatically loaded
await RunServiceAsync();
```

### 2. User-Defined Automation
```csharp
// Users can create rules via UI, persist automatically
public async Task CreateUserRule(string name, LightTrigger trigger, LightAction[] actions)
{
    var ruleId = _engine.CreateRule(name, trigger, actions);
    // Automatically persisted via ruleStore
    return ruleId;
}
```

### 3. Rule Templates
```csharp
// Load predefined rules from storage
var templateStore = new JsonFileRuleStore("templates/rules.json");
var templates = await templateStore.GetRulesAsync();

foreach (var template in templates)
{
    await userRuleStore.UpsertRuleAsync(template);
}
```

## Common Issues and Solutions

### Issue: Rules not persisting
**Cause**: Not enough time for async save

**Solution**: Add delay before shutdown:
```csharp
engine.CreateRule(...);
await Task.Delay(100); // Wait for persistence
await engine.StopAsync();
```

### Issue: File access denied
**Cause**: Insufficient permissions

**Solution**: Use user-accessible directory:
```csharp
var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var rulesPath = Path.Combine(appData, "MyApp", "rules.json");
```

### Issue: Duplicate rules after restart
**Cause**: Creating rules without checking existing

**Solution**: Check before creating:
```csharp
if (!await ruleStore.RuleExistsAsync(ruleId))
{
    engine.CreateRule(...);
}
```

## Next Steps

- **Example 4**: Learn about process execution
- **Example 5**: Advanced configuration options
- **Example 2**: Review scheduled automation

## Related Documentation

- [IRuleStore Interface](../docs/API.md#irulestore)
- [JsonFileRuleStore](../docs/API.md#jsonfilerulestore)
- [SimpleLightEngine Persistence](../docs/API.md#simplelightengine-persistence)

---

**Example Type**: Intermediate
**Complexity**: ⭐⭐⭐☆☆
**Estimated Time**: 15 minutes
**Last Updated**: 2025-01-16
