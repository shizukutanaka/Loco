# Loco セキュリティガイド

## 概要

Locoは企業グレードのセキュリティ機能を備えた自動化プラットフォームです。このガイドでは、セキュリティ機能の設定と最適な使用方法について説明します。

---

## セキュリティ機能

### 1. コマンドホワイトリスト

**目的:** 実行可能なコマンドを制限し、任意コマンド実行を防止

**デフォルトの許可コマンド:**
- `cmd.exe`
- `powershell.exe`
- `dotnet.exe`
- `git.exe`
- `node.exe`
- `npm.exe`
- `python.exe`
- `robocopy.exe`
- `xcopy.exe`
- `tar.exe`
- `7z.exe`
- `curl.exe`
- `wget.exe`

**カスタム設定:**

1. ホワイトリスト設定ファイルを作成:
```json
[
  "cmd.exe",
  "powershell.exe",
  "custom-tool.exe"
]
```

2. プログラム起動時にロード:
```csharp
using Loco.Core.Security;

// アプリケーション起動時
CommandWhitelist.LoadFromFile("config/allowed-commands.json");
```

**使用例:**
```csharp
// コマンドの実行前チェック
if (CommandWhitelist.IsAllowed(commandPath))
{
    // 安全に実行
}
```

---

### 2. パストラバーサル対策

**実装内容:**
- `Path.GetFullPath()` による完全パス解決
- ベースディレクトリとの比較検証
- 許可ディレクトリのホワイトリスト

**安全なパス検証:**
```csharp
using Loco.Core.Security;

if (SecurityUtilities.IsPathSafe(userProvidedPath))
{
    // 安全に処理
}
```

**保護対象:**
- `..` を使用したディレクトリトラバーサル
- URLエンコーディングによるバイパス
- シンボリックリンク経由の攻撃

---

### 3. パスワードハッシュ

**仕様:**
- **アルゴリズム:** PBKDF2 (SHA-256)
- **反復回数:** 600,000 (OWASP 2024推奨)
- **ソルト:** 16バイト (ランダム生成)
- **ハッシュ長:** 32バイト

**使用方法:**
```csharp
using Loco.Core.Security;

// パスワードのハッシュ化
string hashedPassword = SecurityUtilities.HashPassword("user-password");

// パスワード検証
bool isValid = SecurityUtilities.VerifyPassword("user-password", hashedPassword);
```

---

### 4. コマンドインジェクション対策

**実装:**
- `ProcessStartInfo.ArgumentList` による安全な引数組み立て
- 文字列補間の排除と `SecurityUtilities.SanitizeInput` によるサニタイズ
- `AccessControlManager` を用いた実行ファイル・作業ディレクトリの検証
- コマンドホワイトリスト適用および `SecurityUtilities.RateLimiter` によるレート制御
- JSON 配列も受け付ける `argumentList` パラメーターでの構造化引数提供

**安全な実装例:**
```csharp
var startInfo = new ProcessStartInfo
{
    FileName = "cmd.exe"
};
startInfo.ArgumentList.Add("/c");
startInfo.ArgumentList.Add(userCommand); // 安全に分離
```

**危険な実装 (修正済み):**
```csharp
// ❌ 脆弱 - 使用禁止
Arguments = $"/c {userCommand}"  // インジェクション可能
```

---

### 5. ファイルアクセス制御

**設定:**
```json
{
  "AllowedPaths": [
    "C:\\Users\\{username}\\Documents",
    "C:\\Projects"
  ],
  "ForbiddenPaths": [
    "C:\\Windows\\System32",
    "C:\\Program Files"
  ],
  "MaxFileSizeBytes": 104857600
}
```

**動作:**
- 許可パス外へのアクセスをブロック
- 禁止パスへのアクセスを拒否
- ファイルサイズ制限の適用

---

## セキュリティベストプラクティス

### 1. 最小権限の原則

**推奨事項:**
- Locoを専用の低権限ユーザーで実行
- 必要最小限のディレクトリのみ許可
- 定期的な権限レビュー

### 2. 監査ログ

**有効化:**
```json
{
  "EnableAuditLogging": true,
  "LogDirectory": "C:\\Loco\\Logs"
}
```

**記録内容:**
- コマンド実行履歴
- ファイルアクセス
- セキュリティイベント

### 3. ネットワーク分離

**推奨構成:**
- 本番環境: インターネットアクセス制限
- ファイアウォール設定
- プロキシ経由の通信

### 4. 定期的なセキュリティ更新

**チェックリスト:**
- [ ] 依存パッケージの更新
- [ ] セキュリティパッチ適用
- [ ] ホワイトリストのレビュー
- [ ] 監査ログの確認

---

## セキュリティインシデント対応

### 1. 異常検知

**監視項目:**
- 異常なコマンド実行パターン
- 大量のファイルアクセス
- 認証失敗の増加

**対応:**
```bash
# ログの確認
loco logs view 100 --level error

# 実行中のプロセス確認
loco health --json
```

### 2. インシデント発生時

**手順:**
1. サービス停止: `loco stop`
2. ログ保存: ログディレクトリをバックアップ
3. 原因調査: 監査ログ分析
4. 修正適用: 設定更新
5. 再起動: セキュリティ設定確認後

---

## 暗号化とデータ保護

### ファイル暗号化

**使用例:**
```csharp
using Loco.Core.Security;

// データ暗号化
string encrypted = CryptographicService.Encrypt(sensitiveData, password);

// データ復号化
string decrypted = CryptographicService.Decrypt(encrypted, password);
```

**仕様:**
- **アルゴリズム:** AES-256
- **モード:** CBC
- **IV:** ランダム生成 (16バイト)

---

## コンプライアンス

### OWASP Top 10 対策状況

| 項目 | 対策状況 | 実装内容 |
|------|---------|----------|
| A01: アクセス制御 | ✅ | パスホワイトリスト、権限チェック |
| A02: 暗号化の失敗 | ✅ | AES-256、PBKDF2 (600k iterations) |
| A03: インジェクション | ✅ | ArgumentList、パラメータ検証 |
| A04: 安全でない設計 | ✅ | 多層防御、最小権限 |
| A05: セキュリティ設定ミス | ✅ | セキュアデフォルト |
| A06: 脆弱なコンポーネント | ✅ | 定期更新、依存関係管理 |
| A07: 認証の失敗 | ✅ | 強力なハッシュ、レート制限 |
| A08: データ完全性 | ✅ | アトミック書き込み、バックアップ |
| A09: ログ監視の失敗 | ✅ | 構造化ログ、監査証跡 |
| A10: SSRF | ✅ | ドメインホワイトリスト |

---

## セキュリティ連絡先

セキュリティ脆弱性を発見した場合:
- GitHubで非公開Issue作成
- 詳細情報を含めて報告
- 修正完了まで公開を控える

---

**最終更新:** 2025-10-10
**バージョン:** 1.0
**対象:** Loco v1.0+
