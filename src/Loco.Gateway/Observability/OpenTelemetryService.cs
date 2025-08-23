using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Loco.Gateway.Observability;

/// <summary>
/// OpenTelemetry distributed tracing and observability service
/// </summary>
public static class OpenTelemetryService
{
    private static readonly string ServiceName = "Loco";
    private static readonly string ServiceVersion = "1.0.0";
    
    // Activity sources for tracing
    public static readonly ActivitySource GatewayActivitySource = new("Loco.Gateway");
    public static readonly ActivitySource AuthActivitySource = new("Loco.Auth");
    public static readonly ActivitySource DataActivitySource = new("Loco.Data");
    public static readonly ActivitySource WorkflowActivitySource = new("Loco.Workflow");
    
    // Meters for metrics
    public static readonly Meter GatewayMeter = new("Loco.Gateway", "1.0.0");
    public static readonly Meter PerformanceMeter = new("Loco.Performance", "1.0.0");
    public static readonly Meter BusinessMeter = new("Loco.Business", "1.0.0");

    /// <summary>
    /// Configure OpenTelemetry services
    /// </summary>
    public static IServiceCollection AddOpenTelemetryObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var otlpEndpoint = configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";
        var environment = configuration["Environment"] ?? "Production";
        
        // Configure resource
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: ServiceName,
                serviceVersion: ServiceVersion,
                serviceInstanceId: Environment.MachineName)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = environment,
                ["host.name"] = Environment.MachineName,
                ["os.type"] = Environment.OSVersion.Platform.ToString(),
                ["process.runtime.name"] = ".NET",
                ["process.runtime.version"] = Environment.Version.ToString()
            });

        // Configure tracing
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .SetSampler(new TraceIdRatioBasedSampler(
                        configuration.GetValue<double>("OpenTelemetry:SamplingRatio", 1.0)))
                    .AddSource(GatewayActivitySource.Name)
                    .AddSource(AuthActivitySource.Name)
                    .AddSource(DataActivitySource.Name)
                    .AddSource(WorkflowActivitySource.Name)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = httpContext =>
                        {
                            // Filter out health checks and metrics endpoints
                            var path = httpContext.Request.Path.Value ?? "";
                            return !path.Contains("/health") && 
                                   !path.Contains("/metrics") &&
                                   !path.Contains("/ready");
                        };
                        options.RecordException = true;
                        options.EnrichWithHttpRequest = (activity, httpRequest) =>
                        {
                            activity.SetTag("http.request.body.size", httpRequest.ContentLength);
                            activity.SetTag("http.user_agent", httpRequest.Headers["User-Agent"]);
                            activity.SetTag("client.ip", httpRequest.HttpContext.Connection.RemoteIpAddress);
                        };
                        options.EnrichWithHttpResponse = (activity, httpResponse) =>
                        {
                            activity.SetTag("http.response.body.size", httpResponse.ContentLength);
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.FilterHttpRequestMessage = httpRequestMessage =>
                        {
                            // Don't trace calls to telemetry backends
                            var host = httpRequestMessage.RequestUri?.Host ?? "";
                            return !host.Contains("telemetry") && 
                                   !host.Contains("jaeger") &&
                                   !host.Contains("zipkin");
                        };
                        options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                        {
                            activity.SetTag("http.request.method", httpRequestMessage.Method);
                            activity.SetTag("http.request.url", httpRequestMessage.RequestUri?.ToString());
                        };
                        options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
                        {
                            activity.SetTag("http.response.status_code", (int)httpResponseMessage.StatusCode);
                        };
                    })
                    .AddEntityFrameworkCoreInstrumentation(options =>
                    {
                        options.SetDbStatementForText = true;
                        options.SetDbStatementForStoredProcedure = true;
                        options.EnrichWithIDbCommand = (activity, command) =>
                        {
                            activity.SetTag("db.name", command.Connection?.Database);
                            activity.SetTag("db.command.timeout", command.CommandTimeout);
                        };
                    })
                    .AddRedisInstrumentation(options =>
                    {
                        options.SetVerboseDatabaseStatements = true;
                        options.EnrichActivityWithTimingEvents = true;
                    })
                    .AddCustomInstrumentation()
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
                        options.Headers = configuration["OpenTelemetry:Headers"];
                        options.TimeoutMilliseconds = 
                            configuration.GetValue<int>("OpenTelemetry:TimeoutMs", 10000);
                    })
                    .AddJaegerExporter(options =>
                    {
                        options.AgentHost = configuration["Jaeger:AgentHost"] ?? "localhost";
                        options.AgentPort = configuration.GetValue<int>("Jaeger:AgentPort", 6831);
                        options.MaxPayloadSizeInBytes = 
                            configuration.GetValue<int>("Jaeger:MaxPayloadSize", 65000);
                    })
                    .AddZipkinExporter(options =>
                    {
                        options.Endpoint = new Uri(
                            configuration["Zipkin:Endpoint"] ?? "http://localhost:9411/api/v2/spans");
                        options.UseShortTraceIds = 
                            configuration.GetValue<bool>("Zipkin:UseShortTraceIds", false);
                    })
                    .AddConsoleExporter(options =>
                    {
                        options.Targets = ConsoleExporterOutputTargets.Debug;
                    });
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter(GatewayMeter.Name)
                    .AddMeter(PerformanceMeter.Name)
                    .AddMeter(BusinessMeter.Name)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddCustomMetrics()
                    .AddView("http.server.request.duration",
                        new ExplicitBucketHistogramConfiguration
                        {
                            Boundaries = new double[] { 0, 5, 10, 25, 50, 75, 100, 250, 500, 1000, 2500, 5000, 10000 }
                        })
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
                    })
                    .AddPrometheusExporter(options =>
                    {
                        options.StartHttpListener = true;
                        options.HttpListenerPrefixes = new[] 
                        { 
                            $"http://+:{configuration.GetValue<int>("Prometheus:Port", 9090)}/metrics/" 
                        };
                    })
                    .AddConsoleExporter();
            });

        // Configure logging
        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(resourceBuilder);
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.ParseStateValues = true;
                options.AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri(otlpEndpoint);
                    otlpOptions.Protocol = OtlpExportProtocol.Grpc;
                });
            });
        });

        // Add custom services
        services.AddSingleton<ITracingService, TracingService>();
        services.AddSingleton<IMetricsService, MetricsService>();
        services.AddSingleton<ICorrelationIdGenerator, CorrelationIdGenerator>();
        services.AddScoped<TraceContextPropagator>();
        
        return services;
    }

    /// <summary>
    /// Add custom instrumentation
    /// </summary>
    private static TracerProviderBuilder AddCustomInstrumentation(this TracerProviderBuilder builder)
    {
        return builder.AddSource("Custom.*");
    }

    /// <summary>
    /// Add custom metrics
    /// </summary>
    private static MeterProviderBuilder AddCustomMetrics(this MeterProviderBuilder builder)
    {
        // Request counter
        var requestCounter = GatewayMeter.CreateCounter<long>(
            "gateway_requests_total",
            description: "Total number of requests processed");

        // Response time histogram
        var responseTimeHistogram = GatewayMeter.CreateHistogram<double>(
            "gateway_response_time_ms",
            unit: "ms",
            description: "Response time in milliseconds");

        // Active connections gauge
        var activeConnectionsGauge = GatewayMeter.CreateObservableGauge(
            "gateway_active_connections",
            () => new Measurement<int>(TracingService.ActiveConnections),
            description: "Number of active connections");

        // Error rate
        var errorRate = PerformanceMeter.CreateObservableGauge(
            "gateway_error_rate",
            () => new Measurement<double>(MetricsService.CalculateErrorRate()),
            unit: "%",
            description: "Error rate percentage");

        // Business metrics
        var workflowsExecuted = BusinessMeter.CreateCounter<long>(
            "workflows_executed_total",
            description: "Total number of workflows executed");

        var automationRulesTriggered = BusinessMeter.CreateCounter<long>(
            "automation_rules_triggered_total",
            description: "Total number of automation rules triggered");

        return builder;
    }

    /// <summary>
    /// Configure OpenTelemetry middleware
    /// </summary>
    public static IApplicationBuilder UseOpenTelemetryObservability(this IApplicationBuilder app)
    {
        // Add correlation ID middleware
        app.UseMiddleware<CorrelationIdMiddleware>();
        
        // Add trace context propagation middleware
        app.UseMiddleware<TraceContextMiddleware>();
        
        // Add custom metrics middleware
        app.UseMiddleware<MetricsMiddleware>();
        
        return app;
    }
}

/// <summary>
/// Tracing service for custom spans
/// </summary>
public interface ITracingService
{
    Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal);
    void RecordException(Activity? activity, Exception exception);
    void AddEvent(Activity? activity, string name, Dictionary<string, object>? attributes = null);
    void SetStatus(Activity? activity, bool isSuccess, string? description = null);
}

public class TracingService : ITracingService
{
    private readonly ILogger<TracingService> _logger;
    public static int ActiveConnections { get; private set; }

    public TracingService(ILogger<TracingService> logger)
    {
        _logger = logger;
    }

    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        var activity = Activity.Current?.Source.StartActivity(name, kind) ??
                      OpenTelemetryService.GatewayActivitySource.StartActivity(name, kind);

        if (activity != null)
        {
            activity.SetTag("custom.activity", true);
            activity.SetTag("thread.id", Environment.CurrentManagedThreadId);
            ActiveConnections++;
        }

        return activity;
    }

    public void RecordException(Activity? activity, Exception exception)
    {
        if (activity == null) return;

        var tags = new ActivityTagsCollection
        {
            ["exception.type"] = exception.GetType().FullName,
            ["exception.message"] = exception.Message,
            ["exception.stacktrace"] = exception.StackTrace,
            ["exception.source"] = exception.Source
        };

        if (exception.InnerException != null)
        {
            tags["exception.inner"] = exception.InnerException.Message;
        }

        activity.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, tags));
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
    }

    public void AddEvent(Activity? activity, string name, Dictionary<string, object>? attributes = null)
    {
        if (activity == null) return;

        var tags = new ActivityTagsCollection();
        if (attributes != null)
        {
            foreach (var kvp in attributes)
            {
                tags[kvp.Key] = kvp.Value;
            }
        }

        activity.AddEvent(new ActivityEvent(name, DateTimeOffset.UtcNow, tags));
    }

    public void SetStatus(Activity? activity, bool isSuccess, string? description = null)
    {
        if (activity == null) return;

        activity.SetStatus(
            isSuccess ? ActivityStatusCode.Ok : ActivityStatusCode.Error,
            description);
        
        if (!isSuccess)
        {
            ActiveConnections = Math.Max(0, ActiveConnections - 1);
        }
    }
}

/// <summary>
/// Metrics service for custom metrics
/// </summary>
public interface IMetricsService
{
    void RecordRequest(string endpoint, int statusCode, double duration);
    void RecordError(string errorType, string endpoint);
    void RecordBusinessMetric(string metricName, double value, Dictionary<string, object>? tags = null);
    Dictionary<string, object> GetMetricsSummary();
}

public class MetricsService : IMetricsService
{
    private static long _totalRequests;
    private static long _totalErrors;
    private static readonly Dictionary<string, long> _endpointCounts = new();
    private static readonly Dictionary<string, double> _endpointDurations = new();
    private static readonly Dictionary<string, long> _errorCounts = new();

    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _durationHistogram;
    private readonly Counter<long> _errorCounter;

    public MetricsService()
    {
        _requestCounter = OpenTelemetryService.GatewayMeter.CreateCounter<long>("requests_total");
        _durationHistogram = OpenTelemetryService.GatewayMeter.CreateHistogram<double>("request_duration_ms");
        _errorCounter = OpenTelemetryService.GatewayMeter.CreateCounter<long>("errors_total");
    }

    public void RecordRequest(string endpoint, int statusCode, double duration)
    {
        Interlocked.Increment(ref _totalRequests);
        
        lock (_endpointCounts)
        {
            _endpointCounts.TryGetValue(endpoint, out var count);
            _endpointCounts[endpoint] = count + 1;
            
            _endpointDurations.TryGetValue(endpoint, out var totalDuration);
            _endpointDurations[endpoint] = totalDuration + duration;
        }

        var tags = new TagList
        {
            { "endpoint", endpoint },
            { "status_code", statusCode },
            { "status_class", $"{statusCode / 100}xx" }
        };

        _requestCounter.Add(1, tags);
        _durationHistogram.Record(duration, tags);

        if (statusCode >= 400)
        {
            Interlocked.Increment(ref _totalErrors);
            _errorCounter.Add(1, tags);
        }
    }

    public void RecordError(string errorType, string endpoint)
    {
        Interlocked.Increment(ref _totalErrors);
        
        lock (_errorCounts)
        {
            var key = $"{errorType}:{endpoint}";
            _errorCounts.TryGetValue(key, out var count);
            _errorCounts[key] = count + 1;
        }

        _errorCounter.Add(1, new TagList
        {
            { "error_type", errorType },
            { "endpoint", endpoint }
        });
    }

    public void RecordBusinessMetric(string metricName, double value, Dictionary<string, object>? tags = null)
    {
        var meter = OpenTelemetryService.BusinessMeter;
        var counter = meter.CreateCounter<double>(metricName);
        
        var tagList = new TagList();
        if (tags != null)
        {
            foreach (var kvp in tags)
            {
                tagList.Add(kvp.Key, kvp.Value);
            }
        }

        counter.Add(value, tagList);
    }

    public Dictionary<string, object> GetMetricsSummary()
    {
        var summary = new Dictionary<string, object>
        {
            ["total_requests"] = _totalRequests,
            ["total_errors"] = _totalErrors,
            ["error_rate"] = CalculateErrorRate(),
            ["endpoints"] = _endpointCounts.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value),
            ["average_durations"] = CalculateAverageDurations(),
            ["error_breakdown"] = _errorCounts.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
        };

        return summary;
    }

    public static double CalculateErrorRate()
    {
        if (_totalRequests == 0) return 0;
        return (_totalErrors * 100.0) / _totalRequests;
    }

    private Dictionary<string, double> CalculateAverageDurations()
    {
        var averages = new Dictionary<string, double>();
        
        lock (_endpointCounts)
        {
            foreach (var endpoint in _endpointCounts.Keys)
            {
                if (_endpointCounts.TryGetValue(endpoint, out var count) && count > 0)
                {
                    if (_endpointDurations.TryGetValue(endpoint, out var totalDuration))
                    {
                        averages[endpoint] = totalDuration / count;
                    }
                }
            }
        }

        return averages;
    }
}

/// <summary>
/// Correlation ID generator
/// </summary>
public interface ICorrelationIdGenerator
{
    string GenerateCorrelationId();
    bool TryParseCorrelationId(string correlationId, out Guid guid);
}

public class CorrelationIdGenerator : ICorrelationIdGenerator
{
    public string GenerateCorrelationId()
    {
        return Guid.NewGuid().ToString("N");
    }

    public bool TryParseCorrelationId(string correlationId, out Guid guid)
    {
        return Guid.TryParse(correlationId, out guid);
    }
}

/// <summary>
/// Correlation ID middleware
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ICorrelationIdGenerator _correlationIdGenerator;
    private const string CorrelationIdHeader = "X-Correlation-Id";

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ICorrelationIdGenerator correlationIdGenerator)
    {
        _next = next;
        _correlationIdGenerator = correlationIdGenerator;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrGenerateCorrelationId(context);
        
        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers.Add(CorrelationIdHeader, correlationId);

        using (Activity.Current?.AddTag("correlation.id", correlationId))
        {
            await _next(context);
        }
    }

    private string GetOrGenerateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId))
        {
            var id = correlationId.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(id) && 
                _correlationIdGenerator.TryParseCorrelationId(id, out _))
            {
                return id;
            }
        }

        return _correlationIdGenerator.GenerateCorrelationId();
    }
}

/// <summary>
/// Trace context middleware
/// </summary>
public class TraceContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ITracingService _tracingService;

    public TraceContextMiddleware(
        RequestDelegate next,
        ITracingService tracingService)
    {
        _next = next;
        _tracingService = tracingService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var activity = _tracingService.StartActivity(
            $"{context.Request.Method} {context.Request.Path}",
            ActivityKind.Server);

        try
        {
            if (activity != null)
            {
                activity.SetTag("http.method", context.Request.Method);
                activity.SetTag("http.url", context.Request.GetDisplayUrl());
                activity.SetTag("http.scheme", context.Request.Scheme);
                activity.SetTag("http.host", context.Request.Host.ToString());
                activity.SetTag("http.path", context.Request.Path.ToString());
                activity.SetTag("http.query", context.Request.QueryString.ToString());
                activity.SetTag("user.agent", context.Request.Headers["User-Agent"].ToString());
                activity.SetTag("client.ip", context.Connection.RemoteIpAddress?.ToString());
            }

            await _next(context);

            if (activity != null)
            {
                activity.SetTag("http.status_code", context.Response.StatusCode);
                _tracingService.SetStatus(activity, 
                    context.Response.StatusCode < 400,
                    $"HTTP {context.Response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _tracingService.RecordException(activity, ex);
            throw;
        }
        finally
        {
            activity?.Dispose();
        }
    }
}

/// <summary>
/// Metrics middleware
/// </summary>
public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMetricsService _metricsService;

    public MetricsMiddleware(
        RequestDelegate next,
        IMetricsService metricsService)
    {
        _next = next;
        _metricsService = metricsService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _metricsService.RecordError(ex.GetType().Name, context.Request.Path);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _metricsService.RecordRequest(
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}

/// <summary>
/// Trace context propagator for distributed tracing
/// </summary>
public class TraceContextPropagator
{
    private const string TraceParentHeader = "traceparent";
    private const string TraceStateHeader = "tracestate";

    public void Inject(HttpRequestMessage request, Activity? activity)
    {
        if (activity == null) return;

        var traceParent = $"00-{activity.TraceId}-{activity.SpanId}-{(activity.Recorded ? "01" : "00")}";
        request.Headers.Add(TraceParentHeader, traceParent);

        if (!string.IsNullOrEmpty(activity.TraceStateString))
        {
            request.Headers.Add(TraceStateHeader, activity.TraceStateString);
        }
    }

    public void Extract(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(TraceParentHeader, out var traceParent))
        {
            // Parse and apply trace context
            var parts = traceParent.ToString().Split('-');
            if (parts.Length == 4)
            {
                // Create activity with parent context
                var traceId = ActivityTraceId.CreateFromString(parts[1]);
                var spanId = ActivitySpanId.CreateFromString(parts[2]);
                var traceFlags = parts[3] == "01" ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;

                var parentContext = new ActivityContext(
                    traceId,
                    spanId,
                    traceFlags,
                    context.Request.Headers[TraceStateHeader].ToString());

                Activity.Current = OpenTelemetryService.GatewayActivitySource.StartActivity(
                    "IncomingRequest",
                    ActivityKind.Server,
                    parentContext);
            }
        }
    }
}
