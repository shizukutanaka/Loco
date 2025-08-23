using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Validation
{
    public class Validator
    {
        private readonly ILogger<Validator> _logger;
        private readonly List<IValidationRule> _customRules = new();

        public Validator(ILogger<Validator> logger)
        {
            _logger = logger;
        }

        public void RegisterRule(IValidationRule rule)
        {
            _customRules.Add(rule);
        }

        public ValidationResult Validate<T>(T model) where T : class
        {
            var result = new ValidationResult();

            if (model == null)
            {
                result.AddError("Model", "Model cannot be null");
                return result;
            }

            // Data Annotations validation
            var context = new ValidationContext(model);
            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            
            if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                model, context, validationResults, true))
            {
                foreach (var validationResult in validationResults)
                {
                    result.AddError(
                        validationResult.MemberNames.FirstOrDefault() ?? "Unknown",
                        validationResult.ErrorMessage);
                }
            }

            // Custom rules validation
            foreach (var rule in _customRules)
            {
                if (rule.CanValidate(model.GetType()))
                {
                    var ruleResult = rule.Validate(model);
                    if (!ruleResult.IsValid)
                    {
                        result.Merge(ruleResult);
                    }
                }
            }

            // Property-specific validation
            ValidateProperties(model, result);

            return result;
        }

        public async Task<ValidationResult> ValidateAsync<T>(T model) where T : class
        {
            var result = Validate(model);

            // Async custom rules
            foreach (var rule in _customRules.OfType<IAsyncValidationRule>())
            {
                if (rule.CanValidate(model.GetType()))
                {
                    var ruleResult = await rule.ValidateAsync(model);
                    if (!ruleResult.IsValid)
                    {
                        result.Merge(ruleResult);
                    }
                }
            }

            return result;
        }

        private void ValidateProperties<T>(T model, ValidationResult result) where T : class
        {
            var properties = typeof(T).GetProperties();

            foreach (var property in properties)
            {
                var value = property.GetValue(model);
                var propertyName = property.Name;

                // String validations
                if (property.PropertyType == typeof(string) && value is string stringValue)
                {
                    ValidateString(propertyName, stringValue, property, result);
                }

                // Numeric validations
                if (IsNumericType(property.PropertyType) && value != null)
                {
                    ValidateNumeric(propertyName, value, property, result);
                }

                // Collection validations
                if (value is System.Collections.IEnumerable enumerable && 
                    !(value is string))
                {
                    ValidateCollection(propertyName, enumerable, property, result);
                }
            }
        }

        private void ValidateString(string propertyName, string value, 
            System.Reflection.PropertyInfo property, ValidationResult result)
        {
            // Check for SQL injection patterns
            if (ContainsSqlInjection(value))
            {
                result.AddError(propertyName, "Value contains potentially dangerous SQL patterns");
            }

            // Check for XSS patterns
            if (ContainsXss(value))
            {
                result.AddError(propertyName, "Value contains potentially dangerous script patterns");
            }

            // Email validation
            var emailAttr = property.GetCustomAttributes(typeof(EmailAddressAttribute), true)
                .FirstOrDefault();
            if (emailAttr != null && !string.IsNullOrEmpty(value) && !IsValidEmail(value))
            {
                result.AddError(propertyName, "Invalid email format");
            }

            // URL validation
            var urlAttr = property.GetCustomAttributes(typeof(UrlAttribute), true)
                .FirstOrDefault();
            if (urlAttr != null && !string.IsNullOrEmpty(value) && !IsValidUrl(value))
            {
                result.AddError(propertyName, "Invalid URL format");
            }
        }

        private void ValidateNumeric(string propertyName, object value,
            System.Reflection.PropertyInfo property, ValidationResult result)
        {
            var rangeAttr = property.GetCustomAttributes(typeof(RangeAttribute), true)
                .FirstOrDefault() as RangeAttribute;
            
            if (rangeAttr != null)
            {
                var comparable = value as IComparable;
                if (comparable != null)
                {
                    if (comparable.CompareTo(rangeAttr.Minimum) < 0 ||
                        comparable.CompareTo(rangeAttr.Maximum) > 0)
                    {
                        result.AddError(propertyName, 
                            $"Value must be between {rangeAttr.Minimum} and {rangeAttr.Maximum}");
                    }
                }
            }
        }

        private void ValidateCollection(string propertyName, 
            System.Collections.IEnumerable collection,
            System.Reflection.PropertyInfo property, ValidationResult result)
        {
            var count = 0;
            foreach (var _ in collection)
            {
                count++;
            }

            var minLengthAttr = property.GetCustomAttributes(typeof(MinLengthAttribute), true)
                .FirstOrDefault() as MinLengthAttribute;
            if (minLengthAttr != null && count < minLengthAttr.Length)
            {
                result.AddError(propertyName, 
                    $"Collection must contain at least {minLengthAttr.Length} items");
            }

            var maxLengthAttr = property.GetCustomAttributes(typeof(MaxLengthAttribute), true)
                .FirstOrDefault() as MaxLengthAttribute;
            if (maxLengthAttr != null && count > maxLengthAttr.Length)
            {
                result.AddError(propertyName, 
                    $"Collection must contain at most {maxLengthAttr.Length} items");
            }
        }

        // Validation helpers
        public bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        public bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || 
                    uriResult.Scheme == Uri.UriSchemeHttps);
        }

        public bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                var fullPath = Path.GetFullPath(path);
                return !path.Contains("..") && !path.Contains("~");
            }
            catch
            {
                return false;
            }
        }

        public bool IsValidPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return false;

            var pattern = @"^\+?[1-9]\d{1,14}$"; // E.164 format
            return Regex.IsMatch(phoneNumber, pattern);
        }

        public bool IsValidCreditCard(string cardNumber)
        {
            if (string.IsNullOrWhiteSpace(cardNumber))
                return false;

            cardNumber = cardNumber.Replace(" ", "").Replace("-", "");
            
            if (!Regex.IsMatch(cardNumber, @"^\d+$"))
                return false;

            // Luhn algorithm
            int sum = 0;
            bool alternate = false;
            
            for (int i = cardNumber.Length - 1; i >= 0; i--)
            {
                int n = int.Parse(cardNumber[i].ToString());
                
                if (alternate)
                {
                    n *= 2;
                    if (n > 9)
                        n -= 9;
                }
                
                sum += n;
                alternate = !alternate;
            }
            
            return sum % 10 == 0;
        }

        public bool IsValidIpAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;

            return IPAddress.TryParse(ipAddress, out _);
        }

        public bool IsStrongPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 12)
                return false;

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));

            return hasUpper && hasLower && hasDigit && hasSpecial;
        }

        private bool ContainsSqlInjection(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var sqlPatterns = new[]
            {
                @"(\bSELECT\b|\bINSERT\b|\bUPDATE\b|\bDELETE\b|\bDROP\b|\bCREATE\b|\bALTER\b)",
                @"(\bEXEC\b|\bEXECUTE\b|\bxp_|\bsp_)",
                @"(--|;|'|""|\/\*|\*\/)"
            };

            return sqlPatterns.Any(pattern => 
                Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase));
        }

        private bool ContainsXss(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var xssPatterns = new[]
            {
                @"<script[^>]*>.*?</script>",
                @"<iframe[^>]*>.*?</iframe>",
                @"javascript:",
                @"on\w+\s*=",
                @"<img[^>]*onerror\s*=",
                @"<body[^>]*onload\s*="
            };

            return xssPatterns.Any(pattern => 
                Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase));
        }

        private bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) ||
                   type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal) || type == typeof(short) ||
                   type == typeof(byte) || type == typeof(uint) ||
                   type == typeof(ulong) || type == typeof(ushort) ||
                   type == typeof(sbyte);
        }
    }

    // Supporting classes
    public class ValidationResult
    {
        private readonly List<ValidationError> _errors = new();

        public bool IsValid => _errors.Count == 0;
        public IReadOnlyList<ValidationError> Errors => _errors.AsReadOnly();

        public void AddError(string field, string message)
        {
            _errors.Add(new ValidationError { Field = field, Message = message });
        }

        public void Merge(ValidationResult other)
        {
            _errors.AddRange(other._errors);
        }

        public override string ToString()
        {
            if (IsValid)
                return "Valid";

            return string.Join("; ", _errors.Select(e => $"{e.Field}: {e.Message}"));
        }
    }

    public class ValidationError
    {
        public string Field { get; set; }
        public string Message { get; set; }
    }

    public interface IValidationRule
    {
        bool CanValidate(Type type);
        ValidationResult Validate(object model);
    }

    public interface IAsyncValidationRule : IValidationRule
    {
        Task<ValidationResult> ValidateAsync(object model);
    }

    // Custom validation attributes
    [AttributeUsage(AttributeTargets.Property)]
    public class NoSqlInjectionAttribute : ValidationAttribute
    {
        protected override System.ComponentModel.DataAnnotations.ValidationResult IsValid(
            object value, ValidationContext validationContext)
        {
            if (value is string stringValue)
            {
                var validator = new Validator(null);
                if (validator.ContainsSqlInjection(stringValue))
                {
                    return new System.ComponentModel.DataAnnotations.ValidationResult(
                        "Value contains potentially dangerous SQL patterns");
                }
            }

            return System.ComponentModel.DataAnnotations.ValidationResult.Success;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class NoXssAttribute : ValidationAttribute
    {
        protected override System.ComponentModel.DataAnnotations.ValidationResult IsValid(
            object value, ValidationContext validationContext)
        {
            if (value is string stringValue)
            {
                var validator = new Validator(null);
                if (validator.ContainsXss(stringValue))
                {
                    return new System.ComponentModel.DataAnnotations.ValidationResult(
                        "Value contains potentially dangerous script patterns");
                }
            }

            return System.ComponentModel.DataAnnotations.ValidationResult.Success;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class StrongPasswordAttribute : ValidationAttribute
    {
        protected override System.ComponentModel.DataAnnotations.ValidationResult IsValid(
            object value, ValidationContext validationContext)
        {
            if (value is string password)
            {
                var validator = new Validator(null);
                if (!validator.IsStrongPassword(password))
                {
                    return new System.ComponentModel.DataAnnotations.ValidationResult(
                        "Password must be at least 12 characters and contain uppercase, lowercase, digit, and special character");
                }
            }

            return System.ComponentModel.DataAnnotations.ValidationResult.Success;
        }
    }
}