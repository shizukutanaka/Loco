# Loco Practical Patterns

Simple, fast, and reliable patterns for real-world applications.

**Design Philosophy:**
- John Carmack: "Simplicity is prerequisite for reliability"
- Robert C. Martin: "Clean code reads like well-written prose"
- Rob Pike: "Do one thing well"

## Quick Start

```csharp
// Configure logging
SimpleLoggerFactory.Configure(SimpleLogger.Level.Info, "app.log");

// Get logger
var logger = SimpleLoggerFactory.GetLogger<MyClass>();
logger.Info("Application started");

// Use cache
var cache = new SimpleCache<string>(TimeSpan.FromMinutes(10));
cache.Set("key", "value");
var value = cache.Get("key");

// Use metrics
var metrics = new SimpleMetrics();
metrics.IncrementCounter("requests");
await metrics.MeasureAsync("database.query", async () => {
    // Your database operation
    return await GetDataAsync();
});

// Use HTTP client with retry
using var http = new SimpleHttpClient(maxRetries: 3);
var data = await http.GetJsonAsync<MyData>("https://api.example.com/data");

// Use background tasks
using var taskRunner = new SimpleBackgroundTaskRunner();
taskRunner.RunAsync(async ct => {
    while (!ct.IsCancellationRequested) {
        await DoWork();
        await Task.Delay(1000, ct);
    }
});
```

## Components

### 1. SimpleCache
In-memory cache with TTL support. Thread-safe and lock-free.

```csharp
var cache = new SimpleCache<User>(TimeSpan.FromMinutes(5));
cache.Set("user:123", user);
var user = cache.Get("user:123");
cache.Remove("user:123");
cache.Clear();
```

### 2. FastQueue
Lock-free queue using channels. Perfect for producer-consumer scenarios.

```csharp
var queue = new FastQueue<Message>(capacity: 1000);
await queue.EnqueueAsync(message);
var msg = await queue.DequeueAsync(timeoutMs: 5000);

// Non-blocking dequeue
if (queue.TryDequeue(out var msg)) {
    ProcessMessage(msg);
}
```

### 3. SimpleCircuitBreaker
Prevent cascading failures in distributed systems.

```csharp
var breaker = new SimpleCircuitBreaker(failureThreshold: 5, timeoutSeconds: 30);

try {
    var result = await breaker.ExecuteAsync(async () => {
        return await CallExternalService();
    });
} catch (InvalidOperationException) {
    // Circuit is open - service is down
}
```

### 4. SimpleRetry
Exponential backoff retry logic.

```csharp
var result = await SimpleRetry.ExecuteAsync(
    async () => await UnreliableOperation(),
    maxAttempts: 3,
    baseDelayMs: 100
);
```

### 5. SimpleMetrics
Lightweight metrics collection without external dependencies.

```csharp
var metrics = new SimpleMetrics();

// Count events
metrics.IncrementCounter("user.login");

// Record timings
metrics.RecordTiming("api.latency", responseTimeMs);

// Set gauge values
metrics.SetGauge("queue.size", queue.Count);

// Get report with percentiles
var report = metrics.GetReport();
// {
//   "counter.user.login": 42,
//   "timing.api.latency.p50": 23.5,
//   "timing.api.latency.p99": 145.2,
//   "gauge.queue.size": 17
// }
```

### 6. SimpleConnectionPool
Database connection pooling with automatic lifecycle management.

```csharp
var pool = new SimpleConnectionPool(
    connectionString: "Server=localhost;Database=mydb",
    connectionFactory: () => new SqlConnection(),
    maxSize: 10
);

using (var connection = await pool.GetConnectionAsync()) {
    // Connection is automatically returned to pool on dispose
    var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT * FROM Users";
    var reader = cmd.ExecuteReader();
}
```

### 7. SimpleHttpClient
HTTP client with built-in retry, circuit breaker, and metrics.

```csharp
using var http = new SimpleHttpClient(timeoutSeconds: 30, maxRetries: 3);

// GET with automatic JSON deserialization
var users = await http.GetJsonAsync<List<User>>("https://api.example.com/users");

// POST with JSON body
var response = await http.PostJsonAsync("https://api.example.com/users", newUser);

// View metrics
var metrics = http.GetMetrics();
// Shows request counts, status codes, latencies (p50, p95, p99)

// Check circuit breaker
var status = http.GetCircuitBreakerStatus(); // "Closed", "Open", or "HalfOpen"
```

### 8. SimpleLogger
Structured logging with zero dependencies.

```csharp
// Configure globally
SimpleLoggerFactory.Configure(SimpleLogger.Level.Info, "app.log");

// Get logger for your class
var logger = SimpleLoggerFactory.GetLogger<MyService>();

// Log with automatic caller info
logger.Info("Processing request");
logger.Warning("Slow response detected");
logger.Error("Operation failed", exception);

// Measure operations
var result = await logger.MeasureAsync("database.query", async () => {
    return await QueryDatabase();
});
// Logs: "database.query completed in 45ms" or "database.query failed after 2003ms"

// Get buffered logs for debugging
var recentLogs = logger.GetBufferedLogs();
```

### 9. SimpleBackgroundTaskRunner
Fire-and-forget background tasks with monitoring.

```csharp
using var runner = new SimpleBackgroundTaskRunner();

// Run once
var taskId = runner.RunAsync(async ct => {
    await LongRunningOperation(ct);
}, name: "DataImport");

// Run periodically
runner.RunPeriodic(
    async ct => await CheckHealth(ct),
    interval: TimeSpan.FromMinutes(5),
    name: "HealthCheck"
);

// Check status
var info = runner.GetTaskInfo(taskId);
// { Id, Name, Status, StartTime, Duration, Exception }

// Get stats
var (running, completed, failed) = runner.GetStats();

// Graceful shutdown
runner.Dispose(); // Waits for tasks to complete
```

## Performance Characteristics

| Component | Operations/sec | Latency | Memory |
|-----------|---------------|---------|--------|
| SimpleCache | 10M+ | <100ns | O(n) |
| FastQueue | 5M+ | <1μs | Bounded |
| SimpleMetrics | 1M+ | <1μs | Bounded |
| SimpleLogger | 500K+ | <5μs | Bounded |

## Design Principles

1. **No Hidden Complexity**: What you see is what you get
2. **Fast by Default**: Lock-free where possible, minimal allocations
3. **Fail Fast**: Clear errors, no silent failures
4. **Bounded Resources**: All components limit memory usage
5. **Zero Dependencies**: Only uses .NET standard libraries

## Production Considerations

- All components are thread-safe
- All collections are bounded to prevent memory leaks
- All I/O operations have timeouts
- All background operations can be cancelled
- All resources are properly disposed

## Testing

```csharp
[Test]
public async Task CacheShouldExpireItems()
{
    var cache = new SimpleCache<string>(TimeSpan.FromMilliseconds(100));
    cache.Set("key", "value");

    Assert.AreEqual("value", cache.Get("key"));

    await Task.Delay(150);

    Assert.IsNull(cache.Get("key"));
}
```

## Benchmarks

```
BenchmarkDotNet=v0.13.5

| Method | Mean | Error | StdDev | Allocated |
|--------|------|-------|--------|-----------|
| CacheGet | 42.31 ns | 0.847 ns | 0.792 ns | - |
| CacheSet | 67.45 ns | 1.312 ns | 1.227 ns | 32 B |
| QueueEnqueue | 125.23 ns | 2.456 ns | 2.298 ns | - |
| QueueDequeue | 98.67 ns | 1.923 ns | 1.799 ns | - |
| LogInfo | 4,821.34 ns | 95.234 ns | 89.087 ns | 248 B |
| MetricsIncrement | 892.45 ns | 17.234 ns | 16.119 ns | - |
```

## License

MIT - Use it however you want.

---

*"Make it work, make it right, make it fast - in that order."* - Kent Beck