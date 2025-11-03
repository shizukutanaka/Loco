using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace Loco.Core.Observability;

/// <summary>
/// OpenTelemetry setup and configuration
/// </summary>
public static class OpenTelemetrySetup
{
    private const string ServiceName = "loco-api";
    private const string ServiceVersion = "1.0.0";

    /// <summary>
    /// Adds comprehensive OpenTelemetry observability
    /// </summary>
    public static IServiceCollection AddLocoObservability(
        this IServiceCollection services,
        string? otlpEndpoint = null)
    {
        var endpoint = otlpEndpoint ?? "http://localhost:4317";

        // Create resource
        var resource = ResourceBuilder.CreateDefault()
            .AddService(ServiceName, serviceVersion: ServiceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                { "environment.name", "production" },
                { "service.instance.id", Environment.MachineName },
                { "deployment.environment", GetEnvironment() }
            });

        // Add OpenTelemetry
        services.AddOpenTelemetry()
            .WithTracing(tracing => ConfigureTracing(tracing, resource, endpoint))
            .WithMetrics(metrics => ConfigureMetrics(metrics, resource, endpoint))
            .ConfigureResource(r => r.AddService(ServiceName, serviceVersion: ServiceVersion));

        // Add logging
        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(options =>
            {
                options.AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = new Uri(endpoint);
                    exporter.Protocol = OtlpExportProtocol.Grpc;
                });
                options.IncludeScopes = true;
                options.ParseStateValues = true;
            });
        });

        // Activity listeners for custom instrumentation
        ActivitySource.AddActivityListener(new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        });

        return services;
    }

    private static TracerProviderBuilder ConfigureTracing(
        TracerProviderBuilder tracing,
        ResourceBuilder resource,
        string endpoint)
    {
        return tracing
            .SetResourceBuilder(resource)
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.EnrichWithHttpRequest = (activity, request) =>
                {
                    activity.SetTag("http.url", request.GetDisplayUrl());
                    activity.SetTag("http.user_agent", request.Headers.UserAgent);
                };
                options.EnrichWithHttpResponse = (activity, response) =>
                {
                    activity.SetTag("http.response.content_type", response.ContentType);
                };
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
                options.EnrichWithHttpRequest = (activity, request) =>
                {
                    activity.SetTag("http.request.uri", request.RequestUri);
                };
                options.EnrichWithHttpResponse = (activity, response) =>
                {
                    activity.SetTag("http.response.status_code", (int)response.StatusCode);
                };
            })
            .AddSqlClientInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddSource(ServiceName)
            .AddConsoleExporter()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(endpoint);
                options.Protocol = OtlpExportProtocol.Grpc;
            });
    }

    private static MeterProviderBuilder ConfigureMetrics(
        MeterProviderBuilder metrics,
        ResourceBuilder resource,
        string endpoint)
    {
        return metrics
            .SetResourceBuilder(resource)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddView("http.server.request.duration",
                new ExplicitBucketHistogramAggregation { Boundaries = new[] { 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 } })
            .AddConsoleExporter()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(endpoint);
                options.Protocol = OtlpExportProtocol.Grpc;
            });
    }

    private static string GetEnvironment()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
    }
}

/// <summary>
/// Custom ActivitySource for Loco operations
/// </summary>
public static class LocoActivitySource
{
    public static readonly ActivitySource Instance = new(
        "Loco.Core",
        "1.0.0");

    /// <summary>
    /// Creates an activity for workflow execution
    /// </summary>
    public static Activity? StartWorkflowExecution(string workflowId)
    {
        var activity = Instance.StartActivity("workflow.execute");
        if (activity != null)
        {
            activity.SetTag("workflow.id", workflowId);
            activity.SetTag("workflow.type", "execution");
        }
        return activity;
    }

    /// <summary>
    /// Creates an activity for job scheduling
    /// </summary>
    public static Activity? StartJobScheduling(string jobId, string jobType)
    {
        var activity = Instance.StartActivity("job.schedule");
        if (activity != null)
        {
            activity.SetTag("job.id", jobId);
            activity.SetTag("job.type", jobType);
        }
        return activity;
    }

    /// <summary>
    /// Creates an activity for data access
    /// </summary>
    public static Activity? StartDataAccess(string operation, string entityType)
    {
        var activity = Instance.StartActivity("data.access");
        if (activity != null)
        {
            activity.SetTag("operation", operation);
            activity.SetTag("entity.type", entityType);
        }
        return activity;
    }
}

/// <summary>
/// Metrics collector for Loco operations
/// </summary>
public static class LocoMetrics
{
    private static readonly System.Diagnostics.Metrics.Meter WorkflowMeter =
        new("Loco.Workflows", "1.0.0");

    private static readonly System.Diagnostics.Metrics.Meter JobMeter =
        new("Loco.Jobs", "1.0.0");

    // Workflow metrics
    public static readonly System.Diagnostics.Metrics.Counter<long> WorkflowExecutionCounter =
        WorkflowMeter.CreateCounter<long>(
            "workflow.executions.total",
            description: "Total number of workflow executions");

    public static readonly System.Diagnostics.Metrics.Histogram<double> WorkflowExecutionDuration =
        WorkflowMeter.CreateHistogram<double>(
            "workflow.execution.duration",
            unit: "ms",
            description: "Duration of workflow executions");

    public static readonly System.Diagnostics.Metrics.Counter<long> WorkflowFailureCounter =
        WorkflowMeter.CreateCounter<long>(
            "workflow.failures.total",
            description: "Total number of workflow failures");

    // Job metrics
    public static readonly System.Diagnostics.Metrics.Counter<long> JobQueueCounter =
        JobMeter.CreateCounter<long>(
            "job.queued.total",
            description: "Total number of jobs queued");

    public static readonly System.Diagnostics.Metrics.Counter<long> JobCompleteCounter =
        JobMeter.CreateCounter<long>(
            "job.completed.total",
            description: "Total number of jobs completed");

    public static readonly System.Diagnostics.Metrics.Counter<long> JobFailureCounter =
        JobMeter.CreateCounter<long>(
            "job.failures.total",
            description: "Total number of job failures");
}
