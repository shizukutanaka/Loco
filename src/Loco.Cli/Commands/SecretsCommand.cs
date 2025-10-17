using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loco.Core.Security;
using Loco.Cli.UI;

namespace Loco.Cli.Commands;

/// <summary>
/// CLI command for secrets management operations
/// シークレット管理用のCLIコマンド
/// </summary>
public class SecretsCommand : BaseCommand
{
    private readonly SecretsManager _secretsManager;

    public SecretsCommand(SecretsManager? secretsManager = null)
    {
        _secretsManager = secretsManager ?? new SecretsManager();
    }

    public override CommandHelp GetHelp()
    {
        return new CommandHelp
        {
            Name = "secrets",
            Description = "Manage encrypted secrets and credentials",
            Usage = "loco secrets <command> [options]",
            Subcommands = new[]
            {
                "set <key> <value>      - Store a new secret",
                "get <key>              - Retrieve a secret value",
                "list                   - List all secrets (metadata only)",
                "delete <key>           - Delete a secret (requires --confirm)",
                "rotate <key>           - Generate new value for a secret",
                "audit                  - Show audit log",
                "stats                  - Show statistics",
                "import [prefix]        - Import from environment variables"
            },
            Examples = new[]
            {
                "loco secrets set api-key \"abc123\" \"Production API key\"",
                "loco secrets set db-password \"secret\" --tags production,database",
                "loco secrets get api-key",
                "loco secrets list",
                "loco secrets delete api-key --confirm",
                "loco secrets rotate api-key",
                "loco secrets import MYAPP_SECRET_"
            },
            Options = new[]
            {
                "--tags tag1,tag2    - Add tags to a secret",
                "--confirm           - Confirm destructive operations"
            }
        };
    }

    public override async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return 1;
        }

        var subCommand = args[0].ToLowerInvariant();

        try
        {
            return subCommand switch
            {
                "set" or "store" => await StoreSecretAsync(args.Skip(1).ToArray()),
                "get" => await GetSecretAsync(args.Skip(1).ToArray()),
                "list" or "ls" => await ListSecretsAsync(args.Skip(1).ToArray()),
                "delete" or "del" or "rm" => await DeleteSecretAsync(args.Skip(1).ToArray()),
                "rotate" => await RotateSecretAsync(args.Skip(1).ToArray()),
                "audit" => await ShowAuditLogAsync(args.Skip(1).ToArray()),
                "stats" => await ShowStatisticsAsync(),
                "import" => await ImportFromEnvAsync(args.Skip(1).ToArray()),
                "help" or "--help" or "-h" => ShowHelp(),
                _ => UnknownCommand(subCommand)
            };
        }
        catch (Exception ex)
        {
            ConsoleUI.FriendlyError("Secrets", ex.Message,
                "Ensure the secret key is valid (alphanumeric, dash, underscore only)\nCheck that you have proper permissions\nVerify the LOCO_MASTER_KEY environment variable is set correctly",
                "シークレットキーが有効であることを確認（英数字、ダッシュ、アンダースコアのみ）\n適切な権限があることを確認\nLOCO_MASTER_KEY環境変数が正しく設定されていることを確認");
            return 1;
        }
    }

    private async Task<int> StoreSecretAsync(string[] args)
    {
        if (args.Length < 2)
        {
            ConsoleUI.Error("Usage: loco secrets set <key> <value> [description] [--tags tag1,tag2]",
                "使用方法: loco secrets set <key> <value> [description] [--tags tag1,tag2]");
            return 1;
        }

        var key = args[0];
        var value = args[1];
        var description = args.Length > 2 && !args[2].StartsWith("--") ? args[2] : null;

        // Parse tags
        string[]? tags = null;
        var tagsIndex = Array.IndexOf(args, "--tags");
        if (tagsIndex >= 0 && tagsIndex + 1 < args.Length)
        {
            tags = args[tagsIndex + 1].Split(',', StringSplitOptions.RemoveEmptyEntries);
        }

        ConsoleUI.Info($"Storing secret: {key}", $"シークレットを保存中: {key}");

        var metadata = tags != null ? new Dictionary<string, string> { ["tags"] = string.Join(",", tags) } : null;
        _secretsManager.StoreSecret(key, value, description, metadata);

        ConsoleUI.Success($"Secret '{key}' stored successfully", $"シークレット '{key}' を正常に保存しました");
        ConsoleUI.Tip("Use 'loco secrets get <key>' to retrieve the secret value", "シークレットの値を取得するには 'loco secrets get <key>' を使用してください");

        return 0;
    }

    private async Task<int> GetSecretAsync(string[] args)
    {
        if (args.Length < 1)
        {
            ConsoleUI.Error("Usage: loco secrets get <key>", "使用方法: loco secrets get <key>");
            return 1;
        }

        var key = args[0];
        var value = _secretsManager.GetSecret(key);

        if (value == null)
        {
            ConsoleUI.Warning($"Secret '{key}' not found", $"シークレット '{key}' が見つかりません");
            return 1;
        }

        // Show value with a warning about sensitive data
        Console.ForegroundColor = ConsoleUI.Colors.Warning;
        Console.WriteLine($"\n{ConsoleUI.Icons.Warning} Sensitive data - handle with care:");
        Console.ResetColor();
        Console.WriteLine(value);
        Console.WriteLine();

        return 0;
    }

    private async Task<int> ListSecretsAsync(string[] args)
    {
        var secrets = _secretsManager.ListSecrets();

        if (secrets.Count == 0)
        {
            ConsoleUI.Info("No secrets found", "シークレットが見つかりません");
            ConsoleUI.Tip("Use 'loco secrets set' to store a secret", "'loco secrets set' でシークレットを保存できます");
            return 0;
        }

        Console.WriteLine($"\nSecrets Vault ({secrets.Count} secrets):\n");

        foreach (var secret in secrets)
        {
            Console.ForegroundColor = ConsoleUI.Colors.Primary;
            Console.Write($"  * ");
            Console.ResetColor();
            Console.Write($"{secret.Key}");

            if (!string.IsNullOrEmpty(secret.Description))
            {
                Console.ForegroundColor = ConsoleUI.Colors.Muted;
                Console.Write($" - {secret.Description}");
                Console.ResetColor();
            }

            Console.WriteLine();

            Console.ForegroundColor = ConsoleUI.Colors.Muted;
            Console.WriteLine($"      Created: {secret.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");

            if (secret.LastAccessedAt.HasValue)
            {
                Console.WriteLine($"      Last accessed: {secret.LastAccessedAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
            }

            // Tags feature not yet fully implemented in SecretMetadata
            // Will be added in future version

            Console.ResetColor();
            Console.WriteLine();
        }

        return 0;
    }

    private async Task<int> DeleteSecretAsync(string[] args)
    {
        if (args.Length < 1)
        {
            ConsoleUI.Error("Usage: loco secrets delete <key>", "使用方法: loco secrets delete <key>");
            return 1;
        }

        var key = args[0];

        // Confirm deletion
        var hasConfirm = args.Contains("--confirm") || args.Contains("-y");
        if (!hasConfirm)
        {
            ConsoleUI.Warning($"This will permanently delete secret '{key}'", $"これはシークレット '{key}' を完全に削除します");
            Console.WriteLine("Use --confirm to proceed");
            return 1;
        }

        var deleted = _secretsManager.DeleteSecret(key);

        if (deleted)
        {
            ConsoleUI.Success($"Secret '{key}' deleted successfully", $"シークレット '{key}' を正常に削除しました");
            return 0;
        }
        else
        {
            ConsoleUI.Warning($"Secret '{key}' not found", $"シークレット '{key}' が見つかりません");
            return 1;
        }
    }

    private async Task<int> RotateSecretAsync(string[] args)
    {
        if (args.Length < 1)
        {
            ConsoleUI.Error("Usage: loco secrets rotate <key>", "使用方法: loco secrets rotate <key>");
            Console.WriteLine("This will generate a new random value for the secret");
            return 1;
        }

        var key = args[0];

        ConsoleUI.Info($"Rotating secret: {key}", $"シークレットをローテーション中: {key}");

        // Simple rotation: generate a new random value
        var newValue = GenerateRandomSecret(32);

        var currentValue = _secretsManager.GetSecret(key);
        if (currentValue == null)
        {
            ConsoleUI.Warning($"Secret '{key}' not found", $"シークレット '{key}' が見つかりません");
            return 1;
        }

        // Update with the new value
        _secretsManager.UpdateSecret(key, newValue);

        ConsoleUI.Success($"Secret '{key}' rotated successfully", $"シークレット '{key}' を正常にローテーションしました");
        ConsoleUI.Warning("Old value:", "古い値:");
        Console.WriteLine($"  {currentValue}");
        ConsoleUI.Info("New value:", "新しい値:");
        Console.WriteLine($"  {newValue}");
        ConsoleUI.Tip("Update any services using this secret with the new value", "このシークレットを使用しているサービスを新しい値で更新してください");

        return 0;
    }

    private async Task<int> ShowAuditLogAsync(string[] args)
    {
        ConsoleUI.Info("Audit log feature not yet fully implemented", "監査ログ機能はまだ完全には実装されていません");
        ConsoleUI.Tip("This will show all access and modification events for secrets", "これはシークレットのすべてのアクセスと変更イベントを表示します");
        return 0;
    }

    private async Task<int> ShowStatisticsAsync()
    {
        var secrets = _secretsManager.ListSecrets();

        Console.WriteLine($"\nSecrets Statistics:\n");

        Console.WriteLine($"  Total secrets: {secrets.Count}");

        if (secrets.Count > 0)
        {
            var withDescription = secrets.Count(s => !string.IsNullOrEmpty(s.Description));

            Console.WriteLine($"  Secrets with description: {withDescription}");

            var oldestSecret = secrets.OrderBy(s => s.CreatedAt).FirstOrDefault();
            var newestSecret = secrets.OrderByDescending(s => s.CreatedAt).FirstOrDefault();

            if (oldestSecret != null)
            {
                Console.WriteLine($"\n  Oldest secret: {oldestSecret.Key}");
                Console.WriteLine($"    Created: {oldestSecret.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            }

            if (newestSecret != null)
            {
                Console.WriteLine($"\n  Newest secret: {newestSecret.Key}");
                Console.WriteLine($"    Created: {newestSecret.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            }

            var recentlyAccessed = secrets
                .Where(s => s.LastAccessedAt.HasValue)
                .OrderByDescending(s => s.LastAccessedAt)
                .Take(5)
                .ToList();

            if (recentlyAccessed.Any())
            {
                Console.WriteLine($"\n  Recently accessed secrets:");
                foreach (var secret in recentlyAccessed)
                {
                    Console.WriteLine($"    {secret.Key} - {secret.LastAccessedAt:yyyy-MM-dd HH:mm:ss} UTC");
                }
            }
        }

        Console.WriteLine();
        return 0;
    }

    private async Task<int> ImportFromEnvAsync(string[] args)
    {
        var prefix = args.Length > 0 ? args[0] : "LOCO_SECRET_";

        ConsoleUI.Info($"Importing secrets from environment variables with prefix: {prefix}",
            $"プレフィックス付き環境変数からシークレットをインポート中: {prefix}");

        // Import from environment variables manually
        var envVars = Environment.GetEnvironmentVariables();
        var imported = 0;

        foreach (var key in envVars.Keys)
        {
            var keyStr = key?.ToString();
            if (string.IsNullOrEmpty(keyStr) || !keyStr.StartsWith(prefix))
                continue;

            var secretKey = keyStr.Substring(prefix.Length);
            var value = envVars[key]?.ToString();

            if (string.IsNullOrEmpty(value))
                continue;

            _secretsManager.StoreSecret(secretKey, value, $"Imported from environment variable {keyStr}");
            imported++;
        }

        ConsoleUI.Info($"Imported {imported} secrets", $"{imported}個のシークレットをインポートしました");

        ConsoleUI.Success("Import completed", "インポートが完了しました");
        ConsoleUI.Tip($"Use 'loco secrets list' to see imported secrets", "インポートされたシークレットを確認するには 'loco secrets list' を使用してください");

        return 0;
    }

    private string GenerateRandomSecret(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private int ShowHelp()
    {
        Console.WriteLine("\nLoco Secrets Management");
        Console.WriteLine("=======================\n");

        Console.WriteLine("Usage: loco secrets <command> [options]\n");

        Console.WriteLine("Commands:");
        Console.WriteLine("  set <key> <value>      Store a new secret");
        Console.WriteLine("  get <key>              Retrieve a secret value");
        Console.WriteLine("  list                   List all secrets (metadata only)");
        Console.WriteLine("  delete <key>           Delete a secret (requires --confirm)");
        Console.WriteLine("  rotate <key>           Generate new value for a secret");
        Console.WriteLine("  audit                  Show audit log");
        Console.WriteLine("  stats                  Show statistics");
        Console.WriteLine("  import [prefix]        Import from environment variables");
        Console.WriteLine("  help                   Show this help message");

        Console.WriteLine("\nExamples:");
        Console.WriteLine("  loco secrets set api-key \"abc123\" \"Production API key\"");
        Console.WriteLine("  loco secrets set db-password \"secret\" --tags production,database");
        Console.WriteLine("  loco secrets get api-key");
        Console.WriteLine("  loco secrets list");
        Console.WriteLine("  loco secrets delete api-key --confirm");
        Console.WriteLine("  loco secrets rotate api-key");
        Console.WriteLine("  loco secrets import MYAPP_SECRET_");

        Console.WriteLine("\nNotes:");
        Console.WriteLine("  - All secrets are encrypted using AES-256");
        Console.WriteLine("  - Set LOCO_MASTER_KEY environment variable for persistent encryption");
        Console.WriteLine("  - Secret keys must contain only alphanumeric characters, dashes, and underscores");

        Console.WriteLine();
        return 0;
    }

    private int UnknownCommand(string command)
    {
        ConsoleUI.Error($"Unknown secrets command: {command}", $"不明なsecretsコマンド: {command}");
        Console.WriteLine("Run 'loco secrets help' for usage information");
        return 1;
    }
}
