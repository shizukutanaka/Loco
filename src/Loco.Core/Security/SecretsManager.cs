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
/// - Values are encrypted with AES-256-GCM, an AEAD cipher: it authenticates as
///   well as encrypts, so a tampered store fails to decrypt instead of silently
///   yielding corrupted plaintext. (Unauthenticated modes such as CBC are
///   malleable and must not be used for at-rest secrets without a separate MAC.)
/// - The key is derived from a passphrase with PBKDF2-HMAC-SHA256 at 600,000
///   iterations over a per-store random salt - the work factor OWASP's Password
///   Storage Cheat Sheet recommends for PBKDF2-HMAC-SHA256.
/// - Each secret gets a fresh 96-bit nonce; the stored blob is nonce||tag||ciphertext.
///   Nonce reuse under a fixed key breaks GCM, so it is drawn from a CSPRNG per write.
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
    private const int NonceSize = 12; // 96-bit nonce - the size GCM is specified for
    private const int TagSize = 16;   // AesGcm.TagByteSizes.MaxSize
    private const int KeySize = 32;   // AES-256

    /// <summary>
    /// OWASP Password Storage Cheat Sheet's recommended work factor for
    /// PBKDF2-HMAC-SHA256. Deriving is deliberately expensive, so the result is
    /// cached per instance (see <see cref="_derivedKey"/>).
    /// </summary>
    private const int Pbkdf2Iterations = 600_000;

    /// <summary>Current on-disk format: PBKDF2-HMAC-SHA256(600k) + AES-256-GCM.</summary>
    private const int CurrentFormatVersion = 1;

    private readonly string _storePath;
    private readonly string _keyFilePath;
    private readonly object _gate = new();

    /// <summary>Cached PBKDF2 output, so a 600k-iteration derive happens once per process.</summary>
    private byte[]? _derivedKey;

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

            if (loaded.Version != CurrentFormatVersion)
            {
                throw new InvalidOperationException(
                    $"Secrets store at {_storePath} uses format version {loaded.Version}, " +
                    $"but this build reads version {CurrentFormatVersion}. " +
                    "Upgrade Loco, or re-create the store with the current version.");
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
        if (_derivedKey is not null) return _derivedKey;

        var passphrase = Environment.GetEnvironmentVariable("LOCO_SECRETS_PASSPHRASE");

        if (string.IsNullOrEmpty(passphrase))
        {
            passphrase = ReadOrCreateMachineKey();
        }

        _derivedKey = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passphrase),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return _derivedKey;
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

    /// <summary>
    /// AES-256-GCM. Layout: nonce(12) || tag(16) || ciphertext, base64-encoded.
    /// </summary>
    private string Encrypt(string plaintext, byte[] salt)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);

        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        // .NET 8 requires the tag size up front (the constructors without one are
        // obsolete: SYSLIB0053), which pins verification to the full 16-byte tag
        // and prevents truncated-tag acceptance.
        using var aesGcm = new AesGcm(DeriveKey(salt), TagSize);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var combined = new byte[NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, combined, NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, combined, NonceSize + TagSize, cipherBytes.Length);

        return Convert.ToBase64String(combined);
    }

    private string Decrypt(string cipherText, byte[] salt)
    {
        byte[] combined;
        try
        {
            combined = Convert.FromBase64String(cipherText);
        }
        catch (FormatException ex)
        {
            // Every other way of damaging the store surfaces as
            // InvalidOperationException - a short blob below, a failed tag
            // below that, unparseable JSON in Load. A caller that catches
            // InvalidOperationException to say "your secrets file is damaged"
            // would have crashed on this one instead, and it is not an exotic
            // case: a truncated write or a hand-edit produces it as readily as
            // a flipped byte does.
            throw new InvalidOperationException(
                "Stored secret is malformed (not valid base64). The secrets store " +
                "has been corrupted or edited by hand.", ex);
        }

        if (combined.Length < NonceSize + TagSize)
        {
            throw new InvalidOperationException(
                "Stored secret is malformed (too short to contain a nonce and tag).");
        }

        var nonce = new byte[NonceSize];
        Buffer.BlockCopy(combined, 0, nonce, 0, NonceSize);

        var tag = new byte[TagSize];
        Buffer.BlockCopy(combined, NonceSize, tag, 0, TagSize);

        var cipherBytes = new byte[combined.Length - NonceSize - TagSize];
        Buffer.BlockCopy(combined, NonceSize + TagSize, cipherBytes, 0, cipherBytes.Length);

        var plainBytes = new byte[cipherBytes.Length];

        using var aesGcm = new AesGcm(DeriveKey(salt), TagSize);
        try
        {
            // Throws if the tag does not verify - i.e. wrong passphrase OR the
            // stored blob was tampered with. GCM makes those detectable rather
            // than returning garbage plaintext.
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                "Failed to decrypt secret: authentication failed. Either the passphrase " +
                "(LOCO_SECRETS_PASSPHRASE) does not match the one used to store it, or the " +
                "secrets store has been modified.", ex);
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
        /// <summary>
        /// On-disk format version. Bump whenever the KDF or cipher changes so an
        /// older store is rejected loudly instead of decrypting to garbage.
        /// 1 = PBKDF2-HMAC-SHA256(600k) + AES-256-GCM.
        /// </summary>
        public int Version { get; set; } = CurrentFormatVersion;

        public byte[] Salt { get; set; } = Array.Empty<byte>();
        public Dictionary<string, StoredSecret> Secrets { get; set; } = new();
    }

    private sealed class StoredSecret
    {
        /// <summary>Base64 of nonce || tag || AES-256-GCM ciphertext.</summary>
        public string Cipher { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastAccessedAt { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
