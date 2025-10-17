using System.IO.Compression;
using System.Text.Json;

namespace Loco.Core.Configuration;

/// <summary>
/// 設定の自動バックアップ機能
/// 市販レベル・国家レベルで必須の設定保護機能
/// 誤った設定変更からの迅速な復旧を可能にする
/// </summary>
public sealed class ConfigBackup
{
    private readonly string _configDirectory;
    private readonly string _backupDirectory;
    private readonly int _maxBackups;

    /// <summary>
    /// バックアップメタデータ
    /// </summary>
    public class BackupMetadata
    {
        public string BackupId { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public List<string> Files { get; set; } = new();
        public long TotalSizeBytes { get; set; }
        public string CreatedBy { get; set; } = Environment.UserName;
        public string MachineName { get; set; } = Environment.MachineName;
    }

    /// <summary>
    /// ConfigBackupを初期化します
    /// </summary>
    /// <param name="maxBackups">最大保持バックアップ数（デフォルト: 10）</param>
    public ConfigBackup(int maxBackups = 10)
    {
        var locoDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Loco");

        _configDirectory = Path.Combine(locoDataDir, "config");
        _backupDirectory = Path.Combine(locoDataDir, "config-backups");
        _maxBackups = maxBackups;

        try
        {
            Directory.CreateDirectory(_backupDirectory);
        }
        catch
        {
            // ディレクトリ作成失敗は無視
        }
    }

    /// <summary>
    /// 現在の設定をバックアップします
    /// </summary>
    /// <param name="description">バックアップの説明（オプション）</param>
    /// <returns>バックアップファイルのパス、失敗時はnull</returns>
    public async Task<string?> CreateBackupAsync(string? description = null)
    {
        if (!Directory.Exists(_configDirectory))
        {
            return null; // 設定ディレクトリが存在しない
        }

        try
        {
            var backupId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var backupFileName = $"config-backup-{backupId}.zip";
            var backupFilePath = Path.Combine(_backupDirectory, backupFileName);

            var metadata = new BackupMetadata
            {
                BackupId = backupId,
                Description = description ?? "自動バックアップ"
            };

            // 一時ディレクトリを作成
            var tempDir = Path.Combine(Path.GetTempPath(), $"loco-backup-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // 設定ファイルをコピー
                var configFiles = Directory.GetFiles(_configDirectory, "*.*", SearchOption.AllDirectories);
                long totalSize = 0;

                foreach (var file in configFiles)
                {
                    var relativePath = Path.GetRelativePath(_configDirectory, file);
                    var destPath = Path.Combine(tempDir, relativePath);
                    var destDir = Path.GetDirectoryName(destPath);

                    if (!string.IsNullOrEmpty(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    File.Copy(file, destPath, true);

                    var fileInfo = new FileInfo(file);
                    totalSize += fileInfo.Length;
                    metadata.Files.Add(relativePath);
                }

                metadata.TotalSizeBytes = totalSize;

                // メタデータを保存
                var metadataPath = Path.Combine(tempDir, "backup-metadata.json");
                var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(metadataPath, metadataJson).ConfigureAwait(false);

                // ZIPファイルを作成
                if (File.Exists(backupFilePath))
                {
                    File.Delete(backupFilePath);
                }

                ZipFile.CreateFromDirectory(tempDir, backupFilePath, CompressionLevel.Optimal, false);

                // 古いバックアップを削除
                await CleanupOldBackupsAsync().ConfigureAwait(false);

                return backupFilePath;
            }
            finally
            {
                // 一時ディレクトリを削除
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// バックアップから設定を復元します
    /// </summary>
    /// <param name="backupFilePath">バックアップファイルのパス</param>
    /// <returns>成功時はtrue</returns>
    public async Task<bool> RestoreBackupAsync(string backupFilePath)
    {
        if (!File.Exists(backupFilePath))
        {
            return false;
        }

        try
        {
            // 現在の設定をバックアップ（復元前バックアップ）
            await CreateBackupAsync("復元前の自動バックアップ").ConfigureAwait(false);

            // 一時ディレクトリに解凍
            var tempDir = Path.Combine(Path.GetTempPath(), $"loco-restore-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                ZipFile.ExtractToDirectory(backupFilePath, tempDir);

                // メタデータを読み込み
                var metadataPath = Path.Combine(tempDir, "backup-metadata.json");
                if (File.Exists(metadataPath))
                {
                    var metadataJson = await File.ReadAllTextAsync(metadataPath).ConfigureAwait(false);
                    var metadata = JsonSerializer.Deserialize<BackupMetadata>(metadataJson);

                    if (metadata != null)
                    {
                        Console.WriteLine($"バックアップ情報:");
                        Console.WriteLine($"  作成日時: {metadata.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
                        Console.WriteLine($"  説明: {metadata.Description}");
                        Console.WriteLine($"  ファイル数: {metadata.Files.Count}");
                    }
                }

                // 現在の設定ディレクトリをクリア
                if (Directory.Exists(_configDirectory))
                {
                    Directory.Delete(_configDirectory, true);
                }
                Directory.CreateDirectory(_configDirectory);

                // バックアップから復元
                var files = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith("backup-metadata.json"));

                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(tempDir, file);
                    var destPath = Path.Combine(_configDirectory, relativePath);
                    var destDir = Path.GetDirectoryName(destPath);

                    if (!string.IsNullOrEmpty(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    File.Copy(file, destPath, true);
                }

                return true;
            }
            finally
            {
                // 一時ディレクトリを削除
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 利用可能なバックアップ一覧を取得します
    /// </summary>
    public async Task<List<BackupInfo>> ListBackupsAsync()
    {
        var backups = new List<BackupInfo>();

        if (!Directory.Exists(_backupDirectory))
        {
            return backups;
        }

        try
        {
            var backupFiles = Directory.GetFiles(_backupDirectory, "config-backup-*.zip")
                .OrderByDescending(f => File.GetLastWriteTimeUtc(f));

            foreach (var backupFile in backupFiles)
            {
                try
                {
                    var info = new BackupInfo
                    {
                        FilePath = backupFile,
                        FileName = Path.GetFileName(backupFile),
                        CreatedTime = File.GetCreationTimeUtc(backupFile),
                        SizeBytes = new FileInfo(backupFile).Length
                    };

                    // メタデータを読み込み
                    using var archive = ZipFile.OpenRead(backupFile);
                    var metadataEntry = archive.GetEntry("backup-metadata.json");

                    if (metadataEntry != null)
                    {
                        using var stream = metadataEntry.Open();
                        using var reader = new StreamReader(stream);
                        var metadataJson = await reader.ReadToEndAsync().ConfigureAwait(false);
                        var metadata = JsonSerializer.Deserialize<BackupMetadata>(metadataJson);

                        if (metadata != null)
                        {
                            info.Description = metadata.Description;
                            info.FileCount = metadata.Files.Count;
                        }
                    }

                    backups.Add(info);
                }
                catch
                {
                    // 個別のバックアップ読み込みエラーは無視
                }
            }
        }
        catch
        {
            // エラーは無視
        }

        return backups;
    }

    /// <summary>
    /// バックアップ情報
    /// </summary>
    public class BackupInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
        public long SizeBytes { get; set; }
        public string Description { get; set; } = string.Empty;
        public int FileCount { get; set; }

        public string FormattedSize
        {
            get
            {
                if (SizeBytes >= 1024 * 1024)
                    return $"{SizeBytes / 1024.0 / 1024.0:F2} MB";
                if (SizeBytes >= 1024)
                    return $"{SizeBytes / 1024.0:F2} KB";
                return $"{SizeBytes} B";
            }
        }
    }

    /// <summary>
    /// 古いバックアップを削除します
    /// </summary>
    private async Task CleanupOldBackupsAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(_backupDirectory))
                {
                    return;
                }

                var backupFiles = Directory.GetFiles(_backupDirectory, "config-backup-*.zip")
                    .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                    .ToList();

                // 最大数を超えたバックアップを削除
                for (int i = _maxBackups; i < backupFiles.Count; i++)
                {
                    try
                    {
                        File.Delete(backupFiles[i]);
                    }
                    catch
                    {
                        // 削除失敗は無視
                    }
                }
            }
            catch
            {
                // エラーは無視
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// バックアップを削除します
    /// </summary>
    public bool DeleteBackup(string backupFilePath)
    {
        try
        {
            if (File.Exists(backupFilePath))
            {
                File.Delete(backupFilePath);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// すべてのバックアップを削除します
    /// </summary>
    public int DeleteAllBackups()
    {
        int deletedCount = 0;

        try
        {
            if (!Directory.Exists(_backupDirectory))
            {
                return 0;
            }

            var backupFiles = Directory.GetFiles(_backupDirectory, "config-backup-*.zip");

            foreach (var file in backupFiles)
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                }
                catch
                {
                    // 削除失敗は無視して続行
                }
            }
        }
        catch
        {
            // エラーは無視
        }

        return deletedCount;
    }

    /// <summary>
    /// 設定変更を検出したときに自動バックアップを作成します
    /// </summary>
    public async Task<bool> CreateAutoBackupIfNeededAsync()
    {
        // 最新のバックアップを確認
        var backups = await ListBackupsAsync().ConfigureAwait(false);

        if (backups.Count == 0)
        {
            // バックアップがない場合は作成
            var result = await CreateBackupAsync("初回自動バックアップ").ConfigureAwait(false);
            return result != null;
        }

        var latestBackup = backups.First();

        // 最新バックアップが24時間以上前の場合は新しいバックアップを作成
        if ((DateTime.UtcNow - latestBackup.CreatedTime).TotalHours > 24)
        {
            var result = await CreateBackupAsync("定期自動バックアップ").ConfigureAwait(false);
            return result != null;
        }

        return false; // バックアップ不要
    }
}
