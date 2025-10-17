using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loco.Core.Testing;

/// <summary>
/// 統合テストフレームワーク
/// 問題: 「テスト不足で本番が壊れる」（企業デプロイの最大問題）
/// 解決: 包括的な統合テストで本番前に問題を検出
/// </summary>
public class IntegrationTestFramework
{
    public class TestCase
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public Func<Task<TestResult>> TestAction { get; set; } = null!;
        public bool IsRequired { get; set; } = true;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    }

    public class TestResult
    {
        public string TestId { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string MessageJa { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string? Exception { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    public class TestSuite
    {
        public string Name { get; set; } = string.Empty;
        public List<TestCase> Tests { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan TotalDuration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;
        public List<TestResult> Results { get; set; } = new();

        public int TotalTests => Tests.Count;
        public int PassedTests => Results.Count(r => r.Success);
        public int FailedTests => Results.Count(r => !r.Success);
        public double SuccessRate => TotalTests > 0 ? (double)PassedTests / TotalTests * 100 : 0;
        public bool AllTestsPassed => FailedTests == 0;
    }

    private readonly List<TestCase> _testCases = new();

    /// <summary>
    /// テストケースを登録
    /// </summary>
    public void RegisterTest(string name, string category, Func<Task<TestResult>> testAction,
        bool isRequired = true, TimeSpan? timeout = null)
    {
        _testCases.Add(new TestCase
        {
            Name = name,
            Category = category,
            TestAction = testAction,
            IsRequired = isRequired,
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        });
    }

    /// <summary>
    /// すべてのテストを実行
    /// </summary>
    public async Task<TestSuite> RunAllTestsAsync()
    {
        var suite = new TestSuite
        {
            Name = "Integration Test Suite",
            Tests = _testCases,
            StartTime = DateTime.UtcNow
        };

        foreach (var testCase in _testCases)
        {
            var result = await RunTestAsync(testCase).ConfigureAwait(false);
            suite.Results.Add(result);
        }

        suite.EndTime = DateTime.UtcNow;
        return suite;
    }

    /// <summary>
    /// 単一のテストを実行
    /// </summary>
    private async Task<TestResult> RunTestAsync(TestCase testCase)
    {
        var sw = Stopwatch.StartNew();
        var result = new TestResult
        {
            TestId = testCase.Id,
            TestName = testCase.Name
        };

        try
        {
            // タイムアウト付きで実行
            var task = testCase.TestAction();
            if (await Task.WhenAny(task, Task.Delay(testCase.Timeout)).ConfigureAwait(false) == task)
            {
                var testResult = await task.ConfigureAwait(false);
                result.Success = testResult.Success;
                result.Message = testResult.Message;
                result.MessageJa = testResult.MessageJa;
                result.Metadata = testResult.Metadata;
            }
            else
            {
                result.Success = false;
                result.Message = $"Test timed out after {testCase.Timeout.TotalSeconds}s";
                result.MessageJa = $"テストが{testCase.Timeout.TotalSeconds}秒でタイムアウトしました";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Test failed with exception: {ex.Message}";
            result.MessageJa = $"テストが例外で失敗しました: {ex.Message}";
            result.Exception = ex.ToString();
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    /// <summary>
    /// デフォルトの統合テストを登録
    /// </summary>
    public void RegisterDefaultTests()
    {
        // 1. ディレクトリアクセステスト
        RegisterTest(
            "Directory Access Test",
            "File System",
            async () =>
            {
                var testDirs = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loco"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loco", "logs"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loco", "config")
                };

                foreach (var dir in testDirs)
                {
                    Directory.CreateDirectory(dir);
                    if (!Directory.Exists(dir))
                    {
                        return new TestResult
                        {
                            Success = false,
                            Message = $"Failed to create directory: {dir}",
                            MessageJa = $"ディレクトリの作成に失敗しました: {dir}"
                        };
                    }
                }

                await Task.CompletedTask;
                return new TestResult
                {
                    Success = true,
                    Message = "All directories accessible",
                    MessageJa = "すべてのディレクトリにアクセス可能"
                };
            }
        );

        // 2. ファイル読み書きテスト
        RegisterTest(
            "File Read/Write Test",
            "File System",
            async () =>
            {
                var testFile = Path.Combine(Path.GetTempPath(), $"loco-test-{Guid.NewGuid()}.tmp");

                try
                {
                    // 書き込み
                    await File.WriteAllTextAsync(testFile, "test content").ConfigureAwait(false);

                    // 読み込み
                    var content = await File.ReadAllTextAsync(testFile).ConfigureAwait(false);

                    // 削除
                    File.Delete(testFile);

                    if (content != "test content")
                    {
                        return new TestResult
                        {
                            Success = false,
                            Message = "File content mismatch",
                            MessageJa = "ファイル内容が一致しません"
                        };
                    }

                    return new TestResult
                    {
                        Success = true,
                        Message = "File read/write successful",
                        MessageJa = "ファイル読み書き成功"
                    };
                }
                catch (Exception ex)
                {
                    return new TestResult
                    {
                        Success = false,
                        Message = $"File operation failed: {ex.Message}",
                        MessageJa = $"ファイル操作が失敗しました: {ex.Message}"
                    };
                }
                finally
                {
                    if (File.Exists(testFile))
                    {
                        File.Delete(testFile);
                    }
                }
            }
        );

        // 3. メモリテスト
        RegisterTest(
            "Memory Availability Test",
            "System Resources",
            async () =>
            {
                var process = Process.GetCurrentProcess();
                var memoryMB = process.WorkingSet64 / 1024.0 / 1024.0;

                await Task.CompletedTask;

                if (memoryMB > 1024)
                {
                    return new TestResult
                    {
                        Success = false,
                        Message = $"Memory usage too high: {memoryMB:F0}MB",
                        MessageJa = $"メモリ使用量が多すぎます: {memoryMB:F0}MB",
                        Metadata = new Dictionary<string, string>
                        {
                            ["MemoryMB"] = memoryMB.ToString("F0")
                        }
                    };
                }

                return new TestResult
                {
                    Success = true,
                    Message = $"Memory usage normal: {memoryMB:F0}MB",
                    MessageJa = $"メモリ使用量は正常です: {memoryMB:F0}MB",
                    Metadata = new Dictionary<string, string>
                    {
                        ["MemoryMB"] = memoryMB.ToString("F0")
                    }
                };
            }
        );

        // 4. 並行処理テスト
        RegisterTest(
            "Concurrent Execution Test",
            "Performance",
            async () =>
            {
                var tasks = Enumerable.Range(0, 10).Select(async i =>
                {
                    await Task.Delay(100).ConfigureAwait(false);
                    return i;
                });

                var results = await Task.WhenAll(tasks).ConfigureAwait(false);

                if (results.Length != 10)
                {
                    return new TestResult
                    {
                        Success = false,
                        Message = "Concurrent execution failed",
                        MessageJa = "並行実行が失敗しました"
                    };
                }

                return new TestResult
                {
                    Success = true,
                    Message = "Concurrent execution successful",
                    MessageJa = "並行実行が成功しました"
                };
            }
        );

        // 5. JSON シリアライズテスト
        RegisterTest(
            "JSON Serialization Test",
            "Data Processing",
            async () =>
            {
                var testData = new Dictionary<string, object>
                {
                    ["string"] = "test",
                    ["number"] = 123,
                    ["boolean"] = true
                };

                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(testData);
                    var deserialized = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                    await Task.CompletedTask;

                    return new TestResult
                    {
                        Success = true,
                        Message = "JSON serialization successful",
                        MessageJa = "JSONシリアライズが成功しました"
                    };
                }
                catch (Exception ex)
                {
                    return new TestResult
                    {
                        Success = false,
                        Message = $"JSON serialization failed: {ex.Message}",
                        MessageJa = $"JSONシリアライズが失敗しました: {ex.Message}"
                    };
                }
            }
        );
    }

    /// <summary>
    /// テストスイート結果を表示
    /// </summary>
    public string FormatTestResults(TestSuite suite)
    {
        var sb = new StringBuilder();

        sb.AppendLine("╔══════════════════════════════════════════════════════════════╗");
        sb.AppendLine($"║  {suite.Name,-58}║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        sb.AppendLine($"Started:  {suite.StartTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Duration: {suite.TotalDuration.TotalSeconds:F2}s");
        sb.AppendLine();

        // サマリー
        var passIcon = suite.AllTestsPassed ? "✅" : "❌";
        sb.AppendLine("Summary / 概要:");
        sb.AppendLine($"  {passIcon} Total: {suite.TotalTests}");
        sb.AppendLine($"  ✓ Passed: {suite.PassedTests}");
        sb.AppendLine($"  ✗ Failed: {suite.FailedTests}");
        sb.AppendLine($"  Success Rate: {suite.SuccessRate:F1}%");
        sb.AppendLine($"  成功率: {suite.SuccessRate:F1}%");
        sb.AppendLine();

        // カテゴリ別
        var byCategory = suite.Results
            .GroupBy(r => suite.Tests.First(t => t.Id == r.TestId).Category)
            .OrderBy(g => g.Key);

        foreach (var category in byCategory)
        {
            sb.AppendLine($"[{category.Key}]");

            foreach (var result in category)
            {
                var icon = result.Success ? "✓" : "✗";
                var color = result.Success ? "green" : "red";

                sb.Append($"  {icon} {result.TestName}");
                sb.Append($" ({result.Duration.TotalMilliseconds:F0}ms)");

                if (!result.Success)
                {
                    sb.AppendLine();
                    sb.AppendLine($"     Error: {result.Message}");
                    sb.AppendLine($"     エラー: {result.MessageJa}");
                }
                else
                {
                    sb.AppendLine();
                }
            }

            sb.AppendLine();
        }

        sb.AppendLine("─────────────────────────────────────────────────────────────");

        if (suite.AllTestsPassed)
        {
            sb.AppendLine("✅ ALL TESTS PASSED - Ready for deployment");
            sb.AppendLine("✅ すべてのテストが合格 - デプロイ準備完了");
        }
        else
        {
            sb.AppendLine("❌ SOME TESTS FAILED - Fix issues before deployment");
            sb.AppendLine("❌ 一部のテストが失敗 - デプロイ前に問題を修正してください");
        }

        return sb.ToString();
    }
}
