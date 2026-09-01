using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Discord connector for sending messages, managing channels, and interacting with servers.
/// Uses Discord Bot API.
/// </summary>
public sealed class DiscordConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public override string Id => "discord";
    public override string Name => "Discord";
    public override string Description => "Communication platform for communities with text, voice, and video chat";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Communication;
    public override string IconUrl => "https://discord.com/assets/favicon.ico";

    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.ForMessaging();

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.ApiKey,
        RequiredCredentials = new CredentialField[]
        {
            new() { Name = "botToken", Label = "Bot Token", Type = ParameterType.Password, Description = "Discord Bot Token from Developer Portal" }
        }
    };

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // Messages
        new()
        {
            Id = "sendMessage",
            Name = "Send Message",
            Description = "Send a message to a channel",
            Parameters = new ActionParameter[]
            {
                new() { Name = "channelId", Type = ParameterType.String, Required = true },
                new() { Name = "content", Type = ParameterType.String, Description = "Text content (required if no embed)" },
                new() { Name = "embed", Type = ParameterType.Json, Description = "Rich embed object" },
                new() { Name = "tts", Type = ParameterType.Boolean, DefaultValue = false, Description = "Text-to-Speech" }
            }
        },
        new()
        {
            Id = "sendDirectMessage",
            Name = "Send Direct Message",
            Description = "Send a direct message to a user",
            Parameters = new ActionParameter[]
            {
                new() { Name = "userId", Type = ParameterType.String, Required = true },
                new() { Name = "content", Type = ParameterType.String },
                new() { Name = "embed", Type = ParameterType.Json }
            }
        },
        new()
        {
            Id = "editMessage",
            Name = "Edit Message",
            Description = "Edit an existing message",
            Parameters = new ActionParameter[]
            {
                new() { Name = "channelId", Type = ParameterType.String, Required = true },
                new() { Name = "messageId", Type = ParameterType.String, Required = true },
                new() { Name = "content", Type = ParameterType.String },
                new() { Name = "embed", Type = ParameterType.Json }
            }
        },
        new()
        {
            Id = "deleteMessage",
            Name = "Delete Message",
            Description = "Delete a message",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "channelId", Type = ParameterType.String, Required = true },
                new() { Name = "messageId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "getMessages",
            Name = "Get Messages",
            Description = "Get messages from a channel",
            Parameters = new ActionParameter[]
            {
                new() { Name = "channelId", Type = ParameterType.String, Required = true },
                new() { Name = "limit", Type = ParameterType.Number, DefaultValue = 50, Description = "Number of messages (1-100)" },
                new() { Name = "before", Type = ParameterType.String, Description = "Before Message ID" },
                new() { Name = "after", Type = ParameterType.String, Description = "After Message ID" }
            }
        },
        new()
        {
            Id = "addReaction",
            Name = "Add Reaction",
            Description = "Add a reaction to a message",
            Parameters = new ActionParameter[]
            {
                new() { Name = "channelId", Type = ParameterType.String, Required = true },
                new() { Name = "messageId", Type = ParameterType.String, Required = true },
                new() { Name = "emoji", Type = ParameterType.String, Required = true, Description = "Unicode emoji or custom emoji (name:id)" }
            }
        },

        // Channels
        new()
        {
            Id = "getChannel",
            Name = "Get Channel",
            Description = "Get channel information",
            Parameters = new ActionParameter[]
            {
                new() { Name = "channelId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "createChannel",
            Name = "Create Channel",
            Description = "Create a new channel in a guild",
            Parameters = new ActionParameter[]
            {
                new() { Name = "guildId", Type = ParameterType.String, Required = true, Description = "Server ID" },
                new() { Name = "name", Type = ParameterType.String, Required = true },
                new() { Name = "type", Type = ParameterType.Number, DefaultValue = 0, Description = "0=text, 2=voice, 4=category, 5=announcement" },
                new() { Name = "topic", Type = ParameterType.String },
                new() { Name = "parentId", Type = ParameterType.String, Description = "Category ID" }
            }
        },
        new()
        {
            Id = "deleteChannel",
            Name = "Delete Channel",
            Description = "Delete a channel",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "channelId", Type = ParameterType.String, Required = true }
            }
        },

        // Guilds
        new()
        {
            Id = "getGuild",
            Name = "Get Server",
            Description = "Get server information",
            Parameters = new ActionParameter[]
            {
                new() { Name = "guildId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "getGuildChannels",
            Name = "Get Server Channels",
            Description = "Get all channels in a server",
            Parameters = new ActionParameter[]
            {
                new() { Name = "guildId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "getGuildMembers",
            Name = "Get Server Members",
            Description = "Get members of a server",
            Parameters = new ActionParameter[]
            {
                new() { Name = "guildId", Type = ParameterType.String, Required = true },
                new() { Name = "limit", Type = ParameterType.Number, DefaultValue = 100 },
                new() { Name = "after", Type = ParameterType.String, Description = "After User ID" }
            }
        },

        // Roles
        new()
        {
            Id = "getGuildRoles",
            Name = "Get Server Roles",
            Description = "Get all roles in a server",
            Parameters = new ActionParameter[]
            {
                new() { Name = "guildId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "createRole",
            Name = "Create Role",
            Description = "Create a new role in a server",
            Parameters = new ActionParameter[]
            {
                new() { Name = "guildId", Type = ParameterType.String, Required = true },
                new() { Name = "name", Type = ParameterType.String, Required = true },
                new() { Name = "color", Type = ParameterType.String, Description = "Hex color code without #" },
                new() { Name = "hoist", Type = ParameterType.Boolean, DefaultValue = false, Description = "Display separately" },
                new() { Name = "mentionable", Type = ParameterType.Boolean, DefaultValue = false }
            }
        },
        new()
        {
            Id = "addRoleToMember",
            Name = "Add Role to Member",
            Description = "Add a role to a member",
            Parameters = new ActionParameter[]
            {
                new() { Name = "guildId", Type = ParameterType.String, Required = true },
                new() { Name = "userId", Type = ParameterType.String, Required = true },
                new() { Name = "roleId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "removeRoleFromMember",
            Name = "Remove Role from Member",
            Description = "Remove a role from a member",
            Parameters = new ActionParameter[]
            {
                new() { Name = "guildId", Type = ParameterType.String, Required = true },
                new() { Name = "userId", Type = ParameterType.String, Required = true },
                new() { Name = "roleId", Type = ParameterType.String, Required = true }
            }
        },

        // Users
        new()
        {
            Id = "getUser",
            Name = "Get User",
            Description = "Get user information",
            Parameters = new ActionParameter[]
            {
                new() { Name = "userId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "getCurrentUser",
            Name = "Get Current User",
            Description = "Get the bot's user information",
            Parameters = Array.Empty<ActionParameter>()
        },

        // Webhooks
        new()
        {
            Id = "sendWebhookMessage",
            Name = "Send Webhook Message",
            Description = "Send a message via webhook URL",
            Parameters = new ActionParameter[]
            {
                new() { Name = "webhookUrl", Type = ParameterType.String, Required = true },
                new() { Name = "content", Type = ParameterType.String },
                new() { Name = "username", Type = ParameterType.String, Description = "Username override" },
                new() { Name = "avatarUrl", Type = ParameterType.String, Description = "Avatar URL override" },
                new() { Name = "embed", Type = ParameterType.Json }
            }
        },

        // Moderation
        new()
        {
            Id = "kickMember",
            Name = "Kick Member",
            Description = "Kick a member from a server",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "guildId", Type = ParameterType.String, Required = true },
                new() { Name = "userId", Type = ParameterType.String, Required = true },
                new() { Name = "reason", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "banMember",
            Name = "Ban Member",
            Description = "Ban a member from a server",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "guildId", Type = ParameterType.String, Required = true },
                new() { Name = "userId", Type = ParameterType.String, Required = true },
                new() { Name = "reason", Type = ParameterType.String },
                new() { Name = "deleteMessageDays", Type = ParameterType.Number, Description = "0-7 days of messages to delete" }
            }
        },
        new()
        {
            Id = "unbanMember",
            Name = "Unban Member",
            Description = "Unban a member from a server",
            Parameters = new ActionParameter[]
            {
                new() { Name = "guildId", Type = ParameterType.String, Required = true },
                new() { Name = "userId", Type = ParameterType.String, Required = true }
            }
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "messageCreate",
            Name = "Message Created",
            Description = "Triggered when a message is sent",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "memberJoin",
            Name = "Member Joined",
            Description = "Triggered when a user joins a server",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "memberLeave",
            Name = "Member Left",
            Description = "Triggered when a user leaves a server",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "reactionAdd",
            Name = "Reaction Added",
            Description = "Triggered when a reaction is added",
            Type = TriggerType.Webhook
        }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        await base.InitializeAsync(config, ct);

        var botToken = config.GetCredentialString("botToken");

        // Dispose any previous client before replacing it. InitializeAsync can run more
        // than once for the same cached connector instance (e.g. ConnectorRegistry.
        // GetInitializedConnectorAsync on credential rotation); overwriting _httpClient
        // unconditionally previously leaked the old HttpClient and its socket handler.
        _httpClient?.Dispose();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://discord.com/api/v10/")
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", botToken);
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
            "sendMessage" => await SendMessageAsync(parameters, ct),
            "sendDirectMessage" => await SendDirectMessageAsync(parameters, ct),
            "editMessage" => await EditMessageAsync(parameters, ct),
            "deleteMessage" => await DeleteAsync($"channels/{parameters.GetString("channelId")}/messages/{parameters.GetString("messageId")}", ct),
            "getMessages" => await GetMessagesAsync(parameters, ct),
            "addReaction" => await AddReactionAsync(parameters, ct),

            "getChannel" => await GetAsync($"channels/{parameters.GetString("channelId")}", ct),
            "createChannel" => await CreateChannelAsync(parameters, ct),
            "deleteChannel" => await DeleteAsync($"channels/{parameters.GetString("channelId")}", ct),

            "getGuild" => await GetAsync($"guilds/{parameters.GetString("guildId")}", ct),
            "getGuildChannels" => await GetAsync($"guilds/{parameters.GetString("guildId")}/channels", ct),
            "getGuildMembers" => await GetGuildMembersAsync(parameters, ct),

            "getGuildRoles" => await GetAsync($"guilds/{parameters.GetString("guildId")}/roles", ct),
            "createRole" => await CreateRoleAsync(parameters, ct),
            "addRoleToMember" => await PutAsync($"guilds/{parameters.GetString("guildId")}/members/{parameters.GetString("userId")}/roles/{parameters.GetString("roleId")}", null, ct),
            "removeRoleFromMember" => await DeleteAsync($"guilds/{parameters.GetString("guildId")}/members/{parameters.GetString("userId")}/roles/{parameters.GetString("roleId")}", ct),

            "getUser" => await GetAsync($"users/{parameters.GetString("userId")}", ct),
            "getCurrentUser" => await GetAsync("users/@me", ct),

            "sendWebhookMessage" => await SendWebhookMessageAsync(parameters, ct),

            "kickMember" => await KickMemberAsync(parameters, ct),
            "banMember" => await BanMemberAsync(parameters, ct),
            "unbanMember" => await DeleteAsync($"guilds/{parameters.GetString("guildId")}/bans/{parameters.GetString("userId")}", ct),

            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> SendMessageAsync(ActionParameters parameters, CancellationToken ct)
    {
        var channelId = parameters.GetString("channelId")!;
        var payload = new Dictionary<string, object>();

        var content = parameters.GetString("content");
        if (!string.IsNullOrEmpty(content))
            payload["content"] = content;

        var embed = parameters.Get<JsonElement?>("embed");
        if (embed.HasValue && embed.Value.ValueKind != JsonValueKind.Undefined)
            payload["embeds"] = new[] { embed.Value };

        if (parameters.GetBool("tts"))
            payload["tts"] = true;

        return await PostAsync($"channels/{channelId}/messages", payload, ct);
    }

    private async Task<ActionResult> SendDirectMessageAsync(ActionParameters parameters, CancellationToken ct)
    {
        var userId = parameters.GetString("userId")!;

        var dmChannelResult = await PostAsync("users/@me/channels", new { recipient_id = userId }, ct);
        if (!dmChannelResult.Success)
            return dmChannelResult;

        var dmData = dmChannelResult.Data as Dictionary<string, object>;
        var channelId = dmData?["id"]?.ToString();
        if (string.IsNullOrEmpty(channelId))
            return ActionResult.Fail("Failed to create DM channel");

        var payload = new Dictionary<string, object>();

        var content = parameters.GetString("content");
        if (!string.IsNullOrEmpty(content))
            payload["content"] = content;

        var embed = parameters.Get<JsonElement?>("embed");
        if (embed.HasValue && embed.Value.ValueKind != JsonValueKind.Undefined)
            payload["embeds"] = new[] { embed.Value };

        return await PostAsync($"channels/{channelId}/messages", payload, ct);
    }

    private async Task<ActionResult> EditMessageAsync(ActionParameters parameters, CancellationToken ct)
    {
        var channelId = parameters.GetString("channelId")!;
        var messageId = parameters.GetString("messageId")!;
        var payload = new Dictionary<string, object>();

        var content = parameters.GetString("content");
        if (!string.IsNullOrEmpty(content))
            payload["content"] = content;

        var embed = parameters.Get<JsonElement?>("embed");
        if (embed.HasValue && embed.Value.ValueKind != JsonValueKind.Undefined)
            payload["embeds"] = new[] { embed.Value };

        return await PatchAsync($"channels/{channelId}/messages/{messageId}", payload, ct);
    }

    private async Task<ActionResult> GetMessagesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var channelId = parameters.GetString("channelId")!;
        var queryParams = new List<string>();

        var limit = parameters.GetInt("limit");
        if (limit > 0)
            queryParams.Add($"limit={limit}");

        var before = parameters.GetString("before");
        if (!string.IsNullOrEmpty(before))
            queryParams.Add($"before={before}");

        var after = parameters.GetString("after");
        if (!string.IsNullOrEmpty(after))
            queryParams.Add($"after={after}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await GetAsync($"channels/{channelId}/messages{query}", ct);
    }

    private async Task<ActionResult> AddReactionAsync(ActionParameters parameters, CancellationToken ct)
    {
        var channelId = parameters.GetString("channelId")!;
        var messageId = parameters.GetString("messageId")!;
        var emoji = Uri.EscapeDataString(parameters.GetString("emoji")!);
        return await PutAsync($"channels/{channelId}/messages/{messageId}/reactions/{emoji}/@me", null, ct);
    }

    private async Task<ActionResult> CreateChannelAsync(ActionParameters parameters, CancellationToken ct)
    {
        var guildId = parameters.GetString("guildId")!;
        var payload = new Dictionary<string, object>
        {
            ["name"] = parameters.GetString("name")!
        };

        var type = parameters.GetInt("type");
        if (type > 0)
            payload["type"] = type;

        var topic = parameters.GetString("topic");
        if (!string.IsNullOrEmpty(topic))
            payload["topic"] = topic;

        var parentId = parameters.GetString("parentId");
        if (!string.IsNullOrEmpty(parentId))
            payload["parent_id"] = parentId;

        return await PostAsync($"guilds/{guildId}/channels", payload, ct);
    }

    private async Task<ActionResult> GetGuildMembersAsync(ActionParameters parameters, CancellationToken ct)
    {
        var guildId = parameters.GetString("guildId")!;
        var queryParams = new List<string>();

        var limit = parameters.GetInt("limit");
        if (limit > 0)
            queryParams.Add($"limit={limit}");

        var after = parameters.GetString("after");
        if (!string.IsNullOrEmpty(after))
            queryParams.Add($"after={after}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await GetAsync($"guilds/{guildId}/members{query}", ct);
    }

    private async Task<ActionResult> CreateRoleAsync(ActionParameters parameters, CancellationToken ct)
    {
        var guildId = parameters.GetString("guildId")!;
        var payload = new Dictionary<string, object>
        {
            ["name"] = parameters.GetString("name")!
        };

        var colorHex = parameters.GetString("color");
        if (!string.IsNullOrEmpty(colorHex))
        {
            var colorStr = colorHex.TrimStart('#');
            if (int.TryParse(colorStr, System.Globalization.NumberStyles.HexNumber, null, out var colorInt))
                payload["color"] = colorInt;
        }

        if (parameters.GetBool("hoist"))
            payload["hoist"] = true;

        if (parameters.GetBool("mentionable"))
            payload["mentionable"] = true;

        return await PostAsync($"guilds/{guildId}/roles", payload, ct);
    }

    private async Task<ActionResult> SendWebhookMessageAsync(ActionParameters parameters, CancellationToken ct)
    {
        var webhookUrl = parameters.GetString("webhookUrl")!;
        var payload = new Dictionary<string, object>();

        var content = parameters.GetString("content");
        if (!string.IsNullOrEmpty(content))
            payload["content"] = content;

        var username = parameters.GetString("username");
        if (!string.IsNullOrEmpty(username))
            payload["username"] = username;

        var avatarUrl = parameters.GetString("avatarUrl");
        if (!string.IsNullOrEmpty(avatarUrl))
            payload["avatar_url"] = avatarUrl;

        var embed = parameters.Get<JsonElement?>("embed");
        if (embed.HasValue && embed.Value.ValueKind != JsonValueKind.Undefined)
            payload["embeds"] = new[] { embed.Value };

        using var webhookClient = new HttpClient();
        var contentStr = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");
        var response = await webhookClient.PostAsync(webhookUrl, contentStr, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ActionResult> KickMemberAsync(ActionParameters parameters, CancellationToken ct)
    {
        var guildId = parameters.GetString("guildId")!;
        var userId = parameters.GetString("userId")!;
        return await DeleteAsync(
            $"guilds/{guildId}/members/{userId}", ct, parameters.GetString("reason"));
    }

    private async Task<ActionResult> BanMemberAsync(ActionParameters parameters, CancellationToken ct)
    {
        var guildId = parameters.GetString("guildId")!;
        var userId = parameters.GetString("userId")!;
        var payload = new Dictionary<string, object>();

        var days = parameters.GetInt("deleteMessageDays");
        if (days > 0)
            payload["delete_message_days"] = days;

        return await PutAsync(
            $"guilds/{guildId}/bans/{userId}", payload, ct, parameters.GetString("reason"));
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

    private async Task<ActionResult> PutAsync(
        string endpoint, object? payload, CancellationToken ct, string? auditReason = null)
    {
        HttpContent? content = payload != null
            ? new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
            : new StringContent("", Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Put, endpoint) { Content = content };
        AddAuditReason(request, auditReason);
        var response = await _httpClient!.SendAsync(request, ct);
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

    private async Task<ActionResult> DeleteAsync(
        string endpoint, CancellationToken ct, string? auditReason = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
        AddAuditReason(request, auditReason);
        var response = await _httpClient!.SendAsync(request, ct);
        return await ProcessResponseAsync(response, ct);
    }

    /// <summary>
    /// Attaches Discord's X-Audit-Log-Reason, which is how a kick or a ban
    /// gets a reason recorded beside it in the server's audit log. Both
    /// actions declared a `reason` parameter and neither sent it anywhere, so
    /// a moderator typed one and it went nowhere.
    /// </summary>
    /// <remarks>
    /// The value is user input going into an HTTP header, so it uses the
    /// VALIDATING Add rather than TryAddWithoutValidation: a newline in a
    /// header value is request splitting, and .NET rejects it here rather
    /// than sending it. Discord caps the reason at 512 characters; a longer
    /// one is truncated rather than refused, since losing the tail of an
    /// explanation is better than failing the moderation action.
    /// </remarks>
    private static void AddAuditReason(HttpRequestMessage request, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return;

        var trimmed = reason.Trim();
        if (trimmed.Length > 512) trimmed = trimmed[..512];

        try
        {
            request.Headers.Add("X-Audit-Log-Reason", trimmed);
        }
        catch (FormatException)
        {
            // A reason that cannot be a header value (control characters)
            // must not take the kick or ban down with it.
        }
    }

    private static async Task<ActionResult> ProcessResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            if (string.IsNullOrEmpty(content))
            {
                return ActionResult.Ok(new Dictionary<string, object> { ["status"] = "success" });
            }

            try
            {
                if (content.TrimStart().StartsWith('['))
                {
                    var arrayData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(content);
                    return ActionResult.Ok(new Dictionary<string, object> { ["items"] = arrayData ?? [] });
                }
                else
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                    return ActionResult.Ok(data ?? new Dictionary<string, object>());
                }
            }
            catch
            {
                return ActionResult.Ok(new Dictionary<string, object> { ["response"] = content });
            }
        }

        if ((int)response.StatusCode == 429)
        {
            return ActionResult.Fail($"Discord rate limit exceeded. {content}");
        }

        return ActionResult.Fail($"Discord API error ({response.StatusCode}): {content}");
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
