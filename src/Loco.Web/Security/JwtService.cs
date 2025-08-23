using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Loco.Web.Security;

public interface IJwtService
{
    string GenerateToken(string userId, string email, string[] roles);
    ClaimsPrincipal? ValidateToken(string token);
    string GenerateRefreshToken();
    (string accessToken, string refreshToken) RefreshTokens(string refreshToken);
}

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _secretKey;
    private readonly int _accessTokenExpirationMinutes;
    private readonly int _refreshTokenExpirationDays;
    private readonly Dictionary<string, RefreshTokenInfo> _refreshTokens = new();

    private class RefreshTokenInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string[] Roles { get; set; } = Array.Empty<string>();
        public DateTime ExpiresAt { get; set; }
    }

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
        _issuer = configuration["Jwt:Issuer"] ?? "Loco";
        _audience = configuration["Jwt:Audience"] ?? "LocoAPI";
        _secretKey = configuration["Jwt:SecretKey"] ?? GenerateDefaultSecretKey();
        _accessTokenExpirationMinutes = int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"] ?? "15");
        _refreshTokenExpirationDays = int.Parse(configuration["Jwt:RefreshTokenExpirationDays"] ?? "30");
    }

    private string GenerateDefaultSecretKey()
    {
        // Generate a secure default key if not configured
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[64];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public string GenerateToken(string userId, string email, string[] roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var tokenHandler = new JwtSecurityTokenHandler();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            
            if (validatedToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha512, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }

    public string GenerateRefreshToken()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public (string accessToken, string refreshToken) RefreshTokens(string refreshToken)
    {
        if (!_refreshTokens.TryGetValue(refreshToken, out var tokenInfo))
        {
            throw new SecurityTokenException("Invalid refresh token");
        }

        if (tokenInfo.ExpiresAt < DateTime.UtcNow)
        {
            _refreshTokens.Remove(refreshToken);
            throw new SecurityTokenException("Refresh token expired");
        }

        // Remove old refresh token
        _refreshTokens.Remove(refreshToken);

        // Generate new tokens
        var newAccessToken = GenerateToken(tokenInfo.UserId, tokenInfo.Email, tokenInfo.Roles);
        var newRefreshToken = GenerateRefreshToken();

        // Store new refresh token
        _refreshTokens[newRefreshToken] = new RefreshTokenInfo
        {
            UserId = tokenInfo.UserId,
            Email = tokenInfo.Email,
            Roles = tokenInfo.Roles,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays)
        };

        return (newAccessToken, newRefreshToken);
    }

    public void StoreRefreshToken(string refreshToken, string userId, string email, string[] roles)
    {
        _refreshTokens[refreshToken] = new RefreshTokenInfo
        {
            UserId = userId,
            Email = email,
            Roles = roles,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays)
        };
    }

    public void RevokeRefreshToken(string refreshToken)
    {
        _refreshTokens.Remove(refreshToken);
    }

    public void RevokeAllUserTokens(string userId)
    {
        var tokensToRemove = _refreshTokens
            .Where(kvp => kvp.Value.UserId == userId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var token in tokensToRemove)
        {
            _refreshTokens.Remove(token);
        }
    }

    public void CleanupExpiredTokens()
    {
        var now = DateTime.UtcNow;
        var expiredTokens = _refreshTokens
            .Where(kvp => kvp.Value.ExpiresAt < now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var token in expiredTokens)
        {
            _refreshTokens.Remove(token);
        }
    }
}
