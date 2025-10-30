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
}
