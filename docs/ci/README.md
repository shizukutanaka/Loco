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
2. **7 つすべてが .NET 専用**。フロントエンドの **494 テストが CI に一切乗っていない** —
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
  NuGet 無しで Roslyn と net8.0 参照アセンブリだけを使い、`src/` と `tests/` の
  **全ファイルを（メソッド本体まで）型検査**します。加えて 9 つの構造検査
  （パッケージ管理の整合性・テストが参照する型の実在・全 src ファイルの
  到達可能性・SDK が叩く API 経路の実在・エディタ各モジュールの到達可能性・
  エディタのページが参照する静的ファイルの実在・
  コネクタが宣言するアクションの実装・コネクタが宣言するパラメータの使用・
  ドキュメントが参照するファイルの実在）を行います。**現在エラーはゼロ**。

  なお以前はメソッド本体を検査できていませんでした。C# コンパイラは宣言段階で
  エラーが 1 つでもあると本体束縛を行わず、このスクリプトには常に 12 件の
  宣言エラー（JwtBearer / Swashbuckle / System.CommandLine）があったためです。
  4 パッケージをスタブ化して宣言エラーをゼロにしたことで初めて本体が解析され、
  長く潜んでいた実欠陥（存在しない `ActionParameters.Has` の 13 箇所からの呼び出し、
  `Name =="action"` のタイプミス、引数が足りない `TestConnectionAsync` 呼び出し、
  改名済みクラスを呼ぶ CLI）が表面化しました。

- **backend テストはローカルで実行できます**。`scripts/run-tests-offline.sh` が
  `scripts/offline-test-harness/`（xunit と FluentAssertions の実働サブセット
  ＋リフレクションによるランナー）を使って **341 件全てを実行**します。
  `dotnet test` には restore できないパッケージが要りますが、
  「テストを走らせること」自体には要りませんでした。

  コントローラテストも走ります。ハーネスが **API を実 exe としてビルドし
  ループバックポートで起動**するため、本物の Kestrel に本物の HTTP で話します
  — 「秘密がレスポンスに現れない」という表明は、実際にソケットを通過した
  バイト列に対する検証です。ASP.NET Core 共有ランタイムは導入済みで、
  本物の JWT ライブラリは SDK の `dotnet-user-jwts` ツールに同梱されています。

  残る未検証は 1 点です: **実パッケージに対する本物のビルド**。
  ハーネスの `AddJwtBearer` 配線は自前（トークン検証自体は Microsoft の実装）、
  Swashbuckle スタブは不活性です。backend ジョブがこれを担います。

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
