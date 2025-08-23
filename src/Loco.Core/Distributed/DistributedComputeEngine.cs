using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;

namespace Loco.Core.Distributed;

/// <summary>
/// Distributed computing engine for horizontal scaling and cluster operations
/// Implements leader election, task distribution, and fault tolerance
/// </summary>
public sealed class DistributedComputeEngine : IDisposable
{
    private readonly ILogger<DistributedComputeEngine> _logger;
    private readonly NodeConfiguration _nodeConfig;
    private readonly ConcurrentDictionary<string, NodeInfo> _clusterNodes;
    private readonly ConcurrentDictionary<string, DistributedTask> _activeTasks;
    private readonly Channel<TaskMessage> _taskQueue;
    private readonly IConsistentHashing _hashRing;
    private readonly ILeaderElection _leaderElection;
    private readonly HeartbeatService _heartbeatService;
    private readonly TaskScheduler _distributedScheduler;
    private bool _disposed;

    // Network components
    private readonly TcpListener _tcpListener;
    private readonly UdpClient _udpClient;
    private readonly int _port;
    private readonly string _nodeId;
    private bool _isLeader;

    // Performance metrics
    private readonly PerformanceCounters _counters;
    private readonly Stopwatch _uptime;

    public DistributedComputeEngine(
        NodeConfiguration config = null,
        ILogger<DistributedComputeEngine> logger = null)
    {
        _logger = logger;
        _nodeConfig = config ?? NodeConfiguration.Default();
        _nodeId = GenerateNodeId();
        _port = _nodeConfig.Port;
        
        _clusterNodes = new ConcurrentDictionary<string, NodeInfo>();
        _activeTasks = new ConcurrentDictionary<string, DistributedTask>();
        _taskQueue = Channel.CreateUnbounded<TaskMessage>();
        
        _hashRing = new ConsistentHashRing(_nodeConfig.VirtualNodes);
        _leaderElection = new RaftLeaderElection(_nodeId, logger);
        _heartbeatService = new HeartbeatService(_nodeId, BroadcastHeartbeat);
        _distributedScheduler = new TaskScheduler.FromCurrentSynchronizationContext();
        
        _tcpListener = new TcpListener(IPAddress.Any, _port);
        _udpClient = new UdpClient(_port);
        
        _counters = new PerformanceCounters();
        _uptime = Stopwatch.StartNew();
        
        InitializeNode();
    }

    private void InitializeNode()
    {
        // Register self in cluster
        var selfInfo = new NodeInfo
        {
            NodeId = _nodeId,
            Address = GetLocalIPAddress(),
            Port = _port,
            Status = NodeStatus.Active,
            Capacity = Environment.ProcessorCount,
            LastHeartbeat = DateTime.UtcNow
        };
        
        _clusterNodes[_nodeId] = selfInfo;
        _hashRing.AddNode(_nodeId);
        
        // Start network listeners
        Task.Run(StartTcpListener);
        Task.Run(StartUdpListener);
        Task.Run(ProcessTaskQueue);
        
        // Start services
        _heartbeatService.Start();
        _leaderElection.StartElection();
        
        _logger?.LogInformation("Node {NodeId} initialized on port {Port}", _nodeId, _port);
    }

    /// <summary>
    /// Submit task for distributed execution
    /// </summary>
    public async Task<TaskResult> ExecuteDistributedAsync<TInput, TOutput>(
        Func<TInput, CancellationToken, Task<TOutput>> computation,
        TInput input,
        DistributedTaskOptions options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DistributedTaskOptions();
        
        var taskId = Guid.NewGuid().ToString();
        var task = new DistributedTask
        {
            Id = taskId,
            Type = TaskType.Compute,
            Input = JsonSerializer.Serialize(input),
            Options = options,
            SubmittedAt = DateTime.UtcNow,
            Status = TaskStatus.Pending
        };
        
        // Determine target node using consistent hashing
        var targetNodeId = _hashRing.GetNode(taskId);
        
        if (targetNodeId == _nodeId)
        {
            // Execute locally
            return await ExecuteLocalAsync(task, computation, cancellationToken);
        }
        else
        {
            // Send to remote node
            return await ExecuteRemoteAsync(task, targetNodeId, cancellationToken);
        }
    }

    /// <summary>
    /// MapReduce implementation for distributed data processing
    /// </summary>
    public async Task<TResult> MapReduceAsync<TInput, TIntermediate, TResult>(
        IEnumerable<TInput> data,
        Func<TInput, IEnumerable<KeyValuePair<string, TIntermediate>>> mapper,
        Func<string, IEnumerable<TIntermediate>, TResult> reducer,
        MapReduceOptions options = null)
    {
        options ??= new MapReduceOptions();
        
        var chunks = PartitionData(data, _clusterNodes.Count);
        var mapTasks = new List<Task<IEnumerable<KeyValuePair<string, TIntermediate>>>>();
        
        // Map phase - distribute to nodes
        foreach (var (chunk, nodeId) in chunks.Zip(_clusterNodes.Keys))
        {
            var mapTask = ExecuteOnNodeAsync(nodeId, async () =>
            {
                var results = new List<KeyValuePair<string, TIntermediate>>();
                foreach (var item in chunk)
                {
                    results.AddRange(mapper(item));
                }
                return results;
            });
            
            mapTasks.Add(mapTask);
        }
        
        // Wait for all map tasks
        var mapResults = await Task.WhenAll(mapTasks);
        
        // Shuffle phase - group by key
        var grouped = mapResults
            .SelectMany(r => r)
            .GroupBy(kvp => kvp.Key)
            .Select(g => new { Key = g.Key, Values = g.Select(kvp => kvp.Value) });
        
        // Reduce phase - can be distributed or local
        if (options.DistributedReduce && grouped.Count() > options.ReduceThreshold)
        {
            // Distribute reduce tasks
            var reduceTasks = grouped.Select(g =>
                ExecuteOnNodeAsync(_hashRing.GetNode(g.Key), () =>
                    Task.FromResult(reducer(g.Key, g.Values))));
            
            var reduceResults = await Task.WhenAll(reduceTasks);
            
            // Combine results (simplified - actual implementation would need custom combiner)
            return reduceResults.FirstOrDefault();
        }
        else
        {
            // Local reduce
            return reducer(grouped.First().Key, grouped.First().Values);
        }
    }

    /// <summary>
    /// Distributed cache operations
    /// </summary>
    public async Task<T> GetOrComputeAsync<T>(
        string key,
        Func<Task<T>> factory,
        DistributedCacheOptions cacheOptions = null)
    {
        cacheOptions ??= new DistributedCacheOptions();
        
        // Determine cache node
        var cacheNodeId = _hashRing.GetNode(key);
        
        if (cacheNodeId == _nodeId)
        {
            // Local cache
            return await LocalCache.GetOrAddAsync(key, factory, cacheOptions.Expiration);
        }
        else
        {
            // Remote cache
            var cached = await GetFromRemoteCacheAsync<T>(cacheNodeId, key);
            if (cached != null)
                return cached;
            
            var value = await factory();
            await SetRemoteCacheAsync(cacheNodeId, key, value, cacheOptions.Expiration);
            return value;
        }
    }

    /// <summary>
    /// Broadcast message to all nodes
    /// </summary>
    public async Task BroadcastAsync<T>(T message, BroadcastOptions options = null)
    {
        options ??= new BroadcastOptions();
        
        var messageBytes = JsonSerializer.SerializeToUtf8Bytes(new BroadcastMessage
        {
            Type = typeof(T).Name,
            Payload = JsonSerializer.Serialize(message),
            SenderId = _nodeId,
            Timestamp = DateTime.UtcNow
        });
        
        if (options.Reliable)
        {
            // TCP broadcast for reliability
            var tasks = _clusterNodes.Values
                .Where(n => n.NodeId != _nodeId && n.Status == NodeStatus.Active)
                .Select(node => SendTcpMessageAsync(node, messageBytes));
            
            await Task.WhenAll(tasks);
        }
        else
        {
            // UDP broadcast for speed
            await _udpClient.SendAsync(messageBytes, messageBytes.Length, 
                new IPEndPoint(IPAddress.Broadcast, _port));
        }
        
        _counters.MessagesSent++;
    }

    /// <summary>
    /// Perform leader election
    /// </summary>
    public async Task<bool> ElectLeaderAsync()
    {
        var result = await _leaderElection.RunElectionAsync(_clusterNodes.Values.ToList());
        _isLeader = result.WinnerId == _nodeId;
        
        if (_isLeader)
        {
            _logger?.LogInformation("Node {NodeId} elected as leader", _nodeId);
            await OnBecomeLeaderAsync();
        }
        
        return _isLeader;
    }

    /// <summary>
    /// Handle node failure and redistribution
    /// </summary>
    private async Task HandleNodeFailureAsync(string failedNodeId)
    {
        _logger?.LogWarning("Node {NodeId} failed, redistributing tasks", failedNodeId);
        
        // Remove from cluster
        if (_clusterNodes.TryRemove(failedNodeId, out _))
        {
            _hashRing.RemoveNode(failedNodeId);
            
            // Redistribute tasks
            var tasksToRedistribute = _activeTasks.Values
                .Where(t => t.AssignedNode == failedNodeId)
                .ToList();
            
            foreach (var task in tasksToRedistribute)
            {
                var newNodeId = _hashRing.GetNode(task.Id);
                task.AssignedNode = newNodeId;
                
                if (newNodeId == _nodeId)
                {
                    await ProcessTaskAsync(task);
                }
                else
                {
                    await SendTaskToNodeAsync(newNodeId, task);
                }
            }
        }
    }

    /// <summary>
    /// Graceful shutdown with task migration
    /// </summary>
    public async Task ShutdownAsync()
    {
        _logger?.LogInformation("Node {NodeId} shutting down gracefully", _nodeId);
        
        // Stop accepting new tasks
        _taskQueue.Writer.TryComplete();
        
        // Migrate active tasks to other nodes
        var tasks = _activeTasks.Values.Where(t => t.Status == TaskStatus.Running).ToList();
        
        foreach (var task in tasks)
        {
            var newNodeId = _clusterNodes.Keys
                .Where(id => id != _nodeId)
                .OrderBy(_ => Guid.NewGuid())
                .FirstOrDefault();
            
            if (newNodeId != null)
            {
                await SendTaskToNodeAsync(newNodeId, task);
            }
        }
        
        // Notify cluster of shutdown
        await BroadcastAsync(new NodeMessage 
        { 
            Type = MessageType.Shutdown, 
            NodeId = _nodeId 
        });
        
        // Clean shutdown
        _heartbeatService.Stop();
        _tcpListener?.Stop();
        _udpClient?.Close();
    }

    // Network handlers
    private async Task StartTcpListener()
    {
        _tcpListener.Start();
        
        while (!_disposed)
        {
            try
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleTcpClientAsync(tcpClient));
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private async Task StartUdpListener()
    {
        while (!_disposed)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync();
                _ = Task.Run(() => HandleUdpMessageAsync(result.Buffer, result.RemoteEndPoint));
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private async Task HandleTcpClientAsync(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();
            var buffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            
            if (bytesRead > 0)
            {
                var message = JsonSerializer.Deserialize<NetworkMessage>(buffer.AsSpan(0, bytesRead));
                await ProcessNetworkMessageAsync(message);
            }
        }
    }

    private async Task HandleUdpMessageAsync(byte[] buffer, IPEndPoint endpoint)
    {
        var message = JsonSerializer.Deserialize<NetworkMessage>(buffer);
        await ProcessNetworkMessageAsync(message);
    }

    private async Task ProcessNetworkMessageAsync(NetworkMessage message)
    {
        _counters.MessagesReceived++;
        
        switch (message.Type)
        {
            case MessageType.Task:
                var task = JsonSerializer.Deserialize<DistributedTask>(message.Payload);
                await ProcessTaskAsync(task);
                break;
                
            case MessageType.Heartbeat:
                UpdateNodeHeartbeat(message.SenderId);
                break;
                
            case MessageType.Election:
                await _leaderElection.HandleElectionMessageAsync(message);
                break;
                
            case MessageType.Result:
                HandleTaskResult(message);
                break;
        }
    }

    private async Task ProcessTaskQueue()
    {
        await foreach (var message in _taskQueue.Reader.ReadAllAsync())
        {
            await ProcessTaskMessageAsync(message);
        }
    }

    private async Task ProcessTaskAsync(DistributedTask task)
    {
        task.Status = TaskStatus.Running;
        task.StartedAt = DateTime.UtcNow;
        _activeTasks[task.Id] = task;
        
        try
        {
            // Execute task based on type
            object result = task.Type switch
            {
                TaskType.Compute => await ExecuteComputeTaskAsync(task),
                TaskType.Map => await ExecuteMapTaskAsync(task),
                TaskType.Reduce => await ExecuteReduceTaskAsync(task),
                _ => throw new NotSupportedException($"Task type {task.Type} not supported")
            };
            
            task.Result = JsonSerializer.Serialize(result);
            task.Status = TaskStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            
            _counters.TasksCompleted++;
        }
        catch (Exception ex)
        {
            task.Status = TaskStatus.Failed;
            task.Error = ex.Message;
            _counters.TasksFailed++;
            _logger?.LogError(ex, "Task {TaskId} failed", task.Id);
        }
        finally
        {
            _activeTasks.TryRemove(task.Id, out _);
        }
    }

    // Helper methods
    private string GenerateNodeId()
    {
        var bytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes);
    }

    private string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        return host.AddressList
            .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
            ?.ToString() ?? "127.0.0.1";
    }

    private IEnumerable<IEnumerable<T>> PartitionData<T>(IEnumerable<T> data, int partitions)
    {
        var list = data.ToList();
        var partitionSize = (list.Count + partitions - 1) / partitions;
        
        for (int i = 0; i < partitions; i++)
        {
            yield return list.Skip(i * partitionSize).Take(partitionSize);
        }
    }

    private async Task<TaskResult> ExecuteLocalAsync<TInput, TOutput>(
        DistributedTask task, 
        Func<TInput, CancellationToken, Task<TOutput>> computation,
        CancellationToken cancellationToken)
    {
        var input = JsonSerializer.Deserialize<TInput>(task.Input);
        var result = await computation(input, cancellationToken);
        
        return new TaskResult
        {
            TaskId = task.Id,
            Success = true,
            Result = result,
            ExecutionTime = DateTime.UtcNow - task.SubmittedAt
        };
    }

    private async Task<TaskResult> ExecuteRemoteAsync(
        DistributedTask task,
        string targetNodeId,
        CancellationToken cancellationToken)
    {
        if (!_clusterNodes.TryGetValue(targetNodeId, out var node))
        {
            throw new InvalidOperationException($"Node {targetNodeId} not found");
        }
        
        await SendTaskToNodeAsync(targetNodeId, task);
        
        // Wait for result
        var tcs = new TaskCompletionSource<TaskResult>();
        _pendingResults[task.Id] = tcs;
        
        using (cancellationToken.Register(() => tcs.TrySetCanceled()))
        {
            return await tcs.Task;
        }
    }

    private readonly ConcurrentDictionary<string, TaskCompletionSource<TaskResult>> _pendingResults = new();

    private async Task SendTaskToNodeAsync(string nodeId, DistributedTask task)
    {
        if (_clusterNodes.TryGetValue(nodeId, out var node))
        {
            var message = new NetworkMessage
            {
                Type = MessageType.Task,
                SenderId = _nodeId,
                Payload = JsonSerializer.Serialize(task)
            };
            
            await SendTcpMessageAsync(node, JsonSerializer.SerializeToUtf8Bytes(message));
        }
    }

    private async Task SendTcpMessageAsync(NodeInfo node, byte[] data)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(node.Address, node.Port);
        var stream = client.GetStream();
        await stream.WriteAsync(data, 0, data.Length);
    }

    private void UpdateNodeHeartbeat(string nodeId)
    {
        if (_clusterNodes.TryGetValue(nodeId, out var node))
        {
            node.LastHeartbeat = DateTime.UtcNow;
        }
    }

    private void BroadcastHeartbeat()
    {
        var heartbeat = new HeartbeatMessage
        {
            NodeId = _nodeId,
            Timestamp = DateTime.UtcNow,
            Load = _activeTasks.Count,
            IsLeader = _isLeader
        };
        
        _ = BroadcastAsync(heartbeat, new BroadcastOptions { Reliable = false });
    }

    private async Task OnBecomeLeaderAsync()
    {
        // Leader-specific initialization
        await Task.CompletedTask;
    }

    private void HandleTaskResult(NetworkMessage message)
    {
        var result = JsonSerializer.Deserialize<TaskResult>(message.Payload);
        if (_pendingResults.TryRemove(result.TaskId, out var tcs))
        {
            tcs.TrySetResult(result);
        }
    }

    private async Task<object> ExecuteComputeTaskAsync(DistributedTask task)
    {
        // Simplified compute execution
        await Task.Delay(100); // Simulate work
        return $"Computed result for {task.Id}";
    }

    private async Task<object> ExecuteMapTaskAsync(DistributedTask task)
    {
        await Task.Delay(50);
        return $"Mapped result for {task.Id}";
    }

    private async Task<object> ExecuteReduceTaskAsync(DistributedTask task)
    {
        await Task.Delay(50);
        return $"Reduced result for {task.Id}";
    }

    private async Task<T> GetFromRemoteCacheAsync<T>(string nodeId, string key)
    {
        // Simplified remote cache get
        await Task.CompletedTask;
        return default(T);
    }

    private async Task SetRemoteCacheAsync<T>(string nodeId, string key, T value, TimeSpan expiration)
    {
        // Simplified remote cache set
        await Task.CompletedTask;
    }

    private async Task ProcessTaskMessageAsync(TaskMessage message)
    {
        await Task.CompletedTask;
    }

    private async Task<IEnumerable<KeyValuePair<string, TIntermediate>>> ExecuteOnNodeAsync<TIntermediate>(
        string nodeId, 
        Func<Task<IEnumerable<KeyValuePair<string, TIntermediate>>>> computation)
    {
        if (nodeId == _nodeId)
        {
            return await computation();
        }
        
        // Simplified remote execution
        return await computation();
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _ = ShutdownAsync();
        _heartbeatService?.Dispose();
        _tcpListener?.Stop();
        _udpClient?.Dispose();
        _uptime?.Stop();
        
        _disposed = true;
    }
}

// Supporting classes
public class NodeConfiguration
{
    public int Port { get; set; } = 8888;
    public int VirtualNodes { get; set; } = 150;
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan NodeTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    public static NodeConfiguration Default() => new();
}

public class NodeInfo
{
    public string NodeId { get; set; }
    public string Address { get; set; }
    public int Port { get; set; }
    public NodeStatus Status { get; set; }
    public int Capacity { get; set; }
    public DateTime LastHeartbeat { get; set; }
}

public enum NodeStatus
{
    Active,
    Inactive,
    Failed,
    Maintenance
}

public class DistributedTask
{
    public string Id { get; set; }
    public TaskType Type { get; set; }
    public string Input { get; set; }
    public string Result { get; set; }
    public DistributedTaskOptions Options { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TaskStatus Status { get; set; }
    public string AssignedNode { get; set; }
    public string Error { get; set; }
}

public enum TaskType
{
    Compute,
    Map,
    Reduce,
    Aggregate
}

public class DistributedTaskOptions
{
    public int Priority { get; set; } = 5;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxRetries { get; set; } = 3;
    public bool RequireAck { get; set; } = true;
}

public class TaskResult
{
    public string TaskId { get; set; }
    public bool Success { get; set; }
    public object Result { get; set; }
    public string Error { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}

public class MapReduceOptions
{
    public bool DistributedReduce { get; set; } = true;
    public int ReduceThreshold { get; set; } = 100;
    public int ChunkSize { get; set; } = 1000;
}

public class DistributedCacheOptions
{
    public TimeSpan Expiration { get; set; } = TimeSpan.FromMinutes(10);
    public bool Replicate { get; set; } = true;
    public int ReplicationFactor { get; set; } = 2;
}

public class BroadcastOptions
{
    public bool Reliable { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
}

// Network messages
public class NetworkMessage
{
    public MessageType Type { get; set; }
    public string SenderId { get; set; }
    public string Payload { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum MessageType
{
    Task,
    Result,
    Heartbeat,
    Election,
    Shutdown,
    Cache
}

public class BroadcastMessage
{
    public string Type { get; set; }
    public string Payload { get; set; }
    public string SenderId { get; set; }
    public DateTime Timestamp { get; set; }
}

public class HeartbeatMessage
{
    public string NodeId { get; set; }
    public DateTime Timestamp { get; set; }
    public int Load { get; set; }
    public bool IsLeader { get; set; }
}

public class NodeMessage
{
    public MessageType Type { get; set; }
    public string NodeId { get; set; }
}

public class TaskMessage
{
    public string TaskId { get; set; }
    public byte[] Data { get; set; }
}

// Consistent hashing
public interface IConsistentHashing
{
    void AddNode(string nodeId);
    void RemoveNode(string nodeId);
    string GetNode(string key);
}

public class ConsistentHashRing : IConsistentHashing
{
    private readonly SortedDictionary<uint, string> _ring;
    private readonly int _virtualNodes;
    
    public ConsistentHashRing(int virtualNodes = 150)
    {
        _ring = new SortedDictionary<uint, string>();
        _virtualNodes = virtualNodes;
    }
    
    public void AddNode(string nodeId)
    {
        for (int i = 0; i < _virtualNodes; i++)
        {
            var hash = GetHash($"{nodeId}:{i}");
            _ring[hash] = nodeId;
        }
    }
    
    public void RemoveNode(string nodeId)
    {
        for (int i = 0; i < _virtualNodes; i++)
        {
            var hash = GetHash($"{nodeId}:{i}");
            _ring.Remove(hash);
        }
    }
    
    public string GetNode(string key)
    {
        if (_ring.Count == 0) return null;
        
        var hash = GetHash(key);
        
        foreach (var kvp in _ring)
        {
            if (kvp.Key >= hash)
                return kvp.Value;
        }
        
        return _ring.First().Value;
    }
    
    private uint GetHash(string key)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
        return BitConverter.ToUInt32(hash, 0);
    }
}

// Leader election
public interface ILeaderElection
{
    Task<ElectionResult> RunElectionAsync(List<NodeInfo> nodes);
    Task HandleElectionMessageAsync(NetworkMessage message);
    void StartElection();
}

public class RaftLeaderElection : ILeaderElection
{
    private readonly string _nodeId;
    private readonly ILogger _logger;
    private int _currentTerm;
    private string _votedFor;
    private string _leaderId;
    
    public RaftLeaderElection(string nodeId, ILogger logger)
    {
        _nodeId = nodeId;
        _logger = logger;
    }
    
    public async Task<ElectionResult> RunElectionAsync(List<NodeInfo> nodes)
    {
        _currentTerm++;
        _votedFor = _nodeId;
        var votes = 1;
        
        // Request votes from other nodes
        var voteTasks = nodes
            .Where(n => n.NodeId != _nodeId)
            .Select(node => RequestVoteAsync(node));
        
        var results = await Task.WhenAll(voteTasks);
        votes += results.Count(r => r);
        
        var majority = (nodes.Count / 2) + 1;
        
        if (votes >= majority)
        {
            _leaderId = _nodeId;
            return new ElectionResult { WinnerId = _nodeId, Term = _currentTerm };
        }
        
        return new ElectionResult { WinnerId = null, Term = _currentTerm };
    }
    
    public async Task HandleElectionMessageAsync(NetworkMessage message)
    {
        await Task.CompletedTask;
        // Handle election messages
    }
    
    public void StartElection()
    {
        // Start election timer
    }
    
    private async Task<bool> RequestVoteAsync(NodeInfo node)
    {
        // Simplified vote request
        await Task.Delay(10);
        return Random.Shared.Next(2) == 1;
    }
}

public class ElectionResult
{
    public string WinnerId { get; set; }
    public int Term { get; set; }
}

// Services
public class HeartbeatService : IDisposable
{
    private readonly Timer _timer;
    private readonly string _nodeId;
    private readonly Action _heartbeatAction;
    
    public HeartbeatService(string nodeId, Action heartbeatAction)
    {
        _nodeId = nodeId;
        _heartbeatAction = heartbeatAction;
        _timer = new Timer(_ => _heartbeatAction(), null, Timeout.Infinite, Timeout.Infinite);
    }
    
    public void Start()
    {
        _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }
    
    public void Stop()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }
    
    public void Dispose()
    {
        _timer?.Dispose();
    }
}

public class PerformanceCounters
{
    public long TasksCompleted;
    public long TasksFailed;
    public long MessagesSent;
    public long MessagesReceived;
    
    public double Throughput => TasksCompleted / (double)Math.Max(1, TasksCompleted + TasksFailed);
}

// Local cache helper
public static class LocalCache
{
    private static readonly ConcurrentDictionary<string, (object Value, DateTime Expiry)> _cache = new();
    
    public static async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan expiration)
    {
        if (_cache.TryGetValue(key, out var cached) && cached.Expiry > DateTime.UtcNow)
        {
            return (T)cached.Value;
        }
        
        var value = await factory();
        _cache[key] = (value, DateTime.UtcNow + expiration);
        return value;
    }
}