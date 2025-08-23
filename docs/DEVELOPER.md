# Loco Developer Guide

## Introduction

Welcome to the Loco development guide. This document provides comprehensive information for developers who want to contribute to Loco or extend its functionality.

## Architecture Overview

Loco follows Clean Architecture principles with clear separation of concerns:

```
┌─────────────────────────────────────────────────┐
│                  Presentation                    │
│         (CLI, Web UI, Mobile, API)              │
├─────────────────────────────────────────────────┤
│                  Application                     │
│        (Services, Commands, Queries)            │
├─────────────────────────────────────────────────┤
│                    Domain                        │
│      (Entities, Interfaces, Business Logic)     │
├─────────────────────────────────────────────────┤
│                Infrastructure                    │
│    (Data Access, External Services, Plugins)    │
└─────────────────────────────────────────────────┘
```

## Development Setup

### Prerequisites

- .NET 8 SDK
- Visual Studio 2022 / VS Code / Rider
- Git
- Node.js 18+ (for Web UI)
- Docker (optional, for containerized development)

### Getting Started

1. Clone the repository:
```bash
git clone https://github.com/loco/loco.git
cd loco
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Build the solution:
```bash
dotnet build
```

4. Run tests:
```bash
dotnet test
```

5. Run the application:
```bash
dotnet run --project src/Loco.Cli/Loco.Cli.csproj
```

## Project Structure

```
Loco/
├── src/
│   ├── Loco.Core/          # Core domain & services (plugins are in Loco.Core.Plugins.*)
│   ├── Loco.Automation/    # Automation engine & interfaces
│   ├── Loco.Cli/           # Command-line interface
│   └── Loco.Cognitive/     # Cognitive components (e.g., visualization)
├── web/                    # Next.js web UI
├── mobile/                 # React Native mobile app
├── tests/
│   ├── Loco.Core.Tests/    # Unit tests (core)
│   ├── Loco.Automation.Tests/
│   ├── Loco.Tests/         # Additional tests/benchmarks
│   └── Loco.LoadTests/     # Load tests (k6)
├── docs/                   # Documentation
├── examples/               # Example flows and rules
└── schemas/                # JSON Schemas
```

## Core Concepts

### Rules

Rules are the fundamental building blocks of automation:

```csharp
// See src/Loco.Core/Models/AutomationDsl.cs
public class Rule
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool Enabled { get; set; }
    public TriggerDefinition Trigger { get; set; }
    public List<ConditionDefinition> Conditions { get; set; }
    public List<ActionDefinition> Actions { get; set; }
    public Dictionary<string, object> Variables { get; set; }
    public PermissionSet Permissions { get; set; }
    public RuleMetadata Metadata { get; set; }
    public int? ExecutionCount { get; set; }
}
```

Rule JSON example / ルールJSON例:

```json
{
  "id": "rule-hello",
  "name": "Daily greeting",
  "description": "At 07:00 notify Hello",
  "enabled": true,
  "trigger": {
    "type": "time",
    "parameters": { "hour": 7, "minute": 0 },
    "priority": 5
  },
  "conditions": [],
  "actions": [
    {
      "type": "notification",
      "parameters": { "title": "Loco", "message": "Hello" },
      "timeout": 10000,
      "retryCount": 0,
      "continueOnError": false
    }
  ],
  "variables": {},
  "permissions": {
    "network": false,
    "fileSystem": false,
    "shell": false,
    "llm": false,
    "notification": true,
    "allowedDomains": [],
    "allowedPaths": []
  },
  "metadata": {
    "createdAt": "2025-08-01T00:00:00Z",
    "updatedAt": "2025-08-01T00:00:00Z",
    "version": "1.0.0",
    "author": "dev",
    "tags": ["example"],
    "source": "docs"
  },
  "executionCount": 0
}
```

### Flows

Flows represent complex workflows with multiple steps:

```csharp
// See src/Loco.Core/Models/FlowDefinition.cs
public class FlowDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool Enabled { get; set; }
    public List<TriggerDefinition> Triggers { get; set; }
    public List<ConditionDefinition> Conditions { get; set; }
    public List<ActionDefinition> Actions { get; set; }
    public Dictionary<string, object> Variables { get; set; }
    public PermissionSet Permissions { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    public int Downloads { get; set; }
}
```

Flow JSON example / フローJSON例:

```json
{
  "id": "flow-quick",
  "name": "Quick Flow",
  "description": "Time trigger then notify",
  "enabled": true,
  "triggers": [
    { "id": "t1", "type": "time.schedule", "parameters": { "hour": 7, "minute": 0 } }
  ],
  "conditions": [],
  "actions": [
    {
      "id": "a1",
      "type": "notification.show",
      "parameters": { "title": "Loco", "message": "Hello" },
      "continueOnError": false,
      "retryCount": 0,
      "timeoutMs": 10000
    }
  ],
  "variables": {},
  "permissions": {
    "network": false,
    "fileSystem": false,
    "shell": false,
    "llm": false,
    "allowedDomains": [],
    "allowedPaths": []
  },
  "createdAt": "2025-08-01T00:00:00Z",
  "updatedAt": "2025-08-01T00:00:00Z",
  "metadata": {},
  "downloads": 0
}
```

### Plugins

Plugins extend Loco's functionality:

```csharp
// Legacy system: see src/Loco.Core/Plugins/PluginSystem.cs (namespace Loco.Core.Plugins.Legacy)
// Modern system (used by CLI): see src/Loco.Core/Plugins/PluginManager.cs
public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string Description { get; }
    PluginManifest Manifest { get; }
    Task InitializeAsync(IPluginHostContext context);
    Task ShutdownAsync();
}
```

Key types:

- PluginManifest: Id, Name, Version, Author, Description, EntryPoint, Dependencies, Permissions, Configuration
- PluginPermissions: Network, FileSystem, Process, Llm, AllowedDomains, AllowedPaths
- IPluginHostContext: Logger, FileSystem, HttpClient, RegisterAction(string name, Type actionType), RegisterTrigger(Type triggerType)

日本語:
- レガシー実装は `Loco.Core.Plugins.Legacy` 名前空間（`PluginSystem.cs`）。
- 現行実装（CLIが使用）は `src/Loco.Core/Plugins/PluginManager.cs`。
- 既定のプラグインディレクトリは `
  %APPDATA%/Loco/Plugins`（`--plugins-path` 未指定時）。
- プラグインは `IPlugin` を実装し、`InitializeAsync(...)` で登録処理を行います。

English:
- Legacy implementation lives under `Loco.Core.Plugins.Legacy` (`PluginSystem.cs`).
- Modern implementation used by the CLI is `src/Loco.Core/Plugins/PluginManager.cs`.
- Default plugins directory is `%APPDATA%/Loco/Plugins` when `--plugins-path` is omitted.
- Implement `IPlugin` and register in `InitializeAsync(...)`.

Environment override / 環境変数の上書き:
- EN: When `--plugins-path` is omitted, the environment variable `LOCO_PLUGINS_PATH` is used if set.
- JA: `--plugins-path` を省略した場合、環境変数 `LOCO_PLUGINS_PATH` が設定されていればそれが使用されます。
- Precedence / 優先順位: explicit `--plugins-path` > `LOCO_PLUGINS_PATH` > default `%APPDATA%/Loco/Plugins`.

#### Plugin paths helper / プラグインパスのヘルパー

英語:
- Path resolution is centralized in `src/Loco.Core/Plugins/PluginPaths.cs`.
- Use `PluginPaths.GetEffectivePluginsDirectory(string? provided = null)` to apply precedence (explicit > env > default) and `PluginPaths.EnsureDirectory(path)` to create if missing.
- `GetDefaultPluginsDirectory()` returns the default path only (no override), mainly for tests and documentation.

日本語:
- プラグインパスの解決は `src/Loco.Core/Plugins/PluginPaths.cs` に集約されています。
- 優先順位（明示指定 > 環境変数 > 既定）を適用するには `PluginPaths.GetEffectivePluginsDirectory(string? provided = null)` を使用し、存在しない場合は `PluginPaths.EnsureDirectory(path)` で作成します。
- `GetDefaultPluginsDirectory()` は既定値のみを返すため（上書きなし）、主にテストやドキュメントに使用します。

Example / 例:
```csharp
using Loco.Core.Plugins;

// Applies precedence: provided > LOCO_PLUGINS_PATH > default
var pluginsDir = PluginPaths.GetEffectivePluginsDirectory();
PluginPaths.EnsureDirectory(pluginsDir);
```

### Action Execution Contracts

See `src/Loco.Core/Interfaces/IAction.cs` and `src/Loco.Core/Models/ActionContext.cs`.

- __IAction__
  - `string Id { get; }`
  - `Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)`

- __ActionContext__
  - `Variables`, `TriggerContext`, `Parameters` (all `Dictionary<string, object>`)
  - `ILogger Logger`
  - `ExecutionStartTime`, `ExecutionId`

- __ActionResult__
  - `Success`, `Message`, `Data`, `OutputVariables`, `Exception`, `ExecutedAt`, `ExecutionTimeMs`

## Coding Standards

### C# Conventions

1. **Naming**
   - PascalCase for public members
   - camelCase for private fields
   - _underscore prefix for private fields
   - Async suffix for async methods

2. **File Organization**
   - One type per file
   - File name matches type name
   - Organize by feature, not by type

3. **Comments**
   - XML documentation for public APIs
   - Inline comments for complex logic
   - TODO comments with issue numbers

### Example Code Style

```csharp
namespace Loco.Core.Services;

/// <summary>
/// Manages automation rules and their execution.
/// </summary>
public class AutomationService : IAutomationService
{
    private readonly ILogger<AutomationService> _logger;
    private readonly IAutomationRuleEngine _ruleEngine;
    
    public AutomationService(
        ILogger<AutomationService> logger,
        IAutomationRuleEngine ruleEngine)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
    }
    
    /// <summary>
    /// Adds a new automation rule.
    /// </summary>
    /// <param name="rule">The rule to add.</param>
    /// <returns>True if successful.</returns>
    public async Task<bool> AddRuleAsync(Rule rule)
    {
        if (rule == null)
        {
            throw new ArgumentNullException(nameof(rule));
        }
        
        try
        {
            _logger.LogInformation("Adding rule {RuleName}", rule.Name);
            return await _ruleEngine.AddRuleAsync(rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add rule {RuleName}", rule.Name);
            throw;
        }
    }
}
```

## Testing

### Unit Testing

Use xUnit for unit tests:

```csharp
public class AutomationServiceTests
{
    private readonly Mock<ILogger<AutomationService>> _loggerMock;
    private readonly Mock<IAutomationRuleEngine> _engineMock;
    private readonly AutomationService _service;
    
    public AutomationServiceTests()
    {
        _loggerMock = new Mock<ILogger<AutomationService>>();
        _engineMock = new Mock<IAutomationRuleEngine>();
        _service = new AutomationService(_loggerMock.Object, _engineMock.Object);
    }
    
    [Fact]
    public async Task AddRuleAsync_Should_Return_True_When_Successful()
    {
        // Arrange
        var rule = new Rule { Id = "test", Name = "Test Rule" };
        _engineMock.Setup(x => x.AddRuleAsync(It.IsAny<Rule>()))
                   .ReturnsAsync(true);
        
        // Act
        var result = await _service.AddRuleAsync(rule);
        
        // Assert
        result.Should().BeTrue();
        _engineMock.Verify(x => x.AddRuleAsync(rule), Times.Once);
    }
}
```

### Integration Testing

Integration tests verify component interactions:

```csharp
public class IntegrationTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;
    
    public IntegrationTests(TestFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public async Task Complete_Workflow_Should_Execute_Successfully()
    {
        // Test complete workflow from rule creation to execution
    }
}
```

### Automated Verification (Plugins Path) / 自動検証（プラグインパス）

英語:
- The integration script runs plugins path verification: it checks precedence and directory creation for default, env override, and explicit path.
- How to run from repo root:
  ```powershell
  .\test-integration.ps1
  ```
- CI runs this verification automatically on Windows runners (`windows-latest`). See workflow step `Verify Plugins Path precedence (Windows)` in `.github/workflows/build-release.yml`.

日本語:
- 統合スクリプトでプラグインパス検証を実行します。既定/環境変数/明示指定の優先順位とディレクトリ作成を確認します。
  - 実行方法（リポジトリルート）:
  ```powershell
  .\test-integration.ps1
  ```
  - CI は Windows ランナー（`windows-latest`）上でこの検証を自動実行します（`.github/workflows/build-release.yml` の `Verify Plugins Path precedence (Windows)`）。

### CI/CD Workflow Notes / CI/CD ワークフロー注意事項

English:

- __Plugins path verification__: The workflow runs `tools/verify-plugins-path.ps1` on Windows with a 5-minute timeout. Command: `./tools/verify-plugins-path.ps1 -VerboseMode`. It validates precedence (explicit > env `LOCO_PLUGINS_PATH` > default) and ensures directories are created.
- __Diagnostics__: The verification step starts a PowerShell transcript and uploads it as an artifact on failure (`verify-plugins-path-transcript`). The script also enforces per-command timeouts (default 90 s, CI uses 60 s) to prevent hangs.
- __Execution hardening__: The script invokes `dotnet` natively (not via nested PowerShell), reads stdout/stderr asynchronously, closes stdin immediately, and sets `Set-StrictMode -Version Latest` with `-NoLogo/-NoProfile/-NonInteractive` to avoid pipe buffering issues and interactive prompts.
- __Diagnostics__: Verbose mode logs working directory and environment overrides for each subprocess, measures and prints elapsed time per command, captures output in UTF-8, and clamps per-command timeout to minimum 1 second to avoid misconfiguration.
- __Secret gating pattern__: Secrets are not referenced directly in `if:`. Each publish job first checks for secret presence using expressions (e.g., `${{ secrets.DOCKER_USERNAME != '' }}`) mapped to boolean envs, emits an `ok=true/false` output, and gates subsequent steps with `if: steps.<id>.outputs.ok == 'true'`.
- __Artifacts__: Uses `actions/upload-artifact@v4` and `actions/download-artifact@v4`. Winget job downloads the Windows artifact, expands the ZIP into `./output`, then points `vedantmgoyal2009/winget-releaser@v2` at `./output/Loco.Cli.exe`.
- __Caching__: Uses `actions/cache@v4` for NuGet cache (`~/.nuget/packages`).
- __Docker__: Login and push are gated by the Docker creds check (same boolean output pattern).
- __Local reproduction__: On Windows PowerShell from repo root:
  ```powershell
  dotnet restore --nologo
  dotnet build -c Release --nologo
  dotnet test -c Release --no-build --nologo --verbosity normal
  powershell -ExecutionPolicy Bypass -File .\tools\verify-plugins-path.ps1 -VerboseMode
  # Run a subset for faster debugging, e.g. only r1 and r2 cases
  powershell -ExecutionPolicy Bypass -File .\tools\verify-plugins-path.ps1 -VerboseMode -Only r1,r2
  ```

日本語:

- __プラグインパス検証__: Windows で `tools/verify-plugins-path.ps1` を 5 分のタイムアウト付きで実行します（`-VerboseMode`）。優先順位（明示 > 環境変数 `LOCO_PLUGINS_PATH` > 既定）とディレクトリ作成を検証します。
- __診断__: 検証ステップで PowerShell トランスクリプトを開始し、失敗時はアーティファクト（`verify-plugins-path-transcript`）として保存します。スクリプト内では各コマンドに個別タイムアウト（既定 90 秒、CI では 60 秒）を設定し、ハングを防止します。
- __実行の堅牢化__: スクリプトは PowerShell 経由ではなく `dotnet` をネイティブ実行し、標準出力/標準エラーを非同期読み取り、標準入力を即時クローズします。さらに `Set-StrictMode -Version Latest` と `-NoLogo/-NoProfile/-NonInteractive` を設定し、パイプ詰まりや対話待ちを回避します。
- __診断強化__: 詳細モードでは各子プロセスの作業ディレクトリーと環境変数上書きを記録し、コマンドごとの経過時間を出力します。UTF-8 で出力を取得し、タイムアウトは最小 1 秒にクランプして誤設定を防止します。
- __シークレットの制御__: `if:` でシークレットを直接参照しません。最初に存在チェック（`${{ secrets.DOCKER_USERNAME != '' }}` 等）をブール値として評価し、`ok=true/false` を出力。以降の手順は `if: steps.<id>.outputs.ok == 'true'` でゲートします。
- __アーティファクト__: `actions/upload-artifact@v4`/`actions/download-artifact@v4` を使用。Winget ジョブは Windows アーティファクトをダウンロードし ZIP を `./output` に展開後、`./output/Loco.Cli.exe` を指定して投稿します。
- __キャッシュ__: NuGet キャッシュに `actions/cache@v4` を使用します。
- __Docker__: ログイン/プッシュは同様のブール出力パターンでゲートします。
- __ローカル再現__: Windows PowerShell（リポジトリルート）:
  ```powershell
  dotnet restore --nologo
  dotnet build -c Release --nologo
  dotnet test -c Release --no-build --nologo --verbosity normal
  powershell -ExecutionPolicy Bypass -File .\tools\verify-plugins-path.ps1 -VerboseMode
  ```

Troubleshooting / トラブルシュート:

- __Hang in verification__: CI will auto-timeout after 5 minutes. Re-run locally with `-VerboseMode` to see detailed logs. Ensure PowerShell execution policy allows the script and the CLI has been built (`dotnet build -c Release`).
- __Path mismatch__: Confirm `LOCO_PLUGINS_PATH` is set as intended, or pass `--plugins-path` explicitly. Use `loco plugins-path -v` to view the effective path and source.

## Performance Optimization

### Guidelines

1. **Async/Await**
   - Use async/await for I/O operations
   - Avoid blocking calls
   - ConfigureAwait(false) in library code

2. **Memory Management**
   - Use object pooling for frequently created objects
   - Implement IDisposable correctly
   - Avoid large object allocations

3. **Caching**
   - Cache expensive computations
   - Use MemoryCache for in-process caching
   - Implement cache invalidation

### Performance Monitoring

```csharp
public class PerformanceMonitor
{
    private readonly ILogger<PerformanceMonitor> _logger;
    
    public async Task<T> MeasureAsync<T>(
        Func<Task<T>> operation,
        string operationName)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await operation();
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "Operation {OperationName} completed in {ElapsedMs}ms",
                operationName,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
```

## Plugin Development

### Creating a Plugin

1. Create a new class library project:
```bash
dotnet new classlib -n MyPlugin
```

2. Add reference to Loco.Core:
```xml
<ItemGroup>
  <ProjectReference Include="../Loco.Core/Loco.Core.csproj" />
</ItemGroup>
```

3. Implement the plugin:
```csharp
using Loco.Core.Plugins;
using Loco.Core.Interfaces;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Models;

public sealed class MyPlugin : PluginBase
{
    public override string Id => "my-plugin";
    public override string Name => "My Plugin";
    public override string Version => "1.0.0";
    public override string Description => "Example plugin";
    public override PluginManifest Manifest => new PluginManifest
    {
        Id = Id,
        Name = Name,
        Version = Version,
        Description = Description,
        EntryPoint = "MyPlugin.dll",
        Permissions = new PluginPermissions
        {
            Network = true,
            AllowedDomains = new() { "api.example.com" },
            FileSystem = true,
            AllowedPaths = new() { "C:\\Data\\MyPlugin\\" }
        }
    };

    public override async Task InitializeAsync(IPluginHostContext context)
    {
        await base.InitializeAsync(context);
        context.RegisterAction("my.custom.action", typeof(MyCustomAction));
    }
}

public sealed class MyCustomAction : IAction
{
    public string Id => "my.custom.action";

    public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
    {
        // Do work
        return Task.FromResult(new ActionResult { Success = true, Message = "OK" });
    }
}
```

### Plugin Manifest

Create a `manifest.json`:
```json
{
  "id": "my-plugin",
  "name": "My Plugin",
  "version": "1.0.0",
  "author": "Your Name",
  "description": "Plugin description",
  "entryPoint": "MyPlugin.dll",
  "permissions": {
    "network": true,
    "fileSystem": true,
    "process": false,
    "llm": false,
    "allowedDomains": ["api.example.com"],
    "allowedPaths": ["C:\\Data\\MyPlugin\\\\"]
  },
  "dependencies": [],
  "configuration": {}
}
```

日本語注意点:
- `manifest.json` の `version` は `X.Y.Z` 形式で必須です。
- 許可ドメインとパスは必要最小限に設定してください。

English notes:
- `version` must be `X.Y.Z`.
- Restrict `allowedDomains`/`allowedPaths` to least privilege.

### Plugin Sandbox and Permissions

- File access goes via `IPluginFileSystem` (SandboxedFileSystem). Paths must match `AllowedPaths`.
- Network access goes via `IPluginHttpClient` (SandboxedHttpClient). Hosts must match `AllowedDomains`.
- Actions registered via `IPluginHostContext.RegisterAction` are available to rules.
- Triggers must implement `Loco.Core.Triggers.IRuntimeTrigger`.

    }
    
    public sealed class MyCustomAction : IAction
    {
        public string Id => "my.custom.action";
    
        public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
        {
            // Do work
            return Task.FromResult(new ActionResult { Success = true, Message = "OK" });
        }
    }
    ```
    
    ### Plugin Manifest
    
    Create a `manifest.json`:
    ```json
    {
      "id": "my-plugin",
      "name": "My Plugin",
      "version": "1.0.0",
      "author": "Your Name",
      "description": "Plugin description",
      "entryPoint": "MyPlugin.dll",
      "permissions": {
        "network": true,
        "fileSystem": true,
        "process": false,
        "llm": false,
        "allowedDomains": ["api.example.com"],
        "allowedPaths": ["C:\\Data\\MyPlugin\\\\"]
      },
      "dependencies": [],
      "configuration": {}
    }
    ```
    
    日本語注意点:
    - `manifest.json` の `version` は `X.Y.Z` 形式で必須です。
    - 許可ドメインとパスは必要最小限に設定してください。
    
    English notes:
    - `version` must be `X.Y.Z`.
    - Restrict `allowedDomains`/`allowedPaths` to least privilege.
    
    ### Plugin Sandbox and Permissions
    
    - File access goes via `IPluginFileSystem` (SandboxedFileSystem). Paths must match `AllowedPaths`.
    - Network access goes via `IPluginHttpClient` (SandboxedHttpClient). Hosts must match `AllowedDomains`.
    - Actions registered via `IPluginHostContext.RegisterAction` are available to rules.
    - Triggers must implement `Loco.Core.Triggers.IRuntimeTrigger`.
    
    - __検出__: `PluginManager` はプラグインディレクトリ配下の各フォルダーを走査します（`LoadPluginsAsync()`）。
    - __検証__: フォルダー直下に `manifest.json` が必須。必須項目は `id`、`name`、`version`（X.Y.Z）、`entryPoint`（DLL）。`ValidateManifest()` を参照。
    - __サンドボックス読込__: `entryPoint` の DLL を `PluginAssemblyLoadContext` に分離ロードします（`PluginSandbox.LoadPluginAsync()`）。
    - __初期化__: 権限制御付き `FileSystem`/`HttpClient` を含む `IPluginHostContext` を `InitializeAsync()` に渡します。ここで `RegisterAction()`/`RegisterTrigger()` を呼び出します。
    - __実行__: 登録したアクションは、ルール/フローから参照されたときにルールエンジンにより実行されます。本実装では汎用 `Execute` API は提供しません。
    - __終了__: アンロード時に `ShutdownAsync()` が呼ばれ、読み込みコンテキストを解放します（`UnloadPluginAsync()`）。
    
    Notes / 注意
    
    - Default plugins directory when `--plugins-path` is not provided: `%APPDATA%/Loco/Plugins` (Windows). See `PluginManager(..., pluginsPath)` defaulting in `PluginManager.cs`.
    - `PluginPermissions` includes `Network`, `FileSystem`, `Process`, `Llm`, `AllowedDomains`, `AllowedPaths`. Current enforcement is for network/domains and file paths via sandbox wrappers.
    
    ### Plugin Debugging / プラグインのデバッグ
    
    English
    
    - __Using CLI__: The `test-plugin` command loads plugins and runs a test rule.
      - Command: `loco test-plugin --plugins-path <dir> [--rule-path <json>]`
      - Defaults: `--rule-path` defaults to `examples/rules/plugin-test-rule.json`.
      - Requires: `--plugins-path`. Missing it sets exit code 1. See `src/Loco.Cli/Program.cs`.
    - __Steps__:
      1. Build your plugin DLL and prepare a folder: `<plugins>\MyPlugin\{ manifest.json, MyPlugin.dll }`.
      2. Run: `loco test-plugin --plugins-path "<plugins>"`.
      3. Observe logs: initialization, registration, rule add/trigger, and any plugin file/network access denials.
    - __IDE debug__: Set startup to `Loco.Cli`, args: `test-plugin --plugins-path "<plugins>" --rule-path "examples/rules/plugin-test-rule.json"`. Set breakpoints in plugin code.
    
    日本語
    
    - __CLI利用__: `test-plugin` はプラグインを読み込み、テスト用ルールを実行します。
      - 形式: `loco test-plugin --plugins-path <dir> [--rule-path <json>]`
      - 既定: `--rule-path` は `examples/rules/plugin-test-rule.json`。
      - 必須: `--plugins-path`。未指定は終了コード 1（`src/Loco.Cli/Program.cs`）。
    - __手順__:
      1. プラグインDLLをビルドし、`<plugins>\MyPlugin\` に `manifest.json` と DLL を配置。
      2. `loco test-plugin --plugins-path "<plugins>"` を実行。
      3. ログで初期化/登録/ルール実行、アクセス拒否（許可外ドメイン/パス）がないか確認。
    - __IDEデバッグ__: スタートアップを `Loco.Cli` に設定し、引数 `test-plugin --plugins-path "<plugins>" --rule-path "examples/rules/plugin-test-rule.json"` を指定。プラグインコードにブレークポイントを設定。
    
    ### Plugin Deployment / プラグインの配布・導入
    
    English
    
    - __Packaging__: Distribute as a folder containing at minimum:
      - `manifest.json` (with `id`, `name`, `version`, `entryPoint`)
      - Plugin DLL referenced by `entryPoint`
      - Any dependent DLLs (same folder)
    - __Install__: Copy the plugin folder into the plugins directory. Example:
      - `%APPDATA%/Loco/Plugins/MyPlugin/`
    - __Uninstall__: Stop the app, remove the plugin folder, restart. Programmatic unload exists (`PluginManager.UnloadPluginAsync(id)`), but there is no CLI install/uninstall command at this time.
    - __Manifest example__:
    
    ```json
    {
      "id": "my-plugin",
      "name": "My Plugin",
      "version": "1.0.0",
      "entryPoint": "MyPlugin.dll",
      "description": "Example",
      "permissions": {
        "network": true,
        "allowedDomains": ["api.example.com"],
        "fileSystem": true,
        "allowedPaths": ["C:\\Data\\MyPlugin\\"]
      }
    }
    ```
    
    日本語
    
    - __パッケージ__: フォルダー配布（少なくとも `manifest.json` と `entryPoint` で参照された DLL、依存DLL）。
    - __導入__: プラグインフォルダーをプラグインディレクトリ直下にコピー（例: `%APPDATA%/Loco/Plugins/MyPlugin/`）。
    - __削除__: アプリ停止後にフォルダー削除。プログラムからのアンロード（`UnloadPluginAsync(id)`）は可能ですが、現時点でCLIによる install/uninstall はありません。
    
    ### Security and Permissions Checklist / セキュリティと権限チェック
    
    - __Least privilege__: Restrict `allowedDomains`/`allowedPaths` to what is necessary.
    - __File I/O__: Use `IPluginFileSystem`. Direct `System.IO` calls may be blocked by policy.
    - __HTTP__: Use `IPluginHttpClient`. Non-allowed domains throw `SecurityException`.
    - __Logging__: Use `IPluginHostContext.Logger` for structured logs.
    
    ## Debugging
    
    ### Local Debugging
    
    1. Set breakpoints in Visual Studio/VS Code
    2. Run with debugger attached:
    ```bash
    dotnet run --project src/Loco.Cli --configuration Debug
    ```
    
    ### Logging
    
    Configure logging in `appsettings.json`:
    ```json
    {
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Loco": "Debug",
          "Microsoft": "Warning"
        }
      }
    }
    ```
    
    Use structured logging:
    ```csharp
    _logger.LogInformation("Processing rule {RuleId} with {ActionCount} actions",
        rule.Id, rule.Actions.Count);
    ```
    
    ## Deployment
    
    ### Docker
    
    Build Docker image:
    ```bash
    docker build -t loco:latest .
    ```
    
    Run container:
    ```bash
    docker run -d -p 5000:5000 loco:latest
    ```
    
    ### Publishing
    
    Publish for different platforms:
    ```bash
    # Windows
    dotnet publish -c Release -r win-x64 --self-contained
    
    # Linux
    dotnet publish -c Release -r linux-x64 --self-contained
    
    # macOS
    dotnet publish -c Release -r osx-x64 --self-contained
    ```
    
    ## Contributing
    
    ### Pull Request Process
    
    1. Fork the repository
    2. Create a feature branch
    3. Make your changes
    4. Add tests
    5. Update documentation
    6. Submit pull request
    
    ### Commit Message Format
    
    ```
    <type>(<scope>): <subject>
    
    <body>
    
    <footer>
    ```
    
    Types:
    - feat: New feature
    - fix: Bug fix
    - docs: Documentation
    - style: Code style
    - refactor: Refactoring
    - test: Tests
    - chore: Maintenance
    
    Example:
    ```
    feat(automation): Add support for webhook triggers
    
    Implemented webhook trigger type that allows rules to be triggered
    by external HTTP requests. Includes authentication and validation.
    
    Closes #123
    ```
    
    ## Troubleshooting
    
    ### Common Issues
    
    1. **Build Errors**
       - Clear NuGet cache: `dotnet nuget locals all --clear`
       - Restore packages: `dotnet restore`
    
    2. **Test Failures**
       - Check test output for details
       - Run specific test: `dotnet test --filter FullyQualifiedName~TestName`
    
    3. **Runtime Errors**
       - Check logs in `%APPDATA%/Loco/logs`
       - Enable debug logging
    
    ## Resources
    
    - [API Reference](./API.md)
    - [User Manual](./USER_MANUAL.md)
    - [Example Flows and Rules](../examples/)
    - [JSON Schemas](../schemas/)
    
    ## CLI Commands and Usage / CLIコマンド
    
    Commands (from `src/Loco.Cli/Program.cs`):
    
    - __Global options__
      - `--plugins-path, -p <dir>`: Plugins directory (required for plugin-related ops)
      - `--lang, -l <code>`: UI language (e.g., ja, en)
    
    - __Root options__
      - `--rules, -r <path>`: Rules file or directory (used by `start` to pre-load rules)
    
    - __start__ — Start automation service
      - Options: `--plugins-path`, `--rules`
      - Loads rules from file or directory if provided; also loads saved rules
      - Example: `loco start --plugins-path "%APPDATA%/Loco/Plugins" --rules examples/rules`
    
    - __build__ — Interactive flow builder
      - No options. Produces a flow JSON file
    
    - __quick <args...>__ — Quick flow creation
      - Positional args parser. Example: `loco quick timer 7:00 notify "Good morning"`
    
    - __execute --file <path>__ — Execute a flow JSON
      - Option: `--file` (required; default shown as `flow.json`)
      - Example: `loco execute --file flow.json`
    
    - __convert --text <string>__ — Convert natural language to rule JSON
      - Option: `--text` (required)
      - Saves output as `nl_rule_<timestamp>.json`
    
    - __validate --file <path>__ — Validate a flow JSON
      - Option: `--file` (required; default shown as `flow.json`)
    
    - __list__ — List available flows
      - No options
    
    - __components__ — List available components
      - No options
    
    - __template list__ | __template apply --name <template>__ — Templates
      - `apply` requires `--name`
    
    - __plugins-path__ — Print and ensure effective plugins directory / 既定のプラグインディレクトリを表示・作成
      - EN: Prints the effective plugins directory after applying precedence: explicit `--plugins-path` > `LOCO_PLUGINS_PATH` > default `%APPDATA%/Loco/Plugins` (Windows). Ensures the directory exists.
      - JA: 優先順位（明示 `--plugins-path` > `LOCO_PLUGINS_PATH` > 既定 `%APPDATA%/Loco/Plugins`）を適用した有効なパスを表示し、存在しない場合は作成します。
      - Example: `loco plugins-path` / `loco plugins-path --plugins-path "C:\\Data\\LocoPlugins"`
    
    - __test-plugin__ — Load plugins and run a test rule
      - Options: `--rule-path` (default `examples/rules/plugin-test-rule.json`)
      - Requires: `--plugins-path`
      - Example: `loco test-plugin --plugins-path path\to\plugins`
    
    - __version__ — Show version info
    
    日本語概要:
    - `start` は `--plugins-path` と `--rules` を受け付けます。
    - `test-plugin` は `--plugins-path` が必須です。
    
    ### CLI Reference (EN)
    
    Global/Root Options
    
    | Scope | Option | Alias | Type   | Required | Default | Description |
    |-------|--------|-------|--------|----------|---------|-------------|
    | Global | --plugins-path | -p | string | no | (none) | Directory containing plugin assemblies |
    | Global | --lang | -l | string | no | (none) | UI language code (e.g., ja, en). Note: not fully applied during CLI initialization |
    | Root | --rules | -r | string | no | (none) | Rules file or directory to pre-load when running `start` |
    
    Commands
    
    | Command | Options/Args | Required | Default | Notes |
    |---------|--------------|----------|---------|-------|
    | start | --plugins-path, --rules | no | (none) | Starts services, loads plugins, pre-loads rules from file/dir if provided, then loads saved rules |
    | build | (none) | - | - | Interactive flow builder; saves to chosen .json |
    | quick | args: string[] | - | - | Quick builder parser (e.g., `timer 7:00 notify "Good morning"`, `time 7:30 run "C:\\Windows\\notepad.exe"`) |
    | execute | --file <path> | yes | flow.json | Executes a flow JSON |
    | convert | --text <string> | yes | - | Converts natural language to Rule JSON, saves as `nl_rule_<timestamp>.json` |
    | validate | --file <path> | yes | flow.json | Validates a flow JSON |
    | list | (none) | - | - | Lists flows in `%APPDATA%/Loco/Flows` or current directory fallback |
    | components | (none) | - | - | Lists available components |
    | template list | (none) | - | - | Lists templates |
    | template apply | --name <template> | yes | - | Applies template |
    | test-plugin | --rule-path <json>, --plugins-path | `--rule-path`: no; `--plugins-path`: required | rule-path default: `examples/rules/plugin-test-rule.json` | Fails with error if `--plugins-path` missing |
    | version | (none) | - | - | Prints version info |
    
    Examples
    
    ```powershell
    # Start with plugins and a rules directory
    loco start --plugins-path "$env:APPDATA/Loco/Plugins" --rules examples/rules
    
    # Quick flow creation
    loco quick timer 7:00 notify "Good morning"
    
    # Execute / Validate
    loco execute --file flow.json
    loco validate --file flow.json
    
    ## Configuration (Environment & Paths) / 設定

- __MVP_RULE_STORE_PATH__: File-based rule store path. Default: `$(AppContext.BaseDirectory)/data/rules.json`.
- __Plugins directory__: default `%APPDATA%/Loco/Plugins`. Override precedence: explicit CLI `--plugins-path` > `LOCO_PLUGINS_PATH` > default.
- __LOCO_PLUGINS_PATH__: When set and `--plugins-path` is omitted, this environment variable specifies the plugins directory.
- __Language option__: `--lang` exists but runtime localization is currently limited in CLI initialization.
- __LLM environment variables__ (provider-agnostic; double underscore for nested):
    - `LOCO_LLM__PROVIDER` (e.g., `ollama|openai|anthropic|gemini`)
    - `LOCO_LLM__MODEL`
    - `LOCO_LLM__APIKEY`, `LOCO_LLM__APIENDPOINT`
    - `LOCO_LLM__TEMPERATURE`, `LOCO_LLM__MAXTOKENS`
    - `LOCO_LLM__HTTPTIMEOUTMS` (default 30000, clamped 1000–600000) / HTTP timeout in milliseconds (既定 30000、範囲 1000–600000 にクランプ)
    - `LOCO_LLM__PRESET` (optional: `OPENAI|OLLAMA|OPENROUTER`) primes defaults for `PROVIDER`/`MODEL`/`APIENDPOINT` without overriding explicitly set values

Example:

```bash
# Provider-agnostic
# Preset (optional): primes defaults without overriding explicit values
LOCO_LLM__PRESET=OLLAMA  # or OPENAI, OPENROUTER
LOCO_LLM__PROVIDER=ollama
OLLAMA_BASE_URL=http://localhost:11434
LOCO_LLM__MODEL=llama3:8b
# optional
LOCO_LLM__APIKEY=your-key
LOCO_LLM__HTTPTIMEOUTMS=45000  # 45 seconds

# Provider fallbacks (examples)
OPENAI_API_KEY=sk-...
OPENAI_BASE_URL=https://api.openai.com/v1
ANTHROPIC_API_KEY=...
ANTHROPIC_BASE_URL=https://api.anthropic.com
GEMINI_API_KEY=...
GOOGLE_API_KEY=...
GEMINI_BASE_URL=https://generativelanguage.googleapis.com
OLLAMA_BASE_URL=http://localhost:11434
```

Note: Provider-specific variables like `OPENAI_API_KEY` are not read by Loco core; prefer `LOCO_LLM__*`. They are listed for convenience when using provider SDKs/tools.

__.env loading__ / __.env ロード__

- EN: Hosts load a `.env` file early at startup via `DotEnvLoader.Load()` (CLI `src/Loco.Cli/Program.cs`, Web `src/Loco.Web/Program.cs`, UI `src/Loco.UI/App.xaml.cs`). The loader searches from `AppContext.BaseDirectory` upward (up to 3 parent directories) and does not override already-set OS environment variables.
- JA: ホストは起動時に `DotEnvLoader.Load()` で `.env` を読み込みます（CLI `src/Loco.Cli/Program.cs`、Web `src/Loco.Web/Program.cs`、UI `src/Loco.UI/App.xaml.cs`）。検索は `AppContext.BaseDirectory` から上位（最大3階層）へ。既に設定済みの環境変数は上書きしません。
- EN: No variable interpolation is performed in `.env` (values are literal).
- JA: `.env` では変数展開は行いません（値はリテラルとして扱われます）。

__Preset priming__ / __プリセット初期化__

- EN: When `LOCO_LLM__PRESET` is set, `LlmConfigurationEnv.PrimeEnvironmentFromPreset(...)` fills missing `LOCO_LLM__PROVIDER`, `LOCO_LLM__MODEL`, and `LOCO_LLM__APIENDPOINT` for known presets without overriding explicit values.
- JA: `LOCO_LLM__PRESET` を設定すると、既知のプリセットに応じて不足分の `LOCO_LLM__PROVIDER`/`MODEL`/`APIENDPOINT` を補完します。明示設定は上書きしません。

__CLI inspection__ / __CLI 確認__

- EN: Use `loco llm config` to print the effective LLM configuration (API key redacted). Output includes `HasApiKey` and `Preset` (from `LOCO_LLM__PRESET`) and matches the Web API `/api/llm/config` fields.
- JA: `loco llm config` で有効な LLM 設定を確認できます（APIキーはマスク表示）。出力には `HasApiKey` と `Preset`（`LOCO_LLM__PRESET`）が含まれ、Web API `/api/llm/config` と一致します。

  Example output / 出力例:
  ```text
  LLM Configuration (effective):
    Provider     : ollama
    Model        : llama3.1
    ApiEndpoint  : http://localhost:11434/api/generate
    MaxTokens    : 1024
    Temperature  : 0.2
    HttpTimeoutMs: 45000
    ApiKey       : ************abcd
    HasApiKey    : true
    Preset       : OLLAMA
  ```

  JSON output / JSON出力:
  ```sh
  loco llm config --json
  ```
  Sample JSON / JSON出力例:
  ```json
  {
    "provider": "ollama",
    "model": "llama3.1",
    "apiEndpoint": "http://localhost:11434/api/generate",
    "maxTokens": 1024,
    "temperature": 0.2,
    "httpTimeoutMs": 45000,
    "apiKey": "redacted",
    "hasApiKey": true,
    "preset": "OLLAMA"
  }
  ```

 ## License

 Loco is licensed under the MIT License. See [LICENSE](../LICENSE) for details.
