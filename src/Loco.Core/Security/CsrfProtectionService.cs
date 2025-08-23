using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace Loco.Core.Security
{
    /// <summary>
    /// CSRF（Cross-Site Request Forgery）攻撃対策サービス - P0項目#3
    /// 二重送信Cookieパターンとトークン検証による包括的なCSRF保護
    /// </summary>
    public class CsrfProtectionService
    {
        private readonly ILogger<CsrfProtectionService> _logger;
        private readonly ConcurrentDictionary<string, CsrfToken> _tokenStore;
        private readonly Timer _cleanupTimer;
        private readonly CsrfConfiguration _config;

        // セキュリティ定数
        private const int TokenLength = 32;
        private const int DefaultExpirationMinutes = 60;
        private const string CsrfCookieName = "__RequestVerificationToken";
        private const string CsrfHeaderName = "X-CSRF-Token";

        public CsrfProtectionService(
            ILogger<CsrfProtectionService> logger = null,
            CsrfConfiguration config = null)
        {
            _logger = logger;
            _config = config ?? new CsrfConfiguration();
            _tokenStore = new ConcurrentDictionary<string, CsrfToken>();

            // 期限切れトークンの定期清掃
            _cleanupTimer = new Timer(
                CleanupExpiredTokens,
                null,
                TimeSpan.FromMinutes(_config.CleanupIntervalMinutes),
                TimeSpan.FromMinutes(_config.CleanupIntervalMinutes));
        }

        /// <summary>
        /// CSRFトークンを生成し、セッションに関連付け
        /// </summary>
        public async Task<string> GenerateTokenAsync(string sessionId, string userAgent = null, string ipAddress = null)
        {
            try
            {
                // セキュアな乱数生成
                using var rng = RandomNumberGenerator.Create();
                var tokenBytes = new byte[TokenLength];
                rng.GetBytes(tokenBytes);

                var token = Convert.ToBase64String(tokenBytes);
                var tokenHash = ComputeTokenHash(token, sessionId, userAgent, ipAddress);

                var csrfToken = new CsrfToken
                {
                    Token = token,
                    SessionId = sessionId,
                    UserAgent = userAgent,
                    IpAddress = ipAddress,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(_config.TokenExpirationMinutes),
                    TokenHash = tokenHash,
                    IsUsed = false
                };

                // トークンをストアに保存
                _tokenStore[token] = csrfToken;

                _logger?.LogDebug("CSRF token generated for session {SessionId}", sessionId);

                return token;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to generate CSRF token");
                throw new SecurityException("CSRF token generation failed", ex);
            }
        }

        /// <summary>
        /// CSRFトークンの検証 - メイン保護機能
        /// </summary>
        public async Task<ValidationResult> ValidateTokenAsync(
            string token,
            string sessionId,
            string userAgent = null,
            string ipAddress = null,
            bool consumeToken = true)
        {
            var result = new ValidationResult();

            try
            {
                // 基本的な入力検証
                if (string.IsNullOrEmpty(token))
                {
                    result.AddError("CSRF token is required");
                    LogSecurityEvent("Missing CSRF token", sessionId, ipAddress);
                    return result;
                }

                if (string.IsNullOrEmpty(sessionId))
                {
                    result.AddError("Session ID is required");
                    LogSecurityEvent("Missing session ID for CSRF validation", sessionId, ipAddress);
                    return result;
                }

                // トークンの存在確認
                if (!_tokenStore.TryGetValue(token, out var csrfToken))
                {
                    result.AddError("Invalid CSRF token");
                    LogSecurityEvent("Unknown CSRF token", sessionId, ipAddress);
                    return result;
                }

                // 期限切れチェック
                if (csrfToken.ExpiresAt < DateTime.UtcNow)
                {
                    result.AddError("CSRF token has expired");
                    _tokenStore.TryRemove(token, out _);
                    LogSecurityEvent("Expired CSRF token", sessionId, ipAddress);
                    return result;
                }

                // 使用済みチェック（ワンタイムトークンの場合）
                if (_config.OneTimeUse && csrfToken.IsUsed)
                {
                    result.AddError("CSRF token has already been used");
                    LogSecurityEvent("Reused CSRF token", sessionId, ipAddress);
                    return result;
                }

                // セッション検証
                if (!string.Equals(csrfToken.SessionId, sessionId, StringComparison.Ordinal))
                {
                    result.AddError("CSRF token session mismatch");
                    LogSecurityEvent("Session mismatch for CSRF token", sessionId, ipAddress);
                    return result;
                }

                // UserAgent検証（有効な場合）
                if (_config.ValidateUserAgent && !string.IsNullOrEmpty(csrfToken.UserAgent))
                {
                    if (!string.Equals(csrfToken.UserAgent, userAgent, StringComparison.Ordinal))
                    {
                        result.AddError("CSRF token User-Agent mismatch");
                        LogSecurityEvent("User-Agent mismatch for CSRF token", sessionId, ipAddress);
                        return result;
                    }
                }

                // IP検証（有効な場合）
                if (_config.ValidateIpAddress && !string.IsNullOrEmpty(csrfToken.IpAddress))
                {
                    if (!string.Equals(csrfToken.IpAddress, ipAddress, StringComparison.Ordinal))
                    {
                        result.AddError("CSRF token IP address mismatch");
                        LogSecurityEvent("IP address mismatch for CSRF token", sessionId, ipAddress);
                        return result;
                    }
                }

                // ハッシュ検証（完全性確認）
                var expectedHash = ComputeTokenHash(token, sessionId, userAgent, ipAddress);
                if (_config.ValidateTokenHash && !string.Equals(csrfToken.TokenHash, expectedHash, StringComparison.Ordinal))
                {
                    result.AddError("CSRF token integrity check failed");
                    LogSecurityEvent("Token integrity check failed", sessionId, ipAddress);
                    return result;
                }

                // トークン使用済みマーク
                if (consumeToken && _config.OneTimeUse)
                {
                    csrfToken.IsUsed = true;
                    csrfToken.UsedAt = DateTime.UtcNow;
                }

                result.IsValid = true;
                _logger?.LogDebug("CSRF token validated successfully for session {SessionId}", sessionId);

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CSRF token validation error");
                result.AddError("CSRF token validation failed");
                LogSecurityEvent("CSRF validation error", sessionId, ipAddress);
                return result;
            }
        }

        /// <summary>
        /// HTTPリクエストからCSRFトークンを抽出
        /// </summary>
        public string ExtractTokenFromRequest(HttpRequest request)
        {
            try
            {
                // ヘッダーから取得
                if (request.Headers.TryGetValue(CsrfHeaderName, out var headerValue))
                {
                    return headerValue.FirstOrDefault();
                }

                // フォームデータから取得
                if (request.HasFormContentType && request.Form.ContainsKey("__RequestVerificationToken"))
                {
                    return request.Form["__RequestVerificationToken"];
                }

                // クエリパラメータから取得（推奨しない）
                if (request.Query.ContainsKey("csrf_token"))
                {
                    return request.Query["csrf_token"];
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to extract CSRF token from request");
                return null;
            }
        }

        /// <summary>
        /// CSRFトークンをCookieに設定
        /// </summary>
        public void SetTokenCookie(HttpResponse response, string token)
        {
            try
            {
                response.Cookies.Append(CsrfCookieName, token, new CookieOptions
                {
                    HttpOnly = _config.CookieHttpOnly,
                    Secure = _config.CookieSecure,
                    SameSite = _config.CookieSameSite,
                    Expires = DateTime.UtcNow.AddMinutes(_config.TokenExpirationMinutes),
                    Path = "/",
                    IsEssential = true
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to set CSRF token cookie");
            }
        }

        /// <summary>
        /// 双方向送信Cookieパターンの検証
        /// </summary>
        public async Task<bool> ValidateDoubleCookieAsync(HttpRequest request)
        {
            try
            {
                // Cookieからトークンを取得
                var cookieToken = request.Cookies[CsrfCookieName];
                if (string.IsNullOrEmpty(cookieToken))
                {
                    return false;
                }

                // ヘッダーからトークンを取得
                var headerToken = ExtractTokenFromRequest(request);
                if (string.IsNullOrEmpty(headerToken))
                {
                    return false;
                }

                // トークンの一致を確認
                return string.Equals(cookieToken, headerToken, StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Double cookie validation failed");
                return false;
            }
        }

        /// <summary>
        /// トークンの統計情報を取得
        /// </summary>
        public CsrfStatistics GetStatistics()
        {
            var now = DateTime.UtcNow;
            var activeTokens = 0;
            var expiredTokens = 0;
            var usedTokens = 0;

            foreach (var token in _tokenStore.Values)
            {
                if (token.ExpiresAt < now)
                {
                    expiredTokens++;
                }
                else if (token.IsUsed)
                {
                    usedTokens++;
                }
                else
                {
                    activeTokens++;
                }
            }

            return new CsrfStatistics
            {
                TotalTokens = _tokenStore.Count,
                ActiveTokens = activeTokens,
                ExpiredTokens = expiredTokens,
                UsedTokens = usedTokens,
                GeneratedAt = now
            };
        }

        /// <summary>
        /// すべてのトークンを無効化（セキュリティ緊急時用）
        /// </summary>
        public void InvalidateAllTokens()
        {
            var count = _tokenStore.Count;
            _tokenStore.Clear();
            _logger?.LogWarning("All {Count} CSRF tokens invalidated", count);
        }

        /// <summary>
        /// 特定セッションのトークンを無効化
        /// </summary>
        public void InvalidateSessionTokens(string sessionId)
        {
            var tokensToRemove = _tokenStore.Values
                .Where(t => string.Equals(t.SessionId, sessionId, StringComparison.Ordinal))
                .Select(t => t.Token)
                .ToList();

            foreach (var token in tokensToRemove)
            {
                _tokenStore.TryRemove(token, out _);
            }

            _logger?.LogDebug("Invalidated {Count} CSRF tokens for session {SessionId}", tokensToRemove.Count, sessionId);
        }

        // プライベートメソッド

        private string ComputeTokenHash(string token, string sessionId, string userAgent = null, string ipAddress = null)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_config.SecretKey));
            var data = $"{token}|{sessionId}|{userAgent}|{ipAddress}";
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash);
        }

        private void CleanupExpiredTokens(object state)
        {
            try
            {
                var now = DateTime.UtcNow;
                var expiredTokens = _tokenStore
                    .Where(kvp => kvp.Value.ExpiresAt < now)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var token in expiredTokens)
                {
                    _tokenStore.TryRemove(token, out _);
                }

                if (expiredTokens.Count > 0)
                {
                    _logger?.LogDebug("Cleaned up {Count} expired CSRF tokens", expiredTokens.Count);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during CSRF token cleanup");
            }
        }

        private void LogSecurityEvent(string message, string sessionId, string ipAddress)
        {
            _logger?.LogWarning("CSRF Security Event: {Message} | Session: {SessionId} | IP: {IpAddress}",
                message, sessionId, ipAddress);
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }

    // サポートクラス

    public class CsrfConfiguration
    {
        public int TokenExpirationMinutes { get; set; } = DefaultExpirationMinutes;
        public int CleanupIntervalMinutes { get; set; } = 30;
        public bool OneTimeUse { get; set; } = false;
        public bool ValidateUserAgent { get; set; } = true;
        public bool ValidateIpAddress { get; set; } = false; // IPが変動する環境では無効化
        public bool ValidateTokenHash { get; set; } = true;
        public bool CookieHttpOnly { get; set; } = true;
        public bool CookieSecure { get; set; } = true;
        public SameSiteMode CookieSameSite { get; set; } = SameSiteMode.Strict;
        public string SecretKey { get; set; } = GenerateSecretKey();

        private static string GenerateSecretKey()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }

    public class CsrfToken
    {
        public string Token { get; set; }
        public string SessionId { get; set; }
        public string UserAgent { get; set; }
        public string IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public string TokenHash { get; set; }
        public bool IsUsed { get; set; }
    }

    public class CsrfStatistics
    {
        public int TotalTokens { get; set; }
        public int ActiveTokens { get; set; }
        public int ExpiredTokens { get; set; }
        public int UsedTokens { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class SecurityException : Exception
    {
        public SecurityException(string message) : base(message) { }
        public SecurityException(string message, Exception innerException) : base(message, innerException) { }
    }
}