using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Salesforce CRM connector for managing leads, contacts, accounts, and opportunities.
/// Uses Salesforce REST API.
/// </summary>
public sealed class SalesforceConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private string? _instanceUrl;
    private const string ApiVersion = "v59.0";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public override string Id => "salesforce";
    public override string Name => "Salesforce";
    public override string Description => "World's #1 CRM platform for sales, service, and marketing";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Api;
    public override string IconUrl => "https://www.salesforce.com/favicon.ico";

    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.ForApi();

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.OAuth2,
        RequiredCredentials = new CredentialField[]
        {
            new() { Name = "instanceUrl", Label = "Instance URL", Type = ParameterType.String, Description = "Your Salesforce instance URL (e.g., https://yourorg.salesforce.com)" },
            new() { Name = "accessToken", Label = "Access Token", Type = ParameterType.Password }
        }
    };

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // Leads
        new()
        {
            Id = "createLead",
            Name = "Create Lead",
            Description = "Create a new lead",
            Parameters = new ActionParameter[]
            {
                new() { Name = "firstName", Type = ParameterType.String },
                new() { Name = "lastName", Type = ParameterType.String, Required = true },
                new() { Name = "company", Type = ParameterType.String, Required = true },
                new() { Name = "email", Type = ParameterType.String },
                new() { Name = "phone", Type = ParameterType.String },
                new() { Name = "title", Type = ParameterType.String },
                new() { Name = "status", Type = ParameterType.String, DefaultValue = "Open - Not Contacted" },
                new() { Name = "leadSource", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "getLead",
            Name = "Get Lead",
            Description = "Get a lead by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "leadId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "updateLead",
            Name = "Update Lead",
            Description = "Update an existing lead",
            Parameters = new ActionParameter[]
            {
                new() { Name = "leadId", Type = ParameterType.String, Required = true },
                new() { Name = "fields", Type = ParameterType.Json, Required = true, Description = "Fields to update" }
            }
        },
        new()
        {
            Id = "deleteLead",
            Name = "Delete Lead",
            Description = "Delete a lead",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "leadId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "convertLead",
            Name = "Convert Lead",
            Description = "Convert a lead to contact/account/opportunity",
            Parameters = new ActionParameter[]
            {
                new() { Name = "leadId", Type = ParameterType.String, Required = true },
                new() { Name = "accountId", Type = ParameterType.String, Description = "Existing account ID (optional)" },
                new() { Name = "contactId", Type = ParameterType.String, Description = "Existing contact ID (optional)" },
                new() { Name = "createOpportunity", Type = ParameterType.Boolean, DefaultValue = true },
                new() { Name = "opportunityName", Type = ParameterType.String }
            }
        },

        // Contacts
        new()
        {
            Id = "createContact",
            Name = "Create Contact",
            Description = "Create a new contact",
            Parameters = new ActionParameter[]
            {
                new() { Name = "firstName", Type = ParameterType.String },
                new() { Name = "lastName", Type = ParameterType.String, Required = true },
                new() { Name = "accountId", Type = ParameterType.String },
                new() { Name = "email", Type = ParameterType.String },
                new() { Name = "phone", Type = ParameterType.String },
                new() { Name = "title", Type = ParameterType.String },
                new() { Name = "department", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "getContact",
            Name = "Get Contact",
            Description = "Get a contact by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "contactId", Type = ParameterType.String, Required = true }
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
                new() { Name = "fields", Type = ParameterType.Json, Required = true }
            }
        },

        // Accounts
        new()
        {
            Id = "createAccount",
            Name = "Create Account",
            Description = "Create a new account",
            Parameters = new ActionParameter[]
            {
                new() { Name = "name", Type = ParameterType.String, Required = true },
                new() { Name = "type", Type = ParameterType.String, Description = "Prospect, Customer, Partner, etc." },
                new() { Name = "industry", Type = ParameterType.String },
                new() { Name = "phone", Type = ParameterType.String },
                new() { Name = "website", Type = ParameterType.String },
                new() { Name = "description", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "getAccount",
            Name = "Get Account",
            Description = "Get an account by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "accountId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "updateAccount",
            Name = "Update Account",
            Description = "Update an existing account",
            Parameters = new ActionParameter[]
            {
                new() { Name = "accountId", Type = ParameterType.String, Required = true },
                new() { Name = "fields", Type = ParameterType.Json, Required = true }
            }
        },

        // Opportunities
        new()
        {
            Id = "createOpportunity",
            Name = "Create Opportunity",
            Description = "Create a new opportunity",
            Parameters = new ActionParameter[]
            {
                new() { Name = "name", Type = ParameterType.String, Required = true },
                new() { Name = "accountId", Type = ParameterType.String },
                new() { Name = "stageName", Type = ParameterType.String, Required = true },
                new() { Name = "closeDate", Type = ParameterType.Date, Required = true },
                new() { Name = "amount", Type = ParameterType.Number },
                new() { Name = "probability", Type = ParameterType.Number },
                new() { Name = "type", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "getOpportunity",
            Name = "Get Opportunity",
            Description = "Get an opportunity by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "opportunityId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "updateOpportunity",
            Name = "Update Opportunity",
            Description = "Update an existing opportunity",
            Parameters = new ActionParameter[]
            {
                new() { Name = "opportunityId", Type = ParameterType.String, Required = true },
                new() { Name = "fields", Type = ParameterType.Json, Required = true }
            }
        },

        // Cases
        new()
        {
            Id = "createCase",
            Name = "Create Case",
            Description = "Create a new support case",
            Parameters = new ActionParameter[]
            {
                new() { Name = "subject", Type = ParameterType.String, Required = true },
                new() { Name = "description", Type = ParameterType.String },
                new() { Name = "contactId", Type = ParameterType.String },
                new() { Name = "accountId", Type = ParameterType.String },
                new() { Name = "status", Type = ParameterType.String, DefaultValue = "New" },
                new() { Name = "priority", Type = ParameterType.String, DefaultValue = "Medium" },
                new() { Name = "origin", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "getCase",
            Name = "Get Case",
            Description = "Get a case by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "caseId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "updateCase",
            Name = "Update Case",
            Description = "Update an existing case",
            Parameters = new ActionParameter[]
            {
                new() { Name = "caseId", Type = ParameterType.String, Required = true },
                new() { Name = "fields", Type = ParameterType.Json, Required = true }
            }
        },

        // Query
        new()
        {
            Id = "query",
            Name = "SOQL Query",
            Description = "Execute a SOQL query",
            Parameters = new ActionParameter[]
            {
                new() { Name = "query", Type = ParameterType.String, Required = true, Description = "SOQL query string" }
            }
        },
        new()
        {
            Id = "search",
            Name = "SOSL Search",
            Description = "Execute a SOSL search",
            Parameters = new ActionParameter[]
            {
                new() { Name = "search", Type = ParameterType.String, Required = true, Description = "SOSL search string" }
            }
        },

        // Generic
        new()
        {
            Id = "createRecord",
            Name = "Create Record",
            Description = "Create a record of any object type",
            Parameters = new ActionParameter[]
            {
                new() { Name = "objectType", Type = ParameterType.String, Required = true, Description = "Salesforce object API name" },
                new() { Name = "fields", Type = ParameterType.Json, Required = true }
            }
        },
        new()
        {
            Id = "getRecord",
            Name = "Get Record",
            Description = "Get a record by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "objectType", Type = ParameterType.String, Required = true },
                new() { Name = "recordId", Type = ParameterType.String, Required = true },
                new() { Name = "fields", Type = ParameterType.String, Description = "Comma-separated field names" }
            }
        },
        new()
        {
            Id = "updateRecord",
            Name = "Update Record",
            Description = "Update a record",
            Parameters = new ActionParameter[]
            {
                new() { Name = "objectType", Type = ParameterType.String, Required = true },
                new() { Name = "recordId", Type = ParameterType.String, Required = true },
                new() { Name = "fields", Type = ParameterType.Json, Required = true }
            }
        },
        new()
        {
            Id = "deleteRecord",
            Name = "Delete Record",
            Description = "Delete a record",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "objectType", Type = ParameterType.String, Required = true },
                new() { Name = "recordId", Type = ParameterType.String, Required = true }
            }
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "leadCreated",
            Name = "Lead Created",
            Description = "Triggered when a new lead is created",
            Type = TriggerType.Polling
        },
        new()
        {
            Id = "opportunityStageChanged",
            Name = "Opportunity Stage Changed",
            Description = "Triggered when an opportunity stage changes",
            Type = TriggerType.Polling
        },
        new()
        {
            Id = "caseCreated",
            Name = "Case Created",
            Description = "Triggered when a new case is created",
            Type = TriggerType.Polling
        }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        await base.InitializeAsync(config, ct);

        _instanceUrl = config.GetCredentialString("instanceUrl")!.TrimEnd('/');
        var accessToken = config.GetCredentialString("accessToken");

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"{_instanceUrl}/services/data/{ApiVersion}/")
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
            "createLead" => await CreateLeadAsync(parameters, ct),
            "getLead" => await GetRecordAsync("Lead", parameters.GetString("leadId")!, null, ct),
            "updateLead" => await UpdateRecordAsync("Lead", parameters.GetString("leadId")!, parameters, ct),
            "deleteLead" => await DeleteRecordAsync("Lead", parameters.GetString("leadId")!, ct),
            "convertLead" => await ConvertLeadAsync(parameters, ct),

            "createContact" => await CreateContactAsync(parameters, ct),
            "getContact" => await GetRecordAsync("Contact", parameters.GetString("contactId")!, null, ct),
            "updateContact" => await UpdateRecordAsync("Contact", parameters.GetString("contactId")!, parameters, ct),

            "createAccount" => await CreateAccountAsync(parameters, ct),
            "getAccount" => await GetRecordAsync("Account", parameters.GetString("accountId")!, null, ct),
            "updateAccount" => await UpdateRecordAsync("Account", parameters.GetString("accountId")!, parameters, ct),

            "createOpportunity" => await CreateOpportunityAsync(parameters, ct),
            "getOpportunity" => await GetRecordAsync("Opportunity", parameters.GetString("opportunityId")!, null, ct),
            "updateOpportunity" => await UpdateRecordAsync("Opportunity", parameters.GetString("opportunityId")!, parameters, ct),

            "createCase" => await CreateCaseAsync(parameters, ct),
            "getCase" => await GetRecordAsync("Case", parameters.GetString("caseId")!, null, ct),
            "updateCase" => await UpdateRecordAsync("Case", parameters.GetString("caseId")!, parameters, ct),

            "query" => await QueryAsync(parameters.GetString("query")!, ct),
            "search" => await SearchAsync(parameters.GetString("search")!, ct),

            "createRecord" => await CreateGenericRecordAsync(parameters, ct),
            "getRecord" => await GetRecordAsync(parameters.GetString("objectType")!, parameters.GetString("recordId")!, parameters.GetString("fields"), ct),
            "updateRecord" => await UpdateRecordAsync(parameters.GetString("objectType")!, parameters.GetString("recordId")!, parameters, ct),
            "deleteRecord" => await DeleteRecordAsync(parameters.GetString("objectType")!, parameters.GetString("recordId")!, ct),

            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> CreateLeadAsync(ActionParameters parameters, CancellationToken ct)
    {
        var lead = new Dictionary<string, object>
        {
            ["LastName"] = parameters.GetString("lastName")!,
            ["Company"] = parameters.GetString("company")!
        };

        var firstName = parameters.GetString("firstName");
        if (!string.IsNullOrEmpty(firstName)) lead["FirstName"] = firstName;

        var email = parameters.GetString("email");
        if (!string.IsNullOrEmpty(email)) lead["Email"] = email;

        var phone = parameters.GetString("phone");
        if (!string.IsNullOrEmpty(phone)) lead["Phone"] = phone;

        var title = parameters.GetString("title");
        if (!string.IsNullOrEmpty(title)) lead["Title"] = title;

        var status = parameters.GetString("status");
        if (!string.IsNullOrEmpty(status)) lead["Status"] = status;

        var leadSource = parameters.GetString("leadSource");
        if (!string.IsNullOrEmpty(leadSource)) lead["LeadSource"] = leadSource;

        return await PostAsync("sobjects/Lead", lead, ct);
    }

    private async Task<ActionResult> CreateContactAsync(ActionParameters parameters, CancellationToken ct)
    {
        var contact = new Dictionary<string, object>
        {
            ["LastName"] = parameters.GetString("lastName")!
        };

        var firstName = parameters.GetString("firstName");
        if (!string.IsNullOrEmpty(firstName)) contact["FirstName"] = firstName;

        var accountId = parameters.GetString("accountId");
        if (!string.IsNullOrEmpty(accountId)) contact["AccountId"] = accountId;

        var email = parameters.GetString("email");
        if (!string.IsNullOrEmpty(email)) contact["Email"] = email;

        var phone = parameters.GetString("phone");
        if (!string.IsNullOrEmpty(phone)) contact["Phone"] = phone;

        var title = parameters.GetString("title");
        if (!string.IsNullOrEmpty(title)) contact["Title"] = title;

        var department = parameters.GetString("department");
        if (!string.IsNullOrEmpty(department)) contact["Department"] = department;

        return await PostAsync("sobjects/Contact", contact, ct);
    }

    private async Task<ActionResult> CreateAccountAsync(ActionParameters parameters, CancellationToken ct)
    {
        var account = new Dictionary<string, object>
        {
            ["Name"] = parameters.GetString("name")!
        };

        var type = parameters.GetString("type");
        if (!string.IsNullOrEmpty(type)) account["Type"] = type;

        var industry = parameters.GetString("industry");
        if (!string.IsNullOrEmpty(industry)) account["Industry"] = industry;

        var phone = parameters.GetString("phone");
        if (!string.IsNullOrEmpty(phone)) account["Phone"] = phone;

        var website = parameters.GetString("website");
        if (!string.IsNullOrEmpty(website)) account["Website"] = website;

        var description = parameters.GetString("description");
        if (!string.IsNullOrEmpty(description)) account["Description"] = description;

        return await PostAsync("sobjects/Account", account, ct);
    }

    private async Task<ActionResult> CreateOpportunityAsync(ActionParameters parameters, CancellationToken ct)
    {
        var opp = new Dictionary<string, object>
        {
            ["Name"] = parameters.GetString("name")!,
            ["StageName"] = parameters.GetString("stageName")!,
            ["CloseDate"] = parameters.GetString("closeDate")!
        };

        var accountId = parameters.GetString("accountId");
        if (!string.IsNullOrEmpty(accountId)) opp["AccountId"] = accountId;

        var amount = parameters.GetInt("amount");
        if (amount > 0) opp["Amount"] = amount;

        var probability = parameters.GetInt("probability");
        if (probability > 0) opp["Probability"] = probability;

        var type = parameters.GetString("type");
        if (!string.IsNullOrEmpty(type)) opp["Type"] = type;

        return await PostAsync("sobjects/Opportunity", opp, ct);
    }

    private async Task<ActionResult> CreateCaseAsync(ActionParameters parameters, CancellationToken ct)
    {
        var caseObj = new Dictionary<string, object>
        {
            ["Subject"] = parameters.GetString("subject")!
        };

        var description = parameters.GetString("description");
        if (!string.IsNullOrEmpty(description)) caseObj["Description"] = description;

        var contactId = parameters.GetString("contactId");
        if (!string.IsNullOrEmpty(contactId)) caseObj["ContactId"] = contactId;

        var accountId = parameters.GetString("accountId");
        if (!string.IsNullOrEmpty(accountId)) caseObj["AccountId"] = accountId;

        var status = parameters.GetString("status");
        if (!string.IsNullOrEmpty(status)) caseObj["Status"] = status;

        var priority = parameters.GetString("priority");
        if (!string.IsNullOrEmpty(priority)) caseObj["Priority"] = priority;

        var origin = parameters.GetString("origin");
        if (!string.IsNullOrEmpty(origin)) caseObj["Origin"] = origin;

        return await PostAsync("sobjects/Case", caseObj, ct);
    }

    private async Task<ActionResult> ConvertLeadAsync(ActionParameters parameters, CancellationToken ct)
    {
        var leadConvert = new Dictionary<string, object>
        {
            ["leadId"] = parameters.GetString("leadId")!,
            ["convertedStatus"] = "Closed - Converted"
        };

        var accountId = parameters.GetString("accountId");
        if (!string.IsNullOrEmpty(accountId)) leadConvert["accountId"] = accountId;

        var contactId = parameters.GetString("contactId");
        if (!string.IsNullOrEmpty(contactId)) leadConvert["contactId"] = contactId;

        if (!parameters.GetBool("createOpportunity", true))
            leadConvert["doNotCreateOpportunity"] = true;

        var oppName = parameters.GetString("opportunityName");
        if (!string.IsNullOrEmpty(oppName)) leadConvert["opportunityName"] = oppName;

        var payload = new { leadConverts = new[] { leadConvert } };
        return await PostAsync("actions/standard/convertLead", payload, ct);
    }

    private async Task<ActionResult> CreateGenericRecordAsync(ActionParameters parameters, CancellationToken ct)
    {
        var objectType = parameters.GetString("objectType")!;
        var fields = parameters.Get<JsonElement>("fields");
        return await PostAsync($"sobjects/{objectType}", fields, ct);
    }

    private async Task<ActionResult> GetRecordAsync(string objectType, string recordId, string? fields, CancellationToken ct)
    {
        var endpoint = $"sobjects/{objectType}/{recordId}";
        if (!string.IsNullOrEmpty(fields))
            endpoint += $"?fields={Uri.EscapeDataString(fields)}";
        return await GetAsync(endpoint, ct);
    }

    private async Task<ActionResult> UpdateRecordAsync(string objectType, string recordId, ActionParameters parameters, CancellationToken ct)
    {
        var fields = parameters.Get<JsonElement>("fields");
        return await PatchAsync($"sobjects/{objectType}/{recordId}", fields, ct);
    }

    private async Task<ActionResult> DeleteRecordAsync(string objectType, string recordId, CancellationToken ct)
    {
        return await DeleteAsync($"sobjects/{objectType}/{recordId}", ct);
    }

    private async Task<ActionResult> QueryAsync(string query, CancellationToken ct)
    {
        return await GetAsync($"query?q={Uri.EscapeDataString(query)}", ct);
    }

    private async Task<ActionResult> SearchAsync(string search, CancellationToken ct)
    {
        return await GetAsync($"search?q={Uri.EscapeDataString(search)}", ct);
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

    private async Task<ActionResult> PatchAsync(string endpoint, object payload, CancellationToken ct)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");
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
            if (string.IsNullOrEmpty(content))
            {
                return ActionResult.Ok(new Dictionary<string, object> { ["success"] = true });
            }

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
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var error = doc.RootElement[0];
                var message = error.TryGetProperty("message", out var msg) ? msg.GetString() : content;
                return ActionResult.Fail($"Salesforce error ({response.StatusCode}): {message}");
            }
        }
        catch { }

        return ActionResult.Fail($"Salesforce error ({response.StatusCode}): {content}");
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
