using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Loco.Core.Platform;

/// <summary>
/// クロスプラットフォームパスハンドラー
/// Cross-platform path handler for Windows/Linux/macOS
///
/// 問題: パスの区切り文字の違い（Windows: \、Linux/macOS: /）
/// Problem: Different path separators (Windows: \, Linux/macOS: /)
///
/// 解決策: OS固有の処理を自動検出して適切に処理
/// Solution: Auto-detect OS and handle paths appropriately
/// </summary>
public class CrossPlatformPathHandler
{
    private readonly PlatformType _platform;
    private readonly FileSystemType _fileSystem;

    public enum PlatformType
    {
        Windows,
        Linux,
        MacOS,
        Unknown
    }

    public enum FileSystemType
    {
        NTFS,       // Windows
        Ext4,       // Linux
        APFS,       // macOS
        HFS_Plus,   // macOS (legacy)
        FAT32,      // Cross-platform
        exFAT,      // Cross-platform
        Unknown
    }

    public CrossPlatformPathHandler()
    {
        _platform = DetectPlatform();
        _fileSystem = DetectFileSystem();
    }

    /// <summary>
    /// プラットフォームを検出
    /// Detect current platform
    /// </summary>
    public static PlatformType DetectPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return PlatformType.Windows;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return PlatformType.Linux;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return PlatformType.MacOS;
        }
        return PlatformType.Unknown;
    }

    /// <summary>
    /// ファイルシステムを検出
    /// Detect file system type
    /// </summary>
    public static FileSystemType DetectFileSystem()
    {
        var platform = DetectPlatform();

        switch (platform)
        {
            case PlatformType.Windows:
                // Windows通常はNTFS
                return FileSystemType.NTFS;

            case PlatformType.Linux:
                // Linux通常はext4
                return FileSystemType.Ext4;

            case PlatformType.MacOS:
                // macOS 10.13+はAPFS
                if (Environment.OSVersion.Version.Major >= 10)
                {
                    return FileSystemType.APFS;
                }
                return FileSystemType.HFS_Plus;

            default:
                return FileSystemType.Unknown;
        }
    }

    public PlatformType Platform => _platform;
    public FileSystemType FileSystem => _fileSystem;

    /// <summary>
    /// パスを正規化（クロスプラットフォーム対応）
    /// Normalize path for cross-platform compatibility
    /// </summary>
    public string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        // 環境変数を展開
        path = Environment.ExpandEnvironmentVariables(path);

        // プラットフォーム固有の処理
        switch (_platform)
        {
            case PlatformType.Windows:
                // バックスラッシュに統一
                path = path.Replace('/', '\\');
                // ドライブレターを大文字化
                if (path.Length >= 2 && path[1] == ':')
                {
                    path = char.ToUpper(path[0]) + path.Substring(1);
                }
                break;

            case PlatformType.Linux:
            case PlatformType.MacOS:
                // フォワードスラッシュに統一
                path = path.Replace('\\', '/');
                // ホームディレクトリ展開
                if (path.StartsWith("~"))
                {
                    path = path.Replace("~", Environment.GetEnvironmentVariable("HOME") ?? "");
                }
                break;
        }

        // 連続するセパレーターを削除
        var separator = Path.DirectorySeparatorChar;
        var escapedSep = Regex.Escape(separator.ToString());
        path = Regex.Replace(path, escapedSep + "{2,}", separator.ToString());

        return path;
    }

    /// <summary>
    /// パスを結合（クロスプラットフォーム対応）
    /// Combine paths with cross-platform support
    /// </summary>
    public string CombinePaths(params string[] paths)
    {
        if (paths == null || paths.Length == 0) return "";

        var normalizedPaths = paths.Select(NormalizePath).ToArray();
        var combined = Path.Combine(normalizedPaths);
        return NormalizePath(combined);
    }

    /// <summary>
    /// 絶対パスを取得
    /// Get absolute path
    /// </summary>
    public string GetAbsolutePath(string path)
    {
        path = NormalizePath(path);
        return Path.GetFullPath(path);
    }

    /// <summary>
    /// 相対パスを取得
    /// Get relative path
    /// </summary>
    public string GetRelativePath(string basePath, string targetPath)
    {
        basePath = NormalizePath(basePath);
        targetPath = NormalizePath(targetPath);
        return Path.GetRelativePath(basePath, targetPath);
    }

    /// <summary>
    /// テンポラリディレクトリを取得
    /// Get temporary directory (OS-specific)
    /// </summary>
    public string GetTempDirectory()
    {
        return Path.GetTempPath();
    }

    /// <summary>
    /// ユーザーホームディレクトリを取得
    /// Get user home directory (OS-specific)
    /// </summary>
    public string GetHomeDirectory()
    {
        return _platform switch
        {
            PlatformType.Windows => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            PlatformType.Linux => Environment.GetEnvironmentVariable("HOME") ?? "/home",
            PlatformType.MacOS => Environment.GetEnvironmentVariable("HOME") ?? "/Users",
            _ => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
    }

    /// <summary>
    /// アプリケーションデータディレクトリを取得
    /// Get application data directory (OS-specific)
    /// </summary>
    public string GetAppDataDirectory(string appName)
    {
        var baseDir = _platform switch
        {
            PlatformType.Windows => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            PlatformType.Linux => CombinePaths(GetHomeDirectory(), ".local", "share"),
            PlatformType.MacOS => CombinePaths(GetHomeDirectory(), "Library", "Application Support"),
            _ => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        };

        return CombinePaths(baseDir, appName);
    }

    /// <summary>
    /// 設定ファイルディレクトリを取得
    /// Get configuration directory (OS-specific)
    /// </summary>
    public string GetConfigDirectory(string appName)
    {
        var baseDir = _platform switch
        {
            PlatformType.Windows => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            PlatformType.Linux => CombinePaths(GetHomeDirectory(), ".config"),
            PlatformType.MacOS => CombinePaths(GetHomeDirectory(), "Library", "Preferences"),
            _ => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        };

        return CombinePaths(baseDir, appName);
    }

    /// <summary>
    /// キャッシュディレクトリを取得
    /// Get cache directory (OS-specific)
    /// </summary>
    public string GetCacheDirectory(string appName)
    {
        var baseDir = _platform switch
        {
            PlatformType.Windows => CombinePaths(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
            PlatformType.Linux => CombinePaths(GetHomeDirectory(), ".cache"),
            PlatformType.MacOS => CombinePaths(GetHomeDirectory(), "Library", "Caches"),
            _ => Path.GetTempPath()
        };

        return CombinePaths(baseDir, appName);
    }

    /// <summary>
    /// ログディレクトリを取得
    /// Get logs directory (OS-specific)
    /// </summary>
    public string GetLogsDirectory(string appName)
    {
        var baseDir = _platform switch
        {
            PlatformType.Windows => CombinePaths(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName, "Logs"),
            PlatformType.Linux => CombinePaths("/var", "log", appName),
            PlatformType.MacOS => CombinePaths(GetHomeDirectory(), "Library", "Logs", appName),
            _ => CombinePaths(GetHomeDirectory(), "logs")
        };

        return baseDir;
    }

    /// <summary>
    /// 実行ファイルの拡張子を取得
    /// Get executable extension (OS-specific)
    /// </summary>
    public string GetExecutableExtension()
    {
        return _platform switch
        {
            PlatformType.Windows => ".exe",
            PlatformType.Linux => "",
            PlatformType.MacOS => "",
            _ => ""
        };
    }

    /// <summary>
    /// スクリプトの拡張子を取得
    /// Get script extension (OS-specific)
    /// </summary>
    public string GetScriptExtension()
    {
        return _platform switch
        {
            PlatformType.Windows => ".ps1",
            PlatformType.Linux => ".sh",
            PlatformType.MacOS => ".sh",
            _ => ".sh"
        };
    }

    /// <summary>
    /// パスが有効かチェック
    /// Check if path is valid for current OS
    /// </summary>
    public bool IsValidPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            var normalized = NormalizePath(path);

            // 不正な文字をチェック
            var invalidChars = Path.GetInvalidPathChars();
            if (normalized.Any(c => invalidChars.Contains(c)))
            {
                return false;
            }

            // プラットフォーム固有の検証
            switch (_platform)
            {
                case PlatformType.Windows:
                    // Windowsパス形式のチェック
                    // C:\path または \\server\share
                    if (!Regex.IsMatch(normalized, @"^[A-Z]:\\|^\\\\"))
                    {
                        return !Path.IsPathFullyQualified(normalized) || normalized.StartsWith("\\\\");
                    }
                    break;

                case PlatformType.Linux:
                case PlatformType.MacOS:
                    // Unix形式のチェック（/ で始まるか相対パス）
                    // 特殊文字のチェックは不要（多くが許可されている）
                    break;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// ファイルシステム固有の最適化情報を取得
    /// Get file system specific optimization info
    /// </summary>
    public FileSystemInfo GetFileSystemInfo()
    {
        return new FileSystemInfo
        {
            Type = _fileSystem,
            Platform = _platform,
            MaxFileNameLength = GetMaxFileNameLength(),
            MaxPathLength = GetMaxPathLength(),
            CaseSensitive = IsCaseSensitive(),
            SupportsSymlinks = SupportsSymlinks(),
            SupportsHardlinks = SupportsHardlinks(),
            SupportsCompression = SupportsCompression(),
            SupportsEncryption = SupportsEncryption(),
            OptimalBlockSize = GetOptimalBlockSize()
        };
    }

    private int GetMaxFileNameLength()
    {
        return _fileSystem switch
        {
            FileSystemType.NTFS => 255,
            FileSystemType.Ext4 => 255,
            FileSystemType.APFS => 255,
            FileSystemType.HFS_Plus => 255,
            FileSystemType.FAT32 => 255,
            FileSystemType.exFAT => 255,
            _ => 255
        };
    }

    private int GetMaxPathLength()
    {
        return _platform switch
        {
            PlatformType.Windows => 32767, // With long path support
            PlatformType.Linux => 4096,
            PlatformType.MacOS => 1024,
            _ => 260
        };
    }

    private bool IsCaseSensitive()
    {
        return _platform switch
        {
            PlatformType.Windows => false, // NTFS is case-insensitive by default
            PlatformType.Linux => true,    // ext4 is case-sensitive
            PlatformType.MacOS => false,   // APFS default is case-insensitive
            _ => false
        };
    }

    private bool SupportsSymlinks()
    {
        return _fileSystem switch
        {
            FileSystemType.NTFS => true,
            FileSystemType.Ext4 => true,
            FileSystemType.APFS => true,
            FileSystemType.HFS_Plus => true,
            FileSystemType.FAT32 => false,
            FileSystemType.exFAT => false,
            _ => false
        };
    }

    private bool SupportsHardlinks()
    {
        return _fileSystem switch
        {
            FileSystemType.NTFS => true,
            FileSystemType.Ext4 => true,
            FileSystemType.APFS => true,
            FileSystemType.HFS_Plus => true,
            FileSystemType.FAT32 => false,
            FileSystemType.exFAT => false,
            _ => false
        };
    }

    private bool SupportsCompression()
    {
        return _fileSystem switch
        {
            FileSystemType.NTFS => true,
            FileSystemType.Ext4 => true,  // With extensions
            FileSystemType.APFS => true,
            FileSystemType.HFS_Plus => true,
            FileSystemType.FAT32 => false,
            FileSystemType.exFAT => false,
            _ => false
        };
    }

    private bool SupportsEncryption()
    {
        return _fileSystem switch
        {
            FileSystemType.NTFS => true,  // EFS
            FileSystemType.Ext4 => true,  // With extensions
            FileSystemType.APFS => true,  // Native encryption
            FileSystemType.HFS_Plus => false,
            FileSystemType.FAT32 => false,
            FileSystemType.exFAT => false,
            _ => false
        };
    }

    private int GetOptimalBlockSize()
    {
        return _fileSystem switch
        {
            FileSystemType.NTFS => 4096,
            FileSystemType.Ext4 => 4096,
            FileSystemType.APFS => 4096,
            FileSystemType.HFS_Plus => 4096,
            FileSystemType.FAT32 => 4096,
            FileSystemType.exFAT => 4096,
            _ => 4096
        };
    }

    /// <summary>
    /// パスを他のOS形式に変換
    /// Convert path to other OS format
    /// </summary>
    public string ConvertPathToFormat(string path, PlatformType targetPlatform)
    {
        path = NormalizePath(path);

        if (_platform == targetPlatform) return path;

        // Windows → Linux/macOS
        if (_platform == PlatformType.Windows &&
            (targetPlatform == PlatformType.Linux || targetPlatform == PlatformType.MacOS))
        {
            // C:\Users\... → /mnt/c/Users/... (WSL style)
            if (path.Length >= 3 && path[1] == ':' && path[2] == '\\')
            {
                var drive = char.ToLower(path[0]);
                path = $"/mnt/{drive}" + path.Substring(2).Replace('\\', '/');
            }
            else
            {
                path = path.Replace('\\', '/');
            }
        }
        // Linux/macOS → Windows
        else if ((targetPlatform == PlatformType.Windows) &&
                 (_platform == PlatformType.Linux || _platform == PlatformType.MacOS))
        {
            // /mnt/c/Users/... → C:\Users\...
            if (path.StartsWith("/mnt/") && path.Length >= 6)
            {
                var drive = char.ToUpper(path[5]);
                path = $"{drive}:" + path.Substring(6).Replace('/', '\\');
            }
            else
            {
                path = path.Replace('/', '\\');
            }
        }

        return path;
    }

    public class FileSystemInfo
    {
        public FileSystemType Type { get; set; }
        public PlatformType Platform { get; set; }
        public int MaxFileNameLength { get; set; }
        public int MaxPathLength { get; set; }
        public bool CaseSensitive { get; set; }
        public bool SupportsSymlinks { get; set; }
        public bool SupportsHardlinks { get; set; }
        public bool SupportsCompression { get; set; }
        public bool SupportsEncryption { get; set; }
        public int OptimalBlockSize { get; set; }
    }
}
