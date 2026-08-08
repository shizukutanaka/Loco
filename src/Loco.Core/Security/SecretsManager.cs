using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Loco.Core.Security;

/// <summary>
/// A stored secret's metadata. The secret VALUE is never exposed on this type -
/// callers get values only through <see cref="SecretsManager.GetSecret"/>, so
/// listing secrets cannot accidentally print them.
/// </summary>
public sealed class SecretEntry
{
    public string Key { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Local encrypted secret storage.
///
/// This class existed only as a reference from <c>Loco.Cli</c>'s SecretsCommand -
/// the type and its namespace were never implemented, so Loco.Cli could not
/// compile. This is the real implementation.
///
/// Design (deliberately BCL-only - no NuGet dependency):
/// - Values are encrypted with AES-256-CBC. The key is derived from a passphrase
///   with PBKDF2 (SHA-256, 200k iterations) over a per-store random salt.
/// - Each secret gets a fresh random IV, stored alongside its ciphertext.
/// - The passphrase comes from the LOCO_SECRETS_PASSPHRASE environment variable.
///   When unset, a machine-local key file (0600 where the OS supports it) is
///   generated so the CLI works out of the box; this protects against casual
///   disclosure (backups, git, screen sharing), NOT against an attacker who
///   already has read access to the user's home directory.
/// - Writes are atomic: serialize to .tmp then File.Move(overwrite) - the same
///   pattern used by JsonFileWorkflowStore.
///
/// Not a replacement for a real KMS/Vault in a server deployment; it is the
/// local-first equivalent, and the storage layer that connector credentials can
/// build on.
/// </summary>
public sealed class SecretsManager
{
    private const int SaltSize = 16;
    private const int IvSize = 16;
    private const int KeySize = 32; // AES-256
    private const int Pbkdf2Iterations = 200_000;

    private readonly string _storePath;
    private readonly string _keyFilePath;
    private readonly object _gate = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private SecretsFile? _cache;

    public SecretsManager(string? storeDirectory = null)
    {
        var dir = storeDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".loco",
            "secrets");

        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "secrets.json");
        _keyFilePath = Path.Combine(dir, "store.key");
    }

    // ---------------------------------------------------------------- public API

    /// <summary>
    /// Stores (or replaces) a secret. Replacing preserves the original CreatedAt.
    /// </summary>
    public void StoreSecret(
        string key,
        string value,
        string? description = null,
        Dictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Secret key must not be empty", nameof(key));
        ArgumentNullException.ThrowIfNull(value);

        lock (_gate)
        {
            var file = Load();
            var now = DateTime.UtcNow;

            file.Secrets.TryGetValue(key, out var existing);

            file.Secrets[key] = new StoredSecret
            {
                Cipher = Encrypt(value, file.Salt),
                Description = description ?? existing?.Description,
                CreatedAt = existing?.CreatedAt ?? now,
                UpdatedAt = existing is null ? null : now,
                LastAccessedAt = existing?.LastAccessedAt,
                Metadata = metadata ?? existing?.Metadata ?? new Dictionary<string, string>(),
            };

            Save(file);
        }
    }

    /// <summary>
    /// Returns the decrypted value, or null when the key is unknown.
    /// Records the access time as a side effect.
    /// </summary>
    public string? GetSecret(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        lock (_gate)
        {
            var file = Load();
            if (!file.Secrets.TryGetValue(key, out var stored)) return null;

            var value = Decrypt(stored.Cipher, file.Salt);

            stored.LastAccessedAt = DateTime.UtcNow;
            Save(file);

            return value;
        }
    }

    /// <summary>
    /// Replaces an existing secret's value. Returns false when the key is unknown.
    /// </summary>
    public bool UpdateSecret(string key, string newValue)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        ArgumentNullException.ThrowIfNull(newValue);

        lock (_gate)
        {
            var file = Load();
            if (!file.Secrets.TryGetValue(key, out var stored)) return false;

            stored.Cipher = Encrypt(newValue, file.Salt);
            stored.UpdatedAt = DateTime.UtcNow;

            Save(file);
            return true;
        }
    }

    /// <summary>
    /// Removes a secret. Returns false when the key is unknown.
    /// </summary>
    public bool DeleteSecret(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        lock (_gate)
        {
            var file = Load();
            if (!file.Secrets.Remove(key)) return false;

            Save(file);
            return true;
        }
    }

    /// <summary>
    /// Lists secret metadata (never values), ordered by key.
    /// </summary>
    public List<SecretEntry> ListSecrets()
    {
        lock (_gate)
        {
            var file = Load();
            return file.Secrets
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .Select(kvp => new SecretEntry
                {
                    Key = kvp.Key,
                    Description = kvp.Value.Description,
                    CreatedAt = kvp.Value.CreatedAt,
                    UpdatedAt = kvp.Value.UpdatedAt,
                    LastAccessedAt = kvp.Value.LastAccessedAt,
                    Metadata = kvp.Value.Metadata,
                })
                .ToList();
        }
    }

    /// <summary>True when a secret with this key exists.</summary>
    public bool ContainsSecret(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        lock (_gate)
        {
            return Load().Secrets.ContainsKey(key);
        }
    }

    // ------------------------------------------------------------- persistence

    private SecretsFile Load()
    {
        if (_cache is not null) return _cache;

        if (!File.Exists(_storePath))
        {
            _cache = new SecretsFile { Salt = RandomNumberGenerator.GetBytes(SaltSize) };
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(_storePath);
            var loaded = JsonSerializer.Deserialize<SecretsFile>(json, JsonOptions);

            // A corrupt or truncated store must not silently discard secrets, so
            // surface it rather than starting fresh over the top of the file.
            if (loaded is null || loaded.Salt.Length != SaltSize)
            {
                throw new InvalidOperationException(
                    $"Secrets store at {_storePath} is corrupt (bad salt). " +
                    "Restore it from a backup or delete it to start over.");
            }

            _cache = loaded;
            return _cache;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Secrets store at {_storePath} is not valid JSON. " +
                "Restore it from a backup or delete it to start over.", ex);
        }
    }

    private void Save(SecretsFile file)
    {
        _cache = file;

        var json = JsonSerializer.Serialize(file, JsonOptions);
        var tmpPath = _storePath + ".tmp";

        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, _storePath, overwrite: true);

        RestrictToOwner(_storePath);
    }

    // -------------------------------------------------------------- encryption

    private byte[] DeriveKey(byte[] salt)
    {
        var passphrase = Environment.GetEnvironmentVariable("LOCO_SECRETS_PASSPHRASE");

        if (string.IsNullOrEmpty(passphrase))
        {
            passphrase = ReadOrCreateMachineKey();
        }

        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passphrase),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
    }

    /// <summary>
    /// Machine-local fallback key so the CLI works without configuration.
    /// Generated once with a CSPRNG and stored owner-readable.
    /// </summary>
    private string ReadOrCreateMachineKey()
    {
        if (File.Exists(_keyFilePath))
        {
            return File.ReadAllText(_keyFilePath).Trim();
        }

        var generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeySize));
        File.WriteAllText(_keyFilePath, generated);
        RestrictToOwner(_keyFilePath);
        return generated;
    }

    private string Encrypt(string plaintext, byte[] salt)
    {
        var key = DeriveKey(salt);
        var iv = RandomNumberGenerator.GetBytes(IvSize);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prefix the IV so each secret carries the IV it was encrypted with
        var combined = new byte[iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(iv, 0, combined, 0, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, iv.Length, cipherBytes.Length);

        return Convert.ToBase64String(combined);
    }

    private string Decrypt(string cipherText, byte[] salt)
    {
        var combined = Convert.FromBase64String(cipherText);
        if (combined.Length <= IvSize)
        {
            throw new InvalidOperationException("Stored secret is malformed (too short to contain an IV).");
        }

        var iv = new byte[IvSize];
        Buffer.BlockCopy(combined, 0, iv, 0, IvSize);

        var cipherBytes = new byte[combined.Length - IvSize];
        Buffer.BlockCopy(combined, IvSize, cipherBytes, 0, cipherBytes.Length);

        using var aes = Aes.Create();
        aes.Key = DeriveKey(salt);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        try
        {
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                "Failed to decrypt secret - the passphrase (LOCO_SECRETS_PASSPHRASE) " +
                "does not match the one used to store it.", ex);
        }
    }

    /// <summary>
    /// Best-effort owner-only permissions. No-op on platforms without Unix modes.
    /// </summary>
    private static void RestrictToOwner(string path)
    {
        if (OperatingSystem.IsWindows()) return;

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception)
        {
            // Permission hardening is defense in depth; never block the operation
        }
    }

    // ------------------------------------------------------------- on-disk shape

    private sealed class SecretsFile
    {
        public byte[] Salt { get; set; } = Array.Empty<byte>();
        public Dictionary<string, StoredSecret> Secrets { get; set; } = new();
    }

    private sealed class StoredSecret
    {
        /// <summary>Base64 of IV || AES-256-CBC ciphertext.</summary>
        public string Cipher { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastAccessedAt { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
