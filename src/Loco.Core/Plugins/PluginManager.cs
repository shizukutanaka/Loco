using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Plugins
{
    /// <summary>
    /// Plugin manager for loading and managing extensibility plugins
    /// Implements secure plugin loading with isolation and validation
    /// </summary>
    public sealed class PluginManager : IDisposable
    {
        private readonly ILogger<PluginManager> _logger;
        private readonly Dictionary<string, PluginContext> _loadedPlugins;
        private readonly Dictionary<string, IPlugin> _activePlugins;
        private readonly PluginValidator _validator;
        private readonly string _pluginsDirectory;
        private readonly PluginConfiguration _configuration;
        private readonly SemaphoreSlim _loadSemaphore;
        private bool _disposed;

        public PluginManager(
            ILogger<PluginManager> logger = null,
            string pluginsDirectory = null,
            PluginConfiguration configuration = null)
        {
            _logger = logger;
            _pluginsDirectory = PluginPaths.GetEffectivePluginsDirectory(pluginsDirectory);
            _configuration = configuration ?? PluginConfiguration.Default;
            _loadedPlugins = new Dictionary<string, PluginContext>();
            _activePlugins = new Dictionary<string, IPlugin>();
            _validator = new PluginValidator();
            _loadSemaphore = new SemaphoreSlim(1, 1);

            EnsurePluginsDirectory();
        }

        /// <summary>
        /// Effective plugins directory path in use. Exposed for diagnostics/logging.
        /// </summary>
        public string PluginsDirectory => _pluginsDirectory;

        /// <summary>
        /// Load all plugins from the plugins directory
        /// </summary>
        public async Task<PluginLoadResult> LoadPluginsAsync()
        {
            await _loadSemaphore.WaitAsync();
            try
            {
                var result = new PluginLoadResult();
                var pluginFiles = Directory.GetFiles(_pluginsDirectory, "*.dll", SearchOption.AllDirectories);

                foreach (var pluginFile in pluginFiles)
                {
                    try
                    {
                        var loadResult = await LoadPluginAsync(pluginFile);
                        if (loadResult.Success)
                        {
                            result.LoadedPlugins.Add(loadResult.Plugin);
                        }
                        else
                        {
                            result.FailedPlugins.Add(new FailedPlugin
                            {
                                Path = pluginFile,
                                Error = loadResult.Error
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to load plugin from {Path}", pluginFile);
                        result.FailedPlugins.Add(new FailedPlugin
                        {
                            Path = pluginFile,
                            Error = ex.Message
                        });
                    }
                }

                result.Success = result.FailedPlugins.Count == 0;
                result.Message = $"Loaded {result.LoadedPlugins.Count} plugins, {result.FailedPlugins.Count} failed";

                return result;
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }

        /// <summary>
        /// Load a specific plugin
        /// </summary>
        public async Task<SinglePluginLoadResult> LoadPluginAsync(string pluginPath)
        {
            try
            {
                // Validate plugin file
                var validationResult = await _validator.ValidatePluginFileAsync(pluginPath);
                if (!validationResult.IsValid)
                {
                    return new SinglePluginLoadResult
                    {
                        Success = false,
                        Error = $"Plugin validation failed: {string.Join(", ", validationResult.Errors)}"
                    };
                }

                // Create isolated context for plugin
                var pluginContext = new PluginContext(pluginPath);
                var assembly = pluginContext.LoadFromAssemblyPath(pluginPath);

                // Find plugin types
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                    .ToList();

                if (pluginTypes.Count == 0)
                {
                    return new SinglePluginLoadResult
                    {
                        Success = false,
                        Error = "No plugin types found in assembly"
                    };
                }

                // Create plugin instance
                var pluginType = pluginTypes.First();
                var plugin = Activator.CreateInstance(pluginType) as IPlugin;

                if (plugin == null)
                {
                    return new SinglePluginLoadResult
                    {
                        Success = false,
                        Error = "Failed to create plugin instance"
                    };
                }

                // Initialize plugin
                var initResult = await InitializePluginAsync(plugin);
                if (!initResult.Success)
                {
                    return new SinglePluginLoadResult
                    {
                        Success = false,
                        Error = initResult.Error
                    };
                }

                // Register plugin
                var pluginId = plugin.Metadata.Id;
                _loadedPlugins[pluginId] = pluginContext;
                _activePlugins[pluginId] = plugin;

                _logger?.LogInformation("Loaded plugin {Name} ({Id}) from {Path}",
                    plugin.Metadata.Name, pluginId, pluginPath);

                // Raise event
                OnPluginLoaded?.Invoke(plugin);

                return new SinglePluginLoadResult
                {
                    Success = true,
                    Plugin = plugin
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load plugin from {Path}", pluginPath);
                return new SinglePluginLoadResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Unload a plugin
        /// </summary>
        public async Task<bool> UnloadPluginAsync(string pluginId)
        {
            try
            {
                if (!_activePlugins.TryGetValue(pluginId, out var plugin))
                {
                    return false;
                }

                // Shutdown plugin
                await plugin.ShutdownAsync();

                // Remove from active plugins
                _activePlugins.Remove(pluginId);

                // Unload context if exists
                if (_loadedPlugins.TryGetValue(pluginId, out var context))
                {
                    context.Unload();
                    _loadedPlugins.Remove(pluginId);
                }

                _logger?.LogInformation("Unloaded plugin {Id}", pluginId);

                // Raise event
                OnPluginUnloaded?.Invoke(pluginId);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to unload plugin {Id}", pluginId);
                return false;
            }
        }

        /// <summary>
        /// Get all loaded plugins
        /// </summary>
        public IEnumerable<IPlugin> GetLoadedPlugins()
        {
            return _activePlugins.Values.ToList();
        }

        /// <summary>
        /// Get a specific plugin
        /// </summary>
        public IPlugin GetPlugin(string pluginId)
        {
            return _activePlugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
        }

        /// <summary>
        /// Execute a plugin command
        /// </summary>
        public async Task<PluginExecutionResult> ExecutePluginCommandAsync(
            string pluginId,
            string command,
            Dictionary<string, object> parameters = null)
        {
            try
            {
                if (!_activePlugins.TryGetValue(pluginId, out var plugin))
                {
                    return new PluginExecutionResult
                    {
                        Success = false,
                        Error = "Plugin not found"
                    };
                }

                // Check if plugin supports the command
                if (!plugin.GetSupportedCommands().Contains(command))
                {
                    return new PluginExecutionResult
                    {
                        Success = false,
                        Error = $"Plugin does not support command: {command}"
                    };
                }

                // Execute command
                var result = await plugin.ExecuteAsync(command, parameters ?? new Dictionary<string, object>());

                return new PluginExecutionResult
                {
                    Success = result.Success,
                    Result = result.Result,
                    Error = result.Error
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to execute plugin command {Command} on {PluginId}",
                    command, pluginId);

                return new PluginExecutionResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Reload all plugins
        /// </summary>
        public async Task<PluginLoadResult> ReloadPluginsAsync()
        {
            // Unload all existing plugins
            var pluginIds = _activePlugins.Keys.ToList();
            foreach (var pluginId in pluginIds)
            {
                await UnloadPluginAsync(pluginId);
            }

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Load plugins again
            return await LoadPluginsAsync();
        }

        /// <summary>
        /// Install a new plugin
        /// </summary>
        public async Task<PluginInstallResult> InstallPluginAsync(string packagePath)
        {
            try
            {
                // Validate package
                var validationResult = await _validator.ValidatePluginPackageAsync(packagePath);
                if (!validationResult.IsValid)
                {
                    return new PluginInstallResult
                    {
                        Success = false,
                        Error = $"Package validation failed: {string.Join(", ", validationResult.Errors)}"
                    };
                }

                // Extract package to plugins directory
                var pluginName = Path.GetFileNameWithoutExtension(packagePath);
                var targetDirectory = Path.Combine(_pluginsDirectory, pluginName);

                if (Directory.Exists(targetDirectory))
                {
                    if (!_configuration.AllowOverwrite)
                    {
                        return new PluginInstallResult
                        {
                            Success = false,
                            Error = "Plugin already exists"
                        };
                    }

                    Directory.Delete(targetDirectory, true);
                }

                Directory.CreateDirectory(targetDirectory);

                // Extract files
                if (packagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(packagePath, targetDirectory);
                }
                else
                {
                    // Copy single DLL
                    var targetPath = Path.Combine(targetDirectory, Path.GetFileName(packagePath));
                    File.Copy(packagePath, targetPath);
                }

                // Load the installed plugin
                var pluginFiles = Directory.GetFiles(targetDirectory, "*.dll");
                if (pluginFiles.Length == 0)
                {
                    return new PluginInstallResult
                    {
                        Success = false,
                        Error = "No plugin DLL found in package"
                    };
                }

                var loadResult = await LoadPluginAsync(pluginFiles[0]);
                if (!loadResult.Success)
                {
                    return new PluginInstallResult
                    {
                        Success = false,
                        Error = loadResult.Error
                    };
                }

                return new PluginInstallResult
                {
                    Success = true,
                    Plugin = loadResult.Plugin,
                    InstalledPath = targetDirectory
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to install plugin from {Path}", packagePath);
                return new PluginInstallResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Uninstall a plugin
        /// </summary>
        public async Task<bool> UninstallPluginAsync(string pluginId)
        {
            try
            {
                // Unload plugin first
                await UnloadPluginAsync(pluginId);

                // Find plugin directory
                var plugin = GetPlugin(pluginId);
                if (plugin != null)
                {
                    var pluginPath = plugin.Metadata.AssemblyPath;
                    var pluginDirectory = Path.GetDirectoryName(pluginPath);

                    if (Directory.Exists(pluginDirectory))
                    {
                        Directory.Delete(pluginDirectory, true);
                    }
                }

                _logger?.LogInformation("Uninstalled plugin {Id}", pluginId);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to uninstall plugin {Id}", pluginId);
                return false;
            }
        }

        // Events
        public event Action<IPlugin> OnPluginLoaded;
        public event Action<string> OnPluginUnloaded;
        public event Action<string, Exception> OnPluginError;

        // Private methods
        private async Task<PluginInitResult> InitializePluginAsync(IPlugin plugin)
        {
            try
            {
                var context = new PluginInitializationContext
                {
                    PluginsDirectory = _pluginsDirectory,
                    Configuration = _configuration,
                    Logger = _logger
                };

                await plugin.InitializeAsync(context);

                return new PluginInitResult { Success = true };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize plugin {Name}", plugin.Metadata.Name);
                return new PluginInitResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        private void EnsurePluginsDirectory()
        {
            var ensured = PluginPaths.EnsureDirectory(_pluginsDirectory);
            if (!string.IsNullOrEmpty(ensured))
            {
                _logger?.LogInformation("Using plugins directory at {Path}", ensured);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Unload all plugins
            var pluginIds = _activePlugins.Keys.ToList();
            foreach (var pluginId in pluginIds)
            {
                try
                {
                    UnloadPluginAsync(pluginId).Wait();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error unloading plugin {Id} during disposal", pluginId);
                }
            }

            _loadSemaphore?.Dispose();
        }

        /// <summary>
        /// Custom AssemblyLoadContext for plugin isolation
        /// </summary>
        private class PluginContext : AssemblyLoadContext
        {
            private readonly AssemblyDependencyResolver _resolver;

            public PluginContext(string pluginPath) : base(isCollectible: true)
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
    }

    /// <summary>
    /// Plugin interface that all plugins must implement
    /// </summary>
    public interface IPlugin
    {
        PluginMetadata Metadata { get; }
        Task InitializeAsync(PluginInitializationContext context);
        Task ShutdownAsync();
        Task<PluginCommandResult> ExecuteAsync(string command, Dictionary<string, object> parameters);
        IEnumerable<string> GetSupportedCommands();
    }

    /// <summary>
    /// Plugin metadata
    /// </summary>
    public class PluginMetadata
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Website { get; set; }
        public string[] Dependencies { get; set; }
        public string AssemblyPath { get; set; }
        public Dictionary<string, string> Properties { get; set; }
    }

    /// <summary>
    /// Plugin initialization context
    /// </summary>
    public class PluginInitializationContext
    {
        public string PluginsDirectory { get; set; }
        public PluginConfiguration Configuration { get; set; }
        public ILogger Logger { get; set; }
    }

    /// <summary>
    /// Plugin configuration
    /// </summary>
    public class PluginConfiguration
    {
        public bool EnableAutoLoad { get; set; } = true;
        public bool AllowOverwrite { get; set; } = false;
        public bool ValidateSignatures { get; set; } = true;
        public int MaxPluginSize { get; set; } = 10 * 1024 * 1024; // 10MB
        public string[] AllowedExtensions { get; set; } = { ".dll", ".zip" };
        public Dictionary<string, object> CustomSettings { get; set; }

        public static PluginConfiguration Default => new PluginConfiguration();
    }

    /// <summary>
    /// Plugin validator
    /// </summary>
    public class PluginValidator
    {
        public async Task<PluginValidationResult> ValidatePluginFileAsync(string path)
        {
            var errors = new List<string>();

            // Check file exists
            if (!File.Exists(path))
            {
                errors.Add("File does not exist");
            }

            // Check file size
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length > 10 * 1024 * 1024) // 10MB limit
            {
                errors.Add("File size exceeds maximum allowed");
            }

            // Check file extension
            if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Invalid file extension");
            }

            // TODO: Add signature validation

            return await Task.FromResult(new PluginValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            });
        }

        public async Task<PluginValidationResult> ValidatePluginPackageAsync(string path)
        {
            var errors = new List<string>();

            // Check file exists
            if (!File.Exists(path))
            {
                errors.Add("Package file does not exist");
            }

            // Check supported formats
            var extension = Path.GetExtension(path).ToLower();
            if (extension != ".dll" && extension != ".zip")
            {
                errors.Add("Unsupported package format");
            }

            // TODO: Add package content validation

            return await Task.FromResult(new PluginValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            });
        }
    }

    // Result classes
    public class PluginLoadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<IPlugin> LoadedPlugins { get; set; } = new List<IPlugin>();
        public List<FailedPlugin> FailedPlugins { get; set; } = new List<FailedPlugin>();
    }

    public class SinglePluginLoadResult
    {
        public bool Success { get; set; }
        public IPlugin Plugin { get; set; }
        public string Error { get; set; }
    }

    public class FailedPlugin
    {
        public string Path { get; set; }
        public string Error { get; set; }
    }

    public class PluginInitResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    public class PluginExecutionResult
    {
        public bool Success { get; set; }
        public object Result { get; set; }
        public string Error { get; set; }
    }

    public class PluginCommandResult
    {
        public bool Success { get; set; }
        public object Result { get; set; }
        public string Error { get; set; }
    }

    public class PluginInstallResult
    {
        public bool Success { get; set; }
        public IPlugin Plugin { get; set; }
        public string InstalledPath { get; set; }
        public string Error { get; set; }
    }

    public class PluginValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; }
    }
}
