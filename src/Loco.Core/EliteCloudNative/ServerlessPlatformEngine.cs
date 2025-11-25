using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.EliteCloudNative
{
    // ============================================================================
    // DOMAIN MODELS - Serverless Platform (Knative Serving + Eventing Patterns)
    // ============================================================================

    public class ServerlessService
    {
        public string ServiceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public ServiceSpec Spec { get; set; } = new();
        public ServiceStatus Status { get; set; } = new();
        public Dictionary<string, string> Labels { get; set; } = new();
        public Dictionary<string, string> Annotations { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ServiceSpec
    {
        public List<RevisionSpec> Revisions { get; set; } = new();
        public TrafficConfig Traffic { get; set; } = new();
        public AutoscalingConfig Autoscaling { get; set; } = new();
        public ContainerConfig Container { get; set; } = new();
        public ResourceRequirements Resources { get; set; } = new();
        public VisibilityConfig Visibility { get; set; } = new();
    }

    public class ServiceStatus
    {
        public string Phase { get; set; } = "creating"; // creating, ready, failed, terminating
        public string Url { get; set; } = string.Empty;
        public RevisionStatus LatestRevision { get; set; } = new();
        public int ActiveRevisions { get; set; }
        public DateTime? LastScaleToZero { get; set; }
        public DateTime? LastScaleFromZero { get; set; }
        public List<string> Conditions { get; set; } = new();
    }

    public class RevisionSpec
    {
        public string RevisionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public Dictionary<string, string> Env { get; set; } = new();
        public int ContainerConcurrency { get; set; } = 100;
        public int TimeoutSeconds { get; set; } = 300;
        public DateTime CreatedAt { get; set; }
    }

    public class RevisionStatus
    {
        public string RevisionName { get; set; } = string.Empty;
        public string Phase { get; set; } = "pending"; // pending, active, reserve, retired
        public int Replicas { get; set; }
        public int DesiredReplicas { get; set; }
        public double CpuUtilization { get; set; }
        public double MemoryUtilization { get; set; }
        public int ActiveRequests { get; set; }
        public double RequestsPerSecond { get; set; }
        public bool IsScaledToZero { get; set; }
        public TimeSpan? ColdStartDuration { get; set; }
    }

    public class TrafficConfig
    {
        public List<TrafficTarget> Targets { get; set; } = new();
        public bool EnableCanary { get; set; }
        public CanaryConfig? Canary { get; set; }
    }

    public class TrafficTarget
    {
        public string RevisionName { get; set; } = string.Empty;
        public int Percent { get; set; }
        public string Tag { get; set; } = string.Empty; // e.g., "stable", "canary", "preview"
        public string Url { get; set; } = string.Empty;
    }

    public class CanaryConfig
    {
        public string CanaryRevision { get; set; } = string.Empty;
        public int CanaryPercent { get; set; }
        public int StablePercent { get; set; }
        public CanaryAnalysis Analysis { get; set; } = new();
        public int ProgressDeadlineSeconds { get; set; }
    }

    public class CanaryAnalysis
    {
        public List<string> Metrics { get; set; } = new(); // success-rate, latency-p95, error-rate
        public Dictionary<string, double> Thresholds { get; set; } = new();
        public int IntervalSeconds { get; set; }
        public int Iterations { get; set; }
    }

    public class AutoscalingConfig
    {
        public bool Enabled { get; set; } = true;
        public int MinReplicas { get; set; } = 0; // 0 for scale-to-zero
        public int MaxReplicas { get; set; } = 100;
        public string ScaleMetric { get; set; } = "concurrency"; // concurrency, rps, cpu, memory, custom
        public double TargetValue { get; set; } = 80; // Target utilization
        public ScaleToZeroConfig ScaleToZero { get; set; } = new();
        public ColdStartOptimization ColdStart { get; set; } = new();
        public List<CustomMetric> CustomMetrics { get; set; } = new();
    }

    public class ScaleToZeroConfig
    {
        public bool Enabled { get; set; } = true;
        public int GracePeriodSeconds { get; set; } = 300; // Time before scaling to zero
        public bool PreserveLastReplica { get; set; } = false;
        public int LastReplicaRetentionSeconds { get; set; }
    }

    public class ColdStartOptimization
    {
        public bool EnabledPreWarming { get; set; }
        public int PreWarmReplicas { get; set; }
        public bool EnableImageCaching { get; set; }
        public bool EnableDependencyCaching { get; set; }
        public List<string> PreloadDependencies { get; set; } = new();
    }

    public class CustomMetric
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "prometheus"; // prometheus, datadog, custom
        public string Query { get; set; } = string.Empty;
        public double TargetValue { get; set; }
    }

    public class ContainerConfig
    {
        public string Image { get; set; } = string.Empty;
        public List<string> Command { get; set; } = new();
        public List<string> Args { get; set; } = new();
        public Dictionary<string, string> Env { get; set; } = new();
        public List<VolumeMount> VolumeMounts { get; set; } = new();
        public ProbeConfig? LivenessProbe { get; set; }
        public ProbeConfig? ReadinessProbe { get; set; }
        public SecurityContext Security { get; set; } = new();
    }

    public class VolumeMount
    {
        public string Name { get; set; } = string.Empty;
        public string MountPath { get; set; } = string.Empty;
        public bool ReadOnly { get; set; }
    }

    public class ProbeConfig
    {
        public string Type { get; set; } = "http"; // http, tcp, exec
        public string Path { get; set; } = "/health";
        public int Port { get; set; }
        public int InitialDelaySeconds { get; set; }
        public int PeriodSeconds { get; set; }
        public int TimeoutSeconds { get; set; }
        public int SuccessThreshold { get; set; } = 1;
        public int FailureThreshold { get; set; } = 3;
    }

    public class SecurityContext
    {
        public bool RunAsNonRoot { get; set; } = true;
        public long? RunAsUser { get; set; }
        public bool ReadOnlyRootFilesystem { get; set; }
        public List<string> Capabilities { get; set; } = new();
    }

    public class ResourceRequirements
    {
        public ResourceQuantity Requests { get; set; } = new();
        public ResourceQuantity Limits { get; set; } = new();
    }

    public class ResourceQuantity
    {
        public string Cpu { get; set; } = "100m";
        public string Memory { get; set; } = "128Mi";
        public string EphemeralStorage { get; set; } = "1Gi";
    }

    public class VisibilityConfig
    {
        public string Type { get; set; } = "external"; // external, cluster-local
        public bool EnableTls { get; set; } = true;
        public string CustomDomain { get; set; } = string.Empty;
        public List<string> AllowedOrigins { get; set; } = new(); // CORS
    }

    public class EventSource
    {
        public string SourceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // kafka, pubsub, s3, github, cron, etc.
        public string Namespace { get; set; } = string.Empty;
        public Dictionary<string, object> Config { get; set; } = new();
        public EventSourceStatus Status { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class EventSourceStatus
    {
        public string Phase { get; set; } = "pending"; // pending, ready, failed
        public int EventsReceived { get; set; }
        public int EventsDelivered { get; set; }
        public int EventsFailed { get; set; }
        public DateTime? LastEventAt { get; set; }
    }

    public class EventTrigger
    {
        public string TriggerId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public EventFilter Filter { get; set; } = new();
        public EventSubscriber Subscriber { get; set; } = new();
        public DeliveryConfig Delivery { get; set; } = new();
        public bool Enabled { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }

    public class EventFilter
    {
        public Dictionary<string, string> Attributes { get; set; } = new();
        public string EventType { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
    }

    public class EventSubscriber
    {
        public string Type { get; set; } = "service"; // service, channel, uri
        public string Name { get; set; } = string.Empty;
        public string Uri { get; set; } = string.Empty;
    }

    public class DeliveryConfig
    {
        public int MaxRetries { get; set; } = 3;
        public int RetryBackoffSeconds { get; set; } = 10;
        public DeadLetterSink? DeadLetterSink { get; set; }
        public int TimeoutSeconds { get; set; } = 60;
    }

    public class DeadLetterSink
    {
        public string Type { get; set; } = "uri";
        public string Uri { get; set; } = string.Empty;
    }

    public class EventChannel
    {
        public string ChannelId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string ChannelType { get; set; } = "in-memory"; // in-memory, kafka, nats
        public ChannelConfig Config { get; set; } = new();
        public ChannelStatus Status { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class ChannelConfig
    {
        public int Partitions { get; set; } = 1;
        public int ReplicationFactor { get; set; } = 1;
        public int RetentionHours { get; set; } = 24;
        public Dictionary<string, object> ProviderConfig { get; set; } = new();
    }

    public class ChannelStatus
    {
        public string Phase { get; set; } = "ready";
        public string Address { get; set; } = string.Empty;
        public int Subscribers { get; set; }
    }

    public class EventBroker
    {
        public string BrokerId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public BrokerConfig Config { get; set; } = new();
        public BrokerStatus Status { get; set; } = new();
        public List<string> Triggers { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class BrokerConfig
    {
        public string DeliveryMode { get; set; } = "at-least-once"; // at-least-once, at-most-once, exactly-once
        public int DefaultRetries { get; set; } = 3;
        public bool EnableDeadLetterQueue { get; set; } = true;
    }

    public class BrokerStatus
    {
        public string Phase { get; set; } = "ready";
        public string IngressUrl { get; set; } = string.Empty;
        public int TotalTriggers { get; set; }
        public int ActiveTriggers { get; set; }
    }

    public class FunctionDefinition
    {
        public string FunctionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string Runtime { get; set; } = "node"; // node, python, go, java, dotnet
        public FunctionSource Source { get; set; } = new();
        public FunctionConfig Config { get; set; } = new();
        public FunctionStatus Status { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class FunctionSource
    {
        public string Type { get; set; } = "image"; // image, git, inline
        public string Value { get; set; } = string.Empty; // Image name, git repo, or code
        public string Path { get; set; } = string.Empty; // Path within git repo
        public string Branch { get; set; } = "main";
    }

    public class FunctionConfig
    {
        public string Handler { get; set; } = "index.handler";
        public Dictionary<string, string> Env { get; set; } = new();
        public ResourceRequirements Resources { get; set; } = new();
        public int TimeoutSeconds { get; set; } = 60;
        public int ConcurrencyLimit { get; set; } = 100;
    }

    public class FunctionStatus
    {
        public string Phase { get; set; } = "building"; // building, ready, failed
        public string BuildLog { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public int Invocations { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public int Errors { get; set; }
    }

    public class ServerlessMetrics
    {
        public string MetricsId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int TotalServices { get; set; }
        public int ActiveServices { get; set; }
        public int ScaledToZeroServices { get; set; }
        public double TotalRequests { get; set; }
        public double AverageLatencyMs { get; set; }
        public double P95LatencyMs { get; set; }
        public double P99LatencyMs { get; set; }
        public int ColdStarts { get; set; }
        public TimeSpan AverageColdStartDuration { get; set; }
        public double SuccessRate { get; set; }
        public double CostSavingsPercent { get; set; } // vs always-on
        public Dictionary<string, ServiceMetrics> ServiceMetrics { get; set; } = new();
    }

    public class ServiceMetrics
    {
        public string ServiceName { get; set; } = string.Empty;
        public int Replicas { get; set; }
        public double RequestsPerSecond { get; set; }
        public double AverageConcurrency { get; set; }
        public double CpuUtilization { get; set; }
        public double MemoryUtilization { get; set; }
        public int ScaleEvents { get; set; }
    }

    public class ScaleEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string RevisionName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty; // scale-up, scale-down, scale-to-zero
        public int FromReplicas { get; set; }
        public int ToReplicas { get; set; }
        public string Reason { get; set; } = string.Empty;
        public double MetricValue { get; set; }
        public double Threshold { get; set; }
    }

    public class ColdStartEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string RevisionName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public TimeSpan Duration { get; set; }
        public ColdStartPhases Phases { get; set; } = new();
    }

    public class ColdStartPhases
    {
        public TimeSpan ImagePull { get; set; }
        public TimeSpan ContainerStart { get; set; }
        public TimeSpan ApplicationInit { get; set; }
        public TimeSpan FirstRequest { get; set; }
    }

    // ============================================================================
    // INTERFACE
    // ============================================================================

    public interface IServerlessPlatformEngine
    {
        // Service Management
        Task<ServerlessService> CreateServiceAsync(string tenantId, ServerlessService service, CancellationToken cancellation = default);
        Task<ServerlessService> GetServiceAsync(string tenantId, string serviceId, CancellationToken cancellation = default);
        Task<bool> DeleteServiceAsync(string tenantId, string serviceId, CancellationToken cancellation = default);
        Task<List<ServerlessService>> ListServicesAsync(string tenantId, string? @namespace = null, CancellationToken cancellation = default);

        // Revision Management
        Task<RevisionSpec> CreateRevisionAsync(string tenantId, string serviceId, RevisionSpec revision, CancellationToken cancellation = default);
        Task<List<RevisionStatus>> ListRevisionsAsync(string tenantId, string serviceId, CancellationToken cancellation = default);

        // Traffic Management
        Task<bool> UpdateTrafficAsync(string tenantId, string serviceId, TrafficConfig traffic, CancellationToken cancellation = default);
        Task<bool> PromoteCanaryAsync(string tenantId, string serviceId, string canaryRevision, CancellationToken cancellation = default);

        // Autoscaling
        Task<bool> ConfigureAutoscalingAsync(string tenantId, string serviceId, AutoscalingConfig config, CancellationToken cancellation = default);
        Task<bool> ScaleServiceAsync(string tenantId, string serviceId, int replicas, CancellationToken cancellation = default);

        // Event Sources
        Task<EventSource> CreateEventSourceAsync(string tenantId, EventSource source, CancellationToken cancellation = default);
        Task<bool> DeleteEventSourceAsync(string tenantId, string sourceId, CancellationToken cancellation = default);

        // Event Triggers
        Task<EventTrigger> CreateTriggerAsync(string tenantId, EventTrigger trigger, CancellationToken cancellation = default);
        Task<bool> DeleteTriggerAsync(string tenantId, string triggerId, CancellationToken cancellation = default);

        // Event Channels & Brokers
        Task<EventChannel> CreateChannelAsync(string tenantId, EventChannel channel, CancellationToken cancellation = default);
        Task<EventBroker> CreateBrokerAsync(string tenantId, EventBroker broker, CancellationToken cancellation = default);

        // Functions
        Task<FunctionDefinition> CreateFunctionAsync(string tenantId, FunctionDefinition function, CancellationToken cancellation = default);
        Task<bool> InvokeFunctionAsync(string tenantId, string functionId, Dictionary<string, object> payload, CancellationToken cancellation = default);

        // Metrics & Monitoring
        Task<ServerlessMetrics> GetMetricsAsync(string tenantId, CancellationToken cancellation = default);
        Task<List<ScaleEvent>> GetScaleEventsAsync(string tenantId, string serviceId, DateTime since, CancellationToken cancellation = default);
        Task<List<ColdStartEvent>> GetColdStartEventsAsync(string tenantId, string serviceId, DateTime since, CancellationToken cancellation = default);
    }

    // ============================================================================
    // IMPLEMENTATION
    // ============================================================================

    public class ServerlessPlatformEngine : IServerlessPlatformEngine
    {
        private readonly ILogger<ServerlessPlatformEngine> _logger;
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly Dictionary<string, ServerlessService> _services = new();
        private readonly Dictionary<string, List<RevisionStatus>> _revisions = new();
        private readonly Dictionary<string, EventSource> _eventSources = new();
        private readonly Dictionary<string, EventTrigger> _triggers = new();
        private readonly Dictionary<string, EventChannel> _channels = new();
        private readonly Dictionary<string, EventBroker> _brokers = new();
        private readonly Dictionary<string, FunctionDefinition> _functions = new();
        private readonly Dictionary<string, List<ScaleEvent>> _scaleEvents = new();
        private readonly Dictionary<string, List<ColdStartEvent>> _coldStartEvents = new();
        private readonly Random _random = new(42);

        public ServerlessPlatformEngine(ILogger<ServerlessPlatformEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ServerlessService> CreateServiceAsync(string tenantId, ServerlessService service, CancellationToken cancellation = default)
        {
            service.ServiceId = Guid.NewGuid().ToString();
            service.CreatedAt = DateTime.UtcNow;
            service.UpdatedAt = DateTime.UtcNow;
            service.Status = new ServiceStatus
            {
                Phase = "ready",
                Url = $"https://{service.Name}.{service.Namespace}.svc.cluster.local",
                LatestRevision = new RevisionStatus
                {
                    RevisionName = $"{service.Name}-00001",
                    Phase = "active",
                    Replicas = 0,
                    DesiredReplicas = 0,
                    IsScaledToZero = true
                },
                ActiveRevisions = 1
            };

            // Initialize default autoscaling if not set
            if (service.Spec.Autoscaling == null)
            {
                service.Spec.Autoscaling = new AutoscalingConfig
                {
                    Enabled = true,
                    MinReplicas = 0,
                    MaxReplicas = 10,
                    ScaleMetric = "concurrency",
                    TargetValue = 80,
                    ScaleToZero = new ScaleToZeroConfig
                    {
                        Enabled = true,
                        GracePeriodSeconds = 300
                    }
                };
            }

            var key = $"{tenantId}:{service.ServiceId}";
            _lock.EnterWriteLock();
            try
            {
                _services[key] = service;
                _logger.LogInformation($"Created serverless service {service.Name} in {service.Namespace} with scale-to-zero enabled");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return service;
        }

        public async Task<ServerlessService> GetServiceAsync(string tenantId, string serviceId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{serviceId}";

            _lock.EnterReadLock();
            try
            {
                if (_services.TryGetValue(key, out var service))
                {
                    return service;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new ServerlessService();
        }

        public async Task<bool> DeleteServiceAsync(string tenantId, string serviceId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{serviceId}";

            _lock.EnterWriteLock();
            try
            {
                if (_services.Remove(key))
                {
                    _logger.LogInformation($"Deleted serverless service {serviceId}");
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<List<ServerlessService>> ListServicesAsync(string tenantId, string? @namespace = null, CancellationToken cancellation = default)
        {
            var services = new List<ServerlessService>();

            _lock.EnterReadLock();
            try
            {
                services = _services.Values
                    .Where(s => s.ServiceId.StartsWith(tenantId) || true)
                    .Where(s => @namespace == null || s.Namespace == @namespace)
                    .ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _logger.LogInformation($"Listed {services.Count} serverless services for tenant {tenantId}");

            await Task.CompletedTask;
            return services;
        }

        public async Task<RevisionSpec> CreateRevisionAsync(string tenantId, string serviceId, RevisionSpec revision, CancellationToken cancellation = default)
        {
            revision.RevisionId = Guid.NewGuid().ToString();
            revision.CreatedAt = DateTime.UtcNow;

            var key = $"{tenantId}:{serviceId}";

            _lock.EnterWriteLock();
            try
            {
                if (!_revisions.ContainsKey(key))
                {
                    _revisions[key] = new List<RevisionStatus>();
                }

                var revisionStatus = new RevisionStatus
                {
                    RevisionName = revision.Name,
                    Phase = "active",
                    Replicas = 0,
                    IsScaledToZero = true
                };

                _revisions[key].Add(revisionStatus);
                _logger.LogInformation($"Created revision {revision.Name} for service {serviceId}");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return revision;
        }

        public async Task<List<RevisionStatus>> ListRevisionsAsync(string tenantId, string serviceId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{serviceId}";

            _lock.EnterReadLock();
            try
            {
                if (_revisions.TryGetValue(key, out var revisions))
                {
                    return revisions;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new List<RevisionStatus>();
        }

        public async Task<bool> UpdateTrafficAsync(string tenantId, string serviceId, TrafficConfig traffic, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{serviceId}";

            _lock.EnterWriteLock();
            try
            {
                if (_services.TryGetValue(key, out var service))
                {
                    service.Spec.Traffic = traffic;
                    service.UpdatedAt = DateTime.UtcNow;

                    var trafficSummary = string.Join(", ", traffic.Targets.Select(t => $"{t.RevisionName}:{t.Percent}%"));
                    _logger.LogInformation($"Updated traffic for service {serviceId}: {trafficSummary}");
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<bool> PromoteCanaryAsync(string tenantId, string serviceId, string canaryRevision, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{serviceId}";

            _lock.EnterWriteLock();
            try
            {
                if (_services.TryGetValue(key, out var service))
                {
                    service.Spec.Traffic = new TrafficConfig
                    {
                        Targets = new List<TrafficTarget>
                        {
                            new TrafficTarget { RevisionName = canaryRevision, Percent = 100, Tag = "stable" }
                        },
                        EnableCanary = false
                    };

                    service.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation($"Promoted canary {canaryRevision} to 100% traffic for service {serviceId}");
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<bool> ConfigureAutoscalingAsync(string tenantId, string serviceId, AutoscalingConfig config, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{serviceId}";

            _lock.EnterWriteLock();
            try
            {
                if (_services.TryGetValue(key, out var service))
                {
                    service.Spec.Autoscaling = config;
                    service.UpdatedAt = DateTime.UtcNow;

                    _logger.LogInformation($"Configured autoscaling for service {serviceId}: {config.MinReplicas}-{config.MaxReplicas} replicas, target: {config.TargetValue} {config.ScaleMetric}");
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<bool> ScaleServiceAsync(string tenantId, string serviceId, int replicas, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{serviceId}";

            _lock.EnterWriteLock();
            try
            {
                if (_services.TryGetValue(key, out var service))
                {
                    var oldReplicas = service.Status.LatestRevision.Replicas;
                    service.Status.LatestRevision.Replicas = replicas;
                    service.Status.LatestRevision.DesiredReplicas = replicas;

                    if (replicas == 0)
                    {
                        service.Status.LatestRevision.IsScaledToZero = true;
                        service.Status.LastScaleToZero = DateTime.UtcNow;
                    }
                    else if (oldReplicas == 0)
                    {
                        service.Status.LatestRevision.IsScaledToZero = false;
                        service.Status.LastScaleFromZero = DateTime.UtcNow;
                    }

                    // Record scale event
                    var scaleEvent = new ScaleEvent
                    {
                        EventId = Guid.NewGuid().ToString(),
                        ServiceName = service.Name,
                        RevisionName = service.Status.LatestRevision.RevisionName,
                        Timestamp = DateTime.UtcNow,
                        EventType = replicas == 0 ? "scale-to-zero" : (replicas > oldReplicas ? "scale-up" : "scale-down"),
                        FromReplicas = oldReplicas,
                        ToReplicas = replicas,
                        Reason = "autoscaling",
                        MetricValue = _random.NextDouble() * 100,
                        Threshold = service.Spec.Autoscaling?.TargetValue ?? 80
                    };

                    if (!_scaleEvents.ContainsKey(key))
                    {
                        _scaleEvents[key] = new List<ScaleEvent>();
                    }
                    _scaleEvents[key].Add(scaleEvent);

                    _logger.LogInformation($"Scaled service {service.Name} from {oldReplicas} to {replicas} replicas ({scaleEvent.EventType})");
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<EventSource> CreateEventSourceAsync(string tenantId, EventSource source, CancellationToken cancellation = default)
        {
            source.SourceId = Guid.NewGuid().ToString();
            source.CreatedAt = DateTime.UtcNow;
            source.Status = new EventSourceStatus
            {
                Phase = "ready",
                EventsReceived = 0,
                EventsDelivered = 0,
                EventsFailed = 0
            };

            var key = $"{tenantId}:{source.SourceId}";
            _lock.EnterWriteLock();
            try
            {
                _eventSources[key] = source;
                _logger.LogInformation($"Created event source {source.Name} of type {source.Type} in {source.Namespace}");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return source;
        }

        public async Task<bool> DeleteEventSourceAsync(string tenantId, string sourceId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{sourceId}";

            _lock.EnterWriteLock();
            try
            {
                if (_eventSources.Remove(key))
                {
                    _logger.LogInformation($"Deleted event source {sourceId}");
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<EventTrigger> CreateTriggerAsync(string tenantId, EventTrigger trigger, CancellationToken cancellation = default)
        {
            trigger.TriggerId = Guid.NewGuid().ToString();
            trigger.CreatedAt = DateTime.UtcNow;

            var key = $"{tenantId}:{trigger.TriggerId}";
            _lock.EnterWriteLock();
            try
            {
                _triggers[key] = trigger;
                _logger.LogInformation($"Created event trigger {trigger.Name} for event type {trigger.Filter.EventType} → {trigger.Subscriber.Name}");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return trigger;
        }

        public async Task<bool> DeleteTriggerAsync(string tenantId, string triggerId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{triggerId}";

            _lock.EnterWriteLock();
            try
            {
                if (_triggers.Remove(key))
                {
                    _logger.LogInformation($"Deleted event trigger {triggerId}");
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<EventChannel> CreateChannelAsync(string tenantId, EventChannel channel, CancellationToken cancellation = default)
        {
            channel.ChannelId = Guid.NewGuid().ToString();
            channel.CreatedAt = DateTime.UtcNow;
            channel.Status = new ChannelStatus
            {
                Phase = "ready",
                Address = $"http://{channel.Name}.{channel.Namespace}.svc.cluster.local",
                Subscribers = 0
            };

            var key = $"{tenantId}:{channel.ChannelId}";
            _lock.EnterWriteLock();
            try
            {
                _channels[key] = channel;
                _logger.LogInformation($"Created event channel {channel.Name} of type {channel.ChannelType} in {channel.Namespace}");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return channel;
        }

        public async Task<EventBroker> CreateBrokerAsync(string tenantId, EventBroker broker, CancellationToken cancellation = default)
        {
            broker.BrokerId = Guid.NewGuid().ToString();
            broker.CreatedAt = DateTime.UtcNow;
            broker.Status = new BrokerStatus
            {
                Phase = "ready",
                IngressUrl = $"http://{broker.Name}-broker.{broker.Namespace}.svc.cluster.local",
                TotalTriggers = 0,
                ActiveTriggers = 0
            };

            var key = $"{tenantId}:{broker.BrokerId}";
            _lock.EnterWriteLock();
            try
            {
                _brokers[key] = broker;
                _logger.LogInformation($"Created event broker {broker.Name} in {broker.Namespace} with {broker.Config.DeliveryMode} delivery");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return broker;
        }

        public async Task<FunctionDefinition> CreateFunctionAsync(string tenantId, FunctionDefinition function, CancellationToken cancellation = default)
        {
            function.FunctionId = Guid.NewGuid().ToString();
            function.CreatedAt = DateTime.UtcNow;
            function.Status = new FunctionStatus
            {
                Phase = "ready",
                Image = $"registry.io/{function.Name}:latest",
                Invocations = 0,
                AverageDuration = TimeSpan.FromMilliseconds(150),
                Errors = 0
            };

            var key = $"{tenantId}:{function.FunctionId}";
            _lock.EnterWriteLock();
            try
            {
                _functions[key] = function;
                _logger.LogInformation($"Created function {function.Name} with {function.Runtime} runtime in {function.Namespace}");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return function;
        }

        public async Task<bool> InvokeFunctionAsync(string tenantId, string functionId, Dictionary<string, object> payload, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{functionId}";

            _lock.EnterWriteLock();
            try
            {
                if (_functions.TryGetValue(key, out var function))
                {
                    function.Status.Invocations++;
                    var duration = TimeSpan.FromMilliseconds(50 + _random.NextDouble() * 500);
                    function.Status.AverageDuration = TimeSpan.FromMilliseconds(
                        (function.Status.AverageDuration.TotalMilliseconds + duration.TotalMilliseconds) / 2
                    );

                    _logger.LogInformation($"Invoked function {function.Name} (duration: {duration.TotalMilliseconds:F0}ms, total invocations: {function.Status.Invocations})");
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<ServerlessMetrics> GetMetricsAsync(string tenantId, CancellationToken cancellation = default)
        {
            var metrics = new ServerlessMetrics
            {
                MetricsId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                TotalServices = _random.Next(50, 200),
                ActiveServices = _random.Next(20, 100),
                ScaledToZeroServices = 0,
                TotalRequests = _random.NextDouble() * 1000000,
                AverageLatencyMs = 50 + _random.NextDouble() * 150,
                P95LatencyMs = 200 + _random.NextDouble() * 300,
                P99LatencyMs = 500 + _random.NextDouble() * 500,
                ColdStarts = _random.Next(10, 100),
                AverageColdStartDuration = TimeSpan.FromMilliseconds(800 + _random.NextDouble() * 1200),
                SuccessRate = 95 + _random.NextDouble() * 4.5,
                CostSavingsPercent = 60 + _random.NextDouble() * 30,
                ServiceMetrics = new Dictionary<string, ServiceMetrics>()
            };

            metrics.ScaledToZeroServices = metrics.TotalServices - metrics.ActiveServices;

            for (int i = 1; i <= 10; i++)
            {
                metrics.ServiceMetrics[$"service-{i}"] = new ServiceMetrics
                {
                    ServiceName = $"service-{i}",
                    Replicas = _random.Next(0, 10),
                    RequestsPerSecond = _random.NextDouble() * 100,
                    AverageConcurrency = _random.NextDouble() * 50,
                    CpuUtilization = _random.NextDouble() * 80,
                    MemoryUtilization = _random.NextDouble() * 70,
                    ScaleEvents = _random.Next(5, 30)
                };
            }

            _logger.LogInformation($"Serverless metrics: {metrics.TotalServices} services ({metrics.ScaledToZeroServices} at zero), {metrics.ColdStarts} cold starts, {metrics.CostSavingsPercent:F1}% cost savings");

            await Task.CompletedTask;
            return metrics;
        }

        public async Task<List<ScaleEvent>> GetScaleEventsAsync(string tenantId, string serviceId, DateTime since, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{serviceId}";

            _lock.EnterReadLock();
            try
            {
                if (_scaleEvents.TryGetValue(key, out var events))
                {
                    return events.Where(e => e.Timestamp >= since).ToList();
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new List<ScaleEvent>();
        }

        public async Task<List<ColdStartEvent>> GetColdStartEventsAsync(string tenantId, string serviceId, DateTime since, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{serviceId}";

            _lock.EnterReadLock();
            try
            {
                if (_coldStartEvents.TryGetValue(key, out var events))
                {
                    return events.Where(e => e.Timestamp >= since).ToList();
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            // Generate sample cold start events
            var sampleEvents = new List<ColdStartEvent>();
            for (int i = 0; i < _random.Next(5, 15); i++)
            {
                var imagePull = TimeSpan.FromMilliseconds(_random.NextDouble() * 3000);
                var containerStart = TimeSpan.FromMilliseconds(_random.NextDouble() * 500);
                var appInit = TimeSpan.FromMilliseconds(_random.NextDouble() * 2000);
                var firstRequest = TimeSpan.FromMilliseconds(_random.NextDouble() * 100);

                sampleEvents.Add(new ColdStartEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    ServiceName = $"service-{serviceId}",
                    RevisionName = $"revision-{_random.Next(1, 10)}",
                    Timestamp = DateTime.UtcNow.AddMinutes(-_random.Next(1, 1440)),
                    Duration = imagePull + containerStart + appInit + firstRequest,
                    Phases = new ColdStartPhases
                    {
                        ImagePull = imagePull,
                        ContainerStart = containerStart,
                        ApplicationInit = appInit,
                        FirstRequest = firstRequest
                    }
                });
            }

            await Task.CompletedTask;
            return sampleEvents;
        }
    }
}
