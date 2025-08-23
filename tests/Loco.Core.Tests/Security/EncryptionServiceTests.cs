using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Loco.Core.Security;

namespace Loco.Core.Tests.Security
{
    public class EncryptionServiceTests : IDisposable
    {
        private readonly EncryptionService _encryptionService;
        private readonly Mock<ILogger<EncryptionService>> _loggerMock;
        private readonly string _testFilePath;
        private readonly string _encryptedFilePath;

        public EncryptionServiceTests()
        {
            _loggerMock = new Mock<ILogger<EncryptionService>>();
            _encryptionService = new EncryptionService(_loggerMock.Object, "TestMasterKey123!");
            _testFilePath = Path.GetTempFileName();
            _encryptedFilePath = _testFilePath + ".encrypted";
        }

        [Fact]
        public void EncryptString_DecryptString_RoundTrip()
        {
            var plainText = "This is a secret message!";

            var encrypted = _encryptionService.EncryptString(plainText);
            Assert.NotNull(encrypted);
            Assert.NotEqual(plainText, encrypted);

            var decrypted = _encryptionService.DecryptString(encrypted);
            Assert.Equal(plainText, decrypted);
        }

        [Fact]
        public void EncryptString_EmptyString_ReturnsEmpty()
        {
            var encrypted = _encryptionService.EncryptString("");
            Assert.Equal("", encrypted);

            var decrypted = _encryptionService.DecryptString("");
            Assert.Equal("", decrypted);
        }

        [Fact]
        public void EncryptString_DifferentInputs_ProduceDifferentOutputs()
        {
            var plainText1 = "Message 1";
            var plainText2 = "Message 2";

            var encrypted1 = _encryptionService.EncryptString(plainText1);
            var encrypted2 = _encryptionService.EncryptString(plainText2);

            Assert.NotEqual(encrypted1, encrypted2);
        }

        [Fact]
        public async Task EncryptFile_DecryptFile_RoundTrip()
        {
            // Arrange
            var testContent = "This is test file content for encryption testing.";
            await File.WriteAllTextAsync(_testFilePath, testContent);
            var decryptedFilePath = _testFilePath + ".decrypted";

            try
            {
                // Act
                await _encryptionService.EncryptFileAsync(_testFilePath, _encryptedFilePath);
                Assert.True(File.Exists(_encryptedFilePath));

                await _encryptionService.DecryptFileAsync(_encryptedFilePath, decryptedFilePath);
                Assert.True(File.Exists(decryptedFilePath));

                // Assert
                var decryptedContent = await File.ReadAllTextAsync(decryptedFilePath);
                Assert.Equal(testContent, decryptedContent);

                // Encrypted file should be different from original
                var encryptedBytes = await File.ReadAllBytesAsync(_encryptedFilePath);
                var originalBytes = Encoding.UTF8.GetBytes(testContent);
                Assert.NotEqual(originalBytes, encryptedBytes);
            }
            finally
            {
                // Cleanup
                if (File.Exists(decryptedFilePath))
                    File.Delete(decryptedFilePath);
            }
        }

        [Fact]
        public void EncryptField_DecryptField_WithContext()
        {
            var fieldName = "creditCard";
            var fieldValue = "1234-5678-9012-3456";

            var encrypted = _encryptionService.EncryptField(fieldValue, fieldName);
            Assert.NotNull(encrypted);
            Assert.NotEqual(fieldValue, encrypted);

            var decrypted = _encryptionService.DecryptField(encrypted, fieldName);
            Assert.Equal(fieldValue, decrypted);
        }

        [Fact]
        public void DecryptField_WrongContext_ThrowsException()
        {
            var fieldName = "creditCard";
            var wrongFieldName = "email";
            var fieldValue = "1234-5678-9012-3456";

            var encrypted = _encryptionService.EncryptField(fieldValue, fieldName);

            Assert.Throws<System.Security.Cryptography.CryptographicException>(() =>
                _encryptionService.DecryptField(encrypted, wrongFieldName));
        }

        [Theory]
        [InlineData("1234567890", 4, "******7890")]
        [InlineData("test@email.com", 4, "**********com")]
        [InlineData("abc", 4, "***")]
        [InlineData("", 4, "")]
        public void MaskSensitiveData_MasksCorrectly(string input, int visibleChars, string expected)
        {
            var result = _encryptionService.MaskSensitiveData(input, visibleChars);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GenerateHash_ConsistentForSameInput()
        {
            var data = "Test data for hashing";

            var hash1 = _encryptionService.GenerateHash(data);
            var hash2 = _encryptionService.GenerateHash(data);

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void GenerateHash_DifferentForDifferentInput()
        {
            var data1 = "Test data 1";
            var data2 = "Test data 2";

            var hash1 = _encryptionService.GenerateHash(data1);
            var hash2 = _encryptionService.GenerateHash(data2);

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void VerifyHash_ValidatesCorrectly()
        {
            var data = "Test data for verification";
            var hash = _encryptionService.GenerateHash(data);

            Assert.True(_encryptionService.VerifyHash(data, hash));
            Assert.False(_encryptionService.VerifyHash("Different data", hash));
        }

        [Fact]
        public void GenerateFileHash_ConsistentForSameFile()
        {
            File.WriteAllText(_testFilePath, "Test file content");

            var hash1 = _encryptionService.GenerateFileHash(_testFilePath);
            var hash2 = _encryptionService.GenerateFileHash(_testFilePath);

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void GenerateKeyPair_CreatesValidKeys()
        {
            var (publicKey, privateKey) = _encryptionService.GenerateKeyPair();

            Assert.NotNull(publicKey);
            Assert.NotNull(privateKey);
            Assert.NotEqual(publicKey, privateKey);
            Assert.True(publicKey.Length > 0);
            Assert.True(privateKey.Length > 0);
        }

        [Fact]
        public void SignData_VerifySignature_RoundTrip()
        {
            var data = "Data to be signed";
            var (publicKey, privateKey) = _encryptionService.GenerateKeyPair();

            var signature = _encryptionService.SignData(data, privateKey);
            Assert.NotNull(signature);

            Assert.True(_encryptionService.VerifySignature(data, signature, publicKey));
            Assert.False(_encryptionService.VerifySignature("Different data", signature, publicKey));
        }

        [Fact]
        public void StoreSecureKey_RetrieveSecureKey_RoundTrip()
        {
            var keyName = $"test-key-{Guid.NewGuid()}";
            var keyValue = "SuperSecretKey123!";

            try
            {
                _encryptionService.StoreSecureKey(keyName, keyValue);
                var retrieved = _encryptionService.RetrieveSecureKey(keyName);

                Assert.Equal(keyValue, retrieved);
            }
            finally
            {
                // Cleanup
                var keyPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Loco", "Keys", $"{keyName}.key");
                if (File.Exists(keyPath))
                    File.Delete(keyPath);
            }
        }

        [Fact]
        public void RetrieveSecureKey_NonExistent_ReturnsNull()
        {
            var result = _encryptionService.RetrieveSecureKey("non-existent-key");
            Assert.Null(result);
        }

        public void Dispose()
        {
            if (File.Exists(_testFilePath))
                File.Delete(_testFilePath);
            if (File.Exists(_encryptedFilePath))
                File.Delete(_encryptedFilePath);
        }
    }
}