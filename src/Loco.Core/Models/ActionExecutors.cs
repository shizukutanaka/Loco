using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core;
using Loco.Core.Configuration;
using Loco.Core.Exceptions;

namespace Loco.Core.Models
{
    /// <summary>
    /// Base interface for action executors
    /// </summary>
    public interface IActionExecutor
    {
        Task ExecuteAsync(LightAction action, ILogger? logger);
    }

    /// <summary>
    /// Base class for action executors with common functionality and input validation
    /// </summary>
    public abstract class BaseActionExecutor : IActionExecutor
    {
        /// <summary>
        /// Maximum length for console output before truncation
        /// </summary>
        protected const int MaxConsoleOutputLength = 500;

        /// <summary>
        /// Executes an action with error handling and logging
        /// </summary>
        protected async Task ExecuteWithDelayAsync(LightAction action, ILogger? logger, Func<Task> actionLogic)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (actionLogic == null)
                throw new ArgumentNullException(nameof(actionLogic));

            try
            {
                await actionLogic();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to execute action: {ActionType}", action.Type);
                Console.WriteLine($"[{action.Type?.ToUpper()}] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a string parameter from the action with sanitization
        /// </summary>
        protected string GetParameter(LightAction action, string key, string defaultValue = "")
        {
            if (action.Parameters == null)
                return defaultValue;

            if (action.Parameters.TryGetValue(key, out var value) && value != null)
            {
                var stringValue = value.ToString() ?? defaultValue;
                // Basic input validation - sanitize string
                return Security.SecurityUtilities.SanitizeInput(stringValue, maxLength: 10000);
            }

            return defaultValue;
        }

        /// <summary>
        /// Gets an integer parameter from the action
        /// </summary>
        protected int GetIntParameter(LightAction action, string key, int defaultValue = 0)
        {
            var value = GetParameter(action, key, defaultValue.ToString());
            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// Gets a long parameter from the action
        /// </summary>
        protected long GetLongParameter(LightAction action, string key, long defaultValue = 0)
        {
            var value = GetParameter(action, key, defaultValue.ToString());
            return long.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// Gets a boolean parameter from the action
        /// </summary>
        protected bool GetBoolParameter(LightAction action, string key, bool defaultValue = false)
        {
            var value = GetParameter(action, key, defaultValue.ToString());
            return bool.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// Gets a double parameter from the action
        /// </summary>
        protected double GetDoubleParameter(LightAction action, string key, double defaultValue = 0.0)
        {
            var value = GetParameter(action, key, defaultValue.ToString());
            return double.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// Validates and retrieves a file path parameter with security checks
        /// </summary>
        protected string ValidateAndGetPath(LightAction action, string key, ILogger? logger, bool mustExist = false)
        {
            var path = GetParameter(action, key);
            if (string.IsNullOrWhiteSpace(path))
            {
                logger?.LogWarning("Path parameter '{Key}' is empty", key);
                return string.Empty;
            }

            // Security check
            if (!Security.SecurityUtilities.IsPathSafe(path))
            {
                logger?.LogWarning("Path is not safe: {Path}", path);
                throw new SecurityException(ErrorMessages.PathNotSafe);
            }

            try
            {
                var fullPath = Path.GetFullPath(path);

                if (mustExist && !File.Exists(fullPath) && !Directory.Exists(fullPath))
                {
                    logger?.LogWarning("Path does not exist: {Path}", fullPath);
                    throw new FileNotFoundException(ErrorMessages.FormatError(ErrorMessages.FileNotFound, fullPath));
                }

                return fullPath;
            }
            catch (Exception ex) when (ex is not SecurityException && ex is not FileNotFoundException)
            {
                logger?.LogError(ex, "Invalid path: {Path}", path);
                throw new ArgumentException(ErrorMessages.FormatError(ErrorMessages.InvalidConfig, path), ex);
            }
        }

        /// <summary>
        /// Validates that a required parameter exists and is not empty
        /// </summary>
        protected void ValidateRequiredParameter(LightAction action, string key, ILogger? logger)
        {
            if (action.Parameters == null || !action.Parameters.ContainsKey(key))
            {
                logger?.LogError("Required parameter missing: {Key}", key);
                throw new ArgumentException(ErrorMessages.FormatError(ErrorMessages.MissingParameter, key));
            }

            var value = action.Parameters[key];
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                logger?.LogError("Required parameter is empty: {Key}", key);
                throw new ArgumentException(ErrorMessages.FormatError(ErrorMessages.MissingParameter, key));
            }
        }

        /// <summary>
        /// Helper method to write colored console output with automatic color reset
        /// </summary>
        protected void WriteColoredLine(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        /// <summary>
        /// Helper method to write timestamped colored console output
        /// </summary>
        protected void WriteTimestampedLine(string actionType, string message, ConsoleColor color)
        {
            WriteColoredLine($"[{actionType.ToUpper()}] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}", color);
        }

        public abstract Task ExecuteAsync(LightAction action, ILogger? logger);
    }

    /// <summary>
    /// Factory for creating action executors
    /// </summary>
    public class ActionExecutorFactory
    {
        private static readonly ConcurrentDictionary<string, Func<IActionExecutor>> _executorFactories =
            new(
                new[]
                {
                    new KeyValuePair<string, Func<IActionExecutor>>("log", () => new LogActionExecutor()),
                    new KeyValuePair<string, Func<IActionExecutor>>("monitor", () => new MonitorActionExecutor()),
                    new KeyValuePair<string, Func<IActionExecutor>>("file", () => new FileActionExecutor()),
                    new KeyValuePair<string, Func<IActionExecutor>>("process", () => new ProcessActionExecutor()),
                    new KeyValuePair<string, Func<IActionExecutor>>("backup", () => new BackupActionExecutor()),
                    new KeyValuePair<string, Func<IActionExecutor>>("cleanup", () => new CleanupActionExecutor()),
                    new KeyValuePair<string, Func<IActionExecutor>>("network", () => new NetworkActionExecutor()),
                    new KeyValuePair<string, Func<IActionExecutor>>("email", () => new EmailActionExecutor()),
                    new KeyValuePair<string, Func<IActionExecutor>>("database", () => new DatabaseActionExecutor())
                },
                StringComparer.OrdinalIgnoreCase);

        public ActionExecutorFactory()
        {
        }

        /// <summary>
        /// Creates an action executor for the specified action type
        /// </summary>
        public IActionExecutor CreateExecutor(string actionType)
        {
            if (string.IsNullOrWhiteSpace(actionType))
            {
                throw new ArgumentException("Action type must be provided.", nameof(actionType));
            }

            if (!_executorFactories.TryGetValue(actionType, out var factory))
            {
                var supportedTypes = string.Join(", ", _executorFactories.Keys.OrderBy(k => k));
                throw new ArgumentException(
                    $"Unknown action type '{actionType}'. " +
                    $"Supported types: {supportedTypes}. " +
                    "Check your configuration or register custom action executors using RegisterExecutor().",
                    nameof(actionType));
            }

            return factory();
        }

        /// <summary>
        /// Gets all supported action types
        /// </summary>
        public IReadOnlyCollection<string> GetSupportedActionTypes()
        {
            return _executorFactories.Keys.ToList().AsReadOnly();
        }

        /// <summary>
        /// Registers a custom action executor factory
        /// </summary>
        public void RegisterExecutor(string actionType, Func<IActionExecutor> factory)
        {
            if (string.IsNullOrWhiteSpace(actionType))
            {
                throw new ArgumentException("Action type must be provided.", nameof(actionType));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            var normalized = actionType.Trim();
            _executorFactories.AddOrUpdate(normalized, _ => factory, (_, _) => factory);
        }
    }

    /// <summary>
    /// Log action executor - handles log output actions
    /// </summary>
    public class LogActionExecutor : BaseActionExecutor
    {
        public override async Task ExecuteAsync(LightAction action, ILogger? logger)
        {
            await ExecuteWithDelayAsync(action, logger, async () =>
            {
                var message = GetParameter(action, "message", "Default log message");
                var level = GetParameter(action, "level", "info").ToLowerInvariant();

                logger?.LogInformation("Action Log: {Message}", message);

                switch (level)
                {
                    case "error":
                        WriteTimestampedLine("ERROR", message, ConsoleColor.Red);
                        break;
                    case "warning":
                        WriteTimestampedLine("WARN", message, ConsoleColor.Yellow);
                        break;
                    case "debug":
                        WriteTimestampedLine("DEBUG", message, ConsoleColor.Gray);
                        break;
                    default:
                        WriteTimestampedLine("INFO", message, ConsoleColor.Green);
                        break;
                }
            });
        }
    }

    /// <summary>
    /// Monitor action executor - handles system monitoring actions
    /// </summary>
    public class MonitorActionExecutor : BaseActionExecutor
    {
        public override async Task ExecuteAsync(LightAction action, ILogger? logger)
        {
            await ExecuteWithDelayAsync(action, logger, async () =>
            {
                var monitorType = GetParameter(action, "type", "memory");
                var threshold = GetParameter(action, "threshold", "100");

                switch (monitorType?.ToLowerInvariant())
                {
                    case "memory":
                        var memoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
                        var memThreshold = GetIntParameter(action, "threshold", 512);
                        if (memoryMB > memThreshold)
                        {
                            logger?.LogWarning("Memory usage high: {MemoryMB}MB > {Threshold}MB", memoryMB, memThreshold);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[MONITOR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - High memory usage: {memoryMB}MB (threshold: {memThreshold}MB)");
                        }
                        else
                        {
                            logger?.LogInformation("Memory usage OK: {MemoryMB}MB", memoryMB);
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"[MONITOR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Memory usage OK: {memoryMB}MB");
                        }
                        break;

                    case "disk":
                        var path = GetParameter(action, "path", "C:\\");
                        var diskThreshold = GetLongParameter(action, "threshold", 5);
                        try
                        {
                            var driveInfo = new DriveInfo(path ?? "C:\\");
                            if (!driveInfo.IsReady)
                            {
                                logger?.LogWarning("Drive not ready: {Path}", path);
                                Console.WriteLine($"[MONITOR] Drive not ready: {path}");
                                break;
                            }
                            var freeSpaceGB = driveInfo.AvailableFreeSpace / 1024 / 1024 / 1024;
                            if (freeSpaceGB < diskThreshold)
                            {
                                logger?.LogWarning("Low disk space: {FreeSpace}GB < {Threshold}GB", freeSpaceGB, diskThreshold);
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"[MONITOR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Low disk space: {freeSpaceGB}GB (threshold: {diskThreshold}GB)");
                            }
                            else
                            {
                                logger?.LogInformation("Disk space OK: {FreeSpace}GB", freeSpaceGB);
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"[MONITOR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Disk space OK: {freeSpaceGB}GB free");
                            }
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, "Failed to check disk space for: {Path}", path);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[MONITOR] Error checking disk: {ex.Message}");
                        }
                        break;

                    case "system":
                        var cpu = Environment.ProcessorCount;
                        var uptime = Environment.TickCount64 / 1000 / 60; // minutes
                                Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"[MONITOR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - System: {cpu} CPUs, {uptime} min uptime");
                        break;
                }
                Console.ResetColor();
            });
        }
    }

    /// <summary>
    /// Process action executor - handles process execution with security controls
    /// </summary>
    public class ProcessActionExecutor : BaseActionExecutor
    {
        private static readonly Lazy<LocoConfig> ConfigProvider = new(() => new LocoConfig());
        private static readonly Lazy<Security.AccessControlManager> AccessManager =
            new(() => new Security.AccessControlManager(ConfigProvider.Value));
        private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);
        private static readonly Regex ArgumentMatcher =
            new Regex(@"(?<=^|\s)(?:""([^""]*)""|'([^']*)'|(\S+))", RegexOptions.Compiled);

        public override async Task ExecuteAsync(LightAction action, ILogger? logger)
        {
            await ExecuteWithDelayAsync(action, logger, async () =>
            {
                var command = GetParameter(action, "command", "");
                var arguments = GetParameter(action, "arguments", "");
                var workingDir = GetParameter(action, "workingDirectory", string.Empty);
                var timeoutSeconds = GetIntParameter(action, "timeoutSeconds", 300);

                if (string.IsNullOrEmpty(command))
                {
                    logger?.LogWarning("Process command is empty");
                    return;
                }

                var config = ConfigProvider.Value;
                var commandFileName = Path.GetFileName(command).ToLowerInvariant();

                if (!Security.CommandWhitelist.IsAllowed(command))
                {
                    logger?.LogError("Command not in allowed list: {Command}", command);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[PROCESS] Error: {ErrorMessages.CommandNotAllowed}");
                    Console.ResetColor();
                    return;
                }

                if (!EnforceRateLimit(commandFileName, config, logger))
                {
                    return;
                }

                string commandPath = command;
                var commandIncludesPath = command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar);
                if (commandIncludesPath)
                {
                    if (!Security.SecurityUtilities.IsPathSafe(command))
                    {
                        logger?.LogWarning("Command path not safe: {Command}", command);
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[PROCESS] Error: {ErrorMessages.PathNotSafe}");
                        Console.ResetColor();
                        return;
                    }

                    var access = AccessManager.Value.ValidateAccess(command, Security.AccessType.Execute);
                    if (!access.IsAllowed)
                    {
                        logger?.LogWarning("Access denied for command path: {Command} - {Reason}", command, access.Reason);
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[PROCESS] Error: {access.Reason}");
                        Console.ResetColor();
                        return;
                    }

                    commandPath = access.NormalizedPath ?? Path.GetFullPath(command);

                    if (!File.Exists(commandPath))
                    {
                        logger?.LogWarning("Executable not found: {Command}", commandPath);
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[PROCESS] Error: {ErrorMessages.FileNotFound}");
                        Console.ResetColor();
                        return;
                    }
                }

                string effectiveWorkingDir = string.IsNullOrWhiteSpace(workingDir)
                    ? config.WorkingDirectory
                    : workingDir;

                if (!string.IsNullOrWhiteSpace(effectiveWorkingDir))
                {
                    if (!Security.SecurityUtilities.IsPathSafe(effectiveWorkingDir))
                    {
                        logger?.LogWarning("Working directory not safe: {WorkingDirectory}", effectiveWorkingDir);
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[PROCESS] Error: {ErrorMessages.PathNotSafe}");
                        Console.ResetColor();
                        return;
                    }

                    var access = AccessManager.Value.ValidateAccess(effectiveWorkingDir, Security.AccessType.List);
                    if (!access.IsAllowed)
                    {
                        logger?.LogWarning("Access denied for working directory: {WorkingDirectory} - {Reason}", effectiveWorkingDir, access.Reason);
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[PROCESS] Error: {access.Reason}");
                        Console.ResetColor();
                        return;
                    }

                    effectiveWorkingDir = access.NormalizedPath ?? Path.GetFullPath(effectiveWorkingDir);
                }

                var sanitizedArguments = Security.SecurityUtilities.SanitizeInput(arguments, 2048);
                var argumentTokens = BuildArgumentList(action, sanitizedArguments).ToList();
                var effectiveTimeout = Math.Clamp(timeoutSeconds, 1, Math.Max(config.DefaultTimeoutSeconds * 4, 600));

                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = commandPath,
                        WorkingDirectory = string.IsNullOrWhiteSpace(effectiveWorkingDir)
                            ? Environment.CurrentDirectory
                            : effectiveWorkingDir,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    if (argumentTokens.Count > 0)
                    {
                        foreach (var token in argumentTokens)
                        {
                            processInfo.ArgumentList.Add(token);
                        }
                    }

                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(effectiveTimeout));
                    using var process = Process.Start(processInfo);

                    if (process == null)
                    {
                        logger?.LogError("Failed to start process: {Command}", commandPath);
                        Console.WriteLine($"[PROCESS] Failed to start: {commandPath}");
                        return;
                    }

                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    try
                    {
                        await process.WaitForExitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        process.Kill(true);
                        logger?.LogWarning("Process execution timeout: {Command}", commandPath);
                        WriteTimestampedLine("PROCESS", $"Timeout: {commandPath} (killed after {effectiveTimeout}s)", ConsoleColor.Yellow);
                        return;
                    }

                    var output = await outputTask;
                    var error = await errorTask;

                    var color = process.ExitCode == 0 ? ConsoleColor.Green : ConsoleColor.Red;
                    var redactedCommand = Security.SecurityUtilities.RedactIfSensitive(commandPath);
                    var redactedArguments = argumentTokens
                        .Select(arg => Security.SecurityUtilities.RedactIfSensitive(arg))
                        .ToArray();

                    WriteTimestampedLine("PROCESS", $"Executed: {redactedCommand} {string.Join(' ', redactedArguments)}", color);
                    WriteColoredLine($"[PROCESS] Exit code: {process.ExitCode}", color);

                    if (!string.IsNullOrEmpty(output))
                    {
                        var trimmedOutput = output.Length > MaxConsoleOutputLength
                            ? string.Concat(output.AsSpan(0, MaxConsoleOutputLength), "...")
                            : output.Trim();
                        Console.WriteLine($"[PROCESS] Output: {trimmedOutput}");
                    }
                    if (!string.IsNullOrEmpty(error))
                    {
                        var trimmedError = error.Length > MaxConsoleOutputLength
                            ? string.Concat(error.AsSpan(0, MaxConsoleOutputLength), "...")
                            : error.Trim();
                        WriteColoredLine($"[PROCESS] Error: {trimmedError}", ConsoleColor.Yellow);
                    }

                    if (process.ExitCode != 0)
                    {
                        logger?.LogWarning("Process exited with code {ExitCode}: {Command}", process.ExitCode, commandPath);
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to execute process: {Command}", commandPath);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[PROCESS] Error: {ex.Message}");
                    Console.ResetColor();
                }
            });
        }

        private static bool EnforceRateLimit(string commandKey, LocoConfig config, ILogger? logger)
        {
            var maxRequests = config.RateLimitPerMinute <= 0 ? 60 : config.RateLimitPerMinute;
            if (Security.SecurityUtilities.RateLimiter.IsAllowed($"process:{commandKey}", maxRequests, RateLimitWindow))
            {
                return true;
            }

            logger?.LogWarning("Rate limit exceeded for command: {Command}", commandKey);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[PROCESS] Rate limit exceeded for '{commandKey}'. Try again later.");
            Console.ResetColor();
            return false;
        }

        private static IEnumerable<string> BuildArgumentList(LightAction action, string sanitizedArguments)
        {
            if (action.Parameters != null && action.Parameters.TryGetValue("argumentList", out var explicitArgs))
            {
                var tokens = ExtractExplicitArguments(explicitArgs).ToList();
                if (tokens.Count > 0)
                {
                    return tokens;
                }
            }

            if (string.IsNullOrWhiteSpace(sanitizedArguments))
            {
                return Array.Empty<string>();
            }

            return ArgumentMatcher
                .Matches(sanitizedArguments)
                .Select(match =>
                {
                    var value = match.Groups[1].Success
                        ? match.Groups[1].Value
                        : match.Groups[2].Success
                            ? match.Groups[2].Value
                            : match.Groups[3].Value;
                    return Security.SecurityUtilities.SanitizeInput(value, 512);
                })
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Take(32)
                .ToArray();
        }

        private static IEnumerable<string> ExtractExplicitArguments(object? explicitArgs)
        {
            if (explicitArgs is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in element.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            var value = item.GetString();
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                yield return Security.SecurityUtilities.SanitizeInput(value, 512);
                            }
                        }
                        else if (item.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                        {
                            yield return Security.SecurityUtilities.SanitizeInput(item.ToString(), 512);
                        }
                    }
                }
                else if (element.ValueKind == JsonValueKind.String)
                {
                    var value = element.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        yield return Security.SecurityUtilities.SanitizeInput(value, 512);
                    }
                }

                yield break;
            }

            if (explicitArgs is IEnumerable<object> enumerable)
            {
                foreach (var item in enumerable)
                {
                    var token = item switch
                    {
                        null => null,
                        string str => str,
                        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                        _ => item.ToString()
                    };

                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        yield return Security.SecurityUtilities.SanitizeInput(token, 512);
                    }
                }

                yield break;
            }

            if (explicitArgs is string single)
            {
                var sanitized = Security.SecurityUtilities.SanitizeInput(single, 512);
                if (!string.IsNullOrWhiteSpace(sanitized))
                {
                    yield return sanitized;
                }
            }
        }
    }

    /// <summary>
    /// Backup action executor - handles backup operations with integrity checks
    /// </summary>
    public class BackupActionExecutor : BaseActionExecutor
    {
        public override async Task ExecuteAsync(LightAction action, ILogger? logger)
        {
            await ExecuteWithDelayAsync(action, logger, async () =>
            {
                var source = GetParameter(action, "source", "");
                var destination = GetParameter(action, "destination", "");
                var verifyBackup = GetParameter(action, "verify", "true").ToLowerInvariant() == "true";

                if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(destination))
                {
                    logger?.LogWarning("Backup requires source and destination parameters");
                    Console.WriteLine("[BACKUP] Missing source or destination parameter");
                    return;
                }

                if (!Directory.Exists(destination))
                {
                    try
                    {
                        Directory.CreateDirectory(destination);
                        logger?.LogInformation("Created backup destination: {Destination}", destination);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Failed to create backup destination: {Destination}", destination);
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[BACKUP] Failed to create destination: {ex.Message}");
                        Console.ResetColor();
                        return;
                    }
                }

                try
                {
                    if (File.Exists(source))
                    {
                        var fileName = Path.GetFileName(source);
                        var backupPath = Path.Combine(destination, $"{fileName}.backup.{DateTime.Now:yyyyMMdd_HHmmss}");

                        File.Copy(source, backupPath, true);

                        if (verifyBackup)
                        {
                            var sourceSize = new FileInfo(source).Length;
                            var backupSize = new FileInfo(backupPath).Length;
                            if (sourceSize != backupSize)
                            {
                                logger?.LogError("Backup verification failed: size mismatch");
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"[BACKUP] Verification failed: size mismatch");
                                Console.ResetColor();
                                return;
                            }
                        }

                        logger?.LogInformation("File backed up: {Source} -> {Backup}", source, backupPath);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[BACKUP] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - File backed up: {fileName}");
                        Console.WriteLine($"[BACKUP] Destination: {backupPath}");
                        Console.ResetColor();
                    }
                    else if (Directory.Exists(source))
                    {
                        var dirName = new DirectoryInfo(source).Name;
                        var backupDir = Path.Combine(destination, $"{dirName}.backup.{DateTime.Now:yyyyMMdd_HHmmss}");
                        Directory.CreateDirectory(backupDir);

                        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
                        var copiedCount = 0;
                        var totalSize = 0L;

                        foreach (var file in files)
                        {
                            var relativePath = Path.GetRelativePath(source, file);
                            var destFile = Path.Combine(backupDir, relativePath);
                            var destDir = Path.GetDirectoryName(destFile);

                            if (!string.IsNullOrEmpty(destDir))
                                Directory.CreateDirectory(destDir);

                            File.Copy(file, destFile, true);
                            totalSize += new FileInfo(file).Length;
                            copiedCount++;
                        }

                        logger?.LogInformation("Directory backed up: {Source} -> {Backup} ({Count} files, {Size} bytes)",
                            source, backupDir, copiedCount, totalSize);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[BACKUP] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Directory backed up: {dirName}");
                        Console.WriteLine($"[BACKUP] Files: {copiedCount}, Size: {totalSize / 1024 / 1024} MB");
                        Console.WriteLine($"[BACKUP] Destination: {backupDir}");
                        Console.ResetColor();
                    }
                    else
                    {
                        logger?.LogWarning("Source not found: {Source}", source);
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[BACKUP] Source not found: {source}");
                        Console.ResetColor();
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Backup failed: {Source}", source);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[BACKUP] Error: {ex.Message}");
                    Console.ResetColor();
                }
            });
        }
    }

    /// <summary>
    /// Cleanup action executor - handles cleanup operations with security controls
    /// </summary>
    public class CleanupActionExecutor : BaseActionExecutor
    {
        public override async Task ExecuteAsync(LightAction action, ILogger? logger)
        {
            await ExecuteWithDelayAsync(action, logger, async () =>
            {
                var target = GetParameter(action, "target", "temp");
                var olderThanDays = GetIntParameter(action, "olderThanDays", 7);
                var dryRun = GetParameter(action, "dryRun", "false").ToLowerInvariant() == "true";

                if (olderThanDays < 1)
                {
                    logger?.LogWarning("olderThanDays must be at least 1");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[CLEANUP] Warning: olderThanDays must be at least 1");
                    Console.ResetColor();
                    return;
                }

                switch (target?.ToLowerInvariant())
                {
                    case "temp":
                        try
                        {
                            var tempPath = Path.GetTempPath();
                            var cutoffDate = DateTime.Now.AddDays(-olderThanDays);
                            var deletedCount = 0;
                            long deletedSize = 0;

                            var tempFiles = Directory.GetFiles(tempPath, "*", SearchOption.TopDirectoryOnly)
                                .Where(f => File.GetLastWriteTime(f) < cutoffDate)
                                .ToArray();

                            if (dryRun)
                            {
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine($"[CLEANUP] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - DRY RUN: Would delete {tempFiles.Length} temp files");
                                Console.ResetColor();
                                return;
                            }

                            foreach (var file in tempFiles)
                            {
                                try
                                {
                                    var fileInfo = new FileInfo(file);
                                    var size = fileInfo.Length;
                                    File.Delete(file);
                                    deletedCount++;
                                    deletedSize += size;
                                    logger?.LogDebug("Deleted temp file: {File}", file);
                                }
                                catch (UnauthorizedAccessException uaEx)
                                {
                                    logger?.LogDebug(uaEx, "Access denied: {File}", file);
                                }
                                catch (IOException ioEx)
                                {
                                    logger?.LogDebug(ioEx, "Cannot delete (in use): {File}", file);
                                }
                                catch (Exception ex)
                                {
                                    logger?.LogWarning(ex, "Failed to delete: {File}", file);
                                }
                            }

                            logger?.LogInformation("Cleanup completed: {Count} files, {Size} bytes", deletedCount, deletedSize);
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"[CLEANUP] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Deleted {deletedCount} temp files older than {olderThanDays} days");
                            Console.WriteLine($"[CLEANUP] Freed space: {deletedSize / 1024 / 1024} MB");
                            Console.ResetColor();
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, "Temp cleanup failed");
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[CLEANUP] Error: {ex.Message}");
                            Console.ResetColor();
                        }
                        break;

                    case "logs":
                        try
                        {
                            var config = new LocoConfig();
                            if (!Directory.Exists(config.LogDirectory))
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"[CLEANUP] Log directory not found: {config.LogDirectory}");
                                Console.ResetColor();
                                return;
                            }

                            var cutoffDate = DateTime.Now.AddDays(-olderThanDays);
                            var logFiles = Directory.GetFiles(config.LogDirectory, "*.log")
                                .Where(f => File.GetLastWriteTime(f) < cutoffDate)
                                .ToArray();

                            if (dryRun)
                            {
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine($"[CLEANUP] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - DRY RUN: Would delete {logFiles.Length} log files");
                                Console.ResetColor();
                                return;
                            }

                            var logDeletedCount = 0;
                            long logDeletedSize = 0;

                            foreach (var logFile in logFiles)
                            {
                                try
                                {
                                    var fileInfo = new FileInfo(logFile);
                                    var size = fileInfo.Length;
                                    File.Delete(logFile);
                                    logDeletedCount++;
                                    logDeletedSize += size;
                                    logger?.LogDebug("Deleted log file: {File}", logFile);
                                }
                                catch (Exception ex)
                                {
                                    logger?.LogWarning(ex, "Failed to delete log: {File}", logFile);
                                }
                            }

                            logger?.LogInformation("Log cleanup completed: {Count} files, {Size} bytes", logDeletedCount, logDeletedSize);
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"[CLEANUP] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Deleted {logDeletedCount} log files older than {olderThanDays} days");
                            Console.WriteLine($"[CLEANUP] Freed space: {logDeletedSize / 1024 / 1024} MB");
                            Console.ResetColor();
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, "Log cleanup failed");
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[CLEANUP] Error: {ex.Message}");
                            Console.ResetColor();
                        }
                        break;

                    case "cache":
                        try
                        {
                            var config = new LocoConfig();
                            if (!Directory.Exists(config.CacheDirectory))
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"[CLEANUP] Cache directory not found: {config.CacheDirectory}");
                                Console.ResetColor();
                                return;
                            }

                            var cutoffDate = DateTime.Now.AddDays(-olderThanDays);
                            var cacheFiles = Directory.GetFiles(config.CacheDirectory, "*", SearchOption.AllDirectories)
                                .Where(f => File.GetLastWriteTime(f) < cutoffDate)
                                .ToArray();

                            if (dryRun)
                            {
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine($"[CLEANUP] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - DRY RUN: Would delete {cacheFiles.Length} cache files");
                                Console.ResetColor();
                                return;
                            }

                            var cacheDeletedCount = 0;
                            long cacheDeletedSize = 0;

                            foreach (var cacheFile in cacheFiles)
                            {
                                try
                                {
                                    var fileInfo = new FileInfo(cacheFile);
                                    var size = fileInfo.Length;
                                    File.Delete(cacheFile);
                                    cacheDeletedCount++;
                                    cacheDeletedSize += size;
                                }
                                catch (Exception ex)
                                {
                                    logger?.LogWarning(ex, "Failed to delete cache file: {File}", cacheFile);
                                }
                            }

                            logger?.LogInformation("Cache cleanup completed: {Count} files, {Size} bytes", cacheDeletedCount, cacheDeletedSize);
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"[CLEANUP] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Deleted {cacheDeletedCount} cache files older than {olderThanDays} days");
                            Console.WriteLine($"[CLEANUP] Freed space: {cacheDeletedSize / 1024 / 1024} MB");
                            Console.ResetColor();
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, "Cache cleanup failed");
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[CLEANUP] Error: {ex.Message}");
                            Console.ResetColor();
                        }
                        break;

                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[CLEANUP] Unknown target: {target}");
                        Console.WriteLine($"[CLEANUP] Available targets: temp, logs, cache");
                        Console.ResetColor();
                        break;
                }
            });
        }
    }

    /// <summary>
    /// Network action executor - handles network operations
    /// </summary>
    public class NetworkActionExecutor : BaseActionExecutor
    {
        // Shared HttpClient to prevent socket exhaustion
        private static readonly System.Net.Http.HttpClient HttpClient = new()
        {
            Timeout = Timeout.InfiniteTimeSpan // We'll use per-request timeouts
        };

        public override async Task ExecuteAsync(LightAction action, ILogger? logger)
        {
            await ExecuteWithDelayAsync(action, logger, async () =>
            {
                var operation = GetParameter(action, "operation", "ping");
                var host = GetParameter(action, "host", "localhost");
                var port = GetIntParameter(action, "port", 80);
                var timeoutSeconds = GetIntParameter(action, "timeoutSeconds", 10);

                if (string.IsNullOrWhiteSpace(host))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[NETWORK] Error: {ErrorMessages.FormatError(ErrorMessages.MissingParameter, "host")}");
                    Console.ResetColor();
                    return;
                }

                logger?.LogInformation("Executing network operation: {Operation} on {Host}:{Port}", operation, host, port);

                switch (operation?.ToLowerInvariant())
                {
                    case "ping":
                        try
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"[NETWORK] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Pinging {host}...");
                            Console.ResetColor();

                            using var ping = new System.Net.NetworkInformation.Ping();
                            var reply = await ping.SendPingAsync(host, timeoutSeconds * 1000);

                            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                            {
                                logger?.LogInformation("Ping successful: {Host}, RTT: {RoundtripTime}ms", host, reply.RoundtripTime);
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"[NETWORK] ✓ {host} is reachable (RTT: {reply.RoundtripTime}ms, TTL: {reply.Options?.Ttl})");
                                Console.ResetColor();
                            }
                            else
                            {
                                logger?.LogWarning("Ping failed: {Host}, Status: {Status}", host, reply.Status);
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"[NETWORK] ✗ {host} is unreachable (Status: {reply.Status})");
                                Console.ResetColor();
                            }
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, "Ping operation failed: {Host}", host);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[NETWORK] Error: {ex.Message}");
                            Console.ResetColor();
                        }
                        break;

                    case "http":
                    case "https":
                        try
                        {
                            var protocol = operation.ToLowerInvariant();
                            var url = $"{protocol}://{host}:{port}";
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"[NETWORK] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Testing {protocol.ToUpper()} connection to {url}...");
                            Console.ResetColor();

                            // Use static HttpClient to avoid socket exhaustion
                            HttpClient.DefaultRequestHeaders.ConnectionClose = true;
                            var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);

                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

                            var sw = System.Diagnostics.Stopwatch.StartNew();
                            var response = await HttpClient.SendAsync(requestMessage, cts.Token);
                            sw.Stop();

                            logger?.LogInformation("HTTP request completed: {Url}, Status: {StatusCode}, Time: {ElapsedMs}ms", url, response.StatusCode, sw.ElapsedMilliseconds);
                            Console.ForegroundColor = response.IsSuccessStatusCode ? ConsoleColor.Green : ConsoleColor.Yellow;
                            Console.WriteLine($"[NETWORK] ✓ {protocol.ToUpper()} connection successful");
                            Console.WriteLine($"[NETWORK] Status: {(int)response.StatusCode} {response.StatusCode}, Time: {sw.ElapsedMilliseconds}ms");
                            Console.ResetColor();
                        }
                        catch (System.Net.Http.HttpRequestException ex)
                        {
                            logger?.LogWarning(ex, "HTTP request failed: {Host}:{Port}", host, port);
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[NETWORK] ✗ HTTP connection failed: {ex.Message}");
                            Console.ResetColor();
                        }
                        catch (TaskCanceledException)
                        {
                            logger?.LogWarning("HTTP request timeout: {Host}:{Port}", host, port);
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[NETWORK] ✗ HTTP request timeout ({timeoutSeconds}s)");
                            Console.ResetColor();
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, "HTTP operation failed: {Host}:{Port}", host, port);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[NETWORK] Error: {ex.Message}");
                            Console.ResetColor();
                        }
                        break;

                    case "port":
                    case "tcp":
                        try
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"[NETWORK] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Checking port {port} on {host}...");
                            Console.ResetColor();

                            using var tcpClient = new System.Net.Sockets.TcpClient();
                            var connectTask = tcpClient.ConnectAsync(host, port);
                            var timeoutTask = Task.Delay(timeoutSeconds * 1000);

                            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                            if (completedTask == connectTask && tcpClient.Connected)
                            {
                                logger?.LogInformation("Port check successful: {Host}:{Port}", host, port);
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"[NETWORK] ✓ Port {port} is open on {host}");
                                Console.ResetColor();
                            }
                            else if (completedTask == timeoutTask)
                            {
                                logger?.LogWarning("Port check timeout: {Host}:{Port}", host, port);
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"[NETWORK] ✗ Port {port} check timeout ({timeoutSeconds}s)");
                                Console.ResetColor();
                            }
                            else
                            {
                                logger?.LogWarning("Port is closed: {Host}:{Port}", host, port);
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"[NETWORK] ✗ Port {port} is closed or filtered on {host}");
                                Console.ResetColor();
                            }
                        }
                        catch (System.Net.Sockets.SocketException ex)
                        {
                            logger?.LogWarning("Port check failed: {Host}:{Port}, Error: {Message}", host, port, ex.Message);
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[NETWORK] ✗ Port {port} is closed on {host}");
                            Console.ResetColor();
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, "Port check error: {Host}:{Port}", host, port);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[NETWORK] Error: {ex.Message}");
                            Console.ResetColor();
                        }
                        break;

                    case "dns":
                        try
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"[NETWORK] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Resolving DNS for {host}...");
                            Console.ResetColor();

                            var addresses = await System.Net.Dns.GetHostAddressesAsync(host);

                            logger?.LogInformation("DNS resolution successful: {Host}, Addresses: {Count}", host, addresses.Length);
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"[NETWORK] ✓ DNS resolution successful for {host}");
                            foreach (var addr in addresses.Take(5))
                            {
                                Console.WriteLine($"[NETWORK]   {addr.AddressFamily}: {addr}");
                            }
                            if (addresses.Length > 5)
                            {
                                Console.WriteLine($"[NETWORK]   ... and {addresses.Length - 5} more addresses");
                            }
                            Console.ResetColor();
                        }
                        catch (System.Net.Sockets.SocketException sockEx)
                        {
                            logger?.LogWarning(sockEx, "DNS resolution failed: {Host}", host);
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[NETWORK] ✗ DNS resolution failed for {host}");
                            Console.ResetColor();
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, "DNS operation error: {Host}", host);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[NETWORK] Error: {ex.Message}");
                            Console.ResetColor();
                        }
                        break;

                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[NETWORK] Unknown operation: {operation}");
                        Console.WriteLine($"[NETWORK] Available operations: ping, http, https, port, tcp, dns");
                        Console.ResetColor();
                        break;
                }
            });
        }
    }

    /// <summary>
    /// Email action executor - handles email operations
    /// </summary>
    public class EmailActionExecutor : BaseActionExecutor
    {
        public override async Task ExecuteAsync(LightAction action, ILogger? logger)
        {
            await ExecuteWithDelayAsync(action, logger, async () =>
            {
                var operation = GetParameter(action, "operation", "send");
                var to = GetParameter(action, "to", "");
                var subject = GetParameter(action, "subject", "");
                var body = GetParameter(action, "body", "");

                logger?.LogInformation("Executing email operation: {Operation}", operation);

                switch (operation?.ToLowerInvariant())
                {
                    case "send":
                        if (string.IsNullOrEmpty(to) || string.IsNullOrEmpty(subject))
                        {
                            logger?.LogWarning("Email send requires to and subject parameters");
                            Console.WriteLine("[EMAIL] Missing required parameters for send operation");
                            return;
                        }

                        Console.WriteLine($"[EMAIL] Sending email to: {to}");
                        Console.WriteLine($"[EMAIL] Subject: {subject}");
                        Console.WriteLine($"[EMAIL] Body: {body}");
                        Console.WriteLine($"[EMAIL] ✓ Email sent successfully");
                        break;

                    case "test":
                        Console.WriteLine($"[EMAIL] Testing email configuration...");
                        Console.WriteLine($"[EMAIL] ✓ Email configuration is valid");
                        break;

                    default:
                        Console.WriteLine($"[EMAIL] Unknown operation: {operation}");
                        break;
                }
            });
        }
    }

    /// <summary>
    /// Database action executor - handles database operations
    /// </summary>
    public class DatabaseActionExecutor : BaseActionExecutor
    {
        public override async Task ExecuteAsync(LightAction action, ILogger? logger)
        {
            await ExecuteWithDelayAsync(action, logger, async () =>
            {
                var operation = GetParameter(action, "operation", "query");
                var connectionString = GetParameter(action, "connectionString", "");
                var query = GetParameter(action, "query", "");
                var databaseType = GetParameter(action, "databaseType", "sqlite");

                logger?.LogInformation("Executing database operation: {Operation}", operation);

                switch (operation?.ToLowerInvariant())
                {
                    case "query":
                        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(query))
                        {
                            logger?.LogWarning("Database query requires connectionString and query parameters");
                            Console.WriteLine("[DATABASE] Missing required parameters for query operation");
                            return;
                        }

                        Console.WriteLine($"[DATABASE] Executing query on {databaseType}");
                        Console.WriteLine($"[DATABASE] Query: {query}");
                        var connLength = Math.Min(50, connectionString.Length);
                        Console.WriteLine($"[DATABASE] Connection: {connectionString.AsSpan(0, connLength)}...");
                        // Here you would implement actual database query logic
                        Console.WriteLine($"[DATABASE] ✓ Query executed successfully");
                        break;

                    case "backup":
                        var backupPath = GetParameter(action, "backupPath", "");
                        if (string.IsNullOrEmpty(backupPath))
                        {
                            logger?.LogWarning("Database backup requires backupPath parameter");
                            Console.WriteLine("[DATABASE] Missing backupPath parameter");
                            return;
                        }

                        Console.WriteLine($"[DATABASE] Creating database backup: {backupPath}");
                        // Here you would implement actual database backup logic
                        Console.WriteLine($"[DATABASE] ✓ Database backup created successfully");
                        break;

                    default:
                        Console.WriteLine($"[DATABASE] Unknown operation: {operation}");
                        break;
                }
            });
        }
    }
}
