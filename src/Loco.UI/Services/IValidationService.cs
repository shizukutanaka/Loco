using System.Collections.Generic;

namespace Loco.UI.Services;

/// <summary>
/// Service for validating user inputs in the UI
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validates that a string is not null or empty
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="fieldName">The name of the field being validated</param>
    /// <returns>Validation result</returns>
    ValidationResult ValidateRequired(string value, string fieldName);
    
    /// <summary>
    /// Validates that a string matches a specific pattern
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="pattern">The regex pattern to match</param>
    /// <param name="fieldName">The name of the field being validated</param>
    /// <returns>Validation result</returns>
    ValidationResult ValidatePattern(string value, string pattern, string fieldName);
    
    /// <summary>
    /// Validates that a number is within a specified range
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="min">Minimum allowed value</param>
    /// <param name="max">Maximum allowed value</param>
    /// <param name="fieldName">The name of the field being validated</param>
    /// <returns>Validation result</returns>
    ValidationResult ValidateRange(int value, int min, int max, string fieldName);
    
    /// <summary>
    /// Validates that a number is within a specified range
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="min">Minimum allowed value</param>
    /// <param name="max">Maximum allowed value</param>
    /// <param name="fieldName">The name of the field being validated</param>
    /// <returns>Validation result</returns>
    ValidationResult ValidateRange(double value, double min, double max, string fieldName);
    
    /// <summary>
    /// Combines multiple validation results
    /// </summary>
    /// <param name="results">Validation results to combine</param>
    /// <returns>Combined validation result</returns>
    ValidationResult Combine(IEnumerable<ValidationResult> results);
}

/// <summary>
/// Represents the result of a validation operation
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the validation passed
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// Error message if validation failed
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    /// <returns>Successful validation result</returns>
    public static ValidationResult Success() => new ValidationResult { IsValid = true };
    
    /// <summary>
    /// Creates a failed validation result
    /// </summary>
    /// <param name="errorMessage">Error message</param>
    /// <returns>Failed validation result</returns>
    public static ValidationResult Failure(string errorMessage) => new ValidationResult { IsValid = false, ErrorMessage = errorMessage };
}
