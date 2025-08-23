using System;
using System.CommandLine;

namespace Loco.Cli.Commands;

public class VersionCommand : Command
{
    public VersionCommand()
        : base("version", "バージョン情報を表示")
    {
        this.SetHandler(() =>
        {
            Console.WriteLine("Loco CLI");
            Console.WriteLine("Version: 0.0.1 (2025/08/14)");
            Console.WriteLine("Copyright (c) 2025 Loco Team");
            Console.WriteLine();
            Console.WriteLine("Features:");
            Console.WriteLine("  - フロービルダー");
            Console.WriteLine("  - 自然言語からフロー生成");
            Console.WriteLine("  - 主要コンポーネント");
        });
    }
}
