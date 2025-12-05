using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// HubSpot connector for CRM, marketing, sales, and service operations.
/// Uses HubSpot API v3.
/// </summary>
public sealed class HubSpotConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public override string Id => "hubspot";
    public override string Name => "HubSpot";
    public override string Description => "CRM platform for marketing, sales, and customer service";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Api;
    public override string IconUrl => "https://www.hubspot.com/favicon.ico";

    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.ForApi();

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.ApiKey,
        RequiredCredentials = new CredentialField[]
        {
            new() { Name = "accessToken", Label = "Private App Access Token", Type = ParameterType.Password, Description = "Access token from HubSpot Private App" }
        }
    };

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // Contacts
        new()
        {
            Id = "createContact",
            Name = "Create Contact",
            Description = "Create a new contact",
            Parameters = new ActionParameter[]
            {
                new() { Name = "email", Type = ParameterType.String, Required = true },
                new() { Name = "firstName", Type = ParameterType.String },
                new() { Name = "lastName", Type = ParameterType.String },
                new() { Name = "phone", Type = ParameterType.String },
                new() { Name = "company", Type = ParameterType.String },
                new() { Name = "website", Type = ParameterType.String },
                new() { Name = "lifecycleStage", Type = ParameterType.String, Description = "subscriber, lead, marketingqualifiedlead, salesqualifiedlead, opportunity, customer, evangelist" },
                new() { Name = "properties", Type = ParameterType.Json, Description = "Additional properties" }
            }
        },
        new()
        {
            Id = "getContact",
            Name = "Get Contact",
            Description = "Get a contact by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "contactId", Type = ParameterType.String, Required = true },
                new() { Name = "properties", Type = ParameterType.String, Description = "Comma-separated property names" }
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
                new() { Name = "properties", Type = ParameterType.Json, Required = true }
            }
        },
        new()
        {
            Id = "deleteContact",
            Name = "Delete Contact",
            Description = "Delete a contact",
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
            Description = "Search contacts with filters",
            Parameters = new ActionParameter[]
            {
                new() { Name = "query", Type = ParameterType.String, Description = "Search query" },
                new() { Name = "filters", Type = ParameterType.Json, Description = "Filter groups array" },
                new() { Name = "properties", Type = ParameterType.String, Description = "Properties to return" },
                new() { Name = "limit", Type = ParameterType.Number, DefaultValue = 10 }
            }
        },

        // Companies
        new()
        {
            Id = "createCompany",
            Name = "Create Company",
            Description = "Create a new company",
            Parameters = new ActionParameter[]
            {
                new() { Name = "name", Type = ParameterType.String, Required = true },
                new() { Name = "domain", Type = ParameterType.String },
                new() { Name = "industry", Type = ParameterType.String },
                new() { Name = "phone", Type = ParameterType.String },
                new() { Name = "city", Type = ParameterType.String },
                new() { Name = "country", Type = ParameterType.String },
                new() { Name = "properties", Type = ParameterType.Json }
            }
        },
        new()
        {
            Id = "getCompany",
            Name = "Get Company",
            Description = "Get a company by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "companyId", Type = ParameterType.String, Required = true },
                new() { Name = "properties", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "updateCompany",
            Name = "Update Company",
            Description = "Update an existing company",
            Parameters = new ActionParameter[]
            {
                new() { Name = "companyId", Type = ParameterType.String, Required = true },
                new() { Name = "properties", Type = ParameterType.Json, Required = true }
            }
        },

        // Deals
        new()
        {
            Id = "createDeal",
            Name = "Create Deal",
            Description = "Create a new deal",
            Parameters = new ActionParameter[]
            {
                new() { Name = "dealName", Type = ParameterType.String, Required = true },
                new() { Name = "pipeline", Type = ParameterType.String, Required = true },
                new() { Name = "dealStage", Type = ParameterType.String, Required = true },
                new() { Name = "amount", Type = ParameterType.Number },
                new() { Name = "closeDate", Type = ParameterType.Date },
                new() { Name = "dealType", Type = ParameterType.String },
                new() { Name = "properties", Type = ParameterType.Json }
            }
        },
        new()
        {
            Id = "getDeal",
            Name = "Get Deal",
            Description = "Get a deal by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "dealId", Type = ParameterType.String, Required = true },
                new() { Name = "properties", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "updateDeal",
            Name = "Update Deal",
            Description = "Update an existing deal",
            Parameters = new ActionParameter[]
            {
                new() { Name = "dealId", Type = ParameterType.String, Required = true },
                new() { Name = "properties", Type = ParameterType.Json, Required = true }
            }
        },

        // Tickets
        new()
        {
            Id = "createTicket",
            Name = "Create Ticket",
            Description = "Create a new support ticket",
            Parameters = new ActionParameter[]
            {
                new() { Name = "subject", Type = ParameterType.String, Required = true },
                new() { Name = "content", Type = ParameterType.String },
                new() { Name = "pipeline", Type = ParameterType.String, Required = true },
                new() { Name = "ticketStatus", Type = ParameterType.String, Required = true },
                new() { Name = "priority", Type = ParameterType.String },
                new() { Name = "properties", Type = ParameterType.Json }
            }
        },
        new()
        {
            Id = "getTicket",
            Name = "Get Ticket",
            Description = "Get a ticket by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "ticketId", Type = ParameterType.String, Required = true },
                new() { Name = "properties", Type = ParameterType.String }
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
                new() { Name = "properties", Type = ParameterType.Json, Required = true }
            }
        },

        // Engagements
        new()
        {
            Id = "createNote",
            Name = "Create Note",
            Description = "Create a note engagement",
            Parameters = new ActionParameter[]
            {
                new() { Name = "body", Type = ParameterType.String, Required = true },
                new() { Name = "contactIds", Type = ParameterType.String, Description = "Comma-separated contact IDs" },
                new() { Name = "companyIds", Type = ParameterType.String },
                new() { Name = "dealIds", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "createTask",
            Name = "Create Task",
            Description = "Create a task engagement",
            Parameters = new ActionParameter[]
            {
                new() { Name = "subject", Type = ParameterType.String, Required = true },
                new() { Name = "body", Type = ParameterType.String },
                new() { Name = "status", Type = ParameterType.String, DefaultValue = "NOT_STARTED" },
                new() { Name = "priority", Type = ParameterType.String, DefaultValue = "MEDIUM" },
                new() { Name = "dueDate", Type = ParameterType.DateTime },
                new() { Name = "contactIds", Type = ParameterType.String },
                new() { Name = "companyIds", Type = ParameterType.String },
                new() { Name = "dealIds", Type = ParameterType.String }
            }
        },

        // Lists
        new()
        {
            Id = "addContactToList",
            Name = "Add Contact to List",
            Description = "Add a contact to a static list",
            Parameters = new ActionParameter[]
            {
                new() { Name = "listId", Type = ParameterType.String, Required = true },
                new() { Name = "contactId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "removeContactFromList",
            Name = "Remove Contact from List",
            Description = "Remove a contact from a static list",
            Parameters = new ActionParameter[]
            {
                new() { Name = "listId", Type = ParameterType.String, Required = true },
                new() { Name = "contactId", Type = ParameterType.String, Required = true }
            }
        },

        // Associations
        new()
        {
            Id = "createAssociation",
            Name = "Create Association",
            Description = "Create an association between objects",
            Parameters = new ActionParameter[]
            {
                new() { Name = "fromObjectType", Type = ParameterType.String, Required = true },
                new() { Name = "fromObjectId", Type = ParameterType.String, Required = true },
                new() { Name = "toObjectType", Type = ParameterType.String, Required = true },
                new() { Name = "toObjectId", Type = ParameterType.String, Required = true },
                new() { Name = "associationType", Type = ParameterType.String, Required = true }
            }
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "contactCreated",
            Name = "Contact Created",
            Description = "Triggered when a new contact is created",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "dealStageChanged",
            Name = "Deal Stage Changed",
            Description = "Triggered when a deal stage changes",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "formSubmitted",
            Name = "Form Submitted",
            Description = "Triggered when a form is submitted",
            Type = TriggerType.Webhook
        }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        await base.InitializeAsync(config, ct);

        var accessToken = config.GetCredentialString("accessToken");

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.hubapi.com/")
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
            "createContact" => await CreateContactAsync(parameters, ct),
            "getContact" => await GetObjectAsync("contacts", parameters.GetString("contactId")!, parameters.GetString("properties"), ct),
            "updateContact" => await UpdateObjectAsync("contacts", parameters.GetString("contactId")!, parameters, ct),
            "deleteContact" => await DeleteAsync($"crm/v3/objects/contacts/{parameters.GetString("contactId")}", ct),
            "searchContacts" => await SearchObjectsAsync("contacts", parameters, ct),

            "createCompany" => await CreateCompanyAsync(parameters, ct),
            "getCompany" => await GetObjectAsync("companies", parameters.GetString("companyId")!, parameters.GetString("properties"), ct),
            "updateCompany" => await UpdateObjectAsync("companies", parameters.GetString("companyId")!, parameters, ct),

            "createDeal" => await CreateDealAsync(parameters, ct),
            "getDeal" => await GetObjectAsync("deals", parameters.GetString("dealId")!, parameters.GetString("properties"), ct),
            "updateDeal" => await UpdateObjectAsync("deals", parameters.GetString("dealId")!, parameters, ct),

            "createTicket" => await CreateTicketAsync(parameters, ct),
            "getTicket" => await GetObjectAsync("tickets", parameters.GetString("ticketId")!, parameters.GetString("properties"), ct),
            "updateTicket" => await UpdateObjectAsync("tickets", parameters.GetString("ticketId")!, parameters, ct),

            "createNote" => await CreateNoteAsync(parameters, ct),
            "createTask" => await CreateTaskAsync(parameters, ct),

            "addContactToList" => await AddToListAsync(parameters, ct),
            "removeContactFromList" => await RemoveFromListAsync(parameters, ct),

            "createAssociation" => await CreateAssociationAsync(parameters, ct),

            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> CreateContactAsync(ActionParameters parameters, CancellationToken ct)
    {
        var properties = new Dictionary<string, object>
        {
            ["email"] = parameters.GetString("email")!
        };

        var firstName = parameters.GetString("firstName");
        if (!string.IsNullOrEmpty(firstName)) properties["firstname"] = firstName;

        var lastName = parameters.GetString("lastName");
        if (!string.IsNullOrEmpty(lastName)) properties["lastname"] = lastName;

        var phone = parameters.GetString("phone");
        if (!string.IsNullOrEmpty(phone)) properties["phone"] = phone;

        var company = parameters.GetString("company");
        if (!string.IsNullOrEmpty(company)) properties["company"] = company;

        var website = parameters.GetString("website");
        if (!string.IsNullOrEmpty(website)) properties["website"] = website;

        var lifecycleStage = parameters.GetString("lifecycleStage");
        if (!string.IsNullOrEmpty(lifecycleStage)) properties["lifecyclestage"] = lifecycleStage;

        var extraProps = parameters.Get<JsonElement?>("properties");
        if (extraProps.HasValue && extraProps.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in extraProps.Value.EnumerateObject())
            {
                properties[prop.Name] = prop.Value.ToString();
            }
        }

        return await PostAsync("crm/v3/objects/contacts", new { properties }, ct);
    }

    private async Task<ActionResult> CreateCompanyAsync(ActionParameters parameters, CancellationToken ct)
    {
        var properties = new Dictionary<string, object>
        {
            ["name"] = parameters.GetString("name")!
        };

        var domain = parameters.GetString("domain");
        if (!string.IsNullOrEmpty(domain)) properties["domain"] = domain;

        var industry = parameters.GetString("industry");
        if (!string.IsNullOrEmpty(industry)) properties["industry"] = industry;

        var phone = parameters.GetString("phone");
        if (!string.IsNullOrEmpty(phone)) properties["phone"] = phone;

        var city = parameters.GetString("city");
        if (!string.IsNullOrEmpty(city)) properties["city"] = city;

        var country = parameters.GetString("country");
        if (!string.IsNullOrEmpty(country)) properties["country"] = country;

        return await PostAsync("crm/v3/objects/companies", new { properties }, ct);
    }

    private async Task<ActionResult> CreateDealAsync(ActionParameters parameters, CancellationToken ct)
    {
        var properties = new Dictionary<string, object>
        {
            ["dealname"] = parameters.GetString("dealName")!,
            ["pipeline"] = parameters.GetString("pipeline")!,
            ["dealstage"] = parameters.GetString("dealStage")!
        };

        var amount = parameters.GetInt("amount");
        if (amount > 0) properties["amount"] = amount;

        var closeDate = parameters.GetString("closeDate");
        if (!string.IsNullOrEmpty(closeDate)) properties["closedate"] = closeDate;

        var dealType = parameters.GetString("dealType");
        if (!string.IsNullOrEmpty(dealType)) properties["dealtype"] = dealType;

        return await PostAsync("crm/v3/objects/deals", new { properties }, ct);
    }

    private async Task<ActionResult> CreateTicketAsync(ActionParameters parameters, CancellationToken ct)
    {
        var properties = new Dictionary<string, object>
        {
            ["subject"] = parameters.GetString("subject")!,
            ["hs_pipeline"] = parameters.GetString("pipeline")!,
            ["hs_pipeline_stage"] = parameters.GetString("ticketStatus")!
        };

        var content = parameters.GetString("content");
        if (!string.IsNullOrEmpty(content)) properties["content"] = content;

        var priority = parameters.GetString("priority");
        if (!string.IsNullOrEmpty(priority)) properties["hs_ticket_priority"] = priority;

        return await PostAsync("crm/v3/objects/tickets", new { properties }, ct);
    }

    private async Task<ActionResult> CreateNoteAsync(ActionParameters parameters, CancellationToken ct)
    {
        var properties = new Dictionary<string, object>
        {
            ["hs_note_body"] = parameters.GetString("body")!,
            ["hs_timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
        };

        var associations = BuildAssociations(parameters);
        var payload = associations.Count > 0
            ? new { properties, associations }
            : (object)new { properties };

        return await PostAsync("crm/v3/objects/notes", payload, ct);
    }

    private async Task<ActionResult> CreateTaskAsync(ActionParameters parameters, CancellationToken ct)
    {
        var properties = new Dictionary<string, object>
        {
            ["hs_task_subject"] = parameters.GetString("subject")!,
            ["hs_task_status"] = parameters.GetString("status") ?? "NOT_STARTED",
            ["hs_task_priority"] = parameters.GetString("priority") ?? "MEDIUM"
        };

        var body = parameters.GetString("body");
        if (!string.IsNullOrEmpty(body)) properties["hs_task_body"] = body;

        var dueDate = parameters.GetString("dueDate");
        if (!string.IsNullOrEmpty(dueDate)) properties["hs_timestamp"] = dueDate;

        var associations = BuildAssociations(parameters);
        var payload = associations.Count > 0
            ? new { properties, associations }
            : (object)new { properties };

        return await PostAsync("crm/v3/objects/tasks", payload, ct);
    }

    private static List<object> BuildAssociations(ActionParameters parameters)
    {
        var associations = new List<object>();

        var contactIds = parameters.GetString("contactIds");
        if (!string.IsNullOrEmpty(contactIds))
        {
            foreach (var id in contactIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                associations.Add(new { to = new { id = id.Trim() }, types = new[] { new { associationCategory = "HUBSPOT_DEFINED", associationTypeId = 202 } } });
            }
        }

        var companyIds = parameters.GetString("companyIds");
        if (!string.IsNullOrEmpty(companyIds))
        {
            foreach (var id in companyIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                associations.Add(new { to = new { id = id.Trim() }, types = new[] { new { associationCategory = "HUBSPOT_DEFINED", associationTypeId = 190 } } });
            }
        }

        var dealIds = parameters.GetString("dealIds");
        if (!string.IsNullOrEmpty(dealIds))
        {
            foreach (var id in dealIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                associations.Add(new { to = new { id = id.Trim() }, types = new[] { new { associationCategory = "HUBSPOT_DEFINED", associationTypeId = 214 } } });
            }
        }

        return associations;
    }

    private async Task<ActionResult> GetObjectAsync(string objectType, string objectId, string? properties, CancellationToken ct)
    {
        var endpoint = $"crm/v3/objects/{objectType}/{objectId}";
        if (!string.IsNullOrEmpty(properties))
            endpoint += $"?properties={Uri.EscapeDataString(properties)}";
        return await GetAsync(endpoint, ct);
    }

    private async Task<ActionResult> UpdateObjectAsync(string objectType, string objectId, ActionParameters parameters, CancellationToken ct)
    {
        var properties = parameters.Get<JsonElement>("properties");
        return await PatchAsync($"crm/v3/objects/{objectType}/{objectId}", new { properties }, ct);
    }

    private async Task<ActionResult> SearchObjectsAsync(string objectType, ActionParameters parameters, CancellationToken ct)
    {
        var payload = new Dictionary<string, object>();

        var query = parameters.GetString("query");
        if (!string.IsNullOrEmpty(query)) payload["query"] = query;

        var filters = parameters.Get<JsonElement?>("filters");
        if (filters.HasValue && filters.Value.ValueKind != JsonValueKind.Undefined)
            payload["filterGroups"] = filters.Value;

        var properties = parameters.GetString("properties");
        if (!string.IsNullOrEmpty(properties))
            payload["properties"] = properties.Split(',').Select(p => p.Trim()).ToArray();

        payload["limit"] = parameters.GetInt("limit", 10);

        return await PostAsync($"crm/v3/objects/{objectType}/search", payload, ct);
    }

    private async Task<ActionResult> AddToListAsync(ActionParameters parameters, CancellationToken ct)
    {
        var listId = parameters.GetString("listId")!;
        var contactId = parameters.GetString("contactId")!;
        return await PutAsync($"contacts/v1/lists/{listId}/add", new { vids = new[] { long.Parse(contactId) } }, ct);
    }

    private async Task<ActionResult> RemoveFromListAsync(ActionParameters parameters, CancellationToken ct)
    {
        var listId = parameters.GetString("listId")!;
        var contactId = parameters.GetString("contactId")!;
        return await PostAsync($"contacts/v1/lists/{listId}/remove", new { vids = new[] { long.Parse(contactId) } }, ct);
    }

    private async Task<ActionResult> CreateAssociationAsync(ActionParameters parameters, CancellationToken ct)
    {
        var fromType = parameters.GetString("fromObjectType")!;
        var fromId = parameters.GetString("fromObjectId")!;
        var toType = parameters.GetString("toObjectType")!;
        var toId = parameters.GetString("toObjectId")!;
        var assocType = parameters.GetString("associationType")!;

        return await PutAsync($"crm/v4/objects/{fromType}/{fromId}/associations/{toType}/{toId}",
            new[] { new { associationCategory = "HUBSPOT_DEFINED", associationTypeId = int.Parse(assocType) } }, ct);
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

        return ActionResult.Fail($"HubSpot error ({response.StatusCode}): {content}");
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
