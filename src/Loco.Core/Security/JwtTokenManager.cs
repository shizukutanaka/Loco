using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Loco.Core.Security;

/// <summary>
/// Advanced JWT token manager with refresh token support
/// </summary>
public interface IJwtTokenManager
{
    /// <summary>
    /// Generates a new JWT token
    /// </summary>
    Task<TokenResponse> GenerateTokenAsync(TokenRequest request);

    /// <summary>
    /// Refreshes an expired token
    /// </summary>
    Task<TokenResponse?> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Validates a token
    /// </summary>
    Task<bool> ValidateTokenAsync(string token);

    /// <summary>
    /// Revokes a token
    /// </summary>
    Task<bool> RevokeTokenAsync(string token);

    /// <summary>
    /// Gets token claims
    /// </summary>
    Task<IEnumerable<Claim>?> GetTokenClaimsAsync(string token);
}

/// <summary>
/// JWT token request
/// </summary>
public class TokenRequest
{
    /// <summary>
    /// Subject (user ID)
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Scopes for authorization
    /// </summary>
    public List<string> Scopes { get; set; } = new();

    /// <summary>
    /// Issuer
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Audience
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Token lifetime
    /// </summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Additional claims
    /// </summary>
    public Dictionary<string, object> AdditionalClaims { get; set; } = new();
}

/// <summary>
/// JWT token response
/// </summary>
public class TokenResponse
{
    /// <summary>
    /// Access token
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Token type
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Expires in (seconds)
    /// </summary>
    public int ExpiresIn { get; set; }
}

/// <summary>
/// Advanced JWT token manager implementation
/// </summary>
public class JwtTokenManager : IJwtTokenManager
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly ILogger<JwtTokenManager> _logger;
    private readonly HashSet<string> _revokedTokens = new();

    public JwtTokenManager(
        string secretKey,
        string issuer,
        string audience,
        ILogger<JwtTokenManager> logger)
    {
        _secretKey = secretKey;
        _issuer = issuer;
        _audience = audience;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TokenResponse> GenerateTokenAsync(TokenRequest request)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, request.Subject),
                new Claim("sub", request.Subject),
                new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
                new Claim("nbf", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            // Add scopes
            foreach (var scope in request.Scopes)
            {
                claims.Add(new Claim("scope", scope));
            }

            // Add additional claims
            foreach (var kvp in request.AdditionalClaims)
            {
                claims.Add(new Claim(kvp.Key, kvp.Value?.ToString() ?? string.Empty));
            }

            var token = new JwtSecurityToken(
                issuer: request.Issuer ?? _issuer,
                audience: request.Audience ?? _audience,
                claims: claims,
                expires: DateTime.UtcNow.Add(request.TokenLifetime),
                signingCredentials: credentials);

            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
            var refreshToken = GenerateRefreshToken();

            _logger.LogInformation(
                "Token generated for subject: {Subject}, Scopes: {Scopes}",
                request.Subject, string.Join(", ", request.Scopes));

            return new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenType = "Bearer",
                ExpiresIn = (int)request.TokenLifetime.TotalSeconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate token for subject: {Subject}", request.Subject);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<TokenResponse?> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                _logger.LogWarning("Refresh token is empty");
                return null;
            }

            // Simplified refresh logic - in production, validate refresh token against storage
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ReadJwtToken(refreshToken);

            var subjectClaim = principal.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            if (string.IsNullOrEmpty(subjectClaim))
            {
                _logger.LogWarning("Invalid refresh token: missing subject claim");
                return null;
            }

            var scopeClaims = principal.Claims
                .Where(c => c.Type == "scope")
                .Select(c => c.Value)
                .ToList();

            var request = new TokenRequest
            {
                Subject = subjectClaim,
                Scopes = scopeClaims,
                TokenLifetime = TimeSpan.FromHours(1)
            };

            return await GenerateTokenAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh token");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            if (_revokedTokens.Contains(token))
            {
                _logger.LogWarning("Token validation failed: token is revoked");
                return false;
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var handler = new JwtSecurityTokenHandler();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            handler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
            return validatedToken != null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Token validation failed");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RevokeTokenAsync(string token)
    {
        try
        {
            _revokedTokens.Add(token);
            _logger.LogInformation("Token revoked");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke token");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Claim>?> GetTokenClaimsAsync(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            return jwtToken.Claims;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract claims from token");
            return null;
        }
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
