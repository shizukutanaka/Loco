# Loco User Manual

Welcome to Loco! This guide will help you get started with installing, configuring, and using Loco to automate your tasks.

## Table of Contents

1. [Introduction](#introduction)
2. [Installation](#installation)
   - [Windows](#windows)
   - [macOS](#macos)
   - [Linux](#linux)
3. [Quick Start](#quick-start)
   - [Creating Your First Automation Rule](#creating-your-first-automation-rule)
   - [Using Natural Language](#using-natural-language)
4. [Core Concepts](#core-concepts)
   - [Rules](#rules)
   - [Triggers](#triggers)
   - [Conditions](#conditions)
   - [Actions](#actions)
5. [Command-Line Interface (CLI)](#command-line-interface-cli)
   - [Common Commands](#common-commands)
6. [Troubleshooting](#troubleshooting)

## Introduction

Loco is a powerful and flexible automation tool that allows you to create custom workflows to handle repetitive tasks, integrate different applications, and manage your digital environment efficiently. From simple file operations to complex, multi-step processes, Loco provides the tools you need to build robust automations.

## Installation

### Windows

You can install Loco on Windows using one of the following methods:

**Using Winget:**
```sh
winget install ShizukuTakahashi.Loco
```

**Using Chocolatey:**
```sh
choco install loco
```

**Manual Installation:**
1. Download the latest release from the [GitHub Releases](https://github.com/shizukutanaka/Loco/releases) page.
2. Unzip the downloaded file.
3. Run `loco.exe` from your terminal.

### macOS & Linux

For macOS and Linux, you can use the `install.sh` script for a convenient installation:

```sh
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/shizukutanaka/Loco/main/install.sh)"
```

## Quick Start

### Creating Your First Automation Rule

Let's create a simple rule that shows a notification every day at 9 AM.

1.  Create a JSON file named `morning-reminder.json` with the following content:

    ```json
    {
      "id": "morning-reminder",
      "name": "Morning Reminder",
      "enabled": true,
      "trigger": {
        "type": "time.schedule",
        "config": {
          "hour": 9,
          "minute": 0
        }
      },
      "actions": [
        {
          "type": "notification.show",
          "config": {
            "title": "Good Morning!",
            "message": "Time to plan your day."
          }
        }
      ]
    }
    ```

2.  Start the service and load the rule file:

    ```sh
    loco start --rules morning-reminder.json [--plugins-path <plugins_directory>]
    ```

    - English: The service loads the rule JSON and keeps running until you stop it.
    - 日本語: サービスはルールJSONを読み込み、停止するまで常駐します。

Note on persistence: rules can also be loaded from a persistent JSON store configured via `MVP_RULE_STORE_PATH` (default: `$(AppContext.BaseDirectory)/data/rules.json`).

### Using Natural Language

You can also generate a rule JSON from natural language:

```sh
loco convert --text "every day at 5pm, run a backup of my documents folder"
```

This produces a JSON file (e.g., `nl_rule_YYYYMMDD_HHMMSS.json`) in the current directory.

## Core Concepts

Loco's automation is built around a few key concepts:

-   **Rules**: The basic unit of automation, combining triggers, conditions, and actions.
-   **Triggers**: Events that start a rule's execution (e.g., a specific time, a file change).
-   **Conditions**: Checks that must be true for the actions to run (e.g., the file size is greater than 1MB).
-   **Actions**: The tasks that are performed when a rule is triggered and its conditions are met (e.g., copy a file, send an email).

For a detailed list of available triggers and actions, please refer to the [API Documentation](./API.md).

## Command-Line Interface (CLI)

Loco is primarily controlled via its command-line interface.

### Global Options / グローバルオプション

- `--plugins-path, -p`
  - EN: Directory containing plugin assemblies.
  - JA: プラグインDLLが格納されたディレクトリ。
- `--lang, -l`
  - EN: UI language code (e.g., `ja`, `en`).
  - JA: UI言語コード（例: `ja`, `en`）。
- `--rules, -r`
  - EN: Path to a rule file or directory loaded at service start (used by `start`).
  - JA: サービス起動時に読み込むルールファイルまたはディレクトリ（`start`で使用）。

Defaults:
- Plugins directory default (when not provided for general operations): `%APPDATA%/Loco/Plugins`.
- Rule store path env var: `MVP_RULE_STORE_PATH` (default `$(AppContext.BaseDirectory)/data/rules.json`).
Environment override / 環境変数による上書き:
- EN: If `--plugins-path` is omitted, `LOCO_PLUGINS_PATH` (when set) is used.
- JA: `--plugins-path` 未指定時は、環境変数 `LOCO_PLUGINS_PATH` が設定されていれば使用されます。
- Precedence / 優先順位: explicit `--plugins-path` > `LOCO_PLUGINS_PATH` > default `%APPDATA%/Loco/Plugins`.

### Commands / コマンド一覧

- `start`
  - EN: Start the automation service, load plugins and rules. Supports `--plugins-path`, `--rules`.
  - JA: 自動化サービスを開始し、プラグインとルールを読み込みます。`--plugins-path`, `--rules`対応。
  - Logging / ログ: When `--plugins-path` is omitted, the CLI logs the effective path and its source: `explicit`, `env:LOCO_PLUGINS_PATH`, or `default`. / `--plugins-path` 未指定時、CLI は有効なパスとその由来（`explicit`, `env:LOCO_PLUGINS_PATH`, `default`）をログに出力します。

- `build`
  - EN: Launch interactive Flow Builder to compose a flow and save as JSON.
  - JA: 対話式フロービルダーを起動してフローを作成し、JSONで保存。

- `quick <args...>`
  - EN: Quickly compose a flow via short arguments (e.g., `loco quick timer 7:00 notify "Good morning"`).
  - JA: 短い引数で素早くフロー作成（例: `loco quick timer 7:00 notify "おはよう"`）。

- `execute --file <flow.json>`
  - EN: Execute a flow definition from file.
  - JA: フロー定義ファイルを実行。

- `convert --text <natural language>`
  - EN: Convert natural language to automation rule JSON and save to file.
  - JA: 自然言語を自動化ルールJSONへ変換し、ファイル保存。

- `validate --file <flow.json>`
  - EN: Validate a flow definition file.
  - JA: フロー定義ファイルを検証。

- `list`
  - EN: List available flow JSON files in the default directory.
  - JA: 既定ディレクトリのフローファイル一覧表示。

- `components`
  - EN: Show available components (triggers, conditions, actions) from the Flow Composer.
  - JA: フロービルダーのコンポーネント一覧を表示。

- `template list`
  - EN: List available templates.
  - JA: テンプレート一覧。

- `template apply --name <templateName>`
  - EN: Apply a template by name.
  - JA: テンプレート名で適用。

- `test-plugin [--rule-path <path>]`
  - EN: Load plugins from `--plugins-path` and run a test rule JSON (default `examples/rules/plugin-test-rule.json`).
  - JA: `--plugins-path`のプラグインを読み込み、テスト用ルールJSONを実行（既定: `examples/rules/plugin-test-rule.json`）。
  - Behavior / 挙動: If `--plugins-path` is omitted, the command uses the effective directory resolved by precedence (env override supported) and proceeds.
  - Logging / ログ: The command logs the effective plugins directory and its source: `explicit`, `env:LOCO_PLUGINS_PATH`, or `default`. / 有効なプラグインディレクトリとその由来（`explicit`, `env:LOCO_PLUGINS_PATH`, `default`）をログ出力します。

- `plugins-path`
  - EN: Print the effective plugins directory and ensure it exists.
  - JA: 有効なプラグインディレクトリを表示し、存在しない場合は作成します。
  - Options / オプション:
    - `--verbose, -v`
      - EN: Also print the source of the path: `explicit`, `env:LOCO_PLUGINS_PATH`, or `default`.
      - JA: パスの由来（`explicit`, `env:LOCO_PLUGINS_PATH`, `default`）も表示します。
  - Note / 注記: By default, output is the path only (no source) for script compatibility. / 既定ではスクリプト互換性のためパスのみを出力します（由来は表示しません）。
  - Example / 例:
    ```sh
    loco plugins-path
    ```
    ```sh
    loco plugins-path --verbose
    # or / または
    loco plugins-path -v
    ```

  Automated verification / 自動検証:
  - EN: To automatically verify default, env override, and explicit path precedence, publish the CLI and run the script:
    ```powershell
    dotnet publish .\src\Loco.Cli\Loco.Cli.csproj -c Release -o .\output
    .\tools\verify-plugins-path.ps1 -VerboseMode
    ```
  - JA: 既定・環境変数・明示指定の優先順位を自動検証するには、CLI を発行後に次を実行します:
    ```powershell
    dotnet publish .\src\Loco.Cli\Loco.Cli.csproj -c Release -o .\output
    .\tools\verify-plugins-path.ps1 -VerboseMode
    ```

  CI note / CI注記:
  - EN: On Windows runners, GitHub Actions runs this verification automatically to prevent regressions.
  - JA: Windows ランナーでは、GitHub Actions がこの検証を自動実行し、リグレッションを防止します。

- `llm config`
  - EN: Display the effective LLM configuration with sensitive fields (API key) redacted.
  - JA: 有効な LLM 設定を表示します（APIキーはマスクされます）。
  - Example / 例:
    ```sh
    loco llm config
    ```
  - EN: Output includes `HasApiKey` and `Preset` (from `LOCO_LLM__PRESET`) and aligns with Web API `/api/llm/config`.
  - JA: 出力には `HasApiKey` と `Preset`（`LOCO_LLM__PRESET`）が含まれ、Web API `/api/llm/config` と整合します。
  - JSON output / JSON出力:
    ```sh
    loco llm config --json
    ```
  - Sample JSON / JSON出力例:
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

- `version`
  - EN: Show CLI version and feature summary.
  - JA: CLIのバージョン情報を表示。

For full help, run `loco --help`.

### Plugin Testing / プラグインテスト

Run plugin integration test with a sample rule:

```sh
loco --plugins-path "C:\\Path\\To\\Plugins" test-plugin --rule-path examples/rules/plugin-test-rule.json
```

- EN: Loads plugins, adds the test rule via AutomationService, and triggers it. Check the plugin’s output/logs.
- JA: プラグインを読み込み、AutomationService経由でテストルールを追加・実行します。プラグインの出力やログを確認してください。

## Configuration (Environment & Paths) / 設定

- __MVP_RULE_STORE_PATH__
  - EN: File-based rule store path. Default: `$(AppContext.BaseDirectory)/data/rules.json`.
  - JA: ルール保存用のファイルパス。既定: `$(AppContext.BaseDirectory)/data/rules.json`。

- __LLM environment variables__ / __LLM 環境変数__
  - Provider-agnostic / プロバイダー共通（入れ子は二重アンダースコア）:
    - `LOCO_LLM__PROVIDER` (e.g., `ollama|openai|anthropic|gemini`)
    - `LOCO_LLM__MODEL`
    - `LOCO_LLM__APIKEY`
    - `LOCO_LLM__APIENDPOINT`
    - `LOCO_LLM__TEMPERATURE`
    - `LOCO_LLM__MAXTOKENS`
    - `LOCO_LLM__HTTPTIMEOUTMS` (default 30000, clamped 1000–600000) / HTTPタイムアウトのミリ秒（既定 30000、範囲 1000–600000 にクランプ）
    - `LOCO_LLM__PRESET` (optional: `OPENAI|OLLAMA|OPENROUTER`) primes defaults without overriding explicit values / 既定値を補完（明示設定は上書きしません）
  - Provider fallbacks / プロバイダー別フォールバック:
    - `OPENAI_API_KEY`, `OPENAI_BASE_URL`
    - `ANTHROPIC_API_KEY`, `ANTHROPIC_BASE_URL`
    - `GEMINI_API_KEY` または `GOOGLE_API_KEY`, `GEMINI_BASE_URL`
    - `OLLAMA_BASE_URL`

Examples / 例:

```powershell
# Windows PowerShell
# Optional preset (primes defaults without overriding) / 任意のプリセット（不足分を補完）
$env:LOCO_LLM__PRESET = "OLLAMA"  # or OPENAI, OPENROUTER
$env:LOCO_LLM__PROVIDER = "ollama"
$env:OLLAMA_BASE_URL = "http://localhost:11434"
$env:LOCO_LLM__MODEL = "llama3:8b"
$env:LOCO_LLM__HTTPTIMEOUTMS = "45000"  # 45 seconds
```

```bash
# macOS/Linux (bash)
# Optional preset (primes defaults without overriding)
export LOCO_LLM__PRESET=OPENAI  # or OLLAMA, OPENROUTER
export LOCO_LLM__PROVIDER=openai
export OPENAI_API_KEY=sk-...
export LOCO_LLM__MODEL=gpt-4o-mini
export LOCO_LLM__HTTPTIMEOUTMS=45000  # 45 seconds
```

__.env loading__ / __.env ロード__

- EN: Loco hosts load a `.env` file at startup via `DotEnvLoader.Load()`. The loader searches from `AppContext.BaseDirectory` upward and does not override already-set environment variables.
- JA: ホストは起動時に `.env` を読み込みます（`DotEnvLoader.Load()`）。`AppContext.BaseDirectory` から上位へ検索し、既存の環境変数は上書きしません。
- EN: No variable interpolation is performed; values are treated literally.
- JA: 変数展開は行われません。値はリテラルとして扱われます。

Recommendation / 推奨:
- EN: Prefer `LOCO_LLM__*` variables. Provider-specific variables like `OPENAI_API_KEY` are not read by Loco core.
- JA: `LOCO_LLM__*` 変数を使用してください。`OPENAI_API_KEY` 等のプロバイダー個別変数は Loco コアでは参照しません。

Security notes / セキュリティ注意:
- EN: Do not commit API keys. Prefer OS-level environment variables or secret stores.
- JA: APIキーをリポジトリに含めないでください。OSの環境変数やシークレットストアを利用してください。

Plugins path environment variable / プラグインパス環境変数:
- __LOCO_PLUGINS_PATH__
  - EN: Override plugins directory when `--plugins-path` is omitted.
  - JA: `--plugins-path` を省略した際のプラグインディレクトリを上書きします。
  - Examples / 例:
    ```powershell
    # Windows PowerShell
    $env:LOCO_PLUGINS_PATH = "C:\\MyPlugins"
    loco plugins-path
    ```
    ```bash
    # macOS/Linux (bash)
    export LOCO_PLUGINS_PATH=/opt/loco/plugins
    loco plugins-path
    ```

## Troubleshooting

If you encounter any issues, the first place to check is the log files located in the `logs` directory within your Loco installation folder. If you need further assistance, please [open an issue](https://github.com/shizukutanaka/Loco/issues) on our GitHub repository.

### Plugins Path issues / プラグインパスの問題

- English
  - Check the effective plugins directory and its source:
    ```powershell
    loco plugins-path -v
    ```
    Output shows the path and whether it came from `explicit`, `env:LOCO_PLUGINS_PATH`, or `default` (default is `%APPDATA%/Loco/Plugins` on Windows).
  - Override via environment variable (used when `--plugins-path` is omitted):
    ```powershell
    # Windows PowerShell
    $env:LOCO_PLUGINS_PATH = "$env:USERPROFILE\AppData\Roaming\Loco\Plugins"
    loco plugins-path -v
    ```
    ```bash
    # macOS/Linux (bash)
    export LOCO_PLUGINS_PATH="$HOME/.config/Loco/Plugins"
    loco plugins-path -v
    ```
  - Verify behavior automatically (optional):
    ```powershell
    dotnet publish .\src\Loco.Cli\Loco.Cli.csproj -c Release -o .\output
    .\tools\verify-plugins-path.ps1 -VerboseMode
    ```

- 日本語
  - 有効なプラグインディレクトリとその由来を確認:
    ```powershell
    loco plugins-path -v
    ```
    出力にはパスと由来（`explicit`, `env:LOCO_PLUGINS_PATH`, `default`）が表示されます（既定は Windows で `%APPDATA%/Loco/Plugins`）。
  - 環境変数で上書き（`--plugins-path` 未指定時に使用）:
    ```powershell
    # Windows PowerShell
    $env:LOCO_PLUGINS_PATH = "$env:USERPROFILE\AppData\Roaming\Loco\Plugins"
    loco plugins-path -v
    ```
    ```bash
    # macOS/Linux (bash)
    export LOCO_PLUGINS_PATH="$HOME/.config/Loco/Plugins"
    loco plugins-path -v
    ```
  - 自動検証（任意）:
    ```powershell
    dotnet publish .\src\Loco.Cli\Loco.Cli.csproj -c Release -o .\output
    .\tools\verify-plugins-path.ps1 -VerboseMode
    ```
