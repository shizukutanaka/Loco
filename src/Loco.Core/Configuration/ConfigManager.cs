using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Loco.Core.ErrorHandling;
using Loco.Core.Validation;

namespace Loco.Core.Configuration
{
    public interface IConfigManager
    {
        T GetValue<T>(string key, T defaultValue = default);
        void SetValue<T>(string key, T value);
        bool HasKey(string key);
        void RemoveKey(string key);
        void SaveConfiguration();
        Models.ValidationResult ValidateConfiguration();
        Dictionary<string, object> GetAllSettings();
        void ResetToDefaults();
    }

    public class ConfigManager : IConfigManager, IDisposable
    {
        private readonly ILogger<ConfigManager> _logger;
        private readonly string _configPath;
        private readonly Dictionary<string, object> _settings;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IConfigurationValidator _validator;
        private readonly IErrorHandler _errorHandler;
        private bool _isDirty = false;
        private readonly Timer _autoSaveTimer;

        public ConfigManager(
            ILogger<ConfigManager> logger = null,
            string configPath = null)
        {
            _logger = logger ?? NullLogger<ConfigManager>.Instance;
            _validator = new ConfigurationValidator(NullLogger<ConfigurationValidator>.Instance);
            _errorHandler = new ErrorHandler(NullLogger<ErrorHandler>.Instance);

            _configPath = configPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Loco",
                "config.json");

            _settings = new Dictionary<string, object>();
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };

            LoadConfiguration();

            // Auto-save every 30 seconds if there are changes
            _autoSaveTimer = new Timer(AutoSave, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public T GetValue<T>(string key, T defaultValue = default)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                    return defaultValue;

                if (!_settings.TryGetValue(key, out var value))
                    return defaultValue;

                if (value is JsonElement jsonElement)
                {
                    return JsonSerializer.Deserialize<T>(jsonElement.GetRawText(), _jsonOptions);
                }

                if (value is T directValue)
                    return directValue;

                // Type conversion
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex)
            {
                _errorHandler.HandleAsync(ex, new ErrorContext
                {
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["Key"] = key,
                        ["DefaultValue"] = defaultValue
                    }
                });
                return defaultValue;
            }
        }

        public void SetValue<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogWarning("Cannot set configuration value with empty key");
                return;
            }

            try
            {
                var oldValue = _settings.ContainsKey(key) ? _settings[key] : null;
                _settings[key] = value;
                _isDirty = true;

                _logger.LogDebug("Configuration value updated: {Key} = {Value}", key, value);

                // Validate critical settings immediately
                if (IsCriticalSetting(key))
                {
                    var result = ValidateConfiguration();
                    if (!result.IsValid)
                    {
                        _logger.LogWarning("Critical configuration validation failed for key {Key}: {Errors}",
                            key, string.Join(", ", result.Errors));
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandler.HandleAsync(ex, new ErrorContext
                {
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["Key"] = key,
                        ["Value"] = value
                    }
                });
            }
        }

        public bool HasKey(string key)
        {
            return !string.IsNullOrEmpty(key) && _settings.ContainsKey(key);
        }

        public void RemoveKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (_settings.Remove(key))
            {
                _isDirty = true;
                _logger.LogDebug("Configuration key removed: {Key}", key);
            }
        }

        public void SaveConfiguration()
        {
            try
            {
                if (!_isDirty)
                    return;

                var directory = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(_settings, _jsonOptions);
                File.WriteAllText(_configPath, json);

                _isDirty = false;
                _logger.LogInformation("Configuration saved to {Path}", _configPath);
            }
            catch (Exception ex)
            {
                _errorHandler.HandleAsync(ex, new ErrorContext
                {
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["ConfigPath"] = _configPath
                    }
                });
            }
        }

        public Models.ValidationResult ValidateConfiguration()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(_settings.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.ToString()))
                .Build();

            return _validator.ValidateConfiguration(config);
        }

        public Dictionary<string, object> GetAllSettings()
        {
            return new Dictionary<string, object>(_settings);
        }

        public void ResetToDefaults()
        {
            _settings.Clear();
            LoadDefaultSettings();
            _isDirty = true;
            _logger.LogInformation("Configuration reset to defaults");
        }

        private void LoadConfiguration()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    LoadDefaultSettings();
                    SaveConfiguration();
                    return;
                }

                var json = File.ReadAllText(_configPath);
                var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions);

                if (settings != null)
                {
                    _settings.Clear();
                    foreach (var kvp in settings)
                    {
                        _settings[kvp.Key] = kvp.Value;
                    }
                }

                _logger.LogInformation("Configuration loaded from {Path}", _configPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load configuration from {Path}, using defaults", _configPath);
                LoadDefaultSettings();
            }
        }

        private void LoadDefaultSettings()
        {
            _settings.Clear();
            _settings["LogLevel"] = "Information";
            _settings["DataPath"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Loco");
            _settings["PluginsPath"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Loco", "Plugins");
            _settings["MaxConcurrentExecutions"] = 5;
            _settings["DefaultTimeout"] = 60;
            _settings["EnableErrorReporting"] = true;
            _settings["AutoSaveInterval"] = 30;
        }

        private bool IsCriticalSetting(string key)
        {
            var criticalKeys = new[]
            {
                "DataPath",
                "PluginsPath",
                "LogLevel",
                "MaxConcurrentExecutions"
            };

            return Array.IndexOf(criticalKeys, key) >= 0;
        }

        private void AutoSave(object state)
        {
            if (_isDirty)
            {
                SaveConfiguration();
            }
        }

        public void Dispose()
        {
            _autoSaveTimer?.Dispose();
            if (_isDirty)
            {
                SaveConfiguration();
            }
        }
    }
}