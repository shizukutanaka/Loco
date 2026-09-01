using System.Security.Cryptography;
using System.Text;

namespace Loco.Core.Security;

/// <summary>
/// PBKDF2 password hashing with a self-describing storage format:
///
///   PBKDF2$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;
///
/// Uses only BCL primitives (Rfc2898DeriveBytes.Pbkdf2 + FixedTimeEquals) -
/// no external packages, which matters in this repo (see Loco.Api.csproj notes).
///
/// Lives in Loco.Core rather than Loco.Api because the CLI needs it too: a
/// user has to be able to produce a hash for Auth:Users before the API will
/// let anyone in, and `loco hash-password` is where they get one. Nothing here
/// depends on ASP.NET.
/// </summary>
public static class PasswordHasher
{
    private const string Prefix = "PBKDF2";
    private const int DefaultIterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password, int iterations = DefaultIterations)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password must not be empty", nameof(password));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, HashSize);

        return $"{Prefix}${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Constant-time verification. Malformed stored hashes verify as false
    /// (never as true, never by throwing into a 500).
    /// </summary>
    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix)
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations < 1_000 || iterations > 10_000_000)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
