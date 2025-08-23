using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;

namespace Loco.Core.Services;

/// <summary>
/// Sandbox executor for secure code execution
/// Following Robert C. Martin's clean code principles
/// </summary>
public class SandboxExecutor : IDisposable
{
    private readonly ILogger<SandboxExecutor> _logger;
    private readonly SemaphoreSlim _executionLock;
    private bool _disposed;
    
    public SandboxExecutor(ILogger<SandboxExecutor> logger = null)
    {
        _logger = logger;
        _executionLock = new SemaphoreSlim(5); // Max 5 concurrent executions
    }
    
    public async Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SandboxExecutor));
        
        var execId = request?.ExecutionId ?? Guid.NewGuid().ToString("N");
        var waitStart = DateTime.UtcNow;
        _logger?.LogInformation("[Sandbox:{ExecId}] Waiting for slot. CurrentCount={Count} Command={Command}", execId, _executionLock.CurrentCount, request?.Command);
        try
        {
            await _executionLock.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("[Sandbox:{ExecId}] Canceled while waiting for slot.", execId);
            throw;
        }
        try
        {
            var waitedMs = (int)(DateTime.UtcNow - waitStart).TotalMilliseconds;
            _logger?.LogInformation("[Sandbox:{ExecId}] Acquired slot after {WaitMs} ms. CurrentCount={Count}", execId, waitedMs, _executionLock.CurrentCount);
            _logger?.LogInformation("Executing in sandbox: {Command}", request.Command);
            
            // Validate permissions
            if (!ValidatePermissions(request))
            {
                return new ExecutionResult
                {
                    Success = false,
                    Error = "Permission denied for requested operation"
                };
            }
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            switch (request.Type)
            {
                case ExecutionType.Process:
                    return await ExecuteProcessAsync(request, cancellationToken);
                case ExecutionType.Script:
                    return await ExecuteScriptAsync(request, cancellationToken);
                case ExecutionType.Shell:
                    return await ExecuteShellAsync(request, cancellationToken);
                default:
                    return new ExecutionResult
                    {
                        Success = false,
                        Error = $"Unknown execution type: {request.Type}"
                    };
            }
        }
        finally
        {
            _executionLock.Release();
            _logger?.LogInformation("[Sandbox:{ExecId}] Released slot. CurrentCount={Count}", execId, _executionLock.CurrentCount);
        }
    }
    
    private bool ValidatePermissions(ExecutionRequest request)
    {
        // Check if requested operation is allowed
        if (request.Type == ExecutionType.Shell && !request.Permissions.Shell)
        {
            _logger?.LogWarning("Shell execution denied - permission not granted");
            return false;
        }
        
        if (!string.IsNullOrEmpty(request.Command))
        {
            // Check for dangerous commands
            var dangerousCommands = new[] { "format", "del", "rm", "shutdown", "reboot" };
            var commandLower = request.Command.ToLower();
            
            foreach (var dangerous in dangerousCommands)
            {
                if (commandLower.Contains(dangerous))
                {
                    _logger?.LogWarning("Dangerous command blocked: {Command}", request.Command);
                    return false;
                }
            }
        }
        
        return true;
    }
    
    private async Task<ExecutionResult> ExecuteProcessAsync(ExecutionRequest request, CancellationToken cancellationToken)
    {
        Process process = null;
        var execId = request.ExecutionId ?? "n/a";
        try
        {
            process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = request.Command,
                Arguments = request.Arguments ?? "",
                WorkingDirectory = request.WorkingDirectory ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            // Add environment variables
            foreach (var env in request.Environment)
            {
                process.StartInfo.Environment[env.Key] = env.Value;
            }
            
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();
            
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    outputBuilder.AppendLine(e.Data);
            };
            
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    errorBuilder.AppendLine(e.Data);
            };
            
            var stopwatch = Stopwatch.StartNew();
            process.Start();
            _logger?.LogInformation("[Sandbox:{ExecId}] Started process PID={Pid} Command={Command} Args={Args}", execId, process.Id, request.Command, request.Arguments);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            // Wait with timeout
            var timeout = request.ResourceLimits?.TimeoutMs ?? 30000;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            
            await process.WaitForExitAsync(cts.Token);
            stopwatch.Stop();
            
            return new ExecutionResult
            {
                Success = process.ExitCode == 0,
                Output = outputBuilder.ToString(),
                Error = errorBuilder.ToString(),
                ExitCode = process.ExitCode,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException)
        {
            // Try to differentiate external cancellation vs timeout
            var external = cancellationToken.IsCancellationRequested;
            var reason = external ? "cancellation" : "timeout";
            try
            {
                if (process != null && !process.HasExited)
                {
                    _logger?.LogWarning("[Sandbox:{ExecId}] Terminating process PID={Pid} due to {Reason}", execId, process.Id, reason);
                    try { process.Kill(true); } catch { /* best effort */ }
                    try { if (!process.WaitForExit(3000)) { _logger?.LogWarning("[Sandbox:{ExecId}] Process PID={Pid} did not exit within grace period", execId, process.Id); } } catch { }
                }
            }
            catch { /* best effort */ }
            return new ExecutionResult
            {
                Success = false,
                Error = external ? "Execution canceled" : "Execution timeout exceeded"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Process execution failed");
            return new ExecutionResult
            {
                Success = false,
                Error = $"Execution failed: {ex.Message}"
            };
        }
        finally
        {
            try
            {
                if (process != null)
                {
                    if (!process.HasExited)
                    {
                        _logger?.LogWarning("[Sandbox:{ExecId}] Disposing live process PID={Pid}, forcing kill", execId, process.Id);
                        try { process.Kill(true); } catch { }
                        try { process.WaitForExit(1000); } catch { }
                    }
                    process.Dispose();
                }
            }
            catch { /* best effort */ }
        }
    }
    
    private async Task<ExecutionResult> ExecuteScriptAsync(ExecutionRequest request, CancellationToken cancellationToken)
    {
        // For script execution, we'll use PowerShell on Windows
        var scriptFile = System.IO.Path.GetTempFileName() + ".ps1";
        try
        {
            await System.IO.File.WriteAllTextAsync(scriptFile, request.Command, cancellationToken);
            
            var scriptRequest = new ExecutionRequest
            {
                Type = ExecutionType.Process,
                Command = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{scriptFile}\"",
                WorkingDirectory = request.WorkingDirectory,
                Environment = request.Environment,
                Permissions = request.Permissions,
                ResourceLimits = request.ResourceLimits,
                ExecutionId = request.ExecutionId
            };
            
            return await ExecuteProcessAsync(scriptRequest, cancellationToken);
        }
        finally
        {
            // Clean up temp file
            if (System.IO.File.Exists(scriptFile))
            {
                try { System.IO.File.Delete(scriptFile); } catch { }
            }
        }
    }
    
    private async Task<ExecutionResult> ExecuteShellAsync(ExecutionRequest request, CancellationToken cancellationToken)
    {
        // Execute shell command using cmd.exe on Windows
        var shellRequest = new ExecutionRequest
        {
            Type = ExecutionType.Process,
            Command = "cmd.exe",
            Arguments = $"/c {request.Command} {request.Arguments}",
            WorkingDirectory = request.WorkingDirectory,
            Environment = request.Environment,
            Permissions = request.Permissions,
            ResourceLimits = request.ResourceLimits,
            ExecutionId = request.ExecutionId
        };
        
        return await ExecuteProcessAsync(shellRequest, cancellationToken);
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _executionLock?.Dispose();
            }
            _disposed = true;
        }
    }
}
