using System.Text;
using System.Text.Json;

namespace Loco.Core.Validation;

/// <summary>
/// ワークフロー検証機能
/// 市販レベル・国家レベルで必須の厳密な事前検証
/// 実行前にすべての問題を検出し、安定性を確保
/// </summary>
public sealed class WorkflowValidator
{
    /// <summary>
    /// 検証結果
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<ValidationError> Errors { get; set; } = new();
        public List<ValidationWarning> Warnings { get; set; } = new();
        public List<ValidationInfo> Infos { get; set; } = new();
    }

    /// <summary>
    /// 検証エラー
    /// </summary>
    public class ValidationError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
    }

    /// <summary>
    /// 検証警告
    /// </summary>
    public class ValidationWarning
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }

    /// <summary>
    /// 検証情報
    /// </summary>
    public class ValidationInfo
    {
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// ワークフロー定義を検証します
    /// </summary>
    public ValidationResult ValidateWorkflow(object workflowDefinition)
    {
        var result = new ValidationResult();

        if (workflowDefinition == null)
        {
            result.Errors.Add(new ValidationError
            {
                Code = "NULL_WORKFLOW",
                Message = "ワークフロー定義がnullです",
                Suggestion = "有効なワークフロー定義を指定してください"
            });
            return result;
        }

        // JSON要素として扱う
        var json = JsonSerializer.Serialize(workflowDefinition);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 必須フィールドの検証
        ValidateRequiredFields(root, result);

        // アクションの検証
        if (root.TryGetProperty("actions", out var actionsElement))
        {
            ValidateActions(actionsElement, result);
        }

        // 条件の検証
        if (root.TryGetProperty("conditions", out var conditionsElement))
        {
            ValidateConditions(conditionsElement, result);
        }

        // トリガーの検証
        if (root.TryGetProperty("triggers", out var triggersElement))
        {
            ValidateTriggers(triggersElement, result);
        }

        // セキュリティの検証
        ValidateSecurity(root, result);

        // パフォーマンスの検証
        ValidatePerformance(root, result);

        return result;
    }

    /// <summary>
    /// 必須フィールドを検証します
    /// </summary>
    private void ValidateRequiredFields(JsonElement root, ValidationResult result)
    {
        // 名前の検証
        if (!root.TryGetProperty("name", out var nameElement) || string.IsNullOrWhiteSpace(nameElement.GetString()))
        {
            result.Errors.Add(new ValidationError
            {
                Code = "MISSING_NAME",
                Message = "ワークフロー名が指定されていません",
                Location = "root.name",
                Suggestion = "nameフィールドを追加してください"
            });
        }
        else
        {
            var name = nameElement.GetString() ?? "";

            // 名前の長さチェック
            if (name.Length > 100)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Code = "LONG_NAME",
                    Message = $"ワークフロー名が長すぎます（{name.Length}文字）",
                    Location = "root.name",
                    Recommendation = "100文字以内に収めることを推奨します"
                });
            }

            // 不正な文字チェック
            if (name.Contains('\\') || name.Contains('/') || name.Contains(':'))
            {
                result.Errors.Add(new ValidationError
                {
                    Code = "INVALID_NAME",
                    Message = "ワークフロー名に不正な文字が含まれています",
                    Location = "root.name",
                    Suggestion = "\\, /, : などの文字は使用できません"
                });
            }
        }

        // バージョンの検証
        if (!root.TryGetProperty("version", out var versionElement))
        {
            result.Warnings.Add(new ValidationWarning
            {
                Code = "MISSING_VERSION",
                Message = "バージョン情報が指定されていません",
                Location = "root.version",
                Recommendation = "バージョン管理のため、versionフィールドの追加を推奨します"
            });
        }
    }

    /// <summary>
    /// アクションを検証します
    /// </summary>
    private void ValidateActions(JsonElement actionsElement, ValidationResult result)
    {
        if (actionsElement.ValueKind != JsonValueKind.Array)
        {
            result.Errors.Add(new ValidationError
            {
                Code = "INVALID_ACTIONS",
                Message = "actionsは配列である必要があります",
                Location = "root.actions"
            });
            return;
        }

        var actionCount = actionsElement.GetArrayLength();
        if (actionCount == 0)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Code = "EMPTY_ACTIONS",
                Message = "アクションが定義されていません",
                Location = "root.actions",
                Recommendation = "少なくとも1つのアクションを定義してください"
            });
        }

        // アクション数の警告
        if (actionCount > 100)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Code = "TOO_MANY_ACTIONS",
                Message = $"アクション数が多すぎます（{actionCount}個）",
                Location = "root.actions",
                Recommendation = "パフォーマンスのため、ワークフローを分割することを推奨します"
            });
        }

        // 各アクションの検証
        int index = 0;
        foreach (var action in actionsElement.EnumerateArray())
        {
            var location = $"root.actions[{index}]";

            // typeフィールドの検証
            if (!action.TryGetProperty("type", out var typeElement) || string.IsNullOrWhiteSpace(typeElement.GetString()))
            {
                result.Errors.Add(new ValidationError
                {
                    Code = "MISSING_ACTION_TYPE",
                    Message = "アクションのtypeが指定されていません",
                    Location = location,
                    Suggestion = "typeフィールドを追加してください（例: file, process, email）"
                });
            }
            else
            {
                var type = typeElement.GetString() ?? "";
                ValidateActionType(type, action, location, result);
            }

            index++;
        }
    }

    /// <summary>
    /// アクションタイプ別の検証を実行します
    /// </summary>
    private void ValidateActionType(string type, JsonElement action, string location, ValidationResult result)
    {
        switch (type.ToLowerInvariant())
        {
            case "file":
            case "file_copy":
            case "file_move":
            case "file_delete":
                ValidateFileAction(action, location, result);
                break;

            case "process":
            case "execute":
                ValidateProcessAction(action, location, result);
                break;

            case "http":
            case "webhook":
                ValidateHttpAction(action, location, result);
                break;

            case "email":
                ValidateEmailAction(action, location, result);
                break;
        }
    }

    /// <summary>
    /// ファイルアクションを検証します
    /// </summary>
    private void ValidateFileAction(JsonElement action, string location, ValidationResult result)
    {
        // pathフィールドの検証
        if (!action.TryGetProperty("path", out var pathElement) || string.IsNullOrWhiteSpace(pathElement.GetString()))
        {
            result.Errors.Add(new ValidationError
            {
                Code = "MISSING_PATH",
                Message = "ファイルパスが指定されていません",
                Location = $"{location}.path",
                Suggestion = "pathフィールドを追加してください"
            });
        }
        else
        {
            var path = pathElement.GetString() ?? "";

            // 絶対パスチェック
            if (!Path.IsPathRooted(path) && !path.StartsWith("$") && !path.StartsWith("{"))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Code = "RELATIVE_PATH",
                    Message = "相対パスが使用されています",
                    Location = $"{location}.path",
                    Recommendation = "絶対パスの使用を推奨します"
                });
            }

            // 危険なパスチェック
            var dangerousPaths = new[] { "C:\\Windows", "C:\\Program Files", "/bin", "/sbin", "/etc" };
            foreach (var dangerous in dangerousPaths)
            {
                if (path.StartsWith(dangerous, StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add(new ValidationError
                    {
                        Code = "DANGEROUS_PATH",
                        Message = $"システムディレクトリへのアクセスは危険です: {dangerous}",
                        Location = $"{location}.path",
                        Suggestion = "ユーザーデータディレクトリを使用してください"
                    });
                }
            }
        }
    }

    /// <summary>
    /// プロセスアクションを検証します
    /// </summary>
    private void ValidateProcessAction(JsonElement action, string location, ValidationResult result)
    {
        // commandフィールドの検証
        if (!action.TryGetProperty("command", out var commandElement) || string.IsNullOrWhiteSpace(commandElement.GetString()))
        {
            result.Errors.Add(new ValidationError
            {
                Code = "MISSING_COMMAND",
                Message = "実行コマンドが指定されていません",
                Location = $"{location}.command",
                Suggestion = "commandフィールドを追加してください"
            });
        }
        else
        {
            var command = commandElement.GetString() ?? "";

            // 危険なコマンドチェック
            var dangerousCommands = new[] { "format", "del /s", "rm -rf", "shutdown", "reboot", "mkfs" };
            foreach (var dangerous in dangerousCommands)
            {
                if (command.Contains(dangerous, StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add(new ValidationError
                    {
                        Code = "DANGEROUS_COMMAND",
                        Message = $"危険なコマンドが検出されました: {dangerous}",
                        Location = $"{location}.command",
                        Suggestion = "このコマンドの実行は推奨されません"
                    });
                }
            }
        }

        // タイムアウトの検証
        if (action.TryGetProperty("timeout", out var timeoutElement) && timeoutElement.ValueKind == JsonValueKind.Number)
        {
            var timeout = timeoutElement.GetInt32();
            if (timeout > 3600000) // 1時間
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Code = "LONG_TIMEOUT",
                    Message = $"タイムアウトが長すぎます（{timeout}ms）",
                    Location = $"{location}.timeout",
                    Recommendation = "1時間以内に収めることを推奨します"
                });
            }
        }
    }

    /// <summary>
    /// HTTPアクションを検証します
    /// </summary>
    private void ValidateHttpAction(JsonElement action, string location, ValidationResult result)
    {
        // urlフィールドの検証
        if (!action.TryGetProperty("url", out var urlElement) || string.IsNullOrWhiteSpace(urlElement.GetString()))
        {
            result.Errors.Add(new ValidationError
            {
                Code = "MISSING_URL",
                Message = "URLが指定されていません",
                Location = $"{location}.url",
                Suggestion = "urlフィールドを追加してください"
            });
        }
        else
        {
            var url = urlElement.GetString() ?? "";

            // URLの形式チェック
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                result.Errors.Add(new ValidationError
                {
                    Code = "INVALID_URL",
                    Message = "無効なURL形式です",
                    Location = $"{location}.url",
                    Suggestion = "http:// または https:// で始まる完全なURLを指定してください"
                });
            }
            else
            {
                // HTTPSの推奨
                if (uri.Scheme == "http")
                {
                    result.Warnings.Add(new ValidationWarning
                    {
                        Code = "HTTP_NOT_SECURE",
                        Message = "HTTPSではなくHTTPが使用されています",
                        Location = $"{location}.url",
                        Recommendation = "セキュリティのためHTTPSの使用を推奨します"
                    });
                }
            }
        }
    }

    /// <summary>
    /// メールアクションを検証します
    /// </summary>
    private void ValidateEmailAction(JsonElement action, string location, ValidationResult result)
    {
        // toフィールドの検証
        if (!action.TryGetProperty("to", out var toElement) || string.IsNullOrWhiteSpace(toElement.GetString()))
        {
            result.Errors.Add(new ValidationError
            {
                Code = "MISSING_EMAIL_TO",
                Message = "送信先メールアドレスが指定されていません",
                Location = $"{location}.to",
                Suggestion = "toフィールドを追加してください"
            });
        }
    }

    /// <summary>
    /// 条件を検証します
    /// </summary>
    private void ValidateConditions(JsonElement conditionsElement, ValidationResult result)
    {
        if (conditionsElement.ValueKind != JsonValueKind.Array)
        {
            result.Errors.Add(new ValidationError
            {
                Code = "INVALID_CONDITIONS",
                Message = "conditionsは配列である必要があります",
                Location = "root.conditions"
            });
        }
    }

    /// <summary>
    /// トリガーを検証します
    /// </summary>
    private void ValidateTriggers(JsonElement triggersElement, ValidationResult result)
    {
        if (triggersElement.ValueKind != JsonValueKind.Array)
        {
            result.Errors.Add(new ValidationError
            {
                Code = "INVALID_TRIGGERS",
                Message = "triggersは配列である必要があります",
                Location = "root.triggers"
            });
        }
    }

    /// <summary>
    /// セキュリティを検証します
    /// </summary>
    private void ValidateSecurity(JsonElement root, ValidationResult result)
    {
        // パスワードや秘密鍵の平文チェック
        var jsonText = root.ToString();

        if (jsonText.Contains("password", StringComparison.OrdinalIgnoreCase) &&
            !jsonText.Contains("$env:", StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add(new ValidationWarning
            {
                Code = "HARDCODED_PASSWORD",
                Message = "パスワードがハードコードされている可能性があります",
                Recommendation = "環境変数や秘密管理システムの使用を推奨します"
            });
        }
    }

    /// <summary>
    /// パフォーマンスを検証します
    /// </summary>
    private void ValidatePerformance(JsonElement root, ValidationResult result)
    {
        // ループの検証
        if (root.TryGetProperty("loop", out var loopElement))
        {
            if (loopElement.TryGetProperty("count", out var countElement) && countElement.ValueKind == JsonValueKind.Number)
            {
                var count = countElement.GetInt32();
                if (count > 1000)
                {
                    result.Warnings.Add(new ValidationWarning
                    {
                        Code = "HIGH_LOOP_COUNT",
                        Message = $"ループ回数が多すぎます（{count}回）",
                        Location = "root.loop.count",
                        Recommendation = "パフォーマンスのため、1000回以下に収めることを推奨します"
                    });
                }
            }
        }
    }

    /// <summary>
    /// 検証結果を色付きで表示します
    /// </summary>
    public static void PrintValidationResult(ValidationResult result)
    {
        var originalColor = Console.ForegroundColor;

        try
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  ワークフロー検証結果 / Workflow Validation Result              ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            if (result.IsValid)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ 検証成功: ワークフローは有効です");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 検証失敗: {result.Errors.Count}個のエラーがあります");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine($"エラー: {result.Errors.Count}");
            Console.WriteLine($"警告:   {result.Warnings.Count}");
            Console.WriteLine($"情報:   {result.Infos.Count}");
            Console.WriteLine();

            // エラーの表示
            if (result.Errors.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("【エラー / Errors】");
                Console.ResetColor();
                foreach (var error in result.Errors)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [{error.Code}] {error.Message}");
                    Console.ResetColor();
                    if (!string.IsNullOrEmpty(error.Location))
                    {
                        Console.WriteLine($"    場所: {error.Location}");
                    }
                    if (!string.IsNullOrEmpty(error.Suggestion))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"    提案: {error.Suggestion}");
                        Console.ResetColor();
                    }
                    Console.WriteLine();
                }
            }

            // 警告の表示
            if (result.Warnings.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("【警告 / Warnings】");
                Console.ResetColor();
                foreach (var warning in result.Warnings)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  [{warning.Code}] {warning.Message}");
                    Console.ResetColor();
                    if (!string.IsNullOrEmpty(warning.Location))
                    {
                        Console.WriteLine($"    場所: {warning.Location}");
                    }
                    if (!string.IsNullOrEmpty(warning.Recommendation))
                    {
                        Console.WriteLine($"    推奨: {warning.Recommendation}");
                    }
                    Console.WriteLine();
                }
            }
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
    }
}
