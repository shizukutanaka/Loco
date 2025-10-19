using System.CommandLine;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Loco.Cli.Commands;

/// <summary>
/// Comprehensive diagnostics reporting
/// </summary>
public class DiagCommand : Command
{
    public DiagCommand() : base("diag", "Generate comprehensive diagnostics report")
    {
        var jsonOption = new Option<bool>("--json", "Output in JSON format");
        var verboseOption = new Option<bool>("--verbose", () => false, "Include detailed information");
        AddOption(jsonOption);
        AddOption(verboseOption);

        AddAlias("diagnostics");

        this.SetHandler((json, verbose) => GenerateDiagnostics(json, verbose), jsonOption, verboseOption);
    }

    public async Task<int> InvokeAsync(string[] args)
    {
        return await ((Command)this).InvokeAsync(args);
    }

    private int GenerateDiagnostics(bool json, bool verbose)
    {
        if (!json)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("System Diagnostics Report");
            Console.WriteLine("════════════════════════════════════════════════════════");
            Console.ResetColor();
            Console.WriteLine();
        }

        var diagnostics = CollectDiagnostics(verbose);

        if (json)
        {
            OutputJson(diagnostics);
        }
        else
        {
            OutputFormatted(diagnostics, verbose);
        }

        return 0;
    }

    private DiagnosticsData CollectDiagnostics(bool verbose)
    {
        var data = new DiagnosticsData
        {
            Timestamp = DateTime.UtcNow,
            Platform = new PlatformInfo
            {
                OS = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                Framework = RuntimeInformation.FrameworkDescription,
                Runtime = Environment.Version.ToString(),
                ProcessorCount = Environment.ProcessorCount
            },
            Environment = new EnvironmentInfo
            {
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                WorkingDirectory = Environment.CurrentDirectory,
                CommandLine = Environment.CommandLine
            },
            Memory = new MemoryInfo
            {
                ManagedMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2)
            }
        };

        // Disk information
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:\\");
            data.Disk = new DiskInfo
            {
                DriveName = drive.Name,
                DriveFormat = drive.DriveFormat,
                TotalSizeGB = drive.TotalSize / 1024 / 1024 / 1024,
                FreeSpaceGB = drive.AvailableFreeSpace / 1024 / 1024 / 1024
            };
        }
        catch (Exception ex)
        {
            data.Disk = new DiskInfo
            {
                Error = ex.Message
            };
        }

        // Process information
        using (var currentProcess = Process.GetCurrentProcess())
        {
            data.Process = new ProcessInfo
            {
                ProcessId = currentProcess.Id,
                ProcessName = currentProcess.ProcessName,
                StartTime = currentProcess.StartTime,
                ThreadCount = currentProcess.Threads.Count,
                HandleCount = currentProcess.HandleCount
            };
        }

        return data;
    }

    private void OutputJson(DiagnosticsData data)
    {
        Console.WriteLine("{");
        Console.WriteLine($"  \"timestamp\": \"{data.Timestamp:O}\",");
        Console.WriteLine("  \"platform\": {");
        Console.WriteLine($"    \"os\": \"{data.Platform.OS}\",");
        Console.WriteLine($"    \"architecture\": \"{data.Platform.Architecture}\",");
        Console.WriteLine($"    \"framework\": \"{data.Platform.Framework}\",");
        Console.WriteLine($"    \"runtime\": \"{data.Platform.Runtime}\",");
        Console.WriteLine($"    \"processorCount\": {data.Platform.ProcessorCount}");
        Console.WriteLine("  },");
        Console.WriteLine("  \"environment\": {");
        Console.WriteLine($"    \"machineName\": \"{data.Environment.MachineName}\",");
        Console.WriteLine($"    \"userName\": \"{data.Environment.UserName}\",");
        Console.WriteLine($"    \"workingDirectory\": \"{data.Environment.WorkingDirectory.Replace("\\", "\\\\")}\"");
        Console.WriteLine("  },");
        Console.WriteLine("  \"memory\": {");
        Console.WriteLine($"    \"managedMemoryMB\": {data.Memory.ManagedMemoryMB},");
        Console.WriteLine($"    \"gen0Collections\": {data.Memory.Gen0Collections},");
        Console.WriteLine($"    \"gen1Collections\": {data.Memory.Gen1Collections},");
        Console.WriteLine($"    \"gen2Collections\": {data.Memory.Gen2Collections}");
        Console.WriteLine("  },");
        Console.WriteLine("  \"disk\": {");
        if (!string.IsNullOrEmpty(data.Disk.Error))
        {
            Console.WriteLine($"    \"error\": \"{data.Disk.Error}\"");
        }
        else
        {
            Console.WriteLine($"    \"driveName\": \"{data.Disk.DriveName}\",");
            Console.WriteLine($"    \"driveFormat\": \"{data.Disk.DriveFormat}\",");
            Console.WriteLine($"    \"totalSizeGB\": {data.Disk.TotalSizeGB},");
            Console.WriteLine($"    \"freeSpaceGB\": {data.Disk.FreeSpaceGB}");
        }
        Console.WriteLine("  },");
        Console.WriteLine("  \"process\": {");
        Console.WriteLine($"    \"processId\": {data.Process.ProcessId},");
        Console.WriteLine($"    \"processName\": \"{data.Process.ProcessName}\",");
        Console.WriteLine($"    \"startTime\": \"{data.Process.StartTime:O}\",");
        Console.WriteLine($"    \"threadCount\": {data.Process.ThreadCount},");
        Console.WriteLine($"    \"handleCount\": {data.Process.HandleCount}");
        Console.WriteLine("  }");
        Console.WriteLine("}");
    }

    private void OutputFormatted(DiagnosticsData data, bool verbose)
    {
        // Platform
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Platform:");
        Console.ResetColor();
        Console.WriteLine($"  OS: {data.Platform.OS}");
        Console.WriteLine($"  Architecture: {data.Platform.Architecture}");
        Console.WriteLine($"  Framework: {data.Platform.Framework}");
        Console.WriteLine($"  Runtime: {data.Platform.Runtime}");
        Console.WriteLine($"  CPU Cores: {data.Platform.ProcessorCount}");
        Console.WriteLine();

        // Environment
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Environment:");
        Console.ResetColor();
        Console.WriteLine($"  Machine: {data.Environment.MachineName}");
        Console.WriteLine($"  User: {data.Environment.UserName}");
        Console.WriteLine($"  Working Dir: {data.Environment.WorkingDirectory}");
        if (verbose)
        {
            Console.WriteLine($"  Command Line: {data.Environment.CommandLine}");
        }
        Console.WriteLine();

        // Memory
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Memory:");
        Console.ResetColor();
        Console.WriteLine($"  Managed Memory: {data.Memory.ManagedMemoryMB} MB");
        Console.WriteLine($"  GC Gen0: {data.Memory.Gen0Collections}");
        Console.WriteLine($"  GC Gen1: {data.Memory.Gen1Collections}");
        Console.WriteLine($"  GC Gen2: {data.Memory.Gen2Collections}");
        Console.WriteLine();

        // Disk
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Disk:");
        Console.ResetColor();
        if (!string.IsNullOrEmpty(data.Disk.Error))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Error: {data.Disk.Error}");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine($"  Drive: {data.Disk.DriveName} ({data.Disk.DriveFormat})");
            Console.WriteLine($"  Total: {data.Disk.TotalSizeGB} GB");
            Console.WriteLine($"  Free: {data.Disk.FreeSpaceGB} GB");
            var usagePercent = (data.Disk.TotalSizeGB - data.Disk.FreeSpaceGB) * 100.0 / data.Disk.TotalSizeGB;
            Console.WriteLine($"  Usage: {usagePercent:F1}%");
        }
        Console.WriteLine();

        // Process
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Process:");
        Console.ResetColor();
        Console.WriteLine($"  PID: {data.Process.ProcessId}");
        Console.WriteLine($"  Name: {data.Process.ProcessName}");
        Console.WriteLine($"  Started: {data.Process.StartTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"  Threads: {data.Process.ThreadCount}");
        Console.WriteLine($"  Handles: {data.Process.HandleCount}");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Report generated at: {data.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        Console.ResetColor();
    }

    private class DiagnosticsData
    {
        public DateTime Timestamp { get; set; }
        public PlatformInfo Platform { get; set; } = new();
        public EnvironmentInfo Environment { get; set; } = new();
        public MemoryInfo Memory { get; set; } = new();
        public DiskInfo Disk { get; set; } = new();
        public ProcessInfo Process { get; set; } = new();
    }

    private class PlatformInfo
    {
        public string OS { get; set; } = "";
        public string Architecture { get; set; } = "";
        public string Framework { get; set; } = "";
        public string Runtime { get; set; } = "";
        public int ProcessorCount { get; set; }
    }

    private class EnvironmentInfo
    {
        public string MachineName { get; set; } = "";
        public string UserName { get; set; } = "";
        public string WorkingDirectory { get; set; } = "";
        public string CommandLine { get; set; } = "";
    }

    private class MemoryInfo
    {
        public long ManagedMemoryMB { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
    }

    private class DiskInfo
    {
        public string DriveName { get; set; } = "";
        public string DriveFormat { get; set; } = "";
        public long TotalSizeGB { get; set; }
        public long FreeSpaceGB { get; set; }
        public string Error { get; set; } = "";
    }

    private class ProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public DateTime StartTime { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
    }
}
