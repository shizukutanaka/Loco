using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Loco.Core.Telemetry
{
    /// <summary>
    /// Provides telemetry tracking for operations to support observability and monitoring.
    /// Essential for production deployments requiring metrics and diagnostics.
    /// </summary>
    public class OperationTelemetry
    {
        private readonly ConcurrentDictionary<string, OperationMetrics> _metrics = new();
        private readonly ConcurrentQueue<OperationEvent> _recentEvents = new();
        private const int MaxRecentEvents = 1000;

        /// <summary>
        /// Starts tracking an operation and returns a disposable scope.
        /// </summary>
        /// <param name="operationName">Name of the operation to track</param>
        /// <param name="properties">Optional properties to attach to the operation</param>
        /// <returns>A disposable operation scope that tracks duration</returns>
        public IDisposable TrackOperation(string operationName, Dictionary<string, string>? properties = null)
        {
            return new OperationScope(this, operationName, properties);
        }

        /// <summary>
        /// Records a successful operation.
        /// </summary>
        public void RecordSuccess(string operationName, TimeSpan duration, Dictionary<string, string>? properties = null)
        {
            var metrics = _metrics.GetOrAdd(operationName, _ => new OperationMetrics(operationName));
            metrics.RecordSuccess(duration);

            RecordEvent(new OperationEvent
            {
                Timestamp = DateTime.UtcNow,
                OperationName = operationName,
                Success = true,
                Duration = duration,
                Properties = properties
            });
        }

        /// <summary>
        /// Records a failed operation.
        /// </summary>
        public void RecordFailure(string operationName, TimeSpan duration, Exception? exception = null, Dictionary<string, string>? properties = null)
        {
            var metrics = _metrics.GetOrAdd(operationName, _ => new OperationMetrics(operationName));
            metrics.RecordFailure(duration);

            RecordEvent(new OperationEvent
            {
                Timestamp = DateTime.UtcNow,
                OperationName = operationName,
                Success = false,
                Duration = duration,
                Exception = exception,
                Properties = properties
            });
        }

        /// <summary>
        /// Gets metrics for a specific operation.
        /// </summary>
        public OperationMetrics? GetMetrics(string operationName)
        {
            return _metrics.TryGetValue(operationName, out var metrics) ? metrics : null;
        }

        /// <summary>
        /// Gets all tracked operation metrics.
        /// </summary>
        public IReadOnlyDictionary<string, OperationMetrics> GetAllMetrics()
        {
            return _metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Gets recent operation events.
        /// </summary>
        public IEnumerable<OperationEvent> GetRecentEvents(int count = 100)
        {
            return _recentEvents.Take(count);
        }

        private void RecordEvent(OperationEvent evt)
        {
            _recentEvents.Enqueue(evt);

            // Keep only recent events to prevent memory growth
            while (_recentEvents.Count > MaxRecentEvents)
            {
                _recentEvents.TryDequeue(out _);
            }
        }

        private class OperationScope : IDisposable
        {
            private readonly OperationTelemetry _telemetry;
            private readonly string _operationName;
            private readonly Dictionary<string, string>? _properties;
            private readonly Stopwatch _stopwatch;
            private bool _disposed;

            public OperationScope(OperationTelemetry telemetry, string operationName, Dictionary<string, string>? properties)
            {
                _telemetry = telemetry;
                _operationName = operationName;
                _properties = properties;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                _stopwatch.Stop();
                _telemetry.RecordSuccess(_operationName, _stopwatch.Elapsed, _properties);
            }
        }
    }

    /// <summary>
    /// Metrics for a specific operation type.
    /// </summary>
    public class OperationMetrics
    {
        private long _totalCount;
        private long _successCount;
        private long _failureCount;
        private long _totalDurationTicks;
        private long _minDurationTicks = long.MaxValue;
        private long _maxDurationTicks;

        public OperationMetrics(string operationName)
        {
            OperationName = operationName;
        }

        /// <summary>
        /// Gets the operation name.
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// Gets the total number of operations.
        /// </summary>
        public long TotalCount => Interlocked.Read(ref _totalCount);

        /// <summary>
        /// Gets the number of successful operations.
        /// </summary>
        public long SuccessCount => Interlocked.Read(ref _successCount);

        /// <summary>
        /// Gets the number of failed operations.
        /// </summary>
        public long FailureCount => Interlocked.Read(ref _failureCount);

        /// <summary>
        /// Gets the success rate as a percentage.
        /// </summary>
        public double SuccessRate
        {
            get
            {
                var total = TotalCount;
                return total > 0 ? (SuccessCount * 100.0) / total : 0;
            }
        }

        /// <summary>
        /// Gets the average operation duration.
        /// </summary>
        public TimeSpan AverageDuration
        {
            get
            {
                var total = TotalCount;
                return total > 0
                    ? TimeSpan.FromTicks(Interlocked.Read(ref _totalDurationTicks) / total)
                    : TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Gets the minimum operation duration.
        /// </summary>
        public TimeSpan MinDuration => TimeSpan.FromTicks(Interlocked.Read(ref _minDurationTicks));

        /// <summary>
        /// Gets the maximum operation duration.
        /// </summary>
        public TimeSpan MaxDuration => TimeSpan.FromTicks(Interlocked.Read(ref _maxDurationTicks));

        internal void RecordSuccess(TimeSpan duration)
        {
            Interlocked.Increment(ref _totalCount);
            Interlocked.Increment(ref _successCount);
            RecordDuration(duration);
        }

        internal void RecordFailure(TimeSpan duration)
        {
            Interlocked.Increment(ref _totalCount);
            Interlocked.Increment(ref _failureCount);
            RecordDuration(duration);
        }

        private void RecordDuration(TimeSpan duration)
        {
            var ticks = duration.Ticks;
            Interlocked.Add(ref _totalDurationTicks, ticks);

            // Update min
            long currentMin;
            do
            {
                currentMin = Interlocked.Read(ref _minDurationTicks);
                if (ticks >= currentMin) break;
            } while (Interlocked.CompareExchange(ref _minDurationTicks, ticks, currentMin) != currentMin);

            // Update max
            long currentMax;
            do
            {
                currentMax = Interlocked.Read(ref _maxDurationTicks);
                if (ticks <= currentMax) break;
            } while (Interlocked.CompareExchange(ref _maxDurationTicks, ticks, currentMax) != currentMax);
        }
    }

    /// <summary>
    /// Represents a single operation event.
    /// </summary>
    public class OperationEvent
    {
        public DateTime Timestamp { get; init; }
        public required string OperationName { get; init; }
        public bool Success { get; init; }
        public TimeSpan Duration { get; init; }
        public Exception? Exception { get; init; }
        public Dictionary<string, string>? Properties { get; init; }
    }
}
