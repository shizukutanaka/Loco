# Integration Guide - Combining Loco.Core Practical Patterns

## Overview

This guide shows how to combine multiple patterns to build complete, production-ready applications. Each pattern is designed to work independently, but they compose naturally to solve real-world problems.

## Table of Contents

1. [Basic Integration Patterns](#basic-integration-patterns)
2. [Common Application Architectures](#common-application-architectures)
3. [Pattern Combinations](#pattern-combinations)
4. [Best Practices](#best-practices)
5. [Anti-Patterns to Avoid](#anti-patterns-to-avoid)

## Basic Integration Patterns

### 1. Configuration + Logging + Metrics

The foundation of any application:

```csharp
public class ApplicationBootstrap
{
    public static (SimpleConfig, SimpleLogger, SimpleMetrics) Initialize(string[] args)
    {
        // Load configuration
        var config = new ConfigBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables("APP_")
            .AddCommandLine(args)
            .Build();

        // Setup logging
        var logLevel = config.Get<string>("LogLevel", "Info");
        var logger = SimpleLoggerFactory.GetLogger("App");
        logger.SetLevel(logLevel switch
        {
            "Debug" => LogLevel.Debug,
            "Info" => LogLevel.Info,
            "Warning" => LogLevel.Warning,
            "Error" => LogLevel.Error,
            _ => LogLevel.Info
        });

        // Setup metrics
        var metrics = new SimpleMetrics();

        logger.Info("Application initialized", new
        {
            LogLevel = logLevel,
            Environment = config.Get<string>("Environment", "Development")
        });

        return (config, logger, metrics);
    }
}
```

### 2. DI Container + All Patterns

Dependency injection ties everything together:

```csharp
public class ServiceRegistry
{
    public static void RegisterServices(
        SimpleContainer container,
        SimpleConfig config,
        SimpleLogger logger,
        SimpleMetrics metrics)
    {
        // Core services
        container.RegisterInstance(config);
        container.RegisterInstance(logger);
        container.RegisterInstance(metrics);

        // Caching
        container.RegisterSingleton(() => new SimpleCache<string>(
            maxSize: config.Get<int>("Cache:MaxSize", 10000)
        ));

        // Database
        container.RegisterSingleton(() => new SimpleDatabase(
            () => new SqliteConnection(config.Get<string>("ConnectionString"))
        ));

        // Storage
        container.RegisterSingleton<IStorage>(() => new LocalStorage(
            config.Get<string>("Storage:Path", "./data"),
            logger
        ));

        // HTTP client
        container.RegisterSingleton(() => new SimpleApiClient(
            config.Get<string>("ApiBaseUrl"),
            logger
        ));

        // Auth
        container.RegisterSingleton(() => new SimpleAuth(
            config.Get<string>("JwtSecret"),
            config.Get<int>("TokenExpiration", 60)
        ));

        // Rate limiter
        container.RegisterSingleton<IRateLimiter>(() => new TokenBucketRateLimiter(
            capacity: config.Get<int>("RateLimit:Capacity", 100),
            refillRate: config.Get<int>("RateLimit:RefillRate", 10),
            refillInterval: TimeSpan.FromSeconds(1),
            logger
        ));

        // Feature flags
        container.RegisterSingleton(() => new SimpleFeatureFlags(logger));

        // Monitoring
        var monitor = new SimpleMonitor(logger: logger);
        container.RegisterInstance(monitor);
        container.RegisterSingleton(() => new PerformanceMonitor(monitor));

        // Background jobs
        container.RegisterSingleton(() => new SimpleJobSystem(logger, metrics));

        // Notifications
        container.RegisterSingleton(() => new SimpleNotificationService(logger));

        logger.Info("Services registered successfully");
    }
}
```

## Common Application Architectures

### 1. REST API with Full Stack

Complete API server with authentication, caching, monitoring:

```csharp
public class RestApiApplication
{
    private readonly SimpleContainer _container;
    private readonly SimpleHttpServer _server;
    private readonly SimpleLogger _logger;
    private readonly SimpleMonitor _monitor;

    public RestApiApplication(string[] args)
    {
        // Bootstrap
        var (config, logger, metrics) = ApplicationBootstrap.Initialize(args);
        _logger = logger;

        // Setup DI
        _container = new SimpleContainer(logger);
        ServiceRegistry.RegisterServices(_container, config, logger, metrics);

        // Get monitor
        _monitor = _container.Resolve<SimpleMonitor>();

        // Setup HTTP server
        var port = config.Get<int>("Port", 8080);
        _server = new SimpleHttpServer(port, logger);

        // Configure middleware
        ConfigureMiddleware();

        // Configure routes
        ConfigureRoutes();

        logger.Info($"REST API application ready on port {port}");
    }

    private void ConfigureMiddleware()
    {
        var perfMonitor = _container.Resolve<PerformanceMonitor>();
        var rateLimiter = _container.Resolve<IRateLimiter>();

        // Performance monitoring
        _server.Use(async (ctx, next) =>
        {
            using var timer = perfMonitor.StartTimer($"api.{ctx.Request.Method}.{ctx.Path}");
            _monitor.Increment($"requests.{ctx.Request.Method}");
            await next();
        });

        // Logging
        _server.Use(CommonMiddleware.Logger(_logger));

        // CORS
        _server.Use(CommonMiddleware.Cors());

        // Rate limiting
        _server.Use(async (ctx, next) =>
        {
            var ip = ctx.Request.RemoteEndPoint?.Address?.ToString() ?? "unknown";
            if (!await rateLimiter.TryAcquireAsync(ip))
            {
                ctx.StatusCode = 429;
                ctx.Json(new { error = "Rate limit exceeded" });
                _monitor.Increment("requests.rate_limited");
                return;
            }
            await next();
        });

        // Error handling
        _server.Use(CommonMiddleware.ErrorHandler());
    }

    private void ConfigureRoutes()
    {
        var authService = new AuthService(
            _container.Resolve<SimpleAuth>(),
            new InMemoryUserStore(),
            _logger
        );

        // Health check
        _server.Get("/health", async ctx =>
        {
            var snapshot = _monitor.GetSnapshot();
            ctx.Json(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                metrics = snapshot.Metrics.Take(5)
            });
            await Task.CompletedTask;
        });

        // Metrics endpoint
        _server.Get("/metrics", async ctx =>
        {
            var dashboard = new MonitorDashboard(_monitor);
            var data = dashboard.GenerateJsonDashboard();
            ctx.Json(data);
            await Task.CompletedTask;
        });

        // Auth endpoints
        _server.Post("/api/auth/register", async ctx =>
        {
            var request = await ctx.ReadJsonAsync<RegisterRequest>();
            if (request == null)
            {
                ctx.StatusCode = 400;
                ctx.Json(new { error = "Invalid request" });
                return;
            }

            var (success, userId, error) = await authService.RegisterAsync(
                request.Username, request.Email, request.Password);

            if (success)
            {
                _monitor.Increment("auth.registrations");
                ctx.Json(new { userId, message = "Registration successful" });
            }
            else
            {
                ctx.StatusCode = 400;
                ctx.Json(new { error });
            }
        });

        _server.Post("/api/auth/login", async ctx =>
        {
            var request = await ctx.ReadJsonAsync<LoginRequest>();
            if (request == null)
            {
                ctx.StatusCode = 400;
                ctx.Json(new { error = "Invalid request" });
                return;
            }

            var (success, token, error) = await authService.LoginAsync(
                request.Username, request.Password);

            if (success)
            {
                _monitor.Increment("auth.logins");
                ctx.Json(new { token });
            }
            else
            {
                ctx.StatusCode = 401;
                ctx.Json(new { error });
            }
        });

        // Protected endpoint with caching
        var cache = _container.Resolve<SimpleCache<UserData>>();
        _server.Get("/api/users/:id", async ctx =>
        {
            var token = ctx.Headers.GetValueOrDefault("Authorization", "").Replace("Bearer ", "");
            var user = await authService.GetUserFromTokenAsync(token);

            if (user == null)
            {
                ctx.StatusCode = 401;
                ctx.Json(new { error = "Unauthorized" });
                return;
            }

            var userId = ctx.PathParams["id"];
            var cacheKey = $"user:{userId}";

            // Try cache first
            var cachedUser = cache.Get(cacheKey);
            if (cachedUser != null)
            {
                _monitor.Increment("cache.hits");
                ctx.Json(cachedUser);
                return;
            }

            // Load from database
            _monitor.Increment("cache.misses");
            var db = _container.Resolve<SimpleDatabase>();
            var userData = await db.QuerySingleAsync<UserData>(
                "SELECT * FROM users WHERE id = @id",
                new { id = userId }
            );

            if (userData != null)
            {
                cache.Set(cacheKey, userData, TimeSpan.FromMinutes(5));
                ctx.Json(userData);
            }
            else
            {
                ctx.StatusCode = 404;
                ctx.Json(new { error = "User not found" });
            }

            await Task.CompletedTask;
        });
    }

    public void Start()
    {
        _server.Start();
        _logger.Info("REST API started");
    }

    public void Stop()
    {
        _server.Stop();
        _logger.Info("REST API stopped");
    }

    record RegisterRequest(string Username, string Email, string Password);
    record LoginRequest(string Username, string Password);
    record UserData(string Id, string Username, string Email, DateTime CreatedAt);
}
```

### 2. Background Worker Service

Processing jobs with monitoring and scheduling:

```csharp
public class WorkerService
{
    private readonly SimpleJobSystem _jobSystem;
    private readonly SimpleScheduler _scheduler;
    private readonly SimpleMonitor _monitor;
    private readonly SimpleLogger _logger;
    private readonly IStorage _storage;

    public WorkerService(
        SimpleConfig config,
        SimpleLogger logger,
        SimpleMetrics metrics)
    {
        _logger = logger;
        _jobSystem = new SimpleJobSystem(logger, metrics);
        _scheduler = new SimpleScheduler(logger);
        _monitor = new SimpleMonitor(logger: logger);
        _storage = new LocalStorage(config.Get<string>("Storage:Path", "./data"), logger);

        SetupJobs();
    }

    private void SetupJobs()
    {
        // Recurring cleanup job - every hour
        _jobSystem.ScheduleRecurring("CleanupOldFiles", async () =>
        {
            using var timer = new PerformanceMonitor(_monitor).StartTimer("job.cleanup");
            _logger.Info("Running cleanup job");

            var files = await _storage.ListKeysAsync("");
            var cutoff = DateTime.UtcNow.AddDays(-7);
            var deleted = 0;

            foreach (var file in files)
            {
                var metadata = await _storage.GetMetadataAsync(file);
                if (metadata?.CreatedAt < cutoff)
                {
                    await _storage.DeleteAsync(file);
                    deleted++;
                }
            }

            _monitor.Increment("cleanup.files_deleted", deleted);
            _logger.Info($"Cleanup completed: {deleted} files deleted");

        }, TimeSpan.FromHours(1));

        // Daily report - 3 AM
        _jobSystem.ScheduleCron("DailyReport", async () =>
        {
            _logger.Info("Generating daily report");

            var snapshot = _monitor.GetSnapshot();
            var report = new
            {
                Date = DateTime.UtcNow.Date,
                Metrics = snapshot.Metrics,
                Events = snapshot.RecentEvents
            };

            var reportJson = SimpleSerializer.ToJson(report, pretty: true);
            await _storage.SaveAsync(
                $"reports/daily-{DateTime.UtcNow:yyyy-MM-dd}.json",
                System.Text.Encoding.UTF8.GetBytes(reportJson)
            );

            _monitor.RecordEvent("report", "Daily report generated");
            _logger.Info("Daily report generated successfully");

        }, "0 3 * * *");

        // High-priority job processing
        _jobSystem.ScheduleRecurring("ProcessHighPriorityQueue", async () =>
        {
            _logger.Debug("Processing high priority queue");
            // Process queue logic here
            await Task.Delay(100); // Simulate work
            _monitor.Increment("queue.high_priority.processed");

        }, TimeSpan.FromSeconds(10));

        // Health monitoring
        _jobSystem.ScheduleRecurring("HealthCheck", async () =>
        {
            var snapshot = _monitor.GetSnapshot();

            // Check for issues
            var errorMetric = snapshot.Metrics.FirstOrDefault(m => m.Name.Contains("error"));
            if (errorMetric != null && errorMetric.Count > 100)
            {
                _logger.Warning($"High error count detected: {errorMetric.Count}");
                _monitor.RecordEvent("alert", "High error rate", new Dictionary<string, string>
                {
                    ["count"] = errorMetric.Count.ToString()
                });
            }

            await Task.CompletedTask;

        }, TimeSpan.FromMinutes(5));
    }

    public async Task StartAsync()
    {
        _scheduler.Start();
        _logger.Info("Worker service started");
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        await _jobSystem.StopAsync();
        _scheduler.Dispose();
        _logger.Info("Worker service stopped");
    }
}
```

### 3. Microservice with All Features

Complete microservice architecture:

```csharp
public class Microservice
{
    private readonly SimpleContainer _container;
    private readonly SimpleHttpServer _server;
    private readonly WorkerService _worker;
    private readonly SimpleLogger _logger;
    private readonly SimpleMonitor _monitor;
    private readonly SimpleNotificationService _notifications;

    public Microservice(string[] args)
    {
        // Bootstrap
        var (config, logger, metrics) = ApplicationBootstrap.Initialize(args);
        _logger = logger;

        // DI
        _container = new SimpleContainer(logger);
        ServiceRegistry.RegisterServices(_container, config, logger, metrics);

        // Monitoring
        _monitor = _container.Resolve<SimpleMonitor>();
        var resourceMonitor = new ResourceMonitor(_monitor);

        // Alerts
        _notifications = _container.Resolve<SimpleNotificationService>();
        _notifications.RegisterChannel("console", new ConsoleNotificationChannel());

        var alertSystem = new AlertSystem(_monitor, _notifications, logger);
        alertSystem.AddRule(new AlertRule
        {
            Name = "High Memory Usage",
            MetricName = "system.memory.mb",
            Condition = m => m.LastValue > 500
        });
        alertSystem.AddRule(new AlertRule
        {
            Name = "High Error Rate",
            MetricName = "errors.total",
            Condition = m => m.Count > 100
        });

        // Periodic alert checking
        var jobSystem = _container.Resolve<SimpleJobSystem>();
        jobSystem.ScheduleRecurring("CheckAlerts", async () =>
        {
            await alertSystem.CheckRulesAsync();
        }, TimeSpan.FromMinutes(1));

        // HTTP server
        var port = config.Get<int>("Port", 8080);
        _server = new SimpleHttpServer(port, logger);
        ConfigureApi();

        // Worker
        _worker = new WorkerService(config, logger, metrics);

        logger.Info("Microservice initialized");
    }

    private void ConfigureApi()
    {
        var perfMonitor = _container.Resolve<PerformanceMonitor>();

        // Middleware
        _server.Use(async (ctx, next) =>
        {
            using var timer = perfMonitor.StartTimer($"api.{ctx.Path}");
            await next();
        });

        _server.Use(CommonMiddleware.Logger(_logger));
        _server.Use(CommonMiddleware.ErrorHandler());

        // Routes
        _server.Get("/health", async ctx =>
        {
            ctx.Json(new { status = "healthy", timestamp = DateTime.UtcNow });
            await Task.CompletedTask;
        });

        _server.Get("/metrics", async ctx =>
        {
            var snapshot = _monitor.GetSnapshot();
            ctx.Json(snapshot);
            await Task.CompletedTask;
        });

        _server.Get("/dashboard", async ctx =>
        {
            var dashboard = new MonitorDashboard(_monitor);
            var text = dashboard.GenerateTextDashboard();
            ctx.Text(text);
            await Task.CompletedTask;
        });
    }

    public async Task RunAsync()
    {
        _server.Start();
        await _worker.StartAsync();
        _logger.Info("Microservice running");

        // Wait for shutdown signal
        Console.CancelKeyPress += async (s, e) =>
        {
            e.Cancel = true;
            await ShutdownAsync();
        };

        await Task.Delay(-1);
    }

    private async Task ShutdownAsync()
    {
        _logger.Info("Shutting down microservice...");
        _server.Stop();
        await _worker.StopAsync();
        await _notifications.StopAsync();
        _logger.Info("Microservice stopped");
    }
}
```

## Pattern Combinations

### Caching + Database + Monitoring

```csharp
public class CachedRepository<T> where T : class
{
    private readonly SimpleCache<T> _cache;
    private readonly SimpleDatabase _db;
    private readonly SimpleMonitor _monitor;
    private readonly string _tableName;

    public CachedRepository(
        SimpleCache<T> cache,
        SimpleDatabase db,
        SimpleMonitor monitor,
        string tableName)
    {
        _cache = cache;
        _db = db;
        _monitor = monitor;
        _tableName = tableName;
    }

    public async Task<T?> GetByIdAsync(string id)
    {
        using var timer = new PerformanceMonitor(_monitor).StartTimer($"repo.{_tableName}.get");

        var cacheKey = $"{_tableName}:{id}";
        var cached = _cache.Get(cacheKey);

        if (cached != null)
        {
            _monitor.Increment($"cache.{_tableName}.hit");
            return cached;
        }

        _monitor.Increment($"cache.{_tableName}.miss");
        var result = await _db.QuerySingleAsync<T>(
            $"SELECT * FROM {_tableName} WHERE id = @id",
            new { id }
        );

        if (result != null)
        {
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        }

        return result;
    }

    public async Task<bool> SaveAsync(string id, T entity)
    {
        using var timer = new PerformanceMonitor(_monitor).StartTimer($"repo.{_tableName}.save");

        // Save to database
        var affected = await _db.ExecuteAsync(
            $"INSERT OR REPLACE INTO {_tableName} VALUES (@entity)",
            new { entity }
        );

        // Invalidate cache
        var cacheKey = $"{_tableName}:{id}";
        _cache.Remove(cacheKey);
        _monitor.Increment($"cache.{_tableName}.invalidate");

        return affected > 0;
    }
}
```

### Rate Limiting + Feature Flags + Auth

```csharp
public class ProtectedEndpoint
{
    private readonly IRateLimiter _rateLimiter;
    private readonly SimpleFeatureFlags _features;
    private readonly SimpleAuth _auth;

    public async Task<bool> HandleRequestAsync(HttpContext ctx)
    {
        // Extract user
        var token = ctx.Headers.GetValueOrDefault("Authorization", "").Replace("Bearer ", "");
        var (valid, principal) = _auth.ValidateToken(token);

        if (!valid || principal == null)
        {
            ctx.StatusCode = 401;
            ctx.Json(new { error = "Unauthorized" });
            return false;
        }

        var userId = principal.FindFirst("userId")?.Value ?? "unknown";

        // Check feature flag
        if (!_features.IsEnabled("api-v2", userId))
        {
            ctx.StatusCode = 403;
            ctx.Json(new { error = "Feature not available" });
            return false;
        }

        // Rate limiting
        if (!await _rateLimiter.TryAcquireAsync(userId))
        {
            ctx.StatusCode = 429;
            ctx.Json(new { error = "Rate limit exceeded" });
            return false;
        }

        // Process request
        return true;
    }
}
```

### Workflow + Jobs + Notifications

```csharp
public class OrderProcessingPipeline
{
    private readonly SimpleWorkflow _workflow;
    private readonly SimpleJobSystem _jobs;
    private readonly SimpleNotificationService _notifications;
    private readonly SimpleLogger _logger;

    public OrderProcessingPipeline(
        SimpleJobSystem jobs,
        SimpleNotificationService notifications,
        SimpleLogger logger)
    {
        _jobs = jobs;
        _notifications = notifications;
        _logger = logger;

        _workflow = BuildWorkflow();
    }

    private SimpleWorkflow BuildWorkflow()
    {
        var workflow = new SimpleWorkflow();

        workflow.AddStep("ValidateOrder", async (context) =>
        {
            var order = context["order"] as Order;
            if (order == null || order.Items.Count == 0)
            {
                _logger.Warning($"Invalid order");
                return false;
            }
            return true;
        });

        workflow.AddStep("CheckInventory", async (context) =>
        {
            // Check inventory
            await Task.Delay(100);
            return true;
        });

        workflow.AddStep("ProcessPayment", async (context) =>
        {
            // Process payment
            await Task.Delay(200);
            context["paymentId"] = Guid.NewGuid().ToString();
            return true;
        });

        workflow.AddStep("SendConfirmation", async (context) =>
        {
            var order = context["order"] as Order;

            // Schedule notification
            _jobs.Enqueue("SendOrderEmail", async () =>
            {
                await _notifications.SendToChannelAsync(
                    "email",
                    "Order Confirmation",
                    $"Your order #{order?.Id} has been confirmed"
                );
            });

            return true;
        });

        return workflow;
    }

    public async Task<bool> ProcessOrderAsync(Order order)
    {
        var context = new Dictionary<string, object>
        {
            ["order"] = order
        };

        var success = await _workflow.ExecuteAsync();

        if (success)
        {
            _logger.Info($"Order {order.Id} processed successfully");
        }
        else
        {
            _logger.Error($"Order {order.Id} processing failed: {_workflow.Error}");
        }

        return success;
    }
}

record Order(string Id, List<OrderItem> Items, string CustomerId);
record OrderItem(string ProductId, int Quantity, decimal Price);
```

## Best Practices

### 1. Always Use DI Container

```csharp
// Good: Services resolved from container
var service = container.Resolve<IMyService>();

// Bad: Direct instantiation
var service = new MyService(new Dependency1(), new Dependency2());
```

### 2. Monitor Everything

```csharp
// Wrap operations with monitoring
using var timer = perfMonitor.StartTimer("operation.name");
try
{
    await DoWorkAsync();
    monitor.Increment("operation.success");
}
catch (Exception ex)
{
    monitor.Increment("operation.failure");
    throw;
}
```

### 3. Use Caching Strategically

```csharp
// Cache expensive operations
var cacheKey = $"user:{userId}:profile";
var cached = cache.Get(cacheKey);
if (cached == null)
{
    cached = await LoadExpensiveDataAsync(userId);
    cache.Set(cacheKey, cached, TimeSpan.FromMinutes(5));
}
```

### 4. Implement Circuit Breakers

```csharp
var circuitBreaker = new SimpleCircuitBreaker(
    failureThreshold: 5,
    resetTimeout: TimeSpan.FromSeconds(30)
);

await circuitBreaker.ExecuteAsync(async () =>
{
    return await externalService.CallAsync();
});
```

### 5. Use Object Pooling

```csharp
// Pool frequently allocated objects
var pool = new SimpleObjectPool<StringBuilder>(
    () => new StringBuilder(256),
    sb => sb.Clear()
);

var sb = pool.Rent();
try
{
    sb.Append("data");
    return sb.ToString();
}
finally
{
    pool.Return(sb);
}
```

## Anti-Patterns to Avoid

### ❌ Don't Mix Concerns

```csharp
// Bad: HTTP logic in business logic
public class OrderService
{
    public async Task ProcessOrder(HttpContext ctx)
    {
        var order = await ctx.ReadJsonAsync<Order>();
        // Business logic mixed with HTTP
    }
}

// Good: Separate concerns
public class OrderService
{
    public async Task<bool> ProcessOrder(Order order)
    {
        // Pure business logic
    }
}
```

### ❌ Don't Over-Cache

```csharp
// Bad: Cache everything
cache.Set("key", value, TimeSpan.FromDays(30));

// Good: Cache selectively with appropriate TTL
cache.Set("user:profile", profile, TimeSpan.FromMinutes(5));
```

### ❌ Don't Ignore Errors

```csharp
// Bad: Swallow exceptions
try
{
    await DoWorkAsync();
}
catch { }

// Good: Log and handle appropriately
try
{
    await DoWorkAsync();
}
catch (Exception ex)
{
    logger.Error("Work failed", ex);
    monitor.Increment("errors.work");
    throw;
}
```

### ❌ Don't Create Circular Dependencies

```csharp
// Bad: A depends on B, B depends on A
public class ServiceA
{
    public ServiceA(ServiceB b) { }
}
public class ServiceB
{
    public ServiceB(ServiceA a) { }
}

// Good: Use interfaces or refactor
public class ServiceA
{
    public ServiceA(IServiceB b) { }
}
```

## Conclusion

The Practical Patterns library is designed for composability. Start with the basics (Config, Logger, Metrics), add patterns as needed, and use the DI container to wire everything together. Each pattern is independent but works seamlessly with others.

Key principles:
- **Start simple**: Add patterns only when needed
- **Monitor everything**: Metrics guide optimization
- **Cache strategically**: Not everything needs caching
- **Handle errors**: Log, monitor, and recover gracefully
- **Use DI**: Container makes testing and changes easy

---

**Next Steps**:
- See [EXAMPLES.md](EXAMPLES.md) for complete applications
- See [BENCHMARKS.md](BENCHMARKS.md) for performance tuning
- See [SUMMARY.md](SUMMARY.md) for design philosophy
