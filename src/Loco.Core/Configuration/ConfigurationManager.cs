using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Loco.Core.Configuration;

/// <summary>
/// Advanced configuration manager with hot reload, validation, and encryption
/// </summary>
public class ConfigurationManager : IDisposable
{
    private readonly ILogger<ConfigurationManager> _logger;
    private readonly ConcurrentDictionary<string, ConfigurationSection> _sections;
    private readonly ConcurrentDictionary<string, IChangeToken> _changeTokens;
    private readonly List<IConfigurationProvider> _providers;
    private readonly SemaphoreSlim _reloadSemaphore;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    // Events
    public event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;
    public event EventHandler<ConfigurationErrorEventArgs> ConfigurationError;

    public ConfigurationManager(ILogger<ConfigurationManager> logger = null)
    {
        _logger = logger;
        _sections = new ConcurrentDictionary<string, ConfigurationSection>();
        _changeTokens = new ConcurrentDictionary<string, IChangeToken>();
        _providers = new List<IConfigurationProvider>();
        _reloadSemaphore = new SemaphoreSlim(1, 1);
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        InitializeDefaultProviders();
    }

    /// <summary>
    /// Load configuration from file
    /// </summary>
    public async Task<T> LoadAsync<T>(string filePath, ConfigurationOptions options = null) where T : class, new()
    {
        options ??= new ConfigurationOptions();

        if (!File.Exists(filePath))
        {
            if (options.CreateIfNotExists)
            {
                var defaultConfig = new T();
                await SaveAsync(filePath, defaultConfig, options);
                return defaultConfig;
            }
            
            throw new FileNotFoundException($"Configuration file not found: {filePath}");
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            
            if (options.Encrypted)
            {
                json = DecryptConfiguration(json, options.EncryptionKey);
            }

            var config = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            
            // Validate configuration
            if (options.ValidateOnLoad)
            {
                var validationResult = ValidateConfiguration(config);
                if (!validationResult.IsValid)
                {
                    throw new InvalidOperationException($"Configuration validation failed: {string.Join(", ", validationResult.Errors)}");
                }
            }

            // Register for hot reload
            if (options.EnableHotReload)
            {
                RegisterHotReload(filePath, typeof(T).Name, () => LoadAsync<T>(filePath, options));
            }

            // Cache the configuration
            var section = new ConfigurationSection
            {
                Name = typeof(T).Name,
                FilePath = filePath,
                Configuration = config,
                Options = options,
                LastModified = File.GetLastWriteTimeUtc(filePath)
            };
            
            _sections[typeof(T).Name] = section;

            _logger?.LogInformation("Loaded configuration from {FilePath}", filePath);
            return config;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load configuration from {FilePath}", filePath);
            ConfigurationError?.Invoke(this, new ConfigurationErrorEventArgs { FilePath = filePath, Error = ex });
            throw;
        }
    }

    /// <summary>
    /// Save configuration to file
    /// </summary>
    public async Task SaveAsync<T>(string filePath, T configuration, ConfigurationOptions options = null) where T : class
    {
        options ??= new ConfigurationOptions();

        await _reloadSemaphore.WaitAsync();
        try
        {
            // Validate before saving
            if (options.ValidateOnSave)
            {
                var validationResult = ValidateConfiguration(configuration);
                if (!validationResult.IsValid)
                {
                    throw new InvalidOperationException($"Configuration validation failed: {string.Join(", ", validationResult.Errors)}");
                }
            }

            // Create backup if requested
            if (options.CreateBackup && File.Exists(filePath))
            {
                var backupPath = $"{filePath}.backup.{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Copy(filePath, backupPath, true);
                _logger?.LogDebug("Created backup at {BackupPath}", backupPath);
            }

            var json = JsonSerializer.Serialize(configuration, _jsonOptions);
            
            if (options.Encrypted)
            {
                json = EncryptConfiguration(json, options.EncryptionKey);
            }

            // Write atomically
            var tempPath = $"{filePath}.tmp";
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, filePath, true);

            // Update cache
            if (_sections.TryGetValue(typeof(T).Name, out var section))
            {
                section.Configuration = configuration;
                section.LastModified = DateTime.UtcNow;
            }

            _logger?.LogInformation("Saved configuration to {FilePath}", filePath);
        }
        finally
        {
            _reloadSemaphore.Release();
        }
    }

    /// <summary>
    /// Get cached configuration
    /// </summary>
    public T Get<T>(string sectionName = null) where T : class
    {
        sectionName ??= typeof(T).Name;
        
        if (_sections.TryGetValue(sectionName, out var section))
        {
            return section.Configuration as T;
        }

        return null;
    }

    /// <summary>
    /// Update configuration value
    /// </summary>
    public async Task UpdateAsync<T>(string sectionName, Action<T> updateAction) where T : class
    {
        if (!_sections.TryGetValue(sectionName, out var section))
        {
            throw new InvalidOperationException($"Configuration section not found: {sectionName}");
        }

        var config = section.Configuration as T;
        if (config == null)
        {
            throw new InvalidOperationException($"Configuration type mismatch for section: {sectionName}");
        }

        updateAction(config);
        
        await SaveAsync(section.FilePath, config, section.Options);
        
        ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
        {
            SectionName = sectionName,
            Configuration = config
        });
    }

    /// <summary>
    /// Merge configurations
    /// </summary>
    public T MergeConfigurations<T>(params T[] configurations) where T : class, new()
    {
        if (configurations == null || configurations.Length == 0)
            return new T();

        var result = JsonSerializer.Deserialize<T>(
            JsonSerializer.Serialize(configurations[0], _jsonOptions), 
            _jsonOptions);

        for (int i = 1; i < configurations.Length; i++)
        {
            if (configurations[i] != null)
            {
                MergeObjects(result, configurations[i]);
            }
        }

        return result;
    }

    /// <summary>
    /// Export configuration to different formats
    /// </summary>
    public async Task ExportAsync<T>(T configuration, string filePath, ExportFormat format) where T : class
    {
        string content;
        
        switch (format)
        {
            case ExportFormat.Json:
                content = JsonSerializer.Serialize(configuration, _jsonOptions);
                break;
                
            case ExportFormat.Yaml:
                content = ConvertToYaml(configuration);
                break;
                
            case ExportFormat.Xml:
                content = ConvertToXml(configuration);
                break;
                
            case ExportFormat.Ini:
                content = ConvertToIni(configuration);
                break;
                
            default:
                throw new NotSupportedException($"Export format not supported: {format}");
        }

        await File.WriteAllTextAsync(filePath, content);
        _logger?.LogInformation("Exported configuration to {FilePath} as {Format}", filePath, format);
    }

    /// <summary>
    /// Import configuration from environment variables
    /// </summary>
    public T ImportFromEnvironment<T>(string prefix = null) where T : class, new()
    {
        var config = new T();
        var properties = typeof(T).GetProperties();
        prefix ??= typeof(T).Name.ToUpperInvariant() + "_";

        foreach (var property in properties)
        {
            var envVarName = prefix + property.Name.ToUpperInvariant();
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            
            if (!string.IsNullOrEmpty(envValue))
            {
                try
                {
                    var convertedValue = Convert.ChangeType(envValue, property.PropertyType);
                    property.SetValue(config, convertedValue);
                    _logger?.LogDebug("Set {Property} from environment variable {EnvVar}", 
                        property.Name, envVarName);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to set {Property} from environment variable {EnvVar}", 
                        property.Name, envVarName);
                }
            }
        }

        return config;
    }

    /// <summary>
    /// Watch for configuration changes
    /// </summary>
    public IDisposable Watch<T>(Action<T> onChange) where T : class
    {
        var sectionName = typeof(T).Name;
        var watcher = new ConfigurationWatcher<T>(this, sectionName, onChange);
        return watcher;
    }

    private void InitializeDefaultProviders()
    {
        // Add JSON provider
        _providers.Add(new JsonConfigurationProvider());
        
        // Add environment variables provider
        _providers.Add(new EnvironmentConfigurationProvider());
        
        // Add command line provider if args are available
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            _providers.Add(new CommandLineConfigurationProvider(args));
        }
    }

    private void RegisterHotReload(string filePath, string sectionName, Func<Task> reloadAction)
    {
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);
        
        var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        watcher.Changed += async (s, e) =>
        {
            await Task.Delay(100); // Debounce
            
            try
            {
                await reloadAction();
                
                if (_sections.TryGetValue(sectionName, out var section))
                {
                    ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
                    {
                        SectionName = sectionName,
                        Configuration = section.Configuration
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Hot reload failed for {FilePath}", filePath);
            }
        };

        _changeTokens[sectionName] = new FileChangeToken(watcher);
    }

    private ValidationResult ValidateConfiguration<T>(T configuration) where T : class
    {
        var result = new ValidationResult { IsValid = true };
        
        // Check for required properties
        var properties = typeof(T).GetProperties();
        
        foreach (var property in properties)
        {
            var requiredAttr = property.GetCustomAttributes(typeof(RequiredAttribute), true).FirstOrDefault();
            if (requiredAttr != null)
            {
                var value = property.GetValue(configuration);
                if (value == null || (value is string str && string.IsNullOrWhiteSpace(str)))
                {
                    result.IsValid = false;
                    result.Errors.Add($"{property.Name} is required");
                }
            }
            
            // Check range validation
            var rangeAttr = property.GetCustomAttributes(typeof(RangeAttribute), true).FirstOrDefault() as RangeAttribute;
            if (rangeAttr != null && property.PropertyType.IsNumeric())
            {
                var value = property.GetValue(configuration);
                if (value != null)
                {
                    var numValue = Convert.ToDouble(value);
                    if (numValue < rangeAttr.Minimum || numValue > rangeAttr.Maximum)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"{property.Name} must be between {rangeAttr.Minimum} and {rangeAttr.Maximum}");
                    }
                }
            }
        }

        // Check if configuration implements IValidatable
        if (configuration is IValidatable validatable)
        {
            var customResult = validatable.Validate();
            if (!customResult.IsValid)
            {
                result.IsValid = false;
                result.Errors.AddRange(customResult.Errors);
            }
        }

        return result;
    }

    private string EncryptConfiguration(string json, string key)
    {
        // Simple XOR encryption for demo (use proper encryption in production)
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key);
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
        var encrypted = new byte[jsonBytes.Length];
        
        for (int i = 0; i < jsonBytes.Length; i++)
        {
            encrypted[i] = (byte)(jsonBytes[i] ^ keyBytes[i % keyBytes.Length]);
        }
        
        return Convert.ToBase64String(encrypted);
    }

    private string DecryptConfiguration(string encrypted, string key)
    {
        // Simple XOR decryption for demo (use proper decryption in production)
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key);
        var encryptedBytes = Convert.FromBase64String(encrypted);
        var decrypted = new byte[encryptedBytes.Length];
        
        for (int i = 0; i < encryptedBytes.Length; i++)
        {
            decrypted[i] = (byte)(encryptedBytes[i] ^ keyBytes[i % keyBytes.Length]);
        }
        
        return System.Text.Encoding.UTF8.GetString(decrypted);
    }

    private void MergeObjects(object target, object source)
    {
        var targetType = target.GetType();
        var sourceType = source.GetType();
        
        if (targetType != sourceType)
            return;

        foreach (var property in targetType.GetProperties())
        {
            if (!property.CanWrite || !property.CanRead)
                continue;

            var sourceValue = property.GetValue(source);
            if (sourceValue != null)
            {
                if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
                {
                    var targetValue = property.GetValue(target);
                    if (targetValue != null)
                    {
                        MergeObjects(targetValue, sourceValue);
                    }
                    else
                    {
                        property.SetValue(target, sourceValue);
                    }
                }
                else
                {
                    property.SetValue(target, sourceValue);
                }
            }
        }
    }

    private string ConvertToYaml<T>(T configuration)
    {
        // Simplified YAML conversion
        var lines = new List<string>();
        ConvertObjectToYaml(configuration, lines, 0);
        return string.Join(Environment.NewLine, lines);
    }

    private void ConvertObjectToYaml(object obj, List<string> lines, int indent)
    {
        var indentStr = new string(' ', indent * 2);
        var properties = obj.GetType().GetProperties();
        
        foreach (var property in properties)
        {
            var value = property.GetValue(obj);
            if (value == null) continue;
            
            if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
            {
                lines.Add($"{indentStr}{property.Name}:");
                ConvertObjectToYaml(value, lines, indent + 1);
            }
            else
            {
                lines.Add($"{indentStr}{property.Name}: {value}");
            }
        }
    }

    private string ConvertToXml<T>(T configuration)
    {
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
        using var writer = new StringWriter();
        serializer.Serialize(writer, configuration);
        return writer.ToString();
    }

    private string ConvertToIni<T>(T configuration)
    {
        var lines = new List<string>();
        var properties = typeof(T).GetProperties();
        
        foreach (var property in properties)
        {
            var value = property.GetValue(configuration);
            if (value != null)
            {
                lines.Add($"{property.Name}={value}");
            }
        }
        
        return string.Join(Environment.NewLine, lines);
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var token in _changeTokens.Values)
        {
            if (token is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _reloadSemaphore?.Dispose();
        _sections.Clear();
        _changeTokens.Clear();
        _providers.Clear();

        _disposed = true;
    }
}

// Supporting classes
public class ConfigurationSection
{
    public string Name { get; set; }
    public string FilePath { get; set; }
    public object Configuration { get; set; }
    public ConfigurationOptions Options { get; set; }
    public DateTime LastModified { get; set; }
}

public class ConfigurationOptions
{
    public bool CreateIfNotExists { get; set; } = true;
    public bool EnableHotReload { get; set; } = true;
    public bool ValidateOnLoad { get; set; } = true;
    public bool ValidateOnSave { get; set; } = true;
    public bool CreateBackup { get; set; } = false;
    public bool Encrypted { get; set; } = false;
    public string EncryptionKey { get; set; }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

public interface IValidatable
{
    ValidationResult Validate();
}

public class ConfigurationChangedEventArgs : EventArgs
{
    public string SectionName { get; set; }
    public object Configuration { get; set; }
}

public class ConfigurationErrorEventArgs : EventArgs
{
    public string FilePath { get; set; }
    public Exception Error { get; set; }
}

public enum ExportFormat
{
    Json,
    Yaml,
    Xml,
    Ini
}

// Attributes
[AttributeUsage(AttributeTargets.Property)]
public class RequiredAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public class RangeAttribute : Attribute
{
    public double Minimum { get; set; }
    public double Maximum { get; set; }
}

// Configuration providers
public interface IConfigurationProvider
{
    Dictionary<string, string> Load();
}

public class JsonConfigurationProvider : IConfigurationProvider
{
    public Dictionary<string, string> Load()
    {
        return new Dictionary<string, string>();
    }
}

public class EnvironmentConfigurationProvider : IConfigurationProvider
{
    public Dictionary<string, string> Load()
    {
        var config = new Dictionary<string, string>();
        
        foreach (var env in Environment.GetEnvironmentVariables())
        {
            if (env is System.Collections.DictionaryEntry entry)
            {
                config[entry.Key.ToString()] = entry.Value?.ToString();
            }
        }
        
        return config;
    }
}

public class CommandLineConfigurationProvider : IConfigurationProvider
{
    private readonly string[] _args;
    
    public CommandLineConfigurationProvider(string[] args)
    {
        _args = args;
    }
    
    public Dictionary<string, string> Load()
    {
        var config = new Dictionary<string, string>();
        
        for (int i = 0; i < _args.Length; i++)
        {
            if (_args[i].StartsWith("--") && i + 1 < _args.Length)
            {
                var key = _args[i].Substring(2);
                var value = _args[i + 1];
                config[key] = value;
                i++;
            }
        }
        
        return config;
    }
}

// Helpers
public class ConfigurationWatcher<T> : IDisposable where T : class
{
    private readonly ConfigurationManager _manager;
    private readonly string _sectionName;
    private readonly Action<T> _onChange;
    private readonly Timer _timer;
    
    public ConfigurationWatcher(ConfigurationManager manager, string sectionName, Action<T> onChange)
    {
        _manager = manager;
        _sectionName = sectionName;
        _onChange = onChange;
        _timer = new Timer(CheckForChanges, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }
    
    private void CheckForChanges(object state)
    {
        var config = _manager.Get<T>(_sectionName);
        if (config != null)
        {
            _onChange(config);
        }
    }
    
    public void Dispose()
    {
        _timer?.Dispose();
    }
}

public class FileChangeToken : IChangeToken, IDisposable
{
    private readonly FileSystemWatcher _watcher;
    
    public FileChangeToken(FileSystemWatcher watcher)
    {
        _watcher = watcher;
    }
    
    public bool HasChanged => false;
    public bool ActiveChangeCallbacks => true;
    
    public IDisposable RegisterChangeCallback(Action<object> callback, object state)
    {
        _watcher.Changed += (s, e) => callback(state);
        return this;
    }
    
    public void Dispose()
    {
        _watcher?.Dispose();
    }
}

// Extensions
public static class TypeExtensions
{
    public static bool IsNumeric(this Type type)
    {
        return type == typeof(int) || type == typeof(long) || type == typeof(float) || 
               type == typeof(double) || type == typeof(decimal) || type == typeof(short) || 
               type == typeof(byte) || type == typeof(uint) || type == typeof(ulong);
    }
}
