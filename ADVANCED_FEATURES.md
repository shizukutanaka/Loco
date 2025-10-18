# Loco Advanced Features - Second Round

このドキュメントは、最初の改善ラウンド後に実装された追加の高度な機能をまとめています。

---

## 🎯 新規実装機能

### 1. ワークフローインクルード/インポート

**目的**: 共通のステップシーケンスを複数のワークフローで再利用可能にする

**設定例**:
```json
{
  "id": "main-deployment",
  "name": "Main Deployment",
  "includes": [
    {
      "id": "pre-deploy-backup",
      "path": "workflows/includes/backup-steps.json",
      "position": 1,
      "variables": {
        "source_path": "${var:source_path}",
        "backup_path": "${var:backup_path}"
      }
    },
    {
      "id": "post-deploy-health",
      "path": "workflows/includes/health-check.json",
      "position": -1,
      "continueOnError": false
    }
  ],
  "steps": [
    ...
  ]
}
```

**プロパティ**:
- `path`: インクルードするワークフローファイルのパス（相対または絶対）
- `id`: このインクルードを参照するためのオプションID
- `position`: ステップを挿入する位置（0=先頭、-1=末尾、デフォルト=-1）
- `variables`: インクルードされたワークフローに渡す変数
- `continueOnError`: インクルードの読み込み失敗時に続行するか（デフォルト=false）

**利点**:
- ✅ コードの重複を削減
- ✅ 標準化された処理手順を共有
- ✅ メンテナンスが容易
- ✅ モジュール化されたワークフロー設計

**使用例**:
```bash
loco workflow workflows/templates/deployment-with-includes.json
```

---

### 2. クリーンアップ/ロールバックハンドラー

**目的**: ステップ失敗時または完了時に自動的にクリーンアップ処理を実行

**設定例**:
```json
{
  "id": "deploy-files",
  "name": "Deploy application files",
  "type": "log",
  "message": "Deploying files...",
  "onFailure": {
    "type": "log",
    "message": "✗ Deployment failed - restoring from backup...",
    "continueAfterCleanup": false
  },
  "onComplete": {
    "type": "log",
    "message": "Deployment step completed",
    "timeoutSeconds": 30
  }
}
```

**ハンドラータイプ**:
- `onFailure`: ステップが失敗したときに実行
- `onComplete`: ステップが成功・失敗に関わらず完了後に実行

**CleanupHandlerプロパティ**:
- `type`: クリーンアップアクションのタイプ（log, process, delete-file等）
- `message`: ログメッセージ（typeがlogの場合）
- `command`: 実行するコマンド（typeがprocessの場合）
- `filePath`: 削除するファイルパス（typeがdelete-fileの場合）
- `continueAfterCleanup`: クリーンアップ後にワークフローを続行するか（デフォルト=false）
- `timeoutSeconds`: クリーンアップアクションのタイムアウト

**利点**:
- ✅ 自動ロールバック
- ✅ リソースの自動クリーンアップ
- ✅ エラー処理の簡素化
- ✅ より堅牢なワークフロー

**Ansibleとの比較**:
- Ansibleの`block/rescue/always`構文よりもシンプル
- ステップごとに細かく制御可能

---

### 3. インタラクティブ確認プロンプト

**目的**: 重要なステップ実行前にユーザーに確認を求める

**設定例**:
```json
{
  "id": "confirm-deployment",
  "name": "Confirm deployment to production",
  "type": "log",
  "message": "Ready to deploy",
  "prompt": {
    "message": "Deploy to production? This will affect live systems.",
    "defaultYes": false,
    "timeoutSeconds": 30,
    "onDecline": "stop"
  }
}
```

**プロパティ**:
- `message`: ユーザーに表示するメッセージ
- `defaultYes`: Enterキーのみを押した場合のデフォルト選択（true=Yes, false=No）
- `timeoutSeconds`: 自動的にデフォルト値を選択するまでのタイムアウト（0=タイムアウトなし）
- `onDecline`: ユーザーがNoと答えた場合の動作
  - `"skip"`: このステップをスキップ
  - `"stop"`: ワークフローを停止
  - `"continue"`: 続行（確認のみ）

**表示例**:
```
  ⚠ Deploy to production? This will affect live systems. [y/N] (timeout in 30s)
```

**利点**:
- ✅ 重要な操作の誤実行を防止
- ✅ 人間による承認フロー
- ✅ タイムアウト機能で無人実行も可能
- ✅ CI/CDでの条件付き承認に対応

**GitHub Actionsとの比較**:
- GitHub Actionsの`environment.protection_rules`より柔軟
- ローカル実行でも使用可能

---

### 4. プログレスインジケーター

**目的**: 長時間実行されるステップの進捗を視覚的に表示

**実装クラス**:

#### **ProgressIndicator**
スピナーアニメーション付きの進捗表示

```csharp
using var progress = new ProgressIndicator("Deploying application");
progress.Start();

// 長時間実行される処理
await DoLongRunningTask();

progress.Stop(success: true);
```

**表示例**:
```
  ⠹ Deploying application [12.3s]
```

#### **ProgressBar**
パーセンテージベースのプログレスバー

```csharp
var progressBar = new ProgressBar(100, "Processing files");

for (int i = 0; i < 100; i++)
{
    // 処理
    progressBar.Update(i + 1);
}

progressBar.Complete();
```

**表示例**:
```
  Processing files: [████████████████████░░░░░░░░░░░░░░░░░░░░] 50% (50/100)
```

**利点**:
- ✅ ユーザーに進捗状況を可視化
- ✅ プロセスが停止していないことを確認
- ✅ 予想実行時間の把握
- ✅ プロフェッショナルなUX

---

### 5. ワークフロー統計とレポート

**目的**: 実行統計の詳細なトラッキングとレポート生成

**クラス**: `WorkflowExecutionStats`, `WorkflowStatsFormatter`

**機能**:
- 総ステップ数、完了数、失敗数、スキップ数の追跡
- リトライ回数の集計
- ステップごとの実行時間記録
- 詳細レポート生成

**レポート例**:
```
╔════════════════════════════════════════════════════════════════════╗
║              WORKFLOW EXECUTION STATISTICS                         ║
╠════════════════════════════════════════════════════════════════════╣
║  Workflow: Production Deployment                                  ║
║  ID: production-deployment                                        ║
╠════════════════════════════════════════════════════════════════════╣
║  Start:    2025-10-18 22:41:14                                   ║
║  End:      2025-10-18 22:41:17                                   ║
║  Duration: 2.7s                                                    ║
╠════════════════════════════════════════════════════════════════════╣
║  Status:   SUCCESS ✓                                              ║
╠════════════════════════════════════════════════════════════════════╣
║  Total Steps:     13                                               ║
║  Completed:       13                                               ║
║  Failed:          0                                                ║
║  Skipped:         0                                                ║
║  Total Retries:   2                                                ║
╚════════════════════════════════════════════════════════════════════╝

Step Details:

  ✓ [OK] Deploy application - 0.5s
  ✓ [OK] Verify deployment - 1.2s
  ⊘ [SKIP] Rollback - 0.0s
```

**利点**:
- ✅ パフォーマンス分析
- ✅ ボトルネック特定
- ✅ 監査ログ
- ✅ トラブルシューティング支援

---

### 6. ワークフローテンプレート生成

**目的**: 一般的なシナリオ用のワークフローを素早く作成

**クラス**: `WorkflowTemplateGenerator`

**テンプレートタイプ**:

1. **基本テンプレート** (`GenerateBasicTemplate`)
   - シンプルな3ステップワークフロー
   - 学習・実験用

2. **デプロイメントテンプレート** (`GenerateDeploymentTemplate`)
   - 環境プリセット付き
   - バックアップ、デプロイ、検証の完全なフロー
   - 本番環境対応

3. **ヘルスチェックテンプレート** (`GenerateHealthCheckTemplate`)
   - API、ディスク、メモリのチェック
   - モニタリング用

4. **バックアップテンプレート** (`GenerateBackupTemplate`)
   - ファイルバックアップと検証
   - タイムスタンプ付きバックアップディレクトリ

**使用例** (将来のCLI統合):
```bash
loco workflow new basic my-workflow
loco workflow new deployment production-deploy
loco workflow new health-check system-health
loco workflow new backup daily-backup
```

**利点**:
- ✅ 素早いプロトタイピング
- ✅ ベストプラクティスの組み込み
- ✅ 学習用サンプル
- ✅ 時間の節約

---

## 📁 新規作成ファイル

### コア機能
- `src/Loco.Core/Workflows/WorkflowInclude.cs` - インクルード機能
- `src/Loco.Core/Workflows/CleanupHandler.cs` - クリーンアップハンドラー
- `src/Loco.Core/Workflows/InteractivePrompt.cs` - インタラクティブプロンプト
- `src/Loco.Core/Workflows/ProgressIndicator.cs` - プログレスインジケーター
- `src/Loco.Core/Workflows/WorkflowStats.cs` - 統計機能
- `src/Loco.Core/Workflows/WorkflowTemplate.cs` - テンプレート生成

### ワークフローテンプレート
- `workflows/templates/interactive-deployment.json` - インタラクティブデプロイメント例
- `workflows/templates/deployment-with-includes.json` - インクルード使用例
- `workflows/includes/health-check.json` - 再利用可能ヘルスチェック
- `workflows/includes/backup-steps.json` - 再利用可能バックアップ手順

### ドキュメント
- `ADVANCED_FEATURES.md` - このドキュメント

---

## 🔧 実装の詳細

### Partial Classパターン

既存のクラスを拡張するために、partial classパターンを使用:

```csharp
// WorkflowLoader.cs
public partial class WorkflowDefinition { ... }
public partial class WorkflowStep { ... }

// WorkflowInclude.cs
public partial class WorkflowDefinition
{
    public List<WorkflowInclude>? Includes { get; set; }
}

// CleanupHandler.cs
public partial class WorkflowStep
{
    public CleanupHandler? OnFailure { get; set; }
    public CleanupHandler? OnComplete { get; set; }
}
```

**利点**:
- 既存コードを変更せずに機能追加
- 関心事の分離
- モジュール化された設計

---

## 🚀 使用シナリオ

### シナリオ1: 本番環境への慎重なデプロイ

```json
{
  "steps": [
    {
      "id": "confirm",
      "type": "log",
      "message": "Ready to deploy",
      "prompt": {
        "message": "Deploy to production?",
        "defaultYes": false,
        "onDecline": "stop"
      }
    },
    {
      "id": "backup",
      "type": "log",
      "message": "Creating backup...",
      "onFailure": {
        "type": "log",
        "message": "Backup failed - cannot proceed",
        "continueAfterCleanup": false
      }
    },
    {
      "id": "deploy",
      "type": "log",
      "message": "Deploying...",
      "onFailure": {
        "type": "log",
        "message": "Deployment failed - rolling back",
        "continueAfterCleanup": false
      }
    }
  ]
}
```

### シナリオ2: モジュール化された複雑なワークフロー

```json
{
  "includes": [
    { "path": "includes/pre-checks.json", "position": 0 },
    { "path": "includes/backup.json", "position": 1 },
    { "path": "includes/health-check.json", "position": -1 }
  ],
  "steps": [
    { "id": "deploy", "type": "log", "message": "Deploying..." }
  ]
}
```

---

## 📊 機能比較（第2ラウンド）

| 機能 | Loco | Jenkins | GitHub Actions | Ansible |
|------|------|---------|----------------|---------|
| ワークフローインクルード | ✅ ネイティブ | ⚠️ Jenkinsfileインポート | ⚠️ 複雑 | ✅ インクルード/ロール |
| クリーンアップハンドラー | ✅ ステップレベル | ⚠️ post stage | ⚠️ always block | ✅ block/rescue |
| インタラクティブプロンプト | ✅ タイムアウト付き | ⚠️ プラグイン | ❌ | ⚠️ pause module |
| プログレスインジケーター | ✅ ネイティブ | ✅ Web UI | ⚠️ Web UI | ❌ |
| 統計レポート | ✅ ターミナル | ✅ Web UI | ✅ Web UI | ⚠️ verbose |
| テンプレート生成 | ✅ ネイティブ | ⚠️ 手動 | ⚠️ テンプレートリポジトリ | ⚠️ ansible-galaxy |

---

## ✅ ビルド状態

- **ビルド**: ✅ 成功（0警告、0エラー）
- **クロスプラットフォーム**: ✅ Windows確認済み
- **テスト**: ✅ 全テスト合格

---

## 🎯 影響まとめ

**第1ラウンドの改善**:
- ワークフロー可視化
- 環境プリセット
- 高度な変数システム
- リトライとタイムアウト
- 条件実行

**第2ラウンドの追加機能**:
- ✅ ワークフローのモジュール化（インクルード）
- ✅ 自動クリーンアップ/ロールバック
- ✅ 人間による承認フロー（プロンプト）
- ✅ 進捗の可視化
- ✅ 詳細な統計レポート
- ✅ テンプレート生成

**結果**: Locoは単なるワークフロー実行ツールから、**エンタープライズレベルの自動化プラットフォーム**へと進化しました。

---

**生成日**: 2025-10-18
**バージョン**: 1.2.0
**状態**: 本番環境対応
