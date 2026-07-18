using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Zendesk connector for customer support ticket management.
/// Uses Zendesk Support API v2.
/// </summary>
public sealed class ZendeskConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public override string Id => "zendesk";
    public override string Name => "Zendesk";
    public override string Description => "Customer service platform for support tickets and help desk";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Api;
    public override string IconUrl => "https://www.zendesk.com/favicon.ico";

    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.ForApi();

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.ApiKey,
        RequiredCredentials = new CredentialField[]
        {
            new() { Name = "subdomain", Label = "Subdomain", Type = ParameterType.String, Description = "Your Zendesk subdomain (e.g., yourcompany)" },
            new() { Name = "email", Label = "Email", Type = ParameterType.String, Description = "Admin email address" },
            new() { Name = "apiToken", Label = "API Token", Type = ParameterType.Password }
        }
    };

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // Tickets
        new()
        {
            Id = "createTicket",
            Name = "Create Ticket",
            Description = "Create a new support ticket",
            Parameters = new ActionParameter[]
            {
                new() { Name = "subject", Type = ParameterType.String, Required = true },
                new() { Name = "description", Type = ParameterType.String, Required = true },
                new() { Name = "requesterId", Type = ParameterType.String },
                new() { Name = "requesterEmail", Type = ParameterType.String, Description = "Required if requesterId not provided" },
                new() { Name = "requesterName", Type = ParameterType.String },
                new() { Name = "assigneeId", Type = ParameterType.String },
                new() { Name = "groupId", Type = ParameterType.String },
                new() { Name = "priority", Type = ParameterType.String, Description = "low, normal, high, urgent" },
                new() { Name = "type", Type = ParameterType.String, Description = "problem, incident, question, task" },
                new() { Name = "status", Type = ParameterType.String, DefaultValue = "new" },
                new() { Name = "tags", Type = ParameterType.String, Description = "Comma-separated tags" },
                new() { Name = "customFields", Type = ParameterType.Json }
            }
        },
        new()
        {
            Id = "getTicket",
            Name = "Get Ticket",
            Description = "Get a ticket by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "ticketId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "updateTicket",
            Name = "Update Ticket",
            Description = "Update an existing ticket",
            Parameters = new ActionParameter[]
            {
                new() { Name = "ticketId", Type = ParameterType.String, Required = true },
                new() { Name = "subject", Type = ParameterType.String },
                new() { Name = "status", Type = ParameterType.String },
                new() { Name = "priority", Type = ParameterType.String },
                new() { Name = "assigneeId", Type = ParameterType.String },
                new() { Name = "groupId", Type = ParameterType.String },
                new() { Name = "tags", Type = ParameterType.String },
                new() { Name = "customFields", Type = ParameterType.Json }
            }
        },
        new()
        {
            Id = "deleteTicket",
            Name = "Delete Ticket",
            Description = "Delete a ticket",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "ticketId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "listTickets",
            Name = "List Tickets",
            Description = "List tickets with optional filtering",
            Parameters = new ActionParameter[]
            {
                new() { Name = "status", Type = ParameterType.String },
                new() { Name = "assigneeId", Type = ParameterType.String },
                new() { Name = "groupId", Type = ParameterType.String },
                new() { Name = "sortBy", Type = ParameterType.String, DefaultValue = "created_at" },
                new() { Name = "sortOrder", Type = ParameterType.String, DefaultValue = "desc" },
                new() { Name = "perPage", Type = ParameterType.Number, DefaultValue = 25 }
            }
        },
        new()
        {
            Id = "searchTickets",
            Name = "Search Tickets",
            Description = "Search tickets using Zendesk query syntax",
            Parameters = new ActionParameter[]
            {
                new() { Name = "query", Type = ParameterType.String, Required = true, Description = "Zendesk search query" },
                new() { Name = "sortBy", Type = ParameterType.String },
                new() { Name = "sortOrder", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "addComment",
            Name = "Add Comment",
            Description = "Add a comment to a ticket",
            Parameters = new ActionParameter[]
            {
                new() { Name = "ticketId", Type = ParameterType.String, Required = true },
                new() { Name = "body", Type = ParameterType.String, Required = true },
                new() { Name = "public", Type = ParameterType.Boolean, DefaultValue = true },
                new() { Name = "authorId", Type = ParameterType.String }
            }
        },

        // Users
        new()
        {
            Id = "createUser",
            Name = "Create User",
            Description = "Create a new user",
            Parameters = new ActionParameter[]
            {
                new() { Name = "name", Type = ParameterType.String, Required = true },
                new() { Name = "email", Type = ParameterType.String, Required = true },
                new() { Name = "phone", Type = ParameterType.String },
                new() { Name = "role", Type = ParameterType.String, DefaultValue = "end-user", Description = "end-user, agent, admin" },
                new() { Name = "organizationId", Type = ParameterType.String },
                new() { Name = "tags", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "getUser",
            Name = "Get User",
            Description = "Get a user by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "userId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "updateUser",
            Name = "Update User",
            Description = "Update an existing user",
            Parameters = new ActionParameter[]
            {
                new() { Name = "userId", Type = ParameterType.String, Required = true },
                new() { Name = "name", Type = ParameterType.String },
                new() { Name = "email", Type = ParameterType.String },
                new() { Name = "phone", Type = ParameterType.String },
                new() { Name = "organizationId", Type = ParameterType.String },
                new() { Name = "tags", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "searchUsers",
            Name = "Search Users",
            Description = "Search users",
            Parameters = new ActionParameter[]
            {
                new() { Name = "query", Type = ParameterType.String, Required = true }
            }
        },

        // Organizations
        new()
        {
            Id = "createOrganization",
            Name = "Create Organization",
            Description = "Create a new organization",
            Parameters = new ActionParameter[]
            {
                new() { Name = "name", Type = ParameterType.String, Required = true },
                new() { Name = "domains", Type = ParameterType.String, Description = "Comma-separated domain names" },
                new() { Name = "details", Type = ParameterType.String },
                new() { Name = "notes", Type = ParameterType.String },
                new() { Name = "tags", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "getOrganization",
            Name = "Get Organization",
            Description = "Get an organization by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "organizationId", Type = ParameterType.String, Required = true }
            }
        },

        // Groups
        new()
        {
            Id = "listGroups",
            Name = "List Groups",
            Description = "List all agent groups",
            Parameters = Array.Empty<ActionParameter>()
        },

        // Views
        new()
        {
            Id = "listViews",
            Name = "List Views",
            Description = "List all ticket views",
            Parameters = Array.Empty<ActionParameter>()
        },
        new()
        {
            Id = "getViewTickets",
            Name = "Get View Tickets",
            Description = "Get tickets in a specific view",
            Parameters = new ActionParameter[]
            {
                new() { Name = "viewId", Type = ParameterType.String, Required = true },
                new() { Name = "perPage", Type = ParameterType.Number, DefaultValue = 25 }
            }
        },

        // Macros
        new()
        {
            Id = "applyMacro",
            Name = "Apply Macro",
            Description = "Apply a macro to a ticket",
            Parameters = new ActionParameter[]
            {
                new() { Name = "ticketId", Type = ParameterType.String, Required = true },
                new() { Name = "macroId", Type = ParameterType.String, Required = true }
            }
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "ticketCreated",
            Name = "Ticket Created",
            Description = "Triggered when a new ticket is created",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "ticketUpdated",
            Name = "Ticket Updated",
            Description = "Triggered when a ticket is updated",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "ticketSolved",
            Name = "Ticket Solved",
            Description = "Triggered when a ticket is solved",
            Type = TriggerType.Webhook
        }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        await base.InitializeAsync(config, ct);

        var subdomain = config.GetCredentialString("subdomain");
        var email = config.GetCredentialString("email");
        var apiToken = config.GetCredentialString("apiToken");

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}/token:{apiToken}"));

        // Dispose any previous client before replacing it. InitializeAsync can run more
        // than once for the same cached connector instance (e.g. ConnectorRegistry.
        // GetInitializedConnectorAsync on credential rotation); overwriting _httpClient
        // unconditionally previously leaked the old HttpClient and its socket handler.
        _httpClient?.Dispose();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"https://{subdomain}.zendesk.com/api/v2/")
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
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
            "createTicket" => await CreateTicketAsync(parameters, ct),
            "getTicket" => await GetAsync($"tickets/{parameters.GetString("ticketId")}.json", ct),
            "updateTicket" => await UpdateTicketAsync(parameters, ct),
            "deleteTicket" => await DeleteAsync($"tickets/{parameters.GetString("ticketId")}.json", ct),
            "listTickets" => await ListTicketsAsync(parameters, ct),
            "searchTickets" => await SearchAsync("tickets", parameters, ct),
            "addComment" => await AddCommentAsync(parameters, ct),

            "createUser" => await CreateUserAsync(parameters, ct),
            "getUser" => await GetAsync($"users/{parameters.GetString("userId")}.json", ct),
            "updateUser" => await UpdateUserAsync(parameters, ct),
            "searchUsers" => await GetAsync($"users/search.json?query={Uri.EscapeDataString(parameters.GetString("query")!)}", ct),

            "createOrganization" => await CreateOrganizationAsync(parameters, ct),
            "getOrganization" => await GetAsync($"organizations/{parameters.GetString("organizationId")}.json", ct),

            "listGroups" => await GetAsync("groups.json", ct),
            "listViews" => await GetAsync("views.json", ct),
            "getViewTickets" => await GetViewTicketsAsync(parameters, ct),
            "applyMacro" => await ApplyMacroAsync(parameters, ct),

            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> CreateTicketAsync(ActionParameters parameters, CancellationToken ct)
    {
        var ticket = new Dictionary<string, object>
        {
            ["subject"] = parameters.GetString("subject")!,
            ["comment"] = new Dictionary<string, object> { ["body"] = parameters.GetString("description")! }
        };

        var requesterId = parameters.GetString("requesterId");
        if (!string.IsNullOrEmpty(requesterId))
        {
            ticket["requester_id"] = long.Parse(requesterId);
        }
        else
        {
            var requesterEmail = parameters.GetString("requesterEmail");
            if (!string.IsNullOrEmpty(requesterEmail))
            {
                var requester = new Dictionary<string, object> { ["email"] = requesterEmail };
                var requesterName = parameters.GetString("requesterName");
                if (!string.IsNullOrEmpty(requesterName)) requester["name"] = requesterName;
                ticket["requester"] = requester;
            }
        }

        var assigneeId = parameters.GetString("assigneeId");
        if (!string.IsNullOrEmpty(assigneeId)) ticket["assignee_id"] = long.Parse(assigneeId);

        var groupId = parameters.GetString("groupId");
        if (!string.IsNullOrEmpty(groupId)) ticket["group_id"] = long.Parse(groupId);

        var priority = parameters.GetString("priority");
        if (!string.IsNullOrEmpty(priority)) ticket["priority"] = priority;

        var type = parameters.GetString("type");
        if (!string.IsNullOrEmpty(type)) ticket["type"] = type;

        var status = parameters.GetString("status");
        if (!string.IsNullOrEmpty(status)) ticket["status"] = status;

        var tags = parameters.GetString("tags");
        if (!string.IsNullOrEmpty(tags))
            ticket["tags"] = tags.Split(',').Select(t => t.Trim()).ToArray();

        var customFields = parameters.Get<JsonElement?>("customFields");
        if (customFields.HasValue && customFields.Value.ValueKind != JsonValueKind.Undefined)
            ticket["custom_fields"] = customFields.Value;

        return await PostAsync("tickets.json", new { ticket }, ct);
    }

    private async Task<ActionResult> UpdateTicketAsync(ActionParameters parameters, CancellationToken ct)
    {
        var ticketId = parameters.GetString("ticketId")!;
        var ticket = new Dictionary<string, object>();

        var subject = parameters.GetString("subject");
        if (!string.IsNullOrEmpty(subject)) ticket["subject"] = subject;

        var status = parameters.GetString("status");
        if (!string.IsNullOrEmpty(status)) ticket["status"] = status;

        var priority = parameters.GetString("priority");
        if (!string.IsNullOrEmpty(priority)) ticket["priority"] = priority;

        var assigneeId = parameters.GetString("assigneeId");
        if (!string.IsNullOrEmpty(assigneeId)) ticket["assignee_id"] = long.Parse(assigneeId);

        var groupId = parameters.GetString("groupId");
        if (!string.IsNullOrEmpty(groupId)) ticket["group_id"] = long.Parse(groupId);

        var tags = parameters.GetString("tags");
        if (!string.IsNullOrEmpty(tags))
            ticket["tags"] = tags.Split(',').Select(t => t.Trim()).ToArray();

        var customFields = parameters.Get<JsonElement?>("customFields");
        if (customFields.HasValue && customFields.Value.ValueKind != JsonValueKind.Undefined)
            ticket["custom_fields"] = customFields.Value;

        return await PutAsync($"tickets/{ticketId}.json", new { ticket }, ct);
    }

    private async Task<ActionResult> ListTicketsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var queryParams = new List<string>();

        var sortBy = parameters.GetString("sortBy") ?? "created_at";
        queryParams.Add($"sort_by={sortBy}");

        var sortOrder = parameters.GetString("sortOrder") ?? "desc";
        queryParams.Add($"sort_order={sortOrder}");

        var perPage = parameters.GetInt("perPage", 25);
        queryParams.Add($"per_page={perPage}");

        return await GetAsync($"tickets.json?{string.Join("&", queryParams)}", ct);
    }

    private async Task<ActionResult> SearchAsync(string type, ActionParameters parameters, CancellationToken ct)
    {
        var query = parameters.GetString("query")!;
        var endpoint = $"search.json?query=type:{type} {Uri.EscapeDataString(query)}";

        var sortBy = parameters.GetString("sortBy");
        if (!string.IsNullOrEmpty(sortBy)) endpoint += $"&sort_by={sortBy}";

        var sortOrder = parameters.GetString("sortOrder");
        if (!string.IsNullOrEmpty(sortOrder)) endpoint += $"&sort_order={sortOrder}";

        return await GetAsync(endpoint, ct);
    }

    private async Task<ActionResult> AddCommentAsync(ActionParameters parameters, CancellationToken ct)
    {
        var ticketId = parameters.GetString("ticketId")!;
        var comment = new Dictionary<string, object>
        {
            ["body"] = parameters.GetString("body")!,
            ["public"] = parameters.GetBool("public", true)
        };

        var authorId = parameters.GetString("authorId");
        if (!string.IsNullOrEmpty(authorId)) comment["author_id"] = long.Parse(authorId);

        return await PutAsync($"tickets/{ticketId}.json", new { ticket = new { comment } }, ct);
    }

    private async Task<ActionResult> CreateUserAsync(ActionParameters parameters, CancellationToken ct)
    {
        var user = new Dictionary<string, object>
        {
            ["name"] = parameters.GetString("name")!,
            ["email"] = parameters.GetString("email")!,
            ["role"] = parameters.GetString("role") ?? "end-user"
        };

        var phone = parameters.GetString("phone");
        if (!string.IsNullOrEmpty(phone)) user["phone"] = phone;

        var orgId = parameters.GetString("organizationId");
        if (!string.IsNullOrEmpty(orgId)) user["organization_id"] = long.Parse(orgId);

        var tags = parameters.GetString("tags");
        if (!string.IsNullOrEmpty(tags))
            user["tags"] = tags.Split(',').Select(t => t.Trim()).ToArray();

        return await PostAsync("users.json", new { user }, ct);
    }

    private async Task<ActionResult> UpdateUserAsync(ActionParameters parameters, CancellationToken ct)
    {
        var userId = parameters.GetString("userId")!;
        var user = new Dictionary<string, object>();

        var name = parameters.GetString("name");
        if (!string.IsNullOrEmpty(name)) user["name"] = name;

        var email = parameters.GetString("email");
        if (!string.IsNullOrEmpty(email)) user["email"] = email;

        var phone = parameters.GetString("phone");
        if (!string.IsNullOrEmpty(phone)) user["phone"] = phone;

        var orgId = parameters.GetString("organizationId");
        if (!string.IsNullOrEmpty(orgId)) user["organization_id"] = long.Parse(orgId);

        var tags = parameters.GetString("tags");
        if (!string.IsNullOrEmpty(tags))
            user["tags"] = tags.Split(',').Select(t => t.Trim()).ToArray();

        return await PutAsync($"users/{userId}.json", new { user }, ct);
    }

    private async Task<ActionResult> CreateOrganizationAsync(ActionParameters parameters, CancellationToken ct)
    {
        var organization = new Dictionary<string, object>
        {
            ["name"] = parameters.GetString("name")!
        };

        var domains = parameters.GetString("domains");
        if (!string.IsNullOrEmpty(domains))
            organization["domain_names"] = domains.Split(',').Select(d => d.Trim()).ToArray();

        var details = parameters.GetString("details");
        if (!string.IsNullOrEmpty(details)) organization["details"] = details;

        var notes = parameters.GetString("notes");
        if (!string.IsNullOrEmpty(notes)) organization["notes"] = notes;

        var tags = parameters.GetString("tags");
        if (!string.IsNullOrEmpty(tags))
            organization["tags"] = tags.Split(',').Select(t => t.Trim()).ToArray();

        return await PostAsync("organizations.json", new { organization }, ct);
    }

    private async Task<ActionResult> GetViewTicketsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var viewId = parameters.GetString("viewId")!;
        var perPage = parameters.GetInt("perPage", 25);
        return await GetAsync($"views/{viewId}/tickets.json?per_page={perPage}", ct);
    }

    private async Task<ActionResult> ApplyMacroAsync(ActionParameters parameters, CancellationToken ct)
    {
        var ticketId = parameters.GetString("ticketId")!;
        var macroId = parameters.GetString("macroId")!;

        // Zendesk's macro "apply" endpoint only PREVIEWS the resulting changes
        // (it returns result.ticket) - it does NOT modify the ticket. The old
        // code returned that preview and reported success as if the macro had
        // been applied. Fetch the preview, then PUT the resulting ticket back to
        // actually apply it.
        var preview = await GetAsync($"tickets/{ticketId}/macros/{macroId}/apply.json", ct);
        if (!preview.Success)
            return preview;

        if (preview.Data is Dictionary<string, object> data
            && data.TryGetValue("result", out var resultObj)
            && resultObj is JsonElement { ValueKind: JsonValueKind.Object } result
            && result.TryGetProperty("ticket", out var ticket))
        {
            return await PutAsync($"tickets/{ticketId}.json", new { ticket }, ct);
        }

        return ActionResult.Fail("Macro preview did not contain the expected ticket changes", "API_ERROR");
    }

    private async Task<ActionResult> GetAsync(string endpoint, CancellationToken ct)
    {
        var response = await _httpClient!.GetAsync(endpoint, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ActionResult> PostAsync(string endpoint, object payload, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync(endpoint, content, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ActionResult> PutAsync(string endpoint, object payload, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient!.PutAsync(endpoint, content, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ActionResult> DeleteAsync(string endpoint, CancellationToken ct)
    {
        var response = await _httpClient!.DeleteAsync(endpoint, ct);
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

        return ActionResult.Fail($"Zendesk error ({response.StatusCode}): {content}");
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
