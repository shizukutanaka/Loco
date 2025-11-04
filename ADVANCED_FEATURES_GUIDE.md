# Loco - Advanced Distributed Systems & Microservices Guide

YouTube と WEB の最新情報に基づいて実装された、Loco プロジェクトの高度な機能ガイドです。

## 目次

1. [Saga パターン - 分散トランザクション](#saga-パターン)
2. [分散キャッシング](#分散キャッシング)
3. [マイクロサービス通信](#マイクロサービス通信)
4. [監査ログシステム](#監査ログシステム)
5. [レジリエンスパターン](#レジリエンスパターン)
6. [フィーチャーフラグ](#フィーチャーフラグ)

---

## Saga パターン

### 概要

Saga パターンは、マイクロサービス間の分散トランザクションを管理するための設計パターンです。

**参考**: [Mastering the Saga Pattern in .NET 8](https://itnext.io/mastering-the-saga-pattern-in-net-8-orchestrate-resilient-microservices-without-2pc-headaches-1e41d82c2ae5)

### 実装

```csharp
public interface ISagaOrchestrator
{
    Task<SagaExecutionResult> ExecuteAsync(
        ISagaDefinition definition,
        Dictionary<string, object?> initialData,
        CancellationToken cancellationToken = default);
}
```

### 使用例

```csharp
// Saga ステップの定義
public class ProcessPaymentStep : SagaStepBase
{
    public override string Name => "ProcessPayment";
    public override string Description => "支払い処理";

    public override async Task<SagaStepResult> ExecuteAsync(
        SagaContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var amount = (decimal)context.Data["amount"]!;

            // 支払い処理
            var result = await ProcessPaymentAsync(amount, cancellationToken);

            return new SagaStepResult
            {
                Success = true,
                Output = new Dictionary<string, object?> { { "transactionId", result } }
            };
        }
        catch (Exception ex)
        {
            return new SagaStepResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ShouldRetry = true
            };
        }
    }

    public override async Task<bool> CompensateAsync(
        SagaContext context,
        CancellationToken cancellationToken)
    {
        // 支払いを返金
        var transactionId = (string)context.StepResults["ProcessPayment"].Output["transactionId"]!;
        return await RefundPaymentAsync(transactionId, cancellationToken);
    }
}

// Saga 実行
var orchestrator = services.GetRequiredService<ISagaOrchestrator>();
var result = await orchestrator.ExecuteAsync(
    sagaDefinition,
    new Dictionary<string, object?> { { "amount", 100.00m } });

if (result.Success)
{
    Console.WriteLine("Saga completed successfully");
}
else if (result.CompensationPerformed)
{
    Console.WriteLine("Saga failed and was compensated");
}
```

### 特徴

- ✅ ステップの自動リトライ（指数バックオフ）
- ✅ タイムアウト管理
- ✅ 失敗時の自動コンペンセーション
- ✅ 実行履歴トラッキング
- ✅ メトリクス収集

---

## 分散キャッシング

### 概要

Redis を使用した分散キャッシング層により、複数のサーバーインスタンス間でキャッシュを共有します。

**参考**: [Distributed Caching in ASP.NET Core with Redis](https://codewithmukesh.com/blog/distributed-caching-in-aspnet-core-with-redis/)

### インターフェース

```csharp
public interface IDistributedCacheService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
    Task RemoveAsync(string key);
    Task<bool> ExistsAsync(string key);
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class;
}
```

### 使用例

```csharp
// キャッシュの設定
var cacheOptions = new CacheOptions
{
    ConnectionString = "localhost:6379",
    DefaultExpiration = TimeSpan.FromHours(1),
    EnableCompression = true,
    KeyPrefix = "loco:"
};

services.AddScoped<IDistributedCacheService>(
    sp => new DistributedCacheService(
        sp.GetRequiredService<IDistributedCache>(),
        cacheOptions,
        sp.GetRequiredService<ILogger<DistributedCacheService>>()));

// 使用
var cacheService = services.GetRequiredService<IDistributedCacheService>();

// キャッシュから取得、なければ作成
var workflow = await cacheService.GetOrCreateAsync<Workflow>(
    $"workflow:{workflowId}",
    async () => await GetWorkflowFromDatabaseAsync(workflowId),
    TimeSpan.FromHours(1));

// 複数の値を一度に取得
var workflows = await cacheService.GetManyAsync<Workflow>(
    "workflow:1", "workflow:2", "workflow:3");

// キャッシュ統計
var stats = await cacheService.GetStatisticsAsync();
Console.WriteLine($"Hit Rate: {stats.HitRate}%");
```

### ベストプラクティス

1. **キー設計**: 常にプレフィックスを使用
2. **有効期限**: TTL を設定してメモリを節約
3. **エラーハンドリング**: キャッシュミス時のフォールバック
4. **圧縮**: 大きなオブジェクトは圧縮

---

## マイクロサービス通信

### 概要

サービス発見と HTTP ベースの通信を実現するマイクロサービスクライアント。

**参考**: [Microservices Architecture with .NET Core and Kubernetes](https://medium.com/@paulotorres/microservices-architecture-with-net-core-and-kubernetes-999a02e43334)

### サービス登録

```csharp
var serviceRegistry = services.GetRequiredService<IServiceRegistry>();

// マイクロサービスを登録
await serviceRegistry.RegisterServiceAsync(new ServiceInstance
{
    ServiceName = "order-service",
    ServiceId = "order-service-1",
    Host = "order-service.local",
    Port = 8080,
    Protocol = ServiceProtocol.Http,
    HealthCheckUrl = "/health",
    Version = "1.0.0"
});

// サービスを発見
var instances = await serviceRegistry.DiscoverServiceAsync("order-service");
foreach (var instance in instances)
{
    Console.WriteLine($"Found: {instance.BaseUrl}");
}
```

### マイクロサービスクライアント

```csharp
var client = services.GetRequiredService<IMicroserviceClient>();

// 別のサービスから API を呼び出し
var order = await client.GetAsync<Order>(
    "order-service",
    "/api/orders/123");

var result = await client.PostAsync<OrderResponse>(
    "order-service",
    "/api/orders",
    new { customerId = 1, items = new[] { 1, 2, 3 } });
```

### 特徴

- ✅ 自動サービス発見
- ✅ ヘルスチェック
- ✅ 負荷分散対応
- ✅ リアクティブイベント通知

---

## 監査ログシステム

### 概要

GDPR/HIPAA/SOC2 準拠の監査ログシステム。

**参考**: [Advanced Audit Logging in .NET 9.0](https://medium.com/asp-dotnet/advanced-audit-logging-in-net-9-0-step-by-step-middleware-setup-and-its-implementation-d400f848ac11)

### 使用例

```csharp
// 監査ロギングを有効化
services.AddAuditLogging();
app.UseAuditLogging();

// 手動で監査イベントを記録
var auditLogger = services.GetRequiredService<IAuditLogger>();

await auditLogger.LogEventAsync(new AuditEvent
{
    UserId = "user-123",
    UserName = "john.doe",
    Action = AuditActionType.Update,
    EntityType = "Workflow",
    EntityId = "workflow-456",
    OldValues = new Dictionary<string, object?> { { "Status", "Draft" } },
    NewValues = new Dictionary<string, object?> { { "Status", "Published" } },
    IpAddress = "192.168.1.1",
    UserAgent = "Mozilla/5.0...",
    CorrelationId = "correlation-123",
    Success = true,
    Classification = DataClassification.Confidential
});

// 監査証跡を取得
var auditTrail = await auditLogger.GetAuditTrailAsync("Workflow", "workflow-456");

// ユーザーの操作履歴
var userActions = await auditLogger.GetUserAuditTrailAsync("user-123", limit: 50);

// 統計
var stats = await auditLogger.GetStatisticsAsync(
    startDate: DateTime.UtcNow.AddDays(-30),
    endDate: DateTime.UtcNow);

Console.WriteLine($"Total Events: {stats.TotalEvents}");
Console.WriteLine($"Success Rate: {stats.SuccessRate}%");
Console.WriteLine($"Failed Operations: {stats.FailedOperations}");
```

### 自動監査

リクエスト（POST/PUT/DELETE）は自動的に監査されます。

```
POST /api/workflows → "Create" アクション記録
PUT  /api/workflows/123 → "Update" アクション記録
DELETE /api/workflows/123 → "Delete" アクション記録
```

---

## レジリエンスパターン

### 概要

Polly ライクなレジリエンスポリシーで障害に強いアプリケーションを構築。

**参考**: [Building Resilient Microservices with Polly's Circuit Breaker](https://atalupadhyay.wordpress.com/2025/03/09/building-resilient-microservices-with-pollys-circuit-breaker-in-net-core/)

### リトライポリシー

```csharp
var retryPolicy = new RetryPolicy(
    maxRetries: 3,
    initialDelay: TimeSpan.FromMilliseconds(100),
    logger: logger);

var result = await retryPolicy.ExecuteAsync(async () =>
{
    return await unreliableService.CallAsync();
});
```

### サーキットブレーカーパターン

```csharp
var circuitBreakerPolicy = new CircuitBreakerPolicy(
    failureThreshold: 5,
    timeout: TimeSpan.FromSeconds(60),
    logger: logger);

try
{
    await circuitBreakerPolicy.ExecuteAsync(async () =>
    {
        return await externalApi.GetDataAsync();
    });
}
catch (InvalidOperationException ex) when (ex.Message.Contains("Circuit breaker is open"))
{
    Console.WriteLine("Circuit breaker is open - service is down");
}
```

### 状態遷移

```
Closed (正常) → Open (障害) → HalfOpen (回復試行) → Closed
```

### メトリクス

```csharp
var metrics = retryPolicy.GetMetrics();
Console.WriteLine($"Total Executions: {metrics.TotalExecutions}");
Console.WriteLine($"Success Rate: {metrics.SuccessRate}%");
Console.WriteLine($"Retries: {metrics.Retries}");
Console.WriteLine($"Circuit Breaker Trips: {metrics.CircuitBreakerTrips}");
Console.WriteLine($"Avg Execution Time: {metrics.AverageExecutionTimeMs}ms");
```

---

## フィーチャーフラグ

### 概要

段階的なロールアウト、A/B テスト、フィーチャー管理を実現。

**参考**: [Feature Flags in .NET Core with LaunchDarkly](https://medium.com/@iamrks/feature-flags-in-net-core-with-launchdarkly-53f14450dfac)

### フィーチャーフラグの作成

```csharp
var featureToggleService = services.GetRequiredService<IFeatureToggleService>();

// シンプルなフィーチャーフラグ
var flag = await featureToggleService.CreateFeatureFlagAsync(new FeatureFlag
{
    Key = "new-dashboard",
    Name = "New Dashboard UI",
    Description = "新しいダッシュボードUI",
    Enabled = true,
    Type = FeatureFlagType.Boolean,
    Owner = "product@example.com",
    Tags = new List<string> { "ui", "frontend" }
});

// 段階的なロールアウト（30%のユーザー）
var gradualRollout = await featureToggleService.CreateFeatureFlagAsync(new FeatureFlag
{
    Key = "new-api-v2",
    Name = "New API v2",
    Enabled = true,
    Percentage = 30, // 30% のトラフィックに対して有効
    Status = FeatureStatus.Active
});

// A/B テスト用バリエーション
var abTestFlag = await featureToggleService.CreateFeatureFlagAsync(new FeatureFlag
{
    Key = "checkout-variant",
    Name = "Checkout Variant",
    Type = FeatureFlagType.String,
    Variations = new Dictionary<string, object?>
    {
        { "control", "original-checkout" },
        { "variant-a", "simplified-checkout" },
        { "variant-b", "express-checkout" }
    },
    DefaultVariation = "control",
    Rules = new List<FeatureFlagRule>
    {
        new FeatureFlagRule
        {
            Name = "Premium users",
            Operator = RuleOperator.In,
            Property = "userTier",
            Value = new[] { "premium", "enterprise" },
            ResultVariation = "variant-b",
            Priority = 1
        }
    }
});
```

### フィーチャーの評価

```csharp
// ユーザーコンテキストなしで評価
bool isEnabled = await featureToggleService.IsEnabledAsync("new-dashboard");

// ユーザーコンテキスト付きで評価
var context = new FeatureContext
{
    UserId = "user-123",
    UserGroups = new List<string> { "beta-testers", "employees" },
    OrganizationId = "org-456",
    Attributes = new Dictionary<string, object?>
    {
        { "userTier", "premium" },
        { "region", "us-east" },
        { "joinDate", new DateTime(2023, 1, 1) }
    }
};

bool enabledForUser = await featureToggleService.IsEnabledAsync("new-api-v2", context);

// フィーチャーフラグ詳細を取得
var flag = await featureToggleService.GetFeatureFlagAsync("new-dashboard");

// 全フィーチャーを取得
var allFlags = await featureToggleService.GetAllFeaturesAsync();

// 統計を取得
var metrics = await featureToggleService.GetMetricsAsync("new-api-v2");
Console.WriteLine($"Evaluation Count: {metrics.TotalEvaluations}");
Console.WriteLine($"Enabled Count: {metrics.EnabledCount}");
Console.WriteLine($"Enable Percentage: {metrics.EnablePercentage}%");
```

### ユースケース

1. **段階的ロールアウト**
   - 新機能を少数のユーザーから開始
   - 段階的にパーセンテージを増加

2. **A/B テスト**
   - 複数バリエーションをテスト
   - ユーザーグループごとに異なるバージョン提供

3. **フィーチャー切り替え**
   - 本番環境でフィーチャーを ON/OFF
   - コードデプロイなしで制御

4. **スケジュール実行**
   - 特定日時でフィーチャーを有効化
   - キャンペーンに合わせた機能公開

---

## ベストプラクティス

### マイクロサービス

1. **サービス分離**: 単一責任原則に従う
2. **非同期通信**: イベント駆動型を活用
3. **断路遮断**: サーキットブレーカーで障害を隔離
4. **監視**: 各サービスのメトリクスを監視

### 分散トランザクション

1. **Saga パターン**: 2PC は避ける
2. **イベントソーシング**: 状態変化を記録
3. **補償トランザクション**: ロールバック戦略を計画
4. **タイムアウト**: 必ず設定する

### キャッシング

1. **キー設計**: 明確なプレフィックスを使用
2. **有効期限**: 常に TTL を設定
3. **スタンピード対策**: GetOrCreateAsync を活用
4. **監視**: キャッシュヒット率を追跡

---

## 参考資料

### マイクロサービス
- [Microservices.io - Pattern Language](https://microservices.io/)
- [Microsoft - Microservices Pattern](https://learn.microsoft.com/en-us/azure/architecture/microservices/)

### Saga パターン
- [Saga Pattern - Azure](https://learn.microsoft.com/en-us/azure/architecture/patterns/saga)
- [Mastering Saga Pattern in .NET 8](https://itnext.io/mastering-the-saga-pattern-in-net-8)

### キャッシング
- [Redis - Distributed Caching](https://redis.io/glossary/distributed-caching/)
- [ASP.NET Core - Caching](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/)

### 監査ログ
- [GDPR - Audit Logging](https://gdpr-info.eu/)
- [Audit Logging Best Practices](https://log-locker.com/)

### レジリエンス
- [Polly - GitHub](https://github.com/App-vNext/Polly)
- [Circuit Breaker Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-circuit-breaker-pattern)

---

**最終更新**: 2025-11-04
**バージョン**: 0.3.0-advanced
**ステータス**: Production Ready
