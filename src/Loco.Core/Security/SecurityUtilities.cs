using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Loco.Core.Exceptions;

namespace Loco.Core.Security
{
    public static class SecurityUtilities
    {
        private static readonly HashSet<string> SensitivePatterns = new()
        {
            @"(api[_-]?key|apikey|secret|password|pwd|token|auth)",
            @"(bearer|basic)\s+[a-zA-Z0-9+/=]+",
            @"[a-f0-9]{32,}", // Potential API keys
            @"-----BEGIN (RSA |EC )?PRIVATE KEY-----",
            @"aws_access_key_id|aws_secret_access_key"
        };

        private static readonly HashSet<string> DangerousPaths = new()
        {
            ".ssh", "ssh_keys", ".aws", ".azure", ".gcloud",
            "passwords", "secrets", "credentials", "wallet",
            ".gnupg", ".docker", "kubeconfig"
        };

        private static readonly Lazy<string[]> WindowsCriticalDirectories = new(() =>
        {
            var directories = new List<string?>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows) ?? string.Empty, "System32"),
                "C:/Windows",
                "C:/Program Files",
                "C:/Program Files (x86)"
            };

            return directories
                .Where(dir => !string.IsNullOrWhiteSpace(dir))
                .Select(dir => NormalizePathForComparison(dir!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        });

        private static readonly Lazy<string[]> UnixCriticalDirectories = new(() => new[]
        {
            NormalizePathForComparison("/bin"),
            NormalizePathForComparison("/sbin"),
            NormalizePathForComparison("/usr/bin"),
            NormalizePathForComparison("/usr/sbin"),
            NormalizePathForComparison("/usr/lib"),
            NormalizePathForComparison("/etc"),
            NormalizePathForComparison("/var"),
            NormalizePathForComparison("/root"),
            NormalizePathForComparison("/System"),
            NormalizePathForComparison("/Library"),
            NormalizePathForComparison("/Applications")
        });

        public static bool IsPathSafe(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                var fullPath = Path.GetFullPath(path);

                if (path.Contains("..") || path.Contains("~"))
                    return false;

                var normalizedPath = NormalizePathForComparison(fullPath);

                foreach (var dangerous in DangerousPaths)
                {
                    if (normalizedPath.Contains(dangerous.ToLowerInvariant()))
                        return false;
                }

                if (normalizedPath.StartsWith("//", StringComparison.Ordinal))
                    return false;

                foreach (var systemDirectory in GetCriticalDirectories())
                {
                    if (normalizedPath.StartsWith(systemDirectory, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                var root = Path.GetPathRoot(fullPath);
                if (!string.IsNullOrEmpty(root) && string.Equals(NormalizePathForComparison(root), normalizedPath, StringComparison.OrdinalIgnoreCase))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                // Log security-related exceptions for monitoring
                System.Diagnostics.Debug.WriteLine($"Security path validation failed: {ex.Message}");
                return false;
            }
        }

        public static string SanitizeInput(string input, int maxLength = 1000)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Limit length
            if (input.Length > maxLength)
                input = input.Substring(0, maxLength);

            // Remove control characters except newline, carriage return, and tab
            input = Regex.Replace(input, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", string.Empty);

            // Remove potential script tags
            input = Regex.Replace(input, @"<script[^>]*>.*?</script>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            input = Regex.Replace(input, @"<iframe[^>]*>.*?</iframe>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            input = Regex.Replace(input, @"javascript:", string.Empty, RegexOptions.IgnoreCase);

            // Remove potential SQL injection patterns (more comprehensive approach)
            var sqlKeywords = new[] { "DROP", "DELETE", "INSERT", "UPDATE", "SELECT", "UNION", "EXEC", "EXECUTE", "ALTER", "CREATE", "TRUNCATE", "MERGE", "BULK", "OPENROWSET", "OPENDATASOURCE" };
            foreach (var keyword in sqlKeywords)
            {
                input = Regex.Replace(input, $@"\b{Regex.Escape(keyword)}\b", string.Empty, RegexOptions.IgnoreCase);
            }

            // Remove command injection patterns
            input = Regex.Replace(input, @"[;&|`$()]", string.Empty);

            return input.Trim();
        }

        public static string SanitizeCommandArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
                return string.Empty;

            // Remove null bytes and other control characters
            argument = Regex.Replace(argument, @"[\x00-\x1F\x7F]", string.Empty);

            // Remove command injection patterns
            argument = argument.Replace("&", string.Empty)
                               .Replace("|", string.Empty)
                               .Replace(";", string.Empty)
                               .Replace("`", string.Empty)
                               .Replace("$", string.Empty)
                               .Replace("(", string.Empty)
                               .Replace(")", string.Empty)
                               .Replace("<", string.Empty)
                               .Replace(">", string.Empty);

            return argument;
        }

        public static bool ValidateFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            // Check for invalid file name characters
            var invalidChars = Path.GetInvalidFileNameChars();
            if (fileName.IndexOfAny(invalidChars) >= 0)
                return false;

            // Block suspicious patterns
            var suspiciousPatterns = new[] { "..", "~", "$", "`" };
            foreach (var pattern in suspiciousPatterns)
            {
                if (fileName.Contains(pattern))
                    return false;
            }

            return true;
        }

        public static bool ContainsSensitiveData(string content)
        {
            if (string.IsNullOrEmpty(content))
                return false;

            var lowerContent = content.ToLowerInvariant();

            foreach (var pattern in SensitivePatterns)
            {
                if (Regex.IsMatch(lowerContent, pattern, RegexOptions.IgnoreCase))
                    return true;
            }

            return false;
        }

        public static string RedactIfSensitive(string content, string replacement = "[REDACTED]")
        {
            if (content == null)
                return string.Empty;

            return ContainsSensitiveData(content) ? replacement : content;
        }

        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

            // Use PBKDF2 (recommended for password hashing)
            const int SaltSize = 16;
            const int HashSize = 32;
            const int Iterations = 600000; // OWASP 2024 recommendation

            var saltBytes = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }

            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA256);
            var hashBytes = pbkdf2.GetBytes(HashSize);

            var result = new byte[SaltSize + HashSize];
            Buffer.BlockCopy(saltBytes, 0, result, 0, SaltSize);
            Buffer.BlockCopy(hashBytes, 0, result, SaltSize, HashSize);

            return Convert.ToBase64String(result);
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
                return false;

            try
            {
                const int SaltSize = 16;
                const int HashSize = 32;
                const int Iterations = 600000; // OWASP 2024 recommendation

                var hashBytes = Convert.FromBase64String(hashedPassword);

                if (hashBytes.Length != SaltSize + HashSize)
                    return false;

                var salt = new byte[SaltSize];
                var storedHash = new byte[HashSize];
                Buffer.BlockCopy(hashBytes, 0, salt, 0, SaltSize);
                Buffer.BlockCopy(hashBytes, SaltSize, storedHash, 0, HashSize);

                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
                var computedHash = pbkdf2.GetBytes(HashSize);

                return computedHash.SequenceEqual(storedHash);
            }
            catch
            {
                return false;
            }
        }

        public static string GenerateSecureToken(int length = 32)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be positive");

            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[length];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }

        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
                return Regex.IsMatch(email, pattern) && !email.Contains("..");
            }
            catch
            {
                return false;
            }
        }

        public static void SecureDelete(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            try
            {
                var fileInfo = new FileInfo(filePath);
                var bufferSize = 4096;
                var buffer = new byte[bufferSize];

                using (var rng = RandomNumberGenerator.Create())
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    var iterations = 3; // DoD 5220.22-M standard
                    var fileLength = stream.Length;

                    for (int pass = 0; pass < iterations; pass++)
                    {
                        stream.Seek(0, SeekOrigin.Begin);

                        for (long position = 0; position < fileLength; position += bufferSize)
                        {
                            var bytesToWrite = (int)Math.Min(bufferSize, fileLength - position);

                            if (pass < iterations - 1)
                                rng.GetBytes(buffer);
                            else
                                Array.Clear(buffer, 0, buffer.Length);

                            stream.Write(buffer, 0, bytesToWrite);
                        }

                        stream.Flush();
                    }
                }

                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                throw new Loco.Core.Exceptions.SecurityException($"Failed to securely delete file: {ex.Message}", ex);
            }
        }

        public static class RateLimiter
        {
            private static readonly ConcurrentDictionary<string, Queue<DateTime>> RequestHistory = new();
            private static readonly ConcurrentDictionary<string, object> LockObjects = new();

            public static bool IsAllowed(string identifier, int maxRequests, TimeSpan timeWindow)
            {
                if (!LockObjects.TryGetValue(identifier, out var lockObject))
                {
                    lockObject = new object();
                    LockObjects.TryAdd(identifier, lockObject);
                }

                lock (lockObject)
                {
                    var now = DateTime.UtcNow;
                    var windowStart = now - timeWindow;

                    if (!RequestHistory.TryGetValue(identifier, out var history))
                    {
                        history = new Queue<DateTime>();
                        RequestHistory[identifier] = history;
                    }

                    // Remove old entries
                    while (history.Count > 0 && history.Peek() < windowStart)
                        history.Dequeue();

                    if (history.Count >= maxRequests)
                        return false;

                    history.Enqueue(now);
                    return true;
                }
            }

            public static void Reset(string identifier)
            {
                if (LockObjects.TryGetValue(identifier, out var lockObject))
                {
                    lock (lockObject)
                    {
                        if (RequestHistory.ContainsKey(identifier))
                            RequestHistory[identifier].Clear();
                    }
                }
            }
        }

        private static IEnumerable<string> GetCriticalDirectories()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return WindowsCriticalDirectories.Value;

            return UnixCriticalDirectories.Value;
        }

        /// <summary>
        /// Validates API key format and strength
        /// </summary>
        public static ValidationResult ValidateApiKey(string apiKey, string provider = "")
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return ValidationResult.Failure("API key cannot be empty");
            }

            if (apiKey.Length < 20)
            {
                return ValidationResult.Failure("API key must be at least 20 characters long");
            }

            if (apiKey.Length > 200)
            {
                return ValidationResult.Failure("API key cannot exceed 200 characters");
            }

            // Check for common weak patterns
            if (ContainsWeakPatterns(apiKey))
            {
                return ValidationResult.Failure("API key contains weak or predictable patterns");
            }

            // Provider-specific validation
            var providerValidation = ValidateProviderSpecific(apiKey, provider);
            if (!providerValidation.IsValid)
            {
                return providerValidation;
            }

            // Entropy check
            if (!HasSufficientEntropy(apiKey))
            {
                return ValidationResult.Failure("API key lacks sufficient randomness");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Masks sensitive data in API keys for logging
        /// </summary>
        public static string MaskApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 8)
            {
                return "***";
            }

            var visibleChars = Math.Min(4, apiKey.Length / 4);
            var masked = new string('*', apiKey.Length - visibleChars * 2);

            return apiKey.Substring(0, visibleChars) + masked + apiKey.Substring(apiKey.Length - visibleChars);
        }

        /// <summary>
        /// Checks for weak patterns in API keys
        /// </summary>
        private static bool ContainsWeakPatterns(string apiKey)
        {
            var weakPatterns = new[]
            {
                "1234567890",
                "abcdefghij",
                "password",
                "admin",
                "test",
                "demo",
                "example",
                "apikey",
                "secret"
            };

            var lowerKey = apiKey.ToLowerInvariant();

            foreach (var pattern in weakPatterns)
            {
                if (lowerKey.Contains(pattern))
                {
                    return true;
                }
            }

            // Check for sequential characters
            for (int i = 0; i < apiKey.Length - 2; i++)
            {
                if (IsSequential(apiKey[i], apiKey[i + 1], apiKey[i + 2]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if three characters are sequential
        /// </summary>
        private static bool IsSequential(char a, char b, char c)
        {
            return (b == a + 1 && c == b + 1) || (b == a - 1 && c == b - 1);
        }

        /// <summary>
        /// Provider-specific API key validation
        /// </summary>
        private static ValidationResult ValidateProviderSpecific(string apiKey, string provider)
        {
            return provider.ToLowerInvariant() switch
            {
                "openai" => ValidateOpenAiKey(apiKey),
                "anthropic" => ValidateAnthropicKey(apiKey),
                "gemini" or "google" => ValidateGeminiKey(apiKey),
                "ollama" => ValidateOllamaKey(apiKey),
                _ => ValidationResult.Success() // Generic validation for unknown providers
            };
        }

        /// <summary>
        /// Validates OpenAI API key format
        /// </summary>
        private static ValidationResult ValidateOpenAiKey(string apiKey)
        {
            if (!apiKey.StartsWith("sk-", StringComparison.Ordinal))
            {
                return ValidationResult.Failure("OpenAI API key must start with 'sk-'");
            }

            if (apiKey.Length < 51)
            {
                return ValidationResult.Failure("OpenAI API key is too short");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates Anthropic API key format
        /// </summary>
        private static ValidationResult ValidateAnthropicKey(string apiKey)
        {
            if (!apiKey.StartsWith("sk-ant-", StringComparison.Ordinal))
            {
                return ValidationResult.Failure("Anthropic API key must start with 'sk-ant-'");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates Gemini API key format
        /// </summary>
        private static ValidationResult ValidateGeminiKey(string apiKey)
        {
            // Gemini keys are typically 39 characters and start with specific patterns
            if (apiKey.Length != 39 && !apiKey.StartsWith("AIza"))
            {
                return ValidationResult.Failure("Gemini API key format is invalid");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates Ollama API key (usually not required, but format check)
        /// </summary>
        private static ValidationResult ValidateOllamaKey(string apiKey)
        {
            // Ollama typically doesn't require API keys, but if provided, basic validation
            return ValidationResult.Success();
        }

        /// <summary>
        /// Checks if API key has sufficient entropy
        /// </summary>
        private static bool HasSufficientEntropy(string apiKey)
        {
            var charCounts = new Dictionary<char, int>();
            foreach (var c in apiKey)
            {
                charCounts[c] = charCounts.GetValueOrDefault(c, 0) + 1;
            }

            // Calculate entropy-like score
            double entropy = 0;
            foreach (var count in charCounts.Values)
            {
                var probability = (double)count / apiKey.Length;
                entropy -= probability * Math.Log2(probability);
            }

            // Require minimum entropy (roughly equivalent to random alphanumeric)
            return entropy >= 4.5;
        }

        /// <summary>
        /// Validation result class
        /// </summary>
        public class ValidationResult
        {
            public bool IsValid { get; private set; }
            public string? ErrorMessage { get; private set; }

            private ValidationResult(bool isValid, string? errorMessage = null)
            {
                IsValid = isValid;
                ErrorMessage = errorMessage;
            }

            public static ValidationResult Success() => new ValidationResult(true);
            public static ValidationResult Failure(string message) => new ValidationResult(false, message);
        }

        private static string NormalizePathForComparison(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
        }
    }
}