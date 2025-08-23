using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loco.Core.Middleware
{
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestResponseLoggingMiddleware> _logger;
        private readonly RequestResponseLoggingOptions _options;
        private readonly HashSet<string> _sensitiveHeaders;

        public RequestResponseLoggingMiddleware(
            RequestDelegate next,
            ILogger<RequestResponseLoggingMiddleware> logger,
            IOptions<RequestResponseLoggingOptions> options)
        {
            _next = next;
            _logger = logger;
            _options = options?.Value ?? new RequestResponseLoggingOptions();
            _sensitiveHeaders = new HashSet<string>(_options.SensitiveHeaders, StringComparer.OrdinalIgnoreCase);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!ShouldLog(context))
            {
                await _next(context);
                return;
            }

            var requestId = GenerateRequestId();
            context.Items["RequestId"] = requestId;
            context.Response.Headers["X-Request-Id"] = requestId;

            var stopwatch = Stopwatch.StartNew();
            var requestLog = await CaptureRequest(context, requestId);

            // Capture original response body stream
            var originalResponseBody = context.Response.Body;
            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            Exception exception = null;
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                
                // Capture and log response
                var responseLog = await CaptureResponse(context, requestId, stopwatch.ElapsedMilliseconds, exception);
                
                // Copy response body back to original stream
                if (context.Response.Body.CanSeek)
                {
                    context.Response.Body.Seek(0, SeekOrigin.Begin);
                    await context.Response.Body.CopyToAsync(originalResponseBody);
                    context.Response.Body = originalResponseBody;
                }

                // Log the complete request/response
                LogRequestResponse(requestLog, responseLog);
                
                // Publish metrics
                PublishMetrics(context, stopwatch.ElapsedMilliseconds, exception != null);
            }
        }

        private bool ShouldLog(HttpContext context)
        {
            // Skip health checks and metrics endpoints
            if (_options.ExcludePaths.Any(path => context.Request.Path.StartsWithSegments(path)))
                return false;

            // Skip static files
            if (_options.ExcludeStaticFiles && IsStaticFile(context.Request.Path))
                return false;

            return true;
        }

        private bool IsStaticFile(PathString path)
        {
            var extensions = new[] { ".js", ".css", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".woff", ".woff2" };
            return extensions.Any(ext => path.Value?.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private string GenerateRequestId()
        {
            return $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 32);
        }

        private async Task<RequestLog> CaptureRequest(HttpContext context, string requestId)
        {
            var request = context.Request;
            
            var requestLog = new RequestLog
            {
                RequestId = requestId,
                Timestamp = DateTime.UtcNow,
                Method = request.Method,
                Path = request.Path,
                QueryString = request.QueryString.ToString(),
                Headers = CaptureHeaders(request.Headers),
                ClientIp = GetClientIp(context),
                UserAgent = request.Headers["User-Agent"].ToString(),
                ContentType = request.ContentType,
                ContentLength = request.ContentLength
            };

            // Capture request body if configured
            if (_options.LogRequestBody && request.ContentLength > 0 && request.ContentLength <= _options.MaxBodySize)
            {
                request.EnableBuffering();
                
                using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                requestLog.Body = await reader.ReadToEndAsync();
                request.Body.Position = 0;

                // Mask sensitive data
                if (_options.MaskSensitiveData)
                {
                    requestLog.Body = MaskSensitiveData(requestLog.Body, request.ContentType);
                }
            }

            // Add custom context
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                requestLog.UserId = context.User.Identity.Name;
                requestLog.Claims = context.User.Claims
                    .Where(c => !_options.SensitiveClaims.Contains(c.Type))
                    .ToDictionary(c => c.Type, c => c.Value);
            }

            return requestLog;
        }

        private async Task<ResponseLog> CaptureResponse(HttpContext context, string requestId, long elapsedMs, Exception exception)
        {
            var response = context.Response;
            
            var responseLog = new ResponseLog
            {
                RequestId = requestId,
                Timestamp = DateTime.UtcNow,
                StatusCode = response.StatusCode,
                Headers = CaptureHeaders(response.Headers),
                ContentType = response.ContentType,
                ContentLength = response.ContentLength,
                ElapsedMilliseconds = elapsedMs
            };

            // Capture response body if configured
            if (_options.LogResponseBody && response.Body.CanSeek)
            {
                response.Body.Seek(0, SeekOrigin.Begin);
                using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                
                if (body.Length <= _options.MaxBodySize)
                {
                    responseLog.Body = body;
                    
                    // Mask sensitive data
                    if (_options.MaskSensitiveData)
                    {
                        responseLog.Body = MaskSensitiveData(responseLog.Body, response.ContentType);
                    }
                }
                
                response.Body.Seek(0, SeekOrigin.Begin);
            }

            // Capture exception details
            if (exception != null)
            {
                responseLog.Exception = new ExceptionLog
                {
                    Type = exception.GetType().Name,
                    Message = exception.Message,
                    StackTrace = _options.LogStackTrace ? exception.StackTrace : null,
                    InnerException = exception.InnerException?.Message
                };
            }

            return responseLog;
        }

        private Dictionary<string, string> CaptureHeaders(IHeaderDictionary headers)
        {
            var result = new Dictionary<string, string>();
            
            foreach (var header in headers)
            {
                if (_sensitiveHeaders.Contains(header.Key))
                {
                    result[header.Key] = "[REDACTED]";
                }
                else
                {
                    result[header.Key] = header.Value.ToString();
                }
            }

            return result;
        }

        private string GetClientIp(HttpContext context)
        {
            // Check for proxy headers
            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded))
            {
                return forwarded.Split(',')[0].Trim();
            }

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private string MaskSensitiveData(string content, string contentType)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            // JSON content
            if (contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
            {
                try
                {
                    var doc = JsonDocument.Parse(content);
                    var masked = MaskJsonDocument(doc.RootElement);
                    return JsonSerializer.Serialize(masked, new JsonSerializerOptions { WriteIndented = true });
                }
                catch
                {
                    // If not valid JSON, return as-is
                    return content;
                }
            }

            // Form data
            if (contentType?.Contains("form", StringComparison.OrdinalIgnoreCase) == true)
            {
                return MaskFormData(content);
            }

            return content;
        }

        private object MaskJsonDocument(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var obj = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        if (IsSensitiveField(property.Name))
                        {
                            obj[property.Name] = "[REDACTED]";
                        }
                        else
                        {
                            obj[property.Name] = MaskJsonDocument(property.Value);
                        }
                    }
                    return obj;

                case JsonValueKind.Array:
                    return element.EnumerateArray().Select(MaskJsonDocument).ToList();

                default:
                    return element.ToString();
            }
        }

        private string MaskFormData(string formData)
        {
            var lines = formData.Split('&');
            var masked = new List<string>();

            foreach (var line in lines)
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2 && IsSensitiveField(parts[0]))
                {
                    masked.Add($"{parts[0]}=[REDACTED]");
                }
                else
                {
                    masked.Add(line);
                }
            }

            return string.Join("&", masked);
        }

        private bool IsSensitiveField(string fieldName)
        {
            var sensitivePatterns = new[]
            {
                "password", "pwd", "secret", "token", "api_key", "apikey",
                "authorization", "credit_card", "card_number", "cvv", "ssn",
                "bank_account", "pin", "private_key"
            };

            return sensitivePatterns.Any(pattern => 
                fieldName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        private void LogRequestResponse(RequestLog request, ResponseLog response)
        {
            var logLevel = DetermineLogLevel(response.StatusCode);
            
            var logEntry = new
            {
                RequestId = request.RequestId,
                Request = request,
                Response = response
            };

            var json = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions
            {
                WriteIndented = _options.PrettyPrint,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            _logger.Log(logLevel, "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms - {Json}",
                request.Method, request.Path, response.StatusCode, response.ElapsedMilliseconds, json);
        }

        private LogLevel DetermineLogLevel(int statusCode)
        {
            return statusCode switch
            {
                >= 500 => LogLevel.Error,
                >= 400 => LogLevel.Warning,
                _ => _options.DefaultLogLevel
            };
        }

        private void PublishMetrics(HttpContext context, long elapsedMs, bool hasError)
        {
            // This would publish to your metrics system (Prometheus, AppInsights, etc.)
            context.Items["Metrics.RequestDuration"] = elapsedMs;
            context.Items["Metrics.StatusCode"] = context.Response.StatusCode;
            context.Items["Metrics.HasError"] = hasError;
        }
    }

    public class RequestResponseLoggingOptions
    {
        public bool LogRequestBody { get; set; } = true;
        public bool LogResponseBody { get; set; } = true;
        public bool LogStackTrace { get; set; } = false;
        public bool MaskSensitiveData { get; set; } = true;
        public bool PrettyPrint { get; set; } = false;
        public bool ExcludeStaticFiles { get; set; } = true;
        public int MaxBodySize { get; set; } = 100_000; // 100KB
        public LogLevel DefaultLogLevel { get; set; } = LogLevel.Information;
        
        public List<string> ExcludePaths { get; set; } = new()
        {
            "/health",
            "/metrics",
            "/swagger"
        };

        public List<string> SensitiveHeaders { get; set; } = new()
        {
            "Authorization",
            "Cookie",
            "Set-Cookie",
            "X-API-Key",
            "X-Auth-Token"
        };

        public List<string> SensitiveClaims { get; set; } = new()
        {
            "password",
            "secret",
            "card_number"
        };
    }

    public class RequestLog
    {
        public string RequestId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Method { get; set; }
        public string Path { get; set; }
        public string QueryString { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string Body { get; set; }
        public string ClientIp { get; set; }
        public string UserAgent { get; set; }
        public string UserId { get; set; }
        public Dictionary<string, string> Claims { get; set; }
        public string ContentType { get; set; }
        public long? ContentLength { get; set; }
    }

    public class ResponseLog
    {
        public string RequestId { get; set; }
        public DateTime Timestamp { get; set; }
        public int StatusCode { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string Body { get; set; }
        public string ContentType { get; set; }
        public long? ContentLength { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public ExceptionLog Exception { get; set; }
    }

    public class ExceptionLog
    {
        public string Type { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public string InnerException { get; set; }
    }
}