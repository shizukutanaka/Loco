// Loco Workflow Templates
// Pre-built workflow templates for common use cases
// Inspired by n8n's template library and Zapier's Zaps

namespace Loco.Core.Workflows;

/// <summary>
/// Template library - pre-built workflows for common automation tasks
/// </summary>
public static class WorkflowTemplates
{
    /// <summary>
    /// Template 1: Database Backup to Email
    /// Backs up database and emails the export file
    /// </summary>
    public static VisualWorkflow DatabaseBackupToEmail()
    {
        return new VisualWorkflowBuilder()
            .WithName("Database Backup to Email")
            .WithDescription("Export database data and send via email daily")
            .AddNode("Schedule", "trigger", "scheduler", "cron", new Dictionary<string, object>
            {
                ["schedule"] = "0 0 * * *", // Daily at midnight
                ["timezone"] = "UTC"
            })
            .AddNode("Export DB", "action", "database", "query", new Dictionary<string, object>
            {
                ["sql"] = "SELECT * FROM critical_data WHERE updated_at > DATE_SUB(NOW(), INTERVAL 1 DAY)",
                ["format"] = "csv"
            })
            .AddNode("Send Email", "action", "email", "send", new Dictionary<string, object>
            {
                ["to"] = "admin@company.com",
                ["subject"] = "Daily Database Backup - {{date}}",
                ["body"] = "Attached is the daily database backup.",
                ["attachments"] = "{{nodes.ExportDB.data}}"
            })
            .Connect("Schedule", "Export DB")
            .Connect("Export DB", "Send Email")
            .Build();
    }

    /// <summary>
    /// Template 2: Slack Alert on API Error
    /// Monitor API endpoint and alert Slack on failures
    /// </summary>
    public static VisualWorkflow ApiHealthCheckToSlack()
    {
        return new VisualWorkflowBuilder()
            .WithName("API Health Check with Slack Alerts")
            .WithDescription("Monitor API health and notify team on failures")
            .AddNode("Schedule", "trigger", "scheduler", "interval", new Dictionary<string, object>
            {
                ["interval"] = 300, // Every 5 minutes
                ["unit"] = "seconds"
            })
            .AddNode("Check API", "action", "http", "get", new Dictionary<string, object>
            {
                ["url"] = "https://api.company.com/health",
                ["timeout"] = 10
            })
            .AddNode("Check Status", "condition", "condition", "evaluate", new Dictionary<string, object>
            {
                ["left"] = "{{nodes.CheckAPI.statusCode}}",
                ["operation"] = "not_equals",
                ["right"] = 200
            })
            .AddNode("Alert Slack", "action", "slack", "send", new Dictionary<string, object>
            {
                ["channel"] = "#alerts",
                ["text"] = "🚨 API Health Check Failed!",
                ["attachments"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["color"] = "danger",
                        ["title"] = "API Status",
                        ["fields"] = new[]
                        {
                            new { title = "Status Code", value = "{{nodes.CheckAPI.statusCode}}", @short = true },
                            new { title = "Timestamp", value = "{{$now}}", @short = true }
                        }
                    }
                }
            })
            .AddNode("Log Success", "action", "variable", "set", new Dictionary<string, object>
            {
                ["name"] = "lastHealthCheck",
                ["value"] = "{{$now}}"
            })
            .Connect("Schedule", "Check API")
            .Connect("Check API", "Check Status")
            .Connect("Check Status", "Alert Slack", "error")
            .Connect("Check Status", "Log Success", "success")
            .Build();
    }

    /// <summary>
    /// Template 3: GitHub Issue to Slack
    /// Forward new GitHub issues to Slack channel
    /// </summary>
    public static VisualWorkflow GitHubIssueToSlack()
    {
        return new VisualWorkflowBuilder()
            .WithName("GitHub Issue to Slack Notification")
            .WithDescription("Post new GitHub issues to Slack automatically")
            .AddNode("Webhook", "trigger", "webhook", "receive", new Dictionary<string, object>
            {
                ["path"] = "/webhooks/github-issues",
                ["method"] = "POST"
            })
            .AddNode("Parse Issue", "transform", "transform", "json", new Dictionary<string, object>
            {
                ["mappings"] = new Dictionary<string, string>
                {
                    ["issueNumber"] = "{{$webhook.body.issue.number}}",
                    ["issueTitle"] = "{{$webhook.body.issue.title}}",
                    ["issueUrl"] = "{{$webhook.body.issue.html_url}}",
                    ["author"] = "{{$webhook.body.issue.user.login}}",
                    ["labels"] = "{{$webhook.body.issue.labels}}"
                }
            })
            .AddNode("Post to Slack", "action", "slack", "send", new Dictionary<string, object>
            {
                ["channel"] = "#github-issues",
                ["text"] = "New GitHub Issue Created",
                ["attachments"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["color"] = "good",
                        ["title"] = "#{{nodes.ParseIssue.issueNumber}}: {{nodes.ParseIssue.issueTitle}}",
                        ["title_link"] = "{{nodes.ParseIssue.issueUrl}}",
                        ["author_name"] = "{{nodes.ParseIssue.author}}",
                        ["fields"] = new[]
                        {
                            new { title = "Labels", value = "{{nodes.ParseIssue.labels}}" }
                        }
                    }
                }
            })
            .Connect("Webhook", "Parse Issue")
            .Connect("Parse Issue", "Post to Slack")
            .Build();
    }

    /// <summary>
    /// Template 4: Data ETL Pipeline
    /// Extract data from API, transform, and load into database
    /// </summary>
    public static VisualWorkflow DataETLPipeline()
    {
        return new VisualWorkflowBuilder()
            .WithName("API to Database ETL Pipeline")
            .WithDescription("Extract data from external API and load into database")
            .AddNode("Schedule", "trigger", "scheduler", "cron", new Dictionary<string, object>
            {
                ["schedule"] = "0 */6 * * *", // Every 6 hours
                ["timezone"] = "UTC"
            })
            .AddNode("Fetch Data", "action", "http", "get", new Dictionary<string, object>
            {
                ["url"] = "https://api.external.com/data",
                ["headers"] = new Dictionary<string, string>
                {
                    ["Authorization"] = "Bearer {{$env.API_TOKEN}}"
                }
            })
            .AddNode("Transform", "transform", "transform", "map", new Dictionary<string, object>
            {
                ["script"] = @"
                    const items = $input.data.items;
                    return items.map(item => ({
                        external_id: item.id,
                        name: item.name,
                        value: parseFloat(item.value),
                        imported_at: new Date().toISOString()
                    }));
                "
            })
            .AddNode("Load to DB", "action", "database", "execute", new Dictionary<string, object>
            {
                ["sql"] = @"
                    INSERT INTO external_data (external_id, name, value, imported_at)
                    VALUES (@external_id, @name, @value, @imported_at)
                    ON CONFLICT (external_id) DO UPDATE SET
                        name = EXCLUDED.name,
                        value = EXCLUDED.value,
                        imported_at = EXCLUDED.imported_at
                ",
                ["batch"] = true
            })
            .AddNode("Log Success", "action", "slack", "send", new Dictionary<string, object>
            {
                ["channel"] = "#data-pipeline",
                ["text"] = "✅ ETL pipeline completed: {{nodes.Transform.length}} records processed"
            })
            .Connect("Schedule", "Fetch Data")
            .Connect("Fetch Data", "Transform")
            .Connect("Transform", "Load to DB")
            .Connect("Load to DB", "Log Success")
            .Build();
    }

    /// <summary>
    /// Template 5: AI Content Moderation
    /// Use AI to moderate user-generated content
    /// </summary>
    public static VisualWorkflow AIContentModeration()
    {
        return new VisualWorkflowBuilder()
            .WithName("AI-Powered Content Moderation")
            .WithDescription("Automatically moderate user content using AI")
            .AddNode("Webhook", "trigger", "webhook", "receive", new Dictionary<string, object>
            {
                ["path"] = "/webhooks/new-content",
                ["method"] = "POST"
            })
            .AddNode("Extract Content", "transform", "transform", "json", new Dictionary<string, object>
            {
                ["mappings"] = new Dictionary<string, string>
                {
                    ["contentId"] = "{{$webhook.body.id}}",
                    ["text"] = "{{$webhook.body.text}}",
                    ["userId"] = "{{$webhook.body.user_id}}"
                }
            })
            .AddNode("AI Moderation", "action", "openai", "completion", new Dictionary<string, object>
            {
                ["model"] = "gpt-4",
                ["prompt"] = @"
                    Analyze the following user-generated content for inappropriate material.
                    Return a JSON object with: {score: 0-100, safe: boolean, reasons: [string]}

                    Content: {{nodes.ExtractContent.text}}
                ",
                ["temperature"] = 0.3
            })
            .AddNode("Check Score", "condition", "condition", "evaluate", new Dictionary<string, object>
            {
                ["left"] = "{{nodes.AIModeration.data.score}}",
                ["operation"] = "greater_than",
                ["right"] = 70
            })
            .AddNode("Flag Content", "action", "database", "execute", new Dictionary<string, object>
            {
                ["sql"] = "UPDATE content SET status = 'flagged', moderation_score = @score WHERE id = @id",
                ["score"] = "{{nodes.AIModeration.data.score}}",
                ["id"] = "{{nodes.ExtractContent.contentId}}"
            })
            .AddNode("Notify Moderators", "action", "slack", "send", new Dictionary<string, object>
            {
                ["channel"] = "#moderation",
                ["text"] = "⚠️ Content flagged for review (Score: {{nodes.AIModeration.data.score}})"
            })
            .AddNode("Approve Content", "action", "database", "execute", new Dictionary<string, object>
            {
                ["sql"] = "UPDATE content SET status = 'approved' WHERE id = @id",
                ["id"] = "{{nodes.ExtractContent.contentId}}"
            })
            .Connect("Webhook", "Extract Content")
            .Connect("Extract Content", "AI Moderation")
            .Connect("AI Moderation", "Check Score")
            .Connect("Check Score", "Flag Content", "error")
            .Connect("Flag Content", "Notify Moderators")
            .Connect("Check Score", "Approve Content", "success")
            .Build();
    }

    /// <summary>
    /// Template 6: Multi-Channel Notification
    /// Send notifications via Email, Slack, and SMS simultaneously
    /// </summary>
    public static VisualWorkflow MultiChannelNotification()
    {
        return new VisualWorkflowBuilder()
            .WithName("Multi-Channel Alert System")
            .WithDescription("Send critical alerts via multiple channels")
            .AddNode("Trigger", "trigger", "webhook", "receive", new Dictionary<string, object>
            {
                ["path"] = "/webhooks/alert",
                ["method"] = "POST"
            })
            .AddNode("Parse Alert", "transform", "transform", "json", new Dictionary<string, object>
            {
                ["mappings"] = new Dictionary<string, string>
                {
                    ["severity"] = "{{$webhook.body.severity}}",
                    ["message"] = "{{$webhook.body.message}}",
                    ["source"] = "{{$webhook.body.source}}"
                }
            })
            .AddNode("Send Email", "action", "email", "send", new Dictionary<string, object>
            {
                ["to"] = "ops-team@company.com",
                ["subject"] = "[{{nodes.ParseAlert.severity}}] Alert from {{nodes.ParseAlert.source}}",
                ["body"] = "{{nodes.ParseAlert.message}}"
            })
            .AddNode("Send Slack", "action", "slack", "send", new Dictionary<string, object>
            {
                ["channel"] = "#alerts",
                ["text"] = "🚨 [{{nodes.ParseAlert.severity}}] {{nodes.ParseAlert.message}}"
            })
            .AddNode("Send SMS", "action", "twilio", "sms", new Dictionary<string, object>
            {
                ["to"] = "+1234567890",
                ["body"] = "[ALERT] {{nodes.ParseAlert.message}}"
            })
            .AddNode("Log Alert", "action", "database", "execute", new Dictionary<string, object>
            {
                ["sql"] = "INSERT INTO alerts (severity, message, source, created_at) VALUES (@severity, @message, @source, NOW())",
                ["severity"] = "{{nodes.ParseAlert.severity}}",
                ["message"] = "{{nodes.ParseAlert.message}}",
                ["source"] = "{{nodes.ParseAlert.source}}"
            })
            .Connect("Trigger", "Parse Alert")
            .Connect("Parse Alert", "Send Email")
            .Connect("Parse Alert", "Send Slack")
            .Connect("Parse Alert", "Send SMS")
            .Connect("Send Email", "Log Alert")
            .Connect("Send Slack", "Log Alert")
            .Connect("Send SMS", "Log Alert")
            .Build();
    }

    /// <summary>
    /// Get all available templates
    /// </summary>
    public static List<WorkflowTemplateMetadata> GetAllTemplates()
    {
        return new List<WorkflowTemplateMetadata>
        {
            new()
            {
                Id = "database-backup-email",
                Name = "Database Backup to Email",
                Description = "Export database data and send via email daily",
                Category = "Data Management",
                Tags = new() { "database", "email", "backup", "scheduled" },
                Difficulty = "Easy",
                EstimatedSetupTime = "5 minutes",
                Factory = DatabaseBackupToEmail
            },
            new()
            {
                Id = "api-health-slack",
                Name = "API Health Check with Slack Alerts",
                Description = "Monitor API health and notify team on failures",
                Category = "Monitoring",
                Tags = new() { "api", "health", "slack", "monitoring" },
                Difficulty = "Easy",
                EstimatedSetupTime = "3 minutes",
                Factory = ApiHealthCheckToSlack
            },
            new()
            {
                Id = "github-issue-slack",
                Name = "GitHub Issue to Slack",
                Description = "Post new GitHub issues to Slack automatically",
                Category = "Development",
                Tags = new() { "github", "slack", "webhook", "collaboration" },
                Difficulty = "Medium",
                EstimatedSetupTime = "10 minutes",
                Factory = GitHubIssueToSlack
            },
            new()
            {
                Id = "data-etl-pipeline",
                Name = "API to Database ETL Pipeline",
                Description = "Extract data from external API and load into database",
                Category = "Data Integration",
                Tags = new() { "etl", "api", "database", "data-pipeline" },
                Difficulty = "Medium",
                EstimatedSetupTime = "15 minutes",
                Factory = DataETLPipeline
            },
            new()
            {
                Id = "ai-content-moderation",
                Name = "AI-Powered Content Moderation",
                Description = "Automatically moderate user content using AI",
                Category = "AI/ML",
                Tags = new() { "ai", "openai", "moderation", "content" },
                Difficulty = "Advanced",
                EstimatedSetupTime = "20 minutes",
                Factory = AIContentModeration
            },
            new()
            {
                Id = "multi-channel-notification",
                Name = "Multi-Channel Alert System",
                Description = "Send critical alerts via multiple channels",
                Category = "Notifications",
                Tags = new() { "alerts", "multi-channel", "email", "slack", "sms" },
                Difficulty = "Easy",
                EstimatedSetupTime = "10 minutes",
                Factory = MultiChannelNotification
            }
        };
    }

    /// <summary>
    /// Get template by ID
    /// </summary>
    public static VisualWorkflow? GetTemplateById(string templateId)
    {
        var template = GetAllTemplates().FirstOrDefault(t => t.Id == templateId);
        return template?.Factory();
    }

    /// <summary>
    /// Search templates by category or tag
    /// </summary>
    public static List<WorkflowTemplateMetadata> SearchTemplates(string? category = null, string? tag = null)
    {
        var templates = GetAllTemplates();

        if (!string.IsNullOrEmpty(category))
            templates = templates.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrEmpty(tag))
            templates = templates.Where(t => t.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();

        return templates;
    }
}

/// <summary>
/// Metadata for a workflow template
/// </summary>
public class WorkflowTemplateMetadata
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public string Difficulty { get; set; } = "Easy"; // Easy, Medium, Advanced
    public string EstimatedSetupTime { get; set; } = "";
    public Func<VisualWorkflow> Factory { get; set; } = null!;
}
