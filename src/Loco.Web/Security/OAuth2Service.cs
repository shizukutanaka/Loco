using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Loco.Web.Security;

public interface IOAuth2Service
{
    string GetAuthorizationUrl(string provider, string state);
    Task<OAuth2TokenResponse?> ExchangeCodeForTokenAsync(string provider, string code, string redirectUri);
    Task<OAuth2UserInfo?> GetUserInfoAsync(string provider, string accessToken);
    Task<OAuth2TokenResponse?> RefreshTokenAsync(string provider, string refreshToken);
    bool ValidateState(string state);
}

public class OAuth2TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string? RefreshToken { get; set; }
    public string? Scope { get; set; }
    public string? IdToken { get; set; }
}

public class OAuth2UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Picture { get; set; }
    public bool EmailVerified { get; set; }
    public Dictionary<string, object> AdditionalClaims { get; set; } = new();
}

public class OAuth2Provider
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AuthorizationEndpoint { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string UserInfoEndpoint { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string ResponseType { get; set; } = "code";
    public string GrantType { get; set; } = "authorization_code";
}

public class OAuth2Service : IOAuth2Service
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OAuth2Service> _logger;
    private readonly Dictionary<string, OAuth2Provider> _providers;
    private readonly Dictionary<string, DateTime> _stateTokens = new();
    private readonly TimeSpan _stateExpiration = TimeSpan.FromMinutes(10);

    public OAuth2Service(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<OAuth2Service> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _providers = LoadProviders();
    }

    private Dictionary<string, OAuth2Provider> LoadProviders()
    {
        var providers = new Dictionary<string, OAuth2Provider>(StringComparer.OrdinalIgnoreCase);

        // Google OAuth2
        if (_configuration["OAuth2:Google:ClientId"] is not null)
        {
            providers["google"] = new OAuth2Provider
            {
                ClientId = _configuration["OAuth2:Google:ClientId"]!,
                ClientSecret = _configuration["OAuth2:Google:ClientSecret"] ?? string.Empty,
                AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
                TokenEndpoint = "https://oauth2.googleapis.com/token",
                UserInfoEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo",
                Scope = "openid email profile"
            };
        }

        // GitHub OAuth2
        if (_configuration["OAuth2:GitHub:ClientId"] is not null)
        {
            providers["github"] = new OAuth2Provider
            {
                ClientId = _configuration["OAuth2:GitHub:ClientId"]!,
                ClientSecret = _configuration["OAuth2:GitHub:ClientSecret"] ?? string.Empty,
                AuthorizationEndpoint = "https://github.com/login/oauth/authorize",
                TokenEndpoint = "https://github.com/login/oauth/access_token",
                UserInfoEndpoint = "https://api.github.com/user",
                Scope = "read:user user:email"
            };
        }

        // Microsoft OAuth2
        if (_configuration["OAuth2:Microsoft:ClientId"] is not null)
        {
            providers["microsoft"] = new OAuth2Provider
            {
                ClientId = _configuration["OAuth2:Microsoft:ClientId"]!,
                ClientSecret = _configuration["OAuth2:Microsoft:ClientSecret"] ?? string.Empty,
                AuthorizationEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
                TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                UserInfoEndpoint = "https://graph.microsoft.com/v1.0/me",
                Scope = "openid email profile User.Read"
            };
        }

        // Auth0
        if (_configuration["OAuth2:Auth0:Domain"] is not null)
        {
            var domain = _configuration["OAuth2:Auth0:Domain"]!;
            providers["auth0"] = new OAuth2Provider
            {
                ClientId = _configuration["OAuth2:Auth0:ClientId"] ?? string.Empty,
                ClientSecret = _configuration["OAuth2:Auth0:ClientSecret"] ?? string.Empty,
                AuthorizationEndpoint = $"https://{domain}/authorize",
                TokenEndpoint = $"https://{domain}/oauth/token",
                UserInfoEndpoint = $"https://{domain}/userinfo",
                Scope = "openid email profile"
            };
        }

        // Okta
        if (_configuration["OAuth2:Okta:Domain"] is not null)
        {
            var domain = _configuration["OAuth2:Okta:Domain"]!;
            providers["okta"] = new OAuth2Provider
            {
                ClientId = _configuration["OAuth2:Okta:ClientId"] ?? string.Empty,
                ClientSecret = _configuration["OAuth2:Okta:ClientSecret"] ?? string.Empty,
                AuthorizationEndpoint = $"https://{domain}/oauth2/default/v1/authorize",
                TokenEndpoint = $"https://{domain}/oauth2/default/v1/token",
                UserInfoEndpoint = $"https://{domain}/oauth2/default/v1/userinfo",
                Scope = "openid email profile"
            };
        }

        return providers;
    }

    public string GetAuthorizationUrl(string provider, string state)
    {
        if (!_providers.TryGetValue(provider, out var providerConfig))
        {
            throw new ArgumentException($"Provider '{provider}' not configured");
        }

        // Store state for validation
        _stateTokens[state] = DateTime.UtcNow.Add(_stateExpiration);
        CleanupExpiredStates();

        var redirectUri = _configuration[$"OAuth2:{provider}:RedirectUri"] ?? "http://localhost:5000/auth/callback";

        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = providerConfig.ClientId,
            ["response_type"] = providerConfig.ResponseType,
            ["scope"] = providerConfig.Scope,
            ["redirect_uri"] = redirectUri,
            ["state"] = state
        };

        // Provider-specific parameters
        if (provider.Equals("google", StringComparison.OrdinalIgnoreCase))
        {
            queryParams["access_type"] = "offline";
            queryParams["prompt"] = "consent";
        }

        var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{providerConfig.AuthorizationEndpoint}?{queryString}";
    }

    public async Task<OAuth2TokenResponse?> ExchangeCodeForTokenAsync(string provider, string code, string redirectUri)
    {
        if (!_providers.TryGetValue(provider, out var providerConfig))
        {
            throw new ArgumentException($"Provider '{provider}' not configured");
        }

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            
            var tokenRequest = new Dictionary<string, string>
            {
                ["grant_type"] = providerConfig.GrantType,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = providerConfig.ClientId,
                ["client_secret"] = providerConfig.ClientSecret
            };

            var request = new HttpRequestMessage(HttpMethod.Post, providerConfig.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(tokenRequest)
            };

            // GitHub requires Accept header
            if (provider.Equals("github", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }

            var response = await httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("OAuth2 token exchange failed for {Provider}: {Error}", provider, error);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);

            if (tokenData == null)
            {
                return null;
            }

            return new OAuth2TokenResponse
            {
                AccessToken = tokenData.TryGetValue("access_token", out var accessToken) ? accessToken.GetString() ?? string.Empty : string.Empty,
                TokenType = tokenData.TryGetValue("token_type", out var tokenType) ? tokenType.GetString() ?? "Bearer" : "Bearer",
                ExpiresIn = tokenData.TryGetValue("expires_in", out var expiresIn) ? expiresIn.GetInt32() : 3600,
                RefreshToken = tokenData.TryGetValue("refresh_token", out var refreshToken) ? refreshToken.GetString() : null,
                Scope = tokenData.TryGetValue("scope", out var scope) ? scope.GetString() : null,
                IdToken = tokenData.TryGetValue("id_token", out var idToken) ? idToken.GetString() : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging code for token with {Provider}", provider);
            return null;
        }
    }

    public async Task<OAuth2UserInfo?> GetUserInfoAsync(string provider, string accessToken)
    {
        if (!_providers.TryGetValue(provider, out var providerConfig))
        {
            throw new ArgumentException($"Provider '{provider}' not configured");
        }

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.GetAsync(providerConfig.UserInfoEndpoint);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get user info from {Provider}: {Error}", provider, error);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var userData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);

            if (userData == null)
            {
                return null;
            }

            // Map provider-specific fields to standard user info
            var userInfo = new OAuth2UserInfo();

            switch (provider.ToLowerInvariant())
            {
                case "google":
                    userInfo.Id = userData.TryGetValue("id", out var googleId) ? googleId.GetString() ?? string.Empty : string.Empty;
                    userInfo.Email = userData.TryGetValue("email", out var googleEmail) ? googleEmail.GetString() ?? string.Empty : string.Empty;
                    userInfo.Name = userData.TryGetValue("name", out var googleName) ? googleName.GetString() ?? string.Empty : string.Empty;
                    userInfo.Picture = userData.TryGetValue("picture", out var googlePicture) ? googlePicture.GetString() : null;
                    userInfo.EmailVerified = userData.TryGetValue("verified_email", out var googleVerified) && googleVerified.GetBoolean();
                    break;

                case "github":
                    userInfo.Id = userData.TryGetValue("id", out var githubId) ? githubId.GetInt64().ToString() : string.Empty;
                    userInfo.Email = userData.TryGetValue("email", out var githubEmail) ? githubEmail.GetString() ?? string.Empty : string.Empty;
                    userInfo.Name = userData.TryGetValue("name", out var githubName) ? githubName.GetString() ?? string.Empty : string.Empty;
                    userInfo.Picture = userData.TryGetValue("avatar_url", out var githubAvatar) ? githubAvatar.GetString() : null;
                    userInfo.EmailVerified = true; // GitHub doesn't provide this
                    break;

                case "microsoft":
                    userInfo.Id = userData.TryGetValue("id", out var msId) ? msId.GetString() ?? string.Empty : string.Empty;
                    userInfo.Email = userData.TryGetValue("mail", out var msMail) ? msMail.GetString() ?? string.Empty : 
                                    userData.TryGetValue("userPrincipalName", out var msUpn) ? msUpn.GetString() ?? string.Empty : string.Empty;
                    userInfo.Name = userData.TryGetValue("displayName", out var msName) ? msName.GetString() ?? string.Empty : string.Empty;
                    userInfo.EmailVerified = true; // Microsoft doesn't provide this directly
                    break;

                case "auth0":
                case "okta":
                    userInfo.Id = userData.TryGetValue("sub", out var sub) ? sub.GetString() ?? string.Empty : string.Empty;
                    userInfo.Email = userData.TryGetValue("email", out var email) ? email.GetString() ?? string.Empty : string.Empty;
                    userInfo.Name = userData.TryGetValue("name", out var name) ? name.GetString() ?? string.Empty : string.Empty;
                    userInfo.Picture = userData.TryGetValue("picture", out var picture) ? picture.GetString() : null;
                    userInfo.EmailVerified = userData.TryGetValue("email_verified", out var emailVerified) && emailVerified.GetBoolean();
                    break;
            }

            // Store additional claims
            foreach (var claim in userData)
            {
                if (!new[] { "id", "sub", "email", "mail", "userPrincipalName", "name", "displayName", "picture", "avatar_url", "email_verified", "verified_email" }.Contains(claim.Key))
                {
                    userInfo.AdditionalClaims[claim.Key] = claim.Value.ToString();
                }
            }

            return userInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user info from {Provider}", provider);
            return null;
        }
    }

    public async Task<OAuth2TokenResponse?> RefreshTokenAsync(string provider, string refreshToken)
    {
        if (!_providers.TryGetValue(provider, out var providerConfig))
        {
            throw new ArgumentException($"Provider '{provider}' not configured");
        }

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            
            var tokenRequest = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = providerConfig.ClientId,
                ["client_secret"] = providerConfig.ClientSecret
            };

            var request = new HttpRequestMessage(HttpMethod.Post, providerConfig.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(tokenRequest)
            };

            var response = await httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("OAuth2 token refresh failed for {Provider}: {Error}", provider, error);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);

            if (tokenData == null)
            {
                return null;
            }

            return new OAuth2TokenResponse
            {
                AccessToken = tokenData.TryGetValue("access_token", out var accessToken) ? accessToken.GetString() ?? string.Empty : string.Empty,
                TokenType = tokenData.TryGetValue("token_type", out var tokenType) ? tokenType.GetString() ?? "Bearer" : "Bearer",
                ExpiresIn = tokenData.TryGetValue("expires_in", out var expiresIn) ? expiresIn.GetInt32() : 3600,
                RefreshToken = tokenData.TryGetValue("refresh_token", out var newRefreshToken) ? newRefreshToken.GetString() : refreshToken,
                Scope = tokenData.TryGetValue("scope", out var scope) ? scope.GetString() : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token with {Provider}", provider);
            return null;
        }
    }

    public bool ValidateState(string state)
    {
        if (_stateTokens.TryGetValue(state, out var expiration))
        {
            _stateTokens.Remove(state);
            return expiration > DateTime.UtcNow;
        }
        return false;
    }

    private void CleanupExpiredStates()
    {
        var now = DateTime.UtcNow;
        var expiredStates = _stateTokens.Where(kvp => kvp.Value < now).Select(kvp => kvp.Key).ToList();
        foreach (var state in expiredStates)
        {
            _stateTokens.Remove(state);
        }
    }

    public IEnumerable<string> GetConfiguredProviders()
    {
        return _providers.Keys;
    }

    public bool IsProviderConfigured(string provider)
    {
        return _providers.ContainsKey(provider);
    }
}
