using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.DataProtection
{
    public interface IDataEncryptionService
    {
        byte[] Encrypt(byte[] data, string key = null);
        byte[] Decrypt(byte[] encryptedData, string key = null);
        string EncryptString(string plainText, string key = null);
        string DecryptString(string encryptedText, string key = null);
        Task<byte[]> EncryptAsync(byte[] data, string key = null);
        Task<byte[]> DecryptAsync(byte[] encryptedData, string key = null);
        string GenerateKey();
        string GenerateIV();
        void RotateKeys();
        bool VerifyIntegrity(byte[] data, byte[] signature);
    }

    public class AesDataEncryptionService : IDataEncryptionService
    {
        private readonly ILogger<AesDataEncryptionService> _logger;
        private readonly byte[] _masterKey;
        private readonly object _lockObject = new object();
        private byte[] _currentKey;
        private byte[] _previousKey;
        private DateTime _lastKeyRotation;
        private readonly TimeSpan _keyRotationInterval = TimeSpan.FromDays(30);

        private const int KeySize = 256;
        private const int BlockSize = 128;
        private const int Iterations = 100000;
        private const int SaltSize = 32;
        private const int IvSize = 16;
        private const int TagSize = 16;

        public AesDataEncryptionService(ILogger<AesDataEncryptionService> logger, string masterKey = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            if (string.IsNullOrEmpty(masterKey))
            {
                masterKey = GenerateKey();
                _logger.LogWarning("Using auto-generated master key. This should be configured in production.");
            }

            _masterKey = Convert.FromBase64String(masterKey);
            _currentKey = DeriveKey(_masterKey, "current");
            _previousKey = DeriveKey(_masterKey, "previous");
            _lastKeyRotation = DateTime.UtcNow;
        }

        public byte[] Encrypt(byte[] data, string key = null)
        {
            if (data == null || data.Length == 0)
                return data;

            try
            {
                using (var aesGcm = new AesGcm(GetEncryptionKey(key)))
                {
                    var nonce = new byte[AesGcm.NonceByteSizeInBytes];
                    var tag = new byte[AesGcm.TagByteSizeInBytes];
                    var ciphertext = new byte[data.Length];

                    using (var rng = RandomNumberGenerator.Create())
                    {
                        rng.GetBytes(nonce);
                    }

                    aesGcm.Encrypt(nonce, data, ciphertext, tag);

                    // Combine: nonce + tag + ciphertext
                    var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
                    Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
                    Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
                    Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encryption failed");
                throw new CryptographicException("Encryption failed", ex);
            }
        }

        public byte[] Decrypt(byte[] encryptedData, string key = null)
        {
            if (encryptedData == null || encryptedData.Length < AesGcm.NonceByteSizeInBytes + AesGcm.TagByteSizeInBytes)
                throw new ArgumentException("Invalid encrypted data");

            try
            {
                var nonce = new byte[AesGcm.NonceByteSizeInBytes];
                var tag = new byte[AesGcm.TagByteSizeInBytes];
                var ciphertext = new byte[encryptedData.Length - nonce.Length - tag.Length];

                Buffer.BlockCopy(encryptedData, 0, nonce, 0, nonce.Length);
                Buffer.BlockCopy(encryptedData, nonce.Length, tag, 0, tag.Length);
                Buffer.BlockCopy(encryptedData, nonce.Length + tag.Length, ciphertext, 0, ciphertext.Length);

                var plaintext = new byte[ciphertext.Length];

                // Try current key first
                try
                {
                    using (var aesGcm = new AesGcm(GetEncryptionKey(key)))
                    {
                        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
                        return plaintext;
                    }
                }
                catch
                {
                    // Try previous key if current fails (for key rotation support)
                    if (_previousKey != null && key == null)
                    {
                        using (var aesGcm = new AesGcm(_previousKey))
                        {
                            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
                            _logger.LogInformation("Data decrypted with previous key, consider re-encrypting");
                            return plaintext;
                        }
                    }
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Decryption failed");
                throw new CryptographicException("Decryption failed", ex);
            }
        }

        public string EncryptString(string plainText, string key = null)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = Encrypt(plainBytes, key);
            return Convert.ToBase64String(encryptedBytes);
        }

        public string DecryptString(string encryptedText, string key = null)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return encryptedText;

            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var plainBytes = Decrypt(encryptedBytes, key);
            return Encoding.UTF8.GetString(plainBytes);
        }

        public async Task<byte[]> EncryptAsync(byte[] data, string key = null)
        {
            return await Task.Run(() => Encrypt(data, key));
        }

        public async Task<byte[]> DecryptAsync(byte[] encryptedData, string key = null)
        {
            return await Task.Run(() => Decrypt(encryptedData, key));
        }

        public string GenerateKey()
        {
            var key = new byte[32]; // 256 bits
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key);
            }
            return Convert.ToBase64String(key);
        }

        public string GenerateIV()
        {
            var iv = new byte[16]; // 128 bits
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }
            return Convert.ToBase64String(iv);
        }

        public void RotateKeys()
        {
            lock (_lockObject)
            {
                _logger.LogInformation("Rotating encryption keys");
                _previousKey = _currentKey;
                _currentKey = DeriveKey(_masterKey, $"current_{DateTime.UtcNow:yyyyMMddHHmmss}");
                _lastKeyRotation = DateTime.UtcNow;
                _logger.LogInformation("Key rotation completed");
            }
        }

        public bool VerifyIntegrity(byte[] data, byte[] signature)
        {
            if (data == null || signature == null)
                return false;

            using (var hmac = new HMACSHA256(_currentKey))
            {
                var computedSignature = hmac.ComputeHash(data);
                return CryptographicOperations.FixedTimeEquals(computedSignature, signature);
            }
        }

        private byte[] GetEncryptionKey(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                return DeriveKey(Convert.FromBase64String(key), "encryption");
            }

            // Check if key rotation is needed
            if (DateTime.UtcNow - _lastKeyRotation > _keyRotationInterval)
            {
                RotateKeys();
            }

            return _currentKey;
        }

        private byte[] DeriveKey(byte[] masterKey, string purpose)
        {
            using (var deriveBytes = new Rfc2898DeriveBytes(
                masterKey,
                Encoding.UTF8.GetBytes($"Loco_{purpose}"),
                Iterations,
                HashAlgorithmName.SHA256))
            {
                return deriveBytes.GetBytes(32);
            }
        }
    }

    // Field-level encryption attribute
    [AttributeUsage(AttributeTargets.Property)]
    public class EncryptedAttribute : Attribute
    {
        public bool Required { get; set; } = true;
        public string Purpose { get; set; }
    }

    // Encryption helper for models
    public class ModelEncryptionHelper
    {
        private readonly IDataEncryptionService _encryptionService;
        private readonly ILogger<ModelEncryptionHelper> _logger;

        public ModelEncryptionHelper(
            IDataEncryptionService encryptionService,
            ILogger<ModelEncryptionHelper> logger)
        {
            _encryptionService = encryptionService;
            _logger = logger;
        }

        public void EncryptModel<T>(T model) where T : class
        {
            if (model == null) return;

            var properties = typeof(T).GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(EncryptedAttribute), false).Any());

            foreach (var property in properties)
            {
                if (property.PropertyType == typeof(string))
                {
                    var value = property.GetValue(model) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        var encrypted = _encryptionService.EncryptString(value);
                        property.SetValue(model, encrypted);
                    }
                }
            }
        }

        public void DecryptModel<T>(T model) where T : class
        {
            if (model == null) return;

            var properties = typeof(T).GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(EncryptedAttribute), false).Any());

            foreach (var property in properties)
            {
                if (property.PropertyType == typeof(string))
                {
                    var value = property.GetValue(model) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        try
                        {
                            var decrypted = _encryptionService.DecryptString(value);
                            property.SetValue(model, decrypted);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to decrypt property {property.Name}");
                        }
                    }
                }
            }
        }
    }

    // Secure string handling
    public sealed class SecureString : IDisposable
    {
        private readonly byte[] _encryptedData;
        private readonly IDataEncryptionService _encryptionService;
        private bool _disposed;

        public SecureString(string value, IDataEncryptionService encryptionService)
        {
            _encryptionService = encryptionService;
            if (!string.IsNullOrEmpty(value))
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                _encryptedData = _encryptionService.Encrypt(bytes);
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        public string GetValue()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SecureString));

            if (_encryptedData == null)
                return null;

            var decrypted = _encryptionService.Decrypt(_encryptedData);
            var value = Encoding.UTF8.GetString(decrypted);
            CryptographicOperations.ZeroMemory(decrypted);
            return value;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_encryptedData != null)
                {
                    CryptographicOperations.ZeroMemory(_encryptedData);
                }
                _disposed = true;
            }
        }
    }
}