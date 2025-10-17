using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Loco.Core.Platform;

/// <summary>
/// クロスプラットフォームシェル統合
/// Cross-platform shell integration (PowerShell/Bash/Zsh)
///
/// 機能: PowerShell (Windows)、Bash (Linux/macOS)、Zsh (macOS) の統一インターフェース
/// Features: Unified interface for PowerShell (Windows), Bash (Linux/macOS), Zsh (macOS)
/// </summary>
public class CrossPlatformShellIntegration
{
    private readonly CrossPlatformPathHandler.PlatformType _platform;
    private readonly ShellType _defaultShell;

    public enum ShellType
    {
        PowerShell,     // Windows PowerShell 5.1
        PowerShellCore, // PowerShell 7+ (cross-platform)
        Bash,           // Linux/macOS
        Zsh,            // macOS default since Catalina
        Sh,             // POSIX shell
        Cmd,            // Windows Command Prompt (legacy)
        Unknown
    }

    public CrossPlatformShellIntegration()
    {
        _platform = CrossPlatformPathHandler.DetectPlatform();
        _defaultShell = DetectDefaultShell();
    }

    public ShellType DefaultShell => _defaultShell;

    /// <summary>
    /// デフォルトシェルを検出
    /// Detect default shell
    /// </summary>
    public static ShellType DetectDefaultShell()
    {
        var platform = CrossPlatformPathHandler.DetectPlatform();

        switch (platform)
        {
            case CrossPlatformPathHandler.PlatformType.Windows:
                // Check for PowerShell 7+ first
                if (IsCommandAvailable("pwsh"))
                {
                    return ShellType.PowerShellCore;
                }
                // Fall back to Windows PowerShell 5.1
                if (IsCommandAvailable("powershell"))
                {
                    return ShellType.PowerShell;
                }
                return ShellType.Cmd;

            case CrossPlatformPathHandler.PlatformType.MacOS:
                // macOS Catalina+ uses zsh by default
                var shell = Environment.GetEnvironmentVariable("SHELL") ?? "";
                if (shell.Contains("zsh") || IsCommandAvailable("zsh"))
                {
                    return ShellType.Zsh;
                }
                if (shell.Contains("bash") || IsCommandAvailable("bash"))
                {
                    return ShellType.Bash;
                }
                return ShellType.Zsh;

            case CrossPlatformPathHandler.PlatformType.Linux:
                // Linux typically uses bash
                var linuxShell = Environment.GetEnvironmentVariable("SHELL") ?? "";
                if (linuxShell.Contains("bash") || IsCommandAvailable("bash"))
                {
                    return ShellType.Bash;
                }
                if (linuxShell.Contains("zsh") || IsCommandAvailable("zsh"))
                {
                    return ShellType.Zsh;
                }
                return ShellType.Bash;

            default:
                return ShellType.Unknown;
        }
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(1000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// コマンドを実行
    /// Execute command with appropriate shell
    /// </summary>
    public async Task<ShellResult> ExecuteCommandAsync(
        string command,
        ShellType? shell = null,
        string? workingDirectory = null,
        Dictionary<string, string>? environmentVariables = null,
        int timeoutMs = 30000)
    {
        var targetShell = shell ?? _defaultShell;
        var shellInfo = GetShellInfo(targetShell);

        var startInfo = new ProcessStartInfo
        {
            FileName = shellInfo.Executable,
            Arguments = FormatCommandArguments(command, targetShell),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
        };

        // Add environment variables
        if (environmentVariables != null)
        {
            foreach (var kvp in environmentVariables)
            {
                startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
        }

        var result = new ShellResult
        {
            Command = command,
            Shell = targetShell,
            StartTime = DateTime.UtcNow
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null) errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var completed = await Task.Run(() => process.WaitForExit(timeoutMs));

            if (!completed)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch { }

                result.Success = false;
                result.ExitCode = -1;
                result.StandardError = "Command timed out";
            }
            else
            {
                result.Success = process.ExitCode == 0;
                result.ExitCode = process.ExitCode;
                result.StandardOutput = outputBuilder.ToString();
                result.StandardError = errorBuilder.ToString();
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ExitCode = -1;
            result.StandardError = ex.Message;
        }

        result.EndTime = DateTime.UtcNow;
        result.Duration = result.EndTime - result.StartTime;

        return result;
    }

    /// <summary>
    /// スクリプトファイルを実行
    /// Execute script file
    /// </summary>
    public async Task<ShellResult> ExecuteScriptAsync(
        string scriptPath,
        ShellType? shell = null,
        string[]? arguments = null,
        string? workingDirectory = null)
    {
        var targetShell = shell ?? _defaultShell;
        var shellInfo = GetShellInfo(targetShell);

        var args = new List<string>();

        // Add script execution flag
        switch (targetShell)
        {
            case ShellType.PowerShell:
            case ShellType.PowerShellCore:
                args.Add("-ExecutionPolicy");
                args.Add("Bypass");
                args.Add("-File");
                args.Add($"\"{scriptPath}\"");
                break;

            case ShellType.Bash:
            case ShellType.Zsh:
            case ShellType.Sh:
                args.Add(scriptPath);
                break;

            case ShellType.Cmd:
                args.Add("/c");
                args.Add(scriptPath);
                break;
        }

        // Add script arguments
        if (arguments != null && arguments.Length > 0)
        {
            args.AddRange(arguments);
        }

        var command = string.Join(" ", args);

        var startInfo = new ProcessStartInfo
        {
            FileName = shellInfo.Executable,
            Arguments = command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory
        };

        var result = new ShellResult
        {
            Command = $"{shellInfo.Executable} {command}",
            Shell = targetShell,
            StartTime = DateTime.UtcNow
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                result.StandardOutput = await outputTask;
                result.StandardError = await errorTask;
                result.ExitCode = process.ExitCode;
                result.Success = process.ExitCode == 0;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ExitCode = -1;
            result.StandardError = ex.Message;
        }

        result.EndTime = DateTime.UtcNow;
        result.Duration = result.EndTime - result.StartTime;

        return result;
    }

    /// <summary>
    /// コマンドの引数をフォーマット
    /// Format command arguments for shell
    /// </summary>
    private string FormatCommandArguments(string command, ShellType shell)
    {
        return shell switch
        {
            ShellType.PowerShell => $"-NoProfile -NonInteractive -Command \"{EscapePowerShellCommand(command)}\"",
            ShellType.PowerShellCore => $"-NoProfile -NonInteractive -Command \"{EscapePowerShellCommand(command)}\"",
            ShellType.Bash => $"-c \"{EscapeBashCommand(command)}\"",
            ShellType.Zsh => $"-c \"{EscapeBashCommand(command)}\"",
            ShellType.Sh => $"-c \"{EscapeBashCommand(command)}\"",
            ShellType.Cmd => $"/c {command}",
            _ => command
        };
    }

    private string EscapePowerShellCommand(string command)
    {
        // Escape double quotes and special characters for PowerShell
        return command.Replace("\"", "`\"").Replace("$", "`$");
    }

    private string EscapeBashCommand(string command)
    {
        // Escape double quotes and special characters for Bash
        return command.Replace("\"", "\\\"").Replace("$", "\\$").Replace("`", "\\`");
    }

    /// <summary>
    /// シェル情報を取得
    /// Get shell information
    /// </summary>
    public ShellInfo GetShellInfo(ShellType shell)
    {
        return shell switch
        {
            ShellType.PowerShell => new ShellInfo
            {
                Type = ShellType.PowerShell,
                Name = "Windows PowerShell",
                Executable = "powershell.exe",
                Version = GetPowerShellVersion("powershell"),
                ScriptExtension = ".ps1",
                CommentPrefix = "#",
                VariablePrefix = "$",
                IsAvailable = IsCommandAvailable("powershell")
            },
            ShellType.PowerShellCore => new ShellInfo
            {
                Type = ShellType.PowerShellCore,
                Name = "PowerShell Core",
                Executable = "pwsh",
                Version = GetPowerShellVersion("pwsh"),
                ScriptExtension = ".ps1",
                CommentPrefix = "#",
                VariablePrefix = "$",
                IsAvailable = IsCommandAvailable("pwsh")
            },
            ShellType.Bash => new ShellInfo
            {
                Type = ShellType.Bash,
                Name = "Bash",
                Executable = "bash",
                Version = GetBashVersion(),
                ScriptExtension = ".sh",
                CommentPrefix = "#",
                VariablePrefix = "$",
                IsAvailable = IsCommandAvailable("bash")
            },
            ShellType.Zsh => new ShellInfo
            {
                Type = ShellType.Zsh,
                Name = "Zsh",
                Executable = "zsh",
                Version = GetZshVersion(),
                ScriptExtension = ".sh",
                CommentPrefix = "#",
                VariablePrefix = "$",
                IsAvailable = IsCommandAvailable("zsh")
            },
            ShellType.Sh => new ShellInfo
            {
                Type = ShellType.Sh,
                Name = "POSIX Shell",
                Executable = "sh",
                Version = "Unknown",
                ScriptExtension = ".sh",
                CommentPrefix = "#",
                VariablePrefix = "$",
                IsAvailable = IsCommandAvailable("sh")
            },
            ShellType.Cmd => new ShellInfo
            {
                Type = ShellType.Cmd,
                Name = "Command Prompt",
                Executable = "cmd.exe",
                Version = "Unknown",
                ScriptExtension = ".bat",
                CommentPrefix = "REM",
                VariablePrefix = "%",
                IsAvailable = true
            },
            _ => new ShellInfo
            {
                Type = ShellType.Unknown,
                Name = "Unknown",
                Executable = "unknown",
                Version = "Unknown",
                ScriptExtension = ".txt",
                CommentPrefix = "#",
                VariablePrefix = "$",
                IsAvailable = false
            }
        };
    }

    private string GetPowerShellVersion(string executable)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "-NoProfile -NonInteractive -Command \"$PSVersionTable.PSVersion.ToString()\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process != null)
            {
                var version = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(2000);
                return version;
            }
        }
        catch { }
        return "Unknown";
    }

    private string GetBashVersion()
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                var firstLine = output.Split('\n')[0];
                process.WaitForExit(2000);
                return firstLine;
            }
        }
        catch { }
        return "Unknown";
    }

    private string GetZshVersion()
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "zsh",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process != null)
            {
                var version = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(2000);
                return version;
            }
        }
        catch { }
        return "Unknown";
    }

    /// <summary>
    /// 利用可能なシェル一覧を取得
    /// Get list of available shells
    /// </summary>
    public List<ShellInfo> GetAvailableShells()
    {
        var shells = new List<ShellType>
        {
            ShellType.PowerShell,
            ShellType.PowerShellCore,
            ShellType.Bash,
            ShellType.Zsh,
            ShellType.Sh,
            ShellType.Cmd
        };

        return shells
            .Select(GetShellInfo)
            .Where(info => info.IsAvailable)
            .ToList();
    }

    /// <summary>
    /// スクリプトを生成
    /// Generate script for target shell
    /// </summary>
    public string GenerateScript(string[] commands, ShellType? shell = null)
    {
        var targetShell = shell ?? _defaultShell;
        var shellInfo = GetShellInfo(targetShell);
        var sb = new StringBuilder();

        // Add shebang for Unix shells
        if (targetShell == ShellType.Bash || targetShell == ShellType.Zsh || targetShell == ShellType.Sh)
        {
            sb.AppendLine($"#!/usr/bin/env {shellInfo.Executable}");
            sb.AppendLine();
        }

        // Add header comment
        sb.AppendLine($"{shellInfo.CommentPrefix} Generated by Loco Automation Platform");
        sb.AppendLine($"{shellInfo.CommentPrefix} Shell: {shellInfo.Name}");
        sb.AppendLine($"{shellInfo.CommentPrefix} Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // Add commands
        foreach (var command in commands)
        {
            sb.AppendLine(command);
        }

        return sb.ToString();
    }

    public class ShellInfo
    {
        public ShellType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Executable { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string ScriptExtension { get; set; } = string.Empty;
        public string CommentPrefix { get; set; } = string.Empty;
        public string VariablePrefix { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
    }

    public class ShellResult
    {
        public string Command { get; set; } = string.Empty;
        public ShellType Shell { get; set; }
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; } = string.Empty;
        public string StandardError { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
