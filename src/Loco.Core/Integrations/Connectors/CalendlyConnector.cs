using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Calendly connector for scheduling and calendar automation.
/// Uses Calendly API v2.
/// </summary>
public sealed class CalendlyConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public override string Id => "calendly";
    public override string Name => "Calendly";
    public override string Description => "Scheduling platform for booking meetings and appointments";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Productivity;
    public override string IconUrl => "https://calendly.com/favicon.ico";

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
            new() { Name = "accessToken", Label = "Personal Access Token", Type = ParameterType.Password, Description = "Calendly API token" }
        }
    };

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // Current User
        new()
        {
            Id = "getCurrentUser",
            Name = "Get Current User",
            Description = "Get information about the authenticated user",
            Parameters = Array.Empty<ActionParameter>()
        },

        // Event Types
        new()
        {
            Id = "getEventTypes",
            Name = "Get Event Types",
            Description = "List all event types for a user",
            Parameters = new ActionParameter[]
            {
                new() { Name = "userUri", Type = ParameterType.String, Description = "User URI (uses current user if not specified)" },
                new() { Name = "active", Type = ParameterType.Boolean, Description = "Filter by active status" }
            }
        },
        new()
        {
            Id = "getEventType",
            Name = "Get Event Type",
            Description = "Get details of a specific event type",
            Parameters = new ActionParameter[]
            {
                new() { Name = "eventTypeUri", Type = ParameterType.String, Required = true, Description = "Event type URI" }
            }
        },

        // Scheduled Events
        new()
        {
            Id = "getScheduledEvents",
            Name = "Get Scheduled Events",
            Description = "List scheduled events",
            Parameters = new ActionParameter[]
            {
                new() { Name = "userUri", Type = ParameterType.String, Description = "Filter by user URI" },
                new() { Name = "organizationUri", Type = ParameterType.String, Description = "Filter by organization URI" },
                new() { Name = "minStartTime", Type = ParameterType.DateTime, Description = "Minimum start time (ISO 8601)" },
                new() { Name = "maxStartTime", Type = ParameterType.DateTime, Description = "Maximum start time (ISO 8601)" },
                new() { Name = "count", Type = ParameterType.Number, DefaultValue = 20, Description = "Results per page (max 100)" },
                new() { Name = "status", Type = ParameterType.String, Description = "active, canceled" }
            }
        },
        new()
        {
            Id = "getScheduledEvent",
            Name = "Get Scheduled Event",
            Description = "Get details of a specific scheduled event",
            Parameters = new ActionParameter[]
            {
                new() { Name = "eventUri", Type = ParameterType.String, Required = true, Description = "Event URI" }
            }
        },
        new()
        {
            Id = "cancelScheduledEvent",
            Name = "Cancel Scheduled Event",
            Description = "Cancel a scheduled event",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "eventUri", Type = ParameterType.String, Required = true },
                new() { Name = "reason", Type = ParameterType.String, Description = "Cancellation reason" }
            }
        },

        // Event Invitees
        new()
        {
            Id = "getEventInvitees",
            Name = "Get Event Invitees",
            Description = "List invitees for a scheduled event",
            Parameters = new ActionParameter[]
            {
                new() { Name = "eventUri", Type = ParameterType.String, Required = true },
                new() { Name = "count", Type = ParameterType.Number, DefaultValue = 20 }
            }
        },
        new()
        {
            Id = "getInvitee",
            Name = "Get Invitee",
            Description = "Get details of a specific invitee",
            Parameters = new ActionParameter[]
            {
                new() { Name = "inviteeUri", Type = ParameterType.String, Required = true }
            }
        },

        // Webhooks
        new()
        {
            Id = "createWebhook",
            Name = "Create Webhook",
            Description = "Create a webhook subscription",
            Parameters = new ActionParameter[]
            {
                new() { Name = "url", Type = ParameterType.String, Required = true, Description = "Webhook endpoint URL" },
                new() { Name = "events", Type = ParameterType.String, Required = true, Description = "Comma-separated event types (invitee.created, invitee.canceled)" },
                new() { Name = "organizationUri", Type = ParameterType.String, Required = true },
                new() { Name = "userUri", Type = ParameterType.String, Description = "Filter events by user" },
                new() { Name = "scope", Type = ParameterType.String, DefaultValue = "organization", Description = "organization or user" }
            }
        },
        new()
        {
            Id = "getWebhooks",
            Name = "Get Webhooks",
            Description = "List all webhook subscriptions",
            Parameters = new ActionParameter[]
            {
                new() { Name = "organizationUri", Type = ParameterType.String, Required = true },
                new() { Name = "scope", Type = ParameterType.String, DefaultValue = "organization" }
            }
        },
        new()
        {
            Id = "deleteWebhook",
            Name = "Delete Webhook",
            Description = "Delete a webhook subscription",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "webhookUri", Type = ParameterType.String, Required = true }
            }
        },

        // Organizations
        new()
        {
            Id = "getOrganizationMemberships",
            Name = "Get Organization Memberships",
            Description = "List user's organization memberships",
            Parameters = new ActionParameter[]
            {
                new() { Name = "userUri", Type = ParameterType.String, Description = "User URI (uses current user if not specified)" }
            }
        },

        // Availability
        new()
        {
            Id = "getUserAvailability",
            Name = "Get User Availability",
            Description = "Get a user's available time slots",
            Parameters = new ActionParameter[]
            {
                new() { Name = "eventTypeUri", Type = ParameterType.String, Required = true },
                new() { Name = "startTime", Type = ParameterType.DateTime, Required = true, Description = "Start of date range (ISO 8601)" },
                new() { Name = "endTime", Type = ParameterType.DateTime, Required = true, Description = "End of date range (ISO 8601)" }
            }
        },

        // Routing Forms
        new()
        {
            Id = "getRoutingForms",
            Name = "Get Routing Forms",
            Description = "List all routing forms",
            Parameters = new ActionParameter[]
            {
                new() { Name = "organizationUri", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "getRoutingForm",
            Name = "Get Routing Form",
            Description = "Get details of a specific routing form",
            Parameters = new ActionParameter[]
            {
                new() { Name = "formUri", Type = ParameterType.String, Required = true }
            }
        },

        // Scheduling Links
        new()
        {
            Id = "createSchedulingLink",
            Name = "Create Scheduling Link",
            Description = "Create a single-use scheduling link",
            Parameters = new ActionParameter[]
            {
                new() { Name = "maxEventCount", Type = ParameterType.Number, Required = true, DefaultValue = 1, Description = "Max bookings allowed" },
                new() { Name = "ownerUri", Type = ParameterType.String, Required = true, Description = "Event type owner URI" },
                new() { Name = "ownerType", Type = ParameterType.String, Required = true, DefaultValue = "EventType", Description = "EventType or Group" }
            }
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "inviteeCreated",
            Name = "Invitee Created",
            Description = "Triggered when a new meeting is scheduled",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "inviteeCanceled",
            Name = "Invitee Canceled",
            Description = "Triggered when a meeting is canceled",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "inviteeNoShow",
            Name = "Invitee No-Show",
            Description = "Triggered when an invitee is marked as no-show",
            Type = TriggerType.Webhook
        }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        await base.InitializeAsync(config, ct);

        var accessToken = config.GetCredentialString("accessToken");

        // Dispose any previous client before replacing it. InitializeAsync can run more
        // than once for the same cached connector instance (e.g. ConnectorRegistry.
        // GetInitializedConnectorAsync on credential rotation); overwriting _httpClient
        // unconditionally previously leaked the old HttpClient and its socket handler.
        _httpClient?.Dispose();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.calendly.com/")
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
            "getCurrentUser" => await GetAsync("users/me", ct),

            "getEventTypes" => await GetEventTypesAsync(parameters, ct),
            "getEventType" => await GetByUriAsync(parameters.GetString("eventTypeUri")!, ct),

            "getScheduledEvents" => await GetScheduledEventsAsync(parameters, ct),
            "getScheduledEvent" => await GetByUriAsync(parameters.GetString("eventUri")!, ct),
            "cancelScheduledEvent" => await CancelScheduledEventAsync(parameters, ct),

            "getEventInvitees" => await GetEventInviteesAsync(parameters, ct),
            "getInvitee" => await GetByUriAsync(parameters.GetString("inviteeUri")!, ct),

            "createWebhook" => await CreateWebhookAsync(parameters, ct),
            "getWebhooks" => await GetWebhooksAsync(parameters, ct),
            "deleteWebhook" => await DeleteByUriAsync(parameters.GetString("webhookUri")!, ct),

            "getOrganizationMemberships" => await GetOrganizationMembershipsAsync(parameters, ct),

            "getUserAvailability" => await GetUserAvailabilityAsync(parameters, ct),

            "getRoutingForms" => await GetRoutingFormsAsync(parameters, ct),
            "getRoutingForm" => await GetByUriAsync(parameters.GetString("formUri")!, ct),

            "createSchedulingLink" => await CreateSchedulingLinkAsync(parameters, ct),

            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> GetEventTypesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var queryParams = new List<string>();

        var userUri = parameters.GetString("userUri");
        if (string.IsNullOrEmpty(userUri))
        {
            var currentUser = await GetAsync("users/me", ct);
            if (!currentUser.Success)
                return currentUser;

            var userData = currentUser.Data as Dictionary<string, object>;
            if (userData?.TryGetValue("resource", out var resource) == true)
            {
                var resourceDict = resource as Dictionary<string, object>;
                if (resourceDict?.TryGetValue("uri", out var uri) == true)
                    userUri = uri?.ToString();
            }
        }

        if (!string.IsNullOrEmpty(userUri))
            queryParams.Add($"user={Uri.EscapeDataString(userUri)}");

        if (parameters.Has("active"))
            queryParams.Add($"active={parameters.GetBool("active", true).ToString().ToLower()}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await GetAsync($"event_types{query}", ct);
    }

    private async Task<ActionResult> GetScheduledEventsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var queryParams = new List<string>
        {
            $"count={parameters.GetInt("count", 20)}"
        };

        var userUri = parameters.GetString("userUri");
        if (!string.IsNullOrEmpty(userUri))
            queryParams.Add($"user={Uri.EscapeDataString(userUri)}");

        var organizationUri = parameters.GetString("organizationUri");
        if (!string.IsNullOrEmpty(organizationUri))
            queryParams.Add($"organization={Uri.EscapeDataString(organizationUri)}");

        var minStartTime = parameters.GetString("minStartTime");
        if (!string.IsNullOrEmpty(minStartTime))
            queryParams.Add($"min_start_time={Uri.EscapeDataString(minStartTime)}");

        var maxStartTime = parameters.GetString("maxStartTime");
        if (!string.IsNullOrEmpty(maxStartTime))
            queryParams.Add($"max_start_time={Uri.EscapeDataString(maxStartTime)}");

        var status = parameters.GetString("status");
        if (!string.IsNullOrEmpty(status))
            queryParams.Add($"status={status}");

        var query = "?" + string.Join("&", queryParams);
        return await GetAsync($"scheduled_events{query}", ct);
    }

    private async Task<ActionResult> CancelScheduledEventAsync(ActionParameters parameters, CancellationToken ct)
    {
        var eventUri = parameters.GetString("eventUri")!;
        var payload = new Dictionary<string, object>();

        var reason = parameters.GetString("reason");
        if (!string.IsNullOrEmpty(reason))
            payload["reason"] = reason;

        return await PostAsync($"scheduled_events/{GetUuidFromUri(eventUri)}/cancellation", payload, ct);
    }

    private async Task<ActionResult> GetEventInviteesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var eventUri = parameters.GetString("eventUri")!;
        var count = parameters.GetInt("count", 20);

        return await GetAsync($"scheduled_events/{GetUuidFromUri(eventUri)}/invitees?count={count}", ct);
    }

    private async Task<ActionResult> CreateWebhookAsync(ActionParameters parameters, CancellationToken ct)
    {
        var url = parameters.GetString("url")!;
        var events = parameters.GetString("events")!.Split(',').Select(e => $"invitee.{e.Trim()}").ToArray();
        var organizationUri = parameters.GetString("organizationUri")!;
        var scope = parameters.GetString("scope") ?? "organization";

        var payload = new Dictionary<string, object>
        {
            ["url"] = url,
            ["events"] = events,
            ["organization"] = organizationUri,
            ["scope"] = scope
        };

        var userUri = parameters.GetString("userUri");
        if (!string.IsNullOrEmpty(userUri))
            payload["user"] = userUri;

        return await PostAsync("webhook_subscriptions", payload, ct);
    }

    private async Task<ActionResult> GetWebhooksAsync(ActionParameters parameters, CancellationToken ct)
    {
        var organizationUri = parameters.GetString("organizationUri")!;
        var scope = parameters.GetString("scope") ?? "organization";

        return await GetAsync($"webhook_subscriptions?organization={Uri.EscapeDataString(organizationUri)}&scope={scope}", ct);
    }

    private async Task<ActionResult> GetOrganizationMembershipsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var userUri = parameters.GetString("userUri");

        if (string.IsNullOrEmpty(userUri))
        {
            var currentUser = await GetAsync("users/me", ct);
            if (!currentUser.Success)
                return currentUser;

            var userData = currentUser.Data as Dictionary<string, object>;
            if (userData?.TryGetValue("resource", out var resource) == true)
            {
                var resourceDict = resource as Dictionary<string, object>;
                if (resourceDict?.TryGetValue("uri", out var uri) == true)
                    userUri = uri?.ToString();
            }
        }

        if (string.IsNullOrEmpty(userUri))
            return ActionResult.Fail("Could not determine user URI");

        return await GetAsync($"organization_memberships?user={Uri.EscapeDataString(userUri)}", ct);
    }

    private async Task<ActionResult> GetUserAvailabilityAsync(ActionParameters parameters, CancellationToken ct)
    {
        var eventTypeUri = parameters.GetString("eventTypeUri")!;
        var startTime = parameters.GetString("startTime")!;
        var endTime = parameters.GetString("endTime")!;

        var query = $"event_type={Uri.EscapeDataString(eventTypeUri)}&start_time={Uri.EscapeDataString(startTime)}&end_time={Uri.EscapeDataString(endTime)}";
        return await GetAsync($"user_availability_schedules?{query}", ct);
    }

    private async Task<ActionResult> GetRoutingFormsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var organizationUri = parameters.GetString("organizationUri")!;
        return await GetAsync($"routing_forms?organization={Uri.EscapeDataString(organizationUri)}", ct);
    }

    private async Task<ActionResult> CreateSchedulingLinkAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = new
        {
            max_event_count = parameters.GetInt("maxEventCount", 1),
            owner = parameters.GetString("ownerUri")!,
            owner_type = parameters.GetString("ownerType") ?? "EventType"
        };

        return await PostAsync("scheduling_links", payload, ct);
    }

    private async Task<ActionResult> GetByUriAsync(string uri, CancellationToken ct)
    {
        var uuid = GetUuidFromUri(uri);
        var endpoint = uri.Contains("event_types") ? "event_types" :
                      uri.Contains("scheduled_events") ? "scheduled_events" :
                      uri.Contains("invitees") ? "scheduled_events/invitees" :
                      uri.Contains("routing_forms") ? "routing_forms" :
                      throw new ArgumentException($"Unknown URI type: {uri}");

        return await GetAsync($"{endpoint}/{uuid}", ct);
    }

    private async Task<ActionResult> DeleteByUriAsync(string uri, CancellationToken ct)
    {
        var uuid = GetUuidFromUri(uri);
        var endpoint = uri.Contains("webhook_subscriptions") ? "webhook_subscriptions" :
                      throw new ArgumentException($"Unknown URI type: {uri}");

        return await DeleteAsync($"{endpoint}/{uuid}", ct);
    }

    private static string GetUuidFromUri(string uri)
    {
        return uri.Split('/').Last();
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
                return ActionResult.Fail($"Calendly error ({response.StatusCode}): {message.GetString()}");
            }
        }
        catch { }

        return ActionResult.Fail($"Calendly error ({response.StatusCode}): {content}");
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
