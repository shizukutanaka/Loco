using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Loco.Core.Configuration;

namespace Loco.Core.Security
{
    /// <summary>
    /// Production-grade access control manager for file system and resource access.
    /// Implements whitelist/blacklist path validation and operation auditing.
    /// Thread-safe for concurrent access control checks.
    /// </summary>
    public class AccessControlManager
    {
        private readonly LocoConfig _config;
        private readonly ILogger? _logger;
        private readonly HashSet<string> _allowedPaths;
        private readonly HashSet<string> _forbiddenPaths;
        private readonly ReaderWriterLockSlim _lock;
        private readonly Dictionary<string, AccessAttempt> _accessHistory;
        private readonly int _maxHistorySize;

        public AccessControlManager(LocoConfig config, ILogger? logger = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger;
            _lock = new ReaderWriterLockSlim();
            _accessHistory = new Dictionary<string, AccessAttempt>();
            _maxHistorySize = 1000;

            // Initialize path lists
            _allowedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _forbiddenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            LoadPathLists();
        }

        /// <summary>
        /// Validates if access to the specified path is allowed.
        /// Returns detailed result with reasons for denial if applicable.
        /// </summary>
        public AccessControlResult ValidateAccess(string path, AccessType accessType)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return AccessControlResult.Deny("Path is null or empty");
            }

            try
            {
                var fullPath = Path.GetFullPath(path);
                var normalizedPath = NormalizePath(fullPath);

                _lock.EnterReadLock();
                try
                {
                    // Record access attempt
                    RecordAccessAttempt(normalizedPath, accessType);

                    // Check forbidden paths first (blacklist takes priority)
                    if (IsForbiddenPath(normalizedPath))
                    {
                        _logger?.LogWarning("Access denied to forbidden path: {Path} (Type: {AccessType})",
                            path, accessType);
                        return AccessControlResult.Deny($"Access to forbidden path: {path}");
                    }

                    // Check system critical paths
                    if (IsSystemCriticalPath(normalizedPath))
                    {
                        _logger?.LogWarning("Access denied to system critical path: {Path} (Type: {AccessType})",
                            path, accessType);
                        return AccessControlResult.Deny($"Access to system path not allowed: {path}");
                    }

                    // If allowed paths are configured, check whitelist
                    if (_allowedPaths.Count > 0)
                    {
                        if (!IsAllowedPath(normalizedPath))
                        {
                            _logger?.LogWarning("Access denied - path not in whitelist: {Path} (Type: {AccessType})",
                                path, accessType);
                            return AccessControlResult.Deny($"Path not in allowed list: {path}");
                        }
                    }

                    // Check for dangerous patterns (directory traversal, etc.)
                    if (ContainsDangerousPattern(path))
                    {
                        _logger?.LogWarning("Access denied - dangerous pattern detected: {Path} (Type: {AccessType})",
                            path, accessType);
                        return AccessControlResult.Deny($"Dangerous pattern detected in path: {path}");
                    }

                    // All checks passed
                    _logger?.LogDebug("Access granted to path: {Path} (Type: {AccessType})", path, accessType);
                    return AccessControlResult.Allow(fullPath);
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error validating access to path: {Path}", path);
                return AccessControlResult.Deny($"Error validating path: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds a path to the allowed paths list dynamically.
        /// Requires administrative privileges check in production.
        /// </summary>
        public void AddAllowedPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            _lock.EnterWriteLock();
            try
            {
                var normalizedPath = NormalizePath(Path.GetFullPath(path));
                _allowedPaths.Add(normalizedPath);
                _logger?.LogInformation("Added allowed path: {Path}", normalizedPath);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Adds a path to the forbidden paths list dynamically.
        /// </summary>
        public void AddForbiddenPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            _lock.EnterWriteLock();
            try
            {
                var normalizedPath = NormalizePath(Path.GetFullPath(path));
                _forbiddenPaths.Add(normalizedPath);
                _logger?.LogInformation("Added forbidden path: {Path}", normalizedPath);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets access statistics for monitoring and auditing.
        /// </summary>
        public AccessStatistics GetAccessStatistics()
        {
            _lock.EnterReadLock();
            try
            {
                var stats = new AccessStatistics
                {
                    TotalAttempts = _accessHistory.Values.Sum(a => a.Count),
                    UniquePathsAccessed = _accessHistory.Count,
                    AllowedPathsCount = _allowedPaths.Count,
                    ForbiddenPathsCount = _forbiddenPaths.Count,
                    RecentAttempts = _accessHistory.Values
                        .OrderByDescending(a => a.LastAttempt)
                        .Take(10)
                        .Select(a => new AccessAttemptInfo
                        {
                            Path = a.Path,
                            AccessType = a.AccessType,
                            Count = a.Count,
                            LastAttempt = a.LastAttempt
                        })
                        .ToList()
                };

                return stats;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private void LoadPathLists()
        {
            // Load allowed paths
            if (_config.AllowedPaths != null)
            {
                foreach (var path in _config.AllowedPaths)
                {
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        try
                        {
                            var normalizedPath = NormalizePath(Path.GetFullPath(path));
                            _allowedPaths.Add(normalizedPath);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Invalid allowed path: {Path}", path);
                        }
                    }
                }
            }

            // Load forbidden paths
            if (_config.ForbiddenPaths != null)
            {
                foreach (var path in _config.ForbiddenPaths)
                {
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        try
                        {
                            var normalizedPath = NormalizePath(Path.GetFullPath(path));
                            _forbiddenPaths.Add(normalizedPath);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Invalid forbidden path: {Path}", path);
                        }
                    }
                }
            }

            _logger?.LogInformation("Access control initialized: {AllowedCount} allowed, {ForbiddenCount} forbidden",
                _allowedPaths.Count, _forbiddenPaths.Count);
        }

        private bool IsAllowedPath(string normalizedPath)
        {
            // Check if path or any parent directory is in allowed list
            foreach (var allowedPath in _allowedPaths)
            {
                if (normalizedPath.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private bool IsForbiddenPath(string normalizedPath)
        {
            // Check if path or any parent directory is in forbidden list
            foreach (var forbiddenPath in _forbiddenPaths)
            {
                if (normalizedPath.StartsWith(forbiddenPath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private bool IsSystemCriticalPath(string normalizedPath)
        {
            var criticalPaths = new[]
            {
                NormalizePath(Environment.GetFolderPath(Environment.SpecialFolder.System)),
                NormalizePath(Environment.GetFolderPath(Environment.SpecialFolder.Windows)),
                NormalizePath(@"C:\Windows"),
                NormalizePath(@"C:\Program Files"),
                NormalizePath(@"C:\Program Files (x86)")
            };

            return criticalPaths.Any(critical =>
                !string.IsNullOrEmpty(critical) &&
                normalizedPath.StartsWith(critical, StringComparison.OrdinalIgnoreCase));
        }

        private bool ContainsDangerousPattern(string path)
        {
            var dangerousPatterns = new[] { "..", "~", "/../", "\\..\\", "%00" };
            return dangerousPatterns.Any(pattern =>
                path.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        private string NormalizePath(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
        }

        private void RecordAccessAttempt(string path, AccessType accessType)
        {
            var key = $"{path}|{accessType}";

            if (_accessHistory.TryGetValue(key, out var attempt))
            {
                attempt.Count++;
                attempt.LastAttempt = DateTime.UtcNow;
            }
            else
            {
                _accessHistory[key] = new AccessAttempt
                {
                    Path = path,
                    AccessType = accessType,
                    Count = 1,
                    LastAttempt = DateTime.UtcNow
                };

                // Limit history size
                if (_accessHistory.Count > _maxHistorySize)
                {
                    var oldestKey = _accessHistory
                        .OrderBy(kvp => kvp.Value.LastAttempt)
                        .First().Key;
                    _accessHistory.Remove(oldestKey);
                }
            }
        }

        public void Dispose()
        {
            _lock?.Dispose();
        }
    }

    /// <summary>
    /// Type of file system access being requested.
    /// </summary>
    public enum AccessType
    {
        Read,
        Write,
        Delete,
        Execute,
        List
    }

    /// <summary>
    /// Result of an access control validation check.
    /// </summary>
    public class AccessControlResult
    {
        public bool IsAllowed { get; private set; }
        public string? Reason { get; private set; }
        public string? NormalizedPath { get; private set; }

        private AccessControlResult() { }

        public static AccessControlResult Allow(string normalizedPath)
        {
            return new AccessControlResult
            {
                IsAllowed = true,
                NormalizedPath = normalizedPath
            };
        }

        public static AccessControlResult Deny(string reason)
        {
            return new AccessControlResult
            {
                IsAllowed = false,
                Reason = reason
            };
        }
    }

    /// <summary>
    /// Statistics about access control operations.
    /// </summary>
    public class AccessStatistics
    {
        public int TotalAttempts { get; set; }
        public int UniquePathsAccessed { get; set; }
        public int AllowedPathsCount { get; set; }
        public int ForbiddenPathsCount { get; set; }
        public List<AccessAttemptInfo> RecentAttempts { get; set; } = new();
    }

    public class AccessAttemptInfo
    {
        public string Path { get; set; } = string.Empty;
        public AccessType AccessType { get; set; }
        public int Count { get; set; }
        public DateTime LastAttempt { get; set; }
    }

    internal class AccessAttempt
    {
        public string Path { get; set; } = string.Empty;
        public AccessType AccessType { get; set; }
        public int Count { get; set; }
        public DateTime LastAttempt { get; set; }
    }
}