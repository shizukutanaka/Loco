#nullable enable

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Loco.Api.BFF;

/// <summary>
/// API Composition & Backend for Frontend (BFF) Patterns
/// Aggregates and transforms data from multiple microservices
/// </summary>

/// <summary>
/// Composite request - aggregates multiple downstream calls
/// </summary>
public interface ICompositeRequest
{
    /// <summary>
    /// Gets the composition result
    /// </summary>
    Task<T> ComposeAsync<T>(IServiceProvider serviceProvider);
}

/// <summary>
/// API composition result
/// </summary>
public class CompositionResult
{
    public int StatusCode { get; set; }
    public object? Data { get; set; }
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public long ExecutionTimeMs { get; set; }
}

/// <summary>
/// Downstream API call definition
/// </summary>
public class DownstreamCall
{
    public string ServiceName { get; set; } = string.Empty;
    public string? Url { get; set; }
    public HttpMethod Method { get; set; } = HttpMethod.Get;
    public object? Body { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
    public bool Optional { get; set; } // Failure doesn't block composition
}

/// <summary>
/// Downstream response wrapper
/// </summary>
public class DownstreamResponse
{
    public string ServiceName { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public Exception? Exception { get; set; }
    public long ExecutionTimeMs { get; set; }
    public bool Success { get; set; }
}

/// <summary>
/// API composition executor - orchestrates multiple API calls
/// </summary>
public class ApiCompositionExecutor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiCompositionExecutor> _logger;

    public ApiCompositionExecutor(HttpClient httpClient, ILogger<ApiCompositionExecutor> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Executes multiple API calls in parallel
    /// Returns aggregated response
    /// </summary>
    public async Task<CompositionResult> ExecuteAsync(
        IEnumerable<DownstreamCall> calls,
        Func<List<DownstreamResponse>, object> aggregator)
    {
        var stopwatch = Stopwatch.StartNew();
        var responses = new List<DownstreamResponse>();

        try
        {
            // Execute all calls in parallel
            var tasks = calls.Select(call => ExecuteDownstreamCallAsync(call)).ToList();
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            responses.AddRange(results);

            stopwatch.Stop();

            // Check for critical failures
            var failedRequired = responses
                .Where(r => !r.Success && r.Exception != null)
                .Where(r => !calls.First(c => c.ServiceName == r.ServiceName).Optional)
                .ToList();

            if (failedRequired.Any())
            {
                return new CompositionResult
                {
                    StatusCode = StatusCodes.Status503ServiceUnavailable,
                    Errors = failedRequired
                        .Select(r => $"{r.ServiceName}: {r.Exception?.Message}")
                        .ToList(),
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

            // Aggregate responses
            var data = aggregator(responses);

            return new CompositionResult
            {
                StatusCode = StatusCodes.Status200OK,
                Data = data,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Metadata = new Dictionary<string, object>
                {
                    ["ResponseCount"] = responses.Count,
                    ["SuccessCount"] = responses.Count(r => r.Success)
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "API composition failed");

            return new CompositionResult
            {
                StatusCode = StatusCodes.Status500InternalServerError,
                Errors = new List<string> { ex.Message },
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private async Task<DownstreamResponse> ExecuteDownstreamCallAsync(DownstreamCall call)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new DownstreamResponse
        {
            ServiceName = call.ServiceName
        };

        try
        {
            using var cts = new CancellationTokenSource(call.Timeout);
            using var request = new HttpRequestMessage(call.Method, call.Url)
            {
                Content = call.Body != null
                    ? new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(call.Body),
                        System.Text.Encoding.UTF8,
                        "application/json")
                    : null
            };

            // Add headers
            foreach (var header in call.Headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            var httpResponse = await _httpClient.SendAsync(request, cts.Token)
                .ConfigureAwait(false);

            stopwatch.Stop();

            response.StatusCode = (int)httpResponse.StatusCode;
            response.ResponseBody = await httpResponse.Content.ReadAsStringAsync()
                .ConfigureAwait(false);
            response.Success = httpResponse.IsSuccessStatusCode;
            response.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation(
                "Downstream call completed: {Service} ({Status}) in {Time}ms",
                call.ServiceName,
                httpResponse.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            response.Exception = new TimeoutException($"Service {call.ServiceName} timed out");
            response.Success = false;
            response.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogWarning(
                "Downstream call timed out: {Service} after {Time}ms",
                call.ServiceName,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            response.Exception = ex;
            response.Success = false;
            response.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogError(
                ex,
                "Downstream call failed: {Service}",
                call.ServiceName);
        }

        return response;
    }
}

/// <summary>
/// Backend for Frontend (BFF) base controller
/// Implements composition and transformation for specific frontend
/// </summary>
public abstract class BffBaseController : ControllerBase
{
    protected readonly ApiCompositionExecutor _compositionExecutor;
    protected readonly ILogger _logger;

    public BffBaseController(
        ApiCompositionExecutor compositionExecutor,
        ILogger logger)
    {
        _compositionExecutor = compositionExecutor;
        _logger = logger;
    }

    /// <summary>
    /// Executes composition and returns result
    /// </summary>
    protected async Task<IActionResult> ComposeAsync(
        IEnumerable<DownstreamCall> calls,
        Func<List<DownstreamResponse>, object> aggregator)
    {
        var result = await _compositionExecutor.ExecuteAsync(calls, aggregator)
            .ConfigureAwait(false);

        if (!result.Errors.Any())
        {
            Response.Headers.Add("X-Composition-Time", result.ExecutionTimeMs.ToString());
            return StatusCode(result.StatusCode, result.Data);
        }

        return StatusCode(result.StatusCode, new
        {
            errors = result.Errors,
            metadata = result.Metadata
        });
    }
}

/// <summary>
/// Example: Mobile BFF for workflow management
/// Composes data from multiple services optimized for mobile
/// </summary>
[ApiController]
[Route("api/mobile/bff")]
public class MobileWorkflowBffController : BffBaseController
{
    public MobileWorkflowBffController(
        ApiCompositionExecutor compositionExecutor,
        ILogger<MobileWorkflowBffController> logger)
        : base(compositionExecutor, logger)
    {
    }

    /// <summary>
    /// Gets workflow dashboard for mobile
    /// Aggregates: workflows + recent executions + stats
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardAsync()
    {
        var calls = new[]
        {
            new DownstreamCall
            {
                ServiceName = "WorkflowService",
                Url = "https://workflow-service/api/workflows/active",
                Method = HttpMethod.Get
            },
            new DownstreamCall
            {
                ServiceName = "ExecutionService",
                Url = "https://execution-service/api/executions/recent?limit=10",
                Method = HttpMethod.Get
            },
            new DownstreamCall
            {
                ServiceName = "AnalyticsService",
                Url = "https://analytics-service/api/metrics/dashboard",
                Method = HttpMethod.Get,
                Optional = true // Failures don't block
            }
        };

        return await ComposeAsync(calls, responses =>
        {
            var workflowResponse = responses.FirstOrDefault(r => r.ServiceName == "WorkflowService");
            var executionResponse = responses.FirstOrDefault(r => r.ServiceName == "ExecutionService");
            var analyticsResponse = responses.FirstOrDefault(r => r.ServiceName == "AnalyticsService");

            var workflows = workflowResponse?.Success
                ? System.Text.Json.JsonSerializer.Deserialize<dynamic>(workflowResponse.ResponseBody ?? "{}")
                : null;

            var executions = executionResponse?.Success
                ? System.Text.Json.JsonSerializer.Deserialize<dynamic>(executionResponse.ResponseBody ?? "{}")
                : null;

            var analytics = analyticsResponse?.Success
                ? System.Text.Json.JsonSerializer.Deserialize<dynamic>(analyticsResponse.ResponseBody ?? "{}")
                : null;

            return new
            {
                workflows,
                recentExecutions = executions,
                analytics,
                summary = new
                {
                    activeWorkflows = (workflows as dynamic)?.Count ?? 0,
                    recentExecutions = (executions as dynamic)?.Count ?? 0
                }
            };
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets workflow details with all related data
    /// Composes: workflow + steps + current execution + events
    /// </summary>
    [HttpGet("workflows/{workflowId}")]
    public async Task<IActionResult> GetWorkflowDetailsAsync(string workflowId)
    {
        var calls = new[]
        {
            new DownstreamCall
            {
                ServiceName = "WorkflowService",
                Url = $"https://workflow-service/api/workflows/{workflowId}",
                Method = HttpMethod.Get
            },
            new DownstreamCall
            {
                ServiceName = "StepService",
                Url = $"https://step-service/api/workflows/{workflowId}/steps",
                Method = HttpMethod.Get
            },
            new DownstreamCall
            {
                ServiceName = "ExecutionService",
                Url = $"https://execution-service/api/workflows/{workflowId}/current",
                Method = HttpMethod.Get
            },
            new DownstreamCall
            {
                ServiceName = "EventService",
                Url = $"https://event-service/api/workflows/{workflowId}/events?limit=20",
                Method = HttpMethod.Get,
                Optional = true
            }
        };

        return await ComposeAsync(calls, responses =>
        {
            return new
            {
                workflow = GetResponseData(responses, "WorkflowService"),
                steps = GetResponseData(responses, "StepService"),
                currentExecution = GetResponseData(responses, "ExecutionService"),
                recentEvents = GetResponseData(responses, "EventService")
            };
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets web-optimized view (includes pagination)
    /// </summary>
    [HttpGet("workflows/list")]
    public async Task<IActionResult> ListWorkflowsAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var calls = new[]
        {
            new DownstreamCall
            {
                ServiceName = "WorkflowService",
                Url = $"https://workflow-service/api/workflows?page={page}&pageSize={pageSize}",
                Method = HttpMethod.Get
            },
            new DownstreamCall
            {
                ServiceName = "AnalyticsService",
                Url = "https://analytics-service/api/metrics/workflow-stats",
                Method = HttpMethod.Get,
                Optional = true
            }
        };

        return await ComposeAsync(calls, responses =>
        {
            var workflows = GetResponseData(responses, "WorkflowService");
            var stats = GetResponseData(responses, "AnalyticsService");

            return new
            {
                workflows,
                stats,
                pagination = new { page, pageSize }
            };
        }).ConfigureAwait(false);
    }

    private dynamic? GetResponseData(List<DownstreamResponse> responses, string serviceName)
    {
        var response = responses.FirstOrDefault(r => r.ServiceName == serviceName);
        if (response?.Success != true)
            return null;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<dynamic>(
                response.ResponseBody ?? "{}");
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Example: Admin BFF for analytics and reporting
/// </summary>
[ApiController]
[Route("api/admin/bff")]
public class AdminAnalyticsBffController : BffBaseController
{
    public AdminAnalyticsBffController(
        ApiCompositionExecutor compositionExecutor,
        ILogger<AdminAnalyticsBffController> logger)
        : base(compositionExecutor, logger)
    {
    }

    /// <summary>
    /// Gets comprehensive analytics dashboard
    /// Aggregates from multiple analytics sources
    /// </summary>
    [HttpGet("analytics/comprehensive")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetComprehensiveAnalyticsAsync(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var calls = new[]
        {
            new DownstreamCall
            {
                ServiceName = "PerformanceService",
                Url = $"https://analytics-service/api/performance?from={from}&to={to}",
                Method = HttpMethod.Get
            },
            new DownstreamCall
            {
                ServiceName = "UsageService",
                Url = $"https://analytics-service/api/usage?from={from}&to={to}",
                Method = HttpMethod.Get
            },
            new DownstreamCall
            {
                ServiceName = "ErrorService",
                Url = $"https://analytics-service/api/errors?from={from}&to={to}",
                Method = HttpMethod.Get
            },
            new DownstreamCall
            {
                ServiceName = "AuditService",
                Url = $"https://audit-service/api/audit-log?limit=100",
                Method = HttpMethod.Get
            }
        };

        return await ComposeAsync(calls, responses =>
        {
            return new
            {
                performance = GetResponseData(responses, "PerformanceService"),
                usage = GetResponseData(responses, "UsageService"),
                errors = GetResponseData(responses, "ErrorService"),
                auditLog = GetResponseData(responses, "AuditService")
            };
        }).ConfigureAwait(false);
    }

    private dynamic? GetResponseData(List<DownstreamResponse> responses, string serviceName)
    {
        var response = responses.FirstOrDefault(r => r.ServiceName == serviceName);
        return response?.Success == true
            ? System.Text.Json.JsonSerializer.Deserialize<dynamic>(response.ResponseBody ?? "{}")
            : null;
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class ApiCompositionExtensions
{
    public static IServiceCollection AddApiComposition(this IServiceCollection services)
    {
        services.AddSingleton<ApiCompositionExecutor>();
        services.AddHttpClient<ApiCompositionExecutor>();
        return services;
    }
}
