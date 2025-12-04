// John Carmack: "Simple things should be simple, complex things should be possible"
// Rob Pike: "A little copying is better than a little dependency"

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;
using Loco.Core.Practical;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// HTTP/REST API connector - Universal connector for any HTTP-based API
/// Supports GET, POST, PUT, PATCH, DELETE with various authentication methods
/// </summary>
public sealed class HttpConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private string? _baseUrl;
    private Dictionary<string, string>? _defaultHeaders;

    public override string Id => "http";
    public override string Name => "HTTP/REST API";
    public override string Description => "Universal HTTP client for REST APIs, webhooks, and web services";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Api;

    public override ConnectorCapabilities Capabilities => new()
    {
        SupportsActions = true,
        SupportsTriggers = true,
        SupportsWebhooks = true,
        RateLimitPerMinute = 1000,
        DefaultTimeout = TimeSpan.FromSeconds(30)
    };

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.Custom,
        RequiredCredentials = new CredentialField[]
        {
            new() { Name = "authType", Label = "Authentication Type", Type = ParameterType.Select, Required = false },
            new() { Name = "apiKey", Label = "API Key", Type = ParameterType.Password, Required = false },
            new() { Name = "apiKeyHeader", Label = "API Key Header", Type = ParameterType.String, Required = false },
            new() { Name = "bearerToken", Label = "Bearer Token", Type = ParameterType.Password, Required = false },
            new() { Name = "username", Label = "Username", Type = ParameterType.String, Required = false },
            new() { Name = "password", Label = "Password", Type = ParameterType.Password, Required = false }
        }
    };

    public override IReadOnlyList<ConfigParameter> ConfigParameters =>
    [
        new() { Name = "baseUrl", Label = "Base URL", Type = ParameterType.String, Description = "Base URL for all requests (optional)" },
        new() { Name = "timeout", Label = "Timeout (seconds)", Type = ParameterType.Number, DefaultValue = 30 },
        new() { Name = "defaultHeaders", Label = "Default Headers", Type = ParameterType.Json, Description = "Default headers for all requests (JSON object)" }
    ];

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        new()
        {
            Id = "get",
            Name = "GET Request",
            Description = "Send HTTP GET request",
            Parameters = GetRequestParameters(),
            RetryConfig = new RetryConfig { MaxAttempts = 3 }
        },
        new()
        {
            Id = "post",
            Name = "POST Request",
            Description = "Send HTTP POST request with body",
            Parameters = GetRequestWithBodyParameters(),
            RetryConfig = new RetryConfig { MaxAttempts = 3 }
        },
        new()
        {
            Id = "put",
            Name = "PUT Request",
            Description = "Send HTTP PUT request with body",
            Parameters = GetRequestWithBodyParameters(),
            RetryConfig = new RetryConfig { MaxAttempts = 3 }
        },
        new()
        {
            Id = "patch",
            Name = "PATCH Request",
            Description = "Send HTTP PATCH request with body",
            Parameters = GetRequestWithBodyParameters(),
            RetryConfig = new RetryConfig { MaxAttempts = 3 }
        },
        new()
        {
            Id = "delete",
            Name = "DELETE Request",
            Description = "Send HTTP DELETE request",
            Parameters = GetRequestParameters(),
            RequiresConfirmation = true,
            RetryConfig = new RetryConfig { MaxAttempts = 3 }
        },
        new()
        {
            Id = "download",
            Name = "Download File",
            Description = "Download file from URL",
            Parameters =
            [
                new() { Name = "url", Type = ParameterType.String, Required = true, Description = "URL to download from" },
                new() { Name = "savePath", Type = ParameterType.String, Required = true, Description = "Local path to save the file" },
                new() { Name = "headers", Type = ParameterType.Json, Description = "Additional headers" }
            ]
        },
        new()
        {
            Id = "upload",
            Name = "Upload File",
            Description = "Upload file to URL (multipart/form-data)",
            Parameters =
            [
                new() { Name = "url", Type = ParameterType.String, Required = true, Description = "URL to upload to" },
                new() { Name = "filePath", Type = ParameterType.String, Required = true, Description = "Local file path" },
                new() { Name = "fieldName", Type = ParameterType.String, DefaultValue = "file", Description = "Form field name" },
                new() { Name = "additionalFields", Type = ParameterType.Json, Description = "Additional form fields" },
                new() { Name = "headers", Type = ParameterType.Json, Description = "Additional headers" }
            ]
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "webhook",
            Name = "Webhook",
            Description = "Receive webhook callbacks",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "path", Type = ParameterType.String, Required = true, Description = "Webhook path" },
                new() { Name = "method", Type = ParameterType.Select, DefaultValue = "POST",
                    Options = [new() { Label = "POST", Value = "POST" }, new() { Label = "GET", Value = "GET" }] },
                new() { Name = "secret", Type = ParameterType.Password, Description = "Webhook secret for verification" }
            ]
        },
        new()
        {
            Id = "poll",
            Name = "Poll URL",
            Description = "Periodically poll a URL for changes",
            Type = TriggerType.Polling,
            ConfigParameters =
            [
                new() { Name = "url", Type = ParameterType.String, Required = true, Description = "URL to poll" },
                new() { Name = "interval", Type = ParameterType.Number, DefaultValue = 60, Description = "Poll interval in seconds" },
                new() { Name = "checkPath", Type = ParameterType.String, Description = "JSON path to check for changes" }
            ]
        }
    ];

    private static ActionParameter[] GetRequestParameters() =>
    [
        new() { Name = "url", Type = ParameterType.String, Required = true, Description = "Request URL (absolute or relative to base URL)" },
        new() { Name = "headers", Type = ParameterType.Json, Description = "Request headers (JSON object)" },
        new() { Name = "queryParams", Type = ParameterType.Json, Description = "Query parameters (JSON object)" },
        new() { Name = "timeout", Type = ParameterType.Number, Description = "Request timeout in seconds" }
    ];

    private static ActionParameter[] GetRequestWithBodyParameters() =>
    [
        new() { Name = "url", Type = ParameterType.String, Required = true, Description = "Request URL" },
        new() { Name = "body", Type = ParameterType.Json, Description = "Request body (JSON)" },
        new() { Name = "bodyRaw", Type = ParameterType.String, Description = "Raw request body (alternative to JSON body)" },
        new() { Name = "contentType", Type = ParameterType.String, DefaultValue = "application/json", Description = "Content-Type header" },
        new() { Name = "headers", Type = ParameterType.Json, Description = "Additional headers" },
        new() { Name = "queryParams", Type = ParameterType.Json, Description = "Query parameters" },
        new() { Name = "timeout", Type = ParameterType.Number, Description = "Request timeout in seconds" }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        var timeout = config.GetSetting<int?>("timeout") ?? 30;
        _baseUrl = config.GetSettingString("baseUrl");

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(timeout)
        };

        // Setup authentication
        await SetupAuthenticationAsync(config);

        // Setup default headers
        var defaultHeadersJson = config.GetSettingString("defaultHeaders");
        if (!string.IsNullOrEmpty(defaultHeadersJson))
        {
            _defaultHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(defaultHeadersJson);
        }

        await base.InitializeAsync(config, ct);
    }

    private Task SetupAuthenticationAsync(ConnectorConfiguration config)
    {
        var authType = config.GetCredentialString("authType")?.ToLowerInvariant();

        switch (authType)
        {
            case "apikey":
                var apiKey = config.GetCredentialString("apiKey");
                var headerName = config.GetCredentialString("apiKeyHeader") ?? "X-Api-Key";
                if (!string.IsNullOrEmpty(apiKey))
                {
                    _httpClient!.DefaultRequestHeaders.Add(headerName, apiKey);
                }
                break;

            case "bearer":
                var token = config.GetCredentialString("bearerToken");
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient!.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);
                }
                break;

            case "basic":
                var username = config.GetCredentialString("username");
                var password = config.GetCredentialString("password");
                if (!string.IsNullOrEmpty(username))
                {
                    var credentials = Convert.ToBase64String(
                        Encoding.ASCII.GetBytes($"{username}:{password}"));
                    _httpClient!.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Basic", credentials);
                }
                break;
        }

        return Task.CompletedTask;
    }

    protected override async Task<ActionResult> ExecuteActionCoreAsync(
        ConnectorAction action,
        ActionParameters parameters,
        Core.ExecutionContext context,
        CancellationToken ct)
    {
        return action.Id switch
        {
            "get" => await ExecuteRequestAsync(HttpMethod.Get, parameters, ct),
            "post" => await ExecuteRequestAsync(HttpMethod.Post, parameters, ct),
            "put" => await ExecuteRequestAsync(HttpMethod.Put, parameters, ct),
            "patch" => await ExecuteRequestAsync(HttpMethod.Patch, parameters, ct),
            "delete" => await ExecuteRequestAsync(HttpMethod.Delete, parameters, ct),
            "download" => await DownloadFileAsync(parameters, ct),
            "upload" => await UploadFileAsync(parameters, ct),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> ExecuteRequestAsync(
        HttpMethod method,
        ActionParameters parameters,
        CancellationToken ct)
    {
        var url = BuildUrl(parameters.GetString("url")!, parameters);
        using var request = new HttpRequestMessage(method, url);

        // Add headers
        AddHeaders(request, parameters);

        // Add body for POST/PUT/PATCH
        if (method != HttpMethod.Get && method != HttpMethod.Delete)
        {
            var body = parameters.Get<object>("body");
            var bodyRaw = parameters.GetString("bodyRaw");
            var contentType = parameters.GetString("contentType") ?? "application/json";

            if (body != null)
            {
                var json = body is string s ? s : JsonSerializer.Serialize(body);
                request.Content = new StringContent(json, Encoding.UTF8, contentType);
            }
            else if (!string.IsNullOrEmpty(bodyRaw))
            {
                request.Content = new StringContent(bodyRaw, Encoding.UTF8, contentType);
            }
        }

        try
        {
            using var response = await _httpClient!.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            var result = new HttpResponse
            {
                StatusCode = (int)response.StatusCode,
                StatusText = response.ReasonPhrase ?? "",
                Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                Body = TryParseJson(content) ?? content
            };

            if (!response.IsSuccessStatusCode)
            {
                return ActionResult.Fail(
                    $"HTTP {result.StatusCode}: {result.StatusText}",
                    result.StatusCode.ToString()) with { Data = result };
            }

            return ActionResult.Ok(result);
        }
        catch (HttpRequestException ex)
        {
            return ActionResult.Fail($"Request failed: {ex.Message}", "REQUEST_FAILED");
        }
        catch (TaskCanceledException)
        {
            return ActionResult.Fail("Request timed out", "TIMEOUT");
        }
    }

    private async Task<ActionResult> DownloadFileAsync(ActionParameters parameters, CancellationToken ct)
    {
        var url = parameters.GetString("url")!;
        var savePath = parameters.GetString("savePath")!;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddHeaders(request, parameters);

        try
        {
            using var response = await _httpClient!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var fileStream = File.Create(savePath);
            await response.Content.CopyToAsync(fileStream, ct);

            return ActionResult.Ok(new
            {
                path = savePath,
                size = new FileInfo(savePath).Length,
                contentType = response.Content.Headers.ContentType?.MediaType
            });
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"Download failed: {ex.Message}");
        }
    }

    private async Task<ActionResult> UploadFileAsync(ActionParameters parameters, CancellationToken ct)
    {
        var url = parameters.GetString("url")!;
        var filePath = parameters.GetString("filePath")!;
        var fieldName = parameters.GetString("fieldName") ?? "file";

        if (!File.Exists(filePath))
        {
            return ActionResult.Fail($"File not found: {filePath}", "FILE_NOT_FOUND");
        }

        using var content = new MultipartFormDataContent();

        // Add file
        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath, ct));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            MimeTypes.GetMimeType(Path.GetExtension(filePath)));
        content.Add(fileContent, fieldName, Path.GetFileName(filePath));

        // Add additional fields
        var additionalFields = parameters.Get<Dictionary<string, string>>("additionalFields");
        if (additionalFields != null)
        {
            foreach (var field in additionalFields)
            {
                content.Add(new StringContent(field.Value), field.Key);
            }
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        AddHeaders(request, parameters);

        try
        {
            using var response = await _httpClient!.SendAsync(request, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return ActionResult.Fail(
                    $"Upload failed: HTTP {(int)response.StatusCode}",
                    ((int)response.StatusCode).ToString());
            }

            return ActionResult.Ok(new
            {
                statusCode = (int)response.StatusCode,
                body = TryParseJson(responseContent) ?? responseContent
            });
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"Upload failed: {ex.Message}");
        }
    }

    private string BuildUrl(string url, ActionParameters parameters)
    {
        // Handle relative URLs
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            if (!string.IsNullOrEmpty(_baseUrl))
            {
                url = _baseUrl.TrimEnd('/') + "/" + url.TrimStart('/');
            }
        }

        // Add query parameters
        var queryParams = parameters.Get<Dictionary<string, object>>("queryParams");
        if (queryParams != null && queryParams.Count > 0)
        {
            var queryString = string.Join("&",
                queryParams.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value?.ToString() ?? "")}"));

            url = url.Contains('?')
                ? $"{url}&{queryString}"
                : $"{url}?{queryString}";
        }

        return url;
    }

    private void AddHeaders(HttpRequestMessage request, ActionParameters parameters)
    {
        // Add default headers
        if (_defaultHeaders != null)
        {
            foreach (var header in _defaultHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        // Add request-specific headers
        var headers = parameters.Get<Dictionary<string, string>>("headers");
        if (headers != null)
        {
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
    }

    private static object? TryParseJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(content);
        }
        catch
        {
            return null;
        }
    }

    public override async Task CleanupAsync(CancellationToken ct = default)
    {
        _httpClient?.Dispose();
        _httpClient = null;
        await base.CleanupAsync(ct);
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// HTTP response model
/// </summary>
public sealed class HttpResponse
{
    public int StatusCode { get; init; }
    public string StatusText { get; init; } = "";
    public Dictionary<string, string> Headers { get; init; } = new();
    public object? Body { get; init; }
}

/// <summary>
/// Simple MIME type lookup
/// </summary>
internal static class MimeTypes
{
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".json"] = "application/json",
        [".xml"] = "application/xml",
        [".html"] = "text/html",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".pdf"] = "application/pdf",
        [".zip"] = "application/zip",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml",
        [".mp4"] = "video/mp4",
        [".mp3"] = "audio/mpeg"
    };

    public static string GetMimeType(string extension) =>
        Types.TryGetValue(extension, out var type) ? type : "application/octet-stream";
}
