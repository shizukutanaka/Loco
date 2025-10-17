using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Cli.UI
{
    /// <summary>
    /// Enhanced console UI utilities for better user experience
    /// Provides progress bars, spinners, tables, and formatting
    /// </summary>
    public static class ConsoleUI
    {
        // Color scheme
        public static class Colors
        {
            public static ConsoleColor Primary = ConsoleColor.Cyan;
            public static ConsoleColor Success = ConsoleColor.Green;
            public static ConsoleColor Warning = ConsoleColor.Yellow;
            public static ConsoleColor Error = ConsoleColor.Red;
            public static ConsoleColor Critical = ConsoleColor.Magenta;
            public static ConsoleColor Info = ConsoleColor.Blue;
            public static ConsoleColor Muted = ConsoleColor.DarkGray;
            public static ConsoleColor Highlight = ConsoleColor.White;
        }

        // Icons
        public static class Icons
        {
            public const string Success = "✓";
            public const string Error = "✗";
            public const string Warning = "⚠";
            public const string Info = "ℹ";
            public const string Arrow = "→";
            public const string Bullet = "•";
            public const string Check = "✔";
            public const string Cross = "✘";
            public const string Star = "★";
            public const string Heart = "♥";
            public const string Clock = "⏱";
            public const string Gear = "⚙";
            public const string Rocket = "🚀";
        }

        /// <summary>
        /// Display a progress bar
        /// </summary>
        public static void ShowProgressBar(int current, int total, string? label = null, int width = 40)
        {
            var percent = (double)current / total;
            var filled = (int)(width * percent);
            var empty = width - filled;

            Console.Write("\r");
            if (!string.IsNullOrEmpty(label))
            {
                Console.ForegroundColor = Colors.Info;
                Console.Write($"{label}: ");
                Console.ResetColor();
            }

            Console.Write("[");
            Console.ForegroundColor = Colors.Success;
            Console.Write(new string('█', filled));
            Console.ForegroundColor = Colors.Muted;
            Console.Write(new string('░', empty));
            Console.ResetColor();
            Console.Write($"] {percent:P0} ({current}/{total})");
        }

        /// <summary>
        /// Clear the progress bar
        /// </summary>
        public static void ClearProgressBar()
        {
            Console.Write("\r" + new string(' ', Console.BufferWidth - 1) + "\r");
        }

        /// <summary>
        /// Show a spinner for long-running operations
        /// </summary>
        public class Spinner : IDisposable
        {
            private readonly string[] _frames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
            private readonly string _message;
            private readonly CancellationTokenSource _cts;
            private readonly Task _spinTask;
            private int _currentFrame;

            public Spinner(string message = "Processing")
            {
                _message = message;
                _cts = new CancellationTokenSource();
                _currentFrame = 0;
                _spinTask = Task.Run(() => Spin(_cts.Token));
            }

            private async Task Spin(CancellationToken cancellationToken)
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        Console.Write($"\r{_frames[_currentFrame]} {_message}...");
                        _currentFrame = (_currentFrame + 1) % _frames.Length;
                        await Task.Delay(80, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when disposed
                }
                finally
                {
                    Console.Write("\r" + new string(' ', Console.BufferWidth - 1) + "\r");
                }
            }

            public void Stop()
            {
                Dispose();
            }

            public void Dispose()
            {
                _cts?.Cancel();
                if (_spinTask is { IsCompleted: false })
                {
                    _ = _spinTask.ContinueWith(
                        _ => _cts?.Dispose(),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
                else
                {
                    _cts?.Dispose();
                }
            }
        }

        /// <summary>
        /// Display a table with headers and rows
        /// </summary>
        public static void ShowTable(string[] headers, List<string[]> rows, bool showBorders = true)
        {
            if (headers == null || headers.Length == 0)
                return;

            // Calculate column widths
            var columnWidths = new int[headers.Length];
            for (int i = 0; i < headers.Length; i++)
            {
                columnWidths[i] = headers[i].Length;
            }

            foreach (var row in rows)
            {
                for (int i = 0; i < Math.Min(row.Length, headers.Length); i++)
                {
                    columnWidths[i] = Math.Max(columnWidths[i], row[i]?.Length ?? 0);
                }
            }

            // Add padding
            for (int i = 0; i < columnWidths.Length; i++)
            {
                columnWidths[i] += 2;
            }

            if (showBorders)
            {
                // Top border
                Console.Write("┌");
                for (int i = 0; i < headers.Length; i++)
                {
                    Console.Write(new string('─', columnWidths[i]));
                    Console.Write(i < headers.Length - 1 ? "┬" : "┐");
                }
                Console.WriteLine();
            }

            // Headers
            Console.Write(showBorders ? "│" : "");
            Console.ForegroundColor = Colors.Primary;
            for (int i = 0; i < headers.Length; i++)
            {
                Console.Write($" {headers[i].PadRight(columnWidths[i] - 1)}");
                Console.Write(showBorders ? "│" : " ");
            }
            Console.ResetColor();
            Console.WriteLine();

            if (showBorders)
            {
                // Header separator
                Console.Write("├");
                for (int i = 0; i < headers.Length; i++)
                {
                    Console.Write(new string('─', columnWidths[i]));
                    Console.Write(i < headers.Length - 1 ? "┼" : "┤");
                }
                Console.WriteLine();
            }

            // Rows
            foreach (var row in rows)
            {
                Console.Write(showBorders ? "│" : "");
                for (int i = 0; i < headers.Length; i++)
                {
                    var value = i < row.Length ? row[i] ?? "" : "";
                    Console.Write($" {value.PadRight(columnWidths[i] - 1)}");
                    Console.Write(showBorders ? "│" : " ");
                }
                Console.WriteLine();
            }

            if (showBorders)
            {
                // Bottom border
                Console.Write("└");
                for (int i = 0; i < headers.Length; i++)
                {
                    Console.Write(new string('─', columnWidths[i]));
                    Console.Write(i < headers.Length - 1 ? "┴" : "┘");
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Display a box with title and content
        /// </summary>
        public static void ShowBox(string title, string content, ConsoleColor color = ConsoleColor.Cyan)
        {
            var lines = content.Split('\n');
            var maxLength = Math.Max(title.Length, lines.Max(l => l.Length)) + 4;

            Console.ForegroundColor = color;
            Console.WriteLine("┌" + new string('─', maxLength) + "┐");
            Console.WriteLine($"│ {title.PadRight(maxLength - 1)}│");
            Console.WriteLine("├" + new string('─', maxLength) + "┤");
            Console.ResetColor();

            foreach (var line in lines)
            {
                Console.ForegroundColor = color;
                Console.Write("│ ");
                Console.ResetColor();
                Console.Write(line.PadRight(maxLength - 1));
                Console.ForegroundColor = color;
                Console.WriteLine("│");
            }

            Console.WriteLine("└" + new string('─', maxLength) + "┘");
            Console.ResetColor();
        }

        /// <summary>
        /// Display a success message
        /// </summary>
        public static void Success(string message, string? messageJA = null)
        {
            Console.ForegroundColor = Colors.Success;
            Console.WriteLine($"{Icons.Success} {message}");
            if (!string.IsNullOrEmpty(messageJA))
            {
                Console.WriteLine($"{Icons.Success} {messageJA}");
            }
            Console.ResetColor();
        }

        /// <summary>
        /// Display an error message
        /// </summary>
        public static void Error(string message, string? messageJA = null)
        {
            Console.ForegroundColor = Colors.Error;
            Console.WriteLine($"{Icons.Error} {message}");
            if (!string.IsNullOrEmpty(messageJA))
            {
                Console.WriteLine($"{Icons.Error} {messageJA}");
            }
            Console.ResetColor();
        }

        /// <summary>
        /// Display a warning message
        /// </summary>
        public static void Warning(string message, string? messageJA = null)
        {
            Console.ForegroundColor = Colors.Warning;
            Console.WriteLine($"{Icons.Warning} {message}");
            if (!string.IsNullOrEmpty(messageJA))
            {
                Console.WriteLine($"{Icons.Warning} {messageJA}");
            }
            Console.ResetColor();
        }

        /// <summary>
        /// Display an info message
        /// </summary>
        public static void Info(string message, string? messageJA = null)
        {
            Console.ForegroundColor = Colors.Info;
            Console.WriteLine($"{Icons.Info} {message}");
            if (!string.IsNullOrEmpty(messageJA))
            {
                Console.WriteLine($"{Icons.Info} {messageJA}");
            }
            Console.ResetColor();
        }

        /// <summary>
        /// Prompt user for confirmation
        /// </summary>
        public static bool Confirm(string message, bool defaultValue = false)
        {
            var prompt = defaultValue ? "(Y/n)" : "(y/N)";
            Console.Write($"{message} {prompt}: ");
            var input = Console.ReadLine()?.Trim().ToLower();

            if (string.IsNullOrEmpty(input))
                return defaultValue;

            return input == "y" || input == "yes";
        }

        /// <summary>
        /// Prompt user for input with validation
        /// </summary>
        public static string? Prompt(string message, string? defaultValue = null, Func<string, bool>? validator = null)
        {
            while (true)
            {
                if (!string.IsNullOrEmpty(defaultValue))
                {
                    Console.Write($"{message} [{defaultValue}]: ");
                }
                else
                {
                    Console.Write($"{message}: ");
                }

                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input))
                {
                    if (defaultValue != null)
                        return defaultValue;
                    Console.ForegroundColor = Colors.Warning;
                    Console.WriteLine("Input cannot be empty. Please try again.");
                    Console.ResetColor();
                    continue;
                }

                if (validator != null && !validator(input))
                {
                    Console.ForegroundColor = Colors.Error;
                    Console.WriteLine("Invalid input. Please try again.");
                    Console.ResetColor();
                    continue;
                }

                return input;
            }
        }

        /// <summary>
        /// Display a menu and get user selection
        /// </summary>
        public static int Menu(string title, string[] options, int defaultIndex = 0)
        {
            Console.ForegroundColor = Colors.Primary;
            Console.WriteLine($"\n{title}");
            Console.WriteLine(new string('═', title.Length));
            Console.ResetColor();
            Console.WriteLine();

            for (int i = 0; i < options.Length; i++)
            {
                if (i == defaultIndex)
                {
                    Console.ForegroundColor = Colors.Highlight;
                    Console.Write($"  {i + 1}. {options[i]} ");
                    Console.ForegroundColor = Colors.Success;
                    Console.WriteLine("[DEFAULT]");
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine($"  {i + 1}. {options[i]}");
                }
            }

            while (true)
            {
                Console.Write($"\nSelect (1-{options.Length}): ");
                var input = Console.ReadLine();

                if (string.IsNullOrEmpty(input))
                    return defaultIndex;

                if (int.TryParse(input, out var choice) && choice >= 1 && choice <= options.Length)
                {
                    return choice - 1;
                }

                Console.ForegroundColor = Colors.Error;
                Console.WriteLine($"Invalid choice. Please enter 1-{options.Length}.");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Display key-value pairs in a formatted way
        /// </summary>
        public static void ShowKeyValues(Dictionary<string, string> data, int labelWidth = 25)
        {
            foreach (var kvp in data)
            {
                Console.ForegroundColor = Colors.Muted;
                Console.Write($"  {kvp.Key.PadRight(labelWidth)}: ");
                Console.ResetColor();
                Console.WriteLine(kvp.Value);
            }
        }

        /// <summary>
        /// Display a section header
        /// </summary>
        public static void SectionHeader(string title, char separator = '═')
        {
            Console.WriteLine();
            Console.ForegroundColor = Colors.Primary;
            Console.WriteLine(title);
            Console.WriteLine(new string(separator, title.Length));
            Console.ResetColor();
        }

        /// <summary>
        /// Execute an operation with a spinner
        /// </summary>
        public static async Task<T> WithSpinner<T>(string message, Func<Task<T>> operation)
        {
            using var spinner = new Spinner(message);
            return await operation();
        }

        /// <summary>
        /// Execute an operation with a spinner (void)
        /// </summary>
        public static async Task WithSpinner(string message, Func<Task> operation)
        {
            using var spinner = new Spinner(message);
            await operation();
        }

        /// <summary>
        /// Display a user-friendly error message with suggestions
        /// </summary>
        public static void FriendlyError(string errorType, string message, string? suggestion = null, string? suggestionJA = null)
        {
            Console.WriteLine();
            Console.ForegroundColor = Colors.Error;
            Console.WriteLine($"🚨 {errorType} / エラー: {errorType}");
            Console.ResetColor();

            Console.ForegroundColor = Colors.Muted;
            Console.WriteLine($"   {message}");
            Console.ResetColor();

            if (!string.IsNullOrEmpty(suggestion))
            {
                Console.WriteLine();
                Console.ForegroundColor = Colors.Info;
                Console.WriteLine($"💡 Suggestion / 提案:");
                Console.ResetColor();
                Console.WriteLine($"   {suggestion}");
            }

            if (!string.IsNullOrEmpty(suggestionJA))
            {
                Console.WriteLine($"   {suggestionJA}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Display common error types with pre-defined messages
        /// </summary>
        public static class ErrorMessages
        {
            public static void FileNotFound(string filePath)
            {
                FriendlyError(
                    "File Not Found / ファイルが見つかりません",
                    $"The specified file does not exist: {filePath}",
                    $"Please check the file path and ensure the file exists.\nTry: 'loco files search \"*.txt\"' to find files",
                    $"ファイルパスを確認し、ファイルが存在することを確かめてください。\n試行: 'loco files search \"*.txt\"' でファイルを検索"
                );
            }

            public static void PermissionDenied(string operation, string path = "")
            {
                var message = string.IsNullOrEmpty(path)
                    ? $"Permission denied for operation: {operation}"
                    : $"Permission denied for operation '{operation}' on: {path}";

                FriendlyError(
                    "Permission Denied / 権限がありません",
                    message,
                    "Run the application with appropriate permissions or as administrator.\nCheck file/folder permissions.",
                    "適切な権限でアプリケーションを実行するか、管理者として実行してください。\nファイル/フォルダの権限を確認してください。"
                );
            }

            public static void NetworkError(string operation, string details = "")
            {
                var message = string.IsNullOrEmpty(details)
                    ? $"Network error during {operation}"
                    : $"Network error during {operation}: {details}";

                FriendlyError(
                    "Network Error / ネットワークエラー",
                    message,
                    "Check your internet connection and try again.\nIf the problem persists, check firewall settings.",
                    "インターネット接続を確認してもう一度試してください。\n問題が解決しない場合は、ファイアウォール設定を確認してください。"
                );
            }

            public static void ConfigurationError(string details, string configPath = "")
            {
                var message = string.IsNullOrEmpty(configPath)
                    ? $"Configuration error: {details}"
                    : $"Configuration error in {configPath}: {details}";

                FriendlyError(
                    "Configuration Error / 設定エラー",
                    message,
                    $"Run 'loco config verify' to check configuration.\nRun 'loco config show' to view current settings.",
                    $"'loco config verify' を実行して設定を確認してください。\n'loco config show' を実行して現在の設定を表示してください。"
                );
            }

            public static void DiskSpaceError(string path, long requiredBytes)
            {
                var requiredMB = requiredBytes / (1024 * 1024);
                FriendlyError(
                    "Insufficient Disk Space / ディスク容量不足",
                    $"Not enough disk space on {path}. Required: {requiredMB} MB",
                    "Free up disk space or choose a different location.\nCheck disk usage with system tools.",
                    "ディスク容量を解放するか、別の場所を選択してください。\nシステムツールでディスク使用量を確認してください。"
                );
            }

            public static void TimeoutError(string operation, int timeoutSeconds)
            {
                FriendlyError(
                    "Operation Timeout / 操作タイムアウト",
                    $"The operation '{operation}' timed out after {timeoutSeconds} seconds",
                    $"Try increasing the timeout setting or check system performance.\nRun 'loco health' to check system status.",
                    $"タイムアウト設定を増やすか、システムパフォーマンスを確認してください。\n'loco health' を実行してシステム状態を確認してください。"
                );
            }
        }

        /// <summary>
        /// Display a success message with additional context
        /// </summary>
        public static void SuccessWithDetails(string message, string details = "", string? messageJA = null, string? detailsJA = null)
        {
            Console.WriteLine();
            Console.ForegroundColor = Colors.Success;
            Console.WriteLine($"{Icons.Success} {message}");
            Console.ResetColor();

            if (!string.IsNullOrEmpty(details))
            {
                Console.ForegroundColor = Colors.Muted;
                Console.WriteLine($"   {details}");
                Console.ResetColor();
            }

            if (!string.IsNullOrEmpty(messageJA))
            {
                Console.ForegroundColor = Colors.Success;
                Console.WriteLine($"{Icons.Success} {messageJA}");
                Console.ResetColor();
            }

            if (!string.IsNullOrEmpty(detailsJA))
            {
                Console.ForegroundColor = Colors.Muted;
                Console.WriteLine($"   {detailsJA}");
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Display a tip or hint to the user
        /// </summary>
        public static void Tip(string message, string? messageJA = null)
        {
            Console.ForegroundColor = Colors.Info;
            Console.Write($"{Icons.Info} Tip / ヒント: ");
            Console.ResetColor();
            Console.WriteLine(message);

            if (!string.IsNullOrEmpty(messageJA))
            {
                Console.ForegroundColor = Colors.Info;
                Console.Write($"{Icons.Info} ヒント: ");
                Console.ResetColor();
                Console.WriteLine(messageJA);
            }
        }

        /// <summary>
        /// Display an interactive menu and get user selection
        /// </summary>
        public static int ShowMenu(string title, string[] options, int defaultSelection = 0, string? titleJA = null)
        {
            var currentSelection = defaultSelection;
            var key = ConsoleKey.NoName;

            while (key != ConsoleKey.Enter && key != ConsoleKey.Escape)
            {
                // Clear previous menu
                if (key != ConsoleKey.NoName)
                {
                    ClearLines(options.Length + 3);
                }

                // Display title
                Console.ForegroundColor = Colors.Primary;
                Console.WriteLine(title);
                if (!string.IsNullOrEmpty(titleJA))
                {
                    Console.WriteLine(titleJA);
                }
                Console.ResetColor();
                Console.WriteLine();

                // Display options
                for (int i = 0; i < options.Length; i++)
                {
                    if (i == currentSelection)
                    {
                        Console.ForegroundColor = Colors.Highlight;
                        Console.BackgroundColor = Colors.Primary;
                        Console.WriteLine($" {Icons.Arrow} {options[i]} ");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine($"   {options[i]}");
                    }
                }

                Console.WriteLine();
                Console.ForegroundColor = Colors.Muted;
                Console.WriteLine("Use ↑↓ arrows to navigate, Enter to select, Esc to cancel");
                Console.WriteLine("↑↓キーで移動、Enterで選択、Escでキャンセル");
                Console.ResetColor();

                key = Console.ReadKey(true).Key;

                switch (key)
                {
                    case ConsoleKey.UpArrow:
                        currentSelection = Math.Max(0, currentSelection - 1);
                        break;
                    case ConsoleKey.DownArrow:
                        currentSelection = Math.Min(options.Length - 1, currentSelection + 1);
                        break;
                    case ConsoleKey.Home:
                        currentSelection = 0;
                        break;
                    case ConsoleKey.End:
                        currentSelection = options.Length - 1;
                        break;
                }
            }

            // Clear the menu
            ClearLines(options.Length + 4);

            return key == ConsoleKey.Enter ? currentSelection : -1;
        }

        /// <summary>
        /// Display a confirmation prompt
        /// </summary>
        public static bool Confirm(string message, bool defaultValue = false, string? messageJA = null)
        {
            var defaultText = defaultValue ? "[Y/n]" : "[y/N]";
            var prompt = $"{message} {defaultText}";

            while (true)
            {
                Console.ForegroundColor = Colors.Warning;
                Console.Write($"{Icons.Warning} {prompt} ");
                Console.ResetColor();

                var response = Console.ReadLine()?.Trim().ToLowerInvariant();

                if (string.IsNullOrEmpty(response))
                {
                    return defaultValue;
                }

                if (response is "y" or "yes" or "はい" or "はい" or "y")
                {
                    return true;
                }

                if (response is "n" or "no" or "いいえ" or "いいえ" or "n")
                {
                    return false;
                }

                Console.ForegroundColor = Colors.Error;
                Console.WriteLine("Please answer yes/y or no/n (はい/y または いいえ/n)");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Display a status indicator with spinner and message
        /// </summary>
        public static async Task ShowStatusAsync(string message, Func<Task> operation, string successMessage = "Completed")
        {
            using var spinner = new Spinner(message);
            try
            {
                await operation();
                spinner.Stop();
                Console.WriteLine();
                Console.ForegroundColor = Colors.Success;
                Console.WriteLine($"{Icons.Success} {successMessage}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                spinner.Stop();
                Console.WriteLine();
                FriendlyError("Operation Failed", ex.Message,
                    "Check your configuration and try again.\nReview the error details above.\nContact support if the issue persists.",
                    "設定を確認して再度お試しください。\n上記のエラー詳細を確認してください。\n問題が解決しない場合はサポートにお問い合わせください。");
                throw;
            }
        }

        /// <summary>
        /// Display a multi-step progress indicator
        /// </summary>
        public static async Task ShowMultiStepProgressAsync(string title, IEnumerable<ProgressStep> steps)
        {
            Console.WriteLine(title);
            Console.WriteLine(new string('=', title.Length));
            Console.WriteLine();

            var stepList = steps.ToList();
            for (int i = 0; i < stepList.Count; i++)
            {
                var step = stepList[i];
                Console.Write($"{i + 1}. {step.Description}... ");

                try
                {
                    await step.Operation();
                    Console.ForegroundColor = Colors.Success;
                    Console.WriteLine($"{Icons.Success} {step.SuccessMessage}");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = Colors.Error;
                    Console.WriteLine($"{Icons.Error} Failed");
                    Console.ResetColor();
                    Console.WriteLine($"   Error: {ex.Message}");

                    if (step.ContinueOnError)
                    {
                        Console.ForegroundColor = Colors.Warning;
                        Console.WriteLine($"   {Icons.Warning} Continuing with next step...");
                        Console.ResetColor();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            Console.WriteLine();
            Console.ForegroundColor = Colors.Success;
            Console.WriteLine($"{Icons.Success} All steps completed!");
            Console.ResetColor();
        }

        /// <summary>
        /// Progress step definition
        /// </summary>
        public class ProgressStep
        {
            public string Description { get; set; } = string.Empty;
            public Func<Task> Operation { get; set; } = () => Task.CompletedTask;
            public string SuccessMessage { get; set; } = "Done";
            public bool ContinueOnError { get; set; } = false;
        }

        private static void ClearLines(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.Write(new string(' ', Console.BufferWidth));
                Console.SetCursorPosition(0, Console.CursorTop);
            }
        }
    }
}
