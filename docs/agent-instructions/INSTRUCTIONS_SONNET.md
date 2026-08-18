# 作業指示書: Sonnet 向け(明確スコープ・機械的タスク)

対象モデル: Claude Sonnet クラス(スコープが明確で、確立済みパターンを反復適用するタスク)。
前提環境: npm が使えれば良い。**NuGet 到達不能なサンドボックスでも S-1/S-2/S-4 は完全実行可能**
(フロントエンドは nuget.org 非依存)。S-3 のみ注意事項あり。

## 共通コンテキスト(全タスク必読)

- ブランチ: `claude/product-strengths-weaknesses-k4tkgk`。作業ディレクトリ: `src/Loco.VisualEditor`(S-1/S-2/S-4)。
- **品質ゲート(毎コミット前に必須、全て緑であること)**:
  ```bash
  npx tsc --noEmit && npx vitest run && npm run build && npm run lint
  ```
- **mutation-verification 手順(新規テストに適用)**: 対象ファイルをバックアップ → テストが守るべきバグを一時的に再導入 →
  該当テストが失敗することを確認 → バックアップから復元(`git diff` で無変更確認) → 全緑を再確認。コミット本文に結果を記録。
- **既知の罠**:
  - `vi.useFakeTimers()` 下で `@testing-library/react` の `waitFor` は永久にハングする(実 setTimeout でポーリングするため)。
    代わりに `act(async () => { await Promise.resolve(); })` で microtask を flush し、インターバル駆動は `vi.advanceTimersByTimeAsync` を使う。
  - jsdom の `Blob` に `.text()` は無い → Blob コンストラクタを `vi.stubGlobal` で差し替えて内容をキャプチャ
    (`src/Loco.VisualEditor/src/utils/exportWorkflow.test.ts` 参照)。
  - jest-dom は未導入 → `toBeInTheDocument()` 等は使えない。`toBeTruthy()` / `container.childElementCount` / `queryBy* === null` で書く。
  - zustand ストアはモジュールグローバル → 各テストの `beforeEach` で `useWorkflowStore.setState({...})` により明示リセット。
    render 後のストア変更は `act(() => ...)` で包む。
- コミット規約: 1 論理単位 = 1 コミット、`test(editor): ...` / `chore(editor): ...`。本文に検証結果(テスト数、mutation 結果)を記録。
  push は `git push -u origin claude/product-strengths-weaknesses-k4tkgk`。
- **してはいけないこと**: 挙動変更を伴うリファクタ(S-2 で型を直す時も実行時挙動は不変に)/ 捏造メトリクスの復活 /
  `docs/PHASE_9`〜`14` の内容をテスト対象・実装対象として扱う / force-push。

---

## Task S-1: 残りユーティリティ/フックのテスト網羅

既にテスト済み(参考パターンとして読む価値あり): `retry`, `nodeEdgeLookup`, `detectChanges`, `deepClone`, `exportWorkflow`,
`useAutoSave`, `useExecutionPolling`, `useWorkflowListData`, `EdgeConditionPanel`, `PropertyPanel`, `ValidationPanel`, `TemplateGallery`。

**未テスト対象(優先度順)** — 各 1 ファイル = 1 コミット:

1. `src/Loco.VisualEditor/src/utils/structuralSharing.ts` — undo/redo 履歴スナップショットの核。参照同一性の保存(無変更なら同じ配列参照を返す)を必ず検証。
2. `src/Loco.VisualEditor/src/utils/formValidation.ts`
3. `src/Loco.VisualEditor/src/utils/environmentVariableValidation.ts`
4. `src/Loco.VisualEditor/src/utils/timeFormatting.ts` / `src/Loco.VisualEditor/src/utils/dataFormatting.ts` / `src/Loco.VisualEditor/src/utils/logFormatting.ts`(3 つで 1 コミット可)
5. `src/Loco.VisualEditor/src/utils/deferHistorySnapshot.ts`(タイマー系 — fake timers の罠に注意)
6. `src/Loco.VisualEditor/src/utils/autoLayout.ts`(dagre 依存 — 決定的な入出力のみ検証)
7. `src/Loco.VisualEditor/src/hooks/useListFiltering.ts` / `src/Loco.VisualEditor/src/hooks/useFormInput.ts` / `src/Loco.VisualEditor/src/hooks/useSecretVisibility.ts`(renderHook で)
8. `src/Loco.VisualEditor/src/utils/errorLogger.ts` / `src/Loco.VisualEditor/src/utils/logger.ts`

**手順(各ファイル共通)**: 対象を全文読み → 公開 API ごとに正常系 + 境界(空入力/null/重複)+ エラー系 →
mutation-verification 1 回以上 → 品質ゲート → コミット。既存テストの書き味(describe 構成、コメントで「何を守るテストか」明記)に合わせる。

## Task S-2: ESLint warning 102 → 0

`npm run lint` は 0 エラー・102 warning。`package.json` の `lint:strict`(`--max-warnings 0`)が通る状態にする。

- `@typescript-eslint/no-explicit-any` → 実際の型を書く(`unknown` + 絞り込みも可)。**実行時挙動は不変**。
- `no-unused-vars` → 削除(公開 API の一部なら `_` プレフィックスではなく本当に不要か確認)。
- `no-useless-escape` / `no-case-declarations` / `no-control-regex` / `no-constant-condition` → 個別に安全修正。
- 20〜30 warning ごとにコミットし、毎回品質ゲート(特に `npx vitest run`)を通す。
- 完了条件: `npm run lint:strict` が exit 0。完了後 `.eslintrc.cjs` の該当ルールを `warn`→`error` に昇格し、退行を恒久防止。

## Task S-3: `Practical/` デッドコード削除(.cs — 環境条件付き)

監査で全 33 クラスの参照を grep 済み。分類:
- **残す(wired)**: `SimpleLogger.cs`(SimpleLoggerFactory 経由で実使用)。
- **fully dead(参照ゼロ、20 ファイル)**: SimpleApiClient, SimpleConfig, SimpleConnectionPool, SimpleContainer, SimpleDatabase,
  SimpleEventBus, SimpleHealthCheck, SimpleHttpServer, SimpleJob, SimpleMonitoring, SimpleObjectPool, SimpleRateLimiter,
  SimpleStateMachine, SimpleStorage, SimpleTemplate, SimpleTest, SimpleValidation, SimpleWorkflow, UnifiedCache, ほか
  `src/Loco.Core/Practical/` 内で `grep -rn "クラス名" src/ tests/ --include=*.cs` がファイル自身以外ヒットしないもの。
- **dead island(相互参照のみ、12 ファイル)**: SimpleCircuitBreakerPattern, SimpleHttpClient, SimpleMetricsPattern,
  SimpleCachePattern, FastQueuePattern, SimpleRetryPattern, SimpleScheduler, SimpleMessageBroker, SimpleSerializer,
  SimpleBackgroundTaskRunner, SimpleNotification, SimpleEmail。

**手順**: 削除前に必ず自分で `grep -rn "<クラス名>" src/ tests/ --include=*.cs` を再実行し「ファイル自身と Practical 内以外ヒットなし」を
確認してから `git rm`。fully dead → dead island の順で 2 コミット。README / `src/Loco.Core/Practical/（インデックスは削除済み）` 等の参照文書も同期。

**環境条件**: NuGet 到達可能なら削除後に `dotnet build` で確認(必須)。到達不能サンドボックスなら**削除のみ・新規 .cs コード追加禁止**とし、
コミット本文に VERIFICATION CAVEAT(ビルド未確認、削除対象の非参照は grep で確認済み)を明記。
※注意: `SlackConnector.cs` 等に `using Loco.Core.Practical;` が残っている(SimpleLogger 用)。namespace ごと消さないこと。

## Task S-4: ドキュメント数値の実態同期

コミット時点の実数に更新: README の「N passing tests」(`npx vitest run` の実数)、コネクタ数(`ls src/Loco.Core/Integrations/Connectors/*.cs | wc -l`)、
CLI コマンド一覧(`src/Loco.Cli/Program.cs` の switch と help を突合)。`docs/PRODUCT_ASSESSMENT.md` の数値も同様。
数えた根拠コマンドをコミット本文に記録。
