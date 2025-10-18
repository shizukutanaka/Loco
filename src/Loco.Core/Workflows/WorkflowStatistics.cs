using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Loco.Core.Workflows
{
    /// <summary>
    /// Tracks workflow execution statistics (lightweight, with optional persistence).
    /// </summary>
    public class WorkflowStatistics
    {
        private readonly ConcurrentDictionary<string, WorkflowStats> _stats = new();
        private readonly string? _persistenceFile;

        public WorkflowStatistics(string? persistenceFile = null)
        {
            _persistenceFile = persistenceFile;
            LoadFromDisk();
        }

        public void RecordExecution(string workflowId, bool success, TimeSpan duration)
        {
            var stats = _stats.GetOrAdd(workflowId, _ => new WorkflowStats { WorkflowId = workflowId });

            lock (stats)
            {
                stats.TotalExecutions++;
                if (success)
                    stats.SuccessfulExecutions++;
                else
                    stats.FailedExecutions++;

                stats.LastExecutionTime = DateTime.UtcNow;
                stats.TotalDuration += duration;
                stats.AverageDuration = TimeSpan.FromMilliseconds(
                    stats.TotalDuration.TotalMilliseconds / stats.TotalExecutions);

                if (duration > stats.MaxDuration)
                    stats.MaxDuration = duration;
                if (stats.MinDuration == TimeSpan.Zero || duration < stats.MinDuration)
                    stats.MinDuration = duration;
            }

            SaveToDisk();
        }

        private void LoadFromDisk()
        {
            if (string.IsNullOrEmpty(_persistenceFile) || !File.Exists(_persistenceFile))
                return;

            try
            {
                var json = File.ReadAllText(_persistenceFile);
                var statsList = JsonSerializer.Deserialize<List<WorkflowStats>>(json);
                if (statsList != null)
                {
                    foreach (var stat in statsList)
                    {
                        _stats[stat.WorkflowId] = stat;
                    }
                }
            }
            catch
            {
                // Ignore errors loading stats
            }
        }

        private void SaveToDisk()
        {
            if (string.IsNullOrEmpty(_persistenceFile))
                return;

            try
            {
                var statsList = _stats.Values.ToList();
                var json = JsonSerializer.Serialize(statsList, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_persistenceFile, json);
            }
            catch
            {
                // Ignore errors saving stats
            }
        }

        public WorkflowStats? GetStats(string workflowId)
        {
            return _stats.TryGetValue(workflowId, out var stats) ? stats : null;
        }

        public IEnumerable<WorkflowStats> GetAllStats()
        {
            return _stats.Values.ToList();
        }

        public void Reset(string? workflowId = null)
        {
            if (workflowId != null)
                _stats.TryRemove(workflowId, out _);
            else
                _stats.Clear();
        }
    }

    public class WorkflowStats
    {
        public string WorkflowId { get; set; } = string.Empty;
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public DateTime? LastExecutionTime { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public TimeSpan MinDuration { get; set; }
        public TimeSpan MaxDuration { get; set; }

        public double SuccessRate => TotalExecutions > 0
            ? (double)SuccessfulExecutions / TotalExecutions * 100
            : 0;

        public override string ToString()
        {
            return $"Workflow: {WorkflowId}\n" +
                   $"  Executions: {TotalExecutions} (Success: {SuccessfulExecutions}, Failed: {FailedExecutions})\n" +
                   $"  Success Rate: {SuccessRate:F1}%\n" +
                   $"  Duration: Avg={AverageDuration.TotalSeconds:F2}s, Min={MinDuration.TotalSeconds:F2}s, Max={MaxDuration.TotalSeconds:F2}s\n" +
                   $"  Last Run: {LastExecutionTime}";
        }
    }
}
