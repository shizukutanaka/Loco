// Uncle Bob: "Functions should do one thing"
// John Carmack: "Robust code is better than fast code"

using System.Net;
using System.Text;
using System.Text.Json;

namespace Loco.Core.Practical;

/// <summary>
/// Simple HTTP client with built-in retry and circuit breaker
/// No complex configuration, just sensible defaults
/// </summary>
public class SimpleHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly SimpleCircuitBreaker _circuitBreaker;
    private readonly SimpleMetrics _metrics;
    private readonly int _maxRetries;

    public SimpleHttpClient(HttpClient? httpClient = null, int timeoutSeconds = 30, int maxRetries = 3)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        _circuitBreaker = new SimpleCircuitBreaker();
        _metrics = new SimpleMetrics();
        _maxRetries = maxRetries;
    }

    // Simple GET with automatic JSON deserialization
    public async Task<T?> GetJsonAsync<T>(string url)
    {
        var response = await GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json);
    }

    // Simple GET
    public async Task<HttpResponseMessage> GetAsync(string url)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _metrics.IncrementCounter("http.get");
            return await _httpClient.GetAsync(url);
        });
    }

    // Simple POST with JSON body
    public async Task<HttpResponseMessage> PostJsonAsync<T>(string url, T data)
    {
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        return await ExecuteWithRetryAsync(async () =>
        {
            _metrics.IncrementCounter("http.post");
            return await _httpClient.PostAsync(url, content);
        });
    }

    // Simple PUT with JSON body
    public async Task<HttpResponseMessage> PutJsonAsync<T>(string url, T data)
    {
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        return await ExecuteWithRetryAsync(async () =>
        {
            _metrics.IncrementCounter("http.put");
            return await _httpClient.PutAsync(url, content);
        });
    }

    // Simple DELETE
    public async Task<HttpResponseMessage> DeleteAsync(string url)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _metrics.IncrementCounter("http.delete");
            return await _httpClient.DeleteAsync(url);
        });
    }

    // Execute with retry and circuit breaker
    private async Task<HttpResponseMessage> ExecuteWithRetryAsync(Func<Task<HttpResponseMessage>> operation)
    {
        return await _metrics.MeasureAsync("http.request", async () =>
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                HttpResponseMessage? lastResponse = null;
                Exception? lastException = null;

                for (int attempt = 0; attempt < _maxRetries; attempt++)
                {
                    try
                    {
                        var response = await operation();

                        // Don't retry on success or client errors
                        if (response.IsSuccessStatusCode ||
                            response.StatusCode >= HttpStatusCode.BadRequest &&
                            response.StatusCode < HttpStatusCode.InternalServerError)
                        {
                            _metrics.IncrementCounter($"http.status.{(int)response.StatusCode}");
                            return response;
                        }

                        lastResponse = response;
                    }
                    catch (HttpRequestException ex)
                    {
                        lastException = ex;
                        _metrics.IncrementCounter("http.error");
                    }
                    catch (TaskCanceledException ex)
                    {
                        lastException = ex;
                        _metrics.IncrementCounter("http.timeout");
                    }

                    if (attempt < _maxRetries - 1)
                    {
                        // Simple exponential backoff
                        await Task.Delay(100 * (1 << attempt));
                    }
                }

                if (lastResponse != null)
                    return lastResponse;

                throw new HttpRequestException($"Request failed after {_maxRetries} attempts", lastException);
            });
        });
    }

    // Get metrics
    public Dictionary<string, object> GetMetrics() => _metrics.GetReport();

    // Get circuit breaker status
    public string GetCircuitBreakerStatus() => _circuitBreaker.GetStatus();

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}