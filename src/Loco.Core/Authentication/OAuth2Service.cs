using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Authentication
{
    public interface IOAuth2Service
    {
        string GetAuthorizationUrl(string provider, string state, string redirectUri);
        Task<OAuth2TokenResponse> ExchangeCodeForTokenAsync(string provider, string code, string redirectUri);
        Task<OAuth2UserInfo> GetUserInfoAsync(string provider, string accessToken);
        Task<AuthenticationResult> AuthenticateWithOAuth2Async(string provider, string code, string redirectUri);
        bool IsProviderSupported(string provider);
    }

    public class OAuth2Service : IOAuth2Service
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<OAuth2Service> _logger;
        private readonly HttpClient _httpClient;
        private readonly IUserRepository _userRepository;
        private readonly IJwtAuthenticationService _jwtService;
        private readonly Dictionary<string, OAuth2Provider> _providers;

        public OAuth2Service(
            IConfiguration configuration,
            ILogger<OAuth2Service> logger,
            HttpClient httpClient,
            IUserRepository userRepository,
            IJwtAuthenticationService jwtService)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;
            _userRepository = userRepository;
            _jwtService = jwtService;
            _providers = LoadProviders();
        }

        private Dictionary<string, OAuth2Provider> LoadProviders()
        {
            var providers = new Dictionary<string, OAuth2Provider>(StringComparer.OrdinalIgnoreCase);

            var googleProvider = new OAuth2Provider
            {
                Name = "Google",
                ClientId = _configuration["OAuth2:Google:ClientId"],
                ClientSecret = _configuration["OAuth2:Google:ClientSecret"],
                AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
                TokenEndpoint = "https://oauth2.googleapis.com/token",
                UserInfoEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo",
                Scopes = new[] { "openid", "email", "profile" }
            };
            if (!string.IsNullOrEmpty(googleProvider.ClientId))
                providers["google"] = googleProvider;

            var microsoftProvider = new OAuth2Provider
            {
                Name = "Microsoft",
                ClientId = _configuration["OAuth2:Microsoft:ClientId"],
                ClientSecret = _configuration["OAuth2:Microsoft:ClientSecret"],
                AuthorizationEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
                TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                UserInfoEndpoint = "https://graph.microsoft.com/v1.0/me",
                Scopes = new[] { "openid", "email", "profile", "User.Read" }
            };
            if (!string.IsNullOrEmpty(microsoftProvider.ClientId))
                providers["microsoft"] = microsoftProvider;

            var githubProvider = new OAuth2Provider
            {
                Name = "GitHub",
                ClientId = _configuration["OAuth2:GitHub:ClientId"],
                ClientSecret = _configuration["OAuth2:GitHub:ClientSecret"],
                AuthorizationEndpoint = "https://github.com/login/oauth/authorize",
                TokenEndpoint = "https://github.com/login/oauth/access_token",
                UserInfoEndpoint = "https://api.github.com/user",
                Scopes = new[] { "read:user", "user:email" }
            };
            if (!string.IsNullOrEmpty(githubProvider.ClientId))
                providers["github"] = githubProvider;

            return providers;
        }

        public string GetAuthorizationUrl(string provider, string state, string redirectUri)
        {
            if (!_providers.TryGetValue(provider, out var oauthProvider))
            {
                throw new ArgumentException($"Unsupported provider: {provider}");
            }

            var queryParams = new Dictionary<string, string>
            {
                ["client_id"] = oauthProvider.ClientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = string.Join(" ", oauthProvider.Scopes),
                ["state"] = state,
                ["access_type"] = "offline",
                ["prompt"] = "consent"
            };

            var queryString = string.Join("&", queryParams.Select(kvp => 
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

            return $"{oauthProvider.AuthorizationEndpoint}?{queryString}";
        }

        public async Task<OAuth2TokenResponse> ExchangeCodeForTokenAsync(string provider, string code, string redirectUri)
        {
            try
            {
                if (!_providers.TryGetValue(provider, out var oauthProvider))
                {
                    throw new ArgumentException($"Unsupported provider: {provider}");
                }

                var tokenRequest = new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = redirectUri,
                    ["client_id"] = oauthProvider.ClientId,
                    ["client_secret"] = oauthProvider.ClientSecret
                };

                var content = new FormUrlEncodedContent(tokenRequest);
                
                if (provider.Equals("github", StringComparison.OrdinalIgnoreCase))
                {
                    _httpClient.DefaultRequestHeaders.Accept.Clear();
                    _httpClient.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                }

                var response = await _httpClient.PostAsync(oauthProvider.TokenEndpoint, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<OAuth2TokenResponse>(responseContent);

                _logger.LogInformation("Successfully exchanged code for token with provider: {Provider}", provider);
                return tokenResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exchanging code for token with provider: {Provider}", provider);
                throw;
            }
        }

        public async Task<OAuth2UserInfo> GetUserInfoAsync(string provider, string accessToken)
        {
            try
            {
                if (!_providers.TryGetValue(provider, out var oauthProvider))
                {
                    throw new ArgumentException($"Unsupported provider: {provider}");
                }

                var request = new HttpRequestMessage(HttpMethod.Get, oauthProvider.UserInfoEndpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var userInfoData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent);

                var userInfo = new OAuth2UserInfo
                {
                    Provider = provider
                };

                switch (provider.ToLower())
                {
                    case "google":
                        userInfo.Id = userInfoData["id"].GetString();
                        userInfo.Email = userInfoData["email"].GetString();
                        userInfo.Name = userInfoData["name"].GetString();
                        userInfo.Picture = userInfoData.ContainsKey("picture") ? userInfoData["picture"].GetString() : null;
                        userInfo.EmailVerified = userInfoData.ContainsKey("verified_email") ? userInfoData["verified_email"].GetBoolean() : false;
                        break;

                    case "microsoft":
                        userInfo.Id = userInfoData["id"].GetString();
                        userInfo.Email = userInfoData.ContainsKey("mail") ? userInfoData["mail"].GetString() : userInfoData["userPrincipalName"].GetString();
                        userInfo.Name = userInfoData["displayName"].GetString();
                        userInfo.EmailVerified = true;
                        break;

                    case "github":
                        userInfo.Id = userInfoData["id"].GetInt64().ToString();
                        userInfo.Email = userInfoData.ContainsKey("email") ? userInfoData["email"].GetString() : null;
                        userInfo.Name = userInfoData["name"].GetString() ?? userInfoData["login"].GetString();
                        userInfo.Picture = userInfoData.ContainsKey("avatar_url") ? userInfoData["avatar_url"].GetString() : null;
                        userInfo.EmailVerified = userInfoData.ContainsKey("email") && !string.IsNullOrEmpty(userInfoData["email"].GetString());
                        
                        if (string.IsNullOrEmpty(userInfo.Email))
                        {
                            userInfo.Email = await GetGitHubEmailAsync(accessToken);
                        }
                        break;
                }

                _logger.LogInformation("Retrieved user info from provider: {Provider}, UserId: {UserId}", provider, userInfo.Id);
                return userInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user info from provider: {Provider}", provider);
                throw;
            }
        }

        private async Task<string> GetGitHubEmailAsync(string accessToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return null;

                var responseContent = await response.Content.ReadAsStringAsync();
                var emails = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(responseContent);
                
                var primaryEmail = emails?.FirstOrDefault(e => 
                    e.ContainsKey("primary") && e["primary"].GetBoolean() && 
                    e.ContainsKey("verified") && e["verified"].GetBoolean());

                return primaryEmail?.ContainsKey("email") == true ? primaryEmail["email"].GetString() : null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<AuthenticationResult> AuthenticateWithOAuth2Async(string provider, string code, string redirectUri)
        {
            try
            {
                var tokenResponse = await ExchangeCodeForTokenAsync(provider, code, redirectUri);
                var userInfo = await GetUserInfoAsync(provider, tokenResponse.AccessToken);

                if (string.IsNullOrEmpty(userInfo.Email))
                {
                    _logger.LogWarning("No email address received from OAuth2 provider: {Provider}", provider);
                    return new AuthenticationResult 
                    { 
                        Success = false, 
                        Error = "Email address is required for authentication" 
                    };
                }

                var user = await _userRepository.GetByEmailAsync(userInfo.Email);
                if (user == null)
                {
                    user = new User
                    {
                        Id = Guid.NewGuid(),
                        Username = userInfo.Email.Split('@')[0] + "_" + provider.ToLower(),
                        Email = userInfo.Email,
                        PasswordHash = null,
                        IsLocked = false,
                        Roles = new List<string> { "User" },
                        Permissions = new List<string>(),
                        OAuth2Accounts = new List<OAuth2Account>
                        {
                            new OAuth2Account
                            {
                                Provider = provider,
                                ProviderId = userInfo.Id,
                                AccessToken = tokenResponse.AccessToken,
                                RefreshToken = tokenResponse.RefreshToken,
                                ExpiresAt = tokenResponse.ExpiresIn.HasValue 
                                    ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn.Value)
                                    : DateTime.UtcNow.AddHours(1)
                            }
                        }
                    };

                    await _userRepository.CreateAsync(user);
                    _logger.LogInformation("Created new user from OAuth2: {Email}, Provider: {Provider}", userInfo.Email, provider);
                }
                else
                {
                    var oauth2Account = user.OAuth2Accounts?.FirstOrDefault(a => 
                        a.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));

                    if (oauth2Account == null)
                    {
                        oauth2Account = new OAuth2Account
                        {
                            Provider = provider,
                            ProviderId = userInfo.Id,
                            AccessToken = tokenResponse.AccessToken,
                            RefreshToken = tokenResponse.RefreshToken,
                            ExpiresAt = tokenResponse.ExpiresIn.HasValue
                                ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn.Value)
                                : DateTime.UtcNow.AddHours(1)
                        };
                        user.OAuth2Accounts ??= new List<OAuth2Account>();
                        user.OAuth2Accounts.Add(oauth2Account);
                    }
                    else
                    {
                        oauth2Account.AccessToken = tokenResponse.AccessToken;
                        oauth2Account.RefreshToken = tokenResponse.RefreshToken ?? oauth2Account.RefreshToken;
                        oauth2Account.ExpiresAt = tokenResponse.ExpiresIn.HasValue
                            ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn.Value)
                            : DateTime.UtcNow.AddHours(1);
                    }

                    await _userRepository.UpdateOAuth2AccountAsync(user.Id, oauth2Account);
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("oauth2_provider", provider),
                    new Claim("oauth2_id", userInfo.Id),
                    new Claim("jti", Guid.NewGuid().ToString()),
                    new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
                };

                foreach (var role in user.Roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                var accessToken = _jwtService.GenerateAccessToken(claims);
                var refreshToken = _jwtService.GenerateRefreshToken();

                _logger.LogInformation("User authenticated via OAuth2: {Email}, Provider: {Provider}", user.Email, provider);

                return new AuthenticationResult
                {
                    Success = true,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresIn = 900,
                    TokenType = "Bearer",
                    User = new AuthenticatedUser
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = user.Email,
                        Roles = user.Roles,
                        Permissions = user.Permissions
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OAuth2 authentication failed for provider: {Provider}", provider);
                return new AuthenticationResult 
                { 
                    Success = false, 
                    Error = "OAuth2 authentication failed" 
                };
            }
        }

        public bool IsProviderSupported(string provider)
        {
            return _providers.ContainsKey(provider);
        }
    }

    public class OAuth2Provider
    {
        public string Name { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string AuthorizationEndpoint { get; set; }
        public string TokenEndpoint { get; set; }
        public string UserInfoEndpoint { get; set; }
        public string[] Scopes { get; set; }
    }

    public class OAuth2TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [JsonPropertyName("scope")]
        public string Scope { get; set; }
    }

    public class OAuth2UserInfo
    {
        public string Provider { get; set; }
        public string Id { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public string Picture { get; set; }
        public bool EmailVerified { get; set; }
    }

    public class OAuth2Account
    {
        public string Provider { get; set; }
        public string ProviderId { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public static class UserRepositoryExtensions
    {
        public static async Task<User> GetByEmailAsync(this IUserRepository repository, string email)
        {
            return await Task.FromResult(default(User));
        }

        public static async Task CreateAsync(this IUserRepository repository, User user)
        {
            await Task.CompletedTask;
        }

        public static async Task UpdateOAuth2AccountAsync(this IUserRepository repository, Guid userId, OAuth2Account account)
        {
            await Task.CompletedTask;
        }
    }
}