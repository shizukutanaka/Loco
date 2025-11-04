using Microsoft.Extensions.Logging;

namespace Loco.Core.Validation;

/// <summary>
/// Validation rule interface
/// </summary>
public interface IValidationRule<T> where T : class
{
    /// <summary>
    /// Validates an object
    /// </summary>
    ValidationResult Validate(T obj);
}

/// <summary>
/// Validation result
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Is valid
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Validation errors
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    /// Has errors
    /// </summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>
    /// Error count
    /// </summary>
    public int ErrorCount => Errors.Count;

    /// <summary>
    /// Gets error messages
    /// </summary>
    public IEnumerable<string> GetErrorMessages() => Errors.Select(e => e.Message);

    /// <summary>
    /// Adds an error
    /// </summary>
    public void AddError(string propertyName, string message, string? code = null)
    {
        IsValid = false;
        Errors.Add(new ValidationError { PropertyName = propertyName, Message = message, Code = code });
    }
}

/// <summary>
/// Validation error
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Property name
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Error code
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Attempted value
    /// </summary>
    public object? AttemptedValue { get; set; }

    /// <summary>
    /// Severity
    /// </summary>
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
}

/// <summary>
/// Validation severity
/// </summary>
public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Validator interface
/// </summary>
public interface IValidator<T> where T : class
{
    /// <summary>
    /// Validates an object
    /// </summary>
    ValidationResult Validate(T obj);

    /// <summary>
    /// Validates asynchronously
    /// </summary>
    Task<ValidationResult> ValidateAsync(T obj, CancellationToken cancellationToken = default);
}

/// <summary>
/// Abstract validator base class
/// </summary>
public abstract class AbstractValidator<T> : IValidator<T> where T : class
{
    protected readonly ILogger<AbstractValidator<T>> Logger;
    protected readonly List<IValidationRule<T>> Rules = new();

    protected AbstractValidator(ILogger<AbstractValidator<T>> logger)
    {
        Logger = logger;
        InitializeRules();
    }

    /// <summary>
    /// Initialize validation rules - override in derived class
    /// </summary>
    protected abstract void InitializeRules();

    /// <summary>
    /// Adds a validation rule
    /// </summary>
    protected void AddRule(IValidationRule<T> rule)
    {
        Rules.Add(rule);
    }

    public ValidationResult Validate(T obj)
    {
        try
        {
            var result = new ValidationResult();

            foreach (var rule in Rules)
            {
                var ruleResult = rule.Validate(obj);
                if (!ruleResult.IsValid)
                {
                    result.Errors.AddRange(ruleResult.Errors);
                    result.IsValid = false;
                }
            }

            if (!result.IsValid)
            {
                Logger.LogWarning("Validation failed for type {Type}: {ErrorCount} errors",
                    typeof(T).Name, result.ErrorCount);
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Validation error for type {Type}", typeof(T).Name);
            throw;
        }
    }

    public async Task<ValidationResult> ValidateAsync(T obj, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(Validate(obj));
    }

    /// <summary>
    /// Adds a required rule
    /// </summary>
    protected void RuleForRequired(Func<T, object?> property, string propertyName, string? message = null)
    {
        AddRule(new RequiredRule<T>(property, propertyName, message));
    }

    /// <summary>
    /// Adds a length rule
    /// </summary>
    protected void RuleForLength(Func<T, string?> property, string propertyName, int minLength, int maxLength, string? message = null)
    {
        AddRule(new LengthRule<T>(property, propertyName, minLength, maxLength, message));
    }

    /// <summary>
    /// Adds an email rule
    /// </summary>
    protected void RuleForEmail(Func<T, string?> property, string propertyName, string? message = null)
    {
        AddRule(new EmailRule<T>(property, propertyName, message));
    }

    /// <summary>
    /// Adds a regex rule
    /// </summary>
    protected void RuleForPattern(Func<T, string?> property, string propertyName, string pattern, string? message = null)
    {
        AddRule(new PatternRule<T>(property, propertyName, pattern, message));
    }

    /// <summary>
    /// Adds a custom rule
    /// </summary>
    protected void RuleForCustom(Func<T, bool> validation, string propertyName, string message)
    {
        AddRule(new CustomRule<T>(validation, propertyName, message));
    }
}

/// <summary>
/// Required validation rule
/// </summary>
public class RequiredRule<T> : IValidationRule<T> where T : class
{
    private readonly Func<T, object?> _property;
    private readonly string _propertyName;
    private readonly string _message;

    public RequiredRule(Func<T, object?> property, string propertyName, string? message = null)
    {
        _property = property;
        _propertyName = propertyName;
        _message = message ?? $"{propertyName} is required";
    }

    public ValidationResult Validate(T obj)
    {
        var result = new ValidationResult();
        var value = _property(obj);

        if (value == null || (value is string str && string.IsNullOrWhiteSpace(str)))
        {
            result.AddError(_propertyName, _message, "REQUIRED");
        }

        return result;
    }
}

/// <summary>
/// Length validation rule
/// </summary>
public class LengthRule<T> : IValidationRule<T> where T : class
{
    private readonly Func<T, string?> _property;
    private readonly string _propertyName;
    private readonly int _minLength;
    private readonly int _maxLength;
    private readonly string _message;

    public LengthRule(Func<T, string?> property, string propertyName, int minLength, int maxLength, string? message = null)
    {
        _property = property;
        _propertyName = propertyName;
        _minLength = minLength;
        _maxLength = maxLength;
        _message = message ?? $"{propertyName} must be between {minLength} and {maxLength} characters";
    }

    public ValidationResult Validate(T obj)
    {
        var result = new ValidationResult();
        var value = _property(obj);

        if (value != null && (value.Length < _minLength || value.Length > _maxLength))
        {
            result.AddError(_propertyName, _message, "LENGTH");
        }

        return result;
    }
}

/// <summary>
/// Email validation rule
/// </summary>
public class EmailRule<T> : IValidationRule<T> where T : class
{
    private readonly Func<T, string?> _property;
    private readonly string _propertyName;
    private readonly string _message;

    public EmailRule(Func<T, string?> property, string propertyName, string? message = null)
    {
        _property = property;
        _propertyName = propertyName;
        _message = message ?? $"{propertyName} is not a valid email";
    }

    public ValidationResult Validate(T obj)
    {
        var result = new ValidationResult();
        var value = _property(obj);

        if (!string.IsNullOrEmpty(value))
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(value);
                if (addr.Address != value)
                {
                    result.AddError(_propertyName, _message, "INVALID_EMAIL");
                }
            }
            catch
            {
                result.AddError(_propertyName, _message, "INVALID_EMAIL");
            }
        }

        return result;
    }
}

/// <summary>
/// Pattern validation rule
/// </summary>
public class PatternRule<T> : IValidationRule<T> where T : class
{
    private readonly Func<T, string?> _property;
    private readonly string _propertyName;
    private readonly System.Text.RegularExpressions.Regex _regex;
    private readonly string _message;

    public PatternRule(Func<T, string?> property, string propertyName, string pattern, string? message = null)
    {
        _property = property;
        _propertyName = propertyName;
        _regex = new System.Text.RegularExpressions.Regex(pattern);
        _message = message ?? $"{propertyName} format is invalid";
    }

    public ValidationResult Validate(T obj)
    {
        var result = new ValidationResult();
        var value = _property(obj);

        if (!string.IsNullOrEmpty(value) && !_regex.IsMatch(value))
        {
            result.AddError(_propertyName, _message, "PATTERN");
        }

        return result;
    }
}

/// <summary>
/// Custom validation rule
/// </summary>
public class CustomRule<T> : IValidationRule<T> where T : class
{
    private readonly Func<T, bool> _validation;
    private readonly string _propertyName;
    private readonly string _message;

    public CustomRule(Func<T, bool> validation, string propertyName, string message)
    {
        _validation = validation;
        _propertyName = propertyName;
        _message = message;
    }

    public ValidationResult Validate(T obj)
    {
        var result = new ValidationResult();

        if (!_validation(obj))
        {
            result.AddError(_propertyName, _message, "CUSTOM");
        }

        return result;
    }
}

/// <summary>
/// Validator factory
/// </summary>
public interface IValidatorFactory
{
    /// <summary>
    /// Gets a validator for a type
    /// </summary>
    IValidator<T>? GetValidator<T>() where T : class;

    /// <summary>
    /// Registers a validator
    /// </summary>
    void RegisterValidator<T>(IValidator<T> validator) where T : class;
}

/// <summary>
/// In-memory validator factory
/// </summary>
public class ValidatorFactory : IValidatorFactory
{
    private readonly Dictionary<Type, object> _validators = new();
    private readonly ILogger<ValidatorFactory> _logger;

    public ValidatorFactory(ILogger<ValidatorFactory> logger)
    {
        _logger = logger;
    }

    public IValidator<T>? GetValidator<T>() where T : class
    {
        var key = typeof(T);
        if (_validators.TryGetValue(key, out var validator))
        {
            return validator as IValidator<T>;
        }

        _logger.LogDebug("No validator registered for type {Type}", key.Name);
        return null;
    }

    public void RegisterValidator<T>(IValidator<T> validator) where T : class
    {
        _validators[typeof(T)] = validator;
        _logger.LogInformation("Validator registered for type {Type}", typeof(T).Name);
    }
}

/// <summary>
/// Validation middleware for ASP.NET Core
/// </summary>
public class ValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidationMiddleware> _logger;

    public ValidationMiddleware(RequestDelegate next, ILogger<ValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Store validator factory in context for use in controllers
        context.Items["ValidatorFactory"] = context.RequestServices.GetService<IValidatorFactory>();
        await _next(context);
    }
}
