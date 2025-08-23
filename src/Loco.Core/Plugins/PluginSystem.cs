using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Loco.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Loco.Core.Plugins;

namespace Loco.Core.Plugins.Legacy;

/// <summary>
/// Plugin system for extending Loco functionality
/// Following Robert C. Martin's Open/Closed Principle
/// </summary>
public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string Description { get; }
    PluginManifest Manifest { get; }
    Task InitializeAsync(IPluginHostContext context);
    Task ShutdownAsync();
}

/// <summary>
/// Plugin manifest with permissions and dependencies
/// </summary>
public class PluginManifest
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Version { get; set; }
    public string Author { get; set; }
    public string Description { get; set; }
    public string EntryPoint { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public PluginPermissions Permissions { get; set; } = new();
    public Dictionary<string, object> Configuration { get; set; } = new();
}

/// <summary>
/// Plugin permissions
/// </summary>
public class PluginPermissions
{
    public bool Network { get; set; }
    public bool FileSystem { get; set; }
    public bool Process { get; set; }
    public bool Llm { get; set; }
    public List<string> AllowedDomains { get; set; } = new();
    public List<string> AllowedPaths { get; set; } = new();
}

/// <summary>
/// Plugin manager for loading and managing plugins
/// </summary>
public class PluginManager
{
    private readonly ILogger<PluginManager> _logger;
    private readonly Dictionary<string, IPlugin> _plugins = new();
    private readonly Dictionary<string, Assembly> _assemblies = new();
    private readonly string _pluginsPath;
    private readonly PluginSandbox _sandbox;
    private readonly IAutomationRuleEngine _automationRuleEngine;

    public PluginManager(ILogger<PluginManager> logger, IAutomationRuleEngine automationRuleEngine, string pluginsPath = null)
    {
        _logger = logger;
        _automationRuleEngine = automationRuleEngine;
        _pluginsPath = PluginPaths.GetEffectivePluginsDirectory(pluginsPath);
        _sandbox = new PluginSandbox(logger);
        
        PluginPaths.EnsureDirectory(_pluginsPath);
    }

    /// <summary>
    /// Load all plugins from the plugins directory
    /// </summary>
    public async Task LoadPluginsAsync()
    {
        _logger.LogInformation("Loading plugins from {Path}", _pluginsPath);
        
        var pluginDirs = Directory.GetDirectories(_pluginsPath);
        
        foreach (var dir in pluginDirs)
        {
            try
            {
                await LoadPluginAsync(dir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from {Directory}", dir);
            }
        }
        
        _logger.LogInformation("Loaded {Count} plugins", _plugins.Count);
    }

    /// <summary>
    /// Load a single plugin
    /// </summary>
    public async Task<bool> LoadPluginAsync(string pluginPath)
    {
        try
        {
            // Load manifest
            var manifestPath = Path.Combine(pluginPath, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                _logger.LogWarning("No manifest found in {Path}", pluginPath);
                return false;
            }
            
            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            var manifest = System.Text.Json.JsonSerializer.Deserialize<PluginManifest>(manifestJson);
            
            if (manifest == null)
            {
                _logger.LogError("Invalid manifest in {Path}", pluginPath);
                return false;
            }
            
            // Validate manifest
            if (!ValidateManifest(manifest))
            {
                _logger.LogError("Manifest validation failed for plugin {Id}", manifest.Id);
                return false;
            }
            
            // Check if already loaded
            if (_plugins.ContainsKey(manifest.Id))
            {
                _logger.LogWarning("Plugin {Id} already loaded", manifest.Id);
                return false;
            }
            
            // Load assembly
            var assemblyPath = Path.Combine(pluginPath, manifest.EntryPoint);
            if (!File.Exists(assemblyPath))
            {
                _logger.LogError("Entry point not found: {Path}", assemblyPath);
                return false;
            }
            
            // Load in sandbox
            var plugin = await _sandbox.LoadPluginAsync(assemblyPath, manifest);
            
            if (plugin == null)
            {
                _logger.LogError("Failed to instantiate plugin {Id}", manifest.Id);
                return false;
            }
            
            // Initialize plugin
            var context = new PluginHostContext(this, manifest.Id, _logger, manifest.Permissions, _automationRuleEngine);
            await plugin.InitializeAsync(context);
            
            // Register plugin
            _plugins[manifest.Id] = plugin;
            
            _logger.LogInformation("Loaded plugin: {Name} v{Version}", manifest.Name, manifest.Version);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading plugin from {Path}", pluginPath);
            return false;
        }
    }

    /// <summary>
    /// Unload a plugin
    /// </summary>
    public async Task<bool> UnloadPluginAsync(string pluginId)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin))
        {
            return false;
        }
        
        try
        {
            await plugin.ShutdownAsync();
            _plugins.Remove(pluginId);
            _sandbox.UnloadPlugin(pluginId);
            
            _logger.LogInformation("Unloaded plugin: {Id}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unloading plugin {Id}", pluginId);
            return false;
        }
    }

    /// <summary>
    /// Get all loaded plugins
    /// </summary>
    public IEnumerable<IPlugin> GetPlugins()
    {
        return _plugins.Values;
    }

    /// <summary>
    /// Get plugin by ID
    /// </summary>
    public IPlugin GetPlugin(string pluginId)
    {
        return _plugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
    }

    /// <summary>
    /// Register custom trigger from plugin
    /// </summary>
    internal void RegisterTrigger(string pluginId, Type triggerType)
    {
        if (!typeof(global::Loco.Core.Triggers.IRuntimeTrigger).IsAssignableFrom(triggerType))
        { 
            throw new ArgumentException("Type must implement IRuntimeTrigger");
        }
        
        // Register the trigger type
        _logger.LogInformation("Registered trigger {Type} from plugin {Plugin}", 
            triggerType.Name, pluginId);
    }


    /// <summary>
    /// Validate plugin manifest
    /// </summary>
    private bool ValidateManifest(PluginManifest manifest)
    {
        if (string.IsNullOrEmpty(manifest.Id))
        {
            _logger.LogError("Plugin ID is required");
            return false;
        }
        
        if (string.IsNullOrEmpty(manifest.Name))
        {
            _logger.LogError("Plugin name is required");
            return false;
        }
        
        if (string.IsNullOrEmpty(manifest.Version))
        {
            _logger.LogError("Plugin version is required");
            return false;
        }
        
        if (string.IsNullOrEmpty(manifest.EntryPoint))
        {
            _logger.LogError("Plugin entry point is required");
            return false;
        }
        
        // Validate version format
        if (!System.Text.RegularExpressions.Regex.IsMatch(manifest.Version, @"^\d+\.\d+\.\d+$"))
        {
            _logger.LogError("Invalid version format: {Version}", manifest.Version);
            return false;
        }
        
        return true;
    }
}

/// <summary>
/// Plugin sandbox for isolated execution
/// </summary>
public class PluginSandbox
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, (PluginAssemblyLoadContext context, IPlugin plugin)> _loadedPlugins = new();

    public PluginSandbox(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Load plugin in isolated context
    /// </summary>
    public async Task<IPlugin> LoadPluginAsync(string assemblyPath, PluginManifest manifest)
    {
        try
        {
            var context = new PluginAssemblyLoadContext(assemblyPath);
            var assembly = context.LoadFromAssemblyPath(assemblyPath);

            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface);

            if (pluginType == null)
            {
                _logger.LogError("No plugin implementation found in assembly {Assembly}", Path.GetFileName(assemblyPath));
                context.Unload();
                return null;
            }

            var pluginInstance = Activator.CreateInstance(pluginType) as IPlugin;
            if (pluginInstance == null)
            {
                _logger.LogError("Failed to create instance of plugin {PluginType}", pluginType.Name);
                context.Unload();
                return null;
            }

            var proxy = new PluginProxy(pluginInstance, manifest.Permissions, _logger);
            _loadedPlugins[manifest.Id] = (context, proxy);

            return proxy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading plugin from {AssemblyPath}", assemblyPath);
            return null;
        }
    }

    /// <summary>
    /// Unload a plugin and its AssemblyLoadContext
    /// </summary>
    public void UnloadPlugin(string pluginId)
    {
        if (_loadedPlugins.TryGetValue(pluginId, out var pluginInfo))
        {
            var (context, plugin) = pluginInfo;
            (plugin as IDisposable)?.Dispose();
            context.Unload();
            _loadedPlugins.Remove(pluginId);
            _logger.LogInformation("Unloaded plugin context for {PluginId}", pluginId);
        }
    }
}

/// <summary>
/// Plugin proxy for permission enforcement
/// </summary>
public class PluginProxy : IPlugin, IDisposable
{
    private readonly IPlugin _plugin;
    private readonly PluginPermissions _permissions;
    private readonly ILogger _logger;

    public PluginProxy(IPlugin plugin, PluginPermissions permissions, ILogger logger)
    {
        _plugin = plugin;
        _permissions = permissions;
        _logger = logger;
    }

    public string Id => _plugin.Id;
    public string Name => _plugin.Name;
    public string Version => _plugin.Version;
    public string Description => _plugin.Description;
    public PluginManifest Manifest => _plugin.Manifest;

    public async Task InitializeAsync(IPluginHostContext context)
    {
        _logger.LogInformation("Initializing plugin {Id} with permissions", Id);
        // Here we could wrap the context in a sandboxed version
        await _plugin.InitializeAsync(context);
    }

    public async Task ShutdownAsync()
    {
        _logger.LogInformation("Shutting down plugin {Id}", Id);
        await _plugin.ShutdownAsync();
    }

    public void Dispose()
    {
        ShutdownAsync().Wait();
    }
}

/// <summary>
/// Defines file system operations available to plugins.
/// </summary>
public interface IPluginFileSystem
{
    Task<string> ReadAllTextAsync(string path);
    Task WriteAllTextAsync(string path, string contents);
    bool FileExists(string path);
}

/// <summary>
/// Provides a context for plugins to interact with the host.
/// </summary>
/// <summary>
/// Defines HTTP client operations available to plugins.
/// </summary>
public interface IPluginHttpClient : IDisposable
{
    Task<HttpResponseMessage> GetAsync(string requestUri);
    Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content);
}

/// <summary>
/// Provides a context for plugins to interact with the host.
/// </summary>
public interface IPluginHostContext
{
    ILogger Logger { get; }
    IPluginFileSystem FileSystem { get; }
    IPluginHttpClient HttpClient { get; }
    void RegisterAction(string name, Type actionType);
    void RegisterTrigger(Type triggerType);
}

/// <summary>
/// Implementation of IPluginHostContext passed to plugins.
/// </summary>
internal class PluginHostContext : IPluginHostContext
{
    private readonly PluginManager _pluginManager;
    private readonly string _pluginId;
    private readonly IAutomationRuleEngine _automationRuleEngine;

    public ILogger Logger { get; }
    public IPluginFileSystem FileSystem { get; }
    public IPluginHttpClient HttpClient { get; }

    public PluginHostContext(PluginManager pluginManager, string pluginId, ILogger logger, PluginPermissions permissions, IAutomationRuleEngine automationRuleEngine)
    {
        _pluginManager = pluginManager;
        _pluginId = pluginId;
        Logger = logger;
        FileSystem = new SandboxedFileSystem(logger, pluginId, permissions);
        HttpClient = new SandboxedHttpClient(logger, pluginId, permissions);
        _automationRuleEngine = automationRuleEngine;
    }

    public void RegisterAction(string name, Type actionType)
    {
        if (!typeof(IAction).IsAssignableFrom(actionType))
        {
            throw new ArgumentException($"Type {actionType.Name} must implement IAction");
        }

        _automationRuleEngine.RegisterActionType(name, actionType);

        Logger.LogInformation("Registered action '{ActionName}' of type {ActionType} from plugin {PluginId}",
            name, actionType.Name, _pluginId);
    }

    public void RegisterTrigger(Type triggerType)
    {
        if (!typeof(global::Loco.Core.Triggers.IRuntimeTrigger).IsAssignableFrom(triggerType))
        {
            throw new ArgumentException("Type must implement IRuntimeTrigger");
        }

        _pluginManager.RegisterTrigger(_pluginId, triggerType);
    }
}

/// <summary>
/// Base class for plugins
/// </summary>
public abstract class PluginBase : IPlugin
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Version { get; }
    public abstract string Description { get; }
    public abstract PluginManifest Manifest { get; }

    protected IPluginHostContext Host { get; private set; }
    protected ILogger Logger => Host.Logger;
    protected IPluginFileSystem FileSystem => Host.FileSystem;
    protected IPluginHttpClient HttpClient => Host.HttpClient;

    public virtual Task InitializeAsync(IPluginHostContext context)
    {
        Host = context;
        return Task.CompletedTask;
    }

    public virtual Task ShutdownAsync()
    {
        Host.HttpClient.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Enforces file system access restrictions for a plugin.
/// </summary>
public class SandboxedFileSystem : IPluginFileSystem
{
    private readonly ILogger _logger;
    private readonly string _pluginId;
    private readonly IReadOnlyList<string> _allowedPaths;
    private readonly bool _allowAllPaths;

    public SandboxedFileSystem(ILogger logger, string pluginId, PluginPermissions permissions)
    {
        _logger = logger;
        _pluginId = pluginId;
        _allowedPaths = permissions?.AllowedPaths ?? new List<string>();
        _allowAllPaths = _allowedPaths.Contains("*");
    }

    private bool IsPathAllowed(string path)
    {
        if (_allowAllPaths) return true;

        if (!_allowedPaths.Any())
        {
            _logger.LogWarning("Plugin {PluginId} attempted file access without any allowed paths configured.", _pluginId);
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        foreach (var allowedPath in _allowedPaths)
        {
            var fullAllowedPath = Path.GetFullPath(allowedPath);
            if (fullPath.StartsWith(fullAllowedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        _logger.LogError("Plugin {PluginId} attempted to access a disallowed path: {Path}", _pluginId, path);
        return false;
    }

    public Task<string> ReadAllTextAsync(string path)
    {
        if (!IsPathAllowed(path)) throw new System.Security.SecurityException($"Plugin {_pluginId} does not have permission to read from {path}.");
        return File.ReadAllTextAsync(path);
    }

    public Task WriteAllTextAsync(string path, string contents)
    {
        if (!IsPathAllowed(path)) throw new System.Security.SecurityException($"Plugin {_pluginId} does not have permission to write to {path}.");
        return File.WriteAllTextAsync(path, contents);
    }

    public bool FileExists(string path)
    {
        if (!IsPathAllowed(path)) throw new System.Security.SecurityException($"Plugin {_pluginId} does not have permission to check existence of {path}.");
        return File.Exists(path);
    }
}

/// <summary>
/// Enforces network access restrictions for a plugin.
/// </summary>
public class SandboxedHttpClient : IPluginHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly string _pluginId;
    private readonly IReadOnlyList<string> _allowedDomains;
    private readonly bool _allowAllDomains;

    public SandboxedHttpClient(ILogger logger, string pluginId, PluginPermissions permissions)
    {
        _httpClient = new HttpClient();
        _logger = logger;
        _pluginId = pluginId;
        _allowedDomains = permissions?.AllowedDomains ?? new List<string>();
        _allowAllDomains = _allowedDomains.Contains("*");
    }

    private bool IsDomainAllowed(string url)
    {
        if (_allowAllDomains) return true;

        if (!_allowedDomains.Any())
        {
            _logger.LogWarning("Plugin {PluginId} attempted network access without any allowed domains configured.", _pluginId);
            return false;
        }

        try
        {
            var uri = new Uri(url);
            var host = uri.Host;

            if (_allowedDomains.Any(d => host.Equals(d, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        catch (UriFormatException ex)
        { 
            _logger.LogError(ex, "Invalid URL format provided by plugin {PluginId}: {Url}", _pluginId, url);
            return false;
        }

        _logger.LogError("Plugin {PluginId} attempted to access a disallowed domain: {Url}", _pluginId, url);
        return false;
    }

    public Task<HttpResponseMessage> GetAsync(string requestUri)
    {
        if (!IsDomainAllowed(requestUri)) throw new System.Security.SecurityException($"Plugin {_pluginId} does not have permission to access {requestUri}.");
        return _httpClient.GetAsync(requestUri);
    }

    public Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content)
    {
        if (!IsDomainAllowed(requestUri)) throw new System.Security.SecurityException($"Plugin {_pluginId} does not have permission to access {requestUri}.");
        return _httpClient.PostAsync(requestUri, content);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}