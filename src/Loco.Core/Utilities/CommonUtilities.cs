using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Loco.Core.Utilities;

/// <summary>
/// Common utility functions
/// </summary>
public static class CommonUtilities
{
    /// <summary>
    /// Generate unique ID
    /// </summary>
    public static string GenerateId(int length = 16)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Range(0, length)
            .Select(_ => chars[random.Next(chars.Length)])
            .ToArray());
    }

    /// <summary>
    /// Generate secure random string
    /// </summary>
    public static string GenerateSecureRandom(int length = 32)
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[length];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Substring(0, length);
    }

    /// <summary>
    /// Calculate file hash (SHA256)
    /// </summary>
    public static async Task<string> CalculateFileHashAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream).ConfigureAwait(false);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Calculate string hash
    /// </summary>
    public static string CalculateHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Safely delete file (no exception if not exists)
    /// </summary>
    public static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    /// <summary>
    /// Safely create directory
    /// </summary>
    public static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    /// <summary>
    /// Get file size in human-readable format
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Format duration in human-readable format
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{duration.TotalDays:F1}d";
        if (duration.TotalHours >= 1)
            return $"{duration.TotalHours:F1}h";
        if (duration.TotalMinutes >= 1)
            return $"{duration.TotalMinutes:F1}m";
        if (duration.TotalSeconds >= 1)
            return $"{duration.TotalSeconds:F1}s";
        return $"{duration.TotalMilliseconds:F0}ms";
    }

    /// <summary>
    /// Deep clone object using JSON serialization
    /// </summary>
    public static T? DeepClone<T>(T obj)
    {
        if (obj == null) return default;

        var json = JsonSerializer.Serialize(obj);
        return JsonSerializer.Deserialize<T>(json);
    }

    /// <summary>
    /// Retry operation with exponential backoff
    /// </summary>
    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        int delayMs = 100,
        double backoffMultiplier = 2.0)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt < maxAttempts - 1)
                {
                    var delay = (int)(delayMs * Math.Pow(backoffMultiplier, attempt));
                    await Task.Delay(delay).ConfigureAwait(false);
                }
            }
        }

        throw new AggregateException(
            $"Operation failed after {maxAttempts} attempts",
            lastException ?? new Exception("Unknown error"));
    }

    /// <summary>
    /// Batch items into groups
    /// </summary>
    public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int batchSize)
    {
        var batch = new List<T>(batchSize);

        foreach (var item in source)
        {
            batch.Add(item);

            if (batch.Count >= batchSize)
            {
                yield return batch;
                batch = new List<T>(batchSize);
            }
        }

        if (batch.Any())
        {
            yield return batch;
        }
    }

    /// <summary>
    /// Truncate string with ellipsis
    /// </summary>
    public static string Truncate(string value, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value.Substring(0, maxLength - suffix.Length) + suffix;
    }

    /// <summary>
    /// Get timestamp in ISO 8601 format
    /// </summary>
    public static string GetTimestamp() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    /// <summary>
    /// Parse ISO 8601 timestamp
    /// </summary>
    public static DateTime ParseTimestamp(string timestamp)
    {
        return DateTime.Parse(timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    /// <summary>
    /// Check if running in Docker
    /// </summary>
    public static bool IsRunningInDocker()
    {
        return File.Exists("/.dockerenv") ||
               Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
    }

    /// <summary>
    /// Get environment name (Development, Staging, Production)
    /// </summary>
    public static string GetEnvironmentName()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
               Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
               "Production";
    }

    /// <summary>
    /// Is development environment
    /// </summary>
    public static bool IsDevelopment() =>
        GetEnvironmentName().Equals("Development", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Is production environment
    /// </summary>
    public static bool IsProduction() =>
        GetEnvironmentName().Equals("Production", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Convert dictionary to query string
    /// </summary>
    public static string ToQueryString(Dictionary<string, string> parameters)
    {
        var pairs = parameters.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}");

        return string.Join("&", pairs);
    }

    /// <summary>
    /// Parse query string to dictionary
    /// </summary>
    public static Dictionary<string, string> ParseQueryString(string queryString)
    {
        var result = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(queryString))
            return result;

        queryString = queryString.TrimStart('?');

        foreach (var pair in queryString.Split('&'))
        {
            var parts = pair.Split('=');
            if (parts.Length == 2)
            {
                result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
            }
        }

        return result;
    }

    /// <summary>
    /// Execute action with timeout
    /// </summary>
    public static async Task<T> WithTimeoutAsync<T>(
        Func<Task<T>> operation,
        TimeSpan timeout)
    {
        using var cts = new System.Threading.CancellationTokenSource(timeout);

        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cts.Token.IsCancellationRequested)
        {
            throw new TimeoutException($"Operation timed out after {timeout}");
        }
    }

    /// <summary>
    /// Combine paths safely
    /// </summary>
    public static string CombinePaths(params string[] paths)
    {
        if (paths == null || paths.Length == 0)
            return string.Empty;

        var result = paths[0];
        for (int i = 1; i < paths.Length; i++)
        {
            result = Path.Combine(result, paths[i]);
        }

        return result;
    }

    /// <summary>
    /// Get relative path
    /// </summary>
    public static string GetRelativePath(string basePath, string fullPath)
    {
        var baseUri = new Uri(basePath.EndsWith(Path.DirectorySeparatorChar.ToString())
            ? basePath
            : basePath + Path.DirectorySeparatorChar);

        var fullUri = new Uri(fullPath);

        return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString())
            .Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Measure execution time
    /// </summary>
    public static async Task<(T Result, TimeSpan Duration)> MeasureAsync<T>(Func<Task<T>> operation)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await operation().ConfigureAwait(false);
        sw.Stop();
        return (result, sw.Elapsed);
    }

    /// <summary>
    /// Convert bytes to hex string
    /// </summary>
    public static string ToHexString(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Convert hex string to bytes
    /// </summary>
    public static byte[] FromHexString(string hex)
    {
        return Enumerable.Range(0, hex.Length)
            .Where(x => x % 2 == 0)
            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
            .ToArray();
    }

    /// <summary>
    /// Get application version
    /// </summary>
    public static string GetVersion()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "0.0.0";
    }

    /// <summary>
    /// Coalesce (return first non-null)
    /// </summary>
    public static T Coalesce<T>(params T?[] values) where T : class
    {
        foreach (var value in values)
        {
            if (value != null)
                return value;
        }

        throw new InvalidOperationException("All values are null");
    }

    /// <summary>
    /// Swap two values
    /// </summary>
    public static void Swap<T>(ref T a, ref T b)
    {
        T temp = a;
        a = b;
        b = temp;
    }
}
