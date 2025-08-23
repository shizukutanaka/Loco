using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Loco.Core.Security
{
    /// <summary>
    /// セキュアなセッション管理サービス - P0項目#6
    /// タイムアウト、セッション固定攻撃対策、セキュアな識別子生成
    /// </summary>
    public class SecureSessionManager : IDisposable
    {
        private readonly ILogger<SecureSessionManager> _logger;
        private readonly ConcurrentDictionary<string, SecureSession> _sessions;
        private readonly Timer _cleanupTimer;
        private readonly SessionConfiguration _config;
        private readonly object _lockObject = new object();

        // セキュリティ定数
        private const int SessionIdLength = 64; // 512ビット
        private const int MinSessionIdEntropy = 128; // ビット
        private const string SessionCookieName = "LOCO_SESSION_ID";

        public SecureSessionManager(
            ILogger<SecureSessionManager> logger = null,
            SessionConfiguration config = null)
        {
            _logger = logger;
            _config = config ?? new SessionConfiguration();
            _sessions = new ConcurrentDictionary<string, SecureSession>();

            // セッション清掃タイマー
            _cleanupTimer = new Timer(
                CleanupExpiredSessions,
                null,
                TimeSpan.FromMinutes(_config.CleanupIntervalMinutes),
                TimeSpan.FromMinutes(_config.CleanupIntervalMinutes));

            _logger?.LogInformation("Secure Session Manager initialized with timeout {Timeout}min",
                _config.IdleTimeoutMinutes);
        }

        /// <summary>
        /// 新しいセキュアなセッションを作成 - P0項目#6
        /// </summary>
        public async Task<string> CreateSessionAsync(
            string userId,
            string userAgent = null,
            string ipAddress = null,
            Dictionary<string, object> initialData = null)
        {
            try
            {
                // セキュアなセッションIDを生成
                var sessionId = GenerateSecureSessionId();
                
                // セッション固定攻撃対策のためのリジェネレーション
                if (_config.EnableSessionRegeneration)
                {
                    // 既存セッションがあれば無効化
                    await InvalidateUserSessionsAsync(userId);
                }

                var session = new SecureSession
                {
                    SessionId = sessionId,
                    UserId = userId,
                    UserAgent = userAgent,
                    IpAddress = ipAddress,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(_config.IdleTimeoutMinutes),
                    AbsoluteExpiresAt = DateTime.UtcNow.AddMinutes(_config.AbsoluteTimeoutMinutes),
                    IsActive = true,
                    Data = initialData ?? new Dictionary<string, object>(),
                    SecurityLevel = CalculateSecurityLevel(userAgent, ipAddress),
                    FailedAccessAttempts = 0
                };

                // セッション整合性ハッシュを生成
                session.IntegrityHash = ComputeSessionHash(session);

                // セッションを保存
                if (!_sessions.TryAdd(sessionId, session))
                {
                    _logger?.LogError("Failed to add session {SessionId} to store", sessionId);
                    throw new InvalidOperationException("Session creation failed");
                }

                // セッション作成をログ記録
                _logger?.LogInformation("Session created for user {UserId} | Session: {SessionId} | IP: {IpAddress}",
                    userId, sessionId, ipAddress);

                return sessionId;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Session creation failed for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// セッションの検証と更新 - メイン認証機能
        /// </summary>
        public async Task<SessionValidationResult> ValidateSessionAsync(
            string sessionId,
            string userAgent = null,
            string ipAddress = null)
        {
            var result = new SessionValidationResult();

            try
            {
                // 基本的な入力検証
                if (string.IsNullOrEmpty(sessionId))
                {
                    result.AddError("Session ID is required");
                    return result;
                }

                // セッション存在確認
                if (!_sessions.TryGetValue(sessionId, out var session))
                {
                    result.AddError("Invalid session");
                    LogSecurityEvent("Unknown session accessed", sessionId, ipAddress);
                    return result;
                }

                lock (_lockObject)
                {
                    // アクティブ状態確認
                    if (!session.IsActive)
                    {
                        result.AddError("Session is inactive");
                        LogSecurityEvent("Inactive session accessed", sessionId, ipAddress);
                        return result;
                    }

                    // アイドルタイムアウト確認
                    if (DateTime.UtcNow > session.ExpiresAt)
                    {
                        result.AddError("Session has expired");
                        session.IsActive = false;
                        LogSecurityEvent("Expired session accessed", sessionId, ipAddress);
                        return result;
                    }

                    // 絶対タイムアウト確認
                    if (DateTime.UtcNow > session.AbsoluteExpiresAt)
                    {
                        result.AddError("Session absolute timeout exceeded");
                        session.IsActive = false;
                        LogSecurityEvent("Absolute timeout exceeded", sessionId, ipAddress);
                        return result;
                    }

                    // User-Agent検証（セッションハイジャック対策）
                    if (_config.ValidateUserAgent && !string.IsNullOrEmpty(session.UserAgent))
                    {
                        if (!string.Equals(session.UserAgent, userAgent, StringComparison.Ordinal))
                        {
                            session.FailedAccessAttempts++;
                            result.AddError("Session User-Agent mismatch");
                            LogSecurityEvent("User-Agent mismatch", sessionId, ipAddress);
                            
                            if (session.FailedAccessAttempts >= _config.MaxFailedAttempts)
                            {
                                session.IsActive = false;
                                LogSecurityEvent("Session locked due to failed attempts", sessionId, ipAddress);
                            }
                            return result;
                        }
                    }

                    // IP検証（厳格モードの場合）
                    if (_config.ValidateIpAddress && !string.IsNullOrEmpty(session.IpAddress))
                    {
                        if (!string.Equals(session.IpAddress, ipAddress, StringComparison.Ordinal))
                        {
                            session.FailedAccessAttempts++;
                            result.AddError("Session IP address mismatch");
                            LogSecurityEvent("IP address mismatch", sessionId, ipAddress);
                            
                            if (session.FailedAccessAttempts >= _config.MaxFailedAttempts)
                            {
                                session.IsActive = false;
                                LogSecurityEvent("Session locked due to IP mismatch", sessionId, ipAddress);
                            }
                            return result;
                        }
                    }

                    // セッション整合性確認
                    if (_config.ValidateIntegrity)
                    {
                        var expectedHash = ComputeSessionHash(session);
                        if (!string.Equals(session.IntegrityHash, expectedHash, StringComparison.Ordinal))
                        {
                            result.AddError("Session integrity check failed");
                            session.IsActive = false;
                            LogSecurityEvent("Session integrity compromised", sessionId, ipAddress);
                            return result;
                        }
                    }

                    // セッション情報を更新
                    session.LastAccessedAt = DateTime.UtcNow;
                    session.ExpiresAt = DateTime.UtcNow.AddMinutes(_config.IdleTimeoutMinutes);
                    session.AccessCount++;
                    session.FailedAccessAttempts = 0; // 成功時にリセット

                    // 整合性ハッシュを再計算
                    if (_config.ValidateIntegrity)
                    {
                        session.IntegrityHash = ComputeSessionHash(session);
                    }

                    result.IsValid = true;
                    result.Session = session;
                    result.UserId = session.UserId;

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Session validation error for {SessionId}", sessionId);
                result.AddError("Session validation failed");
                return result;
            }
        }

        /// <summary>
        /// セッションデータの安全な更新
        /// </summary>
        public async Task<bool> UpdateSessionDataAsync(string sessionId, string key, object value)
        {
            try
            {
                if (!_sessions.TryGetValue(sessionId, out var session) || !session.IsActive)
                {
                    return false;
                }

                lock (_lockObject)
                {
                    session.Data[key] = value;
                    session.LastModifiedAt = DateTime.UtcNow;
                    
                    // 整合性ハッシュ更新
                    if (_config.ValidateIntegrity)
                    {
                        session.IntegrityHash = ComputeSessionHash(session);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to update session data for {SessionId}", sessionId);
                return false;
            }
        }

        /// <summary>
        /// セッションの安全な終了
        /// </summary>
        public async Task<bool> EndSessionAsync(string sessionId)
        {
            try
            {
                if (_sessions.TryRemove(sessionId, out var session))
                {
                    session.IsActive = false;
                    session.EndedAt = DateTime.UtcNow;
                    
                    _logger?.LogInformation("Session ended for user {UserId} | Session: {SessionId}",
                        session.UserId, sessionId);
                    
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to end session {SessionId}", sessionId);
                return false;
            }
        }

        /// <summary>
        /// ユーザーの全セッションを無効化
        /// </summary>
        public async Task InvalidateUserSessionsAsync(string userId)
        {
            try
            {
                var sessionsToRemove = _sessions.Values
                    .Where(s => string.Equals(s.UserId, userId, StringComparison.Ordinal))
                    .Select(s => s.SessionId)
                    .ToList();

                foreach (var sessionId in sessionsToRemove)
                {
                    await EndSessionAsync(sessionId);
                }

                _logger?.LogInformation("Invalidated {Count} sessions for user {UserId}",
                    sessionsToRemove.Count, userId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to invalidate sessions for user {UserId}", userId);
            }
        }

        /// <summary>
        /// セッション統計の取得
        /// </summary>
        public SessionStatistics GetStatistics()
        {
            var now = DateTime.UtcNow;
            var activeSessions = 0;
            var expiredSessions = 0;
            var totalSessions = _sessions.Count;

            foreach (var session in _sessions.Values)
            {
                if (session.IsActive && session.ExpiresAt > now)
                {
                    activeSessions++;
                }
                else
                {
                    expiredSessions++;
                }
            }

            return new SessionStatistics
            {
                TotalSessions = totalSessions,
                ActiveSessions = activeSessions,
                ExpiredSessions = expiredSessions,
                GeneratedAt = now
            };
        }

        // プライベートメソッド

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GenerateSecureSessionId()
        {
            // CSPRNGを使用した高エントロピーセッションID生成
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[SessionIdLength];
            rng.GetBytes(bytes);

            // Base64エンコード（URL安全版）
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", "");
        }

        private string ComputeSessionHash(SecureSession session)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_config.SecretKey));
            var data = $"{session.SessionId}|{session.UserId}|{session.CreatedAt:O}|{session.UserAgent}|{session.IpAddress}";
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash);
        }

        private SecurityLevel CalculateSecurityLevel(string userAgent, string ipAddress)
        {
            var level = SecurityLevel.Standard;

            // TORブラウザやプロキシの検出
            if (!string.IsNullOrEmpty(userAgent))
            {
                if (userAgent.Contains("Tor", StringComparison.OrdinalIgnoreCase))
                {
                    level = SecurityLevel.High;
                }
            }

            // プライベートIPアドレスの検出
            if (!string.IsNullOrEmpty(ipAddress))
            {
                if (IsPrivateIpAddress(ipAddress))
                {
                    level = SecurityLevel.Low; // 内部ネットワーク
                }
            }

            return level;
        }

        private bool IsPrivateIpAddress(string ipAddress)
        {
            if (System.Net.IPAddress.TryParse(ipAddress, out var ip))
            {
                var bytes = ip.GetAddressBytes();
                return (bytes[0] == 10) ||
                       (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                       (bytes[0] == 192 && bytes[1] == 168);
            }
            return false;
        }

        private void CleanupExpiredSessions(object state)
        {
            try
            {
                var now = DateTime.UtcNow;
                var expiredSessions = _sessions
                    .Where(kvp => !kvp.Value.IsActive || kvp.Value.ExpiresAt < now || kvp.Value.AbsoluteExpiresAt < now)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var sessionId in expiredSessions)
                {
                    _sessions.TryRemove(sessionId, out _);
                }

                if (expiredSessions.Count > 0)
                {
                    _logger?.LogDebug("Cleaned up {Count} expired sessions", expiredSessions.Count);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during session cleanup");
            }
        }

        private void LogSecurityEvent(string message, string sessionId, string ipAddress)
        {
            _logger?.LogWarning("Session Security Event: {Message} | Session: {SessionId} | IP: {IpAddress}",
                message, sessionId, ipAddress);
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }

    // サポートクラス

    public class SessionConfiguration
    {
        public int IdleTimeoutMinutes { get; set; } = 30;
        public int AbsoluteTimeoutMinutes { get; set; } = 480; // 8時間
        public int CleanupIntervalMinutes { get; set; } = 15;
        public int MaxFailedAttempts { get; set; } = 3;
        public bool ValidateUserAgent { get; set; } = true;
        public bool ValidateIpAddress { get; set; } = false;
        public bool ValidateIntegrity { get; set; } = true;
        public bool EnableSessionRegeneration { get; set; } = true;
        public string SecretKey { get; set; } = GenerateSecretKey();

        private static string GenerateSecretKey()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }

    public class SecureSession
    {
        public string SessionId { get; set; }
        public string UserId { get; set; }
        public string UserAgent { get; set; }
        public string IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public DateTime? LastModifiedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime AbsoluteExpiresAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
        public string IntegrityHash { get; set; }
        public SecurityLevel SecurityLevel { get; set; }
        public int AccessCount { get; set; }
        public int FailedAccessAttempts { get; set; }
    }

    public class SessionValidationResult
    {
        public bool IsValid { get; set; }
        public SecureSession Session { get; set; }
        public string UserId { get; set; }
        public List<string> Errors { get; set; } = new List<string>();

        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }
    }

    public class SessionStatistics
    {
        public int TotalSessions { get; set; }
        public int ActiveSessions { get; set; }
        public int ExpiredSessions { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public enum SecurityLevel
    {
        Low,
        Standard,
        High,
        Critical
    }
}