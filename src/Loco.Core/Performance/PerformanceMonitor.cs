using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Performance
{
    /// <summary>
    /// Enhanced performance monitoring utilities for enterprise applications.
    /// Provides structured logging, memory monitoring, and performance metrics collection.
    /// </summary>
    public static class PerformanceMonitor
    {
        private static readonly Dictionary<string, PerformanceMetrics> _metrics = new();
        private static readonly object _lock = new();

        /// <summary>
        /// Records a performance measurement with structured logging.
        /// </summary>
        public static void RecordMeasurement(string operationName, TimeSpan duration, long memoryUsage, ILogger? logger = null)
        {
            var metrics = GetOrCreateMetrics(operationName);

            lock (_lock)
            {
                metrics.ExecutionCount++;
                metrics.TotalDuration += duration;
                metrics.TotalMemoryUsage += memoryUsage;
                metrics.LastExecutionTime = DateTime.UtcNow;

                if (duration > metrics.MaxDuration)
                    metrics.MaxDuration = duration;

                if (duration < metrics.MinDuration || metrics.MinDuration == TimeSpan.Zero)
                    metrics.MinDuration = duration;

                metrics.AverageDuration = TimeSpan.FromMilliseconds(
                    metrics.TotalDuration.TotalMilliseconds / metrics.ExecutionCount);

                metrics.AverageMemoryUsage = metrics.TotalMemoryUsage / metrics.ExecutionCount;
            }

            // Structured logging with performance details
            logger?.LogInformation(
                "Performance recorded for {Operation}: Duration={Duration}ms, Memory={Memory}KB, Average={AverageDuration}ms",
                operationName,
                duration.TotalMilliseconds,
                memoryUsage / 1024,
                metrics.AverageDuration.TotalMilliseconds);

            // Log warnings for slow operations
            if (duration > TimeSpan.FromSeconds(5))
            {
                logger?.LogWarning(
                    "Slow operation detected: {Operation} took {Duration}ms (threshold: 5s)",
                    operationName,
                    duration.TotalMilliseconds);
            }

            // Log warnings for high memory usage
            if (memoryUsage > 100 * 1024 * 1024) // 100MB
            {
                logger?.LogWarning(
                    "High memory usage detected: {Operation} used {Memory}MB",
                    operationName,
                    memoryUsage / (1024 * 1024));
            }
        }

        /// <summary>
        /// Gets performance metrics for a specific operation.
        /// </summary>
        public static PerformanceMetrics? GetMetrics(string operationName)
        {
            lock (_lock)
            {
                return _metrics.TryGetValue(operationName, out var metrics) ? metrics : null;
            }
        }

        /// <summary>
        /// Gets all recorded performance metrics.
        /// </summary>
        public static IReadOnlyDictionary<string, PerformanceMetrics> GetAllMetrics()
        {
            lock (_lock)
            {
                return _metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
        }

        /// <summary>
        /// Resets all performance metrics.
        /// </summary>
        public static void ResetMetrics()
        {
            lock (_lock)
            {
                _metrics.Clear();
            }
        }

        /// <summary>
        /// Generates a performance report with structured output.
        /// </summary>
        public static string GenerateReport()
        {
            var report = new StringBuilder();
            report.AppendLine("=== Performance Report ===");
            report.AppendLine($"Generated at: {DateTime.UtcNow}");
            report.AppendLine();

            var metrics = GetAllMetrics();
            if (!metrics.Any())
            {
                report.AppendLine("No performance metrics recorded.");
                return report.ToString();
            }

            foreach (var (operationName, metric) in metrics.OrderByDescending(kvp => kvp.Value.TotalDuration))
            {
                report.AppendLine($"Operation: {operationName}");
                report.AppendLine($"  Executions: {metric.ExecutionCount}");
                report.AppendLine($"  Total Duration: {metric.TotalDuration.TotalMilliseconds}ms");
                report.AppendLine($"  Average Duration: {metric.AverageDuration.TotalMilliseconds}ms");
                report.AppendLine($"  Min Duration: {metric.MinDuration.TotalMilliseconds}ms");
                report.AppendLine($"  Max Duration: {metric.MaxDuration.TotalMilliseconds}ms");
                report.AppendLine($"  Total Memory: {metric.TotalMemoryUsage / (1024 * 1024)}MB");
                report.AppendLine($"  Average Memory: {metric.AverageMemoryUsage / 1024}KB");
                report.AppendLine($"  Last Execution: {metric.LastExecutionTime}");
                report.AppendLine();
            }

            return report.ToString();
        }

        private static PerformanceMetrics GetOrCreateMetrics(string operationName)
        {
            lock (_lock)
            {
                if (!_metrics.TryGetValue(operationName, out var metrics))
                {
                    metrics = new PerformanceMetrics { OperationName = operationName };
                    _metrics[operationName] = metrics;
                }
                return metrics;
            }
        }
    }

    /// <summary>
    /// Performance metrics for a specific operation.
    /// </summary>
    public class PerformanceMetrics
    {
        public string OperationName { get; set; } = string.Empty;
        public int ExecutionCount { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public TimeSpan MinDuration { get; set; }
        public TimeSpan MaxDuration { get; set; }
        public long TotalMemoryUsage { get; set; }
        public long AverageMemoryUsage { get; set; }
        public DateTime LastExecutionTime { get; set; }
    }

    /// <summary>
    /// Scoped performance measurement utility.
    /// </summary>
    public sealed class PerformanceScope : IDisposable
    {
        private readonly string _operationName;
        private readonly ILogger? _logger;
        private readonly Stopwatch _stopwatch;
        private readonly long _initialMemory;
        private bool _disposed;

        public PerformanceScope(string operationName, ILogger? logger = null)
        {
            _operationName = operationName;
            _logger = logger;
            _stopwatch = Stopwatch.StartNew();
            _initialMemory = GC.GetTotalMemory(false);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _stopwatch.Stop();
            var finalMemory = GC.GetTotalMemory(false);
            var memoryDelta = finalMemory - _initialMemory;

            PerformanceMonitor.RecordMeasurement(_operationName, _stopwatch.Elapsed, memoryDelta, _logger);
            _disposed = true;
        }
    }
}
