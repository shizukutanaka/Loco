using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Validation
{
    public interface IInputValidationService
    {
        bool ValidateEmail(string email);
        bool ValidatePhoneNumber(string phoneNumber);
        bool ValidateUrl(string url);
        bool ValidateCreditCard(string cardNumber);
        bool ValidateIPAddress(string ipAddress);
        bool ValidatePassword(string password, PasswordPolicy policy = null);
        string SanitizeHtml(string html);
        string SanitizeSql(string input);
        string SanitizeFileName(string fileName);
        string SanitizeInput(string input, SanitizationOptions options = null);
        ValidationResult ValidateModel<T>(T model) where T : class;
        Task<ValidationResult> ValidateAsync<T>(T model) where T : class;
    }

    public class PasswordPolicy
    {
        public int MinLength { get; set; } = 12;
        public int MaxLength { get; set; } = 128;
        public bool RequireUppercase { get; set; } = true;
        public bool RequireLowercase { get; set; } = true;
        public bool RequireDigit { get; set; } = true;
        public bool RequireSpecialChar { get; set; } = true;
        public int MinUniqueChars { get; set; } = 8;
        public bool PreventCommonPasswords { get; set; } = true;
        public bool PreventUserInfo { get; set; } = true;
        public int PasswordHistoryCount { get; set; } = 5;
    }

    public class SanitizationOptions
    {
        public bool RemoveHtml { get; set; } = true;
        public bool RemoveSql { get; set; } = true;
        public bool RemoveScript { get; set; } = true;
        public bool TrimWhitespace { get; set; } = true;
        public bool NormalizeWhitespace { get; set; } = true;
        public int? MaxLength { get; set; }
        public bool AllowedCharactersOnly { get; set; } = false;
        public string AllowedCharacters { get; set; } = @"[a-zA-Z0-9\s\-_.,!?@#$%^&*()+=]";
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationError> Errors { get; set; } = new List<ValidationError>();
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    public class ValidationError
    {
        public string Field { get; set; }
        public string Message { get; set; }
        public string Code { get; set; }
        public ValidationSeverity Severity { get; set; }
    }

    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public class InputValidationService : IInputValidationService
    {
        private readonly ILogger<InputValidationService> _logger;
        private readonly HashSet<string> _commonPasswords;
        private readonly Dictionary<string, Regex> _validationPatterns;

        public InputValidationService(ILogger<InputValidationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _commonPasswords = LoadCommonPasswords();
            _validationPatterns = InitializeValidationPatterns();
        }

        public bool ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // RFC 5322 compliant email validation
                var pattern = @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$";
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                
                if (!regex.IsMatch(email))
                    return false;

                // Additional checks
                var parts = email.Split('@');
                if (parts.Length != 2)
                    return false;

                // Check for consecutive dots
                if (email.Contains(".."))
                    return false;

                // Check domain has at least one dot
                if (!parts[1].Contains("."))
                    return false;

                // Check TLD length (2-63 characters)
                var tld = parts[1].Split('.').Last();
                if (tld.Length < 2 || tld.Length > 63)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating email");
                return false;
            }
        }

        public bool ValidatePhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return false;

            // Remove common formatting characters
            var cleaned = Regex.Replace(phoneNumber, @"[\s\-\(\)\.]", "");
            
            // Check if it starts with + for international
            if (cleaned.StartsWith("+"))
            {
                cleaned = cleaned.Substring(1);
            }

            // Check if all remaining characters are digits
            if (!cleaned.All(char.IsDigit))
                return false;

            // Check length (typically 7-15 digits)
            if (cleaned.Length < 7 || cleaned.Length > 15)
                return false;

            return true;
        }

        public bool ValidateUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            try
            {
                var uri = new Uri(url);
                
                // Check for valid scheme
                if (uri.Scheme != Uri.UriSchemeHttp && 
                    uri.Scheme != Uri.UriSchemeHttps &&
                    uri.Scheme != Uri.UriSchemeFtp)
                    return false;

                // Check for valid host
                if (string.IsNullOrWhiteSpace(uri.Host))
                    return false;

                // Additional security checks
                if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.StartsWith("127.") ||
                    uri.Host.StartsWith("192.168.") ||
                    uri.Host.StartsWith("10.") ||
                    uri.Host.Equals("0.0.0.0"))
                {
                    _logger.LogWarning($"Potentially dangerous URL detected: {url}");
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ValidateCreditCard(string cardNumber)
        {
            if (string.IsNullOrWhiteSpace(cardNumber))
                return false;

            // Remove spaces and dashes
            cardNumber = Regex.Replace(cardNumber, @"[\s\-]", "");

            // Check if all characters are digits
            if (!cardNumber.All(char.IsDigit))
                return false;

            // Check length (typically 13-19 digits)
            if (cardNumber.Length < 13 || cardNumber.Length > 19)
                return false;

            // Luhn algorithm validation
            int sum = 0;
            bool alternate = false;
            
            for (int i = cardNumber.Length - 1; i >= 0; i--)
            {
                int digit = int.Parse(cardNumber[i].ToString());
                
                if (alternate)
                {
                    digit *= 2;
                    if (digit > 9)
                        digit -= 9;
                }
                
                sum += digit;
                alternate = !alternate;
            }
            
            return sum % 10 == 0;
        }

        public bool ValidateIPAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;

            // IPv4 validation
            var ipv4Pattern = @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
            if (Regex.IsMatch(ipAddress, ipv4Pattern))
                return true;

            // IPv6 validation (simplified)
            var ipv6Pattern = @"^(([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:))$";
            return Regex.IsMatch(ipAddress, ipv6Pattern);
        }

        public bool ValidatePassword(string password, PasswordPolicy policy = null)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            policy ??= new PasswordPolicy();

            // Length check
            if (password.Length < policy.MinLength || password.Length > policy.MaxLength)
                return false;

            // Uppercase check
            if (policy.RequireUppercase && !password.Any(char.IsUpper))
                return false;

            // Lowercase check
            if (policy.RequireLowercase && !password.Any(char.IsLower))
                return false;

            // Digit check
            if (policy.RequireDigit && !password.Any(char.IsDigit))
                return false;

            // Special character check
            if (policy.RequireSpecialChar)
            {
                var specialChars = @"!@#$%^&*()_+-=[]{}|;':""<>?,./`~";
                if (!password.Any(c => specialChars.Contains(c)))
                    return false;
            }

            // Unique characters check
            if (password.Distinct().Count() < policy.MinUniqueChars)
                return false;

            // Common passwords check
            if (policy.PreventCommonPasswords && _commonPasswords.Contains(password.ToLower()))
                return false;

            // No repeating characters (e.g., "aaa", "111")
            for (int i = 0; i < password.Length - 2; i++)
            {
                if (password[i] == password[i + 1] && password[i] == password[i + 2])
                    return false;
            }

            // No sequential characters (e.g., "abc", "123")
            for (int i = 0; i < password.Length - 2; i++)
            {
                if ((password[i + 1] == password[i] + 1) && (password[i + 2] == password[i] + 2))
                    return false;
            }

            return true;
        }

        public string SanitizeHtml(string html)
        {
            if (string.IsNullOrEmpty(html))
                return html;

            // Remove script tags
            html = Regex.Replace(html, @"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", "", RegexOptions.IgnoreCase);
            
            // Remove style tags
            html = Regex.Replace(html, @"<style\b[^<]*(?:(?!<\/style>)<[^<]*)*<\/style>", "", RegexOptions.IgnoreCase);
            
            // Remove iframe tags
            html = Regex.Replace(html, @"<iframe\b[^<]*(?:(?!<\/iframe>)<[^<]*)*<\/iframe>", "", RegexOptions.IgnoreCase);
            
            // Remove object tags
            html = Regex.Replace(html, @"<object\b[^<]*(?:(?!<\/object>)<[^<]*)*<\/object>", "", RegexOptions.IgnoreCase);
            
            // Remove embed tags
            html = Regex.Replace(html, @"<embed\b[^<]*(?:(?!<\/embed>)<[^<]*)*<\/embed>", "", RegexOptions.IgnoreCase);
            
            // Remove event handlers
            html = Regex.Replace(html, @"\s*on\w+\s*=\s*""[^""]*""", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"\s*on\w+\s*=\s*'[^']*'", "", RegexOptions.IgnoreCase);
            
            // Remove javascript: protocol
            html = Regex.Replace(html, @"javascript\s*:", "", RegexOptions.IgnoreCase);
            
            // Remove data: protocol (can be used for XSS)
            html = Regex.Replace(html, @"data\s*:.*?base64", "", RegexOptions.IgnoreCase);
            
            return html;
        }

        public string SanitizeSql(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Remove or escape dangerous SQL characters
            input = input.Replace("'", "''");
            input = input.Replace(";", "");
            input = input.Replace("--", "");
            input = input.Replace("/*", "");
            input = input.Replace("*/", "");
            input = input.Replace("xp_", "");
            input = input.Replace("sp_", "");
            
            // Remove dangerous SQL keywords (case-insensitive)
            var dangerousKeywords = new[] { "DROP", "DELETE", "INSERT", "UPDATE", "EXEC", "EXECUTE", "CREATE", "ALTER", "GRANT", "REVOKE" };
            foreach (var keyword in dangerousKeywords)
            {
                input = Regex.Replace(input, $@"\b{keyword}\b", "", RegexOptions.IgnoreCase);
            }
            
            return input;
        }

        public string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return fileName;

            // Remove path characters
            fileName = System.IO.Path.GetFileName(fileName);
            
            // Remove invalid characters
            var invalidChars = System.IO.Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                fileName = fileName.Replace(c.ToString(), "");
            }
            
            // Remove additional dangerous characters
            fileName = fileName.Replace("..", "");
            fileName = fileName.Replace("/", "");
            fileName = fileName.Replace("\\", "");
            
            // Limit length
            if (fileName.Length > 255)
                fileName = fileName.Substring(0, 255);
            
            // Ensure it doesn't start or end with a dot or space
            fileName = fileName.Trim('.', ' ');
            
            return fileName;
        }

        public string SanitizeInput(string input, SanitizationOptions options = null)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            options ??= new SanitizationOptions();

            // Trim whitespace
            if (options.TrimWhitespace)
                input = input.Trim();

            // Normalize whitespace
            if (options.NormalizeWhitespace)
                input = Regex.Replace(input, @"\s+", " ");

            // Remove HTML
            if (options.RemoveHtml)
                input = SanitizeHtml(input);

            // Remove SQL
            if (options.RemoveSql)
                input = SanitizeSql(input);

            // Remove script tags
            if (options.RemoveScript)
            {
                input = Regex.Replace(input, @"<script.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                input = Regex.Replace(input, @"javascript:", "", RegexOptions.IgnoreCase);
            }

            // Apply allowed characters filter
            if (options.AllowedCharactersOnly && !string.IsNullOrEmpty(options.AllowedCharacters))
            {
                var pattern = $"[^{options.AllowedCharacters}]";
                input = Regex.Replace(input, pattern, "");
            }

            // Apply max length
            if (options.MaxLength.HasValue && input.Length > options.MaxLength.Value)
                input = input.Substring(0, options.MaxLength.Value);

            return input;
        }

        public ValidationResult ValidateModel<T>(T model) where T : class
        {
            var result = new ValidationResult { IsValid = true };

            if (model == null)
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Field = "model",
                    Message = "Model cannot be null",
                    Code = "NULL_MODEL",
                    Severity = ValidationSeverity.Error
                });
                return result;
            }

            // Use reflection to validate properties
            var properties = typeof(T).GetProperties();
            foreach (var property in properties)
            {
                var value = property.GetValue(model);
                var attributes = property.GetCustomAttributes(true);

                // Check for required attribute
                if (attributes.Any(a => a.GetType().Name == "RequiredAttribute") && value == null)
                {
                    result.IsValid = false;
                    result.Errors.Add(new ValidationError
                    {
                        Field = property.Name,
                        Message = $"{property.Name} is required",
                        Code = "REQUIRED_FIELD",
                        Severity = ValidationSeverity.Error
                    });
                }

                // Validate string properties
                if (property.PropertyType == typeof(string) && value != null)
                {
                    var stringValue = value.ToString();
                    
                    // Check for email
                    if (property.Name.ToLower().Contains("email") && !ValidateEmail(stringValue))
                    {
                        result.IsValid = false;
                        result.Errors.Add(new ValidationError
                        {
                            Field = property.Name,
                            Message = $"{property.Name} is not a valid email",
                            Code = "INVALID_EMAIL",
                            Severity = ValidationSeverity.Error
                        });
                    }

                    // Check for phone
                    if (property.Name.ToLower().Contains("phone") && !ValidatePhoneNumber(stringValue))
                    {
                        result.IsValid = false;
                        result.Errors.Add(new ValidationError
                        {
                            Field = property.Name,
                            Message = $"{property.Name} is not a valid phone number",
                            Code = "INVALID_PHONE",
                            Severity = ValidationSeverity.Error
                        });
                    }

                    // Check for URL
                    if (property.Name.ToLower().Contains("url") && !ValidateUrl(stringValue))
                    {
                        result.IsValid = false;
                        result.Errors.Add(new ValidationError
                        {
                            Field = property.Name,
                            Message = $"{property.Name} is not a valid URL",
                            Code = "INVALID_URL",
                            Severity = ValidationSeverity.Error
                        });
                    }
                }
            }

            return result;
        }

        public async Task<ValidationResult> ValidateAsync<T>(T model) where T : class
        {
            return await Task.Run(() => ValidateModel(model));
        }

        private HashSet<string> LoadCommonPasswords()
        {
            // This would typically load from a file or database
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "password", "123456", "password123", "12345678", "qwerty", "abc123",
                "123456789", "password1", "12345", "1234567", "letmein", "qwertyuiop",
                "123", "monkey", "dragon", "111111", "baseball", "iloveyou", "trustno1",
                "1234567890", "sunshine", "princess", "football", "welcome", "shadow",
                "superman", "michael", "ninja", "mustang", "admin", "administrator"
            };
        }

        private Dictionary<string, Regex> InitializeValidationPatterns()
        {
            return new Dictionary<string, Regex>
            {
                ["Email"] = new Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled),
                ["Phone"] = new Regex(@"^[\+]?[(]?[0-9]{3}[)]?[-\s\.]?[0-9]{3}[-\s\.]?[0-9]{4,6}$", RegexOptions.Compiled),
                ["URL"] = new Regex(@"^(https?|ftp)://[^\s/$.?#].[^\s]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                ["IPv4"] = new Regex(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$", RegexOptions.Compiled),
                ["AlphaNumeric"] = new Regex(@"^[a-zA-Z0-9]+$", RegexOptions.Compiled),
                ["Alpha"] = new Regex(@"^[a-zA-Z]+$", RegexOptions.Compiled),
                ["Numeric"] = new Regex(@"^[0-9]+$", RegexOptions.Compiled)
            };
        }
    }
}