// John Carmack: "Low-level thinking leads to high-level success"
// Rob Pike: "Caches are almost always a mistake"

using System.Net.Sockets;
using System.Text;
using Loco.Core.Integrations.Core;
using Loco.Core.Practical;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Redis connector for caching, pub/sub, and data structures
/// Implements RESP3 protocol directly for zero dependencies
/// </summary>
public sealed class RedisConnector : ConnectorBase
{
    private string _host = "localhost";
    private int _port = 6379;
    private string? _password;
    private int _database = 0;

    public override string Id => "redis";
    public override string Name => "Redis";
    public override string Description => "Redis in-memory data store for caching, pub/sub, and data structures";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Database;

    public override ConnectorCapabilities Capabilities => new()
    {
        SupportsActions = true,
        SupportsTriggers = true,
        SupportsStreaming = true,
        RateLimitPerMinute = 10000,
        DefaultTimeout = TimeSpan.FromSeconds(5)
    };

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.Custom,
        RequiredCredentials =
        [
            new() { Name = "host", Label = "Host", Type = ParameterType.String, Required = true },
            new() { Name = "port", Label = "Port", Type = ParameterType.Number, Required = false },
            new() { Name = "password", Label = "Password", Type = ParameterType.Password, Required = false },
            new() { Name = "database", Label = "Database Index", Type = ParameterType.Number, Required = false }
        ]
    };

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        // String operations
        new()
        {
            Id = "get",
            Name = "Get Value",
            Description = "Get the value of a key",
            Parameters =
            [
                new() { Name = "key", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "set",
            Name = "Set Value",
            Description = "Set the value of a key",
            Parameters =
            [
                new() { Name = "key", Type = ParameterType.String, Required = true },
                new() { Name = "value", Type = ParameterType.String, Required = true },
                new() { Name = "expirySeconds", Type = ParameterType.Number, Description = "TTL in seconds" },
                new() { Name = "nx", Type = ParameterType.Boolean, DefaultValue = false, Description = "Only set if not exists" },
                new() { Name = "xx", Type = ParameterType.Boolean, DefaultValue = false, Description = "Only set if exists" }
            ]
        },
        new()
        {
            Id = "delete",
            Name = "Delete Key",
            Description = "Delete one or more keys",
            Parameters =
            [
                new() { Name = "keys", Type = ParameterType.String, Required = true, Description = "Comma-separated keys" }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "exists",
            Name = "Key Exists",
            Description = "Check if a key exists",
            Parameters =
            [
                new() { Name = "key", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "expire",
            Name = "Set Expiry",
            Description = "Set a timeout on a key",
            Parameters =
            [
                new() { Name = "key", Type = ParameterType.String, Required = true },
                new() { Name = "seconds", Type = ParameterType.Number, Required = true }
            ]
        },
        new()
        {
            Id = "ttl",
            Name = "Get TTL",
            Description = "Get the remaining time to live of a key",
            Parameters =
            [
                new() { Name = "key", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "incr",
            Name = "Increment",
            Description = "Increment the integer value of a key",
            Parameters =
            [
                new() { Name = "key", Type = ParameterType.String, Required = true },
                new() { Name = "by", Type = ParameterType.Number, DefaultValue = 1 }
            ]
        },
        // Hash operations
        new()
        {
            Id = "hget",
            Name = "Hash Get",
            Description = "Get the value of a hash field",
            Parameters =
            [
                new() { Name = "key", Type = ParameterType.String, Required = true },
                new() { Name = "field", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "hset",
            Name = "Hash Set",
            Description = "Set hash field(s)",
            Parameters =
            [
                new() { Name = "key", Type = ParameterType.String, Required = true },
                new() { Name = "fields", Type = ParameterType.Json, Required = true, Description = "Object with field-value pairs" }
            ]
        },
        new()
        {
            Id = "hgetall",
            Name = "Hash Get All",
            Description = "Get all fields and values of a hash",
            Parameters =
            [
                new() { Name = "key", Type = ParameterType.String, Required = true }
            ]
        },
        // List operations
        new()
        {
            Id = "lpush",
            Name = "List Push Left",
            Description = "Push values to the head of a list",
            Parameters =
            [
                new() { Name = "key", Type = ParameterType.String, Required = true },
                new() { Name = "values", Type = ParameterType.Json, Required = true, Description = "Array of values" }
            ]
        },
        new()
        {
            Id = "rpush",
            Name = "List Push Right",
            Description = "Push values to the tail of a list",
            Parameters =
            [
                new() { Name = "key", Type = ParameterType.String, Required = true },
                new() { Name = "values", Type = ParameterType.Json, Required = true }
            ]
        },
        new()
        {
            Id = "lrange",
            Name = "List Range",
            Description = "Get a range of elements from a list",
            Parameters =
            [
                new() { Name = "key", Type = ParameterType.String, Required = true },
                new() { Name = "start", Type = ParameterType.Number, DefaultValue = 0 },
                new() { Name = "stop", Type = ParameterType.Number, DefaultValue = -1 }
            ]
        },
        // Pub/Sub
        new()
        {
            Id = "publish",
            Name = "Publish Message",
            Description = "Publish a message to a channel",
            Parameters =
            [
                new() { Name = "channel", Type = ParameterType.String, Required = true },
                new() { Name = "message", Type = ParameterType.String, Required = true }
            ]
        },
        // Utility
        new()
        {
            Id = "keys",
            Name = "Find Keys",
            Description = "Find all keys matching a pattern",
            Parameters =
            [
                new() { Name = "pattern", Type = ParameterType.String, Required = true, Description = "Pattern like 'user:*'" }
            ]
        },
        new()
        {
            Id = "info",
            Name = "Server Info",
            Description = "Get server information",
            Parameters =
            [
                new() { Name = "section", Type = ParameterType.String, Description = "Info section (server, clients, memory, etc.)" }
            ]
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "subscribe",
            Name = "Subscribe",
            Description = "Subscribe to pub/sub channel",
            Type = TriggerType.Stream,
            ConfigParameters =
            [
                new() { Name = "channel", Type = ParameterType.String, Required = true },
                new() { Name = "pattern", Type = ParameterType.Boolean, DefaultValue = false, Description = "Use pattern matching" }
            ]
        },
        new()
        {
            Id = "keyspace",
            Name = "Keyspace Notifications",
            Description = "Listen for key events",
            Type = TriggerType.Stream,
            ConfigParameters =
            [
                new() { Name = "pattern", Type = ParameterType.String, Required = true, Description = "Key pattern" },
                new() { Name = "events", Type = ParameterType.String, DefaultValue = "KEA", Description = "Event types (K=keyspace, E=keyevent, A=all)" }
            ]
        }
    ];

    public override async Task<ConnectionTestResult> TestConnectionAsync(
        ConnectorConfiguration config,
        CancellationToken ct = default)
    {
        try
        {
            InitializeFromConfig(config);

            var result = await ExecuteCommandAsync("PING", ct);
            if (result == "PONG")
            {
                return ConnectionTestResult.Ok($"Connected to Redis at {_host}:{_port}");
            }

            return ConnectionTestResult.Fail($"Unexpected response: {result}");
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Fail("Connection failed", ex);
        }
    }

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        InitializeFromConfig(config);
        await base.InitializeAsync(config, ct);
    }

    private void InitializeFromConfig(ConnectorConfiguration config)
    {
        _host = config.GetCredentialString("host") ?? "localhost";
        _port = config.GetCredential<int?>("port") ?? 6379;
        _password = config.GetCredentialString("password");
        _database = config.GetCredential<int?>("database") ?? 0;
    }

    protected override async Task<ActionResult> ExecuteActionCoreAsync(
        ConnectorAction action,
        ActionParameters parameters,
        Core.ExecutionContext context,
        CancellationToken ct)
    {
        return action.Id switch
        {
            "get" => await GetAsync(parameters, ct),
            "set" => await SetAsync(parameters, ct),
            "delete" => await DeleteAsync(parameters, ct),
            "exists" => await ExistsAsync(parameters, ct),
            "expire" => await ExpireAsync(parameters, ct),
            "ttl" => await TtlAsync(parameters, ct),
            "incr" => await IncrAsync(parameters, ct),
            "hget" => await HGetAsync(parameters, ct),
            "hset" => await HSetAsync(parameters, ct),
            "hgetall" => await HGetAllAsync(parameters, ct),
            "lpush" => await LPushAsync(parameters, ct),
            "rpush" => await RPushAsync(parameters, ct),
            "lrange" => await LRangeAsync(parameters, ct),
            "publish" => await PublishAsync(parameters, ct),
            "keys" => await KeysAsync(parameters, ct),
            "info" => await InfoAsync(parameters, ct),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> GetAsync(ActionParameters parameters, CancellationToken ct)
    {
        var key = parameters.GetString("key")!;
        var result = await ExecuteCommandAsync($"GET {key}", ct);
        return ActionResult.Ok(new { key, value = result });
    }

    private async Task<ActionResult> SetAsync(ActionParameters parameters, CancellationToken ct)
    {
        var key = parameters.GetString("key")!;
        var value = parameters.GetString("value")!;
        var expiry = parameters.GetInt("expirySeconds", 0);
        var nx = parameters.GetBool("nx", false);
        var xx = parameters.GetBool("xx", false);

        var cmd = $"SET {key} {EscapeValue(value)}";
        if (expiry > 0) cmd += $" EX {expiry}";
        if (nx) cmd += " NX";
        if (xx) cmd += " XX";

        var result = await ExecuteCommandAsync(cmd, ct);
        return ActionResult.Ok(new { key, success = result == "OK" || result != null });
    }

    private async Task<ActionResult> DeleteAsync(ActionParameters parameters, CancellationToken ct)
    {
        var keys = parameters.GetString("keys")!
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var cmd = $"DEL {string.Join(" ", keys)}";
        var result = await ExecuteCommandAsync(cmd, ct);

        return ActionResult.Ok(new { deletedCount = int.TryParse(result, out var count) ? count : 0 });
    }

    private async Task<ActionResult> ExistsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var key = parameters.GetString("key")!;
        var result = await ExecuteCommandAsync($"EXISTS {key}", ct);
        return ActionResult.Ok(new { key, exists = result == "1" });
    }

    private async Task<ActionResult> ExpireAsync(ActionParameters parameters, CancellationToken ct)
    {
        var key = parameters.GetString("key")!;
        var seconds = parameters.GetInt("seconds", 0);
        var result = await ExecuteCommandAsync($"EXPIRE {key} {seconds}", ct);
        return ActionResult.Ok(new { key, success = result == "1" });
    }

    private async Task<ActionResult> TtlAsync(ActionParameters parameters, CancellationToken ct)
    {
        var key = parameters.GetString("key")!;
        var result = await ExecuteCommandAsync($"TTL {key}", ct);
        var ttl = int.TryParse(result, out var t) ? t : -1;
        return ActionResult.Ok(new { key, ttl, exists = ttl >= -1 });
    }

    private async Task<ActionResult> IncrAsync(ActionParameters parameters, CancellationToken ct)
    {
        var key = parameters.GetString("key")!;
        var by = parameters.GetInt("by", 1);

        var cmd = by == 1 ? $"INCR {key}" : $"INCRBY {key} {by}";
        var result = await ExecuteCommandAsync(cmd, ct);

        return ActionResult.Ok(new { key, value = long.TryParse(result, out var v) ? v : 0 });
    }

    private async Task<ActionResult> HGetAsync(ActionParameters parameters, CancellationToken ct)
    {
        var key = parameters.GetString("key")!;
        var field = parameters.GetString("field")!;
        var result = await ExecuteCommandAsync($"HGET {key} {field}", ct);
        return ActionResult.Ok(new { key, field, value = result });
    }

    private async Task<ActionResult> HSetAsync(ActionParameters parameters, CancellationToken ct)
    {
        var key = parameters.GetString("key")!;
        var fields = parameters.Get<Dictionary<string, object>>("fields") ?? new();

        var args = new List<string> { "HSET", key };
        foreach (var kvp in fields)
        {
            args.Add(kvp.Key);
            args.Add(EscapeValue(kvp.Value?.ToString() ?? ""));
        }

        var result = await ExecuteCommandAsync(string.Join(" ", args), ct);
        return ActionResult.Ok(new { key, fieldsSet = int.TryParse(result, out var count) ? count : 0 });
    }

    private async Task<ActionResult> HGetAllAsync(ActionParameters parameters, CancellationToken ct)
    {
        var key = parameters.GetString("key")!;
        var result = await ExecuteArrayCommandAsync($"HGETALL {key}", ct);

        // Convert alternating key-value pairs to dictionary
        var dict = new Dictionary<string, string>();
        for (int i = 0; i < result.Count - 1; i += 2)
        {
            dict[result[i]] = result[i + 1];
        }

        return ActionResult.Ok(new { key, fields = dict });
    }

    private async Task<ActionResult> LPushAsync(ActionParameters parameters, CancellationToken ct)
    {
        var key = parameters.GetString("key")!;
        var values = parameters.Get<List<object>>("values") ?? new();

        var args = new List<string> { "LPUSH", key };
        args.AddRange(values.Select(v => EscapeValue(v?.ToString() ?? "")));

        var result = await ExecuteCommandAsync(string.Join(" ", args), ct);
        return ActionResult.Ok(new { key, listLength = long.TryParse(result, out var len) ? len : 0 });
    }

    private async Task<ActionResult> RPushAsync(ActionParameters parameters, CancellationToken ct)
    {
        var key = parameters.GetString("key")!;
        var values = parameters.Get<List<object>>("values") ?? new();

        var args = new List<string> { "RPUSH", key };
        args.AddRange(values.Select(v => EscapeValue(v?.ToString() ?? "")));

        var result = await ExecuteCommandAsync(string.Join(" ", args), ct);
        return ActionResult.Ok(new { key, listLength = long.TryParse(result, out var len) ? len : 0 });
    }

    private async Task<ActionResult> LRangeAsync(ActionParameters parameters, CancellationToken ct)
    {
        var key = parameters.GetString("key")!;
        var start = parameters.GetInt("start", 0);
        var stop = parameters.GetInt("stop", -1);

        var result = await ExecuteArrayCommandAsync($"LRANGE {key} {start} {stop}", ct);
        return ActionResult.Ok(new { key, values = result });
    }

    private async Task<ActionResult> PublishAsync(ActionParameters parameters, CancellationToken ct)
    {
        var channel = parameters.GetString("channel")!;
        var message = parameters.GetString("message")!;

        var result = await ExecuteCommandAsync($"PUBLISH {channel} {EscapeValue(message)}", ct);
        return ActionResult.Ok(new { channel, subscribersReceived = long.TryParse(result, out var count) ? count : 0 });
    }

    private async Task<ActionResult> KeysAsync(ActionParameters parameters, CancellationToken ct)
    {
        var pattern = parameters.GetString("pattern")!;
        var result = await ExecuteArrayCommandAsync($"KEYS {pattern}", ct);
        return ActionResult.Ok(new { pattern, keys = result });
    }

    private async Task<ActionResult> InfoAsync(ActionParameters parameters, CancellationToken ct)
    {
        var section = parameters.GetString("section");
        var cmd = string.IsNullOrEmpty(section) ? "INFO" : $"INFO {section}";
        var result = await ExecuteCommandAsync(cmd, ct);

        // Parse INFO response into dictionary
        var info = new Dictionary<string, string>();
        if (result != null)
        {
            foreach (var line in result.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith('#')) continue;
                var parts = line.Split(':', 2);
                if (parts.Length == 2)
                {
                    info[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        return ActionResult.Ok(new { info });
    }

    private async Task<string?> ExecuteCommandAsync(string command, CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_host, _port, ct);

        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        // Authenticate if password set
        if (!string.IsNullOrEmpty(_password))
        {
            await SendCommandAsync(writer, $"AUTH {_password}");
            await ReadResponseAsync(reader, ct);
        }

        // Select database
        if (_database > 0)
        {
            await SendCommandAsync(writer, $"SELECT {_database}");
            await ReadResponseAsync(reader, ct);
        }

        // Execute command
        await SendCommandAsync(writer, command);
        return await ReadResponseAsync(reader, ct);
    }

    private async Task<List<string>> ExecuteArrayCommandAsync(string command, CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_host, _port, ct);

        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        // Authenticate and select database
        if (!string.IsNullOrEmpty(_password))
        {
            await SendCommandAsync(writer, $"AUTH {_password}");
            await ReadResponseAsync(reader, ct);
        }

        if (_database > 0)
        {
            await SendCommandAsync(writer, $"SELECT {_database}");
            await ReadResponseAsync(reader, ct);
        }

        await SendCommandAsync(writer, command);
        return await ReadArrayResponseAsync(reader, ct);
    }

    private static async Task SendCommandAsync(StreamWriter writer, string command)
    {
        var parts = ParseCommand(command);
        await writer.WriteAsync($"*{parts.Count}\r\n");

        foreach (var part in parts)
        {
            await writer.WriteAsync($"${Encoding.UTF8.GetByteCount(part)}\r\n{part}\r\n");
        }
    }

    private static List<string> ParseCommand(string command)
    {
        var parts = new List<string>();
        var inQuote = false;
        var current = new StringBuilder();

        foreach (var c in command)
        {
            if (c == '"' && (current.Length == 0 || current[^1] != '\\'))
            {
                inQuote = !inQuote;
            }
            else if (c == ' ' && !inQuote)
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts;
    }

    private static async Task<string?> ReadResponseAsync(StreamReader reader, CancellationToken ct)
    {
        var line = await reader.ReadLineAsync(ct);
        if (string.IsNullOrEmpty(line)) return null;

        return line[0] switch
        {
            '+' => line[1..], // Simple string
            '-' => throw new Exception(line[1..]), // Error
            ':' => line[1..], // Integer
            '$' => await ReadBulkStringAsync(reader, line, ct),
            '*' => null, // Array (not handled here)
            _ => line
        };
    }

    private static async Task<List<string>> ReadArrayResponseAsync(StreamReader reader, CancellationToken ct)
    {
        var line = await reader.ReadLineAsync(ct);
        if (string.IsNullOrEmpty(line) || line[0] != '*')
        {
            return new List<string>();
        }

        var count = int.Parse(line[1..]);
        var result = new List<string>(count);

        for (int i = 0; i < count; i++)
        {
            line = await reader.ReadLineAsync(ct);
            if (line != null && line[0] == '$')
            {
                var str = await ReadBulkStringAsync(reader, line, ct);
                result.Add(str ?? "");
            }
        }

        return result;
    }

    private static async Task<string?> ReadBulkStringAsync(StreamReader reader, string sizeLine, CancellationToken ct)
    {
        var size = int.Parse(sizeLine[1..]);
        if (size < 0) return null;

        var buffer = new char[size];
        await reader.ReadAsync(buffer, 0, size);
        await reader.ReadLineAsync(ct); // Consume \r\n

        return new string(buffer);
    }

    private static string EscapeValue(string value)
    {
        if (value.Contains(' ') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }
        return value;
    }
}
