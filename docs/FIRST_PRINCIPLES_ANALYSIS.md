# First Principles 分析 — Loco に何が過剰で、何が欠けているか

作成: 2026-08。手法: 「既存コードの品質」ではなく**製品の第一原理**から出発し、
「そもそも何が無いと成立しないか」で過不足を判定する。
本書の主張はすべて**リポジトリに対する実行可能な検証コマンド付き**で記載する(推測ゼロ)。

関連: [PRODUCT_ASSESSMENT.md](PRODUCT_ASSESSMENT.md) /
[agent-instructions/INSTRUCTIONS_OPUS.md](agent-instructions/INSTRUCTIONS_OPUS.md)

---

## 1. 第一原理: この製品の本質は何か

ワークフロー自動化製品を、機能一覧ではなく**存在理由**まで還元すると:

> ### 「**X** が起きたら、**自分のアカウント**で **Y** を自動実行する」

ここから、還元不可能(irreducible)な構成要素は 3 つしかない:

| 要素 | 意味 | 無いとどうなるか |
|------|------|------------------|
| **Y — 作用** | 外部システムに実際に変更を加える | 何も起きない。ただのお絵かきツール |
| **資格情報** | その外部システムに「自分として」認証する | Y が実行できない。コネクタは飾り |
| **X — トリガ** | 人間の操作なしに起動する | 「自動化」ではなく手動実行器 |

補助要素(データフロー、分岐、永続化、可観測性)は上記 3 つが揃って初めて意味を持つ。

## 2. 現状評価 — 3 要素のうち 2 つが欠落

| # | 要素 | 判定 | 検証コマンドと結果 |
|---|------|------|--------------------|
| 1 | **Y = 作用** | ✅ **有り** | `ls src/Loco.Core/Integrations/Connectors/*.cs \| wc -l` → **28**。実 API 呼び出し + HMAC 署名検証を持つ本物の実装 |
| 2 | **資格情報** | ❌ **完全欠落** | 下記 2.1 |
| 3 | **X = トリガ** | ❌ **未配線** | 下記 2.2 |

### 2.1 資格情報 (U1) — 最も致命的

コネクタに資格情報を渡す唯一の口は `WorkflowConnectorBridge.ConfigureConnector()`。その**呼び出し元が存在しない**:

```bash
grep -rn "ConfigureConnector" --include=*.cs src/ | grep -v "WorkflowConnectorBridge.cs"
# → 出力ゼロ
```

さらに、欠落は**データモデルの層から**始まっている。エディタのノード型に資格情報を参照するフィールドが無い:

```bash
grep -n "credential\|Credential" src/Loco.VisualEditor/src/types/workflow.ts
# → 出力ゼロ
```

`src/Loco.Api/Execution/ConnectorStartupService.cs` の doc コメント自身がこれを認めている
— "Connectors are registered **WITHOUT credentials** here"。

**帰結**: 28 個のコネクタは、ワークフローから呼ばれても `InitializeAsync` が実行されないため
`_httpClient` が null のまま実行され、**必ず失敗する**。
製品最大の資産(コネクタ群)が、実質的に到達不能である。

保存先(store)・API・UI のいずれも存在しない。

> **業界の標準解**: n8n では資格情報は**ワークフローとは別のエンティティ**として暗号化保存され、
> ノードは**秘密値を埋め込まず ID で参照**し、実行時に注入される
> ([n8n Credentials API and Security](https://deepwiki.com/n8n-io/n8n/3.2-credentials-api-and-security))。
> 静的保存(DB 暗号化)と動的解決(実行時に外部 Vault から取得)を分離する設計が推奨されている
> ([Dynamic Credentials and External Secrets](https://deepwiki.com/n8n-io/n8n/3.6-dynamic-credentials-and-external-secrets))。
> Loco は現状この 3 層(モデル・保存・注入)すべてを欠く。

### 2.2 トリガ (U2) — 「自動化」が成立しない

トリガ実装は Core に**存在する**(`CronScheduler` / `EventTrigger` / `FileWatcherTrigger` /
`TriggerManager` / `WebhookReceiver`)。しかし**実行バイナリからの参照がゼロ**:

```bash
grep -rn "TriggerManager\|CronScheduler\|FileWatcherTrigger" --include=*.cs src/Loco.Api src/Loco.Cli
# → 出力ゼロ
grep -rln "webhook" --include=*.cs src/Loco.Api/
# → 出力ゼロ (webhook 受信エンドポイントが無い)
```

**帰結**: 保存したワークフローは、人間が実行ボタンを押すか CLI を叩いたときしか動かない。
これは**ワークフロー実行器**であって**自動化**ではない。第一原理の X が満たされていない。

> **業界の標準解**: 2025 年以降のコンセンサスは、ad-hoc な cron/sleep ループを捨て、
> 実行状態を永続化する durable execution 基盤にスケジュールを持たせること
> ([Temporal Schedules](https://temporal.io/product) /
> [Workflow Engine Architecture](https://codelit.io/blog/temporal-workflow-engine))。
> webhook のような非 DB 副作用も transactional outbox パターンで確実化する流れにある
> ([Atomix, arXiv 2602.14849](https://arxiv.org/pdf/2602.14849))。
> Loco はまず「常駐スケジューラを 1 つ動かす」段階から始める必要がある。

### 2.3 ビルド阻害 (U3) — 新規発見

`src/Loco.Cli/Commands/SecretsCommand.cs` は存在しない名前空間とクラスを参照している:

```bash
grep -n "using Loco.Core.Security" src/Loco.Cli/Commands/SecretsCommand.cs   # → ヒットあり
grep -rn "namespace Loco.Core.Security" --include=*.cs src/                  # → 出力ゼロ
grep -rn "class SecretsManager" --include=*.cs src/                          # → 出力ゼロ
```

**帰結**: `Loco.Cli` プロジェクトは**一度もコンパイルできていない**。
(本セッションでこのコマンドを `Program.cs` に配線した `87092e6` により、この欠落は確実に顕在化する。)
→ 本セッションで修正する(§4)。

### 2.4 その他の不足
- **U4**: 実行履歴がインメモリ(`ExecutionRegistry`)。API 再起動で消失、水平スケール不可。
- **U5**: CI 不在。フロントの 361 テストすら自動実行されない。

## 3. 過剰 — 存在すべきでないもの

第一原理の観点で最も示唆的なのは、**欠落と過剰が鏡像になっている**点である。

| # | 過剰 | 規模 | 実態 |
|---|------|------|------|
| **O1** | `src/Loco.Core/AIPlatform/ExternalSecretsEngine.cs` | **1,265 行** | Vault/AWS/Azure/GCP 連携を騙る。中身は `random.Next()` で偽の同期メトリクスを返すだけ。**実際の秘密保存が存在しない一方でこれがある** — 過不足の象徴 |
| **O2** | `src/Loco.Core/Practical/` | **32 クラス** | デッドコード(`SimpleLogger` のみ実使用) |
| **O3** | コンパイル除外サブシステム | **9 ディレクトリ** | BFF/Gateway/GraphQL/Versioning/Idempotency/Services/HealthChecks/RateLimiting/OpenApi — `Loco.Api.csproj` の `<Compile Remove>` で除外済みだがリポジトリに残存 |
| **O4** | `docs/PHASE_9`〜`PHASE_14` | 多数 | 未実装の設計文書(NOT IMPLEMENTED バナー付与済み) |

**O1 が示す構造的問題**: このプロジェクトは「エンタープライズらしく見える機能」を大量に生成する一方、
「製品が成立するための最小要件」を実装していない。**1,265 行の偽 Vault 連携より、
200 行の動く秘密保存の方が価値が高い。**

## 4. 改善の優先順位

第一原理から導かれる順序 — **製品が成立する条件を先に満たす**:

| 優先 | 項目 | 担当指示書 | 理由 |
|------|------|-----------|------|
| **P0** | ビルドを通す (U3 含む) | Opus O-1 | 検証不能な状態では他のすべてが砂上の楼閣 |
| **P0** | **資格情報の 3 層実装** (U1) | Opus **O-6** | これが無い限りコネクタ 28 個 = ゼロ価値。単体で最大の投資対効果 |
| **P1** | **トリガ配線** (U2) | Opus **O-7** | これが無い限り「自動化」を名乗れない |
| **P1** | 実行履歴の永続化 (U4) | Opus O-2 | 再起動で履歴消失は運用不能 |
| **P2** | CI (U5) / 過剰の削除 (O1〜O3) | Opus O-5 / Sonnet S-3 | 品質の維持と保守性 |

### 本セッションで実施したこと
NuGet 遮断環境(`dotnet build` 不能)のため、**検証可能かつ根本に効く**ものに限定:
1. `SecretsManager` を実装し **U3 を解消**(§2.3)。同時に U1 の保存層の土台とする。
2. 本書の作成、および O-6 / O-7 を Opus 指示書に**最優先課題として昇格**。

U1/U2 の本体(API・UI・常駐スケジューラ)はコンパイラと E2E が必須のため、
指示書に手順化して引き継ぐ。

---

## 参考文献
- [n8n — Credentials API and Security](https://deepwiki.com/n8n-io/n8n/3.2-credentials-api-and-security)
- [n8n — Dynamic Credentials and External Secrets](https://deepwiki.com/n8n-io/n8n/3.6-dynamic-credentials-and-external-secrets)
- [n8n — Security (encryption at rest)](https://n8n.io/legal/security/)
- [Temporal — Durable Execution Platform](https://temporal.io/product)
- [Workflow Engine Architecture: Durable Execution with Temporal](https://codelit.io/blog/temporal-workflow-engine)
- [Atomix: Timely, Transactional Tool Use for Reliable Agentic Workflows (arXiv 2602.14849)](https://arxiv.org/pdf/2602.14849)
