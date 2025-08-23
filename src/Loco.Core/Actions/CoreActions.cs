using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Interfaces;
using Loco.Core.Models;

namespace Loco.Core.Actions
{
    /// <summary>
    /// Core action implementations for the automation engine
    /// </summary>
    public static class CoreActions
    {
        public static Dictionary<string, Func<IAction>> GetCoreActions()
        {
            return new Dictionary<string, Func<IAction>>
            {
                { "log", () => new LogAction() },
                { "file-write", () => new FileWriteAction() },
                { "file-read", () => new FileReadAction() },
                { "http-request", () => new HttpRequestAction() },
                { "delay", () => new DelayAction() },
                { "execute", () => new ExecuteAction() },
                { "notification", () => new NotificationAction() }
            };
        }
    }

    public class LogAction : IAction
    {
        public string Id => "log";
        public string Type => "log";
        
        public async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
        {
            var message = context.Parameters.GetValueOrDefault("message", "")?.ToString() ?? "";
            var level = context.Parameters.GetValueOrDefault("level", "info")?.ToString() ?? "info";
            
            var logger = context.Services?.GetService(typeof(ILogger<LogAction>)) as ILogger<LogAction>;
            
            switch (level.ToLower())
            {
                case "error":
                    logger?.LogError(message);
                    break;
                case "warning":
                    logger?.LogWarning(message);
                    break;
                case "debug":
                    logger?.LogDebug(message);
                    break;
                default:
                    logger?.LogInformation(message);
                    break;
            }

            return new ActionResult
            {
                Success = true,
                Output = new Dictionary<string, object>
                {
                    { "message", message },
                    { "level", level },
                    { "timestamp", DateTime.UtcNow }
                }
            };
        }
    }

    public class FileWriteAction : IAction
    {
        public string Id => "file-write";
        public string Type => "file-write";
        
        public async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
        {
            var path = context.Parameters.GetValueOrDefault("path", "")?.ToString();
            var content = context.Parameters.GetValueOrDefault("content", "")?.ToString() ?? "";
            var append = Convert.ToBoolean(context.Parameters.GetValueOrDefault("append", false));
            
            if (string.IsNullOrEmpty(path))
            {
                return new ActionResult
                {
                    Success = false,
                    Error = "File path is required"
                };
            }

            try
            {
                if (append)
                {
                    await File.AppendAllTextAsync(path, content, cancellationToken);
                }
                else
                {
                    await File.WriteAllTextAsync(path, content, cancellationToken);
                }

                return new ActionResult
                {
                    Success = true,
                    Output = new Dictionary<string, object>
                    {
                        { "path", path },
                        { "bytesWritten", Encoding.UTF8.GetByteCount(content) }
                    }
                };
            }
            catch (Exception ex)
            {
                return new ActionResult
                {
                    Success = false,
                    Error = $"Failed to write file: {ex.Message}"
                };
            }
        }
    }

    public class FileReadAction : IAction
    {
        public string Id => "file-read";
        public string Type => "file-read";
        
        public async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
        {
            var path = context.Parameters.GetValueOrDefault("path", "")?.ToString();
            
            if (string.IsNullOrEmpty(path))
            {
                return new ActionResult
                {
                    Success = false,
                    Error = "File path is required"
                };
            }

            try
            {
                var content = await File.ReadAllTextAsync(path, cancellationToken);
                
                return new ActionResult
                {
                    Success = true,
                    Output = new Dictionary<string, object>
                    {
                        { "path", path },
                        { "content", content },
                        { "size", new FileInfo(path).Length }
                    }
                };
            }
            catch (Exception ex)
            {
                return new ActionResult
                {
                    Success = false,
                    Error = $"Failed to read file: {ex.Message}"
                };
            }
        }
    }

    public class HttpRequestAction : IAction
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        
        public string Id => "http-request";
        public string Type => "http-request";
        
        public async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
        {
            var url = context.Parameters.GetValueOrDefault("url", "")?.ToString();
            var method = context.Parameters.GetValueOrDefault("method", "GET")?.ToString() ?? "GET";
            var headers = context.Parameters.GetValueOrDefault("headers") as Dictionary<string, object>;
            var body = context.Parameters.GetValueOrDefault("body", "")?.ToString();
            
            if (string.IsNullOrEmpty(url))
            {
                return new ActionResult
                {
                    Success = false,
                    Error = "URL is required"
                };
            }

            try
            {
                var request = new HttpRequestMessage(new HttpMethod(method), url);
                
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value?.ToString());
                    }
                }
                
                if (!string.IsNullOrEmpty(body) && method != "GET")
                {
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }
                
                var response = await HttpClient.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                return new ActionResult
                {
                    Success = response.IsSuccessStatusCode,
                    Output = new Dictionary<string, object>
                    {
                        { "statusCode", (int)response.StatusCode },
                        { "body", responseBody },
                        { "headers", response.Headers.ToString() }
                    }
                };
            }
            catch (Exception ex)
            {
                return new ActionResult
                {
                    Success = false,
                    Error = $"HTTP request failed: {ex.Message}"
                };
            }
        }
    }

    public class DelayAction : IAction
    {
        public string Id => "delay";
        public string Type => "delay";
        
        public async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
        {
            var milliseconds = Convert.ToInt32(context.Parameters.GetValueOrDefault("milliseconds", 1000));
            
            await Task.Delay(milliseconds, cancellationToken);
            
            return new ActionResult
            {
                Success = true,
                Output = new Dictionary<string, object>
                {
                    { "delayedMilliseconds", milliseconds }
                }
            };
        }
    }

    public class ExecuteAction : IAction
    {
        public string Id => "execute";
        public string Type => "execute";
        
        public async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
        {
            var command = context.Parameters.GetValueOrDefault("command", "")?.ToString();
            var arguments = context.Parameters.GetValueOrDefault("arguments", "")?.ToString();
            
            if (string.IsNullOrEmpty(command))
            {
                return new ActionResult
                {
                    Success = false,
                    Error = "Command is required"
                };
            }

            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments ?? "",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process == null)
                {
                    return new ActionResult
                    {
                        Success = false,
                        Error = "Failed to start process"
                    };
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                return new ActionResult
                {
                    Success = process.ExitCode == 0,
                    Output = new Dictionary<string, object>
                    {
                        { "exitCode", process.ExitCode },
                        { "output", output },
                        { "error", error }
                    }
                };
            }
            catch (Exception ex)
            {
                return new ActionResult
                {
                    Success = false,
                    Error = $"Failed to execute command: {ex.Message}"
                };
            }
        }
    }

    public class NotificationAction : IAction
    {
        public string Id => "notification";
        public string Type => "notification";
        
        public async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
        {
            var title = context.Parameters.GetValueOrDefault("title", "Notification")?.ToString() ?? "Notification";
            var message = context.Parameters.GetValueOrDefault("message", "")?.ToString() ?? "";
            var level = context.Parameters.GetValueOrDefault("level", "info")?.ToString() ?? "info";
            
            // In a real implementation, this would send actual notifications
            // For now, we just log it
            var logger = context.Services?.GetService(typeof(ILogger<NotificationAction>)) as ILogger<NotificationAction>;
            logger?.LogInformation("Notification: {Title} - {Message} [{Level}]", title, message, level);

            return new ActionResult
            {
                Success = true,
                Output = new Dictionary<string, object>
                {
                    { "title", title },
                    { "message", message },
                    { "level", level },
                    { "timestamp", DateTime.UtcNow }
                }
            };
        }
    }
}