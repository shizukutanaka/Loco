# Example 2: Scheduled Task Automation

This example demonstrates how to schedule automation rules to run at specific times or intervals.

## Overview

This example shows:
- Creating automation rules
- Scheduling rules to run periodically
- Scheduling one-time executions
- Managing scheduled tasks

## Code Example

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core;
using Loco.Core.Models;
using Loco.Core.Configuration;

namespace LocoExamples
{
    class ScheduledAutomation
    {
        static async Task Main(string[] args)
        {
            // 1. Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            var logger = loggerFactory.CreateLogger<SimpleLightEngine>();

            // 2. Create and start the engine
            using var engine = new SimpleLightEngine(logger);
            await engine.StartAsync();
            Console.WriteLine("Automation engine started!\n");

            // 3. Create a rule with a log action
            var ruleId = engine.CreateRule(
                name: "Periodic Status Check",
                trigger: new LightTrigger { Type = "manual" },
                actions: new[]
                {
                    new LightAction
                    {
                        Type = "log",
                        Parameters = new Dictionary<string, object>
                        {
                            ["message"] = $"Status check at {DateTime.Now:HH:mm:ss}"
                        }
                    }
                }
            );

            Console.WriteLine($"Created rule: {ruleId}\n");

            // 4. Schedule the rule to run every 5 seconds
            Console.WriteLine("Scheduling rule to run every 5 seconds...");
            engine.ScheduleRule(ruleId, TimeSpan.FromSeconds(5));

            // 5. Create another rule for one-time execution
            var onceRuleId = engine.CreateRule(
                name: "One-Time Notification",
                trigger: new LightTrigger { Type = "manual" },
                actions: new[]
                {
                    new LightAction
                    {
                        Type = "log",
                        Parameters = new Dictionary<string, object>
                        {
                            ["message"] = "This is a one-time notification!"
                        }
                    }
                }
            );

            // 6. Schedule it to run once in 10 seconds
            var runTime = DateTime.Now.AddSeconds(10);
            Console.WriteLine($"Scheduling one-time rule to run at {runTime:HH:mm:ss}...\n");
            engine.ScheduleRuleOnce(onceRuleId, runTime);

            // 7. Let it run for 30 seconds
            Console.WriteLine("Running for 30 seconds... Press Ctrl+C to stop early.");
            await Task.Delay(TimeSpan.FromSeconds(30));

            // 8. Cancel the periodic schedule
            Console.WriteLine("\nCancelling periodic schedule...");
            var cancelled = engine.CancelScheduledRule(ruleId);
            Console.WriteLine($"Schedule cancelled: {cancelled}");

            // 9. Check status
            var status = engine.GetEngineStatus();
            Console.WriteLine($"\nFinal Status:");
            Console.WriteLine($"  Total Rules: {status.RuleCount}");
            Console.WriteLine($"  Total Executions: {status.TotalExecutions}");
            Console.WriteLine($"  Successful: {status.SuccessfulExecutions}");

            // 10. Stop the engine
            await engine.StopAsync();
            Console.WriteLine("\nAutomation engine stopped.");
        }
    }
}
```

## Step-by-Step Explanation

### 1-2. Setup and Start Engine
```csharp
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
var logger = loggerFactory.CreateLogger<SimpleLightEngine>();

using var engine = new SimpleLightEngine(logger);
await engine.StartAsync();
```
- Standard engine initialization
- Logging shows scheduled executions

### 3. Create a Rule
```csharp
var ruleId = engine.CreateRule(
    name: "Periodic Status Check",
    trigger: new LightTrigger { Type = "manual" },
    actions: new[] { ... }
);
```
- Rules define what actions to perform
- "manual" trigger means we control when it runs
- Returns a unique rule ID for scheduling

### 4. Schedule Periodic Execution
```csharp
engine.ScheduleRule(ruleId, TimeSpan.FromSeconds(5));
```
- Runs the rule every 5 seconds
- Continues until cancelled or engine stops
- Time intervals can be seconds, minutes, hours, etc.

### 5-6. Schedule One-Time Execution
```csharp
var runTime = DateTime.Now.AddSeconds(10);
engine.ScheduleRuleOnce(onceRuleId, runTime);
```
- Runs exactly once at the specified time
- Useful for delayed actions or reminders
- Automatically removed after execution

### 7. Wait for Executions
```csharp
await Task.Delay(TimeSpan.FromSeconds(30));
```
- Keeps the program running while schedules execute
- In a real application, this might be a long-running service

### 8. Cancel a Schedule
```csharp
var cancelled = engine.CancelScheduledRule(ruleId);
```
- Stops the periodic execution
- Returns true if successfully cancelled
- One-time schedules are auto-cancelled after execution

### 9-10. Cleanup
```csharp
var status = engine.GetEngineStatus();
await engine.StopAsync();
```
- Check execution statistics
- Clean shutdown stops all schedules

## Advanced Scheduling Examples

### Multiple Actions in a Rule
```csharp
var ruleId = engine.CreateRule(
    name: "Multi-Action Rule",
    trigger: new LightTrigger { Type = "manual" },
    actions: new[]
    {
        new LightAction
        {
            Type = "log",
            Parameters = new Dictionary<string, object>
            {
                ["message"] = "Step 1: Starting backup"
            }
        },
        new LightAction
        {
            Type = "file_copy",
            Parameters = new Dictionary<string, object>
            {
                ["source"] = @"C:\data\file.txt",
                ["destination"] = @"C:\backup\file.txt"
            }
        },
        new LightAction
        {
            Type = "log",
            Parameters = new Dictionary<string, object>
            {
                ["message"] = "Step 2: Backup complete"
            }
        }
    }
);

// Schedule to run daily at 2 AM
var tomorrow2AM = DateTime.Today.AddDays(1).AddHours(2);
engine.ScheduleRuleOnce(ruleId, tomorrow2AM);
```

### Different Time Intervals
```csharp
// Every minute
engine.ScheduleRule(ruleId, TimeSpan.FromMinutes(1));

// Every hour
engine.ScheduleRule(ruleId, TimeSpan.FromHours(1));

// Every day
engine.ScheduleRule(ruleId, TimeSpan.FromDays(1));

// Custom: Every 2.5 hours
engine.ScheduleRule(ruleId, TimeSpan.FromHours(2.5));

// Very frequent: Every 500 milliseconds
engine.ScheduleRule(ruleId, TimeSpan.FromMilliseconds(500));
```

### Scheduling Multiple Rules
```csharp
// Morning report at 8 AM
var morningRule = engine.CreateRule("Morning Report", ...);
var tomorrow8AM = DateTime.Today.AddDays(1).AddHours(8);
engine.ScheduleRuleOnce(morningRule, tomorrow8AM);

// Hourly status check
var statusRule = engine.CreateRule("Hourly Status", ...);
engine.ScheduleRule(statusRule, TimeSpan.FromHours(1));

// Cleanup every 6 hours
var cleanupRule = engine.CreateRule("Cleanup Task", ...);
engine.ScheduleRule(cleanupRule, TimeSpan.FromHours(6));
```

## Running the Example

### Compile and Run
```bash
dotnet new console -n LocoScheduledAutomation
cd LocoScheduledAutomation
dotnet add reference path/to/Loco.Core/Loco.Core.csproj
# Copy code to Program.cs
dotnet run
```

## Expected Output

```
Automation engine started!

Created rule: a1b2c3d4-e5f6-7890-abcd-ef1234567890

Scheduling rule to run every 5 seconds...
Scheduling one-time rule to run at 14:25:45...

Running for 30 seconds... Press Ctrl+C to stop early.
info: Loco.Core.SimpleLightEngine[0]
      Executing rule: Periodic Status Check
info: Loco.Core.SimpleLightEngine[0]
      LogAction: Status check at 14:25:40
info: Loco.Core.SimpleLightEngine[0]
      Executing rule: Periodic Status Check
info: Loco.Core.SimpleLightEngine[0]
      LogAction: Status check at 14:25:45
info: Loco.Core.SimpleLightEngine[0]
      Executing rule: One-Time Notification
info: Loco.Core.SimpleLightEngine[0]
      LogAction: This is a one-time notification!
info: Loco.Core.SimpleLightEngine[0]
      Executing rule: Periodic Status Check
info: Loco.Core.SimpleLightEngine[0]
      LogAction: Status check at 14:25:50
... (continues every 5 seconds) ...

Cancelling periodic schedule...
Schedule cancelled: True

Final Status:
  Total Rules: 2
  Total Executions: 7
  Successful: 7

Automation engine stopped.
```

## Use Cases

### 1. Regular Backups
```csharp
var backupRule = engine.CreateRule("Daily Backup", ...);
// Run every day at 2 AM
var nextRun = DateTime.Today.AddDays(1).AddHours(2);
engine.ScheduleRuleOnce(backupRule, nextRun);
```

### 2. Health Checks
```csharp
var healthCheck = engine.CreateRule("Health Check", ...);
// Check every 5 minutes
engine.ScheduleRule(healthCheck, TimeSpan.FromMinutes(5));
```

### 3. Reminder System
```csharp
var reminder = engine.CreateRule("Meeting Reminder", ...);
// Remind 15 minutes before meeting
var meetingTime = DateTime.Today.AddHours(14); // 2 PM
var reminderTime = meetingTime.AddMinutes(-15);
engine.ScheduleRuleOnce(reminder, reminderTime);
```

### 4. Log Rotation
```csharp
var rotateRule = engine.CreateRule("Rotate Logs", ...);
// Rotate logs daily at midnight
var midnight = DateTime.Today.AddDays(1);
engine.ScheduleRuleOnce(rotateRule, midnight);
```

## Common Issues and Solutions

### Issue: Schedule not running
**Cause**: Engine stopped or schedule cancelled

**Solution**: Ensure engine stays running:
```csharp
// Keep running indefinitely
await Task.Delay(Timeout.Infinite);

// Or use a cancellation token
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
await Task.Delay(Timeout.Infinite, cts.Token);
```

### Issue: High CPU usage with frequent schedules
**Cause**: Very short intervals (< 1 second)

**Solution**:
- Use reasonable intervals (≥ 1 second)
- For sub-second timing, consider alternative approaches
- Monitor engine status for execution counts

### Issue: One-time schedule doesn't run
**Cause**: Scheduled time is in the past

**Solution**: Always schedule for future times:
```csharp
var runTime = DateTime.Now.AddSeconds(10); // Future time
if (runTime > DateTime.Now)
{
    engine.ScheduleRuleOnce(ruleId, runTime);
}
```

## Next Steps

- **Example 3**: Learn about rule persistence across restarts
- **Example 4**: Execute processes and commands
- **Example 1**: Review basic file automation

## Related Documentation

- [SimpleLightEngine.ScheduleRule](../docs/API.md#schedulerule)
- [SimpleLightEngine.ScheduleRuleOnce](../docs/API.md#scheduleruleonce)
- [SimpleLightEngine.CancelScheduledRule](../docs/API.md#cancelscheduledrule)

---

**Example Type**: Intermediate
**Complexity**: ⭐⭐☆☆☆
**Estimated Time**: 10 minutes
**Last Updated**: 2025-01-16
