// =============================================================================
// Cache Infrastructure Engine
// Kubernetes-native cache management with Redis, Valkey, Dragonfly
// Based on: Redis Operator, Valkey, Dragonfly, KeyDB
// Research: https://ot-container-kit.github.io/redis-operator/
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
    /// Cache platform type
    /// </summary>
    public enum CachePlatform
    {
        Redis,
        Valkey,
        Dragonfly,
        KeyDB,
        Memcached
    }

    /// <summary>
    /// Cache cluster mode
    /// </summary>
    public enum CacheClusterMode
    {
        Standalone,        // Single instance
        Sentinel,          // Redis Sentinel HA
        Cluster,           // Redis Cluster (sharding)
        Replication       // Primary-replica replication
    }

    /// <summary>
    /// Eviction policy
    /// </summary>
    public enum EvictionPolicy
    {
        NoEviction,
        AllKeysLRU,
        AllKeysLFU,
        AllKeysRandom,
        VolatileLRU,
        VolatileLFU,
        VolatileRandom,
        VolatileTTL
    }

    /// <summary>
    /// Persistence mode
    /// </summary>
    public enum PersistenceMode
    {
        None,
        RDB,               // Snapshot-based persistence
        AOF,               // Append-only file
        RDBAndAOF          // Both RDB and AOF
    }

    /// <summary>
    /// Cache node role
    /// </summary>
    public enum CacheNodeRole
    {
        Primary,
        Replica,
        Sentinel
    }

    #endregion

    #region Core Types

    /// <summary>
    /// Cache cluster specification
    /// </summary>
    public class CacheCluster
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = "default";

        /// <summary>
        /// Cache platform
        /// </summary>
        public CachePlatform Platform { get; set; } = CachePlatform.Redis;

        /// <summary>
        /// Platform version
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Cluster mode
        /// </summary>
        public CacheClusterMode Mode { get; set; } = CacheClusterMode.Sentinel;

        /// <summary>
        /// Cluster size configuration
        /// </summary>
        public CacheClusterSize Size { get; set; } = new();

        /// <summary>
        /// Resource requirements
        /// </summary>
        public ResourceRequirements Resources { get; set; } = new();

        /// <summary>
        /// Persistence configuration
        /// </summary>
        public CachePersistenceConfig Persistence { get; set; } = new();

        /// <summary>
        /// Memory configuration
        /// </summary>
        public CacheMemoryConfig Memory { get; set; } = new();

        /// <summary>
        /// Security configuration
        /// </summary>
        public CacheSecurityConfig Security { get; set; } = new();

        /// <summary>
        /// Sentinel configuration (for Sentinel mode)
        /// </summary>
        public SentinelConfig? Sentinel { get; set; }

        /// <summary>
        /// Cluster configuration (for Cluster mode)
        /// </summary>
        public RedisClusterConfig? ClusterConfig { get; set; }

        /// <summary>
        /// Monitoring configuration
        /// </summary>
        public CacheMonitoringConfig Monitoring { get; set; } = new();

        /// <summary>
        /// Redis configuration overrides
        /// </summary>
        public Dictionary<string, string> Config { get; set; } = new();

        /// <summary>
        /// Labels
        /// </summary>
        public Dictionary<string, string> Labels { get; set; } = new();

        /// <summary>
        /// Cluster status
        /// </summary>
        public CacheClusterStatus Status { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Cluster size configuration
    /// </summary>
    public class CacheClusterSize
    {
        /// <summary>
        /// Number of primary nodes (masters)
        /// </summary>
        public int Primaries { get; set; } = 1;

        /// <summary>
        /// Replicas per primary
        /// </summary>
        public int ReplicasPerPrimary { get; set; } = 2;

        /// <summary>
        /// Number of sentinel nodes
        /// </summary>
        public int Sentinels { get; set; } = 3;

        /// <summary>
        /// Total nodes
        /// </summary>
        public int TotalNodes => Mode == CacheClusterMode.Sentinel
            ? Primaries + (Primaries * ReplicasPerPrimary) + Sentinels
            : Primaries + (Primaries * ReplicasPerPrimary);

        public CacheClusterMode Mode { get; set; } = CacheClusterMode.Sentinel;
    }

    /// <summary>
    /// Persistence configuration
    /// </summary>
    public class CachePersistenceConfig
    {
        /// <summary>
        /// Persistence mode
        /// </summary>
        public PersistenceMode Mode { get; set; } = PersistenceMode.AOF;

        /// <summary>
        /// Storage size
        /// </summary>
        public string StorageSize { get; set; } = "10Gi";

        /// <summary>
        /// Storage class
        /// </summary>
        public string StorageClass { get; set; } = "standard";

        /// <summary>
        /// RDB configuration
        /// </summary>
        public RDBConfig? RDB { get; set; }

        /// <summary>
        /// AOF configuration
        /// </summary>
        public AOFConfig? AOF { get; set; }
    }

    /// <summary>
    /// RDB (snapshot) configuration
    /// </summary>
    public class RDBConfig
    {
        /// <summary>
        /// Save rules (seconds changes)
        /// </summary>
        public List<string> SaveRules { get; set; } = new()
        {
            "900 1",    // After 900 sec if at least 1 key changed
            "300 10",   // After 300 sec if at least 10 keys changed
            "60 10000"  // After 60 sec if at least 10000 keys changed
        };

        /// <summary>
        /// RDB compression
        /// </summary>
        public bool Compression { get; set; } = true;

        /// <summary>
        /// Checksum
        /// </summary>
        public bool Checksum { get; set; } = true;
    }

    /// <summary>
    /// AOF configuration
    /// </summary>
    public class AOFConfig
    {
        /// <summary>
        /// AOF fsync policy
        /// </summary>
        public string Fsync { get; set; } = "everysec"; // always, everysec, no

        /// <summary>
        /// Auto rewrite percentage
        /// </summary>
        public int AutoRewritePercentage { get; set; } = 100;

        /// <summary>
        /// Auto rewrite minimum size
        /// </summary>
        public string AutoRewriteMinSize { get; set; } = "64mb";

        /// <summary>
        /// Use RDB preamble
        /// </summary>
        public bool UseRDBPreamble { get; set; } = true;
    }

    /// <summary>
    /// Memory configuration
    /// </summary>
    public class CacheMemoryConfig
    {
        /// <summary>
        /// Max memory (e.g., "2gb")
        /// </summary>
        public string MaxMemory { get; set; } = "1gb";

        /// <summary>
        /// Eviction policy
        /// </summary>
        public EvictionPolicy EvictionPolicy { get; set; } = EvictionPolicy.AllKeysLRU;

        /// <summary>
        /// Max memory samples for LRU
        /// </summary>
        public int MaxMemorySamples { get; set; } = 5;

        /// <summary>
        /// Enable active memory defragmentation
        /// </summary>
        public bool ActiveDefrag { get; set; } = true;
    }

    /// <summary>
    /// Security configuration
    /// </summary>
    public class CacheSecurityConfig
    {
        /// <summary>
        /// Enable password authentication
        /// </summary>
        public bool RequirePassword { get; set; } = true;

        /// <summary>
        /// Secret containing password
        /// </summary>
        public string PasswordSecretName { get; set; } = string.Empty;

        /// <summary>
        /// Enable TLS
        /// </summary>
        public bool TLSEnabled { get; set; } = false;

        /// <summary>
        /// TLS certificate secret
        /// </summary>
        public string? TLSSecretName { get; set; }

        /// <summary>
        /// Enable ACLs (Redis 6+)
        /// </summary>
        public bool ACLEnabled { get; set; } = true;

        /// <summary>
        /// ACL file
        /// </summary>
        public string? ACLFile { get; set; }
    }

    /// <summary>
    /// Sentinel configuration
    /// </summary>
    public class SentinelConfig
    {
        /// <summary>
        /// Quorum for failover
        /// </summary>
        public int Quorum { get; set; } = 2;

        /// <summary>
        /// Down-after-milliseconds
        /// </summary>
        public int DownAfterMs { get; set; } = 5000;

        /// <summary>
        /// Failover timeout
        /// </summary>
        public int FailoverTimeoutMs { get; set; } = 60000;

        /// <summary>
        /// Parallel syncs during failover
        /// </summary>
        public int ParallelSyncs { get; set; } = 1;

        /// <summary>
        /// Resources for sentinel nodes
        /// </summary>
        public ResourceRequirements Resources { get; set; } = new()
        {
            CpuRequest = "100m",
            MemoryRequest = "128Mi"
        };
    }

    /// <summary>
    /// Redis Cluster configuration
    /// </summary>
    public class RedisClusterConfig
    {
        /// <summary>
        /// Number of shards (masters)
        /// </summary>
        public int Shards { get; set; } = 3;

        /// <summary>
        /// Replicas per shard
        /// </summary>
        public int ReplicasPerShard { get; set; } = 1;

        /// <summary>
        /// Node timeout
        /// </summary>
        public int NodeTimeoutMs { get; set; } = 15000;

        /// <summary>
        /// Cluster require full coverage
        /// </summary>
        public bool RequireFullCoverage { get; set; } = true;

        /// <summary>
        /// Enable cluster bus encryption
        /// </summary>
        public bool ClusterBusEncryption { get; set; } = false;
    }

    /// <summary>
    /// Monitoring configuration
    /// </summary>
    public class CacheMonitoringConfig
    {
        public bool Enabled { get; set; } = true;
        public bool PrometheusExporter { get; set; } = true;
        public int ExporterPort { get; set; } = 9121;
        public bool SlowLogEnabled { get; set; } = true;
        public int SlowLogMaxLen { get; set; } = 128;
        public int SlowLogSlowerThan { get; set; } = 10000; // microseconds
    }

    /// <summary>
    /// Cluster status
    /// </summary>
    public class CacheClusterStatus
    {
        public string Phase { get; set; } = "Pending";
        public int ReadyNodes { get; set; }
        public int TotalNodes { get; set; }
        public string? PrimaryEndpoint { get; set; }
        public string? SentinelEndpoint { get; set; }
        public string? ClusterEndpoint { get; set; }
        public List<CacheNodeStatus> Nodes { get; set; } = new();
        public List<ClusterCondition> Conditions { get; set; } = new();
        public CacheClusterMetrics? Metrics { get; set; }
    }

    /// <summary>
    /// Cache node status
    /// </summary>
    public class CacheNodeStatus
    {
        public string Name { get; set; } = string.Empty;
        public string PodName { get; set; } = string.Empty;
        public CacheNodeRole Role { get; set; }
        public bool Ready { get; set; }
        public string? Address { get; set; }
        public int? Port { get; set; }
        public string? MasterId { get; set; }
        public long? ReplicationOffset { get; set; }
        public string? SlotRange { get; set; }
    }

    /// <summary>
    /// Cluster metrics
    /// </summary>
    public class CacheClusterMetrics
    {
        public long ConnectedClients { get; set; }
        public long UsedMemoryBytes { get; set; }
        public long TotalKeys { get; set; }
        public double HitRate { get; set; }
        public long OpsPerSecond { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    #endregion

    #region User Types

    /// <summary>
    /// Cache user (ACL)
    /// </summary>
    public class CacheUser
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClusterId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// User is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Password hashes
        /// </summary>
        public List<string> PasswordHashes { get; set; } = new();

        /// <summary>
        /// Password secret
        /// </summary>
        public string? PasswordSecretName { get; set; }

        /// <summary>
        /// Key patterns (read access)
        /// </summary>
        public List<string> KeyPatterns { get; set; } = new() { "~*" };

        /// <summary>
        /// Pub/Sub channels
        /// </summary>
        public List<string> Channels { get; set; } = new() { "&*" };

        /// <summary>
        /// Allowed commands
        /// </summary>
        public List<string> AllowedCommands { get; set; } = new() { "+@all" };

        /// <summary>
        /// Denied commands
        /// </summary>
        public List<string> DeniedCommands { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Cache user template (common patterns)
    /// </summary>
    public class CacheUserTemplate
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> AllowedCommands { get; set; } = new();
        public List<string> DeniedCommands { get; set; } = new();
        public List<string> KeyPatterns { get; set; } = new();
    }

    #endregion

    #region Backup Types

    /// <summary>
    /// Cache backup
    /// </summary>
    public class CacheBackup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClusterId { get; set; } = string.Empty;
        public string ClusterName { get; set; } = string.Empty;

        /// <summary>
        /// Backup type (rdb, aof)
        /// </summary>
        public string Type { get; set; } = "rdb";

        /// <summary>
        /// Backup status
        /// </summary>
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Backup location
        /// </summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// Backup size in bytes
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Database size at backup time
        /// </summary>
        public long DatabaseSizeBytes { get; set; }

        /// <summary>
        /// Keys count at backup time
        /// </summary>
        public long KeysCount { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Backup schedule
    /// </summary>
    public class CacheBackupSchedule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClusterId { get; set; } = string.Empty;

        /// <summary>
        /// Schedule (cron format)
        /// </summary>
        public string Schedule { get; set; } = "0 0 * * *";

        /// <summary>
        /// Backup destination
        /// </summary>
        public BackupDestination Destination { get; set; } = new();

        /// <summary>
        /// Retention count
        /// </summary>
        public int RetentionCount { get; set; } = 7;

        public bool Enabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    #endregion

    #region Connection Types

    /// <summary>
    /// Connection pool configuration
    /// </summary>
    public class ConnectionPoolConfig
    {
        /// <summary>
        /// Minimum idle connections
        /// </summary>
        public int MinIdle { get; set; } = 10;

        /// <summary>
        /// Maximum active connections
        /// </summary>
        public int MaxActive { get; set; } = 100;

        /// <summary>
        /// Maximum idle connections
        /// </summary>
        public int MaxIdle { get; set; } = 50;

        /// <summary>
        /// Max wait time for connection
        /// </summary>
        public TimeSpan MaxWait { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Connection timeout
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Read timeout
        /// </summary>
        public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Connection string builder
    /// </summary>
    public class CacheConnectionInfo
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 6379;
        public string? Password { get; set; }
        public bool UseTLS { get; set; }
        public int Database { get; set; } = 0;

        /// <summary>
        /// Sentinel configuration
        /// </summary>
        public SentinelConnectionInfo? Sentinel { get; set; }

        /// <summary>
        /// Cluster endpoints
        /// </summary>
        public List<string>? ClusterEndpoints { get; set; }

        /// <summary>
        /// Generate connection string
        /// </summary>
        public string ToConnectionString()
        {
            var sb = new StringBuilder();

            if (Sentinel != null)
            {
                sb.Append(string.Join(",", Sentinel.SentinelEndpoints));
                sb.Append($",serviceName={Sentinel.MasterName}");
            }
            else if (ClusterEndpoints?.Any() == true)
            {
                sb.Append(string.Join(",", ClusterEndpoints));
            }
            else
            {
                sb.Append($"{Host}:{Port}");
            }

            if (!string.IsNullOrEmpty(Password))
                sb.Append($",password={Password}");

            if (UseTLS)
                sb.Append(",ssl=true");

            if (Database > 0)
                sb.Append($",defaultDatabase={Database}");

            return sb.ToString();
        }
    }

    /// <summary>
    /// Sentinel connection info
    /// </summary>
    public class SentinelConnectionInfo
    {
        public List<string> SentinelEndpoints { get; set; } = new();
        public string MasterName { get; set; } = string.Empty;
        public string? SentinelPassword { get; set; }
    }

    #endregion

    #region Analytics Types

    /// <summary>
    /// Key analysis result
    /// </summary>
    public class KeyAnalysis
    {
        public string ClusterId { get; set; } = string.Empty;
        public long TotalKeys { get; set; }
        public long TotalMemoryBytes { get; set; }

        /// <summary>
        /// Keys by type
        /// </summary>
        public Dictionary<string, long> KeysByType { get; set; } = new();

        /// <summary>
        /// Memory by type
        /// </summary>
        public Dictionary<string, long> MemoryByType { get; set; } = new();

        /// <summary>
        /// Top keys by memory
        /// </summary>
        public List<KeyInfo> TopKeysByMemory { get; set; } = new();

        /// <summary>
        /// Keys with no TTL
        /// </summary>
        public long KeysWithoutTTL { get; set; }

        /// <summary>
        /// Key patterns found
        /// </summary>
        public List<KeyPattern> Patterns { get; set; } = new();

        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Key information
    /// </summary>
    public class KeyInfo
    {
        public string Key { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long MemoryBytes { get; set; }
        public TimeSpan? TTL { get; set; }
        public long? Length { get; set; }
    }

    /// <summary>
    /// Key pattern analysis
    /// </summary>
    public class KeyPattern
    {
        public string Pattern { get; set; } = string.Empty;
        public long KeyCount { get; set; }
        public long TotalMemoryBytes { get; set; }
        public double PercentOfTotal { get; set; }
    }

    /// <summary>
    /// Slow log entry
    /// </summary>
    public class SlowLogEntry
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan Duration { get; set; }
        public string Command { get; set; } = string.Empty;
        public string ClientAddress { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
    }

    #endregion

    #region Interface

    /// <summary>
    /// Cache Infrastructure Engine interface
    /// Provides Kubernetes-native cache management
    /// </summary>
    public interface ICacheInfrastructureEngine
    {
        #region Cluster Management

        /// <summary>
        /// Create cache cluster
        /// </summary>
        Task<CacheCluster> CreateClusterAsync(
            string tenantId,
            CacheCluster cluster,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get cluster by ID
        /// </summary>
        Task<CacheCluster?> GetClusterAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Update cluster
        /// </summary>
        Task<CacheCluster> UpdateClusterAsync(
            string tenantId,
            CacheCluster cluster,
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
        Task<List<CacheCluster>> ListClustersAsync(
            string tenantId,
            CachePlatform? platform = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Scale cluster
        /// </summary>
        Task<CacheCluster> ScaleClusterAsync(
            string tenantId,
            string clusterId,
            int replicasPerPrimary,
            CancellationToken cancellation = default);

        #endregion

        #region Failover

        /// <summary>
        /// Trigger manual failover
        /// </summary>
        Task<bool> TriggerFailoverAsync(
            string tenantId,
            string clusterId,
            string? targetNode = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get replication info
        /// </summary>
        Task<List<CacheNodeStatus>> GetReplicationInfoAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default);

        #endregion

        #region User Management

        /// <summary>
        /// Create cache user
        /// </summary>
        Task<CacheUser> CreateUserAsync(
            string tenantId,
            string clusterId,
            CacheUser user,
            CancellationToken cancellation = default);

        /// <summary>
        /// Delete user
        /// </summary>
        Task<bool> DeleteUserAsync(
            string tenantId,
            string clusterId,
            string username,
            CancellationToken cancellation = default);

        /// <summary>
        /// List users
        /// </summary>
        Task<List<CacheUser>> ListUsersAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get user templates
        /// </summary>
        Task<List<CacheUserTemplate>> GetUserTemplatesAsync(
            CancellationToken cancellation = default);

        #endregion

        #region Backup & Restore

        /// <summary>
        /// Create backup
        /// </summary>
        Task<CacheBackup> CreateBackupAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default);

        /// <summary>
        /// List backups
        /// </summary>
        Task<List<CacheBackup>> ListBackupsAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Restore from backup
        /// </summary>
        Task<CacheCluster> RestoreFromBackupAsync(
            string tenantId,
            string backupId,
            string targetClusterName,
            CancellationToken cancellation = default);

        /// <summary>
        /// Configure backup schedule
        /// </summary>
        Task<CacheBackupSchedule> ConfigureBackupScheduleAsync(
            string tenantId,
            string clusterId,
            CacheBackupSchedule schedule,
            CancellationToken cancellation = default);

        #endregion

        #region Analysis

        /// <summary>
        /// Analyze keys
        /// </summary>
        Task<KeyAnalysis> AnalyzeKeysAsync(
            string tenantId,
            string clusterId,
            string? pattern = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get slow log
        /// </summary>
        Task<List<SlowLogEntry>> GetSlowLogAsync(
            string tenantId,
            string clusterId,
            int count = 100,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get cluster metrics
        /// </summary>
        Task<CacheClusterMetrics> GetMetricsAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default);

        #endregion

        #region Connection

        /// <summary>
        /// Get connection info
        /// </summary>
        Task<CacheConnectionInfo> GetConnectionInfoAsync(
            string tenantId,
            string clusterId,
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

    #region Implementation

    /// <summary>
    /// Cache Infrastructure Engine implementation
    /// </summary>
    public class CacheInfrastructureEngine : ICacheInfrastructureEngine
    {
        private readonly ILogger<CacheInfrastructureEngine> _logger;
        private readonly Dictionary<string, Dictionary<string, CacheCluster>> _clusters = new();
        private readonly Dictionary<string, Dictionary<string, CacheUser>> _users = new();
        private readonly Dictionary<string, List<CacheBackup>> _backups = new();
        private readonly Dictionary<string, CacheBackupSchedule> _schedules = new();

        private readonly Random _random = new();

        public CacheInfrastructureEngine(ILogger<CacheInfrastructureEngine> logger)
        {
            _logger = logger;
        }

        #region Cluster Management

        public Task<CacheCluster> CreateClusterAsync(
            string tenantId,
            CacheCluster cluster,
            CancellationToken cancellation = default)
        {
            if (!_clusters.ContainsKey(tenantId))
                _clusters[tenantId] = new();

            cluster.TenantId = tenantId;
            cluster.CreatedAt = DateTime.UtcNow;

            // Apply platform defaults
            ApplyPlatformDefaults(cluster);

            // Simulate cluster creation
            SimulateClusterCreation(cluster);

            _clusters[tenantId][cluster.Id] = cluster;

            _logger.LogInformation(
                "Created {Platform} cache cluster {Name} in {Mode} mode",
                cluster.Platform, cluster.Name, cluster.Mode);

            return Task.FromResult(cluster);
        }

        private void ApplyPlatformDefaults(CacheCluster cluster)
        {
            switch (cluster.Platform)
            {
                case CachePlatform.Redis:
                    cluster.Version = string.IsNullOrEmpty(cluster.Version) ? "7.2" : cluster.Version;
                    break;
                case CachePlatform.Valkey:
                    cluster.Version = string.IsNullOrEmpty(cluster.Version) ? "7.2" : cluster.Version;
                    break;
                case CachePlatform.Dragonfly:
                    cluster.Version = string.IsNullOrEmpty(cluster.Version) ? "1.15" : cluster.Version;
                    cluster.Mode = CacheClusterMode.Replication; // Dragonfly doesn't use Sentinel
                    break;
                case CachePlatform.KeyDB:
                    cluster.Version = string.IsNullOrEmpty(cluster.Version) ? "6.3" : cluster.Version;
                    break;
            }

            // Set default persistence
            if (cluster.Persistence.Mode == PersistenceMode.AOF)
            {
                cluster.Persistence.AOF ??= new AOFConfig();
            }
            else if (cluster.Persistence.Mode == PersistenceMode.RDB)
            {
                cluster.Persistence.RDB ??= new RDBConfig();
            }

            // Set Sentinel defaults
            if (cluster.Mode == CacheClusterMode.Sentinel)
            {
                cluster.Sentinel ??= new SentinelConfig();
            }

            // Set Cluster defaults
            if (cluster.Mode == CacheClusterMode.Cluster)
            {
                cluster.ClusterConfig ??= new RedisClusterConfig();
                cluster.Size.Primaries = cluster.ClusterConfig.Shards;
                cluster.Size.ReplicasPerPrimary = cluster.ClusterConfig.ReplicasPerShard;
            }
        }

        private void SimulateClusterCreation(CacheCluster cluster)
        {
            cluster.Size.Mode = cluster.Mode;

            cluster.Status = new CacheClusterStatus
            {
                Phase = "Running",
                TotalNodes = cluster.Size.TotalNodes,
                ReadyNodes = cluster.Size.TotalNodes
            };

            // Create primary nodes
            for (int i = 0; i < cluster.Size.Primaries; i++)
            {
                var primary = new CacheNodeStatus
                {
                    Name = $"{cluster.Name}-{i}",
                    PodName = $"{cluster.Name}-{i}-0",
                    Role = CacheNodeRole.Primary,
                    Ready = true,
                    Address = $"{cluster.Name}-{i}.{cluster.Name}.{cluster.Namespace}.svc",
                    Port = 6379
                };

                if (cluster.Mode == CacheClusterMode.Cluster)
                {
                    var slotStart = i * (16384 / cluster.Size.Primaries);
                    var slotEnd = (i + 1) * (16384 / cluster.Size.Primaries) - 1;
                    primary.SlotRange = $"{slotStart}-{slotEnd}";
                }

                cluster.Status.Nodes.Add(primary);

                // Add replicas for this primary
                for (int r = 0; r < cluster.Size.ReplicasPerPrimary; r++)
                {
                    cluster.Status.Nodes.Add(new CacheNodeStatus
                    {
                        Name = $"{cluster.Name}-{i}-replica-{r}",
                        PodName = $"{cluster.Name}-{i}-{r + 1}",
                        Role = CacheNodeRole.Replica,
                        Ready = true,
                        Address = $"{cluster.Name}-{i}-{r + 1}.{cluster.Name}.{cluster.Namespace}.svc",
                        Port = 6379,
                        MasterId = primary.Name,
                        ReplicationOffset = _random.Next(1000000)
                    });
                }
            }

            // Add sentinel nodes
            if (cluster.Mode == CacheClusterMode.Sentinel)
            {
                for (int s = 0; s < cluster.Size.Sentinels; s++)
                {
                    cluster.Status.Nodes.Add(new CacheNodeStatus
                    {
                        Name = $"{cluster.Name}-sentinel-{s}",
                        PodName = $"{cluster.Name}-sentinel-{s}",
                        Role = CacheNodeRole.Sentinel,
                        Ready = true,
                        Address = $"{cluster.Name}-sentinel-{s}.{cluster.Name}-sentinel.{cluster.Namespace}.svc",
                        Port = 26379
                    });
                }

                cluster.Status.SentinelEndpoint = $"{cluster.Name}-sentinel.{cluster.Namespace}.svc:26379";
            }

            // Set primary endpoint
            cluster.Status.PrimaryEndpoint = cluster.Mode == CacheClusterMode.Sentinel
                ? $"{cluster.Name}.{cluster.Namespace}.svc:6379"
                : $"{cluster.Name}-0.{cluster.Name}.{cluster.Namespace}.svc:6379";

            if (cluster.Mode == CacheClusterMode.Cluster)
            {
                cluster.Status.ClusterEndpoint = $"{cluster.Name}.{cluster.Namespace}.svc:6379";
            }

            cluster.Status.Conditions.Add(new ClusterCondition
            {
                Type = "Ready",
                Status = "True",
                LastTransitionTime = DateTime.UtcNow,
                Reason = "ClusterReady",
                Message = "All nodes are running"
            });

            cluster.Status.Metrics = new CacheClusterMetrics
            {
                ConnectedClients = _random.Next(10, 100),
                UsedMemoryBytes = _random.Next(100000000, 500000000),
                TotalKeys = _random.Next(10000, 100000),
                HitRate = 0.95 + (_random.NextDouble() * 0.04),
                OpsPerSecond = _random.Next(1000, 10000),
                LastUpdated = DateTime.UtcNow
            };
        }

        public Task<CacheCluster?> GetClusterAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            if (_clusters.TryGetValue(tenantId, out var clusters) &&
                clusters.TryGetValue(clusterId, out var cluster))
            {
                return Task.FromResult<CacheCluster?>(cluster);
            }

            return Task.FromResult<CacheCluster?>(null);
        }

        public Task<CacheCluster> UpdateClusterAsync(
            string tenantId,
            CacheCluster cluster,
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
                return Task.FromResult(clusters.Remove(clusterId));
            }

            return Task.FromResult(false);
        }

        public Task<List<CacheCluster>> ListClustersAsync(
            string tenantId,
            CachePlatform? platform = null,
            CancellationToken cancellation = default)
        {
            if (!_clusters.TryGetValue(tenantId, out var clusters))
                return Task.FromResult(new List<CacheCluster>());

            var result = clusters.Values.AsEnumerable();
            if (platform.HasValue)
            {
                result = result.Where(c => c.Platform == platform.Value);
            }

            return Task.FromResult(result.ToList());
        }

        public async Task<CacheCluster> ScaleClusterAsync(
            string tenantId,
            string clusterId,
            int replicasPerPrimary,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            var oldReplicas = cluster.Size.ReplicasPerPrimary;
            cluster.Size.ReplicasPerPrimary = replicasPerPrimary;

            // Update nodes
            cluster.Status.Nodes = cluster.Status.Nodes
                .Where(n => n.Role != CacheNodeRole.Replica)
                .ToList();

            foreach (var primary in cluster.Status.Nodes.Where(n => n.Role == CacheNodeRole.Primary).ToList())
            {
                var primaryIndex = int.Parse(primary.Name.Split('-').Last());
                for (int r = 0; r < replicasPerPrimary; r++)
                {
                    cluster.Status.Nodes.Add(new CacheNodeStatus
                    {
                        Name = $"{cluster.Name}-{primaryIndex}-replica-{r}",
                        PodName = $"{cluster.Name}-{primaryIndex}-{r + 1}",
                        Role = CacheNodeRole.Replica,
                        Ready = true,
                        MasterId = primary.Name
                    });
                }
            }

            cluster.Status.TotalNodes = cluster.Size.TotalNodes;
            cluster.Status.ReadyNodes = cluster.Size.TotalNodes;

            _logger.LogInformation(
                "Scaled cluster {ClusterId} replicas from {Old} to {New} per primary",
                clusterId, oldReplicas, replicasPerPrimary);

            return cluster;
        }

        #endregion

        #region Failover

        public async Task<bool> TriggerFailoverAsync(
            string tenantId,
            string clusterId,
            string? targetNode = null,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            var currentPrimary = cluster.Status.Nodes.FirstOrDefault(n => n.Role == CacheNodeRole.Primary);
            var replicas = cluster.Status.Nodes.Where(n => n.Role == CacheNodeRole.Replica).ToList();

            if (!replicas.Any())
            {
                _logger.LogError("No replicas available for failover");
                return false;
            }

            var newPrimary = targetNode != null
                ? replicas.FirstOrDefault(n => n.Name == targetNode)
                : replicas.OrderByDescending(n => n.ReplicationOffset).First();

            if (newPrimary == null)
            {
                _logger.LogError("Target replica not found");
                return false;
            }

            // Perform failover
            if (currentPrimary != null)
            {
                currentPrimary.Role = CacheNodeRole.Replica;
                currentPrimary.MasterId = newPrimary.Name;
            }

            newPrimary.Role = CacheNodeRole.Primary;
            newPrimary.MasterId = null;
            newPrimary.ReplicationOffset = null;

            // Update other replicas
            foreach (var replica in replicas.Where(r => r.Name != newPrimary.Name))
            {
                replica.MasterId = newPrimary.Name;
            }

            _logger.LogInformation(
                "Failover completed: {OldPrimary} -> {NewPrimary}",
                currentPrimary?.Name ?? "none", newPrimary.Name);

            return true;
        }

        public async Task<List<CacheNodeStatus>> GetReplicationInfoAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            return cluster.Status.Nodes;
        }

        #endregion

        #region User Management

        public async Task<CacheUser> CreateUserAsync(
            string tenantId,
            string clusterId,
            CacheUser user,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            if (!_users.ContainsKey(clusterId))
                _users[clusterId] = new();

            user.ClusterId = clusterId;
            user.CreatedAt = DateTime.UtcNow;
            user.PasswordSecretName ??= $"{cluster.Name}-{user.Username}-credentials";

            _users[clusterId][user.Id] = user;

            _logger.LogInformation(
                "Created cache user {Username} with key patterns: {Patterns}",
                user.Username, string.Join(", ", user.KeyPatterns));

            return user;
        }

        public async Task<bool> DeleteUserAsync(
            string tenantId,
            string clusterId,
            string username,
            CancellationToken cancellation = default)
        {
            if (_users.TryGetValue(clusterId, out var users))
            {
                var user = users.Values.FirstOrDefault(u => u.Username == username);
                if (user != null)
                {
                    return users.Remove(user.Id);
                }
            }

            return false;
        }

        public Task<List<CacheUser>> ListUsersAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            if (!_users.TryGetValue(clusterId, out var users))
                return Task.FromResult(new List<CacheUser>());

            return Task.FromResult(users.Values.ToList());
        }

        public Task<List<CacheUserTemplate>> GetUserTemplatesAsync(
            CancellationToken cancellation = default)
        {
            var templates = new List<CacheUserTemplate>
            {
                new CacheUserTemplate
                {
                    Name = "readonly",
                    Description = "Read-only access to all keys",
                    AllowedCommands = new List<string> { "+@read", "+@connection" },
                    DeniedCommands = new List<string> { "-@write", "-@admin", "-@dangerous" },
                    KeyPatterns = new List<string> { "~*" }
                },
                new CacheUserTemplate
                {
                    Name = "readwrite",
                    Description = "Read-write access to all keys",
                    AllowedCommands = new List<string> { "+@all" },
                    DeniedCommands = new List<string> { "-@admin", "-@dangerous" },
                    KeyPatterns = new List<string> { "~*" }
                },
                new CacheUserTemplate
                {
                    Name = "admin",
                    Description = "Full administrative access",
                    AllowedCommands = new List<string> { "+@all" },
                    DeniedCommands = new List<string>(),
                    KeyPatterns = new List<string> { "~*" }
                },
                new CacheUserTemplate
                {
                    Name = "application",
                    Description = "Typical application user with prefix restriction",
                    AllowedCommands = new List<string> { "+@read", "+@write", "+@connection", "+@fast" },
                    DeniedCommands = new List<string> { "-@admin", "-@slow", "-@dangerous", "-KEYS" },
                    KeyPatterns = new List<string> { "~app:*" }
                }
            };

            return Task.FromResult(templates);
        }

        #endregion

        #region Backup & Restore

        public async Task<CacheBackup> CreateBackupAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            if (!_backups.ContainsKey(clusterId))
                _backups[clusterId] = new();

            var backup = new CacheBackup
            {
                ClusterId = clusterId,
                ClusterName = cluster.Name,
                Type = cluster.Persistence.Mode == PersistenceMode.AOF ? "aof" : "rdb",
                StartTime = DateTime.UtcNow,
                Status = "Completed",
                EndTime = DateTime.UtcNow.AddSeconds(30),
                SizeBytes = _random.Next(10000000, 100000000),
                DatabaseSizeBytes = cluster.Status.Metrics?.UsedMemoryBytes ?? 0,
                KeysCount = cluster.Status.Metrics?.TotalKeys ?? 0,
                Location = $"s3://backups/{cluster.Name}/{DateTime.UtcNow:yyyyMMddHHmmss}.rdb"
            };

            _backups[clusterId].Add(backup);

            _logger.LogInformation(
                "Created backup {BackupId} for cluster {ClusterName}",
                backup.Id, cluster.Name);

            return backup;
        }

        public Task<List<CacheBackup>> ListBackupsAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            if (!_backups.TryGetValue(clusterId, out var backups))
                return Task.FromResult(new List<CacheBackup>());

            return Task.FromResult(backups.OrderByDescending(b => b.StartTime).ToList());
        }

        public async Task<CacheCluster> RestoreFromBackupAsync(
            string tenantId,
            string backupId,
            string targetClusterName,
            CancellationToken cancellation = default)
        {
            // Find backup
            CacheBackup? backup = null;
            CacheCluster? sourceCluster = null;

            foreach (var clusterId in _backups.Keys)
            {
                backup = _backups[clusterId].FirstOrDefault(b => b.Id == backupId);
                if (backup != null)
                {
                    sourceCluster = await GetClusterAsync(tenantId, clusterId, cancellation);
                    break;
                }
            }

            if (backup == null || sourceCluster == null)
                throw new InvalidOperationException($"Backup {backupId} not found");

            // Create new cluster from backup
            var newCluster = new CacheCluster
            {
                Name = targetClusterName,
                Namespace = sourceCluster.Namespace,
                Platform = sourceCluster.Platform,
                Version = sourceCluster.Version,
                Mode = sourceCluster.Mode,
                Size = sourceCluster.Size,
                Resources = sourceCluster.Resources,
                Persistence = sourceCluster.Persistence,
                Memory = sourceCluster.Memory,
                Security = sourceCluster.Security,
                Labels = new Dictionary<string, string>
                {
                    ["restored-from"] = backup.Id
                }
            };

            return await CreateClusterAsync(tenantId, newCluster, cancellation);
        }

        public Task<CacheBackupSchedule> ConfigureBackupScheduleAsync(
            string tenantId,
            string clusterId,
            CacheBackupSchedule schedule,
            CancellationToken cancellation = default)
        {
            schedule.ClusterId = clusterId;
            schedule.CreatedAt = DateTime.UtcNow;

            _schedules[clusterId] = schedule;

            _logger.LogInformation(
                "Configured backup schedule for cluster {ClusterId}: {Schedule}",
                clusterId, schedule.Schedule);

            return Task.FromResult(schedule);
        }

        #endregion

        #region Analysis

        public async Task<KeyAnalysis> AnalyzeKeysAsync(
            string tenantId,
            string clusterId,
            string? pattern = null,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            // Simulate key analysis
            var analysis = new KeyAnalysis
            {
                ClusterId = clusterId,
                TotalKeys = cluster.Status.Metrics?.TotalKeys ?? _random.Next(10000, 100000),
                TotalMemoryBytes = cluster.Status.Metrics?.UsedMemoryBytes ?? _random.Next(100000000, 500000000)
            };

            analysis.KeysByType = new Dictionary<string, long>
            {
                ["string"] = (long)(analysis.TotalKeys * 0.6),
                ["hash"] = (long)(analysis.TotalKeys * 0.2),
                ["list"] = (long)(analysis.TotalKeys * 0.1),
                ["set"] = (long)(analysis.TotalKeys * 0.05),
                ["zset"] = (long)(analysis.TotalKeys * 0.05)
            };

            analysis.MemoryByType = new Dictionary<string, long>
            {
                ["string"] = (long)(analysis.TotalMemoryBytes * 0.5),
                ["hash"] = (long)(analysis.TotalMemoryBytes * 0.3),
                ["list"] = (long)(analysis.TotalMemoryBytes * 0.1),
                ["set"] = (long)(analysis.TotalMemoryBytes * 0.05),
                ["zset"] = (long)(analysis.TotalMemoryBytes * 0.05)
            };

            analysis.TopKeysByMemory = new List<KeyInfo>
            {
                new KeyInfo { Key = "cache:large-object", Type = "hash", MemoryBytes = 10000000, TTL = TimeSpan.FromHours(24) },
                new KeyInfo { Key = "session:data", Type = "hash", MemoryBytes = 5000000, TTL = TimeSpan.FromHours(1) },
                new KeyInfo { Key = "user:profiles", Type = "hash", MemoryBytes = 3000000, TTL = null }
            };

            analysis.KeysWithoutTTL = (long)(analysis.TotalKeys * 0.3);

            analysis.Patterns = new List<KeyPattern>
            {
                new KeyPattern { Pattern = "cache:*", KeyCount = (long)(analysis.TotalKeys * 0.4), PercentOfTotal = 40 },
                new KeyPattern { Pattern = "session:*", KeyCount = (long)(analysis.TotalKeys * 0.3), PercentOfTotal = 30 },
                new KeyPattern { Pattern = "user:*", KeyCount = (long)(analysis.TotalKeys * 0.2), PercentOfTotal = 20 },
                new KeyPattern { Pattern = "other", KeyCount = (long)(analysis.TotalKeys * 0.1), PercentOfTotal = 10 }
            };

            return analysis;
        }

        public async Task<List<SlowLogEntry>> GetSlowLogAsync(
            string tenantId,
            string clusterId,
            int count = 100,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            // Simulate slow log
            var entries = new List<SlowLogEntry>();
            var commands = new[] { "KEYS *", "SMEMBERS largeset", "HGETALL bighash", "LRANGE list 0 -1", "SCAN 0" };

            for (int i = 0; i < Math.Min(count, 10); i++)
            {
                entries.Add(new SlowLogEntry
                {
                    Id = i,
                    Timestamp = DateTime.UtcNow.AddMinutes(-_random.Next(1, 60)),
                    Duration = TimeSpan.FromMicroseconds(_random.Next(10000, 100000)),
                    Command = commands[_random.Next(commands.Length)],
                    ClientAddress = $"10.0.0.{_random.Next(1, 255)}:12345",
                    ClientName = $"app-{_random.Next(1, 10)}"
                });
            }

            return entries.OrderByDescending(e => e.Timestamp).ToList();
        }

        public async Task<CacheClusterMetrics> GetMetricsAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            return cluster.Status.Metrics ?? new CacheClusterMetrics
            {
                LastUpdated = DateTime.UtcNow
            };
        }

        #endregion

        #region Connection

        public async Task<CacheConnectionInfo> GetConnectionInfoAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            var info = new CacheConnectionInfo
            {
                Host = cluster.Status.PrimaryEndpoint?.Split(':')[0] ?? $"{cluster.Name}.{cluster.Namespace}.svc",
                Port = 6379,
                UseTLS = cluster.Security.TLSEnabled
            };

            if (cluster.Mode == CacheClusterMode.Sentinel)
            {
                info.Sentinel = new SentinelConnectionInfo
                {
                    MasterName = cluster.Name,
                    SentinelEndpoints = cluster.Status.Nodes
                        .Where(n => n.Role == CacheNodeRole.Sentinel)
                        .Select(n => $"{n.Address}:{n.Port}")
                        .ToList()
                };
            }

            if (cluster.Mode == CacheClusterMode.Cluster)
            {
                info.ClusterEndpoints = cluster.Status.Nodes
                    .Where(n => n.Role == CacheNodeRole.Primary)
                    .Select(n => $"{n.Address}:{n.Port}")
                    .ToList();
            }

            return info;
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

            return cluster.Mode switch
            {
                CacheClusterMode.Sentinel => GenerateSentinelManifest(cluster),
                CacheClusterMode.Cluster => GenerateClusterManifest(cluster),
                CacheClusterMode.Replication => GenerateReplicationManifest(cluster),
                _ => GenerateStandaloneManifest(cluster)
            };
        }

        private string GenerateSentinelManifest(CacheCluster cluster)
        {
            var evictionPolicy = cluster.Memory.EvictionPolicy.ToString().ToLower().Replace("allkeys", "allkeys-").Replace("volatile", "volatile-");

            return $@"apiVersion: redis.redis.opstreelabs.in/v1beta2
kind: Redis
metadata:
  name: {cluster.Name}
  namespace: {cluster.Namespace}
spec:
  clusterSize: {cluster.Size.Primaries}
  kubernetesConfig:
    image: redis:{cluster.Version}
    imagePullPolicy: IfNotPresent
    resources:
      requests:
        cpu: {cluster.Resources.CpuRequest}
        memory: {cluster.Resources.MemoryRequest}
      limits:
        cpu: {cluster.Resources.CpuLimit}
        memory: {cluster.Resources.MemoryLimit}

  redisConfig:
    additionalRedisConfig: |
      maxmemory {cluster.Memory.MaxMemory}
      maxmemory-policy {evictionPolicy}
      maxmemory-samples {cluster.Memory.MaxMemorySamples}
{(cluster.Persistence.Mode == PersistenceMode.AOF ? $@"      appendonly yes
      appendfsync {cluster.Persistence.AOF?.Fsync ?? "everysec"}" : "      appendonly no")}

  storage:
    volumeClaimTemplate:
      spec:
        storageClassName: {cluster.Persistence.StorageClass}
        accessModes: [""ReadWriteOnce""]
        resources:
          requests:
            storage: {cluster.Persistence.StorageSize}

  redisExporter:
    enabled: {cluster.Monitoring.PrometheusExporter.ToString().ToLower()}
    image: oliver006/redis_exporter:latest
---
apiVersion: redis.redis.opstreelabs.in/v1beta2
kind: RedisSentinel
metadata:
  name: {cluster.Name}-sentinel
  namespace: {cluster.Namespace}
spec:
  clusterSize: {cluster.Size.Sentinels}
  redisSentinelConfig:
    redisReplicationName: {cluster.Name}
    quorum: ""{cluster.Sentinel?.Quorum ?? 2}""
    downAfterMilliseconds: ""{cluster.Sentinel?.DownAfterMs ?? 5000}""
    failoverTimeout: ""{cluster.Sentinel?.FailoverTimeoutMs ?? 60000}""
    parallelSyncs: ""{cluster.Sentinel?.ParallelSyncs ?? 1}""
  kubernetesConfig:
    image: redis:{cluster.Version}
    resources:
      requests:
        cpu: {cluster.Sentinel?.Resources.CpuRequest ?? "100m"}
        memory: {cluster.Sentinel?.Resources.MemoryRequest ?? "128Mi"}";
        }

        private string GenerateClusterManifest(CacheCluster cluster)
        {
            return $@"apiVersion: redis.redis.opstreelabs.in/v1beta2
kind: RedisCluster
metadata:
  name: {cluster.Name}
  namespace: {cluster.Namespace}
spec:
  clusterSize: {cluster.ClusterConfig?.Shards ?? 3}
  clusterVersion: v7
  persistenceEnabled: {(cluster.Persistence.Mode != PersistenceMode.None).ToString().ToLower()}
  kubernetesConfig:
    image: redis:{cluster.Version}
    imagePullPolicy: IfNotPresent
    resources:
      requests:
        cpu: {cluster.Resources.CpuRequest}
        memory: {cluster.Resources.MemoryRequest}
      limits:
        cpu: {cluster.Resources.CpuLimit}
        memory: {cluster.Resources.MemoryLimit}

  redisLeader:
    replicas: {cluster.ClusterConfig?.Shards ?? 3}
    redisConfig:
      additionalRedisConfig: |
        maxmemory {cluster.Memory.MaxMemory}
        cluster-enabled yes
        cluster-node-timeout {cluster.ClusterConfig?.NodeTimeoutMs ?? 15000}
        cluster-require-full-coverage {(cluster.ClusterConfig?.RequireFullCoverage ?? true).ToString().ToLower()}

  redisFollower:
    replicas: {(cluster.ClusterConfig?.Shards ?? 3) * (cluster.ClusterConfig?.ReplicasPerShard ?? 1)}

  storage:
    volumeClaimTemplate:
      spec:
        storageClassName: {cluster.Persistence.StorageClass}
        accessModes: [""ReadWriteOnce""]
        resources:
          requests:
            storage: {cluster.Persistence.StorageSize}

  redisExporter:
    enabled: {cluster.Monitoring.PrometheusExporter.ToString().ToLower()}
    image: oliver006/redis_exporter:latest";
        }

        private string GenerateReplicationManifest(CacheCluster cluster)
        {
            return $@"apiVersion: redis.redis.opstreelabs.in/v1beta2
kind: RedisReplication
metadata:
  name: {cluster.Name}
  namespace: {cluster.Namespace}
spec:
  clusterSize: {1 + cluster.Size.ReplicasPerPrimary}
  kubernetesConfig:
    image: {(cluster.Platform == CachePlatform.Dragonfly ? "docker.dragonflydb.io/dragonflydb/dragonfly" : $"redis:{cluster.Version}")}
    imagePullPolicy: IfNotPresent
    resources:
      requests:
        cpu: {cluster.Resources.CpuRequest}
        memory: {cluster.Resources.MemoryRequest}
      limits:
        cpu: {cluster.Resources.CpuLimit}
        memory: {cluster.Resources.MemoryLimit}

  redisConfig:
    additionalRedisConfig: |
      maxmemory {cluster.Memory.MaxMemory}

  storage:
    volumeClaimTemplate:
      spec:
        storageClassName: {cluster.Persistence.StorageClass}
        accessModes: [""ReadWriteOnce""]
        resources:
          requests:
            storage: {cluster.Persistence.StorageSize}";
        }

        private string GenerateStandaloneManifest(CacheCluster cluster)
        {
            return $@"apiVersion: redis.redis.opstreelabs.in/v1beta2
kind: Redis
metadata:
  name: {cluster.Name}
  namespace: {cluster.Namespace}
spec:
  kubernetesConfig:
    image: redis:{cluster.Version}
    imagePullPolicy: IfNotPresent
    resources:
      requests:
        cpu: {cluster.Resources.CpuRequest}
        memory: {cluster.Resources.MemoryRequest}
      limits:
        cpu: {cluster.Resources.CpuLimit}
        memory: {cluster.Resources.MemoryLimit}

  redisConfig:
    additionalRedisConfig: |
      maxmemory {cluster.Memory.MaxMemory}

  storage:
    volumeClaimTemplate:
      spec:
        storageClassName: {cluster.Persistence.StorageClass}
        accessModes: [""ReadWriteOnce""]
        resources:
          requests:
            storage: {cluster.Persistence.StorageSize}

  redisExporter:
    enabled: {cluster.Monitoring.PrometheusExporter.ToString().ToLower()}";
        }

        #endregion
    }

    #endregion
}
