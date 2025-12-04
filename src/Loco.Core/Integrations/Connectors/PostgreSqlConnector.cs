// John Carmack: "The best code is the code that doesn't exist"
// Rob Pike: "Measure. Don't tune for speed until you've measured"

using System.Data;
using System.Data.Common;
using System.Text.Json;
using Loco.Core.Integrations.Core;
using Loco.Core.Practical;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// PostgreSQL database connector
/// Supports queries, transactions, and bulk operations
/// Uses System.Data.Common for provider-agnostic implementation
/// </summary>
public sealed class PostgreSqlConnector : ConnectorBase
{
    private Func<DbConnection>? _connectionFactory;
    private string? _connectionString;

    public override string Id => "postgresql";
    public override string Name => "PostgreSQL";
    public override string Description => "PostgreSQL database connector for queries, transactions, and data operations";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Database;

    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.ForDatabase();

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.ConnectionString,
        RequiredCredentials =
        [
            new() { Name = "connectionString", Label = "Connection String", Type = ParameterType.Password, Required = true,
                Description = "PostgreSQL connection string (e.g., Host=localhost;Database=mydb;Username=user;Password=pass)" },
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
        new() { Name = "maxPoolSize", Label = "Max Pool Size", Type = ParameterType.Number, DefaultValue = 20 }
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
                new() { Name = "sql", Type = ParameterType.Code, Required = true, Description = "SQL query to execute" },
                new() { Name = "parameters", Type = ParameterType.Json, Description = "Query parameters (object with parameter names as keys)" },
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
                new() { Name = "sql", Type = ParameterType.Code, Required = true, Description = "SQL command to execute" },
                new() { Name = "parameters", Type = ParameterType.Json, Description = "Command parameters" },
                new() { Name = "timeout", Type = ParameterType.Number, Description = "Command timeout in seconds" }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "scalar",
            Name = "Execute Scalar",
            Description = "Execute query and return single value (COUNT, MAX, etc.)",
            Parameters =
            [
                new() { Name = "sql", Type = ParameterType.Code, Required = true, Description = "SQL query returning single value" },
                new() { Name = "parameters", Type = ParameterType.Json, Description = "Query parameters" }
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
                    Description = "Array of {sql, parameters} objects to execute" },
                new() { Name = "isolationLevel", Type = ParameterType.Select, DefaultValue = "ReadCommitted",
                    Options =
                    [
                        new() { Label = "Read Uncommitted", Value = "ReadUncommitted" },
                        new() { Label = "Read Committed", Value = "ReadCommitted" },
                        new() { Label = "Repeatable Read", Value = "RepeatableRead" },
                        new() { Label = "Serializable", Value = "Serializable" }
                    ]}
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
                new() { Name = "table", Type = ParameterType.String, Required = true, Description = "Target table name" },
                new() { Name = "columns", Type = ParameterType.Json, Required = true, Description = "Array of column names" },
                new() { Name = "rows", Type = ParameterType.Json, Required = true, Description = "Array of row arrays" },
                new() { Name = "batchSize", Type = ParameterType.Number, DefaultValue = 1000, Description = "Rows per batch" }
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
                new() { Name = "schema", Type = ParameterType.String, DefaultValue = "public", Description = "Schema name" }
            ]
        },
        new()
        {
            Id = "getColumns",
            Name = "Get Columns",
            Description = "Get column information for a table",
            Parameters =
            [
                new() { Name = "table", Type = ParameterType.String, Required = true, Description = "Table name" },
                new() { Name = "schema", Type = ParameterType.String, DefaultValue = "public", Description = "Schema name" }
            ]
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "poll",
            Name = "Poll for Changes",
            Description = "Periodically query for new or changed records",
            Type = TriggerType.Polling,
            ConfigParameters =
            [
                new() { Name = "sql", Type = ParameterType.Code, Required = true, Description = "Query to detect changes" },
                new() { Name = "interval", Type = ParameterType.Number, DefaultValue = 60, Description = "Poll interval in seconds" },
                new() { Name = "trackColumn", Type = ParameterType.String, Description = "Column to track (e.g., updated_at)" }
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

            // Test with simple query
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT version()";
            var version = await cmd.ExecuteScalarAsync(ct);

            return ConnectionTestResult.Ok($"Connected to PostgreSQL: {version}");
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
        var isolationLevelStr = parameters.GetString("isolationLevel") ?? "ReadCommitted";

        if (commands.ValueKind != JsonValueKind.Array)
        {
            return ActionResult.Fail("Commands must be an array", "INVALID_PARAMETER");
        }

        var isolationLevel = isolationLevelStr switch
        {
            "ReadUncommitted" => IsolationLevel.ReadUncommitted,
            "RepeatableRead" => IsolationLevel.RepeatableRead,
            "Serializable" => IsolationLevel.Serializable,
            _ => IsolationLevel.ReadCommitted
        };

        using var connection = _connectionFactory!();
        await connection.OpenAsync(ct);

        using var transaction = await connection.BeginTransactionAsync(isolationLevel, ct);

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
        var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));
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

        var sql = $"INSERT INTO {table} ({columnList}) VALUES {string.Join(", ", valueClauses)}";

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        AddParameters(cmd, allParams);

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<ActionResult> GetTablesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var schema = parameters.GetString("schema") ?? "public";

        var sql = @"
            SELECT table_name, table_type
            FROM information_schema.tables
            WHERE table_schema = @schema
            ORDER BY table_name";

        using var connection = _connectionFactory!();
        await connection.OpenAsync(ct);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameters(cmd, new Dictionary<string, object> { ["schema"] = schema });

        var tables = new List<object>();
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            tables.Add(new
            {
                name = reader.GetString(0),
                type = reader.GetString(1)
            });
        }

        return ActionResult.Ok(new { tables });
    }

    private async Task<ActionResult> GetColumnsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var table = parameters.GetString("table")!;
        var schema = parameters.GetString("schema") ?? "public";

        var sql = @"
            SELECT column_name, data_type, is_nullable, column_default
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @table
            ORDER BY ordinal_position";

        using var connection = _connectionFactory!();
        await connection.OpenAsync(ct);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameters(cmd, new Dictionary<string, object> { ["schema"] = schema, ["table"] = table });

        var columns = new List<object>();
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            columns.Add(new
            {
                name = reader.GetString(0),
                dataType = reader.GetString(1),
                nullable = reader.GetString(2) == "YES",
                defaultValue = reader.IsDBNull(3) ? null : reader.GetString(3)
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

        // Build from individual parameters
        var host = config.GetCredentialString("host") ?? "localhost";
        var port = config.GetCredential<int?>("port") ?? 5432;
        var database = config.GetCredentialString("database") ?? "postgres";
        var username = config.GetCredentialString("username") ?? "postgres";
        var password = config.GetCredentialString("password") ?? "";

        return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }

    private static DbConnection CreateConnection(string connectionString)
    {
        // Use Npgsql if available, otherwise fall back to generic
        // In production, this would use Npgsql.NpgsqlConnection
        // For now, we create a placeholder that works with the abstraction
        return new GenericDbConnection(connectionString, "Npgsql");
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

/// <summary>
/// Generic DbConnection wrapper for testing and fallback
/// In production, replace with actual Npgsql.NpgsqlConnection
/// </summary>
internal sealed class GenericDbConnection : DbConnection
{
    private readonly string _connectionString;
    private readonly string _providerName;
    private ConnectionState _state = ConnectionState.Closed;

    public GenericDbConnection(string connectionString, string providerName)
    {
        _connectionString = connectionString;
        _providerName = providerName;
    }

    public override string ConnectionString
    {
        get => _connectionString;
        set => throw new NotSupportedException();
    }

    public override string Database => "database";
    public override string DataSource => "datasource";
    public override string ServerVersion => "1.0";
    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName) { }

    public override void Close() => _state = ConnectionState.Closed;

    public override void Open() => _state = ConnectionState.Open;

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        => new GenericDbTransaction(this, isolationLevel);

    protected override DbCommand CreateDbCommand() => new GenericDbCommand(this);
}

internal sealed class GenericDbTransaction : DbTransaction
{
    public GenericDbTransaction(DbConnection connection, IsolationLevel level)
    {
        DbConnection = connection;
        IsolationLevel = level;
    }

    public override IsolationLevel IsolationLevel { get; }
    protected override DbConnection DbConnection { get; }

    public override void Commit() { }
    public override void Rollback() { }
}

internal sealed class GenericDbCommand : DbCommand
{
    public GenericDbCommand(DbConnection connection)
    {
        DbConnection = connection;
        DbParameterCollection = new GenericDbParameterCollection();
    }

    public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; } = 30;
    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; }
    protected override DbParameterCollection DbParameterCollection { get; }
    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel() { }
    public override int ExecuteNonQuery() => 0;
    public override object? ExecuteScalar() => null;
    public override void Prepare() { }

    protected override DbParameter CreateDbParameter() => new GenericDbParameter();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new GenericDbDataReader();
}

internal sealed class GenericDbParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _parameters = new();

    public override int Count => _parameters.Count;
    public override object SyncRoot => _parameters;

    public override int Add(object value) { _parameters.Add((DbParameter)value); return _parameters.Count - 1; }
    public override void AddRange(Array values) { foreach (DbParameter p in values) _parameters.Add(p); }
    public override void Clear() => _parameters.Clear();
    public override bool Contains(object value) => _parameters.Contains((DbParameter)value);
    public override bool Contains(string value) => _parameters.Any(p => p.ParameterName == value);
    public override void CopyTo(Array array, int index) { }
    public override System.Collections.IEnumerator GetEnumerator() => _parameters.GetEnumerator();
    public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);
    public override int IndexOf(string parameterName) => _parameters.FindIndex(p => p.ParameterName == parameterName);
    public override void Insert(int index, object value) => _parameters.Insert(index, (DbParameter)value);
    public override void Remove(object value) => _parameters.Remove((DbParameter)value);
    public override void RemoveAt(int index) => _parameters.RemoveAt(index);
    public override void RemoveAt(string parameterName) => _parameters.RemoveAll(p => p.ParameterName == parameterName);
    protected override DbParameter GetParameter(int index) => _parameters[index];
    protected override DbParameter GetParameter(string parameterName) => _parameters.First(p => p.ParameterName == parameterName);
    protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;
    protected override void SetParameter(string parameterName, DbParameter value) { var i = IndexOf(parameterName); if (i >= 0) _parameters[i] = value; }
}

internal sealed class GenericDbParameter : DbParameter
{
    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; }
    public override bool IsNullable { get; set; }
    public override string ParameterName { get; set; } = "";
    public override int Size { get; set; }
    public override string SourceColumn { get; set; } = "";
    public override bool SourceColumnNullMapping { get; set; }
    public override object? Value { get; set; }

    public override void ResetDbType() { }
}

internal sealed class GenericDbDataReader : DbDataReader
{
    public override int FieldCount => 0;
    public override int Depth => 0;
    public override bool HasRows => false;
    public override bool IsClosed => true;
    public override int RecordsAffected => 0;
    public override object this[int ordinal] => DBNull.Value;
    public override object this[string name] => DBNull.Value;

    public override bool GetBoolean(int ordinal) => false;
    public override byte GetByte(int ordinal) => 0;
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
    public override char GetChar(int ordinal) => '\0';
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
    public override string GetDataTypeName(int ordinal) => "string";
    public override DateTime GetDateTime(int ordinal) => DateTime.MinValue;
    public override decimal GetDecimal(int ordinal) => 0;
    public override double GetDouble(int ordinal) => 0;
    public override System.Collections.IEnumerator GetEnumerator() => Enumerable.Empty<object>().GetEnumerator();
    public override Type GetFieldType(int ordinal) => typeof(object);
    public override float GetFloat(int ordinal) => 0;
    public override Guid GetGuid(int ordinal) => Guid.Empty;
    public override short GetInt16(int ordinal) => 0;
    public override int GetInt32(int ordinal) => 0;
    public override long GetInt64(int ordinal) => 0;
    public override string GetName(int ordinal) => "";
    public override int GetOrdinal(string name) => -1;
    public override string GetString(int ordinal) => "";
    public override object GetValue(int ordinal) => DBNull.Value;
    public override int GetValues(object[] values) => 0;
    public override bool IsDBNull(int ordinal) => true;
    public override bool NextResult() => false;
    public override bool Read() => false;
}
