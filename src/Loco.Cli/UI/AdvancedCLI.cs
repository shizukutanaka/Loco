using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Cli.UI;

/// <summary>
/// 進歩的開示を実装した高度なCLI
/// Advanced CLI with progressive disclosure
///
/// 機能: 対話モード、進捗表示、アニメーション
/// Features: Interactive mode, progress display, animations
/// </summary>
public class AdvancedCLI
{
    private readonly bool _isInteractive;
    private readonly bool _useColor;
    private int _indentLevel;

    public AdvancedCLI()
    {
        _isInteractive = !Console.IsOutputRedirected && Environment.UserInteractive;
        _useColor = _isInteractive && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TERM"));
    }

    public enum IconType
    {
        Success,
        Error,
        Warning,
        Info,
        Question,
        Loading,
        Arrow,
        Bullet
    }

    /// <summary>
    /// アイコン付きメッセージを表示
    /// Display message with icon
    /// </summary>
    public void WriteIcon(IconType icon, string message, ConsoleColor? color = null)
    {
        var iconChar = icon switch
        {
            IconType.Success => "✓",
            IconType.Error => "✗",
            IconType.Warning => "⚠",
            IconType.Info => "ℹ",
            IconType.Question => "?",
            IconType.Loading => "⋯",
            IconType.Arrow => "→",
            IconType.Bullet => "•",
            _ => " "
        };

        var defaultColor = icon switch
        {
            IconType.Success => ConsoleColor.Green,
            IconType.Error => ConsoleColor.Red,
            IconType.Warning => ConsoleColor.Yellow,
            IconType.Info => ConsoleColor.Cyan,
            IconType.Question => ConsoleColor.Magenta,
            _ => ConsoleColor.White
        };

        var indent = new string(' ', _indentLevel * 2);

        if (_useColor && color == null)
        {
            color = defaultColor;
        }

        if (_useColor && color.HasValue)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color.Value;
            Console.Write($"{indent}{iconChar} ");
            Console.ForegroundColor = originalColor;
            Console.WriteLine(message);
        }
        else
        {
            Console.WriteLine($"{indent}{iconChar} {message}");
        }
    }

    /// <summary>
    /// 進捗バーを表示
    /// Display progress bar
    /// </summary>
    public class ProgressBar : IDisposable
    {
        private readonly int _total;
        private readonly string _label;
        private readonly bool _isInteractive;
        private int _current;
        private readonly DateTime _startTime;

        public ProgressBar(string label, int total, bool isInteractive)
        {
            _label = label;
            _total = total;
            _isInteractive = isInteractive;
            _current = 0;
            _startTime = DateTime.UtcNow;

            if (_isInteractive)
            {
                Console.CursorVisible = false;
                Render();
            }
        }

        public void Increment(string? currentItem = null)
        {
            _current++;
            if (_isInteractive)
            {
                Render(currentItem);
            }
            else if (_current % Math.Max(1, _total / 10) == 0)
            {
                Console.WriteLine($"{_label}: {_current}/{_total} ({_current * 100 / _total}%)");
            }
        }

        private void Render(string? currentItem = null)
        {
            if (!_isInteractive) return;

            Console.SetCursorPosition(0, Console.CursorTop);

            var percent = _total > 0 ? (double)_current / _total : 0;
            var barWidth = 30;
            var filled = (int)(barWidth * percent);

            var bar = new string('█', filled) + new string('░', barWidth - filled);
            var percentText = $"{percent:P0}".PadLeft(4);

            var elapsed = DateTime.UtcNow - _startTime;
            var eta = _current > 0
                ? TimeSpan.FromTicks(elapsed.Ticks * (_total - _current) / _current)
                : TimeSpan.Zero;

            var status = currentItem != null && currentItem.Length <= 30
                ? currentItem.PadRight(30)
                : "";

            Console.Write($"{_label} [{bar}] {percentText} {_current}/{_total} ");
            if (_current > 0 && _current < _total)
            {
                Console.Write($"ETA {eta:mm\\:ss} ");
            }
            if (!string.IsNullOrEmpty(status))
            {
                Console.Write($"- {status}");
            }

            // Clear to end of line
            var currentPos = Console.CursorLeft;
            var width = Console.WindowWidth;
            if (currentPos < width - 1)
            {
                Console.Write(new string(' ', width - currentPos - 1));
            }
        }

        public void Dispose()
        {
            if (_isInteractive)
            {
                Render();
                Console.WriteLine();
                Console.CursorVisible = true;
            }
            else
            {
                Console.WriteLine($"{_label}: Completed {_current}/{_total}");
            }
        }
    }

    /// <summary>
    /// スピナーアニメーションを表示
    /// Display spinner animation
    /// </summary>
    public class Spinner : IDisposable
    {
        private readonly string _label;
        private readonly bool _isInteractive;
        private readonly CancellationTokenSource _cts;
        private readonly Task _spinTask;
        private readonly string[] _frames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        private int _frameIndex;

        public Spinner(string label, bool isInteractive)
        {
            _label = label;
            _isInteractive = isInteractive;
            _cts = new CancellationTokenSource();

            if (_isInteractive)
            {
                Console.CursorVisible = false;
                _spinTask = Task.Run(async () =>
                {
                    while (!_cts.Token.IsCancellationRequested)
                    {
                        Render();
                        await Task.Delay(80, _cts.Token).ConfigureAwait(false);
                    }
                });
            }
            else
            {
                Console.WriteLine($"{_label}...");
                _spinTask = Task.CompletedTask;
            }
        }

        private void Render()
        {
            if (!_isInteractive) return;

            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write($"{_frames[_frameIndex]} {_label}");

            // Clear to end of line
            var currentPos = Console.CursorLeft;
            var width = Console.WindowWidth;
            if (currentPos < width - 1)
            {
                Console.Write(new string(' ', width - currentPos - 1));
            }

            _frameIndex = (_frameIndex + 1) % _frames.Length;
        }

        public void Dispose()
        {
            if (_isInteractive)
            {
                _cts.Cancel();
                _spinTask.Wait(200);
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write(new string(' ', Console.WindowWidth - 1));
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.CursorVisible = true;
            }
            _cts.Dispose();
        }
    }

    /// <summary>
    /// 対話的な確認プロンプト
    /// Interactive confirmation prompt
    /// </summary>
    public bool Confirm(string question, bool defaultValue = false)
    {
        if (!_isInteractive)
        {
            return defaultValue;
        }

        var suffix = defaultValue ? "[Y/n]" : "[y/N]";
        Console.Write($"? {question} {suffix}: ");

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            Console.WriteLine();

            if (key.Key == ConsoleKey.Enter)
            {
                return defaultValue;
            }

            if (key.KeyChar == 'y' || key.KeyChar == 'Y')
            {
                return true;
            }

            if (key.KeyChar == 'n' || key.KeyChar == 'N')
            {
                return false;
            }

            Console.Write($"Please enter y or n {suffix}: ");
        }
    }

    /// <summary>
    /// 対話的な選択プロンプト
    /// Interactive selection prompt
    /// </summary>
    public int Select(string question, List<string> options, int defaultIndex = 0)
    {
        if (!_isInteractive)
        {
            return defaultIndex;
        }

        Console.WriteLine($"? {question}");

        var selectedIndex = defaultIndex;
        RenderOptions(options, selectedIndex);

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex = selectedIndex > 0 ? selectedIndex - 1 : options.Count - 1;
                    RenderOptions(options, selectedIndex);
                    break;

                case ConsoleKey.DownArrow:
                    selectedIndex = selectedIndex < options.Count - 1 ? selectedIndex + 1 : 0;
                    RenderOptions(options, selectedIndex);
                    break;

                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return selectedIndex;

                case ConsoleKey.Escape:
                    Console.WriteLine();
                    return defaultIndex;
            }
        }
    }

    private void RenderOptions(List<string> options, int selectedIndex)
    {
        Console.SetCursorPosition(0, Console.CursorTop - options.Count);

        for (var i = 0; i < options.Count; i++)
        {
            if (i == selectedIndex)
            {
                if (_useColor)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                }
                Console.WriteLine($"❯ {options[i].PadRight(Console.WindowWidth - 3)}");
                if (_useColor)
                {
                    Console.ResetColor();
                }
            }
            else
            {
                Console.WriteLine($"  {options[i].PadRight(Console.WindowWidth - 3)}");
            }
        }
    }

    /// <summary>
    /// 対話的なテキスト入力プロンプト
    /// Interactive text input prompt
    /// </summary>
    public string Input(string question, string? defaultValue = null, Func<string, string?>? validator = null)
    {
        if (!_isInteractive)
        {
            return defaultValue ?? "";
        }

        var suffix = !string.IsNullOrEmpty(defaultValue) ? $"({defaultValue})" : "";
        Console.Write($"? {question} {suffix}: ");

        while (true)
        {
            var input = Console.ReadLine() ?? "";

            if (string.IsNullOrWhiteSpace(input) && !string.IsNullOrEmpty(defaultValue))
            {
                return defaultValue;
            }

            if (validator != null)
            {
                var error = validator(input);
                if (error != null)
                {
                    WriteIcon(IconType.Error, error, ConsoleColor.Red);
                    Console.Write($"? {question} {suffix}: ");
                    continue;
                }
            }

            return input;
        }
    }

    /// <summary>
    /// インデントレベルを変更
    /// Change indent level
    /// </summary>
    public void Indent() => _indentLevel++;
    public void Unindent() => _indentLevel = Math.Max(0, _indentLevel - 1);

    /// <summary>
    /// セクションヘッダーを表示
    /// Display section header
    /// </summary>
    public void Section(string title)
    {
        Console.WriteLine();
        if (_useColor)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"═══ {title} ═══");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine($"=== {title} ===");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// 進捗バーを作成
    /// Create progress bar
    /// </summary>
    public ProgressBar CreateProgress(string label, int total)
    {
        return new ProgressBar(label, total, _isInteractive);
    }

    /// <summary>
    /// スピナーを作成
    /// Create spinner
    /// </summary>
    public Spinner CreateSpinner(string label)
    {
        return new Spinner(label, _isInteractive);
    }

    /// <summary>
    /// 長時間実行タスクを実行
    /// Execute long-running task with feedback
    /// </summary>
    public async Task<T> WithSpinnerAsync<T>(string label, Func<Task<T>> task)
    {
        using var spinner = CreateSpinner(label);
        try
        {
            var result = await task().ConfigureAwait(false);
            spinner.Dispose();
            WriteIcon(IconType.Success, label);
            return result;
        }
        catch (Exception ex)
        {
            spinner.Dispose();
            WriteIcon(IconType.Error, $"{label}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// テーブル形式でデータを表示
    /// Display data in table format
    /// </summary>
    public void Table<T>(List<T> items, params (string Header, Func<T, string> Selector)[] columns)
    {
        if (items.Count == 0)
        {
            WriteIcon(IconType.Info, "No items to display");
            return;
        }

        // Calculate column widths
        var widths = columns.Select((col, i) =>
        {
            var headerWidth = col.Header.Length;
            var dataWidth = items.Max(item => col.Selector(item).Length);
            return Math.Max(headerWidth, dataWidth) + 2;
        }).ToArray();

        // Print header
        if (_useColor)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
        }
        for (var i = 0; i < columns.Length; i++)
        {
            Console.Write(columns[i].Header.PadRight(widths[i]));
        }
        Console.WriteLine();

        // Print separator
        for (var i = 0; i < columns.Length; i++)
        {
            Console.Write(new string('─', widths[i]));
        }
        Console.WriteLine();
        if (_useColor)
        {
            Console.ResetColor();
        }

        // Print rows
        foreach (var item in items)
        {
            for (var i = 0; i < columns.Length; i++)
            {
                Console.Write(columns[i].Selector(item).PadRight(widths[i]));
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// エラーメッセージを表示
    /// Display error message
    /// </summary>
    public void Error(string message, Exception? ex = null)
    {
        WriteIcon(IconType.Error, message, ConsoleColor.Red);
        if (ex != null && _isInteractive)
        {
            Indent();
            WriteIcon(IconType.Bullet, $"Type: {ex.GetType().Name}");
            WriteIcon(IconType.Bullet, $"Message: {ex.Message}");
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                WriteIcon(IconType.Bullet, "Stack trace available (use --verbose for details)");
            }
            Unindent();
        }
    }

    /// <summary>
    /// 成功メッセージを表示
    /// Display success message
    /// </summary>
    public void Success(string message)
    {
        WriteIcon(IconType.Success, message, ConsoleColor.Green);
    }

    /// <summary>
    /// 警告メッセージを表示
    /// Display warning message
    /// </summary>
    public void Warning(string message)
    {
        WriteIcon(IconType.Warning, message, ConsoleColor.Yellow);
    }

    /// <summary>
    /// 情報メッセージを表示
    /// Display info message
    /// </summary>
    public void Info(string message)
    {
        WriteIcon(IconType.Info, message, ConsoleColor.Cyan);
    }
}
