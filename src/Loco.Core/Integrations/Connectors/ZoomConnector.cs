using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Zoom connector for video conferencing and webinar automation.
/// Uses Zoom API v2.
/// </summary>
public sealed class ZoomConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public override string Id => "zoom";
    public override string Name => "Zoom";
    public override string Description => "Video conferencing and webinar platform";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Communication;
    public override string IconUrl => "https://zoom.us/favicon.ico";

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
            new() { Name = "accessToken", Label = "Access Token", Type = ParameterType.Password, Description = "OAuth access token or Server-to-Server token" }
        }
    };

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // Users
        new()
        {
            Id = "getUsers",
            Name = "Get Users",
            Description = "List users in the account",
            Parameters = new ActionParameter[]
            {
                new() { Name = "status", Type = ParameterType.String, DefaultValue = "active", Description = "active, inactive, pending" },
                new() { Name = "pageSize", Type = ParameterType.Number, DefaultValue = 30, Description = "Results per page (max 300)" }
            }
        },
        new()
        {
            Id = "getUser",
            Name = "Get User",
            Description = "Get details of a specific user",
            Parameters = new ActionParameter[]
            {
                new() { Name = "userId", Type = ParameterType.String, Required = true, Description = "User ID or email" }
            }
        },
        new()
        {
            Id = "createUser",
            Name = "Create User",
            Description = "Create a new user",
            Parameters = new ActionParameter[]
            {
                new() { Name = "email", Type = ParameterType.String, Required = true },
                new() { Name = "type", Type = ParameterType.Number, Required = true, DefaultValue = 1, Description = "1=Basic, 2=Licensed, 3=On-prem" },
                new() { Name = "firstName", Type = ParameterType.String },
                new() { Name = "lastName", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "updateUser",
            Name = "Update User",
            Description = "Update user information",
            Parameters = new ActionParameter[]
            {
                new() { Name = "userId", Type = ParameterType.String, Required = true },
                new() { Name = "firstName", Type = ParameterType.String },
                new() { Name = "lastName", Type = ParameterType.String },
                new() { Name = "type", Type = ParameterType.Number },
                new() { Name = "pmi", Type = ParameterType.Number, Description = "Personal Meeting ID" }
            }
        },
        new()
        {
            Id = "deleteUser",
            Name = "Delete User",
            Description = "Delete a user",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "userId", Type = ParameterType.String, Required = true },
                new() { Name = "action", Type = ParameterType.String, DefaultValue = "delete", Description = "delete, disassociate" }
            }
        },

        // Meetings
        new()
        {
            Id = "getMeetings",
            Name = "Get Meetings",
            Description = "List meetings for a user",
            Parameters = new ActionParameter[]
            {
                new() { Name = "userId", Type = ParameterType.String, Required = true },
                new() { Name = "type", Type = ParameterType.String, DefaultValue = "scheduled", Description = "scheduled, live, upcoming, upcoming_meetings, previous_meetings" },
                new() { Name = "pageSize", Type = ParameterType.Number, DefaultValue = 30 }
            }
        },
        new()
        {
            Id = "getMeeting",
            Name = "Get Meeting",
            Description = "Get details of a specific meeting",
            Parameters = new ActionParameter[]
            {
                new() { Name = "meetingId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "createMeeting",
            Name = "Create Meeting",
            Description = "Create a new meeting",
            Parameters = new ActionParameter[]
            {
                new() { Name = "userId", Type = ParameterType.String, Required = true, Description = "User ID or email (host)" },
                new() { Name = "topic", Type = ParameterType.String, Required = true },
                new() { Name = "type", Type = ParameterType.Number, DefaultValue = 2, Description = "1=Instant, 2=Scheduled, 3=Recurring no fixed time, 8=Recurring fixed time" },
                new() { Name = "startTime", Type = ParameterType.DateTime, Description = "Start time (ISO 8601)" },
                new() { Name = "duration", Type = ParameterType.Number, Description = "Duration in minutes" },
                new() { Name = "timezone", Type = ParameterType.String, DefaultValue = "UTC" },
                new() { Name = "password", Type = ParameterType.Password, Description = "Meeting password" },
                new() { Name = "agenda", Type = ParameterType.String },
                new() { Name = "autoRecording", Type = ParameterType.String, Description = "local, cloud, none" }
            }
        },
        new()
        {
            Id = "updateMeeting",
            Name = "Update Meeting",
            Description = "Update meeting details",
            Parameters = new ActionParameter[]
            {
                new() { Name = "meetingId", Type = ParameterType.String, Required = true },
                new() { Name = "topic", Type = ParameterType.String },
                new() { Name = "startTime", Type = ParameterType.DateTime },
                new() { Name = "duration", Type = ParameterType.Number },
                new() { Name = "agenda", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "deleteMeeting",
            Name = "Delete Meeting",
            Description = "Delete a meeting",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "meetingId", Type = ParameterType.String, Required = true }
            }
        },

        // Webinars
        new()
        {
            Id = "getWebinars",
            Name = "Get Webinars",
            Description = "List webinars for a user",
            Parameters = new ActionParameter[]
            {
                new() { Name = "userId", Type = ParameterType.String, Required = true },
                new() { Name = "pageSize", Type = ParameterType.Number, DefaultValue = 30 }
            }
        },
        new()
        {
            Id = "getWebinar",
            Name = "Get Webinar",
            Description = "Get details of a specific webinar",
            Parameters = new ActionParameter[]
            {
                new() { Name = "webinarId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "createWebinar",
            Name = "Create Webinar",
            Description = "Create a new webinar",
            Parameters = new ActionParameter[]
            {
                new() { Name = "userId", Type = ParameterType.String, Required = true },
                new() { Name = "topic", Type = ParameterType.String, Required = true },
                new() { Name = "type", Type = ParameterType.Number, DefaultValue = 5, Description = "5=Webinar, 6=Recurring no fixed time, 9=Recurring fixed time" },
                new() { Name = "startTime", Type = ParameterType.DateTime },
                new() { Name = "duration", Type = ParameterType.Number },
                new() { Name = "timezone", Type = ParameterType.String, DefaultValue = "UTC" },
                new() { Name = "agenda", Type = ParameterType.String }
            }
        },

        // Recordings
        new()
        {
            Id = "getMeetingRecordings",
            Name = "Get Meeting Recordings",
            Description = "Get recordings for a meeting",
            Parameters = new ActionParameter[]
            {
                new() { Name = "meetingId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "getUserRecordings",
            Name = "Get User Recordings",
            Description = "List recordings for a user",
            Parameters = new ActionParameter[]
            {
                new() { Name = "userId", Type = ParameterType.String, Required = true },
                new() { Name = "pageSize", Type = ParameterType.Number, DefaultValue = 30 },
                new() { Name = "from", Type = ParameterType.DateTime, Description = "Start date (yyyy-MM-dd)" },
                new() { Name = "to", Type = ParameterType.DateTime, Description = "End date (yyyy-MM-dd)" }
            }
        },
        new()
        {
            Id = "deleteRecording",
            Name = "Delete Recording",
            Description = "Delete a recording",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "meetingId", Type = ParameterType.String, Required = true },
                new() { Name =="action", Type = ParameterType.String, DefaultValue = "trash", Description = "trash, delete" }
            }
        },

        // Reports
        new()
        {
            Id = "getMeetingParticipants",
            Name = "Get Meeting Participants",
            Description = "Get participant report for a past meeting",
            Parameters = new ActionParameter[]
            {
                new() { Name = "meetingId", Type = ParameterType.String, Required = true },
                new() { Name = "pageSize", Type = ParameterType.Number, DefaultValue = 30 }
            }
        },
        new()
        {
            Id = "getWebinarParticipants",
            Name = "Get Webinar Participants",
            Description = "Get participant report for a past webinar",
            Parameters = new ActionParameter[]
            {
                new() { Name = "webinarId", Type = ParameterType.String, Required = true },
                new() { Name = "pageSize", Type = ParameterType.Number, DefaultValue = 30 }
            }
        },

        // Cloud Recordings
        new()
        {
            Id = "getCloudRecordings",
            Name = "Get Cloud Recordings",
            Description = "List all cloud recordings for an account",
            Parameters = new ActionParameter[]
            {
                new() { Name = "from", Type = ParameterType.DateTime, Required = true, Description = "Start date (yyyy-MM-dd)" },
                new() { Name = "to", Type = ParameterType.DateTime, Required = true, Description = "End date (yyyy-MM-dd)" },
                new() { Name = "pageSize", Type = ParameterType.Number, DefaultValue = 30 }
            }
        },

        // Webhooks
        new()
        {
            Id = "createWebhook",
            Name = "Create Webhook",
            Description = "Create a webhook subscription (requires webhook app)",
            Parameters = new ActionParameter[]
            {
                new() { Name = "url", Type = ParameterType.String, Required = true, Description = "Webhook endpoint URL" },
                new() { Name = "events", Type = ParameterType.String, Required = true, Description = "Comma-separated event types" },
                new() { Name = "authUser", Type = ParameterType.String, Description = "Basic auth username" },
                new() { Name = "authPassword", Type = ParameterType.Password, Description = "Basic auth password" }
            }
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "meetingStarted",
            Name = "Meeting Started",
            Description = "Triggered when a meeting starts",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "meetingEnded",
            Name = "Meeting Ended",
            Description = "Triggered when a meeting ends",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "participantJoined",
            Name = "Participant Joined",
            Description = "Triggered when a participant joins a meeting",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "recordingCompleted",
            Name = "Recording Completed",
            Description = "Triggered when a recording is completed",
            Type = TriggerType.Webhook
        }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        await base.InitializeAsync(config, ct);

        var accessToken = config.GetCredentialString("accessToken");

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.zoom.us/v2/")
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
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
            "getUsers" => await GetAsync($"users?status={parameters.GetString("status") ?? "active"}&page_size={parameters.GetInt("pageSize", 30)}", ct),
            "getUser" => await GetAsync($"users/{parameters.GetString("userId")}", ct),
            "createUser" => await CreateUserAsync(parameters, ct),
            "updateUser" => await UpdateUserAsync(parameters, ct),
            "deleteUser" => await DeleteAsync($"users/{parameters.GetString("userId")}?action={parameters.GetString("action") ?? "delete"}", ct),

            "getMeetings" => await GetAsync($"users/{parameters.GetString("userId")}/meetings?type={parameters.GetString("type") ?? "scheduled"}&page_size={parameters.GetInt("pageSize", 30)}", ct),
            "getMeeting" => await GetAsync($"meetings/{parameters.GetString("meetingId")}", ct),
            "createMeeting" => await CreateMeetingAsync(parameters, ct),
            "updateMeeting" => await UpdateMeetingAsync(parameters, ct),
            "deleteMeeting" => await DeleteAsync($"meetings/{parameters.GetString("meetingId")}", ct),

            "getWebinars" => await GetAsync($"users/{parameters.GetString("userId")}/webinars?page_size={parameters.GetInt("pageSize", 30)}", ct),
            "getWebinar" => await GetAsync($"webinars/{parameters.GetString("webinarId")}", ct),
            "createWebinar" => await CreateWebinarAsync(parameters, ct),

            "getMeetingRecordings" => await GetAsync($"meetings/{parameters.GetString("meetingId")}/recordings", ct),
            "getUserRecordings" => await GetUserRecordingsAsync(parameters, ct),
            "deleteRecording" => await DeleteAsync($"meetings/{parameters.GetString("meetingId")}/recordings?action={parameters.GetString("action") ?? "trash"}", ct),

            "getMeetingParticipants" => await GetAsync($"report/meetings/{parameters.GetString("meetingId")}/participants?page_size={parameters.GetInt("pageSize", 30)}", ct),
            "getWebinarParticipants" => await GetAsync($"report/webinars/{parameters.GetString("webinarId")}/participants?page_size={parameters.GetInt("pageSize", 30)}", ct),

            "getCloudRecordings" => await GetCloudRecordingsAsync(parameters, ct),

            "createWebhook" => await CreateWebhookAsync(parameters, ct),

            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> CreateUserAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = new
        {
            action = "create",
            user_info = new
            {
                email = parameters.GetString("email")!,
                type = parameters.GetInt("type", 1),
                first_name = parameters.GetString("firstName") ?? "",
                last_name = parameters.GetString("lastName") ?? ""
            }
        };

        return await PostAsync("users", payload, ct);
    }

    private async Task<ActionResult> UpdateUserAsync(ActionParameters parameters, CancellationToken ct)
    {
        var userId = parameters.GetString("userId")!;
        var updates = new Dictionary<string, object>();

        var firstName = parameters.GetString("firstName");
        if (!string.IsNullOrEmpty(firstName))
            updates["first_name"] = firstName;

        var lastName = parameters.GetString("lastName");
        if (!string.IsNullOrEmpty(lastName))
            updates["last_name"] = lastName;

        if (parameters.Has("type"))
            updates["type"] = parameters.GetInt("type", 1);

        if (parameters.Has("pmi"))
            updates["pmi"] = parameters.GetInt("pmi", 0);

        return await PatchAsync($"users/{userId}", updates, ct);
    }

    private async Task<ActionResult> CreateMeetingAsync(ActionParameters parameters, CancellationToken ct)
    {
        var userId = parameters.GetString("userId")!;
        var meeting = new Dictionary<string, object>
        {
            ["topic"] = parameters.GetString("topic")!,
            ["type"] = parameters.GetInt("type", 2),
            ["timezone"] = parameters.GetString("timezone") ?? "UTC"
        };

        var startTime = parameters.GetString("startTime");
        if (!string.IsNullOrEmpty(startTime))
            meeting["start_time"] = startTime;

        if (parameters.Has("duration"))
            meeting["duration"] = parameters.GetInt("duration", 0);

        var password = parameters.GetString("password");
        if (!string.IsNullOrEmpty(password))
            meeting["password"] = password;

        var agenda = parameters.GetString("agenda");
        if (!string.IsNullOrEmpty(agenda))
            meeting["agenda"] = agenda;

        var autoRecording = parameters.GetString("autoRecording");
        if (!string.IsNullOrEmpty(autoRecording))
        {
            meeting["settings"] = new Dictionary<string, object>
            {
                ["auto_recording"] = autoRecording
            };
        }

        return await PostAsync($"users/{userId}/meetings", meeting, ct);
    }

    private async Task<ActionResult> UpdateMeetingAsync(ActionParameters parameters, CancellationToken ct)
    {
        var meetingId = parameters.GetString("meetingId")!;
        var updates = new Dictionary<string, object>();

        var topic = parameters.GetString("topic");
        if (!string.IsNullOrEmpty(topic))
            updates["topic"] = topic;

        var startTime = parameters.GetString("startTime");
        if (!string.IsNullOrEmpty(startTime))
            updates["start_time"] = startTime;

        if (parameters.Has("duration"))
            updates["duration"] = parameters.GetInt("duration", 0);

        var agenda = parameters.GetString("agenda");
        if (!string.IsNullOrEmpty(agenda))
            updates["agenda"] = agenda;

        return await PatchAsync($"meetings/{meetingId}", updates, ct);
    }

    private async Task<ActionResult> CreateWebinarAsync(ActionParameters parameters, CancellationToken ct)
    {
        var userId = parameters.GetString("userId")!;
        var webinar = new Dictionary<string, object>
        {
            ["topic"] = parameters.GetString("topic")!,
            ["type"] = parameters.GetInt("type", 5),
            ["timezone"] = parameters.GetString("timezone") ?? "UTC"
        };

        var startTime = parameters.GetString("startTime");
        if (!string.IsNullOrEmpty(startTime))
            webinar["start_time"] = startTime;

        if (parameters.Has("duration"))
            webinar["duration"] = parameters.GetInt("duration", 0);

        var agenda = parameters.GetString("agenda");
        if (!string.IsNullOrEmpty(agenda))
            webinar["agenda"] = agenda;

        return await PostAsync($"users/{userId}/webinars", webinar, ct);
    }

    private async Task<ActionResult> GetUserRecordingsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var userId = parameters.GetString("userId")!;
        var queryParams = new List<string>
        {
            $"page_size={parameters.GetInt("pageSize", 30)}"
        };

        var from = parameters.GetString("from");
        if (!string.IsNullOrEmpty(from))
            queryParams.Add($"from={from}");

        var to = parameters.GetString("to");
        if (!string.IsNullOrEmpty(to))
            queryParams.Add($"to={to}");

        var query = string.Join("&", queryParams);
        return await GetAsync($"users/{userId}/recordings?{query}", ct);
    }

    private async Task<ActionResult> GetCloudRecordingsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var from = parameters.GetString("from")!;
        var to = parameters.GetString("to")!;
        var pageSize = parameters.GetInt("pageSize", 30);

        return await GetAsync($"accounts/mine/recordings?from={from}&to={to}&page_size={pageSize}", ct);
    }

    private async Task<ActionResult> CreateWebhookAsync(ActionParameters parameters, CancellationToken ct)
    {
        var url = parameters.GetString("url")!;
        var events = parameters.GetString("events")!.Split(',').Select(e => e.Trim()).ToArray();

        var webhook = new Dictionary<string, object>
        {
            ["url"] = url,
            ["events"] = events
        };

        var authUser = parameters.GetString("authUser");
        var authPassword = parameters.GetString("authPassword");
        if (!string.IsNullOrEmpty(authUser) && !string.IsNullOrEmpty(authPassword))
        {
            webhook["auth_user"] = authUser;
            webhook["auth_password"] = authPassword;
        }

        return await PostAsync("webhooks", webhook, ct);
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

    private async Task<ActionResult> PatchAsync(string endpoint, object payload, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient!.PatchAsync(endpoint, content, ct);
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
            if (string.IsNullOrEmpty(content) || response.StatusCode == System.Net.HttpStatusCode.NoContent)
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
            if (doc.RootElement.TryGetProperty("message", out var message))
            {
                return ActionResult.Fail($"Zoom error ({response.StatusCode}): {message.GetString()}");
            }
        }
        catch { }

        return ActionResult.Fail($"Zoom error ({response.StatusCode}): {content}");
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
