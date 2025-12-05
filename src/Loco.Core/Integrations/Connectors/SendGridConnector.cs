// John Carmack: "Low-level thinking is helpful when solving high-level problems"
// Rob Pike: "Measure. Don't tune for speed until you've measured"

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// SendGrid connector for transactional and marketing email
/// Uses SendGrid Mail Send API v3
/// </summary>
public sealed class SendGridConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private string? _defaultFromEmail;
    private string? _defaultFromName;

    public override string Id => "sendgrid";
    public override string Name => "SendGrid";
    public override string Description => "Send transactional emails, manage contacts, and track email analytics";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Communication;

    public override ConnectorCapabilities Capabilities => new()
    {
        SupportsActions = true,
        SupportsTriggers = true,
        SupportsWebhooks = true,
        SupportsBatching = true,
        RateLimitPerMinute = 600 // SendGrid allows higher rates
    };

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.ApiKey,
        RequiredCredentials =
        [
            new() { Name = "apiKey", Label = "API Key", Type = ParameterType.Password, Required = true,
                Description = "SendGrid API key with Mail Send permissions" }
        ]
    };

    public override IReadOnlyList<ConfigParameter> ConfigParameters =>
    [
        new() { Name = "fromEmail", Label = "Default From Email", Type = ParameterType.String, Required = true },
        new() { Name = "fromName", Label = "Default From Name", Type = ParameterType.String },
        new() { Name = "sandboxMode", Label = "Sandbox Mode", Type = ParameterType.Boolean, DefaultValue = false,
            Description = "Test sending without actually delivering" }
    ];

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // Send Email
        new()
        {
            Id = "sendEmail",
            Name = "Send Email",
            Description = "Send a single email",
            Parameters =
            [
                new() { Name = "to", Type = ParameterType.String, Required = true, Description = "Recipient email" },
                new() { Name = "toName", Type = ParameterType.String },
                new() { Name = "subject", Type = ParameterType.String, Required = true },
                new() { Name = "text", Type = ParameterType.String, Description = "Plain text content" },
                new() { Name = "html", Type = ParameterType.String, Description = "HTML content" },
                new() { Name = "from", Type = ParameterType.String },
                new() { Name = "fromName", Type = ParameterType.String },
                new() { Name = "replyTo", Type = ParameterType.String },
                new() { Name = "cc", Type = ParameterType.Json, Description = "[{email: \"...\", name: \"...\"}]" },
                new() { Name = "bcc", Type = ParameterType.Json },
                new() { Name = "attachments", Type = ParameterType.Json,
                    Description = "[{content: \"base64...\", filename: \"...\", type: \"...\"}]" },
                new() { Name = "categories", Type = ParameterType.Json, Description = "[\"category1\"]" },
                new() { Name = "customArgs", Type = ParameterType.Json, Description = "{\"key\": \"value\"}" }
            ]
        },
        new()
        {
            Id = "sendBulkEmail",
            Name = "Send Bulk Email",
            Description = "Send personalized emails to multiple recipients",
            Parameters =
            [
                new() { Name = "recipients", Type = ParameterType.Json, Required = true,
                    Description = "[{email: \"...\", name: \"...\", substitutions: {...}}]" },
                new() { Name = "subject", Type = ParameterType.String, Required = true },
                new() { Name = "text", Type = ParameterType.String },
                new() { Name = "html", Type = ParameterType.String },
                new() { Name = "from", Type = ParameterType.String },
                new() { Name = "fromName", Type = ParameterType.String }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "sendTemplate",
            Name = "Send Template Email",
            Description = "Send email using a dynamic template",
            Parameters =
            [
                new() { Name = "to", Type = ParameterType.String, Required = true },
                new() { Name = "toName", Type = ParameterType.String },
                new() { Name = "templateId", Type = ParameterType.String, Required = true,
                    Description = "SendGrid dynamic template ID (d-xxx)" },
                new() { Name = "dynamicTemplateData", Type = ParameterType.Json, Required = true,
                    Description = "Template variables: {name: \"John\", order_id: \"123\"}" },
                new() { Name = "from", Type = ParameterType.String },
                new() { Name = "fromName", Type = ParameterType.String }
            ]
        },
        // Contacts
        new()
        {
            Id = "addContact",
            Name = "Add Contact",
            Description = "Add or update a contact",
            Parameters =
            [
                new() { Name = "email", Type = ParameterType.String, Required = true },
                new() { Name = "firstName", Type = ParameterType.String },
                new() { Name = "lastName", Type = ParameterType.String },
                new() { Name = "listIds", Type = ParameterType.Json, Description = "[\"list-id-1\"]" },
                new() { Name = "customFields", Type = ParameterType.Json }
            ]
        },
        new()
        {
            Id = "addContacts",
            Name = "Add Multiple Contacts",
            Description = "Add or update multiple contacts",
            Parameters =
            [
                new() { Name = "contacts", Type = ParameterType.Json, Required = true,
                    Description = "[{email: \"...\", first_name: \"...\"}]" },
                new() { Name = "listIds", Type = ParameterType.Json }
            ]
        },
        new()
        {
            Id = "getContact",
            Name = "Get Contact",
            Description = "Get contact by email",
            Parameters =
            [
                new() { Name = "email", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "deleteContact",
            Name = "Delete Contact",
            Description = "Delete a contact",
            Parameters =
            [
                new() { Name = "email", Type = ParameterType.String },
                new() { Name = "contactId", Type = ParameterType.String }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "searchContacts",
            Name = "Search Contacts",
            Description = "Search contacts using SGQL",
            Parameters =
            [
                new() { Name = "query", Type = ParameterType.String, Required = true,
                    Description = "SGQL query: email LIKE '%@example.com'" }
            ]
        },
        // Lists
        new()
        {
            Id = "createList",
            Name = "Create List",
            Description = "Create a contact list",
            Parameters =
            [
                new() { Name = "name", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "getLists",
            Name = "Get Lists",
            Description = "Get all contact lists",
            Parameters =
            [
                new() { Name = "pageSize", Type = ParameterType.Number, DefaultValue = 100 }
            ]
        },
        new()
        {
            Id = "deleteList",
            Name = "Delete List",
            Description = "Delete a contact list",
            Parameters =
            [
                new() { Name = "listId", Type = ParameterType.String, Required = true },
                new() { Name = "deleteContacts", Type = ParameterType.Boolean, DefaultValue = false }
            ],
            RequiresConfirmation = true
        },
        // Templates
        new()
        {
            Id = "getTemplates",
            Name = "Get Templates",
            Description = "Get all dynamic templates",
            Parameters =
            [
                new() { Name = "generations", Type = ParameterType.Select, DefaultValue = "dynamic",
                    Options =
                    [
                        new() { Label = "Dynamic", Value = "dynamic" },
                        new() { Label = "Legacy", Value = "legacy" }
                    ]},
                new() { Name = "pageSize", Type = ParameterType.Number, DefaultValue = 100 }
            ]
        },
        new()
        {
            Id = "getTemplate",
            Name = "Get Template",
            Description = "Get a template by ID",
            Parameters =
            [
                new() { Name = "templateId", Type = ParameterType.String, Required = true }
            ]
        },
        // Stats
        new()
        {
            Id = "getStats",
            Name = "Get Email Stats",
            Description = "Get email statistics",
            Parameters =
            [
                new() { Name = "startDate", Type = ParameterType.Date, Required = true,
                    Description = "YYYY-MM-DD" },
                new() { Name = "endDate", Type = ParameterType.Date },
                new() { Name = "aggregatedBy", Type = ParameterType.Select, DefaultValue = "day",
                    Options =
                    [
                        new() { Label = "Day", Value = "day" },
                        new() { Label = "Week", Value = "week" },
                        new() { Label = "Month", Value = "month" }
                    ]}
            ]
        },
        new()
        {
            Id = "getCategoryStats",
            Name = "Get Category Stats",
            Description = "Get statistics by category",
            Parameters =
            [
                new() { Name = "categories", Type = ParameterType.Json, Required = true,
                    Description = "[\"category1\", \"category2\"]" },
                new() { Name = "startDate", Type = ParameterType.Date, Required = true },
                new() { Name = "endDate", Type = ParameterType.Date }
            ]
        },
        // Validation
        new()
        {
            Id = "validateEmail",
            Name = "Validate Email",
            Description = "Validate an email address",
            Parameters =
            [
                new() { Name = "email", Type = ParameterType.String, Required = true },
                new() { Name = "source", Type = ParameterType.String, Description = "Source identifier" }
            ]
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "onDelivered",
            Name = "On Email Delivered",
            Description = "Triggered when an email is delivered",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "onOpened",
            Name = "On Email Opened",
            Description = "Triggered when an email is opened",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "onClicked",
            Name = "On Link Clicked",
            Description = "Triggered when a link in the email is clicked",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "onBounced",
            Name = "On Email Bounced",
            Description = "Triggered when an email bounces",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "onUnsubscribed",
            Name = "On Unsubscribe",
            Description = "Triggered when recipient unsubscribes",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "onSpamReport",
            Name = "On Spam Report",
            Description = "Triggered when email is marked as spam",
            Type = TriggerType.Webhook
        }
    ];

    public override async Task<ConnectionTestResult> TestConnectionAsync(
        ConnectorConfiguration config,
        CancellationToken ct = default)
    {
        try
        {
            var apiKey = config.GetCredentialString("apiKey")!;

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await client.GetAsync("https://api.sendgrid.com/v3/user/profile", ct);

            if (!response.IsSuccessStatusCode)
            {
                return ConnectionTestResult.Fail($"Authentication failed: {response.StatusCode}");
            }

            var result = await response.Content.ReadAsStringAsync(ct);
            var profile = JsonSerializer.Deserialize<JsonElement>(result);
            var username = profile.TryGetProperty("username", out var u) ? u.GetString() : "Unknown";

            return ConnectionTestResult.Ok($"Connected to SendGrid ({username})");
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Fail("Connection test failed", ex);
        }
    }

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        var apiKey = config.GetCredentialString("apiKey")!;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.sendgrid.com/v3/")
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _defaultFromEmail = config.GetSettingString("fromEmail");
        _defaultFromName = config.GetSettingString("fromName");

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
            "sendEmail" => await SendEmailAsync(parameters, ct),
            "sendBulkEmail" => await SendBulkEmailAsync(parameters, ct),
            "sendTemplate" => await SendTemplateEmailAsync(parameters, ct),
            "addContact" => await AddContactAsync(parameters, ct),
            "addContacts" => await AddContactsAsync(parameters, ct),
            "getContact" => await GetContactAsync(parameters, ct),
            "deleteContact" => await DeleteContactAsync(parameters, ct),
            "searchContacts" => await SearchContactsAsync(parameters, ct),
            "createList" => await CreateListAsync(parameters, ct),
            "getLists" => await GetListsAsync(parameters, ct),
            "deleteList" => await DeleteListAsync(parameters, ct),
            "getTemplates" => await GetTemplatesAsync(parameters, ct),
            "getTemplate" => await GetTemplateAsync(parameters, ct),
            "getStats" => await GetStatsAsync(parameters, ct),
            "getCategoryStats" => await GetCategoryStatsAsync(parameters, ct),
            "validateEmail" => await ValidateEmailAsync(parameters, ct),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> SendEmailAsync(ActionParameters parameters, CancellationToken ct)
    {
        var to = parameters.GetString("to")!;
        var toName = parameters.GetString("toName");
        var subject = parameters.GetString("subject")!;
        var text = parameters.GetString("text");
        var html = parameters.GetString("html");
        var from = parameters.GetString("from") ?? _defaultFromEmail;
        var fromName = parameters.GetString("fromName") ?? _defaultFromName;
        var replyTo = parameters.GetString("replyTo");

        if (string.IsNullOrEmpty(from))
        {
            return ActionResult.Fail("From email is required", "MISSING_PARAMETER");
        }

        if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(html))
        {
            return ActionResult.Fail("Either text or html content is required", "MISSING_PARAMETER");
        }

        var payload = new Dictionary<string, object>
        {
            ["personalizations"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["to"] = new[] { BuildEmailAddress(to, toName) }
                }
            },
            ["from"] = BuildEmailAddress(from, fromName),
            ["subject"] = subject
        };

        // Add CC/BCC
        var cc = parameters.Get<JsonElement?>("cc");
        if (cc.HasValue && cc.Value.ValueKind == JsonValueKind.Array)
        {
            ((Dictionary<string, object>)((object[])payload["personalizations"])[0])["cc"] =
                cc.Value.EnumerateArray().Select(c => new
                {
                    email = c.GetProperty("email").GetString(),
                    name = c.TryGetProperty("name", out var n) ? n.GetString() : null
                }).ToList();
        }

        var bcc = parameters.Get<JsonElement?>("bcc");
        if (bcc.HasValue && bcc.Value.ValueKind == JsonValueKind.Array)
        {
            ((Dictionary<string, object>)((object[])payload["personalizations"])[0])["bcc"] =
                bcc.Value.EnumerateArray().Select(b => new
                {
                    email = b.GetProperty("email").GetString(),
                    name = b.TryGetProperty("name", out var n) ? n.GetString() : null
                }).ToList();
        }

        // Add content
        var content = new List<object>();
        if (!string.IsNullOrEmpty(text))
        {
            content.Add(new { type = "text/plain", value = text });
        }
        if (!string.IsNullOrEmpty(html))
        {
            content.Add(new { type = "text/html", value = html });
        }
        payload["content"] = content;

        // Add reply-to
        if (!string.IsNullOrEmpty(replyTo))
        {
            payload["reply_to"] = new { email = replyTo };
        }

        // Add attachments
        var attachments = parameters.Get<JsonElement?>("attachments");
        if (attachments.HasValue && attachments.Value.ValueKind == JsonValueKind.Array)
        {
            payload["attachments"] = attachments.Value.EnumerateArray().Select(a => new
            {
                content = a.GetProperty("content").GetString(),
                filename = a.GetProperty("filename").GetString(),
                type = a.TryGetProperty("type", out var t) ? t.GetString() : "application/octet-stream",
                disposition = a.TryGetProperty("disposition", out var d) ? d.GetString() : "attachment"
            }).ToList();
        }

        // Add categories
        var categories = parameters.Get<JsonElement?>("categories");
        if (categories.HasValue && categories.Value.ValueKind == JsonValueKind.Array)
        {
            payload["categories"] = categories.Value.EnumerateArray().Select(c => c.GetString()).ToList();
        }

        // Add custom args
        var customArgs = parameters.Get<JsonElement?>("customArgs");
        if (customArgs.HasValue && customArgs.Value.ValueKind == JsonValueKind.Object)
        {
            ((Dictionary<string, object>)((object[])payload["personalizations"])[0])["custom_args"] = customArgs.Value;
        }

        // Sandbox mode
        var sandboxMode = Configuration?.GetSetting<bool?>("sandboxMode") ?? false;
        if (sandboxMode)
        {
            payload["mail_settings"] = new { sandbox_mode = new { enable = true } };
        }

        return await PostAsync("mail/send", payload, ct);
    }

    private async Task<ActionResult> SendBulkEmailAsync(ActionParameters parameters, CancellationToken ct)
    {
        var recipients = parameters.Get<JsonElement>("recipients");
        var subject = parameters.GetString("subject")!;
        var text = parameters.GetString("text");
        var html = parameters.GetString("html");
        var from = parameters.GetString("from") ?? _defaultFromEmail;
        var fromName = parameters.GetString("fromName") ?? _defaultFromName;

        if (recipients.ValueKind != JsonValueKind.Array)
        {
            return ActionResult.Fail("Recipients must be an array", "INVALID_PARAMETER");
        }

        var personalizations = new List<object>();
        foreach (var recipient in recipients.EnumerateArray())
        {
            var email = recipient.GetProperty("email").GetString();
            var name = recipient.TryGetProperty("name", out var n) ? n.GetString() : null;

            var p = new Dictionary<string, object>
            {
                ["to"] = new[] { BuildEmailAddress(email!, name) }
            };

            if (recipient.TryGetProperty("substitutions", out var subs) && subs.ValueKind == JsonValueKind.Object)
            {
                p["substitutions"] = subs;
            }

            personalizations.Add(p);
        }

        var payload = new Dictionary<string, object>
        {
            ["personalizations"] = personalizations,
            ["from"] = BuildEmailAddress(from!, fromName),
            ["subject"] = subject
        };

        var content = new List<object>();
        if (!string.IsNullOrEmpty(text))
        {
            content.Add(new { type = "text/plain", value = text });
        }
        if (!string.IsNullOrEmpty(html))
        {
            content.Add(new { type = "text/html", value = html });
        }
        payload["content"] = content;

        return await PostAsync("mail/send", payload, ct);
    }

    private async Task<ActionResult> SendTemplateEmailAsync(ActionParameters parameters, CancellationToken ct)
    {
        var to = parameters.GetString("to")!;
        var toName = parameters.GetString("toName");
        var templateId = parameters.GetString("templateId")!;
        var dynamicTemplateData = parameters.Get<JsonElement>("dynamicTemplateData");
        var from = parameters.GetString("from") ?? _defaultFromEmail;
        var fromName = parameters.GetString("fromName") ?? _defaultFromName;

        var personalization = new Dictionary<string, object>
        {
            ["to"] = new[] { BuildEmailAddress(to, toName) },
            ["dynamic_template_data"] = dynamicTemplateData
        };

        var payload = new Dictionary<string, object>
        {
            ["personalizations"] = new[] { personalization },
            ["from"] = BuildEmailAddress(from!, fromName),
            ["template_id"] = templateId
        };

        return await PostAsync("mail/send", payload, ct);
    }

    private async Task<ActionResult> AddContactAsync(ActionParameters parameters, CancellationToken ct)
    {
        var email = parameters.GetString("email")!;
        var firstName = parameters.GetString("firstName");
        var lastName = parameters.GetString("lastName");
        var listIds = parameters.Get<JsonElement?>("listIds");
        var customFields = parameters.Get<JsonElement?>("customFields");

        var contact = new Dictionary<string, object> { ["email"] = email };
        if (!string.IsNullOrEmpty(firstName)) contact["first_name"] = firstName;
        if (!string.IsNullOrEmpty(lastName)) contact["last_name"] = lastName;
        if (customFields.HasValue && customFields.Value.ValueKind == JsonValueKind.Object)
        {
            contact["custom_fields"] = customFields.Value;
        }

        var payload = new Dictionary<string, object> { ["contacts"] = new[] { contact } };

        if (listIds.HasValue && listIds.Value.ValueKind == JsonValueKind.Array)
        {
            payload["list_ids"] = listIds.Value.EnumerateArray().Select(l => l.GetString()).ToList();
        }

        return await PutAsync("marketing/contacts", payload, ct);
    }

    private async Task<ActionResult> AddContactsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var contacts = parameters.Get<JsonElement>("contacts");
        var listIds = parameters.Get<JsonElement?>("listIds");

        var payload = new Dictionary<string, object> { ["contacts"] = contacts };

        if (listIds.HasValue && listIds.Value.ValueKind == JsonValueKind.Array)
        {
            payload["list_ids"] = listIds.Value.EnumerateArray().Select(l => l.GetString()).ToList();
        }

        return await PutAsync("marketing/contacts", payload, ct);
    }

    private async Task<ActionResult> GetContactAsync(ActionParameters parameters, CancellationToken ct)
    {
        var email = parameters.GetString("email")!;

        var payload = new { query = $"email = '{email}'" };
        return await PostAsync("marketing/contacts/search", payload, ct);
    }

    private async Task<ActionResult> DeleteContactAsync(ActionParameters parameters, CancellationToken ct)
    {
        var email = parameters.GetString("email");
        var contactId = parameters.GetString("contactId");

        if (string.IsNullOrEmpty(contactId) && !string.IsNullOrEmpty(email))
        {
            // Look up contact by email first
            var searchResult = await GetContactAsync(parameters, ct);
            if (!searchResult.Success) return searchResult;

            var data = (JsonElement)searchResult.Data!;
            if (data.TryGetProperty("result", out var results) && results.GetArrayLength() > 0)
            {
                contactId = results[0].GetProperty("id").GetString();
            }
            else
            {
                return ActionResult.Fail("Contact not found", "NOT_FOUND");
            }
        }

        if (string.IsNullOrEmpty(contactId))
        {
            return ActionResult.Fail("Contact ID or email is required", "MISSING_PARAMETER");
        }

        var response = await _httpClient!.DeleteAsync($"marketing/contacts?ids={contactId}", ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to delete contact: {error}", "API_ERROR");
        }

        return ActionResult.Ok(new { deleted = true, contactId });
    }

    private async Task<ActionResult> SearchContactsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var query = parameters.GetString("query")!;
        var payload = new { query };
        return await PostAsync("marketing/contacts/search", payload, ct);
    }

    private async Task<ActionResult> CreateListAsync(ActionParameters parameters, CancellationToken ct)
    {
        var name = parameters.GetString("name")!;
        var payload = new { name };
        return await PostAsync("marketing/lists", payload, ct);
    }

    private async Task<ActionResult> GetListsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var pageSize = parameters.GetInt("pageSize", 100);
        return await GetAsync($"marketing/lists?page_size={pageSize}", ct);
    }

    private async Task<ActionResult> DeleteListAsync(ActionParameters parameters, CancellationToken ct)
    {
        var listId = parameters.GetString("listId")!;
        var deleteContacts = parameters.GetBool("deleteContacts");

        var url = $"marketing/lists/{listId}";
        if (deleteContacts)
        {
            url += "?delete_contacts=true";
        }

        var response = await _httpClient!.DeleteAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to delete list: {error}", "API_ERROR");
        }

        return ActionResult.Ok(new { deleted = true, listId });
    }

    private async Task<ActionResult> GetTemplatesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var generations = parameters.GetString("generations") ?? "dynamic";
        var pageSize = parameters.GetInt("pageSize", 100);
        return await GetAsync($"templates?generations={generations}&page_size={pageSize}", ct);
    }

    private async Task<ActionResult> GetTemplateAsync(ActionParameters parameters, CancellationToken ct)
    {
        var templateId = parameters.GetString("templateId")!;
        return await GetAsync($"templates/{templateId}", ct);
    }

    private async Task<ActionResult> GetStatsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var startDate = parameters.GetString("startDate")!;
        var endDate = parameters.GetString("endDate") ?? startDate;
        var aggregatedBy = parameters.GetString("aggregatedBy") ?? "day";

        return await GetAsync($"stats?start_date={startDate}&end_date={endDate}&aggregated_by={aggregatedBy}", ct);
    }

    private async Task<ActionResult> GetCategoryStatsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var categories = parameters.Get<JsonElement>("categories");
        var startDate = parameters.GetString("startDate")!;
        var endDate = parameters.GetString("endDate") ?? startDate;

        var categoryList = categories.EnumerateArray()
            .Select(c => $"categories={Uri.EscapeDataString(c.GetString()!)}")
            .ToList();

        var query = string.Join("&", categoryList) + $"&start_date={startDate}&end_date={endDate}";
        return await GetAsync($"categories/stats?{query}", ct);
    }

    private async Task<ActionResult> ValidateEmailAsync(ActionParameters parameters, CancellationToken ct)
    {
        var email = parameters.GetString("email")!;
        var source = parameters.GetString("source");

        var payload = new Dictionary<string, object> { ["email"] = email };
        if (!string.IsNullOrEmpty(source))
        {
            payload["source"] = source;
        }

        return await PostAsync("validations/email", payload, ct);
    }

    private static object BuildEmailAddress(string email, string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return new { email };
        }
        return new { email, name };
    }

    private async Task<ActionResult> GetAsync(string endpoint, CancellationToken ct)
    {
        var response = await _httpClient!.GetAsync(endpoint, ct);
        var result = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail($"API error: {result}", "API_ERROR");
        }

        if (string.IsNullOrEmpty(result))
        {
            return ActionResult.Ok(new { success = true });
        }

        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> PostAsync(string endpoint, object payload, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync(endpoint, content, ct);
        var result = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail($"API error: {result}", "API_ERROR");
        }

        // mail/send returns 202 with empty body on success
        if (response.StatusCode == System.Net.HttpStatusCode.Accepted || string.IsNullOrEmpty(result))
        {
            return ActionResult.Ok(new { success = true, statusCode = (int)response.StatusCode });
        }

        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> PutAsync(string endpoint, object payload, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient!.PutAsync(endpoint, content, ct);
        var result = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail($"API error: {result}", "API_ERROR");
        }

        if (string.IsNullOrEmpty(result))
        {
            return ActionResult.Ok(new { success = true });
        }

        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
