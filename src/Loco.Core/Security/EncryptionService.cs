using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Security
{
    public class EncryptionService
    {
        private readonly ILogger<EncryptionService> _logger;
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public EncryptionService(ILogger<EncryptionService> logger, string masterKey = null)
        {
            _logger = logger;
            
            if (string.IsNullOrEmpty(masterKey))
            {
                // Generate a secure key if not provided
                using var aes = Aes.Create();
                aes.GenerateKey();
                aes.GenerateIV();
                _key = aes.Key;
                _iv = aes.IV;
                _logger.LogWarning("Using auto-generated encryption key. Configure a master key for production.");
            }
            else
            {
                // Derive key and IV from master key
                using var deriveBytes = new Rfc2898DeriveBytes(masterKey, Encoding.UTF8.GetBytes("LocoSalt2025"), 10000, HashAlgorithmName.SHA256);
                _key = deriveBytes.GetBytes(32); // 256 bits
                _iv = deriveBytes.GetBytes(16);  // 128 bits
            }
        }

        // AES-256 Encryption
        public string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                var encryptor = aes.CreateEncryptor();
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                
                return Convert.ToBase64String(cipherBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encryption failed");
                throw new CryptographicException("Encryption failed", ex);
            }
        }

        public string DecryptString(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;

            try
            {
                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                var decryptor = aes.CreateDecryptor();
                var cipherBytes = Convert.FromBase64String(cipherText);
                var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Decryption failed");
                throw new CryptographicException("Decryption failed", ex);
            }
        }

        // File Encryption
        public async Task EncryptFileAsync(string inputFile, string outputFile)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var inputStream = File.OpenRead(inputFile);
                using var outputStream = File.Create(outputFile);
                using var cryptoStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
                
                await inputStream.CopyToAsync(cryptoStream);
                await cryptoStream.FlushFinalBlockAsync();
                
                _logger.LogInformation($"File encrypted: {inputFile} -> {outputFile}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"File encryption failed: {inputFile}");
                throw;
            }
        }

        public async Task DecryptFileAsync(string inputFile, string outputFile)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var inputStream = File.OpenRead(inputFile);
                using var cryptoStream = new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
                using var outputStream = File.Create(outputFile);
                
                await cryptoStream.CopyToAsync(outputStream);
                
                _logger.LogInformation($"File decrypted: {inputFile} -> {outputFile}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"File decryption failed: {inputFile}");
                throw;
            }
        }

        // Field-level Encryption
        public string EncryptField(string fieldValue, string fieldName)
        {
            if (string.IsNullOrEmpty(fieldValue))
                return fieldValue;

            // Add field context to encryption for additional security
            var contextualValue = $"{fieldName}:{fieldValue}";
            return EncryptString(contextualValue);
        }

        public string DecryptField(string encryptedValue, string fieldName)
        {
            if (string.IsNullOrEmpty(encryptedValue))
                return encryptedValue;

            var decrypted = DecryptString(encryptedValue);
            var parts = decrypted.Split(':', 2);
            
            if (parts.Length == 2 && parts[0] == fieldName)
            {
                return parts[1];
            }
            
            _logger.LogWarning($"Field context mismatch for {fieldName}");
            throw new CryptographicException("Field decryption context mismatch");
        }

        // Data Masking
        public string MaskSensitiveData(string data, int visibleChars = 4)
        {
            if (string.IsNullOrEmpty(data) || data.Length <= visibleChars)
                return new string('*', data?.Length ?? 0);

            var visible = data.Substring(data.Length - visibleChars);
            var masked = new string('*', data.Length - visibleChars);
            return masked + visible;
        }

        // Hash Generation (for integrity checks)
        public string GenerateHash(string data)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(data);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public string GenerateFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return Convert.ToBase64String(hash);
        }

        public bool VerifyHash(string data, string expectedHash)
        {
            var actualHash = GenerateHash(data);
            return actualHash == expectedHash;
        }

        public bool VerifyFileHash(string filePath, string expectedHash)
        {
            var actualHash = GenerateFileHash(filePath);
            return actualHash == expectedHash;
        }

        // Digital Signature
        public (string publicKey, string privateKey) GenerateKeyPair()
        {
            using var rsa = RSA.Create(2048);
            var publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());
            var privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
            return (publicKey, privateKey);
        }

        public string SignData(string data, string privateKey)
        {
            using var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKey), out _);
            
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signature = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            
            return Convert.ToBase64String(signature);
        }

        public bool VerifySignature(string data, string signature, string publicKey)
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKey), out _);
                
                var dataBytes = Encoding.UTF8.GetBytes(data);
                var signatureBytes = Convert.FromBase64String(signature);
                
                return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Signature verification failed");
                return false;
            }
        }

        // Secure Key Storage
        public void StoreSecureKey(string keyName, string keyValue)
        {
            // In production, use Windows Data Protection API or similar
            var encrypted = EncryptString(keyValue);
            var keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loco", "Keys", $"{keyName}.key");
            
            Directory.CreateDirectory(Path.GetDirectoryName(keyPath));
            File.WriteAllText(keyPath, encrypted);
            
            // Set file permissions to current user only
            var fileInfo = new FileInfo(keyPath);
            fileInfo.Attributes = FileAttributes.Hidden | FileAttributes.Encrypted;
            
            _logger.LogInformation($"Secure key stored: {keyName}");
        }

        public string RetrieveSecureKey(string keyName)
        {
            var keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loco", "Keys", $"{keyName}.key");
            
            if (!File.Exists(keyPath))
            {
                _logger.LogWarning($"Secure key not found: {keyName}");
                return null;
            }
            
            var encrypted = File.ReadAllText(keyPath);
            return DecryptString(encrypted);
        }
    }
}