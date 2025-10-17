using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Configuration;

namespace Loco.Core;

/// <summary>
/// Lightweight persistent key-value cache with simple file-based storage.
/// </summary>
public sealed class SimpleCacheStore : IDisposable
{
    private readonly string _cacheDirectory;
    private readonly string _cacheFilePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    // In-memory cache for better performance
    private Dictionary<string, CacheEntry>? _cacheData;
    private bool _isLoaded = false;
    private bool _disposed = false;

    public SimpleCacheStore(string? cacheDirectory = null)
    {
        var baseDirectory = cacheDirectory ?? new LocoConfig().CacheDirectory;
        _cacheDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Loco", "Cache")
            : baseDirectory;

        // Ensure cache directory exists with proper error handling
        DirectoryUtils.EnsureDirectoryExists(_cacheDirectory);
        _cacheFilePath = Path.Combine(_cacheDirectory, "cache.json");
    }

    public async Task<IReadOnlyDictionary<string, CacheEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteWithLockAsync<IReadOnlyDictionary<string, CacheEntry>>(async () =>
        {
            await EnsureCacheLoadedAsync(cancellationToken).ConfigureAwait(false);
            return _cacheData?.OrderByDescending(x => x.Value.UpdatedAt)
                .ToDictionary(k => k.Key, v => v.Value) ?? new Dictionary<string, CacheEntry>();
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CacheEntry?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        return await ExecuteWithLockAsync<CacheEntry?>(async () =>
        {
            await EnsureCacheLoadedAsync(cancellationToken).ConfigureAwait(false);
            return _cacheData?.TryGetValue(key, out var entry) == true ? entry : null;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be empty", nameof(key));
        }

        await ExecuteWithLockAsync<bool>(async () =>
        {
            await EnsureCacheLoadedAsync(cancellationToken).ConfigureAwait(false);

            _cacheData ??= new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
            _cacheData[key] = new CacheEntry
            {
                Value = value,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await SaveCacheAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        return await ExecuteWithLockAsync<bool>(async () =>
        {
            await EnsureCacheLoadedAsync(cancellationToken).ConfigureAwait(false);

            if (_cacheData?.Remove(key) == true)
            {
                await SaveCacheAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }

            return false;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteWithLockAsync<bool>(async () =>
        {
            _cacheData?.Clear();
            _isLoaded = true; // Mark as loaded to avoid reloading from disk

            if (File.Exists(_cacheFilePath))
            {
                File.Delete(_cacheFilePath);
            }
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> ExecuteWithLockAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureCacheLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded || _cacheData != null) return;

        _cacheData = await JsonFileUtils.LoadJsonAsync<CacheEntry>(_cacheFilePath, cancellationToken).ConfigureAwait(false);
        _isLoaded = true;
    }

    private async Task SaveCacheAsync(CancellationToken cancellationToken)
    {
        if (_cacheData == null) return;

        await JsonFileUtils.SaveJsonAsync(_cacheFilePath, _cacheData, _serializerOptions, cancellationToken).ConfigureAwait(false);
    }

    public sealed class CacheEntry
    {
        public string Value { get; set; } = string.Empty;
        public DateTimeOffset UpdatedAt { get; set; }
            = DateTimeOffset.MinValue;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _gate?.Dispose();
    }
}
