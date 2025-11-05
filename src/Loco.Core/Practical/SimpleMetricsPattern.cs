// John Carmack: "Measure everything that matters"
// Uncle Bob: "The code should be self-documenting"

using System.Collections.Concurrent;

namespace Loco.Core.Practical;

/// <summary>
/// Lightweight metrics collection - no external dependencies
/// Just count, measure, and report what matters
/// </summary>
public class SimpleMetrics
{
    private readonly ConcurrentDictionary<string, long> _counters = new();
    private readonly ConcurrentDictionary<string, List<double>> _timings = new();
    private readonly ConcurrentDictionary<string, double> _gauges = new();

    // Increment counter
    public void IncrementCounter(string name, long value = 1)
    {
        _counters.AddOrUpdate(name, value, (_, old) => old + value);
    }

    // Record timing in milliseconds
    public void RecordTiming(string name, double milliseconds)
    {
        _timings.AddOrUpdate(name,
            new List<double> { milliseconds },
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(milliseconds);
                    // Keep only last 1000 samples to avoid memory growth
                    if (list.Count > 1000)
                        list.RemoveAt(0);
                }
                return list;
            });
    }

    // Set gauge value
    public void SetGauge(string name, double value)
    {
        _gauges[name] = value;
    }

    // Measure execution time
    public async Task<T> MeasureAsync<T>(string name, Func<Task<T>> operation)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            return await operation();
        }
        finally
        {
            RecordTiming(name, sw.Elapsed.TotalMilliseconds);
        }
    }

    // Get simple report
    public Dictionary<string, object> GetReport()
    {
        var report = new Dictionary<string, object>();

        // Counters
        foreach (var (name, value) in _counters)
        {
            report[$"counter.{name}"] = value;
        }

        // Gauges
        foreach (var (name, value) in _gauges)
        {
            report[$"gauge.{name}"] = value;
        }

        // Timings - calculate percentiles
        foreach (var (name, timings) in _timings)
        {
            if (timings.Count > 0)
            {
                List<double> sortedTimings;
                lock (timings)
                {
                    sortedTimings = timings.OrderBy(t => t).ToList();
                }

                report[$"timing.{name}.count"] = sortedTimings.Count;
                report[$"timing.{name}.min"] = sortedTimings.First();
                report[$"timing.{name}.max"] = sortedTimings.Last();
                report[$"timing.{name}.avg"] = Math.Round(sortedTimings.Average(), 2);

                // P50, P95, P99
                report[$"timing.{name}.p50"] = GetPercentile(sortedTimings, 50);
                report[$"timing.{name}.p95"] = GetPercentile(sortedTimings, 95);
                report[$"timing.{name}.p99"] = GetPercentile(sortedTimings, 99);
            }
        }

        return report;
    }

    private static double GetPercentile(List<double> sorted, int percentile)
    {
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }

    // Reset all metrics
    public void Reset()
    {
        _counters.Clear();
        _timings.Clear();
        _gauges.Clear();
    }
}