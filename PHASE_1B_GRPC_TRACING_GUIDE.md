# Phase 1B: gRPC + 分散トレーシング実装ガイド

## 📊 概要（韓国語コミュニティの推奨）

gRPC と分散トレーシング により：
- **通信性能**: REST 比 40% 高速化
- **帯域幅**: 50% 削減（Protocol Buffers）
- **可視性**: 完全な分散トレーシング
- **スケーラビリティ**: マイクロサービス対応

## 🎯 実装戦略

### Step 1: gRPC サービス定義

```proto
syntax = "proto3";

package loco.workflow;

service WorkflowEngine {
  rpc ExecuteWorkflow (ExecuteRequest) returns (ExecuteResponse);
  rpc GetExecutionStatus (StatusRequest) returns (StatusResponse);
  rpc StreamExecutionLogs (LogRequest) returns (stream LogEntry);
}

message ExecuteRequest {
  string workflow_id = 1;
  bytes input = 2;
}

message ExecuteResponse {
  string execution_id = 1;
  bytes output = 2;
  string status = 3;
}

message StatusRequest {
  string execution_id = 1;
}

message StatusResponse {
  string execution_id = 1;
  string status = 2;
  float progress = 3;
}

message LogRequest {
  string execution_id = 1;
}

message LogEntry {
  string timestamp = 1;
  string level = 2;
  string message = 3;
}
```

### Step 2: gRPC サービス実装

```csharp
public class WorkflowEngineService : WorkflowEngine.WorkflowEngineBase
{
    private readonly IWorkflowExecutor _executor;
    private readonly ILogger<WorkflowEngineService> _logger;

    public override async Task<ExecuteResponse> ExecuteWorkflow(
        ExecuteRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("Executing workflow: {WorkflowId}", request.WorkflowId);

        var result = await _executor.ExecuteAsync(request.WorkflowId, request.Input);

        return new ExecuteResponse
        {
            ExecutionId = result.ExecutionId,
            Output = result.Output,
            Status = result.Status.ToString()
        };
    }

    public override async Task StreamExecutionLogs(
        LogRequest request,
        IAsyncStreamWriter<LogEntry> responseStream,
        ServerCallContext context)
    {
        var logs = _executor.StreamLogsAsync(request.ExecutionId, context.CancellationToken);

        await foreach (var log in logs)
        {
            await responseStream.WriteAsync(new LogEntry
            {
                Timestamp = log.Timestamp.ToString("O"),
                Level = log.Level,
                Message = log.Message
            });
        }
    }
}
```

### Step 3: Jaeger 分散トレーシング統合

```csharp
// Program.cs
var jaegerExporter = new JaegerExporter(new JaegerExporterOptions
{
    AgentHost = Environment.GetEnvironmentVariable("JAEGER_AGENT_HOST") ?? "localhost",
    AgentPort = int.Parse(
        Environment.GetEnvironmentVariable("JAEGER_AGENT_PORT") ?? "6831")
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracingBuilder => tracingBuilder
        .AddSource("Loco.Workflow")
        .AddGrpcClientInstrumentation()
        .AddGrpcCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddJaegerExporter())
    .WithMetrics(metricsBuilder => metricsBuilder
        .AddMeter("Loco.Metrics")
        .AddProcessInstrumentation()
        .AddRuntimeInstrumentation()
        .AddJaegerExporter());

// gRPC サービス登録
builder.Services.AddGrpc();
var app = builder.Build();
app.MapGrpcService<WorkflowEngineService>();
```

### Step 4: gRPC クライアント実装

```csharp
public class WorkflowGrpcClient
{
    private readonly WorkflowEngine.WorkflowEngineClient _client;

    public WorkflowGrpcClient(string address)
    {
        var channel = GrpcChannel.ForAddress(address);
        _client = new WorkflowEngine.WorkflowEngineClient(channel);
    }

    public async Task<string> ExecuteAsync(string workflowId, byte[] input)
    {
        var response = await _client.ExecuteWorkflowAsync(new ExecuteRequest
        {
            WorkflowId = workflowId,
            Input = ByteString.CopyFrom(input)
        });

        return response.ExecutionId;
    }

    public async IAsyncEnumerable<string> StreamLogsAsync(string executionId)
    {
        using var call = _client.StreamExecutionLogs(
            new LogRequest { ExecutionId = executionId });

        await foreach (var log in call.ResponseStream.ReadAllAsync())
        {
            yield return $"[{log.Level}] {log.Message}";
        }
    }
}
```

## 📊 期待される改善効果

| 項目 | 向上度 |
|-----|--------|
| **通信レイテンシー** | 40% 削減 |
| **帯域幅** | 50% 削減 |
| **スループット** | 60% 向上 |
| **可視性** | 100% 完全トレーシング |

## 📋 環境変数設定

```bash
# Jaeger 設定
export JAEGER_AGENT_HOST=localhost
export JAEGER_AGENT_PORT=6831

# オプション: Jaeger UI へのアクセス
# http://localhost:16686
```

## ✅ チェックリスト

- [ ] Proto ファイル定義
- [ ] gRPC サービス実装
- [ ] gRPC クライアント実装
- [ ] Jaeger 統合設定
- [ ] トレーシング動作確認
- [ ] ベンチマーク測定

## 🚀 次のステップ

1. **gRPC プロトコル確認** (1-2時間)
2. **プロトタイプ実装** (2-3日)
3. **Jaeger 統合** (1-2日)
4. **パフォーマンス検証** (1日)

---

**実装見積もり**: 3-4日
**影響度**: ⭐⭐⭐ (通信40%高速化 + 完全可視化)
