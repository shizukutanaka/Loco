#nullable enable

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Loco.Api.Idempotency;

/// <summary>
/// Idempotent API Design Patterns
/// Ensures that duplicate requests are processed only once
/// Guarantees "at-most-once" semantics for mutations
/// </summary>

/// <summary>
/// Idempotency key - unique identifier for request
/// </summary>
public class IdempotencyKey
{
    public string Value { get; set; } = string.Empty;
    public override string ToString() => Value;

    public static IdempotencyKey Generate() => new() { Value = Guid.NewGuid().ToString() };
}

/// <summary>
/// Idempotency response - cached result of previous execution
/// </summary>
public class IdempotencyResponse
{
    /// <summary>
    /// Unique key for this idempotent operation
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// HTTP status code of original response
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Serialized response body
    /// </summary>
    public string ResponseBody { get; set; } = string.Empty;

    /// <summary>
    /// When the response was cached
    /// </summary>
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Request signature (for verification)
    /// </summary>
    public string RequestSignature { get; set; } = string.Empty;

    /// <summary>
    /// Response headers (subset)
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();
}

/// <summary>
/// Idempotency store - persists request results
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Stores response for idempotency key
    /// </summary>
    Task StoreAsync(IdempotencyResponse response);

    /// <summary>
    /// Retrieves cached response
    /// </summary>
    Task<IdempotencyResponse?> GetAsync(string idempotencyKey);

    /// <summary>
    /// Checks if key exists
    /// </summary>
    Task<bool> ExistsAsync(string idempotencyKey);

    /// <summary>
    /// Removes entry (after expiration)
    /// </summary>
    Task RemoveAsync(string idempotencyKey);
}

/// <summary>
/// In-memory idempotency store (for demo)
/// Production: Use Redis or database
/// </summary>
public class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyResponse> _cache = new();
    private readonly ILogger<InMemoryIdempotencyStore> _logger;
    private readonly TimeSpan _expirationTime = TimeSpan.FromHours(24);

    public InMemoryIdempotencyStore(ILogger<InMemoryIdempotencyStore> logger)
    {
        _logger = logger;
        // Start cleanup task
        _ = Task.Run(CleanupExpiredAsync);
    }

    public Task StoreAsync(IdempotencyResponse response)
    {
        _cache[response.IdempotencyKey] = response;
        _logger.LogInformation("Idempotency response stored: {Key}", response.IdempotencyKey);
        return Task.CompletedTask;
    }

    public Task<IdempotencyResponse?> GetAsync(string idempotencyKey)
    {
        if (_cache.TryGetValue(idempotencyKey, out var response))
        {
            // Check expiration
            if (DateTime.UtcNow - response.CachedAt > _expirationTime)
            {
                _cache.TryRemove(idempotencyKey, out _);
                return Task.FromResult<IdempotencyResponse?>(null);
            }

            _logger.LogDebug("Idempotency cache hit: {Key}", idempotencyKey);
            return Task.FromResult<IdempotencyResponse?>(response);
        }

        return Task.FromResult<IdempotencyResponse?>(null);
    }

    public Task<bool> ExistsAsync(string idempotencyKey)
    {
        return Task.FromResult(_cache.ContainsKey(idempotencyKey));
    }

    public Task RemoveAsync(string idempotencyKey)
    {
        _cache.TryRemove(idempotencyKey, out _);
        return Task.CompletedTask;
    }

    private async Task CleanupExpiredAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromHours(1)).ConfigureAwait(false);

            var expired = _cache
                .Where(kvp => DateTime.UtcNow - kvp.Value.CachedAt > _expirationTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expired)
            {
                _cache.TryRemove(key, out _);
            }

            _logger.LogInformation("Idempotency store cleanup: removed {Count} expired entries", expired.Count);
        }
    }
}

/// <summary>
/// Request signature for idempotency validation
/// Ensures requests are identical
/// </summary>
public class RequestSignature
{
    /// <summary>
    /// Computes signature from request
    /// </summary>
    public static string ComputeSignature(
        string method,
        string path,
        string? body,
        Dictionary<string, string>? headers = null)
    {
        var signatureData = $"{method}:{path}:{body ?? ""}";

        // Include relevant headers in signature
        if (headers != null)
        {
            var relevantHeaders = headers
                .Where(h => h.Key.StartsWith("X-"))
                .OrderBy(h => h.Key)
                .Select(h => $"{h.Key}={h.Value}");

            signatureData += ":" + string.Join(",", relevantHeaders);
        }

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(signatureData));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verifies signature matches
    /// </summary>
    public static bool VerifySignature(
        string method,
        string path,
        string? body,
        string expectedSignature,
        Dictionary<string, string>? headers = null)
    {
        var computed = ComputeSignature(method, path, body, headers);
        return computed == expectedSignature;
    }
}

/// <summary>
/// Idempotency service - main API
/// </summary>
public class IdempotencyService
{
    private readonly IIdempotencyStore _store;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(
        IIdempotencyStore store,
        ILogger<IdempotencyService> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Processes request with idempotency guarantee
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        string idempotencyKey,
        string method,
        string path,
        string? body,
        Func<Task<T>> operation,
        Func<T, int> getStatusCode,
        Func<T, string> serializeResponse)
    {
        // Check if request was processed before
        var cached = await _store.GetAsync(idempotencyKey).ConfigureAwait(false);
        if (cached != null)
        {
            _logger.LogInformation(
                "Duplicate request detected, returning cached response: {Key}",
                idempotencyKey);

            // Verify request signature matches
            var currentSignature = RequestSignature.ComputeSignature(method, path, body);
            if (cached.RequestSignature != currentSignature)
            {
                throw new IdempotencyMismatchException(
                    "Request signature mismatch - request parameters differ from original");
            }

            // Return cached response (would be deserialized in real implementation)
            return (T?)JsonSerializer.Deserialize<T>(cached.ResponseBody)
                ?? throw new InvalidOperationException("Failed to deserialize cached response");
        }

        // Execute operation
        _logger.LogInformation("Executing idempotent operation: {Key}", idempotencyKey);

        try
        {
            var result = await operation().ConfigureAwait(false);
            var statusCode = getStatusCode(result);
            var responseBody = serializeResponse(result);

            // Cache response
            var idempotencyResponse = new IdempotencyResponse
            {
                IdempotencyKey = idempotencyKey,
                StatusCode = statusCode,
                ResponseBody = responseBody,
                RequestSignature = RequestSignature.ComputeSignature(method, path, body),
                CachedAt = DateTime.UtcNow,
                Headers = new Dictionary<string, string>
                {
                    ["X-Idempotency-Key"] = idempotencyKey
                }
            };

            await _store.StoreAsync(idempotencyResponse).ConfigureAwait(false);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Idempotent operation failed: {Key}", idempotencyKey);
            throw;
        }
    }

    /// <summary>
    /// Checks if key was processed
    /// </summary>
    public async Task<bool> IsProcessedAsync(string idempotencyKey)
    {
        return await _store.ExistsAsync(idempotencyKey).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets cached response if exists
    /// </summary>
    public async Task<IdempotencyResponse?> GetCachedResponseAsync(string idempotencyKey)
    {
        return await _store.GetAsync(idempotencyKey).ConfigureAwait(false);
    }
}

/// <summary>
/// Idempotency middleware for ASP.NET Core
/// </summary>
public class IdempotencyMiddleware
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IIdempotencyStore store)
    {
        // Only apply to POST, PUT, PATCH (write operations)
        if (!IsWriteOperation(context.Request.Method))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Extract idempotency key
        if (!context.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var idempotencyKeyValue))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Idempotency-Key header is required"
            }).ConfigureAwait(false);
            return;
        }

        var idempotencyKey = idempotencyKeyValue.ToString();

        // Validate key format (UUID)
        if (!Guid.TryParse(idempotencyKey, out _))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Idempotency-Key must be a valid UUID"
            }).ConfigureAwait(false);
            return;
        }

        // Check if already processed
        var cached = await store.GetAsync(idempotencyKey).ConfigureAwait(false);
        if (cached != null)
        {
            _logger.LogInformation("Idempotent request detected: {Key}", idempotencyKey);

            // Return cached response
            context.Response.StatusCode = cached.StatusCode;
            context.Response.Headers.Add("X-Idempotency-Replay", "true");

            foreach (var header in cached.Headers)
            {
                context.Response.Headers.Add(header.Key, header.Value);
            }

            await context.Response.WriteAsync(cached.ResponseBody).ConfigureAwait(false);
            return;
        }

        // Store original response
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context).ConfigureAwait(false);

            // Read response
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var body = await new StreamReader(context.Response.Body).ReadToEndAsync()
                .ConfigureAwait(false);
            context.Response.Body.Seek(0, SeekOrigin.Begin);

            // Cache response
            var idempotencyResponse = new IdempotencyResponse
            {
                IdempotencyKey = idempotencyKey,
                StatusCode = context.Response.StatusCode,
                ResponseBody = body,
                RequestSignature = RequestSignature.ComputeSignature(
                    context.Request.Method,
                    context.Request.Path,
                    context.Request.Query.ToString()),
                CachedAt = DateTime.UtcNow
            };

            await store.StoreAsync(idempotencyResponse).ConfigureAwait(false);

            // Copy response back
            await responseBody.CopyToAsync(originalBodyStream).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private bool IsWriteOperation(string method)
    {
        return method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
               method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
               method.Equals("PATCH", StringComparison.OrdinalIgnoreCase) ||
               method.Equals("DELETE", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Request idempotency filter for controllers
/// </summary>
public class IdempotentOperationAttribute : Attribute
{
    public string? Name { get; set; }
}

/// <summary>
/// Idempotency result filter
/// </summary>
public class IdempotencyResultFilter : IAsyncResultFilter
{
    private readonly IIdempotencyStore _store;
    private readonly ILogger<IdempotencyResultFilter> _logger;

    public IdempotencyResultFilter(IIdempotencyStore store, ILogger<IdempotencyResultFilter> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var idempotencyKey = context.HttpContext.Request.Headers["Idempotency-Key"].ToString();

        if (!string.IsNullOrEmpty(idempotencyKey) && Guid.TryParse(idempotencyKey, out _))
        {
            // Serialize result
            if (context.Result is ObjectResult objectResult)
            {
                var responseBody = JsonSerializer.Serialize(objectResult.Value);

                var idempotencyResponse = new IdempotencyResponse
                {
                    IdempotencyKey = idempotencyKey,
                    StatusCode = objectResult.StatusCode ?? StatusCodes.Status200OK,
                    ResponseBody = responseBody,
                    CachedAt = DateTime.UtcNow,
                    RequestSignature = RequestSignature.ComputeSignature(
                        context.HttpContext.Request.Method,
                        context.HttpContext.Request.Path,
                        ""),
                    Headers = new Dictionary<string, string>
                    {
                        ["X-Idempotency-Key"] = idempotencyKey
                    }
                };

                await _store.StoreAsync(idempotencyResponse).ConfigureAwait(false);
                _logger.LogDebug("Idempotency response cached: {Key}", idempotencyKey);
            }
        }

        await next().ConfigureAwait(false);
    }

    public Task OnResultExecutingAsync(ResultExecutingContext context)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Exceptions
/// </summary>
public class IdempotencyMismatchException : Exception
{
    public IdempotencyMismatchException(string message) : base(message) { }
}

/// <summary>
/// Extension methods
/// </summary>
public static class IdempotencyExtensions
{
    public static IServiceCollection AddIdempotency(this IServiceCollection services)
    {
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        services.AddSingleton<IdempotencyService>();
        return services;
    }

    public static IApplicationBuilder UseIdempotency(this IApplicationBuilder app)
    {
        return app.UseMiddleware<IdempotencyMiddleware>();
    }
}

/// <summary>
/// Example idempotent API endpoint
/// </summary>
public class OrderRequest
{
    public string? OrderId { get; set; }
    public string? CustomerId { get; set; }
    public decimal Amount { get; set; }
}

public class OrderResponse
{
    public string? OrderId { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

[ApiController]
[Route("api/orders")]
public class IdempotentOrdersController : ControllerBase
{
    private readonly IdempotencyService _idempotencyService;

    public IdempotentOrdersController(IdempotencyService idempotencyService)
    {
        _idempotencyService = idempotencyService;
    }

    /// <summary>
    /// Creates order with idempotency guarantee
    /// Client must provide Idempotency-Key header
    /// </summary>
    [HttpPost]
    [IdempotentOperation(Name = "CreateOrder")]
    public async Task<IActionResult> CreateOrderAsync(
        [FromBody] OrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
            return BadRequest("Idempotency-Key header required");

        if (!Guid.TryParse(idempotencyKey, out _))
            return BadRequest("Idempotency-Key must be a valid UUID");

        var response = await _idempotencyService.ExecuteAsync(
            idempotencyKey,
            HttpContext.Request.Method,
            HttpContext.Request.Path,
            JsonSerializer.Serialize(request),
            async () =>
            {
                // Actual order creation logic
                await Task.Delay(100); // Simulate DB operation

                return new OrderResponse
                {
                    OrderId = Guid.NewGuid().ToString(),
                    Status = "Created",
                    CreatedAt = DateTime.UtcNow
                };
            },
            r => StatusCodes.Status201Created,
            r => JsonSerializer.Serialize(r)
        );

        return CreatedAtAction(nameof(CreateOrderAsync), new { id = response.OrderId }, response);
    }

    /// <summary>
    /// Checks if order was created
    /// </summary>
    [HttpHead("{idempotencyKey}")]
    public async Task<IActionResult> IsProcessedAsync(string idempotencyKey)
    {
        var isProcessed = await _idempotencyService.IsProcessedAsync(idempotencyKey);
        return isProcessed ? Ok() : NotFound();
    }
}

/// <summary>
/// Example client using idempotency
/// </summary>
public class IdempotentApiClient
{
    private readonly HttpClient _httpClient;

    public IdempotentApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Creates order with automatic retry using idempotency
    /// </summary>
    public async Task<OrderResponse?> CreateOrderAsync(OrderRequest request)
    {
        // Generate idempotency key once
        var idempotencyKey = Guid.NewGuid().ToString();

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var httpRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.example.com/orders")
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(request),
                        Encoding.UTF8,
                        "application/json")
                };

                // Add idempotency key (same on retry!)
                httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);

                var response = await _httpClient.SendAsync(httpRequest);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<OrderResponse>(content);
                }

                // Check if response was from cache
                if (response.Headers.Contains("X-Idempotency-Replay"))
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<OrderResponse>(content);
                }

                // Retry on failure
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
            catch (HttpRequestException ex) when (attempt < 2)
            {
                // Retry
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }

        return null;
    }
}
