# Loco 製品評価 — 長所・短所・改善案

作成: 2026-07 品質改善セッション(ブランチ `claude/product-strengths-weaknesses-k4tkgk`)の総括。
根拠: 6 角度のマルチエージェント監査(56 findings、確認済み項目は個別に独立再検証)+ 8 フェーズの修正実装。

> **読む前に**: 本書の「短所」は隠さず列挙しています。各項目の改善タスクは
> [`agent-instructions/INSTRUCTIONS_OPUS.md`](agent-instructions/INSTRUCTIONS_OPUS.md)(高複雑度・要コンパイル環境)と
> [`agent-instructions/INSTRUCTIONS_SONNET.md`](agent-instructions/INSTRUCTIONS_SONNET.md)(明確スコープ・機械的)に
> 実行可能な形で割り当て済みです。

---

## 長所(検証済みの資産)

1. **28 個の実装済みコネクタ** — Slack / GitHub / Stripe / Salesforce 等。実 API 呼び出し・HMAC 署名検証を持つ本物の実装。
   直近の監査で発見された実バグ 6 件(Slack DM の不可能キャスト、Calendly 自動検出、Zendesk macro のプレビュー止まり、
   HubSpot リスト追加の PUT/POST、Twilio bulk の From 欠落、Email bulk の変数消失)は修正済み(`3a55911`, `b10c0e2`)。
2. **正史エンジンの確立** — `VisualWorkflowEngine` + `WorkflowConnectorBridge` が API
   (`POST /api/v1/workflows/{id}/execute`)と CLI(`loco workflow run-visual`)の両方から到達可能。
   リトライ/バックオフ、エラー分岐ルーティング、キャンセル(CTS 連携)対応。
3. **フロントエンドの品質** — React 18 + TypeScript + Vite。`npx tsc --noEmit` クリーン、
   **256/256 テスト緑**、`npm run build` 成功、ESLint 0 エラー。undo/redo、エッジ条件ルーティング UI
   (マウス不要・キーボード到達可)、主要修正は mutation-verification(バグ再導入→テスト失敗確認→復元)済み。
4. **リーンで安全な API** — JWT + PBKDF2(設定ユーザー、ゼロユーザー時は 501 — 旧「何でも通る」認証を廃止)、
   フロント契約(`{success,data|error}` 封筒 / camelCase / page/pageSize)と一致、レート制限(429 + Retry-After)、
   liveness/readiness 分離、JSON ファイル永続化(tmp→move 原子的書込、ID 検証によるパストラバーサル防止)。
5. **TZ/DST 対応 cron**(`e75e3dc`)、**HttpClient リーク修正**(全 24 コネクタ、`65e1094`)、**CLI 全 13 コマンド配線**(`87092e6`)。
6. **誠実なドキュメント** — 捏造メトリクス・架空機能の主張を除去し、Known limitations を README に明記(`d42c820`, `ed138ab`)。

## 短所(優先度順)

> **2026-08 追記**: [FIRST_PRINCIPLES_ANALYSIS.md](FIRST_PRINCIPLES_ANALYSIS.md) により、
> 下表より**上位の欠落**が判明した(製品の成立条件そのもの):
> - **資格情報が完全欠落** — `ConfigureConnector()` の呼び出し元ゼロ、ノードモデルに資格情報フィールド無し
>   → **28 コネクタは実行時に必ず失敗する**。→ Opus **O-6**
> - **トリガ未配線** — Core にあるトリガ群が Api/Cli から一切参照されていない
>   → 手動実行器であり「自動化」ではない。→ Opus **O-7**
> - ~~`Loco.Cli` がコンパイル不能(`SecretsManager` 不在)~~ → **`6147783` で修正済み**

| # | 問題 | 深刻度 | 改善タスク |
|---|------|--------|-----------|
| 1 | **バックエンドが一度もコンパイルされていない**。開発環境が NuGet(api.nuget.org)遮断だったため、`8f52278` 以降の全 .cs 変更はコンパイラ検証なし(該当コミットは全て本文に VERIFICATION CAVEAT を明記) | 最重要 | Opus O-1 |
| 2 | xunit テスト群(Loco.Core.Tests 復活 50 件 + 新規、Loco.Api.Tests)が「実装済み・未実行」 | 高 | Opus O-1 |
| 3 | 実行履歴がインメモリ(`ExecutionRegistry`)— API 再起動で消失、水平スケール不可 | 高 | Opus O-2 |
| 4 | E2E(エディタ → API → エンジン → コネクタ)疎通が未実施 — API がビルド不能だったため | 高 | Opus O-4 |
| 5 | custom condition 式は UI で保存できるがエンジン未評価(現状は「常時追従」扱い — UI の helpText に明記済み) | 中 | Opus O-3 |
| 6 | CI パイプライン不在 — フロントの 256 テストすら自動実行されない | 中 | Opus O-5 |
| 7 | `src/Loco.Core/Practical/` の 32 クラスがデッドコード(fully dead 20 / dead island 12 / wired は SimpleLogger のみ) | 中 | Sonnet S-3 |
| 8 | ESLint warning 102 件(エラーは 0。`lint:strict` は未達) | 低 | Sonnet S-2 |
| 9 | `loco preset` はシミュレーション、`loco backup` はスタブ(いずれも出力で明示済み) | 低 | 据え置き(誠実表示済み) |
| 10 | `docs/PHASE_9`〜`PHASE_14` は実装されていない設計文書(各ファイルに NOT IMPLEMENTED バナー付与済み) | 低 | 据え置き or 削除判断 |

## 改善案(優先度付きロードマップ)

### P0 — 信頼性の土台(他の全てに先行)
- **O-1**: NuGet 到達可能環境で初の `dotnet restore/build/test` を実行し、コンパイルエラーを修正、全テストを緑にする。
- **O-5**: GitHub Actions CI(front: tsc/vitest/build/lint、back: build/test)。

### P1 — 製品としての完成度
- **O-2**: 実行履歴のファイル永続化(既存 `JsonFileWorkflowStore` パターン踏襲)。
- **O-3**: custom condition 式の実評価(限定文法から開始)。
- **O-4**: エディタ→API の E2E 疎通確認と統合テスト常設。

### P2 — 保守性・磨き込み
- **S-1**: 残りユーティリティ/フックのテスト網羅(対象リストは Sonnet 指示書)。
- **S-2**: ESLint warning 0 化。
- **S-3**: `Practical/` デッドコード削除。
- **S-4**: ドキュメント数値の実態同期。

---

## 参考: 本セッションの主要コミット

| SHA | 内容 |
|-----|------|
| `447a1c1` | フロントエンドを初めてコンパイル可能に(35 エラー修正) |
| `169c338` | ワークフロー永続化ストア + エンジンキャンセル + 50 テスト復活 |
| `8f52278` | Loco.Api 全面リーン書き直し(認証・封筒契約・実 CRUD) |
| `1993393` | Loco.Api.Tests(WebApplicationFactory 統合テスト) |
| `4ccdaf7` | エディタ認証配線・フックオーダー修正・ESLint 導入 |
| `d42c820` | ドキュメント誠実化 |
| `e75e3dc` | cron の TZ/DST 対応 |
| `65e1094` | 全コネクタの HttpClient リーク修正 |
| `944dfdc` | CLI `run-visual`(CLI から初めてコネクタが使用可能に) |
| `51c1ecd`〜`3d86379` | エッジ条件ルーティング UI(新機能 + 自己レビュー修正) |
| `b10c0e2`, `3a55911` | 監査確定バグの surgical 修正(コネクタ 6 件 + CLI 5 件) |
| `87092e6` | CLI 全コマンド配線 |
| `41a89be`, `c1dce37`, `af10035` | テスト網羅 3 バッチ(196→256) |
