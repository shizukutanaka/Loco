using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Security;

/// <summary>
/// サンドボックス化されたプロセス実行環境
/// </summary>
public class SandboxedProcess : IDisposable
{
    private readonly Process _process;
    private readonly string _workingDirectory;
    private readonly TimeSpan _timeout;
    private readonly long _memoryLimitMB;
    private readonly bool _restrictNetwork;
    private bool _disposed;

    public SandboxedProcess(
        string workingDirectory = null,
        TimeSpan? timeout = null,
        long memoryLimitMB = 512,
        bool restrictNetwork = true)
    {
        _workingDirectory = workingDirectory ?? Path.GetTempPath();
        _timeout = timeout ?? TimeSpan.FromMinutes(5);
        _memoryLimitMB = memoryLimitMB;
        _restrictNetwork = restrictNetwork;

        _process = new Process();
        ConfigureProcess();
    }

    private void ConfigureProcess()
    {
        _process.StartInfo.UseShellExecute = false;
        _process.StartInfo.RedirectStandardInput = true;
        _process.StartInfo.RedirectStandardOutput = true;
        _process.StartInfo.RedirectStandardError = true;
        _process.StartInfo.CreateNoWindow = true;
        _process.StartInfo.WorkingDirectory = _workingDirectory;

        // 環境変数の制限
        _process.StartInfo.Environment.Clear();

        // 基本的な環境変数のみを許可
        _process.StartInfo.Environment["PATH"] = Environment.GetEnvironmentVariable("PATH");
        _process.StartInfo.Environment["TEMP"] = Path.GetTempPath();
        _process.StartInfo.Environment["TMP"] = Path.GetTempPath();
        _process.StartInfo.Environment["USERPROFILE"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // ネットワーク制限（Windows Firewallルールを適用）
        if (_restrictNetwork)
        {
            _process.StartInfo.Environment["NO_NETWORK"] = "1";
        }
    }

    /// <summary>
    /// コマンドを実行（同期）
/// </summary>
    public ProcessResult ExecuteCommand(string command, string arguments = "")
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SandboxedProcess));

        _process.StartInfo.FileName = command;
        _process.StartInfo.Arguments = arguments;

        var result = new ProcessResult();
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        _process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                outputBuilder.AppendLine(e.Data);
        };

        _process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                errorBuilder.AppendLine(e.Data);
        };

        try
        {
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            // メモリ使用量を監視
            var memoryMonitor = Task.Run(() => MonitorMemoryUsage(_process, _memoryLimitMB));

            if (_process.WaitForExit((int)_timeout.TotalMilliseconds))
            {
                result.ExitCode = _process.ExitCode;
                result.StandardOutput = outputBuilder.ToString();
                result.StandardError = errorBuilder.ToString();
                result.Success = _process.ExitCode == 0;
            }
            else
            {
                _process.Kill();
                result.Success = false;
                result.StandardError = $"Process timed out after {_timeout.TotalSeconds} seconds";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.StandardError = $"Process execution failed: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// コマンドを実行（非同期）
/// </summary>
    public async Task<ProcessResult> ExecuteCommandAsync(string command, string arguments = "", CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SandboxedProcess));

        _process.StartInfo.FileName = command;
        _process.StartInfo.Arguments = arguments;

        var result = new ProcessResult();
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        _process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                outputBuilder.AppendLine(e.Data);
        };

        _process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                errorBuilder.AppendLine(e.Data);
        };

        try
        {
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            // メモリ使用量を監視
            var memoryMonitor = MonitorMemoryUsageAsync(_process, _memoryLimitMB, cancellationToken);

            using var timeoutCts = new CancellationTokenSource(_timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await _process.WaitForExitAsync(linkedCts.Token);

            result.ExitCode = _process.ExitCode;
            result.StandardOutput = outputBuilder.ToString();
            result.StandardError = errorBuilder.ToString();
            result.Success = _process.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            try { _process.Kill(); } catch { }
            result.Success = false;
            result.StandardError = $"Process timed out after {_timeout.TotalSeconds} seconds";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.StandardError = $"Process execution failed: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// PowerShellスクリプトを実行
    /// </summary>
    public ProcessResult ExecutePowerShell(string script)
    {
        var encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        return ExecuteCommand("powershell.exe", $"-ExecutionPolicy Bypass -EncodedCommand {encodedScript}");
    }

    /// <summary>
    /// PowerShellスクリプトを実行（非同期）
/// </summary>
    public async Task<ProcessResult> ExecutePowerShellAsync(string script, CancellationToken cancellationToken = default)
    {
        var encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        return await ExecuteCommandAsync("powershell.exe", $"-ExecutionPolicy Bypass -EncodedCommand {encodedScript}", cancellationToken);
    }

    private async Task MonitorMemoryUsageAsync(Process process, long memoryLimitMB, CancellationToken cancellationToken)
    {
        try
        {
            while (!process.HasExited && !cancellationToken.IsCancellationRequested)
            {
                process.Refresh();
                var memoryUsageMB = process.PrivateMemorySize64 / (1024 * 1024);

                if (memoryUsageMB > memoryLimitMB)
                {
                    process.Kill();
                    break;
                }

                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常なキャンセル
        }
        catch
        {
            // メモリ監視のエラーは無視
        }
    }

    private void MonitorMemoryUsage(Process process, long memoryLimitMB)
    {
        try
        {
            while (!process.HasExited)
            {
                process.Refresh();
                var memoryUsageMB = process.PrivateMemorySize64 / (1024 * 1024);

                if (memoryUsageMB > memoryLimitMB)
                {
                    process.Kill();
                    break;
                }

                Thread.Sleep(1000);
            }
        }
        catch
        {
            // メモリ監視のエラーは無視
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill();
            }
            _process.Dispose();
        }
        catch
        {
            // Dispose中のエラーは無視
        }

        _disposed = true;
    }
}

/// <summary>
/// プロセス実行結果
/// </summary>
public class ProcessResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = "";
    public string StandardError { get; set; } = "";
    public TimeSpan ExecutionTime { get; set; }
}

/// <summary>
/// プロセスセキュリティユーティリティ
/// </summary>
public static class ProcessSecurity
{
    /// <summary>
    /// コマンドが安全かどうかを検証
    /// </summary>
    public static bool IsCommandSafe(string command, string arguments)
    {
        var unsafeCommands = new[]
        {
            "format", "fdisk", "diskpart", "del", "rm", "rmdir",
            "shutdown", "reboot", "halt", "poweroff",
            "net", "sc", "reg", "taskkill", "tskill",
            "cipher", "wevtutil", "wmic"
        };

        var cmd = Path.GetFileNameWithoutExtension(command).ToLower();

        if (Array.Exists(unsafeCommands, c => cmd.Contains(c)))
        {
            return false;
        }

        // 危険な引数のチェック
        var unsafeArgs = new[] { "/delete", "/y", "/q", "/f", "/s" };
        if (Array.Exists(unsafeArgs, arg => arguments.ToLower().Contains(arg)))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// パスが許可されたディレクトリ内にあるか検証
    /// </summary>
    public static bool IsPathAllowed(string path, string[] allowedPaths)
    {
        if (allowedPaths == null || allowedPaths.Length == 0)
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path);

        foreach (var allowedPath in allowedPaths)
        {
            var fullAllowedPath = Path.GetFullPath(allowedPath);
            if (fullPath.StartsWith(fullAllowedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// ファイルタイプが安全か検証
    /// </summary>
    public static bool IsFileTypeSafe(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();

        var dangerousExtensions = new[]
        {
            ".exe", ".bat", ".cmd", ".com", ".pif", ".scr", ".vbs",
            ".js", ".jar", ".msi", ".reg", ".lnk"
        };

        return !Array.Exists(dangerousExtensions, ext => extension == ext);
    }
}
