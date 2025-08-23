using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.FileWatcher;

namespace Loco.Core.HotReload;

/// <summary>
/// Hot reload service for dynamic code updates without restart
/// </summary>
public class HotReloadService : IDisposable
{
    private readonly ILogger<HotReloadService> _logger;
    private readonly SmartFileWatcher _fileWatcher;
    private readonly Dictionary<string, AssemblyLoadContext> _loadContexts;
    private readonly Dictionary<string, Type> _reloadableTypes;
    private readonly Dictionary<string, object> _instances;
    private readonly SemaphoreSlim _reloadSemaphore;
    private bool _disposed;

    // Events
    public event EventHandler<ReloadEventArgs> BeforeReload;
    public event EventHandler<ReloadEventArgs> AfterReload;
    public event EventHandler<ReloadErrorEventArgs> ReloadError;

    public HotReloadService(ILogger<HotReloadService> logger = null)
    {
        _logger = logger;
        _fileWatcher = new SmartFileWatcher(null, TimeSpan.FromSeconds(1));
        _loadContexts = new Dictionary<string, AssemblyLoadContext>();
        _reloadableTypes = new Dictionary<string, Type>();
        _instances = new Dictionary<string, object>();
        _reloadSemaphore = new SemaphoreSlim(1, 1);

        _fileWatcher.FileChanged += OnFileChanged;
    }

    /// <summary>
    /// Register an assembly for hot reload
    /// </summary>
    public void RegisterAssembly(string assemblyPath, params string[] typeNames)
    {
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Assembly not found: {assemblyPath}");

        var context = CreateLoadContext(assemblyPath);
        var assembly = context.LoadFromAssemblyPath(assemblyPath);

        foreach (var typeName in typeNames)
        {
            var type = assembly.GetType(typeName);
            if (type != null)
            {
                _reloadableTypes[typeName] = type;
                _logger?.LogInformation("Registered type for hot reload: {TypeName}", typeName);
            }
        }

        _loadContexts[assemblyPath] = context;
        _fileWatcher.WatchFile(assemblyPath);

        _logger?.LogInformation("Registered assembly for hot reload: {Path}", assemblyPath);
    }

    /// <summary>
    /// Register a directory for plugin hot reload
    /// </summary>
    public void RegisterPluginDirectory(string directory, string searchPattern = "*.dll")
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Directory not found: {directory}");

        var options = new WatchOptions
        {
            Filter = searchPattern,
            IncludeSubdirectories = true,
            TrackContentChanges = true
        };

        _fileWatcher.WatchDirectory(directory, options);

        // Load initial plugins
        var pluginFiles = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);
        foreach (var pluginFile in pluginFiles)
        {
            LoadPlugin(pluginFile);
        }

        _logger?.LogInformation("Registered plugin directory for hot reload: {Directory}", directory);
    }

    /// <summary>
    /// Create or get an instance of a reloadable type
    /// </summary>
    public T GetInstance<T>(string typeName, params object[] constructorArgs) where T : class
    {
        if (!_reloadableTypes.TryGetValue(typeName, out var type))
            throw new InvalidOperationException($"Type not registered: {typeName}");

        var key = $"{typeName}_{string.Join("_", constructorArgs.Select(a => a?.GetHashCode() ?? 0))}";

        if (!_instances.TryGetValue(key, out var instance))
        {
            instance = Activator.CreateInstance(type, constructorArgs);
            _instances[key] = instance;
        }

        return instance as T;
    }

    /// <summary>
    /// Manually trigger a reload
    /// </summary>
    public async Task<bool> ReloadAsync(string assemblyPath)
    {
        await _reloadSemaphore.WaitAsync();
        try
        {
            return await PerformReload(assemblyPath);
        }
        finally
        {
            _reloadSemaphore.Release();
        }
    }

    private async void OnFileChanged(object sender, FileChangedEventArgs e)
    {
        if (Path.GetExtension(e.FullPath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            await ReloadAsync(e.FullPath);
        }
    }

    private async Task<bool> PerformReload(string assemblyPath)
    {
        try
        {
            _logger?.LogInformation("Starting hot reload for: {Path}", assemblyPath);

            var reloadArgs = new ReloadEventArgs { AssemblyPath = assemblyPath };
            BeforeReload?.Invoke(this, reloadArgs);

            // Wait for file to be fully written
            await WaitForFileReady(assemblyPath);

            // Unload old context
            if (_loadContexts.TryGetValue(assemblyPath, out var oldContext))
            {
                // Save state of existing instances
                var savedStates = SaveInstanceStates();

                // Clear instances
                _instances.Clear();

                // Unload old assembly
                oldContext.Unload();
                _loadContexts.Remove(assemblyPath);

                // Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            // Load new assembly
            var newContext = CreateLoadContext(assemblyPath);
            var assembly = newContext.LoadFromAssemblyPath(assemblyPath);
            _loadContexts[assemblyPath] = newContext;

            // Update type references
            var typesToReload = _reloadableTypes.Where(kvp => 
                kvp.Value.Assembly.Location.Equals(assemblyPath, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key).ToList();

            foreach (var typeName in typesToReload)
            {
                var newType = assembly.GetType(typeName);
                if (newType != null)
                {
                    _reloadableTypes[typeName] = newType;
                }
            }

            // Check for new plugins
            if (IsPlugin(assembly))
            {
                RegisterPlugin(assembly);
            }

            AfterReload?.Invoke(this, reloadArgs);
            _logger?.LogInformation("Hot reload completed for: {Path}", assemblyPath);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Hot reload failed for: {Path}", assemblyPath);
            
            ReloadError?.Invoke(this, new ReloadErrorEventArgs 
            { 
                AssemblyPath = assemblyPath, 
                Error = ex 
            });

            return false;
        }
    }

    private void LoadPlugin(string pluginPath)
    {
        try
        {
            var context = CreateLoadContext(pluginPath);
            var assembly = context.LoadFromAssemblyPath(pluginPath);
            
            if (IsPlugin(assembly))
            {
                RegisterPlugin(assembly);
                _loadContexts[pluginPath] = context;
                _logger?.LogInformation("Loaded plugin: {Path}", pluginPath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load plugin: {Path}", pluginPath);
        }
    }

    private bool IsPlugin(Assembly assembly)
    {
        // Check if assembly contains plugin interface implementation
        var pluginInterface = typeof(IPlugin);
        return assembly.GetTypes().Any(t => 
            pluginInterface.IsAssignableFrom(t) && 
            !t.IsInterface && 
            !t.IsAbstract);
    }

    private void RegisterPlugin(Assembly assembly)
    {
        var pluginInterface = typeof(IPlugin);
        var pluginTypes = assembly.GetTypes()
            .Where(t => pluginInterface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var pluginType in pluginTypes)
        {
            _reloadableTypes[pluginType.FullName] = pluginType;
            
            // Auto-instantiate plugin
            try
            {
                var plugin = Activator.CreateInstance(pluginType) as IPlugin;
                plugin?.Initialize();
                _instances[pluginType.FullName] = plugin;
                
                _logger?.LogInformation("Initialized plugin: {TypeName}", pluginType.FullName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize plugin: {TypeName}", pluginType.FullName);
            }
        }
    }

    private AssemblyLoadContext CreateLoadContext(string name)
    {
        return new CollectibleAssemblyLoadContext(name);
    }

    private async Task WaitForFileReady(string filePath, int maxAttempts = 10)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    // File is ready
                    return;
                }
            }
            catch (IOException)
            {
                // File is still being written
                await Task.Delay(100);
            }
        }

        throw new TimeoutException($"File not ready after {maxAttempts} attempts: {filePath}");
    }

    private Dictionary<string, object> SaveInstanceStates()
    {
        var states = new Dictionary<string, object>();
        
        foreach (var kvp in _instances)
        {
            if (kvp.Value is IStateful stateful)
            {
                states[kvp.Key] = stateful.SaveState();
            }
        }

        return states;
    }

    private void RestoreInstanceStates(Dictionary<string, object> states)
    {
        foreach (var kvp in states)
        {
            if (_instances.TryGetValue(kvp.Key, out var instance) && instance is IStateful stateful)
            {
                stateful.RestoreState(kvp.Value);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _fileWatcher?.Dispose();
        _reloadSemaphore?.Dispose();

        foreach (var context in _loadContexts.Values)
        {
            context.Unload();
        }

        _loadContexts.Clear();
        _reloadableTypes.Clear();
        _instances.Clear();

        _disposed = true;
    }
}

// Supporting classes
public class CollectibleAssemblyLoadContext : AssemblyLoadContext
{
    public CollectibleAssemblyLoadContext(string name) : base(name, isCollectible: true)
    {
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        // Return null to use default resolution
        return null;
    }
}

public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    void Initialize();
    void Shutdown();
}

public interface IStateful
{
    object SaveState();
    void RestoreState(object state);
}

public class ReloadEventArgs : EventArgs
{
    public string AssemblyPath { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ReloadErrorEventArgs : ReloadEventArgs
{
    public Exception Error { get; set; }
}

/// <summary>
/// Attribute to mark types for automatic hot reload
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class HotReloadableAttribute : Attribute
{
    public bool PreserveState { get; set; } = true;
    public string Group { get; set; } = "default";
}

/// <summary>
/// Hot reload manager for application-wide coordination
/// </summary>
public class HotReloadManager
{
    private static readonly Lazy<HotReloadManager> _instance = new(() => new HotReloadManager());
    private readonly HotReloadService _service;
    private readonly ILogger<HotReloadManager> _logger;

    public static HotReloadManager Instance => _instance.Value;

    private HotReloadManager()
    {
        _service = new HotReloadService();
    }

    public void EnableForAssembly(Assembly assembly)
    {
        var hotReloadableTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<HotReloadableAttribute>() != null)
            .ToList();

        if (hotReloadableTypes.Any())
        {
            _service.RegisterAssembly(assembly.Location, hotReloadableTypes.Select(t => t.FullName).ToArray());
            _logger?.LogInformation("Enabled hot reload for {Count} types in {Assembly}", 
                hotReloadableTypes.Count, assembly.GetName().Name);
        }
    }

    public void EnableForPlugins(string pluginDirectory)
    {
        _service.RegisterPluginDirectory(pluginDirectory);
    }

    public T GetReloadable<T>(string typeName, params object[] args) where T : class
    {
        return _service.GetInstance<T>(typeName, args);
    }
}
