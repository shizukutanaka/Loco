using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Loco.Core.Microservices;

/// <summary>
/// In-memory service registry implementation
/// </summary>
public class InMemoryServiceRegistry : IServiceRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ServiceInstance>> _services = new();
    private readonly ILogger<InMemoryServiceRegistry> _logger;
    private readonly Subject<ServiceRegistryEvent> _eventSubject = new Subject<ServiceRegistryEvent>();

    public InMemoryServiceRegistry(ILogger<InMemoryServiceRegistry> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RegisterServiceAsync(ServiceInstance service)
    {
        try
        {
            var instances = _services.GetOrAdd(service.ServiceName, _ => new ConcurrentDictionary<string, ServiceInstance>());
            instances[service.ServiceId] = service;

            _logger.LogInformation(
                "Service registered: {ServiceName}/{ServiceId} at {BaseUrl}",
                service.ServiceName, service.ServiceId, service.BaseUrl);

            _eventSubject.OnNext(new ServiceRegistryEvent
            {
                EventType = ServiceRegistryEventType.ServiceRegistered,
                ServiceInstance = service
            });

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering service: {ServiceName}", service.ServiceName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeregisterServiceAsync(string serviceName, string serviceId)
    {
        try
        {
            if (_services.TryGetValue(serviceName, out var instances))
            {
                if (instances.TryRemove(serviceId, out var removed))
                {
                    _logger.LogInformation(
                        "Service deregistered: {ServiceName}/{ServiceId}",
                        serviceName, serviceId);

                    _eventSubject.OnNext(new ServiceRegistryEvent
                    {
                        EventType = ServiceRegistryEventType.ServiceDeregistered,
                        ServiceName = serviceName
                    });
                }
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deregistering service: {ServiceName}/{ServiceId}", serviceName, serviceId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ServiceInstance>> DiscoverServiceAsync(string serviceName)
    {
        try
        {
            if (_services.TryGetValue(serviceName, out var instances))
            {
                return instances.Values
                    .Where(s => s.Status == ServiceStatus.Healthy)
                    .ToList();
            }

            _logger.LogWarning("No healthy instances found for service: {ServiceName}", serviceName);
            return Enumerable.Empty<ServiceInstance>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering service: {ServiceName}", serviceName);
            return Enumerable.Empty<ServiceInstance>();
        }
    }

    /// <inheritdoc />
    public async Task<ServiceInstance?> GetServiceAsync(string serviceName, string serviceId)
    {
        if (_services.TryGetValue(serviceName, out var instances))
        {
            instances.TryGetValue(serviceId, out var instance);
            return instance;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ServiceInstance>> GetAllServicesAsync()
    {
        var allServices = new List<ServiceInstance>();
        foreach (var serviceGroup in _services.Values)
        {
            allServices.AddRange(serviceGroup.Values);
        }

        return allServices;
    }

    /// <inheritdoc />
    public async Task<bool> HealthCheckAsync(string serviceName, string serviceId)
    {
        try
        {
            if (!_services.TryGetValue(serviceName, out var instances))
            {
                return false;
            }

            if (!instances.TryGetValue(serviceId, out var service))
            {
                return false;
            }

            // In production, make an actual HTTP request to health check endpoint
            service.LastHeartbeat = DateTime.UtcNow;
            service.Status = ServiceStatus.Healthy;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed: {ServiceName}/{ServiceId}", serviceName, serviceId);
            await UpdateServiceStatusAsync(serviceName, serviceId, ServiceStatus.Unhealthy);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task UpdateServiceStatusAsync(string serviceName, string serviceId, ServiceStatus status)
    {
        try
        {
            if (_services.TryGetValue(serviceName, out var instances))
            {
                if (instances.TryGetValue(serviceId, out var service))
                {
                    service.Status = status;
                    service.LastHeartbeat = DateTime.UtcNow;

                    _logger.LogInformation(
                        "Service status updated: {ServiceName}/{ServiceId} -> {Status}",
                        serviceName, serviceId, status);

                    _eventSubject.OnNext(new ServiceRegistryEvent
                    {
                        EventType = ServiceRegistryEventType.ServiceStatusChanged,
                        ServiceInstance = service
                    });
                }
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service status: {ServiceName}/{ServiceId}", serviceName, serviceId);
        }
    }

    /// <inheritdoc />
    public IObservable<ServiceRegistryEvent> WatchServices()
    {
        return _eventSubject;
    }
}

/// <summary>
/// Simple subject implementation for reactive events
/// </summary>
public class Subject<T> : IObservable<T>
{
    private readonly List<IObserver<T>> _observers = new();
    private readonly object _lock = new();

    public IDisposable Subscribe(IObserver<T> observer)
    {
        lock (_lock)
        {
            _observers.Add(observer);
        }

        return new Unsubscriber(_observers, observer);
    }

    public void OnNext(T value)
    {
        lock (_lock)
        {
            foreach (var observer in _observers.ToList())
            {
                observer.OnNext(value);
            }
        }
    }

    public void OnError(Exception error)
    {
        lock (_lock)
        {
            foreach (var observer in _observers.ToList())
            {
                observer.OnError(error);
            }
        }
    }

    public void OnCompleted()
    {
        lock (_lock)
        {
            foreach (var observer in _observers.ToList())
            {
                observer.OnCompleted();
            }
        }
    }

    private class Unsubscriber : IDisposable
    {
        private readonly List<IObserver<T>> _observers;
        private readonly IObserver<T> _observer;

        public Unsubscriber(List<IObserver<T>> observers, IObserver<T> observer)
        {
            _observers = observers;
            _observer = observer;
        }

        public void Dispose()
        {
            lock (_observers)
            {
                _observers.Remove(_observer);
            }
        }
    }
}

/// <summary>
/// Microservice HTTP client implementation
/// </summary>
public class MicroserviceClient : IMicroserviceClient
{
    private readonly IServiceRegistry _serviceRegistry;
    private readonly HttpClient _httpClient;
    private readonly ILogger<MicroserviceClient> _logger;

    public MicroserviceClient(
        IServiceRegistry serviceRegistry,
        HttpClient httpClient,
        ILogger<MicroserviceClient> logger)
    {
        _serviceRegistry = serviceRegistry;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string serviceName, string endpoint) where T : class
    {
        var url = await GetServiceUrlAsync(serviceName);
        var fullUrl = $"{url}{endpoint}";

        _logger.LogDebug("GET request: {Url}", fullUrl);

        var response = await _httpClient.GetAsync(fullUrl);
        return await DeserializeResponseAsync<T>(response);
    }

    /// <inheritdoc />
    public async Task<T> PostAsync<T>(string serviceName, string endpoint, object data) where T : class
    {
        var url = await GetServiceUrlAsync(serviceName);
        var fullUrl = $"{url}{endpoint}";

        var content = new StringContent(
            JsonSerializer.Serialize(data),
            System.Text.Encoding.UTF8,
            "application/json");

        _logger.LogDebug("POST request: {Url}", fullUrl);

        var response = await _httpClient.PostAsync(fullUrl, content);
        return await DeserializeResponseAsync<T>(response);
    }

    /// <inheritdoc />
    public async Task<T> PutAsync<T>(string serviceName, string endpoint, object data) where T : class
    {
        var url = await GetServiceUrlAsync(serviceName);
        var fullUrl = $"{url}{endpoint}";

        var content = new StringContent(
            JsonSerializer.Serialize(data),
            System.Text.Encoding.UTF8,
            "application/json");

        _logger.LogDebug("PUT request: {Url}", fullUrl);

        var response = await _httpClient.PutAsync(fullUrl, content);
        return await DeserializeResponseAsync<T>(response);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string serviceName, string endpoint)
    {
        var url = await GetServiceUrlAsync(serviceName);
        var fullUrl = $"{url}{endpoint}";

        _logger.LogDebug("DELETE request: {Url}", fullUrl);

        var response = await _httpClient.DeleteAsync(fullUrl);
        return response.IsSuccessStatusCode;
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> SendAsync(string serviceName, HttpRequestMessage request)
    {
        var url = await GetServiceUrlAsync(serviceName);
        request.RequestUri = new Uri($"{url}{request.RequestUri?.PathAndQuery}");

        return await _httpClient.SendAsync(request);
    }

    /// <inheritdoc />
    public async Task<string> GetServiceUrlAsync(string serviceName)
    {
        var instances = await _serviceRegistry.DiscoverServiceAsync(serviceName);
        var instance = instances.FirstOrDefault();

        if (instance == null)
        {
            throw new InvalidOperationException($"No healthy instances found for service: {serviceName}");
        }

        return instance.BaseUrl;
    }

    private async Task<T> DeserializeResponseAsync<T>(HttpResponseMessage response) where T : class
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogError("HTTP Error {StatusCode}: {Content}", response.StatusCode, content);
            throw new HttpRequestException($"HTTP {response.StatusCode}: {content}");
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException("Failed to deserialize response");
    }
}
