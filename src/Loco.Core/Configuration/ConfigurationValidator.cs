using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Loco.Core.Exceptions;

namespace Loco.Core.Configuration;

/// <summary>
/// Validates LocoConfig settings and constraints
/// Extracted from LocoConfig to follow Single Responsibility Principle
/// </summary>
public class ConfigurationValidator
{
    /// <summary>
    /// Validates configuration settings and throws if invalid
    /// </summary>
    public void ValidateConfiguration(LocoConfig config)
    {
        var validationErrors = new List<string>();

        ValidateNumericRanges(config, validationErrors);
        ValidateDirectories(config, validationErrors);
        ValidatePathConflicts(config, validationErrors);
        ValidateLogLevel(config, validationErrors);

        if (validationErrors.Count > 0)
        {
            var errorMessage = $"Configuration validation failed: {string.Join("; ", validationErrors)}";
            throw new LocoConfigurationException(errorMessage);
        }
    }

    /// <summary>
    /// Validates numeric property ranges
    /// </summary>
    private void ValidateNumericRanges(LocoConfig config, List<string> errors)
    {
        if (config.MaxConcurrentFlows < 1 || config.MaxConcurrentFlows > 1000)
            errors.Add("MaxConcurrentFlows must be between 1 and 1000");

        if (config.MemoryLimitMB < 64 || config.MemoryLimitMB > 8192)
            errors.Add("MemoryLimitMB must be between 64 and 8192");

        if (config.CacheSizeMB < 16 || config.CacheSizeMB > 1024)
            errors.Add("CacheSizeMB must be between 16 and 1024");

        if (config.LogRetentionDays < 1 || config.LogRetentionDays > 365)
            errors.Add("LogRetentionDays must be between 1 and 365");

        if (config.DefaultTimeoutSeconds < 1 || config.DefaultTimeoutSeconds > 3600)
            errors.Add("DefaultTimeoutSeconds must be between 1 and 3600");

        if (config.RateLimitPerMinute < 1 || config.RateLimitPerMinute > 10000)
            errors.Add("RateLimitPerMinute must be between 1 and 10000");

        if (config.HealthCheckIntervalSeconds < 10 || config.HealthCheckIntervalSeconds > 3600)
            errors.Add("HealthCheckIntervalSeconds must be between 10 and 3600");

        if (config.CircuitBreakerThreshold < 1 || config.CircuitBreakerThreshold > 100)
            errors.Add("CircuitBreakerThreshold must be between 1 and 100");

        if (config.CircuitBreakerTimeoutSeconds < 10 || config.CircuitBreakerTimeoutSeconds > 3600)
            errors.Add("CircuitBreakerTimeoutSeconds must be between 10 and 3600");

        if (config.CompressionThresholdKB < 1 || config.CompressionThresholdKB > 1048576)
            errors.Add("CompressionThresholdKB must be between 1 and 1048576");

        if (config.AllowedPaths.Length > 32)
            errors.Add("AllowedPaths cannot exceed 32 entries");

        if (config.ForbiddenPaths.Length > 32)
            errors.Add("ForbiddenPaths cannot exceed 32 entries");

        if (config.MaxSecurityEventsPerMinute < 1 || config.MaxSecurityEventsPerMinute > 100000)
            errors.Add("MaxSecurityEventsPerMinute must be between 1 and 100000");

        if (config.SecurityLogRetentionDays < 1 || config.SecurityLogRetentionDays > 365)
            errors.Add("SecurityLogRetentionDays must be between 1 and 365");

        if (config.MaxProcessMemoryMB < 32 || config.MaxProcessMemoryMB > 8192)
            errors.Add("MaxProcessMemoryMB must be between 32 and 8192");
    }

    /// <summary>
    /// Validates and creates required directories
    /// </summary>
    private void ValidateDirectories(LocoConfig config, List<string> errors)
    {
        ValidateSingleDirectory(config.WorkingDirectory, "WorkingDirectory", config, errors, createIfMissing: true);
        ValidateSingleDirectory(config.CacheDirectory, "CacheDirectory", config, errors, createIfMissing: true);
        ValidateSingleDirectory(config.LogDirectory, "LogDirectory", config, errors, createIfMissing: true);
        ValidateSingleDirectory(config.SecurityLogDirectory, "SecurityLogDirectory", config, errors, createIfMissing: true);
    }

    /// <summary>
    /// Validates a single directory
    /// </summary>
    private void ValidateSingleDirectory(string path, string propertyName, LocoConfig config, List<string> errors, bool createIfMissing = false)
    {
        if (string.IsNullOrEmpty(path))
            return;

        if (!LocoConfig.IsSafePath(path))
        {
            errors.Add($"{propertyName} '{path}' is not allowed.");
            return;
        }

        if (!Directory.Exists(path))
        {
            if (createIfMissing)
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to create {propertyName}: {path} - {ex.Message}");
                }
            }
            else
            {
                errors.Add($"{propertyName} directory does not exist: {path}");
            }
        }
    }

    /// <summary>
    /// Validates for path conflicts between allowed and forbidden paths
    /// </summary>
    private void ValidatePathConflicts(LocoConfig config, List<string> errors)
    {
        var normalizedForbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var forbidden in config.ForbiddenPaths)
        {
            if (string.IsNullOrWhiteSpace(forbidden))
                continue;

            if (!LocoConfig.IsSafePath(forbidden))
            {
                errors.Add($"Forbidden path '{forbidden}' is not allowed.");
                continue;
            }

            normalizedForbidden.Add(NormalizeForComparison(forbidden));
        }

        foreach (var allowed in config.AllowedPaths)
        {
            if (string.IsNullOrWhiteSpace(allowed))
                continue;

            if (!LocoConfig.IsSafePath(allowed))
            {
                errors.Add($"Allowed path '{allowed}' is not allowed.");
                continue;
            }

            var normalizedAllowed = NormalizeForComparison(allowed);
            foreach (var forbidden in normalizedForbidden)
            {
                if (IsSameOrChildPath(normalizedAllowed, forbidden))
                {
                    errors.Add($"Allowed path '{allowed}' conflicts with forbidden path '{forbidden}'.");
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Validates log level is from accepted set
    /// </summary>
    private void ValidateLogLevel(LocoConfig config, List<string> errors)
    {
        var validLogLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
        if (!validLogLevels.Contains(config.LogLevel))
        {
            errors.Add($"LogLevel must be one of: {string.Join(", ", validLogLevels)}");
        }
    }

    /// <summary>
    /// Normalizes path for comparison purposes
    /// </summary>
    private string NormalizeForComparison(string path)
    {
        return Path.GetFullPath(path).ToLowerInvariant().TrimEnd(Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Checks if one path is same as or child of another
    /// </summary>
    private bool IsSameOrChildPath(string childPath, string parentPath)
    {
        return childPath == parentPath || childPath.StartsWith(parentPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
