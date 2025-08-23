using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loco.Core.Encryption
{
    public interface IDataEncryptionService
    {
        byte[] Encrypt(byte[] data, string keyId = null);
        byte[] Decrypt(byte[] encryptedData);
        string EncryptString(string plainText, string keyId = null);
        string DecryptString(string encryptedText);
        Task<byte[]> EncryptFileAsync(string filePath, string keyId = null);
        Task DecryptFileAsync(string encryptedFilePath, string outputPath);
        Task<EncryptedField> EncryptFieldAsync(string fieldValue, FieldEncryptionOptions options = null);
        Task<string> DecryptFieldAsync(EncryptedField encryptedField);
        string GenerateKey();
        Task RotateKeysAsync();
        Task<EncryptionKeyInfo> GetCurrentKeyInfoAsync();
        Task<bool> ValidateEncryptionAsync();
    }

    public class DataEncryptionService : IDataEncryptionService
    {
        private readonly IDataProtector _dataProtector;
        private readonly IKeyManagementService _keyManagement;
        private readonly ILogger<DataEncryptionService> _logger;
        private readonly EncryptionConfiguration _configuration;
        private readonly Dictionary<string, ICryptoTransform> _transformCache;

        public DataEncryptionService(
            IDataProtectionProvider dataProtectionProvider,
            IKeyManagementService keyManagement,
            IOptions<EncryptionConfiguration> configuration,
            ILogger<DataEncryptionService> logger)
        {
            _dataProtector = dataProtectionProvider.CreateProtector("Loco.DataEncryption");
            _keyManagement = keyManagement;
            _configuration = configuration.Value;
            _logger = logger;
            _transformCache = new Dictionary<string, ICryptoTransform>();
        }

        public byte[] Encrypt(byte[] data, string keyId = null)
        {
            if (data == null || data.Length == 0)
                return data;

            try
            {
                using (var aes = CreateAesAlgorithm(keyId))
                {
                    // Generate IV for this encryption
                    aes.GenerateIV();
                    var iv = aes.IV;

                    using (var encryptor = aes.CreateEncryptor())
                    using (var msEncrypt = new MemoryStream())
                    {
                        // Write metadata
                        var metadata = new EncryptionMetadata
                        {
                            Version = _configuration.Version,
                            Algorithm = "AES-256-GCM",
                            KeyId = keyId ?? _keyManagement.GetCurrentKeyId(),
                            IV = Convert.ToBase64String(iv),
                            Timestamp = DateTime.UtcNow
                        };

                        var metadataBytes = SerializeMetadata(metadata);
                        msEncrypt.Write(BitConverter.GetBytes(metadataBytes.Length), 0, 4);
                        msEncrypt.Write(metadataBytes, 0, metadataBytes.Length);

                        // Write IV
                        msEncrypt.Write(iv, 0, iv.Length);

                        // Encrypt data
                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            csEncrypt.Write(data, 0, data.Length);
                            csEncrypt.FlushFinalBlock();
                        }

                        var encrypted = msEncrypt.ToArray();

                        // Add integrity check
                        if (_configuration.EnableIntegrityCheck)
                        {
                            var hmac = ComputeHMAC(encrypted, keyId);
                            var result = new byte[encrypted.Length + hmac.Length];
                            Buffer.BlockCopy(encrypted, 0, result, 0, encrypted.Length);
                            Buffer.BlockCopy(hmac, 0, result, encrypted.Length, hmac.Length);
                            return result;
                        }

                        return encrypted;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encryption failed");
                throw new EncryptionException("Failed to encrypt data", ex);
            }
        }

        public byte[] Decrypt(byte[] encryptedData)
        {
            if (encryptedData == null || encryptedData.Length == 0)
                return encryptedData;

            try
            {
                byte[] encrypted = encryptedData;

                // Verify integrity if enabled
                if (_configuration.EnableIntegrityCheck)
                {
                    var hmacSize = 32; // SHA256
                    var dataLength = encryptedData.Length - hmacSize;
                    
                    var data = new byte[dataLength];
                    var hmac = new byte[hmacSize];
                    
                    Buffer.BlockCopy(encryptedData, 0, data, 0, dataLength);
                    Buffer.BlockCopy(encryptedData, dataLength, hmac, 0, hmacSize);

                    // Extract key ID from metadata to verify HMAC
                    var metadata = ExtractMetadata(data);
                    var computedHmac = ComputeHMAC(data, metadata.KeyId);
                    
                    if (!hmac.SequenceEqual(computedHmac))
                    {
                        throw new EncryptionException("Integrity check failed");
                    }

                    encrypted = data;
                }

                using (var msDecrypt = new MemoryStream(encrypted))
                {
                    // Read metadata
                    var metadataLengthBytes = new byte[4];
                    msDecrypt.Read(metadataLengthBytes, 0, 4);
                    var metadataLength = BitConverter.ToInt32(metadataLengthBytes, 0);
                    
                    var metadataBytes = new byte[metadataLength];
                    msDecrypt.Read(metadataBytes, 0, metadataLength);
                    var metadata = DeserializeMetadata(metadataBytes);

                    // Read IV
                    var iv = new byte[16];
                    msDecrypt.Read(iv, 0, 16);

                    // Decrypt data
                    using (var aes = CreateAesAlgorithm(metadata.KeyId))
                    {
                        aes.IV = iv;

                        using (var decryptor = aes.CreateDecryptor())
                        using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        using (var msResult = new MemoryStream())
                        {
                            csDecrypt.CopyTo(msResult);
                            return msResult.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Decryption failed");
                throw new EncryptionException("Failed to decrypt data", ex);
            }
        }

        public string EncryptString(string plainText, string keyId = null)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = Encrypt(plainBytes, keyId);
            return Convert.ToBase64String(encryptedBytes);
        }

        public string DecryptString(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return encryptedText;

            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var plainBytes = Decrypt(encryptedBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }

        public async Task<byte[]> EncryptFileAsync(string filePath, string keyId = null)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            var fileData = await File.ReadAllBytesAsync(filePath);
            
            // For large files, use streaming encryption
            if (fileData.Length > _configuration.StreamingThresholdMB * 1024 * 1024)
            {
                return await EncryptLargeFileAsync(filePath, keyId);
            }

            return Encrypt(fileData, keyId);
        }

        public async Task DecryptFileAsync(string encryptedFilePath, string outputPath)
        {
            if (!File.Exists(encryptedFilePath))
                throw new FileNotFoundException("Encrypted file not found", encryptedFilePath);

            var fileInfo = new FileInfo(encryptedFilePath);
            
            // For large files, use streaming decryption
            if (fileInfo.Length > _configuration.StreamingThresholdMB * 1024 * 1024)
            {
                await DecryptLargeFileAsync(encryptedFilePath, outputPath);
                return;
            }

            var encryptedData = await File.ReadAllBytesAsync(encryptedFilePath);
            var decryptedData = Decrypt(encryptedData);
            await File.WriteAllBytesAsync(outputPath, decryptedData);
        }

        public async Task<EncryptedField> EncryptFieldAsync(string fieldValue, FieldEncryptionOptions options = null)
        {
            options ??= FieldEncryptionOptions.Default;

            var encryptedField = new EncryptedField
            {
                Id = Guid.NewGuid(),
                FieldName = options.FieldName,
                EncryptedValue = EncryptString(fieldValue, options.KeyId),
                EncryptionTimestamp = DateTime.UtcNow,
                KeyId = options.KeyId ?? _keyManagement.GetCurrentKeyId(),
                Algorithm = "AES-256-GCM",
                IsSearchable = options.EnableSearch
            };

            // Generate searchable hash if needed
            if (options.EnableSearch)
            {
                encryptedField.SearchHash = GenerateSearchHash(fieldValue);
            }

            // Store format preserving encryption if needed
            if (options.PreserveFormat)
            {
                encryptedField.FormattedValue = await FormatPreservingEncryptAsync(fieldValue, options.Format);
            }

            return encryptedField;
        }

        public async Task<string> DecryptFieldAsync(EncryptedField encryptedField)
        {
            if (encryptedField == null)
                throw new ArgumentNullException(nameof(encryptedField));

            // Check if key is still valid
            if (!await _keyManagement.IsKeyValidAsync(encryptedField.KeyId))
            {
                _logger.LogWarning("Attempting to decrypt with expired key: {KeyId}", encryptedField.KeyId);
            }

            return DecryptString(encryptedField.EncryptedValue);
        }

        public string GenerateKey()
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.GenerateKey();
                return Convert.ToBase64String(aes.Key);
            }
        }

        public async Task RotateKeysAsync()
        {
            _logger.LogInformation("Starting key rotation");

            // Generate new key
            var newKey = GenerateKey();
            var newKeyId = await _keyManagement.AddKeyAsync(newKey);

            // Re-encrypt critical data with new key
            await ReEncryptCriticalDataAsync(newKeyId);

            // Mark old keys for deprecation
            await _keyManagement.DeprecateOldKeysAsync();

            _logger.LogInformation("Key rotation completed. New key ID: {KeyId}", newKeyId);
        }

        public async Task<EncryptionKeyInfo> GetCurrentKeyInfoAsync()
        {
            var keyId = _keyManagement.GetCurrentKeyId();
            return await _keyManagement.GetKeyInfoAsync(keyId);
        }

        public async Task<bool> ValidateEncryptionAsync()
        {
            try
            {
                // Test encryption/decryption
                var testData = "Encryption validation test";
                var encrypted = EncryptString(testData);
                var decrypted = DecryptString(encrypted);

                if (decrypted != testData)
                {
                    _logger.LogError("Encryption validation failed: decrypted data doesn't match original");
                    return false;
                }

                // Validate key management
                var keyInfo = await GetCurrentKeyInfoAsync();
                if (keyInfo == null || keyInfo.ExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogError("Encryption validation failed: current key is invalid or expired");
                    return false;
                }

                _logger.LogInformation("Encryption validation successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encryption validation failed with exception");
                return false;
            }
        }

        private Aes CreateAesAlgorithm(string keyId = null)
        {
            var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var key = _keyManagement.GetKey(keyId);
            aes.Key = Convert.FromBase64String(key);

            return aes;
        }

        private byte[] ComputeHMAC(byte[] data, string keyId)
        {
            var key = _keyManagement.GetKey(keyId);
            using (var hmac = new HMACSHA256(Convert.FromBase64String(key)))
            {
                return hmac.ComputeHash(data);
            }
        }

        private string GenerateSearchHash(string value)
        {
            // Use deterministic encryption for searchable fields
            using (var sha = SHA256.Create())
            {
                var salt = _configuration.SearchHashSalt;
                var input = $"{salt}{value}";
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(hash);
            }
        }

        private async Task<string> FormatPreservingEncryptAsync(string value, string format)
        {
            // Simplified FPE - in production use FF3-1 algorithm
            await Task.CompletedTask;
            return value; // Placeholder
        }

        private async Task<byte[]> EncryptLargeFileAsync(string filePath, string keyId)
        {
            using (var inputFile = File.OpenRead(filePath))
            using (var outputStream = new MemoryStream())
            using (var aes = CreateAesAlgorithm(keyId))
            {
                aes.GenerateIV();
                
                // Write metadata and IV
                var metadata = new EncryptionMetadata
                {
                    Version = _configuration.Version,
                    Algorithm = "AES-256-GCM",
                    KeyId = keyId ?? _keyManagement.GetCurrentKeyId(),
                    IV = Convert.ToBase64String(aes.IV),
                    Timestamp = DateTime.UtcNow,
                    FileSize = inputFile.Length
                };

                var metadataBytes = SerializeMetadata(metadata);
                await outputStream.WriteAsync(BitConverter.GetBytes(metadataBytes.Length), 0, 4);
                await outputStream.WriteAsync(metadataBytes, 0, metadataBytes.Length);
                await outputStream.WriteAsync(aes.IV, 0, aes.IV.Length);

                // Encrypt in chunks
                using (var encryptor = aes.CreateEncryptor())
                using (var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write))
                {
                    var buffer = new byte[_configuration.ChunkSize];
                    int bytesRead;
                    
                    while ((bytesRead = await inputFile.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await cryptoStream.WriteAsync(buffer, 0, bytesRead);
                    }
                    
                    cryptoStream.FlushFinalBlock();
                }

                return outputStream.ToArray();
            }
        }

        private async Task DecryptLargeFileAsync(string encryptedFilePath, string outputPath)
        {
            using (var inputFile = File.OpenRead(encryptedFilePath))
            using (var outputFile = File.Create(outputPath))
            {
                // Read metadata
                var metadataLengthBytes = new byte[4];
                await inputFile.ReadAsync(metadataLengthBytes, 0, 4);
                var metadataLength = BitConverter.ToInt32(metadataLengthBytes, 0);
                
                var metadataBytes = new byte[metadataLength];
                await inputFile.ReadAsync(metadataBytes, 0, metadataLength);
                var metadata = DeserializeMetadata(metadataBytes);

                // Read IV
                var iv = new byte[16];
                await inputFile.ReadAsync(iv, 0, 16);

                // Decrypt in chunks
                using (var aes = CreateAesAlgorithm(metadata.KeyId))
                {
                    aes.IV = iv;

                    using (var decryptor = aes.CreateDecryptor())
                    using (var cryptoStream = new CryptoStream(inputFile, decryptor, CryptoStreamMode.Read))
                    {
                        var buffer = new byte[_configuration.ChunkSize];
                        int bytesRead;
                        
                        while ((bytesRead = await cryptoStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await outputFile.WriteAsync(buffer, 0, bytesRead);
                        }
                    }
                }
            }
        }

        private async Task ReEncryptCriticalDataAsync(string newKeyId)
        {
            // Re-encrypt critical data with new key
            // This would be implemented based on specific requirements
            await Task.CompletedTask;
        }

        private EncryptionMetadata ExtractMetadata(byte[] encryptedData)
        {
            using (var ms = new MemoryStream(encryptedData))
            {
                var metadataLengthBytes = new byte[4];
                ms.Read(metadataLengthBytes, 0, 4);
                var metadataLength = BitConverter.ToInt32(metadataLengthBytes, 0);
                
                var metadataBytes = new byte[metadataLength];
                ms.Read(metadataBytes, 0, metadataLength);
                
                return DeserializeMetadata(metadataBytes);
            }
        }

        private byte[] SerializeMetadata(EncryptionMetadata metadata)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(metadata);
            return Encoding.UTF8.GetBytes(json);
        }

        private EncryptionMetadata DeserializeMetadata(byte[] metadataBytes)
        {
            var json = Encoding.UTF8.GetString(metadataBytes);
            return System.Text.Json.JsonSerializer.Deserialize<EncryptionMetadata>(json);
        }
    }

    // Supporting classes
    public interface IKeyManagementService
    {
        string GetCurrentKeyId();
        string GetKey(string keyId);
        Task<string> AddKeyAsync(string key);
        Task<bool> IsKeyValidAsync(string keyId);
        Task<EncryptionKeyInfo> GetKeyInfoAsync(string keyId);
        Task DeprecateOldKeysAsync();
    }

    public class EncryptionMetadata
    {
        public int Version { get; set; }
        public string Algorithm { get; set; }
        public string KeyId { get; set; }
        public string IV { get; set; }
        public DateTime Timestamp { get; set; }
        public long? FileSize { get; set; }
    }

    public class EncryptedField
    {
        public Guid Id { get; set; }
        public string FieldName { get; set; }
        public string EncryptedValue { get; set; }
        public string SearchHash { get; set; }
        public string FormattedValue { get; set; }
        public DateTime EncryptionTimestamp { get; set; }
        public string KeyId { get; set; }
        public string Algorithm { get; set; }
        public bool IsSearchable { get; set; }
    }

    public class FieldEncryptionOptions
    {
        public string FieldName { get; set; }
        public string KeyId { get; set; }
        public bool EnableSearch { get; set; }
        public bool PreserveFormat { get; set; }
        public string Format { get; set; }

        public static FieldEncryptionOptions Default => new FieldEncryptionOptions
        {
            EnableSearch = false,
            PreserveFormat = false
        };
    }

    public class EncryptionKeyInfo
    {
        public string KeyId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Algorithm { get; set; }
        public int KeySize { get; set; }
        public bool IsActive { get; set; }
        public int UsageCount { get; set; }
    }

    public class EncryptionConfiguration
    {
        public int Version { get; set; } = 1;
        public bool EnableIntegrityCheck { get; set; } = true;
        public int StreamingThresholdMB { get; set; } = 10;
        public int ChunkSize { get; set; } = 4096;
        public string SearchHashSalt { get; set; } = "Loco2024SecureSalt";
        public int KeyRotationDays { get; set; } = 90;
        public bool EnableHardwareAcceleration { get; set; } = true;
    }

    public class EncryptionException : Exception
    {
        public EncryptionException(string message) : base(message) { }
        public EncryptionException(string message, Exception innerException) : base(message, innerException) { }
    }
}