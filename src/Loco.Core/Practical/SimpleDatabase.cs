// John Carmack: "Abstraction without reason is complexity"
// Rob Pike: "The best code is no code. The second best is simple code."

using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text;

namespace Loco.Core.Practical;

/// <summary>
/// Simple database - Lightweight data access without ORM overhead
/// Direct SQL, parameter binding, transaction support
/// </summary>
public class SimpleDatabase
{
    private readonly Func<DbConnection> _connectionFactory;
    private readonly SimpleLogger _logger;

    public SimpleDatabase(Func<DbConnection> connectionFactory, SimpleLogger? logger = null)
    {
        _connectionFactory = connectionFactory;
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleDatabase));
    }

    // Execute query and return rows
    public async Task<List<Dictionary<string, object?>>> QueryAsync(
        string sql,
        object? parameters = null)
    {
        using var conn = _connectionFactory();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParameters(cmd, parameters);

        var results = new List<Dictionary<string, object?>>();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }

        _logger.Debug($"Query returned {results.Count} rows");
        return results;
    }

    // Query and map to typed objects
    public async Task<List<T>> QueryAsync<T>(string sql, object? parameters = null) where T : new()
    {
        var rows = await QueryAsync(sql, parameters);
        return rows.Select(row => MapToObject<T>(row)).ToList();
    }

    // Query single row
    public async Task<Dictionary<string, object?>?> QuerySingleAsync(
        string sql,
        object? parameters = null)
    {
        var results = await QueryAsync(sql, parameters);
        return results.FirstOrDefault();
    }

    // Query single typed object
    public async Task<T?> QuerySingleAsync<T>(string sql, object? parameters = null) where T : new()
    {
        var row = await QuerySingleAsync(sql, parameters);
        return row != null ? MapToObject<T>(row) : default;
    }

    // Execute non-query (INSERT, UPDATE, DELETE)
    public async Task<int> ExecuteAsync(string sql, object? parameters = null)
    {
        using var conn = _connectionFactory();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParameters(cmd, parameters);

        var affected = await cmd.ExecuteNonQueryAsync();
        _logger.Debug($"Execute affected {affected} rows");
        return affected;
    }

    // Execute scalar (COUNT, MAX, etc.)
    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null)
    {
        using var conn = _connectionFactory();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParameters(cmd, parameters);

        var result = await cmd.ExecuteScalarAsync();
        return result != null ? (T)Convert.ChangeType(result, typeof(T)) : default;
    }

    // Transaction support
    public async Task<T> TransactionAsync<T>(Func<SimpleTransaction, Task<T>> action)
    {
        using var conn = _connectionFactory();
        await conn.OpenAsync();

        using var transaction = await conn.BeginTransactionAsync();

        try
        {
            var tx = new SimpleTransaction(conn, transaction, _logger);
            var result = await action(tx);
            await transaction.CommitAsync();
            _logger.Debug("Transaction committed");
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            _logger.Warning("Transaction rolled back");
            throw;
        }
    }

    private void AddParameters(DbCommand cmd, object? parameters)
    {
        if (parameters == null) return;

        if (parameters is IDictionary<string, object> dict)
        {
            foreach (var kvp in dict)
            {
                var param = cmd.CreateParameter();
                param.ParameterName = kvp.Key.StartsWith("@") ? kvp.Key : "@" + kvp.Key;
                param.Value = kvp.Value ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
        }
        else
        {
            var props = parameters.GetType().GetProperties();
            foreach (var prop in props)
            {
                var param = cmd.CreateParameter();
                param.ParameterName = "@" + prop.Name;
                param.Value = prop.GetValue(parameters) ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
        }
    }

    private T MapToObject<T>(Dictionary<string, object?> row) where T : new()
    {
        var obj = new T();
        var props = typeof(T).GetProperties()
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in row)
        {
            if (props.TryGetValue(kvp.Key, out var prop) && kvp.Value != null)
            {
                try
                {
                    var value = Convert.ChangeType(kvp.Value, prop.PropertyType);
                    prop.SetValue(obj, value);
                }
                catch { }
            }
        }

        return obj;
    }
}

/// <summary>
/// Transaction wrapper
/// </summary>
public class SimpleTransaction
{
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private readonly SimpleLogger _logger;

    internal SimpleTransaction(DbConnection connection, DbTransaction transaction, SimpleLogger logger)
    {
        _connection = connection;
        _transaction = transaction;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(string sql, object? parameters = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = _transaction;
        cmd.CommandText = sql;
        AddParameters(cmd, parameters);
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<T>> QueryAsync<T>(string sql, object? parameters = null) where T : new()
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = _transaction;
        cmd.CommandText = sql;
        AddParameters(cmd, parameters);

        var results = new List<T>();
        using var reader = await cmd.ExecuteReaderAsync();

        var props = typeof(T).GetProperties()
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync())
        {
            var obj = new T();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                if (props.TryGetValue(name, out var prop) && !reader.IsDBNull(i))
                {
                    try
                    {
                        var value = Convert.ChangeType(reader.GetValue(i), prop.PropertyType);
                        prop.SetValue(obj, value);
                    }
                    catch { }
                }
            }
            results.Add(obj);
        }

        return results;
    }

    private void AddParameters(DbCommand cmd, object? parameters)
    {
        if (parameters == null) return;

        var props = parameters.GetType().GetProperties();
        foreach (var prop in props)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = "@" + prop.Name;
            param.Value = prop.GetValue(parameters) ?? DBNull.Value;
            cmd.Parameters.Add(param);
        }
    }
}

/// <summary>
/// Query builder
/// </summary>
public class QueryBuilder
{
    private readonly StringBuilder _sql = new();
    private readonly Dictionary<string, object> _parameters = new();

    public QueryBuilder Select(params string[] columns)
    {
        _sql.Append("SELECT ").Append(string.Join(", ", columns));
        return this;
    }

    public QueryBuilder From(string table)
    {
        _sql.Append(" FROM ").Append(table);
        return this;
    }

    public QueryBuilder Where(string condition, object? value = null)
    {
        if (_sql.ToString().Contains(" WHERE "))
            _sql.Append(" AND ");
        else
            _sql.Append(" WHERE ");

        _sql.Append(condition);

        if (value != null)
        {
            var paramName = $"p{_parameters.Count}";
            _parameters[paramName] = value;
        }

        return this;
    }

    public QueryBuilder OrderBy(string column, bool descending = false)
    {
        _sql.Append(" ORDER BY ").Append(column);
        if (descending) _sql.Append(" DESC");
        return this;
    }

    public QueryBuilder Limit(int count)
    {
        _sql.Append(" LIMIT ").Append(count);
        return this;
    }

    public (string sql, Dictionary<string, object> parameters) Build()
    {
        return (_sql.ToString(), _parameters);
    }

    public override string ToString() => _sql.ToString();
}

/// <summary>
/// In-memory database (for testing)
/// </summary>
public class InMemoryDatabase
{
    private readonly ConcurrentDictionary<string, List<Dictionary<string, object?>>> _tables = new();

    public void CreateTable(string name)
    {
        _tables[name] = new List<Dictionary<string, object?>>();
    }

    public void Insert(string table, Dictionary<string, object?> row)
    {
        if (_tables.TryGetValue(table, out var rows))
        {
            rows.Add(new Dictionary<string, object?>(row));
        }
    }

    public List<Dictionary<string, object?>> Select(string table, Func<Dictionary<string, object?>, bool>? predicate = null)
    {
        if (_tables.TryGetValue(table, out var rows))
        {
            return predicate != null ? rows.Where(predicate).ToList() : rows.ToList();
        }
        return new List<Dictionary<string, object?>>();
    }

    public int Update(string table, Func<Dictionary<string, object?>, bool> predicate, Dictionary<string, object?> values)
    {
        if (!_tables.TryGetValue(table, out var rows)) return 0;

        var count = 0;
        foreach (var row in rows.Where(predicate))
        {
            foreach (var kvp in values)
            {
                row[kvp.Key] = kvp.Value;
            }
            count++;
        }
        return count;
    }

    public int Delete(string table, Func<Dictionary<string, object?>, bool> predicate)
    {
        if (!_tables.TryGetValue(table, out var rows)) return 0;

        var toRemove = rows.Where(predicate).ToList();
        foreach (var row in toRemove)
        {
            rows.Remove(row);
        }
        return toRemove.Count;
    }
}

/// <summary>
/// Example models
/// </summary>
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

/// <summary>
/// Example usage
/// </summary>
public class DatabaseExamples
{
    public static async Task Examples()
    {
        // Note: This example uses System.Data.SQLite or similar
        // var db = new SimpleDatabase(() => new SqliteConnection("Data Source=app.db"));

        // Query users
        // var users = await db.QueryAsync<User>("SELECT * FROM users WHERE active = @active", new { active = true });

        // Query single user
        // var user = await db.QuerySingleAsync<User>("SELECT * FROM users WHERE id = @id", new { id = 1 });

        // Insert
        // await db.ExecuteAsync("INSERT INTO users (name, email) VALUES (@name, @email)",
        //     new { name = "John", email = "john@example.com" });

        // Update
        // await db.ExecuteAsync("UPDATE users SET name = @name WHERE id = @id",
        //     new { id = 1, name = "Jane" });

        // Delete
        // await db.ExecuteAsync("DELETE FROM users WHERE id = @id", new { id = 1 });

        // Transaction
        // await db.TransactionAsync(async tx =>
        // {
        //     await tx.ExecuteAsync("INSERT INTO users (name) VALUES (@name)", new { name = "Alice" });
        //     await tx.ExecuteAsync("INSERT INTO users (name) VALUES (@name)", new { name = "Bob" });
        //     return true;
        // });

        // Query builder
        var qb = new QueryBuilder()
            .Select("id", "name", "email")
            .From("users")
            .Where("active = @active", true)
            .OrderBy("name")
            .Limit(10);

        var (sql, parameters) = qb.Build();
        // var results = await db.QueryAsync<User>(sql, parameters);

        // In-memory database
        var memDb = new InMemoryDatabase();
        memDb.CreateTable("users");
        memDb.Insert("users", new Dictionary<string, object?> { ["id"] = 1, ["name"] = "John" });
        var rows = memDb.Select("users");
    }
}