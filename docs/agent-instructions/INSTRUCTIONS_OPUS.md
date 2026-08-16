# 作業指示書: Opus 向け(高複雑度・要判断タスク)

対象モデル: Claude Opus クラス(アーキテクチャ判断・広範囲デバッグを伴うタスク)。
前提環境: **NuGet(api.nuget.org)に到達できる開発環境**。O-1 以外のタスクも O-1 完了(= ビルドが通る状態)を前提とする。

> **実行順序**: `O-1` → **`O-6`(資格情報)** → **`O-7`(トリガ)** → `O-2` → `O-3` → `O-4` → `O-5`。
> O-6/O-7 は [FIRST_PRINCIPLES_ANALYSIS.md](../FIRST_PRINCIPLES_ANALYSIS.md) で
> 「製品が成立するための必須要件」と判定されたため、後発だが O-2 以降より優先する。

## 共通コンテキスト(全タスク必読)

- ブランチ: `claude/product-strengths-weaknesses-k4tkgk`(旧 WPF プロジェクトの退避先端は `ad9c23d` — 触らない)。
- **エンジンの条件分岐意味論**(`src/Loco.Core/Workflows/VisualWorkflowEngine.cs` の `ShouldFollowConnection`):
  `null` / `"default"` / `"success"` → 前段成功時のみ追従、`"error"` → 前段失敗時のみ追従、**それ以外の非空文字列 → 常時追従**。
  フロント(`EdgeConditionPanel`)はこの意味論に合わせ済み。変更する場合は両側同時に。
- **API 契約**: フロント `src/Loco.VisualEditor/src/api/types.ts` の封筒
  `{success:true,data} | {success:false,error:{code,message,details}}`、camelCase、`page`/`pageSize`。サーバがフロントに合わせる方針。
- **Central Package Management 有効**: 各 csproj の `<PackageReference>` に `Version=` を書かない。
  バージョンはリポジトリルート `Directory.Packages.props` の `<PackageVersion>` のみ。違反すると NU1008。
- コミット規約: 論理単位ごと、`fix(scope): ...` / `feat(scope): ...` / `test(scope): ...`。push は `git push -u origin <branch>`。
- **してはいけないこと**: `docs/PHASE_9`〜`PHASE_14` を実装済み仕様として扱う / README に未計測の性能メトリクスを書く /
  force-push(初回退避以降は禁止)/ 旧 `SimpleLightEngine` を正史エンジンとして拡張する(正史は `VisualWorkflowEngine`)。

---

## Task O-1(最優先): 初のコンパイル + 全テスト実行

**背景**: バックエンドの 2026-07 セッション変更(`8f52278` 以降の全 .cs)は NuGet 遮断サンドボックスで書かれ、
**一度もコンパイルされていない**。各コミット本文の VERIFICATION CAVEAT がその印。

**手順**:
1. `dotnet restore Loco.sln` → `dotnet build Loco.sln`。
2. コンパイルエラーを**最小差分**で修正。意図はコミットメッセージに全て記録されているので、エラー箇所のコミットを
   `git log -p -- <file>` で読み、意図を保存したまま直す。
3. `dotnet test tests/Loco.Core.Tests` → `dotnet test tests/Loco.Api.Tests`。
   - `Loco.Core.Tests` は `<Compile Remove>` 絞り込みで約 50 テストを復活させてある(`169c338`)。Windows 専用 27 件・Sync 系は除外継続で良い。
   - テスト失敗時: テストと現行実装の意味論が食い違う場合、どちらが正か `git log` の経緯で判断(テスト側が古ければテストを直す)。
4. 特に注意して見るべき箇所(未検証度が高い順):
   - `src/Loco.Api/Program.cs`(`AddOptions<JwtBearerOptions>().Configure<JwtSigningKeyProvider>` パターン、rate limiter、CORS)
   - `src/Loco.Api/Controllers/WorkflowsController.cs`(dry-run 分岐 `722d242`、ExecutionRegistry 連携)
   - `src/Loco.Core/Workflows/VisualWorkflowEngine.cs` のキャンセル追加(`[JsonIgnore]` CancellationToken、OperationCanceledException catch 2 箇所)
   - `src/Loco.Core/Storage/JsonFileWorkflowStore.cs` / `Workflows/WorkflowMapper.cs` / `Workflows/StoredWorkflow.cs`
   - コネクタ修正: Slack/Calendly/Zendesk/HubSpot/Twilio/Stripe/Email(`b10c0e2`, `3a55911`)— boxed `JsonElement` の
     プロパティパターン(`is JsonElement { ValueKind: ... }`)は C# 12 前提
   - `src/Loco.Cli/Program.cs` のコマンド配線(`87092e6`)— 各コマンドを実際に 1 回ずつ起動して smoke test
5. **パラメータ受け渡し経路の E2E 確認**(必須): エディタ→実行までの引数は
   `PropertyPanel(config.parameters.X)` → `StoredWorkflow.Data.Config` →
   `WorkflowMapper`(**`parameters` オブジェクトを展開**) → `WorkflowNode.Parameters` →
   `ActionParameters` → `connector.GetString("X")` と流れる。
   ここが 1 箇所でもずれると**全コネクタの全引数が null になる**(実際に起きていた欠陥。
   `cdf9426` 以降で mapper 側を修正済み、`WorkflowMapperTests` に回帰テストあり)。
   http ノードを 1 つ作って `url` が実際にコネクタまで届くことを実機で確認すること。
6. 完了条件: `dotnet build` 0 エラー、全テスト緑、`loco help` に列挙された全コマンドが起動する。
7. 修正コミットの本文に「どのキャベアト付きコミットのエラーをいくつ直したか」を記録(以後キャベアトは不要になる)。

## Task O-6 ★ O-1 の次に最優先: 資格情報の 3 層実装

**背景**: [FIRST_PRINCIPLES_ANALYSIS.md](../FIRST_PRINCIPLES_ANALYSIS.md) §2.1。
`WorkflowConnectorBridge.ConfigureConnector()` の**呼び出し元がゼロ**、かつエディタのノード型に
**資格情報フィールドが存在しない**。結果として 28 コネクタは `InitializeAsync` されず
`_httpClient` が null のまま実行され、**必ず失敗する**。製品最大の資産が到達不能。

```bash
grep -rn "ConfigureConnector" --include=*.cs src/ | grep -v "WorkflowConnectorBridge.cs"   # → ゼロ
grep -n  "credential\|Credential" src/Loco.VisualEditor/src/types/workflow.ts               # → ゼロ
```

**方針** — n8n の実証済みモデルに倣う(資格情報はワークフローと別エンティティ、
ノードは**秘密値を埋め込まず ID で参照**、実行時に注入):

1. **保存層**: `src/Loco.Core/Security/SecretsManager.cs`(**実装済み** — AES-256 + PBKDF2 +
   原子的書込)を土台に `ConnectorCredentialStore` を作る。1 資格情報 = `{id, connectorId, name,
   secrets: Dictionary<string,string>}`。値は SecretsManager 経由で暗号化。
2. **モデル層**: `WorkflowNode.data` に `credentialId?: string` を追加(フロント `types/workflow.ts`
   と `WorkflowMapper` の両方)。**秘密値そのものはワークフロー JSON に絶対に入れない**。
3. **注入層**: `WorkflowsController` の execute 経路で、ワークフローが参照する `credentialId` を解決し
   `bridge.ConfigureConnector(connectorId, config)` を呼んでから実行する。
   ※ `ConnectorStartupService` の doc コメント(現在 "registered WITHOUT credentials" と明記)も更新すること。
4. **API**: `ConnectionsController` — `GET /api/v1/connections`(**値は返さない**、メタデータのみ)、
   `POST`(作成)、`PUT /{id}`、`DELETE /{id}`、`POST /{id}/test`(`IConnector.TestConnectionAsync` を再利用)。
5. **UI**: 接続一覧・作成フォーム(`ConfigParameters` から動的生成 — 各コネクタが既に宣言済み)、
   PropertyPanel のノードに接続セレクタ。既存 `useSecretVisibility` フックでマスク表示。

**受け入れ条件**: Slack 接続を UI で登録 → ワークフローの Slack ノードでそれを選択 → 実行 →
実際に Slack へ送信される。`GET /connections` のレスポンスに秘密値が含まれないことをテストで保証。

## Task O-7 ★ 最優先の次: トリガ配線(「自動化」の成立条件)

**背景**: 同 §2.2。`CronScheduler`/`EventTrigger`/`FileWatcherTrigger`/`TriggerManager`/`WebhookReceiver`
は Core に**存在するが、Loco.Api・Loco.Cli からの参照がゼロ**。webhook 受信エンドポイントも無い。
→ 人間が実行ボタンを押さない限り何も起きない = 自動化ではない。

```bash
grep -rn "TriggerManager\|CronScheduler\|FileWatcherTrigger" --include=*.cs src/Loco.Api src/Loco.Cli  # → ゼロ
```

**方針**(小さく始める。durable execution の全面導入はしない):
1. **スケジュール**: `IHostedService`(`ConnectorStartupService` と同じ形)で常駐スケジューラを 1 つ起動。
   保存済みワークフローのうちトリガノードが cron を持つものを、**TZ 対応済みの既存 `CronScheduler`**
   (`e75e3dc` で TimeZoneInfo 対応済み)で評価し、期限が来たら通常の実行経路に流す。
2. **Webhook**: `POST /api/v1/webhooks/{workflowId}` を追加し、既存 `WebhookReceiver`
   (**HMAC 署名検証を実装済み**)を再利用。ボディを初期実行コンテキストとして渡す。
   認証は JWT ではなくワークフロー個別トークン + HMAC にすること(送信元は外部サービスのため)。
3. **重複実行の防止**: 再起動直後に過去分が一斉発火しないよう、最終発火時刻を永続化する
   (O-2 の実行履歴永続化と同じストアで良い)。
4. **UI**: トリガノードに cron 式入力と webhook URL 表示。

**受け入れ条件**: cron を設定したワークフローが**人手を介さず**発火し、`GET /executions` に履歴が残る。
webhook URL に curl で POST すると実行される。

> 参考: 2025 年以降は ad-hoc な cron/sleep ループではなく、実行状態を永続化した基盤に
> スケジュールを持たせるのが標準([Temporal Schedules](https://temporal.io/product))。
> 上記 3.(最終発火時刻の永続化)はその最小版にあたる。

## Task O-2: 実行履歴のファイル永続化

**背景**: `src/Loco.Api/Execution/ExecutionRegistry.cs` はインメモリ(完了 500 件上限・最古 evict)。API 再起動で全実行履歴が消える。

**方針**: 実証済みパターン `src/Loco.Core/Storage/JsonFileWorkflowStore.cs`
(SemaphoreSlim + キャッシュ + `.tmp` 書込→`File.Move(overwrite:true)`、ID 検証 `^[A-Za-z0-9_-]{1,128}$`)を踏襲した
`JsonFileExecutionStore` を `Loco.Core.Storage` に新設。**完了時のみ**書き込む(Running 中はメモリのまま)。

**受け入れ条件**: 実行完了 → API 再起動 → `GET /api/v1/executions/{id}` が 200 で完了時の内容を返す。
`ExecutionResponseFactory.ToFrontendStatus` のマッピング(Success→"completed" 等)を再利用。統合テストを `Loco.Api.Tests` に追加。

## Task O-3: custom condition 式の実評価

**背景**: `EdgeConditionPanel` は任意式(例 `output.status === 200`)を保存できるが、エンジンは「success/error/default 以外 = 常時追従」
としか解釈しない。UI の helpText にも「未実装」と明記してある。

**方針**:
1. 第一段階は限定文法で安全に: `output.<path> == <literal>` / `!=` / 数値比較程度。前段の実行結果
   (`NodeExecutionResult.Data`)への JSON パス参照 + 比較。外部式エンジン依存を増やす場合は
   `Directory.Packages.props` 追加を含め慎重に(サプライチェーンを評価)。
2. `ShouldFollowConnection` に「既知キーワード以外 → 式として評価、評価失敗は fail-safe に false + ExecutionLog へ警告」を追加。
   **注意**: これは既存の「それ以外 = 常時追従」からの意味論変更。`'always'` は常時追従キーワードとして明示的に残すこと
   (フロントは `'always'` を送る — `3d86379` 参照)。
3. フロント: `EdgeConditionPanel.tsx` の helpText から「未実装」文言を外し、対応文法を記載。
   `KNOWN_VALUES` はそのまま(`always` は dropdown、他文字列は custom 扱い)。
4. テスト: エンジン単体(真/偽/パス不在/型不一致/構文エラー)+ mapper 経由 round-trip + フロント側 helpText 更新のテスト調整。

## Task O-4: エディタ → API の E2E 疎通

**手順**: `dotnet run --project src/Loco.Api`(port 5000、`Properties/launchSettings.json` 設定済み)+
`cd src/Loco.VisualEditor && npm install && npm run dev`(vite が `/api` を 5000 へ proxy)。
ブラウザ(または Playwright)で: ワークフロー作成 → ノード 2 個配線 → 保存 → 実行 → ExecutionPanel で completed 確認 →
delay ノードでキャンセル確認。発見した契約不一致は「サーバをフロントに合わせる」方針で修正。

## Task O-5: CI パイプライン

`.github/workflows/ci.yml` 新設: (a) frontend ジョブ = `npm ci && npx tsc --noEmit && npx vitest run && npm run build && npm run lint`、
(b) backend ジョブ = `dotnet build && dotnet test`。**注意**: 過去セッションで GitHub トークンに workflows スコープが無く
`.github/workflows` を push できなかった事例あり。push が拒否された場合は YAML を `docs/ci/` に置きユーザーに手動配置を依頼する。
