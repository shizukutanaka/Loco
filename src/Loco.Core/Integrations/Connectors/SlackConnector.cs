// John Carmack: "Make it work, make it right, make it fast"
// Rob Pike: "Don't communicate by sharing memory; share memory by communicating"

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;
using Loco.Core.Practical;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Slack connector for messaging, channels, and user management
/// Uses Slack Web API with OAuth2 or Bot Token authentication
/// </summary>
public sealed class SlackConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private string? _token;
    private const string SlackApiBase = "https://slack.com/api";

    public override string Id => "slack";
    public override string Name => "Slack";
    public override string Description => "Send messages, manage channels, and interact with Slack workspaces";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Communication;

    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.ForMessaging();

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.OAuth2,
        AuthorizationUrl = "https://slack.com/oauth/v2/authorize",
        TokenUrl = "https://slack.com/api/oauth.v2.access",
        Scopes = ["channels:read", "channels:write", "chat:write", "users:read", "files:write"],
        RequiredCredentials =
        [
            new() { Name = "botToken", Label = "Bot Token", Type = ParameterType.Password, Required = true,
                Description = "Slack Bot Token (xoxb-...)" },
            new() { Name = "signingSecret", Label = "Signing Secret", Type = ParameterType.Password, Required = false,
                Description = "App Signing Secret for webhook verification" }
        ]
    };

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        new()
        {
            Id = "sendMessage",
            Name = "Send Message",
            Description = "Send a message to a channel or user",
            Parameters =
            [
                new() { Name = "channel", Type = ParameterType.String, Required = true,
                    Description = "Channel ID (C...) or User ID (U...) to send message to" },
                new() { Name = "text", Type = ParameterType.String, Required = true,
                    Description = "Message text (supports Slack markdown)" },
                new() { Name = "blocks", Type = ParameterType.Json,
                    Description = "Block Kit blocks for rich formatting" },
                new() { Name = "attachments", Type = ParameterType.Json,
                    Description = "Legacy attachments" },
                new() { Name = "threadTs", Type = ParameterType.String,
                    Description = "Thread timestamp for reply" },
                new() { Name = "unfurlLinks", Type = ParameterType.Boolean, DefaultValue = true,
                    Description = "Unfurl URLs in message" }
            ],
            RetryConfig = new RetryConfig { MaxAttempts = 3 }
        },
        new()
        {
            Id = "sendDirectMessage",
            Name = "Send Direct Message",
            Description = "Send a direct message to a user",
            Parameters =
            [
                new() { Name = "userId", Type = ParameterType.String, Required = true,
                    Description = "User ID to send DM to" },
                new() { Name = "text", Type = ParameterType.String, Required = true,
                    Description = "Message text" },
                new() { Name = "blocks", Type = ParameterType.Json,
                    Description = "Block Kit blocks" }
            ]
        },
        new()
        {
            Id = "updateMessage",
            Name = "Update Message",
            Description = "Update an existing message",
            Parameters =
            [
                new() { Name = "channel", Type = ParameterType.String, Required = true },
                new() { Name = "ts", Type = ParameterType.String, Required = true,
                    Description = "Timestamp of message to update" },
                new() { Name = "text", Type = ParameterType.String, Required = true },
                new() { Name = "blocks", Type = ParameterType.Json }
            ]
        },
        new()
        {
            Id = "deleteMessage",
            Name = "Delete Message",
            Description = "Delete a message",
            Parameters =
            [
                new() { Name = "channel", Type = ParameterType.String, Required = true },
                new() { Name = "ts", Type = ParameterType.String, Required = true,
                    Description = "Timestamp of message to delete" }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "addReaction",
            Name = "Add Reaction",
            Description = "Add emoji reaction to a message",
            Parameters =
            [
                new() { Name = "channel", Type = ParameterType.String, Required = true },
                new() { Name = "timestamp", Type = ParameterType.String, Required = true },
                new() { Name = "emoji", Type = ParameterType.String, Required = true,
                    Description = "Emoji name without colons (e.g., 'thumbsup')" }
            ]
        },
        new()
        {
            Id = "listChannels",
            Name = "List Channels",
            Description = "Get list of channels",
            Parameters =
            [
                new() { Name = "types", Type = ParameterType.String, DefaultValue = "public_channel",
                    Description = "Channel types: public_channel, private_channel, mpim, im" },
                new() { Name = "limit", Type = ParameterType.Number, DefaultValue = 100 },
                new() { Name = "excludeArchived", Type = ParameterType.Boolean, DefaultValue = true }
            ]
        },
        new()
        {
            Id = "createChannel",
            Name = "Create Channel",
            Description = "Create a new channel",
            Parameters =
            [
                new() { Name = "name", Type = ParameterType.String, Required = true,
                    Description = "Channel name (lowercase, no spaces)" },
                new() { Name = "isPrivate", Type = ParameterType.Boolean, DefaultValue = false }
            ]
        },
        new()
        {
            Id = "inviteToChannel",
            Name = "Invite to Channel",
            Description = "Invite users to a channel",
            Parameters =
            [
                new() { Name = "channel", Type = ParameterType.String, Required = true },
                new() { Name = "users", Type = ParameterType.String, Required = true,
                    Description = "Comma-separated user IDs" }
            ]
        },
        new()
        {
            Id = "getUserInfo",
            Name = "Get User Info",
            Description = "Get information about a user",
            Parameters =
            [
                new() { Name = "userId", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "lookupUserByEmail",
            Name = "Lookup User by Email",
            Description = "Find a user by email address",
            Parameters =
            [
                new() { Name = "email", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "setStatus",
            Name = "Set Status",
            Description = "Set user status",
            Parameters =
            [
                new() { Name = "statusText", Type = ParameterType.String, Required = true },
                new() { Name = "statusEmoji", Type = ParameterType.String, DefaultValue = ":speech_balloon:" },
                new() { Name = "expirationMinutes", Type = ParameterType.Number,
                    Description = "Status expiration in minutes (0 = never)" }
            ]
        },
        new()
        {
            Id = "uploadFile",
            Name = "Upload File",
            Description = "Upload a file to a channel",
            Parameters =
            [
                new() { Name = "channels", Type = ParameterType.String, Required = true,
                    Description = "Comma-separated channel IDs" },
                new() { Name = "filePath", Type = ParameterType.String, Required = true,
                    Description = "Local file path" },
                new() { Name = "title", Type = ParameterType.String,
                    Description = "File title" },
                new() { Name = "initialComment", Type = ParameterType.String,
                    Description = "Initial comment" }
            ]
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "message",
            Name = "New Message",
            Description = "Triggered when a new message is posted",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "channelFilter", Type = ParameterType.String,
                    Description = "Channel ID filter (optional)" },
                new() { Name = "mentionOnly", Type = ParameterType.Boolean, DefaultValue = false,
                    Description = "Only trigger on mentions" }
            ]
        },
        new()
        {
            Id = "reaction",
            Name = "Reaction Added",
            Description = "Triggered when a reaction is added",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "emojiFilter", Type = ParameterType.String,
                    Description = "Emoji name filter" }
            ]
        }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        _token = config.GetCredentialString("botToken");

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _token);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

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
            "sendMessage" => await SendMessageAsync(parameters, ct),
            "sendDirectMessage" => await SendDirectMessageAsync(parameters, ct),
            "updateMessage" => await UpdateMessageAsync(parameters, ct),
            "deleteMessage" => await DeleteMessageAsync(parameters, ct),
            "addReaction" => await AddReactionAsync(parameters, ct),
            "listChannels" => await ListChannelsAsync(parameters, ct),
            "createChannel" => await CreateChannelAsync(parameters, ct),
            "inviteToChannel" => await InviteToChannelAsync(parameters, ct),
            "getUserInfo" => await GetUserInfoAsync(parameters, ct),
            "lookupUserByEmail" => await LookupUserByEmailAsync(parameters, ct),
            "setStatus" => await SetStatusAsync(parameters, ct),
            "uploadFile" => await UploadFileAsync(parameters, ct),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> SendMessageAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["channel"] = parameters.GetString("channel"),
            ["text"] = parameters.GetString("text"),
            ["unfurl_links"] = parameters.GetBool("unfurlLinks", true)
        };

        var blocks = parameters.Get<object>("blocks");
        if (blocks != null) payload["blocks"] = blocks;

        var attachments = parameters.Get<object>("attachments");
        if (attachments != null) payload["attachments"] = attachments;

        var threadTs = parameters.GetString("threadTs");
        if (!string.IsNullOrEmpty(threadTs)) payload["thread_ts"] = threadTs;

        return await CallSlackApiAsync("chat.postMessage", payload, ct);
    }

    private async Task<ActionResult> SendDirectMessageAsync(ActionParameters parameters, CancellationToken ct)
    {
        // First, open a DM channel
        var openResult = await CallSlackApiAsync("conversations.open", new Dictionary<string, object?>
        {
            ["users"] = parameters.GetString("userId")
        }, ct);

        if (!openResult.Success) return openResult;

        var channelData = openResult.Data as Dictionary<string, object?>;
        var channel = (channelData?["channel"] as Dictionary<string, object?>)?["id"]?.ToString();

        if (string.IsNullOrEmpty(channel))
        {
            return ActionResult.Fail("Failed to open DM channel");
        }

        // Send the message
        var payload = new Dictionary<string, object?>
        {
            ["channel"] = channel,
            ["text"] = parameters.GetString("text")
        };

        var blocks = parameters.Get<object>("blocks");
        if (blocks != null) payload["blocks"] = blocks;

        return await CallSlackApiAsync("chat.postMessage", payload, ct);
    }

    private async Task<ActionResult> UpdateMessageAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["channel"] = parameters.GetString("channel"),
            ["ts"] = parameters.GetString("ts"),
            ["text"] = parameters.GetString("text")
        };

        var blocks = parameters.Get<object>("blocks");
        if (blocks != null) payload["blocks"] = blocks;

        return await CallSlackApiAsync("chat.update", payload, ct);
    }

    private async Task<ActionResult> DeleteMessageAsync(ActionParameters parameters, CancellationToken ct)
    {
        return await CallSlackApiAsync("chat.delete", new Dictionary<string, object?>
        {
            ["channel"] = parameters.GetString("channel"),
            ["ts"] = parameters.GetString("ts")
        }, ct);
    }

    private async Task<ActionResult> AddReactionAsync(ActionParameters parameters, CancellationToken ct)
    {
        return await CallSlackApiAsync("reactions.add", new Dictionary<string, object?>
        {
            ["channel"] = parameters.GetString("channel"),
            ["timestamp"] = parameters.GetString("timestamp"),
            ["name"] = parameters.GetString("emoji")
        }, ct);
    }

    private async Task<ActionResult> ListChannelsAsync(ActionParameters parameters, CancellationToken ct)
    {
        return await CallSlackApiAsync("conversations.list", new Dictionary<string, object?>
        {
            ["types"] = parameters.GetString("types") ?? "public_channel",
            ["limit"] = parameters.GetInt("limit", 100),
            ["exclude_archived"] = parameters.GetBool("excludeArchived", true)
        }, ct);
    }

    private async Task<ActionResult> CreateChannelAsync(ActionParameters parameters, CancellationToken ct)
    {
        return await CallSlackApiAsync("conversations.create", new Dictionary<string, object?>
        {
            ["name"] = parameters.GetString("name"),
            ["is_private"] = parameters.GetBool("isPrivate", false)
        }, ct);
    }

    private async Task<ActionResult> InviteToChannelAsync(ActionParameters parameters, CancellationToken ct)
    {
        return await CallSlackApiAsync("conversations.invite", new Dictionary<string, object?>
        {
            ["channel"] = parameters.GetString("channel"),
            ["users"] = parameters.GetString("users")
        }, ct);
    }

    private async Task<ActionResult> GetUserInfoAsync(ActionParameters parameters, CancellationToken ct)
    {
        return await CallSlackApiAsync("users.info", new Dictionary<string, object?>
        {
            ["user"] = parameters.GetString("userId")
        }, ct);
    }

    private async Task<ActionResult> LookupUserByEmailAsync(ActionParameters parameters, CancellationToken ct)
    {
        return await CallSlackApiAsync("users.lookupByEmail", new Dictionary<string, object?>
        {
            ["email"] = parameters.GetString("email")
        }, ct);
    }

    private async Task<ActionResult> SetStatusAsync(ActionParameters parameters, CancellationToken ct)
    {
        var expiration = parameters.GetInt("expirationMinutes", 0);
        var expirationTime = expiration > 0
            ? DateTimeOffset.UtcNow.AddMinutes(expiration).ToUnixTimeSeconds()
            : 0;

        return await CallSlackApiAsync("users.profile.set", new Dictionary<string, object?>
        {
            ["profile"] = new Dictionary<string, object?>
            {
                ["status_text"] = parameters.GetString("statusText"),
                ["status_emoji"] = parameters.GetString("statusEmoji") ?? ":speech_balloon:",
                ["status_expiration"] = expirationTime
            }
        }, ct);
    }

    private async Task<ActionResult> UploadFileAsync(ActionParameters parameters, CancellationToken ct)
    {
        var filePath = parameters.GetString("filePath")!;

        if (!File.Exists(filePath))
        {
            return ActionResult.Fail($"File not found: {filePath}", "FILE_NOT_FOUND");
        }

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(parameters.GetString("channels")!), "channels");

        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath, ct));
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        var title = parameters.GetString("title");
        if (!string.IsNullOrEmpty(title))
            content.Add(new StringContent(title), "title");

        var comment = parameters.GetString("initialComment");
        if (!string.IsNullOrEmpty(comment))
            content.Add(new StringContent(comment), "initial_comment");

        var response = await _httpClient!.PostAsync($"{SlackApiBase}/files.upload", content, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        return ParseSlackResponse(responseContent);
    }

    private async Task<ActionResult> CallSlackApiAsync(
        string method,
        Dictionary<string, object?> payload,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient!.PostAsync($"{SlackApiBase}/{method}", content, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        return ParseSlackResponse(responseContent);
    }

    private static ActionResult ParseSlackResponse(string responseContent)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(responseContent);

            if (json.TryGetProperty("ok", out var okProp) && okProp.GetBoolean())
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(responseContent);
                return ActionResult.Ok(data);
            }

            var error = json.TryGetProperty("error", out var errorProp)
                ? errorProp.GetString()
                : "Unknown error";

            return ActionResult.Fail($"Slack API error: {error}", error);
        }
        catch (JsonException ex)
        {
            return ActionResult.Fail($"Failed to parse Slack response: {ex.Message}");
        }
    }

    /// <summary>
    /// Verify Slack webhook signature
    /// </summary>
    public static bool VerifyWebhookSignature(
        string body,
        string timestamp,
        string signature,
        string signingSecret)
    {
        var baseString = $"v0:{timestamp}:{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
        var computedSignature = "v0=" + Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature),
            Encoding.UTF8.GetBytes(computedSignature));
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

/// <summary>
/// Slack Block Kit builder for rich messages
/// </summary>
public static class SlackBlockKit
{
    public static object Section(string text, string? accessory = null) => new
    {
        type = "section",
        text = new { type = "mrkdwn", text }
    };

    public static object Header(string text) => new
    {
        type = "header",
        text = new { type = "plain_text", text, emoji = true }
    };

    public static object Divider() => new { type = "divider" };

    public static object Image(string url, string altText, string? title = null) => new
    {
        type = "image",
        image_url = url,
        alt_text = altText,
        title = title != null ? new { type = "plain_text", text = title } : null
    };

    public static object Actions(params object[] elements) => new
    {
        type = "actions",
        elements
    };

    public static object Button(string text, string actionId, string? value = null, string style = "primary") => new
    {
        type = "button",
        text = new { type = "plain_text", text, emoji = true },
        action_id = actionId,
        value = value ?? actionId,
        style
    };

    public static object Context(params string[] texts) => new
    {
        type = "context",
        elements = texts.Select(t => new { type = "mrkdwn", text = t }).ToArray()
    };

    public static object[] RichMessage(string title, string body, string? footer = null)
    {
        var blocks = new List<object>
        {
            Header(title),
            Section(body)
        };

        if (!string.IsNullOrEmpty(footer))
        {
            blocks.Add(Divider());
            blocks.Add(Context(footer));
        }

        return blocks.ToArray();
    }
}
