using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Loco.Core.Authentication
{
    public interface IJwtAuthenticationService
    {
        string GenerateToken(UserClaims userClaims, TokenOptions options = null);
        ClaimsPrincipal ValidateToken(string token);
        string RefreshToken(string token, string refreshToken);
        Task<bool> RevokeTokenAsync(string token);
        Task<bool> IsTokenRevokedAsync(string token);
        string GenerateRefreshToken();
        Task<AuthenticationResult> AuthenticateAsync(string username, string password);
        Task<bool> ValidateRefreshTokenAsync(string refreshToken, string username);
        void ConfigureTokenValidation(TokenValidationParameters parameters);
    }

    public class UserClaims
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
        public Dictionary<string, string> CustomClaims { get; set; } = new Dictionary<string, string>();
        public string SessionId { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
    }

    public class TokenOptions
    {
        public int ExpirationMinutes { get; set; } = 60;
        public int RefreshTokenExpirationDays { get; set; } = 7;
        public bool IncludeRefreshToken { get; set; } = true;
        public string Audience { get; set; }
        public string Issuer { get; set; }
        public bool RequireHttps { get; set; } = true;
        public bool ValidateLifetime { get; set; } = true;
        public bool ValidateAudience { get; set; } = true;
        public bool ValidateIssuer { get; set; } = true;
    }

    public class AuthenticationResult
    {
        public bool Success { get; set; }
        public string Token { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public UserClaims UserClaims { get; set; }
        public string ErrorMessage { get; set; }
        public AuthenticationErrorCode? ErrorCode { get; set; }
    }

    public enum AuthenticationErrorCode
    {
        InvalidCredentials,
        AccountLocked,
        AccountDisabled,
        PasswordExpired,
        MfaRequired,
        TokenExpired,
        TokenInvalid,
        RefreshTokenExpired,
        RefreshTokenInvalid
    }

    public class JwtAuthenticationService : IJwtAuthenticationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<JwtAuthenticationService> _logger;
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly HashSet<string> _revokedTokens;
        private readonly Dictionary<string, RefreshTokenInfo> _refreshTokens;
        private readonly object _lockObject = new object();
        private TokenValidationParameters _tokenValidationParameters;

        public JwtAuthenticationService(
            IConfiguration configuration,
            ILogger<JwtAuthenticationService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _secretKey = _configuration["Jwt:SecretKey"] ?? GenerateSecretKey();
            _issuer = _configuration["Jwt:Issuer"] ?? "Loco";
            _audience = _configuration["Jwt:Audience"] ?? "LocoUsers";
            
            _revokedTokens = new HashSet<string>();
            _refreshTokens = new Dictionary<string, RefreshTokenInfo>();
            
            InitializeTokenValidationParameters();
        }

        public string GenerateToken(UserClaims userClaims, TokenOptions options = null)
        {
            if (userClaims == null)
                throw new ArgumentNullException(nameof(userClaims));

            options ??= new TokenOptions();

            try
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
                var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

                var claims = new List<Claim>
                {
                    new Claim(JwtRegisteredClaimNames.Sub, userClaims.UserId),
                    new Claim(JwtRegisteredClaimNames.UniqueName, userClaims.Username),
                    new Claim(JwtRegisteredClaimNames.Email, userClaims.Email ?? ""),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                    new Claim("session_id", userClaims.SessionId ?? Guid.NewGuid().ToString()),
                    new Claim("ip", userClaims.IpAddress ?? "unknown"),
                    new Claim("user_agent", userClaims.UserAgent ?? "unknown")
                };

                // Add roles
                foreach (var role in userClaims.Roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                // Add custom claims
                foreach (var customClaim in userClaims.CustomClaims)
                {
                    claims.Add(new Claim(customClaim.Key, customClaim.Value));
                }

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.UtcNow.AddMinutes(options.ExpirationMinutes),
                    Issuer = options.Issuer ?? _issuer,
                    Audience = options.Audience ?? _audience,
                    SigningCredentials = credentials,
                    NotBefore = DateTime.UtcNow,
                    IssuedAt = DateTime.UtcNow
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                _logger.LogInformation($"Token generated for user: {userClaims.Username}");
                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating JWT token");
                throw;
            }
        }

        public ClaimsPrincipal ValidateToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            try
            {
                // Check if token is revoked
                if (IsTokenRevokedAsync(token).Result)
                {
                    _logger.LogWarning("Attempted to use revoked token");
                    return null;
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var principal = tokenHandler.ValidateToken(token, _tokenValidationParameters, out SecurityToken validatedToken);

                // Additional validation
                if (validatedToken is JwtSecurityToken jwtToken)
                {
                    // Check algorithm
                    if (!jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha512, StringComparison.InvariantCultureIgnoreCase))
                    {
                        _logger.LogWarning($"Invalid token algorithm: {jwtToken.Header.Alg}");
                        return null;
                    }

                    // Check expiration
                    if (jwtToken.ValidTo < DateTime.UtcNow)
                    {
                        _logger.LogWarning("Token has expired");
                        return null;
                    }
                }

                return principal;
            }
            catch (SecurityTokenException ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token validation");
                return null;
            }
        }

        public string RefreshToken(string token, string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(refreshToken))
                return null;

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jsonToken = tokenHandler.ReadJwtToken(token);
                
                var username = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.UniqueName)?.Value;
                if (string.IsNullOrEmpty(username))
                    return null;

                if (!ValidateRefreshTokenAsync(refreshToken, username).Result)
                {
                    _logger.LogWarning($"Invalid refresh token for user: {username}");
                    return null;
                }

                // Create new token with same claims
                var userClaims = new UserClaims
                {
                    UserId = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Sub)?.Value,
                    Username = username,
                    Email = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Email)?.Value,
                    Roles = jsonToken.Claims.Where(x => x.Type == ClaimTypes.Role).Select(x => x.Value).ToList(),
                    SessionId = jsonToken.Claims.FirstOrDefault(x => x.Type == "session_id")?.Value,
                    IpAddress = jsonToken.Claims.FirstOrDefault(x => x.Type == "ip")?.Value,
                    UserAgent = jsonToken.Claims.FirstOrDefault(x => x.Type == "user_agent")?.Value
                };

                // Revoke old token
                RevokeTokenAsync(token).Wait();

                // Generate new token
                return GenerateToken(userClaims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return null;
            }
        }

        public async Task<bool> RevokeTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            try
            {
                lock (_lockObject)
                {
                    _revokedTokens.Add(token);
                }

                _logger.LogInformation("Token revoked successfully");
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                return false;
            }
        }

        public async Task<bool> IsTokenRevokedAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return true;

            lock (_lockObject)
            {
                return Task.FromResult(_revokedTokens.Contains(token)).Result;
            }
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }

        public async Task<AuthenticationResult> AuthenticateAsync(string username, string password)
        {
            try
            {
                // This would typically validate against a database
                // For demo purposes, we'll use a simple check
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    return new AuthenticationResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid credentials",
                        ErrorCode = AuthenticationErrorCode.InvalidCredentials
                    };
                }

                // Create user claims (would come from database)
                var userClaims = new UserClaims
                {
                    UserId = Guid.NewGuid().ToString(),
                    Username = username,
                    Email = $"{username}@example.com",
                    Roles = new List<string> { "User" },
                    SessionId = Guid.NewGuid().ToString()
                };

                var token = GenerateToken(userClaims);
                var refreshToken = GenerateRefreshToken();

                // Store refresh token
                lock (_lockObject)
                {
                    _refreshTokens[refreshToken] = new RefreshTokenInfo
                    {
                        Username = username,
                        Token = refreshToken,
                        ExpiresAt = DateTime.UtcNow.AddDays(7)
                    };
                }

                return new AuthenticationResult
                {
                    Success = true,
                    Token = token,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    UserClaims = userClaims
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication failed");
                return new AuthenticationResult
                {
                    Success = false,
                    ErrorMessage = "Authentication failed",
                    ErrorCode = AuthenticationErrorCode.InvalidCredentials
                };
            }
        }

        public async Task<bool> ValidateRefreshTokenAsync(string refreshToken, string username)
        {
            if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(username))
                return false;

            lock (_lockObject)
            {
                if (_refreshTokens.TryGetValue(refreshToken, out var tokenInfo))
                {
                    if (tokenInfo.Username == username && tokenInfo.ExpiresAt > DateTime.UtcNow)
                    {
                        return Task.FromResult(true).Result;
                    }

                    // Remove expired token
                    _refreshTokens.Remove(refreshToken);
                }
            }

            return await Task.FromResult(false);
        }

        public void ConfigureTokenValidation(TokenValidationParameters parameters)
        {
            _tokenValidationParameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }

        private void InitializeTokenValidationParameters()
        {
            _tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey)),
                ClockSkew = TimeSpan.Zero,
                RequireExpirationTime = true,
                RequireSignedTokens = true
            };
        }

        private string GenerateSecretKey()
        {
            var key = new byte[64]; // 512 bits
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key);
            }
            return Convert.ToBase64String(key);
        }

        private class RefreshTokenInfo
        {
            public string Username { get; set; }
            public string Token { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }

    // JWT authentication middleware
    public class JwtAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IJwtAuthenticationService _authService;
        private readonly ILogger<JwtAuthenticationMiddleware> _logger;

        public JwtAuthenticationMiddleware(
            RequestDelegate next,
            IJwtAuthenticationService authService,
            ILogger<JwtAuthenticationMiddleware> logger)
        {
            _next = next;
            _authService = authService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var token = context.Request.Headers["Authorization"]
                .FirstOrDefault()?.Split(" ").Last();

            if (!string.IsNullOrWhiteSpace(token))
            {
                try
                {
                    var principal = _authService.ValidateToken(token);
                    if (principal != null)
                    {
                        context.User = principal;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Token validation failed");
                }
            }

            await _next(context);
        }
    }
}