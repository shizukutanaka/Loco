using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Security;

/// <summary>
/// Production-ready security service with AES-256 encryption
/// Following John Carmack's performance principles
/// </summary>
public sealed class SecurityService : IDisposable
{
    private readonly ILogger<SecurityService> _logger;
    private readonly byte[] _key;
    private readonly byte[] _iv;
    private readonly Aes _aes;
    private bool _disposed;

    public SecurityService(ILogger<SecurityService> logger)
    {
        _logger = logger;
        _aes = Aes.Create();
        _aes.Mode = CipherMode.CBC;
        _aes.Padding = PaddingMode.PKCS7;
        _aes.KeySize = 256;
        
        // Generate or load secure keys
        (_key, _iv) = GenerateOrLoadKeys();
        _aes.Key = _key;
        _aes.IV = _iv;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<byte[]> EncryptAsync(byte[] data)
    {
        if (data == null || data.Length == 0) 
            throw new ArgumentException("Data cannot be null or empty", nameof(data));

        using var encryptor = _aes.CreateEncryptor();
        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        
        await cs.WriteAsync(data, 0, data.Length);
        await cs.FlushFinalBlockAsync();
        
        return ms.ToArray();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<byte[]> DecryptAsync(byte[] encryptedData)
    {
        if (encryptedData == null || encryptedData.Length == 0)
            throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));

        using var decryptor = _aes.CreateDecryptor();
        using var ms = new MemoryStream(encryptedData);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var result = new MemoryStream();
        
        await cs.CopyToAsync(result);
        return result.ToArray();
    }

    public string EncryptString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var data = Encoding.UTF8.GetBytes(plainText);
        var encrypted = EncryptAsync(data).GetAwaiter().GetResult();
        return Convert.ToBase64String(encrypted);
    }

    public string DecryptString(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        try
        {
            var data = Convert.FromBase64String(cipherText);
            var decrypted = DecryptAsync(data).GetAwaiter().GetResult();
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt string");
            throw new SecurityException("Decryption failed", ex);
        }
    }

    public string HashPassword(string password, byte[] salt = null)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be empty", nameof(password));

        salt ??= GenerateSalt();
        
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            100000, // iterations
            HashAlgorithmName.SHA256);
        
        var hash = pbkdf2.GetBytes(32);
        var hashBytes = new byte[48];
        Array.Copy(salt, 0, hashBytes, 0, 16);
        Array.Copy(hash, 0, hashBytes, 16, 32);
        
        return Convert.ToBase64String(hashBytes);
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
            return false;

        try
        {
            var hashBytes = Convert.FromBase64String(hashedPassword);
            if (hashBytes.Length != 48)
                return false;

            var salt = new byte[16];
            Array.Copy(hashBytes, 0, salt, 0, 16);

            var newHash = HashPassword(password, salt);
            return newHash == hashedPassword;
        }
        catch
        {
            return false;
        }
    }

    public string GenerateSecureToken(int length = 32)
    {
        var bytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public static byte[] GenerateSalt(int size = 16)
    {
        var salt = new byte[size];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return salt;
    }

    private (byte[] key, byte[] iv) GenerateOrLoadKeys()
    {
        var keyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Loco", ".keys");

        try
        {
            if (File.Exists(keyPath))
            {
                var data = File.ReadAllBytes(keyPath);
                if (data.Length == 48) // 32 bytes key + 16 bytes IV
                {
                    var key = new byte[32];
                    var iv = new byte[16];
                    Array.Copy(data, 0, key, 0, 32);
                    Array.Copy(data, 32, iv, 0, 16);
                    return (key, iv);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load encryption keys, generating new ones");
        }

        // Generate new keys
        using var aes = Aes.Create();
        aes.GenerateKey();
        aes.GenerateIV();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
            var data = new byte[48];
            Array.Copy(aes.Key, 0, data, 0, 32);
            Array.Copy(aes.IV, 0, data, 32, 16);
            File.WriteAllBytes(keyPath, data);
            
            // Set file permissions (Windows)
            if (OperatingSystem.IsWindows())
            {
                File.SetAttributes(keyPath, FileAttributes.Hidden | FileAttributes.System);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save encryption keys");
        }

        return (aes.Key, aes.IV);
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _aes?.Dispose();
        Array.Clear(_key, 0, _key.Length);
        Array.Clear(_iv, 0, _iv.Length);
        
        _disposed = true;
    }
}

/// <summary>
/// Custom security exception
/// </summary>
public class SecurityException : Exception
{
    public SecurityException(string message) : base(message) { }
    public SecurityException(string message, Exception inner) : base(message, inner) { }
}