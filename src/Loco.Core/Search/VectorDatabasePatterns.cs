#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Search;

/// <summary>
/// Vector Database & Semantic Search Patterns
/// Embeddings, similarity search, RAG (Retrieval-Augmented Generation)
/// </summary>

/// <summary>
/// Text embedding
/// </summary>
public class TextEmbedding
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("vector")]
    public double[] Vector { get; set; } = Array.Empty<double>();

    [JsonPropertyName("embedding_model")]
    public string EmbeddingModel { get; set; } = "sentence-transformers/all-mpnet-base-v2";

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Vector search query
/// </summary>
public class VectorSearchQuery
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("queryVector")]
    public double[] QueryVector { get; set; } = Array.Empty<double>();

    [JsonPropertyName("topK")]
    public int TopK { get; set; } = 10;

    [JsonPropertyName("minSimilarity")]
    public double MinSimilarity { get; set; } = 0.5;

    [JsonPropertyName("filters")]
    public Dictionary<string, object> Filters { get; set; } = new();

    [JsonPropertyName("useHybridSearch")]
    public bool UseHybridSearch { get; set; } = true;

    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = new(); // For BM25 keyword search
}

/// <summary>
/// Search result
/// </summary>
public class SearchResult
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("similarity")]
    public double Similarity { get; set; }

    [JsonPropertyName("bm25Score")]
    public double? Bm25Score { get; set; } // Keyword relevance

    [JsonPropertyName("hybridScore")]
    public double HybridScore { get; set; } // Combined score

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    [JsonPropertyName("rank")]
    public int Rank { get; set; }
}

/// <summary>
/// Vector database for semantic search
/// In-memory implementation; production: Qdrant, Pinecone, Weaviate
/// </summary>
public class VectorDatabase
{
    private readonly ConcurrentDictionary<string, TextEmbedding> _vectors = new();
    private readonly ConcurrentDictionary<string, List<string>> _metadata_index = new();
    private readonly int _vectorDimension = 768; // Standard for many models
    private readonly ILogger<VectorDatabase> _logger;

    public VectorDatabase(ILogger<VectorDatabase> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Index text embedding
    /// </summary>
    public async Task<string> IndexAsync(TextEmbedding embedding)
    {
        if (embedding.Vector.Length != _vectorDimension)
        {
            throw new ArgumentException($"Vector dimension mismatch. Expected {_vectorDimension}, got {embedding.Vector.Length}");
        }

        _vectors[embedding.Id] = embedding;

        // Index metadata for filtering
        foreach (var kvp in embedding.Metadata)
        {
            var metadataKey = $"metadata:{kvp.Key}:{kvp.Value}";
            if (!_metadata_index.ContainsKey(metadataKey))
            {
                _metadata_index[metadataKey] = new();
            }

            _metadata_index[metadataKey].Add(embedding.Id);
        }

        _logger.LogInformation(
            "Indexed embedding {Id}: {Text}",
            embedding.Id,
            embedding.Text[..Math.Min(50, embedding.Text.Length)]);

        return embedding.Id;
    }

    /// <summary>
    /// Search for similar documents
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(VectorSearchQuery query)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<SearchResult>();

        if (query.QueryVector.Length != _vectorDimension)
        {
            _logger.LogWarning("Query vector dimension mismatch");
            return results;
        }

        // Vector similarity search
        var vectorResults = PerformVectorSearch(query);

        // Optional: Hybrid search combining vector + keyword search
        if (query.UseHybridSearch && query.Keywords.Any())
        {
            var keywordResults = PerformKeywordSearch(query);
            vectorResults = CombineResults(vectorResults, keywordResults);
        }

        // Apply filters if specified
        var filtered = ApplyFilters(vectorResults, query.Filters);

        // Rank by hybrid score
        results = filtered
            .OrderByDescending(r => r.HybridScore)
            .Take(query.TopK)
            .Select((r, idx) => { r.Rank = idx + 1; return r; })
            .ToList();

        stopwatch.Stop();

        _logger.LogInformation(
            "Vector search completed: {Count} results in {Time}ms, query={Query}",
            results.Count,
            stopwatch.ElapsedMilliseconds,
            query.Query[..Math.Min(50, query.Query.Length)]);

        return results;
    }

    /// <summary>
    /// Perform vector similarity search (cosine similarity)
    /// </summary>
    private List<SearchResult> PerformVectorSearch(VectorSearchQuery query)
    {
        var results = new List<SearchResult>();

        foreach (var embedding in _vectors.Values)
        {
            var similarity = CosineSimilarity(query.QueryVector, embedding.Vector);

            if (similarity >= query.MinSimilarity)
            {
                results.Add(new SearchResult
                {
                    DocumentId = embedding.Id,
                    Text = embedding.Text,
                    Similarity = similarity,
                    HybridScore = similarity,
                    Metadata = new(embedding.Metadata)
                });
            }
        }

        return results.OrderByDescending(r => r.Similarity).ToList();
    }

    /// <summary>
    /// Perform keyword search (BM25 algorithm simplified)
    /// </summary>
    private List<SearchResult> PerformKeywordSearch(VectorSearchQuery query)
    {
        var results = new List<SearchResult>();

        foreach (var embedding in _vectors.Values)
        {
            var score = CalculateBm25Score(query.Keywords, embedding.Text);

            if (score > 0)
            {
                results.Add(new SearchResult
                {
                    DocumentId = embedding.Id,
                    Text = embedding.Text,
                    Bm25Score = score,
                    HybridScore = score,
                    Metadata = new(embedding.Metadata)
                });
            }
        }

        return results.OrderByDescending(r => r.Bm25Score ?? 0).ToList();
    }

    /// <summary>
    /// Combine vector and keyword results with weighted averaging
    /// </summary>
    private List<SearchResult> CombineResults(
        List<SearchResult> vectorResults,
        List<SearchResult> keywordResults)
    {
        var combined = new Dictionary<string, SearchResult>();

        // Add vector results with weight 0.7
        foreach (var result in vectorResults)
        {
            combined[result.DocumentId] = result;
            result.HybridScore = result.Similarity * 0.7;
        }

        // Add/merge keyword results with weight 0.3
        foreach (var result in keywordResults)
        {
            if (combined.TryGetValue(result.DocumentId, out var existing))
            {
                // Combine scores
                existing.HybridScore = (existing.HybridScore / 0.7) * 0.7 + (result.Bm25Score ?? 0) * 0.3;
            }
            else
            {
                result.HybridScore = (result.Bm25Score ?? 0) * 0.3;
                combined[result.DocumentId] = result;
            }
        }

        return combined.Values.ToList();
    }

    /// <summary>
    /// Apply metadata filters
    /// </summary>
    private List<SearchResult> ApplyFilters(
        List<SearchResult> results,
        Dictionary<string, object> filters)
    {
        if (!filters.Any())
        {
            return results;
        }

        var filtered = new List<SearchResult>();

        foreach (var result in results)
        {
            var matches = true;

            foreach (var filter in filters)
            {
                if (!result.Metadata.TryGetValue(filter.Key, out var value))
                {
                    matches = false;
                    break;
                }

                if (!value.Equals(filter.Value))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                filtered.Add(result);
            }
        }

        return filtered;
    }

    /// <summary>
    /// Calculate cosine similarity between two vectors
    /// </summary>
    private double CosineSimilarity(double[] vectorA, double[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
            return 0;

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        magnitudeA = Math.Sqrt(magnitudeA);
        magnitudeB = Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    /// <summary>
    /// Calculate BM25 relevance score (simplified)
    /// </summary>
    private double CalculateBm25Score(List<string> keywords, string text)
    {
        var score = 0.0;
        var textLower = text.ToLower();

        foreach (var keyword in keywords)
        {
            var keywordLower = keyword.ToLower();
            var count = 0;
            var index = 0;

            while ((index = textLower.IndexOf(keywordLower, index)) != -1)
            {
                count++;
                index += keywordLower.Length;
            }

            // BM25 formula (simplified)
            if (count > 0)
            {
                score += Math.Log(1 + count);
            }
        }

        return score;
    }

    /// <summary>
    /// Get database statistics
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        return new()
        {
            ["totalDocuments"] = _vectors.Count,
            ["vectorDimension"] = _vectorDimension,
            ["memoryUsageMb"] = (_vectors.Count * _vectorDimension * 8) / (1024 * 1024), // Approximate
            ["indexedMetadataKeys"] = _metadata_index.Count
        };
    }
}

/// <summary>
/// RAG (Retrieval-Augmented Generation) system
/// Retrieves relevant context for LLM to generate better responses
/// </summary>
public class RagSystem
{
    private readonly VectorDatabase _vectorDatabase;
    private readonly ILogger<RagSystem> _logger;

    public RagSystem(VectorDatabase vectorDatabase, ILogger<RagSystem> logger)
    {
        _vectorDatabase = vectorDatabase;
        _logger = logger;
    }

    /// <summary>
    /// Retrieve context for query
    /// </summary>
    public async Task<RagContext> RetrieveContextAsync(
        string query,
        double[] queryVector,
        int topK = 5)
    {
        var stopwatch = Stopwatch.StartNew();

        var searchQuery = new VectorSearchQuery
        {
            Query = query,
            QueryVector = queryVector,
            TopK = topK
        };

        var searchResults = await _vectorDatabase.SearchAsync(searchQuery);

        stopwatch.Stop();

        var context = new RagContext
        {
            Query = query,
            RetrievedDocuments = searchResults
                .Select(r => new RetrievedDocument
                {
                    DocumentId = r.DocumentId,
                    Content = r.Text,
                    Relevance = r.Similarity,
                    Metadata = r.Metadata
                })
                .ToList(),
            RetrievalTimeMs = stopwatch.ElapsedMilliseconds
        };

        _logger.LogInformation(
            "RAG context retrieved: {DocumentCount} documents in {Time}ms",
            context.RetrievedDocuments.Count,
            stopwatch.ElapsedMilliseconds);

        return context;
    }
}

/// <summary>
/// RAG context
/// </summary>
public class RagContext
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("retrievedDocuments")]
    public List<RetrievedDocument> RetrievedDocuments { get; set; } = new();

    [JsonPropertyName("combinedContext")]
    public string CombinedContext => string.Join("\n\n",
        RetrievedDocuments.Select(d => d.Content));

    [JsonPropertyName("retrievalTimeMs")]
    public long RetrievalTimeMs { get; set; }
}

/// <summary>
/// Retrieved document for RAG
/// </summary>
public class RetrievedDocument
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("relevance")]
    public double Relevance { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Embedding service - generates embeddings
/// </summary>
public class EmbeddingService
{
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(ILogger<EmbeddingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate embedding for text
    /// In production: use OpenAI, Azure OpenAI, or local models
    /// </summary>
    public async Task<double[]> GenerateEmbeddingAsync(string text)
    {
        // Simplified: return pseudo-random vector
        // Real implementation would use embedding model
        var hash = text.GetHashCode();
        var random = new Random(hash);

        var vector = new double[768];
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] = (random.NextDouble() * 2) - 1; // Range [-1, 1]
        }

        // Normalize
        var magnitude = Math.Sqrt(vector.Sum(v => v * v));
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] /= magnitude;
        }

        _logger.LogDebug(
            "Generated embedding for text: {Text}",
            text[..Math.Min(50, text.Length)]);

        return vector;
    }

    /// <summary>
    /// Batch generate embeddings
    /// </summary>
    public async Task<List<double[]>> GenerateEmbeddingsAsync(List<string> texts)
    {
        var embeddings = new List<double[]>();

        foreach (var text in texts)
        {
            var embedding = await GenerateEmbeddingAsync(text);
            embeddings.Add(embedding);
        }

        return embeddings;
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class VectorSearchExtensions
{
    public static IServiceCollection AddVectorDatabase(this IServiceCollection services)
    {
        services.AddSingleton<VectorDatabase>();
        services.AddSingleton<EmbeddingService>();
        services.AddSingleton<RagSystem>();
        return services;
    }
}
