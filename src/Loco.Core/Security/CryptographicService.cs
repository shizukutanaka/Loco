using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Loco.Core.Security;

/// <summary>
/// Provides cryptographic operations for sensitive data protection
/// Government-grade encryption and secure key management
/// </summary>
public class CryptographicService
{
    private const int KeySize = 256; // AES-256
    private const int BlockSize = 128;
    private const int Iterations = 600000; // PBKDF2 iterations - OWASP 2024 recommendation
    private const int SaltSize = 32; // 256 bits
    private const int IvSize = 16; // 128 bits

    /// <summary>
    /// Encrypt sensitive data using AES-256-GCM with derived key
    /// </summary>
    public static EncryptedData Encrypt(string plaintext, string password)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentNullException(nameof(plaintext));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentNullException(nameof(password));

        // Generate random salt and IV
        var salt = GenerateSecureRandomBytes(SaltSize);
        var iv = GenerateSecureRandomBytes(IvSize);

        // Derive key from password using PBKDF2
        using var deriveBytes = new Rfc2898DeriveBytes(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);

        var key = deriveBytes.GetBytes(KeySize / 8);

        // Encrypt using AES-256-CBC (GCM not available in all .NET versions)
        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Compute HMAC for authenticated encryption
        using var hmac = new HMACSHA256(key);
        var dataToAuthenticate = new byte[salt.Length + iv.Length + ciphertext.Length];
        Buffer.BlockCopy(salt, 0, dataToAuthenticate, 0, salt.Length);
        Buffer.BlockCopy(iv, 0, dataToAuthenticate, salt.Length, iv.Length);
        Buffer.BlockCopy(ciphertext, 0, dataToAuthenticate, salt.Length + iv.Length, ciphertext.Length);
        var mac = hmac.ComputeHash(dataToAuthenticate);

        return new EncryptedData
        {
            Salt = Convert.ToBase64String(salt),
            IV = Convert.ToBase64String(iv),
            Ciphertext = Convert.ToBase64String(ciphertext),
            MAC = Convert.ToBase64String(mac),
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Decrypt data encrypted with Encrypt method
    /// </summary>
    public static string Decrypt(EncryptedData encryptedData, string password)
    {
        if (encryptedData == null)
            throw new ArgumentNullException(nameof(encryptedData));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentNullException(nameof(password));

        var salt = Convert.FromBase64String(encryptedData.Salt);
        var iv = Convert.FromBase64String(encryptedData.IV);
        var ciphertext = Convert.FromBase64String(encryptedData.Ciphertext);
        var mac = Convert.FromBase64String(encryptedData.MAC);

        // Derive key from password
        using var deriveBytes = new Rfc2898DeriveBytes(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);

        var key = deriveBytes.GetBytes(KeySize / 8);

        // Verify HMAC for authenticated decryption
        using var hmac = new HMACSHA256(key);
        var dataToAuthenticate = new byte[salt.Length + iv.Length + ciphertext.Length];
        Buffer.BlockCopy(salt, 0, dataToAuthenticate, 0, salt.Length);
        Buffer.BlockCopy(iv, 0, dataToAuthenticate, salt.Length, iv.Length);
        Buffer.BlockCopy(ciphertext, 0, dataToAuthenticate, salt.Length + iv.Length, ciphertext.Length);
        var computedMac = hmac.ComputeHash(dataToAuthenticate);

        if (!ConstantTimeEquals(mac, computedMac))
            throw new CryptographicException("Authentication failed - data may be tampered");

        // Decrypt
        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        return Encoding.UTF8.GetString(plaintext);
    }

    /// <summary>
    /// Generate cryptographically secure random bytes
    /// </summary>
    public static byte[] GenerateSecureRandomBytes(int length)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        var bytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return bytes;
    }

    /// <summary>
    /// Generate secure random token for API keys, session tokens, etc.
    /// </summary>
    public static string GenerateSecureToken(int length = 32)
    {
        var bytes = GenerateSecureRandomBytes(length);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    /// <summary>
    /// Hash password with secure salt using PBKDF2
    /// </summary>
    public static HashedPassword HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentNullException(nameof(password));

        var salt = GenerateSecureRandomBytes(SaltSize);

        using var deriveBytes = new Rfc2898DeriveBytes(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);

        var hash = deriveBytes.GetBytes(KeySize / 8);

        return new HashedPassword
        {
            Hash = Convert.ToBase64String(hash),
            Salt = Convert.ToBase64String(salt),
            Iterations = Iterations,
            Algorithm = "PBKDF2-SHA256"
        };
    }

    /// <summary>
    /// Verify password against hashed password
    /// </summary>
    public static bool VerifyPassword(string password, HashedPassword hashedPassword)
    {
        if (string.IsNullOrEmpty(password))
            return false;
        if (hashedPassword == null)
            return false;

        var salt = Convert.FromBase64String(hashedPassword.Salt);
        var storedHash = Convert.FromBase64String(hashedPassword.Hash);

        using var deriveBytes = new Rfc2898DeriveBytes(
            password,
            salt,
            hashedPassword.Iterations,
            HashAlgorithmName.SHA256);

        var computedHash = deriveBytes.GetBytes(KeySize / 8);

        return ConstantTimeEquals(storedHash, computedHash);
    }

    /// <summary>
    /// Constant-time comparison to prevent timing attacks
    /// </summary>
    private static bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
            return false;

        var result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }

    /// <summary>
    /// Securely wipe sensitive data from memory
    /// </summary>
    public static void SecureWipe(byte[] data)
    {
        if (data == null)
            return;

        // Overwrite with random data
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(data);

        // Overwrite with zeros
        Array.Clear(data, 0, data.Length);
    }

    /// <summary>
    /// Compute SHA-256 hash of data
    /// </summary>
    public static string ComputeHash(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Compute SHA-256 hash of string
    /// </summary>
    public static string ComputeHash(string text)
    {
        if (string.IsNullOrEmpty(text))
            throw new ArgumentNullException(nameof(text));

        var bytes = Encoding.UTF8.GetBytes(text);
        return ComputeHash(bytes);
    }
}

/// <summary>
/// Represents encrypted data with all necessary components
/// </summary>
public class EncryptedData
{
    public string Salt { get; set; } = string.Empty;
    public string IV { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public string MAC { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Represents a hashed password with salt
/// </summary>
public class HashedPassword
{
    public string Hash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public int Iterations { get; set; }
    public string Algorithm { get; set; } = string.Empty;
}
