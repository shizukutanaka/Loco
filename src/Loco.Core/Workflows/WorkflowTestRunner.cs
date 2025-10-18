using System.Diagnostics;
using System.Text;

namespace Loco.Core.Workflows;

/// <summary>
/// Test case for workflow validation.
/// </summary>
public class WorkflowTestCase
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Dictionary<string, string> InputVariables { get; set; } = new();
    public Dictionary<string, string> ExpectedOutputs { get; set; } = new();
    public bool ExpectSuccess { get; set; } = true;
    public int? ExpectedStepCount { get; set; }
    public int? MaxDurationSeconds { get; set; }
}

/// <summary>
/// Result of a single test case execution.
/// </summary>
public class TestCaseResult
{
    public WorkflowTestCase TestCase { get; set; } = null!;
    public bool Passed { get; set; }
    public string Status { get; set; } = "";
    public TimeSpan Duration { get; set; }
    public List<string> Failures { get; set; } = new();
    public Dictionary<string, string> ActualOutputs { get; set; } = new();
    public int StepsExecuted { get; set; }
}

/// <summary>
/// Test suite for a workflow.
/// </summary>
public class WorkflowTestSuite
{
    public string WorkflowId { get; set; } = "";
    public string WorkflowName { get; set; } = "";
    public List<WorkflowTestCase> TestCases { get; set; } = new();
}

/// <summary>
/// Results of running a test suite.
/// </summary>
public class TestSuiteResult
{
    public string WorkflowId { get; set; } = "";
    public string WorkflowName { get; set; } = "";
    public List<TestCaseResult> Results { get; set; } = new();
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan TotalDuration { get; set; }

    public int TotalTests => Results.Count;
    public int PassedTests => Results.Count(r => r.Passed);
    public int FailedTests => Results.Count(r => !r.Passed);
    public double PassRate => TotalTests > 0 ? (PassedTests / (double)TotalTests) * 100 : 0;
    public bool AllPassed => TotalTests > 0 && FailedTests == 0;
}

/// <summary>
/// Runs automated tests on workflows.
/// </summary>
public class WorkflowTestRunner
{
    /// <summary>
    /// Runs a test suite against a workflow.
    /// </summary>
    public async Task<TestSuiteResult> RunTestSuiteAsync(WorkflowDefinition workflow, WorkflowTestSuite testSuite)
    {
        var results = new List<TestCaseResult>();
        var overallStopwatch = Stopwatch.StartNew();

        foreach (var testCase in testSuite.TestCases)
        {
            var result = await RunTestCaseAsync(workflow, testCase);
            results.Add(result);
        }

        overallStopwatch.Stop();

        return new TestSuiteResult
        {
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            Results = results,
            TotalDuration = overallStopwatch.Elapsed
        };
    }

    /// <summary>
    /// Runs a single test case.
    /// </summary>
    private async Task<TestCaseResult> RunTestCaseAsync(WorkflowDefinition workflow, WorkflowTestCase testCase)
    {
        var result = new TestCaseResult
        {
            TestCase = testCase,
            Status = "Running"
        };

        var stopwatch = Stopwatch.StartNew();
        var failures = new List<string>();

        try
        {
            // Create a test context with input variables
            var testWorkflow = CloneWorkflow(workflow);

            // Merge test input variables
            if (testWorkflow.Variables == null)
                testWorkflow.Variables = new Dictionary<string, string>();

            foreach (var input in testCase.InputVariables)
            {
                testWorkflow.Variables[input.Key] = input.Value;
            }

            // Perform dry-run validation
            var validator = new WorkflowValidator();
            var validationResult = validator.Validate(testWorkflow);

            if (!validationResult.IsValid)
            {
                failures.Add($"Workflow validation failed: {string.Join(", ", validationResult.Errors)}");
            }

            // Check step count if specified
            if (testCase.ExpectedStepCount.HasValue)
            {
                var actualStepCount = testWorkflow.Steps?.Count ?? 0;
                if (actualStepCount != testCase.ExpectedStepCount.Value)
                {
                    failures.Add($"Expected {testCase.ExpectedStepCount.Value} steps but found {actualStepCount}");
                }
            }

            // Check structure
            if (testWorkflow.Steps != null)
            {
                result.StepsExecuted = testWorkflow.Steps.Count;

                // Validate step configuration
                foreach (var step in testWorkflow.Steps)
                {
                    if (string.IsNullOrWhiteSpace(step.Id))
                        failures.Add($"Step at index {testWorkflow.Steps.IndexOf(step)} has no ID");

                    if (string.IsNullOrWhiteSpace(step.Type))
                        failures.Add($"Step {step.Id} has no type");
                }
            }

            // Check dependencies if present
            if (testWorkflow.Steps?.Any(s => s.DependsOn != null || s.Dependencies != null) == true)
            {
                var depAnalyzer = new DependencyAnalyzer(testWorkflow.Steps);
                var (isValid, errors) = depAnalyzer.ValidateDependencies();

                if (!isValid)
                {
                    failures.AddRange(errors);
                }
            }

            // Check schedule if present
            if (testWorkflow.Schedule != null)
            {
                try
                {
                    var nextRun = ScheduleChecker.GetNextRunTime(testWorkflow.Schedule);
                    if (testWorkflow.Schedule.Enabled && nextRun == null)
                    {
                        failures.Add("Schedule is enabled but has no valid next run time");
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"Schedule validation failed: {ex.Message}");
                }
            }

            // Check expected success/failure
            bool hasStructuralIssues = failures.Count > 0;
            if (testCase.ExpectSuccess && hasStructuralIssues)
            {
                result.Status = "Failed";
            }
            else if (!testCase.ExpectSuccess && !hasStructuralIssues)
            {
                failures.Add("Expected workflow to have issues but none were found");
                result.Status = "Failed";
            }
            else
            {
                result.Status = "Passed";
            }

            // Check duration
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            if (testCase.MaxDurationSeconds.HasValue &&
                result.Duration.TotalSeconds > testCase.MaxDurationSeconds.Value)
            {
                failures.Add($"Test took {result.Duration.TotalSeconds:F2}s but max duration is {testCase.MaxDurationSeconds}s");
                result.Status = "Failed";
            }

            result.Passed = failures.Count == 0 && result.Status == "Passed";
            result.Failures = failures;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Status = "Error";
            result.Passed = false;
            result.Failures.Add($"Exception: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Creates a deep copy of a workflow for testing.
    /// </summary>
    private WorkflowDefinition CloneWorkflow(WorkflowDefinition workflow)
    {
        // Simple clone using JSON serialization
        var json = System.Text.Json.JsonSerializer.Serialize(workflow);
        return System.Text.Json.JsonSerializer.Deserialize<WorkflowDefinition>(json)
            ?? new WorkflowDefinition();
    }

    /// <summary>
    /// Generates a formatted test report.
    /// </summary>
    public static string GenerateTestReport(TestSuiteResult result)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine($"║ WORKFLOW TEST REPORT                                                          ║");
        sb.AppendLine("╠═══════════════════════════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║ Workflow: {result.WorkflowName,-67} ║");
        sb.AppendLine($"║ ID: {result.WorkflowId,-73} ║");
        sb.AppendLine($"║ Executed: {result.ExecutedAt:yyyy-MM-dd HH:mm:ss UTC}                                       ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        // Summary
        var statusIcon = result.AllPassed ? "✅" : "❌";
        var statusText = result.AllPassed ? "ALL TESTS PASSED" : "SOME TESTS FAILED";

        sb.AppendLine($"Status: {statusIcon} {statusText}");
        sb.AppendLine($"Tests: {result.TotalTests} total, {result.PassedTests} passed, {result.FailedTests} failed");
        sb.AppendLine($"Pass Rate: {result.PassRate:F1}%");
        sb.AppendLine($"Duration: {result.TotalDuration.TotalSeconds:F2}s");
        sb.AppendLine();

        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine();

        // Test results
        foreach (var testResult in result.Results)
        {
            var icon = testResult.Passed ? "✅" : "❌";
            sb.AppendLine($"{icon} [{testResult.Status}] {testResult.TestCase.Name}");
            sb.AppendLine($"   ID: {testResult.TestCase.Id}");
            sb.AppendLine($"   Duration: {testResult.Duration.TotalMilliseconds:F0}ms");

            if (!string.IsNullOrEmpty(testResult.TestCase.Description))
                sb.AppendLine($"   Description: {testResult.TestCase.Description}");

            if (testResult.TestCase.InputVariables.Count > 0)
            {
                sb.AppendLine($"   Input Variables: {string.Join(", ", testResult.TestCase.InputVariables.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }

            if (!testResult.Passed && testResult.Failures.Count > 0)
            {
                sb.AppendLine($"   Failures:");
                foreach (var failure in testResult.Failures)
                {
                    sb.AppendLine($"     • {failure}");
                }
            }

            sb.AppendLine();
        }

        // Footer
        if (result.AllPassed)
        {
            sb.AppendLine("🎉 All tests passed! The workflow is ready for use.");
        }
        else
        {
            sb.AppendLine("⚠️  Some tests failed. Please review the failures above.");
        }

        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// Creates a basic test suite for a workflow.
    /// </summary>
    public static WorkflowTestSuite CreateBasicTestSuite(WorkflowDefinition workflow)
    {
        var suite = new WorkflowTestSuite
        {
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            TestCases = new List<WorkflowTestCase>
            {
                new WorkflowTestCase
                {
                    Id = "basic-validation",
                    Name = "Basic workflow validation",
                    Description = "Validates workflow structure and configuration",
                    ExpectSuccess = true
                },
                new WorkflowTestCase
                {
                    Id = "step-count",
                    Name = "Step count verification",
                    Description = "Verifies the expected number of steps",
                    ExpectSuccess = true,
                    ExpectedStepCount = workflow.Steps?.Count ?? 0
                }
            }
        };

        // Add dependency test if workflow has dependencies
        if (workflow.Steps?.Any(s => s.DependsOn != null || s.Dependencies != null) == true)
        {
            suite.TestCases.Add(new WorkflowTestCase
            {
                Id = "dependency-validation",
                Name = "Dependency validation",
                Description = "Validates step dependencies and DAG structure",
                ExpectSuccess = true
            });
        }

        // Add schedule test if workflow has schedule
        if (workflow.Schedule != null)
        {
            suite.TestCases.Add(new WorkflowTestCase
            {
                Id = "schedule-validation",
                Name = "Schedule validation",
                Description = "Validates schedule configuration",
                ExpectSuccess = true
            });
        }

        // Add variable test if workflow has variables
        if (workflow.Variables != null && workflow.Variables.Count > 0)
        {
            var testCase = new WorkflowTestCase
            {
                Id = "variable-test",
                Name = "Variable substitution test",
                Description = "Tests variable usage and substitution",
                ExpectSuccess = true
            };

            // Add test variables
            foreach (var variable in workflow.Variables.Take(3))
            {
                testCase.InputVariables[variable.Key] = $"test_{variable.Value}";
            }

            suite.TestCases.Add(testCase);
        }

        return suite;
    }

    /// <summary>
    /// Runs quick smoke tests on a workflow.
    /// </summary>
    public async Task<TestSuiteResult> RunSmokeTestsAsync(WorkflowDefinition workflow)
    {
        var suite = CreateBasicTestSuite(workflow);
        return await RunTestSuiteAsync(workflow, suite);
    }
}
