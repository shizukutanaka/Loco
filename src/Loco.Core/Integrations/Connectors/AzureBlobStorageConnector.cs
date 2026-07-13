using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Loco.Core.Integrations.Core;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// Azure Blob Storage connector for managing containers and blobs.
/// Uses Azure Blob Storage REST API with Shared Key authentication.
/// </summary>
public sealed class AzureBlobStorageConnector : ConnectorBase
{
    private HttpClient? _httpClient;
    private string? _accountName;
    private string? _accountKey;
    private string? _baseUrl;
    private const string ApiVersion = "2024-02-04";

    public override string Id => "azure-blob-storage";
    public override string Name => "Azure Blob Storage";
    public override string Description => "Microsoft Azure cloud object storage service for unstructured data";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Storage;
    public override string IconUrl => "https://azure.microsoft.com/favicon.ico";

    public override ConnectorCapabilities Capabilities => new()
    {
        SupportsActions = true,
        SupportsTriggers = true,
        SupportsWebhooks = true,
        MaxConcurrentConnections = 50,
        RateLimitPerMinute = 2000
    };

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.Custom,
        RequiredCredentials = new CredentialField[]
        {
            new() { Name = "accountName", Label = "Storage Account Name", Type = ParameterType.String },
            new() { Name = "accountKey", Label = "Account Key", Type = ParameterType.Password, Description = "Primary or secondary access key" }
        }
    };

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        new()
        {
            Id = "listContainers",
            Name = "List Containers",
            Description = "List all containers in the storage account",
            Parameters = new ActionParameter[]
            {
                new() { Name = "prefix", Type = ParameterType.String, Description = "Filter by container name prefix" },
                new() { Name = "maxResults", Type = ParameterType.Number, DefaultValue = 100 }
            }
        },
        new()
        {
            Id = "createContainer",
            Name = "Create Container",
            Description = "Create a new container",
            Parameters = new ActionParameter[]
            {
                new() { Name = "containerName", Type = ParameterType.String, Required = true },
                new() { Name = "publicAccess", Type = ParameterType.String, Description = "container, blob, or none (default)" }
            }
        },
        new()
        {
            Id = "deleteContainer",
            Name = "Delete Container",
            Description = "Delete a container and all its blobs",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "containerName", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "listBlobs",
            Name = "List Blobs",
            Description = "List blobs in a container",
            Parameters = new ActionParameter[]
            {
                new() { Name = "containerName", Type = ParameterType.String, Required = true },
                new() { Name = "prefix", Type = ParameterType.String, Description = "Filter by blob name prefix" },
                new() { Name = "delimiter", Type = ParameterType.String, Description = "Use '/' for virtual directory listing" },
                new() { Name = "maxResults", Type = ParameterType.Number, DefaultValue = 100 }
            }
        },
        new()
        {
            Id = "uploadBlob",
            Name = "Upload Blob",
            Description = "Upload content as a blob",
            Parameters = new ActionParameter[]
            {
                new() { Name = "containerName", Type = ParameterType.String, Required = true },
                new() { Name = "blobName", Type = ParameterType.String, Required = true },
                new() { Name = "content", Type = ParameterType.String, Required = true, Description = "Text content or base64-encoded binary" },
                new() { Name = "contentType", Type = ParameterType.String, DefaultValue = "application/octet-stream" },
                new() { Name = "isBase64", Type = ParameterType.Boolean, DefaultValue = false }
            }
        },
        new()
        {
            Id = "downloadBlob",
            Name = "Download Blob",
            Description = "Download blob content",
            Parameters = new ActionParameter[]
            {
                new() { Name = "containerName", Type = ParameterType.String, Required = true },
                new() { Name = "blobName", Type = ParameterType.String, Required = true },
                new() { Name = "returnBase64", Type = ParameterType.Boolean, DefaultValue = false }
            }
        },
        new()
        {
            Id = "deleteBlob",
            Name = "Delete Blob",
            Description = "Delete a blob",
            RequiresConfirmation = true,
            Parameters = new ActionParameter[]
            {
                new() { Name = "containerName", Type = ParameterType.String, Required = true },
                new() { Name = "blobName", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "getBlobProperties",
            Name = "Get Blob Properties",
            Description = "Get properties and metadata of a blob",
            Parameters = new ActionParameter[]
            {
                new() { Name = "containerName", Type = ParameterType.String, Required = true },
                new() { Name = "blobName", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "copyBlob",
            Name = "Copy Blob",
            Description = "Copy a blob to a new location",
            Parameters = new ActionParameter[]
            {
                new() { Name = "sourceContainer", Type = ParameterType.String, Required = true },
                new() { Name = "sourceBlob", Type = ParameterType.String, Required = true },
                new() { Name = "destContainer", Type = ParameterType.String, Required = true },
                new() { Name = "destBlob", Type = ParameterType.String, Required = true }
            }
        },
        new()
        {
            Id = "generateSasUrl",
            Name = "Generate SAS URL",
            Description = "Generate a Shared Access Signature URL for a blob",
            Parameters = new ActionParameter[]
            {
                new() { Name = "containerName", Type = ParameterType.String, Required = true },
                new() { Name = "blobName", Type = ParameterType.String, Description = "Leave empty for container-level SAS" },
                new() { Name = "permissions", Type = ParameterType.String, Required = true, Description = "r=read, w=write, d=delete, l=list" },
                new() { Name = "expiryMinutes", Type = ParameterType.Number, DefaultValue = 60 }
            }
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "blobCreated",
            Name = "Blob Created",
            Description = "Triggered when a blob is created",
            Type = TriggerType.Webhook
        },
        new()
        {
            Id = "blobDeleted",
            Name = "Blob Deleted",
            Description = "Triggered when a blob is deleted",
            Type = TriggerType.Webhook
        }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        await base.InitializeAsync(config, ct);

        _accountName = config.GetCredentialString("accountName");
        _accountKey = config.GetCredentialString("accountKey");
        _baseUrl = $"https://{_accountName}.blob.core.windows.net";

        // Dispose any previous client before replacing it. InitializeAsync can run more
        // than once for the same cached connector instance (e.g. ConnectorRegistry.
        // GetInitializedConnectorAsync on credential rotation); overwriting _httpClient
        // unconditionally previously leaked the old HttpClient and its socket handler.
        _httpClient?.Dispose();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl)
        };
    }

    protected override async Task<ActionResult> ExecuteActionCoreAsync(
        ConnectorAction action,
        ActionParameters parameters,
        Core.ExecutionContext context,
        CancellationToken ct)
    {
        return action.Id switch
        {
            "listContainers" => await ListContainersAsync(parameters, ct),
            "createContainer" => await CreateContainerAsync(parameters, ct),
            "deleteContainer" => await DeleteContainerAsync(parameters, ct),
            "listBlobs" => await ListBlobsAsync(parameters, ct),
            "uploadBlob" => await UploadBlobAsync(parameters, ct),
            "downloadBlob" => await DownloadBlobAsync(parameters, ct),
            "deleteBlob" => await DeleteBlobAsync(parameters, ct),
            "getBlobProperties" => await GetBlobPropertiesAsync(parameters, ct),
            "copyBlob" => await CopyBlobAsync(parameters, ct),
            "generateSasUrl" => GenerateSasUrl(parameters),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> ListContainersAsync(ActionParameters parameters, CancellationToken ct)
    {
        var queryParams = new List<string> { "comp=list" };

        var prefix = parameters.GetString("prefix");
        if (!string.IsNullOrEmpty(prefix))
            queryParams.Add($"prefix={Uri.EscapeDataString(prefix)}");

        var maxResults = parameters.GetInt("maxResults");
        if (maxResults > 0)
            queryParams.Add($"maxresults={maxResults}");

        var uri = $"/?{string.Join("&", queryParams)}";
        var response = await SendRequestAsync(HttpMethod.Get, uri, null, null, ct);
        return await ProcessXmlResponseAsync(response, ct);
    }

    private async Task<ActionResult> CreateContainerAsync(ActionParameters parameters, CancellationToken ct)
    {
        var containerName = parameters.GetString("containerName")!;
        var headers = new Dictionary<string, string>();

        var publicAccess = parameters.GetString("publicAccess");
        if (!string.IsNullOrEmpty(publicAccess))
        {
            var accessLevel = publicAccess.ToLower();
            if (accessLevel == "container" || accessLevel == "blob")
                headers["x-ms-blob-public-access"] = accessLevel;
        }

        var uri = $"/{containerName}?restype=container";
        var response = await SendRequestAsync(HttpMethod.Put, uri, null, headers, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ActionResult> DeleteContainerAsync(ActionParameters parameters, CancellationToken ct)
    {
        var containerName = parameters.GetString("containerName")!;
        var uri = $"/{containerName}?restype=container";
        var response = await SendRequestAsync(HttpMethod.Delete, uri, null, null, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ActionResult> ListBlobsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var containerName = parameters.GetString("containerName")!;
        var queryParams = new List<string> { "restype=container", "comp=list" };

        var prefix = parameters.GetString("prefix");
        if (!string.IsNullOrEmpty(prefix))
            queryParams.Add($"prefix={Uri.EscapeDataString(prefix)}");

        var delimiter = parameters.GetString("delimiter");
        if (!string.IsNullOrEmpty(delimiter))
            queryParams.Add($"delimiter={Uri.EscapeDataString(delimiter)}");

        var maxResults = parameters.GetInt("maxResults");
        if (maxResults > 0)
            queryParams.Add($"maxresults={maxResults}");

        var uri = $"/{containerName}?{string.Join("&", queryParams)}";
        var response = await SendRequestAsync(HttpMethod.Get, uri, null, null, ct);
        return await ProcessXmlResponseAsync(response, ct);
    }

    private async Task<ActionResult> UploadBlobAsync(ActionParameters parameters, CancellationToken ct)
    {
        var containerName = parameters.GetString("containerName")!;
        var blobName = parameters.GetString("blobName")!;
        var content = parameters.GetString("content")!;
        var contentType = parameters.GetString("contentType") ?? "application/octet-stream";
        var isBase64 = parameters.GetBool("isBase64");

        var blobContent = isBase64
            ? Convert.FromBase64String(content)
            : Encoding.UTF8.GetBytes(content);

        var headers = new Dictionary<string, string>
        {
            ["x-ms-blob-type"] = "BlockBlob",
            ["Content-Type"] = contentType
        };

        var uri = $"/{containerName}/{Uri.EscapeDataString(blobName)}";
        var response = await SendRequestAsync(HttpMethod.Put, uri, blobContent, headers, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ActionResult> DownloadBlobAsync(ActionParameters parameters, CancellationToken ct)
    {
        var containerName = parameters.GetString("containerName")!;
        var blobName = parameters.GetString("blobName")!;
        var returnBase64 = parameters.GetBool("returnBase64");

        var uri = $"/{containerName}/{Uri.EscapeDataString(blobName)}";
        var response = await SendRequestAsync(HttpMethod.Get, uri, null, null, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Azure Blob Storage error ({response.StatusCode}): {errorContent}");
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var result = new Dictionary<string, object>
        {
            ["contentType"] = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
            ["contentLength"] = bytes.Length
        };

        if (returnBase64)
        {
            result["content"] = Convert.ToBase64String(bytes);
            result["encoding"] = "base64";
        }
        else
        {
            result["content"] = Encoding.UTF8.GetString(bytes);
            result["encoding"] = "utf-8";
        }

        return ActionResult.Ok(result);
    }

    private async Task<ActionResult> DeleteBlobAsync(ActionParameters parameters, CancellationToken ct)
    {
        var containerName = parameters.GetString("containerName")!;
        var blobName = parameters.GetString("blobName")!;

        var uri = $"/{containerName}/{Uri.EscapeDataString(blobName)}";
        var response = await SendRequestAsync(HttpMethod.Delete, uri, null, null, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private async Task<ActionResult> GetBlobPropertiesAsync(ActionParameters parameters, CancellationToken ct)
    {
        var containerName = parameters.GetString("containerName")!;
        var blobName = parameters.GetString("blobName")!;
        var uri = $"/{containerName}/{Uri.EscapeDataString(blobName)}";
        var response = await SendRequestAsync(HttpMethod.Head, uri, null, null, ct);
        return ExtractHeadersAsResult(response);
    }

    private async Task<ActionResult> CopyBlobAsync(ActionParameters parameters, CancellationToken ct)
    {
        var sourceContainer = parameters.GetString("sourceContainer")!;
        var sourceBlob = parameters.GetString("sourceBlob")!;
        var destContainer = parameters.GetString("destContainer")!;
        var destBlob = parameters.GetString("destBlob")!;

        var sourceUrl = $"{_baseUrl}/{sourceContainer}/{Uri.EscapeDataString(sourceBlob)}";
        var headers = new Dictionary<string, string>
        {
            ["x-ms-copy-source"] = sourceUrl
        };

        var uri = $"/{destContainer}/{Uri.EscapeDataString(destBlob)}";
        var response = await SendRequestAsync(HttpMethod.Put, uri, null, headers, ct);
        return await ProcessResponseAsync(response, ct);
    }

    private ActionResult GenerateSasUrl(ActionParameters parameters)
    {
        var containerName = parameters.GetString("containerName")!;
        var blobName = parameters.GetString("blobName");
        var permissions = parameters.GetString("permissions")!;
        var expiryMinutes = parameters.GetInt("expiryMinutes", 60);

        var expiryTime = DateTime.UtcNow.AddMinutes(expiryMinutes);
        var signedExpiry = expiryTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var signedResource = string.IsNullOrEmpty(blobName) ? "c" : "b";
        var signedVersion = ApiVersion;

        var canonicalizedResource = string.IsNullOrEmpty(blobName)
            ? $"/blob/{_accountName}/{containerName}"
            : $"/blob/{_accountName}/{containerName}/{blobName}";

        var stringToSign = string.Join("\n",
            permissions,
            "",
            signedExpiry,
            canonicalizedResource,
            "", "", "", signedVersion, signedResource,
            "", "", "", "", "", "");

        using var hmac = new HMACSHA256(Convert.FromBase64String(_accountKey!));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));

        var sasToken = string.Join("&",
            $"sv={signedVersion}",
            $"sr={signedResource}",
            $"sp={permissions}",
            $"se={Uri.EscapeDataString(signedExpiry)}",
            $"sig={Uri.EscapeDataString(signature)}"
        );

        var url = string.IsNullOrEmpty(blobName)
            ? $"{_baseUrl}/{containerName}?{sasToken}"
            : $"{_baseUrl}/{containerName}/{Uri.EscapeDataString(blobName)}?{sasToken}";

        return ActionResult.Ok(new Dictionary<string, object>
        {
            ["url"] = url,
            ["sasToken"] = sasToken,
            ["expiresAt"] = signedExpiry
        });
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        HttpMethod method,
        string uri,
        byte[]? content,
        Dictionary<string, string>? additionalHeaders,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, uri);

        var dateStr = DateTime.UtcNow.ToString("R");
        request.Headers.Add("x-ms-date", dateStr);
        request.Headers.Add("x-ms-version", ApiVersion);

        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders)
            {
                if (!header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        if (content != null)
        {
            var contentType = additionalHeaders?.GetValueOrDefault("Content-Type") ?? "application/octet-stream";
            request.Content = new ByteArrayContent(content);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }
        else if (method == HttpMethod.Put || method == HttpMethod.Post)
        {
            request.Content = new ByteArrayContent([]);
            request.Content.Headers.ContentLength = 0;
        }

        var authHeader = GenerateAuthorizationHeader(method, uri, dateStr, additionalHeaders, content?.Length ?? 0);
        request.Headers.Authorization = new AuthenticationHeaderValue("SharedKey", authHeader);

        return await _httpClient!.SendAsync(request, ct);
    }

    private string GenerateAuthorizationHeader(
        HttpMethod method,
        string uri,
        string dateStr,
        Dictionary<string, string>? additionalHeaders,
        long contentLength)
    {
        var uriParts = uri.Split('?');
        var resource = uriParts[0];
        var queryParams = uriParts.Length > 1 ? ParseQueryString(uriParts[1]) : new Dictionary<string, string>();

        var canonicalizedHeaders = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-ms-date"] = dateStr,
            ["x-ms-version"] = ApiVersion
        };

        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders.Where(h => h.Key.StartsWith("x-ms-", StringComparison.OrdinalIgnoreCase)))
            {
                canonicalizedHeaders[header.Key.ToLowerInvariant()] = header.Value;
            }
        }

        var canonicalizedHeadersStr = string.Join("\n",
            canonicalizedHeaders.Select(h => $"{h.Key}:{h.Value}"));

        var canonicalizedResource = $"/{_accountName}{resource}";
        if (queryParams.Count > 0)
        {
            var sortedParams = queryParams.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase);
            canonicalizedResource += "\n" + string.Join("\n",
                sortedParams.Select(p => $"{p.Key.ToLowerInvariant()}:{p.Value}"));
        }

        var contentType = additionalHeaders?.GetValueOrDefault("Content-Type") ?? "";

        var stringToSign = string.Join("\n",
            method.Method.ToUpperInvariant(),
            "", "", contentLength > 0 ? contentLength.ToString() : "",
            "", contentType, "", "", "", "", "", "",
            canonicalizedHeadersStr,
            canonicalizedResource);

        using var hmac = new HMACSHA256(Convert.FromBase64String(_accountKey!));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));

        return $"{_accountName}:{signature}";
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
            }
        }
        return result;
    }

    private static async Task<ActionResult> ProcessResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return ActionResult.Ok(new Dictionary<string, object>
            {
                ["status"] = "success",
                ["statusCode"] = (int)response.StatusCode
            });
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        return ActionResult.Fail($"Azure Blob Storage error ({response.StatusCode}): {content}");
    }

    private static async Task<ActionResult> ProcessXmlResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail($"Azure Blob Storage error ({response.StatusCode}): {content}");
        }

        try
        {
            var doc = XDocument.Parse(content);
            var result = XmlToJson(doc.Root!);
            return ActionResult.Ok(result);
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"Failed to parse XML response: {ex.Message}");
        }
    }

    private static Dictionary<string, object> XmlToJson(XElement element)
    {
        var result = new Dictionary<string, object>();

        foreach (var group in element.Elements().GroupBy(e => e.Name.LocalName))
        {
            var items = group.ToList();
            if (items.Count == 1)
            {
                var item = items[0];
                if (item.HasElements)
                {
                    result[group.Key] = XmlToJson(item);
                }
                else
                {
                    result[group.Key] = item.Value;
                }
            }
            else
            {
                result[group.Key] = items.Select(item =>
                    item.HasElements ? XmlToJson(item) : (object)item.Value).ToList();
            }
        }

        return result;
    }

    private static ActionResult ExtractHeadersAsResult(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail($"Azure Blob Storage error ({response.StatusCode})");
        }

        var result = new Dictionary<string, object>();

        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            result[header.Key] = string.Join(", ", header.Value);
        }

        return ActionResult.Ok(result);
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
