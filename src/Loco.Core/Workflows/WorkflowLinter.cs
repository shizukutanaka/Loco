using System.Text;
using System.Text.RegularExpressions;

namespace Loco.Core.Workflows;

/// <summary>
/// Linting rule for workflows.
/// </summary>
public class LintRule
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public HealthSeverity Severity { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// A linting violation found in a workflow.
/// </summary>
public class LintViolation
{
    public LintRule Rule { get; set; } = null!;
    public string Message { get; set; } = "";
    public string? Location { get; set; }
    public string? CodeSnippet { get; set; }
    public string? FixSuggestion { get; set; }
}

/// <summary>
/// Linting report for a workflow.
/// </summary>
public class LintReport
{
    public string WorkflowId { get; set; } = "";
    public string WorkflowName { get; set; } = "";
    public List<LintViolation> Violations { get; set; } = new();
    public int RulesChecked { get; set; }
    public DateTime LintedAt { get; set; } = DateTime.UtcNow;

    public bool HasViolations => Violations.Count > 0;
    public bool HasCriticalViolations => Violations.Any(v => v.Rule.Severity == HealthSeverity.Critical);
    public bool HasErrors => Violations.Any(v => v.Rule.Severity == HealthSeverity.Error);
}

/// <summary>
/// Lints workflows for code quality and best practices.
/// </summary>
public class WorkflowLinter
{
    private readonly List<LintRule> _rules = new();
    private readonly List<LintViolation> _violations = new();

    public WorkflowLinter()
    {
        InitializeRules();
    }

    /// <summary>
    /// Initializes built-in linting rules.
    /// </summary>
    private void InitializeRules()
    {
        // Naming conventions
        _rules.Add(new LintRule
        {
            Id = "naming-001",
            Name = "Workflow ID format",
            Description = "Workflow IDs should use kebab-case",
            Severity = HealthSeverity.Warning
        });

        _rules.Add(new LintRule
        {
            Id = "naming-002",
            Name = "Step ID format",
            Description = "Step IDs should use kebab-case",
            Severity = HealthSeverity.Warning
        });

        _rules.Add(new LintRule
        {
            Id = "naming-003",
            Name = "Descriptive names",
            Description = "Names should be descriptive (minimum 3 characters)",
            Severity = HealthSeverity.Info
        });

        // Documentation
        _rules.Add(new LintRule
        {
            Id = "docs-001",
            Name = "Workflow description required",
            Description = "All workflows should have a description",
            Severity = HealthSeverity.Warning
        });

        _rules.Add(new LintRule
        {
            Id = "docs-002",
            Name = "Complex step documentation",
            Description = "Steps with conditions or retries should have descriptions",
            Severity = HealthSeverity.Info
        });

        // Error handling
        _rules.Add(new LintRule
        {
            Id = "error-001",
            Name = "HTTP error handling",
            Description = "HTTP steps should have retry logic or error handlers",
            Severity = HealthSeverity.Warning
        });

        _rules.Add(new LintRule
        {
            Id = "error-002",
            Name = "Critical step protection",
            Description = "Steps that shouldn't fail should have continueOnError",
            Severity = HealthSeverity.Info
        });

        // Performance
        _rules.Add(new LintRule
        {
            Id = "perf-001",
            Name = "HTTP timeout required",
            Description = "HTTP steps should have explicit timeouts",
            Severity = HealthSeverity.Warning
        });

        _rules.Add(new LintRule
        {
            Id = "perf-002",
            Name = "Excessive retries",
            Description = "Retry count should not exceed 5",
            Severity = HealthSeverity.Warning
        });

        _rules.Add(new LintRule
        {
            Id = "perf-003",
            Name = "Parallel execution opportunity",
            Description = "Independent steps should use parallel execution",
            Severity = HealthSeverity.Info
        });

        // Security
        _rules.Add(new LintRule
        {
            Id = "security-001",
            Name = "No hardcoded credentials",
            Description = "Credentials should use environment variables",
            Severity = HealthSeverity.Critical
        });

        _rules.Add(new LintRule
        {
            Id = "security-002",
            Name = "HTTPS preferred",
            Description = "Use HTTPS instead of HTTP when possible",
            Severity = HealthSeverity.Warning
        });

        // Maintainability
        _rules.Add(new LintRule
        {
            Id = "maint-001",
            Name = "Workflow size",
            Description = "Workflows should have fewer than 50 steps",
            Severity = HealthSeverity.Warning
        });

        _rules.Add(new LintRule
        {
            Id = "maint-002",
            Name = "Unused variables",
            Description = "Remove unused variables",
            Severity = HealthSeverity.Info
        });

        _rules.Add(new LintRule
        {
            Id = "maint-003",
            Name = "Magic values",
            Description = "Replace magic values with variables",
            Severity = HealthSeverity.Info
        });
    }

    /// <summary>
    /// Lints a workflow and returns a report.
    /// </summary>
    public LintReport LintWorkflow(WorkflowDefinition workflow)
    {
        _violations.Clear();

        CheckNamingConventions(workflow);
        CheckDocumentation(workflow);
        CheckErrorHandling(workflow);
        CheckPerformance(workflow);
        CheckSecurity(workflow);
        CheckMaintainability(workflow);

        return new LintReport
        {
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            Violations = new List<LintViolation>(_violations),
            RulesChecked = _rules.Count(r => r.Enabled)
        };
    }

    /// <summary>
    /// Checks naming conventions.
    /// </summary>
    private void CheckNamingConventions(WorkflowDefinition workflow)
    {
        // Check workflow ID format
        if (!string.IsNullOrWhiteSpace(workflow.Id) && !IsKebabCase(workflow.Id))
        {
            AddViolation("naming-001",
                $"Workflow ID '{workflow.Id}' should use kebab-case (e.g., 'my-workflow-name')",
                fixSuggestion: $"Rename to: {ToKebabCase(workflow.Id)}");
        }

        // Check step IDs
        if (workflow.Steps != null)
        {
            foreach (var step in workflow.Steps)
            {
                if (!string.IsNullOrWhiteSpace(step.Id) && !IsKebabCase(step.Id))
                {
                    AddViolation("naming-002",
                        $"Step ID '{step.Id}' should use kebab-case",
                        location: $"Step {step.Id}",
                        fixSuggestion: $"Rename to: {ToKebabCase(step.Id)}");
                }

                // Check name length
                if (!string.IsNullOrWhiteSpace(step.Name) && step.Name.Length < 3)
                {
                    AddViolation("naming-003",
                        $"Step name '{step.Name}' is too short",
                        location: $"Step {step.Id}",
                        fixSuggestion: "Use a more descriptive name (at least 3 characters)");
                }
            }
        }
    }

    /// <summary>
    /// Checks documentation requirements.
    /// </summary>
    private void CheckDocumentation(WorkflowDefinition workflow)
    {
        // Check workflow description
        if (string.IsNullOrWhiteSpace(workflow.Description))
        {
            AddViolation("docs-001",
                "Workflow has no description",
                fixSuggestion: "Add a 'description' field explaining the workflow's purpose");
        }

        // Check complex step documentation
        if (workflow.Steps != null)
        {
            foreach (var step in workflow.Steps)
            {
                bool isComplex = step.RetryCount.HasValue ||
                                 step.RunIf != null ||
                                 step.SkipIf != null ||
                                 (step.DependsOn != null && step.DependsOn.Count > 0) ||
                                 (step.Dependencies != null && step.Dependencies.Count > 0);

                if (isComplex && string.IsNullOrWhiteSpace(step.Description))
                {
                    AddViolation("docs-002",
                        $"Complex step '{step.Id}' has no description",
                        location: $"Step {step.Id}",
                        fixSuggestion: "Add a 'description' field explaining the step's behavior");
                }
            }
        }
    }

    /// <summary>
    /// Checks error handling.
    /// </summary>
    private void CheckErrorHandling(WorkflowDefinition workflow)
    {
        if (workflow.Steps == null) return;

        foreach (var step in workflow.Steps)
        {
            // Check HTTP error handling
            if (step.Type == "http")
            {
                bool hasErrorHandling = step.RetryCount.HasValue ||
                                        step.OnFailure != null ||
                                        step.ContinueOnError == true;

                if (!hasErrorHandling)
                {
                    AddViolation("error-001",
                        $"HTTP step '{step.Id}' has no error handling",
                        location: $"Step {step.Id}",
                        fixSuggestion: "Add 'retryCount', 'onFailure', or 'continueOnError'");
                }
            }
        }
    }

    /// <summary>
    /// Checks performance issues.
    /// </summary>
    private void CheckPerformance(WorkflowDefinition workflow)
    {
        if (workflow.Steps == null) return;

        foreach (var step in workflow.Steps)
        {
            // Check HTTP timeouts
            if (step.Type == "http" && !step.TimeoutSeconds.HasValue)
            {
                AddViolation("perf-001",
                    $"HTTP step '{step.Id}' has no timeout",
                    location: $"Step {step.Id}",
                    fixSuggestion: "Add 'timeoutSeconds: 30' (or appropriate value)");
            }

            // Check excessive retries
            if (step.RetryCount.HasValue && step.RetryCount.Value > 5)
            {
                AddViolation("perf-002",
                    $"Step '{step.Id}' has excessive retry count: {step.RetryCount}",
                    location: $"Step {step.Id}",
                    fixSuggestion: "Reduce retry count to 5 or less");
            }
        }

        // Check for parallel execution opportunities
        if (workflow.Steps.Count > 5)
        {
            var hasParallel = workflow.Steps.Any(s => s.AllowParallel == true);
            var hasDeps = workflow.Steps.Any(s =>
                (s.DependsOn != null && s.DependsOn.Count > 0) ||
                (s.Dependencies != null && s.Dependencies.Count > 0));

            if (!hasParallel && !hasDeps)
            {
                AddViolation("perf-003",
                    "Workflow has no parallel execution configured",
                    fixSuggestion: "Consider using 'dependsOn' and 'allowParallel' for independent steps");
            }
        }
    }

    /// <summary>
    /// Checks security issues.
    /// </summary>
    private void CheckSecurity(WorkflowDefinition workflow)
    {
        // Check for hardcoded credentials patterns
        var credentialPatterns = new[]
        {
            @"password\s*[:=]\s*['""][^'""]+['""]",
            @"api[_-]?key\s*[:=]\s*['""][^'""]+['""]",
            @"secret\s*[:=]\s*['""][^'""]+['""]",
            @"token\s*[:=]\s*['""][^'""]+['""]"
        };

        var workflowJson = System.Text.Json.JsonSerializer.Serialize(workflow).ToLowerInvariant();

        foreach (var pattern in credentialPatterns)
        {
            if (Regex.IsMatch(workflowJson, pattern, RegexOptions.IgnoreCase))
            {
                AddViolation("security-001",
                    "Possible hardcoded credential detected",
                    fixSuggestion: "Use environment variables: ${env:MY_PASSWORD}");
            }
        }

        // Check for HTTP (non-HTTPS) URLs
        if (workflow.Steps != null)
        {
            foreach (var step in workflow.Steps)
            {
                if (step.Type == "http" && !string.IsNullOrWhiteSpace(step.Url))
                {
                    if (step.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !step.Url.Contains("localhost") &&
                        !step.Url.Contains("127.0.0.1"))
                    {
                        AddViolation("security-002",
                            $"Step '{step.Id}' uses HTTP instead of HTTPS",
                            location: $"Step {step.Id}",
                            codeSnippet: step.Url,
                            fixSuggestion: "Change to HTTPS if the server supports it");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks maintainability issues.
    /// </summary>
    private void CheckMaintainability(WorkflowDefinition workflow)
    {
        // Check workflow size
        if (workflow.Steps != null && workflow.Steps.Count > 50)
        {
            AddViolation("maint-001",
                $"Workflow has {workflow.Steps.Count} steps (recommended: < 50)",
                fixSuggestion: "Consider breaking into smaller workflows or using includes");
        }

        // Check for unused variables
        if (workflow.Variables != null && workflow.Variables.Count > 0)
        {
            var workflowJson = System.Text.Json.JsonSerializer.Serialize(workflow);

            foreach (var variable in workflow.Variables.Keys)
            {
                bool isUsed = workflowJson.Contains($"${{var:{variable}}}") ||
                              workflowJson.Contains($"${{ctx:{variable}}}");

                if (!isUsed)
                {
                    AddViolation("maint-002",
                        $"Variable '{variable}' is defined but never used",
                        fixSuggestion: $"Remove variable '{variable}' or add a reference to it");
                }
            }
        }

        // Check for magic numbers/values in URLs or messages
        if (workflow.Steps != null)
        {
            foreach (var step in workflow.Steps)
            {
                // Check for repeated URLs
                var sameUrlCount = workflow.Steps.Count(s => s.Url == step.Url && !string.IsNullOrWhiteSpace(step.Url));
                if (sameUrlCount > 2)
                {
                    AddViolation("maint-003",
                        $"URL '{step.Url}' is repeated {sameUrlCount} times",
                        location: $"Step {step.Id}",
                        fixSuggestion: "Extract to a variable: 'baseUrl' or similar");
                }
            }
        }
    }

    /// <summary>
    /// Adds a violation to the list.
    /// </summary>
    private void AddViolation(string ruleId, string message, string? location = null, string? codeSnippet = null, string? fixSuggestion = null)
    {
        var rule = _rules.FirstOrDefault(r => r.Id == ruleId);
        if (rule == null || !rule.Enabled) return;

        _violations.Add(new LintViolation
        {
            Rule = rule,
            Message = message,
            Location = location,
            CodeSnippet = codeSnippet,
            FixSuggestion = fixSuggestion
        });
    }

    /// <summary>
    /// Checks if a string is in kebab-case format.
    /// </summary>
    private bool IsKebabCase(string value)
    {
        return Regex.IsMatch(value, @"^[a-z0-9]+(-[a-z0-9]+)*$");
    }

    /// <summary>
    /// Converts a string to kebab-case.
    /// </summary>
    private string ToKebabCase(string value)
    {
        // Convert spaces and underscores to hyphens
        value = Regex.Replace(value, @"[\s_]+", "-");

        // Insert hyphen before capital letters
        value = Regex.Replace(value, @"([a-z0-9])([A-Z])", "$1-$2");

        // Convert to lowercase
        value = value.ToLowerInvariant();

        // Remove consecutive hyphens
        value = Regex.Replace(value, @"-+", "-");

        // Remove leading/trailing hyphens
        return value.Trim('-');
    }

    /// <summary>
    /// Generates a formatted lint report.
    /// </summary>
    public static string GenerateLintReport(LintReport report)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine($"║ WORKFLOW LINT REPORT                                                          ║");
        sb.AppendLine("╠═══════════════════════════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║ Workflow: {report.WorkflowName,-67} ║");
        sb.AppendLine($"║ ID: {report.WorkflowId,-73} ║");
        sb.AppendLine($"║ Linted: {report.LintedAt:yyyy-MM-dd HH:mm:ss UTC}                                         ║");
        sb.AppendLine($"║ Rules checked: {report.RulesChecked,-63} ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        if (report.Violations.Count == 0)
        {
            sb.AppendLine("✅ No violations found! This workflow follows all best practices.");
            sb.AppendLine();
            return sb.ToString();
        }

        // Summary
        sb.AppendLine($"Found {report.Violations.Count} violation(s):");
        sb.AppendLine();

        var grouped = report.Violations.GroupBy(v => v.Rule.Severity).OrderByDescending(g => g.Key);
        foreach (var group in grouped)
        {
            var icon = WorkflowHealthChecker.GetSeverityIcon(group.Key);
            sb.AppendLine($"{icon} {group.Key}: {group.Count()}");
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine();

        // Detailed violations
        foreach (var violation in report.Violations.OrderByDescending(v => v.Rule.Severity))
        {
            var icon = WorkflowHealthChecker.GetSeverityIcon(violation.Rule.Severity);
            sb.AppendLine($"{icon} [{violation.Rule.Severity}] {violation.Rule.Name} ({violation.Rule.Id})");
            sb.AppendLine($"   {violation.Message}");

            if (!string.IsNullOrEmpty(violation.Location))
                sb.AppendLine($"   Location: {violation.Location}");

            if (!string.IsNullOrEmpty(violation.CodeSnippet))
                sb.AppendLine($"   Code: {violation.CodeSnippet}");

            if (!string.IsNullOrEmpty(violation.FixSuggestion))
                sb.AppendLine($"   🔧 Fix: {violation.FixSuggestion}");

            sb.AppendLine();
        }

        // Summary recommendation
        if (report.HasCriticalViolations)
        {
            sb.AppendLine("⚠️  CRITICAL ISSUES FOUND - These must be fixed before deploying to production.");
        }
        else if (report.HasErrors)
        {
            sb.AppendLine("⚠️  ERRORS FOUND - Consider fixing these issues to improve workflow quality.");
        }
        else
        {
            sb.AppendLine("💡 Minor issues found - Consider addressing these for better maintainability.");
        }

        sb.AppendLine();
        return sb.ToString();
    }
}
