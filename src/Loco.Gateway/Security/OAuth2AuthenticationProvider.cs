using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Loco.Gateway.Security;

/// <summary>
/// Enterprise OAuth2/OpenID Connect authentication provider
/// </summary>
public class OAuth2AuthenticationProvider
{
    private readonly ILogger<OAuth2AuthenticationProvider> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TokenValidationService _tokenValidator;
    private readonly RefreshTokenService _refreshTokenService;
    private readonly UserSessionManager _sessionManager;
    private readonly SecurityAuditService _auditService;

    public OAuth2AuthenticationProvider(
        ILogger<OAuth2AuthenticationProvider> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _tokenValidator = new TokenValidationService(configuration);
        _refreshTokenService = new RefreshTokenService();
        _sessionManager = new UserSessionManager();
        _auditService = new SecurityAuditService(logger);
    }

    /// <summary>
    /// Configure OAuth2/OpenID Connect authentication
    /// </summary>
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Authentication:Jwt");
        var oauth2Settings = configuration.GetSection("Authentication:OAuth2");

        // Configure JWT Bearer authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings["SecretKey"] ?? GenerateSecretKey())),
                ClockSkew = TimeSpan.Zero,
                RequireExpirationTime = true,
                RequireSignedTokens = true
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<OAuth2AuthenticationProvider>>();
                    logger.LogWarning("Authentication failed: {Error}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<OAuth2AuthenticationProvider>>();
                    logger.LogInformation("Token validated for user: {User}", 
                        context.Principal?.Identity?.Name);
                    return Task.CompletedTask;
                },
                OnMessageReceived = context =>
                {
                    // Support token from query string for WebSocket connections
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    
                    if (!string.IsNullOrEmpty(accessToken) && 
                        path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    
                    return Task.CompletedTask;
                }
            };
        })
        .AddOpenIdConnect("oidc", options =>
        {
            options.Authority = oauth2Settings["Authority"];
            options.ClientId = oauth2Settings["ClientId"];
            options.ClientSecret = oauth2Settings["ClientSecret"];
            options.ResponseType = "code";
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
            options.Scope.Add("offline_access");
            
            options.Events = new Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectEvents
            {
                OnRedirectToIdentityProvider = context =>
                {
                    // Add custom parameters if needed
                    context.ProtocolMessage.SetParameter("prompt", "select_account");
                    return Task.CompletedTask;
                },
                OnTokenResponseReceived = context =>
                {
                    // Store refresh token
                    var refreshToken = context.TokenEndpointResponse.RefreshToken;
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        // Store securely
                        var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        if (userId != null)
                        {
                            var refreshService = context.HttpContext.RequestServices
                                .GetRequiredService<RefreshTokenService>();
                            refreshService.StoreRefreshTokenAsync(userId, refreshToken);
                        }
                    }
                    return Task.CompletedTask;
                }
            };
        })
        .AddOAuth("GitHub", options =>
        {
            options.ClientId = oauth2Settings["GitHub:ClientId"] ?? "";
            options.ClientSecret = oauth2Settings["GitHub:ClientSecret"] ?? "";
            options.CallbackPath = new PathString("/signin-github");
            options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
            options.TokenEndpoint = "https://github.com/login/oauth/access_token";
            options.UserInformationEndpoint = "https://api.github.com/user";
            options.SaveTokens = true;
            options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
            options.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
            options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
            options.ClaimActions.MapJsonKey("avatar_url", "avatar_url");
        })
        .AddOAuth("Google", options =>
        {
            options.ClientId = oauth2Settings["Google:ClientId"] ?? "";
            options.ClientSecret = oauth2Settings["Google:ClientSecret"] ?? "";
            options.CallbackPath = new PathString("/signin-google");
            options.AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
            options.TokenEndpoint = "https://oauth2.googleapis.com/token";
            options.UserInformationEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";
            options.SaveTokens = true;
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
        })
        .AddOAuth("Microsoft", options =>
        {
            options.ClientId = oauth2Settings["Microsoft:ClientId"] ?? "";
            options.ClientSecret = oauth2Settings["Microsoft:ClientSecret"] ?? "";
            options.CallbackPath = new PathString("/signin-microsoft");
            options.AuthorizationEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
            options.TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
            options.UserInformationEndpoint = "https://graph.microsoft.com/v1.0/me";
            options.SaveTokens = true;
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
        });

        // Add authorization policies
        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAuthenticatedUser", policy =>
                policy.RequireAuthenticatedUser());
            
            options.AddPolicy("RequireAdminRole", policy =>
                policy.RequireRole("Admin"));
            
            options.AddPolicy("RequireApiScope", policy =>
                policy.RequireClaim("scope", "api.access"));
            
            options.AddPolicy("RequireMfa", policy =>
                policy.RequireClaim("amr", "mfa"));
            
            options.AddPolicy("RequireVerifiedEmail", policy =>
                policy.RequireClaim("email_verified", "true"));
        });

        // Add custom authorization handler
        services.AddSingleton<IAuthorizationHandler, CustomAuthorizationHandler>();
        
        // Add token services
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<RefreshTokenService>();
        services.AddSingleton<UserSessionManager>();
        services.AddSingleton<SecurityAuditService>();
    }

    /// <summary>
    /// Generate JWT token for authenticated user
    /// </summary>
    public async Task<AuthenticationToken> GenerateTokenAsync(ClaimsPrincipal user, TokenRequest request)
    {
        try
        {
            // Validate user
            if (user?.Identity?.IsAuthenticated != true)
            {
                throw new UnauthorizedAccessException("User is not authenticated");
            }

            // Extract claims
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                        user.FindFirst("sub")?.Value ?? 
                        Guid.NewGuid().ToString();
            
            var email = user.FindFirst(ClaimTypes.Email)?.Value;
            var name = user.FindFirst(ClaimTypes.Name)?.Value ?? 
                      user.FindFirst("name")?.Value;
            
            var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            var scopes = request.Scopes ?? new[] { "api.access" };

            // Create claims
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, 
                    new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(), 
                    ClaimValueTypes.Integer64),
                new Claim(JwtRegisteredClaimNames.Nbf,
                    new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64),
                new Claim(JwtRegisteredClaimNames.Exp,
                    new DateTimeOffset(DateTime.UtcNow.AddMinutes(request.ExpirationMinutes ?? 60))
                        .ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64)
            };

            if (!string.IsNullOrEmpty(email))
                claims.Add(new Claim(JwtRegisteredClaimNames.Email, email));
            
            if (!string.IsNullOrEmpty(name))
                claims.Add(new Claim(JwtRegisteredClaimNames.Name, name));

            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            foreach (var scope in scopes)
                claims.Add(new Claim("scope", scope));

            // Add custom claims
            if (request.CustomClaims != null)
            {
                foreach (var customClaim in request.CustomClaims)
                {
                    claims.Add(new Claim(customClaim.Key, customClaim.Value));
                }
            }

            // Generate token
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Authentication:Jwt:SecretKey"] ?? 
                    throw new InvalidOperationException("JWT secret key not configured")));
            
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            
            var token = new JwtSecurityToken(
                issuer: _configuration["Authentication:Jwt:Issuer"],
                audience: _configuration["Authentication:Jwt:Audience"],
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(request.ExpirationMinutes ?? 60),
                signingCredentials: credentials
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            // Generate refresh token if requested
            string? refreshToken = null;
            if (request.IncludeRefreshToken)
            {
                refreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(userId);
            }

            // Create session
            await _sessionManager.CreateSessionAsync(userId, tokenString, request.DeviceInfo);

            // Audit log
            await _auditService.LogAuthenticationAsync(userId, "TokenGenerated", request.IpAddress);

            return new AuthenticationToken
            {
                AccessToken = tokenString,
                RefreshToken = refreshToken,
                ExpiresIn = (request.ExpirationMinutes ?? 60) * 60,
                TokenType = "Bearer",
                Scope = string.Join(" ", scopes),
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(request.ExpirationMinutes ?? 60)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate token");
            throw;
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    public async Task<AuthenticationToken> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            // Validate refresh token
            var userId = await _refreshTokenService.ValidateRefreshTokenAsync(refreshToken);
            
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("Invalid refresh token");
            }

            // Get user claims
            var userClaims = await GetUserClaimsAsync(userId);
            
            // Generate new access token
            var principal = new ClaimsPrincipal(new ClaimsIdentity(userClaims, "Bearer"));
            
            var newToken = await GenerateTokenAsync(principal, new TokenRequest
            {
                ExpirationMinutes = 60,
                IncludeRefreshToken = true
            });

            // Rotate refresh token
            await _refreshTokenService.RotateRefreshTokenAsync(refreshToken, newToken.RefreshToken!);

            // Audit log
            await _auditService.LogAuthenticationAsync(userId, "TokenRefreshed", null);

            return newToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh token");
            throw;
        }
    }

    /// <summary>
    /// Revoke token
    /// </summary>
    public async Task RevokeTokenAsync(string token, TokenType tokenType)
    {
        try
        {
            if (tokenType == TokenType.RefreshToken)
            {
                await _refreshTokenService.RevokeRefreshTokenAsync(token);
            }
            else
            {
                // Add access token to blacklist
                await _tokenValidator.BlacklistTokenAsync(token);
            }

            _logger.LogInformation("Token revoked: {TokenType}", tokenType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke token");
            throw;
        }
    }

    /// <summary>
    /// Validate token
    /// </summary>
    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        return await _tokenValidator.ValidateTokenAsync(token);
    }

    /// <summary>
    /// Get user claims from user store
    /// </summary>
    private async Task<List<Claim>> GetUserClaimsAsync(string userId)
    {
        // This would typically fetch from a user store/database
        await Task.Delay(1);
        
        return new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, $"User_{userId}"),
            new Claim(ClaimTypes.Email, $"user_{userId}@example.com")
        };
    }

    private static string GenerateSecretKey()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[64];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}

/// <summary>
/// Token validation service
/// </summary>
public class TokenValidationService
{
    private readonly IConfiguration _configuration;
    private readonly HashSet<string> _blacklistedTokens;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public TokenValidationService(IConfiguration configuration)
    {
        _configuration = configuration;
        _blacklistedTokens = new HashSet<string>();
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        try
        {
            if (_blacklistedTokens.Contains(token))
                return null;

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["Authentication:Jwt:Issuer"],
                ValidAudience = _configuration["Authentication:Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(_configuration["Authentication:Jwt:SecretKey"] ?? "")),
                ClockSkew = TimeSpan.Zero
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);
            return await Task.FromResult(principal);
        }
        catch
        {
            return null;
        }
    }

    public async Task BlacklistTokenAsync(string token)
    {
        _blacklistedTokens.Add(token);
        await Task.CompletedTask;
    }
}

/// <summary>
/// Refresh token service
/// </summary>
public class RefreshTokenService
{
    private readonly Dictionary<string, RefreshTokenData> _refreshTokens;

    public RefreshTokenService()
    {
        _refreshTokens = new Dictionary<string, RefreshTokenData>();
    }

    public async Task<string> GenerateRefreshTokenAsync(string userId)
    {
        var refreshToken = GenerateSecureToken();
        
        _refreshTokens[refreshToken] = new RefreshTokenData
        {
            UserId = userId,
            Token = refreshToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        await Task.CompletedTask;
        return refreshToken;
    }

    public async Task<string?> ValidateRefreshTokenAsync(string refreshToken)
    {
        if (_refreshTokens.TryGetValue(refreshToken, out var tokenData))
        {
            if (tokenData.ExpiresAt > DateTime.UtcNow && !tokenData.IsRevoked)
            {
                await Task.CompletedTask;
                return tokenData.UserId;
            }
        }

        return null;
    }

    public async Task StoreRefreshTokenAsync(string userId, string refreshToken)
    {
        _refreshTokens[refreshToken] = new RefreshTokenData
        {
            UserId = userId,
            Token = refreshToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        await Task.CompletedTask;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken)
    {
        if (_refreshTokens.TryGetValue(refreshToken, out var tokenData))
        {
            tokenData.IsRevoked = true;
            tokenData.RevokedAt = DateTime.UtcNow;
        }

        await Task.CompletedTask;
    }

    public async Task RotateRefreshTokenAsync(string oldToken, string newToken)
    {
        await RevokeRefreshTokenAsync(oldToken);
        
        if (_refreshTokens.TryGetValue(oldToken, out var oldTokenData))
        {
            await StoreRefreshTokenAsync(oldTokenData.UserId, newToken);
        }
    }

    private string GenerateSecureToken()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}

/// <summary>
/// User session manager
/// </summary>
public class UserSessionManager
{
    private readonly Dictionary<string, UserSession> _sessions;

    public UserSessionManager()
    {
        _sessions = new Dictionary<string, UserSession>();
    }

    public async Task CreateSessionAsync(string userId, string token, DeviceInfo? deviceInfo)
    {
        var sessionId = Guid.NewGuid().ToString();
        
        _sessions[sessionId] = new UserSession
        {
            SessionId = sessionId,
            UserId = userId,
            Token = token,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            DeviceInfo = deviceInfo,
            IsActive = true
        };

        await Task.CompletedTask;
    }

    public async Task<UserSession?> GetSessionAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.LastActivityAt = DateTime.UtcNow;
            return await Task.FromResult(session);
        }

        return null;
    }

    public async Task EndSessionAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.IsActive = false;
            session.EndedAt = DateTime.UtcNow;
        }

        await Task.CompletedTask;
    }
}

/// <summary>
/// Security audit service
/// </summary>
public class SecurityAuditService
{
    private readonly ILogger _logger;
    private readonly List<SecurityAuditEntry> _auditLog;

    public SecurityAuditService(ILogger logger)
    {
        _logger = logger;
        _auditLog = new List<SecurityAuditEntry>();
    }

    public async Task LogAuthenticationAsync(string userId, string action, string? ipAddress)
    {
        var entry = new SecurityAuditEntry
        {
            UserId = userId,
            Action = action,
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow,
            Success = true
        };

        _auditLog.Add(entry);
        _logger.LogInformation("Security audit: {Action} for user {UserId} from {IpAddress}",
            action, userId, ipAddress);

        await Task.CompletedTask;
    }

    public async Task LogFailedAuthenticationAsync(string identifier, string reason, string? ipAddress)
    {
        var entry = new SecurityAuditEntry
        {
            UserId = identifier,
            Action = "AuthenticationFailed",
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow,
            Success = false,
            FailureReason = reason
        };

        _auditLog.Add(entry);
        _logger.LogWarning("Failed authentication: {Reason} for {Identifier} from {IpAddress}",
            reason, identifier, ipAddress);

        await Task.CompletedTask;
    }
}

/// <summary>
/// Custom authorization handler
/// </summary>
public class CustomAuthorizationHandler : AuthorizationHandler<CustomAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CustomAuthorizationRequirement requirement)
    {
        if (requirement.RequireMfa)
        {
            var mfaClaim = context.User.FindFirst("amr");
            if (mfaClaim?.Value == "mfa")
            {
                context.Succeed(requirement);
            }
        }
        else
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

// Supporting classes
public class AuthenticationToken
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public string? Scope { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class TokenRequest
{
    public string[]? Scopes { get; set; }
    public int? ExpirationMinutes { get; set; }
    public bool IncludeRefreshToken { get; set; }
    public Dictionary<string, string>? CustomClaims { get; set; }
    public string? IpAddress { get; set; }
    public DeviceInfo? DeviceInfo { get; set; }
}

public class RefreshTokenData
{
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public class UserSession
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DeviceInfo? DeviceInfo { get; set; }
    public bool IsActive { get; set; }
}

public class DeviceInfo
{
    public string? DeviceId { get; set; }
    public string? DeviceType { get; set; }
    public string? Browser { get; set; }
    public string? Os { get; set; }
    public string? IpAddress { get; set; }
}

public class SecurityAuditEntry
{
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
}

public class CustomAuthorizationRequirement : IAuthorizationRequirement
{
    public bool RequireMfa { get; set; }
}

public enum TokenType
{
    AccessToken,
    RefreshToken
}

public interface ITokenService
{
    Task<AuthenticationToken> GenerateTokenAsync(ClaimsPrincipal user, TokenRequest request);
    Task<AuthenticationToken> RefreshTokenAsync(string refreshToken);
    Task RevokeTokenAsync(string token, TokenType tokenType);
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token);
}

public class JwtTokenService : ITokenService
{
    private readonly OAuth2AuthenticationProvider _provider;

    public JwtTokenService(OAuth2AuthenticationProvider provider)
    {
        _provider = provider;
    }

    public async Task<AuthenticationToken> GenerateTokenAsync(ClaimsPrincipal user, TokenRequest request)
    {
        return await _provider.GenerateTokenAsync(user, request);
    }

    public async Task<AuthenticationToken> RefreshTokenAsync(string refreshToken)
    {
        return await _provider.RefreshTokenAsync(refreshToken);
    }

    public async Task RevokeTokenAsync(string token, TokenType tokenType)
    {
        await _provider.RevokeTokenAsync(token, tokenType);
    }

    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        return await _provider.ValidateTokenAsync(token);
    }
}
