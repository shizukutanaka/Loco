using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Loco.Api.Controllers;

/// <summary>
/// Authentication API for token generation
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AuthenticationController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthenticationController> _logger;

    public AuthenticationController(IConfiguration configuration, ILogger<AuthenticationController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Authenticate and get JWT token
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>JWT token</returns>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GetToken([FromBody] TokenRequest request)
    {
        _logger.LogInformation("Token request from user: {Username}", request.Username);

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Username and password are required" });

        // In a real application, validate against a database or authentication service
        if (!ValidateCredentials(request.Username, request.Password))
        {
            _logger.LogWarning("Failed authentication attempt for user: {Username}", request.Username);
            return Unauthorized(new { message = "Invalid credentials" });
        }

        var token = GenerateJwtToken(request.Username);

        _logger.LogInformation("Token generated for user: {Username}", request.Username);

        return Ok(new TokenResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = 3600,
            Scope = "workflows:read workflows:manage workflows:execute"
        });
    }

    /// <summary>
    /// Refresh JWT token
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <returns>New JWT token</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult RefreshToken([FromBody] RefreshTokenRequest request)
    {
        _logger.LogInformation("Token refresh request");

        // In a real application, validate the refresh token
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { message = "Refresh token is required" });

        // For now, return a new token
        var token = GenerateJwtToken("system");

        return Ok(new TokenResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = 3600,
            Scope = "workflows:read workflows:manage workflows:execute"
        });
    }

    /// <summary>
    /// Validate API key
    /// </summary>
    /// <param name="request">API key validation request</param>
    /// <returns>Validation result</returns>
    [HttpPost("validate-key")]
    [ProducesResponseType(typeof(ApiKeyValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult ValidateApiKey([FromBody] ApiKeyRequest request)
    {
        _logger.LogInformation("API key validation request");

        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return BadRequest(new { message = "API key is required" });

        // In a real application, validate against database
        var isValid = ValidateApiKey(request.ApiKey);

        if (!isValid)
            return Unauthorized(new { message = "Invalid API key" });

        return Ok(new ApiKeyValidationResponse
        {
            IsValid = true,
            Scope = "workflows:read workflows:manage workflows:execute"
        });
    }

    private bool ValidateCredentials(string username, string password)
    {
        // This is a placeholder. In production, verify against your user database
        // For demo purposes, accept any non-empty credentials
        return !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password);
    }

    private bool ValidateApiKey(string apiKey)
    {
        // This is a placeholder. In production, verify against your API key store
        return apiKey.StartsWith("loco_");
    }

    private string GenerateJwtToken(string username)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings.GetValue<string>("SecretKey") ?? "DefaultSecretKeyChangeInProduction12345";
        var issuer = jwtSettings.GetValue<string>("Issuer") ?? "https://loco.local";
        var audience = jwtSettings.GetValue<string>("Audience") ?? "loco-api";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, username),
            new Claim(ClaimTypes.Name, username),
            new Claim("sub", username),
            new Claim("scope", "workflows:read workflows:manage workflows:execute"),
            new Claim("aud", audience)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class TokenRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? GrantType { get; set; } = "password";
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = "";
}

public class ApiKeyRequest
{
    public string ApiKey { get; set; } = "";
}

public class TokenResponse
{
    public string AccessToken { get; set; } = "";
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public string? Scope { get; set; }
    public string? RefreshToken { get; set; }
}

public class ApiKeyValidationResponse
{
    public bool IsValid { get; set; }
    public string? Scope { get; set; }
}
