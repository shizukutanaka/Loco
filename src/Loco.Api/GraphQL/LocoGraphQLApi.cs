// Phase 6: GraphQL API for Mobile & External Integrations
// Complete GraphQL schema, resolvers, and subscriptions for external clients
// Enables real-time updates, complex queries, and simplified mobile integrations

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Api.GraphQL;

/// <summary>
/// GraphQL Query Root Types
/// </summary>
public class WorkflowQuery
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int ExecutionCount { get; set; }
    public double SuccessRate { get; set; }
    public List<StepQuery>? Steps { get; set; }
    public List<ExecutionQuery>? RecentExecutions { get; set; }
}

/// <summary>
/// GraphQL Step Query Type
/// </summary>
public class StepQuery
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object>? Configuration { get; set; }
    public int? RetryCount { get; set; }
    public int? TimeoutSeconds { get; set; }
}

/// <summary>
/// GraphQL Execution Query Type
/// </summary>
public class ExecutionQuery
{
    public string Id { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // running, completed, failed
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long DurationMs { get; set; }
    public Dictionary<string, object>? Input { get; set; }
    public Dictionary<string, object>? Output { get; set; }
    public string? ErrorMessage { get; set; }
    public List<StepExecutionQuery>? StepExecutions { get; set; }
}

/// <summary>
/// GraphQL Step Execution Query Type
/// </summary>
public class StepExecutionQuery
{
    public string StepId { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public int AttemptNumber { get; set; }
    public Dictionary<string, object>? Output { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// GraphQL Metrics Query Type
/// </summary>
public class MetricsQuery
{
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double SuccessRate { get; set; }
    public long AverageDurationMs { get; set; }
    public long P95DurationMs { get; set; }
    public long P99DurationMs { get; set; }
    public Dictionary<string, int>? ExecutionsByStatus { get; set; }
    public Dictionary<string, double>? ExecutionsByWorkflow { get; set; }
}

/// <summary>
/// GraphQL Mutation Input Types
/// </summary>
public class CreateWorkflowInput
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<StepInputType>? Steps { get; set; }
}

/// <summary>
/// GraphQL Step Input Type
/// </summary>
public class StepInputType
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object>? Configuration { get; set; }
}

/// <summary>
/// GraphQL Execution Input Type
/// </summary>
public class ExecuteWorkflowInput
{
    public string WorkflowId { get; set; } = string.Empty;
    public Dictionary<string, object>? Input { get; set; }
    public int? TimeoutSeconds { get; set; }
}

/// <summary>
/// GraphQL API Interface
/// </summary>
public interface ILocoGraphQLApi
{
    // Query operations
    Task<WorkflowQuery?> GetWorkflowAsync(
        string workflowId,
        CancellationToken ct = default);

    Task<List<WorkflowQuery>> ListWorkflowsAsync(
        int limit = 50,
        int offset = 0,
        string? status = null,
        CancellationToken ct = default);

    Task<ExecutionQuery?> GetExecutionAsync(
        string executionId,
        CancellationToken ct = default);

    Task<List<ExecutionQuery>> ListExecutionsAsync(
        string? workflowId = null,
        string? status = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 50,
        CancellationToken ct = default);

    Task<MetricsQuery> GetMetricsAsync(
        string? workflowId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);

    // Mutation operations
    Task<WorkflowQuery> CreateWorkflowAsync(
        CreateWorkflowInput input,
        CancellationToken ct = default);

    Task<WorkflowQuery> UpdateWorkflowAsync(
        string workflowId,
        CreateWorkflowInput input,
        CancellationToken ct = default);

    Task<bool> DeleteWorkflowAsync(
        string workflowId,
        CancellationToken ct = default);

    Task<ExecutionQuery> ExecuteWorkflowAsync(
        ExecuteWorkflowInput input,
        CancellationToken ct = default);

    Task<bool> CancelExecutionAsync(
        string executionId,
        CancellationToken ct = default);

    // Subscription setup
    IAsyncEnumerable<ExecutionQuery> SubscribeToExecutionAsync(
        string executionId,
        CancellationToken ct = default);

    IAsyncEnumerable<WorkflowQuery> SubscribeToWorkflowChangesAsync(
        string workflowId,
        CancellationToken ct = default);
}

/// <summary>
/// GraphQL API Implementation
/// </summary>
public class LocoGraphQLApi : ILocoGraphQLApi
{
    private readonly ILogger<LocoGraphQLApi> _logger;
    private readonly Dictionary<string, WorkflowQuery> _workflows;
    private readonly Dictionary<string, ExecutionQuery> _executions;
    private readonly Dictionary<string, List<DateTime>> _executionTimestamps;

    public LocoGraphQLApi(ILogger<LocoGraphQLApi> logger)
    {
        _logger = logger;
        _workflows = new Dictionary<string, WorkflowQuery>();
        _executions = new Dictionary<string, ExecutionQuery>();
        _executionTimestamps = new Dictionary<string, List<DateTime>>();
    }

    // Queries
    public async Task<WorkflowQuery?> GetWorkflowAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _workflows.TryGetValue(workflowId, out var workflow);
        return workflow;
    }

    public async Task<List<WorkflowQuery>> ListWorkflowsAsync(
        int limit = 50,
        int offset = 0,
        string? status = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var results = _workflows.Values
            .Where(w => status == null || w.Status == status)
            .OrderByDescending(w => w.UpdatedAt)
            .Skip(offset)
            .Take(limit)
            .ToList();

        _logger.LogDebug(
            "Listed {Count} workflows, limit: {Limit}, offset: {Offset}",
            results.Count, limit, offset);

        return results;
    }

    public async Task<ExecutionQuery?> GetExecutionAsync(
        string executionId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _executions.TryGetValue(executionId, out var execution);
        return execution;
    }

    public async Task<List<ExecutionQuery>> ListExecutionsAsync(
        string? workflowId = null,
        string? status = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var results = _executions.Values
            .Where(e => workflowId == null || e.WorkflowId == workflowId)
            .Where(e => status == null || e.Status == status)
            .Where(e => from == null || e.StartedAt >= from)
            .Where(e => to == null || e.StartedAt <= to)
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .ToList();

        return results;
    }

    public async Task<MetricsQuery> GetMetricsAsync(
        string? workflowId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate aggregation

        var executions = _executions.Values
            .Where(e => workflowId == null || e.WorkflowId == workflowId)
            .Where(e => from == null || e.StartedAt >= from)
            .Where(e => to == null || e.StartedAt <= to)
            .ToList();

        if (executions.Count == 0)
        {
            return new MetricsQuery();
        }

        var successful = executions.Count(e => e.Status == "completed");
        var failed = executions.Count(e => e.Status == "failed");
        var durations = executions.Where(e => e.CompletedAt.HasValue)
            .Select(e => e.DurationMs)
            .OrderBy(d => d)
            .ToList();

        return new MetricsQuery
        {
            TotalExecutions = executions.Count,
            SuccessfulExecutions = successful,
            FailedExecutions = failed,
            SuccessRate = successful / (double)executions.Count,
            AverageDurationMs = (long)durations.Average(),
            P95DurationMs = durations.Count > 0 ? durations[(int)(durations.Count * 0.95)] : 0,
            P99DurationMs = durations.Count > 0 ? durations[(int)(durations.Count * 0.99)] : 0,
        };
    }

    // Mutations
    public async Task<WorkflowQuery> CreateWorkflowAsync(
        CreateWorkflowInput input,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var workflow = new WorkflowQuery
        {
            Id = Guid.NewGuid().ToString(),
            Name = input.Name,
            Description = input.Description,
            Status = "active",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Steps = input.Steps?.Select(s => new StepQuery
            {
                Id = s.Id,
                Name = s.Name,
                Type = s.Type,
                Configuration = s.Configuration,
            }).ToList() ?? new List<StepQuery>(),
        };

        _workflows[workflow.Id] = workflow;

        _logger.LogInformation(
            "Workflow created via GraphQL: {WorkflowId} ({Name})",
            workflow.Id, workflow.Name);

        return workflow;
    }

    public async Task<WorkflowQuery> UpdateWorkflowAsync(
        string workflowId,
        CreateWorkflowInput input,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_workflows.TryGetValue(workflowId, out var workflow))
        {
            throw new KeyNotFoundException($"Workflow not found: {workflowId}");
        }

        workflow.Name = input.Name;
        workflow.Description = input.Description;
        workflow.UpdatedAt = DateTime.UtcNow;
        workflow.Version++;
        workflow.Steps = input.Steps?.Select(s => new StepQuery
        {
            Id = s.Id,
            Name = s.Name,
            Type = s.Type,
            Configuration = s.Configuration,
        }).ToList() ?? new List<StepQuery>();

        _logger.LogInformation(
            "Workflow updated via GraphQL: {WorkflowId}",
            workflowId);

        return workflow;
    }

    public async Task<bool> DeleteWorkflowAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var removed = _workflows.Remove(workflowId);

        if (removed)
        {
            _logger.LogInformation(
                "Workflow deleted via GraphQL: {WorkflowId}",
                workflowId);
        }

        return removed;
    }

    public async Task<ExecutionQuery> ExecuteWorkflowAsync(
        ExecuteWorkflowInput input,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var execution = new ExecutionQuery
        {
            Id = Guid.NewGuid().ToString(),
            WorkflowId = input.WorkflowId,
            Status = "running",
            StartedAt = DateTime.UtcNow,
            Input = input.Input,
            StepExecutions = new List<StepExecutionQuery>(),
        };

        _executions[execution.Id] = execution;

        // Track timestamp for metrics
        if (!_executionTimestamps.ContainsKey(input.WorkflowId))
        {
            _executionTimestamps[input.WorkflowId] = new List<DateTime>();
        }
        _executionTimestamps[input.WorkflowId].Add(DateTime.UtcNow);

        _logger.LogInformation(
            "Workflow execution started via GraphQL: {ExecutionId}, Workflow: {WorkflowId}",
            execution.Id, input.WorkflowId);

        // Simulate execution completion
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            if (_executions.TryGetValue(execution.Id, out var exec))
            {
                exec.Status = "completed";
                exec.CompletedAt = DateTime.UtcNow;
                exec.DurationMs = (long)(exec.CompletedAt.Value - exec.StartedAt).TotalMilliseconds;
            }
        });

        return execution;
    }

    public async Task<bool> CancelExecutionAsync(
        string executionId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_executions.TryGetValue(executionId, out var execution))
        {
            if (execution.Status == "running")
            {
                execution.Status = "cancelled";
                execution.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation(
                    "Execution cancelled via GraphQL: {ExecutionId}",
                    executionId);
                return true;
            }
        }

        return false;
    }

    // Subscriptions (for real-time updates)
    public async IAsyncEnumerable<ExecutionQuery> SubscribeToExecutionAsync(
        string executionId,
        CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_executions.TryGetValue(executionId, out var execution))
            {
                yield return execution;

                if (execution.Status == "completed" || execution.Status == "failed" || execution.Status == "cancelled")
                {
                    break;
                }
            }

            await Task.Delay(500, ct);
        }
    }

    public async IAsyncEnumerable<WorkflowQuery> SubscribeToWorkflowChangesAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        var lastVersion = 0;

        while (!ct.IsCancellationRequested)
        {
            if (_workflows.TryGetValue(workflowId, out var workflow))
            {
                if (workflow.Version > lastVersion)
                {
                    yield return workflow;
                    lastVersion = workflow.Version;
                }
            }

            await Task.Delay(1000, ct);
        }
    }
}

/// <summary>
/// GraphQL Schema Builder
/// </summary>
public static class GraphQLSchemaBuilder
{
    public static string BuildSchema()
    {
        return @"
type Query {
  workflow(id: ID!): Workflow
  workflows(limit: Int, offset: Int, status: String): [Workflow!]!
  execution(id: ID!): Execution
  executions(workflowId: ID, status: String, from: DateTime, to: DateTime, limit: Int): [Execution!]!
  metrics(workflowId: ID, from: DateTime, to: DateTime): Metrics!
}

type Mutation {
  createWorkflow(input: CreateWorkflowInput!): Workflow!
  updateWorkflow(id: ID!, input: CreateWorkflowInput!): Workflow!
  deleteWorkflow(id: ID!): Boolean!
  executeWorkflow(input: ExecuteWorkflowInput!): Execution!
  cancelExecution(id: ID!): Boolean!
}

subscription {
  executionUpdated(id: ID!): Execution!
  workflowChanged(id: ID!): Workflow!
}

type Workflow {
  id: ID!
  name: String!
  description: String
  status: String!
  version: Int!
  createdAt: DateTime!
  updatedAt: DateTime!
  executionCount: Int!
  successRate: Float!
  steps: [Step!]
  recentExecutions: [Execution!]
}

type Step {
  id: ID!
  name: String!
  type: String!
  configuration: JSON
  retryCount: Int
  timeoutSeconds: Int
}

type Execution {
  id: ID!
  workflowId: ID!
  status: String!
  startedAt: DateTime!
  completedAt: DateTime
  durationMs: Long!
  input: JSON
  output: JSON
  errorMessage: String
  stepExecutions: [StepExecution!]
}

type StepExecution {
  stepId: ID!
  stepName: String!
  status: String!
  durationMs: Long!
  attemptNumber: Int!
  output: JSON
  errorMessage: String
}

type Metrics {
  totalExecutions: Int!
  successfulExecutions: Int!
  failedExecutions: Int!
  successRate: Float!
  averageDurationMs: Long!
  p95DurationMs: Long!
  p99DurationMs: Long!
  executionsByStatus: [MetricEntry!]
  executionsByWorkflow: [MetricEntry!]
}

type MetricEntry {
  key: String!
  value: Float!
}

input CreateWorkflowInput {
  name: String!
  description: String
  steps: [StepInput!]
}

input StepInput {
  id: ID!
  name: String!
  type: String!
  configuration: JSON
}

input ExecuteWorkflowInput {
  workflowId: ID!
  input: JSON
  timeoutSeconds: Int
}

scalar DateTime
scalar JSON
scalar Long
";
    }
}
