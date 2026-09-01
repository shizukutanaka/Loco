namespace Loco.Api.Security;

/// <summary>
/// Bound from the "Auth" configuration section. Example appsettings:
///
///   "Auth": {
///     "Issuer": "loco-api",
///     "Audience": "loco-clients",
///     "TokenLifetimeMinutes": 60,
///     "Users": [
///       {
///         "Username": "admin",
///         "PasswordHash": "PBKDF2$100000$&lt;saltB64&gt;$&lt;hashB64&gt;",
///         "Scopes": [ "workflows:read", "workflows:manage", "workflows:execute" ]
///       }
///     ]
///   }
///
/// Generate a PasswordHash with:
///   printf 'your-password' | dotnet run --project src/Loco.Cli -- hash-password
/// </summary>
public class AuthOptions
{
    public string Issuer { get; set; } = "loco-api";
    public string Audience { get; set; } = "loco-clients";
    public int TokenLifetimeMinutes { get; set; } = 60;
    public List<AuthUser> Users { get; set; } = new();

    public TimeSpan TokenLifetime => TimeSpan.FromMinutes(
        TokenLifetimeMinutes is > 0 and <= 24 * 60 ? TokenLifetimeMinutes : 60);
}

public class AuthUser
{
    public string Username { get; set; } = "";

    /// <summary>PBKDF2$iterations$saltBase64$hashBase64 (see PasswordHasher).</summary>
    public string PasswordHash { get; set; } = "";

    public List<string> Scopes { get; set; } = new();
}
