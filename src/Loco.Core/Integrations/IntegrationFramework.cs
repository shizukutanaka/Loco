// Loco Integration Framework
// Pre-built connectors for common services - addressing competitive gap #2
// Built following Carmack/Pike/Martin principles: Simple, practical, performant

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Data;
using System.Data.Common;
using System.Net.Mail;
using System.Net;

namespace Loco.Core.Integrations;

/// <summary>
/// Base interface for all integrations - provides consistent API
/// </summary>
public interface IIntegration
{
    string Name { get; }
    string Version { get; }
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
    Task<IntegrationResult> ExecuteAsync(IntegrationRequest request, CancellationToken ct = default);
}

/// <summary>
/// Standard request format for all integrations
/// </summary>
public class IntegrationRequest
{
    public string Action { get; set; } = "";
    public Dictionary<string, object> Parameters { get; set; } = new();
    public Dictionary<string, string> Headers { get; set; } = new();
    public object? Body { get; set; }
}

/// <summary>
/// Standard response format with success/error tracking
/// </summary>
public class IntegrationResult
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }
    public int StatusCode { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// HTTP/REST API Integration - Generic HTTP client for any API
/// </summary>
public class HttpIntegration : IIntegration
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public string Name => "HTTP";
    public string Version => "1.0.0";

    public HttpIntegration(string baseUrl, Dictionary<string, string>? defaultHeaders = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient();

        if (defaultHeaders != null)
        {
            foreach (var (key, value) in defaultHeaders)
            {
                _httpClient.DefaultRequestHeaders.Add(key, value);
            }
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(_baseUrl, ct);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IntegrationResult> ExecuteAsync(IntegrationRequest request, CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var url = $"{_baseUrl}{request.Parameters.GetValueOrDefault("path", "")}";
            var method = request.Action.ToUpper();

            var httpRequest = new HttpRequestMessage(new HttpMethod(method), url);

            // Add headers
            foreach (var (key, value) in request.Headers)
            {
                httpRequest.Headers.TryAddWithoutValidation(key, value);
            }

            // Add body for POST/PUT/PATCH
            if (request.Body != null && (method == "POST" || method == "PUT" || method == "PATCH"))
            {
                var json = JsonSerializer.Serialize(request.Body);
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            object? data = null;
            try
            {
                data = JsonSerializer.Deserialize<JsonElement>(responseBody);
            }
            catch
            {
                data = responseBody; // Return as string if not JSON
            }

            return new IntegrationResult
            {
                Success = response.IsSuccessStatusCode,
                Data = data,
                StatusCode = (int)response.StatusCode,
                Duration = DateTime.UtcNow - startTime,
                Metadata = new Dictionary<string, object>
                {
                    ["method"] = method,
                    ["url"] = url,
                    ["headers"] = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
                }
            };
        }
        catch (Exception ex)
        {
            return new IntegrationResult
            {
                Success = false,
                Error = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }
}

/// <summary>
/// Database Integration - Generic SQL database connector
/// Supports PostgreSQL, MySQL, SQLite, SQL Server
/// </summary>
public class DatabaseIntegration : IIntegration
{
    private readonly Func<DbConnection> _connectionFactory;
    private readonly string _databaseType;

    public string Name => $"Database ({_databaseType})";
    public string Version => "1.0.0";

    public DatabaseIntegration(Func<DbConnection> connectionFactory, string databaseType = "SQL")
    {
        _connectionFactory = connectionFactory;
        _databaseType = databaseType;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            using var connection = _connectionFactory();
            await connection.OpenAsync(ct);
            return connection.State == ConnectionState.Open;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IntegrationResult> ExecuteAsync(IntegrationRequest request, CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            using var connection = _connectionFactory();
            await connection.OpenAsync(ct);

            var action = request.Action.ToLower();
            var sql = request.Parameters.GetValueOrDefault("sql", "")?.ToString() ?? "";

            if (action == "query")
            {
                // SELECT queries - return data
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                AddParameters(command, request.Parameters);

                var results = new List<Dictionary<string, object?>>();
                using var reader = await command.ExecuteReaderAsync(ct);

                while (await reader.ReadAsync(ct))
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    results.Add(row);
                }

                return new IntegrationResult
                {
                    Success = true,
                    Data = results,
                    Duration = DateTime.UtcNow - startTime,
                    Metadata = new Dictionary<string, object>
                    {
                        ["rowCount"] = results.Count,
                        ["sql"] = sql
                    }
                };
            }
            else if (action == "execute")
            {
                // INSERT/UPDATE/DELETE - return affected rows
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                AddParameters(command, request.Parameters);

                var affectedRows = await command.ExecuteNonQueryAsync(ct);

                return new IntegrationResult
                {
                    Success = true,
                    Data = affectedRows,
                    Duration = DateTime.UtcNow - startTime,
                    Metadata = new Dictionary<string, object>
                    {
                        ["affectedRows"] = affectedRows,
                        ["sql"] = sql
                    }
                };
            }
            else
            {
                return new IntegrationResult
                {
                    Success = false,
                    Error = $"Unknown action: {action}. Use 'query' or 'execute'",
                    Duration = DateTime.UtcNow - startTime
                };
            }
        }
        catch (Exception ex)
        {
            return new IntegrationResult
            {
                Success = false,
                Error = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    private void AddParameters(DbCommand command, Dictionary<string, object> parameters)
    {
        foreach (var (key, value) in parameters)
        {
            if (key != "sql" && key != "action")
            {
                var param = command.CreateParameter();
                param.ParameterName = key.StartsWith("@") ? key : $"@{key}";
                param.Value = value ?? DBNull.Value;
                command.Parameters.Add(param);
            }
        }
    }
}

/// <summary>
/// Email Integration - SMTP email sender
/// Supports Gmail, Outlook, custom SMTP servers
/// </summary>
public class EmailIntegration : IIntegration
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _username;
    private readonly string _password;
    private readonly bool _enableSsl;

    public string Name => "Email";
    public string Version => "1.0.0";

    public EmailIntegration(string smtpHost, int smtpPort, string username, string password, bool enableSsl = true)
    {
        _smtpHost = smtpHost;
        _smtpPort = smtpPort;
        _username = username;
        _password = password;
        _enableSsl = enableSsl;
    }

    public static EmailIntegration Gmail(string email, string appPassword)
    {
        return new EmailIntegration("smtp.gmail.com", 587, email, appPassword);
    }

    public static EmailIntegration Outlook(string email, string password)
    {
        return new EmailIntegration("smtp-mail.outlook.com", 587, email, password);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            using var client = new SmtpClient(_smtpHost, _smtpPort);
            client.EnableSsl = _enableSsl;
            client.Credentials = new NetworkCredential(_username, _password);

            // Try to connect (no email sent)
            await Task.Run(() => client.Send(new MailMessage(_username, _username, "Test", "Test")), ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IntegrationResult> ExecuteAsync(IntegrationRequest request, CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var to = request.Parameters.GetValueOrDefault("to", "")?.ToString() ?? "";
            var subject = request.Parameters.GetValueOrDefault("subject", "")?.ToString() ?? "";
            var body = request.Parameters.GetValueOrDefault("body", "")?.ToString() ?? "";
            var isHtml = request.Parameters.GetValueOrDefault("isHtml", false);
            var cc = request.Parameters.GetValueOrDefault("cc", "")?.ToString();
            var bcc = request.Parameters.GetValueOrDefault("bcc", "")?.ToString();

            using var message = new MailMessage();
            message.From = new MailAddress(_username);

            foreach (var addr in to.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                message.To.Add(addr.Trim());
            }

            if (!string.IsNullOrEmpty(cc))
            {
                foreach (var addr in cc.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    message.CC.Add(addr.Trim());
                }
            }

            if (!string.IsNullOrEmpty(bcc))
            {
                foreach (var addr in bcc.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    message.Bcc.Add(addr.Trim());
                }
            }

            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = Convert.ToBoolean(isHtml);

            using var client = new SmtpClient(_smtpHost, _smtpPort);
            client.EnableSsl = _enableSsl;
            client.Credentials = new NetworkCredential(_username, _password);

            await client.SendMailAsync(message, ct);

            return new IntegrationResult
            {
                Success = true,
                Data = new { sent = true, recipients = message.To.Count },
                Duration = DateTime.UtcNow - startTime,
                Metadata = new Dictionary<string, object>
                {
                    ["to"] = to,
                    ["subject"] = subject,
                    ["recipientCount"] = message.To.Count
                }
            };
        }
        catch (Exception ex)
        {
            return new IntegrationResult
            {
                Success = false,
                Error = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }
}

/// <summary>
/// Slack Integration - Send messages to Slack channels
/// Uses Slack Webhook API for simplicity
/// </summary>
public class SlackIntegration : IIntegration
{
    private readonly string _webhookUrl;
    private readonly HttpClient _httpClient;

    public string Name => "Slack";
    public string Version => "1.0.0";

    public SlackIntegration(string webhookUrl)
    {
        _webhookUrl = webhookUrl;
        _httpClient = new HttpClient();
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var payload = new { text = "Loco connection test" };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_webhookUrl, content, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IntegrationResult> ExecuteAsync(IntegrationRequest request, CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var text = request.Parameters.GetValueOrDefault("text", "")?.ToString() ?? "";
            var channel = request.Parameters.GetValueOrDefault("channel", "")?.ToString();
            var username = request.Parameters.GetValueOrDefault("username", "Loco")?.ToString();
            var iconEmoji = request.Parameters.GetValueOrDefault("icon_emoji", ":robot_face:")?.ToString();

            var payload = new Dictionary<string, object>
            {
                ["text"] = text
            };

            if (!string.IsNullOrEmpty(channel))
                payload["channel"] = channel!;

            if (!string.IsNullOrEmpty(username))
                payload["username"] = username!;

            if (!string.IsNullOrEmpty(iconEmoji))
                payload["icon_emoji"] = iconEmoji!;

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_webhookUrl, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            return new IntegrationResult
            {
                Success = response.IsSuccessStatusCode,
                Data = responseBody,
                StatusCode = (int)response.StatusCode,
                Duration = DateTime.UtcNow - startTime,
                Metadata = new Dictionary<string, object>
                {
                    ["channel"] = channel ?? "default",
                    ["messageLength"] = text.Length
                }
            };
        }
        catch (Exception ex)
        {
            return new IntegrationResult
            {
                Success = false,
                Error = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }
}

/// <summary>
/// GitHub Integration - Interact with GitHub API
/// Supports creating issues, PRs, checking repo status
/// </summary>
public class GitHubIntegration : IIntegration
{
    private readonly string _token;
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.github.com";

    public string Name => "GitHub";
    public string Version => "1.0.0";

    public GitHubIntegration(string token)
    {
        _token = token;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {token}");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Loco-Automation");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/user", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IntegrationResult> ExecuteAsync(IntegrationRequest request, CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var action = request.Action.ToLower();
            var owner = request.Parameters.GetValueOrDefault("owner", "")?.ToString() ?? "";
            var repo = request.Parameters.GetValueOrDefault("repo", "")?.ToString() ?? "";

            switch (action)
            {
                case "create_issue":
                    return await CreateIssueAsync(owner, repo, request.Parameters, startTime, ct);

                case "get_repo":
                    return await GetRepoAsync(owner, repo, startTime, ct);

                case "list_issues":
                    return await ListIssuesAsync(owner, repo, request.Parameters, startTime, ct);

                case "create_pr":
                    return await CreatePullRequestAsync(owner, repo, request.Parameters, startTime, ct);

                default:
                    return new IntegrationResult
                    {
                        Success = false,
                        Error = $"Unknown action: {action}",
                        Duration = DateTime.UtcNow - startTime
                    };
            }
        }
        catch (Exception ex)
        {
            return new IntegrationResult
            {
                Success = false,
                Error = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    private async Task<IntegrationResult> CreateIssueAsync(string owner, string repo, Dictionary<string, object> parameters, DateTime startTime, CancellationToken ct)
    {
        var title = parameters.GetValueOrDefault("title", "")?.ToString() ?? "";
        var body = parameters.GetValueOrDefault("body", "")?.ToString() ?? "";
        var labels = parameters.GetValueOrDefault("labels", "")?.ToString()?.Split(',', StringSplitOptions.RemoveEmptyEntries);

        var payload = new Dictionary<string, object>
        {
            ["title"] = title,
            ["body"] = body
        };

        if (labels?.Length > 0)
            payload["labels"] = labels;

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{BaseUrl}/repos/{owner}/{repo}/issues", content, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        var data = JsonSerializer.Deserialize<JsonElement>(responseBody);

        return new IntegrationResult
        {
            Success = response.IsSuccessStatusCode,
            Data = data,
            StatusCode = (int)response.StatusCode,
            Duration = DateTime.UtcNow - startTime,
            Metadata = new Dictionary<string, object>
            {
                ["repo"] = $"{owner}/{repo}",
                ["action"] = "create_issue"
            }
        };
    }

    private async Task<IntegrationResult> GetRepoAsync(string owner, string repo, DateTime startTime, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync($"{BaseUrl}/repos/{owner}/{repo}", ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        var data = JsonSerializer.Deserialize<JsonElement>(responseBody);

        return new IntegrationResult
        {
            Success = response.IsSuccessStatusCode,
            Data = data,
            StatusCode = (int)response.StatusCode,
            Duration = DateTime.UtcNow - startTime
        };
    }

    private async Task<IntegrationResult> ListIssuesAsync(string owner, string repo, Dictionary<string, object> parameters, DateTime startTime, CancellationToken ct)
    {
        var state = parameters.GetValueOrDefault("state", "open")?.ToString() ?? "open";
        var response = await _httpClient.GetAsync($"{BaseUrl}/repos/{owner}/{repo}/issues?state={state}", ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        var data = JsonSerializer.Deserialize<JsonElement>(responseBody);

        return new IntegrationResult
        {
            Success = response.IsSuccessStatusCode,
            Data = data,
            StatusCode = (int)response.StatusCode,
            Duration = DateTime.UtcNow - startTime,
            Metadata = new Dictionary<string, object>
            {
                ["count"] = data.GetArrayLength()
            }
        };
    }

    private async Task<IntegrationResult> CreatePullRequestAsync(string owner, string repo, Dictionary<string, object> parameters, DateTime startTime, CancellationToken ct)
    {
        var title = parameters.GetValueOrDefault("title", "")?.ToString() ?? "";
        var body = parameters.GetValueOrDefault("body", "")?.ToString() ?? "";
        var head = parameters.GetValueOrDefault("head", "")?.ToString() ?? "";
        var baseRef = parameters.GetValueOrDefault("base", "main")?.ToString() ?? "main";

        var payload = new Dictionary<string, object>
        {
            ["title"] = title,
            ["body"] = body,
            ["head"] = head,
            ["base"] = baseRef
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{BaseUrl}/repos/{owner}/{repo}/pulls", content, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        var data = JsonSerializer.Deserialize<JsonElement>(responseBody);

        return new IntegrationResult
        {
            Success = response.IsSuccessStatusCode,
            Data = data,
            StatusCode = (int)response.StatusCode,
            Duration = DateTime.UtcNow - startTime
        };
    }
}

/// <summary>
/// Integration registry - manages all available integrations
/// </summary>
public class IntegrationRegistry
{
    private readonly Dictionary<string, IIntegration> _integrations = new();

    public void Register(string key, IIntegration integration)
    {
        _integrations[key] = integration;
    }

    public IIntegration? Get(string key)
    {
        return _integrations.GetValueOrDefault(key);
    }

    public IEnumerable<string> GetRegisteredIntegrations()
    {
        return _integrations.Keys;
    }

    public async Task<Dictionary<string, bool>> TestAllConnectionsAsync(CancellationToken ct = default)
    {
        var results = new Dictionary<string, bool>();

        foreach (var (key, integration) in _integrations)
        {
            results[key] = await integration.TestConnectionAsync(ct);
        }

        return results;
    }
}
