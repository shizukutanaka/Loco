using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Platform;

namespace Loco.Core.Components.Actions;

/// <summary>
/// File operation actions - copy, move, delete, etc.
/// </summary>
public class FileAction : ComponentBase, IAction
{
    private readonly ILogger<FileAction> _logger;
    
    public FileAction(ILogger<FileAction> logger = null)
        : base("file.operation", "File Operation", "Performs file operations", ComponentType.Action)
    {
        _logger = logger;
    }
    
    public async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var operation = GetConfig<string>("operation", "copy");
            var source = GetConfig<string>("source", "");
            var destination = GetConfig<string>("destination", "");
            
            // Replace variables in paths
            source = ReplaceVariables(source, context.Variables);
            destination = ReplaceVariables(destination, context.Variables);
            
            switch (operation.ToLower())
            {
                case "copy":
                    if (File.Exists(source))
                    {
                        File.Copy(source, destination, true);
                        _logger?.LogInformation($"Copied file from {source} to {destination}");
                    }
                    else if (Directory.Exists(source))
                    {
                        CopyDirectory(source, destination);
                        _logger?.LogInformation($"Copied directory from {source} to {destination}");
                    }
                    break;
                    
                case "move":
                    if (File.Exists(source))
                    {
                        File.Move(source, destination, true);
                        _logger?.LogInformation($"Moved file from {source} to {destination}");
                    }
                    else if (Directory.Exists(source))
                    {
                        Directory.Move(source, destination);
                        _logger?.LogInformation($"Moved directory from {source} to {destination}");
                    }
                    break;
                    
                case "delete":
                    if (File.Exists(source))
                    {
                        File.Delete(source);
                        _logger?.LogInformation($"Deleted file {source}");
                    }
                    else if (Directory.Exists(source))
                    {
                        Directory.Delete(source, true);
                        _logger?.LogInformation($"Deleted directory {source}");
                    }
                    break;
                    
                case "create":
                    Directory.CreateDirectory(destination);
                    _logger?.LogInformation($"Created directory {destination}");
                    break;
            }
            
            return ActionResult.Ok();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "File operation failed");
            return ActionResult.Fail(ex.Message);
        }
    }
    
    private void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        
        foreach (var file in Directory.GetFiles(source))
        {
            var destFile = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }
        
        foreach (var dir in Directory.GetDirectories(source))
        {
            var destDir = Path.Combine(destination, Path.GetFileName(dir));
            CopyDirectory(dir, destDir);
        }
    }
    
    private string ReplaceVariables(string input, Dictionary<string, object> variables)
    {
        foreach (var kvp in variables)
        {
            input = input.Replace($"${{{kvp.Key}}}", kvp.Value?.ToString() ?? "");
        }
        return input;
    }
}

/// <summary>
/// Application launcher action
/// </summary>
public class ApplicationAction : ComponentBase, IAction
{
    private readonly ILogger<ApplicationAction> _logger;
    
    public ApplicationAction(ILogger<ApplicationAction> logger = null)
        : base("app.launch", "Application Launcher", "Launches applications", ComponentType.Action)
    {
        _logger = logger;
    }
    
    public async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = GetConfig<string>("path", "");
            var arguments = GetConfig<string>("arguments", "");
            var workingDirectory = GetConfig<string>("workingDirectory", "");
            var waitForExit = GetConfig<bool>("waitForExit", false);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = arguments,
                UseShellExecute = true
            };
            
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }
            
            var process = Process.Start(startInfo);
            
            if (process != null && waitForExit)
            {
                await process.WaitForExitAsync(cancellationToken);
                return ActionResult.Ok(process.ExitCode);
            }
            
            _logger?.LogInformation($"Launched application: {path} {arguments}");
            return ActionResult.Ok();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to launch application");
            return ActionResult.Fail(ex.Message);
        }
    }
}

/// <summary>
/// HTTP request action
/// </summary>
public class HttpAction : ComponentBase, IAction
{
    private static readonly HttpClient _httpClient = new();
    private readonly ILogger<HttpAction> _logger;
    
    public HttpAction(ILogger<HttpAction> logger = null)
        : base("http.request", "HTTP Request", "Makes HTTP requests", ComponentType.Action)
    {
        _logger = logger;
    }
    
    public async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = GetConfig<string>("url", "");
            var method = GetConfig<string>("method", "GET");
            var headers = GetConfig<Dictionary<string, string>>("headers", new());
            var body = GetConfig<string>("body", "");
            var timeout = GetConfig<int>("timeout", 30);
            
            using var request = new HttpRequestMessage(new HttpMethod(method), url);
            
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            
            if (!string.IsNullOrEmpty(body) && method != "GET")
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));
            
            var response = await _httpClient.SendAsync(request, cts.Token);
            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
            
            _logger?.LogInformation($"HTTP {method} {url} returned {response.StatusCode}");
            
            return ActionResult.Ok(new
            {
                statusCode = (int)response.StatusCode,
                body = responseBody,
                headers = response.Headers
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "HTTP request failed");
            return ActionResult.Fail(ex.Message);
        }
    }
}

/// <summary>
/// Notification action - Cross-platform
/// </summary>
public class NotificationAction : ComponentBase, IAction
{
    private readonly ILogger<NotificationAction> _logger;
    private readonly IPlatformService _platformService;
    
    public NotificationAction(ILogger<NotificationAction> logger = null, IPlatformService platformService = null)
        : base("notification.show", "Show Notification", "Displays notifications", ComponentType.Action)
    {
        _logger = logger;
        _platformService = platformService ?? PlatformServiceFactory.Create(logger);
    }
    
    public async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var title = GetConfig<string>("title", "Loco Notification");
            var message = GetConfig<string>("message", "");
            var typeStr = GetConfig<string>("type", "info");
            
            var type = typeStr.ToLower() switch
            {
                "success" => NotificationType.Success,
                "warning" => NotificationType.Warning,
                "error" => NotificationType.Error,
                _ => NotificationType.Info
            };
            
            var success = await _platformService.ShowNotificationAsync(title, message, type);
            
            if (success)
            {
                _logger?.LogInformation($"Notification shown: {title} - {message}");
                return ActionResult.Ok();
            }
            else
            {
                return ActionResult.Fail("Failed to show notification");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show notification");
            return ActionResult.Fail(ex.Message);
        }
    }
}

/// <summary>
/// Text-to-speech action - Cross-platform
/// </summary>
public class TextToSpeechAction : ComponentBase, IAction
{
    private readonly ILogger<TextToSpeechAction> _logger;
    private readonly IPlatformService _platformService;
    
    public TextToSpeechAction(ILogger<TextToSpeechAction> logger = null, IPlatformService platformService = null)
        : base("tts.speak", "Text to Speech", "Converts text to speech", ComponentType.Action)
    {
        _logger = logger;
        _platformService = platformService ?? PlatformServiceFactory.Create(logger);
    }
    
    public async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var text = GetConfig<string>("text", "");
            var rate = GetConfig<int>("rate", 0);
            var volume = GetConfig<int>("volume", 100);
            
            // Replace variables in text
            foreach (var kvp in context.Variables)
            {
                text = text.Replace($"${{{kvp.Key}}}", kvp.Value?.ToString() ?? "");
            }
            
            var success = await _platformService.TextToSpeechAsync(text, rate, volume);
            
            if (success)
            {
                _logger?.LogInformation($"Spoke text: {text}");
                return ActionResult.Ok();
            }
            else
            {
                return ActionResult.Fail("Text-to-speech failed");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Text-to-speech failed");
            return ActionResult.Fail(ex.Message);
        }
    }
}

/// <summary>
/// Command execution action
/// </summary>
public class CommandAction : ComponentBase, IAction
{
    private readonly ILogger<CommandAction> _logger;
    
    public CommandAction(ILogger<CommandAction> logger = null)
        : base("command.execute", "Execute Command", "Executes system commands", ComponentType.Action)
    {
        _logger = logger;
    }
    
    public async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var command = GetConfig<string>("command", "");
            var arguments = GetConfig<string>("arguments", "");
            var workingDirectory = GetConfig<string>("workingDirectory", "");
            var captureOutput = GetConfig<bool>("captureOutput", true);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = captureOutput,
                RedirectStandardError = captureOutput,
                CreateNoWindow = true
            };
            
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }
            
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return ActionResult.Fail("Failed to start process");
            }
            
            if (captureOutput)
            {
                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                
                await process.WaitForExitAsync(cancellationToken);
                
                _logger?.LogInformation($"Command executed: {command} {arguments}, Exit code: {process.ExitCode}");
                
                return ActionResult.Ok(new
                {
                    exitCode = process.ExitCode,
                    output,
                    error
                });
            }
            else
            {
                await process.WaitForExitAsync(cancellationToken);
                return ActionResult.Ok(process.ExitCode);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Command execution failed");
            return ActionResult.Fail(ex.Message);
        }
    }
}

/// <summary>
/// Email action (simplified)
/// </summary>
public class EmailAction : ComponentBase, IAction
{
    private readonly ILogger<EmailAction> _logger;
    
    public EmailAction(ILogger<EmailAction> logger = null)
        : base("email.send", "Send Email", "Sends email notifications", ComponentType.Action)
    {
        _logger = logger;
    }
    
    public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var to = GetConfig<string>("to", "");
            var subject = GetConfig<string>("subject", "");
            var body = GetConfig<string>("body", "");
            
            // In production, use proper SMTP client
            _logger?.LogInformation($"Email would be sent to {to} with subject: {subject}");
            
            return Task.FromResult(ActionResult.Ok());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Email send failed");
            return Task.FromResult(ActionResult.Fail(ex.Message));
        }
    }
}
