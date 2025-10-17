using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Performance
{
    /// <summary>
    /// Production-grade performance profiler for monitoring and optimization.
    /// Tracks execution times, memory usage, and provides performance insights.
    /// </summary>
    public class PerformanceProfiler : IDisposable
    {
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, OperationMetrics> _metrics;
        private readonly Timer _reportTimer;
        private bool _disposed;

        public PerformanceProfiler(ILogger? logger = null, TimeSpan? reportInterval = null)
        {
            _logger = logger;
            _metrics = new ConcurrentDictionary<string, OperationMetrics>();

            // Auto-report every 5 minutes by default
            var interval = reportInterval ?? TimeSpan.FromMinutes(5);
            _reportTimer = new Timer(_ => GenerateReport(), null, interval, interval);
        }

        /// <summary>
        /// Creates a timed operation for profiling.
        /// </summary>
        public IDisposable Profile(string operationName)
        {
            return new ProfiledOperation(this, operationName);
        }

        /// <summary>
        /// Records an operation execution.
        /// </summary>
        internal void RecordOperation(string operationName, TimeSpan duration, long memoryDelta)
        {
            var metrics = _metrics.GetOrAdd(operationName, _ => new OperationMetrics { OperationName = operationName });

            lock (metrics)
            {
                metrics.ExecutionCount++;
                metrics.TotalDuration += duration;
                metrics.MinDuration = metrics.MinDuration == TimeSpan.Zero
                    ? duration
                    : TimeSpan.FromMilliseconds(Math.Min(metrics.MinDuration.TotalMilliseconds, duration.TotalMilliseconds));
                metrics.MaxDuration = TimeSpan.FromMilliseconds(Math.Max(metrics.MaxDuration.TotalMilliseconds, duration.TotalMilliseconds));
                metrics.AverageDuration = TimeSpan.FromMilliseconds(metrics.TotalDuration.TotalMilliseconds / metrics.ExecutionCount);
                metrics.TotalMemoryDelta += memoryDelta;
                metrics.LastExecutionTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Gets metrics for a specific operation.
        /// </summary>
        public OperationMetrics? GetMetrics(string operationName)
        {
            return _metrics.TryGetValue(operationName, out var metrics) ? metrics : null;
        }

        /// <summary>
        /// Gets all operation metrics.
        /// </summary>
        public List<OperationMetrics> GetAllMetrics()
        {
            return _metrics.Values.ToList();
        }

        /// <summary>
        /// Gets performance summary.
        /// </summary>
        public PerformanceSummary GetSummary()
        {
            var allMetrics = GetAllMetrics();

            if (allMetrics.Count == 0)
            {
                return new PerformanceSummary
                {
                    TotalOperations = 0,
                    TotalExecutions = 0,
                    TotalDuration = TimeSpan.Zero
                };
            }

            return new PerformanceSummary
            {
                TotalOperations = allMetrics.Count,
                TotalExecutions = allMetrics.Sum(m => m.ExecutionCount),
                TotalDuration = TimeSpan.FromMilliseconds(allMetrics.Sum(m => m.TotalDuration.TotalMilliseconds)),
                SlowestOperation = allMetrics.OrderByDescending(m => m.MaxDuration).FirstOrDefault(),
                FastestOperation = allMetrics.OrderBy(m => m.MinDuration).FirstOrDefault(),
                MostFrequentOperation = allMetrics.OrderByDescending(m => m.ExecutionCount).FirstOrDefault()
            };
        }

        /// <summary>
        /// Generates and logs performance report.
        /// </summary>
        public void GenerateReport()
        {
            var summary = GetSummary();

            if (summary.TotalOperations == 0)
            {
                _logger?.LogInformation("Performance Report: No operations profiled");
                return;
            }

            _logger?.LogInformation(
                "Performance Report: {Operations} operations, {Executions} total executions, {Duration:F2}s total time",
                summary.TotalOperations,
                summary.TotalExecutions,
                summary.TotalDuration.TotalSeconds);

            if (summary.SlowestOperation != null)
            {
                _logger?.LogInformation(
                    "  Slowest: {Name} (max: {Max:F2}ms, avg: {Avg:F2}ms)",
                    summary.SlowestOperation.OperationName,
                    summary.SlowestOperation.MaxDuration.TotalMilliseconds,
                    summary.SlowestOperation.AverageDuration.TotalMilliseconds);
            }

            if (summary.MostFrequentOperation != null)
            {
                _logger?.LogInformation(
                    "  Most Frequent: {Name} ({Count} executions)",
                    summary.MostFrequentOperation.OperationName,
                    summary.MostFrequentOperation.ExecutionCount);
            }

            // Identify performance issues
            var slowOps = GetAllMetrics()
                .Where(m => m.AverageDuration.TotalMilliseconds > 1000)
                .OrderByDescending(m => m.AverageDuration)
                .Take(5)
                .ToList();

            if (slowOps.Any())
            {
                _logger?.LogWarning("Performance Issues - Slow Operations:");
                foreach (var op in slowOps)
                {
                    _logger?.LogWarning(
                        "  {Name}: avg {Avg:F2}ms, max {Max:F2}ms ({Count} executions)",
                        op.OperationName,
                        op.AverageDuration.TotalMilliseconds,
                        op.MaxDuration.TotalMilliseconds,
                        op.ExecutionCount);
                }
            }
        }

        /// <summary>
        /// Resets all metrics.
        /// </summary>
        public void Reset()
        {
            _metrics.Clear();
            _logger?.LogInformation("Performance metrics reset");
        }

        /// <summary>
        /// Gets top N slowest operations.
        /// </summary>
        public List<OperationMetrics> GetSlowestOperations(int count = 10)
        {
            return GetAllMetrics()
                .OrderByDescending(m => m.AverageDuration)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Gets operations exceeding threshold.
        /// </summary>
        public List<OperationMetrics> GetOperationsExceedingThreshold(TimeSpan threshold)
        {
            return GetAllMetrics()
                .Where(m => m.AverageDuration > threshold)
                .OrderByDescending(m => m.AverageDuration)
                .ToList();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _reportTimer?.Dispose();
            GenerateReport(); // Final report
            _disposed = true;
        }

        /// <summary>
        /// Profiled operation scope.
        /// </summary>
        private class ProfiledOperation : IDisposable
        {
            private readonly PerformanceProfiler _profiler;
            private readonly string _operationName;
            private readonly Stopwatch _stopwatch;
            private readonly long _startMemory;

            public ProfiledOperation(PerformanceProfiler profiler, string operationName)
            {
                _profiler = profiler;
                _operationName = operationName;
                _startMemory = GC.GetTotalMemory(false);
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                var endMemory = GC.GetTotalMemory(false);
                var memoryDelta = endMemory - _startMemory;

                _profiler.RecordOperation(_operationName, _stopwatch.Elapsed, memoryDelta);
            }
        }
    }

    /// <summary>
    /// Metrics for a single operation.
    /// </summary>
    public class OperationMetrics
    {
        public string OperationName { get; set; } = string.Empty;
        public long ExecutionCount { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public TimeSpan MinDuration { get; set; }
        public TimeSpan MaxDuration { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public long TotalMemoryDelta { get; set; }
        public DateTime LastExecutionTime { get; set; }

        public double AverageMemoryDelta => ExecutionCount > 0 ? (double)TotalMemoryDelta / ExecutionCount : 0;

        public string GetSummary()
        {
            return $"{OperationName}: {ExecutionCount} executions, " +
                   $"avg {AverageDuration.TotalMilliseconds:F2}ms, " +
                   $"min {MinDuration.TotalMilliseconds:F2}ms, " +
                   $"max {MaxDuration.TotalMilliseconds:F2}ms";
        }
    }

    /// <summary>
    /// Overall performance summary.
    /// </summary>
    public class PerformanceSummary
    {
        public int TotalOperations { get; set; }
        public long TotalExecutions { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public OperationMetrics? SlowestOperation { get; set; }
        public OperationMetrics? FastestOperation { get; set; }
        public OperationMetrics? MostFrequentOperation { get; set; }

        public double AverageExecutionTime =>
            TotalExecutions > 0 ? TotalDuration.TotalMilliseconds / TotalExecutions : 0;
    }
}