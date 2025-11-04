#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.MachineLearning;

/// <summary>
/// Machine Learning Model Serving Patterns
/// ONNX inference, model management, prediction serving
/// </summary>

/// <summary>
/// ML model metadata
/// </summary>
public class MLModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("framework")]
    public string Framework { get; set; } = "ONNX"; // ONNX, TensorFlow, PyTorch

    [JsonPropertyName("modelPath")]
    public string ModelPath { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public Dictionary<string, string> InputSchema { get; set; } = new(); // name -> type

    [JsonPropertyName("outputSchema")]
    public Dictionary<string, string> OutputSchema { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Inference request
/// </summary>
public class InferenceRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = string.Empty;

    [JsonPropertyName("inputs")]
    public Dictionary<string, object> Inputs { get; set; } = new();

    [JsonPropertyName("requestedAt")]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Inference response
/// </summary>
public class InferenceResponse
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("outputs")]
    public Dictionary<string, object> Outputs { get; set; } = new();

    [JsonPropertyName("confidenceScores")]
    public Dictionary<string, double>? ConfidenceScores { get; set; }

    [JsonPropertyName("executionTimeMs")]
    public long ExecutionTimeMs { get; set; }

    [JsonPropertyName("modelVersion")]
    public string? ModelVersion { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Model prediction metrics
/// </summary>
public class PredictionMetrics
{
    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = string.Empty;

    [JsonPropertyName("totalInferences")]
    public long TotalInferences { get; set; }

    [JsonPropertyName("averageLatencyMs")]
    public double AverageLatencyMs { get; set; }

    [JsonPropertyName("p99LatencyMs")]
    public long P99LatencyMs { get; set; }

    [JsonPropertyName("throughputQps")]
    public double ThroughputQps { get; set; }

    [JsonPropertyName("errorRate")]
    public double ErrorRate { get; set; }

    [JsonPropertyName("gpuUtilization")]
    public double? GpuUtilization { get; set; }

    [JsonPropertyName("memoryUsageMb")]
    public long MemoryUsageMb { get; set; }
}

/// <summary>
/// Model session pool - manages thread-safe ONNX session instances
/// Critical because ONNX Runtime sessions are not thread-safe
/// </summary>
public class ModelSessionPool : IAsyncDisposable
{
    private readonly MLModel _model;
    private readonly ConcurrentBag<ModelSession> _availableSessions;
    private readonly int _poolSize;
    private readonly ILogger<ModelSessionPool> _logger;

    public ModelSessionPool(
        MLModel model,
        int poolSize,
        ILogger<ModelSessionPool> logger)
    {
        _model = model;
        _poolSize = poolSize;
        _logger = logger;
        _availableSessions = new();

        // Pre-create sessions
        for (int i = 0; i < poolSize; i++)
        {
            _availableSessions.Add(new ModelSession { SessionId = Guid.NewGuid().ToString() });
        }

        _logger.LogInformation(
            "Created session pool for model {Model} with {PoolSize} sessions",
            model.Name,
            poolSize);
    }

    /// <summary>
    /// Acquire session from pool
    /// </summary>
    public async Task<ModelSession> AcquireSessionAsync(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        var deadline = DateTime.UtcNow.Add(timeout.Value);

        while (DateTime.UtcNow < deadline)
        {
            if (_availableSessions.TryTake(out var session))
            {
                session.AcquiredAt = DateTime.UtcNow;
                session.IsInUse = true;

                _logger.LogDebug(
                    "Acquired session {SessionId} from pool",
                    session.SessionId);

                return session;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("No sessions available in pool");
    }

    /// <summary>
    /// Release session back to pool
    /// </summary>
    public async Task ReleaseSessionAsync(ModelSession session)
    {
        if (session == null)
            return;

        session.IsInUse = false;
        session.ReleasedAt = DateTime.UtcNow;
        _availableSessions.Add(session);

        _logger.LogDebug(
            "Released session {SessionId} back to pool",
            session.SessionId);
    }

    /// <summary>
    /// Get pool statistics
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        return new()
        {
            ["poolSize"] = _poolSize,
            ["availableCount"] = _availableSessions.Count,
            ["inUseCount"] = _poolSize - _availableSessions.Count
        };
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing model session pool");
    }
}

/// <summary>
/// Model session wrapper
/// </summary>
public class ModelSession
{
    public string SessionId { get; set; } = string.Empty;
    public bool IsInUse { get; set; }
    public DateTime AcquiredAt { get; set; }
    public DateTime ReleasedAt { get; set; }
}

/// <summary>
/// ML model inference engine
/// Serves predictions using ONNX Runtime
/// </summary>
public class MLInferenceEngine
{
    private readonly Dictionary<string, MLModel> _models = new();
    private readonly Dictionary<string, ModelSessionPool> _sessionPools = new();
    private readonly Dictionary<string, Queue<long>> _latencyMetrics = new();
    private readonly ILogger<MLInferenceEngine> _logger;

    public MLInferenceEngine(ILogger<MLInferenceEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register model
    /// </summary>
    public async Task RegisterModelAsync(MLModel model, int sessionPoolSize = 5)
    {
        _models[model.Id] = model;

        // Create session pool for thread-safe inference
        var pool = new ModelSessionPool(model, sessionPoolSize, _logger);
        _sessionPools[model.Id] = pool;

        _latencyMetrics[model.Id] = new();

        _logger.LogInformation(
            "Registered model {Name} (v{Version}) with {PoolSize} sessions",
            model.Name,
            model.Version,
            sessionPoolSize);
    }

    /// <summary>
    /// Run inference
    /// </summary>
    public async Task<InferenceResponse> InferAsync(InferenceRequest request)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!_models.TryGetValue(request.ModelId, out var model))
            {
                throw new InvalidOperationException($"Model {request.ModelId} not found");
            }

            // Acquire session from thread-safe pool
            ModelSession session = null;
            try
            {
                session = await _sessionPools[request.ModelId].AcquireSessionAsync();

                // Validate inputs against schema
                ValidateInputs(request.Inputs, model.InputSchema);

                // Run inference (simplified - actual ONNX Runtime call)
                var outputs = await RunInferenceAsync(model, request.Inputs, session);

                stopwatch.Stop();

                // Record metrics
                _latencyMetrics[request.ModelId].Enqueue(stopwatch.ElapsedMilliseconds);
                if (_latencyMetrics[request.ModelId].Count > 1000)
                {
                    _latencyMetrics[request.ModelId].Dequeue();
                }

                _logger.LogInformation(
                    "Inference completed: model={Model}, latency={Latency}ms",
                    model.Name,
                    stopwatch.ElapsedMilliseconds);

                return new InferenceResponse
                {
                    RequestId = request.Id,
                    Outputs = outputs,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    ModelVersion = model.Version,
                    ConfidenceScores = ExtractConfidenceScores(outputs)
                };
            }
            finally
            {
                if (session != null)
                {
                    await _sessionPools[request.ModelId].ReleaseSessionAsync(session);
                }
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Inference failed for model {Model}", request.ModelId);

            return new InferenceResponse
            {
                RequestId = request.Id,
                Outputs = new() { ["error"] = ex.Message },
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Batch inference - more efficient for multiple predictions
    /// </summary>
    public async Task<List<InferenceResponse>> InferenceBatchAsync(List<InferenceRequest> requests)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<InferenceResponse>();

        // Process in parallel for better throughput
        var tasks = requests.Select(r => InferAsync(r)).ToList();
        var responses = await Task.WhenAll(tasks);

        stopwatch.Stop();

        _logger.LogInformation(
            "Batch inference completed: {Count} predictions in {Latency}ms",
            requests.Count,
            stopwatch.ElapsedMilliseconds);

        return responses.ToList();
    }

    /// <summary>
    /// Run inference with model
    /// </summary>
    private async Task<Dictionary<string, object>> RunInferenceAsync(
        MLModel model,
        Dictionary<string, object> inputs,
        ModelSession session)
    {
        // Simulate inference
        await Task.Delay(10); // Simulated inference time

        // Build outputs based on schema
        var outputs = new Dictionary<string, object>();
        foreach (var outputKey in model.OutputSchema.Keys)
        {
            outputs[outputKey] = new[] { 0.95, 0.04, 0.01 }; // Simulated predictions
        }

        return outputs;
    }

    /// <summary>
    /// Validate inputs against model schema
    /// </summary>
    private void ValidateInputs(Dictionary<string, object> inputs, Dictionary<string, string> schema)
    {
        foreach (var schemaKey in schema.Keys)
        {
            if (!inputs.ContainsKey(schemaKey))
            {
                throw new ArgumentException($"Missing required input: {schemaKey}");
            }
        }
    }

    /// <summary>
    /// Extract confidence scores from outputs
    /// </summary>
    private Dictionary<string, double> ExtractConfidenceScores(Dictionary<string, object> outputs)
    {
        var scores = new Dictionary<string, double>();

        foreach (var kvp in outputs)
        {
            if (kvp.Value is double[] predictions)
            {
                scores[kvp.Key] = predictions.Max();
            }
        }

        return scores;
    }

    /// <summary>
    /// Get model metrics
    /// </summary>
    public PredictionMetrics GetMetrics(string modelId)
    {
        if (!_latencyMetrics.TryGetValue(modelId, out var latencies))
        {
            return new() { ModelId = modelId };
        }

        var latencyList = latencies.ToList();

        return new PredictionMetrics
        {
            ModelId = modelId,
            TotalInferences = latencyList.Count,
            AverageLatencyMs = latencyList.Count > 0 ? latencyList.Average() : 0,
            P99LatencyMs = latencyList.Count > 0 ? latencyList.OrderByDescending(x => x).Take((int)(latencyList.Count * 0.01)).FirstOrDefault() : 0,
            ThroughputQps = latencyList.Count > 0 ? 1000.0 / latencyList.Average() : 0,
            ErrorRate = 0.0 // Simplified
        };
    }

    /// <summary>
    /// List all registered models
    /// </summary>
    public List<MLModel> ListModels()
    {
        return _models.Values.ToList();
    }
}

/// <summary>
/// Model A/B testing for gradual rollout
/// </summary>
public class ModelABTesting
{
    private readonly Dictionary<string, ModelVariant> _variants = new();
    private readonly ILogger<ModelABTesting> _logger;

    public ModelABTesting(ILogger<ModelABTesting> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register model variant
    /// </summary>
    public async Task RegisterVariantAsync(string experimentId, ModelVariant variant)
    {
        _variants[experimentId] = variant;

        _logger.LogInformation(
            "Registered model variant: {Experiment}, control={Control}, treatment={Treatment}",
            experimentId,
            variant.ControlModelId,
            variant.TreatmentModelId);
    }

    /// <summary>
    /// Select model variant based on user cohort
    /// </summary>
    public string SelectVariant(string experimentId, string userId)
    {
        if (!_variants.TryGetValue(experimentId, out var variant))
        {
            throw new InvalidOperationException($"Experiment {experimentId} not found");
        }

        // Consistent hashing to assign user to variant
        var hash = Math.Abs(userId.GetHashCode());
        var trafficPercentage = hash % 100;

        return trafficPercentage < variant.TrafficPercentage
            ? variant.TreatmentModelId
            : variant.ControlModelId;
    }
}

/// <summary>
/// Model variant configuration
/// </summary>
public class ModelVariant
{
    [JsonPropertyName("experimentId")]
    public string ExperimentId { get; set; } = string.Empty;

    [JsonPropertyName("controlModelId")]
    public string ControlModelId { get; set; } = string.Empty;

    [JsonPropertyName("treatmentModelId")]
    public string TreatmentModelId { get; set; } = string.Empty;

    [JsonPropertyName("trafficPercentage")]
    public int TrafficPercentage { get; set; } = 50; // % of users in treatment

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("endsAt")]
    public DateTime? EndsAt { get; set; }

    [JsonPropertyName("metrics")]
    public Dictionary<string, double> Metrics { get; set; } = new();
}

/// <summary>
/// Extension methods
/// </summary>
public static class MLServingExtensions
{
    public static IServiceCollection AddMLModelServing(this IServiceCollection services)
    {
        services.AddSingleton<MLInferenceEngine>();
        services.AddSingleton<ModelABTesting>();
        return services;
    }
}
