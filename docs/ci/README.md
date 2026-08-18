# CI — 要手動適用

`docs/ci/ci.yml` は統合済みの CI 定義ですが、**自動でコミットできませんでした**。
このセッションの GitHub App に `workflows` 権限が無く、push が拒否されるためです:

```
! [remote rejected] refusing to allow a GitHub App to create or update
  workflow `.github/workflows/ci.yml` without `workflows` permission
```

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

問題は 2 つあります:

1. **6 つが同じ push で同時に走る**（重複実行）。
2. **7 つすべてが .NET 専用**。フロントエンドの **400 テストが CI に一切乗っていない** —
   このプロダクトで唯一検証済みの部分が自動化されていない、という逆転が起きています。

統合版は 2 ジョブ構成です:

- **frontend**: `npm ci` → `tsc --noEmit` → `vitest` → `build` → `lint`。
  バックエンドに依存しないので、.NET がビルドできなくても緑を維持できます。
- **backend**: `restore` → `build` → `test`。
  **当面は失敗する想定**です。バックエンドは NuGet 到達不能な環境で書かれたため
  一度もコンパイルされておらず、該当コミットには全て VERIFICATION CAVEAT が付いています。
  **このジョブが初めて緑になった時点で、それらの但し書きを外せます。**

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
