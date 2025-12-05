using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Notion connector for managing pages, databases, and blocks.
/// Uses Notion API v1.
/// </summary>
public sealed class NotionConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private const string NotionVersion = "2022-06-28";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public override string Id => "notion";
    public override string Name => "Notion";
    public override string Description => "All-in-one workspace for notes, docs, wikis, and databases";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Productivity;
    public override string IconUrl => "https://www.notion.so/images/favicon.ico";

    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.ForApi();

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.ApiKey,
        RequiredCredentials = new CredentialField[]
        {
            new() { Name = "apiKey", Label = "Integration Token", Type = ParameterType.Password, Description = "Internal integration token from Notion" }
        }
    };

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        new()
        {
            Id = "createPage",
            Name = "Create Page",
            Description = "Create a new page in a database or as a child of another page",
            Parameters = new ActionParameter[]
            {
                new() { Name = "parentType", Type = ParameterType.String, Required = true, Description = "database_id or page_id" },
                new() { Name = "parentId", Type = ParameterType.String, Required = true },
                new() { Name = "properties", Type = ParameterType.Json, Required = true, Description = "Page properties" },
                new() { Name = "children", Type = ParameterType.Json, Description = "Initial page content blocks" },
                new() { Name = "icon", Type = ParameterType.String, Description = "Emoji or URL" }
            }
        },
        new()
        {
            Id = "getPage",
            Name = "Get Page",
            Description = "Retrieve a page by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "pageId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "updatePage",
            Name = "Update Page",
            Description = "Update page properties",
            Parameters = new ActionParameter[]
            {
                new() { Name = "pageId", Type = ParameterType.String, Required = true },
                new() { Name = "properties", Type = ParameterType.Json },
                new() { Name = "archived", Type = ParameterType.Boolean }
            }
        },
        new()
        {
            Id = "archivePage",
            Name = "Archive Page",
            Description = "Archive (delete) a page",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "pageId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "queryDatabase",
            Name = "Query Database",
            Description = "Query a database with filters and sorts",
            Parameters = new ActionParameter[]
            {
                new() { Name = "databaseId", Type = ParameterType.String, Required = true },
                new() { Name = "filter", Type = ParameterType.Json, Description = "Notion filter object" },
                new() { Name = "sorts", Type = ParameterType.Json, Description = "Array of sort objects" },
                new() { Name = "pageSize", Type = ParameterType.Number, DefaultValue = 100 },
                new() { Name = "startCursor", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "getDatabase",
            Name = "Get Database",
            Description = "Retrieve database metadata and schema",
            Parameters = new ActionParameter[]
            {
                new() { Name = "databaseId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "getBlockChildren",
            Name = "Get Block Children",
            Description = "Retrieve children blocks of a block or page",
            Parameters = new ActionParameter[]
            {
                new() { Name = "blockId", Type = ParameterType.String, Required = true },
                new() { Name = "pageSize", Type = ParameterType.Number, DefaultValue = 100 },
                new() { Name = "startCursor", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "appendBlockChildren",
            Name = "Append Block Children",
            Description = "Append new children blocks to a parent",
            Parameters = new ActionParameter[]
            {
                new() { Name = "blockId", Type = ParameterType.String, Required = true },
                new() { Name = "children", Type = ParameterType.Json, Required = true }
            }
        },
        new()
        {
            Id = "search",
            Name = "Search",
            Description = "Search pages and databases",
            Parameters = new ActionParameter[]
            {
                new() { Name = "query", Type = ParameterType.String, Description = "Text to search for" },
                new() { Name = "filter", Type = ParameterType.String, Description = "page or database" },
                new() { Name = "pageSize", Type = ParameterType.Number, DefaultValue = 100 }
            }
        },
        new()
        {
            Id = "listUsers",
            Name = "List Users",
            Description = "List all users in the workspace",
            Parameters = new ActionParameter[]
            {
                new() { Name = "pageSize", Type = ParameterType.Number, DefaultValue = 100 }
            }
        },
        new()
        {
            Id = "getMe",
            Name = "Get Bot User",
            Description = "Retrieve the bot user information",
            Parameters = Array.Empty<ActionParameter>()
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "pageCreated",
            Name = "Page Created",
            Description = "Triggered when a new page is created in a database",
            Type = TriggerType.Polling
        },
        new()
        {
            Id = "pageUpdated",
            Name = "Page Updated",
            Description = "Triggered when a page is updated",
            Type = TriggerType.Polling
        }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        await base.InitializeAsync(config, ct);

        var apiKey = config.GetCredentialString("apiKey");

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.notion.com/v1/")
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Add("Notion-Version", NotionVersion);
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
            "createPage" => await CreatePageAsync(parameters, ct),
            "getPage" => await GetAsync($"pages/{parameters.GetString("pageId")}", ct),
            "updatePage" => await UpdatePageAsync(parameters, ct),
            "archivePage" => await PatchAsync($"pages/{parameters.GetString("pageId")}", new { archived = true }, ct),
            "queryDatabase" => await QueryDatabaseAsync(parameters, ct),
            "getDatabase" => await GetAsync($"databases/{parameters.GetString("databaseId")}", ct),
            "getBlockChildren" => await GetBlockChildrenAsync(parameters, ct),
            "appendBlockChildren" => await AppendBlockChildrenAsync(parameters, ct),
            "search" => await SearchAsync(parameters, ct),
            "listUsers" => await ListUsersAsync(parameters, ct),
            "getMe" => await GetAsync("users/me", ct),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> CreatePageAsync(ActionParameters parameters, CancellationToken ct)
    {
        var parentType = parameters.GetString("parentType")!;
        var parentId = parameters.GetString("parentId")!;

        var payload = new Dictionary<string, object>
        {
            ["parent"] = new Dictionary<string, object> { [parentType] = parentId },
            ["properties"] = parameters.Get<JsonElement>("properties")
        };

        var children = parameters.Get<JsonElement?>("children");
        if (children.HasValue && children.Value.ValueKind != JsonValueKind.Undefined)
            payload["children"] = children.Value;

        var iconStr = parameters.GetString("icon");
        if (!string.IsNullOrEmpty(iconStr))
        {
            payload["icon"] = iconStr.StartsWith("http")
                ? new Dictionary<string, object> { ["type"] = "external", ["external"] = new { url = iconStr } }
                : new Dictionary<string, object> { ["type"] = "emoji", ["emoji"] = iconStr };
        }

        return await PostAsync("pages", payload, ct);
    }

    private async Task<ActionResult> UpdatePageAsync(ActionParameters parameters, CancellationToken ct)
    {
        var pageId = parameters.GetString("pageId")!;
        var payload = new Dictionary<string, object>();

        var properties = parameters.Get<JsonElement?>("properties");
        if (properties.HasValue && properties.Value.ValueKind != JsonValueKind.Undefined)
            payload["properties"] = properties.Value;

        if (parameters.GetBool("archived"))
            payload["archived"] = true;

        return await PatchAsync($"pages/{pageId}", payload, ct);
    }

    private async Task<ActionResult> QueryDatabaseAsync(ActionParameters parameters, CancellationToken ct)
    {
        var databaseId = parameters.GetString("databaseId")!;
        var payload = new Dictionary<string, object>();

        var filter = parameters.Get<JsonElement?>("filter");
        if (filter.HasValue && filter.Value.ValueKind != JsonValueKind.Undefined)
            payload["filter"] = filter.Value;

        var sorts = parameters.Get<JsonElement?>("sorts");
        if (sorts.HasValue && sorts.Value.ValueKind != JsonValueKind.Undefined)
            payload["sorts"] = sorts.Value;

        var pageSize = parameters.GetInt("pageSize");
        if (pageSize > 0)
            payload["page_size"] = pageSize;

        var cursor = parameters.GetString("startCursor");
        if (!string.IsNullOrEmpty(cursor))
            payload["start_cursor"] = cursor;

        return await PostAsync($"databases/{databaseId}/query", payload, ct);
    }

    private async Task<ActionResult> GetBlockChildrenAsync(ActionParameters parameters, CancellationToken ct)
    {
        var blockId = parameters.GetString("blockId")!;
        var queryParams = new List<string>();

        var pageSize = parameters.GetInt("pageSize");
        if (pageSize > 0)
            queryParams.Add($"page_size={pageSize}");

        var cursor = parameters.GetString("startCursor");
        if (!string.IsNullOrEmpty(cursor))
            queryParams.Add($"start_cursor={cursor}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await GetAsync($"blocks/{blockId}/children{query}", ct);
    }

    private async Task<ActionResult> AppendBlockChildrenAsync(ActionParameters parameters, CancellationToken ct)
    {
        var blockId = parameters.GetString("blockId")!;
        var payload = new Dictionary<string, object>
        {
            ["children"] = parameters.Get<JsonElement>("children")
        };
        return await PatchAsync($"blocks/{blockId}/children", payload, ct);
    }

    private async Task<ActionResult> SearchAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = new Dictionary<string, object>();

        var query = parameters.GetString("query");
        if (!string.IsNullOrEmpty(query))
            payload["query"] = query;

        var filterType = parameters.GetString("filter");
        if (!string.IsNullOrEmpty(filterType))
        {
            payload["filter"] = new Dictionary<string, object>
            {
                ["property"] = "object",
                ["value"] = filterType
            };
        }

        var pageSize = parameters.GetInt("pageSize");
        if (pageSize > 0)
            payload["page_size"] = pageSize;

        return await PostAsync("search", payload, ct);
    }

    private async Task<ActionResult> ListUsersAsync(ActionParameters parameters, CancellationToken ct)
    {
        var queryParams = new List<string>();
        var pageSize = parameters.GetInt("pageSize");
        if (pageSize > 0)
            queryParams.Add($"page_size={pageSize}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await GetAsync($"users{query}", ct);
    }

    private async Task<ActionResult> GetAsync(string endpoint, CancellationToken ct)
    {
        var response = await _httpClient!.GetAsync(endpoint, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ActionResult> PostAsync(string endpoint, object payload, CancellationToken ct)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");
        var response = await _httpClient!.PostAsync(endpoint, content, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ActionResult> PatchAsync(string endpoint, object payload, CancellationToken ct)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");
        var response = await _httpClient!.PatchAsync(endpoint, content, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private static async Task<ActionResult> ProcessResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                return ActionResult.Ok(data ?? new Dictionary<string, object>());
            }
            catch
            {
                return ActionResult.Ok(new Dictionary<string, object> { ["response"] = content });
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            var message = doc.RootElement.TryGetProperty("message", out var msgProp)
                ? msgProp.GetString()
                : content;
            return ActionResult.Fail($"Notion API error ({response.StatusCode}): {message}");
        }
        catch
        {
            return ActionResult.Fail($"Notion API error ({response.StatusCode}): {content}");
        }
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
