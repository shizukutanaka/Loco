using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Diagnostics;

/// <summary>
/// Comprehensive diagnostics command for troubleshooting and support
/// Generates detailed system reports for production issues
/// </summary>
public class DiagnosticsCommand
{
    private readonly ILogger? _logger;

    public DiagnosticsCommand(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate comprehensive diagnostics report
    /// </summary>
    public async Task<string> GenerateReportAsync(string? outputPath = null)
    {
        var diagnostics = new SystemDiagnostics(_logger);
        var report = await diagnostics.CollectDiagnosticsAsync();

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("  LOCO DIAGNOSTICS REPORT");
        sb.AppendLine($"  Generated: {report.Timestamp:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();

        // Machine Info
        sb.AppendLine("┌─ MACHINE INFORMATION");
        sb.AppendLine($"│  Machine Name:     {report.Machine.MachineName}");
        sb.AppendLine($"│  User:             {report.Machine.UserDomainName}\\{report.Machine.UserName}");
        sb.AppendLine($"│  Architecture:     {(report.Machine.Is64BitOperatingSystem ? "64-bit" : "32-bit")} OS, {(report.Machine.Is64BitProcess ? "64-bit" : "32-bit")} Process");
        sb.AppendLine($"│  Processor Count:  {report.Machine.ProcessorCount} logical processors");
        sb.AppendLine($"│  System Directory: {report.Machine.SystemDirectory}");
        sb.AppendLine($"│  Working Directory:{report.Machine.CurrentDirectory}");
        sb.AppendLine("└─");
        sb.AppendLine();

        // OS Info
        sb.AppendLine("┌─ OPERATING SYSTEM");
        sb.AppendLine($"│  Platform:   {report.OperatingSystem.Platform}");
        sb.AppendLine($"│  Version:    {report.OperatingSystem.VersionString}");
        if (!string.IsNullOrEmpty(report.OperatingSystem.ServicePack))
        {
            sb.AppendLine($"│  Service Pack: {report.OperatingSystem.ServicePack}");
        }
        sb.AppendLine($"│  Uptime:     {FormatUptime(report.OperatingSystem.UptimeSeconds)}");
        sb.AppendLine("└─");
        sb.AppendLine();

        // Hardware Info
        sb.AppendLine("┌─ HARDWARE");
        sb.AppendLine($"│  Processor:    {report.Hardware.ProcessorName}");
        sb.AppendLine($"│  Cores:        {report.Hardware.ProcessorCores} physical, {report.Hardware.ProcessorLogicalProcessors} logical");
        sb.AppendLine($"│  Clock Speed:  {report.Hardware.ProcessorMaxClockSpeedMHz} MHz");
        sb.AppendLine($"│  Total Memory: {report.Hardware.TotalPhysicalMemoryMB:N0} MB ({report.Hardware.TotalPhysicalMemoryMB / 1024.0:F1} GB)");
        sb.AppendLine("└─");
        sb.AppendLine();

        // Runtime Info
        sb.AppendLine("┌─ RUNTIME");
        sb.AppendLine($"│  Framework:        {report.Runtime.FrameworkDescription}");
        sb.AppendLine($"│  Runtime Version:  {report.Runtime.RuntimeVersion}");
        sb.AppendLine($"│  Process ID:       {report.Runtime.ProcessId}");
        sb.AppendLine($"│  Process Name:     {report.Runtime.ProcessName}");
        sb.AppendLine($"│  Start Time:       {report.Runtime.StartTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"│  Running For:      {FormatUptime((DateTime.Now - report.Runtime.StartTime).TotalSeconds)}");
        sb.AppendLine($"│  Working Set:      {report.Runtime.WorkingSetMB:F1} MB");
        sb.AppendLine($"│  Private Memory:   {report.Runtime.PrivateMemoryMB:F1} MB");
        sb.AppendLine($"│  Virtual Memory:   {report.Runtime.VirtualMemoryMB:F1} MB");
        sb.AppendLine($"│  Managed Memory:   {report.Runtime.ManagedMemoryMB:F1} MB");
        sb.AppendLine($"│  Thread Count:     {report.Runtime.ThreadCount}");
        sb.AppendLine($"│  Handle Count:     {report.Runtime.HandleCount}");
        sb.AppendLine($"│  GC Collections:   Gen0={report.Runtime.Gen0Collections}, Gen1={report.Runtime.Gen1Collections}, Gen2={report.Runtime.Gen2Collections}");
        sb.AppendLine("└─");
        sb.AppendLine();

        // Network Info
        sb.AppendLine("┌─ NETWORK");
        sb.AppendLine($"│  Internet:         {(report.Network.InternetConnectivity ? "✓ Connected" : "✗ Disconnected")}");
        if (report.Network.InternetConnectivity)
        {
            sb.AppendLine($"│  Latency:          {report.Network.InternetLatencyMs}ms");
        }
        sb.AppendLine($"│  Active Interfaces: {report.Network.Interfaces.Count}");
        foreach (var iface in report.Network.Interfaces)
        {
            sb.AppendLine($"│    • {iface.Name} ({iface.Type})");
            sb.AppendLine($"│      Status:  {iface.Status}");
            sb.AppendLine($"│      Speed:   {FormatSpeed(iface.Speed)}");
            sb.AppendLine($"│      Sent:    {FormatBytes(iface.BytesSent)}");
            sb.AppendLine($"│      Received:{FormatBytes(iface.BytesReceived)}");
        }
        sb.AppendLine("└─");
        sb.AppendLine();

        // Storage Info
        sb.AppendLine("┌─ STORAGE");
        foreach (var drive in report.Storage.Drives)
        {
            sb.AppendLine($"│  Drive {drive.Name}");
            sb.AppendLine($"│    Type:       {drive.DriveType}");
            sb.AppendLine($"│    File System:{drive.FileSystem}");
            sb.AppendLine($"│    Total:      {drive.TotalSizeGB:F1} GB");
            sb.AppendLine($"│    Used:       {drive.UsedSpaceGB:F1} GB ({drive.UsagePercent:F1}%)");
            sb.AppendLine($"│    Free:       {drive.FreeSpaceGB:F1} GB");

            if (drive.UsagePercent > 90)
            {
                sb.AppendLine($"│    ⚠ WARNING: Low disk space!");
            }
        }
        sb.AppendLine("└─");
        sb.AppendLine();

        // Performance Info
        sb.AppendLine("┌─ PERFORMANCE");
        sb.AppendLine($"│  Total CPU Time:      {report.Performance.TotalProcessorTime}");
        sb.AppendLine($"│  User CPU Time:       {report.Performance.UserProcessorTime}");
        sb.AppendLine($"│  Privileged CPU Time: {report.Performance.PrivilegedProcessorTime}");
        sb.AppendLine($"│  Paged Memory:        {report.Performance.PagedMemoryMB:F1} MB");
        sb.AppendLine($"│  Paged System Memory: {report.Performance.PagedSystemMemoryMB:F1} MB");
        sb.AppendLine($"│  Non-Paged Sys Memory:{report.Performance.NonPagedSystemMemoryMB:F1} MB");
        sb.AppendLine("└─");
        sb.AppendLine();

        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("  END OF REPORT");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");

        var reportText = sb.ToString();

        // Save to file if path provided
        if (!string.IsNullOrEmpty(outputPath))
        {
            await File.WriteAllTextAsync(outputPath, reportText);
            _logger?.LogInformation("Diagnostics report saved to: {Path}", outputPath);

            // Also save JSON version
            var jsonPath = Path.ChangeExtension(outputPath, ".json");
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(report, jsonOptions);
            await File.WriteAllTextAsync(jsonPath, json);
            _logger?.LogInformation("JSON diagnostics report saved to: {Path}", jsonPath);
        }

        return reportText;
    }

    private string FormatUptime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalDays >= 1)
            return $"{ts.Days}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1)
            return $"{ts.Hours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }

    private string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:N1} {suffixes[counter]}";
    }

    private string FormatSpeed(long bitsPerSecond)
    {
        if (bitsPerSecond >= 1_000_000_000)
            return $"{bitsPerSecond / 1_000_000_000.0:F1} Gbps";
        if (bitsPerSecond >= 1_000_000)
            return $"{bitsPerSecond / 1_000_000.0:F1} Mbps";
        if (bitsPerSecond >= 1_000)
            return $"{bitsPerSecond / 1_000.0:F1} Kbps";
        return $"{bitsPerSecond} bps";
    }
}
