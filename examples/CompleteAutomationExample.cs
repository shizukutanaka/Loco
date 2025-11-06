// Complete Loco Automation Example
// Demonstrates integration of all major features:
// - Visual Workflows
// - Pre-built Integrations (HTTP, Database, Email, Slack, GitHub)
// - AI Integration (OpenAI, Claude)
// - Practical Patterns (Logger, Cache, Monitoring)

using Loco.Core.Workflows;
using Loco.Core.Integrations;
using Loco.Core.AI;
using Loco.Core.Practical;
using System.Data.SQLite;

namespace Loco.Examples;

/// <summary>
/// Complete automation system combining all Loco features
/// Use Case: AI-powered content pipeline with multi-channel notifications
/// </summary>
public class CompleteAutomationExample
{
    private readonly VisualWorkflowEngine _workflowEngine;
    private readonly IntegrationRegistry _integrations;
    private readonly AIOrchestrator _aiOrchestrator;
    private readonly SimpleLogger _logger;
    private readonly SimpleCache<object> _cache;
    private readonly SimpleMonitor _monitor;

    public CompleteAutomationExample(
        string openAiKey,
        string claudeKey,
        string slackWebhook,
        string githubToken,
        string gmailAppPassword)
    {
        // Setup logging and monitoring
        _logger = SimpleLoggerFactory.GetLogger("AutomationSystem");
        _monitor = new SimpleMonitor();
        _cache = new SimpleCache<object>(maxSize: 10000);

        // Setup AI providers
        _aiOrchestrator = new AIOrchestrator();
        _aiOrchestrator.RegisterProvider("openai", new OpenAIProvider(openAiKey));
        _aiOrchestrator.RegisterProvider("claude", new ClaudeProvider(claudeKey));

        // Setup integrations
        _integrations = new IntegrationRegistry();
        _integrations.Register("http", new HttpIntegration("https://api.github.com", new Dictionary<string, string>
        {
            ["Authorization"] = $"token {githubToken}",
            ["User-Agent"] = "Loco-Automation"
        }));
        _integrations.Register("database", new DatabaseIntegration(
            () => new SQLiteConnection("Data Source=automation.db"),
            "SQLite"
        ));
        _integrations.Register("email", EmailIntegration.Gmail("automation@company.com", gmailAppPassword));
        _integrations.Register("slack", new SlackIntegration(slackWebhook));
        _integrations.Register("github", new GitHubIntegration(githubToken));

        // Setup workflow engine
        _workflowEngine = new VisualWorkflowEngine(_logger.Info);
        RegisterWorkflowHandlers();

        _logger.Info("Automation system initialized", new {
            integrations = _integrations.GetRegisteredIntegrations().Count(),
            aiProviders = 2
        });
    }

    /// <summary>
    /// Example 1: AI-Powered GitHub Issue Triage
    /// Automatically categorize and prioritize GitHub issues using AI
    /// </summary>
    public async Task<WorkflowExecutionContext> RunIssueTriageAsync(string owner, string repo)
    {
        _logger.Info("Starting issue triage workflow", new { owner, repo });
        _monitor.Increment("workflows.issue_triage.started");

        var workflow = new VisualWorkflowBuilder()
            .WithName("AI-Powered Issue Triage")
            .WithDescription("Fetch GitHub issues, analyze with AI, categorize and notify team")

            // Step 1: Fetch open issues from GitHub
            .AddNode("Fetch Issues", "action", "github", "list_issues", new()
            {
                ["owner"] = owner,
                ["repo"] = repo,
                ["state"] = "open"
            })

            // Step 2: AI analysis of each issue
            .AddNode("Analyze with AI", "action", "openai", "analyze", new()
            {
                ["model"] = "gpt-4",
                ["prompt"] = @"Analyze this GitHub issue and provide:
                    1. Category (bug, feature, documentation, question)
                    2. Priority (low, medium, high, critical)
                    3. Estimated complexity (simple, moderate, complex)
                    4. Suggested labels

                    Return JSON format.
                    Issue: {{nodes.FetchIssues.data}}"
            })

            // Step 3: Store analysis in database
            .AddNode("Store Analysis", "action", "database", "execute", new()
            {
                ["sql"] = @"INSERT INTO issue_analysis
                    (issue_number, category, priority, complexity, labels, analyzed_at)
                    VALUES (@number, @category, @priority, @complexity, @labels, @timestamp)"
            })

            // Step 4: Check if critical priority
            .AddNode("Check Priority", "condition", "condition", "evaluate", new()
            {
                ["left"] = "{{nodes.AnalyzeWithAI.data.priority}}",
                ["operation"] = "equals",
                ["right"] = "critical"
            })

            // Step 5a: Alert team for critical issues
            .AddNode("Alert Slack", "action", "slack", "send", new()
            {
                ["channel"] = "#critical-issues",
                ["text"] = "🚨 Critical Issue Detected",
                ["attachments"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["color"] = "danger",
                        ["title"] = "Issue #{{nodes.AnalyzeWithAI.data.issueNumber}}",
                        ["fields"] = new[]
                        {
                            new { title = "Category", value = "{{nodes.AnalyzeWithAI.data.category}}" },
                            new { title = "Complexity", value = "{{nodes.AnalyzeWithAI.data.complexity}}" }
                        }
                    }
                }
            })

            // Step 5b: Email summary for non-critical
            .AddNode("Email Summary", "action", "email", "send", new()
            {
                ["to"] = "team@company.com",
                ["subject"] = "Issue Analyzed: {{nodes.AnalyzeWithAI.data.category}}",
                ["body"] = "Issue analysis completed. Priority: {{nodes.AnalyzeWithAI.data.priority}}"
            })

            .Connect("Fetch Issues", "Analyze with AI")
            .Connect("Analyze with AI", "Store Analysis")
            .Connect("Store Analysis", "Check Priority")
            .Connect("Check Priority", "Alert Slack", "success")
            .Connect("Check Priority", "Email Summary", "error")
            .Build();

        var result = await _workflowEngine.ExecuteAsync(workflow);

        _monitor.Increment($"workflows.issue_triage.{result.Status.ToString().ToLower()}");
        _monitor.RecordMetric("workflows.issue_triage.duration", result.Duration.TotalMilliseconds);

        _logger.Info("Issue triage completed", new {
            status = result.Status,
            duration = result.Duration,
            nodesExecuted = result.NodeResults.Count
        });

        return result;
    }

    /// <summary>
    /// Example 2: Multi-AI Content Generation Pipeline
    /// Use both OpenAI and Claude for content creation and review
    /// </summary>
    public async Task<string> GenerateAndReviewContentAsync(string topic)
    {
        _logger.Info("Starting content generation", new { topic });
        _monitor.Increment("content_generation.started");

        using var timer = new PerformanceMonitor(_monitor).StartTimer("content_generation");

        // Step 1: Generate initial draft with OpenAI (fast)
        var draftResponse = await _aiOrchestrator.ExecuteAsync("openai", new AIRequest
        {
            Model = "gpt-4-turbo",
            Temperature = 0.8,
            MaxTokens = 1000,
            Messages = new List<AIMessage>
            {
                new() { Role = "system", Content = "You are a technical content writer." },
                new() { Role = "user", Content = $"Write a comprehensive blog post about: {topic}" }
            }
        });

        var draft = draftResponse.Content;
        _logger.Info("Draft generated", new { length = draft.Length, cost = draftResponse.Usage.EstimatedCost });

        // Step 2: Review and improve with Claude (quality)
        var reviewResponse = await _aiOrchestrator.ExecuteAsync("claude", new AIRequest
        {
            Model = "claude-3-sonnet-20240229",
            Temperature = 0.3,
            MaxTokens = 1500,
            Messages = new List<AIMessage>
            {
                new() { Role = "user", Content = $@"Review and improve this draft:

{draft}

Provide:
1. Quality score (0-100)
2. Suggestions for improvement
3. Revised version" }
            }
        });

        _logger.Info("Review completed", new { cost = reviewResponse.Usage.EstimatedCost });

        // Get statistics
        var stats = _aiOrchestrator.GetStats();
        _logger.Info("AI pipeline stats", new {
            totalRequests = stats.TotalRequests,
            totalCost = stats.TotalCost,
            totalTokens = stats.TotalTokens,
            avgDuration = stats.AverageDuration
        });

        _monitor.Increment("content_generation.completed");
        return reviewResponse.Content;
    }

    /// <summary>
    /// Example 3: Data Sync with Caching and Monitoring
    /// Sync external API data to database with intelligent caching
    /// </summary>
    public async Task<int> SyncExternalDataAsync(string apiUrl)
    {
        _logger.Info("Starting data sync", new { apiUrl });
        _monitor.Increment("data_sync.started");

        var cacheKey = $"api_data:{apiUrl}";

        // Check cache first
        var cached = _cache.Get(cacheKey);
        if (cached != null)
        {
            _monitor.Increment("data_sync.cache_hit");
            _logger.Info("Using cached data");
            return 0; // No sync needed
        }

        _monitor.Increment("data_sync.cache_miss");

        using var timer = new PerformanceMonitor(_monitor).StartTimer("data_sync");

        // Fetch data from API
        var http = _integrations.Get("http")!;
        var apiResult = await http.ExecuteAsync(new IntegrationRequest
        {
            Action = "GET",
            Parameters = new() { ["path"] = apiUrl }
        });

        if (!apiResult.Success)
        {
            _logger.Error("API fetch failed", new { error = apiResult.Error });
            _monitor.Increment("data_sync.failed");
            throw new Exception($"API fetch failed: {apiResult.Error}");
        }

        // Store in cache (5 minutes)
        _cache.Set(cacheKey, apiResult.Data!, TimeSpan.FromMinutes(5));

        // Parse and save to database
        var items = apiResult.Data as List<Dictionary<string, object>>;
        var db = _integrations.Get("database")!;

        int synced = 0;
        foreach (var item in items ?? new())
        {
            var dbResult = await db.ExecuteAsync(new IntegrationRequest
            {
                Action = "execute",
                Parameters = new()
                {
                    ["sql"] = "INSERT OR REPLACE INTO external_data (id, data, synced_at) VALUES (@id, @data, @timestamp)",
                    ["id"] = item.GetValueOrDefault("id"),
                    ["data"] = System.Text.Json.JsonSerializer.Serialize(item),
                    ["timestamp"] = DateTime.UtcNow
                }
            });

            if (dbResult.Success) synced++;
        }

        _monitor.RecordMetric("data_sync.items_synced", synced);
        _monitor.Increment("data_sync.completed");

        _logger.Info("Data sync completed", new { itemsSynced = synced, duration = timer });

        // Notify team
        var slack = _integrations.Get("slack")!;
        await slack.ExecuteAsync(new IntegrationRequest
        {
            Parameters = new()
            {
                ["text"] = $"✅ Data sync completed: {synced} items synced from {apiUrl}"
            }
        });

        return synced;
    }

    /// <summary>
    /// Example 4: Complete Workflow using Template
    /// Deploy a pre-built template with custom configuration
    /// </summary>
    public async Task<WorkflowExecutionContext> RunDatabaseBackupAsync()
    {
        _logger.Info("Starting database backup workflow");
        _monitor.Increment("workflows.backup.started");

        // Load template
        var workflow = WorkflowTemplates.GetTemplateById("database-backup-email");

        if (workflow == null)
        {
            throw new Exception("Template not found");
        }

        // Validate before execution
        var validator = new WorkflowValidator();
        var validation = validator.Validate(workflow);

        if (!validation.IsValid)
        {
            _logger.Error("Workflow validation failed", new { errors = validation.Errors });
            throw new InvalidOperationException($"Invalid workflow: {string.Join(", ", validation.Errors)}");
        }

        if (validation.Warnings.Any())
        {
            _logger.Warning("Workflow validation warnings", new { warnings = validation.Warnings });
        }

        // Execute
        var result = await _workflowEngine.ExecuteAsync(workflow);

        _monitor.Increment($"workflows.backup.{result.Status.ToString().ToLower()}");
        _monitor.RecordMetric("workflows.backup.duration", result.Duration.TotalMilliseconds);

        // Log execution details
        _logger.Info("Backup workflow completed", new
        {
            status = result.Status,
            duration = result.Duration,
            nodesExecuted = result.NodeResults.Count,
            success = result.Status == WorkflowExecutionStatus.Success
        });

        // Print execution log
        foreach (var logEntry in result.ExecutionLog)
        {
            Console.WriteLine(logEntry);
        }

        return result;
    }

    /// <summary>
    /// Example 5: Health Check and Monitoring Dashboard
    /// Test all integrations and report system health
    /// </summary>
    public async Task<SystemHealthReport> GetSystemHealthAsync()
    {
        _logger.Info("Performing system health check");

        var report = new SystemHealthReport
        {
            Timestamp = DateTime.UtcNow,
            MonitoringSnapshot = _monitor.GetSnapshot()
        };

        // Test all integrations
        var connectionTests = await _integrations.TestAllConnectionsAsync();
        report.IntegrationStatus = connectionTests;
        report.HealthyIntegrations = connectionTests.Count(kvp => kvp.Value);
        report.TotalIntegrations = connectionTests.Count;

        // Cache statistics
        report.CacheStats = new Dictionary<string, object>
        {
            ["size"] = _cache.Count,
            ["hitRate"] = CalculateCacheHitRate()
        };

        // AI usage statistics
        var aiStats = _aiOrchestrator.GetStats();
        report.AIStats = new Dictionary<string, object>
        {
            ["totalRequests"] = aiStats.TotalRequests,
            ["totalCost"] = aiStats.TotalCost,
            ["totalTokens"] = aiStats.TotalTokens,
            ["avgDuration"] = aiStats.AverageDuration.TotalMilliseconds
        };

        report.OverallHealth = report.HealthyIntegrations == report.TotalIntegrations ? "Healthy" : "Degraded";

        _logger.Info("Health check completed", new {
            health = report.OverallHealth,
            integrations = $"{report.HealthyIntegrations}/{report.TotalIntegrations}"
        });

        return report;
    }

    private void RegisterWorkflowHandlers()
    {
        // HTTP integration handler
        _workflowEngine.RegisterNodeHandler("http:get", async (node, context) =>
        {
            var http = _integrations.Get("http")!;
            var result = await http.ExecuteAsync(new IntegrationRequest
            {
                Action = "GET",
                Parameters = node.Parameters
            });
            return result.Data;
        });

        // Database integration handler
        _workflowEngine.RegisterNodeHandler("database:query", async (node, context) =>
        {
            var db = _integrations.Get("database")!;
            var result = await db.ExecuteAsync(new IntegrationRequest
            {
                Action = "query",
                Parameters = node.Parameters
            });
            return result.Data;
        });

        _workflowEngine.RegisterNodeHandler("database:execute", async (node, context) =>
        {
            var db = _integrations.Get("database")!;
            var result = await db.ExecuteAsync(new IntegrationRequest
            {
                Action = "execute",
                Parameters = node.Parameters
            });
            return result.Data;
        });

        // Email integration handler
        _workflowEngine.RegisterNodeHandler("email:send", async (node, context) =>
        {
            var email = _integrations.Get("email")!;
            var result = await email.ExecuteAsync(new IntegrationRequest
            {
                Parameters = node.Parameters
            });
            return result.Data;
        });

        // Slack integration handler
        _workflowEngine.RegisterNodeHandler("slack:send", async (node, context) =>
        {
            var slack = _integrations.Get("slack")!;
            var result = await slack.ExecuteAsync(new IntegrationRequest
            {
                Parameters = node.Parameters
            });
            return result.Data;
        });

        // GitHub integration handlers
        _workflowEngine.RegisterNodeHandler("github:list_issues", async (node, context) =>
        {
            var github = _integrations.Get("github")!;
            var result = await github.ExecuteAsync(new IntegrationRequest
            {
                Action = "list_issues",
                Parameters = node.Parameters
            });
            return result.Data;
        });

        _workflowEngine.RegisterNodeHandler("github:create_issue", async (node, context) =>
        {
            var github = _integrations.Get("github")!;
            var result = await github.ExecuteAsync(new IntegrationRequest
            {
                Action = "create_issue",
                Parameters = node.Parameters
            });
            return result.Data;
        });

        // AI integration handler (OpenAI)
        _workflowEngine.RegisterNodeHandler("openai:analyze", async (node, context) =>
        {
            var prompt = node.Parameters.GetValueOrDefault("prompt")?.ToString() ?? "";
            var model = node.Parameters.GetValueOrDefault("model")?.ToString() ?? "gpt-4";

            var response = await _aiOrchestrator.ExecuteAsync("openai", new AIRequest
            {
                Model = model,
                Messages = new List<AIMessage>
                {
                    new() { Role = "user", Content = prompt }
                }
            });

            return response.Content;
        });
    }

    private double CalculateCacheHitRate()
    {
        var snapshot = _monitor.GetSnapshot();
        var hits = snapshot.Counters.GetValueOrDefault("data_sync.cache_hit", 0);
        var misses = snapshot.Counters.GetValueOrDefault("data_sync.cache_miss", 0);
        var total = hits + misses;
        return total > 0 ? (double)hits / total : 0;
    }
}

public class SystemHealthReport
{
    public DateTime Timestamp { get; set; }
    public string OverallHealth { get; set; } = "Unknown";
    public Dictionary<string, bool> IntegrationStatus { get; set; } = new();
    public int HealthyIntegrations { get; set; }
    public int TotalIntegrations { get; set; }
    public Dictionary<string, object> CacheStats { get; set; } = new();
    public Dictionary<string, object> AIStats { get; set; } = new();
    public MonitoringSnapshot MonitoringSnapshot { get; set; } = null!;
}

/// <summary>
/// Console application entry point demonstrating all features
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Loco Complete Automation Example ===\n");

        // Initialize system (use environment variables for credentials)
        var automation = new CompleteAutomationExample(
            openAiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "your-key",
            claudeKey: Environment.GetEnvironmentVariable("CLAUDE_API_KEY") ?? "your-key",
            slackWebhook: Environment.GetEnvironmentVariable("SLACK_WEBHOOK") ?? "your-webhook",
            githubToken: Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "your-token",
            gmailAppPassword: Environment.GetEnvironmentVariable("GMAIL_APP_PASSWORD") ?? "your-password"
        );

        try
        {
            // Example 1: System health check
            Console.WriteLine("\n1. System Health Check");
            Console.WriteLine("=====================");
            var health = await automation.GetSystemHealthAsync();
            Console.WriteLine($"Overall Health: {health.OverallHealth}");
            Console.WriteLine($"Integrations: {health.HealthyIntegrations}/{health.TotalIntegrations} healthy");
            Console.WriteLine($"AI Requests: {health.AIStats["totalRequests"]}");
            Console.WriteLine($"AI Cost: ${health.AIStats["totalCost"]}");

            // Example 2: AI content generation
            Console.WriteLine("\n2. AI Content Generation");
            Console.WriteLine("========================");
            var content = await automation.GenerateAndReviewContentAsync("Microservices Architecture Best Practices");
            Console.WriteLine($"Generated {content.Length} characters of content");

            // Example 3: Data sync with caching
            Console.WriteLine("\n3. Data Synchronization");
            Console.WriteLine("=======================");
            var synced = await automation.SyncExternalDataAsync("/users");
            Console.WriteLine($"Synced {synced} records");

            // Example 4: GitHub issue triage
            Console.WriteLine("\n4. GitHub Issue Triage");
            Console.WriteLine("======================");
            var triageResult = await automation.RunIssueTriageAsync("loco-automation", "loco");
            Console.WriteLine($"Workflow Status: {triageResult.Status}");
            Console.WriteLine($"Nodes Executed: {triageResult.NodeResults.Count}");
            Console.WriteLine($"Duration: {triageResult.Duration.TotalSeconds:F2}s");

            // Example 5: Database backup workflow
            Console.WriteLine("\n5. Database Backup Workflow");
            Console.WriteLine("===========================");
            var backupResult = await automation.RunDatabaseBackupAsync();
            Console.WriteLine($"Backup Status: {backupResult.Status}");

            // Final health check
            Console.WriteLine("\n6. Final System Status");
            Console.WriteLine("======================");
            var finalHealth = await automation.GetSystemHealthAsync();
            Console.WriteLine($"Cache Hit Rate: {finalHealth.CacheStats["hitRate"]:P2}");
            Console.WriteLine($"Total Metrics: {finalHealth.MonitoringSnapshot.Metrics.Count}");
            Console.WriteLine($"Total Counters: {finalHealth.MonitoringSnapshot.Counters.Count}");

            Console.WriteLine("\n✅ All examples completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
