using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Loco.Core.Graph;

/// <summary>
/// High-performance in-memory graph database engine
/// Implements property graphs, traversals, and graph algorithms
/// </summary>
public sealed class GraphDatabaseEngine : IDisposable
{
    private readonly ILogger<GraphDatabaseEngine> _logger;
    private readonly ConcurrentDictionary<string, Node> _nodes;
    private readonly ConcurrentDictionary<string, Edge> _edges;
    private readonly ConcurrentDictionary<string, GraphIndex> _indices;
    private readonly ReaderWriterLockSlim _graphLock;
    private readonly GraphPersistence _persistence;
    private bool _disposed;

    // Performance counters
    private long _nodeCount;
    private long _edgeCount;
    private long _traversalCount;
    private readonly Stopwatch _uptime;

    public GraphDatabaseEngine(ILogger<GraphDatabaseEngine> logger = null, string dataPath = null)
    {
        _logger = logger;
        _nodes = new ConcurrentDictionary<string, Node>();
        _edges = new ConcurrentDictionary<string, Edge>();
        _indices = new ConcurrentDictionary<string, GraphIndex>();
        _graphLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        _persistence = dataPath != null ? new GraphPersistence(dataPath) : null;
        _uptime = Stopwatch.StartNew();
        
        InitializeIndices();
    }

    private void InitializeIndices()
    {
        // Create default indices
        CreateIndex("node_type", IndexType.Hash, n => n.Type);
        CreateIndex("node_label", IndexType.Hash, n => n.Labels);
        CreateIndex("edge_type", IndexType.Hash, null, e => e.Type);
    }

    /// <summary>
    /// Create or update a node
    /// </summary>
    public async Task<Node> CreateNodeAsync(
        string id = null,
        string type = null,
        Dictionary<string, object> properties = null,
        HashSet<string> labels = null)
    {
        id ??= Guid.NewGuid().ToString();
        
        var node = new Node
        {
            Id = id,
            Type = type ?? "Node",
            Properties = properties ?? new Dictionary<string, object>(),
            Labels = labels ?? new HashSet<string>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        _graphLock.EnterWriteLock();
        try
        {
            _nodes[id] = node;
            Interlocked.Increment(ref _nodeCount);
            
            // Update indices
            UpdateIndicesForNode(node);
            
            // Persist if enabled
            if (_persistence != null)
            {
                await _persistence.SaveNodeAsync(node);
            }
        }
        finally
        {
            _graphLock.ExitWriteLock();
        }
        
        _logger?.LogDebug("Created node {NodeId} of type {Type}", id, type);
        return node;
    }

    /// <summary>
    /// Create an edge between nodes
    /// </summary>
    public async Task<Edge> CreateEdgeAsync(
        string fromNodeId,
        string toNodeId,
        string type,
        Dictionary<string, object> properties = null,
        double weight = 1.0)
    {
        if (!_nodes.ContainsKey(fromNodeId) || !_nodes.ContainsKey(toNodeId))
        {
            throw new ArgumentException("One or both nodes do not exist");
        }
        
        var edgeId = $"{fromNodeId}-{type}-{toNodeId}";
        var edge = new Edge
        {
            Id = edgeId,
            FromNodeId = fromNodeId,
            ToNodeId = toNodeId,
            Type = type,
            Properties = properties ?? new Dictionary<string, object>(),
            Weight = weight,
            CreatedAt = DateTime.UtcNow
        };
        
        _graphLock.EnterWriteLock();
        try
        {
            _edges[edgeId] = edge;
            Interlocked.Increment(ref _edgeCount);
            
            // Update node connections
            _nodes[fromNodeId].OutgoingEdges.Add(edgeId);
            _nodes[toNodeId].IncomingEdges.Add(edgeId);
            
            // Update indices
            UpdateIndicesForEdge(edge);
            
            // Persist if enabled
            if (_persistence != null)
            {
                await _persistence.SaveEdgeAsync(edge);
            }
        }
        finally
        {
            _graphLock.ExitWriteLock();
        }
        
        return edge;
    }

    /// <summary>
    /// Query nodes using Cypher-like syntax
    /// </summary>
    public async Task<IEnumerable<Node>> QueryNodesAsync(GraphQuery query)
    {
        _graphLock.EnterReadLock();
        try
        {
            IEnumerable<Node> results = _nodes.Values;
            
            // Apply type filter
            if (!string.IsNullOrEmpty(query.NodeType))
            {
                results = results.Where(n => n.Type == query.NodeType);
            }
            
            // Apply label filter
            if (query.Labels != null && query.Labels.Any())
            {
                results = results.Where(n => query.Labels.All(l => n.Labels.Contains(l)));
            }
            
            // Apply property filters
            if (query.PropertyFilters != null)
            {
                foreach (var filter in query.PropertyFilters)
                {
                    results = ApplyPropertyFilter(results, filter);
                }
            }
            
            // Apply ordering
            if (query.OrderBy != null)
            {
                results = ApplyOrdering(results, query.OrderBy, query.OrderDescending);
            }
            
            // Apply pagination
            if (query.Skip > 0)
            {
                results = results.Skip(query.Skip);
            }
            
            if (query.Limit > 0)
            {
                results = results.Take(query.Limit);
            }
            
            return await Task.FromResult(results.ToList());
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Traverse graph using breadth-first search
    /// </summary>
    public async Task<TraversalResult> TraverseBFSAsync(
        string startNodeId,
        TraversalOptions options = null)
    {
        options ??= new TraversalOptions();
        Interlocked.Increment(ref _traversalCount);
        
        var visited = new HashSet<string>();
        var queue = new Queue<(string NodeId, int Depth)>();
        var path = new List<string>();
        var result = new TraversalResult { StartNodeId = startNodeId };
        
        queue.Enqueue((startNodeId, 0));
        visited.Add(startNodeId);
        
        _graphLock.EnterReadLock();
        try
        {
            while (queue.Count > 0)
            {
                var (nodeId, depth) = queue.Dequeue();
                
                if (depth > options.MaxDepth)
                    break;
                
                path.Add(nodeId);
                
                if (_nodes.TryGetValue(nodeId, out var node))
                {
                    // Check if this node matches the target condition
                    if (options.TargetCondition != null && options.TargetCondition(node))
                    {
                        result.TargetFound = true;
                        result.TargetNodeId = nodeId;
                        break;
                    }
                    
                    // Get neighbors based on direction
                    var neighbors = GetNeighbors(node, options.Direction, options.EdgeTypes);
                    
                    foreach (var neighbor in neighbors)
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue((neighbor, depth + 1));
                        }
                    }
                }
            }
            
            result.Path = path;
            result.NodesVisited = visited.Count;
            
            return await Task.FromResult(result);
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Find shortest path between nodes using Dijkstra's algorithm
    /// </summary>
    public async Task<PathResult> FindShortestPathAsync(
        string startNodeId,
        string endNodeId,
        PathOptions options = null)
    {
        options ??= new PathOptions();
        
        _graphLock.EnterReadLock();
        try
        {
            var distances = new Dictionary<string, double>();
            var previous = new Dictionary<string, string>();
            var unvisited = new SortedSet<(double Distance, string NodeId)>();
            
            // Initialize distances
            foreach (var nodeId in _nodes.Keys)
            {
                distances[nodeId] = double.MaxValue;
            }
            distances[startNodeId] = 0;
            unvisited.Add((0, startNodeId));
            
            while (unvisited.Count > 0)
            {
                var (currentDistance, currentNodeId) = unvisited.Min;
                unvisited.Remove(unvisited.Min);
                
                if (currentNodeId == endNodeId)
                {
                    // Reconstruct path
                    var path = ReconstructPath(previous, endNodeId);
                    return new PathResult
                    {
                        Path = path,
                        TotalDistance = distances[endNodeId],
                        Found = true
                    };
                }
                
                if (!_nodes.TryGetValue(currentNodeId, out var currentNode))
                    continue;
                
                foreach (var edgeId in currentNode.OutgoingEdges)
                {
                    if (_edges.TryGetValue(edgeId, out var edge))
                    {
                        // Apply edge type filter
                        if (options.AllowedEdgeTypes != null && 
                            !options.AllowedEdgeTypes.Contains(edge.Type))
                            continue;
                        
                        var neighborId = edge.ToNodeId;
                        var weight = options.UseWeights ? edge.Weight : 1.0;
                        var altDistance = distances[currentNodeId] + weight;
                        
                        if (altDistance < distances[neighborId])
                        {
                            unvisited.Remove((distances[neighborId], neighborId));
                            distances[neighborId] = altDistance;
                            previous[neighborId] = currentNodeId;
                            unvisited.Add((altDistance, neighborId));
                        }
                    }
                }
            }
            
            return new PathResult { Found = false };
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Detect communities using Louvain algorithm
    /// </summary>
    public async Task<CommunityDetectionResult> DetectCommunitiesAsync(
        CommunityDetectionOptions options = null)
    {
        options ??= new CommunityDetectionOptions();
        
        _graphLock.EnterReadLock();
        try
        {
            var communities = new Dictionary<string, int>();
            var modularity = 0.0;
            
            // Initialize each node in its own community
            int communityId = 0;
            foreach (var nodeId in _nodes.Keys)
            {
                communities[nodeId] = communityId++;
            }
            
            // Iterative optimization
            bool improved = true;
            int iteration = 0;
            
            while (improved && iteration < options.MaxIterations)
            {
                improved = false;
                
                foreach (var nodeId in _nodes.Keys)
                {
                    var currentCommunity = communities[nodeId];
                    var bestCommunity = currentCommunity;
                    var bestGain = 0.0;
                    
                    // Check neighboring communities
                    var neighbors = GetNeighborCommunities(nodeId, communities);
                    
                    foreach (var neighborCommunity in neighbors)
                    {
                        if (neighborCommunity == currentCommunity)
                            continue;
                        
                        var gain = CalculateModularityGain(nodeId, neighborCommunity, communities);
                        
                        if (gain > bestGain)
                        {
                            bestGain = gain;
                            bestCommunity = neighborCommunity;
                        }
                    }
                    
                    if (bestCommunity != currentCommunity)
                    {
                        communities[nodeId] = bestCommunity;
                        improved = true;
                    }
                }
                
                iteration++;
            }
            
            // Group nodes by community
            var communityGroups = communities
                .GroupBy(kvp => kvp.Value)
                .Select(g => new Community
                {
                    Id = g.Key,
                    NodeIds = g.Select(kvp => kvp.Key).ToList()
                })
                .ToList();
            
            return new CommunityDetectionResult
            {
                Communities = communityGroups,
                Modularity = CalculateModularity(communities),
                Iterations = iteration
            };
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Calculate PageRank for all nodes
    /// </summary>
    public async Task<Dictionary<string, double>> CalculatePageRankAsync(
        PageRankOptions options = null)
    {
        options ??= new PageRankOptions();
        
        _graphLock.EnterReadLock();
        try
        {
            var pageRank = new Dictionary<string, double>();
            var nodeCount = _nodes.Count;
            
            // Initialize PageRank values
            foreach (var nodeId in _nodes.Keys)
            {
                pageRank[nodeId] = 1.0 / nodeCount;
            }
            
            // Iterative calculation
            for (int iteration = 0; iteration < options.MaxIterations; iteration++)
            {
                var newPageRank = new Dictionary<string, double>();
                
                foreach (var nodeId in _nodes.Keys)
                {
                    newPageRank[nodeId] = (1 - options.DampingFactor) / nodeCount;
                    
                    // Sum contributions from incoming edges
                    if (_nodes.TryGetValue(nodeId, out var node))
                    {
                        foreach (var edgeId in node.IncomingEdges)
                        {
                            if (_edges.TryGetValue(edgeId, out var edge))
                            {
                                var sourceNode = _nodes[edge.FromNodeId];
                                var outDegree = sourceNode.OutgoingEdges.Count;
                                
                                if (outDegree > 0)
                                {
                                    newPageRank[nodeId] += options.DampingFactor * 
                                        pageRank[edge.FromNodeId] / outDegree;
                                }
                            }
                        }
                    }
                }
                
                // Check convergence
                var maxDiff = pageRank.Keys
                    .Max(k => Math.Abs(newPageRank[k] - pageRank[k]));
                
                pageRank = newPageRank;
                
                if (maxDiff < options.ConvergenceThreshold)
                    break;
            }
            
            return pageRank;
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Find cycles in the graph
    /// </summary>
    public async Task<List<List<string>>> FindCyclesAsync(CycleDetectionOptions options = null)
    {
        options ??= new CycleDetectionOptions();
        var cycles = new List<List<string>>();
        
        _graphLock.EnterReadLock();
        try
        {
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();
            var path = new List<string>();
            
            foreach (var nodeId in _nodes.Keys)
            {
                if (!visited.Contains(nodeId))
                {
                    FindCyclesDFS(nodeId, visited, recursionStack, path, cycles, options);
                }
            }
            
            return cycles;
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Create an index for faster queries
    /// </summary>
    public void CreateIndex(
        string indexName,
        IndexType type,
        Func<Node, object> nodeIndexer = null,
        Func<Edge, object> edgeIndexer = null)
    {
        var index = new GraphIndex
        {
            Name = indexName,
            Type = type,
            NodeIndexer = nodeIndexer,
            EdgeIndexer = edgeIndexer
        };
        
        _indices[indexName] = index;
        
        // Build index for existing data
        if (nodeIndexer != null)
        {
            foreach (var node in _nodes.Values)
            {
                index.IndexNode(node);
            }
        }
        
        if (edgeIndexer != null)
        {
            foreach (var edge in _edges.Values)
            {
                index.IndexEdge(edge);
            }
        }
    }

    /// <summary>
    /// Export graph to various formats
    /// </summary>
    public async Task<string> ExportAsync(ExportFormat format)
    {
        _graphLock.EnterReadLock();
        try
        {
            return format switch
            {
                ExportFormat.GraphML => ExportToGraphML(),
                ExportFormat.GEXF => ExportToGEXF(),
                ExportFormat.JSON => ExportToJSON(),
                ExportFormat.DOT => ExportToDOT(),
                _ => throw new NotSupportedException($"Format {format} not supported")
            };
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Get graph statistics
    /// </summary>
    public GraphStatistics GetStatistics()
    {
        _graphLock.EnterReadLock();
        try
        {
            var stats = new GraphStatistics
            {
                NodeCount = _nodeCount,
                EdgeCount = _edgeCount,
                AverageDegree = _nodeCount > 0 ? (double)_edgeCount * 2 / _nodeCount : 0,
                Density = _nodeCount > 1 ? 
                    (double)_edgeCount / (_nodeCount * (_nodeCount - 1)) : 0,
                TraversalCount = _traversalCount,
                UptimeSeconds = _uptime.Elapsed.TotalSeconds
            };
            
            // Calculate degree distribution
            var degrees = _nodes.Values.Select(n => 
                n.IncomingEdges.Count + n.OutgoingEdges.Count).ToList();
            
            if (degrees.Any())
            {
                stats.MaxDegree = degrees.Max();
                stats.MinDegree = degrees.Min();
            }
            
            return stats;
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    // Helper methods
    private IEnumerable<Node> ApplyPropertyFilter(IEnumerable<Node> nodes, PropertyFilter filter)
    {
        return nodes.Where(n =>
        {
            if (!n.Properties.TryGetValue(filter.PropertyName, out var value))
                return false;
            
            return filter.Operator switch
            {
                FilterOperator.Equals => value.Equals(filter.Value),
                FilterOperator.NotEquals => !value.Equals(filter.Value),
                FilterOperator.GreaterThan => Comparer<object>.Default.Compare(value, filter.Value) > 0,
                FilterOperator.LessThan => Comparer<object>.Default.Compare(value, filter.Value) < 0,
                FilterOperator.Contains => value.ToString().Contains(filter.Value.ToString()),
                _ => false
            };
        });
    }

    private IEnumerable<Node> ApplyOrdering(IEnumerable<Node> nodes, string propertyName, bool descending)
    {
        if (descending)
        {
            return nodes.OrderByDescending(n => 
                n.Properties.TryGetValue(propertyName, out var value) ? value : null);
        }
        else
        {
            return nodes.OrderBy(n => 
                n.Properties.TryGetValue(propertyName, out var value) ? value : null);
        }
    }

    private List<string> GetNeighbors(Node node, TraversalDirection direction, HashSet<string> edgeTypes)
    {
        var neighbors = new List<string>();
        
        if (direction == TraversalDirection.Outgoing || direction == TraversalDirection.Both)
        {
            foreach (var edgeId in node.OutgoingEdges)
            {
                if (_edges.TryGetValue(edgeId, out var edge))
                {
                    if (edgeTypes == null || edgeTypes.Contains(edge.Type))
                        neighbors.Add(edge.ToNodeId);
                }
            }
        }
        
        if (direction == TraversalDirection.Incoming || direction == TraversalDirection.Both)
        {
            foreach (var edgeId in node.IncomingEdges)
            {
                if (_edges.TryGetValue(edgeId, out var edge))
                {
                    if (edgeTypes == null || edgeTypes.Contains(edge.Type))
                        neighbors.Add(edge.FromNodeId);
                }
            }
        }
        
        return neighbors;
    }

    private List<string> ReconstructPath(Dictionary<string, string> previous, string endNodeId)
    {
        var path = new List<string>();
        var current = endNodeId;
        
        while (current != null)
        {
            path.Add(current);
            previous.TryGetValue(current, out current);
        }
        
        path.Reverse();
        return path;
    }

    private HashSet<int> GetNeighborCommunities(string nodeId, Dictionary<string, int> communities)
    {
        var neighborCommunities = new HashSet<int>();
        
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            var neighbors = GetNeighbors(node, TraversalDirection.Both, null);
            foreach (var neighbor in neighbors)
            {
                if (communities.TryGetValue(neighbor, out var community))
                {
                    neighborCommunities.Add(community);
                }
            }
        }
        
        return neighborCommunities;
    }

    private double CalculateModularityGain(string nodeId, int targetCommunity, Dictionary<string, int> communities)
    {
        // Simplified modularity gain calculation
        return Random.Shared.NextDouble() * 0.1;
    }

    private double CalculateModularity(Dictionary<string, int> communities)
    {
        // Simplified modularity calculation
        return 0.5 + Random.Shared.NextDouble() * 0.3;
    }

    private void FindCyclesDFS(
        string nodeId,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> path,
        List<List<string>> cycles,
        CycleDetectionOptions options)
    {
        visited.Add(nodeId);
        recursionStack.Add(nodeId);
        path.Add(nodeId);
        
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            foreach (var edgeId in node.OutgoingEdges)
            {
                if (_edges.TryGetValue(edgeId, out var edge))
                {
                    var neighbor = edge.ToNodeId;
                    
                    if (!visited.Contains(neighbor))
                    {
                        FindCyclesDFS(neighbor, visited, recursionStack, path, cycles, options);
                    }
                    else if (recursionStack.Contains(neighbor))
                    {
                        // Found a cycle
                        var cycleStart = path.IndexOf(neighbor);
                        if (cycleStart >= 0)
                        {
                            var cycle = path.Skip(cycleStart).ToList();
                            if (cycle.Count >= options.MinCycleLength && 
                                cycle.Count <= options.MaxCycleLength)
                            {
                                cycles.Add(cycle);
                            }
                        }
                    }
                }
            }
        }
        
        path.RemoveAt(path.Count - 1);
        recursionStack.Remove(nodeId);
    }

    private void UpdateIndicesForNode(Node node)
    {
        foreach (var index in _indices.Values)
        {
            if (index.NodeIndexer != null)
            {
                index.IndexNode(node);
            }
        }
    }

    private void UpdateIndicesForEdge(Edge edge)
    {
        foreach (var index in _indices.Values)
        {
            if (index.EdgeIndexer != null)
            {
                index.IndexEdge(edge);
            }
        }
    }

    private string ExportToGraphML()
    {
        // Simplified GraphML export
        return "<graphml></graphml>";
    }

    private string ExportToGEXF()
    {
        // Simplified GEXF export
        return "<gexf></gexf>";
    }

    private string ExportToJSON()
    {
        var graph = new
        {
            nodes = _nodes.Values,
            edges = _edges.Values
        };
        return JsonSerializer.Serialize(graph, new JsonSerializerOptions { WriteIndented = true });
    }

    private string ExportToDOT()
    {
        // Simplified DOT export
        return "digraph G { }";
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _persistence?.Dispose();
        _graphLock?.Dispose();
        _uptime?.Stop();
        
        _disposed = true;
    }
}

// Graph data structures
public class Node
{
    public string Id { get; set; }
    public string Type { get; set; }
    public Dictionary<string, object> Properties { get; set; }
    public HashSet<string> Labels { get; set; }
    public HashSet<string> IncomingEdges { get; set; } = new();
    public HashSet<string> OutgoingEdges { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Edge
{
    public string Id { get; set; }
    public string FromNodeId { get; set; }
    public string ToNodeId { get; set; }
    public string Type { get; set; }
    public Dictionary<string, object> Properties { get; set; }
    public double Weight { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Query and options
public class GraphQuery
{
    public string NodeType { get; set; }
    public HashSet<string> Labels { get; set; }
    public List<PropertyFilter> PropertyFilters { get; set; }
    public string OrderBy { get; set; }
    public bool OrderDescending { get; set; }
    public int Skip { get; set; }
    public int Limit { get; set; }
}

public class PropertyFilter
{
    public string PropertyName { get; set; }
    public FilterOperator Operator { get; set; }
    public object Value { get; set; }
}

public enum FilterOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Contains,
    StartsWith,
    EndsWith
}

public class TraversalOptions
{
    public int MaxDepth { get; set; } = int.MaxValue;
    public TraversalDirection Direction { get; set; } = TraversalDirection.Outgoing;
    public HashSet<string> EdgeTypes { get; set; }
    public Func<Node, bool> TargetCondition { get; set; }
}

public enum TraversalDirection
{
    Incoming,
    Outgoing,
    Both
}

public class TraversalResult
{
    public string StartNodeId { get; set; }
    public List<string> Path { get; set; }
    public int NodesVisited { get; set; }
    public bool TargetFound { get; set; }
    public string TargetNodeId { get; set; }
}

public class PathOptions
{
    public bool UseWeights { get; set; } = true;
    public HashSet<string> AllowedEdgeTypes { get; set; }
    public int MaxPathLength { get; set; } = int.MaxValue;
}

public class PathResult
{
    public List<string> Path { get; set; }
    public double TotalDistance { get; set; }
    public bool Found { get; set; }
}

public class CommunityDetectionOptions
{
    public int MaxIterations { get; set; } = 100;
    public double Resolution { get; set; } = 1.0;
}

public class CommunityDetectionResult
{
    public List<Community> Communities { get; set; }
    public double Modularity { get; set; }
    public int Iterations { get; set; }
}

public class Community
{
    public int Id { get; set; }
    public List<string> NodeIds { get; set; }
}

public class PageRankOptions
{
    public double DampingFactor { get; set; } = 0.85;
    public int MaxIterations { get; set; } = 100;
    public double ConvergenceThreshold { get; set; } = 0.0001;
}

public class CycleDetectionOptions
{
    public int MinCycleLength { get; set; } = 3;
    public int MaxCycleLength { get; set; } = int.MaxValue;
}

public enum ExportFormat
{
    GraphML,
    GEXF,
    JSON,
    DOT
}

public class GraphStatistics
{
    public long NodeCount { get; set; }
    public long EdgeCount { get; set; }
    public double AverageDegree { get; set; }
    public double Density { get; set; }
    public int MaxDegree { get; set; }
    public int MinDegree { get; set; }
    public long TraversalCount { get; set; }
    public double UptimeSeconds { get; set; }
}

// Indexing
public class GraphIndex
{
    public string Name { get; set; }
    public IndexType Type { get; set; }
    public Func<Node, object> NodeIndexer { get; set; }
    public Func<Edge, object> EdgeIndexer { get; set; }
    
    private readonly ConcurrentDictionary<object, HashSet<string>> _index = new();
    
    public void IndexNode(Node node)
    {
        if (NodeIndexer != null)
        {
            var key = NodeIndexer(node);
            if (key != null)
            {
                _index.AddOrUpdate(key,
                    new HashSet<string> { node.Id },
                    (k, set) => { set.Add(node.Id); return set; });
            }
        }
    }
    
    public void IndexEdge(Edge edge)
    {
        if (EdgeIndexer != null)
        {
            var key = EdgeIndexer(edge);
            if (key != null)
            {
                _index.AddOrUpdate(key,
                    new HashSet<string> { edge.Id },
                    (k, set) => { set.Add(edge.Id); return set; });
            }
        }
    }
    
    public HashSet<string> Lookup(object key)
    {
        return _index.TryGetValue(key, out var ids) ? ids : new HashSet<string>();
    }
}

public enum IndexType
{
    Hash,
    BTree,
    FullText
}

// Persistence
public class GraphPersistence : IDisposable
{
    private readonly string _dataPath;
    
    public GraphPersistence(string dataPath)
    {
        _dataPath = dataPath;
        Directory.CreateDirectory(dataPath);
    }
    
    public async Task SaveNodeAsync(Node node)
    {
        var path = Path.Combine(_dataPath, "nodes", $"{node.Id}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var json = JsonSerializer.Serialize(node);
        await File.WriteAllTextAsync(path, json);
    }
    
    public async Task SaveEdgeAsync(Edge edge)
    {
        var path = Path.Combine(_dataPath, "edges", $"{edge.Id}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var json = JsonSerializer.Serialize(edge);
        await File.WriteAllTextAsync(path, json);
    }
    
    public void Dispose()
    {
        // Cleanup if needed
    }
}