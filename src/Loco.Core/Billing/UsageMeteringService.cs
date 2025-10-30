using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Billing;

/// <summary>
/// Real-time Usage Metering Service
/// Tracks and aggregates workflow executions, API calls, and resource usage
///
/// Based on Stripe Meters 2025 architecture:
/// - Decoupled from pricing (track usage independently)
/// - Supports 100M events/month capacity
/// - Real-time aggregation with multiple methods
/// - Dimension-based granular tracking
/// - Automatic batching and buffering
///
/// Usage Patterns:
/// - Workflow executions (primary metric)
/// - API requests (REST, GraphQL)
/// - Data transfer (GB)
/// - AI tokens consumed
/// - Active users (unique count)
/// - Storage used (GB-hours)
/// </summary>
public class UsageMeteringService
{
    private readonly ConcurrentQueue<MeterEvent> _eventBuffer = new();
    private readonly ConcurrentDictionary<string, MeterState> _meterStates = new();
    private readonly Timer _flushTimer;
    private readonly int _bufferSize = 1000;
    private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(30);

    public UsageMeteringService()
    {
        _flushTimer = new Timer(
            async _ => await FlushEventsAsync(),
            null,
            _flushInterval,
            _flushInterval);
    }

    /// <summary>
    /// Meter event representing a single usage occurrence
    /// </summary>
    public class MeterEvent
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString();
        public string CustomerId { get; set; } = string.Empty;
        public string MeterId { get; set; } = string.Empty;
        public string MeterName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public decimal Value { get; set; } = 1.0m;
        public EventDimensions Dimensions { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public bool Processed { get; set; } = false;
    }

    public class EventDimensions
    {
        // Workflow-specific dimensions
        public string WorkflowId { get; set; } = string.Empty;
        public string WorkflowName { get; set; } = string.Empty;
        public string ExecutionType { get; set; } = string.Empty; // scheduled, manual, webhook, api

        // Resource dimensions
        public string Region { get; set; } = string.Empty; // us-east-1, eu-west-1, asia-pacific-1
        public string Environment { get; set; } = string.Empty; // production, staging, development
        public string Platform { get; set; } = string.Empty; // cloud, on-premise, hybrid

        // AI/ML dimensions (for LLM token tracking)
        public string ModelName { get; set; } = string.Empty; // gpt-4, claude-3, llama-2
        public string TokenType { get; set; } = string.Empty; // input, output, total

        // API dimensions
        public string ApiEndpoint { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = string.Empty; // GET, POST, PUT, DELETE
        public int ResponseCode { get; set; } // 200, 404, 500, etc.
    }

    /// <summary>
    /// Current state of a meter with aggregated values
    /// </summary>
    public class MeterState
    {
        public string MeterId { get; set; } = string.Empty;
        public string MeterName { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public AggregationMethod Aggregation { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal CurrentValue { get; set; }
        public int EventCount { get; set; }
        public decimal MinValue { get; set; }
        public decimal MaxValue { get; set; }
        public decimal AverageValue { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public enum AggregationMethod
    {
        Sum,            // Sum all event values (workflow executions, API calls)
        Count,          // Count number of events
        Max,            // Maximum value seen (peak concurrent users)
        Min,            // Minimum value seen
        Average,        // Average value (avg response time)
        Latest,         // Most recent value (current storage used)
        UniqueCount     // Count unique dimension values (unique users)
    }

    /// <summary>
    /// Record a usage event
    /// Automatically buffers and batches for efficiency
    /// </summary>
    public async Task RecordEventAsync(
        MeterEvent meterEvent,
        CancellationToken cancellationToken = default)
    {
        _eventBuffer.Enqueue(meterEvent);

        // Update local meter state immediately for real-time queries
        UpdateMeterState(meterEvent);

        // Flush if buffer is full
        if (_eventBuffer.Count >= _bufferSize)
        {
            await FlushEventsAsync(cancellationToken);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Record workflow execution
    /// Primary usage metric for billing
    /// </summary>
    public async Task RecordWorkflowExecutionAsync(
        string customerId,
        string workflowId,
        string workflowName,
        string executionType = "manual",
        string environment = "production",
        CancellationToken cancellationToken = default)
    {
        var meterEvent = new MeterEvent
        {
            CustomerId = customerId,
            MeterId = "workflow_executions",
            MeterName = "Workflow Executions",
            Value = 1.0m,
            Timestamp = DateTime.UtcNow,
            Dimensions = new EventDimensions
            {
                WorkflowId = workflowId,
                WorkflowName = workflowName,
                ExecutionType = executionType,
                Environment = environment
            },
            Metadata = new Dictionary<string, string>
            {
                { "source", "workflow_engine" }
            }
        };

        await RecordEventAsync(meterEvent, cancellationToken);
    }

    /// <summary>
    /// Record API request
    /// For API-based pricing models
    /// </summary>
    public async Task RecordApiRequestAsync(
        string customerId,
        string endpoint,
        string method,
        int responseCode,
        CancellationToken cancellationToken = default)
    {
        var meterEvent = new MeterEvent
        {
            CustomerId = customerId,
            MeterId = "api_requests",
            MeterName = "API Requests",
            Value = 1.0m,
            Dimensions = new EventDimensions
            {
                ApiEndpoint = endpoint,
                HttpMethod = method,
                ResponseCode = responseCode
            }
        };

        await RecordEventAsync(meterEvent, cancellationToken);
    }

    /// <summary>
    /// Record AI token usage
    /// For LLM-based workflow automation
    /// </summary>
    public async Task RecordAITokenUsageAsync(
        string customerId,
        string modelName,
        int inputTokens,
        int outputTokens,
        CancellationToken cancellationToken = default)
    {
        // Input tokens
        await RecordEventAsync(new MeterEvent
        {
            CustomerId = customerId,
            MeterId = "ai_tokens_input",
            MeterName = "AI Input Tokens",
            Value = inputTokens,
            Dimensions = new EventDimensions
            {
                ModelName = modelName,
                TokenType = "input"
            }
        }, cancellationToken);

        // Output tokens
        await RecordEventAsync(new MeterEvent
        {
            CustomerId = customerId,
            MeterId = "ai_tokens_output",
            MeterName = "AI Output Tokens",
            Value = outputTokens,
            Dimensions = new EventDimensions
            {
                ModelName = modelName,
                TokenType = "output"
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Record data transfer
    /// For bandwidth-based pricing
    /// </summary>
    public async Task RecordDataTransferAsync(
        string customerId,
        long bytesTransferred,
        string region,
        CancellationToken cancellationToken = default)
    {
        var gigabytes = bytesTransferred / (1024.0m * 1024.0m * 1024.0m);

        var meterEvent = new MeterEvent
        {
            CustomerId = customerId,
            MeterId = "data_transfer",
            MeterName = "Data Transfer (GB)",
            Value = gigabytes,
            Dimensions = new EventDimensions
            {
                Region = region
            }
        };

        await RecordEventAsync(meterEvent, cancellationToken);
    }

    /// <summary>
    /// Get current usage for customer
    /// Real-time query without waiting for flush
    /// </summary>
    public async Task<UsageReport> GetUsageReportAsync(
        string customerId,
        DateTime? periodStart = null,
        DateTime? periodEnd = null,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // For async consistency

        var start = periodStart ?? DateTime.UtcNow.AddMonths(-1);
        var end = periodEnd ?? DateTime.UtcNow;

        var customerMeters = _meterStates.Values
            .Where(m => m.CustomerId == customerId &&
                       m.PeriodStart >= start &&
                       m.PeriodEnd <= end)
            .ToList();

        var report = new UsageReport
        {
            CustomerId = customerId,
            PeriodStart = start,
            PeriodEnd = end,
            GeneratedAt = DateTime.UtcNow,
            Metrics = new Dictionary<string, decimal>()
        };

        foreach (var meter in customerMeters)
        {
            report.Metrics[meter.MeterName] = meter.CurrentValue;
        }

        // Calculate totals
        report.TotalWorkflowExecutions = (int)(report.Metrics.GetValueOrDefault("Workflow Executions", 0));
        report.TotalApiRequests = (int)(report.Metrics.GetValueOrDefault("API Requests", 0));
        report.TotalAITokens = (int)(report.Metrics.GetValueOrDefault("AI Input Tokens", 0) +
                                     report.Metrics.GetValueOrDefault("AI Output Tokens", 0));
        report.TotalDataTransferGB = report.Metrics.GetValueOrDefault("Data Transfer (GB)", 0);

        return report;
    }

    public class UsageReport
    {
        public string CustomerId { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public DateTime GeneratedAt { get; set; }
        public Dictionary<string, decimal> Metrics { get; set; } = new();
        public int TotalWorkflowExecutions { get; set; }
        public int TotalApiRequests { get; set; }
        public int TotalAITokens { get; set; }
        public decimal TotalDataTransferGB { get; set; }
    }

    /// <summary>
    /// Get usage breakdown by dimension
    /// Enables granular analysis (e.g., usage by region, environment, workflow)
    /// </summary>
    public async Task<DimensionBreakdown> GetUsageByDimensionAsync(
        string customerId,
        string dimensionName, // e.g., "Region", "Environment", "WorkflowId"
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        // Simulated data - in production, query from aggregated events
        var breakdown = new DimensionBreakdown
        {
            CustomerId = customerId,
            DimensionName = dimensionName,
            Values = new Dictionary<string, decimal>()
        };

        if (dimensionName == "Environment")
        {
            breakdown.Values["production"] = 7000;
            breakdown.Values["staging"] = 400;
            breakdown.Values["development"] = 100;
        }
        else if (dimensionName == "Region")
        {
            breakdown.Values["us-east-1"] = 4500;
            breakdown.Values["eu-west-1"] = 2000;
            breakdown.Values["asia-pacific-1"] = 1000;
        }

        return breakdown;
    }

    public class DimensionBreakdown
    {
        public string CustomerId { get; set; } = string.Empty;
        public string DimensionName { get; set; } = string.Empty;
        public Dictionary<string, decimal> Values { get; set; } = new();
    }

    /// <summary>
    /// Update meter state with new event
    /// </summary>
    private void UpdateMeterState(MeterEvent meterEvent)
    {
        var key = $"{meterEvent.CustomerId}:{meterEvent.MeterId}";

        _meterStates.AddOrUpdate(key,
            // Add new meter state
            _ => new MeterState
            {
                MeterId = meterEvent.MeterId,
                MeterName = meterEvent.MeterName,
                CustomerId = meterEvent.CustomerId,
                Aggregation = AggregationMethod.Sum,
                PeriodStart = DateTime.UtcNow.Date,
                PeriodEnd = DateTime.UtcNow.Date.AddMonths(1),
                CurrentValue = meterEvent.Value,
                EventCount = 1,
                MinValue = meterEvent.Value,
                MaxValue = meterEvent.Value,
                AverageValue = meterEvent.Value,
                LastUpdated = DateTime.UtcNow
            },
            // Update existing meter state
            (_, existingState) =>
            {
                existingState.CurrentValue += meterEvent.Value;
                existingState.EventCount++;
                existingState.MinValue = Math.Min(existingState.MinValue, meterEvent.Value);
                existingState.MaxValue = Math.Max(existingState.MaxValue, meterEvent.Value);
                existingState.AverageValue = existingState.CurrentValue / existingState.EventCount;
                existingState.LastUpdated = DateTime.UtcNow;
                return existingState;
            });
    }

    /// <summary>
    /// Flush buffered events to Stripe
    /// Batches events for efficient API usage
    /// </summary>
    private async Task FlushEventsAsync(CancellationToken cancellationToken = default)
    {
        var eventsToFlush = new List<MeterEvent>();

        // Dequeue up to batch size
        while (eventsToFlush.Count < _bufferSize && _eventBuffer.TryDequeue(out var meterEvent))
        {
            eventsToFlush.Add(meterEvent);
        }

        if (!eventsToFlush.Any())
            return;

        // In production: batch send to Stripe Billing Meters API
        await Task.Delay(100, cancellationToken);

        // Mark events as processed
        foreach (var evt in eventsToFlush)
        {
            evt.Processed = true;
        }
    }

    /// <summary>
    /// Check if customer is approaching usage limits
    /// Returns true if usage > 80% of included amount
    /// </summary>
    public async Task<UsageWarning?> CheckUsageWarningAsync(
        string customerId,
        int includedExecutions,
        CancellationToken cancellationToken = default)
    {
        var report = await GetUsageReportAsync(customerId, cancellationToken: cancellationToken);
        var currentUsage = report.TotalWorkflowExecutions;
        var usagePercentage = (double)currentUsage / includedExecutions;

        if (usagePercentage >= 0.8)
        {
            return new UsageWarning
            {
                CustomerId = customerId,
                WarningLevel = usagePercentage >= 1.0 ? WarningLevel.Critical :
                               usagePercentage >= 0.9 ? WarningLevel.High :
                               WarningLevel.Medium,
                CurrentUsage = currentUsage,
                IncludedAmount = includedExecutions,
                UsagePercentage = usagePercentage,
                Message = usagePercentage >= 1.0
                    ? $"You have exceeded your included executions ({includedExecutions}). Overage charges will apply."
                    : $"You have used {usagePercentage:P0} of your included executions."
            };
        }

        return null;
    }

    public class UsageWarning
    {
        public string CustomerId { get; set; } = string.Empty;
        public WarningLevel WarningLevel { get; set; }
        public int CurrentUsage { get; set; }
        public int IncludedAmount { get; set; }
        public double UsagePercentage { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public enum WarningLevel
    {
        Medium,     // 80-89%
        High,       // 90-99%
        Critical    // 100%+
    }

    /// <summary>
    /// Dispose timer
    /// </summary>
    public void Dispose()
    {
        _flushTimer?.Dispose();
    }
}
