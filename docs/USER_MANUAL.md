## Rule Management / ルール管理

### Listing Rules / ルールの一覧表示
```bash
dotnet run --project src/Loco.Cli rule list
dotnet run --project src/Loco.Cli rule list --json
dotnet run --project src/Loco.Cli rule list --rules-path C:\Data\rules.json
```

### Enabling or Disabling Rules / ルールの有効化・無効化
```bash
dotnet run --project src/Loco.Cli rule disable <ruleId>
dotnet run --project src/Loco.Cli rule enable <ruleId>
```

### Deleting Rules / ルールの削除
```bash
dotnet run --project src/Loco.Cli rule delete <ruleId>
```

### Rule Storage / ルールストレージ
- Default path: `%APPDATA%/Loco/rules.json`
- Override with environment variable `LOCO_RulesFilePath` or CLI option `--rules-path`

## UI Design Reference / UI設計参照
- See `docs/STYLE_GUIDE.md` for design tokens, layout guidance, and accessibility checklist.
# Loco User Manual

This manual provides comprehensive guidance for using the Loco automation platform.

## Introduction

Loco is a lightweight automation platform designed for Windows environments. It provides both command-line interface (CLI) and web-based interfaces for creating and managing automation workflows.

## Installation

### Prerequisites
- Windows 10/11 or Windows Server 2019+
- .NET 8 Runtime (automatically installed if needed)

### Quick Install
1. Download the latest release from your organization's approved software distribution channel
2. Extract to your desired directory (e.g., `C:\Program Files\Loco`)
3. Run `Loco.Cli.exe` or use `dotnet run --project src/Loco.Cli`

## Getting Started

### Starting the Engine
```bash
# Start the automation engine
dotnet run --project src/Loco.Cli start

# Or from the extracted release
Loco.Cli.exe start
```
### Basic Commands
```bash
# Check system health
dotnet run --project src/Loco.Cli health

# Machine-readable health summary
dotnet run --project src/Loco.Cli health --json

# Machine-readable health summary with custom rule store
dotnet run --project src/Loco.Cli health --json --rules-path C:\Data\rules.json

# Show system information
dotnet run --project src/Loco.Cli info

# Show system information for alternate rule store
dotnet run --project src/Loco.Cli info --rules-path C:\Data\rules.json
```
# View version
dotnet run --project src/Loco.Cli version
```

## Command Line Interface

### Core Commands

#### Engine Management
- `start`: Start the automation engine
- `test`: Run system tests
- `health`: Check system health status (`--json`, `--rules-path` supported)
- `info`: Display system and engine information (`--rules-path` supported)

##### Info Command Diagnostics
- `info` reports the loaded configuration file path (or `(default)` when using built-in defaults).
- `info` indicates whether `PathResolutionWarnings` are present and, when warnings exist, prints each warning plus a summary line for quick triage.

#### Automation Rules
- `preset system`: Create system monitoring preset (`--rules-path` supported)
- `preset daily`: Create daily maintenance preset (`--rules-path` supported)
- `preset cleanup`: Create cleanup preset (`--rules-path` supported)
- `preset list`: List available presets

#### File Operations
- `files search "*.txt"`: Search for text files
- `files search "*.log" logs/`: Search in specific directory
- `files stats`: Show directory statistics
- `files stats Documents/`: Show statistics for specific directory

#### Log Management
- `logs view`: View recent log entries
- `logs view 100`: View last 100 log entries
- `logs stats`: Show log statistics
- `logs search "ERROR"`: Search for error messages

## Configuration

### Configuration File
`LocoConfig` exposes the loaded configuration path through the `SourceConfigPath` property for diagnostics. Paths such as `workingDirectory`, `cacheDirectory`, `logDirectory`, `allowedPaths`, and `forbiddenPaths` may be specified as relative paths; they are resolved against the location of the JSON configuration file. The CLI `info` command surfaces this path along with any warnings.

{{ ... }}
`LocoConfig` は `SourceConfigPath` プロパティを通じて読み込まれた構成ファイルの場所を公開します。`workingDirectory` や `cacheDirectory` などのパス設定は相対指定が可能で、設定ファイルのディレクトリを基準に解決されます。

If a path entry is invalid or duplicated, the configuration loader skips it and records the detail in `PathResolutionWarnings`. The boolean `HasPathResolutionWarnings` is true when warnings are present. Review these warnings via CLI diagnostics or logs to adjust your configuration.

無効または重複したパスは読み込み時にスキップされ、その内容が `PathResolutionWarnings` に記録されます。警告があるときは `HasPathResolutionWarnings` が true になります。CLI の診断機能やログから警告を確認し、必要に応じて設定を修正してください。

Create a `config/loco.config.json` file:

```json
{
  "maxConcurrentFlows": 10,
  "enableAutoBackup": true,
  "workingDirectory": "C:/Projects/Loco/Working",
  "logDirectory": "C:/Projects/Loco/Logs",
  "enableFileLogging": true,
  "enableConsoleLogging": true,
  "logRetentionDays": 30,
  "memoryLimitMB": 512,
  "enableMemoryOptimization": true
}
```

Example using relative directories:

相対ディレクトリを利用する例:

```json
{
  "workingDirectory": "../workspace",
  "logDirectory": "logs",
  "allowedPaths": ["data", "../shared/config.json"]
}
```

When the configuration file resides in `C:/Loco/config/loco.config.json`, the example above resolves to:

上記設定が `C:/Loco/config/loco.config.json` にある場合、解決結果は以下の通りです:

- `workingDirectory`: `C:/Loco/workspace`
- `logDirectory`: `C:/Loco/config/logs`
- `allowedPaths`: `C:/Loco/config/data`, `C:/Loco/shared/config.json`

### Environment Variables
- `LOCO_CONFIG_PATH`: Path to custom configuration file
- `LOCO_LOG_LEVEL`: Set logging level (Debug, Information, Warning, Error)
- `LOCO_RulesFilePath`: Override rule storage location (default `%APPDATA%/Loco/rules.json`)

### Configuration Diagnostics / 構成診断

```bash
# Show the active configuration and any warnings
dotnet run --project src/Loco.Cli config show
dotnet run --project src/Loco.Cli config show --json

# Validate configuration using the same checks as HealthCheckService
dotnet run --project src/Loco.Cli config verify
dotnet run --project src/Loco.Cli config verify --json
```

- `config show --json` returns a machine-readable snapshot including `AllowedPaths`, `ForbiddenPaths`, and accumulated warnings (exit code 1 when warnings exist).
- `config verify` executes the full validation pipeline; exit code 0 indicates success, 1 indicates warnings, and 2 signals validation failures that require immediate remediation. When warnings or errors occur, CLI output includes **Recommended actions** (e.g., run `config show --json`, review `docs/CONFIGURATION.md`, take a configuration backup) to guide operators through triage.
- Interactive `config show` now groups details by category: **Core Limits** (rate limits, circuit breaker thresholds), **Logging**, **Security Controls** (audit logging, input validation, metrics, etc.), and path/domain allowlists. Warnings are summarized with counts and detailed entries to aid rapid triage.
- **Bilingual Output**: All labels are displayed in both English and Japanese (e.g., "Config Path / 構成パス"), making the output accessible to international users.

## Automation Workflows

### Creating Presets
Loco includes several pre-configured automation presets:

#### System Monitoring
```bash
dotnet run --project src/Loco.Cli preset system --rules-path C:\Data\rules.json
```
This creates a rule that monitors:
- Memory usage (512MB threshold)
- Disk space (5GB threshold)
- System information

#### Daily Maintenance
```bash
dotnet run --project src/Loco.Cli preset daily --rules-path C:\Data\rules.json
```
This creates a rule that performs:
- Clean temporary files older than 7 days
- Clean log files older than 30 days
- List current directory files

#### Cleanup
```bash
dotnet run --project src/Loco.Cli preset cleanup --rules-path C:\Data\rules.json
```
This creates a rule that cleans temporary files older than 1 day.

### Custom Workflows
Workflows can be created by combining different actions:

- **Log Actions**: Log messages and information
- **File Actions**: File operations and management
- **Monitor Actions**: System monitoring and alerts
- **Process Actions**: Execute system commands
- **Backup Actions**: File and directory backup
- **Cleanup Actions**: Clean temporary files and logs

## Monitoring and Logging

### Viewing Logs
```bash
# View recent logs
dotnet run --project src/Loco.Cli logs view

# View specific number of lines
dotnet run --project src/Loco.Cli logs view 200

# Search logs
dotnet run --project src/Loco.Cli logs search "ERROR"

# Show log statistics
dotnet run --project src/Loco.Cli logs stats
```

### Log Files
Log files are stored in `%APPDATA%/Loco/Logs` by default and include:
- Engine execution logs
- System monitoring logs
- Error and warning logs
- Audit logs

## System Monitoring

### Health Checks
```bash
# Check overall health
dotnet run --project src/Loco.Cli health

# JSON report for automation pipelines
dotnet run --project src/Loco.Cli health --json

# Detailed health information
dotnet run --project src/Loco.Cli info
```

`health --json` は CLI の診断結果を JSON 形式で出力します。スケジューラや監視ツールからパースしやすく、`OverallStatus`・各チェックの `status`・推奨事項を含みます。英語環境では同じコマンドで利用でき、フィールド名は camelCase で統一されています。

Example / 例:

```json
{
  "health": {
    "timestamp": "2025-10-08T09:05:12.345Z",
    "overallStatus": "Healthy",
    "checks": [
      {
        "name": "Memory",
        "status": "Healthy",
        "message": "Memory usage normal (245.3 MB)",
        "details": {
          "ProcessMemory": "245.3 MB",
          "GCMemory": "188.9 MB"
        },
        "recommendations": []
      }
    ]
  },
  "engine": {
    "healthy": true,
    "flowCount": 0,
    "ruleCount": 0,
    "totalExecutions": 0,
    "successRate": 0.0
  }
}
```

### Performance Monitoring
```bash
# Quick system stats
dotnet run --project src/Loco.Cli quick stats

# Monitor specific metrics
dotnet run --project src/Loco.Cli monitor memory 512
dotnet run --project src/Loco.Cli monitor disk C: 10
```

## Webhook Listener

`WebhookTriggerSystem` は HTTP ベースの自動化を外部システムから安全に呼び出すためのリスナーです。既定のベース URL は `http://localhost:8080/` ですが、運用環境では必ず HTTPS を有効化してください (`RequireHttps = true`)。

- **同時実行制御**: `MaxConcurrentRequests` によって待機キューを制御し、上限に達すると 503 (Server busy) を返します。
- **ペイロード制限**: `MaxRequestBodyBytes` を超える要求は 413 (Payload too large) で拒否します。
- **パス検証**: `/alerts/high-priority` など英数字・`/`・`-`・`_`・`.` のみ受け付けます。
- **タイムアウト**: `RequestTimeout` を超えた処理は 504 (Handler timeout) にフォールバックします。
- **相関 ID**: すべての応答ヘッダーとロギングに `X-Request-Id` が付与され、トレースが容易です。
- **署名検証 (任意)**: `RequireSignature = true` と `SharedSecret` を設定すると、`X-Signature` ヘッダー (Base64 エンコードされた HMAC-SHA256) を検証します。
- **送信元 IP 制御**: `RemoteIpValidator` デリゲートで許可 IP をホワイトリスト化できます。
- **ホスト検証**: ワイルドカードを許可しない場合 (`AllowWildcardPrefixes = false`)、受信したリクエストのホスト名とポートが指定したベース URL と一致しないと 421 (Misdirected request) を返します。
- **エラー情報制御**: `IncludeExceptionDetails` を `true` にすると例外メッセージを応答本文へ含めます (開発環境専用)。
- **パス整合性検査**: `RejectEncodedPathSeparators` を `true` にすると `%2F` や `%5C` などの二重エンコード経路を 400 で拒否し、`RejectPathTraversalSegments` を有効にすると `../` 等のトラバーサルセグメントを拒否します。
- **メトリクス収集**: `MetricsSink` に `IWebhookMetricsSink` 実装を渡すと、受信/完了/失敗/レート制限/再試行イベントを外部メトリクス基盤へ転送できます (例: Prometheus, OpenTelemetry)。未設定時は `WebhookMetricsSink.Null` が使用されるため、既存アプリケーションに影響を与えません。
- **エンドポイント統計**: `WebhookEndpoint` には `RequestCount`、`ErrorCount`、`RetryCount`、`SuccessfulResponseCount`、`AverageResponseTime`、`LastLatencyMs`、`LastStatusCode`、`LastResponseCompletedAt` が格納されます。成功応答時のみ `SuccessfulResponseCount` と平均レイテンシーが更新されるため、失敗リクエストの影響を受けない実測値を確認できます。
- **統計取得/リセット API**: `WebhookTriggerSystem.GetEndpointStatistics()` で現在の統計スナップショットを取得し、`ResetEndpointStatistics(path, version?)` や `ResetAllEndpointStatistics()` で個別または全エンドポイントの統計をクリアできます。運用時はダッシュボード更新やローテーション時に活用してください。
- **バージョン付きルーティング**: `EnableVersionedRouting` を `true` にすると、`/hooks/v1/alert` のようにバージョンセグメントを持つパスを登録できます。`VersionedRoutePrefix` で共通プレフィックス、`VersionedRoutePattern` で許容フォーマット (例: `v[0-9]+`)、`EnableVersionFallback` で最新バージョンへのフォールバック可否、`VersionedRouteLatestAlias` でエイリアス名 (例: `latest`) を指定します。
- **冪等性保護**: `EnableIdempotencyProtection` を `true` にし、`IdempotencyHeaderName` または `IdempotencyKeyResolver` を設定するとリクエスト毎に一意キーを検証します。`IdempotencyTtl` で応答キャッシュ寿命、`IdempotencyCacheMaxEntries` で格納上限、`IdempotencyRejectOnMissingKey` でキー欠如時の 409 応答を制御できます。
- **圧縮ボディ対応**: `EnableRequestDecompression` を `true` にし、`AllowedContentEncodings` を設定すると `Content-Encoding` が `gzip`・`deflate`・`br` などの圧縮リクエストを自動展開します。`MaxContentEncodingLayers` で入れ子の圧縮段数上限を指定し、未許可または過剰な圧縮は 415 で拒否されます。
- **Accept ヘッダーの厳格化**: `EnforceAcceptHeader` を `true` にすると、`AllowedAcceptMediaTypes` に列挙した MIME のみを受理し、その他は 406 (Not Acceptable) を返します。未指定時は `application/json` 系を暗黙受理し、適合性を維持します。
- **メトリクス拡張**: `IWebhookMetricsSink.RecordRequestRejected` が呼び出され、Accept ヘッダー拒否や圧縮ポリシー違反などの拒否理由を収集できます。外部監視で拒否傾向を可視化し、自動アラートやレポートに連携してください。
- **認証設定**: `AuthenticationSchemes` で `HttpListener` の認証方式 (例: Basic、Negotiate) を指定でき、匿名モード以外を選択した上で `AuthenticationChallenges` に複数の `WWW-Authenticate` チャレンジを定義して応答 401 のヘッダーを制御できます。
- **ヘッダー抑制**: `SuppressServerHeader` を `true` にすると `Server` ヘッダーを除去し、`SuppressWwwAuthenticateOn401` を `true` にすると 401 応答でも `WWW-Authenticate` を付与しません。
- **セキュリティヘッダー上書き**: `SecurityHeaderOverrides` にキーと値を設定すると、既定のセキュリティヘッダーを追加・上書きできます (例: `Strict-Transport-Security` の秒数変更)。
- **リトライガイダンス**: `BusyRetryAfterSeconds` を設定すると 503/504/429 に `Retry-After` を自動付与します。
- **レスポンスヘッダー共通化**: `DefaultResponseHeaders` で `Cache-Control` などの共通ヘッダーを追加できます。
- **セキュリティヘッダー自動付与**: レスポンスには `X-Content-Type-Options: nosniff`、`X-Frame-Options: DENY`、`Cache-Control: no-store`、`Pragma: no-cache` が自動付与され、クリックジャッキングや MIME 誤検知を防ぎます。
- **追加ヘッダー保護**: さらに `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'`、`Referrer-Policy: no-referrer`、`X-Permitted-Cross-Domain-Policies: none`、`Cross-Origin-Resource-Policy: same-origin`、`Cross-Origin-Opener-Policy: same-origin`、`Cross-Origin-Embedder-Policy: require-corp`、`Permissions-Policy: geolocation=(), microphone=(), camera=(), payment=()`、`X-DNS-Prefetch-Control: off` を自動付与し、HTTPS ベースでは `Strict-Transport-Security` も送出します。
- **許可メソッド案内**: `OPTIONS` リクエストでサポートメソッドを返し、`Allow` ヘッダーを自動付与します。
- **ヘッダー/クエリ制限**: `MaxHeaderCount` と `MaxQueryParameterCount` を超える要求は拒否されます。
- **許可 MIME タイプ**: `AllowUnknownContentTypes` を `false` に設定し、`AllowedContentTypes` を指定すると MIME タイプをホワイトリスト化できます。
- **CORS 制御**: `EnableCors` を `true` にし、`CorsAllowedOrigins`・`CorsAllowedHeaders`・`CorsExposeHeaders`・`CorsAllowCredentials` を設定するとオリジン/ヘッダーごとの許可を細かく制御できます。プリフライト要求には `Access-Control-Allow-*` が付与されます。
- **レート制限 (クライアント/API キー/組織)**: `EnablePerClientRateLimit` で IP ベースの制限、`EnableApiKeyRateLimit` で API キー単位、`EnableOrganizationRateLimit` で組織 ID 単位の制限を有効化できます。各スコープに対し `*RateLimitRequests`・`*RateLimitWindow`・`*RateLimitMaxEntries` を設定し、ヘッダー (`ApiKeyHeaderName`, `OrganizationHeaderName`) もしくは独自リゾルバー (`ApiKeyResolver`, `OrganizationResolver`) でキーを決定します。超過時は 429 と `Retry-After` が返却され、応答 JSON に `scope` と `subject` が含まれます。
- **ハンドラー再試行**: `HandlerRetryMaxAttempts`・`HandlerRetryInitialDelay`・`HandlerRetryBackoffFactor`・`HandlerRetryMaxDelay`・`HandlerRetryOnException`・`HandlerRetryStatusCodes` でバックオフポリシーを構成できます。`HandlerRetryOverrideHeaderName` (既定 `X-Webhook-Retry`) を指定するとリクエストヘッダー経由で `maxAttempts`, `initialDelayMs`, `maxDelayMs`, `backoff`, `retryOnException`, `codes` を上書きでき、解析に失敗した場合はサーバーログへ警告が出力されます。

設定例:

```csharp
var webhookOptions = new WebhookTriggerOptions
{
    RequireHttps = true,
    RequireSignature = true,
    SharedSecret = Environment.GetEnvironmentVariable("LOCO_WEBHOOK_SECRET"),
    RemoteIpValidator = ip => ip == "10.0.0.5",
    IncludeExceptionDetails = false,
    BusyRetryAfterSeconds = 5,
    DefaultResponseHeaders = new Dictionary<string, string>
    {
        ["Cache-Control"] = "no-store"
    },
    AllowUnknownContentTypes = false,
    AllowedContentTypes = new[] { "application/json" },
    EnableCors = true,
    CorsAllowedOrigins = new[] { "https://example.com" },
    CorsAllowedHeaders = new[] { "content-type", "x-custom-header" },
    CorsExposeHeaders = new[] { "X-Request-Id" },
    CorsAllowCredentials = true,
    EnablePerClientRateLimit = true,
    ClientRateLimitRequests = 30,
    ClientRateLimitWindow = TimeSpan.FromSeconds(60),
    EnableApiKeyRateLimit = true,
    ApiKeyRateLimitRequests = 100,
    ApiKeyRateLimitWindow = TimeSpan.FromSeconds(60),
    ApiKeyHeaderName = "X-Api-Key",
    EnableOrganizationRateLimit = true,
    OrganizationRateLimitRequests = 500,
    OrganizationRateLimitWindow = TimeSpan.FromMinutes(5),
    OrganizationHeaderName = "X-Organization-Id",
    HandlerRetryMaxAttempts = 5,
    HandlerRetryInitialDelay = TimeSpan.FromMilliseconds(200),
    HandlerRetryBackoffFactor = 2.5,
    HandlerRetryMaxDelay = TimeSpan.FromSeconds(10),
    HandlerRetryOnException = true,
    HandlerRetryStatusCodes = new[] { 408, 429, 500, 502, 503, 504 },
    MetricsSink = new PrometheusWebhookMetricsSink(),
    EnableIdempotencyProtection = true,
    IdempotencyHeaderName = "Idempotency-Key",
    IdempotencyTtl = TimeSpan.FromMinutes(15),
    IdempotencyCacheMaxEntries = 20000,
    IdempotencyRejectOnMissingKey = true,
    EnableRequestDecompression = true,
    AllowedContentEncodings = new[] { "gzip", "deflate", "br" },
    MaxContentEncodingLayers = 3,
    EnforceAcceptHeader = true,
    AllowedAcceptMediaTypes = new[] { "application/json", "application/*+json" },
    EnableVersionedRouting = true,
    VersionedRoutePrefix = "/hooks",
    VersionedRoutePattern = "v[0-9]+",
    EnableVersionFallback = true,
    VersionedRouteLatestAlias = "latest"
};
var webhookSystem = new WebhookTriggerSystem("https://localhost:8443/", logger, webhookOptions);
```

In English:

The `WebhookTriggerSystem` listens for inbound automation requests. Enable HTTPS (`RequireHttps = true`) for production deployments. Configure `MaxConcurrentRequests`, `MaxRequestBodyBytes`, and `RequestTimeout` to control resource usage. Every response carries an `X-Request-Id` header for diagnostics, and the system automatically adds hardened security headers (`X-Content-Type-Options`, `X-Frame-Options`, `Cache-Control`, `Pragma`, `Content-Security-Policy`, `Referrer-Policy`, `X-Permitted-Cross-Domain-Policies`, and `Strict-Transport-Security` when HTTPS) to mitigate clickjacking, caching leaks, and cross-domain abuse. Optional signature validation (`RequireSignature` + `SharedSecret`) rejects tampered payloads that lack a valid Base64 HMAC-SHA256 signature in `X-Signature`. Use `RemoteIpValidator` to whitelist source addresses, and keep `IncludeExceptionDetails` disabled outside development. Handlers receive both `WebhookRequest.Body` and `WebhookRequest.BodyBytes` plus the original `BodyEncoding`, so binary payloads can be processed without re-encoding, and `WebhookRequest.RequestCancellationToken` allows long-running handlers to respect server-side cancellation. Turn on CORS (`EnableCors`) to allow specific origins/headers, enforce `AllowWildcardPrefixes = false` unless intentional star-listening is required—in which case incoming connections are no longer host-validated—configure `AuthenticationSchemes`/`AuthenticationChallenges` and header suppression toggles for your environment, and enable per-client rate limiting (`EnablePerClientRateLimit`) to throttle high-volume IPs with automatic 429 responses plus an accurate `Retry-After` header that conveys how long the client should wait before retrying.

Enable versioned routing (`EnableVersionedRouting`) when you need side-by-side API revisions. Set `VersionedRoutePrefix` (default `/hooks`) and `VersionedRoutePattern` (default `v[0-9]+`) so requests like `/hooks/v2/alert` are dispatched to the appropriate handler. If `EnableVersionFallback` is `true`, requests lacking an explicit version (or using the alias defined by `VersionedRouteLatestAlias`, default `latest`) automatically fall back to the most recent registered version, simplifying gradual rollouts.

- **Endpoint metrics**: Each `WebhookEndpoint` tracks `RequestCount`, `ErrorCount`, `RetryCount`, `SuccessfulResponseCount`, `AverageResponseTime`, `LastLatencyMs`, `LastStatusCode`, and `LastResponseCompletedAt`. Only successful responses increment `SuccessfulResponseCount` and contribute to `AverageResponseTime`, so failed requests do not skew latency measurements. Use these fields in dashboards or health checks to observe handler performance.
- **Statistics snapshot/reset API**: Call `WebhookTriggerSystem.GetEndpointStatistics()` to fetch an immutable snapshot, `TryGetEndpointStatistics(path, out statistics, version?)` for a single endpoint (requiring explicit version when multiple variants exist), `ResetEndpointStatistics(path, version?)` to clear specific routes, or `ResetAllEndpointStatistics()` for a global reset. Use `GetAggregateStatistics()` to retrieve overall totals (requests, successes, errors, retries, average latency) for dashboards or health checks.

Configure handler retries with `HandlerRetryMaxAttempts`, `HandlerRetryInitialDelay`, `HandlerRetryBackoffFactor`, `HandlerRetryMaxDelay`, `HandlerRetryOnException`, and optional `HandlerRetryStatusCodes`. The default behavior retries 5xx responses up to three times with exponential backoff. Clients can override retry behavior per request by sending the header defined in `HandlerRetryOverrideHeaderName` (defaults to `X-Webhook-Retry`) using key/value pairs such as `maxAttempts=5;initialDelayMs=500;maxDelayMs=5000;backoff=2;retryOnException=false;codes=429,503`.

`GET` を許可すると `HEAD` も自動的に許可され、`OPTIONS` 応答や `Allow` ヘッダーに含まれます。CORS 設定では `CorsAllowedOrigins` に HTTP/HTTPS のオリジンのみ指定でき、パス・クエリ・フラグメントは無視される点に注意してください。/ When `GET` is listed in `AllowedMethods`, the system implicitly permits `HEAD` and advertises it in both `OPTIONS` responses and the `Allow` header. CORS origin rules accept only HTTP or HTTPS authorities without paths, queries, or fragments; invalid entries are rejected during configuration.

For observability, set `MetricsSink` to a custom `IWebhookMetricsSink` that exports counters, histograms, or spans to your monitoring stack. Include rate-limit hits, handler retries, and success/failure totals in dashboards to detect anomalies quickly and trigger automated remediation.

Enable idempotency protection (`EnableIdempotencyProtection`) to prevent duplicate processing of retried requests. Provide keys via `IdempotencyHeaderName` or a custom `IdempotencyKeyResolver`, adjust cache retention with `IdempotencyTtl` and `IdempotencyCacheMaxEntries`, and decide whether missing keys should be rejected (`IdempotencyRejectOnMissingKey`).

Enable request decompression (`EnableRequestDecompression`) to accept compressed payloads. Configure the whitelist via `AllowedContentEncodings` (e.g., `gzip`, `deflate`, `br`), limit nesting with `MaxContentEncodingLayers`, and note that unsupported or excessive encodings return HTTP 415.

Enforce client expectations by enabling `EnforceAcceptHeader`. Provide MIME types via `AllowedAcceptMediaTypes` (for example `application/json`, vendor-specific `application/*+json`); clients with incompatible `Accept` headers receive HTTP 406 for immediate feedback.

## Troubleshooting

### Common Issues

#### Engine Won't Start
1. Check if .NET 8 is installed
2. Verify configuration file syntax
3. Check log files for error messages
4. Ensure sufficient disk space

#### Rules Not Executing
1. Verify trigger conditions
2. Check action configurations
3. Review log files for errors
4. Test individual components

#### Memory Issues
1. Reduce `maxConcurrentFlows` setting
2. Enable memory optimization
3. Increase system memory limit
4. Monitor memory usage regularly

#### Log Files Missing
1. Check log directory permissions
2. Verify `enableFileLogging` setting
3. Ensure disk space is available
4. Check configuration file

### Getting Help
```bash
# Show all available commands
dotnet run --project src/Loco.Cli help

# Show command-specific help
dotnet run --project src/Loco.Cli files
dotnet run --project src/Loco.Cli logs
```

## Advanced Usage

### Custom Configuration
Advanced users can create custom configurations:

```json
{
  "defaultTimeoutSeconds": 60,
  "defaultRetryCount": 5,
  "rateLimitPerMinute": 100,
  "allowedPaths": ["C:/Projects", "C:/Data"],
  "forbiddenPaths": ["C:/Windows/System32"],
  "enableAuditLogging": true,
  "enableHealthChecks": true,
  "healthCheckIntervalSeconds": 30
}
```

### Performance Tuning
Optimize performance by adjusting these settings:

- `maxConcurrentFlows`: Maximum simultaneous workflows
- `memoryLimitMB`: Memory usage limit
- `cacheSizeMB`: Cache size allocation
- `rateLimitPerMinute`: API rate limiting
- `enableMemoryOptimization`: Enable memory optimization

## Security Considerations

### Access Control
- Use `allowedPaths` and `forbiddenPaths` to restrict file access
- Enable audit logging for security monitoring
- Regularly review log files for suspicious activity

### Best Practices
- Use strong, unique API keys
- Limit file system access to necessary directories
- Enable input validation
- Keep log retention reasonable to manage disk space
- Regularly update and patch the system

## Support and Resources

### Getting Help
- Check the documentation in the `docs/` directory
- Review log files for error details
- Test individual components to isolate issues
- Use the `info` command for system diagnostics

### Community
- Report bugs and issues on the project repository
- Suggest features and improvements
- Share workflows and automation examples

This user manual covers the essential features and usage patterns of the Loco automation platform. For advanced usage and development, refer to the Developer Guide.
