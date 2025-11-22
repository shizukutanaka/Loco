// Phase 8: Enterprise Support & Diagnostics Dashboard
// Comprehensive system diagnostics, support ticketing, and issue management
// Provides enterprise support teams with visibility into platform health and operations

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Support;

/// <summary>
/// System diagnostic result
/// </summary>
public class SystemDiagnostic
{
    public string DiagnosticId { get; set; } = Guid.NewGuid().ToString();
    public string ComponentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // healthy, warning, critical
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public long DiskUsageBytes { get; set; }
    public int ActiveConnections { get; set; }
    public double? ResponseTimeMs { get; set; }
    public long? LastCheckTimeMs { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public DateTime RunAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Support ticket
/// </summary>
public class SupportTicket
{
    public string TicketId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // performance, error, feature, billing
    public string Priority { get; set; } = string.Empty; // low, medium, high, critical
    public string Status { get; set; } = "open"; // open, in_progress, resolved, closed
    public string AssignedTo { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public List<TicketComment> Comments { get; set; } = new();
    public List<string> AttachedLogs { get; set; } = new();
    public int ResolutionTimeMinutes { get; set; }
}

/// <summary>
/// Ticket comment
/// </summary>
public class TicketComment
{
    public string CommentId { get; set; } = Guid.NewGuid().ToString();
    public string Author { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsInternal { get; set; }
}

/// <summary>
/// Diagnostic log entry
/// </summary>
public class DiagnosticLogEntry
{
    public string LogId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public string LogLevel { get; set; } = string.Empty; // debug, info, warning, error, critical
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string>? ContextData { get; set; }
    public string? StackTrace { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Performance profile
/// </summary>
public class PerformanceProfile
{
    public string ProfileId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public DateTime ProfileDate { get; set; }
    public double AverageCpuPercent { get; set; }
    public double PeakCpuPercent { get; set; }
    public double AverageMemoryPercent { get; set; }
    public double PeakMemoryPercent { get; set; }
    public long ExecutedWorkflows { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public double P95ExecutionTimeMs { get; set; }
    public double P99ExecutionTimeMs { get; set; }
    public int FailedExecutions { get; set; }
    public int ErrorCount { get; set; }
    public double SuccessRatePercent { get; set; }
}

/// <summary>
/// Issue detection
/// </summary>
public class DetectedIssue
{
    public string IssueId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty; // performance, reliability, security, resource
    public string Severity { get; set; } = string.Empty; // low, medium, high, critical
    public string Description { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public List<string>? AffectedResources { get; set; }
    public bool IsResolved { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>
/// Support dashboard data
/// </summary>
public class SupportDashboardView
{
    public string TenantId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // System Health
    public int HealthyComponents { get; set; }
    public int WarningComponents { get; set; }
    public int CriticalComponents { get; set; }
    public List<SystemDiagnostic> ComponentStatus { get; set; } = new();

    // Support Tickets
    public int OpenTickets { get; set; }
    public int TicketsInProgress { get; set; }
    public double AverageResolutionTimeMinutes { get; set; }
    public List<SupportTicket> RecentTickets { get; set; } = new();

    // Issues
    public int ActiveIssues { get; set; }
    public int CriticalIssues { get; set; }
    public List<DetectedIssue> RecentIssues { get; set; } = new();

    // Performance
    public double CurrentCpuPercent { get; set; }
    public double CurrentMemoryPercent { get; set; }
    public long DiskUsageBytes { get; set; }
    public PerformanceProfile? LatestProfile { get; set; }
}

/// <summary>
/// Support dashboard interface
/// </summary>
public interface IEnterpriseSupportDashboard
{
    // Diagnostics
    Task<SystemDiagnostic> RunDiagnosticsAsync(
        string componentName,
        CancellationToken ct = default);

    Task<List<SystemDiagnostic>> GetAllDiagnosticsAsync(
        CancellationToken ct = default);

    Task<List<DiagnosticLogEntry>> GetLogsAsync(
        string? tenantId = null,
        string? logLevel = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);

    // Support Tickets
    Task<SupportTicket> CreateTicketAsync(
        string tenantId,
        SupportTicket ticket,
        CancellationToken ct = default);

    Task<SupportTicket?> GetTicketAsync(
        string ticketId,
        CancellationToken ct = default);

    Task<List<SupportTicket>> GetTicketsAsync(
        string tenantId,
        string? status = null,
        CancellationToken ct = default);

    Task<bool> UpdateTicketAsync(
        string ticketId,
        SupportTicket ticket,
        CancellationToken ct = default);

    Task<bool> AddCommentAsync(
        string ticketId,
        TicketComment comment,
        CancellationToken ct = default);

    // Performance Analysis
    Task<PerformanceProfile> RecordPerformanceAsync(
        string tenantId,
        PerformanceProfile profile,
        CancellationToken ct = default);

    Task<PerformanceProfile?> GetLatestProfileAsync(
        string tenantId,
        CancellationToken ct = default);

    Task<List<PerformanceProfile>> GetProfileHistoryAsync(
        string tenantId,
        int days = 30,
        CancellationToken ct = default);

    // Issue Detection
    Task<DetectedIssue> ReportIssueAsync(
        string tenantId,
        DetectedIssue issue,
        CancellationToken ct = default);

    Task<List<DetectedIssue>> GetActiveIssuesAsync(
        string tenantId,
        CancellationToken ct = default);

    Task<bool> ResolveIssueAsync(
        string issueId,
        CancellationToken ct = default);

    // Dashboard
    Task<SupportDashboardView> GetDashboardAsync(
        string tenantId,
        CancellationToken ct = default);

    // Analytics
    Task<Dictionary<string, object>> GetSupportAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default);
}

/// <summary>
/// Enterprise support dashboard implementation
/// </summary>
public class EnterpriseSupportDashboard : IEnterpriseSupportDashboard
{
    private readonly ILogger<EnterpriseSupportDashboard> _logger;
    private readonly Dictionary<string, SystemDiagnostic> _diagnostics;
    private readonly Dictionary<string, SupportTicket> _tickets;
    private readonly Dictionary<string, DiagnosticLogEntry> _logs;
    private readonly Dictionary<string, PerformanceProfile> _profiles;
    private readonly Dictionary<string, List<DetectedIssue>> _issues;

    public EnterpriseSupportDashboard(ILogger<EnterpriseSupportDashboard> logger)
    {
        _logger = logger;
        _diagnostics = new Dictionary<string, SystemDiagnostic>();
        _tickets = new Dictionary<string, SupportTicket>();
        _logs = new Dictionary<string, DiagnosticLogEntry>();
        _profiles = new Dictionary<string, PerformanceProfile>();
        _issues = new Dictionary<string, List<DetectedIssue>>();
    }

    // Diagnostics
    public async Task<SystemDiagnostic> RunDiagnosticsAsync(
        string componentName,
        CancellationToken ct = default)
    {
        await Task.Delay(200, ct); // Simulate diagnostics

        var diagnostic = new SystemDiagnostic
        {
            ComponentName = componentName,
            CpuUsagePercent = 45.5,
            MemoryUsagePercent = 62.3,
            DiskUsageBytes = 512000000000,
            ActiveConnections = 250,
            ResponseTimeMs = 45.2,
            Status = "healthy",
        };

        // Add warnings if threshold exceeded
        if (diagnostic.MemoryUsagePercent > 80)
        {
            diagnostic.Status = "warning";
            diagnostic.Warnings.Add("Memory usage approaching limit");
        }

        if (diagnostic.CpuUsagePercent > 90)
        {
            diagnostic.Status = "critical";
            diagnostic.Errors.Add("CPU usage critical");
        }

        _diagnostics[componentName] = diagnostic;

        _logger.LogInformation(
            "Diagnostics run for {ComponentName}: Status={Status}, CPU={CpuPercent}%, Memory={MemoryPercent}%",
            componentName, diagnostic.Status, diagnostic.CpuUsagePercent, diagnostic.MemoryUsagePercent);

        return diagnostic;
    }

    public async Task<List<SystemDiagnostic>> GetAllDiagnosticsAsync(
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return _diagnostics.Values
            .OrderByDescending(d => d.RunAt)
            .ToList();
    }

    public async Task<List<DiagnosticLogEntry>> GetLogsAsync(
        string? tenantId = null,
        string? logLevel = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var results = _logs.Values
            .Where(l => tenantId == null || l.TenantId == tenantId)
            .Where(l => logLevel == null || l.LogLevel == logLevel)
            .Where(l => from == null || l.Timestamp >= from)
            .Where(l => to == null || l.Timestamp <= to)
            .OrderByDescending(l => l.Timestamp)
            .ToList();

        return results;
    }

    // Support Tickets
    public async Task<SupportTicket> CreateTicketAsync(
        string tenantId,
        SupportTicket ticket,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        ticket.TenantId = tenantId;
        _tickets[ticket.TicketId] = ticket;

        _logger.LogInformation(
            "Support ticket created: {TicketId}, Tenant: {TenantId}, Priority: {Priority}",
            ticket.TicketId, tenantId, ticket.Priority);

        return ticket;
    }

    public async Task<SupportTicket?> GetTicketAsync(
        string ticketId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _tickets.TryGetValue(ticketId, out var ticket);
        return ticket;
    }

    public async Task<List<SupportTicket>> GetTicketsAsync(
        string tenantId,
        string? status = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var results = _tickets.Values
            .Where(t => t.TenantId == tenantId)
            .Where(t => status == null || t.Status == status)
            .OrderByDescending(t => t.CreatedAt)
            .ToList();

        return results;
    }

    public async Task<bool> UpdateTicketAsync(
        string ticketId,
        SupportTicket ticket,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_tickets.TryGetValue(ticketId, out var existing))
        {
            return false;
        }

        ticket.TicketId = ticketId;
        if (ticket.Status == "resolved" && existing.Status != "resolved")
        {
            ticket.ResolvedAt = DateTime.UtcNow;
            ticket.ResolutionTimeMinutes = (int)(ticket.ResolvedAt.Value - ticket.CreatedAt).TotalMinutes;
        }

        _tickets[ticketId] = ticket;

        _logger.LogInformation(
            "Support ticket updated: {TicketId}, Status: {Status}",
            ticketId, ticket.Status);

        return true;
    }

    public async Task<bool> AddCommentAsync(
        string ticketId,
        TicketComment comment,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_tickets.TryGetValue(ticketId, out var ticket))
        {
            return false;
        }

        ticket.Comments.Add(comment);

        _logger.LogInformation(
            "Comment added to ticket: {TicketId}, Author: {Author}",
            ticketId, comment.Author);

        return true;
    }

    // Performance Analysis
    public async Task<PerformanceProfile> RecordPerformanceAsync(
        string tenantId,
        PerformanceProfile profile,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        profile.TenantId = tenantId;
        var profileKey = $"{tenantId}_{profile.ProfileDate:yyyyMMdd}";
        _profiles[profileKey] = profile;

        _logger.LogInformation(
            "Performance profile recorded: Tenant={TenantId}, CPU={AvgCpu}%, Memory={AvgMemory}%, Success={SuccessRate}%",
            tenantId, profile.AverageCpuPercent, profile.AverageMemoryPercent, profile.SuccessRatePercent);

        return profile;
    }

    public async Task<PerformanceProfile?> GetLatestProfileAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var profiles = _profiles.Values
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.ProfileDate)
            .FirstOrDefault();

        return profiles;
    }

    public async Task<List<PerformanceProfile>> GetProfileHistoryAsync(
        string tenantId,
        int days = 30,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var startDate = DateTime.UtcNow.AddDays(-days);
        return _profiles.Values
            .Where(p => p.TenantId == tenantId)
            .Where(p => p.ProfileDate >= startDate)
            .OrderByDescending(p => p.ProfileDate)
            .ToList();
    }

    // Issue Detection
    public async Task<DetectedIssue> ReportIssueAsync(
        string tenantId,
        DetectedIssue issue,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        issue.TenantId = tenantId;
        if (!_issues.ContainsKey(tenantId))
        {
            _issues[tenantId] = new List<DetectedIssue>();
        }

        _issues[tenantId].Add(issue);

        _logger.LogWarning(
            "Issue detected: {IssueId}, Tenant: {TenantId}, Type: {IssueType}, Severity: {Severity}",
            issue.IssueId, tenantId, issue.IssueType, issue.Severity);

        return issue;
    }

    public async Task<List<DetectedIssue>> GetActiveIssuesAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_issues.TryGetValue(tenantId, out var issues))
        {
            return new List<DetectedIssue>();
        }

        return issues
            .Where(i => !i.IsResolved)
            .OrderByDescending(i => i.DetectedAt)
            .ToList();
    }

    public async Task<bool> ResolveIssueAsync(
        string issueId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        foreach (var issues in _issues.Values)
        {
            var issue = issues.FirstOrDefault(i => i.IssueId == issueId);
            if (issue != null)
            {
                issue.IsResolved = true;
                issue.ResolvedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Issue resolved: {IssueId}",
                    issueId);

                return true;
            }
        }

        return false;
    }

    // Dashboard
    public async Task<SupportDashboardView> GetDashboardAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate aggregation

        var tickets = await GetTicketsAsync(tenantId, ct: ct);
        var activeIssues = await GetActiveIssuesAsync(tenantId, ct: ct);
        var diagnostics = await GetAllDiagnosticsAsync(ct: ct);

        var dashboard = new SupportDashboardView
        {
            TenantId = tenantId,
            GeneratedAt = DateTime.UtcNow,
            OpenTickets = tickets.Count(t => t.Status == "open"),
            TicketsInProgress = tickets.Count(t => t.Status == "in_progress"),
            AverageResolutionTimeMinutes = tickets
                .Where(t => t.ResolutionTimeMinutes > 0)
                .Average(t => (double)t.ResolutionTimeMinutes),
            RecentTickets = tickets.Take(5).ToList(),
            ActiveIssues = activeIssues.Count,
            CriticalIssues = activeIssues.Count(i => i.Severity == "critical"),
            RecentIssues = activeIssues.Take(5).ToList(),
            ComponentStatus = diagnostics.Take(10).ToList(),
            HealthyComponents = diagnostics.Count(d => d.Status == "healthy"),
            WarningComponents = diagnostics.Count(d => d.Status == "warning"),
            CriticalComponents = diagnostics.Count(d => d.Status == "critical"),
            CurrentCpuPercent = diagnostics.FirstOrDefault()?.CpuUsagePercent ?? 0,
            CurrentMemoryPercent = diagnostics.FirstOrDefault()?.MemoryUsagePercent ?? 0,
        };

        return dashboard;
    }

    // Analytics
    public async Task<Dictionary<string, object>> GetSupportAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var tickets = await GetTicketsAsync(tenantId, ct: ct);
        var activeIssues = await GetActiveIssuesAsync(tenantId, ct: ct);

        var resolvedTickets = tickets.Where(t => t.Status == "resolved").ToList();
        var avgResolutionTime = resolvedTickets.Count > 0
            ? resolvedTickets.Average(t => t.ResolutionTimeMinutes)
            : 0;

        return new Dictionary<string, object>
        {
            ["total_tickets"] = tickets.Count,
            ["open_tickets"] = tickets.Count(t => t.Status == "open"),
            ["in_progress_tickets"] = tickets.Count(t => t.Status == "in_progress"),
            ["resolved_tickets"] = tickets.Count(t => t.Status == "resolved"),
            ["average_resolution_minutes"] = avgResolutionTime,
            ["active_issues"] = activeIssues.Count,
            ["critical_issues"] = activeIssues.Count(i => i.Severity == "critical"),
            ["high_priority_issues"] = activeIssues.Count(i => i.Severity == "high"),
        };
    }
}
