# 作業指示書: Opus 向け(高複雑度・要判断タスク)

対象モデル: Claude Opus クラス(アーキテクチャ判断・広範囲デバッグを伴うタスク)。
前提環境: **NuGet(api.nuget.org)に到達できる開発環境**。O-1 以外のタスクも O-1 完了(= ビルドが通る状態)を前提とする。

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
5. 完了条件: `dotnet build` 0 エラー、全テスト緑、`loco help` に列挙された全コマンドが起動する。
6. 修正コミットの本文に「どのキャベアト付きコミットのエラーをいくつ直したか」を記録(以後キャベアトは不要になる)。

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
