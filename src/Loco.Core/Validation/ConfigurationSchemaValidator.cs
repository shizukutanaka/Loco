using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Validation;

/// <summary>
/// Comprehensive configuration validator with schema validation
/// Ensures production-grade configuration safety
/// </summary>
public class ConfigurationSchemaValidator
{
    private readonly ILogger? _logger;

    public ConfigurationSchemaValidator(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validate configuration file comprehensively
    /// </summary>
    public ValidationReport ValidateConfiguration(string configPath)
    {
        var report = new ValidationReport { ConfigPath = configPath };

        try
        {
            // Check file exists
            if (!File.Exists(configPath))
            {
                report.AddError("CONFIG_NOT_FOUND", $"Configuration file not found: {configPath}");
                return report;
            }

            // Read and parse JSON
            string json;
            try
            {
                json = File.ReadAllText(configPath);
            }
            catch (Exception ex)
            {
                report.AddError("CONFIG_READ_FAILED", $"Failed to read configuration: {ex.Message}");
                return report;
            }

            JsonDocument? doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                report.AddError("CONFIG_INVALID_JSON", $"Invalid JSON: {ex.Message}");
                return report;
            }

            var root = doc.RootElement;

            // Validate numeric ranges
            ValidateNumericProperty(root, "maxConcurrentFlows", 1, 100, report);
            ValidateNumericProperty(root, "memoryLimitMB", 64, 8192, report);
            ValidateNumericProperty(root, "cacheSizeMB", 16, 2048, report);
            ValidateNumericProperty(root, "defaultTimeoutSeconds", 1, 3600, report);
            ValidateNumericProperty(root, "defaultRetryCount", 0, 10, report);
            ValidateNumericProperty(root, "rateLimitPerMinute", 1, 10000, report);
            ValidateNumericProperty(root, "logRetentionDays", 1, 365, report);
            ValidateNumericProperty(root, "healthCheckIntervalSeconds", 10, 3600, report);

            // Validate string properties
            ValidateStringProperty(root, "workingDirectory", report);
            ValidateStringProperty(root, "logDirectory", report);
            ValidateStringProperty(root, "cacheDirectory", report);

            // Validate log level
            if (root.TryGetProperty("logLevel", out var logLevel))
            {
                var validLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical", "None" };
                if (!validLevels.Contains(logLevel.GetString()))
                {
                    report.AddWarning("INVALID_LOG_LEVEL",
                        $"Invalid log level: {logLevel.GetString()}. Valid values: {string.Join(", ", validLevels)}");
                }
            }

            // Validate paths
            ValidatePathsArray(root, "allowedPaths", report);
            ValidatePathsArray(root, "forbiddenPaths", report);

            // Validate boolean flags
            ValidateBooleanProperty(root, "enableAutoBackup", report);
            ValidateBooleanProperty(root, "enableFileLogging", report);
            ValidateBooleanProperty(root, "enableConsoleLogging", report);
            ValidateBooleanProperty(root, "enableMemoryOptimization", report);
            ValidateBooleanProperty(root, "enableAuditLogging", report);
            ValidateBooleanProperty(root, "enableInputValidation", report);
            ValidateBooleanProperty(root, "enableHealthChecks", report);
            ValidateBooleanProperty(root, "enableMetrics", report);

            // Check for unknown properties
            var knownProperties = new HashSet<string>
            {
                "maxConcurrentFlows", "enableAutoBackup", "workingDirectory", "cacheDirectory",
                "logDirectory", "logLevel", "enableFileLogging", "enableConsoleLogging",
                "logRetentionDays", "memoryLimitMB", "cacheSizeMB", "enableMemoryOptimization",
                "defaultTimeoutSeconds", "defaultRetryCount", "rateLimitPerMinute",
                "allowedPaths", "forbiddenPaths", "maxFileSizeBytes", "enableAuditLogging",
                "enableInputValidation", "enableHealthChecks", "enableMetrics",
                "healthCheckIntervalSeconds"
            };

            foreach (var property in root.EnumerateObject())
            {
                if (!knownProperties.Contains(property.Name))
                {
                    report.AddWarning("UNKNOWN_PROPERTY", $"Unknown configuration property: {property.Name}");
                }
            }

            // Security validation
            if (root.TryGetProperty("enableInputValidation", out var inputVal) &&
                inputVal.ValueKind == JsonValueKind.False)
            {
                report.AddWarning("SECURITY_DISABLED",
                    "Input validation is disabled - this is not recommended for production");
            }

            if (root.TryGetProperty("enableAuditLogging", out var auditVal) &&
                auditVal.ValueKind == JsonValueKind.False)
            {
                report.AddWarning("AUDIT_DISABLED",
                    "Audit logging is disabled - this may not meet compliance requirements");
            }

            _logger?.LogInformation("Configuration validation completed: {Errors} errors, {Warnings} warnings",
                report.Errors.Count, report.Warnings.Count);
        }
        catch (Exception ex)
        {
            report.AddError("VALIDATION_FAILED", $"Validation failed: {ex.Message}");
            _logger?.LogError(ex, "Configuration validation failed");
        }

        return report;
    }

    private void ValidateNumericProperty(JsonElement root, string propertyName, int min, int max, ValidationReport report)
    {
        if (root.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind != JsonValueKind.Number)
            {
                report.AddError("INVALID_TYPE", $"{propertyName} must be a number");
                return;
            }

            var value = prop.GetInt32();
            if (value < min || value > max)
            {
                report.AddError("OUT_OF_RANGE",
                    $"{propertyName} value {value} is out of valid range [{min}, {max}]");
            }
        }
    }

    private void ValidateStringProperty(JsonElement root, string propertyName, ValidationReport report)
    {
        if (root.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind != JsonValueKind.String)
            {
                report.AddError("INVALID_TYPE", $"{propertyName} must be a string");
                return;
            }

            var value = prop.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                report.AddWarning("EMPTY_VALUE", $"{propertyName} is empty");
            }
        }
    }

    private void ValidateBooleanProperty(JsonElement root, string propertyName, ValidationReport report)
    {
        if (root.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind != JsonValueKind.True && prop.ValueKind != JsonValueKind.False)
            {
                report.AddError("INVALID_TYPE", $"{propertyName} must be a boolean (true/false)");
            }
        }
    }

    private void ValidatePathsArray(JsonElement root, string propertyName, ValidationReport report)
    {
        if (root.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind != JsonValueKind.Array)
            {
                report.AddError("INVALID_TYPE", $"{propertyName} must be an array");
                return;
            }

            var index = 0;
            foreach (var item in prop.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    report.AddError("INVALID_ARRAY_ITEM",
                        $"{propertyName}[{index}] must be a string");
                }
                else
                {
                    var path = item.GetString();
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        report.AddWarning("EMPTY_PATH",
                            $"{propertyName}[{index}] is empty");
                    }
                    else if (path.Contains(".."))
                    {
                        report.AddWarning("SUSPICIOUS_PATH",
                            $"{propertyName}[{index}] contains '..' which may be dangerous");
                    }
                }
                index++;
            }
        }
    }
}

/// <summary>
/// Configuration validation report
/// </summary>
public class ValidationReport
{
    public string ConfigPath { get; set; } = string.Empty;
    public List<ValidationIssue> Errors { get; } = new();
    public List<ValidationIssue> Warnings { get; } = new();
    public bool IsValid => Errors.Count == 0;
    public bool HasWarnings => Warnings.Count > 0;

    public void AddError(string code, string message)
    {
        Errors.Add(new ValidationIssue
        {
            Code = code,
            Message = message,
            Severity = IssueSeverity.Error
        });
    }

    public void AddWarning(string code, string message)
    {
        Warnings.Add(new ValidationIssue
        {
            Code = code,
            Message = message,
            Severity = IssueSeverity.Warning
        });
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Validation Report for: {ConfigPath}");
        sb.AppendLine($"Status: {(IsValid ? "✓ VALID" : "✗ INVALID")}");
        sb.AppendLine($"Errors: {Errors.Count}, Warnings: {Warnings.Count}");

        if (Errors.Any())
        {
            sb.AppendLine("\nErrors:");
            foreach (var error in Errors)
            {
                sb.AppendLine($"  [{error.Code}] {error.Message}");
            }
        }

        if (Warnings.Any())
        {
            sb.AppendLine("\nWarnings:");
            foreach (var warning in Warnings)
            {
                sb.AppendLine($"  [{warning.Code}] {warning.Message}");
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// Validation issue
/// </summary>
public class ValidationIssue
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public IssueSeverity Severity { get; set; }
}

/// <summary>
/// Issue severity
/// </summary>
public enum IssueSeverity
{
    Warning,
    Error,
    Critical
}
