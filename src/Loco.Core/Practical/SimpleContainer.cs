// Rob Pike: "The bigger the interface, the weaker the abstraction"
// John Carmack: "Dependency injection should be simple and obvious"

using System.Collections.Concurrent;

namespace Loco.Core.Practical;

/// <summary>
/// Simple dependency injection container - No magic, no reflection overhead
/// Constructor injection, lifetime management, simple registration
/// </summary>
public class SimpleContainer
{
    private readonly ConcurrentDictionary<Type, ServiceRegistration> _services = new();
    private readonly ConcurrentDictionary<Type, object> _singletons = new();
    private readonly SimpleLogger _logger;

    public SimpleContainer(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleContainer));
    }

    // Register transient (new instance every time)
    public void RegisterTransient<TService, TImplementation>()
        where TImplementation : TService, new()
    {
        _services[typeof(TService)] = new ServiceRegistration
        {
            Lifetime = ServiceLifetime.Transient,
            Factory = () => new TImplementation()
        };
        _logger.Debug($"Registered transient: {typeof(TService).Name} -> {typeof(TImplementation).Name}");
    }

    // Register transient with factory
    public void RegisterTransient<TService>(Func<TService> factory)
    {
        _services[typeof(TService)] = new ServiceRegistration
        {
            Lifetime = ServiceLifetime.Transient,
            Factory = () => factory()!
        };
        _logger.Debug($"Registered transient with factory: {typeof(TService).Name}");
    }

    // Register singleton (single instance)
    public void RegisterSingleton<TService, TImplementation>()
        where TImplementation : TService, new()
    {
        _services[typeof(TService)] = new ServiceRegistration
        {
            Lifetime = ServiceLifetime.Singleton,
            Factory = () => new TImplementation()
        };
        _logger.Debug($"Registered singleton: {typeof(TService).Name} -> {typeof(TImplementation).Name}");
    }

    // Register singleton with factory
    public void RegisterSingleton<TService>(Func<TService> factory)
    {
        _services[typeof(TService)] = new ServiceRegistration
        {
            Lifetime = ServiceLifetime.Singleton,
            Factory = () => factory()!
        };
        _logger.Debug($"Registered singleton with factory: {typeof(TService).Name}");
    }

    // Register singleton instance
    public void RegisterInstance<TService>(TService instance)
    {
        _singletons[typeof(TService)] = instance!;
        _logger.Debug($"Registered instance: {typeof(TService).Name}");
    }

    // Resolve service
    public T Resolve<T>()
    {
        var service = Resolve(typeof(T));
        return service != null ? (T)service : throw new InvalidOperationException($"Service {typeof(T).Name} not registered");
    }

    // Resolve by type
    public object? Resolve(Type serviceType)
    {
        // Check singleton cache first
        if (_singletons.TryGetValue(serviceType, out var singleton))
        {
            return singleton;
        }

        // Check registrations
        if (_services.TryGetValue(serviceType, out var registration))
        {
            var instance = registration.Factory();

            if (registration.Lifetime == ServiceLifetime.Singleton)
            {
                _singletons[serviceType] = instance;
            }

            return instance;
        }

        _logger.Warning($"Service {serviceType.Name} not registered");
        return null;
    }

    // Try resolve (returns null if not registered)
    public T? TryResolve<T>() where T : class
    {
        return Resolve(typeof(T)) as T;
    }

    // Check if service is registered
    public bool IsRegistered<T>()
    {
        return _services.ContainsKey(typeof(T)) || _singletons.ContainsKey(typeof(T));
    }

    // Clear all registrations
    public void Clear()
    {
        _services.Clear();
        _singletons.Clear();
        _logger.Debug("Container cleared");
    }

    private enum ServiceLifetime
    {
        Transient,
        Singleton
    }

    private class ServiceRegistration
    {
        public ServiceLifetime Lifetime { get; set; }
        public Func<object> Factory { get; set; } = null!;
    }
}

/// <summary>
/// Service locator pattern (use sparingly)
/// </summary>
public static class ServiceLocator
{
    private static SimpleContainer? _container;

    public static void SetContainer(SimpleContainer container)
    {
        _container = container;
    }

    public static T Resolve<T>()
    {
        if (_container == null)
            throw new InvalidOperationException("Container not initialized");
        return _container.Resolve<T>();
    }

    public static T? TryResolve<T>() where T : class
    {
        return _container?.TryResolve<T>();
    }
}

/// <summary>
/// Simple service collection builder
/// </summary>
public class ServiceCollection
{
    private readonly SimpleContainer _container = new();

    public ServiceCollection AddTransient<TService, TImplementation>()
        where TImplementation : TService, new()
    {
        _container.RegisterTransient<TService, TImplementation>();
        return this;
    }

    public ServiceCollection AddTransient<TService>(Func<TService> factory)
    {
        _container.RegisterTransient(factory);
        return this;
    }

    public ServiceCollection AddSingleton<TService, TImplementation>()
        where TImplementation : TService, new()
    {
        _container.RegisterSingleton<TService, TImplementation>();
        return this;
    }

    public ServiceCollection AddSingleton<TService>(Func<TService> factory)
    {
        _container.RegisterSingleton(factory);
        return this;
    }

    public ServiceCollection AddSingleton<TService>(TService instance)
    {
        _container.RegisterInstance(instance);
        return this;
    }

    public SimpleContainer BuildServiceProvider()
    {
        return _container;
    }
}

/// <summary>
/// Constructor injection helper
/// </summary>
public class InjectionHelper
{
    private readonly SimpleContainer _container;

    public InjectionHelper(SimpleContainer container)
    {
        _container = container;
    }

    // Create instance with dependency injection
    public T CreateInstance<T>(params object[] additionalArgs)
    {
        var constructors = typeof(T).GetConstructors();
        if (constructors.Length == 0)
            throw new InvalidOperationException($"No public constructor found for {typeof(T).Name}");

        var constructor = constructors[0]; // Take first constructor
        var parameters = constructor.GetParameters();
        var args = new object[parameters.Length];

        var additionalIndex = 0;
        for (int i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;

            // Try resolve from container
            var resolved = _container.Resolve(paramType);
            if (resolved != null)
            {
                args[i] = resolved;
            }
            else if (additionalIndex < additionalArgs.Length)
            {
                args[i] = additionalArgs[additionalIndex++];
            }
            else
            {
                throw new InvalidOperationException($"Cannot resolve parameter {parameters[i].Name} of type {paramType.Name}");
            }
        }

        return (T)constructor.Invoke(args);
    }
}

/// <summary>
/// Example services
/// </summary>
public interface ILogger
{
    void Log(string message);
}

public class ConsoleLogger : ILogger
{
    public void Log(string message)
    {
        Console.WriteLine($"[LOG] {message}");
    }
}

public interface IDatabase
{
    Task<string> QueryAsync(string sql);
}

public class InMemoryDatabase : IDatabase
{
    public Task<string> QueryAsync(string sql)
    {
        return Task.FromResult($"Result for: {sql}");
    }
}

public class UserService
{
    private readonly ILogger _logger;
    private readonly IDatabase _database;

    public UserService(ILogger logger, IDatabase database)
    {
        _logger = logger;
        _database = database;
    }

    public async Task<string> GetUserAsync(int id)
    {
        _logger.Log($"Getting user {id}");
        return await _database.QueryAsync($"SELECT * FROM users WHERE id = {id}");
    }
}

/// <summary>
/// Example usage
/// </summary>
public class ContainerExamples
{
    public static async Task Examples()
    {
        // Manual registration
        var container = new SimpleContainer();
        container.RegisterSingleton<ILogger, ConsoleLogger>();
        container.RegisterSingleton<IDatabase, InMemoryDatabase>();
        container.RegisterTransient<UserService>(() =>
        {
            var logger = container.Resolve<ILogger>();
            var db = container.Resolve<IDatabase>();
            return new UserService(logger, db);
        });

        var userService = container.Resolve<UserService>();
        await userService.GetUserAsync(1);

        // Using ServiceCollection
        var services = new ServiceCollection()
            .AddSingleton<ILogger, ConsoleLogger>()
            .AddSingleton<IDatabase, InMemoryDatabase>()
            .AddTransient<UserService>(() =>
            {
                var sp = ServiceLocator.Resolve<SimpleContainer>();
                return new UserService(
                    sp.Resolve<ILogger>(),
                    sp.Resolve<IDatabase>()
                );
            });

        var provider = services.BuildServiceProvider();
        ServiceLocator.SetContainer(provider);

        var userService2 = provider.Resolve<UserService>();
        await userService2.GetUserAsync(2);

        // Using InjectionHelper
        var helper = new InjectionHelper(container);
        var userService3 = helper.CreateInstance<UserService>();
        await userService3.GetUserAsync(3);
    }
}

/// <summary>
/// Scoped container (for request-scoped services)
/// </summary>
public class ScopedContainer : IDisposable
{
    private readonly SimpleContainer _parent;
    private readonly SimpleContainer _scoped = new();
    private readonly List<IDisposable> _disposables = new();

    public ScopedContainer(SimpleContainer parent)
    {
        _parent = parent;
    }

    public T Resolve<T>()
    {
        // Try scoped first
        var service = _scoped.Resolve(typeof(T));
        if (service != null)
        {
            if (service is IDisposable disposable)
            {
                _disposables.Add(disposable);
            }
            return (T)service;
        }

        // Fallback to parent
        return _parent.Resolve<T>();
    }

    public void RegisterScoped<TService, TImplementation>()
        where TImplementation : TService, new()
    {
        _scoped.RegisterTransient<TService, TImplementation>();
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
        _disposables.Clear();
        _scoped.Clear();
    }
}

/// <summary>
/// Lazy service resolver
/// </summary>
public class LazyService<T>
{
    private readonly SimpleContainer _container;
    private T? _instance;
    private bool _resolved;

    public LazyService(SimpleContainer container)
    {
        _container = container;
    }

    public T Value
    {
        get
        {
            if (!_resolved)
            {
                _instance = _container.Resolve<T>();
                _resolved = true;
            }
            return _instance!;
        }
    }
}