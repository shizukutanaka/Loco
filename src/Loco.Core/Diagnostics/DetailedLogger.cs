using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Loco.Core.Diagnostics;

/// <summary>
/// 詳細ロギング機能 - ユーザーが求める「なぜ失敗したか分かる」機能
/// 問題: ユーザーが最も嫌う「エラーの原因が分からない」を解決
/// </summary>
public class DetailedLogger
{
    private readonly string _logDirectory;
    private readonly bool _enableDetailedLogging;
    private readonly List<LogEntry> _sessionLogs = new();
    private const int MaxSessionLogs = 10000;

    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Level { get; set; } = "INFO";
        public string Message { get; set; } = string.Empty;
        public string? Command { get; set; }
        public Dictionary<string, string> Context { get; set; } = new();
        public string? Exception { get; set; }
        public string? StackTrace { get; set; }
        public double DurationMs { get; set; }
        public string? ThreadId { get; set; }
        public string? UserId { get; set; }
    }

    public class ExecutionTrace
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Command { get; set; } = string.Empty;
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public double DurationMs => EndTime.HasValue ? (EndTime.Value - StartTime).TotalMilliseconds : 0;
        public List<TraceStep> Steps { get; set; } = new();
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class TraceStep
    {
        public string Name { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public double DurationMs { get; set; }
        public bool Success { get; set; }
        public string? Output { get; set; }
        public string? Error { get; set; }
        public Dictionary<string, string> Variables { get; set; } = new();
    }

    public DetailedLogger(string? logDirectory = null, bool enableDetailedLogging = true)
    {
        _logDirectory = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Loco", "logs", "detailed");
        _enableDetailedLogging = enableDetailedLogging;

        if (_enableDetailedLogging)
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    /// <summary>
    /// 実行トレースを開始
    /// </summary>
    public ExecutionTrace StartTrace(string command, Dictionary<string, object>? metadata = null)
    {
        var trace = new ExecutionTrace
        {
            Command = command,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        Log("INFO", $"Starting execution trace: {command}", command, new Dictionary<string, string>
        {
            ["TraceId"] = trace.Id,
            ["Command"] = command
        });

        return trace;
    }

    /// <summary>
    /// トレースステップを追加
    /// </summary>
    public void AddTraceStep(ExecutionTrace trace, string stepName, bool success, double durationMs,
        string? output = null, string? error = null, Dictionary<string, string>? variables = null)
    {
        var step = new TraceStep
        {
            Name = stepName,
            DurationMs = durationMs,
            Success = success,
            Output = output,
            Error = error,
            Variables = variables ?? new Dictionary<string, string>()
        };

        trace.Steps.Add(step);

        var level = success ? "INFO" : "ERROR";
        var message = success
            ? $"Step '{stepName}' completed successfully in {durationMs:F2}ms"
            : $"Step '{stepName}' failed: {error}";

        Log(level, message, trace.Command, new Dictionary<string, string>
        {
            ["TraceId"] = trace.Id,
            ["Step"] = stepName,
            ["Success"] = success.ToString(),
            ["DurationMs"] = durationMs.ToString("F2")
        });
    }

    /// <summary>
    /// 実行トレースを終了
    /// </summary>
    public void EndTrace(ExecutionTrace trace, bool success, string? errorMessage = null)
    {
        trace.EndTime = DateTime.UtcNow;
        trace.Success = success;
        trace.ErrorMessage = errorMessage;

        var level = success ? "INFO" : "ERROR";
        var message = success
            ? $"Execution trace completed successfully in {trace.DurationMs:F2}ms"
            : $"Execution trace failed: {errorMessage}";

        Log(level, message, trace.Command, new Dictionary<string, string>
        {
            ["TraceId"] = trace.Id,
            ["Success"] = success.ToString(),
            ["DurationMs"] = trace.DurationMs.ToString("F2"),
            ["Steps"] = trace.Steps.Count.ToString()
        });

        // トレースをファイルに保存
        if (_enableDetailedLogging)
        {
            SaveTrace(trace);
        }
    }

    /// <summary>
    /// 詳細ログを記録
    /// </summary>
    public void Log(string level, string message, string? command = null,
        Dictionary<string, string>? context = null, Exception? exception = null)
    {
        var entry = new LogEntry
        {
            Level = level,
            Message = message,
            Command = command,
            Context = context ?? new Dictionary<string, string>(),
            ThreadId = Environment.CurrentManagedThreadId.ToString(),
            UserId = Environment.UserName
        };

        if (exception != null)
        {
            entry.Exception = exception.Message;
            entry.StackTrace = exception.StackTrace;
        }

        _sessionLogs.Add(entry);

        // セッションログのサイズ制限
        if (_sessionLogs.Count > MaxSessionLogs)
        {
            _sessionLogs.RemoveAt(0);
        }

        // ファイルにも書き込み
        if (_enableDetailedLogging)
        {
            WriteToFile(entry);
        }
    }

    /// <summary>
    /// トレースを保存
    /// </summary>
    private void SaveTrace(ExecutionTrace trace)
    {
        try
        {
            var fileName = $"trace-{trace.StartTime:yyyyMMdd-HHmmss}-{trace.Id}.json";
            var filePath = Path.Combine(_logDirectory, fileName);

            var json = JsonSerializer.Serialize(trace, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(filePath, json);
        }
        catch
        {
            // ログ書き込みエラーは無視（アプリケーション動作に影響させない）
        }
    }

    /// <summary>
    /// ファイルに書き込み
    /// </summary>
    private void WriteToFile(LogEntry entry)
    {
        try
        {
            var fileName = $"detailed-{DateTime.UtcNow:yyyyMMdd}.log";
            var filePath = Path.Combine(_logDirectory, fileName);

            var logLine = FormatLogEntry(entry);
            File.AppendAllText(filePath, logLine + Environment.NewLine);
        }
        catch
        {
            // ログ書き込みエラーは無視
        }
    }

    /// <summary>
    /// ログエントリをフォーマット
    /// </summary>
    private string FormatLogEntry(LogEntry entry)
    {
        var sb = new StringBuilder();
        sb.Append($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] ");
        sb.Append($"[{entry.Level}] ");

        if (!string.IsNullOrEmpty(entry.Command))
        {
            sb.Append($"[{entry.Command}] ");
        }

        sb.Append(entry.Message);

        if (entry.Context.Count > 0)
        {
            sb.Append(" | ");
            sb.Append(string.Join(", ", entry.Context.Select(kv => $"{kv.Key}={kv.Value}")));
        }

        if (!string.IsNullOrEmpty(entry.Exception))
        {
            sb.Append($" | Exception: {entry.Exception}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// セッションログを取得
    /// </summary>
    public List<LogEntry> GetSessionLogs(string? level = null, string? command = null, int? last = null)
    {
        var logs = _sessionLogs.AsEnumerable();

        if (!string.IsNullOrEmpty(level))
        {
            logs = logs.Where(l => l.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(command))
        {
            logs = logs.Where(l => l.Command?.Equals(command, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (last.HasValue)
        {
            logs = logs.TakeLast(last.Value);
        }

        return logs.ToList();
    }

    /// <summary>
    /// トレースを検索
    /// </summary>
    public List<ExecutionTrace> SearchTraces(DateTime? startTime = null, DateTime? endTime = null,
        bool? successOnly = null, string? command = null)
    {
        var traces = new List<ExecutionTrace>();

        if (!_enableDetailedLogging || !Directory.Exists(_logDirectory))
        {
            return traces;
        }

        var traceFiles = Directory.GetFiles(_logDirectory, "trace-*.json");

        foreach (var file in traceFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var trace = JsonSerializer.Deserialize<ExecutionTrace>(json);

                if (trace == null) continue;

                // フィルタ適用
                if (startTime.HasValue && trace.StartTime < startTime.Value) continue;
                if (endTime.HasValue && trace.StartTime > endTime.Value) continue;
                if (successOnly.HasValue && trace.Success != successOnly.Value) continue;
                if (!string.IsNullOrEmpty(command) && !trace.Command.Equals(command, StringComparison.OrdinalIgnoreCase)) continue;

                traces.Add(trace);
            }
            catch
            {
                // 破損したファイルはスキップ
            }
        }

        return traces.OrderByDescending(t => t.StartTime).ToList();
    }

    /// <summary>
    /// デバッグレポートを生成
    /// </summary>
    public string GenerateDebugReport(string? command = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("  DEBUG REPORT / デバッグレポート");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // エラーログ
        var errorLogs = GetSessionLogs(level: "ERROR", command: command);
        if (errorLogs.Any())
        {
            sb.AppendLine("🔴 ERROR LOGS / エラーログ");
            sb.AppendLine();

            foreach (var log in errorLogs.TakeLast(10))
            {
                sb.AppendLine($"[{log.Timestamp:HH:mm:ss}] {log.Message}");
                if (!string.IsNullOrEmpty(log.Exception))
                {
                    sb.AppendLine($"   Exception: {log.Exception}");
                }
                if (log.Context.Any())
                {
                    sb.AppendLine($"   Context: {string.Join(", ", log.Context.Select(kv => $"{kv.Key}={kv.Value}"))}");
                }
                sb.AppendLine();
            }
        }

        // 警告ログ
        var warningLogs = GetSessionLogs(level: "WARN", command: command);
        if (warningLogs.Any())
        {
            sb.AppendLine("⚠️  WARNING LOGS / 警告ログ");
            sb.AppendLine();

            foreach (var log in warningLogs.TakeLast(10))
            {
                sb.AppendLine($"[{log.Timestamp:HH:mm:ss}] {log.Message}");
                sb.AppendLine();
            }
        }

        // 最近のトレース
        var recentTraces = SearchTraces(
            startTime: DateTime.UtcNow.AddHours(-1),
            successOnly: false,
            command: command
        );

        if (recentTraces.Any())
        {
            sb.AppendLine("📊 RECENT EXECUTION TRACES / 最近の実行トレース");
            sb.AppendLine();

            foreach (var trace in recentTraces.Take(5))
            {
                var status = trace.Success ? "✓ SUCCESS" : "✗ FAILED";
                sb.AppendLine($"{status} | {trace.Command} | {trace.DurationMs:F0}ms | {trace.Steps.Count} steps");

                if (!trace.Success)
                {
                    sb.AppendLine($"   Error: {trace.ErrorMessage}");

                    // 失敗したステップを表示
                    var failedSteps = trace.Steps.Where(s => !s.Success).ToList();
                    foreach (var step in failedSteps)
                    {
                        sb.AppendLine($"   ✗ Step '{step.Name}' failed: {step.Error}");
                    }
                }

                sb.AppendLine();
            }
        }

        // 統計情報
        var totalLogs = _sessionLogs.Count;
        var errorCount = _sessionLogs.Count(l => l.Level == "ERROR");
        var warnCount = _sessionLogs.Count(l => l.Level == "WARN");

        sb.AppendLine("📈 STATISTICS / 統計情報");
        sb.AppendLine();
        sb.AppendLine($"Total logs: {totalLogs}");
        sb.AppendLine($"Errors: {errorCount} ({(totalLogs > 0 ? (double)errorCount / totalLogs : 0):P1})");
        sb.AppendLine($"Warnings: {warnCount} ({(totalLogs > 0 ? (double)warnCount / totalLogs : 0):P1})");
        sb.AppendLine();

        sb.AppendLine("═══════════════════════════════════════════════════════════");

        return sb.ToString();
    }

    /// <summary>
    /// トレース詳細を表示
    /// </summary>
    public string FormatTraceDetails(ExecutionTrace trace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine($"  EXECUTION TRACE: {trace.Command}");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();

        sb.AppendLine($"Trace ID: {trace.Id}");
        sb.AppendLine($"Command: {trace.Command}");
        sb.AppendLine($"Started: {trace.StartTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Duration: {trace.DurationMs:F2}ms");
        sb.AppendLine($"Status: {(trace.Success ? "✓ SUCCESS" : "✗ FAILED")}");

        if (!trace.Success)
        {
            sb.AppendLine($"Error: {trace.ErrorMessage}");
        }

        sb.AppendLine();
        sb.AppendLine($"STEPS ({trace.Steps.Count}):");
        sb.AppendLine();

        for (int i = 0; i < trace.Steps.Count; i++)
        {
            var step = trace.Steps[i];
            var status = step.Success ? "✓" : "✗";

            sb.AppendLine($"{i + 1}. {status} {step.Name} ({step.DurationMs:F2}ms)");

            if (!string.IsNullOrEmpty(step.Output))
            {
                sb.AppendLine($"   Output: {step.Output}");
            }

            if (!step.Success && !string.IsNullOrEmpty(step.Error))
            {
                sb.AppendLine($"   Error: {step.Error}");
            }

            if (step.Variables.Any())
            {
                sb.AppendLine($"   Variables: {string.Join(", ", step.Variables.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("═══════════════════════════════════════════════════════════");

        return sb.ToString();
    }
}
