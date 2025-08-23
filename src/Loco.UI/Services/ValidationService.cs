using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Loco.UI.Services;

/// <summary>
/// Implementation of IValidationService for validating user inputs
/// </summary>
public class ValidationService : IValidationService
{
    /// <summary>
    /// Validates that a string is not null or empty
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="fieldName">The name of the field being validated</param>
    /// <returns>Validation result</returns>
    public ValidationResult ValidateRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Failure($"{fieldName} is required");
        }
        
        return ValidationResult.Success();
    }
    
    /// <summary>
    /// Validates that a string matches a specific pattern
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="pattern">The regex pattern to match</param>
    /// <param name="fieldName">The name of the field being validated</param>
    /// <returns>Validation result</returns>
    public ValidationResult ValidatePattern(string value, string pattern, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Success(); // Let required validation handle this
        }
        
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return ValidationResult.Success(); // No pattern to validate against
        }
        
        try
        {
            var regex = new Regex(pattern);
            if (!regex.IsMatch(value))
            {
                return ValidationResult.Failure($"{fieldName} format is invalid");
            }
            
            return ValidationResult.Success();
        }
        catch (ArgumentException)
        {
            return ValidationResult.Failure($"Invalid validation pattern for {fieldName}");
        }
    }
    
    /// <summary>
    /// Validates that a number is within a specified range
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="min">Minimum allowed value</param>
    /// <param name="max">Maximum allowed value</param>
    /// <param name="fieldName">The name of the field being validated</param>
    /// <returns>Validation result</returns>
    public ValidationResult ValidateRange(int value, int min, int max, string fieldName)
    {
        if (value < min || value > max)
        {
            return ValidationResult.Failure($"{fieldName} must be between {min} and {max}");
        }
        
        return ValidationResult.Success();
    }
    
    /// <summary>
    /// Validates that a number is within a specified range
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="min">Minimum allowed value</param>
    /// <param name="max">Maximum allowed value</param>
    /// <param name="fieldName">The name of the field being validated</param>
    /// <returns>Validation result</returns>
    public ValidationResult ValidateRange(double value, double min, double max, string fieldName)
    {
        if (value < min || value > max)
        {
            return ValidationResult.Failure($"{fieldName} must be between {min} and {max}");
        }
        
        return ValidationResult.Success();
    }
    
    /// <summary>
    /// Combines multiple validation results
    /// </summary>
    /// <param name="results">Validation results to combine</param>
    /// <returns>Combined validation result</returns>
    public ValidationResult Combine(IEnumerable<ValidationResult> results)
    {
        var failedResults = results.Where(r => !r.IsValid).ToList();
        
        if (!failedResults.Any())
        {
            return ValidationResult.Success();
        }
        
        var errorMessages = failedResults.Select(r => r.ErrorMessage);
        return ValidationResult.Failure(string.Join(", ", errorMessages));
    }
}
