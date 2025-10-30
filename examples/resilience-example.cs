using System;
using System.Net.Http;
using System.Threading.Tasks;
using Loco.Core.Resilience;
using Loco.Cli.UI;
using Microsoft.Extensions.Logging;

namespace Loco.Examples;

/// <summary>
/// Demonstrates resilience patterns: Circuit Breaker, Retry, Fallback, and Pipeline
/// 回復力パターンのデモ: サーキットブレーカー、リトライ、フォールバック、パイプライン
/// </summary>
public class ResilienceExample
{
    /// <summary>
    /// Example 1: Simple Circuit Breaker
    /// 例1: シンプルなサーキットブレーカー
    /// </summary>
    public static async Task CircuitBreakerExample()
    {
        ColorConsole.Header("Circuit Breaker Example");

        var breaker = new CircuitBreaker(
            failureThreshold: 3,
            openDuration: TimeSpan.FromSeconds(5)
        );

        for (int i = 1; i <= 10; i++)
        {
            try
            {
                using var spinner = new Spinner($"Request #{i}");
                spinner.Start();

                var result = await breaker.ExecuteAsync(async () =>
                {
                    await Task.Delay(100);

                    // Simulate failures for first 5 requests
                    if (i <= 5)
                        throw new Exception("Service unavailable");

                    return $"Success: {i}";
                });

                spinner.Success(result);
            }
            catch (CircuitBreakerOpenException)
            {
                ColorConsole.Warning($"Request #{i}: Circuit is OPEN, failing fast");
            }
            catch (Exception ex)
            {
                ColorConsole.Error($"Request #{i}: {ex.Message}");
            }

            await Task.Delay(500);
        }

        var stats = breaker.GetStats();
        ColorConsole.Info($"Final state: {stats.State}, Failures: {stats.FailureCount}");
    }

    /// <summary>
    /// Example 2: Advanced Retry Policy with Multiple Strategies
    /// 例2: 複数の戦略を持つ高度なリトライポリシー
    /// </summary>
    public static async Task RetryPolicyExample()
    {
        ColorConsole.Header("Retry Policy Example");

        // Strategy 1: Exponential Backoff with Jitter (Recommended)
        ColorConsole.Write("Strategy: Exponential Backoff with Jitter", ConsoleColor.Cyan, true);
        var policy = AdvancedRetryPolicy.Builder()
            .WithMaxRetries(3)
            .WithStrategy(AdvancedRetryPolicy.BackoffStrategy.ExponentialWithJitter)
            .WithInitialDelay(TimeSpan.FromMilliseconds(100))
            .WithShouldRetry(_ => true)
            .Build();

        var attempt = 0;
        try
        {
            using var spinner = new Spinner("Attempting operation with retry...");
            spinner.Start();

            var result = await policy.ExecuteAsync(async () =>
            {
                attempt++;
                await Task.Delay(50);

                if (attempt < 3)
                {
                    ColorConsole.Warning($"  Attempt {attempt} failed, will retry...");
                    throw new Exception($"Temporary failure #{attempt}");
                }

                return "Operation succeeded!";
            });

            spinner.Success(result);
        }
        catch (Exception ex)
        {
            ColorConsole.Error($"All retries exhausted: {ex.Message}");
        }

        Console.WriteLine();

        // Strategy 2: Decorrelated Jitter (AWS Recommended)
        ColorConsole.Write("Strategy: Decorrelated Jitter (AWS Style)", ConsoleColor.Cyan, true);
        var awsPolicy = AdvancedRetryPolicy.Builder()
            .WithMaxRetries(3)
            .WithStrategy(AdvancedRetryPolicy.BackoffStrategy.DecorrelatedJitter)
            .WithInitialDelay(TimeSpan.FromMilliseconds(100))
            .WithShouldRetry(_ => true)
            .Build();

        attempt = 0;
        try
        {
            var result = await awsPolicy.ExecuteAsync(async () =>
            {
                attempt++;
                ColorConsole.Info($"  AWS-style attempt {attempt}");
                await Task.Delay(50);

                if (attempt < 2)
                    throw new Exception("Temporary error");

                return "Success with AWS retry!";
            });

            ColorConsole.Success(result);
        }
        catch (Exception ex)
        {
            ColorConsole.Error(ex.Message);
        }
    }

    /// <summary>
    /// Example 3: Fallback Policy
    /// 例3: フォールバックポリシー
    /// </summary>
    public static async Task FallbackPolicyExample()
    {
        ColorConsole.Header("Fallback Policy Example");

        // Fallback with alternative value
        var fallback = FallbackPolicy<string>.Builder()
            .WithFallbackValue("Cached data from 2025-10-19")
            .Build();

        using var spinner = new Spinner("Fetching data from API...");
        spinner.Start();

        var result = await fallback.ExecuteAsync(async () =>
        {
            await Task.Delay(100);
            throw new HttpRequestException("API server is down");
        });

        spinner.Warning("API failed, using fallback");
        ColorConsole.Info($"Result: {result}");

        Console.WriteLine();

        // Fallback with alternative function
        var smartFallback = FallbackPolicy<string>.Builder()
            .WithFallback(async (exception) =>
            {
                ColorConsole.Warning($"Primary failed: {exception.Message}");
                ColorConsole.Info("Trying alternative source...");
                await Task.Delay(100);
                return "Data from backup server";
            })
            .Build();

        using var spinner2 = new Spinner("Fetching from primary source...");
        spinner2.Start();

        var result2 = await smartFallback.ExecuteAsync(async () =>
        {
            await Task.Delay(50);
            throw new Exception("Primary source failed");
        });

        spinner2.Info(result2);
    }

    /// <summary>
    /// Example 4: Complete Resilience Pipeline
    /// 例4: 完全な回復力パイプライン
    /// </summary>
    public static async Task ResiliencePipelineExample()
    {
        ColorConsole.Header("Complete Resilience Pipeline Example");

        // Build a comprehensive pipeline
        var pipeline = ResiliencePipeline<string>.Builder()
            .WithCircuitBreaker(
                failureThreshold: 3,
                openDuration: TimeSpan.FromSeconds(10)
            )
            .WithRetry(
                maxRetries: 3,
                strategy: AdvancedRetryPolicy.BackoffStrategy.ExponentialWithJitter
            )
            .WithFallbackValue("Fallback: Service temporarily unavailable")
            .Build();

        ColorConsole.Info("Pipeline: Circuit Breaker → Retry → Fallback → Operation");
        Console.WriteLine();

        // Simulate various scenarios
        var scenarios = new[]
        {
            ("Success immediately", 0),
            ("Success after 2 retries", 2),
            ("Failure, use fallback", 5)
        };

        foreach (var (description, failCount) in scenarios)
        {
            ColorConsole.Write($"\nScenario: {description}", ConsoleColor.Yellow, true);

            using var spinner = new Spinner("Executing through pipeline...");
            spinner.Start();

            var attempt = 0;
            var result = await pipeline.ExecuteAsync(async () =>
            {
                attempt++;
                await Task.Delay(50);

                if (attempt <= failCount)
                {
                    throw new Exception($"Simulated failure (attempt {attempt})");
                }

                return $"Success after {attempt} attempt(s)";
            });

            spinner.Success(result);
        }
    }

    /// <summary>
    /// Example 5: Real-world HTTP API Call with Resilience
    /// 例5: 実際のHTTP API呼び出しと回復力
    /// </summary>
    public static async Task RealWorldApiExample()
    {
        ColorConsole.Header("Real-world API Call with Resilience");

        var pipeline = ResiliencePipelinePresets.Standard<string>();

        var apiUrls = new[]
        {
            "https://api.github.com/zen",
            "https://httpstat.us/500", // Will fail
            "https://api.github.com/zen"
        };

        foreach (var url in apiUrls)
        {
            ColorConsole.Write($"\nCalling: {url}", ConsoleColor.Cyan, true);

            using var spinner = new Spinner("Making API request...");
            spinner.Start();

            try
            {
                var result = await pipeline.ExecuteAsync(async () =>
                {
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.Add("User-Agent", "Loco-Example");
                    client.Timeout = TimeSpan.FromSeconds(5);

                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                    return content.Length > 100 ? content[..100] + "..." : content;
                });

                spinner.Success("API call succeeded");
                Console.WriteLine($"Response: {result}");
            }
            catch (Exception ex)
            {
                spinner.Error("API call failed");
                ColorConsole.Error($"Error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Run all examples
    /// 全ての例を実行
    /// </summary>
    public static async Task Main(string[] args)
    {
        Console.WriteLine();
        ColorConsole.Highlight("Loco Resilience Patterns Examples");
        ColorConsole.Separator();
        Console.WriteLine();

        try
        {
            await CircuitBreakerExample();
            await Task.Delay(1000);
            Console.WriteLine("\n");

            await RetryPolicyExample();
            await Task.Delay(1000);
            Console.WriteLine("\n");

            await FallbackPolicyExample();
            await Task.Delay(1000);
            Console.WriteLine("\n");

            await ResiliencePipelineExample();
            await Task.Delay(1000);
            Console.WriteLine("\n");

            // Uncomment to test real API calls
            // await RealWorldApiExample();

            Console.WriteLine();
            ColorConsole.Success("All examples completed!");
        }
        catch (Exception ex)
        {
            ColorConsole.Error($"Unexpected error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        Console.WriteLine();
        ColorConsole.Write("Press any key to exit...", ConsoleColor.Gray);
        Console.ReadKey();
    }
}
