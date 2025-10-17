using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Loco.Core.Platform;

/// <summary>
/// OS固有の自動化統合
/// OS-specific automation integration (hotkeys, shortcuts, notifications)
///
/// 機能: Windows (AutoHotkey style)、Linux (AutoKey style)、macOS (AppleScript style)
/// Features: Windows shortcuts, Linux automation, macOS scripting
/// </summary>
public class OSAutomationIntegration
{
    private readonly CrossPlatformPathHandler.PlatformType _platform;
    private readonly CrossPlatformShellIntegration _shell;

    public OSAutomationIntegration()
    {
        _platform = CrossPlatformPathHandler.DetectPlatform();
        _shell = new CrossPlatformShellIntegration();
    }

    /// <summary>
    /// OS固有の通知を表示
    /// Display OS-specific notification
    /// </summary>
    public async Task<bool> ShowNotificationAsync(
        string title,
        string message,
        NotificationPriority priority = NotificationPriority.Normal)
    {
        var command = _platform switch
        {
            CrossPlatformPathHandler.PlatformType.Windows => BuildWindowsNotification(title, message, priority),
            CrossPlatformPathHandler.PlatformType.Linux => BuildLinuxNotification(title, message, priority),
            CrossPlatformPathHandler.PlatformType.MacOS => BuildMacOSNotification(title, message, priority),
            _ => null
        };

        if (command == null) return false;

        var result = await _shell.ExecuteCommandAsync(command, timeoutMs: 5000);
        return result.Success;
    }

    private string BuildWindowsNotification(string title, string message, NotificationPriority priority)
    {
        // PowerShell Toast notification (Windows 10/11) - simplified version
        var escapedTitle = title.Replace("'", "''");
        var escapedMessage = message.Replace("'", "''");

        return $"Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.MessageBox]::Show('{escapedMessage}', '{escapedTitle}', 0, [System.Windows.Forms.MessageBoxIcon]::Information)";
    }

    private string BuildLinuxNotification(string title, string message, NotificationPriority priority)
    {
        // Use notify-send (available on most Linux distributions)
        var urgency = priority switch
        {
            NotificationPriority.Low => "low",
            NotificationPriority.Normal => "normal",
            NotificationPriority.High => "normal",
            NotificationPriority.Critical => "critical",
            _ => "normal"
        };

        return $"notify-send -u {urgency} \"{title}\" \"{message}\"";
    }

    private string BuildMacOSNotification(string title, string message, NotificationPriority priority)
    {
        // Use osascript with AppleScript
        return $"osascript -e 'display notification \"{message}\" with title \"{title}\"'";
    }

    /// <summary>
    /// システムトレイ/メニューバーに表示
    /// Show in system tray/menu bar
    /// </summary>
    public async Task<bool> ShowInSystemTrayAsync(string message)
    {
        var command = _platform switch
        {
            CrossPlatformPathHandler.PlatformType.Windows => $"msg * \"{message}\"",
            CrossPlatformPathHandler.PlatformType.Linux => $"zenity --info --text=\"{message}\" --timeout=3 || notify-send \"{message}\"",
            CrossPlatformPathHandler.PlatformType.MacOS => $"osascript -e 'display alert \"{message}\"'",
            _ => null
        };

        if (command == null) return false;

        var result = await _shell.ExecuteCommandAsync(command, timeoutMs: 5000);
        return result.Success;
    }

    /// <summary>
    /// システムサウンドを再生
    /// Play system sound
    /// </summary>
    public async Task<bool> PlaySystemSoundAsync(SystemSound sound)
    {
        var command = _platform switch
        {
            CrossPlatformPathHandler.PlatformType.Windows => BuildWindowsSoundCommand(sound),
            CrossPlatformPathHandler.PlatformType.Linux => BuildLinuxSoundCommand(sound),
            CrossPlatformPathHandler.PlatformType.MacOS => BuildMacOSSoundCommand(sound),
            _ => null
        };

        if (command == null) return false;

        var result = await _shell.ExecuteCommandAsync(command, timeoutMs: 3000);
        return result.Success;
    }

    private string BuildWindowsSoundCommand(SystemSound sound)
    {
        var soundName = sound switch
        {
            SystemSound.Beep => "SystemAsterisk",
            SystemSound.Notification => "SystemNotification",
            SystemSound.Warning => "SystemExclamation",
            SystemSound.Error => "SystemHand",
            SystemSound.Success => "SystemAsterisk",
            _ => "SystemAsterisk"
        };

        return $"[System.Media.SystemSounds]::{soundName}.Play()";
    }

    private string BuildLinuxSoundCommand(SystemSound sound)
    {
        // Use paplay (PulseAudio) or beep
        return sound switch
        {
            SystemSound.Beep => "paplay /usr/share/sounds/freedesktop/stereo/bell.oga || beep",
            SystemSound.Notification => "paplay /usr/share/sounds/freedesktop/stereo/message.oga",
            SystemSound.Warning => "paplay /usr/share/sounds/freedesktop/stereo/dialog-warning.oga",
            SystemSound.Error => "paplay /usr/share/sounds/freedesktop/stereo/dialog-error.oga",
            SystemSound.Success => "paplay /usr/share/sounds/freedesktop/stereo/complete.oga",
            _ => "beep"
        };
    }

    private string BuildMacOSSoundCommand(SystemSound sound)
    {
        var soundName = sound switch
        {
            SystemSound.Beep => "Ping",
            SystemSound.Notification => "Glass",
            SystemSound.Warning => "Basso",
            SystemSound.Error => "Sosumi",
            SystemSound.Success => "Hero",
            _ => "Ping"
        };

        return $"afplay /System/Library/Sounds/{soundName}.aiff";
    }

    /// <summary>
    /// クリップボードにテキストをコピー
    /// Copy text to clipboard
    /// </summary>
    public async Task<bool> CopyToClipboardAsync(string text)
    {
        var command = _platform switch
        {
            CrossPlatformPathHandler.PlatformType.Windows => $"Set-Clipboard -Value \"{text}\"",
            CrossPlatformPathHandler.PlatformType.Linux => $"echo \"{text}\" | xclip -selection clipboard || echo \"{text}\" | xsel --clipboard",
            CrossPlatformPathHandler.PlatformType.MacOS => $"echo \"{text}\" | pbcopy",
            _ => null
        };

        if (command == null) return false;

        var result = await _shell.ExecuteCommandAsync(command, timeoutMs: 3000);
        return result.Success;
    }

    /// <summary>
    /// クリップボードからテキストを取得
    /// Get text from clipboard
    /// </summary>
    public async Task<string?> GetFromClipboardAsync()
    {
        var command = _platform switch
        {
            CrossPlatformPathHandler.PlatformType.Windows => "Get-Clipboard",
            CrossPlatformPathHandler.PlatformType.Linux => "xclip -selection clipboard -o || xsel --clipboard",
            CrossPlatformPathHandler.PlatformType.MacOS => "pbpaste",
            _ => null
        };

        if (command == null) return null;

        var result = await _shell.ExecuteCommandAsync(command, timeoutMs: 3000);
        return result.Success ? result.StandardOutput.Trim() : null;
    }

    /// <summary>
    /// ファイルエクスプローラーでフォルダを開く
    /// Open folder in file explorer
    /// </summary>
    public async Task<bool> OpenInExplorerAsync(string path)
    {
        var command = _platform switch
        {
            CrossPlatformPathHandler.PlatformType.Windows => $"explorer \"{path}\"",
            CrossPlatformPathHandler.PlatformType.Linux => $"xdg-open \"{path}\" || nautilus \"{path}\" || dolphin \"{path}\"",
            CrossPlatformPathHandler.PlatformType.MacOS => $"open \"{path}\"",
            _ => null
        };

        if (command == null) return false;

        var result = await _shell.ExecuteCommandAsync(command, timeoutMs: 5000);
        return result.Success;
    }

    /// <summary>
    /// デフォルトアプリケーションでファイルを開く
    /// Open file with default application
    /// </summary>
    public async Task<bool> OpenFileAsync(string filePath)
    {
        var command = _platform switch
        {
            CrossPlatformPathHandler.PlatformType.Windows => $"Start-Process \"{filePath}\"",
            CrossPlatformPathHandler.PlatformType.Linux => $"xdg-open \"{filePath}\"",
            CrossPlatformPathHandler.PlatformType.MacOS => $"open \"{filePath}\"",
            _ => null
        };

        if (command == null) return false;

        var result = await _shell.ExecuteCommandAsync(command, timeoutMs: 5000);
        return result.Success;
    }

    /// <summary>
    /// URLをデフォルトブラウザで開く
    /// Open URL in default browser
    /// </summary>
    public async Task<bool> OpenUrlAsync(string url)
    {
        var command = _platform switch
        {
            CrossPlatformPathHandler.PlatformType.Windows => $"Start-Process \"{url}\"",
            CrossPlatformPathHandler.PlatformType.Linux => $"xdg-open \"{url}\"",
            CrossPlatformPathHandler.PlatformType.MacOS => $"open \"{url}\"",
            _ => null
        };

        if (command == null) return false;

        var result = await _shell.ExecuteCommandAsync(command, timeoutMs: 5000);
        return result.Success;
    }

    /// <summary>
    /// スクリーンショットを撮る
    /// Take screenshot
    /// </summary>
    public async Task<ScreenshotResult> TakeScreenshotAsync(string? savePath = null)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        savePath ??= _platform switch
        {
            CrossPlatformPathHandler.PlatformType.Windows => $"%USERPROFILE%\\Pictures\\screenshot_{timestamp}.png",
            CrossPlatformPathHandler.PlatformType.Linux => $"~/Pictures/screenshot_{timestamp}.png",
            CrossPlatformPathHandler.PlatformType.MacOS => $"~/Pictures/screenshot_{timestamp}.png",
            _ => $"screenshot_{timestamp}.png"
        };

        var command = _platform switch
        {
            CrossPlatformPathHandler.PlatformType.Windows => "Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.SendKeys]::SendWait('%{PRTSC}')",
            CrossPlatformPathHandler.PlatformType.Linux => $"gnome-screenshot -f \"{savePath}\" || scrot \"{savePath}\" || import -window root \"{savePath}\"",
            CrossPlatformPathHandler.PlatformType.MacOS => $"screencapture \"{savePath}\"",
            _ => null
        };

        if (command == null)
        {
            return new ScreenshotResult { Success = false, ErrorMessage = "Platform not supported" };
        }

        var result = await _shell.ExecuteCommandAsync(command, timeoutMs: 10000);

        return new ScreenshotResult
        {
            Success = result.Success,
            FilePath = result.Success ? savePath : null,
            ErrorMessage = result.Success ? null : result.StandardError
        };
    }

    /// <summary>
    /// システム情報を取得
    /// Get system information
    /// </summary>
    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        var info = new SystemInfo
        {
            Platform = _platform,
            OsVersion = Environment.OSVersion.ToString(),
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            ProcessorCount = Environment.ProcessorCount,
            Is64BitOperatingSystem = Environment.Is64BitOperatingSystem
        };

        // Get detailed OS info
        var command = _platform switch
        {
            CrossPlatformPathHandler.PlatformType.Windows => "systeminfo | findstr /C:\"OS Name\" /C:\"OS Version\"",
            CrossPlatformPathHandler.PlatformType.Linux => "uname -a && lsb_release -a 2>/dev/null || cat /etc/os-release",
            CrossPlatformPathHandler.PlatformType.MacOS => "sw_vers",
            _ => null
        };

        if (command != null)
        {
            var result = await _shell.ExecuteCommandAsync(command, timeoutMs: 5000);
            if (result.Success)
            {
                info.DetailedInfo = result.StandardOutput;
            }
        }

        return info;
    }

    /// <summary>
    /// スタートアップに登録
    /// Register for startup
    /// </summary>
    public async Task<bool> RegisterStartupAsync(string appName, string executablePath)
    {
        var command = _platform switch
        {
            CrossPlatformPathHandler.PlatformType.Windows => BuildWindowsStartupCommand(appName, executablePath),
            CrossPlatformPathHandler.PlatformType.Linux => BuildLinuxStartupCommand(appName, executablePath),
            CrossPlatformPathHandler.PlatformType.MacOS => BuildMacOSStartupCommand(appName, executablePath),
            _ => null
        };

        if (command == null) return false;

        var result = await _shell.ExecuteCommandAsync(command, timeoutMs: 5000);
        return result.Success;
    }

    private string BuildWindowsStartupCommand(string appName, string executablePath)
    {
        // Add to registry Run key
        return $"New-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run' -Name '{appName}' -Value '\"{executablePath}\"' -PropertyType String -Force";
    }

    private string BuildLinuxStartupCommand(string appName, string executablePath)
    {
        // Create .desktop file in ~/.config/autostart/ - use echo commands
        var commands = new List<string>
        {
            "mkdir -p ~/.config/autostart",
            $"echo '[Desktop Entry]' > ~/.config/autostart/{appName}.desktop",
            $"echo 'Type=Application' >> ~/.config/autostart/{appName}.desktop",
            $"echo 'Name={appName}' >> ~/.config/autostart/{appName}.desktop",
            $"echo 'Exec={executablePath}' >> ~/.config/autostart/{appName}.desktop",
            $"echo 'Terminal=false' >> ~/.config/autostart/{appName}.desktop",
            $"echo 'X-GNOME-Autostart-enabled=true' >> ~/.config/autostart/{appName}.desktop"
        };

        return string.Join(" && ", commands);
    }

    private string BuildMacOSStartupCommand(string appName, string executablePath)
    {
        // Create LaunchAgent plist - use simple echo commands
        var commands = new List<string>
        {
            $"mkdir -p ~/Library/LaunchAgents",
            $"echo '<?xml version=\"1.0\" encoding=\"UTF-8\"?>' > ~/Library/LaunchAgents/com.loco.{appName}.plist",
            $"echo '<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">' >> ~/Library/LaunchAgents/com.loco.{appName}.plist",
            $"echo '<plist version=\"1.0\">' >> ~/Library/LaunchAgents/com.loco.{appName}.plist",
            $"echo '<dict>' >> ~/Library/LaunchAgents/com.loco.{appName}.plist",
            $"echo '  <key>Label</key>' >> ~/Library/LaunchAgents/com.loco.{appName}.plist",
            $"echo '  <string>com.loco.{appName}</string>' >> ~/Library/LaunchAgents/com.loco.{appName}.plist",
            $"echo '  <key>ProgramArguments</key>' >> ~/Library/LaunchAgents/com.loco.{appName}.plist",
            $"echo '  <array>' >> ~/Library/LaunchAgents/com.loco.{appName}.plist",
            $"echo '    <string>{executablePath}</string>' >> ~/Library/LaunchAgents/com.loco.{appName}.plist",
            $"echo '  </array>' >> ~/Library/LaunchAgents/com.loco.{appName}.plist",
            $"echo '  <key>RunAtLoad</key>' >> ~/Library/LaunchAgents/com.loco.{appName}.plist",
            $"echo '  <true/>' >> ~/Library/LaunchAgents/com.loco.{appName}.plist",
            $"echo '</dict>' >> ~/Library/LaunchAgents/com.loco.{appName}.plist",
            $"echo '</plist>' >> ~/Library/LaunchAgents/com.loco.{appName}.plist"
        };

        return string.Join(" && ", commands);
    }

    public enum NotificationPriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    public enum SystemSound
    {
        Beep,
        Notification,
        Warning,
        Error,
        Success
    }

    public class ScreenshotResult
    {
        public bool Success { get; set; }
        public string? FilePath { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class SystemInfo
    {
        public CrossPlatformPathHandler.PlatformType Platform { get; set; }
        public string OsVersion { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int ProcessorCount { get; set; }
        public bool Is64BitOperatingSystem { get; set; }
        public string? DetailedInfo { get; set; }
    }
}
