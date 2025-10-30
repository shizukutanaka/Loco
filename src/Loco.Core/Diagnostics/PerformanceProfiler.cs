using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Diagnostics
{
    /// <summary>
    /// Lightweight performance profiler for tracking operation timings and resource usage.
    /// Provides insights into bottlenecks and performance characteristics.
    /// </summary>
    public class PerformanceProfiler
    {
        private readonly ConcurrentDictionary<string, ProfileMetrics> _profiles = new();
        private readonly ILogger? _logger;
        private readonly Timer? _reportTimer;
        private readonly TimeSpan _reportInterval;
        private bool _disposed;

        public PerformanceProfiler(ILogger? logger = null, TimeSpan? reportInterval = null)
        {
            _logger = logger;
            _reportInterval = reportInterval ?? TimeSpan.FromMinutes(5);

            if (_reportInterval > TimeSpan.Zero)
            {
                _reportTimer = new Timer(
                    GenerateReportCallback,
                    null,
                    (int)_reportInterval.TotalMilliseconds,
                    (int)_reportInterval.TotalMilliseconds);
            }
        }

        /// <summary>
        /// Starts profiling an operation.
        /// </summary>
        /// <param name="operationName">Name of the operation to profile</param>
        /// <returns>A disposable scope that tracks the operation</returns>
        public IDisposable Profile(string operationName)
        {
            return new ProfileScope(this, operationName);
        }

        /// <summary>
        /// Records a profiled operation.
        /// </summary>
        public void RecordOperation(string operationName, TimeSpan duration, long memoryAllocated = 0)
        {
            var metrics = _profiles.GetOrAdd(operationName, _ => new ProfileMetrics(operationName));
            metrics.Record(duration, memoryAllocated);
        }

        /// <summary>
        /// Gets metrics for a specific operation.
        /// </summary>
        public ProfileMetrics? GetMetrics(string operationName)
        {
            return _profiles.TryGetValue(operationName, out var metrics) ? metrics : null;
        }

        /// <summary>
        /// Gets all profiled operations sorted by total time.
        /// </summary>
        public IEnumerable<ProfileMetrics> GetTopOperations(int count = 10)
        {
            return _profiles.Values
                .OrderByDescending(m => m.TotalTime)
                .Take(count);
        }

        /// <summary>
        /// Generates a performance report.
        /// </summary>
        public PerformanceReport GenerateReport()
        {
            return GenerateReportInternal();
        }

        private void GenerateReportCallback(object? state)
        {
            GenerateReportInternal();
        }

        private PerformanceReport GenerateReportInternal()
        {
            var report = new PerformanceReport
            {
                Timestamp = DateTime.UtcNow,
                TotalOperations = _profiles.Values.Sum(m => m.CallCount),
                Profiles = _profiles.Values.OrderByDescending(m => m.TotalTime).ToList()
            };

            if (_logger != null && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Performance Report: {TotalOps} operations, Top: {TopOp} ({TotalTime:F2}ms avg, {Calls} calls)",
                    report.TotalOperations,
                    report.Profiles.FirstOrDefault()?.OperationName ?? "N/A",
                    report.Profiles.FirstOrDefault()?.AverageTime.TotalMilliseconds ?? 0,
                    report.Profiles.FirstOrDefault()?.CallCount ?? 0);
            }

            return report;
        }

        /// <summary>
        /// Resets all profiling data.
        /// </summary>
        public void Reset()
        {
            _profiles.Clear();
            _logger?.LogInformation("Performance profiler reset");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _reportTimer?.Dispose();
            _logger?.LogInformation("Performance profiler disposed");
        }

        private class ProfileScope : IDisposable
        {
            private readonly PerformanceProfiler _profiler;
            private readonly string _operationName;
            private readonly Stopwatch _stopwatch;
            private readonly long _initialMemory;
            private bool _disposed;

            public ProfileScope(PerformanceProfiler profiler, string operationName)
            {
                _profiler = profiler;
                _operationName = operationName;
                _initialMemory = GC.GetTotalMemory(false);
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                _stopwatch.Stop();
                var finalMemory = GC.GetTotalMemory(false);
                var memoryDelta = Math.Max(0, finalMemory - _initialMemory);

                _profiler.RecordOperation(_operationName, _stopwatch.Elapsed, memoryDelta);
            }
        }
    }

    /// <summary>
    /// Metrics for a profiled operation.
    /// </summary>
    public class ProfileMetrics
    {
        private long _callCount;
        private long _totalTicks;
        private long _totalMemory;
        private long _minTicks = long.MaxValue;
        private long _maxTicks;

        public ProfileMetrics(string operationName)
        {
            OperationName = operationName;
        }

        public string OperationName { get; }
        public long CallCount => Interlocked.Read(ref _callCount);
        public TimeSpan TotalTime => TimeSpan.FromTicks(Interlocked.Read(ref _totalTicks));
        public TimeSpan AverageTime
        {
            get
            {
                var count = CallCount;
                return count > 0
                    ? TimeSpan.FromTicks(Interlocked.Read(ref _totalTicks) / count)
                    : TimeSpan.Zero;
            }
        }
        public TimeSpan MinTime => TimeSpan.FromTicks(Interlocked.Read(ref _minTicks));
        public TimeSpan MaxTime => TimeSpan.FromTicks(Interlocked.Read(ref _maxTicks));
        public long TotalMemoryBytes => Interlocked.Read(ref _totalMemory);
        public long AverageMemoryBytes
        {
            get
            {
                var count = CallCount;
                return count > 0 ? TotalMemoryBytes / count : 0;
            }
        }

        internal void Record(TimeSpan duration, long memoryAllocated)
        {
            var ticks = duration.Ticks;

            Interlocked.Increment(ref _callCount);
            Interlocked.Add(ref _totalTicks, ticks);
            Interlocked.Add(ref _totalMemory, memoryAllocated);

            // Update min
            long currentMin;
            do
            {
                currentMin = Interlocked.Read(ref _minTicks);
                if (ticks >= currentMin) break;
            } while (Interlocked.CompareExchange(ref _minTicks, ticks, currentMin) != currentMin);

            // Update max
            long currentMax;
            do
            {
                currentMax = Interlocked.Read(ref _maxTicks);
                if (ticks <= currentMax) break;
            } while (Interlocked.CompareExchange(ref _maxTicks, ticks, currentMax) != currentMax);
        }
    }

    /// <summary>
    /// Performance profiling report.
    /// </summary>
    public class PerformanceReport
    {
        public DateTime Timestamp { get; init; }
        public long TotalOperations { get; init; }
        public List<ProfileMetrics> Profiles { get; init; } = new();

        public string FormatReport()
        {
            var lines = new List<string>
            {
                $"Performance Report - {Timestamp:yyyy-MM-dd HH:mm:ss}",
                $"Total Operations: {TotalOperations:N0}",
                "",
                "Top Operations by Total Time:",
                "-------------------------------------------------------------"
            };

            foreach (var profile in Profiles.Take(10))
            {
                lines.Add($"  {profile.OperationName}:");
                lines.Add($"    Calls: {profile.CallCount:N0}");
                lines.Add($"    Total: {profile.TotalTime.TotalMilliseconds:F2}ms");
                lines.Add($"    Avg: {profile.AverageTime.TotalMilliseconds:F2}ms");
                lines.Add($"    Min: {profile.MinTime.TotalMilliseconds:F2}ms");
                lines.Add($"    Max: {profile.MaxTime.TotalMilliseconds:F2}ms");
                lines.Add($"    Memory: {profile.AverageMemoryBytes:N0} bytes avg");
                lines.Add("");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
