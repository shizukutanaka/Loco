# Loco API Documentation

## Overview

Loco provides a comprehensive API for automation and workflow management. This document covers the core APIs, interfaces, and services available for developers.

## Table of Contents

1. [Core Services](#core-services)
2. [Automation Engine](#automation-engine)
3. [Flow Engine](#flow-engine)
4. [Plugin System](#plugin-system)
5. [Natural Language Processing](#natural-language-processing)
6. [Caching System](#caching-system)
7. [Error Handling](#error-handling)

## Core Services

### IAutomationService

The main service for managing automation rules.

```csharp
public interface IAutomationService
{
    Task<bool> StartAsync(CancellationToken cancellationToken = default);
    Task<bool> StopAsync(CancellationToken cancellationToken = default);

    Task<bool> RegisterFlowAsync(IFlow flow, CancellationToken cancellationToken = default);
    Task<bool> UnregisterFlowAsync(string flowId, CancellationToken cancellationToken = default);
    Task<IEnumerable<IFlow>> GetActiveFlowsAsync(CancellationToken cancellationToken = default);

    Task LoadSavedRulesAsync(CancellationToken cancellationToken = default);

    Task<RuleValidationResult> ValidateRuleJsonAsync(string json, CancellationToken cancellationToken = default);
    Task<RuleValidationResult> ValidateRuleJsonAsync(JsonNode node, CancellationToken cancellationToken = default);

    Task<bool> AddRuleFromJsonAsync(string json, CancellationToken cancellationToken = default);
    Task<bool> AddRuleFromJsonAsync(JsonNode node, CancellationToken cancellationToken = default);
}
```

#### Methods

##### AddRuleFromJsonAsync
Adds a new automation rule to the system from JSON.

**Parameters:**
- `json` (string): JSON string representing the rule

**Returns:**
- `Task<bool>`: True if successful, false otherwise

**Example:**
```csharp
var ruleJson = @"{
  \"id\": \"morning-routine\",
  \"name\": \"Morning Routine\",
  \"enabled\": true,
  \"trigger\": { \"type\": \"time.schedule\", \"config\": { \"hour\": 7, \"minute\": 0 } },
  \"actions\": [ { \"type\": \"notification.show\", \"config\": { \"title\": \"Good Morning\", \"message\": \"Time to start your day!\" } } ]
}";

bool success = await automationService.AddRuleFromJsonAsync(ruleJson);
```

##### ValidateRuleJsonAsync
Validates an automation rule provided as JSON.

**Parameters:**
- `json` (string) or `node` (JsonNode)

**Returns:**
- `Task<RuleValidationResult>`: Validation result with errors if any

**Example:**
```csharp
var validation = await automationService.ValidateRuleJsonAsync(ruleJson);
if (!validation.IsValid)
{
    Console.WriteLine(string.Join("\n", validation.Errors));
}
```

### IFlowEngine

Manages and runs flows.

```csharp
public interface IFlowEngine
{
    Task RunAsync(IFlow flow, FlowContext context, CancellationToken cancellationToken = default);
}
```

## Automation Engine

### Rule Structure

```csharp
public class Rule
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool Enabled { get; set; }
    public int Priority { get; set; }
    public Trigger Trigger { get; set; }
    public List<Condition> Conditions { get; set; }
    public List<Action> Actions { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}
```

### Trigger Types

| Type | Description | Configuration |
|------|-------------|---------------|
| `time.schedule` | Time-based trigger | `hour`, `minute`, `days` |
| `file.changed` | File system monitor | `path`, `pattern` |
| `app.started` | Application launch | `processName` |
| `system.startup` | System boot | None |
| `webhook` | HTTP webhook | `url`, `method` |
| `manual` | Manual trigger | None |

### Action Types

| Type | Description | Configuration |
|------|-------------|---------------|
| `notification.show` | Display notification | `title`, `message` |
| `file.copy` | Copy files | `source`, `destination` |
| `file.move` | Move files | `source`, `destination` |
| `file.delete` | Delete files | `path` |
| `app.run` | Launch application | `path`, `arguments` |
| `http.request` | HTTP request | `url`, `method`, `headers`, `body` |
| `email.send` | Send email | `to`, `subject`, `body` |
| `tts.speak` | Text-to-speech | `text`, `voice` |
| `log` | Write to log | `message`, `level` |

## Flow Engine

### FlowDefinition Structure

```csharp
public class FlowDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<FlowTrigger> Triggers { get; set; }
    public List<FlowCondition> Conditions { get; set; }
    public List<FlowAction> Actions { get; set; }
    public Dictionary<string, object> Variables { get; set; }
}
```

### Flow Execution

Flows are executed in the following order:
1. Trigger evaluation
2. Condition checking
3. Action execution (in sequence)
4. Result handling

## Plugin System

### Creating a Plugin

1. Create a class library project
2. Reference `Loco.Core`
3. Implement plugin interfaces

```csharp
public class MyPlugin : ILocoPlugin
{
    public string Name => "My Custom Plugin";
    public string Version => "1.0.0";
    public string Description => "Adds custom functionality";

    public void Initialize(IServiceProvider services)
    {
        // Plugin initialization
    }

    public void RegisterActions(IActionRegistry registry)
    {
        registry.Register("my.action", new MyCustomAction());
    }

    public void RegisterTriggers(ITriggerRegistry registry)
    {
        registry.Register("my.trigger", new MyCustomTrigger());
    }
}
```

### Plugin Manifest

```json
{
  "name": "My Plugin",
  "version": "1.0.0",
  "author": "Your Name",
  "description": "Plugin description",
  "main": "MyPlugin.dll",
  "dependencies": {
    "Loco.Core": ">=1.0.0"
  },
  "permissions": [
    "file.read",
    "file.write",
    "network.access"
  ]
}
```

## Natural Language Processing

### Converting Natural Language to Rules

```csharp
var nlService = serviceProvider.GetRequiredService<INaturalLanguageRuleService>();

string input = "Every morning at 7 AM, send me a reminder to exercise";
string ruleJson = await nlService.ConvertTextToRuleJsonAsync(input);

bool success = await automationService.AddRuleFromJsonAsync(ruleJson);
```

### Supported Patterns

- Time-based: "at 9 AM", "every day at", "every Monday"
- File operations: "backup files", "copy from X to Y"
- Notifications: "notify me", "send alert", "remind me"
- Conditions: "if X then Y", "when X happens"

## Caching System

### FastCache Usage

```csharp
var cache = serviceProvider.GetRequiredService<IFastCache>();

// Set value
await cache.SetAsync("key", "value", TimeSpan.FromMinutes(5));

// Get value
var value = await cache.GetAsync<string>("key");

// Get or create
var data = await cache.GetOrCreateAsync("data-key", async () =>
{
    // Expensive operation
    return await FetchDataAsync();
}, TimeSpan.FromHours(1));
```

## Error Handling

### Common error categories

| Category | Typical cause | Recommended action |
|----------|----------------|--------------------|
| Rule validation errors | Missing fields, invalid types in JSON | Inspect `ValidateRuleJsonAsync` errors and fix JSON |
| Rule add failures | Duplicate IDs, guard checks failed | Ensure unique `id`, required fields present |
| Plugin load failures | Invalid DLLs or path | Verify `--plugins-path` and plugin assemblies |

### Example: safe validation and add with logging

```csharp
var validation = await automationService.ValidateRuleJsonAsync(ruleJson);
if (!validation.IsValid)
{
    logger.LogError("Rule validation failed: {Errors}", string.Join("; ", validation.Errors));
    return; // stop on invalid input
}

var added = await automationService.AddRuleFromJsonAsync(ruleJson);
if (!added)
{
    logger.LogError("Failed to add rule from JSON.");
    return;
}

logger.LogInformation("Rule added successfully.");
```

## Web API Endpoints

This section documents the RESTful API endpoints provided by the Loco web service.

### Flows API

#### `GET /api/flows`

Retrieves flows with optional pagination and simple search.

Query parameters:

- `skip` (int, optional): Number of items to skip
- `take` (int, optional): Max number of items to return
- `q` (string, optional): Case-insensitive search in `name` and `description`

Response headers:

- `X-Total-Count`: Total number of matching items (before pagination)
- `Cache-Control`: `public, max-age=30`

**Response (200 OK):**
```json
[
  {
    "id": "daily-backup",
    "name": "Daily Backup",
    "description": "Backs up important files every day at midnight."
  },
  {
    "id": "morning-routine",
    "name": "Morning Routine",
    "description": "Starts your day with news, weather, and your calendar."
  }
]
```

#### `POST /api/flows`

Creates a new automation flow.

**Request Body:**
The request body should be a JSON object representing the flow definition.
```json
{
  "id": "new-flow",
  "name": "My New Flow",
  "description": "A description of the new flow.",
  "actions": [
    {
      "type": "notification.show",
      "config": {
        "title": "Flow Created",
        "message": "Your new flow is ready!"
      }
    }
  ]
}
```

**Response (201 Created):**
```json
{
  "flowId": "generated-id",
  "shareUrl": "about:blank",
  "shortUrl": "about:blank"
}
```

#### `GET /api/flows/{id}`

Retrieves a specific automation flow by its ID.

**Parameters:**
- `id` (string, required): The ID of the flow to retrieve.

**Response (200 OK):**
```json
{
  "id": "daily-backup",
  "name": "Daily Backup",
  "description": "Backs up important files every day at midnight.",
  "triggers": [
    {
      "type": "time.schedule",
      "config": { "hour": 0, "minute": 0 }
    }
  ],
  "actions": [
    {
      "type": "file.copy",
      "config": { "source": "C:\\Users\\user\\Documents", "destination": "D:\\Backup" }
    }
  ]
}
```
**Response (404 Not Found):**
If a flow with the specified ID is not found.

#### `GET /api/flows/{id}/download`

Returns the JSON definition of a specific flow and increments its download count.

**Parameters:**
- `id` (string, required): The ID of the flow to download.

**Response (200 OK):**
JSON body of the flow with header `Cache-Control: public, max-age=60`.

**Response (404 Not Found):**
If the flow is not found.

#### `PUT /api/flows/{id}`

Upserts a flow by ID. If the flow exists, it is updated; otherwise, it is created with the given `id`.

**Request Body:** FlowDefinition JSON. `name` is required.

**Response (200 OK):** Returns the stored flow.

**Response (400 Bad Request):** Missing or invalid body.

#### `DELETE /api/flows/{id}`

Deletes a flow by ID.

**Response (204 No Content):** Deleted successfully.

**Response (404 Not Found):** If the flow does not exist.

### Sharing and Installation

#### `POST /api/share`

Generates share artifacts for a given flow definition.

**Request Body:** FlowDefinition JSON.

**Response (200 OK):**
```json
{
  "shareId": "...",
  "shortCode": "...",
  "shareUrl": "...",
  "locoUrl": "loco://install/...",
  "markdownLink": "[Install](loco://install/...)",
  "htmlLink": "<a href=\"loco://install/...\">Install</a>",
  "qrCode": "ASCII-QR",
  "expiresAt": "2025-01-01T00:00:00Z"
}
```

#### `GET /api/install/{id}`

Provides a one-click installation page for a shared flow.

**Parameters:**
- `id` (string, required): The ID of the flow to install.

**Response (200 OK):**
The response is an HTML page that guides the user through the installation process.

### Health

#### `GET /healthz`

Returns the current health status of the service.

**Response (200 OK):** Health details JSON.

### LLM Configuration

#### `GET /api/llm/config`

Returns the effective LLM configuration. The API key is redacted.

**Response (200 OK):**
```json
{
  "provider": "openai",
  "model": "gpt-4",
  "apiEndpoint": "https://api.openai.com/v1/completions",
  "maxTokens": 1000,
  "temperature": 0.7,
  "httpTimeoutMs": 30000,
  "apiKey": "redacted",
  "hasApiKey": true,
  "preset": "OPENAI"
}
```
Notes:
- `apiKey` is always redacted as the literal string `"redacted"` when a key is configured. If no key is set, `apiKey` is an empty string `""` and `hasApiKey` is `false`.
- `preset` is the value of `LOCO_LLM__PRESET` when set; otherwise it is `null` and still included in the JSON.
- `httpTimeoutMs` is clamped between `1000` and `600000` milliseconds.

### Root

#### `GET /`

Provides basic service info:

```json
{
  "name": "Loco API",
  "version": "x.y.z",
  "endpoints": ["/api/flows", "/api/install/{id}", "/api/share", "/api/llm/config", "/healthz"]
}
```

### Configuration

Environment variables:

- `MVP_RULE_STORE_PATH`: Absolute path to the JSON rule store used by the console app. Defaults to `$(AppContext.BaseDirectory)/data/rules.json`.

- `LOCO_LLM__PROVIDER`: LLM provider id (e.g., `openai`, `anthropic`, `gemini`, `ollama`).
- `LOCO_LLM__MODEL`: Model name (e.g., `gpt-4o`, `claude-3-5-sonnet`, `gemini-1.5-pro`, `llama3`).
- `LOCO_LLM__APIKEY`: API key for the provider.
- `LOCO_LLM__APIENDPOINT`: Custom base URL for the provider.
- `LOCO_LLM__TEMPERATURE`: Sampling temperature (float).
- `LOCO_LLM__MAXTOKENS`: Max tokens for responses (int).
- `LOCO_LLM__HTTPTIMEOUTMS`: HTTP timeout in milliseconds. Default 30000; clamped to 1000–600000.
- `LOCO_LLM__PRESET`: Optional preset that primes defaults for provider/model/endpoint. Supported values: `OPENAI`, `OLLAMA`, `OPENROUTER`. Explicit variables are never overridden by the preset.
- `LOCO_PLUGINS_PATH`: Overrides plugins directory for CLI and Plugin Manager. Used only when `--plugins-path` is not provided. Default `%APPDATA%/Loco/Plugins`.

Provider-specific variables (informational; core prefers `LOCO_LLM__*` and does not read these):

- `OPENAI_API_KEY`, `OPENAI_BASE_URL`
- `ANTHROPIC_API_KEY`, `ANTHROPIC_BASE_URL`
- `GEMINI_API_KEY` or `GOOGLE_API_KEY`, `GEMINI_BASE_URL`
- `OLLAMA_BASE_URL`

Note: Hosts load a `.env` file early at startup (searching from `AppContext.BaseDirectory` upward) and do not override already-set OS environment variables.


## Best Practices

1. **Rule Design**
   - Keep rules simple and focused
   - Use conditions to prevent unnecessary executions
   - Set appropriate priorities for rule ordering

2. **Performance**
   - Use caching for expensive operations
   - Implement async/await properly
   - Dispose resources correctly

3. **Error Handling**
   - Always handle exceptions gracefully
   - Log errors with appropriate context
   - Implement retry logic for transient failures

4. **Security**
   - Validate all user input
   - Use parameterized queries
   - Implement proper authentication for webhooks

## Code Examples

### Complete Rule Creation Example

```csharp
public async Task CreateCompleteAutomationRule()
{
    var rule = new Rule
    {
        Id = Guid.NewGuid().ToString(),
        Name = "Smart File Backup",
        Description = "Backs up important files when changes are detected",
        Enabled = true,
        Priority = 1,
        Trigger = new Trigger
        {
            Type = "file.changed",
            Config = new Dictionary<string, object>
            {
                { "path", @"C:\ImportantDocuments" },
                { "pattern", "*.docx" },
                { "recursive", true }
            }
        },
        Conditions = new List<Condition>
        {
            new Condition
            {
                Type = "file.size",
                Config = new Dictionary<string, object>
                {
                    { "operator", "less_than" },
                    { "value", 10485760 } // 10MB
                }
            }
        },
        Actions = new List<Action>
        {
            new Action
            {
                Type = "file.copy",
                Config = new Dictionary<string, object>
                {
                    { "source", "${trigger.file_path}" },
                    { "destination", @"D:\Backup\${date:yyyy-MM-dd}\${trigger.file_name}" }
                }
            },
            new Action
            {
                Type = "notification.show",
                Config = new Dictionary<string, object>
                {
                    { "title", "Backup Complete" },
                    { "message", "File ${trigger.file_name} has been backed up" }
                }
            },
            new Action
            {
                Type = "log",
                Config = new Dictionary<string, object>
                {
                    { "message", "Backed up ${trigger.file_path} at ${date:HH:mm:ss}" },
                    { "level", "Information" }
                }
            }
        },
        Metadata = new Dictionary<string, object>
        {
            { "created_by", "user@example.com" },
            { "category", "backup" },
            { "tags", new[] { "files", "backup", "automation" } }
        }
    };

    var automationService = serviceProvider.GetRequiredService<IAutomationService>();
    // Serialize to JSON to use the JSON-based API
    var json = System.Text.Json.JsonSerializer.Serialize(rule);
    bool success = await automationService.AddRuleFromJsonAsync(json);
    
    if (success)
    {
        Console.WriteLine($"Rule '{rule.Name}' created successfully with ID: {rule.Id}");
    }
}
```

## API Response Formats

### Success Response

```json
{
  "success": true,
  "data": {
    "id": "rule-123",
    "name": "Morning Routine",
    "status": "active"
  },
  "timestamp": "2025-08-15T10:30:00Z"
}
```

### Error Response

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid rule configuration",
    "details": [
      "Trigger type 'invalid_type' is not recognized",
      "Action configuration is missing required field 'path'"
    ]
  },
  "timestamp": "2025-08-15T10:30:00Z"
}
```

## Versioning

The API version is reported by the root endpoint (`/`). Maintain a single evolving version line; see the repository releases for packaged artifacts.
