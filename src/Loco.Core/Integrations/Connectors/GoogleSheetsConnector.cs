// John Carmack: "The best code is no code at all"
// Rob Pike: "Clear is better than clever"

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Google Sheets connector for spreadsheet operations
/// Uses Google Sheets API v4
/// </summary>
public sealed class GoogleSheetsConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private const string SheetsApiBase = "https://sheets.googleapis.com/v4/spreadsheets";

    public override string Id => "google-sheets";
    public override string Name => "Google Sheets";
    public override string Description => "Read, write, and manipulate Google Sheets spreadsheets";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Productivity;

    public override ConnectorCapabilities Capabilities => new()
    {
        SupportsActions = true,
        SupportsTriggers = false, // Would need Google Apps Script
        SupportsBatching = true,
        RateLimitPerMinute = 60
    };

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.OAuth2,
        AuthorizationUrl = "https://accounts.google.com/o/oauth2/v2/auth",
        TokenUrl = "https://oauth2.googleapis.com/token",
        Scopes = ["https://www.googleapis.com/auth/spreadsheets"],
        RequiredCredentials =
        [
            new() { Name = "accessToken", Label = "Access Token", Type = ParameterType.Password, Required = true,
                Description = "OAuth2 access token with spreadsheets scope" },
            new() { Name = "refreshToken", Label = "Refresh Token", Type = ParameterType.Password, Required = false },
            new() { Name = "apiKey", Label = "API Key", Type = ParameterType.Password, Required = false,
                Description = "Alternative: API key for public sheets" }
        ]
    };

    public override IReadOnlyList<ConfigParameter> ConfigParameters =>
    [
        new() { Name = "defaultSpreadsheetId", Label = "Default Spreadsheet ID", Type = ParameterType.String,
            Description = "Default spreadsheet ID to use" }
    ];

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // Read operations
        new()
        {
            Id = "getValues",
            Name = "Get Values",
            Description = "Read values from a range",
            Parameters =
            [
                new() { Name = "spreadsheetId", Type = ParameterType.String, Description = "Uses default if not specified" },
                new() { Name = "range", Type = ParameterType.String, Required = true,
                    Description = "A1 notation: Sheet1!A1:D10" },
                new() { Name = "valueRenderOption", Type = ParameterType.Select, DefaultValue = "FORMATTED_VALUE",
                    Options =
                    [
                        new() { Label = "Formatted Value", Value = "FORMATTED_VALUE" },
                        new() { Label = "Unformatted Value", Value = "UNFORMATTED_VALUE" },
                        new() { Label = "Formula", Value = "FORMULA" }
                    ]},
                new() { Name = "majorDimension", Type = ParameterType.Select, DefaultValue = "ROWS",
                    Options =
                    [
                        new() { Label = "Rows", Value = "ROWS" },
                        new() { Label = "Columns", Value = "COLUMNS" }
                    ]}
            ]
        },
        new()
        {
            Id = "batchGetValues",
            Name = "Batch Get Values",
            Description = "Read values from multiple ranges",
            Parameters =
            [
                new() { Name = "spreadsheetId", Type = ParameterType.String },
                new() { Name = "ranges", Type = ParameterType.Json, Required = true,
                    Description = "[\"Sheet1!A1:B10\", \"Sheet2!A1:C5\"]" }
            ]
        },
        // Write operations
        new()
        {
            Id = "updateValues",
            Name = "Update Values",
            Description = "Write values to a range",
            Parameters =
            [
                new() { Name = "spreadsheetId", Type = ParameterType.String },
                new() { Name = "range", Type = ParameterType.String, Required = true },
                new() { Name = "values", Type = ParameterType.Json, Required = true,
                    Description = "[[\"A1\", \"B1\"], [\"A2\", \"B2\"]]" },
                new() { Name = "valueInputOption", Type = ParameterType.Select, DefaultValue = "USER_ENTERED",
                    Options =
                    [
                        new() { Label = "User Entered (parse formulas)", Value = "USER_ENTERED" },
                        new() { Label = "Raw (literal text)", Value = "RAW" }
                    ]}
            ]
        },
        new()
        {
            Id = "appendValues",
            Name = "Append Values",
            Description = "Append rows to a sheet",
            Parameters =
            [
                new() { Name = "spreadsheetId", Type = ParameterType.String },
                new() { Name = "range", Type = ParameterType.String, Required = true,
                    Description = "Sheet name or range to append to" },
                new() { Name = "values", Type = ParameterType.Json, Required = true },
                new() { Name = "valueInputOption", Type = ParameterType.Select, DefaultValue = "USER_ENTERED",
                    Options =
                    [
                        new() { Label = "User Entered", Value = "USER_ENTERED" },
                        new() { Label = "Raw", Value = "RAW" }
                    ]},
                new() { Name = "insertDataOption", Type = ParameterType.Select, DefaultValue = "INSERT_ROWS",
                    Options =
                    [
                        new() { Label = "Insert Rows", Value = "INSERT_ROWS" },
                        new() { Label = "Overwrite", Value = "OVERWRITE" }
                    ]}
            ]
        },
        new()
        {
            Id = "clearValues",
            Name = "Clear Values",
            Description = "Clear values from a range",
            Parameters =
            [
                new() { Name = "spreadsheetId", Type = ParameterType.String },
                new() { Name = "range", Type = ParameterType.String, Required = true }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "batchUpdateValues",
            Name = "Batch Update Values",
            Description = "Update multiple ranges at once",
            Parameters =
            [
                new() { Name = "spreadsheetId", Type = ParameterType.String },
                new() { Name = "data", Type = ParameterType.Json, Required = true,
                    Description = "[{range: \"A1:B2\", values: [[1,2],[3,4]]}]" },
                new() { Name = "valueInputOption", Type = ParameterType.Select, DefaultValue = "USER_ENTERED",
                    Options =
                    [
                        new() { Label = "User Entered", Value = "USER_ENTERED" },
                        new() { Label = "Raw", Value = "RAW" }
                    ]}
            ]
        },
        // Spreadsheet operations
        new()
        {
            Id = "getSpreadsheet",
            Name = "Get Spreadsheet",
            Description = "Get spreadsheet metadata",
            Parameters =
            [
                new() { Name = "spreadsheetId", Type = ParameterType.String },
                new() { Name = "includeGridData", Type = ParameterType.Boolean, DefaultValue = false }
            ]
        },
        new()
        {
            Id = "createSpreadsheet",
            Name = "Create Spreadsheet",
            Description = "Create a new spreadsheet",
            Parameters =
            [
                new() { Name = "title", Type = ParameterType.String, Required = true },
                new() { Name = "sheets", Type = ParameterType.Json, Description = "[{title: \"Sheet1\"}]" }
            ]
        },
        // Sheet operations
        new()
        {
            Id = "addSheet",
            Name = "Add Sheet",
            Description = "Add a new sheet to a spreadsheet",
            Parameters =
            [
                new() { Name = "spreadsheetId", Type = ParameterType.String },
                new() { Name = "title", Type = ParameterType.String, Required = true },
                new() { Name = "rowCount", Type = ParameterType.Number, DefaultValue = 1000 },
                new() { Name = "columnCount", Type = ParameterType.Number, DefaultValue = 26 }
            ]
        },
        new()
        {
            Id = "deleteSheet",
            Name = "Delete Sheet",
            Description = "Delete a sheet from a spreadsheet",
            Parameters =
            [
                new() { Name = "spreadsheetId", Type = ParameterType.String },
                new() { Name = "sheetId", Type = ParameterType.Number, Required = true }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "copySheet",
            Name = "Copy Sheet",
            Description = "Copy a sheet to another spreadsheet",
            Parameters =
            [
                new() { Name = "spreadsheetId", Type = ParameterType.String },
                new() { Name = "sheetId", Type = ParameterType.Number, Required = true },
                new() { Name = "destinationSpreadsheetId", Type = ParameterType.String, Required = true }
            ]
        },
        // Row operations
        new()
        {
            Id = "insertRows",
            Name = "Insert Rows",
            Description = "Insert empty rows",
            Parameters =
            [
                new() { Name = "spreadsheetId", Type = ParameterType.String },
                new() { Name = "sheetId", Type = ParameterType.Number, Required = true },
                new() { Name = "startIndex", Type = ParameterType.Number, Required = true },
                new() { Name = "endIndex", Type = ParameterType.Number, Required = true }
            ]
        },
        new()
        {
            Id = "deleteRows",
            Name = "Delete Rows",
            Description = "Delete rows",
            Parameters =
            [
                new() { Name = "spreadsheetId", Type = ParameterType.String },
                new() { Name = "sheetId", Type = ParameterType.Number, Required = true },
                new() { Name = "startIndex", Type = ParameterType.Number, Required = true },
                new() { Name = "endIndex", Type = ParameterType.Number, Required = true }
            ],
            RequiresConfirmation = true
        },
        // Formatting
        new()
        {
            Id = "formatCells",
            Name = "Format Cells",
            Description = "Apply formatting to cells",
            Parameters =
            [
                new() { Name = "spreadsheetId", Type = ParameterType.String },
                new() { Name = "sheetId", Type = ParameterType.Number, Required = true },
                new() { Name = "startRowIndex", Type = ParameterType.Number, Required = true },
                new() { Name = "endRowIndex", Type = ParameterType.Number, Required = true },
                new() { Name = "startColumnIndex", Type = ParameterType.Number, Required = true },
                new() { Name = "endColumnIndex", Type = ParameterType.Number, Required = true },
                new() { Name = "format", Type = ParameterType.Json, Required = true,
                    Description = "{backgroundColor: {red: 1}, textFormat: {bold: true}}" }
            ]
        },
        // Find
        new()
        {
            Id = "findRow",
            Name = "Find Row",
            Description = "Find rows containing a value",
            Parameters =
            [
                new() { Name = "spreadsheetId", Type = ParameterType.String },
                new() { Name = "range", Type = ParameterType.String, Required = true },
                new() { Name = "searchValue", Type = ParameterType.String, Required = true },
                new() { Name = "column", Type = ParameterType.Number, Description = "Column index to search (0-based)" }
            ]
        }
    ];

    public override async Task<ConnectionTestResult> TestConnectionAsync(
        ConnectorConfiguration config,
        CancellationToken ct = default)
    {
        try
        {
            var accessToken = config.GetCredentialString("accessToken");
            var apiKey = config.GetCredentialString("apiKey");

            using var client = new HttpClient();

            string url;
            if (!string.IsNullOrEmpty(accessToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                url = "https://www.googleapis.com/drive/v3/about?fields=user";
            }
            else if (!string.IsNullOrEmpty(apiKey))
            {
                url = $"https://www.googleapis.com/drive/v3/about?fields=user&key={apiKey}";
            }
            else
            {
                return ConnectionTestResult.Fail("Access token or API key is required");
            }

            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                return ConnectionTestResult.Fail($"Authentication failed: {response.StatusCode}");
            }

            var result = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<JsonElement>(result);
            var email = data.GetProperty("user").GetProperty("emailAddress").GetString();

            return ConnectionTestResult.Ok($"Connected as {email}");
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Fail("Connection test failed", ex);
        }
    }

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        _httpClient = new HttpClient();

        var accessToken = config.GetCredentialString("accessToken");
        if (!string.IsNullOrEmpty(accessToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

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
            "getValues" => await GetValuesAsync(parameters, ct),
            "batchGetValues" => await BatchGetValuesAsync(parameters, ct),
            "updateValues" => await UpdateValuesAsync(parameters, ct),
            "appendValues" => await AppendValuesAsync(parameters, ct),
            "clearValues" => await ClearValuesAsync(parameters, ct),
            "batchUpdateValues" => await BatchUpdateValuesAsync(parameters, ct),
            "getSpreadsheet" => await GetSpreadsheetAsync(parameters, ct),
            "createSpreadsheet" => await CreateSpreadsheetAsync(parameters, ct),
            "addSheet" => await AddSheetAsync(parameters, ct),
            "deleteSheet" => await DeleteSheetAsync(parameters, ct),
            "copySheet" => await CopySheetAsync(parameters, ct),
            "insertRows" => await InsertRowsAsync(parameters, ct),
            "deleteRows" => await DeleteRowsAsync(parameters, ct),
            "formatCells" => await FormatCellsAsync(parameters, ct),
            "findRow" => await FindRowAsync(parameters, ct),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private string GetSpreadsheetId(ActionParameters parameters)
    {
        return parameters.GetString("spreadsheetId")
            ?? Configuration?.GetSettingString("defaultSpreadsheetId")
            ?? throw new ArgumentException("Spreadsheet ID is required");
    }

    private async Task<ActionResult> GetValuesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var spreadsheetId = GetSpreadsheetId(parameters);
        var range = parameters.GetString("range")!;
        var valueRenderOption = parameters.GetString("valueRenderOption") ?? "FORMATTED_VALUE";
        var majorDimension = parameters.GetString("majorDimension") ?? "ROWS";

        var url = $"{SheetsApiBase}/{spreadsheetId}/values/{Uri.EscapeDataString(range)}" +
                  $"?valueRenderOption={valueRenderOption}&majorDimension={majorDimension}";

        var response = await _httpClient!.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to get values: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> BatchGetValuesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var spreadsheetId = GetSpreadsheetId(parameters);
        var ranges = parameters.Get<JsonElement>("ranges");

        var rangeList = ranges.EnumerateArray()
            .Select(r => Uri.EscapeDataString(r.GetString()!))
            .ToList();

        var url = $"{SheetsApiBase}/{spreadsheetId}/values:batchGet?ranges=" + string.Join("&ranges=", rangeList);

        var response = await _httpClient!.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to batch get values: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> UpdateValuesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var spreadsheetId = GetSpreadsheetId(parameters);
        var range = parameters.GetString("range")!;
        var values = parameters.Get<JsonElement>("values");
        var valueInputOption = parameters.GetString("valueInputOption") ?? "USER_ENTERED";

        var url = $"{SheetsApiBase}/{spreadsheetId}/values/{Uri.EscapeDataString(range)}?valueInputOption={valueInputOption}";

        var payload = new { values };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient!.PutAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to update values: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> AppendValuesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var spreadsheetId = GetSpreadsheetId(parameters);
        var range = parameters.GetString("range")!;
        var values = parameters.Get<JsonElement>("values");
        var valueInputOption = parameters.GetString("valueInputOption") ?? "USER_ENTERED";
        var insertDataOption = parameters.GetString("insertDataOption") ?? "INSERT_ROWS";

        var url = $"{SheetsApiBase}/{spreadsheetId}/values/{Uri.EscapeDataString(range)}:append" +
                  $"?valueInputOption={valueInputOption}&insertDataOption={insertDataOption}";

        var payload = new { values };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to append values: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> ClearValuesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var spreadsheetId = GetSpreadsheetId(parameters);
        var range = parameters.GetString("range")!;

        var url = $"{SheetsApiBase}/{spreadsheetId}/values/{Uri.EscapeDataString(range)}:clear";

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to clear values: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> BatchUpdateValuesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var spreadsheetId = GetSpreadsheetId(parameters);
        var data = parameters.Get<JsonElement>("data");
        var valueInputOption = parameters.GetString("valueInputOption") ?? "USER_ENTERED";

        var url = $"{SheetsApiBase}/{spreadsheetId}/values:batchUpdate";

        var payload = new
        {
            valueInputOption,
            data
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to batch update: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> GetSpreadsheetAsync(ActionParameters parameters, CancellationToken ct)
    {
        var spreadsheetId = GetSpreadsheetId(parameters);
        var includeGridData = parameters.GetBool("includeGridData");

        var url = $"{SheetsApiBase}/{spreadsheetId}?includeGridData={includeGridData.ToString().ToLower()}";

        var response = await _httpClient!.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to get spreadsheet: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> CreateSpreadsheetAsync(ActionParameters parameters, CancellationToken ct)
    {
        var title = parameters.GetString("title")!;
        var sheets = parameters.Get<JsonElement?>("sheets");

        var payload = new Dictionary<string, object>
        {
            ["properties"] = new { title }
        };

        if (sheets.HasValue && sheets.Value.ValueKind == JsonValueKind.Array)
        {
            var sheetList = new List<object>();
            foreach (var sheet in sheets.Value.EnumerateArray())
            {
                var sheetTitle = sheet.TryGetProperty("title", out var t) ? t.GetString() : "Sheet1";
                sheetList.Add(new { properties = new { title = sheetTitle } });
            }
            payload["sheets"] = sheetList;
        }

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync(SheetsApiBase, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to create spreadsheet: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> AddSheetAsync(ActionParameters parameters, CancellationToken ct)
    {
        var spreadsheetId = GetSpreadsheetId(parameters);
        var title = parameters.GetString("title")!;
        var rowCount = parameters.GetInt("rowCount", 1000);
        var columnCount = parameters.GetInt("columnCount", 26);

        var request = new
        {
            requests = new[]
            {
                new
                {
                    addSheet = new
                    {
                        properties = new
                        {
                            title,
                            gridProperties = new { rowCount, columnCount }
                        }
                    }
                }
            }
        };

        return await BatchUpdateAsync(spreadsheetId, request, ct);
    }

    private async Task<ActionResult> DeleteSheetAsync(ActionParameters parameters, CancellationToken ct)
    {
        var spreadsheetId = GetSpreadsheetId(parameters);
        var sheetId = parameters.GetInt("sheetId");

        var request = new
        {
            requests = new[]
            {
                new { deleteSheet = new { sheetId } }
            }
        };

        return await BatchUpdateAsync(spreadsheetId, request, ct);
    }

    private async Task<ActionResult> CopySheetAsync(ActionParameters parameters, CancellationToken ct)
    {
        var spreadsheetId = GetSpreadsheetId(parameters);
        var sheetId = parameters.GetInt("sheetId");
        var destinationSpreadsheetId = parameters.GetString("destinationSpreadsheetId")!;

        var url = $"{SheetsApiBase}/{spreadsheetId}/sheets/{sheetId}:copyTo";
        var payload = new { destinationSpreadsheetId };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Failed to copy sheet: {error}", "API_ERROR");
        }

        var result = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    private async Task<ActionResult> InsertRowsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var spreadsheetId = GetSpreadsheetId(parameters);
        var sheetId = parameters.GetInt("sheetId");
        var startIndex = parameters.GetInt("startIndex");
        var endIndex = parameters.GetInt("endIndex");

        var request = new
        {
            requests = new[]
            {
                new
                {
                    insertDimension = new
                    {
                        range = new
                        {
                            sheetId,
                            dimension = "ROWS",
                            startIndex,
                            endIndex
                        },
                        inheritFromBefore = false
                    }
                }
            }
        };

        return await BatchUpdateAsync(spreadsheetId, request, ct);
    }

    private async Task<ActionResult> DeleteRowsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var spreadsheetId = GetSpreadsheetId(parameters);
        var sheetId = parameters.GetInt("sheetId");
        var startIndex = parameters.GetInt("startIndex");
        var endIndex = parameters.GetInt("endIndex");

        var request = new
        {
            requests = new[]
            {
                new
                {
                    deleteDimension = new
                    {
                        range = new
                        {
                            sheetId,
                            dimension = "ROWS",
                            startIndex,
                            endIndex
                        }
                    }
                }
            }
        };

        return await BatchUpdateAsync(spreadsheetId, request, ct);
    }

    private async Task<ActionResult> FormatCellsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var spreadsheetId = GetSpreadsheetId(parameters);
        var sheetId = parameters.GetInt("sheetId");
        var startRowIndex = parameters.GetInt("startRowIndex");
        var endRowIndex = parameters.GetInt("endRowIndex");
        var startColumnIndex = parameters.GetInt("startColumnIndex");
        var endColumnIndex = parameters.GetInt("endColumnIndex");
        var format = parameters.Get<JsonElement>("format");

        var request = new
        {
            requests = new[]
            {
                new
                {
                    repeatCell = new
                    {
                        range = new
                        {
                            sheetId,
                            startRowIndex,
                            endRowIndex,
                            startColumnIndex,
                            endColumnIndex
                        },
                        cell = new { userEnteredFormat = format },
                        fields = "userEnteredFormat"
                    }
                }
            }
        };

        return await BatchUpdateAsync(spreadsheetId, request, ct);
    }

    private async Task<ActionResult> FindRowAsync(ActionParameters parameters, CancellationToken ct)
    {
        var spreadsheetId = GetSpreadsheetId(parameters);
        var range = parameters.GetString("range")!;
        var searchValue = parameters.GetString("searchValue")!;
        var column = parameters.GetInt("column", -1);

        // Get all values first
        var valuesResult = await GetValuesAsync(new ActionParameters(new Dictionary<string, object?>
        {
            ["spreadsheetId"] = spreadsheetId,
            ["range"] = range
        }), ct);

        if (!valuesResult.Success)
        {
            return valuesResult;
        }

        var data = (JsonElement)valuesResult.Data!;
        if (!data.TryGetProperty("values", out var values))
        {
            return ActionResult.Ok(new { found = false, rows = Array.Empty<object>() });
        }

        var matchingRows = new List<object>();
        var rowIndex = 0;

        foreach (var row in values.EnumerateArray())
        {
            var cols = row.EnumerateArray().ToList();
            var found = false;

            if (column >= 0 && column < cols.Count)
            {
                found = cols[column].ToString().Contains(searchValue, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                found = cols.Any(c => c.ToString().Contains(searchValue, StringComparison.OrdinalIgnoreCase));
            }

            if (found)
            {
                matchingRows.Add(new
                {
                    rowIndex,
                    values = cols.Select(c => c.ToString()).ToList()
                });
            }

            rowIndex++;
        }

        return ActionResult.Ok(new
        {
            found = matchingRows.Count > 0,
            count = matchingRows.Count,
            rows = matchingRows
        });
    }

    private async Task<ActionResult> BatchUpdateAsync(string spreadsheetId, object request, CancellationToken ct)
    {
        var url = $"{SheetsApiBase}/{spreadsheetId}:batchUpdate";
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Batch update failed: {error}", "API_ERROR");
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
