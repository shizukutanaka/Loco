// Phase 7: Usage-Based Billing & Rate Limiting
// Comprehensive billing calculation, usage tracking, and rate limiting
// Enables usage-based SaaS pricing models with per-tenant resource controls

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Billing;

/// <summary>
/// Usage metric type
/// </summary>
public enum UsageMetricType
{
    WorkflowExecutions = 0,
    ApiCalls = 1,
    DataProcessed = 2,
    StorageUsed = 3,
    IntegrationCalls = 4,
    ComputeTime = 5,
}

/// <summary>
/// Billing cycle
/// </summary>
public enum BillingCycle
{
    Monthly = 0,
    Quarterly = 1,
    Annually = 2,
}

/// <summary>
/// Usage record
/// </summary>
public class UsageRecord
{
    public string RecordId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public UsageMetricType MetricType { get; set; }
    public double Amount { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Billing invoice
/// </summary>
public class BillingInvoice
{
    public string InvoiceId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public BillingCycle BillingCycle { get; set; }

    // Charges
    public Dictionary<string, double> UsageCharges { get; set; } = new();
    public double BaseCharge { get; set; } // Plan base cost
    public double OverageCharge { get; set; }
    public double DiscountAmount { get; set; }
    public double TaxAmount { get; set; }
    public double TotalAmount { get; set; }

    // Status
    public string Status { get; set; } = "draft"; // draft, sent, paid, overdue
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueAt { get; set; }
    public DateTime? PaidAt { get; set; }

    // Payment
    public string? PaymentMethod { get; set; }
    public string? TransactionId { get; set; }
}

/// <summary>
/// Rate limit configuration
/// </summary>
public class RateLimitConfig
{
    public string ConfigId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;

    // Execution limits
    public int ExecutionsPerSecond { get; set; } = 100;
    public int ExecutionsPerMinute { get; set; } = 6000;
    public int ExecutionsPerHour { get; set; } = 360000;
    public int ExecutionsPerDay { get; set; } = 10000000;

    // API limits
    public int ApiCallsPerSecond { get; set; } = 1000;
    public int ApiCallsPerMinute { get; set; } = 60000;

    // Data limits
    public long DataTransferPerMinuteMb { get; set; } = 100;
    public long DataTransferPerHourMb { get; set; } = 6000;

    // Concurrent limits
    public int MaxConcurrentExecutions { get; set; } = 100;
    public int MaxConcurrentApiCalls { get; set; } = 1000;

    // Throttling behavior
    public string OnExceed { get; set; } = "throttle"; // throttle, reject, queue
    public bool HasBurst { get; set; } = true;
    public int BurstMultiplier { get; set; } = 2;
}

/// <summary>
/// Rate limit status
/// </summary>
public class RateLimitStatus
{
    public string TenantId { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;

    // Current usage
    public int CurrentPerSecond { get; set; }
    public int CurrentPerMinute { get; set; }
    public int CurrentPerHour { get; set; }
    public int CurrentConcurrent { get; set; }

    // Limits
    public int LimitPerSecond { get; set; }
    public int LimitPerMinute { get; set; }
    public int LimitPerHour { get; set; }
    public int LimitConcurrent { get; set; }

    // Status
    public bool IsRateLimited { get; set; }
    public int SecondsUntilReset { get; set; }
    public double PercentageOfLimit { get; set; }
}

/// <summary>
/// Billing and rate limiting interface
/// </summary>
public interface IBillingAndRateLimiting
{
    // Usage Tracking
    Task RecordUsageAsync(
        string tenantId,
        UsageMetricType metricType,
        double amount,
        string unit,
        CancellationToken ct = default);

    Task<List<UsageRecord>> GetUsageAsync(
        string tenantId,
        UsageMetricType? metricType = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);

    Task<Dictionary<string, double>> GetCurrentMonthUsageAsync(
        string tenantId,
        CancellationToken ct = default);

    // Billing
    Task<BillingInvoice> GenerateInvoiceAsync(
        string tenantId,
        DateTime periodStart,
        DateTime periodEnd,
        BillingCycle cycle,
        CancellationToken ct = default);

    Task<BillingInvoice?> GetInvoiceAsync(
        string invoiceId,
        CancellationToken ct = default);

    Task<List<BillingInvoice>> GetInvoicesAsync(
        string tenantId,
        int limit = 20,
        CancellationToken ct = default);

    Task<bool> MarkInvoicePaidAsync(
        string invoiceId,
        string transactionId,
        CancellationToken ct = default);

    // Rate Limiting
    Task<RateLimitConfig> SetRateLimitAsync(
        string tenantId,
        RateLimitConfig config,
        CancellationToken ct = default);

    Task<RateLimitConfig?> GetRateLimitAsync(
        string tenantId,
        CancellationToken ct = default);

    Task<RateLimitStatus> CheckRateLimitAsync(
        string tenantId,
        string resource,
        int requestCount = 1,
        CancellationToken ct = default);

    Task<bool> ConsumeRateLimitAsync(
        string tenantId,
        string resource,
        int amount = 1,
        CancellationToken ct = default);

    // Cost estimation
    Task<double> EstimateMonthlyChargeAsync(
        string tenantId,
        CancellationToken ct = default);

    Task<Dictionary<string, double>> GetCostBreakdownAsync(
        string tenantId,
        CancellationToken ct = default);
}

/// <summary>
/// Billing and rate limiting implementation
/// </summary>
public class BillingAndRateLimiting : IBillingAndRateLimiting
{
    private readonly ILogger<BillingAndRateLimiting> _logger;
    private readonly Dictionary<string, UsageRecord> _usageRecords;
    private readonly Dictionary<string, BillingInvoice> _invoices;
    private readonly Dictionary<string, RateLimitConfig> _rateLimits;
    private readonly Dictionary<string, Dictionary<string, (int Count, DateTime ResetTime)>> _currentUsage;

    public BillingAndRateLimiting(ILogger<BillingAndRateLimiting> logger)
    {
        _logger = logger;
        _usageRecords = new Dictionary<string, UsageRecord>();
        _invoices = new Dictionary<string, BillingInvoice>();
        _rateLimits = new Dictionary<string, RateLimitConfig>();
        _currentUsage = new Dictionary<string, Dictionary<string, (int Count, DateTime ResetTime)>>();
    }

    // Usage Tracking
    public async Task RecordUsageAsync(
        string tenantId,
        UsageMetricType metricType,
        double amount,
        string unit,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var record = new UsageRecord
        {
            TenantId = tenantId,
            MetricType = metricType,
            Amount = amount,
            Unit = unit,
            RecordedAt = DateTime.UtcNow,
        };

        _usageRecords[record.RecordId] = record;

        _logger.LogDebug(
            "Usage recorded: {TenantId}, {MetricType}, {Amount} {Unit}",
            tenantId, metricType, amount, unit);
    }

    public async Task<List<UsageRecord>> GetUsageAsync(
        string tenantId,
        UsageMetricType? metricType = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var results = _usageRecords.Values
            .Where(r => r.TenantId == tenantId)
            .Where(r => metricType == null || r.MetricType == metricType)
            .Where(r => from == null || r.RecordedAt >= from)
            .Where(r => to == null || r.RecordedAt <= to)
            .OrderByDescending(r => r.RecordedAt)
            .ToList();

        return results;
    }

    public async Task<Dictionary<string, double>> GetCurrentMonthUsageAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var usage = await GetUsageAsync(tenantId, from: monthStart, ct: ct);

        return usage
            .GroupBy(r => r.MetricType.ToString())
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
    }

    // Billing
    public async Task<BillingInvoice> GenerateInvoiceAsync(
        string tenantId,
        DateTime periodStart,
        DateTime periodEnd,
        BillingCycle cycle,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate calculation

        var usage = await GetUsageAsync(tenantId, from: periodStart, to: periodEnd, ct: ct);

        var invoice = new BillingInvoice
        {
            TenantId = tenantId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            BillingCycle = cycle,
            BaseCharge = 99.00, // Base plan charge
            IssuedAt = DateTime.UtcNow,
            DueAt = DateTime.UtcNow.AddDays(30),
        };

        // Calculate usage charges
        foreach (var metric in usage.GroupBy(u => u.MetricType))
        {
            var total = metric.Sum(u => u.Amount);
            var charge = CalculateChargeForMetric(metric.Key, total);
            invoice.UsageCharges[metric.Key.ToString()] = charge;
            invoice.OverageCharge += charge;
        }

        invoice.TotalAmount = invoice.BaseCharge + invoice.OverageCharge - invoice.DiscountAmount + invoice.TaxAmount;

        _invoices[invoice.InvoiceId] = invoice;

        _logger.LogInformation(
            "Invoice generated: {InvoiceId}, Tenant: {TenantId}, Amount: ${Amount:F2}",
            invoice.InvoiceId, tenantId, invoice.TotalAmount);

        return invoice;
    }

    public async Task<BillingInvoice?> GetInvoiceAsync(
        string invoiceId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _invoices.TryGetValue(invoiceId, out var invoice);
        return invoice;
    }

    public async Task<List<BillingInvoice>> GetInvoicesAsync(
        string tenantId,
        int limit = 20,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return _invoices.Values
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.IssuedAt)
            .Take(limit)
            .ToList();
    }

    public async Task<bool> MarkInvoicePaidAsync(
        string invoiceId,
        string transactionId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_invoices.TryGetValue(invoiceId, out var invoice))
        {
            return false;
        }

        invoice.Status = "paid";
        invoice.PaidAt = DateTime.UtcNow;
        invoice.TransactionId = transactionId;

        _logger.LogInformation(
            "Invoice marked paid: {InvoiceId}, Transaction: {TransactionId}",
            invoiceId, transactionId);

        return true;
    }

    // Rate Limiting
    public async Task<RateLimitConfig> SetRateLimitAsync(
        string tenantId,
        RateLimitConfig config,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        config.TenantId = tenantId;
        _rateLimits[tenantId] = config;

        _logger.LogInformation(
            "Rate limit configured: {TenantId}, ExecutionsPerSecond: {Rate}",
            tenantId, config.ExecutionsPerSecond);

        return config;
    }

    public async Task<RateLimitConfig?> GetRateLimitAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _rateLimits.TryGetValue(tenantId, out var config);
        return config;
    }

    public async Task<RateLimitStatus> CheckRateLimitAsync(
        string tenantId,
        string resource,
        int requestCount = 1,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var config = await GetRateLimitAsync(tenantId, ct);
        if (config == null)
        {
            return new RateLimitStatus { TenantId = tenantId, Resource = resource };
        }

        var key = $"{tenantId}:{resource}";
        if (!_currentUsage.ContainsKey(key))
        {
            _currentUsage[key] = new Dictionary<string, (int, DateTime)>();
        }

        var usage = _currentUsage[key];
        var now = DateTime.UtcNow;
        var secondKey = $"sec_{now:yyyy-MM-dd-HH-mm-ss}";
        var minuteKey = $"min_{now:yyyy-MM-dd-HH-mm}";

        var currentPerSecond = usage.TryGetValue(secondKey, out var secUsage) ? secUsage.Count : 0;
        var currentPerMinute = usage.Values.Count(u => u.ResetTime > now.AddMinutes(-1));

        var status = new RateLimitStatus
        {
            TenantId = tenantId,
            Resource = resource,
            CurrentPerSecond = currentPerSecond,
            CurrentPerMinute = currentPerMinute,
            LimitPerSecond = config.ExecutionsPerSecond,
            LimitPerMinute = config.ExecutionsPerMinute,
            PercentageOfLimit = (currentPerSecond / (double)config.ExecutionsPerSecond) * 100,
            IsRateLimited = currentPerSecond >= config.ExecutionsPerSecond,
        };

        return status;
    }

    public async Task<bool> ConsumeRateLimitAsync(
        string tenantId,
        string resource,
        int amount = 1,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var status = await CheckRateLimitAsync(tenantId, resource, amount, ct);

        if (status.IsRateLimited)
        {
            _logger.LogWarning(
                "Rate limit exceeded: {TenantId}, {Resource}",
                tenantId, resource);
            return false;
        }

        return true;
    }

    // Cost estimation
    public async Task<double> EstimateMonthlyChargeAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var usage = await GetUsageAsync(tenantId, from: monthStart, ct: ct);

        var baseCharge = 99.00;
        var usageCharge = usage
            .GroupBy(u => u.MetricType)
            .Sum(g => CalculateChargeForMetric(g.Key, g.Sum(u => u.Amount)));

        return baseCharge + usageCharge;
    }

    public async Task<Dictionary<string, double>> GetCostBreakdownAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var usage = await GetUsageAsync(tenantId, from: monthStart, ct: ct);

        var breakdown = new Dictionary<string, double>
        {
            ["base_charge"] = 99.00,
        };

        foreach (var metric in usage.GroupBy(u => u.MetricType))
        {
            var charge = CalculateChargeForMetric(metric.Key, metric.Sum(u => u.Amount));
            breakdown[metric.Key.ToString()] = charge;
        }

        return breakdown;
    }

    private double CalculateChargeForMetric(UsageMetricType metricType, double amount)
    {
        return metricType switch
        {
            UsageMetricType.WorkflowExecutions => amount * 0.001, // $0.001 per execution
            UsageMetricType.ApiCalls => amount * 0.0001, // $0.0001 per API call
            UsageMetricType.DataProcessed => amount * 0.00001, // $0.00001 per GB
            UsageMetricType.StorageUsed => amount * 0.023, // $0.023 per GB-month
            UsageMetricType.IntegrationCalls => amount * 0.0005, // $0.0005 per call
            UsageMetricType.ComputeTime => amount * 0.00002, // $0.00002 per compute-second
            _ => 0
        };
    }
}
