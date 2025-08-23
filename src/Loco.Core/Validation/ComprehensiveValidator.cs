using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;

namespace Loco.Core.Validation
{
    /// <summary>
    /// Comprehensive validation system following Clean Architecture principles
    /// Implements thorough input validation, security checks, and business rule validation
    /// </summary>
    public sealed class ComprehensiveValidator
    {
        private readonly ILogger<ComprehensiveValidator> _logger;
        private readonly Dictionary<string, IValidationRule> _validationRules;
        private readonly SecurityValidator _securityValidator;

        public ComprehensiveValidator(ILogger<ComprehensiveValidator> logger = null)
        {
            _logger = logger;
            _validationRules = new Dictionary<string, IValidationRule>();
            _securityValidator = new SecurityValidator();
            InitializeDefaultRules();
        }

        private void InitializeDefaultRules()
        {
            // Register default validation rules
            RegisterRule("required", new RequiredValidationRule());
            RegisterRule("email", new EmailValidationRule());
            RegisterRule("url", new UrlValidationRule());
            RegisterRule("file_path", new FilePathValidationRule());
            RegisterRule("json", new JsonValidationRule());
            RegisterRule("regex", new RegexValidationRule());
            RegisterRule("range", new RangeValidationRule());
            RegisterRule("length", new LengthValidationRule());
        }

        public void RegisterRule(string name, IValidationRule rule)
        {
            _validationRules[name] = rule ?? throw new ArgumentNullException(nameof(rule));
        }

        /// <summary>
        /// Validate automation rule with comprehensive checks
        /// </summary>
        public async Task<ValidationResult> ValidateAutomationRuleAsync(AutomationDsl.Rule rule)
        {
            var errors = new List<ValidationError>();

            // Basic structure validation
            if (rule == null)
            {
                errors.Add(new ValidationError("Rule", "Rule cannot be null"));
                return new ValidationResult { IsValid = false, Errors = errors };
            }

            // ID validation
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                errors.Add(new ValidationError("Id", "Rule ID is required"));
            }
            else if (!IsValidIdentifier(rule.Id))
            {
                errors.Add(new ValidationError("Id", "Rule ID contains invalid characters"));
            }

            // Name validation
            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                errors.Add(new ValidationError("Name", "Rule name is required"));
            }
            else if (rule.Name.Length > 255)
            {
                errors.Add(new ValidationError("Name", "Rule name exceeds maximum length of 255 characters"));
            }

            // Trigger validation
            if (rule.Trigger == null)
            {
                errors.Add(new ValidationError("Trigger", "At least one trigger is required"));
            }
            else
            {
                var triggerErrors = await ValidateTriggerAsync(rule.Trigger);
                errors.AddRange(triggerErrors);
            }

            // Actions validation
            if (rule.Actions == null || !rule.Actions.Any())
            {
                errors.Add(new ValidationError("Actions", "At least one action is required"));
            }
            else
            {
                foreach (var action in rule.Actions)
                {
                    var actionErrors = await ValidateActionAsync(action);
                    errors.AddRange(actionErrors);
                }
            }

            // Conditions validation (optional)
            if (rule.Conditions != null)
            {
                foreach (var condition in rule.Conditions)
                {
                    var conditionErrors = await ValidateConditionAsync(condition);
                    errors.AddRange(conditionErrors);
                }
            }

            // Security validation
            var securityErrors = await _securityValidator.ValidateRuleSecurityAsync(rule);
            errors.AddRange(securityErrors);

            return new ValidationResult
            {
                IsValid = !errors.Any(),
                Errors = errors
            };
        }

        private async Task<List<ValidationError>> ValidateTriggerAsync(AutomationDsl.TriggerDefinition trigger)
        {
            var errors = new List<ValidationError>();

            if (string.IsNullOrWhiteSpace(trigger.Type))
            {
                errors.Add(new ValidationError("Trigger.Type", "Trigger type is required"));
                return errors;
            }

            // Type-specific validation
            switch (trigger.Type.ToLower())
            {
                case "file":
                    errors.AddRange(await ValidateFileTriggerAsync(trigger));
                    break;
                case "time":
                    errors.AddRange(ValidateTimeTrigger(trigger));
                    break;
                case "http":
                    errors.AddRange(ValidateHttpTrigger(trigger));
                    break;
                case "system":
                    errors.AddRange(ValidateSystemTrigger(trigger));
                    break;
                default:
                    errors.Add(new ValidationError("Trigger.Type", $"Unknown trigger type: {trigger.Type}"));
                    break;
            }

            return errors;
        }

        private async Task<List<ValidationError>> ValidateFileTriggerAsync(AutomationDsl.TriggerDefinition trigger)
        {
            var errors = new List<ValidationError>();

            if (trigger.Parameters == null)
            {
                errors.Add(new ValidationError("Trigger.Parameters", "File trigger requires parameters"));
                return errors;
            }

            // Path validation
            if (!trigger.Parameters.TryGetValue("path", out var pathObj))
            {
                errors.Add(new ValidationError("Trigger.Parameters.path", "File path is required"));
            }
            else
            {
                var path = pathObj?.ToString();
                if (!string.IsNullOrEmpty(path))
                {
                    // Security check for path traversal
                    if (path.Contains("..") || path.Contains("~"))
                    {
                        errors.Add(new ValidationError("Trigger.Parameters.path", 
                            "Path contains potentially dangerous characters"));
                    }

                    // Check if path is accessible (async)
                    await Task.Run(() =>
                    {
                        try
                        {
                            var fullPath = Environment.ExpandEnvironmentVariables(path);
                            if (!Directory.Exists(Path.GetDirectoryName(fullPath)) && !File.Exists(fullPath))
                            {
                                errors.Add(new ValidationError("Trigger.Parameters.path", 
                                    $"Path does not exist or is not accessible: {path}"));
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add(new ValidationError("Trigger.Parameters.path", 
                                $"Invalid path: {ex.Message}"));
                        }
                    });
                }
            }

            // Event validation
            if (trigger.Parameters.TryGetValue("event", out var eventObj))
            {
                var validEvents = new[] { "created", "modified", "deleted", "renamed" };
                var eventType = eventObj?.ToString()?.ToLower();
                if (!string.IsNullOrEmpty(eventType) && !validEvents.Contains(eventType))
                {
                    errors.Add(new ValidationError("Trigger.Parameters.event", 
                        $"Invalid event type. Valid types: {string.Join(", ", validEvents)}"));
                }
            }

            return errors;
        }

        private List<ValidationError> ValidateTimeTrigger(AutomationDsl.TriggerDefinition trigger)
        {
            var errors = new List<ValidationError>();

            if (trigger.Parameters == null)
            {
                errors.Add(new ValidationError("Trigger.Parameters", "Time trigger requires parameters"));
                return errors;
            }

            // Cron expression validation
            if (trigger.Parameters.TryGetValue("cron", out var cronObj))
            {
                var cron = cronObj?.ToString();
                if (!string.IsNullOrEmpty(cron) && !IsValidCronExpression(cron))
                {
                    errors.Add(new ValidationError("Trigger.Parameters.cron", "Invalid cron expression"));
                }
            }
            // Interval validation
            else if (trigger.Parameters.TryGetValue("interval", out var intervalObj))
            {
                if (intervalObj is int interval)
                {
                    if (interval < 1000) // Minimum 1 second
                    {
                        errors.Add(new ValidationError("Trigger.Parameters.interval", 
                            "Interval must be at least 1000ms (1 second)"));
                    }
                    else if (interval > 86400000) // Maximum 24 hours
                    {
                        errors.Add(new ValidationError("Trigger.Parameters.interval", 
                            "Interval cannot exceed 86400000ms (24 hours)"));
                    }
                }
                else
                {
                    errors.Add(new ValidationError("Trigger.Parameters.interval", 
                        "Interval must be a number (milliseconds)"));
                }
            }
            else
            {
                errors.Add(new ValidationError("Trigger.Parameters", 
                    "Time trigger requires either 'cron' or 'interval' parameter"));
            }

            return errors;
        }

        private List<ValidationError> ValidateHttpTrigger(AutomationDsl.TriggerDefinition trigger)
        {
            var errors = new List<ValidationError>();

            if (trigger.Parameters == null)
            {
                errors.Add(new ValidationError("Trigger.Parameters", "HTTP trigger requires parameters"));
                return errors;
            }

            // URL validation
            if (!trigger.Parameters.TryGetValue("url", out var urlObj))
            {
                errors.Add(new ValidationError("Trigger.Parameters.url", "URL is required"));
            }
            else
            {
                var url = urlObj?.ToString();
                if (!string.IsNullOrEmpty(url))
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        errors.Add(new ValidationError("Trigger.Parameters.url", "Invalid URL format"));
                    }
                    else if (uri.Scheme != "http" && uri.Scheme != "https")
                    {
                        errors.Add(new ValidationError("Trigger.Parameters.url", 
                            "URL must use HTTP or HTTPS protocol"));
                    }
                }
            }

            // Method validation
            if (trigger.Parameters.TryGetValue("method", out var methodObj))
            {
                var validMethods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };
                var method = methodObj?.ToString()?.ToUpper();
                if (!string.IsNullOrEmpty(method) && !validMethods.Contains(method))
                {
                    errors.Add(new ValidationError("Trigger.Parameters.method", 
                        $"Invalid HTTP method. Valid methods: {string.Join(", ", validMethods)}"));
                }
            }

            return errors;
        }

        private List<ValidationError> ValidateSystemTrigger(AutomationDsl.TriggerDefinition trigger)
        {
            var errors = new List<ValidationError>();

            if (trigger.Parameters == null)
            {
                errors.Add(new ValidationError("Trigger.Parameters", "System trigger requires parameters"));
                return errors;
            }

            // Event type validation
            if (!trigger.Parameters.TryGetValue("event", out var eventObj))
            {
                errors.Add(new ValidationError("Trigger.Parameters.event", "System event type is required"));
            }
            else
            {
                var validEvents = new[] { "startup", "shutdown", "login", "logout", "sleep", "wake", 
                                         "network_connected", "network_disconnected", "battery_low" };
                var eventType = eventObj?.ToString()?.ToLower();
                if (!string.IsNullOrEmpty(eventType) && !validEvents.Contains(eventType))
                {
                    errors.Add(new ValidationError("Trigger.Parameters.event", 
                        $"Invalid system event. Valid events: {string.Join(", ", validEvents)}"));
                }
            }

            return errors;
        }

        private async Task<List<ValidationError>> ValidateActionAsync(AutomationDsl.ActionDefinition action)
        {
            var errors = new List<ValidationError>();

            if (string.IsNullOrWhiteSpace(action.Type))
            {
                errors.Add(new ValidationError("Action.Type", "Action type is required"));
                return errors;
            }

            // Common parameter validation
            if (action.Parameters != null)
            {
                foreach (var param in action.Parameters)
                {
                    // Check for injection attempts
                    if (param.Value is string strValue)
                    {
                        if (ContainsSqlInjection(strValue))
                        {
                            errors.Add(new ValidationError($"Action.Parameters.{param.Key}", 
                                "Parameter contains potentially dangerous SQL"));
                        }
                        if (ContainsScriptInjection(strValue))
                        {
                            errors.Add(new ValidationError($"Action.Parameters.{param.Key}", 
                                "Parameter contains potentially dangerous script"));
                        }
                    }
                }
            }

            // Type-specific validation
            switch (action.Type.ToLower())
            {
                case "file":
                    errors.AddRange(await ValidateFileActionAsync(action));
                    break;
                case "http":
                case "httprequest":
                    errors.AddRange(ValidateHttpAction(action));
                    break;
                case "notification":
                    errors.AddRange(ValidateNotificationAction(action));
                    break;
                case "launchapp":
                    errors.AddRange(ValidateLaunchAppAction(action));
                    break;
            }

            return errors;
        }

        private async Task<List<ValidationError>> ValidateFileActionAsync(AutomationDsl.ActionDefinition action)
        {
            var errors = new List<ValidationError>();

            if (action.Parameters == null)
            {
                errors.Add(new ValidationError("Action.Parameters", "File action requires parameters"));
                return errors;
            }

            // Operation validation
            if (!action.Parameters.TryGetValue("operation", out var opObj))
            {
                errors.Add(new ValidationError("Action.Parameters.operation", "File operation is required"));
            }
            else
            {
                var validOps = new[] { "read", "write", "copy", "move", "delete", "create" };
                var operation = opObj?.ToString()?.ToLower();
                if (!string.IsNullOrEmpty(operation) && !validOps.Contains(operation))
                {
                    errors.Add(new ValidationError("Action.Parameters.operation", 
                        $"Invalid operation. Valid operations: {string.Join(", ", validOps)}"));
                }

                // Path validation
                if (action.Parameters.TryGetValue("path", out var pathObj))
                {
                    var path = pathObj?.ToString();
                    if (!string.IsNullOrEmpty(path))
                    {
                        // Security checks
                        if (path.Contains("..") || path.Contains("~"))
                        {
                            errors.Add(new ValidationError("Action.Parameters.path", 
                                "Path contains potentially dangerous characters"));
                        }

                        // Check restricted paths
                        if (IsRestrictedPath(path))
                        {
                            errors.Add(new ValidationError("Action.Parameters.path", 
                                "Access to system directories is restricted"));
                        }
                    }
                }
            }

            return await Task.FromResult(errors);
        }

        private List<ValidationError> ValidateHttpAction(AutomationDsl.ActionDefinition action)
        {
            var errors = new List<ValidationError>();

            if (action.Parameters == null)
            {
                errors.Add(new ValidationError("Action.Parameters", "HTTP action requires parameters"));
                return errors;
            }

            // URL validation
            if (!action.Parameters.TryGetValue("url", out var urlObj))
            {
                errors.Add(new ValidationError("Action.Parameters.url", "URL is required"));
            }
            else
            {
                var url = urlObj?.ToString();
                if (!string.IsNullOrEmpty(url))
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        errors.Add(new ValidationError("Action.Parameters.url", "Invalid URL format"));
                    }
                    else
                    {
                        // Check for localhost/internal IPs (security consideration)
                        if (IsInternalUrl(uri))
                        {
                            _logger?.LogWarning("HTTP action targets internal URL: {Url}", url);
                        }
                    }
                }
            }

            return errors;
        }

        private List<ValidationError> ValidateNotificationAction(AutomationDsl.ActionDefinition action)
        {
            var errors = new List<ValidationError>();

            if (action.Parameters == null)
            {
                errors.Add(new ValidationError("Action.Parameters", "Notification action requires parameters"));
                return errors;
            }

            // Message validation
            if (!action.Parameters.TryGetValue("message", out var msgObj))
            {
                errors.Add(new ValidationError("Action.Parameters.message", "Message is required"));
            }
            else
            {
                var message = msgObj?.ToString();
                if (!string.IsNullOrEmpty(message) && message.Length > 1000)
                {
                    errors.Add(new ValidationError("Action.Parameters.message", 
                        "Message exceeds maximum length of 1000 characters"));
                }
            }

            return errors;
        }

        private List<ValidationError> ValidateLaunchAppAction(AutomationDsl.ActionDefinition action)
        {
            var errors = new List<ValidationError>();

            if (action.Parameters == null)
            {
                errors.Add(new ValidationError("Action.Parameters", "Launch app action requires parameters"));
                return errors;
            }

            // App path validation
            if (!action.Parameters.TryGetValue("appPath", out var pathObj) && 
                !action.Parameters.TryGetValue("appName", out var nameObj))
            {
                errors.Add(new ValidationError("Action.Parameters", 
                    "Either 'appPath' or 'appName' is required"));
            }
            else if (pathObj != null)
            {
                var path = pathObj.ToString();
                if (!string.IsNullOrEmpty(path))
                {
                    // Security check
                    if (IsRestrictedExecutable(path))
                    {
                        errors.Add(new ValidationError("Action.Parameters.appPath", 
                            "Launching system executables is restricted"));
                    }
                }
            }

            return errors;
        }

        private async Task<List<ValidationError>> ValidateConditionAsync(AutomationDsl.ConditionDefinition condition)
        {
            var errors = new List<ValidationError>();

            if (string.IsNullOrWhiteSpace(condition.Type))
            {
                errors.Add(new ValidationError("Condition.Type", "Condition type is required"));
            }

            if (condition.Value == null && condition.Parameters == null)
            {
                errors.Add(new ValidationError("Condition", 
                    "Condition must have either a value or parameters"));
            }

            return await Task.FromResult(errors);
        }

        // Helper methods
        private bool IsValidIdentifier(string id)
        {
            return Regex.IsMatch(id, @"^[a-zA-Z0-9_-]+$");
        }

        private bool IsValidCronExpression(string cron)
        {
            // Simplified cron validation (5 or 6 fields)
            var parts = cron.Split(' ');
            return parts.Length >= 5 && parts.Length <= 6;
        }

        private bool ContainsSqlInjection(string input)
        {
            var sqlKeywords = new[] { "DROP", "DELETE", "INSERT", "UPDATE", "EXEC", "EXECUTE", 
                                     "UNION", "SELECT", "--", "/*", "*/" };
            var upperInput = input.ToUpper();
            return sqlKeywords.Any(keyword => upperInput.Contains(keyword));
        }

        private bool ContainsScriptInjection(string input)
        {
            var scriptPatterns = new[] { "<script", "javascript:", "onerror=", "onclick=", 
                                        "onload=", "eval(", "document.", "window." };
            var lowerInput = input.ToLower();
            return scriptPatterns.Any(pattern => lowerInput.Contains(pattern));
        }

        private bool IsRestrictedPath(string path)
        {
            var restrictedPaths = new[] { @"C:\Windows", @"C:\Program Files", 
                                         "/etc", "/usr/bin", "/usr/sbin", "/bin", "/sbin" };
            var normalizedPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
            return restrictedPaths.Any(restricted => 
                normalizedPath.StartsWith(restricted, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsRestrictedExecutable(string path)
        {
            var restrictedExes = new[] { "cmd.exe", "powershell.exe", "bash", "sh", 
                                        "regedit.exe", "format.com", "del.exe" };
            var fileName = Path.GetFileName(path)?.ToLower();
            return restrictedExes.Any(exe => fileName?.Contains(exe) == true);
        }

        private bool IsInternalUrl(Uri uri)
        {
            var host = uri.Host.ToLower();
            return host == "localhost" || 
                   host == "127.0.0.1" || 
                   host.StartsWith("192.168.") ||
                   host.StartsWith("10.") ||
                   host.StartsWith("172.");
        }
    }

    // Supporting classes
    public interface IValidationRule
    {
        Task<ValidationResult> ValidateAsync(object value, Dictionary<string, object> parameters = null);
    }

    public class RequiredValidationRule : IValidationRule
    {
        public Task<ValidationResult> ValidateAsync(object value, Dictionary<string, object> parameters = null)
        {
            var isValid = value != null && 
                         (value is not string str || !string.IsNullOrWhiteSpace(str));
            
            return Task.FromResult(new ValidationResult
            {
                IsValid = isValid,
                Errors = isValid ? null : new[] { new ValidationError("Value", "This field is required") }
            });
        }
    }

    public class EmailValidationRule : IValidationRule
    {
        private static readonly Regex EmailRegex = new Regex(
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public Task<ValidationResult> ValidateAsync(object value, Dictionary<string, object> parameters = null)
        {
            if (value == null) return Task.FromResult(new ValidationResult { IsValid = true });
            
            var isValid = value is string str && EmailRegex.IsMatch(str);
            
            return Task.FromResult(new ValidationResult
            {
                IsValid = isValid,
                Errors = isValid ? null : new[] { new ValidationError("Value", "Invalid email format") }
            });
        }
    }

    public class UrlValidationRule : IValidationRule
    {
        public Task<ValidationResult> ValidateAsync(object value, Dictionary<string, object> parameters = null)
        {
            if (value == null) return Task.FromResult(new ValidationResult { IsValid = true });
            
            var isValid = value is string str && 
                         Uri.TryCreate(str, UriKind.Absolute, out var uri) &&
                         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            
            return Task.FromResult(new ValidationResult
            {
                IsValid = isValid,
                Errors = isValid ? null : new[] { new ValidationError("Value", "Invalid URL format") }
            });
        }
    }

    public class FilePathValidationRule : IValidationRule
    {
        public Task<ValidationResult> ValidateAsync(object value, Dictionary<string, object> parameters = null)
        {
            if (value == null) return Task.FromResult(new ValidationResult { IsValid = true });
            
            var errors = new List<ValidationError>();
            
            if (value is string path)
            {
                try
                {
                    var fullPath = Path.GetFullPath(path);
                    if (path.Contains(".."))
                    {
                        errors.Add(new ValidationError("Value", "Path traversal detected"));
                    }
                }
                catch
                {
                    errors.Add(new ValidationError("Value", "Invalid file path"));
                }
            }
            else
            {
                errors.Add(new ValidationError("Value", "Value must be a string"));
            }
            
            return Task.FromResult(new ValidationResult
            {
                IsValid = !errors.Any(),
                Errors = errors
            });
        }
    }

    public class JsonValidationRule : IValidationRule
    {
        public Task<ValidationResult> ValidateAsync(object value, Dictionary<string, object> parameters = null)
        {
            if (value == null) return Task.FromResult(new ValidationResult { IsValid = true });
            
            var isValid = false;
            
            if (value is string str)
            {
                try
                {
                    System.Text.Json.JsonDocument.Parse(str);
                    isValid = true;
                }
                catch
                {
                    // Invalid JSON
                }
            }
            
            return Task.FromResult(new ValidationResult
            {
                IsValid = isValid,
                Errors = isValid ? null : new[] { new ValidationError("Value", "Invalid JSON format") }
            });
        }
    }

    public class RegexValidationRule : IValidationRule
    {
        public Task<ValidationResult> ValidateAsync(object value, Dictionary<string, object> parameters = null)
        {
            if (value == null) return Task.FromResult(new ValidationResult { IsValid = true });
            
            if (parameters == null || !parameters.TryGetValue("pattern", out var patternObj))
            {
                return Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Errors = new[] { new ValidationError("Parameters", "Pattern is required") }
                });
            }
            
            var pattern = patternObj?.ToString();
            if (string.IsNullOrEmpty(pattern))
            {
                return Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Errors = new[] { new ValidationError("Parameters", "Pattern cannot be empty") }
                });
            }
            
            try
            {
                var regex = new Regex(pattern);
                var isValid = value is string str && regex.IsMatch(str);
                
                return Task.FromResult(new ValidationResult
                {
                    IsValid = isValid,
                    Errors = isValid ? null : new[] { new ValidationError("Value", "Value does not match pattern") }
                });
            }
            catch
            {
                return Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Errors = new[] { new ValidationError("Parameters", "Invalid regex pattern") }
                });
            }
        }
    }

    public class RangeValidationRule : IValidationRule
    {
        public Task<ValidationResult> ValidateAsync(object value, Dictionary<string, object> parameters = null)
        {
            if (value == null) return Task.FromResult(new ValidationResult { IsValid = true });
            
            if (parameters == null)
            {
                return Task.FromResult(new ValidationResult { IsValid = true });
            }
            
            var errors = new List<ValidationError>();
            
            if (value is IComparable comparable)
            {
                if (parameters.TryGetValue("min", out var minObj) && minObj is IComparable min)
                {
                    if (comparable.CompareTo(min) < 0)
                    {
                        errors.Add(new ValidationError("Value", $"Value must be at least {min}"));
                    }
                }
                
                if (parameters.TryGetValue("max", out var maxObj) && maxObj is IComparable max)
                {
                    if (comparable.CompareTo(max) > 0)
                    {
                        errors.Add(new ValidationError("Value", $"Value must be at most {max}"));
                    }
                }
            }
            
            return Task.FromResult(new ValidationResult
            {
                IsValid = !errors.Any(),
                Errors = errors
            });
        }
    }

    public class LengthValidationRule : IValidationRule
    {
        public Task<ValidationResult> ValidateAsync(object value, Dictionary<string, object> parameters = null)
        {
            if (value == null) return Task.FromResult(new ValidationResult { IsValid = true });
            
            if (parameters == null)
            {
                return Task.FromResult(new ValidationResult { IsValid = true });
            }
            
            var errors = new List<ValidationError>();
            
            if (value is string str)
            {
                if (parameters.TryGetValue("min", out var minObj) && minObj is int minLength)
                {
                    if (str.Length < minLength)
                    {
                        errors.Add(new ValidationError("Value", $"Value must be at least {minLength} characters"));
                    }
                }
                
                if (parameters.TryGetValue("max", out var maxObj) && maxObj is int maxLength)
                {
                    if (str.Length > maxLength)
                    {
                        errors.Add(new ValidationError("Value", $"Value must be at most {maxLength} characters"));
                    }
                }
            }
            
            return Task.FromResult(new ValidationResult
            {
                IsValid = !errors.Any(),
                Errors = errors
            });
        }
    }

    public class SecurityValidator
    {
        public async Task<List<ValidationError>> ValidateRuleSecurityAsync(AutomationDsl.Rule rule)
        {
            var errors = new List<ValidationError>();
            
            // Check for potentially dangerous combinations
            bool hasFileDelete = rule.Actions?.Any(a => 
                a.Type?.ToLower() == "file" && 
                a.Parameters?.GetValueOrDefault("operation")?.ToString()?.ToLower() == "delete") ?? false;
                
            bool hasSystemTrigger = rule.Trigger?.Type?.ToLower() == "system";
            
            if (hasFileDelete && hasSystemTrigger)
            {
                errors.Add(new ValidationError("Security", 
                    "Rules with system triggers and file deletion require additional review"));
            }
            
            // Check for excessive permissions
            int dangerousActionCount = 0;
            if (rule.Actions != null)
            {
                foreach (var action in rule.Actions)
                {
                    if (IsDangerousAction(action))
                    {
                        dangerousActionCount++;
                    }
                }
            }
            
            if (dangerousActionCount > 3)
            {
                errors.Add(new ValidationError("Security", 
                    "Rule contains multiple potentially dangerous actions"));
            }
            
            return await Task.FromResult(errors);
        }
        
        private bool IsDangerousAction(AutomationDsl.ActionDefinition action)
        {
            var dangerousTypes = new[] { "file", "launchApp", "system", "registry" };
            return dangerousTypes.Contains(action.Type?.ToLower());
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public IEnumerable<ValidationError> Errors { get; set; }
    }

    public class ValidationError
    {
        public string Field { get; set; }
        public string Message { get; set; }
        
        public ValidationError(string field, string message)
        {
            Field = field;
            Message = message;
        }
    }
}
