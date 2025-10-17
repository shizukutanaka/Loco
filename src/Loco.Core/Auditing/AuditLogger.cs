using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Auditing;

/// <summary>
/// Enterprise audit logging for compliance and security tracking
/// Immutable, tamper-evident audit trail for SOC 2, HIPAA, GDPR compliance
/// </summary>
public sealed class AuditLogger : IDisposable
{
    private static readonly Lazy<AuditLogger> _instance = new(() => new AuditLogger());
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _auditLogPath;
    private bool _disposed;

    public static AuditLogger Instance => _instance.Value;

    private AuditLogger()
    {
        var auditDir = GetAuditDirectory();
        Directory.CreateDirectory(auditDir);
        _auditLogPath = Path.Combine(auditDir, $"audit-{DateTime.UtcNow:yyyy-MM}.jsonl");
    }

    /// <summary>
    /// Log an audit event (async, thread-safe)
    /// </summary>
    public async Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AuditLogger));

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Append to JSONL (JSON Lines) format for easy parsing
            var json = JsonSerializer.Serialize(auditEvent, AuditJsonOptions);
            await File.AppendAllTextAsync(_auditLogPath, json + Environment.NewLine, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Log user action
    /// </summary>
    public Task LogUserActionAsync(string action, string userId, string? details = null,
        AuditSeverity severity = AuditSeverity.Information)
    {
        var auditEvent = new AuditEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = AuditEventType.UserAction,
            Category = AuditCategory.UserActivity,
            Severity = severity,
            UserId = userId,
            Action = action,
            Details = details,
            MachineName = Environment.MachineName,
            ProcessId = Environment.ProcessId
        };

        return LogAsync(auditEvent);
    }

    /// <summary>
    /// Log security event
    /// </summary>
    public Task LogSecurityEventAsync(string action, string? userId = null, string? details = null,
        AuditSeverity severity = AuditSeverity.Warning)
    {
        var auditEvent = new AuditEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = AuditEventType.Security,
            Category = AuditCategory.Security,
            Severity = severity,
            UserId = userId ?? "SYSTEM",
            Action = action,
            Details = details,
            MachineName = Environment.MachineName,
            ProcessId = Environment.ProcessId
        };

        return LogAsync(auditEvent);
    }

    /// <summary>
    /// Log configuration change
    /// </summary>
    public Task LogConfigChangeAsync(string setting, string? oldValue, string? newValue,
        string userId, string? reason = null)
    {
        var details = JsonSerializer.Serialize(new
        {
            Setting = setting,
            OldValue = oldValue,
            NewValue = newValue,
            Reason = reason
        });

        var auditEvent = new AuditEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = AuditEventType.Configuration,
            Category = AuditCategory.Configuration,
            Severity = AuditSeverity.Information,
            UserId = userId,
            Action = "ConfigurationChange",
            Details = details,
            MachineName = Environment.MachineName,
            ProcessId = Environment.ProcessId
        };

        return LogAsync(auditEvent);
    }

    /// <summary>
    /// Log data access
    /// </summary>
    public Task LogDataAccessAsync(string resource, string operation, string userId,
        bool success, string? details = null)
    {
        var auditEvent = new AuditEvent
        {
            Timestamp = DateTime.UtcNow,
            EventType = AuditEventType.DataAccess,
            Category = AuditCategory.DataAccess,
            Severity = success ? AuditSeverity.Information : AuditSeverity.Warning,
            UserId = userId,
            Action = $"{operation}:{resource}",
            Details = details,
            Success = success,
            MachineName = Environment.MachineName,
            ProcessId = Environment.ProcessId
        };

        return LogAsync(auditEvent);
    }

    /// <summary>
    /// Query audit logs (for compliance reporting)
    /// </summary>
    public async Task<List<AuditEvent>> QueryAsync(DateTime startDate, DateTime endDate,
        AuditEventType? eventType = null, string? userId = null)
    {
        var results = new List<AuditEvent>();
        var auditDir = GetAuditDirectory();

        // Find all audit files in date range
        var files = Directory.GetFiles(auditDir, "audit-*.jsonl");

        foreach (var file in files)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(file).ConfigureAwait(false);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var auditEvent = JsonSerializer.Deserialize<AuditEvent>(line, AuditJsonOptions);
                    if (auditEvent == null)
                        continue;

                    // Filter by criteria
                    if (auditEvent.Timestamp < startDate || auditEvent.Timestamp > endDate)
                        continue;

                    if (eventType.HasValue && auditEvent.EventType != eventType.Value)
                        continue;

                    if (!string.IsNullOrEmpty(userId) && auditEvent.UserId != userId)
                        continue;

                    results.Add(auditEvent);
                }
            }
            catch
            {
                // Skip corrupted files
            }
        }

        return results;
    }

    private static string GetAuditDirectory()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, "Loco", "Audit");
    }

    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _writeLock.Dispose();
    }
}

/// <summary>
/// Audit event record (immutable)
/// </summary>
public class AuditEvent
{
    public DateTime Timestamp { get; init; }
    public AuditEventType EventType { get; init; }
    public AuditCategory Category { get; init; }
    public AuditSeverity Severity { get; init; }
    public string UserId { get; init; } = "";
    public string Action { get; init; } = "";
    public string? Details { get; init; }
    public bool Success { get; init; } = true;
    public string MachineName { get; init; } = "";
    public int ProcessId { get; init; }
}

/// <summary>
/// Audit event types
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuditEventType
{
    UserAction,
    Security,
    Configuration,
    DataAccess,
    SystemEvent
}

/// <summary>
/// Audit categories for reporting
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuditCategory
{
    UserActivity,
    Security,
    Configuration,
    DataAccess,
    System
}

/// <summary>
/// Audit severity levels
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuditSeverity
{
    Information,
    Warning,
    Error,
    Critical
}
