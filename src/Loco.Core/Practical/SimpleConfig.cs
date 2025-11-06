// Rob Pike: "Data dominates. If you've chosen the right data structures, the algorithms will be self-evident"
// John Carmack: "Configuration should be simple and obvious"

using System.Collections.Concurrent;
using System.Text.Json;

namespace Loco.Core.Practical;

/// <summary>
/// Simple configuration - Load from files, environment, command line
/// Type-safe, hot reload, zero dependencies
/// </summary>
public class SimpleConfig
{
    private readonly ConcurrentDictionary<string, object> _values = new();
    private readonly List<IConfigSource> _sources = new();
    private readonly SimpleLogger _logger;

    public SimpleConfig(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleConfig));
    }

    // Get value with type safety
    public T Get<T>(string key, T defaultValue = default!)
    {
        if (_values.TryGetValue(key, out var value))
        {
            try
            {
                if (value is T typedValue)
                    return typedValue;

                if (value is string strValue)
                {
                    return ConvertFromString<T>(strValue);
                }

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to convert config value {key}: {ex.Message}");
                return defaultValue;
            }
        }

        return defaultValue;
    }

    // Set value
    public void Set<T>(string key, T value)
    {
        if (value != null)
        {
            _values[key] = value;
            _logger.Debug($"Config set: {key} = {value}");
        }
    }

    // Check if key exists
    public bool Has(string key) => _values.ContainsKey(key);

    // Get all keys
    public IEnumerable<string> GetAllKeys() => _values.Keys;

    // Load from JSON file
    public void LoadFromJsonFile(string path)
    {
        if (!File.Exists(path))
        {
            _logger.Warning($"Config file not found: {path}");
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

            if (dict != null)
            {
                foreach (var kvp in dict)
                {
                    _values[kvp.Key] = ConvertJsonElement(kvp.Value);
                }
                _logger.Info($"Loaded config from {path}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load config from {path}", ex);
        }
    }

    // Load from environment variables
    public void LoadFromEnvironment(string prefix = "")
    {
        foreach (var key in Environment.GetEnvironmentVariables().Keys)
        {
            var keyStr = key.ToString() ?? "";
            if (string.IsNullOrEmpty(prefix) || keyStr.StartsWith(prefix))
            {
                var configKey = string.IsNullOrEmpty(prefix) ? keyStr : keyStr.Substring(prefix.Length);
                var value = Environment.GetEnvironmentVariable(keyStr);
                if (value != null)
                {
                    _values[configKey] = value;
                }
            }
        }
        _logger.Info("Loaded config from environment variables");
    }

    // Load from command line args
    public void LoadFromArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            // Handle --key=value
            if (arg.StartsWith("--") && arg.Contains('='))
            {
                var parts = arg.Substring(2).Split('=', 2);
                if (parts.Length == 2)
                {
                    _values[parts[0]] = parts[1];
                }
            }
            // Handle --key value
            else if (arg.StartsWith("--") && i + 1 < args.Length)
            {
                var key = arg.Substring(2);
                var value = args[i + 1];
                if (!value.StartsWith("--"))
                {
                    _values[key] = value;
                    i++;
                }
            }
            // Handle -k value
            else if (arg.StartsWith("-") && !arg.StartsWith("--") && i + 1 < args.Length)
            {
                var key = arg.Substring(1);
                var value = args[i + 1];
                if (!value.StartsWith("-"))
                {
                    _values[key] = value;
                    i++;
                }
            }
        }
        _logger.Info("Loaded config from command line args");
    }

    // Add config source
    public void AddSource(IConfigSource source)
    {
        _sources.Add(source);
    }

    // Reload all sources
    public async Task ReloadAsync()
    {
        foreach (var source in _sources)
        {
            try
            {
                var values = await source.LoadAsync();
                foreach (var kvp in values)
                {
                    _values[kvp.Key] = kvp.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to reload from source {source.GetType().Name}", ex);
            }
        }
    }

    // Save to JSON file
    public void SaveToJsonFile(string path)
    {
        try
        {
            var dict = _values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            _logger.Info($"Saved config to {path}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save config to {path}", ex);
        }
    }

    private T ConvertFromString<T>(string value)
    {
        var type = typeof(T);

        if (type == typeof(bool))
            return (T)(object)bool.Parse(value);
        if (type == typeof(int))
            return (T)(object)int.Parse(value);
        if (type == typeof(long))
            return (T)(object)long.Parse(value);
        if (type == typeof(double))
            return (T)(object)double.Parse(value);
        if (type == typeof(decimal))
            return (T)(object)decimal.Parse(value);
        if (type == typeof(DateTime))
            return (T)(object)DateTime.Parse(value);
        if (type == typeof(TimeSpan))
            return (T)(object)TimeSpan.Parse(value);

        return (T)(object)value;
    }

    private object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => element.ToString()
        };
    }
}

/// <summary>
/// Configuration source interface
/// </summary>
public interface IConfigSource
{
    Task<Dictionary<string, object>> LoadAsync();
}

/// <summary>
/// File-based config source with hot reload
/// </summary>
public class FileConfigSource : IConfigSource
{
    private readonly string _path;
    private readonly SimpleConfig _config;
    private FileSystemWatcher? _watcher;

    public FileConfigSource(string path, SimpleConfig config)
    {
        _path = path;
        _config = config;
    }

    public async Task<Dictionary<string, object>> LoadAsync()
    {
        if (!File.Exists(_path))
            return new Dictionary<string, object>();

        var json = await File.ReadAllTextAsync(_path);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

        return dict?.ToDictionary(
            kvp => kvp.Key,
            kvp => (object)(kvp.Value.ValueKind == JsonValueKind.String ? kvp.Value.GetString()! : kvp.Value.ToString()))
            ?? new Dictionary<string, object>();
    }

    public void EnableHotReload()
    {
        var directory = Path.GetDirectoryName(_path) ?? ".";
        var fileName = Path.GetFileName(_path);

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };

        _watcher.Changed += async (s, e) => await _config.ReloadAsync();
        _watcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}

/// <summary>
/// Typed configuration
/// </summary>
public class TypedConfig<T> where T : class, new()
{
    private readonly SimpleConfig _config;
    private readonly string _section;

    public TypedConfig(SimpleConfig config, string section = "")
    {
        _config = config;
        _section = section;
    }

    public T Get()
    {
        var instance = new T();
        var properties = typeof(T).GetProperties();

        foreach (var prop in properties)
        {
            var key = string.IsNullOrEmpty(_section) ? prop.Name : $"{_section}:{prop.Name}";
            if (_config.Has(key))
            {
                var value = _config.Get<object>(key);
                if (value != null)
                {
                    try
                    {
                        var converted = Convert.ChangeType(value, prop.PropertyType);
                        prop.SetValue(instance, converted);
                    }
                    catch { }
                }
            }
        }

        return instance;
    }

    public void Set(T value)
    {
        var properties = typeof(T).GetProperties();

        foreach (var prop in properties)
        {
            var key = string.IsNullOrEmpty(_section) ? prop.Name : $"{_section}:{prop.Name}";
            var propValue = prop.GetValue(value);
            if (propValue != null)
            {
                _config.Set(key, propValue);
            }
        }
    }
}

/// <summary>
/// Configuration builder
/// </summary>
public class ConfigBuilder
{
    private readonly SimpleConfig _config = new();

    public ConfigBuilder AddJsonFile(string path, bool optional = false)
    {
        if (File.Exists(path) || !optional)
        {
            _config.LoadFromJsonFile(path);
        }
        return this;
    }

    public ConfigBuilder AddEnvironmentVariables(string prefix = "")
    {
        _config.LoadFromEnvironment(prefix);
        return this;
    }

    public ConfigBuilder AddCommandLine(string[] args)
    {
        _config.LoadFromArgs(args);
        return this;
    }

    public ConfigBuilder AddInMemory(Dictionary<string, object> values)
    {
        foreach (var kvp in values)
        {
            _config.Set(kvp.Key, kvp.Value);
        }
        return this;
    }

    public SimpleConfig Build() => _config;
}

/// <summary>
/// Example configuration classes
/// </summary>
public class AppConfig
{
    public string AppName { get; set; } = "MyApp";
    public string Environment { get; set; } = "Development";
    public int Port { get; set; } = 8080;
    public bool EnableLogging { get; set; } = true;
    public string LogLevel { get; set; } = "Info";
}

public class DatabaseConfig
{
    public string ConnectionString { get; set; } = "";
    public int MaxConnections { get; set; } = 10;
    public int TimeoutSeconds { get; set; } = 30;
    public bool EnableRetry { get; set; } = true;
}

/// <summary>
/// Example usage
/// </summary>
public class ConfigExamples
{
    public static void Examples()
    {
        // Simple usage
        var config = new SimpleConfig();
        config.Set("AppName", "MyApp");
        config.Set("Port", 8080);
        var appName = config.Get<string>("AppName");
        var port = config.Get<int>("Port");

        // Load from multiple sources
        var config2 = new ConfigBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ENVIRONMENT")}.json", optional: true)
            .AddEnvironmentVariables("APP_")
            .AddCommandLine(Environment.GetCommandLineArgs())
            .Build();

        // Typed configuration
        var typedConfig = new TypedConfig<AppConfig>(config2);
        var appConfig = typedConfig.Get();
        Console.WriteLine($"App: {appConfig.AppName} on port {appConfig.Port}");

        // Hot reload
        var fileSource = new FileConfigSource("config.json", config2);
        config2.AddSource(fileSource);
        fileSource.EnableHotReload();
    }
}