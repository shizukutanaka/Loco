// John Carmack: "Efficient code is not about being clever; it's about being clear"
// Rob Pike: "When in doubt, use brute force"

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Loco.Core.Integrations.Core;
using Loco.Core.Practical;

namespace Loco.Core.Integrations.Connectors;

/// <summary>
/// AWS S3 connector for object storage operations
/// Implements S3 REST API with AWS Signature V4
/// </summary>
public sealed class AwsS3Connector : ConnectorBase
{
    private HttpClient? _httpClient;
    private string _accessKey = "";
    private string _secretKey = "";
    private string _region = "";
    private string _endpoint = "";

    public override string Id => "s3";
    public override string Name => "AWS S3";
    public override string Description => "Amazon S3 object storage for file uploads, downloads, and management";
    public override string Version => "1.0.0";
    public override ConnectorCategory Category => ConnectorCategory.Storage;

    public override ConnectorCapabilities Capabilities => new()
    {
        SupportsActions = true,
        SupportsTriggers = true,
        SupportsWebhooks = true, // S3 Event Notifications
        SupportsBatching = true,
        SupportsStreaming = true,
        RateLimitPerMinute = 3500, // S3 limit per prefix
        DefaultTimeout = TimeSpan.FromMinutes(5)
    };

    public override AuthenticationConfig AuthConfig => new()
    {
        Type = AuthenticationType.ApiKey,
        RequiredCredentials =
        [
            new() { Name = "accessKeyId", Label = "Access Key ID", Type = ParameterType.String, Required = true,
                Description = "AWS Access Key ID" },
            new() { Name = "secretAccessKey", Label = "Secret Access Key", Type = ParameterType.Password, Required = true,
                Description = "AWS Secret Access Key" },
            new() { Name = "region", Label = "Region", Type = ParameterType.String, Required = true,
                Description = "AWS region (e.g., us-east-1, eu-west-1)" }
        ]
    };

    public override IReadOnlyList<ConfigParameter> ConfigParameters =>
    [
        new() { Name = "endpoint", Label = "Custom Endpoint", Type = ParameterType.String,
            Description = "Custom S3-compatible endpoint (for MinIO, DigitalOcean Spaces, etc.)" },
        new() { Name = "forcePathStyle", Label = "Force Path Style", Type = ParameterType.Boolean, DefaultValue = false,
            Description = "Use path-style URLs instead of virtual-hosted-style" }
    ];

    public override IReadOnlyList<ConnectorAction> Actions =>
    [
        new()
        {
            Id = "upload",
            Name = "Upload File",
            Description = "Upload a file to S3",
            Parameters =
            [
                new() { Name = "bucket", Type = ParameterType.String, Required = true, Description = "S3 bucket name" },
                new() { Name = "key", Type = ParameterType.String, Required = true, Description = "Object key (path)" },
                new() { Name = "filePath", Type = ParameterType.String, Required = true, Description = "Local file path" },
                new() { Name = "contentType", Type = ParameterType.String, Description = "MIME type (auto-detected if not specified)" },
                new() { Name = "metadata", Type = ParameterType.Json, Description = "Custom metadata (key-value pairs)" },
                new() { Name = "acl", Type = ParameterType.Select, DefaultValue = "private",
                    Options =
                    [
                        new() { Label = "Private", Value = "private" },
                        new() { Label = "Public Read", Value = "public-read" },
                        new() { Label = "Authenticated Read", Value = "authenticated-read" }
                    ]}
            ],
            RetryConfig = new RetryConfig { MaxAttempts = 3 }
        },
        new()
        {
            Id = "uploadContent",
            Name = "Upload Content",
            Description = "Upload string or JSON content directly to S3",
            Parameters =
            [
                new() { Name = "bucket", Type = ParameterType.String, Required = true },
                new() { Name = "key", Type = ParameterType.String, Required = true },
                new() { Name = "content", Type = ParameterType.String, Required = true, Description = "Content to upload" },
                new() { Name = "contentType", Type = ParameterType.String, DefaultValue = "text/plain" },
                new() { Name = "metadata", Type = ParameterType.Json }
            ]
        },
        new()
        {
            Id = "download",
            Name = "Download File",
            Description = "Download a file from S3",
            Parameters =
            [
                new() { Name = "bucket", Type = ParameterType.String, Required = true },
                new() { Name = "key", Type = ParameterType.String, Required = true },
                new() { Name = "savePath", Type = ParameterType.String, Required = true, Description = "Local path to save file" }
            ]
        },
        new()
        {
            Id = "getContent",
            Name = "Get Content",
            Description = "Get object content as string",
            Parameters =
            [
                new() { Name = "bucket", Type = ParameterType.String, Required = true },
                new() { Name = "key", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "delete",
            Name = "Delete Object",
            Description = "Delete an object from S3",
            Parameters =
            [
                new() { Name = "bucket", Type = ParameterType.String, Required = true },
                new() { Name = "key", Type = ParameterType.String, Required = true }
            ],
            RequiresConfirmation = true
        },
        new()
        {
            Id = "list",
            Name = "List Objects",
            Description = "List objects in a bucket",
            Parameters =
            [
                new() { Name = "bucket", Type = ParameterType.String, Required = true },
                new() { Name = "prefix", Type = ParameterType.String, Description = "Filter by prefix" },
                new() { Name = "maxKeys", Type = ParameterType.Number, DefaultValue = 1000 },
                new() { Name = "delimiter", Type = ParameterType.String, DefaultValue = "/", Description = "Hierarchy delimiter" }
            ]
        },
        new()
        {
            Id = "copy",
            Name = "Copy Object",
            Description = "Copy an object within or between buckets",
            Parameters =
            [
                new() { Name = "sourceBucket", Type = ParameterType.String, Required = true },
                new() { Name = "sourceKey", Type = ParameterType.String, Required = true },
                new() { Name = "destBucket", Type = ParameterType.String, Required = true },
                new() { Name = "destKey", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "getPresignedUrl",
            Name = "Get Presigned URL",
            Description = "Generate a presigned URL for temporary access",
            Parameters =
            [
                new() { Name = "bucket", Type = ParameterType.String, Required = true },
                new() { Name = "key", Type = ParameterType.String, Required = true },
                new() { Name = "expirationMinutes", Type = ParameterType.Number, DefaultValue = 60 },
                new() { Name = "method", Type = ParameterType.Select, DefaultValue = "GET",
                    Options =
                    [
                        new() { Label = "GET (Download)", Value = "GET" },
                        new() { Label = "PUT (Upload)", Value = "PUT" }
                    ]}
            ]
        },
        new()
        {
            Id = "headObject",
            Name = "Get Object Metadata",
            Description = "Get object metadata without downloading",
            Parameters =
            [
                new() { Name = "bucket", Type = ParameterType.String, Required = true },
                new() { Name = "key", Type = ParameterType.String, Required = true }
            ]
        },
        new()
        {
            Id = "listBuckets",
            Name = "List Buckets",
            Description = "List all buckets in the account",
            Parameters = Array.Empty<ActionParameter>()
        }
    ];

    public override IReadOnlyList<ConnectorTrigger> Triggers =>
    [
        new()
        {
            Id = "objectCreated",
            Name = "Object Created",
            Description = "Triggered when a new object is created",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "bucket", Type = ParameterType.String, Required = true },
                new() { Name = "prefix", Type = ParameterType.String, Description = "Filter by prefix" },
                new() { Name = "suffix", Type = ParameterType.String, Description = "Filter by suffix (e.g., .jpg)" }
            ]
        },
        new()
        {
            Id = "objectDeleted",
            Name = "Object Deleted",
            Description = "Triggered when an object is deleted",
            Type = TriggerType.Webhook,
            ConfigParameters =
            [
                new() { Name = "bucket", Type = ParameterType.String, Required = true },
                new() { Name = "prefix", Type = ParameterType.String }
            ]
        }
    ];

    public override async Task InitializeAsync(ConnectorConfiguration config, CancellationToken ct = default)
    {
        _accessKey = config.GetCredentialString("accessKeyId")!;
        _secretKey = config.GetCredentialString("secretAccessKey")!;
        _region = config.GetCredentialString("region")!;
        _endpoint = config.GetSettingString("endpoint") ?? $"https://s3.{_region}.amazonaws.com";

        // Dispose any previous client before replacing it. InitializeAsync can run more
        // than once for the same cached connector instance (e.g. ConnectorRegistry.
        // GetInitializedConnectorAsync on credential rotation); overwriting _httpClient
        // unconditionally previously leaked the old HttpClient and its socket handler.
        _httpClient?.Dispose();
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

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
            "upload" => await UploadFileAsync(parameters, ct),
            "uploadContent" => await UploadContentAsync(parameters, ct),
            "download" => await DownloadFileAsync(parameters, ct),
            "getContent" => await GetContentAsync(parameters, ct),
            "delete" => await DeleteObjectAsync(parameters, ct),
            "list" => await ListObjectsAsync(parameters, ct),
            "copy" => await CopyObjectAsync(parameters, ct),
            "getPresignedUrl" => await GetPresignedUrlAsync(parameters, ct),
            "headObject" => await HeadObjectAsync(parameters, ct),
            "listBuckets" => await ListBucketsAsync(ct),
            _ => ActionResult.Fail($"Unknown action: {action.Id}")
        };
    }

    private async Task<ActionResult> UploadFileAsync(ActionParameters parameters, CancellationToken ct)
    {
        var bucket = parameters.GetString("bucket")!;
        var key = parameters.GetString("key")!;
        var filePath = parameters.GetString("filePath")!;

        if (!File.Exists(filePath))
        {
            return ActionResult.Fail($"File not found: {filePath}", "FILE_NOT_FOUND");
        }

        var contentType = parameters.GetString("contentType") ??
            MimeTypes.GetMimeType(Path.GetExtension(filePath));
        var acl = parameters.GetString("acl") ?? "private";
        var metadata = parameters.Get<Dictionary<string, string>>("metadata");

        var content = await File.ReadAllBytesAsync(filePath, ct);
        return await PutObjectAsync(bucket, key, content, contentType, acl, metadata, ct);
    }

    private async Task<ActionResult> UploadContentAsync(ActionParameters parameters, CancellationToken ct)
    {
        var bucket = parameters.GetString("bucket")!;
        var key = parameters.GetString("key")!;
        var contentStr = parameters.GetString("content")!;
        var contentType = parameters.GetString("contentType") ?? "text/plain";
        var metadata = parameters.Get<Dictionary<string, string>>("metadata");

        var content = Encoding.UTF8.GetBytes(contentStr);
        return await PutObjectAsync(bucket, key, content, contentType, "private", metadata, ct);
    }

    private async Task<ActionResult> PutObjectAsync(
        string bucket,
        string key,
        byte[] content,
        string contentType,
        string acl,
        Dictionary<string, string>? metadata,
        CancellationToken ct)
    {
        var url = $"{_endpoint}/{bucket}/{key}";
        using var request = new HttpRequestMessage(HttpMethod.Put, url);

        request.Content = new ByteArrayContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        request.Headers.Add("x-amz-acl", acl);

        if (metadata != null)
        {
            foreach (var kvp in metadata)
            {
                request.Headers.Add($"x-amz-meta-{kvp.Key}", kvp.Value);
            }
        }

        SignRequest(request, "s3", content);

        var response = await _httpClient!.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return ActionResult.Fail($"Upload failed: {error}", ((int)response.StatusCode).ToString());
        }

        return ActionResult.Ok(new
        {
            bucket,
            key,
            size = content.Length,
            etag = response.Headers.ETag?.Tag,
            url = $"{_endpoint}/{bucket}/{key}"
        });
    }

    private async Task<ActionResult> DownloadFileAsync(ActionParameters parameters, CancellationToken ct)
    {
        var bucket = parameters.GetString("bucket")!;
        var key = parameters.GetString("key")!;
        var savePath = parameters.GetString("savePath")!;

        var url = $"{_endpoint}/{bucket}/{key}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        SignRequest(request, "s3");

        var response = await _httpClient!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail($"Download failed: {response.StatusCode}", ((int)response.StatusCode).ToString());
        }

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

    private async Task<ActionResult> GetContentAsync(ActionParameters parameters, CancellationToken ct)
    {
        var bucket = parameters.GetString("bucket")!;
        var key = parameters.GetString("key")!;

        var url = $"{_endpoint}/{bucket}/{key}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        SignRequest(request, "s3");

        var response = await _httpClient!.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail($"Get failed: {response.StatusCode}", ((int)response.StatusCode).ToString());
        }

        var content = await response.Content.ReadAsStringAsync(ct);

        return ActionResult.Ok(new
        {
            content,
            contentType = response.Content.Headers.ContentType?.MediaType,
            size = response.Content.Headers.ContentLength
        });
    }

    private async Task<ActionResult> DeleteObjectAsync(ActionParameters parameters, CancellationToken ct)
    {
        var bucket = parameters.GetString("bucket")!;
        var key = parameters.GetString("key")!;

        var url = $"{_endpoint}/{bucket}/{key}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        SignRequest(request, "s3");

        var response = await _httpClient!.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NoContent)
        {
            return ActionResult.Fail($"Delete failed: {response.StatusCode}");
        }

        return ActionResult.Ok(new { deleted = true, bucket, key });
    }

    private async Task<ActionResult> ListObjectsAsync(ActionParameters parameters, CancellationToken ct)
    {
        var bucket = parameters.GetString("bucket")!;
        var prefix = parameters.GetString("prefix") ?? "";
        var maxKeys = parameters.GetInt("maxKeys", 1000);
        var delimiter = parameters.GetString("delimiter") ?? "/";

        var query = $"list-type=2&max-keys={maxKeys}";
        if (!string.IsNullOrEmpty(prefix)) query += $"&prefix={Uri.EscapeDataString(prefix)}";
        if (!string.IsNullOrEmpty(delimiter)) query += $"&delimiter={Uri.EscapeDataString(delimiter)}";

        var url = $"{_endpoint}/{bucket}?{query}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        SignRequest(request, "s3");

        var response = await _httpClient!.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail($"List failed: {response.StatusCode}");
        }

        var xml = await response.Content.ReadAsStringAsync(ct);
        var objects = ParseListObjectsResponse(xml);

        return ActionResult.Ok(new { objects, bucket, prefix });
    }

    private async Task<ActionResult> CopyObjectAsync(ActionParameters parameters, CancellationToken ct)
    {
        var sourceBucket = parameters.GetString("sourceBucket")!;
        var sourceKey = parameters.GetString("sourceKey")!;
        var destBucket = parameters.GetString("destBucket")!;
        var destKey = parameters.GetString("destKey")!;

        var url = $"{_endpoint}/{destBucket}/{destKey}";
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Add("x-amz-copy-source", $"/{sourceBucket}/{sourceKey}");
        SignRequest(request, "s3");

        var response = await _httpClient!.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail($"Copy failed: {response.StatusCode}");
        }

        return ActionResult.Ok(new
        {
            copied = true,
            source = $"{sourceBucket}/{sourceKey}",
            destination = $"{destBucket}/{destKey}"
        });
    }

    private Task<ActionResult> GetPresignedUrlAsync(ActionParameters parameters, CancellationToken ct)
    {
        var bucket = parameters.GetString("bucket")!;
        var key = parameters.GetString("key")!;
        var expirationMinutes = parameters.GetInt("expirationMinutes", 60);
        var method = parameters.GetString("method") ?? "GET";

        var expiration = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes);
        var url = GeneratePresignedUrl(bucket, key, method, expiration);

        return Task.FromResult(ActionResult.Ok(new
        {
            url,
            method,
            expiresAt = expiration.ToString("o"),
            expiresInMinutes = expirationMinutes
        }));
    }

    private async Task<ActionResult> HeadObjectAsync(ActionParameters parameters, CancellationToken ct)
    {
        var bucket = parameters.GetString("bucket")!;
        var key = parameters.GetString("key")!;

        var url = $"{_endpoint}/{bucket}/{key}";
        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        SignRequest(request, "s3");

        var response = await _httpClient!.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return ActionResult.Ok(new { exists = false, bucket, key });
            }
            return ActionResult.Fail($"Head failed: {response.StatusCode}");
        }

        return ActionResult.Ok(new
        {
            exists = true,
            bucket,
            key,
            contentType = response.Content.Headers.ContentType?.MediaType,
            contentLength = response.Content.Headers.ContentLength,
            lastModified = response.Content.Headers.LastModified?.ToString("o"),
            etag = response.Headers.ETag?.Tag
        });
    }

    private async Task<ActionResult> ListBucketsAsync(CancellationToken ct)
    {
        var url = _endpoint;
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        SignRequest(request, "s3");

        var response = await _httpClient!.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            return ActionResult.Fail($"List buckets failed: {response.StatusCode}");
        }

        var xml = await response.Content.ReadAsStringAsync(ct);
        var buckets = ParseListBucketsResponse(xml);

        return ActionResult.Ok(new { buckets });
    }

    private void SignRequest(HttpRequestMessage request, string service, byte[]? payload = null)
    {
        var now = DateTime.UtcNow;
        var dateStamp = now.ToString("yyyyMMdd");
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");

        request.Headers.Add("x-amz-date", amzDate);
        request.Headers.Add("x-amz-content-sha256", GetPayloadHash(payload));

        var canonicalRequest = CreateCanonicalRequest(request, payload);
        var stringToSign = CreateStringToSign(amzDate, dateStamp, service, canonicalRequest);
        var signature = CalculateSignature(dateStamp, service, stringToSign);

        var authorization = $"AWS4-HMAC-SHA256 Credential={_accessKey}/{dateStamp}/{_region}/{service}/aws4_request, " +
                          $"SignedHeaders={GetSignedHeaders(request)}, Signature={signature}";

        request.Headers.TryAddWithoutValidation("Authorization", authorization);
    }

    private string GeneratePresignedUrl(string bucket, string key, string method, DateTimeOffset expiration)
    {
        var now = DateTime.UtcNow;
        var dateStamp = now.ToString("yyyyMMdd");
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");
        var expiresIn = (int)(expiration - DateTimeOffset.UtcNow).TotalSeconds;

        var credential = $"{_accessKey}/{dateStamp}/{_region}/s3/aws4_request";
        var canonicalUri = $"/{bucket}/{key}";

        var queryParams = new SortedDictionary<string, string>
        {
            ["X-Amz-Algorithm"] = "AWS4-HMAC-SHA256",
            ["X-Amz-Credential"] = credential,
            ["X-Amz-Date"] = amzDate,
            ["X-Amz-Expires"] = expiresIn.ToString(),
            ["X-Amz-SignedHeaders"] = "host"
        };

        var canonicalQueryString = string.Join("&",
            queryParams.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        var host = new Uri(_endpoint).Host;
        var canonicalHeaders = $"host:{host}\n";
        var signedHeaders = "host";

        var canonicalRequest = $"{method}\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\nUNSIGNED-PAYLOAD";
        var stringToSign = CreateStringToSign(amzDate, dateStamp, "s3", canonicalRequest);
        var signature = CalculateSignature(dateStamp, "s3", stringToSign);

        return $"{_endpoint}{canonicalUri}?{canonicalQueryString}&X-Amz-Signature={signature}";
    }

    private string CreateCanonicalRequest(HttpRequestMessage request, byte[]? payload)
    {
        var uri = request.RequestUri!;
        var canonicalUri = uri.AbsolutePath;
        var canonicalQueryString = string.Join("&",
            (uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            .OrderBy(q => q));

        var canonicalHeaders = new StringBuilder();
        var signedHeadersList = new List<string>();

        // Always include host
        canonicalHeaders.AppendLine($"host:{uri.Host}");
        signedHeadersList.Add("host");

        foreach (var header in request.Headers.OrderBy(h => h.Key.ToLowerInvariant()))
        {
            if (header.Key.StartsWith("x-amz-", StringComparison.OrdinalIgnoreCase))
            {
                canonicalHeaders.AppendLine($"{header.Key.ToLowerInvariant()}:{string.Join(",", header.Value)}");
                signedHeadersList.Add(header.Key.ToLowerInvariant());
            }
        }

        var signedHeaders = string.Join(";", signedHeadersList.OrderBy(h => h));

        return $"{request.Method}\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{GetPayloadHash(payload)}";
    }

    private string CreateStringToSign(string amzDate, string dateStamp, string service, string canonicalRequest)
    {
        var scope = $"{dateStamp}/{_region}/{service}/aws4_request";
        var hashedRequest = Hash(canonicalRequest);
        return $"AWS4-HMAC-SHA256\n{amzDate}\n{scope}\n{hashedRequest}";
    }

    private string CalculateSignature(string dateStamp, string service, string stringToSign)
    {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes($"AWS4{_secretKey}"), dateStamp);
        var kRegion = HmacSha256(kDate, _region);
        var kService = HmacSha256(kRegion, service);
        var kSigning = HmacSha256(kService, "aws4_request");

        return Convert.ToHexString(HmacSha256(kSigning, stringToSign)).ToLowerInvariant();
    }

    private static string GetSignedHeaders(HttpRequestMessage request)
    {
        var headers = new List<string> { "host" };
        headers.AddRange(request.Headers
            .Where(h => h.Key.StartsWith("x-amz-", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.Key.ToLowerInvariant()));
        return string.Join(";", headers.OrderBy(h => h));
    }

    private static string GetPayloadHash(byte[]? payload)
    {
        if (payload == null || payload.Length == 0)
        {
            return Hash("");
        }
        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static byte[] HmacSha256(byte[] key, string data) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));

    private static List<object> ParseListObjectsResponse(string xml)
    {
        // Simple XML parsing for S3 ListObjects response
        var objects = new List<object>();

        // Parse <Contents> elements
        var contentRegex = new System.Text.RegularExpressions.Regex(
            @"<Contents>.*?<Key>(.+?)</Key>.*?<Size>(\d+)</Size>.*?<LastModified>(.+?)</LastModified>.*?</Contents>",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        foreach (System.Text.RegularExpressions.Match match in contentRegex.Matches(xml))
        {
            objects.Add(new
            {
                key = match.Groups[1].Value,
                size = long.Parse(match.Groups[2].Value),
                lastModified = match.Groups[3].Value
            });
        }

        return objects;
    }

    private static List<object> ParseListBucketsResponse(string xml)
    {
        var buckets = new List<object>();

        var bucketRegex = new System.Text.RegularExpressions.Regex(
            @"<Bucket>.*?<Name>(.+?)</Name>.*?<CreationDate>(.+?)</CreationDate>.*?</Bucket>",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        foreach (System.Text.RegularExpressions.Match match in bucketRegex.Matches(xml))
        {
            buckets.Add(new
            {
                name = match.Groups[1].Value,
                creationDate = match.Groups[2].Value
            });
        }

        return buckets;
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
