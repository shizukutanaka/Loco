using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Loco.Core.Security
{
    /// <summary>
    /// 個人情報保護法・GDPR・CCPA対応サービス - P0項目#6
    /// データ保護、同意管理、削除権、ポータビリティ権の実装
    /// </summary>
    public class PrivacyComplianceService : IDisposable
    {
        private readonly ILogger<PrivacyComplianceService> _logger;
        private readonly SecurityAuditLogger _auditLogger;
        private readonly PrivacyConfiguration _config;
        
        // 個人情報検出パターン
        private static readonly Dictionary<PersonalDataType, Regex> DetectionPatterns = new()
        {
            { PersonalDataType.Email, new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled) },
            { PersonalDataType.PhoneNumber, new Regex(@"(\+\d{1,3}[-.\s]?)?\(?\d{1,4}\)?[-.\s]?\d{1,4}[-.\s]?\d{1,9}", RegexOptions.Compiled) },
            { PersonalDataType.CreditCard, new Regex(@"\b(?:\d{4}[-\s]?){3}\d{4}\b", RegexOptions.Compiled) },
            { PersonalDataType.SocialSecurityNumber, new Regex(@"\b\d{3}-?\d{2}-?\d{4}\b", RegexOptions.Compiled) },
            { PersonalDataType.JapaneseMyNumber, new Regex(@"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}\b", RegexOptions.Compiled) },
            { PersonalDataType.BankAccount, new Regex(@"\b\d{7,12}\b", RegexOptions.Compiled) },
            { PersonalDataType.IPAddress, new Regex(@"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b", RegexOptions.Compiled) }
        };

        public PrivacyComplianceService(
            ILogger<PrivacyComplianceService> logger = null,
            SecurityAuditLogger auditLogger = null,
            PrivacyConfiguration config = null)
        {
            _logger = logger;
            _auditLogger = auditLogger;
            _config = config ?? new PrivacyConfiguration();
        }

        /// <summary>
        /// データ処理同意の記録と管理
        /// </summary>
        public async Task<string> RecordConsentAsync(
            string userId,
            ConsentType consentType,
            string purpose,
            string legalBasis = "Consent",
            Dictionary<string, object> additionalData = null)
        {
            try
            {
                var consent = new DataProcessingConsent
                {
                    ConsentId = GenerateConsentId(),
                    UserId = userId,
                    ConsentType = consentType,
                    Purpose = purpose,
                    LegalBasis = legalBasis,
                    GrantedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.Add(_config.ConsentRetentionPeriod),
                    Status = ConsentStatus.Active,
                    Version = _config.PrivacyPolicyVersion,
                    AdditionalData = additionalData ?? new Dictionary<string, object>(),
                    DigitalSignature = ComputeConsentHash(userId, consentType, purpose)
                };

                // 同意記録の保存（実装依存）
                await SaveConsentRecordAsync(consent);

                // 監査ログ記録
                await _auditLogger?.LogPrivacyEventAsync(
                    "ConsentGranted",
                    userId,
                    $"{consentType}:{purpose}"
                );

                _logger?.LogInformation("Consent recorded for user {UserId} | Type: {ConsentType} | Purpose: {Purpose}",
                    userId, consentType, purpose);

                return consent.ConsentId;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to record consent for user {UserId}", userId);
                throw new PrivacyComplianceException("Consent recording failed", ex);
            }
        }

        /// <summary>
        /// 同意の撤回処理
        /// </summary>
        public async Task<bool> RevokeConsentAsync(string userId, string consentId, string reason = null)
        {
            try
            {
                var consent = await GetConsentRecordAsync(consentId);
                if (consent == null || !string.Equals(consent.UserId, userId, StringComparison.Ordinal))
                {
                    _logger?.LogWarning("Consent revocation attempted for invalid consent {ConsentId} by user {UserId}",
                        consentId, userId);
                    return false;
                }

                consent.Status = ConsentStatus.Revoked;
                consent.RevokedAt = DateTime.UtcNow;
                consent.RevocationReason = reason;

                await UpdateConsentRecordAsync(consent);

                // 関連データの処理停止
                await ProcessDataRetentionAsync(userId, consent.ConsentType);

                // 監査ログ記録
                await _auditLogger?.LogPrivacyEventAsync(
                    "ConsentRevoked",
                    userId,
                    $"{consent.ConsentType}:{reason}"
                );

                _logger?.LogInformation("Consent revoked for user {UserId} | ConsentId: {ConsentId}",
                    userId, consentId);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to revoke consent {ConsentId} for user {UserId}", consentId, userId);
                return false;
            }
        }

        /// <summary>
        /// 個人データの検出と分類
        /// </summary>
        public PersonalDataScanResult ScanForPersonalData(string content, string context = null)
        {
            var result = new PersonalDataScanResult
            {
                Content = content,
                Context = context,
                ScannedAt = DateTime.UtcNow,
                DetectedItems = new List<PersonalDataItem>()
            };

            try
            {
                foreach (var pattern in DetectionPatterns)
                {
                    var matches = pattern.Value.Matches(content);
                    foreach (Match match in matches)
                    {
                        var item = new PersonalDataItem
                        {
                            Type = pattern.Key,
                            Value = _config.LogSensitiveData ? match.Value : MaskSensitiveData(match.Value),
                            Position = match.Index,
                            Length = match.Length,
                            Confidence = CalculateConfidence(pattern.Key, match.Value),
                            RiskLevel = AssessRiskLevel(pattern.Key)
                        };

                        result.DetectedItems.Add(item);
                    }
                }

                result.HasPersonalData = result.DetectedItems.Any();
                result.RiskScore = CalculateOverallRiskScore(result.DetectedItems);

                if (result.HasPersonalData)
                {
                    _logger?.LogInformation("Personal data detected in content | Items: {Count} | Risk: {RiskScore}",
                        result.DetectedItems.Count, result.RiskScore);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during personal data scan");
                result.HasError = true;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// データポータビリティ権の実装（データエクスポート）
        /// </summary>
        public async Task<DataExportResult> ExportUserDataAsync(string userId, DataExportFormat format = DataExportFormat.Json)
        {
            try
            {
                // ユーザーの同意確認
                var hasValidConsent = await ValidateDataPortabilityConsentAsync(userId);
                if (!hasValidConsent)
                {
                    return new DataExportResult
                    {
                        Success = false,
                        ErrorMessage = "Valid consent required for data export"
                    };
                }

                var userData = await GatherUserDataAsync(userId);
                var exportData = new UserDataExport
                {
                    UserId = userId,
                    ExportedAt = DateTime.UtcNow,
                    DataCategories = userData,
                    Metadata = new Dictionary<string, object>
                    {
                        { "exportFormat", format.ToString() },
                        { "privacyPolicyVersion", _config.PrivacyPolicyVersion },
                        { "exportId", Guid.NewGuid().ToString() }
                    }
                };

                // データの暗号化
                var encryptedData = await EncryptExportDataAsync(exportData);

                // 監査ログ記録
                await _auditLogger?.LogPrivacyEventAsync(
                    "DataExported",
                    userId,
                    $"Format:{format}"
                );

                return new DataExportResult
                {
                    Success = true,
                    Data = encryptedData,
                    Format = format,
                    ExportedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.Add(_config.ExportDataRetentionPeriod)
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to export data for user {UserId}", userId);
                return new DataExportResult
                {
                    Success = false,
                    ErrorMessage = "Data export failed"
                };
            }
        }

        /// <summary>
        /// 忘れられる権利の実装（データ完全削除）
        /// </summary>
        public async Task<DeletionResult> DeleteUserDataAsync(
            string userId,
            DeletionScope scope = DeletionScope.All,
            string reason = null)
        {
            var result = new DeletionResult
            {
                UserId = userId,
                RequestedAt = DateTime.UtcNow,
                Scope = scope,
                Reason = reason
            };

            try
            {
                // 削除前のデータ整合性確認
                var dataIntegrity = await VerifyDataIntegrityAsync(userId);
                
                var deletedItems = new List<DeletedDataItem>();

                // スコープに応じた削除処理
                switch (scope)
                {
                    case DeletionScope.All:
                        deletedItems.AddRange(await DeleteAllUserDataAsync(userId));
                        break;
                    case DeletionScope.PersonalDataOnly:
                        deletedItems.AddRange(await DeletePersonalDataAsync(userId));
                        break;
                    case DeletionScope.Specific:
                        deletedItems.AddRange(await DeleteSpecificDataAsync(userId, reason));
                        break;
                }

                // 削除確認と検証
                var verificationResult = await VerifyDeletionCompletionAsync(userId, deletedItems);
                
                result.Success = verificationResult.IsComplete;
                result.DeletedItems = deletedItems;
                result.ProcessedAt = DateTime.UtcNow;
                result.VerificationHash = ComputeDeletionHash(userId, deletedItems);

                // 監査ログ記録（削除後も保持）
                await _auditLogger?.LogPrivacyEventAsync(
                    "DataDeleted",
                    userId,
                    $"Scope:{scope},Items:{deletedItems.Count}"
                );

                _logger?.LogInformation("User data deletion completed | User: {UserId} | Items: {Count}",
                    userId, deletedItems.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to delete user data for {UserId}", userId);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// データ保持期間の管理
        /// </summary>
        public async Task ProcessDataRetentionAsync(string userId, ConsentType? specificConsent = null)
        {
            try
            {
                var retentionPolicies = await GetRetentionPoliciesAsync(userId);
                var expiredData = new List<string>();

                foreach (var policy in retentionPolicies)
                {
                    if (specificConsent.HasValue && policy.ConsentType != specificConsent.Value)
                        continue;

                    if (IsDataRetentionExpired(policy))
                    {
                        await ArchiveOrDeleteDataAsync(userId, policy);
                        expiredData.Add(policy.DataCategory);
                    }
                }

                if (expiredData.Count > 0)
                {
                    await _auditLogger?.LogPrivacyEventAsync(
                        "DataRetentionProcessed",
                        userId,
                        string.Join(",", expiredData)
                    );
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Data retention processing failed for user {UserId}", userId);
            }
        }

        /// <summary>
        /// プライバシー違反の検出と報告
        /// </summary>
        public async Task<PrivacyViolationReport> DetectPrivacyViolationsAsync(
            string operationType,
            Dictionary<string, object> operationData)
        {
            var report = new PrivacyViolationReport
            {
                OperationType = operationType,
                DetectedAt = DateTime.UtcNow,
                Violations = new List<PrivacyViolation>()
            };

            try
            {
                // 同意なしでの個人データ処理をチェック
                var consentViolations = await CheckConsentViolationsAsync(operationData);
                report.Violations.AddRange(consentViolations);

                // データ最小化原則の違反をチェック
                var minimizationViolations = CheckDataMinimizationViolations(operationData);
                report.Violations.AddRange(minimizationViolations);

                // 目的外利用をチェック
                var purposeViolations = await CheckPurposeLimitationViolationsAsync(operationData);
                report.Violations.AddRange(purposeViolations);

                report.RiskLevel = CalculateViolationRiskLevel(report.Violations);
                report.RequiresImmediateAction = report.Violations.Any(v => v.Severity == ViolationSeverity.Critical);

                if (report.Violations.Count > 0)
                {
                    await _auditLogger?.LogPrivacyEventAsync(
                        "PrivacyViolationDetected",
                        null,
                        $"Operation:{operationType},Violations:{report.Violations.Count}"
                    );
                }

                return report;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Privacy violation detection failed for operation {OperationType}", operationType);
                report.HasError = true;
                report.ErrorMessage = ex.Message;
                return report;
            }
        }

        // プライベートメソッド

        private string GenerateConsentId()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[16];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        private string ComputeConsentHash(string userId, ConsentType consentType, string purpose)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_config.HashSecretKey));
            var data = $"{userId}|{consentType}|{purpose}|{DateTime.UtcNow:O}";
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash);
        }

        private string MaskSensitiveData(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= 4)
                return "***";
            
            return value.Substring(0, 2) + new string('*', value.Length - 4) + value.Substring(value.Length - 2);
        }

        private double CalculateConfidence(PersonalDataType type, string value)
        {
            // 各データタイプに応じた信頼度計算
            return type switch
            {
                PersonalDataType.Email => value.Contains("@") && value.Contains(".") ? 0.95 : 0.6,
                PersonalDataType.CreditCard => IsValidCreditCardNumber(value) ? 0.9 : 0.5,
                PersonalDataType.SocialSecurityNumber => value.Length >= 9 ? 0.85 : 0.4,
                _ => 0.7
            };
        }

        private RiskLevel AssessRiskLevel(PersonalDataType type)
        {
            return type switch
            {
                PersonalDataType.CreditCard => RiskLevel.Critical,
                PersonalDataType.SocialSecurityNumber => RiskLevel.Critical,
                PersonalDataType.JapaneseMyNumber => RiskLevel.Critical,
                PersonalDataType.Email => RiskLevel.Medium,
                PersonalDataType.PhoneNumber => RiskLevel.Medium,
                PersonalDataType.IPAddress => RiskLevel.Low,
                _ => RiskLevel.Low
            };
        }

        private int CalculateOverallRiskScore(List<PersonalDataItem> items)
        {
            if (!items.Any()) return 0;

            var score = items.Sum(item => item.RiskLevel switch
            {
                RiskLevel.Critical => 10,
                RiskLevel.High => 7,
                RiskLevel.Medium => 5,
                RiskLevel.Low => 2,
                _ => 1
            });

            return Math.Min(score, 100);
        }

        private bool IsValidCreditCardNumber(string number)
        {
            // Luhnアルゴリズムによるクレジットカード番号の検証
            var digits = number.Where(char.IsDigit).Select(c => c - '0').ToArray();
            if (digits.Length < 13 || digits.Length > 19) return false;

            var sum = 0;
            var isEven = false;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                var digit = digits[i];
                if (isEven)
                {
                    digit *= 2;
                    if (digit > 9) digit -= 9;
                }
                sum += digit;
                isEven = !isEven;
            }
            return sum % 10 == 0;
        }

        private string ComputeDeletionHash(string userId, List<DeletedDataItem> deletedItems)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_config.HashSecretKey));
            var data = $"{userId}|{DateTime.UtcNow:O}|{string.Join("|", deletedItems.Select(d => d.ItemId))}";
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash);
        }

        // 実装依存の抽象メソッド（継承またはDIで実装）
        protected virtual Task SaveConsentRecordAsync(DataProcessingConsent consent) => Task.CompletedTask;
        protected virtual Task<DataProcessingConsent> GetConsentRecordAsync(string consentId) => Task.FromResult<DataProcessingConsent>(null);
        protected virtual Task UpdateConsentRecordAsync(DataProcessingConsent consent) => Task.CompletedTask;
        protected virtual Task<bool> ValidateDataPortabilityConsentAsync(string userId) => Task.FromResult(true);
        protected virtual Task<Dictionary<string, object>> GatherUserDataAsync(string userId) => Task.FromResult(new Dictionary<string, object>());
        protected virtual Task<byte[]> EncryptExportDataAsync(UserDataExport exportData) => Task.FromResult(JsonSerializer.SerializeToUtf8Bytes(exportData));
        protected virtual Task<List<DeletedDataItem>> DeleteAllUserDataAsync(string userId) => Task.FromResult(new List<DeletedDataItem>());
        protected virtual Task<List<DeletedDataItem>> DeletePersonalDataAsync(string userId) => Task.FromResult(new List<DeletedDataItem>());
        protected virtual Task<List<DeletedDataItem>> DeleteSpecificDataAsync(string userId, string criteria) => Task.FromResult(new List<DeletedDataItem>());
        protected virtual Task<DataIntegrityResult> VerifyDataIntegrityAsync(string userId) => Task.FromResult(new DataIntegrityResult { IsValid = true });
        protected virtual Task<DeletionVerificationResult> VerifyDeletionCompletionAsync(string userId, List<DeletedDataItem> deletedItems) => Task.FromResult(new DeletionVerificationResult { IsComplete = true });
        protected virtual Task<List<DataRetentionPolicy>> GetRetentionPoliciesAsync(string userId) => Task.FromResult(new List<DataRetentionPolicy>());
        protected virtual bool IsDataRetentionExpired(DataRetentionPolicy policy) => policy.ExpiresAt < DateTime.UtcNow;
        protected virtual Task ArchiveOrDeleteDataAsync(string userId, DataRetentionPolicy policy) => Task.CompletedTask;
        protected virtual Task<List<PrivacyViolation>> CheckConsentViolationsAsync(Dictionary<string, object> operationData) => Task.FromResult(new List<PrivacyViolation>());
        protected virtual List<PrivacyViolation> CheckDataMinimizationViolations(Dictionary<string, object> operationData) => new();
        protected virtual Task<List<PrivacyViolation>> CheckPurposeLimitationViolationsAsync(Dictionary<string, object> operationData) => Task.FromResult(new List<PrivacyViolation>());
        protected virtual RiskLevel CalculateViolationRiskLevel(List<PrivacyViolation> violations) => violations.Any() ? RiskLevel.Medium : RiskLevel.Low;

        public void Dispose()
        {
            // リソース解放処理
        }
    }

    // サポートクラスとEnum

    public class PrivacyConfiguration
    {
        public TimeSpan ConsentRetentionPeriod { get; set; } = TimeSpan.FromYears(7);
        public TimeSpan ExportDataRetentionPeriod { get; set; } = TimeSpan.FromDays(30);
        public string PrivacyPolicyVersion { get; set; } = "1.0";
        public bool LogSensitiveData { get; set; } = false;
        public string HashSecretKey { get; set; } = GenerateSecretKey();

        private static string GenerateSecretKey()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }

    public class DataProcessingConsent
    {
        public string ConsentId { get; set; }
        public string UserId { get; set; }
        public ConsentType ConsentType { get; set; }
        public string Purpose { get; set; }
        public string LegalBasis { get; set; }
        public DateTime GrantedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public ConsentStatus Status { get; set; }
        public string Version { get; set; }
        public string RevocationReason { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
        public string DigitalSignature { get; set; }
    }

    public class PersonalDataScanResult
    {
        public string Content { get; set; }
        public string Context { get; set; }
        public DateTime ScannedAt { get; set; }
        public bool HasPersonalData { get; set; }
        public List<PersonalDataItem> DetectedItems { get; set; } = new();
        public int RiskScore { get; set; }
        public bool HasError { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class PersonalDataItem
    {
        public PersonalDataType Type { get; set; }
        public string Value { get; set; }
        public int Position { get; set; }
        public int Length { get; set; }
        public double Confidence { get; set; }
        public RiskLevel RiskLevel { get; set; }
    }

    public class DataExportResult
    {
        public bool Success { get; set; }
        public byte[] Data { get; set; }
        public DataExportFormat Format { get; set; }
        public DateTime ExportedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class UserDataExport
    {
        public string UserId { get; set; }
        public DateTime ExportedAt { get; set; }
        public Dictionary<string, object> DataCategories { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class DeletionResult
    {
        public string UserId { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public DeletionScope Scope { get; set; }
        public string Reason { get; set; }
        public bool Success { get; set; }
        public List<DeletedDataItem> DeletedItems { get; set; } = new();
        public string VerificationHash { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class DeletedDataItem
    {
        public string ItemId { get; set; }
        public string ItemType { get; set; }
        public DateTime DeletedAt { get; set; }
        public string Location { get; set; }
        public bool VerifiedDeleted { get; set; }
    }

    public class DataRetentionPolicy
    {
        public string PolicyId { get; set; }
        public string UserId { get; set; }
        public ConsentType ConsentType { get; set; }
        public string DataCategory { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public TimeSpan RetentionPeriod { get; set; }
    }

    public class PrivacyViolationReport
    {
        public string OperationType { get; set; }
        public DateTime DetectedAt { get; set; }
        public List<PrivacyViolation> Violations { get; set; } = new();
        public RiskLevel RiskLevel { get; set; }
        public bool RequiresImmediateAction { get; set; }
        public bool HasError { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class PrivacyViolation
    {
        public string ViolationType { get; set; }
        public string Description { get; set; }
        public ViolationSeverity Severity { get; set; }
        public string RecommendedAction { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
    }

    public class DataIntegrityResult
    {
        public bool IsValid { get; set; }
        public List<string> Issues { get; set; } = new();
    }

    public class DeletionVerificationResult
    {
        public bool IsComplete { get; set; }
        public List<string> RemainingItems { get; set; } = new();
    }

    public enum PersonalDataType
    {
        Email,
        PhoneNumber,
        CreditCard,
        SocialSecurityNumber,
        JapaneseMyNumber,
        BankAccount,
        IPAddress,
        BiometricData,
        HealthInformation,
        FinancialData
    }

    public enum ConsentType
    {
        DataProcessing,
        Marketing,
        Analytics,
        Cookies,
        ThirdPartySharing,
        Profiling,
        AutomatedDecisionMaking
    }

    public enum ConsentStatus
    {
        Active,
        Expired,
        Revoked,
        Pending
    }

    public enum DataExportFormat
    {
        Json,
        Xml,
        Csv,
        Pdf
    }

    public enum DeletionScope
    {
        All,
        PersonalDataOnly,
        Specific
    }

    public enum RiskLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum ViolationSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public class PrivacyComplianceException : Exception
    {
        public PrivacyComplianceException(string message) : base(message) { }
        public PrivacyComplianceException(string message, Exception innerException) : base(message, innerException) { }
    }
}