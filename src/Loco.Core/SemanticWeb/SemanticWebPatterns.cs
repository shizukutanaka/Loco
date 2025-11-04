#nullable enable

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.SemanticWeb;

/// <summary>
/// Semantic Web & Ontology Patterns
/// RDF, OWL, knowledge representation, semantic reasoning
/// </summary>

public class RDFTriple
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty; // Entity

    [JsonPropertyName("predicate")]
    public string Predicate { get; set; } = string.Empty; // Relationship

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty; // Value or Entity

    [JsonPropertyName("context")]
    public string Context { get; set; } = string.Empty; // Named graph

    [JsonPropertyName("dataType")]
    public string DataType { get; set; } = string.Empty; // Literal, URI, BlankNode

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OntologyClass
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty; // Unique identifier

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty; // Human-readable name

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parentClasses")]
    public List<string> ParentClasses { get; set; } = new(); // Inheritance

    [JsonPropertyName("subClasses")]
    public List<string> SubClasses { get; set; } = new();

    [JsonPropertyName("properties")]
    public List<OntologyProperty> Properties { get; set; } = new();

    [JsonPropertyName("restrictions")]
    public Dictionary<string, object> Restrictions { get; set; } = new();
}

public class OntologyProperty
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty; // Class it applies to

    [JsonPropertyName("range")]
    public string Range { get; set; } = string.Empty; // Expected value type

    [JsonPropertyName("cardinality")]
    public string Cardinality { get; set; } = string.Empty; // 0..1, 1..1, 0..*, 1..*

    [JsonPropertyName("isFunctional")]
    public bool IsFunctional { get; set; } // True = only one value allowed

    [JsonPropertyName("isInverseFunctional")]
    public bool IsInverseFunctional { get; set; }
}

public class SemanticEntity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public List<string> Type { get; set; } = new(); // Classes entity belongs to

    [JsonPropertyName("attributes")]
    public Dictionary<string, object> Attributes { get; set; } = new();

    [JsonPropertyName("relationships")]
    public Dictionary<string, List<string>> Relationships { get; set; } = new();

    [JsonPropertyName("labels")]
    public Dictionary<string, string> Labels { get; set; } = new(); // Multi-language labels

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SemanticQuery
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("sparqlQuery")]
    public string SparqlQuery { get; set; } = string.Empty;

    [JsonPropertyName("naturalLanguage")]
    public string NaturalLanguage { get; set; } = string.Empty;

    [JsonPropertyName("resultBindings")]
    public List<Dictionary<string, string>> ResultBindings { get; set; } = new();

    [JsonPropertyName("executionTimeMs")]
    public double ExecutionTimeMs { get; set; }

    [JsonPropertyName("resultCount")]
    public int ResultCount { get; set; }
}

public class InferenceRule
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("antecedent")]
    public string Antecedent { get; set; } = string.Empty; // IF condition

    [JsonPropertyName("consequent")]
    public string Consequent { get; set; } = string.Empty; // THEN conclusion

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;

    [JsonPropertyName("fired")]
    public int FiredCount { get; set; } = 0;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 1.0;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class KnowledgeGraph
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("tripleCount")]
    public long TripleCount { get; set; }

    [JsonPropertyName("classCount")]
    public int ClassCount { get; set; }

    [JsonPropertyName("propertyCount")]
    public int PropertyCount { get; set; }

    [JsonPropertyName("entityCount")]
    public long EntityCount { get; set; }

    [JsonPropertyName("inferenceRulesCount")]
    public int InferenceRulesCount { get; set; }

    [JsonPropertyName("coverage")]
    public double CoveragePercent { get; set; }
}

public class SemanticWebStatistics
{
    [JsonPropertyName("totalTriples")]
    public long TotalTriples { get; set; }

    [JsonPropertyName("totalEntities")]
    public long TotalEntities { get; set; }

    [JsonPropertyName("totalProperties")]
    public int TotalProperties { get; set; }

    [JsonPropertyName("totalClasses")]
    public int TotalClasses { get; set; }

    [JsonPropertyName("inferenceRulesFired")]
    public long InferenceRulesFired { get; set; }

    [JsonPropertyName("queryTime50P")]
    public double QueryTime50PMs { get; set; }

    [JsonPropertyName("queryTime99P")]
    public double QueryTime99PMs { get; set; }

    [JsonPropertyName("ontologyCompleteness")]
    public double OntologyCompleteness { get; set; }
}

/// <summary>
/// Semantic Web Engine
/// </summary>
public class SemanticWebEngine
{
    private readonly ConcurrentDictionary<string, RDFTriple> _triples = new();
    private readonly ConcurrentDictionary<string, OntologyClass> _classes = new();
    private readonly ConcurrentDictionary<string, OntologyProperty> _properties = new();
    private readonly ConcurrentDictionary<string, SemanticEntity> _entities = new();
    private readonly List<InferenceRule> _rules = new();
    private readonly List<SemanticQuery> _queryLog = new();
    private readonly SemanticWebStatistics _stats = new();
    private readonly ILogger<SemanticWebEngine> _logger;

    public SemanticWebEngine(ILogger<SemanticWebEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Add RDF triple
    /// </summary>
    public async Task<RDFTriple> AddTripleAsync(
        string subject,
        string predicate,
        string objectValue,
        string dataType = "URI")
    {
        var triple = new RDFTriple
        {
            Subject = subject,
            Predicate = predicate,
            Object = objectValue,
            DataType = dataType
        };

        _triples[triple.Id] = triple;
        _stats.TotalTriples++;

        _logger.LogInformation(
            "Added RDF triple: {Subject} -{Predicate}-> {Object}",
            subject,
            predicate,
            objectValue);

        return triple;
    }

    /// <summary>
    /// Define ontology class
    /// </summary>
    public async Task<OntologyClass> DefineClassAsync(
        string uri,
        string label,
        string? parentClass = null)
    {
        var ontologyClass = new OntologyClass
        {
            Uri = uri,
            Label = label,
            ParentClasses = parentClass != null ? new() { parentClass } : new()
        };

        _classes[uri] = ontologyClass;
        _stats.TotalClasses++;

        _logger.LogInformation(
            "Defined ontology class: {Label} ({Uri})",
            label,
            uri);

        return ontologyClass;
    }

    /// <summary>
    /// Define ontology property
    /// </summary>
    public async Task<OntologyProperty> DefinePropertyAsync(
        string uri,
        string label,
        string domain,
        string range,
        string cardinality = "0..*")
    {
        var property = new OntologyProperty
        {
            Uri = uri,
            Label = label,
            Domain = domain,
            Range = range,
            Cardinality = cardinality,
            IsFunctional = cardinality.Contains("1")
        };

        _properties[uri] = property;
        _stats.TotalProperties++;

        _logger.LogInformation(
            "Defined property: {Label} ({Domain} -> {Range})",
            label,
            domain,
            range);

        return property;
    }

    /// <summary>
    /// Create semantic entity
    /// </summary>
    public async Task<SemanticEntity> CreateEntityAsync(
        string uri,
        List<string> types,
        Dictionary<string, object> attributes)
    {
        var entity = new SemanticEntity
        {
            Uri = uri,
            Type = types,
            Attributes = attributes
        };

        _entities[uri] = entity;
        _stats.TotalEntities++;

        _logger.LogInformation(
            "Created semantic entity: {Uri} (types: {Types})",
            uri,
            string.Join(", ", types));

        return entity;
    }

    /// <summary>
    /// Register inference rule
    /// </summary>
    public async Task RegisterInferenceRuleAsync(
        string name,
        string antecedent,
        string consequent,
        double confidence = 1.0)
    {
        var rule = new InferenceRule
        {
            Name = name,
            Antecedent = antecedent,
            Consequent = consequent,
            Confidence = confidence
        };

        _rules.Add(rule);

        _logger.LogInformation(
            "Registered inference rule: {Name} (confidence: {Conf:F2})",
            name,
            confidence);
    }

    /// <summary>
    /// Execute SPARQL query
    /// </summary>
    public async Task<SemanticQuery> ExecuteQueryAsync(
        string sparqlQuery,
        string? naturalLanguage = null)
    {
        var startTime = DateTime.UtcNow;

        var query = new SemanticQuery
        {
            SparqlQuery = sparqlQuery,
            NaturalLanguage = naturalLanguage,
            ResultBindings = new(),
            ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
            ResultCount = 0
        };

        // Simulate query execution
        if (sparqlQuery.Contains("SELECT"))
        {
            query.ResultBindings = _entities
                .Take(5)
                .Select(e => new Dictionary<string, string> { ["?entity"] = e.Value.Uri })
                .ToList();
            query.ResultCount = query.ResultBindings.Count;
        }

        _queryLog.Add(query);

        _logger.LogInformation(
            "Executed SPARQL query: {Results} results in {Time}ms",
            query.ResultCount,
            query.ExecutionTimeMs);

        return query;
    }

    /// <summary>
    /// Fire inference rules
    /// </summary>
    public async Task<List<string>> FireInferenceRulesAsync()
    {
        var inferences = new List<string>();

        foreach (var rule in _rules.OrderByDescending(r => r.Priority))
        {
            if (MatchesAntecedent(rule.Antecedent))
            {
                inferences.Add(rule.Consequent);
                rule.FiredCount++;
                _stats.InferenceRulesFired++;

                _logger.LogInformation(
                    "Fired inference rule: {Name} -> {Consequent}",
                    rule.Name,
                    rule.Consequent);
            }
        }

        return inferences;
    }

    /// <summary>
    /// Get semantic web statistics
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        var queryTimes = _queryLog.Select(q => q.ExecutionTimeMs).ToList();

        return new()
        {
            ["totalTriples"] = _stats.TotalTriples,
            ["totalEntities"] = _stats.TotalEntities,
            ["totalClasses"] = _stats.TotalClasses,
            ["totalProperties"] = _stats.TotalProperties,
            ["totalRules"] = _rules.Count,
            ["inferenceRulesFired"] = _stats.InferenceRulesFired,
            ["totalQueries"] = _queryLog.Count,
            ["query50thPercentileMs"] = queryTimes.Count > 0
                ? Math.Round(queryTimes.OrderBy(x => x).Skip((int)(queryTimes.Count * 0.5)).First(), 2)
                : 0,
            ["query99thPercentileMs"] = queryTimes.Count > 0
                ? Math.Round(queryTimes.OrderBy(x => x).Skip((int)(queryTimes.Count * 0.99)).FirstOrDefault(), 2)
                : 0
        };
    }

    private bool MatchesAntecedent(string antecedent)
    {
        // Simplified antecedent matching
        return _entities.Values.Any(e =>
            e.Type.Any(t => t.Contains(antecedent.Split(" ")[0])));
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class SemanticWebExtensions
{
    public static IServiceCollection AddSemanticWeb(this IServiceCollection services)
    {
        services.AddSingleton<SemanticWebEngine>();
        return services;
    }
}
