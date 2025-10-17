using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Loco.Core.Integration;

/// <summary>
/// Windowsサービス統合機能
/// </summary>
public class WindowsServiceIntegration
{
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool StartService(IntPtr hService, int dwNumServiceArgs, string[] lpServiceArgVectors);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr hSCObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern IntPtr OpenSCManager(string lpMachineName, string lpDatabaseName, uint dwDesiredAccess);

    private const uint SC_MANAGER_CONNECT = 0x0001;
    private const uint SERVICE_START = 0x0010;
    private const uint SERVICE_QUERY_STATUS = 0x0004;

    /// <summary>
    /// サービスがインストールされているか確認
    /// </summary>
    public static bool IsServiceInstalled(string serviceName)
    {
        try
        {
            using var serviceController = new ServiceController(serviceName);
            var dummy = serviceController.ServiceName; // アクセスして存在を確認
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// サービスのステータスを取得
    /// </summary>
    public static ServiceControllerStatus GetServiceStatus(string serviceName)
    {
        try
        {
            using var serviceController = new ServiceController(serviceName);
            return serviceController.Status;
        }
        catch
        {
            throw new InvalidOperationException($"Service '{serviceName}' not found.");
        }
    }

    /// <summary>
    /// サービスを開始
    /// </summary>
    public static async Task StartServiceAsync(string serviceName, TimeSpan timeout)
    {
        using var serviceController = new ServiceController(serviceName);
        if (serviceController.Status != ServiceControllerStatus.Running)
        {
            serviceController.Start();
            serviceController.WaitForStatus(ServiceControllerStatus.Running, timeout);
        }
    }

    /// <summary>
    /// サービスを停止
    /// </summary>
    public static async Task StopServiceAsync(string serviceName, TimeSpan timeout)
    {
        using var serviceController = new ServiceController(serviceName);
        if (serviceController.Status != ServiceControllerStatus.Stopped)
        {
            serviceController.Stop();
            serviceController.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
        }
    }

    /// <summary>
    /// サービスを再起動
    /// </summary>
    public static async Task RestartServiceAsync(string serviceName, TimeSpan timeout)
    {
        await StopServiceAsync(serviceName, timeout);
        await StartServiceAsync(serviceName, timeout);
    }

    /// <summary>
    /// すべてのサービスを取得
    /// </summary>
    public static IEnumerable<ServiceInfo> GetAllServices()
    {
        return ServiceController.GetServices().Select(sc => new ServiceInfo
        {
            Name = sc.ServiceName,
            DisplayName = sc.DisplayName,
            Status = sc.Status,
            CanStop = sc.CanStop,
            CanPauseAndContinue = sc.CanPauseAndContinue
        });
    }

    /// <summary>
    /// 自動起動サービスを取得
    /// </summary>
    public static IEnumerable<ServiceInfo> GetAutoStartServices()
    {
        try
        {
            var services = new List<ServiceInfo>();
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");

            if (key != null)
            {
                foreach (var serviceName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var serviceKey = key.OpenSubKey(serviceName);
                        if (serviceKey != null)
                        {
                            var startValue = serviceKey.GetValue("Start");
                            if (startValue is int start && start == 2) // SERVICE_AUTO_START
                            {
                                using var sc = new ServiceController(serviceName);
                                services.Add(new ServiceInfo
                                {
                                    Name = sc.ServiceName,
                                    DisplayName = sc.DisplayName,
                                    Status = sc.Status
                                });
                            }
                        }
                    }
                    catch
                    {
                        // サービスにアクセスできない場合はスキップ
                    }
                }
            }

            return services;
        }
        catch
        {
            return Enumerable.Empty<ServiceInfo>();
        }
    }

    /// <summary>
    /// サービス情報
    /// </summary>
    public class ServiceInfo
    {
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public ServiceControllerStatus Status { get; set; }
        public bool CanStop { get; set; }
        public bool CanPauseAndContinue { get; set; }
    }
}

/// <summary>
/// タスクスケジューラ統合機能
/// </summary>
public class TaskSchedulerIntegration
{
    /// <summary>
    /// スケジュールされたタスクを作成
    /// </summary>
    public static async Task CreateScheduledTaskAsync(string taskName, string executablePath,
        string arguments, DateTime startTime, TimeSpan interval, bool enabled = true)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = $"/Create /TN \"{taskName}\" /TR \"\\\"{executablePath}\\\" {arguments}\" " +
                       $"/SC MINUTE /MO {Math.Max(1, (int)interval.TotalMinutes)} " +
                       $"/ST {startTime:HH:mm} /SD {startTime:MM/dd/yyyy} " +
                       $"{(enabled ? "" : "/DISABLE")}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process != null)
        {
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"Failed to create scheduled task: {error}");
            }
        }
    }

    /// <summary>
    /// スケジュールされたタスクを削除
    /// </summary>
    public static async Task DeleteScheduledTaskAsync(string taskName)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = $"/Delete /TN \"{taskName}\" /F",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process != null)
        {
            await process.WaitForExitAsync();
        }
    }

    /// <summary>
    /// スケジュールされたタスクを有効化/無効化
    /// </summary>
    public static async Task SetTaskEnabledAsync(string taskName, bool enabled)
    {
        var action = enabled ? "/ENABLE" : "/DISABLE";
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = $"/Change /TN \"{taskName}\" {action}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process != null)
        {
            await process.WaitForExitAsync();
        }
    }

    /// <summary>
    /// スケジュールされたタスクの一覧を取得
    /// </summary>
    public static async Task<IEnumerable<ScheduledTaskInfo>> GetScheduledTasksAsync()
    {
        var tasks = new List<ScheduledTaskInfo>();

        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = "/Query /FO CSV /NH",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process != null)
        {
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines.Skip(1)) // ヘッダーをスキップ
            {
                var parts = line.Split(',');
                if (parts.Length >= 3)
                {
                    tasks.Add(new ScheduledTaskInfo
                    {
                        Name = parts[0].Trim('\"'),
                        NextRunTime = parts[1].Trim('\"'),
                        Status = parts[2].Trim('\"')
                    });
                }
            }
        }

        return tasks;
    }

    /// <summary>
    /// スケジュールされたタスク情報
    /// </summary>
    public class ScheduledTaskInfo
    {
        public string Name { get; set; } = "";
        public string NextRunTime { get; set; } = "";
        public string Status { get; set; } = "";
    }
}

/// <summary>
/// レジストリ操作機能
/// </summary>
public class RegistryOperations
{
    /// <summary>
    /// レジストリ値を読み取り
    /// </summary>
    public static object? ReadRegistryValue(RegistryHive hive, string keyPath, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(keyPath);
            return key?.GetValue(valueName);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// レジストリ値を書き込み
    /// </summary>
    public static bool WriteRegistryValue(RegistryHive hive, string keyPath, string valueName, object value, RegistryValueKind valueKind = RegistryValueKind.String)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.CreateSubKey(keyPath);
            key?.SetValue(valueName, value, valueKind);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// レジストリキーを削除
    /// </summary>
    public static bool DeleteRegistryKey(RegistryHive hive, string keyPath)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            baseKey.DeleteSubKeyTree(keyPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// レジストリ値を削除
    /// </summary>
    public static bool DeleteRegistryValue(RegistryHive hive, string keyPath, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(keyPath, true);
            key?.DeleteValue(valueName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// レジストリキーのサブキーを列挙
    /// </summary>
    public static IEnumerable<string> EnumerateRegistrySubKeys(RegistryHive hive, string keyPath)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(keyPath);
            return key?.GetSubKeyNames() ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// レジストリ値の名前を列挙
    /// </summary>
    public static IEnumerable<string> EnumerateRegistryValueNames(RegistryHive hive, string keyPath)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(keyPath);
            return key?.GetValueNames() ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Loco設定をレジストリに保存
    /// </summary>
    public static void SaveLocoSettingsToRegistry(Dictionary<string, object> settings)
    {
        const string locoKey = @"SOFTWARE\Loco Automation\Settings";

        foreach (var setting in settings)
        {
            WriteRegistryValue(RegistryHive.CurrentUser, locoKey, setting.Key, setting.Value);
        }
    }

    /// <summary>
    /// Loco設定をレジストリから読み込み
    /// </summary>
    public static Dictionary<string, object> LoadLocoSettingsFromRegistry()
    {
        var settings = new Dictionary<string, object>();
        const string locoKey = @"SOFTWARE\Loco Automation\Settings";

        try
        {
            var valueNames = EnumerateRegistryValueNames(RegistryHive.CurrentUser, locoKey);
            foreach (var valueName in valueNames)
            {
                var value = ReadRegistryValue(RegistryHive.CurrentUser, locoKey, valueName);
                if (value != null)
                {
                    settings[valueName] = value;
                }
            }
        }
        catch
        {
            // 設定が存在しない場合は空の辞書を返す
        }

        return settings;
    }
}

/// <summary>
/// 環境変数管理機能
/// </summary>
public class EnvironmentVariableManager
{
    /// <summary>
    /// 環境変数の値を取得
    /// </summary>
    public static string? GetEnvironmentVariable(string name, EnvironmentVariableTarget target = EnvironmentVariableTarget.Process)
    {
        return Environment.GetEnvironmentVariable(name, target);
    }

    /// <summary>
    /// 環境変数を設定
    /// </summary>
    public static void SetEnvironmentVariable(string name, string? value, EnvironmentVariableTarget target = EnvironmentVariableTarget.Process)
    {
        Environment.SetEnvironmentVariable(name, value, target);
    }

    /// <summary>
    /// すべての環境変数を取得
    /// </summary>
    public static Dictionary<string, string> GetAllEnvironmentVariables(EnvironmentVariableTarget target = EnvironmentVariableTarget.Process)
    {
        var variables = new Dictionary<string, string>();
        try
        {
            var envVars = Environment.GetEnvironmentVariables(target);
            foreach (System.Collections.DictionaryEntry entry in envVars)
            {
                variables[entry.Key.ToString() ?? ""] = entry.Value?.ToString() ?? "";
            }
        }
        catch
        {
            // 環境変数にアクセスできない場合
        }
        return variables;
    }

    /// <summary>
    /// PATH環境変数にディレクトリを追加
    /// </summary>
    public static bool AddToPath(string directoryPath, EnvironmentVariableTarget target = EnvironmentVariableTarget.User)
    {
        try
        {
            var currentPath = GetEnvironmentVariable("PATH", target) ?? "";
            if (currentPath.Contains(directoryPath, StringComparison.OrdinalIgnoreCase))
            {
                return true; // 既に存在する
            }

            var newPath = currentPath + Path.PathSeparator + directoryPath;
            SetEnvironmentVariable("PATH", newPath, target);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// PATH環境変数からディレクトリを削除
    /// </summary>
    public static bool RemoveFromPath(string directoryPath, EnvironmentVariableTarget target = EnvironmentVariableTarget.User)
    {
        try
        {
            var currentPath = GetEnvironmentVariable("PATH", target) ?? "";
            var paths = currentPath.Split(Path.PathSeparator);
            var newPaths = paths.Where(p => !p.Equals(directoryPath, StringComparison.OrdinalIgnoreCase)).ToArray();
            var newPath = string.Join(Path.PathSeparator.ToString(), newPaths);
            SetEnvironmentVariable("PATH", newPath, target);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 一時環境変数を作成
    /// </summary>
    public static IDisposable CreateTemporaryEnvironmentVariable(string name, string value)
    {
        var originalValue = GetEnvironmentVariable(name);
        SetEnvironmentVariable(name, value);

        return new TemporaryEnvironmentVariable(name, originalValue);
    }

    private class TemporaryEnvironmentVariable : IDisposable
    {
        private readonly string _name;
        private readonly string? _originalValue;

        public TemporaryEnvironmentVariable(string name, string? originalValue)
        {
            _name = name;
            _originalValue = originalValue;
        }

        public void Dispose()
        {
            SetEnvironmentVariable(_name, _originalValue);
        }
    }
}

/// <summary>
/// プロセス監視機能
/// </summary>
public class ProcessMonitor
{
    private readonly Dictionary<int, ProcessInfo> _monitoredProcesses = new();
    private readonly System.Timers.Timer _monitorTimer;

    public event EventHandler<ProcessEventArgs>? ProcessStarted;
    public event EventHandler<ProcessEventArgs>? ProcessStopped;

    public ProcessMonitor()
    {
        _monitorTimer = new System.Timers.Timer(5000); // 5秒間隔
        _monitorTimer.Elapsed += async (sender, e) => await CheckProcessesAsync();
        _monitorTimer.Start();
    }

    /// <summary>
    /// プロセスを監視対象に追加
    /// </summary>
    public void AddProcessToMonitor(string processName, Action<ProcessInfo>? onStarted = null, Action<ProcessInfo>? onStopped = null)
    {
        var processes = Process.GetProcessesByName(processName);
        foreach (var process in processes)
        {
            var info = new ProcessInfo
            {
                Id = process.Id,
                Name = process.ProcessName,
                StartTime = process.StartTime,
                OnStarted = onStarted,
                OnStopped = onStopped
            };

            _monitoredProcesses[process.Id] = info;
        }
    }

    /// <summary>
    /// プロセス監視を停止
    /// </summary>
    public void StopMonitoring(int processId)
    {
        _monitoredProcesses.Remove(processId);
    }

    /// <summary>
    /// すべてのプロセス監視を停止
    /// </summary>
    public void StopAllMonitoring()
    {
        _monitoredProcesses.Clear();
    }

    /// <summary>
    /// プロセスを強制終了
    /// </summary>
    public static bool KillProcess(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            process.Kill();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// プロセスを強制終了（名前指定）
    /// </summary>
    public static int KillProcessesByName(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        int killedCount = 0;

        foreach (var process in processes)
        {
            try
            {
                process.Kill();
                killedCount++;
            }
            catch
            {
                // プロセス終了に失敗した場合
            }
        }

        return killedCount;
    }

    /// <summary>
    /// プロセスの詳細情報を取得
    /// </summary>
    public static ProcessInfo? GetProcessInfo(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return new ProcessInfo
            {
                Id = process.Id,
                Name = process.ProcessName,
                StartTime = process.StartTime,
                MemoryUsage = process.WorkingSet64,
                CpuTime = process.TotalProcessorTime,
                ThreadCount = process.Threads.Count
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// プロセス一覧を取得
    /// </summary>
    public static IEnumerable<ProcessInfo> GetProcessList()
    {
        return Process.GetProcesses().Select(p => new ProcessInfo
        {
            Id = p.Id,
            Name = p.ProcessName,
            MemoryUsage = p.WorkingSet64,
            CpuTime = p.TotalProcessorTime,
            ThreadCount = p.Threads.Count
        }).ToList();
    }

    private async Task CheckProcessesAsync()
    {
        var currentProcessIds = new HashSet<int>(_monitoredProcesses.Keys);

        foreach (var processId in currentProcessIds)
        {
            var info = _monitoredProcesses[processId];
            var exists = ProcessExists(processId);

            if (!info.WasRunning && exists)
            {
                // プロセスが開始された
                info.WasRunning = true;
                info.OnStarted?.Invoke(info);
                ProcessStarted?.Invoke(this, new ProcessEventArgs { ProcessInfo = info, EventType = ProcessEventType.Started });
            }
            else if (info.WasRunning && !exists)
            {
                // プロセスが停止した
                info.WasRunning = false;
                info.OnStopped?.Invoke(info);
                ProcessStopped?.Invoke(this, new ProcessEventArgs { ProcessInfo = info, EventType = ProcessEventType.Stopped });
            }
        }
    }

    private static bool ProcessExists(int processId)
    {
        try
        {
            Process.GetProcessById(processId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// プロセス情報
    /// </summary>
    public class ProcessInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public DateTime StartTime { get; set; }
        public long MemoryUsage { get; set; }
        public TimeSpan CpuTime { get; set; }
        public int ThreadCount { get; set; }
        public bool WasRunning { get; set; }
        public Action<ProcessInfo>? OnStarted { get; set; }
        public Action<ProcessInfo>? OnStopped { get; set; }
    }

    /// <summary>
    /// プロセスイベント引数
    /// </summary>
    public class ProcessEventArgs : EventArgs
    {
        public ProcessInfo? ProcessInfo { get; set; }
        public ProcessEventType EventType { get; set; }
    }

    public enum ProcessEventType
    {
        Started,
        Stopped
    }

    /// <summary>
    /// リソース解放
    /// </summary>
    public void Dispose()
    {
        _monitorTimer?.Dispose();
        StopAllMonitoring();
    }
}
