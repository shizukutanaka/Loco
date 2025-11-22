// Phase 3: OAuth 2.0 Server Endpoints
// Complete OAuth 2.0 implementation with PKCE support

using Loco.Core.DataAccess;
using Loco.Core.Models;
using Loco.Core.Security;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace Loco.Api.Endpoints;

/// <summary>
/// OAuth 2.0 Authorization Server Endpoints
/// Implements RFC 6749 (OAuth 2.0) and RFC 7636 (PKCE)
/// </summary>
public static class OAuthEndpoints
{
    public static void MapOAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/oauth")
            .WithTags("OAuth 2.0")
            .WithOpenApi();

        // Authorization endpoint (GET/POST)
        group.MapGet("/authorize", HandleAuthorizeRequest)
            .WithName("OAuthAuthorize")
            .WithOpenApi()
            .Produces(StatusCodes.Status302Found)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        group.MapPost("/authorize", HandleAuthorizeRequest)
            .WithName("OAuthAuthorizePost")
            .WithOpenApi()
            .Produces(StatusCodes.Status302Found)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        // Token endpoint (POST only)
        group.MapPost("/token", HandleTokenRequest)
            .WithName("OAuthToken")
            .WithOpenApi()
            .Produces<TokenResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized);

        // Token revocation endpoint (POST)
        group.MapPost("/revoke", HandleRevokeRequest)
            .WithName("OAuthRevoke")
            .WithOpenApi()
            .Produces(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        // UserInfo endpoint (GET) - OpenID Connect
        group.MapGet("/userinfo", HandleUserInfoRequest)
            .RequireAuthorization()
            .WithName("OAuthUserInfo")
            .WithOpenApi()
            .Produces<UserInfoResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// OAuth 2.0 Authorization Endpoint (GET/POST)
    /// RFC 6749 Section 3.1
    /// </summary>
    private static async Task<IResult> HandleAuthorizeRequest(
        HttpContext context,
        IOAuthClientManager clientManager,
        IOAuthAuthorizationCodeManager authCodeManager,
        ILogger<OAuthEndpoints> logger,
        [FromQuery(Name = "client_id")] string? clientId,
        [FromQuery(Name = "response_type")] string? responseType,
        [FromQuery(Name = "redirect_uri")] string? redirectUri,
        [FromQuery(Name = "scope")] string? scope,
        [FromQuery(Name = "state")] string? state,
        [FromQuery(Name = "code_challenge")] string? codeChallenge,
        [FromQuery(Name = "code_challenge_method")] string? codeChallengeMethod,
        [FromQuery(Name = "nonce")] string? nonce)
    {
        // Validate required parameters
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(responseType) || string.IsNullOrEmpty(redirectUri))
        {
            logger.LogWarning("Invalid authorization request parameters");
            return Results.BadRequest(new ErrorResponse
            {
                Error = "invalid_request",
                ErrorDescription = "Missing required parameters: client_id, response_type, redirect_uri"
            });
        }

        // Only support authorization_code flow
        if (responseType != "code")
        {
            logger.LogWarning("Unsupported response type: {ResponseType}", responseType);
            return Results.BadRequest(new ErrorResponse
            {
                Error = "unsupported_response_type",
                ErrorDescription = "Only 'code' response type is supported"
            });
        }

        // Validate client
        var client = await clientManager.GetClientAsync(clientId);
        if (client == null)
        {
            logger.LogWarning("OAuth client not found: {ClientId}", clientId);
            return Results.BadRequest(new ErrorResponse
            {
                Error = "invalid_client",
                ErrorDescription = "Unknown client"
            });
        }

        // Validate redirect URI
        if (!await clientManager.ValidateRedirectUriAsync(clientId, redirectUri))
        {
            logger.LogWarning("Invalid redirect URI for client: {ClientId}", clientId);
            return Results.BadRequest(new ErrorResponse
            {
                Error = "invalid_request",
                ErrorDescription = "Invalid redirect URI"
            });
        }

        // Validate scopes
        scope ??= client.Scopes;
        var requestedScopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var requestedScope in requestedScopes)
        {
            if (!await clientManager.IsScopeAuthorizedAsync(clientId, requestedScope))
            {
                logger.LogWarning("Scope not authorized for client: {ClientId}, {Scope}", clientId, requestedScope);
                return Results.BadRequest(new ErrorResponse
                {
                    Error = "invalid_scope",
                    ErrorDescription = $"Scope '{requestedScope}' not authorized"
                });
            }
        }

        // Check if user is authenticated
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            // User not authenticated - redirect to login
            logger.LogInformation("User not authenticated for authorization request");
            return Results.Redirect($"/login?returnUrl={Uri.EscapeDataString(context.Request.GetDisplayUrl())}");
        }

        var userId = userIdClaim.Value;

        // Generate authorization code with PKCE support
        var code = await authCodeManager.CreateAuthorizationCodeAsync(
            clientId,
            userId,
            redirectUri,
            scope,
            codeChallenge,
            codeChallengeMethod,
            nonce);

        // Build authorization response
        var authorizationUrl = $"{redirectUri}?code={code}";
        if (!string.IsNullOrEmpty(state))
            authorizationUrl += $"&state={Uri.EscapeDataString(state)}";

        logger.LogInformation("Authorization granted: Client={ClientId}, User={UserId}", clientId, userId);

        return Results.Redirect(authorizationUrl);
    }

    /// <summary>
    /// OAuth 2.0 Token Endpoint (POST)
    /// RFC 6749 Section 3.2
    /// </summary>
    private static async Task<IResult> HandleTokenRequest(
        HttpContext context,
        IOAuthClientManager clientManager,
        IOAuthAuthorizationCodeManager authCodeManager,
        IOAuthUserRepository userRepository,
        IJwtTokenManager jwtTokenManager,
        ILogger<OAuthEndpoints> logger)
    {
        var form = await context.Request.ReadFormAsync();

        var grantType = form["grant_type"].ToString();
        var clientId = form["client_id"].ToString();
        var clientSecret = form["client_secret"].ToString();
        var code = form["code"].ToString();
        var redirectUri = form["redirect_uri"].ToString();
        var codeVerifier = form["code_verifier"].ToString();
        var refreshToken = form["refresh_token"].ToString();

        // Validate client credentials
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            logger.LogWarning("Missing client credentials in token request");
            return Results.Unauthorized();
        }

        if (!await clientManager.ValidateClientAsync(clientId, clientSecret))
        {
            logger.LogWarning("Invalid client credentials: {ClientId}", clientId);
            return Results.Unauthorized();
        }

        // Handle authorization_code grant
        if (grantType == "authorization_code")
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(redirectUri))
            {
                logger.LogWarning("Missing required parameters for authorization_code grant");
                return Results.BadRequest(new ErrorResponse
                {
                    Error = "invalid_request",
                    ErrorDescription = "Missing code or redirect_uri"
                });
            }

            // Validate authorization code
            if (!await authCodeManager.ValidateAuthorizationCodeAsync(code, clientId, redirectUri))
            {
                logger.LogWarning("Invalid authorization code: {Code}", code);
                return Results.BadRequest(new ErrorResponse
                {
                    Error = "invalid_grant",
                    ErrorDescription = "Invalid authorization code"
                });
            }

            // Validate PKCE code challenge
            if (!string.IsNullOrEmpty(codeVerifier))
            {
                if (!await authCodeManager.ValidateCodeChallengeAsync(code, codeVerifier))
                {
                    logger.LogWarning("PKCE code challenge validation failed: {Code}", code);
                    return Results.BadRequest(new ErrorResponse
                    {
                        Error = "invalid_grant",
                        ErrorDescription = "Invalid code_verifier"
                    });
                }
            }

            // Get authorization code details
            var authCode = await authCodeManager.GetAuthorizationCodeAsync(code);
            if (authCode == null || !authCode.IsValid)
            {
                return Results.BadRequest(new ErrorResponse
                {
                    Error = "invalid_grant",
                    ErrorDescription = "Authorization code expired"
                });
            }

            // Get user
            var user = await userRepository.GetByIdAsync(authCode.UserId);
            if (user == null || !user.CanLogin)
            {
                logger.LogWarning("User cannot login: {UserId}", authCode.UserId);
                return Results.BadRequest(new ErrorResponse
                {
                    Error = "invalid_grant",
                    ErrorDescription = "User not found or inactive"
                });
            }

            // Generate tokens
            var tokenRequest = new TokenRequest
            {
                Username = user.Username,
                Scope = authCode.Scopes
            };

            var accessToken = await jwtTokenManager.GenerateTokenAsync(tokenRequest);
            var refreshTokenStr = Guid.NewGuid().ToString("N");

            // Redeem authorization code
            await authCodeManager.RedeemAuthorizationCodeAsync(code);

            logger.LogInformation(
                "Token issued via authorization_code: User={UserId}, Client={ClientId}",
                user.Id,
                clientId);

            return Results.Ok(new TokenResponse
            {
                AccessToken = accessToken,
                TokenType = "Bearer",
                ExpiresIn = 3600,
                RefreshToken = refreshTokenStr,
                Scope = authCode.Scopes
            });
        }

        // Handle refresh_token grant
        if (grantType == "refresh_token")
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                return Results.BadRequest(new ErrorResponse
                {
                    Error = "invalid_request",
                    ErrorDescription = "Missing refresh_token"
                });
            }

            // TODO: Implement refresh token validation and new token generation
            logger.LogWarning("Refresh token grant not yet fully implemented");
            return Results.BadRequest(new ErrorResponse
            {
                Error = "unsupported_grant_type",
                ErrorDescription = "Refresh token grant not yet supported"
            });
        }

        logger.LogWarning("Unsupported grant type: {GrantType}", grantType);
        return Results.BadRequest(new ErrorResponse
        {
            Error = "unsupported_grant_type",
            ErrorDescription = "Grant type not supported"
        });
    }

    /// <summary>
    /// OAuth 2.0 Token Revocation Endpoint (POST)
    /// RFC 7009
    /// </summary>
    private static async Task<IResult> HandleRevokeRequest(
        HttpContext context,
        IJwtTokenManager jwtTokenManager,
        ILogger<OAuthEndpoints> logger)
    {
        var form = await context.Request.ReadFormAsync();
        var token = form["token"].ToString();
        var tokenTypeHint = form["token_type_hint"].ToString();

        if (string.IsNullOrEmpty(token))
        {
            return Results.BadRequest(new ErrorResponse
            {
                Error = "invalid_request",
                ErrorDescription = "Missing token"
            });
        }

        // Revoke the token
        await jwtTokenManager.RevokeTokenAsync(token);

        logger.LogInformation("Token revoked: {TokenTypeHint}", tokenTypeHint ?? "unknown");

        return Results.Ok();
    }

    /// <summary>
    /// OpenID Connect UserInfo Endpoint (GET)
    /// RFC 6750 / OpenID Connect Core 1.0
    /// </summary>
    private static async Task<IResult> HandleUserInfoRequest(
        HttpContext context,
        IOAuthUserRepository userRepository,
        ILogger<OAuthEndpoints> logger)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            logger.LogWarning("UserInfo request without authenticated user");
            return Results.Unauthorized();
        }

        var user = await userRepository.GetByIdAsync(userIdClaim.Value);
        if (user == null)
        {
            logger.LogWarning("User not found for UserInfo: {UserId}", userIdClaim.Value);
            return Results.Unauthorized();
        }

        return Results.Ok(new UserInfoResponse
        {
            Sub = user.Id,
            Name = user.Username,
            Email = user.Email,
            EmailVerified = user.EmailVerified,
            UpdatedAt = (long)(user.UpdatedAt.Subtract(new DateTime(1970, 1, 1))).TotalSeconds
        });
    }
}

/// <summary>
/// OAuth 2.0 Token Response
/// </summary>
public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; } = 3600;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; } // For OpenID Connect
}

/// <summary>
/// OAuth 2.0 Error Response
/// </summary>
public class ErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }

    [JsonPropertyName("error_uri")]
    public string? ErrorUri { get; set; }
}

/// <summary>
/// OpenID Connect UserInfo Response
/// </summary>
public class UserInfoResponse
{
    [JsonPropertyName("sub")]
    public string Sub { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("email_verified")]
    public bool EmailVerified { get; set; }

    [JsonPropertyName("updated_at")]
    public long UpdatedAt { get; set; }
}

/// <summary>
/// Helper extension methods
/// </summary>
public static class OAuthEndpointsExtensions
{
    /// <summary>
    /// Register OAuth 2.0 services in DI container
    /// </summary>
    public static IServiceCollection AddOAuth2Services(this IServiceCollection services)
    {
        services.AddScoped<IOAuthClientManager, OAuthClientManager>();
        services.AddScoped<IOAuthAuthorizationCodeManager, OAuthAuthorizationCodeManager>();
        services.AddScoped<IOAuthUserRepository, OAuthUserRepository>();

        return services;
    }
}
