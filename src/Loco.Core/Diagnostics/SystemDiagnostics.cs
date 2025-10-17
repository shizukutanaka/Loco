using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Diagnostics;

/// <summary>
/// Comprehensive system diagnostics for production monitoring
/// Collects hardware, OS, network, and runtime information
/// </summary>
public class SystemDiagnostics
{
    private readonly ILogger? _logger;

    public SystemDiagnostics(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Collect comprehensive system diagnostics
    /// </summary>
    public async Task<SystemDiagnosticsReport> CollectDiagnosticsAsync()
    {
        var report = new SystemDiagnosticsReport
        {
            Timestamp = DateTime.UtcNow,
            Machine = GetMachineInfo(),
            OperatingSystem = GetOperatingSystemInfo(),
            Hardware = await GetHardwareInfoAsync(),
            Runtime = GetRuntimeInfo(),
            Network = await GetNetworkInfoAsync(),
            Storage = GetStorageInfo(),
            Performance = GetPerformanceInfo()
        };

        return report;
    }

    private MachineInfo GetMachineInfo()
    {
        return new MachineInfo
        {
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            UserDomainName = Environment.UserDomainName,
            Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
            Is64BitProcess = Environment.Is64BitProcess,
            ProcessorCount = Environment.ProcessorCount,
            SystemDirectory = Environment.SystemDirectory,
            CurrentDirectory = Environment.CurrentDirectory
        };
    }

    private OperatingSystemInfo GetOperatingSystemInfo()
    {
        return new OperatingSystemInfo
        {
            Platform = Environment.OSVersion.Platform.ToString(),
            Version = Environment.OSVersion.Version.ToString(),
            VersionString = Environment.OSVersion.VersionString,
            ServicePack = Environment.OSVersion.ServicePack,
            TickCount = Environment.TickCount64,
            UptimeSeconds = Environment.TickCount64 / 1000.0
        };
    }

    private async Task<HardwareInfo> GetHardwareInfoAsync()
    {
        var info = new HardwareInfo();

        try
        {
            // Run blocking WMI queries on thread pool to avoid blocking
            await Task.Run(() =>
            {
#pragma warning disable CA1416 // Platform compatibility - This is Windows-only by design
                // Get CPU info
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        info.ProcessorName = obj["Name"]?.ToString() ?? "Unknown";
                        info.ProcessorCores = int.Parse(obj["NumberOfCores"]?.ToString() ?? "0");
                        info.ProcessorLogicalProcessors = int.Parse(obj["NumberOfLogicalProcessors"]?.ToString() ?? "0");
                        info.ProcessorMaxClockSpeedMHz = int.Parse(obj["MaxClockSpeed"]?.ToString() ?? "0");
                        break; // Only first processor
                    }
                }

                // Get memory info
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var totalMemory = long.Parse(obj["TotalPhysicalMemory"]?.ToString() ?? "0");
                        info.TotalPhysicalMemoryMB = totalMemory / 1024 / 1024;
                    }
                }
#pragma warning restore CA1416
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to collect hardware info");
        }

        return info;
    }

    private RuntimeInfo GetRuntimeInfo()
    {
        var process = Process.GetCurrentProcess();

        return new RuntimeInfo
        {
            RuntimeVersion = Environment.Version.ToString(),
            FrameworkDescription = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            ProcessId = process.Id,
            ProcessName = process.ProcessName,
            StartTime = process.StartTime,
            WorkingSetMB = process.WorkingSet64 / 1024.0 / 1024.0,
            PrivateMemoryMB = process.PrivateMemorySize64 / 1024.0 / 1024.0,
            VirtualMemoryMB = process.VirtualMemorySize64 / 1024.0 / 1024.0,
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount,
            ManagedMemoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };
    }

    private async Task<NetworkInfo> GetNetworkInfoAsync()
    {
        var info = new NetworkInfo
        {
            Interfaces = new List<NetworkInterfaceInfo>()
        };

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up)
                {
                    var ipProps = ni.GetIPProperties();
                    var ipv4Stats = ni.GetIPv4Statistics();

                    info.Interfaces.Add(new NetworkInterfaceInfo
                    {
                        Name = ni.Name,
                        Description = ni.Description,
                        Type = ni.NetworkInterfaceType.ToString(),
                        Status = ni.OperationalStatus.ToString(),
                        Speed = ni.Speed,
                        BytesSent = ipv4Stats.BytesSent,
                        BytesReceived = ipv4Stats.BytesReceived
                    });
                }
            }

            // Test internet connectivity
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 2000);
                info.InternetConnectivity = reply.Status == IPStatus.Success;
                info.InternetLatencyMs = reply.Status == IPStatus.Success ? reply.RoundtripTime : 0;
            }
            catch
            {
                info.InternetConnectivity = false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to collect network info");
        }

        return info;
    }

    private StorageInfo GetStorageInfo()
    {
        var info = new StorageInfo
        {
            Drives = new List<DriveInfoModel>()
        };

        try
        {
            foreach (var drive in System.IO.DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    info.Drives.Add(new DriveInfoModel
                    {
                        Name = drive.Name,
                        DriveType = drive.DriveType.ToString(),
                        FileSystem = drive.DriveFormat,
                        TotalSizeGB = drive.TotalSize / 1024.0 / 1024.0 / 1024.0,
                        FreeSpaceGB = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0,
                        UsedSpaceGB = (drive.TotalSize - drive.AvailableFreeSpace) / 1024.0 / 1024.0 / 1024.0
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to collect storage info");
        }

        return info;
    }

    private PerformanceInfo GetPerformanceInfo()
    {
        var process = Process.GetCurrentProcess();

        return new PerformanceInfo
        {
            TotalProcessorTime = process.TotalProcessorTime,
            UserProcessorTime = process.UserProcessorTime,
            PrivilegedProcessorTime = process.PrivilegedProcessorTime,
            PagedMemoryMB = process.PagedMemorySize64 / 1024.0 / 1024.0,
            PagedSystemMemoryMB = process.PagedSystemMemorySize64 / 1024.0 / 1024.0,
            NonPagedSystemMemoryMB = process.NonpagedSystemMemorySize64 / 1024.0 / 1024.0
        };
    }
}

public class SystemDiagnosticsReport
{
    public DateTime Timestamp { get; set; }
    public MachineInfo Machine { get; set; } = new();
    public OperatingSystemInfo OperatingSystem { get; set; } = new();
    public HardwareInfo Hardware { get; set; } = new();
    public RuntimeInfo Runtime { get; set; } = new();
    public NetworkInfo Network { get; set; } = new();
    public StorageInfo Storage { get; set; } = new();
    public PerformanceInfo Performance { get; set; } = new();
}

public class MachineInfo
{
    public string MachineName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserDomainName { get; set; } = string.Empty;
    public bool Is64BitOperatingSystem { get; set; }
    public bool Is64BitProcess { get; set; }
    public int ProcessorCount { get; set; }
    public string SystemDirectory { get; set; } = string.Empty;
    public string CurrentDirectory { get; set; } = string.Empty;
}

public class OperatingSystemInfo
{
    public string Platform { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string VersionString { get; set; } = string.Empty;
    public string ServicePack { get; set; } = string.Empty;
    public long TickCount { get; set; }
    public double UptimeSeconds { get; set; }
}

public class HardwareInfo
{
    public string ProcessorName { get; set; } = string.Empty;
    public int ProcessorCores { get; set; }
    public int ProcessorLogicalProcessors { get; set; }
    public int ProcessorMaxClockSpeedMHz { get; set; }
    public long TotalPhysicalMemoryMB { get; set; }
}

public class RuntimeInfo
{
    public string RuntimeVersion { get; set; } = string.Empty;
    public string FrameworkDescription { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public double WorkingSetMB { get; set; }
    public double PrivateMemoryMB { get; set; }
    public double VirtualMemoryMB { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public double ManagedMemoryMB { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
}

public class NetworkInfo
{
    public List<NetworkInterfaceInfo> Interfaces { get; set; } = new();
    public bool InternetConnectivity { get; set; }
    public long InternetLatencyMs { get; set; }
}

public class NetworkInterfaceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long Speed { get; set; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
}

public class StorageInfo
{
    public List<DriveInfoModel> Drives { get; set; } = new();
}

public class DriveInfoModel
{
    public string Name { get; set; } = string.Empty;
    public string DriveType { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public double TotalSizeGB { get; set; }
    public double FreeSpaceGB { get; set; }
    public double UsedSpaceGB { get; set; }
    public double UsagePercent => TotalSizeGB > 0 ? (UsedSpaceGB / TotalSizeGB) * 100 : 0;
}

public class PerformanceInfo
{
    public TimeSpan TotalProcessorTime { get; set; }
    public TimeSpan UserProcessorTime { get; set; }
    public TimeSpan PrivilegedProcessorTime { get; set; }
    public double PagedMemoryMB { get; set; }
    public double PagedSystemMemoryMB { get; set; }
    public double NonPagedSystemMemoryMB { get; set; }
}
