using Loco.Core.Practical;

// John Carmack: "Focus on what actually matters"
// This example shows real-world usage of the practical patterns

// Configure logging
SimpleLoggerFactory.Configure(SimpleLogger.Level.Info, "app.log");
var logger = SimpleLoggerFactory.GetLogger("Main");

logger.Info("Starting practical example application");

// 1. Metrics collection
var metrics = new SimpleMetrics();

// 2. Cache for frequently accessed data
var userCache = new SimpleCache<User>(TimeSpan.FromMinutes(5));

// 3. Background task runner
using var taskRunner = new SimpleBackgroundTaskRunner();

// 4. HTTP client with resilience
using var httpClient = new SimpleHttpClient(timeoutSeconds: 10, maxRetries: 3);

// 5. Queue for async processing
var eventQueue = new FastQueue<Event>(capacity: 1000);

// Start background event processor
taskRunner.RunAsync(async ct =>
{
    logger.Info("Event processor started");

    while (!ct.IsCancellationRequested)
    {
        var evt = await eventQueue.DequeueAsync(5000);
        if (evt != null)
        {
            await metrics.MeasureAsync("event.processing", async () =>
            {
                await ProcessEvent(evt);
                return true;
            });
        }
    }
}, name: "EventProcessor");

// Start health check
taskRunner.RunPeriodic(async ct =>
{
    var health = await CheckHealth();
    metrics.SetGauge("health.status", health ? 1 : 0);

    if (!health)
    {
        logger.Warning("Health check failed");
    }
}, TimeSpan.FromSeconds(30), name: "HealthCheck");

// Simulate API endpoint
await SimulateApiEndpoint();

// Show metrics
ShowMetrics();

// Graceful shutdown
logger.Info("Shutting down...");
await taskRunner.WaitForAllAsync();

// --- Helper functions ---

async Task SimulateApiEndpoint()
{
    logger.Info("Simulating API requests");

    // Process 100 requests
    var tasks = new List<Task>();

    for (int i = 0; i < 100; i++)
    {
        var userId = i % 10; // 10 different users

        tasks.Add(Task.Run(async () =>
        {
            try
            {
                // Check cache first
                var user = userCache.Get($"user:{userId}");

                if (user == null)
                {
                    // Cache miss - fetch from "database"
                    user = await metrics.MeasureAsync("database.fetch", async () =>
                    {
                        await Task.Delay(10); // Simulate DB call
                        return new User { Id = userId, Name = $"User {userId}" };
                    });

                    userCache.Set($"user:{userId}", user);
                    metrics.IncrementCounter("cache.miss");
                }
                else
                {
                    metrics.IncrementCounter("cache.hit");
                }

                // Queue event for async processing
                await eventQueue.EnqueueAsync(new Event
                {
                    Type = "UserAccess",
                    UserId = userId,
                    Timestamp = DateTime.UtcNow
                });

                metrics.IncrementCounter("api.requests");
            }
            catch (Exception ex)
            {
                logger.Error("Request failed", ex);
                metrics.IncrementCounter("api.errors");
            }
        }));
    }

    await Task.WhenAll(tasks);
    logger.Info("API simulation completed");
}

async Task ProcessEvent(Event evt)
{
    // Simulate event processing
    await Task.Delay(5);
    metrics.IncrementCounter($"event.{evt.Type}");
    logger.Debug($"Processed event: {evt.Type} for user {evt.UserId}");
}

async Task<bool> CheckHealth()
{
    try
    {
        // Check external service
        var response = await httpClient.GetAsync("https://www.google.com");
        return response.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}

void ShowMetrics()
{
    var report = metrics.GetReport();

    logger.Info("=== Metrics Report ===");
    foreach (var (key, value) in report.OrderBy(kvp => kvp.Key))
    {
        logger.Info($"{key}: {value}");
    }

    var (poolSize, maxSize, available) = userCache.GetStats();
    logger.Info($"Cache: {poolSize} items");

    var (running, completed, failed) = taskRunner.GetStats();
    logger.Info($"Tasks: {running} running, {completed} completed, {failed} failed");

    var httpMetrics = httpClient.GetMetrics();
    logger.Info($"HTTP Circuit Breaker: {httpClient.GetCircuitBreakerStatus()}");
}

// Simple data classes
record User
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
}

record Event
{
    public string Type { get; init; } = "";
    public int UserId { get; init; }
    public DateTime Timestamp { get; init; }
}