using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Loco.Core.Platform;

/// <summary>
/// ファイルシステム最適化
/// File system optimizer for NTFS/ext4/APFS
///
/// 機能: OS固有のファイルシステム機能を活用した最適化
/// Features: Optimization using OS-specific file system features
/// </summary>
public class FileSystemOptimizer
{
    private readonly CrossPlatformPathHandler _pathHandler;
    private readonly CrossPlatformPathHandler.FileSystemType _fileSystem;

    public FileSystemOptimizer()
    {
        _pathHandler = new CrossPlatformPathHandler();
        _fileSystem = _pathHandler.FileSystem;
    }

    /// <summary>
    /// ファイル書き込み最適化
    /// Optimized file write
    /// </summary>
    public async Task WriteFileOptimizedAsync(
        string path,
        string content,
        bool useCompression = false,
        bool useEncryption = false)
    {
        path = _pathHandler.NormalizePath(path);

        var encoding = Encoding.UTF8;
        var bytes = encoding.GetBytes(content);

        // バッファサイズを最適化（ファイルシステムのブロックサイズに合わせる）
        var optimalBufferSize = GetOptimalBufferSize();

        using var fileStream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            optimalBufferSize,
            useAsync: true // 非同期I/Oを有効化
        );

        await fileStream.WriteAsync(bytes, 0, bytes.Length);
        await fileStream.FlushAsync();

        // ファイルシステム固有の最適化を適用
        ApplyFileSystemSpecificOptimizations(path, useCompression, useEncryption);
    }

    /// <summary>
    /// ファイル読み込み最適化
    /// Optimized file read
    /// </summary>
    public async Task<string> ReadFileOptimizedAsync(string path)
    {
        path = _pathHandler.NormalizePath(path);

        var optimalBufferSize = GetOptimalBufferSize();

        using var fileStream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            optimalBufferSize,
            useAsync: true
        );

        using var reader = new StreamReader(fileStream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// 大容量ファイルのコピー最適化
    /// Optimized large file copy
    /// </summary>
    public async Task CopyFileOptimizedAsync(
        string sourcePath,
        string destinationPath,
        bool preserveMetadata = true,
        IProgress<double>? progress = null)
    {
        sourcePath = _pathHandler.NormalizePath(sourcePath);
        destinationPath = _pathHandler.NormalizePath(destinationPath);

        var fileInfo = new FileInfo(sourcePath);
        var fileSize = fileInfo.Length;
        var optimalBufferSize = GetOptimalBufferSize();

        // APFS/ext4でのCopy-on-Writeやリフリンクを試みる
        if (TryOptimizedCopy(sourcePath, destinationPath))
        {
            progress?.Report(100.0);
            return;
        }

        // 通常のコピー（バッファサイズ最適化）
        using var sourceStream = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            optimalBufferSize,
            useAsync: true
        );

        using var destStream = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            optimalBufferSize,
            useAsync: true
        );

        var buffer = new byte[optimalBufferSize];
        long totalBytesRead = 0;
        int bytesRead;

        while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await destStream.WriteAsync(buffer, 0, bytesRead);
            totalBytesRead += bytesRead;

            // 進捗報告
            if (progress != null && fileSize > 0)
            {
                var progressPercent = (double)totalBytesRead / fileSize * 100.0;
                progress.Report(progressPercent);
            }
        }

        await destStream.FlushAsync();

        // メタデータを保持
        if (preserveMetadata)
        {
            PreserveFileMetadata(sourcePath, destinationPath);
        }
    }

    /// <summary>
    /// ディレクトリのバッチ操作最適化
    /// Optimized batch directory operations
    /// </summary>
    public async Task<BatchOperationResult> ProcessDirectoryBatchAsync(
        string directoryPath,
        Func<string, Task<bool>> fileProcessor,
        int maxParallelism = 4)
    {
        directoryPath = _pathHandler.NormalizePath(directoryPath);

        var result = new BatchOperationResult
        {
            StartTime = DateTime.UtcNow
        };

        if (!Directory.Exists(directoryPath))
        {
            result.Success = false;
            result.ErrorMessage = $"Directory not found: {directoryPath}";
            return result;
        }

        var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
        result.TotalFiles = files.Length;

        // 並列処理（CPUコア数に基づく）
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxParallelism
        };

        try
        {
            await Parallel.ForEachAsync(files, parallelOptions, async (file, ct) =>
            {
                try
                {
                    var success = await fileProcessor(file);
                    if (success)
                    {
                        result.SuccessCount++;
                    }
                    else
                    {
                        result.FailureCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.FailureCount++;
                    result.Errors.Add($"{file}: {ex.Message}");
                }
            });

            result.Success = result.FailureCount == 0;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        result.EndTime = DateTime.UtcNow;
        result.Duration = result.EndTime - result.StartTime;

        return result;
    }

    /// <summary>
    /// 最適なバッファサイズを取得
    /// Get optimal buffer size for file system
    /// </summary>
    private int GetOptimalBufferSize()
    {
        return _fileSystem switch
        {
            CrossPlatformPathHandler.FileSystemType.NTFS => 65536,      // 64 KB
            CrossPlatformPathHandler.FileSystemType.Ext4 => 131072,     // 128 KB
            CrossPlatformPathHandler.FileSystemType.APFS => 131072,     // 128 KB
            CrossPlatformPathHandler.FileSystemType.HFS_Plus => 65536,  // 64 KB
            CrossPlatformPathHandler.FileSystemType.FAT32 => 32768,     // 32 KB
            CrossPlatformPathHandler.FileSystemType.exFAT => 65536,     // 64 KB
            _ => 65536 // Default 64 KB
        };
    }

    /// <summary>
    /// ファイルシステム固有の最適化を適用
    /// Apply file system specific optimizations
    /// </summary>
    private void ApplyFileSystemSpecificOptimizations(
        string path,
        bool useCompression,
        bool useEncryption)
    {
        switch (_fileSystem)
        {
            case CrossPlatformPathHandler.FileSystemType.NTFS:
                ApplyNTFSOptimizations(path, useCompression, useEncryption);
                break;

            case CrossPlatformPathHandler.FileSystemType.Ext4:
                ApplyExt4Optimizations(path, useCompression);
                break;

            case CrossPlatformPathHandler.FileSystemType.APFS:
                ApplyAPFSOptimizations(path, useCompression, useEncryption);
                break;
        }
    }

    /// <summary>
    /// NTFS固有の最適化
    /// NTFS-specific optimizations
    /// </summary>
    private void ApplyNTFSOptimizations(string path, bool useCompression, bool useEncryption)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            var shell = new CrossPlatformShellIntegration();

            if (useCompression && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // NTFS compression can be set via file attributes
                // Requires P/Invoke or Process.Start("compact.exe")
                _ = shell.ExecuteCommandAsync($"compact /c \"{path}\"", timeoutMs: 5000).Result;
            }

            if (useEncryption && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // NTFS encryption (EFS) via file attributes
                _ = shell.ExecuteCommandAsync($"cipher /e \"{path}\"", timeoutMs: 5000).Result;
            }
        }
        catch
        {
            // Ignore errors - best effort
        }
    }

    /// <summary>
    /// ext4固有の最適化
    /// ext4-specific optimizations
    /// </summary>
    private void ApplyExt4Optimizations(string path, bool useCompression)
    {
        try
        {
            if (useCompression && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // ext4 compression requires kernel support
                // Using chattr to set compression attribute
                var shell = new CrossPlatformShellIntegration();
                _ = shell.ExecuteCommandAsync($"chattr +c \"{path}\"", timeoutMs: 5000).Result;
            }
        }
        catch
        {
            // Ignore errors - best effort
        }
    }

    /// <summary>
    /// APFS固有の最適化
    /// APFS-specific optimizations
    /// </summary>
    private void ApplyAPFSOptimizations(string path, bool useCompression, bool useEncryption)
    {
        try
        {
            // APFS has native compression and encryption
            // These are usually set at volume level, not per-file
            // File-level operations are handled by macOS automatically
        }
        catch
        {
            // Ignore errors
        }
    }

    /// <summary>
    /// 最適化されたコピーを試行（CoW、リフリンクなど）
    /// Try optimized copy (CoW, reflink, etc.)
    /// </summary>
    private bool TryOptimizedCopy(string sourcePath, string destinationPath)
    {
        try
        {
            switch (_fileSystem)
            {
                case CrossPlatformPathHandler.FileSystemType.APFS:
                    // APFS supports clonefile
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        var shell = new CrossPlatformShellIntegration();
                        var result = shell.ExecuteCommandAsync(
                            $"cp -c \"{sourcePath}\" \"{destinationPath}\"",
                            timeoutMs: 10000
                        ).Result;
                        return result.Success;
                    }
                    break;

                case CrossPlatformPathHandler.FileSystemType.Ext4:
                    // ext4 supports reflink (if enabled)
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        var shell = new CrossPlatformShellIntegration();
                        var result = shell.ExecuteCommandAsync(
                            $"cp --reflink=auto \"{sourcePath}\" \"{destinationPath}\"",
                            timeoutMs: 10000
                        ).Result;
                        return result.Success;
                    }
                    break;
            }
        }
        catch
        {
            // Fall through to regular copy
        }

        return false;
    }

    /// <summary>
    /// ファイルメタデータを保持
    /// Preserve file metadata
    /// </summary>
    private void PreserveFileMetadata(string sourcePath, string destinationPath)
    {
        try
        {
            var sourceInfo = new FileInfo(sourcePath);
            var destInfo = new FileInfo(destinationPath);

            destInfo.CreationTime = sourceInfo.CreationTime;
            destInfo.LastWriteTime = sourceInfo.LastWriteTime;
            destInfo.LastAccessTime = sourceInfo.LastAccessTime;
            destInfo.Attributes = sourceInfo.Attributes;
        }
        catch
        {
            // Ignore errors - best effort
        }
    }

    /// <summary>
    /// ファイルシステム情報を取得
    /// Get file system information
    /// </summary>
    public FileSystemPerformanceInfo GetPerformanceInfo(string path)
    {
        path = _pathHandler.NormalizePath(path);

        var driveInfo = new DriveInfo(Path.GetPathRoot(path) ?? "/");

        return new FileSystemPerformanceInfo
        {
            FileSystem = _fileSystem,
            TotalSize = driveInfo.TotalSize,
            AvailableSpace = driveInfo.AvailableFreeSpace,
            UsedSpace = driveInfo.TotalSize - driveInfo.AvailableFreeSpace,
            UsedPercentage = (double)(driveInfo.TotalSize - driveInfo.AvailableFreeSpace) / driveInfo.TotalSize * 100,
            OptimalBufferSize = GetOptimalBufferSize(),
            SupportsCompression = SupportsCompression(),
            SupportsEncryption = SupportsEncryption(),
            SupportsCopyOnWrite = SupportsCopyOnWrite()
        };
    }

    private bool SupportsCompression()
    {
        return _fileSystem switch
        {
            CrossPlatformPathHandler.FileSystemType.NTFS => true,
            CrossPlatformPathHandler.FileSystemType.Ext4 => true,
            CrossPlatformPathHandler.FileSystemType.APFS => true,
            CrossPlatformPathHandler.FileSystemType.HFS_Plus => true,
            _ => false
        };
    }

    private bool SupportsEncryption()
    {
        return _fileSystem switch
        {
            CrossPlatformPathHandler.FileSystemType.NTFS => true,
            CrossPlatformPathHandler.FileSystemType.Ext4 => true,
            CrossPlatformPathHandler.FileSystemType.APFS => true,
            _ => false
        };
    }

    private bool SupportsCopyOnWrite()
    {
        return _fileSystem switch
        {
            CrossPlatformPathHandler.FileSystemType.APFS => true,
            CrossPlatformPathHandler.FileSystemType.Ext4 => true, // With reflink
            _ => false
        };
    }

    public class BatchOperationResult
    {
        public bool Success { get; set; }
        public int TotalFiles { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class FileSystemPerformanceInfo
    {
        public CrossPlatformPathHandler.FileSystemType FileSystem { get; set; }
        public long TotalSize { get; set; }
        public long AvailableSpace { get; set; }
        public long UsedSpace { get; set; }
        public double UsedPercentage { get; set; }
        public int OptimalBufferSize { get; set; }
        public bool SupportsCompression { get; set; }
        public bool SupportsEncryption { get; set; }
        public bool SupportsCopyOnWrite { get; set; }
    }
}
