using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Loco.Core.Tracing
{
    public interface IDistributedTracingService
    {
        Activity StartTrace(string operationName, TraceKind kind = TraceKind.Internal);
        Activity StartChildTrace(string operationName, Activity parent = null);
        void AddEvent(string eventName, Dictionary<string, object> attributes = null);
        void AddBaggage(string key, string value);
        void SetStatus(Status status, string description = null);
        void RecordException(Exception exception);
        Task<T> TraceAsync<T>(string operationName, Func<Activity, Task<T>> operation);
        string GetTraceId();
        string GetSpanId();
        void InjectContext(IDictionary<string, string> carrier);
        Activity ExtractContext(IDictionary<string, string> carrier);
    }

    public class DistributedTracingService : IDistributedTracingService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DistributedTracingService> _logger;
        private readonly ActivitySource _activitySource;
        private readonly string _serviceName;
        private readonly string _serviceVersion;
        private readonly TracerProvider _tracerProvider;

        public DistributedTracingService(
            IConfiguration configuration,
            ILogger<DistributedTracingService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            _serviceName = _configuration["Tracing:ServiceName"] ?? "LocoService";
            _serviceVersion = _configuration["Tracing:ServiceVersion"] ?? "1.0.0";
            
            _activitySource = new ActivitySource(_serviceName, _serviceVersion);
            _tracerProvider = ConfigureTracing();
        }

        private TracerProvider ConfigureTracing()
        {
            var builder = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService(_serviceName, _serviceVersion)
                    .AddAttributes(new Dictionary<string, object>
                    {
                        ["deployment.environment"] = _configuration["Tracing:Environment"] ?? "production",
                        ["host.name"] = Environment.MachineName,
                        ["process.pid"] = Environment.ProcessId,
                        ["process.runtime.name"] = ".NET",
                        ["process.runtime.version"] = Environment.Version.ToString()
                    }))
                .AddSource(_serviceName)
                .SetSampler(new TraceIdRatioBasedSampler(
                    _configuration.GetValue<double>("Tracing:SamplingRatio", 1.0)))
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.SetHttpFlavor = true;
                    options.FilterHttpRequestMessage = (httpRequestMessage) =>
                    {
                        return !httpRequestMessage.RequestUri?.AbsolutePath.Contains("/health") ?? true;
                    };
                })
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = (httpContext) =>
                    {
                        return !httpContext.Request.Path.StartsWithSegments("/health");
                    };
                });

            var otlpEndpoint = _configuration["Tracing:OtlpEndpoint"];
            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                builder.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                    options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                });
            }

            var jaegerEndpoint = _configuration["Tracing:JaegerEndpoint"];
            if (!string.IsNullOrEmpty(jaegerEndpoint))
            {
                builder.AddJaegerExporter(options =>
                {
                    options.AgentHost = new Uri(jaegerEndpoint).Host;
                    options.AgentPort = new Uri(jaegerEndpoint).Port;
                });
            }

            if (_configuration.GetValue<bool>("Tracing:EnableConsoleExporter", false))
            {
                builder.AddConsoleExporter();
            }

            return builder.Build();
        }

        public Activity StartTrace(string operationName, TraceKind kind = TraceKind.Internal)
        {
            var activity = _activitySource.StartActivity(
                operationName,
                ConvertTraceKind(kind),
                Activity.Current?.Context ?? default);

            if (activity != null)
            {
                activity.SetTag("service.name", _serviceName);
                activity.SetTag("service.version", _serviceVersion);
                activity.SetTag("operation.name", operationName);
                activity.SetTag("span.kind", kind.ToString());
                
                if (Activity.Current?.Baggage != null)
                {
                    foreach (var baggage in Activity.Current.Baggage)
                    {
                        activity.SetBaggage(baggage.Key, baggage.Value);
                    }
                }
                
                _logger.LogDebug("Started trace: {OperationName} with TraceId: {TraceId}", 
                    operationName, activity.TraceId);
            }

            return activity;
        }

        public Activity StartChildTrace(string operationName, Activity parent = null)
        {
            var parentContext = parent?.Context ?? Activity.Current?.Context;
            
            var activity = _activitySource.StartActivity(
                operationName,
                ActivityKind.Internal,
                parentContext ?? default);

            if (activity != null)
            {
                activity.SetTag("service.name", _serviceName);
                activity.SetTag("operation.name", operationName);
                
                if (parent != null)
                {
                    activity.SetTag("parent.span.id", parent.SpanId.ToString());
                }
                
                _logger.LogDebug("Started child trace: {OperationName} with SpanId: {SpanId}", 
                    operationName, activity.SpanId);
            }

            return activity;
        }

        public void AddEvent(string eventName, Dictionary<string, object> attributes = null)
        {
            var activity = Activity.Current;
            if (activity == null) return;

            var tags = new ActivityTagsCollection(attributes ?? new Dictionary<string, object>());
            activity.AddEvent(new ActivityEvent(eventName, DateTimeOffset.UtcNow, tags));
            
            _logger.LogDebug("Added event '{EventName}' to trace {TraceId}", eventName, activity.TraceId);
        }

        public void AddBaggage(string key, string value)
        {
            Activity.Current?.SetBaggage(key, value);
            _logger.LogDebug("Added baggage: {Key}={Value}", key, value);
        }

        public void SetStatus(Status status, string description = null)
        {
            var activity = Activity.Current;
            if (activity == null) return;

            var statusCode = status switch
            {
                Status.Ok => ActivityStatusCode.Ok,
                Status.Error => ActivityStatusCode.Error,
                _ => ActivityStatusCode.Unset
            };

            activity.SetStatus(statusCode, description);
            activity.SetTag("otel.status_code", statusCode.ToString());
            
            if (!string.IsNullOrEmpty(description))
            {
                activity.SetTag("otel.status_description", description);
            }
        }

        public void RecordException(Exception exception)
        {
            var activity = Activity.Current;
            if (activity == null) return;

            var tags = new ActivityTagsCollection
            {
                ["exception.type"] = exception.GetType().FullName,
                ["exception.message"] = exception.Message,
                ["exception.stacktrace"] = exception.StackTrace
            };

            if (exception.InnerException != null)
            {
                tags["exception.inner.type"] = exception.InnerException.GetType().FullName;
                tags["exception.inner.message"] = exception.InnerException.Message;
            }

            activity.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, tags));
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            
            _logger.LogError(exception, "Exception recorded in trace {TraceId}", activity.TraceId);
        }

        public async Task<T> TraceAsync<T>(string operationName, Func<Activity, Task<T>> operation)
        {
            using var activity = StartTrace(operationName);
            
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await operation(activity);
                stopwatch.Stop();
                
                activity?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds);
                SetStatus(Status.Ok);
                
                return result;
            }
            catch (Exception ex)
            {
                RecordException(ex);
                SetStatus(Status.Error, ex.Message);
                throw;
            }
        }

        public string GetTraceId()
        {
            return Activity.Current?.TraceId.ToString() ?? string.Empty;
        }

        public string GetSpanId()
        {
            return Activity.Current?.SpanId.ToString() ?? string.Empty;
        }

        public void InjectContext(IDictionary<string, string> carrier)
        {
            var activity = Activity.Current;
            if (activity == null) return;

            carrier["traceparent"] = $"00-{activity.TraceId}-{activity.SpanId}-{(activity.Recorded ? "01" : "00")}";
            
            if (activity.TraceStateString != null)
            {
                carrier["tracestate"] = activity.TraceStateString;
            }

            foreach (var baggage in activity.Baggage)
            {
                carrier[$"baggage-{baggage.Key}"] = baggage.Value;
            }
            
            _logger.LogDebug("Injected trace context: TraceId={TraceId}, SpanId={SpanId}", 
                activity.TraceId, activity.SpanId);
        }

        public Activity ExtractContext(IDictionary<string, string> carrier)
        {
            if (!carrier.TryGetValue("traceparent", out var traceparent))
                return null;

            var parts = traceparent.Split('-');
            if (parts.Length != 4)
                return null;

            var traceId = ActivityTraceId.CreateFromString(parts[1].AsSpan());
            var spanId = ActivitySpanId.CreateFromString(parts[2].AsSpan());
            var flags = (ActivityTraceFlags)(Convert.ToByte(parts[3], 16));

            var context = new ActivityContext(traceId, spanId, flags);
            
            var activity = _activitySource.StartActivity(
                "extracted",
                ActivityKind.Server,
                context);

            if (activity != null && carrier.TryGetValue("tracestate", out var tracestate))
            {
                activity.TraceStateString = tracestate;
            }

            foreach (var kvp in carrier)
            {
                if (kvp.Key.StartsWith("baggage-"))
                {
                    var baggageKey = kvp.Key.Substring(8);
                    activity?.SetBaggage(baggageKey, kvp.Value);
                }
            }
            
            _logger.LogDebug("Extracted trace context: TraceId={TraceId}, SpanId={SpanId}", 
                traceId, spanId);

            return activity;
        }

        private ActivityKind ConvertTraceKind(TraceKind kind)
        {
            return kind switch
            {
                TraceKind.Server => ActivityKind.Server,
                TraceKind.Client => ActivityKind.Client,
                TraceKind.Producer => ActivityKind.Producer,
                TraceKind.Consumer => ActivityKind.Consumer,
                _ => ActivityKind.Internal
            };
        }
    }

    public enum TraceKind
    {
        Internal,
        Server,
        Client,
        Producer,
        Consumer
    }

    public enum Status
    {
        Unset,
        Ok,
        Error
    }

    public static class TracingExtensions
    {
        public static IDisposable TraceMethod(this IDistributedTracingService tracing, 
            [System.Runtime.CompilerServices.CallerMemberName] string methodName = "")
        {
            return tracing.StartTrace($"Method.{methodName}");
        }

        public static async Task<T> TraceOperationAsync<T>(
            this IDistributedTracingService tracing,
            string operationName,
            Func<Task<T>> operation,
            Dictionary<string, object> tags = null)
        {
            using var activity = tracing.StartTrace(operationName);
            
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    activity?.SetTag(tag.Key, tag.Value);
                }
            }

            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                tracing.RecordException(ex);
                throw;
            }
        }

        public static void TraceHttpRequest(this IDistributedTracingService tracing,
            string method, string url, int statusCode, double duration)
        {
            var activity = Activity.Current;
            if (activity == null) return;

            activity.SetTag("http.method", method);
            activity.SetTag("http.url", url);
            activity.SetTag("http.status_code", statusCode);
            activity.SetTag("http.duration", duration);
            
            if (statusCode >= 400)
            {
                activity.SetStatus(ActivityStatusCode.Error, $"HTTP {statusCode}");
            }
        }

        public static void TraceDatabaseQuery(this IDistributedTracingService tracing,
            string operation, string table, double duration, bool success)
        {
            var activity = Activity.Current;
            if (activity == null) return;

            activity.SetTag("db.operation", operation);
            activity.SetTag("db.table", table);
            activity.SetTag("db.duration", duration);
            activity.SetTag("db.success", success);
            
            if (!success)
            {
                activity.SetStatus(ActivityStatusCode.Error, "Database query failed");
            }
        }
    }
}