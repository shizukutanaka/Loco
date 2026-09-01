# Getting Started with Loco

Complete guide to building your first automated workflow with Loco in under 15 minutes.

## 📋 Prerequisites

- **.NET 8.0 SDK** or later
- **IDE**: Visual Studio 2022, VS Code, or Rider
- **Optional**: API keys for integrations (OpenAI, Slack, GitHub, etc.)

## 🚀 Quick Start (5 minutes)

### 1. Clone and Build

```bash
git clone https://github.com/loco-automation/loco.git
cd loco
dotnet build
```

### 2. Your First Workflow

Create a new console application:

```bash
dotnet new console -n MyFirstWorkflow
cd MyFirstWorkflow
dotnet add reference ../loco/src/Loco.Core/Loco.Core.csproj
```

**Program.cs:**

```csharp
using Loco.Core.Workflows;

// Create a simple workflow
var workflow = new VisualWorkflowBuilder()
    .WithName("Hello Workflow")
    .AddNode("Start", "trigger", "manual", "start", new())
    .AddNode("Transform", "transform", "transform", "json", new()
    {
        ["data"] = new { message = "Hello, Loco!" }
    })
    .Connect("Start", "Transform")
    .Build();

// Execute
var engine = new VisualWorkflowEngine(Console.WriteLine);
var result = await engine.ExecuteAsync(workflow);

Console.WriteLine($"Status: {result.Status}");
Console.WriteLine($"Duration: {result.Duration}");
```

Run it:

```bash
dotnet run
```

**Output:**
```
[12:00:00] Starting workflow: Hello Workflow
[12:00:00] Executing node: Start (trigger)
[12:00:00] Node succeeded: Start
[12:00:00] Executing node: Transform (transform)
[12:00:00] Node succeeded: Transform
Status: Success
Duration: 00:00:00.0123456
```

## 🎯 Feature Walkthroughs

### 1. Using Pre-built Integrations (10 minutes)

#### A. HTTP API Integration

```csharp
using Loco.Core.Integrations;

// Setup HTTP integration
var http = new HttpIntegration("https://api.github.com", new()
{
    ["User-Agent"] = "Loco-App"
});

// Test connection
var connected = await http.TestConnectionAsync();
Console.WriteLine($"Connected: {connected}");

// Make API call
var result = await http.ExecuteAsync(new IntegrationRequest
{
    Action = "GET",
    Parameters = new() { ["path"] = "/users/octocat" }
});

if (result.Success)
{
    Console.WriteLine($"Status: {result.StatusCode}");
    Console.WriteLine($"Data: {result.Data}");
}
```

#### B. Database Integration

```csharp
using System.Data.SQLite;

// Setup database
var db = new DatabaseIntegration(
    () => new SQLiteConnection("Data Source=myapp.db"),
    "SQLite"
);

// Create table
await db.ExecuteAsync(new IntegrationRequest
{
    Action = "execute",
    Parameters = new()
    {
        ["sql"] = @"CREATE TABLE IF NOT EXISTS users (
            id INTEGER PRIMARY KEY,
            name TEXT NOT NULL,
            email TEXT UNIQUE
        )"
    }
});

// Insert data
await db.ExecuteAsync(new IntegrationRequest
{
    Action = "execute",
    Parameters = new()
    {
        ["sql"] = "INSERT INTO users (name, email) VALUES (@name, @email)",
        ["name"] = "John Doe",
        ["email"] = "john@example.com"
    }
});

// Query data
var queryResult = await db.ExecuteAsync(new IntegrationRequest
{
    Action = "query",
    Parameters = new() { ["sql"] = "SELECT * FROM users" }
});

var users = queryResult.Data as List<Dictionary<string, object?>>;
Console.WriteLine($"Found {users?.Count} users");
```

#### C. Email Integration

```csharp
// Gmail example (requires App Password)
var email = EmailIntegration.Gmail(
    "your-email@gmail.com",
    "your-app-password" // Get from: https://myaccount.google.com/apppasswords
);

// Send email
var result = await email.ExecuteAsync(new IntegrationRequest
{
    Parameters = new()
    {
        ["to"] = "recipient@example.com",
        ["subject"] = "Hello from Loco",
        ["body"] = "This is an automated email!",
        ["isHtml"] = false
    }
});

Console.WriteLine($"Email sent: {result.Success}");
```

#### D. Slack Integration

```csharp
// Get webhook URL from: https://api.slack.com/messaging/webhooks
var slack = new SlackIntegration("https://hooks.slack.com/services/YOUR/WEBHOOK/URL");

var result = await slack.ExecuteAsync(new IntegrationRequest
{
    Parameters = new()
    {
        ["text"] = "🚀 Deployment completed successfully!",
        ["channel"] = "#deployments"
    }
});

Console.WriteLine($"Message sent: {result.Success}");
```

### 2. Building Visual Workflows (15 minutes)

#### Complete Workflow Example

```csharp
using Loco.Core.Workflows;
using Loco.Core.Integrations;

// Setup integrations
var registry = new IntegrationRegistry();
registry.Register("http", new HttpIntegration("https://api.github.com"));
registry.Register("slack", new SlackIntegration(slackWebhook));
registry.Register("database", new DatabaseIntegration(() => new SQLiteConnection(connStr)));

// Build workflow
var workflow = new VisualWorkflowBuilder()
    .WithName("API Monitor with Alerts")
    .WithDescription("Check API health every 5 minutes and alert on failures")

    // Step 1: Schedule trigger
    .AddNode("Schedule", "trigger", "scheduler", "interval", new()
    {
        ["interval"] = 300, // 5 minutes
        ["unit"] = "seconds"
    })

    // Step 2: Check API
    .AddNode("Health Check", "action", "http", "get", new()
    {
        ["path"] = "/health"
    })

    // Step 3: Condition - check if failed
    .AddNode("Check Status", "condition", "condition", "evaluate", new()
    {
        ["left"] = "{{nodes.HealthCheck.statusCode}}",
        ["operation"] = "not_equals",
        ["right"] = 200
    })

    // Step 4a: Alert on failure
    .AddNode("Alert Team", "action", "slack", "send", new()
    {
        ["channel"] = "#alerts",
        ["text"] = "🚨 API health check failed! Status: {{nodes.HealthCheck.statusCode}}"
    })

    // Step 4b: Log success
    .AddNode("Log Success", "action", "database", "execute", new()
    {
        ["sql"] = "INSERT INTO health_checks (status, timestamp) VALUES ('ok', @time)",
        ["time"] = "{{$now}}"
    })

    // Connect nodes
    .Connect("Schedule", "Health Check")
    .Connect("Health Check", "Check Status")
    .Connect("Check Status", "Alert Team", "error")
    .Connect("Check Status", "Log Success", "success")
    .Build();

// Setup engine with handlers
var engine = new VisualWorkflowEngine(Console.WriteLine);

engine.RegisterNodeHandler("http:get", async (node, context) =>
{
    var http = registry.Get("http")!;
    return await http.ExecuteAsync(new IntegrationRequest
    {
        Action = "GET",
        Parameters = node.Parameters
    });
});

engine.RegisterNodeHandler("slack:send", async (node, context) =>
{
    var slack = registry.Get("slack")!;
    return await slack.ExecuteAsync(new IntegrationRequest
    {
        Parameters = node.Parameters
    });
});

engine.RegisterNodeHandler("database:execute", async (node, context) =>
{
    var db = registry.Get("database")!;
    return await db.ExecuteAsync(new IntegrationRequest
    {
        Action = "execute",
        Parameters = node.Parameters
    });
});

// Validate
var validator = new WorkflowValidator();
var validation = validator.Validate(workflow);

if (!validation.IsValid)
{
    Console.WriteLine("Validation errors:");
    validation.Errors.ForEach(Console.WriteLine);
    return;
}

// Execute
var result = await engine.ExecuteAsync(workflow);

Console.WriteLine($"Workflow completed: {result.Status}");
Console.WriteLine($"Nodes executed: {result.NodeResults.Count}");
Console.WriteLine($"Duration: {result.Duration}");

// Print execution log
foreach (var log in result.ExecutionLog)
{
    Console.WriteLine(log);
}
```

#### Export/Import Workflows

```csharp
// Export to JSON
var json = new VisualWorkflowBuilder()
    .FromWorkflow(workflow)
    .ToJson();

File.WriteAllText("my-workflow.json", json);

// Import from JSON
var loadedWorkflow = VisualWorkflowBuilder.FromJson(
    File.ReadAllText("my-workflow.json")
);
```

### 3. AI Integration (10 minutes)

#### Using OpenAI

```csharp
using Loco.Core.AI;

var openai = new OpenAIProvider("sk-your-api-key");

// Simple completion
var response = await openai.CompleteTextAsync(
    "Explain microservices in one sentence.",
    new AIOptions { Temperature = 0.7, MaxTokens = 100 }
);

Console.WriteLine($"Response: {response.Content}");
Console.WriteLine($"Cost: ${response.Usage.EstimatedCost:F4}");
Console.WriteLine($"Tokens: {response.Usage.TotalTokens}");
```

#### Using Claude

```csharp
var claude = new ClaudeProvider("sk-your-api-key");

var response = await claude.CompleteTextAsync(
    "Write a Python function to calculate factorial.",
    new AIOptions { Model = "claude-3-sonnet-20240229" }
);

Console.WriteLine(response.Content);
```

#### Multi-Step AI Chains

```csharp
var orchestrator = new AIOrchestrator();
orchestrator.RegisterProvider("openai", openai);
orchestrator.RegisterProvider("claude", claude);

// Chain: Generate → Review → Finalize
var result = await orchestrator.ChainAsync(
    ("openai", "Generate a product description for a smart watch"),
    ("claude", "Review and improve this description for clarity"),
    ("openai", "Make it more concise (under 100 words)")
);

Console.WriteLine(result.Content);

// Get statistics
var stats = orchestrator.GetStats();
Console.WriteLine($"Total cost: ${stats.TotalCost:F4}");
Console.WriteLine($"Total tokens: {stats.TotalTokens}");
```

#### Prompt Templates

```csharp
var template = PromptTemplate.Create(
    "product-review",
    @"Review this product and provide:
    - Rating (1-5 stars)
    - Pros
    - Cons
    - Recommendation

    Product: {{product_name}}
    Description: {{product_description}}"
);

var prompt = template.Render(new Dictionary<string, string>
{
    ["product_name"] = "Smart Watch Pro",
    ["product_description"] = "Advanced fitness tracking with heart rate monitor"
});

var review = await openai.CompleteTextAsync(prompt);
```

### 4. Using Workflow Templates (5 minutes)

```csharp
using Loco.Core.Workflows;

// List all templates
var templates = WorkflowTemplates.GetAllTemplates();

foreach (var template in templates)
{
    Console.WriteLine($"{template.Name}");
    Console.WriteLine($"  Category: {template.Category}");
    Console.WriteLine($"  Difficulty: {template.Difficulty}");
    Console.WriteLine($"  Tags: {string.Join(", ", template.Tags)}");
    Console.WriteLine();
}

// Load and use a template
var workflow = WorkflowTemplates.GetTemplateById("database-backup-email");

// Customize if needed
workflow.Nodes[0].Parameters["schedule"] = "0 2 * * *"; // 2 AM daily

// Execute
var engine = new VisualWorkflowEngine();
var result = await engine.ExecuteAsync(workflow);
```

### 5. Monitoring and Logging (5 minutes)

```csharp
using Loco.Core.Practical;

// Setup logger
var logger = SimpleLoggerFactory.GetLogger("MyApp");
logger.Info("Application started");

// Setup monitoring
var monitor = new SimpleMonitor();

// Track metrics
monitor.Increment("requests.count");
monitor.RecordMetric("request.duration", 123.45);

// Use performance timer
using (new PerformanceMonitor(monitor).StartTimer("database.query"))
{
    // Your code here
    await Task.Delay(100);
}

// Get snapshot
var snapshot = monitor.GetSnapshot();
Console.WriteLine($"Total requests: {snapshot.Counters["requests.count"]}");
Console.WriteLine($"Avg duration: {snapshot.Metrics.First().Average}ms");
```

## 📚 Next Steps

### Learn More

1. **[Complete Example](../examples/CompleteAutomationExample.cs)** - Full-featured automation system
2. **[Practical Patterns](../src/Loco.Core/Practical/SimpleLogger.cs)** - 37 lightweight patterns
3. **[Example workflows](../examples/)** - runnable JSON workflows
4. **[Integration Guide](../src/Loco.Core/Integrations/Connectors/)** - Using connectors
5. **[Connector library](../src/Loco.Core/Integrations/Connectors/)** - all 28 connectors

### Common Patterns

#### Pattern 1: Scheduled Data Sync

```csharp
var workflow = new VisualWorkflowBuilder()
    .WithName("Hourly Data Sync")
    .AddNode("Schedule", "trigger", "scheduler", "interval", new() { ["interval"] = 3600 })
    .AddNode("Fetch", "action", "http", "get", new() { ["url"] = "https://api.example.com/data" })
    .AddNode("Store", "action", "database", "execute", new() { ["sql"] = "INSERT INTO data..." })
    .Connect("Schedule", "Fetch")
    .Connect("Fetch", "Store")
    .Build();
```

#### Pattern 2: Error Notification

```csharp
// Add error handling to any node
node.RetryConfig = new RetryConfig
{
    MaxAttempts = 3,
    DelaySeconds = 5,
    ExponentialBackoff = true
};

// Add error notification
.AddNode("On Error", "action", "slack", "send", new()
{
    ["text"] = "❌ Workflow failed: {{$error}}"
})
.Connect("MainNode", "On Error", "error");
```

#### Pattern 3: Conditional Processing

```csharp
.AddNode("Check Value", "condition", "condition", "evaluate", new()
{
    ["left"] = "{{$input.value}}",
    ["operation"] = "greater_than",
    ["right"] = 1000
})
.AddNode("High Value Handler", "action", "email", "send", new() { ... })
.AddNode("Low Value Handler", "action", "database", "execute", new() { ... })
.Connect("Check Value", "High Value Handler", "success")
.Connect("Check Value", "Low Value Handler", "error");
```

## 🔧 Configuration

### Environment Variables

Create `.env` file:

```bash
# API Keys
OPENAI_API_KEY=sk-...
CLAUDE_API_KEY=sk-ant-...
GITHUB_TOKEN=ghp_...

# Email
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USER=your-email@gmail.com
SMTP_PASSWORD=your-app-password

# Slack
SLACK_WEBHOOK_URL=https://hooks.slack.com/services/...

# Database
DATABASE_CONNECTION=Data Source=app.db
```

### Load Configuration

```csharp
using Loco.Core.Practical;

var config = new ConfigBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

var openAiKey = config.Get<string>("OPENAI_API_KEY");
var dbConnection = config.Get<string>("DATABASE_CONNECTION");
```

## 🐛 Troubleshooting

### Common Issues

**1. "Integration connection failed"**
```csharp
// Test connection first
var connected = await integration.TestConnectionAsync();
if (!connected)
{
    Console.WriteLine("Connection failed - check credentials");
}
```

**2. "Workflow validation failed"**
```csharp
var validator = new WorkflowValidator();
var result = validator.Validate(workflow);

if (!result.IsValid)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"Error: {error}");
    }
}
```

**3. "Node execution timeout"**
```csharp
// Add timeout handling
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var result = await engine.ExecuteAsync(workflow, ct: cts.Token);
```

### Debug Mode

```csharp
// Enable detailed logging
var engine = new VisualWorkflowEngine(msg =>
{
    Console.WriteLine($"[DEBUG] {msg}");
});

// Check execution log
var result = await engine.ExecuteAsync(workflow);
foreach (var log in result.ExecutionLog)
{
    Console.WriteLine(log);
}

// Inspect node results
foreach (var (nodeId, nodeResult) in result.NodeResults)
{
    Console.WriteLine($"{nodeResult.NodeName}: {nodeResult.Success}");
    if (!nodeResult.Success)
    {
        Console.WriteLine($"  Error: {nodeResult.Error}");
    }
}
```

## 💡 Tips and Best Practices

1. **Always validate workflows before execution**
   ```csharp
   var validation = validator.Validate(workflow);
   if (!validation.IsValid) throw new Exception("Invalid workflow");
   ```

2. **Use retry configuration for network operations**
   ```csharp
   node.RetryConfig = new RetryConfig { MaxAttempts = 3, ExponentialBackoff = true };
   ```

3. **Cache frequently accessed data**
   ```csharp
   var cache = new SimpleCache<T>(maxSize: 10000);
   cache.Set(key, value, TimeSpan.FromMinutes(5));
   ```

4. **Monitor performance**
   ```csharp
   var monitor = new SimpleMonitor();
   using var timer = new PerformanceMonitor(monitor).StartTimer("operation");
   ```

5. **Use templates as starting points**
   ```csharp
   var workflow = WorkflowTemplates.GetTemplateById("api-health-slack");
   // Customize parameters...
   ```

## 🔐 API Authentication Setup

The HTTP API (`Loco.Api`) protects its endpoints with JWT bearer auth. Two
things must be configured before the API will issue tokens:

**1. A signing key.** Set `Jwt:SecretKey` (>= 32 bytes) via environment
variable or user secrets — never commit it:

```bash
export Jwt__SecretKey="a-long-random-string-at-least-32-bytes-long"
```

Outside Development the API refuses to start without this. In Development a
random per-run key is generated (tokens then reset on restart).

**2. At least one user.** Users live in the `Auth:Users` configuration array,
each with a PBKDF2 password hash and a scope list. Generate a hash:

```bash
printf 'your-password' | dotnet run --project src/Loco.Cli -- hash-password
# -> PBKDF2$100000$<salt>$<hash>, followed by the Auth:Users block to paste
```

The password is read from standard input rather than passed as an argument,
so it does not land in your shell history or the process list.

Then configure (e.g. in `appsettings.Development.json` or environment):

```json
{
  "Auth": {
    "Users": [
      {
        "Username": "admin",
        "PasswordHash": "PBKDF2$100000$...$...",
        "Scopes": ["workflows:read", "workflows:manage", "workflows:execute"]
      }
    ]
  }
}
```

With no users configured, `POST /api/v1/authentication/token` returns
`501 AUTH_NOT_CONFIGURED` — it never accepts arbitrary credentials. Exchange
credentials for a token, then send it as `Authorization: Bearer <token>`:

```bash
curl -X POST http://localhost:5000/api/v1/authentication/token \
  -H 'Content-Type: application/json' \
  -d '{"username":"admin","password":"your-password"}'
```

## 📖 Additional Resources

- **[Full Documentation](../README.md)** - Complete feature overview
- **[Practical Patterns Index](../src/Loco.Core/Practical/SimpleLogger.cs)** - All 37 patterns
- **[Competitive Analysis](COMPETITIVE_ANALYSIS_2025.md)** - Market positioning
- **[Examples](../examples/)** - More code samples
- **[GitHub Issues](https://github.com/loco-automation/loco/issues)** - Report bugs

---

**Ready to automate!** Start with the [Complete Example](../examples/CompleteAutomationExample.cs) to see all features in action.
