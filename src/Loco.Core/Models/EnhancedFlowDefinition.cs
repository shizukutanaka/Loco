using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Loco.Core.Models
{
    /// <summary>
    /// Extended flow definition with optimization and security features
    /// </summary>
    public class EnhancedFlowDefinition : FlowDefinition
    {
        // Optimization properties
        [JsonPropertyName("executionMode")]
        public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.Sequential;

        [JsonPropertyName("maxParallelism")]
        public int MaxParallelism { get; set; } = Environment.ProcessorCount;

        [JsonPropertyName("cachingEnabled")]
        public bool CachingEnabled { get; set; }

        [JsonPropertyName("cacheDuration")]
        public TimeSpan? CacheDuration { get; set; }

        [JsonPropertyName("executionFrequency")]
        public int ExecutionFrequency { get; set; }

        // Security properties
        [JsonPropertyName("securityLevel")]
        public SecurityLevel SecurityLevel { get; set; } = SecurityLevel.Standard;

        [JsonPropertyName("auditEnabled")]
        public bool AuditEnabled { get; set; } = true;

        [JsonPropertyName("requiredPermissions")]
        public List<string> RequiredPermissions { get; set; } = new();

        [JsonPropertyName("encryptionEnabled")]
        public bool EncryptionEnabled { get; set; }

        // Performance tracking
        [JsonPropertyName("performanceProfile")]
        public PerformanceProfile PerformanceProfile { get; set; }

        [JsonPropertyName("metrics")]
        public FlowMetrics Metrics { get; set; } = new();

        // Advanced features
        [JsonPropertyName("errorHandling")]
        public ErrorHandlingStrategy ErrorHandling { get; set; } = new();

        [JsonPropertyName("scheduling")]
        public SchedulingOptions Scheduling { get; set; } = new();

        [JsonPropertyName("dependencies")]
        public List<FlowDependency> Dependencies { get; set; } = new();

        [JsonPropertyName("optimization")]
        public OptimizationSettings Optimization { get; set; } = new();

        // Methods
        public EnhancedFlowDefinition Clone()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(this);
            return System.Text.Json.JsonSerializer.Deserialize<EnhancedFlowDefinition>(json);
        }

        public bool IsOptimized()
        {
            return Metadata != null && 
                   Metadata.ContainsKey("optimized") && 
                   Metadata["optimized"].ToString() == "true";
        }

        public void ApplyOptimization(OptimizationResult result)
        {
            if (result.OptimizedFlow != null)
            {
                ExecutionMode = result.OptimizedFlow.ExecutionMode;
                MaxParallelism = result.OptimizedFlow.MaxParallelism;
                CachingEnabled = result.OptimizedFlow.CachingEnabled;
                CacheDuration = result.OptimizedFlow.CacheDuration;
                
                Metadata["optimized"] = "true";
                Metadata["optimizedAt"] = DateTime.UtcNow.ToString("O");
                Metadata["optimizationScore"] = result.ImprovementScore;
            }
        }
    }

    public enum ExecutionMode
    {
        Sequential,
        Parallel,
        Hybrid,
        Adaptive
    }

    public enum SecurityLevel
    {
        None,
        Basic,
        Standard,
        High,
        Maximum
    }

    public class PerformanceProfile
    {
        [JsonPropertyName("cpuIntensive")]
        public bool CpuIntensive { get; set; }

        [JsonPropertyName("memoryIntensive")]
        public bool MemoryIntensive { get; set; }

        [JsonPropertyName("ioIntensive")]
        public bool IoIntensive { get; set; }

        [JsonPropertyName("expectedDuration")]
        public TimeSpan ExpectedDuration { get; set; }

        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 5;
    }

    public class FlowMetrics
    {
        [JsonPropertyName("totalExecutions")]
        public long TotalExecutions { get; set; }

        [JsonPropertyName("successfulExecutions")]
        public long SuccessfulExecutions { get; set; }

        [JsonPropertyName("failedExecutions")]
        public long FailedExecutions { get; set; }

        [JsonPropertyName("averageExecutionTime")]
        public double AverageExecutionTime { get; set; }

        [JsonPropertyName("lastExecutionTime")]
        public DateTime? LastExecutionTime { get; set; }

        [JsonPropertyName("errorRate")]
        public double ErrorRate => TotalExecutions > 0 
            ? (double)FailedExecutions / TotalExecutions 
            : 0;

        [JsonPropertyName("successRate")]
        public double SuccessRate => TotalExecutions > 0 
            ? (double)SuccessfulExecutions / TotalExecutions 
            : 0;
    }

    public class ErrorHandlingStrategy
    {
        [JsonPropertyName("retryPolicy")]
        public RetryPolicy RetryPolicy { get; set; } = new();

        [JsonPropertyName("fallbackAction")]
        public string FallbackAction { get; set; }

        [JsonPropertyName("circuitBreaker")]
        public CircuitBreakerSettings CircuitBreaker { get; set; } = new();

        [JsonPropertyName("errorNotification")]
        public bool ErrorNotification { get; set; } = true;
    }

    public class RetryPolicy
    {
        [JsonPropertyName("maxRetries")]
        public int MaxRetries { get; set; } = 3;

        [JsonPropertyName("initialDelay")]
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

        [JsonPropertyName("backoffMultiplier")]
        public double BackoffMultiplier { get; set; } = 2.0;

        [JsonPropertyName("maxDelay")]
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);
    }

    public class CircuitBreakerSettings
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("failureThreshold")]
        public int FailureThreshold { get; set; } = 5;

        [JsonPropertyName("successThreshold")]
        public int SuccessThreshold { get; set; } = 2;

        [JsonPropertyName("timeout")]
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(1);
    }

    public class SchedulingOptions
    {
        [JsonPropertyName("type")]
        public ScheduleType Type { get; set; } = ScheduleType.None;

        [JsonPropertyName("cronExpression")]
        public string CronExpression { get; set; }

        [JsonPropertyName("interval")]
        public TimeSpan? Interval { get; set; }

        [JsonPropertyName("startTime")]
        public DateTime? StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public DateTime? EndTime { get; set; }

        [JsonPropertyName("timezone")]
        public string Timezone { get; set; } = "UTC";
    }

    public enum ScheduleType
    {
        None,
        Once,
        Interval,
        Cron,
        Event
    }

    public class FlowDependency
    {
        [JsonPropertyName("flowId")]
        public string FlowId { get; set; }

        [JsonPropertyName("type")]
        public DependencyType Type { get; set; }

        [JsonPropertyName("condition")]
        public string Condition { get; set; }
    }

    public enum DependencyType
    {
        Required,
        Optional,
        Sequential,
        Parallel
    }

    public class OptimizationSettings
    {
        [JsonPropertyName("autoOptimize")]
        public bool AutoOptimize { get; set; }

        [JsonPropertyName("optimizationLevel")]
        public OptimizationLevel Level { get; set; } = OptimizationLevel.Balanced;

        [JsonPropertyName("targetMetric")]
        public string TargetMetric { get; set; } = "execution_time";

        [JsonPropertyName("constraints")]
        public Dictionary<string, object> Constraints { get; set; } = new();
    }

    public enum OptimizationLevel
    {
        None,
        Conservative,
        Balanced,
        Aggressive,
        Maximum
    }

    public class OptimizationResult
    {
        public EnhancedFlowDefinition OptimizedFlow { get; set; }
        public double ImprovementScore { get; set; }
        public List<string> AppliedOptimizations { get; set; }
        public Dictionary<string, object> Metrics { get; set; }
    }

    // Extension for ActionDefinition
    public class EnhancedActionDefinition : ActionDefinition
    {
        [JsonPropertyName("config")]
        public Dictionary<string, object> Config { get; set; } = new();

        [JsonPropertyName("cost")]
        public double Cost { get; set; } = 1.0;

        [JsonPropertyName("parallel")]
        public bool Parallel { get; set; }

        [JsonPropertyName("dependencies")]
        public List<string> Dependencies { get; set; } = new();
    }

    // Extension for ConditionDefinition
    public class EnhancedConditionDefinition : ConditionDefinition
    {
        [JsonPropertyName("cost")]
        public double Cost { get; set; } = 1.0;

        [JsonPropertyName("shortCircuit")]
        public bool ShortCircuit { get; set; } = true;

        [JsonPropertyName("cacheResult")]
        public bool CacheResult { get; set; }
    }
}
