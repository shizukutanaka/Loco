// Phase 5: Workflow DSL (Domain Specific Language)
// Fluent API for defining workflows programmatically
// Makes workflow definition intuitive and type-safe

using System;
using System.Collections.Generic;
using System.Linq;
using Loco.Core.Workflows.Advanced;

namespace Loco.Core.Workflows.DSL;

/// <summary>
/// Fluent workflow builder
/// </summary>
public class WorkflowBuilder
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private readonly List<AdvancedStep> _steps;

    public WorkflowBuilder()
    {
        _steps = new List<AdvancedStep>();
    }

    /// <summary>
    /// Set workflow name
    /// </summary>
    public WorkflowBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Set workflow description
    /// </summary>
    public WorkflowBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Add action step
    /// </summary>
    public WorkflowBuilder AddAction(string stepId, string name, string action, Dictionary<string, object>? parameters = null)
    {
        _steps.Add(new AdvancedStep
        {
            Id = stepId,
            Name = name,
            Type = StepType.Action,
            Action = action,
            Parameters = parameters ?? new Dictionary<string, object>(),
        });

        return this;
    }

    /// <summary>
    /// Add conditional step
    /// </summary>
    public WorkflowBuilder AddCondition(
        string stepId,
        string condition,
        Action<WorkflowBuilder> thenBuild,
        Action<WorkflowBuilder>? elseBuild = null)
    {
        var thenBuilder = new WorkflowBuilder();
        thenBuild(thenBuilder);

        var conditionStep = new AdvancedStep
        {
            Id = stepId,
            Name = $"If {condition}",
            Type = StepType.Condition,
            Condition = condition,
            ThenSteps = thenBuilder._steps,
        };

        if (elseBuild != null)
        {
            var elseBuilder = new WorkflowBuilder();
            elseBuild(elseBuilder);
            conditionStep.ElseSteps = elseBuilder._steps;
        }

        _steps.Add(conditionStep);

        return this;
    }

    /// <summary>
    /// Add parallel steps
    /// </summary>
    public WorkflowBuilder AddParallel(string stepId, params Action<WorkflowBuilder>[] parallelBuilders)
    {
        var parallelSteps = new List<AdvancedStep>();

        foreach (var builder in parallelBuilders)
        {
            var parallelBuilder = new WorkflowBuilder();
            builder(parallelBuilder);
            parallelSteps.AddRange(parallelBuilder._steps);
        }

        _steps.Add(new AdvancedStep
        {
            Id = stepId,
            Name = "Parallel Steps",
            Type = StepType.Parallel,
            ParallelSteps = parallelSteps,
        });

        return this;
    }

    /// <summary>
    /// Add loop step
    /// </summary>
    public WorkflowBuilder AddLoop(
        string stepId,
        string loopVariable,
        Action<WorkflowBuilder> loopBuild)
    {
        var loopBuilder = new WorkflowBuilder();
        loopBuild(loopBuilder);

        _steps.Add(new AdvancedStep
        {
            Id = stepId,
            Name = $"Loop over {loopVariable}",
            Type = StepType.Loop,
            LoopVariable = loopVariable,
            LoopSteps = loopBuilder._steps,
        });

        return this;
    }

    /// <summary>
    /// Add switch step
    /// </summary>
    public WorkflowBuilder AddSwitch(
        string stepId,
        string switchExpression,
        Dictionary<string, Action<WorkflowBuilder>> cases)
    {
        var switchCases = new Dictionary<string, List<AdvancedStep>>();

        foreach (var (caseValue, caseBuild) in cases)
        {
            var caseBuilder = new WorkflowBuilder();
            caseBuild(caseBuilder);
            switchCases[caseValue] = caseBuilder._steps;
        }

        _steps.Add(new AdvancedStep
        {
            Id = stepId,
            Name = $"Switch {switchExpression}",
            Type = StepType.Switch,
            SwitchExpression = switchExpression,
            Cases = switchCases,
        });

        return this;
    }

    /// <summary>
    /// Add delay step
    /// </summary>
    public WorkflowBuilder AddDelay(string stepId, int delaySeconds)
    {
        _steps.Add(new AdvancedStep
        {
            Id = stepId,
            Name = $"Delay {delaySeconds}s",
            Type = StepType.Delay,
            DelaySeconds = delaySeconds,
        });

        return this;
    }

    /// <summary>
    /// Build workflow definition
    /// </summary>
    public List<AdvancedStep> Build()
    {
        return _steps;
    }
}

/// <summary>
/// Fluent step configuration
/// </summary>
public class StepBuilder
{
    private readonly AdvancedStep _step;

    public StepBuilder(AdvancedStep step)
    {
        _step = step;
    }

    /// <summary>
    /// Set retry policy
    /// </summary>
    public StepBuilder WithRetry(int maxAttempts = 3, int initialDelaySeconds = 1, double backoffMultiplier = 2.0)
    {
        _step.RetryPolicy = new RetryPolicy
        {
            MaxAttempts = maxAttempts,
            InitialDelaySeconds = initialDelaySeconds,
            BackoffMultiplier = backoffMultiplier,
        };

        return this;
    }

    /// <summary>
    /// Set timeout
    /// </summary>
    public StepBuilder WithTimeout(int timeoutSeconds)
    {
        _step.TimeoutSeconds = timeoutSeconds;
        return this;
    }

    /// <summary>
    /// Set error handling
    /// </summary>
    public StepBuilder OnError(string errorPolicy) // 'continue', 'stop', 'compensate'
    {
        _step.OnError = errorPolicy;
        return this;
    }

    /// <summary>
    /// Add parameters
    /// </summary>
    public StepBuilder WithParameter(string key, object value)
    {
        _step.Parameters ??= new Dictionary<string, object>();
        _step.Parameters[key] = value;
        return this;
    }

    /// <summary>
    /// Build step
    /// </summary>
    public AdvancedStep Build()
    {
        return _step;
    }
}

/// <summary>
/// Example: Order Processing Workflow
/// Demonstrates DSL usage
/// </summary>
public static class WorkflowExamples
{
    /// <summary>
    /// Create order processing workflow using DSL
    /// </summary>
    public static List<AdvancedStep> CreateOrderProcessingWorkflow()
    {
        return new WorkflowBuilder()
            .WithName("Order Processing")
            .WithDescription("Process customer orders with payment and fulfillment")

            // Step 1: Validate order
            .AddAction("validate-order", "Validate Order", "validate-endpoint",
                new Dictionary<string, object>
                {
                    ["apiUrl"] = "https://api.example.com/validate",
                    ["timeout"] = 30,
                })

            // Step 2: Check inventory (parallel with payment)
            .AddParallel("parallel-checks",
                builder => builder.AddAction("check-inventory", "Check Inventory", "inventory-service"),
                builder => builder.AddAction("process-payment", "Process Payment", "payment-service")
            )

            // Step 3: If payment successful
            .AddCondition("check-payment",
                "${paymentStatus} == 'success'",
                thenBuilder => thenBuilder
                    .AddAction("create-shipment", "Create Shipment", "fulfillment-service")
                    .AddAction("send-confirmation", "Send Confirmation", "email-service"),

                elseBuilder => elseBuilder
                    .AddAction("refund-payment", "Refund Payment", "payment-service")
                    .AddAction("notify-customer", "Notify Customer", "notification-service")
            )

            // Step 4: Archive order
            .AddAction("archive-order", "Archive Order", "archive-service")

            .Build();
    }

    /// <summary>
    /// Create data processing workflow
    /// </summary>
    public static List<AdvancedStep> CreateDataProcessingWorkflow()
    {
        return new WorkflowBuilder()
            .WithName("Data Processing Pipeline")
            .WithDescription("Process data through multiple stages")

            // Step 1: Extract data
            .AddAction("extract", "Extract Data", "s3-extract",
                new Dictionary<string, object> { ["bucket"] = "raw-data" })

            // Step 2: Transform data in parallel for each format
            .AddLoop("transform-loop",
                "${formats}",
                builder => builder
                    .AddAction("transform-data", "Transform", "transform-service")
                    .AddDelay("pause", 5)
            )

            // Step 3: Load to warehouse
            .AddAction("load", "Load to DW", "warehouse-load")

            // Step 4: Validate
            .AddCondition("validate-result",
                "${rowsLoaded} > 0",
                thenBuilder => thenBuilder
                    .AddAction("send-report", "Send Report", "email-service"),
                elseBuilder => elseBuilder
                    .AddAction("alert-team", "Alert Team", "slack-service")
            )

            .Build();
    }

    /// <summary>
    /// Create user onboarding workflow
    /// </summary>
    public static List<AdvancedStep> CreateOnboardingWorkflow()
    {
        return new WorkflowBuilder()
            .WithName("User Onboarding")
            .WithDescription("Complete user registration and setup")

            // Step 1: Create account
            .AddAction("create-account", "Create Account", "user-service")

            // Step 2: Send verification email and create default resources (parallel)
            .AddParallel("setup-parallel",
                builder => builder.AddAction("send-email", "Send Verification Email", "email-service"),
                builder => builder.AddAction("create-workspace", "Create Workspace", "workspace-service"),
                builder => builder.AddAction("provision-storage", "Provision Storage", "storage-service")
            )

            // Step 3: Wait for email verification
            .AddDelay("wait-verification", 300) // 5 minutes

            // Step 4: Check verification status
            .AddCondition("check-verified",
                "${emailVerified} == true",
                thenBuilder => thenBuilder
                    .AddAction("activate-account", "Activate Account", "user-service")
                    .AddAction("send-welcome", "Send Welcome Email", "email-service"),
                elseBuilder => elseBuilder
                    .AddAction("resend-verification", "Resend Verification", "email-service")
            )

            .Build();
    }

    /// <summary>
    /// Create approval workflow with conditional routing
    /// </summary>
    public static List<AdvancedStep> CreateApprovalWorkflow()
    {
        return new WorkflowBuilder()
            .WithName("Expense Approval")
            .WithDescription("Route expense requests to appropriate approvers")

            // Step 1: Validate expense
            .AddAction("validate-expense", "Validate Expense", "expense-service")

            // Step 2: Route based on amount
            .AddSwitch("route-approval",
                "${amount}",
                new Dictionary<string, Action<WorkflowBuilder>>
                {
                    {
                        "low",
                        builder => builder
                            .AddAction("auto-approve", "Auto Approve", "approval-service")
                            .AddAction("notify-submitter", "Notify Submitter", "notification-service")
                    },
                    {
                        "medium",
                        builder => builder
                            .AddAction("send-to-manager", "Send to Manager", "routing-service")
                            .AddDelay("wait-approval", 3600)
                    },
                    {
                        "high",
                        builder => builder
                            .AddAction("send-to-director", "Send to Director", "routing-service")
                            .AddDelay("wait-approval", 7200)
                    },
                }
            )

            // Step 3: Process result
            .AddCondition("check-approval",
                "${approved} == true",
                thenBuilder => thenBuilder
                    .AddAction("process-payment", "Process Payment", "finance-service"),
                elseBuilder => elseBuilder
                    .AddAction("notify-rejection", "Notify Rejection", "notification-service")
            )

            .Build();
    }
}

/// <summary>
/// Workflow validation
/// </summary>
public class WorkflowValidator
{
    /// <summary>
    /// Validate workflow definition
    /// </summary>
    public static (bool IsValid, List<string> Errors) Validate(List<AdvancedStep> steps)
    {
        var errors = new List<string>();

        if (steps == null || steps.Count == 0)
        {
            errors.Add("Workflow must contain at least one step");
            return (false, errors);
        }

        var stepIds = new HashSet<string>();

        foreach (var step in steps)
        {
            // Validate step ID uniqueness
            if (stepIds.Contains(step.Id))
            {
                errors.Add($"Duplicate step ID: {step.Id}");
                continue;
            }
            stepIds.Add(step.Id);

            // Validate step type-specific requirements
            switch (step.Type)
            {
                case StepType.Action:
                    if (string.IsNullOrEmpty(step.Action))
                        errors.Add($"Step {step.Id}: Action step must have action specified");
                    break;

                case StepType.Condition:
                    if (string.IsNullOrEmpty(step.Condition))
                        errors.Add($"Step {step.Id}: Condition step must have condition expression");
                    if ((step.ThenSteps == null || step.ThenSteps.Count == 0) &&
                        (step.ElseSteps == null || step.ElseSteps.Count == 0))
                        errors.Add($"Step {step.Id}: Condition step must have at least one branch");
                    break;

                case StepType.Parallel:
                    if (step.ParallelSteps == null || step.ParallelSteps.Count < 2)
                        errors.Add($"Step {step.Id}: Parallel step must have at least 2 parallel branches");
                    break;

                case StepType.Loop:
                    if (string.IsNullOrEmpty(step.LoopVariable))
                        errors.Add($"Step {step.Id}: Loop step must specify loop variable");
                    if (step.LoopSteps == null || step.LoopSteps.Count == 0)
                        errors.Add($"Step {step.Id}: Loop must have at least one step");
                    break;

                case StepType.Switch:
                    if (string.IsNullOrEmpty(step.SwitchExpression))
                        errors.Add($"Step {step.Id}: Switch step must specify switch expression");
                    if (step.Cases == null || step.Cases.Count == 0)
                        errors.Add($"Step {step.Id}: Switch must have at least one case");
                    break;
            }
        }

        return (errors.Count == 0, errors);
    }
}
