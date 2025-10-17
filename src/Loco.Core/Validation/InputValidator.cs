using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Loco.Core.Exceptions;

namespace Loco.Core.Validation
{
    /// <summary>
    /// Production-grade input validation for government-level security requirements.
    /// Validates all user inputs to prevent injection attacks, path traversal, and malicious operations.
    /// </summary>
    public static class InputValidator
    {
        private static readonly HashSet<string> DangerousFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jar",
            ".msi", ".scr", ".com", ".pif", ".reg", ".hta"
        };

        private static readonly HashSet<string> ForbiddenPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            "C:\\Windows\\System32",
            "C:\\Windows\\SysWOW64",
            "C:\\Program Files",
            "C:\\Program Files (x86)",
            "C:\\ProgramData\\Microsoft"
        };

        private static readonly Regex SafeFilenameRegex = new(@"^[a-zA-Z0-9_\-. ]+$", RegexOptions.Compiled);
        private static readonly Regex SafeIdRegex = new(@"^[a-zA-Z0-9_\-]+$", RegexOptions.Compiled);
        private static readonly Regex CommandInjectionPattern = new(@"[;&|`$()<>]", RegexOptions.Compiled);

        /// <summary>
        /// Validates a file path for safety.
        /// </summary>
        public static ValidationResult ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return ValidationResult.Failure("Path cannot be empty");

            try
            {
                // Get full path to normalize
                var fullPath = Path.GetFullPath(path);

                // Check for path traversal attempts
                if (path.Contains("..") || path.Contains("./") || path.Contains(".\\"))
                    return ValidationResult.Failure("Path traversal detected");

                // Check against forbidden paths
                foreach (var forbidden in ForbiddenPaths)
                {
                    if (fullPath.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase))
                        return ValidationResult.Failure($"Access to system directory forbidden: {forbidden}");
                }

                // Check for dangerous extensions
                var extension = Path.GetExtension(fullPath);
                if (!string.IsNullOrEmpty(extension) && DangerousFileExtensions.Contains(extension))
                    return ValidationResult.Failure($"Dangerous file extension not allowed: {extension}");

                return ValidationResult.Success(fullPath);
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Invalid path: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates a filename for safety.
        /// </summary>
        public static ValidationResult ValidateFilename(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return ValidationResult.Failure("Filename cannot be empty");

            if (filename.Length > 255)
                return ValidationResult.Failure("Filename too long (max 255 characters)");

            if (!SafeFilenameRegex.IsMatch(filename))
                return ValidationResult.Failure("Filename contains invalid characters");

            var invalidChars = Path.GetInvalidFileNameChars();
            if (filename.Any(c => invalidChars.Contains(c)))
                return ValidationResult.Failure("Filename contains invalid characters");

            return ValidationResult.Success(filename);
        }

        /// <summary>
        /// Validates an identifier (workflow ID, rule ID, etc.).
        /// </summary>
        public static ValidationResult ValidateIdentifier(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return ValidationResult.Failure("Identifier cannot be empty");

            if (id.Length > 100)
                return ValidationResult.Failure("Identifier too long (max 100 characters)");

            if (!SafeIdRegex.IsMatch(id))
                return ValidationResult.Failure("Identifier contains invalid characters (use only letters, numbers, hyphens, and underscores)");

            return ValidationResult.Success(id);
        }

        /// <summary>
        /// Validates a command string for injection attempts.
        /// </summary>
        public static ValidationResult ValidateCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return ValidationResult.Failure("Command cannot be empty");

            if (command.Length > 1000)
                return ValidationResult.Failure("Command too long (max 1000 characters)");

            if (CommandInjectionPattern.IsMatch(command))
                return ValidationResult.Failure("Command contains potentially dangerous characters");

            return ValidationResult.Success(command);
        }

        /// <summary>
        /// Validates a JSON string for basic structure.
        /// </summary>
        public static ValidationResult ValidateJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return ValidationResult.Failure("JSON cannot be empty");

            try
            {
                // Simple validation - check brackets are balanced
                int braceCount = 0, bracketCount = 0;
                foreach (char c in json)
                {
                    if (c == '{') braceCount++;
                    else if (c == '}') braceCount--;
                    else if (c == '[') bracketCount++;
                    else if (c == ']') bracketCount--;

                    if (braceCount < 0 || bracketCount < 0)
                        return ValidationResult.Failure("Malformed JSON: unbalanced brackets");
                }

                if (braceCount != 0 || bracketCount != 0)
                    return ValidationResult.Failure("Malformed JSON: unbalanced brackets");

                return ValidationResult.Success(json);
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Invalid JSON: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates a timeout value.
        /// </summary>
        public static ValidationResult ValidateTimeout(int timeoutSeconds)
        {
            if (timeoutSeconds < 1)
                return ValidationResult.Failure("Timeout must be at least 1 second");

            if (timeoutSeconds > 3600)
                return ValidationResult.Failure("Timeout cannot exceed 3600 seconds (1 hour)");

            return ValidationResult.Success(timeoutSeconds);
        }

        /// <summary>
        /// Validates a memory limit value.
        /// </summary>
        public static ValidationResult ValidateMemoryLimit(int memoryMB)
        {
            if (memoryMB < 64)
                return ValidationResult.Failure("Memory limit must be at least 64 MB");

            if (memoryMB > 8192)
                return ValidationResult.Failure("Memory limit cannot exceed 8192 MB");

            return ValidationResult.Success(memoryMB);
        }

        /// <summary>
        /// Validates a concurrent execution limit.
        /// </summary>
        public static ValidationResult ValidateConcurrentLimit(int limit)
        {
            if (limit < 1)
                return ValidationResult.Failure("Concurrent limit must be at least 1");

            if (limit > 100)
                return ValidationResult.Failure("Concurrent limit cannot exceed 100");

            return ValidationResult.Success(limit);
        }

        /// <summary>
        /// Sanitizes a string for safe display in logs.
        /// </summary>
        public static string SanitizeForLog(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Remove control characters
            var sanitized = Regex.Replace(input, @"[\x00-\x1F\x7F]", "");

            // Truncate if too long
            if (sanitized.Length > 500)
                sanitized = sanitized.Substring(0, 500) + "...";

            return sanitized;
        }

        /// <summary>
        /// Validates an email address format.
        /// </summary>
        public static ValidationResult ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return ValidationResult.Failure("Email cannot be empty");

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                if (addr.Address != email)
                    return ValidationResult.Failure("Invalid email format");

                return ValidationResult.Success(email);
            }
            catch
            {
                return ValidationResult.Failure("Invalid email format");
            }
        }

        /// <summary>
        /// Validates a port number.
        /// </summary>
        public static ValidationResult ValidatePort(int port)
        {
            if (port < 1 || port > 65535)
                return ValidationResult.Failure("Port must be between 1 and 65535");

            if (port < 1024)
                return ValidationResult.Failure("Port must be above 1024 (privileged ports not allowed)");

            return ValidationResult.Success(port);
        }

        /// <summary>
        /// Validates a URL.
        /// </summary>
        public static ValidationResult ValidateUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return ValidationResult.Failure("URL cannot be empty");

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return ValidationResult.Failure("Invalid URL format");

            if (uri.Scheme != "http" && uri.Scheme != "https")
                return ValidationResult.Failure("Only HTTP and HTTPS URLs are allowed");

            return ValidationResult.Success(url);
        }
    }

    /// <summary>
    /// Validation result with success/failure status and message.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; }
        public string Message { get; }
        public object? Value { get; }

        private ValidationResult(bool isValid, string message, object? value = null)
        {
            IsValid = isValid;
            Message = message;
            Value = value;
        }

        public static ValidationResult Success(object? value = null)
            => new ValidationResult(true, "Valid", value);

        public static ValidationResult Failure(string message)
            => new ValidationResult(false, message);

        public T GetValue<T>() where T : notnull
        {
            if (!IsValid)
                throw new InvalidOperationException("Cannot get value from failed validation");

            if (Value is T typedValue)
                return typedValue;

            throw new InvalidOperationException($"Value is not of type {typeof(T).Name}");
        }
    }
}