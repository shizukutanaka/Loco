// John Carmack: "Simple is better than complex"
// Rob Pike: "A little copying is better than a little dependency"

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Twilio connector for SMS, voice, and messaging
/// Supports SMS, MMS, WhatsApp, and voice calls
/// </summary>
public sealed class TwilioConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private string? _accountSid;
    private string? _fromNumber;

    public override string Id => "twilio";
    public override string Name => "Twilio";
    public override string Description => "Send SMS, MMS, WhatsApp messages, and make voice calls via Twilio";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Communication;

    public override ConnectorCapabilities Capabilities => new()
    {
        SupportsActions = true,
        SupportsTriggers = true,
        SupportsWebhooks = true,
        RateLimitPerMinute = 100
    };

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.Basic,
        RequiredCredentials =
        [
            new() { Name = "accountSid", Label = "Account SID", Type = ParameterType.String, Required = true,
                Description = "Twilio Account SID from console" },
            new() { Name = "authToken", Label = "Auth Token", Type = ParameterType.Password, Required = true,
                Description = "Twilio Auth Token from console" }
        ]
    };

    public override IReadOnlyList<ConfigParameter> ConfigParameters =>
    [
        new() { Name = "fromNumber", Label = "Default From Number", Type = ParameterType.String, Required = true,
            Description = "Twilio phone number in E.164 format (+1234567890)" },
        new() { Name = "messagingServiceSid", Label = "Messaging Service SID", Type = ParameterType.String,
            Description = "Optional: Use messaging service instead of phone number" },
        new() { Name = "statusCallbackUrl", Label = "Status Callback URL", Type = ParameterType.String,
            Description = "URL for delivery status webhooks" }
    ];

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        new()
        {
            Id = "sendSms",
            Name = "Send SMS",
            Description = "Send an SMS text message",
            Parameters =
            [
                new() { Name = "to", Type = ParameterType.String, Required = true,
                    Description = "Recipient phone number in E.164 format" },
                new() { Name = "body", Type = ParameterType.String, Required = true,
                    Description = "Message content (max 1600 chars)" },
                new() { Name = "from", Type = ParameterType.String,
                    Description = "Sender phone number (uses default if not specified)" },
                new() { Name = "statusCallback", Type = ParameterType.String,
                    Description = "URL for delivery status webhook" }
            ]
        },
        new()
        {
            Id = "sendMms",
            Name = "Send MMS",
            Description = "Send an MMS message with media",
            Parameters =
            [
                new() { Name = "to", Type = ParameterType.String, Required = true },
                new() { Name = "body", Type = ParameterType.String },
                new() { Name = "mediaUrl", Type = ParameterType.String, Required = true,
                    Description = "URL of the media to send (image, video, etc.)" },
                new() { Name = "from", Type = ParameterType.String }
            ]
        },
        new()
        {
            Id = "sendWhatsApp",
            Name = "Send WhatsApp Message",
            Description = "Send a WhatsApp message (requires WhatsApp-enabled number)",
            Parameters =
            [
                new() { Name = "to", Type = ParameterType.String, Required = true,
                    Description = "WhatsApp number in E.164 format (prefix with whatsapp:)" },
                new() { Name = "body", Type = ParameterType.String, Required = true },
                new() { Name = "from", Type = ParameterType.String,
                    Description = "WhatsApp-enabled Twilio number" },
                new() { Name = "contentSid", Type = ParameterType.String,
                    Description = "Content template SID for pre-approved messages" }
            ]
        },
        new()
        {
            Id = "sendBulkSms",
            Name = "Send Bulk SMS",
            Description = "Send SMS to multiple recipients",
            Parameters =
            [
                new() { Name = "recipients", Type = ParameterType.Json, Required = true,
                    Description = "Array of phone numbers in E.164 format" },
                new() { Name = "body", Type = ParameterType.String, Required = true },
                new() { Name = "from", Type = ParameterType.String }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "makeCall",
            Name = "Make Voice Call",
            Description = "Initiate a voice call with TwiML or URL",
            Parameters =
            [
                new() { Name = "to", Type = ParameterType.String, Required = true },
                new() { Name = "twiml", Type = ParameterType.Code,
                    Description = "TwiML instructions for the call" },
                new() { Name = "url", Type = ParameterType.String,
                    Description = "URL returning TwiML (alternative to inline TwiML)" },
                new() { Name = "from", Type = ParameterType.String },
                new() { Name = "record", Type = ParameterType.Boolean, DefaultValue = false },
                new() { Name = "timeout", Type = ParameterType.Number, DefaultValue = 30 }
            ]
        },
        new()
        {
            Id = "getMessage",
            Name = "Get Message",
            Description = "Get details of a sent message",
            Parameters =
            [
                new() { Name = "messageSid", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "listMessages",
            Name = "List Messages",
            Description = "List recent messages",
            Parameters =
            [
                new() { Name = "to", Type = ParameterType.String, Description = "Filter by recipient" },
                new() { Name = "from", Type = ParameterType.String, Description = "Filter by sender" },
                new() { Name = "dateSent", Type = ParameterType.Date, Description = "Filter by date" },
                new() { Name = "limit", Type = ParameterType.Number, DefaultValue = 20 }
            ]
        },
        new()
        {
            Id = "getCall",
            Name = "Get Call",
            Description = "Get details of a call",
            Parameters =
            [
                new() { Name = "callSid", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "lookupNumber",
            Name = "Lookup Phone Number",
            Description = "Get carrier and caller info for a phone number",
            Parameters =
            [
                new() { Name = "phoneNumber", Type = ParameterType.String, Required = true },
                new() { Name = "type", Type = ParameterType.MultiSelect, DefaultValue = new[] { "carrier" },
                    Options =
                    [
                        new() { Label = "Carrier Info", Value = "carrier" },
                        new() { Label = "Caller Name", Value = "caller-name" }
                    ]}
            ]
        },
        new()
        {
            Id = "verifyStart",
            Name = "Start Verification",
            Description = "Send a verification code to a phone number",
            Parameters =
            [
                new() { Name = "to", Type = ParameterType.String, Required = true },
                new() { Name = "channel", Type = ParameterType.Select, Required = true, DefaultValue = "sms",
                    Options =
                    [
                        new() { Label = "SMS", Value = "sms" },
                        new() { Label = "Voice Call", Value = "call" },
                        new() { Label = "WhatsApp", Value = "whatsapp" },
                        new() { Label = "Email", Value = "email" }
                    ]},
                new() { Name = "serviceSid", Type = ParameterType.String, Required = true,
                    Description = "Verify Service SID from Twilio console" }
            ]
        },
        new()
        {
            Id = "verifyCheck",
            Name = "Check Verification",
            Description = "Verify a code entered by the user",
            Parameters =
            [
                new() { Name = "to", Type = ParameterType.String, Required = true },
                new() { Name = "code", Type = ParameterType.String, Required = true },
                new() { Name = "serviceSid", Type = ParameterType.String, Required = true }
            ]
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "onSmsReceived",
            Name = "On SMS Received",
            Description = "Triggered when an SMS is received",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "fromFilter", Type = ParameterType.String, Description = "Only trigger for messages from this number" },
                new() { Name = "bodyContains", Type = ParameterType.String, Description = "Only trigger if body contains this text" }
            ]
        },
        new()
        {
            Id = "onCallReceived",
            Name = "On Call Received",
            Description = "Triggered when a voice call is received",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "onMessageStatus",
            Name = "On Message Status",
            Description = "Triggered when message status changes (delivered, failed, etc.)",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "status", Type = ParameterType.MultiSelect,
                    Options =
                    [
                        new() { Label = "Delivered", Value = "delivered" },
                        new() { Label = "Failed", Value = "failed" },
                        new() { Label = "Undelivered", Value = "undelivered" }
                    ]}
            ]
        }
    ];

    public override async Task<ConnectionTestResult> TestConnectionAsync(
        ConnectorConfiguration config,
        CancellationToken ct = default)
    {
        try
        {
            var accountSid = config.GetCredentialString("accountSid")!;
            var authToken = config.GetCredentialString("authToken")!;

            using var client = new HttpClient();
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accountSid}:{authToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            var response = await client.GetAsync(
                $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}.json",
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return ConnectionTestResult.Fail($"Authentication failed: {response.StatusCode}");
            }

            var result = await response.Content.ReadAsStringAsync(ct);
            var account = JsonSerializer.Deserialize<JsonElement>(result);
            var friendlyName = account.GetProperty("friendly_name").GetString();
            var status = account.GetProperty("status").GetString();

            return ConnectionTestResult.Ok($"Connected to Twilio account: {friendlyName} (Status: {status})");
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Fail("Connection test failed", ex);
        }
    }

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        _accountSid = config.GetCredentialString("accountSid")!;
        var authToken = config.GetCredentialString("authToken")!;
        _fromNumber = config.GetSettingString("fromNumber");

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}/")
        };

        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_accountSid}:{authToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

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
            "sendSms" => await SendSmsAsync(parameters, ct),
            "sendMms" => await SendMmsAsync(parameters, ct),
            "sendWhatsApp" => await SendWhatsAppAsync(parameters, ct),
            "sendBulkSms" => await SendBulkSmsAsync(parameters, ct),
            "makeCall" => await MakeCallAsync(parameters, ct),
            "getMessage" => await GetMessageAsync(parameters, ct),
            "listMessages" => await ListMessagesAsync(parameters, ct),
            "getCall" => await GetCallAsync(parameters, ct),
            "lookupNumber" => await LookupNumberAsync(parameters, ct),
            "verifyStart" => await StartVerificationAsync(parameters, ct),
            "verifyCheck" => await CheckVerificationAsync(parameters, ct),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> SendSmsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var to = parameters.GetString("to")!;
        var body = parameters.GetString("body")!;
        var from = parameters.GetString("from") ?? _fromNumber;
        var statusCallback = parameters.GetString("statusCallback") ?? Configuration?.GetSettingString("statusCallbackUrl");

        if (string.IsNullOrEmpty(from))
        {
            return ActionResult.Fail("From number is required", "MISSING_PARAMETER");
        }

        var formData = new Dictionary<string, string>
        {
            ["To"] = to,
            ["From"] = from,
            ["Body"] = body
        };

        if (!string.IsNullOrEmpty(statusCallback))
        {
            formData["StatusCallback"] = statusCallback;
        }

        return await SendMessageAsync(formData, ct);
    }

    private async Task<ActionResult> SendMmsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var to = parameters.GetString("to")!;
        var body = parameters.GetString("body") ?? "";
        var mediaUrl = parameters.GetString("mediaUrl")!;
        var from = parameters.GetString("from") ?? _fromNumber;

        if (string.IsNullOrEmpty(from))
        {
            return ActionResult.Fail("From number is required", "MISSING_PARAMETER");
        }

        var formData = new Dictionary<string, string>
        {
            ["To"] = to,
            ["From"] = from,
            ["Body"] = body,
            ["MediaUrl"] = mediaUrl
        };

        return await SendMessageAsync(formData, ct);
    }

    private async Task<ActionResult> SendWhatsAppAsync(ActionParameters parameters, CancellationToken ct)
    {
        var to = parameters.GetString("to")!;
        var body = parameters.GetString("body")!;
        var from = parameters.GetString("from") ?? _fromNumber;
        var contentSid = parameters.GetString("contentSid");

        if (string.IsNullOrEmpty(from))
        {
            return ActionResult.Fail("From number is required", "MISSING_PARAMETER");
        }

        // Ensure WhatsApp prefix
        if (!to.StartsWith("whatsapp:"))
        {
            to = $"whatsapp:{to}";
        }
        if (!from.StartsWith("whatsapp:"))
        {
            from = $"whatsapp:{from}";
        }

        var formData = new Dictionary<string, string>
        {
            ["To"] = to,
            ["From"] = from,
            ["Body"] = body
        };

        if (!string.IsNullOrEmpty(contentSid))
        {
            formData["ContentSid"] = contentSid;
        }

        return await SendMessageAsync(formData, ct);
    }

    private async Task<ActionResult> SendBulkSmsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var recipients = parameters.Get<JsonElement>("recipients");
        var body = parameters.GetString("body")!;
        var from = parameters.GetString("from") ?? _fromNumber;

        if (recipients.ValueKind != JsonValueKind.Array)
        {
            return ActionResult.Fail("Recipients must be an array", "INVALID_PARAMETER");
        }

        var results = new List<object>();
        var succeeded = 0;
        var failed = 0;

        foreach (var recipient in recipients.EnumerateArray())
        {
            var to = recipient.GetString()!;
            var formData = new Dictionary<string, string>
            {
                ["To"] = to,
                ["From"] = from!,
                ["Body"] = body
            };

            var result = await SendMessageAsync(formData, ct);
            if (result.Success)
            {
                succeeded++;
                results.Add(new { to, success = true, messageSid = result.Data });
            }
            else
            {
                failed++;
                results.Add(new { to, success = false, error = result.ErrorMessage });
            }

            // Small delay to avoid rate limiting
            await Task.Delay(50, ct);
        }

        return ActionResult.Ok(new { succeeded, failed, total = succeeded + failed, results });
    }

    private async Task<ActionResult> MakeCallAsync(ActionParameters parameters, CancellationToken ct)
    {
        var to = parameters.GetString("to")!;
        var twiml = parameters.GetString("twiml");
        var url = parameters.GetString("url");
        var from = parameters.GetString("from") ?? _fromNumber;
        var record = parameters.GetBool("record");
        var timeout = parameters.GetInt("timeout", 30);

        if (string.IsNullOrEmpty(from))
        {
            return ActionResult.Fail("From number is required", "MISSING_PARAMETER");
        }

        if (string.IsNullOrEmpty(twiml) && string.IsNullOrEmpty(url))
        {
            return ActionResult.Fail("Either TwiML or URL is required", "MISSING_PARAMETER");
        }

        var formData = new Dictionary<string, string>
        {
            ["To"] = to,
            ["From"] = from,
            ["Timeout"] = timeout.ToString()
        };

        if (!string.IsNullOrEmpty(twiml))
        {
            formData["Twiml"] = twiml;
        }
        else
        {
            formData["Url"] = url!;
        }

        if (record)
        {
            formData["Record"] = "true";
        }

        var content = new FormUrlEncodedContent(formData);
        var response = await _httpClient!.PostAsync("Calls.json", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to make call: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        var call = JsonSerializer.Deserialize<JsonElement>(result);

        return ActionResult.Ok(new
        {
            callSid = call.GetProperty("sid").GetString(),
            status = call.GetProperty("status").GetString(),
            direction = call.GetProperty("direction").GetString()
        });
    }

    private async Task<ActionResult> GetMessageAsync(ActionParameters parameters, CancellationToken ct)
    {
        var messageSid = parameters.GetString("messageSid")!;
        var response = await _httpClient!.GetAsync($"Messages/{messageSid}.json", ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Message not found", "NOT_FOUND");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> ListMessagesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var queryParams = new List<string>();

        var to = parameters.GetString("to");
        var from = parameters.GetString("from");
        var dateSent = parameters.GetString("dateSent");
        var limit = parameters.GetInt("limit", 20);

        if (!string.IsNullOrEmpty(to)) queryParams.Add($"To={Uri.EscapeDataString(to)}");
        if (!string.IsNullOrEmpty(from)) queryParams.Add($"From={Uri.EscapeDataString(from)}");
        if (!string.IsNullOrEmpty(dateSent)) queryParams.Add($"DateSent={Uri.EscapeDataString(dateSent)}");
        queryParams.Add($"PageSize={limit}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        var response = await _httpClient!.GetAsync($"Messages.json{query}", ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Failed to list messages", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        var data = JsonSerializer.Deserialize<JsonElement>(result);

        var messages = new List<object>();
        foreach (var msg in data.GetProperty("messages").EnumerateArray())
        {
            messages.Add(new
            {
                sid = msg.GetProperty("sid").GetString(),
                from = msg.GetProperty("from").GetString(),
                to = msg.GetProperty("to").GetString(),
                body = msg.GetProperty("body").GetString(),
                status = msg.GetProperty("status").GetString(),
                dateSent = msg.TryGetProperty("date_sent", out var ds) ? ds.GetString() : null
            });
        }

        return ActionResult.Ok(new { messages, count = messages.Count });
    }

    private async Task<ActionResult> GetCallAsync(ActionParameters parameters, CancellationToken ct)
    {
        var callSid = parameters.GetString("callSid")!;
        var response = await _httpClient!.GetAsync($"Calls/{callSid}.json", ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Call not found", "NOT_FOUND");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> LookupNumberAsync(ActionParameters parameters, CancellationToken ct)
    {
        var phoneNumber = parameters.GetString("phoneNumber")!;
        var types = parameters.Get<JsonElement>("type");

        var typeList = new List<string>();
        if (types.ValueKind == JsonValueKind.Array)
        {
            typeList = types.EnumerateArray().Select(t => t.GetString()!).ToList();
        }
        else
        {
            typeList.Add("carrier");
        }

        var typeQuery = string.Join("&", typeList.Select(t => $"Type={t}"));

        // Lookup API uses a different base URL
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = _httpClient!.DefaultRequestHeaders.Authorization;

        var response = await client.GetAsync(
            $"https://lookups.twilio.com/v1/PhoneNumbers/{Uri.EscapeDataString(phoneNumber)}?{typeQuery}",
            ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail("Lookup failed", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> StartVerificationAsync(ActionParameters parameters, CancellationToken ct)
    {
        var to = parameters.GetString("to")!;
        var channel = parameters.GetString("channel") ?? "sms";
        var serviceSid = parameters.GetString("serviceSid")!;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = _httpClient!.DefaultRequestHeaders.Authorization;

        var formData = new Dictionary<string, string>
        {
            ["To"] = to,
            ["Channel"] = channel
        };

        var response = await client.PostAsync(
            $"https://verify.twilio.com/v2/Services/{serviceSid}/Verifications",
            new FormUrlEncodedContent(formData),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Verification failed: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        var verification = JsonSerializer.Deserialize<JsonElement>(result);

        return ActionResult.Ok(new
        {
            sid = verification.GetProperty("sid").GetString(),
            status = verification.GetProperty("status").GetString(),
            channel = verification.GetProperty("channel").GetString()
        });
    }

    private async Task<ActionResult> CheckVerificationAsync(ActionParameters parameters, CancellationToken ct)
    {
        var to = parameters.GetString("to")!;
        var code = parameters.GetString("code")!;
        var serviceSid = parameters.GetString("serviceSid")!;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = _httpClient!.DefaultRequestHeaders.Authorization;

        var formData = new Dictionary<string, string>
        {
            ["To"] = to,
            ["Code"] = code
        };

        var response = await client.PostAsync(
            $"https://verify.twilio.com/v2/Services/{serviceSid}/VerificationCheck",
            new FormUrlEncodedContent(formData),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Verification check failed: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        var check = JsonSerializer.Deserialize<JsonElement>(result);
        var status = check.GetProperty("status").GetString();

        return ActionResult.Ok(new
        {
            valid = status == "approved",
            status,
            channel = check.GetProperty("channel").GetString()
        });
    }

    private async Task<ActionResult> SendMessageAsync(Dictionary<string, string> formData, CancellationToken ct)
    {
        var content = new FormUrlEncodedContent(formData);
        var response = await _httpClient!.PostAsync("Messages.json", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            var errorJson = JsonSerializer.Deserialize<JsonElement>(error);
            var errorMessage = errorJson.TryGetProperty("message", out var msg)
                ? msg.GetString()
                : error;
            return ActionResult.Fail($"Failed to send message: {errorMessage}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        var message = JsonSerializer.Deserialize<JsonElement>(result);

        return ActionResult.Ok(new
        {
            messageSid = message.GetProperty("sid").GetString(),
            status = message.GetProperty("status").GetString(),
            to = message.GetProperty("to").GetString(),
            from = message.GetProperty("from").GetString()
        });
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// TwiML builder for voice calls
/// </summary>
public static class TwimlBuilder
{
    public static string Say(string message, string? voice = null, string? language = null)
    {
        var attrs = new List<string>();
        if (voice != null) attrs.Add($"voice=\"{voice}\"");
        if (language != null) attrs.Add($"language=\"{language}\"");
        var attrStr = attrs.Count > 0 ? " " + string.Join(" ", attrs) : "";
        return $"<Say{attrStr}>{EscapeXml(message)}</Say>";
    }

    public static string Play(string url) => $"<Play>{EscapeXml(url)}</Play>";

    public static string Pause(int seconds = 1) => $"<Pause length=\"{seconds}\"/>";

    public static string Gather(string action, int numDigits = 1, int timeout = 5, string? finishOnKey = null)
    {
        var attrs = $"action=\"{EscapeXml(action)}\" numDigits=\"{numDigits}\" timeout=\"{timeout}\"";
        if (finishOnKey != null) attrs += $" finishOnKey=\"{finishOnKey}\"";
        return $"<Gather {attrs}/>";
    }

    public static string Record(string? action = null, int maxLength = 60, bool transcribe = false)
    {
        var attrs = $"maxLength=\"{maxLength}\"";
        if (action != null) attrs += $" action=\"{EscapeXml(action)}\"";
        if (transcribe) attrs += " transcribe=\"true\"";
        return $"<Record {attrs}/>";
    }

    public static string Dial(string number, string? callerId = null, int? timeout = null)
    {
        var attrs = "";
        if (callerId != null) attrs += $" callerId=\"{EscapeXml(callerId)}\"";
        if (timeout != null) attrs += $" timeout=\"{timeout}\"";
        return $"<Dial{attrs}>{EscapeXml(number)}</Dial>";
    }

    public static string Redirect(string url) => $"<Redirect>{EscapeXml(url)}</Redirect>";

    public static string Hangup() => "<Hangup/>";

    public static string Response(params string[] verbs) =>
        $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Response>{string.Join("", verbs)}</Response>";

    private static string EscapeXml(string text) =>
        text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
}
