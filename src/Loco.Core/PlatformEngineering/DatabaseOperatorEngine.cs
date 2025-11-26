// =============================================================================
// Database Operator Engine
// Kubernetes-native database management with CloudNativePG, Vitess, Percona
// Based on: CloudNativePG, Vitess, Percona Operator, Zalando Postgres Operator
// Research: https://cloudnative-pg.io, https://vitess.io
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.PlatformEngineering
{
    #region Enums

    /// <summary>
    /// Database engine type
    /// </summary>
    public enum DatabaseEngine
    {
        PostgreSQL,
        MySQL,
        MariaDB,
        MongoDB,
        CockroachDB,
        TiDB,
        YugabyteDB,
        Vitess
    }

    /// <summary>
    /// Database cluster topology
    /// </summary>
    public enum ClusterTopology
    {
        Standalone,        // Single instance
        PrimaryReplica,    // Primary with read replicas
        MultiPrimary,      // Multi-primary (Galera, Group Replication)
        Distributed        // Horizontally scaled (Vitess, CockroachDB)
    }

    /// <summary>
    /// Replication mode
    /// </summary>
    public enum ReplicationMode
    {
        Async,             // Asynchronous replication
        Sync,              // Synchronous replication
        SemiSync,          // Semi-synchronous (at least one replica)
        Quorum             // Quorum-based (majority acknowledgment)
    }

    /// <summary>
    /// Backup type
    /// </summary>
    public enum BackupType
    {
        Full,              // Full backup
        Incremental,       // Incremental backup
        Differential,      // Differential backup
        Continuous         // Continuous WAL/binlog archiving
    }

    /// <summary>
    /// Backup storage type
    /// </summary>
    public enum BackupStorage
    {
        S3,
        GCS,
        Azure,
        MinIO,
        Local
    }

    /// <summary>
    /// Database instance role
    /// </summary>
    public enum InstanceRole
    {
        Primary,
        Replica,
        StandbyLeader,
        Candidate
    }

    /// <summary>
    /// Failover type
    /// </summary>
    public enum FailoverType
    {
        Automatic,
        Manual,
        Planned
    }

    /// <summary>
    /// Connection pooler type
    /// </summary>
    public enum ConnectionPooler
    {
        PgBouncer,
        Pgpool,
        ProxySQL,
        Odyssey,
        None
    }

    #endregion

    #region Core Types

    /// <summary>
    /// Database cluster specification
    /// </summary>
    public class DatabaseCluster
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = "default";

        /// <summary>
        /// Database engine
        /// </summary>
        public DatabaseEngine Engine { get; set; }

        /// <summary>
        /// Engine version
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Cluster topology
        /// </summary>
        public ClusterTopology Topology { get; set; } = ClusterTopology.PrimaryReplica;

        /// <summary>
        /// Number of instances
        /// </summary>
        public int Instances { get; set; } = 3;

        /// <summary>
        /// Storage configuration
        /// </summary>
        public StorageConfig Storage { get; set; } = new();

        /// <summary>
        /// Resource requirements
        /// </summary>
        public ResourceRequirements Resources { get; set; } = new();

        /// <summary>
        /// Replication configuration
        /// </summary>
        public ReplicationConfig Replication { get; set; } = new();

        /// <summary>
        /// Backup configuration
        /// </summary>
        public BackupConfig Backup { get; set; } = new();

        /// <summary>
        /// High availability configuration
        /// </summary>
        public HAConfig HighAvailability { get; set; } = new();

        /// <summary>
        /// Connection pooler configuration
        /// </summary>
        public PoolerConfig? ConnectionPooler { get; set; }

        /// <summary>
        /// Monitoring configuration
        /// </summary>
        public MonitoringConfig Monitoring { get; set; } = new();

        /// <summary>
        /// Labels
        /// </summary>
        public Dictionary<string, string> Labels { get; set; } = new();

        /// <summary>
        /// Annotations
        /// </summary>
        public Dictionary<string, string> Annotations { get; set; } = new();

        /// <summary>
        /// Cluster status
        /// </summary>
        public ClusterStatus Status { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Storage configuration
    /// </summary>
    public class StorageConfig
    {
        /// <summary>
        /// Storage size (e.g., "100Gi")
        /// </summary>
        public string Size { get; set; } = "10Gi";

        /// <summary>
        /// Storage class
        /// </summary>
        public string StorageClass { get; set; } = "standard";

        /// <summary>
        /// WAL storage size (PostgreSQL)
        /// </summary>
        public string? WalStorageSize { get; set; }

        /// <summary>
        /// Tablespace definitions
        /// </summary>
        public List<TablespaceConfig> Tablespaces { get; set; } = new();
    }

    /// <summary>
    /// Tablespace configuration
    /// </summary>
    public class TablespaceConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string StorageClass { get; set; } = string.Empty;
    }

    /// <summary>
    /// Resource requirements
    /// </summary>
    public class ResourceRequirements
    {
        public string CpuRequest { get; set; } = "1";
        public string CpuLimit { get; set; } = "2";
        public string MemoryRequest { get; set; } = "2Gi";
        public string MemoryLimit { get; set; } = "4Gi";
    }

    /// <summary>
    /// Replication configuration
    /// </summary>
    public class ReplicationConfig
    {
        /// <summary>
        /// Replication mode
        /// </summary>
        public ReplicationMode Mode { get; set; } = ReplicationMode.Async;

        /// <summary>
        /// Number of synchronous replicas (for sync mode)
        /// </summary>
        public int SyncReplicas { get; set; } = 1;

        /// <summary>
        /// Max lag threshold (bytes or seconds)
        /// </summary>
        public long MaxLagThreshold { get; set; } = 1024 * 1024 * 100; // 100MB

        /// <summary>
        /// Replication slots (PostgreSQL)
        /// </summary>
        public bool UseReplicationSlots { get; set; } = true;
    }

    /// <summary>
    /// Backup configuration
    /// </summary>
    public class BackupConfig
    {
        /// <summary>
        /// Enable backups
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Backup schedule (cron format)
        /// </summary>
        public string Schedule { get; set; } = "0 0 * * *"; // Daily at midnight

        /// <summary>
        /// Retention policy
        /// </summary>
        public RetentionPolicy Retention { get; set; } = new();

        /// <summary>
        /// Backup storage destination
        /// </summary>
        public BackupDestination Destination { get; set; } = new();

        /// <summary>
        /// Continuous WAL archiving
        /// </summary>
        public bool ContinuousArchiving { get; set; } = true;

        /// <summary>
        /// Compression
        /// </summary>
        public string Compression { get; set; } = "gzip";

        /// <summary>
        /// Encryption
        /// </summary>
        public BackupEncryption? Encryption { get; set; }
    }

    /// <summary>
    /// Retention policy
    /// </summary>
    public class RetentionPolicy
    {
        public int Daily { get; set; } = 7;
        public int Weekly { get; set; } = 4;
        public int Monthly { get; set; } = 3;
    }

    /// <summary>
    /// Backup destination
    /// </summary>
    public class BackupDestination
    {
        public BackupStorage Type { get; set; } = BackupStorage.S3;
        public string Bucket { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string CredentialsSecret { get; set; } = string.Empty;
    }

    /// <summary>
    /// Backup encryption
    /// </summary>
    public class BackupEncryption
    {
        public bool Enabled { get; set; } = true;
        public string Algorithm { get; set; } = "AES256";
        public string KeySecret { get; set; } = string.Empty;
    }

    /// <summary>
    /// High availability configuration
    /// </summary>
    public class HAConfig
    {
        /// <summary>
        /// Enable HA
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Failover mode
        /// </summary>
        public FailoverType FailoverMode { get; set; } = FailoverType.Automatic;

        /// <summary>
        /// Failover timeout
        /// </summary>
        public TimeSpan FailoverTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Switchover timeout
        /// </summary>
        public TimeSpan SwitchoverTimeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Minimum instances for automatic failover
        /// </summary>
        public int MinInstances { get; set; } = 2;

        /// <summary>
        /// Pod disruption budget
        /// </summary>
        public int MaxUnavailable { get; set; } = 1;
    }

    /// <summary>
    /// Connection pooler configuration
    /// </summary>
    public class PoolerConfig
    {
        public ConnectionPooler Type { get; set; } = ConnectionPooler.PgBouncer;
        public int Instances { get; set; } = 2;
        public int PoolSize { get; set; } = 100;
        public string PoolMode { get; set; } = "transaction"; // session, transaction, statement
        public int MaxClientConnections { get; set; } = 1000;
        public ResourceRequirements Resources { get; set; } = new()
        {
            CpuRequest = "100m",
            CpuLimit = "500m",
            MemoryRequest = "128Mi",
            MemoryLimit = "256Mi"
        };
    }

    /// <summary>
    /// Monitoring configuration
    /// </summary>
    public class MonitoringConfig
    {
        public bool Enabled { get; set; } = true;
        public bool PrometheusExporter { get; set; } = true;
        public int ExporterPort { get; set; } = 9187;
        public bool EnablePgStatStatements { get; set; } = true;
        public bool EnableAutoVacuumLogging { get; set; } = true;
    }

    /// <summary>
    /// Cluster status
    /// </summary>
    public class ClusterStatus
    {
        public string Phase { get; set; } = "Pending"; // Pending, Creating, Running, Failed
        public int ReadyInstances { get; set; }
        public int TotalInstances { get; set; }
        public string CurrentPrimary { get; set; } = string.Empty;
        public DateTime? LastBackup { get; set; }
        public DateTime? LastSuccessfulBackup { get; set; }
        public string LatestWalArchive { get; set; } = string.Empty;
        public List<InstanceStatus> Instances { get; set; } = new();
        public List<ClusterCondition> Conditions { get; set; } = new();
    }

    /// <summary>
    /// Instance status
    /// </summary>
    public class InstanceStatus
    {
        public string Name { get; set; } = string.Empty;
        public InstanceRole Role { get; set; }
        public string PodName { get; set; } = string.Empty;
        public bool Ready { get; set; }
        public long ReplicationLagBytes { get; set; }
        public TimeSpan? ReplicationLagTime { get; set; }
        public string Timeline { get; set; } = string.Empty;
    }

    /// <summary>
    /// Cluster condition
    /// </summary>
    public class ClusterCondition
    {
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime LastTransitionTime { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    #endregion

    #region Backup Types

    /// <summary>
    /// Backup record
    /// </summary>
    public class DatabaseBackup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClusterId { get; set; } = string.Empty;
        public string ClusterName { get; set; } = string.Empty;
        public BackupType Type { get; set; }
        public string Phase { get; set; } = "Pending"; // Pending, Running, Completed, Failed
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
        public long SizeBytes { get; set; }
        public string Location { get; set; } = string.Empty;
        public string WalStartLsn { get; set; } = string.Empty;
        public string WalEndLsn { get; set; } = string.Empty;
        public string Timeline { get; set; } = string.Empty;
        public bool Encrypted { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Point-in-time recovery target
    /// </summary>
    public class RecoveryTarget
    {
        /// <summary>
        /// Target time for PITR
        /// </summary>
        public DateTime? TargetTime { get; set; }

        /// <summary>
        /// Target LSN (PostgreSQL)
        /// </summary>
        public string? TargetLsn { get; set; }

        /// <summary>
        /// Target transaction ID
        /// </summary>
        public string? TargetXid { get; set; }

        /// <summary>
        /// Target named restore point
        /// </summary>
        public string? TargetName { get; set; }

        /// <summary>
        /// Recovery action (pause, promote, shutdown)
        /// </summary>
        public string RecoveryAction { get; set; } = "promote";
    }

    /// <summary>
    /// Restore request
    /// </summary>
    public class RestoreRequest
    {
        public string SourceClusterId { get; set; } = string.Empty;
        public string? BackupId { get; set; }
        public RecoveryTarget? RecoveryTarget { get; set; }
        public string TargetClusterName { get; set; } = string.Empty;
        public string TargetNamespace { get; set; } = string.Empty;
    }

    #endregion

    #region User Management Types

    /// <summary>
    /// Database user specification
    /// </summary>
    public class DatabaseUser
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClusterId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string PasswordSecretName { get; set; } = string.Empty;
        public List<string> Databases { get; set; } = new();
        public List<string> Roles { get; set; } = new();
        public UserOptions Options { get; set; } = new();
        public ConnectionLimits? ConnectionLimits { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// User options
    /// </summary>
    public class UserOptions
    {
        public bool Superuser { get; set; }
        public bool CreateDb { get; set; }
        public bool CreateRole { get; set; }
        public bool Login { get; set; } = true;
        public bool Replication { get; set; }
        public bool BypassRls { get; set; }
    }

    /// <summary>
    /// Connection limits for user
    /// </summary>
    public class ConnectionLimits
    {
        public int? MaxConnections { get; set; }
        public TimeSpan? ConnectionTimeout { get; set; }
    }

    /// <summary>
    /// Database definition
    /// </summary>
    public class DatabaseDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClusterId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Encoding { get; set; } = "UTF8";
        public string LcCollate { get; set; } = "en_US.utf8";
        public string LcCtype { get; set; } = "en_US.utf8";
        public string? Tablespace { get; set; }
        public List<string> Extensions { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    #endregion

    #region Vitess Types

    /// <summary>
    /// Vitess keyspace configuration
    /// </summary>
    public class VitessKeyspace
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string ClusterId { get; set; } = string.Empty;

        /// <summary>
        /// Sharding specification
        /// </summary>
        public ShardingSpec Sharding { get; set; } = new();

        /// <summary>
        /// Vindexes (Vitess indexes for sharding)
        /// </summary>
        public List<Vindex> Vindexes { get; set; } = new();

        /// <summary>
        /// Tables with vschema
        /// </summary>
        public List<VitessTable> Tables { get; set; } = new();

        public bool Durability { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Sharding specification
    /// </summary>
    public class ShardingSpec
    {
        /// <summary>
        /// Sharded or unsharded
        /// </summary>
        public bool Sharded { get; set; }

        /// <summary>
        /// Shard definitions
        /// </summary>
        public List<ShardDefinition> Shards { get; set; } = new();

        /// <summary>
        /// Number of shards (for auto-sharding)
        /// </summary>
        public int ShardCount { get; set; } = 1;
    }

    /// <summary>
    /// Shard definition
    /// </summary>
    public class ShardDefinition
    {
        public string Name { get; set; } = string.Empty; // e.g., "-80", "80-"
        public string KeyRange { get; set; } = string.Empty;
        public int Replicas { get; set; } = 2;
        public int RdonlyReplicas { get; set; } = 1;
    }

    /// <summary>
    /// Vitess index (Vindex)
    /// </summary>
    public class Vindex
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "hash"; // hash, binary, lookup, etc.
        public Dictionary<string, string> Params { get; set; } = new();
        public string? Owner { get; set; }
    }

    /// <summary>
    /// Vitess table definition
    /// </summary>
    public class VitessTable
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = ""; // "" for sharded, "reference" for reference tables
        public List<VitessColumn> ColumnVindexes { get; set; } = new();
        public bool AutoIncrement { get; set; }
        public string? AutoIncrementSequence { get; set; }
    }

    /// <summary>
    /// Column vindex mapping
    /// </summary>
    public class VitessColumn
    {
        public string Column { get; set; } = string.Empty;
        public string Vindex { get; set; } = string.Empty;
    }

    #endregion

    #region Maintenance Types

    /// <summary>
    /// Maintenance window configuration
    /// </summary>
    public class MaintenanceWindow
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClusterId { get; set; } = string.Empty;

        /// <summary>
        /// Day of week (0-6, Sunday=0)
        /// </summary>
        public int DayOfWeek { get; set; }

        /// <summary>
        /// Start hour (0-23)
        /// </summary>
        public int StartHour { get; set; }

        /// <summary>
        /// Duration
        /// </summary>
        public TimeSpan Duration { get; set; } = TimeSpan.FromHours(2);

        /// <summary>
        /// Allowed operations
        /// </summary>
        public List<string> AllowedOperations { get; set; } = new()
        {
            "version-upgrade",
            "vacuum",
            "reindex",
            "parameter-change"
        };

        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Scheduled operation
    /// </summary>
    public class ScheduledOperation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClusterId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime ScheduledTime { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string Status { get; set; } = "Scheduled";
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Error { get; set; }
    }

    #endregion

    #region Interface

    /// <summary>
    /// Database Operator Engine interface
    /// Provides Kubernetes-native database management
    /// </summary>
    public interface IDatabaseOperatorEngine
    {
        #region Cluster Management

        /// <summary>
        /// Create database cluster
        /// </summary>
        Task<DatabaseCluster> CreateClusterAsync(
            string tenantId,
            DatabaseCluster cluster,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get cluster by ID
        /// </summary>
        Task<DatabaseCluster?> GetClusterAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Update cluster
        /// </summary>
        Task<DatabaseCluster> UpdateClusterAsync(
            string tenantId,
            DatabaseCluster cluster,
            CancellationToken cancellation = default);

        /// <summary>
        /// Delete cluster
        /// </summary>
        Task<bool> DeleteClusterAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default);

        /// <summary>
        /// List clusters
        /// </summary>
        Task<List<DatabaseCluster>> ListClustersAsync(
            string tenantId,
            DatabaseEngine? engine = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Scale cluster instances
        /// </summary>
        Task<DatabaseCluster> ScaleClusterAsync(
            string tenantId,
            string clusterId,
            int instances,
            CancellationToken cancellation = default);

        #endregion

        #region Failover & Switchover

        /// <summary>
        /// Trigger manual failover
        /// </summary>
        Task<bool> TriggerFailoverAsync(
            string tenantId,
            string clusterId,
            string? targetInstance = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Perform planned switchover
        /// </summary>
        Task<bool> SwitchoverAsync(
            string tenantId,
            string clusterId,
            string targetInstance,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get replication status
        /// </summary>
        Task<List<InstanceStatus>> GetReplicationStatusAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default);

        #endregion

        #region Backup & Restore

        /// <summary>
        /// Create on-demand backup
        /// </summary>
        Task<DatabaseBackup> CreateBackupAsync(
            string tenantId,
            string clusterId,
            BackupType type = BackupType.Full,
            CancellationToken cancellation = default);

        /// <summary>
        /// List backups
        /// </summary>
        Task<List<DatabaseBackup>> ListBackupsAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Restore from backup
        /// </summary>
        Task<DatabaseCluster> RestoreAsync(
            string tenantId,
            RestoreRequest request,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get recovery point objectives
        /// </summary>
        Task<RecoveryPointInfo> GetRecoveryPointInfoAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default);

        #endregion

        #region User & Database Management

        /// <summary>
        /// Create database user
        /// </summary>
        Task<DatabaseUser> CreateUserAsync(
            string tenantId,
            string clusterId,
            DatabaseUser user,
            CancellationToken cancellation = default);

        /// <summary>
        /// Create database
        /// </summary>
        Task<DatabaseDefinition> CreateDatabaseAsync(
            string tenantId,
            string clusterId,
            DatabaseDefinition database,
            CancellationToken cancellation = default);

        /// <summary>
        /// List databases
        /// </summary>
        Task<List<DatabaseDefinition>> ListDatabasesAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default);

        #endregion

        #region Vitess Operations

        /// <summary>
        /// Create Vitess keyspace
        /// </summary>
        Task<VitessKeyspace> CreateKeyspaceAsync(
            string tenantId,
            string clusterId,
            VitessKeyspace keyspace,
            CancellationToken cancellation = default);

        /// <summary>
        /// Reshard keyspace
        /// </summary>
        Task<bool> ReshardAsync(
            string tenantId,
            string keyspaceId,
            List<ShardDefinition> newShards,
            CancellationToken cancellation = default);

        #endregion

        #region Maintenance

        /// <summary>
        /// Schedule maintenance operation
        /// </summary>
        Task<ScheduledOperation> ScheduleMaintenanceAsync(
            string tenantId,
            string clusterId,
            string operationType,
            Dictionary<string, object> parameters,
            DateTime? scheduledTime = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Configure maintenance window
        /// </summary>
        Task<MaintenanceWindow> ConfigureMaintenanceWindowAsync(
            string tenantId,
            string clusterId,
            MaintenanceWindow window,
            CancellationToken cancellation = default);

        #endregion

        #region Manifests

        /// <summary>
        /// Generate Kubernetes manifests
        /// </summary>
        Task<string> GenerateManifestsAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default);

        #endregion
    }

    #endregion

    #region Additional Types

    /// <summary>
    /// Recovery point information
    /// </summary>
    public class RecoveryPointInfo
    {
        public DateTime OldestRecoveryPoint { get; set; }
        public DateTime LatestRecoveryPoint { get; set; }
        public TimeSpan RecoveryPointObjective { get; set; }
        public string LatestWalSegment { get; set; } = string.Empty;
        public List<DateTime> AvailableRecoveryPoints { get; set; } = new();
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Database Operator Engine implementation
    /// </summary>
    public class DatabaseOperatorEngine : IDatabaseOperatorEngine
    {
        private readonly ILogger<DatabaseOperatorEngine> _logger;
        private readonly Dictionary<string, Dictionary<string, DatabaseCluster>> _clusters = new();
        private readonly Dictionary<string, List<DatabaseBackup>> _backups = new();
        private readonly Dictionary<string, Dictionary<string, DatabaseUser>> _users = new();
        private readonly Dictionary<string, Dictionary<string, DatabaseDefinition>> _databases = new();
        private readonly Dictionary<string, Dictionary<string, VitessKeyspace>> _keyspaces = new();
        private readonly Dictionary<string, Dictionary<string, MaintenanceWindow>> _maintenanceWindows = new();
        private readonly Dictionary<string, List<ScheduledOperation>> _scheduledOps = new();

        public DatabaseOperatorEngine(ILogger<DatabaseOperatorEngine> logger)
        {
            _logger = logger;
        }

        #region Cluster Management

        public Task<DatabaseCluster> CreateClusterAsync(
            string tenantId,
            DatabaseCluster cluster,
            CancellationToken cancellation = default)
        {
            if (!_clusters.ContainsKey(tenantId))
                _clusters[tenantId] = new();

            cluster.TenantId = tenantId;
            cluster.CreatedAt = DateTime.UtcNow;
            cluster.Status = new ClusterStatus
            {
                Phase = "Creating",
                TotalInstances = cluster.Instances,
                ReadyInstances = 0
            };

            // Set defaults based on engine
            ApplyEngineDefaults(cluster);

            _clusters[tenantId][cluster.Id] = cluster;

            // Simulate instance creation
            SimulateClusterCreation(cluster);

            _logger.LogInformation(
                "Created {Engine} cluster {Name} with {Instances} instances",
                cluster.Engine, cluster.Name, cluster.Instances);

            return Task.FromResult(cluster);
        }

        private void ApplyEngineDefaults(DatabaseCluster cluster)
        {
            switch (cluster.Engine)
            {
                case DatabaseEngine.PostgreSQL:
                    cluster.Version = string.IsNullOrEmpty(cluster.Version) ? "16" : cluster.Version;
                    cluster.ConnectionPooler ??= new PoolerConfig
                    {
                        Type = ConnectionPooler.PgBouncer,
                        PoolMode = "transaction"
                    };
                    break;
                case DatabaseEngine.MySQL:
                    cluster.Version = string.IsNullOrEmpty(cluster.Version) ? "8.0" : cluster.Version;
                    break;
                case DatabaseEngine.Vitess:
                    cluster.Version = string.IsNullOrEmpty(cluster.Version) ? "18.0" : cluster.Version;
                    cluster.Topology = ClusterTopology.Distributed;
                    break;
            }
        }

        private void SimulateClusterCreation(DatabaseCluster cluster)
        {
            // Simulate instances coming up
            for (int i = 0; i < cluster.Instances; i++)
            {
                var instance = new InstanceStatus
                {
                    Name = $"{cluster.Name}-{i + 1}",
                    PodName = $"{cluster.Name}-{i + 1}-0",
                    Role = i == 0 ? InstanceRole.Primary : InstanceRole.Replica,
                    Ready = true,
                    ReplicationLagBytes = i == 0 ? 0 : new Random().Next(0, 1024 * 100)
                };
                cluster.Status.Instances.Add(instance);
            }

            cluster.Status.Phase = "Running";
            cluster.Status.ReadyInstances = cluster.Instances;
            cluster.Status.CurrentPrimary = $"{cluster.Name}-1";
            cluster.Status.Conditions.Add(new ClusterCondition
            {
                Type = "Ready",
                Status = "True",
                LastTransitionTime = DateTime.UtcNow,
                Reason = "ClusterReady",
                Message = "All instances are running"
            });
        }

        public Task<DatabaseCluster?> GetClusterAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            if (_clusters.TryGetValue(tenantId, out var clusters) &&
                clusters.TryGetValue(clusterId, out var cluster))
            {
                return Task.FromResult<DatabaseCluster?>(cluster);
            }

            return Task.FromResult<DatabaseCluster?>(null);
        }

        public Task<DatabaseCluster> UpdateClusterAsync(
            string tenantId,
            DatabaseCluster cluster,
            CancellationToken cancellation = default)
        {
            if (!_clusters.ContainsKey(tenantId) ||
                !_clusters[tenantId].ContainsKey(cluster.Id))
            {
                throw new InvalidOperationException($"Cluster {cluster.Id} not found");
            }

            cluster.UpdatedAt = DateTime.UtcNow;
            _clusters[tenantId][cluster.Id] = cluster;

            return Task.FromResult(cluster);
        }

        public Task<bool> DeleteClusterAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            if (_clusters.TryGetValue(tenantId, out var clusters))
            {
                var removed = clusters.Remove(clusterId);
                if (removed)
                {
                    _logger.LogInformation("Deleted cluster {ClusterId}", clusterId);
                }
                return Task.FromResult(removed);
            }

            return Task.FromResult(false);
        }

        public Task<List<DatabaseCluster>> ListClustersAsync(
            string tenantId,
            DatabaseEngine? engine = null,
            CancellationToken cancellation = default)
        {
            if (!_clusters.TryGetValue(tenantId, out var clusters))
                return Task.FromResult(new List<DatabaseCluster>());

            var result = clusters.Values.AsEnumerable();
            if (engine.HasValue)
            {
                result = result.Where(c => c.Engine == engine.Value);
            }

            return Task.FromResult(result.ToList());
        }

        public async Task<DatabaseCluster> ScaleClusterAsync(
            string tenantId,
            string clusterId,
            int instances,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            var oldInstances = cluster.Instances;
            cluster.Instances = instances;
            cluster.Status.TotalInstances = instances;

            // Scale up
            if (instances > oldInstances)
            {
                for (int i = oldInstances; i < instances; i++)
                {
                    cluster.Status.Instances.Add(new InstanceStatus
                    {
                        Name = $"{cluster.Name}-{i + 1}",
                        PodName = $"{cluster.Name}-{i + 1}-0",
                        Role = InstanceRole.Replica,
                        Ready = true
                    });
                }
            }
            // Scale down
            else if (instances < oldInstances)
            {
                cluster.Status.Instances = cluster.Status.Instances.Take(instances).ToList();
            }

            cluster.Status.ReadyInstances = instances;

            _logger.LogInformation(
                "Scaled cluster {ClusterId} from {OldInstances} to {NewInstances} instances",
                clusterId, oldInstances, instances);

            return cluster;
        }

        #endregion

        #region Failover & Switchover

        public async Task<bool> TriggerFailoverAsync(
            string tenantId,
            string clusterId,
            string? targetInstance = null,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            var replicas = cluster.Status.Instances
                .Where(i => i.Role == InstanceRole.Replica && i.Ready)
                .OrderBy(i => i.ReplicationLagBytes)
                .ToList();

            if (!replicas.Any())
            {
                _logger.LogError("No available replicas for failover");
                return false;
            }

            var newPrimary = targetInstance != null
                ? replicas.FirstOrDefault(i => i.Name == targetInstance)
                : replicas.First();

            if (newPrimary == null)
            {
                _logger.LogError("Target instance not found or not eligible");
                return false;
            }

            // Promote replica
            var oldPrimary = cluster.Status.Instances.FirstOrDefault(i => i.Role == InstanceRole.Primary);
            if (oldPrimary != null)
            {
                oldPrimary.Role = InstanceRole.Replica;
            }

            newPrimary.Role = InstanceRole.Primary;
            newPrimary.ReplicationLagBytes = 0;
            cluster.Status.CurrentPrimary = newPrimary.Name;

            cluster.Status.Conditions.Add(new ClusterCondition
            {
                Type = "Failover",
                Status = "True",
                LastTransitionTime = DateTime.UtcNow,
                Reason = "FailoverCompleted",
                Message = $"Failover to {newPrimary.Name} completed"
            });

            _logger.LogInformation(
                "Failover completed for cluster {ClusterId}: new primary is {NewPrimary}",
                clusterId, newPrimary.Name);

            return true;
        }

        public async Task<bool> SwitchoverAsync(
            string tenantId,
            string clusterId,
            string targetInstance,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            var target = cluster.Status.Instances.FirstOrDefault(i => i.Name == targetInstance);
            if (target == null || target.Role != InstanceRole.Replica)
            {
                throw new InvalidOperationException("Target must be a replica instance");
            }

            // Planned switchover (wait for sync)
            var oldPrimary = cluster.Status.Instances.First(i => i.Role == InstanceRole.Primary);
            oldPrimary.Role = InstanceRole.Replica;
            oldPrimary.ReplicationLagBytes = 0;

            target.Role = InstanceRole.Primary;
            target.ReplicationLagBytes = 0;
            cluster.Status.CurrentPrimary = target.Name;

            _logger.LogInformation(
                "Planned switchover completed for cluster {ClusterId}: new primary is {NewPrimary}",
                clusterId, target.Name);

            return true;
        }

        public async Task<List<InstanceStatus>> GetReplicationStatusAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            return cluster.Status.Instances;
        }

        #endregion

        #region Backup & Restore

        public async Task<DatabaseBackup> CreateBackupAsync(
            string tenantId,
            string clusterId,
            BackupType type = BackupType.Full,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            if (!_backups.ContainsKey(clusterId))
                _backups[clusterId] = new();

            var backup = new DatabaseBackup
            {
                ClusterId = clusterId,
                ClusterName = cluster.Name,
                Type = type,
                StartTime = DateTime.UtcNow,
                Phase = "Running"
            };

            // Simulate backup completion
            backup.EndTime = DateTime.UtcNow.AddMinutes(5);
            backup.Phase = "Completed";
            backup.SizeBytes = new Random().Next(100 * 1024 * 1024, 1024 * 1024 * 1024); // 100MB - 1GB
            backup.Location = $"{cluster.Backup.Destination.Bucket}/{cluster.Name}/backups/{backup.Id}";
            backup.WalStartLsn = "0/5000000";
            backup.WalEndLsn = "0/6000000";
            backup.Timeline = "1";
            backup.Encrypted = cluster.Backup.Encryption?.Enabled ?? false;

            _backups[clusterId].Add(backup);
            cluster.Status.LastBackup = backup.EndTime;
            cluster.Status.LastSuccessfulBackup = backup.EndTime;

            _logger.LogInformation(
                "Created {Type} backup {BackupId} for cluster {ClusterName}",
                type, backup.Id, cluster.Name);

            return backup;
        }

        public Task<List<DatabaseBackup>> ListBackupsAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            if (!_backups.TryGetValue(clusterId, out var backups))
                return Task.FromResult(new List<DatabaseBackup>());

            return Task.FromResult(backups.OrderByDescending(b => b.StartTime).ToList());
        }

        public async Task<DatabaseCluster> RestoreAsync(
            string tenantId,
            RestoreRequest request,
            CancellationToken cancellation = default)
        {
            var sourceCluster = await GetClusterAsync(tenantId, request.SourceClusterId, cancellation);
            if (sourceCluster == null)
                throw new InvalidOperationException($"Source cluster {request.SourceClusterId} not found");

            // Clone cluster configuration
            var newCluster = new DatabaseCluster
            {
                Name = request.TargetClusterName,
                Namespace = request.TargetNamespace,
                Engine = sourceCluster.Engine,
                Version = sourceCluster.Version,
                Topology = sourceCluster.Topology,
                Instances = sourceCluster.Instances,
                Storage = sourceCluster.Storage,
                Resources = sourceCluster.Resources,
                Replication = sourceCluster.Replication,
                Backup = sourceCluster.Backup,
                HighAvailability = sourceCluster.HighAvailability,
                ConnectionPooler = sourceCluster.ConnectionPooler,
                Monitoring = sourceCluster.Monitoring,
                Annotations = new Dictionary<string, string>
                {
                    ["cloudnative-pg.io/clone-from"] = sourceCluster.Name
                }
            };

            if (request.RecoveryTarget != null)
            {
                newCluster.Annotations["cloudnative-pg.io/recovery-target-time"] =
                    request.RecoveryTarget.TargetTime?.ToString("O") ?? "";
            }

            return await CreateClusterAsync(tenantId, newCluster, cancellation);
        }

        public async Task<RecoveryPointInfo> GetRecoveryPointInfoAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            var backups = await ListBackupsAsync(tenantId, clusterId, cancellation);
            var completedBackups = backups.Where(b => b.Phase == "Completed").ToList();

            return new RecoveryPointInfo
            {
                OldestRecoveryPoint = completedBackups.LastOrDefault()?.StartTime ?? DateTime.UtcNow,
                LatestRecoveryPoint = DateTime.UtcNow.AddMinutes(-1), // Continuous archiving
                RecoveryPointObjective = TimeSpan.FromMinutes(1),
                LatestWalSegment = completedBackups.FirstOrDefault()?.WalEndLsn ?? "0/0",
                AvailableRecoveryPoints = completedBackups
                    .Select(b => b.StartTime)
                    .OrderByDescending(t => t)
                    .ToList()
            };
        }

        #endregion

        #region User & Database Management

        public async Task<DatabaseUser> CreateUserAsync(
            string tenantId,
            string clusterId,
            DatabaseUser user,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            if (!_users.ContainsKey(clusterId))
                _users[clusterId] = new();

            user.ClusterId = clusterId;
            user.CreatedAt = DateTime.UtcNow;
            user.PasswordSecretName = $"{cluster.Name}-{user.Username}-credentials";

            _users[clusterId][user.Id] = user;

            _logger.LogInformation(
                "Created user {Username} in cluster {ClusterName}",
                user.Username, cluster.Name);

            return user;
        }

        public async Task<DatabaseDefinition> CreateDatabaseAsync(
            string tenantId,
            string clusterId,
            DatabaseDefinition database,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            if (!_databases.ContainsKey(clusterId))
                _databases[clusterId] = new();

            database.ClusterId = clusterId;
            database.CreatedAt = DateTime.UtcNow;

            _databases[clusterId][database.Id] = database;

            _logger.LogInformation(
                "Created database {DatabaseName} in cluster {ClusterName}",
                database.Name, cluster.Name);

            return database;
        }

        public Task<List<DatabaseDefinition>> ListDatabasesAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            if (!_databases.TryGetValue(clusterId, out var databases))
                return Task.FromResult(new List<DatabaseDefinition>());

            return Task.FromResult(databases.Values.ToList());
        }

        #endregion

        #region Vitess Operations

        public async Task<VitessKeyspace> CreateKeyspaceAsync(
            string tenantId,
            string clusterId,
            VitessKeyspace keyspace,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null || cluster.Engine != DatabaseEngine.Vitess)
                throw new InvalidOperationException("Keyspace can only be created on Vitess clusters");

            if (!_keyspaces.ContainsKey(clusterId))
                _keyspaces[clusterId] = new();

            keyspace.ClusterId = clusterId;
            keyspace.CreatedAt = DateTime.UtcNow;

            // Set default sharding if not specified
            if (!keyspace.Sharding.Shards.Any() && keyspace.Sharding.Sharded)
            {
                keyspace.Sharding.Shards = new List<ShardDefinition>
                {
                    new ShardDefinition { Name = "-80", KeyRange = "-80", Replicas = 2 },
                    new ShardDefinition { Name = "80-", KeyRange = "80-", Replicas = 2 }
                };
            }

            _keyspaces[clusterId][keyspace.Id] = keyspace;

            _logger.LogInformation(
                "Created Vitess keyspace {KeyspaceName} with {ShardCount} shards",
                keyspace.Name, keyspace.Sharding.Shards.Count);

            return keyspace;
        }

        public async Task<bool> ReshardAsync(
            string tenantId,
            string keyspaceId,
            List<ShardDefinition> newShards,
            CancellationToken cancellation = default)
        {
            // Find keyspace
            VitessKeyspace? keyspace = null;
            foreach (var ksDict in _keyspaces.Values)
            {
                if (ksDict.TryGetValue(keyspaceId, out var ks))
                {
                    keyspace = ks;
                    break;
                }
            }

            if (keyspace == null)
                throw new InvalidOperationException($"Keyspace {keyspaceId} not found");

            _logger.LogInformation(
                "Starting reshard of keyspace {KeyspaceName}: {OldShards} -> {NewShards} shards",
                keyspace.Name, keyspace.Sharding.Shards.Count, newShards.Count);

            // Simulate resharding (in real implementation would use vtctlclient)
            keyspace.Sharding.Shards = newShards;
            keyspace.Sharding.ShardCount = newShards.Count;

            return true;
        }

        #endregion

        #region Maintenance

        public async Task<ScheduledOperation> ScheduleMaintenanceAsync(
            string tenantId,
            string clusterId,
            string operationType,
            Dictionary<string, object> parameters,
            DateTime? scheduledTime = null,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            if (!_scheduledOps.ContainsKey(clusterId))
                _scheduledOps[clusterId] = new();

            var operation = new ScheduledOperation
            {
                ClusterId = clusterId,
                Type = operationType,
                ScheduledTime = scheduledTime ?? DateTime.UtcNow.AddHours(1),
                Parameters = parameters
            };

            _scheduledOps[clusterId].Add(operation);

            _logger.LogInformation(
                "Scheduled {OperationType} maintenance for cluster {ClusterName} at {ScheduledTime}",
                operationType, cluster.Name, operation.ScheduledTime);

            return operation;
        }

        public async Task<MaintenanceWindow> ConfigureMaintenanceWindowAsync(
            string tenantId,
            string clusterId,
            MaintenanceWindow window,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            if (!_maintenanceWindows.ContainsKey(tenantId))
                _maintenanceWindows[tenantId] = new();

            window.ClusterId = clusterId;
            _maintenanceWindows[tenantId][clusterId] = window;

            _logger.LogInformation(
                "Configured maintenance window for cluster {ClusterName}: Day {Day} at {Hour}:00",
                cluster.Name, window.DayOfWeek, window.StartHour);

            return window;
        }

        #endregion

        #region Manifests

        public async Task<string> GenerateManifestsAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            return cluster.Engine switch
            {
                DatabaseEngine.PostgreSQL => GenerateCloudNativePGManifest(cluster),
                DatabaseEngine.MySQL => GeneratePerconaXtraDBManifest(cluster),
                DatabaseEngine.Vitess => GenerateVitessManifest(cluster),
                _ => GenerateGenericManifest(cluster)
            };
        }

        private string GenerateCloudNativePGManifest(DatabaseCluster cluster)
        {
            var sb = new StringBuilder();

            // CloudNativePG Cluster resource
            sb.AppendLine($@"apiVersion: postgresql.cnpg.io/v1
kind: Cluster
metadata:
  name: {cluster.Name}
  namespace: {cluster.Namespace}
spec:
  instances: {cluster.Instances}
  imageName: ghcr.io/cloudnative-pg/postgresql:{cluster.Version}

  postgresql:
    parameters:
      shared_buffers: ""256MB""
      max_connections: ""100""
      pg_stat_statements.track: all

  bootstrap:
    initdb:
      database: app
      owner: app

  storage:
    size: {cluster.Storage.Size}
    storageClass: {cluster.Storage.StorageClass}

  resources:
    requests:
      cpu: {cluster.Resources.CpuRequest}
      memory: {cluster.Resources.MemoryRequest}
    limits:
      cpu: {cluster.Resources.CpuLimit}
      memory: {cluster.Resources.MemoryLimit}");

            // Backup configuration
            if (cluster.Backup.Enabled)
            {
                sb.AppendLine($@"
  backup:
    barmanObjectStore:
      destinationPath: s3://{cluster.Backup.Destination.Bucket}/{cluster.Name}/
      s3Credentials:
        accessKeyId:
          name: {cluster.Backup.Destination.CredentialsSecret}
          key: ACCESS_KEY_ID
        secretAccessKey:
          name: {cluster.Backup.Destination.CredentialsSecret}
          key: SECRET_ACCESS_KEY
      wal:
        compression: {cluster.Backup.Compression}
    retentionPolicy: ""{cluster.Backup.Retention.Daily}d""");
            }

            // Replication configuration
            if (cluster.Replication.Mode == ReplicationMode.Sync)
            {
                sb.AppendLine($@"
  minSyncReplicas: {cluster.Replication.SyncReplicas}
  maxSyncReplicas: {cluster.Replication.SyncReplicas}");
            }

            // Connection pooler
            if (cluster.ConnectionPooler != null)
            {
                sb.AppendLine($@"
---
apiVersion: postgresql.cnpg.io/v1
kind: Pooler
metadata:
  name: {cluster.Name}-pooler
  namespace: {cluster.Namespace}
spec:
  cluster:
    name: {cluster.Name}
  instances: {cluster.ConnectionPooler.Instances}
  type: rw
  pgbouncer:
    poolMode: {cluster.ConnectionPooler.PoolMode}
    parameters:
      default_pool_size: ""{cluster.ConnectionPooler.PoolSize}""
      max_client_conn: ""{cluster.ConnectionPooler.MaxClientConnections}""");
            }

            // Scheduled backup
            if (cluster.Backup.Enabled)
            {
                sb.AppendLine($@"
---
apiVersion: postgresql.cnpg.io/v1
kind: ScheduledBackup
metadata:
  name: {cluster.Name}-scheduled-backup
  namespace: {cluster.Namespace}
spec:
  schedule: ""{cluster.Backup.Schedule}""
  cluster:
    name: {cluster.Name}
  immediate: true
  backupOwnerReference: self");
            }

            return sb.ToString();
        }

        private string GeneratePerconaXtraDBManifest(DatabaseCluster cluster)
        {
            return $@"apiVersion: pxc.percona.com/v1
kind: PerconaXtraDBCluster
metadata:
  name: {cluster.Name}
  namespace: {cluster.Namespace}
spec:
  crVersion: 1.13.0
  secretsName: {cluster.Name}-secrets
  allowUnsafeConfigurations: false

  pxc:
    size: {cluster.Instances}
    image: percona/percona-xtradb-cluster:{cluster.Version}
    resources:
      requests:
        cpu: {cluster.Resources.CpuRequest}
        memory: {cluster.Resources.MemoryRequest}
      limits:
        cpu: {cluster.Resources.CpuLimit}
        memory: {cluster.Resources.MemoryLimit}
    volumeSpec:
      persistentVolumeClaim:
        storageClassName: {cluster.Storage.StorageClass}
        resources:
          requests:
            storage: {cluster.Storage.Size}

  haproxy:
    enabled: true
    size: 2
    image: percona/percona-xtradb-cluster-operator:1.13.0-haproxy

  proxysql:
    enabled: false

  backup:
    image: percona/percona-xtradb-cluster-operator:1.13.0-pxc8.0-backup
    storages:
      s3-backup:
        type: s3
        s3:
          bucket: {cluster.Backup.Destination.Bucket}
          credentialsSecret: {cluster.Backup.Destination.CredentialsSecret}
    schedule:
      - name: daily-backup
        schedule: ""{cluster.Backup.Schedule}""
        keep: {cluster.Backup.Retention.Daily}
        storageName: s3-backup";
        }

        private string GenerateVitessManifest(DatabaseCluster cluster)
        {
            return $@"apiVersion: planetscale.com/v2
kind: VitessCluster
metadata:
  name: {cluster.Name}
  namespace: {cluster.Namespace}
spec:
  images:
    vtctld: vitess/vtctld:v{cluster.Version}
    vtgate: vitess/vtgate:v{cluster.Version}
    vttablet: vitess/vttablet:v{cluster.Version}
    vtbackup: vitess/vtbackup:v{cluster.Version}

  cells:
    - name: zone1
      gateway:
        replicas: 2
        resources:
          requests:
            cpu: {cluster.Resources.CpuRequest}
            memory: {cluster.Resources.MemoryRequest}

  vitessDashboard:
    replicas: 1

  keyspaces:
    - name: main
      turndownPolicy: Immediate
      partitionings:
        - equal:
            parts: 2
            shardTemplate:
              databaseInitScriptSecret:
                name: {cluster.Name}-init-script
                key: init.sql
              replication:
                enforceSemiSync: {(cluster.Replication.Mode == ReplicationMode.SemiSync).ToString().ToLower()}
              tabletPools:
                - cell: zone1
                  type: replica
                  replicas: {cluster.Instances}
                  vttablet:
                    resources:
                      requests:
                        cpu: {cluster.Resources.CpuRequest}
                        memory: {cluster.Resources.MemoryRequest}
                  mysqld:
                    resources:
                      requests:
                        cpu: {cluster.Resources.CpuRequest}
                        memory: {cluster.Resources.MemoryRequest}
                  dataVolumeClaimTemplate:
                    storageClassName: {cluster.Storage.StorageClass}
                    resources:
                      requests:
                        storage: {cluster.Storage.Size}";
        }

        private string GenerateGenericManifest(DatabaseCluster cluster)
        {
            return $@"# Generic database cluster configuration
# Engine: {cluster.Engine}
# Name: {cluster.Name}
# Instances: {cluster.Instances}

apiVersion: v1
kind: ConfigMap
metadata:
  name: {cluster.Name}-config
  namespace: {cluster.Namespace}
data:
  engine: ""{cluster.Engine}""
  version: ""{cluster.Version}""
  instances: ""{cluster.Instances}""
  storage_size: ""{cluster.Storage.Size}""
  storage_class: ""{cluster.Storage.StorageClass}""";
        }

        #endregion
    }

    #endregion
}
