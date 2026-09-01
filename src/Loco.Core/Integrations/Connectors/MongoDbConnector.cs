// John Carmack: "Simple things should be simple, complex things should be possible"
// Rob Pike: "Don't panic"

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// MongoDB connector using MongoDB Data API
/// Supports CRUD operations, aggregation, and Atlas features
/// </summary>
public sealed class MongoDbConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private string? _dataSource;
    private string? _database;

    public override string Id => "mongodb";
    public override string Name => "MongoDB";
    public override string Description => "NoSQL document database operations: CRUD, aggregation, Atlas features";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Database;

    public override ConnectorCapabilities Capabilities => new()
    {
        SupportsActions = true,
        SupportsTriggers = true,
        SupportsWebhooks = true, // Atlas triggers
        SupportsBatching = true,
        DefaultTimeout = TimeSpan.FromSeconds(60)
    };

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.ApiKey,
        RequiredCredentials =
        [
            new() { Name = "apiKey", Label = "Data API Key", Type = ParameterType.Password, Required = true,
                Description = "MongoDB Atlas Data API key" },
            new() { Name = "appId", Label = "App ID", Type = ParameterType.String, Required = true,
                Description = "Data API App ID from Atlas" },
            new() { Name = "dataSource", Label = "Data Source", Type = ParameterType.String, Required = true,
                Description = "Cluster name (e.g., Cluster0)" }
        ]
    };

    public override IReadOnlyList<ConfigParameter> ConfigParameters =>
    [
        new() { Name = "database", Label = "Default Database", Type = ParameterType.String, Required = true },
        new() { Name = "region", Label = "Region", Type = ParameterType.Select, DefaultValue = "us-east-1",
            Options =
            [
                new() { Label = "US East 1", Value = "us-east-1" },
                new() { Label = "US West 2", Value = "us-west-2" },
                new() { Label = "EU West 1", Value = "eu-west-1" },
                new() { Label = "AP Southeast 1", Value = "ap-southeast-1" }
            ]}
    ];

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // Find
        new()
        {
            Id = "findOne",
            Name = "Find One",
            Description = "Find a single document",
            Parameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true },
                new() { Name = "filter", Type = ParameterType.Json, Required = true, Description = "{\"field\": \"value\"}" },
                new() { Name = "projection", Type = ParameterType.Json, Description = "{\"field\": 1}" },
                new() { Name = "database", Type = ParameterType.String }
            ]
        },
        new()
        {
            Id = "find",
            Name = "Find Many",
            Description = "Find multiple documents",
            Parameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true },
                new() { Name = "filter", Type = ParameterType.Json, DefaultValue = "{}" },
                new() { Name = "projection", Type = ParameterType.Json },
                new() { Name = "sort", Type = ParameterType.Json, Description = "{\"field\": 1}" },
                new() { Name = "limit", Type = ParameterType.Number, DefaultValue = 100 },
                new() { Name = "skip", Type = ParameterType.Number, DefaultValue = 0 },
                new() { Name = "database", Type = ParameterType.String }
            ]
        },
        // Insert
        new()
        {
            Id = "insertOne",
            Name = "Insert One",
            Description = "Insert a single document",
            Parameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true },
                new() { Name = "document", Type = ParameterType.Json, Required = true },
                new() { Name = "database", Type = ParameterType.String }
            ]
        },
        new()
        {
            Id = "insertMany",
            Name = "Insert Many",
            Description = "Insert multiple documents",
            Parameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true },
                new() { Name = "documents", Type = ParameterType.Json, Required = true, Description = "[{...}, {...}]" },
                new() { Name = "database", Type = ParameterType.String }
            ],
            RequiresConfirmation = true
        },
        // Update
        new()
        {
            Id = "updateOne",
            Name = "Update One",
            Description = "Update a single document",
            Parameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true },
                new() { Name = "filter", Type = ParameterType.Json, Required = true },
                new() { Name = "update", Type = ParameterType.Json, Required = true, Description = "{\"$set\": {...}}" },
                new() { Name = "upsert", Type = ParameterType.Boolean, DefaultValue = false },
                new() { Name = "database", Type = ParameterType.String }
            ]
        },
        new()
        {
            Id = "updateMany",
            Name = "Update Many",
            Description = "Update multiple documents",
            Parameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true },
                new() { Name = "filter", Type = ParameterType.Json, Required = true },
                new() { Name = "update", Type = ParameterType.Json, Required = true },
                new() { Name = "upsert", Type = ParameterType.Boolean, DefaultValue = false },
                new() { Name = "database", Type = ParameterType.String }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "replaceOne",
            Name = "Replace One",
            Description = "Replace a single document",
            Parameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true },
                new() { Name = "filter", Type = ParameterType.Json, Required = true },
                new() { Name = "replacement", Type = ParameterType.Json, Required = true },
                new() { Name = "upsert", Type = ParameterType.Boolean, DefaultValue = false },
                new() { Name = "database", Type = ParameterType.String }
            ]
        },
        // Delete
        new()
        {
            Id = "deleteOne",
            Name = "Delete One",
            Description = "Delete a single document",
            Parameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true },
                new() { Name = "filter", Type = ParameterType.Json, Required = true },
                new() { Name = "database", Type = ParameterType.String }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "deleteMany",
            Name = "Delete Many",
            Description = "Delete multiple documents",
            Parameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true },
                new() { Name = "filter", Type = ParameterType.Json, Required = true },
                new() { Name = "database", Type = ParameterType.String }
            ],
            RequiresConfirmation = true
        },
        // Aggregation
        new()
        {
            Id = "aggregate",
            Name = "Aggregate",
            Description = "Run an aggregation pipeline",
            Parameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true },
                new() { Name = "pipeline", Type = ParameterType.Json, Required = true,
                    Description = "[{\"$match\": {...}}, {\"$group\": {...}}]" },
                new() { Name = "database", Type = ParameterType.String }
            ]
        },
        // Count
        new()
        {
            Id = "countDocuments",
            Name = "Count Documents",
            Description = "Count documents matching a filter",
            Parameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true },
                new() { Name = "filter", Type = ParameterType.Json, DefaultValue = "{}" },
                new() { Name = "database", Type = ParameterType.String }
            ]
        },
        // Distinct
        new()
        {
            Id = "distinct",
            Name = "Distinct",
            Description = "Get distinct values for a field",
            Parameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true },
                new() { Name = "field", Type = ParameterType.String, Required = true },
                new() { Name = "filter", Type = ParameterType.Json, DefaultValue = "{}" },
                new() { Name = "database", Type = ParameterType.String }
            ]
        },
        // No index operations. This connector speaks the Atlas Data API
        // (data.mongodb-api.com/.../endpoint/data/v1/action/*), which offers
        // find, insert, update, delete and aggregate and nothing else - index
        // management lives in the Atlas Admin API or a driver, neither of
        // which this connector uses. A "createIndex" action was declared here
        // with no dispatch arm behind it, so the editor offered it, the
        // catalogue published it, and choosing it failed at execution with
        // "Unknown action: createIndex".
        // Find and modify
        new()
        {
            Id = "findOneAndUpdate",
            Name = "Find and Update",
            Description = "Find a document and update it atomically",
            Parameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true },
                new() { Name = "filter", Type = ParameterType.Json, Required = true },
                new() { Name = "update", Type = ParameterType.Json, Required = true },
                new() { Name = "returnDocument", Type = ParameterType.Select, DefaultValue = "after",
                    Options =
                    [
                        new() { Label = "Before", Value = "before" },
                        new() { Label = "After", Value = "after" }
                    ]},
                new() { Name = "upsert", Type = ParameterType.Boolean, DefaultValue = false },
                new() { Name = "database", Type = ParameterType.String }
            ]
        },
        new()
        {
            Id = "findOneAndDelete",
            Name = "Find and Delete",
            Description = "Find a document and delete it atomically",
            Parameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true },
                new() { Name = "filter", Type = ParameterType.Json, Required = true },
                new() { Name = "database", Type = ParameterType.String }
            ],
            RequiresConfirmation = true
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "onInsert",
            Name = "On Insert",
            Description = "Triggered when a document is inserted (Atlas Trigger)",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true },
                new() { Name = "database", Type = ParameterType.String }
            ]
        },
        new()
        {
            Id = "onUpdate",
            Name = "On Update",
            Description = "Triggered when a document is updated",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true },
                new() { Name = "fullDocument", Type = ParameterType.Boolean, DefaultValue = true }
            ]
        },
        new()
        {
            Id = "onDelete",
            Name = "On Delete",
            Description = "Triggered when a document is deleted",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "collection", Type = ParameterType.String, Required = true }
            ]
        }
    ];

    public override async Task<ConnectionTestResult> TestConnectionAsync(
        ConnectorConfiguration config,
        CancellationToken ct = default)
    {
        try
        {
            var apiKey = config.GetCredentialString("apiKey")!;
            var appId = config.GetCredentialString("appId")!;
            var dataSource = config.GetCredentialString("dataSource")!;
            var database = config.GetSettingString("database")!;
            var region = config.GetSettingString("region") ?? "us-east-1";

            using var client = new HttpClient();
            var baseUrl = GetDataApiUrl(appId, region);
            client.DefaultRequestHeaders.Add("api-key", apiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = new
            {
                dataSource,
                database,
                collection = "test",
                filter = new { }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{baseUrl}/action/findOne", content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                return ConnectionTestResult.Fail($"Connection failed: {error}");
            }

            return ConnectionTestResult.Ok($"Connected to MongoDB Atlas ({dataSource})");
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Fail("Connection test failed", ex);
        }
    }

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        var apiKey = config.GetCredentialString("apiKey")!;
        var appId = config.GetCredentialString("appId")!;
        var region = config.GetSettingString("region") ?? "us-east-1";

        _dataSource = config.GetCredentialString("dataSource")!;
        _database = config.GetSettingString("database")!;

        var baseUrl = GetDataApiUrl(appId, region);

        // Dispose any previous client before replacing it. InitializeAsync can run more
        // than once for the same cached connector instance (e.g. ConnectorRegistry.
        // GetInitializedConnectorAsync on credential rotation); overwriting _httpClient
        // unconditionally previously leaked the old HttpClient and its socket handler.
        _httpClient?.Dispose();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl + "/action/")
        };
        _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        await base.InitializeAsync(config, ct);
    }

    private static string GetDataApiUrl(string appId, string region)
    {
        return $"https://{region}.aws.data.mongodb-api.com/app/{appId}/endpoint/data/v1";
    }

    protected override async Task<ActionResult> ExecuteActionCoreAsync(
        ConnectorAction action,
        ActionParameters parameters,
        Core.ExecutionContext context,
        CancellationToken ct)
    {
        return action.Id switch
        {
            "findOne" => await FindOneAsync(parameters, ct),
            "find" => await FindAsync(parameters, ct),
            "insertOne" => await InsertOneAsync(parameters, ct),
            "insertMany" => await InsertManyAsync(parameters, ct),
            "updateOne" => await UpdateOneAsync(parameters, ct),
            "updateMany" => await UpdateManyAsync(parameters, ct),
            "replaceOne" => await ReplaceOneAsync(parameters, ct),
            "deleteOne" => await DeleteOneAsync(parameters, ct),
            "deleteMany" => await DeleteManyAsync(parameters, ct),
            "aggregate" => await AggregateAsync(parameters, ct),
            "countDocuments" => await CountDocumentsAsync(parameters, ct),
            "distinct" => await DistinctAsync(parameters, ct),
            "findOneAndUpdate" => await FindOneAndUpdateAsync(parameters, ct),
            "findOneAndDelete" => await FindOneAndDeleteAsync(parameters, ct),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> FindOneAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = BuildBasePayload(parameters);
        payload["filter"] = parameters.Get<JsonElement>("filter");

        var projection = parameters.Get<JsonElement?>("projection");
        if (projection.HasValue && projection.Value.ValueKind != JsonValueKind.Null)
        {
            payload["projection"] = projection.Value;
        }

        return await ExecuteDataApiAsync("findOne", payload, ct);
    }

    private async Task<ActionResult> FindAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = BuildBasePayload(parameters);
        payload["filter"] = parameters.Get<JsonElement?>("filter") ?? JsonDocument.Parse("{}").RootElement;

        var projection = parameters.Get<JsonElement?>("projection");
        if (projection.HasValue && projection.Value.ValueKind != JsonValueKind.Null)
        {
            payload["projection"] = projection.Value;
        }

        var sort = parameters.Get<JsonElement?>("sort");
        if (sort.HasValue && sort.Value.ValueKind != JsonValueKind.Null)
        {
            payload["sort"] = sort.Value;
        }

        var limit = parameters.GetInt("limit", 100);
        if (limit > 0)
        {
            payload["limit"] = limit;
        }

        var skip = parameters.GetInt("skip", 0);
        if (skip > 0)
        {
            payload["skip"] = skip;
        }

        return await ExecuteDataApiAsync("find", payload, ct);
    }

    private async Task<ActionResult> InsertOneAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = BuildBasePayload(parameters);
        payload["document"] = parameters.Get<JsonElement>("document");

        return await ExecuteDataApiAsync("insertOne", payload, ct);
    }

    private async Task<ActionResult> InsertManyAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = BuildBasePayload(parameters);
        payload["documents"] = parameters.Get<JsonElement>("documents");

        return await ExecuteDataApiAsync("insertMany", payload, ct);
    }

    private async Task<ActionResult> UpdateOneAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = BuildBasePayload(parameters);
        payload["filter"] = parameters.Get<JsonElement>("filter");
        payload["update"] = parameters.Get<JsonElement>("update");
        payload["upsert"] = parameters.GetBool("upsert");

        return await ExecuteDataApiAsync("updateOne", payload, ct);
    }

    private async Task<ActionResult> UpdateManyAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = BuildBasePayload(parameters);
        payload["filter"] = parameters.Get<JsonElement>("filter");
        payload["update"] = parameters.Get<JsonElement>("update");
        payload["upsert"] = parameters.GetBool("upsert");

        return await ExecuteDataApiAsync("updateMany", payload, ct);
    }

    private async Task<ActionResult> ReplaceOneAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = BuildBasePayload(parameters);
        payload["filter"] = parameters.Get<JsonElement>("filter");
        payload["replacement"] = parameters.Get<JsonElement>("replacement");
        payload["upsert"] = parameters.GetBool("upsert");

        return await ExecuteDataApiAsync("replaceOne", payload, ct);
    }

    private async Task<ActionResult> DeleteOneAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = BuildBasePayload(parameters);
        payload["filter"] = parameters.Get<JsonElement>("filter");

        return await ExecuteDataApiAsync("deleteOne", payload, ct);
    }

    private async Task<ActionResult> DeleteManyAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = BuildBasePayload(parameters);
        payload["filter"] = parameters.Get<JsonElement>("filter");

        return await ExecuteDataApiAsync("deleteMany", payload, ct);
    }

    private async Task<ActionResult> AggregateAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = BuildBasePayload(parameters);
        payload["pipeline"] = parameters.Get<JsonElement>("pipeline");

        return await ExecuteDataApiAsync("aggregate", payload, ct);
    }

    private async Task<ActionResult> CountDocumentsAsync(ActionParameters parameters, CancellationToken ct)
    {
        // Use aggregation with $count for counting
        var payload = BuildBasePayload(parameters);
        var filter = parameters.Get<JsonElement?>("filter") ?? JsonDocument.Parse("{}").RootElement;

        var pipeline = new object[]
        {
            new { match = filter },
            new { count = "count" }
        };

        payload["pipeline"] = JsonSerializer.SerializeToElement(pipeline);

        var result = await ExecuteDataApiAsync("aggregate", payload, ct);
        if (!result.Success) return result;

        var data = (JsonElement)result.Data!;
        if (data.TryGetProperty("documents", out var docs) && docs.GetArrayLength() > 0)
        {
            var count = docs[0].GetProperty("count").GetInt32();
            return ActionResult.Ok(new { count });
        }

        return ActionResult.Ok(new { count = 0 });
    }

    private async Task<ActionResult> DistinctAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = BuildBasePayload(parameters);
        var field = parameters.GetString("field")!;
        var filter = parameters.Get<JsonElement?>("filter") ?? JsonDocument.Parse("{}").RootElement;

        // Use aggregation for distinct values
        var pipeline = new object[]
        {
            new { match = filter },
            new { group = new { _id = $"${field}" } },
            new { project = new { _id = 0, value = "$_id" } }
        };

        payload["pipeline"] = JsonSerializer.SerializeToElement(pipeline);

        var result = await ExecuteDataApiAsync("aggregate", payload, ct);
        if (!result.Success) return result;

        var data = (JsonElement)result.Data!;
        if (data.TryGetProperty("documents", out var docs))
        {
            var values = docs.EnumerateArray()
                .Select(d => d.GetProperty("value"))
                .ToList();
            return ActionResult.Ok(new { values, count = values.Count });
        }

        return ActionResult.Ok(new { values = Array.Empty<object>(), count = 0 });
    }

    private async Task<ActionResult> FindOneAndUpdateAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = BuildBasePayload(parameters);
        payload["filter"] = parameters.Get<JsonElement>("filter");
        payload["update"] = parameters.Get<JsonElement>("update");
        payload["returnDocument"] = parameters.GetString("returnDocument") ?? "after";
        payload["upsert"] = parameters.GetBool("upsert");

        return await ExecuteDataApiAsync("findOneAndUpdate", payload, ct);
    }

    private async Task<ActionResult> FindOneAndDeleteAsync(ActionParameters parameters, CancellationToken ct)
    {
        var payload = BuildBasePayload(parameters);
        payload["filter"] = parameters.Get<JsonElement>("filter");

        return await ExecuteDataApiAsync("findOneAndDelete", payload, ct);
    }

    private Dictionary<string, object> BuildBasePayload(ActionParameters parameters)
    {
        return new Dictionary<string, object>
        {
            ["dataSource"] = _dataSource!,
            ["database"] = parameters.GetString("database") ?? _database!,
            ["collection"] = parameters.GetString("collection")!
        };
    }

    private async Task<ActionResult> ExecuteDataApiAsync(string action, Dictionary<string, object> payload, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient!.PostAsync(action, content, ct);

        var result = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorJson = JsonSerializer.Deserialize<JsonElement>(result);
            var errorMessage = errorJson.TryGetProperty("error", out var err)
                ? err.GetString()
                : result;
            return ActionResult.Fail($"MongoDB error: {errorMessage}", "API_ERROR");
        }

        return ActionResult.Ok(JsonSerializer.Deserialize<JsonElement>(result));
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
