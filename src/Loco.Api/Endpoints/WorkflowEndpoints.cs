// Phase 3: Extended Minimal API Endpoints for Workflow Management
// High-performance endpoints with caching and optimizations

using Loco.Core.Data;
using Loco.Core.DataAccess;
using Loco.Core.Execution;
using Loco.Core.Workflows.DurableExecution;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Loco.Api.Endpoints;

/// <summary>
/// Extended Minimal API endpoints for Workflow management
/// Replaces controller-based approach for better performance
/// </summary>
public static class WorkflowEndpoints
{
    public static void MapWorkflowEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/workflows")
            .WithTags("Workflows")
            .WithOpenApi()
            .RequireAuthorization();

        // List workflows with pagination and filtering
        group.MapGet("/", GetWorkflows)
            .WithName("ListWorkflows")
            .Produces<PaginatedResponse<WorkflowSummary>>(StatusCodes.Status200OK)
            .WithOpenApi();

        // Get workflow by ID
        group.MapGet("/{workflowId}", GetWorkflowById)
            .WithName("GetWorkflow")
            .Produces<WorkflowDetailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi();

        // Get workflow status (lightweight)
        group.MapGet("/{workflowId}/status", GetWorkflowStatus)
            .WithName("GetWorkflowStatus")
            .Produces<WorkflowStatusResponse>(StatusCodes.Status200OK)
            .WithOpenApi();

        // Create workflow
        group.MapPost("/", CreateWorkflow)
            .WithName("CreateWorkflow")
            .Produces<WorkflowDetailResponse>(StatusCodes.Status201Created)
            .Produces<ValidationErrorResponse>(StatusCodes.Status400BadRequest)
            .WithOpenApi();

        // Update workflow
        group.MapPut("/{workflowId}", UpdateWorkflow)
            .WithName("UpdateWorkflow")
            .Produces<WorkflowDetailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi();

        // Delete workflow
        group.MapDelete("/{workflowId}", DeleteWorkflow)
            .WithName("DeleteWorkflow")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi();

        // Get workflow execution history
        group.MapGet("/{workflowId}/executions", GetWorkflowExecutions)
            .WithName("GetWorkflowExecutions")
            .Produces<PaginatedResponse<ExecutionSummary>>(StatusCodes.Status200OK)
            .WithOpenApi();

        // Get execution details
        group.MapGet("/{workflowId}/executions/{executionId}", GetExecutionDetails)
            .WithName("GetExecutionDetails")
            .Produces<ExecutionDetailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi();

        // Get workflow metrics/analytics
        group.MapGet("/{workflowId}/metrics", GetWorkflowMetrics)
            .WithName("GetWorkflowMetrics")
            .Produces<WorkflowMetricsResponse>(StatusCodes.Status200OK)
            .WithOpenApi();

        // Bulk operations
        group.MapPost("/bulk-enable", BulkEnableWorkflows)
            .WithName("BulkEnableWorkflows")
            .Produces<BulkOperationResponse>(StatusCodes.Status200OK)
            .WithOpenApi();

        group.MapPost("/bulk-disable", BulkDisableWorkflows)
            .WithName("BulkDisableWorkflows")
            .Produces<BulkOperationResponse>(StatusCodes.Status200OK)
            .WithOpenApi();

        // Search workflows
        group.MapGet("/search", SearchWorkflows)
            .WithName("SearchWorkflows")
            .Produces<PaginatedResponse<WorkflowSummary>>(StatusCodes.Status200OK)
            .WithOpenApi();
    }

    /// <summary>
    /// Get paginated list of workflows
    /// </summary>
    private static async Task<Ok<PaginatedResponse<WorkflowSummary>>> GetWorkflows(
        IWorkflowRepository workflowRepo,
        ILogger<WorkflowEndpoints> logger,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "limit")] int limit = 50,
        [FromQuery(Name = "sort")] string sort = "updated_desc")
    {
        try
        {
            var workflows = await workflowRepo.GetAllAsync();

            // Apply sorting
            var sorted = sort switch
            {
                "updated_asc" => workflows.OrderBy(w => w.UpdatedAt),
                "updated_desc" => workflows.OrderByDescending(w => w.UpdatedAt),
                "name_asc" => workflows.OrderBy(w => w.Name),
                "name_desc" => workflows.OrderByDescending(w => w.Name),
                "created_asc" => workflows.OrderBy(w => w.CreatedAt),
                "created_desc" => workflows.OrderByDescending(w => w.CreatedAt),
                _ => workflows.OrderByDescending(w => w.UpdatedAt),
            };

            var skip = (page - 1) * limit;
            var total = sorted.Count();
            var items = sorted.Skip(skip).Take(limit).Select(w => new WorkflowSummary
            {
                Id = w.Id,
                Name = w.Name,
                Description = w.Description,
                IsActive = w.IsActive,
                UpdatedAt = w.UpdatedAt,
                CreatedAt = w.CreatedAt,
            });

            return TypedResults.Ok(new PaginatedResponse<WorkflowSummary>
            {
                Items = items,
                Total = total,
                Page = page,
                Limit = limit,
                TotalPages = (int)Math.Ceiling((double)total / limit),
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting workflows");
            throw;
        }
    }

    /// <summary>
    /// Get workflow by ID with full details
    /// </summary>
    private static async Task<IResult> GetWorkflowById(
        string workflowId,
        IWorkflowRepository workflowRepo,
        ILogger<WorkflowEndpoints> logger)
    {
        try
        {
            var workflow = await workflowRepo.GetByIdAsync(workflowId);
            if (workflow == null)
                return Results.NotFound();

            return Results.Ok(new WorkflowDetailResponse
            {
                Id = workflow.Id,
                Name = workflow.Name,
                Description = workflow.Description,
                Definition = workflow.Definition,
                IsActive = workflow.IsActive,
                Version = workflow.Version,
                CreatedAt = workflow.CreatedAt,
                UpdatedAt = workflow.UpdatedAt,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting workflow: {WorkflowId}", workflowId);
            throw;
        }
    }

    /// <summary>
    /// Get lightweight workflow status
    /// </summary>
    private static async Task<Ok<WorkflowStatusResponse>> GetWorkflowStatus(
        string workflowId,
        IWorkflowRepository workflowRepo,
        IExecutionHistoryRepository executionRepo)
    {
        var workflow = await workflowRepo.GetByIdAsync(workflowId);
        if (workflow == null)
            return TypedResults.Ok(new WorkflowStatusResponse { Status = "not_found" });

        var recentExecution = await executionRepo.GetRecentAsync(limit: 1);
        var lastExecution = recentExecution.FirstOrDefault();

        return TypedResults.Ok(new WorkflowStatusResponse
        {
            Id = workflowId,
            IsActive = workflow.IsActive,
            Status = workflow.IsActive ? "active" : "inactive",
            LastExecutionTime = lastExecution?.StartedAt,
            LastExecutionStatus = lastExecution?.Status.ToString(),
            TotalExecutions = await executionRepo.CountByWorkflowAsync(workflowId),
        });
    }

    /// <summary>
    /// Create new workflow
    /// </summary>
    private static async Task<IResult> CreateWorkflow(
        CreateWorkflowRequest request,
        IWorkflowRepository workflowRepo,
        ILogger<WorkflowEndpoints> logger)
    {
        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new ValidationErrorResponse { Message = "Workflow name is required" });

            var workflow = new WorkflowEntity
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Description = request.Description,
                Definition = request.Definition ?? "{}",
                IsActive = true,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            await workflowRepo.AddAsync(workflow);

            logger.LogInformation("Workflow created: {WorkflowId} ({Name})", workflow.Id, workflow.Name);

            return Results.Created($"/api/v1/workflows/{workflow.Id}", new WorkflowDetailResponse
            {
                Id = workflow.Id,
                Name = workflow.Name,
                Description = workflow.Description,
                Definition = workflow.Definition,
                IsActive = workflow.IsActive,
                Version = workflow.Version,
                CreatedAt = workflow.CreatedAt,
                UpdatedAt = workflow.UpdatedAt,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating workflow");
            throw;
        }
    }

    /// <summary>
    /// Update existing workflow
    /// </summary>
    private static async Task<IResult> UpdateWorkflow(
        string workflowId,
        UpdateWorkflowRequest request,
        IWorkflowRepository workflowRepo,
        ILogger<WorkflowEndpoints> logger)
    {
        try
        {
            var workflow = await workflowRepo.GetByIdAsync(workflowId);
            if (workflow == null)
                return Results.NotFound();

            workflow.Name = request.Name ?? workflow.Name;
            workflow.Description = request.Description ?? workflow.Description;
            if (!string.IsNullOrEmpty(request.Definition))
                workflow.Definition = request.Definition;
            if (request.IsActive.HasValue)
                workflow.IsActive = request.IsActive.Value;

            workflow.Version++;
            workflow.UpdatedAt = DateTime.UtcNow;

            await workflowRepo.UpdateAsync(workflow);

            logger.LogInformation("Workflow updated: {WorkflowId}", workflowId);

            return Results.Ok(new WorkflowDetailResponse
            {
                Id = workflow.Id,
                Name = workflow.Name,
                Description = workflow.Description,
                Definition = workflow.Definition,
                IsActive = workflow.IsActive,
                Version = workflow.Version,
                CreatedAt = workflow.CreatedAt,
                UpdatedAt = workflow.UpdatedAt,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating workflow: {WorkflowId}", workflowId);
            throw;
        }
    }

    /// <summary>
    /// Delete workflow
    /// </summary>
    private static async Task<IResult> DeleteWorkflow(
        string workflowId,
        IWorkflowRepository workflowRepo,
        ILogger<WorkflowEndpoints> logger)
    {
        try
        {
            var deleted = await workflowRepo.DeleteAsync(workflowId);
            if (!deleted)
                return Results.NotFound();

            logger.LogInformation("Workflow deleted: {WorkflowId}", workflowId);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting workflow: {WorkflowId}", workflowId);
            throw;
        }
    }

    /// <summary>
    /// Get execution history for workflow
    /// </summary>
    private static async Task<Ok<PaginatedResponse<ExecutionSummary>>> GetWorkflowExecutions(
        string workflowId,
        IExecutionHistoryRepository executionRepo,
        ILogger<WorkflowEndpoints> logger,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "limit")] int limit = 50)
    {
        try
        {
            var executions = await executionRepo.GetByWorkflowAsync(workflowId);
            var sorted = executions.OrderByDescending(e => e.StartedAt);

            var skip = (page - 1) * limit;
            var total = sorted.Count();
            var items = sorted.Skip(skip).Take(limit).Select(e => new ExecutionSummary
            {
                Id = e.Id,
                WorkflowId = e.WorkflowId,
                Status = e.Status.ToString(),
                StartedAt = e.StartedAt,
                CompletedAt = e.CompletedAt,
                DurationMs = e.CompletedAt.HasValue
                    ? (long)(e.CompletedAt.Value - e.StartedAt).TotalMilliseconds
                    : null,
            });

            return TypedResults.Ok(new PaginatedResponse<ExecutionSummary>
            {
                Items = items,
                Total = total,
                Page = page,
                Limit = limit,
                TotalPages = (int)Math.Ceiling((double)total / limit),
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting executions for workflow: {WorkflowId}", workflowId);
            throw;
        }
    }

    /// <summary>
    /// Get execution details
    /// </summary>
    private static async Task<IResult> GetExecutionDetails(
        string workflowId,
        string executionId,
        IExecutionHistoryRepository executionRepo)
    {
        var execution = await executionRepo.GetByIdAsync(executionId);
        if (execution == null || execution.WorkflowId != workflowId)
            return Results.NotFound();

        return Results.Ok(new ExecutionDetailResponse
        {
            Id = execution.Id,
            WorkflowId = execution.WorkflowId,
            Status = execution.Status.ToString(),
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            Result = execution.Result,
            ErrorMessage = execution.ErrorMessage,
            Parameters = execution.Parameters,
        });
    }

    /// <summary>
    /// Get workflow analytics/metrics
    /// </summary>
    private static async Task<Ok<WorkflowMetricsResponse>> GetWorkflowMetrics(
        string workflowId,
        IExecutionHistoryRepository executionRepo)
    {
        var executions = await executionRepo.GetByWorkflowAsync(workflowId);
        var executionList = executions.ToList();

        var successful = executionList.Count(e => e.Status == ExecutionStatus.Success);
        var failed = executionList.Count(e => e.Status == ExecutionStatus.Failed);
        var totalDuration = executionList
            .Where(e => e.CompletedAt.HasValue)
            .Sum(e => (e.CompletedAt!.Value - e.StartedAt).TotalMilliseconds);

        var avgDuration = executionList.Count > 0
            ? totalDuration / executionList.Count(e => e.CompletedAt.HasValue)
            : 0;

        return TypedResults.Ok(new WorkflowMetricsResponse
        {
            WorkflowId = workflowId,
            TotalExecutions = executionList.Count,
            SuccessfulExecutions = successful,
            FailedExecutions = failed,
            SuccessRate = executionList.Count > 0 ? (double)successful / executionList.Count : 0,
            AverageDurationMs = avgDuration,
            LastExecutionTime = executionList.OrderByDescending(e => e.StartedAt).FirstOrDefault()?.StartedAt,
        });
    }

    /// <summary>
    /// Bulk enable workflows
    /// </summary>
    private static async Task<Ok<BulkOperationResponse>> BulkEnableWorkflows(
        BulkOperationRequest request,
        IWorkflowRepository workflowRepo,
        ILogger<WorkflowEndpoints> logger)
    {
        try
        {
            int updated = 0;
            foreach (var workflowId in request.WorkflowIds)
            {
                var workflow = await workflowRepo.GetByIdAsync(workflowId);
                if (workflow != null)
                {
                    workflow.IsActive = true;
                    workflow.UpdatedAt = DateTime.UtcNow;
                    await workflowRepo.UpdateAsync(workflow);
                    updated++;
                }
            }

            logger.LogInformation("Bulk enabled {Count} workflows", updated);

            return TypedResults.Ok(new BulkOperationResponse
            {
                TotalRequested = request.WorkflowIds.Count,
                TotalProcessed = updated,
                Success = true,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error bulk enabling workflows");
            throw;
        }
    }

    /// <summary>
    /// Bulk disable workflows
    /// </summary>
    private static async Task<Ok<BulkOperationResponse>> BulkDisableWorkflows(
        BulkOperationRequest request,
        IWorkflowRepository workflowRepo,
        ILogger<WorkflowEndpoints> logger)
    {
        try
        {
            int updated = 0;
            foreach (var workflowId in request.WorkflowIds)
            {
                var workflow = await workflowRepo.GetByIdAsync(workflowId);
                if (workflow != null)
                {
                    workflow.IsActive = false;
                    workflow.UpdatedAt = DateTime.UtcNow;
                    await workflowRepo.UpdateAsync(workflow);
                    updated++;
                }
            }

            logger.LogInformation("Bulk disabled {Count} workflows", updated);

            return TypedResults.Ok(new BulkOperationResponse
            {
                TotalRequested = request.WorkflowIds.Count,
                TotalProcessed = updated,
                Success = true,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error bulk disabling workflows");
            throw;
        }
    }

    /// <summary>
    /// Search workflows by name/description
    /// </summary>
    private static async Task<Ok<PaginatedResponse<WorkflowSummary>>> SearchWorkflows(
        IWorkflowRepository workflowRepo,
        [FromQuery(Name = "q")] string query,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "limit")] int limit = 50)
    {
        var workflows = await workflowRepo.GetAllAsync();
        var filtered = workflows.Where(w =>
            w.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            w.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
        );

        var skip = (page - 1) * limit;
        var total = filtered.Count();
        var items = filtered.Skip(skip).Take(limit).Select(w => new WorkflowSummary
        {
            Id = w.Id,
            Name = w.Name,
            Description = w.Description,
            IsActive = w.IsActive,
            UpdatedAt = w.UpdatedAt,
            CreatedAt = w.CreatedAt,
        });

        return TypedResults.Ok(new PaginatedResponse<WorkflowSummary>
        {
            Items = items,
            Total = total,
            Page = page,
            Limit = limit,
            TotalPages = (int)Math.Ceiling((double)total / limit),
        });
    }
}

// Request/Response DTOs

public class CreateWorkflowRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Definition { get; set; }
}

public class UpdateWorkflowRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Definition { get; set; }
    public bool? IsActive { get; set; }
}

public class BulkOperationRequest
{
    [Required]
    public List<string> WorkflowIds { get; set; } = new();
}

public class WorkflowSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WorkflowDetailResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Definition { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WorkflowStatusResponse
{
    public string? Id { get; set; }
    public bool IsActive { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastExecutionTime { get; set; }
    public string? LastExecutionStatus { get; set; }
    public int TotalExecutions { get; set; }
}

public class ExecutionSummary
{
    public string Id { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? DurationMs { get; set; }
}

public class ExecutionDetailResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Parameters { get; set; }
}

public class WorkflowMetricsResponse
{
    public string WorkflowId { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double SuccessRate { get; set; }
    public double AverageDurationMs { get; set; }
    public DateTime? LastExecutionTime { get; set; }
}

public class BulkOperationResponse
{
    public int TotalRequested { get; set; }
    public int TotalProcessed { get; set; }
    public bool Success { get; set; }
}

public class PaginatedResponse<T>
{
    [JsonPropertyName("items")]
    public IEnumerable<T> Items { get; set; } = new List<T>();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }
}

public class ValidationErrorResponse
{
    public string Message { get; set; } = string.Empty;
}
