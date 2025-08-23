using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace Loco.Portable;

/// <summary>
/// クロスプラットフォーム対応の実用的な自動化ツール
/// Windows, Linux, macOS で動作
/// </summary>
public class PortableAutomation
{
    private readonly Dictionary<string, string> _appPaths;
    private readonly List<ScheduledTask> _tasks = new();
    private readonly string _configPath;
    private readonly bool _isWindows;
    private readonly bool _isMac;
    private readonly bool _isLinux;
    
    public PortableAutomation()
    {
        _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        _isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        _isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        
        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Loco",
            "config.json"
        );
        
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
        
        _appPaths = GetApplicationPaths();
        LoadConfig();
    }
    
    /// <summary>
    /// OSごとのアプリケーションパスを取得
    /// </summary>
    private Dictionary<string, string> GetApplicationPaths()
    {
        var apps = new Dictionary<string, string>();
        
        if (_isWindows)
        {
            apps["chrome"] = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
            apps["firefox"] = @"C:\Program Files\Mozilla Firefox\firefox.exe";
            apps["notepad"] = "notepad.exe";
            apps["calculator"] = "calc.exe";
            apps["vscode"] = "code";
        }
        else if (_isMac)
        {
            apps["chrome"] = "/Applications/Google Chrome.app";
            apps["firefox"] = "/Applications/Firefox.app";
            apps["safari"] = "/Applications/Safari.app";
            apps["vscode"] = "/Applications/Visual Studio Code.app";
            apps["terminal"] = "/Applications/Utilities/Terminal.app";
        }
        else if (_isLinux)
        {
            apps["chrome"] = "google-chrome";
            apps["firefox"] = "firefox";
            apps["terminal"] = "gnome-terminal";
            apps["vscode"] = "code";
            apps["text"] = "gedit";
        }
        
        return apps;
    }
    
    /// <summary>
    /// コマンドを実行
    /// </summary>
    public async Task Execute(string command)
    {
        var parts = command.Split(' ', 2);
        var action = parts[0].ToLower();
        var args = parts.Length > 1 ? parts[1] : "";
        
        switch (action)
        {
            case "open":
                OpenApp(args);
                break;
                
            case "backup":
                await BackupAsync(args);
                break;
                
            case "notify":
                Notify(args);
                break;
                
            case "schedule":
                Schedule(args);
                break;
                
            case "list":
                ListTasks();
                break;
                
            case "help":
                ShowHelp();
                break;
                
            case "clear":
                Console.Clear();
                break;
                
            case "exit":
            case "quit":
                Environment.Exit(0);
                break;
                
            default:
                Console.WriteLine($"Unknown command: {action}. Type 'help' for commands.");
                break;
        }
    }
    
    /// <summary>
    /// アプリケーションを開く
    /// </summary>
    private void OpenApp(string appName)
    {
        try
        {
            var key = appName.ToLower();
            string command = _appPaths.ContainsKey(key) ? _appPaths[key] : appName;
            
            if (_isWindows)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = command,
                    UseShellExecute = true
                });
            }
            else if (_isMac)
            {
                Process.Start("open", command.Contains(".app") ? command : $"-a \"{command}\"");
            }
            else if (_isLinux)
            {
                Process.Start(command);
            }
            
            Console.WriteLine($"Opened: {appName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open {appName}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// ファイルをバックアップ
    /// </summary>
    private async Task BackupAsync(string args)
    {
        var parts = args.Split(' ');
        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: backup <source> <destination>");
            return;
        }
        
        var source = parts[0];
        var dest = parts[1];
        
        try
        {
            if (File.Exists(source))
            {
                var destFile = Path.Combine(dest, Path.GetFileName(source));
                File.Copy(source, destFile, true);
                Console.WriteLine($"Backed up: {source} → {destFile}");
            }
            else if (Directory.Exists(source))
            {
                await CopyDirectoryAsync(source, dest);
                Console.WriteLine($"Backed up directory: {source} → {dest}");
            }
            else
            {
                Console.WriteLine($"Source not found: {source}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Backup failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 通知を表示
    /// </summary>
    private void Notify(string message)
    {
        Console.WriteLine($"\n[NOTIFICATION] {message}\n");
        
        try
        {
            if (_isWindows)
            {
                // Windows 10+ notification
                var script = $@"
                    Add-Type -AssemblyName System.Windows.Forms
                    $notification = New-Object System.Windows.Forms.NotifyIcon
                    $notification.Icon = [System.Drawing.SystemIcons]::Information
                    $notification.BalloonTipTitle = 'Loco'
                    $notification.BalloonTipText = '{message}'
                    $notification.Visible = $true
                    $notification.ShowBalloonTip(5000)
                ";
                Process.Start("powershell", $"-Command \"{script}\"");
            }
            else if (_isMac)
            {
                // macOS notification
                var script = $"display notification \"{message}\" with title \"Loco\"";
                Process.Start("osascript", $"-e '{script}'");
            }
            else if (_isLinux)
            {
                // Linux notification
                Process.Start("notify-send", $"\"Loco\" \"{message}\"");
            }
        }
        catch
        {
            // Notification failed, but console output was shown
        }
    }
    
    /// <summary>
    /// タスクをスケジュール
    /// </summary>
    private void Schedule(string args)
    {
        var parts = args.Split(' ', 2);
        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: schedule <time> <command>");
            return;
        }
        
        if (TimeSpan.TryParse(parts[0], out var time))
        {
            var task = new ScheduledTask
            {
                Id = Guid.NewGuid().ToString(),
                Time = time,
                Command = parts[1]
            };
            
            _tasks.Add(task);
            SaveConfig();
            
            Console.WriteLine($"Scheduled: {parts[1]} at {time}");
            
            // Start timer
            _ = Task.Run(async () =>
            {
                var now = DateTime.Now.TimeOfDay;
                var delay = time - now;
                if (delay < TimeSpan.Zero)
                    delay = delay.Add(TimeSpan.FromDays(1));
                
                await Task.Delay(delay);
                await Execute(task.Command);
            });
        }
        else
        {
            Console.WriteLine("Invalid time format. Use HH:MM");
        }
    }
    
    /// <summary>
    /// タスク一覧を表示
    /// </summary>
    private void ListTasks()
    {
        if (_tasks.Count == 0)
        {
            Console.WriteLine("No scheduled tasks");
        }
        else
        {
            Console.WriteLine("Scheduled tasks:");
            foreach (var task in _tasks)
            {
                Console.WriteLine($"  {task.Time:hh\\:mm} - {task.Command}");
            }
        }
    }
    
    /// <summary>
    /// ヘルプを表示
    /// </summary>
    private void ShowHelp()
    {
        Console.WriteLine(@"
Loco Portable - Cross-platform Automation

Commands:
  open <app>            Open application
  backup <src> <dest>   Backup files/folders
  notify <message>      Show notification
  schedule <time> <cmd> Schedule task (HH:MM)
  list                  List scheduled tasks
  clear                 Clear screen
  help                  Show this help
  exit                  Exit program

Examples:
  open chrome
  backup ~/Documents ~/Backup
  notify ""Task completed""
  schedule 14:30 notify ""Meeting time""
");
    }
    
    /// <summary>
    /// ディレクトリをコピー
    /// </summary>
    private async Task CopyDirectoryAsync(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        
        foreach (var file in Directory.GetFiles(source))
        {
            var destFile = Path.Combine(dest, Path.GetFileName(file));
            await Task.Run(() => File.Copy(file, destFile, true));
        }
        
        foreach (var dir in Directory.GetDirectories(source))
        {
            var destDir = Path.Combine(dest, Path.GetFileName(dir));
            await CopyDirectoryAsync(dir, destDir);
        }
    }
    
    /// <summary>
    /// 設定を保存
    /// </summary>
    private void SaveConfig()
    {
        try
        {
            var json = JsonSerializer.Serialize(_tasks, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(_configPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
    
    /// <summary>
    /// 設定を読み込み
    /// </summary>
    private void LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var tasks = JsonSerializer.Deserialize<List<ScheduledTask>>(json);
                if (tasks != null)
                {
                    _tasks.AddRange(tasks);
                }
            }
        }
        catch
        {
            // Ignore load errors
        }
    }
}

/// <summary>
/// スケジュールされたタスク
/// </summary>
public class ScheduledTask
{
    public string Id { get; set; }
    public TimeSpan Time { get; set; }
    public string Command { get; set; }
}

/// <summary>
/// メインプログラム
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" :
                RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" : "Unknown";
        
        Console.WriteLine($"Loco Portable v1.0 - Running on {os}");
        Console.WriteLine("Type 'help' for commands\n");
        
        var automation = new PortableAutomation();
        
        // Command line mode
        if (args.Length > 0)
        {
            await automation.Execute(string.Join(" ", args));
            return;
        }
        
        // Interactive mode
        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(input))
                continue;
            
            await automation.Execute(input);
        }
    }
}
