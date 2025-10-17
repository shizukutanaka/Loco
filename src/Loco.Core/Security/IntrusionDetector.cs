using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Security;

/// <summary>
/// 侵入検知および異常検知システム
/// </summary>
public class IntrusionDetector
{
    private readonly string _logPath;
    private readonly List<SecurityEvent> _events;
    private readonly Dictionary<string, int> _eventCounts;
    private readonly Dictionary<string, DateTime> _lastEventTimes;
    private readonly object _lock = new();

    // 検知ルール
    private readonly List<DetectionRule> _rules;

    public event EventHandler<SecurityAlertEventArgs>? SecurityAlert;

    public IntrusionDetector(string logPath)
    {
        _logPath = logPath;
        _events = new List<SecurityEvent>();
        _eventCounts = new Dictionary<string, int>();
        _lastEventTimes = new Dictionary<string, DateTime>();
        _rules = InitializeRules();

        LoadExistingEvents();
        StartMonitoring();
    }

    private List<DetectionRule> InitializeRules()
    {
        return new List<DetectionRule>
        {
            // ブルートフォース攻撃検知
            new DetectionRule
            {
                Name = "BruteForceLogin",
                Description = "連続したログイン失敗を検知",
                EventType = "authentication_failure",
                Threshold = 5,
                TimeWindow = TimeSpan.FromMinutes(10),
                Severity = AlertSeverity.Medium
            },

            // 異常なファイルアクセス
            new DetectionRule
            {
                Name = "SuspiciousFileAccess",
                Description = "システムファイルへの異常アクセスを検知",
                EventType = "file_access",
                Threshold = 3,
                TimeWindow = TimeSpan.FromMinutes(5),
                Severity = AlertSeverity.Low,
                CustomCondition = (events) => events.Any(e =>
                    e.Details.Contains("system32") ||
                    e.Details.Contains("windows\\system") ||
                    e.Details.Contains("boot.ini"))
            },

            // 大量のネットワーク接続
            new DetectionRule
            {
                Name = "HighNetworkActivity",
                Description = "異常に高いネットワーク活動を検知",
                EventType = "network_connection",
                Threshold = 100,
                TimeWindow = TimeSpan.FromMinutes(1),
                Severity = AlertSeverity.Medium
            },

            // 権限昇格の試行
            new DetectionRule
            {
                Name = "PrivilegeEscalation",
                Description = "権限昇格の試行を検知",
                EventType = "privilege_change",
                Threshold = 1,
                TimeWindow = TimeSpan.FromHours(1),
                Severity = AlertSeverity.High
            },

            // 機密情報のアクセス
            new DetectionRule
            {
                Name = "SensitiveDataAccess",
                Description = "機密情報へのアクセスを検知",
                EventType = "data_access",
                Threshold = 1,
                TimeWindow = TimeSpan.FromHours(24),
                Severity = AlertSeverity.High,
                CustomCondition = (events) => events.Any(e =>
                    e.Details.Contains("password") ||
                    e.Details.Contains("api_key") ||
                    e.Details.Contains("secret"))
            },

            // 異常なプロセス実行
            new DetectionRule
            {
                Name = "SuspiciousProcess",
                Description = "疑わしいプロセスの実行を検知",
                EventType = "process_start",
                Threshold = 1,
                TimeWindow = TimeSpan.FromMinutes(30),
                Severity = AlertSeverity.Medium,
                CustomCondition = (events) => events.Any(e =>
                    e.Details.Contains("cmd.exe") ||
                    e.Details.Contains("powershell.exe") ||
                    e.Details.Contains("net.exe"))
            }
        };
    }

    /// <summary>
    /// セキュリティイベントを記録
    /// </summary>
    public void LogEvent(string eventType, string source, string details, Dictionary<string, object>? metadata = null)
    {
        var securityEvent = new SecurityEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            Source = source,
            Details = details,
            Metadata = metadata ?? new Dictionary<string, object>(),
            Hash = GenerateEventHash(eventType, source, details)
        };

        lock (_lock)
        {
            _events.Add(securityEvent);

            // イベントカウントを更新
            var key = $"{eventType}:{source}";
            _eventCounts[key] = _eventCounts.GetValueOrDefault(key, 0) + 1;
            _lastEventTimes[key] = securityEvent.Timestamp;

            // イベントをファイルに保存
            SaveEvent(securityEvent);

            // ルールチェック
            CheckRules(securityEvent);
        }
    }

    /// <summary>
    /// セキュリティアラートを生成
    /// </summary>
    public void GenerateAlert(string ruleName, string description, AlertSeverity severity, IEnumerable<SecurityEvent> relatedEvents)
    {
        var alert = new SecurityAlert
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            RuleName = ruleName,
            Description = description,
            Severity = severity,
            RelatedEvents = relatedEvents.ToList(),
            Status = AlertStatus.New
        };

        // アラートをログに記録
        LogEvent("security_alert", "IntrusionDetector", $"Alert generated: {ruleName} - {description}", new Dictionary<string, object>
        {
            ["alert_id"] = alert.Id,
            ["severity"] = severity.ToString(),
            ["rule_name"] = ruleName
        });

        // アラートイベントを発火
        SecurityAlert?.Invoke(this, new SecurityAlertEventArgs(alert));
    }

    /// <summary>
    /// 指定期間のイベントを取得
    /// </summary>
    public IEnumerable<SecurityEvent> GetEvents(DateTime startTime, DateTime endTime)
    {
        lock (_lock)
        {
            return _events.Where(e => e.Timestamp >= startTime && e.Timestamp <= endTime).ToList();
        }
    }

    /// <summary>
    /// イベントタイプでフィルタリング
    /// </summary>
    public IEnumerable<SecurityEvent> GetEventsByType(string eventType)
    {
        lock (_lock)
        {
            return _events.Where(e => e.EventType == eventType).ToList();
        }
    }

    /// <summary>
    /// セキュリティ統計を取得
    /// </summary>
    public SecurityStatistics GetStatistics(TimeSpan timeWindow)
    {
        var cutoffTime = DateTime.UtcNow - timeWindow;

        lock (_lock)
        {
            var relevantEvents = _events.Where(e => e.Timestamp >= cutoffTime).ToList();

            return new SecurityStatistics
            {
                TimeWindow = timeWindow,
                TotalEvents = relevantEvents.Count,
                EventsByType = relevantEvents
                    .GroupBy(e => e.EventType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                TopSources = relevantEvents
                    .GroupBy(e => e.Source)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .ToDictionary(g => g.Key, g => g.Count()),
                AlertsGenerated = relevantEvents
                    .Count(e => e.EventType == "security_alert")
            };
        }
    }

    private void CheckRules(SecurityEvent securityEvent)
    {
        foreach (var rule in _rules)
        {
            if (rule.EventType != securityEvent.EventType) continue;

            var timeWindow = securityEvent.Timestamp - rule.TimeWindow;
            var recentEvents = _events.Where(e =>
                e.EventType == rule.EventType &&
                e.Timestamp >= timeWindow &&
                e.Source == securityEvent.Source).ToList();

            // カスタム条件チェック
            if (rule.CustomCondition != null && !rule.CustomCondition(recentEvents))
                continue;

            // 閾値チェック
            if (recentEvents.Count >= rule.Threshold)
            {
                GenerateAlert(
                    rule.Name,
                    rule.Description,
                    rule.Severity,
                    recentEvents
                );
            }
        }
    }

    private string GenerateEventHash(string eventType, string source, string details)
    {
        using var sha256 = SHA256.Create();
        var input = $"{eventType}:{source}:{details}:{DateTime.UtcNow.Ticks}";
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private void SaveEvent(SecurityEvent securityEvent)
    {
        try
        {
            var logDirectory = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            var logEntry = JsonSerializer.Serialize(securityEvent, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.AppendAllText(_logPath, logEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            // ログ保存の失敗は無視（ただしこれはセキュリティリスク）
            Console.Error.WriteLine($"Failed to save security event: {ex.Message}");
        }
    }

    private void LoadExistingEvents()
    {
        if (!File.Exists(_logPath)) return;

        try
        {
            var lines = File.ReadAllLines(_logPath);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var securityEvent = JsonSerializer.Deserialize<SecurityEvent>(line);
                    if (securityEvent != null)
                    {
                        _events.Add(securityEvent);
                    }
                }
                catch
                {
                    // 無効な行はスキップ
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load security events: {ex.Message}");
        }
    }

    private void StartMonitoring()
    {
        // 定期的なクリーンアップタスクを開始
        Task.Run(() => CleanupOldEvents());
    }

    private async Task CleanupOldEvents()
    {
        while (true)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow - TimeSpan.FromDays(30); // 30日以上前のイベントを削除

                lock (_lock)
                {
                    _events.RemoveAll(e => e.Timestamp < cutoffTime);
                }

                // ログファイルをローテーション
                RotateLogFile();
            }
            catch (Exception ex)
            {
                LogEvent("system_error", "IntrusionDetector", $"Cleanup failed: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromHours(24)); // 24時間ごとに実行
        }
    }

    private void RotateLogFile()
    {
        try
        {
            var fileInfo = new FileInfo(_logPath);
            if (fileInfo.Length > 10 * 1024 * 1024) // 10MBを超えたらローテーション
            {
                var backupPath = $"{_logPath}.{DateTime.UtcNow:yyyyMMdd-HHmmss}.bak";
                File.Move(_logPath, backupPath);
            }
        }
        catch
        {
            // ローテーション失敗は無視
        }
    }
}

/// <summary>
/// セキュリティイベント
/// </summary>
public class SecurityEvent
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = "";
    public string Source { get; set; } = "";
    public string Details { get; set; } = "";
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string Hash { get; set; } = "";
}

/// <summary>
/// 検知ルール
/// </summary>
public class DetectionRule
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string EventType { get; set; } = "";
    public int Threshold { get; set; }
    public TimeSpan TimeWindow { get; set; }
    public AlertSeverity Severity { get; set; }
    public Func<List<SecurityEvent>, bool>? CustomCondition { get; set; }
}

/// <summary>
/// セキュリティアラート
/// </summary>
public class SecurityAlert
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string RuleName { get; set; } = "";
    public string Description { get; set; } = "";
    public AlertSeverity Severity { get; set; }
    public List<SecurityEvent> RelatedEvents { get; set; } = new();
    public AlertStatus Status { get; set; }
}

/// <summary>
/// アラート深刻度
/// </summary>
public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// アラートステータス
/// </summary>
public enum AlertStatus
{
    New,
    Investigating,
    Resolved,
    FalsePositive
}

/// <summary>
/// セキュリティ統計
/// </summary>
public class SecurityStatistics
{
    public TimeSpan TimeWindow { get; set; }
    public int TotalEvents { get; set; }
    public Dictionary<string, int> EventsByType { get; set; } = new();
    public Dictionary<string, int> TopSources { get; set; } = new();
    public int AlertsGenerated { get; set; }
}

/// <summary>
/// セキュリティアラートイベント引数
/// </summary>
public class SecurityAlertEventArgs : EventArgs
{
    public SecurityAlert Alert { get; }

    public SecurityAlertEventArgs(SecurityAlert alert)
    {
        Alert = alert;
    }
}
