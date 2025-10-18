using System.Diagnostics;
using System.Text;

namespace Loco.Core.UI;

/// <summary>
/// Progress bar style options.
/// </summary>
public enum ProgressBarStyle
{
    /// <summary>
    /// Simple ASCII characters: [####    ]
    /// </summary>
    Simple,

    /// <summary>
    /// Block characters: [████░░░░]
    /// </summary>
    Block,

    /// <summary>
    /// Dots: [●●●●○○○○]
    /// </summary>
    Dots,

    /// <summary>
    /// Arrows: [>>>>----]
    /// </summary>
    Arrows,

    /// <summary>
    /// Gradient blocks: [█▓▒░    ]
    /// </summary>
    Gradient
}

/// <summary>
/// Terminal-based progress bar with multiple styles and features.
/// </summary>
public class ProgressBar : IDisposable
{
    private readonly int _total;
    private readonly string _description;
    private readonly ProgressBarStyle _style;
    private readonly int _width;
    private readonly bool _showPercentage;
    private readonly bool _showEta;
    private readonly Stopwatch _stopwatch;
    private int _current;
    private bool _disposed;
    private readonly object _lock = new();
    private int _lastRenderedLength;

    public ProgressBar(
        int total,
        string description = "",
        ProgressBarStyle style = ProgressBarStyle.Block,
        int width = 40,
        bool showPercentage = true,
        bool showEta = true)
    {
        _total = total;
        _description = description;
        _style = style;
        _width = width;
        _showPercentage = showPercentage;
        _showEta = showEta;
        _current = 0;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Updates the progress bar to a specific value.
    /// </summary>
    public void Update(int current)
    {
        lock (_lock)
        {
            _current = Math.Min(current, _total);
            Render();
        }
    }

    /// <summary>
    /// Increments the progress by one.
    /// </summary>
    public void Increment()
    {
        lock (_lock)
        {
            _current = Math.Min(_current + 1, _total);
            Render();
        }
    }

    /// <summary>
    /// Increments the progress by a specific amount.
    /// </summary>
    public void IncrementBy(int amount)
    {
        lock (_lock)
        {
            _current = Math.Min(_current + amount, _total);
            Render();
        }
    }

    /// <summary>
    /// Completes the progress bar.
    /// </summary>
    public void Complete()
    {
        lock (_lock)
        {
            _current = _total;
            Render();
            Console.WriteLine(); // Move to next line
        }
    }

    /// <summary>
    /// Renders the progress bar to the console.
    /// </summary>
    private void Render()
    {
        var percentage = _total > 0 ? (double)_current / _total : 0;
        var filledWidth = (int)(_width * percentage);

        var sb = new StringBuilder();

        // Description
        if (!string.IsNullOrEmpty(_description))
        {
            sb.Append(_description);
            sb.Append(": ");
        }

        // Progress bar
        sb.Append('[');
        sb.Append(GetFilledPart(filledWidth));
        sb.Append(GetEmptyPart(_width - filledWidth));
        sb.Append(']');

        // Percentage
        if (_showPercentage)
        {
            sb.Append($" {percentage * 100:F1}%");
        }

        // Count
        sb.Append($" ({_current}/{_total})");

        // ETA
        if (_showEta && _current > 0 && _current < _total)
        {
            var elapsed = _stopwatch.Elapsed;
            var rate = _current / elapsed.TotalSeconds;
            var remaining = (_total - _current) / rate;
            var eta = TimeSpan.FromSeconds(remaining);

            sb.Append($" ETA: {FormatTimeSpan(eta)}");
        }

        // Elapsed time
        if (_current == _total)
        {
            sb.Append($" Done in {FormatTimeSpan(_stopwatch.Elapsed)}");
        }

        var output = sb.ToString();

        // Clear previous line and write new one
        Console.Write("\r" + output);

        // Pad with spaces to clear previous longer output
        var currentLength = output.Length;
        if (currentLength < _lastRenderedLength)
        {
            Console.Write(new string(' ', _lastRenderedLength - currentLength));
        }
        _lastRenderedLength = currentLength;
    }

    /// <summary>
    /// Gets the filled portion of the progress bar based on style.
    /// </summary>
    private string GetFilledPart(int width)
    {
        return _style switch
        {
            ProgressBarStyle.Simple => new string('#', width),
            ProgressBarStyle.Block => new string('█', width),
            ProgressBarStyle.Dots => new string('●', width),
            ProgressBarStyle.Arrows => new string('>', width),
            ProgressBarStyle.Gradient => GetGradientFilled(width),
            _ => new string('#', width)
        };
    }

    /// <summary>
    /// Gets the empty portion of the progress bar based on style.
    /// </summary>
    private string GetEmptyPart(int width)
    {
        return _style switch
        {
            ProgressBarStyle.Simple => new string(' ', width),
            ProgressBarStyle.Block => new string('░', width),
            ProgressBarStyle.Dots => new string('○', width),
            ProgressBarStyle.Arrows => new string('-', width),
            ProgressBarStyle.Gradient => new string(' ', width),
            _ => new string(' ', width)
        };
    }

    /// <summary>
    /// Gets gradient-style filled portion.
    /// </summary>
    private string GetGradientFilled(int width)
    {
        var sb = new StringBuilder();
        var chars = new[] { '█', '▓', '▒', '░' };

        for (int i = 0; i < width; i++)
        {
            var position = (double)i / width;
            var charIndex = (int)(position * (chars.Length - 1));
            sb.Append(chars[Math.Min(charIndex, chars.Length - 1)]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a TimeSpan for display.
    /// </summary>
    private string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _stopwatch.Stop();
            _disposed = true;
        }
    }
}

/// <summary>
/// Multi-progress bar manager for tracking multiple concurrent operations.
/// </summary>
public class MultiProgressBar : IDisposable
{
    private readonly Dictionary<string, ProgressBarState> _bars = new();
    private readonly object _lock = new();
    private bool _disposed;
    private readonly Timer _renderTimer;

    private class ProgressBarState
    {
        public string Id { get; set; } = "";
        public string Description { get; set; } = "";
        public int Current { get; set; }
        public int Total { get; set; }
        public Stopwatch Stopwatch { get; set; } = new();
        public bool Completed { get; set; }
    }

    public MultiProgressBar(int refreshIntervalMs = 100)
    {
        _renderTimer = new Timer(_ => Render(), null, 0, refreshIntervalMs);
    }

    /// <summary>
    /// Adds a new progress bar.
    /// </summary>
    public void Add(string id, string description, int total)
    {
        lock (_lock)
        {
            _bars[id] = new ProgressBarState
            {
                Id = id,
                Description = description,
                Total = total,
                Current = 0,
                Stopwatch = Stopwatch.StartNew(),
                Completed = false
            };
        }
    }

    /// <summary>
    /// Updates progress for a specific bar.
    /// </summary>
    public void Update(string id, int current)
    {
        lock (_lock)
        {
            if (_bars.TryGetValue(id, out var bar))
            {
                bar.Current = Math.Min(current, bar.Total);
            }
        }
    }

    /// <summary>
    /// Increments progress for a specific bar.
    /// </summary>
    public void Increment(string id)
    {
        lock (_lock)
        {
            if (_bars.TryGetValue(id, out var bar))
            {
                bar.Current = Math.Min(bar.Current + 1, bar.Total);
            }
        }
    }

    /// <summary>
    /// Marks a progress bar as completed.
    /// </summary>
    public void Complete(string id)
    {
        lock (_lock)
        {
            if (_bars.TryGetValue(id, out var bar))
            {
                bar.Current = bar.Total;
                bar.Completed = true;
                bar.Stopwatch.Stop();
            }
        }
    }

    /// <summary>
    /// Renders all progress bars.
    /// </summary>
    private void Render()
    {
        lock (_lock)
        {
            // Move cursor to top
            Console.SetCursorPosition(0, Console.CursorTop - _bars.Count);

            foreach (var bar in _bars.Values)
            {
                var percentage = bar.Total > 0 ? (double)bar.Current / bar.Total : 0;
                var filledWidth = (int)(30 * percentage);
                var filled = new string('█', filledWidth);
                var empty = new string('░', 30 - filledWidth);

                var status = bar.Completed ? "✓" : "▶";
                var line = $"{status} {bar.Description,-20} [{filled}{empty}] {percentage * 100:F0}% ({bar.Current}/{bar.Total})";

                // Pad to clear previous content
                Console.WriteLine(line.PadRight(Console.WindowWidth - 1));
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _renderTimer?.Dispose();
            _disposed = true;
            Console.WriteLine(); // Move to next line after all bars
        }
    }
}

/// <summary>
/// Spinner for indeterminate progress.
/// </summary>
public class Spinner : IDisposable
{
    private readonly string _description;
    private readonly string[] _frames;
    private readonly int _intervalMs;
    private readonly Timer _timer;
    private int _currentFrame;
    private bool _disposed;

    private static readonly string[][] SpinnerStyles = new[]
    {
        new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" }, // Dots
        new[] { "◐", "◓", "◑", "◒" }, // Circle
        new[] { "▖", "▘", "▝", "▗" }, // Box
        new[] { "|", "/", "-", "\\" }, // Line
        new[] { "●", "○" }, // Simple
    };

    public Spinner(string description = "", int style = 0, int intervalMs = 80)
    {
        _description = description;
        _frames = SpinnerStyles[Math.Min(style, SpinnerStyles.Length - 1)];
        _intervalMs = intervalMs;
        _currentFrame = 0;

        _timer = new Timer(_ => Render(), null, 0, _intervalMs);
    }

    private void Render()
    {
        if (_disposed) return;

        var frame = _frames[_currentFrame];
        _currentFrame = (_currentFrame + 1) % _frames.Length;

        var output = string.IsNullOrEmpty(_description)
            ? $"\r{frame} "
            : $"\r{frame} {_description}";

        Console.Write(output);
    }

    public void Stop(string finalMessage = "")
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);

        var message = string.IsNullOrEmpty(finalMessage) ? "Done" : finalMessage;
        Console.Write($"\r✓ {_description} {message}".PadRight(Console.WindowWidth - 1));
        Console.WriteLine();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _timer?.Dispose();
            _disposed = true;
        }
    }
}
