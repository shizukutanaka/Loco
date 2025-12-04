// John Carmack: "Simplicity first, optimization later"
// Rob Pike: "Clear is better than clever"

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Loco.Core.Practical;

/// <summary>
/// Simple file storage - Local and cloud-agnostic storage abstraction
/// Save, load, delete files with versioning and metadata
/// </summary>
public interface IStorage
{
    Task<bool> SaveAsync(string key, byte[] data);
    Task<byte[]?> LoadAsync(string key);
    Task<bool> ExistsAsync(string key);
    Task<bool> DeleteAsync(string key);
    Task<List<string>> ListKeysAsync(string? prefix = null);
}

/// <summary>
/// Local file system storage
/// </summary>
public class LocalStorage : IStorage
{
    private readonly string _basePath;
    private readonly SimpleLogger _logger;

    public LocalStorage(string basePath, SimpleLogger? logger = null)
    {
        _basePath = Path.GetFullPath(basePath);
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(LocalStorage));

        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
            _logger.Info($"Created storage directory: {_basePath}");
        }
    }

    public async Task<bool> SaveAsync(string key, byte[] data)
    {
        try
        {
            var path = GetFilePath(key);
            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(path, data);
            _logger.Debug($"Saved: {key} ({data.Length} bytes)");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save {key}", ex);
            return false;
        }
    }

    public async Task<byte[]?> LoadAsync(string key)
    {
        try
        {
            var path = GetFilePath(key);
            if (!File.Exists(path)) return null;

            var data = await File.ReadAllBytesAsync(path);
            _logger.Debug($"Loaded: {key} ({data.Length} bytes)");
            return data;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load {key}", ex);
            return null;
        }
    }

    public Task<bool> ExistsAsync(string key)
    {
        var path = GetFilePath(key);
        return Task.FromResult(File.Exists(path));
    }

    public Task<bool> DeleteAsync(string key)
    {
        try
        {
            var path = GetFilePath(key);
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.Debug($"Deleted: {key}");
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to delete {key}", ex);
            return Task.FromResult(false);
        }
    }

    public Task<List<string>> ListKeysAsync(string? prefix = null)
    {
        try
        {
            var searchPath = prefix != null ? Path.Combine(_basePath, prefix) : _basePath;
            var files = Directory.GetFiles(searchPath, "*", SearchOption.AllDirectories);
            var keys = files.Select(f => Path.GetRelativePath(_basePath, f).Replace('\\', '/')).ToList();
            return Task.FromResult(keys);
        }
        catch
        {
            return Task.FromResult(new List<string>());
        }
    }

    private string GetFilePath(string key)
    {
        var safePath = key.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_basePath, safePath);
    }
}

/// <summary>
/// In-memory storage (for testing)
/// </summary>
public class MemoryStorage : IStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _data = new();

    public Task<bool> SaveAsync(string key, byte[] data)
    {
        _data[key] = data;
        return Task.FromResult(true);
    }

    public Task<byte[]?> LoadAsync(string key)
    {
        return Task.FromResult(_data.TryGetValue(key, out var data) ? data : null);
    }

    public Task<bool> ExistsAsync(string key)
    {
        return Task.FromResult(_data.ContainsKey(key));
    }

    public Task<bool> DeleteAsync(string key)
    {
        return Task.FromResult(_data.TryRemove(key, out _));
    }

    public Task<List<string>> ListKeysAsync(string? prefix = null)
    {
        var keys = prefix != null
            ? _data.Keys.Where(k => k.StartsWith(prefix)).ToList()
            : _data.Keys.ToList();
        return Task.FromResult(keys);
    }

    public void Clear() => _data.Clear();
}

/// <summary>
/// Storage with metadata
/// </summary>
public class MetadataStorage
{
    private readonly IStorage _storage;
    private readonly ConcurrentDictionary<string, FileMetadata> _metadata = new();

    public MetadataStorage(IStorage storage)
    {
        _storage = storage;
    }

    public async Task<bool> SaveAsync(string key, byte[] data, Dictionary<string, string>? metadata = null)
    {
        var hash = ComputeHash(data);
        _metadata[key] = new FileMetadata
        {
            Key = key,
            Size = data.Length,
            Hash = hash,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            ContentType = GuessContentType(key),
            CustomMetadata = metadata ?? new Dictionary<string, string>()
        };

        return await _storage.SaveAsync(key, data);
    }

    public async Task<(byte[]? data, FileMetadata? metadata)> LoadAsync(string key)
    {
        var data = await _storage.LoadAsync(key);
        _metadata.TryGetValue(key, out var metadata);
        return (data, metadata);
    }

    public FileMetadata? GetMetadata(string key)
    {
        _metadata.TryGetValue(key, out var metadata);
        return metadata;
    }

    public async Task<bool> DeleteAsync(string key)
    {
        _metadata.TryRemove(key, out _);
        return await _storage.DeleteAsync(key);
    }

    private string ComputeHash(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToBase64String(hash);
    }

    private string GuessContentType(string key)
    {
        var ext = Path.GetExtension(key).ToLowerInvariant();
        return ext switch
        {
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".html" => "text/html",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }

    public class FileMetadata
    {
        public string Key { get; set; } = "";
        public long Size { get; set; }
        public string Hash { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public string ContentType { get; set; } = "";
        public Dictionary<string, string> CustomMetadata { get; set; } = new();
    }
}

/// <summary>
/// Storage helpers for common types
/// </summary>
public static class StorageExtensions
{
    public static async Task<bool> SaveTextAsync(this IStorage storage, string key, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return await storage.SaveAsync(key, bytes);
    }

    public static async Task<string?> LoadTextAsync(this IStorage storage, string key)
    {
        var bytes = await storage.LoadAsync(key);
        return bytes != null ? Encoding.UTF8.GetString(bytes) : null;
    }

    public static async Task<bool> SaveJsonAsync<T>(this IStorage storage, string key, T obj)
    {
        var json = SimpleSerializer.ToJson(obj);
        return await storage.SaveTextAsync(key, json);
    }

    public static async Task<T?> LoadJsonAsync<T>(this IStorage storage, string key)
    {
        var json = await storage.LoadTextAsync(key);
        return json != null ? SimpleSerializer.FromJson<T>(json) : default;
    }

    public static async Task<bool> SaveFileAsync(this IStorage storage, string key, string filePath)
    {
        var bytes = await File.ReadAllBytesAsync(filePath);
        return await storage.SaveAsync(key, bytes);
    }

    public static async Task<bool> LoadToFileAsync(this IStorage storage, string key, string filePath)
    {
        var bytes = await storage.LoadAsync(key);
        if (bytes == null) return false;

        await File.WriteAllBytesAsync(filePath, bytes);
        return true;
    }
}

/// <summary>
/// Versioned storage
/// </summary>
public class VersionedStorage
{
    private readonly IStorage _storage;
    private readonly string _versionPrefix;

    public VersionedStorage(IStorage storage, string versionPrefix = "_versions")
    {
        _storage = storage;
        _versionPrefix = versionPrefix;
    }

    public async Task<bool> SaveAsync(string key, byte[] data)
    {
        // Save current version
        var versionKey = $"{_versionPrefix}/{key}/{DateTime.UtcNow:yyyyMMddHHmmss}";
        await _storage.SaveAsync(versionKey, data);

        // Save as current
        return await _storage.SaveAsync(key, data);
    }

    public async Task<byte[]?> LoadAsync(string key, int? version = null)
    {
        if (version == null)
        {
            return await _storage.LoadAsync(key);
        }

        var versions = await ListVersionsAsync(key);
        if (version < 0 || version >= versions.Count) return null;

        var versionKey = versions[version.Value];
        return await _storage.LoadAsync(versionKey);
    }

    public async Task<List<string>> ListVersionsAsync(string key)
    {
        return await _storage.ListKeysAsync($"{_versionPrefix}/{key}/");
    }
}

/// <summary>
/// Cached storage
/// </summary>
public class CachedStorage : IStorage
{
    private readonly IStorage _underlying;
    private readonly SimpleCache<byte[]> _cache;

    public CachedStorage(IStorage underlying, int maxCacheSize = 100)
    {
        _underlying = underlying;
        _cache = new SimpleCache<byte[]>(TimeSpan.FromMinutes(maxCacheSize));
    }

    public async Task<bool> SaveAsync(string key, byte[] data)
    {
        var result = await _underlying.SaveAsync(key, data);
        if (result)
        {
            _cache.Set(key, data, TimeSpan.FromMinutes(10));
        }
        return result;
    }

    public async Task<byte[]?> LoadAsync(string key)
    {
        var cached = _cache.Get(key);
        if (cached != null) return cached;

        var data = await _underlying.LoadAsync(key);
        if (data != null)
        {
            _cache.Set(key, data, TimeSpan.FromMinutes(10));
        }
        return data;
    }

    public Task<bool> ExistsAsync(string key) => _underlying.ExistsAsync(key);

    public async Task<bool> DeleteAsync(string key)
    {
        _cache.Remove(key);
        return await _underlying.DeleteAsync(key);
    }

    public Task<List<string>> ListKeysAsync(string? prefix = null) =>
        _underlying.ListKeysAsync(prefix);
}

/// <summary>
/// Example usage
/// </summary>
public class StorageExamples
{
    public static async Task Examples()
    {
        // Local storage
        var storage = new LocalStorage("./data");

        // Save and load binary
        await storage.SaveAsync("files/data.bin", new byte[] { 1, 2, 3 });
        var data = await storage.LoadAsync("files/data.bin");

        // Save and load text
        await storage.SaveTextAsync("files/readme.txt", "Hello World");
        var text = await storage.LoadTextAsync("files/readme.txt");

        // Save and load JSON
        var user = new { Id = 1, Name = "John" };
        await storage.SaveJsonAsync("users/1.json", user);
        var loadedUser = await storage.LoadJsonAsync<dynamic>("users/1.json");

        // List files
        var keys = await storage.ListKeysAsync("files/");
        foreach (var key in keys)
        {
            Console.WriteLine($"Found: {key}");
        }

        // Metadata storage
        var metaStorage = new MetadataStorage(storage);
        await metaStorage.SaveAsync("image.jpg", new byte[1024], new Dictionary<string, string>
        {
            ["author"] = "John",
            ["tags"] = "photo,vacation"
        });

        var (imageData, metadata) = await metaStorage.LoadAsync("image.jpg");
        Console.WriteLine($"File: {metadata?.Size} bytes, type: {metadata?.ContentType}");

        // Versioned storage
        var versionedStorage = new VersionedStorage(storage);
        await versionedStorage.SaveAsync("document.txt", Encoding.UTF8.GetBytes("Version 1"));
        await versionedStorage.SaveAsync("document.txt", Encoding.UTF8.GetBytes("Version 2"));

        var versions = await versionedStorage.ListVersionsAsync("document.txt");
        Console.WriteLine($"Document has {versions.Count} versions");

        // In-memory storage (for testing)
        var memStorage = new MemoryStorage();
        await memStorage.SaveTextAsync("test", "data");
        var testData = await memStorage.LoadTextAsync("test");
    }
}