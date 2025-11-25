using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.EliteCloudNative
{
    // ============================================================================
    // DOMAIN MODELS - Zero Trust Security (SPIFFE/SPIRE Patterns)
    // ============================================================================

    public class TrustDomain
    {
        public string DomainId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string SpiffeId { get; set; } = string.Empty; // spiffe://example.org
        public TrustDomainConfig Config { get; set; } = new();
        public TrustDomainStatus Status { get; set; } = new();
        public List<string> FederatedDomains { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class TrustDomainConfig
    {
        public CertificateAuthorityConfig CA { get; set; } = new();
        public AttestationConfig Attestation { get; set; } = new();
        public RotationConfig Rotation { get; set; } = new();
        public FederationConfig Federation { get; set; } = new();
    }

    public class CertificateAuthorityConfig
    {
        public string Type { get; set; } = "self-signed"; // self-signed, upstream, disk, aws-pca, vault
        public int DefaultTtlHours { get; set; } = 1;
        public int MaxTtlHours { get; set; } = 24;
        public string KeyType { get; set; } = "ec-p256"; // ec-p256, ec-p384, rsa-2048, rsa-4096
        public bool EnableJwtSvid { get; set; } = true;
    }

    public class AttestationConfig
    {
        public List<string> NodeAttestors { get; set; } = new(); // k8s_psat, aws_iid, gcp_iit, azure_msi
        public List<string> WorkloadAttestors { get; set; } = new(); // k8s, docker, unix
        public bool RequireMultipleAttestors { get; set; }
    }

    public class RotationConfig
    {
        public int RotationIntervalHours { get; set; } = 1;
        public int GracePeriodMinutes { get; set; } = 10;
        public bool AutoRotate { get; set; } = true;
    }

    public class FederationConfig
    {
        public bool Enabled { get; set; }
        public string BundleEndpoint { get; set; } = string.Empty;
        public string BundleEndpointProfile { get; set; } = "https_spiffe"; // https_spiffe, https_web
        public int RefreshIntervalSeconds { get; set; } = 300;
    }

    public class TrustDomainStatus
    {
        public string State { get; set; } = "active";
        public int RegisteredWorkloads { get; set; }
        public int ActiveSvids { get; set; }
        public int Agents { get; set; }
        public DateTime? LastRotation { get; set; }
        public TrustBundle CurrentBundle { get; set; } = new();
    }

    public class TrustBundle
    {
        public string TrustDomainId { get; set; } = string.Empty;
        public List<X509CertificateInfo> Certificates { get; set; } = new();
        public long SequenceNumber { get; set; }
        public DateTime RefreshedAt { get; set; }
    }

    public class X509CertificateInfo
    {
        public string Subject { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public DateTime NotBefore { get; set; }
        public DateTime NotAfter { get; set; }
        public string Thumbprint { get; set; } = string.Empty;
    }

    public class SpireServer
    {
        public string ServerId { get; set; } = string.Empty;
        public string TrustDomain { get; set; } = string.Empty;
        public ServerConfig Config { get; set; } = new();
        public ServerStatus Status { get; set; } = new();
        public DateTime StartedAt { get; set; }
    }

    public class ServerConfig
    {
        public int BindPort { get; set; } = 8081;
        public string SocketPath { get; set; } = "/tmp/spire-server/private/api.sock";
        public string DataDir { get; set; } = "/var/lib/spire/server";
        public LogConfig Logging { get; set; } = new();
        public DatabaseConfig? Database { get; set; }
        public HighAvailabilityConfig? HA { get; set; }
    }

    public class LogConfig
    {
        public string Level { get; set; } = "INFO"; // DEBUG, INFO, WARN, ERROR
        public string Format { get; set; } = "json"; // text, json
    }

    public class DatabaseConfig
    {
        public string Type { get; set; } = "sqlite"; // sqlite, postgres, mysql
        public string ConnectionString { get; set; } = string.Empty;
    }

    public class HighAvailabilityConfig
    {
        public bool Enabled { get; set; }
        public int Replicas { get; set; } = 3;
        public string LeaderElection { get; set; } = "database"; // database, etcd
    }

    public class ServerStatus
    {
        public string State { get; set; } = "running";
        public bool IsLeader { get; set; }
        public int ConnectedAgents { get; set; }
        public long TotalEntries { get; set; }
        public DateTime? LastBundleRotation { get; set; }
    }

    public class SpireAgent
    {
        public string AgentId { get; set; } = string.Empty;
        public string SpiffeId { get; set; } = string.Empty; // spiffe://example.org/spire/agent/...
        public string TrustDomain { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public AgentConfig Config { get; set; } = new();
        public AgentStatus Status { get; set; } = new();
        public DateTime RegisteredAt { get; set; }
    }

    public class AgentConfig
    {
        public string ServerAddress { get; set; } = string.Empty;
        public int ServerPort { get; set; } = 8081;
        public string SocketPath { get; set; } = "/tmp/spire-agent/public/api.sock";
        public string DataDir { get; set; } = "/var/lib/spire/agent";
        public NodeAttestorConfig NodeAttestor { get; set; } = new();
        public List<WorkloadAttestorConfig> WorkloadAttestors { get; set; } = new();
    }

    public class NodeAttestorConfig
    {
        public string Type { get; set; } = "k8s_psat"; // k8s_psat, aws_iid, gcp_iit, azure_msi, join_token
        public Dictionary<string, object> Config { get; set; } = new();
    }

    public class WorkloadAttestorConfig
    {
        public string Type { get; set; } = "k8s"; // k8s, docker, unix
        public Dictionary<string, object> Config { get; set; } = new();
    }

    public class AgentStatus
    {
        public string State { get; set; } = "attested";
        public DateTime? LastAttestation { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int ManagedWorkloads { get; set; }
        public int ActiveSvids { get; set; }
    }

    public class RegistrationEntry
    {
        public string EntryId { get; set; } = string.Empty;
        public string SpiffeId { get; set; } = string.Empty;
        public string ParentId { get; set; } = string.Empty;
        public List<Selector> Selectors { get; set; } = new();
        public EntryConfig Config { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class Selector
    {
        public string Type { get; set; } = string.Empty; // k8s:ns, k8s:sa, k8s:pod-label, unix:uid, docker:label
        public string Value { get; set; } = string.Empty;
    }

    public class EntryConfig
    {
        public int TtlSeconds { get; set; } = 3600;
        public List<string> DnsNames { get; set; } = new();
        public bool Admin { get; set; }
        public bool Downstream { get; set; }
        public List<string> FederatesWith { get; set; } = new();
    }

    public class SVID
    {
        public string SvidId { get; set; } = string.Empty;
        public string SpiffeId { get; set; } = string.Empty;
        public SvidType Type { get; set; } = SvidType.X509;
        public X509SvidInfo? X509 { get; set; }
        public JwtSvidInfo? Jwt { get; set; }
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public enum SvidType
    {
        X509,
        JWT
    }

    public class X509SvidInfo
    {
        public string Certificate { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public string CertificateChain { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty;
    }

    public class JwtSvidInfo
    {
        public string Token { get; set; } = string.Empty;
        public List<string> Audience { get; set; } = new();
        public Dictionary<string, object> Claims { get; set; } = new();
    }

    public class WorkloadIdentity
    {
        public string IdentityId { get; set; } = string.Empty;
        public string SpiffeId { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string ServiceAccount { get; set; } = string.Empty;
        public string PodName { get; set; } = string.Empty;
        public IdentityStatus Status { get; set; } = new();
        public List<SVID> Svids { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class IdentityStatus
    {
        public string State { get; set; } = "active";
        public bool HasValidX509 { get; set; }
        public bool HasValidJwt { get; set; }
        public DateTime? LastRotation { get; set; }
        public int RotationCount { get; set; }
    }

    public class AccessPolicy
    {
        public string PolicyId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public PolicySpec Spec { get; set; } = new();
        public bool Enabled { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }

    public class PolicySpec
    {
        public List<string> SourceSpiffeIds { get; set; } = new();
        public List<string> DestinationSpiffeIds { get; set; } = new();
        public List<string> AllowedPaths { get; set; } = new();
        public List<string> AllowedMethods { get; set; } = new();
        public PolicyAction Action { get; set; } = PolicyAction.Allow;
        public MtlsConfig Mtls { get; set; } = new();
    }

    public enum PolicyAction
    {
        Allow,
        Deny,
        Audit
    }

    public class MtlsConfig
    {
        public bool Required { get; set; } = true;
        public string MinTlsVersion { get; set; } = "TLS1.2";
        public List<string> CipherSuites { get; set; } = new();
        public bool VerifyClientCert { get; set; } = true;
    }

    public class FederationRelationship
    {
        public string RelationshipId { get; set; } = string.Empty;
        public string LocalDomain { get; set; } = string.Empty;
        public string RemoteDomain { get; set; } = string.Empty;
        public FederationRelationshipConfig Config { get; set; } = new();
        public FederationStatus Status { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class FederationRelationshipConfig
    {
        public string BundleEndpointUrl { get; set; } = string.Empty;
        public string BundleEndpointProfile { get; set; } = "https_spiffe";
        public string? EndpointSpiffeId { get; set; }
        public int TrustDomainBundleFormat { get; set; } = 1; // SPIFFE bundle format version
    }

    public class FederationStatus
    {
        public string State { get; set; } = "active";
        public DateTime? LastBundleFetch { get; set; }
        public DateTime? NextBundleFetch { get; set; }
        public string? Error { get; set; }
    }

    public class AuthorizationDecision
    {
        public string DecisionId { get; set; } = string.Empty;
        public string SourceSpiffeId { get; set; } = string.Empty;
        public string DestinationSpiffeId { get; set; } = string.Empty;
        public string Resource { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public bool Allowed { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? MatchedPolicy { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ZeroTrustMetrics
    {
        public string MetricsId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int TotalWorkloads { get; set; }
        public int WorkloadsWithValidSvid { get; set; }
        public int ActiveAgents { get; set; }
        public long TotalSvidsIssued { get; set; }
        public long TotalRotations { get; set; }
        public long AuthorizationDecisions { get; set; }
        public long AllowedRequests { get; set; }
        public long DeniedRequests { get; set; }
        public int FederatedDomains { get; set; }
        public Dictionary<string, DomainMetrics> DomainMetrics { get; set; } = new();
    }

    public class DomainMetrics
    {
        public string DomainName { get; set; } = string.Empty;
        public int Workloads { get; set; }
        public int Agents { get; set; }
        public long SvidsIssued { get; set; }
        public double AverageSvidLifetimeHours { get; set; }
    }

    public class AuditEvent
    {
        public string EventId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty; // svid_issued, svid_rotated, access_denied, policy_changed
        public string SpiffeId { get; set; } = string.Empty;
        public Dictionary<string, object> Details { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    // ============================================================================
    // INTERFACE
    // ============================================================================

    public interface IZeroTrustSecurityEngine
    {
        // Trust Domains
        Task<TrustDomain> CreateTrustDomainAsync(string tenantId, TrustDomain domain, CancellationToken cancellation = default);
        Task<TrustDomain> GetTrustDomainAsync(string tenantId, string domainId, CancellationToken cancellation = default);
        Task<TrustBundle> GetTrustBundleAsync(string tenantId, string domainId, CancellationToken cancellation = default);

        // SPIRE Server
        Task<SpireServer> DeployServerAsync(string tenantId, SpireServer server, CancellationToken cancellation = default);
        Task<ServerStatus> GetServerStatusAsync(string tenantId, string serverId, CancellationToken cancellation = default);

        // SPIRE Agents
        Task<SpireAgent> RegisterAgentAsync(string tenantId, SpireAgent agent, CancellationToken cancellation = default);
        Task<List<SpireAgent>> ListAgentsAsync(string tenantId, string trustDomain, CancellationToken cancellation = default);
        Task<bool> DeleteAgentAsync(string tenantId, string agentId, CancellationToken cancellation = default);

        // Registration Entries
        Task<RegistrationEntry> CreateEntryAsync(string tenantId, RegistrationEntry entry, CancellationToken cancellation = default);
        Task<RegistrationEntry> GetEntryAsync(string tenantId, string entryId, CancellationToken cancellation = default);
        Task<List<RegistrationEntry>> ListEntriesAsync(string tenantId, string? spiffeId = null, CancellationToken cancellation = default);
        Task<bool> DeleteEntryAsync(string tenantId, string entryId, CancellationToken cancellation = default);

        // SVIDs
        Task<SVID> IssueSvidAsync(string tenantId, string workloadId, SvidType type, CancellationToken cancellation = default);
        Task<SVID> RotateSvidAsync(string tenantId, string svidId, CancellationToken cancellation = default);
        Task<bool> RevokeSvidAsync(string tenantId, string svidId, CancellationToken cancellation = default);

        // Workload Identity
        Task<WorkloadIdentity> GetWorkloadIdentityAsync(string tenantId, string identityId, CancellationToken cancellation = default);
        Task<List<WorkloadIdentity>> ListWorkloadIdentitiesAsync(string tenantId, string? @namespace = null, CancellationToken cancellation = default);

        // Access Policies
        Task<AccessPolicy> CreatePolicyAsync(string tenantId, AccessPolicy policy, CancellationToken cancellation = default);
        Task<AuthorizationDecision> EvaluatePolicyAsync(string tenantId, string sourceSpiffeId, string destSpiffeId, string resource, string action, CancellationToken cancellation = default);
        Task<List<AccessPolicy>> ListPoliciesAsync(string tenantId, CancellationToken cancellation = default);

        // Federation
        Task<FederationRelationship> CreateFederationAsync(string tenantId, FederationRelationship relationship, CancellationToken cancellation = default);
        Task<List<FederationRelationship>> ListFederationsAsync(string tenantId, string domainId, CancellationToken cancellation = default);
        Task<bool> RefreshFederationBundleAsync(string tenantId, string relationshipId, CancellationToken cancellation = default);

        // Metrics & Audit
        Task<ZeroTrustMetrics> GetMetricsAsync(string tenantId, CancellationToken cancellation = default);
        Task<List<AuditEvent>> GetAuditEventsAsync(string tenantId, DateTime since, CancellationToken cancellation = default);
    }

    // ============================================================================
    // IMPLEMENTATION
    // ============================================================================

    public class ZeroTrustSecurityEngine : IZeroTrustSecurityEngine
    {
        private readonly ILogger<ZeroTrustSecurityEngine> _logger;
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly Dictionary<string, TrustDomain> _trustDomains = new();
        private readonly Dictionary<string, SpireServer> _servers = new();
        private readonly Dictionary<string, SpireAgent> _agents = new();
        private readonly Dictionary<string, RegistrationEntry> _entries = new();
        private readonly Dictionary<string, SVID> _svids = new();
        private readonly Dictionary<string, WorkloadIdentity> _identities = new();
        private readonly Dictionary<string, AccessPolicy> _policies = new();
        private readonly Dictionary<string, FederationRelationship> _federations = new();
        private readonly List<AuditEvent> _auditEvents = new();
        private readonly Random _random = new(42);

        public ZeroTrustSecurityEngine(ILogger<ZeroTrustSecurityEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TrustDomain> CreateTrustDomainAsync(string tenantId, TrustDomain domain, CancellationToken cancellation = default)
        {
            domain.DomainId = Guid.NewGuid().ToString();
            domain.CreatedAt = DateTime.UtcNow;
            domain.SpiffeId = $"spiffe://{domain.Name}";
            domain.Status = new TrustDomainStatus
            {
                State = "active",
                RegisteredWorkloads = 0,
                ActiveSvids = 0,
                Agents = 0,
                CurrentBundle = new TrustBundle
                {
                    TrustDomainId = domain.DomainId,
                    SequenceNumber = 1,
                    RefreshedAt = DateTime.UtcNow,
                    Certificates = new List<X509CertificateInfo>
                    {
                        new X509CertificateInfo
                        {
                            Subject = $"CN={domain.Name}",
                            Issuer = $"CN={domain.Name}",
                            SerialNumber = Guid.NewGuid().ToString("N"),
                            NotBefore = DateTime.UtcNow,
                            NotAfter = DateTime.UtcNow.AddYears(1),
                            Thumbprint = Guid.NewGuid().ToString("N").Substring(0, 40).ToUpper()
                        }
                    }
                }
            };

            var key = $"{tenantId}:{domain.DomainId}";
            _lock.EnterWriteLock();
            try
            {
                _trustDomains[key] = domain;
                _logger.LogInformation($"Created trust domain {domain.Name} ({domain.SpiffeId})");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return domain;
        }

        public async Task<TrustDomain> GetTrustDomainAsync(string tenantId, string domainId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{domainId}";

            _lock.EnterReadLock();
            try
            {
                if (_trustDomains.TryGetValue(key, out var domain))
                {
                    return domain;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new TrustDomain();
        }

        public async Task<TrustBundle> GetTrustBundleAsync(string tenantId, string domainId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{domainId}";

            _lock.EnterReadLock();
            try
            {
                if (_trustDomains.TryGetValue(key, out var domain))
                {
                    return domain.Status.CurrentBundle;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new TrustBundle();
        }

        public async Task<SpireServer> DeployServerAsync(string tenantId, SpireServer server, CancellationToken cancellation = default)
        {
            server.ServerId = Guid.NewGuid().ToString();
            server.StartedAt = DateTime.UtcNow;
            server.Status = new ServerStatus
            {
                State = "running",
                IsLeader = true,
                ConnectedAgents = 0,
                TotalEntries = 0
            };

            var key = $"{tenantId}:{server.ServerId}";
            _lock.EnterWriteLock();
            try
            {
                _servers[key] = server;
                _logger.LogInformation($"Deployed SPIRE server for trust domain {server.TrustDomain} (port: {server.Config.BindPort})");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return server;
        }

        public async Task<ServerStatus> GetServerStatusAsync(string tenantId, string serverId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{serverId}";

            _lock.EnterReadLock();
            try
            {
                if (_servers.TryGetValue(key, out var server))
                {
                    server.Status.ConnectedAgents = _agents.Values.Count(a => a.TrustDomain == server.TrustDomain);
                    server.Status.TotalEntries = _entries.Count;
                    return server.Status;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new ServerStatus();
        }

        public async Task<SpireAgent> RegisterAgentAsync(string tenantId, SpireAgent agent, CancellationToken cancellation = default)
        {
            agent.AgentId = Guid.NewGuid().ToString();
            agent.RegisteredAt = DateTime.UtcNow;
            agent.SpiffeId = $"spiffe://{agent.TrustDomain}/spire/agent/{agent.Config.NodeAttestor.Type}/{agent.NodeName}";
            agent.Status = new AgentStatus
            {
                State = "attested",
                LastAttestation = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                ManagedWorkloads = 0,
                ActiveSvids = 0
            };

            var key = $"{tenantId}:{agent.AgentId}";
            _lock.EnterWriteLock();
            try
            {
                _agents[key] = agent;

                // Update trust domain agent count
                foreach (var domain in _trustDomains.Values)
                {
                    if (domain.Name == agent.TrustDomain)
                    {
                        domain.Status.Agents++;
                        break;
                    }
                }

                _logger.LogInformation($"Registered SPIRE agent {agent.NodeName} ({agent.SpiffeId}) using {agent.Config.NodeAttestor.Type}");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return agent;
        }

        public async Task<List<SpireAgent>> ListAgentsAsync(string tenantId, string trustDomain, CancellationToken cancellation = default)
        {
            var agents = new List<SpireAgent>();

            _lock.EnterReadLock();
            try
            {
                agents = _agents.Values
                    .Where(a => a.TrustDomain == trustDomain)
                    .ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _logger.LogInformation($"Listed {agents.Count} agents for trust domain {trustDomain}");

            await Task.CompletedTask;
            return agents;
        }

        public async Task<bool> DeleteAgentAsync(string tenantId, string agentId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{agentId}";

            _lock.EnterWriteLock();
            try
            {
                if (_agents.Remove(key))
                {
                    _logger.LogInformation($"Deleted SPIRE agent {agentId}");
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

        public async Task<RegistrationEntry> CreateEntryAsync(string tenantId, RegistrationEntry entry, CancellationToken cancellation = default)
        {
            entry.EntryId = Guid.NewGuid().ToString();
            entry.CreatedAt = DateTime.UtcNow;

            var key = $"{tenantId}:{entry.EntryId}";
            _lock.EnterWriteLock();
            try
            {
                _entries[key] = entry;

                var selectorStr = string.Join(", ", entry.Selectors.Select(s => $"{s.Type}:{s.Value}"));
                _logger.LogInformation($"Created registration entry {entry.SpiffeId} with selectors [{selectorStr}]");

                _auditEvents.Add(new AuditEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    EventType = "entry_created",
                    SpiffeId = entry.SpiffeId,
                    Details = new Dictionary<string, object>
                    {
                        { "entryId", entry.EntryId },
                        { "selectors", entry.Selectors.Count }
                    },
                    Timestamp = DateTime.UtcNow
                });
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return entry;
        }

        public async Task<RegistrationEntry> GetEntryAsync(string tenantId, string entryId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{entryId}";

            _lock.EnterReadLock();
            try
            {
                if (_entries.TryGetValue(key, out var entry))
                {
                    return entry;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new RegistrationEntry();
        }

        public async Task<List<RegistrationEntry>> ListEntriesAsync(string tenantId, string? spiffeId = null, CancellationToken cancellation = default)
        {
            var entries = new List<RegistrationEntry>();

            _lock.EnterReadLock();
            try
            {
                entries = _entries.Values
                    .Where(e => spiffeId == null || e.SpiffeId == spiffeId || e.ParentId == spiffeId)
                    .ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _logger.LogInformation($"Listed {entries.Count} registration entries");

            await Task.CompletedTask;
            return entries;
        }

        public async Task<bool> DeleteEntryAsync(string tenantId, string entryId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{entryId}";

            _lock.EnterWriteLock();
            try
            {
                if (_entries.Remove(key))
                {
                    _logger.LogInformation($"Deleted registration entry {entryId}");
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

        public async Task<SVID> IssueSvidAsync(string tenantId, string workloadId, SvidType type, CancellationToken cancellation = default)
        {
            var svid = new SVID
            {
                SvidId = Guid.NewGuid().ToString(),
                SpiffeId = $"spiffe://example.org/workload/{workloadId}",
                Type = type,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };

            if (type == SvidType.X509)
            {
                svid.X509 = new X509SvidInfo
                {
                    Certificate = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                    PrivateKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                    CertificateChain = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                    Hint = workloadId
                };
            }
            else
            {
                svid.Jwt = new JwtSvidInfo
                {
                    Token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                    Audience = new List<string> { "api.example.org" },
                    Claims = new Dictionary<string, object>
                    {
                        { "sub", svid.SpiffeId },
                        { "iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                        { "exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }
                    }
                };
            }

            var key = $"{tenantId}:{svid.SvidId}";
            _lock.EnterWriteLock();
            try
            {
                _svids[key] = svid;
                _logger.LogInformation($"Issued {type} SVID for {svid.SpiffeId} (expires: {svid.ExpiresAt})");

                _auditEvents.Add(new AuditEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    EventType = "svid_issued",
                    SpiffeId = svid.SpiffeId,
                    Details = new Dictionary<string, object>
                    {
                        { "svidId", svid.SvidId },
                        { "type", type.ToString() },
                        { "ttl", 3600 }
                    },
                    Timestamp = DateTime.UtcNow
                });
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return svid;
        }

        public async Task<SVID> RotateSvidAsync(string tenantId, string svidId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{svidId}";

            _lock.EnterWriteLock();
            try
            {
                if (_svids.TryGetValue(key, out var svid))
                {
                    svid.IssuedAt = DateTime.UtcNow;
                    svid.ExpiresAt = DateTime.UtcNow.AddHours(1);

                    if (svid.Type == SvidType.X509 && svid.X509 != null)
                    {
                        svid.X509.Certificate = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                        svid.X509.PrivateKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                    }
                    else if (svid.Jwt != null)
                    {
                        svid.Jwt.Token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                    }

                    _logger.LogInformation($"Rotated SVID {svidId} ({svid.SpiffeId})");

                    _auditEvents.Add(new AuditEvent
                    {
                        EventId = Guid.NewGuid().ToString(),
                        EventType = "svid_rotated",
                        SpiffeId = svid.SpiffeId,
                        Details = new Dictionary<string, object> { { "svidId", svidId } },
                        Timestamp = DateTime.UtcNow
                    });

                    return svid;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return new SVID();
        }

        public async Task<bool> RevokeSvidAsync(string tenantId, string svidId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{svidId}";

            _lock.EnterWriteLock();
            try
            {
                if (_svids.Remove(key))
                {
                    _logger.LogInformation($"Revoked SVID {svidId}");
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

        public async Task<WorkloadIdentity> GetWorkloadIdentityAsync(string tenantId, string identityId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{identityId}";

            _lock.EnterReadLock();
            try
            {
                if (_identities.TryGetValue(key, out var identity))
                {
                    return identity;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new WorkloadIdentity();
        }

        public async Task<List<WorkloadIdentity>> ListWorkloadIdentitiesAsync(string tenantId, string? @namespace = null, CancellationToken cancellation = default)
        {
            var identities = new List<WorkloadIdentity>();

            _lock.EnterReadLock();
            try
            {
                identities = _identities.Values
                    .Where(i => @namespace == null || i.Namespace == @namespace)
                    .ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _logger.LogInformation($"Listed {identities.Count} workload identities");

            await Task.CompletedTask;
            return identities;
        }

        public async Task<AccessPolicy> CreatePolicyAsync(string tenantId, AccessPolicy policy, CancellationToken cancellation = default)
        {
            policy.PolicyId = Guid.NewGuid().ToString();
            policy.CreatedAt = DateTime.UtcNow;

            var key = $"{tenantId}:{policy.PolicyId}";
            _lock.EnterWriteLock();
            try
            {
                _policies[key] = policy;
                _logger.LogInformation($"Created access policy {policy.Name} ({policy.Spec.Action}): {policy.Spec.SourceSpiffeIds.Count} sources -> {policy.Spec.DestinationSpiffeIds.Count} destinations");

                _auditEvents.Add(new AuditEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    EventType = "policy_created",
                    SpiffeId = string.Join(",", policy.Spec.SourceSpiffeIds),
                    Details = new Dictionary<string, object>
                    {
                        { "policyId", policy.PolicyId },
                        { "action", policy.Spec.Action.ToString() }
                    },
                    Timestamp = DateTime.UtcNow
                });
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return policy;
        }

        public async Task<AuthorizationDecision> EvaluatePolicyAsync(string tenantId, string sourceSpiffeId, string destSpiffeId, string resource, string action, CancellationToken cancellation = default)
        {
            var decision = new AuthorizationDecision
            {
                DecisionId = Guid.NewGuid().ToString(),
                SourceSpiffeId = sourceSpiffeId,
                DestinationSpiffeId = destSpiffeId,
                Resource = resource,
                Action = action,
                Timestamp = DateTime.UtcNow
            };

            _lock.EnterReadLock();
            try
            {
                // Find matching policy
                var matchingPolicy = _policies.Values.FirstOrDefault(p =>
                    p.Enabled &&
                    (p.Spec.SourceSpiffeIds.Contains(sourceSpiffeId) || p.Spec.SourceSpiffeIds.Contains("*")) &&
                    (p.Spec.DestinationSpiffeIds.Contains(destSpiffeId) || p.Spec.DestinationSpiffeIds.Contains("*")));

                if (matchingPolicy != null)
                {
                    decision.Allowed = matchingPolicy.Spec.Action == PolicyAction.Allow;
                    decision.MatchedPolicy = matchingPolicy.PolicyId;
                    decision.Reason = $"Matched policy {matchingPolicy.Name}";
                }
                else
                {
                    // Default deny
                    decision.Allowed = false;
                    decision.Reason = "No matching policy found - default deny";
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _logger.LogInformation($"Authorization decision: {sourceSpiffeId} -> {destSpiffeId} [{action}] = {(decision.Allowed ? "ALLOW" : "DENY")}");

            _auditEvents.Add(new AuditEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = decision.Allowed ? "access_allowed" : "access_denied",
                SpiffeId = sourceSpiffeId,
                Details = new Dictionary<string, object>
                {
                    { "destination", destSpiffeId },
                    { "resource", resource },
                    { "action", action }
                },
                Timestamp = DateTime.UtcNow
            });

            await Task.CompletedTask;
            return decision;
        }

        public async Task<List<AccessPolicy>> ListPoliciesAsync(string tenantId, CancellationToken cancellation = default)
        {
            var policies = new List<AccessPolicy>();

            _lock.EnterReadLock();
            try
            {
                policies = _policies.Values.ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _logger.LogInformation($"Listed {policies.Count} access policies");

            await Task.CompletedTask;
            return policies;
        }

        public async Task<FederationRelationship> CreateFederationAsync(string tenantId, FederationRelationship relationship, CancellationToken cancellation = default)
        {
            relationship.RelationshipId = Guid.NewGuid().ToString();
            relationship.CreatedAt = DateTime.UtcNow;
            relationship.Status = new FederationStatus
            {
                State = "active",
                LastBundleFetch = DateTime.UtcNow,
                NextBundleFetch = DateTime.UtcNow.AddMinutes(5)
            };

            var key = $"{tenantId}:{relationship.RelationshipId}";
            _lock.EnterWriteLock();
            try
            {
                _federations[key] = relationship;
                _logger.LogInformation($"Created federation relationship {relationship.LocalDomain} <-> {relationship.RemoteDomain}");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return relationship;
        }

        public async Task<List<FederationRelationship>> ListFederationsAsync(string tenantId, string domainId, CancellationToken cancellation = default)
        {
            var federations = new List<FederationRelationship>();

            _lock.EnterReadLock();
            try
            {
                federations = _federations.Values
                    .Where(f => f.LocalDomain == domainId)
                    .ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _logger.LogInformation($"Listed {federations.Count} federation relationships for domain {domainId}");

            await Task.CompletedTask;
            return federations;
        }

        public async Task<bool> RefreshFederationBundleAsync(string tenantId, string relationshipId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{relationshipId}";

            _lock.EnterWriteLock();
            try
            {
                if (_federations.TryGetValue(key, out var federation))
                {
                    federation.Status.LastBundleFetch = DateTime.UtcNow;
                    federation.Status.NextBundleFetch = DateTime.UtcNow.AddMinutes(5);
                    _logger.LogInformation($"Refreshed federation bundle for {federation.LocalDomain} <-> {federation.RemoteDomain}");
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

        public async Task<ZeroTrustMetrics> GetMetricsAsync(string tenantId, CancellationToken cancellation = default)
        {
            var metrics = new ZeroTrustMetrics
            {
                MetricsId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                TotalWorkloads = _identities.Count + _random.Next(50, 200),
                WorkloadsWithValidSvid = _svids.Count + _random.Next(40, 150),
                ActiveAgents = _agents.Count + _random.Next(10, 50),
                TotalSvidsIssued = _random.Next(10000, 100000),
                TotalRotations = _random.Next(50000, 500000),
                AuthorizationDecisions = _random.Next(1000000, 10000000),
                AllowedRequests = _random.Next(900000, 9500000),
                DeniedRequests = _random.Next(5000, 50000),
                FederatedDomains = _federations.Count,
                DomainMetrics = new Dictionary<string, DomainMetrics>()
            };

            foreach (var domain in _trustDomains.Values)
            {
                metrics.DomainMetrics[domain.Name] = new DomainMetrics
                {
                    DomainName = domain.Name,
                    Workloads = domain.Status.RegisteredWorkloads + _random.Next(10, 100),
                    Agents = domain.Status.Agents + _random.Next(5, 20),
                    SvidsIssued = _random.Next(1000, 10000),
                    AverageSvidLifetimeHours = 0.8 + _random.NextDouble() * 0.4
                };
            }

            _logger.LogInformation($"Zero trust metrics: {metrics.TotalWorkloads} workloads, {metrics.ActiveAgents} agents, {metrics.AllowedRequests} allowed/{metrics.DeniedRequests} denied");

            await Task.CompletedTask;
            return metrics;
        }

        public async Task<List<AuditEvent>> GetAuditEventsAsync(string tenantId, DateTime since, CancellationToken cancellation = default)
        {
            var events = new List<AuditEvent>();

            _lock.EnterReadLock();
            try
            {
                events = _auditEvents
                    .Where(e => e.Timestamp >= since)
                    .OrderByDescending(e => e.Timestamp)
                    .Take(1000)
                    .ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _logger.LogInformation($"Retrieved {events.Count} audit events since {since}");

            await Task.CompletedTask;
            return events;
        }
    }
}
