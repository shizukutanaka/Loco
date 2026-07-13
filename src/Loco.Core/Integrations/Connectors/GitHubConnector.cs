// John Carmack: "If you want to go fast, go alone. If you want to go far, go together."
// Linus Torvalds: "Talk is cheap. Show me the code."

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;
using Loco.Core.Practical;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// GitHub connector for repository, issue, and PR operations
/// Uses GitHub REST API v2022-11-28
/// </summary>
public sealed class GitHubConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private const string GitHubApiBase = "https://api.github.com";
    private const string ApiVersion = "2022-11-28";

    public override string Id => "github";
    public override string Name => "GitHub";
    public override string Description => "GitHub repository, issue, pull request, and workflow management";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.DevOps;

    public override ConnectorCapabilities Capabilities => new()
    {
        SupportsActions = true,
        SupportsTriggers = true,
        SupportsWebhooks = true,
        RateLimitPerMinute = 5000, // GitHub API limit for authenticated requests
        DefaultTimeout = TimeSpan.FromSeconds(30)
    };

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.Bearer,
        RequiredCredentials =
        [
            new() { Name = "token", Label = "Personal Access Token", Type = ParameterType.Password, Required = true,
                Description = "GitHub PAT with appropriate scopes" }
        ]
    };

    public override IReadOnlyList<ConfigParameter> ConfigParameters =>
    [
        new() { Name = "baseUrl", Label = "Base URL", Type = ParameterType.String,
            Description = "GitHub Enterprise URL (leave empty for github.com)" }
    ];

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // Repository operations
        new()
        {
            Id = "getRepo",
            Name = "Get Repository",
            Description = "Get repository information",
            Parameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "listRepos",
            Name = "List Repositories",
            Description = "List repositories for a user or organization",
            Parameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "type", Type = ParameterType.Select, DefaultValue = "all",
                    Options =
                    [
                        new() { Label = "All", Value = "all" },
                        new() { Label = "Public", Value = "public" },
                        new() { Label = "Private", Value = "private" },
                        new() { Label = "Forks", Value = "forks" },
                        new() { Label = "Sources", Value = "sources" }
                    ]},
                new() { Name = "perPage", Type = ParameterType.Number, DefaultValue = 30 }
            ]
        },
        // Issue operations
        new()
        {
            Id = "createIssue",
            Name = "Create Issue",
            Description = "Create a new issue",
            Parameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true },
                new() { Name = "title", Type = ParameterType.String, Required = true },
                new() { Name = "body", Type = ParameterType.String },
                new() { Name = "labels", Type = ParameterType.Json, Description = "Array of label names" },
                new() { Name = "assignees", Type = ParameterType.Json, Description = "Array of usernames" }
            ]
        },
        new()
        {
            Id = "updateIssue",
            Name = "Update Issue",
            Description = "Update an existing issue",
            Parameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true },
                new() { Name = "issueNumber", Type = ParameterType.Number, Required = true },
                new() { Name = "title", Type = ParameterType.String },
                new() { Name = "body", Type = ParameterType.String },
                new() { Name = "state", Type = ParameterType.Select,
                    Options =
                    [
                        new() { Label = "Open", Value = "open" },
                        new() { Label = "Closed", Value = "closed" }
                    ]},
                new() { Name = "labels", Type = ParameterType.Json },
                new() { Name = "assignees", Type = ParameterType.Json }
            ]
        },
        new()
        {
            Id = "listIssues",
            Name = "List Issues",
            Description = "List repository issues",
            Parameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true },
                new() { Name = "state", Type = ParameterType.Select, DefaultValue = "open",
                    Options =
                    [
                        new() { Label = "Open", Value = "open" },
                        new() { Label = "Closed", Value = "closed" },
                        new() { Label = "All", Value = "all" }
                    ]},
                new() { Name = "labels", Type = ParameterType.String, Description = "Comma-separated labels" },
                new() { Name = "perPage", Type = ParameterType.Number, DefaultValue = 30 }
            ]
        },
        new()
        {
            Id = "addIssueComment",
            Name = "Add Issue Comment",
            Description = "Add a comment to an issue or PR",
            Parameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true },
                new() { Name = "issueNumber", Type = ParameterType.Number, Required = true },
                new() { Name = "body", Type = ParameterType.String, Required = true }
            ]
        },
        // Pull Request operations
        new()
        {
            Id = "createPR",
            Name = "Create Pull Request",
            Description = "Create a new pull request",
            Parameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true },
                new() { Name = "title", Type = ParameterType.String, Required = true },
                new() { Name = "head", Type = ParameterType.String, Required = true, Description = "Branch to merge from" },
                new() { Name = "base", Type = ParameterType.String, Required = true, Description = "Branch to merge into" },
                new() { Name = "body", Type = ParameterType.String },
                new() { Name = "draft", Type = ParameterType.Boolean, DefaultValue = false }
            ]
        },
        new()
        {
            Id = "mergePR",
            Name = "Merge Pull Request",
            Description = "Merge a pull request",
            Parameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true },
                new() { Name = "pullNumber", Type = ParameterType.Number, Required = true },
                new() { Name = "mergeMethod", Type = ParameterType.Select, DefaultValue = "merge",
                    Options =
                    [
                        new() { Label = "Merge", Value = "merge" },
                        new() { Label = "Squash", Value = "squash" },
                        new() { Label = "Rebase", Value = "rebase" }
                    ]},
                new() { Name = "commitTitle", Type = ParameterType.String },
                new() { Name = "commitMessage", Type = ParameterType.String }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "listPRs",
            Name = "List Pull Requests",
            Description = "List repository pull requests",
            Parameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true },
                new() { Name = "state", Type = ParameterType.Select, DefaultValue = "open",
                    Options =
                    [
                        new() { Label = "Open", Value = "open" },
                        new() { Label = "Closed", Value = "closed" },
                        new() { Label = "All", Value = "all" }
                    ]},
                new() { Name = "perPage", Type = ParameterType.Number, DefaultValue = 30 }
            ]
        },
        // Workflow operations
        new()
        {
            Id = "triggerWorkflow",
            Name = "Trigger Workflow",
            Description = "Trigger a GitHub Actions workflow",
            Parameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true },
                new() { Name = "workflowId", Type = ParameterType.String, Required = true, Description = "Workflow file name or ID" },
                new() { Name = "ref", Type = ParameterType.String, Required = true, Description = "Branch or tag" },
                new() { Name = "inputs", Type = ParameterType.Json, Description = "Workflow inputs" }
            ]
        },
        new()
        {
            Id = "listWorkflowRuns",
            Name = "List Workflow Runs",
            Description = "List workflow runs for a repository",
            Parameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true },
                new() { Name = "workflowId", Type = ParameterType.String, Description = "Filter by workflow" },
                new() { Name = "status", Type = ParameterType.Select,
                    Options =
                    [
                        new() { Label = "Completed", Value = "completed" },
                        new() { Label = "In Progress", Value = "in_progress" },
                        new() { Label = "Queued", Value = "queued" }
                    ]},
                new() { Name = "perPage", Type = ParameterType.Number, DefaultValue = 30 }
            ]
        },
        // Content operations
        new()
        {
            Id = "getContent",
            Name = "Get File Content",
            Description = "Get contents of a file or directory",
            Parameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true },
                new() { Name = "path", Type = ParameterType.String, Required = true },
                new() { Name = "ref", Type = ParameterType.String, Description = "Branch, tag, or SHA" }
            ]
        },
        new()
        {
            Id = "createOrUpdateFile",
            Name = "Create or Update File",
            Description = "Create or update a file in the repository",
            Parameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true },
                new() { Name = "path", Type = ParameterType.String, Required = true },
                new() { Name = "content", Type = ParameterType.Code, Required = true },
                new() { Name = "message", Type = ParameterType.String, Required = true, Description = "Commit message" },
                new() { Name = "branch", Type = ParameterType.String, Description = "Branch name" },
                new() { Name = "sha", Type = ParameterType.String, Description = "SHA of file to update (required for updates)" }
            ]
        },
        // Release operations
        new()
        {
            Id = "createRelease",
            Name = "Create Release",
            Description = "Create a new release",
            Parameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true },
                new() { Name = "tagName", Type = ParameterType.String, Required = true },
                new() { Name = "name", Type = ParameterType.String },
                new() { Name = "body", Type = ParameterType.String, Description = "Release notes" },
                new() { Name = "draft", Type = ParameterType.Boolean, DefaultValue = false },
                new() { Name = "prerelease", Type = ParameterType.Boolean, DefaultValue = false }
            ]
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "push",
            Name = "Push Event",
            Description = "Triggered when commits are pushed",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true },
                new() { Name = "branch", Type = ParameterType.String, Description = "Filter by branch" }
            ]
        },
        new()
        {
            Id = "pullRequest",
            Name = "Pull Request Event",
            Description = "Triggered on PR actions",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true },
                new() { Name = "actions", Type = ParameterType.Json, Description = "Filter by actions (opened, closed, etc.)" }
            ]
        },
        new()
        {
            Id = "issue",
            Name = "Issue Event",
            Description = "Triggered on issue actions",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "workflowRun",
            Name = "Workflow Run Event",
            Description = "Triggered when workflow runs complete",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "owner", Type = ParameterType.String, Required = true },
                new() { Name = "repo", Type = ParameterType.String, Required = true },
                new() { Name = "workflowName", Type = ParameterType.String }
            ]
        }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        var token = config.GetCredentialString("token")!;
        var baseUrl = config.GetSettingString("baseUrl");

        // Dispose any previous client before replacing it. InitializeAsync can run more
        // than once for the same cached connector instance (e.g. ConnectorRegistry.
        // GetInitializedConnectorAsync on credential rotation); overwriting _httpClient
        // unconditionally previously leaked the old HttpClient and its socket handler.
        _httpClient?.Dispose();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(string.IsNullOrEmpty(baseUrl) ? GitHubApiBase : baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", ApiVersion);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Loco", "1.0"));

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
            "getRepo" => await GetRepoAsync(parameters, ct),
            "listRepos" => await ListReposAsync(parameters, ct),
            "createIssue" => await CreateIssueAsync(parameters, ct),
            "updateIssue" => await UpdateIssueAsync(parameters, ct),
            "listIssues" => await ListIssuesAsync(parameters, ct),
            "addIssueComment" => await AddIssueCommentAsync(parameters, ct),
            "createPR" => await CreatePRAsync(parameters, ct),
            "mergePR" => await MergePRAsync(parameters, ct),
            "listPRs" => await ListPRsAsync(parameters, ct),
            "triggerWorkflow" => await TriggerWorkflowAsync(parameters, ct),
            "listWorkflowRuns" => await ListWorkflowRunsAsync(parameters, ct),
            "getContent" => await GetContentAsync(parameters, ct),
            "createOrUpdateFile" => await CreateOrUpdateFileAsync(parameters, ct),
            "createRelease" => await CreateReleaseAsync(parameters, ct),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> GetRepoAsync(ActionParameters parameters, CancellationToken ct)
    {
        var owner = parameters.GetString("owner")!;
        var repo = parameters.GetString("repo")!;
        return await GetAsync($"/repos/{owner}/{repo}", ct);
    }

    private async Task<ActionResult> ListReposAsync(ActionParameters parameters, CancellationToken ct)
    {
        var owner = parameters.GetString("owner")!;
        var type = parameters.GetString("type") ?? "all";
        var perPage = parameters.GetInt("perPage", 30);
        return await GetAsync($"/users/{owner}/repos?type={type}&per_page={perPage}", ct);
    }

    private async Task<ActionResult> CreateIssueAsync(ActionParameters parameters, CancellationToken ct)
    {
        var owner = parameters.GetString("owner")!;
        var repo = parameters.GetString("repo")!;

        var body = new Dictionary<string, object?> { ["title"] = parameters.GetString("title") };
        if (parameters.Contains("body")) body["body"] = parameters.GetString("body");
        if (parameters.Contains("labels")) body["labels"] = parameters.Get<List<string>>("labels");
        if (parameters.Contains("assignees")) body["assignees"] = parameters.Get<List<string>>("assignees");

        return await PostAsync($"/repos/{owner}/{repo}/issues", body, ct);
    }

    private async Task<ActionResult> UpdateIssueAsync(ActionParameters parameters, CancellationToken ct)
    {
        var owner = parameters.GetString("owner")!;
        var repo = parameters.GetString("repo")!;
        var issueNumber = parameters.GetInt("issueNumber", 0);

        var body = new Dictionary<string, object?>();
        if (parameters.Contains("title")) body["title"] = parameters.GetString("title");
        if (parameters.Contains("body")) body["body"] = parameters.GetString("body");
        if (parameters.Contains("state")) body["state"] = parameters.GetString("state");
        if (parameters.Contains("labels")) body["labels"] = parameters.Get<List<string>>("labels");
        if (parameters.Contains("assignees")) body["assignees"] = parameters.Get<List<string>>("assignees");

        return await PatchAsync($"/repos/{owner}/{repo}/issues/{issueNumber}", body, ct);
    }

    private async Task<ActionResult> ListIssuesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var owner = parameters.GetString("owner")!;
        var repo = parameters.GetString("repo")!;
        var state = parameters.GetString("state") ?? "open";
        var labels = parameters.GetString("labels");
        var perPage = parameters.GetInt("perPage", 30);

        var url = $"/repos/{owner}/{repo}/issues?state={state}&per_page={perPage}";
        if (!string.IsNullOrEmpty(labels)) url += $"&labels={labels}";

        return await GetAsync(url, ct);
    }

    private async Task<ActionResult> AddIssueCommentAsync(ActionParameters parameters, CancellationToken ct)
    {
        var owner = parameters.GetString("owner")!;
        var repo = parameters.GetString("repo")!;
        var issueNumber = parameters.GetInt("issueNumber", 0);

        var body = new Dictionary<string, object?> { ["body"] = parameters.GetString("body") };
        return await PostAsync($"/repos/{owner}/{repo}/issues/{issueNumber}/comments", body, ct);
    }

    private async Task<ActionResult> CreatePRAsync(ActionParameters parameters, CancellationToken ct)
    {
        var owner = parameters.GetString("owner")!;
        var repo = parameters.GetString("repo")!;

        var body = new Dictionary<string, object?>
        {
            ["title"] = parameters.GetString("title"),
            ["head"] = parameters.GetString("head"),
            ["base"] = parameters.GetString("base"),
            ["draft"] = parameters.GetBool("draft", false)
        };
        if (parameters.Contains("body")) body["body"] = parameters.GetString("body");

        return await PostAsync($"/repos/{owner}/{repo}/pulls", body, ct);
    }

    private async Task<ActionResult> MergePRAsync(ActionParameters parameters, CancellationToken ct)
    {
        var owner = parameters.GetString("owner")!;
        var repo = parameters.GetString("repo")!;
        var pullNumber = parameters.GetInt("pullNumber", 0);

        var body = new Dictionary<string, object?>
        {
            ["merge_method"] = parameters.GetString("mergeMethod") ?? "merge"
        };
        if (parameters.Contains("commitTitle")) body["commit_title"] = parameters.GetString("commitTitle");
        if (parameters.Contains("commitMessage")) body["commit_message"] = parameters.GetString("commitMessage");

        return await PutAsync($"/repos/{owner}/{repo}/pulls/{pullNumber}/merge", body, ct);
    }

    private async Task<ActionResult> ListPRsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var owner = parameters.GetString("owner")!;
        var repo = parameters.GetString("repo")!;
        var state = parameters.GetString("state") ?? "open";
        var perPage = parameters.GetInt("perPage", 30);

        return await GetAsync($"/repos/{owner}/{repo}/pulls?state={state}&per_page={perPage}", ct);
    }

    private async Task<ActionResult> TriggerWorkflowAsync(ActionParameters parameters, CancellationToken ct)
    {
        var owner = parameters.GetString("owner")!;
        var repo = parameters.GetString("repo")!;
        var workflowId = parameters.GetString("workflowId")!;
        var refValue = parameters.GetString("ref")!;

        var body = new Dictionary<string, object?> { ["ref"] = refValue };
        if (parameters.Contains("inputs")) body["inputs"] = parameters.Get<object>("inputs");

        return await PostAsync($"/repos/{owner}/{repo}/actions/workflows/{workflowId}/dispatches", body, ct);
    }

    private async Task<ActionResult> ListWorkflowRunsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var owner = parameters.GetString("owner")!;
        var repo = parameters.GetString("repo")!;
        var workflowId = parameters.GetString("workflowId");
        var status = parameters.GetString("status");
        var perPage = parameters.GetInt("perPage", 30);

        var url = workflowId != null
            ? $"/repos/{owner}/{repo}/actions/workflows/{workflowId}/runs"
            : $"/repos/{owner}/{repo}/actions/runs";

        url += $"?per_page={perPage}";
        if (!string.IsNullOrEmpty(status)) url += $"&status={status}";

        return await GetAsync(url, ct);
    }

    private async Task<ActionResult> GetContentAsync(ActionParameters parameters, CancellationToken ct)
    {
        var owner = parameters.GetString("owner")!;
        var repo = parameters.GetString("repo")!;
        var path = parameters.GetString("path")!;
        var refValue = parameters.GetString("ref");

        var url = $"/repos/{owner}/{repo}/contents/{path}";
        if (!string.IsNullOrEmpty(refValue)) url += $"?ref={refValue}";

        return await GetAsync(url, ct);
    }

    private async Task<ActionResult> CreateOrUpdateFileAsync(ActionParameters parameters, CancellationToken ct)
    {
        var owner = parameters.GetString("owner")!;
        var repo = parameters.GetString("repo")!;
        var path = parameters.GetString("path")!;
        var content = parameters.GetString("content")!;
        var message = parameters.GetString("message")!;

        var body = new Dictionary<string, object?>
        {
            ["message"] = message,
            ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content))
        };

        if (parameters.Contains("branch")) body["branch"] = parameters.GetString("branch");
        if (parameters.Contains("sha")) body["sha"] = parameters.GetString("sha");

        return await PutAsync($"/repos/{owner}/{repo}/contents/{path}", body, ct);
    }

    private async Task<ActionResult> CreateReleaseAsync(ActionParameters parameters, CancellationToken ct)
    {
        var owner = parameters.GetString("owner")!;
        var repo = parameters.GetString("repo")!;

        var body = new Dictionary<string, object?>
        {
            ["tag_name"] = parameters.GetString("tagName"),
            ["draft"] = parameters.GetBool("draft", false),
            ["prerelease"] = parameters.GetBool("prerelease", false)
        };

        if (parameters.Contains("name")) body["name"] = parameters.GetString("name");
        if (parameters.Contains("body")) body["body"] = parameters.GetString("body");

        return await PostAsync($"/repos/{owner}/{repo}/releases", body, ct);
    }

    private async Task<ActionResult> GetAsync(string url, CancellationToken ct)
    {
        var response = await _httpClient!.GetAsync(url, ct);
        return await ParseResponseAsync(response, ct);
    }

    private async Task<ActionResult> PostAsync(string url, object body, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync(url, content, ct);
        return await ParseResponseAsync(response, ct);
    }

    private async Task<ActionResult> PutAsync(string url, object body, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient!.PutAsync(url, content, ct);
        return await ParseResponseAsync(response, ct);
    }

    private async Task<ActionResult> PatchAsync(string url, object body, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
        var response = await _httpClient!.SendAsync(request, ct);
        return await ParseResponseAsync(response, ct);
    }

    private static async Task<ActionResult> ParseResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                var error = JsonSerializer.Deserialize<JsonElement>(content);
                var message = error.TryGetProperty("message", out var msg) ? msg.GetString() : content;
                return ActionResult.Fail($"GitHub API error: {message}", ((int)response.StatusCode).ToString());
            }
            catch
            {
                return ActionResult.Fail($"GitHub API error: {response.StatusCode}", ((int)response.StatusCode).ToString());
            }
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return ActionResult.Ok(new { success = true });
        }

        try
        {
            var data = JsonSerializer.Deserialize<object>(content);
            return ActionResult.Ok(data);
        }
        catch
        {
            return ActionResult.Ok(new { response = content });
        }
    }

    public override async Task CleanupAsync(CancellationToken ct = default)
    {
        _httpClient?.Dispose();
        _httpClient = null;
        await base.CleanupAsync(ct);
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
