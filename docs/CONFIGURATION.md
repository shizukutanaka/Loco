# Configuration Guide / 設定ガイド

## Overview / 概要
- **EN**: Loco stores its runtime artifacts under the Windows roaming AppData root to keep data portable across devices.
- **JA**: Loco は Windows の Roaming AppData 配下に実行時アーティファクトを保存し、環境間の移行を容易にします。

## JSON Configuration / JSON 設定
- **EN**: Define configuration overrides in `loco.config.json` (or the file pointed to by `LOCO_CONFIG_PATH`). All properties are optional and use direct mapping.
- **JA**: `loco.config.json`（または `LOCO_CONFIG_PATH` で指定したファイル）で設定の上書きを定義できます。すべてのプロパティはオプションで、直接マッピングを使用します。

### Available Properties / 利用可能なプロパティ
- **EN**: All configuration properties are at the root level of the JSON file. Boolean values accept `true`/`false`, numeric values must be integers.
- **JA**: すべての設定プロパティは JSON ファイルのルートレベルにあります。真偽値は `true`/`false` を受け付け、数値は整数で指定します。

```json
{
  "maxConcurrentFlows": 8,
  "enableAutoBackup": false,
  "workingDirectory": "C:/Projects/Loco/Working",
  "cacheDirectory": "C:/Projects/Loco/Cache",
  "logDirectory": "C:/Projects/Loco/Logs",
  "logLevel": "Warning",
  "enableFileLogging": true,
  "enableConsoleLogging": false,
  "logRetentionDays": 14,
  "memoryLimitMB": 1024,
  "cacheSizeMB": 128,
  "enableMemoryOptimization": false,
  "defaultTimeoutSeconds": 45,
  "defaultRetryCount": 5,
  "rateLimitPerMinute": 250,
  "allowedPaths": ["./data", "C:/Projects/Loco"],
  "forbiddenPaths": ["C:/sensitive"],
  "maxFileSizeBytes": 1073741824,
  "enableAuditLogging": true,
  "enableInputValidation": true,
  "enableHealthChecks": false,
  "enableMetrics": true,
  "healthCheckIntervalSeconds": 120
}
```

## Default Values / 既定値
- **EN**: If no configuration file is found or properties are missing, these defaults are used:
- **JA**: 設定ファイルが見つからない場合やプロパティが欠落している場合、以下の既定値が使用されます：

| Property / プロパティ | Default Value / 既定値 |
|---------------------|----------------------|
| maxConcurrentFlows | 10 |
| enableAutoBackup | true |
| workingDirectory | Current process directory |
| cacheDirectory | `%APPDATA%/Loco/Cache` |
| logDirectory | `%APPDATA%/Loco/Logs` |
| logLevel | "Information" |
| enableFileLogging | true |
| enableConsoleLogging | true |
| logRetentionDays | 30 |
| memoryLimitMB | 512 |
| cacheSizeMB | 64 |
| enableMemoryOptimization | true |
| defaultTimeoutSeconds | 30 |
| defaultRetryCount | 3 |
| rateLimitPerMinute | 100 |
| allowedPaths | [] (empty array) |
| forbiddenPaths | [] (empty array) |
| maxFileSizeBytes | 1073741824 (1GB) |
| enableAuditLogging | true |
| enableInputValidation | true |
| enableHealthChecks | true |
| enableMetrics | true |
| healthCheckIntervalSeconds | 60 |

## Directory Resolution / ディレクトリ解決
- **Cache Directory**: `%APPDATA%/Loco/Cache`
  - **EN**: Created on startup. JSON overrides are normalized automatically.
  - **JA**: 起動時に作成されます。JSON 上書きは自動的に正規化されます。
- **Log Directory**: `%APPDATA%/Loco/Logs`
  - **EN**: Logging output directory. JSON overrides are normalized automatically.
  - **JA**: ログ出力先です。JSON 上書きは自動的に正規化されます。
- **Working Directory**: Defaults to the current process directory.
  - **EN**: If overridden, the value is normalized and used as-is.
  - **JA**: 上書きした場合、その値が正規化されてそのまま使用されます。

## Security Configuration / セキュリティ設定
- **AllowedPaths**: Array of paths that are permitted for file operations
  - **EN**: Additional paths beyond defaults. Normalized and deduplicated automatically.
  - **JA**: 既定値を超える追加のパス。自動的に正規化・重複排除されます。
- **ForbiddenPaths**: Array of paths that are blocked from file operations
  - **EN**: Paths that override defaults and are forcibly excluded from AllowedPaths.
  - **JA**: 既定で許可されるパスを上書きして強制的に除外するパス。
- **Path Safety Enforcement**
  - **EN**: `WorkingDirectory`, `CacheDirectory`, `LogDirectory`, `AllowedPaths`, and `ForbiddenPaths` entries are rejected if they target restricted or unsafe locations (for example: system folders, traversal attempts). Violations raise configuration validation failures during startup.
  - **JA**: `WorkingDirectory`、`CacheDirectory`、`LogDirectory`、`AllowedPaths`、`ForbiddenPaths` に制限された場所や危険な場所が指定された場合、起動時の構成検証で拒否されます（システムフォルダーやディレクトリトラバーサルなど）。
- **Verification Command**
  - **EN**: Run `loco config verify` (use `--json` for structured output) to execute the same validation pipeline used by `HealthCheckService`. Non-zero exit codes indicate warnings or failures that must be addressed before deployment.
  - **JA**: `loco config verify`（構造化出力が必要な場合は `--json` を付与）を実行すると、`HealthCheckService` と同じ検証パイプラインが動作します。警告や失敗がある場合は非ゼロ終了コードとなり、本番展開前に対処する必要があります。

## Environment Override / 環境変数による上書き
- `LOCO_CONFIG_PATH`
  - **EN**: Points to an optional JSON configuration file. Relative paths are resolved relative to the current working directory.
  - **JA**: 任意の JSON 設定ファイルを指す環境変数です。相対パスはカレントディレクトリを基準に解決されます。

### Usage Examples / 使用例
- **PowerShell**
  - **EN**: `Set-Item Env:LOCO_CONFIG_PATH "C:/Projects/Loco/custom.config.json"`
  - **JA**: `Set-Item Env:LOCO_CONFIG_PATH "C:/Projects/Loco/custom.config.json"`
- **Command Prompt**
  - **EN**: `set LOCO_CONFIG_PATH=C:\\Projects\\Loco\\custom.config.json`
  - **JA**: `set LOCO_CONFIG_PATH=C:\\Projects\\Loco\\custom.config.json`

## Error Handling / エラーハンドリング
- **EN**: If the configuration file is missing or contains invalid JSON, Loco will use default values and continue operation. No complex error tracking or reload mechanisms are implemented.
- **JA**: 設定ファイルが存在しない場合や無効な JSON が含まれる場合、Loco は既定値を使用して動作を継続します。複雑なエラートラッキングや再読み込みメカニズムは実装されていません。

## Validation / 検証
- **EN**: Run `dotnet test` after altering configuration files to ensure the new settings are accepted.
- **JA**: 設定ファイルを変更した際は `dotnet test` を実行し、新しい設定が受け入れられることを確認してください。
