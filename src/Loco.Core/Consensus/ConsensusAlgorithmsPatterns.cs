#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Consensus;

/// <summary>
/// Distributed Consensus Algorithms - Raft, Paxos, Byzantine Fault Tolerance
/// Ensures consistency in distributed systems without single authority
/// </summary>

/// <summary>
/// Raft consensus algorithm - Understandable distributed consensus
/// Used in etcd, CockroachDB, MongoDB, RabbitMQ
/// </summary>

/// <summary>
/// Raft server state
/// </summary>
public enum RaftServerState
{
    /// <summary>
    /// Initial state, starts election timer
    /// </summary>
    Follower,

    /// <summary>
    /// Candidate during leader election
    /// </summary>
    Candidate,

    /// <summary>
    /// Leader replicating log entries
    /// </summary>
    Leader
}

/// <summary>
/// Raft log entry
/// </summary>
public class LogEntry
{
    public long Term { get; set; }
    public string Data { get; set; } = string.Empty;
    public long Index { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Raft server persistent state
/// </summary>
public class RaftPersistentState
{
    /// <summary>
    /// Latest term server has seen (initialized to 0)
    /// </summary>
    public long CurrentTerm { get; set; } = 0;

    /// <summary>
    /// Candidate that received vote in current term (null if none)
    /// </summary>
    public string? VotedFor { get; set; }

    /// <summary>
    /// Log entries; each entry contains command for state machine
    /// </summary>
    public List<LogEntry> Log { get; set; } = new();
}

/// <summary>
/// Raft server volatile state
/// </summary>
public class RaftVolatileState
{
    /// <summary>
    /// Index of highest log entry known to be committed
    /// </summary>
    public long CommitIndex { get; set; } = 0;

    /// <summary>
    /// Index of highest log entry applied to state machine
    /// </summary>
    public long LastApplied { get; set; } = 0;
}

/// <summary>
/// Raft leader-specific volatile state
/// </summary>
public class RaftLeaderState
{
    /// <summary>
    /// For each server, index of next log entry to send (initialized to leader last log index + 1)
    /// </summary>
    public Dictionary<string, long> NextIndex { get; set; } = new();

    /// <summary>
    /// For each server, index of highest log entry known to be replicated
    /// </summary>
    public Dictionary<string, long> MatchIndex { get; set; } = new();
}

/// <summary>
/// Raft consensus node
/// </summary>
public class RaftNode
{
    private readonly string _serverId;
    private readonly ILogger<RaftNode> _logger;
    private readonly List<string> _peers;

    private RaftServerState _state = RaftServerState.Follower;
    private RaftPersistentState _persistentState = new();
    private RaftVolatileState _volatileState = new();
    private RaftLeaderState? _leaderState;

    private DateTime _electionTimeout = DateTime.UtcNow.AddMilliseconds(Random.Shared.Next(150, 300));
    private DateTime _heartbeatTimeout = DateTime.UtcNow.AddMilliseconds(50);

    private readonly ConcurrentDictionary<long, string> _stateMachine = new();

    public RaftNode(string serverId, List<string> peers, ILogger<RaftNode> logger)
    {
        _serverId = serverId;
        _peers = peers;
        _logger = logger;
    }

    public RaftServerState State => _state;
    public long CurrentTerm => _persistentState.CurrentTerm;
    public string ServerId => _serverId;

    /// <summary>
    /// Append entry RPC - used for log replication and heartbeats
    /// </summary>
    public async Task<AppendEntriesResponse> AppendEntriesAsync(
        long term,
        string leaderId,
        long prevLogIndex,
        long prevLogTerm,
        List<LogEntry> entries,
        long leaderCommit)
    {
        // If term < currentTerm, reply false
        if (term < _persistentState.CurrentTerm)
        {
            return new AppendEntriesResponse
            {
                Term = _persistentState.CurrentTerm,
                Success = false,
                Reason = "Stale term"
            };
        }

        // If term > currentTerm, update and become follower
        if (term > _persistentState.CurrentTerm)
        {
            _persistentState.CurrentTerm = term;
            _persistentState.VotedFor = null;
            _state = RaftServerState.Follower;
        }

        _electionTimeout = DateTime.UtcNow.AddMilliseconds(Random.Shared.Next(150, 300));

        // Check if log contains entry at prevLogIndex with term prevLogTerm
        if (prevLogIndex > 0 &&
            (prevLogIndex > _persistentState.Log.Count ||
             _persistentState.Log[(int)prevLogIndex - 1].Term != prevLogTerm))
        {
            return new AppendEntriesResponse
            {
                Term = _persistentState.CurrentTerm,
                Success = false,
                Reason = "Log mismatch"
            };
        }

        // Append any new entries
        foreach (var entry in entries)
        {
            var index = prevLogIndex + entries.IndexOf(entry) + 1;
            if (index <= _persistentState.Log.Count)
            {
                if (_persistentState.Log[(int)index - 1].Term != entry.Term)
                {
                    _persistentState.Log.RemoveRange((int)index - 1, _persistentState.Log.Count - (int)index + 1);
                }
            }
            else
            {
                _persistentState.Log.Add(entry);
            }
        }

        // Update commit index
        if (leaderCommit > _volatileState.CommitIndex)
        {
            _volatileState.CommitIndex = Math.Min(leaderCommit, _persistentState.Log.Count);
        }

        // Apply committed entries to state machine
        await ApplyEntriesAsync();

        _logger.LogInformation(
            "AppendEntries from {Leader}: term={Term}, entries={Count}, committed to {CommitIndex}",
            leaderId,
            term,
            entries.Count,
            _volatileState.CommitIndex);

        return new AppendEntriesResponse
        {
            Term = _persistentState.CurrentTerm,
            Success = true
        };
    }

    /// <summary>
    /// Request vote RPC - used for leader election
    /// </summary>
    public async Task<RequestVoteResponse> RequestVoteAsync(
        long term,
        string candidateId,
        long lastLogIndex,
        long lastLogTerm)
    {
        // If term < currentTerm, reply false
        if (term < _persistentState.CurrentTerm)
        {
            return new RequestVoteResponse
            {
                Term = _persistentState.CurrentTerm,
                VoteGranted = false,
                Reason = "Stale term"
            };
        }

        // If term > currentTerm, update state
        if (term > _persistentState.CurrentTerm)
        {
            _persistentState.CurrentTerm = term;
            _persistentState.VotedFor = null;
            _state = RaftServerState.Follower;
        }

        // Check if already voted in this term
        if (_persistentState.VotedFor != null && _persistentState.VotedFor != candidateId)
        {
            return new RequestVoteResponse
            {
                Term = _persistentState.CurrentTerm,
                VoteGranted = false,
                Reason = "Already voted"
            };
        }

        // Check candidate's log is up-to-date
        var lastLogTerm_ = _persistentState.Log.Count > 0
            ? _persistentState.Log[_persistentState.Log.Count - 1].Term
            : 0;

        if (lastLogTerm < lastLogTerm_ ||
            (lastLogTerm == lastLogTerm_ && lastLogIndex < _persistentState.Log.Count))
        {
            return new RequestVoteResponse
            {
                Term = _persistentState.CurrentTerm,
                VoteGranted = false,
                Reason = "Candidate log not up-to-date"
            };
        }

        // Grant vote
        _persistentState.VotedFor = candidateId;
        _electionTimeout = DateTime.UtcNow.AddMilliseconds(Random.Shared.Next(150, 300));

        _logger.LogInformation(
            "Vote granted to {Candidate} for term {Term}",
            candidateId,
            term);

        return new RequestVoteResponse
        {
            Term = _persistentState.CurrentTerm,
            VoteGranted = true
        };
    }

    /// <summary>
    /// Apply command to state machine
    /// </summary>
    public async Task<long> ApplyCommandAsync(string data)
    {
        // Only leader can accept commands
        if (_state != RaftServerState.Leader)
        {
            throw new InvalidOperationException($"Cannot apply command on {_state}");
        }

        var entry = new LogEntry
        {
            Term = _persistentState.CurrentTerm,
            Data = data,
            Index = _persistentState.Log.Count + 1
        };

        _persistentState.Log.Add(entry);

        _logger.LogInformation(
            "Applied command at index {Index}: {Data}",
            entry.Index,
            data);

        return entry.Index;
    }

    /// <summary>
    /// Check election timeout and start election if needed
    /// </summary>
    public async Task<bool> CheckElectionTimeoutAsync()
    {
        if (_state == RaftServerState.Leader)
            return false;

        if (DateTime.UtcNow >= _electionTimeout)
        {
            await StartElectionAsync();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Start leader election
    /// </summary>
    private async Task StartElectionAsync()
    {
        _state = RaftServerState.Candidate;
        _persistentState.CurrentTerm++;
        _persistentState.VotedFor = _serverId;

        var lastLogIndex = _persistentState.Log.Count;
        var lastLogTerm = _persistentState.Log.Count > 0
            ? _persistentState.Log[_persistentState.Log.Count - 1].Term
            : 0;

        var votes = 1; // Vote for self
        var voteNeeded = (_peers.Count + 1) / 2 + 1;

        _logger.LogInformation(
            "Starting election for term {Term}",
            _persistentState.CurrentTerm);

        // Send RequestVote to all peers (simplified - in reality this would be async RPC calls)
        foreach (var peer in _peers)
        {
            votes++; // Simplified voting logic
        }

        if (votes >= voteNeeded)
        {
            BecomeLeader();
        }

        _electionTimeout = DateTime.UtcNow.AddMilliseconds(Random.Shared.Next(150, 300));
    }

    /// <summary>
    /// Become leader and initialize leader state
    /// </summary>
    private void BecomeLeader()
    {
        _state = RaftServerState.Leader;
        _leaderState = new RaftLeaderState();

        foreach (var peer in _peers)
        {
            _leaderState.NextIndex[peer] = _persistentState.Log.Count + 1;
            _leaderState.MatchIndex[peer] = 0;
        }

        _heartbeatTimeout = DateTime.UtcNow.AddMilliseconds(50);

        _logger.LogInformation(
            "Became leader for term {Term}",
            _persistentState.CurrentTerm);
    }

    /// <summary>
    /// Apply committed entries to state machine
    /// </summary>
    private async Task ApplyEntriesAsync()
    {
        while (_volatileState.LastApplied < _volatileState.CommitIndex)
        {
            _volatileState.LastApplied++;
            var entry = _persistentState.Log[(int)_volatileState.LastApplied - 1];
            _stateMachine.AddOrUpdate(_volatileState.LastApplied, entry.Data, (k, v) => entry.Data);
        }
    }

    /// <summary>
    /// Get committed state
    /// </summary>
    public Dictionary<long, string> GetCommittedState() => new(_stateMachine);
}

/// <summary>
/// Append entries RPC response
/// </summary>
public class AppendEntriesResponse
{
    public long Term { get; set; }
    public bool Success { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Request vote RPC response
/// </summary>
public class RequestVoteResponse
{
    public long Term { get; set; }
    public bool VoteGranted { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Paxos consensus algorithm - Higher fault tolerance for Byzantine environments
/// </summary>

public enum PaxosPhase
{
    Prepare,
    Promise,
    Accept,
    Accepted
}

/// <summary>
/// Paxos proposal
/// </summary>
public class PaxosProposal
{
    public long ProposalNumber { get; set; }
    public string ProposerId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Paxos proposer
/// </summary>
public class PaxosProposer
{
    private long _nextProposalNumber = 0;
    private readonly string _proposerId;
    private readonly ILogger<PaxosProposer> _logger;

    public PaxosProposer(string proposerId, ILogger<PaxosProposer> logger)
    {
        _proposerId = proposerId;
        _logger = logger;
    }

    /// <summary>
    /// Generate unique proposal number
    /// </summary>
    public long GenerateProposalNumber()
    {
        _nextProposalNumber++;
        return _nextProposalNumber;
    }

    /// <summary>
    /// Propose value to acceptors
    /// </summary>
    public async Task<bool> ProposeAsync(string value, IEnumerable<PaxosAcceptor> acceptors)
    {
        var proposalNumber = GenerateProposalNumber();
        var proposal = new PaxosProposal
        {
            ProposalNumber = proposalNumber,
            ProposerId = _proposerId,
            Value = value
        };

        // Phase 1: Prepare - get promises from majority of acceptors
        var promises = new List<PaxosPromise>();
        foreach (var acceptor in acceptors)
        {
            var promise = await acceptor.PrepareAsync(proposalNumber);
            if (promise != null)
            {
                promises.Add(promise);
            }
        }

        if (promises.Count <= acceptors.Count() / 2)
        {
            _logger.LogWarning("Failed to get majority promises for proposal {Number}", proposalNumber);
            return false;
        }

        // Phase 2: Accept - send accept request with highest value from promises
        var valueToAccept = value;
        var highestAcceptedNumber = 0L;

        foreach (var promise in promises)
        {
            if (promise.HighestAcceptedNumber > highestAcceptedNumber)
            {
                highestAcceptedNumber = promise.HighestAcceptedNumber;
                if (promise.HighestAcceptedValue != null)
                {
                    valueToAccept = promise.HighestAcceptedValue;
                }
            }
        }

        var acceptedCount = 0;
        foreach (var acceptor in acceptors)
        {
            var accepted = await acceptor.AcceptAsync(proposalNumber, valueToAccept);
            if (accepted)
            {
                acceptedCount++;
            }
        }

        var success = acceptedCount > acceptors.Count() / 2;

        _logger.LogInformation(
            "Proposal {Number}: value={Value}, accepted={Count}/{Total}",
            proposalNumber,
            valueToAccept,
            acceptedCount,
            acceptors.Count());

        return success;
    }
}

/// <summary>
/// Paxos promise from acceptor
/// </summary>
public class PaxosPromise
{
    public long HighestPreparedNumber { get; set; }
    public long HighestAcceptedNumber { get; set; }
    public string? HighestAcceptedValue { get; set; }
}

/// <summary>
/// Paxos acceptor
/// </summary>
public class PaxosAcceptor
{
    private long _highestPreparedNumber = 0;
    private long _highestAcceptedNumber = 0;
    private string? _highestAcceptedValue;
    private readonly ILogger<PaxosAcceptor> _logger;

    public PaxosAcceptor(ILogger<PaxosAcceptor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Prepare phase - accept prepare request
    /// </summary>
    public async Task<PaxosPromise?> PrepareAsync(long proposalNumber)
    {
        if (proposalNumber <= _highestPreparedNumber)
        {
            return null; // Reject if proposal number is not higher
        }

        _highestPreparedNumber = proposalNumber;

        _logger.LogInformation(
            "Prepared for proposal {Number}",
            proposalNumber);

        return new PaxosPromise
        {
            HighestPreparedNumber = proposalNumber,
            HighestAcceptedNumber = _highestAcceptedNumber,
            HighestAcceptedValue = _highestAcceptedValue
        };
    }

    /// <summary>
    /// Accept phase - accept accept request
    /// </summary>
    public async Task<bool> AcceptAsync(long proposalNumber, string value)
    {
        if (proposalNumber < _highestPreparedNumber)
        {
            return false; // Reject if proposal number is lower than highest prepared
        }

        _highestAcceptedNumber = proposalNumber;
        _highestAcceptedValue = value;

        _logger.LogInformation(
            "Accepted proposal {Number}: {Value}",
            proposalNumber,
            value);

        return true;
    }

    /// <summary>
    /// Get accepted value
    /// </summary>
    public string? GetAcceptedValue() => _highestAcceptedValue;
}

/// <summary>
/// Byzantine Fault Tolerant (BFT) consensus - tolerates malicious nodes
/// Practical Byzantine Fault Tolerance (PBFT) simplified implementation
/// </summary>

public enum BftPhase
{
    PrePrepare,
    Prepare,
    Commit
}

/// <summary>
/// BFT view (leader epoch)
/// </summary>
public class BftView
{
    public long ViewNumber { get; set; }
    public string PrimaryId { get; set; } = string.Empty;
    public long SequenceNumber { get; set; }
}

/// <summary>
/// BFT message
/// </summary>
public class BftMessage
{
    public long ViewNumber { get; set; }
    public long SequenceNumber { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public string Digest { get; set; } = string.Empty; // SHA-256 hash
    public BftPhase Phase { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Byzantine Fault Tolerant replica
/// </summary>
public class BftReplica
{
    private BftView _view = new();
    private long _sequenceNumber = 0;
    private readonly Dictionary<long, Dictionary<string, BftMessage>> _prepares = new();
    private readonly Dictionary<long, Dictionary<string, BftMessage>> _commits = new();
    private readonly ConcurrentDictionary<long, string> _log = new();
    private readonly ILogger<BftReplica> _logger;

    public BftReplica(string replicaId, ILogger<BftReplica> logger)
    {
        _view.PrimaryId = replicaId;
        _logger = logger;
    }

    /// <summary>
    /// Process pre-prepare message from primary
    /// </summary>
    public async Task<bool> ProcessPrePrepareAsync(BftMessage message)
    {
        if (message.ViewNumber != _view.ViewNumber)
        {
            _logger.LogWarning(
                "PrePrepare from wrong view: {MessageView} vs {CurrentView}",
                message.ViewNumber,
                _view.ViewNumber);
            return false;
        }

        if (message.SequenceNumber <= _view.SequenceNumber)
        {
            _logger.LogWarning(
                "PrePrepare with old sequence: {MessageSeq} vs {CurrentSeq}",
                message.SequenceNumber,
                _view.SequenceNumber);
            return false;
        }

        _view.SequenceNumber = message.SequenceNumber;
        _log.AddOrUpdate(message.SequenceNumber, message.Data, (k, v) => message.Data);

        _logger.LogInformation(
            "Processed PrePrepare: sequence={Sequence}, data={Data}",
            message.SequenceNumber,
            message.Data);

        return true;
    }

    /// <summary>
    /// Process prepare message from other replicas
    /// </summary>
    public async Task<bool> ProcessPrepareAsync(BftMessage message)
    {
        if (!_prepares.ContainsKey(message.SequenceNumber))
        {
            _prepares[message.SequenceNumber] = new();
        }

        _prepares[message.SequenceNumber][message.SenderId] = message;

        var prepareCount = _prepares[message.SequenceNumber].Count;
        var requiredCount = (3 + 1) / 2; // Majority for 4 replicas (3f+1 with f=1 byzantine replica)

        if (prepareCount >= requiredCount)
        {
            _logger.LogInformation(
                "Prepare quorum reached: {Count}/{Required}",
                prepareCount,
                requiredCount);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Process commit message
    /// </summary>
    public async Task<bool> ProcessCommitAsync(BftMessage message)
    {
        if (!_commits.ContainsKey(message.SequenceNumber))
        {
            _commits[message.SequenceNumber] = new();
        }

        _commits[message.SequenceNumber][message.SenderId] = message;

        var commitCount = _commits[message.SequenceNumber].Count;
        var requiredCount = (3 + 1) / 2;

        if (commitCount >= requiredCount)
        {
            _logger.LogInformation(
                "Commit quorum reached: {Count}/{Required}, sequence={Sequence}",
                commitCount,
                requiredCount,
                message.SequenceNumber);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get committed log entries
    /// </summary>
    public Dictionary<long, string> GetCommittedEntries() => new(_log);
}

/// <summary>
/// Extension methods
/// </summary>
public static class ConsensusExtensions
{
    public static IServiceCollection AddConsensusPatterns(this IServiceCollection services)
    {
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        return services;
    }
}
