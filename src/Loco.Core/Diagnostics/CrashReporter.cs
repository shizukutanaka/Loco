using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Loco.Core.Diagnostics;

/// <summary>
/// クラッシュレポート機能
/// プライバシーに配慮した市販レベルの診断情報収集
/// 個人情報は一切含まれません
/// </summary>
public static class CrashReporter
{
    private static readonly string CrashLogDirectory;

    static CrashReporter()
    {
        var locoDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Loco");
        CrashLogDirectory = Path.Combine(locoDataDir, "crash-reports");

        try
        {
            Directory.CreateDirectory(CrashLogDirectory);
        }
        catch
        {
            // ディレクトリ作成失敗は静かに無視
        }
    }

    /// <summary>
    /// クラッシュレポート
    /// </summary>
    public class CrashReport
    {
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");
        public string Version { get; set; } = string.Empty;
        public string ExceptionType { get; set; } = string.Empty;
        public string ExceptionMessage { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
        public Dictionary<string, string> SystemInfo { get; set; } = new();
        public List<string> RecentActions { get; set; } = new();
    }

    /// <summary>
    /// 例外をクラッシュレポートとして記録します
    /// </summary>
    /// <param name="exception">発生した例外</param>
    /// <param name="version">アプリケーションバージョン</param>
    /// <param name="recentActions">最近実行されたアクション（オプション）</param>
    /// <returns>クラッシュレポートファイルのパス（失敗時はnull）</returns>
    public static string? ReportCrash(Exception exception, string version, List<string>? recentActions = null)
    {
        if (exception == null)
        {
            return null;
        }

        try
        {
            var report = new CrashReport
            {
                Version = version,
                ExceptionType = exception.GetType().FullName ?? "Unknown",
                ExceptionMessage = SanitizeMessage(exception.Message),
                StackTrace = SanitizeStackTrace(exception.StackTrace ?? string.Empty),
                SystemInfo = CollectSystemInfo(),
                RecentActions = recentActions ?? new List<string>()
            };

            // 内部例外も記録
            if (exception.InnerException != null)
            {
                report.SystemInfo["InnerExceptionType"] = exception.InnerException.GetType().FullName ?? "Unknown";
                report.SystemInfo["InnerExceptionMessage"] = SanitizeMessage(exception.InnerException.Message);
            }

            var filename = $"crash-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json";
            var filepath = Path.Combine(CrashLogDirectory, filename);

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(filepath, json, Encoding.UTF8);

            // 古いクラッシュレポートをクリーンアップ（30日以上前）
            CleanupOldReports(30);

            return filepath;
        }
        catch
        {
            // クラッシュレポート作成の失敗は静かに無視
            return null;
        }
    }

    /// <summary>
    /// システム情報を収集します（個人情報は含まれません）
    /// </summary>
    private static Dictionary<string, string> CollectSystemInfo()
    {
        var info = new Dictionary<string, string>();

        try
        {
            // OS情報
            info["OS"] = RuntimeInformation.OSDescription;
            info["OSArchitecture"] = RuntimeInformation.OSArchitecture.ToString();

            // .NET情報
            info["Framework"] = RuntimeInformation.FrameworkDescription;
            info["ProcessArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString();

            // プロセッサ情報
            info["ProcessorCount"] = Environment.ProcessorCount.ToString();

            // メモリ情報
            var process = Process.GetCurrentProcess();
            info["WorkingSet"] = $"{process.WorkingSet64 / 1024 / 1024} MB";
            info["PrivateMemory"] = $"{process.PrivateMemorySize64 / 1024 / 1024} MB";

            // タイミング情報
            info["SystemUptime"] = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"d\.hh\:mm\:ss");

            // GC情報
            info["GCMemory"] = $"{GC.GetTotalMemory(false) / 1024 / 1024} MB";
            info["Gen0Collections"] = GC.CollectionCount(0).ToString();
            info["Gen1Collections"] = GC.CollectionCount(1).ToString();
            info["Gen2Collections"] = GC.CollectionCount(2).ToString();
        }
        catch
        {
            // 情報収集エラーは無視
        }

        return info;
    }

    /// <summary>
    /// 例外メッセージから個人情報を削除します
    /// </summary>
    private static string SanitizeMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        // ファイルパスからユーザー名を削除
        // C:\Users\username\... → C:\Users\***\...
        message = System.Text.RegularExpressions.Regex.Replace(
            message,
            @"C:\\Users\\[^\\]+",
            @"C:\Users\***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Linuxパスからユーザー名を削除
        // /home/username/... → /home/***/...
        message = System.Text.RegularExpressions.Regex.Replace(
            message,
            @"/home/[^/]+",
            "/home/***");

        // macOSパスからユーザー名を削除
        // /Users/username/... → /Users/***/...
        message = System.Text.RegularExpressions.Regex.Replace(
            message,
            @"/Users/[^/]+",
            "/Users/***");

        return message;
    }

    /// <summary>
    /// スタックトレースから個人情報を削除します
    /// </summary>
    private static string SanitizeStackTrace(string stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
        {
            return string.Empty;
        }

        // ファイルパスからユーザー名を削除
        stackTrace = System.Text.RegularExpressions.Regex.Replace(
            stackTrace,
            @"C:\\Users\\[^\\]+",
            @"C:\Users\***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        stackTrace = System.Text.RegularExpressions.Regex.Replace(
            stackTrace,
            @"/home/[^/]+",
            "/home/***");

        stackTrace = System.Text.RegularExpressions.Regex.Replace(
            stackTrace,
            @"/Users/[^/]+",
            "/Users/***");

        return stackTrace;
    }

    /// <summary>
    /// 古いクラッシュレポートを削除します
    /// </summary>
    /// <param name="daysToKeep">保持する日数</param>
    private static void CleanupOldReports(int daysToKeep)
    {
        try
        {
            if (!Directory.Exists(CrashLogDirectory))
            {
                return;
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
            var files = Directory.GetFiles(CrashLogDirectory, "crash-*.json");

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTimeUtc < cutoffDate)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // 削除失敗は無視
                    }
                }
            }
        }
        catch
        {
            // クリーンアップエラーは無視
        }
    }

    /// <summary>
    /// 最近のクラッシュレポート数を取得します
    /// </summary>
    /// <param name="days">過去何日間のレポートをカウントするか</param>
    /// <returns>クラッシュレポート数</returns>
    public static int GetRecentCrashCount(int days = 7)
    {
        try
        {
            if (!Directory.Exists(CrashLogDirectory))
            {
                return 0;
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            var files = Directory.GetFiles(CrashLogDirectory, "crash-*.json");

            return files.Count(file =>
            {
                var fileInfo = new FileInfo(file);
                return fileInfo.LastWriteTimeUtc >= cutoffDate;
            });
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// すべてのクラッシュレポートを削除します
    /// </summary>
    public static void ClearAllReports()
    {
        try
        {
            if (!Directory.Exists(CrashLogDirectory))
            {
                return;
            }

            var files = Directory.GetFiles(CrashLogDirectory, "crash-*.json");
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
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
    }
}
