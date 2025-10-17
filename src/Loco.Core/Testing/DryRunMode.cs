using System.Text.Json;

namespace Loco.Core.Testing;

/// <summary>
/// ドライラン（テストモード）機能
/// 実際には実行せず、何が起こるかをシミュレーションします
/// YouTubeレビューで指摘される「テストが面倒」問題を解決
/// </summary>
public sealed class DryRunMode
{
    private readonly List<SimulatedAction> _simulatedActions = new();
    private bool _enabled;

    /// <summary>
    /// シミュレートされたアクション
    /// </summary>
    public class SimulatedAction
    {
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string WhatWouldHappen { get; set; } = string.Empty;
        public List<string> SafetyChecks { get; set; } = new();
        public bool IsSafe { get; set; } = true;
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// ドライランモードが有効かどうか
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>
    /// シミュレートされたアクションのリスト
    /// </summary>
    public IReadOnlyList<SimulatedAction> SimulatedActions => _simulatedActions.AsReadOnly();

    /// <summary>
    /// アクションをシミュレートします
    /// </summary>
    public void SimulateAction(
        string actionType,
        string description,
        Dictionary<string, object> parameters,
        string whatWouldHappen)
    {
        if (!_enabled)
        {
            return;
        }

        var action = new SimulatedAction
        {
            ActionType = actionType,
            Description = description,
            Parameters = parameters,
            WhatWouldHappen = whatWouldHappen
        };

        // 安全性チェック
        PerformSafetyChecks(action);

        _simulatedActions.Add(action);
    }

    /// <summary>
    /// 安全性チェックを実行します
    /// </summary>
    private void PerformSafetyChecks(SimulatedAction action)
    {
        action.SafetyChecks.Clear();
        action.Warnings.Clear();
        action.IsSafe = true;

        switch (action.ActionType.ToLowerInvariant())
        {
            case "file_delete":
            case "delete":
                action.SafetyChecks.Add("ファイル削除操作を検出");
                if (action.Parameters.TryGetValue("path", out var path))
                {
                    var pathStr = path?.ToString() ?? string.Empty;

                    // システムディレクトリチェック
                    if (IsSystemDirectory(pathStr))
                    {
                        action.IsSafe = false;
                        action.Warnings.Add("⚠️ 警告: システムディレクトリへの削除操作");
                    }

                    // ワイルドカードチェック
                    if (pathStr.Contains("*"))
                    {
                        action.Warnings.Add("⚠️ 注意: ワイルドカードによる複数ファイル削除");
                    }

                    action.SafetyChecks.Add($"削除対象: {pathStr}");
                }
                break;

            case "file_write":
            case "write":
                action.SafetyChecks.Add("ファイル書き込み操作を検出");
                if (action.Parameters.TryGetValue("path", out var writePath))
                {
                    var pathStr = writePath?.ToString() ?? string.Empty;

                    if (IsSystemDirectory(pathStr))
                    {
                        action.IsSafe = false;
                        action.Warnings.Add("⚠️ 警告: システムディレクトリへの書き込み操作");
                    }

                    // 既存ファイル上書きチェック
                    if (File.Exists(pathStr))
                    {
                        action.Warnings.Add("⚠️ 注意: 既存ファイルを上書きします");
                    }

                    action.SafetyChecks.Add($"書き込み先: {pathStr}");
                }
                break;

            case "process":
            case "execute":
                action.SafetyChecks.Add("プログラム実行操作を検出");
                if (action.Parameters.TryGetValue("command", out var cmd))
                {
                    var cmdStr = cmd?.ToString() ?? string.Empty;

                    // 危険なコマンドチェック
                    var dangerousCommands = new[] { "format", "del /s", "rm -rf", "shutdown", "reboot" };
                    foreach (var dangerous in dangerousCommands)
                    {
                        if (cmdStr.Contains(dangerous, StringComparison.OrdinalIgnoreCase))
                        {
                            action.IsSafe = false;
                            action.Warnings.Add($"⚠️ 警告: 危険なコマンド '{dangerous}' を検出");
                        }
                    }

                    action.SafetyChecks.Add($"実行コマンド: {cmdStr}");
                }
                break;

            case "http":
            case "webhook":
                action.SafetyChecks.Add("HTTP/ネットワーク操作を検出");
                if (action.Parameters.TryGetValue("url", out var url))
                {
                    action.SafetyChecks.Add($"送信先: {url}");
                }
                action.Warnings.Add("💡 ドライランモードではHTTPリクエストは実際には送信されません");
                break;

            case "email":
                action.SafetyChecks.Add("メール送信操作を検出");
                if (action.Parameters.TryGetValue("to", out var to))
                {
                    action.SafetyChecks.Add($"送信先: {to}");
                }
                action.Warnings.Add("💡 ドライランモードではメールは実際には送信されません");
                break;
        }
    }

    /// <summary>
    /// システムディレクトリかどうかをチェック
    /// </summary>
    private static bool IsSystemDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var systemPaths = new[]
        {
            @"C:\Windows",
            @"C:\Program Files",
            @"C:\Program Files (x86)",
            @"/bin",
            @"/sbin",
            @"/usr/bin",
            @"/usr/sbin",
            @"/etc",
            @"/System",
            @"/Library"
        };

        var normalizedPath = Path.GetFullPath(path);
        return systemPaths.Any(sp => normalizedPath.StartsWith(sp, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// シミュレーション結果をレポートとして出力します
    /// </summary>
    public string GenerateReport()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("╔══════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║  ドライランレポート / Dry Run Report                             ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine("このレポートは、実際には何も実行されていません。");
        sb.AppendLine("以下は、本番実行時に何が起こるかのシミュレーションです。");
        sb.AppendLine();

        if (_simulatedActions.Count == 0)
        {
            sb.AppendLine("シミュレートされたアクションはありません。");
            return sb.ToString();
        }

        sb.AppendLine($"総アクション数: {_simulatedActions.Count}");
        sb.AppendLine($"安全なアクション: {_simulatedActions.Count(a => a.IsSafe)}");
        sb.AppendLine($"警告があるアクション: {_simulatedActions.Count(a => a.Warnings.Count > 0)}");
        sb.AppendLine();

        for (int i = 0; i < _simulatedActions.Count; i++)
        {
            var action = _simulatedActions[i];
            sb.AppendLine($"━━━ アクション #{i + 1}: {action.ActionType} ━━━");
            sb.AppendLine();
            sb.AppendLine($"説明: {action.Description}");
            sb.AppendLine($"実行内容: {action.WhatWouldHappen}");
            sb.AppendLine();

            if (action.Parameters.Count > 0)
            {
                sb.AppendLine("パラメータ:");
                foreach (var param in action.Parameters)
                {
                    sb.AppendLine($"  • {param.Key}: {param.Value}");
                }
                sb.AppendLine();
            }

            if (action.SafetyChecks.Count > 0)
            {
                sb.AppendLine("安全性チェック:");
                foreach (var check in action.SafetyChecks)
                {
                    sb.AppendLine($"  ✓ {check}");
                }
                sb.AppendLine();
            }

            if (action.Warnings.Count > 0)
            {
                sb.AppendLine("警告:");
                foreach (var warning in action.Warnings)
                {
                    sb.AppendLine($"  {warning}");
                }
                sb.AppendLine();
            }

            if (!action.IsSafe)
            {
                sb.AppendLine("🚫 このアクションは安全性の問題があります！");
                sb.AppendLine("   本番実行前に設定を見直すことを強く推奨します。");
                sb.AppendLine();
            }
        }

        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();
        sb.AppendLine("【次のステップ】");
        sb.AppendLine("  1. 上記の内容を確認してください");
        sb.AppendLine("  2. 警告がある場合は、設定を見直してください");
        sb.AppendLine("  3. 問題がなければ、--dry-run フラグを外して本番実行してください");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// JSON形式でレポートを生成します
    /// </summary>
    public string GenerateJsonReport()
    {
        var report = new
        {
            DryRun = true,
            TotalActions = _simulatedActions.Count,
            SafeActions = _simulatedActions.Count(a => a.IsSafe),
            ActionsWithWarnings = _simulatedActions.Count(a => a.Warnings.Count > 0),
            Actions = _simulatedActions
        };

        return JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// コンソールに色付きでレポートを表示します
    /// </summary>
    public void PrintColoredReport()
    {
        var originalColor = Console.ForegroundColor;

        try
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  ドライランレポート / Dry Run Report                             ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("このレポートは、実際には何も実行されていません。");
            Console.WriteLine("以下は、本番実行時に何が起こるかのシミュレーションです。");
            Console.ResetColor();
            Console.WriteLine();

            if (_simulatedActions.Count == 0)
            {
                Console.WriteLine("シミュレートされたアクションはありません。");
                return;
            }

            Console.WriteLine($"総アクション数: {_simulatedActions.Count}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"安全なアクション: {_simulatedActions.Count(a => a.IsSafe)}");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"警告があるアクション: {_simulatedActions.Count(a => a.Warnings.Count > 0)}");
            Console.ResetColor();
            Console.WriteLine();

            for (int i = 0; i < _simulatedActions.Count; i++)
            {
                var action = _simulatedActions[i];

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"━━━ アクション #{i + 1}: {action.ActionType} ━━━");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine($"説明: {action.Description}");
                Console.WriteLine($"実行内容: {action.WhatWouldHappen}");
                Console.WriteLine();

                if (action.Warnings.Count > 0)
                {
                    foreach (var warning in action.Warnings)
                    {
                        if (warning.StartsWith("⚠️ 警告"))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                        }
                        else if (warning.StartsWith("⚠️ 注意"))
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                        }
                        Console.WriteLine($"  {warning}");
                    }
                    Console.ResetColor();
                    Console.WriteLine();
                }

                if (!action.IsSafe)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("🚫 このアクションは安全性の問題があります！");
                    Console.WriteLine("   本番実行前に設定を見直すことを強く推奨します。");
                    Console.ResetColor();
                    Console.WriteLine();
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("【次のステップ】");
            Console.ResetColor();
            Console.WriteLine("  1. 上記の内容を確認してください");
            Console.WriteLine("  2. 警告がある場合は、設定を見直してください");
            Console.WriteLine("  3. 問題がなければ、--dry-run フラグを外して本番実行してください");
            Console.WriteLine();
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
    }

    /// <summary>
    /// シミュレーション結果をクリアします
    /// </summary>
    public void Clear()
    {
        _simulatedActions.Clear();
    }
}
