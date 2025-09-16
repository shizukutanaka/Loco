using System;
using System.Collections.Generic;
using System.Linq;

namespace Loco.Core.Models;

public class ValidationResult
{
    private readonly List<ValidationError> _errors = new();
    private readonly List<ValidationWarning> _warnings = new();

    public bool IsValid => !_errors.Any();
    public IReadOnlyList<ValidationError> Errors => _errors;
    public IReadOnlyList<ValidationWarning> Warnings => _warnings;

    public void AddError(string field, string message)
    {
        _errors.Add(new ValidationError { Field = field, Message = message });
    }

    public void AddWarning(string field, string message)
    {
        _warnings.Add(new ValidationWarning { Field = field, Message = message });
    }

    public void MergeResult(ValidationResult other, string prefix = null)
    {
        foreach (var error in other.Errors)
        {
            var field = string.IsNullOrEmpty(prefix) ? error.Field : $"{prefix}.{error.Field}";
            AddError(field, error.Message);
        }

        foreach (var warning in other.Warnings)
        {
            var field = string.IsNullOrEmpty(prefix) ? warning.Field : $"{prefix}.{warning.Field}";
            AddWarning(field, warning.Message);
        }
    }

    public static ValidationResult Success() => new();

    public static ValidationResult Failure(string field, string message)
    {
        var result = new ValidationResult();
        result.AddError(field, message);
        return result;
    }

    public static ValidationResult Failure(params ValidationError[] errors)
    {
        var result = new ValidationResult();
        foreach (var error in errors)
        {
            result._errors.Add(error);
        }
        return result;
    }

    public override string ToString()
    {
        var messages = new List<string>();

        if (_errors.Any())
        {
            messages.Add($"Errors ({_errors.Count}):");
            messages.AddRange(_errors.Select(e => $"  - {e.Field}: {e.Message}"));
        }

        if (_warnings.Any())
        {
            messages.Add($"Warnings ({_warnings.Count}):");
            messages.AddRange(_warnings.Select(w => $"  - {w.Field}: {w.Message}"));
        }

        return string.Join(Environment.NewLine, messages);
    }
}

public class ValidationError
{
    public string Field { get; set; }
    public string Message { get; set; }
}

public class ValidationWarning
{
    public string Field { get; set; }
    public string Message { get; set; }
}