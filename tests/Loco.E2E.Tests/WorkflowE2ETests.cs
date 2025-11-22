// Phase 3: End-to-End Testing Framework
// Comprehensive E2E tests for workflow management and OAuth authentication

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace Loco.E2E.Tests;

/// <summary>
/// End-to-End test class for workflow operations
/// Tests complete user journeys from API to database
/// </summary>
[Collection("E2E Test Collection")]
public class WorkflowE2ETests : IAsyncLifetime
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "http://localhost:5000";
    private string? _authToken;
    private const string TestUsername = "e2e-test-user";
    private const string TestPassword = "TestPassword123!@#";

    public WorkflowE2ETests()
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Loco-E2E-Tests/3.0.0");
    }

    /// <summary>
    /// Initialize test environment (setup)
    /// </summary>
    public async Task InitializeAsync()
    {
        // Wait for API to be ready
        await WaitForApiReady(TimeSpan.FromSeconds(30));

        // Authenticate for subsequent tests
        _authToken = await AuthenticateUser();
        Assert.NotNull(_authToken);
    }

    /// <summary>
    /// Cleanup test environment (teardown)
    /// </summary>
    public async Task DisposeAsync()
    {
        // Clean up test data
        if (!string.IsNullOrEmpty(_authToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);

            // Delete test workflows
            var workflows = await GetWorkflowsList();
            foreach (var workflow in workflows.Where(w => w.Name.StartsWith("E2E-Test-")))
            {
                await DeleteWorkflow(workflow.Id);
            }
        }

        _httpClient?.Dispose();
    }

    /// <summary>
    /// Test: Health check endpoint
    /// </summary>
    [Fact]
    public async Task HealthCheck_ShouldRespondOk()
    {
        var response = await _httpClient.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content);
    }

    /// <summary>
    /// Test: User registration
    /// </summary>
    [Fact]
    public async Task UserRegistration_ShouldCreateNewUser()
    {
        var registerRequest = new
        {
            username = $"e2e-user-{Guid.NewGuid():N}",
            password = TestPassword,
            email = $"e2e-{Guid.NewGuid():N}@test.local"
        };

        var response = await _httpClient.PostAsJsonAsync(
            "/api/v1/auth/register",
            registerRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadAsAsync<RegistrationResponse>();
        Assert.NotNull(result.UserId);
    }

    /// <summary>
    /// Test: OAuth authentication flow
    /// </summary>
    [Fact]
    public async Task OAuthFlow_ShouldIssueAccessToken()
    {
        var tokenRequest = new
        {
            grant_type = "password",
            username = TestUsername,
            password = TestPassword,
            scope = "openid profile email"
        };

        var response = await _httpClient.PostAsJsonAsync(
            "/oauth/token",
            tokenRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<TokenResponse>();
        Assert.NotNull(result.AccessToken);
        Assert.Equal("Bearer", result.TokenType);
    }

    /// <summary>
    /// Test: Create workflow with full schema validation
    /// </summary>
    [Fact]
    public async Task CreateWorkflow_WithValidData_ShouldSucceed()
    {
        var createRequest = new CreateWorkflowRequest
        {
            Name = $"E2E-Test-Workflow-{Guid.NewGuid():N}",
            Description = "Test workflow for E2E testing",
            Definition = new WorkflowDefinition
            {
                Version = "1.0",
                Trigger = new WorkflowTrigger { Type = "manual" },
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        Id = "step-1",
                        Name = "Send Email",
                        Action = "send-email",
                        Parameters = new { to = "test@example.com", subject = "Test" }
                    }
                }
            }
        };

        SetAuthHeader();
        var response = await _httpClient.PostAsJsonAsync(
            "/api/v1/workflows",
            createRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadAsAsync<WorkflowDetailResponse>();
        Assert.NotNull(result.Id);
        Assert.Equal(createRequest.Name, result.Name);
    }

    /// <summary>
    /// Test: Get workflow by ID
    /// </summary>
    [Fact]
    public async Task GetWorkflow_WithValidId_ShouldReturnWorkflow()
    {
        SetAuthHeader();

        // Create workflow first
        var workflowId = await CreateTestWorkflow();

        var response = await _httpClient.GetAsync($"/api/v1/workflows/{workflowId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<WorkflowDetailResponse>();
        Assert.Equal(workflowId, result.Id);
    }

    /// <summary>
    /// Test: Update workflow
    /// </summary>
    [Fact]
    public async Task UpdateWorkflow_WithValidData_ShouldSucceed()
    {
        SetAuthHeader();

        var workflowId = await CreateTestWorkflow();
        var updateRequest = new
        {
            name = $"Updated-{Guid.NewGuid():N}",
            description = "Updated description",
            isActive = false
        };

        var response = await _httpClient.PutAsJsonAsync(
            $"/api/v1/workflows/{workflowId}",
            updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<WorkflowDetailResponse>();
        Assert.Equal(updateRequest.name, result.Name);
        Assert.False(result.IsActive);
    }

    /// <summary>
    /// Test: List workflows with pagination
    /// </summary>
    [Fact]
    public async Task ListWorkflows_WithPagination_ShouldReturnPage()
    {
        SetAuthHeader();

        // Create multiple workflows
        for (int i = 0; i < 5; i++)
        {
            await CreateTestWorkflow();
        }

        var response = await _httpClient.GetAsync(
            "/api/v1/workflows?page=1&limit=3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<PaginatedResponse>();
        Assert.NotEmpty(result.Items);
        Assert.True(result.Total >= 5);
    }

    /// <summary>
    /// Test: Execute workflow
    /// </summary>
    [Fact]
    public async Task ExecuteWorkflow_ShouldStartExecution()
    {
        SetAuthHeader();

        var workflowId = await CreateTestWorkflow();
        var executeRequest = new { input = new { key = "value" } };

        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/workflows/{workflowId}/execute",
            executeRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<ExecutionStartResponse>();
        Assert.NotNull(result.ExecutionId);
    }

    /// <summary>
    /// Test: Get execution history
    /// </summary>
    [Fact]
    public async Task GetExecutionHistory_ShouldReturnExecutions()
    {
        SetAuthHeader();

        var workflowId = await CreateTestWorkflow();

        // Execute workflow
        await _httpClient.PostAsJsonAsync(
            $"/api/v1/workflows/{workflowId}/execute",
            new { input = new { } });

        // Get executions
        var response = await _httpClient.GetAsync(
            $"/api/v1/workflows/{workflowId}/executions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<PaginatedResponse>();
        Assert.NotEmpty(result.Items);
    }

    /// <summary>
    /// Test: Get workflow metrics
    /// </summary>
    [Fact]
    public async Task GetWorkflowMetrics_ShouldReturnMetrics()
    {
        SetAuthHeader();

        var workflowId = await CreateTestWorkflow();

        var response = await _httpClient.GetAsync(
            $"/api/v1/workflows/{workflowId}/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<WorkflowMetricsResponse>();
        Assert.NotNull(result.WorkflowId);
        Assert.True(result.TotalExecutions >= 0);
    }

    /// <summary>
    /// Test: Delete workflow
    /// </summary>
    [Fact]
    public async Task DeleteWorkflow_WithValidId_ShouldSucceed()
    {
        SetAuthHeader();

        var workflowId = await CreateTestWorkflow();

        var response = await _httpClient.DeleteAsync(
            $"/api/v1/workflows/{workflowId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify deletion
        var getResponse = await _httpClient.GetAsync(
            $"/api/v1/workflows/{workflowId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    /// <summary>
    /// Test: Concurrent workflow execution
    /// </summary>
    [Fact]
    public async Task ConcurrentWorkflowExecution_ShouldHandleParallel()
    {
        SetAuthHeader();

        var workflowId = await CreateTestWorkflow();

        // Execute workflow 10 times concurrently
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _httpClient.PostAsJsonAsync(
                $"/api/v1/workflows/{workflowId}/execute",
                new { input = new { } }))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        // Verify all executions were recorded
        var historyResponse = await _httpClient.GetAsync(
            $"/api/v1/workflows/{workflowId}/executions");
        var history = await historyResponse.Content.ReadAsAsync<PaginatedResponse>();
        Assert.True(history.Total >= 10);
    }

    /// <summary>
    /// Test: Bulk operations
    /// </summary>
    [Fact]
    public async Task BulkEnableWorkflows_ShouldUpdateMultiple()
    {
        SetAuthHeader();

        var ids = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            ids.Add(await CreateTestWorkflow());
        }

        // Disable all
        var bulkRequest = new { workflowIds = ids };
        var disableResponse = await _httpClient.PostAsJsonAsync(
            "/api/v1/workflows/bulk-disable",
            bulkRequest);

        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

        // Verify disabled
        foreach (var id in ids)
        {
            var getResponse = await _httpClient.GetAsync($"/api/v1/workflows/{id}");
            var workflow = await getResponse.Content.ReadAsAsync<WorkflowDetailResponse>();
            Assert.False(workflow.IsActive);
        }
    }

    // ===== Helper Methods =====

    private async Task WaitForApiReady(TimeSpan timeout)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                var response = await _httpClient.GetAsync("/health");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch { }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"API not ready after {timeout.TotalSeconds}s");
    }

    private async Task<string> AuthenticateUser()
    {
        var loginRequest = new { username = TestUsername, password = TestPassword };
        var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        if (!response.IsSuccessStatusCode)
            throw new Exception("Failed to authenticate");

        var result = await response.Content.ReadAsAsync<TokenResponse>();
        return result.AccessToken;
    }

    private void SetAuthHeader()
    {
        if (!string.IsNullOrEmpty(_authToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
        }
    }

    private async Task<string> CreateTestWorkflow()
    {
        var request = new CreateWorkflowRequest
        {
            Name = $"E2E-Test-{Guid.NewGuid():N}",
            Description = "Test workflow",
            Definition = new WorkflowDefinition
            {
                Version = "1.0",
                Trigger = new WorkflowTrigger { Type = "manual" },
                Steps = new List<WorkflowStep>()
            }
        };

        var response = await _httpClient.PostAsJsonAsync("/api/v1/workflows", request);
        var result = await response.Content.ReadAsAsync<WorkflowDetailResponse>();
        return result.Id;
    }

    private async Task<List<WorkflowSummary>> GetWorkflowsList()
    {
        var response = await _httpClient.GetAsync("/api/v1/workflows?limit=1000");
        var result = await response.Content.ReadAsAsync<PaginatedResponse>();
        return result.Items.Cast<WorkflowSummary>().ToList();
    }

    private async Task DeleteWorkflow(string workflowId)
    {
        await _httpClient.DeleteAsync($"/api/v1/workflows/{workflowId}");
    }
}

// ===== Data Models for E2E Tests =====

public class CreateWorkflowRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("definition")]
    public WorkflowDefinition Definition { get; set; } = new();
}

public class WorkflowDefinition
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("trigger")]
    public WorkflowTrigger Trigger { get; set; } = new();

    [JsonPropertyName("steps")]
    public List<WorkflowStep> Steps { get; set; } = new();
}

public class WorkflowTrigger
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "manual";
}

public class WorkflowStep
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public object Parameters { get; set; } = new();
}

public class WorkflowDetailResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }
}

public class WorkflowSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;
}

public class RegistrationResponse
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;
}

public class ExecutionStartResponse
{
    [JsonPropertyName("executionId")]
    public string ExecutionId { get; set; } = string.Empty;
}

public class PaginatedResponse
{
    [JsonPropertyName("items")]
    public List<object> Items { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public class WorkflowMetricsResponse
{
    [JsonPropertyName("workflowId")]
    public string WorkflowId { get; set; } = string.Empty;

    [JsonPropertyName("totalExecutions")]
    public int TotalExecutions { get; set; }
}
