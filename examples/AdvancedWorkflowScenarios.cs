// Loco Advanced Workflow Scenarios
// Real-world examples combining multiple templates and integrations
// Demonstrates enterprise automation patterns

using Loco.Core.Workflows;
using Loco.Core.Integrations;
using System.Text.Json;

namespace Loco.Examples;

/// <summary>
/// Scenario 1: E-Commerce Order Processing Pipeline
/// Combines: Stripe payment → Database → Email → Slack → Error tracking
/// Business Value: Complete order fulfillment automation
/// </summary>
public class ECommerceOrderPipeline
{
    private readonly IntegrationRegistry _integrations;
    private readonly VisualWorkflowEngine _engine;

    public ECommerceOrderPipeline()
    {
        _integrations = new IntegrationRegistry();
        _engine = new VisualWorkflowEngine(Console.WriteLine);

        // Setup integrations
        _integrations.Register("stripe", new StripeIntegration(Environment.GetEnvironmentVariable("STRIPE_KEY")!));
        _integrations.Register("redis", new RedisIntegration("localhost:6379"));
        _integrations.Register("db", new DatabaseIntegration(() => new SqliteConnection("orders.db")));
        _integrations.Register("sendgrid", new SendGridIntegration(Environment.GetEnvironmentVariable("SENDGRID_KEY")!));
        _integrations.Register("slack", new SlackIntegration(Environment.GetEnvironmentVariable("SLACK_WEBHOOK")!));

        RegisterHandlers();
    }

    public async Task<WorkflowExecutionContext> ProcessOrderAsync(OrderRequest order)
    {
        var workflow = new VisualWorkflowBuilder()
            .WithName("E-Commerce Order Processing")
            .WithDescription("Complete order fulfillment from payment to shipping")

            // 1. Validate order in cache (fast lookup)
            .AddNode("Check Cache", "action", "redis", "get", new()
            {
                ["key"] = $"order:{order.OrderId}"
            })

            // 2. Check for duplicate order
            .AddNode("Validate Duplicate", "condition", "condition", "evaluate", new()
            {
                ["left"] = "{{nodes.CheckCache.data}}",
                ["operation"] = "equals",
                ["right"] = null
            })

            // 3. Process Stripe payment
            .AddNode("Process Payment", "action", "stripe", "create_payment", new()
            {
                ["amount"] = order.TotalAmount.ToString(),
                ["currency"] = "usd",
                ["customer_id"] = order.CustomerId,
                ["description"] = $"Order #{order.OrderId}"
            })

            // 4. Cache order (prevent duplicates)
            .AddNode("Cache Order", "action", "redis", "set", new()
            {
                ["key"] = $"order:{order.OrderId}",
                ["value"] = JsonSerializer.Serialize(order),
                ["ttl"] = 86400 // 24 hours
            })

            // 5. Save to database
            .AddNode("Save Order", "action", "database", "execute", new()
            {
                ["sql"] = @"
                    INSERT INTO orders (id, customer_id, total_amount, payment_id, status, created_at)
                    VALUES (@id, @customer_id, @total, @payment_id, 'processing', NOW())
                ",
                ["id"] = order.OrderId,
                ["customer_id"] = order.CustomerId,
                ["total"] = order.TotalAmount,
                ["payment_id"] = "{{nodes.ProcessPayment.data.id}}"
            })

            // 6. Send confirmation email
            .AddNode("Send Email", "action", "sendgrid", "send", new()
            {
                ["from"] = "orders@shop.com",
                ["to"] = order.CustomerEmail,
                ["subject"] = $"Order #{order.OrderId} Confirmed",
                ["html"] = $@"
                    <h1>Thank you for your order!</h1>
                    <p>Order ID: {order.OrderId}</p>
                    <p>Total: ${order.TotalAmount}</p>
                    <p>We'll notify you when it ships.</p>
                "
            })

            // 7. Notify fulfillment team
            .AddNode("Notify Team", "action", "slack", "send", new()
            {
                ["channel"] = "#fulfillment",
                ["text"] = $"New order #{order.OrderId} - ${order.TotalAmount}",
                ["attachments"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["color"] = "good",
                        ["fields"] = new[]
                        {
                            new { title = "Customer", value = order.CustomerEmail, @short = true },
                            new { title = "Amount", value = $"${order.TotalAmount}", @short = true },
                            new { title = "Items", value = order.Items.Count.ToString(), @short = true }
                        }
                    }
                }
            })

            // 8. Error handling - refund on failure
            .AddNode("Refund Payment", "action", "stripe", "refund", new()
            {
                ["payment_id"] = "{{nodes.ProcessPayment.data.id}}"
            })

            .AddNode("Log Error", "action", "database", "execute", new()
            {
                ["sql"] = @"
                    INSERT INTO order_errors (order_id, error_message, created_at)
                    VALUES (@order_id, @error, NOW())
                ",
                ["order_id"] = order.OrderId,
                ["error"] = "{{$error.message}}"
            })

            // Connections
            .Connect("Check Cache", "Validate Duplicate")
            .Connect("Validate Duplicate", "Process Payment", "success")
            .Connect("Process Payment", "Cache Order")
            .Connect("Cache Order", "Save Order")
            .Connect("Save Order", "Send Email")
            .Connect("Send Email", "Notify Team")

            // Error path
            .Connect("Process Payment", "Refund Payment", "error")
            .Connect("Refund Payment", "Log Error")

            .Build();

        return await _engine.ExecuteAsync(workflow);
    }

    private void RegisterHandlers()
    {
        _engine.RegisterNodeHandler("redis:get", async (node, ctx) =>
        {
            var redis = _integrations.Get("redis")!;
            var result = await redis.ExecuteAsync(new IntegrationRequest
            {
                Action = "get",
                Parameters = node.Parameters
            });
            return result.Data;
        });

        _engine.RegisterNodeHandler("redis:set", async (node, ctx) =>
        {
            var redis = _integrations.Get("redis")!;
            var result = await redis.ExecuteAsync(new IntegrationRequest
            {
                Action = "set",
                Parameters = node.Parameters
            });
            return result.Success;
        });

        _engine.RegisterNodeHandler("stripe:create_payment", async (node, ctx) =>
        {
            var stripe = _integrations.Get("stripe")!;
            var result = await stripe.ExecuteAsync(new IntegrationRequest
            {
                Action = "create_payment",
                Parameters = node.Parameters
            });
            return result.Data;
        });

        _engine.RegisterNodeHandler("database:execute", async (node, ctx) =>
        {
            var db = _integrations.Get("db")!;
            var result = await db.ExecuteAsync(new IntegrationRequest
            {
                Action = "execute",
                Parameters = node.Parameters
            });
            return result.Success;
        });

        _engine.RegisterNodeHandler("sendgrid:send", async (node, ctx) =>
        {
            var sendgrid = _integrations.Get("sendgrid")!;
            var result = await sendgrid.ExecuteAsync(new IntegrationRequest
            {
                Parameters = node.Parameters
            });
            return result.Success;
        });

        _engine.RegisterNodeHandler("slack:send", async (node, ctx) =>
        {
            var slack = _integrations.Get("slack")!;
            var result = await slack.ExecuteAsync(new IntegrationRequest
            {
                Parameters = node.Parameters
            });
            return result.Success;
        });
    }
}

/// <summary>
/// Scenario 2: SaaS Customer Lifecycle Automation
/// Combines: Stripe subscription → Customer onboarding → Redis → Google Sheets reporting
/// Business Value: Complete customer journey automation
/// </summary>
public class SaaSCustomerLifecycle
{
    private readonly IntegrationRegistry _integrations;
    private readonly VisualWorkflowEngine _engine;

    public SaaSCustomerLifecycle()
    {
        _integrations = new IntegrationRegistry();
        _engine = new VisualWorkflowEngine(Console.WriteLine);

        _integrations.Register("stripe", new StripeIntegration(Environment.GetEnvironmentVariable("STRIPE_KEY")!));
        _integrations.Register("db", new DatabaseIntegration(() => new SqliteConnection("customers.db")));
        _integrations.Register("sendgrid", new SendGridIntegration(Environment.GetEnvironmentVariable("SENDGRID_KEY")!));
        _integrations.Register("slack", new SlackIntegration(Environment.GetEnvironmentVariable("SLACK_WEBHOOK")!));
        _integrations.Register("sheets", new GoogleSheetsIntegration(Environment.GetEnvironmentVariable("GOOGLE_API_KEY")!));
        _integrations.Register("redis", new RedisIntegration("localhost:6379"));

        RegisterHandlers();
    }

    public async Task<WorkflowExecutionContext> OnboardCustomerAsync(CustomerSignup signup)
    {
        // Use template as base, then extend
        var baseWorkflow = WorkflowTemplates.CustomerOnboarding();

        var workflow = new VisualWorkflowBuilder()
            .WithName("SaaS Customer Lifecycle - Onboarding")
            .WithDescription("Complete customer onboarding with subscription management")

            // Start with customer data
            .AddNode("Validate Signup", "transform", "transform", "json", new()
            {
                ["mappings"] = new Dictionary<string, string>
                {
                    ["email"] = signup.Email,
                    ["name"] = signup.Name,
                    ["plan"] = signup.PlanId,
                    ["company"] = signup.CompanyName
                }
            })

            // Create Stripe customer
            .AddNode("Create Stripe Customer", "action", "stripe", "create_customer", new()
            {
                ["email"] = signup.Email,
                ["name"] = signup.Name
            })

            // Start subscription
            .AddNode("Create Subscription", "action", "stripe", "create_subscription", new()
            {
                ["customer_id"] = "{{nodes.CreateStripeCustomer.data.id}}",
                ["price_id"] = signup.PlanId
            })

            // Cache customer session
            .AddNode("Cache Session", "action", "redis", "set", new()
            {
                ["key"] = $"session:{signup.Email}",
                ["value"] = JsonSerializer.Serialize(new {
                    customerId = "{{nodes.CreateStripeCustomer.data.id}}",
                    subscriptionId = "{{nodes.CreateSubscription.data.id}}",
                    plan = signup.PlanId
                }),
                ["ttl"] = 3600 // 1 hour
            })

            // Save to database
            .AddNode("Save Customer", "action", "database", "execute", new()
            {
                ["sql"] = @"
                    INSERT INTO customers (
                        email, name, company, plan_id,
                        stripe_customer_id, stripe_subscription_id,
                        status, created_at
                    ) VALUES (
                        @email, @name, @company, @plan,
                        @stripe_cust, @stripe_sub,
                        'active', NOW()
                    )
                ",
                ["email"] = signup.Email,
                ["name"] = signup.Name,
                ["company"] = signup.CompanyName,
                ["plan"] = signup.PlanId,
                ["stripe_cust"] = "{{nodes.CreateStripeCustomer.data.id}}",
                ["stripe_sub"] = "{{nodes.CreateSubscription.data.id}}"
            })

            // Send welcome email
            .AddNode("Send Welcome", "action", "sendgrid", "send", new()
            {
                ["from"] = "welcome@saas.com",
                ["to"] = signup.Email,
                ["subject"] = "Welcome to Our Platform!",
                ["html"] = $@"
                    <h1>Welcome {signup.Name}!</h1>
                    <p>Your {signup.PlanId} subscription is now active.</p>
                    <h2>Getting Started:</h2>
                    <ol>
                        <li><a href='https://app.saas.com/dashboard'>Access your dashboard</a></li>
                        <li><a href='https://app.saas.com/docs'>Read documentation</a></li>
                        <li><a href='https://app.saas.com/support'>Contact support</a></li>
                    </ol>
                "
            })

            // Notify sales team
            .AddNode("Notify Sales", "action", "slack", "send", new()
            {
                ["channel"] = "#sales",
                ["text"] = $"New customer: {signup.Name} ({signup.CompanyName}) - {signup.PlanId} plan"
            })

            // Log to Google Sheets for reporting
            .AddNode("Log to Sheets", "action", "sheets", "append", new()
            {
                ["spreadsheet_id"] = Environment.GetEnvironmentVariable("SHEETS_ID"),
                ["range"] = "Customers!A:F",
                ["values"] = new[]
                {
                    new[] {
                        DateTime.UtcNow.ToString("yyyy-MM-dd"),
                        signup.Name,
                        signup.Email,
                        signup.CompanyName,
                        signup.PlanId,
                        "Active"
                    }
                }
            })

            // Schedule follow-up (7 days)
            .AddNode("Schedule Follow-up", "action", "scheduler", "schedule_once", new()
            {
                ["delay"] = 604800, // 7 days
                ["webhook_url"] = "https://api.saas.com/webhooks/followup",
                ["payload"] = new { email = signup.Email, type = "onboarding_check" }
            })

            .Connect("Validate Signup", "Create Stripe Customer")
            .Connect("Create Stripe Customer", "Create Subscription")
            .Connect("Create Subscription", "Cache Session")
            .Connect("Cache Session", "Save Customer")
            .Connect("Save Customer", "Send Welcome")
            .Connect("Send Welcome", "Notify Sales")
            .Connect("Notify Sales", "Log to Sheets")
            .Connect("Log to Sheets", "Schedule Follow-up")

            .Build();

        return await _engine.ExecuteAsync(workflow);
    }

    private void RegisterHandlers()
    {
        // Similar to ECommerceOrderPipeline handlers
        // Implementation omitted for brevity
    }
}

/// <summary>
/// Scenario 3: DevOps Incident Response Pipeline
/// Combines: Error tracking → GitHub → Telegram → PagerDuty → Compliance logging
/// Business Value: Automated incident management and compliance
/// </summary>
public class DevOpsIncidentResponse
{
    private readonly IntegrationRegistry _integrations;
    private readonly VisualWorkflowEngine _engine;

    public DevOpsIncidentResponse()
    {
        _integrations = new IntegrationRegistry();
        _engine = new VisualWorkflowEngine(Console.WriteLine);

        _integrations.Register("db", new DatabaseIntegration(() => new SqliteConnection("logs.db")));
        _integrations.Register("github", new GitHubIntegration(Environment.GetEnvironmentVariable("GITHUB_TOKEN")!));
        _integrations.Register("telegram", new TelegramIntegration(Environment.GetEnvironmentVariable("TELEGRAM_TOKEN")!));
        _integrations.Register("slack", new SlackIntegration(Environment.GetEnvironmentVariable("SLACK_WEBHOOK")!));
        _integrations.Register("s3", new S3Integration(
            Environment.GetEnvironmentVariable("AWS_ACCESS_KEY")!,
            Environment.GetEnvironmentVariable("AWS_SECRET_KEY")!,
            "incident-logs",
            "us-east-1"
        ));

        RegisterHandlers();
    }

    public async Task<WorkflowExecutionContext> HandleIncidentAsync(IncidentData incident)
    {
        // Use error tracking template as base
        var baseWorkflow = WorkflowTemplates.ErrorTracking();

        var workflow = new VisualWorkflowBuilder()
            .WithName("DevOps Incident Response")
            .WithDescription("Automated incident detection, notification, and compliance logging")

            // 1. Classify incident severity
            .AddNode("Classify Severity", "condition", "condition", "evaluate", new()
            {
                ["left"] = incident.ErrorCount.ToString(),
                ["operation"] = "greater_than",
                ["right"] = "10"
            })

            // 2. Critical path - immediate escalation
            .AddNode("Send Critical Alert", "action", "telegram", "send_message", new()
            {
                ["chat_id"] = Environment.GetEnvironmentVariable("TELEGRAM_ONCALL_CHAT"),
                ["text"] = $@"
🚨 CRITICAL INCIDENT 🚨

Service: {incident.ServiceName}
Error Count: {incident.ErrorCount}
Time Window: Last 5 minutes

First Error: {incident.FirstError}

Action Required: Immediate investigation
                ",
                ["parse_mode"] = "HTML"
            })

            // 3. Create GitHub incident issue
            .AddNode("Create GitHub Issue", "action", "github", "create_issue", new()
            {
                ["owner"] = "myorg",
                ["repo"] = "incidents",
                ["title"] = $"[INCIDENT] {incident.ServiceName} - {incident.ErrorCount} errors",
                ["body"] = $@"
## Incident Details

**Service**: {incident.ServiceName}
**Severity**: {(incident.ErrorCount > 10 ? "Critical" : "High")}
**Error Count**: {incident.ErrorCount}
**Time Window**: {incident.TimeWindow}

## First Error
```
{incident.FirstError}
```

## Stack Trace
```
{incident.StackTrace}
```

## Next Steps
- [ ] Investigate root cause
- [ ] Deploy fix
- [ ] Verify resolution
- [ ] Update runbook
                ",
                ["labels"] = new[] { "incident", "production", incident.ErrorCount > 10 ? "critical" : "high" }
            })

            // 4. Normal path - team notification
            .AddNode("Send Team Alert", "action", "slack", "send", new()
            {
                ["channel"] = "#incidents",
                ["text"] = "New incident detected",
                ["attachments"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["color"] = "warning",
                        ["title"] = $"Incident: {incident.ServiceName}",
                        ["fields"] = new[]
                        {
                            new { title = "Error Count", value = incident.ErrorCount.ToString(), @short = true },
                            new { title = "Severity", value = incident.ErrorCount > 10 ? "Critical" : "High", @short = true },
                            new { title = "GitHub Issue", value = "{{nodes.CreateGitHubIssue.data.html_url}}", @short = false }
                        }
                    }
                }
            })

            // 5. Log to database
            .AddNode("Log Incident", "action", "database", "execute", new()
            {
                ["sql"] = @"
                    INSERT INTO incidents (
                        service_name, error_count, severity,
                        first_error, stack_trace, github_issue_id,
                        created_at, status
                    ) VALUES (
                        @service, @count, @severity,
                        @error, @stack, @github_id,
                        NOW(), 'open'
                    )
                ",
                ["service"] = incident.ServiceName,
                ["count"] = incident.ErrorCount,
                ["severity"] = incident.ErrorCount > 10 ? "critical" : "high",
                ["error"] = incident.FirstError,
                ["stack"] = incident.StackTrace,
                ["github_id"] = "{{nodes.CreateGitHubIssue.data.number}}"
            })

            // 6. Archive to S3 for compliance
            .AddNode("Archive to S3", "action", "s3", "upload", new()
            {
                ["key"] = $"incidents/{DateTime.UtcNow:yyyy-MM-dd}/{incident.ServiceName}-{DateTime.UtcNow:HHmmss}.json",
                ["content"] = JsonSerializer.Serialize(incident),
                ["content_type"] = "application/json",
                ["metadata"] = new Dictionary<string, string>
                {
                    ["service"] = incident.ServiceName,
                    ["severity"] = incident.ErrorCount > 10 ? "critical" : "high",
                    ["timestamp"] = DateTime.UtcNow.ToString("O")
                }
            })

            // 7. Update metrics
            .AddNode("Update Metrics", "action", "http", "post", new()
            {
                ["url"] = "https://metrics.company.com/api/incidents",
                ["headers"] = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {Environment.GetEnvironmentVariable("METRICS_TOKEN")}"
                },
                ["body"] = new
                {
                    service = incident.ServiceName,
                    error_count = incident.ErrorCount,
                    severity = incident.ErrorCount > 10 ? "critical" : "high",
                    timestamp = DateTime.UtcNow
                }
            })

            .Connect("Classify Severity", "Send Critical Alert", "success") // Critical path
            .Connect("Classify Severity", "Send Team Alert", "error") // Normal path
            .Connect("Send Critical Alert", "Create GitHub Issue")
            .Connect("Send Team Alert", "Create GitHub Issue")
            .Connect("Create GitHub Issue", "Log Incident")
            .Connect("Log Incident", "Archive to S3")
            .Connect("Archive to S3", "Update Metrics")

            .Build();

        return await _engine.ExecuteAsync(workflow);
    }

    private void RegisterHandlers()
    {
        // Handler implementations
    }
}

/// <summary>
/// Scenario 4: Marketing Campaign Automation
/// Combines: Social media monitoring → AI analysis → Multi-channel response → Analytics
/// Business Value: Real-time brand engagement and sentiment tracking
/// </summary>
public class MarketingCampaignAutomation
{
    private readonly IntegrationRegistry _integrations;
    private readonly VisualWorkflowEngine _engine;

    public MarketingCampaignAutomation()
    {
        _integrations = new IntegrationRegistry();
        _engine = new VisualWorkflowEngine(Console.WriteLine);

        // Setup all required integrations
        _integrations.Register("http", new HttpIntegration("https://api.twitter.com"));
        _integrations.Register("openai", new OpenAIProvider(Environment.GetEnvironmentVariable("OPENAI_KEY")!));
        _integrations.Register("discord", new DiscordIntegration(Environment.GetEnvironmentVariable("DISCORD_WEBHOOK")!));
        _integrations.Register("db", new DatabaseIntegration(() => new SqliteConnection("marketing.db")));
        _integrations.Register("sheets", new GoogleSheetsIntegration(Environment.GetEnvironmentVariable("GOOGLE_API_KEY")!));
        _integrations.Register("slack", new SlackIntegration(Environment.GetEnvironmentVariable("SLACK_WEBHOOK")!));

        RegisterHandlers();
    }

    public async Task<WorkflowExecutionContext> MonitorCampaignAsync(string campaignHashtag)
    {
        // Extends social media monitoring template
        var workflow = new VisualWorkflowBuilder()
            .WithName("Marketing Campaign Monitor")
            .WithDescription("Real-time campaign monitoring with AI-powered engagement")

            // 1. Search for campaign mentions
            .AddNode("Search Twitter", "action", "http", "get", new()
            {
                ["url"] = "https://api.twitter.com/2/tweets/search/recent",
                ["headers"] = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {Environment.GetEnvironmentVariable("TWITTER_TOKEN")}"
                },
                ["query"] = new Dictionary<string, string>
                {
                    ["query"] = $"#{campaignHashtag} -is:retweet",
                    ["max_results"] = "100",
                    ["tweet.fields"] = "created_at,author_id,public_metrics"
                }
            })

            // 2. AI sentiment analysis
            .AddNode("Analyze Sentiment", "action", "openai", "completion", new()
            {
                ["model"] = "gpt-4",
                ["prompt"] = @"
                    Analyze these tweets for sentiment and engagement potential.
                    Return JSON array with: {tweet_id, sentiment, score, engagement_worthy, suggested_response}

                    Tweets: {{JSON.stringify(nodes.SearchTwitter.data)}}
                ",
                ["temperature"] = 0.3
            })

            // 3. Filter high-engagement opportunities
            .AddNode("Filter High Value", "transform", "transform", "filter", new()
            {
                ["script"] = @"
                    return $input.filter(tweet =>
                        tweet.engagement_worthy === true &&
                        tweet.sentiment !== 'negative'
                    );
                "
            })

            // 4. Log all mentions to database
            .AddNode("Log Mentions", "action", "database", "execute", new()
            {
                ["sql"] = @"
                    INSERT INTO campaign_mentions (
                        campaign, tweet_id, author_id, text,
                        sentiment, score, created_at
                    ) VALUES (?, ?, ?, ?, ?, ?, ?)
                ",
                ["batch"] = true
            })

            // 5. Update Google Sheets dashboard
            .AddNode("Update Dashboard", "action", "sheets", "append", new()
            {
                ["spreadsheet_id"] = Environment.GetEnvironmentVariable("CAMPAIGN_SHEET_ID"),
                ["range"] = "Mentions!A:G",
                ["values"] = "{{nodes.AnalyzeSentiment.data.map(t => [Date.now(), t.tweet_id, t.sentiment, t.score])}}"
            })

            // 6. Notify team of high-value engagements
            .AddNode("Notify Team", "action", "discord", "send", new()
            {
                ["webhook_url"] = Environment.GetEnvironmentVariable("DISCORD_MARKETING_WEBHOOK"),
                ["content"] = $"High-value engagement opportunities for #{campaignHashtag}",
                ["embeds"] = "{{nodes.FilterHighValue.data.map(t => ({ title: 'Opportunity', description: t.text, color: 3066993 }))}}"
            })

            // 7. Calculate campaign metrics
            .AddNode("Calculate Metrics", "transform", "transform", "aggregate", new()
            {
                ["script"] = @"
                    const mentions = $input;
                    return {
                        total_mentions: mentions.length,
                        positive: mentions.filter(m => m.sentiment === 'positive').length,
                        neutral: mentions.filter(m => m.sentiment === 'neutral').length,
                        negative: mentions.filter(m => m.sentiment === 'negative').length,
                        avg_score: mentions.reduce((sum, m) => sum + m.score, 0) / mentions.length,
                        engagement_opportunities: mentions.filter(m => m.engagement_worthy).length
                    };
                "
            })

            // 8. Send summary to Slack
            .AddNode("Send Summary", "action", "slack", "send", new()
            {
                ["channel"] = "#marketing",
                ["text"] = $"Campaign Update: #{campaignHashtag}",
                ["attachments"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["color"] = "good",
                        ["title"] = "Campaign Metrics (Last Hour)",
                        ["fields"] = new[]
                        {
                            new { title = "Total Mentions", value = "{{nodes.CalculateMetrics.data.total_mentions}}", @short = true },
                            new { title = "Avg Sentiment", value = "{{nodes.CalculateMetrics.data.avg_score}}", @short = true },
                            new { title = "Positive", value = "{{nodes.CalculateMetrics.data.positive}}", @short = true },
                            new { title = "Engagement Opps", value = "{{nodes.CalculateMetrics.data.engagement_opportunities}}", @short = true }
                        }
                    }
                }
            })

            .Connect("Search Twitter", "Analyze Sentiment")
            .Connect("Analyze Sentiment", "Filter High Value")
            .Connect("Filter High Value", "Log Mentions")
            .Connect("Log Mentions", "Update Dashboard")
            .Connect("Update Dashboard", "Notify Team")
            .Connect("Analyze Sentiment", "Calculate Metrics")
            .Connect("Calculate Metrics", "Send Summary")

            .Build();

        return await _engine.ExecuteAsync(workflow);
    }

    private void RegisterHandlers()
    {
        // Handler implementations
    }
}

// Supporting data models

public record OrderRequest(
    string OrderId,
    string CustomerId,
    string CustomerEmail,
    decimal TotalAmount,
    List<OrderItem> Items
);

public record OrderItem(string ProductId, int Quantity, decimal Price);

public record CustomerSignup(
    string Email,
    string Name,
    string CompanyName,
    string PlanId
);

public record IncidentData(
    string ServiceName,
    int ErrorCount,
    string TimeWindow,
    string FirstError,
    string StackTrace
);
