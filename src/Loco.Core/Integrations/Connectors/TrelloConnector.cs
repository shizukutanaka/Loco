using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Trello connector for visual project management and task tracking.
/// Uses Trello REST API v1.
/// </summary>
public sealed class TrelloConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private string? _apiKey;
    private string? _token;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public override string Id => "trello";
    public override string Name => "Trello";
    public override string Description => "Visual project management with boards, lists, and cards";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Productivity;
    public override string IconUrl => "https://trello.com/favicon.ico";

    public override ConnectorCapabilities Capabilities => new()
    {
        SupportsActions = true,
        SupportsTriggers = true,
        SupportsWebhooks = true
    };

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.OAuth2,
        RequiredCredentials = new CredentialField[]
        {
            new() { Name = "apiKey", Label = "API Key", Type = ParameterType.String, Description = "Trello API key" },
            new() { Name = "token", Label = "Token", Type = ParameterType.Password, Description = "OAuth token" }
        }
    };

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // Boards
        new()
        {
            Id = "getBoards",
            Name = "Get Boards",
            Description = "Get all boards for the authenticated user",
            Parameters = new ActionParameter[]
            {
                new() { Name = "filter", Type = ParameterType.String, DefaultValue = "all", Description = "all, open, closed, starred" }
            }
        },
        new()
        {
            Id = "getBoard",
            Name = "Get Board",
            Description = "Get details of a specific board",
            Parameters = new ActionParameter[]
            {
                new() { Name = "boardId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "createBoard",
            Name = "Create Board",
            Description = "Create a new board",
            Parameters = new ActionParameter[]
            {
                new() { Name = "name", Type = ParameterType.String, Required = true },
                new() { Name = "desc", Type = ParameterType.String },
                new() { Name = "defaultLists", Type = ParameterType.Boolean, DefaultValue = true },
                new() { Name = "prefs_background", Type = ParameterType.String, DefaultValue = "blue" }
            }
        },
        new()
        {
            Id = "updateBoard",
            Name = "Update Board",
            Description = "Update board properties",
            Parameters = new ActionParameter[]
            {
                new() { Name = "boardId", Type = ParameterType.String, Required = true },
                new() { Name = "name", Type = ParameterType.String },
                new() { Name = "desc", Type = ParameterType.String },
                new() { Name = "closed", Type = ParameterType.Boolean }
            }
        },

        // Lists
        new()
        {
            Id = "getLists",
            Name = "Get Lists",
            Description = "Get all lists on a board",
            Parameters = new ActionParameter[]
            {
                new() { Name = "boardId", Type = ParameterType.String, Required = true },
                new() { Name = "filter", Type = ParameterType.String, DefaultValue = "open", Description = "all, open, closed" }
            }
        },
        new()
        {
            Id = "createList",
            Name = "Create List",
            Description = "Create a new list on a board",
            Parameters = new ActionParameter[]
            {
                new() { Name = "boardId", Type = ParameterType.String, Required = true },
                new() { Name = "name", Type = ParameterType.String, Required = true },
                new() { Name = "pos", Type = ParameterType.String, DefaultValue = "bottom", Description = "top, bottom, or positive number" }
            }
        },
        new()
        {
            Id = "updateList",
            Name = "Update List",
            Description = "Update a list",
            Parameters = new ActionParameter[]
            {
                new() { Name = "listId", Type = ParameterType.String, Required = true },
                new() { Name = "name", Type = ParameterType.String },
                new() { Name = "closed", Type = ParameterType.Boolean },
                new() { Name = "pos", Type = ParameterType.String }
            }
        },

        // Cards
        new()
        {
            Id = "getCards",
            Name = "Get Cards",
            Description = "Get all cards in a list",
            Parameters = new ActionParameter[]
            {
                new() { Name = "listId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "getCard",
            Name = "Get Card",
            Description = "Get details of a specific card",
            Parameters = new ActionParameter[]
            {
                new() { Name = "cardId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "createCard",
            Name = "Create Card",
            Description = "Create a new card",
            Parameters = new ActionParameter[]
            {
                new() { Name = "listId", Type = ParameterType.String, Required = true },
                new() { Name = "name", Type = ParameterType.String, Required = true },
                new() { Name = "desc", Type = ParameterType.String },
                new() { Name = "pos", Type = ParameterType.String, DefaultValue = "bottom" },
                new() { Name = "due", Type = ParameterType.DateTime },
                new() { Name = "labels", Type = ParameterType.String, Description = "Comma-separated label IDs" },
                new() { Name = "members", Type = ParameterType.String, Description = "Comma-separated member IDs" }
            }
        },
        new()
        {
            Id = "updateCard",
            Name = "Update Card",
            Description = "Update card properties",
            Parameters = new ActionParameter[]
            {
                new() { Name = "cardId", Type = ParameterType.String, Required = true },
                new() { Name = "name", Type = ParameterType.String },
                new() { Name = "desc", Type = ParameterType.String },
                new() { Name = "closed", Type = ParameterType.Boolean },
                new() { Name = "idList", Type = ParameterType.String, Description = "Move card to a different list" },
                new() { Name = "pos", Type = ParameterType.String },
                new() { Name = "due", Type = ParameterType.DateTime }
            }
        },
        new()
        {
            Id = "deleteCard",
            Name = "Delete Card",
            Description = "Delete a card permanently",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "cardId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "moveCard",
            Name = "Move Card",
            Description = "Move a card to a different list",
            Parameters = new ActionParameter[]
            {
                new() { Name = "cardId", Type = ParameterType.String, Required = true },
                new() { Name = "listId", Type = ParameterType.String, Required = true },
                new() { Name = "pos", Type = ParameterType.String, DefaultValue = "bottom" }
            }
        },

        // Checklists
        new()
        {
            Id = "createChecklist",
            Name = "Create Checklist",
            Description = "Add a checklist to a card",
            Parameters = new ActionParameter[]
            {
                new() { Name = "cardId", Type = ParameterType.String, Required = true },
                new() { Name = "name", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "addCheckItem",
            Name = "Add Check Item",
            Description = "Add an item to a checklist",
            Parameters = new ActionParameter[]
            {
                new() { Name = "checklistId", Type = ParameterType.String, Required = true },
                new() { Name = "name", Type = ParameterType.String, Required = true },
                new() { Name = "checked", Type = ParameterType.Boolean, DefaultValue = false }
            }
        },
        new()
        {
            Id = "updateCheckItem",
            Name = "Update Check Item",
            Description = "Update a checklist item",
            Parameters = new ActionParameter[]
            {
                new() { Name = "cardId", Type = ParameterType.String, Required = true },
                new() { Name = "checkItemId", Type = ParameterType.String, Required = true },
                new() { Name = "state", Type = ParameterType.String, Required = true, Description = "complete, incomplete" }
            }
        },

        // Comments
        new()
        {
            Id = "addComment",
            Name = "Add Comment",
            Description = "Add a comment to a card",
            Parameters = new ActionParameter[]
            {
                new() { Name = "cardId", Type = ParameterType.String, Required = true },
                new() { Name = "text", Type = ParameterType.String, Required = true }
            }
        },

        // Labels
        new()
        {
            Id = "addLabelToCard",
            Name = "Add Label to Card",
            Description = "Add a label to a card",
            Parameters = new ActionParameter[]
            {
                new() { Name = "cardId", Type = ParameterType.String, Required = true },
                new() { Name = "labelId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "createLabel",
            Name = "Create Label",
            Description = "Create a new label on a board",
            Parameters = new ActionParameter[]
            {
                new() { Name = "boardId", Type = ParameterType.String, Required = true },
                new() { Name = "name", Type = ParameterType.String, Required = true },
                new() { Name = "color", Type = ParameterType.String, Required = true, Description = "yellow, purple, blue, red, green, orange, black, sky, pink, lime" }
            }
        },

        // Members
        new()
        {
            Id = "addMemberToCard",
            Name = "Add Member to Card",
            Description = "Assign a member to a card",
            Parameters = new ActionParameter[]
            {
                new() { Name = "cardId", Type = ParameterType.String, Required = true },
                new() { Name = "memberId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "removeMemberFromCard",
            Name = "Remove Member from Card",
            Description = "Unassign a member from a card",
            Parameters = new ActionParameter[]
            {
                new() { Name = "cardId", Type = ParameterType.String, Required = true },
                new() { Name = "memberId", Type = ParameterType.String, Required = true }
            }
        },

        // Search
        new()
        {
            Id = "searchCards",
            Name = "Search Cards",
            Description = "Search for cards",
            Parameters = new ActionParameter[]
            {
                new() { Name = "query", Type = ParameterType.String, Required = true },
                new() { Name = "boardIds", Type = ParameterType.String, Description = "Comma-separated board IDs to search in" }
            }
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "cardCreated",
            Name = "Card Created",
            Description = "Triggered when a new card is created",
            Type = TriggerType.Webhook,
            ConfigParameters = new ActionParameter[]
            {
                new() { Name = "boardId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "cardUpdated",
            Name = "Card Updated",
            Description = "Triggered when a card is updated",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "cardMoved",
            Name = "Card Moved",
            Description = "Triggered when a card is moved to a different list",
            Type = TriggerType.Webhook
        }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        await base.InitializeAsync(config, ct);

        _apiKey = config.GetCredentialString("apiKey");
        _token = config.GetCredentialString("token");

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.trello.com/1/")
        };
    }

    protected override async Task<ActionResult> ExecuteActionCoreAsync(
        ConnectorAction action,
        ActionParameters parameters,
        Core.ExecutionContext context,
        CancellationToken ct)
    {
        return action.Id switch
        {
            "getBoards" => await GetAsync($"members/me/boards?filter={parameters.GetString("filter") ?? "all"}", ct),
            "getBoard" => await GetAsync($"boards/{parameters.GetString("boardId")}", ct),
            "createBoard" => await CreateBoardAsync(parameters, ct),
            "updateBoard" => await UpdateBoardAsync(parameters, ct),

            "getLists" => await GetAsync($"boards/{parameters.GetString("boardId")}/lists?filter={parameters.GetString("filter") ?? "open"}", ct),
            "createList" => await CreateListAsync(parameters, ct),
            "updateList" => await UpdateListAsync(parameters, ct),

            "getCards" => await GetAsync($"lists/{parameters.GetString("listId")}/cards", ct),
            "getCard" => await GetAsync($"cards/{parameters.GetString("cardId")}", ct),
            "createCard" => await CreateCardAsync(parameters, ct),
            "updateCard" => await UpdateCardAsync(parameters, ct),
            "deleteCard" => await DeleteAsync($"cards/{parameters.GetString("cardId")}", ct),
            "moveCard" => await MoveCardAsync(parameters, ct),

            "createChecklist" => await CreateChecklistAsync(parameters, ct),
            "addCheckItem" => await AddCheckItemAsync(parameters, ct),
            "updateCheckItem" => await UpdateCheckItemAsync(parameters, ct),

            "addComment" => await AddCommentAsync(parameters, ct),

            "addLabelToCard" => await PostAsync($"cards/{parameters.GetString("cardId")}/idLabels?value={parameters.GetString("labelId")}", null, ct),
            "createLabel" => await CreateLabelAsync(parameters, ct),

            "addMemberToCard" => await PostAsync($"cards/{parameters.GetString("cardId")}/idMembers?value={parameters.GetString("memberId")}", null, ct),
            "removeMemberFromCard" => await DeleteAsync($"cards/{parameters.GetString("cardId")}/idMembers/{parameters.GetString("memberId")}", ct),

            "searchCards" => await SearchCardsAsync(parameters, ct),

            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private string AddAuth(string url)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}key={_apiKey}&token={_token}";
    }

    private async Task<ActionResult> CreateBoardAsync(ActionParameters parameters, CancellationToken ct)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["name"] = parameters.GetString("name")!,
            ["defaultLists"] = parameters.GetBool("defaultLists", true).ToString().ToLower(),
            ["prefs_background"] = parameters.GetString("prefs_background") ?? "blue"
        };

        var desc = parameters.GetString("desc");
        if (!string.IsNullOrEmpty(desc))
            queryParams["desc"] = desc;

        var query = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        return await PostAsync($"boards?{query}", null, ct);
    }

    private async Task<ActionResult> UpdateBoardAsync(ActionParameters parameters, CancellationToken ct)
    {
        var boardId = parameters.GetString("boardId")!;
        var queryParams = new List<string>();

        var name = parameters.GetString("name");
        if (!string.IsNullOrEmpty(name))
            queryParams.Add($"name={Uri.EscapeDataString(name)}");

        var desc = parameters.GetString("desc");
        if (!string.IsNullOrEmpty(desc))
            queryParams.Add($"desc={Uri.EscapeDataString(desc)}");

        if (parameters.Has("closed"))
            queryParams.Add($"closed={parameters.GetBool("closed", false).ToString().ToLower()}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await PutAsync($"boards/{boardId}{query}", null, ct);
    }

    private async Task<ActionResult> CreateListAsync(ActionParameters parameters, CancellationToken ct)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["idBoard"] = parameters.GetString("boardId")!,
            ["name"] = parameters.GetString("name")!,
            ["pos"] = parameters.GetString("pos") ?? "bottom"
        };

        var query = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        return await PostAsync($"lists?{query}", null, ct);
    }

    private async Task<ActionResult> UpdateListAsync(ActionParameters parameters, CancellationToken ct)
    {
        var listId = parameters.GetString("listId")!;
        var queryParams = new List<string>();

        var name = parameters.GetString("name");
        if (!string.IsNullOrEmpty(name))
            queryParams.Add($"name={Uri.EscapeDataString(name)}");

        if (parameters.Has("closed"))
            queryParams.Add($"closed={parameters.GetBool("closed", false).ToString().ToLower()}");

        var pos = parameters.GetString("pos");
        if (!string.IsNullOrEmpty(pos))
            queryParams.Add($"pos={Uri.EscapeDataString(pos)}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await PutAsync($"lists/{listId}{query}", null, ct);
    }

    private async Task<ActionResult> CreateCardAsync(ActionParameters parameters, CancellationToken ct)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["idList"] = parameters.GetString("listId")!,
            ["name"] = parameters.GetString("name")!,
            ["pos"] = parameters.GetString("pos") ?? "bottom"
        };

        var desc = parameters.GetString("desc");
        if (!string.IsNullOrEmpty(desc))
            queryParams["desc"] = desc;

        var due = parameters.GetString("due");
        if (!string.IsNullOrEmpty(due))
            queryParams["due"] = due;

        var labels = parameters.GetString("labels");
        if (!string.IsNullOrEmpty(labels))
            queryParams["idLabels"] = labels;

        var members = parameters.GetString("members");
        if (!string.IsNullOrEmpty(members))
            queryParams["idMembers"] = members;

        var query = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        return await PostAsync($"cards?{query}", null, ct);
    }

    private async Task<ActionResult> UpdateCardAsync(ActionParameters parameters, CancellationToken ct)
    {
        var cardId = parameters.GetString("cardId")!;
        var queryParams = new List<string>();

        var name = parameters.GetString("name");
        if (!string.IsNullOrEmpty(name))
            queryParams.Add($"name={Uri.EscapeDataString(name)}");

        var desc = parameters.GetString("desc");
        if (!string.IsNullOrEmpty(desc))
            queryParams.Add($"desc={Uri.EscapeDataString(desc)}");

        if (parameters.Has("closed"))
            queryParams.Add($"closed={parameters.GetBool("closed", false).ToString().ToLower()}");

        var idList = parameters.GetString("idList");
        if (!string.IsNullOrEmpty(idList))
            queryParams.Add($"idList={idList}");

        var pos = parameters.GetString("pos");
        if (!string.IsNullOrEmpty(pos))
            queryParams.Add($"pos={Uri.EscapeDataString(pos)}");

        var due = parameters.GetString("due");
        if (!string.IsNullOrEmpty(due))
            queryParams.Add($"due={Uri.EscapeDataString(due)}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await PutAsync($"cards/{cardId}{query}", null, ct);
    }

    private async Task<ActionResult> MoveCardAsync(ActionParameters parameters, CancellationToken ct)
    {
        var cardId = parameters.GetString("cardId")!;
        var listId = parameters.GetString("listId")!;
        var pos = parameters.GetString("pos") ?? "bottom";

        return await PutAsync($"cards/{cardId}?idList={listId}&pos={Uri.EscapeDataString(pos)}", null, ct);
    }

    private async Task<ActionResult> CreateChecklistAsync(ActionParameters parameters, CancellationToken ct)
    {
        var cardId = parameters.GetString("cardId")!;
        var name = parameters.GetString("name")!;
        return await PostAsync($"checklists?idCard={cardId}&name={Uri.EscapeDataString(name)}", null, ct);
    }

    private async Task<ActionResult> AddCheckItemAsync(ActionParameters parameters, CancellationToken ct)
    {
        var checklistId = parameters.GetString("checklistId")!;
        var name = parameters.GetString("name")!;
        var checked_ = parameters.GetBool("checked", false);

        return await PostAsync($"checklists/{checklistId}/checkItems?name={Uri.EscapeDataString(name)}&checked={checked_.ToString().ToLower()}", null, ct);
    }

    private async Task<ActionResult> UpdateCheckItemAsync(ActionParameters parameters, CancellationToken ct)
    {
        var cardId = parameters.GetString("cardId")!;
        var checkItemId = parameters.GetString("checkItemId")!;
        var state = parameters.GetString("state")!;

        return await PutAsync($"cards/{cardId}/checkItem/{checkItemId}?state={state}", null, ct);
    }

    private async Task<ActionResult> AddCommentAsync(ActionParameters parameters, CancellationToken ct)
    {
        var cardId = parameters.GetString("cardId")!;
        var text = parameters.GetString("text")!;
        return await PostAsync($"cards/{cardId}/actions/comments?text={Uri.EscapeDataString(text)}", null, ct);
    }

    private async Task<ActionResult> CreateLabelAsync(ActionParameters parameters, CancellationToken ct)
    {
        var boardId = parameters.GetString("boardId")!;
        var name = parameters.GetString("name")!;
        var color = parameters.GetString("color")!;

        return await PostAsync($"labels?idBoard={boardId}&name={Uri.EscapeDataString(name)}&color={color}", null, ct);
    }

    private async Task<ActionResult> SearchCardsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var query = parameters.GetString("query")!;
        var endpoint = $"search?query={Uri.EscapeDataString(query)}&modelTypes=cards";

        var boardIds = parameters.GetString("boardIds");
        if (!string.IsNullOrEmpty(boardIds))
            endpoint += $"&idBoards={boardIds}";

        return await GetAsync(endpoint, ct);
    }

    private async Task<ActionResult> GetAsync(string endpoint, CancellationToken ct)
    {
        var url = AddAuth(endpoint);
        var response = await _httpClient!.GetAsync(url, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ActionResult> PostAsync(string endpoint, object? payload, CancellationToken ct)
    {
        var url = AddAuth(endpoint);
        HttpContent? content = null;

        if (payload != null)
            content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        var response = await _httpClient!.PostAsync(url, content, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ActionResult> PutAsync(string endpoint, object? payload, CancellationToken ct)
    {
        var url = AddAuth(endpoint);
        HttpContent? content = null;

        if (payload != null)
            content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        var response = await _httpClient!.PutAsync(url, content, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ActionResult> DeleteAsync(string endpoint, CancellationToken ct)
    {
        var url = AddAuth(endpoint);
        var response = await _httpClient!.DeleteAsync(url, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private static async Task<ActionResult> ProcessResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            if (string.IsNullOrEmpty(content))
                return ActionResult.Ok(new Dictionary<string, object> { ["success"] = true });

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

        return ActionResult.Fail($"Trello error ({response.StatusCode}): {content}");
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
