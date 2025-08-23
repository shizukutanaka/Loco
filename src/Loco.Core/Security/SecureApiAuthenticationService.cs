using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Loco.Core.Security
{
    /// <summary>
    /// セキュアエンドポイント認証サービス - P0項目#8
    /// JWT、API Key、OAuth2.0認証の実装
    /// </summary>
    public class SecureApiAuthenticationService
    {
        private readonly ILogger<SecureApiAuthenticationService> _logger;
        private readonly SecurityAuditLogger _auditLogger;
        private readonly ApiAuthConfiguration _config;
        private readonly JwtSecurityTokenHandler _tokenHandler;

        public SecureApiAuthenticationService(
            ILogger<SecureApiAuthenticationService> logger = null,
            SecurityAuditLogger auditLogger = null,
            ApiAuthConfiguration config = null)
        {
            _logger = logger;
            _auditLogger = auditLogger;
            _config = config ?? new ApiAuthConfiguration();
            _tokenHandler = new JwtSecurityTokenHandler();
        }

        /// <summary>
        /// JWTトークンの生成
        /// </summary>
        public async Task<AuthenticationToken> GenerateJwtTokenAsync(
            string userId,
            List<Claim> claims = null,
            Dictionary<string, object> additionalData = null)
        {
            try
            {
                var tokenId = Guid.NewGuid().ToString();
                var issuedAt = DateTime.UtcNow;
                var expiresAt = issuedAt.Add(_config.TokenLifetime);

                var tokenClaims = new List<Claim>
                {
                    new Claim(JwtRegisteredClaimNames.Sub, userId),
                    new Claim(JwtRegisteredClaimNames.Jti, tokenId),
                    new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                    new Claim(JwtRegisteredClaimNames.Nbf, new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
                };

                if (claims != null)
                {
                    tokenClaims.AddRange(claims);
                }

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.SecretKey));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(tokenClaims),
                    Expires = expiresAt,
                    SigningCredentials = creds,
                    Issuer = _config.Issuer,
                    Audience = _config.Audience
                };

                var token = _tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = _tokenHandler.WriteToken(token);

                // リフレッシュトークンの生成
                var refreshToken = GenerateRefreshToken();

                await _auditLogger?.LogSecurityEventAsync(
                    SecurityEventType.Authentication,
                    "JwtTokenGenerated",
                    userId,
                    new Dictionary<string, object> { { "tokenId", tokenId } }
                );

                return new AuthenticationToken
                {
                    AccessToken = tokenString,
                    RefreshToken = refreshToken,
                    TokenType = "Bearer",
                    ExpiresIn = (int)_config.TokenLifetime.TotalSeconds,
                    IssuedAt = issuedAt,
                    ExpiresAt = expiresAt
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "JWT token generation failed for user {UserId}", userId);
                throw new SecurityException("Token generation failed", ex);
            }
        }

        /// <summary>
        /// JWTトークンの検証
        /// </summary>
        public async Task<TokenValidationResult> ValidateJwtTokenAsync(string token)
        {
            var result = new TokenValidationResult();

            try
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.SecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = _config.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _config.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
                
                result.IsValid = true;
                result.Principal = principal;
                result.UserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                                principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                result.Claims = principal.Claims;
                result.ValidatedToken = validatedToken;

                return result;
            }
            catch (SecurityTokenExpiredException)
            {
                result.AddError("Token has expired");
                result.IsExpired = true;
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                result.AddError("Invalid token signature");
                await _auditLogger?.LogSecurityEventAsync(
                    SecurityEventType.SecurityViolation,
                    "InvalidJwtSignature",
                    null
                );
            }
            catch (SecurityTokenException ex)
            {
                result.AddError($"Token validation failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "JWT token validation error");
                result.AddError("Token validation error");
            }

            return result;
        }

        /// <summary>
        /// API Keyの生成
        /// </summary>
        public async Task<ApiKey> GenerateApiKeyAsync(
            string applicationId,
            string applicationName,
            List<string> scopes = null,
            DateTime? expiresAt = null)
        {
            try
            {
                var apiKey = new ApiKey
                {
                    KeyId = Guid.NewGuid().ToString(),
                    ApplicationId = applicationId,
                    ApplicationName = applicationName,
                    Key = GenerateSecureApiKey(),
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt ?? DateTime.UtcNow.AddYears(1),
                    Scopes = scopes ?? new List<string>(),
                    IsActive = true,
                    LastUsedAt = null
                };

                // ハッシュ化して保存
                apiKey.KeyHash = HashApiKey(apiKey.Key);

                await _auditLogger?.LogSecurityEventAsync(
                    SecurityEventType.Configuration,
                    "ApiKeyGenerated",
                    applicationId,
                    new Dictionary<string, object>
                    {
                        { "keyId", apiKey.KeyId },
                        { "scopes", string.Join(",", apiKey.Scopes) }
                    }
                );

                return apiKey;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "API key generation failed for application {ApplicationId}", applicationId);
                throw;
            }
        }

        /// <summary>
        /// API Keyの検証
        /// </summary>
        public async Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey)
        {
            var result = new ApiKeyValidationResult();

            try
            {
                if (string.IsNullOrEmpty(apiKey))
                {
                    result.AddError("API key is required");
                    return result;
                }

                // ハッシュ化して比較
                var keyHash = HashApiKey(apiKey);
                
                // 実装依存: データストアからキー情報を取得
                var storedKey = await GetApiKeyByHashAsync(keyHash);
                
                if (storedKey == null)
                {
                    result.AddError("Invalid API key");
                    await _auditLogger?.LogSecurityEventAsync(
                        SecurityEventType.SecurityViolation,
                        "InvalidApiKey",
                        null
                    );
                    return result;
                }

                if (!storedKey.IsActive)
                {
                    result.AddError("API key is inactive");
                    return result;
                }

                if (storedKey.ExpiresAt < DateTime.UtcNow)
                {
                    result.AddError("API key has expired");
                    return result;
                }

                result.IsValid = true;
                result.ApplicationId = storedKey.ApplicationId;
                result.Scopes = storedKey.Scopes;
                result.KeyId = storedKey.KeyId;

                // 最終使用日時を更新
                await UpdateApiKeyLastUsedAsync(storedKey.KeyId);

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "API key validation error");
                result.AddError("API key validation failed");
                return result;
            }
        }

        /// <summary>
        /// リフレッシュトークンによるアクセストークン更新
        /// </summary>
        public async Task<AuthenticationToken> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                // 実装依存: リフレッシュトークンの検証
                var validationResult = await ValidateRefreshTokenAsync(refreshToken);
                
                if (!validationResult.IsValid)
                {
                    throw new SecurityException("Invalid refresh token");
                }

                // 新しいトークンを生成
                return await GenerateJwtTokenAsync(
                    validationResult.UserId,
                    validationResult.Claims
                );
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Token refresh failed");
                throw new SecurityException("Token refresh failed", ex);
            }
        }

        /// <summary>
        /// トークンの無効化（ログアウト）
        /// </summary>
        public async Task<bool> RevokeTokenAsync(string token)
        {
            try
            {
                // JWTトークンからJTIを取得
                var validationResult = await ValidateJwtTokenAsync(token);
                if (validationResult.ValidatedToken is JwtSecurityToken jwtToken)
                {
                    var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
                    
                    if (!string.IsNullOrEmpty(jti))
                    {
                        // 実装依存: ブラックリストに追加
                        await AddToTokenBlacklistAsync(jti, jwtToken.ValidTo);
                        
                        await _auditLogger?.LogSecurityEventAsync(
                            SecurityEventType.Authentication,
                            "TokenRevoked",
                            validationResult.UserId,
                            new Dictionary<string, object> { { "jti", jti } }
                        );
                        
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Token revocation failed");
                return false;
            }
        }

        // プライベートメソッド

        private string GenerateRefreshToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }

        private string GenerateSecureApiKey()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return $"loco_{Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")}";
        }

        private string HashApiKey(string apiKey)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(apiKey + _config.ApiKeySalt));
            return Convert.ToBase64String(bytes);
        }

        // 実装依存の抽象メソッド
        protected virtual Task<ApiKey> GetApiKeyByHashAsync(string keyHash) => Task.FromResult<ApiKey>(null);
        protected virtual Task UpdateApiKeyLastUsedAsync(string keyId) => Task.CompletedTask;
        protected virtual Task<RefreshTokenValidationResult> ValidateRefreshTokenAsync(string refreshToken) 
            => Task.FromResult(new RefreshTokenValidationResult { IsValid = false });
        protected virtual Task AddToTokenBlacklistAsync(string jti, DateTime expiresAt) => Task.CompletedTask;
    }

    // サポートクラス

    public class ApiAuthConfiguration
    {
        public string SecretKey { get; set; } = GenerateSecretKey();
        public string ApiKeySalt { get; set; } = GenerateSecretKey();
        public string Issuer { get; set; } = "Loco.Authentication";
        public string Audience { get; set; } = "Loco.Api";
        public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);

        private static string GenerateSecretKey()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }

    public class AuthenticationToken
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string TokenType { get; set; }
        public int ExpiresIn { get; set; }
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class TokenValidationResult
    {
        public bool IsValid { get; set; }
        public bool IsExpired { get; set; }
        public ClaimsPrincipal Principal { get; set; }
        public string UserId { get; set; }
        public IEnumerable<Claim> Claims { get; set; }
        public SecurityToken ValidatedToken { get; set; }
        public List<string> Errors { get; set; } = new();

        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }
    }

    public class ApiKey
    {
        public string KeyId { get; set; }
        public string ApplicationId { get; set; }
        public string ApplicationName { get; set; }
        public string Key { get; set; }
        public string KeyHash { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public List<string> Scopes { get; set; } = new();
        public bool IsActive { get; set; }
    }

    public class ApiKeyValidationResult
    {
        public bool IsValid { get; set; }
        public string ApplicationId { get; set; }
        public string KeyId { get; set; }
        public List<string> Scopes { get; set; } = new();
        public List<string> Errors { get; set; } = new();

        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }
    }

    public class RefreshTokenValidationResult
    {
        public bool IsValid { get; set; }
        public string UserId { get; set; }
        public List<Claim> Claims { get; set; } = new();
    }

    public class SecurityException : Exception
    {
        public SecurityException(string message) : base(message) { }
        public SecurityException(string message, Exception innerException) : base(message, innerException) { }
    }
}