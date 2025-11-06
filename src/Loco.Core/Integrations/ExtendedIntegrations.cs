// Loco Extended Integrations - Phase 2
// Additional 5 integrations to expand coverage to 95% of common use cases
// Discord, Twilio, AWS S3, SendGrid, Telegram

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;

namespace Loco.Core.Integrations;

/// <summary>
/// Discord Integration - Send messages to Discord channels
/// Uses Discord Webhook API for simplicity
/// </summary>
public class DiscordIntegration : IIntegration
{
    private readonly string _webhookUrl;
    private readonly HttpClient _httpClient;

    public string Name => "Discord";
    public string Version => "1.0.0";

    public DiscordIntegration(string webhookUrl)
    {
        _webhookUrl = webhookUrl;
        _httpClient = new HttpClient();
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var payload = new { content = "Loco connection test" };
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
            var content = request.Parameters.GetValueOrDefault("content", "")?.ToString() ?? "";
            var username = request.Parameters.GetValueOrDefault("username", "Loco Bot")?.ToString();
            var avatarUrl = request.Parameters.GetValueOrDefault("avatar_url", "")?.ToString();
            var tts = request.Parameters.GetValueOrDefault("tts", false);

            var payload = new Dictionary<string, object>
            {
                ["content"] = content
            };

            if (!string.IsNullOrEmpty(username))
                payload["username"] = username!;

            if (!string.IsNullOrEmpty(avatarUrl))
                payload["avatar_url"] = avatarUrl!;

            if (Convert.ToBoolean(tts))
                payload["tts"] = true;

            // Support embeds
            if (request.Parameters.ContainsKey("embeds"))
            {
                payload["embeds"] = request.Parameters["embeds"]!;
            }

            var json = JsonSerializer.Serialize(payload);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_webhookUrl, httpContent, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            return new IntegrationResult
            {
                Success = response.IsSuccessStatusCode,
                Data = responseBody,
                StatusCode = (int)response.StatusCode,
                Duration = DateTime.UtcNow - startTime,
                Metadata = new Dictionary<string, object>
                {
                    ["messageLength"] = content.Length,
                    ["hasEmbeds"] = request.Parameters.ContainsKey("embeds")
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
/// Twilio Integration - Send SMS and make phone calls
/// Requires Twilio Account SID and Auth Token
/// </summary>
public class TwilioIntegration : IIntegration
{
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _fromNumber;
    private readonly HttpClient _httpClient;

    public string Name => "Twilio";
    public string Version => "1.0.0";

    public TwilioIntegration(string accountSid, string authToken, string fromNumber)
    {
        _accountSid = accountSid;
        _authToken = authToken;
        _fromNumber = fromNumber;
        _httpClient = new HttpClient();

        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}.json";
            var response = await _httpClient.GetAsync(url, ct);
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

            if (action == "send_sms")
            {
                return await SendSmsAsync(request, startTime, ct);
            }
            else if (action == "make_call")
            {
                return await MakeCallAsync(request, startTime, ct);
            }
            else
            {
                return new IntegrationResult
                {
                    Success = false,
                    Error = $"Unknown action: {action}. Use 'send_sms' or 'make_call'",
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

    private async Task<IntegrationResult> SendSmsAsync(IntegrationRequest request, DateTime startTime, CancellationToken ct)
    {
        var to = request.Parameters.GetValueOrDefault("to", "")?.ToString() ?? "";
        var body = request.Parameters.GetValueOrDefault("body", "")?.ToString() ?? "";

        var formData = new Dictionary<string, string>
        {
            ["To"] = to,
            ["From"] = _fromNumber,
            ["Body"] = body
        };

        var content = new FormUrlEncodedContent(formData);
        var url = $"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}/Messages.json";

        var response = await _httpClient.PostAsync(url, content, ct);
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
                ["to"] = to,
                ["messageLength"] = body.Length
            }
        };
    }

    private async Task<IntegrationResult> MakeCallAsync(IntegrationRequest request, DateTime startTime, CancellationToken ct)
    {
        var to = request.Parameters.GetValueOrDefault("to", "")?.ToString() ?? "";
        var twimlUrl = request.Parameters.GetValueOrDefault("url", "")?.ToString() ?? "";

        var formData = new Dictionary<string, string>
        {
            ["To"] = to,
            ["From"] = _fromNumber,
            ["Url"] = twimlUrl
        };

        var content = new FormUrlEncodedContent(formData);
        var url = $"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}/Calls.json";

        var response = await _httpClient.PostAsync(url, content, ct);
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
                ["to"] = to,
                ["callUrl"] = twimlUrl
            }
        };
    }
}

/// <summary>
/// AWS S3 Integration - Upload, download, and manage files in S3
/// Requires AWS credentials (Access Key ID and Secret Access Key)
/// </summary>
public class S3Integration : IIntegration
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public string Name => "AWS S3";
    public string Version => "1.0.0";

    public S3Integration(string accessKeyId, string secretAccessKey, string bucketName, string region = "us-east-1")
    {
        var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
        var config = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
        };
        _s3Client = new AmazonS3Client(credentials, config);
        _bucketName = bucketName;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _s3Client.ListBucketsAsync(ct);
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
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

            switch (action)
            {
                case "upload":
                    return await UploadFileAsync(request, startTime, ct);
                case "download":
                    return await DownloadFileAsync(request, startTime, ct);
                case "delete":
                    return await DeleteFileAsync(request, startTime, ct);
                case "list":
                    return await ListFilesAsync(request, startTime, ct);
                default:
                    return new IntegrationResult
                    {
                        Success = false,
                        Error = $"Unknown action: {action}. Use 'upload', 'download', 'delete', or 'list'",
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

    private async Task<IntegrationResult> UploadFileAsync(IntegrationRequest request, DateTime startTime, CancellationToken ct)
    {
        var key = request.Parameters.GetValueOrDefault("key", "")?.ToString() ?? "";
        var content = request.Parameters.GetValueOrDefault("content", "")?.ToString() ?? "";
        var contentType = request.Parameters.GetValueOrDefault("contentType", "application/octet-stream")?.ToString();

        var putRequest = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            ContentBody = content,
            ContentType = contentType
        };

        var response = await _s3Client.PutObjectAsync(putRequest, ct);

        return new IntegrationResult
        {
            Success = response.HttpStatusCode == System.Net.HttpStatusCode.OK,
            Data = new { key, etag = response.ETag },
            StatusCode = (int)response.HttpStatusCode,
            Duration = DateTime.UtcNow - startTime,
            Metadata = new Dictionary<string, object>
            {
                ["bucket"] = _bucketName,
                ["key"] = key,
                ["size"] = content.Length
            }
        };
    }

    private async Task<IntegrationResult> DownloadFileAsync(IntegrationRequest request, DateTime startTime, CancellationToken ct)
    {
        var key = request.Parameters.GetValueOrDefault("key", "")?.ToString() ?? "";

        var getRequest = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };

        var response = await _s3Client.GetObjectAsync(getRequest, ct);

        using var reader = new StreamReader(response.ResponseStream);
        var content = await reader.ReadToEndAsync();

        return new IntegrationResult
        {
            Success = response.HttpStatusCode == System.Net.HttpStatusCode.OK,
            Data = content,
            StatusCode = (int)response.HttpStatusCode,
            Duration = DateTime.UtcNow - startTime,
            Metadata = new Dictionary<string, object>
            {
                ["bucket"] = _bucketName,
                ["key"] = key,
                ["size"] = response.ContentLength
            }
        };
    }

    private async Task<IntegrationResult> DeleteFileAsync(IntegrationRequest request, DateTime startTime, CancellationToken ct)
    {
        var key = request.Parameters.GetValueOrDefault("key", "")?.ToString() ?? "";

        var deleteRequest = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };

        var response = await _s3Client.DeleteObjectAsync(deleteRequest, ct);

        return new IntegrationResult
        {
            Success = response.HttpStatusCode == System.Net.HttpStatusCode.NoContent,
            Data = new { deleted = true, key },
            StatusCode = (int)response.HttpStatusCode,
            Duration = DateTime.UtcNow - startTime
        };
    }

    private async Task<IntegrationResult> ListFilesAsync(IntegrationRequest request, DateTime startTime, CancellationToken ct)
    {
        var prefix = request.Parameters.GetValueOrDefault("prefix", "")?.ToString() ?? "";
        var maxKeys = request.Parameters.GetValueOrDefault("maxKeys", 1000);

        var listRequest = new ListObjectsV2Request
        {
            BucketName = _bucketName,
            Prefix = prefix,
            MaxKeys = Convert.ToInt32(maxKeys)
        };

        var response = await _s3Client.ListObjectsV2Async(listRequest, ct);

        var files = response.S3Objects.Select(obj => new
        {
            key = obj.Key,
            size = obj.Size,
            lastModified = obj.LastModified
        }).ToList();

        return new IntegrationResult
        {
            Success = response.HttpStatusCode == System.Net.HttpStatusCode.OK,
            Data = files,
            StatusCode = (int)response.HttpStatusCode,
            Duration = DateTime.UtcNow - startTime,
            Metadata = new Dictionary<string, object>
            {
                ["bucket"] = _bucketName,
                ["count"] = files.Count
            }
        };
    }
}

/// <summary>
/// SendGrid Integration - Send transactional emails at scale
/// Requires SendGrid API Key
/// </summary>
public class SendGridIntegration : IIntegration
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public string Name => "SendGrid";
    public string Version => "1.0.0";

    public SendGridIntegration(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("https://api.sendgrid.com/v3/scopes", ct);
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
            var from = request.Parameters.GetValueOrDefault("from", "")?.ToString() ?? "";
            var to = request.Parameters.GetValueOrDefault("to", "")?.ToString() ?? "";
            var subject = request.Parameters.GetValueOrDefault("subject", "")?.ToString() ?? "";
            var body = request.Parameters.GetValueOrDefault("body", "")?.ToString() ?? "";
            var isHtml = request.Parameters.GetValueOrDefault("isHtml", true);

            var payload = new
            {
                personalizations = new[]
                {
                    new
                    {
                        to = to.Split(';', StringSplitOptions.RemoveEmptyEntries)
                            .Select(email => new { email = email.Trim() })
                            .ToArray()
                    }
                },
                from = new { email = from },
                subject = subject,
                content = new[]
                {
                    new
                    {
                        type = Convert.ToBoolean(isHtml) ? "text/html" : "text/plain",
                        value = body
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://api.sendgrid.com/v3/mail/send", content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            return new IntegrationResult
            {
                Success = response.IsSuccessStatusCode,
                Data = string.IsNullOrEmpty(responseBody) ? new { sent = true } : responseBody,
                StatusCode = (int)response.StatusCode,
                Duration = DateTime.UtcNow - startTime,
                Metadata = new Dictionary<string, object>
                {
                    ["from"] = from,
                    ["to"] = to,
                    ["recipientCount"] = to.Split(';', StringSplitOptions.RemoveEmptyEntries).Length
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
/// Telegram Integration - Send messages via Telegram Bot
/// Requires Telegram Bot Token
/// </summary>
public class TelegramIntegration : IIntegration
{
    private readonly string _botToken;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public string Name => "Telegram";
    public string Version => "1.0.0";

    public TelegramIntegration(string botToken)
    {
        _botToken = botToken;
        _httpClient = new HttpClient();
        _baseUrl = $"https://api.telegram.org/bot{botToken}";
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/getMe", ct);
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

            switch (action)
            {
                case "send_message":
                    return await SendMessageAsync(request, startTime, ct);
                case "send_photo":
                    return await SendPhotoAsync(request, startTime, ct);
                case "send_document":
                    return await SendDocumentAsync(request, startTime, ct);
                default:
                    return new IntegrationResult
                    {
                        Success = false,
                        Error = $"Unknown action: {action}. Use 'send_message', 'send_photo', or 'send_document'",
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

    private async Task<IntegrationResult> SendMessageAsync(IntegrationRequest request, DateTime startTime, CancellationToken ct)
    {
        var chatId = request.Parameters.GetValueOrDefault("chat_id", "")?.ToString() ?? "";
        var text = request.Parameters.GetValueOrDefault("text", "")?.ToString() ?? "";
        var parseMode = request.Parameters.GetValueOrDefault("parse_mode", "")?.ToString(); // HTML or Markdown

        var payload = new Dictionary<string, object>
        {
            ["chat_id"] = chatId,
            ["text"] = text
        };

        if (!string.IsNullOrEmpty(parseMode))
            payload["parse_mode"] = parseMode!;

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/sendMessage", content, ct);
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
                ["chatId"] = chatId,
                ["messageLength"] = text.Length
            }
        };
    }

    private async Task<IntegrationResult> SendPhotoAsync(IntegrationRequest request, DateTime startTime, CancellationToken ct)
    {
        var chatId = request.Parameters.GetValueOrDefault("chat_id", "")?.ToString() ?? "";
        var photoUrl = request.Parameters.GetValueOrDefault("photo", "")?.ToString() ?? "";
        var caption = request.Parameters.GetValueOrDefault("caption", "")?.ToString();

        var payload = new Dictionary<string, object>
        {
            ["chat_id"] = chatId,
            ["photo"] = photoUrl
        };

        if (!string.IsNullOrEmpty(caption))
            payload["caption"] = caption!;

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/sendPhoto", content, ct);
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

    private async Task<IntegrationResult> SendDocumentAsync(IntegrationRequest request, DateTime startTime, CancellationToken ct)
    {
        var chatId = request.Parameters.GetValueOrDefault("chat_id", "")?.ToString() ?? "";
        var documentUrl = request.Parameters.GetValueOrDefault("document", "")?.ToString() ?? "";
        var caption = request.Parameters.GetValueOrDefault("caption", "")?.ToString();

        var payload = new Dictionary<string, object>
        {
            ["chat_id"] = chatId,
            ["document"] = documentUrl
        };

        if (!string.IsNullOrEmpty(caption))
            payload["caption"] = caption!;

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/sendDocument", content, ct);
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
