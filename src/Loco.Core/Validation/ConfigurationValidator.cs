using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Loco.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Validation
{
    /// <summary>
    /// Validates Loco configuration for production readiness
    /// </summary>
    public class ConfigurationValidator
    {
        private readonly ILogger? _logger;
        private readonly List<ValidationIssue> _issues = new();

        public ConfigurationValidator(ILogger? logger = null)
        {
            _logger = logger;
        }

        public class ValidationIssue
        {
            public ValidationSeverity Severity { get; set; }
            public string Category { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string? Recommendation { get; set; }
        }

        public enum ValidationSeverity
        {
            Information,
            Warning,
            Error,
            Critical
        }

        public ValidationResult Validate(LocoConfig config)
        {
            _issues.Clear();

            ValidatePerformanceSettings(config);
            ValidateSecuritySettings(config);
            ValidateReliabilitySettings(config);
            ValidateDirectorySettings(config);
            ValidateLoggingSettings(config);

            return new ValidationResult
            {
                IsValid = !_issues.Any(i => i.Severity >= ValidationSeverity.Error),
                Issues = _issues.ToList(),
                CriticalCount = _issues.Count(i => i.Severity == ValidationSeverity.Critical),
                ErrorCount = _issues.Count(i => i.Severity == ValidationSeverity.Error),
                WarningCount = _issues.Count(i => i.Severity == ValidationSeverity.Warning)
            };
        }

        private void ValidatePerformanceSettings(LocoConfig config)
        {
            // Max concurrent flows
            if (config.MaxConcurrentFlows < 1)
            {
                AddIssue(ValidationSeverity.Error, "Performance",
                    "MaxConcurrentFlows must be at least 1",
                    "Set MaxConcurrentFlows to a value between 1 and 1000");
            }
            else if (config.MaxConcurrentFlows > 1000)
            {
                AddIssue(ValidationSeverity.Warning, "Performance",
                    "MaxConcurrentFlows exceeds recommended limit (1000)",
                    "Consider reducing to 100 or less for typical workloads");
            }

            // Memory limits
            if (config.MemoryLimitMB < 64)
            {
                AddIssue(ValidationSeverity.Warning, "Performance",
                    "MemoryLimitMB is very low (< 64MB)",
                    "Consider increasing to at least 256MB for stable operation");
            }

            if (config.CacheSizeMB < 16)
            {
                AddIssue(ValidationSeverity.Information, "Performance",
                    "CacheSizeMB is low, may impact performance",
                    "Consider increasing to 64MB or more");
            }
        }

        private void ValidateSecuritySettings(LocoConfig config)
        {
            // Input validation
            if (!config.EnableInputValidation)
            {
                AddIssue(ValidationSeverity.Critical, "Security",
                    "Input validation is disabled - major security risk",
                    "Enable EnableInputValidation for production use");
            }

            // Audit logging
            if (!config.EnableAuditLogging)
            {
                AddIssue(ValidationSeverity.Warning, "Security",
                    "Audit logging is disabled",
                    "Enable EnableAuditLogging for production environments");
            }

            // Allowed paths
            if (config.AllowedPaths == null || config.AllowedPaths.Length == 0)
            {
                AddIssue(ValidationSeverity.Warning, "Security",
                    "No AllowedPaths configured - all paths accessible",
                    "Configure AllowedPaths to restrict file system access");
            }

            // Forbidden paths
            if (config.ForbiddenPaths == null || config.ForbiddenPaths.Length == 0)
            {
                AddIssue(ValidationSeverity.Information, "Security",
                    "No ForbiddenPaths configured",
                    "Consider adding system directories to ForbiddenPaths");
            }

            // Rate limiting
            if (config.RateLimitPerMinute <= 0)
            {
                AddIssue(ValidationSeverity.Warning, "Security",
                    "Rate limiting is disabled",
                    "Set RateLimitPerMinute to protect against abuse");
            }
            else if (config.RateLimitPerMinute > 1000)
            {
                AddIssue(ValidationSeverity.Information, "Security",
                    "Rate limit is very high",
                    "Consider reducing for better protection");
            }

            // Max file size
            if (config.MaxFileSizeBytes > 10L * 1024 * 1024 * 1024) // 10GB
            {
                AddIssue(ValidationSeverity.Warning, "Security",
                    "MaxFileSizeBytes exceeds 10GB - potential DoS risk",
                    "Consider reducing to prevent resource exhaustion");
            }
        }

        private void ValidateReliabilitySettings(LocoConfig config)
        {
            // Timeouts
            if (config.DefaultTimeoutSeconds < 1)
            {
                AddIssue(ValidationSeverity.Error, "Reliability",
                    "DefaultTimeoutSeconds must be at least 1",
                    "Set to 30 seconds or more for typical operations");
            }
            else if (config.DefaultTimeoutSeconds > 3600)
            {
                AddIssue(ValidationSeverity.Warning, "Reliability",
                    "DefaultTimeoutSeconds exceeds 1 hour",
                    "Long timeouts may cause hung processes");
            }

            // Retry count
            if (config.DefaultRetryCount < 0)
            {
                AddIssue(ValidationSeverity.Error, "Reliability",
                    "DefaultRetryCount cannot be negative",
                    "Set to 3 for typical retry behavior");
            }
            else if (config.DefaultRetryCount > 10)
            {
                AddIssue(ValidationSeverity.Warning, "Reliability",
                    "DefaultRetryCount is very high",
                    "Excessive retries may mask underlying issues");
            }

            // Circuit breaker
            if (config.EnableCircuitBreaker)
            {
                if (config.CircuitBreakerThreshold < 1)
                {
                    AddIssue(ValidationSeverity.Error, "Reliability",
                        "CircuitBreakerThreshold must be at least 1",
                        "Set to 5 for typical circuit breaker behavior");
                }

                if (config.CircuitBreakerTimeoutSeconds < 10)
                {
                    AddIssue(ValidationSeverity.Warning, "Reliability",
                        "CircuitBreakerTimeoutSeconds is very short",
                        "Consider increasing to 60 seconds or more");
                }
            }
        }

        private void ValidateDirectorySettings(LocoConfig config)
        {
            // Working directory
            ValidateDirectory(config.WorkingDirectory, "WorkingDirectory");

            // Cache directory
            ValidateDirectory(config.CacheDirectory, "CacheDirectory");

            // Log directory
            ValidateDirectory(config.LogDirectory, "LogDirectory");

            // Check path resolution warnings
            if (config.HasPathResolutionWarnings)
            {
                AddIssue(ValidationSeverity.Warning, "Configuration",
                    "Configuration has path resolution warnings",
                    "Run 'Loco.Cli.exe info' to see details");
            }
        }

        private void ValidateDirectory(string path, string name)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                AddIssue(ValidationSeverity.Warning, "Configuration",
                    $"{name} is not configured",
                    $"Set {name} to a valid directory path");
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(path);

                // Check if directory exists or can be created
                if (!Directory.Exists(fullPath))
                {
                    AddIssue(ValidationSeverity.Information, "Configuration",
                        $"{name} does not exist: {fullPath}",
                        "Directory will be created on first use");
                }
            }
            catch (Exception ex)
            {
                AddIssue(ValidationSeverity.Error, "Configuration",
                    $"Invalid {name} path: {ex.Message}",
                    "Provide a valid absolute or relative path");
            }
        }

        private void ValidateLoggingSettings(LocoConfig config)
        {
            // At least one logging output
            if (!config.EnableFileLogging && !config.EnableConsoleLogging)
            {
                AddIssue(ValidationSeverity.Critical, "Logging",
                    "All logging is disabled",
                    "Enable FileLogging or ConsoleLogging for diagnostics");
            }

            // Log retention
            if (config.LogRetentionDays < 1)
            {
                AddIssue(ValidationSeverity.Warning, "Logging",
                    "LogRetentionDays is less than 1",
                    "Set to at least 7 days for troubleshooting");
            }
            else if (config.LogRetentionDays > 365)
            {
                AddIssue(ValidationSeverity.Information, "Logging",
                    "LogRetentionDays exceeds 1 year",
                    "Long retention may consume significant disk space");
            }

            // Health checks
            if (config.EnableHealthChecks && config.HealthCheckIntervalSeconds < 10)
            {
                AddIssue(ValidationSeverity.Warning, "Monitoring",
                    "HealthCheckIntervalSeconds is very short",
                    "Consider 30 seconds or more to reduce overhead");
            }
        }

        private void AddIssue(ValidationSeverity severity, string category, string message, string? recommendation = null)
        {
            var issue = new ValidationIssue
            {
                Severity = severity,
                Category = category,
                Message = message,
                Recommendation = recommendation
            };

            _issues.Add(issue);

            // Log based on severity
            switch (severity)
            {
                case ValidationSeverity.Critical:
                case ValidationSeverity.Error:
                    _logger?.LogError("{Category}: {Message}", category, message);
                    break;
                case ValidationSeverity.Warning:
                    _logger?.LogWarning("{Category}: {Message}", category, message);
                    break;
                case ValidationSeverity.Information:
                    _logger?.LogInformation("{Category}: {Message}", category, message);
                    break;
            }
        }

        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<ValidationIssue> Issues { get; set; } = new();
            public int CriticalCount { get; set; }
            public int ErrorCount { get; set; }
            public int WarningCount { get; set; }

            public string GetSummary()
            {
                if (IsValid && Issues.Count == 0)
                    return "Configuration is valid with no issues.";

                var summary = $"Configuration validation: {(IsValid ? "PASSED" : "FAILED")}";
                if (CriticalCount > 0) summary += $" - {CriticalCount} critical";
                if (ErrorCount > 0) summary += $" - {ErrorCount} errors";
                if (WarningCount > 0) summary += $" - {WarningCount} warnings";

                return summary;
            }
        }
    }
}