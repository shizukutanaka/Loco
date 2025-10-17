using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loco.Core.History;

/// <summary>
/// 実行履歴機能
/// YouTubeレビューで指摘される「デバッグが困難」「なぜ失敗したのか分からない」問題を解決
/// </summary>
public sealed class ExecutionHistory : IDisposable
{
    private readonly string _historyFile;
    private readonly int _maxHistoryEntries;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// 実行履歴エントリ
    /// </summary>
    public class HistoryEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("flowName")]
        public string FlowName { get; set; } = string.Empty;

        [JsonPropertyName("ruleName")]
        public string? RuleName { get; set; }

        [JsonPropertyName("status")]
        public ExecutionStatus Status { get; set; }

        [JsonPropertyName("durationMs")]
        public double DurationMs { get; set; }

        [JsonPropertyName("actions")]
        public List<ActionRecord> Actions { get; set; } = new();

        [JsonPropertyName("error")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("errorType")]
        public string? ErrorType { get; set; }

        [JsonPropertyName("stackTrace")]
        public string? StackTrace { get; set; }

        [JsonPropertyName("context")]
        public Dictionary<string, string> Context { get; set; } = new();
    }

    /// <summary>
    /// アクション記録
    /// </summary>
    public class ActionRecord
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public DateTime? EndTime { get; set; }

        [JsonPropertyName("durationMs")]
        public double DurationMs { get; set; }

        [JsonPropertyName("status")]
        public ExecutionStatus Status { get; set; }

        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>
    /// 実行ステータス
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ExecutionStatus
    {
        Success,
        Failed,
        Cancelled,
        Skipped
    }

    /// <summary>
    /// ExecutionHistoryを初期化します
    /// </summary>
    /// <param name="maxEntries">最大保持エントリ数（デフォルト: 100）</param>
    public ExecutionHistory(int maxEntries = 100)
    {
        var locoDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Loco");

        _historyFile = Path.Combine(locoDataDir, "execution-history.json");
        _maxHistoryEntries = maxEntries;

        try
        {
            Directory.CreateDirectory(locoDataDir);
        }
        catch
        {
            // ディレクトリ作成失敗は無視
        }
    }

    /// <summary>
    /// 実行履歴エントリを追加します
    /// </summary>
    public async Task AddEntryAsync(HistoryEntry entry, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = await LoadEntriesAsync(cancellationToken).ConfigureAwait(false);

            // 新しいエントリを追加
            entries.Insert(0, entry);

            // 古いエントリを削除
            if (entries.Count > _maxHistoryEntries)
            {
                entries = entries.Take(_maxHistoryEntries).ToList();
            }

            // 保存
            await SaveEntriesAsync(entries, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 実行履歴を取得します
    /// </summary>
    /// <param name="limit">取得する件数（デフォルト: 10）</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>実行履歴エントリのリスト</returns>
    public async Task<List<HistoryEntry>> GetHistoryAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = await LoadEntriesAsync(cancellationToken).ConfigureAwait(false);
            return entries.Take(limit).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 特定のフローの実行履歴を取得します
    /// </summary>
    public async Task<List<HistoryEntry>> GetHistoryByFlowAsync(
        string flowName,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = await LoadEntriesAsync(cancellationToken).ConfigureAwait(false);
            return entries
                .Where(e => e.FlowName.Equals(flowName, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 失敗した実行履歴のみを取得します
    /// </summary>
    public async Task<List<HistoryEntry>> GetFailedHistoryAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = await LoadEntriesAsync(cancellationToken).ConfigureAwait(false);
            return entries
                .Where(e => e.Status == ExecutionStatus.Failed)
                .Take(limit)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 実行履歴の統計を取得します
    /// </summary>
    public async Task<ExecutionStats> GetStatsAsync(
        TimeSpan? period = null,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = await LoadEntriesAsync(cancellationToken).ConfigureAwait(false);

            if (period.HasValue)
            {
                var cutoff = DateTime.UtcNow - period.Value;
                entries = entries.Where(e => e.Timestamp >= cutoff).ToList();
            }

            return new ExecutionStats
            {
                TotalExecutions = entries.Count,
                SuccessfulExecutions = entries.Count(e => e.Status == ExecutionStatus.Success),
                FailedExecutions = entries.Count(e => e.Status == ExecutionStatus.Failed),
                CancelledExecutions = entries.Count(e => e.Status == ExecutionStatus.Cancelled),
                AverageDurationMs = entries.Any() ? entries.Average(e => e.DurationMs) : 0,
                TotalDurationMs = entries.Sum(e => e.DurationMs),
                MostCommonErrors = entries
                    .Where(e => !string.IsNullOrEmpty(e.ErrorType))
                    .GroupBy(e => e.ErrorType!)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .ToDictionary(g => g.Key, g => g.Count()),
                MostExecutedFlows = entries
                    .GroupBy(e => e.FlowName)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 実行統計
    /// </summary>
    public class ExecutionStats
    {
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public int CancelledExecutions { get; set; }
        public double AverageDurationMs { get; set; }
        public double TotalDurationMs { get; set; }
        public Dictionary<string, int> MostCommonErrors { get; set; } = new();
        public Dictionary<string, int> MostExecutedFlows { get; set; } = new();

        public double SuccessRate => TotalExecutions > 0
            ? (double)SuccessfulExecutions / TotalExecutions * 100
            : 0;
    }

    /// <summary>
    /// エントリを読み込みます
    /// </summary>
    private async Task<List<HistoryEntry>> LoadEntriesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_historyFile))
        {
            return new List<HistoryEntry>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_historyFile, cancellationToken).ConfigureAwait(false);
            var entries = JsonSerializer.Deserialize<List<HistoryEntry>>(json);
            return entries ?? new List<HistoryEntry>();
        }
        catch
        {
            return new List<HistoryEntry>();
        }
    }

    /// <summary>
    /// エントリを保存します
    /// </summary>
    private async Task SaveEntriesAsync(List<HistoryEntry> entries, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var directory = Path.GetDirectoryName(_historyFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_historyFile, json, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // 保存エラーは静かに無視
        }
    }

    /// <summary>
    /// 実行履歴を人間が読みやすい形式で表示します
    /// </summary>
    public static void PrintHistoryEntry(HistoryEntry entry)
    {
        var originalColor = Console.ForegroundColor;

        try
        {
            // ステータスに応じた色
            var statusColor = entry.Status switch
            {
                ExecutionStatus.Success => ConsoleColor.Green,
                ExecutionStatus.Failed => ConsoleColor.Red,
                ExecutionStatus.Cancelled => ConsoleColor.Yellow,
                ExecutionStatus.Skipped => ConsoleColor.Gray,
                _ => ConsoleColor.White
            };

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"━━━ {entry.FlowName} ━━━");
            Console.ResetColor();
            Console.WriteLine($"実行時刻: {entry.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"実行時間: {entry.DurationMs:F2}ms");
            Console.ForegroundColor = statusColor;
            Console.WriteLine($"ステータス: {entry.Status}");
            Console.ResetColor();

            if (entry.Actions.Count > 0)
            {
                Console.WriteLine($"\nアクション数: {entry.Actions.Count}");
                foreach (var action in entry.Actions)
                {
                    var actionColor = action.Status == ExecutionStatus.Success
                        ? ConsoleColor.Green
                        : ConsoleColor.Red;
                    Console.ForegroundColor = actionColor;
                    Console.Write($"  • {action.Type}: ");
                    Console.ResetColor();
                    Console.WriteLine($"{action.Description} ({action.DurationMs:F2}ms)");

                    if (!string.IsNullOrEmpty(action.Error))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"    エラー: {action.Error}");
                        Console.ResetColor();
                    }
                }
            }

            if (entry.Status == ExecutionStatus.Failed && !string.IsNullOrEmpty(entry.ErrorMessage))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nエラー詳細:");
                Console.WriteLine($"  タイプ: {entry.ErrorType}");
                Console.WriteLine($"  メッセージ: {entry.ErrorMessage}");
                Console.ResetColor();
            }

            Console.WriteLine();
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
    }

    /// <summary>
    /// すべての履歴をクリアします
    /// </summary>
    public async Task ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(_historyFile))
            {
                File.Delete(_historyFile);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// リソースを解放します
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _lock?.Dispose();
        _disposed = true;
    }
}
