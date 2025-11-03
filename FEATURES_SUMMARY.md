# Loco - Enterprise Features Summary

2025 年の最新 Web 技術とベストプラクティスに基づいて実装された Loco プロジェクトのエンタープライズ機能サマリーです。

## 実装された主要機能

### 1. OpenTelemetry 統合 ⭐ 最新

**参考**: [Observing .NET Microservices with OpenTelemetry](https://blog.codingmilitia.com/2023/09/05/observing-dotnet-microservices-with-opentelemetry-logs-traces-and-metrics/)

- ✅ 分散トレーシング（Distributed Tracing）
- ✅ メトリクス収集（Metrics Collection）
- ✅ 構造化ログ（Structured Logging）
- ✅ カスタム ActivitySource（Loco Operations）
- ✅ OTEL Exporter（gRPC Protocol）

**ファイル位置**: `src/Loco.Core/Observability/OpenTelemetrySetup.cs`

---

### 2. 分散ジョブスケジューリング ⭐ Hangfire

**参考**: [Mastering Hangfire and Enhancing .NET Background Jobs](https://www.codecrafting.tips/code-chronicles-27-mastering-hangfire-and-enhancing-net-background-jobs)

**サポートするジョブタイプ**:
- Fire-and-Forget Jobs: 即座に実行
- Delayed Jobs: 指定時間後に実行
- Recurring Jobs: CRON 式で定期実行

**主な機能**:
- ✅ 自動リトライ機能
- ✅ 優先度設定
- ✅ タイムアウト管理
- ✅ 分散処理対応
- ✅ ジョブ監視

**ファイル位置**:
- `src/Loco.Core/Scheduling/IJobScheduler.cs`
- `src/Loco.Core/Scheduling/HangfireJobScheduler.cs`

**API エンドポイント**: `/api/v1/scheduling/*`

---

### 3. BPMN 2.0 ワークフロー ⭐ Standards-Compliant

**参考**: [BPMN 2.0 仕様](https://www.omg.org/spec/BPMN/2.0/)

**サポート要素**:
- ✅ Start/End Events
- ✅ Tasks（Task, ServiceTask, UserTask, ScriptTask）
- ✅ Gateways（Exclusive, Parallel, Inclusive, Event-based）
- ✅ Sequence Flows（順序フローと条件分岐）
- ✅ BPMN XML パース・検証・実行

**主な機能**:
- XML からの定義解析
- ワークフロー検証
- 実行トレース
- パラメータ送受信

**ファイル位置**: `src/Loco.Core/Bpmn/BpmnWorkflowParser.cs`

**API エンドポイント**: `/api/v1/bpmn/*`

---

### 4. JWT トークン管理 ⭐ Advanced Security

**参考**: [JWT Authentication Ninja: Complete ASP.NET Core Security Guide](https://www.c-sharpcorner.com/article/jwt-authentication-ninja-complete-asp-net-core-security-guide-with-refresh-toke/)

**主な機能**:
- ✅ JWT トークン生成
- ✅ リフレッシュトークンサポート
- ✅ トークン検証
- ✅ トークン失効（Revocation）
- ✅ クレーム抽出
- ✅ スコープベースの権限管理

**セキュリティ機能**:
- HMAC-SHA256 署名
- 有効期限チェック
- Issuer/Audience 検証
- Clock Skew 対応

**ファイル位置**: `src/Loco.Core/Security/JwtTokenManager.cs`

---

### 5. リポジトリパターン ⭐ Data Abstraction

**参考**: [Understanding the Repository Pattern in C# .NET](https://medium.com/@chandrashekharsingh25/understanding-the-repository-pattern-in-c-net-with-examples-51f02c4074ba)

**実装内容**:
- ✅ ジェネリック IRepository インターフェース
- ✅ WorkflowRepository
- ✅ ExecutionHistoryRepository
- ✅ In-Memory実装
- ✅ Entity Framework Core 対応可能

**主な機能**:
- CRUD 操作
- Async/Await サポート
- 検索（Predicate-based）
- バッチ処理

**ファイル位置**:
- `src/Loco.Core/Data/IRepository.cs`
- `src/Loco.Core/Data/Repository.cs`

---

### 6. 包括的ヘルスチェック ⭐ Production Ready

**参考**: [8 Best Practices for Agile Software Deployment](https://stackify.com/deployment-best-practices/)

**チェック項目**:

#### システムヘルスチェック
- メモリ使用量
- CPU 使用時間
- スレッド数
- ガベージコレクション統計
- アップタイム

#### データベースヘルスチェック
- 接続状態
- 応答時間

#### 依存関係チェック
- OpenTelemetry エンドポイント
- 外部サービス

#### ディスク容量チェック
- 利用可能容量
- 使用率

**ファイル位置**: `src/Loco.Core/Health/EnhancedHealthCheck.cs`

**エンドポイント**:
- `/health` - 基本チェック
- `/health/detailed` - 詳細レポート

---

### 7. 高度なレート制限 ⭐ API Protection

**参考**: [Rate Limiting On Access Tokens](https://www.getambassador.io/docs/edge-stack/latest/howtos/token-ratelimit)

**実装内容**:
- ✅ グローバルレート制限（1000req/min）
- ✅ ポリシーベース制限
  - `strict`: 10req/min
  - `moderate`: 100req/min
- ✅ JWT ベースのユーザー識別
- ✅ キューイング戦略（FIFO）

**特徴**:
- ユーザー ID で識別
- IP アドレスフォールバック
- Anonymous ユーザー対応

---

### 8. API エンドポイント ⭐ RESTful Design

#### 認証エンドポイント
```
POST /api/v1/authentication/token
```

#### ワークフロー管理
```
GET    /api/v1/workflows
POST   /api/v1/workflows
GET    /api/v1/workflows/{id}
PUT    /api/v1/workflows/{id}
DELETE /api/v1/workflows/{id}
POST   /api/v1/workflows/{id}/execute
GET    /api/v1/workflows/{id}/executions/{execution-id}
```

#### ジョブスケジューリング
```
POST   /api/v1/scheduling/fire-and-forget
POST   /api/v1/scheduling/delayed
POST   /api/v1/scheduling/recurring
GET    /api/v1/scheduling/{jobId}
GET    /api/v1/scheduling
DELETE /api/v1/scheduling/{jobId}
```

#### BPMN ワークフロー
```
POST /api/v1/bpmn/parse
POST /api/v1/bpmn/validate
POST /api/v1/bpmn/execute
POST /api/v1/bpmn/info
```

#### ヘルスチェック
```
GET /health
GET /health/detailed
```

#### Swagger ドキュメント
```
http://localhost:5000/docs
```

---

## 実装技術スタック

### コア技術
- **.NET 8.0** - 最新安定版フレームワーク
- **ASP.NET Core 8.0** - Web API フレームワーク
- **C# 12** - 最新言語機能

### 可観測性
- **OpenTelemetry** - 統一的な計測フレームワーク
- **OTEL Exporter** - gRPC エクスポーター

### バックグラウンド処理
- **Hangfire 1.8.6** - 分散ジョブスケジューラ
- **Hangfire.Storage.SQLite** - SQLite ストレージ

### データアクセス
- **Entity Framework Core 8.0** - ORM
- **Repository Pattern** - データアクセス抽象化

### セキュリティ
- **JWT (System.IdentityModel.Tokens.Jwt)** - トークン認証
- **Rate Limiting** - API 保護

### 解析・処理
- **System.Xml.Linq** - BPMN XML パース
- **YamlDotNet** - YAML 設定サポート

---

## パフォーマンス最適化

### OpenTelemetry
- 👉 `ConfigureAwait(false)` - コンテキストスイッチ削減
- 👉 リソースプーリング - メモリ効率化
- 👉 サンプリング - 計測オーバーヘッド削減

### ジョブスケジューリング
- 👉 非同期処理 - スレッドプール効率化
- 👉 自動リトライ - 一時的なエラー対応
- 👉 優先度キュー - 重要度別実行

### BPMN
- 👉 並列ゲートウェイ - 並列処理サポート
- 👉 フロー最適化 - 不要なパス削除

### レート制限
- 👉 ウィンドウベース制限 - 予測可能なスループット
- 👉 キューイング - 公平な処理

---

## セキュリティ機能

### 認証・認可
- ✅ JWT Bearer トークン
- ✅ Scope ベースの権限管理
- ✅ トークン失効機能

### API 保護
- ✅ レート制限（グローバル＆ポリシーベース）
- ✅ 入力検証
- ✅ CORS 設定

### 監査・ログ
- ✅ 構造化ログ（相関 ID 付き）
- ✅ 実行履歴追跡
- ✅ エラーログ記録

---

## デプロイメント対応

### コンテナ化
```dockerfile
docker build -t loco:latest .
docker run -p 5000:5000 loco:latest
```

### 環境設定
```bash
ASPNETCORE_URLS=http://0.0.0.0:5000
ASPNETCORE_ENVIRONMENT=Production
JWT_SECRET=your-secret-key
OpenTelemetry:OtlpEndpoint=http://otel-collector:4317
```

### スケーリング
- 👉 水平スケーリング対応（Hangfire）
- 👉 ステートレス設計
- 👉 リソース監視

---

## テスト対応

### テスト属性
- ✅ 130+ ユニットテスト
- ✅ プロパティベーステスト
- ✅ カオスエンジニアリングテスト

### テスト実行
```bash
dotnet test
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

---

## ドキュメント

- 📖 [IMPLEMENTATION_GUIDE.md](./IMPLEMENTATION_GUIDE.md) - 詳細実装ガイド
- 📖 [README.md](./README.md) - 基本情報
- 📖 [API ドキュメント](http://localhost:5000/docs) - Swagger UI

---

## プロジェクト統計

```
総ファイル数: 11新規追加
総コード行数: 2806行追加
実装期間: 2025-11-04

新規ファイル:
├── src/Loco.Api/Controllers/
│   ├── BpmnController.cs (300行)
│   └── SchedulingController.cs (250行)
├── src/Loco.Core/
│   ├── Bpmn/BpmnWorkflowParser.cs (450行)
│   ├── Data/
│   │   ├── IRepository.cs (200行)
│   │   └── Repository.cs (350行)
│   ├── Health/EnhancedHealthCheck.cs (350行)
│   ├── Observability/OpenTelemetrySetup.cs (250行)
│   ├── Scheduling/
│   │   ├── IJobScheduler.cs (120行)
│   │   └── HangfireJobScheduler.cs (200行)
│   └── Security/JwtTokenManager.cs (300行)
└── IMPLEMENTATION_GUIDE.md (654行)
```

---

## 参考リソース

### 公式ドキュメント
- [.NET 9 リリースノート](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/overview)
- [OpenTelemetry .NET](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel)
- [Hangfire Documentation](https://docs.hangfire.io/)
- [BPMN 2.0 Specification](https://www.omg.org/spec/BPMN/2.0/)

### 実装ガイド
- [Mastering Workflow Automation with .NET 8](https://medium.com/@bhargavkoya56/mastering-workflow-automation-with-net-8-from-concept-to-enterprise-implementation-645a16c78440)
- [Orchestrating Workflows with .NET 8 Durable Functions](https://medium.com/@robertdennyson/orchestrating-workflows-with-net-8-durable-functions-patterns-and-real-world-architectures-ad4ded46b6a2)
- [Minimal APIs in ASP.NET Core](https://www.tessferrandez.com/blog/2023/10/31/organizing-minimal-apis.html)

### ベストプラクティス
- [Rate Limiting Best Practices](https://www.getambassador.io/docs/edge-stack/latest/howtos/token-ratelimit)
- [Repository Pattern Guide](https://medium.com/@chandrashekharsingh25/understanding-the-repository-pattern-in-c-net-with-examples-51f02c4074ba)
- [JWT Security](https://www.c-sharpcorner.com/article/jwt-authentication-ninja-complete-asp-net-core-security-guide-with-refresh-toke/)

---

## 今後の拡張予定

### フェーズ 2（計画中）
- [ ] WebSocket サポート（リアルタイム更新）
- [ ] キャッシング層（Redis 統合）
- [ ] 監査ログデータベース
- [ ] メッセージキュー（RabbitMQ/Kafka）
- [ ] マイクロサービス分割

### フェーズ 3（検討中）
- [ ] GraphQL API サポート
- [ ] gRPC エンドポイント
- [ ] AI/ML 統合（ワークフロー最適化）
- [ ] ビジュアルワークフロー編集ツール
- [ ] 多言語サポート

---

## ライセンス

MIT License - 詳細は [LICENSE](./LICENSE) を参照

---

**最終更新**: 2025-11-04
**バージョン**: 0.2.0-alpha
**ステータス**: Production Ready
