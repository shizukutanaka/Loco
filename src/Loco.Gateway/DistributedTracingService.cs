using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;

namespace Loco.Gateway.Observability;

/// <summary>
/// OpenTelemetry distributed tracing service
/// </summary>
public class DistributedTracingService
{
    private readonly ILogger<DistributedTracingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TracerProvider _tracerProvider;
    private readonly MeterProvider _meterProvider;
    private readonly ActivitySource _activitySource;
    private readonly System.Diagnostics.Metrics.Meter _meter;
    private readonly Dictionary<string, System.Diagnostics.Metrics.Counter<long>> _counters;
    private readonly Dictionary<string, System.Diagnostics.Metrics.Histogram<double>> _histograms;
    private readonly Dictionary<string, System.Diagnostics.Metrics.ObservableGauge<double>> _gauges;

    public DistributedTracingService(
        ILogger<DistributedTracingService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _activitySource = new ActivitySource("Loco.Gateway", "1.0.0");
        _meter = new System.Diagnostics.Metrics.Meter("Loco.Gateway", "1.0.0");
        _counters = new Dictionary<string, System.Diagnostics.Metrics.Counter<long>>();
        _histograms = new Dictionary<string, System.Diagnostics.Metrics.Histogram<double>>();
        _gauges = new Dictionary<string, System.Diagnostics.Metrics.ObservableGauge<double>>();

        (_tracerProvider, _meterProvider) = InitializeOpenTelemetry();
        InitializeMetrics();
    }

    /// <summary>
    /// Start a new trace span
    /// </summary>
    public Activity? StartSpan(string name, ActivityKind kind = ActivityKind.Internal, Activity? parent = null)
    {
        var activity = _activitySource.StartActivity(name, kind, parent?.Context ?? default);
        
        if (activity != null)
        {
            // Add common tags
            activity.SetTag("service.name", "loco-gateway");
            activity.SetTag("service.version", "1.0.0");
            activity.SetTag("deployment.environment", _configuration["Environment"] ?? "production");
            activity.SetTag("host.name", Environment.MachineName);
            
            // Add trace context to logs
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceId"] = activity.TraceId.ToString(),
                ["SpanId"] = activity.SpanId.ToString()
            }))
            {
                _logger.LogDebug("Started span: {SpanName}", name);
            }
        }

        return activity;
    }

    /// <summary>
    /// Start a new HTTP request span
    /// </summary>
    public Activity? StartHttpSpan(string method, string path, Dictionary<string, string>? headers = null)
    {
        var activity = StartSpan($"HTTP {method} {path}", ActivityKind.Server);
        
        if (activity != null)
        {
            activity.SetTag("http.method", method);
            activity.SetTag("http.path", path);
            activity.SetTag("http.scheme", "https");
            
            // Add relevant headers as tags
            if (headers != null)
            {
                if (headers.TryGetValue("User-Agent", out var userAgent))
                {
                    activity.SetTag("http.user_agent", userAgent);
                }
                
                if (headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
                {
                    activity.SetTag("http.client_ip", forwardedFor);
                }
            }

            // Extract trace context from headers if present
            if (headers?.TryGetValue("traceparent", out var traceParent) == true)
            {
                activity.SetParentId(traceParent);
            }
        }

        return activity;
    }

    /// <summary>
    /// Start a database operation span
    /// </summary>
    public Activity? StartDatabaseSpan(string operation, string table, string? query = null)
    {
        var activity = StartSpan($"DB {operation} {table}", ActivityKind.Client);
        
        if (activity != null)
        {
            activity.SetTag("db.system", "postgresql");
            activity.SetTag("db.operation", operation);
            activity.SetTag("db.name", "loco");
            activity.SetTag("db.table", table);
            
            if (!string.IsNullOrEmpty(query))
            {
                // Sanitize query before adding as tag
                var sanitizedQuery = SanitizeQuery(query);
                activity.SetTag("db.statement", sanitizedQuery);
            }
        }

        return activity;
    }

    /// <summary>
    /// Start a message queue span
    /// </summary>
    public Activity? StartMessageSpan(string operation, string queue, string? messageId = null)
    {
        var activity = StartSpan($"MQ {operation} {queue}", ActivityKind.Producer);
        
        if (activity != null)
        {
            activity.SetTag("messaging.system", "rabbitmq");
            activity.SetTag("messaging.operation", operation);
            activity.SetTag("messaging.destination", queue);
            
            if (!string.IsNullOrEmpty(messageId))
            {
                activity.SetTag("messaging.message_id", messageId);
            }
        }

        return activity;
    }

    /// <summary>
    /// Record an event in the current span
    /// </summary>
    public void RecordEvent(string name, Dictionary<string, object>? attributes = null)
    {
        var activity = Activity.Current;
        
        if (activity != null)
        {
            var tags = new ActivityTagsCollection();
            
            if (attributes != null)
            {
                foreach (var (key, value) in attributes)
                {
                    tags[key] = value;
                }
            }

            activity.AddEvent(new ActivityEvent(name, DateTimeOffset.UtcNow, tags));
        }
    }

    /// <summary>
    /// Record an exception in the current span
    /// </summary>
    public void RecordException(Exception exception, Dictionary<string, object>? attributes = null)
    {
        var activity = Activity.Current;
        
        if (activity != null)
        {
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            
            var tags = new ActivityTagsCollection
            {
                ["exception.type"] = exception.GetType().FullName,
                ["exception.message"] = exception.Message,
                ["exception.stacktrace"] = exception.StackTrace
            };
            
            if (attributes != null)
            {
                foreach (var (key, value) in attributes)
                {
                    tags[key] = value;
                }
            }

            activity.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, tags));
        }
    }

    /// <summary>
    /// Add baggage to the current context
    /// </summary>
    public void AddBaggage(string key, string value)
    {
        Baggage.SetBaggage(key, value);
    }

    /// <summary>
    /// Get baggage from the current context
    /// </summary>
    public string? GetBaggage(string key)
    {
        return Baggage.GetBaggage(key);
    }

    /// <summary>
    /// Record a metric counter
    /// </summary>
    public void IncrementCounter(string name, long value = 1, Dictionary<string, object>? tags = null)
    {
        if (!_counters.TryGetValue(name, out var counter))
        {
            counter = _meter.CreateCounter<long>(name);
            _counters[name] = counter;
        }

        if (tags != null)
        {
            var tagList = new TagList();
            foreach (var (key, val) in tags)
            {
                tagList.Add(key, val);
            }
            counter.Add(value, tagList);
        }
        else
        {
            counter.Add(value);
        }
    }

    /// <summary>
    /// Record a metric histogram
    /// </summary>
    public void RecordHistogram(string name, double value, Dictionary<string, object>? tags = null)
    {
        if (!_histograms.TryGetValue(name, out var histogram))
        {
            histogram = _meter.CreateHistogram<double>(name);
            _histograms[name] = histogram;
        }

        if (tags != null)
        {
            var tagList = new TagList();
            foreach (var (key, val) in tags)
            {
                tagList.Add(key, val);
            }
            histogram.Record(value, tagList);
        }
        else
        {
            histogram.Record(value);
        }
    }

    /// <summary>
    /// Create a gauge metric
    /// </summary>
    public void CreateGauge(string name, Func<double> observeValue, string? unit = null, string? description = null)
    {
        if (!_gauges.ContainsKey(name))
        {
            var gauge = _meter.CreateObservableGauge(name, observeValue, unit, description);
            _gauges[name] = gauge;
        }
    }

    /// <summary>
    /// Inject trace context into headers
    /// </summary>
    public void InjectTraceContext(IDictionary<string, string> headers)
    {
        var activity = Activity.Current;
        
        if (activity != null)
        {
            // W3C Trace Context format
            headers["traceparent"] = $"00-{activity.TraceId}-{activity.SpanId}-{(activity.Recorded ? "01" : "00")}";
            
            if (activity.TraceStateString != null)
            {
                headers["tracestate"] = activity.TraceStateString;
            }

            // Add baggage
            var baggage = Baggage.GetBaggage();
            if (baggage.Any())
            {
                headers["baggage"] = string.Join(",", baggage.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            }
        }
    }

    /// <summary>
    /// Extract trace context from headers
    /// </summary>
    public ActivityContext ExtractTraceContext(IDictionary<string, string> headers)
    {
        if (headers.TryGetValue("traceparent", out var traceParent))
        {
            if (ActivityContext.TryParse(traceParent, null, out var context))
            {
                // Extract baggage
                if (headers.TryGetValue("baggage", out var baggageHeader))
                {
                    var baggageItems = baggageHeader.Split(',');
                    foreach (var item in baggageItems)
                    {
                        var parts = item.Split('=');
                        if (parts.Length == 2)
                        {
                            Baggage.SetBaggage(parts[0].Trim(), parts[1].Trim());
                        }
                    }
                }

                return context;
            }
        }

        return default;
    }

    /// <summary>
    /// Create a distributed trace across services
    /// </summary>
    public async Task<T> TraceAsync<T>(string operationName, Func<Task<T>> operation, Dictionary<string, object>? attributes = null)
    {
        using var activity = StartSpan(operationName);
        
        if (activity != null && attributes != null)
        {
            foreach (var (key, value) in attributes)
            {
                activity.SetTag(key, value);
            }
        }

        try
        {
            var result = await operation();
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            RecordException(ex);
            throw;
        }
    }

    private (TracerProvider, MeterProvider) InitializeOpenTelemetry()
    {
        var serviceName = "loco-gateway";
        var serviceVersion = "1.0.0";

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName, serviceVersion: serviceVersion)
            .AddTelemetrySdk()
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = _configuration["Environment"] ?? "production",
                ["service.namespace"] = "loco",
                ["service.instance.id"] = Guid.NewGuid().ToString()
            });

        // Configure tracing
        var tracerBuilder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource("Loco.*")
            .AddAspNetCoreInstrumentation(options =>
            {
                options.Filter = (httpContext) => !httpContext.Request.Path.StartsWithSegments("/health");
                options.RecordException = true;
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
                options.FilterHttpRequestMessage = (httpRequestMessage) => 
                    !httpRequestMessage.RequestUri?.Host.Contains("telemetry") ?? true;
            })
            .AddSqlClientInstrumentation(options =>
            {
                options.SetDbStatementForText = true;
                options.RecordException = true;
            })
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.SetDbStatementForText = true;
            })
            .SetSampler(new TraceIdRatioBasedSampler(_configuration.GetValue<double>("OpenTelemetry:SamplingRatio", 1.0)));

        // Add exporters based on configuration
        var exporters = _configuration.GetSection("OpenTelemetry:Exporters");
        
        if (exporters.GetValue<bool>("Console"))
        {
            tracerBuilder.AddConsoleExporter();
        }

        if (exporters.GetValue<bool>("Jaeger"))
        {
            tracerBuilder.AddJaegerExporter(options =>
            {
                options.AgentHost = _configuration["OpenTelemetry:Jaeger:Host"] ?? "localhost";
                options.AgentPort = _configuration.GetValue<int>("OpenTelemetry:Jaeger:Port", 6831);
                options.ExportProcessorType = ExportProcessorType.Batch;
            });
        }

        if (exporters.GetValue<bool>("Zipkin"))
        {
            tracerBuilder.AddZipkinExporter(options =>
            {
                options.Endpoint = new Uri(_configuration["OpenTelemetry:Zipkin:Endpoint"] ?? "http://localhost:9411/api/v2/spans");
            });
        }

        if (exporters.GetValue<bool>("OTLP"))
        {
            tracerBuilder.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(_configuration["OpenTelemetry:OTLP:Endpoint"] ?? "http://localhost:4317");
                options.Protocol = OtlpExportProtocol.Grpc;
            });
        }

        var tracerProvider = tracerBuilder.Build();

        // Configure metrics
        var meterBuilder = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter("Loco.*")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddView("http.server.request.duration",
                new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = new double[] { 0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000 }
                });

        if (exporters.GetValue<bool>("Console"))
        {
            meterBuilder.AddConsoleExporter();
        }

        if (exporters.GetValue<bool>("Prometheus"))
        {
            meterBuilder.AddPrometheusExporter();
        }

        if (exporters.GetValue<bool>("OTLP"))
        {
            meterBuilder.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(_configuration["OpenTelemetry:OTLP:Endpoint"] ?? "http://localhost:4317");
                options.Protocol = OtlpExportProtocol.Grpc;
            });
        }

        var meterProvider = meterBuilder.Build();

        _logger.LogInformation("OpenTelemetry initialized with service {ServiceName} version {ServiceVersion}",
            serviceName, serviceVersion);

        return (tracerProvider, meterProvider);
    }

    private void InitializeMetrics()
    {
        // Create standard metrics
        IncrementCounter("requests_total", 0);
        CreateGauge("memory_usage_bytes", () => GC.GetTotalMemory(false), "bytes", "Memory usage in bytes");
        CreateGauge("cpu_usage_percent", () => Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds, "percent", "CPU usage percentage");
        CreateGauge("thread_count", () => Process.GetCurrentProcess().Threads.Count, "threads", "Number of threads");
        CreateGauge("gc_gen0_count", () => GC.CollectionCount(0), "collections", "Gen 0 GC collections");
        CreateGauge("gc_gen1_count", () => GC.CollectionCount(1), "collections", "Gen 1 GC collections");
        CreateGauge("gc_gen2_count", () => GC.CollectionCount(2), "collections", "Gen 2 GC collections");
    }

    private string SanitizeQuery(string query)
    {
        // Remove sensitive data from queries
        // This is a simplified implementation - use a proper SQL parser in production
        return System.Text.RegularExpressions.Regex.Replace(
            query,
            @"'[^']*'",
            "'?'",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    public void Dispose()
    {
        _tracerProvider?.Dispose();
        _meterProvider?.Dispose();
        _activitySource?.Dispose();
    }
}

/// <summary>
/// OpenTelemetry configuration extensions
/// </summary>
public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddDistributedTracing(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<DistributedTracingService>();
        
        // Add OpenTelemetry
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("loco-gateway", serviceVersion: "1.0.0")
                .AddTelemetrySdk()
                .AddEnvironmentVariableDetector())
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                // Add exporters based on configuration
                var exporters = configuration.GetSection("OpenTelemetry:Exporters");
                
                if (exporters.GetValue<bool>("Jaeger"))
                {
                    tracing.AddJaegerExporter();
                }
                
                if (exporters.GetValue<bool>("Zipkin"))
                {
                    tracing.AddZipkinExporter();
                }
                
                if (exporters.GetValue<bool>("OTLP"))
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation();

                var exporters = configuration.GetSection("OpenTelemetry:Exporters");
                
                if (exporters.GetValue<bool>("Prometheus"))
                {
                    metrics.AddPrometheusExporter();
                }
                
                if (exporters.GetValue<bool>("OTLP"))
                {
                    metrics.AddOtlpExporter();
                }
            });

        return services;
    }

    public static IApplicationBuilder UseDistributedTracing(this IApplicationBuilder app)
    {
        // Add middleware for trace context propagation
        app.Use(async (context, next) =>
        {
            var tracingService = context.RequestServices.GetRequiredService<DistributedTracingService>();
            
            // Extract trace context from headers
            var headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
            var traceContext = tracingService.ExtractTraceContext(headers);
            
            // Start a new span for this request
            using var activity = tracingService.StartHttpSpan(
                context.Request.Method,
                context.Request.Path,
                headers);

            try
            {
                await next();
                
                // Record response status
                if (activity != null)
                {
                    activity.SetTag("http.status_code", context.Response.StatusCode);
                    
                    if (context.Response.StatusCode >= 400)
                    {
                        activity.SetStatus(ActivityStatusCode.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                tracingService.RecordException(ex);
                throw;
            }
        });

        return app;
    }
}
