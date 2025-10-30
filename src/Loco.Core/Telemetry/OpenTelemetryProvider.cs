using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Telemetry
{
    /// <summary>
    /// Provides OpenTelemetry integration for distributed tracing and metrics.
    /// Enables observability in production environments with minimal overhead.
    /// </summary>
    public class OpenTelemetryProvider : IDisposable
    {
        private static readonly ActivitySource ActivitySource = new("Loco.Automation", "1.0.0");
        private static readonly Meter Meter = new("Loco.Automation", "1.0.0");

        private readonly Counter<long> _operationCounter;
        private readonly Histogram<double> _operationDuration;
        private readonly Counter<long> _errorCounter;
        private readonly ObservableGauge<int> _activeOperations;
        private readonly ILogger? _logger;

        private int _currentActiveOperations;
        private bool _disposed;

        public OpenTelemetryProvider(ILogger? logger = null)
        {
            _logger = logger;

            // Initialize metrics
            _operationCounter = Meter.CreateCounter<long>(
                "loco.operations.total",
                description: "Total number of operations executed");

            _operationDuration = Meter.CreateHistogram<double>(
                "loco.operations.duration",
                unit: "ms",
                description: "Duration of operations in milliseconds");

            _errorCounter = Meter.CreateCounter<long>(
                "loco.operations.errors",
                description: "Total number of operation errors");

            _activeOperations = Meter.CreateObservableGauge(
                "loco.operations.active",
                () => _currentActiveOperations,
                description: "Number of currently active operations");

            _logger?.LogInformation("OpenTelemetry provider initialized");
        }

        /// <summary>
        /// Starts a new traced operation with distributed tracing support.
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="tags">Optional tags to attach to the trace</param>
        /// <returns>A disposable activity scope</returns>
        public Activity? StartActivity(string operationName, Dictionary<string, object?>? tags = null)
        {
            var activity = ActivitySource.StartActivity(operationName, ActivityKind.Internal);

            if (activity != null && tags != null)
            {
                foreach (var tag in tags)
                {
                    activity.SetTag(tag.Key, tag.Value);
                }
            }

            System.Threading.Interlocked.Increment(ref _currentActiveOperations);

            return activity;
        }

        /// <summary>
        /// Records operation completion with metrics.
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="duration">Duration of the operation</param>
        /// <param name="success">Whether the operation succeeded</param>
        /// <param name="tags">Optional tags for metric dimensions</param>
        public void RecordOperation(
            string operationName,
            TimeSpan duration,
            bool success,
            Dictionary<string, object?>? tags = null)
        {
            var tagList = new TagList();
            tagList.Add("operation", operationName);
            tagList.Add("success", success);

            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    tagList.Add(tag.Key, tag.Value);
                }
            }

            _operationCounter.Add(1, tagList);
            _operationDuration.Record(duration.TotalMilliseconds, tagList);

            if (!success)
            {
                _errorCounter.Add(1, tagList);
            }

            System.Threading.Interlocked.Decrement(ref _currentActiveOperations);
        }

        /// <summary>
        /// Creates a traced operation scope that automatically tracks duration and success.
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="tags">Optional tags</param>
        /// <returns>A disposable scope that tracks the operation</returns>
        public IDisposable CreateOperationScope(string operationName, Dictionary<string, object?>? tags = null)
        {
            return new TracedOperationScope(this, operationName, tags);
        }

        /// <summary>
        /// Records an error with contextual information.
        /// </summary>
        /// <param name="operationName">Name of the operation where error occurred</param>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="tags">Optional additional context</param>
        public void RecordError(string operationName, Exception exception, Dictionary<string, object?>? tags = null)
        {
            var tagList = new TagList
            {
                { "operation", operationName },
                { "error.type", exception.GetType().Name },
                { "error.message", exception.Message }
            };

            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    tagList.Add(tag.Key, tag.Value);
                }
            }

            _errorCounter.Add(1, tagList);
            _logger?.LogError(exception, "Error in operation {OperationName}", operationName);
        }

        /// <summary>
        /// Gets the current number of active operations.
        /// </summary>
        public int ActiveOperations => _currentActiveOperations;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            ActivitySource.Dispose();
            Meter.Dispose();

            _logger?.LogInformation("OpenTelemetry provider disposed");
        }

        private class TracedOperationScope : IDisposable
        {
            private readonly OpenTelemetryProvider _provider;
            private readonly string _operationName;
            private readonly Dictionary<string, object?>? _tags;
            private readonly Activity? _activity;
            private readonly Stopwatch _stopwatch;
            private bool _disposed;
            private bool _success = true;

            public TracedOperationScope(
                OpenTelemetryProvider provider,
                string operationName,
                Dictionary<string, object?>? tags)
            {
                _provider = provider;
                _operationName = operationName;
                _tags = tags;
                _activity = provider.StartActivity(operationName, tags);
                _stopwatch = Stopwatch.StartNew();
            }

            public void SetSuccess(bool success)
            {
                _success = success;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                _stopwatch.Stop();

                _activity?.SetTag("success", _success);
                _activity?.Dispose();

                _provider.RecordOperation(_operationName, _stopwatch.Elapsed, _success, _tags);
            }
        }
    }
}
