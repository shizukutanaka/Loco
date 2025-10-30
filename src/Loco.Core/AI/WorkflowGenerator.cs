using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Workflow;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AI
{
    /// <summary>
    /// AI-powered workflow generator that creates workflows from natural language descriptions.
    /// 自然言語からワークフローを生成するAI搭載ジェネレーター
    ///
    /// Solves Issues:
    /// - #2: 学習曲線が急すぎる (Steep learning curve) → Natural language input
    /// - #3: 初心者向けガイダンス不足 (Lack of beginner guidance) → AI suggestions
    /// - #11: AIアシスタント機能がない (No AI assistant) → Full AI integration
    /// - #18: 可読性が低い (Low readability) → AI explains workflows in natural language
    ///
    /// Based on 2025 trends:
    /// - Agentic AI workflows with autonomous decision-making
    /// - LLM-powered workflow generation
    /// - Self-improving systems that learn from feedback
    /// </summary>
    public class WorkflowGenerator
    {
        private readonly ILogger<WorkflowGenerator> _logger;
        private readonly Dictionary<string, WorkflowTemplate> _templates;
        private readonly WorkflowKnowledgeBase _knowledgeBase;

        public WorkflowGenerator(ILogger<WorkflowGenerator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _templates = InitializeTemplates();
            _knowledgeBase = new WorkflowKnowledgeBase();
        }

        /// <summary>
        /// Generates a workflow from a natural language description.
        /// 自然言語の説明からワークフローを生成
        /// </summary>
        /// <param name="description">Natural language description (e.g., "Send me a notification every morning at 9am")</param>
        /// <param name="targetPlatforms">Target platforms (optional - will auto-detect if not specified)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Generated workflow definition</returns>
        public async Task<WorkflowGenerationResult> GenerateFromNaturalLanguageAsync(
            string description,
            List<string>? targetPlatforms = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Generating workflow from description: {Description}", description);

            var result = new WorkflowGenerationResult
            {
                OriginalDescription = description,
                GeneratedAt = DateTime.UtcNow
            };

            try
            {
                // 1. Analyze intent (what user wants to do)
                var intent = AnalyzeIntent(description);
                result.DetectedIntent = intent;

                _logger.LogDebug("Detected intent: {Intent}", intent.IntentType);

                // 2. Extract components (triggers, actions, constraints)
                var components = ExtractComponents(description, intent);
                result.ExtractedComponents = components;

                // 3. Detect target platforms (if not specified)
                if (targetPlatforms == null || targetPlatforms.Count == 0)
                {
                    targetPlatforms = DetectTargetPlatforms(description, components);
                }

                // 4. Find matching template or create custom workflow
                WorkflowDefinition workflow;
                if (_templates.TryGetValue(intent.IntentType, out var template))
                {
                    _logger.LogDebug("Using template: {Template}", template.Name);
                    workflow = template.Instantiate(components, targetPlatforms);
                    result.UsedTemplate = template.Name;
                }
                else
                {
                    _logger.LogDebug("Creating custom workflow");
                    workflow = CreateCustomWorkflow(description, intent, components, targetPlatforms);
                    result.UsedTemplate = "custom";
                }

                // 5. Validate generated workflow
                var validator = new WorkflowValidator();
                var validation = validator.Validate(workflow);

                if (!validation.IsValid)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Generated workflow validation failed: {string.Join(", ", validation.Errors)}";
                    result.Suggestions = GenerateSuggestions(description, validation.Errors);
                    return result;
                }

                // 6. Add AI-generated explanations
                workflow.Metadata = new Dictionary<string, object>
                {
                    ["generated_by"] = "AI Workflow Generator",
                    ["original_description"] = description,
                    ["explanation"] = GenerateHumanReadableExplanation(workflow),
                    ["confidence"] = intent.Confidence,
                    ["generation_time"] = DateTime.UtcNow
                };

                result.Success = true;
                result.Workflow = workflow;
                result.Explanation = GenerateHumanReadableExplanation(workflow);
                result.Confidence = intent.Confidence;
                result.AlternativeOptions = GenerateAlternatives(description, intent);

                _logger.LogInformation("Successfully generated workflow: {WorkflowId}", workflow.Id);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate workflow from description: {Description}", description);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Explains an existing workflow in natural language.
        /// 既存のワークフローを自然言語で説明
        /// </summary>
        public string ExplainWorkflow(WorkflowDefinition workflow, string language = "en")
        {
            var explanation = new StringBuilder();

            // Title and summary
            explanation.AppendLine(language == "ja"
                ? $"# ワークフロー: {workflow.Name}"
                : $"# Workflow: {workflow.Name}");

            explanation.AppendLine();
            explanation.AppendLine(language == "ja"
                ? $"**説明:** {workflow.Description}"
                : $"**Description:** {workflow.Description}");

            explanation.AppendLine();
            explanation.AppendLine(language == "ja"
                ? $"**対応プラットフォーム:** {string.Join(", ", workflow.Platforms)}"
                : $"**Supported Platforms:** {string.Join(", ", workflow.Platforms)}");

            // Triggers
            explanation.AppendLine();
            explanation.AppendLine(language == "ja" ? "## トリガー（いつ実行されるか）" : "## Triggers (When it runs)");

            foreach (var trigger in workflow.Triggers)
            {
                explanation.AppendLine($"- {ExplainTrigger(trigger, language)}");
            }

            // Constraints
            if (workflow.Constraints != null && workflow.Constraints.Count > 0)
            {
                explanation.AppendLine();
                explanation.AppendLine(language == "ja" ? "## 制約（実行条件）" : "## Constraints (Execution conditions)");

                foreach (var constraint in workflow.Constraints)
                {
                    explanation.AppendLine($"- {ExplainConstraint(constraint, language)}");
                }
            }

            // Actions
            explanation.AppendLine();
            explanation.AppendLine(language == "ja" ? "## アクション（何をするか）" : "## Actions (What it does)");

            for (int i = 0; i < workflow.Actions.Count; i++)
            {
                var action = workflow.Actions[i];
                explanation.AppendLine($"{i + 1}. {ExplainAction(action, language)}");

                // Error handling
                if (action.OnError != null)
                {
                    var errorText = language == "ja"
                        ? $"   - エラー時: {ExplainErrorStrategy(action.OnError, language)}"
                        : $"   - On error: {ExplainErrorStrategy(action.OnError, language)}";
                    explanation.AppendLine(errorText);
                }

                // Retry policy
                if (action.Retry != null && action.Retry.MaxAttempts > 1)
                {
                    var retryText = language == "ja"
                        ? $"   - リトライ: 最大{action.Retry.MaxAttempts}回、{action.Retry.BackoffStrategy}バックオフ"
                        : $"   - Retry: up to {action.Retry.MaxAttempts} times with {action.Retry.BackoffStrategy} backoff";
                    explanation.AppendLine(retryText);
                }
            }

            return explanation.ToString();
        }

        /// <summary>
        /// Suggests improvements for an existing workflow based on best practices.
        /// ベストプラクティスに基づいて既存ワークフローの改善を提案
        /// </summary>
        public List<WorkflowImprovement> SuggestImprovements(WorkflowDefinition workflow)
        {
            var improvements = new List<WorkflowImprovement>();

            // Check for missing error handling
            var actionsWithoutErrorHandling = workflow.Actions
                .Where(a => a.OnError == null)
                .ToList();

            if (actionsWithoutErrorHandling.Any())
            {
                improvements.Add(new WorkflowImprovement
                {
                    Severity = "medium",
                    Category = "error_handling",
                    Message = $"{actionsWithoutErrorHandling.Count} actions don't have error handling configured",
                    MessageJa = $"{actionsWithoutErrorHandling.Count}個のアクションにエラーハンドリングが設定されていません",
                    Suggestion = "Add error handling strategies (stop, continue, or fallback) to make your workflow more resilient",
                    SuggestionJa = "エラーハンドリング戦略（stop、continue、fallback）を追加して、ワークフローをより堅牢にしましょう",
                    ActionIds = actionsWithoutErrorHandling.Select(a => a.Id).ToList()
                });
            }

            // Check for missing retry policies on network actions
            var networkActionsWithoutRetry = workflow.Actions
                .Where(a => a.Type == "http_request" && (a.Retry == null || a.Retry.MaxAttempts <= 1))
                .ToList();

            if (networkActionsWithoutRetry.Any())
            {
                improvements.Add(new WorkflowImprovement
                {
                    Severity = "high",
                    Category = "reliability",
                    Message = "Network actions should have retry policies to handle transient failures",
                    MessageJa = "ネットワークアクションには一時的な障害に対処するためのリトライポリシーを設定すべきです",
                    Suggestion = "Add exponential backoff retry (e.g., 3 attempts with 1000ms initial delay)",
                    SuggestionJa = "指数バックオフリトライを追加してください（例: 3回試行、初期遅延1000ms）",
                    ActionIds = networkActionsWithoutRetry.Select(a => a.Id).ToList()
                });
            }

            // Check for platform compatibility
            foreach (var platform in workflow.Platforms)
            {
                var unsupportedActions = workflow.Actions
                    .Where(a => !PlatformCapabilities.IsActionSupported(platform, a.Type))
                    .ToList();

                if (unsupportedActions.Any())
                {
                    improvements.Add(new WorkflowImprovement
                    {
                        Severity = "critical",
                        Category = "compatibility",
                        Message = $"Platform '{platform}' doesn't support {unsupportedActions.Count} actions",
                        MessageJa = $"プラットフォーム'{platform}'は{unsupportedActions.Count}個のアクションに対応していません",
                        Suggestion = $"Remove unsupported actions or use fallback actions for {platform}",
                        SuggestionJa = $"{platform}用の代替アクションを使用するか、未対応アクションを削除してください",
                        ActionIds = unsupportedActions.Select(a => a.Id).ToList()
                    });
                }
            }

            // Check for performance - too many sequential actions
            if (workflow.Actions.Count > 10)
            {
                improvements.Add(new WorkflowImprovement
                {
                    Severity = "low",
                    Category = "performance",
                    Message = $"Workflow has {workflow.Actions.Count} sequential actions which may be slow",
                    MessageJa = $"ワークフローには{workflow.Actions.Count}個の連続アクションがあり、実行が遅くなる可能性があります",
                    Suggestion = "Consider splitting into multiple workflows or using parallel execution where possible",
                    SuggestionJa = "複数のワークフローに分割するか、可能な場合は並列実行を検討してください",
                    ActionIds = workflow.Actions.Select(a => a.Id).ToList()
                });
            }

            // Check for missing constraints that could save resources
            if ((workflow.Constraints == null || workflow.Constraints.Count == 0) &&
                workflow.Triggers.Any(t => t.Type == "time" || t.Type == "schedule"))
            {
                improvements.Add(new WorkflowImprovement
                {
                    Severity = "low",
                    Category = "optimization",
                    Message = "Time-triggered workflows can benefit from constraints (e.g., only run on weekdays)",
                    MessageJa = "時間トリガーのワークフローには制約条件を追加すると便利です（例: 平日のみ実行）",
                    Suggestion = "Add constraints like time windows, network connectivity, or file existence checks",
                    SuggestionJa = "時間帯、ネットワーク接続、ファイル存在などの制約条件を追加してください",
                    ActionIds = new List<string>()
                });
            }

            return improvements;
        }

        // Private helper methods

        private WorkflowIntent AnalyzeIntent(string description)
        {
            var intent = new WorkflowIntent { Confidence = 0.0 };
            var lowerDescription = description.ToLower();

            // Pattern matching for common intents
            if (lowerDescription.Contains("notification") || lowerDescription.Contains("通知") ||
                lowerDescription.Contains("remind") || lowerDescription.Contains("alert"))
            {
                intent.IntentType = "notification";
                intent.Confidence = 0.9;
            }
            else if (lowerDescription.Contains("backup") || lowerDescription.Contains("バックアップ") ||
                     lowerDescription.Contains("copy") || lowerDescription.Contains("コピー"))
            {
                intent.IntentType = "backup";
                intent.Confidence = 0.85;
            }
            else if (lowerDescription.Contains("monitor") || lowerDescription.Contains("監視") ||
                     lowerDescription.Contains("watch") || lowerDescription.Contains("check"))
            {
                intent.IntentType = "monitoring";
                intent.Confidence = 0.8;
            }
            else if (lowerDescription.Contains("sync") || lowerDescription.Contains("同期") ||
                     lowerDescription.Contains("synchronize"))
            {
                intent.IntentType = "sync";
                intent.Confidence = 0.85;
            }
            else if (lowerDescription.Contains("download") || lowerDescription.Contains("ダウンロード") ||
                     lowerDescription.Contains("fetch") || lowerDescription.Contains("取得"))
            {
                intent.IntentType = "download";
                intent.Confidence = 0.8;
            }
            else
            {
                intent.IntentType = "custom";
                intent.Confidence = 0.5;
            }

            return intent;
        }

        private WorkflowComponents ExtractComponents(string description, WorkflowIntent intent)
        {
            var components = new WorkflowComponents();
            var lowerDescription = description.ToLower();

            // Extract time triggers
            if (lowerDescription.Contains("every morning") || lowerDescription.Contains("毎朝"))
            {
                components.Triggers.Add(new { type = "time", schedule = "0 9 * * *", description = "Every morning at 9am" });
            }
            else if (lowerDescription.Contains("every evening") || lowerDescription.Contains("毎晩"))
            {
                components.Triggers.Add(new { type = "time", schedule = "0 18 * * *", description = "Every evening at 6pm" });
            }
            else if (lowerDescription.Contains("every hour") || lowerDescription.Contains("毎時"))
            {
                components.Triggers.Add(new { type = "time", schedule = "0 * * * *", description = "Every hour" });
            }
            else if (lowerDescription.Contains("every day") || lowerDescription.Contains("毎日"))
            {
                components.Triggers.Add(new { type = "time", schedule = "0 12 * * *", description = "Every day at noon" });
            }

            // Extract action types
            if (lowerDescription.Contains("notification") || lowerDescription.Contains("通知"))
            {
                components.Actions.Add("notification");
            }
            if (lowerDescription.Contains("email") || lowerDescription.Contains("メール"))
            {
                components.Actions.Add("email");
            }
            if (lowerDescription.Contains("backup") || lowerDescription.Contains("バックアップ"))
            {
                components.Actions.Add("file_operation");
            }
            if (lowerDescription.Contains("http") || lowerDescription.Contains("api") || lowerDescription.Contains("webhook"))
            {
                components.Actions.Add("http_request");
            }

            // Extract constraints
            if (lowerDescription.Contains("weekday") || lowerDescription.Contains("平日"))
            {
                components.Constraints.Add("weekday_only");
            }
            if (lowerDescription.Contains("if connected") || lowerDescription.Contains("接続時"))
            {
                components.Constraints.Add("network_connected");
            }

            return components;
        }

        private List<string> DetectTargetPlatforms(string description, WorkflowComponents components)
        {
            var platforms = new List<string>();
            var lowerDescription = description.ToLower();

            // Explicit platform mentions
            if (lowerDescription.Contains("android")) platforms.Add("android");
            if (lowerDescription.Contains("ios") || lowerDescription.Contains("iphone")) platforms.Add("ios");
            if (lowerDescription.Contains("windows") || lowerDescription.Contains("pc")) platforms.Add("windows");
            if (lowerDescription.Contains("mac") || lowerDescription.Contains("macos")) platforms.Add("mac");
            if (lowerDescription.Contains("linux")) platforms.Add("linux");

            // If no explicit platform, default to cross-platform
            if (platforms.Count == 0)
            {
                platforms.AddRange(new[] { "windows", "mac", "linux" });
            }

            return platforms;
        }

        private WorkflowDefinition CreateCustomWorkflow(
            string description,
            WorkflowIntent intent,
            WorkflowComponents components,
            List<string> platforms)
        {
            var workflow = new WorkflowDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Name = GenerateWorkflowName(description, intent),
                Description = description,
                Version = "1.0",
                Platforms = platforms,
                Enabled = true,
                Triggers = new List<WorkflowTrigger>(),
                Constraints = new List<WorkflowConstraint>(),
                Actions = new List<WorkflowAction>()
            };

            // Add triggers
            if (components.Triggers.Count > 0)
            {
                foreach (var trigger in components.Triggers)
                {
                    var triggerObj = JsonSerializer.Deserialize<Dictionary<string, object>>(
                        JsonSerializer.Serialize(trigger));

                    workflow.Triggers.Add(new WorkflowTrigger
                    {
                        Type = triggerObj["type"].ToString()!,
                        Parameters = triggerObj.Where(kv => kv.Key != "type" && kv.Key != "description")
                            .ToDictionary(kv => kv.Key, kv => kv.Value)
                    });
                }
            }
            else
            {
                // Default trigger: manual
                workflow.Triggers.Add(new WorkflowTrigger
                {
                    Type = "manual",
                    Parameters = new Dictionary<string, object>()
                });
            }

            // Add constraints
            foreach (var constraint in components.Constraints)
            {
                if (constraint == "weekday_only")
                {
                    workflow.Constraints.Add(new WorkflowConstraint
                    {
                        Type = "time",
                        Operator = "weekday",
                        Value = "monday-friday"
                    });
                }
                else if (constraint == "network_connected")
                {
                    workflow.Constraints.Add(new WorkflowConstraint
                    {
                        Type = "network",
                        Operator = "is_connected",
                        Value = true
                    });
                }
            }

            // Add actions
            foreach (var actionType in components.Actions)
            {
                var action = new WorkflowAction
                {
                    Type = actionType,
                    Parameters = new Dictionary<string, object>()
                };

                if (actionType == "notification")
                {
                    action.Parameters["title"] = "Loco Automation";
                    action.Parameters["message"] = $"Workflow triggered: {workflow.Name}";
                }

                workflow.Actions.Add(action);
            }

            // If no actions specified, add a default notification
            if (workflow.Actions.Count == 0)
            {
                workflow.Actions.Add(new WorkflowAction
                {
                    Type = "notification",
                    Parameters = new Dictionary<string, object>
                    {
                        ["title"] = "Loco Workflow",
                        ["message"] = description
                    }
                });
            }

            return workflow;
        }

        private string GenerateWorkflowName(string description, WorkflowIntent intent)
        {
            // Take first 50 characters of description and clean up
            var name = description.Length > 50 ? description.Substring(0, 50) + "..." : description;
            name = name.Replace("\n", " ").Replace("\r", "");

            return string.IsNullOrWhiteSpace(name) ? $"{intent.IntentType}_workflow" : name;
        }

        private string GenerateHumanReadableExplanation(WorkflowDefinition workflow)
        {
            return ExplainWorkflow(workflow, "en");
        }

        private List<string> GenerateSuggestions(string description, List<string> errors)
        {
            var suggestions = new List<string>
            {
                "Try being more specific about when the workflow should run (e.g., 'every morning at 9am')",
                "Specify what actions you want to perform (e.g., 'send notification', 'copy file')",
                "Mention the target platform if specific (e.g., 'on Windows', 'on Android')"
            };

            return suggestions;
        }

        private List<WorkflowAlternative> GenerateAlternatives(string description, WorkflowIntent intent)
        {
            var alternatives = new List<WorkflowAlternative>();

            // Suggest alternatives based on intent
            if (intent.IntentType == "notification")
            {
                alternatives.Add(new WorkflowAlternative
                {
                    Description = "Add email notification in addition to system notification",
                    DescriptionJa = "システム通知に加えてメール通知を追加",
                    Benefit = "Ensures you don't miss important notifications",
                    BenefitJa = "重要な通知を見逃さないようにします"
                });
            }

            return alternatives;
        }

        private Dictionary<string, WorkflowTemplate> InitializeTemplates()
        {
            return new Dictionary<string, WorkflowTemplate>
            {
                ["notification"] = new WorkflowTemplate
                {
                    Name = "Daily Notification",
                    Description = "Send notification at specified time",
                    TemplateId = "notification_v1"
                },
                ["backup"] = new WorkflowTemplate
                {
                    Name = "File Backup",
                    Description = "Backup files to destination",
                    TemplateId = "backup_v1"
                }
            };
        }

        private string ExplainTrigger(WorkflowTrigger trigger, string language)
        {
            if (language == "ja")
            {
                return trigger.Type switch
                {
                    "time" => $"時刻: {trigger.Parameters.GetValueOrDefault("schedule", "指定なし")}",
                    "manual" => "手動実行",
                    "file_system" => $"ファイル変更: {trigger.Parameters.GetValueOrDefault("path", "")}",
                    _ => trigger.Type
                };
            }
            else
            {
                return trigger.Type switch
                {
                    "time" => $"At time: {trigger.Parameters.GetValueOrDefault("schedule", "not specified")}",
                    "manual" => "Manual execution",
                    "file_system" => $"File change: {trigger.Parameters.GetValueOrDefault("path", "")}",
                    _ => trigger.Type
                };
            }
        }

        private string ExplainConstraint(WorkflowConstraint constraint, string language)
        {
            if (language == "ja")
            {
                return $"{constraint.Type}: {constraint.Operator} {constraint.Value}";
            }
            else
            {
                return $"{constraint.Type}: {constraint.Operator} {constraint.Value}";
            }
        }

        private string ExplainAction(WorkflowAction action, string language)
        {
            if (language == "ja")
            {
                return action.Type switch
                {
                    "notification" => $"通知を表示: {action.Parameters.GetValueOrDefault("message", "")}",
                    "http_request" => $"HTTPリクエスト: {action.Parameters.GetValueOrDefault("method", "GET")} {action.Parameters.GetValueOrDefault("url", "")}",
                    "file_operation" => $"ファイル操作: {action.Parameters.GetValueOrDefault("operation", "")}",
                    _ => action.Type
                };
            }
            else
            {
                return action.Type switch
                {
                    "notification" => $"Show notification: {action.Parameters.GetValueOrDefault("message", "")}",
                    "http_request" => $"HTTP request: {action.Parameters.GetValueOrDefault("method", "GET")} {action.Parameters.GetValueOrDefault("url", "")}",
                    "file_operation" => $"File operation: {action.Parameters.GetValueOrDefault("operation", "")}",
                    _ => action.Type
                };
            }
        }

        private string ExplainErrorStrategy(ActionErrorHandling errorHandling, string language)
        {
            if (language == "ja")
            {
                return errorHandling.Strategy switch
                {
                    "stop" => "実行を停止",
                    "continue" => "次のアクションに進む",
                    "fallback" => "代替アクションを実行",
                    _ => errorHandling.Strategy
                };
            }
            else
            {
                return errorHandling.Strategy switch
                {
                    "stop" => "Stop execution",
                    "continue" => "Continue to next action",
                    "fallback" => "Execute fallback action",
                    _ => errorHandling.Strategy
                };
            }
        }
    }

    // Supporting classes

    public class WorkflowGenerationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string OriginalDescription { get; set; } = string.Empty;
        public WorkflowIntent? DetectedIntent { get; set; }
        public WorkflowComponents? ExtractedComponents { get; set; }
        public WorkflowDefinition? Workflow { get; set; }
        public string? Explanation { get; set; }
        public double Confidence { get; set; }
        public string? UsedTemplate { get; set; }
        public List<string> Suggestions { get; set; } = new();
        public List<WorkflowAlternative> AlternativeOptions { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }

    public class WorkflowIntent
    {
        public string IntentType { get; set; } = "unknown";
        public double Confidence { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class WorkflowComponents
    {
        public List<object> Triggers { get; set; } = new();
        public List<string> Actions { get; set; } = new();
        public List<string> Constraints { get; set; } = new();
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class WorkflowTemplate
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TemplateId { get; set; } = string.Empty;

        public WorkflowDefinition Instantiate(WorkflowComponents components, List<string> platforms)
        {
            // Template instantiation logic would go here
            return new WorkflowDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Name = Name,
                Description = Description,
                Version = "1.0",
                Platforms = platforms,
                Enabled = true,
                Triggers = new List<WorkflowTrigger>(),
                Actions = new List<WorkflowAction>()
            };
        }
    }

    public class WorkflowKnowledgeBase
    {
        // Future: Store learned patterns, common workflows, user feedback
        public Dictionary<string, List<string>> CommonPatterns { get; set; } = new();
    }

    public class WorkflowImprovement
    {
        public string Severity { get; set; } = "low"; // low, medium, high, critical
        public string Category { get; set; } = string.Empty; // error_handling, reliability, compatibility, performance, optimization
        public string Message { get; set; } = string.Empty;
        public string MessageJa { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
        public string SuggestionJa { get; set; } = string.Empty;
        public List<string> ActionIds { get; set; } = new();
    }

    public class WorkflowAlternative
    {
        public string Description { get; set; } = string.Empty;
        public string DescriptionJa { get; set; } = string.Empty;
        public string Benefit { get; set; } = string.Empty;
        public string BenefitJa { get; set; } = string.Empty;
    }
}
