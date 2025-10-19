using System.CommandLine;
using Loco.Core;
using Loco.Core.Configuration;

namespace Loco.Cli.Commands;

/// <summary>
/// Log management and viewing
/// </summary>
public class LogsCommand : Command
{
    public LogsCommand() : base("logs", "Log management and viewing")
    {
        // View subcommand
        var viewCommand = new Command("view", "View recent log entries");
        var linesArg = new Argument<int>("lines", () => 50, "Number of recent lines to display");
        viewCommand.AddArgument(linesArg);
        viewCommand.SetHandler((lines) => ViewLogs(lines), linesArg);
        AddCommand(viewCommand);

        // Stats subcommand
        var statsCommand = new Command("stats", "Show log statistics");
        statsCommand.SetHandler(() => ShowStats());
        AddCommand(statsCommand);

        // Search subcommand
        var searchCommand = new Command("search", "Search logs for a pattern");
        var patternArg = new Argument<string>("pattern", "Search pattern");
        var maxResultsArg = new Argument<int>("max-results", () => 100, "Maximum number of results");
        searchCommand.AddArgument(patternArg);
        searchCommand.AddArgument(maxResultsArg);
        searchCommand.SetHandler((pattern, max) => SearchLogs(pattern, max), patternArg, maxResultsArg);
        AddCommand(searchCommand);

        // Clear subcommand
        var clearCommand = new Command("clear", "Clear old log files");
        var daysArg = new Argument<int>("days", () => 30, "Delete logs older than this many days");
        var forceOption = new Option<bool>("--force", "Skip confirmation prompt");
        clearCommand.AddArgument(daysArg);
        clearCommand.AddOption(forceOption);
        clearCommand.SetHandler((days, force) => ClearLogs(days, force), daysArg, forceOption);
        AddCommand(clearCommand);
    }

    public async Task<int> InvokeAsync(string[] args)
    {
        return await ((Command)this).InvokeAsync(args);
    }

    private int ViewLogs(int lines)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Log Viewer");
        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();

        try
        {
            var config = new LocoConfig();
            var logDir = config.LogDirectory;

            if (!Directory.Exists(logDir))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ No log directory found");
                Console.ResetColor();
                Console.WriteLine($"Expected location: {logDir}");
                return 0;
            }

            var logFiles = Directory.GetFiles(logDir, "*.log")
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .ToArray();

            if (logFiles.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ No log files found");
                Console.ResetColor();
                return 0;
            }

            var mostRecentLog = logFiles[0];
            Console.WriteLine($"File: {Path.GetFileName(mostRecentLog)}");
            Console.WriteLine($"Lines: Last {lines} entries");
            Console.WriteLine();

            var allLines = File.ReadAllLines(mostRecentLog);
            var recentLines = allLines.TakeLast(lines);

            foreach (var line in recentLines)
            {
                // Colorize log levels
                if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("[ERR]"))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(line);
                    Console.ResetColor();
                }
                else if (line.Contains("WARN", StringComparison.OrdinalIgnoreCase) || line.Contains("[WRN]"))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(line);
                    Console.ResetColor();
                }
                else if (line.Contains("INFO", StringComparison.OrdinalIgnoreCase) || line.Contains("[INF]"))
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(line);
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine(line);
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Showing {recentLines.Count()} of {allLines.Length} total lines");
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ View failed: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private int ShowStats()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Log Statistics");
        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();

        try
        {
            var config = new LocoConfig();
            var logDir = config.LogDirectory;

            if (!Directory.Exists(logDir))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ No log directory found");
                Console.ResetColor();
                Console.WriteLine($"Expected location: {logDir}");
                return 0;
            }

            var logFiles = Directory.GetFiles(logDir, "*.log");

            if (logFiles.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ No log files found");
                Console.ResetColor();
                return 0;
            }

            Console.WriteLine($"Directory: {logDir}");
            Console.WriteLine($"Files: {logFiles.Length}");
            Console.WriteLine();

            long totalSize = 0;
            int totalLines = 0;
            var logLevels = new Dictionary<string, int>
            {
                ["ERROR"] = 0,
                ["WARNING"] = 0,
                ["INFO"] = 0,
                ["DEBUG"] = 0
            };

            foreach (var file in logFiles)
            {
                var info = new FileInfo(file);
                totalSize += info.Length;

                var fileLines = File.ReadAllLines(file);
                totalLines += fileLines.Length;

                foreach (var line in fileLines)
                {
                    if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("[ERR]"))
                        logLevels["ERROR"]++;
                    else if (line.Contains("WARN", StringComparison.OrdinalIgnoreCase) || line.Contains("[WRN]"))
                        logLevels["WARNING"]++;
                    else if (line.Contains("INFO", StringComparison.OrdinalIgnoreCase) || line.Contains("[INF]"))
                        logLevels["INFO"]++;
                    else if (line.Contains("DEBUG", StringComparison.OrdinalIgnoreCase) || line.Contains("[DBG]"))
                        logLevels["DEBUG"]++;
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Summary:");
            Console.ResetColor();
            Console.WriteLine($"  Total Size: {FormatBytes(totalSize)}");
            Console.WriteLine($"  Total Lines: {totalLines:N0}");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Log Levels:");
            Console.ResetColor();
            foreach (var level in logLevels.OrderByDescending(x => x.Value))
            {
                var color = level.Key switch
                {
                    "ERROR" => ConsoleColor.Red,
                    "WARNING" => ConsoleColor.Yellow,
                    "INFO" => ConsoleColor.Cyan,
                    _ => ConsoleColor.Gray
                };

                Console.ForegroundColor = color;
                Console.Write($"  {level.Key,-10}");
                Console.ResetColor();
                Console.WriteLine($" {level.Value,10:N0}");
            }

            Console.WriteLine();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Stats failed: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private int SearchLogs(string pattern, int maxResults)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Log Search");
        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();

        try
        {
            var config = new LocoConfig();
            var logDir = config.LogDirectory;

            if (!Directory.Exists(logDir))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ No log directory found");
                Console.ResetColor();
                Console.WriteLine($"Expected location: {logDir}");
                return 0;
            }

            Console.WriteLine($"Pattern: {pattern}");
            Console.WriteLine($"Max Results: {maxResults}");
            Console.WriteLine();

            var logFiles = Directory.GetFiles(logDir, "*.log")
                .OrderByDescending(f => new FileInfo(f).LastWriteTime);

            var matchCount = 0;
            foreach (var file in logFiles)
            {
                if (matchCount >= maxResults)
                    break;

                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length && matchCount < maxResults; i++)
                {
                    if (lines[i].Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write($"[{Path.GetFileName(file)}:{i + 1}] ");
                        Console.ResetColor();
                        Console.WriteLine(lines[i]);
                        matchCount++;
                    }
                }
            }

            Console.WriteLine();
            if (matchCount == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠ No matches found for: {pattern}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Found {matchCount} match(es)");
                Console.ResetColor();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Search failed: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private int ClearLogs(int days, bool force)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Clear Logs");
        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();

        try
        {
            var config = new LocoConfig();
            var logDir = config.LogDirectory;

            if (!Directory.Exists(logDir))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ No log directory found");
                Console.ResetColor();
                return 0;
            }

            var cutoffDate = DateTime.Now.AddDays(-days);
            var logFiles = Directory.GetFiles(logDir, "*.log");
            var oldFiles = logFiles
                .Where(f => new FileInfo(f).LastWriteTime < cutoffDate)
                .ToArray();

            if (oldFiles.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ No log files older than {days} days");
                Console.ResetColor();
                return 0;
            }

            long totalSize = oldFiles.Sum(f => new FileInfo(f).Length);

            Console.WriteLine($"Files to delete: {oldFiles.Length}");
            Console.WriteLine($"Size to free: {FormatBytes(totalSize)}");
            Console.WriteLine($"Older than: {cutoffDate:yyyy-MM-dd}");
            Console.WriteLine();

            foreach (var file in oldFiles.Take(10))
            {
                var info = new FileInfo(file);
                Console.WriteLine($"  • {Path.GetFileName(file)} ({FormatBytes(info.Length)}, {info.LastWriteTime:yyyy-MM-dd})");
            }

            if (oldFiles.Length > 10)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  ... and {oldFiles.Length - 10} more file(s)");
                Console.ResetColor();
            }

            Console.WriteLine();

            if (!force)
            {
                Console.Write("Delete these log files? (y/N): ");
                var response = Console.ReadLine();
                if (response?.ToLower() != "y")
                {
                    Console.WriteLine("Clear cancelled.");
                    return 0;
                }
            }

            int deleted = 0;
            foreach (var file in oldFiles)
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
            Console.WriteLine($"✓ Deleted {deleted} log file(s), freed {FormatBytes(totalSize)}");
            Console.ResetColor();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Clear failed: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
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
