# Loco Developer Guide

This guide provides detailed information for developers working with the Loco automation platform.

## Development Environment

### Prerequisites
- .NET 8 SDK or later
- Windows 10/11 or Windows Server 2019+
- Visual Studio 2022, Visual Studio Code, or another compatible IDE

### Getting Started
1. Clone the repository
2. Restore dependencies: `dotnet restore`
3. Build the solution: `dotnet build`
4. Run tests: `dotnet test`

## Project Structure

### Core Components
- **Loco.Core**: Core automation engine
  - `SimpleLightEngine`: Main automation engine
  - `Models/`: Data models and interfaces
  - `Configuration/`: Configuration management
  - `Interfaces/`: Core interfaces

- **Loco.Cli**: Command-line interface
  - `Program.cs`: Main CLI application with comprehensive commands

- **Loco.Web**: RESTful API server
  - `Program.cs`: Web API endpoints and middleware

### Key Classes
- `SimpleLightEngine`: Core automation engine
- `SimpleFlow`: Flow definition and execution
- `LightTrigger`: Trigger definitions
- `LightAction`: Action definitions
- `LocoConfig`: Configuration management
- `ActionExecutorFactory`: Factory for action executors

## Configuration

### Environment Variables
- `LOCO_CONFIG_PATH`: Path to custom configuration file
- `LOCO_LOG_LEVEL`: Logging level (Debug, Information, Warning, Error)
- `LOCO_RulesFilePath`: Override rule storage location (default `%APPDATA%/Loco/rules.json`)

#### LLM Settings / LLM 設定
- `LocoConfig` prefers the double-underscore environment variables and falls back to legacy single-underscore names when necessary.
- `LocoConfig` はダブルアンダースコア形式を優先し、必要に応じて旧来のシングルアンダースコア形式へフォールバックします。

| Setting | Description (EN) | 説明 (JA) | Environment Variables |
| --- | --- | --- | --- |
| Provider | LLM provider identifier | LLM プロバイダー識別子 | `LOCO_LLM__PROVIDER`, `LOCO_LLM_PROVIDER` |
| Model | Default model name | 既定モデル名 | `LOCO_LLM__MODEL`, `LOCO_LLM_MODEL` |
| API key | Credential (masked in CLI/API output) | 認証キー（CLI/API 表示時にマスク） | `LOCO_LLM__APIKEY`, `LOCO_LLM_API_KEY` |

Provider-specific fallbacks such as `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, and `OLLAMA_BASE_URL` remain supported when the corresponding `LOCO_LLM__*` variable is not set.

## API Referencefiguration File
Configuration is stored in JSON format with direct property mapping. `LocoConfig` exposes the loaded file path through `SourceConfigPath` for diagnostics and resolves relative entries (for example `workingDirectory`, `allowedPaths`, `forbiddenPaths`) against the directory containing the configuration file. Invalid or duplicate path entries are skipped and logged in `PathResolutionWarnings`, and `HasPathResolutionWarnings` provides a quick status indicator for tooling.

```json
{
  "maxConcurrentFlows": 10,
  "logDirectory": "C:/Projects/Loco/Logs",
  "enableFileLogging": true,
  "enableConsoleLogging": true
}
```

## API Reference

### Core Engine API
```csharp
// Create and start engine with persistent rule store
var ruleStore = new PersistentRuleStore();
var engine = new SimpleLightEngine(logger, new LocoConfig(), ruleStore);
await engine.StartAsync();

// Create a rule
var ruleId = engine.CreateRule(
    "My Rule",
    new LightTrigger { Type = "manual" },
    new[] { new LightAction { Type = "log", Parameters = new() { ["message"] = "Hello World" } } }
);

// Execute a rule
await engine.ExecuteRuleAsync(ruleId);

// Check health
var isHealthy = await engine.IsHealthyAsync();
var status = engine.GetEngineStatus();
```

### CLI Command Status / CLI コマンド状況

| Command | Status | Notes |
| --- | --- | --- |
| `start`, `test`, `health`, `info` | Implemented | `--rules-path` supported for alternate stores |
| `preset system`, `preset daily`, `preset cleanup` | Implemented | Uses shared `RunPresetAsync` (supports `--rules-path`) |
| `rule list`, `rule enable`, `rule disable`, `rule delete` | Implemented | Persistent rule management, JSON output supported |
| `files search`, `files stats` | Implemented | File discovery and directory statistics |
| `logs view`, `logs stats`, `logs search`, `logs clear` | Implemented | Log inspection and maintenance |
| `quick log`, `quick stats` | Implemented | Convenience utilities |
| `version` | Implemented | Displays CLI version information |
| `cache`, `monitor`, `process`, `backup`, `watch`, `schedule`, `config`, `history` | Implemented | Each supports existing documented options |

### Web API Endpoints
- `GET /api/health`: System health check (implemented)
- `GET /api/metrics`: System metrics (implemented)
- `POST /api/workflows/execute`: Workflow execution stub (returns placeholder)
- `GET /api/schedules`: Scheduling stub endpoint
- `POST /api/schedules`: Scheduling stub endpoint
- `GET /`: Static dashboard shell served from `src/Loco.Web/wwwroot/`

## Testing

### Running Tests
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Loco.Core.Tests

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Writing Tests
Tests should follow the AAA pattern (Arrange, Act, Assert):

```csharp
[Test]
public async Task ExecuteRuleAsync_ValidRule_ReturnsTrue()
{
    // Arrange
    var engine = new SimpleLightEngine();
    await engine.StartAsync();
    var ruleId = engine.CreateRule("Test", new LightTrigger(), new[] { new LightAction() });

    // Act
    var result = await engine.ExecuteRuleAsync(ruleId);

    // Assert
    Assert.True(result);
}
```

## Logging

### Configuration
Logging is configured through the `LocoConfig` class:

```json
{
  "logLevel": "Information",
  "enableFileLogging": true,
  "enableConsoleLogging": true,
  "logDirectory": "C:/Projects/Loco/Logs",
  "logRetentionDays": 30
}
```

### Log Levels
- `Debug`: Detailed diagnostic information
- `Information`: General information messages
- `Warning`: Warning conditions
- `Error`: Error conditions

## Performance Considerations

### Memory Management
- The engine uses concurrent collections for thread safety
- Memory optimization can be enabled in configuration
- Monitor memory usage with the `monitor` command

### Execution Limits
- Configurable maximum concurrent flows
- Rate limiting per minute
- Timeout settings for operations

## Troubleshooting

### Common Issues
1. **Engine won't start**: Check configuration and dependencies
2. **Rules not executing**: Verify trigger and action configurations
3. **Memory issues**: Adjust memory limits in configuration
4. **Log files not created**: Check log directory permissions

### Debug Mode
Enable debug logging for detailed diagnostic information:

```json
{
  "logLevel": "Debug",
  "enableConsoleLogging": true
}
```

## Contributing

## UI Guidelines / UIガイドライン
- Refer to `docs/STYLE_GUIDE.md` for design tokens, layout, and accessibility patterns when building Web UI or documentation visuals.

### Code Style
- Follow C# coding conventions
- Use nullable reference types
- Include XML documentation
- Write comprehensive tests

### Pull Request Process
1. Create a feature branch
2. Write tests for new functionality
3. Update documentation
4. Ensure all tests pass
5. Submit pull request with description

## Deployment

### CLI Deployment
```bash
# Create release build
dotnet publish src/Loco.Cli --configuration Release --output ./publish

# Run from publish directory
./publish/Loco.Cli.exe start
```

### Web API Deployment
```bash
# Create release build
dotnet publish src/Loco.Web --configuration Release --output ./publish

# Run web server
./publish/Loco.Web.exe
```

## Support

For support and questions:
- Check the documentation
- Review existing issues
- Create new issues for bugs and features
- Join the development discussions
