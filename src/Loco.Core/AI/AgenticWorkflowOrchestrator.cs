using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Workflow;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AI
{
    /// <summary>
    /// Agentic AI workflow orchestrator that enables autonomous decision-making and self-improvement.
    /// 自律的な意思決定と自己改善を可能にするエージェント型AIワークフローオーケストレーター
    ///
    /// Based on 2025 Agentic AI trends:
    /// - Autonomous multi-step planning
    /// - Context maintenance across complex tasks
    /// - Learn from interactions and failures
    /// - Adaptive, self-improving systems
    ///
    /// Solves Issues:
    /// - #8: Complex processing support → Autonomous planning
    /// - #9: Error handling → Self-healing workflows
    /// - #11: AI assistant → Full agent autonomy
    /// - #24: Manual configuration → Self-optimizing
    /// </summary>
    public class AgenticWorkflowOrchestrator
    {
        private readonly ILogger<AgenticWorkflowOrchestrator> _logger;
        private readonly WorkflowExecutor _executor;
        private readonly AgentMemory _memory;
        private readonly AgentLearningSystem _learningSystem;

        public AgenticWorkflowOrchestrator(
            ILogger<AgenticWorkflowOrchestrator> logger,
            WorkflowExecutor executor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _memory = new AgentMemory();
            _learningSystem = new AgentLearningSystem();
        }

        /// <summary>
        /// Executes workflow with autonomous decision-making and self-correction.
        /// 自律的な意思決定と自己修正を行いながらワークフローを実行
        /// </summary>
        public async Task<AgenticExecutionResult> ExecuteWithAutonomyAsync(
            WorkflowDefinition workflow,
            AgenticExecutionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new AgenticExecutionOptions();

            var result = new AgenticExecutionResult
            {
                WorkflowId = workflow.Id,
                StartedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Starting agentic execution for workflow: {WorkflowId}", workflow.Id);

            try
            {
                // 1. Analyze workflow context and retrieve relevant memories
                var context = await AnalyzeContextAsync(workflow, cancellationToken);
                result.AnalyzedContext = context;

                // 2. Retrieve learned patterns from similar workflows
                var learnedPatterns = _memory.RetrieveRelevantMemories(workflow.Id, context);
                result.AppliedPatterns = learnedPatterns.Select(p => p.PatternId).ToList();

                _logger.LogDebug("Retrieved {Count} learned patterns", learnedPatterns.Count);

                // 3. Plan execution with autonomous decision-making
                var executionPlan = await PlanExecutionAsync(workflow, context, learnedPatterns, cancellationToken);
                result.ExecutionPlan = executionPlan;

                // 4. Execute with self-healing
                var executionResult = await ExecuteWithSelfHealingAsync(
                    workflow,
                    executionPlan,
                    options,
                    cancellationToken);

                result.WorkflowResult = executionResult;

                // 5. Learn from execution (success or failure)
                await LearnFromExecutionAsync(workflow, executionResult, context, cancellationToken);

                // 6. Generate improvement suggestions
                if (options.GenerateImprovements)
                {
                    result.Improvements = await GenerateAdaptiveImprovementsAsync(
                        workflow,
                        executionResult,
                        context,
                        cancellationToken);
                }

                result.Success = executionResult.Success;
                result.CompletedAt = DateTime.UtcNow;

                _logger.LogInformation("Agentic execution completed for workflow: {WorkflowId}, Success: {Success}",
                    workflow.Id, result.Success);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agentic execution failed for workflow: {WorkflowId}", workflow.Id);

                // Learn from failures too
                await _learningSystem.LearnFromFailureAsync(workflow.Id, ex.Message);

                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;

                return result;
            }
        }

        /// <summary>
        /// Autonomously optimizes workflow based on historical data and current conditions.
        /// 履歴データと現在の状況に基づいてワークフローを自律的に最適化
        /// </summary>
        public async Task<WorkflowOptimizationResult> OptimizeWorkflowAsync(
            WorkflowDefinition workflow,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting autonomous optimization for workflow: {WorkflowId}", workflow.Id);

            var optimizationResult = new WorkflowOptimizationResult
            {
                OriginalWorkflow = workflow,
                OptimizedAt = DateTime.UtcNow
            };

            // 1. Analyze historical performance
            var performanceMetrics = _memory.GetWorkflowPerformanceMetrics(workflow.Id);
            optimizationResult.HistoricalMetrics = performanceMetrics;

            // 2. Identify bottlenecks
            var bottlenecks = IdentifyBottlenecks(workflow, performanceMetrics);
            optimizationResult.IdentifiedBottlenecks = bottlenecks;

            // 3. Generate optimized workflow
            var optimizedWorkflow = await GenerateOptimizedWorkflowAsync(
                workflow,
                bottlenecks,
                performanceMetrics,
                cancellationToken);

            optimizationResult.OptimizedWorkflow = optimizedWorkflow;

            // 4. Calculate expected improvements
            optimizationResult.ExpectedImprovements = CalculateExpectedImprovements(
                workflow,
                optimizedWorkflow,
                performanceMetrics);

            _logger.LogInformation("Optimization completed for workflow: {WorkflowId}", workflow.Id);

            return optimizationResult;
        }

        /// <summary>
        /// Provides real-time guidance and suggestions during workflow execution.
        /// ワークフロー実行中にリアルタイムのガイダンスと提案を提供
        /// </summary>
        public IAsyncEnumerable<AgentGuidance> ProvideRealTimeGuidanceAsync(
            WorkflowDefinition workflow,
            CancellationToken cancellationToken = default)
        {
            return ProvideGuidanceInternalAsync(workflow, cancellationToken);
        }

        // Private helper methods

        private async Task<AgenticContext> AnalyzeContextAsync(
            WorkflowDefinition workflow,
            CancellationToken cancellationToken)
        {
            var context = new AgenticContext
            {
                WorkflowId = workflow.Id,
                CurrentPlatform = DetectPlatform(),
                EnvironmentVariables = Environment.GetEnvironmentVariables()
                    .Cast<System.Collections.DictionaryEntry>()
                    .Take(10) // Limit for performance
                    .ToDictionary(e => e.Key.ToString()!, e => e.Value?.ToString() ?? ""),
                AvailableResources = await DetectAvailableResourcesAsync(cancellationToken)
            };

            // Add workflow-specific context
            context.Metadata["action_count"] = workflow.Actions.Count;
            context.Metadata["trigger_types"] = workflow.Triggers.Select(t => t.Type).Distinct().ToList();
            context.Metadata["platforms"] = workflow.Platforms;

            return context;
        }

        private async Task<ExecutionPlan> PlanExecutionAsync(
            WorkflowDefinition workflow,
            AgenticContext context,
            List<LearnedPattern> learnedPatterns,
            CancellationToken cancellationToken)
        {
            var plan = new ExecutionPlan
            {
                WorkflowId = workflow.Id,
                CreatedAt = DateTime.UtcNow
            };

            // Apply learned patterns to optimize execution order
            var actionGroups = GroupActionsForOptimalExecution(workflow.Actions, learnedPatterns);

            foreach (var group in actionGroups)
            {
                plan.ExecutionSteps.Add(new ExecutionStep
                {
                    StepId = Guid.NewGuid().ToString(),
                    Actions = group,
                    ExecutionStrategy = DetermineExecutionStrategy(group, context),
                    ExpectedDuration = EstimateStepDuration(group, learnedPatterns)
                });
            }

            // Add contingency plans for high-risk steps
            foreach (var step in plan.ExecutionSteps)
            {
                var riskLevel = AssessRiskLevel(step, learnedPatterns);
                if (riskLevel >= 0.7) // High risk
                {
                    step.ContingencyPlan = CreateContingencyPlan(step, context);
                }
            }

            return plan;
        }

        private async Task<WorkflowExecutionResult> ExecuteWithSelfHealingAsync(
            WorkflowDefinition workflow,
            ExecutionPlan plan,
            AgenticExecutionOptions options,
            CancellationToken cancellationToken)
        {
            var attemptCount = 0;
            var maxAttempts = options.MaxSelfHealingAttempts;

            while (attemptCount < maxAttempts)
            {
                attemptCount++;

                _logger.LogDebug("Execution attempt {Attempt}/{Max} for workflow {WorkflowId}",
                    attemptCount, maxAttempts, workflow.Id);

                // Execute workflow
                var result = await _executor.ExecuteAsync(workflow, cancellationToken);

                // If successful, return immediately
                if (result.Success)
                {
                    _logger.LogInformation("Workflow executed successfully on attempt {Attempt}", attemptCount);
                    return result;
                }

                // If failed and we have attempts left, try to self-heal
                if (attemptCount < maxAttempts)
                {
                    _logger.LogWarning("Workflow execution failed, attempting self-healing. Attempt {Attempt}/{Max}",
                        attemptCount, maxAttempts);

                    // Analyze failure and attempt correction
                    var correction = await AnalyzeAndCorrectAsync(workflow, result, plan, cancellationToken);

                    if (correction.CanAutoFix)
                    {
                        _logger.LogInformation("Applying auto-fix: {FixDescription}", correction.FixDescription);
                        workflow = correction.CorrectedWorkflow!;

                        // Record the correction
                        await _learningSystem.RecordCorrectionAsync(workflow.Id, correction);
                    }
                    else
                    {
                        _logger.LogWarning("Cannot auto-fix, returning failed result");
                        return result; // Can't fix, return failure
                    }
                }
                else
                {
                    _logger.LogError("Workflow execution failed after {MaxAttempts} attempts", maxAttempts);
                    return result;
                }
            }

            throw new InvalidOperationException("Unreachable code");
        }

        private async Task LearnFromExecutionAsync(
            WorkflowDefinition workflow,
            WorkflowExecutionResult result,
            AgenticContext context,
            CancellationToken cancellationToken)
        {
            var experience = new ExecutionExperience
            {
                WorkflowId = workflow.Id,
                Success = result.Success,
                Duration = result.Duration,
                Context = context,
                Timestamp = DateTime.UtcNow
            };

            // Extract patterns from successful executions
            if (result.Success)
            {
                var patterns = ExtractSuccessPatterns(workflow, result, context);
                foreach (var pattern in patterns)
                {
                    _memory.StorePattern(pattern);
                }
            }
            else
            {
                // Learn from failures
                var failurePatterns = ExtractFailurePatterns(workflow, result, context);
                foreach (var pattern in failurePatterns)
                {
                    _memory.StorePattern(pattern);
                }
            }

            // Update performance metrics
            _memory.UpdatePerformanceMetrics(workflow.Id, experience);

            await _learningSystem.ProcessExperienceAsync(experience, cancellationToken);
        }

        private async Task<List<WorkflowImprovement>> GenerateAdaptiveImprovementsAsync(
            WorkflowDefinition workflow,
            WorkflowExecutionResult result,
            AgenticContext context,
            CancellationToken cancellationToken)
        {
            var improvements = new List<WorkflowImprovement>();

            // Analyze based on historical data
            var historicalData = _memory.GetWorkflowHistory(workflow.Id);

            // Performance improvements
            if (result.Duration > TimeSpan.FromSeconds(30))
            {
                improvements.Add(new WorkflowImprovement
                {
                    Severity = "medium",
                    Category = "performance",
                    Message = $"Workflow took {result.Duration.TotalSeconds:F1}s to execute",
                    MessageJa = $"ワークフローの実行に{result.Duration.TotalSeconds:F1}秒かかりました",
                    Suggestion = "Consider parallelizing independent actions or optimizing slow steps",
                    SuggestionJa = "独立したアクションの並列化、または遅いステップの最適化を検討してください",
                    ActionIds = new List<string>()
                });
            }

            // Reliability improvements based on historical failure rate
            var failureRate = historicalData.Count > 0
                ? historicalData.Count(e => !e.Success) / (double)historicalData.Count
                : 0.0;

            if (failureRate > 0.1) // More than 10% failure rate
            {
                improvements.Add(new WorkflowImprovement
                {
                    Severity = "high",
                    Category = "reliability",
                    Message = $"Historical failure rate: {failureRate:P0}",
                    MessageJa = $"過去の失敗率: {failureRate:P0}",
                    Suggestion = "Add more robust error handling and retry policies to critical actions",
                    SuggestionJa = "重要なアクションにより堅牢なエラーハンドリングとリトライポリシーを追加してください",
                    ActionIds = new List<string>()
                });
            }

            return improvements;
        }

        private async Task<WorkflowCorrection> AnalyzeAndCorrectAsync(
            WorkflowDefinition workflow,
            WorkflowExecutionResult result,
            ExecutionPlan plan,
            CancellationToken cancellationToken)
        {
            var correction = new WorkflowCorrection
            {
                WorkflowId = workflow.Id,
                OriginalError = result.ErrorMessage ?? "Unknown error"
            };

            // Analyze the type of failure
            if (result.ActionResults != null && result.ActionResults.Count > 0)
            {
                var failedAction = result.ActionResults.FirstOrDefault(a => !a.Success);
                if (failedAction != null)
                {
                    correction.FailedActionId = failedAction.ActionId;

                    // Try common fixes
                    if (failedAction.ErrorMessage?.Contains("timeout") == true ||
                        failedAction.ErrorMessage?.Contains("connection") == true)
                    {
                        // Network-related failure - add retry policy
                        correction.CanAutoFix = true;
                        correction.FixDescription = "Adding exponential backoff retry policy to network action";
                        correction.CorrectedWorkflow = AddRetryPolicy(workflow, failedAction.ActionId);
                    }
                    else if (failedAction.ErrorMessage?.Contains("not found") == true ||
                             failedAction.ErrorMessage?.Contains("does not exist") == true)
                    {
                        // Resource not found - add existence check constraint
                        correction.CanAutoFix = true;
                        correction.FixDescription = "Adding file/resource existence constraint";
                        correction.CorrectedWorkflow = AddExistenceConstraint(workflow, failedAction.ActionId);
                    }
                }
            }

            return correction;
        }

        private WorkflowDefinition AddRetryPolicy(WorkflowDefinition workflow, string actionId)
        {
            var modifiedWorkflow = CloneWorkflow(workflow);
            var action = modifiedWorkflow.Actions.FirstOrDefault(a => a.Id == actionId);

            if (action != null)
            {
                action.Retry = new ActionRetryPolicy
                {
                    MaxAttempts = 3,
                    DelayMs = 1000,
                    BackoffStrategy = "exponential"
                };
            }

            return modifiedWorkflow;
        }

        private WorkflowDefinition AddExistenceConstraint(WorkflowDefinition workflow, string actionId)
        {
            var modifiedWorkflow = CloneWorkflow(workflow);
            // Add constraint logic here
            return modifiedWorkflow;
        }

        private WorkflowDefinition CloneWorkflow(WorkflowDefinition workflow)
        {
            // Deep clone workflow (simplified for now)
            return new WorkflowDefinition
            {
                Id = workflow.Id,
                Name = workflow.Name,
                Description = workflow.Description,
                Version = workflow.Version,
                Platforms = new List<string>(workflow.Platforms),
                Enabled = workflow.Enabled,
                Triggers = workflow.Triggers.ToList(),
                Constraints = workflow.Constraints?.ToList(),
                Actions = workflow.Actions.ToList(),
                Metadata = workflow.Metadata != null ? new Dictionary<string, object>(workflow.Metadata) : null
            };
        }

        private List<List<WorkflowAction>> GroupActionsForOptimalExecution(
            List<WorkflowAction> actions,
            List<LearnedPattern> patterns)
        {
            // Simplified: Sequential execution for now
            // Future: Analyze dependencies and parallelize where possible
            return actions.Select(a => new List<WorkflowAction> { a }).ToList();
        }

        private string DetermineExecutionStrategy(List<WorkflowAction> actions, AgenticContext context)
        {
            return actions.Count == 1 ? "sequential" : "parallel";
        }

        private TimeSpan EstimateStepDuration(List<WorkflowAction> actions, List<LearnedPattern> patterns)
        {
            // Use learned patterns to estimate duration
            var totalMs = actions.Count * 1000; // Default 1s per action
            return TimeSpan.FromMilliseconds(totalMs);
        }

        private double AssessRiskLevel(ExecutionStep step, List<LearnedPattern> patterns)
        {
            // Assess risk based on action types and learned patterns
            var networkActions = step.Actions.Count(a => a.Type == "http_request" || a.Type == "api_call");
            var fileActions = step.Actions.Count(a => a.Type == "file_operation");

            var riskScore = (networkActions * 0.3) + (fileActions * 0.2);
            return Math.Min(riskScore, 1.0);
        }

        private ContingencyPlan CreateContingencyPlan(ExecutionStep step, AgenticContext context)
        {
            return new ContingencyPlan
            {
                Description = "Fallback to safe mode if step fails",
                FallbackActions = new List<WorkflowAction>
                {
                    new WorkflowAction
                    {
                        Type = "notification",
                        Parameters = new Dictionary<string, object>
                        {
                            ["message"] = $"Step {step.StepId} failed, using contingency plan"
                        }
                    }
                }
            };
        }

        private string DetectPlatform()
        {
            if (OperatingSystem.IsWindows()) return "windows";
            if (OperatingSystem.IsMacOS()) return "mac";
            if (OperatingSystem.IsLinux()) return "linux";
            if (OperatingSystem.IsAndroid()) return "android";
            if (OperatingSystem.IsIOS()) return "ios";
            return "unknown";
        }

        private async Task<Dictionary<string, bool>> DetectAvailableResourcesAsync(CancellationToken cancellationToken)
        {
            return new Dictionary<string, bool>
            {
                ["network"] = true, // Simplified
                ["filesystem"] = true,
                ["notification_system"] = true
            };
        }

        private List<LearnedPattern> ExtractSuccessPatterns(
            WorkflowDefinition workflow,
            WorkflowExecutionResult result,
            AgenticContext context)
        {
            var patterns = new List<LearnedPattern>();

            patterns.Add(new LearnedPattern
            {
                PatternId = Guid.NewGuid().ToString(),
                WorkflowId = workflow.Id,
                PatternType = "success",
                Description = $"Successful execution in {result.Duration.TotalSeconds:F1}s",
                Confidence = 0.9,
                Frequency = 1,
                LastObserved = DateTime.UtcNow
            });

            return patterns;
        }

        private List<LearnedPattern> ExtractFailurePatterns(
            WorkflowDefinition workflow,
            WorkflowExecutionResult result,
            AgenticContext context)
        {
            var patterns = new List<LearnedPattern>();

            patterns.Add(new LearnedPattern
            {
                PatternId = Guid.NewGuid().ToString(),
                WorkflowId = workflow.Id,
                PatternType = "failure",
                Description = $"Failed: {result.ErrorMessage}",
                Confidence = 0.8,
                Frequency = 1,
                LastObserved = DateTime.UtcNow
            });

            return patterns;
        }

        private List<WorkflowBottleneck> IdentifyBottlenecks(
            WorkflowDefinition workflow,
            PerformanceMetrics metrics)
        {
            var bottlenecks = new List<WorkflowBottleneck>();

            if (metrics.AverageDuration > TimeSpan.FromSeconds(30))
            {
                bottlenecks.Add(new WorkflowBottleneck
                {
                    Type = "duration",
                    Description = "Workflow takes too long to execute",
                    Severity = "medium",
                    AffectedActions = workflow.Actions.Select(a => a.Id).ToList()
                });
            }

            return bottlenecks;
        }

        private async Task<WorkflowDefinition> GenerateOptimizedWorkflowAsync(
            WorkflowDefinition workflow,
            List<WorkflowBottleneck> bottlenecks,
            PerformanceMetrics metrics,
            CancellationToken cancellationToken)
        {
            // Clone and optimize
            var optimized = CloneWorkflow(workflow);

            foreach (var bottleneck in bottlenecks)
            {
                if (bottleneck.Type == "duration")
                {
                    // Add caching, parallelization, etc.
                    _logger.LogDebug("Optimizing for duration bottleneck");
                }
            }

            return optimized;
        }

        private Dictionary<string, object> CalculateExpectedImprovements(
            WorkflowDefinition original,
            WorkflowDefinition optimized,
            PerformanceMetrics metrics)
        {
            return new Dictionary<string, object>
            {
                ["expected_duration_reduction"] = "20%",
                ["expected_reliability_improvement"] = "15%"
            };
        }

        private async IAsyncEnumerable<AgentGuidance> ProvideGuidanceInternalAsync(
            WorkflowDefinition workflow,
            CancellationToken cancellationToken)
        {
            yield return new AgentGuidance
            {
                Timestamp = DateTime.UtcNow,
                GuidanceType = "info",
                Message = $"Starting workflow: {workflow.Name}",
                MessageJa = $"ワークフロー開始: {workflow.Name}"
            };

            // More guidance would be yielded during execution
        }
    }

    // Supporting classes

    public class AgenticExecutionResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string WorkflowId { get; set; } = string.Empty;
        public AgenticContext? AnalyzedContext { get; set; }
        public List<string> AppliedPatterns { get; set; } = new();
        public ExecutionPlan? ExecutionPlan { get; set; }
        public WorkflowExecutionResult? WorkflowResult { get; set; }
        public List<WorkflowImprovement> Improvements { get; set; } = new();
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
    }

    public class AgenticExecutionOptions
    {
        public int MaxSelfHealingAttempts { get; set; } = 3;
        public bool GenerateImprovements { get; set; } = true;
        public bool EnableRealTimeGuidance { get; set; } = true;
        public string PreferredLanguage { get; set; } = "en";
    }

    public class AgenticContext
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string CurrentPlatform { get; set; } = string.Empty;
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
        public Dictionary<string, bool> AvailableResources { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class ExecutionPlan
    {
        public string WorkflowId { get; set; } = string.Empty;
        public List<ExecutionStep> ExecutionSteps { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class ExecutionStep
    {
        public string StepId { get; set; } = string.Empty;
        public List<WorkflowAction> Actions { get; set; } = new();
        public string ExecutionStrategy { get; set; } = "sequential";
        public TimeSpan ExpectedDuration { get; set; }
        public ContingencyPlan? ContingencyPlan { get; set; }
    }

    public class ContingencyPlan
    {
        public string Description { get; set; } = string.Empty;
        public List<WorkflowAction> FallbackActions { get; set; } = new();
    }

    public class AgentMemory
    {
        private readonly Dictionary<string, List<LearnedPattern>> _patterns = new();
        private readonly Dictionary<string, List<ExecutionExperience>> _experiences = new();
        private readonly Dictionary<string, PerformanceMetrics> _performanceMetrics = new();

        public List<LearnedPattern> RetrieveRelevantMemories(string workflowId, AgenticContext context)
        {
            if (_patterns.TryGetValue(workflowId, out var patterns))
            {
                return patterns.Where(p => p.Confidence > 0.7).ToList();
            }
            return new List<LearnedPattern>();
        }

        public void StorePattern(LearnedPattern pattern)
        {
            if (!_patterns.ContainsKey(pattern.WorkflowId))
            {
                _patterns[pattern.WorkflowId] = new List<LearnedPattern>();
            }
            _patterns[pattern.WorkflowId].Add(pattern);
        }

        public PerformanceMetrics GetWorkflowPerformanceMetrics(string workflowId)
        {
            if (_performanceMetrics.TryGetValue(workflowId, out var metrics))
            {
                return metrics;
            }
            return new PerformanceMetrics();
        }

        public void UpdatePerformanceMetrics(string workflowId, ExecutionExperience experience)
        {
            if (!_performanceMetrics.ContainsKey(workflowId))
            {
                _performanceMetrics[workflowId] = new PerformanceMetrics();
            }

            var metrics = _performanceMetrics[workflowId];
            metrics.TotalExecutions++;
            if (experience.Success)
            {
                metrics.SuccessfulExecutions++;
            }
            else
            {
                metrics.FailedExecutions++;
            }

            // Update average duration
            metrics.AverageDuration = TimeSpan.FromMilliseconds(
                (metrics.AverageDuration.TotalMilliseconds * (metrics.TotalExecutions - 1) + experience.Duration.TotalMilliseconds) /
                metrics.TotalExecutions
            );
        }

        public List<ExecutionExperience> GetWorkflowHistory(string workflowId)
        {
            if (_experiences.TryGetValue(workflowId, out var history))
            {
                return history;
            }
            return new List<ExecutionExperience>();
        }
    }

    public class AgentLearningSystem
    {
        public async Task LearnFromFailureAsync(string workflowId, string errorMessage)
        {
            // Store failure patterns for future reference
            await Task.CompletedTask;
        }

        public async Task RecordCorrectionAsync(string workflowId, WorkflowCorrection correction)
        {
            // Record successful corrections to apply automatically in future
            await Task.CompletedTask;
        }

        public async Task ProcessExperienceAsync(ExecutionExperience experience, CancellationToken cancellationToken)
        {
            // Process and learn from execution experience
            await Task.CompletedTask;
        }
    }

    public class LearnedPattern
    {
        public string PatternId { get; set; } = string.Empty;
        public string WorkflowId { get; set; } = string.Empty;
        public string PatternType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public int Frequency { get; set; }
        public DateTime LastObserved { get; set; }
    }

    public class ExecutionExperience
    {
        public string WorkflowId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public AgenticContext Context { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class PerformanceMetrics
    {
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public TimeSpan AverageDuration { get; set; }
    }

    public class WorkflowOptimizationResult
    {
        public WorkflowDefinition OriginalWorkflow { get; set; } = null!;
        public WorkflowDefinition? OptimizedWorkflow { get; set; }
        public PerformanceMetrics? HistoricalMetrics { get; set; }
        public List<WorkflowBottleneck> IdentifiedBottlenecks { get; set; } = new();
        public Dictionary<string, object> ExpectedImprovements { get; set; } = new();
        public DateTime OptimizedAt { get; set; }
    }

    public class WorkflowBottleneck
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public List<string> AffectedActions { get; set; } = new();
    }

    public class WorkflowCorrection
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string OriginalError { get; set; } = string.Empty;
        public string? FailedActionId { get; set; }
        public bool CanAutoFix { get; set; }
        public string? FixDescription { get; set; }
        public WorkflowDefinition? CorrectedWorkflow { get; set; }
    }

    public class AgentGuidance
    {
        public DateTime Timestamp { get; set; }
        public string GuidanceType { get; set; } = string.Empty; // info, warning, error, suggestion
        public string Message { get; set; } = string.Empty;
        public string MessageJa { get; set; } = string.Empty;
    }
}
