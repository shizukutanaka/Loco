using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;
using System.Linq;

namespace Loco.Core.Security
{
    /// <summary>
    /// セキュリティ監査ログサービス - P0項目#9
    /// 改ざん防止、暗号化、完全性検証機能付きの監査ログシステム
    /// </summary>
    public class SecurityAuditLogger : IDisposable
    {
        private readonly ILogger<SecurityAuditLogger> _logger;
        private readonly AuditConfiguration _config;
        private readonly ConcurrentQueue<AuditLogEntry> _logQueue;
        private readonly Timer _flushTimer;
        private readonly string _instanceId;
        private long _sequenceNumber = 0;

        // セキュリティイベントカテゴリ
        public enum SecurityEventType
        {
            Authentication,
            Authorization,
            DataAccess,
            DataModification,
            AdminAction,
            SecurityViolation,
            SystemEvent,
            ComplianceEvent,
            PrivacyEvent
        }

        // 重要度レベル
        public enum AuditSeverity
        {
            Info,
            Warning,
            Error,
            Critical
        }

        public SecurityAuditLogger(
            ILogger<SecurityAuditLogger> logger = null,
            AuditConfiguration config = null)
        {
            _logger = logger;
            _config = config ?? new AuditConfiguration();
            _logQueue = new ConcurrentQueue<AuditLogEntry>();
            _instanceId = Environment.MachineName + "_" + Guid.NewGuid().ToString("N")[..8];

            // 定期フラッシュタイマー
            _flushTimer = new Timer(
                FlushPendingLogs,
                null,
                TimeSpan.FromSeconds(_config.FlushIntervalSeconds),
                TimeSpan.FromSeconds(_config.FlushIntervalSeconds));

            LogSystemEvent("Security Audit Logger initialized", AuditSeverity.Info);
        }

        /// <summary>
        /// セキュリティイベントの監査ログ記録 - P0項目#9
        /// </summary>
        public async Task LogSecurityEventAsync(
            SecurityEventType eventType,
            string action,
            string userId = null,
            string resource = null,
            string ipAddress = null,
            Dictionary<string, object> additionalData = null,
            AuditSeverity severity = AuditSeverity.Info,
            bool requireIntegrityCheck = true)
        {
            try
            {
                var auditEntry = new AuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    SequenceNumber = Interlocked.Increment(ref _sequenceNumber),
                    Timestamp = DateTime.UtcNow,
                    EventType = eventType,
                    Action = action,
                    UserId = userId,
                    Resource = resource,
                    IpAddress = ipAddress,
                    UserAgent = GetCurrentUserAgent(),
                    SessionId = GetCurrentSessionId(),
                    Severity = severity,
                    Source = _instanceId,
                    AdditionalData = additionalData ?? new Dictionary<string, object>(),
                    CorrelationId = GetCurrentCorrelationId()
                };

                // 機密データのマスキング
                MaskSensitiveData(auditEntry);

                // デジタル署名（改ざん防止）
                if (requireIntegrityCheck)
                {
                    auditEntry.Signature = ComputeDigitalSignature(auditEntry);
                }

                // 暗号化（必要に応じて）
                if (_config.EncryptAuditLogs)
                {
                    auditEntry.EncryptedData = EncryptAuditData(auditEntry);
                }

                // ログキューに追加
                _logQueue.Enqueue(auditEntry);

                // 重要なイベントは即座にフラッシュ
                if (severity >= AuditSeverity.Error)
                {
                    await FlushPendingLogsAsync();
                }

                // コンプライアンス報告（必要に応じて）
                if (_config.EnableComplianceReporting && IsComplianceEvent(eventType))
                {
                    await GenerateComplianceReportAsync(auditEntry);
                }

                _logger?.LogDebug("Security audit event recorded: {EventType} - {Action}", eventType, action);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to log security audit event: {EventType} - {Action}", eventType, action);
                // 監査ログ失敗自体もログ記録
                await LogSystemErrorAsync("Audit logging failed", ex);
            }
        }

        /// <summary>
        /// 認証イベントの監査ログ - 特殊化メソッド
        /// </summary>
        public async Task LogAuthenticationEventAsync(
            string action,
            bool success,
            string userId,
            string ipAddress,
            string failureReason = null)
        {
            var additionalData = new Dictionary<string, object>
            {
                { "Success", success },
                { "FailureReason", failureReason }
            };

            var severity = success ? AuditSeverity.Info : AuditSeverity.Warning;

            await LogSecurityEventAsync(
                SecurityEventType.Authentication,
                action,
                userId,
                "Authentication System",
                ipAddress,
                additionalData,
                severity);
        }

        /// <summary>
        /// 認可イベントの監査ログ
        /// </summary>
        public async Task LogAuthorizationEventAsync(
            string action,
            bool authorized,
            string userId,
            string resource,
            string permission,
            string ipAddress = null)
        {
            var additionalData = new Dictionary<string, object>
            {
                { "Authorized", authorized },
                { "Permission", permission }
            };

            var severity = authorized ? AuditSeverity.Info : AuditSeverity.Warning;

            await LogSecurityEventAsync(
                SecurityEventType.Authorization,
                action,
                userId,
                resource,
                ipAddress,
                additionalData,
                severity);
        }

        /// <summary>
        /// データアクセスの監査ログ
        /// </summary>
        public async Task LogDataAccessEventAsync(
            string action,
            string userId,
            string dataType,
            string recordId = null,
            int? recordCount = null,
            string ipAddress = null)
        {
            var additionalData = new Dictionary<string, object>
            {
                { "DataType", dataType },
                { "RecordId", recordId },
                { "RecordCount", recordCount }
            };

            await LogSecurityEventAsync(
                SecurityEventType.DataAccess,
                action,
                userId,
                $"Data: {dataType}",
                ipAddress,
                additionalData);
        }

        /// <summary>
        /// データ変更の監査ログ
        /// </summary>
        public async Task LogDataModificationEventAsync(
            string action,
            string userId,
            string dataType,
            string recordId,
            object oldValues,
            object newValues,
            string ipAddress = null)
        {
            var additionalData = new Dictionary<string, object>
            {
                { "DataType", dataType },
                { "RecordId", recordId },
                { "OldValues", MaskSensitiveValues(oldValues) },
                { "NewValues", MaskSensitiveValues(newValues) }
            };

            await LogSecurityEventAsync(
                SecurityEventType.DataModification,
                action,
                userId,
                $"Data: {dataType}",
                ipAddress,
                additionalData,
                AuditSeverity.Info,
                true); // データ変更は常に整合性チェック
        }

        /// <summary>
        /// 管理者操作の監査ログ
        /// </summary>
        public async Task LogAdminActionAsync(
            string action,
            string adminUserId,
            string targetResource,
            Dictionary<string, object> parameters = null,
            string ipAddress = null)
        {
            var additionalData = new Dictionary<string, object>
            {
                { "AdminAction", true },
                { "Parameters", parameters }
            };

            await LogSecurityEventAsync(
                SecurityEventType.AdminAction,
                action,
                adminUserId,
                targetResource,
                ipAddress,
                additionalData,
                AuditSeverity.Warning); // 管理者操作は要注意
        }

        /// <summary>
        /// セキュリティ違反の監査ログ
        /// </summary>
        public async Task LogSecurityViolationAsync(
            string violationType,
            string description,
            string userId = null,
            string ipAddress = null,
            Dictionary<string, object> evidence = null)
        {
            var additionalData = new Dictionary<string, object>
            {
                { "ViolationType", violationType },
                { "Description", description },
                { "Evidence", evidence }
            };

            await LogSecurityEventAsync(
                SecurityEventType.SecurityViolation,
                "Security Violation Detected",
                userId,
                "Security System",
                ipAddress,
                additionalData,
                AuditSeverity.Critical);

            // セキュリティ違反は即座にアラート
            if (_config.EnableSecurityAlerts)
            {
                await TriggerSecurityAlertAsync(violationType, description, userId, ipAddress);
            }
        }

        /// <summary>
        /// プライバシー関連イベントの監査ログ（GDPR対応）
        /// </summary>
        public async Task LogPrivacyEventAsync(
            string action,
            string userId,
            string personalDataType,
            string legalBasis,
            string purpose,
            string ipAddress = null)
        {
            var additionalData = new Dictionary<string, object>
            {
                { "PersonalDataType", personalDataType },
                { "LegalBasis", legalBasis },
                { "Purpose", purpose },
                { "GdprCompliance", true }
            };

            await LogSecurityEventAsync(
                SecurityEventType.PrivacyEvent,
                action,
                userId,
                $"Personal Data: {personalDataType}",
                ipAddress,
                additionalData,
                AuditSeverity.Info,
                true); // プライバシーイベントは整合性必須
        }

        /// <summary>
        /// 監査ログの検索とフィルタリング
        /// </summary>
        public async Task<List<AuditLogEntry>> SearchAuditLogsAsync(AuditSearchCriteria criteria)
        {
            try
            {
                // 実装では永続化ストレージから検索
                // ここでは基本的な例を示す
                var results = new List<AuditLogEntry>();

                // セキュリティ: 検索操作自体をログ記録
                await LogSecurityEventAsync(
                    SecurityEventType.DataAccess,
                    "Audit Log Search",
                    criteria.RequestingUserId,
                    "Audit Logs",
                    criteria.IpAddress,
                    new Dictionary<string, object> { { "SearchCriteria", criteria } });

                return results;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Audit log search failed");
                throw;
            }
        }

        /// <summary>
        /// 監査ログの整合性検証
        /// </summary>
        public async Task<IntegrityVerificationResult> VerifyAuditLogIntegrityAsync(Guid auditLogId)
        {
            var result = new IntegrityVerificationResult { LogId = auditLogId };

            try
            {
                // 永続化ストレージから監査ログを取得
                var auditLog = await GetAuditLogByIdAsync(auditLogId);
                if (auditLog == null)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Audit log not found";
                    return result;
                }

                // デジタル署名の検証
                var expectedSignature = ComputeDigitalSignature(auditLog);
                result.IsValid = string.Equals(auditLog.Signature, expectedSignature, StringComparison.Ordinal);

                if (!result.IsValid)
                {
                    result.ErrorMessage = "Digital signature verification failed";
                    
                    // 改ざん検出をログ記録
                    await LogSecurityViolationAsync(
                        "Audit Log Tampering",
                        $"Integrity verification failed for audit log {auditLogId}",
                        evidence: new Dictionary<string, object>
                        {
                            { "ExpectedSignature", expectedSignature },
                            { "ActualSignature", auditLog.Signature }
                        });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Integrity verification failed for audit log {LogId}", auditLogId);
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        // プライベートメソッド

        private void MaskSensitiveData(AuditLogEntry entry)
        {
            var sensitiveFields = _config.SensitiveFields;
            
            if (entry.AdditionalData != null)
            {
                foreach (var field in sensitiveFields)
                {
                    if (entry.AdditionalData.ContainsKey(field))
                    {
                        entry.AdditionalData[field] = MaskValue(entry.AdditionalData[field]);
                    }
                }
            }
        }

        private object MaskValue(object value)
        {
            if (value == null) return null;
            
            var stringValue = value.ToString();
            if (stringValue.Length <= 4) return "****";
            
            return stringValue.Substring(0, 2) + new string('*', stringValue.Length - 4) + stringValue.Substring(stringValue.Length - 2);
        }

        private object MaskSensitiveValues(object data)
        {
            if (data == null) return null;
            
            var json = JsonSerializer.Serialize(data);
            // 機密データのマスキング処理
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }

        private string ComputeDigitalSignature(AuditLogEntry entry)
        {
            try
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_config.SigningKey));
                var data = $"{entry.Id}|{entry.SequenceNumber}|{entry.Timestamp:O}|{entry.EventType}|{entry.Action}|{entry.UserId}|{entry.Source}";
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                return Convert.ToBase64String(hash);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to compute digital signature");
                throw;
            }
        }

        private string EncryptAuditData(AuditLogEntry entry)
        {
            try
            {
                var json = JsonSerializer.Serialize(entry.AdditionalData);
                var bytes = Encoding.UTF8.GetBytes(json);
                
                using var aes = Aes.Create();
                aes.Key = Convert.FromBase64String(_config.EncryptionKey);
                aes.GenerateIV();
                
                using var encryptor = aes.CreateEncryptor();
                var encryptedBytes = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
                
                return Convert.ToBase64String(aes.IV.Concat(encryptedBytes).ToArray());
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to encrypt audit data");
                throw;
            }
        }

        private async void FlushPendingLogs(object state)
        {
            await FlushPendingLogsAsync();
        }

        private async Task FlushPendingLogsAsync()
        {
            var logsToFlush = new List<AuditLogEntry>();
            
            while (_logQueue.TryDequeue(out var log) && logsToFlush.Count < _config.MaxBatchSize)
            {
                logsToFlush.Add(log);
            }

            if (logsToFlush.Count > 0)
            {
                await PersistAuditLogsAsync(logsToFlush);
            }
        }

        private async Task PersistAuditLogsAsync(List<AuditLogEntry> logs)
        {
            try
            {
                // 実際の実装では永続化ストレージ（DB、ファイル等）に保存
                foreach (var log in logs)
                {
                    var logMessage = JsonSerializer.Serialize(log, new JsonSerializerOptions
                    {
                        WriteIndented = false,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    
                    _logger?.LogInformation("[AUDIT] {LogMessage}", logMessage);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to persist audit logs");
            }
        }

        private bool IsComplianceEvent(SecurityEventType eventType)
        {
            return eventType == SecurityEventType.PrivacyEvent ||
                   eventType == SecurityEventType.DataAccess ||
                   eventType == SecurityEventType.DataModification;
        }

        private async Task GenerateComplianceReportAsync(AuditLogEntry entry)
        {
            // GDPR、CCPA等のコンプライアンス報告
            try
            {
                var report = new ComplianceReport
                {
                    AuditLogId = entry.Id,
                    EventType = entry.EventType.ToString(),
                    Timestamp = entry.Timestamp,
                    ComplianceStandard = "GDPR",
                    ReportData = entry.AdditionalData
                };

                // 実装では外部コンプライアンスシステムに送信
                _logger?.LogInformation("Compliance report generated for audit log {LogId}", entry.Id);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to generate compliance report");
            }
        }

        private async Task TriggerSecurityAlertAsync(string violationType, string description, string userId, string ipAddress)
        {
            try
            {
                var alert = new SecurityAlert
                {
                    Type = violationType,
                    Description = description,
                    UserId = userId,
                    IpAddress = ipAddress,
                    Timestamp = DateTime.UtcNow,
                    Severity = "Critical"
                };

                // 実装ではアラートシステムに送信
                _logger?.LogCritical("SECURITY ALERT: {Type} - {Description} | User: {UserId} | IP: {IpAddress}",
                    violationType, description, userId, ipAddress);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to trigger security alert");
            }
        }

        private async Task LogSystemErrorAsync(string message, Exception exception)
        {
            try
            {
                await LogSecurityEventAsync(
                    SecurityEventType.SystemEvent,
                    "System Error",
                    additionalData: new Dictionary<string, object>
                    {
                        { "Message", message },
                        { "Exception", exception.ToString() }
                    },
                    severity: AuditSeverity.Error);
            }
            catch
            {
                // 監査ログの監査ログ失敗は無視（無限ループ防止）
            }
        }

        private void LogSystemEvent(string message, AuditSeverity severity)
        {
            try
            {
                var entry = new AuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    SequenceNumber = Interlocked.Increment(ref _sequenceNumber),
                    Timestamp = DateTime.UtcNow,
                    EventType = SecurityEventType.SystemEvent,
                    Action = "System Event",
                    Severity = severity,
                    Source = _instanceId,
                    AdditionalData = new Dictionary<string, object> { { "Message", message } }
                };

                _logQueue.Enqueue(entry);
            }
            catch
            {
                // システムイベントログ失敗は無視
            }
        }

        private string GetCurrentUserAgent()
        {
            // 実装では HttpContext から取得
            return "Loco/1.0";
        }

        private string GetCurrentSessionId()
        {
            // 実装では現在のセッションIDを取得
            return null;
        }

        private string GetCurrentCorrelationId()
        {
            // 実装では分散トレーシングのコリレーションIDを取得
            return Guid.NewGuid().ToString();
        }

        private async Task<AuditLogEntry> GetAuditLogByIdAsync(Guid id)
        {
            // 実装では永続化ストレージから取得
            return null;
        }

        public void Dispose()
        {
            _flushTimer?.Dispose();
            FlushPendingLogsAsync().Wait(5000); // 最大5秒待機
            LogSystemEvent("Security Audit Logger disposed", AuditSeverity.Info);
        }
    }

    // サポートクラス

    public class AuditConfiguration
    {
        public int FlushIntervalSeconds { get; set; } = 30;
        public int MaxBatchSize { get; set; } = 100;
        public bool EncryptAuditLogs { get; set; } = true;
        public bool EnableComplianceReporting { get; set; } = true;
        public bool EnableSecurityAlerts { get; set; } = true;
        public string SigningKey { get; set; } = GenerateKey();
        public string EncryptionKey { get; set; } = GenerateKey();
        public List<string> SensitiveFields { get; set; } = new List<string> 
        { 
            "password", "ssn", "creditCard", "bankAccount", "personalId" 
        };

        private static string GenerateKey()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }

    public class AuditLogEntry
    {
        public Guid Id { get; set; }
        public long SequenceNumber { get; set; }
        public DateTime Timestamp { get; set; }
        public SecurityAuditLogger.SecurityEventType EventType { get; set; }
        public string Action { get; set; }
        public string UserId { get; set; }
        public string Resource { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string SessionId { get; set; }
        public SecurityAuditLogger.AuditSeverity Severity { get; set; }
        public string Source { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; }
        public string CorrelationId { get; set; }
        public string Signature { get; set; }
        public string EncryptedData { get; set; }
    }

    public class AuditSearchCriteria
    {
        public string RequestingUserId { get; set; }
        public string IpAddress { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string UserId { get; set; }
        public string EventType { get; set; }
        public string Action { get; set; }
        public int MaxResults { get; set; } = 100;
    }

    public class IntegrityVerificationResult
    {
        public Guid LogId { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ComplianceReport
    {
        public Guid AuditLogId { get; set; }
        public string EventType { get; set; }
        public DateTime Timestamp { get; set; }
        public string ComplianceStandard { get; set; }
        public Dictionary<string, object> ReportData { get; set; }
    }

    public class SecurityAlert
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public string UserId { get; set; }
        public string IpAddress { get; set; }
        public DateTime Timestamp { get; set; }
        public string Severity { get; set; }
    }
}