// John Carmack: "The key to good code is to think in terms of data flow"
// Rob Pike: "Fancy algorithms are slow when n is small, and n is usually small"

using System.Text.Json;
using System.Text.Json.Serialization;
using Loco.Core.Practical;

namespace Loco.Core.AI;

/// <summary>
/// AI-powered workflow generator
/// Converts natural language descriptions into executable Loco workflows
/// </summary>
public sealed class WorkflowGenerator
{
    private readonly ILLMProvider _llmProvider;
    private readonly PatternLibrary _patternLibrary;
    private readonly SimpleLogger _logger;

    public WorkflowGenerator(
        ILLMProvider llmProvider,
        PatternLibrary? patternLibrary = null,
        SimpleLogger? logger = null)
    {
        _llmProvider = llmProvider;
        _patternLibrary = patternLibrary ?? PatternLibrary.Default;
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(WorkflowGenerator));
    }

    /// <summary>
    /// Generate a workflow from a natural language description
    /// </summary>
    public async Task<WorkflowGenerationResult> GenerateAsync(
        string description,
        WorkflowGenerationOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new WorkflowGenerationOptions();

        _logger.Info($"Generating workflow from: {description}");

        // Step 1: Extract intent
        var intent = await ExtractIntentAsync(description, ct);

        _logger.Debug($"Extracted intent: {intent.Goal}, patterns: [{string.Join(", ", intent.SuggestedPatterns)}]");

        // Step 2: Match patterns
        var matchedPatterns = _patternLibrary.MatchPatterns(intent);

        // Step 3: Generate workflow
        var prompt = BuildGenerationPrompt(description, intent, matchedPatterns, options);
        var response = await _llmProvider.CompleteAsync(prompt, options.Temperature, ct);

        // Step 4: Parse and validate
        var workflow = ParseWorkflowResponse(response);

        if (workflow == null)
        {
            return new WorkflowGenerationResult
            {
                Success = false,
                ErrorMessage = "Failed to generate valid workflow JSON",
                RawResponse = response
            };
        }

        // Step 5: Validate workflow
        var validation = ValidateWorkflow(workflow);

        return new WorkflowGenerationResult
        {
            Success = validation.IsValid,
            Workflow = workflow,
            Intent = intent,
            MatchedPatterns = matchedPatterns.Select(p => p.Id).ToList(),
            Confidence = intent.Confidence,
            ValidationErrors = validation.Errors,
            RawResponse = response
        };
    }

    /// <summary>
    /// Generate workflow suggestions based on partial input
    /// </summary>
    public async Task<List<WorkflowSuggestion>> SuggestAsync(
        string partialDescription,
        CancellationToken ct = default)
    {
        var prompt = $@"Given this partial workflow description, suggest 3-5 complete workflow ideas:

Description: {partialDescription}

Return JSON array of suggestions:
[{{""title"": ""..., ""description"": ""..., ""complexity"": ""simple|medium|complex""}}]";

        var response = await _llmProvider.CompleteAsync(prompt, 0.7f, ct);

        try
        {
            return JsonSerializer.Deserialize<List<WorkflowSuggestion>>(response)
                ?? new List<WorkflowSuggestion>();
        }
        catch
        {
            return new List<WorkflowSuggestion>();
        }
    }

    /// <summary>
    /// Recommend next steps for a workflow in progress
    /// </summary>
    public async Task<List<StepRecommendation>> RecommendNextStepsAsync(
        GeneratedWorkflow currentWorkflow,
        string lastAddedStepId,
        CancellationToken ct = default)
    {
        var lastStep = currentWorkflow.Nodes.FirstOrDefault(n => n.Id == lastAddedStepId);
        if (lastStep == null)
        {
            return new List<StepRecommendation>();
        }

        var prompt = $@"Given a workflow with these steps:
{JsonSerializer.Serialize(currentWorkflow.Nodes.Select(n => new { n.Id, n.Type, n.Integration, n.Action }))}

The last added step was: {lastStep.Integration}/{lastStep.Action}

Recommend 3-5 logical next steps. Return JSON array:
[{{""integration"": ""..."", ""action"": ""..."", ""reason"": ""..."", ""confidence"": 0.0-1.0}}]";

        var response = await _llmProvider.CompleteAsync(prompt, 0.5f, ct);

        try
        {
            return JsonSerializer.Deserialize<List<StepRecommendation>>(response)
                ?? new List<StepRecommendation>();
        }
        catch
        {
            return new List<StepRecommendation>();
        }
    }

    /// <summary>
    /// Explain an error and suggest fixes
    /// </summary>
    public async Task<ErrorExplanation> ExplainErrorAsync(
        string errorMessage,
        string? stepContext = null,
        CancellationToken ct = default)
    {
        var prompt = $@"Explain this workflow error and suggest fixes:

Error: {errorMessage}
{(stepContext != null ? $"Step context: {stepContext}" : "")}

Return JSON:
{{
    ""rootCause"": ""..."",
    ""explanation"": ""..."",
    ""suggestedFixes"": [{{""title"": ""..."", ""steps"": [""...""]}}],
    ""preventionTips"": [""...""]
}}";

        var response = await _llmProvider.CompleteAsync(prompt, 0.3f, ct);

        try
        {
            return JsonSerializer.Deserialize<ErrorExplanation>(response)
                ?? new ErrorExplanation { RootCause = "Unable to analyze error" };
        }
        catch
        {
            return new ErrorExplanation { RootCause = errorMessage };
        }
    }

    private async Task<WorkflowIntent> ExtractIntentAsync(string description, CancellationToken ct)
    {
        var prompt = $@"Extract the workflow intent from this description:

""{description}""

Identify:
1. Main goal (what the workflow should accomplish)
2. Trigger type (schedule, webhook, manual, event)
3. Required integrations (http, database, slack, email, s3, etc.)
4. Key actions and their order
5. Conditions or branching logic
6. Error handling needs

Return JSON:
{{
    ""goal"": ""brief description"",
    ""triggerType"": ""schedule|webhook|manual|event"",
    ""schedule"": ""cron expression if scheduled"",
    ""integrations"": [""list of integrations""],
    ""actions"": [""ordered list of actions""],
    ""hasConditions"": true/false,
    ""needsErrorHandling"": true/false,
    ""suggestedPatterns"": [""pattern names that might help""],
    ""confidence"": 0.0-1.0
}}";

        var response = await _llmProvider.CompleteAsync(prompt, 0.3f, ct);

        try
        {
            return JsonSerializer.Deserialize<WorkflowIntent>(response)
                ?? new WorkflowIntent { Goal = description };
        }
        catch
        {
            // Fallback to basic intent
            return new WorkflowIntent
            {
                Goal = description,
                TriggerType = "manual",
                Confidence = 0.5f
            };
        }
    }

    private string BuildGenerationPrompt(
        string description,
        WorkflowIntent intent,
        List<PatternInfo> patterns,
        WorkflowGenerationOptions options)
    {
        var patternDescriptions = string.Join("\n", patterns.Select(p =>
            $"- {p.Id}: {p.Description} (use cases: {string.Join(", ", p.UseCases)})"));

        return $@"Generate a Loco workflow from this description:

""{description}""

Extracted Intent:
- Goal: {intent.Goal}
- Trigger: {intent.TriggerType}
- Integrations needed: {string.Join(", ", intent.Integrations)}

Available Patterns:
{patternDescriptions}

Available Integrations and Actions:
- http: get, post, put, delete, download, upload
- postgresql: query, execute, scalar, transaction, bulkInsert
- slack: sendMessage, sendDirectMessage, listChannels, getUserInfo
- email: send, sendWithAttachment, sendTemplate, sendBulk
- s3: upload, download, delete, list, copy, getPresignedUrl

Generate a valid Loco workflow JSON:

{{
    ""name"": ""workflow name"",
    ""description"": ""what it does"",
    ""trigger"": {{
        ""type"": ""schedule|webhook|manual|event"",
        ""config"": {{}}
    }},
    ""nodes"": [
        {{
            ""id"": ""unique-id"",
            ""type"": ""action|condition|loop"",
            ""integration"": ""http|postgresql|slack|email|s3"",
            ""action"": ""action name"",
            ""name"": ""display name"",
            ""parameters"": {{}},
            ""retryConfig"": {{""maxAttempts"": 3}},
            ""onError"": ""continue|stop|retry""
        }}
    ],
    ""connections"": [
        {{
            ""sourceNodeId"": ""..."",
            ""targetNodeId"": ""..."",
            ""condition"": ""success|failure|always""
        }}
    ],
    ""variables"": {{""key"": ""default value""}},
    ""errorHandler"": {{
        ""type"": ""notification|retry|custom"",
        ""config"": {{}}
    }}
}}

Requirements:
1. Use variable interpolation: {{{{$node.nodeId.output.field}}}}
2. Include error handling for network operations
3. Add retry for unreliable operations
4. Use sensible defaults for parameters
5. Make connection conditions explicit

Return ONLY the JSON workflow, no explanation.";
    }

    private static GeneratedWorkflow? ParseWorkflowResponse(string response)
    {
        // Extract JSON from response (may have markdown code blocks)
        var json = response
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

        // Find JSON object boundaries
        var start = json.IndexOf('{');
        var end = json.LastIndexOf('}');

        if (start >= 0 && end > start)
        {
            json = json[start..(end + 1)];
        }

        try
        {
            return JsonSerializer.Deserialize<GeneratedWorkflow>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private static WorkflowValidation ValidateWorkflow(GeneratedWorkflow workflow)
    {
        var errors = new List<string>();

        // Check basic structure
        if (string.IsNullOrEmpty(workflow.Name))
        {
            errors.Add("Workflow name is required");
        }

        if (workflow.Nodes == null || workflow.Nodes.Count == 0)
        {
            errors.Add("Workflow must have at least one node");
        }

        // Check for orphan nodes (not connected)
        var connectedNodes = new HashSet<string>();
        if (workflow.Connections != null)
        {
            foreach (var conn in workflow.Connections)
            {
                connectedNodes.Add(conn.SourceNodeId);
                connectedNodes.Add(conn.TargetNodeId);
            }
        }

        if (workflow.Nodes != null)
        {
            foreach (var node in workflow.Nodes)
            {
                if (string.IsNullOrEmpty(node.Id))
                {
                    errors.Add("All nodes must have an ID");
                }

                if (string.IsNullOrEmpty(node.Integration) && node.Type == "action")
                {
                    errors.Add($"Action node '{node.Id}' must specify an integration");
                }
            }

            // Check for circular dependencies
            if (HasCircularDependencies(workflow))
            {
                errors.Add("Workflow has circular dependencies");
            }
        }

        return new WorkflowValidation
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private static bool HasCircularDependencies(GeneratedWorkflow workflow)
    {
        if (workflow.Connections == null) return false;

        var graph = new Dictionary<string, List<string>>();

        foreach (var conn in workflow.Connections)
        {
            if (!graph.ContainsKey(conn.SourceNodeId))
            {
                graph[conn.SourceNodeId] = new List<string>();
            }
            graph[conn.SourceNodeId].Add(conn.TargetNodeId);
        }

        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var node in graph.Keys)
        {
            if (DetectCycle(node, graph, visited, recursionStack))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DetectCycle(
        string node,
        Dictionary<string, List<string>> graph,
        HashSet<string> visited,
        HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(node)) return true;
        if (visited.Contains(node)) return false;

        visited.Add(node);
        recursionStack.Add(node);

        if (graph.TryGetValue(node, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (DetectCycle(neighbor, graph, visited, recursionStack))
                {
                    return true;
                }
            }
        }

        recursionStack.Remove(node);
        return false;
    }
}

/// <summary>
/// LLM provider interface for AI completions
/// </summary>
public interface ILLMProvider
{
    string Name { get; }
    bool IsAvailable { get; }

    Task<string> CompleteAsync(
        string prompt,
        float temperature = 0.7f,
        CancellationToken ct = default);

    Task<float[]> EmbedAsync(
        string text,
        CancellationToken ct = default);
}

/// <summary>
/// Options for workflow generation
/// </summary>
public sealed class WorkflowGenerationOptions
{
    public float Temperature { get; init; } = 0.3f;
    public bool IncludeErrorHandling { get; init; } = true;
    public bool IncludeRetry { get; init; } = true;
    public bool ValidateOutput { get; init; } = true;
    public int MaxNodes { get; init; } = 20;
}

/// <summary>
/// Result of workflow generation
/// </summary>
public sealed class WorkflowGenerationResult
{
    public bool Success { get; init; }
    public GeneratedWorkflow? Workflow { get; init; }
    public WorkflowIntent? Intent { get; init; }
    public List<string> MatchedPatterns { get; init; } = new();
    public float Confidence { get; init; }
    public List<string> ValidationErrors { get; init; } = new();
    public string? ErrorMessage { get; init; }
    public string? RawResponse { get; init; }
}

/// <summary>
/// Extracted workflow intent
/// </summary>
public sealed class WorkflowIntent
{
    [JsonPropertyName("goal")]
    public string Goal { get; init; } = "";

    [JsonPropertyName("triggerType")]
    public string TriggerType { get; init; } = "manual";

    [JsonPropertyName("schedule")]
    public string? Schedule { get; init; }

    [JsonPropertyName("integrations")]
    public List<string> Integrations { get; init; } = new();

    [JsonPropertyName("actions")]
    public List<string> Actions { get; init; } = new();

    [JsonPropertyName("hasConditions")]
    public bool HasConditions { get; init; }

    [JsonPropertyName("needsErrorHandling")]
    public bool NeedsErrorHandling { get; init; }

    [JsonPropertyName("suggestedPatterns")]
    public List<string> SuggestedPatterns { get; init; } = new();

    [JsonPropertyName("confidence")]
    public float Confidence { get; init; }
}

/// <summary>
/// Generated workflow structure
/// </summary>
public sealed class GeneratedWorkflow
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    [JsonPropertyName("trigger")]
    public WorkflowTrigger? Trigger { get; init; }

    [JsonPropertyName("nodes")]
    public List<WorkflowNode> Nodes { get; init; } = new();

    [JsonPropertyName("connections")]
    public List<WorkflowConnection> Connections { get; init; } = new();

    [JsonPropertyName("variables")]
    public Dictionary<string, object?> Variables { get; init; } = new();

    [JsonPropertyName("errorHandler")]
    public ErrorHandlerConfig? ErrorHandler { get; init; }
}

/// <summary>
/// Workflow trigger configuration
/// </summary>
public sealed class WorkflowTrigger
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "manual";

    [JsonPropertyName("config")]
    public Dictionary<string, object?> Config { get; init; } = new();
}

/// <summary>
/// Workflow node
/// </summary>
public sealed class WorkflowNode
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "action";

    [JsonPropertyName("integration")]
    public string? Integration { get; init; }

    [JsonPropertyName("action")]
    public string? Action { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object?> Parameters { get; init; } = new();

    [JsonPropertyName("retryConfig")]
    public Dictionary<string, object?>? RetryConfig { get; init; }

    [JsonPropertyName("onError")]
    public string? OnError { get; init; }
}

/// <summary>
/// Workflow connection
/// </summary>
public sealed class WorkflowConnection
{
    [JsonPropertyName("sourceNodeId")]
    public string SourceNodeId { get; init; } = "";

    [JsonPropertyName("targetNodeId")]
    public string TargetNodeId { get; init; } = "";

    [JsonPropertyName("condition")]
    public string? Condition { get; init; }
}

/// <summary>
/// Error handler configuration
/// </summary>
public sealed class ErrorHandlerConfig
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "notification";

    [JsonPropertyName("config")]
    public Dictionary<string, object?> Config { get; init; } = new();
}

/// <summary>
/// Workflow suggestion
/// </summary>
public sealed class WorkflowSuggestion
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    [JsonPropertyName("complexity")]
    public string Complexity { get; init; } = "simple";
}

/// <summary>
/// Step recommendation
/// </summary>
public sealed class StepRecommendation
{
    [JsonPropertyName("integration")]
    public string Integration { get; init; } = "";

    [JsonPropertyName("action")]
    public string Action { get; init; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = "";

    [JsonPropertyName("confidence")]
    public float Confidence { get; init; }
}

/// <summary>
/// Error explanation
/// </summary>
public sealed class ErrorExplanation
{
    [JsonPropertyName("rootCause")]
    public string RootCause { get; init; } = "";

    [JsonPropertyName("explanation")]
    public string Explanation { get; init; } = "";

    [JsonPropertyName("suggestedFixes")]
    public List<SuggestedFix> SuggestedFixes { get; init; } = new();

    [JsonPropertyName("preventionTips")]
    public List<string> PreventionTips { get; init; } = new();
}

/// <summary>
/// Suggested fix for an error
/// </summary>
public sealed class SuggestedFix
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("steps")]
    public List<string> Steps { get; init; } = new();
}

/// <summary>
/// Workflow validation result
/// </summary>
public sealed class WorkflowValidation
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
}

/// <summary>
/// Pattern library for workflow generation
/// </summary>
public sealed class PatternLibrary
{
    private readonly List<PatternInfo> _patterns = new();

    public static PatternLibrary Default => CreateDefault();

    public void AddPattern(PatternInfo pattern) => _patterns.Add(pattern);

    public List<PatternInfo> MatchPatterns(WorkflowIntent intent)
    {
        return _patterns
            .Select(p => new
            {
                Pattern = p,
                Score = CalculateMatchScore(p, intent)
            })
            .Where(x => x.Score > 0.3f)
            .OrderByDescending(x => x.Score)
            .Take(5)
            .Select(x => x.Pattern)
            .ToList();
    }

    private static float CalculateMatchScore(PatternInfo pattern, WorkflowIntent intent)
    {
        var score = 0f;

        // Check if pattern is in suggested patterns
        if (intent.SuggestedPatterns.Contains(pattern.Id, StringComparer.OrdinalIgnoreCase))
        {
            score += 0.5f;
        }

        // Check keyword matches in goal
        var goalWords = intent.Goal.ToLowerInvariant().Split(' ');
        var keywordMatches = pattern.Keywords.Count(k =>
            goalWords.Any(w => w.Contains(k, StringComparison.OrdinalIgnoreCase)));
        score += keywordMatches * 0.1f;

        // Check integration matches
        var integrationMatches = pattern.Integrations.Count(i =>
            intent.Integrations.Contains(i, StringComparer.OrdinalIgnoreCase));
        score += integrationMatches * 0.15f;

        return Math.Min(score, 1f);
    }

    private static PatternLibrary CreateDefault()
    {
        var library = new PatternLibrary();

        library.AddPattern(new PatternInfo
        {
            Id = "SimpleHttpClient",
            Description = "HTTP client with retry and circuit breaker",
            UseCases = ["API calls", "Web requests", "Webhooks"],
            Keywords = ["http", "api", "request", "fetch", "call", "web"],
            Integrations = ["http"]
        });

        library.AddPattern(new PatternInfo
        {
            Id = "SimpleDatabase",
            Description = "Database operations with transactions",
            UseCases = ["SQL queries", "Data storage", "Transactions"],
            Keywords = ["database", "sql", "query", "insert", "update", "delete", "data"],
            Integrations = ["postgresql", "mysql", "sqlserver"]
        });

        library.AddPattern(new PatternInfo
        {
            Id = "SimpleEmail",
            Description = "Email sending with templates",
            UseCases = ["Notifications", "Reports", "Alerts"],
            Keywords = ["email", "mail", "send", "notify", "notification", "alert"],
            Integrations = ["email"]
        });

        library.AddPattern(new PatternInfo
        {
            Id = "SimpleScheduler",
            Description = "Job scheduling with cron expressions",
            UseCases = ["Periodic tasks", "Scheduled jobs", "Automation"],
            Keywords = ["schedule", "cron", "daily", "hourly", "periodic", "timer"],
            Integrations = []
        });

        library.AddPattern(new PatternInfo
        {
            Id = "SimpleStorage",
            Description = "File storage operations",
            UseCases = ["File uploads", "Backups", "Data export"],
            Keywords = ["file", "upload", "download", "storage", "backup", "s3", "blob"],
            Integrations = ["s3"]
        });

        library.AddPattern(new PatternInfo
        {
            Id = "SimpleNotification",
            Description = "Multi-channel notifications",
            UseCases = ["Alerts", "Updates", "User notifications"],
            Keywords = ["notify", "alert", "message", "slack", "teams"],
            Integrations = ["slack", "email"]
        });

        library.AddPattern(new PatternInfo
        {
            Id = "SimpleRetryPattern",
            Description = "Retry with exponential backoff",
            UseCases = ["Error recovery", "Resilience", "Fault tolerance"],
            Keywords = ["retry", "error", "fail", "recover", "resilient"],
            Integrations = []
        });

        library.AddPattern(new PatternInfo
        {
            Id = "SimpleCachePattern",
            Description = "Caching for performance",
            UseCases = ["Performance", "Rate limiting", "Data caching"],
            Keywords = ["cache", "performance", "fast", "memory"],
            Integrations = []
        });

        return library;
    }
}

/// <summary>
/// Pattern information
/// </summary>
public sealed class PatternInfo
{
    public string Id { get; init; } = "";
    public string Description { get; init; } = "";
    public List<string> UseCases { get; init; } = new();
    public List<string> Keywords { get; init; } = new();
    public List<string> Integrations { get; init; } = new();
}
