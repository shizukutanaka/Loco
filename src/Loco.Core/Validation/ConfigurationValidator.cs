using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;

namespace Loco.Core.Validation
{
    public interface IConfigurationValidator
    {
        ValidationResult ValidateConfiguration(IConfiguration configuration);
        ValidationResult ValidatePath(string path, PathValidationType type);
        ValidationResult ValidateConnectionString(string connectionString, string provider);
        ValidationResult ValidateApiKey(string apiKey, string service);
        ValidationResult ValidateEnvironmentVariables();
    }

    public class ConfigurationValidator : IConfigurationValidator
    {
        private readonly ILogger<ConfigurationValidator> _logger;
        private readonly Dictionary<string, IConfigSectionValidator> _sectionValidators;

        public ConfigurationValidator(ILogger<ConfigurationValidator> logger)
        {
            _logger = logger;
            _sectionValidators = InitializeSectionValidators();
        }

        public ValidationResult ValidateConfiguration(IConfiguration configuration)
        {
            var result = new ValidationResult();

            foreach (var section in configuration.GetChildren())
            {
                if (_sectionValidators.TryGetValue(section.Key, out var validator))
                {
                    var sectionResult = validator.Validate(section);
                    result.MergeResult(sectionResult, section.Key);
                }
            }

            // Validate critical settings
            ValidateCriticalSettings(configuration, result);

            // Check for security issues
            ValidateSecuritySettings(configuration, result);

            return result;
        }

        public ValidationResult ValidatePath(string path, PathValidationType type)
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(path))
            {
                result.AddError("Path", "Path cannot be empty");
                return result;
            }

            // Check for path traversal attacks
            if (path.Contains("..") || path.Contains("~"))
            {
                result.AddError("Path", "Path contains potentially dangerous characters");
                return result;
            }

            // Validate path format
            try
            {
                var fullPath = Path.GetFullPath(path);

                switch (type)
                {
                    case PathValidationType.Directory:
                        if (!Directory.Exists(fullPath))
                        {
                            result.AddWarning("Path", $"Directory does not exist: {fullPath}");
                        }
                        break;

                    case PathValidationType.File:
                        if (!File.Exists(fullPath))
                        {
                            result.AddWarning("Path", $"File does not exist: {fullPath}");
                        }
                        break;

                    case PathValidationType.DirectoryMustExist:
                        if (!Directory.Exists(fullPath))
                        {
                            result.AddError("Path", $"Directory must exist: {fullPath}");
                        }
                        break;

                    case PathValidationType.FileMustExist:
                        if (!File.Exists(fullPath))
                        {
                            result.AddError("Path", $"File must exist: {fullPath}");
                        }
                        break;

                    case PathValidationType.Writable:
                        if (!IsPathWritable(fullPath))
                        {
                            result.AddError("Path", $"Path is not writable: {fullPath}");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                result.AddError("Path", $"Invalid path format: {ex.Message}");
            }

            return result;
        }

        public ValidationResult ValidateConnectionString(string connectionString, string provider)
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                result.AddError("ConnectionString", "Connection string cannot be empty");
                return result;
            }

            // Check for common security issues
            if (connectionString.ToLower().Contains("password=") &&
                !connectionString.ToLower().Contains("integrated security=true"))
            {
                // Check if password is in plain text
                var passwordMatch = Regex.Match(connectionString, @"password=([^;]+)", RegexOptions.IgnoreCase);
                if (passwordMatch.Success)
                {
                    var password = passwordMatch.Groups[1].Value;
                    if (!IsEncrypted(password))
                    {
                        result.AddWarning("ConnectionString", "Connection string contains plain text password. Consider using secure storage");
                    }
                }
            }

            // Provider-specific validation
            switch (provider?.ToLower())
            {
                case "sqlite":
                    ValidateSqliteConnectionString(connectionString, result);
                    break;

                case "sqlserver":
                    ValidateSqlServerConnectionString(connectionString, result);
                    break;

                case "postgresql":
                    ValidatePostgresConnectionString(connectionString, result);
                    break;

                case "mysql":
                    ValidateMySqlConnectionString(connectionString, result);
                    break;
            }

            return result;
        }

        public ValidationResult ValidateApiKey(string apiKey, string service)
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                result.AddError("ApiKey", $"API key for {service} is required");
                return result;
            }

            // Check for placeholder values
            if (apiKey.Contains("YOUR_API_KEY") || apiKey.Contains("REPLACE_ME") || apiKey.Contains("xxx"))
            {
                result.AddError("ApiKey", $"API key for {service} appears to be a placeholder");
                return result;
            }

            // Service-specific validation
            switch (service?.ToLower())
            {
                case "openai":
                    if (!apiKey.StartsWith("sk-") && !apiKey.StartsWith("org-"))
                    {
                        result.AddWarning("ApiKey", "OpenAI API key format may be invalid");
                    }
                    break;

                case "azure":
                    if (apiKey.Length != 32 && apiKey.Length != 64)
                    {
                        result.AddWarning("ApiKey", "Azure API key length may be invalid");
                    }
                    break;

                case "anthropic":
                    if (!apiKey.StartsWith("sk-ant-"))
                    {
                        result.AddWarning("ApiKey", "Anthropic API key format may be invalid");
                    }
                    break;
            }

            // Check for hardcoded keys (basic check)
            if (IsHardcodedValue(apiKey))
            {
                result.AddWarning("ApiKey", $"API key for {service} may be hardcoded. Consider using environment variables");
            }

            return result;
        }

        public ValidationResult ValidateEnvironmentVariables()
        {
            var result = new ValidationResult();
            var requiredVars = new[]
            {
                "LOCO_ENVIRONMENT",
                "LOCO_LOG_LEVEL"
            };

            foreach (var varName in requiredVars)
            {
                var value = Environment.GetEnvironmentVariable(varName);
                if (string.IsNullOrWhiteSpace(value))
                {
                    result.AddWarning("Environment", $"Recommended environment variable {varName} is not set");
                }
            }

            // Check for development settings in production
            var environment = Environment.GetEnvironmentVariable("LOCO_ENVIRONMENT");
            if (environment?.ToLower() == "production")
            {
                ValidateProductionEnvironment(result);
            }

            return result;
        }

        private void ValidateCriticalSettings(IConfiguration configuration, ValidationResult result)
        {
            // Check data directory
            var dataPath = configuration["DataPath"] ??
                          Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Loco");

            var pathResult = ValidatePath(dataPath, PathValidationType.Directory);
            if (!pathResult.IsValid)
            {
                result.MergeResult(pathResult, "DataPath");
            }

            // Check plugin directory
            var pluginPath = configuration["PluginsPath"];
            if (!string.IsNullOrWhiteSpace(pluginPath))
            {
                var pluginPathResult = ValidatePath(pluginPath, PathValidationType.Directory);
                result.MergeResult(pluginPathResult, "PluginsPath");
            }
        }

        private void ValidateSecuritySettings(IConfiguration configuration, ValidationResult result)
        {
            // Check for sensitive data in configuration
            var configString = configuration.ToString();

            // Check for common sensitive patterns
            var sensitivePatterns = new[]
            {
                @"password\s*=\s*['""]?[^'"";\s]+",
                @"apikey\s*=\s*['""]?[^'"";\s]+",
                @"secret\s*=\s*['""]?[^'"";\s]+",
                @"token\s*=\s*['""]?[^'"";\s]+"
            };

            foreach (var pattern in sensitivePatterns)
            {
                if (Regex.IsMatch(configString, pattern, RegexOptions.IgnoreCase))
                {
                    result.AddWarning("Security", "Configuration may contain sensitive data in plain text");
                    break;
                }
            }
        }

        private void ValidateProductionEnvironment(ValidationResult result)
        {
            // Check debug settings
            if (Environment.GetEnvironmentVariable("LOCO_DEBUG")?.ToLower() == "true")
            {
                result.AddWarning("Environment", "Debug mode is enabled in production environment");
            }

            // Check log level
            var logLevel = Environment.GetEnvironmentVariable("LOCO_LOG_LEVEL");
            if (logLevel?.ToLower() == "trace" || logLevel?.ToLower() == "debug")
            {
                result.AddWarning("Environment", "Verbose logging is enabled in production environment");
            }
        }

        private void ValidateSqliteConnectionString(string connectionString, ValidationResult result)
        {
            if (!connectionString.ToLower().Contains("data source="))
            {
                result.AddError("ConnectionString", "SQLite connection string must contain 'Data Source'");
            }
        }

        private void ValidateSqlServerConnectionString(string connectionString, ValidationResult result)
        {
            if (!connectionString.ToLower().Contains("server=") && !connectionString.ToLower().Contains("data source="))
            {
                result.AddError("ConnectionString", "SQL Server connection string must contain 'Server' or 'Data Source'");
            }
        }

        private void ValidatePostgresConnectionString(string connectionString, ValidationResult result)
        {
            if (!connectionString.ToLower().Contains("host="))
            {
                result.AddError("ConnectionString", "PostgreSQL connection string must contain 'Host'");
            }
        }

        private void ValidateMySqlConnectionString(string connectionString, ValidationResult result)
        {
            if (!connectionString.ToLower().Contains("server="))
            {
                result.AddError("ConnectionString", "MySQL connection string must contain 'Server'");
            }
        }

        private bool IsPathWritable(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    var testFile = Path.Combine(path, $".test_{Guid.NewGuid()}.tmp");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    return true;
                }
                else if (File.Exists(path))
                {
                    using (var stream = File.Open(path, FileMode.Open, FileAccess.Write))
                    {
                        return stream.CanWrite;
                    }
                }
                else
                {
                    // Path doesn't exist, check parent directory
                    var directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        return IsPathWritable(directory);
                    }
                }
            }
            catch
            {
                // Any exception means not writable
            }

            return false;
        }

        private bool IsEncrypted(string value)
        {
            // Simple heuristic - encrypted values typically have high entropy
            if (string.IsNullOrWhiteSpace(value) || value.Length < 16)
                return false;

            // Check for base64 encoding (common for encrypted values)
            try
            {
                Convert.FromBase64String(value);
                return true;
            }
            catch
            {
                // Not base64
            }

            // Check for hex encoding
            return Regex.IsMatch(value, @"^[0-9A-Fa-f]+$");
        }

        private bool IsHardcodedValue(string value)
        {
            // Check if value looks like it comes from an environment variable
            return !value.StartsWith("$(") && !value.StartsWith("${") && !value.StartsWith("%");
        }

        private Dictionary<string, IConfigSectionValidator> InitializeSectionValidators()
        {
            return new Dictionary<string, IConfigSectionValidator>
            {
                ["Logging"] = new LoggingConfigValidator(),
                ["Llm"] = new LlmConfigValidator(),
                ["Database"] = new DatabaseConfigValidator(),
                ["Security"] = new SecurityConfigValidator()
            };
        }
    }

    public enum PathValidationType
    {
        Directory,
        File,
        DirectoryMustExist,
        FileMustExist,
        Writable
    }

    public interface IConfigSectionValidator
    {
        ValidationResult Validate(IConfigurationSection section);
    }

    public class LoggingConfigValidator : IConfigSectionValidator
    {
        public ValidationResult Validate(IConfigurationSection section)
        {
            var result = new ValidationResult();

            var logLevel = section["LogLevel:Default"];
            if (!string.IsNullOrWhiteSpace(logLevel))
            {
                var validLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical", "None" };
                if (!validLevels.Contains(logLevel, StringComparer.OrdinalIgnoreCase))
                {
                    result.AddError("LogLevel", $"Invalid log level '{logLevel}'. Valid levels are: {string.Join(", ", validLevels)}");
                }
            }

            return result;
        }
    }

    public class LlmConfigValidator : IConfigSectionValidator
    {
        public ValidationResult Validate(IConfigurationSection section)
        {
            var result = new ValidationResult();

            var apiKey = section["ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                result.AddError("ApiKey", "LLM API key is required");
            }

            var endpoint = section["Endpoint"];
            if (!string.IsNullOrWhiteSpace(endpoint) && !Uri.TryCreate(endpoint, UriKind.Absolute, out _))
            {
                result.AddError("Endpoint", "Invalid LLM endpoint URL");
            }

            var timeout = section["Timeout"];
            if (!string.IsNullOrWhiteSpace(timeout))
            {
                if (!int.TryParse(timeout, out var timeoutMs) || timeoutMs < 1000 || timeoutMs > 300000)
                {
                    result.AddError("Timeout", "LLM timeout must be between 1000 and 300000 milliseconds");
                }
            }

            return result;
        }
    }

    public class DatabaseConfigValidator : IConfigSectionValidator
    {
        public ValidationResult Validate(IConfigurationSection section)
        {
            var result = new ValidationResult();

            var provider = section["Provider"];
            if (!string.IsNullOrWhiteSpace(provider))
            {
                var validProviders = new[] { "sqlite", "sqlserver", "postgresql", "mysql" };
                if (!validProviders.Contains(provider, StringComparer.OrdinalIgnoreCase))
                {
                    result.AddWarning("Provider", $"Unknown database provider '{provider}'");
                }
            }

            var connectionString = section["ConnectionString"];
            if (!string.IsNullOrWhiteSpace(connectionString) && connectionString.Contains("Password="))
            {
                result.AddWarning("ConnectionString", "Database connection string contains password. Consider using secure storage");
            }

            return result;
        }
    }

    public class SecurityConfigValidator : IConfigSectionValidator
    {
        public ValidationResult Validate(IConfigurationSection section)
        {
            var result = new ValidationResult();

            var encryptionKey = section["EncryptionKey"];
            if (!string.IsNullOrWhiteSpace(encryptionKey))
            {
                if (encryptionKey.Length < 32)
                {
                    result.AddError("EncryptionKey", "Encryption key must be at least 32 characters");
                }

                if (encryptionKey == "CHANGE_ME" || encryptionKey.Contains("default"))
                {
                    result.AddError("EncryptionKey", "Encryption key appears to be a default value");
                }
            }

            var jwtSecret = section["JwtSecret"];
            if (!string.IsNullOrWhiteSpace(jwtSecret))
            {
                if (jwtSecret.Length < 64)
                {
                    result.AddWarning("JwtSecret", "JWT secret should be at least 64 characters for security");
                }
            }

            return result;
        }
    }
}