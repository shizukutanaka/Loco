using System;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Cli.UI;

/// <summary>
/// コンソールベースのプログレスバーコンポーネント
/// </summary>
public class ProgressBar : IDisposable
{
    private readonly int _totalWidth;
    private readonly char _progressChar;
    private readonly char _backgroundChar;
    private readonly ConsoleColor _progressColor;
    private readonly ConsoleColor _backgroundColor;
    private readonly string _prefix;
    private readonly string _suffix;
    private int _currentValue;
    private int _maximumValue;
    private bool _isVisible;
    private readonly object _lock = new();

    /// <summary>
    /// プログレスバーを作成
    /// </summary>
    /// <param name="totalWidth">バーの総幅</param>
    /// <param name="progressChar">プログレス部分の文字</param>
    /// <param name="backgroundChar">背景部分の文字</param>
    /// <param name="progressColor">プログレス部分の色</param>
    /// <param name="backgroundColor">背景部分の色</param>
    /// <param name="prefix">プレフィックステキスト</param>
    /// <param name="suffix">サフィックステキスト</param>
    public ProgressBar(
        int totalWidth = 50,
        char progressChar = '█',
        char backgroundChar = '░',
        ConsoleColor progressColor = ConsoleColor.Green,
        ConsoleColor backgroundColor = ConsoleColor.DarkGray,
        string prefix = "",
        string suffix = "")
    {
        _totalWidth = Math.Max(10, totalWidth);
        _progressChar = progressChar;
        _backgroundChar = backgroundChar;
        _progressColor = progressColor;
        _backgroundColor = backgroundColor;
        _prefix = prefix;
        _suffix = suffix;
        _currentValue = 0;
        _maximumValue = 100;
    }

    /// <summary>
    /// プログレスバーを表示
    /// </summary>
    public void Show()
    {
        lock (_lock)
        {
            if (_isVisible) return;
            _isVisible = true;
            UpdateDisplay();
        }
    }

    /// <summary>
    /// プログレスバーを非表示
    /// </summary>
    public void Hide()
    {
        lock (_lock)
        {
            if (!_isVisible) return;
            _isVisible = false;
            Console.WriteLine(); // 改行して次の行へ
        }
    }

    /// <summary>
    /// 進捗値を設定
    /// </summary>
    /// <param name="value">現在の値</param>
    /// <param name="maximum">最大値</param>
    public void SetProgress(int value, int maximum = 100)
    {
        lock (_lock)
        {
            _currentValue = Math.Max(0, Math.Min(value, maximum));
            _maximumValue = Math.Max(1, maximum);

            if (_isVisible)
            {
                UpdateDisplay();
            }
        }
    }

    /// <summary>
    /// 進捗を1ステップ進める
    /// </summary>
    public void Step()
    {
        SetProgress(_currentValue + 1, _maximumValue);
    }

    /// <summary>
    /// パーセントで進捗を設定
    /// </summary>
    /// <param name="percentage">パーセント (0-100)</param>
    public void SetPercentage(int percentage)
    {
        SetProgress(percentage, 100);
    }

    /// <summary>
    /// 進捗率を取得
    /// </summary>
    public double GetProgressRatio() => (double)_currentValue / _maximumValue;

    /// <summary>
    /// パーセントを取得
    /// </summary>
    public int GetPercentage() => (int)(GetProgressRatio() * 100);

    private void UpdateDisplay()
    {
        var ratio = GetProgressRatio();
        var filledWidth = (int)(_totalWidth * ratio);
        var emptyWidth = _totalWidth - filledWidth;

        var progressBar = new string(_progressChar, filledWidth) + new string(_backgroundChar, emptyWidth);
        var percentage = $"{GetPercentage(),3}%";

        // 現在の行をクリア
        Console.Write("\r");

        // プレフィックス
        if (!string.IsNullOrEmpty(_prefix))
        {
            Console.Write(_prefix);
        }

        // プログレスバー
        Console.ForegroundColor = _progressColor;
        Console.Write(progressBar[..filledWidth]);
        Console.ForegroundColor = _backgroundColor;
        Console.Write(progressBar[filledWidth..]);
        Console.ResetColor();

        // パーセント
        Console.Write($" {percentage}");

        // サフィックス
        if (!string.IsNullOrEmpty(_suffix))
        {
            Console.Write($" {_suffix}");
        }

        // 行末までクリア
        Console.Write(new string(' ', Console.WindowWidth - Console.CursorLeft - 1));
    }

    /// <summary>
    /// プログレスバーを使用した処理を実行
    /// </summary>
    /// <param name="action">実行するアクション</param>
    /// <param name="totalSteps">総ステップ数</param>
    public static async Task RunWithProgressAsync(Func<IProgress<int>, Task> action, int totalSteps = 100)
    {
        var progressBar = new ProgressBar();
        var progress = new Progress<int>(value => progressBar.SetProgress(value, totalSteps));

        progressBar.Show();
        try
        {
            await action(progress);
        }
        finally
        {
            progressBar.Hide();
        }
    }

    /// <summary>
    /// プログレスバーを使用した同期処理を実行
    /// </summary>
    /// <param name="action">実行するアクション</param>
    /// <param name="totalSteps">総ステップ数</param>
    public static void RunWithProgress(Action<IProgress<int>> action, int totalSteps = 100)
    {
        var progressBar = new ProgressBar();
        var progress = new Progress<int>(value => progressBar.SetProgress(value, totalSteps));

        progressBar.Show();
        try
        {
            action(progress);
        }
        finally
        {
            progressBar.Hide();
        }
    }

    public void Dispose()
    {
        Hide();
    }
}

/// <summary>
/// プログレスバーの拡張メソッド
/// </summary>
public static class ProgressBarExtensions
{
    /// <summary>
    /// プログレスバー付きでファイルをコピー
    /// </summary>
    public static async Task CopyFileWithProgressAsync(
        this ProgressBar progressBar,
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        const int bufferSize = 81920; // 80KB buffer
        var fileInfo = new FileInfo(sourcePath);

        progressBar.Show();

        using var sourceStream = File.OpenRead(sourcePath);
        using var destinationStream = File.Create(destinationPath);

        var buffer = new byte[bufferSize];
        long totalBytesRead = 0;
        int bytesRead;

        while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destinationStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;

            var percentage = (int)((double)totalBytesRead / fileInfo.Length * 100);
            progressBar.SetPercentage(percentage);
        }

        progressBar.Hide();
    }
}
