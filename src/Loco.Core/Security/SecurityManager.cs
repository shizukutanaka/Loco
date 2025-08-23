using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Security
{
    public class SecurityManager
    {
        private readonly ILogger<SecurityManager> _logger;
        private readonly Dictionary<string, RateLimiter> _rateLimiters = new();
        private readonly Dictionary<string, int> _failedAttempts = new();
        private readonly HashSet<string> _blacklistedIps = new();
        private readonly object _lockObject = new();

        public SecurityManager(ILogger<SecurityManager> logger)
        {
            _logger = logger;
        }

        // Input Sanitization
        public string SanitizeInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Remove potential SQL injection patterns
            input = Regex.Replace(input, @"(\-\-|;|'|""|\/\*|\*\/|xp_|sp_|exec|execute|select|insert|update|delete|drop|create|alter|grant|revoke)", "", RegexOptions.IgnoreCase);
            
            // Remove potential XSS patterns
            input = Regex.Replace(input, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            input = Regex.Replace(input, @"<iframe[^>]*>.*?</iframe>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            input = Regex.Replace(input, @"javascript:", "", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"on\w+\s*=", "", RegexOptions.IgnoreCase);
            
            // Encode HTML entities
            input = System.Net.WebUtility.HtmlEncode(input);
            
            return input.Trim();
        }

        // Path Traversal Prevention
        public bool IsPathSafe(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            var dangerousPatterns = new[] { "..", "~", "./" };
            return !dangerousPatterns.Any(pattern => path.Contains(pattern));
        }

        // Rate Limiting
        public bool CheckRateLimit(string identifier, int maxRequests = 100, TimeSpan? period = null)
        {
            period ??= TimeSpan.FromMinutes(1);

            lock (_lockObject)
            {
                if (!_rateLimiters.ContainsKey(identifier))
                {
                    _rateLimiters[identifier] = new RateLimiter(maxRequests, period.Value);
                }

                var limiter = _rateLimiters[identifier];
                var allowed = limiter.AllowRequest();
                
                if (!allowed)
                {
                    _logger.LogWarning($"Rate limit exceeded for {identifier}");
                }

                return allowed;
            }
        }

        // Account Lockout
        public bool CheckAccountLockout(string username, int maxAttempts = 5)
        {
            lock (_lockObject)
            {
                if (_failedAttempts.ContainsKey(username) && _failedAttempts[username] >= maxAttempts)
                {
                    _logger.LogWarning($"Account locked for {username} after {maxAttempts} failed attempts");
                    return false;
                }
                return true;
            }
        }

        public void RecordFailedAttempt(string username)
        {
            lock (_lockObject)
            {
                if (!_failedAttempts.ContainsKey(username))
                    _failedAttempts[username] = 0;
                
                _failedAttempts[username]++;
                _logger.LogInformation($"Failed login attempt {_failedAttempts[username]} for {username}");
            }
        }

        public void ResetFailedAttempts(string username)
        {
            lock (_lockObject)
            {
                if (_failedAttempts.ContainsKey(username))
                {
                    _failedAttempts.Remove(username);
                    _logger.LogInformation($"Reset failed attempts for {username}");
                }
            }
        }

        // Password Validation
        public bool ValidatePasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 12)
                return false;

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));

            return hasUpper && hasLower && hasDigit && hasSpecial;
        }

        // Secure Random Generation
        public string GenerateSecureToken(int length = 32)
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[length];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        // Password Hashing
        public string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var salt = GenerateSecureToken(16);
            var combined = Encoding.UTF8.GetBytes(password + salt);
            var hash = sha256.ComputeHash(combined);
            return $"{salt}:{Convert.ToBase64String(hash)}";
        }

        public bool VerifyPassword(string password, string storedHash)
        {
            var parts = storedHash.Split(':');
            if (parts.Length != 2)
                return false;

            var salt = parts[0];
            var hash = parts[1];

            using var sha256 = SHA256.Create();
            var combined = Encoding.UTF8.GetBytes(password + salt);
            var computedHash = Convert.ToBase64String(sha256.ComputeHash(combined));

            return hash == computedHash;
        }

        // IP Blacklisting
        public void BlacklistIP(string ipAddress)
        {
            lock (_lockObject)
            {
                _blacklistedIps.Add(ipAddress);
                _logger.LogWarning($"IP {ipAddress} has been blacklisted");
            }
        }

        public bool IsIPBlacklisted(string ipAddress)
        {
            lock (_lockObject)
            {
                return _blacklistedIps.Contains(ipAddress);
            }
        }

        // CSRF Token Generation and Validation
        private readonly Dictionary<string, string> _csrfTokens = new();

        public string GenerateCSRFToken(string sessionId)
        {
            var token = GenerateSecureToken();
            lock (_lockObject)
            {
                _csrfTokens[sessionId] = token;
            }
            return token;
        }

        public bool ValidateCSRFToken(string sessionId, string token)
        {
            lock (_lockObject)
            {
                return _csrfTokens.ContainsKey(sessionId) && _csrfTokens[sessionId] == token;
            }
        }

        // Session Management
        private readonly Dictionary<string, SessionInfo> _sessions = new();

        public string CreateSession(string userId, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromMinutes(30);
            var sessionId = GenerateSecureToken();
            
            lock (_lockObject)
            {
                _sessions[sessionId] = new SessionInfo
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.Add(timeout.Value),
                    LastActivity = DateTime.UtcNow
                };
            }
            
            _logger.LogInformation($"Session created for user {userId}");
            return sessionId;
        }

        public bool ValidateSession(string sessionId)
        {
            lock (_lockObject)
            {
                if (!_sessions.ContainsKey(sessionId))
                    return false;

                var session = _sessions[sessionId];
                if (DateTime.UtcNow > session.ExpiresAt)
                {
                    _sessions.Remove(sessionId);
                    return false;
                }

                session.LastActivity = DateTime.UtcNow;
                return true;
            }
        }

        public void InvalidateSession(string sessionId)
        {
            lock (_lockObject)
            {
                if (_sessions.ContainsKey(sessionId))
                {
                    _sessions.Remove(sessionId);
                    _logger.LogInformation($"Session {sessionId} invalidated");
                }
            }
        }

        // Audit Logging
        public void LogSecurityEvent(string eventType, string userId, string details, string ipAddress = null)
        {
            var logEntry = new SecurityAuditLog
            {
                EventType = eventType,
                UserId = userId,
                Details = details,
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation($"SECURITY_AUDIT: {System.Text.Json.JsonSerializer.Serialize(logEntry)}");
        }

        // Helper Classes
        private class RateLimiter
        {
            private readonly int _maxRequests;
            private readonly TimeSpan _period;
            private readonly Queue<DateTime> _requests = new();

            public RateLimiter(int maxRequests, TimeSpan period)
            {
                _maxRequests = maxRequests;
                _period = period;
            }

            public bool AllowRequest()
            {
                var now = DateTime.UtcNow;
                var cutoff = now.Subtract(_period);

                while (_requests.Count > 0 && _requests.Peek() < cutoff)
                {
                    _requests.Dequeue();
                }

                if (_requests.Count >= _maxRequests)
                {
                    return false;
                }

                _requests.Enqueue(now);
                return true;
            }
        }

        private class SessionInfo
        {
            public string UserId { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public DateTime LastActivity { get; set; }
        }

        private class SecurityAuditLog
        {
            public string EventType { get; set; }
            public string UserId { get; set; }
            public string Details { get; set; }
            public string IpAddress { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}