using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Loco.Core.Security;

/// <summary>
/// Enterprise-grade secrets management and credentials vault.
/// Based on 2025 best practices: HashiCorp Vault patterns, encrypted storage, rotation policies.
/// Key features: encryption at rest, access control, audit logging, secret rotation, versioning.
/// </summary>
public class SecretsManager
{
    private readonly ConcurrentDictionary<string, SecretEntry> _secrets = new();
    private readonly ConcurrentDictionary<string, AccessPolicy> _policies = new();
    private readonly List<AuditLogEntry> _auditLog = new();
    private readonly string _storageRoot;
    private readonly byte[] _masterKey;

    public SecretsManager(string? storageRoot = null, string? masterKeyHex = null)
    {
        _storageRoot = storageRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Loco", "secrets");

        Directory.CreateDirectory(_storageRoot);

        // Initialize or load master key
        _masterKey = masterKeyHex != null
            ? Convert.FromHexString(masterKeyHex)
            : LoadOrCreateMasterKey();

        LoadSecretsFromDisk();
    }

    #region Secret Operations

    public string StoreSecret(
        string key,
        string value,
        string? description = null,
        Dictionary<string, string>? metadata = null,
        TimeSpan? ttl = null)
    {
        // Encrypt the secret
        var encrypted = EncryptSecret(value);

        var entry = new SecretEntry
        {
            Id = Guid.NewGuid().ToString(),
            Key = key,
            EncryptedValue = encrypted,
            Description = description ?? "",
            Metadata = metadata ?? new Dictionary<string, string>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1,
            ExpiresAt = ttl.HasValue ? DateTime.UtcNow.Add(ttl.Value) : null
        };

        _secrets[key] = entry;
        PersistSecret(entry);

        LogAudit(AuditAction.SecretCreated, key, "Secret created successfully");

        return entry.Id;
    }

    public string? GetSecret(string key, string? requestedBy = null)
    {
        if (!_secrets.TryGetValue(key, out var entry))
        {
            LogAudit(AuditAction.SecretAccessDenied, key, "Secret not found", requestedBy);
            return null;
        }

        // Check expiration
        if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
        {
            LogAudit(AuditAction.SecretAccessDenied, key, "Secret expired", requestedBy);
            return null;
        }

        // Check access policy
        if (!CheckAccess(key, requestedBy))
        {
            LogAudit(AuditAction.SecretAccessDenied, key, "Access denied by policy", requestedBy);
            return null;
        }

        // Decrypt and return
        entry.LastAccessedAt = DateTime.UtcNow;
        entry.AccessCount++;

        LogAudit(AuditAction.SecretAccessed, key, "Secret accessed", requestedBy);

        return DecryptSecret(entry.EncryptedValue);
    }

    public bool UpdateSecret(
        string key,
        string newValue,
        string? updatedBy = null)
    {
        if (!_secrets.TryGetValue(key, out var entry))
            return false;

        // Store previous version
        var previousVersion = new SecretVersion
        {
            Version = entry.Version,
            EncryptedValue = entry.EncryptedValue,
            CreatedAt = entry.UpdatedAt
        };
        entry.PreviousVersions.Add(previousVersion);

        // Update with new encrypted value
        entry.EncryptedValue = EncryptSecret(newValue);
        entry.Version++;
        entry.UpdatedAt = DateTime.UtcNow;

        PersistSecret(entry);

        LogAudit(AuditAction.SecretUpdated, key, $"Secret updated to version {entry.Version}", updatedBy);

        return true;
    }

    public bool DeleteSecret(string key, string? deletedBy = null)
    {
        if (_secrets.TryRemove(key, out var entry))
        {
            DeleteSecretFromDisk(entry.Id);
            LogAudit(AuditAction.SecretDeleted, key, "Secret deleted", deletedBy);
            return true;
        }

        return false;
    }

    public List<SecretMetadata> ListSecrets(bool includeExpired = false)
    {
        var now = DateTime.UtcNow;

        return _secrets.Values
            .Where(s => includeExpired || !s.ExpiresAt.HasValue || s.ExpiresAt.Value > now)
            .Select(s => new SecretMetadata
            {
                Key = s.Key,
                Description = s.Description,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                Version = s.Version,
                ExpiresAt = s.ExpiresAt,
                AccessCount = s.AccessCount,
                LastAccessedAt = s.LastAccessedAt
            })
            .OrderBy(s => s.Key)
            .ToList();
    }

    #endregion

    #region Access Control

    public void CreateAccessPolicy(
        string policyName,
        List<string> allowedKeys,
        List<string> allowedPrincipals,
        AccessLevel level = AccessLevel.Read)
    {
        var policy = new AccessPolicy
        {
            Name = policyName,
            AllowedKeys = allowedKeys,
            AllowedPrincipals = allowedPrincipals,
            Level = level,
            CreatedAt = DateTime.UtcNow
        };

        _policies[policyName] = policy;

        LogAudit(AuditAction.PolicyCreated, policyName, $"Policy created with {level} access");
    }

    public bool CheckAccess(string key, string? principal)
    {
        // If no principal specified, allow (backward compatibility)
        if (string.IsNullOrEmpty(principal))
            return true;

        // Check all policies
        foreach (var policy in _policies.Values)
        {
            // Check if principal is allowed
            if (!policy.AllowedPrincipals.Contains(principal) && !policy.AllowedPrincipals.Contains("*"))
                continue;

            // Check if key is allowed
            if (policy.AllowedKeys.Contains(key) || policy.AllowedKeys.Contains("*"))
                return true;
        }

        return false;
    }

    public List<AccessPolicy> ListPolicies()
    {
        return _policies.Values.OrderBy(p => p.Name).ToList();
    }

    #endregion

    #region Secret Rotation

    public RotationResult RotateSecret(
        string key,
        Func<string, string> rotationFunc,
        string? rotatedBy = null)
    {
        var result = new RotationResult
        {
            Key = key,
            StartTime = DateTime.UtcNow
        };

        try
        {
            var currentValue = GetSecret(key, rotatedBy);
            if (currentValue == null)
            {
                result.Success = false;
                result.Error = "Secret not found or access denied";
                return result;
            }

            // Execute rotation function
            var newValue = rotationFunc(currentValue);

            // Update secret
            if (UpdateSecret(key, newValue, rotatedBy))
            {
                result.Success = true;
                result.NewVersion = _secrets[key].Version;

                LogAudit(AuditAction.SecretRotated, key, $"Secret rotated to version {result.NewVersion}", rotatedBy);
            }
            else
            {
                result.Success = false;
                result.Error = "Failed to update secret";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
        }

        return result;
    }

    public void SetupAutoRotation(
        string key,
        TimeSpan interval,
        Func<string, string> rotationFunc)
    {
        if (_secrets.TryGetValue(key, out var entry))
        {
            entry.AutoRotationEnabled = true;
            entry.AutoRotationInterval = interval;
            entry.NextRotationAt = DateTime.UtcNow.Add(interval);

            LogAudit(AuditAction.AutoRotationEnabled, key, $"Auto-rotation enabled with interval {interval}");
        }
    }

    public List<string> GetSecretsNeedingRotation()
    {
        var now = DateTime.UtcNow;

        return _secrets.Values
            .Where(s => s.AutoRotationEnabled && s.NextRotationAt.HasValue && s.NextRotationAt.Value <= now)
            .Select(s => s.Key)
            .ToList();
    }

    #endregion

    #region Versioning

    public string? GetSecretVersion(string key, int version)
    {
        if (!_secrets.TryGetValue(key, out var entry))
            return null;

        if (version == entry.Version)
            return DecryptSecret(entry.EncryptedValue);

        var previousVersion = entry.PreviousVersions.FirstOrDefault(v => v.Version == version);
        if (previousVersion != null)
            return DecryptSecret(previousVersion.EncryptedValue);

        return null;
    }

    public List<VersionInfo> GetVersionHistory(string key)
    {
        if (!_secrets.TryGetValue(key, out var entry))
            return new List<VersionInfo>();

        var versions = new List<VersionInfo>();

        // Add current version
        versions.Add(new VersionInfo
        {
            Version = entry.Version,
            CreatedAt = entry.UpdatedAt,
            IsCurrent = true
        });

        // Add previous versions
        versions.AddRange(entry.PreviousVersions.Select(v => new VersionInfo
        {
            Version = v.Version,
            CreatedAt = v.CreatedAt,
            IsCurrent = false
        }));

        return versions.OrderByDescending(v => v.Version).ToList();
    }

    public bool RollbackToVersion(string key, int version, string? rolledBackBy = null)
    {
        if (!_secrets.TryGetValue(key, out var entry))
            return false;

        var targetVersion = entry.PreviousVersions.FirstOrDefault(v => v.Version == version);
        if (targetVersion == null)
            return false;

        // Store current as previous version
        var currentVersion = new SecretVersion
        {
            Version = entry.Version,
            EncryptedValue = entry.EncryptedValue,
            CreatedAt = entry.UpdatedAt
        };
        entry.PreviousVersions.Add(currentVersion);

        // Restore target version
        entry.EncryptedValue = targetVersion.EncryptedValue;
        entry.Version++;
        entry.UpdatedAt = DateTime.UtcNow;

        PersistSecret(entry);

        LogAudit(AuditAction.SecretRolledBack, key, $"Rolled back from version {currentVersion.Version} to {version}", rolledBackBy);

        return true;
    }

    #endregion

    #region Audit Logging

    public List<AuditLogEntry> GetAuditLog(
        string? key = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int maxEntries = 100)
    {
        var query = _auditLog.AsEnumerable();

        if (!string.IsNullOrEmpty(key))
            query = query.Where(e => e.Key == key);

        if (startTime.HasValue)
            query = query.Where(e => e.Timestamp >= startTime.Value);

        if (endTime.HasValue)
            query = query.Where(e => e.Timestamp <= endTime.Value);

        return query
            .OrderByDescending(e => e.Timestamp)
            .Take(maxEntries)
            .ToList();
    }

    public AuditStatistics GetAuditStatistics()
    {
        var stats = new AuditStatistics
        {
            TotalEntries = _auditLog.Count,
            TotalSecrets = _secrets.Count
        };

        var last24Hours = DateTime.UtcNow.AddHours(-24);
        var recentLogs = _auditLog.Where(e => e.Timestamp >= last24Hours).ToList();

        stats.AccessesLast24Hours = recentLogs.Count(e => e.Action == AuditAction.SecretAccessed);
        stats.UpdatesLast24Hours = recentLogs.Count(e => e.Action == AuditAction.SecretUpdated);
        stats.DenialsLast24Hours = recentLogs.Count(e => e.Action == AuditAction.SecretAccessDenied);

        // Most accessed secrets
        stats.MostAccessedSecrets = _secrets.Values
            .OrderByDescending(s => s.AccessCount)
            .Take(10)
            .Select(s => new KeyValuePair<string, int>(s.Key, s.AccessCount))
            .ToList();

        return stats;
    }

    private void LogAudit(AuditAction action, string key, string details, string? principal = null)
    {
        var entry = new AuditLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = action,
            Key = key,
            Principal = principal ?? "system",
            Details = details
        };

        _auditLog.Add(entry);

        // Keep audit log size manageable (max 10000 entries)
        if (_auditLog.Count > 10000)
        {
            _auditLog.RemoveRange(0, 1000);
        }
    }

    #endregion

    #region Encryption

    private string EncryptSecret(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _masterKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertextBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Combine IV + ciphertext
        var combined = new byte[aes.IV.Length + ciphertextBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
        Buffer.BlockCopy(ciphertextBytes, 0, combined, aes.IV.Length, ciphertextBytes.Length);

        return Convert.ToBase64String(combined);
    }

    private string DecryptSecret(string encryptedBase64)
    {
        var combined = Convert.FromBase64String(encryptedBase64);

        using var aes = Aes.Create();
        aes.Key = _masterKey;

        // Extract IV and ciphertext
        var iv = new byte[aes.IV.Length];
        var ciphertext = new byte[combined.Length - iv.Length];

        Buffer.BlockCopy(combined, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(combined, iv.Length, ciphertext, 0, ciphertext.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private byte[] LoadOrCreateMasterKey()
    {
        var keyPath = Path.Combine(_storageRoot, ".masterkey");

        if (File.Exists(keyPath))
        {
            try
            {
                var keyHex = File.ReadAllText(keyPath);
                return Convert.FromHexString(keyHex);
            }
            catch
            {
                // Fall through to create new key
            }
        }

        // Generate new master key
        using var aes = Aes.Create();
        aes.GenerateKey();
        var key = aes.Key;

        // Save to disk with restricted permissions
        File.WriteAllText(keyPath, Convert.ToHexString(key));

        // Set file to read-only
        try
        {
            File.SetAttributes(keyPath, FileAttributes.ReadOnly | FileAttributes.Hidden);
        }
        catch
        {
            // Best effort
        }

        return key;
    }

    #endregion

    #region Persistence

    private void PersistSecret(SecretEntry entry)
    {
        var path = GetSecretPath(entry.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private void DeleteSecretFromDisk(string id)
    {
        var path = GetSecretPath(id);
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Best effort
            }
        }
    }

    private void LoadSecretsFromDisk()
    {
        var secretsDir = Path.Combine(_storageRoot, "entries");
        if (!Directory.Exists(secretsDir))
            return;

        foreach (var file in Directory.GetFiles(secretsDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var entry = JsonSerializer.Deserialize<SecretEntry>(json);
                if (entry != null)
                {
                    _secrets[entry.Key] = entry;
                }
            }
            catch
            {
                // Skip corrupted files
            }
        }
    }

    private string GetSecretPath(string id)
    {
        return Path.Combine(_storageRoot, "entries", $"{id}.json");
    }

    #endregion
}

#region Models

public class SecretEntry
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string EncryptedValue { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int Version { get; set; }
    public int AccessCount { get; set; }
    public List<SecretVersion> PreviousVersions { get; set; } = new();
    public bool AutoRotationEnabled { get; set; }
    public TimeSpan AutoRotationInterval { get; set; }
    public DateTime? NextRotationAt { get; set; }
}

public class SecretVersion
{
    public int Version { get; set; }
    public string EncryptedValue { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SecretMetadata
{
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int Version { get; set; }
    public int AccessCount { get; set; }
}

public class AccessPolicy
{
    public string Name { get; set; } = string.Empty;
    public List<string> AllowedKeys { get; set; } = new();
    public List<string> AllowedPrincipals { get; set; } = new();
    public AccessLevel Level { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum AccessLevel
{
    Read,
    Write,
    Admin
}

public class RotationResult
{
    public string Key { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int NewVersion { get; set; }
    public string? Error { get; set; }
}

public class VersionInfo
{
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsCurrent { get; set; }
}

public class AuditLogEntry
{
    public DateTime Timestamp { get; set; }
    public AuditAction Action { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Principal { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

public enum AuditAction
{
    SecretCreated,
    SecretAccessed,
    SecretUpdated,
    SecretDeleted,
    SecretRotated,
    SecretRolledBack,
    SecretAccessDenied,
    PolicyCreated,
    AutoRotationEnabled
}

public class AuditStatistics
{
    public int TotalEntries { get; set; }
    public int TotalSecrets { get; set; }
    public int AccessesLast24Hours { get; set; }
    public int UpdatesLast24Hours { get; set; }
    public int DenialsLast24Hours { get; set; }
    public List<KeyValuePair<string, int>> MostAccessedSecrets { get; set; } = new();
}

#endregion
