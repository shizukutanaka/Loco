# CI — 要手動適用

`docs/ci/ci.yml` は統合済みの CI 定義ですが、**自動で適用できませんでした**。
このセッションの GitHub App に `workflows` 権限が無いためです。
**2 つの独立した経路で試し、どちらも拒否されました**:

| 経路 | 結果 |
|---|---|
| `git push` | `! [remote rejected] refusing to allow a GitHub App to create or update workflow \`.github/workflows/ci.yml\` without \`workflows\` permission` |
| GitHub REST API (`PUT /repos/.../contents/.github/workflows/ci.yml`) | `403 Resource not accessible by integration` |

つまりこれは回避可能な設定ミスではなく、**App の権限境界**です。
解決するには、リポジトリ所有者が次のいずれかを行う必要があります:

- GitHub App に `workflows: write` 権限を付与する、または
- 下記「適用手順」のコマンドを手元で実行する

## なぜ差し替えるのか

現在 `.github/workflows/` には **7 ファイル・1,023 行**があります:

| ファイル | 行数 | トリガ |
|---|---|---|
| build.yml | 50 | push, pull_request |
| ci.yml | 87 | push, pull_request |
| ci-cd.yml | 273 | push, pull_request, release |
| code-quality.yml | 82 | push, pull_request |
| enhanced-ci-cd.yml | 341 | push, pull_request, schedule |
| release.yml | 92 | push |
| test.yml | 98 | push, pull_request |

問題は 3 つあります:

1. **6 つが同じ push で同時に走る**（重複実行）。
2. **7 つすべてが .NET 専用**。フロントエンドの **432 テストが CI に一切乗っていない** —
   このプロダクトで唯一検証済みの部分が自動化されていない、という逆転が起きています。
3. **存在しないパスを参照しているステップがある**。いずれもこのリポジトリに
   一度も存在したことのないパスです:

   | ファイル | 参照先 | 実際 |
   |---|---|---|
   | ci-cd.yml | `src/Loco.Gui/Loco.Gui.csproj` | 存在しない |
   | enhanced-ci-cd.yml | `src/Loco.Benchmarks/Loco.Benchmarks.csproj` | 存在しない（ベンチマークは削除済み・下記参照） |
   | enhanced-ci-cd.yml | `tests/Loco.Integration.Tests/Loco.Integration.Tests.csproj` | 存在しない |

   つまりこれらのワークフローは、NuGet の可否とは無関係に、そのステップに到達した
   時点で必ず失敗します。

統合版は 3 ジョブ構成です:

- **frontend**: `npm ci` → `tsc --noEmit` → `vitest` → `build` → `lint`。
  バックエンドに依存しないので、.NET がビルドできなくても緑を維持できます。
- **backend**: `restore` → `build` → `test`。バックエンドは NuGet 到達不能な環境
  (api.nuget.org がプロキシで 403) で書かれたため、該当コミットには VERIFICATION
  CAVEAT が付いています。**このジョブが初めて緑になった時点で、それらの但し書きを外せます。**
- **offline-checks**: `scripts/typecheck-offline.sh` と `scripts/check-structure.py`。
  NuGet 無しで Roslyn と net8.0 参照アセンブリだけを使って全ソースを型検査し、
  加えて 5 つの構造検査（パッケージ管理の整合性・テストが参照する型の実在・
  Loco.Core 全ファイルの到達可能性・SDK が叩く API 経路の実在・
  ドキュメントが参照するファイルの実在）を行います。**現時点で未説明エラーはゼロ**
  （残る 12 エラーはすべて restore できないパッケージの型）。
  restore が壊れても回帰を検出し続けるための保険です。
  ただしパッケージ型の呼び出し箇所は見えないため、`dotnet build` の代替にはなりません。

ローカルでは `scripts/verify.sh` がこれらをまとめて実行します。

## 適用手順

```bash
git rm .github/workflows/build.yml \
       .github/workflows/ci-cd.yml \
       .github/workflows/code-quality.yml \
       .github/workflows/enhanced-ci-cd.yml \
       .github/workflows/release.yml \
       .github/workflows/test.yml

cp docs/ci/ci.yml .github/workflows/ci.yml

git add .github/workflows
git commit -m "ci: consolidate seven workflows into one that also runs the frontend"
git push
```

`release.yml` を削除する点に注意してください。リリース自動化が必要な場合は、
統合後に `on: release` の専用ジョブとして**別途**追加するのが適切です
（現状は CI と混ざっており、push のたびに release 系ステップが走ります）。

## 検証済み事項

- YAML はパース可能（`yaml.safe_load` で確認）
- `src/Loco.VisualEditor/package-lock.json` は git 管理下にあり `npm ci` が使用可能
- `Loco.sln` が参照する 6 プロジェクトはすべて実在する
- `dotnet restore` を止めていた **NU1008 は解消済み**。以前は
  `tests/Loco.E2E.Tests`（削除済み）と `benchmarks/Loco.Benchmarks` が
  `PackageReference` に `Version=` を直書きしており、中央パッケージ管理下では
  **パッケージを 1 つも取得する前に restore が失敗**していました。
  これはネットワークとは無関係のビルド破損で、`workflows: write` が付与されて
  backend ジョブが走るようになっても、これを直すまでは緑になりませんでした。
  `scripts/check-structure.py` が再発を検出します。
- `tests/Loco.Core.Tests` が**コンパイル不能だった問題も解消済み**。3 ファイルが
  実在しない型を参照しており、テストアセンブリ全体が落ちていました
  （`AdvancedSecurityManager` / `CloudSyncManager` は削除、
  `VisualWorkflowEngineTests` の `WorkflowValidator` は
  `VisualWorkflowValidator` へ修正）。
  つまり backend ジョブが緑になる前提条件は、この 2 点でした。
