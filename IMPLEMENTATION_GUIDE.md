# Loco - Advanced Enterprise Implementation Guide

このドキュメントは、YouTube と WEB の情報に基づいて実装した Loco プロジェクトの新機能と、その使用方法について説明します。

## 目次

1. [OpenTelemetry 統合](#opentelemetry-統合)
2. [分散ジョブスケジューリング](#分散ジョブスケジューリング)
3. [BPMN 2.0 ワークフロー](#bpmn-20-ワークフロー)
4. [JWT トークン管理](#jwt-トークン管理)
5. [リポジトリパターン](#リポジトリパターン)
6. [ヘルスチェック](#ヘルスチェック)
7. [API リファレンス](#api-リファレンス)

---

## OpenTelemetry 統合

### 概要

OpenTelemetry による包括的な可観測性を実装しました。ログ、メトリクス、トレース、の 3 つの柱に対応しています。

### 機能

- **分散トレース**: リクエストの全体フローをトレース
- **メトリクス**: パフォーマンスメトリクスのリアルタイム監視
- **構造化ログ**: 相関 ID 付きの詳細ログ出力

### 設定

```csharp
// Program.cs で以下を追加
services.AddLocoObservability(
    otlpEndpoint: "http://localhost:4317"
);
```

### カスタムトレース

```csharp
using Loco.Core.Observability;

// ワークフロー実行のトレース
using var activity = LocoActivitySource.StartWorkflowExecution("workflow-123");
// ... ワークフロー実行 ...
activity?.Stop();

// ジョブスケジューリングのトレース
using var jobActivity = LocoActivitySource.StartJobScheduling("job-456", "workflow-execution");
```

### カスタムメトリクス

```csharp
// ワークフロー実行を記録
LocoMetrics.WorkflowExecutionCounter.Add(1, new KeyValuePair<string, object?>("workflow_id", "workflow-123"));

// ワークフロー実行時間を記録
LocoMetrics.WorkflowExecutionDuration.Record(duration.TotalMilliseconds);
```

---

## 分散ジョブスケジューリング

### 概要

Hangfire ベースの分散ジョブスケジューラを実装しました。Fire-and-Forget、Delayed、Recurring ジョブをサポートしています。

### インターフェース

```csharp
public interface IJobScheduler
{
    Task<string> ScheduleFireAndForgetAsync(ScheduledJob job);
    Task<string> ScheduleDelayedAsync(ScheduledJob job, TimeSpan delay);
    Task<string> ScheduleRecurringAsync(ScheduledJob job, string cronExpression);
    Task<JobDetails?> GetJobAsync(string jobId);
    Task<bool> DeleteJobAsync(string jobId);
    Task<IEnumerable<JobDetails>> ListJobsAsync();
}
```

### API エンドポイント

#### Fire-and-Forget ジョブのスケジュール

```bash
POST /api/v1/scheduling/fire-and-forget
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "immediate-workflow",
  "workflowId": "workflow-123",
  "parameters": {
    "user_id": "123",
    "action": "process"
  },
  "priority": 5,
  "maxRetries": 3
}

Response:
{
  "jobId": "job-uuid-here"
}
```

#### Delayed ジョブのスケジュール

```bash
POST /api/v1/scheduling/delayed
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "delayed-workflow",
  "workflowId": "workflow-456",
  "delaySeconds": 3600,
  "parameters": {},
  "maxRetries": 5
}
```

#### Recurring ジョブのスケジュール

```bash
POST /api/v1/scheduling/recurring
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "daily-cleanup",
  "workflowId": "cleanup-workflow",
  "cronExpression": "0 2 * * *",
  "parameters": {}
}
```

#### ジョブの詳細を取得

```bash
GET /api/v1/scheduling/{jobId}
Authorization: Bearer {token}

Response:
{
  "id": "job-uuid",
  "name": "immediate-workflow",
  "state": "Succeeded",
  "createdAt": "2025-11-04T10:00:00Z",
  "lastExecutedAt": "2025-11-04T10:05:00Z",
  "nextExecutionAt": null,
  "attempts": 1,
  "result": "Workflow completed successfully"
}
```

#### すべてのジョブをリスト

```bash
GET /api/v1/scheduling
Authorization: Bearer {token}

Response: [
  { "id": "job-1", "name": "job-1-name", "state": "Succeeded", ... },
  { "id": "job-2", "name": "job-2-name", "state": "Processing", ... }
]
```

#### ジョブを削除

```bash
DELETE /api/v1/scheduling/{jobId}
Authorization: Bearer {token}
```

---

## BPMN 2.0 ワークフロー

### 概要

BPMN 2.0 標準に準拠したワークフロー定義をサポートします。ビジュアルプロセス定義を XML で表現できます。

### サポートされる要素

- **イベント**: StartEvent, EndEvent
- **タスク**: Task, ServiceTask, UserTask, ScriptTask
- **ゲートウェイ**: ExclusiveGateway, ParallelGateway, InclusiveGateway, EventBasedGateway
- **シーケンスフロー**: タスク間の接続と条件付きフロー

### API エンドポイント

#### BPMN ワークフローを解析

```bash
POST /api/v1/bpmn/parse
Content-Type: application/json
Authorization: Bearer {token}

{
  "bpmnXml": "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<definitions ...>"
}

Response:
{
  "id": "Process_1",
  "name": "Sample Process",
  "elements": [
    {
      "id": "StartEvent_1",
      "name": "Start",
      "type": "StartEvent",
      "incomingFlows": [],
      "outgoingFlows": ["Flow_1"]
    },
    ...
  ],
  "sequenceFlows": [...],
  "gateways": [...]
}
```

#### BPMN ワークフローを検証

```bash
POST /api/v1/bpmn/validate
Content-Type: application/json
Authorization: Bearer {token}

{
  "bpmnXml": "..."
}

Response:
{
  "isValid": true,
  "workflowId": "Process_1",
  "elementCount": 5,
  "flowCount": 4
}
```

#### BPMN ワークフローを実行

```bash
POST /api/v1/bpmn/execute
Content-Type: application/json
Authorization: Bearer {token}

{
  "bpmnXml": "...",
  "parameters": {
    "approver": "john@example.com",
    "amount": 5000
  }
}

Response:
{
  "success": true,
  "output": {
    "approver": "john@example.com",
    "amount": 5000,
    "approved": true
  },
  "errorMessage": null,
  "duration": "00:00:01.2345",
  "executedElements": ["StartEvent_1", "Task_1", "EndEvent_1"]
}
```

#### ワークフロー情報を取得

```bash
POST /api/v1/bpmn/info
Content-Type: application/json
Authorization: Bearer {token}

{
  "bpmnXml": "..."
}

Response:
{
  "workflowId": "Process_1",
  "workflowName": "Approval Process",
  "elementCount": 5,
  "startEvents": ["Start"],
  "endEvents": ["End"],
  "tasks": ["Review Task", "Approval Task"],
  "gateways": ["ExclusiveGateway"]
}
```

### BPMN XML 例

```xml
<?xml version="1.0" encoding="UTF-8"?>
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
             xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI"
             id="diagram_1">
  <process id="Process_1" name="Approval Workflow">
    <startEvent id="StartEvent_1" name="Start"/>
    <task id="Task_1" name="Review Request">
      <incoming>Flow_1</incoming>
      <outgoing>Flow_2</outgoing>
    </task>
    <exclusiveGateway id="Gateway_1" name="Approved?">
      <incoming>Flow_2</incoming>
      <outgoing>Flow_3</outgoing>
      <outgoing>Flow_4</outgoing>
    </exclusiveGateway>
    <task id="Task_2" name="Process Approved"/>
    <task id="Task_3" name="Send Rejection"/>
    <endEvent id="EndEvent_1" name="End"/>

    <sequenceFlow id="Flow_1" sourceRef="StartEvent_1" targetRef="Task_1"/>
    <sequenceFlow id="Flow_2" sourceRef="Task_1" targetRef="Gateway_1"/>
    <sequenceFlow id="Flow_3" sourceRef="Gateway_1" targetRef="Task_2" name="Yes"/>
    <sequenceFlow id="Flow_4" sourceRef="Gateway_1" targetRef="Task_3" name="No"/>
    <sequenceFlow id="Flow_5" sourceRef="Task_2" targetRef="EndEvent_1"/>
    <sequenceFlow id="Flow_6" sourceRef="Task_3" targetRef="EndEvent_1"/>
  </process>
</definitions>
```

---

## JWT トークン管理

### 概要

高度な JWT トークン管理により、セキュアなトークン生成、リフレッシュ、検証、失効をサポートします。

### インターフェース

```csharp
public interface IJwtTokenManager
{
    Task<TokenResponse> GenerateTokenAsync(TokenRequest request);
    Task<TokenResponse?> RefreshTokenAsync(string refreshToken);
    Task<bool> ValidateTokenAsync(string token);
    Task<bool> RevokeTokenAsync(string token);
    Task<IEnumerable<Claim>?> GetTokenClaimsAsync(string token);
}
```

### トークン生成

```csharp
var tokenManager = services.GetRequiredService<IJwtTokenManager>();

var response = await tokenManager.GenerateTokenAsync(new TokenRequest
{
    Subject = "user-123",
    Scopes = new List<string>
    {
        "workflows:read",
        "workflows:execute",
        "jobs:manage"
    },
    TokenLifetime = TimeSpan.FromHours(1),
    AdditionalClaims = new Dictionary<string, object>
    {
        { "email", "user@example.com" },
        { "department", "engineering" }
    }
});

// response.AccessToken - JWT トークン
// response.RefreshToken - リフレッシュトークン
// response.ExpiresIn - 有効期限（秒）
```

### トークン検証

```csharp
var isValid = await tokenManager.ValidateTokenAsync(token);
```

### トークン失効

```csharp
await tokenManager.RevokeTokenAsync(token);
// 失効後はValidateTokenAsync が false を返す
```

---

## リポジトリパターン

### 概要

ジェネリック リポジトリパターンを実装し、データアクセス層を抽象化しました。

### インターフェース

```csharp
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(string id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Func<T, bool> predicate);
    Task AddAsync(T entity);
    Task AddRangeAsync(IEnumerable<T> entities);
    Task UpdateAsync(T entity);
    Task RemoveAsync(T entity);
    Task RemoveRangeAsync(IEnumerable<T> entities);
    Task<bool> AnyAsync(Func<T, bool> predicate);
    Task<int> CountAsync(Func<T, bool>? predicate = null);
    Task<int> SaveChangesAsync();
}
```

### ワークフロー リポジトリ

```csharp
public interface IWorkflowRepository : IRepository<WorkflowEntity>
{
    Task<IEnumerable<WorkflowEntity>> GetByNameAsync(string name);
    Task<IEnumerable<WorkflowEntity>> GetActiveAsync();
}
```

### 実行履歴 リポジトリ

```csharp
public interface IExecutionHistoryRepository : IRepository<ExecutionHistoryEntity>
{
    Task<IEnumerable<ExecutionHistoryEntity>> GetByWorkflowIdAsync(string workflowId);
    Task<IEnumerable<ExecutionHistoryEntity>> GetRecentAsync(int limit = 100);
    Task<IEnumerable<ExecutionHistoryEntity>> GetFailedAsync();
}
```

### 使用例

```csharp
// 依存性注入
services.AddScoped<IWorkflowRepository, InMemoryWorkflowRepository>();

// 使用
public class WorkflowService
{
    private readonly IWorkflowRepository _workflowRepository;

    public WorkflowService(IWorkflowRepository workflowRepository)
    {
        _workflowRepository = workflowRepository;
    }

    public async Task<WorkflowEntity?> GetWorkflowAsync(string id)
    {
        return await _workflowRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<WorkflowEntity>> GetActiveWorkflowsAsync()
    {
        return await _workflowRepository.GetActiveAsync();
    }
}
```

---

## ヘルスチェック

### 概要

包括的なヘルスチェック機能により、システムの状態を監視します。

### チェック項目

1. **システムヘルスチェック**
   - メモリ使用量
   - CPU 使用時間
   - スレッド数
   - ガベージコレクション統計
   - アップタイム

2. **データベースヘルスチェック**
   - 接続状態
   - 応答時間

3. **依存関係ヘルスチェック**
   - OpenTelemetry エンドポイント
   - 外部サービス

4. **ディスク容量チェック**
   - 利用可能容量
   - 使用率

### エンドポイント

#### 基本的なヘルスチェック

```bash
GET /health
```

#### 詳細なヘルスチェック

```bash
GET /health/detailed

Response:
{
  "status": "Healthy",
  "timestamp": "2025-11-04T10:00:00Z",
  "checks": [
    {
      "name": "loco-health",
      "status": "Healthy",
      "description": "System is healthy",
      "duration": "00:00:00.0234",
      "data": {
        "memory_mb": 125.34,
        "thread_count": 24,
        "uptime_seconds": 3600
      }
    },
    ...
  ]
}
```

---

## API リファレンス

### 認証

すべての API エンドポイント（`/health` を除く）は JWT 認証が必要です。

```bash
Authorization: Bearer {jwt_token}
```

### レート制限

グローバルレート制限:
- **制限**: ユーザーあたり 1000 リクエスト/分
- **エラーコード**: 429 Too Many Requests

ポリシー別制限:
- `strict`: 10 リクエスト/分
- `moderate`: 100 リクエスト/分

### エラーレスポンス

```json
{
  "error": "Error message",
  "code": "ERROR_CODE",
  "details": "Detailed error information"
}
```

### ステータスコード

- `200 OK`: リクエスト成功
- `201 Created`: リソース作成成功
- `204 No Content`: リクエスト成功（コンテンツなし）
- `400 Bad Request`: 無効なリクエスト
- `401 Unauthorized`: 認証失敗
- `403 Forbidden`: 権限がない
- `404 Not Found`: リソースが見つからない
- `429 Too Many Requests`: レート制限超過
- `500 Internal Server Error`: サーバーエラー

---

## ベストプラクティス

### セキュリティ

1. **JWT トークン**
   - 常に HTTPS を使用
   - トークン有効期限を短く設定
   - 定期的にリフレッシュトークンを更新

2. **レート制限**
   - ブルートフォース攻撃を防止
   - ユーザー ID で識別

3. **入力検証**
   - すべてのリクエストを検証
   - SQL インジェクション対策

### パフォーマンス

1. **ジョブスケジューリング**
   - 長時間実行タスクを非同期化
   - 適切なリトライポリシー

2. **BPMN ワークフロー**
   - 複雑なロジックは小さなタスクに分割
   - ゲートウェイで条件分岐

3. **キャッシング**
   - ワークフロー定義をキャッシュ
   - トークン検証結果をキャッシュ

### 監視

1. **ロギング**
   - すべての重要なイベントをログ
   - エラースタックトレースを記録

2. **メトリクス**
   - ワークフロー実行数
   - 平均実行時間
   - エラー率

3. **トレース**
   - リクエスト全体フロー
   - 外部サービス呼び出し

---

## 参考資料

### OpenTelemetry
- [OpenTelemetry 公式ドキュメント](https://opentelemetry.io/)
- [.NET での OpenTelemetry](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel)
- [Observing .NET microservices with OpenTelemetry](https://blog.codingmilitia.com/2023/09/05/observing-dotnet-microservices-with-opentelemetry-logs-traces-and-metrics/)

### Hangfire
- [Hangfire 公式サイト](https://www.hangfire.io/)
- [Hangfire ドキュメント](https://docs.hangfire.io/)
- [Mastering Hangfire and Enhancing .NET Background Jobs](https://www.codecrafting.tips/code-chronicles-27-mastering-hangfire-and-enhancing-net-background-jobs)

### BPMN
- [BPMN 2.0 仕様](https://www.omg.org/spec/BPMN/2.0/)
- [BPMN ワークフロー入門](https://medium.com/@justintilson/workflows-bpmn-32a05c1f757a)
- [Workflows & BPMN: An Introductory Primer](https://www.cmwlab.com/bpmn/)

### .NET ベストプラクティス
- [What's new in .NET 9](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/overview)
- [Minimal APIs in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview)
- [Repository Pattern in C# .NET](https://medium.com/@chandrashekharsingh25/understanding-the-repository-pattern-in-c-net-with-examples-51f02c4074ba)

---

## サポート

問題や質問がある場合は、GitHub Issues で報告してください。

---

**最終更新**: 2025-11-04
