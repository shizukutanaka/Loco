using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Configuration;

namespace Loco.Core.Validation
{
    /// <summary>
    /// 設定ファイルのバリデーションを行うユーティリティ
    /// </summary>
    public static class ConfigValidator
    {
        private static readonly Dictionary<string, Func<LocoConfig, ValidationResult>> Validators = new(StringComparer.OrdinalIgnoreCase)
        {
            { "MaxConcurrentFlows", config => ValidateMaxConcurrentFlows(config.MaxConcurrentFlows) },
            { "DefaultTimeoutSeconds", config => ValidateDefaultTimeoutSeconds(config.DefaultTimeoutSeconds) },
            { "DefaultRetryCount", config => ValidateDefaultRetryCount(config.DefaultRetryCount) },
            { "MemoryLimitMB", config => ValidateMemoryLimitMB(config.MemoryLimitMB) },
            { "CacheSizeMB", config => ValidateCacheSizeMB(config.CacheSizeMB) },
            { "LogLevel", config => ValidateLogLevel(config.LogLevel) },
            { "LogRetentionDays", config => ValidateLogRetentionDays(config.LogRetentionDays) },
            { "RateLimitPerMinute", config => ValidateRateLimitPerMinute(config.RateLimitPerMinute) },
            { "HealthCheckIntervalSeconds", config => ValidateHealthCheckIntervalSeconds(config.HealthCheckIntervalSeconds) },
            { "CircuitBreakerThreshold", config => ValidateCircuitBreakerThreshold(config.CircuitBreakerThreshold) },
            { "CircuitBreakerTimeoutSeconds", config => ValidateCircuitBreakerTimeoutSeconds(config.CircuitBreakerTimeoutSeconds) },
            { "CompressionThresholdKB", config => ValidateCompressionThresholdKB(config.CompressionThresholdKB) },
            { "AllowedPaths", config => ValidateAllowedPaths(config.AllowedPaths) },
            { "ForbiddenPaths", config => ValidateForbiddenPaths(config.ForbiddenPaths) },
            { "MaxFileSizeBytes", config => ValidateMaxFileSizeBytes(config.MaxFileSizeBytes) },
            { "LlmMaxTokens", config => ValidateLlmMaxTokens(config.LlmMaxTokens) },
            { "LlmTemperature", config => ValidateLlmTemperature(config.LlmTemperature) },
            { "LlmHttpTimeoutMs", config => ValidateLlmHttpTimeoutMs(config.LlmHttpTimeoutMs) },
            { "MaxSecurityEventsPerMinute", config => ValidateMaxSecurityEventsPerMinute(config.MaxSecurityEventsPerMinute) },
            { "MaxProcessMemoryMB", config => ValidateMaxProcessMemoryMB(config.MaxProcessMemoryMB) },
            { "MaxProcessExecutionTime", config => ValidateMaxProcessExecutionTime(config.MaxProcessExecutionTime) }
        };

        /// <summary>
        /// 設定をバリデーション
        /// </summary>
        public static ValidationResult Validate(LocoConfig config)
        {
            var results = new List<ValidationResult>();

            foreach (var validator in Validators)
            {
                var result = validator.Value(config);
                results.Add(result);
            }

            return new ValidationResult
            {
                IsValid = results.All(r => r.IsValid),
                Errors = results.SelectMany(r => r.Errors).ToList(),
                Warnings = results.SelectMany(r => r.Warnings).ToList()
            };
        }

        /// <summary>
        /// 非同期で設定をバリデーション（パフォーマンス最適化）
        /// </summary>
        public static async Task<ValidationResult> ValidateAsync(LocoConfig config, CancellationToken cancellationToken = default)
        {
            // Parallel validation for better performance
            var validationTasks = Validators.Select(async kvp =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await Task.Run(() => kvp.Value(config), cancellationToken);
            });

            var results = await Task.WhenAll(validationTasks);

            return new ValidationResult
            {
                IsValid = results.All(r => r.IsValid),
                Errors = results.SelectMany(r => r.Errors).ToList(),
                Warnings = results.SelectMany(r => r.Warnings).ToList()
            };
        }

        /// <summary>
        /// 高速バリデーション（警告を無視してエラーのみチェック）
        /// </summary>
        public static ValidationResult ValidateFast(LocoConfig config)
        {
            var errors = new List<string>();

            foreach (var validator in Validators)
            {
                var result = validator.Value(config);
                if (!result.IsValid)
                {
                    errors.AddRange(result.Errors);
                }
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = new List<string>() // Fast mode ignores warnings
            };
        }

        /// <summary>
        /// 設定の健全性チェック（重要な設定のみ）
        /// </summary>
        public static ValidationResult ValidateHealth(LocoConfig config)
        {
            var criticalValidators = new Dictionary<string, Func<LocoConfig, ValidationResult>>
            {
                { "MaxConcurrentFlows", config => ValidateMaxConcurrentFlows(config.MaxConcurrentFlows) },
                { "MemoryLimitMB", config => ValidateMemoryLimitMB(config.MemoryLimitMB) },
                { "LogLevel", config => ValidateLogLevel(config.LogLevel) },
                { "AllowedPaths", config => ValidateAllowedPaths(config.AllowedPaths) },
                { "ForbiddenPaths", config => ValidateForbiddenPaths(config.ForbiddenPaths) }
            };

            var errors = new List<string>();
            var warnings = new List<string>();

            foreach (var validator in criticalValidators)
            {
                var result = validator.Value(config);
                errors.AddRange(result.Errors);
                warnings.AddRange(result.Warnings);
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateMaxConcurrentFlows(int value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value < 1)
            {
                errors.Add("MaxConcurrentFlows must be at least 1");
            }
            else if (value > 100)
            {
                warnings.Add("MaxConcurrentFlows is very high (>100), may cause performance issues");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateDefaultTimeoutSeconds(int value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value < 1)
            {
                errors.Add("DefaultTimeoutSeconds must be at least 1");
            }
            else if (value > 3600)
            {
                warnings.Add("DefaultTimeoutSeconds is very high (>3600s), may cause long waits");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateDefaultRetryCount(int value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value < 0)
            {
                errors.Add("DefaultRetryCount cannot be negative");
            }
            else if (value > 10)
            {
                warnings.Add("DefaultRetryCount is high (>10), may cause long execution times");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateMemoryLimitMB(int value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value < 64)
            {
                errors.Add("MemoryLimitMB must be at least 64MB");
            }
            else if (value > 8192)
            {
                warnings.Add("MemoryLimitMB is very high (>8192MB), may cause system instability");
            }
            else if (value > 2048)
            {
                warnings.Add("MemoryLimitMB is high (>2048MB), consider reducing for better performance");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateCacheSizeMB(int value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value < 16)
            {
                errors.Add("CacheSizeMB must be at least 16MB");
            }
            else if (value > 1024)
            {
                warnings.Add("CacheSizeMB is very high (>1024MB), may impact memory usage");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateLogLevel(string value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            var validLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
            if (!validLevels.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"LogLevel must be one of: {string.Join(", ", validLevels)}");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateLogRetentionDays(int value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value < 1)
            {
                errors.Add("LogRetentionDays must be at least 1");
            }
            else if (value > 365)
            {
                warnings.Add("LogRetentionDays is very high (>365), may consume significant disk space");
            }
            else if (value > 90)
            {
                warnings.Add("LogRetentionDays is high (>90), consider reducing for better disk usage");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateRateLimitPerMinute(int value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value < 1)
            {
                errors.Add("RateLimitPerMinute must be at least 1");
            }
            else if (value > 10000)
            {
                warnings.Add("RateLimitPerMinute is very high (>10000), may impact performance");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateHealthCheckIntervalSeconds(int value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value < 10)
            {
                errors.Add("HealthCheckIntervalSeconds must be at least 10");
            }
            else if (value > 3600)
            {
                warnings.Add("HealthCheckIntervalSeconds is very high (>3600s), health checks may be too infrequent");
            }
            else if (value < 30)
            {
                warnings.Add("HealthCheckIntervalSeconds is low (<30s), may impact performance");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateCircuitBreakerThreshold(int value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value < 1)
            {
                errors.Add("CircuitBreakerThreshold must be at least 1");
            }
            else if (value > 100)
            {
                warnings.Add("CircuitBreakerThreshold is high (>100), circuit may rarely open");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateCircuitBreakerTimeoutSeconds(int value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value < 10)
            {
                errors.Add("CircuitBreakerTimeoutSeconds must be at least 10");
            }
            else if (value > 3600)
            {
                warnings.Add("CircuitBreakerTimeoutSeconds is very high (>3600s), recovery may be slow");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateCompressionThresholdKB(int value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value < 1)
            {
                errors.Add("CompressionThresholdKB must be at least 1KB");
            }
            else if (value > 1048576)
            {
                warnings.Add("CompressionThresholdKB is very high (>1GB), compression may be ineffective");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateAllowedPaths(string[] paths)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (paths.Length > 32)
            {
                warnings.Add("AllowedPaths contains many entries (>32), may impact performance");
            }

            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    errors.Add("AllowedPaths contains empty or whitespace-only entries");
                    continue;
                }

                if (path.Length > 260)
                {
                    warnings.Add($"AllowedPaths entry is very long (>260 chars): {path[..50]}...");
                }

                // Check for dangerous patterns
                if (path.Contains("..") || Path.IsPathRooted(path) && !IsSafeRootedPath(path))
                {
                    errors.Add($"AllowedPaths contains potentially unsafe path: {path}");
                }
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateForbiddenPaths(string[] paths)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (paths.Length > 32)
            {
                warnings.Add("ForbiddenPaths contains many entries (>32), may impact performance");
            }

            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    errors.Add("ForbiddenPaths contains empty or whitespace-only entries");
                    continue;
                }

                if (path.Length > 260)
                {
                    warnings.Add($"ForbiddenPaths entry is very long (>260 chars): {path[..50]}...");
                }
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateMaxFileSizeBytes(long value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value < 1024) // 1KB
            {
                errors.Add("MaxFileSizeBytes must be at least 1024 bytes (1KB)");
            }
            else if (value > 1073741824) // 1GB
            {
                warnings.Add("MaxFileSizeBytes is very high (>1GB), may impact memory usage");
            }
            else if (value > 104857600) // 100MB
            {
                warnings.Add("MaxFileSizeBytes is high (>100MB), consider reducing for better performance");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateLlmMaxTokens(int? value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value.HasValue)
            {
                if (value.Value < 1)
                {
                    errors.Add("LlmMaxTokens must be at least 1");
                }
                else if (value.Value > 32768)
                {
                    warnings.Add("LlmMaxTokens is very high (>32768), may impact API costs and performance");
                }
                else if (value.Value > 4096)
                {
                    warnings.Add("LlmMaxTokens is high (>4096), consider reducing for cost optimization");
                }
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateLlmTemperature(double? value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value.HasValue)
            {
                if (value.Value < 0.0 || value.Value > 2.0)
                {
                    errors.Add("LlmTemperature must be between 0.0 and 2.0");
                }
                else if (value.Value > 1.5)
                {
                    warnings.Add("LlmTemperature is high (>1.5), responses may be very random");
                }
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateLlmHttpTimeoutMs(int? value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value.HasValue)
            {
                if (value.Value < 1000)
                {
                    errors.Add("LlmHttpTimeoutMs must be at least 1000ms (1 second)");
                }
                else if (value.Value > 300000)
                {
                    warnings.Add("LlmHttpTimeoutMs is very high (>300s), may cause long waits");
                }
                else if (value.Value > 60000)
                {
                    warnings.Add("LlmHttpTimeoutMs is high (>60s), consider reducing for better responsiveness");
                }
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateMaxSecurityEventsPerMinute(int value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value < 10)
            {
                errors.Add("MaxSecurityEventsPerMinute must be at least 10");
            }
            else if (value > 1000)
            {
                warnings.Add("MaxSecurityEventsPerMinute is high (>1000), may generate excessive logs");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateMaxProcessMemoryMB(long value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value < 32)
            {
                errors.Add("MaxProcessMemoryMB must be at least 32MB");
            }
            else if (value > 4096)
            {
                warnings.Add("MaxProcessMemoryMB is very high (>4096MB), may impact system stability");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static ValidationResult ValidateMaxProcessExecutionTime(TimeSpan value)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (value < TimeSpan.FromSeconds(1))
            {
                errors.Add("MaxProcessExecutionTime must be at least 1 second");
            }
            else if (value > TimeSpan.FromHours(1))
            {
                warnings.Add("MaxProcessExecutionTime is very high (>1 hour), may cause resource issues");
            }
            else if (value > TimeSpan.FromMinutes(30))
            {
                warnings.Add("MaxProcessExecutionTime is high (>30 minutes), consider reducing");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        private static bool IsSafeRootedPath(string path)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                // Basic safety check - avoid system directories
                var systemDirs = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows)
                };

                foreach (var systemDir in systemDirs)
                {
                    if (!string.IsNullOrEmpty(systemDir) &&
                        fullPath.StartsWith(systemDir, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// バリデーション結果
        /// </summary>
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new();
            public List<string> Warnings { get; set; } = new();
        }
    }
}
