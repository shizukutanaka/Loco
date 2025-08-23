using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Loco.Core.Interfaces;

namespace Loco.Core.Plugins;

/// <summary>
/// High-performance plugin loader with isolated AssemblyLoadContexts
/// Implements hot-reload and memory-efficient loading
/// </summary>
public sealed class AdvancedPluginLoader : IDisposable
{
    private readonly ILogger<AdvancedPluginLoader> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, PluginContext> _loadedPlugins;
    private readonly FileSystemWatcher _watcher;
    private readonly string _pluginsDirectory;
    private readonly SemaphoreSlim _loadSemaphore;
    
    private class PluginContext : IDisposable
    {
        public string Id { get; set; }
        public string FilePath { get; set; }
        public AssemblyLoadContext LoadContext { get; set; }
        public Assembly Assembly { get; set; }
        public IPlugin Instance { get; set; }
        public PluginMetadata Metadata { get; set; }
        public DateTime LoadTime { get; set; }
        public long MemoryUsage { get; set; }
        
        public void Dispose()
        {
            try
            {
                (Instance as IDisposable)?.Dispose();
                LoadContext?.Unload();
            }
            catch { /* Best effort cleanup */ }
        }
    }
    
    public class PluginMetadata
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public string[] Dependencies { get; set; }
        public Dictionary<string, object> Configuration { get; set; }
    }
    
    public AdvancedPluginLoader(
        ILogger<AdvancedPluginLoader> logger,
        IServiceProvider serviceProvider,
        string pluginsDirectory = null)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _loadedPlugins = new ConcurrentDictionary<string, PluginContext>();
        _loadSemaphore = new SemaphoreSlim(1, 1);
        
        _pluginsDirectory = pluginsDirectory ?? PluginPaths.GetEffectivePluginsDirectory();
        
        if (!Directory.Exists(_pluginsDirectory))
        {
            Directory.CreateDirectory(_pluginsDirectory);
            _logger.LogInformation("Created plugins directory: {Directory}", _pluginsDirectory);
        }
        
        // Setup file watcher for hot reload
        _watcher = new FileSystemWatcher(_pluginsDirectory, "*.dll")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        
        _watcher.Changed += OnPluginFileChanged;
        _watcher.Created += OnPluginFileChanged;
        _watcher.Deleted += OnPluginFileDeleted;
        
        _logger.LogInformation("Advanced plugin loader initialized. Directory: {Directory}", _pluginsDirectory);
    }
    
    /// <summary>
    /// Load all plugins from the plugins directory
    /// </summary>
    public async Task<LoadResult> LoadAllPluginsAsync(CancellationToken cancellationToken = default)
    {
        var result = new LoadResult();
        var pluginFiles = Directory.GetFiles(_pluginsDirectory, "*.dll", SearchOption.AllDirectories);
        
        _logger.LogInformation("Found {Count} potential plugin files", pluginFiles.Length);
        
        var tasks = pluginFiles.Select(async file =>
        {
            try
            {
                var plugin = await LoadPluginAsync(file, cancellationToken);
                if (plugin != null)
                {
                    result.LoadedPlugins.Add(plugin.Name);
                    _logger.LogInformation("Successfully loaded plugin: {Name} v{Version}", 
                        plugin.Name, plugin.Version);
                }
            }
            catch (Exception ex)
            {
                result.FailedPlugins.Add(new FailedPlugin
                {
                    FilePath = file,
                    Error = ex.Message
                });
                _logger.LogError(ex, "Failed to load plugin from {File}", file);
            }
        });
        
        await Task.WhenAll(tasks);
        
        result.TotalPlugins = pluginFiles.Length;
        result.SuccessCount = result.LoadedPlugins.Count;
        result.FailureCount = result.FailedPlugins.Count;
        
        return result;
    }
    
    /// <summary>
    /// Load a single plugin with isolation
    /// </summary>
    public async Task<IPlugin> LoadPluginAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Plugin file not found: {filePath}");
        }
        
        await _loadSemaphore.WaitAsync(cancellationToken);
        try
        {
            var fileInfo = new FileInfo(filePath);
            var pluginId = Path.GetFileNameWithoutExtension(filePath);
            
            // Check if already loaded
            if (_loadedPlugins.TryGetValue(pluginId, out var existing))
            {
                _logger.LogDebug("Plugin {Id} already loaded, returning cached instance", pluginId);
                return existing.Instance;
            }
            
            // Create isolated load context
            var loadContext = new PluginLoadContext(filePath);
            
            // Load assembly
            var assembly = loadContext.LoadFromAssemblyPath(filePath);
            
            // Find IPlugin implementations
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .ToList();
            
            if (pluginTypes.Count == 0)
            {
                throw new InvalidOperationException($"No IPlugin implementations found in {filePath}");
            }
            
            if (pluginTypes.Count > 1)
            {
                _logger.LogWarning("Multiple IPlugin implementations found in {File}, using first", filePath);
            }
            
            // Create instance
            var pluginType = pluginTypes.First();
            var instance = Activator.CreateInstance(pluginType) as IPlugin;
            
            if (instance == null)
            {
                throw new InvalidOperationException($"Failed to create instance of {pluginType.Name}");
            }
            
            // Initialize plugin
            await instance.InitializeAsync(_serviceProvider, cancellationToken);
            
            // Extract metadata
            var metadata = ExtractMetadata(assembly, instance);
            
            // Store in context
            var context = new PluginContext
            {
                Id = pluginId,
                FilePath = filePath,
                LoadContext = loadContext,
                Assembly = assembly,
                Instance = instance,
                Metadata = metadata,
                LoadTime = DateTime.UtcNow,
                MemoryUsage = GC.GetTotalMemory(false)
            };
            
            _loadedPlugins.TryAdd(pluginId, context);
            
            _logger.LogInformation("Loaded plugin: {Name} v{Version} from {File}",
                instance.Name, instance.Version, filePath);
            
            return instance;
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }
    
    /// <summary>
    /// Unload a plugin and free resources
    /// </summary>
    public async Task UnloadPluginAsync(string pluginId)
    {
        if (_loadedPlugins.TryRemove(pluginId, out var context))
        {
            _logger.LogInformation("Unloading plugin: {Id}", pluginId);
            
            try
            {
                // Shutdown plugin
                if (context.Instance is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (context.Instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                
                // Unload assembly context
                context.LoadContext?.Unload();
                
                // Force GC to clean up
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                _logger.LogInformation("Plugin {Id} unloaded successfully", pluginId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unloading plugin {Id}", pluginId);
            }
        }
    }
    
    /// <summary>
    /// Reload a plugin (hot reload)
    /// </summary>
    public async Task<IPlugin> ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        if (_loadedPlugins.TryGetValue(pluginId, out var context))
        {
            var filePath = context.FilePath;
            
            // Unload old version
            await UnloadPluginAsync(pluginId);
            
            // Wait a bit for file system to stabilize
            await Task.Delay(100, cancellationToken);
            
            // Load new version
            return await LoadPluginAsync(filePath, cancellationToken);
        }
        
        throw new InvalidOperationException($"Plugin {pluginId} not found");
    }
    
    /// <summary>
    /// Get all loaded plugins
    /// </summary>
    public IEnumerable<IPlugin> GetLoadedPlugins()
    {
        return _loadedPlugins.Values.Select(c => c.Instance);
    }
    
    /// <summary>
    /// Get plugin by ID
    /// </summary>
    public IPlugin GetPlugin(string pluginId)
    {
        return _loadedPlugins.TryGetValue(pluginId, out var context) ? context.Instance : null;
    }
    
    /// <summary>
    /// Get plugin statistics
    /// </summary>
    public PluginStatistics GetStatistics()
    {
        var contexts = _loadedPlugins.Values.ToList();
        
        return new PluginStatistics
        {
            TotalPlugins = contexts.Count,
            TotalMemoryUsage = contexts.Sum(c => c.MemoryUsage),
            Plugins = contexts.Select(c => new PluginInfo
            {
                Id = c.Id,
                Name = c.Instance.Name,
                Version = c.Instance.Version,
                LoadTime = c.LoadTime,
                MemoryUsage = c.MemoryUsage,
                FilePath = c.FilePath,
                Metadata = c.Metadata
            }).ToList()
        };
    }
    
    private PluginMetadata ExtractMetadata(Assembly assembly, IPlugin instance)
    {
        var metadata = new PluginMetadata
        {
            Name = instance.Name,
            Version = instance.Version,
            Configuration = new Dictionary<string, object>()
        };
        
        // Try to extract additional metadata from attributes
        var assemblyAttributes = assembly.GetCustomAttributes();
        
        foreach (var attr in assemblyAttributes)
        {
            switch (attr)
            {
                case AssemblyCompanyAttribute company:
                    metadata.Author = company.Company;
                    break;
                case AssemblyDescriptionAttribute description:
                    metadata.Description = description.Description;
                    break;
            }
        }
        
        // Extract dependencies
        metadata.Dependencies = assembly.GetReferencedAssemblies()
            .Select(a => $"{a.Name} v{a.Version}")
            .ToArray();
        
        return metadata;
    }
    
    private async void OnPluginFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            _logger.LogInformation("Plugin file changed: {File}", e.FullPath);
            
            // Debounce file changes
            await Task.Delay(500);
            
            var pluginId = Path.GetFileNameWithoutExtension(e.FullPath);
            
            if (_loadedPlugins.ContainsKey(pluginId))
            {
                _logger.LogInformation("Reloading plugin: {Id}", pluginId);
                await ReloadPluginAsync(pluginId);
            }
            else
            {
                _logger.LogInformation("Loading new plugin: {File}", e.FullPath);
                await LoadPluginAsync(e.FullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling plugin file change");
        }
    }
    
    private async void OnPluginFileDeleted(object sender, FileSystemEventArgs e)
    {
        try
        {
            var pluginId = Path.GetFileNameWithoutExtension(e.FullPath);
            
            if (_loadedPlugins.ContainsKey(pluginId))
            {
                _logger.LogInformation("Plugin file deleted, unloading: {Id}", pluginId);
                await UnloadPluginAsync(pluginId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling plugin file deletion");
        }
    }
    
    public void Dispose()
    {
        _watcher?.Dispose();
        _loadSemaphore?.Dispose();
        
        // Unload all plugins
        foreach (var context in _loadedPlugins.Values)
        {
            context.Dispose();
        }
        
        _loadedPlugins.Clear();
    }
}

/// <summary>
/// Custom AssemblyLoadContext for plugin isolation
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    
    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }
    
    protected override Assembly Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
    }
    
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
    }
}

// Result classes
public class LoadResult
{
    public int TotalPlugins { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<string> LoadedPlugins { get; set; } = new();
    public List<FailedPlugin> FailedPlugins { get; set; } = new();
}

public class FailedPlugin
{
    public string FilePath { get; set; }
    public string Error { get; set; }
}

public class PluginStatistics
{
    public int TotalPlugins { get; set; }
    public long TotalMemoryUsage { get; set; }
    public List<PluginInfo> Plugins { get; set; }
}

public class PluginInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Version { get; set; }
    public DateTime LoadTime { get; set; }
    public long MemoryUsage { get; set; }
    public string FilePath { get; set; }
    public AdvancedPluginLoader.PluginMetadata Metadata { get; set; }
}

/// <summary>
/// Base interface for plugins
/// </summary>
public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
}
