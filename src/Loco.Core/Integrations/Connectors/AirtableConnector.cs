using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Airtable connector for managing bases, tables, and records.
/// Uses Airtable Web API.
/// </summary>
public sealed class AirtableConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public override string Id => "airtable";
    public override string Name => "Airtable";
    public override string Description => "Spreadsheet-database hybrid for organizing and sharing data";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Database;
    public override string IconUrl => "https://airtable.com/favicon.ico";

    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.ForDatabase();

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.ApiKey,
        RequiredCredentials = new CredentialField[]
        {
            new() { Name = "accessToken", Label = "Personal Access Token", Type = ParameterType.Password, Description = "Personal access token from Airtable account" }
        }
    };

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        new()
        {
            Id = "listRecords",
            Name = "List Records",
            Description = "List records from a table",
            Parameters = new ActionParameter[]
            {
                new() { Name = "baseId", Type = ParameterType.String, Required = true },
                new() { Name = "tableId", Type = ParameterType.String, Required = true, Description = "Table ID or Name" },
                new() { Name = "view", Type = ParameterType.String },
                new() { Name = "filterByFormula", Type = ParameterType.String, Description = "Airtable formula to filter records" },
                new() { Name = "maxRecords", Type = ParameterType.Number },
                new() { Name = "pageSize", Type = ParameterType.Number, DefaultValue = 100 },
                new() { Name = "sort", Type = ParameterType.Json, Description = "Array of {field, direction} objects" },
                new() { Name = "fields", Type = ParameterType.String, Description = "Comma-separated field names" },
                new() { Name = "offset", Type = ParameterType.String, Description = "Pagination offset" }
            }
        },
        new()
        {
            Id = "getRecord",
            Name = "Get Record",
            Description = "Get a single record by ID",
            Parameters = new ActionParameter[]
            {
                new() { Name = "baseId", Type = ParameterType.String, Required = true },
                new() { Name = "tableId", Type = ParameterType.String, Required = true },
                new() { Name = "recordId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "createRecord",
            Name = "Create Record",
            Description = "Create a new record in a table",
            Parameters = new ActionParameter[]
            {
                new() { Name = "baseId", Type = ParameterType.String, Required = true },
                new() { Name = "tableId", Type = ParameterType.String, Required = true },
                new() { Name = "fields", Type = ParameterType.Json, Required = true, Description = "Field values as key-value pairs" },
                new() { Name = "typecast", Type = ParameterType.Boolean, DefaultValue = false, Description = "Auto-convert string values" }
            }
        },
        new()
        {
            Id = "createRecords",
            Name = "Create Records (Batch)",
            Description = "Create multiple records in a table (max 10)",
            Parameters = new ActionParameter[]
            {
                new() { Name = "baseId", Type = ParameterType.String, Required = true },
                new() { Name = "tableId", Type = ParameterType.String, Required = true },
                new() { Name = "records", Type = ParameterType.Json, Required = true, Description = "Array of {fields: {...}} objects" },
                new() { Name = "typecast", Type = ParameterType.Boolean, DefaultValue = false }
            }
        },
        new()
        {
            Id = "updateRecord",
            Name = "Update Record",
            Description = "Update an existing record (PATCH - partial update)",
            Parameters = new ActionParameter[]
            {
                new() { Name = "baseId", Type = ParameterType.String, Required = true },
                new() { Name = "tableId", Type = ParameterType.String, Required = true },
                new() { Name = "recordId", Type = ParameterType.String, Required = true },
                new() { Name = "fields", Type = ParameterType.Json, Required = true },
                new() { Name = "typecast", Type = ParameterType.Boolean, DefaultValue = false }
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
                new() { Name = "baseId", Type = ParameterType.String, Required = true },
                new() { Name = "tableId", Type = ParameterType.String, Required = true },
                new() { Name = "recordId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "listBases",
            Name = "List Bases",
            Description = "List all accessible bases",
            Parameters = new ActionParameter[]
            {
                new() { Name = "offset", Type = ParameterType.String }
            }
        },
        new()
        {
            Id = "getBaseSchema",
            Name = "Get Base Schema",
            Description = "Get the schema of a base (tables and fields)",
            Parameters = new ActionParameter[]
            {
                new() { Name = "baseId", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "findRecords",
            Name = "Find Records",
            Description = "Find records by field value",
            Parameters = new ActionParameter[]
            {
                new() { Name = "baseId", Type = ParameterType.String, Required = true },
                new() { Name = "tableId", Type = ParameterType.String, Required = true },
                new() { Name = "fieldName", Type = ParameterType.String, Required = true },
                new() { Name = "value", Type = ParameterType.String, Required = true },
                new() { Name = "maxRecords", Type = ParameterType.Number, DefaultValue = 100 }
            }
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "recordCreated",
            Name = "Record Created",
            Description = "Triggered when a new record is created",
            Type = TriggerType.Polling
        },
        new()
        {
            Id = "recordUpdated",
            Name = "Record Updated",
            Description = "Triggered when a record is updated",
            Type = TriggerType.Polling
        }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        await base.InitializeAsync(config, ct);

        var accessToken = config.GetCredentialString("accessToken");

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.airtable.com/v0/")
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
            "listRecords" => await ListRecordsAsync(parameters, ct),
            "getRecord" => await GetRecordAsync(parameters, ct),
            "createRecord" => await CreateRecordAsync(parameters, ct),
            "createRecords" => await CreateRecordsAsync(parameters, ct),
            "updateRecord" => await UpdateRecordAsync(parameters, ct),
            "deleteRecord" => await DeleteRecordAsync(parameters, ct),
            "listBases" => await ListBasesAsync(parameters, ct),
            "getBaseSchema" => await GetAsync($"meta/bases/{parameters.GetString("baseId")}/tables", ct),
            "findRecords" => await FindRecordsAsync(parameters, ct),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> ListRecordsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var baseId = parameters.GetString("baseId")!;
        var tableId = Uri.EscapeDataString(parameters.GetString("tableId")!);
        var queryParams = new List<string>();

        var view = parameters.GetString("view");
        if (!string.IsNullOrEmpty(view))
            queryParams.Add($"view={Uri.EscapeDataString(view)}");

        var filter = parameters.GetString("filterByFormula");
        if (!string.IsNullOrEmpty(filter))
            queryParams.Add($"filterByFormula={Uri.EscapeDataString(filter)}");

        var maxRecords = parameters.GetInt("maxRecords");
        if (maxRecords > 0)
            queryParams.Add($"maxRecords={maxRecords}");

        var pageSize = parameters.GetInt("pageSize");
        if (pageSize > 0)
            queryParams.Add($"pageSize={pageSize}");

        var fields = parameters.GetString("fields");
        if (!string.IsNullOrEmpty(fields))
        {
            foreach (var field in fields.Split(','))
            {
                queryParams.Add($"fields[]={Uri.EscapeDataString(field.Trim())}");
            }
        }

        var offset = parameters.GetString("offset");
        if (!string.IsNullOrEmpty(offset))
            queryParams.Add($"offset={offset}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await GetAsync($"{baseId}/{tableId}{query}", ct);
    }

    private async Task<ActionResult> GetRecordAsync(ActionParameters parameters, CancellationToken ct)
    {
        var baseId = parameters.GetString("baseId")!;
        var tableId = Uri.EscapeDataString(parameters.GetString("tableId")!);
        var recordId = parameters.GetString("recordId")!;
        return await GetAsync($"{baseId}/{tableId}/{recordId}", ct);
    }

    private async Task<ActionResult> CreateRecordAsync(ActionParameters parameters, CancellationToken ct)
    {
        var baseId = parameters.GetString("baseId")!;
        var tableId = Uri.EscapeDataString(parameters.GetString("tableId")!);

        var payload = new Dictionary<string, object>
        {
            ["fields"] = parameters.Get<JsonElement>("fields")
        };

        if (parameters.GetBool("typecast"))
            payload["typecast"] = true;

        return await PostAsync($"{baseId}/{tableId}", payload, ct);
    }

    private async Task<ActionResult> CreateRecordsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var baseId = parameters.GetString("baseId")!;
        var tableId = Uri.EscapeDataString(parameters.GetString("tableId")!);

        var payload = new Dictionary<string, object>
        {
            ["records"] = parameters.Get<JsonElement>("records")
        };

        if (parameters.GetBool("typecast"))
            payload["typecast"] = true;

        return await PostAsync($"{baseId}/{tableId}", payload, ct);
    }

    private async Task<ActionResult> UpdateRecordAsync(ActionParameters parameters, CancellationToken ct)
    {
        var baseId = parameters.GetString("baseId")!;
        var tableId = Uri.EscapeDataString(parameters.GetString("tableId")!);
        var recordId = parameters.GetString("recordId")!;

        var payload = new Dictionary<string, object>
        {
            ["fields"] = parameters.Get<JsonElement>("fields")
        };

        if (parameters.GetBool("typecast"))
            payload["typecast"] = true;

        return await PatchAsync($"{baseId}/{tableId}/{recordId}", payload, ct);
    }

    private async Task<ActionResult> DeleteRecordAsync(ActionParameters parameters, CancellationToken ct)
    {
        var baseId = parameters.GetString("baseId")!;
        var tableId = Uri.EscapeDataString(parameters.GetString("tableId")!);
        var recordId = parameters.GetString("recordId")!;
        return await DeleteAsync($"{baseId}/{tableId}/{recordId}", ct);
    }

    private async Task<ActionResult> ListBasesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var queryParams = new List<string>();
        var offset = parameters.GetString("offset");
        if (!string.IsNullOrEmpty(offset))
            queryParams.Add($"offset={offset}");

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await GetAsync($"meta/bases{query}", ct);
    }

    private async Task<ActionResult> FindRecordsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var baseId = parameters.GetString("baseId")!;
        var tableId = Uri.EscapeDataString(parameters.GetString("tableId")!);
        var fieldName = parameters.GetString("fieldName")!;
        var value = parameters.GetString("value")!;
        var maxRecords = parameters.GetInt("maxRecords", 100);

        var formula = $"{{{fieldName}}} = \"{value.Replace("\"", "\\\"")}\"";
        var query = $"?filterByFormula={Uri.EscapeDataString(formula)}&maxRecords={maxRecords}";

        return await GetAsync($"{baseId}/{tableId}{query}", ct);
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
                return ActionResult.Ok(new Dictionary<string, object> { ["status"] = "success" });
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

        if ((int)response.StatusCode == 429)
        {
            return ActionResult.Fail("Airtable rate limit exceeded. Please wait 30 seconds before retrying.");
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                var message = error.TryGetProperty("message", out var msgProp)
                    ? msgProp.GetString()
                    : content;
                return ActionResult.Fail($"Airtable API error ({response.StatusCode}): {message}");
            }
        }
        catch { }

        return ActionResult.Fail($"Airtable API error ({response.StatusCode}): {content}");
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
