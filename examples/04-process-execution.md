# Example 4: Process Execution

Execute external processes and commands with automation rules.

## Simple Example

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loco.Core;
using Loco.Core.Models;

class ProcessExecution
{
    static async Task Main()
    {
        using var engine = new SimpleLightEngine();
        await engine.StartAsync();

        // Execute a command
        var ruleId = engine.CreateRule(
            name: "Run Command",
            trigger: new LightTrigger { Type = "manual" },
            actions: new[]
            {
                new LightAction
                {
                    Type = "process",
                    Parameters = new Dictionary<string, object>
                    {
                        ["command"] = "ping",
                        ["arguments"] = "localhost -n 4"
                    }
                }
            }
        );

        await engine.ExecuteRuleAsync(ruleId);
        await engine.StopAsync();
    }
}
```

## Use Cases

- Running system commands
- Executing scripts (PowerShell, batch, bash)
- Starting applications
- System maintenance tasks

---

**Complexity**: ⭐⭐☆☆☆
**Last Updated**: 2025-01-16
