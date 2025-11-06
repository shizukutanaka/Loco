# Loco Pre-built Integrations

Pre-built connectors for common services - addressing the #2 competitive gap identified in our market analysis. These integrations allow you to connect to popular services without writing custom code.

## 🎯 Why Integrations Matter

**Market Context (2025)**:
- **Zapier**: 8,000+ integrations, 2.2M users
- **n8n**: 400+ integrations, 230K+ users
- **Make**: 2,800+ integrations

**Our Approach**: Start with the top 10 most-requested integrations, following the same lightweight philosophy as Practical Patterns.

## 📦 Available Integrations

### Core Integrations (Phase 1)

### 1. HTTP/REST API Integration

**Universal connector for any HTTP API.**

```csharp
// Setup
var http = new HttpIntegration("https://api.example.com", new Dictionary<string, string>
{
    ["Authorization"] = "Bearer your-token"
});

// GET request
var result = await http.ExecuteAsync(new IntegrationRequest
{
    Action = "GET",
    Parameters = new() { ["path"] = "/users/123" }
});

// POST request
var createResult = await http.ExecuteAsync(new IntegrationRequest
{
    Action = "POST",
    Parameters = new() { ["path"] = "/users" },
    Body = new { name = "John", email = "john@example.com" }
});

Console.WriteLine($"Status: {createResult.StatusCode}");
Console.WriteLine($"Data: {createResult.Data}");
Console.WriteLine($"Duration: {createResult.Duration.TotalMilliseconds}ms");
```

**Supported Methods**: GET, POST, PUT, PATCH, DELETE
**Features**: Custom headers, JSON body, automatic response parsing

### 2. Database Integration

**Generic SQL database connector - works with PostgreSQL, MySQL, SQLite, SQL Server.**

```csharp
// SQLite example
var db = new DatabaseIntegration(
    () => new SqliteConnection("Data Source=mydb.db"),
    "SQLite"
);

// Test connection
var connected = await db.TestConnectionAsync();
Console.WriteLine($"Connected: {connected}");

// Query data (SELECT)
var queryResult = await db.ExecuteAsync(new IntegrationRequest
{
    Action = "query",
    Parameters = new()
    {
        ["sql"] = "SELECT * FROM users WHERE active = @active",
        ["active"] = true
    }
});

var users = queryResult.Data as List<Dictionary<string, object?>>;
Console.WriteLine($"Found {users?.Count} users");

// Execute command (INSERT/UPDATE/DELETE)
var insertResult = await db.ExecuteAsync(new IntegrationRequest
{
    Action = "execute",
    Parameters = new()
    {
        ["sql"] = "INSERT INTO users (name, email) VALUES (@name, @email)",
        ["name"] = "Jane",
        ["email"] = "jane@example.com"
    }
});

Console.WriteLine($"Affected rows: {insertResult.Data}");
```

**Supported Databases**: PostgreSQL, MySQL, SQLite, SQL Server (any ADO.NET provider)
**Features**: Parameterized queries (SQL injection protection), query + execute actions, metadata tracking

### 3. Email Integration

**SMTP email sender with Gmail/Outlook presets.**

```csharp
// Gmail example (requires App Password)
var gmail = EmailIntegration.Gmail("your-email@gmail.com", "app-password");

// Outlook example
var outlook = EmailIntegration.Outlook("your-email@outlook.com", "password");

// Custom SMTP
var smtp = new EmailIntegration("smtp.example.com", 587, "user", "pass");

// Send email
var result = await gmail.ExecuteAsync(new IntegrationRequest
{
    Parameters = new()
    {
        ["to"] = "recipient@example.com",
        ["subject"] = "Hello from Loco",
        ["body"] = "This is an automated email from your workflow.",
        ["isHtml"] = false
    }
});

// Send HTML email with CC/BCC
var htmlResult = await gmail.ExecuteAsync(new IntegrationRequest
{
    Parameters = new()
    {
        ["to"] = "user1@example.com;user2@example.com",
        ["cc"] = "manager@example.com",
        ["bcc"] = "archive@example.com",
        ["subject"] = "Weekly Report",
        ["body"] = "<h1>Report</h1><p>Data here...</p>",
        ["isHtml"] = true
    }
});

Console.WriteLine($"Sent to {result.Metadata["recipientCount"]} recipients");
```

**Supported Providers**: Gmail, Outlook, any SMTP server
**Features**: HTML emails, CC/BCC, multiple recipients, SSL/TLS support

### 4. Slack Integration

**Send messages to Slack channels via webhooks.**

```csharp
// Setup (get webhook URL from Slack app settings)
var slack = new SlackIntegration("https://hooks.slack.com/services/YOUR/WEBHOOK/URL");

// Test connection
var connected = await slack.TestConnectionAsync();

// Send message
var result = await slack.ExecuteAsync(new IntegrationRequest
{
    Parameters = new()
    {
        ["text"] = "Deployment completed successfully!",
        ["channel"] = "#deployments",
        ["username"] = "Loco Bot",
        ["icon_emoji"] = ":rocket:"
    }
});

// Send alert
var alertResult = await slack.ExecuteAsync(new IntegrationRequest
{
    Parameters = new()
    {
        ["text"] = "⚠️ Error in production: Database connection timeout",
        ["channel"] = "#alerts"
    }
});

Console.WriteLine($"Message sent: {result.Success}");
```

**Features**: Custom channel, username, emoji, simple webhook-based (no OAuth complexity)

### 5. GitHub Integration

**Interact with GitHub repositories - create issues, PRs, check status.**

```csharp
// Setup (requires GitHub Personal Access Token)
var github = new GitHubIntegration("ghp_YourPersonalAccessToken");

// Test connection
var connected = await github.TestConnectionAsync();

// Get repository info
var repoResult = await github.ExecuteAsync(new IntegrationRequest
{
    Action = "get_repo",
    Parameters = new()
    {
        ["owner"] = "loco-automation",
        ["repo"] = "loco"
    }
});

// Create issue
var issueResult = await github.ExecuteAsync(new IntegrationRequest
{
    Action = "create_issue",
    Parameters = new()
    {
        ["owner"] = "loco-automation",
        ["repo"] = "loco",
        ["title"] = "Add new feature",
        ["body"] = "Description of the feature request...",
        ["labels"] = "enhancement,good-first-issue"
    }
});

// List open issues
var issuesResult = await github.ExecuteAsync(new IntegrationRequest
{
    Action = "list_issues",
    Parameters = new()
    {
        ["owner"] = "loco-automation",
        ["repo"] = "loco",
        ["state"] = "open"
    }
});

// Create pull request
var prResult = await github.ExecuteAsync(new IntegrationRequest
{
    Action = "create_pr",
    Parameters = new()
    {
        ["owner"] = "loco-automation",
        ["repo"] = "loco",
        ["title"] = "Fix bug in integration",
        ["body"] = "This PR fixes...",
        ["head"] = "feature-branch",
        ["base"] = "main"
    }
});

Console.WriteLine($"Issue created: #{issueResult.Data}");
```

**Supported Actions**: create_issue, list_issues, get_repo, create_pr
**Features**: Full GitHub API v3 support, automatic rate limiting handling

---

### Extended Integrations (Phase 2)

### 6. Discord Integration

**Send messages to Discord channels via webhooks.**

```csharp
// Setup (get webhook URL from Discord channel settings → Integrations)
var discord = new DiscordIntegration("https://discord.com/api/webhooks/YOUR/WEBHOOK/URL");

// Test connection
var connected = await discord.TestConnectionAsync();

// Send simple message
var result = await discord.ExecuteAsync(new IntegrationRequest
{
    Parameters = new()
    {
        ["content"] = "🚀 Deployment completed successfully!",
        ["username"] = "Loco Bot"
    }
});

// Send rich embed
var embedResult = await discord.ExecuteAsync(new IntegrationRequest
{
    Parameters = new()
    {
        ["content"] = "System Alert",
        ["embeds"] = new[]
        {
            new Dictionary<string, object>
            {
                ["title"] = "Error Detected",
                ["description"] = "Database connection timeout",
                ["color"] = 15158332, // Red color
                ["fields"] = new[]
                {
                    new { name = "Server", value = "prod-01", inline = true },
                    new { name = "Time", value = DateTime.UtcNow.ToString(), inline = true }
                }
            }
        }
    }
});

Console.WriteLine($"Message sent: {result.Success}");
```

**Features**: Simple text messages, rich embeds, custom username/avatar, TTS support

### 7. Twilio Integration

**Send SMS and make phone calls via Twilio API.**

```csharp
// Setup (requires Account SID, Auth Token, and a Twilio phone number)
var twilio = new TwilioIntegration(
    accountSid: "ACxxxxxxxxxx",
    authToken: "your-auth-token",
    fromNumber: "+1234567890"
);

// Test connection
var connected = await twilio.TestConnectionAsync();

// Send SMS
var smsResult = await twilio.ExecuteAsync(new IntegrationRequest
{
    Action = "send_sms",
    Parameters = new()
    {
        ["to"] = "+1987654321",
        ["body"] = "Your verification code is: 123456"
    }
});

// Make phone call
var callResult = await twilio.ExecuteAsync(new IntegrationRequest
{
    Action = "make_call",
    Parameters = new()
    {
        ["to"] = "+1987654321",
        ["url"] = "http://demo.twilio.com/docs/voice.xml" // TwiML URL
    }
});

Console.WriteLine($"SMS sent: {smsResult.Success}");
```

**Supported Actions**: send_sms, make_call
**Features**: SMS delivery, voice calls, TwiML support, delivery tracking

### 8. AWS S3 Integration

**Upload, download, and manage files in Amazon S3.**

```csharp
// Setup (requires AWS Access Key ID, Secret Access Key, and bucket name)
var s3 = new S3Integration(
    accessKeyId: "AKIAXXXXXXXXXX",
    secretAccessKey: "your-secret-key",
    bucketName: "my-bucket",
    region: "us-east-1"
);

// Test connection
var connected = await s3.TestConnectionAsync();

// Upload file
var uploadResult = await s3.ExecuteAsync(new IntegrationRequest
{
    Action = "upload",
    Parameters = new()
    {
        ["key"] = "reports/report-2025.txt",
        ["content"] = "File content here...",
        ["contentType"] = "text/plain"
    }
});

// Download file
var downloadResult = await s3.ExecuteAsync(new IntegrationRequest
{
    Action = "download",
    Parameters = new()
    {
        ["key"] = "reports/report-2025.txt"
    }
});

// List files
var listResult = await s3.ExecuteAsync(new IntegrationRequest
{
    Action = "list",
    Parameters = new()
    {
        ["prefix"] = "reports/",
        ["maxKeys"] = 100
    }
});

// Delete file
var deleteResult = await s3.ExecuteAsync(new IntegrationRequest
{
    Action = "delete",
    Parameters = new()
    {
        ["key"] = "reports/old-report.txt"
    }
});

Console.WriteLine($"Upload successful: {uploadResult.Success}");
```

**Supported Actions**: upload, download, delete, list
**Features**: File management, metadata support, prefix filtering, multi-region support

### 9. SendGrid Integration

**Send transactional emails at scale via SendGrid API.**

```csharp
// Setup (requires SendGrid API Key)
var sendgrid = new SendGridIntegration("SG.xxxxxxxxxx");

// Test connection
var connected = await sendgrid.TestConnectionAsync();

// Send email
var result = await sendgrid.ExecuteAsync(new IntegrationRequest
{
    Parameters = new()
    {
        ["from"] = "noreply@company.com",
        ["to"] = "user@example.com;admin@example.com",
        ["subject"] = "Welcome to Loco!",
        ["body"] = "<h1>Welcome!</h1><p>Thanks for signing up.</p>",
        ["isHtml"] = true
    }
});

Console.WriteLine($"Email sent: {result.Success}");
```

**Features**: HTML emails, multiple recipients, high deliverability, transactional email templates

### 10. Telegram Integration

**Send messages and media via Telegram Bot API.**

```csharp
// Setup (requires Telegram Bot Token from @BotFather)
var telegram = new TelegramIntegration("123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11");

// Test connection
var connected = await telegram.TestConnectionAsync();

// Send message
var messageResult = await telegram.ExecuteAsync(new IntegrationRequest
{
    Action = "send_message",
    Parameters = new()
    {
        ["chat_id"] = "123456789",
        ["text"] = "🚀 Deployment completed!",
        ["parse_mode"] = "HTML"
    }
});

// Send photo
var photoResult = await telegram.ExecuteAsync(new IntegrationRequest
{
    Action = "send_photo",
    Parameters = new()
    {
        ["chat_id"] = "123456789",
        ["photo"] = "https://example.com/image.jpg",
        ["caption"] = "System status dashboard"
    }
});

// Send document
var docResult = await telegram.ExecuteAsync(new IntegrationRequest
{
    Action = "send_document",
    Parameters = new()
    {
        ["chat_id"] = "123456789",
        ["document"] = "https://example.com/report.pdf",
        ["caption"] = "Monthly report"
    }
});

Console.WriteLine($"Message sent: {messageResult.Success}");
```

**Supported Actions**: send_message, send_photo, send_document
**Features**: Text messages, media sharing, HTML/Markdown formatting, bot commands

---

## 🔧 Integration Registry

**Manage multiple integrations in one place.**

```csharp
// Setup
var registry = new IntegrationRegistry();

// Register integrations
registry.Register("http", new HttpIntegration("https://api.example.com"));
registry.Register("db", new DatabaseIntegration(() => new SqliteConnection(connStr)));
registry.Register("email", EmailIntegration.Gmail("me@gmail.com", "app-password"));
registry.Register("slack", new SlackIntegration(webhookUrl));
registry.Register("github", new GitHubIntegration(token));

// Use from registry
var http = registry.Get("http");
var result = await http?.ExecuteAsync(request);

// Test all connections
var connectionStatus = await registry.TestAllConnectionsAsync();
foreach (var (name, connected) in connectionStatus)
{
    Console.WriteLine($"{name}: {(connected ? "✅" : "❌")}");
}
```

## 📊 Complete Workflow Example

**Combining multiple integrations in a real workflow:**

```csharp
public class DeploymentWorkflow
{
    private readonly IntegrationRegistry _integrations;

    public DeploymentWorkflow()
    {
        _integrations = new IntegrationRegistry();

        // Setup all integrations
        _integrations.Register("github", new GitHubIntegration(githubToken));
        _integrations.Register("slack", new SlackIntegration(slackWebhook));
        _integrations.Register("db", new DatabaseIntegration(() => new SqliteConnection(connStr)));
        _integrations.Register("email", EmailIntegration.Gmail(email, appPassword));
    }

    public async Task<bool> ExecuteDeploymentAsync(string version, string branch)
    {
        try
        {
            // 1. Create GitHub release issue
            var github = _integrations.Get("github")!;
            var issueResult = await github.ExecuteAsync(new IntegrationRequest
            {
                Action = "create_issue",
                Parameters = new()
                {
                    ["owner"] = "myorg",
                    ["repo"] = "myapp",
                    ["title"] = $"Deploy version {version}",
                    ["body"] = $"Deploying from branch: {branch}",
                    ["labels"] = "deployment"
                }
            });

            if (!issueResult.Success)
                throw new Exception($"GitHub error: {issueResult.Error}");

            // 2. Record deployment in database
            var db = _integrations.Get("db")!;
            var dbResult = await db.ExecuteAsync(new IntegrationRequest
            {
                Action = "execute",
                Parameters = new()
                {
                    ["sql"] = "INSERT INTO deployments (version, branch, timestamp) VALUES (@version, @branch, @timestamp)",
                    ["version"] = version,
                    ["branch"] = branch,
                    ["timestamp"] = DateTime.UtcNow
                }
            });

            // 3. Notify Slack
            var slack = _integrations.Get("slack")!;
            var slackResult = await slack.ExecuteAsync(new IntegrationRequest
            {
                Parameters = new()
                {
                    ["text"] = $"🚀 Deployment started: version {version}",
                    ["channel"] = "#deployments"
                }
            });

            // 4. Send email notification
            var email = _integrations.Get("email")!;
            var emailResult = await email.ExecuteAsync(new IntegrationRequest
            {
                Parameters = new()
                {
                    ["to"] = "team@company.com",
                    ["subject"] = $"Deployment: {version}",
                    ["body"] = $"Deployment of version {version} has started."
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            // Notify failure
            var slack = _integrations.Get("slack")!;
            await slack.ExecuteAsync(new IntegrationRequest
            {
                Parameters = new()
                {
                    ["text"] = $"❌ Deployment failed: {ex.Message}",
                    ["channel"] = "#alerts"
                }
            });

            return false;
        }
    }
}
```

## 🎯 Integration with Loco Workflows

**Use integrations inside Loco workflows:**

```csharp
using Loco.Core.Workflows;
using Loco.Core.Integrations;

var registry = new IntegrationRegistry();
registry.Register("slack", new SlackIntegration(webhookUrl));
registry.Register("db", new DatabaseIntegration(() => new SqliteConnection(connStr)));

var workflow = new WorkflowBuilder()
    .Step("FetchData", async () =>
    {
        var db = registry.Get("db")!;
        var result = await db.ExecuteAsync(new IntegrationRequest
        {
            Action = "query",
            Parameters = new() { ["sql"] = "SELECT * FROM orders WHERE status = 'pending'" }
        });
        return result.Success;
    })
    .Step("ProcessOrders", async () =>
    {
        // Process orders...
        return true;
    })
    .Step("NotifySlack", async () =>
    {
        var slack = registry.Get("slack")!;
        var result = await slack.ExecuteAsync(new IntegrationRequest
        {
            Parameters = new() { ["text"] = "Orders processed successfully!" }
        });
        return result.Success;
    })
    .Build();

var success = await workflow.ExecuteAsync();
```

## 📈 Performance Characteristics

| Integration | Throughput | Latency | Notes |
|-------------|-----------|---------|-------|
| HTTP | 1K-10K req/sec | 10-500ms | Depends on target API |
| Database | 5K-50K queries/sec | 1-50ms | Depends on DB and query |
| Email | 10-100 emails/sec | 100-2000ms | Network bound |
| Slack | 1-100 msgs/sec | 50-500ms | Rate limited by Slack |
| GitHub | 10-5000 req/hr | 100-1000ms | Rate limited by GitHub |

## 🔒 Security Best Practices

### 1. Store Credentials Securely

```csharp
// ❌ Bad: Hardcoded credentials
var email = new EmailIntegration("smtp.gmail.com", 587, "user@gmail.com", "password");

// ✅ Good: Use environment variables or config
var config = new SimpleConfig();
var email = EmailIntegration.Gmail(
    config.Get<string>("Email:Username"),
    config.Get<string>("Email:AppPassword")
);
```

### 2. Use App-Specific Passwords

- **Gmail**: Use App Password, not account password
- **GitHub**: Use Personal Access Token with minimal scopes
- **Slack**: Use Webhook URLs (no full OAuth needed for simple cases)

### 3. Validate Inputs

```csharp
// Validate before executing
if (string.IsNullOrEmpty(email))
    throw new ArgumentException("Email address required");

if (!email.Contains("@"))
    throw new ArgumentException("Invalid email format");
```

### 4. Handle Errors Gracefully

```csharp
var result = await integration.ExecuteAsync(request);

if (!result.Success)
{
    logger.Error($"Integration failed: {result.Error}");
    // Retry logic, fallback, or alert
}
```

## 🚀 Next Steps

**Planned Integrations (Coming Soon)**:
1. **Discord** - Send messages to Discord channels
2. **Twilio** - SMS and phone call notifications
3. **AWS S3** - File storage and retrieval
4. **Google Sheets** - Read/write spreadsheet data
5. **Stripe** - Payment processing
6. **SendGrid** - Transactional email at scale
7. **Telegram** - Bot messaging
8. **Webhooks** - Generic webhook sender/receiver
9. **FTP/SFTP** - File transfer
10. **Redis** - Cache and pub/sub

**Want an integration?** Create an issue on GitHub with the `integration-request` label.

## 📚 Related Documentation

- [Practical Patterns](../Practical/README.md) - Core patterns used in integrations
- [AI Integration](../AI/README.md) - AI provider integrations (OpenAI, Claude)
- [Examples](../Practical/EXAMPLES.md) - More workflow examples
- [Competitive Analysis](../../../docs/COMPETITIVE_ANALYSIS_2025.md) - Why integrations matter

---

**Version**: 1.0.0
**Last Updated**: 2025-11-07
**Status**: Production Ready ✅
