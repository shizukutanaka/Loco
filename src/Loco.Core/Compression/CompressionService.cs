using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Buffers;

namespace Loco.Core.Compression
{
    public interface ICompressionService
    {
        byte[] Compress(byte[] data, CompressionLevel level = CompressionLevel.Optimal);
        byte[] Decompress(byte[] data);
        Task<byte[]> CompressAsync(byte[] data, CompressionLevel level = CompressionLevel.Optimal);
        Task<byte[]> DecompressAsync(byte[] data);
        Stream CreateCompressionStream(Stream stream, CompressionLevel level = CompressionLevel.Optimal);
        Stream CreateDecompressionStream(Stream stream);
        CompressionStatistics GetStatistics();
    }

    public class CompressionStatistics
    {
        public long TotalBytesCompressed { get; set; }
        public long TotalBytesDecompressed { get; set; }
        public long TotalCompressionTime { get; set; }
        public long TotalDecompressionTime { get; set; }
        public double AverageCompressionRatio { get; set; }
        public long CompressionCount { get; set; }
        public long DecompressionCount { get; set; }
    }

    public class AdvancedCompressionService : ICompressionService
    {
        private readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
        private readonly ConcurrentDictionary<string, byte[]> _compressionCache;
        private readonly CompressionStatistics _statistics;
        private readonly int _maxCacheSize;
        private readonly bool _enableCache;

        public AdvancedCompressionService(int maxCacheSize = 100, bool enableCache = true)
        {
            _maxCacheSize = maxCacheSize;
            _enableCache = enableCache;
            _compressionCache = new ConcurrentDictionary<string, byte[]>();
            _statistics = new CompressionStatistics();
        }

        public byte[] Compress(byte[] data, CompressionLevel level = CompressionLevel.Optimal)
        {
            if (data == null || data.Length == 0)
                return data;

            var startTime = Environment.TickCount64;

            try
            {
                // Check cache
                if (_enableCache)
                {
                    var hash = GetDataHash(data);
                    if (_compressionCache.TryGetValue(hash, out var cached))
                    {
                        UpdateStatistics(data.Length, cached.Length, true, Environment.TickCount64 - startTime);
                        return cached;
                    }
                }

                // Use multiple compression algorithms and choose the best
                var results = new[]
                {
                    CompressWithGZip(data, level),
                    CompressWithBrotli(data, level),
                    CompressWithDeflate(data, level)
                };

                // Select the smallest result
                byte[] bestResult = data;
                int bestSize = data.Length;

                foreach (var result in results)
                {
                    if (result.Length < bestSize)
                    {
                        bestResult = result;
                        bestSize = result.Length;
                    }
                }

                // Cache the result if compression was effective
                if (_enableCache && bestResult.Length < data.Length)
                {
                    var hash = GetDataHash(data);
                    if (_compressionCache.Count < _maxCacheSize)
                    {
                        _compressionCache.TryAdd(hash, bestResult);
                    }
                }

                UpdateStatistics(data.Length, bestResult.Length, true, Environment.TickCount64 - startTime);
                return bestResult;
            }
            catch (Exception ex)
            {
                // Log error and return original data
                Console.WriteLine($"Compression error: {ex.Message}");
                return data;
            }
        }

        public byte[] Decompress(byte[] data)
        {
            if (data == null || data.Length == 0)
                return data;

            var startTime = Environment.TickCount64;

            try
            {
                // Try different decompression methods
                byte[] result = null;
                
                try
                {
                    result = DecompressWithGZip(data);
                }
                catch
                {
                    try
                    {
                        result = DecompressWithBrotli(data);
                    }
                    catch
                    {
                        try
                        {
                            result = DecompressWithDeflate(data);
                        }
                        catch
                        {
                            // If all fail, return original
                            return data;
                        }
                    }
                }

                UpdateStatistics(data.Length, result?.Length ?? data.Length, false, Environment.TickCount64 - startTime);
                return result ?? data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Decompression error: {ex.Message}");
                return data;
            }
        }

        public async Task<byte[]> CompressAsync(byte[] data, CompressionLevel level = CompressionLevel.Optimal)
        {
            return await Task.Run(() => Compress(data, level));
        }

        public async Task<byte[]> DecompressAsync(byte[] data)
        {
            return await Task.Run(() => Decompress(data));
        }

        public Stream CreateCompressionStream(Stream stream, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Use Brotli for best compression ratio
            return new BrotliStream(stream, level);
        }

        public Stream CreateDecompressionStream(Stream stream)
        {
            // Auto-detect compression format
            if (!stream.CanSeek)
            {
                // Default to GZip if we can't seek
                return new GZipStream(stream, CompressionMode.Decompress);
            }

            var buffer = new byte[4];
            stream.Read(buffer, 0, 4);
            stream.Seek(0, SeekOrigin.Begin);

            // Check magic numbers
            if (buffer[0] == 0x1f && buffer[1] == 0x8b) // GZip
            {
                return new GZipStream(stream, CompressionMode.Decompress);
            }
            else if (buffer[0] == 0x78 && (buffer[1] == 0x01 || buffer[1] == 0x9c || buffer[1] == 0xda)) // Deflate
            {
                return new DeflateStream(stream, CompressionMode.Decompress);
            }
            else // Assume Brotli
            {
                return new BrotliStream(stream, CompressionMode.Decompress);
            }
        }

        public CompressionStatistics GetStatistics()
        {
            return new CompressionStatistics
            {
                TotalBytesCompressed = _statistics.TotalBytesCompressed,
                TotalBytesDecompressed = _statistics.TotalBytesDecompressed,
                TotalCompressionTime = _statistics.TotalCompressionTime,
                TotalDecompressionTime = _statistics.TotalDecompressionTime,
                AverageCompressionRatio = _statistics.AverageCompressionRatio,
                CompressionCount = _statistics.CompressionCount,
                DecompressionCount = _statistics.DecompressionCount
            };
        }

        private byte[] CompressWithGZip(byte[] data, CompressionLevel level)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, level))
            {
                gzip.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        private byte[] CompressWithBrotli(byte[] data, CompressionLevel level)
        {
            using var output = new MemoryStream();
            using (var brotli = new BrotliStream(output, level))
            {
                brotli.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        private byte[] CompressWithDeflate(byte[] data, CompressionLevel level)
        {
            using var output = new MemoryStream();
            using (var deflate = new DeflateStream(output, level))
            {
                deflate.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        private byte[] DecompressWithGZip(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        private byte[] DecompressWithBrotli(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var brotli = new BrotliStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            brotli.CopyTo(output);
            return output.ToArray();
        }

        private byte[] DecompressWithDeflate(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetDataHash(byte[] data)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(data);
            return Convert.ToBase64String(hash);
        }

        private void UpdateStatistics(long originalSize, long compressedSize, bool isCompression, long elapsedTime)
        {
            lock (_statistics)
            {
                if (isCompression)
                {
                    _statistics.TotalBytesCompressed += originalSize;
                    _statistics.TotalCompressionTime += elapsedTime;
                    _statistics.CompressionCount++;
                    
                    // Update compression ratio (weighted average)
                    var ratio = (double)compressedSize / originalSize;
                    _statistics.AverageCompressionRatio = 
                        (_statistics.AverageCompressionRatio * (_statistics.CompressionCount - 1) + ratio) 
                        / _statistics.CompressionCount;
                }
                else
                {
                    _statistics.TotalBytesDecompressed += compressedSize;
                    _statistics.TotalDecompressionTime += elapsedTime;
                    _statistics.DecompressionCount++;
                }
            }
        }

        public void ClearCache()
        {
            _compressionCache.Clear();
        }

        public int GetCacheSize()
        {
            return _compressionCache.Count;
        }
    }

    // Extension methods for convenience
    public static class CompressionExtensions
    {
        private static readonly ICompressionService _defaultService = new AdvancedCompressionService();

        public static byte[] Compress(this byte[] data, CompressionLevel level = CompressionLevel.Optimal)
        {
            return _defaultService.Compress(data, level);
        }

        public static byte[] Decompress(this byte[] data)
        {
            return _defaultService.Decompress(data);
        }

        public static async Task<byte[]> CompressAsync(this byte[] data, CompressionLevel level = CompressionLevel.Optimal)
        {
            return await _defaultService.CompressAsync(data, level);
        }

        public static async Task<byte[]> DecompressAsync(this byte[] data)
        {
            return await _defaultService.DecompressAsync(data);
        }

        public static string CompressString(this string text, CompressionLevel level = CompressionLevel.Optimal)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var bytes = Encoding.UTF8.GetBytes(text);
            var compressed = _defaultService.Compress(bytes, level);
            return Convert.ToBase64String(compressed);
        }

        public static string DecompressString(this string compressedText)
        {
            if (string.IsNullOrEmpty(compressedText))
                return compressedText;

            try
            {
                var bytes = Convert.FromBase64String(compressedText);
                var decompressed = _defaultService.Decompress(bytes);
                return Encoding.UTF8.GetString(decompressed);
            }
            catch
            {
                return compressedText;
            }
        }
    }
}