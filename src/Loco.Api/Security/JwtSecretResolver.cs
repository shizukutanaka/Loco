using System.Security.Cryptography;

namespace Loco.Api.Security;

/// <summary>
/// Resolves the JWT signing secret without any shipped, shared default.
///
/// Resolution order:
/// 1. An explicitly configured secret (<c>Jwt:SecretKey</c>, normally the
///    <c>Jwt__SecretKey</c> environment variable) always wins.
/// 2. Otherwise a per-installation secret persisted next to the rule store
///    is used. On first start it is generated from a cryptographic random
///    source and written to that file, so tokens issued before a restart
///    keep validating after it.
/// 3. If neither is possible (the file cannot be read or written), startup
///    fails with an explicit reason instead of silently falling back to a
///    well-known value that every installation would share.
/// </summary>
public static class JwtSecretResolver
{
    /// <summary>File name of the persisted per-installation secret.</summary>
    public const string SecretFileName = "jwt-secret.key";

    // 48 random bytes = 384 bits of entropy, base64-encoded to 64 chars.
    private const int SecretBytes = 48;

    // HS256 keys shorter than this are treated as unusable when read back
    // from the persisted file (guards against a truncated/corrupted file).
    private const int MinimumSecretLength = 32;

    /// <summary>
    /// Returns the signing secret to use, generating and persisting one at
    /// <paramref name="secretFilePath"/> when nothing is configured.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The persisted file exists but is unusable, or a new secret could not
    /// be persisted. The message states the reason and the remedies.
    /// </exception>
    public static string Resolve(string? configuredSecret, string secretFilePath)
    {
        if (!string.IsNullOrWhiteSpace(configuredSecret))
        {
            return configuredSecret;
        }

        if (File.Exists(secretFilePath))
        {
            var stored = File.ReadAllText(secretFilePath).Trim();
            if (stored.Length >= MinimumSecretLength)
            {
                return stored;
            }

            throw new InvalidOperationException(
                $"The JWT secret file '{secretFilePath}' exists but does not contain a usable secret " +
                $"(at least {MinimumSecretLength} characters are required). Fix its contents, " +
                "delete it so a fresh secret is generated on startup, " +
                "or set the Jwt__SecretKey environment variable.");
        }

        var generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes));
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(secretFilePath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(secretFilePath, generated);
            TryRestrictToOwner(secretFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"No JWT secret is configured and a new one could not be persisted to '{secretFilePath}'. " +
                "Set the Jwt__SecretKey environment variable or make the data directory writable.", ex);
        }

        return generated;
    }

    /// <summary>
    /// The persisted secret lives in the same directory as the rule store
    /// file, i.e. in the installation's data directory (the compose named
    /// volume), so both survive together.
    /// </summary>
    public static string GetSecretFilePath(string ruleStorePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(ruleStorePath));
        return Path.Combine(directory!, SecretFileName);
    }

    private static void TryRestrictToOwner(string path)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch
        {
            // Best effort only: failing to tighten permissions must not
            // prevent startup; the secret is still per-installation random.
        }
    }
}
