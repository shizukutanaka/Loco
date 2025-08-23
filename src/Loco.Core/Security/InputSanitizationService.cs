using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Loco.Core.Security
{
    /// <summary>
    /// 高度な入力サニタイゼーションサービス - P0セキュリティ項目
    /// 全ユーザー入力の包括的なサニタイゼーションとバリデーション
    /// </summary>
    public class InputSanitizationService
    {
        private readonly ILogger<InputSanitizationService> _logger;
        
        // SQL注入攻撃パターン
        private static readonly Regex SqlInjectionPattern = new Regex(
            @"(\b(ALTER|CREATE|DELETE|DROP|EXEC(UTE)?|INSERT( +INTO)?|MERGE|SELECT|UPDATE|UNION( +ALL)?)\b)|('|('')|;|--|\*|\*/|@@|@|\||OR\b|AND\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // XSS攻撃パターン  
        private static readonly Regex XssPattern = new Regex(
            @"<\s*script[^>]*>.*?</\s*script\s*>|javascript:|vbscript:|onload|onerror|onclick|onmouseover|onfocus|onblur|onchange|onsubmit",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

        // Path Traversal攻撃パターン
        private static readonly Regex PathTraversalPattern = new Regex(
            @"(\.\./|\.\.\\|%2e%2e%2f|%252e%252e%252f|%c0%ae|%c1%9c)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // コマンドインジェクション攻撃パターン
        private static readonly Regex CommandInjectionPattern = new Regex(
            @"(\||&|;|\$\(|\`|<|>|\{|\}|\[|\]|\(|\)|&&|\|\||cmd|powershell|bash|sh)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // LDAP注入攻撃パターン
        private static readonly Regex LdapInjectionPattern = new Regex(
            @"(\*|\(|\)|\\|/|\!|&|\|)",
            RegexOptions.Compiled);

        // NoSQL注入攻撃パターン
        private static readonly Regex NoSqlInjectionPattern = new Regex(
            @"(\$where|\$ne|\$in|\$nin|\$gt|\$gte|\$lt|\$lte|\$regex|\$exists|eval\s*\(|function\s*\()",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 危険なファイル拡張子
        private static readonly HashSet<string> DangerousExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".bat", ".cmd", ".com", ".scr", ".pif", ".vbs", ".js", ".jar", ".php", ".asp", ".aspx", ".jsp"
        };

        public InputSanitizationService(ILogger<InputSanitizationService> logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 包括的な入力サニタイゼーション - P0項目#1
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string SanitizeInput(string input, SanitizationType type = SanitizationType.General)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            try
            {
                var sanitized = input;

                // 基本的なサニタイゼーション
                sanitized = RemoveControlCharacters(sanitized);
                sanitized = NormalizeWhitespace(sanitized);

                // タイプ別サニタイゼーション
                sanitized = type switch
                {
                    SanitizationType.Html => SanitizeHtml(sanitized),
                    SanitizationType.Sql => SanitizeSql(sanitized),
                    SanitizationType.JavaScript => SanitizeJavaScript(sanitized),
                    SanitizationType.FilePath => SanitizeFilePath(sanitized),
                    SanitizationType.Email => SanitizeEmail(sanitized),
                    SanitizationType.Url => SanitizeUrl(sanitized),
                    SanitizationType.CommandLine => SanitizeCommandLine(sanitized),
                    SanitizationType.Ldap => SanitizeLdap(sanitized),
                    SanitizationType.NoSql => SanitizeNoSql(sanitized),
                    _ => SanitizeGeneral(sanitized)
                };

                return sanitized;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Input sanitization failed for type {Type}", type);
                return string.Empty; // セキュリティ優先で空文字を返す
            }
        }

        /// <summary>
        /// SQL注入攻撃対策 - P0項目#2
        /// </summary>
        public bool HasSqlInjection(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var detected = SqlInjectionPattern.IsMatch(input);
            if (detected)
            {
                _logger?.LogWarning("SQL Injection attempt detected: {Input}", SanitizeForLog(input));
            }
            
            return detected;
        }

        /// <summary>
        /// XSS攻撃対策 - P0項目#15
        /// </summary>
        public bool HasXssVulnerability(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var detected = XssPattern.IsMatch(input);
            if (detected)
            {
                _logger?.LogWarning("XSS attempt detected: {Input}", SanitizeForLog(input));
            }

            return detected;
        }

        /// <summary>
        /// Path Traversal攻撃対策 - P0項目#17
        /// </summary>
        public bool HasPathTraversal(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var detected = PathTraversalPattern.IsMatch(input);
            if (detected)
            {
                _logger?.LogWarning("Path traversal attempt detected: {Input}", SanitizeForLog(input));
            }

            return detected;
        }

        /// <summary>
        /// ファイルアップロードの安全性検証 - P0項目#16
        /// </summary>
        public ValidationResult ValidateFileUpload(string fileName, byte[] fileContent, long maxSize = 10 * 1024 * 1024)
        {
            var result = new ValidationResult();

            try
            {
                // ファイル名検証
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    result.AddError("File name is required");
                    return result;
                }

                // 危険な拡張子チェック
                var extension = System.IO.Path.GetExtension(fileName);
                if (DangerousExtensions.Contains(extension))
                {
                    result.AddError($"File extension {extension} is not allowed");
                    return result;
                }

                // Path Traversalチェック
                if (HasPathTraversal(fileName))
                {
                    result.AddError("File name contains path traversal characters");
                    return result;
                }

                // ファイルサイズチェック
                if (fileContent?.Length > maxSize)
                {
                    result.AddError($"File size exceeds maximum allowed size of {maxSize} bytes");
                    return result;
                }

                // ファイル署名（マジックナンバー）検証
                if (fileContent != null && fileContent.Length > 0)
                {
                    if (!IsValidFileSignature(fileContent, extension))
                    {
                        result.AddError("File content does not match its extension");
                        return result;
                    }
                }

                // NULL byte injection check
                if (fileName.Contains('\0'))
                {
                    result.AddError("File name contains null bytes");
                    return result;
                }

                result.IsValid = true;
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "File upload validation failed");
                result.AddError("File validation failed");
                return result;
            }
        }

        /// <summary>
        /// 高度な入力バリデーション - 複数攻撃パターンの同時検出
        /// </summary>
        public SecurityValidationResult ValidateInput(string input, InputType inputType)
        {
            var result = new SecurityValidationResult();

            if (string.IsNullOrEmpty(input))
            {
                result.IsValid = true;
                return result;
            }

            try
            {
                // 攻撃パターン検出
                var threats = new List<SecurityThreat>();

                if (HasSqlInjection(input))
                    threats.Add(new SecurityThreat(ThreatType.SqlInjection, "SQL injection pattern detected"));

                if (HasXssVulnerability(input))
                    threats.Add(new SecurityThreat(ThreatType.Xss, "XSS pattern detected"));

                if (HasPathTraversal(input))
                    threats.Add(new SecurityThreat(ThreatType.PathTraversal, "Path traversal pattern detected"));

                if (CommandInjectionPattern.IsMatch(input))
                    threats.Add(new SecurityThreat(ThreatType.CommandInjection, "Command injection pattern detected"));

                if (LdapInjectionPattern.IsMatch(input))
                    threats.Add(new SecurityThreat(ThreatType.LdapInjection, "LDAP injection pattern detected"));

                if (NoSqlInjectionPattern.IsMatch(input))
                    threats.Add(new SecurityThreat(ThreatType.NoSqlInjection, "NoSQL injection pattern detected"));

                // 長さ制限チェック
                var maxLength = GetMaxLengthForInputType(inputType);
                if (input.Length > maxLength)
                    threats.Add(new SecurityThreat(ThreatType.BufferOverflow, $"Input exceeds maximum length of {maxLength}"));

                // 文字エンコーディング検証
                if (HasEncodingAnomalies(input))
                    threats.Add(new SecurityThreat(ThreatType.EncodingAttack, "Suspicious character encoding detected"));

                result.Threats = threats;
                result.IsValid = threats.Count == 0;
                result.RiskLevel = CalculateRiskLevel(threats);

                if (!result.IsValid)
                {
                    _logger?.LogWarning("Input validation failed with {ThreatCount} threats detected", threats.Count);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Input validation error");
                result.IsValid = false;
                result.Threats = new List<SecurityThreat> 
                { 
                    new SecurityThreat(ThreatType.ValidationError, "Validation process failed") 
                };
                return result;
            }
        }

        // プライベートメソッド

        private string SanitizeGeneral(string input)
        {
            return HttpUtility.HtmlEncode(input);
        }

        private string SanitizeHtml(string input)
        {
            // HTMLタグを完全に削除
            return Regex.Replace(input, @"<[^>]*>", string.Empty);
        }

        private string SanitizeSql(string input)
        {
            return input.Replace("'", "''")
                       .Replace("--", "")
                       .Replace("/*", "")
                       .Replace("*/", "")
                       .Replace(";", "");
        }

        private string SanitizeJavaScript(string input)
        {
            return input.Replace("<script", "&lt;script")
                       .Replace("javascript:", "")
                       .Replace("vbscript:", "")
                       .Replace("eval(", "")
                       .Replace("alert(", "");
        }

        private string SanitizeFilePath(string input)
        {
            return input.Replace("../", "")
                       .Replace("..\\", "")
                       .Replace("\0", "");
        }

        private string SanitizeEmail(string input)
        {
            // 基本的なメールアドレス文字のみ許可
            return Regex.Replace(input, @"[^a-zA-Z0-9@._-]", "");
        }

        private string SanitizeUrl(string input)
        {
            // URLエンコード
            return Uri.EscapeUriString(input);
        }

        private string SanitizeCommandLine(string input)
        {
            return input.Replace("|", "")
                       .Replace("&", "")
                       .Replace(";", "")
                       .Replace("`", "")
                       .Replace("$(", "")
                       .Replace("${", "");
        }

        private string SanitizeLdap(string input)
        {
            return input.Replace("*", "\\2a")
                       .Replace("(", "\\28")
                       .Replace(")", "\\29")
                       .Replace("\\", "\\5c")
                       .Replace("/", "\\2f")
                       .Replace("!", "\\21");
        }

        private string SanitizeNoSql(string input)
        {
            return input.Replace("$where", "")
                       .Replace("$ne", "")
                       .Replace("$in", "")
                       .Replace("$regex", "")
                       .Replace("eval(", "")
                       .Replace("function(", "");
        }

        private string RemoveControlCharacters(string input)
        {
            return new string(input.Where(c => !char.IsControl(c) || c == '\t' || c == '\r' || c == '\n').ToArray());
        }

        private string NormalizeWhitespace(string input)
        {
            return Regex.Replace(input, @"\s+", " ").Trim();
        }

        private bool IsValidFileSignature(byte[] fileContent, string extension)
        {
            if (fileContent == null || fileContent.Length < 4)
                return true; // 小さなファイルはスキップ

            var signature = BitConverter.ToString(fileContent.Take(4).ToArray()).Replace("-", "");
            
            return extension.ToLowerInvariant() switch
            {
                ".pdf" => signature.StartsWith("25504446"),
                ".jpg" or ".jpeg" => signature.StartsWith("FFD8FF"),
                ".png" => signature.StartsWith("89504E47"),
                ".gif" => signature.StartsWith("47494638"),
                ".zip" => signature.StartsWith("504B0304"),
                ".docx" => signature.StartsWith("504B0304"),
                ".xlsx" => signature.StartsWith("504B0304"),
                _ => true // 不明な拡張子はスキップ
            };
        }

        private bool HasEncodingAnomalies(string input)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var decoded = Encoding.UTF8.GetString(bytes);
                return !string.Equals(input, decoded, StringComparison.Ordinal);
            }
            catch
            {
                return true; // エンコーディングエラーは異常として扱う
            }
        }

        private int GetMaxLengthForInputType(InputType inputType)
        {
            return inputType switch
            {
                InputType.Username => 255,
                InputType.Email => 320,
                InputType.Password => 128,
                InputType.PhoneNumber => 20,
                InputType.Address => 500,
                InputType.Description => 2000,
                InputType.Comment => 1000,
                InputType.SearchQuery => 200,
                InputType.FileName => 255,
                InputType.Url => 2048,
                _ => 1000
            };
        }

        private RiskLevel CalculateRiskLevel(List<SecurityThreat> threats)
        {
            if (threats.Count == 0)
                return RiskLevel.None;

            var highRiskThreats = threats.Where(t => t.Type == ThreatType.SqlInjection || 
                                                   t.Type == ThreatType.CommandInjection).ToList();

            if (highRiskThreats.Any())
                return RiskLevel.Critical;

            var mediumRiskThreats = threats.Where(t => t.Type == ThreatType.Xss || 
                                                     t.Type == ThreatType.PathTraversal).ToList();

            if (mediumRiskThreats.Any())
                return RiskLevel.High;

            return threats.Count > 2 ? RiskLevel.Medium : RiskLevel.Low;
        }

        private string SanitizeForLog(string input)
        {
            return input?.Length > 100 ? input.Substring(0, 100) + "..." : input ?? "";
        }
    }

    // 関連するenumとクラス
    public enum SanitizationType
    {
        General,
        Html,
        Sql,
        JavaScript,
        FilePath,
        Email,
        Url,
        CommandLine,
        Ldap,
        NoSql
    }

    public enum InputType
    {
        General,
        Username,
        Email,
        Password,
        PhoneNumber,
        Address,
        Description,
        Comment,
        SearchQuery,
        FileName,
        Url
    }

    public enum ThreatType
    {
        SqlInjection,
        Xss,
        PathTraversal,
        CommandInjection,
        LdapInjection,
        NoSqlInjection,
        BufferOverflow,
        EncodingAttack,
        ValidationError
    }

    public enum RiskLevel
    {
        None,
        Low,
        Medium,
        High,
        Critical
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();

        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }
    }

    public class SecurityValidationResult
    {
        public bool IsValid { get; set; }
        public List<SecurityThreat> Threats { get; set; } = new List<SecurityThreat>();
        public RiskLevel RiskLevel { get; set; } = RiskLevel.None;
    }

    public class SecurityThreat
    {
        public ThreatType Type { get; set; }
        public string Description { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

        public SecurityThreat(ThreatType type, string description)
        {
            Type = type;
            Description = description;
        }
    }
}