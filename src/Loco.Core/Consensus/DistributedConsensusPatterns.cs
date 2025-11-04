#nullable enable

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Consensus;

/// <summary>
/// Distributed Consensus Patterns
/// PBFT, Raft, Byzantine fault tolerance, consensus algorithms
/// </summary>

public class ConsensusNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("nodeId")]
    public int NodeId { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = "follower"; // follower, candidate, leader

    [JsonPropertyName("term")]
    public long Term { get; set; } = 0;

    [JsonPropertyName("votedFor")]
    public int? VotedFor { get; set; }

    [JsonPropertyName("lastHeartbeat")]
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("logs")]
    public List<LogEntry> Logs { get; set; } = new();
}

public class LogEntry
{
    [JsonPropertyName("index")]
    public long Index { get; set; }

    [JsonPropertyName("term")]
    public long Term { get; set; }

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new();

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class PBFTMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // pre-prepare, prepare, commit

    [JsonPropertyName("viewNumber")]
    public long ViewNumber { get; set; }

    [JsonPropertyName("sequenceNumber")]
    public long SequenceNumber { get; set; }

    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;

    [JsonPropertyName("senderId")]
    public int SenderId { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ByzantineFaultTolerance
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("totalNodes")]
    public int TotalNodes { get; set; }

    [JsonPropertyName("maxFaultyNodes")]
    public int MaxFaultyNodes { get; set; } // floor((n-1)/3)

    [JsonPropertyName("minimumQuorum")]
    public int MinimumQuorum { get; set; } // 2*f + 1

    [JsonPropertyName("faultyNodesDetected")]
    public int FaultyNodesDetected { get; set; } = 0;

    [JsonPropertyName("isSystemSafe")]
    public bool IsSystemSafe => FaultyNodesDetected <= MaxFaultyNodes;
}

public class ConsensusMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("consensusRound")]
    public long ConsensusRound { get; set; }

    [JsonPropertyName("fromNode")]
    public int FromNode { get; set; }

    [JsonPropertyName("messageType")]
    public string MessageType { get; set; } = string.Empty; // proposal, vote, acknowledgement

    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;

    [JsonPropertyName("digest")]
    public string Digest { get; set; } = string.Empty;

    [JsonPropertyName("receivedAt")]
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

public class ConsensusStatistics
{
    [JsonPropertyName("totalProposals")]
    public long TotalProposals { get; set; }

    [JsonPropertyName("committedBlocks")]
    public long CommittedBlocks { get; set; }

    [JsonPropertyName("averageLatencyMs")]
    public double AverageLatencyMs { get; set; }

    [JsonPropertyName("p99LatencyMs")]
    public double P99LatencyMs { get; set; }

    [JsonPropertyName("throughputTxSec")]
    public double ThroughputTxSec { get; set; }

    [JsonPropertyName("consensusType")]
    public string ConsensusType { get; set; } = string.Empty; // Raft, PBFT, V-PBFT
}

/// <summary>
/// Distributed Consensus Engine
/// </summary>
public class DistributedConsensusEngine
{
    private readonly ConcurrentDictionary<int, ConsensusNode> _nodes = new();
    private readonly List<ConsensusMessage> _messageLog = new();
    private readonly ByzantineFaultTolerance _bft = new();
    private readonly ConsensusStatistics _stats = new();
    private readonly ILogger<DistributedConsensusEngine> _logger;
    private long _currentRound = 0;

    public DistributedConsensusEngine(ILogger<DistributedConsensusEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initialize consensus cluster
    /// </summary>
    public async Task InitializeClusterAsync(int totalNodes, string consensusType = "Raft")
    {
        _stats.ConsensusType = consensusType;
        _bft.TotalNodes = totalNodes;
        _bft.MaxFaultyNodes = (totalNodes - 1) / 3;
        _bft.MinimumQuorum = 2 * _bft.MaxFaultyNodes + 1;

        for (int i = 0; i < totalNodes; i++)
        {
            var node = new ConsensusNode { NodeId = i };
            _nodes[i] = node;
        }

        _logger.LogInformation(
            "Initialized consensus cluster: {Type} with {Nodes} nodes, BFT: {MaxFaulty}/{Quorum}",
            consensusType,
            totalNodes,
            _bft.MaxFaultyNodes,
            _bft.MinimumQuorum);
    }

    /// <summary>
    /// Process consensus message
    /// </summary>
    public async Task<bool> ProcessConsensusMessageAsync(ConsensusMessage message)
    {
        _messageLog.Add(message);
        _currentRound++;

        if (!_nodes.TryGetValue(message.FromNode, out var node))
            return false;

        switch (message.MessageType)
        {
            case "proposal":
                return await HandleProposalAsync(message, node);
            case "vote":
                return await HandleVoteAsync(message, node);
            case "acknowledgement":
                return await HandleAcknowledgementAsync(message, node);
            default:
                return false;
        }
    }

    /// <summary>
    /// Propose value to consensus
    /// </summary>
    public async Task<LogEntry?> ProposeValueAsync(string command, Dictionary<string, object> data)
    {
        var leader = _nodes.Values.FirstOrDefault(n => n.State == "leader");
        if (leader == null)
        {
            _logger.LogWarning("No leader elected, cannot propose value");
            return null;
        }

        var entry = new LogEntry
        {
            Index = leader.Logs.Count + 1,
            Term = leader.Term,
            Command = command,
            Data = data
        };

        leader.Logs.Add(entry);
        _stats.TotalProposals++;

        _logger.LogInformation(
            "Proposed value: {Command} at index {Index}",
            command,
            entry.Index);

        return entry;
    }

    /// <summary>
    /// Elect leader (Raft-based)
    /// </summary>
    public async Task<ConsensusNode?> ElectLeaderAsync()
    {
        var maxTerm = _nodes.Values.Max(n => n.Term);
        var candidates = _nodes.Values.Where(n => n.Term >= maxTerm - 1).ToList();

        if (candidates.Count == 0)
            return null;

        var leader = candidates.OrderByDescending(n => n.Logs.Count).First();
        leader.State = "leader";
        leader.Term = maxTerm + 1;

        foreach (var node in _nodes.Values.Where(n => n.NodeId != leader.NodeId))
        {
            node.State = "follower";
            node.VotedFor = leader.NodeId;
        }

        _logger.LogInformation(
            "Elected leader: Node {NodeId} with term {Term}",
            leader.NodeId,
            leader.Term);

        return leader;
    }

    /// <summary>
    /// Commit log entry across quorum
    /// </summary>
    public async Task<bool> CommitEntryAsync(long logIndex)
    {
        var replicatedCount = _nodes.Values.Count(n =>
            n.Logs.Any(l => l.Index == logIndex));

        if (replicatedCount >= _bft.MinimumQuorum)
        {
            _stats.CommittedBlocks++;

            _logger.LogInformation(
                "Committed log entry {Index} across quorum ({Count}/{Required})",
                logIndex,
                replicatedCount,
                _bft.MinimumQuorum);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Detect faulty node
    /// </summary>
    public async Task DetectFaultyNodeAsync(int nodeId)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            _bft.FaultyNodesDetected++;

            _logger.LogWarning(
                "Detected faulty node: {NodeId}, Total faults: {Count}/{Max}",
                nodeId,
                _bft.FaultyNodesDetected,
                _bft.MaxFaultyNodes);
        }
    }

    /// <summary>
    /// Get consensus statistics
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        var recentMessages = _messageLog.TakeLast(100).ToList();
        var latencies = recentMessages
            .Select(m => (DateTime.UtcNow - m.ReceivedAt).TotalMilliseconds)
            .ToList();

        return new()
        {
            ["totalNodes"] = _nodes.Count,
            ["leaderNode"] = _nodes.Values.FirstOrDefault(n => n.State == "leader")?.NodeId ?? -1,
            ["totalProposals"] = _stats.TotalProposals,
            ["committedBlocks"] = _stats.CommittedBlocks,
            ["consensusType"] = _stats.ConsensusType,
            ["maxFaultyNodes"] = _bft.MaxFaultyNodes,
            ["currentFaultyNodes"] = _bft.FaultyNodesDetected,
            ["systemSafe"] = _bft.IsSystemSafe,
            ["averageLatencyMs"] = latencies.Count > 0 ? latencies.Average() : 0,
            ["messagesProcessed"] = _messageLog.Count
        };
    }

    private async Task<bool> HandleProposalAsync(ConsensusMessage message, ConsensusNode node)
    {
        node.LastHeartbeat = DateTime.UtcNow;
        return true;
    }

    private async Task<bool> HandleVoteAsync(ConsensusMessage message, ConsensusNode node)
    {
        var voteCount = _messageLog
            .Where(m => m.ConsensusRound == message.ConsensusRound && m.MessageType == "vote")
            .Count();

        return voteCount >= _bft.MinimumQuorum;
    }

    private async Task<bool> HandleAcknowledgementAsync(ConsensusMessage message, ConsensusNode node)
    {
        node.LastHeartbeat = DateTime.UtcNow;
        return true;
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class ConsensusExtensions
{
    public static IServiceCollection AddDistributedConsensus(this IServiceCollection services)
    {
        services.AddSingleton<DistributedConsensusEngine>();
        return services;
    }
}
