using System.CommandLine;
using System.Diagnostics;

namespace Loco.Cli.Commands;

/// <summary>
/// System health check command
/// </summary>
public class HealthCommand : Command
{
    public HealthCommand() : base("health", "Check system health status")
    {
        var jsonOption = new Option<bool>("--json", "Output in JSON format");
        AddOption(jsonOption);

        this.SetHandler((json) => CheckHealth(json), jsonOption);
    }

    public async Task<int> InvokeAsync(string[] args)
    {
        return await ((Command)this).InvokeAsync(args);
    }

    private int CheckHealth(bool json)
    {
        if (!json)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("System Health Check");
            Console.WriteLine("════════════════════════════════════════════════════════");
            Console.ResetColor();
            Console.WriteLine();
        }

        var checks = new List<HealthCheck>();

        // Memory check
        var memoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
        checks.Add(new HealthCheck
        {
            Name = "Memory Usage",
            Status = memoryMB < 500 ? "Healthy" : memoryMB < 1000 ? "Warning" : "Critical",
            Value = $"{memoryMB} MB",
            Healthy = memoryMB < 1000
        });

        // CPU check
        var cpuCount = Environment.ProcessorCount;
        checks.Add(new HealthCheck
        {
            Name = "CPU Cores",
            Status = "Healthy",
            Value = cpuCount.ToString(),
            Healthy = true
        });

        // Disk space check
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:\\");
            var freeSpaceGB = drive.AvailableFreeSpace / 1024 / 1024 / 1024;
            checks.Add(new HealthCheck
            {
                Name = "Disk Space",
                Status = freeSpaceGB > 10 ? "Healthy" : freeSpaceGB > 5 ? "Warning" : "Critical",
                Value = $"{freeSpaceGB} GB free",
                Healthy = freeSpaceGB > 5
            });
        }
        catch
        {
            checks.Add(new HealthCheck
            {
                Name = "Disk Space",
                Status = "Unknown",
                Value = "Unable to check",
                Healthy = true
            });
        }

        // .NET Runtime check
        checks.Add(new HealthCheck
        {
            Name = ".NET Runtime",
            Status = "Healthy",
            Value = Environment.Version.ToString(),
            Healthy = true
        });

        // Working directory check
        var workingDirExists = Directory.Exists(Environment.CurrentDirectory);
        checks.Add(new HealthCheck
        {
            Name = "Working Directory",
            Status = workingDirExists ? "Healthy" : "Critical",
            Value = workingDirExists ? "Accessible" : "Not found",
            Healthy = workingDirExists
        });

        if (json)
        {
            Console.WriteLine("{");
            Console.WriteLine($"  \"overallStatus\": \"{(checks.All(c => c.Healthy) ? "Healthy" : "Unhealthy")}\",");
            Console.WriteLine($"  \"timestamp\": \"{DateTime.UtcNow:O}\",");
            Console.WriteLine("  \"checks\": [");
            for (int i = 0; i < checks.Count; i++)
            {
                var check = checks[i];
                Console.WriteLine("    {");
                Console.WriteLine($"      \"name\": \"{check.Name}\",");
                Console.WriteLine($"      \"status\": \"{check.Status}\",");
                Console.WriteLine($"      \"value\": \"{check.Value}\",");
                Console.WriteLine($"      \"healthy\": {check.Healthy.ToString().ToLower()}");
                Console.Write("    }");
                if (i < checks.Count - 1)
                    Console.WriteLine(",");
                else
                    Console.WriteLine();
            }
            Console.WriteLine("  ]");
            Console.WriteLine("}");
        }
        else
        {
            var allHealthy = checks.All(c => c.Healthy);
            Console.ForegroundColor = allHealthy ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.WriteLine($"Overall Status: {(allHealthy ? "✓ Healthy" : "⚠ Needs Attention")}");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Health Checks:");
            Console.ResetColor();

            foreach (var check in checks)
            {
                var statusColor = check.Status switch
                {
                    "Healthy" => ConsoleColor.Green,
                    "Warning" => ConsoleColor.Yellow,
                    "Critical" => ConsoleColor.Red,
                    _ => ConsoleColor.Gray
                };

                var icon = check.Status switch
                {
                    "Healthy" => "✓",
                    "Warning" => "⚠",
                    "Critical" => "✗",
                    _ => "?"
                };

                Console.ForegroundColor = statusColor;
                Console.Write($"  {icon} {check.Name,-20}");
                Console.ResetColor();
                Console.WriteLine($" {check.Value}");
            }

            Console.WriteLine();

            if (!allHealthy)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Recommendations:");
                Console.ResetColor();
                foreach (var check in checks.Where(c => !c.Healthy))
                {
                    Console.WriteLine($"  • Check {check.Name}: {check.Status}");
                }
                Console.WriteLine();
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Checked at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.ResetColor();
        }

        return checks.All(c => c.Healthy) ? 0 : 1;
    }

    private class HealthCheck
    {
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public string Value { get; set; } = "";
        public bool Healthy { get; set; }
    }
}
