using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Loco.Core.Exceptions;
using Loco.Core.Security;

namespace Loco.Core.Configuration
{
    /// <summary>
    /// Simple configuration for Loco - John Carmack style: direct, no nonsense
    /// </summary>
    public class LocoConfig
    {
        private static readonly JsonSerializerOptions ConfigSerializerOptions = new()
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private readonly List<string> _pathResolutionWarnings = new();

        // Direct environment variable for config path
        public const string ConfigPathEnvVar = "LOCO_CONFIG_PATH";

        // Core settings - direct properties, no complex hierarchies
        public int MaxConcurrentFlows { get; set; } = 5;
        public bool EnableAutoBackup { get; set; } = false;
        public string WorkingDirectory { get; set; } = Environment.CurrentDirectory;
        public string CacheDirectory { get; set; } = GetDefaultCacheDir();
        public string LogDirectory { get; set; } = GetDefaultLogDir();
        public string LogLevel { get; set; } = "Information";
        public bool EnableFileLogging { get; set; } = true;
        public bool EnableConsoleLogging { get; set; } = true;
        public int LogRetentionDays { get; set; } = 7;
        public int MemoryLimitMB { get; set; } = 256;
        public int CacheSizeMB { get; set; } = 32;
        public bool EnableMemoryOptimization { get; set; } = true;
        public int DefaultTimeoutSeconds { get; set; } = 30;
        public int DefaultRetryCount { get; set; } = 2;
        public int RateLimitPerMinute { get; set; } = 60;
        public string[] AllowedPaths { get; set; } = Array.Empty<string>();
        public string[] ForbiddenPaths { get; set; } = Array.Empty<string>();
        public long MaxFileSizeBytes { get; set; } = 104_857_600; // 100MB
        public bool EnableAuditLogging { get; set; } = false;
        public bool EnableInputValidation { get; set; } = true;
        public bool EnableHealthChecks { get; set; } = false;
        public bool EnableMetrics { get; set; } = false;
        public int HealthCheckIntervalSeconds { get; set; } = 60;
        public bool EnableCircuitBreaker { get; set; } = false;
        public int CircuitBreakerThreshold { get; set; } = 5;
        public int CircuitBreakerTimeoutSeconds { get; set; } = 60;
        public bool EnableCompression { get; set; } = false;
        public int CompressionThresholdKB { get; set; } = 1024;
        public string[] TrustedDomains { get; set; } = Array.Empty<string>();
        public string[] BlockedDomains { get; set; } = Array.Empty<string>();

        // Security settings
        public bool EnableIntrusionDetection { get; set; } = true;
        public bool EnablePasswordManager { get; set; } = false;
        public bool EnableProcessSandboxing { get; set; } = true;
        public int SecurityLogRetentionDays { get; set; } = 90;
        public string SecurityLogDirectory { get; set; } = GetDefaultSecurityLogDir();
        public int MaxSecurityEventsPerMinute { get; set; } = 100;
        public bool EnableSecureProcessExecution { get; set; } = true;
        public long MaxProcessMemoryMB { get; set; } = 512;
        public TimeSpan MaxProcessExecutionTime { get; set; } = TimeSpan.FromMinutes(5);
        public string? SourceConfigPath { get; private set; }
        public IReadOnlyList<string> PathResolutionWarnings => _pathResolutionWarnings;
        public bool HasPathResolutionWarnings => _pathResolutionWarnings.Count > 0;

        public IReadOnlyList<string> GetPathResolutionWarningsSnapshot()
        {
            if (_pathResolutionWarnings.Count == 0)
            {
                return Array.Empty<string>();
            }

            return _pathResolutionWarnings.ToArray();
        }

        public string? GetPathResolutionWarningsSummary(string separator = "; ")
        {
            if (_pathResolutionWarnings.Count == 0)
            {
                return null;
            }

            return string.Join(separator, _pathResolutionWarnings);
        }

        public IDictionary<string, object?> GetDiagnosticSnapshot()
        {
            var allowedPaths = AllowedPaths.Length == 0 ? Array.Empty<string>() : AllowedPaths.ToArray();
            var forbiddenPaths = ForbiddenPaths.Length == 0 ? Array.Empty<string>() : ForbiddenPaths.ToArray();
            var trustedDomains = TrustedDomains.Length == 0 ? Array.Empty<string>() : TrustedDomains.ToArray();
            var blockedDomains = BlockedDomains.Length == 0 ? Array.Empty<string>() : BlockedDomains.ToArray();
            var warnings = GetPathResolutionWarningsSnapshot();

            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["SourceConfigPath"] = SourceConfigPath ?? "defaults",
                ["MaxConcurrentFlows"] = MaxConcurrentFlows,
                ["EnableAutoBackup"] = EnableAutoBackup,
                ["WorkingDirectory"] = WorkingDirectory,
                ["CacheDirectory"] = CacheDirectory,
                ["LogDirectory"] = LogDirectory,
                ["LogLevel"] = LogLevel,
                ["EnableFileLogging"] = EnableFileLogging,
                ["EnableConsoleLogging"] = EnableConsoleLogging,
                ["LogRetentionDays"] = LogRetentionDays,
                ["MemoryLimitMB"] = MemoryLimitMB,
                ["CacheSizeMB"] = CacheSizeMB,
                ["EnableMemoryOptimization"] = EnableMemoryOptimization,
                ["DefaultTimeoutSeconds"] = DefaultTimeoutSeconds,
                ["DefaultRetryCount"] = DefaultRetryCount,
                ["RateLimitPerMinute"] = RateLimitPerMinute,
                ["AllowedPaths"] = allowedPaths,
                ["ForbiddenPaths"] = forbiddenPaths,
                ["MaxFileSizeBytes"] = MaxFileSizeBytes,
                ["EnableAuditLogging"] = EnableAuditLogging,
                ["EnableInputValidation"] = EnableInputValidation,
                ["EnableHealthChecks"] = EnableHealthChecks,
                ["EnableMetrics"] = EnableMetrics,
                ["HealthCheckIntervalSeconds"] = HealthCheckIntervalSeconds,
                ["EnableCircuitBreaker"] = EnableCircuitBreaker,
                ["CircuitBreakerThreshold"] = CircuitBreakerThreshold,
                ["CircuitBreakerTimeoutSeconds"] = CircuitBreakerTimeoutSeconds,
                ["EnableCompression"] = EnableCompression,
                ["CompressionThresholdKB"] = CompressionThresholdKB,
                ["TrustedDomains"] = trustedDomains,
                ["BlockedDomains"] = blockedDomains,
                ["EnableIntrusionDetection"] = EnableIntrusionDetection,
                ["EnablePasswordManager"] = EnablePasswordManager,
                ["EnableProcessSandboxing"] = EnableProcessSandboxing,
                ["SecurityLogRetentionDays"] = SecurityLogRetentionDays,
                ["SecurityLogDirectory"] = SecurityLogDirectory,
                ["MaxSecurityEventsPerMinute"] = MaxSecurityEventsPerMinute,
                ["EnableSecureProcessExecution"] = EnableSecureProcessExecution,
                ["MaxProcessMemoryMB"] = MaxProcessMemoryMB,
                ["MaxProcessExecutionTime"] = MaxProcessExecutionTime,
                ["HasPathResolutionWarnings"] = HasPathResolutionWarnings,
                ["WarningsCount"] = warnings.Count,
                ["Warnings"] = warnings,
                ["WarningsSummary"] = GetPathResolutionWarningsSummary(),
                ["LlmProvider"] = LlmProvider,
                ["LlmModel"] = LlmModel,
                ["LlmApiEndpoint"] = LlmApiEndpoint,
                ["LlmMaxTokens"] = LlmMaxTokens,
                ["LlmTemperature"] = LlmTemperature,
                ["LlmHttpTimeoutMs"] = LlmHttpTimeoutMs,
                ["IsLlmApiKeyConfigured"] = !string.IsNullOrWhiteSpace(LlmApiKey)
            };
        }

        public void ValidateConfiguration()
        {
            var validationErrors = new List<string>();

            // Validate numeric ranges
            if (MaxConcurrentFlows < 1 || MaxConcurrentFlows > 1000)
                validationErrors.Add("MaxConcurrentFlows must be between 1 and 1000");

            if (MemoryLimitMB < 64 || MemoryLimitMB > 8192)
                validationErrors.Add("MemoryLimitMB must be between 64 and 8192");

            if (CacheSizeMB < 16 || CacheSizeMB > 1024)
                validationErrors.Add("CacheSizeMB must be between 16 and 1024");

            if (LogRetentionDays < 1 || LogRetentionDays > 365)
                validationErrors.Add("LogRetentionDays must be between 1 and 365");

            if (DefaultTimeoutSeconds < 1 || DefaultTimeoutSeconds > 3600)
                validationErrors.Add("DefaultTimeoutSeconds must be between 1 and 3600");

            if (RateLimitPerMinute < 1 || RateLimitPerMinute > 10000)
                validationErrors.Add("RateLimitPerMinute must be between 1 and 10000");

            if (HealthCheckIntervalSeconds < 10 || HealthCheckIntervalSeconds > 3600)
                validationErrors.Add("HealthCheckIntervalSeconds must be between 10 and 3600");

            if (CircuitBreakerThreshold < 1 || CircuitBreakerThreshold > 100)
                validationErrors.Add("CircuitBreakerThreshold must be between 1 and 100");

            if (CircuitBreakerTimeoutSeconds < 10 || CircuitBreakerTimeoutSeconds > 3600)
                validationErrors.Add("CircuitBreakerTimeoutSeconds must be between 10 and 3600");

            if (CompressionThresholdKB < 1 || CompressionThresholdKB > 1048576)
                validationErrors.Add("CompressionThresholdKB must be between 1 and 1048576");

            if (AllowedPaths.Length > 32)
                validationErrors.Add("AllowedPaths cannot exceed 32 entries");

            if (ForbiddenPaths.Length > 32)
                validationErrors.Add("ForbiddenPaths cannot exceed 32 entries");

            // Create required directories if they don't exist
            if (!IsSafePath(WorkingDirectory))
            {
                validationErrors.Add($"WorkingDirectory '{WorkingDirectory}' is not allowed.");
            }
            else if (!Directory.Exists(WorkingDirectory))
            {
                try
                {
                    Directory.CreateDirectory(WorkingDirectory);
                }
                catch (Exception ex)
                {
                    validationErrors.Add($"Failed to create WorkingDirectory: {WorkingDirectory} - {ex.Message}");
                }
            }

            if (!string.IsNullOrEmpty(CacheDirectory))
            {
                if (!IsSafePath(CacheDirectory))
                {
                    validationErrors.Add($"CacheDirectory '{CacheDirectory}' is not allowed.");
                }
                else if (!Directory.Exists(CacheDirectory))
                {
                    try
                    {
                        Directory.CreateDirectory(CacheDirectory);
                    }
                    catch (Exception ex)
                    {
                        validationErrors.Add($"Failed to create CacheDirectory: {CacheDirectory} - {ex.Message}");
                    }
                }
            }

            if (!string.IsNullOrEmpty(LogDirectory))
            {
                if (!IsSafePath(LogDirectory))
                {
                    validationErrors.Add($"LogDirectory '{LogDirectory}' is not allowed.");
                }
                else if (!Directory.Exists(LogDirectory))
                {
                    try
                    {
                        Directory.CreateDirectory(LogDirectory);
                    }
                    catch (Exception ex)
                    {
                        validationErrors.Add($"Failed to create LogDirectory: {LogDirectory} - {ex.Message}");
                    }
                }
            }

            // Validate path conflicts
            var normalizedForbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var forbidden in ForbiddenPaths)
            {
                if (string.IsNullOrWhiteSpace(forbidden))
                    continue;

                if (!IsSafePath(forbidden))
                {
                    validationErrors.Add($"Forbidden path '{forbidden}' is not allowed.");
                    continue;
                }

                normalizedForbidden.Add(NormalizeForComparison(forbidden));
            }

            foreach (var allowed in AllowedPaths)
            {
                if (string.IsNullOrWhiteSpace(allowed))
                    continue;

                if (!IsSafePath(allowed))
                {
                    validationErrors.Add($"Allowed path '{allowed}' is not allowed.");
                    continue;
                }

                var normalizedAllowed = NormalizeForComparison(allowed);
                foreach (var forbidden in normalizedForbidden)
                {
                    if (IsSameOrChildPath(normalizedAllowed, forbidden))
                    {
                        validationErrors.Add($"Allowed path '{allowed}' conflicts with forbidden path '{forbidden}'.");
                        break;
                    }
                }
            }

            // Validate log level
            var validLogLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
            if (!validLogLevels.Contains(LogLevel))
            {
                validationErrors.Add($"LogLevel must be one of: {string.Join(", ", validLogLevels)}");
            }

            if (validationErrors.Count > 0)
            {
                var errorMessage = $"Configuration validation failed: {string.Join("; ", validationErrors)}";
                _pathResolutionWarnings.Add(errorMessage);
                throw new LocoConfigurationException(errorMessage, "CONFIG_VALIDATION_FAILED");
            }
        }

        // LLM Configuration Properties
        private string? _llmProvider;
        private string? _llmModel;
        private string? _llmApiKey;
        private string? _llmApiEndpoint;
        private int? _llmMaxTokens;
        private double? _llmTemperature;
        private int? _llmHttpTimeoutMs;

        public string? LlmProvider
        {
            get => _llmProvider ?? GetEnvironmentVariable("LOCO_LLM__PROVIDER", "LOCO_LLM_PROVIDER");
            set => _llmProvider = value;
        }

        public string? LlmModel
        {
            get => _llmModel ?? GetEnvironmentVariable("LOCO_LLM__MODEL", "LOCO_LLM_MODEL");
            set => _llmModel = value;
        }

        public string? LlmApiKey
        {
            get => _llmApiKey ?? GetEnvironmentVariable("LOCO_LLM__APIKEY", "LOCO_LLM_API_KEY");
            set => _llmApiKey = value;
        }

        public string? LlmApiEndpoint
        {
            get => _llmApiEndpoint ?? GetEnvironmentVariable("LOCO_LLM__APIENDPOINT", "LOCO_LLM_API_ENDPOINT");
            set => _llmApiEndpoint = value;
        }

        public int? LlmMaxTokens
        {
            get => _llmMaxTokens ?? GetEnvironmentVariableInt(new[] { "LOCO_LLM__MAXTOKENS", "LOCO_LLM_MAX_TOKENS" }, 1000);
            set => _llmMaxTokens = value;
        }

        public double? LlmTemperature
        {
            get => _llmTemperature ?? GetEnvironmentVariableDouble(new[] { "LOCO_LLM__TEMPERATURE", "LOCO_LLM_TEMPERATURE" }, 0.7);
            set => _llmTemperature = value;
        }

        public int? LlmHttpTimeoutMs
        {
            get => _llmHttpTimeoutMs ?? GetEnvironmentVariableInt(new[] { "LOCO_LLM__HTTPTIMEOUTMS", "LOCO_LLM_HTTP_TIMEOUT_MS" }, 30000);
            set => _llmHttpTimeoutMs = value;
        }

        // Simple constructor - load config once
        public LocoConfig()
        {
            LoadConfig();
            ValidateConfiguration();
        }

        // Direct config loading - no complex overrides, just load the JSON
        private void LoadConfig()
        {
            SourceConfigPath = null;
            _pathResolutionWarnings.Clear();
            string? configPath = GetConfigPath();
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
                return;

            SourceConfigPath = configPath;
            var configDirectory = Path.GetDirectoryName(configPath);

            try
            {
                string json = File.ReadAllText(configPath);
                var configData = JsonSerializer.Deserialize<ConfigData>(json, ConfigSerializerOptions);

                if (configData == null) return;

                // Direct property mapping - no complex validation or overrides
                MaxConcurrentFlows = configData.MaxConcurrentFlows ?? MaxConcurrentFlows;
                EnableAutoBackup = configData.EnableAutoBackup ?? EnableAutoBackup;
                WorkingDirectory = ResolveConfiguredPath(configData.WorkingDirectory, configDirectory) ?? WorkingDirectory;
                CacheDirectory = ResolveConfiguredPath(configData.CacheDirectory, configDirectory) ?? CacheDirectory;
                LogDirectory = ResolveConfiguredPath(configData.LogDirectory, configDirectory) ?? LogDirectory;
                LogLevel = configData.LogLevel ?? LogLevel;
                EnableFileLogging = configData.EnableFileLogging ?? EnableFileLogging;
                EnableConsoleLogging = configData.EnableConsoleLogging ?? EnableConsoleLogging;
                LogRetentionDays = configData.LogRetentionDays ?? LogRetentionDays;
                MemoryLimitMB = configData.MemoryLimitMB ?? MemoryLimitMB;
                CacheSizeMB = configData.CacheSizeMB ?? CacheSizeMB;
                EnableMemoryOptimization = configData.EnableMemoryOptimization ?? EnableMemoryOptimization;
                DefaultTimeoutSeconds = configData.DefaultTimeoutSeconds ?? DefaultTimeoutSeconds;
                DefaultRetryCount = configData.DefaultRetryCount ?? DefaultRetryCount;
                RateLimitPerMinute = configData.RateLimitPerMinute ?? RateLimitPerMinute;
                AllowedPaths = ResolveConfiguredPaths(configData.AllowedPaths, configDirectory) ?? AllowedPaths;
                ForbiddenPaths = ResolveConfiguredPaths(configData.ForbiddenPaths, configDirectory) ?? ForbiddenPaths;
                MaxFileSizeBytes = configData.MaxFileSizeBytes ?? MaxFileSizeBytes;
                EnableAuditLogging = configData.EnableAuditLogging ?? EnableAuditLogging;
                EnableInputValidation = configData.EnableInputValidation ?? EnableInputValidation;
                EnableHealthChecks = configData.EnableHealthChecks ?? EnableHealthChecks;
                EnableMetrics = configData.EnableMetrics ?? EnableMetrics;
                HealthCheckIntervalSeconds = configData.HealthCheckIntervalSeconds ?? HealthCheckIntervalSeconds;
                EnableCircuitBreaker = configData.EnableCircuitBreaker ?? EnableCircuitBreaker;
                CircuitBreakerThreshold = configData.CircuitBreakerThreshold ?? CircuitBreakerThreshold;
                CircuitBreakerTimeoutSeconds = configData.CircuitBreakerTimeoutSeconds ?? CircuitBreakerTimeoutSeconds;
                EnableCompression = configData.EnableCompression ?? EnableCompression;
                CompressionThresholdKB = configData.CompressionThresholdKB ?? CompressionThresholdKB;
                TrustedDomains = configData.TrustedDomains ?? TrustedDomains;
                BlockedDomains = configData.BlockedDomains ?? BlockedDomains;
                EnableIntrusionDetection = configData.EnableIntrusionDetection ?? EnableIntrusionDetection;
                EnablePasswordManager = configData.EnablePasswordManager ?? EnablePasswordManager;
                EnableProcessSandboxing = configData.EnableProcessSandboxing ?? EnableProcessSandboxing;
                SecurityLogRetentionDays = configData.SecurityLogRetentionDays ?? SecurityLogRetentionDays;
                SecurityLogDirectory = ResolveConfiguredPath(configData.SecurityLogDirectory, configDirectory) ?? SecurityLogDirectory;
                MaxSecurityEventsPerMinute = configData.MaxSecurityEventsPerMinute ?? MaxSecurityEventsPerMinute;
                EnableSecureProcessExecution = configData.EnableSecureProcessExecution ?? EnableSecureProcessExecution;
                MaxProcessMemoryMB = configData.MaxProcessMemoryMB ?? MaxProcessMemoryMB;
                MaxProcessExecutionTime = configData.MaxProcessExecutionTime ?? MaxProcessExecutionTime;

                // LLM Configuration
                _llmProvider = configData.LlmProvider;
                _llmModel = configData.LlmModel;
                _llmApiKey = configData.LlmApiKey;
                _llmApiEndpoint = configData.LlmApiEndpoint;
                _llmMaxTokens = configData.LlmMaxTokens;
                _llmTemperature = configData.LlmTemperature;
                _llmHttpTimeoutMs = configData.LlmHttpTimeoutMs;
            }
            catch (Exception ex)
            {
                // If config fails to load, just use defaults - no complex error handling
                _pathResolutionWarnings.Add($"Configuration loading failed: {ex.Message}");
            }
        }

        // Simple config path resolution
        private static string? GetConfigPath()
        {
            string? path = Environment.GetEnvironmentVariable(ConfigPathEnvVar);
            if (!string.IsNullOrWhiteSpace(path))
                return Path.GetFullPath(path);

            string baseDir = AppContext.BaseDirectory;
            string defaultPath = Path.Combine(baseDir, "config", "loco.config.json");
            return File.Exists(defaultPath) ? defaultPath : null;
        }

        // Direct default directory getters
        private static string GetDefaultCacheDir()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
                appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(appData, "Loco", "Cache");
        }

        private static string GetDefaultLogDir()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
                appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(appData, "Loco", "Logs");
        }

        private static string GetDefaultSecurityLogDir()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
                appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(appData, "Loco", "Security");
        }

        // Environment variable helpers
        private static string? GetEnvironmentVariable(params string[] names)
        {
            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var value = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }

            return null;
        }

        private static int GetEnvironmentVariableInt(string[] names, int defaultValue)
        {
            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var value = Environment.GetEnvironmentVariable(name);
                if (int.TryParse(value, out var result))
                    return result;
            }

            return defaultValue;
        }

        private string? ResolveConfiguredPath(string? configuredPath, string? configDirectory)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
                return null;

            try
            {
                if (Path.IsPathRooted(configuredPath) || string.IsNullOrEmpty(configDirectory))
                    return Path.GetFullPath(configuredPath);

                var combined = Path.Combine(configDirectory, configuredPath);
                return Path.GetFullPath(combined);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                _pathResolutionWarnings.Add($"Unable to resolve path '{configuredPath}': {ex.Message}");
                return null;
            }
        }

        private string[]? ResolveConfiguredPaths(IEnumerable<string>? configuredPaths, string? configDirectory)
        {
            if (configuredPaths == null)
                return null;

            var resolved = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in configuredPaths)
            {
                var fullPath = ResolveConfiguredPath(path, configDirectory);
                if (string.IsNullOrEmpty(fullPath))
                {
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        _pathResolutionWarnings.Add($"Skipping path entry '{path}' because it could not be resolved.");
                    }
                    continue;
                }

                if (!IsSafePath(fullPath))
                {
                    _pathResolutionWarnings.Add($"Skipping path entry '{path}' because it targets a restricted location.");
                    continue;
                }

                if (seen.Add(fullPath))
                {
                    resolved.Add(fullPath);
                }
                else
                {
                    _pathResolutionWarnings.Add($"Skipping duplicate path entry '{path}'.");
                }
            }

            return resolved.Count == 0 ? Array.Empty<string>() : resolved.ToArray();
        }

        private static double GetEnvironmentVariableDouble(string[] names, double defaultValue)
        {
            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var value = Environment.GetEnvironmentVariable(name);
                if (double.TryParse(value, out var result))
                    return result;
            }

            return defaultValue;
        }

        public void ClearPathResolutionWarnings()
        {
            _pathResolutionWarnings.Clear();
        }

        private static bool IsSafePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                var fullPath = Path.GetFullPath(path);
                return SecurityUtilities.IsPathSafe(fullPath);
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeForComparison(string path)
        {
            return Path.GetFullPath(path)
                .Replace('\\', '/')
                .TrimEnd('/')
                .ToLowerInvariant();
        }

        private static bool IsSameOrChildPath(string candidate, string root)
        {
            if (candidate.Equals(root, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!root.EndsWith('/'))
                root += '/';

            return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        private class ConfigData
        {
            public int? MaxConcurrentFlows { get; set; }
            public bool? EnableAutoBackup { get; set; }
            public string? WorkingDirectory { get; set; }
            public string? CacheDirectory { get; set; }
            public string? LogDirectory { get; set; }
            public string? LogLevel { get; set; }
            public bool? EnableFileLogging { get; set; }
            public bool? EnableConsoleLogging { get; set; }
            public int? LogRetentionDays { get; set; }
            public int? MemoryLimitMB { get; set; }
            public int? CacheSizeMB { get; set; }
            public bool? EnableMemoryOptimization { get; set; }
            public int? DefaultTimeoutSeconds { get; set; }
            public int? DefaultRetryCount { get; set; }
            public int? RateLimitPerMinute { get; set; }
            public string[]? AllowedPaths { get; set; }
            public string[]? ForbiddenPaths { get; set; }
            public long? MaxFileSizeBytes { get; set; }
            public bool? EnableAuditLogging { get; set; }
            public bool? EnableInputValidation { get; set; }
            public bool? EnableHealthChecks { get; set; }
            public bool? EnableMetrics { get; set; }
            public int? HealthCheckIntervalSeconds { get; set; }
            public bool? EnableCircuitBreaker { get; set; }
            public int? CircuitBreakerThreshold { get; set; }
            public int? CircuitBreakerTimeoutSeconds { get; set; }
            public bool? EnableCompression { get; set; }
            public int? CompressionThresholdKB { get; set; }
            public string[]? TrustedDomains { get; set; }
            public string[]? BlockedDomains { get; set; }
            public bool? EnableIntrusionDetection { get; set; }
            public bool? EnablePasswordManager { get; set; }
            public bool? EnableProcessSandboxing { get; set; }
            public int? SecurityLogRetentionDays { get; set; }
            public string? SecurityLogDirectory { get; set; }
            public int? MaxSecurityEventsPerMinute { get; set; }
            public bool? EnableSecureProcessExecution { get; set; }
            public long? MaxProcessMemoryMB { get; set; }
            public TimeSpan? MaxProcessExecutionTime { get; set; }

            // LLM Configuration
            public string? LlmProvider { get; set; }
            public string? LlmModel { get; set; }
            public string? LlmApiKey { get; set; }
            public string? LlmApiEndpoint { get; set; }
            public int? LlmMaxTokens { get; set; }
            public double? LlmTemperature { get; set; }
            public int? LlmHttpTimeoutMs { get; set; }
        }
    }
}
