using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Loco.Core.ErrorHandling;
using Loco.Core.Exceptions;

namespace Loco.Api.Filters;

/// <summary>
/// Exception filter for LocoException
/// </summary>
public class LocoExceptionFilter : IExceptionFilter
{
    private readonly ILogger<LocoExceptionFilter> _logger;

    public LocoExceptionFilter(ILogger<LocoExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not LocoException locoException)
        {
            return;
        }

        _logger.LogError(locoException, "Loco exception occurred: {ErrorCode}", locoException.ErrorCode);

        var statusCode = GetStatusCode(locoException);
        var title = GetTitle(locoException);

        context.Result = new ObjectResult(new ValidationProblemDetails
        {
            Title = title,
            Detail = locoException.Message,
            Status = statusCode,
            Instance = context.HttpContext.Request.Path,
            TraceId = context.HttpContext.TraceIdentifier,
            Extensions = locoException.Context?.Count > 0 ? new Dictionary<string, object?>
            {
                { "context", locoException.Context }
            } : null
        })
        {
            StatusCode = statusCode
        };

        context.ExceptionHandled = true;
    }

    private int GetStatusCode(LocoException exception)
    {
        return exception switch
        {
            WorkflowExecutionException => StatusCodes.Status500InternalServerError,
            WorkflowValidationException => StatusCodes.Status400BadRequest,
            ActionException => StatusCodes.Status400BadRequest,
            EngineException => StatusCodes.Status500InternalServerError,
            ResourceException => StatusCodes.Status404NotFound,
            TimeoutException => StatusCodes.Status504GatewayTimeout,
            SecurityException => StatusCodes.Status403Forbidden,
            LocoConfigurationException => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private string GetTitle(LocoException exception)
    {
        return exception switch
        {
            WorkflowExecutionException => "Workflow Execution Error",
            WorkflowValidationException => "Workflow Validation Error",
            ActionException => "Action Error",
            EngineException => "Engine Error",
            ResourceException => "Resource Not Found",
            TimeoutException => "Operation Timeout",
            SecurityException => "Security Error",
            LocoConfigurationException => "Configuration Error",
            _ => "Loco Error"
        };
    }
}

/// <summary>
/// Exception filter for validation errors
/// </summary>
public class ValidationExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ValidationExceptionFilter> _logger;

    public ValidationExceptionFilter(ILogger<ValidationExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ValidationException validationException)
        {
            return;
        }

        _logger.LogWarning("Validation exception occurred");

        context.Result = new BadRequestObjectResult(new ValidationProblemDetails
        {
            Title = "Validation Failed",
            Detail = validationException.Message,
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path,
            TraceId = context.HttpContext.TraceIdentifier,
            Errors = validationException.Errors
        });

        context.ExceptionHandled = true;
    }
}

/// <summary>
/// Exception filter for argument exceptions
/// </summary>
public class ArgumentExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ArgumentExceptionFilter> _logger;
    private readonly IHostEnvironment _environment;

    public ArgumentExceptionFilter(ILogger<ArgumentExceptionFilter> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not (ArgumentNullException or ArgumentException))
        {
            return;
        }

        _logger.LogWarning(context.Exception, "Argument exception occurred");

        var isDevelopment = _environment.IsDevelopment();

        context.Result = new BadRequestObjectResult(new ValidationProblemDetails
        {
            Title = "Invalid Argument",
            Detail = isDevelopment ? context.Exception.Message : "The provided argument is invalid",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path,
            TraceId = context.HttpContext.TraceIdentifier,
            Extensions = isDevelopment ? new Dictionary<string, object?>
            {
                { "exceptionType", context.Exception.GetType().Name },
                { "paramName", (context.Exception as ArgumentException)?.ParamName }
            } : null
        });

        context.ExceptionHandled = true;
    }
}

/// <summary>
/// Exception filter for unauthorized access
/// </summary>
public class UnauthorizedAccessFilter : IExceptionFilter
{
    private readonly ILogger<UnauthorizedAccessFilter> _logger;

    public UnauthorizedAccessFilter(ILogger<UnauthorizedAccessFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not UnauthorizedAccessException)
        {
            return;
        }

        _logger.LogWarning("Unauthorized access attempt");

        context.Result = new ForbidResult();
        context.ExceptionHandled = true;
    }
}

/// <summary>
/// Exception filter for not found exceptions
/// </summary>
public class NotFoundFilter : IExceptionFilter
{
    private readonly ILogger<NotFoundFilter> _logger;

    public NotFoundFilter(ILogger<NotFoundFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not KeyNotFoundException)
        {
            return;
        }

        _logger.LogWarning("Resource not found");

        context.Result = new NotFoundObjectResult(new ProblemDetails
        {
            Title = "Resource Not Found",
            Detail = context.Exception.Message,
            Status = StatusCodes.Status404NotFound,
            Instance = context.HttpContext.Request.Path,
            TraceId = context.HttpContext.TraceIdentifier
        });

        context.ExceptionHandled = true;
    }
}

/// <summary>
/// Exception filter for timeout exceptions
/// </summary>
public class TimeoutExceptionFilter : IExceptionFilter
{
    private readonly ILogger<TimeoutExceptionFilter> _logger;

    public TimeoutExceptionFilter(ILogger<TimeoutExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not TimeoutException)
        {
            return;
        }

        _logger.LogWarning("Operation timeout");

        context.Result = new ObjectResult(new ProblemDetails
        {
            Title = "Operation Timeout",
            Detail = "The operation took too long to complete",
            Status = StatusCodes.Status504GatewayTimeout,
            Instance = context.HttpContext.Request.Path,
            TraceId = context.HttpContext.TraceIdentifier
        })
        {
            StatusCode = StatusCodes.Status504GatewayTimeout
        };

        context.ExceptionHandled = true;
    }
}

/// <summary>
/// Global exception filter for unhandled exceptions
/// </summary>
public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public void OnException(ExceptionContext context)
    {
        // Let other filters handle specific exception types first
        // This only handles unhandled exceptions

        _logger.LogError(context.Exception, "Unhandled exception occurred");

        var isDevelopment = _environment.IsDevelopment();
        var statusCode = context.Exception switch
        {
            ArgumentNullException or ArgumentException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            TimeoutException => StatusCodes.Status504GatewayTimeout,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        context.Result = new ObjectResult(new ProblemDetails
        {
            Title = "An error occurred",
            Detail = isDevelopment ? context.Exception.Message : "An internal error occurred",
            Status = statusCode,
            Instance = context.HttpContext.Request.Path,
            TraceId = context.HttpContext.TraceIdentifier,
            Extensions = isDevelopment ? new Dictionary<string, object?>
            {
                { "stackTrace", context.Exception.StackTrace },
                { "exceptionType", context.Exception.GetType().FullName }
            } : null
        })
        {
            StatusCode = statusCode
        };

        context.ExceptionHandled = true;
    }
}

/// <summary>
/// Extension methods for exception filter registration
/// </summary>
public static class ExceptionFilterExtensions
{
    /// <summary>
    /// Adds all Loco exception filters
    /// </summary>
    public static IMvcBuilder AddLocoExceptionFilters(this IMvcBuilder builder)
    {
        return builder
            .AddMvcOptions(options =>
            {
                // Order matters - more specific filters should come first
                options.Filters.Add<LocoExceptionFilter>(1);
                options.Filters.Add<ValidationExceptionFilter>(2);
                options.Filters.Add<ArgumentExceptionFilter>(3);
                options.Filters.Add<UnauthorizedAccessFilter>(4);
                options.Filters.Add<NotFoundFilter>(5);
                options.Filters.Add<TimeoutExceptionFilter>(6);
                options.Filters.Add<GlobalExceptionFilter>(7);
            });
    }
}
