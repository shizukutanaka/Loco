// Uncle Bob: "A function should do one thing and do it well"
// John Carmack: "If you aren't measuring, you aren't engineering"

using System.Text.RegularExpressions;

namespace Loco.Core.Practical;

/// <summary>
/// Simple validation - no complex frameworks, just what works
/// Fast, clear error messages, composable rules
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; }
    public List<string> Errors { get; }

    public ValidationResult(bool isValid, params string[] errors)
    {
        IsValid = isValid;
        Errors = errors.ToList();
    }

    public static ValidationResult Success() => new(true);
    public static ValidationResult Failure(params string[] errors) => new(false, errors);

    // Combine multiple validation results
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        var errors = results.SelectMany(r => r.Errors).ToList();
        return errors.Count > 0
            ? new ValidationResult(false, errors.ToArray())
            : Success();
    }
}

/// <summary>
/// Fluent validation builder
/// </summary>
public class Validator<T>
{
    private readonly List<(Func<T, bool> rule, string errorMessage)> _rules = new();

    public Validator<T> Rule(Func<T, bool> predicate, string errorMessage)
    {
        _rules.Add((predicate, errorMessage));
        return this;
    }

    public ValidationResult Validate(T value)
    {
        var errors = new List<string>();

        foreach (var (rule, errorMessage) in _rules)
        {
            try
            {
                if (!rule(value))
                {
                    errors.Add(errorMessage);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Validation error: {ex.Message}");
            }
        }

        return errors.Count > 0
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    // Validate multiple items
    public ValidationResult ValidateAll(IEnumerable<T> values)
    {
        var results = values.Select(Validate).ToArray();
        return ValidationResult.Combine(results);
    }
}

/// <summary>
/// Common validation rules - reusable, tested, fast
/// </summary>
public static class ValidationRules
{
    // String validations
    public static bool NotEmpty(string? value) =>
        !string.IsNullOrWhiteSpace(value);

    public static bool MinLength(string? value, int min) =>
        value?.Length >= min;

    public static bool MaxLength(string? value, int max) =>
        value?.Length <= max;

    public static bool IsEmail(string? value) =>
        value != null && Regex.IsMatch(value,
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            RegexOptions.Compiled);

    public static bool IsUrl(string? value)
    {
        return value != null && Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public static bool IsAlphanumeric(string? value) =>
        value != null && Regex.IsMatch(value, @"^[a-zA-Z0-9]+$", RegexOptions.Compiled);

    // Number validations
    public static bool InRange(int value, int min, int max) =>
        value >= min && value <= max;

    public static bool InRange(double value, double min, double max) =>
        value >= min && value <= max;

    public static bool IsPositive(int value) => value > 0;
    public static bool IsPositive(double value) => value > 0;

    public static bool IsNonNegative(int value) => value >= 0;
    public static bool IsNonNegative(double value) => value >= 0;

    // Collection validations
    public static bool NotEmpty<T>(IEnumerable<T>? collection) =>
        collection?.Any() == true;

    public static bool HasMinCount<T>(IEnumerable<T>? collection, int min) =>
        collection?.Count() >= min;

    public static bool HasMaxCount<T>(IEnumerable<T>? collection, int max) =>
        collection?.Count() <= max;

    public static bool AllUnique<T>(IEnumerable<T>? collection) =>
        collection == null || collection.Count() == collection.Distinct().Count();

    // Date validations
    public static bool IsFuture(DateTime date) =>
        date > DateTime.UtcNow;

    public static bool IsPast(DateTime date) =>
        date < DateTime.UtcNow;

    public static bool InDateRange(DateTime date, DateTime min, DateTime max) =>
        date >= min && date <= max;

    // Custom regex validation
    public static bool Matches(string? value, string pattern) =>
        value != null && Regex.IsMatch(value, pattern, RegexOptions.Compiled);
}

/// <summary>
/// Pre-built validators for common scenarios
/// </summary>
public static class CommonValidators
{
    // Email validator
    public static readonly Validator<string> Email = new Validator<string>()
        .Rule(ValidationRules.NotEmpty, "Email is required")
        .Rule(v => ValidationRules.IsEmail(v), "Invalid email format");

    // Password validator (example: min 8 chars, at least 1 number)
    public static readonly Validator<string> Password = new Validator<string>()
        .Rule(ValidationRules.NotEmpty, "Password is required")
        .Rule(v => ValidationRules.MinLength(v, 8), "Password must be at least 8 characters")
        .Rule(v => v?.Any(char.IsDigit) == true, "Password must contain at least one number")
        .Rule(v => v?.Any(char.IsUpper) == true, "Password must contain at least one uppercase letter");

    // Username validator
    public static readonly Validator<string> Username = new Validator<string>()
        .Rule(ValidationRules.NotEmpty, "Username is required")
        .Rule(v => ValidationRules.MinLength(v, 3), "Username must be at least 3 characters")
        .Rule(v => ValidationRules.MaxLength(v, 20), "Username must be at most 20 characters")
        .Rule(v => ValidationRules.IsAlphanumeric(v), "Username must be alphanumeric");

    // URL validator
    public static readonly Validator<string> Url = new Validator<string>()
        .Rule(ValidationRules.NotEmpty, "URL is required")
        .Rule(v => ValidationRules.IsUrl(v), "Invalid URL format");

    // Phone number validator (simple US format)
    public static readonly Validator<string> PhoneNumber = new Validator<string>()
        .Rule(ValidationRules.NotEmpty, "Phone number is required")
        .Rule(v => ValidationRules.Matches(v, @"^\d{3}-\d{3}-\d{4}$"),
            "Phone number must be in format: 123-456-7890");
}

/// <summary>
/// Example: Validate a user registration model
/// </summary>
public class UserRegistrationValidator
{
    private readonly Validator<UserRegistration> _validator;

    public UserRegistrationValidator()
    {
        _validator = new Validator<UserRegistration>()
            .Rule(u => ValidationRules.NotEmpty(u.Username), "Username is required")
            .Rule(u => ValidationRules.MinLength(u.Username, 3), "Username too short")
            .Rule(u => ValidationRules.IsEmail(u.Email), "Invalid email")
            .Rule(u => u.Password == u.ConfirmPassword, "Passwords don't match")
            .Rule(u => u.Age >= 18, "Must be 18 or older")
            .Rule(u => u.AcceptedTerms, "Must accept terms of service");
    }

    public ValidationResult Validate(UserRegistration user) =>
        _validator.Validate(user);
}

public record UserRegistration(
    string Username,
    string Email,
    string Password,
    string ConfirmPassword,
    int Age,
    bool AcceptedTerms);