// Phase 3: OAuth 2.0 Client Manager
// Manages OAuth 2.0 client registration and validation

using Loco.Core.DataAccess;
using Loco.Core.Models;
using System.Security.Cryptography;
using System.Text;

namespace Loco.Core.Security;

/// <summary>
/// OAuth 2.0 Client Manager
/// Handles client registration, validation, and secret management
/// </summary>
public interface IOAuthClientManager
{
    Task<(OAuthClientEntity client, string plainSecret)> RegisterClientAsync(
        string clientName,
        string[] redirectUris,
        string[] grantTypes,
        string[] scopes,
        bool requirePkce = false,
        CancellationToken ct = default);

    Task<OAuthClientEntity?> GetClientAsync(string clientId, CancellationToken ct = default);

    Task<bool> ValidateClientAsync(
        string clientId,
        string clientSecret,
        CancellationToken ct = default);

    Task<bool> ValidateRedirectUriAsync(
        string clientId,
        string redirectUri,
        CancellationToken ct = default);

    Task<bool> IsScopeAuthorizedAsync(
        string clientId,
        string scope,
        CancellationToken ct = default);

    Task<OAuthClientEntity> UpdateClientAsync(
        OAuthClientEntity client,
        CancellationToken ct = default);

    Task<bool> RevokeClientAsync(string clientId, CancellationToken ct = default);
}

/// <summary>
/// EF Core implementation of OAuth Client Manager
/// </summary>
public class OAuthClientManager : IOAuthClientManager
{
    private readonly LocoDbContext _context;
    private readonly ICryptographicService _crypto;
    private readonly ILogger<OAuthClientManager> _logger;

    public OAuthClientManager(
        LocoDbContext context,
        ICryptographicService crypto,
        ILogger<OAuthClientManager> logger)
    {
        _context = context;
        _crypto = crypto;
        _logger = logger;
    }

    public async Task<(OAuthClientEntity client, string plainSecret)> RegisterClientAsync(
        string clientName,
        string[] redirectUris,
        string[] grantTypes,
        string[] scopes,
        bool requirePkce = false,
        CancellationToken ct = default)
    {
        try
        {
            var clientId = GenerateClientId();
            var plainSecret = GenerateClientSecret();
            var secretHash = _crypto.HashPassword(plainSecret);

            var client = new OAuthClientEntity
            {
                Id = clientId,
                Name = clientName,
                SecretHash = secretHash,
                GrantTypes = string.Join(" ", grantTypes),
                RedirectUris = string.Join("\n", redirectUris),
                Scopes = string.Join(" ", scopes),
                PkceRequirement = requirePkce ? "required" : "recommended",
                IsActive = true,
                IsConfidential = true,
                AccessTokenLifetime = 3600,
                RefreshTokenLifetime = 2592000,
                AuthorizationCodeLifetime = 300,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _context.OAuthClients.Add(client);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("OAuth client registered: {ClientId} ({ClientName})", clientId, clientName);

            // Return plain secret only once during registration
            return (client, plainSecret);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering OAuth client: {ClientName}", clientName);
            throw;
        }
    }

    public async Task<OAuthClientEntity?> GetClientAsync(string clientId, CancellationToken ct = default)
    {
        try
        {
            return await _context.OAuthClients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clientId && c.IsActive, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting OAuth client: {ClientId}", clientId);
            throw;
        }
    }

    public async Task<bool> ValidateClientAsync(
        string clientId,
        string clientSecret,
        CancellationToken ct = default)
    {
        try
        {
            var client = await GetClientAsync(clientId, ct);
            if (client == null)
            {
                _logger.LogWarning("OAuth client not found or inactive: {ClientId}", clientId);
                return false;
            }

            // Verify secret using constant-time comparison
            bool isSecretValid = _crypto.VerifyPassword(clientSecret, client.SecretHash ?? string.Empty);

            if (!isSecretValid)
            {
                _logger.LogWarning("OAuth client secret validation failed: {ClientId}", clientId);
            }

            return isSecretValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating OAuth client: {ClientId}", clientId);
            return false;
        }
    }

    public async Task<bool> ValidateRedirectUriAsync(
        string clientId,
        string redirectUri,
        CancellationToken ct = default)
    {
        try
        {
            var client = await GetClientAsync(clientId, ct);
            if (client == null)
                return false;

            var authorizedUris = client.RedirectUris
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(uri => uri.Trim())
                .ToList();

            // Exact match required (prevent open redirects)
            bool isValid = authorizedUris.Contains(redirectUri);

            if (!isValid)
            {
                _logger.LogWarning(
                    "OAuth redirect URI validation failed: {ClientId}, URI: {RedirectUri}",
                    clientId,
                    redirectUri);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating redirect URI: {ClientId}", clientId);
            return false;
        }
    }

    public async Task<bool> IsScopeAuthorizedAsync(
        string clientId,
        string scope,
        CancellationToken ct = default)
    {
        try
        {
            var client = await GetClientAsync(clientId, ct);
            if (client == null)
                return false;

            var authorizedScopes = client.Scopes
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            return authorizedScopes.Contains(scope);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking scope authorization: {ClientId}, {Scope}", clientId, scope);
            return false;
        }
    }

    public async Task<OAuthClientEntity> UpdateClientAsync(
        OAuthClientEntity client,
        CancellationToken ct = default)
    {
        try
        {
            client.UpdatedAt = DateTime.UtcNow;

            _context.OAuthClients.Update(client);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("OAuth client updated: {ClientId}", client.Id);
            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating OAuth client: {ClientId}", client.Id);
            throw;
        }
    }

    public async Task<bool> RevokeClientAsync(string clientId, CancellationToken ct = default)
    {
        try
        {
            var client = await _context.OAuthClients
                .FirstOrDefaultAsync(c => c.Id == clientId, ct);

            if (client == null)
                return false;

            client.IsActive = false;
            client.UpdatedAt = DateTime.UtcNow;

            _context.OAuthClients.Update(client);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("OAuth client revoked: {ClientId}", clientId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking OAuth client: {ClientId}", clientId);
            throw;
        }
    }

    /// <summary>
    /// Generate a unique client ID (base36 encoded random bytes)
    /// </summary>
    private static string GenerateClientId()
    {
        var bytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return "client_" + Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    /// <summary>
    /// Generate a secure client secret (base64 encoded)
    /// </summary>
    private static string GenerateClientSecret()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return "secret_" + Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}
