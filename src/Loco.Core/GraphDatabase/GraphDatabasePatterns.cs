#nullable enable

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.GraphDatabase;

/// <summary>
/// Graph Database Patterns
/// Neo4j, knowledge graphs, semantic queries, AI integration
/// </summary>

public class GraphNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty; // Person, Company, Product

    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class GraphEdge
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("sourceId")]
    public string SourceId { get; set; } = string.Empty;

    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = string.Empty;

    [JsonPropertyName("relationship")]
    public string Relationship { get; set; } = string.Empty; // KNOWS, WORKS_FOR, OWNS

    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; set; } = new();

    [JsonPropertyName("weight")]
    public double Weight { get; set; } = 1.0;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class KnowledgeGraphQuery
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("cypher")]
    public string CypherQuery { get; set; } = string.Empty;

    [JsonPropertyName("startNode")]
    public string StartNode { get; set; } = string.Empty;

    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = string.Empty; // e.g., "(person)-[:KNOWS]-(friend)"

    [JsonPropertyName("filters")]
    public Dictionary<string, object> Filters { get; set; } = new();

    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 100;

    [JsonPropertyName("executionTimeMs")]
    public double ExecutionTimeMs { get; set; }

    [JsonPropertyName("resultCount")]
    public int ResultCount { get; set; }
}

public class GraphAlgorithmResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("algorithmName")]
    public string AlgorithmName { get; set; } = string.Empty; // PageRank, Betweenness, Louvain

    [JsonPropertyName("startNode")]
    public string StartNode { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public Dictionary<string, object> Result { get; set; } = new();

    [JsonPropertyName("executionTimeMs")]
    public double ExecutionTimeMs { get; set; }

    [JsonPropertyName("nodesProcessed")]
    public long NodesProcessed { get; set; }

    [JsonPropertyName("edgesProcessed")]
    public long EdgesProcessed { get; set; }
}

public class SemanticQuery
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("naturalLanguage")]
    public string NaturalLanguage { get; set; } = string.Empty;

    [JsonPropertyName("translatedCypher")]
    public string TranslatedCypher { get; set; } = string.Empty;

    [JsonPropertyName("semanticMeaning")]
    public string SemanticMeaning { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 0.95;

    [JsonPropertyName("results")]
    public List<Dictionary<string, object>> Results { get; set; } = new();
}

public class AIIntegration
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("graphEmbeddingModel")]
    public string EmbeddingModel { get; set; } = string.Empty; // Word2Vec, Graph2Vec, Node2Vec

    [JsonPropertyName("nodeEmbeddings")]
    public Dictionary<string, List<double>> NodeEmbeddings { get; set; } = new();

    [JsonPropertyName("similarityThreshold")]
    public double SimilarityThreshold { get; set; } = 0.8;

    [JsonPropertyName("predictedLinks")]
    public List<(string source, string target, double score)> PredictedLinks { get; set; } = new();

    [JsonPropertyName("anomaliesDetected")]
    public List<string> AnomaliesDetected { get; set; } = new();
}

public class GraphStatistics
{
    [JsonPropertyName("totalNodes")]
    public long TotalNodes { get; set; }

    [JsonPropertyName("totalEdges")]
    public long TotalEdges { get; set; }

    [JsonPropertyName("averageNodeDegree")]
    public double AverageNodeDegree { get; set; }

    [JsonPropertyName("diameterSteps")]
    public int DiameterSteps { get; set; }

    [JsonPropertyName("averagePathLength")]
    public double AveragePathLength { get; set; }

    [JsonPropertyName("clusteringCoefficient")]
    public double ClusteringCoefficient { get; set; }

    [JsonPropertyName("queryTime50P")]
    public double QueryTime50PMs { get; set; }

    [JsonPropertyName("queryTime99P")]
    public double QueryTime99PMs { get; set; }

    [JsonPropertyName("queriesPerSecond")]
    public double QueriesPerSecond { get; set; }
}

/// <summary>
/// Graph Database Engine (Neo4j-like)
/// </summary>
public class GraphDatabaseEngine
{
    private readonly ConcurrentDictionary<string, GraphNode> _nodes = new();
    private readonly ConcurrentDictionary<string, GraphEdge> _edges = new();
    private readonly List<KnowledgeGraphQuery> _queryLog = new();
    private readonly GraphStatistics _stats = new();
    private readonly ILogger<GraphDatabaseEngine> _logger;

    public GraphDatabaseEngine(ILogger<GraphDatabaseEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Create node in graph
    /// </summary>
    public async Task<GraphNode> CreateNodeAsync(
        string label,
        Dictionary<string, object> properties)
    {
        var node = new GraphNode
        {
            Label = label,
            Properties = properties
        };

        _nodes[node.Id] = node;
        _stats.TotalNodes++;

        _logger.LogInformation(
            "Created node: {Label} with {Props} properties",
            label,
            properties.Count);

        return node;
    }

    /// <summary>
    /// Create edge (relationship) between nodes
    /// </summary>
    public async Task<GraphEdge> CreateEdgeAsync(
        string sourceId,
        string targetId,
        string relationship,
        Dictionary<string, object>? properties = null)
    {
        if (!_nodes.ContainsKey(sourceId) || !_nodes.ContainsKey(targetId))
            throw new InvalidOperationException("Source or target node not found");

        var edge = new GraphEdge
        {
            SourceId = sourceId,
            TargetId = targetId,
            Relationship = relationship,
            Properties = properties ?? new()
        };

        _edges[edge.Id] = edge;
        _stats.TotalEdges++;

        _logger.LogInformation(
            "Created edge: {Source} -[{Relationship}]-> {Target}",
            sourceId[..8],
            relationship,
            targetId[..8]);

        return edge;
    }

    /// <summary>
    /// Execute Cypher query
    /// </summary>
    public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(
        string cypherQuery,
        Dictionary<string, object>? parameters = null)
    {
        var startTime = DateTime.UtcNow;
        var results = new List<Dictionary<string, object>>();

        // Simulate query execution
        if (cypherQuery.Contains("MATCH"))
        {
            results = _nodes.Values.Take(10).Select(n =>
                new Dictionary<string, object> { ["node"] = n.Label, ["id"] = n.Id }
            ).ToList();
        }

        var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

        var query = new KnowledgeGraphQuery
        {
            CypherQuery = cypherQuery,
            ExecutionTimeMs = executionTime,
            ResultCount = results.Count
        };

        _queryLog.Add(query);

        _logger.LogInformation(
            "Executed query: {Results} results in {Time}ms",
            results.Count,
            executionTime);

        return results;
    }

    /// <summary>
    /// Find shortest path between nodes
    /// </summary>
    public async Task<List<string>> FindShortestPathAsync(
        string sourceId,
        string targetId,
        string? relationshipFilter = null)
    {
        if (!_nodes.ContainsKey(sourceId) || !_nodes.ContainsKey(targetId))
            return new();

        var path = BFS(sourceId, targetId, relationshipFilter);

        _logger.LogInformation(
            "Found shortest path: {Distance} hops",
            path.Count);

        return path;
    }

    /// <summary>
    /// Get node neighbors
    /// </summary>
    public async Task<List<GraphNode>> GetNeighborsAsync(
        string nodeId,
        int depth = 1)
    {
        if (!_nodes.ContainsKey(nodeId))
            return new();

        var neighbors = new List<GraphNode>();
        var visited = new HashSet<string> { nodeId };
        var queue = new Queue<(string id, int d)> { (nodeId, 0) };

        while (queue.Count > 0)
        {
            var (currentId, currentDepth) = queue.Dequeue();
            if (currentDepth >= depth)
                continue;

            var connectedEdges = _edges.Values.Where(e =>
                e.SourceId == currentId || e.TargetId == currentId);

            foreach (var edge in connectedEdges)
            {
                var neighborId = edge.SourceId == currentId ? edge.TargetId : edge.SourceId;
                if (!visited.Contains(neighborId))
                {
                    visited.Add(neighborId);
                    if (_nodes.TryGetValue(neighborId, out var neighbor))
                        neighbors.Add(neighbor);
                    queue.Enqueue((neighborId, currentDepth + 1));
                }
            }
        }

        return neighbors;
    }

    /// <summary>
    /// Run PageRank algorithm
    /// </summary>
    public async Task<GraphAlgorithmResult> RunPageRankAsync(int iterations = 10)
    {
        var startTime = DateTime.UtcNow;
        var pageRanks = new Dictionary<string, double>();

        foreach (var nodeId in _nodes.Keys)
            pageRanks[nodeId] = 1.0 / _nodes.Count;

        for (int i = 0; i < iterations; i++)
        {
            var newRanks = new Dictionary<string, double>();
            foreach (var nodeId in _nodes.Keys)
                newRanks[nodeId] = 0.15 / _nodes.Count;

            foreach (var edge in _edges.Values)
            {
                var sourceOutDegree = _edges.Values.Count(e => e.SourceId == edge.SourceId);
                if (sourceOutDegree > 0)
                    newRanks[edge.TargetId] += 0.85 * pageRanks[edge.SourceId] / sourceOutDegree;
            }
            pageRanks = newRanks;
        }

        var result = new GraphAlgorithmResult
        {
            AlgorithmName = "PageRank",
            ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
            NodesProcessed = _nodes.Count,
            EdgesProcessed = _edges.Count,
            Result = pageRanks.ToDictionary(k => k.Key, v => (object)v.Value)
        };

        return result;
    }

    /// <summary>
    /// Get graph statistics
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        var recentQueries = _queryLog.TakeLast(100).ToList();
        var queryTimes = recentQueries.Select(q => q.ExecutionTimeMs).ToList();

        _stats.TotalNodes = _nodes.Count;
        _stats.TotalEdges = _edges.Count;
        _stats.AverageNodeDegree = _nodes.Count > 0
            ? (double)_edges.Count * 2 / _nodes.Count
            : 0;
        _stats.QueryTime50PMs = queryTimes.Count > 0
            ? queryTimes.OrderBy(x => x).Skip((int)(queryTimes.Count * 0.5)).First()
            : 0;
        _stats.QueryTime99PMs = queryTimes.Count > 0
            ? queryTimes.OrderBy(x => x).Skip((int)(queryTimes.Count * 0.99)).FirstOrDefault()
            : 0;

        return new()
        {
            ["totalNodes"] = _stats.TotalNodes,
            ["totalEdges"] = _stats.TotalEdges,
            ["averageNodeDegree"] = Math.Round(_stats.AverageNodeDegree, 2),
            ["totalQueries"] = _queryLog.Count,
            ["query50thPercentileMs"] = Math.Round(_stats.QueryTime50PMs, 2),
            ["query99thPercentileMs"] = Math.Round(_stats.QueryTime99PMs, 2),
            ["averageQueryTimeMs"] = Math.Round(queryTimes.Average(), 2)
        };
    }

    private List<string> BFS(string source, string target, string? relationshipFilter)
    {
        var path = new List<string> { source };
        var visited = new HashSet<string> { source };
        var queue = new Queue<string> { source };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == target)
                return path;

            var neighbors = _edges.Values
                .Where(e => (e.SourceId == current || e.TargetId == current) &&
                           (relationshipFilter == null || e.Relationship == relationshipFilter))
                .Select(e => e.SourceId == current ? e.TargetId : e.SourceId)
                .Where(n => !visited.Contains(n));

            foreach (var neighbor in neighbors)
            {
                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return new();
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class GraphDatabaseExtensions
{
    public static IServiceCollection AddGraphDatabase(this IServiceCollection services)
    {
        services.AddSingleton<GraphDatabaseEngine>();
        return services;
    }
}
