using Microsoft.Extensions.Logging;

namespace Loco.Core.Audit;

/// <summary>
/// In-memory audit logger implementation
/// </summary>
public class InMemoryAuditLogger : IAuditLogger
{
    private readonly List<AuditEvent> _auditEvents = new();
    private readonly object _lock = new();
    private readonly ILogger<InMemoryAuditLogger> _logger;
    private readonly int _maxEvents = 10000; // Maximum events to keep in memory

    public InMemoryAuditLogger(ILogger<InMemoryAuditLogger> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LogEventAsync(AuditEvent auditEvent)
    {
        try
        {
            lock (_lock)
            {
                _auditEvents.Add(auditEvent);

                // Trim old events if we exceed max capacity
                if (_auditEvents.Count > _maxEvents)
                {
                    _auditEvents.RemoveRange(0, _auditEvents.Count - _maxEvents);
                }
            }

            _logger.LogInformation(
                "Audit event logged: Action={Action}, Entity={Entity}/{EntityId}, User={User}",
                auditEvent.Action, auditEvent.EntityType, auditEvent.EntityId, auditEvent.UserId);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging audit event");
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AuditEvent>> GetAuditTrailAsync(string entityType, string entityId, int limit = 100)
    {
        try
        {
            lock (_lock)
            {
                return _auditEvents
                    .Where(e => e.EntityType == entityType && e.EntityId == entityId)
                    .OrderByDescending(e => e.Timestamp)
                    .Take(limit)
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit trail for {EntityType}/{EntityId}", entityType, entityId);
            return Enumerable.Empty<AuditEvent>();
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AuditEvent>> GetUserAuditTrailAsync(string userId, int limit = 100)
    {
        try
        {
            lock (_lock)
            {
                return _auditEvents
                    .Where(e => e.UserId == userId)
                    .OrderByDescending(e => e.Timestamp)
                    .Take(limit)
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit trail for user {UserId}", userId);
            return Enumerable.Empty<AuditEvent>();
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AuditEvent>> GetAuditLogsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            lock (_lock)
            {
                return _auditEvents
                    .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                    .OrderByDescending(e => e.Timestamp)
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs for date range {StartDate}-{EndDate}", startDate, endDate);
            return Enumerable.Empty<AuditEvent>();
        }
    }

    /// <inheritdoc />
    public async Task<AuditStatistics> GetStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            lock (_lock)
            {
                var events = _auditEvents.AsEnumerable();

                if (startDate.HasValue)
                    events = events.Where(e => e.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    events = events.Where(e => e.Timestamp <= endDate.Value);

                var eventList = events.ToList();

                var statistics = new AuditStatistics
                {
                    TotalEvents = eventList.Count,
                    FailedOperations = eventList.Count(e => !e.Success),
                    StartDate = startDate,
                    EndDate = endDate,
                    EventsByActionType = eventList
                        .GroupBy(e => e.Action.ToString())
                        .ToDictionary(g => g.Key, g => (long)g.Count()),
                    EventsByEntityType = eventList
                        .GroupBy(e => e.EntityType)
                        .ToDictionary(g => g.Key, g => (long)g.Count()),
                    TopUsers = eventList
                        .GroupBy(e => e.UserId)
                        .Select(g => (g.Key, (long)g.Count()))
                        .OrderByDescending(x => x.Item2)
                        .Take(10)
                        .ToList()
                };

                return statistics;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating audit statistics");
            return new AuditStatistics();
        }
    }
}

/// <summary>
/// Audit logger middleware for ASP.NET Core
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, IAuditLogger auditLogger, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Store the user information for audit logging
        var userId = context.User?.FindFirst("sub")?.Value ?? "Anonymous";
        var userName = context.User?.Identity?.Name ?? "Unknown";
        var ipAddress = context.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = context.Request.Headers["User-Agent"].ToString();
        var correlationId = context.TraceIdentifier;

        // Store in context for use in services
        context.Items["AuditContext"] = new AuditContext
        {
            UserId = userId,
            UserName = userName,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CorrelationId = correlationId
        };

        try
        {
            await _next(context);

            // Log successful request
            if (IsAuditableAction(context.Request.Method, context.Request.Path))
            {
                await _auditLogger.LogEventAsync(new AuditEvent
                {
                    UserId = userId,
                    UserName = userName,
                    Action = GetAuditActionFromHttpMethod(context.Request.Method),
                    EntityType = ExtractEntityTypeFromPath(context.Request.Path),
                    EntityId = ExtractEntityIdFromPath(context.Request.Path),
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    CorrelationId = correlationId,
                    Success = context.Response.StatusCode < 400,
                    Metadata = new Dictionary<string, object?>
                    {
                        { "Method", context.Request.Method },
                        { "Path", context.Request.Path },
                        { "StatusCode", context.Response.StatusCode }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in audit logging middleware");
            throw;
        }
    }

    private bool IsAuditableAction(string method, string path)
    {
        // Only audit write operations (POST, PUT, DELETE, PATCH)
        return method switch
        {
            "POST" or "PUT" or "DELETE" or "PATCH" => true,
            _ => false
        };
    }

    private AuditActionType GetAuditActionFromHttpMethod(string method)
    {
        return method switch
        {
            "POST" => AuditActionType.Create,
            "PUT" or "PATCH" => AuditActionType.Update,
            "DELETE" => AuditActionType.Delete,
            _ => AuditActionType.Other
        };
    }

    private string ExtractEntityTypeFromPath(string path)
    {
        // Extract entity type from path like /api/v1/workflows/123
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 3)
        {
            return segments[2]; // "workflows"
        }
        return path;
    }

    private string ExtractEntityIdFromPath(string path)
    {
        // Extract ID from path like /api/v1/workflows/123
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 4)
        {
            return segments[3]; // "123"
        }
        return string.Empty;
    }
}

/// <summary>
/// Extension methods for audit logging
/// </summary>
public static class AuditLoggingExtensions
{
    /// <summary>
    /// Adds audit logging to the service collection
    /// </summary>
    public static IServiceCollection AddAuditLogging(this IServiceCollection services)
    {
        services.AddScoped<IAuditLogger, InMemoryAuditLogger>();
        return services;
    }

    /// <summary>
    /// Uses audit logging middleware
    /// </summary>
    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder app)
    {
        app.UseMiddleware<AuditLoggingMiddleware>();
        return app;
    }
}
