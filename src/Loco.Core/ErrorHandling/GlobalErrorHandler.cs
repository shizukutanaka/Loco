using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Loco.Core.ErrorHandling
{
    public interface IGlobalErrorHandler
    {
        Task HandleExceptionAsync(HttpContext context, Exception exception);
        ErrorResponse CreateErrorResponse(Exception exception, bool includeDetails = false);
        void LogError(Exception exception, string correlationId = null);
        string GenerateCorrelationId();
    }

    public class ErrorResponse
    {
        public string Type { get; set; }
        public string Title { get; set; }
        public int Status { get; set; }
        public string Detail { get; set; }
        public string Instance { get; set; }
        public string CorrelationId { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Extensions { get; set; }
    }

    public class GlobalErrorHandler : IGlobalErrorHandler
    {
        private readonly ILogger<GlobalErrorHandler> _logger;
        private readonly bool _isDevelopment;
        private readonly Dictionary<Type, Func<Exception, ErrorResponse>> _exceptionHandlers;

        public GlobalErrorHandler(ILogger<GlobalErrorHandler> logger, bool isDevelopment = false)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _isDevelopment = isDevelopment;
            _exceptionHandlers = InitializeExceptionHandlers();
        }

        public async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var correlationId = GenerateCorrelationId();
            context.Response.Headers.Add("X-Correlation-Id", correlationId);

            LogError(exception, correlationId);

            var errorResponse = CreateErrorResponse(exception, _isDevelopment);
            errorResponse.CorrelationId = correlationId;
            errorResponse.Instance = context.Request.Path;

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = errorResponse.Status;

            var json = JsonConvert.SerializeObject(errorResponse, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = _isDevelopment ? Formatting.Indented : Formatting.None
            });

            await context.Response.WriteAsync(json);
        }

        public ErrorResponse CreateErrorResponse(Exception exception, bool includeDetails = false)
        {
            // Check if we have a specific handler for this exception type
            foreach (var handler in _exceptionHandlers)
            {
                if (handler.Key.IsAssignableFrom(exception.GetType()))
                {
                    return handler.Value(exception);
                }
            }

            // Default error response
            var response = new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "An error occurred while processing your request",
                Status = (int)HttpStatusCode.InternalServerError,
                Timestamp = DateTime.UtcNow
            };

            if (includeDetails)
            {
                response.Detail = exception.Message;
                response.Extensions = new Dictionary<string, object>
                {
                    ["stackTrace"] = exception.StackTrace,
                    ["source"] = exception.Source,
                    ["targetSite"] = exception.TargetSite?.ToString()
                };

                if (exception.InnerException != null)
                {
                    response.Extensions["innerException"] = CreateErrorResponse(exception.InnerException, true);
                }
            }

            return response;
        }

        public void LogError(Exception exception, string correlationId = null)
        {
            correlationId ??= GenerateCorrelationId();

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["ExceptionType"] = exception.GetType().FullName,
                ["MachineName"] = Environment.MachineName,
                ["ProcessId"] = Process.GetCurrentProcess().Id
            }))
            {
                _logger.LogError(exception, "An unhandled exception occurred. CorrelationId: {CorrelationId}", correlationId);
            }
        }

        public string GenerateCorrelationId()
        {
            return $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 32);
        }

        private Dictionary<Type, Func<Exception, ErrorResponse>> InitializeExceptionHandlers()
        {
            return new Dictionary<Type, Func<Exception, ErrorResponse>>
            {
                [typeof(ValidationException)] = ex => new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Validation Error",
                    Status = (int)HttpStatusCode.BadRequest,
                    Detail = ex.Message,
                    Timestamp = DateTime.UtcNow,
                    Extensions = new Dictionary<string, object>
                    {
                        ["errors"] = ((ValidationException)ex).Errors
                    }
                },

                [typeof(NotFoundException)] = ex => new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                    Title = "Resource Not Found",
                    Status = (int)HttpStatusCode.NotFound,
                    Detail = ex.Message,
                    Timestamp = DateTime.UtcNow
                },

                [typeof(UnauthorizedException)] = ex => new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                    Title = "Unauthorized",
                    Status = (int)HttpStatusCode.Unauthorized,
                    Detail = _isDevelopment ? ex.Message : "Authentication required",
                    Timestamp = DateTime.UtcNow
                },

                [typeof(ForbiddenException)] = ex => new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                    Title = "Forbidden",
                    Status = (int)HttpStatusCode.Forbidden,
                    Detail = _isDevelopment ? ex.Message : "Access denied",
                    Timestamp = DateTime.UtcNow
                },

                [typeof(ConflictException)] = ex => new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                    Title = "Conflict",
                    Status = (int)HttpStatusCode.Conflict,
                    Detail = ex.Message,
                    Timestamp = DateTime.UtcNow
                },

                [typeof(BusinessException)] = ex => new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Business Rule Violation",
                    Status = (int)HttpStatusCode.BadRequest,
                    Detail = ex.Message,
                    Timestamp = DateTime.UtcNow,
                    Extensions = new Dictionary<string, object>
                    {
                        ["code"] = ((BusinessException)ex).Code
                    }
                },

                [typeof(RateLimitException)] = ex => new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc6585#section-4",
                    Title = "Too Many Requests",
                    Status = 429,
                    Detail = ex.Message,
                    Timestamp = DateTime.UtcNow,
                    Extensions = new Dictionary<string, object>
                    {
                        ["retryAfter"] = ((RateLimitException)ex).RetryAfter
                    }
                },

                [typeof(ServiceUnavailableException)] = ex => new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.4",
                    Title = "Service Unavailable",
                    Status = (int)HttpStatusCode.ServiceUnavailable,
                    Detail = _isDevelopment ? ex.Message : "Service temporarily unavailable",
                    Timestamp = DateTime.UtcNow
                },

                [typeof(TimeoutException)] = ex => new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.5",
                    Title = "Gateway Timeout",
                    Status = (int)HttpStatusCode.GatewayTimeout,
                    Detail = _isDevelopment ? ex.Message : "Request timeout",
                    Timestamp = DateTime.UtcNow
                },

                [typeof(ArgumentNullException)] = ex => new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Invalid Argument",
                    Status = (int)HttpStatusCode.BadRequest,
                    Detail = _isDevelopment ? ex.Message : "Invalid request parameters",
                    Timestamp = DateTime.UtcNow
                },

                [typeof(ArgumentException)] = ex => new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Invalid Argument",
                    Status = (int)HttpStatusCode.BadRequest,
                    Detail = _isDevelopment ? ex.Message : "Invalid request parameters",
                    Timestamp = DateTime.UtcNow
                },

                [typeof(InvalidOperationException)] = ex => new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Invalid Operation",
                    Status = (int)HttpStatusCode.BadRequest,
                    Detail = _isDevelopment ? ex.Message : "Invalid operation",
                    Timestamp = DateTime.UtcNow
                },

                [typeof(NotImplementedException)] = ex => new ErrorResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.2",
                    Title = "Not Implemented",
                    Status = (int)HttpStatusCode.NotImplemented,
                    Detail = _isDevelopment ? ex.Message : "Feature not implemented",
                    Timestamp = DateTime.UtcNow
                }
            };
        }
    }

    // Custom exception types
    public class ValidationException : Exception
    {
        public Dictionary<string, string[]> Errors { get; }

        public ValidationException(string message, Dictionary<string, string[]> errors = null) : base(message)
        {
            Errors = errors ?? new Dictionary<string, string[]>();
        }
    }

    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }

    public class UnauthorizedException : Exception
    {
        public UnauthorizedException(string message = "Unauthorized") : base(message) { }
    }

    public class ForbiddenException : Exception
    {
        public ForbiddenException(string message = "Forbidden") : base(message) { }
    }

    public class ConflictException : Exception
    {
        public ConflictException(string message) : base(message) { }
    }

    public class BusinessException : Exception
    {
        public string Code { get; }

        public BusinessException(string message, string code = null) : base(message)
        {
            Code = code;
        }
    }

    public class RateLimitException : Exception
    {
        public int RetryAfter { get; }

        public RateLimitException(string message, int retryAfter) : base(message)
        {
            RetryAfter = retryAfter;
        }
    }

    public class ServiceUnavailableException : Exception
    {
        public ServiceUnavailableException(string message = "Service unavailable") : base(message) { }
    }

    // Global error handling middleware
    public class GlobalErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IGlobalErrorHandler _errorHandler;
        private readonly ILogger<GlobalErrorHandlingMiddleware> _logger;

        public GlobalErrorHandlingMiddleware(
            RequestDelegate next,
            IGlobalErrorHandler errorHandler,
            ILogger<GlobalErrorHandlingMiddleware> logger)
        {
            _next = next;
            _errorHandler = errorHandler;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during request processing");
                await _errorHandler.HandleExceptionAsync(context, ex);
            }
        }
    }
}