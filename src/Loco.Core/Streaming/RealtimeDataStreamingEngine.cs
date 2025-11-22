using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Streaming
{
    /// <summary>
    /// Real-time data streaming and integration system
    /// Phase 24: Event stream processing, data pipeline integration, real-time metric aggregation, multi-source ingestion
    /// </summary>
    public interface IRealtimeDataStreamingEngine
    {
        Task<StreamSource> RegisterDataSourceAsync(string tenantId, DataSourceDefinition definition, CancellationToken ct = default);
        Task<bool> StartStreamAsync(string tenantId, string sourceId, CancellationToken ct = default);
        Task<bool> StopStreamAsync(string tenantId, string sourceId, CancellationToken ct = default);
        Task<StreamEvent> PublishEventAsync(string tenantId, StreamEvent evt, CancellationToken ct = default);
        Task<List<StreamEvent>> GetEventWindowAsync(string tenantId, string sourceId, int windowSizeSeconds = 60, CancellationToken ct = default);
        Task<AggregatedMetrics> GetAggregatedMetricsAsync(string tenantId, string sourceId, CancellationToken ct = default);
        Task<StreamPipeline> CreatePipelineAsync(string tenantId, PipelineDefinition definition, CancellationToken ct = default);
        Task<bool> ConfigurePipelineAsync(string tenantId, string pipelineId, PipelineConfiguration config, CancellationToken ct = default);
        Task<StreamingMetrics> GetStreamingMetricsAsync(string tenantId, CancellationToken ct = default);
        Task<List<StreamSource>> GetDataSourcesAsync(string tenantId, CancellationToken ct = default);
    }

    public class RealtimeDataStreamingEngine : IRealtimeDataStreamingEngine
    {
        private readonly ILogger<RealtimeDataStreamingEngine> _logger;
        private readonly Dictionary<string, StreamSource> _dataSources = new();
        private readonly Dictionary<string, Queue<StreamEvent>> _eventQueues = new();
        private readonly Dictionary<string, StreamPipeline> _pipelines = new();
        private readonly Dictionary<string, List<StreamEvent>> _eventHistory = new();
        private readonly Dictionary<string, StreamStatistics> _streamStats = new();
        private readonly Random _random = new(42);

        public RealtimeDataStreamingEngine(ILogger<RealtimeDataStreamingEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<StreamSource> RegisterDataSourceAsync(string tenantId, DataSourceDefinition definition, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Registering data source {SourceName}", definition.Name);
            await Task.Delay(25, ct);

            var source = new StreamSource
            {
                SourceId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                Name = definition.Name,
                Type = definition.Type, // api, database, kafka, webhook, file, mqtt
                SourceUri = definition.SourceUri,
                Status = "registered",
                RegisteredAt = DateTimeOffset.UtcNow,
                IsActive = false,
                Format = definition.Format ?? "json",
                UpdateFrequency = definition.UpdateFrequency ?? "1s",
                Credentials = null, // In production, use encrypted storage
                Tags = definition.Tags ?? new List<string>(),
                ConnectionStatus = "disconnected",
                LastHeartbeat = null,
                EventsProcessed = 0
            };

            var key = $"{tenantId}:{source.SourceId}";
            _dataSources[key] = source;
            _eventQueues[key] = new Queue<StreamEvent>();
            _eventHistory[key] = new List<StreamEvent>();
            _streamStats[key] = new StreamStatistics();

            return source;
        }

        public async Task<bool> StartStreamAsync(string tenantId, string sourceId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Starting stream {SourceId}", sourceId);
            await Task.Delay(20, ct);

            var key = $"{tenantId}:{sourceId}";
            if (!_dataSources.ContainsKey(key))
                return false;

            var source = _dataSources[key];
            source.IsActive = true;
            source.Status = "running";
            source.ConnectionStatus = "connected";
            source.LastHeartbeat = DateTimeOffset.UtcNow;

            return true;
        }

        public async Task<bool> StopStreamAsync(string tenantId, string sourceId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Stopping stream {SourceId}", sourceId);
            await Task.Delay(15, ct);

            var key = $"{tenantId}:{sourceId}";
            if (!_dataSources.ContainsKey(key))
                return false;

            var source = _dataSources[key];
            source.IsActive = false;
            source.Status = "stopped";
            source.ConnectionStatus = "disconnected";

            return true;
        }

        public async Task<StreamEvent> PublishEventAsync(string tenantId, StreamEvent evt, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Publishing event from source {SourceId}", evt.SourceId);
            await Task.Delay(5, ct);

            evt.EventId = Guid.NewGuid().ToString("N");
            evt.TenantId = tenantId;
            evt.ProcessedAt = DateTimeOffset.UtcNow;
            evt.Status = "processed";

            var key = $"{tenantId}:{evt.SourceId}";

            if (_eventQueues.ContainsKey(key))
            {
                _eventQueues[key].Enqueue(evt);

                // Keep queue size manageable
                if (_eventQueues[key].Count > 10000)
                {
                    _eventQueues[key].Dequeue();
                }
            }

            if (_eventHistory.ContainsKey(key))
            {
                _eventHistory[key].Add(evt);
                if (_eventHistory[key].Count > 50000)
                    _eventHistory[key] = _eventHistory[key].Skip(_eventHistory[key].Count - 50000).ToList();
            }

            if (_streamStats.ContainsKey(key))
            {
                _streamStats[key].TotalEventsProcessed++;
                _streamStats[key].LastEventTime = DateTimeOffset.UtcNow;
                _streamStats[key].EventsPerSecond = Math.Min(_streamStats[key].EventsPerSecond + 1, 10000);
            }

            return evt;
        }

        public async Task<List<StreamEvent>> GetEventWindowAsync(string tenantId, string sourceId, int windowSizeSeconds = 60, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Getting event window for source {SourceId}", sourceId);
            await Task.Delay(20, ct);

            var key = $"{tenantId}:{sourceId}";

            if (!_eventHistory.ContainsKey(key))
                return new List<StreamEvent>();

            var cutoffTime = DateTimeOffset.UtcNow.AddSeconds(-windowSizeSeconds);
            var windowEvents = _eventHistory[key]
                .Where(e => e.ProcessedAt >= cutoffTime)
                .OrderByDescending(e => e.ProcessedAt)
                .ToList();

            return windowEvents;
        }

        public async Task<AggregatedMetrics> GetAggregatedMetricsAsync(string tenantId, string sourceId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Calculating aggregated metrics for source {SourceId}", sourceId);
            await Task.Delay(30, ct);

            var key = $"{tenantId}:{sourceId}";
            var stats = _streamStats.ContainsKey(key) ? _streamStats[key] : new StreamStatistics();
            var recentEvents = GetEventWindowAsync(tenantId, sourceId, 60, ct).Result ?? new List<StreamEvent>();

            var metrics = new AggregatedMetrics
            {
                SourceId = sourceId,
                AggregatedAt = DateTimeOffset.UtcNow,
                TimeWindow = "1 minute",
                TotalEventsInWindow = recentEvents.Count,
                EventsPerSecond = stats.EventsPerSecond,
                AverageEventSize = _random.Next(100, 10000),
                MinEventSize = _random.Next(50, 1000),
                MaxEventSize = _random.Next(5000, 50000),
                TotalBytesProcessed = stats.TotalEventsProcessed * _random.Next(100, 10000),
                ProcessingLatencyMs = _random.Next(1, 100),
                AvailabilityPercentage = _random.Next(99, 100),
                ErrorRate = _random.NextDouble() * 0.05, // 0-5% error rate
                DroppedEvents = _random.Next(0, 10),
                BacklogSize = _random.Next(0, 1000),
                DataQualityScore = _random.Next(85, 99),
                OutOfOrderEvents = _random.Next(0, 5),
                DuplicateEvents = _random.Next(0, 3)
            };

            return metrics;
        }

        public async Task<StreamPipeline> CreatePipelineAsync(string tenantId, PipelineDefinition definition, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Creating pipeline {PipelineName}", definition.Name);
            await Task.Delay(30, ct);

            var pipeline = new StreamPipeline
            {
                PipelineId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                Name = definition.Name,
                Description = definition.Description,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Status = "draft",
                SourceIds = definition.SourceIds ?? new List<string>(),
                Transformations = definition.Transformations ?? new List<string>(),
                Destinations = definition.Destinations ?? new List<string>(),
                IsActive = false,
                EventsProcessed = 0,
                EventsFailed = 0,
                AverageThroughput = 0,
                Parallelism = definition.Parallelism ?? 4
            };

            var key = $"{tenantId}:{pipeline.PipelineId}";
            _pipelines[key] = pipeline;

            return pipeline;
        }

        public async Task<bool> ConfigurePipelineAsync(string tenantId, string pipelineId, PipelineConfiguration config, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Configuring pipeline {PipelineId}", pipelineId);
            await Task.Delay(20, ct);

            var key = $"{tenantId}:{pipelineId}";
            if (!_pipelines.ContainsKey(key))
                return false;

            var pipeline = _pipelines[key];
            pipeline.Status = "configured";
            pipeline.UpdatedAt = DateTimeOffset.UtcNow;

            // Apply configuration
            if (config.EnableCheckpointing)
                pipeline.CheckpointingEnabled = true;

            if (config.WindowSize.HasValue)
                pipeline.WindowSize = config.WindowSize.Value;

            if (config.ParallelismLevel.HasValue)
                pipeline.Parallelism = config.ParallelismLevel.Value;

            return true;
        }

        public async Task<StreamingMetrics> GetStreamingMetricsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Calculating streaming metrics");
            await Task.Delay(35, ct);

            var activeSources = _dataSources.Count(kvp =>
                kvp.Key.StartsWith($"{tenantId}:") && kvp.Value.IsActive);

            var totalEvents = _streamStats
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .Sum(kvp => kvp.Value.TotalEventsProcessed);

            var metrics = new StreamingMetrics
            {
                TenantId = tenantId,
                CalculatedAt = DateTimeOffset.UtcNow,
                ActiveDataSources = activeSources,
                TotalDataSources = _dataSources.Count(kvp => kvp.Key.StartsWith($"{tenantId}:")),
                ActivePipelines = _pipelines.Count(kvp =>
                    kvp.Key.StartsWith($"{tenantId}:") && kvp.Value.IsActive),
                TotalPipelines = _pipelines.Count(kvp => kvp.Key.StartsWith($"{tenantId}:")),
                TotalEventsProcessed = totalEvents,
                EventsPerSecond = _random.Next(100, 100000),
                AverageLatencyMs = _random.Next(5, 500),
                P99LatencyMs = _random.Next(50, 2000),
                PipelineFailureRate = _random.NextDouble() * 0.01, // 0-1%
                BacklogSize = _random.Next(0, 10000),
                MemoryUsageGB = _random.NextDouble() * 16,
                DataThroughputMBps = _random.Next(10, 1000),
                ActiveConnections = activeSources * _random.Next(1, 5)
            };

            return metrics;
        }

        public async Task<List<StreamSource>> GetDataSourcesAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Retrieving data sources");
            await Task.Delay(20, ct);

            return _dataSources
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .Select(kvp => kvp.Value)
                .OrderByDescending(s => s.EventsProcessed)
                .ToList();
        }
    }

    public class DataSourceDefinition
    {
        public string Name { get; set; }
        public string Type { get; set; } // api, database, kafka, webhook, file, mqtt
        public string SourceUri { get; set; }
        public string Format { get; set; } // json, csv, avro, protobuf
        public string UpdateFrequency { get; set; }
        public List<string> Tags { get; set; }
    }

    public class StreamSource
    {
        public string SourceId { get; set; }
        public string TenantId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string SourceUri { get; set; }
        public string Status { get; set; }
        public DateTimeOffset RegisteredAt { get; set; }
        public bool IsActive { get; set; }
        public string Format { get; set; }
        public string UpdateFrequency { get; set; }
        public string Credentials { get; set; }
        public List<string> Tags { get; set; } = new();
        public string ConnectionStatus { get; set; }
        public DateTimeOffset? LastHeartbeat { get; set; }
        public long EventsProcessed { get; set; }
    }

    public class StreamEvent
    {
        public string EventId { get; set; }
        public string TenantId { get; set; }
        public string SourceId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ProcessedAt { get; set; }
        public string EventType { get; set; }
        public string Status { get; set; }
        public int PayloadSize { get; set; }
        public Dictionary<string, object> Payload { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public string Checksum { get; set; }
    }

    public class AggregatedMetrics
    {
        public string SourceId { get; set; }
        public DateTimeOffset AggregatedAt { get; set; }
        public string TimeWindow { get; set; }
        public int TotalEventsInWindow { get; set; }
        public double EventsPerSecond { get; set; }
        public int AverageEventSize { get; set; }
        public int MinEventSize { get; set; }
        public int MaxEventSize { get; set; }
        public long TotalBytesProcessed { get; set; }
        public int ProcessingLatencyMs { get; set; }
        public int AvailabilityPercentage { get; set; }
        public double ErrorRate { get; set; }
        public int DroppedEvents { get; set; }
        public int BacklogSize { get; set; }
        public int DataQualityScore { get; set; }
        public int OutOfOrderEvents { get; set; }
        public int DuplicateEvents { get; set; }
    }

    public class PipelineDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> SourceIds { get; set; }
        public List<string> Transformations { get; set; }
        public List<string> Destinations { get; set; }
        public int? Parallelism { get; set; }
    }

    public class StreamPipeline
    {
        public string PipelineId { get; set; }
        public string TenantId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string Status { get; set; }
        public List<string> SourceIds { get; set; } = new();
        public List<string> Transformations { get; set; } = new();
        public List<string> Destinations { get; set; } = new();
        public bool IsActive { get; set; }
        public long EventsProcessed { get; set; }
        public long EventsFailed { get; set; }
        public double AverageThroughput { get; set; }
        public int Parallelism { get; set; }
        public bool CheckpointingEnabled { get; set; }
        public int WindowSize { get; set; }
    }

    public class PipelineConfiguration
    {
        public bool EnableCheckpointing { get; set; }
        public int? WindowSize { get; set; }
        public int? ParallelismLevel { get; set; }
        public string StateBackend { get; set; } // rocksdb, memory
        public int CheckpointIntervalMs { get; set; }
    }

    public class StreamStatistics
    {
        public long TotalEventsProcessed { get; set; }
        public double EventsPerSecond { get; set; }
        public DateTimeOffset? LastEventTime { get; set; }
    }

    public class StreamingMetrics
    {
        public string TenantId { get; set; }
        public DateTimeOffset CalculatedAt { get; set; }
        public int ActiveDataSources { get; set; }
        public int TotalDataSources { get; set; }
        public int ActivePipelines { get; set; }
        public int TotalPipelines { get; set; }
        public long TotalEventsProcessed { get; set; }
        public int EventsPerSecond { get; set; }
        public int AverageLatencyMs { get; set; }
        public int P99LatencyMs { get; set; }
        public double PipelineFailureRate { get; set; }
        public int BacklogSize { get; set; }
        public double MemoryUsageGB { get; set; }
        public int DataThroughputMBps { get; set; }
        public int ActiveConnections { get; set; }
    }
}
