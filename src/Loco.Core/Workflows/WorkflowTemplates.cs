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
    /// Template 7: Social Media Monitoring
    /// Monitor Twitter/social feeds for brand mentions and respond via Discord
    /// </summary>
    public static VisualWorkflow SocialMediaMonitoring()
    {
        return new VisualWorkflowBuilder()
            .WithName("Social Media Brand Monitoring")
            .WithDescription("Track brand mentions across social media and notify team")
            .AddNode("Schedule", "trigger", "scheduler", "interval", new Dictionary<string, object>
            {
                ["interval"] = 300, // Every 5 minutes
                ["unit"] = "seconds"
            })
            .AddNode("Search Twitter", "action", "http", "get", new Dictionary<string, object>
            {
                ["url"] = "https://api.twitter.com/2/tweets/search/recent",
                ["headers"] = new Dictionary<string, string>
                {
                    ["Authorization"] = "Bearer {{$env.TWITTER_TOKEN}}"
                },
                ["query"] = new Dictionary<string, string>
                {
                    ["query"] = "{{$env.BRAND_NAME}} -is:retweet",
                    ["max_results"] = "10"
                }
            })
            .AddNode("Filter New", "transform", "transform", "filter", new Dictionary<string, object>
            {
                ["script"] = @"
                    const lastCheck = $context.lastCheckTime || new Date(Date.now() - 600000);
                    return $input.data.filter(tweet =>
                        new Date(tweet.created_at) > lastCheck
                    );
                "
            })
            .AddNode("Analyze Sentiment", "action", "openai", "completion", new Dictionary<string, object>
            {
                ["model"] = "gpt-3.5-turbo",
                ["prompt"] = @"
                    Analyze the sentiment of this tweet: {{nodes.FilterNew.text}}
                    Return JSON: {sentiment: 'positive'|'negative'|'neutral', score: 0-100}
                ",
                ["temperature"] = 0.3
            })
            .AddNode("Post to Discord", "action", "discord", "send", new Dictionary<string, object>
            {
                ["webhook_url"] = "{{$env.DISCORD_WEBHOOK}}",
                ["content"] = "New Brand Mention Detected!",
                ["embeds"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["title"] = "Twitter Mention",
                        ["description"] = "{{nodes.FilterNew.text}}",
                        ["color"] = "{{nodes.AnalyzeSentiment.sentiment === 'positive' ? 3066993 : 15158332}}",
                        ["fields"] = new[]
                        {
                            new { name = "Sentiment", value = "{{nodes.AnalyzeSentiment.sentiment}}", inline = true },
                            new { name = "Score", value = "{{nodes.AnalyzeSentiment.score}}", inline = true },
                            new { name = "Author", value = "{{nodes.FilterNew.author_id}}", inline = true }
                        },
                        ["url"] = "https://twitter.com/user/status/{{nodes.FilterNew.id}}"
                    }
                }
            })
            .AddNode("Log Mention", "action", "database", "execute", new Dictionary<string, object>
            {
                ["sql"] = @"
                    INSERT INTO social_mentions (platform, tweet_id, text, sentiment, score, created_at)
                    VALUES ('twitter', @tweet_id, @text, @sentiment, @score, @created_at)
                ",
                ["tweet_id"] = "{{nodes.FilterNew.id}}",
                ["text"] = "{{nodes.FilterNew.text}}",
                ["sentiment"] = "{{nodes.AnalyzeSentiment.sentiment}}",
                ["score"] = "{{nodes.AnalyzeSentiment.score}}",
                ["created_at"] = "{{nodes.FilterNew.created_at}}"
            })
            .Connect("Schedule", "Search Twitter")
            .Connect("Search Twitter", "Filter New")
            .Connect("Filter New", "Analyze Sentiment")
            .Connect("Analyze Sentiment", "Post to Discord")
            .Connect("Post to Discord", "Log Mention")
            .Build();
    }

    /// <summary>
    /// Template 8: Customer Onboarding
    /// Automated welcome sequence for new customers
    /// </summary>
    public static VisualWorkflow CustomerOnboarding()
    {
        return new VisualWorkflowBuilder()
            .WithName("Automated Customer Onboarding")
            .WithDescription("Send welcome emails and setup tasks for new customers")
            .AddNode("Webhook", "trigger", "webhook", "receive", new Dictionary<string, object>
            {
                ["path"] = "/webhooks/new-customer",
                ["method"] = "POST"
            })
            .AddNode("Extract Customer", "transform", "transform", "json", new Dictionary<string, object>
            {
                ["mappings"] = new Dictionary<string, string>
                {
                    ["customerId"] = "{{$webhook.body.customer_id}}",
                    ["email"] = "{{$webhook.body.email}}",
                    ["name"] = "{{$webhook.body.name}}",
                    ["plan"] = "{{$webhook.body.plan}}"
                }
            })
            .AddNode("Create Customer Record", "action", "database", "execute", new Dictionary<string, object>
            {
                ["sql"] = @"
                    INSERT INTO customers (id, email, name, plan, status, created_at)
                    VALUES (@id, @email, @name, @plan, 'onboarding', NOW())
                ",
                ["id"] = "{{nodes.ExtractCustomer.customerId}}",
                ["email"] = "{{nodes.ExtractCustomer.email}}",
                ["name"] = "{{nodes.ExtractCustomer.name}}",
                ["plan"] = "{{nodes.ExtractCustomer.plan}}"
            })
            .AddNode("Send Welcome Email", "action", "sendgrid", "send", new Dictionary<string, object>
            {
                ["to"] = "{{nodes.ExtractCustomer.email}}",
                ["from"] = "welcome@company.com",
                ["subject"] = "Welcome to Our Platform, {{nodes.ExtractCustomer.name}}!",
                ["html"] = @"
                    <h1>Welcome {{nodes.ExtractCustomer.name}}!</h1>
                    <p>We're excited to have you on the {{nodes.ExtractCustomer.plan}} plan.</p>
                    <p>Here are your next steps:</p>
                    <ul>
                        <li>Complete your profile</li>
                        <li>Set up your first project</li>
                        <li>Invite your team</li>
                    </ul>
                    <a href='https://app.company.com/onboarding'>Get Started</a>
                "
            })
            .AddNode("Notify Sales Team", "action", "slack", "send", new Dictionary<string, object>
            {
                ["channel"] = "#sales",
                ["text"] = "New customer onboarded!",
                ["attachments"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["color"] = "good",
                        ["fields"] = new[]
                        {
                            new { title = "Name", value = "{{nodes.ExtractCustomer.name}}", @short = true },
                            new { title = "Plan", value = "{{nodes.ExtractCustomer.plan}}", @short = true },
                            new { title = "Email", value = "{{nodes.ExtractCustomer.email}}", @short = false }
                        }
                    }
                }
            })
            .AddNode("Schedule Follow-up", "action", "scheduler", "schedule_once", new Dictionary<string, object>
            {
                ["delay"] = 86400, // 24 hours
                ["webhook_url"] = "{{$env.APP_URL}}/webhooks/onboarding-followup",
                ["payload"] = new Dictionary<string, object>
                {
                    ["customer_id"] = "{{nodes.ExtractCustomer.customerId}}",
                    ["email"] = "{{nodes.ExtractCustomer.email}}"
                }
            })
            .AddNode("Create Onboarding Tasks", "action", "database", "execute", new Dictionary<string, object>
            {
                ["sql"] = @"
                    INSERT INTO tasks (customer_id, title, status, created_at)
                    VALUES
                        (@customer_id, 'Complete profile', 'pending', NOW()),
                        (@customer_id, 'Create first project', 'pending', NOW()),
                        (@customer_id, 'Invite team members', 'pending', NOW())
                ",
                ["customer_id"] = "{{nodes.ExtractCustomer.customerId}}"
            })
            .Connect("Webhook", "Extract Customer")
            .Connect("Extract Customer", "Create Customer Record")
            .Connect("Create Customer Record", "Send Welcome Email")
            .Connect("Create Customer Record", "Notify Sales Team")
            .Connect("Create Customer Record", "Schedule Follow-up")
            .Connect("Create Customer Record", "Create Onboarding Tasks")
            .Build();
    }

    /// <summary>
    /// Template 9: Error Tracking and Alerting
    /// Monitor application logs for errors and alert team
    /// </summary>
    public static VisualWorkflow ErrorTracking()
    {
        return new VisualWorkflowBuilder()
            .WithName("Application Error Tracking")
            .WithDescription("Monitor logs, track errors, and alert development team")
            .AddNode("Schedule", "trigger", "scheduler", "interval", new Dictionary<string, object>
            {
                ["interval"] = 60, // Every minute
                ["unit"] = "seconds"
            })
            .AddNode("Query Logs", "action", "database", "query", new Dictionary<string, object>
            {
                ["sql"] = @"
                    SELECT id, level, message, stack_trace, user_id, created_at
                    FROM application_logs
                    WHERE level IN ('ERROR', 'FATAL')
                      AND created_at > DATE_SUB(NOW(), INTERVAL 1 MINUTE)
                      AND notified = FALSE
                    ORDER BY created_at DESC
                "
            })
            .AddNode("Check for Errors", "condition", "condition", "evaluate", new Dictionary<string, object>
            {
                ["left"] = "{{nodes.QueryLogs.length}}",
                ["operation"] = "greater_than",
                ["right"] = 0
            })
            .AddNode("Group by Error Type", "transform", "transform", "aggregate", new Dictionary<string, object>
            {
                ["script"] = @"
                    const errors = $input;
                    const grouped = {};
                    errors.forEach(error => {
                        const key = error.message.substring(0, 100);
                        if (!grouped[key]) {
                            grouped[key] = { count: 0, errors: [], first: error };
                        }
                        grouped[key].count++;
                        grouped[key].errors.push(error);
                    });
                    return Object.values(grouped);
                "
            })
            .AddNode("Check Error Frequency", "condition", "condition", "evaluate", new Dictionary<string, object>
            {
                ["left"] = "{{nodes.GroupByErrorType[0].count}}",
                ["operation"] = "greater_than",
                ["right"] = 5 // Alert if same error occurs 5+ times
            })
            .AddNode("Send High Priority Alert", "action", "telegram", "send_message", new Dictionary<string, object>
            {
                ["chat_id"] = "{{$env.TELEGRAM_CHAT_ID}}",
                ["text"] = @"
🚨 HIGH FREQUENCY ERROR DETECTED 🚨

Error: {{nodes.GroupByErrorType[0].first.message}}
Occurrences: {{nodes.GroupByErrorType[0].count}} times in 1 minute

Stack trace: {{nodes.GroupByErrorType[0].first.stack_trace}}
                ",
                ["parse_mode"] = "HTML"
            })
            .AddNode("Send Normal Alert", "action", "slack", "send", new Dictionary<string, object>
            {
                ["channel"] = "#errors",
                ["text"] = "New application errors detected",
                ["attachments"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["color"] = "danger",
                        ["title"] = "Error Summary",
                        ["fields"] = new[]
                        {
                            new { title = "Total Errors", value = "{{nodes.QueryLogs.length}}", @short = true },
                            new { title = "Unique Types", value = "{{nodes.GroupByErrorType.length}}", @short = true },
                            new { title = "Most Common", value = "{{nodes.GroupByErrorType[0].first.message}}", @short = false }
                        }
                    }
                }
            })
            .AddNode("Create GitHub Issue", "action", "github", "create_issue", new Dictionary<string, object>
            {
                ["owner"] = "{{$env.GITHUB_OWNER}}",
                ["repo"] = "{{$env.GITHUB_REPO}}",
                ["title"] = "[ERROR] {{nodes.GroupByErrorType[0].first.message}}",
                ["body"] = @"
## Error Details
**Message:** {{nodes.GroupByErrorType[0].first.message}}
**Occurrences:** {{nodes.GroupByErrorType[0].count}}
**First Seen:** {{nodes.GroupByErrorType[0].first.created_at}}

## Stack Trace
```
{{nodes.GroupByErrorType[0].first.stack_trace}}
```

## Affected Users
User ID: {{nodes.GroupByErrorType[0].first.user_id}}
                ",
                ["labels"] = new[] { "bug", "automated" }
            })
            .AddNode("Mark as Notified", "action", "database", "execute", new Dictionary<string, object>
            {
                ["sql"] = @"
                    UPDATE application_logs
                    SET notified = TRUE
                    WHERE id IN ({{nodes.QueryLogs.*.id}})
                "
            })
            .Connect("Schedule", "Query Logs")
            .Connect("Query Logs", "Check for Errors")
            .Connect("Check for Errors", "Group by Error Type", "success")
            .Connect("Group by Error Type", "Check Error Frequency")
            .Connect("Check Error Frequency", "Send High Priority Alert", "success")
            .Connect("Check Error Frequency", "Send Normal Alert", "error")
            .Connect("Send High Priority Alert", "Create GitHub Issue")
            .Connect("Send Normal Alert", "Mark as Notified")
            .Connect("Create GitHub Issue", "Mark as Notified")
            .Build();
    }

    /// <summary>
    /// Template 10: Compliance Reporting
    /// Generate and distribute compliance reports automatically
    /// </summary>
    public static VisualWorkflow ComplianceReporting()
    {
        return new VisualWorkflowBuilder()
            .WithName("Automated Compliance Reporting")
            .WithDescription("Generate monthly compliance reports and distribute to stakeholders")
            .AddNode("Schedule", "trigger", "scheduler", "cron", new Dictionary<string, object>
            {
                ["schedule"] = "0 0 1 * *", // First day of each month at midnight
                ["timezone"] = "UTC"
            })
            .AddNode("Calculate Metrics", "action", "database", "query", new Dictionary<string, object>
            {
                ["sql"] = @"
                    SELECT
                        COUNT(DISTINCT user_id) as total_users,
                        COUNT(*) as total_transactions,
                        SUM(CASE WHEN flagged = TRUE THEN 1 ELSE 0 END) as flagged_transactions,
                        SUM(CASE WHEN reviewed = TRUE THEN 1 ELSE 0 END) as reviewed_transactions,
                        AVG(review_time_hours) as avg_review_time
                    FROM transactions
                    WHERE created_at >= DATE_SUB(NOW(), INTERVAL 1 MONTH)
                "
            })
            .AddNode("Query Violations", "action", "database", "query", new Dictionary<string, object>
            {
                ["sql"] = @"
                    SELECT id, type, severity, description, resolved, created_at
                    FROM compliance_violations
                    WHERE created_at >= DATE_SUB(NOW(), INTERVAL 1 MONTH)
                    ORDER BY severity DESC, created_at DESC
                "
            })
            .AddNode("Query Access Logs", "action", "database", "query", new Dictionary<string, object>
            {
                ["sql"] = @"
                    SELECT
                        user_id,
                        resource,
                        COUNT(*) as access_count,
                        MAX(accessed_at) as last_access
                    FROM access_logs
                    WHERE accessed_at >= DATE_SUB(NOW(), INTERVAL 1 MONTH)
                    GROUP BY user_id, resource
                    HAVING access_count > 100
                    ORDER BY access_count DESC
                "
            })
            .AddNode("Generate Report with AI", "action", "claude", "completion", new Dictionary<string, object>
            {
                ["model"] = "claude-3-sonnet-20240229",
                ["prompt"] = @"
                    Generate a comprehensive compliance report based on the following data:

                    Metrics:
                    {{JSON.stringify(nodes.CalculateMetrics.data[0], null, 2)}}

                    Violations:
                    {{JSON.stringify(nodes.QueryViolations.data, null, 2)}}

                    High-Access Resources:
                    {{JSON.stringify(nodes.QueryAccessLogs.data, null, 2)}}

                    Format the report in HTML with:
                    1. Executive Summary
                    2. Key Metrics
                    3. Violations Analysis
                    4. Access Patterns
                    5. Recommendations

                    Make it professional and suitable for C-level executives.
                ",
                ["max_tokens"] = 4000
            })
            .AddNode("Upload to S3", "action", "s3", "upload", new Dictionary<string, object>
            {
                ["bucket"] = "{{$env.COMPLIANCE_BUCKET}}",
                ["key"] = "reports/compliance-{{$now.format('YYYY-MM')}}.html",
                ["content"] = "{{nodes.GenerateReportWithAI.content}}",
                ["content_type"] = "text/html",
                ["metadata"] = new Dictionary<string, string>
                {
                    ["report_type"] = "compliance",
                    ["period"] = "{{$now.format('YYYY-MM')}}"
                }
            })
            .AddNode("Send to Stakeholders", "action", "sendgrid", "send", new Dictionary<string, object>
            {
                ["to"] = new[] { "compliance@company.com", "ceo@company.com", "legal@company.com" },
                ["from"] = "reports@company.com",
                ["subject"] = "Monthly Compliance Report - {{$now.format('MMMM YYYY')}}",
                ["html"] = "{{nodes.GenerateReportWithAI.content}}",
                ["attachments"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["filename"] = "compliance-report-{{$now.format('YYYY-MM')}}.html",
                        ["content"] = "{{nodes.GenerateReportWithAI.content}}",
                        ["type"] = "text/html"
                    }
                }
            })
            .AddNode("Post Summary to Slack", "action", "slack", "send", new Dictionary<string, object>
            {
                ["channel"] = "#compliance",
                ["text"] = "Monthly compliance report generated",
                ["attachments"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["color"] = "good",
                        ["title"] = "Compliance Report - {{$now.format('MMMM YYYY')}}",
                        ["fields"] = new[]
                        {
                            new { title = "Total Transactions", value = "{{nodes.CalculateMetrics.data[0].total_transactions}}", @short = true },
                            new { title = "Violations", value = "{{nodes.QueryViolations.length}}", @short = true },
                            new { title = "Avg Review Time", value = "{{nodes.CalculateMetrics.data[0].avg_review_time}} hours", @short = true },
                            new { title = "Report URL", value = "{{nodes.UploadToS3.url}}", @short = false }
                        }
                    }
                }
            })
            .AddNode("Log Report Generation", "action", "database", "execute", new Dictionary<string, object>
            {
                ["sql"] = @"
                    INSERT INTO compliance_reports (period, total_violations, total_transactions, report_url, created_at)
                    VALUES (@period, @violations, @transactions, @url, NOW())
                ",
                ["period"] = "{{$now.format('YYYY-MM')}}",
                ["violations"] = "{{nodes.QueryViolations.length}}",
                ["transactions"] = "{{nodes.CalculateMetrics.data[0].total_transactions}}",
                ["url"] = "{{nodes.UploadToS3.url}}"
            })
            .Connect("Schedule", "Calculate Metrics")
            .Connect("Schedule", "Query Violations")
            .Connect("Schedule", "Query Access Logs")
            .Connect("Calculate Metrics", "Generate Report with AI")
            .Connect("Query Violations", "Generate Report with AI")
            .Connect("Query Access Logs", "Generate Report with AI")
            .Connect("Generate Report with AI", "Upload to S3")
            .Connect("Upload to S3", "Send to Stakeholders")
            .Connect("Send to Stakeholders", "Post Summary to Slack")
            .Connect("Post Summary to Slack", "Log Report Generation")
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
            },
            new()
            {
                Id = "social-media-monitoring",
                Name = "Social Media Brand Monitoring",
                Description = "Track brand mentions across social media and notify team",
                Category = "Marketing",
                Tags = new() { "social-media", "twitter", "discord", "ai", "sentiment" },
                Difficulty = "Medium",
                EstimatedSetupTime = "15 minutes",
                Factory = SocialMediaMonitoring
            },
            new()
            {
                Id = "customer-onboarding",
                Name = "Automated Customer Onboarding",
                Description = "Send welcome emails and setup tasks for new customers",
                Category = "Customer Success",
                Tags = new() { "onboarding", "email", "sendgrid", "slack", "automation" },
                Difficulty = "Medium",
                EstimatedSetupTime = "12 minutes",
                Factory = CustomerOnboarding
            },
            new()
            {
                Id = "error-tracking",
                Name = "Application Error Tracking",
                Description = "Monitor logs, track errors, and alert development team",
                Category = "DevOps",
                Tags = new() { "errors", "monitoring", "telegram", "github", "logging" },
                Difficulty = "Advanced",
                EstimatedSetupTime = "18 minutes",
                Factory = ErrorTracking
            },
            new()
            {
                Id = "compliance-reporting",
                Name = "Automated Compliance Reporting",
                Description = "Generate monthly compliance reports and distribute to stakeholders",
                Category = "Compliance",
                Tags = new() { "compliance", "reporting", "ai", "s3", "sendgrid" },
                Difficulty = "Advanced",
                EstimatedSetupTime = "25 minutes",
                Factory = ComplianceReporting
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
