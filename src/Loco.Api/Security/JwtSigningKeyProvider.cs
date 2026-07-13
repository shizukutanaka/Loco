using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Loco.Api.Security;

/// <summary>
/// Resolves the JWT signing key once at startup with fail-fast semantics:
///
/// - Jwt:SecretKey configured and &gt;= 32 bytes: used as-is.
/// - Missing/short in Production: the app REFUSES TO START. The previous code
///   fell back to a hardcoded literal ("DefaultSecretKeyChangeInProduction12345"),
///   and appsettings.json even shipped a committed "production" secret - with
///   either, every token is forgeable by anyone who has read the source.
/// - Missing/short in Development: a random per-run key is generated and a loud
///   warning logged. Tokens then survive only until restart, which is fine for
///   local development and never silently insecure.
/// </summary>
public sealed class JwtSigningKeyProvider
{
    public SymmetricSecurityKey Key { get; }

    public JwtSigningKeyProvider(IConfiguration configuration, IHostEnvironment environment, ILogger<JwtSigningKeyProvider> logger)
    {
        var configured = configuration["Jwt:SecretKey"];

        if (!string.IsNullOrWhiteSpace(configured) && Encoding.UTF8.GetByteCount(configured) >= 32)
        {
            Key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configured));
            return;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Jwt:SecretKey is missing or shorter than 32 bytes. Set it via configuration " +
                "or the Jwt__SecretKey environment variable. Refusing to start with a weak or " +
                "default signing key outside Development.");
        }

        logger.LogWarning(
            "Jwt:SecretKey is not configured; generated a RANDOM per-run key (Development only). " +
            "All tokens become invalid on restart. Configure Jwt:SecretKey for a stable key.");
        Key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64));
    }
}
