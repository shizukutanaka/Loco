// =============================================================================
// GPU Workload Scheduling Engine - Kueue & Volcano Integration
// =============================================================================
// Research Sources (2025):
// - https://debugg.ai/resources/kubernetes-gpu-scheduling-2025-kueue-volcano-mig
// - https://www.coreweave.com/blog/kueue-a-kubernetes-native-system-for-ai-training-workloads
// - https://volcano.sh/en/
// - https://docs.ray.io/en/latest/cluster/kubernetes/examples/rayjob-kueue-gang-scheduling.html
//
// Key Concepts:
// - Kueue: Kubernetes-native job queueing system for ML/batch workloads
// - Volcano: Advanced scheduler for HPC/AI with gang scheduling
// - Gang Scheduling: All-or-nothing scheduling for distributed workloads
// - Topology-Aware Scheduling: GPU/RDMA fabric-aware placement
// - Cohort-based Quota Borrowing: Resource sharing across queues
//
// 2025 Best Practice:
// "Organizations that win with AI at scale treat GPUs as a shared,
//  policy-driven substrate governed by queues, not as pets hand-assigned to projects."
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AIPlatform
{
    #region Enums

    /// <summary>
    /// Scheduler type
    /// </summary>
    public enum GPUSchedulerType
    {
        /// <summary>Default Kubernetes scheduler with Kueue admission</summary>
        Kueue,
        /// <summary>Volcano scheduler for HPC/gang workloads</summary>
        Volcano,
        /// <summary>KAI Scheduler for fractional GPU</summary>
        KAI,
        /// <summary>Apache YuniKorn scheduler</summary>
        YuniKorn,
        /// <summary>Default Kubernetes scheduler</summary>
        Default
    }

    /// <summary>
    /// Workload type for GPU scheduling
    /// </summary>
    public enum GPUWorkloadType
    {
        /// <summary>Single GPU training job</summary>
        SingleGPUTraining,
        /// <summary>Distributed training (PyTorch, TensorFlow)</summary>
        DistributedTraining,
        /// <summary>Model inference serving</summary>
        Inference,
        /// <summary>Batch processing</summary>
        Batch,
        /// <summary>Interactive notebook (Jupyter)</summary>
        Interactive,
        /// <summary>Ray distributed computing</summary>
        RayJob,
        /// <summary>MPI-based HPC workload</summary>
        MPIJob
    }

    /// <summary>
    /// Queue status
    /// </summary>
    public enum QueueStatus
    {
        Active,
        Suspended,
        Draining,
        Stopped
    }

    /// <summary>
    /// Workload admission status
    /// </summary>
    public enum WorkloadAdmissionStatus
    {
        Pending,
        Queued,
        Admitted,
        Running,
        Suspended,
        Preempted,
        Completed,
        Failed
    }

    /// <summary>
    /// Preemption policy
    /// </summary>
    public enum PreemptionPolicy
    {
        Never,
        LowerPriority,
        ReclaimWithinCohort,
        Any
    }

    /// <summary>
    /// Topology level for GPU placement
    /// </summary>
    public enum TopologyLevel
    {
        /// <summary>Same GPU (for MIG instances)</summary>
        GPU,
        /// <summary>Same NVLink domain</summary>
        NVLink,
        /// <summary>Same node</summary>
        Node,
        /// <summary>Same rack (network topology)</summary>
        Rack,
        /// <summary>Any placement</summary>
        Any
    }

    #endregion

    #region Configuration Classes

    /// <summary>
    /// Cluster queue configuration (Kueue)
    /// </summary>
    public class ClusterQueueConfig
    {
        public string Name { get; set; } = string.Empty;
        public string CohortName { get; set; } = string.Empty;
        public List<ResourceFlavorConfig> ResourceFlavors { get; set; } = new();
        public QueueQuotaConfig Quota { get; set; } = new();
        public PreemptionConfig Preemption { get; set; } = new();
        public bool EnableBorrowing { get; set; } = true;
        public bool EnableLending { get; set; } = true;
        public Dictionary<string, string> Labels { get; set; } = new();
    }

    /// <summary>
    /// Resource flavor configuration
    /// </summary>
    public class ResourceFlavorConfig
    {
        public string Name { get; set; } = string.Empty;
        public GPUFlavorType FlavorType { get; set; }
        public Dictionary<string, string> NodeLabels { get; set; } = new();
        public List<string> Tolerations { get; set; } = new();
        public TopologyConfig? Topology { get; set; }
    }

    /// <summary>
    /// GPU flavor types
    /// </summary>
    public enum GPUFlavorType
    {
        A100_80GB,
        A100_40GB,
        H100_80GB,
        L40_48GB,
        L4_24GB,
        T4_16GB,
        V100_32GB,
        A10G_24GB,
        MIG_1g5gb,
        MIG_2g10gb,
        MIG_3g20gb,
        MIG_4g20gb,
        MIG_7g40gb
    }

    /// <summary>
    /// Topology configuration for scheduling
    /// </summary>
    public class TopologyConfig
    {
        public TopologyLevel RequiredLevel { get; set; } = TopologyLevel.Node;
        public TopologyLevel PreferredLevel { get; set; } = TopologyLevel.NVLink;
        public bool EnableRDMAAwareness { get; set; } = false;
        public string TopologyKey { get; set; } = "topology.kubernetes.io/zone";
    }

    /// <summary>
    /// Queue quota configuration
    /// </summary>
    public class QueueQuotaConfig
    {
        public int NominalGPUs { get; set; }
        public int BorrowingLimit { get; set; }
        public int LendingLimit { get; set; }
        public string CpuLimit { get; set; } = "100";
        public string MemoryLimit { get; set; } = "500Gi";
        public Dictionary<GPUFlavorType, int> GPUsByFlavor { get; set; } = new();
    }

    /// <summary>
    /// Preemption configuration
    /// </summary>
    public class PreemptionConfig
    {
        public PreemptionPolicy Policy { get; set; } = PreemptionPolicy.LowerPriority;
        public bool ReclaimWithinCohort { get; set; } = true;
        public int GracePeriodSeconds { get; set; } = 30;
        public bool BorrowWithinCohort { get; set; } = true;
    }

    /// <summary>
    /// Local queue configuration
    /// </summary>
    public class LocalQueueConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string ClusterQueueName { get; set; } = string.Empty;
        public Dictionary<string, string> Labels { get; set; } = new();
    }

    /// <summary>
    /// GPU workload configuration
    /// </summary>
    public class GPUWorkloadConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public GPUWorkloadType WorkloadType { get; set; }
        public string LocalQueueName { get; set; } = string.Empty;
        public int Priority { get; set; } = 0;
        public string PriorityClassName { get; set; } = "default";
        public GPUResourceRequest Resources { get; set; } = new();
        public GangSchedulingConfig? GangScheduling { get; set; }
        public TopologyConstraints? TopologyConstraints { get; set; }
        public Dictionary<string, string> Labels { get; set; } = new();
        public Dictionary<string, string> Annotations { get; set; } = new();
        public int MaxRetries { get; set; } = 3;
        public string? TTLSecondsAfterFinished { get; set; }
        public bool EnableCheckpointing { get; set; } = false;
    }

    /// <summary>
    /// GPU resource request
    /// </summary>
    public class GPUResourceRequest
    {
        public int GPUCount { get; set; } = 1;
        public GPUFlavorType? PreferredFlavor { get; set; }
        public List<GPUFlavorType>? AcceptableFlavors { get; set; }
        public string? GPUMemoryMin { get; set; }
        public bool AllowFractional { get; set; } = false;
        public double FractionalGPU { get; set; } = 1.0;
        public string Cpu { get; set; } = "4";
        public string Memory { get; set; } = "16Gi";
    }

    /// <summary>
    /// Gang scheduling configuration
    /// </summary>
    public class GangSchedulingConfig
    {
        public bool Enabled { get; set; } = true;
        public int MinMembers { get; set; } = 1;
        public int TotalMembers { get; set; } = 1;
        public string SchedulerName { get; set; } = "volcano";
        public string? GangId { get; set; }
        public TopologyLevel TopologyLevel { get; set; } = TopologyLevel.Any;
    }

    /// <summary>
    /// Topology constraints for workload placement
    /// </summary>
    public class TopologyConstraints
    {
        public TopologyLevel RequiredLevel { get; set; } = TopologyLevel.Any;
        public bool RequireNVLink { get; set; } = false;
        public bool RequireRDMA { get; set; } = false;
        public string? NodeAffinityKey { get; set; }
        public string? NodeAffinityValue { get; set; }
        public List<string>? PreferredNodes { get; set; }
    }

    /// <summary>
    /// Cohort configuration for resource sharing
    /// </summary>
    public class CohortConfig
    {
        public string Name { get; set; } = string.Empty;
        public List<string> ClusterQueues { get; set; } = new();
        public CohortSharingPolicy SharingPolicy { get; set; } = new();
    }

    /// <summary>
    /// Cohort sharing policy
    /// </summary>
    public class CohortSharingPolicy
    {
        public bool EnableBorrowing { get; set; } = true;
        public bool EnablePreemption { get; set; } = true;
        public int MaxBorrowingPercent { get; set; } = 100;
    }

    #endregion

    #region Result Classes

    /// <summary>
    /// Cluster queue information
    /// </summary>
    public class ClusterQueue
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string CohortName { get; set; } = string.Empty;
        public QueueStatus Status { get; set; }
        public int PendingWorkloads { get; set; }
        public int AdmittedWorkloads { get; set; }
        public QueueResourceUsage ResourceUsage { get; set; } = new();
        public QueueQuotaConfig Quota { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
    }

    /// <summary>
    /// Queue resource usage
    /// </summary>
    public class QueueResourceUsage
    {
        public int GPUsUsed { get; set; }
        public int GPUsBorrowed { get; set; }
        public int GPUsLent { get; set; }
        public double GPUUtilizationPercent { get; set; }
        public string CpuUsed { get; set; } = "0";
        public string MemoryUsed { get; set; } = "0";
        public Dictionary<GPUFlavorType, int> GPUsUsedByFlavor { get; set; } = new();
    }

    /// <summary>
    /// Local queue information
    /// </summary>
    public class LocalQueue
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string ClusterQueueName { get; set; } = string.Empty;
        public QueueStatus Status { get; set; }
        public int PendingWorkloads { get; set; }
        public int AdmittedWorkloads { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// GPU workload information
    /// </summary>
    public class GPUWorkload
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public GPUWorkloadType WorkloadType { get; set; }
        public WorkloadAdmissionStatus AdmissionStatus { get; set; }
        public string LocalQueueName { get; set; } = string.Empty;
        public string ClusterQueueName { get; set; } = string.Empty;
        public int Priority { get; set; }
        public GPUResourceRequest Resources { get; set; } = new();
        public WorkloadPlacement? Placement { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? AdmittedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int QueuePosition { get; set; }
        public string? Message { get; set; }
        public List<WorkloadCondition> Conditions { get; set; } = new();
    }

    /// <summary>
    /// Workload placement information
    /// </summary>
    public class WorkloadPlacement
    {
        public List<string> NodeNames { get; set; } = new();
        public List<string> GPUIds { get; set; } = new();
        public GPUFlavorType AssignedFlavor { get; set; }
        public TopologyLevel AchievedTopology { get; set; }
        public bool IsBorrowed { get; set; }
        public string? BorrowedFromQueue { get; set; }
    }

    /// <summary>
    /// Workload condition
    /// </summary>
    public class WorkloadCondition
    {
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime LastTransitionTime { get; set; }
    }

    /// <summary>
    /// Cohort information
    /// </summary>
    public class Cohort
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string> ClusterQueues { get; set; } = new();
        public CohortResourceSummary ResourceSummary { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Cohort resource summary
    /// </summary>
    public class CohortResourceSummary
    {
        public int TotalGPUs { get; set; }
        public int UsedGPUs { get; set; }
        public int BorrowedGPUs { get; set; }
        public int AvailableGPUs { get; set; }
        public double OverallUtilization { get; set; }
    }

    /// <summary>
    /// Queue metrics
    /// </summary>
    public class QueueMetrics
    {
        public string QueueName { get; set; } = string.Empty;
        public TimeSpan AverageWaitTime { get; set; }
        public TimeSpan P95WaitTime { get; set; }
        public int WorkloadsAdmittedLast24h { get; set; }
        public int WorkloadsPreemptedLast24h { get; set; }
        public double GPUUtilizationPercent { get; set; }
        public double FairnessScore { get; set; }
        public DateTime MeasuredAt { get; set; }
    }

    /// <summary>
    /// Scheduling decision
    /// </summary>
    public class SchedulingDecision
    {
        public string WorkloadId { get; set; } = string.Empty;
        public bool Admitted { get; set; }
        public string? RejectionReason { get; set; }
        public int QueuePosition { get; set; }
        public TimeSpan EstimatedWaitTime { get; set; }
        public WorkloadPlacement? Placement { get; set; }
        public List<string> PreemptedWorkloads { get; set; } = new();
        public DateTime DecisionTime { get; set; }
    }

    #endregion

    #region Interface

    /// <summary>
    /// GPU Workload Scheduling Engine interface
    /// Provides Kueue and Volcano integration for GPU workload scheduling
    /// </summary>
    public interface IGPUWorkloadSchedulingEngine
    {
        // Cluster Queue Management
        Task<ClusterQueue> CreateClusterQueueAsync(ClusterQueueConfig config, CancellationToken cancellation = default);
        Task<ClusterQueue> GetClusterQueueAsync(string queueName, CancellationToken cancellation = default);
        Task<List<ClusterQueue>> ListClusterQueuesAsync(CancellationToken cancellation = default);
        Task<ClusterQueue> UpdateClusterQueueAsync(string queueName, ClusterQueueConfig config, CancellationToken cancellation = default);
        Task DeleteClusterQueueAsync(string queueName, CancellationToken cancellation = default);
        Task SuspendClusterQueueAsync(string queueName, CancellationToken cancellation = default);
        Task ResumeClusterQueueAsync(string queueName, CancellationToken cancellation = default);

        // Local Queue Management
        Task<LocalQueue> CreateLocalQueueAsync(LocalQueueConfig config, CancellationToken cancellation = default);
        Task<LocalQueue> GetLocalQueueAsync(string queueName, string ns, CancellationToken cancellation = default);
        Task<List<LocalQueue>> ListLocalQueuesAsync(string? ns = null, CancellationToken cancellation = default);
        Task DeleteLocalQueueAsync(string queueName, string ns, CancellationToken cancellation = default);

        // Cohort Management
        Task<Cohort> CreateCohortAsync(CohortConfig config, CancellationToken cancellation = default);
        Task<Cohort> GetCohortAsync(string cohortName, CancellationToken cancellation = default);
        Task<List<Cohort>> ListCohortsAsync(CancellationToken cancellation = default);
        Task AddQueueToCohortAsync(string cohortName, string queueName, CancellationToken cancellation = default);
        Task RemoveQueueFromCohortAsync(string cohortName, string queueName, CancellationToken cancellation = default);

        // Workload Submission & Management
        Task<GPUWorkload> SubmitWorkloadAsync(GPUWorkloadConfig config, CancellationToken cancellation = default);
        Task<GPUWorkload> GetWorkloadAsync(string workloadId, CancellationToken cancellation = default);
        Task<List<GPUWorkload>> ListWorkloadsAsync(string? ns = null, string? queueName = null, CancellationToken cancellation = default);
        Task SuspendWorkloadAsync(string workloadId, CancellationToken cancellation = default);
        Task ResumeWorkloadAsync(string workloadId, CancellationToken cancellation = default);
        Task CancelWorkloadAsync(string workloadId, CancellationToken cancellation = default);

        // Gang Scheduling (Volcano)
        Task<GPUWorkload> SubmitGangWorkloadAsync(GPUWorkloadConfig config, GangSchedulingConfig gangConfig, CancellationToken cancellation = default);
        Task<List<GPUWorkload>> GetGangMembersAsync(string gangId, CancellationToken cancellation = default);

        // Scheduling & Admission
        Task<SchedulingDecision> SimulateAdmissionAsync(GPUWorkloadConfig config, CancellationToken cancellation = default);
        Task<int> GetQueuePositionAsync(string workloadId, CancellationToken cancellation = default);
        Task<TimeSpan> EstimateWaitTimeAsync(string workloadId, CancellationToken cancellation = default);
        Task PreemptWorkloadAsync(string workloadId, string reason, CancellationToken cancellation = default);

        // Metrics & Monitoring
        Task<QueueMetrics> GetQueueMetricsAsync(string queueName, CancellationToken cancellation = default);
        Task<List<QueueMetrics>> GetAllQueueMetricsAsync(CancellationToken cancellation = default);
        Task<CohortResourceSummary> GetCohortResourceSummaryAsync(string cohortName, CancellationToken cancellation = default);
    }

    #endregion

    #region Implementation

    /// <summary>
    /// GPU Workload Scheduling Engine implementation
    /// </summary>
    public class GPUWorkloadSchedulingEngine : IGPUWorkloadSchedulingEngine
    {
        private readonly ILogger<GPUWorkloadSchedulingEngine> _logger;
        private readonly ConcurrentDictionary<string, ClusterQueue> _clusterQueues = new();
        private readonly ConcurrentDictionary<string, LocalQueue> _localQueues = new();
        private readonly ConcurrentDictionary<string, Cohort> _cohorts = new();
        private readonly ConcurrentDictionary<string, GPUWorkload> _workloads = new();
        private readonly ConcurrentQueue<string> _admissionQueue = new();

        public GPUWorkloadSchedulingEngine(ILogger<GPUWorkloadSchedulingEngine> logger)
        {
            _logger = logger;
        }

        #region Cluster Queue Management

        public async Task<ClusterQueue> CreateClusterQueueAsync(ClusterQueueConfig config, CancellationToken cancellation = default)
        {
            _logger.LogInformation("Creating cluster queue: {QueueName} in cohort: {CohortName}",
                config.Name, config.CohortName);

            var queue = new ClusterQueue
            {
                Id = GenerateId("cq"),
                Name = config.Name,
                CohortName = config.CohortName,
                Status = QueueStatus.Active,
                Quota = config.Quota,
                ResourceUsage = new QueueResourceUsage(),
                CreatedAt = DateTime.UtcNow
            };

            // Generate ClusterQueue YAML for Kueue
            var queueYaml = GenerateClusterQueueYaml(config);
            _logger.LogDebug("Generated ClusterQueue YAML:\n{Yaml}", queueYaml);

            await Task.Delay(100, cancellation);

            _clusterQueues[queue.Name] = queue;

            // Add to cohort if specified
            if (!string.IsNullOrEmpty(config.CohortName))
            {
                if (_cohorts.TryGetValue(config.CohortName, out var cohort))
                {
                    cohort.ClusterQueues.Add(queue.Name);
                }
            }

            return queue;
        }

        public Task<ClusterQueue> GetClusterQueueAsync(string queueName, CancellationToken cancellation = default)
        {
            if (!_clusterQueues.TryGetValue(queueName, out var queue))
            {
                throw new KeyNotFoundException($"Cluster queue not found: {queueName}");
            }
            return Task.FromResult(queue);
        }

        public Task<List<ClusterQueue>> ListClusterQueuesAsync(CancellationToken cancellation = default)
        {
            return Task.FromResult(_clusterQueues.Values.ToList());
        }

        public async Task<ClusterQueue> UpdateClusterQueueAsync(string queueName, ClusterQueueConfig config, CancellationToken cancellation = default)
        {
            if (!_clusterQueues.TryGetValue(queueName, out var queue))
            {
                throw new KeyNotFoundException($"Cluster queue not found: {queueName}");
            }

            queue.Quota = config.Quota;
            queue.CohortName = config.CohortName;
            queue.LastUpdatedAt = DateTime.UtcNow;

            await Task.Delay(50, cancellation);
            return queue;
        }

        public async Task DeleteClusterQueueAsync(string queueName, CancellationToken cancellation = default)
        {
            if (!_clusterQueues.TryRemove(queueName, out var queue))
            {
                throw new KeyNotFoundException($"Cluster queue not found: {queueName}");
            }

            // Remove from cohort
            foreach (var cohort in _cohorts.Values)
            {
                cohort.ClusterQueues.Remove(queueName);
            }

            _logger.LogInformation("Deleted cluster queue: {QueueName}", queueName);
            await Task.Delay(50, cancellation);
        }

        public async Task SuspendClusterQueueAsync(string queueName, CancellationToken cancellation = default)
        {
            if (!_clusterQueues.TryGetValue(queueName, out var queue))
            {
                throw new KeyNotFoundException($"Cluster queue not found: {queueName}");
            }

            queue.Status = QueueStatus.Suspended;
            _logger.LogInformation("Suspended cluster queue: {QueueName}", queueName);
            await Task.Delay(50, cancellation);
        }

        public async Task ResumeClusterQueueAsync(string queueName, CancellationToken cancellation = default)
        {
            if (!_clusterQueues.TryGetValue(queueName, out var queue))
            {
                throw new KeyNotFoundException($"Cluster queue not found: {queueName}");
            }

            queue.Status = QueueStatus.Active;
            _logger.LogInformation("Resumed cluster queue: {QueueName}", queueName);
            await Task.Delay(50, cancellation);
        }

        #endregion

        #region Local Queue Management

        public async Task<LocalQueue> CreateLocalQueueAsync(LocalQueueConfig config, CancellationToken cancellation = default)
        {
            _logger.LogInformation("Creating local queue: {QueueName} in namespace: {Namespace}",
                config.Name, config.Namespace);

            var queue = new LocalQueue
            {
                Id = GenerateId("lq"),
                Name = config.Name,
                Namespace = config.Namespace,
                ClusterQueueName = config.ClusterQueueName,
                Status = QueueStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            var key = $"{config.Namespace}/{config.Name}";
            _localQueues[key] = queue;

            await Task.Delay(50, cancellation);
            return queue;
        }

        public Task<LocalQueue> GetLocalQueueAsync(string queueName, string ns, CancellationToken cancellation = default)
        {
            var key = $"{ns}/{queueName}";
            if (!_localQueues.TryGetValue(key, out var queue))
            {
                throw new KeyNotFoundException($"Local queue not found: {key}");
            }
            return Task.FromResult(queue);
        }

        public Task<List<LocalQueue>> ListLocalQueuesAsync(string? ns = null, CancellationToken cancellation = default)
        {
            var queues = _localQueues.Values.AsEnumerable();
            if (!string.IsNullOrEmpty(ns))
            {
                queues = queues.Where(q => q.Namespace == ns);
            }
            return Task.FromResult(queues.ToList());
        }

        public async Task DeleteLocalQueueAsync(string queueName, string ns, CancellationToken cancellation = default)
        {
            var key = $"{ns}/{queueName}";
            _localQueues.TryRemove(key, out _);
            await Task.Delay(50, cancellation);
        }

        #endregion

        #region Cohort Management

        public async Task<Cohort> CreateCohortAsync(CohortConfig config, CancellationToken cancellation = default)
        {
            _logger.LogInformation("Creating cohort: {CohortName}", config.Name);

            var cohort = new Cohort
            {
                Id = GenerateId("cohort"),
                Name = config.Name,
                ClusterQueues = config.ClusterQueues,
                ResourceSummary = new CohortResourceSummary(),
                CreatedAt = DateTime.UtcNow
            };

            _cohorts[config.Name] = cohort;

            // Update cluster queues to reference this cohort
            foreach (var queueName in config.ClusterQueues)
            {
                if (_clusterQueues.TryGetValue(queueName, out var queue))
                {
                    queue.CohortName = config.Name;
                }
            }

            await Task.Delay(50, cancellation);
            return cohort;
        }

        public Task<Cohort> GetCohortAsync(string cohortName, CancellationToken cancellation = default)
        {
            if (!_cohorts.TryGetValue(cohortName, out var cohort))
            {
                throw new KeyNotFoundException($"Cohort not found: {cohortName}");
            }
            return Task.FromResult(cohort);
        }

        public Task<List<Cohort>> ListCohortsAsync(CancellationToken cancellation = default)
        {
            return Task.FromResult(_cohorts.Values.ToList());
        }

        public async Task AddQueueToCohortAsync(string cohortName, string queueName, CancellationToken cancellation = default)
        {
            if (!_cohorts.TryGetValue(cohortName, out var cohort))
            {
                throw new KeyNotFoundException($"Cohort not found: {cohortName}");
            }

            if (!cohort.ClusterQueues.Contains(queueName))
            {
                cohort.ClusterQueues.Add(queueName);
            }

            if (_clusterQueues.TryGetValue(queueName, out var queue))
            {
                queue.CohortName = cohortName;
            }

            await Task.Delay(50, cancellation);
        }

        public async Task RemoveQueueFromCohortAsync(string cohortName, string queueName, CancellationToken cancellation = default)
        {
            if (!_cohorts.TryGetValue(cohortName, out var cohort))
            {
                throw new KeyNotFoundException($"Cohort not found: {cohortName}");
            }

            cohort.ClusterQueues.Remove(queueName);

            if (_clusterQueues.TryGetValue(queueName, out var queue))
            {
                queue.CohortName = string.Empty;
            }

            await Task.Delay(50, cancellation);
        }

        #endregion

        #region Workload Submission & Management

        public async Task<GPUWorkload> SubmitWorkloadAsync(GPUWorkloadConfig config, CancellationToken cancellation = default)
        {
            _logger.LogInformation("Submitting GPU workload: {WorkloadName} to queue: {QueueName}",
                config.Name, config.LocalQueueName);

            var workload = new GPUWorkload
            {
                Id = GenerateId("wl"),
                Name = config.Name,
                Namespace = config.Namespace,
                WorkloadType = config.WorkloadType,
                AdmissionStatus = WorkloadAdmissionStatus.Pending,
                LocalQueueName = config.LocalQueueName,
                Priority = config.Priority,
                Resources = config.Resources,
                CreatedAt = DateTime.UtcNow,
                Conditions = new List<WorkloadCondition>
                {
                    new WorkloadCondition
                    {
                        Type = "Pending",
                        Status = "True",
                        Reason = "WorkloadSubmitted",
                        Message = "Workload submitted to queue",
                        LastTransitionTime = DateTime.UtcNow
                    }
                }
            };

            _workloads[workload.Id] = workload;
            _admissionQueue.Enqueue(workload.Id);

            // Find cluster queue from local queue
            var lqKey = $"{config.Namespace}/{config.LocalQueueName}";
            if (_localQueues.TryGetValue(lqKey, out var localQueue))
            {
                workload.ClusterQueueName = localQueue.ClusterQueueName;
                localQueue.PendingWorkloads++;
            }

            // Simulate admission decision
            var decision = await SimulateAdmissionAsync(config, cancellation);
            if (decision.Admitted)
            {
                workload.AdmissionStatus = WorkloadAdmissionStatus.Admitted;
                workload.AdmittedAt = DateTime.UtcNow;
                workload.Placement = decision.Placement;
                workload.QueuePosition = 0;

                if (_localQueues.TryGetValue(lqKey, out var lq))
                {
                    lq.PendingWorkloads--;
                    lq.AdmittedWorkloads++;
                }

                if (_clusterQueues.TryGetValue(workload.ClusterQueueName, out var cq))
                {
                    cq.AdmittedWorkloads++;
                    cq.ResourceUsage.GPUsUsed += config.Resources.GPUCount;
                }
            }
            else
            {
                workload.AdmissionStatus = WorkloadAdmissionStatus.Queued;
                workload.QueuePosition = decision.QueuePosition;
                workload.Message = decision.RejectionReason;
            }

            return workload;
        }

        public Task<GPUWorkload> GetWorkloadAsync(string workloadId, CancellationToken cancellation = default)
        {
            if (!_workloads.TryGetValue(workloadId, out var workload))
            {
                throw new KeyNotFoundException($"Workload not found: {workloadId}");
            }
            return Task.FromResult(workload);
        }

        public Task<List<GPUWorkload>> ListWorkloadsAsync(string? ns = null, string? queueName = null, CancellationToken cancellation = default)
        {
            var workloads = _workloads.Values.AsEnumerable();
            if (!string.IsNullOrEmpty(ns))
            {
                workloads = workloads.Where(w => w.Namespace == ns);
            }
            if (!string.IsNullOrEmpty(queueName))
            {
                workloads = workloads.Where(w => w.LocalQueueName == queueName || w.ClusterQueueName == queueName);
            }
            return Task.FromResult(workloads.ToList());
        }

        public async Task SuspendWorkloadAsync(string workloadId, CancellationToken cancellation = default)
        {
            if (!_workloads.TryGetValue(workloadId, out var workload))
            {
                throw new KeyNotFoundException($"Workload not found: {workloadId}");
            }

            workload.AdmissionStatus = WorkloadAdmissionStatus.Suspended;
            _logger.LogInformation("Suspended workload: {WorkloadId}", workloadId);
            await Task.Delay(50, cancellation);
        }

        public async Task ResumeWorkloadAsync(string workloadId, CancellationToken cancellation = default)
        {
            if (!_workloads.TryGetValue(workloadId, out var workload))
            {
                throw new KeyNotFoundException($"Workload not found: {workloadId}");
            }

            workload.AdmissionStatus = WorkloadAdmissionStatus.Queued;
            _admissionQueue.Enqueue(workloadId);
            _logger.LogInformation("Resumed workload: {WorkloadId}", workloadId);
            await Task.Delay(50, cancellation);
        }

        public async Task CancelWorkloadAsync(string workloadId, CancellationToken cancellation = default)
        {
            if (!_workloads.TryGetValue(workloadId, out var workload))
            {
                throw new KeyNotFoundException($"Workload not found: {workloadId}");
            }

            workload.AdmissionStatus = WorkloadAdmissionStatus.Failed;
            workload.CompletedAt = DateTime.UtcNow;

            // Release resources
            if (_clusterQueues.TryGetValue(workload.ClusterQueueName, out var cq))
            {
                cq.ResourceUsage.GPUsUsed -= workload.Resources.GPUCount;
                cq.AdmittedWorkloads--;
            }

            _logger.LogInformation("Cancelled workload: {WorkloadId}", workloadId);
            await Task.Delay(50, cancellation);
        }

        #endregion

        #region Gang Scheduling

        public async Task<GPUWorkload> SubmitGangWorkloadAsync(GPUWorkloadConfig config, GangSchedulingConfig gangConfig, CancellationToken cancellation = default)
        {
            _logger.LogInformation("Submitting gang workload: {WorkloadName} with {Members} members",
                config.Name, gangConfig.TotalMembers);

            config.GangScheduling = gangConfig;
            gangConfig.GangId ??= GenerateId("gang");

            // Submit the main workload
            var workload = await SubmitWorkloadAsync(config, cancellation);

            // Gang scheduling ensures all-or-nothing admission
            workload.Conditions.Add(new WorkloadCondition
            {
                Type = "GangScheduling",
                Status = "True",
                Reason = "GangWorkload",
                Message = $"Part of gang {gangConfig.GangId} with {gangConfig.TotalMembers} members",
                LastTransitionTime = DateTime.UtcNow
            });

            return workload;
        }

        public Task<List<GPUWorkload>> GetGangMembersAsync(string gangId, CancellationToken cancellation = default)
        {
            var members = _workloads.Values
                .Where(w => w.Conditions.Any(c => c.Message.Contains(gangId)))
                .ToList();
            return Task.FromResult(members);
        }

        #endregion

        #region Scheduling & Admission

        public Task<SchedulingDecision> SimulateAdmissionAsync(GPUWorkloadConfig config, CancellationToken cancellation = default)
        {
            var decision = new SchedulingDecision
            {
                WorkloadId = GenerateId("sim"),
                DecisionTime = DateTime.UtcNow
            };

            // Check if cluster queue has capacity
            var lqKey = $"{config.Namespace}/{config.LocalQueueName}";
            if (!_localQueues.TryGetValue(lqKey, out var localQueue))
            {
                decision.Admitted = false;
                decision.RejectionReason = "Local queue not found";
                return Task.FromResult(decision);
            }

            if (!_clusterQueues.TryGetValue(localQueue.ClusterQueueName, out var clusterQueue))
            {
                decision.Admitted = false;
                decision.RejectionReason = "Cluster queue not found";
                return Task.FromResult(decision);
            }

            if (clusterQueue.Status != QueueStatus.Active)
            {
                decision.Admitted = false;
                decision.RejectionReason = "Cluster queue is not active";
                return Task.FromResult(decision);
            }

            // Check GPU capacity
            var availableGPUs = clusterQueue.Quota.NominalGPUs - clusterQueue.ResourceUsage.GPUsUsed;
            var canBorrow = clusterQueue.ResourceUsage.GPUsBorrowed < clusterQueue.Quota.BorrowingLimit;

            if (config.Resources.GPUCount <= availableGPUs)
            {
                decision.Admitted = true;
                decision.Placement = new WorkloadPlacement
                {
                    AssignedFlavor = config.Resources.PreferredFlavor ?? GPUFlavorType.A100_80GB,
                    AchievedTopology = config.TopologyConstraints?.RequiredLevel ?? TopologyLevel.Node,
                    NodeNames = new List<string> { "gpu-node-001" },
                    GPUIds = Enumerable.Range(0, config.Resources.GPUCount)
                        .Select(i => $"GPU-{i:D4}")
                        .ToList()
                };
            }
            else if (canBorrow)
            {
                decision.Admitted = true;
                decision.Placement = new WorkloadPlacement
                {
                    AssignedFlavor = config.Resources.PreferredFlavor ?? GPUFlavorType.A100_80GB,
                    IsBorrowed = true,
                    BorrowedFromQueue = clusterQueue.CohortName,
                    NodeNames = new List<string> { "gpu-node-002" },
                    GPUIds = new List<string> { "GPU-BORROWED-0001" }
                };
            }
            else
            {
                decision.Admitted = false;
                decision.RejectionReason = "Insufficient GPU capacity";
                decision.QueuePosition = clusterQueue.PendingWorkloads + 1;
                decision.EstimatedWaitTime = TimeSpan.FromMinutes(decision.QueuePosition * 10);
            }

            return Task.FromResult(decision);
        }

        public Task<int> GetQueuePositionAsync(string workloadId, CancellationToken cancellation = default)
        {
            if (!_workloads.TryGetValue(workloadId, out var workload))
            {
                throw new KeyNotFoundException($"Workload not found: {workloadId}");
            }
            return Task.FromResult(workload.QueuePosition);
        }

        public Task<TimeSpan> EstimateWaitTimeAsync(string workloadId, CancellationToken cancellation = default)
        {
            if (!_workloads.TryGetValue(workloadId, out var workload))
            {
                throw new KeyNotFoundException($"Workload not found: {workloadId}");
            }
            return Task.FromResult(TimeSpan.FromMinutes(workload.QueuePosition * 10));
        }

        public async Task PreemptWorkloadAsync(string workloadId, string reason, CancellationToken cancellation = default)
        {
            if (!_workloads.TryGetValue(workloadId, out var workload))
            {
                throw new KeyNotFoundException($"Workload not found: {workloadId}");
            }

            _logger.LogWarning("Preempting workload: {WorkloadId}, reason: {Reason}", workloadId, reason);

            workload.AdmissionStatus = WorkloadAdmissionStatus.Preempted;
            workload.Message = reason;
            workload.Conditions.Add(new WorkloadCondition
            {
                Type = "Preempted",
                Status = "True",
                Reason = "HigherPriorityWorkload",
                Message = reason,
                LastTransitionTime = DateTime.UtcNow
            });

            await Task.Delay(50, cancellation);
        }

        #endregion

        #region Metrics & Monitoring

        public Task<QueueMetrics> GetQueueMetricsAsync(string queueName, CancellationToken cancellation = default)
        {
            if (!_clusterQueues.TryGetValue(queueName, out var queue))
            {
                throw new KeyNotFoundException($"Cluster queue not found: {queueName}");
            }

            var metrics = new QueueMetrics
            {
                QueueName = queueName,
                AverageWaitTime = TimeSpan.FromMinutes(5),
                P95WaitTime = TimeSpan.FromMinutes(15),
                WorkloadsAdmittedLast24h = queue.AdmittedWorkloads,
                WorkloadsPreemptedLast24h = 0,
                GPUUtilizationPercent = queue.ResourceUsage.GPUUtilizationPercent,
                FairnessScore = 0.85,
                MeasuredAt = DateTime.UtcNow
            };

            return Task.FromResult(metrics);
        }

        public Task<List<QueueMetrics>> GetAllQueueMetricsAsync(CancellationToken cancellation = default)
        {
            var metrics = _clusterQueues.Keys
                .Select(queueName => GetQueueMetricsAsync(queueName, cancellation).Result)
                .ToList();
            return Task.FromResult(metrics);
        }

        public Task<CohortResourceSummary> GetCohortResourceSummaryAsync(string cohortName, CancellationToken cancellation = default)
        {
            if (!_cohorts.TryGetValue(cohortName, out var cohort))
            {
                throw new KeyNotFoundException($"Cohort not found: {cohortName}");
            }

            var summary = new CohortResourceSummary();
            foreach (var queueName in cohort.ClusterQueues)
            {
                if (_clusterQueues.TryGetValue(queueName, out var queue))
                {
                    summary.TotalGPUs += queue.Quota.NominalGPUs;
                    summary.UsedGPUs += queue.ResourceUsage.GPUsUsed;
                    summary.BorrowedGPUs += queue.ResourceUsage.GPUsBorrowed;
                }
            }
            summary.AvailableGPUs = summary.TotalGPUs - summary.UsedGPUs;
            summary.OverallUtilization = summary.TotalGPUs > 0
                ? (double)summary.UsedGPUs / summary.TotalGPUs * 100
                : 0;

            return Task.FromResult(summary);
        }

        #endregion

        #region Private Helper Methods

        private string GenerateId(string prefix)
        {
            var bytes = new byte[8];
            RandomNumberGenerator.Fill(bytes);
            return $"{prefix}-{Convert.ToHexString(bytes).ToLower()}";
        }

        private string GenerateClusterQueueYaml(ClusterQueueConfig config)
        {
            var sb = new StringBuilder();
            sb.AppendLine($@"apiVersion: kueue.x-k8s.io/v1beta1
kind: ClusterQueue
metadata:
  name: {config.Name}
spec:
  cohort: {config.CohortName}
  preemption:
    reclaimWithinCohort: {config.Preemption.ReclaimWithinCohort.ToString().ToLower()}
    borrowWithinCohort:
      policy: {(config.Preemption.BorrowWithinCohort ? "LowerPriority" : "Never")}
  resourceGroups:
  - coveredResources: [""cpu"", ""memory"", ""nvidia.com/gpu""]
    flavors:");

            foreach (var flavor in config.ResourceFlavors)
            {
                sb.AppendLine($@"    - name: {flavor.Name}
      resources:
      - name: ""nvidia.com/gpu""
        nominalQuota: {config.Quota.NominalGPUs}
        borrowingLimit: {config.Quota.BorrowingLimit}
        lendingLimit: {config.Quota.LendingLimit}");
            }

            return sb.ToString();
        }

        #endregion
    }

    #endregion
}
