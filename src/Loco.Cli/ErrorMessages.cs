using System;
using System.Collections.Generic;
using System.Text;

namespace Loco.Cli
{
    /// <summary>
    /// Centralized error message management with user-friendly explanations and solutions.
    /// Provides multi-language support for English and Japanese.
    /// </summary>
    public static class ErrorMessages
    {
        private static readonly Dictionary<string, ErrorMessage> Messages = new()
        {
            ["CONFIG_NOT_FOUND"] = new()
            {
                CodeEN = "CONFIG_NOT_FOUND",
                CodeJA = "設定未検出",
                MessageEN = "Configuration file not found",
                MessageJA = "設定ファイルが見つかりません",
                SolutionEN = "Create a configuration file at 'config/loco.config.json' or set LOCO_CONFIG_PATH environment variable",
                SolutionJA = "'config/loco.config.json' に設定ファイルを作成するか、環境変数 LOCO_CONFIG_PATH を設定してください",
                Severity = ErrorSeverity.Warning
            },

            ["PATH_ACCESS_DENIED"] = new()
            {
                CodeEN = "PATH_ACCESS_DENIED",
                CodeJA = "パスアクセス拒否",
                MessageEN = "Access to path is not allowed",
                MessageJA = "パスへのアクセスが許可されていません",
                SolutionEN = "Add the path to 'allowedPaths' in configuration file, or check if path is in 'forbiddenPaths'",
                SolutionJA = "設定ファイルの 'allowedPaths' にパスを追加するか、'forbiddenPaths' に含まれていないか確認してください",
                Severity = ErrorSeverity.Error
            },

            ["MEMORY_LIMIT_EXCEEDED"] = new()
            {
                CodeEN = "MEMORY_LIMIT_EXCEEDED",
                CodeJA = "メモリ制限超過",
                MessageEN = "Memory usage exceeded configured limit",
                MessageJA = "メモリ使用量が設定された制限を超えました",
                SolutionEN = "Increase 'memoryLimitMB' in configuration, or reduce concurrent flow count with 'maxConcurrentFlows'",
                SolutionJA = "設定の 'memoryLimitMB' を増やすか、'maxConcurrentFlows' で同時実行数を減らしてください",
                Severity = ErrorSeverity.Warning
            },

            ["EXECUTION_TIMEOUT"] = new()
            {
                CodeEN = "EXECUTION_TIMEOUT",
                CodeJA = "実行タイムアウト",
                MessageEN = "Operation exceeded timeout limit",
                MessageJA = "操作がタイムアウト制限を超えました",
                SolutionEN = "Increase 'defaultTimeoutSeconds' in configuration for longer operations",
                SolutionJA = "長時間の操作には設定の 'defaultTimeoutSeconds' を増やしてください",
                Severity = ErrorSeverity.Warning
            },

            ["INVALID_WORKFLOW"] = new()
            {
                CodeEN = "INVALID_WORKFLOW",
                CodeJA = "無効なワークフロー",
                MessageEN = "Workflow definition is invalid or corrupted",
                MessageJA = "ワークフロー定義が無効または破損しています",
                SolutionEN = "Validate workflow JSON structure and ensure all required fields are present",
                SolutionJA = "ワークフロー JSON 構造を検証し、すべての必須フィールドが存在することを確認してください",
                Severity = ErrorSeverity.Error
            },

            ["NETWORK_UNREACHABLE"] = new()
            {
                CodeEN = "NETWORK_UNREACHABLE",
                CodeJA = "ネットワーク到達不可",
                MessageEN = "Network endpoint is unreachable",
                MessageJA = "ネットワークエンドポイントに到達できません",
                SolutionEN = "Check network connectivity, firewall rules, and endpoint address",
                SolutionJA = "ネットワーク接続、ファイアウォールルール、エンドポイントアドレスを確認してください",
                Severity = ErrorSeverity.Warning
            },

            ["DISK_SPACE_LOW"] = new()
            {
                CodeEN = "DISK_SPACE_LOW",
                CodeJA = "ディスク容量不足",
                MessageEN = "Available disk space is below recommended threshold",
                MessageJA = "使用可能なディスク容量が推奨しきい値を下回っています",
                SolutionEN = "Free up disk space by removing old logs, temp files, or unused data",
                SolutionJA = "古いログ、一時ファイル、未使用データを削除してディスク容量を確保してください",
                Severity = ErrorSeverity.Warning
            },

            ["PERMISSION_DENIED"] = new()
            {
                CodeEN = "PERMISSION_DENIED",
                CodeJA = "権限拒否",
                MessageEN = "Operation requires elevated permissions",
                MessageJA = "操作には昇格された権限が必要です",
                SolutionEN = "Run the application with administrator privileges or adjust file/directory permissions",
                SolutionJA = "アプリケーションを管理者権限で実行するか、ファイル/ディレクトリの権限を調整してください",
                Severity = ErrorSeverity.Error
            },

            ["RATE_LIMIT_EXCEEDED"] = new()
            {
                CodeEN = "RATE_LIMIT_EXCEEDED",
                CodeJA = "レート制限超過",
                MessageEN = "Too many requests - rate limit exceeded",
                MessageJA = "リクエストが多すぎます - レート制限を超えました",
                SolutionEN = "Wait before retrying, or increase 'rateLimitPerMinute' in configuration",
                SolutionJA = "再試行前に待機するか、設定の 'rateLimitPerMinute' を増やしてください",
                Severity = ErrorSeverity.Warning
            },

            ["ENGINE_NOT_HEALTHY"] = new()
            {
                CodeEN = "ENGINE_NOT_HEALTHY",
                CodeJA = "エンジン不健全",
                MessageEN = "Automation engine health check failed",
                MessageJA = "自動化エンジンのヘルスチェックに失敗しました",
                SolutionEN = "Run 'loco health' for detailed diagnostics and check log files for errors",
                SolutionJA = "'loco health' で詳細な診断を実行し、ログファイルでエラーを確認してください",
                Severity = ErrorSeverity.Critical
            }
        };

        /// <summary>
        /// Gets formatted error message with solution in both English and Japanese.
        /// </summary>
        public static string GetErrorMessage(string errorCode, bool includeJapanese = true, params object[] args)
        {
            if (!Messages.TryGetValue(errorCode, out var error))
            {
                return $"Unknown error: {errorCode}";
            }

            var sb = new StringBuilder();

            // English
            sb.AppendLine($"[{error.Severity.ToString().ToUpper()}] {error.CodeEN}");
            sb.AppendLine($"  Error: {string.Format(error.MessageEN, args)}");
            sb.AppendLine($"  Solution: {error.SolutionEN}");

            // Japanese
            if (includeJapanese)
            {
                sb.AppendLine();
                sb.AppendLine($"[{GetSeverityJA(error.Severity)}] {error.CodeJA}");
                sb.AppendLine($"  エラー: {string.Format(error.MessageJA, args)}");
                sb.AppendLine($"  解決策: {error.SolutionJA}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Displays formatted error message to console with appropriate colors.
        /// </summary>
        public static void DisplayError(string errorCode, params object[] args)
        {
            if (!Messages.TryGetValue(errorCode, out var error))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n✗ Unknown error: {errorCode}");
                Console.ResetColor();
                return;
            }

            // Icon based on severity
            var icon = error.Severity switch
            {
                ErrorSeverity.Critical => "✗",
                ErrorSeverity.Error => "✗",
                ErrorSeverity.Warning => "⚠",
                ErrorSeverity.Info => "ℹ",
                _ => "•"
            };

            // Set color based on severity
            var color = error.Severity switch
            {
                ErrorSeverity.Critical => ConsoleColor.Magenta,
                ErrorSeverity.Error => ConsoleColor.Red,
                ErrorSeverity.Warning => ConsoleColor.Yellow,
                ErrorSeverity.Info => ConsoleColor.Cyan,
                _ => ConsoleColor.White
            };

            Console.WriteLine();
            Console.WriteLine("┌" + new string('─', 60) + "┐");

            // Display English
            Console.ForegroundColor = color;
            Console.Write($"│ {icon} [{error.Severity.ToString().ToUpper()}] {error.CodeEN}");
            Console.ResetColor();
            var padding = 60 - 5 - error.Severity.ToString().Length - error.CodeEN.Length - icon.Length;
            Console.WriteLine(new string(' ', Math.Max(0, padding)) + "│");

            Console.WriteLine("├" + new string('─', 60) + "┤");
            Console.WriteLine($"│ Error: {WrapText(string.Format(error.MessageEN, args), 53)}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"│ Solution: {WrapText(error.SolutionEN, 50)}");
            Console.ResetColor();

            // Display Japanese
            Console.WriteLine("├" + new string('─', 60) + "┤");
            Console.ForegroundColor = color;
            Console.Write($"│ {icon} [{GetSeverityJA(error.Severity)}] {error.CodeJA}");
            Console.ResetColor();
            var paddingJA = 60 - 5 - GetSeverityJA(error.Severity).Length - error.CodeJA.Length - icon.Length - 6; // -6 for Japanese char width
            Console.WriteLine(new string(' ', Math.Max(0, paddingJA)) + "│");

            Console.WriteLine($"│ エラー: {WrapText(string.Format(error.MessageJA, args), 51)}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"│ 解決策: {WrapText(error.SolutionJA, 51)}");
            Console.ResetColor();
            Console.WriteLine("└" + new string('─', 60) + "┘");
            Console.WriteLine();
        }

        private static string WrapText(string text, int maxWidth)
        {
            if (text.Length <= maxWidth)
            {
                return text.PadRight(maxWidth) + "│";
            }

            var lines = new StringBuilder();
            var currentLine = "";
            var words = text.Split(' ');

            foreach (var word in words)
            {
                if ((currentLine + word).Length > maxWidth)
                {
                    lines.AppendLine(currentLine.TrimEnd().PadRight(maxWidth) + "│");
                    lines.Append("│         ");
                    currentLine = word + " ";
                }
                else
                {
                    currentLine += word + " ";
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Append(currentLine.TrimEnd().PadRight(maxWidth) + "│");
            }

            return lines.ToString().TrimEnd('\r', '\n', '│') + "│";
        }

        /// <summary>
        /// Gets success message in both languages.
        /// </summary>
        public static void DisplaySuccess(string messageEN, string messageJA)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n✓ {messageEN}");
            Console.WriteLine($"✓ {messageJA}");
            Console.ResetColor();
            Console.WriteLine();
        }

        /// <summary>
        /// Gets info message in both languages.
        /// </summary>
        public static void DisplayInfo(string messageEN, string messageJA)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\nℹ {messageEN}");
            Console.WriteLine($"ℹ {messageJA}");
            Console.ResetColor();
            Console.WriteLine();
        }

        /// <summary>
        /// Gets warning message in both languages.
        /// </summary>
        public static void DisplayWarning(string messageEN, string messageJA)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n⚠ {messageEN}");
            Console.WriteLine($"⚠ {messageJA}");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static string GetSeverityJA(ErrorSeverity severity)
        {
            return severity switch
            {
                ErrorSeverity.Critical => "重大",
                ErrorSeverity.Error => "エラー",
                ErrorSeverity.Warning => "警告",
                ErrorSeverity.Info => "情報",
                _ => "不明"
            };
        }

        public static IEnumerable<string> GetAllErrorCodes()
        {
            return Messages.Keys;
        }
    }

    public enum ErrorSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    internal class ErrorMessage
    {
        public string CodeEN { get; set; } = string.Empty;
        public string CodeJA { get; set; } = string.Empty;
        public string MessageEN { get; set; } = string.Empty;
        public string MessageJA { get; set; } = string.Empty;
        public string SolutionEN { get; set; } = string.Empty;
        public string SolutionJA { get; set; } = string.Empty;
        public ErrorSeverity Severity { get; set; }
    }
}