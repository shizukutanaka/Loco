namespace Loco.Core.Audit;

/// <summary>
/// Audit logger interface for compliance and security
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Logs an audit event
    /// </summary>
    Task LogEventAsync(AuditEvent auditEvent);

    /// <summary>
    /// Gets audit logs by entity
    /// </summary>
    Task<IEnumerable<AuditEvent>> GetAuditTrailAsync(string entityType, string entityId, int limit = 100);

    /// <summary>
    /// Gets audit logs by user
    /// </summary>
    Task<IEnumerable<AuditEvent>> GetUserAuditTrailAsync(string userId, int limit = 100);

    /// <summary>
    /// Gets audit logs by date range
    /// </summary>
    Task<IEnumerable<AuditEvent>> GetAuditLogsByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets audit statistics
    /// </summary>
    Task<AuditStatistics> GetStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);
}

/// <summary>
/// Audit event
/// </summary>
public class AuditEvent
{
    /// <summary>
    /// Event ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID who performed the action
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User name
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Action type
    /// </summary>
    public AuditActionType Action { get; set; }

    /// <summary>
    /// Entity type
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Entity ID
    /// </summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Old values (before change)
    /// </summary>
    public Dictionary<string, object?> OldValues { get; set; } = new();

    /// <summary>
    /// New values (after change)
    /// </summary>
    public Dictionary<string, object?> NewValues { get; set; } = new();

    /// <summary>
    /// IP address
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// User agent
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Correlation ID for tracing
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = new();

    /// <summary>
    /// Success flag
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Data classification (for compliance)
    /// </summary>
    public DataClassification Classification { get; set; } = DataClassification.Internal;
}

/// <summary>
/// Audit action types
/// </summary>
public enum AuditActionType
{
    Create,
    Read,
    Update,
    Delete,
    Approve,
    Reject,
    Publish,
    Export,
    Import,
    Execute,
    Cancel,
    Login,
    Logout,
    AccessDenied,
    PermissionChange,
    SettingChange,
    Other
}

/// <summary>
/// Data classification levels (GDPR/HIPAA/SOC2)
/// </summary>
public enum DataClassification
{
    Public,
    Internal,
    Confidential,
    Restricted,
    PII,
    PHI,
    Financial
}

/// <summary>
/// Audit statistics
/// </summary>
public class AuditStatistics
{
    /// <summary>
    /// Total audit events
    /// </summary>
    public long TotalEvents { get; set; }

    /// <summary>
    /// Events by action type
    /// </summary>
    public Dictionary<string, long> EventsByActionType { get; set; } = new();

    /// <summary>
    /// Events by entity type
    /// </summary>
    public Dictionary<string, long> EventsByEntityType { get; set; } = new();

    /// <summary>
    /// Top users by action count
    /// </summary>
    public List<(string UserId, long Count)> TopUsers { get; set; } = new();

    /// <summary>
    /// Failed operations count
    /// </summary>
    public long FailedOperations { get; set; }

    /// <summary>
    /// Success rate percentage
    /// </summary>
    public double SuccessRate => TotalEvents > 0 ? ((TotalEvents - FailedOperations) / (double)TotalEvents) * 100 : 0;

    /// <summary>
    /// Period start date
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Period end date
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Data retention policy (days)
    /// </summary>
    public int RetentionDays { get; set; } = 90;
}

/// <summary>
/// Audit context for capturing request information
/// </summary>
public class AuditContext
{
    /// <summary>
    /// User ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User name
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// IP address
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// User agent
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Correlation ID
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = new();
}

/// <summary>
/// Extension methods for audit logging
/// </summary>
public static class AuditExtensions
{
    /// <summary>
    /// Gets changed values between old and new objects
    /// </summary>
    public static (Dictionary<string, object?> OldValues, Dictionary<string, object?> NewValues) GetChangedValues<T>(
        T oldValue,
        T newValue) where T : class
    {
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        if (oldValue == null || newValue == null)
            return (oldValues, newValues);

        var properties = typeof(T).GetProperties();
        foreach (var property in properties)
        {
            var oldPropertyValue = property.GetValue(oldValue);
            var newPropertyValue = property.GetValue(newValue);

            if (!Equals(oldPropertyValue, newPropertyValue))
            {
                oldValues[property.Name] = oldPropertyValue;
                newValues[property.Name] = newPropertyValue;
            }
        }

        return (oldValues, newValues);
    }
}
