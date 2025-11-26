// =============================================================================
// Continuous Profiling Engine
// Parca/Pyroscope-based profiling and performance analysis engine
// Based on: Parca, Pyroscope, pprof, Polar Signals
// Research: https://www.parca.dev, https://pyroscope.io
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.PlatformEngineering
{
    #region Enums

    /// <summary>
    /// Profile type
    /// </summary>
    public enum ProfileType
    {
        CPU,               // CPU time profiling
        Memory,            // Memory allocation profiling
        Goroutine,         // Go goroutine profiling
        Mutex,             // Mutex contention profiling
        Block,             // Blocking profiling
        Heap,              // Heap memory profiling
        Allocs,            // Allocation rate profiling
        ThreadCreate,      // Thread creation profiling
        WallTime,          // Wall clock time profiling
        Exceptions,        // Exception profiling
        Lock,              // Lock contention profiling
        IOWait,            // I/O wait profiling
        Custom             // Custom profile type
    }

    /// <summary>
    /// Profile format
    /// </summary>
    public enum ProfileFormat
    {
        Pprof,             // Google pprof format
        JFR,               // Java Flight Recorder
        Perf,              // Linux perf format
        Collapsed,         // Collapsed stack format
        Pyroscope          // Pyroscope native format
    }

    /// <summary>
    /// Target runtime/language
    /// </summary>
    public enum ProfileTarget
    {
        Go,
        Java,
        Python,
        NodeJS,
        DotNet,
        Rust,
        Ruby,
        PHP,
        eBPF                // Kernel-level with eBPF
    }

    /// <summary>
    /// Comparison mode
    /// </summary>
    public enum ComparisonMode
    {
        Diff,              // Show difference
        Ratio,             // Show ratio
        SideBySide,        // Side by side view
        Baseline           // Compare against baseline
    }

    /// <summary>
    /// Alert condition type
    /// </summary>
    public enum ProfileAlertCondition
    {
        CPUSpike,          // CPU usage spike
        MemoryLeak,        // Memory leak detection
        ContentionHigh,    // High lock contention
        SlowFunction,      // Function taking too long
        Regression,        // Performance regression
        Anomaly            // Statistical anomaly
    }

    #endregion

    #region Core Types

    /// <summary>
    /// Profile data record
    /// </summary>
    public class ProfileData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Profile type
        /// </summary>
        public ProfileType Type { get; set; }

        /// <summary>
        /// Profile format
        /// </summary>
        public ProfileFormat Format { get; set; } = ProfileFormat.Pprof;

        /// <summary>
        /// Target application/service
        /// </summary>
        public ProfileTarget Target { get; set; }

        /// <summary>
        /// Service identifier
        /// </summary>
        public string ServiceId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// Labels for filtering
        /// </summary>
        public Dictionary<string, string> Labels { get; set; } = new();

        /// <summary>
        /// Profile time window
        /// </summary>
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;

        /// <summary>
        /// Sample count
        /// </summary>
        public long SampleCount { get; set; }

        /// <summary>
        /// Raw profile data (base64 encoded pprof)
        /// </summary>
        public string? RawData { get; set; }

        /// <summary>
        /// Parsed stack traces
        /// </summary>
        public List<StackTrace> StackTraces { get; set; } = new();

        /// <summary>
        /// Aggregated function statistics
        /// </summary>
        public List<FunctionStats> FunctionStats { get; set; } = new();

        /// <summary>
        /// Metadata
        /// </summary>
        public ProfileMetadata Metadata { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Profile metadata
    /// </summary>
    public class ProfileMetadata
    {
        public string Hostname { get; set; } = string.Empty;
        public string PodName { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string ContainerId { get; set; } = string.Empty;
        public string RuntimeVersion { get; set; } = string.Empty;
        public string ProfilerVersion { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string BuildId { get; set; } = string.Empty;
        public string CommitHash { get; set; } = string.Empty;
    }

    /// <summary>
    /// Stack trace record
    /// </summary>
    public class StackTrace
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Stack frames (top to bottom)
        /// </summary>
        public List<StackFrame> Frames { get; set; } = new();

        /// <summary>
        /// Sample value (time in ns, bytes, count, etc.)
        /// </summary>
        public long Value { get; set; }

        /// <summary>
        /// Number of samples with this stack
        /// </summary>
        public long Count { get; set; }

        /// <summary>
        /// Labels associated with this stack
        /// </summary>
        public Dictionary<string, string> Labels { get; set; } = new();
    }

    /// <summary>
    /// Stack frame
    /// </summary>
    public class StackFrame
    {
        /// <summary>
        /// Function name
        /// </summary>
        public string Function { get; set; } = string.Empty;

        /// <summary>
        /// File name
        /// </summary>
        public string File { get; set; } = string.Empty;

        /// <summary>
        /// Line number
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// Module/package name
        /// </summary>
        public string Module { get; set; } = string.Empty;

        /// <summary>
        /// Memory address
        /// </summary>
        public ulong Address { get; set; }

        /// <summary>
        /// Inlined function
        /// </summary>
        public bool Inlined { get; set; }
    }

    /// <summary>
    /// Function-level statistics
    /// </summary>
    public class FunctionStats
    {
        public string Function { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;

        /// <summary>
        /// Flat value (self time/memory)
        /// </summary>
        public long Flat { get; set; }

        /// <summary>
        /// Flat percentage
        /// </summary>
        public double FlatPercent { get; set; }

        /// <summary>
        /// Cumulative value (including called functions)
        /// </summary>
        public long Cumulative { get; set; }

        /// <summary>
        /// Cumulative percentage
        /// </summary>
        public double CumulativePercent { get; set; }

        /// <summary>
        /// Sample count
        /// </summary>
        public long SampleCount { get; set; }

        /// <summary>
        /// Called functions
        /// </summary>
        public List<string> Callees { get; set; } = new();

        /// <summary>
        /// Calling functions
        /// </summary>
        public List<string> Callers { get; set; } = new();
    }

    #endregion

    #region Query Types

    /// <summary>
    /// Profile query
    /// </summary>
    public class ProfileQuery
    {
        /// <summary>
        /// Service to query
        /// </summary>
        public string? ServiceId { get; set; }

        /// <summary>
        /// Profile type filter
        /// </summary>
        public ProfileType? Type { get; set; }

        /// <summary>
        /// Time range
        /// </summary>
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Label selectors
        /// </summary>
        public Dictionary<string, string> LabelSelectors { get; set; } = new();

        /// <summary>
        /// Function name filter
        /// </summary>
        public string? FunctionFilter { get; set; }

        /// <summary>
        /// Aggregation settings
        /// </summary>
        public ProfileAggregation Aggregation { get; set; } = new();

        /// <summary>
        /// Maximum results
        /// </summary>
        public int MaxResults { get; set; } = 100;
    }

    /// <summary>
    /// Aggregation settings
    /// </summary>
    public class ProfileAggregation
    {
        /// <summary>
        /// Group by labels
        /// </summary>
        public List<string> GroupBy { get; set; } = new();

        /// <summary>
        /// Merge profiles in time window
        /// </summary>
        public bool MergeProfiles { get; set; } = true;

        /// <summary>
        /// Step size for time series
        /// </summary>
        public TimeSpan Step { get; set; } = TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// Query result
    /// </summary>
    public class ProfileQueryResult
    {
        public string QueryId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Merged profile data
        /// </summary>
        public ProfileData? MergedProfile { get; set; }

        /// <summary>
        /// Individual profiles
        /// </summary>
        public List<ProfileData> Profiles { get; set; } = new();

        /// <summary>
        /// Top functions
        /// </summary>
        public List<FunctionStats> TopFunctions { get; set; } = new();

        /// <summary>
        /// Time series data
        /// </summary>
        public List<ProfileTimeSeries> TimeSeries { get; set; } = new();

        /// <summary>
        /// Flamegraph data
        /// </summary>
        public FlamegraphData? Flamegraph { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Profile time series point
    /// </summary>
    public class ProfileTimeSeries
    {
        public DateTime Timestamp { get; set; }
        public long Value { get; set; }
        public Dictionary<string, string> Labels { get; set; } = new();
    }

    #endregion

    #region Flamegraph Types

    /// <summary>
    /// Flamegraph data structure
    /// </summary>
    public class FlamegraphData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Root node
        /// </summary>
        public FlamegraphNode Root { get; set; } = new();

        /// <summary>
        /// Total samples
        /// </summary>
        public long TotalSamples { get; set; }

        /// <summary>
        /// Total value
        /// </summary>
        public long TotalValue { get; set; }

        /// <summary>
        /// Value unit (ns, bytes, count)
        /// </summary>
        public string Unit { get; set; } = "nanoseconds";

        /// <summary>
        /// Profile type
        /// </summary>
        public ProfileType Type { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Flamegraph node
    /// </summary>
    public class FlamegraphNode
    {
        /// <summary>
        /// Function/frame name
        /// </summary>
        public string Name { get; set; } = "root";

        /// <summary>
        /// Self value (flat)
        /// </summary>
        public long Self { get; set; }

        /// <summary>
        /// Total value (cumulative)
        /// </summary>
        public long Total { get; set; }

        /// <summary>
        /// Children nodes
        /// </summary>
        public List<FlamegraphNode> Children { get; set; } = new();

        /// <summary>
        /// Source location
        /// </summary>
        public string? File { get; set; }
        public int? Line { get; set; }

        /// <summary>
        /// Module/package
        /// </summary>
        public string? Module { get; set; }
    }

    #endregion

    #region Comparison Types

    /// <summary>
    /// Profile comparison result
    /// </summary>
    public class ProfileComparison
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Base profile (before)
        /// </summary>
        public string BaseProfileId { get; set; } = string.Empty;

        /// <summary>
        /// Comparison profile (after)
        /// </summary>
        public string CompareProfileId { get; set; } = string.Empty;

        /// <summary>
        /// Comparison mode
        /// </summary>
        public ComparisonMode Mode { get; set; }

        /// <summary>
        /// Function-level diffs
        /// </summary>
        public List<FunctionDiff> FunctionDiffs { get; set; } = new();

        /// <summary>
        /// Overall summary
        /// </summary>
        public ComparisonSummary Summary { get; set; } = new();

        /// <summary>
        /// Diff flamegraph
        /// </summary>
        public FlamegraphData? DiffFlamegraph { get; set; }

        /// <summary>
        /// Detected regressions
        /// </summary>
        public List<PerformanceRegression> Regressions { get; set; } = new();

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Function-level diff
    /// </summary>
    public class FunctionDiff
    {
        public string Function { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;

        /// <summary>
        /// Base value
        /// </summary>
        public long BaseValue { get; set; }

        /// <summary>
        /// Compare value
        /// </summary>
        public long CompareValue { get; set; }

        /// <summary>
        /// Absolute difference
        /// </summary>
        public long Difference { get; set; }

        /// <summary>
        /// Percentage change
        /// </summary>
        public double PercentChange { get; set; }

        /// <summary>
        /// Is this a regression?
        /// </summary>
        public bool IsRegression { get; set; }
    }

    /// <summary>
    /// Comparison summary
    /// </summary>
    public class ComparisonSummary
    {
        public long BaseTotalValue { get; set; }
        public long CompareTotalValue { get; set; }
        public double OverallChange { get; set; }
        public int ImprovedFunctions { get; set; }
        public int RegressedFunctions { get; set; }
        public int NewFunctions { get; set; }
        public int RemovedFunctions { get; set; }
    }

    /// <summary>
    /// Performance regression detection
    /// </summary>
    public class PerformanceRegression
    {
        public string Function { get; set; } = string.Empty;
        public string Severity { get; set; } = "medium";
        public double ImpactPercent { get; set; }
        public long ImpactValue { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<string> PossibleCauses { get; set; } = new();
    }

    #endregion

    #region Analysis Types

    /// <summary>
    /// Profile analysis result
    /// </summary>
    public class ProfileAnalysis
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProfileId { get; set; } = string.Empty;

        /// <summary>
        /// Hot spots (high CPU/memory functions)
        /// </summary>
        public List<HotSpot> HotSpots { get; set; } = new();

        /// <summary>
        /// Potential memory leaks
        /// </summary>
        public List<MemoryLeakCandidate> MemoryLeaks { get; set; } = new();

        /// <summary>
        /// Lock contention issues
        /// </summary>
        public List<ContentionIssue> ContentionIssues { get; set; } = new();

        /// <summary>
        /// Optimization recommendations
        /// </summary>
        public List<OptimizationHint> Recommendations { get; set; } = new();

        /// <summary>
        /// Analysis summary
        /// </summary>
        public AnalysisSummary Summary { get; set; } = new();

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Hot spot identification
    /// </summary>
    public class HotSpot
    {
        public string Function { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public int Line { get; set; }

        /// <summary>
        /// Percentage of total
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// Absolute value
        /// </summary>
        public long Value { get; set; }

        /// <summary>
        /// Hot spot type
        /// </summary>
        public string Type { get; set; } = string.Empty; // cpu, memory, io

        /// <summary>
        /// Call path to this hot spot
        /// </summary>
        public List<string> CallPath { get; set; } = new();
    }

    /// <summary>
    /// Memory leak candidate
    /// </summary>
    public class MemoryLeakCandidate
    {
        public string Function { get; set; } = string.Empty;
        public string AllocationType { get; set; } = string.Empty;

        /// <summary>
        /// Growth rate (bytes/sec)
        /// </summary>
        public double GrowthRate { get; set; }

        /// <summary>
        /// Total allocated
        /// </summary>
        public long TotalAllocated { get; set; }

        /// <summary>
        /// Confidence score (0-1)
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Stack trace leading to allocation
        /// </summary>
        public List<string> AllocationStack { get; set; } = new();
    }

    /// <summary>
    /// Lock contention issue
    /// </summary>
    public class ContentionIssue
    {
        public string Lock { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// Total wait time
        /// </summary>
        public TimeSpan TotalWaitTime { get; set; }

        /// <summary>
        /// Contention count
        /// </summary>
        public long ContentionCount { get; set; }

        /// <summary>
        /// Average wait time
        /// </summary>
        public TimeSpan AverageWaitTime { get; set; }

        /// <summary>
        /// Competing functions
        /// </summary>
        public List<string> CompetingFunctions { get; set; } = new();
    }

    /// <summary>
    /// Optimization hint
    /// </summary>
    public class OptimizationHint
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // cpu, memory, io, concurrency
        public string Priority { get; set; } = "medium";
        public string AffectedFunction { get; set; } = string.Empty;
        public List<string> SuggestedActions { get; set; } = new();
        public double PotentialImprovement { get; set; }
    }

    /// <summary>
    /// Analysis summary
    /// </summary>
    public class AnalysisSummary
    {
        public int HotSpotCount { get; set; }
        public int PotentialLeakCount { get; set; }
        public int ContentionIssueCount { get; set; }
        public int RecommendationCount { get; set; }
        public string OverallHealth { get; set; } = "good"; // good, warning, critical
        public double EfficiencyScore { get; set; }
    }

    #endregion

    #region Alert Types

    /// <summary>
    /// Profile alert configuration
    /// </summary>
    public class ProfileAlertConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Alert condition
        /// </summary>
        public ProfileAlertCondition Condition { get; set; }

        /// <summary>
        /// Threshold configuration
        /// </summary>
        public AlertThreshold Threshold { get; set; } = new();

        /// <summary>
        /// Target service
        /// </summary>
        public string? ServiceId { get; set; }

        /// <summary>
        /// Target function (optional)
        /// </summary>
        public string? TargetFunction { get; set; }

        /// <summary>
        /// Notification channels
        /// </summary>
        public List<string> NotificationChannels { get; set; } = new();

        /// <summary>
        /// Severity
        /// </summary>
        public string Severity { get; set; } = "warning";

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Alert threshold
    /// </summary>
    public class AlertThreshold
    {
        /// <summary>
        /// Threshold value
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Comparison operator
        /// </summary>
        public string Operator { get; set; } = "gt"; // gt, lt, gte, lte, eq

        /// <summary>
        /// Duration for sustained threshold
        /// </summary>
        public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Percentage change threshold (for regressions)
        /// </summary>
        public double? ChangePercent { get; set; }
    }

    /// <summary>
    /// Profile alert instance
    /// </summary>
    public class ProfileAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ConfigId { get; set; } = string.Empty;
        public string AlertName { get; set; } = string.Empty;

        /// <summary>
        /// Alert condition
        /// </summary>
        public ProfileAlertCondition Condition { get; set; }

        /// <summary>
        /// Affected service
        /// </summary>
        public string ServiceId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// Affected function (if applicable)
        /// </summary>
        public string? Function { get; set; }

        /// <summary>
        /// Current value
        /// </summary>
        public double CurrentValue { get; set; }

        /// <summary>
        /// Threshold value
        /// </summary>
        public double ThresholdValue { get; set; }

        /// <summary>
        /// Alert message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Related profile IDs
        /// </summary>
        public List<string> RelatedProfiles { get; set; } = new();

        public string Status { get; set; } = "firing";
        public DateTime FiredAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
    }

    #endregion

    #region Target Configuration

    /// <summary>
    /// Profiling target configuration
    /// </summary>
    public class ProfilingTarget
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Service identifier
        /// </summary>
        public string ServiceId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// Target runtime
        /// </summary>
        public ProfileTarget Target { get; set; }

        /// <summary>
        /// Enabled profile types
        /// </summary>
        public List<ProfileType> EnabledProfiles { get; set; } = new();

        /// <summary>
        /// Sampling rate (samples per second)
        /// </summary>
        public int SamplingRate { get; set; } = 100;

        /// <summary>
        /// Upload interval
        /// </summary>
        public TimeSpan UploadInterval { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Label selectors (Kubernetes style)
        /// </summary>
        public Dictionary<string, string> LabelSelectors { get; set; } = new();

        /// <summary>
        /// Agent configuration
        /// </summary>
        public AgentConfig AgentConfig { get; set; } = new();

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Profiling agent configuration
    /// </summary>
    public class AgentConfig
    {
        /// <summary>
        /// Agent type (parca-agent, pyroscope-agent, ebpf)
        /// </summary>
        public string AgentType { get; set; } = "parca-agent";

        /// <summary>
        /// Agent version
        /// </summary>
        public string Version { get; set; } = "latest";

        /// <summary>
        /// Resource limits
        /// </summary>
        public AgentResourceLimits Resources { get; set; } = new();

        /// <summary>
        /// Additional agent flags
        /// </summary>
        public Dictionary<string, string> Flags { get; set; } = new();
    }

    /// <summary>
    /// Agent resource limits
    /// </summary>
    public class AgentResourceLimits
    {
        public string CpuLimit { get; set; } = "100m";
        public string MemoryLimit { get; set; } = "128Mi";
    }

    #endregion

    #region Interface

    /// <summary>
    /// Continuous Profiling Engine interface
    /// Provides Parca/Pyroscope-style profiling capabilities
    /// </summary>
    public interface IContinuousProfilingEngine
    {
        #region Profile Ingestion

        /// <summary>
        /// Ingest profile data
        /// </summary>
        Task<ProfileData> IngestProfileAsync(
            string tenantId,
            ProfileData profile,
            CancellationToken cancellation = default);

        /// <summary>
        /// Ingest raw pprof data
        /// </summary>
        Task<ProfileData> IngestPprofAsync(
            string tenantId,
            string serviceId,
            ProfileType type,
            byte[] pprofData,
            Dictionary<string, string> labels,
            CancellationToken cancellation = default);

        #endregion

        #region Profile Query

        /// <summary>
        /// Query profiles
        /// </summary>
        Task<ProfileQueryResult> QueryProfilesAsync(
            string tenantId,
            ProfileQuery query,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get profile by ID
        /// </summary>
        Task<ProfileData?> GetProfileAsync(
            string tenantId,
            string profileId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Generate flamegraph
        /// </summary>
        Task<FlamegraphData> GenerateFlamegraphAsync(
            string tenantId,
            ProfileQuery query,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get function statistics
        /// </summary>
        Task<List<FunctionStats>> GetTopFunctionsAsync(
            string tenantId,
            ProfileQuery query,
            int limit = 50,
            CancellationToken cancellation = default);

        #endregion

        #region Profile Comparison

        /// <summary>
        /// Compare two profiles
        /// </summary>
        Task<ProfileComparison> CompareProfilesAsync(
            string tenantId,
            string baseProfileId,
            string compareProfileId,
            ComparisonMode mode = ComparisonMode.Diff,
            CancellationToken cancellation = default);

        /// <summary>
        /// Compare against baseline
        /// </summary>
        Task<ProfileComparison> CompareToBaselineAsync(
            string tenantId,
            string profileId,
            string baselineId,
            CancellationToken cancellation = default);

        #endregion

        #region Analysis

        /// <summary>
        /// Analyze profile for issues
        /// </summary>
        Task<ProfileAnalysis> AnalyzeProfileAsync(
            string tenantId,
            string profileId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Detect memory leaks
        /// </summary>
        Task<List<MemoryLeakCandidate>> DetectMemoryLeaksAsync(
            string tenantId,
            string serviceId,
            TimeSpan window,
            CancellationToken cancellation = default);

        /// <summary>
        /// Detect performance regressions
        /// </summary>
        Task<List<PerformanceRegression>> DetectRegressionsAsync(
            string tenantId,
            string serviceId,
            TimeSpan window,
            CancellationToken cancellation = default);

        #endregion

        #region Targets & Agents

        /// <summary>
        /// Register profiling target
        /// </summary>
        Task<ProfilingTarget> RegisterTargetAsync(
            string tenantId,
            ProfilingTarget target,
            CancellationToken cancellation = default);

        /// <summary>
        /// List profiling targets
        /// </summary>
        Task<List<ProfilingTarget>> ListTargetsAsync(
            string tenantId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Generate agent configuration
        /// </summary>
        Task<string> GenerateAgentConfigAsync(
            string tenantId,
            string targetId,
            CancellationToken cancellation = default);

        #endregion

        #region Alerting

        /// <summary>
        /// Create alert configuration
        /// </summary>
        Task<ProfileAlertConfig> CreateAlertConfigAsync(
            string tenantId,
            ProfileAlertConfig config,
            CancellationToken cancellation = default);

        /// <summary>
        /// Check alerts
        /// </summary>
        Task<List<ProfileAlert>> CheckAlertsAsync(
            string tenantId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get active alerts
        /// </summary>
        Task<List<ProfileAlert>> GetActiveAlertsAsync(
            string tenantId,
            CancellationToken cancellation = default);

        #endregion
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Continuous Profiling Engine implementation
    /// </summary>
    public class ContinuousProfilingEngine : IContinuousProfilingEngine
    {
        private readonly ILogger<ContinuousProfilingEngine> _logger;
        private readonly Dictionary<string, List<ProfileData>> _profiles = new();
        private readonly Dictionary<string, Dictionary<string, ProfilingTarget>> _targets = new();
        private readonly Dictionary<string, Dictionary<string, ProfileAlertConfig>> _alertConfigs = new();
        private readonly Dictionary<string, List<ProfileAlert>> _alerts = new();

        private readonly Random _random = new();

        public ContinuousProfilingEngine(ILogger<ContinuousProfilingEngine> logger)
        {
            _logger = logger;
        }

        #region Profile Ingestion

        public Task<ProfileData> IngestProfileAsync(
            string tenantId,
            ProfileData profile,
            CancellationToken cancellation = default)
        {
            if (!_profiles.ContainsKey(tenantId))
                _profiles[tenantId] = new();

            profile.TenantId = tenantId;
            profile.CreatedAt = DateTime.UtcNow;

            // Calculate function statistics from stack traces
            if (profile.StackTraces.Any() && !profile.FunctionStats.Any())
            {
                profile.FunctionStats = CalculateFunctionStats(profile.StackTraces);
            }

            _profiles[tenantId].Add(profile);

            _logger.LogInformation(
                "Ingested {Type} profile for service {Service}: {SampleCount} samples",
                profile.Type, profile.ServiceName, profile.SampleCount);

            return Task.FromResult(profile);
        }

        public async Task<ProfileData> IngestPprofAsync(
            string tenantId,
            string serviceId,
            ProfileType type,
            byte[] pprofData,
            Dictionary<string, string> labels,
            CancellationToken cancellation = default)
        {
            // Parse pprof data (simplified - real implementation would use pprof library)
            var profile = new ProfileData
            {
                ServiceId = serviceId,
                Type = type,
                Format = ProfileFormat.Pprof,
                Labels = labels,
                RawData = Convert.ToBase64String(pprofData),
                StartTime = DateTime.UtcNow.AddSeconds(-10),
                EndTime = DateTime.UtcNow,
                SampleCount = pprofData.Length / 100 // Approximate
            };

            // Generate simulated stack traces
            profile.StackTraces = GenerateSimulatedStackTraces(type, 100);
            profile.FunctionStats = CalculateFunctionStats(profile.StackTraces);

            return await IngestProfileAsync(tenantId, profile, cancellation);
        }

        private List<StackTrace> GenerateSimulatedStackTraces(ProfileType type, int count)
        {
            var traces = new List<StackTrace>();
            var functions = type switch
            {
                ProfileType.CPU => new[] { "main", "processRequest", "handleHTTP", "parseJSON", "validateInput", "executeQuery", "serializeResponse" },
                ProfileType.Memory => new[] { "allocateBuffer", "createObject", "cloneData", "appendSlice", "growArray", "newConnection" },
                ProfileType.Mutex => new[] { "Lock", "Unlock", "RLock", "RUnlock", "TryLock", "WaitGroup.Wait" },
                _ => new[] { "function1", "function2", "function3", "function4", "function5" }
            };

            for (int i = 0; i < count; i++)
            {
                var depth = _random.Next(3, 8);
                var frames = new List<StackFrame>();

                for (int j = 0; j < depth; j++)
                {
                    frames.Add(new StackFrame
                    {
                        Function = functions[_random.Next(functions.Length)],
                        Module = "main",
                        File = "main.go",
                        Line = _random.Next(1, 500)
                    });
                }

                traces.Add(new StackTrace
                {
                    Frames = frames,
                    Value = _random.Next(1000, 100000),
                    Count = _random.Next(1, 100)
                });
            }

            return traces;
        }

        private List<FunctionStats> CalculateFunctionStats(List<StackTrace> traces)
        {
            var stats = new Dictionary<string, FunctionStats>();
            var totalValue = traces.Sum(t => t.Value);

            foreach (var trace in traces)
            {
                // Flat (self) time - only top frame
                if (trace.Frames.Any())
                {
                    var topFrame = trace.Frames.First();
                    var key = $"{topFrame.Module}.{topFrame.Function}";

                    if (!stats.ContainsKey(key))
                    {
                        stats[key] = new FunctionStats
                        {
                            Function = topFrame.Function,
                            Module = topFrame.Module,
                            File = topFrame.File
                        };
                    }

                    stats[key].Flat += trace.Value;
                    stats[key].SampleCount += trace.Count;
                }

                // Cumulative time - all frames
                foreach (var frame in trace.Frames)
                {
                    var key = $"{frame.Module}.{frame.Function}";

                    if (!stats.ContainsKey(key))
                    {
                        stats[key] = new FunctionStats
                        {
                            Function = frame.Function,
                            Module = frame.Module,
                            File = frame.File
                        };
                    }

                    stats[key].Cumulative += trace.Value;
                }
            }

            // Calculate percentages
            foreach (var stat in stats.Values)
            {
                stat.FlatPercent = totalValue > 0 ? (double)stat.Flat / totalValue * 100 : 0;
                stat.CumulativePercent = totalValue > 0 ? (double)stat.Cumulative / totalValue * 100 : 0;
            }

            return stats.Values
                .OrderByDescending(s => s.Flat)
                .ToList();
        }

        #endregion

        #region Profile Query

        public async Task<ProfileQueryResult> QueryProfilesAsync(
            string tenantId,
            ProfileQuery query,
            CancellationToken cancellation = default)
        {
            if (!_profiles.TryGetValue(tenantId, out var profiles))
                return new ProfileQueryResult();

            var filtered = profiles.AsEnumerable();

            if (!string.IsNullOrEmpty(query.ServiceId))
                filtered = filtered.Where(p => p.ServiceId == query.ServiceId);

            if (query.Type.HasValue)
                filtered = filtered.Where(p => p.Type == query.Type.Value);

            if (query.StartTime.HasValue)
                filtered = filtered.Where(p => p.StartTime >= query.StartTime.Value);

            if (query.EndTime.HasValue)
                filtered = filtered.Where(p => p.EndTime <= query.EndTime.Value);

            foreach (var selector in query.LabelSelectors)
            {
                filtered = filtered.Where(p =>
                    p.Labels.TryGetValue(selector.Key, out var value) && value == selector.Value);
            }

            var results = filtered.Take(query.MaxResults).ToList();

            var result = new ProfileQueryResult
            {
                Profiles = results
            };

            // Merge profiles if requested
            if (query.Aggregation.MergeProfiles && results.Any())
            {
                result.MergedProfile = MergeProfiles(results);
                result.TopFunctions = result.MergedProfile.FunctionStats.Take(50).ToList();
            }

            // Generate flamegraph
            if (results.Any())
            {
                result.Flamegraph = await GenerateFlamegraphAsync(tenantId, query, cancellation);
            }

            return result;
        }

        private ProfileData MergeProfiles(List<ProfileData> profiles)
        {
            var merged = new ProfileData
            {
                Type = profiles.First().Type,
                ServiceId = profiles.First().ServiceId,
                ServiceName = profiles.First().ServiceName,
                StartTime = profiles.Min(p => p.StartTime),
                EndTime = profiles.Max(p => p.EndTime),
                SampleCount = profiles.Sum(p => p.SampleCount)
            };

            // Merge stack traces
            var allTraces = profiles.SelectMany(p => p.StackTraces).ToList();
            merged.StackTraces = allTraces;
            merged.FunctionStats = CalculateFunctionStats(allTraces);

            return merged;
        }

        public Task<ProfileData?> GetProfileAsync(
            string tenantId,
            string profileId,
            CancellationToken cancellation = default)
        {
            if (_profiles.TryGetValue(tenantId, out var profiles))
            {
                var profile = profiles.FirstOrDefault(p => p.Id == profileId);
                return Task.FromResult<ProfileData?>(profile);
            }

            return Task.FromResult<ProfileData?>(null);
        }

        public Task<FlamegraphData> GenerateFlamegraphAsync(
            string tenantId,
            ProfileQuery query,
            CancellationToken cancellation = default)
        {
            var flamegraph = new FlamegraphData
            {
                Type = query.Type ?? ProfileType.CPU,
                Unit = query.Type == ProfileType.Memory ? "bytes" : "nanoseconds"
            };

            // Build flamegraph tree from stack traces
            if (_profiles.TryGetValue(tenantId, out var profiles))
            {
                var filtered = profiles.Where(p => query.ServiceId == null || p.ServiceId == query.ServiceId);
                if (query.Type.HasValue)
                    filtered = filtered.Where(p => p.Type == query.Type.Value);

                var allTraces = filtered.SelectMany(p => p.StackTraces).ToList();
                flamegraph.TotalSamples = allTraces.Sum(t => t.Count);
                flamegraph.TotalValue = allTraces.Sum(t => t.Value);

                flamegraph.Root = BuildFlamegraphTree(allTraces);
            }

            return Task.FromResult(flamegraph);
        }

        private FlamegraphNode BuildFlamegraphTree(List<StackTrace> traces)
        {
            var root = new FlamegraphNode { Name = "root" };

            foreach (var trace in traces)
            {
                var current = root;

                // Walk frames from bottom to top (callers first)
                foreach (var frame in trace.Frames.AsEnumerable().Reverse())
                {
                    var child = current.Children.FirstOrDefault(c => c.Name == frame.Function);
                    if (child == null)
                    {
                        child = new FlamegraphNode
                        {
                            Name = frame.Function,
                            Module = frame.Module,
                            File = frame.File,
                            Line = frame.Line
                        };
                        current.Children.Add(child);
                    }

                    child.Total += trace.Value;
                    current = child;
                }

                // Self time on leaf
                current.Self += trace.Value;
            }

            // Calculate root totals
            root.Total = traces.Sum(t => t.Value);

            return root;
        }

        public async Task<List<FunctionStats>> GetTopFunctionsAsync(
            string tenantId,
            ProfileQuery query,
            int limit = 50,
            CancellationToken cancellation = default)
        {
            var result = await QueryProfilesAsync(tenantId, query, cancellation);

            if (result.MergedProfile != null)
            {
                return result.MergedProfile.FunctionStats.Take(limit).ToList();
            }

            return result.TopFunctions.Take(limit).ToList();
        }

        #endregion

        #region Profile Comparison

        public async Task<ProfileComparison> CompareProfilesAsync(
            string tenantId,
            string baseProfileId,
            string compareProfileId,
            ComparisonMode mode = ComparisonMode.Diff,
            CancellationToken cancellation = default)
        {
            var baseProfile = await GetProfileAsync(tenantId, baseProfileId, cancellation);
            var compareProfile = await GetProfileAsync(tenantId, compareProfileId, cancellation);

            if (baseProfile == null || compareProfile == null)
                throw new InvalidOperationException("One or both profiles not found");

            var comparison = new ProfileComparison
            {
                BaseProfileId = baseProfileId,
                CompareProfileId = compareProfileId,
                Mode = mode
            };

            // Build function maps
            var baseFuncs = baseProfile.FunctionStats.ToDictionary(f => $"{f.Module}.{f.Function}");
            var compareFuncs = compareProfile.FunctionStats.ToDictionary(f => $"{f.Module}.{f.Function}");

            var allFuncs = baseFuncs.Keys.Union(compareFuncs.Keys);

            foreach (var func in allFuncs)
            {
                var baseValue = baseFuncs.TryGetValue(func, out var bf) ? bf.Flat : 0;
                var compareValue = compareFuncs.TryGetValue(func, out var cf) ? cf.Flat : 0;

                var diff = new FunctionDiff
                {
                    Function = func.Split('.').Last(),
                    Module = func.Split('.').First(),
                    BaseValue = baseValue,
                    CompareValue = compareValue,
                    Difference = compareValue - baseValue,
                    PercentChange = baseValue > 0 ? (double)(compareValue - baseValue) / baseValue * 100 : 0,
                    IsRegression = compareValue > baseValue * 1.1 // 10% threshold
                };

                comparison.FunctionDiffs.Add(diff);
            }

            // Calculate summary
            comparison.Summary = new ComparisonSummary
            {
                BaseTotalValue = baseProfile.FunctionStats.Sum(f => f.Flat),
                CompareTotalValue = compareProfile.FunctionStats.Sum(f => f.Flat),
                ImprovedFunctions = comparison.FunctionDiffs.Count(d => d.Difference < 0),
                RegressedFunctions = comparison.FunctionDiffs.Count(d => d.IsRegression),
                NewFunctions = comparison.FunctionDiffs.Count(d => d.BaseValue == 0 && d.CompareValue > 0),
                RemovedFunctions = comparison.FunctionDiffs.Count(d => d.BaseValue > 0 && d.CompareValue == 0)
            };
            comparison.Summary.OverallChange = comparison.Summary.BaseTotalValue > 0
                ? (double)(comparison.Summary.CompareTotalValue - comparison.Summary.BaseTotalValue) / comparison.Summary.BaseTotalValue * 100
                : 0;

            // Detect regressions
            comparison.Regressions = comparison.FunctionDiffs
                .Where(d => d.IsRegression && d.PercentChange > 20)
                .Select(d => new PerformanceRegression
                {
                    Function = d.Function,
                    Severity = d.PercentChange > 50 ? "high" : d.PercentChange > 30 ? "medium" : "low",
                    ImpactPercent = d.PercentChange,
                    ImpactValue = d.Difference,
                    Description = $"{d.Function} increased by {d.PercentChange:F1}%",
                    PossibleCauses = GeneratePossibleCauses(d)
                })
                .ToList();

            return comparison;
        }

        private List<string> GeneratePossibleCauses(FunctionDiff diff)
        {
            return new List<string>
            {
                "Algorithm change in recent deployment",
                "Increased input size or complexity",
                "New code path being executed",
                "Cache miss rate increase",
                "Resource contention with other services"
            };
        }

        public Task<ProfileComparison> CompareToBaselineAsync(
            string tenantId,
            string profileId,
            string baselineId,
            CancellationToken cancellation = default)
        {
            return CompareProfilesAsync(tenantId, baselineId, profileId, ComparisonMode.Baseline, cancellation);
        }

        #endregion

        #region Analysis

        public async Task<ProfileAnalysis> AnalyzeProfileAsync(
            string tenantId,
            string profileId,
            CancellationToken cancellation = default)
        {
            var profile = await GetProfileAsync(tenantId, profileId, cancellation);
            if (profile == null)
                throw new InvalidOperationException($"Profile {profileId} not found");

            var analysis = new ProfileAnalysis
            {
                ProfileId = profileId
            };

            // Identify hot spots
            var topFunctions = profile.FunctionStats.Take(10).ToList();
            analysis.HotSpots = topFunctions.Select(f => new HotSpot
            {
                Function = f.Function,
                Module = f.Module,
                File = f.File,
                Percentage = f.FlatPercent,
                Value = f.Flat,
                Type = profile.Type.ToString().ToLower()
            }).ToList();

            // Generate recommendations
            analysis.Recommendations = GenerateRecommendations(profile, analysis.HotSpots);

            // Memory leak detection for memory profiles
            if (profile.Type == ProfileType.Memory || profile.Type == ProfileType.Heap)
            {
                analysis.MemoryLeaks = await DetectMemoryLeaksAsync(
                    tenantId, profile.ServiceId, TimeSpan.FromHours(1), cancellation);
            }

            // Summary
            analysis.Summary = new AnalysisSummary
            {
                HotSpotCount = analysis.HotSpots.Count,
                PotentialLeakCount = analysis.MemoryLeaks.Count,
                ContentionIssueCount = analysis.ContentionIssues.Count,
                RecommendationCount = analysis.Recommendations.Count,
                OverallHealth = DetermineOverallHealth(analysis),
                EfficiencyScore = CalculateEfficiencyScore(profile)
            };

            return analysis;
        }

        private List<OptimizationHint> GenerateRecommendations(ProfileData profile, List<HotSpot> hotSpots)
        {
            var recommendations = new List<OptimizationHint>();

            foreach (var hotSpot in hotSpots.Where(h => h.Percentage > 10))
            {
                recommendations.Add(new OptimizationHint
                {
                    Title = $"Optimize {hotSpot.Function}",
                    Description = $"Function consumes {hotSpot.Percentage:F1}% of {profile.Type} resources",
                    Category = profile.Type.ToString().ToLower(),
                    Priority = hotSpot.Percentage > 20 ? "high" : "medium",
                    AffectedFunction = hotSpot.Function,
                    SuggestedActions = GenerateSuggestedActions(profile.Type, hotSpot),
                    PotentialImprovement = hotSpot.Percentage * 0.3 // Estimate 30% improvement
                });
            }

            return recommendations;
        }

        private List<string> GenerateSuggestedActions(ProfileType type, HotSpot hotSpot)
        {
            return type switch
            {
                ProfileType.CPU => new List<string>
                {
                    "Profile the function with a line-level profiler",
                    "Consider algorithmic improvements",
                    "Check for unnecessary computations in loops",
                    "Consider caching computed results",
                    "Evaluate if work can be parallelized"
                },
                ProfileType.Memory or ProfileType.Heap => new List<string>
                {
                    "Review allocation patterns",
                    "Consider object pooling",
                    "Check for string concatenation in loops",
                    "Use value types where appropriate",
                    "Evaluate buffer reuse opportunities"
                },
                ProfileType.Mutex or ProfileType.Lock => new List<string>
                {
                    "Review lock scope and duration",
                    "Consider lock-free alternatives",
                    "Evaluate read-write lock usage",
                    "Check for nested lock acquisitions",
                    "Consider partitioned locking"
                },
                _ => new List<string>
                {
                    "Review function implementation",
                    "Consider optimization opportunities"
                }
            };
        }

        private string DetermineOverallHealth(ProfileAnalysis analysis)
        {
            if (analysis.MemoryLeaks.Any(l => l.Confidence > 0.8) ||
                analysis.HotSpots.Any(h => h.Percentage > 50))
            {
                return "critical";
            }

            if (analysis.MemoryLeaks.Any() ||
                analysis.ContentionIssues.Any() ||
                analysis.HotSpots.Any(h => h.Percentage > 25))
            {
                return "warning";
            }

            return "good";
        }

        private double CalculateEfficiencyScore(ProfileData profile)
        {
            // Higher score = more distributed work (better)
            var topFunctionPercent = profile.FunctionStats.FirstOrDefault()?.FlatPercent ?? 0;
            return Math.Max(0, 100 - topFunctionPercent);
        }

        public Task<List<MemoryLeakCandidate>> DetectMemoryLeaksAsync(
            string tenantId,
            string serviceId,
            TimeSpan window,
            CancellationToken cancellation = default)
        {
            // Simplified leak detection
            // Real implementation would analyze allocation trends over time
            var candidates = new List<MemoryLeakCandidate>();

            if (_profiles.TryGetValue(tenantId, out var profiles))
            {
                var memoryProfiles = profiles
                    .Where(p => p.ServiceId == serviceId &&
                               (p.Type == ProfileType.Memory || p.Type == ProfileType.Heap) &&
                               p.StartTime >= DateTime.UtcNow - window)
                    .OrderBy(p => p.StartTime)
                    .ToList();

                if (memoryProfiles.Count >= 2)
                {
                    var first = memoryProfiles.First();
                    var last = memoryProfiles.Last();

                    // Compare allocation patterns
                    var firstAllocs = first.FunctionStats.ToDictionary(f => f.Function, f => f.Flat);
                    var lastAllocs = last.FunctionStats.ToDictionary(f => f.Function, f => f.Flat);

                    foreach (var func in lastAllocs.Keys)
                    {
                        if (firstAllocs.TryGetValue(func, out var firstValue))
                        {
                            var growth = lastAllocs[func] - firstValue;
                            var elapsed = (last.StartTime - first.StartTime).TotalSeconds;
                            var growthRate = elapsed > 0 ? growth / elapsed : 0;

                            // Flag if growing consistently
                            if (growthRate > 1000) // 1KB/sec threshold
                            {
                                candidates.Add(new MemoryLeakCandidate
                                {
                                    Function = func,
                                    AllocationType = "heap",
                                    GrowthRate = growthRate,
                                    TotalAllocated = lastAllocs[func],
                                    Confidence = Math.Min(0.95, growthRate / 10000)
                                });
                            }
                        }
                    }
                }
            }

            return Task.FromResult(candidates.OrderByDescending(c => c.Confidence).Take(10).ToList());
        }

        public async Task<List<PerformanceRegression>> DetectRegressionsAsync(
            string tenantId,
            string serviceId,
            TimeSpan window,
            CancellationToken cancellation = default)
        {
            var regressions = new List<PerformanceRegression>();

            if (!_profiles.TryGetValue(tenantId, out var profiles))
                return regressions;

            var serviceProfiles = profiles
                .Where(p => p.ServiceId == serviceId && p.StartTime >= DateTime.UtcNow - window)
                .OrderBy(p => p.StartTime)
                .ToList();

            if (serviceProfiles.Count < 2)
                return regressions;

            // Compare recent vs older profiles
            var midpoint = serviceProfiles.Count / 2;
            var olderProfiles = serviceProfiles.Take(midpoint).ToList();
            var newerProfiles = serviceProfiles.Skip(midpoint).ToList();

            var olderMerged = MergeProfiles(olderProfiles);
            var newerMerged = MergeProfiles(newerProfiles);

            var comparison = await CompareProfilesAsync(
                tenantId, olderMerged.Id, newerMerged.Id,
                ComparisonMode.Diff, cancellation);

            return comparison.Regressions;
        }

        #endregion

        #region Targets & Agents

        public Task<ProfilingTarget> RegisterTargetAsync(
            string tenantId,
            ProfilingTarget target,
            CancellationToken cancellation = default)
        {
            if (!_targets.ContainsKey(tenantId))
                _targets[tenantId] = new();

            target.TenantId = tenantId;
            target.CreatedAt = DateTime.UtcNow;

            // Set default enabled profiles if not specified
            if (!target.EnabledProfiles.Any())
            {
                target.EnabledProfiles = target.Target switch
                {
                    ProfileTarget.Go => new List<ProfileType> { ProfileType.CPU, ProfileType.Memory, ProfileType.Goroutine, ProfileType.Mutex },
                    ProfileTarget.Java => new List<ProfileType> { ProfileType.CPU, ProfileType.Memory, ProfileType.Lock },
                    ProfileTarget.DotNet => new List<ProfileType> { ProfileType.CPU, ProfileType.Memory, ProfileType.Exceptions },
                    ProfileTarget.Python => new List<ProfileType> { ProfileType.CPU, ProfileType.Memory },
                    ProfileTarget.NodeJS => new List<ProfileType> { ProfileType.CPU, ProfileType.Memory },
                    _ => new List<ProfileType> { ProfileType.CPU, ProfileType.Memory }
                };
            }

            _targets[tenantId][target.Id] = target;

            _logger.LogInformation(
                "Registered profiling target {Id} for service {Service}",
                target.Id, target.ServiceName);

            return Task.FromResult(target);
        }

        public Task<List<ProfilingTarget>> ListTargetsAsync(
            string tenantId,
            CancellationToken cancellation = default)
        {
            if (!_targets.TryGetValue(tenantId, out var targets))
                return Task.FromResult(new List<ProfilingTarget>());

            return Task.FromResult(targets.Values.ToList());
        }

        public Task<string> GenerateAgentConfigAsync(
            string tenantId,
            string targetId,
            CancellationToken cancellation = default)
        {
            if (!_targets.TryGetValue(tenantId, out var targets) ||
                !targets.TryGetValue(targetId, out var target))
            {
                throw new InvalidOperationException($"Target {targetId} not found");
            }

            var config = target.AgentConfig.AgentType switch
            {
                "parca-agent" => GenerateParcaAgentConfig(target),
                "pyroscope-agent" => GeneratePyroscopeAgentConfig(target),
                _ => GenerateGenericAgentConfig(target)
            };

            return Task.FromResult(config);
        }

        private string GenerateParcaAgentConfig(ProfilingTarget target)
        {
            var profileTypes = string.Join(",", target.EnabledProfiles.Select(p => p.ToString().ToLower()));

            return $@"# Parca Agent Configuration
# Generated for: {target.ServiceName}

node: {{target.Metadata.PodName}}
http-address: :7071
profiling-duration: {target.UploadInterval.TotalSeconds}s
profiling-cpu-sampling-frequency: {target.SamplingRate}

# Remote write configuration
remote-store-address: parca-server:7070
remote-store-bearer-token: $PARCA_TOKEN
remote-store-insecure: false

# Kubernetes discovery
kubernetes:
  enabled: true
  namespace: {target.LabelSelectors.GetValueOrDefault("namespace", "default")}

# Object storage for profiles
object-storage:
  bucket:
    type: S3

# Debuginfo configuration
debuginfo:
  strip: true
  upload:
    enabled: true

# Labels
external-labels:
  service: {target.ServiceId}
  tenant: {target.TenantId}
  environment: production
";
        }

        private string GeneratePyroscopeAgentConfig(ProfilingTarget target)
        {
            return $@"# Pyroscope Agent Configuration
# Generated for: {target.ServiceName}

server-address: http://pyroscope-server:4040

application-name: {target.ServiceName}

# Sampling configuration
sample-rate: {target.SamplingRate}
upload-rate: {target.UploadInterval.TotalSeconds}s

# Profile types
profiling-enabled:
  cpu: {target.EnabledProfiles.Contains(ProfileType.CPU).ToString().ToLower()}
  alloc_objects: {target.EnabledProfiles.Contains(ProfileType.Memory).ToString().ToLower()}
  alloc_space: {target.EnabledProfiles.Contains(ProfileType.Heap).ToString().ToLower()}
  goroutine: {target.EnabledProfiles.Contains(ProfileType.Goroutine).ToString().ToLower()}
  mutex_count: {target.EnabledProfiles.Contains(ProfileType.Mutex).ToString().ToLower()}
  mutex_duration: {target.EnabledProfiles.Contains(ProfileType.Mutex).ToString().ToLower()}
  block_count: {target.EnabledProfiles.Contains(ProfileType.Block).ToString().ToLower()}
  block_duration: {target.EnabledProfiles.Contains(ProfileType.Block).ToString().ToLower()}

# Labels
tags:
  service: {target.ServiceId}
  tenant: {target.TenantId}
  runtime: {target.Target.ToString().ToLower()}
";
        }

        private string GenerateGenericAgentConfig(ProfilingTarget target)
        {
            return $@"# Generic Profiling Agent Configuration
# Generated for: {target.ServiceName}

service_name: {target.ServiceName}
service_id: {target.ServiceId}
tenant_id: {target.TenantId}
sampling_rate: {target.SamplingRate}
upload_interval: {target.UploadInterval.TotalSeconds}s
enabled_profiles:
{string.Join("\n", target.EnabledProfiles.Select(p => $"  - {p.ToString().ToLower()}"))}
";
        }

        #endregion

        #region Alerting

        public Task<ProfileAlertConfig> CreateAlertConfigAsync(
            string tenantId,
            ProfileAlertConfig config,
            CancellationToken cancellation = default)
        {
            if (!_alertConfigs.ContainsKey(tenantId))
                _alertConfigs[tenantId] = new();

            config.TenantId = tenantId;
            config.CreatedAt = DateTime.UtcNow;

            _alertConfigs[tenantId][config.Id] = config;

            _logger.LogInformation(
                "Created profile alert config {Id} '{Name}'",
                config.Id, config.Name);

            return Task.FromResult(config);
        }

        public async Task<List<ProfileAlert>> CheckAlertsAsync(
            string tenantId,
            CancellationToken cancellation = default)
        {
            var alerts = new List<ProfileAlert>();

            if (!_alertConfigs.TryGetValue(tenantId, out var configs))
                return alerts;

            foreach (var config in configs.Values.Where(c => c.IsActive))
            {
                var query = new ProfileQuery
                {
                    ServiceId = config.ServiceId,
                    StartTime = DateTime.UtcNow - config.Threshold.Duration,
                    EndTime = DateTime.UtcNow
                };

                var result = await QueryProfilesAsync(tenantId, query, cancellation);

                if (result.MergedProfile != null)
                {
                    var alert = EvaluateAlertCondition(config, result.MergedProfile);
                    if (alert != null)
                    {
                        alerts.Add(alert);

                        if (!_alerts.ContainsKey(tenantId))
                            _alerts[tenantId] = new();
                        _alerts[tenantId].Add(alert);
                    }
                }
            }

            return alerts;
        }

        private ProfileAlert? EvaluateAlertCondition(ProfileAlertConfig config, ProfileData profile)
        {
            double currentValue = config.Condition switch
            {
                ProfileAlertCondition.CPUSpike => profile.FunctionStats.Sum(f => f.FlatPercent),
                ProfileAlertCondition.MemoryLeak => profile.FunctionStats.Sum(f => f.Flat) / 1024.0 / 1024.0, // MB
                ProfileAlertCondition.ContentionHigh => profile.FunctionStats.Where(f => f.Function.Contains("Lock")).Sum(f => f.FlatPercent),
                ProfileAlertCondition.SlowFunction => config.TargetFunction != null
                    ? profile.FunctionStats.FirstOrDefault(f => f.Function == config.TargetFunction)?.FlatPercent ?? 0
                    : 0,
                _ => 0
            };

            bool shouldAlert = config.Threshold.Operator switch
            {
                "gt" => currentValue > config.Threshold.Value,
                "gte" => currentValue >= config.Threshold.Value,
                "lt" => currentValue < config.Threshold.Value,
                "lte" => currentValue <= config.Threshold.Value,
                "eq" => Math.Abs(currentValue - config.Threshold.Value) < 0.001,
                _ => false
            };

            if (shouldAlert)
            {
                return new ProfileAlert
                {
                    ConfigId = config.Id,
                    AlertName = config.Name,
                    Condition = config.Condition,
                    ServiceId = profile.ServiceId,
                    ServiceName = profile.ServiceName,
                    Function = config.TargetFunction,
                    CurrentValue = currentValue,
                    ThresholdValue = config.Threshold.Value,
                    Message = $"Alert '{config.Name}': {config.Condition} detected. Current: {currentValue:F2}, Threshold: {config.Threshold.Value}",
                    RelatedProfiles = new List<string> { profile.Id }
                };
            }

            return null;
        }

        public Task<List<ProfileAlert>> GetActiveAlertsAsync(
            string tenantId,
            CancellationToken cancellation = default)
        {
            if (!_alerts.TryGetValue(tenantId, out var alerts))
                return Task.FromResult(new List<ProfileAlert>());

            var active = alerts.Where(a => a.Status == "firing").ToList();
            return Task.FromResult(active);
        }

        #endregion
    }

    #endregion
}
