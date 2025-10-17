using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core;

/// <summary>
/// Utility class for JSON file operations with proper error handling
/// </summary>
public static class JsonFileUtils
{
    private static readonly JsonSerializerOptions DefaultSerializerOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Loads JSON data from file with error handling
    /// </summary>
    public static async Task<Dictionary<string, T>> LoadJsonAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            await using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var data = await JsonSerializer.DeserializeAsync<Dictionary<string, T>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return data ?? new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            // Log file read error with details
            System.Diagnostics.Debug.WriteLine($"Failed to load JSON from {filePath}: {ex.Message}");
            // If file is corrupted or can't be read, return empty dictionary
            return new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Saves JSON data to file with atomic operation
    /// </summary>
    public static async Task SaveJsonAsync<T>(
        string filePath,
        Dictionary<string, T> data,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        if (data == null) return;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            DirectoryUtils.EnsureDirectoryExists(directory);
        }

        var tempFile = $"{filePath}_{Guid.NewGuid():N}.tmp";
        serializerOptions ??= DefaultSerializerOptions;

        try
        {
            await using (var stream = File.Open(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, data, serializerOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(filePath))
            {
                File.Replace(tempFile, filePath, null, true);
            }
            else
            {
                File.Move(tempFile, filePath);
            }
        }
        catch (Exception ex)
        {
            // Log save error with details
            System.Diagnostics.Debug.WriteLine($"Failed to save JSON to {filePath}: {ex.Message}");
            // Clean up temp file if save failed
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { /* Ignore cleanup errors */ }
            }
            throw;
        }
    }

    /// <summary>
    /// Loads JSON data from file with error handling (legacy method for compatibility)
    /// </summary>
    public static async Task<T?> LoadJsonFileAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return default;
        }

        try
        {
            await using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var result = await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            // Log error with details
            System.Diagnostics.Debug.WriteLine($"Failed to load JSON from {filePath}: {ex.Message}");
            // If file is corrupted or can't be read, return default value
            return default;
        }
    }

    /// <summary>
    /// Saves JSON data to file with atomic operation (legacy method for compatibility)
    /// </summary>
    public static async Task SaveJsonFileAsync<T>(
        string filePath,
        T data,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        if (data == null) return;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            DirectoryUtils.EnsureDirectoryExists(directory);
        }

        var tempFile = $"{filePath}_{Guid.NewGuid():N}.tmp";
        serializerOptions ??= DefaultSerializerOptions;

        try
        {
            await using (var stream = File.Open(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, data, serializerOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(filePath))
            {
                File.Replace(tempFile, filePath, null, true);
            }
            else
            {
                File.Move(tempFile, filePath);
            }
        }
        catch (Exception ex)
        {
            // Log save error with details
            System.Diagnostics.Debug.WriteLine($"Failed to save JSON to {filePath}: {ex.Message}");
            // Clean up temp file if save failed
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { /* Ignore cleanup errors */ }
            }
            throw;
        }
    }
}
