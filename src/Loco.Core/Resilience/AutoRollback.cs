using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Loco.Core.Resilience;

/// <summary>
/// 自動ロールバック機能
/// 問題: 「突然の大規模変更でワークフローが壊れる」（Reddit最大の不満）
/// 解決: 変更を自動でロールバックし、以前の状態に復元
/// </summary>
public class AutoRollback
{
    private readonly string _snapshotDirectory;
    private readonly int _maxSnapshots;

    public class Snapshot
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public Dictionary<string, string> Files { get; set; } = new(); // Path -> Content hash
        public Dictionary<string, object> Configuration { get; set; } = new();
        public Dictionary<string, object> State { get; set; } = new();
        public bool IsAutomatic { get; set; }
        public string? TriggeredBy { get; set; }
    }

    public class RollbackResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string MessageJa { get; set; } = string.Empty;
        public int FilesRestored { get; set; }
        public List<string> Errors { get; set; } = new();
        public TimeSpan Duration { get; set; }
    }

    public AutoRollback(string? snapshotDirectory = null, int maxSnapshots = 10)
    {
        _snapshotDirectory = snapshotDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Loco", "snapshots");
        _maxSnapshots = maxSnapshots;

        Directory.CreateDirectory(_snapshotDirectory);
    }

    /// <summary>
    /// スナップショットを作成
    /// </summary>
    public async Task<Snapshot> CreateSnapshotAsync(string description, bool isAutomatic = false,
        Dictionary<string, string>? filesToBackup = null,
        Dictionary<string, object>? configuration = null,
        Dictionary<string, object>? state = null)
    {
        var snapshot = new Snapshot
        {
            Description = description,
            IsAutomatic = isAutomatic,
            TriggeredBy = Environment.UserName,
            Files = filesToBackup ?? new Dictionary<string, string>(),
            Configuration = configuration ?? new Dictionary<string, object>(),
            State = state ?? new Dictionary<string, object>()
        };

        // スナップショットを保存
        var snapshotPath = Path.Combine(_snapshotDirectory, $"snapshot-{snapshot.Id}.json");
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(snapshotPath, json).ConfigureAwait(false);

        // ファイル内容をバックアップ
        if (filesToBackup != null && filesToBackup.Any())
        {
            var backupDir = Path.Combine(_snapshotDirectory, snapshot.Id);
            Directory.CreateDirectory(backupDir);

            foreach (var (filePath, _) in filesToBackup)
            {
                if (File.Exists(filePath))
                {
                    var fileName = Path.GetFileName(filePath);
                    var backupPath = Path.Combine(backupDir, fileName);
                    File.Copy(filePath, backupPath, overwrite: true);
                }
            }
        }

        // 古いスナップショットを削除
        await CleanupOldSnapshotsAsync().ConfigureAwait(false);

        return snapshot;
    }

    /// <summary>
    /// スナップショットからロールバック
    /// </summary>
    public async Task<RollbackResult> RollbackAsync(string snapshotId)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new RollbackResult();

        try
        {
            // スナップショットを読み込み
            var snapshotPath = Path.Combine(_snapshotDirectory, $"snapshot-{snapshotId}.json");
            if (!File.Exists(snapshotPath))
            {
                result.Success = false;
                result.Message = $"Snapshot not found: {snapshotId}";
                result.MessageJa = $"スナップショットが見つかりません: {snapshotId}";
                return result;
            }

            var json = await File.ReadAllTextAsync(snapshotPath).ConfigureAwait(false);
            var snapshot = JsonSerializer.Deserialize<Snapshot>(json);

            if (snapshot == null)
            {
                result.Success = false;
                result.Message = "Failed to deserialize snapshot";
                result.MessageJa = "スナップショットの読み込みに失敗しました";
                return result;
            }

            // 現在の状態をバックアップ（ロールバック前の安全ネット）
            await CreateSnapshotAsync(
                $"Pre-rollback backup (before restoring {snapshotId})",
                isAutomatic: true
            ).ConfigureAwait(false);

            // ファイルを復元
            var backupDir = Path.Combine(_snapshotDirectory, snapshotId);
            if (Directory.Exists(backupDir))
            {
                foreach (var (originalPath, _) in snapshot.Files)
                {
                    try
                    {
                        var fileName = Path.GetFileName(originalPath);
                        var backupPath = Path.Combine(backupDir, fileName);

                        if (File.Exists(backupPath))
                        {
                            // ディレクトリが存在しない場合は作成
                            var directory = Path.GetDirectoryName(originalPath);
                            if (!string.IsNullOrEmpty(directory))
                            {
                                Directory.CreateDirectory(directory);
                            }

                            File.Copy(backupPath, originalPath, overwrite: true);
                            result.FilesRestored++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Failed to restore {originalPath}: {ex.Message}");
                    }
                }
            }

            sw.Stop();
            result.Duration = sw.Elapsed;
            result.Success = result.Errors.Count == 0;
            result.Message = result.Success
                ? $"Successfully rolled back to snapshot {snapshotId} (restored {result.FilesRestored} files)"
                : $"Rollback completed with {result.Errors.Count} errors";
            result.MessageJa = result.Success
                ? $"スナップショット {snapshotId} にロールバックしました（{result.FilesRestored}ファイル復元）"
                : $"ロールバックが完了しましたが、{result.Errors.Count}個のエラーがあります";

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.Success = false;
            result.Duration = sw.Elapsed;
            result.Message = $"Rollback failed: {ex.Message}";
            result.MessageJa = $"ロールバックに失敗しました: {ex.Message}";
            result.Errors.Add(ex.Message);
            return result;
        }
    }

    /// <summary>
    /// スナップショット一覧を取得
    /// </summary>
    public async Task<List<Snapshot>> ListSnapshotsAsync()
    {
        var snapshots = new List<Snapshot>();
        var snapshotFiles = Directory.GetFiles(_snapshotDirectory, "snapshot-*.json");

        foreach (var file in snapshotFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                var snapshot = JsonSerializer.Deserialize<Snapshot>(json);
                if (snapshot != null)
                {
                    snapshots.Add(snapshot);
                }
            }
            catch
            {
                // 破損したファイルはスキップ
            }
        }

        return snapshots.OrderByDescending(s => s.Timestamp).ToList();
    }

    /// <summary>
    /// 最新のスナップショットを取得
    /// </summary>
    public async Task<Snapshot?> GetLatestSnapshotAsync()
    {
        var snapshots = await ListSnapshotsAsync().ConfigureAwait(false);
        return snapshots.FirstOrDefault();
    }

    /// <summary>
    /// 古いスナップショットを削除
    /// </summary>
    private async Task CleanupOldSnapshotsAsync()
    {
        var snapshots = await ListSnapshotsAsync().ConfigureAwait(false);

        if (snapshots.Count > _maxSnapshots)
        {
            var toDelete = snapshots.Skip(_maxSnapshots).ToList();

            foreach (var snapshot in toDelete)
            {
                try
                {
                    // JSONファイルを削除
                    var snapshotPath = Path.Combine(_snapshotDirectory, $"snapshot-{snapshot.Id}.json");
                    if (File.Exists(snapshotPath))
                    {
                        File.Delete(snapshotPath);
                    }

                    // バックアップディレクトリを削除
                    var backupDir = Path.Combine(_snapshotDirectory, snapshot.Id);
                    if (Directory.Exists(backupDir))
                    {
                        Directory.Delete(backupDir, recursive: true);
                    }
                }
                catch
                {
                    // 削除失敗は無視
                }
            }
        }
    }

    /// <summary>
    /// スナップショットを削除
    /// </summary>
    public bool DeleteSnapshot(string snapshotId)
    {
        try
        {
            var snapshotPath = Path.Combine(_snapshotDirectory, $"snapshot-{snapshotId}.json");
            if (File.Exists(snapshotPath))
            {
                File.Delete(snapshotPath);
            }

            var backupDir = Path.Combine(_snapshotDirectory, snapshotId);
            if (Directory.Exists(backupDir))
            {
                Directory.Delete(backupDir, recursive: true);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 自動ロールバック判定
    /// </summary>
    public async Task<bool> ShouldAutoRollbackAsync(
        int errorThreshold = 3,
        TimeSpan? timeWindow = null)
    {
        var window = timeWindow ?? TimeSpan.FromMinutes(5);
        var cutoffTime = DateTime.UtcNow - window;

        // この機能は外部のエラー追跡システムと統合することを想定
        // 現時点では手動ロールバックのみサポート

        await Task.CompletedTask;
        return false; // 将来実装予定
    }

    /// <summary>
    /// スナップショット情報を表示
    /// </summary>
    public string FormatSnapshotInfo(Snapshot snapshot)
    {
        return $@"
╔══════════════════════════════════════════════════════════════╗
║  Snapshot Information / スナップショット情報                  ║
╚══════════════════════════════════════════════════════════════╝

ID:          {snapshot.Id}
Description: {snapshot.Description}
説明:        {snapshot.Description}

Created:     {snapshot.Timestamp:yyyy-MM-dd HH:mm:ss} UTC
Type:        {(snapshot.IsAutomatic ? "Automatic" : "Manual")}
             {(snapshot.IsAutomatic ? "自動" : "手動")}
Triggered by: {snapshot.TriggeredBy}

Files backed up: {snapshot.Files.Count}
バックアップファイル数: {snapshot.Files.Count}

Configuration items: {snapshot.Configuration.Count}
設定項目数: {snapshot.Configuration.Count}
";
    }

    /// <summary>
    /// ロールバック結果を表示
    /// </summary>
    public string FormatRollbackResult(RollbackResult result)
    {
        var status = result.Success ? "✓ SUCCESS" : "✗ FAILED";
        var statusJa = result.Success ? "✓ 成功" : "✗ 失敗";

        var output = $@"
╔══════════════════════════════════════════════════════════════╗
║  Rollback Result / ロールバック結果                          ║
╚══════════════════════════════════════════════════════════════╝

Status: {status} / {statusJa}

{result.Message}
{result.MessageJa}

Files restored: {result.FilesRestored}
復元ファイル数: {result.FilesRestored}

Duration: {result.Duration.TotalSeconds:F2}s
所要時間: {result.Duration.TotalSeconds:F2}秒
";

        if (result.Errors.Any())
        {
            output += "\nErrors / エラー:\n";
            foreach (var error in result.Errors)
            {
                output += $"  • {error}\n";
            }
        }

        return output;
    }
}
