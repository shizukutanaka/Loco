// Phase 3: OAuth 2.0 User Entity
// Persistent user storage for OAuth 2.0 authentication

using System;

namespace Loco.Core.Models;

/// <summary>
/// OAuth 2.0 User entity for authentication
/// </summary>
public class OAuthUserEntity
{
    /// <summary>
    /// Unique user identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Username (unique)
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Email address (unique, optional)
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// PBKDF2 password hash with salt
    /// Format: algorithm$iterations$salt$hash
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// User is active and can authenticate
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Email verification status
    /// </summary>
    public bool EmailVerified { get; set; } = false;

    /// <summary>
    /// Multi-factor authentication enabled
    /// </summary>
    public bool MfaEnabled { get; set; } = false;

    /// <summary>
    /// Last successful login timestamp
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Failed login attempts counter (for lockout)
    /// </summary>
    public int FailedLoginAttempts { get; set; } = 0;

    /// <summary>
    /// Account locked until timestamp
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// User roles (comma-separated: "admin,user")
    /// </summary>
    public string Roles { get; set; } = "user";

    /// <summary>
    /// Custom metadata (JSON)
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Account creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last account modification timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Account deletion timestamp (soft delete)
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Check if user account is locked
    /// </summary>
    public bool IsLocked => LockedUntil.HasValue && LockedUntil > DateTime.UtcNow;

    /// <summary>
    /// Check if user can login
    /// </summary>
    public bool CanLogin => IsActive && !IsLocked && DeletedAt == null;
}

/// <summary>
/// OAuth 2.0 Client entity for registered applications
/// </summary>
public class OAuthClientEntity
{
    /// <summary>
    /// Unique client identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Client display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Client secret (hashed with PBKDF2)
    /// Only populated during creation, never returned
    /// </summary>
    public string? SecretHash { get; set; }

    /// <summary>
    /// Grant types allowed (space-separated: "authorization_code refresh_token client_credentials")
    /// </summary>
    public string GrantTypes { get; set; } = "authorization_code refresh_token";

    /// <summary>
    /// Authorized redirect URIs (newline-separated)
    /// </summary>
    public string RedirectUris { get; set; } = string.Empty;

    /// <summary>
    /// Allowed scopes (space-separated)
    /// </summary>
    public string Scopes { get; set; } = "openid profile email";

    /// <summary>
    /// PKCE requirement: "never", "recommended", "required"
    /// </summary>
    public string PkceRequirement { get; set; } = "recommended";

    /// <summary>
    /// Access token lifetime in seconds
    /// </summary>
    public int AccessTokenLifetime { get; set; } = 3600; // 1 hour

    /// <summary>
    /// Refresh token lifetime in seconds
    /// </summary>
    public int RefreshTokenLifetime { get; set; } = 2592000; // 30 days

    /// <summary>
    /// Authorization code lifetime in seconds
    /// </summary>
    public int AuthorizationCodeLifetime { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Client is active and can authenticate
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Client requires secure transport (HTTPS)
    /// </summary>
    public bool RequireSecureTransport { get; set; } = true;

    /// <summary>
    /// Client is confidential (has secret) vs public
    /// </summary>
    public bool IsConfidential { get; set; } = true;

    /// <summary>
    /// Client registration timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last client modification timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// OAuth 2.0 Authorization Code entity (authorization_code grant flow)
/// </summary>
public class OAuthAuthorizationCodeEntity
{
    /// <summary>
    /// Authorization code (random, short-lived)
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Client ID that requested the authorization
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// User ID who authorized the client
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Redirect URI where code will be sent
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// Granted scopes (space-separated)
    /// </summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>
    /// PKCE code challenge (if provided)
    /// </summary>
    public string? CodeChallenge { get; set; }

    /// <summary>
    /// PKCE code challenge method: "plain" or "S256"
    /// </summary>
    public string? CodeChallengeMethod { get; set; }

    /// <summary>
    /// Nonce for OpenID Connect (prevents replay attacks)
    /// </summary>
    public string? Nonce { get; set; }

    /// <summary>
    /// Authorization issued timestamp
    /// </summary>
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Authorization expiration timestamp
    /// </summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(5);

    /// <summary>
    /// Authorization has been redeemed for token
    /// </summary>
    public bool IsRedeemed { get; set; } = false;

    /// <summary>
    /// Timestamp when code was redeemed
    /// </summary>
    public DateTime? RedeemedAt { get; set; }

    /// <summary>
    /// Check if authorization code is valid and not expired
    /// </summary>
    public bool IsValid => !IsRedeemed && ExpiresAt > DateTime.UtcNow;
}

/// <summary>
/// OAuth 2.0 Refresh Token entity
/// </summary>
public class OAuthRefreshTokenEntity
{
    /// <summary>
    /// Refresh token (random, long-lived)
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Client ID that issued the token
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// User ID who owns the token
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Original granted scopes (space-separated)
    /// </summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token issued timestamp
    /// </summary>
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Refresh token expiration timestamp
    /// </summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);

    /// <summary>
    /// Token has been revoked
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    /// <summary>
    /// Timestamp when token was revoked
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Last time token was used to refresh access token
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Check if refresh token is valid and not expired/revoked
    /// </summary>
    public bool IsValid => !IsRevoked && ExpiresAt > DateTime.UtcNow;
}

/// <summary>
/// OAuth 2.0 Scope definition
/// </summary>
public class OAuthScopeEntity
{
    /// <summary>
    /// Scope identifier (e.g., "workflows:read")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable scope description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Scope requires explicit user consent
    /// </summary>
    public bool RequiresConsent { get; set; } = true;

    /// <summary>
    /// Scope is built-in and cannot be modified
    /// </summary>
    public bool IsSystemScope { get; set; } = false;

    /// <summary>
    /// Scope creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
