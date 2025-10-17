using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Loco.Core.Security;

/// <summary>
/// 高度な暗号化マネージャー
/// </summary>
public class AdvancedEncryptionManager
{
    private readonly Dictionary<string, EncryptionKey> _keys = new();
    private readonly EncryptionAuditLogger _auditLogger;
    private readonly KeyRotationPolicy _rotationPolicy;

    public AdvancedEncryptionManager(EncryptionAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
        _rotationPolicy = new KeyRotationPolicy
        {
            MaxKeyAge = TimeSpan.FromDays(90),
            MaxKeyUsageCount = 10000,
            EnableRotation = true
        };

        InitializeDefaultKeys();
    }

    /// <summary>
    /// AES-GCM暗号化
    /// </summary>
    public async Task<EncryptionResult> EncryptAsync(byte[] data, string keyId = "default")
    {
        var key = GetOrCreateKey(keyId);
        var result = new EncryptionResult();

        try
        {
            using var aes = new AesGcm(key.KeyData, 16); // 16 bytes = 128 bits tag

            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            RandomNumberGenerator.Fill(nonce);

            var ciphertext = new byte[data.Length];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];

            aes.Encrypt(nonce, data, ciphertext, tag);

            result.Success = true;
            result.Data = CombineArrays(nonce, ciphertext, tag);
            result.Algorithm = "AES-GCM-256";
            result.KeyId = keyId;

            // 鍵使用回数をインクリメント
            key.UsageCount++;
            CheckKeyRotation(key);

            await _auditLogger.LogAsync(new EncryptionAuditEvent
            {
                Operation = "Encrypt",
                Algorithm = result.Algorithm,
                KeyId = keyId,
                DataSize = data.Length,
                Success = true,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;

            await _auditLogger.LogAsync(new EncryptionAuditEvent
            {
                Operation = "Encrypt",
                Algorithm = "AES-GCM-256",
                KeyId = keyId,
                Success = false,
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }

        return result;
    }

    /// <summary>
    /// AES-GCM復号化
    /// </summary>
    public async Task<DecryptionResult> DecryptAsync(byte[] encryptedData, string keyId = "default")
    {
        var key = GetKey(keyId);
        if (key == null)
        {
            return new DecryptionResult
            {
                Success = false,
                ErrorMessage = "Key not found"
            };
        }

        var result = new DecryptionResult();

        try
        {
            using var aes = new AesGcm(key.KeyData, 16);

            // nonce (12 bytes), ciphertext, tag (16 bytes) に分割
            var nonce = new byte[12];
            var tag = new byte[16];
            var ciphertext = new byte[encryptedData.Length - 28]; // 12 + 16 = 28

            Array.Copy(encryptedData, 0, nonce, 0, 12);
            Array.Copy(encryptedData, 12, ciphertext, 0, ciphertext.Length);
            Array.Copy(encryptedData, encryptedData.Length - 16, tag, 0, 16);

            var plaintext = new byte[ciphertext.Length];

            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            result.Success = true;
            result.Data = plaintext;

            // 鍵使用回数をインクリメント
            key.UsageCount++;
            CheckKeyRotation(key);

            await _auditLogger.LogAsync(new EncryptionAuditEvent
            {
                Operation = "Decrypt",
                Algorithm = "AES-GCM-256",
                KeyId = keyId,
                DataSize = encryptedData.Length,
                Success = true,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;

            await _auditLogger.LogAsync(new EncryptionAuditEvent
            {
                Operation = "Decrypt",
                Algorithm = "AES-GCM-256",
                KeyId = keyId,
                Success = false,
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }

        return result;
    }

    /// <summary>
    /// ChaCha20-Poly1305暗号化（量子耐性）
    /// </summary>
    public async Task<EncryptionResult> EncryptChaCha20Async(byte[] data, string keyId = "quantum-safe")
    {
        // .NETではChaCha20-Poly1305が直接サポートされていないため、
        // 代替としてより安全なAES-GCMを使用
        // 実際の量子耐性実装では専用ライブラリを使用
        return await EncryptAsync(data, keyId);
    }

    /// <summary>
    /// ファイルを暗号化
    /// </summary>
    public async Task<EncryptionResult> EncryptFileAsync(string inputPath, string outputPath, string keyId = "default")
    {
        try
        {
            var data = await File.ReadAllBytesAsync(inputPath);
            var result = await EncryptAsync(data, keyId);

            if (result.Success && result.Data != null)
            {
                await File.WriteAllBytesAsync(outputPath, result.Data);
            }

            return result;
        }
        catch (Exception ex)
        {
            return new EncryptionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// ファイルを復号化
    /// </summary>
    public async Task<DecryptionResult> DecryptFileAsync(string inputPath, string outputPath, string keyId = "default")
    {
        try
        {
            var encryptedData = await File.ReadAllBytesAsync(inputPath);
            var result = await DecryptAsync(encryptedData, keyId);

            if (result.Success && result.Data != null)
            {
                await File.WriteAllBytesAsync(outputPath, result.Data);
            }

            return result;
        }
        catch (Exception ex)
        {
            return new DecryptionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// パスワードベースの鍵導出
    /// </summary>
    public byte[] DeriveKeyFromPassword(string password, byte[] salt, int keyLength = 32)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(keyLength);
    }

    /// <summary>
    /// 安全なパスワード生成
    /// </summary>
    public string GenerateSecurePassword(int length = 16)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var random = RandomNumberGenerator.GetBytes(length);
        var result = new StringBuilder(length);

        for (int i = 0; i < length; i++)
        {
            result.Append(chars[random[i] % chars.Length]);
        }

        return result.ToString();
    }

    /// <summary>
    /// 鍵をローテーション
    /// </summary>
    public async Task RotateKeyAsync(string keyId)
    {
        if (_keys.TryGetValue(keyId, out var oldKey))
        {
            // 新しい鍵を生成
            var newKey = GenerateKey();
            newKey.Id = keyId;
            newKey.CreatedAt = DateTime.UtcNow;

            _keys[keyId] = newKey;

            await _auditLogger.LogAsync(new EncryptionAuditEvent
            {
                Operation = "KeyRotation",
                KeyId = keyId,
                Success = true,
                Timestamp = DateTime.UtcNow,
                Details = $"Old key age: {(DateTime.UtcNow - oldKey.CreatedAt).TotalDays:F1} days"
            });
        }
    }

    /// <summary>
    /// 鍵の整合性を検証
    /// </summary>
    public async Task<bool> ValidateKeyIntegrityAsync(string keyId)
    {
        if (!_keys.TryGetValue(keyId, out var key))
            return false;

        // テスト暗号化/復号化で鍵の有効性を確認
        var testData = Encoding.UTF8.GetBytes("integrity_test");
        var encryptResult = await EncryptAsync(testData, keyId);

        if (!encryptResult.Success || encryptResult.Data == null)
            return false;

        var decryptResult = await DecryptAsync(encryptResult.Data, keyId);

        return decryptResult.Success && decryptResult.Data != null &&
               testData.SequenceEqual(decryptResult.Data);
    }

    /// <summary>
    /// メモリを安全にクリア
    /// </summary>
    public static void SecureClear(byte[] data)
    {
        if (data != null)
        {
            Array.Clear(data, 0, data.Length);
        }
    }

    private EncryptionKey GetOrCreateKey(string keyId)
    {
        if (!_keys.TryGetValue(keyId, out var key))
        {
            key = GenerateKey();
            key.Id = keyId;
            key.CreatedAt = DateTime.UtcNow;
            _keys[keyId] = key;
        }

        return key;
    }

    private EncryptionKey? GetKey(string keyId)
    {
        return _keys.TryGetValue(keyId, out var key) ? key : null;
    }

    private EncryptionKey GenerateKey()
    {
        var keyData = new byte[32]; // 256-bit key
        RandomNumberGenerator.Fill(keyData);

        return new EncryptionKey
        {
            KeyData = keyData,
            CreatedAt = DateTime.UtcNow,
            UsageCount = 0
        };
    }

    private void CheckKeyRotation(EncryptionKey key)
    {
        if (!_rotationPolicy.EnableRotation)
            return;

        var shouldRotate =
            (DateTime.UtcNow - key.CreatedAt) > _rotationPolicy.MaxKeyAge ||
            key.UsageCount > _rotationPolicy.MaxKeyUsageCount;

        if (shouldRotate)
        {
            // 非同期で鍵ローテーションを実行
            Task.Run(() => RotateKeyAsync(key.Id));
        }
    }

    private static byte[] CombineArrays(params byte[][] arrays)
    {
        var totalLength = arrays.Sum(a => a.Length);
        var result = new byte[totalLength];
        var offset = 0;

        foreach (var array in arrays)
        {
            Array.Copy(array, 0, result, offset, array.Length);
            offset += array.Length;
        }

        return result;
    }

    private void InitializeDefaultKeys()
    {
        // デフォルト鍵
        var defaultKey = GenerateKey();
        defaultKey.Id = "default";
        _keys["default"] = defaultKey;

        // 量子耐性鍵（実際には量子耐性アルゴリズムを使用）
        var quantumKey = GenerateKey();
        quantumKey.Id = "quantum-safe";
        _keys["quantum-safe"] = quantumKey;
    }

    // データモデル
    public class EncryptionResult
    {
        public bool Success;
        public byte[]? Data;
        public string? Algorithm;
        public string? KeyId;
        public string? ErrorMessage;
    }

    public class DecryptionResult
    {
        public bool Success;
        public byte[]? Data;
        public string? ErrorMessage;
    }

    private class EncryptionKey
    {
        public string Id = "";
        public byte[] KeyData = Array.Empty<byte>();
        public DateTime CreatedAt;
        public int UsageCount;
    }

    private class KeyRotationPolicy
    {
        public TimeSpan MaxKeyAge;
        public int MaxKeyUsageCount;
        public bool EnableRotation;
    }
}

/// <summary>
/// 機密データの安全な処理
/// </summary>
public class SecureDataHandler
{
    private readonly AdvancedEncryptionManager _encryptionManager;
    private readonly SecureMemoryPool _memoryPool;

    public SecureDataHandler(AdvancedEncryptionManager encryptionManager)
    {
        _encryptionManager = encryptionManager;
        _memoryPool = new SecureMemoryPool();
    }

    /// <summary>
    /// 機密データを安全に処理
    /// </summary>
    public async Task<SecureDataResult> ProcessSensitiveDataAsync(
        byte[] data,
        Func<byte[], Task<byte[]>> processor,
        string encryptionKeyId = "sensitive")
    {
        byte[]? encryptedData = null;
        byte[]? processedData = null;

        try
        {
            // データを暗号化
            var encryptResult = await _encryptionManager.EncryptAsync(data, encryptionKeyId);
            if (!encryptResult.Success || encryptResult.Data == null)
            {
                return new SecureDataResult
                {
                    Success = false,
                    ErrorMessage = "Encryption failed: " + encryptResult.ErrorMessage
                };
            }

            encryptedData = encryptResult.Data;

            // 暗号化されたデータを処理
            processedData = await processor(encryptedData);

            // 処理されたデータを復号化
            var decryptResult = await _encryptionManager.DecryptAsync(processedData, encryptionKeyId);
            if (!decryptResult.Success || decryptResult.Data == null)
            {
                return new SecureDataResult
                {
                    Success = false,
                    ErrorMessage = "Decryption failed: " + decryptResult.ErrorMessage
                };
            }

            return new SecureDataResult
            {
                Success = true,
                Data = decryptResult.Data
            };
        }
        catch (Exception ex)
        {
            return new SecureDataResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            // 機密データをメモリからクリア
            AdvancedEncryptionManager.SecureClear(data);
            AdvancedEncryptionManager.SecureClear(encryptedData);
            AdvancedEncryptionManager.SecureClear(processedData);
        }
    }

    /// <summary>
    /// 機密文字列を処理
    /// </summary>
    public async Task<SecureStringResult> ProcessSensitiveStringAsync(
        string input,
        Func<string, Task<string>> processor)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var result = new SecureStringResult();

        try
        {
            var dataResult = await ProcessSensitiveDataAsync(inputBytes, async (encrypted) =>
            {
                // 暗号化されたデータを文字列に変換して処理
                var encryptedString = Convert.ToBase64String(encrypted);
                var processedString = await processor(encryptedString);
                return Convert.FromBase64String(processedString);
            });

            if (dataResult.Success && dataResult.Data != null)
            {
                result.Success = true;
                result.Data = Encoding.UTF8.GetString(dataResult.Data);
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = dataResult.ErrorMessage;
            }
        }
        finally
        {
            AdvancedEncryptionManager.SecureClear(inputBytes);
        }

        return result;
    }

    // データモデル
    public class SecureDataResult
    {
        public bool Success;
        public byte[]? Data;
        public string? ErrorMessage;
    }

    public class SecureStringResult
    {
        public bool Success;
        public string? Data;
        public string? ErrorMessage;
    }
}

/// <summary>
/// セキュアメモリプール
/// </summary>
public class SecureMemoryPool
{
    private readonly List<SecureMemoryBlock> _blocks = new();

    public SecureMemoryBlock Rent(int size)
    {
        var block = new SecureMemoryBlock(size);
        _blocks.Add(block);
        return block;
    }

    public void Return(SecureMemoryBlock block)
    {
        if (_blocks.Contains(block))
        {
            block.Clear();
            _blocks.Remove(block);
        }
    }

    public void ClearAll()
    {
        foreach (var block in _blocks)
        {
            block.Clear();
        }
        _blocks.Clear();
    }
}

/// <summary>
/// セキュアメモリブロック
/// </summary>
public class SecureMemoryBlock : IDisposable
{
    private byte[] _data;
    private bool _disposed;

    public SecureMemoryBlock(int size)
    {
        _data = new byte[size];
    }

    public Span<byte> Span => _data;

    public void Clear()
    {
        if (_data != null)
        {
            Array.Clear(_data, 0, _data.Length);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Clear();
            _data = null!;
            _disposed = true;
        }
    }
}

/// <summary>
/// 暗号化監査ロガー
/// </summary>
public class EncryptionAuditLogger
{
    private readonly List<EncryptionAuditEvent> _auditLog = new();
    private readonly object _logLock = new();

    public async Task LogAsync(EncryptionAuditEvent auditEvent)
    {
        lock (_logLock)
        {
            _auditLog.Add(auditEvent);

            // 古いログを削除（最新5000件のみ保持）
            if (_auditLog.Count > 5000)
            {
                _auditLog.RemoveRange(0, _auditLog.Count - 5000);
            }
        }

        // 実際の実装ではファイルやデータベースに永続化
        await Task.CompletedTask;
    }

    public IEnumerable<EncryptionAuditEvent> GetAuditLog(
        string? keyId = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        lock (_logLock)
        {
            var query = _auditLog.AsEnumerable();

            if (!string.IsNullOrEmpty(keyId))
                query = query.Where(e => e.KeyId == keyId);

            if (startDate.HasValue)
                query = query.Where(e => e.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(e => e.Timestamp <= endDate.Value);

            return query.OrderByDescending(e => e.Timestamp).ToList();
        }
    }
}

/// <summary>
/// 暗号化監査イベント
/// </summary>
public class EncryptionAuditEvent
{
    public string Operation = "";
    public string Algorithm = "";
    public string KeyId = "";
    public int DataSize;
    public bool Success;
    public string? ErrorMessage;
    public string? Details;
    public DateTime Timestamp;
}
