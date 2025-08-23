using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Loco.Simple;

/// <summary>
/// シンプルで実用的な自動化エンジン
/// 実際に動作する最小限の実装
/// </summary>
public class SimpleAutomation
{
    private readonly Dictionary<string, Func<Task>> _commands = new();
    private readonly List<ScheduledTask> _scheduledTasks = new();
    private readonly FileSystemWatcher _fileWatcher;
    private readonly string _configPath;
    
    public SimpleAutomation()
    {
        _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Loco", "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
        
        // ファイル監視の設定
        _fileWatcher = new FileSystemWatcher();
        RegisterBuiltInCommands();
        LoadConfig();
    }
    
    /// <summary>
    /// コマンドを実行
    /// </summary>
    public async Task ExecuteCommand(string command)
    {
        var parts = command.Split(' ', 2);
        var action = parts[0].ToLower();
        var args = parts.Length > 1 ? parts[1] : "";
        
        switch (action)
        {
            case "open":
                await OpenApplication(args);
                break;
                
            case "backup":
                await BackupFiles(args);
                break;
                
            case "schedule":
                await ScheduleTask(args);
                break;
                
            case "watch":
                await WatchFolder(args);
                break;
                
            case "run":
                await RunScript(args);
                break;
                
            case "notify":
                await ShowNotification(args);
                break;
                
            default:
                if (_commands.ContainsKey(action))
                {
                    await _commands[action]();
                }
                else
                {
                    Console.WriteLine($"Unknown command: {action}");
                }
                break;
        }
    }
    
    /// <summary>
    /// アプリケーションを開く
    /// </summary>
    private async Task OpenApplication(string appName)
    {
        try
        {
            var apps = new Dictionary<string, string>
            {
                ["chrome"] = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                ["firefox"] = @"C:\Program Files\Mozilla Firefox\firefox.exe",
                ["notepad"] = "notepad.exe",
                ["calculator"] = "calc.exe",
                ["explorer"] = "explorer.exe",
                ["vscode"] = @"C:\Users\%USERNAME%\AppData\Local\Programs\Microsoft VS Code\Code.exe"
            };
            
            var key = appName.ToLower();
            if (apps.ContainsKey(key))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Environment.ExpandEnvironmentVariables(apps[key]),
                    UseShellExecute = true
                });
                Console.WriteLine($"Opened {appName}");
            }
            else
            {
                // Try to open as-is
                Process.Start(new ProcessStartInfo
                {
                    FileName = appName,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open {appName}: {ex.Message}");
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// ファイルをバックアップ
    /// </summary>
    private async Task BackupFiles(string args)
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
                Console.WriteLine($"Backed up {source} to {destFile}");
            }
            else if (Directory.Exists(source))
            {
                await CopyDirectory(source, dest);
                Console.WriteLine($"Backed up directory {source} to {dest}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Backup failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// タスクをスケジュール
    /// </summary>
    private async Task ScheduleTask(string args)
    {
        // Format: schedule 14:30 open chrome
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
                Time = time,
                Command = parts[1],
                Id = Guid.NewGuid().ToString()
            };
            
            _scheduledTasks.Add(task);
            SaveConfig();
            
            Console.WriteLine($"Scheduled: {parts[1]} at {time}");
            
            // Start timer for the task
            _ = Task.Run(async () =>
            {
                var now = DateTime.Now.TimeOfDay;
                var delay = time - now;
                if (delay < TimeSpan.Zero)
                    delay = delay.Add(TimeSpan.FromDays(1));
                
                await Task.Delay(delay);
                await ExecuteCommand(task.Command);
            });
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// フォルダを監視
    /// </summary>
    private async Task WatchFolder(string args)
    {
        var parts = args.Split(' ', 2);
        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: watch <folder> <command>");
            return;
        }
        
        var folder = parts[0];
        var command = parts[1];
        
        if (Directory.Exists(folder))
        {
            _fileWatcher.Path = folder;
            _fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            _fileWatcher.Filter = "*.*";
            
            _fileWatcher.Changed += async (s, e) =>
            {
                Console.WriteLine($"File changed: {e.FullPath}");
                await ExecuteCommand(command.Replace("{file}", e.FullPath));
            };
            
            _fileWatcher.EnableRaisingEvents = true;
            Console.WriteLine($"Watching {folder}");
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// スクリプトを実行
    /// </summary>
    private async Task RunScript(string scriptPath)
    {
        if (!File.Exists(scriptPath))
        {
            Console.WriteLine($"Script not found: {scriptPath}");
            return;
        }
        
        var lines = await File.ReadAllLinesAsync(scriptPath);
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
            {
                await ExecuteCommand(line.Trim());
                await Task.Delay(100); // Small delay between commands
            }
        }
    }
    
    /// <summary>
    /// 通知を表示
    /// </summary>
    private async Task ShowNotification(string message)
    {
        // Simple console notification for now
        Console.WriteLine($"\n[NOTIFICATION] {message}\n");
        
        // Windows 10+ toast notification
        if (Environment.OSVersion.Version.Major >= 10)
        {
            try
            {
                var ps = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-Command \"New-BurntToastNotification -Text 'Loco', '{message}'\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(ps);
            }
            catch
            {
                // Fallback to message box
                var msgBox = new ProcessStartInfo
                {
                    FileName = "msg",
                    Arguments = $"* /TIME:5 \"{message}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(msgBox);
            }
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// カスタムコマンドを登録
    /// </summary>
    public void RegisterCommand(string name, Func<Task> action)
    {
        _commands[name.ToLower()] = action;
    }
    
    /// <summary>
    /// ディレクトリをコピー
    /// </summary>
    private async Task CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        
        foreach (var file in Directory.GetFiles(source))
        {
            var destFile = Path.Combine(dest, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }
        
        foreach (var dir in Directory.GetDirectories(source))
        {
            var destDir = Path.Combine(dest, Path.GetFileName(dir));
            await CopyDirectory(dir, destDir);
        }
    }
    
    /// <summary>
    /// ビルトインコマンドを登録
    /// </summary>
    private void RegisterBuiltInCommands()
    {
        RegisterCommand("time", async () =>
        {
            Console.WriteLine($"Current time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            await Task.CompletedTask;
        });
        
        RegisterCommand("clear", async () =>
        {
            Console.Clear();
            await Task.CompletedTask;
        });
        
        RegisterCommand("list", async () =>
        {
            Console.WriteLine("Scheduled tasks:");
            foreach (var task in _scheduledTasks)
            {
                Console.WriteLine($"  {task.Time}: {task.Command}");
            }
            await Task.CompletedTask;
        });
        
        RegisterCommand("help", async () =>
        {
            Console.WriteLine(@"
Available commands:
  open <app>           - Open application
  backup <src> <dest>  - Backup files
  schedule <time> <cmd>- Schedule task
  watch <folder> <cmd> - Watch folder
  run <script>         - Run script file
  notify <message>     - Show notification
  time                 - Show current time
  clear                - Clear screen
  list                 - List scheduled tasks
  help                 - Show this help
");
            await Task.CompletedTask;
        });
    }
    
    /// <summary>
    /// 設定を保存
    /// </summary>
    private void SaveConfig()
    {
        var config = new Config
        {
            ScheduledTasks = _scheduledTasks
        };
        
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json);
    }
    
    /// <summary>
    /// 設定を読み込み
    /// </summary>
    private void LoadConfig()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<Config>(json);
                if (config?.ScheduledTasks != null)
                {
                    _scheduledTasks.AddRange(config.ScheduledTasks);
                }
            }
            catch
            {
                // Ignore config errors
            }
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
/// 設定
/// </summary>
public class Config
{
    public List<ScheduledTask> ScheduledTasks { get; set; } = new();
}

/// <summary>
/// メインプログラム
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Loco Simple Automation - v1.0");
        Console.WriteLine("Type 'help' for commands, 'exit' to quit\n");
        
        var automation = new SimpleAutomation();
        
        // Process command line arguments
        if (args.Length > 0)
        {
            var command = string.Join(" ", args);
            await automation.ExecuteCommand(command);
            return;
        }
        
        // Interactive mode
        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(input))
                continue;
            
            if (input.ToLower() == "exit" || input.ToLower() == "quit")
                break;
            
            try
            {
                await automation.ExecuteCommand(input);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
