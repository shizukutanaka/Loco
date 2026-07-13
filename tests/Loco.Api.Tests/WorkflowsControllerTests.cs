using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Loco.Api.Tests;

/// <summary>
/// End-to-end integration tests for the workflow API: CRUD round-trip through
/// the JSON file store, execution on the real VisualWorkflowEngine (built-in
/// node handlers only - no external connector credentials needed), polling,
/// cancellation, and validation. Assertions are made on raw JSON so the
/// envelope + camelCase contract the frontend depends on is pinned exactly.
///
/// The previous tests in this file unit-tested the deleted stub controller
/// (skip/take, IAutomationEngine mocks) and never compiled (NU1008).
///
/// NOTE: authored in an environment where dotnet test could not run (NuGet
/// egress blocked); execution status is recorded in the commit message.
/// </summary>
public class WorkflowApiTests : IClassFixture<LocoApiFactory>
{
    private readonly LocoApiFactory _factory;

    public WorkflowApiTests(LocoApiFactory factory)
    {
        _factory = factory;
    }

    private static object VariableWorkflowBody(string name) => new
    {
        name,
        description = "set-then-get variable chain",
        nodes = new object[]
        {
            new
            {
                id = "n-set",
                type = "action",
                position = new { x = 0, y = 0 },
                data = new
                {
                    label = "Set greeting",
                    integration = "variable",
                    config = new Dictionary<string, object> { ["action"] = "set", ["name"] = "greeting", ["value"] = "hello" },
                },
            },
            new
            {
                id = "n-get",
                type = "action",
                position = new { x = 200, y = 0 },
                data = new
                {
                    label = "Get greeting",
                    integration = "variable",
                    config = new Dictionary<string, object> { ["action"] = "get", ["name"] = "greeting" },
                },
            },
        },
        edges = new object[]
        {
            new { id = "e1", source = "n-set", target = "n-get" },
        },
        metadata = new { version = "1.0.0", isPublic = false },
    };

    [Fact]
    public async Task Crud_RoundTrip_PreservesEnvelopeAndCamelCase()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Create
        var createResponse = await client.PostAsJsonAsync("/api/v1/workflows", VariableWorkflowBody("crud-wf"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        createBody.GetProperty("success").GetBoolean().Should().BeTrue();
        var created = createBody.GetProperty("data");
        var id = created.GetProperty("id").GetString()!;
        created.GetProperty("name").GetString().Should().Be("crud-wf");
        created.GetProperty("createdAt").GetString().Should().NotBeNullOrWhiteSpace();
        // camelCase contract: nodes[0].data.label must exist with these exact key spellings
        created.GetProperty("nodes")[0].GetProperty("data").GetProperty("label")
            .GetString().Should().Be("Set greeting");

        // Get
        var getBody = await client.GetFromJsonAsync<JsonElement>($"/api/v1/workflows/{id}");
        getBody.GetProperty("success").GetBoolean().Should().BeTrue();
        getBody.GetProperty("data").GetProperty("id").GetString().Should().Be(id);

        // List (envelope: data.workflows/total/page/pageSize)
        var listBody = await client.GetFromJsonAsync<JsonElement>("/api/v1/workflows?page=1&pageSize=10");
        listBody.GetProperty("success").GetBoolean().Should().BeTrue();
        var listData = listBody.GetProperty("data");
        listData.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        listData.GetProperty("page").GetInt32().Should().Be(1);
        listData.GetProperty("pageSize").GetInt32().Should().Be(10);
        listData.GetProperty("workflows").EnumerateArray()
            .Select(w => w.GetProperty("id").GetString())
            .Should().Contain(id);

        // Update (partial)
        var updateResponse = await client.PutAsJsonAsync($"/api/v1/workflows/{id}",
            new { name = "crud-wf-renamed" });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updateBody = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        updateBody.GetProperty("data").GetProperty("name").GetString().Should().Be("crud-wf-renamed");
        updateBody.GetProperty("data").GetProperty("nodes").GetArrayLength()
            .Should().Be(2, "partial update must not clear fields that were not supplied");

        // Delete
        var deleteResponse = await client.DeleteAsync($"/api/v1/workflows/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/v1/workflows/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMissingWorkflow_Returns404ErrorEnvelope()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/workflows/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("error").GetProperty("code").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task Execute_VariableChain_CompletesAndExposesOutput()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/v1/workflows", VariableWorkflowBody("exec-wf"));
        var id = (await createResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("id").GetString()!;

        var executeResponse = await client.PostAsJsonAsync($"/api/v1/workflows/{id}/execute", new { });
        executeResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var executionId = (await executeResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("executionId").GetString()!;

        // Poll until terminal (variable nodes complete near-instantly; generous timeout).
        var status = await PollUntilTerminalAsync(client, executionId, TimeSpan.FromSeconds(10));

        status.GetProperty("status").GetString().Should().Be("completed");
        status.GetProperty("completedAt").GetString().Should().NotBeNullOrWhiteSpace();
        status.GetProperty("output").GetProperty("n-get").GetString().Should().Be("hello");
    }

    // The ExecuteRequest.DryRun field existed with nothing checking it - a caller
    // passing dryRun:true expecting a safe preview would silently get a real
    // execution instead (every connector action actually invoked). This pins the
    // fix: dry run must never create a pollable execution or touch a connector.
    [Fact]
    public async Task Execute_DryRun_ReturnsPlanImmediately_WithoutInvokingConnectorsOrCreatingExecution()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/v1/workflows", VariableWorkflowBody("dry-run-wf"));
        var id = (await createResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("id").GetString()!;

        var executeResponse = await client.PostAsJsonAsync(
            $"/api/v1/workflows/{id}/execute", new { dryRun = true });

        // Synchronous 200, not the real-execution 202 Accepted.
        executeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await executeResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeTrue();

        var data = body.GetProperty("data");
        data.GetProperty("status").GetString().Should().Be("completed");
        data.GetProperty("completedAt").GetString().Should().NotBeNullOrWhiteSpace();
        var plannedNodes = data.GetProperty("output").GetProperty("plannedNodes");
        plannedNodes.GetArrayLength().Should().Be(2);
        plannedNodes[0].GetProperty("integration").GetString().Should().Be("variable");

        // Must not have registered a real, pollable execution.
        var executionId = data.GetProperty("executionId").GetString()!;
        var pollResponse = await client.GetAsync($"/api/v1/executions/{executionId}");
        pollResponse.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a dry run must never create an ExecutionRegistry entry - it never ran anything");
    }

    [Fact]
    public async Task Cancel_DelayWorkflow_ReportsCancelledNotFailed()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var body = new
        {
            name = "delay-wf",
            nodes = new object[]
            {
                new
                {
                    id = "n-delay",
                    type = "delay",
                    position = new { x = 0, y = 0 },
                    data = new
                    {
                        label = "Long delay",
                        config = new Dictionary<string, object> { ["seconds"] = 30 },
                    },
                },
            },
            edges = Array.Empty<object>(),
            metadata = new { version = "1.0.0", isPublic = false },
        };
        var createResponse = await client.PostAsJsonAsync("/api/v1/workflows", body);
        var id = (await createResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("id").GetString()!;

        var executeResponse = await client.PostAsJsonAsync($"/api/v1/workflows/{id}/execute", new { });
        var executionId = (await executeResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("executionId").GetString()!;

        var cancelResponse = await client.PostAsync($"/api/v1/executions/{executionId}/cancel", content: null);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await PollUntilTerminalAsync(client, executionId, TimeSpan.FromSeconds(10));
        status.GetProperty("status").GetString().Should().Be("cancelled",
            "user-requested cancellation must not be reported as a failure");
    }

    [Fact]
    public async Task GetUnknownExecution_Returns404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/executions/nope");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Validate_CyclicWorkflow_ReturnsValidFalseWithErrors()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var cyclic = new
        {
            id = "cyclic-wf",
            name = "cyclic",
            nodes = new object[]
            {
                new { id = "a", type = "action", position = new { x = 0, y = 0 }, data = new { label = "a", config = new Dictionary<string, object>() } },
                new { id = "b", type = "action", position = new { x = 0, y = 0 }, data = new { label = "b", config = new Dictionary<string, object>() } },
            },
            edges = new object[]
            {
                new { id = "e1", source = "a", target = "b" },
                new { id = "e2", source = "b", target = "a" },
            },
            metadata = new { version = "1.0.0", isPublic = false },
            createdAt = "2026-01-01T00:00:00.000Z",
            updatedAt = "2026-01-01T00:00:00.000Z",
        };

        var response = await client.PostAsJsonAsync("/api/v1/workflows/validate", cyclic);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        body.GetProperty("data").GetProperty("valid").GetBoolean().Should().BeFalse();
        body.GetProperty("data").GetProperty("errors").GetArrayLength().Should().BeGreaterThan(0);
    }

    private static async Task<JsonElement> PollUntilTerminalAsync(
        HttpClient client, string executionId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var body = await client.GetFromJsonAsync<JsonElement>($"/api/v1/executions/{executionId}");
            var data = body.GetProperty("data");
            var status = data.GetProperty("status").GetString();
            if (status is "completed" or "failed" or "cancelled")
            {
                return data;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Execution {executionId} still '{status}' after {timeout}");
            }

            await Task.Delay(100);
        }
    }
}
