// John Carmack: "Make the common case fast and simple"
// Rob Pike: "Errors are values, handle them explicitly"

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Loco.Core.Practical;

/// <summary>
/// Simple API client - REST API calls without complexity
/// Type-safe, retry support, authentication
/// </summary>
public class SimpleApiClient
{
    private readonly HttpClient _httpClient;
    private readonly SimpleLogger _logger;
    private readonly string _baseUrl;
    private readonly Dictionary<string, string> _defaultHeaders = new();

    public SimpleApiClient(string baseUrl, SimpleLogger? logger = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleApiClient));
    }

    // Add default header
    public void AddHeader(string name, string value)
    {
        _defaultHeaders[name] = value;
        _httpClient.DefaultRequestHeaders.Add(name, value);
    }

    // Set bearer token
    public void SetBearerToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // Set basic auth
    public void SetBasicAuth(string username, string password)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    // GET request
    public async Task<T?> GetAsync<T>(string path, Dictionary<string, string>? queryParams = null)
    {
        var url = BuildUrl(path, queryParams);
        _logger.Debug($"GET {url}");

        try
        {
            var response = await _httpClient.GetAsync(url);
            return await HandleResponseAsync<T>(response);
        }
        catch (Exception ex)
        {
            _logger.Error($"GET {url} failed", ex);
            throw;
        }
    }

    // GET as string
    public async Task<string> GetStringAsync(string path, Dictionary<string, string>? queryParams = null)
    {
        var url = BuildUrl(path, queryParams);
        _logger.Debug($"GET {url}");

        try
        {
            return await _httpClient.GetStringAsync(url);
        }
        catch (Exception ex)
        {
            _logger.Error($"GET {url} failed", ex);
            throw;
        }
    }

    // POST request
    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest data)
    {
        var url = _baseUrl + "/" + path.TrimStart('/');
        _logger.Debug($"POST {url}");

        try
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            return await HandleResponseAsync<TResponse>(response);
        }
        catch (Exception ex)
        {
            _logger.Error($"POST {url} failed", ex);
            throw;
        }
    }

    // POST without response
    public async Task<bool> PostAsync<TRequest>(string path, TRequest data)
    {
        var url = _baseUrl + "/" + path.TrimStart('/');
        _logger.Debug($"POST {url}");

        try
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.Error($"POST {url} failed", ex);
            return false;
        }
    }

    // PUT request
    public async Task<TResponse?> PutAsync<TRequest, TResponse>(string path, TRequest data)
    {
        var url = _baseUrl + "/" + path.TrimStart('/');
        _logger.Debug($"PUT {url}");

        try
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(url, content);
            return await HandleResponseAsync<TResponse>(response);
        }
        catch (Exception ex)
        {
            _logger.Error($"PUT {url} failed", ex);
            throw;
        }
    }

    // DELETE request
    public async Task<bool> DeleteAsync(string path)
    {
        var url = _baseUrl + "/" + path.TrimStart('/');
        _logger.Debug($"DELETE {url}");

        try
        {
            var response = await _httpClient.DeleteAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.Error($"DELETE {url} failed", ex);
            return false;
        }
    }

    // PATCH request
    public async Task<TResponse?> PatchAsync<TRequest, TResponse>(string path, TRequest data)
    {
        var url = _baseUrl + "/" + path.TrimStart('/');
        _logger.Debug($"PATCH {url}");

        try
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PatchAsync(url, content);
            return await HandleResponseAsync<TResponse>(response);
        }
        catch (Exception ex)
        {
            _logger.Error($"PATCH {url} failed", ex);
            throw;
        }
    }

    // Upload file
    public async Task<T?> UploadFileAsync<T>(string path, string filePath, string fieldName = "file")
    {
        var url = _baseUrl + "/" + path.TrimStart('/');
        _logger.Debug($"POST {url} (file upload)");

        try
        {
            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            form.Add(fileContent, fieldName, Path.GetFileName(filePath));

            var response = await _httpClient.PostAsync(url, form);
            return await HandleResponseAsync<T>(response);
        }
        catch (Exception ex)
        {
            _logger.Error($"File upload to {url} failed", ex);
            throw;
        }
    }

    // Download file
    public async Task<bool> DownloadFileAsync(string path, string destinationPath)
    {
        var url = _baseUrl + "/" + path.TrimStart('/');
        _logger.Debug($"GET {url} (file download)");

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var bytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(destinationPath, bytes);

            _logger.Info($"Downloaded file to {destinationPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"File download from {url} failed", ex);
            return false;
        }
    }

    private string BuildUrl(string path, Dictionary<string, string>? queryParams)
    {
        var url = _baseUrl + "/" + path.TrimStart('/');

        if (queryParams?.Any() == true)
        {
            var query = string.Join("&", queryParams.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
            url += "?" + query;
        }

        return url;
    }

    private async Task<T?> HandleResponseAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.Warning($"API error {response.StatusCode}: {error}");
            throw new HttpRequestException($"API returned {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

/// <summary>
/// API client with retry support
/// </summary>
public class ResilientApiClient
{
    private readonly SimpleApiClient _client;
    private readonly SimpleRetry _retry;
    private readonly SimpleLogger _logger;

    public ResilientApiClient(string baseUrl, int maxRetries = 3, SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(ResilientApiClient));
        _client = new SimpleApiClient(baseUrl, _logger);
        _retry = new SimpleRetry(maxRetries, initialDelayMs: 100);
    }

    public void AddHeader(string name, string value) => _client.AddHeader(name, value);
    public void SetBearerToken(string token) => _client.SetBearerToken(token);

    public async Task<T?> GetAsync<T>(string path, Dictionary<string, string>? queryParams = null)
    {
        return await _retry.ExecuteAsync(async () =>
            await _client.GetAsync<T>(path, queryParams));
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest data)
    {
        return await _retry.ExecuteAsync(async () =>
            await _client.PostAsync<TRequest, TResponse>(path, data));
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}

/// <summary>
/// Typed API client
/// </summary>
public abstract class TypedApiClient
{
    protected readonly SimpleApiClient Client;
    protected readonly SimpleLogger Logger;

    protected TypedApiClient(string baseUrl, SimpleLogger? logger = null)
    {
        Logger = logger ?? SimpleLoggerFactory.GetLogger(GetType().Name);
        Client = new SimpleApiClient(baseUrl, Logger);
    }

    protected async Task<T?> GetAsync<T>(string path) => await Client.GetAsync<T>(path);
    protected async Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest data) =>
        await Client.PostAsync<TRequest, TResponse>(path, data);

    public void Dispose()
    {
        Client.Dispose();
    }
}

/// <summary>
/// Example: GitHub API client
/// </summary>
public class GitHubApiClient : TypedApiClient
{
    public GitHubApiClient(string token) : base("https://api.github.com")
    {
        Client.SetBearerToken(token);
        Client.AddHeader("User-Agent", "SimpleApiClient");
    }

    public async Task<GitHubUser?> GetUserAsync(string username)
    {
        return await GetAsync<GitHubUser>($"users/{username}");
    }

    public async Task<List<GitHubRepo>?> GetUserReposAsync(string username)
    {
        return await GetAsync<List<GitHubRepo>>($"users/{username}/repos");
    }

    public record GitHubUser(string Login, string Name, string Bio, int PublicRepos);
    public record GitHubRepo(string Name, string Description, int StargazersCount, string Language);
}

/// <summary>
/// Example: REST API wrapper
/// </summary>
public class UserApiClient
{
    private readonly SimpleApiClient _client;

    public UserApiClient(string baseUrl)
    {
        _client = new SimpleApiClient(baseUrl);
    }

    public async Task<User?> GetUserAsync(int id)
    {
        return await _client.GetAsync<User>($"users/{id}");
    }

    public async Task<List<User>?> GetUsersAsync(int page = 1, int pageSize = 10)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };
        return await _client.GetAsync<List<User>>("users", queryParams);
    }

    public async Task<User?> CreateUserAsync(CreateUserRequest request)
    {
        return await _client.PostAsync<CreateUserRequest, User>("users", request);
    }

    public async Task<User?> UpdateUserAsync(int id, UpdateUserRequest request)
    {
        return await _client.PutAsync<UpdateUserRequest, User>($"users/{id}", request);
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        return await _client.DeleteAsync($"users/{id}");
    }

    public record User(int Id, string Name, string Email, DateTime CreatedAt);
    public record CreateUserRequest(string Name, string Email, string Password);
    public record UpdateUserRequest(string? Name, string? Email);
}

/// <summary>
/// Example usage
/// </summary>
public class ApiClientExamples
{
    public static async Task Examples()
    {
        // Simple usage
        var client = new SimpleApiClient("https://api.example.com");
        client.AddHeader("X-Api-Key", "your-api-key");

        var user = await client.GetAsync<dynamic>("users/1");
        Console.WriteLine($"User: {user}");

        // With retry
        var resilientClient = new ResilientApiClient("https://api.example.com");
        resilientClient.SetBearerToken("your-token");

        var data = await resilientClient.GetAsync<dynamic>("data");

        // Typed client
        var userApi = new UserApiClient("https://api.example.com");
        var users = await userApi.GetUsersAsync(page: 1, pageSize: 20);

        // GitHub API
        var github = new GitHubApiClient("your-github-token");
        var githubUser = await github.GetUserAsync("octocat");
        var repos = await github.GetUserReposAsync("octocat");
    }
}