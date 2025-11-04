#nullable enable

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Api.Security;

/// <summary>
/// Advanced API Security Patterns
/// OAuth2, PKCE, mTLS, Zero Trust Architecture, Token Management
/// </summary>

/// <summary>
/// OAuth2 client configuration
/// </summary>
public class OAuth2ClientConfig
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;

    [JsonPropertyName("redirectUri")]
    public string RedirectUri { get; set; } = string.Empty;

    [JsonPropertyName("scopes")]
    public List<string> Scopes { get; set; } = new();

    [JsonPropertyName("tokenEndpoint")]
    public string TokenEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("authorizationEndpoint")]
    public string AuthorizationEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("clientType")]
    public string ClientType { get; set; } = "confidential"; // confidential, public
}

/// <summary>
/// PKCE (Proof Key for Code Exchange) parameters
/// Prevents authorization code interception attacks
/// </summary>
public class PkceParameters
{
    [JsonPropertyName("codeVerifier")]
    public string CodeVerifier { get; set; } = string.Empty;

    [JsonPropertyName("codeChallenge")]
    public string CodeChallenge { get; set; } = string.Empty;

    [JsonPropertyName("codeChallengeMethod")]
    public string CodeChallengeMethod { get; set; } = "S256"; // S256 or plain

    /// <summary>
    /// Generate PKCE parameters for OAuth2 flow
    /// </summary>
    public static PkceParameters Generate()
    {
        // Generate 43-128 character code verifier
        var codeVerifier = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        // Create code challenge using SHA256
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        var codeChallenge = Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return new PkceParameters
        {
            CodeVerifier = codeVerifier,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = "S256"
        };
    }
}

/// <summary>
/// OAuth2 authorization request
/// </summary>
public class OAuth2AuthorizationRequest
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("responseType")]
    public string ResponseType { get; set; } = "code"; // code, token, id_token

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    [JsonPropertyName("redirectUri")]
    public string RedirectUri { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty; // CSRF protection

    [JsonPropertyName("nonce")]
    public string? Nonce { get; set; } // For OpenID Connect

    [JsonPropertyName("codeChallenge")]
    public string? CodeChallenge { get; set; } // PKCE

    [JsonPropertyName("codeChallengeMethod")]
    public string? CodeChallengeMethod { get; set; } // PKCE

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// OAuth2 token response
/// </summary>
public class OAuth2TokenResponse
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("tokenType")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expiresIn")]
    public int ExpiresIn { get; set; } = 3600;

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("idToken")]
    public string? IdToken { get; set; } // OpenID Connect

    [JsonPropertyName("issuedAt")]
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// JWT token claims
/// </summary>
public class JwtClaims
{
    [JsonPropertyName("sub")]
    public string Subject { get; set; } = string.Empty; // User ID

    [JsonPropertyName("aud")]
    public List<string> Audience { get; set; } = new(); // Intended recipient

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    [JsonPropertyName("iat")]
    public long IssuedAt { get; set; } // Issued at (unix timestamp)

    [JsonPropertyName("exp")]
    public long ExpiresAt { get; set; } // Expires at (unix timestamp)

    [JsonPropertyName("jti")]
    public string JwtId { get; set; } = Guid.NewGuid().ToString(); // Unique ID for revocation

    [JsonPropertyName("cnf")]
    public Dictionary<string, object>? Confirmation { get; set; } // Certificate binding

    [JsonPropertyName("custom")]
    public Dictionary<string, object> CustomClaims { get; set; } = new();
}

/// <summary>
/// OAuth2 & JWT token manager
/// </summary>
public class OAuth2TokenManager
{
    private readonly Dictionary<string, JwtClaims> _tokens = new();
    private readonly HashSet<string> _revokedTokens = new();
    private readonly ILogger<OAuth2TokenManager> _logger;

    public OAuth2TokenManager(ILogger<OAuth2TokenManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Create access token
    /// </summary>
    public string CreateAccessToken(
        string userId,
        List<string> scopes,
        TimeSpan expiresIn,
        Dictionary<string, object>? customClaims = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiresAt = now + (long)expiresIn.TotalSeconds;

        var claims = new JwtClaims
        {
            Subject = userId,
            Scope = string.Join(" ", scopes),
            IssuedAt = now,
            ExpiresAt = expiresAt,
            Audience = new() { "api" }
        };

        if (customClaims != null)
        {
            foreach (var kvp in customClaims)
            {
                claims.CustomClaims[kvp.Key] = kvp.Value;
            }
        }

        var jti = claims.JwtId;
        _tokens[jti] = claims;

        _logger.LogInformation(
            "Created access token for user {UserId} with scopes {Scopes}, expires in {ExpiresIn}s",
            userId,
            string.Join(",", scopes),
            expiresIn.TotalSeconds);

        return EncodeJwt(claims);
    }

    /// <summary>
    /// Create refresh token (longer lifetime)
    /// </summary>
    public string CreateRefreshToken(
        string userId,
        List<string> scopes,
        TimeSpan expiresIn)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiresAt = now + (long)expiresIn.TotalSeconds;

        var claims = new JwtClaims
        {
            Subject = userId,
            Scope = string.Join(" ", scopes),
            IssuedAt = now,
            ExpiresAt = expiresAt,
            Audience = new() { "refresh" },
            CustomClaims = new() { ["type"] = "refresh" }
        };

        var jti = claims.JwtId;
        _tokens[jti] = claims;

        _logger.LogInformation(
            "Created refresh token for user {UserId}, expires in {ExpiresIn}s",
            userId,
            expiresIn.TotalSeconds);

        return EncodeJwt(claims);
    }

    /// <summary>
    /// Validate token
    /// </summary>
    public bool ValidateToken(string token, out JwtClaims? claims)
    {
        claims = null;

        if (!DecodeJwt(token, out var decodedClaims))
        {
            return false;
        }

        // Check if revoked
        if (_revokedTokens.Contains(decodedClaims.JwtId))
        {
            _logger.LogWarning("Token has been revoked: {JwtId}", decodedClaims.JwtId);
            return false;
        }

        // Check expiration
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (decodedClaims.ExpiresAt < now)
        {
            _logger.LogWarning("Token has expired: {JwtId}", decodedClaims.JwtId);
            return false;
        }

        claims = decodedClaims;
        return true;
    }

    /// <summary>
    /// Revoke token (e.g., on logout)
    /// </summary>
    public void RevokeToken(string jwtId)
    {
        _revokedTokens.Add(jwtId);

        _logger.LogInformation("Token revoked: {JwtId}", jwtId);
    }

    /// <summary>
    /// Refresh token - get new access token using refresh token
    /// </summary>
    public string? RefreshAccessToken(string refreshToken, List<string> newScopes)
    {
        if (!ValidateToken(refreshToken, out var claims))
        {
            return null;
        }

        if (claims!.CustomClaims.GetValueOrDefault("type") as string != "refresh")
        {
            _logger.LogWarning("Invalid token type for refresh: {JwtId}", claims.JwtId);
            return null;
        }

        // Revoke old refresh token (rotation)
        RevokeToken(claims.JwtId);

        // Create new access token with new scopes (or keep old if not specified)
        var scopeList = newScopes.Any() ? newScopes : claims.Scope.Split(' ').ToList();

        return CreateAccessToken(
            claims.Subject,
            scopeList,
            TimeSpan.FromHours(1));
    }

    /// <summary>
    /// Check scope permission
    /// </summary>
    public bool HasScope(JwtClaims claims, string requiredScope)
    {
        var userScopes = claims.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return userScopes.Contains(requiredScope) || userScopes.Contains("*");
    }

    private string EncodeJwt(JwtClaims claims)
    {
        // Simplified JWT encoding
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"typ\":\"JWT\",\"alg\":\"HS256\"}"));
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(claims)));

        return $"{header}.{payload}.signature";
    }

    private bool DecodeJwt(string token, out JwtClaims claims)
    {
        claims = null;

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        try
        {
            var payload = Encoding.UTF8.GetString(
                Convert.FromBase64String(parts[1] + "=="));
            claims = System.Text.Json.JsonSerializer.Deserialize<JwtClaims>(payload);
            return claims != null;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// mTLS (Mutual TLS) certificate management
/// </summary>
public class MutualTlsManager
{
    private readonly Dictionary<string, ClientCertificate> _clientCerts = new();
    private readonly ILogger<MutualTlsManager> _logger;

    public MutualTlsManager(ILogger<MutualTlsManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register client certificate
    /// </summary>
    public void RegisterClientCertificate(string clientId, string certificateThumbprint, string[] allowedSubjects)
    {
        _clientCerts[clientId] = new ClientCertificate
        {
            ClientId = clientId,
            CertificateThumbprint = certificateThumbprint,
            AllowedSubjects = allowedSubjects.ToList(),
            RegisteredAt = DateTime.UtcNow
        };

        _logger.LogInformation(
            "Registered mTLS certificate for client {ClientId}",
            clientId);
    }

    /// <summary>
    /// Validate client certificate
    /// </summary>
    public bool ValidateClientCertificate(string clientId, string certificateThumbprint, string subject)
    {
        if (!_clientCerts.TryGetValue(clientId, out var cert))
        {
            _logger.LogWarning("Client certificate not found: {ClientId}", clientId);
            return false;
        }

        if (cert.CertificateThumbprint != certificateThumbprint)
        {
            _logger.LogWarning(
                "Certificate thumbprint mismatch for client {ClientId}",
                clientId);
            return false;
        }

        if (!cert.AllowedSubjects.Contains(subject))
        {
            _logger.LogWarning(
                "Subject not allowed for client {ClientId}: {Subject}",
                clientId,
                subject);
            return false;
        }

        _logger.LogInformation(
            "mTLS certificate validation passed for client {ClientId}",
            clientId);

        return true;
    }
}

/// <summary>
/// Client certificate
/// </summary>
public class ClientCertificate
{
    public string ClientId { get; set; } = string.Empty;
    public string CertificateThumbprint { get; set; } = string.Empty;
    public List<string> AllowedSubjects { get; set; } = new();
    public DateTime RegisteredAt { get; set; }
}

/// <summary>
/// Zero Trust security policy
/// Verify every access request regardless of network location
/// </summary>
public class ZeroTrustPolicy
{
    [JsonPropertyName("policyId")]
    public string PolicyId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("resource")]
    public string Resource { get; set; } = string.Empty;

    [JsonPropertyName("principalId")]
    public string? PrincipalId { get; set; } // User/service ID

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty; // read, write, delete

    [JsonPropertyName("conditions")]
    public List<AccessCondition> Conditions { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Access condition for Zero Trust
/// </summary>
public class AccessCondition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // timeWindow, ipAddress, device, location

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Zero Trust access evaluation
/// </summary>
public class ZeroTrustEvaluator
{
    private readonly List<ZeroTrustPolicy> _policies = new();
    private readonly ILogger<ZeroTrustEvaluator> _logger;

    public ZeroTrustEvaluator(ILogger<ZeroTrustEvaluator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Add Zero Trust policy
    /// </summary>
    public void AddPolicy(ZeroTrustPolicy policy)
    {
        _policies.Add(policy);

        _logger.LogInformation(
            "Added Zero Trust policy for resource {Resource}",
            policy.Resource);
    }

    /// <summary>
    /// Evaluate access request
    /// </summary>
    public bool EvaluateAccess(
        string principalId,
        string resource,
        string action,
        Dictionary<string, string> context)
    {
        var applicablePolicies = _policies
            .Where(p => p.Resource == resource && p.Action == action)
            .Where(p => p.PrincipalId == null || p.PrincipalId == principalId)
            .ToList();

        if (!applicablePolicies.Any())
        {
            _logger.LogWarning(
                "No Zero Trust policies found for {Principal}:{Resource}:{Action}",
                principalId,
                resource,
                action);

            return false; // Deny by default
        }

        foreach (var policy in applicablePolicies)
        {
            var conditionsMet = true;

            foreach (var condition in policy.Conditions)
            {
                if (!EvaluateCondition(condition, context))
                {
                    conditionsMet = false;
                    break;
                }
            }

            if (conditionsMet)
            {
                _logger.LogInformation(
                    "Access granted by policy {PolicyId} for {Principal}:{Resource}:{Action}",
                    policy.PolicyId,
                    principalId,
                    resource,
                    action);

                return true;
            }
        }

        _logger.LogWarning(
            "Access denied for {Principal}:{Resource}:{Action}",
            principalId,
            resource,
            action);

        return false;
    }

    private bool EvaluateCondition(AccessCondition condition, Dictionary<string, string> context)
    {
        switch (condition.Type)
        {
            case "timeWindow":
                // Check if current time is within allowed window
                return true; // Simplified

            case "ipAddress":
                // Check if request IP is in allowed list
                return context.ContainsKey("ipAddress") &&
                       context["ipAddress"] == condition.Value;

            case "device":
                // Check device registration and health
                return context.ContainsKey("deviceId");

            case "location":
                // Check geographic location (requires geo-IP lookup)
                return true; // Simplified

            default:
                return false;
        }
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class AdvancedSecurityExtensions
{
    public static IServiceCollection AddAdvancedApiSecurity(this IServiceCollection services)
    {
        services.AddSingleton<OAuth2TokenManager>();
        services.AddSingleton<MutualTlsManager>();
        services.AddSingleton<ZeroTrustEvaluator>();
        return services;
    }
}
