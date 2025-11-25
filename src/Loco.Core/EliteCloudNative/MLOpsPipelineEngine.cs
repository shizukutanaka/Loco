using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.EliteCloudNative
{
    // ============================================================================
    // DOMAIN MODELS - MLOps Pipeline (MLflow + End-to-End ML Lifecycle)
    // ============================================================================

    public class MLExperiment
    {
        public string ExperimentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string ArtifactLocation { get; set; } = string.Empty;
        public Dictionary<string, string> Tags { get; set; } = new();
        public List<string> RunIds { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? LastRunAt { get; set; }
    }

    public class MLRun
    {
        public string RunId { get; set; } = string.Empty;
        public string ExperimentId { get; set; } = string.Empty;
        public string RunName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public RunStatus Status { get; set; } = new();
        public Dictionary<string, object> Params { get; set; } = new();
        public Dictionary<string, double> Metrics { get; set; } = new();
        public Dictionary<string, string> Tags { get; set; } = new();
        public List<Artifact> Artifacts { get; set; } = new();
        public RunMetadata Metadata { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    public class RunStatus
    {
        public string Status { get; set; } = "running"; // running, finished, failed, killed, scheduled
        public string? StatusMessage { get; set; }
    }

    public class Artifact
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "model"; // model, dataset, plot, notebook
        public string Path { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RunMetadata
    {
        public string SourceType { get; set; } = "project"; // project, notebook, job
        public string SourceName { get; set; } = string.Empty;
        public string GitCommit { get; set; } = string.Empty;
        public string GitBranch { get; set; } = string.Empty;
        public string GitRepoUrl { get; set; } = string.Empty;
        public Dictionary<string, string> SystemMetrics { get; set; } = new();
    }

    public class RegisteredModel
    {
        public string ModelId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<ModelVersion> Versions { get; set; } = new();
        public Dictionary<string, string> Tags { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ModelVersion
    {
        public string VersionId { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string Version { get; set; } = "1";
        public string RunId { get; set; } = string.Empty;
        public string Stage { get; set; } = "none"; // none, staging, production, archived
        public ModelSignature Signature { get; set; } = new();
        public string Source { get; set; } = string.Empty;
        public ModelMetadata Metadata { get; set; } = new();
        public Dictionary<string, string> Tags { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
    }

    public class ModelSignature
    {
        public List<SchemaField> Inputs { get; set; } = new();
        public List<SchemaField> Outputs { get; set; } = new();
    }

    public class SchemaField
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // double, float, integer, string, binary
        public bool Required { get; set; } = true;
    }

    public class ModelMetadata
    {
        public string Framework { get; set; } = string.Empty; // sklearn, tensorflow, pytorch, xgboost
        public string FrameworkVersion { get; set; } = string.Empty;
        public Dictionary<string, double> PerformanceMetrics { get; set; } = new();
        public long ModelSizeBytes { get; set; }
        public string Flavor { get; set; } = string.Empty;
    }

    public class MLPipeline
    {
        public string PipelineId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public PipelineSpec Spec { get; set; } = new();
        public PipelineStatus Status { get; set; } = new();
        public ScheduleConfig? Schedule { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastRunAt { get; set; }
    }

    public class PipelineSpec
    {
        public List<PipelineStage> Stages { get; set; } = new();
        public Dictionary<string, object> Parameters { get; set; } = new();
        public ResourceRequirements Resources { get; set; } = new();
        public RetryPolicy RetryPolicy { get; set; } = new();
    }

    public class PipelineStage
    {
        public string StageName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // data-ingestion, preprocessing, training, validation, deployment
        public Dictionary<string, object> Config { get; set; } = new();
        public List<string> DependsOn { get; set; } = new();
        public CachingPolicy? Caching { get; set; }
    }

    public class CachingPolicy
    {
        public bool Enabled { get; set; } = true;
        public int MaxCacheAgeDays { get; set; } = 7;
        public string CacheKey { get; set; } = string.Empty;
    }

    public class PipelineStatus
    {
        public string Phase { get; set; } = "pending"; // pending, running, succeeded, failed
        public int CurrentStageIndex { get; set; }
        public Dictionary<string, StageStatus> StageStatuses { get; set; } = new();
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    public class StageStatus
    {
        public string Phase { get; set; } = "pending";
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Error { get; set; }
        public Dictionary<string, object> Outputs { get; set; } = new();
    }

    public class ScheduleConfig
    {
        public bool Enabled { get; set; }
        public string CronExpression { get; set; } = string.Empty;
        public string Timezone { get; set; } = "UTC";
        public DateTime? NextRunTime { get; set; }
    }

    public class ResourceRequirements
    {
        public string Cpu { get; set; } = "2";
        public string Memory { get; set; } = "4Gi";
        public int GpuCount { get; set; }
    }

    public class RetryPolicy
    {
        public int MaxRetries { get; set; } = 3;
        public int BackoffSeconds { get; set; } = 60;
        public string BackoffStrategy { get; set; } = "exponential"; // linear, exponential
    }

    public class FeatureStore
    {
        public string StoreId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "online"; // online, offline, hybrid
        public List<FeatureGroup> FeatureGroups { get; set; } = new();
        public StorageBackend Storage { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class FeatureGroup
    {
        public string GroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<Feature> Features { get; set; } = new();
        public EntitySchema Entity { get; set; } = new();
        public DataSource Source { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
    }

    public class Feature
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty; // int, float, string, timestamp
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Statistics { get; set; } = new();
    }

    public class EntitySchema
    {
        public string Name { get; set; } = string.Empty;
        public List<string> KeyFields { get; set; } = new();
    }

    public class DataSource
    {
        public string Type { get; set; } = "batch"; // batch, stream
        public string Path { get; set; } = string.Empty;
        public string Format { get; set; } = "parquet"; // parquet, csv, avro
        public Dictionary<string, string> Options { get; set; } = new();
    }

    public class StorageBackend
    {
        public string OnlineStore { get; set; } = "redis"; // redis, dynamodb, cassandra
        public string OfflineStore { get; set; } = "s3"; // s3, gcs, snowflake
        public Dictionary<string, string> Config { get; set; } = new();
    }

    public class DataVersionControl
    {
        public string VersionId { get; set; } = string.Empty;
        public string DatasetName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DatasetMetadata Metadata { get; set; } = new();
        public string StoragePath { get; set; } = string.Empty;
        public string CommitHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class DatasetMetadata
    {
        public long SizeBytes { get; set; }
        public int NumRecords { get; set; }
        public List<string> Columns { get; set; } = new();
        public Dictionary<string, object> Statistics { get; set; } = new();
        public string Checksum { get; set; } = string.Empty;
    }

    public class ModelMonitor
    {
        public string MonitorId { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string ModelVersion { get; set; } = string.Empty;
        public MonitoringConfig Config { get; set; } = new();
        public MonitoringStatus Status { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class MonitoringConfig
    {
        public bool EnableDataDrift { get; set; } = true;
        public bool EnableModelDrift { get; set; } = true;
        public bool EnablePerformanceDrift { get; set; } = true;
        public DriftThresholds Thresholds { get; set; } = new();
        public int MonitoringIntervalSeconds { get; set; } = 3600;
        public List<string> AlertChannels { get; set; } = new(); // email, slack, pagerduty
    }

    public class DriftThresholds
    {
        public double DataDriftThreshold { get; set; } = 0.1; // KL divergence
        public double ModelDriftThreshold { get; set; } = 0.05; // Accuracy drop
        public double PerformanceDriftThreshold { get; set; } = 0.1; // Latency increase
    }

    public class MonitoringStatus
    {
        public bool IsHealthy { get; set; } = true;
        public List<DriftAlert> Alerts { get; set; } = new();
        public DateTime? LastCheckAt { get; set; }
        public Dictionary<string, double> CurrentMetrics { get; set; } = new();
        public Dictionary<string, double> BaselineMetrics { get; set; } = new();
    }

    public class DriftAlert
    {
        public string AlertId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // data-drift, model-drift, performance-drift
        public string Severity { get; set; } = "medium"; // low, medium, high, critical
        public double DriftScore { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
        public bool Acknowledged { get; set; }
    }

    public class ModelDeployment
    {
        public string DeploymentId { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string ModelVersion { get; set; } = string.Empty;
        public DeploymentStrategy Strategy { get; set; } = new();
        public DeploymentStatus Status { get; set; } = new();
        public ServingConfig Serving { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class DeploymentStrategy
    {
        public string Type { get; set; } = "rolling"; // rolling, blue-green, canary, shadow
        public int RolloutPercent { get; set; } = 100;
        public bool EnableShadowTraffic { get; set; }
        public AutoRollback? AutoRollback { get; set; }
    }

    public class AutoRollback
    {
        public bool Enabled { get; set; } = true;
        public List<RollbackCondition> Conditions { get; set; } = new();
    }

    public class RollbackCondition
    {
        public string Metric { get; set; } = string.Empty; // error-rate, latency, accuracy
        public string Operator { get; set; } = ">"; // >, <, >=, <=
        public double Threshold { get; set; }
        public int WindowSeconds { get; set; } = 300;
    }

    public class DeploymentStatus
    {
        public string Phase { get; set; } = "deploying"; // deploying, active, failed, rolling-back
        public int ReadyReplicas { get; set; }
        public int DesiredReplicas { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public DateTime? AvailableSince { get; set; }
    }

    public class ServingConfig
    {
        public int MinReplicas { get; set; } = 1;
        public int MaxReplicas { get; set; } = 10;
        public ResourceRequirements Resources { get; set; } = new();
        public AutoscalingPolicy Autoscaling { get; set; } = new();
    }

    public class AutoscalingPolicy
    {
        public string Metric { get; set; } = "cpu"; // cpu, memory, rps, custom
        public double TargetValue { get; set; } = 70;
    }

    public class MLWorkflow
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public WorkflowSpec Spec { get; set; } = new();
        public WorkflowStatus Status { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class WorkflowSpec
    {
        public string Entrypoint { get; set; } = string.Empty;
        public List<WorkflowTemplate> Templates { get; set; } = new();
        public Dictionary<string, object> Arguments { get; set; } = new();
    }

    public class WorkflowTemplate
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "container"; // container, dag, steps
        public ContainerTemplate? Container { get; set; }
        public DAGTemplate? DAG { get; set; }
        public List<WorkflowStep>? Steps { get; set; }
    }

    public class ContainerTemplate
    {
        public string Image { get; set; } = string.Empty;
        public List<string> Command { get; set; } = new();
        public List<string> Args { get; set; } = new();
        public Dictionary<string, string> Env { get; set; } = new();
    }

    public class DAGTemplate
    {
        public List<DAGTask> Tasks { get; set; } = new();
    }

    public class DAGTask
    {
        public string Name { get; set; } = string.Empty;
        public string Template { get; set; } = string.Empty;
        public List<string> Dependencies { get; set; } = new();
    }

    public class WorkflowStep
    {
        public string Name { get; set; } = string.Empty;
        public string Template { get; set; } = string.Empty;
    }

    public class WorkflowStatus
    {
        public string Phase { get; set; } = "pending";
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public Dictionary<string, string> Nodes { get; set; } = new(); // node -> status
    }

    public class ExperimentComparison
    {
        public string ComparisonId { get; set; } = string.Empty;
        public List<string> RunIds { get; set; } = new();
        public List<MetricComparison> Metrics { get; set; } = new();
        public List<ParamComparison> Params { get; set; } = new();
        public string BestRunId { get; set; } = string.Empty;
        public DateTime ComparedAt { get; set; }
    }

    public class MetricComparison
    {
        public string MetricName { get; set; } = string.Empty;
        public Dictionary<string, double> RunValues { get; set; } = new(); // runId -> value
        public string BestRunId { get; set; } = string.Empty;
    }

    public class ParamComparison
    {
        public string ParamName { get; set; } = string.Empty;
        public Dictionary<string, object> RunValues { get; set; } = new(); // runId -> value
    }

    public class MLOpsMetrics
    {
        public string MetricsId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int TotalExperiments { get; set; }
        public int ActiveRuns { get; set; }
        public int RegisteredModels { get; set; }
        public int ProductionModels { get; set; }
        public int ActivePipelines { get; set; }
        public int ActiveMonitors { get; set; }
        public double AverageModelAccuracy { get; set; }
        public TimeSpan AveragePipelineDuration { get; set; }
        public int DriftAlertsLast24Hours { get; set; }
        public Dictionary<string, PipelineMetrics> PipelineMetrics { get; set; } = new();
    }

    public class PipelineMetrics
    {
        public string PipelineName { get; set; } = string.Empty;
        public int TotalRuns { get; set; }
        public int SuccessfulRuns { get; set; }
        public int FailedRuns { get; set; }
        public double SuccessRate { get; set; }
        public TimeSpan AverageDuration { get; set; }
    }

    // ============================================================================
    // INTERFACE
    // ============================================================================

    public interface IMLOpsPipelineEngine
    {
        // Experiments & Runs
        Task<MLExperiment> CreateExperimentAsync(string tenantId, MLExperiment experiment, CancellationToken cancellation = default);
        Task<MLRun> CreateRunAsync(string tenantId, MLRun run, CancellationToken cancellation = default);
        Task<MLRun> GetRunAsync(string tenantId, string runId, CancellationToken cancellation = default);
        Task<bool> LogMetricAsync(string tenantId, string runId, string metricName, double value, CancellationToken cancellation = default);
        Task<bool> LogParameterAsync(string tenantId, string runId, string paramName, object value, CancellationToken cancellation = default);
        Task<ExperimentComparison> CompareRunsAsync(string tenantId, List<string> runIds, CancellationToken cancellation = default);

        // Model Registry
        Task<RegisteredModel> RegisterModelAsync(string tenantId, RegisteredModel model, CancellationToken cancellation = default);
        Task<ModelVersion> CreateModelVersionAsync(string tenantId, ModelVersion version, CancellationToken cancellation = default);
        Task<bool> TransitionModelStageAsync(string tenantId, string modelId, string version, string stage, CancellationToken cancellation = default);
        Task<List<ModelVersion>> GetModelVersionsByStageAsync(string tenantId, string modelName, string stage, CancellationToken cancellation = default);

        // Pipelines
        Task<MLPipeline> CreatePipelineAsync(string tenantId, MLPipeline pipeline, CancellationToken cancellation = default);
        Task<bool> RunPipelineAsync(string tenantId, string pipelineId, Dictionary<string, object>? parameters = null, CancellationToken cancellation = default);
        Task<PipelineStatus> GetPipelineStatusAsync(string tenantId, string pipelineId, CancellationToken cancellation = default);

        // Feature Store
        Task<FeatureStore> CreateFeatureStoreAsync(string tenantId, FeatureStore store, CancellationToken cancellation = default);
        Task<FeatureGroup> CreateFeatureGroupAsync(string tenantId, string storeId, FeatureGroup group, CancellationToken cancellation = default);
        Task<Dictionary<string, object>> GetFeaturesAsync(string tenantId, string storeId, string groupName, List<string> entityKeys, CancellationToken cancellation = default);

        // Data Versioning
        Task<DataVersionControl> CreateDataVersionAsync(string tenantId, DataVersionControl version, CancellationToken cancellation = default);
        Task<DataVersionControl> GetDataVersionAsync(string tenantId, string datasetName, string version, CancellationToken cancellation = default);

        // Model Monitoring
        Task<ModelMonitor> CreateMonitorAsync(string tenantId, ModelMonitor monitor, CancellationToken cancellation = default);
        Task<MonitoringStatus> GetMonitoringStatusAsync(string tenantId, string monitorId, CancellationToken cancellation = default);
        Task<bool> AcknowledgeDriftAlertAsync(string tenantId, string monitorId, string alertId, CancellationToken cancellation = default);

        // Model Deployment
        Task<ModelDeployment> DeployModelAsync(string tenantId, ModelDeployment deployment, CancellationToken cancellation = default);
        Task<DeploymentStatus> GetDeploymentStatusAsync(string tenantId, string deploymentId, CancellationToken cancellation = default);

        // Workflows
        Task<MLWorkflow> CreateWorkflowAsync(string tenantId, MLWorkflow workflow, CancellationToken cancellation = default);
        Task<WorkflowStatus> GetWorkflowStatusAsync(string tenantId, string workflowId, CancellationToken cancellation = default);

        // Metrics
        Task<MLOpsMetrics> GetMetricsAsync(string tenantId, CancellationToken cancellation = default);
    }

    // ============================================================================
    // IMPLEMENTATION
    // ============================================================================

    public class MLOpsPipelineEngine : IMLOpsPipelineEngine
    {
        private readonly ILogger<MLOpsPipelineEngine> _logger;
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly Dictionary<string, MLExperiment> _experiments = new();
        private readonly Dictionary<string, MLRun> _runs = new();
        private readonly Dictionary<string, RegisteredModel> _models = new();
        private readonly Dictionary<string, MLPipeline> _pipelines = new();
        private readonly Dictionary<string, FeatureStore> _featureStores = new();
        private readonly Dictionary<string, DataVersionControl> _dataVersions = new();
        private readonly Dictionary<string, ModelMonitor> _monitors = new();
        private readonly Dictionary<string, ModelDeployment> _deployments = new();
        private readonly Dictionary<string, MLWorkflow> _workflows = new();
        private readonly Random _random = new(42);

        public MLOpsPipelineEngine(ILogger<MLOpsPipelineEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<MLExperiment> CreateExperimentAsync(string tenantId, MLExperiment experiment, CancellationToken cancellation = default)
        {
            experiment.ExperimentId = Guid.NewGuid().ToString();
            experiment.CreatedAt = DateTime.UtcNow;
            experiment.ArtifactLocation = $"s3://mlflow-artifacts/{experiment.Name}";

            var key = $"{tenantId}:{experiment.ExperimentId}";
            _lock.EnterWriteLock();
            try
            {
                _experiments[key] = experiment;
                _logger.LogInformation($"Created ML experiment {experiment.Name} in {experiment.Namespace}");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return experiment;
        }

        public async Task<MLRun> CreateRunAsync(string tenantId, MLRun run, CancellationToken cancellation = default)
        {
            run.RunId = Guid.NewGuid().ToString();
            run.StartTime = DateTime.UtcNow;
            run.Status = new RunStatus { Status = "running" };

            var key = $"{tenantId}:{run.RunId}";
            _lock.EnterWriteLock();
            try
            {
                _runs[key] = run;

                // Add run to experiment
                var expKey = $"{tenantId}:{run.ExperimentId}";
                if (_experiments.TryGetValue(expKey, out var experiment))
                {
                    experiment.RunIds.Add(run.RunId);
                    experiment.LastRunAt = DateTime.UtcNow;
                }

                _logger.LogInformation($"Created ML run {run.RunName} for experiment {run.ExperimentId}");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return run;
        }

        public async Task<MLRun> GetRunAsync(string tenantId, string runId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{runId}";

            _lock.EnterReadLock();
            try
            {
                if (_runs.TryGetValue(key, out var run))
                {
                    return run;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new MLRun();
        }

        public async Task<bool> LogMetricAsync(string tenantId, string runId, string metricName, double value, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{runId}";

            _lock.EnterWriteLock();
            try
            {
                if (_runs.TryGetValue(key, out var run))
                {
                    run.Metrics[metricName] = value;
                    _logger.LogInformation($"Logged metric {metricName}={value:F4} for run {runId}");
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

        public async Task<bool> LogParameterAsync(string tenantId, string runId, string paramName, object value, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{runId}";

            _lock.EnterWriteLock();
            try
            {
                if (_runs.TryGetValue(key, out var run))
                {
                    run.Params[paramName] = value;
                    _logger.LogInformation($"Logged parameter {paramName}={value} for run {runId}");
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

        public async Task<ExperimentComparison> CompareRunsAsync(string tenantId, List<string> runIds, CancellationToken cancellation = default)
        {
            var comparison = new ExperimentComparison
            {
                ComparisonId = Guid.NewGuid().ToString(),
                RunIds = runIds,
                Metrics = new List<MetricComparison>(),
                Params = new List<ParamComparison>(),
                ComparedAt = DateTime.UtcNow
            };

            var runs = new List<MLRun>();

            _lock.EnterReadLock();
            try
            {
                foreach (var runId in runIds)
                {
                    var key = $"{tenantId}:{runId}";
                    if (_runs.TryGetValue(key, out var run))
                    {
                        runs.Add(run);
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            // Compare metrics
            var allMetricNames = runs.SelectMany(r => r.Metrics.Keys).Distinct();
            foreach (var metricName in allMetricNames)
            {
                var metricComp = new MetricComparison
                {
                    MetricName = metricName,
                    RunValues = runs.Where(r => r.Metrics.ContainsKey(metricName))
                                    .ToDictionary(r => r.RunId, r => r.Metrics[metricName])
                };

                if (metricComp.RunValues.Any())
                {
                    metricComp.BestRunId = metricComp.RunValues.OrderByDescending(kv => kv.Value).First().Key;
                }

                comparison.Metrics.Add(metricComp);
            }

            // Determine best overall run (using first metric)
            if (comparison.Metrics.Any())
            {
                comparison.BestRunId = comparison.Metrics.First().BestRunId;
            }

            _logger.LogInformation($"Compared {runIds.Count} runs across {comparison.Metrics.Count} metrics, best: {comparison.BestRunId}");

            await Task.CompletedTask;
            return comparison;
        }

        public async Task<RegisteredModel> RegisterModelAsync(string tenantId, RegisteredModel model, CancellationToken cancellation = default)
        {
            model.ModelId = Guid.NewGuid().ToString();
            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;

            var key = $"{tenantId}:{model.ModelId}";
            _lock.EnterWriteLock();
            try
            {
                _models[key] = model;
                _logger.LogInformation($"Registered model {model.Name} in model registry");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return model;
        }

        public async Task<ModelVersion> CreateModelVersionAsync(string tenantId, ModelVersion version, CancellationToken cancellation = default)
        {
            version.VersionId = Guid.NewGuid().ToString();
            version.CreatedAt = DateTime.UtcNow;

            _lock.EnterWriteLock();
            try
            {
                // Find the registered model and add version
                foreach (var model in _models.Values)
                {
                    if (model.Name == version.ModelName)
                    {
                        model.Versions.Add(version);
                        model.UpdatedAt = DateTime.UtcNow;
                        _logger.LogInformation($"Created model version {version.Version} for {version.ModelName} (stage: {version.Stage})");
                        break;
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return version;
        }

        public async Task<bool> TransitionModelStageAsync(string tenantId, string modelId, string version, string stage, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{modelId}";

            _lock.EnterWriteLock();
            try
            {
                if (_models.TryGetValue(key, out var model))
                {
                    var modelVersion = model.Versions.FirstOrDefault(v => v.Version == version);
                    if (modelVersion != null)
                    {
                        var oldStage = modelVersion.Stage;
                        modelVersion.Stage = stage;
                        modelVersion.LastUpdatedAt = DateTime.UtcNow;
                        _logger.LogInformation($"Transitioned model {model.Name} v{version} from {oldStage} to {stage}");
                        return true;
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<List<ModelVersion>> GetModelVersionsByStageAsync(string tenantId, string modelName, string stage, CancellationToken cancellation = default)
        {
            var versions = new List<ModelVersion>();

            _lock.EnterReadLock();
            try
            {
                foreach (var model in _models.Values)
                {
                    if (model.Name == modelName)
                    {
                        versions = model.Versions.Where(v => v.Stage == stage).ToList();
                        break;
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _logger.LogInformation($"Found {versions.Count} versions of {modelName} in {stage} stage");

            await Task.CompletedTask;
            return versions;
        }

        public async Task<MLPipeline> CreatePipelineAsync(string tenantId, MLPipeline pipeline, CancellationToken cancellation = default)
        {
            pipeline.PipelineId = Guid.NewGuid().ToString();
            pipeline.CreatedAt = DateTime.UtcNow;
            pipeline.Status = new PipelineStatus { Phase = "pending" };

            var key = $"{tenantId}:{pipeline.PipelineId}";
            _lock.EnterWriteLock();
            try
            {
                _pipelines[key] = pipeline;
                _logger.LogInformation($"Created ML pipeline {pipeline.Name} with {pipeline.Spec.Stages.Count} stages");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return pipeline;
        }

        public async Task<bool> RunPipelineAsync(string tenantId, string pipelineId, Dictionary<string, object>? parameters = null, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{pipelineId}";

            _lock.EnterWriteLock();
            try
            {
                if (_pipelines.TryGetValue(key, out var pipeline))
                {
                    pipeline.Status = new PipelineStatus
                    {
                        Phase = "running",
                        CurrentStageIndex = 0,
                        StartTime = DateTime.UtcNow
                    };

                    pipeline.LastRunAt = DateTime.UtcNow;

                    if (parameters != null)
                    {
                        pipeline.Spec.Parameters = parameters;
                    }

                    _logger.LogInformation($"Started pipeline {pipeline.Name} with {pipeline.Spec.Stages.Count} stages");
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

        public async Task<PipelineStatus> GetPipelineStatusAsync(string tenantId, string pipelineId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{pipelineId}";

            _lock.EnterReadLock();
            try
            {
                if (_pipelines.TryGetValue(key, out var pipeline))
                {
                    // Simulate pipeline progress
                    if (pipeline.Status.Phase == "running")
                    {
                        pipeline.Status.CurrentStageIndex = Math.Min(
                            pipeline.Status.CurrentStageIndex + 1,
                            pipeline.Spec.Stages.Count
                        );

                        if (pipeline.Status.CurrentStageIndex >= pipeline.Spec.Stages.Count)
                        {
                            pipeline.Status.Phase = "succeeded";
                            pipeline.Status.EndTime = DateTime.UtcNow;
                        }
                    }

                    return pipeline.Status;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new PipelineStatus();
        }

        public async Task<FeatureStore> CreateFeatureStoreAsync(string tenantId, FeatureStore store, CancellationToken cancellation = default)
        {
            store.StoreId = Guid.NewGuid().ToString();
            store.CreatedAt = DateTime.UtcNow;

            var key = $"{tenantId}:{store.StoreId}";
            _lock.EnterWriteLock();
            try
            {
                _featureStores[key] = store;
                _logger.LogInformation($"Created feature store {store.Name} ({store.Type}) with {store.FeatureGroups.Count} groups");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return store;
        }

        public async Task<FeatureGroup> CreateFeatureGroupAsync(string tenantId, string storeId, FeatureGroup group, CancellationToken cancellation = default)
        {
            group.GroupId = Guid.NewGuid().ToString();
            group.CreatedAt = DateTime.UtcNow;

            var key = $"{tenantId}:{storeId}";
            _lock.EnterWriteLock();
            try
            {
                if (_featureStores.TryGetValue(key, out var store))
                {
                    store.FeatureGroups.Add(group);
                    _logger.LogInformation($"Created feature group {group.Name} with {group.Features.Count} features");
                    return group;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return new FeatureGroup();
        }

        public async Task<Dictionary<string, object>> GetFeaturesAsync(string tenantId, string storeId, string groupName, List<string> entityKeys, CancellationToken cancellation = default)
        {
            var features = new Dictionary<string, object>();

            var key = $"{tenantId}:{storeId}";
            _lock.EnterReadLock();
            try
            {
                if (_featureStores.TryGetValue(key, out var store))
                {
                    var group = store.FeatureGroups.FirstOrDefault(g => g.Name == groupName);
                    if (group != null)
                    {
                        // Simulate feature retrieval
                        foreach (var feature in group.Features)
                        {
                            features[feature.Name] = _random.NextDouble() * 100;
                        }
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _logger.LogInformation($"Retrieved {features.Count} features from {groupName} for {entityKeys.Count} entities");

            await Task.CompletedTask;
            return features;
        }

        public async Task<DataVersionControl> CreateDataVersionAsync(string tenantId, DataVersionControl version, CancellationToken cancellation = default)
        {
            version.VersionId = Guid.NewGuid().ToString();
            version.CreatedAt = DateTime.UtcNow;
            version.CommitHash = Guid.NewGuid().ToString("N").Substring(0, 8);

            var key = $"{tenantId}:{version.VersionId}";
            _lock.EnterWriteLock();
            try
            {
                _dataVersions[key] = version;
                _logger.LogInformation($"Created data version {version.DatasetName} v{version.Version} ({version.Metadata.SizeBytes / 1_000_000}MB, {version.Metadata.NumRecords} records)");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return version;
        }

        public async Task<DataVersionControl> GetDataVersionAsync(string tenantId, string datasetName, string version, CancellationToken cancellation = default)
        {
            _lock.EnterReadLock();
            try
            {
                foreach (var dv in _dataVersions.Values)
                {
                    if (dv.DatasetName == datasetName && dv.Version == version)
                    {
                        return dv;
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new DataVersionControl();
        }

        public async Task<ModelMonitor> CreateMonitorAsync(string tenantId, ModelMonitor monitor, CancellationToken cancellation = default)
        {
            monitor.MonitorId = Guid.NewGuid().ToString();
            monitor.CreatedAt = DateTime.UtcNow;
            monitor.Status = new MonitoringStatus
            {
                IsHealthy = true,
                LastCheckAt = DateTime.UtcNow,
                CurrentMetrics = new Dictionary<string, double>
                {
                    { "accuracy", 0.92 + _random.NextDouble() * 0.05 },
                    { "latency_p95", 50 + _random.NextDouble() * 30 }
                },
                BaselineMetrics = new Dictionary<string, double>
                {
                    { "accuracy", 0.95 },
                    { "latency_p95", 50 }
                }
            };

            var key = $"{tenantId}:{monitor.MonitorId}";
            _lock.EnterWriteLock();
            try
            {
                _monitors[key] = monitor;
                _logger.LogInformation($"Created model monitor for {monitor.ModelName} v{monitor.ModelVersion}");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return monitor;
        }

        public async Task<MonitoringStatus> GetMonitoringStatusAsync(string tenantId, string monitorId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{monitorId}";

            _lock.EnterReadLock();
            try
            {
                if (_monitors.TryGetValue(key, out var monitor))
                {
                    // Simulate drift detection
                    if (_random.Next(10) < 2) // 20% chance of drift
                    {
                        var alert = new DriftAlert
                        {
                            AlertId = Guid.NewGuid().ToString(),
                            Type = "performance-drift",
                            Severity = "medium",
                            DriftScore = 0.08 + _random.NextDouble() * 0.05,
                            Description = "Model accuracy decreased by 3%",
                            DetectedAt = DateTime.UtcNow,
                            Acknowledged = false
                        };

                        monitor.Status.Alerts.Add(alert);
                        monitor.Status.IsHealthy = false;
                    }

                    return monitor.Status;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new MonitoringStatus();
        }

        public async Task<bool> AcknowledgeDriftAlertAsync(string tenantId, string monitorId, string alertId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{monitorId}";

            _lock.EnterWriteLock();
            try
            {
                if (_monitors.TryGetValue(key, out var monitor))
                {
                    var alert = monitor.Status.Alerts.FirstOrDefault(a => a.AlertId == alertId);
                    if (alert != null)
                    {
                        alert.Acknowledged = true;
                        _logger.LogInformation($"Acknowledged drift alert {alertId} for monitor {monitorId}");
                        return true;
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<ModelDeployment> DeployModelAsync(string tenantId, ModelDeployment deployment, CancellationToken cancellation = default)
        {
            deployment.DeploymentId = Guid.NewGuid().ToString();
            deployment.CreatedAt = DateTime.UtcNow;
            deployment.Status = new DeploymentStatus
            {
                Phase = "active",
                ReadyReplicas = deployment.Serving.MinReplicas,
                DesiredReplicas = deployment.Serving.MinReplicas,
                Endpoint = $"https://{deployment.ModelName}.serving.default.svc.cluster.local",
                AvailableSince = DateTime.UtcNow
            };

            var key = $"{tenantId}:{deployment.DeploymentId}";
            _lock.EnterWriteLock();
            try
            {
                _deployments[key] = deployment;
                _logger.LogInformation($"Deployed model {deployment.ModelName} v{deployment.ModelVersion} ({deployment.Strategy.Type} strategy, {deployment.Status.ReadyReplicas} replicas)");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return deployment;
        }

        public async Task<DeploymentStatus> GetDeploymentStatusAsync(string tenantId, string deploymentId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{deploymentId}";

            _lock.EnterReadLock();
            try
            {
                if (_deployments.TryGetValue(key, out var deployment))
                {
                    return deployment.Status;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new DeploymentStatus();
        }

        public async Task<MLWorkflow> CreateWorkflowAsync(string tenantId, MLWorkflow workflow, CancellationToken cancellation = default)
        {
            workflow.WorkflowId = Guid.NewGuid().ToString();
            workflow.CreatedAt = DateTime.UtcNow;
            workflow.Status = new WorkflowStatus
            {
                Phase = "running",
                StartedAt = DateTime.UtcNow
            };

            var key = $"{tenantId}:{workflow.WorkflowId}";
            _lock.EnterWriteLock();
            try
            {
                _workflows[key] = workflow;
                _logger.LogInformation($"Created ML workflow {workflow.Name} with {workflow.Spec.Templates.Count} templates");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return workflow;
        }

        public async Task<WorkflowStatus> GetWorkflowStatusAsync(string tenantId, string workflowId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{workflowId}";

            _lock.EnterReadLock();
            try
            {
                if (_workflows.TryGetValue(key, out var workflow))
                {
                    return workflow.Status;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new WorkflowStatus();
        }

        public async Task<MLOpsMetrics> GetMetricsAsync(string tenantId, CancellationToken cancellation = default)
        {
            var metrics = new MLOpsMetrics
            {
                MetricsId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                TotalExperiments = _random.Next(50, 200),
                ActiveRuns = _random.Next(10, 50),
                RegisteredModels = _random.Next(100, 500),
                ProductionModels = _random.Next(20, 100),
                ActivePipelines = _random.Next(10, 50),
                ActiveMonitors = _random.Next(20, 100),
                AverageModelAccuracy = 0.90 + _random.NextDouble() * 0.08,
                AveragePipelineDuration = TimeSpan.FromMinutes(30 + _random.NextDouble() * 90),
                DriftAlertsLast24Hours = _random.Next(2, 15),
                PipelineMetrics = new Dictionary<string, PipelineMetrics>()
            };

            for (int i = 1; i <= 5; i++)
            {
                var totalRuns = _random.Next(50, 200);
                var successfulRuns = _random.Next((int)(totalRuns * 0.7), (int)(totalRuns * 0.95));

                metrics.PipelineMetrics[$"pipeline-{i}"] = new PipelineMetrics
                {
                    PipelineName = $"ml-pipeline-{i}",
                    TotalRuns = totalRuns,
                    SuccessfulRuns = successfulRuns,
                    FailedRuns = totalRuns - successfulRuns,
                    SuccessRate = (double)successfulRuns / totalRuns * 100,
                    AverageDuration = TimeSpan.FromMinutes(20 + _random.NextDouble() * 60)
                };
            }

            _logger.LogInformation($"MLOps metrics: {metrics.TotalExperiments} experiments, {metrics.RegisteredModels} models, {metrics.ProductionModels} in production, {metrics.DriftAlertsLast24Hours} drift alerts");

            await Task.CompletedTask;
            return metrics;
        }
    }
}
