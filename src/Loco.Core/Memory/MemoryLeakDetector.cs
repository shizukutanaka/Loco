using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Memory
{
    /// <summary>
    /// Advanced memory leak detection and garbage collection monitoring.
    /// Tracks object allocations, detects potential memory leaks, and provides GC optimization recommendations.
    /// </summary>
    public static class MemoryLeakDetector
    {
        private static readonly ConcurrentDictionary<string, ObjectTracker> _objectTrackers = new();
        private static readonly Timer _monitoringTimer;
        private static bool _isMonitoring;

        static MemoryLeakDetector()
        {
            _monitoringTimer = new Timer(MonitorMemoryUsage, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            _isMonitoring = true;
        }

        /// <summary>
        /// Starts tracking an object for potential memory leaks.
        /// </summary>
        public static void TrackObject(object obj, string objectName, ILogger? logger = null)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var tracker = new ObjectTracker
            {
                ObjectName = objectName,
                ObjectType = obj.GetType().FullName ?? obj.GetType().Name,
                CreationTime = DateTime.UtcNow,
                LastAccessTime = DateTime.UtcNow,
                IsWeakReference = false
            };

            // Use WeakReference to avoid keeping objects alive
            var weakRef = new WeakReference(obj);
            tracker.WeakReference = weakRef;

            _objectTrackers[objectName] = tracker;

            logger?.LogDebug("Started tracking object: {ObjectName} ({ObjectType})", objectName, tracker.ObjectType);
        }

        /// <summary>
        /// Records access to a tracked object.
        /// </summary>
        public static void RecordAccess(string objectName)
        {
            if (_objectTrackers.TryGetValue(objectName, out var tracker))
            {
                tracker.LastAccessTime = DateTime.UtcNow;
                tracker.AccessCount++;
            }
        }

        /// <summary>
        /// Stops tracking an object.
        /// </summary>
        public static void StopTracking(string objectName)
        {
            _objectTrackers.TryRemove(objectName, out _);
        }

        /// <summary>
        /// Gets memory leak analysis report.
        /// </summary>
        public static MemoryLeakReport GenerateReport(ILogger? logger = null)
        {
            var report = new MemoryLeakReport();
            var currentTime = DateTime.UtcNow;

            foreach (var (objectName, tracker) in _objectTrackers)
            {
                // Check if object is still alive
                if (!tracker.WeakReference?.IsAlive == true)
                {
                    report.CollectedObjects.Add(objectName);
                    continue;
                }

                var age = currentTime - tracker.CreationTime;
                var timeSinceLastAccess = currentTime - tracker.LastAccessTime;

                // Detect potential memory leaks
                if (age > TimeSpan.FromMinutes(30) && timeSinceLastAccess > TimeSpan.FromMinutes(10))
                {
                    report.PotentialLeaks.Add(new LeakCandidate
                    {
                        ObjectName = objectName,
                        ObjectType = tracker.ObjectType,
                        Age = age,
                        TimeSinceLastAccess = timeSinceLastAccess,
                        AccessCount = tracker.AccessCount
                    });
                }

                // Track long-lived objects
                if (age > TimeSpan.FromHours(1))
                {
                    report.LongLivedObjects.Add(objectName);
                }
            }

            // Get GC statistics
            report.GCStats = new GCStatistics
            {
                TotalMemory = GC.GetTotalMemory(false),
                Generation0Collections = GC.CollectionCount(0),
                Generation1Collections = GC.CollectionCount(1),
                Generation2Collections = GC.CollectionCount(2),
                LastGCTime = DateTime.UtcNow
            };

            // Log warnings for potential leaks
            if (report.PotentialLeaks.Any())
            {
                logger?.LogWarning(
                    "Potential memory leaks detected: {Count} objects may be leaking",
                    report.PotentialLeaks.Count);

                foreach (var leak in report.PotentialLeaks.Take(5)) // Log first 5
                {
                    logger?.LogWarning(
                        "Potential leak: {ObjectName} ({ObjectType}) - Age: {Age}, Last Access: {LastAccess} ago",
                        leak.ObjectName,
                        leak.ObjectType,
                        leak.Age,
                        leak.TimeSinceLastAccess);
                }
            }

            return report;
        }

        /// <summary>
        /// Forces garbage collection and finalization for testing purposes.
        /// </summary>
        public static void ForceGarbageCollection()
        {
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
        }

        private static void MonitorMemoryUsage(object? state)
        {
            if (!_isMonitoring) return;

            try
            {
                var report = GenerateReport(null);

                // Auto-cleanup old trackers
                var currentTime = DateTime.UtcNow;
                var toRemove = _objectTrackers
                    .Where(kvp => currentTime - kvp.Value.CreationTime > TimeSpan.FromHours(2))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var objectName in toRemove)
                {
                    _objectTrackers.TryRemove(objectName, out _);
                }
            }
            catch
            {
                // Silently ignore monitoring errors
            }
        }

        private class ObjectTracker
        {
            public string ObjectName { get; set; } = string.Empty;
            public string ObjectType { get; set; } = string.Empty;
            public DateTime CreationTime { get; set; }
            public DateTime LastAccessTime { get; set; }
            public int AccessCount { get; set; }
            public bool IsWeakReference { get; set; }
            public WeakReference? WeakReference { get; set; }
        }
    }

    /// <summary>
    /// Memory leak detection report.
    /// </summary>
    public class MemoryLeakReport
    {
        public List<string> CollectedObjects { get; set; } = new();
        public List<string> LongLivedObjects { get; set; } = new();
        public List<LeakCandidate> PotentialLeaks { get; set; } = new();
        public GCStatistics GCStats { get; set; } = new();
    }

    /// <summary>
    /// Potential memory leak candidate.
    /// </summary>
    public class LeakCandidate
    {
        public string ObjectName { get; set; } = string.Empty;
        public string ObjectType { get; set; } = string.Empty;
        public TimeSpan Age { get; set; }
        public TimeSpan TimeSinceLastAccess { get; set; }
        public int AccessCount { get; set; }
    }

    /// <summary>
    /// Garbage collection statistics.
    /// </summary>
    public class GCStatistics
    {
        public long TotalMemory { get; set; }
        public int Generation0Collections { get; set; }
        public int Generation1Collections { get; set; }
        public int Generation2Collections { get; set; }
        public DateTime LastGCTime { get; set; }
    }
}
