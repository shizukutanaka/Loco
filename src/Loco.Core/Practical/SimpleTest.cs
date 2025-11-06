// John Carmack: "Testing is about confidence, not coverage"
// Rob Pike: "Make tests simple, obvious, and fast"

using System.Diagnostics;

namespace Loco.Core.Practical;

/// <summary>
/// Simple test framework - Lightweight testing without heavy frameworks
/// Fast, clear output, easy assertions
/// </summary>
public class SimpleTest
{
    private readonly List<TestResult> _results = new();
    private readonly SimpleLogger _logger;
    private readonly Stopwatch _sw = new();

    public SimpleTest(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleTest));
    }

    // Run test
    public SimpleTest Test(string name, Action test)
    {
        _sw.Restart();
        try
        {
            test();
            _sw.Stop();
            _results.Add(new TestResult
            {
                Name = name,
                Passed = true,
                Duration = _sw.Elapsed
            });
            _logger.Info($"✓ {name} ({_sw.ElapsedMilliseconds}ms)");
        }
        catch (Exception ex)
        {
            _sw.Stop();
            _results.Add(new TestResult
            {
                Name = name,
                Passed = false,
                Error = ex.Message,
                Duration = _sw.Elapsed
            });
            _logger.Error($"✗ {name} ({_sw.ElapsedMilliseconds}ms): {ex.Message}", ex);
        }
        return this;
    }

    // Run async test
    public SimpleTest TestAsync(string name, Func<Task> test)
    {
        _sw.Restart();
        try
        {
            test().Wait();
            _sw.Stop();
            _results.Add(new TestResult
            {
                Name = name,
                Passed = true,
                Duration = _sw.Elapsed
            });
            _logger.Info($"✓ {name} ({_sw.ElapsedMilliseconds}ms)");
        }
        catch (Exception ex)
        {
            _sw.Stop();
            _results.Add(new TestResult
            {
                Name = name,
                Passed = false,
                Error = ex.Message,
                Duration = _sw.Elapsed
            });
            _logger.Error($"✗ {name} ({_sw.ElapsedMilliseconds}ms): {ex.Message}", ex);
        }
        return this;
    }

    // Get summary
    public TestSummary GetSummary()
    {
        return new TestSummary
        {
            Total = _results.Count,
            Passed = _results.Count(r => r.Passed),
            Failed = _results.Count(r => !r.Passed),
            TotalDuration = TimeSpan.FromMilliseconds(_results.Sum(r => r.Duration.TotalMilliseconds)),
            Results = _results
        };
    }

    // Print summary
    public void PrintSummary()
    {
        var summary = GetSummary();
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine($"Test Results: {summary.Passed}/{summary.Total} passed");
        Console.WriteLine($"Duration: {summary.TotalDuration.TotalMilliseconds:F2}ms");
        Console.WriteLine(new string('=', 60));

        if (summary.Failed > 0)
        {
            Console.WriteLine("\nFailed Tests:");
            foreach (var result in summary.Results.Where(r => !r.Passed))
            {
                Console.WriteLine($"  ✗ {result.Name}: {result.Error}");
            }
        }
    }

    private class TestResult
    {
        public string Name { get; set; } = "";
        public bool Passed { get; set; }
        public string? Error { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class TestSummary
    {
        public int Total { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public List<TestResult> Results { get; set; } = new();
    }
}

/// <summary>
/// Simple assertions
/// </summary>
public static class Assert
{
    public static void IsTrue(bool condition, string? message = null)
    {
        if (!condition)
            throw new AssertionException(message ?? "Expected true but was false");
    }

    public static void IsFalse(bool condition, string? message = null)
    {
        if (condition)
            throw new AssertionException(message ?? "Expected false but was true");
    }

    public static void AreEqual<T>(T expected, T actual, string? message = null)
    {
        if (!Equals(expected, actual))
            throw new AssertionException(message ?? $"Expected {expected} but was {actual}");
    }

    public static void AreNotEqual<T>(T notExpected, T actual, string? message = null)
    {
        if (Equals(notExpected, actual))
            throw new AssertionException(message ?? $"Expected not {notExpected}");
    }

    public static void IsNull(object? obj, string? message = null)
    {
        if (obj != null)
            throw new AssertionException(message ?? "Expected null");
    }

    public static void IsNotNull(object? obj, string? message = null)
    {
        if (obj == null)
            throw new AssertionException(message ?? "Expected not null");
    }

    public static void Throws<TException>(Action action, string? message = null) where TException : Exception
    {
        try
        {
            action();
            throw new AssertionException(message ?? $"Expected {typeof(TException).Name} to be thrown");
        }
        catch (TException)
        {
            // Expected
        }
    }

    public static async Task ThrowsAsync<TException>(Func<Task> action, string? message = null) where TException : Exception
    {
        try
        {
            await action();
            throw new AssertionException(message ?? $"Expected {typeof(TException).Name} to be thrown");
        }
        catch (TException)
        {
            // Expected
        }
    }

    public static void Contains<T>(IEnumerable<T> collection, T item, string? message = null)
    {
        if (!collection.Contains(item))
            throw new AssertionException(message ?? $"Collection does not contain {item}");
    }

    public static void DoesNotContain<T>(IEnumerable<T> collection, T item, string? message = null)
    {
        if (collection.Contains(item))
            throw new AssertionException(message ?? $"Collection contains {item}");
    }
}

public class AssertionException : Exception
{
    public AssertionException(string message) : base(message) { }
}

/// <summary>
/// Mock object helper
/// </summary>
public class SimpleMock<T> where T : class
{
    private readonly Dictionary<string, Delegate> _implementations = new();
    private readonly List<(string method, object?[] args)> _calls = new();

    public SimpleMock<T> Setup(string methodName, Delegate implementation)
    {
        _implementations[methodName] = implementation;
        return this;
    }

    public TResult? Call<TResult>(string methodName, params object?[] args)
    {
        _calls.Add((methodName, args));

        if (_implementations.TryGetValue(methodName, out var impl))
        {
            return (TResult?)impl.DynamicInvoke(args);
        }

        return default;
    }

    public void Verify(string methodName, int expectedCalls)
    {
        var actualCalls = _calls.Count(c => c.method == methodName);
        if (actualCalls != expectedCalls)
        {
            throw new AssertionException($"Expected {expectedCalls} calls to {methodName} but was {actualCalls}");
        }
    }

    public bool WasCalled(string methodName) => _calls.Any(c => c.method == methodName);
}

/// <summary>
/// Benchmark helper
/// </summary>
public class SimpleBenchmark
{
    private readonly string _name;
    private readonly Stopwatch _sw = new();
    private readonly List<long> _durations = new();

    public SimpleBenchmark(string name)
    {
        _name = name;
    }

    public void Run(Action action, int iterations = 1000)
    {
        // Warmup
        for (int i = 0; i < 10; i++)
        {
            action();
        }

        // Measure
        for (int i = 0; i < iterations; i++)
        {
            _sw.Restart();
            action();
            _sw.Stop();
            _durations.Add(_sw.ElapsedTicks);
        }

        PrintResults(iterations);
    }

    public async Task RunAsync(Func<Task> action, int iterations = 1000)
    {
        // Warmup
        for (int i = 0; i < 10; i++)
        {
            await action();
        }

        // Measure
        for (int i = 0; i < iterations; i++)
        {
            _sw.Restart();
            await action();
            _sw.Stop();
            _durations.Add(_sw.ElapsedTicks);
        }

        PrintResults(iterations);
    }

    private void PrintResults(int iterations)
    {
        var avgTicks = _durations.Average();
        var avgMs = avgTicks * 1000.0 / Stopwatch.Frequency;
        var minMs = _durations.Min() * 1000.0 / Stopwatch.Frequency;
        var maxMs = _durations.Max() * 1000.0 / Stopwatch.Frequency;

        Console.WriteLine($"\nBenchmark: {_name}");
        Console.WriteLine($"Iterations: {iterations}");
        Console.WriteLine($"Average: {avgMs:F4}ms");
        Console.WriteLine($"Min: {minMs:F4}ms");
        Console.WriteLine($"Max: {maxMs:F4}ms");
        Console.WriteLine($"Ops/sec: {1000.0 / avgMs:F0}");
    }
}

/// <summary>
/// Example tests
/// </summary>
public class SimpleTestExamples
{
    public static void RunTests()
    {
        var tests = new SimpleTest();

        tests
            .Test("Math addition", () =>
            {
                var result = 2 + 2;
                Assert.AreEqual(4, result);
            })
            .Test("String operations", () =>
            {
                var str = "hello";
                Assert.IsTrue(str.Contains("ell"));
                Assert.AreEqual(5, str.Length);
            })
            .Test("Collections", () =>
            {
                var list = new List<int> { 1, 2, 3 };
                Assert.Contains(list, 2);
                Assert.DoesNotContain(list, 5);
            })
            .Test("Exceptions", () =>
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    throw new ArgumentNullException("test");
                });
            })
            .TestAsync("Async operations", async () =>
            {
                await Task.Delay(10);
                Assert.IsTrue(true);
            });

        tests.PrintSummary();
    }

    public static void RunBenchmarks()
    {
        // Benchmark string concatenation
        var bench1 = new SimpleBenchmark("String concat");
        bench1.Run(() =>
        {
            var result = "Hello" + " " + "World";
        }, iterations: 10000);

        // Benchmark StringBuilder
        var bench2 = new SimpleBenchmark("StringBuilder");
        bench2.Run(() =>
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("Hello");
            sb.Append(" ");
            sb.Append("World");
            var result = sb.ToString();
        }, iterations: 10000);
    }
}

/// <summary>
/// Test data builder
/// </summary>
public class TestDataBuilder<T> where T : new()
{
    private readonly T _instance = new();
    private readonly Dictionary<string, object?> _values = new();

    public TestDataBuilder<T> With(string propertyName, object? value)
    {
        _values[propertyName] = value;
        return this;
    }

    public T Build()
    {
        var type = typeof(T);
        foreach (var kvp in _values)
        {
            var prop = type.GetProperty(kvp.Key);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(_instance, kvp.Value);
            }
        }
        return _instance;
    }
}

/// <summary>
/// Fake clock for testing time-dependent code
/// </summary>
public class FakeClock
{
    private DateTime _currentTime = DateTime.UtcNow;

    public DateTime UtcNow => _currentTime;
    public DateTime Now => _currentTime.ToLocalTime();

    public void SetTime(DateTime time)
    {
        _currentTime = time;
    }

    public void Advance(TimeSpan duration)
    {
        _currentTime = _currentTime.Add(duration);
    }
}