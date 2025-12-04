// John Carmack: "The obvious implementation is often the best"
// Rob Pike: "Simplicity is the key to reliability"

using System.Data;
using System.Data.Common;
using System.Text.Json;
using Loco.Core.Integrations.Core;
using Loco.Core.Practical;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// MySQL/MariaDB database connector
/// Compatible with PostgreSQL connector API for easy migration
/// </summary>
public sealed class MySqlConnector : ConnectorBase
{
    private Func<DbConnection>? _connectionFactory;
    private string? _connectionString;

    public override string Id => "mysql";
    public override string Name => "MySQL";
    public override string Description => "MySQL/MariaDB database connector for queries, transactions, and data operations";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Database;

    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.ForDatabase();

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.ConnectionString,
        RequiredCredentials =
        [
            new() { Name = "connectionString", Label = "Connection String", Type = ParameterType.Password, Required = false,
                Description = "MySQL connection string (e.g., Server=localhost;Database=mydb;User=user;Password=pass)" },
            new() { Name = "host", Label = "Host", Type = ParameterType.String, Required = false },
            new() { Name = "port", Label = "Port", Type = ParameterType.Number, Required = false },
            new() { Name = "database", Label = "Database", Type = ParameterType.String, Required = false },
            new() { Name = "username", Label = "Username", Type = ParameterType.String, Required = false },
            new() { Name = "password", Label = "Password", Type = ParameterType.Password, Required = false }
        ]
    };

    public override IReadOnlyList<ConfigParameter> ConfigParameters =>
    [
        new() { Name = "commandTimeout", Label = "Command Timeout (seconds)", Type = ParameterType.Number, DefaultValue = 30 },
        new() { Name = "sslMode", Label = "SSL Mode", Type = ParameterType.Select, DefaultValue = "Preferred",
            Options =
            [
                new() { Label = "None", Value = "None" },
                new() { Label = "Preferred", Value = "Preferred" },
                new() { Label = "Required", Value = "Required" },
                new() { Label = "Verify CA", Value = "VerifyCA" },
                new() { Label = "Verify Full", Value = "VerifyFull" }
            ]}
    ];

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        new()
        {
            Id = "query",
            Name = "Execute Query",
            Description = "Execute a SELECT query and return results",
            Parameters =
            [
                new() { Name = "sql", Type = ParameterType.Code, Required = true, Description = "SQL query" },
                new() { Name = "parameters", Type = ParameterType.Json, Description = "Query parameters" },
                new() { Name = "timeout", Type = ParameterType.Number, Description = "Query timeout in seconds" }
            ]
        },
        new()
        {
            Id = "execute",
            Name = "Execute Command",
            Description = "Execute INSERT, UPDATE, DELETE, or DDL command",
            Parameters =
            [
                new() { Name = "sql", Type = ParameterType.Code, Required = true },
                new() { Name = "parameters", Type = ParameterType.Json },
                new() { Name = "timeout", Type = ParameterType.Number }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "scalar",
            Name = "Execute Scalar",
            Description = "Execute query and return single value",
            Parameters =
            [
                new() { Name = "sql", Type = ParameterType.Code, Required = true },
                new() { Name = "parameters", Type = ParameterType.Json }
            ]
        },
        new()
        {
            Id = "transaction",
            Name = "Execute Transaction",
            Description = "Execute multiple commands in a transaction",
            Parameters =
            [
                new() { Name = "commands", Type = ParameterType.Json, Required = true,
                    Description = "Array of {sql, parameters} objects" }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "bulkInsert",
            Name = "Bulk Insert",
            Description = "Insert multiple rows efficiently",
            Parameters =
            [
                new() { Name = "table", Type = ParameterType.String, Required = true },
                new() { Name = "columns", Type = ParameterType.Json, Required = true },
                new() { Name = "rows", Type = ParameterType.Json, Required = true },
                new() { Name = "batchSize", Type = ParameterType.Number, DefaultValue = 1000 }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "getTables",
            Name = "Get Tables",
            Description = "List all tables in the database",
            Parameters =
            [
                new() { Name = "database", Type = ParameterType.String, Description = "Database name (uses current if not specified)" }
            ]
        },
        new()
        {
            Id = "getColumns",
            Name = "Get Columns",
            Description = "Get column information for a table",
            Parameters =
            [
                new() { Name = "table", Type = ParameterType.String, Required = true }
            ]
        }
    ];

    public override async Task<ConnectionTestResult> TestConnectionAsync(
        ConnectorConfiguration config,
        CancellationToken ct = default)
    {
        try
        {
            var connectionString = BuildConnectionString(config);
            using var connection = CreateConnection(connectionString);
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT VERSION()";
            var version = await cmd.ExecuteScalarAsync(ct);

            return ConnectionTestResult.Ok($"Connected to MySQL: {version}");
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Fail("Connection failed", ex);
        }
    }

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        _connectionString = BuildConnectionString(config);
        _connectionFactory = () => CreateConnection(_connectionString);
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
            "query" => await ExecuteQueryAsync(parameters, ct),
            "execute" => await ExecuteCommandAsync(parameters, ct),
            "scalar" => await ExecuteScalarAsync(parameters, ct),
            "transaction" => await ExecuteTransactionAsync(parameters, ct),
            "bulkInsert" => await BulkInsertAsync(parameters, ct),
            "getTables" => await GetTablesAsync(parameters, ct),
            "getColumns" => await GetColumnsAsync(parameters, ct),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> ExecuteQueryAsync(ActionParameters parameters, CancellationToken ct)
    {
        var sql = parameters.GetString("sql")!;
        var queryParams = parameters.Get<Dictionary<string, object>>("parameters");
        var timeout = parameters.GetInt("timeout", 30);

        using var connection = _connectionFactory!();
        await connection.OpenAsync(ct);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = timeout;
        AddParameters(cmd, queryParams);

        var results = new List<Dictionary<string, object?>>();

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }

        return ActionResult.Ok(new { rows = results, rowCount = results.Count });
    }

    private async Task<ActionResult> ExecuteCommandAsync(ActionParameters parameters, CancellationToken ct)
    {
        var sql = parameters.GetString("sql")!;
        var cmdParams = parameters.Get<Dictionary<string, object>>("parameters");
        var timeout = parameters.GetInt("timeout", 30);

        using var connection = _connectionFactory!();
        await connection.OpenAsync(ct);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = timeout;
        AddParameters(cmd, cmdParams);

        var affectedRows = await cmd.ExecuteNonQueryAsync(ct);
        return ActionResult.Ok(new { affectedRows });
    }

    private async Task<ActionResult> ExecuteScalarAsync(ActionParameters parameters, CancellationToken ct)
    {
        var sql = parameters.GetString("sql")!;
        var queryParams = parameters.Get<Dictionary<string, object>>("parameters");

        using var connection = _connectionFactory!();
        await connection.OpenAsync(ct);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameters(cmd, queryParams);

        var result = await cmd.ExecuteScalarAsync(ct);
        return ActionResult.Ok(new { value = result });
    }

    private async Task<ActionResult> ExecuteTransactionAsync(ActionParameters parameters, CancellationToken ct)
    {
        var commands = parameters.Get<JsonElement>("commands");

        if (commands.ValueKind != JsonValueKind.Array)
        {
            return ActionResult.Fail("Commands must be an array", "INVALID_PARAMETER");
        }

        using var connection = _connectionFactory!();
        await connection.OpenAsync(ct);

        using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            var results = new List<int>();

            foreach (var cmdDef in commands.EnumerateArray())
            {
                var sql = cmdDef.GetProperty("sql").GetString()!;
                Dictionary<string, object>? cmdParams = null;

                if (cmdDef.TryGetProperty("parameters", out var paramsElement))
                {
                    cmdParams = JsonSerializer.Deserialize<Dictionary<string, object>>(paramsElement.GetRawText());
                }

                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = sql;
                AddParameters(cmd, cmdParams);

                var affected = await cmd.ExecuteNonQueryAsync(ct);
                results.Add(affected);
            }

            await transaction.CommitAsync(ct);

            return ActionResult.Ok(new
            {
                committed = true,
                commandResults = results,
                totalAffected = results.Sum()
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return ActionResult.Fail($"Transaction rolled back: {ex.Message}", "TRANSACTION_FAILED");
        }
    }

    private async Task<ActionResult> BulkInsertAsync(ActionParameters parameters, CancellationToken ct)
    {
        var table = parameters.GetString("table")!;
        var columns = parameters.Get<List<string>>("columns")!;
        var rows = parameters.Get<JsonElement>("rows");
        var batchSize = parameters.GetInt("batchSize", 1000);

        if (rows.ValueKind != JsonValueKind.Array)
        {
            return ActionResult.Fail("Rows must be an array", "INVALID_PARAMETER");
        }

        var rowList = rows.EnumerateArray().ToList();
        var totalInserted = 0;

        using var connection = _connectionFactory!();
        await connection.OpenAsync(ct);

        using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            // MySQL supports multi-row INSERT
            for (int i = 0; i < rowList.Count; i += batchSize)
            {
                var batch = rowList.Skip(i).Take(batchSize).ToList();
                var inserted = await InsertBatchAsync(connection, transaction, table, columns, batch, ct);
                totalInserted += inserted;
            }

            await transaction.CommitAsync(ct);
            return ActionResult.Ok(new { insertedRows = totalInserted });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return ActionResult.Fail($"Bulk insert failed: {ex.Message}", "BULK_INSERT_FAILED");
        }
    }

    private async Task<int> InsertBatchAsync(
        DbConnection connection,
        DbTransaction transaction,
        string table,
        List<string> columns,
        List<JsonElement> rows,
        CancellationToken ct)
    {
        var columnList = string.Join(", ", columns.Select(c => $"`{c}`"));
        var paramCount = 0;

        var valueClauses = new List<string>();
        var allParams = new Dictionary<string, object?>();

        foreach (var row in rows)
        {
            var valueParams = new List<string>();
            var rowArray = row.EnumerateArray().ToList();

            for (int i = 0; i < columns.Count && i < rowArray.Count; i++)
            {
                var paramName = $"p{paramCount++}";
                valueParams.Add($"@{paramName}");
                allParams[paramName] = GetJsonValue(rowArray[i]);
            }

            valueClauses.Add($"({string.Join(", ", valueParams)})");
        }

        var sql = $"INSERT INTO `{table}` ({columnList}) VALUES {string.Join(", ", valueClauses)}";

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        AddParameters(cmd, allParams);

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<ActionResult> GetTablesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var database = parameters.GetString("database");

        var sql = database != null
            ? $"SHOW TABLES FROM `{database}`"
            : "SHOW TABLES";

        using var connection = _connectionFactory!();
        await connection.OpenAsync(ct);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        var tables = new List<object>();
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            tables.Add(new { name = reader.GetString(0) });
        }

        return ActionResult.Ok(new { tables });
    }

    private async Task<ActionResult> GetColumnsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var table = parameters.GetString("table")!;

        var sql = $"DESCRIBE `{table}`";

        using var connection = _connectionFactory!();
        await connection.OpenAsync(ct);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        var columns = new List<object>();
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            columns.Add(new
            {
                name = reader.GetString(0),
                dataType = reader.GetString(1),
                nullable = reader.GetString(2) == "YES",
                key = reader.IsDBNull(3) ? null : reader.GetString(3),
                defaultValue = reader.IsDBNull(4) ? null : reader.GetValue(4)
            });
        }

        return ActionResult.Ok(new { columns });
    }

    private static string BuildConnectionString(ConnectorConfiguration config)
    {
        var connectionString = config.GetCredentialString("connectionString");

        if (!string.IsNullOrEmpty(connectionString))
        {
            return connectionString;
        }

        var host = config.GetCredentialString("host") ?? "localhost";
        var port = config.GetCredential<int?>("port") ?? 3306;
        var database = config.GetCredentialString("database") ?? "mysql";
        var username = config.GetCredentialString("username") ?? "root";
        var password = config.GetCredentialString("password") ?? "";
        var sslMode = config.GetSettingString("sslMode") ?? "Preferred";

        return $"Server={host};Port={port};Database={database};User={username};Password={password};SslMode={sslMode}";
    }

    private static DbConnection CreateConnection(string connectionString)
    {
        // Uses MySqlConnector library in production
        return new GenericDbConnection(connectionString, "MySql");
    }

    private static void AddParameters(DbCommand cmd, Dictionary<string, object?>? parameters)
    {
        if (parameters == null) return;

        foreach (var kvp in parameters)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = kvp.Key.StartsWith("@") ? kvp.Key : "@" + kvp.Key;
            param.Value = kvp.Value ?? DBNull.Value;
            cmd.Parameters.Add(param);
        }
    }

    private static object? GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }
}
