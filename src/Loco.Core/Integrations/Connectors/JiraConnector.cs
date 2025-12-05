// John Carmack: "If you're going to do something, do it well"
// Rob Pike: "Design the data structures first"

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Jira connector for project management and issue tracking
/// Supports Jira Cloud REST API v3
/// </summary>
public sealed class JiraConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private string? _cloudId;

    public override string Id => "jira";
    public override string Name => "Jira";
    public override string Description => "Create and manage issues, projects, sprints, and workflows in Jira";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Productivity;

    public override ConnectorCapabilities Capabilities => new()
    {
        SupportsActions = true,
        SupportsTriggers = true,
        SupportsWebhooks = true,
        RateLimitPerMinute = 100
    };

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.Basic,
        RequiredCredentials =
        [
            new() { Name = "domain", Label = "Jira Domain", Type = ParameterType.String, Required = true,
                Description = "Your Jira domain (e.g., yourcompany.atlassian.net)" },
            new() { Name = "email", Label = "Email", Type = ParameterType.String, Required = true,
                Description = "Your Atlassian account email" },
            new() { Name = "apiToken", Label = "API Token", Type = ParameterType.Password, Required = true,
                Description = "API token from id.atlassian.com" }
        ]
    };

    public override IReadOnlyList<ConfigParameter> ConfigParameters =>
    [
        new() { Name = "defaultProject", Label = "Default Project Key", Type = ParameterType.String,
            Description = "Default project key for creating issues" }
    ];

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // Issues
        new()
        {
            Id = "createIssue",
            Name = "Create Issue",
            Description = "Create a new issue (bug, story, task, etc.)",
            Parameters =
            [
                new() { Name = "projectKey", Type = ParameterType.String, Description = "Project key (uses default if not specified)" },
                new() { Name = "issueType", Type = ParameterType.String, Required = true,
                    Description = "Issue type: Bug, Story, Task, Epic, etc." },
                new() { Name = "summary", Type = ParameterType.String, Required = true },
                new() { Name = "description", Type = ParameterType.String },
                new() { Name = "priority", Type = ParameterType.Select,
                    Options =
                    [
                        new() { Label = "Highest", Value = "Highest" },
                        new() { Label = "High", Value = "High" },
                        new() { Label = "Medium", Value = "Medium" },
                        new() { Label = "Low", Value = "Low" },
                        new() { Label = "Lowest", Value = "Lowest" }
                    ]},
                new() { Name = "assignee", Type = ParameterType.String, Description = "Assignee account ID" },
                new() { Name = "labels", Type = ParameterType.Json, Description = "[\"label1\", \"label2\"]" },
                new() { Name = "parentKey", Type = ParameterType.String, Description = "Parent issue key for subtasks" }
            ]
        },
        new()
        {
            Id = "getIssue",
            Name = "Get Issue",
            Description = "Get issue details by key",
            Parameters =
            [
                new() { Name = "issueKey", Type = ParameterType.String, Required = true, Description = "e.g., PROJ-123" },
                new() { Name = "fields", Type = ParameterType.String, Description = "Comma-separated field names" }
            ]
        },
        new()
        {
            Id = "updateIssue",
            Name = "Update Issue",
            Description = "Update an existing issue",
            Parameters =
            [
                new() { Name = "issueKey", Type = ParameterType.String, Required = true },
                new() { Name = "summary", Type = ParameterType.String },
                new() { Name = "description", Type = ParameterType.String },
                new() { Name = "priority", Type = ParameterType.String },
                new() { Name = "assignee", Type = ParameterType.String },
                new() { Name = "labels", Type = ParameterType.Json }
            ]
        },
        new()
        {
            Id = "deleteIssue",
            Name = "Delete Issue",
            Description = "Delete an issue",
            Parameters =
            [
                new() { Name = "issueKey", Type = ParameterType.String, Required = true },
                new() { Name = "deleteSubtasks", Type = ParameterType.Boolean, DefaultValue = false }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "transitionIssue",
            Name = "Transition Issue",
            Description = "Move issue to a different status",
            Parameters =
            [
                new() { Name = "issueKey", Type = ParameterType.String, Required = true },
                new() { Name = "transitionId", Type = ParameterType.String, Description = "Transition ID (use getTransitions to find)" },
                new() { Name = "transitionName", Type = ParameterType.String, Description = "Or transition name: To Do, In Progress, Done" },
                new() { Name = "comment", Type = ParameterType.String }
            ]
        },
        new()
        {
            Id = "getTransitions",
            Name = "Get Transitions",
            Description = "Get available transitions for an issue",
            Parameters =
            [
                new() { Name = "issueKey", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "addComment",
            Name = "Add Comment",
            Description = "Add a comment to an issue",
            Parameters =
            [
                new() { Name = "issueKey", Type = ParameterType.String, Required = true },
                new() { Name = "body", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "assignIssue",
            Name = "Assign Issue",
            Description = "Assign an issue to a user",
            Parameters =
            [
                new() { Name = "issueKey", Type = ParameterType.String, Required = true },
                new() { Name = "accountId", Type = ParameterType.String, Required = true, Description = "User account ID" }
            ]
        },
        // Search
        new()
        {
            Id = "searchIssues",
            Name = "Search Issues (JQL)",
            Description = "Search issues using JQL",
            Parameters =
            [
                new() { Name = "jql", Type = ParameterType.String, Required = true,
                    Description = "JQL query, e.g., project = PROJ AND status = Open" },
                new() { Name = "maxResults", Type = ParameterType.Number, DefaultValue = 50 },
                new() { Name = "startAt", Type = ParameterType.Number, DefaultValue = 0 },
                new() { Name = "fields", Type = ParameterType.String, Description = "Comma-separated field names" }
            ]
        },
        // Projects
        new()
        {
            Id = "getProject",
            Name = "Get Project",
            Description = "Get project details",
            Parameters =
            [
                new() { Name = "projectKey", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "listProjects",
            Name = "List Projects",
            Description = "List all accessible projects",
            Parameters =
            [
                new() { Name = "maxResults", Type = ParameterType.Number, DefaultValue = 50 }
            ]
        },
        // Sprints (Jira Software)
        new()
        {
            Id = "getActiveSprint",
            Name = "Get Active Sprint",
            Description = "Get the active sprint for a board",
            Parameters =
            [
                new() { Name = "boardId", Type = ParameterType.Number, Required = true }
            ]
        },
        new()
        {
            Id = "getSprintIssues",
            Name = "Get Sprint Issues",
            Description = "Get all issues in a sprint",
            Parameters =
            [
                new() { Name = "sprintId", Type = ParameterType.Number, Required = true },
                new() { Name = "maxResults", Type = ParameterType.Number, DefaultValue = 50 }
            ]
        },
        new()
        {
            Id = "moveToSprint",
            Name = "Move to Sprint",
            Description = "Move issues to a sprint",
            Parameters =
            [
                new() { Name = "sprintId", Type = ParameterType.Number, Required = true },
                new() { Name = "issueKeys", Type = ParameterType.Json, Required = true, Description = "[\"PROJ-1\", \"PROJ-2\"]" }
            ]
        },
        // Users
        new()
        {
            Id = "searchUsers",
            Name = "Search Users",
            Description = "Search for users",
            Parameters =
            [
                new() { Name = "query", Type = ParameterType.String, Required = true },
                new() { Name = "maxResults", Type = ParameterType.Number, DefaultValue = 50 }
            ]
        },
        new()
        {
            Id = "getCurrentUser",
            Name = "Get Current User",
            Description = "Get the authenticated user's details",
            Parameters = []
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "onIssueCreated",
            Name = "On Issue Created",
            Description = "Triggered when a new issue is created",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "projectKey", Type = ParameterType.String, Description = "Filter by project" },
                new() { Name = "issueType", Type = ParameterType.String, Description = "Filter by issue type" }
            ]
        },
        new()
        {
            Id = "onIssueUpdated",
            Name = "On Issue Updated",
            Description = "Triggered when an issue is updated",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "onIssueTransitioned",
            Name = "On Issue Transitioned",
            Description = "Triggered when an issue changes status",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "toStatus", Type = ParameterType.String, Description = "Filter by target status" }
            ]
        },
        new()
        {
            Id = "onCommentAdded",
            Name = "On Comment Added",
            Description = "Triggered when a comment is added to an issue",
            Type = TriggerType.Webhook
        }
    ];

    public override async Task<ConnectionTestResult> TestConnectionAsync(
        ConnectorConfiguration config,
        CancellationToken ct = default)
    {
        try
        {
            var domain = config.GetCredentialString("domain")!;
            var email = config.GetCredentialString("email")!;
            var apiToken = config.GetCredentialString("apiToken")!;

            using var client = new HttpClient();
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            var response = await client.GetAsync($"https://{domain}/rest/api/3/myself", ct);

            if (!response.IsSuccessStatusCode)
            {
                return ConnectionTestResult.Fail($"Authentication failed: {response.StatusCode}");
            }

            var result = await response.Content.ReadAsStringAsync(ct);
            var user = JsonSerializer.Deserialize<JsonElement>(result);
            var displayName = user.GetProperty("displayName").GetString();

            return ConnectionTestResult.Ok($"Connected as {displayName}");
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Fail("Connection test failed", ex);
        }
    }

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        var domain = config.GetCredentialString("domain")!;
        var email = config.GetCredentialString("email")!;
        var apiToken = config.GetCredentialString("apiToken")!;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"https://{domain}/rest/api/3/")
        };

        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        await base.InitializeAsync(config, ct);
    }

    protected override async Task<ActionResult> ExecuteActionCoreAsync(
        ConnectorAction action,
        ActionParameters parameters,
        Core.ExecutionContext context,
        CancellationToken ct)
    {
        return action.Id switch
        {
            "createIssue" => await CreateIssueAsync(parameters, ct),
            "getIssue" => await GetIssueAsync(parameters, ct),
            "updateIssue" => await UpdateIssueAsync(parameters, ct),
            "deleteIssue" => await DeleteIssueAsync(parameters, ct),
            "transitionIssue" => await TransitionIssueAsync(parameters, ct),
            "getTransitions" => await GetTransitionsAsync(parameters, ct),
            "addComment" => await AddCommentAsync(parameters, ct),
            "assignIssue" => await AssignIssueAsync(parameters, ct),
            "searchIssues" => await SearchIssuesAsync(parameters, ct),
            "getProject" => await GetProjectAsync(parameters, ct),
            "listProjects" => await ListProjectsAsync(parameters, ct),
            "getActiveSprint" => await GetActiveSprintAsync(parameters, ct),
            "getSprintIssues" => await GetSprintIssuesAsync(parameters, ct),
            "moveToSprint" => await MoveToSprintAsync(parameters, ct),
            "searchUsers" => await SearchUsersAsync(parameters, ct),
            "getCurrentUser" => await GetCurrentUserAsync(ct),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> CreateIssueAsync(ActionParameters parameters, CancellationToken ct)
    {
        var projectKey = parameters.GetString("projectKey") ?? Configuration?.GetSettingString("defaultProject");
        if (string.IsNullOrEmpty(projectKey))
        {
            return ActionResult.Fail("Project key is required", "MISSING_PARAMETER");
        }

        var fields = new Dictionary<string, object>
        {
            ["project"] = new { key = projectKey },
            ["issuetype"] = new { name = parameters.GetString("issueType") },
            ["summary"] = parameters.GetString("summary")!
        };

        var description = parameters.GetString("description");
        if (!string.IsNullOrEmpty(description))
        {
            fields["description"] = new
            {
                type = "doc",
                version = 1,
                content = new[]
                {
                    new
                    {
                        type = "paragraph",
                        content = new[] { new { type = "text", text = description } }
                    }
                }
            };
        }

        var priority = parameters.GetString("priority");
        if (!string.IsNullOrEmpty(priority))
        {
            fields["priority"] = new { name = priority };
        }

        var assignee = parameters.GetString("assignee");
        if (!string.IsNullOrEmpty(assignee))
        {
            fields["assignee"] = new { accountId = assignee };
        }

        var labels = parameters.Get<JsonElement?>("labels");
        if (labels.HasValue && labels.Value.ValueKind == JsonValueKind.Array)
        {
            fields["labels"] = labels.Value.EnumerateArray().Select(l => l.GetString()).ToList();
        }

        var parentKey = parameters.GetString("parentKey");
        if (!string.IsNullOrEmpty(parentKey))
        {
            fields["parent"] = new { key = parentKey };
        }

        var payload = new { fields };
        return await PostJsonAsync("issue", payload, ct);
    }

    private async Task<ActionResult> GetIssueAsync(ActionParameters parameters, CancellationToken ct)
    {
        var issueKey = parameters.GetString("issueKey")!;
        var fields = parameters.GetString("fields");

        var url = $"issue/{issueKey}";
        if (!string.IsNullOrEmpty(fields))
        {
            url += $"?fields={Uri.EscapeDataString(fields)}";
        }

        var response = await _httpClient!.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Issue not found", "NOT_FOUND");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> UpdateIssueAsync(ActionParameters parameters, CancellationToken ct)
    {
        var issueKey = parameters.GetString("issueKey")!;
        var fields = new Dictionary<string, object>();

        var summary = parameters.GetString("summary");
        if (!string.IsNullOrEmpty(summary))
        {
            fields["summary"] = summary;
        }

        var description = parameters.GetString("description");
        if (!string.IsNullOrEmpty(description))
        {
            fields["description"] = new
            {
                type = "doc",
                version = 1,
                content = new[]
                {
                    new
                    {
                        type = "paragraph",
                        content = new[] { new { type = "text", text = description } }
                    }
                }
            };
        }

        var priority = parameters.GetString("priority");
        if (!string.IsNullOrEmpty(priority))
        {
            fields["priority"] = new { name = priority };
        }

        var assignee = parameters.GetString("assignee");
        if (!string.IsNullOrEmpty(assignee))
        {
            fields["assignee"] = new { accountId = assignee };
        }

        if (fields.Count == 0)
        {
            return ActionResult.Fail("No fields to update", "INVALID_PARAMETER");
        }

        var payload = new { fields };
        return await PutJsonAsync($"issue/{issueKey}", payload, ct);
    }

    private async Task<ActionResult> DeleteIssueAsync(ActionParameters parameters, CancellationToken ct)
    {
        var issueKey = parameters.GetString("issueKey")!;
        var deleteSubtasks = parameters.GetBool("deleteSubtasks");

        var url = $"issue/{issueKey}";
        if (deleteSubtasks)
        {
            url += "?deleteSubtasks=true";
        }

        var response = await _httpClient!.DeleteAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to delete issue: {error}", "API_ERROR");
        }

        return ActionResult.Ok(new { deleted = true, issueKey });
    }

    private async Task<ActionResult> TransitionIssueAsync(ActionParameters parameters, CancellationToken ct)
    {
        var issueKey = parameters.GetString("issueKey")!;
        var transitionId = parameters.GetString("transitionId");
        var transitionName = parameters.GetString("transitionName");

        if (string.IsNullOrEmpty(transitionId) && !string.IsNullOrEmpty(transitionName))
        {
            // Find transition by name
            var transitionsResult = await GetTransitionsAsync(parameters, ct);
            if (!transitionsResult.Success)
            {
                return transitionsResult;
            }

            var transitions = (JsonElement)transitionsResult.Data!;
            foreach (var t in transitions.GetProperty("transitions").EnumerateArray())
            {
                if (t.GetProperty("name").GetString()?.Equals(transitionName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    transitionId = t.GetProperty("id").GetString();
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(transitionId))
        {
            return ActionResult.Fail("Transition ID or name is required", "MISSING_PARAMETER");
        }

        var payload = new Dictionary<string, object>
        {
            ["transition"] = new { id = transitionId }
        };

        var comment = parameters.GetString("comment");
        if (!string.IsNullOrEmpty(comment))
        {
            payload["update"] = new
            {
                comment = new[]
                {
                    new
                    {
                        add = new
                        {
                            body = new
                            {
                                type = "doc",
                                version = 1,
                                content = new[]
                                {
                                    new
                                    {
                                        type = "paragraph",
                                        content = new[] { new { type = "text", text = comment } }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        return await PostJsonAsync($"issue/{issueKey}/transitions", payload, ct);
    }

    private async Task<ActionResult> GetTransitionsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var issueKey = parameters.GetString("issueKey")!;
        var response = await _httpClient!.GetAsync($"issue/{issueKey}/transitions", ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Failed to get transitions", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> AddCommentAsync(ActionParameters parameters, CancellationToken ct)
    {
        var issueKey = parameters.GetString("issueKey")!;
        var body = parameters.GetString("body")!;

        var payload = new
        {
            body = new
            {
                type = "doc",
                version = 1,
                content = new[]
                {
                    new
                    {
                        type = "paragraph",
                        content = new[] { new { type = "text", text = body } }
                    }
                }
            }
        };

        return await PostJsonAsync($"issue/{issueKey}/comment", payload, ct);
    }

    private async Task<ActionResult> AssignIssueAsync(ActionParameters parameters, CancellationToken ct)
    {
        var issueKey = parameters.GetString("issueKey")!;
        var accountId = parameters.GetString("accountId")!;

        var payload = new { accountId };
        return await PutJsonAsync($"issue/{issueKey}/assignee", payload, ct);
    }

    private async Task<ActionResult> SearchIssuesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var jql = parameters.GetString("jql")!;
        var maxResults = parameters.GetInt("maxResults", 50);
        var startAt = parameters.GetInt("startAt", 0);
        var fields = parameters.GetString("fields");

        var payload = new Dictionary<string, object>
        {
            ["jql"] = jql,
            ["maxResults"] = maxResults,
            ["startAt"] = startAt
        };

        if (!string.IsNullOrEmpty(fields))
        {
            payload["fields"] = fields.Split(',').Select(f => f.Trim()).ToList();
        }

        return await PostJsonAsync("search", payload, ct);
    }

    private async Task<ActionResult> GetProjectAsync(ActionParameters parameters, CancellationToken ct)
    {
        var projectKey = parameters.GetString("projectKey")!;
        var response = await _httpClient!.GetAsync($"project/{projectKey}", ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Project not found", "NOT_FOUND");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> ListProjectsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var maxResults = parameters.GetInt("maxResults", 50);
        var response = await _httpClient!.GetAsync($"project?maxResults={maxResults}", ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Failed to list projects", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> GetActiveSprintAsync(ActionParameters parameters, CancellationToken ct)
    {
        var boardId = parameters.GetInt("boardId");

        // Use Jira Software REST API
        var domain = Configuration?.GetCredentialString("domain");
        var response = await _httpClient!.GetAsync(
            $"https://{domain}/rest/agile/1.0/board/{boardId}/sprint?state=active",
            ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Failed to get active sprint", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> GetSprintIssuesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var sprintId = parameters.GetInt("sprintId");
        var maxResults = parameters.GetInt("maxResults", 50);

        var domain = Configuration?.GetCredentialString("domain");
        var response = await _httpClient!.GetAsync(
            $"https://{domain}/rest/agile/1.0/sprint/{sprintId}/issue?maxResults={maxResults}",
            ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Failed to get sprint issues", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> MoveToSprintAsync(ActionParameters parameters, CancellationToken ct)
    {
        var sprintId = parameters.GetInt("sprintId");
        var issueKeys = parameters.Get<JsonElement>("issueKeys");

        if (issueKeys.ValueKind != JsonValueKind.Array)
        {
            return ActionResult.Fail("issueKeys must be an array", "INVALID_PARAMETER");
        }

        var issues = issueKeys.EnumerateArray().Select(i => i.GetString()).ToList();
        var payload = new { issues };

        var domain = Configuration?.GetCredentialString("domain");
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync(
            $"https://{domain}/rest/agile/1.0/sprint/{sprintId}/issue",
            content,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to move issues: {error}", "API_ERROR");
        }

        return ActionResult.Ok(new { moved = true, sprintId, issueCount = issues.Count });
    }

    private async Task<ActionResult> SearchUsersAsync(ActionParameters parameters, CancellationToken ct)
    {
        var query = parameters.GetString("query")!;
        var maxResults = parameters.GetInt("maxResults", 50);

        var response = await _httpClient!.GetAsync(
            $"user/search?query={Uri.EscapeDataString(query)}&maxResults={maxResults}",
            ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Failed to search users", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> GetCurrentUserAsync(CancellationToken ct)
    {
        var response = await _httpClient!.GetAsync("myself", ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Failed to get current user", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> PostJsonAsync(string endpoint, object payload, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync(endpoint, content, ct);

        var result = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail($"API error: {result}", "API_ERROR");
        }

        if (string.IsNullOrEmpty(result))
        {
            return ActionResult.Ok(new { success = true });
        }

        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> PutJsonAsync(string endpoint, object payload, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient!.PutAsync(endpoint, content, ct);

        var result = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail($"API error: {result}", "API_ERROR");
        }

        if (string.IsNullOrEmpty(result))
        {
            return ActionResult.Ok(new { success = true });
        }

        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
