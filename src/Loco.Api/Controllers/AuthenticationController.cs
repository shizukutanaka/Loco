using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Loco.Api.Contracts;
using Loco.Api.Security;

namespace Loco.Api.Controllers;

/// <summary>
/// Authentication API for token issuance.
///
/// Users come from configuration (Auth:Users), each with a PBKDF2 password hash
/// and an explicit scope list. The previous implementation accepted ANY
/// non-empty username/password and issued a full-scope token
/// ("For demo purposes, accept any non-empty credentials") - i.e. authentication
/// existed in name only. With zero configured users this endpoint now refuses
/// to issue tokens instead of accepting everyone.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AuthenticationController : ControllerBase
{
    private readonly AuthOptions _options;
    private readonly JwtSigningKeyProvider _signingKey;
    private readonly ILogger<AuthenticationController> _logger;

    public AuthenticationController(
        AuthOptions options,
        JwtSigningKeyProvider signingKey,
        ILogger<AuthenticationController> logger)
    {
        _options = options;
        _signingKey = signingKey;
        _logger = logger;
    }

    /// <summary>Authenticate with configured credentials and receive a JWT.</summary>
    [HttpPost("token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult GetToken([FromBody] TokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(Envelope.Fail("INVALID_ARGUMENT", "Username and password are required"));
        }

        if (_options.Users.Count == 0)
        {
            // Fail closed: no configured users means nobody can log in - never
            // fall back to accept-all.
            _logger.LogWarning("Token requested but Auth:Users is empty; refusing");
            return StatusCode(StatusCodes.Status501NotImplemented,
                Envelope.Fail("AUTH_NOT_CONFIGURED",
                    "No users are configured. Add Auth:Users entries (see docs/GETTING_STARTED.md)."));
        }

        var user = _options.Users.FirstOrDefault(u =>
            string.Equals(u.Username, request.Username, StringComparison.Ordinal));

        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed authentication attempt for user: {Username}", request.Username);
            return Unauthorized(Envelope.Fail("UNAUTHORIZED", "Invalid credentials"));
        }

        var scopes = user.Scopes.Count > 0 ? string.Join(' ', user.Scopes) : "workflows:read";
        var token = GenerateJwtToken(user.Username, scopes);

        _logger.LogInformation("Token issued for user: {Username}", request.Username);

        return Ok(Envelope.Ok(new
        {
            accessToken = token,
            tokenType = "Bearer",
            expiresIn = (int)_options.TokenLifetime.TotalSeconds,
            scope = scopes,
        }));
    }

    private string GenerateJwtToken(string username, string scopes)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("scope", scopes),
        };

        var credentials = new SigningCredentials(_signingKey.Key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(_options.TokenLifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class TokenRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
