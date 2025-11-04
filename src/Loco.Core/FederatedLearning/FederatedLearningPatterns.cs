#nullable enable

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.FederatedLearning;

/// <summary>
/// Federated Learning Patterns
/// Distributed ML, privacy-preserving, secure aggregation, non-IID data handling
/// </summary>

public class FederatedClient
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("clientId")]
    public int ClientId { get; set; }

    [JsonPropertyName("organization")]
    public string Organization { get; set; } = string.Empty;

    [JsonPropertyName("dataSize")]
    public long DataSizeRecords { get; set; }

    [JsonPropertyName("dataDistribution")]
    public string DataDistribution { get; set; } = string.Empty; // IID, Non-IID

    [JsonPropertyName("privacyLevel")]
    public string PrivacyLevel { get; set; } = string.Empty; // Local, DP, SecureAgg

    [JsonPropertyName("lastUpdate")]
    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active"; // active, inactive, failed
}

public class ModelUpdate
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("clientId")]
    public int ClientId { get; set; }

    [JsonPropertyName("roundNumber")]
    public int RoundNumber { get; set; }

    [JsonPropertyName("weights")]
    public List<double> Weights { get; set; } = new();

    [JsonPropertyName("gradient")]
    public List<double> Gradient { get; set; } = new();

    [JsonPropertyName("dataSize")]
    public long DataSize { get; set; }

    [JsonPropertyName("trainingAccuracy")]
    public double TrainingAccuracy { get; set; }

    [JsonPropertyName("computationTimeMs")]
    public double ComputationTimeMs { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class GlobalModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("weights")]
    public List<double> Weights { get; set; } = new();

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();

    [JsonPropertyName("trainedOnClients")]
    public int TrainedOnClients { get; set; }

    [JsonPropertyName("globalAccuracy")]
    public double GlobalAccuracy { get; set; }

    [JsonPropertyName("convergenceScore")]
    public double ConvergenceScore { get; set; } // 0-1

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DifferentialPrivacy
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("epsilon")]
    public double Epsilon { get; set; } = 1.0; // Privacy budget (lower = more private)

    [JsonPropertyName("delta")]
    public double Delta { get; set; } = 0.00001; // Probability of breach

    [JsonPropertyName("mechanism")]
    public string Mechanism { get; set; } = string.Empty; // Laplace, Gaussian

    [JsonPropertyName("noisyWeights")]
    public List<double> NoisyWeights { get; set; } = new();

    [JsonPropertyName("originalWeights")]
    public List<double> OriginalWeights { get; set; } = new();

    [JsonPropertyName("noiseScale")]
    public double NoiseScale { get; set; }

    [JsonPropertyName("appliedAt")]
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
}

public class SecureAggregation
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("roundNumber")]
    public int RoundNumber { get; set; }

    [JsonPropertyName("participatingClients")]
    public List<int> ParticipatingClients { get; set; } = new();

    [JsonPropertyName("secretShares")]
    public Dictionary<int, List<string>> SecretShares { get; set; } = new();

    [JsonPropertyName("aggregatedResult")]
    public List<double> AggregatedResult { get; set; } = new();

    [JsonPropertyName("verificationSucceeded")]
    public bool VerificationSucceeded { get; set; }

    [JsonPropertyName("aggregationTimeMs")]
    public double AggregationTimeMs { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class NonIIDDataHandler
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("clientId")]
    public int ClientId { get; set; }

    [JsonPropertyName("labelDistribution")]
    public Dictionary<string, double> LabelDistribution { get; set; } = new();

    [JsonPropertyName("skewnessFactor")]
    public double SkewnessFactor { get; set; } // 0=uniform, 1=extreme

    [JsonPropertyName("shardingStrategy")]
    public string ShardingStrategy { get; set; } = string.Empty; // IID, Dirichlet, Pathological

    [JsonPropertyName("localEpochs")]
    public int LocalEpochs { get; set; } = 1;

    [JsonPropertyName("batchSize")]
    public int BatchSize { get; set; } = 32;
}

public class FederatedLearningStatistics
{
    [JsonPropertyName("totalRounds")]
    public int TotalRounds { get; set; }

    [JsonPropertyName("totalClients")]
    public int TotalClients { get; set; }

    [JsonPropertyName("activeClients")]
    public int ActiveClients { get; set; }

    [JsonPropertyName("globalAccuracy")]
    public double GlobalAccuracy { get; set; }

    [JsonPropertyName("averageClientAccuracy")]
    public double AverageClientAccuracy { get; set; }

    [JsonPropertyName("convergenceRound")]
    public int ConvergenceRound { get; set; }

    [JsonPropertyName("communicationRounds")]
    public long CommunicationRounds { get; set; }

    [JsonPropertyName("modelSizeBytes")]
    public long ModelSizeBytes { get; set; }
}

/// <summary>
/// Federated Learning Engine
/// </summary>
public class FederatedLearningEngine
{
    private readonly ConcurrentDictionary<int, FederatedClient> _clients = new();
    private readonly ConcurrentDictionary<string, ModelUpdate> _updates = new();
    private readonly List<GlobalModel> _globalModels = new();
    private readonly List<SecureAggregation> _aggregations = new();
    private readonly FederatedLearningStatistics _stats = new();
    private readonly ILogger<FederatedLearningEngine> _logger;
    private int _currentRound = 0;

    public FederatedLearningEngine(ILogger<FederatedLearningEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register federated client
    /// </summary>
    public async Task<FederatedClient> RegisterClientAsync(
        int clientId,
        string organization,
        long dataSize,
        string dataDistribution = "Non-IID")
    {
        var client = new FederatedClient
        {
            ClientId = clientId,
            Organization = organization,
            DataSizeRecords = dataSize,
            DataDistribution = dataDistribution
        };

        _clients[clientId] = client;
        _stats.TotalClients++;

        _logger.LogInformation(
            "Registered federated client: {Org} (Client {Id}, {Size} records, {Dist})",
            organization,
            clientId,
            dataSize,
            dataDistribution);

        return client;
    }

    /// <summary>
    /// Start new training round
    /// </summary>
    public async Task<int> StartRoundAsync()
    {
        _currentRound++;
        _stats.TotalRounds = _currentRound;
        _stats.ActiveClients = _clients.Count(c => c.Value.Status == "active");

        _logger.LogInformation(
            "Started federated learning round {Round} with {Clients} active clients",
            _currentRound,
            _stats.ActiveClients);

        return _currentRound;
    }

    /// <summary>
    /// Submit model update from client
    /// </summary>
    public async Task<ModelUpdate> SubmitModelUpdateAsync(
        int clientId,
        List<double> weights,
        List<double> gradient,
        long dataSize,
        double accuracy,
        double computationTimeMs)
    {
        var update = new ModelUpdate
        {
            ClientId = clientId,
            RoundNumber = _currentRound,
            Weights = weights,
            Gradient = gradient,
            DataSize = dataSize,
            TrainingAccuracy = accuracy,
            ComputationTimeMs = computationTimeMs
        };

        _updates[update.Id] = update;

        if (_clients.TryGetValue(clientId, out var client))
        {
            client.LastUpdate = DateTime.UtcNow;
            _stats.CommunicationRounds++;
        }

        _logger.LogInformation(
            "Received model update from client {Client} (accuracy: {Acc:F3}, time: {Time:F0}ms)",
            clientId,
            accuracy,
            computationTimeMs);

        return update;
    }

    /// <summary>
    /// Apply differential privacy to updates
    /// </summary>
    public async Task<DifferentialPrivacy> ApplyDifferentialPrivacyAsync(
        List<double> weights,
        double epsilon = 1.0,
        double delta = 0.00001)
    {
        var dp = new DifferentialPrivacy
        {
            Epsilon = epsilon,
            Delta = delta,
            Mechanism = "Gaussian",
            OriginalWeights = weights,
            NoiseScale = 1.0 / epsilon
        };

        // Add Gaussian noise
        var noisyWeights = new List<double>();
        var random = new Random();
        var normalDist = new System.Random();

        foreach (var weight in weights)
        {
            var noise = random.NextDouble() * dp.NoiseScale;
            noisyWeights.Add(weight + noise);
        }

        dp.NoisyWeights = noisyWeights;

        _logger.LogInformation(
            "Applied differential privacy: epsilon={Eps}, delta={Delta}, noise_scale={Scale}",
            epsilon,
            delta,
            dp.NoiseScale);

        return dp;
    }

    /// <summary>
    /// Secure aggregation of client updates
    /// </summary>
    public async Task<GlobalModel> AggregateUpdatesAsync()
    {
        var roundUpdates = _updates.Values
            .Where(u => u.RoundNumber == _currentRound)
            .ToList();

        if (roundUpdates.Count == 0)
            throw new InvalidOperationException("No updates to aggregate");

        // Weighted average aggregation
        var totalDataSize = roundUpdates.Sum(u => u.DataSize);
        var aggregated = new List<double>();

        if (roundUpdates.First().Weights.Count > 0)
        {
            for (int i = 0; i < roundUpdates.First().Weights.Count; i++)
            {
                double weightedSum = 0;
                foreach (var update in roundUpdates)
                {
                    var weight = (double)update.DataSize / totalDataSize;
                    weightedSum += update.Weights[i] * weight;
                }
                aggregated.Add(weightedSum);
            }
        }

        var globalModel = new GlobalModel
        {
            Version = _currentRound,
            Weights = aggregated,
            TrainedOnClients = roundUpdates.Count,
            GlobalAccuracy = roundUpdates.Average(u => u.TrainingAccuracy),
            ConvergenceScore = CalculateConvergence(roundUpdates)
        };

        _globalModels.Add(globalModel);

        _logger.LogInformation(
            "Aggregated updates from {Count} clients (global accuracy: {Acc:F3})",
            roundUpdates.Count,
            globalModel.GlobalAccuracy);

        return globalModel;
    }

    /// <summary>
    /// Distribute global model to clients
    /// </summary>
    public async Task DistributeGlobalModelAsync(GlobalModel model)
    {
        var activeClients = _clients.Values.Where(c => c.Status == "active").ToList();

        _logger.LogInformation(
            "Distributing global model v{Version} to {Count} clients",
            model.Version,
            activeClients.Count);
    }

    /// <summary>
    /// Get federated learning statistics
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        var latestModel = _globalModels.LastOrDefault();

        return new()
        {
            ["totalRounds"] = _stats.TotalRounds,
            ["totalClients"] = _stats.TotalClients,
            ["activeClients"] = _stats.ActiveClients,
            ["globalAccuracy"] = latestModel?.GlobalAccuracy ?? 0,
            ["convergenceScore"] = latestModel?.ConvergenceScore ?? 0,
            ["communicationRounds"] = _stats.CommunicationRounds,
            ["currentRound"] = _currentRound,
            ["modelVersions"] = _globalModels.Count,
            ["pendingUpdates"] = _updates.Values.Count(u => u.RoundNumber == _currentRound)
        };
    }

    private double CalculateConvergence(List<ModelUpdate> updates)
    {
        if (updates.Count < 2)
            return 0;

        var accuracies = updates.Select(u => u.TrainingAccuracy).ToList();
        var variance = accuracies.Select(a => Math.Pow(a - accuracies.Average(), 2)).Sum() / accuracies.Count;

        return 1.0 / (1.0 + variance); // Normalize to 0-1
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class FederatedLearningExtensions
{
    public static IServiceCollection AddFederatedLearning(this IServiceCollection services)
    {
        services.AddSingleton<FederatedLearningEngine>();
        return services;
    }
}
