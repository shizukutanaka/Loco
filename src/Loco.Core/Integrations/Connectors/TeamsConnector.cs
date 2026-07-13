// John Carmack: "Focus on the work that matters most"
// Rob Pike: "Don't communicate by sharing memory, share memory by communicating"

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Microsoft Teams connector for team collaboration
/// Uses Microsoft Graph API for messaging and webhooks for incoming messages
/// </summary>
public sealed class TeamsConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private string? _accessToken;
    private string? _webhookUrl;

    public override string Id => "teams";
    public override string Name => "Microsoft Teams";
    public override string Description => "Send messages, cards, and notifications to Microsoft Teams channels";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Communication;

    public override ConnectorCapabilities Capabilities => new()
    {
        SupportsActions = true,
        SupportsTriggers = true,
        SupportsWebhooks = true,
        RateLimitPerMinute = 60
    };

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.OAuth2,
        AuthorizationUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
        TokenUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/token",
        Scopes = ["ChannelMessage.Send", "Chat.ReadWrite", "Team.ReadBasic.All", "Channel.ReadBasic.All"],
        RequiredCredentials =
        [
            new() { Name = "clientId", Label = "Client ID", Type = ParameterType.String, Required = true },
            new() { Name = "clientSecret", Label = "Client Secret", Type = ParameterType.Password, Required = true },
            new() { Name = "tenantId", Label = "Tenant ID", Type = ParameterType.String, Required = false,
                Description = "Azure AD tenant ID (use 'common' for multi-tenant)" },
            new() { Name = "accessToken", Label = "Access Token", Type = ParameterType.Password, Required = false,
                Description = "Pre-obtained access token (alternative to OAuth flow)" },
            new() { Name = "webhookUrl", Label = "Incoming Webhook URL", Type = ParameterType.String, Required = false,
                Description = "Teams incoming webhook URL for simple message posting" }
        ]
    };

    public override IReadOnlyList<ConfigParameter> ConfigParameters =>
    [
        new() { Name = "defaultTeamId", Label = "Default Team ID", Type = ParameterType.String },
        new() { Name = "defaultChannelId", Label = "Default Channel ID", Type = ParameterType.String }
    ];

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        new()
        {
            Id = "sendMessage",
            Name = "Send Channel Message",
            Description = "Send a message to a Teams channel",
            Parameters =
            [
                new() { Name = "teamId", Type = ParameterType.String, Description = "Team ID (uses default if not specified)" },
                new() { Name = "channelId", Type = ParameterType.String, Description = "Channel ID (uses default if not specified)" },
                new() { Name = "message", Type = ParameterType.String, Required = true, Description = "Message content (supports HTML)" },
                new() { Name = "importance", Type = ParameterType.Select, DefaultValue = "normal",
                    Options =
                    [
                        new() { Label = "Normal", Value = "normal" },
                        new() { Label = "High", Value = "high" },
                        new() { Label = "Urgent", Value = "urgent" }
                    ]}
            ]
        },
        new()
        {
            Id = "sendWebhook",
            Name = "Send Webhook Message",
            Description = "Send a message via incoming webhook (no OAuth required)",
            Parameters =
            [
                new() { Name = "webhookUrl", Type = ParameterType.String, Description = "Webhook URL (uses configured if not specified)" },
                new() { Name = "message", Type = ParameterType.String, Required = true },
                new() { Name = "title", Type = ParameterType.String },
                new() { Name = "themeColor", Type = ParameterType.String, DefaultValue = "0076D7" }
            ]
        },
        new()
        {
            Id = "sendAdaptiveCard",
            Name = "Send Adaptive Card",
            Description = "Send a rich adaptive card to a channel",
            Parameters =
            [
                new() { Name = "teamId", Type = ParameterType.String },
                new() { Name = "channelId", Type = ParameterType.String },
                new() { Name = "card", Type = ParameterType.Json, Required = true, Description = "Adaptive card JSON" }
            ]
        },
        new()
        {
            Id = "sendChat",
            Name = "Send Chat Message",
            Description = "Send a direct chat message to a user",
            Parameters =
            [
                new() { Name = "userId", Type = ParameterType.String, Required = true, Description = "User ID or email" },
                new() { Name = "message", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "listTeams",
            Name = "List Teams",
            Description = "List all teams the bot/user has access to",
            Parameters = []
        },
        new()
        {
            Id = "listChannels",
            Name = "List Channels",
            Description = "List all channels in a team",
            Parameters =
            [
                new() { Name = "teamId", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "getTeamMembers",
            Name = "Get Team Members",
            Description = "Get members of a team",
            Parameters =
            [
                new() { Name = "teamId", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "replyToMessage",
            Name = "Reply to Message",
            Description = "Reply to an existing message in a channel",
            Parameters =
            [
                new() { Name = "teamId", Type = ParameterType.String, Required = true },
                new() { Name = "channelId", Type = ParameterType.String, Required = true },
                new() { Name = "messageId", Type = ParameterType.String, Required = true },
                new() { Name = "message", Type = ParameterType.String, Required = true }
            ]
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "onMessage",
            Name = "On Message",
            Description = "Triggered when a message is posted in a channel",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "teamId", Type = ParameterType.String },
                new() { Name = "channelId", Type = ParameterType.String },
                new() { Name = "keywordFilter", Type = ParameterType.String, Description = "Only trigger on messages containing this keyword" }
            ]
        },
        new()
        {
            Id = "onMention",
            Name = "On Bot Mention",
            Description = "Triggered when the bot is mentioned",
            Type = TriggerType.Webhook
        }
    ];

    public override async Task<ConnectionTestResult> TestConnectionAsync(
        ConnectorConfiguration config,
        CancellationToken ct = default)
    {
        try
        {
            var webhookUrl = config.GetCredentialString("webhookUrl");
            if (!string.IsNullOrEmpty(webhookUrl))
            {
                // Test webhook by sending a test message
                using var client = new HttpClient();
                var testPayload = new { text = "Connection test from Loco" };
                var response = await client.PostAsync(
                    webhookUrl,
                    new StringContent(JsonSerializer.Serialize(testPayload), Encoding.UTF8, "application/json"),
                    ct);

                return response.IsSuccessStatusCode
                    ? ConnectionTestResult.Ok("Webhook connection successful")
                    : ConnectionTestResult.Fail($"Webhook test failed: {response.StatusCode}");
            }

            var accessToken = config.GetCredentialString("accessToken");
            if (!string.IsNullOrEmpty(accessToken))
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await client.GetAsync("https://graph.microsoft.com/v1.0/me/joinedTeams", ct);
                return response.IsSuccessStatusCode
                    ? ConnectionTestResult.Ok("Graph API connection successful")
                    : ConnectionTestResult.Fail($"Graph API test failed: {response.StatusCode}");
            }

            return ConnectionTestResult.Fail("No authentication configured. Provide either webhook URL or access token.");
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Fail("Connection test failed", ex);
        }
    }

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        _webhookUrl = config.GetCredentialString("webhookUrl");
        _accessToken = config.GetCredentialString("accessToken");

        if (!string.IsNullOrEmpty(_accessToken))
        {
            // Dispose any previous client before replacing it. InitializeAsync can run more
            // than once for the same cached connector instance (e.g. ConnectorRegistry.
            // GetInitializedConnectorAsync on credential rotation); overwriting _httpClient
            // unconditionally previously leaked the old HttpClient and its socket handler.
            _httpClient?.Dispose();
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://graph.microsoft.com/v1.0/")
            };
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

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
            "sendMessage" => await SendChannelMessageAsync(parameters, ct),
            "sendWebhook" => await SendWebhookMessageAsync(parameters, ct),
            "sendAdaptiveCard" => await SendAdaptiveCardAsync(parameters, ct),
            "sendChat" => await SendChatMessageAsync(parameters, ct),
            "listTeams" => await ListTeamsAsync(ct),
            "listChannels" => await ListChannelsAsync(parameters, ct),
            "getTeamMembers" => await GetTeamMembersAsync(parameters, ct),
            "replyToMessage" => await ReplyToMessageAsync(parameters, ct),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> SendChannelMessageAsync(ActionParameters parameters, CancellationToken ct)
    {
        if (_httpClient == null)
        {
            return ActionResult.Fail("Graph API not configured. Provide access token.", "NOT_CONFIGURED");
        }

        var teamId = parameters.GetString("teamId") ?? Configuration?.GetSettingString("defaultTeamId");
        var channelId = parameters.GetString("channelId") ?? Configuration?.GetSettingString("defaultChannelId");
        var message = parameters.GetString("message")!;
        var importance = parameters.GetString("importance") ?? "normal";

        if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(channelId))
        {
            return ActionResult.Fail("Team ID and Channel ID are required", "MISSING_PARAMETER");
        }

        var payload = new
        {
            body = new { contentType = "html", content = message },
            importance
        };

        var response = await _httpClient.PostAsync(
            $"teams/{teamId}/channels/{channelId}/messages",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to send message: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        var messageResult = JsonSerializer.Deserialize<JsonElement>(result);

        return ActionResult.Ok(new
        {
            messageId = messageResult.GetProperty("id").GetString(),
            createdDateTime = messageResult.GetProperty("createdDateTime").GetString()
        });
    }

    private async Task<ActionResult> SendWebhookMessageAsync(ActionParameters parameters, CancellationToken ct)
    {
        var webhookUrl = parameters.GetString("webhookUrl") ?? _webhookUrl;
        var message = parameters.GetString("message")!;
        var title = parameters.GetString("title");
        var themeColor = parameters.GetString("themeColor") ?? "0076D7";

        if (string.IsNullOrEmpty(webhookUrl))
        {
            return ActionResult.Fail("Webhook URL is required", "MISSING_PARAMETER");
        }

        // MessageCard format for webhooks
        var payload = new Dictionary<string, object>
        {
            ["@type"] = "MessageCard",
            ["@context"] = "http://schema.org/extensions",
            ["themeColor"] = themeColor,
            ["text"] = message
        };

        if (!string.IsNullOrEmpty(title))
        {
            payload["title"] = title;
        }

        using var client = new HttpClient();
        var response = await client.PostAsync(
            webhookUrl,
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            ct);

        return response.IsSuccessStatusCode
            ? ActionResult.Ok(new { sent = true })
            : ActionResult.Fail($"Webhook failed: {response.StatusCode}", "WEBHOOK_ERROR");
    }

    private async Task<ActionResult> SendAdaptiveCardAsync(ActionParameters parameters, CancellationToken ct)
    {
        if (_httpClient == null)
        {
            return ActionResult.Fail("Graph API not configured", "NOT_CONFIGURED");
        }

        var teamId = parameters.GetString("teamId") ?? Configuration?.GetSettingString("defaultTeamId");
        var channelId = parameters.GetString("channelId") ?? Configuration?.GetSettingString("defaultChannelId");
        var card = parameters.Get<JsonElement>("card");

        if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(channelId))
        {
            return ActionResult.Fail("Team ID and Channel ID are required", "MISSING_PARAMETER");
        }

        var payload = new
        {
            body = new
            {
                contentType = "html",
                content = "<attachment id=\"adaptiveCard\"></attachment>"
            },
            attachments = new[]
            {
                new
                {
                    id = "adaptiveCard",
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = card.GetRawText()
                }
            }
        };

        var response = await _httpClient.PostAsync(
            $"teams/{teamId}/channels/{channelId}/messages",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to send card: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> SendChatMessageAsync(ActionParameters parameters, CancellationToken ct)
    {
        if (_httpClient == null)
        {
            return ActionResult.Fail("Graph API not configured", "NOT_CONFIGURED");
        }

        var userId = parameters.GetString("userId")!;
        var message = parameters.GetString("message")!;

        // First, create or get chat with user
        var chatPayload = new
        {
            chatType = "oneOnOne",
            members = new[]
            {
                new {
                    @odata_type = "#microsoft.graph.aadUserConversationMember",
                    roles = new[] { "owner" },
                    userId = userId
                }
            }
        };

        var chatResponse = await _httpClient.PostAsync(
            "chats",
            new StringContent(JsonSerializer.Serialize(chatPayload), Encoding.UTF8, "application/json"),
            ct);

        if (!chatResponse.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Failed to create chat", "API_ERROR");
        }

        var chatResult = JsonSerializer.Deserialize<JsonElement>(await chatResponse.Content.ReadAsStringAsync(ct));
        var chatId = chatResult.GetProperty("id").GetString();

        // Send message to chat
        var messagePayload = new { body = new { content = message } };
        var msgResponse = await _httpClient.PostAsync(
            $"chats/{chatId}/messages",
            new StringContent(JsonSerializer.Serialize(messagePayload), Encoding.UTF8, "application/json"),
            ct);

        if (!msgResponse.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Failed to send chat message", "API_ERROR");
        }

        return ActionResult.Ok(new { chatId, sent = true });
    }

    private async Task<ActionResult> ListTeamsAsync(CancellationToken ct)
    {
        if (_httpClient == null)
        {
            return ActionResult.Fail("Graph API not configured", "NOT_CONFIGURED");
        }

        var response = await _httpClient.GetAsync("me/joinedTeams", ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Failed to list teams", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        var data = JsonSerializer.Deserialize<JsonElement>(result);

        var teams = new List<object>();
        foreach (var team in data.GetProperty("value").EnumerateArray())
        {
            teams.Add(new
            {
                id = team.GetProperty("id").GetString(),
                displayName = team.GetProperty("displayName").GetString(),
                description = team.TryGetProperty("description", out var desc) ? desc.GetString() : null
            });
        }

        return ActionResult.Ok(new { teams });
    }

    private async Task<ActionResult> ListChannelsAsync(ActionParameters parameters, CancellationToken ct)
    {
        if (_httpClient == null)
        {
            return ActionResult.Fail("Graph API not configured", "NOT_CONFIGURED");
        }

        var teamId = parameters.GetString("teamId")!;
        var response = await _httpClient.GetAsync($"teams/{teamId}/channels", ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Failed to list channels", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        var data = JsonSerializer.Deserialize<JsonElement>(result);

        var channels = new List<object>();
        foreach (var channel in data.GetProperty("value").EnumerateArray())
        {
            channels.Add(new
            {
                id = channel.GetProperty("id").GetString(),
                displayName = channel.GetProperty("displayName").GetString(),
                membershipType = channel.TryGetProperty("membershipType", out var mt) ? mt.GetString() : "standard"
            });
        }

        return ActionResult.Ok(new { channels });
    }

    private async Task<ActionResult> GetTeamMembersAsync(ActionParameters parameters, CancellationToken ct)
    {
        if (_httpClient == null)
        {
            return ActionResult.Fail("Graph API not configured", "NOT_CONFIGURED");
        }

        var teamId = parameters.GetString("teamId")!;
        var response = await _httpClient.GetAsync($"teams/{teamId}/members", ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Failed to get team members", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        var data = JsonSerializer.Deserialize<JsonElement>(result);

        var members = new List<object>();
        foreach (var member in data.GetProperty("value").EnumerateArray())
        {
            members.Add(new
            {
                id = member.GetProperty("id").GetString(),
                displayName = member.TryGetProperty("displayName", out var dn) ? dn.GetString() : null,
                email = member.TryGetProperty("email", out var email) ? email.GetString() : null,
                roles = member.TryGetProperty("roles", out var roles)
                    ? roles.EnumerateArray().Select(r => r.GetString()).ToList()
                    : new List<string?>()
            });
        }

        return ActionResult.Ok(new { members });
    }

    private async Task<ActionResult> ReplyToMessageAsync(ActionParameters parameters, CancellationToken ct)
    {
        if (_httpClient == null)
        {
            return ActionResult.Fail("Graph API not configured", "NOT_CONFIGURED");
        }

        var teamId = parameters.GetString("teamId")!;
        var channelId = parameters.GetString("channelId")!;
        var messageId = parameters.GetString("messageId")!;
        var message = parameters.GetString("message")!;

        var payload = new { body = new { content = message } };
        var response = await _httpClient.PostAsync(
            $"teams/{teamId}/channels/{channelId}/messages/{messageId}/replies",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to reply: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Helper for building Teams Adaptive Cards
/// </summary>
public static class TeamsAdaptiveCard
{
    public static object Create(string? title = null, string? body = null, Action<List<object>>? addActions = null)
    {
        var card = new Dictionary<string, object>
        {
            ["type"] = "AdaptiveCard",
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["version"] = "1.4",
            ["body"] = new List<object>()
        };

        var bodyElements = (List<object>)card["body"];

        if (!string.IsNullOrEmpty(title))
        {
            bodyElements.Add(new
            {
                type = "TextBlock",
                text = title,
                weight = "bolder",
                size = "large"
            });
        }

        if (!string.IsNullOrEmpty(body))
        {
            bodyElements.Add(new
            {
                type = "TextBlock",
                text = body,
                wrap = true
            });
        }

        if (addActions != null)
        {
            var actions = new List<object>();
            addActions(actions);
            card["actions"] = actions;
        }

        return card;
    }

    public static object TextBlock(string text, string? weight = null, string? size = null, string? color = null)
    {
        var block = new Dictionary<string, object>
        {
            ["type"] = "TextBlock",
            ["text"] = text,
            ["wrap"] = true
        };
        if (weight != null) block["weight"] = weight;
        if (size != null) block["size"] = size;
        if (color != null) block["color"] = color;
        return block;
    }

    public static object ActionOpenUrl(string title, string url) => new
    {
        type = "Action.OpenUrl",
        title,
        url
    };

    public static object ActionSubmit(string title, object data) => new
    {
        type = "Action.Submit",
        title,
        data
    };

    public static object FactSet(params (string title, string value)[] facts) => new
    {
        type = "FactSet",
        facts = facts.Select(f => new { title = f.title, value = f.value }).ToArray()
    };

    public static object Image(string url, string? altText = null, string? size = null)
    {
        var img = new Dictionary<string, object>
        {
            ["type"] = "Image",
            ["url"] = url
        };
        if (altText != null) img["altText"] = altText;
        if (size != null) img["size"] = size;
        return img;
    }
}
