using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Logging
{
    /// <summary>
    /// ログファイルの管理とクリーンアップを行うユーティリティ
    /// </summary>
    public static class LogManager
    {
        /// <summary>
        /// 古いログファイルをクリーンアップ
        /// </summary>
        public static void CleanupOldLogs(string logDirectory, int retentionDays, ILogger? logger = null)
        {
            try
            {
                if (!Directory.Exists(logDirectory))
                {
                    logger?.LogInformation("Log directory does not exist: {LogDirectory}", logDirectory);
                    return;
                }

                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                var logFiles = Directory.GetFiles(logDirectory, "*.log", SearchOption.AllDirectories)
                    .Where(f => File.GetLastWriteTimeUtc(f) < cutoffDate)
                    .ToArray();

                if (logFiles.Length == 0)
                {
                    logger?.LogInformation("No old log files to clean up in {LogDirectory}", logDirectory);
                    return;
                }

                var deletedCount = 0;
                var totalSize = 0L;

                foreach (var logFile in logFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(logFile);
                        totalSize += fileInfo.Length;

                        File.Delete(logFile);
                        deletedCount++;

                        logger?.LogInformation("Deleted old log file: {LogFile}", logFile);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to delete log file: {LogFile}", logFile);
                    }
                }

                var sizeText = totalSize > 1024 * 1024
                    ? $"{totalSize / (1024.0 * 1024.0):F1} MB"
                    : $"{totalSize / 1024.0:F1} KB";

                logger?.LogInformation("Log cleanup completed: {DeletedCount} files deleted, {TotalSize} freed",
                    deletedCount, sizeText);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to cleanup old logs in {LogDirectory}", logDirectory);
            }
        }

        /// <summary>
        /// ログディレクトリの統計情報を取得
        /// </summary>
        public static LogStats GetLogStats(string logDirectory)
        {
            var stats = new LogStats();

            try
            {
                if (!Directory.Exists(logDirectory))
                {
                    return stats;
                }

                var logFiles = Directory.GetFiles(logDirectory, "*.log", SearchOption.AllDirectories);

                foreach (var file in logFiles)
                {
                    var fileInfo = new FileInfo(file);
                    stats.TotalFiles++;
                    stats.TotalSize += fileInfo.Length;

                    if (fileInfo.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-7))
                    {
                        stats.OldFiles++;
                        stats.OldSize += fileInfo.Length;
                    }
                }
            }
            catch (Exception)
            {
                // 統計取得エラーは無視
            }

            return stats;
        }

        /// <summary>
        /// ログファイルのサイズを制限
        /// </summary>
        public static void RotateLogFile(string logFilePath, long maxSizeBytes, ILogger? logger = null)
        {
            try
            {
                if (!File.Exists(logFilePath))
                {
                    return;
                }

                var fileInfo = new FileInfo(logFilePath);
                if (fileInfo.Length < maxSizeBytes)
                {
                    return;
                }

                // ログファイルをローテーション
                var backupPath = logFilePath + ".1";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                File.Move(logFilePath, backupPath);

                // 元のログファイルを空で再作成
                File.WriteAllText(logFilePath, string.Empty);

                logger?.LogInformation("Rotated log file: {LogFile} (size: {Size} bytes)",
                    logFilePath, fileInfo.Length);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to rotate log file: {LogFile}", logFilePath);
            }
        }
    }

    /// <summary>
    /// ログ統計情報
    /// </summary>
    public class LogStats
    {
        public int TotalFiles { get; set; }
        public long TotalSize { get; set; }
        public int OldFiles { get; set; }
        public long OldSize { get; set; }

        public string GetSummary()
        {
            var totalSizeText = TotalSize > 1024 * 1024
                ? $"{TotalSize / (1024.0 * 1024.0):F1} MB"
                : $"{TotalSize / 1024.0:F1} KB";

            var oldSizeText = OldSize > 1024 * 1024
                ? $"{OldSize / (1024.0 * 1024.0):F1} MB"
                : $"{OldSize / 1024.0:F1} KB";

            return $"Total: {TotalFiles} files ({totalSizeText}), Old: {OldFiles} files ({oldSizeText})";
        }
    }
}
