// ================================================================
// Loco - AI Platform
// AI Workload Scheduler Engine
//
// Implements KAITO, Kueue, Volcano patterns for AI/ML workload scheduling
// with GPU management, gang scheduling, and cost optimization.
//
// Patterns:
// - KAITO: CNCF Sandbox (2025), auto-provisioning, vLLM integration
// - Kueue: Gang scheduling, fast preemption, multi-cluster (MultiKueue)
// - Volcano: CNCF Incubating, dynamic MIG, network topology-aware
// - GPU Scheduling: MIG, time-slicing, DRA, Karpenter autoscaling
// - Cost Optimization: Spot instances (70-90% discount), elastic training
// - Elastic Training: 60% cost reduction, dynamic resource adjustment
//
// References:
// - Kueue 2025: Gang scheduling, 130K nodes (Google), MultiKueue
// - Volcano 2025: Dynamic MIG, network topology-aware, queue capacity
// - K8s 1.35 Alpha: Native gang scheduling (Workload Aware Scheduling)
// - Karpenter: Groupless autoscaling, spot/on-demand, real-time
// - Elastic Training: 60% cost savings, ML Goodput 80%→90% (Google A3)
// - Preferred Networks: Multi-GPU Kubernetes cluster operations (Japanese)
// ================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AIPlatform
{
    #region Core Interfaces

    /// <summary>
    /// Service for scheduling AI/ML workloads with GPU management and cost optimization
    /// </summary>
    public interface IAIWorkloadSchedulerEngine
    {
        // Workload Operations
        Task<AIWorkload> SubmitWorkloadAsync(string tenantId, AIWorkload workload, CancellationToken cancellation = default);
        Task<AIWorkload> GetWorkloadAsync(string tenantId, string workloadId, CancellationToken cancellation = default);
        Task<List<AIWorkload>> ListWorkloadsAsync(string tenantId, string? queueName = null, CancellationToken cancellation = default);
        Task<AIWorkload> CancelWorkloadAsync(string tenantId, string workloadId, CancellationToken cancellation = default);

        // Queue Management (Kueue-style)
        Task<WorkloadQueue> CreateQueueAsync(string tenantId, WorkloadQueue queue, CancellationToken cancellation = default);
        Task<List<WorkloadQueue>> ListQueuesAsync(string tenantId, CancellationToken cancellation = default);
        Task<QueueStatus> GetQueueStatusAsync(string tenantId, string queueName, CancellationToken cancellation = default);

        // Resource Management
        Task<ResourceQuota> SetResourceQuotaAsync(string tenantId, string queueName, ResourceQuota quota, CancellationToken cancellation = default);
        Task<List<GPUNode>> ListGPUNodesAsync(string tenantId, CancellationToken cancellation = default);
        Task<GPUAllocation> AllocateGPUsAsync(string tenantId, GPURequest request, CancellationToken cancellation = default);

        // Scheduling Policies
        Task<SchedulingPolicy> SetPolicyAsync(string tenantId, SchedulingPolicy policy, CancellationToken cancellation = default);
        Task<PreemptionResult> PreemptWorkloadAsync(string tenantId, string workloadId, CancellationToken cancellation = default);

        // Auto-scaling
        Task<AutoscalerConfig> ConfigureAutoscalerAsync(string tenantId, AutoscalerConfig config, CancellationToken cancellation = default);
        Task<ScalingDecision> GetScalingRecommendationAsync(string tenantId, CancellationToken cancellation = default);

        // Cost Optimization
        Task<CostOptimizationReport> GenerateCostReportAsync(string tenantId, TimeSpan duration, CancellationToken cancellation = default);
        Task<SpotInstanceConfig> ConfigureSpotInstancesAsync(string tenantId, SpotInstanceConfig config, CancellationToken cancellation = default);

        // Analytics
        Task<SchedulerMetrics> GetMetricsAsync(string tenantId, TimeSpan duration, CancellationToken cancellation = default);
    }

    #endregion

    #region Workload Models

    public class AIWorkload
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string QueueName { get; set; } = "default";
        public WorkloadType Type { get; set; }

        public WorkloadSpec Spec { get; set; } = new();
        public ResourceRequirements Resources { get; set; } = new();
        public SchedulingConstraints Constraints { get; set; } = new();

        public WorkloadStatus Status { get; set; } = new();
        public WorkloadPriority Priority { get; set; } = new();

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public string SubmittedBy { get; set; } = string.Empty;
        public Dictionary<string, string> Labels { get; set; } = new();
        public Dictionary<string, string> Annotations { get; set; } = new();
    }

    public enum WorkloadType
    {
        Training,           // Distributed training
        FineTuning,        // Model fine-tuning
        Inference,         // Batch inference
        Serving,           // Model serving
        Experiment,        // Experimentation
        DataProcessing     // Data pipeline
    }

    public class WorkloadSpec
    {
        public string Image { get; set; } = string.Empty;
        public List<string> Command { get; set; } = new();
        public List<string> Args { get; set; } = new();
        public Dictionary<string, string> Environment { get; set; } = new();

        // Distributed training spec
        public DistributedTrainingSpec? DistributedTraining { get; set; }

        // Gang scheduling
        public GangSchedulingSpec? GangScheduling { get; set; }

        // Elastic training
        public ElasticTrainingSpec? ElasticTraining { get; set; }
    }

    public class DistributedTrainingSpec
    {
        public DistributedFramework Framework { get; set; }
        public int Workers { get; set; } = 1;
        public int? ParameterServers { get; set; }
        public ParallelismStrategy Parallelism { get; set; } = new();
    }

    public enum DistributedFramework
    {
        PyTorchDDP,
        TensorFlowMirroredStrategy,
        Horovod,
        DeepSpeed,
        MegatronLM,
        Ray
    }

    public class ParallelismStrategy
    {
        public int DataParallel { get; set; } = 1;
        public int TensorParallel { get; set; } = 1;
        public int PipelineParallel { get; set; } = 1;
    }

    public class GangSchedulingSpec
    {
        public bool Enabled { get; set; } = true;
        public int MinMembers { get; set; } = 1;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);
        public GangSchedulingPolicy Policy { get; set; } = GangSchedulingPolicy.AllOrNothing;
    }

    public enum GangSchedulingPolicy
    {
        AllOrNothing,      // All pods must be scheduled together
        BestEffort,        // Try to schedule all, but allow partial
        MinMembers         // At least minMembers must be scheduled
    }

    public class ElasticTrainingSpec
    {
        public bool Enabled { get; set; } = false;
        public int MinWorkers { get; set; } = 1;
        public int MaxWorkers { get; set; } = 10;
        public bool EnableSpotInstances { get; set; } = true;
        public CheckpointingConfig Checkpointing { get; set; } = new();
    }

    public class CheckpointingConfig
    {
        public bool Enabled { get; set; } = true;
        public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(10);
        public string StorageLocation { get; set; } = string.Empty;
        public int MaxCheckpoints { get; set; } = 3;
    }

    public class ResourceRequirements
    {
        public ComputeResources Requests { get; set; } = new();
        public ComputeResources? Limits { get; set; }
        public GPURequirements GPU { get; set; } = new();
    }

    public class ComputeResources
    {
        public int CPUCores { get; set; }
        public int MemoryGB { get; set; }
        public int? StorageGB { get; set; }
    }

    public class GPURequirements
    {
        public int Count { get; set; } = 1;
        public List<string>? PreferredTypes { get; set; } // H100, A100, V100, etc.
        public GPUSharingMode SharingMode { get; set; } = GPUSharingMode.Exclusive;
        public MIGProfile? MIGProfile { get; set; }
        public int? GPUMemoryGB { get; set; }
    }

    public enum GPUSharingMode
    {
        Exclusive,         // Dedicated GPU
        TimeSlicing,       // Time-sliced sharing
        MIG,               // Multi-Instance GPU
        MPS                // NVIDIA Multi-Process Service
    }

    public class MIGProfile
    {
        public string Profile { get; set; } = string.Empty; // 1g.5gb, 2g.10gb, etc.
        public int Instances { get; set; } = 1;
    }

    public class SchedulingConstraints
    {
        public NodeAffinity? NodeAffinity { get; set; }
        public List<Toleration>? Tolerations { get; set; }
        public TopologySpreadConstraints? TopologySpread { get; set; }
        public bool RequireSpotInstances { get; set; } = false;
        public bool AllowPreemption { get; set; } = true;
    }

    public class NodeAffinity
    {
        public Dictionary<string, string> RequiredLabels { get; set; } = new();
        public Dictionary<string, string> PreferredLabels { get; set; } = new();
        public List<string>? AvailabilityZones { get; set; }
    }

    public class Toleration
    {
        public string Key { get; set; } = string.Empty;
        public string Operator { get; set; } = "Equal"; // Equal, Exists
        public string? Value { get; set; }
        public string Effect { get; set; } = "NoSchedule";
    }

    public class TopologySpreadConstraints
    {
        public string TopologyKey { get; set; } = "topology.kubernetes.io/zone";
        public int MaxSkew { get; set; } = 1;
        public string WhenUnsatisfiable { get; set; } = "ScheduleAnyway";
    }

    public class WorkloadStatus
    {
        public WorkloadState State { get; set; } = WorkloadState.Pending;
        public string? Message { get; set; }
        public List<string> Conditions { get; set; } = new();
        public WorkloadPlacement? Placement { get; set; }
        public bool IsPreempted { get; set; }
        public int PreemptionCount { get; set; }
    }

    public enum WorkloadState
    {
        Pending,           // Waiting in queue
        Admitted,          // Admitted by scheduler
        Provisioning,      // Resources being provisioned
        Running,           // Running
        Succeeded,         // Completed successfully
        Failed,            // Failed
        Preempted,         // Preempted by higher priority
        Canceled           // Canceled by user
    }

    public class WorkloadPlacement
    {
        public List<string> NodeNames { get; set; } = new();
        public List<GPUAllocation> GPUAllocations { get; set; } = new();
        public string ClusterId { get; set; } = string.Empty;
        public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
    }

    public class WorkloadPriority
    {
        public int PriorityClass { get; set; } = 0; // 0 (lowest) to 1000 (highest)
        public string PriorityClassName { get; set; } = "default";
        public bool Preemptible { get; set; } = true;
        public TimeSpan? MaxWaitTime { get; set; }
    }

    #endregion

    #region Queue Models

    public class WorkloadQueue
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public QueueConfig Config { get; set; } = new();
        public ResourceQuota Quota { get; set; } = new();
        public QueueStatus Status { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Dictionary<string, string> Labels { get; set; } = new();
    }

    public class QueueConfig
    {
        public QueueingStrategy Strategy { get; set; } = QueueingStrategy.StrictFIFO;
        public bool EnablePreemption { get; set; } = true;
        public bool EnableFairSharing { get; set; } = true;
        public int? MaxConcurrentWorkloads { get; set; }
        public TimeSpan? MaxWaitTime { get; set; }
    }

    public enum QueueingStrategy
    {
        StrictFIFO,        // Strict first-in-first-out
        BestEffortFIFO,    // FIFO with resource availability
        PriorityBased,     // Priority-based scheduling
        FairShare          // Fair sharing across users/teams
    }

    public class ResourceQuota
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string QueueName { get; set; } = string.Empty;

        public ComputeQuota Compute { get; set; } = new();
        public GPUQuota GPU { get; set; } = new();

        public bool EnforceStrictQuota { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ComputeQuota
    {
        public int? MaxCPUCores { get; set; }
        public int? MaxMemoryGB { get; set; }
        public int? MaxConcurrentWorkloads { get; set; }
    }

    public class GPUQuota
    {
        public int? MaxGPUs { get; set; }
        public Dictionary<string, int>? MaxGPUsByType { get; set; } // "H100": 10, "A100": 20
        public int? MaxGPUHours { get; set; }
    }

    public class QueueStatus
    {
        public int PendingWorkloads { get; set; }
        public int RunningWorkloads { get; set; }
        public ResourceUsage CurrentUsage { get; set; } = new();
        public ResourceUsage QuotaUtilization { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class ResourceUsage
    {
        public int CPUCores { get; set; }
        public int MemoryGB { get; set; }
        public int GPUs { get; set; }
        public Dictionary<string, int> GPUsByType { get; set; } = new();
        public double UtilizationPercent { get; set; }
    }

    #endregion

    #region GPU Management Models

    public class GPUNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string ClusterId { get; set; } = string.Empty;

        public NodeType Type { get; set; }
        public List<GPU> GPUs { get; set; } = new();
        public NodeCapacity Capacity { get; set; } = new();
        public NodeStatus Status { get; set; } = new();

        public Dictionary<string, string> Labels { get; set; } = new();
    }

    public enum NodeType
    {
        OnDemand,
        Spot,
        Reserved,
        Preemptible
    }

    public class GPU
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Model { get; set; } = string.Empty; // H100, A100, V100
        public int MemoryGB { get; set; }
        public GPUState State { get; set; } = GPUState.Available;

        // MIG configuration
        public MIGConfig? MIGConfig { get; set; }

        // Current allocations
        public List<GPUAllocation> Allocations { get; set; } = new();
    }

    public enum GPUState
    {
        Available,
        InUse,
        Reserved,
        Maintenance,
        Failed
    }

    public class MIGConfig
    {
        public bool Enabled { get; set; }
        public List<MIGInstance> Instances { get; set; } = new();
    }

    public class MIGInstance
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Profile { get; set; } = string.Empty; // 1g.5gb, 2g.10gb, 3g.20gb, etc.
        public int GPUSlice { get; set; }
        public int MemoryGB { get; set; }
        public bool IsAllocated { get; set; }
    }

    public class GPUAllocation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string WorkloadId { get; set; } = string.Empty;
        public string GPUId { get; set; } = string.Empty;
        public GPUSharingMode Mode { get; set; }
        public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
        public TimeSpan? Duration { get; set; }
    }

    public class NodeCapacity
    {
        public int CPUCores { get; set; }
        public int MemoryGB { get; set; }
        public int TotalGPUs { get; set; }
        public int AvailableGPUs { get; set; }
    }

    public class NodeStatus
    {
        public string State { get; set; } = "Ready";
        public double CPUUtilization { get; set; }
        public double MemoryUtilization { get; set; }
        public double GPUUtilization { get; set; }
        public bool IsSpotInstance { get; set; }
        public DateTime? SpotTerminationTime { get; set; }
    }

    public class GPURequest
    {
        public string WorkloadId { get; set; } = string.Empty;
        public int Count { get; set; } = 1;
        public List<string>? PreferredTypes { get; set; }
        public GPUSharingMode SharingMode { get; set; } = GPUSharingMode.Exclusive;
        public TimeSpan? Duration { get; set; }
    }

    #endregion

    #region Scheduling Policy Models

    public class SchedulingPolicy
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;

        public SchedulingStrategy Strategy { get; set; } = SchedulingStrategy.BinPacking;
        public PreemptionPolicy Preemption { get; set; } = new();
        public GangSchedulingPolicy GangPolicy { get; set; }
        public FairSharingPolicy? FairSharing { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum SchedulingStrategy
    {
        BinPacking,        // Pack workloads tightly (cost optimization)
        Spreading,         // Spread workloads (high availability)
        Balanced,          // Balance between packing and spreading
        TopologyAware      // Consider network topology (Volcano 2025)
    }

    public class PreemptionPolicy
    {
        public bool Enabled { get; set; } = true;
        public PreemptionStrategy Strategy { get; set; } = PreemptionStrategy.LowestPriority;
        public int MinPriorityGap { get; set; } = 100; // Min priority difference for preemption
        public TimeSpan GracePeriod { get; set; } = TimeSpan.FromMinutes(5);
    }

    public enum PreemptionStrategy
    {
        LowestPriority,    // Preempt lowest priority workloads
        LeastImpact,       // Preempt workloads with least impact
        RecentlyStarted,   // Preempt recently started workloads
        Custom
    }

    public class FairSharingPolicy
    {
        public bool Enabled { get; set; } = true;
        public FairShareUnit Unit { get; set; } = FairShareUnit.GPU;
        public Dictionary<string, double> Weights { get; set; } = new(); // user/team -> weight
    }

    public enum FairShareUnit
    {
        CPU,
        Memory,
        GPU,
        Cost
    }

    public class PreemptionResult
    {
        public string WorkloadId { get; set; } = string.Empty;
        public bool WasPreempted { get; set; }
        public string? Reason { get; set; }
        public List<string> PreemptedBy { get; set; } = new();
        public DateTime PreemptedAt { get; set; }
    }

    #endregion

    #region Auto-scaling Models

    public class AutoscalerConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public bool Enabled { get; set; } = true;

        public AutoscalerType Type { get; set; } = AutoscalerType.Karpenter;
        public ScalingBounds Bounds { get; set; } = new();
        public ScalingTriggers Triggers { get; set; } = new();

        // Spot instance configuration
        public bool EnableSpotInstances { get; set; } = true;
        public double SpotInstancePercentage { get; set; } = 0.7; // 70% spot, 30% on-demand

        // Elastic training support
        public bool EnableElasticScaling { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum AutoscalerType
    {
        ClusterAutoscaler,  // Traditional K8s CA
        Karpenter,          // Groupless autoscaling (recommended for GPU)
        Volcano,            // Volcano autoscaler
        Custom
    }

    public class ScalingBounds
    {
        public int MinNodes { get; set; } = 0;
        public int MaxNodes { get; set; } = 100;
        public int MinGPUs { get; set; } = 0;
        public int MaxGPUs { get; set; } = 1000;
    }

    public class ScalingTriggers
    {
        public double TargetCPUUtilization { get; set; } = 0.7;
        public double TargetGPUUtilization { get; set; } = 0.8;
        public int QueueDepthThreshold { get; set; } = 10;
        public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromMinutes(10);
    }

    public class ScalingDecision
    {
        public bool ShouldScaleUp { get; set; }
        public bool ShouldScaleDown { get; set; }
        public int RecommendedNodes { get; set; }
        public List<NodeRecommendation> NodeRecommendations { get; set; } = new();
        public string Reason { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    public class NodeRecommendation
    {
        public string InstanceType { get; set; } = string.Empty;
        public int Count { get; set; }
        public NodeType Type { get; set; }
        public double EstimatedCostPerHour { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    #endregion

    #region Cost Optimization Models

    public class CostOptimizationReport
    {
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public TimeSpan Period { get; set; }

        public CostSummary Summary { get; set; } = new();
        public List<CostBreakdown> Breakdowns { get; set; } = new();
        public List<CostOptimizationRecommendation> Recommendations { get; set; } = new();
    }

    public class CostSummary
    {
        public double TotalCost { get; set; }
        public double ComputeCost { get; set; }
        public double GPUCost { get; set; }
        public double StorageCost { get; set; }

        public double OnDemandCost { get; set; }
        public double SpotCost { get; set; }
        public double SpotSavings { get; set; }
        public double SpotSavingsPercent { get; set; }

        public ResourceEfficiency Efficiency { get; set; } = new();
    }

    public class CostBreakdown
    {
        public string QueueName { get; set; } = string.Empty;
        public double Cost { get; set; }
        public int WorkloadCount { get; set; }
        public double GPUHours { get; set; }
        public double CPUHours { get; set; }
    }

    public class ResourceEfficiency
    {
        public double CPUUtilization { get; set; }
        public double MemoryUtilization { get; set; }
        public double GPUUtilization { get; set; }
        public double WastedCost { get; set; }
    }

    public class CostOptimizationRecommendation
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double EstimatedSavings { get; set; }
        public double EstimatedSavingsPercent { get; set; }
        public int Priority { get; set; } // 1-10
        public string Category { get; set; } = string.Empty;
    }

    public class SpotInstanceConfig
    {
        public bool Enabled { get; set; } = true;
        public double TargetPercentage { get; set; } = 0.7; // 70% spot
        public List<string> AllowedInstanceTypes { get; set; } = new();
        public List<string> PreferredAvailabilityZones { get; set; } = new();

        public SpotInterruptionHandling InterruptionHandling { get; set; } = new();
    }

    public class SpotInterruptionHandling
    {
        public bool EnableCheckpointing { get; set; } = true;
        public bool EnableAutomaticRescheduling { get; set; } = true;
        public TimeSpan GracePeriod { get; set; } = TimeSpan.FromMinutes(2);
        public int MaxRetries { get; set; } = 3;
    }

    #endregion

    #region Metrics Models

    public class SchedulerMetrics
    {
        public TimeSpan Period { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public WorkloadMetrics Workloads { get; set; } = new();
        public SchedulingEfficiency Efficiency { get; set; } = new();
        public ResourceUtilizationMetrics Utilization { get; set; } = new();
        public CostMetrics Costs { get; set; } = new();
    }

    public class WorkloadMetrics
    {
        public int TotalSubmitted { get; set; }
        public int TotalCompleted { get; set; }
        public int TotalFailed { get; set; }
        public int TotalPreempted { get; set; }

        public TimeSpan AverageWaitTime { get; set; }
        public TimeSpan AverageRunTime { get; set; }
        public TimeSpan P95WaitTime { get; set; }

        public double SuccessRate { get; set; }
    }

    public class SchedulingEfficiency
    {
        public double SchedulingLatency { get; set; } // ms
        public double GangSchedulingSuccessRate { get; set; }
        public int PreemptionCount { get; set; }
        public double PreemptionLatency { get; set; } // ms (Kueue: near-instantaneous)

        public double ResourceFragmentation { get; set; }
        public double BinPackingEfficiency { get; set; }
    }

    public class ResourceUtilizationMetrics
    {
        public double CPUUtilization { get; set; }
        public double MemoryUtilization { get; set; }
        public double GPUUtilization { get; set; }
        public double GPUMemoryUtilization { get; set; }

        public Dictionary<string, double> GPUUtilizationByType { get; set; } = new();
    }

    public class CostMetrics
    {
        public double TotalCost { get; set; }
        public double CostPerGPUHour { get; set; }
        public double SpotSavings { get; set; }
        public double WastedCost { get; set; }
    }

    #endregion

    #region Implementation

    public class AIWorkloadSchedulerEngine : IAIWorkloadSchedulerEngine
    {
        private readonly ILogger<AIWorkloadSchedulerEngine> _logger;

        private readonly Dictionary<string, List<AIWorkload>> _workloads = new();
        private readonly Dictionary<string, List<WorkloadQueue>> _queues = new();
        private readonly Dictionary<string, List<GPUNode>> _gpuNodes = new();
        private readonly Dictionary<string, SchedulingPolicy> _policies = new();
        private readonly Dictionary<string, AutoscalerConfig> _autoscalerConfigs = new();

        public AIWorkloadSchedulerEngine(ILogger<AIWorkloadSchedulerEngine> logger)
        {
            _logger = logger;
        }

        #region Workload Operations

        public async Task<AIWorkload> SubmitWorkloadAsync(
            string tenantId,
            AIWorkload workload,
            CancellationToken cancellation = default)
        {
            _logger.LogInformation(
                "Submitting {Type} workload {Name} to queue {Queue} with {GPUs} GPUs, priority {Priority}",
                workload.Type, workload.Name, workload.QueueName,
                workload.Resources.GPU.Count, workload.Priority.PriorityClass);

            // Validate workload
            ValidateWorkload(workload);

            // Initialize status
            workload.Status = new WorkloadStatus
            {
                State = WorkloadState.Pending,
                Conditions = new List<string> { "Submitted" }
            };

            // Store workload
            if (!_workloads.ContainsKey(tenantId))
                _workloads[tenantId] = new List<AIWorkload>();

            _workloads[tenantId].Add(workload);

            // Log scheduling requirements
            if (workload.Spec.GangScheduling?.Enabled == true)
            {
                _logger.LogInformation(
                    "Gang scheduling enabled: minMembers={Min}, timeout={Timeout}",
                    workload.Spec.GangScheduling.MinMembers,
                    workload.Spec.GangScheduling.Timeout);
            }

            if (workload.Spec.ElasticTraining?.Enabled == true)
            {
                _logger.LogInformation(
                    "Elastic training enabled: workers={Min}-{Max}, spot={Spot}",
                    workload.Spec.ElasticTraining.MinWorkers,
                    workload.Spec.ElasticTraining.MaxWorkers,
                    workload.Spec.ElasticTraining.EnableSpotInstances);
            }

            // Simulate scheduling (in production, this would interact with Kueue/Volcano)
            await ScheduleWorkloadAsync(tenantId, workload, cancellation);

            return workload;
        }

        public async Task<AIWorkload> GetWorkloadAsync(
            string tenantId,
            string workloadId,
            CancellationToken cancellation = default)
        {
            if (!_workloads.TryGetValue(tenantId, out var workloads))
                throw new KeyNotFoundException($"No workloads found for tenant {tenantId}");

            var workload = workloads.FirstOrDefault(w => w.Id == workloadId);
            if (workload == null)
                throw new KeyNotFoundException($"Workload {workloadId} not found");

            return await Task.FromResult(workload);
        }

        public async Task<List<AIWorkload>> ListWorkloadsAsync(
            string tenantId,
            string? queueName = null,
            CancellationToken cancellation = default)
        {
            if (!_workloads.TryGetValue(tenantId, out var workloads))
                return new List<AIWorkload>();

            var filtered = queueName == null
                ? workloads
                : workloads.Where(w => w.QueueName == queueName).ToList();

            return await Task.FromResult(filtered);
        }

        public async Task<AIWorkload> CancelWorkloadAsync(
            string tenantId,
            string workloadId,
            CancellationToken cancellation = default)
        {
            var workload = await GetWorkloadAsync(tenantId, workloadId, cancellation);

            workload.Status.State = WorkloadState.Canceled;
            workload.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Workload {Name} canceled", workload.Name);

            return workload;
        }

        private void ValidateWorkload(AIWorkload workload)
        {
            if (workload.Resources.GPU.Count < 1)
                throw new ArgumentException("GPU count must be at least 1");

            if (workload.Spec.GangScheduling?.Enabled == true)
            {
                var minMembers = workload.Spec.GangScheduling.MinMembers;
                if (workload.Spec.DistributedTraining != null &&
                    workload.Spec.DistributedTraining.Workers < minMembers)
                {
                    throw new ArgumentException(
                        $"Workers ({workload.Spec.DistributedTraining.Workers}) must be >= minMembers ({minMembers})");
                }
            }
        }

        private async Task ScheduleWorkloadAsync(
            string tenantId,
            AIWorkload workload,
            CancellationToken cancellation)
        {
            // Simulate scheduling delay
            await Task.Delay(new Random().Next(100, 500), cancellation);

            // Check resource availability
            var nodesAvailable = CheckResourceAvailability(tenantId, workload);

            if (nodesAvailable)
            {
                workload.Status.State = WorkloadState.Admitted;
                workload.Status.Conditions.Add("Admitted by scheduler");

                // Allocate GPUs
                var gpuAllocation = await AllocateGPUsAsync(
                    tenantId,
                    new GPURequest
                    {
                        WorkloadId = workload.Id,
                        Count = workload.Resources.GPU.Count,
                        PreferredTypes = workload.Resources.GPU.PreferredTypes,
                        SharingMode = workload.Resources.GPU.SharingMode
                    },
                    cancellation);

                workload.Status.Placement = new WorkloadPlacement
                {
                    GPUAllocations = new List<GPUAllocation> { gpuAllocation },
                    PlacedAt = DateTime.UtcNow
                };

                // Start workload
                workload.Status.State = WorkloadState.Running;
                workload.StartedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Workload {Name} scheduled and running on {GPUCount} GPUs",
                    workload.Name, gpuAllocation.Id);
            }
            else
            {
                _logger.LogWarning(
                    "Workload {Name} pending: insufficient resources",
                    workload.Name);
            }
        }

        private bool CheckResourceAvailability(string tenantId, AIWorkload workload)
        {
            // Simplified resource check
            return new Random().Next(0, 100) < 80; // 80% admission rate
        }

        #endregion

        #region Queue Management

        public async Task<WorkloadQueue> CreateQueueAsync(
            string tenantId,
            WorkloadQueue queue,
            CancellationToken cancellation = default)
        {
            _logger.LogInformation(
                "Creating queue {Name} with strategy {Strategy}, preemption={Preemption}",
                queue.Name, queue.Config.Strategy, queue.Config.EnablePreemption);

            if (!_queues.ContainsKey(tenantId))
                _queues[tenantId] = new List<WorkloadQueue>();

            _queues[tenantId].Add(queue);

            return await Task.FromResult(queue);
        }

        public async Task<List<WorkloadQueue>> ListQueuesAsync(
            string tenantId,
            CancellationToken cancellation = default)
        {
            if (!_queues.TryGetValue(tenantId, out var queues))
                return new List<WorkloadQueue>();

            return await Task.FromResult(queues);
        }

        public async Task<QueueStatus> GetQueueStatusAsync(
            string tenantId,
            string queueName,
            CancellationToken cancellation = default)
        {
            var workloads = await ListWorkloadsAsync(tenantId, queueName, cancellation);

            var status = new QueueStatus
            {
                PendingWorkloads = workloads.Count(w => w.Status.State == WorkloadState.Pending),
                RunningWorkloads = workloads.Count(w => w.Status.State == WorkloadState.Running),
                CurrentUsage = CalculateResourceUsage(workloads.Where(w => w.Status.State == WorkloadState.Running).ToList()),
                LastUpdated = DateTime.UtcNow
            };

            return await Task.FromResult(status);
        }

        private ResourceUsage CalculateResourceUsage(List<AIWorkload> workloads)
        {
            return new ResourceUsage
            {
                CPUCores = workloads.Sum(w => w.Resources.Requests.CPUCores),
                MemoryGB = workloads.Sum(w => w.Resources.Requests.MemoryGB),
                GPUs = workloads.Sum(w => w.Resources.GPU.Count),
                UtilizationPercent = workloads.Count > 0 ? new Random().Next(60, 95) : 0
            };
        }

        #endregion

        #region Resource Management

        public async Task<ResourceQuota> SetResourceQuotaAsync(
            string tenantId,
            string queueName,
            ResourceQuota quota,
            CancellationToken cancellation = default)
        {
            _logger.LogInformation(
                "Setting resource quota for queue {Queue}: CPUs={CPU}, Memory={Memory}GB, GPUs={GPU}",
                queueName,
                quota.Compute.MaxCPUCores,
                quota.Compute.MaxMemoryGB,
                quota.GPU.MaxGPUs);

            quota.QueueName = queueName;
            quota.UpdatedAt = DateTime.UtcNow;

            return await Task.FromResult(quota);
        }

        public async Task<List<GPUNode>> ListGPUNodesAsync(
            string tenantId,
            CancellationToken cancellation = default)
        {
            if (!_gpuNodes.TryGetValue(tenantId, out var nodes))
            {
                // Generate sample GPU nodes
                nodes = GenerateSampleGPUNodes();
                _gpuNodes[tenantId] = nodes;
            }

            return await Task.FromResult(nodes);
        }

        public async Task<GPUAllocation> AllocateGPUsAsync(
            string tenantId,
            GPURequest request,
            CancellationToken cancellation = default)
        {
            _logger.LogInformation(
                "Allocating {Count} GPUs with mode {Mode} for workload {WorkloadId}",
                request.Count, request.SharingMode, request.WorkloadId);

            var allocation = new GPUAllocation
            {
                WorkloadId = request.WorkloadId,
                GPUId = Guid.NewGuid().ToString(),
                Mode = request.SharingMode,
                AllocatedAt = DateTime.UtcNow,
                Duration = request.Duration
            };

            return await Task.FromResult(allocation);
        }

        private List<GPUNode> GenerateSampleGPUNodes()
        {
            var random = new Random();
            var nodes = new List<GPUNode>();

            for (int i = 0; i < 10; i++)
            {
                var gpuModel = new[] { "H100", "A100", "V100" }[random.Next(3)];
                var gpuCount = new[] { 4, 8 }[random.Next(2)];
                var isSpot = random.Next(0, 100) < 70; // 70% spot

                var node = new GPUNode
                {
                    Name = $"gpu-node-{i}",
                    Type = isSpot ? NodeType.Spot : NodeType.OnDemand,
                    Capacity = new NodeCapacity
                    {
                        CPUCores = 96,
                        MemoryGB = 768,
                        TotalGPUs = gpuCount,
                        AvailableGPUs = random.Next(0, gpuCount + 1)
                    },
                    Status = new NodeStatus
                    {
                        State = "Ready",
                        CPUUtilization = random.Next(30, 80),
                        MemoryUtilization = random.Next(40, 85),
                        GPUUtilization = random.Next(50, 95),
                        IsSpotInstance = isSpot
                    }
                };

                for (int j = 0; j < gpuCount; j++)
                {
                    node.GPUs.Add(new GPU
                    {
                        Model = gpuModel,
                        MemoryGB = gpuModel == "H100" ? 80 : gpuModel == "A100" ? 80 : 16,
                        State = random.Next(0, 100) < 70 ? GPUState.InUse : GPUState.Available
                    });
                }

                nodes.Add(node);
            }

            return nodes;
        }

        #endregion

        #region Scheduling Policies

        public async Task<SchedulingPolicy> SetPolicyAsync(
            string tenantId,
            SchedulingPolicy policy,
            CancellationToken cancellation = default)
        {
            _logger.LogInformation(
                "Setting scheduling policy {Name}: strategy={Strategy}, preemption={Preemption}",
                policy.Name, policy.Strategy, policy.Preemption.Enabled);

            _policies[tenantId] = policy;

            return await Task.FromResult(policy);
        }

        public async Task<PreemptionResult> PreemptWorkloadAsync(
            string tenantId,
            string workloadId,
            CancellationToken cancellation = default)
        {
            var workload = await GetWorkloadAsync(tenantId, workloadId, cancellation);

            workload.Status.State = WorkloadState.Preempted;
            workload.Status.IsPreempted = true;
            workload.Status.PreemptionCount++;

            _logger.LogInformation(
                "Workload {Name} preempted (count: {Count})",
                workload.Name, workload.Status.PreemptionCount);

            return new PreemptionResult
            {
                WorkloadId = workloadId,
                WasPreempted = true,
                Reason = "Preempted by higher priority workload",
                PreemptedAt = DateTime.UtcNow
            };
        }

        #endregion

        #region Auto-scaling

        public async Task<AutoscalerConfig> ConfigureAutoscalerAsync(
            string tenantId,
            AutoscalerConfig config,
            CancellationToken cancellation = default)
        {
            _logger.LogInformation(
                "Configuring {Type} autoscaler: nodes={Min}-{Max}, spot={Spot}%",
                config.Type,
                config.Bounds.MinNodes,
                config.Bounds.MaxNodes,
                config.SpotInstancePercentage * 100);

            _autoscalerConfigs[tenantId] = config;

            return await Task.FromResult(config);
        }

        public async Task<ScalingDecision> GetScalingRecommendationAsync(
            string tenantId,
            CancellationToken cancellation = default)
        {
            var workloads = await ListWorkloadsAsync(tenantId, null, cancellation);
            var pendingCount = workloads.Count(w => w.Status.State == WorkloadState.Pending);

            var decision = new ScalingDecision
            {
                ShouldScaleUp = pendingCount > 5,
                ShouldScaleDown = pendingCount == 0,
                RecommendedNodes = Math.Max(1, pendingCount / 4),
                Reason = pendingCount > 5
                    ? $"{pendingCount} workloads pending, recommend scaling up"
                    : "No pending workloads, consider scaling down",
                GeneratedAt = DateTime.UtcNow
            };

            if (decision.ShouldScaleUp)
            {
                decision.NodeRecommendations.Add(new NodeRecommendation
                {
                    InstanceType = "p4d.24xlarge",
                    Count = decision.RecommendedNodes,
                    Type = NodeType.Spot,
                    EstimatedCostPerHour = 7.50, // 70% discount
                    Reason = "Spot instance for cost savings"
                });
            }

            return await Task.FromResult(decision);
        }

        #endregion

        #region Cost Optimization

        public async Task<CostOptimizationReport> GenerateCostReportAsync(
            string tenantId,
            TimeSpan duration,
            CancellationToken cancellation = default)
        {
            _logger.LogInformation(
                "Generating cost optimization report for period {Duration}",
                duration);

            var workloads = await ListWorkloadsAsync(tenantId, null, cancellation);
            var nodes = await ListGPUNodesAsync(tenantId, cancellation);

            var report = new CostOptimizationReport
            {
                GeneratedAt = DateTime.UtcNow,
                Period = duration,
                Summary = CalculateCostSummary(workloads, nodes, duration),
                Recommendations = GenerateCostRecommendations(workloads, nodes)
            };

            _logger.LogInformation(
                "Cost report: Total=${Total:F2}, Spot savings=${Savings:F2} ({Percent:F1}%)",
                report.Summary.TotalCost,
                report.Summary.SpotSavings,
                report.Summary.SpotSavingsPercent);

            return report;
        }

        public async Task<SpotInstanceConfig> ConfigureSpotInstancesAsync(
            string tenantId,
            SpotInstanceConfig config,
            CancellationToken cancellation = default)
        {
            _logger.LogInformation(
                "Configuring spot instances: enabled={Enabled}, target={Target}%, checkpointing={Checkpoint}",
                config.Enabled,
                config.TargetPercentage * 100,
                config.InterruptionHandling.EnableCheckpointing);

            return await Task.FromResult(config);
        }

        private CostSummary CalculateCostSummary(
            List<AIWorkload> workloads,
            List<GPUNode> nodes,
            TimeSpan duration)
        {
            var random = new Random();
            var spotNodes = nodes.Count(n => n.Type == NodeType.Spot);
            var onDemandNodes = nodes.Count - spotNodes;

            var spotCostPerHour = 7.50; // H100 spot ~$7.50/hr
            var onDemandCostPerHour = 25.00; // H100 on-demand ~$25/hr

            var hours = duration.TotalHours;
            var spotCost = spotNodes * spotCostPerHour * hours;
            var onDemandCost = onDemandNodes * onDemandCostPerHour * hours;
            var totalCost = spotCost + onDemandCost;

            var spotSavings = spotNodes * (onDemandCostPerHour - spotCostPerHour) * hours;

            return new CostSummary
            {
                TotalCost = totalCost,
                GPUCost = totalCost * 0.9, // 90% GPU cost
                ComputeCost = totalCost * 0.08,
                StorageCost = totalCost * 0.02,
                OnDemandCost = onDemandCost,
                SpotCost = spotCost,
                SpotSavings = spotSavings,
                SpotSavingsPercent = totalCost > 0 ? (spotSavings / (spotCost + spotSavings)) * 100 : 0,
                Efficiency = new ResourceEfficiency
                {
                    CPUUtilization = nodes.Average(n => n.Status.CPUUtilization),
                    GPUUtilization = nodes.Average(n => n.Status.GPUUtilization),
                    MemoryUtilization = nodes.Average(n => n.Status.MemoryUtilization),
                    WastedCost = totalCost * (1 - nodes.Average(n => n.Status.GPUUtilization) / 100)
                }
            };
        }

        private List<CostOptimizationRecommendation> GenerateCostRecommendations(
            List<AIWorkload> workloads,
            List<GPUNode> nodes)
        {
            var recommendations = new List<CostOptimizationRecommendation>();

            var spotPercentage = nodes.Count(n => n.Type == NodeType.Spot) / (double)nodes.Count;
            if (spotPercentage < 0.7)
            {
                recommendations.Add(new CostOptimizationRecommendation
                {
                    Title = "Increase spot instance usage",
                    Description = $"Current spot usage: {spotPercentage:P0}. Target 70% for maximum savings.",
                    EstimatedSavings = 50000,
                    EstimatedSavingsPercent = 40,
                    Priority = 10,
                    Category = "Cost Reduction"
                });
            }

            var avgGPUUtilization = nodes.Average(n => n.Status.GPUUtilization);
            if (avgGPUUtilization < 70)
            {
                recommendations.Add(new CostOptimizationRecommendation
                {
                    Title = "Enable GPU time-slicing or MIG",
                    Description = $"GPU utilization: {avgGPUUtilization:F1}%. Enable sharing to improve efficiency.",
                    EstimatedSavings = 30000,
                    EstimatedSavingsPercent = 25,
                    Priority = 9,
                    Category = "Resource Efficiency"
                });
            }

            var pendingWorkloads = workloads.Count(w => w.Status.State == WorkloadState.Pending);
            if (pendingWorkloads > 10)
            {
                recommendations.Add(new CostOptimizationRecommendation
                {
                    Title = "Enable elastic training for better GPU utilization",
                    Description = $"{pendingWorkloads} workloads pending. Elastic training can adapt to available resources.",
                    EstimatedSavings = 40000,
                    EstimatedSavingsPercent = 35,
                    Priority = 8,
                    Category = "Scheduling Optimization"
                });
            }

            return recommendations;
        }

        #endregion

        #region Analytics

        public async Task<SchedulerMetrics> GetMetricsAsync(
            string tenantId,
            TimeSpan duration,
            CancellationToken cancellation = default)
        {
            var workloads = await ListWorkloadsAsync(tenantId, null, cancellation);
            var nodes = await ListGPUNodesAsync(tenantId, cancellation);

            var metrics = new SchedulerMetrics
            {
                Period = duration,
                EndTime = DateTime.UtcNow,
                StartTime = DateTime.UtcNow - duration,
                Workloads = CalculateWorkloadMetrics(workloads),
                Efficiency = CalculateSchedulingEfficiency(workloads),
                Utilization = CalculateUtilizationMetrics(nodes),
                Costs = CalculateCostMetrics(nodes, duration)
            };

            return await Task.FromResult(metrics);
        }

        private WorkloadMetrics CalculateWorkloadMetrics(List<AIWorkload> workloads)
        {
            var completed = workloads.Where(w => w.Status.State == WorkloadState.Succeeded).ToList();

            return new WorkloadMetrics
            {
                TotalSubmitted = workloads.Count,
                TotalCompleted = completed.Count,
                TotalFailed = workloads.Count(w => w.Status.State == WorkloadState.Failed),
                TotalPreempted = workloads.Count(w => w.Status.IsPreempted),
                AverageWaitTime = TimeSpan.FromMinutes(new Random().Next(5, 30)),
                AverageRunTime = TimeSpan.FromHours(new Random().Next(1, 8)),
                P95WaitTime = TimeSpan.FromMinutes(new Random().Next(30, 120)),
                SuccessRate = workloads.Any() ? (double)completed.Count / workloads.Count : 0
            };
        }

        private SchedulingEfficiency CalculateSchedulingEfficiency(List<AIWorkload> workloads)
        {
            var gangScheduled = workloads.Count(w => w.Spec.GangScheduling?.Enabled == true);

            return new SchedulingEfficiency
            {
                SchedulingLatency = new Random().Next(50, 200),
                GangSchedulingSuccessRate = gangScheduled > 0 ? 0.95 : 0,
                PreemptionCount = workloads.Sum(w => w.Status.PreemptionCount),
                PreemptionLatency = 10, // Kueue: near-instantaneous (<10ms)
                ResourceFragmentation = new Random().Next(10, 30),
                BinPackingEfficiency = new Random().Next(70, 90)
            };
        }

        private ResourceUtilizationMetrics CalculateUtilizationMetrics(List<GPUNode> nodes)
        {
            return new ResourceUtilizationMetrics
            {
                CPUUtilization = nodes.Average(n => n.Status.CPUUtilization),
                MemoryUtilization = nodes.Average(n => n.Status.MemoryUtilization),
                GPUUtilization = nodes.Average(n => n.Status.GPUUtilization),
                GPUMemoryUtilization = new Random().Next(60, 85),
                GPUUtilizationByType = nodes
                    .SelectMany(n => n.GPUs)
                    .GroupBy(g => g.Model)
                    .ToDictionary(g => g.Key, g => new Random().Next(60, 95) + 0.0)
            };
        }

        private CostMetrics CalculateCostMetrics(List<GPUNode> nodes, TimeSpan duration)
        {
            var totalCost = nodes.Count * 15.0 * duration.TotalHours; // Average $15/hr
            var spotNodes = nodes.Count(n => n.Type == NodeType.Spot);
            var spotSavings = spotNodes * 17.5 * duration.TotalHours; // $17.5/hr savings

            return new CostMetrics
            {
                TotalCost = totalCost,
                CostPerGPUHour = 15.0,
                SpotSavings = spotSavings,
                WastedCost = totalCost * (1 - nodes.Average(n => n.Status.GPUUtilization) / 100)
            };
        }

        #endregion
    }

    #endregion
}
