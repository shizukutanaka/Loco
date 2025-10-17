using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Loco.Core.Security;

/// <summary>
/// 安全なパスワード/APIキー管理システム
/// </summary>
public class PasswordManager
{
    private readonly string _storagePath;
    private readonly byte[] _masterKey;
    private Dictionary<string, EncryptedCredential> _credentials;

    public PasswordManager(string storagePath, string masterPassword)
    {
        _storagePath = storagePath;
        _masterKey = DeriveKey(masterPassword, "LocoMasterSalt2025", 10000);
        _credentials = new Dictionary<string, EncryptedCredential>();
        LoadCredentials();
    }

    /// <summary>
    /// 資格情報を保存
    /// </summary>
    public void StoreCredential(string key, string username, string password, string? description = null)
    {
        var credential = new Credential
        {
            Username = username,
            Password = password,
            Description = description,
            Created = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        var encrypted = EncryptCredential(credential);
        _credentials[key] = encrypted;
        SaveCredentials();
    }

    /// <summary>
    /// 資格情報を取得
    /// </summary>
    public Credential? GetCredential(string key)
    {
        if (_credentials.TryGetValue(key, out var encrypted))
        {
            return DecryptCredential(encrypted);
        }
        return null;
    }

    /// <summary>
    /// 資格情報のユーザー名のみを取得
    /// </summary>
    public string? GetUsername(string key)
    {
        var credential = GetCredential(key);
        return credential?.Username;
    }

    /// <summary>
    /// 資格情報のパスワードのみを取得
    /// </summary>
    public string? GetPassword(string key)
    {
        var credential = GetCredential(key);
        return credential?.Password;
    }

    /// <summary>
    /// 資格情報を削除
    /// </summary>
    public bool RemoveCredential(string key)
    {
        if (_credentials.Remove(key))
        {
            SaveCredentials();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 保存されているすべての資格情報キーを取得
    /// </summary>
    public IEnumerable<string> ListKeys()
    {
        return _credentials.Keys;
    }

    /// <summary>
    /// 資格情報が存在するか確認
    /// </summary>
    public bool HasCredential(string key)
    {
        return _credentials.ContainsKey(key);
    }

    /// <summary>
    /// 資格情報を更新
    /// </summary>
    public void UpdateCredential(string key, string? username = null, string? password = null, string? description = null)
    {
        var existing = GetCredential(key);
        if (existing == null) return;

        if (username != null) existing.Username = username;
        if (password != null) existing.Password = password;
        if (description != null) existing.Description = description;
        existing.LastModified = DateTime.UtcNow;

        var encrypted = EncryptCredential(existing);
        _credentials[key] = encrypted;
        SaveCredentials();
    }

    private static byte[] DeriveKey(string password, string salt, int iterations = 10000)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, Encoding.UTF8.GetBytes(salt), iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32); // 256-bit key
    }

    private EncryptedCredential EncryptCredential(Credential credential)
    {
        var json = JsonSerializer.Serialize(credential);
        var plainBytes = Encoding.UTF8.GetBytes(json);

        using var aes = Aes.Create();
        aes.Key = _masterKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        return new EncryptedCredential
        {
            Data = Convert.ToBase64String(encryptedBytes),
            IV = Convert.ToBase64String(aes.IV),
            Salt = Convert.ToBase64String(Encoding.UTF8.GetBytes("LocoCredentialSalt2025"))
        };
    }

    private Credential DecryptCredential(EncryptedCredential encrypted)
    {
        var encryptedBytes = Convert.FromBase64String(encrypted.Data);
        var iv = Convert.FromBase64String(encrypted.IV);

        using var aes = Aes.Create();
        aes.Key = _masterKey;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

        var json = Encoding.UTF8.GetString(decryptedBytes);
        return JsonSerializer.Deserialize<Credential>(json)!;
    }

    private void LoadCredentials()
    {
        if (!File.Exists(_storagePath))
        {
            _credentials = new Dictionary<string, EncryptedCredential>();
            return;
        }

        try
        {
            var json = File.ReadAllText(_storagePath);
            var container = JsonSerializer.Deserialize<CredentialContainer>(json);
            _credentials = container?.Credentials ?? new Dictionary<string, EncryptedCredential>();
        }
        catch
        {
            // ファイルが破損している場合は空の辞書を作成
            _credentials = new Dictionary<string, EncryptedCredential>();
        }
    }

    private void SaveCredentials()
    {
        var container = new CredentialContainer
        {
            Version = "1.0",
            LastModified = DateTime.UtcNow,
            Credentials = _credentials
        };

        var json = JsonSerializer.Serialize(container, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        // 安全のため、バックアップを作成してから保存
        var backupPath = _storagePath + ".bak";
        if (File.Exists(_storagePath))
        {
            File.Copy(_storagePath, backupPath, true);
        }

        File.WriteAllText(_storagePath, json);

        // 保存が成功したらバックアップを削除
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }
    }

    /// <summary>
    /// マスターパスワードを検証
    /// </summary>
    public static bool ValidateMasterPassword(string storagePath, string masterPassword)
    {
        if (!File.Exists(storagePath)) return true; // 新規作成時は常に有効

        try
        {
            var json = File.ReadAllText(storagePath);
            var container = JsonSerializer.Deserialize<CredentialContainer>(json);
            if (container?.Credentials.Count == 0) return true;

            // テスト用の復号を試行
            var testKey = DeriveKey(masterPassword, "LocoMasterSalt2025", 10000);
            var firstCredential = container.Credentials.First().Value;

            var encryptedBytes = Convert.FromBase64String(firstCredential.Data);
            var iv = Convert.FromBase64String(firstCredential.IV);

            using var aes = Aes.Create();
            aes.Key = testKey;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// 資格情報クラス
/// </summary>
public class Credential
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Description { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
}

/// <summary>
/// 暗号化された資格情報
/// </summary>
public class EncryptedCredential
{
    public string Data { get; set; } = "";
    public string IV { get; set; } = "";
    public string Salt { get; set; } = "";
}

/// <summary>
/// 資格情報コンテナ
/// </summary>
public class CredentialContainer
{
    public string Version { get; set; } = "1.0";
    public DateTime LastModified { get; set; }
    public Dictionary<string, EncryptedCredential> Credentials { get; set; } = new();
}
