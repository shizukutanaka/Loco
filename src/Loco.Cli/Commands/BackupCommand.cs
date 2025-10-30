using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;
// using Loco.Core.Backup; // TODO: Re-enable when UnifiedBackupSystem is implemented

namespace Loco.Cli.Commands;

/// <summary>
/// バックアップコマンド
/// Backup command
/// NOTE: Currently disabled - requires UnifiedBackupSystem implementation
/// </summary>
public static class BackupCommand
{
    public static Command Create()
    {
        var command = new Command("backup", "Backup and restore operations (Not yet implemented) / バックアップと復元操作（未実装）");

        command.SetHandler(() =>
        {
            Console.WriteLine("⚠️  Backup functionality is not yet implemented.");
            Console.WriteLine("バックアップ機能はまだ実装されていません。");
            return Task.CompletedTask;
        });

        return command;
    }

    #if false  // Commented out until UnifiedBackupSystem is implemented
    public static Command CreateOLD()
    {
        var command = new Command("backup", "Backup and restore operations / バックアップと復元操作")
        {
            CreateListCommand(),
            CreateCreateCommand(),
            CreateRestoreCommand(),
            CreateDeleteCommand(),
            CreateAutoCommand()
        };

        return command;
    }

    private static Command CreateListCommand()
    {
        var command = new Command("list", "List all backups / すべてのバックアップを一覧表示");

        command.SetHandler(async () =>
        {
            var backupSystem = new UnifiedBackupSystem(GetBackupDirectory());
            var backups = await backupSystem.ListBackupsAsync();

            if (backups.Count == 0)
            {
                Console.WriteLine("No backups found / バックアップが見つかりません");
                return;
            }

            Console.WriteLine("\n╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Backup List / バックアップ一覧                               ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");

            foreach (var backup in backups)
            {
                var sizeKb = backup.SizeBytes / 1024.0;
                var sizeDisplay = sizeKb > 1024
                    ? $"{sizeKb / 1024:F2} MB"
                    : $"{sizeKb:F2} KB";

                Console.WriteLine($"ID: {backup.Id}");
                Console.WriteLine($"Created: {backup.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Type: {backup.Type}");
                Console.WriteLine($"Size: {sizeDisplay}");
                Console.WriteLine($"Files: {backup.FileCount}");
                Console.WriteLine($"Description: {backup.Description}");

                if (backup.IsCompressed)
                    Console.WriteLine("  ✓ Compressed");
                if (backup.IsEncrypted)
                    Console.WriteLine("  ✓ Encrypted");

                Console.WriteLine();
            }
        });

        return command;
    }

    private static Command CreateCreateCommand()
    {
        var command = new Command("create", "Create a new backup / 新しいバックアップを作成");

        var pathsOption = new Option<string[]>(
            aliases: new[] { "--paths", "-p" },
            description: "Paths to backup / バックアップするパス"
        ) { IsRequired = true, AllowMultipleArgumentsPerToken = true };

        var descOption = new Option<string>(
            aliases: new[] { "--description", "-d" },
            description: "Backup description / バックアップの説明",
            getDefaultValue: () => $"Manual backup {DateTime.Now:yyyy-MM-dd HH:mm}"
        );

        var compressOption = new Option<bool>(
            aliases: new[] { "--compress", "-c" },
            description: "Compress backup / バックアップを圧縮",
            getDefaultValue: () => true
        );

        command.AddOption(pathsOption);
        command.AddOption(descOption);
        command.AddOption(compressOption);

        command.SetHandler(async (string[] paths, string description, bool compress) =>
        {
            Console.WriteLine("\n🔄 Creating backup... / バックアップを作成中...\n");

            var backupSystem = new UnifiedBackupSystem(GetBackupDirectory());
            var result = await backupSystem.CreateFullBackupAsync(
                paths.ToList(),
                description,
                compress,
                encrypt: false
            );

            if (result.Success)
            {
                Console.WriteLine("✅ Backup created successfully! / バックアップが正常に作成されました！\n");
                Console.WriteLine($"Backup ID: {result.BackupId}");
                Console.WriteLine($"Files backed up: {result.FilesBackedUp}");
                Console.WriteLine($"Total size: {result.TotalBytes / 1024.0:F2} KB");
                Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F2}s");
            }
            else
            {
                Console.WriteLine($"❌ Backup failed: {result.ErrorMessage}");
            }
        }, pathsOption, descOption, compressOption);

        return command;
    }

    private static Command CreateRestoreCommand()
    {
        var command = new Command("restore", "Restore from backup / バックアップから復元");

        var idOption = new Option<string>(
            aliases: new[] { "--id", "-i" },
            description: "Backup ID / バックアップID"
        ) { IsRequired = true };

        var targetOption = new Option<string?>(
            aliases: new[] { "--target", "-t" },
            description: "Target directory / ターゲットディレクトリ (optional)"
        );

        var overwriteOption = new Option<bool>(
            aliases: new[] { "--overwrite", "-o" },
            description: "Overwrite existing files / 既存ファイルを上書き",
            getDefaultValue: () => false
        );

        command.AddOption(idOption);
        command.AddOption(targetOption);
        command.AddOption(overwriteOption);

        command.SetHandler(async (string id, string? target, bool overwrite) =>
        {
            Console.WriteLine("\n🔄 Restoring backup... / バックアップを復元中...\n");

            var backupSystem = new UnifiedBackupSystem(GetBackupDirectory());
            var result = await backupSystem.RestoreBackupAsync(id, target, overwrite);

            if (result.Success)
            {
                Console.WriteLine("✅ Restore completed! / 復元が完了しました！\n");
                Console.WriteLine($"Files restored: {result.FilesRestored}");
                Console.WriteLine($"Files skipped: {result.FilesSkipped}");
                Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F2}s");

                if (result.Errors.Count > 0)
                {
                    Console.WriteLine("\n⚠️ Errors:");
                    foreach (var error in result.Errors.Take(5))
                    {
                        Console.WriteLine($"  - {error}");
                    }
                    if (result.Errors.Count > 5)
                    {
                        Console.WriteLine($"  ... and {result.Errors.Count - 5} more");
                    }
                }
            }
            else
            {
                Console.WriteLine("❌ Restore failed:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }
        }, idOption, targetOption, overwriteOption);

        return command;
    }

    private static Command CreateDeleteCommand()
    {
        var command = new Command("delete", "Delete a backup / バックアップを削除");

        var idOption = new Option<string>(
            aliases: new[] { "--id", "-i" },
            description: "Backup ID / バックアップID"
        ) { IsRequired = true };

        command.AddOption(idOption);

        command.SetHandler(async (string id) =>
        {
            Console.WriteLine($"\n🗑️  Deleting backup {id}...\n");

            var backupSystem = new UnifiedBackupSystem(GetBackupDirectory());
            var success = await backupSystem.DeleteBackupAsync(id);

            if (success)
            {
                Console.WriteLine("✅ Backup deleted successfully! / バックアップが正常に削除されました！");
            }
            else
            {
                Console.WriteLine("❌ Failed to delete backup / バックアップの削除に失敗しました");
            }
        }, idOption);

        return command;
    }

    private static Command CreateAutoCommand()
    {
        var command = new Command("auto", "Automatic backup / 自動バックアップ");

        var pathsOption = new Option<string[]>(
            aliases: new[] { "--paths", "-p" },
            description: "Paths to backup / バックアップするパス"
        ) { IsRequired = true, AllowMultipleArgumentsPerToken = true };

        var scheduleOption = new Option<string>(
            aliases: new[] { "--schedule", "-s" },
            description: "Schedule: daily, weekly, monthly",
            getDefaultValue: () => "daily"
        );

        command.AddOption(pathsOption);
        command.AddOption(scheduleOption);

        command.SetHandler(async (string[] paths, string schedule) =>
        {
            Console.WriteLine($"\n📅 Running {schedule} auto backup...\n");

            var backupSystem = new UnifiedBackupSystem(GetBackupDirectory());
            var result = await backupSystem.AutoBackupAsync(paths.ToList(), schedule);

            if (result.Success)
            {
                Console.WriteLine("✅ Auto backup completed!");
                Console.WriteLine($"Backup ID: {result.BackupId}");
                Console.WriteLine($"Files: {result.FilesBackedUp}");
                Console.WriteLine($"Size: {result.TotalBytes / 1024.0:F2} KB");
            }
            else
            {
                Console.WriteLine($"❌ Auto backup failed: {result.ErrorMessage}");
            }
        }, pathsOption, scheduleOption);

        return command;
    }

    private static string GetBackupDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Loco", "Backups");
    }
    #endif
}
