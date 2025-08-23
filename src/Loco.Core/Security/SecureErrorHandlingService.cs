using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Security
{
    /// <summary>
    /// セキュアエラーハンドリングサービス - P0項目#7
    /// 情報漏洩防止、スタックトレース除去、エラー分析機能
    /// </summary>
    public class SecureErrorHandlingService
    {
        private readonly ILogger<SecureErrorHandlingService> _logger;
        private readonly SecurityAuditLogger _auditLogger;
        private readonly ErrorHandlingConfiguration _config;

        // 機密情報検出パターン
        private static readonly Dictionary<string, Regex> SensitivePatterns = new()
        {
            { "StackTrace", new Regex(@"at\s+[\w\.\<\>]+\([^)]*\)\s+in\s+[^\r\n]+", RegexOptions.Compiled | RegexOptions.IgnoreCase) },
            { "FilePath", new Regex(@"[A-Za-z]:\\[^\\/:*?""<>|\r\n]*", RegexOptions.Compiled) },
            { "DatabaseConnection", new Regex(@"(server|data source|initial catalog|user id|password|pwd)\s*=\s*[^;]+", RegexOptions.Compiled | RegexOptions.IgnoreCase) },
            { "ApiKey", new Regex(@"(api[_-]?key|token|secret)[""']?\s*[:=]\s*[""']?[\w\-\.]+", RegexOptions.Compiled | RegexOptions.IgnoreCase) },
            { "SqlQuery", new Regex(@"\b(SELECT|INSERT|UPDATE|DELETE|CREATE|DROP|ALTER|EXEC)\b[\s\S]*?\b(FROM|INTO|SET|WHERE|VALUES)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase) },
            { "IpAddress", new Regex(@"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b", RegexOptions.Compiled) },
            { "Email", new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled) },
            { "CreditCard", new Regex(@"\b(?:\d{4}[-\s]?){3}\d{4}\b", RegexOptions.Compiled) },
            { "PhoneNumber", new Regex(@"(\+\d{1,3}[-.\s]?)?\(?\d{1,4}\)?[-.\s]?\d{1,4}[-.\s]?\d{1,9}", RegexOptions.Compiled) }
        };

        // 汎用エラーメッセージマップ
        private static readonly Dictionary<string, string> GenericErrorMessages = new()
        {
            { "SqlException", "データベース処理でエラーが発生しました" },
            { "UnauthorizedAccessException", "アクセス権限がありません" },
            { "FileNotFoundException", "要求されたリソースが見つかりません" },
            { "ArgumentException", "入力データが無効です" },
            { "InvalidOperationException", "現在の状態では操作を実行できません" },
            { "NotSupportedException", "サポートされていない操作です" },
            { "TimeoutException", "処理がタイムアウトしました" },
            { "OutOfMemoryException", "システムリソースが不足しています" },
            { "NetworkException", "ネットワーク接続エラーが発生しました" },
            { "SecurityException", "セキュリティ制限により処理できません" }
        };

        public SecureErrorHandlingService(
            ILogger<SecureErrorHandlingService> logger = null,
            SecurityAuditLogger auditLogger = null,
            ErrorHandlingConfiguration config = null)
        {
            _logger = logger;
            _auditLogger = auditLogger;
            _config = config ?? new ErrorHandlingConfiguration();
        }

        /// <summary>
        /// セキュアなエラーレスポンスを生成
        /// </summary>
        public SecureErrorResponse ProcessError(
            Exception exception,
            string userId = null,
            string requestId = null,
            Dictionary<string, object> context = null)
        {
            var errorId = GenerateErrorId();
            var response = new SecureErrorResponse
            {
                ErrorId = errorId,
                RequestId = requestId,
                OccurredAt = DateTime.UtcNow,
                Context = SanitizeContext(context)
            };

            try
            {
                // エラー分析とカテゴリ化
                var analysis = AnalyzeException(exception);
                response.Category = analysis.Category;
                response.Severity = analysis.Severity;

                // ユーザー向けの安全なメッセージを生成
                response.UserMessage = GenerateSafeUserMessage(exception, analysis);

                // 開発者向けの詳細情報（設定に応じて）
                if (_config.IncludeDeveloperDetails)
                {
                    response.DeveloperMessage = GenerateDeveloperMessage(exception, analysis);
                }

                // 内部ログ記録（完全な詳細）
                LogInternalError(exception, errorId, userId, context);

                // セキュリティ監査ログ
                if (analysis.IsSecurityRelated)
                {
                    LogSecurityEvent(exception, errorId, userId, analysis);
                }

                // エラー統計更新
                UpdateErrorStatistics(analysis.Category, analysis.Severity);

                return response;
            }
            catch (Exception processingError)
            {
                // エラー処理中のエラーに対する最後の砦
                _logger?.LogCritical(processingError, "Critical error in error processing for ErrorId: {ErrorId}", errorId);
                
                return new SecureErrorResponse
                {
                    ErrorId = errorId,
                    RequestId = requestId,
                    OccurredAt = DateTime.UtcNow,
                    Category = ErrorCategory.System,
                    Severity = ErrorSeverity.Critical,
                    UserMessage = "システムエラーが発生しました。サポートにお問い合わせください。",
                    Context = new Dictionary<string, object> { { "processedSafely", true } }
                };
            }
        }

        /// <summary>
        /// 機密情報の除去とサニタイズ
        /// </summary>
        public string SanitizeErrorMessage(string message, SanitizationLevel level = SanitizationLevel.Standard)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            var sanitized = message;

            try
            {
                // レベルに応じたサニタイズ処理
                switch (level)
                {
                    case SanitizationLevel.Minimal:
                        sanitized = RemoveStackTraces(sanitized);
                        break;
                    case SanitizationLevel.Standard:
                        sanitized = ApplyStandardSanitization(sanitized);
                        break;
                    case SanitizationLevel.Aggressive:
                        sanitized = ApplyAggressiveSanitization(sanitized);
                        break;
                }

                // 長さ制限
                if (sanitized.Length > _config.MaxMessageLength)
                {
                    sanitized = sanitized.Substring(0, _config.MaxMessageLength) + "...";
                }

                return sanitized;
            }
            catch
            {
                return "エラーメッセージの処理中にエラーが発生しました";
            }
        }

        /// <summary>
        /// エラーパターン分析
        /// </summary>
        public ErrorAnalysis AnalyzeException(Exception exception)
        {
            var analysis = new ErrorAnalysis
            {
                ExceptionType = exception.GetType().Name,
                Message = exception.Message,
                AnalyzedAt = DateTime.UtcNow
            };

            try
            {
                // カテゴリ分類
                analysis.Category = ClassifyErrorCategory(exception);

                // 重要度判定
                analysis.Severity = DetermineSeverity(exception);

                // セキュリティ関連かチェック
                analysis.IsSecurityRelated = IsSecurityRelated(exception);

                // 機密情報含有チェック
                analysis.ContainsSensitiveData = ContainsSensitiveInformation(exception.ToString());

                // 復旧可能性判定
                analysis.IsRecoverable = IsRecoverable(exception);

                // 根本原因分析
                analysis.RootCause = AnalyzeRootCause(exception);

                // 推奨対応
                analysis.RecommendedActions = GenerateRecommendations(exception, analysis);

                return analysis;
            }
            catch (Exception analysisError)
            {
                _logger?.LogError(analysisError, "Error analysis failed for exception type: {ExceptionType}", exception.GetType().Name);
                
                return new ErrorAnalysis
                {
                    ExceptionType = exception.GetType().Name,
                    Category = ErrorCategory.Unknown,
                    Severity = ErrorSeverity.Medium,
                    IsSecurityRelated = false,
                    IsRecoverable = false,
                    AnalyzedAt = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// エラー統計の取得
        /// </summary>
        public ErrorStatistics GetErrorStatistics(TimeSpan? period = null)
        {
            var actualPeriod = period ?? TimeSpan.FromHours(24);
            var cutoff = DateTime.UtcNow.Subtract(actualPeriod);

            // 実装依存の統計データ取得
            return GetErrorStatisticsFromStorage(cutoff);
        }

        // プライベートメソッド

        private string GenerateErrorId()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[8];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "");
        }

        private string GenerateSafeUserMessage(Exception exception, ErrorAnalysis analysis)
        {
            // 例外タイプに基づく汎用メッセージ
            var exceptionTypeName = exception.GetType().Name;
            if (GenericErrorMessages.TryGetValue(exceptionTypeName, out var genericMessage))
            {
                return genericMessage;
            }

            // カテゴリに基づくメッセージ
            return analysis.Category switch
            {
                ErrorCategory.Authentication => "認証に失敗しました。ログイン情報をご確認ください。",
                ErrorCategory.Authorization => "この操作を実行する権限がありません。",
                ErrorCategory.Validation => "入力データに問題があります。内容をご確認ください。",
                ErrorCategory.Database => "データ処理中にエラーが発生しました。しばらく時間をおいて再試行してください。",
                ErrorCategory.Network => "ネットワーク接続に問題があります。接続状況をご確認ください。",
                ErrorCategory.FileSystem => "ファイル処理中にエラーが発生しました。",
                ErrorCategory.Configuration => "システム設定に問題があります。管理者にお問い合わせください。",
                ErrorCategory.External => "外部サービスとの通信でエラーが発生しました。",
                _ => "予期しないエラーが発生しました。サポートにお問い合わせください。"
            };
        }

        private string GenerateDeveloperMessage(Exception exception, ErrorAnalysis analysis)
        {
            if (!_config.IncludeDeveloperDetails)
                return null;

            var details = new Dictionary<string, object>
            {
                { "exceptionType", analysis.ExceptionType },
                { "category", analysis.Category.ToString() },
                { "severity", analysis.Severity.ToString() },
                { "isRecoverable", analysis.IsRecoverable },
                { "rootCause", analysis.RootCause },
                { "recommendations", analysis.RecommendedActions }
            };

            // 機密情報を除去したスタックトレース
            if (_config.IncludeStackTrace && !analysis.ContainsSensitiveData)
            {
                details["stackTrace"] = SanitizeErrorMessage(exception.StackTrace, SanitizationLevel.Standard);
            }

            return JsonSerializer.Serialize(details, new JsonSerializerOptions { WriteIndented = true });
        }

        private void LogInternalError(Exception exception, string errorId, string userId, Dictionary<string, object> context)
        {
            var sanitizedMessage = SanitizeErrorMessage(exception.Message, SanitizationLevel.Minimal);
            var sanitizedStackTrace = SanitizeErrorMessage(exception.StackTrace, SanitizationLevel.Standard);

            _logger?.LogError(exception, 
                "Internal Error | ErrorId: {ErrorId} | UserId: {UserId} | Type: {ExceptionType} | Message: {Message}",
                errorId, userId, exception.GetType().Name, sanitizedMessage);
        }

        private void LogSecurityEvent(Exception exception, string errorId, string userId, ErrorAnalysis analysis)
        {
            _auditLogger?.LogSecurityEventAsync(
                SecurityEventType.Error,
                "SecurityRelatedError",
                userId,
                new Dictionary<string, object>
                {
                    { "errorId", errorId },
                    { "exceptionType", analysis.ExceptionType },
                    { "category", analysis.Category.ToString() },
                    { "severity", analysis.Severity.ToString() }
                }
            );
        }

        private ErrorCategory ClassifyErrorCategory(Exception exception)
        {
            var exceptionType = exception.GetType();
            var message = exception.Message?.ToLower() ?? string.Empty;

            // 型ベースの分類
            if (exceptionType.Name.Contains("Sql") || exceptionType.Name.Contains("Database"))
                return ErrorCategory.Database;
            if (exceptionType.Name.Contains("Unauthorized") || exceptionType.Name.Contains("Authentication"))
                return ErrorCategory.Authentication;
            if (exceptionType.Name.Contains("Security") || exceptionType.Name.Contains("Authorization"))
                return ErrorCategory.Authorization;
            if (exceptionType.Name.Contains("Argument") || exceptionType.Name.Contains("Validation"))
                return ErrorCategory.Validation;
            if (exceptionType.Name.Contains("Network") || exceptionType.Name.Contains("Http"))
                return ErrorCategory.Network;
            if (exceptionType.Name.Contains("File") || exceptionType.Name.Contains("IO"))
                return ErrorCategory.FileSystem;
            if (exceptionType.Name.Contains("Configuration") || exceptionType.Name.Contains("Config"))
                return ErrorCategory.Configuration;

            // メッセージベースの分類
            if (message.Contains("timeout") || message.Contains("connection"))
                return ErrorCategory.Network;
            if (message.Contains("permission") || message.Contains("access"))
                return ErrorCategory.Authorization;
            if (message.Contains("invalid") || message.Contains("validation"))
                return ErrorCategory.Validation;

            return ErrorCategory.System;
        }

        private ErrorSeverity DetermineSeverity(Exception exception)
        {
            var exceptionType = exception.GetType();

            // 重要度の高い例外
            if (exceptionType.Name.Contains("OutOfMemory") || 
                exceptionType.Name.Contains("StackOverflow") ||
                exceptionType.Name.Contains("AccessViolation"))
                return ErrorSeverity.Critical;

            if (exceptionType.Name.Contains("Security") ||
                exceptionType.Name.Contains("Unauthorized") ||
                exceptionType.Name.Contains("Sql"))
                return ErrorSeverity.High;

            if (exceptionType.Name.Contains("Timeout") ||
                exceptionType.Name.Contains("Network") ||
                exceptionType.Name.Contains("IO"))
                return ErrorSeverity.Medium;

            return ErrorSeverity.Low;
        }

        private bool IsSecurityRelated(Exception exception)
        {
            var exceptionType = exception.GetType().Name.ToLower();
            var message = exception.Message?.ToLower() ?? string.Empty;

            return exceptionType.Contains("security") ||
                   exceptionType.Contains("unauthorized") ||
                   exceptionType.Contains("authentication") ||
                   exceptionType.Contains("authorization") ||
                   message.Contains("injection") ||
                   message.Contains("xss") ||
                   message.Contains("csrf");
        }

        private bool ContainsSensitiveInformation(string content)
        {
            if (string.IsNullOrEmpty(content))
                return false;

            return SensitivePatterns.Values.Any(pattern => pattern.IsMatch(content));
        }

        private bool IsRecoverable(Exception exception)
        {
            var exceptionType = exception.GetType();

            // 回復不可能な例外
            if (exceptionType.Name.Contains("OutOfMemory") ||
                exceptionType.Name.Contains("StackOverflow") ||
                exceptionType.Name.Contains("AccessViolation"))
                return false;

            // 回復可能な例外
            if (exceptionType.Name.Contains("Timeout") ||
                exceptionType.Name.Contains("Network") ||
                exceptionType.Name.Contains("Sql") ||
                exceptionType.Name.Contains("IO"))
                return true;

            return true; // デフォルトは回復可能
        }

        private string AnalyzeRootCause(Exception exception)
        {
            // 内部例外の分析
            var innerException = exception.InnerException;
            if (innerException != null)
            {
                return $"Root cause: {innerException.GetType().Name} - {SanitizeErrorMessage(innerException.Message, SanitizationLevel.Standard)}";
            }

            // メッセージからの推定
            var message = exception.Message?.ToLower() ?? string.Empty;
            if (message.Contains("connection") && message.Contains("timeout"))
                return "Network connectivity or database performance issue";
            if (message.Contains("access") && message.Contains("denied"))
                return "Insufficient permissions or authentication failure";
            if (message.Contains("not found"))
                return "Missing resource or configuration";

            return "Unable to determine root cause from available information";
        }

        private List<string> GenerateRecommendations(Exception exception, ErrorAnalysis analysis)
        {
            var recommendations = new List<string>();

            switch (analysis.Category)
            {
                case ErrorCategory.Database:
                    recommendations.Add("データベース接続とパフォーマンスを確認");
                    recommendations.Add("クエリの最適化を検討");
                    break;
                case ErrorCategory.Network:
                    recommendations.Add("ネットワーク接続状況を確認");
                    recommendations.Add("タイムアウト設定を見直し");
                    break;
                case ErrorCategory.Authentication:
                    recommendations.Add("認証設定を確認");
                    recommendations.Add("ユーザー権限を検証");
                    break;
                case ErrorCategory.Validation:
                    recommendations.Add("入力データの検証ルールを確認");
                    recommendations.Add("ユーザーインターフェースの改善を検討");
                    break;
            }

            if (analysis.IsRecoverable)
            {
                recommendations.Add("自動リトライ機能の実装を検討");
            }

            return recommendations;
        }

        private string RemoveStackTraces(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            return SensitivePatterns["StackTrace"].Replace(message, "[スタックトレース情報を除去]");
        }

        private string ApplyStandardSanitization(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            var sanitized = message;

            // 各パターンに対してサニタイズを適用
            foreach (var pattern in SensitivePatterns)
            {
                sanitized = pattern.Value.Replace(sanitized, $"[{pattern.Key}情報を除去]");
            }

            return sanitized;
        }

        private string ApplyAggressiveSanitization(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            var sanitized = ApplyStandardSanitization(message);

            // より積極的なサニタイズ
            sanitized = Regex.Replace(sanitized, @"[A-Za-z]:\\[^\s]*", "[パス情報を除去]");
            sanitized = Regex.Replace(sanitized, @"\b\w+\.\w+\.\w+\b", "[名前空間情報を除去]");
            sanitized = Regex.Replace(sanitized, @"\b\d+\.\d+\.\d+\.\d+\b", "[IPアドレスを除去]");

            return sanitized;
        }

        private Dictionary<string, object> SanitizeContext(Dictionary<string, object> context)
        {
            if (context == null)
                return new Dictionary<string, object>();

            var sanitized = new Dictionary<string, object>();

            foreach (var kvp in context)
            {
                if (kvp.Value == null)
                    continue;

                var key = kvp.Key?.ToLower() ?? string.Empty;
                
                // 機密情報を含む可能性のあるキーをスキップ
                if (key.Contains("password") || key.Contains("secret") || key.Contains("key") || key.Contains("token"))
                    continue;

                var value = kvp.Value.ToString();
                if (ContainsSensitiveInformation(value))
                {
                    sanitized[kvp.Key] = "[機密情報を除去]";
                }
                else
                {
                    sanitized[kvp.Key] = SanitizeErrorMessage(value, SanitizationLevel.Standard);
                }
            }

            return sanitized;
        }

        private void UpdateErrorStatistics(ErrorCategory category, ErrorSeverity severity)
        {
            // 実装依存の統計更新処理
            // 通常はデータベースやキャッシュに統計情報を保存
        }

        protected virtual ErrorStatistics GetErrorStatisticsFromStorage(DateTime cutoff)
        {
            // 実装依存の統計データ取得
            return new ErrorStatistics
            {
                Period = DateTime.UtcNow.Subtract(cutoff),
                TotalErrors = 0,
                CategoryBreakdown = new Dictionary<ErrorCategory, int>(),
                SeverityBreakdown = new Dictionary<ErrorSeverity, int>(),
                GeneratedAt = DateTime.UtcNow
            };
        }
    }

    // サポートクラスとEnum

    public class ErrorHandlingConfiguration
    {
        public bool IncludeDeveloperDetails { get; set; } = false;
        public bool IncludeStackTrace { get; set; } = false;
        public int MaxMessageLength { get; set; } = 500;
        public SanitizationLevel DefaultSanitizationLevel { get; set; } = SanitizationLevel.Standard;
    }

    public class SecureErrorResponse
    {
        public string ErrorId { get; set; }
        public string RequestId { get; set; }
        public DateTime OccurredAt { get; set; }
        public ErrorCategory Category { get; set; }
        public ErrorSeverity Severity { get; set; }
        public string UserMessage { get; set; }
        public string DeveloperMessage { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
    }

    public class ErrorAnalysis
    {
        public string ExceptionType { get; set; }
        public string Message { get; set; }
        public ErrorCategory Category { get; set; }
        public ErrorSeverity Severity { get; set; }
        public bool IsSecurityRelated { get; set; }
        public bool ContainsSensitiveData { get; set; }
        public bool IsRecoverable { get; set; }
        public string RootCause { get; set; }
        public List<string> RecommendedActions { get; set; } = new();
        public DateTime AnalyzedAt { get; set; }
    }

    public class ErrorStatistics
    {
        public TimeSpan Period { get; set; }
        public int TotalErrors { get; set; }
        public Dictionary<ErrorCategory, int> CategoryBreakdown { get; set; } = new();
        public Dictionary<ErrorSeverity, int> SeverityBreakdown { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }

    public enum ErrorCategory
    {
        System,
        Authentication,
        Authorization,
        Validation,
        Database,
        Network,
        FileSystem,
        Configuration,
        External,
        Unknown
    }

    public enum ErrorSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum SanitizationLevel
    {
        Minimal,
        Standard,
        Aggressive
    }
}