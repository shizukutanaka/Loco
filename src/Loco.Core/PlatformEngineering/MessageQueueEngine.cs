// =============================================================================
// Message Queue Engine
// Kubernetes-native message queue management with Strimzi, RabbitMQ, Pulsar
// Based on: Strimzi Kafka Operator, RabbitMQ Cluster Operator, Apache Pulsar
// Research: https://strimzi.io, https://www.rabbitmq.com/kubernetes
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
    /// Message queue platform
    /// </summary>
    public enum QueuePlatform
    {
        Kafka,
        RabbitMQ,
        Pulsar,
        NATS,
        RedPanda
    }

    /// <summary>
    /// Kafka listener type
    /// </summary>
    public enum KafkaListenerType
    {
        Internal,          // ClusterIP service
        External,          // LoadBalancer/NodePort
        Route,             // OpenShift Route
        Ingress            // Kubernetes Ingress
    }

    /// <summary>
    /// Authentication type
    /// </summary>
    public enum QueueAuthType
    {
        None,
        TLS,
        SCRAM_SHA_256,
        SCRAM_SHA_512,
        OAuth,
        PLAIN
    }

    /// <summary>
    /// Topic cleanup policy
    /// </summary>
    public enum TopicCleanupPolicy
    {
        Delete,
        Compact,
        CompactDelete
    }

    /// <summary>
    /// RabbitMQ queue type
    /// </summary>
    public enum RabbitQueueType
    {
        Classic,
        Quorum,
        Stream
    }

    /// <summary>
    /// Exchange type for RabbitMQ
    /// </summary>
    public enum ExchangeType
    {
        Direct,
        Fanout,
        Topic,
        Headers
    }

    #endregion

    #region Core Types

    /// <summary>
    /// Message queue cluster specification
    /// </summary>
    public class MessageQueueCluster
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = "default";

        /// <summary>
        /// Queue platform
        /// </summary>
        public QueuePlatform Platform { get; set; }

        /// <summary>
        /// Platform version
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Number of broker replicas
        /// </summary>
        public int Replicas { get; set; } = 3;

        /// <summary>
        /// Storage configuration
        /// </summary>
        public QueueStorageConfig Storage { get; set; } = new();

        /// <summary>
        /// Resource requirements
        /// </summary>
        public ResourceRequirements Resources { get; set; } = new();

        /// <summary>
        /// Authentication configuration
        /// </summary>
        public QueueAuthConfig Authentication { get; set; } = new();

        /// <summary>
        /// TLS configuration
        /// </summary>
        public TLSConfig? TLS { get; set; }

        /// <summary>
        /// Kafka-specific configuration
        /// </summary>
        public KafkaConfig? Kafka { get; set; }

        /// <summary>
        /// RabbitMQ-specific configuration
        /// </summary>
        public RabbitMQConfig? RabbitMQ { get; set; }

        /// <summary>
        /// Monitoring configuration
        /// </summary>
        public QueueMonitoringConfig Monitoring { get; set; } = new();

        /// <summary>
        /// Labels
        /// </summary>
        public Dictionary<string, string> Labels { get; set; } = new();

        /// <summary>
        /// Cluster status
        /// </summary>
        public QueueClusterStatus Status { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Storage configuration for queue clusters
    /// </summary>
    public class QueueStorageConfig
    {
        public string Type { get; set; } = "persistent-claim";
        public string Size { get; set; } = "100Gi";
        public string StorageClass { get; set; } = "standard";
        public bool DeleteClaim { get; set; } = false;
    }

    /// <summary>
    /// Authentication configuration
    /// </summary>
    public class QueueAuthConfig
    {
        public QueueAuthType Type { get; set; } = QueueAuthType.SCRAM_SHA_512;
        public string? OAuthClientId { get; set; }
        public string? OAuthTokenEndpoint { get; set; }
        public string? OAuthAudience { get; set; }
    }

    /// <summary>
    /// TLS configuration
    /// </summary>
    public class TLSConfig
    {
        public bool Enabled { get; set; } = true;
        public string? CertSecretName { get; set; }
        public string? CASecretName { get; set; }
        public bool GenerateCertificates { get; set; } = true;
        public int CertificateValidityDays { get; set; } = 365;
    }

    /// <summary>
    /// Kafka-specific configuration
    /// </summary>
    public class KafkaConfig
    {
        /// <summary>
        /// Listeners configuration
        /// </summary>
        public List<KafkaListener> Listeners { get; set; } = new();

        /// <summary>
        /// ZooKeeper configuration (if not using KRaft)
        /// </summary>
        public ZooKeeperConfig? ZooKeeper { get; set; }

        /// <summary>
        /// Use KRaft mode (ZooKeeper-less)
        /// </summary>
        public bool UseKRaft { get; set; } = true;

        /// <summary>
        /// Kafka broker configuration
        /// </summary>
        public Dictionary<string, string> Config { get; set; } = new()
        {
            ["offsets.topic.replication.factor"] = "3",
            ["transaction.state.log.replication.factor"] = "3",
            ["transaction.state.log.min.isr"] = "2",
            ["default.replication.factor"] = "3",
            ["min.insync.replicas"] = "2",
            ["auto.create.topics.enable"] = "false"
        };

        /// <summary>
        /// Rack awareness configuration
        /// </summary>
        public RackAwarenessConfig? RackAwareness { get; set; }

        /// <summary>
        /// JVM options
        /// </summary>
        public string JvmOptions { get; set; } = "-Xms2g -Xmx2g";
    }

    /// <summary>
    /// Kafka listener configuration
    /// </summary>
    public class KafkaListener
    {
        public string Name { get; set; } = string.Empty;
        public int Port { get; set; }
        public KafkaListenerType Type { get; set; }
        public bool TLS { get; set; }
        public QueueAuthConfig? Authentication { get; set; }
    }

    /// <summary>
    /// ZooKeeper configuration
    /// </summary>
    public class ZooKeeperConfig
    {
        public int Replicas { get; set; } = 3;
        public QueueStorageConfig Storage { get; set; } = new() { Size = "10Gi" };
        public ResourceRequirements Resources { get; set; } = new()
        {
            CpuRequest = "500m",
            MemoryRequest = "1Gi"
        };
    }

    /// <summary>
    /// Rack awareness configuration
    /// </summary>
    public class RackAwarenessConfig
    {
        public string TopologyKey { get; set; } = "topology.kubernetes.io/zone";
    }

    /// <summary>
    /// RabbitMQ-specific configuration
    /// </summary>
    public class RabbitMQConfig
    {
        /// <summary>
        /// RabbitMQ configuration
        /// </summary>
        public Dictionary<string, string> Config { get; set; } = new()
        {
            ["cluster_partition_handling"] = "pause_minority",
            ["vm_memory_high_watermark.relative"] = "0.6",
            ["disk_free_limit.absolute"] = "2GB"
        };

        /// <summary>
        /// Enabled plugins
        /// </summary>
        public List<string> Plugins { get; set; } = new()
        {
            "rabbitmq_management",
            "rabbitmq_prometheus",
            "rabbitmq_shovel",
            "rabbitmq_federation"
        };

        /// <summary>
        /// Override stateful set configuration
        /// </summary>
        public RabbitMQOverrideConfig? Override { get; set; }

        /// <summary>
        /// Enable quorum queues by default
        /// </summary>
        public bool DefaultQuorumQueues { get; set; } = true;
    }

    /// <summary>
    /// RabbitMQ override configuration
    /// </summary>
    public class RabbitMQOverrideConfig
    {
        public Dictionary<string, string> StatefulSetAnnotations { get; set; } = new();
        public Dictionary<string, string> ServiceAnnotations { get; set; } = new();
    }

    /// <summary>
    /// Queue monitoring configuration
    /// </summary>
    public class QueueMonitoringConfig
    {
        public bool Enabled { get; set; } = true;
        public bool PrometheusExporter { get; set; } = true;
        public bool GrafanaDashboards { get; set; } = true;
        public int MetricsPort { get; set; } = 9404;
    }

    /// <summary>
    /// Cluster status
    /// </summary>
    public class QueueClusterStatus
    {
        public string Phase { get; set; } = "Pending";
        public int ReadyReplicas { get; set; }
        public int TotalReplicas { get; set; }
        public List<BrokerStatus> Brokers { get; set; } = new();
        public string? BootstrapServers { get; set; }
        public List<ClusterCondition> Conditions { get; set; } = new();
    }

    /// <summary>
    /// Broker status
    /// </summary>
    public class BrokerStatus
    {
        public int BrokerId { get; set; }
        public string PodName { get; set; } = string.Empty;
        public bool Ready { get; set; }
        public string? Address { get; set; }
        public string? NodeId { get; set; }
        public bool IsController { get; set; }
    }

    #endregion

    #region Topic Types

    /// <summary>
    /// Kafka topic specification
    /// </summary>
    public class KafkaTopic
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClusterId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Number of partitions
        /// </summary>
        public int Partitions { get; set; } = 3;

        /// <summary>
        /// Replication factor
        /// </summary>
        public int Replicas { get; set; } = 3;

        /// <summary>
        /// Topic configuration
        /// </summary>
        public Dictionary<string, string> Config { get; set; } = new();

        /// <summary>
        /// Cleanup policy
        /// </summary>
        public TopicCleanupPolicy CleanupPolicy { get; set; } = TopicCleanupPolicy.Delete;

        /// <summary>
        /// Retention time in milliseconds
        /// </summary>
        public long? RetentionMs { get; set; }

        /// <summary>
        /// Retention bytes
        /// </summary>
        public long? RetentionBytes { get; set; }

        /// <summary>
        /// Topic status
        /// </summary>
        public TopicStatus Status { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Topic status
    /// </summary>
    public class TopicStatus
    {
        public string Phase { get; set; } = "Ready";
        public List<PartitionStatus> Partitions { get; set; } = new();
        public long TotalMessages { get; set; }
        public long TotalBytes { get; set; }
    }

    /// <summary>
    /// Partition status
    /// </summary>
    public class PartitionStatus
    {
        public int PartitionId { get; set; }
        public int Leader { get; set; }
        public List<int> Replicas { get; set; } = new();
        public List<int> InSyncReplicas { get; set; } = new();
        public long BeginOffset { get; set; }
        public long EndOffset { get; set; }
    }

    /// <summary>
    /// RabbitMQ queue specification
    /// </summary>
    public class RabbitQueue
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClusterId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string VHost { get; set; } = "/";

        /// <summary>
        /// Queue type
        /// </summary>
        public RabbitQueueType Type { get; set; } = RabbitQueueType.Quorum;

        /// <summary>
        /// Durable queue
        /// </summary>
        public bool Durable { get; set; } = true;

        /// <summary>
        /// Auto-delete queue
        /// </summary>
        public bool AutoDelete { get; set; } = false;

        /// <summary>
        /// Queue arguments
        /// </summary>
        public Dictionary<string, object> Arguments { get; set; } = new();

        /// <summary>
        /// Dead letter exchange
        /// </summary>
        public string? DeadLetterExchange { get; set; }

        /// <summary>
        /// Message TTL
        /// </summary>
        public int? MessageTTL { get; set; }

        /// <summary>
        /// Max length
        /// </summary>
        public int? MaxLength { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// RabbitMQ exchange specification
    /// </summary>
    public class RabbitExchange
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClusterId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string VHost { get; set; } = "/";
        public ExchangeType Type { get; set; } = ExchangeType.Direct;
        public bool Durable { get; set; } = true;
        public bool AutoDelete { get; set; } = false;
        public bool Internal { get; set; } = false;
        public Dictionary<string, object> Arguments { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// RabbitMQ binding
    /// </summary>
    public class RabbitBinding
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClusterId { get; set; } = string.Empty;
        public string VHost { get; set; } = "/";
        public string Source { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string DestinationType { get; set; } = "queue"; // queue or exchange
        public string RoutingKey { get; set; } = string.Empty;
        public Dictionary<string, object> Arguments { get; set; } = new();
    }

    #endregion

    #region User Types

    /// <summary>
    /// Kafka user specification
    /// </summary>
    public class KafkaUser
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClusterId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Authentication type
        /// </summary>
        public QueueAuthType Authentication { get; set; } = QueueAuthType.SCRAM_SHA_512;

        /// <summary>
        /// ACL rules
        /// </summary>
        public List<KafkaAclRule> Acls { get; set; } = new();

        /// <summary>
        /// Quotas
        /// </summary>
        public KafkaUserQuotas? Quotas { get; set; }

        /// <summary>
        /// Secret name containing credentials
        /// </summary>
        public string SecretName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Kafka ACL rule
    /// </summary>
    public class KafkaAclRule
    {
        /// <summary>
        /// Resource type (topic, group, cluster, transactionalId)
        /// </summary>
        public string ResourceType { get; set; } = "topic";

        /// <summary>
        /// Resource name (or pattern)
        /// </summary>
        public string ResourceName { get; set; } = string.Empty;

        /// <summary>
        /// Pattern type (literal, prefix)
        /// </summary>
        public string PatternType { get; set; } = "literal";

        /// <summary>
        /// Operations allowed
        /// </summary>
        public List<string> Operations { get; set; } = new();

        /// <summary>
        /// Host restriction
        /// </summary>
        public string Host { get; set; } = "*";

        /// <summary>
        /// Allow or Deny
        /// </summary>
        public string Type { get; set; } = "allow";
    }

    /// <summary>
    /// Kafka user quotas
    /// </summary>
    public class KafkaUserQuotas
    {
        public long? ProducerByteRate { get; set; }
        public long? ConsumerByteRate { get; set; }
        public double? RequestPercentage { get; set; }
        public double? ControllerMutationRate { get; set; }
    }

    /// <summary>
    /// RabbitMQ user specification
    /// </summary>
    public class RabbitUser
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClusterId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// User tags (administrator, monitoring, management, policymaker)
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Permissions per vhost
        /// </summary>
        public List<RabbitPermission> Permissions { get; set; } = new();

        /// <summary>
        /// Secret containing credentials
        /// </summary>
        public string CredentialsSecret { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// RabbitMQ permission
    /// </summary>
    public class RabbitPermission
    {
        public string VHost { get; set; } = "/";
        public string Configure { get; set; } = ".*";
        public string Write { get; set; } = ".*";
        public string Read { get; set; } = ".*";
    }

    #endregion

    #region Consumer Types

    /// <summary>
    /// Consumer group status
    /// </summary>
    public class ConsumerGroupStatus
    {
        public string GroupId { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public List<ConsumerMemberStatus> Members { get; set; } = new();
        public List<ConsumerTopicLag> TopicLags { get; set; } = new();
        public long TotalLag { get; set; }
    }

    /// <summary>
    /// Consumer member status
    /// </summary>
    public class ConsumerMemberStatus
    {
        public string MemberId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public List<string> AssignedPartitions { get; set; } = new();
    }

    /// <summary>
    /// Consumer topic lag
    /// </summary>
    public class ConsumerTopicLag
    {
        public string Topic { get; set; } = string.Empty;
        public int Partition { get; set; }
        public long CurrentOffset { get; set; }
        public long LogEndOffset { get; set; }
        public long Lag { get; set; }
    }

    #endregion

    #region Connector Types

    /// <summary>
    /// Kafka Connect cluster
    /// </summary>
    public class KafkaConnectCluster
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string KafkaClusterId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = "default";
        public int Replicas { get; set; } = 2;
        public string BootstrapServers { get; set; } = string.Empty;

        /// <summary>
        /// Connector plugins to include
        /// </summary>
        public List<ConnectorPlugin> Plugins { get; set; } = new();

        /// <summary>
        /// Connect configuration
        /// </summary>
        public Dictionary<string, string> Config { get; set; } = new();

        public ResourceRequirements Resources { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Connector plugin
    /// </summary>
    public class ConnectorPlugin
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "maven"; // maven, url, artifact
        public string? MavenGroup { get; set; }
        public string? MavenArtifact { get; set; }
        public string? MavenVersion { get; set; }
        public string? Url { get; set; }
    }

    /// <summary>
    /// Kafka Connector
    /// </summary>
    public class KafkaConnector
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ConnectClusterId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Class { get; set; } = string.Empty;
        public int TasksMax { get; set; } = 1;
        public Dictionary<string, string> Config { get; set; } = new();
        public ConnectorStatus Status { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Connector status
    /// </summary>
    public class ConnectorStatus
    {
        public string State { get; set; } = "RUNNING";
        public string? WorkerId { get; set; }
        public List<ConnectorTaskStatus> Tasks { get; set; } = new();
    }

    /// <summary>
    /// Connector task status
    /// </summary>
    public class ConnectorTaskStatus
    {
        public int TaskId { get; set; }
        public string State { get; set; } = "RUNNING";
        public string? WorkerId { get; set; }
        public string? Trace { get; set; }
    }

    #endregion

    #region Schema Registry Types

    /// <summary>
    /// Schema registry configuration
    /// </summary>
    public class SchemaRegistry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string KafkaClusterId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = "default";
        public int Replicas { get; set; } = 2;
        public string BootstrapServers { get; set; } = string.Empty;
        public ResourceRequirements Resources { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Schema definition
    /// </summary>
    public class SchemaDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Subject { get; set; } = string.Empty;
        public int Version { get; set; }
        public string SchemaType { get; set; } = "AVRO"; // AVRO, JSON, PROTOBUF
        public string Schema { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    #endregion

    #region Interface

    /// <summary>
    /// Message Queue Engine interface
    /// Provides Kubernetes-native message queue management
    /// </summary>
    public interface IMessageQueueEngine
    {
        #region Cluster Management

        /// <summary>
        /// Create message queue cluster
        /// </summary>
        Task<MessageQueueCluster> CreateClusterAsync(
            string tenantId,
            MessageQueueCluster cluster,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get cluster by ID
        /// </summary>
        Task<MessageQueueCluster?> GetClusterAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Update cluster
        /// </summary>
        Task<MessageQueueCluster> UpdateClusterAsync(
            string tenantId,
            MessageQueueCluster cluster,
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
        Task<List<MessageQueueCluster>> ListClustersAsync(
            string tenantId,
            QueuePlatform? platform = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Scale cluster
        /// </summary>
        Task<MessageQueueCluster> ScaleClusterAsync(
            string tenantId,
            string clusterId,
            int replicas,
            CancellationToken cancellation = default);

        #endregion

        #region Kafka Topics

        /// <summary>
        /// Create Kafka topic
        /// </summary>
        Task<KafkaTopic> CreateTopicAsync(
            string tenantId,
            string clusterId,
            KafkaTopic topic,
            CancellationToken cancellation = default);

        /// <summary>
        /// List topics
        /// </summary>
        Task<List<KafkaTopic>> ListTopicsAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Delete topic
        /// </summary>
        Task<bool> DeleteTopicAsync(
            string tenantId,
            string clusterId,
            string topicName,
            CancellationToken cancellation = default);

        /// <summary>
        /// Update topic configuration
        /// </summary>
        Task<KafkaTopic> UpdateTopicAsync(
            string tenantId,
            string clusterId,
            KafkaTopic topic,
            CancellationToken cancellation = default);

        #endregion

        #region RabbitMQ Resources

        /// <summary>
        /// Create RabbitMQ queue
        /// </summary>
        Task<RabbitQueue> CreateQueueAsync(
            string tenantId,
            string clusterId,
            RabbitQueue queue,
            CancellationToken cancellation = default);

        /// <summary>
        /// Create RabbitMQ exchange
        /// </summary>
        Task<RabbitExchange> CreateExchangeAsync(
            string tenantId,
            string clusterId,
            RabbitExchange exchange,
            CancellationToken cancellation = default);

        /// <summary>
        /// Create binding
        /// </summary>
        Task<RabbitBinding> CreateBindingAsync(
            string tenantId,
            string clusterId,
            RabbitBinding binding,
            CancellationToken cancellation = default);

        #endregion

        #region User Management

        /// <summary>
        /// Create Kafka user
        /// </summary>
        Task<KafkaUser> CreateKafkaUserAsync(
            string tenantId,
            string clusterId,
            KafkaUser user,
            CancellationToken cancellation = default);

        /// <summary>
        /// Create RabbitMQ user
        /// </summary>
        Task<RabbitUser> CreateRabbitUserAsync(
            string tenantId,
            string clusterId,
            RabbitUser user,
            CancellationToken cancellation = default);

        #endregion

        #region Consumer Groups

        /// <summary>
        /// Get consumer group status
        /// </summary>
        Task<ConsumerGroupStatus> GetConsumerGroupStatusAsync(
            string tenantId,
            string clusterId,
            string groupId,
            CancellationToken cancellation = default);

        /// <summary>
        /// List consumer groups
        /// </summary>
        Task<List<ConsumerGroupStatus>> ListConsumerGroupsAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Reset consumer group offsets
        /// </summary>
        Task<bool> ResetConsumerOffsetsAsync(
            string tenantId,
            string clusterId,
            string groupId,
            string topic,
            string resetTo,
            CancellationToken cancellation = default);

        #endregion

        #region Kafka Connect

        /// <summary>
        /// Create Kafka Connect cluster
        /// </summary>
        Task<KafkaConnectCluster> CreateConnectClusterAsync(
            string tenantId,
            KafkaConnectCluster connectCluster,
            CancellationToken cancellation = default);

        /// <summary>
        /// Create connector
        /// </summary>
        Task<KafkaConnector> CreateConnectorAsync(
            string tenantId,
            string connectClusterId,
            KafkaConnector connector,
            CancellationToken cancellation = default);

        /// <summary>
        /// List connectors
        /// </summary>
        Task<List<KafkaConnector>> ListConnectorsAsync(
            string tenantId,
            string connectClusterId,
            CancellationToken cancellation = default);

        #endregion

        #region Schema Registry

        /// <summary>
        /// Create schema registry
        /// </summary>
        Task<SchemaRegistry> CreateSchemaRegistryAsync(
            string tenantId,
            SchemaRegistry registry,
            CancellationToken cancellation = default);

        /// <summary>
        /// Register schema
        /// </summary>
        Task<SchemaDefinition> RegisterSchemaAsync(
            string tenantId,
            string registryId,
            SchemaDefinition schema,
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
    /// Message Queue Engine implementation
    /// </summary>
    public class MessageQueueEngine : IMessageQueueEngine
    {
        private readonly ILogger<MessageQueueEngine> _logger;
        private readonly Dictionary<string, Dictionary<string, MessageQueueCluster>> _clusters = new();
        private readonly Dictionary<string, Dictionary<string, KafkaTopic>> _topics = new();
        private readonly Dictionary<string, Dictionary<string, RabbitQueue>> _queues = new();
        private readonly Dictionary<string, Dictionary<string, RabbitExchange>> _exchanges = new();
        private readonly Dictionary<string, Dictionary<string, KafkaUser>> _kafkaUsers = new();
        private readonly Dictionary<string, Dictionary<string, RabbitUser>> _rabbitUsers = new();
        private readonly Dictionary<string, Dictionary<string, KafkaConnectCluster>> _connectClusters = new();
        private readonly Dictionary<string, Dictionary<string, KafkaConnector>> _connectors = new();
        private readonly Dictionary<string, Dictionary<string, SchemaRegistry>> _registries = new();

        public MessageQueueEngine(ILogger<MessageQueueEngine> logger)
        {
            _logger = logger;
        }

        #region Cluster Management

        public Task<MessageQueueCluster> CreateClusterAsync(
            string tenantId,
            MessageQueueCluster cluster,
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
                "Created {Platform} cluster {Name} with {Replicas} replicas",
                cluster.Platform, cluster.Name, cluster.Replicas);

            return Task.FromResult(cluster);
        }

        private void ApplyPlatformDefaults(MessageQueueCluster cluster)
        {
            switch (cluster.Platform)
            {
                case QueuePlatform.Kafka:
                    cluster.Version = string.IsNullOrEmpty(cluster.Version) ? "3.7.0" : cluster.Version;
                    cluster.Kafka ??= new KafkaConfig
                    {
                        UseKRaft = true,
                        Listeners = new List<KafkaListener>
                        {
                            new KafkaListener
                            {
                                Name = "plain",
                                Port = 9092,
                                Type = KafkaListenerType.Internal,
                                TLS = false
                            },
                            new KafkaListener
                            {
                                Name = "tls",
                                Port = 9093,
                                Type = KafkaListenerType.Internal,
                                TLS = true
                            }
                        }
                    };
                    break;

                case QueuePlatform.RabbitMQ:
                    cluster.Version = string.IsNullOrEmpty(cluster.Version) ? "3.13" : cluster.Version;
                    cluster.RabbitMQ ??= new RabbitMQConfig();
                    break;

                case QueuePlatform.Pulsar:
                    cluster.Version = string.IsNullOrEmpty(cluster.Version) ? "3.2.0" : cluster.Version;
                    break;

                case QueuePlatform.NATS:
                    cluster.Version = string.IsNullOrEmpty(cluster.Version) ? "2.10" : cluster.Version;
                    break;

                case QueuePlatform.RedPanda:
                    cluster.Version = string.IsNullOrEmpty(cluster.Version) ? "24.1" : cluster.Version;
                    break;
            }
        }

        private void SimulateClusterCreation(MessageQueueCluster cluster)
        {
            cluster.Status = new QueueClusterStatus
            {
                Phase = "Running",
                TotalReplicas = cluster.Replicas,
                ReadyReplicas = cluster.Replicas,
                BootstrapServers = cluster.Platform switch
                {
                    QueuePlatform.Kafka => $"{cluster.Name}-kafka-bootstrap.{cluster.Namespace}.svc:9092",
                    QueuePlatform.RabbitMQ => $"{cluster.Name}.{cluster.Namespace}.svc:5672",
                    _ => $"{cluster.Name}.{cluster.Namespace}.svc"
                }
            };

            for (int i = 0; i < cluster.Replicas; i++)
            {
                cluster.Status.Brokers.Add(new BrokerStatus
                {
                    BrokerId = i,
                    PodName = $"{cluster.Name}-{i}",
                    Ready = true,
                    Address = $"{cluster.Name}-{i}.{cluster.Name}.{cluster.Namespace}.svc:9092",
                    IsController = i == 0
                });
            }

            cluster.Status.Conditions.Add(new ClusterCondition
            {
                Type = "Ready",
                Status = "True",
                LastTransitionTime = DateTime.UtcNow,
                Reason = "ClusterReady",
                Message = "All brokers are running"
            });
        }

        public Task<MessageQueueCluster?> GetClusterAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            if (_clusters.TryGetValue(tenantId, out var clusters) &&
                clusters.TryGetValue(clusterId, out var cluster))
            {
                return Task.FromResult<MessageQueueCluster?>(cluster);
            }

            return Task.FromResult<MessageQueueCluster?>(null);
        }

        public Task<MessageQueueCluster> UpdateClusterAsync(
            string tenantId,
            MessageQueueCluster cluster,
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

        public Task<List<MessageQueueCluster>> ListClustersAsync(
            string tenantId,
            QueuePlatform? platform = null,
            CancellationToken cancellation = default)
        {
            if (!_clusters.TryGetValue(tenantId, out var clusters))
                return Task.FromResult(new List<MessageQueueCluster>());

            var result = clusters.Values.AsEnumerable();
            if (platform.HasValue)
            {
                result = result.Where(c => c.Platform == platform.Value);
            }

            return Task.FromResult(result.ToList());
        }

        public async Task<MessageQueueCluster> ScaleClusterAsync(
            string tenantId,
            string clusterId,
            int replicas,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null)
                throw new InvalidOperationException($"Cluster {clusterId} not found");

            var oldReplicas = cluster.Replicas;
            cluster.Replicas = replicas;
            cluster.Status.TotalReplicas = replicas;
            cluster.Status.ReadyReplicas = replicas;

            // Update brokers
            if (replicas > oldReplicas)
            {
                for (int i = oldReplicas; i < replicas; i++)
                {
                    cluster.Status.Brokers.Add(new BrokerStatus
                    {
                        BrokerId = i,
                        PodName = $"{cluster.Name}-{i}",
                        Ready = true
                    });
                }
            }
            else if (replicas < oldReplicas)
            {
                cluster.Status.Brokers = cluster.Status.Brokers.Take(replicas).ToList();
            }

            _logger.LogInformation(
                "Scaled cluster {ClusterId} from {OldReplicas} to {NewReplicas}",
                clusterId, oldReplicas, replicas);

            return cluster;
        }

        #endregion

        #region Kafka Topics

        public async Task<KafkaTopic> CreateTopicAsync(
            string tenantId,
            string clusterId,
            KafkaTopic topic,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null || cluster.Platform != QueuePlatform.Kafka)
                throw new InvalidOperationException("Topic can only be created on Kafka clusters");

            if (!_topics.ContainsKey(clusterId))
                _topics[clusterId] = new();

            topic.ClusterId = clusterId;
            topic.CreatedAt = DateTime.UtcNow;

            // Set default config based on cleanup policy
            if (topic.CleanupPolicy == TopicCleanupPolicy.Compact)
            {
                topic.Config["cleanup.policy"] = "compact";
            }
            else if (topic.CleanupPolicy == TopicCleanupPolicy.CompactDelete)
            {
                topic.Config["cleanup.policy"] = "compact,delete";
            }

            if (topic.RetentionMs.HasValue)
            {
                topic.Config["retention.ms"] = topic.RetentionMs.Value.ToString();
            }

            // Simulate partition status
            topic.Status = new TopicStatus
            {
                Phase = "Ready",
                Partitions = Enumerable.Range(0, topic.Partitions)
                    .Select(i => new PartitionStatus
                    {
                        PartitionId = i,
                        Leader = i % cluster.Replicas,
                        Replicas = Enumerable.Range(0, topic.Replicas).ToList(),
                        InSyncReplicas = Enumerable.Range(0, topic.Replicas).ToList(),
                        BeginOffset = 0,
                        EndOffset = 0
                    }).ToList()
            };

            _topics[clusterId][topic.Id] = topic;

            _logger.LogInformation(
                "Created topic {TopicName} with {Partitions} partitions, RF={Replicas}",
                topic.Name, topic.Partitions, topic.Replicas);

            return topic;
        }

        public Task<List<KafkaTopic>> ListTopicsAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            if (!_topics.TryGetValue(clusterId, out var topics))
                return Task.FromResult(new List<KafkaTopic>());

            return Task.FromResult(topics.Values.ToList());
        }

        public Task<bool> DeleteTopicAsync(
            string tenantId,
            string clusterId,
            string topicName,
            CancellationToken cancellation = default)
        {
            if (_topics.TryGetValue(clusterId, out var topics))
            {
                var topic = topics.Values.FirstOrDefault(t => t.Name == topicName);
                if (topic != null)
                {
                    return Task.FromResult(topics.Remove(topic.Id));
                }
            }

            return Task.FromResult(false);
        }

        public async Task<KafkaTopic> UpdateTopicAsync(
            string tenantId,
            string clusterId,
            KafkaTopic topic,
            CancellationToken cancellation = default)
        {
            if (!_topics.ContainsKey(clusterId) ||
                !_topics[clusterId].ContainsKey(topic.Id))
            {
                throw new InvalidOperationException($"Topic {topic.Id} not found");
            }

            _topics[clusterId][topic.Id] = topic;
            return topic;
        }

        #endregion

        #region RabbitMQ Resources

        public async Task<RabbitQueue> CreateQueueAsync(
            string tenantId,
            string clusterId,
            RabbitQueue queue,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null || cluster.Platform != QueuePlatform.RabbitMQ)
                throw new InvalidOperationException("Queue can only be created on RabbitMQ clusters");

            if (!_queues.ContainsKey(clusterId))
                _queues[clusterId] = new();

            queue.ClusterId = clusterId;
            queue.CreatedAt = DateTime.UtcNow;

            // Set quorum queue arguments
            if (queue.Type == RabbitQueueType.Quorum)
            {
                queue.Arguments["x-queue-type"] = "quorum";
            }
            else if (queue.Type == RabbitQueueType.Stream)
            {
                queue.Arguments["x-queue-type"] = "stream";
            }

            if (!string.IsNullOrEmpty(queue.DeadLetterExchange))
            {
                queue.Arguments["x-dead-letter-exchange"] = queue.DeadLetterExchange;
            }

            _queues[clusterId][queue.Id] = queue;

            _logger.LogInformation(
                "Created {Type} queue {QueueName} in vhost {VHost}",
                queue.Type, queue.Name, queue.VHost);

            return queue;
        }

        public async Task<RabbitExchange> CreateExchangeAsync(
            string tenantId,
            string clusterId,
            RabbitExchange exchange,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null || cluster.Platform != QueuePlatform.RabbitMQ)
                throw new InvalidOperationException("Exchange can only be created on RabbitMQ clusters");

            if (!_exchanges.ContainsKey(clusterId))
                _exchanges[clusterId] = new();

            exchange.ClusterId = clusterId;
            exchange.CreatedAt = DateTime.UtcNow;

            _exchanges[clusterId][exchange.Id] = exchange;

            _logger.LogInformation(
                "Created {Type} exchange {ExchangeName} in vhost {VHost}",
                exchange.Type, exchange.Name, exchange.VHost);

            return exchange;
        }

        public Task<RabbitBinding> CreateBindingAsync(
            string tenantId,
            string clusterId,
            RabbitBinding binding,
            CancellationToken cancellation = default)
        {
            binding.ClusterId = clusterId;

            _logger.LogInformation(
                "Created binding from {Source} to {Destination} with routing key {RoutingKey}",
                binding.Source, binding.Destination, binding.RoutingKey);

            return Task.FromResult(binding);
        }

        #endregion

        #region User Management

        public async Task<KafkaUser> CreateKafkaUserAsync(
            string tenantId,
            string clusterId,
            KafkaUser user,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null || cluster.Platform != QueuePlatform.Kafka)
                throw new InvalidOperationException("Kafka user can only be created on Kafka clusters");

            if (!_kafkaUsers.ContainsKey(clusterId))
                _kafkaUsers[clusterId] = new();

            user.ClusterId = clusterId;
            user.CreatedAt = DateTime.UtcNow;
            user.SecretName = $"{cluster.Name}-{user.Username}";

            _kafkaUsers[clusterId][user.Id] = user;

            _logger.LogInformation(
                "Created Kafka user {Username} with {AclCount} ACL rules",
                user.Username, user.Acls.Count);

            return user;
        }

        public async Task<RabbitUser> CreateRabbitUserAsync(
            string tenantId,
            string clusterId,
            RabbitUser user,
            CancellationToken cancellation = default)
        {
            var cluster = await GetClusterAsync(tenantId, clusterId, cancellation);
            if (cluster == null || cluster.Platform != QueuePlatform.RabbitMQ)
                throw new InvalidOperationException("RabbitMQ user can only be created on RabbitMQ clusters");

            if (!_rabbitUsers.ContainsKey(clusterId))
                _rabbitUsers[clusterId] = new();

            user.ClusterId = clusterId;
            user.CreatedAt = DateTime.UtcNow;
            user.CredentialsSecret = $"{cluster.Name}-{user.Username}-credentials";

            _rabbitUsers[clusterId][user.Id] = user;

            _logger.LogInformation(
                "Created RabbitMQ user {Username} with tags {Tags}",
                user.Username, string.Join(",", user.Tags));

            return user;
        }

        #endregion

        #region Consumer Groups

        public Task<ConsumerGroupStatus> GetConsumerGroupStatusAsync(
            string tenantId,
            string clusterId,
            string groupId,
            CancellationToken cancellation = default)
        {
            // Simulate consumer group status
            var status = new ConsumerGroupStatus
            {
                GroupId = groupId,
                State = "Stable",
                Members = new List<ConsumerMemberStatus>
                {
                    new ConsumerMemberStatus
                    {
                        MemberId = $"{groupId}-0",
                        ClientId = "consumer-1",
                        Host = "/10.0.0.1",
                        AssignedPartitions = new List<string> { "topic-0", "topic-1" }
                    },
                    new ConsumerMemberStatus
                    {
                        MemberId = $"{groupId}-1",
                        ClientId = "consumer-2",
                        Host = "/10.0.0.2",
                        AssignedPartitions = new List<string> { "topic-2" }
                    }
                },
                TopicLags = new List<ConsumerTopicLag>
                {
                    new ConsumerTopicLag
                    {
                        Topic = "test-topic",
                        Partition = 0,
                        CurrentOffset = 1000,
                        LogEndOffset = 1050,
                        Lag = 50
                    }
                },
                TotalLag = 50
            };

            return Task.FromResult(status);
        }

        public Task<List<ConsumerGroupStatus>> ListConsumerGroupsAsync(
            string tenantId,
            string clusterId,
            CancellationToken cancellation = default)
        {
            // Simulate consumer groups list
            return Task.FromResult(new List<ConsumerGroupStatus>
            {
                new ConsumerGroupStatus { GroupId = "group-1", State = "Stable", TotalLag = 100 },
                new ConsumerGroupStatus { GroupId = "group-2", State = "Stable", TotalLag = 0 }
            });
        }

        public Task<bool> ResetConsumerOffsetsAsync(
            string tenantId,
            string clusterId,
            string groupId,
            string topic,
            string resetTo,
            CancellationToken cancellation = default)
        {
            _logger.LogInformation(
                "Reset consumer group {GroupId} offsets for topic {Topic} to {ResetTo}",
                groupId, topic, resetTo);

            return Task.FromResult(true);
        }

        #endregion

        #region Kafka Connect

        public Task<KafkaConnectCluster> CreateConnectClusterAsync(
            string tenantId,
            KafkaConnectCluster connectCluster,
            CancellationToken cancellation = default)
        {
            if (!_connectClusters.ContainsKey(tenantId))
                _connectClusters[tenantId] = new();

            connectCluster.CreatedAt = DateTime.UtcNow;
            _connectClusters[tenantId][connectCluster.Id] = connectCluster;

            _logger.LogInformation(
                "Created Kafka Connect cluster {Name} with {Replicas} replicas",
                connectCluster.Name, connectCluster.Replicas);

            return Task.FromResult(connectCluster);
        }

        public Task<KafkaConnector> CreateConnectorAsync(
            string tenantId,
            string connectClusterId,
            KafkaConnector connector,
            CancellationToken cancellation = default)
        {
            if (!_connectors.ContainsKey(connectClusterId))
                _connectors[connectClusterId] = new();

            connector.ConnectClusterId = connectClusterId;
            connector.CreatedAt = DateTime.UtcNow;
            connector.Status = new ConnectorStatus
            {
                State = "RUNNING",
                Tasks = Enumerable.Range(0, connector.TasksMax)
                    .Select(i => new ConnectorTaskStatus { TaskId = i, State = "RUNNING" })
                    .ToList()
            };

            _connectors[connectClusterId][connector.Id] = connector;

            _logger.LogInformation(
                "Created connector {Name} of class {Class}",
                connector.Name, connector.Class);

            return Task.FromResult(connector);
        }

        public Task<List<KafkaConnector>> ListConnectorsAsync(
            string tenantId,
            string connectClusterId,
            CancellationToken cancellation = default)
        {
            if (!_connectors.TryGetValue(connectClusterId, out var connectors))
                return Task.FromResult(new List<KafkaConnector>());

            return Task.FromResult(connectors.Values.ToList());
        }

        #endregion

        #region Schema Registry

        public Task<SchemaRegistry> CreateSchemaRegistryAsync(
            string tenantId,
            SchemaRegistry registry,
            CancellationToken cancellation = default)
        {
            if (!_registries.ContainsKey(tenantId))
                _registries[tenantId] = new();

            registry.CreatedAt = DateTime.UtcNow;
            _registries[tenantId][registry.Id] = registry;

            _logger.LogInformation(
                "Created Schema Registry {Name} with {Replicas} replicas",
                registry.Name, registry.Replicas);

            return Task.FromResult(registry);
        }

        public Task<SchemaDefinition> RegisterSchemaAsync(
            string tenantId,
            string registryId,
            SchemaDefinition schema,
            CancellationToken cancellation = default)
        {
            schema.CreatedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Registered {Type} schema for subject {Subject} version {Version}",
                schema.SchemaType, schema.Subject, schema.Version);

            return Task.FromResult(schema);
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

            return cluster.Platform switch
            {
                QueuePlatform.Kafka => GenerateStrimziManifest(cluster),
                QueuePlatform.RabbitMQ => GenerateRabbitMQManifest(cluster),
                _ => GenerateGenericManifest(cluster)
            };
        }

        private string GenerateStrimziManifest(MessageQueueCluster cluster)
        {
            var sb = new StringBuilder();

            // Kafka cluster
            sb.AppendLine($@"apiVersion: kafka.strimzi.io/v1beta2
kind: Kafka
metadata:
  name: {cluster.Name}
  namespace: {cluster.Namespace}
spec:
  kafka:
    version: {cluster.Version}
    replicas: {cluster.Replicas}
    listeners:");

            // Add listeners
            foreach (var listener in cluster.Kafka?.Listeners ?? new List<KafkaListener>())
            {
                sb.AppendLine($@"      - name: {listener.Name}
        port: {listener.Port}
        type: {listener.Type.ToString().ToLower()}
        tls: {listener.TLS.ToString().ToLower()}");

                if (listener.Authentication != null && listener.Authentication.Type != QueueAuthType.None)
                {
                    sb.AppendLine($@"        authentication:
          type: {listener.Authentication.Type.ToString().ToLower().Replace("_", "-")}");
                }
            }

            // Kafka config
            if (cluster.Kafka?.Config?.Any() == true)
            {
                sb.AppendLine("    config:");
                foreach (var config in cluster.Kafka.Config)
                {
                    sb.AppendLine($@"      {config.Key}: {config.Value}");
                }
            }

            // Storage
            sb.AppendLine($@"    storage:
      type: {cluster.Storage.Type}
      size: {cluster.Storage.Size}
      class: {cluster.Storage.StorageClass}
      deleteClaim: {cluster.Storage.DeleteClaim.ToString().ToLower()}");

            // Resources
            sb.AppendLine($@"    resources:
      requests:
        cpu: {cluster.Resources.CpuRequest}
        memory: {cluster.Resources.MemoryRequest}
      limits:
        cpu: {cluster.Resources.CpuLimit}
        memory: {cluster.Resources.MemoryLimit}");

            // JVM options
            if (!string.IsNullOrEmpty(cluster.Kafka?.JvmOptions))
            {
                sb.AppendLine($@"    jvmOptions:
      -Xms: {cluster.Kafka.JvmOptions.Split(' ')[0].Replace("-Xms", "")}
      -Xmx: {cluster.Kafka.JvmOptions.Split(' ')[1].Replace("-Xmx", "")}");
            }

            // Metrics
            if (cluster.Monitoring.Enabled)
            {
                sb.AppendLine($@"    metricsConfig:
      type: jmxPrometheusExporter
      valueFrom:
        configMapKeyRef:
          name: kafka-metrics
          key: kafka-metrics-config.yml");
            }

            // ZooKeeper or KRaft
            if (cluster.Kafka?.UseKRaft == true)
            {
                sb.AppendLine(@"  # KRaft mode - no ZooKeeper needed");
            }
            else if (cluster.Kafka?.ZooKeeper != null)
            {
                sb.AppendLine($@"  zookeeper:
    replicas: {cluster.Kafka.ZooKeeper.Replicas}
    storage:
      type: persistent-claim
      size: {cluster.Kafka.ZooKeeper.Storage.Size}
      class: {cluster.Kafka.ZooKeeper.Storage.StorageClass}
    resources:
      requests:
        cpu: {cluster.Kafka.ZooKeeper.Resources.CpuRequest}
        memory: {cluster.Kafka.ZooKeeper.Resources.MemoryRequest}");
            }

            // Entity Operator
            sb.AppendLine(@"  entityOperator:
    topicOperator: {}
    userOperator: {}");

            return sb.ToString();
        }

        private string GenerateRabbitMQManifest(MessageQueueCluster cluster)
        {
            var sb = new StringBuilder();

            sb.AppendLine($@"apiVersion: rabbitmq.com/v1beta1
kind: RabbitmqCluster
metadata:
  name: {cluster.Name}
  namespace: {cluster.Namespace}
spec:
  replicas: {cluster.Replicas}
  image: rabbitmq:{cluster.Version}-management

  resources:
    requests:
      cpu: {cluster.Resources.CpuRequest}
      memory: {cluster.Resources.MemoryRequest}
    limits:
      cpu: {cluster.Resources.CpuLimit}
      memory: {cluster.Resources.MemoryLimit}

  persistence:
    storageClassName: {cluster.Storage.StorageClass}
    storage: {cluster.Storage.Size}");

            // RabbitMQ configuration
            if (cluster.RabbitMQ?.Config?.Any() == true)
            {
                sb.AppendLine(@"
  rabbitmq:
    additionalConfig: |");
                foreach (var config in cluster.RabbitMQ.Config)
                {
                    sb.AppendLine($@"      {config.Key} = {config.Value}");
                }
            }

            // Plugins
            if (cluster.RabbitMQ?.Plugins?.Any() == true)
            {
                sb.AppendLine(@"
    additionalPlugins:");
                foreach (var plugin in cluster.RabbitMQ.Plugins)
                {
                    sb.AppendLine($@"      - {plugin}");
                }
            }

            // TLS
            if (cluster.TLS?.Enabled == true)
            {
                sb.AppendLine($@"
  tls:
    secretName: {cluster.TLS.CertSecretName ?? $"{cluster.Name}-tls"}");
            }

            return sb.ToString();
        }

        private string GenerateGenericManifest(MessageQueueCluster cluster)
        {
            return $@"# Generic message queue cluster configuration
# Platform: {cluster.Platform}
# Name: {cluster.Name}
# Replicas: {cluster.Replicas}

apiVersion: v1
kind: ConfigMap
metadata:
  name: {cluster.Name}-config
  namespace: {cluster.Namespace}
data:
  platform: ""{cluster.Platform}""
  version: ""{cluster.Version}""
  replicas: ""{cluster.Replicas}""";
        }

        #endregion
    }

    #endregion
}
