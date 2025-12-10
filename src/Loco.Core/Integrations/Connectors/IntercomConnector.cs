using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Intercom connector for customer messaging and support.
/// Uses Intercom REST API v2.
/// </summary>
public sealed class IntercomConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public override string Id => "intercom";
    public override string Name => "Intercom";
    public override string Description => "Customer messaging platform for support, marketing, and engagement";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Communication;
    public override string IconUrl => "https://www.intercom.com/favicon.ico";

    public override ConnectorCapabilities Capabilities => new()
    {
        SupportsActions = true,
        SupportsTriggers = true,
        SupportsWebhooks = true
    };

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.Bearer,
        RequiredCredentials = new CredentialField[]
        {
            new() { Name = "accessToken", Label = "Access Token", Type = ParameterType.Password, Description = "Intercom access token" }
        }
    };

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // Contacts/Users
        new()
        {
            Id = "getContacts",
            Name = "Get Contacts",
            Description = "List all contacts",
            Parameters = new ActionParameter[]
            {
                new() { Name = "perPage", Type = ParameterType.Number, DefaultValue = 50, Description = "Results per page (max 150)" }
            }
        },
        new()
        {
            Id = "getContact",
            Name = "Get Contact",
            Description = "Get a specific contact",
            Parameters = new ActionParameter[]
            {
                new() { Name = "contactId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "createContact",
            Name = "Create Contact",
            Description = "Create or update a contact",
            Parameters = new ActionParameter[]
            {
                new() { Name = "email", Type = ParameterType.String },
                new() { Name = "phone", Type = ParameterType.String },
                new() { Name = "name", Type = ParameterType.String },
                new() { Name = "userId", Type = ParameterType.String, Description = "External user ID" },
                new() { Name = "customAttributes", Type = ParameterType.Json, Description = "Custom attributes as JSON object" }
            }
        },
        new()
        {
            Id = "updateContact",
            Name = "Update Contact",
            Description = "Update an existing contact",
            Parameters = new ActionParameter[]
            {
                new() { Name = "contactId", Type = ParameterType.String, Required = true },
                new() { Name = "email", Type = ParameterType.String },
                new() { Name = "name", Type = ParameterType.String },
                new() { Name = "phone", Type = ParameterType.String },
                new() { Name = "customAttributes", Type = ParameterType.Json }
            }
        },
        new()
        {
            Id = "deleteContact",
            Name = "Delete Contact",
            Description = "Permanently delete a contact",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "contactId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "searchContacts",
            Name = "Search Contacts",
            Description = "Search for contacts",
            Parameters = new ActionParameter[]
            {
                new() { Name = "query", Type = ParameterType.String, Required = true, Description = "Email or name to search" }
            }
        },

        // Conversations
        new()
        {
            Id = "getConversations",
            Name = "Get Conversations",
            Description = "List all conversations",
            Parameters = new ActionParameter[]
            {
                new() { Name = "perPage", Type = ParameterType.Number, DefaultValue = 50 },
                new() { Name = "state", Type = ParameterType.String, Description = "open, closed, snoozed" }
            }
        },
        new()
        {
            Id = "getConversation",
            Name = "Get Conversation",
            Description = "Get a specific conversation",
            Parameters = new ActionParameter[]
            {
                new() { Name = "conversationId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "replyToConversation",
            Name = "Reply to Conversation",
            Description = "Add a reply to a conversation",
            Parameters = new ActionParameter[]
            {
                new() { Name = "conversationId", Type = ParameterType.String, Required = true },
                new() { Name = "body", Type = ParameterType.String, Required = true },
                new() { Name = "messageType", Type = ParameterType.String, DefaultValue = "comment", Description = "comment, note" },
                new() { Name = "adminId", Type = ParameterType.String, Description = "Admin who is replying" }
            }
        },
        new()
        {
            Id = "closeConversation",
            Name = "Close Conversation",
            Description = "Close a conversation",
            Parameters = new ActionParameter[]
            {
                new() { Name = "conversationId", Type = ParameterType.String, Required = true },
                new() { Name = "adminId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "assignConversation",
            Name = "Assign Conversation",
            Description = "Assign a conversation to an admin or team",
            Parameters = new ActionParameter[]
            {
                new() { Name = "conversationId", Type = ParameterType.String, Required = true },
                new() { Name = "assigneeId", Type = ParameterType.String, Required = true },
                new() { Name = "adminId", Type = ParameterType.String, Required = true, Description = "Admin making the assignment" }
            }
        },
        new()
        {
            Id = "snoozeConversation",
            Name = "Snooze Conversation",
            Description = "Snooze a conversation",
            Parameters = new ActionParameter[]
            {
                new() { Name = "conversationId", Type = ParameterType.String, Required = true },
                new() { Name = "adminId", Type = ParameterType.String, Required = true },
                new() { Name = "snoozedUntil", Type = ParameterType.Number, Required = true, Description = "Unix timestamp" }
            }
        },

        // Messages
        new()
        {
            Id = "sendMessage",
            Name = "Send Message",
            Description = "Send a message to a user",
            Parameters = new ActionParameter[]
            {
                new() { Name = "from", Type = ParameterType.String, Required = true, Description = "Admin ID" },
                new() { Name = "to", Type = ParameterType.String, Required = true, Description = "User ID or email" },
                new() { Name = "messageType", Type = ParameterType.String, Required = true, DefaultValue = "inapp", Description = "inapp, email" },
                new() { Name = "subject", Type = ParameterType.String, Description = "Email subject (for email type)" },
                new() { Name = "body", Type = ParameterType.String, Required = true },
                new() { Name = "template", Type = ParameterType.String, Description = "Email template (for email type)" }
            }
        },

        // Notes
        new()
        {
            Id = "createNote",
            Name = "Create Note",
            Description = "Create a note about a contact",
            Parameters = new ActionParameter[]
            {
                new() { Name = "contactId", Type = ParameterType.String, Required = true },
                new() { Name = "body", Type = ParameterType.String, Required = true },
                new() { Name = "adminId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "getNotes",
            Name = "Get Notes",
            Description = "Get all notes for a contact",
            Parameters = new ActionParameter[]
            {
                new() { Name = "contactId", Type = ParameterType.String, Required = true }
            }
        },

        // Tags
        new()
        {
            Id = "getTags",
            Name = "Get Tags",
            Description = "List all tags",
            Parameters = Array.Empty<ActionParameter>()
        },
        new()
        {
            Id = "createTag",
            Name = "Create Tag",
            Description = "Create a new tag",
            Parameters = new ActionParameter[]
            {
                new() { Name = "name", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "tagContact",
            Name = "Tag Contact",
            Description = "Apply a tag to a contact",
            Parameters = new ActionParameter[]
            {
                new() { Name = "contactId", Type = ParameterType.String, Required = true },
                new() { Name = "tagId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "untagContact",
            Name = "Untag Contact",
            Description = "Remove a tag from a contact",
            Parameters = new ActionParameter[]
            {
                new() { Name = "contactId", Type = ParameterType.String, Required = true },
                new() { Name = "tagId", Type = ParameterType.String, Required = true }
            }
        },

        // Teams
        new()
        {
            Id = "getTeams",
            Name = "Get Teams",
            Description = "List all teams",
            Parameters = Array.Empty<ActionParameter>()
        },

        // Admins
        new()
        {
            Id = "getAdmins",
            Name = "Get Admins",
            Description = "List all admins",
            Parameters = Array.Empty<ActionParameter>()
        },
        new()
        {
            Id = "getAdmin",
            Name = "Get Admin",
            Description = "Get a specific admin",
            Parameters = new ActionParameter[]
            {
                new() { Name = "adminId", Type = ParameterType.String, Required = true }
            }
        },

        // Articles (Help Center)
        new()
        {
            Id = "getArticles",
            Name = "Get Articles",
            Description = "List all help center articles",
            Parameters = new ActionParameter[]
            {
                new() { Name = "perPage", Type = ParameterType.Number, DefaultValue = 50 }
            }
        },
        new()
        {
            Id = "getArticle",
            Name = "Get Article",
            Description = "Get a specific article",
            Parameters = new ActionParameter[]
            {
                new() { Name = "articleId", Type = ParameterType.String, Required = true }
            }
        },

        // Events
        new()
        {
            Id = "trackEvent",
            Name = "Track Event",
            Description = "Track a custom event for a user",
            Parameters = new ActionParameter[]
            {
                new() { Name = "eventName", Type = ParameterType.String, Required = true },
                new() { Name = "userId", Type = ParameterType.String, Description = "User ID (userId or email required)" },
                new() { Name = "email", Type = ParameterType.String, Description = "User email" },
                new() { Name = "createdAt", Type = ParameterType.Number, Description = "Unix timestamp (defaults to now)" },
                new() { Name = "metadata", Type = ParameterType.Json, Description = "Event metadata as JSON" }
            }
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "conversationCreated",
            Name = "Conversation Created",
            Description = "Triggered when a new conversation is created",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "conversationUserReplied",
            Name = "User Replied",
            Description = "Triggered when a user replies to a conversation",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "conversationAdminReplied",
            Name = "Admin Replied",
            Description = "Triggered when an admin replies to a conversation",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "contactCreated",
            Name = "Contact Created",
            Description = "Triggered when a new contact is created",
            Type = TriggerType.Webhook
        }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        await base.InitializeAsync(config, ct);

        var accessToken = config.GetCredentialString("accessToken");

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.intercom.io/")
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("Intercom-Version", "2.10");
    }

    protected override async Task<ActionResult> ExecuteActionCoreAsync(
        ConnectorAction action,
        ActionParameters parameters,
        Core.ExecutionContext context,
        CancellationToken ct)
    {
        return action.Id switch
        {
            "getContacts" => await GetAsync($"contacts?per_page={parameters.GetInt("perPage", 50)}", ct),
            "getContact" => await GetAsync($"contacts/{parameters.GetString("contactId")}", ct),
            "createContact" => await CreateContactAsync(parameters, ct),
            "updateContact" => await UpdateContactAsync(parameters, ct),
            "deleteContact" => await DeleteAsync($"contacts/{parameters.GetString("contactId")}", ct),
            "searchContacts" => await SearchContactsAsync(parameters, ct),

            "getConversations" => await GetConversationsAsync(parameters, ct),
            "getConversation" => await GetAsync($"conversations/{parameters.GetString("conversationId")}", ct),
            "replyToConversation" => await ReplyToConversationAsync(parameters, ct),
            "closeConversation" => await CloseConversationAsync(parameters, ct),
            "assignConversation" => await AssignConversationAsync(parameters, ct),
            "snoozeConversation" => await SnoozeConversationAsync(parameters, ct),

            "sendMessage" => await SendMessageAsync(parameters, ct),

            "createNote" => await CreateNoteAsync(parameters, ct),
            "getNotes" => await GetAsync($"contacts/{parameters.GetString("contactId")}/notes", ct),

            "getTags" => await GetAsync("tags", ct),
            "createTag" => await PostAsync("tags", new { name = parameters.GetString("name") }, ct),
            "tagContact" => await TagContactAsync(parameters, true, ct),
            "untagContact" => await TagContactAsync(parameters, false, ct),

            "getTeams" => await GetAsync("teams", ct),

            "getAdmins" => await GetAsync("admins", ct),
            "getAdmin" => await GetAsync($"admins/{parameters.GetString("adminId")}", ct),

            "getArticles" => await GetAsync($"articles?per_page={parameters.GetInt("perPage", 50)}", ct),
            "getArticle" => await GetAsync($"articles/{parameters.GetString("articleId")}", ct),

            "trackEvent" => await TrackEventAsync(parameters, ct),

            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> CreateContactAsync(ActionParameters parameters, CancellationToken ct)
    {
        var contact = new Dictionary<string, object>
        {
            ["role"] = "user"
        };

        var email = parameters.GetString("email");
        if (!string.IsNullOrEmpty(email))
            contact["email"] = email;

        var phone = parameters.GetString("phone");
        if (!string.IsNullOrEmpty(phone))
            contact["phone"] = phone;

        var name = parameters.GetString("name");
        if (!string.IsNullOrEmpty(name))
            contact["name"] = name;

        var userId = parameters.GetString("userId");
        if (!string.IsNullOrEmpty(userId))
            contact["external_id"] = userId;

        var customAttributes = parameters.Get<JsonElement?>("customAttributes");
        if (customAttributes.HasValue && customAttributes.Value.ValueKind == JsonValueKind.Object)
        {
            var attrs = new Dictionary<string, object>();
            foreach (var prop in customAttributes.Value.EnumerateObject())
            {
                attrs[prop.Name] = prop.Value.ToString();
            }
            contact["custom_attributes"] = attrs;
        }

        return await PostAsync("contacts", contact, ct);
    }

    private async Task<ActionResult> UpdateContactAsync(ActionParameters parameters, CancellationToken ct)
    {
        var contactId = parameters.GetString("contactId")!;
        var updates = new Dictionary<string, object>();

        var email = parameters.GetString("email");
        if (!string.IsNullOrEmpty(email))
            updates["email"] = email;

        var name = parameters.GetString("name");
        if (!string.IsNullOrEmpty(name))
            updates["name"] = name;

        var phone = parameters.GetString("phone");
        if (!string.IsNullOrEmpty(phone))
            updates["phone"] = phone;

        var customAttributes = parameters.Get<JsonElement?>("customAttributes");
        if (customAttributes.HasValue && customAttributes.Value.ValueKind == JsonValueKind.Object)
        {
            var attrs = new Dictionary<string, object>();
            foreach (var prop in customAttributes.Value.EnumerateObject())
            {
                attrs[prop.Name] = prop.Value.ToString();
            }
            updates["custom_attributes"] = attrs;
        }

        return await PutAsync($"contacts/{contactId}", updates, ct);
    }

    private async Task<ActionResult> SearchContactsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var query = parameters.GetString("query")!;
        var searchPayload = new
        {
            query = new
            {
                @operator = "OR",
                value = new[]
                {
                    new { field = "email", @operator = "~", value = query },
                    new { field = "name", @operator = "~", value = query }
                }
            }
        };

        return await PostAsync("contacts/search", searchPayload, ct);
    }

    private async Task<ActionResult> GetConversationsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var queryParams = new List<string>
        {
            $"per_page={parameters.GetInt("perPage", 50)}"
        };

        var state = parameters.GetString("state");
        if (!string.IsNullOrEmpty(state))
            queryParams.Add($"state={state}");

        var query = string.Join("&", queryParams);
        return await GetAsync($"conversations?{query}", ct);
    }

    private async Task<ActionResult> ReplyToConversationAsync(ActionParameters parameters, CancellationToken ct)
    {
        var conversationId = parameters.GetString("conversationId")!;
        var body = parameters.GetString("body")!;
        var messageType = parameters.GetString("messageType") ?? "comment";

        var payload = new Dictionary<string, object>
        {
            ["message_type"] = messageType,
            ["type"] = "admin",
            ["body"] = body
        };

        var adminId = parameters.GetString("adminId");
        if (!string.IsNullOrEmpty(adminId))
            payload["admin_id"] = adminId;

        return await PostAsync($"conversations/{conversationId}/reply", payload, ct);
    }

    private async Task<ActionResult> CloseConversationAsync(ActionParameters parameters, CancellationToken ct)
    {
        var conversationId = parameters.GetString("conversationId")!;
        var adminId = parameters.GetString("adminId")!;

        var payload = new
        {
            message_type = "close",
            type = "admin",
            admin_id = adminId
        };

        return await PostAsync($"conversations/{conversationId}/reply", payload, ct);
    }

    private async Task<ActionResult> AssignConversationAsync(ActionParameters parameters, CancellationToken ct)
    {
        var conversationId = parameters.GetString("conversationId")!;
        var assigneeId = parameters.GetString("assigneeId")!;
        var adminId = parameters.GetString("adminId")!;

        var payload = new
        {
            message_type = "assignment",
            type = "admin",
            admin_id = adminId,
            assignee_id = assigneeId
        };

        return await PostAsync($"conversations/{conversationId}/reply", payload, ct);
    }

    private async Task<ActionResult> SnoozeConversationAsync(ActionParameters parameters, CancellationToken ct)
    {
        var conversationId = parameters.GetString("conversationId")!;
        var adminId = parameters.GetString("adminId")!;
        var snoozedUntil = parameters.GetInt("snoozedUntil", 0);

        var payload = new
        {
            message_type = "snoozed",
            type = "admin",
            admin_id = adminId,
            snoozed_until = snoozedUntil
        };

        return await PostAsync($"conversations/{conversationId}/reply", payload, ct);
    }

    private async Task<ActionResult> SendMessageAsync(ActionParameters parameters, CancellationToken ct)
    {
        var from = new { type = "admin", id = parameters.GetString("from")! };
        var to = new { type = "user", id = parameters.GetString("to")! };
        var messageType = parameters.GetString("messageType") ?? "inapp";
        var body = parameters.GetString("body")!;

        var message = new Dictionary<string, object>
        {
            ["message_type"] = messageType,
            ["from"] = from,
            ["to"] = to,
            ["body"] = body
        };

        var subject = parameters.GetString("subject");
        if (!string.IsNullOrEmpty(subject))
            message["subject"] = subject;

        var template = parameters.GetString("template");
        if (!string.IsNullOrEmpty(template))
            message["template"] = template;

        return await PostAsync("messages", message, ct);
    }

    private async Task<ActionResult> CreateNoteAsync(ActionParameters parameters, CancellationToken ct)
    {
        var contactId = parameters.GetString("contactId")!;
        var body = parameters.GetString("body")!;
        var adminId = parameters.GetString("adminId")!;

        var payload = new
        {
            contact = new { id = contactId },
            admin_id = adminId,
            body
        };

        return await PostAsync("notes", payload, ct);
    }

    private async Task<ActionResult> TagContactAsync(ActionParameters parameters, bool tag, CancellationToken ct)
    {
        var contactId = parameters.GetString("contactId")!;
        var tagId = parameters.GetString("tagId")!;

        var payload = new
        {
            id = tagId,
            contacts = new[] { new { id = contactId, untag = !tag } }
        };

        return await PostAsync("tags", payload, ct);
    }

    private async Task<ActionResult> TrackEventAsync(ActionParameters parameters, CancellationToken ct)
    {
        var eventName = parameters.GetString("eventName")!;

        var payload = new Dictionary<string, object>
        {
            ["event_name"] = eventName,
            ["created_at"] = parameters.GetInt("createdAt", (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        var userId = parameters.GetString("userId");
        if (!string.IsNullOrEmpty(userId))
            payload["user_id"] = userId;
        else
        {
            var email = parameters.GetString("email");
            if (!string.IsNullOrEmpty(email))
                payload["email"] = email;
            else
                return ActionResult.Fail("Either userId or email is required");
        }

        var metadata = parameters.Get<JsonElement?>("metadata");
        if (metadata.HasValue && metadata.Value.ValueKind == JsonValueKind.Object)
        {
            var meta = new Dictionary<string, object>();
            foreach (var prop in metadata.Value.EnumerateObject())
            {
                meta[prop.Name] = prop.Value.ToString();
            }
            payload["metadata"] = meta;
        }

        return await PostAsync("events", payload, ct);
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

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("errors", out var errors))
            {
                return ActionResult.Fail($"Intercom error ({response.StatusCode}): {errors}");
            }
        }
        catch { }

        return ActionResult.Fail($"Intercom error ({response.StatusCode}): {content}");
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
