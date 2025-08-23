using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Security
{
    public interface IAdvancedSecurityService
    {
        string SanitizeInput(string input, InputType type = InputType.General);
        bool ValidateInput(string input, InputType type);
        string HashPassword(string password, out string salt);
        bool VerifyPassword(string password, string hash, string salt);
        string GenerateSecureToken(int length = 32);
        string EncryptData(string plainText, string key);
        string DecryptData(string cipherText, string key);
        bool ValidateCsrfToken(string token, string sessionToken);
        string GenerateCsrfToken(string sessionId);
        void LogSecurityEvent(SecurityEventType eventType, string details, string userId = null);
        Task<bool> CheckForSqlInjection(string input);
        Task<bool> CheckForXss(string input);
        Task<bool> CheckForPathTraversal(string input);
        string GenerateApiKey();
        bool ValidateApiKey(string apiKey);
    }

    public class AdvancedSecurityService : IAdvancedSecurityService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdvancedSecurityService> _logger;
        private readonly HashSet<string> _blacklistedPatterns;
        private readonly Dictionary<string, Regex> _validationPatterns;
        private readonly int _pbkdf2Iterations;

        public AdvancedSecurityService(
            IConfiguration configuration,
            ILogger<AdvancedSecurityService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _pbkdf2Iterations = _configuration.GetValue<int>("Security:PBKDF2Iterations", 100000);
            
            InitializeBlacklistPatterns();
            InitializeValidationPatterns();
        }

        private void InitializeBlacklistPatterns()
        {
            _blacklistedPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // SQL Injection patterns
                "select", "insert", "update", "delete", "drop", "create", "alter", "exec",
                "execute", "union", "having", "group by", "order by", "xp_cmdshell",
                "sp_executesql", "waitfor", "delay", "benchmark", "sleep",
                
                // XSS patterns
                "<script", "</script", "javascript:", "onerror", "onload", "onclick",
                "onmouseover", "onfocus", "onblur", "alert(", "prompt(", "confirm(",
                "document.cookie", "window.location", "eval(", "expression(",
                
                // Path traversal patterns
                "../", "..\\", "%2e%2e/", "%2e%2e\\", "..%2f", "..%5c",
                
                // Command injection patterns
                ";", "|", "&", "&&", "||", "`", "$(", "${", "%0a", "%0d",
                
                // LDAP injection patterns
                "*", "(", ")", "\\", "NUL"
            };
        }

        private void InitializeValidationPatterns()
        {
            _validationPatterns = new Dictionary<string, Regex>
            {
                ["email"] = new Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled),
                ["url"] = new Regex(@"^https?://[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}(?:/[^?#]*)?(?:\?[^#]*)?(?:#.*)?$", RegexOptions.Compiled),
                ["alphanumeric"] = new Regex(@"^[a-zA-Z0-9]+$", RegexOptions.Compiled),
                ["numeric"] = new Regex(@"^[0-9]+$", RegexOptions.Compiled),
                ["phone"] = new Regex(@"^[\+]?[(]?[0-9]{1,4}[)]?[-\s\.]?[(]?[0-9]{1,4}[)]?[-\s\.]?[0-9]{1,5}[-\s\.]?[0-9]{1,5}$", RegexOptions.Compiled),
                ["creditcard"] = new Regex(@"^[0-9]{13,19}$", RegexOptions.Compiled),
                ["ipaddress"] = new Regex(@"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$", RegexOptions.Compiled),
                ["username"] = new Regex(@"^[a-zA-Z0-9._-]{3,20}$", RegexOptions.Compiled),
                ["password"] = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$", RegexOptions.Compiled)
            };
        }

        public string SanitizeInput(string input, InputType type = InputType.General)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var sanitized = input.Trim();

            // Remove null bytes
            sanitized = sanitized.Replace("\0", "");

            // HTML encode
            sanitized = System.Net.WebUtility.HtmlEncode(sanitized);

            // Remove control characters
            sanitized = Regex.Replace(sanitized, @"[\x00-\x1F\x7F]", "");

            switch (type)
            {
                case InputType.Sql:
                    sanitized = SanitizeSql(sanitized);
                    break;
                case InputType.Html:
                    sanitized = SanitizeHtml(sanitized);
                    break;
                case InputType.FilePath:
                    sanitized = SanitizeFilePath(sanitized);
                    break;
                case InputType.Url:
                    sanitized = SanitizeUrl(sanitized);
                    break;
            }

            LogSecurityEvent(SecurityEventType.InputSanitized, $"Type: {type}, Length: {input.Length} -> {sanitized.Length}");
            
            return sanitized;
        }

        private string SanitizeSql(string input)
        {
            // Escape single quotes
            var sanitized = input.Replace("'", "''");
            
            // Remove SQL comments
            sanitized = Regex.Replace(sanitized, @"--.*$", "", RegexOptions.Multiline);
            sanitized = Regex.Replace(sanitized, @"/\*.*?\*/", "", RegexOptions.Singleline);
            
            // Remove dangerous SQL keywords
            foreach (var pattern in _blacklistedPatterns.Where(p => IsSqlKeyword(p)))
            {
                sanitized = Regex.Replace(sanitized, $@"\b{pattern}\b", "", RegexOptions.IgnoreCase);
            }
            
            return sanitized;
        }

        private string SanitizeHtml(string input)
        {
            // Remove all HTML tags
            var sanitized = Regex.Replace(input, @"<[^>]*>", "");
            
            // Remove JavaScript event handlers
            sanitized = Regex.Replace(sanitized, @"on\w+\s*=", "", RegexOptions.IgnoreCase);
            
            // Remove JavaScript protocols
            sanitized = Regex.Replace(sanitized, @"javascript:|vbscript:|data:", "", RegexOptions.IgnoreCase);
            
            return sanitized;
        }

        private string SanitizeFilePath(string input)
        {
            // Remove path traversal sequences
            var sanitized = input.Replace("..", "");
            sanitized = sanitized.Replace("~", "");
            
            // Remove invalid path characters
            var invalidChars = System.IO.Path.GetInvalidPathChars();
            foreach (var c in invalidChars)
            {
                sanitized = sanitized.Replace(c.ToString(), "");
            }
            
            // Normalize path separators
            sanitized = sanitized.Replace('/', System.IO.Path.DirectorySeparatorChar);
            sanitized = sanitized.Replace('\\', System.IO.Path.DirectorySeparatorChar);
            
            return sanitized;
        }

        private string SanitizeUrl(string input)
        {
            try
            {
                var uri = new Uri(input);
                
                // Only allow HTTP and HTTPS
                if (uri.Scheme != "http" && uri.Scheme != "https")
                {
                    return "";
                }
                
                return uri.ToString();
            }
            catch
            {
                return "";
            }
        }

        public bool ValidateInput(string input, InputType type)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var patternKey = type switch
            {
                InputType.Email => "email",
                InputType.Url => "url",
                InputType.Phone => "phone",
                InputType.CreditCard => "creditcard",
                InputType.IpAddress => "ipaddress",
                InputType.Username => "username",
                InputType.Password => "password",
                _ => null
            };

            if (patternKey != null && _validationPatterns.TryGetValue(patternKey, out var pattern))
            {
                return pattern.IsMatch(input);
            }

            // Check for blacklisted patterns
            var lowerInput = input.ToLower();
            foreach (var blacklisted in _blacklistedPatterns)
            {
                if (lowerInput.Contains(blacklisted))
                {
                    LogSecurityEvent(SecurityEventType.ValidationFailed, $"Blacklisted pattern detected: {blacklisted}");
                    return false;
                }
            }

            return true;
        }

        public string HashPassword(string password, out string salt)
        {
            // Generate salt
            var saltBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            salt = Convert.ToBase64String(saltBytes);

            // Hash password with PBKDF2
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, _pbkdf2Iterations, HashAlgorithmName.SHA256))
            {
                var hash = pbkdf2.GetBytes(32);
                return Convert.ToBase64String(hash);
            }
        }

        public bool VerifyPassword(string password, string hash, string salt)
        {
            try
            {
                var saltBytes = Convert.FromBase64String(salt);
                var hashBytes = Convert.FromBase64String(hash);

                using (var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, _pbkdf2Iterations, HashAlgorithmName.SHA256))
                {
                    var testHash = pbkdf2.GetBytes(32);
                    return testHash.SequenceEqual(hashBytes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying password");
                return false;
            }
        }

        public string GenerateSecureToken(int length = 32)
        {
            var tokenBytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }
            return Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        public string EncryptData(string plainText, string key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = DeriveKey(key);
                aes.GenerateIV();

                using (var encryptor = aes.CreateEncryptor())
                {
                    var plainBytes = Encoding.UTF8.GetBytes(plainText);
                    var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                    
                    var result = new byte[aes.IV.Length + cipherBytes.Length];
                    Array.Copy(aes.IV, 0, result, 0, aes.IV.Length);
                    Array.Copy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
                    
                    return Convert.ToBase64String(result);
                }
            }
        }

        public string DecryptData(string cipherText, string key)
        {
            var cipherBytes = Convert.FromBase64String(cipherText);
            
            using (var aes = Aes.Create())
            {
                aes.Key = DeriveKey(key);
                
                var iv = new byte[aes.IV.Length];
                var cipher = new byte[cipherBytes.Length - iv.Length];
                
                Array.Copy(cipherBytes, 0, iv, 0, iv.Length);
                Array.Copy(cipherBytes, iv.Length, cipher, 0, cipher.Length);
                
                aes.IV = iv;
                
                using (var decryptor = aes.CreateDecryptor())
                {
                    var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                    return Encoding.UTF8.GetString(plainBytes);
                }
            }
        }

        private byte[] DeriveKey(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            }
        }

        public bool ValidateCsrfToken(string token, string sessionToken)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(sessionToken))
                return false;

            try
            {
                using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sessionToken)))
                {
                    var expectedToken = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(sessionToken)));
                    return token == expectedToken;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating CSRF token");
                return false;
            }
        }

        public string GenerateCsrfToken(string sessionId)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sessionId)))
            {
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(sessionId)));
            }
        }

        public void LogSecurityEvent(SecurityEventType eventType, string details, string userId = null)
        {
            var logEntry = new
            {
                Timestamp = DateTime.UtcNow,
                EventType = eventType.ToString(),
                Details = details,
                UserId = userId,
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent()
            };

            _logger.LogInformation("Security Event: {SecurityEvent}", System.Text.Json.JsonSerializer.Serialize(logEntry));
        }

        public async Task<bool> CheckForSqlInjection(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var patterns = new[]
            {
                @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|EXECUTE|UNION|HAVING)\b)",
                @"(--|\*/|/\*)",
                @"(xp_cmdshell|sp_executesql)",
                @"(WAITFOR\s+DELAY|BENCHMARK|SLEEP)",
                @"('|""|;|\\x[0-9a-fA-F]{{2}})"
            };

            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                {
                    LogSecurityEvent(SecurityEventType.SqlInjectionAttempt, $"Pattern matched: {pattern}");
                    return true;
                }
            }

            return await Task.FromResult(false);
        }

        public async Task<bool> CheckForXss(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var patterns = new[]
            {
                @"<script[^>]*>.*?</script>",
                @"javascript:",
                @"on\w+\s*=",
                @"<iframe[^>]*>",
                @"<object[^>]*>",
                @"<embed[^>]*>",
                @"<applet[^>]*>",
                @"eval\s*\(",
                @"expression\s*\(",
                @"vbscript:",
                @"data:text/html"
            };

            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                {
                    LogSecurityEvent(SecurityEventType.XssAttempt, $"Pattern matched: {pattern}");
                    return true;
                }
            }

            return await Task.FromResult(false);
        }

        public async Task<bool> CheckForPathTraversal(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var patterns = new[]
            {
                @"\.\./",
                @"\.\.\\",
                @"%2e%2e/",
                @"%2e%2e\\",
                @"\.\.%2f",
                @"\.\.%5c",
                @"~/"
            };

            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                {
                    LogSecurityEvent(SecurityEventType.PathTraversalAttempt, $"Pattern matched: {pattern}");
                    return true;
                }
            }

            return await Task.FromResult(false);
        }

        public string GenerateApiKey()
        {
            var keyBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(keyBytes);
            }
            
            var apiKey = $"loco_{Convert.ToBase64String(keyBytes).Replace("+", "").Replace("/", "").Replace("=", "")}";
            
            LogSecurityEvent(SecurityEventType.ApiKeyGenerated, $"New API key generated");
            
            return apiKey;
        }

        public bool ValidateApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;

            // Check format
            if (!apiKey.StartsWith("loco_") || apiKey.Length < 40)
                return false;

            // Additional validation logic here (e.g., check against database)
            
            return true;
        }

        private bool IsSqlKeyword(string word)
        {
            var sqlKeywords = new[] { "select", "insert", "update", "delete", "drop", "create", "alter", "exec", "execute", "union", "having" };
            return sqlKeywords.Contains(word.ToLower());
        }

        private string GetClientIpAddress()
        {
            // Implementation would get actual client IP from HTTP context
            return "127.0.0.1";
        }

        private string GetUserAgent()
        {
            // Implementation would get actual user agent from HTTP context
            return "Unknown";
        }
    }

    public enum InputType
    {
        General,
        Email,
        Url,
        Phone,
        CreditCard,
        IpAddress,
        Username,
        Password,
        Sql,
        Html,
        FilePath
    }

    public enum SecurityEventType
    {
        InputSanitized,
        ValidationFailed,
        SqlInjectionAttempt,
        XssAttempt,
        PathTraversalAttempt,
        ApiKeyGenerated,
        LoginAttempt,
        LoginSuccess,
        LoginFailed,
        PasswordChanged,
        AccountLocked,
        UnauthorizedAccess,
        CsrfAttempt,
        BruteForceAttempt
    }
}