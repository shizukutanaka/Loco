using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Loco.Gateway.Security;

/// <summary>
/// Enterprise OAuth2/OpenID Connect authentication service
/// </summary>
public class OAuth2AuthenticationService
{
    private readonly ILogger<OAuth2AuthenticationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TokenValidationParameters _tokenValidationParameters;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly Dictionary<string, OAuthProvider> _providers;
    private readonly ITokenCache _tokenCache;
    private readonly IRateLimiter _rateLimiter;

    public OAuth2AuthenticationService(
        ILogger<OAuth2AuthenticationService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ITokenCache tokenCache,
        IRateLimiter rateLimiter)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _tokenCache = tokenCache;
        _rateLimiter = rateLimiter;
        _tokenHandler = new JwtSecurityTokenHandler();
        _providers = new Dictionary<string, OAuthProvider>();
        
        InitializeProviders();
        _tokenValidationParameters = CreateTokenValidationParameters();
    }

    /// <summary>
    /// Authenticate user with OAuth2 provider
    /// </summary>
    public async Task<AuthenticationResult> AuthenticateAsync(string provider, string code, string? state = null)
    {
        if (!_providers.TryGetValue(provider.ToLower(), out var oauthProvider))
        {
            return new AuthenticationResult 
            { 
                Success = false, 
                Error = "Invalid provider" 
            };
        }

        try
        {
            // Exchange authorization code for tokens
            var tokens = await ExchangeCodeForTokensAsync(oauthProvider, code);
            
            if (tokens == null)
            {
                return new AuthenticationResult 
                { 
                    Success = false, 
                    Error = "Failed to exchange code for tokens" 
                };
            }

            // Validate ID token
            var principal = await ValidateTokenAsync(tokens.IdToken, oauthProvider);
            
            if (principal == null)
            {
                return new AuthenticationResult 
                { 
                    Success = false, 
                    Error = "Invalid ID token" 
                };
            }

            // Get user info
            var userInfo = await GetUserInfoAsync(oauthProvider, tokens.AccessToken);
            
            // Create internal JWT token
            var internalToken = GenerateInternalToken(principal, userInfo, provider);
            
            // Cache tokens
            await _tokenCache.SetAsync($"access_{principal.Identity!.Name}", tokens.AccessToken, 
                TimeSpan.FromSeconds(tokens.ExpiresIn));
            
            if (!string.IsNullOrEmpty(tokens.RefreshToken))
            {
                await _tokenCache.SetAsync($"refresh_{principal.Identity!.Name}", tokens.RefreshToken, 
                    TimeSpan.FromDays(30));
            }

            return new AuthenticationResult
            {
                Success = true,
                Token = internalToken,
                RefreshToken = tokens.RefreshToken,
                User = new UserInfo
                {
                    Id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "",
                    Email = principal.FindFirst(ClaimTypes.Email)?.Value ?? "",
                    Name = principal.FindFirst(ClaimTypes.Name)?.Value ?? "",
                    Provider = provider,
                    Claims = principal.Claims.ToDictionary(c => c.Type, c => c.Value)
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication failed for provider {Provider}", provider);
            return new AuthenticationResult 
            { 
                Success = false, 
                Error = "Authentication failed" 
            };
        }
    }

    /// <summary>
    /// Refresh access token
    /// </summary>
    public async Task<AuthenticationResult> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            // Validate refresh token
            var principal = _tokenHandler.ValidateToken(refreshToken, _tokenValidationParameters, out _);
            
            var provider = principal.FindFirst("provider")?.Value;
            if (string.IsNullOrEmpty(provider) || !_providers.TryGetValue(provider, out var oauthProvider))
            {
                return new AuthenticationResult { Success = false, Error = "Invalid provider" };
            }

            // Get stored refresh token
            var storedRefreshToken = await _tokenCache.GetAsync($"refresh_{principal.Identity!.Name}");
            
            if (storedRefreshToken != refreshToken)
            {
                return new AuthenticationResult { Success = false, Error = "Invalid refresh token" };
            }

            // Exchange refresh token for new tokens
            var tokens = await RefreshTokensAsync(oauthProvider, refreshToken);
            
            if (tokens == null)
            {
                return new AuthenticationResult { Success = false, Error = "Failed to refresh token" };
            }

            // Generate new internal token
            var newToken = GenerateInternalToken(principal, null, provider);
            
            // Update cache
            await _tokenCache.SetAsync($"access_{principal.Identity!.Name}", tokens.AccessToken, 
                TimeSpan.FromSeconds(tokens.ExpiresIn));
            
            if (!string.IsNullOrEmpty(tokens.RefreshToken))
            {
                await _tokenCache.SetAsync($"refresh_{principal.Identity!.Name}", tokens.RefreshToken, 
                    TimeSpan.FromDays(30));
            }

            return new AuthenticationResult
            {
                Success = true,
                Token = newToken,
                RefreshToken = tokens.RefreshToken ?? refreshToken
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh failed");
            return new AuthenticationResult { Success = false, Error = "Token refresh failed" };
        }
    }

    /// <summary>
    /// Validate JWT token
    /// </summary>
    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token, OAuthProvider? provider = null)
    {
        try
        {
            var validationParameters = provider != null 
                ? await GetProviderValidationParametersAsync(provider)
                : _tokenValidationParameters;

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            
            // Additional validation
            if (validatedToken is JwtSecurityToken jwtToken)
            {
                // Check token expiration
                if (jwtToken.ValidTo < DateTime.UtcNow)
                {
                    _logger.LogWarning("Token expired");
                    return null;
                }

                // Check issuer
                if (provider != null && !jwtToken.Issuer.Equals(provider.Issuer, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Invalid issuer: {Issuer}", jwtToken.Issuer);
                    return null;
                }
            }

            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token validation failed");
            return null;
        }
    }

    /// <summary>
    /// Revoke tokens
    /// </summary>
    public async Task<bool> RevokeTokenAsync(string token, string? refreshToken = null)
    {
        try
        {
            var principal = await ValidateTokenAsync(token);
            if (principal == null)
            {
                return false;
            }

            var provider = principal.FindFirst("provider")?.Value;
            if (string.IsNullOrEmpty(provider) || !_providers.TryGetValue(provider, out var oauthProvider))
            {
                return false;
            }

            // Revoke with provider if supported
            if (!string.IsNullOrEmpty(oauthProvider.RevocationEndpoint))
            {
                await RevokeWithProviderAsync(oauthProvider, token, refreshToken);
            }

            // Clear from cache
            var userId = principal.Identity!.Name;
            await _tokenCache.RemoveAsync($"access_{userId}");
            await _tokenCache.RemoveAsync($"refresh_{userId}");

            // Add to revocation list
            await _tokenCache.SetAsync($"revoked_{token}", "true", TimeSpan.FromHours(24));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token revocation failed");
            return false;
        }
    }

    /// <summary>
    /// Get authorization URL
    /// </summary>
    public string GetAuthorizationUrl(string provider, string redirectUri, string? state = null)
    {
        if (!_providers.TryGetValue(provider.ToLower(), out var oauthProvider))
        {
            throw new ArgumentException($"Invalid provider: {provider}");
        }

        var stateParam = state ?? GenerateState();
        var nonce = GenerateNonce();

        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = oauthProvider.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = string.Join(" ", oauthProvider.Scopes),
            ["state"] = stateParam
        };

        if (oauthProvider.SupportsOpenIdConnect)
        {
            parameters["nonce"] = nonce;
        }

        if (!string.IsNullOrEmpty(oauthProvider.Audience))
        {
            parameters["audience"] = oauthProvider.Audience;
        }

        var query = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        return $"{oauthProvider.AuthorizationEndpoint}?{query}";
    }

    private void InitializeProviders()
    {
        var providersConfig = _configuration.GetSection("OAuth2:Providers");
        
        foreach (var providerConfig in providersConfig.GetChildren())
        {
            var provider = new OAuthProvider
            {
                Name = providerConfig.Key,
                ClientId = providerConfig["ClientId"] ?? "",
                ClientSecret = providerConfig["ClientSecret"] ?? "",
                AuthorizationEndpoint = providerConfig["AuthorizationEndpoint"] ?? "",
                TokenEndpoint = providerConfig["TokenEndpoint"] ?? "",
                UserInfoEndpoint = providerConfig["UserInfoEndpoint"] ?? "",
                RevocationEndpoint = providerConfig["RevocationEndpoint"],
                Issuer = providerConfig["Issuer"] ?? "",
                Audience = providerConfig["Audience"],
                Scopes = providerConfig.GetSection("Scopes").Get<string[]>() ?? new[] { "openid", "profile", "email" },
                SupportsOpenIdConnect = providerConfig.GetValue<bool>("SupportsOpenIdConnect", true),
                JwksUri = providerConfig["JwksUri"]
            };

            _providers[provider.Name.ToLower()] = provider;
        }

        // Add default providers if not configured
        if (!_providers.ContainsKey("google"))
        {
            _providers["google"] = new OAuthProvider
            {
                Name = "Google",
                ClientId = _configuration["OAuth2:Google:ClientId"] ?? "",
                ClientSecret = _configuration["OAuth2:Google:ClientSecret"] ?? "",
                AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
                TokenEndpoint = "https://oauth2.googleapis.com/token",
                UserInfoEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo",
                RevocationEndpoint = "https://oauth2.googleapis.com/revoke",
                Issuer = "https://accounts.google.com",
                Scopes = new[] { "openid", "profile", "email" },
                SupportsOpenIdConnect = true,
                JwksUri = "https://www.googleapis.com/oauth2/v3/certs"
            };
        }

        if (!_providers.ContainsKey("github"))
        {
            _providers["github"] = new OAuthProvider
            {
                Name = "GitHub",
                ClientId = _configuration["OAuth2:GitHub:ClientId"] ?? "",
                ClientSecret = _configuration["OAuth2:GitHub:ClientSecret"] ?? "",
                AuthorizationEndpoint = "https://github.com/login/oauth/authorize",
                TokenEndpoint = "https://github.com/login/oauth/access_token",
                UserInfoEndpoint = "https://api.github.com/user",
                Issuer = "https://github.com",
                Scopes = new[] { "user:email", "read:user" },
                SupportsOpenIdConnect = false
            };
        }

        if (!_providers.ContainsKey("microsoft"))
        {
            _providers["microsoft"] = new OAuthProvider
            {
                Name = "Microsoft",
                ClientId = _configuration["OAuth2:Microsoft:ClientId"] ?? "",
                ClientSecret = _configuration["OAuth2:Microsoft:ClientSecret"] ?? "",
                AuthorizationEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
                TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                UserInfoEndpoint = "https://graph.microsoft.com/v1.0/me",
                Issuer = "https://login.microsoftonline.com/common/v2.0",
                Scopes = new[] { "openid", "profile", "email", "User.Read" },
                SupportsOpenIdConnect = true,
                JwksUri = "https://login.microsoftonline.com/common/discovery/v2.0/keys"
            };
        }
    }

    private TokenValidationParameters CreateTokenValidationParameters()
    {
        var key = Encoding.UTF8.GetBytes(_configuration["JWT:Secret"] ?? GenerateSecret());
        
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _configuration["JWT:Issuer"] ?? "https://loco.io",
            ValidAudience = _configuration["JWT:Audience"] ?? "https://loco.io",
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    }

    private async Task<TokenValidationParameters> GetProviderValidationParametersAsync(OAuthProvider provider)
    {
        if (string.IsNullOrEmpty(provider.JwksUri))
        {
            return _tokenValidationParameters;
        }

        // Get signing keys from JWKS endpoint
        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            provider.JwksUri,
            new OpenIdConnectConfigurationRetriever(),
            _httpClientFactory.CreateClient());

        var config = await configManager.GetConfigurationAsync();
        
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = provider.Issuer,
            ValidateAudience = !string.IsNullOrEmpty(provider.Audience),
            ValidAudience = provider.Audience,
            ValidateLifetime = true,
            IssuerSigningKeys = config.SigningKeys,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    }

    private async Task<TokenResponse?> ExchangeCodeForTokensAsync(OAuthProvider provider, string code)
    {
        var httpClient = _httpClientFactory.CreateClient();
        
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = provider.ClientId,
            ["client_secret"] = provider.ClientSecret
        };

        var content = new FormUrlEncodedContent(parameters);
        var response = await httpClient.PostAsync(provider.TokenEndpoint, content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Token exchange failed: {Error}", error);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TokenResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private async Task<TokenResponse?> RefreshTokensAsync(OAuthProvider provider, string refreshToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = provider.ClientId,
            ["client_secret"] = provider.ClientSecret
        };

        var content = new FormUrlEncodedContent(parameters);
        var response = await httpClient.PostAsync(provider.TokenEndpoint, content);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TokenResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private async Task<Dictionary<string, object>> GetUserInfoAsync(OAuthProvider provider, string accessToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.GetAsync(provider.UserInfoEndpoint);
        
        if (!response.IsSuccessStatusCode)
        {
            return new Dictionary<string, object>();
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
    }

    private async Task RevokeWithProviderAsync(OAuthProvider provider, string token, string? refreshToken)
    {
        if (string.IsNullOrEmpty(provider.RevocationEndpoint))
        {
            return;
        }

        var httpClient = _httpClientFactory.CreateClient();
        
        // Revoke access token
        var parameters = new Dictionary<string, string>
        {
            ["token"] = token,
            ["token_type_hint"] = "access_token"
        };

        await httpClient.PostAsync(provider.RevocationEndpoint, new FormUrlEncodedContent(parameters));

        // Revoke refresh token if provided
        if (!string.IsNullOrEmpty(refreshToken))
        {
            parameters["token"] = refreshToken;
            parameters["token_type_hint"] = "refresh_token";
            await httpClient.PostAsync(provider.RevocationEndpoint, new FormUrlEncodedContent(parameters));
        }
    }

    private string GenerateInternalToken(ClaimsPrincipal principal, Dictionary<string, object>? userInfo, string provider)
    {
        var key = Encoding.UTF8.GetBytes(_configuration["JWT:Secret"] ?? GenerateSecret());
        var signingKey = new SymmetricSecurityKey(key);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>(principal.Claims)
        {
            new Claim("provider", provider),
            new Claim("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        // Add user info claims
        if (userInfo != null)
        {
            foreach (var (claimType, value) in userInfo)
            {
                if (!claims.Any(c => c.Type == claimType))
                {
                    claims.Add(new Claim(claimType, value.ToString() ?? ""));
                }
            }
        }

        var token = new JwtSecurityToken(
            issuer: _configuration["JWT:Issuer"] ?? "https://loco.io",
            audience: _configuration["JWT:Audience"] ?? "https://loco.io",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return _tokenHandler.WriteToken(token);
    }

    private string GenerateState()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private string GenerateNonce()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private string GenerateSecret()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}

// Supporting classes
public class OAuthProvider
{
    public string Name { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AuthorizationEndpoint { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string UserInfoEndpoint { get; set; } = string.Empty;
    public string? RevocationEndpoint { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public string? Audience { get; set; }
    public string[] Scopes { get; set; } = Array.Empty<string>();
    public bool SupportsOpenIdConnect { get; set; }
    public string? JwksUri { get; set; }
}

public class AuthenticationResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public UserInfo? User { get; set; }
    public string? Error { get; set; }
}

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public Dictionary<string, string> Claims { get; set; } = new();
}

public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string? IdToken { get; set; }
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
}

public interface ITokenCache
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value, TimeSpan expiration);
    Task RemoveAsync(string key);
}

public interface IRateLimiter
{
    Task<bool> AllowRequestAsync(string clientId);
}

/// <summary>
/// In-memory token cache implementation
/// </summary>
public class InMemoryTokenCache : ITokenCache
{
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly object _lock = new();

    public Task<string?> GetAsync(string key)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry) && entry.Expiration > DateTime.UtcNow)
            {
                return Task.FromResult<string?>(entry.Value);
            }
            return Task.FromResult<string?>(null);
        }
    }

    public Task SetAsync(string key, string value, TimeSpan expiration)
    {
        lock (_lock)
        {
            _cache[key] = new CacheEntry
            {
                Value = value,
                Expiration = DateTime.UtcNow.Add(expiration)
            };
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        lock (_lock)
        {
            _cache.Remove(key);
        }
        return Task.CompletedTask;
    }

    private class CacheEntry
    {
        public string Value { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
    }
}
