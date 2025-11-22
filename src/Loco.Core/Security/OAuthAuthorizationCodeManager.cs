// Phase 3: OAuth 2.0 Authorization Code Manager
// Manages authorization code lifecycle (PKCE-aware)

using Loco.Core.DataAccess;
using Loco.Core.Models;
using System.Security.Cryptography;
using System.Text;

namespace Loco.Core.Security;

/// <summary>
/// OAuth 2.0 Authorization Code Manager
/// Handles authorization code generation, validation, and redemption
/// Implements PKCE (RFC 7636) support
/// </summary>
public interface IOAuthAuthorizationCodeManager
{
    Task<string> CreateAuthorizationCodeAsync(
        string clientId,
        string userId,
        string redirectUri,
        string scopes,
        string? codeChallenge = null,
        string? codeChallengeMethod = null,
        string? nonce = null,
        CancellationToken ct = default);

    Task<OAuthAuthorizationCodeEntity?> GetAuthorizationCodeAsync(
        string code,
        CancellationToken ct = default);

    Task<bool> ValidateAuthorizationCodeAsync(
        string code,
        string clientId,
        string redirectUri,
        CancellationToken ct = default);

    Task<bool> ValidateCodeChallengeAsync(
        string code,
        string codeVerifier,
        CancellationToken ct = default);

    Task<bool> RedeemAuthorizationCodeAsync(
        string code,
        CancellationToken ct = default);

    Task<bool> RevokeAuthorizationCodeAsync(
        string code,
        CancellationToken ct = default);

    Task CleanupExpiredCodesAsync(CancellationToken ct = default);
}

/// <summary>
/// EF Core implementation of OAuth Authorization Code Manager
/// </summary>
public class OAuthAuthorizationCodeManager : IOAuthAuthorizationCodeManager
{
    private readonly LocoDbContext _context;
    private readonly ILogger<OAuthAuthorizationCodeManager> _logger;

    public OAuthAuthorizationCodeManager(
        LocoDbContext context,
        ILogger<OAuthAuthorizationCodeManager> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> CreateAuthorizationCodeAsync(
        string clientId,
        string userId,
        string redirectUri,
        string scopes,
        string? codeChallenge = null,
        string? codeChallengeMethod = null,
        string? nonce = null,
        CancellationToken ct = default)
    {
        try
        {
            var code = GenerateAuthorizationCode();
            var now = DateTime.UtcNow;

            var authCode = new OAuthAuthorizationCodeEntity
            {
                Code = code,
                ClientId = clientId,
                UserId = userId,
                RedirectUri = redirectUri,
                Scopes = scopes,
                CodeChallenge = codeChallenge,
                CodeChallengeMethod = codeChallengeMethod,
                Nonce = nonce,
                IssuedAt = now,
                ExpiresAt = now.AddMinutes(5), // 5-minute expiration
                IsRedeemed = false,
                RedeemedAt = null,
            };

            _context.OAuthAuthorizationCodes.Add(authCode);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Authorization code created: {Code} (Client: {ClientId}, User: {UserId})",
                code,
                clientId,
                userId);

            return code;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating authorization code for client: {ClientId}", clientId);
            throw;
        }
    }

    public async Task<OAuthAuthorizationCodeEntity?> GetAuthorizationCodeAsync(
        string code,
        CancellationToken ct = default)
    {
        try
        {
            return await _context.OAuthAuthorizationCodes
                .AsNoTracking()
                .FirstOrDefaultAsync(ac => ac.Code == code, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authorization code");
            throw;
        }
    }

    public async Task<bool> ValidateAuthorizationCodeAsync(
        string code,
        string clientId,
        string redirectUri,
        CancellationToken ct = default)
    {
        try
        {
            var authCode = await GetAuthorizationCodeAsync(code, ct);

            // Validation checks:
            // 1. Code exists
            if (authCode == null)
            {
                _logger.LogWarning("Authorization code not found: {Code}", code);
                return false;
            }

            // 2. Code not expired
            if (authCode.ExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogWarning("Authorization code expired: {Code}", code);
                return false;
            }

            // 3. Code not already redeemed
            if (authCode.IsRedeemed)
            {
                _logger.LogWarning("Authorization code already redeemed: {Code}", code);
                return false;
            }

            // 4. Client ID matches
            if (authCode.ClientId != clientId)
            {
                _logger.LogWarning(
                    "Authorization code client ID mismatch: {Code}, Expected: {ClientId}, Got: {ProvidedClientId}",
                    code,
                    authCode.ClientId,
                    clientId);
                return false;
            }

            // 5. Redirect URI matches exactly
            if (authCode.RedirectUri != redirectUri)
            {
                _logger.LogWarning(
                    "Authorization code redirect URI mismatch: {Code}, Expected: {RedirectUri}, Got: {ProvidedUri}",
                    code,
                    authCode.RedirectUri,
                    redirectUri);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating authorization code: {Code}", code);
            return false;
        }
    }

    public async Task<bool> ValidateCodeChallengeAsync(
        string code,
        string codeVerifier,
        CancellationToken ct = default)
    {
        try
        {
            var authCode = await GetAuthorizationCodeAsync(code, ct);

            // If no code challenge was provided during authorization, skip PKCE validation
            if (string.IsNullOrEmpty(authCode?.CodeChallenge))
            {
                return true;
            }

            // PKCE validation required
            if (string.IsNullOrEmpty(codeVerifier))
            {
                _logger.LogWarning("PKCE code verifier not provided: {Code}", code);
                return false;
            }

            string computedChallenge = authCode.CodeChallengeMethod == "S256"
                ? ComputeSha256Challenge(codeVerifier)
                : codeVerifier; // "plain" method

            bool isValid = computedChallenge == authCode.CodeChallenge;

            if (!isValid)
            {
                _logger.LogWarning("PKCE code challenge validation failed: {Code}", code);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating PKCE code challenge: {Code}", code);
            return false;
        }
    }

    public async Task<bool> RedeemAuthorizationCodeAsync(
        string code,
        CancellationToken ct = default)
    {
        try
        {
            var authCode = await _context.OAuthAuthorizationCodes
                .FirstOrDefaultAsync(ac => ac.Code == code, ct);

            if (authCode == null)
            {
                _logger.LogWarning("Cannot redeem - authorization code not found: {Code}", code);
                return false;
            }

            authCode.IsRedeemed = true;
            authCode.RedeemedAt = DateTime.UtcNow;

            _context.OAuthAuthorizationCodes.Update(authCode);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Authorization code redeemed: {Code}", code);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error redeeming authorization code: {Code}", code);
            throw;
        }
    }

    public async Task<bool> RevokeAuthorizationCodeAsync(
        string code,
        CancellationToken ct = default)
    {
        try
        {
            var authCode = await _context.OAuthAuthorizationCodes
                .FirstOrDefaultAsync(ac => ac.Code == code, ct);

            if (authCode == null)
                return false;

            _context.OAuthAuthorizationCodes.Remove(authCode);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Authorization code revoked: {Code}", code);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking authorization code: {Code}", code);
            throw;
        }
    }

    public async Task CleanupExpiredCodesAsync(CancellationToken ct = default)
    {
        try
        {
            var expiredCodes = await _context.OAuthAuthorizationCodes
                .Where(ac => ac.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync(ct);

            if (expiredCodes.Count == 0)
                return;

            _context.OAuthAuthorizationCodes.RemoveRange(expiredCodes);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Cleaned up {Count} expired authorization codes", expiredCodes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired authorization codes");
        }
    }

    /// <summary>
    /// Generate a secure authorization code (base36 encoded)
    /// </summary>
    private static string GenerateAuthorizationCode()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "")
            .Substring(0, 48); // Limit to 48 characters
    }

    /// <summary>
    /// Compute SHA256 PKCE code challenge
    /// </summary>
    private static string ComputeSha256Challenge(string codeVerifier)
    {
        using (var sha256 = SHA256.Create())
        {
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
            return Convert.ToBase64String(hash)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}
