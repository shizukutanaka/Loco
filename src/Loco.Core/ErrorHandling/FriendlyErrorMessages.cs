using System.Text;

namespace Loco.Core.ErrorHandling;

/// <summary>
/// 初心者にも分かりやすいエラーメッセージシステム
/// YouTubeレビューで指摘される「エラーメッセージが技術的すぎる」問題を解決
/// </summary>
public static class FriendlyErrorMessages
{
    /// <summary>
    /// エラーコンテキスト
    /// </summary>
    public class ErrorContext
    {
        public string TechnicalMessage { get; set; } = string.Empty;
        public string UserFriendlyMessage { get; set; } = string.Empty;
        public List<string> PossibleCauses { get; set; } = new();
        public List<string> Solutions { get; set; } = new();
        public string HelpDocumentUrl { get; set; } = string.Empty;
        public string? RelatedCommand { get; set; }
    }

    private static readonly Dictionary<string, ErrorContext> ErrorPatterns = new()
    {
        // ファイルアクセスエラー
        ["FileNotFoundException"] = new ErrorContext
        {
            UserFriendlyMessage = "ファイルが見つかりませんでした",
            PossibleCauses = new List<string>
            {
                "ファイルパスが間違っている",
                "ファイルが削除または移動された",
                "ファイル名の入力ミス（大文字小文字の違いなど）"
            },
            Solutions = new List<string>
            {
                "ファイルパスを確認してください",
                "ファイルが正しい場所にあるか確認してください",
                "ファイル名のスペルを確認してください",
                "絶対パスで指定してみてください"
            },
            HelpDocumentUrl = "TROUBLESHOOTING.md#file-not-found"
        },

        ["UnauthorizedAccessException"] = new ErrorContext
        {
            UserFriendlyMessage = "ファイルやフォルダーにアクセスできませんでした",
            PossibleCauses = new List<string>
            {
                "ファイルが他のプログラムで開かれている",
                "管理者権限が必要なファイル",
                "ファイルが読み取り専用になっている",
                "フォルダーのアクセス許可がない"
            },
            Solutions = new List<string>
            {
                "ファイルを開いている他のプログラムを閉じてください",
                "管理者として実行してください",
                "ファイルの読み取り専用属性を解除してください",
                "フォルダーのアクセス許可を確認してください"
            },
            HelpDocumentUrl = "TROUBLESHOOTING.md#access-denied"
        },

        ["IOException"] = new ErrorContext
        {
            UserFriendlyMessage = "ファイルの読み書き中にエラーが発生しました",
            PossibleCauses = new List<string>
            {
                "ディスク容量が不足している",
                "ファイルが破損している",
                "ネットワークドライブへの接続が切れた"
            },
            Solutions = new List<string>
            {
                "ディスクの空き容量を確認してください",
                "別の場所にファイルをコピーしてみてください",
                "ネットワーク接続を確認してください"
            },
            HelpDocumentUrl = "TROUBLESHOOTING.md#io-error"
        },

        // ネットワークエラー
        ["HttpRequestException"] = new ErrorContext
        {
            UserFriendlyMessage = "インターネット接続に問題があります",
            PossibleCauses = new List<string>
            {
                "インターネットに接続していない",
                "ファイアウォールがブロックしている",
                "接続先のサーバーがダウンしている",
                "プロキシ設定が必要"
            },
            Solutions = new List<string>
            {
                "インターネット接続を確認してください",
                "ファイアウォール設定を確認してください",
                "しばらく待ってから再試行してください",
                "プロキシ設定が必要か確認してください"
            },
            HelpDocumentUrl = "TROUBLESHOOTING.md#network-error"
        },

        ["TaskCanceledException"] = new ErrorContext
        {
            UserFriendlyMessage = "処理がタイムアウトしました",
            PossibleCauses = new List<string>
            {
                "サーバーの応答が遅い",
                "ネットワークが不安定",
                "処理に時間がかかりすぎている"
            },
            Solutions = new List<string>
            {
                "ネットワーク接続を確認してください",
                "タイムアウト時間を延長してみてください",
                "しばらく待ってから再試行してください"
            },
            HelpDocumentUrl = "TROUBLESHOOTING.md#timeout"
        },

        // 設定エラー
        ["JsonException"] = new ErrorContext
        {
            UserFriendlyMessage = "設定ファイルの形式が正しくありません",
            PossibleCauses = new List<string>
            {
                "JSON形式が間違っている",
                "カンマやカッコが足りない、または多い",
                "文字列がダブルクォートで囲まれていない"
            },
            Solutions = new List<string>
            {
                "設定ファイルのJSON形式を確認してください",
                "JSONバリデーターで検証してください (例: jsonlint.com)",
                "サンプル設定ファイルと比較してください",
                "'loco validate' コマンドで設定を検証してください"
            },
            HelpDocumentUrl = "docs/CONFIGURATION.md",
            RelatedCommand = "loco validate"
        },

        // プロセスエラー
        ["Win32Exception"] = new ErrorContext
        {
            UserFriendlyMessage = "プログラムの実行に失敗しました",
            PossibleCauses = new List<string>
            {
                "プログラムが見つからない",
                "プログラム名のスペルミス",
                "環境変数PATHに登録されていない",
                "実行権限がない"
            },
            Solutions = new List<string>
            {
                "プログラムがインストールされているか確認してください",
                "プログラムのフルパスを指定してみてください",
                "環境変数PATHを確認してください",
                "実行権限を確認してください"
            },
            HelpDocumentUrl = "TROUBLESHOOTING.md#process-error"
        },

        // メモリエラー
        ["OutOfMemoryException"] = new ErrorContext
        {
            UserFriendlyMessage = "メモリが不足しています",
            PossibleCauses = new List<string>
            {
                "処理するデータが大きすぎる",
                "他のプログラムがメモリを大量に使用している",
                "メモリリークが発生している"
            },
            Solutions = new List<string>
            {
                "他のプログラムを終了してメモリを解放してください",
                "データを小さく分割して処理してください",
                "コンピューターを再起動してください",
                "メモリ使用量の多いフローを見直してください"
            },
            HelpDocumentUrl = "PERFORMANCE_TUNING.md#memory"
        }
    };

    /// <summary>
    /// 例外を初心者向けのエラーメッセージに変換します
    /// </summary>
    /// <param name="exception">発生した例外</param>
    /// <param name="additionalContext">追加のコンテキスト情報</param>
    /// <returns>分かりやすいエラーメッセージ</returns>
    public static string GetFriendlyErrorMessage(Exception exception, string? additionalContext = null)
    {
        var sb = new StringBuilder();

        // ヘッダー
        sb.AppendLine("╔══════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║  エラーが発生しました / An Error Occurred                        ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        // エラーコンテキストを取得
        var context = GetErrorContext(exception);

        // 分かりやすいメッセージ
        sb.AppendLine("【何が起きたか / What Happened】");
        sb.AppendLine($"  {context.UserFriendlyMessage}");
        sb.AppendLine();

        // 追加コンテキスト
        if (!string.IsNullOrWhiteSpace(additionalContext))
        {
            sb.AppendLine("【詳細 / Details】");
            sb.AppendLine($"  {additionalContext}");
            sb.AppendLine();
        }

        // 考えられる原因
        if (context.PossibleCauses.Count > 0)
        {
            sb.AppendLine("【考えられる原因 / Possible Causes】");
            for (int i = 0; i < context.PossibleCauses.Count; i++)
            {
                sb.AppendLine($"  {i + 1}. {context.PossibleCauses[i]}");
            }
            sb.AppendLine();
        }

        // 解決方法
        if (context.Solutions.Count > 0)
        {
            sb.AppendLine("【解決方法 / Solutions】");
            for (int i = 0; i < context.Solutions.Count; i++)
            {
                sb.AppendLine($"  {i + 1}. {context.Solutions[i]}");
            }
            sb.AppendLine();
        }

        // 関連コマンド
        if (!string.IsNullOrWhiteSpace(context.RelatedCommand))
        {
            sb.AppendLine("【役立つコマンド / Helpful Command】");
            sb.AppendLine($"  {context.RelatedCommand}");
            sb.AppendLine();
        }

        // ヘルプドキュメント
        if (!string.IsNullOrWhiteSpace(context.HelpDocumentUrl))
        {
            sb.AppendLine("【詳細情報 / More Information】");
            sb.AppendLine($"  {context.HelpDocumentUrl}");
            sb.AppendLine();
        }

        // 技術的な詳細（デバッグ用、折りたたみ可能）
        sb.AppendLine("【技術的な詳細 / Technical Details】");
        sb.AppendLine($"  エラータイプ: {exception.GetType().Name}");
        sb.AppendLine($"  メッセージ: {exception.Message}");
        sb.AppendLine();

        // サポート情報
        sb.AppendLine("【さらにヘルプが必要な場合 / Need More Help?】");
        sb.AppendLine("  1. FAQ.md で類似の問題を検索してください");
        sb.AppendLine("  2. TROUBLESHOOTING.md でトラブルシューティングを確認してください");
        sb.AppendLine("  3. 'loco diag' コマンドで診断情報を収集してください");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// エラーコンテキストを取得します
    /// </summary>
    private static ErrorContext GetErrorContext(Exception exception)
    {
        var exceptionType = exception.GetType().Name;

        if (ErrorPatterns.TryGetValue(exceptionType, out var context))
        {
            return context;
        }

        // デフォルトコンテキスト
        return new ErrorContext
        {
            UserFriendlyMessage = "予期しないエラーが発生しました",
            PossibleCauses = new List<string>
            {
                "プログラムの不具合の可能性があります",
                "予期しない入力データ",
                "システムリソースの問題"
            },
            Solutions = new List<string>
            {
                "コンピューターを再起動してみてください",
                "最新バージョンにアップデートしてください",
                "'loco diag' で診断情報を収集してください",
                "それでも解決しない場合は、クラッシュレポートを確認してください"
            },
            HelpDocumentUrl = "TROUBLESHOOTING.md"
        };
    }

    /// <summary>
    /// 簡易版エラーメッセージ（1行）
    /// </summary>
    public static string GetShortErrorMessage(Exception exception)
    {
        var context = GetErrorContext(exception);
        return $"❌ {context.UserFriendlyMessage} - {exception.Message}";
    }

    /// <summary>
    /// コンソール向けの色付きエラーメッセージ
    /// </summary>
    public static void PrintColoredError(Exception exception, string? additionalContext = null)
    {
        var originalColor = Console.ForegroundColor;

        try
        {
            var context = GetErrorContext(exception);

            // ヘッダー
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  エラーが発生しました / An Error Occurred                        ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // 分かりやすいメッセージ
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("【何が起きたか / What Happened】");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  {context.UserFriendlyMessage}");
            Console.WriteLine();

            // 追加コンテキスト
            if (!string.IsNullOrWhiteSpace(additionalContext))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("【詳細 / Details】");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"  {additionalContext}");
                Console.WriteLine();
            }

            // 考えられる原因
            if (context.PossibleCauses.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("【考えられる原因 / Possible Causes】");
                Console.ForegroundColor = ConsoleColor.Gray;
                for (int i = 0; i < context.PossibleCauses.Count; i++)
                {
                    Console.WriteLine($"  {i + 1}. {context.PossibleCauses[i]}");
                }
                Console.WriteLine();
            }

            // 解決方法
            if (context.Solutions.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("【解決方法 / Solutions】");
                Console.ForegroundColor = ConsoleColor.White;
                for (int i = 0; i < context.Solutions.Count; i++)
                {
                    Console.WriteLine($"  {i + 1}. {context.Solutions[i]}");
                }
                Console.WriteLine();
            }

            // 関連コマンド
            if (!string.IsNullOrWhiteSpace(context.RelatedCommand))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("【役立つコマンド / Helpful Command】");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"  {context.RelatedCommand}");
                Console.WriteLine();
            }

            // ヘルプドキュメント
            if (!string.IsNullOrWhiteSpace(context.HelpDocumentUrl))
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("【詳細情報 / More Information】");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"  {context.HelpDocumentUrl}");
                Console.WriteLine();
            }

            // 技術的な詳細
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("【技術的な詳細 / Technical Details】");
            Console.WriteLine($"  エラータイプ: {exception.GetType().Name}");
            Console.WriteLine($"  メッセージ: {exception.Message}");
            Console.WriteLine();
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
    }
}
