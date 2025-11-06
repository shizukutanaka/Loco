# Complete Application Examples

Real-world examples showing how to use practical patterns together.

## Example 1: Simple Web API

```csharp
using Loco.Core.Practical;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Setup configuration
        var config = new ConfigBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables("APP_")
            .AddCommandLine(args)
            .Build();

        // Setup logging
        var logger = SimpleLoggerFactory.GetLogger("API");

        // Setup DI container
        var container = new SimpleContainer(logger);
        container.RegisterSingleton<SimpleLogger>(() => logger);
        container.RegisterSingleton<IUserStore, InMemoryUserStore>();
        container.RegisterSingleton(() => new SimpleAuth(
            config.Get<string>("JwtSecret", "default-secret-key-change-in-production"),
            config.Get<int>("TokenExpiration", 60)
        ));
        container.RegisterSingleton<AuthService>(() =>
            new AuthService(
                container.Resolve<SimpleAuth>(),
                container.Resolve<IUserStore>(),
                logger
            )
        );

        // Setup HTTP server
        var port = config.Get<int>("Port", 8080);
        var server = new SimpleHttpServer(port, logger);

        // Add middleware
        server.Use(CommonMiddleware.Logger(logger));
        server.Use(CommonMiddleware.Cors());
        server.Use(CommonMiddleware.ErrorHandler());

        // Setup routes
        SetupRoutes(server, container);

        // Start server
        server.Start();
        logger.Info($"Server running on http://localhost:{port}");

        Console.WriteLine("Press Enter to stop...");
        Console.ReadLine();

        server.Stop();
        server.Dispose();
    }

    private static void SetupRoutes(SimpleHttpServer server, SimpleContainer container)
    {
        var authService = container.Resolve<AuthService>();

        // Health check
        server.Get("/health", async ctx =>
        {
            ctx.Json(new { status = "healthy", timestamp = DateTime.UtcNow });
            await Task.CompletedTask;
        });

        // Register user
        server.Post("/api/auth/register", async ctx =>
        {
            var request = await ctx.ReadJsonAsync<RegisterRequest>();
            if (request == null)
            {
                ctx.StatusCode = 400;
                ctx.Json(new { error = "Invalid request" });
                return;
            }

            var (success, userId, error) = await authService.RegisterAsync(
                request.Username,
                request.Email,
                request.Password
            );

            if (success)
            {
                ctx.Json(new { userId, message = "Registration successful" });
            }
            else
            {
                ctx.StatusCode = 400;
                ctx.Json(new { error });
            }
        });

        // Login
        server.Post("/api/auth/login", async ctx =>
        {
            var request = await ctx.ReadJsonAsync<LoginRequest>();
            if (request == null)
            {
                ctx.StatusCode = 400;
                ctx.Json(new { error = "Invalid request" });
                return;
            }

            var (success, token, error) = await authService.LoginAsync(
                request.Username,
                request.Password
            );

            if (success)
            {
                ctx.Json(new { token });
            }
            else
            {
                ctx.StatusCode = 401;
                ctx.Json(new { error });
            }
        });

        // Protected endpoint
        server.Get("/api/profile", async ctx =>
        {
            var token = ctx.Headers.GetValueOrDefault("Authorization", "").Replace("Bearer ", "");
            var user = await authService.GetUserFromTokenAsync(token);

            if (user == null)
            {
                ctx.StatusCode = 401;
                ctx.Json(new { error = "Unauthorized" });
                return;
            }

            ctx.Json(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.CreatedAt
            });

            await Task.CompletedTask;
        });
    }

    record RegisterRequest(string Username, string Email, string Password);
    record LoginRequest(string Username, string Password);
}
```

## Example 2: Background Job Processor

```csharp
using Loco.Core.Practical;

public class BackgroundProcessor
{
    public static async Task Main()
    {
        var logger = SimpleLoggerFactory.GetLogger("Processor");
        var jobSystem = new SimpleJobSystem(logger);
        var monitor = new SimpleMonitor(logger: logger);

        // Setup resource monitoring
        var resourceMonitor = new ResourceMonitor(monitor);

        // Schedule recurring jobs
        jobSystem.ScheduleCron("CleanupOldFiles", async () =>
        {
            using var timer = new PerformanceMonitor(monitor).StartTimer("cleanup.duration");
            logger.Info("Running cleanup job");

            // Cleanup logic
            await Task.Delay(100);
            monitor.Increment("cleanup.runs");

        }, "0 3 * * *"); // Daily at 3 AM

        jobSystem.ScheduleRecurring("HealthCheck", async () =>
        {
            logger.Info("Health check");
            monitor.RecordEvent("healthcheck", "System healthy");
            await Task.CompletedTask;
        }, TimeSpan.FromMinutes(5));

        // Process work queue
        var workQueue = new FastQueue<WorkItem>(1000);

        // Start consumers
        for (int i = 0; i < 4; i++)
        {
            var consumerId = i;
            _ = Task.Run(async () =>
            {
                logger.Info($"Consumer {consumerId} started");

                while (true)
                {
                    var item = await workQueue.DequeueAsync();
                    if (item == null) continue;

                    using var timer = new PerformanceMonitor(monitor).StartTimer("work.processing");

                    try
                    {
                        await ProcessWorkItemAsync(item, logger);
                        monitor.Increment("work.completed");
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Failed to process work item", ex);
                        monitor.Increment("work.failed");
                    }
                }
            });
        }

        // Enqueue work
        for (int i = 0; i < 100; i++)
        {
            await workQueue.EnqueueAsync(new WorkItem { Id = i, Data = $"Data {i}" });
        }

        // Setup dashboard
        var dashboard = new MonitorDashboard(monitor);
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(10000);
                Console.Clear();
                Console.WriteLine(dashboard.GenerateTextDashboard());
            }
        });

        logger.Info("Background processor running. Press Enter to stop...");
        Console.ReadLine();

        resourceMonitor.Dispose();
        jobSystem.Dispose();
    }

    private static async Task ProcessWorkItemAsync(WorkItem item, SimpleLogger logger)
    {
        logger.Debug($"Processing work item {item.Id}");
        await Task.Delay(100); // Simulate work
    }

    record WorkItem
    {
        public int Id { get; init; }
        public string Data { get; init; } = "";
    }
}
```

## Example 3: Data Processing Pipeline

```csharp
using Loco.Core.Practical;

public class DataPipelineApp
{
    public static async Task Main()
    {
        var logger = SimpleLoggerFactory.GetLogger("Pipeline");
        var storage = new LocalStorage("./data", logger);
        var monitor = new SimpleMonitor(logger: logger);

        // Build workflow
        var workflow = new WorkflowBuilder()
            .Step("ExtractData", async () =>
            {
                using var timer = new PerformanceMonitor(monitor).StartTimer("extract");
                logger.Info("Extracting data...");

                // Simulate data extraction
                await Task.Delay(500);
                var data = Enumerable.Range(1, 1000).Select(i => new DataRow
                {
                    Id = i,
                    Value = Random.Shared.Next(1, 100)
                }).ToList();

                await storage.SaveJsonAsync("raw/data.json", data);
                monitor.Increment("extract.rows", data.Count);
                return true;
            })
            .Step("TransformData", async () =>
            {
                using var timer = new PerformanceMonitor(monitor).StartTimer("transform");
                logger.Info("Transforming data...");

                var rawData = await storage.LoadJsonAsync<List<DataRow>>("raw/data.json");
                if (rawData == null) return false;

                var transformed = rawData
                    .Where(r => r.Value > 50)
                    .Select(r => new TransformedRow
                    {
                        Id = r.Id,
                        Category = r.Value > 75 ? "High" : "Medium"
                    })
                    .ToList();

                await storage.SaveJsonAsync("transformed/data.json", transformed);
                monitor.Increment("transform.rows", transformed.Count);
                return true;
            })
            .Retry("LoadData", async () =>
            {
                using var timer = new PerformanceMonitor(monitor).StartTimer("load");
                logger.Info("Loading data...");

                var data = await storage.LoadJsonAsync<List<TransformedRow>>("transformed/data.json");
                if (data == null) return false;

                // Simulate database load
                await Task.Delay(300);
                monitor.Increment("load.rows", data.Count);
                return true;
            }, maxRetries: 3)
            .Build();

        // Execute workflow
        var success = await workflow.ExecuteAsync();

        if (success)
        {
            logger.Info("Pipeline completed successfully");

            // Generate report
            var snapshot = monitor.GetSnapshot();
            foreach (var metric in snapshot.Metrics)
            {
                logger.Info($"{metric.Name}: {metric.Count} (avg: {metric.Average:F2}ms)");
            }
        }
        else
        {
            logger.Error($"Pipeline failed: {workflow.Error}");
        }
    }

    record DataRow
    {
        public int Id { get; init; }
        public int Value { get; init; }
    }

    record TransformedRow
    {
        public int Id { get; init; }
        public string Category { get; init; } = "";
    }
}
```

## Example 4: Microservice with All Features

```csharp
using Loco.Core.Practical;

public class MicroserviceApp
{
    private static SimpleContainer? _container;
    private static SimpleMonitor? _monitor;

    public static async Task Main(string[] args)
    {
        // Initialize
        var config = new ConfigBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var logger = SimpleLoggerFactory.GetLogger("Service");
        _monitor = new SimpleMonitor(logger: logger);

        // Setup DI
        _container = new SimpleContainer(logger);
        RegisterServices(_container, config, logger);

        // Setup monitoring
        var resourceMonitor = new ResourceMonitor(_monitor);
        var perfMonitor = new PerformanceMonitor(_monitor);

        // Setup notifications
        var notificationService = new SimpleNotificationService(logger);
        notificationService.RegisterChannel("console", new ConsoleNotificationChannel());

        // Setup alerts
        var alertSystem = new AlertSystem(_monitor, notificationService, logger);
        alertSystem.AddRule(new AlertRule
        {
            Name = "High Memory",
            MetricName = "system.memory.mb",
            Condition = m => m.LastValue > 500
        });

        // Setup job system
        var jobSystem = new SimpleJobSystem(logger);

        // Schedule jobs
        jobSystem.ScheduleRecurring("Metrics", async () =>
        {
            await alertSystem.CheckRulesAsync();
        }, TimeSpan.FromMinutes(1));

        // Start HTTP server
        var server = _container.Resolve<SimpleHttpServer>();
        SetupEndpoints(server, perfMonitor);
        server.Start();

        logger.Info("Microservice started");

        // Graceful shutdown
        Console.CancelKeyPress += async (s, e) =>
        {
            e.Cancel = true;
            logger.Info("Shutting down...");
            server.Stop();
            await jobSystem.StopAsync();
            jobSystem.Dispose();
            resourceMonitor.Dispose();
        };

        await Task.Delay(-1);
    }

    private static void RegisterServices(SimpleContainer container, SimpleConfig config, SimpleLogger logger)
    {
        container.RegisterInstance(logger);
        container.RegisterInstance(config);
        container.RegisterInstance(_monitor!);

        container.RegisterSingleton(() => new SimpleHttpServer(
            config.Get<int>("Port", 8080),
            logger
        ));

        container.RegisterSingleton<IStorage>(() => new LocalStorage("./data", logger));
    }

    private static void SetupEndpoints(SimpleHttpServer server, PerformanceMonitor perfMonitor)
    {
        server.Get("/metrics", async ctx =>
        {
            using var timer = perfMonitor.StartTimer("api.metrics");
            var snapshot = _monitor!.GetSnapshot();
            ctx.Json(snapshot);
            await Task.CompletedTask;
        });

        server.Get("/health", async ctx =>
        {
            ctx.Json(new { status = "healthy" });
            await Task.CompletedTask;
        });
    }
}
```

## Key Takeaways

1. **Start Simple**: Begin with basic patterns and add complexity only when needed
2. **Compose Patterns**: Combine multiple patterns for complete solutions
3. **Monitor Everything**: Use SimpleMonitor to track performance and errors
4. **Handle Errors**: Use retry logic and circuit breakers
5. **Stay Fast**: All patterns are optimized for performance
6. **Keep It Clear**: Code should be obvious and easy to debug

All examples follow the philosophy:
- **Simplicity** over cleverness
- **Clarity** over conciseness
- **Reliability** over features
