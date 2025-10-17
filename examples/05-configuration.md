# Example 5: Configuration & Validation

Configure the automation engine with custom settings and validation.

## Simple Example

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Loco.Core;
using Loco.Core.Configuration;
using Loco.Core.Validation;

class ConfigurationExample
{
    static async Task Main()
    {
        // Create custom configuration
        var config = new LocoConfig
        {
            MaxConcurrentFlows = 10,
            DefaultTimeoutSeconds = 60,
            DefaultRetryCount = 3,
            LogLevel = "Debug",
            EnableFileLogging = true,
            LogDirectory = Path.Combine(Environment.CurrentDirectory, "logs")
        };

        // Validate configuration
        var validationResult = ConfigValidator.Validate(config);
        if (!validationResult.IsValid)
        {
            Console.WriteLine("Configuration errors:");
            foreach (var error in validationResult.Errors)
            {
                Console.WriteLine($"  ✗ {error}");
            }
            return;
        }

        if (validationResult.Warnings.Count > 0)
        {
            Console.WriteLine("Configuration warnings:");
            foreach (var warning in validationResult.Warnings)
            {
                Console.WriteLine($"  ⚠ {warning}");
            }
        }

        // Use configuration
        using var engine = new SimpleLightEngine(null, config);
        await engine.StartAsync();

        Console.WriteLine($"Engine started with:");
        Console.WriteLine($"  Max Concurrent Flows: {config.MaxConcurrentFlows}");
        Console.WriteLine($"  Timeout: {config.DefaultTimeoutSeconds}s");
        Console.WriteLine($"  Log Level: {config.LogLevel}");

        await engine.StopAsync();
    }
}
```

## Key Configuration Options

- `MaxConcurrentFlows`: Parallel execution limit
- `DefaultTimeoutSeconds`: Action timeout
- `DefaultRetryCount`: Retry attempts
- `LogLevel`: Debug, Information, Warning, Error
- `EnableFileLogging`: Log to files
- `LogDirectory`: Log file location

---

**Complexity**: ⭐⭐☆☆☆
**Last Updated**: 2025-01-16
