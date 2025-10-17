using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core;

namespace Loco.Core.Models
{
    /// <summary>
    /// File action executor - handles file system operations with security controls
    /// </summary>
    public class FileActionExecutor : BaseActionExecutor
    {
        public override async Task ExecuteAsync(LightAction action, ILogger? logger)
        {
            await ExecuteWithDelayAsync(action, logger, async () =>
            {
                var operation = GetParameter(action, "operation", "list");
                var path = GetParameter(action, "path", ".");

                // Security validation
                if (!Security.SecurityUtilities.IsPathSafe(path))
                {
                    logger?.LogWarning("Unsafe path detected: {Path}", path);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[FILE] Error: {ErrorMessages.PathNotSafe}");
                    Console.ResetColor();
                    return;
                }

                logger?.LogInformation("Executing file operation: {Operation} on {Path}", operation, path);

                switch (operation?.ToLowerInvariant())
                {
                    case "list":
                        try
                        {
                            if (!Directory.Exists(path))
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"[FILE] {ErrorMessages.FormatError(ErrorMessages.DirectoryNotFound, path)}");
                                Console.ResetColor();
                                return;
                            }

                            var files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"[FILE] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Files in {path}: {files.Length}");
                            Console.ResetColor();

                            foreach (var file in files.Take(20))
                            {
                                var info = new FileInfo(file);
                                Console.WriteLine($"  {Path.GetFileName(file)} ({info.Length / 1024} KB, {info.LastWriteTime:yyyy-MM-dd HH:mm})");
                            }

                            if (files.Length > 20)
                            {
                                Console.WriteLine($"  ... and {files.Length - 20} more files");
                            }
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            logger?.LogWarning(ex, "Access denied to path: {Path}", path);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[FILE] {ErrorMessages.FormatError(ErrorMessages.AccessDenied, path)}");
                            Console.ResetColor();
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, "Failed to list files in: {Path}", path);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[FILE] Error: {ex.Message}");
                            Console.ResetColor();
                        }
                        break;

                    case "count":
                        if (!Directory.Exists(path))
                        {
                            Console.WriteLine($"[FILE] {ErrorMessages.FormatError(ErrorMessages.DirectoryNotFound, path)}");
                            return;
                        }

                        var recursive = GetParameter(action, "recursive", "false").ToLowerInvariant() == "true";
                        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                        var fileCount = Directory.GetFiles(path, "*", searchOption).Length;
                        var dirCount = Directory.GetDirectories(path, "*", searchOption).Length;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[FILE] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Path: {path} ({(recursive ? "recursive" : "top-level only")})");
                        Console.WriteLine($"[FILE] Files: {fileCount}, Directories: {dirCount}");
                        Console.ResetColor();
                        break;

                    case "size":
                        if (!Directory.Exists(path))
                        {
                            Console.WriteLine($"[FILE] {ErrorMessages.FormatError(ErrorMessages.DirectoryNotFound, path)}");
                            return;
                        }

                        var recursiveSize = GetParameter(action, "recursive", "false").ToLowerInvariant() == "true";
                        var sizeSearchOption = recursiveSize ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                        var totalSize = Directory.GetFiles(path, "*", sizeSearchOption)
                            .Sum(f => new FileInfo(f).Length);
                        var sizeMB = totalSize / 1024 / 1024;
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"[FILE] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Total size: {sizeMB} MB ({totalSize:N0} bytes) ({(recursiveSize ? "recursive" : "top-level only")})");
                        Console.ResetColor();
                        break;

                    case "exists":
                        var targetPath = GetParameter(action, "target", path);
                        var exists = File.Exists(targetPath) || Directory.Exists(targetPath);
                        var resultColor = exists ? ConsoleColor.Green : ConsoleColor.Yellow;
                        Console.ForegroundColor = resultColor;
                        Console.WriteLine($"[FILE] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {targetPath}: {(exists ? "EXISTS" : "NOT FOUND")}");
                        Console.ResetColor();
                        break;

                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[FILE] Unknown operation: {operation}");
                        Console.WriteLine($"[FILE] Available operations: list, count, size, exists");
                        Console.ResetColor();
                        break;
                }
            });
        }
    }
}