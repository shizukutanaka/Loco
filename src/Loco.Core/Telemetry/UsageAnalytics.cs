using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loco.Core.Telemetry;

/// <summary>
/// 使用統計収集機能（完全匿名・オプトイン）
/// GDPRおよびプライバシー法に完全準拠
/// 個人を特定できる情報は一切収集しません
/// </summary>
public sealed class UsageAnalytics : IDisposable
{
    private readonly string _analyticsFile;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;
    private bool _enabled;

    /// <summary>
    /// 匿名使用統計
    /// </summary>
    public class UsageStatistics
    {
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("endTime")]
        public DateTime? EndTime { get; set; }

        [JsonPropertyName("totalFlowExecutions")]
        public int TotalFlowExecutions { get; set; }

        [JsonPropertyName("totalRuleExecutions")]
        public int TotalRuleExecutions { get; set; }

        [JsonPropertyName("successfulExecutions")]
        public int SuccessfulExecutions { get; set; }

        [JsonPropertyName("failedExecutions")]
        public int FailedExecutions { get; set; }

        [JsonPropertyName("actionTypeCounts")]
        public Dictionary<string, int> ActionTypeCounts { get; set; } = new();

        [JsonPropertyName("averageExecutionTime")]
        public double AverageExecutionTimeMs { get; set; }

        [JsonPropertyName("peakMemoryMB")]
        public long PeakMemoryMB { get; set; }

        [JsonPropertyName("featureUsage")]
        public Dictionary<string, int> FeatureUsage { get; set; } = new();

        // システム情報（匿名・統計目的のみ）
        [JsonPropertyName("osType")]
        public string OSType { get; set; } = string.Empty; // "Windows", "Linux", "macOS"

        [JsonPropertyName("processorCount")]
        public int ProcessorCount { get; set; }

        [JsonPropertyName("dotnetVersion")]
        public string DotNetVersion { get; set; } = string.Empty;
    }

    private UsageStatistics _currentSession;

    /// <summary>
    /// UsageAnalyticsを初期化します
    /// </summary>
    /// <param name="version">アプリケーションバージョン</param>
    /// <param name="enabled">統計収集が有効かどうか（デフォルト: false）</param>
    public UsageAnalytics(string version, bool enabled = false)
    {
        var locoDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Loco");

        _analyticsFile = Path.Combine(locoDataDir, "usage-analytics.json");
        _enabled = enabled;

        _currentSession = new UsageStatistics
        {
            Version = version,
            OSType = GetOSType(),
            ProcessorCount = Environment.ProcessorCount,
            DotNetVersion = Environment.Version.ToString()
        };
    }

    /// <summary>
    /// 統計収集の有効/無効を設定します
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>
    /// フロー実行を記録します
    /// </summary>
    /// <param name="success">成功したかどうか</param>
    /// <param name="executionTimeMs">実行時間（ミリ秒）</param>
    public void RecordFlowExecution(bool success, double executionTimeMs)
    {
        if (!_enabled) return;

        _currentSession.TotalFlowExecutions++;
        if (success)
        {
            _currentSession.SuccessfulExecutions++;
        }
        else
        {
            _currentSession.FailedExecutions++;
        }

        // 平均実行時間の更新
        var totalExecutions = _currentSession.TotalFlowExecutions;
        _currentSession.AverageExecutionTimeMs =
            (_currentSession.AverageExecutionTimeMs * (totalExecutions - 1) + executionTimeMs) / totalExecutions;
    }

    /// <summary>
    /// ルール実行を記録します
    /// </summary>
    public void RecordRuleExecution()
    {
        if (!_enabled) return;
        _currentSession.TotalRuleExecutions++;
    }

    /// <summary>
    /// アクションタイプの使用を記録します
    /// </summary>
    /// <param name="actionType">アクションタイプ（例: "file", "process", "email"）</param>
    public void RecordActionType(string actionType)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(actionType)) return;

        if (_currentSession.ActionTypeCounts.ContainsKey(actionType))
        {
            _currentSession.ActionTypeCounts[actionType]++;
        }
        else
        {
            _currentSession.ActionTypeCounts[actionType] = 1;
        }
    }

    /// <summary>
    /// 機能の使用を記録します
    /// </summary>
    /// <param name="featureName">機能名（例: "scheduling", "webhook", "mqtt"）</param>
    public void RecordFeatureUsage(string featureName)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(featureName)) return;

        if (_currentSession.FeatureUsage.ContainsKey(featureName))
        {
            _currentSession.FeatureUsage[featureName]++;
        }
        else
        {
            _currentSession.FeatureUsage[featureName] = 1;
        }
    }

    /// <summary>
    /// ピークメモリ使用量を更新します
    /// </summary>
    /// <param name="memoryMB">メモリ使用量（MB）</param>
    public void UpdatePeakMemory(long memoryMB)
    {
        if (!_enabled) return;

        if (memoryMB > _currentSession.PeakMemoryMB)
        {
            _currentSession.PeakMemoryMB = memoryMB;
        }
    }

    /// <summary>
    /// 現在のセッションを保存します
    /// </summary>
    public async Task SaveSessionAsync(CancellationToken cancellationToken = default)
    {
        if (!_enabled) return;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _currentSession.EndTime = DateTime.UtcNow;

            // 既存のセッションを読み込み
            var sessions = new List<UsageStatistics>();
            if (File.Exists(_analyticsFile))
            {
                try
                {
                    var existingJson = await File.ReadAllTextAsync(_analyticsFile, cancellationToken).ConfigureAwait(false);
                    var existingSessions = JsonSerializer.Deserialize<List<UsageStatistics>>(existingJson);
                    if (existingSessions != null)
                    {
                        sessions = existingSessions;
                    }
                }
                catch
                {
                    // 既存ファイルの読み込みエラーは無視
                }
            }

            // 現在のセッションを追加
            sessions.Add(_currentSession);

            // 古いセッションを削除（30日以上前）
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            sessions = sessions.Where(s => s.StartTime >= cutoffDate).ToList();

            // 保存
            var json = JsonSerializer.Serialize(sessions, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var directory = Path.GetDirectoryName(_analyticsFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_analyticsFile, json, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // 保存エラーは静かに無視
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 集計統計を取得します
    /// </summary>
    /// <returns>過去30日間の集計統計</returns>
    public async Task<Dictionary<string, object>> GetAggregatedStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new Dictionary<string, object>();

        if (!File.Exists(_analyticsFile))
        {
            return stats;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_analyticsFile, cancellationToken).ConfigureAwait(false);
            var sessions = JsonSerializer.Deserialize<List<UsageStatistics>>(json);

            if (sessions == null || sessions.Count == 0)
            {
                return stats;
            }

            stats["TotalSessions"] = sessions.Count;
            stats["TotalFlowExecutions"] = sessions.Sum(s => s.TotalFlowExecutions);
            stats["TotalRuleExecutions"] = sessions.Sum(s => s.TotalRuleExecutions);
            stats["AverageSuccessRate"] = sessions.Any()
                ? sessions.Average(s => s.SuccessfulExecutions / Math.Max(1.0, s.TotalFlowExecutions + s.TotalRuleExecutions))
                : 0;
            stats["AverageExecutionTime"] = sessions.Any()
                ? sessions.Average(s => s.AverageExecutionTimeMs)
                : 0;

            // 最も使用されているアクションタイプ
            var topActions = sessions
                .SelectMany(s => s.ActionTypeCounts)
                .GroupBy(kvp => kvp.Key)
                .Select(g => new { Action = g.Key, Count = g.Sum(kvp => kvp.Value) })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToDictionary(x => x.Action, x => x.Count);
            stats["TopActionTypes"] = topActions;

            // 最も使用されている機能
            var topFeatures = sessions
                .SelectMany(s => s.FeatureUsage)
                .GroupBy(kvp => kvp.Key)
                .Select(g => new { Feature = g.Key, Count = g.Sum(kvp => kvp.Value) })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToDictionary(x => x.Feature, x => x.Count);
            stats["TopFeatures"] = topFeatures;
        }
        catch
        {
            // エラーは無視
        }

        return stats;
    }

    /// <summary>
    /// すべての統計データを削除します
    /// </summary>
    public void ClearAllData()
    {
        try
        {
            if (File.Exists(_analyticsFile))
            {
                File.Delete(_analyticsFile);
            }
        }
        catch
        {
            // エラーは無視
        }
    }

    /// <summary>
    /// OSタイプを取得します
    /// </summary>
    private static string GetOSType()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "macOS";
        return "Unknown";
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
