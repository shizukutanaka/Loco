using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Loco.Core.Configuration;
using Loco.Cli.UI;

namespace Loco.Cli.Commands;

/// <summary>
/// BackupConfigCommand - Manage configuration backups
/// 市販レベル: 設定バックアップ管理機能
/// </summary>
public static class BackupConfigCommand
{
    public static async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            ShowUsage();
            return 1;
        }

        var subCommand = args[0].ToLowerInvariant();

        try
        {
            var backup = new ConfigBackup(maxBackups: 10);

            switch (subCommand)
            {
                case "create":
                    {
                        var description = args.Length > 1 ? string.Join(" ", args.Skip(1)) : "Manual backup";

                        Console.WriteLine("📦 Creating configuration backup...");
                        Console.WriteLine("📦 設定バックアップを作成中...");
                        Console.WriteLine();

                        var backupPath = await backup.CreateBackupAsync(description).ConfigureAwait(false);

                        if (backupPath == null)
                        {
                            ConsoleUI.Error("Failed to create backup. Configuration directory may not exist.",
                                          "バックアップの作成に失敗しました。設定ディレクトリが存在しない可能性があります。");
                            return 1;
                        }

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ Backup created successfully");
                        Console.WriteLine($"✓ バックアップが正常に作成されました");
                        Console.ResetColor();
                        Console.WriteLine();
                        Console.WriteLine($"Location / 場所:");
                        Console.WriteLine($"  {backupPath}");
                        Console.WriteLine();
                        Console.WriteLine($"Size / サイズ: {new FileInfo(backupPath).Length / 1024.0:F1} KB");

                        return 0;
                    }

                case "list":
                    {
                        Console.WriteLine("📋 Available Configuration Backups");
                        Console.WriteLine("📋 利用可能な設定バックアップ");
                        Console.WriteLine();

                        var backups = await backup.ListBackupsAsync().ConfigureAwait(false);

                        if (backups.Count == 0)
                        {
                            ConsoleUI.Info("No backups found. Use 'backup-config create' to create your first backup.",
                                         "バックアップが見つかりません。'backup-config create' で最初のバックアップを作成してください。");
                            return 0;
                        }

                        Console.WriteLine($"Total backups: {backups.Count}");
                        Console.WriteLine($"合計バックアップ数: {backups.Count}");
                        Console.WriteLine();

                        var headers = new[] { "#", "File", "Created", "Size", "Files", "Description" };
                        var rows = backups
                            .Select((b, index) => new[]
                            {
                                (index + 1).ToString(),
                                Path.GetFileName(b.FileName),
                                b.CreatedTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                                b.FormattedSize,
                                b.FileCount.ToString(),
                                b.Description.Length > 30 ? b.Description.Substring(0, 27) + "..." : b.Description
                            })
                            .ToList();

                        ConsoleUI.ShowTable(headers, rows);

                        Console.WriteLine();
                        Console.WriteLine("Tip: Use 'backup-config restore <number>' to restore a backup");
                        Console.WriteLine("ヒント: 'backup-config restore <番号>' でバックアップを復元できます");

                        return 0;
                    }

                case "restore":
                    {
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: backup-config restore <backup_number>");
                            Console.WriteLine("Use 'backup-config list' to see available backups.");
                            return 1;
                        }

                        var backups = await backup.ListBackupsAsync().ConfigureAwait(false);

                        if (backups.Count == 0)
                        {
                            ConsoleUI.Error("No backups available to restore.",
                                          "復元可能なバックアップがありません。");
                            return 1;
                        }

                        if (!int.TryParse(args[1], out var backupIndex) || backupIndex < 1 || backupIndex > backups.Count)
                        {
                            ConsoleUI.Error($"Invalid backup number. Please choose 1-{backups.Count}",
                                          $"無効なバックアップ番号。1-{backups.Count} を選択してください");
                            return 1;
                        }

                        var selectedBackup = backups[backupIndex - 1];

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("⚠️  Warning: This will replace your current configuration!");
                        Console.WriteLine("⚠️  警告: 現在の設定が置き換えられます!");
                        Console.ResetColor();
                        Console.WriteLine();
                        Console.WriteLine($"Restore from: {selectedBackup.FileName}");
                        Console.WriteLine($"復元元: {selectedBackup.FileName}");
                        Console.WriteLine($"Created: {selectedBackup.CreatedTime.ToLocalTime():yyyy-MM-dd HH:mm}");
                        Console.WriteLine($"作成日時: {selectedBackup.CreatedTime.ToLocalTime():yyyy-MM-dd HH:mm}");
                        Console.WriteLine($"Description: {selectedBackup.Description}");
                        Console.WriteLine($"説明: {selectedBackup.Description}");
                        Console.WriteLine();

                        Console.Write("Type 'yes' to confirm: ");
                        var confirmation = Console.ReadLine();

                        if (!confirmation?.Equals("yes", StringComparison.OrdinalIgnoreCase) ?? true)
                        {
                            Console.WriteLine("Restore cancelled.");
                            Console.WriteLine("復元がキャンセルされました。");
                            return 0;
                        }

                        Console.WriteLine();
                        Console.WriteLine("🔄 Restoring configuration...");
                        Console.WriteLine("🔄 設定を復元中...");

                        var success = await backup.RestoreBackupAsync(selectedBackup.FilePath).ConfigureAwait(false);

                        if (!success)
                        {
                            ConsoleUI.Error("Failed to restore backup.",
                                          "バックアップの復元に失敗しました。");
                            return 1;
                        }

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ Configuration restored successfully");
                        Console.WriteLine($"✓ 設定が正常に復元されました");
                        Console.ResetColor();
                        Console.WriteLine();
                        Console.WriteLine("Note: Restart Loco for changes to take effect.");
                        Console.WriteLine("注意: 変更を反映するには Loco を再起動してください。");

                        return 0;
                    }

                case "delete":
                    {
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: backup-config delete <backup_number>");
                            Console.WriteLine("Use 'backup-config list' to see available backups.");
                            return 1;
                        }

                        var backups = await backup.ListBackupsAsync().ConfigureAwait(false);

                        if (backups.Count == 0)
                        {
                            ConsoleUI.Error("No backups available to delete.",
                                          "削除可能なバックアップがありません。");
                            return 1;
                        }

                        if (!int.TryParse(args[1], out var backupIndex) || backupIndex < 1 || backupIndex > backups.Count)
                        {
                            ConsoleUI.Error($"Invalid backup number. Please choose 1-{backups.Count}",
                                          $"無効なバックアップ番号。1-{backups.Count} を選択してください");
                            return 1;
                        }

                        var selectedBackup = backups[backupIndex - 1];
                        var deleted = backup.DeleteBackup(selectedBackup.FilePath);

                        if (deleted)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"✓ Backup deleted: {selectedBackup.FileName}");
                            Console.WriteLine($"✓ バックアップを削除しました: {selectedBackup.FileName}");
                            Console.ResetColor();
                            return 0;
                        }

                        ConsoleUI.Error("Failed to delete backup.",
                                      "バックアップの削除に失敗しました。");
                        return 1;
                    }

                case "clear":
                    {
                        var backups = await backup.ListBackupsAsync().ConfigureAwait(false);

                        if (backups.Count == 0)
                        {
                            ConsoleUI.Info("No backups to clear.",
                                         "削除するバックアップがありません。");
                            return 0;
                        }

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"⚠️  This will delete all {backups.Count} backup(s)!");
                        Console.WriteLine($"⚠️  すべての {backups.Count} 個のバックアップが削除されます!");
                        Console.ResetColor();
                        Console.Write("Type 'yes' to confirm: ");

                        var confirmation = Console.ReadLine();

                        if (!confirmation?.Equals("yes", StringComparison.OrdinalIgnoreCase) ?? true)
                        {
                            Console.WriteLine("Clear cancelled.");
                            Console.WriteLine("削除がキャンセルされました。");
                            return 0;
                        }

                        var deletedCount = backup.DeleteAllBackups();

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ Deleted {deletedCount} backup(s)");
                        Console.WriteLine($"✓ {deletedCount} 個のバックアップを削除しました");
                        Console.ResetColor();

                        return 0;
                    }

                case "auto":
                    {
                        Console.WriteLine("🔍 Checking if automatic backup is needed...");
                        Console.WriteLine("🔍 自動バックアップが必要か確認中...");

                        var created = await backup.CreateAutoBackupIfNeededAsync().ConfigureAwait(false);

                        if (created)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("✓ Automatic backup created (24h interval exceeded)");
                            Console.WriteLine("✓ 自動バックアップを作成しました (24時間経過)");
                            Console.ResetColor();
                            return 0;
                        }

                        ConsoleUI.Info("No automatic backup needed (recent backup exists).",
                                     "自動バックアップは不要です (最近のバックアップが存在します)。");
                        return 0;
                    }

                default:
                    ShowUsage();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            ConsoleUI.Error($"Backup operation failed: {ex.Message}",
                          $"バックアップ操作が失敗しました: {ex.Message}");
            return 1;
        }
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage: loco backup-config <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  create [description]  - Create a new configuration backup");
        Console.WriteLine("  list                  - List all available backups");
        Console.WriteLine("  restore <number>      - Restore a configuration from backup");
        Console.WriteLine("  delete <number>       - Delete a specific backup");
        Console.WriteLine("  clear                 - Delete all backups");
        Console.WriteLine("  auto                  - Create automatic backup if needed (24h interval)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  loco backup-config create \"Before major changes\"");
        Console.WriteLine("  loco backup-config list");
        Console.WriteLine("  loco backup-config restore 1");
    }
}
