# Loco プロジェクト - 実装ガイド

本ドキュメントは、`IMPROVEMENT_ANALYSIS.md` で提案された改善点を実装するための詳細なガイドです。

---

## Phase 1: クイックウイン（実装済み・進行中）

### ✅ 1.1 Dynamic PGO 設定

**状態**: ✅ **実装完了**

**実装内容**:
- `Loco.Api.csproj` に `<TieredPGO>true</TieredPGO>` を追加
- `Loco.Core.csproj` に `<TieredPGO>true</TieredPGO>` を追加
- `<ReadyToRunComposite>true</ReadyToRunComposite>` と `<PublishReadyToRun>true</PublishReadyToRun>` を追加

**期待効果**:
- JIT コンパイル時間 30-40% 短縮
- ランタイムパフォーマンス 15-20% 向上
- 初期起動時間 20-30% 短縮

**検証方法**:
```bash
dotnet publish -c Release
# イメージサイズと起動時間を測定
```

---

## Phase 2: パフォーマンス最適化（2-3週間）

### 📋 2.1 EF Core + Dapper ハイブリッド実装

**ファイル**: `src/Loco.Core/DataAccess/WorkflowDataAccessService.cs`

```csharp
using System.Data;
using Microsoft.EntityFrameworkCore;
using Dapper;

namespace Loco.Core.DataAccess;

public class WorkflowDataAccessService
{
    private readonly LocoDbContext _dbContext;
    private readonly IDbConnection _dbConnection;

    // Write Operations: EF Core
    // Read Operations: Dapper (Performance Critical)
}
```

---

## Phase 3: アーキテクチャ改善（3-4週間）

### 📋 3.1 Durable Execution パターン実装

**期待効果**:
- 失敗時の自動リカバリー
- 完全な実行監査ログ
- 実行の再現可能性

---

## 実装優先順序

| 優先度 | 改善点 | 実装時間 | 影響度 |
|--------|--------|---------|--------|
| ⭐⭐⭐ | Dynamic PGO 設定 | 1日 | ⭐⭐⭐ |
| ⭐⭐⭐ | EF Core + Dapper | 3-5日 | ⭐⭐⭐ |
| ⭐⭐⭐ | Durable Execution | 5-7日 | ⭐⭐⭐ |
| ⭐⭐ | FrozenDictionary | 2-3日 | ⭐⭐ |
| ⭐⭐ | Span<T> 活用 | 2日 | ⭐⭐ |

**最終更新**: 2024年11月21日
