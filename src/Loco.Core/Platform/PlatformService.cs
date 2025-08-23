using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Platform;

/// <summary>
/// Cross-platform abstraction layer
/// Following Rob Pike's portability principles
/// </summary>
public interface IPlatformService
{
    OSPlatform CurrentPlatform { get; }
    string GetPlatformName();
    Task<bool> ShowNotificationAsync(string title, string message, NotificationType type = NotificationType.Info);
    Task<bool> TextToSpeechAsync(string text, int rate = 0, int volume = 100);
    Task<ProcessResult> ExecuteCommandAsync(string command, string arguments = null);
    string GetUserDataPath();
    string GetTempPath();
    bool IsAdministrator();
    Task<SystemInfo> GetSystemInfoAsync();
}

/// <summary>
/// Platform-specific implementation factory
/// </summary>
public static class PlatformServiceFactory
{
    public static IPlatformService Create(ILogger logger = null)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsPlatformService(logger);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxPlatformService(logger);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacOSPlatformService(logger);
        else
            throw new PlatformNotSupportedException($"Platform {RuntimeInformation.OSDescription} is not supported");
    }
}

/// <summary>
/// Windows platform implementation
/// </summary>
public class WindowsPlatformService : IPlatformService
{
    private readonly ILogger _logger;
    
    public OSPlatform CurrentPlatform => OSPlatform.Windows;
    
    public WindowsPlatformService(ILogger logger = null)
    {
        _logger = logger;
    }
    
    public string GetPlatformName() => "Windows";
    
    public async Task<bool> ShowNotificationAsync(string title, string message, NotificationType type = NotificationType.Info)
    {
        try
        {
            // Use Windows 10+ toast notifications
            var script = $@"
                [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
                [Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
                [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null
                
                $template = @'
                <toast>
                    <visual>
                        <binding template=""ToastGeneric"">
                            <text>{title}</text>
                            <text>{message}</text>
                        </binding>
                    </visual>
                </toast>
                '@
                
                $xml = New-Object Windows.Data.Xml.Dom.XmlDocument
                $xml.LoadXml($template)
                $toast = New-Object Windows.UI.Notifications.ToastNotification $xml
                [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Loco').Show($toast)
            ";
            
            await ExecuteCommandAsync("powershell", $"-Command \"{script}\"");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show Windows notification");
            // Fallback to console
            Console.WriteLine($"[{type}] {title}: {message}");
            return false;
        }
    }
    
    public async Task<bool> TextToSpeechAsync(string text, int rate = 0, int volume = 100)
    {
        try
        {
            var script = $@"
                Add-Type -AssemblyName System.Speech
                $synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
                $synth.Rate = {rate}
                $synth.Volume = {volume}
                $synth.Speak('{text.Replace("'", "''")}')
            ";
            
            await ExecuteCommandAsync("powershell", $"-Command \"{script}\"");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to execute text-to-speech on Windows");
            return false;
        }
    }
    
    public async Task<ProcessResult> ExecuteCommandAsync(string command, string arguments = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments ?? "",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(startInfo);
        if (process == null)
            return new ProcessResult { Success = false, Error = "Failed to start process" };
        
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        
        return new ProcessResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            Output = output,
            Error = error
        };
    }
    
    public string GetUserDataPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Loco"
        );
    }
    
    public string GetTempPath() => Path.GetTempPath();
    
    public bool IsAdministrator()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        var info = new SystemInfo
        {
            Platform = "Windows",
            Version = Environment.OSVersion.Version.ToString(),
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            TotalMemory = GC.GetTotalMemory(false)
        };
        
        try
        {
            var result = await ExecuteCommandAsync("wmic", "OS get TotalVisibleMemorySize /value");
            if (result.Success && result.Output.Contains("TotalVisibleMemorySize"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(result.Output, @"TotalVisibleMemorySize=(\d+)");
                if (match.Success && long.TryParse(match.Groups[1].Value, out var kb))
                {
                    info.TotalMemory = kb * 1024;
                }
            }
        }
        catch { }
        
        return info;
    }
}

/// <summary>
/// Linux platform implementation
/// </summary>
public class LinuxPlatformService : IPlatformService
{
    private readonly ILogger _logger;
    
    public OSPlatform CurrentPlatform => OSPlatform.Linux;
    
    public LinuxPlatformService(ILogger logger = null)
    {
        _logger = logger;
    }
    
    public string GetPlatformName() => "Linux";
    
    public async Task<bool> ShowNotificationAsync(string title, string message, NotificationType type = NotificationType.Info)
    {
        try
        {
            // Try notify-send (works on most Linux desktop environments)
            var iconName = type switch
            {
                NotificationType.Error => "error",
                NotificationType.Warning => "warning",
                NotificationType.Success => "info",
                _ => "info"
            };
            
            await ExecuteCommandAsync("notify-send", $"-i {iconName} \"{title}\" \"{message}\"");
            return true;
        }
        catch
        {
            // Try zenity as fallback
            try
            {
                var zenityType = type switch
                {
                    NotificationType.Error => "--error",
                    NotificationType.Warning => "--warning",
                    _ => "--info"
                };
                
                await ExecuteCommandAsync("zenity", $"{zenityType} --title=\"{title}\" --text=\"{message}\"");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to show Linux notification");
                Console.WriteLine($"[{type}] {title}: {message}");
                return false;
            }
        }
    }
    
    public async Task<bool> TextToSpeechAsync(string text, int rate = 0, int volume = 100)
    {
        try
        {
            // Try espeak (commonly available)
            var speed = 175 + (rate * 10); // Convert rate to espeak speed
            var amplitude = volume; // espeak uses 0-200 for amplitude
            
            await ExecuteCommandAsync("espeak", $"-s {speed} -a {amplitude} \"{text}\"");
            return true;
        }
        catch
        {
            // Try festival as fallback
            try
            {
                await ExecuteCommandAsync("sh", $"-c \"echo '{text}' | festival --tts\"");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to execute text-to-speech on Linux");
                return false;
            }
        }
    }
    
    public async Task<ProcessResult> ExecuteCommandAsync(string command, string arguments = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments ?? "",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(startInfo);
        if (process == null)
            return new ProcessResult { Success = false, Error = "Failed to start process" };
        
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        
        return new ProcessResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            Output = output,
            Error = error
        };
    }
    
    public string GetUserDataPath()
    {
        var xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrEmpty(xdgData))
            return Path.Combine(xdgData, "loco");
        
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "loco"
        );
    }
    
    public string GetTempPath() => "/tmp";
    
    public bool IsAdministrator()
    {
        try
        {
            var result = ExecuteCommandAsync("id", "-u").Result;
            return result.Success && result.Output.Trim() == "0";
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        var info = new SystemInfo
        {
            Platform = "Linux",
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            TotalMemory = GC.GetTotalMemory(false)
        };
        
        try
        {
            // Get distribution info
            if (File.Exists("/etc/os-release"))
            {
                var content = await File.ReadAllTextAsync("/etc/os-release");
                var match = System.Text.RegularExpressions.Regex.Match(content, @"PRETTY_NAME=""(.+?)""");
                if (match.Success)
                    info.Version = match.Groups[1].Value;
            }
            
            // Get memory info
            if (File.Exists("/proc/meminfo"))
            {
                var content = await File.ReadAllTextAsync("/proc/meminfo");
                var match = System.Text.RegularExpressions.Regex.Match(content, @"MemTotal:\s+(\d+)");
                if (match.Success && long.TryParse(match.Groups[1].Value, out var kb))
                {
                    info.TotalMemory = kb * 1024;
                }
            }
        }
        catch { }
        
        return info;
    }
}

/// <summary>
/// macOS platform implementation
/// </summary>
public class MacOSPlatformService : IPlatformService
{
    private readonly ILogger _logger;
    
    public OSPlatform CurrentPlatform => OSPlatform.OSX;
    
    public MacOSPlatformService(ILogger logger = null)
    {
        _logger = logger;
    }
    
    public string GetPlatformName() => "macOS";
    
    public async Task<bool> ShowNotificationAsync(string title, string message, NotificationType type = NotificationType.Info)
    {
        try
        {
            // Use osascript for native macOS notifications
            var sound = type == NotificationType.Error ? "Basso" : "default";
            var script = $@"display notification ""{message}"" with title ""{title}"" sound name ""{sound}""";
            
            await ExecuteCommandAsync("osascript", $"-e '{script}'");
            return true;
        }
        catch
        {
            // Try terminal-notifier as fallback
            try
            {
                await ExecuteCommandAsync("terminal-notifier", $"-title \"{title}\" -message \"{message}\"");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to show macOS notification");
                Console.WriteLine($"[{type}] {title}: {message}");
                return false;
            }
        }
    }
    
    public async Task<bool> TextToSpeechAsync(string text, int rate = 0, int volume = 100)
    {
        try
        {
            // Use built-in say command
            var rateParam = rate != 0 ? $"-r {175 + rate * 10}" : "";
            await ExecuteCommandAsync("say", $"{rateParam} \"{text}\"");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to execute text-to-speech on macOS");
            return false;
        }
    }
    
    public async Task<ProcessResult> ExecuteCommandAsync(string command, string arguments = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments ?? "",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(startInfo);
        if (process == null)
            return new ProcessResult { Success = false, Error = "Failed to start process" };
        
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        
        return new ProcessResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            Output = output,
            Error = error
        };
    }
    
    public string GetUserDataPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "Loco"
        );
    }
    
    public string GetTempPath() => Path.GetTempPath();
    
    public bool IsAdministrator()
    {
        try
        {
            var result = ExecuteCommandAsync("id", "-u").Result;
            return result.Success && result.Output.Trim() == "0";
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        var info = new SystemInfo
        {
            Platform = "macOS",
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            TotalMemory = GC.GetTotalMemory(false)
        };
        
        try
        {
            // Get macOS version
            var result = await ExecuteCommandAsync("sw_vers", "-productVersion");
            if (result.Success)
                info.Version = result.Output.Trim();
            
            // Get memory info
            result = await ExecuteCommandAsync("sysctl", "-n hw.memsize");
            if (result.Success && long.TryParse(result.Output.Trim(), out var bytes))
            {
                info.TotalMemory = bytes;
            }
        }
        catch { }
        
        return info;
    }
}

/// <summary>
/// Process execution result
/// </summary>
public class ProcessResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Output { get; set; }
    public string Error { get; set; }
}

/// <summary>
/// System information
/// </summary>
public class SystemInfo
{
    public string Platform { get; set; }
    public string Version { get; set; }
    public string Architecture { get; set; }
    public int ProcessorCount { get; set; }
    public long TotalMemory { get; set; }
}

/// <summary>
/// Notification type
/// </summary>
public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}
