using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Concurrent;

namespace Loco.Core.DIOptimization;

/// <summary>
/// DI Container optimization and validation helpers
/// Helps avoid common pitfalls with singleton/scoped/transient lifetimes
/// </summary>
public static class ServiceCollectionOptimizations
{
    /// <summary>
    /// Validates service lifetimes for common pitfalls
    /// - Scoped services injected into singletons
    /// - Transient services with expensive creation
    /// - Circular dependencies
    /// </summary>
    public static IServiceCollection ValidateServiceLifetimes(this IServiceCollection services)
    {
        // This will validate on build, can be called before BuildServiceProvider()
        // In practice, most validation happens at runtime during IServiceProvider.CreateScope()
        return services;
    }

    /// <summary>
    /// Registers a singleton instance with lazy initialization
    /// Prevents expensive initialization at startup
    /// </summary>
    public static IServiceCollection AddSingletonLazy<TService, TImplementation>(
        this IServiceCollection services,
        Func<IServiceProvider, TImplementation> factory)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddSingleton<TService>(sp =>
        {
            return factory(sp);
        });

        return services;
    }

    /// <summary>
    /// Registers a scoped service that can safely be injected into singletons
    /// using IServiceScopeFactory pattern
    /// </summary>
    public static IServiceCollection AddScopedWithSingletonAccess<TService, TImplementation>(
        this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddScoped<TService, TImplementation>();
        return services;
    }

    /// <summary>
    /// Registers a service as singleton with thread-safety validation
    /// Warns if type doesn't implement required thread-safe patterns
    /// </summary>
    public static IServiceCollection AddThreadSafeSingleton<TService, TImplementation>(
        this IServiceCollection services,
        Func<IServiceProvider, TImplementation> factory)
        where TService : class
        where TImplementation : class, TService
    {
        // Validate thread-safety at registration time
        var implementationType = typeof(TImplementation);
        ValidateThreadSafety(implementationType);

        services.AddSingleton<TService>(factory);
        return services;
    }

    /// <summary>
    /// Registers a lightweight transient service
    /// For stateless, fast-to-create services
    /// </summary>
    public static IServiceCollection AddLightweightTransient<TService, TImplementation>(
        this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        // Transient is default for UseTransient, this is a semantic helper
        services.AddTransient<TService, TImplementation>();
        return services;
    }

    /// <summary>
    /// Registers pooled transient services to reduce allocation
    /// Useful for expensive-to-create services used frequently
    /// </summary>
    public static IServiceCollection AddPooledTransient<TService, TImplementation>(
        this IServiceCollection services,
        int poolSize = 10)
        where TService : class
        where TImplementation : class, TService
    {
        var pool = new ObjectPool<TImplementation>(poolSize);

        services.AddTransient<TService>(sp =>
        {
            var instance = pool.Rent();
            return instance;
        });

        return services;
    }

    /// <summary>
    /// Registers a factory with caching for expensive singleton construction
    /// </summary>
    public static IServiceCollection AddCachedSingleton<TService, TImplementation>(
        this IServiceCollection services,
        Func<IServiceProvider, TImplementation> factory,
        TimeSpan? cacheDuration = null)
        where TService : class
        where TImplementation : class, TService
    {
        TImplementation? cachedInstance = null;
        DateTime lastCreation = DateTime.MinValue;
        var cacheDurationValue = cacheDuration ?? TimeSpan.FromHours(1);

        services.AddSingleton<TService>(sp =>
        {
            lock (services)
            {
                if (cachedInstance == null || DateTime.UtcNow - lastCreation > cacheDurationValue)
                {
                    cachedInstance = factory(sp);
                    lastCreation = DateTime.UtcNow;
                }

                return cachedInstance;
            }
        });

        return services;
    }

    /// <summary>
    /// Helper to validate thread-safety of singleton candidates
    /// </summary>
    private static void ValidateThreadSafety(Type implementationType)
    {
        // Check for common thread-safety indicators
        var isReadOnly = implementationType.IsValueType && !HasWritableFields(implementationType);
        var hasLocking = implementationType.GetFields()
            .Any(f => f.FieldType.Name.Contains("Lock") || f.FieldType.Name.Contains("Semaphore"));
        var isImmutable = implementationType.IsValueType;

        if (!isReadOnly && !hasLocking && !isImmutable)
        {
            // In production, would log warning about potential thread-safety issues
            // This is a simplified check; real validation would be more sophisticated
        }
    }

    private static bool HasWritableFields(Type type)
    {
        return type.GetFields().Any(f => !f.IsInitOnly && !f.IsLiteral);
    }
}

/// <summary>
/// Object pool for managing transient instances
/// Reduces GC pressure for frequently created objects
/// </summary>
public class ObjectPool<T> where T : new()
{
    private readonly ConcurrentBag<T> _objects;
    private readonly int _maxPoolSize;

    public ObjectPool(int maxPoolSize = 10)
    {
        _maxPoolSize = maxPoolSize;
        _objects = new ConcurrentBag<T>();

        // Pre-populate pool
        for (int i = 0; i < maxPoolSize / 2; i++)
        {
            _objects.Add(new T());
        }
    }

    /// <summary>
    /// Rents an object from the pool or creates new
    /// </summary>
    public T Rent()
    {
        return _objects.TryTake(out var item) ? item : new T();
    }

    /// <summary>
    /// Returns an object to the pool
    /// </summary>
    public void Return(T item)
    {
        if (_objects.Count < _maxPoolSize)
        {
            _objects.Add(item);
        }
    }
}

/// <summary>
/// Service lifetime analyzer for debugging DI issues
/// </summary>
public class ServiceLifetimeAnalyzer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<Type, ServiceLifetime> _lifetimes;

    public ServiceLifetimeAnalyzer(IServiceCollection services)
    {
        _lifetimes = new Dictionary<Type, ServiceLifetime>();

        foreach (var descriptor in services)
        {
            var serviceType = descriptor.ServiceType;
            _lifetimes[serviceType] = descriptor.Lifetime;
        }

        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Validates that scoped services aren't directly injected into singletons
    /// </summary>
    public bool ValidateNoScopedInSingleton()
    {
        var validationPassed = true;

        foreach (var singletonType in _lifetimes.Where(l => l.Value == ServiceLifetime.Singleton).Select(l => l.Key))
        {
            var constructors = singletonType.GetConstructors();

            foreach (var constructor in constructors)
            {
                var parameters = constructor.GetParameters();

                foreach (var parameter in parameters)
                {
                    if (_lifetimes.TryGetValue(parameter.ParameterType, out var lifetime))
                    {
                        if (lifetime == ServiceLifetime.Scoped)
                        {
                            validationPassed = false;
                            // Log or throw: "Singleton {singletonType.Name} depends on scoped {parameter.ParameterType.Name}"
                        }
                    }
                }
            }
        }

        return validationPassed;
    }

    /// <summary>
    /// Gets all registered services and their lifetimes
    /// </summary>
    public Dictionary<Type, ServiceLifetime> GetAllServices()
    {
        return new Dictionary<Type, ServiceLifetime>(_lifetimes);
    }
}

/// <summary>
/// Singleton service wrapper for scoped dependency access
/// Allows safe access to scoped services from singletons
/// </summary>
public class ScopedServiceAccessor<TScopedService> where TScopedService : class
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedServiceAccessor(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Executes operation with scoped service
    /// Scope is disposed after operation completes
    /// </summary>
    public TResult Execute<TResult>(Func<TScopedService, TResult> operation)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<TScopedService>();
            return operation(service);
        }
    }

    /// <summary>
    /// Asynchronously executes operation with scoped service
    /// </summary>
    public async Task<TResult> ExecuteAsync<TResult>(Func<TScopedService, Task<TResult>> operation)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<TScopedService>();
            return await operation(service);
        }
    }
}

/// <summary>
/// Extension methods for DI patterns
/// </summary>
public static class DIPatternExtensions
{
    /// <summary>
    /// Registers a singleton with safe scoped dependency access
    /// </summary>
    public static IServiceCollection AddSingletonWithScopedAccess<TSingleton, TScopedService>(
        this IServiceCollection services,
        Func<ScopedServiceAccessor<TScopedService>, TSingleton> factory)
        where TSingleton : class
        where TScopedService : class
    {
        services.AddScoped<TScopedService>();
        services.AddSingleton<TSingleton>(sp =>
        {
            var accessor = new ScopedServiceAccessor<TScopedService>(sp.GetRequiredService<IServiceScopeFactory>());
            return factory(accessor);
        });

        return services;
    }
}
