using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Linear connector for modern issue tracking and project management.
/// Uses Linear GraphQL API.
/// </summary>
public sealed class LinearConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public override string Id => "linear";
    public override string Name => "Linear";
    public override string Description => "Issue tracking and project management for modern software teams";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Productivity;
    public override string IconUrl => "https://linear.app/favicon.ico";

    public override ConnectorCapabilities Capabilities => new()
    {
        SupportsActions = true,
        SupportsTriggers = true,
        SupportsWebhooks = true
    };

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.ApiKey,
        RequiredCredentials = new CredentialField[]
        {
            new() { Name = "apiKey", Label = "API Key", Type = ParameterType.Password, Description = "Linear API key" }
        }
    };

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // Issues
        new()
        {
            Id = "getIssues",
            Name = "Get Issues",
            Description = "Get all issues",
            Parameters = new ActionParameter[]
            {
                new() { Name = "first", Type = ParameterType.Number, DefaultValue = 50, Description = "Number of issues to fetch" },
                new() { Name = "teamId", Type = ParameterType.String, Description = "Filter by team ID" },
                new() { Name = "assigneeId", Type = ParameterType.String, Description = "Filter by assignee ID" },
                new() { Name = "state", Type = ParameterType.String, Description = "Filter by state name" }
            }
        },
        new()
        {
            Id = "getIssue",
            Name = "Get Issue",
            Description = "Get a specific issue",
            Parameters = new ActionParameter[]
            {
                new() { Name = "issueId", Type = ParameterType.String, Required = true, Description = "Issue ID or identifier (e.g., ENG-123)" }
            }
        },
        new()
        {
            Id = "createIssue",
            Name = "Create Issue",
            Description = "Create a new issue",
            Parameters = new ActionParameter[]
            {
                new() { Name = "title", Type = ParameterType.String, Required = true },
                new() { Name = "description", Type = ParameterType.String },
                new() { Name = "teamId", Type = ParameterType.String, Required = true },
                new() { Name = "priority", Type = ParameterType.Number, Description = "0=No priority, 1=Urgent, 2=High, 3=Medium, 4=Low" },
                new() { Name = "assigneeId", Type = ParameterType.String },
                new() { Name = "projectId", Type = ParameterType.String },
                new() { Name = "labelIds", Type = ParameterType.String, Description = "Comma-separated label IDs" },
                new() { Name = "estimate", Type = ParameterType.Number, Description = "Story points estimate" },
                new() { Name = "dueDate", Type = ParameterType.DateTime }
            }
        },
        new()
        {
            Id = "updateIssue",
            Name = "Update Issue",
            Description = "Update an existing issue",
            Parameters = new ActionParameter[]
            {
                new() { Name = "issueId", Type = ParameterType.String, Required = true },
                new() { Name = "title", Type = ParameterType.String },
                new() { Name = "description", Type = ParameterType.String },
                new() { Name = "priority", Type = ParameterType.Number },
                new() { Name = "assigneeId", Type = ParameterType.String },
                new() { Name = "stateId", Type = ParameterType.String },
                new() { Name = "estimate", Type = ParameterType.Number },
                new() { Name = "dueDate", Type = ParameterType.DateTime }
            }
        },
        new()
        {
            Id = "deleteIssue",
            Name = "Delete Issue",
            Description = "Archive/delete an issue",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "issueId", Type = ParameterType.String, Required = true }
            }
        },

        // Comments
        new()
        {
            Id = "addComment",
            Name = "Add Comment",
            Description = "Add a comment to an issue",
            Parameters = new ActionParameter[]
            {
                new() { Name = "issueId", Type = ParameterType.String, Required = true },
                new() { Name = "body", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "getComments",
            Name = "Get Comments",
            Description = "Get all comments for an issue",
            Parameters = new ActionParameter[]
            {
                new() { Name = "issueId", Type = ParameterType.String, Required = true }
            }
        },

        // Projects
        new()
        {
            Id = "getProjects",
            Name = "Get Projects",
            Description = "Get all projects",
            Parameters = new ActionParameter[]
            {
                new() { Name = "first", Type = ParameterType.Number, DefaultValue = 50 }
            }
        },
        new()
        {
            Id = "createProject",
            Name = "Create Project",
            Description = "Create a new project",
            Parameters = new ActionParameter[]
            {
                new() { Name = "name", Type = ParameterType.String, Required = true },
                new() { Name = "description", Type = ParameterType.String },
                new() { Name = "teamIds", Type = ParameterType.String, Required = true, Description = "Comma-separated team IDs" },
                new() { Name = "targetDate", Type = ParameterType.DateTime },
                new() { Name = "state", Type = ParameterType.String, DefaultValue = "planned", Description = "planned, started, paused, completed, canceled" }
            }
        },
        new()
        {
            Id = "updateProject",
            Name = "Update Project",
            Description = "Update a project",
            Parameters = new ActionParameter[]
            {
                new() { Name = "projectId", Type = ParameterType.String, Required = true },
                new() { Name = "name", Type = ParameterType.String },
                new() { Name = "description", Type = ParameterType.String },
                new() { Name = "state", Type = ParameterType.String },
                new() { Name = "targetDate", Type = ParameterType.DateTime }
            }
        },

        // Teams
        new()
        {
            Id = "getTeams",
            Name = "Get Teams",
            Description = "Get all teams",
            Parameters = new ActionParameter[]
            {
                new() { Name = "first", Type = ParameterType.Number, DefaultValue = 50 }
            }
        },
        new()
        {
            Id = "getTeam",
            Name = "Get Team",
            Description = "Get a specific team",
            Parameters = new ActionParameter[]
            {
                new() { Name = "teamId", Type = ParameterType.String, Required = true }
            }
        },

        // Users
        new()
        {
            Id = "getUsers",
            Name = "Get Users",
            Description = "Get all users",
            Parameters = new ActionParameter[]
            {
                new() { Name = "first", Type = ParameterType.Number, DefaultValue = 50 }
            }
        },
        new()
        {
            Id = "getViewer",
            Name = "Get Current User",
            Description = "Get information about the authenticated user",
            Parameters = Array.Empty<ActionParameter>()
        },

        // Labels
        new()
        {
            Id = "getLabels",
            Name = "Get Labels",
            Description = "Get all issue labels",
            Parameters = new ActionParameter[]
            {
                new() { Name = "first", Type = ParameterType.Number, DefaultValue = 50 }
            }
        },
        new()
        {
            Id = "createLabel",
            Name = "Create Label",
            Description = "Create a new label",
            Parameters = new ActionParameter[]
            {
                new() { Name = "name", Type = ParameterType.String, Required = true },
                new() { Name = "color", Type = ParameterType.String, Description = "Hex color code (e.g., #FF0000)" },
                new() { Name = "teamId", Type = ParameterType.String }
            }
        },

        // Workflow States
        new()
        {
            Id = "getWorkflowStates",
            Name = "Get Workflow States",
            Description = "Get all workflow states",
            Parameters = new ActionParameter[]
            {
                new() { Name = "teamId", Type = ParameterType.String, Description = "Filter by team ID" }
            }
        },

        // Search
        new()
        {
            Id = "searchIssues",
            Name = "Search Issues",
            Description = "Search for issues",
            Parameters = new ActionParameter[]
            {
                new() { Name = "query", Type = ParameterType.String, Required = true },
                new() { Name = "first", Type = ParameterType.Number, DefaultValue = 50 }
            }
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "issueCreated",
            Name = "Issue Created",
            Description = "Triggered when a new issue is created",
            Type = TriggerType.Webhook,
            ConfigParameters = new ActionParameter[]
            {
                new() { Name = "teamId", Type = ParameterType.String, Description = "Filter by team" }
            }
        },
        new()
        {
            Id = "issueUpdated",
            Name = "Issue Updated",
            Description = "Triggered when an issue is updated",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "issueStatusChanged",
            Name = "Issue Status Changed",
            Description = "Triggered when an issue status changes",
            Type = TriggerType.Webhook
        }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        await base.InitializeAsync(config, ct);

        var apiKey = config.GetCredentialString("apiKey");

        // Dispose any previous client before replacing it. InitializeAsync can run more
        // than once for the same cached connector instance (e.g. ConnectorRegistry.
        // GetInitializedConnectorAsync on credential rotation); overwriting _httpClient
        // unconditionally previously leaked the old HttpClient and its socket handler.
        _httpClient?.Dispose();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.linear.app/graphql")
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    protected override async Task<ActionResult> ExecuteActionCoreAsync(
        ConnectorAction action,
        ActionParameters parameters,
        Core.ExecutionContext context,
        CancellationToken ct)
    {
        return action.Id switch
        {
            "getIssues" => await GetIssuesAsync(parameters, ct),
            "getIssue" => await GetIssueAsync(parameters, ct),
            "createIssue" => await CreateIssueAsync(parameters, ct),
            "updateIssue" => await UpdateIssueAsync(parameters, ct),
            "deleteIssue" => await DeleteIssueAsync(parameters, ct),

            "addComment" => await AddCommentAsync(parameters, ct),
            "getComments" => await GetCommentsAsync(parameters, ct),

            "getProjects" => await GetProjectsAsync(parameters, ct),
            "createProject" => await CreateProjectAsync(parameters, ct),
            "updateProject" => await UpdateProjectAsync(parameters, ct),

            "getTeams" => await GetTeamsAsync(parameters, ct),
            "getTeam" => await GetTeamAsync(parameters, ct),

            "getUsers" => await GetUsersAsync(parameters, ct),
            "getViewer" => await GetViewerAsync(ct),

            "getLabels" => await GetLabelsAsync(parameters, ct),
            "createLabel" => await CreateLabelAsync(parameters, ct),

            "getWorkflowStates" => await GetWorkflowStatesAsync(parameters, ct),

            "searchIssues" => await SearchIssuesAsync(parameters, ct),

            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> GetIssuesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var first = parameters.GetInt("first", 50);
        var filters = new List<string>();

        var teamId = parameters.GetString("teamId");
        if (!string.IsNullOrEmpty(teamId))
            filters.Add($"team: {{ id: {{ eq: \"{teamId}\" }} }}");

        var assigneeId = parameters.GetString("assigneeId");
        if (!string.IsNullOrEmpty(assigneeId))
            filters.Add($"assignee: {{ id: {{ eq: \"{assigneeId}\" }} }}");

        var state = parameters.GetString("state");
        if (!string.IsNullOrEmpty(state))
            filters.Add($"state: {{ name: {{ eq: \"{state}\" }} }}");

        var filterClause = filters.Count > 0 ? $", filter: {{ {string.Join(", ", filters)} }}" : "";

        var query = $$"""
        {
          issues(first: {{first}}{{filterClause}}) {
            nodes {
              id
              identifier
              title
              description
              priority
              estimate
              dueDate
              createdAt
              updatedAt
              state { id name type }
              assignee { id name email }
              team { id name }
              project { id name }
              labels { nodes { id name color } }
            }
          }
        }
        """;

        return await ExecuteGraphQLAsync(query, ct);
    }

    private async Task<ActionResult> GetIssueAsync(ActionParameters parameters, CancellationToken ct)
    {
        var issueId = parameters.GetString("issueId")!;

        var query = $$"""
        {
          issue(id: "{{issueId}}") {
            id
            identifier
            title
            description
            priority
            estimate
            dueDate
            createdAt
            updatedAt
            state { id name type }
            assignee { id name email }
            team { id name }
            project { id name }
            labels { nodes { id name color } }
            comments { nodes { id body createdAt user { id name } } }
          }
        }
        """;

        return await ExecuteGraphQLAsync(query, ct);
    }

    private async Task<ActionResult> CreateIssueAsync(ActionParameters parameters, CancellationToken ct)
    {
        var inputs = new List<string>
        {
            $"title: \"{EscapeGraphQL(parameters.GetString("title")!)}\"",
            $"teamId: \"{parameters.GetString("teamId")!}\""
        };

        var description = parameters.GetString("description");
        if (!string.IsNullOrEmpty(description))
            inputs.Add($"description: \"{EscapeGraphQL(description)}\"");

        if (parameters.Has("priority"))
            inputs.Add($"priority: {parameters.GetInt("priority", 0)}");

        var assigneeId = parameters.GetString("assigneeId");
        if (!string.IsNullOrEmpty(assigneeId))
            inputs.Add($"assigneeId: \"{assigneeId}\"");

        var projectId = parameters.GetString("projectId");
        if (!string.IsNullOrEmpty(projectId))
            inputs.Add($"projectId: \"{projectId}\"");

        var labelIds = parameters.GetString("labelIds");
        if (!string.IsNullOrEmpty(labelIds))
        {
            var ids = string.Join("\", \"", labelIds.Split(',').Select(s => s.Trim()));
            inputs.Add($"labelIds: [\"{ids}\"]");
        }

        if (parameters.Has("estimate"))
            inputs.Add($"estimate: {parameters.GetInt("estimate", 0)}");

        var dueDate = parameters.GetString("dueDate");
        if (!string.IsNullOrEmpty(dueDate))
            inputs.Add($"dueDate: \"{dueDate}\"");

        var mutation = $$"""
        mutation {
          issueCreate(input: { {{string.Join(", ", inputs)}} }) {
            success
            issue {
              id
              identifier
              title
              url
            }
          }
        }
        """;

        return await ExecuteGraphQLAsync(mutation, ct);
    }

    private async Task<ActionResult> UpdateIssueAsync(ActionParameters parameters, CancellationToken ct)
    {
        var issueId = parameters.GetString("issueId")!;
        var inputs = new List<string>();

        var title = parameters.GetString("title");
        if (!string.IsNullOrEmpty(title))
            inputs.Add($"title: \"{EscapeGraphQL(title)}\"");

        var description = parameters.GetString("description");
        if (!string.IsNullOrEmpty(description))
            inputs.Add($"description: \"{EscapeGraphQL(description)}\"");

        if (parameters.Has("priority"))
            inputs.Add($"priority: {parameters.GetInt("priority", 0)}");

        var assigneeId = parameters.GetString("assigneeId");
        if (!string.IsNullOrEmpty(assigneeId))
            inputs.Add($"assigneeId: \"{assigneeId}\"");

        var stateId = parameters.GetString("stateId");
        if (!string.IsNullOrEmpty(stateId))
            inputs.Add($"stateId: \"{stateId}\"");

        if (parameters.Has("estimate"))
            inputs.Add($"estimate: {parameters.GetInt("estimate", 0)}");

        var dueDate = parameters.GetString("dueDate");
        if (!string.IsNullOrEmpty(dueDate))
            inputs.Add($"dueDate: \"{dueDate}\"");

        if (inputs.Count == 0)
            return ActionResult.Fail("No fields to update");

        var mutation = $$"""
        mutation {
          issueUpdate(id: "{{issueId}}", input: { {{string.Join(", ", inputs)}} }) {
            success
            issue {
              id
              identifier
              title
            }
          }
        }
        """;

        return await ExecuteGraphQLAsync(mutation, ct);
    }

    private async Task<ActionResult> DeleteIssueAsync(ActionParameters parameters, CancellationToken ct)
    {
        var issueId = parameters.GetString("issueId")!;

        var mutation = $$"""
        mutation {
          issueArchive(id: "{{issueId}}") {
            success
          }
        }
        """;

        return await ExecuteGraphQLAsync(mutation, ct);
    }

    private async Task<ActionResult> AddCommentAsync(ActionParameters parameters, CancellationToken ct)
    {
        var issueId = parameters.GetString("issueId")!;
        var body = parameters.GetString("body")!;

        var mutation = $$"""
        mutation {
          commentCreate(input: { issueId: "{{issueId}}", body: "{{EscapeGraphQL(body)}}" }) {
            success
            comment {
              id
              body
              createdAt
            }
          }
        }
        """;

        return await ExecuteGraphQLAsync(mutation, ct);
    }

    private async Task<ActionResult> GetCommentsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var issueId = parameters.GetString("issueId")!;

        var query = $$"""
        {
          issue(id: "{{issueId}}") {
            comments {
              nodes {
                id
                body
                createdAt
                updatedAt
                user { id name email }
              }
            }
          }
        }
        """;

        return await ExecuteGraphQLAsync(query, ct);
    }

    private async Task<ActionResult> GetProjectsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var first = parameters.GetInt("first", 50);

        var query = $$"""
        {
          projects(first: {{first}}) {
            nodes {
              id
              name
              description
              state
              targetDate
              createdAt
              teams { nodes { id name } }
            }
          }
        }
        """;

        return await ExecuteGraphQLAsync(query, ct);
    }

    private async Task<ActionResult> CreateProjectAsync(ActionParameters parameters, CancellationToken ct)
    {
        var name = parameters.GetString("name")!;
        var teamIds = parameters.GetString("teamIds")!;
        var state = parameters.GetString("state") ?? "planned";

        var inputs = new List<string>
        {
            $"name: \"{EscapeGraphQL(name)}\"",
            $"teamIds: [\"{string.Join("\", \"", teamIds.Split(',').Select(s => s.Trim()))}\"]",
            $"state: \"{state}\""
        };

        var description = parameters.GetString("description");
        if (!string.IsNullOrEmpty(description))
            inputs.Add($"description: \"{EscapeGraphQL(description)}\"");

        var targetDate = parameters.GetString("targetDate");
        if (!string.IsNullOrEmpty(targetDate))
            inputs.Add($"targetDate: \"{targetDate}\"");

        var mutation = $$"""
        mutation {
          projectCreate(input: { {{string.Join(", ", inputs)}} }) {
            success
            project {
              id
              name
              url
            }
          }
        }
        """;

        return await ExecuteGraphQLAsync(mutation, ct);
    }

    private async Task<ActionResult> UpdateProjectAsync(ActionParameters parameters, CancellationToken ct)
    {
        var projectId = parameters.GetString("projectId")!;
        var inputs = new List<string>();

        var name = parameters.GetString("name");
        if (!string.IsNullOrEmpty(name))
            inputs.Add($"name: \"{EscapeGraphQL(name)}\"");

        var description = parameters.GetString("description");
        if (!string.IsNullOrEmpty(description))
            inputs.Add($"description: \"{EscapeGraphQL(description)}\"");

        var state = parameters.GetString("state");
        if (!string.IsNullOrEmpty(state))
            inputs.Add($"state: \"{state}\"");

        var targetDate = parameters.GetString("targetDate");
        if (!string.IsNullOrEmpty(targetDate))
            inputs.Add($"targetDate: \"{targetDate}\"");

        if (inputs.Count == 0)
            return ActionResult.Fail("No fields to update");

        var mutation = $$"""
        mutation {
          projectUpdate(id: "{{projectId}}", input: { {{string.Join(", ", inputs)}} }) {
            success
            project {
              id
              name
            }
          }
        }
        """;

        return await ExecuteGraphQLAsync(mutation, ct);
    }

    private async Task<ActionResult> GetTeamsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var first = parameters.GetInt("first", 50);

        var query = $$"""
        {
          teams(first: {{first}}) {
            nodes {
              id
              name
              key
              description
            }
          }
        }
        """;

        return await ExecuteGraphQLAsync(query, ct);
    }

    private async Task<ActionResult> GetTeamAsync(ActionParameters parameters, CancellationToken ct)
    {
        var teamId = parameters.GetString("teamId")!;

        var query = $$"""
        {
          team(id: "{{teamId}}") {
            id
            name
            key
            description
            members { nodes { id name email } }
          }
        }
        """;

        return await ExecuteGraphQLAsync(query, ct);
    }

    private async Task<ActionResult> GetUsersAsync(ActionParameters parameters, CancellationToken ct)
    {
        var first = parameters.GetInt("first", 50);

        var query = $$"""
        {
          users(first: {{first}}) {
            nodes {
              id
              name
              email
              displayName
              active
            }
          }
        }
        """;

        return await ExecuteGraphQLAsync(query, ct);
    }

    private async Task<ActionResult> GetViewerAsync(CancellationToken ct)
    {
        var query = """
        {
          viewer {
            id
            name
            email
            displayName
          }
        }
        """;

        return await ExecuteGraphQLAsync(query, ct);
    }

    private async Task<ActionResult> GetLabelsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var first = parameters.GetInt("first", 50);

        var query = $$"""
        {
          issueLabels(first: {{first}}) {
            nodes {
              id
              name
              color
              description
            }
          }
        }
        """;

        return await ExecuteGraphQLAsync(query, ct);
    }

    private async Task<ActionResult> CreateLabelAsync(ActionParameters parameters, CancellationToken ct)
    {
        var name = parameters.GetString("name")!;
        var inputs = new List<string> { $"name: \"{EscapeGraphQL(name)}\"" };

        var color = parameters.GetString("color");
        if (!string.IsNullOrEmpty(color))
            inputs.Add($"color: \"{color}\"");

        var teamId = parameters.GetString("teamId");
        if (!string.IsNullOrEmpty(teamId))
            inputs.Add($"teamId: \"{teamId}\"");

        var mutation = $$"""
        mutation {
          issueLabelCreate(input: { {{string.Join(", ", inputs)}} }) {
            success
            issueLabel {
              id
              name
              color
            }
          }
        }
        """;

        return await ExecuteGraphQLAsync(mutation, ct);
    }

    private async Task<ActionResult> GetWorkflowStatesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var teamId = parameters.GetString("teamId");
        var filterClause = !string.IsNullOrEmpty(teamId) ? $", filter: {{ team: {{ id: {{ eq: \"{teamId}\" }} }} }}" : "";

        var query = $$"""
        {
          workflowStates(first: 100{{filterClause}}) {
            nodes {
              id
              name
              type
              position
              color
              team { id name }
            }
          }
        }
        """;

        return await ExecuteGraphQLAsync(query, ct);
    }

    private async Task<ActionResult> SearchIssuesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var query = parameters.GetString("query")!;
        var first = parameters.GetInt("first", 50);

        var graphQLQuery = $$"""
        {
          issueSearch(query: "{{EscapeGraphQL(query)}}", first: {{first}}) {
            nodes {
              id
              identifier
              title
              description
              priority
              state { name }
              assignee { name }
              team { name }
            }
          }
        }
        """;

        return await ExecuteGraphQLAsync(graphQLQuery, ct);
    }

    private async Task<ActionResult> ExecuteGraphQLAsync(string query, CancellationToken ct)
    {
        var payload = new { query };
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        var response = await _httpClient!.PostAsync("", content, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                return ActionResult.Ok(data ?? new Dictionary<string, object>());
            }
            catch
            {
                return ActionResult.Ok(new Dictionary<string, object> { ["response"] = responseContent });
            }
        }

        return ActionResult.Fail($"Linear error ({response.StatusCode}): {responseContent}");
    }

    private static string EscapeGraphQL(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
