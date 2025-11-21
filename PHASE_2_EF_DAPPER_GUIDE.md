# Phase 2: EF Core + Dapper ハイブリッド実装ガイド

## 📊 概要

EF Core と Dapper を組み合わせることで：
- **読み取り性能**: 5-10倍高速化
- **メモリ効率**: 30-40% 削減
- **開発効率**: 両者のメリットを活用

## 🎯 実装戦略

### Write Operations: EF Core
```csharp
// ACID 保証が必要な操作
public async Task CreateWorkflowAsync(Workflow workflow)
{
    _dbContext.Workflows.Add(workflow);
    await _dbContext.SaveChangesAsync();
}
```

### Read Operations: Dapper
```csharp
// 高速なクエリが必要な操作
public async Task<List<WorkflowSummary>> GetWorkflowsAsync()
{
    const string sql = @"
        SELECT Id, Name, Status, CreatedAt
        FROM Workflows
        WHERE Status = @Status
        ORDER BY CreatedAt DESC
        LIMIT 100";

    return (await connection.QueryAsync<WorkflowSummary>(
        sql,
        new { Status = "Active" })).ToList();
}
```

## 📋 実装ステップ

### Step 1: Dapper NuGet パッケージ追加
```bash
dotnet add src/Loco.Core package Dapper
```

### Step 2: IDbConnection の登録
```csharp
// Program.cs
var connectionString = "Data Source=loco.db";

builder.Services.AddDbContext<LocoDbContext>(options =>
    options.UseSqlite(connectionString));

// Dapper 用に IDbConnection を登録
builder.Services.AddScoped<IDbConnection>(sp =>
    new SqliteConnection(connectionString));
```

### Step 3: ハイブリッドリポジトリの実装
```csharp
public class WorkflowHybridRepository
{
    private readonly LocoDbContext _dbContext;
    private readonly IDbConnection _dbConnection;

    // Write: EF Core
    public async Task SaveAsync(Workflow workflow)
    {
        _dbContext.Workflows.Add(workflow);
        await _dbContext.SaveChangesAsync();
    }

    // Read: Dapper
    public async Task<List<Workflow>> GetActiveAsync()
    {
        const string sql = @"
            SELECT * FROM Workflows
            WHERE Status = 'Active'
            ORDER BY CreatedAt DESC";

        return (await _dbConnection.QueryAsync<Workflow>(sql))
            .ToList();
    }
}
```

## 🚀 期待される改善効果

| 操作 | 改善度 |
|-----|--------|
| 読み取り (単件) | 2-3倍 |
| 読み取り (複数件) | 5-10倍 |
| ダッシュボード | 50%+ 高速化 |
| メモリ | 30-40% 削減 |

## ✅ チェックリスト

- [ ] Dapper NuGet 追加
- [ ] IDbConnection 登録
- [ ] ハイブリッドリポジトリ実装
- [ ] ユニットテスト作成
- [ ] ベンチマーク測定
- [ ] 本番デプロイ

## 📚 参考資料

- [Dapper GitHub](https://github.com/DapperLib/Dapper)
- [EF Core vs Dapper Benchmark](https://github.com/dotnet/EntityFramework.Docs)

## 💡 ベストプラクティス

1. **単純な読み取り**: Dapper 使用
2. **複雑なクエリ**: Dapper + raw SQL
3. **トランザクション**: EF Core 使用
4. **大量読み取り**: Dapper + ページング

---

**実装見積もり**: 3-5日
**影響度**: ⭐⭐⭐ (5-10倍性能改善)
