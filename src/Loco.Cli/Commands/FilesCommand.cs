using System.CommandLine;

namespace Loco.Cli.Commands;

/// <summary>
/// File operations and search functionality
/// </summary>
public class FilesCommand : Command
{
    public FilesCommand() : base("files", "File operations and search")
    {
        // Search subcommand
        var searchCommand = new Command("search", "Search for files by pattern");
        var patternArg = new Argument<string>("pattern", "File pattern to search for (e.g., *.txt)");
        var directoryArg = new Argument<string?>("directory", () => ".", "Directory to search in");
        searchCommand.AddArgument(patternArg);
        searchCommand.AddArgument(directoryArg);
        searchCommand.SetHandler((pattern, directory) => SearchFiles(pattern, directory ?? "."), patternArg, directoryArg);
        AddCommand(searchCommand);

        // Stats subcommand
        var statsCommand = new Command("stats", "Show directory statistics");
        var statsDirectoryArg = new Argument<string?>("directory", () => ".", "Directory to analyze");
        statsCommand.AddArgument(statsDirectoryArg);
        statsCommand.SetHandler((directory) => ShowStats(directory ?? "."), statsDirectoryArg);
        AddCommand(statsCommand);

        // Clean subcommand
        var cleanCommand = new Command("clean", "Clean temporary and cache files");
        var cleanDirectoryArg = new Argument<string?>("directory", () => ".", "Directory to clean");
        var dryRunOption = new Option<bool>("--dry-run", "Show what would be deleted without deleting");
        cleanCommand.AddArgument(cleanDirectoryArg);
        cleanCommand.AddOption(dryRunOption);
        cleanCommand.SetHandler((directory, dryRun) => CleanFiles(directory ?? ".", dryRun), cleanDirectoryArg, dryRunOption);
        AddCommand(cleanCommand);

        // Organize subcommand
        var organizeCommand = new Command("organize", "Organize files by type or date");
        var organizeDirectoryArg = new Argument<string?>("directory", () => ".", "Directory to organize");
        var byTypeOption = new Option<bool>("--by-type", "Organize by file type");
        var byDateOption = new Option<bool>("--by-date", "Organize by date");
        organizeCommand.AddArgument(organizeDirectoryArg);
        organizeCommand.AddOption(byTypeOption);
        organizeCommand.AddOption(byDateOption);
        organizeCommand.SetHandler((directory, byType, byDate) => OrganizeFiles(directory ?? ".", byType, byDate),
            organizeDirectoryArg, byTypeOption, byDateOption);
        AddCommand(organizeCommand);
    }

    public async Task<int> InvokeAsync(string[] args)
    {
        return await ((Command)this).InvokeAsync(args);
    }

    private int SearchFiles(string pattern, string directory)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("File Search");
        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();

        if (!Directory.Exists(directory))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Directory not found: {directory}");
            Console.ResetColor();
            return 1;
        }

        var fullPath = Path.GetFullPath(directory);
        Console.WriteLine($"Pattern: {pattern}");
        Console.WriteLine($"Directory: {fullPath}");
        Console.WriteLine();

        try
        {
            var files = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);

            if (files.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ No files found matching the pattern");
                Console.ResetColor();
                return 0;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Found {files.Length} file(s):");
            Console.ResetColor();
            Console.WriteLine();

            var displayCount = Math.Min(files.Length, 50);
            for (int i = 0; i < displayCount; i++)
            {
                var file = files[i];
                var info = new FileInfo(file);
                var relativePath = Path.GetRelativePath(directory, file);

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [{i + 1,3}] ");
                Console.ResetColor();
                Console.Write(relativePath);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($" ({FormatBytes(info.Length)}, {info.LastWriteTime:yyyy-MM-dd HH:mm})");
                Console.ResetColor();
            }

            if (files.Length > 50)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  ... and {files.Length - 50} more file(s)");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine($"Total: {files.Length} file(s)");
            return 0;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Access denied: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Search failed: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private int ShowStats(string directory)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Directory Statistics");
        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();

        if (!Directory.Exists(directory))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Directory not found: {directory}");
            Console.ResetColor();
            return 1;
        }

        var fullPath = Path.GetFullPath(directory);
        Console.WriteLine($"Directory: {fullPath}");
        Console.WriteLine();

        try
        {
            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            var directories = Directory.GetDirectories(directory, "*", SearchOption.AllDirectories);

            long totalSize = 0;
            var extensionStats = new Dictionary<string, (int count, long size)>();

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                totalSize += info.Length;

                var ext = info.Extension.ToLower();
                if (string.IsNullOrEmpty(ext))
                    ext = "(no extension)";

                if (extensionStats.ContainsKey(ext))
                {
                    var (count, size) = extensionStats[ext];
                    extensionStats[ext] = (count + 1, size + info.Length);
                }
                else
                {
                    extensionStats[ext] = (1, info.Length);
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Summary:");
            Console.ResetColor();
            Console.WriteLine($"  Files: {files.Length:N0}");
            Console.WriteLine($"  Directories: {directories.Length:N0}");
            Console.WriteLine($"  Total Size: {FormatBytes(totalSize)}");
            Console.WriteLine();

            if (extensionStats.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("File Types:");
                Console.ResetColor();

                var topExtensions = extensionStats
                    .OrderByDescending(x => x.Value.size)
                    .Take(10);

                foreach (var (ext, (count, size)) in topExtensions)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"  {ext,-15}");
                    Console.ResetColor();
                    Console.WriteLine($" {count,6:N0} file(s)  {FormatBytes(size),10}");
                }

                if (extensionStats.Count > 10)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  ... and {extensionStats.Count - 10} more type(s)");
                    Console.ResetColor();
                }
            }

            Console.WriteLine();
            return 0;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Access denied: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Stats failed: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private int CleanFiles(string directory, bool dryRun)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(dryRun ? "Clean Files (Dry Run)" : "Clean Files");
        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();

        if (!Directory.Exists(directory))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Directory not found: {directory}");
            Console.ResetColor();
            return 1;
        }

        var fullPath = Path.GetFullPath(directory);
        Console.WriteLine($"Directory: {fullPath}");
        if (dryRun)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Mode: Dry run (no files will be deleted)");
            Console.ResetColor();
        }
        Console.WriteLine();

        try
        {
            // Patterns for common temporary/cache files. Deliberately excludes
            // "*.log": recursively deleting every .log under a directory would
            // wipe live application/user logs, which are not temp/cache files.
            var patterns = new[] { "*.tmp", "*.temp", "*.cache", "*.bak", "~*" };
            var filesToClean = new List<string>();

            foreach (var pattern in patterns)
            {
                var files = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
                filesToClean.AddRange(files);
            }

            if (filesToClean.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ No temporary files found");
                Console.ResetColor();
                return 0;
            }

            long totalSize = 0;
            Console.WriteLine($"Found {filesToClean.Count} file(s) to clean:");
            Console.WriteLine();

            foreach (var file in filesToClean.Take(20))
            {
                var info = new FileInfo(file);
                totalSize += info.Length;
                var relativePath = Path.GetRelativePath(directory, file);

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("  • ");
                Console.ResetColor();
                Console.WriteLine($"{relativePath} ({FormatBytes(info.Length)})");
            }

            if (filesToClean.Count > 20)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  ... and {filesToClean.Count - 20} more file(s)");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine($"Total size to free: {FormatBytes(totalSize)}");
            Console.WriteLine();

            if (dryRun)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ Dry run complete. No files were deleted.");
                Console.WriteLine("Run without --dry-run to actually delete files.");
                Console.ResetColor();
            }
            else
            {
                Console.Write("Delete these files? (y/N): ");
                var response = Console.ReadLine();
                if (response?.ToLower() == "y")
                {
                    int deleted = 0;
                    foreach (var file in filesToClean)
                    {
                        try
                        {
                            File.Delete(file);
                            deleted++;
                        }
                        catch
                        {
                            // Skip files we can't delete
                        }
                    }

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ Deleted {deleted} file(s), freed {FormatBytes(totalSize)}");
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine("Cleanup cancelled.");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Clean failed: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private int OrganizeFiles(string directory, bool byType, bool byDate)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Organize Files");
        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();

        if (!byType && !byDate)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠ Please specify organization method:");
            Console.ResetColor();
            Console.WriteLine("  --by-type   Organize by file type");
            Console.WriteLine("  --by-date   Organize by date");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  Loco.Cli.exe files organize --by-type");
            return 1;
        }

        if (!Directory.Exists(directory))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Directory not found: {directory}");
            Console.ResetColor();
            return 1;
        }

        var fullPath = Path.GetFullPath(directory);
        Console.WriteLine($"Directory: {fullPath}");
        Console.WriteLine($"Method: {(byType ? "By Type" : "By Date")}");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠ File organization is not yet implemented");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("This feature will:");
        if (byType)
        {
            Console.WriteLine("  • Create folders for each file type (Documents, Images, Videos, etc.)");
            Console.WriteLine("  • Move files to appropriate type-based folders");
        }
        else
        {
            Console.WriteLine("  • Create folders by year and month");
            Console.WriteLine("  • Move files to date-based folders based on creation/modification date");
        }
        Console.WriteLine();
        Console.WriteLine("Use these commands instead:");
        Console.WriteLine("  • Loco.Cli.exe files search <pattern> - Find files");
        Console.WriteLine("  • Loco.Cli.exe files stats - Analyze directory");
        Console.WriteLine();

        return 1;
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:N1} {suffixes[counter]}";
    }
}
