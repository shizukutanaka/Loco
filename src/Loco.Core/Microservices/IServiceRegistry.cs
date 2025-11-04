namespace Loco.Core.Microservices;

/// <summary>
/// Service registry interface for service discovery
/// </summary>
public interface IServiceRegistry
{
    /// <summary>
    /// Registers a service
    /// </summary>
    Task RegisterServiceAsync(ServiceInstance service);

    /// <summary>
    /// Deregisters a service
    /// </summary>
    Task DeregisterServiceAsync(string serviceName, string serviceId);

    /// <summary>
    /// Discovers service instances
    /// </summary>
    Task<IEnumerable<ServiceInstance>> DiscoverServiceAsync(string serviceName);

    /// <summary>
    /// Gets a specific service instance
    /// </summary>
    Task<ServiceInstance?> GetServiceAsync(string serviceName, string serviceId);

    /// <summary>
    /// Gets all registered services
    /// </summary>
    Task<IEnumerable<ServiceInstance>> GetAllServicesAsync();

    /// <summary>
    /// Health check for service
    /// </summary>
    Task<bool> HealthCheckAsync(string serviceName, string serviceId);

    /// <summary>
    /// Updates service status
    /// </summary>
    Task UpdateServiceStatusAsync(string serviceName, string serviceId, ServiceStatus status);

    /// <summary>
    /// Watches for service changes
    /// </summary>
    IObservable<ServiceRegistryEvent> WatchServices();
}

/// <summary>
/// Service instance definition
/// </summary>
public class ServiceInstance
{
    /// <summary>
    /// Service name
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Service ID (unique identifier)
    /// </summary>
    public string ServiceId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Service host
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Service port
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Service protocol
    /// </summary>
    public ServiceProtocol Protocol { get; set; } = ServiceProtocol.Http;

    /// <summary>
    /// Service status
    /// </summary>
    public ServiceStatus Status { get; set; } = ServiceStatus.Healthy;

    /// <summary>
    /// Base URL
    /// </summary>
    public string BaseUrl => $"{Protocol.ToString().ToLower()}://{Host}:{Port}";

    /// <summary>
    /// Health check URL
    /// </summary>
    public string? HealthCheckUrl { get; set; }

    /// <summary>
    /// Service metadata
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Last heartbeat time
    /// </summary>
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Registration time
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tags
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Version
    /// </summary>
    public string? Version { get; set; }
}

/// <summary>
/// Service protocol enumeration
/// </summary>
public enum ServiceProtocol
{
    Http,
    Https,
    Grpc,
    GrpcWeb,
    Amqp,
    Kafka
}

/// <summary>
/// Service status enumeration
/// </summary>
public enum ServiceStatus
{
    Healthy,
    Unhealthy,
    Degraded,
    Offline,
    Registering,
    Deregistering
}

/// <summary>
/// Service registry event
/// </summary>
public class ServiceRegistryEvent
{
    /// <summary>
    /// Event type
    /// </summary>
    public ServiceRegistryEventType EventType { get; set; }

    /// <summary>
    /// Service instance
    /// </summary>
    public ServiceInstance? ServiceInstance { get; set; }

    /// <summary>
    /// Service name
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Service registry event type
/// </summary>
public enum ServiceRegistryEventType
{
    ServiceRegistered,
    ServiceDeregistered,
    ServiceStatusChanged,
    ServiceHealthCheckFailed,
    ServiceAdded,
    ServiceRemoved
}

/// <summary>
/// Microservice communication client interface
/// </summary>
public interface IMicroserviceClient
{
    /// <summary>
    /// Makes a GET request
    /// </summary>
    Task<T> GetAsync<T>(string serviceName, string endpoint) where T : class;

    /// <summary>
    /// Makes a POST request
    /// </summary>
    Task<T> PostAsync<T>(string serviceName, string endpoint, object data) where T : class;

    /// <summary>
    /// Makes a PUT request
    /// </summary>
    Task<T> PutAsync<T>(string serviceName, string endpoint, object data) where T : class;

    /// <summary>
    /// Makes a DELETE request
    /// </summary>
    Task<bool> DeleteAsync(string serviceName, string endpoint);

    /// <summary>
    /// Makes a raw HTTP request
    /// </summary>
    Task<HttpResponseMessage> SendAsync(string serviceName, HttpRequestMessage request);

    /// <summary>
    /// Gets service base URL
    /// </summary>
    Task<string> GetServiceUrlAsync(string serviceName);
}
