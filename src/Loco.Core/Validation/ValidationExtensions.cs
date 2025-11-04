using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Loco.Core.ErrorHandling;

namespace Loco.Core.Validation;

/// <summary>
/// Extension methods for validation framework integration
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Adds validation framework to the dependency injection container
    /// </summary>
    public static IServiceCollection AddLocoValidation(this IServiceCollection services)
    {
        services.AddSingleton<IValidatorFactory, ValidatorFactory>();

        return services;
    }

    /// <summary>
    /// Registers a specific validator
    /// </summary>
    public static IServiceCollection AddValidator<T>(
        this IServiceCollection services,
        IValidator<T> validator) where T : class
    {
        var factory = services.BuildServiceProvider().GetRequiredService<IValidatorFactory>();
        factory.RegisterValidator(validator);

        return services;
    }

    /// <summary>
    /// Adds validation middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UseLocoValidation(this IApplicationBuilder app)
    {
        app.UseMiddleware<ValidationMiddleware>();
        return app;
    }

    /// <summary>
    /// Configures API behavior for validation errors
    /// </summary>
    public static IMvcBuilder ConfigureApiValidation(this IMvcBuilder builder)
    {
        builder.ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

                var validationException = ValidationException.FromValidationErrors(
                    errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList()));

                return new BadRequestObjectResult(new ValidationProblemDetails
                {
                    Title = "Validation Failed",
                    Status = 400,
                    Errors = validationException.Errors,
                    Detail = $"Validation failed: {validationException.Message}",
                    Instance = context.HttpContext.Request.Path
                });
            };
        });

        return builder;
    }
}

/// <summary>
/// Validation result extensions
/// </summary>
public static class ValidationResultExtensions
{
    /// <summary>
    /// Converts validation result to exception if invalid
    /// </summary>
    public static void ThrowIfInvalid(this ValidationResult result, string? customMessage = null)
    {
        if (!result.IsValid)
        {
            var message = customMessage ?? $"Validation failed with {result.ErrorCount} error(s)";
            var errorDict = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.Message).ToList());

            throw ValidationException.FromValidationErrors(errorDict);
        }
    }

    /// <summary>
    /// Returns validation error dictionary
    /// </summary>
    public static Dictionary<string, string[]> ToErrorDictionary(this ValidationResult result)
    {
        return result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.Message).ToArray());
    }
}

/// <summary>
/// Request validation middleware
/// </summary>
public class RequestValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestValidationMiddleware> _logger;

    public RequestValidationMiddleware(RequestDelegate next, ILogger<RequestValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Store correlation ID for tracing
        var correlationId = context.Request.Headers.TryGetValue("X-Correlation-ID", out var headerValue)
            ? headerValue.ToString()
            : Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers.Add("X-Correlation-ID", correlationId);

        await _next(context);
    }
}

/// <summary>
/// Helper class for controller validation
/// </summary>
public static class ControllerValidationHelper
{
    /// <summary>
    /// Validates an object using the validator factory
    /// </summary>
    public static ValidationResult ValidateRequest<T>(
        this IValidatorFactory factory,
        T obj,
        string? customMessage = null) where T : class
    {
        var validator = factory.GetValidator<T>();
        if (validator == null)
        {
            throw new InvalidOperationException($"No validator registered for type {typeof(T).Name}");
        }

        var result = validator.Validate(obj);
        if (!result.IsValid)
        {
            result.ThrowIfInvalid(customMessage);
        }

        return result;
    }

    /// <summary>
    /// Asynchronously validates an object using the validator factory
    /// </summary>
    public static async Task<ValidationResult> ValidateRequestAsync<T>(
        this IValidatorFactory factory,
        T obj,
        string? customMessage = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var validator = factory.GetValidator<T>();
        if (validator == null)
        {
            throw new InvalidOperationException($"No validator registered for type {typeof(T).Name}");
        }

        var result = await validator.ValidateAsync(obj, cancellationToken);
        if (!result.IsValid)
        {
            result.ThrowIfInvalid(customMessage);
        }

        return result;
    }
}

/// <summary>
/// Example custom validator implementation
/// </summary>
public abstract class FluentValidator<T> : AbstractValidator<T> where T : class
{
    /// <summary>
    /// Fluent API for adding required field rule
    /// </summary>
    protected FluentValidatorRuleBuilder<T> RuleFor(Func<T, object?> property, string propertyName)
    {
        return new FluentValidatorRuleBuilder<T>(this, property, propertyName);
    }

    /// <summary>
    /// Fluent API for adding string field rule
    /// </summary>
    protected FluentValidatorRuleBuilder<T> RuleForString(Func<T, string?> property, string propertyName)
    {
        return new FluentValidatorRuleBuilder<T>(this, property, propertyName);
    }
}

/// <summary>
/// Fluent validator rule builder for chainable API
/// </summary>
public class FluentValidatorRuleBuilder<T> where T : class
{
    private readonly AbstractValidator<T> _validator;
    private readonly Func<T, object?> _property;
    private readonly string _propertyName;

    public FluentValidatorRuleBuilder(
        AbstractValidator<T> validator,
        Func<T, object?> property,
        string propertyName)
    {
        _validator = validator;
        _property = property;
        _propertyName = propertyName;
    }

    /// <summary>
    /// Adds required rule
    /// </summary>
    public FluentValidatorRuleBuilder<T> Required(string? message = null)
    {
        _validator.RuleForRequired(_property, _propertyName, message);
        return this;
    }

    /// <summary>
    /// Adds length rule
    /// </summary>
    public FluentValidatorRuleBuilder<T> Length(int min, int max, string? message = null)
    {
        if (_property is Func<T, string?> stringProperty)
        {
            _validator.RuleForLength(stringProperty, _propertyName, min, max, message);
        }
        return this;
    }

    /// <summary>
    /// Adds email rule
    /// </summary>
    public FluentValidatorRuleBuilder<T> Email(string? message = null)
    {
        if (_property is Func<T, string?> stringProperty)
        {
            _validator.RuleForEmail(stringProperty, _propertyName, message);
        }
        return this;
    }

    /// <summary>
    /// Adds pattern rule
    /// </summary>
    public FluentValidatorRuleBuilder<T> Pattern(string regex, string? message = null)
    {
        if (_property is Func<T, string?> stringProperty)
        {
            _validator.RuleForPattern(stringProperty, _propertyName, regex, message);
        }
        return this;
    }

    /// <summary>
    /// Adds custom validation rule
    /// </summary>
    public FluentValidatorRuleBuilder<T> Custom(Func<T, bool> validation, string message)
    {
        _validator.RuleForCustom(validation, _propertyName, message);
        return this;
    }
}
