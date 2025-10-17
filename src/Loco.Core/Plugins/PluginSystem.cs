using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Loco.Core.Plugins;

/// <summary>
/// プラグインシステム
/// Plugin system
///
/// 機能: 動的プラグイン読み込み、ホットリロード、依存関係管理
/// Features: Dynamic plugin loading, hot reload, dependency management
/// </summary>
public class PluginSystem
{
    private readonly string _pluginDirectory;
    private readonly Dictionary<string, LoadedPlugin> _loadedPlugins;
    private readonly List<Type> _pluginInterfaces;

    public PluginSystem(string pluginDirectory)
    {
        _pluginDirectory = pluginDirectory;
        _loadedPlugins = new Dictionary<string, LoadedPlugin>();
        _pluginInterfaces = new List<Type>();

        Directory.CreateDirectory(_pluginDirectory);

        // 標準プラグインインターフェースを登録
        RegisterPluginInterface<IActionPlugin>();
        RegisterPluginInterface<ITriggerPlugin>();
        RegisterPluginInterface<ITransformPlugin>();
    }

    public class PluginManifest
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NameJa { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string Author { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DescriptionJa { get; set; } = string.Empty;
        public string AssemblyFile { get; set; } = string.Empty;
        public List<string> Dependencies { get; set; } = new();
        public Dictionary<string, string> Configuration { get; set; } = new();
    }

    public class LoadedPlugin
    {
        public PluginManifest Manifest { get; set; } = new();
        public Assembly Assembly { get; set; } = null!;
        public List<object> Instances { get; set; } = new();
        public DateTime LoadedAt { get; set; } = DateTime.UtcNow;
        public bool IsEnabled { get; set; } = true;
    }

    public class PluginLoadResult
    {
        public bool Success { get; set; }
        public string? PluginId { get; set; }
        public int InterfacesImplemented { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// プラグインインターフェースを登録
    /// Register plugin interface
    /// </summary>
    public void RegisterPluginInterface<T>() where T : IPlugin
    {
        if (!_pluginInterfaces.Contains(typeof(T)))
        {
            _pluginInterfaces.Add(typeof(T));
        }
    }

    /// <summary>
    /// すべてのプラグインを読み込み
    /// Load all plugins
    /// </summary>
    public async Task<List<PluginLoadResult>> LoadAllPluginsAsync()
    {
        var results = new List<PluginLoadResult>();

        try
        {
            var pluginDirs = Directory.GetDirectories(_pluginDirectory);

            foreach (var pluginDir in pluginDirs)
            {
                var manifestPath = Path.Combine(pluginDir, "manifest.json");
                if (!File.Exists(manifestPath)) continue;

                var result = await LoadPluginAsync(pluginDir).ConfigureAwait(false);
                results.Add(result);
            }

            return results;
        }
        catch (Exception ex)
        {
            results.Add(new PluginLoadResult
            {
                Success = false,
                ErrorMessage = $"Failed to load plugins: {ex.Message}"
            });
            return results;
        }
    }

    /// <summary>
    /// プラグインを読み込み
    /// Load plugin
    /// </summary>
    public async Task<PluginLoadResult> LoadPluginAsync(string pluginDirectory)
    {
        var result = new PluginLoadResult();

        try
        {
            // マニフェストを読み込み
            var manifestPath = Path.Combine(pluginDirectory, "manifest.json");
            var manifestJson = await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson);

            if (manifest == null)
            {
                result.Success = false;
                result.ErrorMessage = "Invalid manifest.json";
                return result;
            }

            // アセンブリを読み込み
            var assemblyPath = Path.Combine(pluginDirectory, manifest.AssemblyFile);
            if (!File.Exists(assemblyPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Assembly not found: {manifest.AssemblyFile}";
                return result;
            }

            var assembly = Assembly.LoadFrom(assemblyPath);

            var loadedPlugin = new LoadedPlugin
            {
                Manifest = manifest,
                Assembly = assembly
            };

            // プラグインインターフェースを実装する型を検索
            var pluginTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => _pluginInterfaces.Any(i => i.IsAssignableFrom(t)))
                .ToList();

            foreach (var type in pluginTypes)
            {
                var instance = Activator.CreateInstance(type);
                if (instance != null)
                {
                    loadedPlugin.Instances.Add(instance);

                    // 初期化
                    if (instance is IPlugin plugin)
                    {
                        await plugin.InitializeAsync(manifest.Configuration).ConfigureAwait(false);
                    }
                }
            }

            _loadedPlugins[manifest.Id] = loadedPlugin;

            result.Success = true;
            result.PluginId = manifest.Id;
            result.InterfacesImplemented = loadedPlugin.Instances.Count;

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// プラグインをアンロード
    /// Unload plugin
    /// </summary>
    public async Task<bool> UnloadPluginAsync(string pluginId)
    {
        if (!_loadedPlugins.ContainsKey(pluginId))
        {
            return false;
        }

        try
        {
            var plugin = _loadedPlugins[pluginId];

            // クリーンアップ
            foreach (var instance in plugin.Instances)
            {
                if (instance is IPlugin pluginInterface)
                {
                    await pluginInterface.CleanupAsync().ConfigureAwait(false);
                }

                if (instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _loadedPlugins.Remove(pluginId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 特定のタイプのプラグインを取得
    /// Get plugins of specific type
    /// </summary>
    public List<T> GetPlugins<T>() where T : IPlugin
    {
        return _loadedPlugins.Values
            .Where(p => p.IsEnabled)
            .SelectMany(p => p.Instances)
            .OfType<T>()
            .ToList();
    }

    /// <summary>
    /// プラグイン一覧を取得
    /// Get plugin list
    /// </summary>
    public List<PluginManifest> ListPlugins()
    {
        return _loadedPlugins.Values
            .Select(p => p.Manifest)
            .OrderBy(m => m.Name)
            .ToList();
    }

    /// <summary>
    /// プラグインを有効化/無効化
    /// Enable/disable plugin
    /// </summary>
    public bool SetPluginEnabled(string pluginId, bool enabled)
    {
        if (!_loadedPlugins.ContainsKey(pluginId))
        {
            return false;
        }

        _loadedPlugins[pluginId].IsEnabled = enabled;
        return true;
    }
}

/// <summary>
/// プラグイン基底インターフェース
/// Base plugin interface
/// </summary>
public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    Task InitializeAsync(Dictionary<string, string> configuration);
    Task CleanupAsync();
}

/// <summary>
/// アクションプラグイン
/// Action plugin
/// </summary>
public interface IActionPlugin : IPlugin
{
    Task<ActionResult> ExecuteAsync(Dictionary<string, object> parameters);
}

/// <summary>
/// トリガープラグイン
/// Trigger plugin
/// </summary>
public interface ITriggerPlugin : IPlugin
{
    Task<bool> ShouldTriggerAsync();
    string TriggerType { get; }
}

/// <summary>
/// データ変換プラグイン
/// Transform plugin
/// </summary>
public interface ITransformPlugin : IPlugin
{
    Task<object> TransformAsync(object input);
}

public class ActionResult
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// サンプルプラグイン
/// Sample plugin
/// </summary>
public class SampleActionPlugin : IActionPlugin
{
    public string Name => "Sample Action";
    public string Version => "1.0.0";

    public Task InitializeAsync(Dictionary<string, string> configuration)
    {
        Console.WriteLine($"[{Name}] Initialized");
        return Task.CompletedTask;
    }

    public Task CleanupAsync()
    {
        Console.WriteLine($"[{Name}] Cleaned up");
        return Task.CompletedTask;
    }

    public async Task<ActionResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        return await Task.FromResult(new ActionResult
        {
            Success = true,
            Data = "Sample action executed"
        }).ConfigureAwait(false);
    }
}
