using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Loco.Core.Configuration;
using Loco.Core.Security;

namespace Loco.Cli;

/// <summary>
/// Interactive setup wizard for first-time users
/// Guides users through configuration based on their needs
/// </summary>
public class SetupWizard
{
    private const string ConfigDirectory = "config";
    private const string ConfigFileName = "loco.config.json";

    /// <summary>
    /// Run the setup wizard
    /// </summary>
    public async Task<int> RunAsync()
    {
        ShowWelcome();

        try
        {
            // Step 1: Detect existing configuration
            if (HasExistingConfiguration())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n⚠ Existing configuration detected!");
                Console.ResetColor();
                Console.WriteLine("Would you like to:");
                Console.WriteLine("  1. Keep existing configuration");
                Console.WriteLine("  2. Reconfigure (backup will be created)");
                Console.Write("\nChoice (1-2): ");

                var choice = Console.ReadLine();
                if (choice == "1")
                {
                    Console.WriteLine("\nKeeping existing configuration.");
                    return 0;
                }
            }

            // Step 2: Choose use case
            var preset = ChoosePreset();

            // Step 3: Customize basic settings
            var config = CustomizeConfiguration(preset);

            // Step 4: Review and confirm
            if (!ReviewConfiguration(config))
            {
                Console.WriteLine("\nSetup cancelled.");
                return 1;
            }

            // Step 5: Save configuration
            await SaveConfigurationAsync(config);

            // Step 6: Create directories
            CreateDirectories(config);

            // Step 7: Show next steps
            ShowNextSteps(preset);

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n✗ Setup failed: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private void ShowWelcome()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                  Loco Setup Wizard                            ║");
        Console.WriteLine("║          Welcome to Loco Automation Platform!                ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("This wizard will help you configure Loco for your needs.");
        Console.WriteLine("You can always reconfigure later by running 'setup' again.");
        Console.WriteLine();
    }

    private string ChoosePreset()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Step 1: Choose Your Use Case");
        Console.WriteLine("════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();

        var presets = GetPresets();
        var recommended = "standard";

        Console.WriteLine($"System detected: {Environment.ProcessorCount} cores, " +
            $"{GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024 / 1024}GB RAM");
        Console.WriteLine();

        for (int i = 0; i < presets.Count; i++)
        {
            var p = presets[i];
            var isRecommended = p.Name == recommended;

            Console.ForegroundColor = isRecommended ? ConsoleColor.Green : ConsoleColor.White;
            Console.Write($"  {i + 1}. {p.DisplayName}");
            if (isRecommended)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(" [RECOMMENDED]");
            }
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"     {p.Description}");
            Console.WriteLine($"     Use case: {p.UseCase}");
            Console.WriteLine($"     Resources: {p.MemoryLimitMB}MB RAM, {p.MaxConcurrentFlows} concurrent flows");
            Console.ResetColor();
            Console.WriteLine();
        }

        while (true)
        {
            Console.Write($"Choose preset (1-{presets.Count}): ");
            var input = Console.ReadLine();

            if (int.TryParse(input, out var choice) && choice >= 1 && choice <= presets.Count)
            {
                return presets[choice - 1].Name;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Invalid choice. Please enter 1-{presets.Count}.");
            Console.ResetColor();
        }
    }

    private LocoConfig CustomizeConfiguration(string presetName)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Step 2: Customize Settings (Optional)");
        Console.WriteLine("══════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();

        var config = GetPresetConfig(presetName);

        Console.WriteLine("Press Enter to keep default, or enter new value:");
        Console.WriteLine();

        config.WorkingDirectory = PromptDirectory("Working directory", config.WorkingDirectory);
        config.LogDirectory = PromptDirectory("Log directory", config.LogDirectory);

        config.LogRetentionDays = PromptInt("Log retention (days)", config.LogRetentionDays, 1, 365);

        // Advanced settings
        Console.WriteLine();
        Console.Write("Configure advanced settings? (y/N): ");
        var advanced = Console.ReadLine()?.ToLower();

        if (advanced == "y" || advanced == "yes")
        {
            ConfigureAdvancedSettings(config);
        }

        return config;
    }

    private static string PromptDirectory(string label, string defaultPath)
    {
        while (true)
        {
            Console.Write($"{label} [{defaultPath}]: ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultPath;
            }

            try
            {
                var fullPath = Path.GetFullPath(input);

                if (!SecurityUtilities.IsPathSafe(fullPath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Specified path is not allowed. Choose a different location.");
                    Console.ResetColor();
                    continue;
                }

                return fullPath;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Invalid path: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    private static int PromptInt(string label, int currentValue, int min, int max)
    {
        while (true)
        {
            Console.Write($"{label} [{currentValue}] ({min}-{max}): ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                return currentValue;
            }

            if (int.TryParse(input, out var value) && value >= min && value <= max)
            {
                return value;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Enter a value between {min} and {max}.");
            Console.ResetColor();
        }
    }

    private static bool PromptBoolean(string label, bool currentValue)
    {
        while (true)
        {
            Console.Write($"{label}? (y/N) [{(currentValue ? "Y" : "N")}]: ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                return currentValue;
            }

            input = input.Trim().ToLowerInvariant();
            if (input is "y" or "yes")
            {
                return true;
            }

            if (input is "n" or "no")
            {
                return false;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Enter 'y' or 'n'.");
            Console.ResetColor();
        }
    }

    private static string PromptLogLevel(string currentLevel)
    {
        var validLogLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };

        while (true)
        {
            Console.Write($"Log level [{currentLevel}] (Trace, Debug, Information, Warning, Error, Critical): ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                return currentLevel;
            }

            if (validLogLevels.Contains(input, StringComparer.OrdinalIgnoreCase))
            {
                return validLogLevels.First(level => level.Equals(input, StringComparison.OrdinalIgnoreCase));
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Invalid log level. Choose from the list provided.");
            Console.ResetColor();
        }
    }

    private static string[] PromptPathCollection(string prompt, IEnumerable<string> existingPaths)
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in existingPaths ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                var normalized = NormalizePath(path);
                if (seen.Add(normalized))
                {
                    paths.Add(path);
                }
            }
        }

        if (paths.Count > 0)
        {
            Console.WriteLine($"Current entries ({paths.Count}):");
            foreach (var path in paths)
            {
                Console.WriteLine($"  - {path}");
            }
        }

        while (paths.Count < 32)
        {
            Console.Write($"{prompt} (leave blank to finish): ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                break;
            }

            try
            {
                var fullPath = Path.GetFullPath(input);

                if (!SecurityUtilities.IsPathSafe(fullPath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Specified path is not permitted. Provide a different location.");
                    Console.ResetColor();
                    continue;
                }

                var normalized = NormalizePath(fullPath);
                if (seen.Add(normalized))
                {
                    paths.Add(fullPath);
                    Console.WriteLine("Path added.");
                }
                else
                {
                    Console.WriteLine("Path already present – skipping duplicate.");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Invalid path: {ex.Message}");
                Console.ResetColor();
            }
        }

        return paths.ToArray();
    }

    private static void EnsureDirectory(string? path, string description)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!SecurityUtilities.IsPathSafe(path))
        {
            throw new InvalidOperationException($"Configured {description} '{path}' is not allowed.");
        }

        Directory.CreateDirectory(path);
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private void ConfigureAdvancedSettings(LocoConfig config)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Advanced Settings:");
        Console.ResetColor();

        config.MaxConcurrentFlows = PromptInt("Max concurrent flows", config.MaxConcurrentFlows, 1, 1000);
        config.MemoryLimitMB = PromptInt("Memory limit (MB)", config.MemoryLimitMB, 64, 8192);
        config.CacheSizeMB = PromptInt("Cache size (MB)", config.CacheSizeMB, 16, 1024);
        config.RateLimitPerMinute = PromptInt("Rate limit per minute", config.RateLimitPerMinute, 1, 10000);

        config.EnableAutoBackup = PromptBoolean("Enable auto backup", config.EnableAutoBackup);
        config.EnableAuditLogging = PromptBoolean("Enable audit logging", config.EnableAuditLogging);
        config.EnableInputValidation = PromptBoolean("Enable input validation", config.EnableInputValidation);
        config.EnableHealthChecks = PromptBoolean("Enable health checks", config.EnableHealthChecks);
        if (config.EnableHealthChecks)
        {
            config.HealthCheckIntervalSeconds = PromptInt("Health check interval (seconds)", config.HealthCheckIntervalSeconds, 10, 3600);
        }

        config.EnableMetrics = PromptBoolean("Enable metrics", config.EnableMetrics);
        config.EnableCircuitBreaker = PromptBoolean("Enable circuit breaker", config.EnableCircuitBreaker);
        if (config.EnableCircuitBreaker)
        {
            config.CircuitBreakerThreshold = PromptInt("Circuit breaker threshold", config.CircuitBreakerThreshold, 1, 100);
            config.CircuitBreakerTimeoutSeconds = PromptInt("Circuit breaker timeout (seconds)", config.CircuitBreakerTimeoutSeconds, 10, 3600);
        }

        config.LogLevel = PromptLogLevel(config.LogLevel);

        config.AllowedPaths = PromptPathCollection("Add allowed path", config.AllowedPaths);
        config.ForbiddenPaths = PromptPathCollection("Add forbidden path", config.ForbiddenPaths);
    }

    private bool ReviewConfiguration(LocoConfig config)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Step 3: Review Configuration");
        Console.WriteLine("═════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();

        Console.WriteLine("Your configuration:");
        Console.WriteLine($"  Working Directory:     {config.WorkingDirectory}");
        Console.WriteLine($"  Log Directory:         {config.LogDirectory}");
        Console.WriteLine($"  Log Retention:         {config.LogRetentionDays} days");
        Console.WriteLine($"  Max Concurrent Flows:  {config.MaxConcurrentFlows}");
        Console.WriteLine($"  Memory Limit:          {config.MemoryLimitMB} MB");
        Console.WriteLine($"  Auto Backup:           {(config.EnableAutoBackup ? "Enabled" : "Disabled")}");
        Console.WriteLine($"  Audit Logging:         {(config.EnableAuditLogging ? "Enabled" : "Disabled")}");
        Console.WriteLine($"  Health Checks:         {(config.EnableHealthChecks ? "Enabled" : "Disabled")}");
        Console.WriteLine($"  Log Level:             {config.LogLevel}");
        Console.WriteLine();

        Console.Write("Save this configuration? (Y/n): ");
        var confirm = Console.ReadLine()?.ToLower();

        return confirm != "n" && confirm != "no";
    }

    private async Task SaveConfigurationAsync(LocoConfig config)
    {
        Console.WriteLine();
        Console.Write("Saving configuration... ");

        // Create backup if exists
        var configPath = Path.Combine(ConfigDirectory, ConfigFileName);
        if (File.Exists(configPath))
        {
            var backupPath = $"{configPath}.backup.{DateTime.Now:yyyyMMddHHmmss}";
            File.Copy(configPath, backupPath);
            Console.WriteLine($"\n  (Backup created: {backupPath})");
        }

        // Create directory
        Directory.CreateDirectory(ConfigDirectory);

        // Save configuration
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(configPath, json);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Done");
        Console.ResetColor();
    }

    private void CreateDirectories(LocoConfig config)
    {
        Console.Write("Creating directories... ");

        try
        {
            EnsureDirectory(config.WorkingDirectory, "working directory");
            EnsureDirectory(config.LogDirectory, "log directory");
            EnsureDirectory(config.CacheDirectory, "cache directory");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Done");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠ Warning: {ex.Message}");
            Console.ResetColor();
        }
    }

    private void ShowNextSteps(string preset)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Setup Complete!");
        Console.WriteLine("═════════════════");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("1. Test your installation:");
        Console.ResetColor();
        Console.WriteLine("   Loco.Cli.exe health");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("2. View system information:");
        Console.ResetColor();
        Console.WriteLine("   Loco.Cli.exe info");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("3. Try a preset workflow:");
        Console.ResetColor();
        Console.WriteLine("   Loco.Cli.exe preset system");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("4. Explore interactive mode:");
        Console.ResetColor();
        Console.WriteLine("   Loco.Cli.exe interactive");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("5. Read the documentation:");
        Console.ResetColor();
        Console.WriteLine("   docs/USER_MANUAL.md");
        Console.WriteLine("   docs/GETTING_STARTED.md");
        Console.WriteLine();

        if (preset == "personal")
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Tip: Start with simple file automation or scheduled tasks.");
            Console.ResetColor();
        }
        else if (preset == "enterprise")
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Tip: Review security settings and audit logs before production rollout.");
            Console.ResetColor();
        }
        Console.WriteLine();
    }

    private bool HasExistingConfiguration()
    {
        var configPath = Path.Combine(ConfigDirectory, ConfigFileName);
        return File.Exists(configPath);
    }

    private List<PresetInfo> GetPresets()
    {
        return new List<PresetInfo>
        {
            new PresetInfo
            {
                Name = "minimal",
                DisplayName = "Minimal",
                Description = "Lightweight setup for basic automation",
                UseCase = "Simple file operations, basic workflows",
                MemoryLimitMB = 128,
                MaxConcurrentFlows = 2
            },
            new PresetInfo
            {
                Name = "standard",
                DisplayName = "Standard",
                Description = "Balanced performance for typical use",
                UseCase = "Daily automation, file management, scheduled tasks",
                MemoryLimitMB = 256,
                MaxConcurrentFlows = 5
            },
            new PresetInfo
            {
                Name = "performance",
                DisplayName = "Performance",
                Description = "High-throughput processing",
                UseCase = "Batch processing, data pipelines, heavy workloads",
                MemoryLimitMB = 512,
                MaxConcurrentFlows = 10
            }
        };
    }

    private LocoConfig GetPresetConfig(string presetName)
    {
        var config = new LocoConfig();

        switch (presetName)
        {
            case "minimal":
                config.MaxConcurrentFlows = 2;
                config.MemoryLimitMB = 128;
                config.CacheSizeMB = 16;
                config.EnableMemoryOptimization = true;
                break;
            case "standard":
                config.MaxConcurrentFlows = 5;
                config.MemoryLimitMB = 256;
                config.CacheSizeMB = 32;
                config.EnableMemoryOptimization = true;
                break;
            case "performance":
                config.MaxConcurrentFlows = 10;
                config.MemoryLimitMB = 512;
                config.CacheSizeMB = 64;
                config.EnableMemoryOptimization = false;
                break;
        }

        return config;
    }
}

internal class PresetInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string UseCase { get; set; } = "";
    public int MemoryLimitMB { get; set; }
    public int MaxConcurrentFlows { get; set; }
}
