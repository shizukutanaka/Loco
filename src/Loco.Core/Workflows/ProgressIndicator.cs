using System.Diagnostics;

namespace Loco.Core.Workflows;

/// <summary>
/// Displays progress indicators for long-running workflow steps.
/// </summary>
public class ProgressIndicator : IDisposable
{
    private readonly string _stepName;
    private readonly CancellationTokenSource _cts;
    private Task? _spinnerTask;
    private readonly Stopwatch _stopwatch;
    private bool _isDisposed;

    private static readonly string[] SpinnerFrames = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
    private int _frameIndex;

    public ProgressIndicator(string stepName)
    {
        _stepName = stepName;
        _cts = new CancellationTokenSource();
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Starts the progress indicator animation.
    /// </summary>
    public void Start()
    {
        if (_spinnerTask != null)
            return;

        _spinnerTask = Task.Run(async () =>
        {
            try
            {
                Console.CursorVisible = false;

                while (!_cts.Token.IsCancellationRequested)
                {
                    var elapsed = _stopwatch.Elapsed;
                    var frame = SpinnerFrames[_frameIndex % SpinnerFrames.Length];

                    Console.Write($"\r  {frame} {_stepName} [{elapsed.TotalSeconds:F1}s]");

                    _frameIndex++;

                    await Task.Delay(100, _cts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when stopped
            }
            finally
            {
                Console.CursorVisible = true;
            }
        }, _cts.Token);
    }

    /// <summary>
    /// Stops the progress indicator.
    /// </summary>
    public void Stop(bool success = true)
    {
        if (_spinnerTask == null)
            return;

        _cts.Cancel();

        try
        {
            _spinnerTask.Wait(500);
        }
        catch
        {
            // Ignore
        }

        _stopwatch.Stop();

        // Clear the line
        Console.Write("\r" + new string(' ', Console.BufferWidth - 1) + "\r");

        // Show final status
        var elapsed = _stopwatch.Elapsed;
        var icon = success ? "✓" : "✗";
        var color = success ? ConsoleColor.Green : ConsoleColor.Red;

        Console.ForegroundColor = color;
        Console.WriteLine($"  {icon} {_stepName} [{elapsed.TotalSeconds:F1}s]");
        Console.ResetColor();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        Stop(false);
        _cts.Dispose();
        _isDisposed = true;
    }
}

/// <summary>
/// Progress bar for showing completion percentage.
/// </summary>
public class ProgressBar
{
    private readonly int _total;
    private int _current;
    private readonly string _description;

    public ProgressBar(int total, string description = "Progress")
    {
        _total = total;
        _current = 0;
        _description = description;
    }

    /// <summary>
    /// Updates the progress bar.
    /// </summary>
    public void Update(int current)
    {
        _current = current;
        Draw();
    }

    /// <summary>
    /// Increments progress by one.
    /// </summary>
    public void Increment()
    {
        _current++;
        Draw();
    }

    private void Draw()
    {
        var percentage = _total > 0 ? (double)_current / _total : 0;
        var barWidth = 40;
        var filled = (int)(percentage * barWidth);

        var bar = new string('█', filled) + new string('░', barWidth - filled);

        Console.Write($"\r  {_description}: [{bar}] {percentage:P0} ({_current}/{_total})");

        if (_current >= _total)
        {
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Completes the progress bar.
    /// </summary>
    public void Complete()
    {
        _current = _total;
        Draw();
    }
}
