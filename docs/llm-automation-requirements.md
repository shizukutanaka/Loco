# オープンLLM自動化ソフト 要件仕様（詳細版） / Open LLM Automation Software Requirements (Detailed)

本書は、ローカルまたは選択的クラウドで動作するオープンソースLLMを安全にダウンロード・管理し、Tasker風の「トリガー→条件→アクション」による自動化を自然言語/GUIで構築・運用するソフトウェアの要件を定義します。
This document defines requirements for software that safely downloads and manages open LLMs locally (or optionally in cloud) and enables Tasker-like automation via "Trigger → Condition → Action" built and operated through natural language and GUI.

---

## 目次 / Table of Contents
- 概要 / Overview
- 目的・ゴール / Goals
- 前提・制約 / Assumptions & Constraints
- ステークホルダー / Stakeholders
- MVP（必須） / MVP (Must-haves)
- 機能要件 / Functional Requirements (F1–F5, S1–S4, C1–C3)
- 非機能要件 / Non-Functional Requirements
- セキュリティ・プライバシー要件 / Security & Privacy Requirements
- システム構成（高水準） / High-level System Architecture
- オートメーション言語（DSL） / Automation DSL
- モデルダウンロード・管理 / Model Download & Management
- エラーハンドリング・デバッグ / Error Handling & Debugging
- テスト計画 / Test Plan
- 運用・配布・ライセンス / Operations, Distribution, Licensing
- ロードマップ / Roadmap
- 受け入れ基準（例） / Acceptance Criteria (Examples)
- 未決事項 / Open Questions
- 次のアクション / Next Actions
- 付録A：JSON Schema（ルール） / Appendix A: JSON Schema (Rule)

---

## 概要 / Overview
- 端末に安全にオープンLLMを導入・管理し、GUIと自然言語で自動化ルールを作成・実行できるクロスプラットフォームソフト。
- Primary target Android first; designed for portability to desktop/server.

## 目的・ゴール / Goals
- ユーザーがローカル動作（または選択的クラウド）のLLMを容易に導入・切替できる。
- Taskerのように端末イベントをトリガーとする自動化が可能。
- 自然言語でルール作成・編集。LLMが意図解釈/テンプレート化を補助。
- セキュリティとプライバシーを最優先（隔離実行、最小権限、同意管理）。

## 前提・制約 / Assumptions & Constraints
- 初期プラットフォーム：Android（後に Linux/Windows/macOS）。
- モデル形式：ローカル実行可能なオープンフォーマット（例：GGUF、ONNX）。
- ダウンロードはユーザー承認必須。ストレージ/計算資源の制御はユーザー責任。
- 各モデルのライセンス順守。商用可否を明確表示。

## ステークホルダー / Stakeholders
- エンドユーザー（個人・技術者） / End users
- 管理者（企業導入） / Admins
- 開発者（プラグイン開発） / Developers
- セキュリティ/法務 / Security & Legal

---

## MVP（必須） / MVP (Must-haves)
- LLMマネージャ：追加（URL/ローカル）、署名/ハッシュ検証、メタ表示、選択・削除、簡易推論テスト。
- オートメーションエンジン：基本トリガー（時刻、アプリ起動、通知）、基本アクション（起動、HTTP、通知、ファイル、スクリプト、TTS、LLMクエリ）。
- ルール作成UI：テンプレ＋GUIエディタ＋自然言語入力→草案生成→ユーザー承認保存。
- セキュリティ：ネットワーク許可はルール単位、最小権限、LLM実行は分離プロセス/サンドボックス。
- ログ＆デバッグ：実行ログ、失敗時のリトライ、明確なエラーメッセージ。

---

## 機能要件 / Functional Requirements

### F1 — モデル管理（LLMマネージャ） / Model Management
- 追加モード：URL、ローカルファイル、LANミラー。
- 事前表示：名称、バージョン識別子、サイズ、推奨RAM/CPU/GPU、ライセンス要点。
- 検証：SHA256、可能なら署名（PGP等）。
- 状態：インストール済/利用可能/破損。破損検出時の復旧フロー。
- テスト推論：簡易プロンプトで応答とレイテンシ/メモリを計測。
- 受け入れ基準：追加→検証成功→「利用可能」表示→テスト応答取得。

### F2 — トリガー管理 / Trigger Management
- 種類：時刻、カレンダー、アプリ起動/終了、通知、WebHook、ジオフェンス、バッテリー/充電。
- 属性：遅延、優先度、サプレッション（短時間の多重発火抑制）。
- 受け入れ基準：各トリガーで発火し、理由がログに残る。

### F3 — アクションセット / Action Set
- 基本：アプリ起動、通知、HTTP、ファイルI/O、TTS、シェル/スクリプト（制限）、LLMクエリ（同期/非同期）。
- パイプ：`$\{var\}` による変数渡し、LLM出力の次段入力。
- 制限：シェルはホワイトリスト＆明示承認。ネットワークはルール単位許可。
- タイムアウト/キャンセル：各アクションで設定可能。
- 受け入れ基準：副作用が目視/ログで確認可能（HTTPコード、通知表示など）。

### F4 — ルールエディタ（GUI + 自然言語） / Rule Editor
- GUI：ブロック型（トリガー→条件→アクション）。ドラッグ＆ドロップ。有効/無効切替。
- 自然言語：日本語入力→LLMがDSL候補複数生成。差分表示。承認後保存。
- 版管理：改訂履歴とロールバック。
- 変数：ローカル/グローバル/シークレット（表示不可）。
- 受け入れ基準：日本語入力→草案提示→編集→保存→実行可能。

### F5 — 実行安全モデル（サンドボックス） / Execution Safety (Sandbox)
- 分離：LLM推論は別プロセス/Wasm/コンテナ。
- 権限：ファイル/ネット/センサーアクセスのACL。作成時に必要権限提示と同意。
- 監査：ネットワークはホワイトリスト・DNSログ。リソース制限（CPU/メモリ/IO）。
- 安全チェック：危険語フィルタ、PIIリーク検査（オプション）。
- 受け入れ基準：未許可アクセスはブロックされ、監査ログで理由が確認可能。

#### 推奨（S1–S4） / Recommended
- S1 — プラグイン/アクションSDK（Kotlin/Java, Python等）。権限宣言テンプレ、サンプル提供。
- S2 — リッチトリガー（複合条件、遅延、変数、ループ、例外処理）。
- S3 — モデル自動選択/フォールバック（軽量モデル優先、許可時クラウドへ）。
- S4 — レシピ共有（署名付エクスポート、インポート時の権限マッピング）。

#### あると良い（C1–C3） / Nice-to-have
- C1 — リモート管理API（安全認証）。
- C2 — ワークフロー可視化（グラフ）。
- C3 — 端末間同期（暗号化バックアップ）。

---

## 非機能要件 / Non-Functional Requirements
- 応答性：UI操作95%が300ms以内。
- 起動：コールドスタート目標3秒（端末差あり）。
- リソース：モデルロード中のメモリ表示と上限制御。
- 可用性：失敗時リトライ。重要ルールは確実実行モード。
- スケーラビリティ：高頻度トリガーでのCPU抑制（ワーカープール、レート制御）。

## セキュリティ・プライバシー要件 / Security & Privacy
- 最小権限原則。ユーザー承認ログ保持。
- モデル検証：ダウンロード/ロード時の整合性とライセンス再確認。
- 実行隔離：Wasm優先。不可ならプロセス分離＋OSサンドボックス。
- シークレット管理：OSキーストア。平文保存禁止。
- データ最小化と自動マスキング。監査証跡のエクスポート。
- 既定でオフライン実行を選択可。ネットワークはルール単位で許可。

---

## システム構成（高水準） / High-level Architecture
- UI層：モデル管理、ルールエディタ、ログ、設定。
- コアランタイム：イベント監視、条件評価、アクション実行（ワーカープール）。
- LLMマネージャ：ダウンロード、検証、ランタイムアダプタ（Wasm/プロセス/コンテナ）。
- サンドボックス：各ランタイムは監視API（ヘルス/メモリ/現在プロンプト）を公開。
- プラグインハブ：動的登録、署名検証、権限分離。
- 永続化：暗号化SQLite/LevelDB（ルール、モデルメタ、ログ）。
- 通信：ミラー更新、オプトインのテレメトリ、リモートAPI（OAuth2）。

## オートメーション言語（DSL） / Automation DSL
- JSONベース。JSON Schemaで型定義（Trigger, Condition, Action, Variable, Policy）。
- 条件：比較、正規表現、時間演算、論理結合。
- 自然言語→DSL：system prompt付与→候補複数→安全チェック→差分表示→承認保存。

## モデルダウンロード・管理 / Model Download & Management
- ソース：公式URL、ミラー、ローカル。
- 事前表示：メタ（推定RAM、量子化、ライセンス）。
- ダウンロード：TLS検証、再試行、断点再開。
- 検証：SHA256、署名確認。破損時は再取得提案。
- インストール：適切なフォルダ、権限制御、初回ロードテスト。
- 登録情報：ソース、ハッシュ、署名、取得日時。

## エラーハンドリング・デバッグ / Error Handling & Debugging
- 段階ごとにエラーコード。ユーザー向け簡潔文＋開発者向け詳細ログ。
- 自動回復：指数バックオフ、破損時の再ダウンロード。
- デバッグモード：詳細トレース（PIIマスク）。

## テスト計画 / Test Plan
- 単体：トリガー、条件、アクション、DSLパーサ。
- 結合：GUI→DSL→保存→実行。
- モデル統合：小型/中型モデルで応答の一貫性確認。
- セキュリティ：未許可ネットワーク、権限昇格、サンドボックス脱出試行。
- 負荷：高頻度トリガー下での安定性。

## 運用・配布・ライセンス / Operations & Licensing
- コアはOSS。モデルは各ライセンスに従いUIで明示。
- アップデート：セキュリティ修正は強調。ユーザー承認で適用。
- 企業向け：オンプレ提供、集中管理、監査ログエクスポート。

## ロードマップ / Roadmap
- Phase 0（2–4週）：PoC（ローカル小型モデル＋時刻トリガー）。
- Phase 1（8–12週）：MVP（モデル管理、GUI、主要トリガー/アクション、基本サンドボックス）。
- Phase 2（12–24週）：自然言語UX強化、プラグインSDK、共有レシピ、複合条件。
- Phase 3（継続）：クロスプラットフォーム、企業機能、コミュニティ運用。

## 受け入れ基準（例） / Acceptance Criteria (Examples)
- 平日7:00ニュース読み上げ：指定時刻にTTSが実行、ログに発火理由と使用モデル。
- モデル追加検証：URL追加→SHA256合格→テストプロンプト応答。
- 未許可ネットワーク：HTTPがブロックされ、UIに理由表示。
- 共有レシピ：インポート→変数マッピング→実行成功。

## 未決事項 / Open Questions
- 初期サポート形式（GGUF優先か）。
- 完全ローカル優先か、クラウドフォールバック許可か。
- Android以外の優先度。

## 次のアクション / Next Actions
- 未決事項の意思決定。
- 主要画面ワイヤーフレーム作成。
- APIスキーマ（JSON Schema）作成。
- PoC実装（Phase 0）タスク分解と見積り。

## 付録A：JSON Schema（ルール） / Appendix A: JSON Schema (Rule)
```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "id": {"type": "string"},
    "name": {"type": "string"},
    "trigger": {"type": "object"},
    "conditions": {"type": "array"},
    "actions": {"type": "array"}
  },
  "required": ["id", "name", "trigger", "actions"]
}
```
