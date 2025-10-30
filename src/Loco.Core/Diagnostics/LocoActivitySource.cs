using System.Diagnostics;

namespace Loco.Core.Diagnostics;

/// <summary>
/// Central ActivitySource for Loco distributed tracing.
/// Used for OpenTelemetry instrumentation across the platform.
/// </summary>
public static class LocoActivitySource
{
    /// <summary>
    /// ActivitySource name for Loco platform
    /// </summary>
    public const string SourceName = "Loco.Platform";

    /// <summary>
    /// ActivitySource version
    /// </summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// Shared ActivitySource instance for all tracing operations
    /// </summary>
    public static readonly ActivitySource Instance = new(SourceName, Version);

    // Activity names for major operations
    public const string ActivityEngineStart = "loco.engine.start";
    public const string ActivityEngineStop = "loco.engine.stop";
    public const string ActivityFlowExecution = "loco.flow.execution";
    public const string ActivityRuleExecution = "loco.rule.execution";
    public const string ActivityRulePersistence = "loco.rule.persistence";
    public const string ActivityStateLoad = "loco.state.load";
    public const string ActivityStateSave = "loco.state.save";
    public const string ActivityWorkflowExecution = "loco.workflow.execution";
    public const string ActivityBackupOperation = "loco.backup.operation";
    public const string ActivityConfigValidation = "loco.config.validation";

    // Tags for activity attributes
    public static class Tags
    {
        public const string RuleId = "loco.rule.id";
        public const string RuleName = "loco.rule.name";
        public const string FlowId = "loco.flow.id";
        public const string FlowName = "loco.flow.name";
        public const string ExecutionId = "loco.execution.id";
        public const string WorkflowId = "loco.workflow.id";
        public const string StepId = "loco.step.id";
        public const string StepName = "loco.step.name";
        public const string ExecutionStatus = "loco.execution.status";
        public const string ErrorCode = "loco.error.code";
        public const string CorrelationId = "loco.correlation.id";
        public const string UserId = "loco.user.id";
        public const string TenantId = "loco.tenant.id";
        public const string Duration = "loco.duration_ms";
    }
}
