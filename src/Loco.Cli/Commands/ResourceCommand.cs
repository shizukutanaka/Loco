using System;
using System.CommandLine;
using System.Threading.Tasks;
using Loco.Core.Scheduling;
using Microsoft.Extensions.Logging;

namespace Loco.Cli.Commands;

/// <summary>
/// Command for monitoring system resources
/// システムリソース監視コマンド
/// </summary>
public class ResourceCommand : Command
{
    public ResourceCommand() : base("resource", "Monitor system resources")
    {
        // Monitor subcommand (default)
        var monitorCommand = new Command("monitor", "Show current resource usage");
        var monitorContinuousOption = new Option<bool>(
            new[] { "--continuous", "-c" },
            "Monitor continuously (updates every second)");
        var monitorIntervalOption = new Option<int>(
            new[] { "--interval", "-i" },
            () => 1,
            "Update interval in seconds (for continuous mode)");
        monitorCommand.AddOption(monitorContinuousOption);
        monitorCommand.AddOption(monitorIntervalOption);
        monitorCommand.SetHandler(MonitorAsync, monitorContinuousOption, monitorIntervalOption);
        AddCommand(monitorCommand);

        // Stats subcommand
        var statsCommand = new Command("stats", "Show resource statistics");
        var statsDetailedOption = new Option<bool>(
            new[] { "--detailed", "-d" },
            "Show detailed statistics");
        statsCommand.AddOption(statsDetailedOption);
        statsCommand.SetHandler(StatsAsync, statsDetailedOption);
        AddCommand(statsCommand);

        // Check subcommand
        var checkCommand = new Command("check", "Check resource availability");
        var checkMemoryOption = new Option<int>(
            new[] { "--memory", "-m" },
            () => 0,
            "Required memory in MB");
        var checkCpuOption = new Option<int>(
            new[] { "--cpu" },
            () => 0,
            "Required CPU percentage");
        var checkDiskOption = new Option<int>(
            new[] { "--disk", "-d" },
            () => 0,
            "Required disk space in MB");
        checkCommand.AddOption(checkMemoryOption);
        checkCommand.AddOption(checkCpuOption);
        checkCommand.AddOption(checkDiskOption);
        checkCommand.SetHandler(CheckAsync, checkMemoryOption, checkCpuOption, checkDiskOption);
        AddCommand(checkCommand);
    }

    private async Task<int> MonitorAsync(bool continuous, int interval)
    {
        try
        {
            var monitor = new ResourceMonitor();

            if (continuous)
            {
                Console.WriteLine("Resource Monitor - Press Ctrl+C to stop");
                Console.WriteLine(new string('=', 60));
                Console.WriteLine();

                while (true)
                {
                    var utilization = await monitor.GetUtilizationAsync();

                    Console.SetCursorPosition(0, Console.CursorTop > 2 ? Console.CursorTop - 3 : 0);
                    Console.WriteLine($"CPU:    {utilization.CpuPercent,3}%");
                    Console.WriteLine($"Memory: {utilization.MemoryUsedMB,6} MB");
                    Console.WriteLine($"Disk:   {utilization.DiskUsedMB,6} MB");

                    await Task.Delay(interval * 1000);
                }
            }
            else
            {
                var utilization = await monitor.GetUtilizationAsync();

                Console.WriteLine("Current Resource Usage");
                Console.WriteLine(new string('=', 60));
                Console.WriteLine();
                Console.WriteLine($"CPU Usage:      {utilization.CpuPercent}%");
                Console.WriteLine($"Memory Used:    {utilization.MemoryUsedMB} MB");
                Console.WriteLine($"Disk Used:      {utilization.DiskUsedMB} MB");

                return 0;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private async Task<int> StatsAsync(bool detailed)
    {
        try
        {
            var monitor = new ResourceMonitor();
            var utilization = await monitor.GetUtilizationAsync();

            Console.WriteLine("Resource Statistics");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine();

            // Current usage
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Current Usage:");
            Console.ResetColor();
            Console.WriteLine($"  CPU:         {utilization.CpuPercent}%");
            Console.WriteLine($"  Memory:      {utilization.MemoryUsedMB} MB");
            Console.WriteLine($"  Disk:        {utilization.DiskUsedMB} MB");
            Console.WriteLine();

            if (detailed)
            {
                // System information
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("System Information:");
                Console.ResetColor();
                Console.WriteLine($"  Processor Count:  {Environment.ProcessorCount}");
                Console.WriteLine($"  OS Version:       {Environment.OSVersion}");
                Console.WriteLine($"  64-bit OS:        {Environment.Is64BitOperatingSystem}");
                Console.WriteLine($"  64-bit Process:   {Environment.Is64BitProcess}");
                Console.WriteLine($"  Working Set:      {Environment.WorkingSet / 1024 / 1024} MB");
                Console.WriteLine();

                // Memory info
                var memoryInfo = GC.GetGCMemoryInfo();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Memory Information:");
                Console.ResetColor();
                Console.WriteLine($"  Total Available:  {memoryInfo.TotalAvailableMemoryBytes / 1024 / 1024} MB");
                Console.WriteLine($"  Heap Size:        {memoryInfo.HeapSizeBytes / 1024 / 1024} MB");
                Console.WriteLine($"  Fragmented:       {memoryInfo.FragmentedBytes / 1024 / 1024} MB");
                Console.WriteLine($"  GC Generation:    {GC.MaxGeneration}");
                Console.WriteLine();

                // Disk info
                try
                {
                    var currentDir = System.IO.Directory.GetCurrentDirectory();
                    var driveInfo = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(currentDir) ?? "C:\\");

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Disk Information:");
                    Console.ResetColor();
                    Console.WriteLine($"  Drive:            {driveInfo.Name}");
                    Console.WriteLine($"  Type:             {driveInfo.DriveType}");
                    Console.WriteLine($"  Format:           {driveInfo.DriveFormat}");
                    Console.WriteLine($"  Total Size:       {driveInfo.TotalSize / 1024 / 1024 / 1024} GB");
                    Console.WriteLine($"  Available:        {driveInfo.AvailableFreeSpace / 1024 / 1024 / 1024} GB");
                    Console.WriteLine($"  Used:             {(driveInfo.TotalSize - driveInfo.AvailableFreeSpace) / 1024 / 1024 / 1024} GB");
                    var usedPercent = ((double)(driveInfo.TotalSize - driveInfo.AvailableFreeSpace) / driveInfo.TotalSize) * 100;
                    Console.WriteLine($"  Usage:            {usedPercent:F1}%");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  Warning: Could not get disk info - {ex.Message}");
                    Console.ResetColor();
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private async Task<int> CheckAsync(int memory, int cpu, int disk)
    {
        try
        {
            var monitor = new ResourceMonitor();

            // Create requirements
            var requirements = new ResourceRequirements
            {
                MemoryMB = memory,
                CpuPercent = cpu,
                DiskMB = disk
            };

            Console.WriteLine("Checking Resource Availability");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine();

            if (memory > 0 || cpu > 0 || disk > 0)
            {
                Console.WriteLine("Requirements:");
                if (memory > 0) Console.WriteLine($"  Memory:  {memory} MB");
                if (cpu > 0) Console.WriteLine($"  CPU:     {cpu}%");
                if (disk > 0) Console.WriteLine($"  Disk:    {disk} MB");
                Console.WriteLine();
            }

            // Check resources
            var result = await monitor.CheckResourcesAsync(requirements);

            if (result.Available)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Resources Available");
                Console.ResetColor();
                Console.WriteLine($"  Available Memory: {result.AvailableMemoryMB} MB");
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ Resources Insufficient");
                Console.ResetColor();
                Console.WriteLine($"  {result.Message}");

                // Estimate delay
                var delay = await monitor.EstimateResourceAvailabilityDelay(requirements);
                Console.WriteLine($"  Estimated availability: {delay.TotalSeconds:F0} seconds");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    public async Task<int> InvokeAsync(string[] args)
    {
        // If no subcommand specified, default to monitor
        if (args.Length == 0)
        {
            return await MonitorAsync(false, 1);
        }

        // Use System.CommandLine's InvokeAsync method
        return await ((Command)this).InvokeAsync(args);
    }
}
