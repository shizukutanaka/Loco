# Troubleshooting Guide - Loco.Core Practical Patterns

## Overview

Common issues, solutions, and debugging tips for the Practical Patterns library.

## Table of Contents

1. [Performance Issues](#performance-issues)
2. [Memory Issues](#memory-issues)
3. [Concurrency Issues](#concurrency-issues)
4. [Configuration Problems](#configuration-problems)
5. [Common Errors](#common-errors)
6. [Debugging Tips](#debugging-tips)

## Performance Issues

### Problem: Cache Miss Rate Too High

**Symptoms**:
- Slow response times
- High database load
- Cache hit rate < 70%

**Solution**:
```csharp
// Check cache configuration
var cache = new SimpleCache<T>(maxSize: 10000); // Increase if needed

// Use appropriate TTL
cache.Set(key, value, TimeSpan.FromMinutes(5)); // Not too short

// Monitor cache effectiveness
monitor.Increment("cache.hits");
monitor.Increment("cache.misses");

var hitRate = hits / (hits + misses);
if (hitRate < 0.7)
{
    logger.Warning($"Low cache hit rate: {hitRate:P}");
}
```

### Problem: HTTP Server Slow Under Load

**Symptoms**:
- High latency (>100ms)
- Request timeout
- CPU usage normal

**Solution**:
```csharp
// 1. Add response caching
private static readonly SimpleCache<string> _responseCache = new(10000);

server.Get("/api/data", async ctx =>
{
    var cacheKey = $"response:{ctx.Path}";
    var cached = _responseCache.Get(cacheKey);

    if (cached != null)
    {
        ctx.Text(cached);
        return;
    }

    var response = await GenerateResponseAsync();
    _responseCache.Set(cacheKey, response, TimeSpan.FromSeconds(30));
    ctx.Text(response);
});

// 2. Use connection pooling
var pool = new ConnectionPool<SqliteConnection>(
    () => new SqliteConnection(connectionString),
    conn => conn.Close(),
    maxSize: 20 // Increase pool size
);

// 3. Enable async everywhere
server.Get("/api/data", async ctx =>
{
    var data = await db.QueryAsync<T>(sql); // Don't block
    ctx.Json(data);
});
```

### Problem: Slow Serialization

**Symptoms**:
- JSON serialization takes >100ms
- High CPU during serialization

**Solution**:
```csharp
// Use Binary for large objects
var bytes = SimpleSerializer.ToBinary(largeObject); // Faster than JSON

// Enable compression for network transfer
var compressed = SimpleSerializer.Compress(bytes);

// Pool StringBuilder for custom serialization
var sb = CommonPools.RentStringBuilder();
try
{
    // Build string
    sb.Append("data");
    return sb.ToString();
}
finally
{
    CommonPools.ReturnStringBuilder(sb);
}
```

### Problem: Background Jobs Running Slowly

**Symptoms**:
- Job queue backing up
- Jobs taking longer than expected

**Solution**:
```csharp
// 1. Increase parallelism
var workflow = new ParallelWorkflow<T>(
    process: ProcessItemAsync,
    maxDegreeOfParallelism: 8 // Increase from default 4
);

// 2. Batch operations
await queue.EnqueueBatchAsync(items); // Faster than individual enqueues

// 3. Use job priorities
jobSystem.Enqueue("HighPriority", async () =>
{
    await ProcessImportantWorkAsync();
});

// 4. Monitor job execution time
using var timer = perfMonitor.StartTimer("job.duration");
await ProcessJobAsync();
```

## Memory Issues

### Problem: Memory Leak in Cache

**Symptoms**:
- Memory grows over time
- GC collections increasing
- Cache never shrinks

**Solution**:
```csharp
// Set max size
var cache = new SimpleCache<T>(maxSize: 10000); // Enforces limit

// Use TTL to auto-expire
cache.Set(key, value, TimeSpan.FromMinutes(5)); // Auto cleanup

// Periodic manual cleanup
var scheduler = new SimpleScheduler();
scheduler.ScheduleRecurring(TimeSpan.FromMinutes(10), async () =>
{
    cache.Clear(); // Or implement selective cleanup
});

// Monitor cache size
monitor.RecordMetric("cache.size", cache.Count);
```

### Problem: High Memory Usage from Logging

**Symptoms**:
- Memory spikes during logging
- Many string allocations
- High Gen0 GC collections

**Solution**:
```csharp
// 1. Use appropriate log levels
logger.SetLevel(LogLevel.Info); // Not Debug in production

// 2. Avoid expensive string operations
// Bad:
logger.Info($"Processing {items.Count} items with {string.Join(",", items)}");

// Good:
logger.Info("Processing items", new { count = items.Count });

// 3. Implement log rotation
var logger = new SimpleLogger(
    filePath: "logs/app.log",
    maxSizeMb: 10 // Rotate after 10MB
);

// 4. Use buffered logging
var logger = new BufferedLogger(
    batchSize: 100,
    flushInterval: TimeSpan.FromSeconds(5)
);
```

### Problem: Object Pool Not Reducing Allocations

**Symptoms**:
- High allocation rate despite pooling
- Pool hit rate low
- GC pressure unchanged

**Solution**:
```csharp
// 1. Increase pool size
var pool = new SimpleObjectPool<T>(
    factory: () => new T(),
    resetAction: t => t.Reset(),
    maxSize: 100 // Increase if needed
);

// 2. Ensure objects are returned
using var pooled = autoPool.Rent(); // Auto-return on dispose
// Use pooled.Value

// Or manually:
var obj = pool.Rent();
try
{
    // Use obj
}
finally
{
    pool.Return(obj); // Always return
}

// 3. Monitor pool effectiveness
var (pooled, total, maxSize) = pool.GetStats();
var hitRate = pooled / (double)total;
if (hitRate < 0.5)
{
    logger.Warning($"Low pool hit rate: {hitRate:P}");
}
```

### Problem: Array Allocations Too High

**Symptoms**:
- Large Gen0 GC collections
- High memory allocation rate
- Many temporary arrays

**Solution**:
```csharp
// Use ArrayPool
var buffer = BufferPool.RentBuffer(4096);
try
{
    // Use buffer
    await stream.ReadAsync(buffer, 0, buffer.Length);
}
finally
{
    BufferPool.ReturnBuffer(buffer); // Always return
}

// For custom sizes
var arrayPool = new SimpleArrayPool<byte>();
var array = arrayPool.Rent(minimumLength: 1024);
try
{
    // Use array
}
finally
{
    arrayPool.Return(array, clearArray: true);
}
```

## Concurrency Issues

### Problem: Deadlock in Queue

**Symptoms**:
- Application hangs
- Queue operations never complete
- Thread blocked indefinitely

**Solution**:
```csharp
// Use async patterns consistently
await queue.EnqueueAsync(item); // Don't use .Result or .Wait()

// Set timeouts
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await queue.EnqueueAsync(item, cts.Token);

// Check queue capacity
if (queue.Count >= queue.Capacity)
{
    logger.Warning("Queue full, consider increasing capacity");
}

// Monitor queue size
monitor.RecordMetric("queue.size", queue.Count);
```

### Problem: Race Condition in Cache

**Symptoms**:
- Inconsistent cache values
- Occasional null returns
- Random errors

**Solution**:
```csharp
// Use GetOrAdd pattern
var value = cache.GetOrAdd(key, () =>
{
    return LoadExpensiveValue();
});

// Or with async
var value = await cache.GetOrAddAsync(key, async () =>
{
    return await LoadExpensiveValueAsync();
});

// Avoid check-then-act
// Bad:
if (cache.Get(key) == null)
{
    cache.Set(key, value); // Race condition!
}

// Good:
cache.GetOrAdd(key, () => value); // Atomic
```

### Problem: Thread Starvation

**Symptoms**:
- Slow processing despite low CPU
- Tasks queuing up
- Long wait times

**Solution**:
```csharp
// 1. Use ConfigureAwait(false) for library code
public async Task<T> LibraryMethodAsync()
{
    var result = await DoWorkAsync().ConfigureAwait(false);
    return result;
}

// 2. Increase thread pool size if needed
ThreadPool.SetMinThreads(
    workerThreads: Environment.ProcessorCount * 2,
    completionPortThreads: Environment.ProcessorCount * 2
);

// 3. Use parallel processing
var parallelPipeline = new ParallelPipeline<T>(
    process: ProcessAsync,
    maxDegreeOfParallelism: Environment.ProcessorCount
);

// 4. Monitor thread pool
monitor.RecordMetric("threadpool.available",
    ThreadPool.ThreadCount - ThreadPool.PendingWorkItemCount);
```

## Configuration Problems

### Problem: Configuration Not Loading

**Symptoms**:
- Default values always used
- Config file changes ignored
- Environment variables not recognized

**Solution**:
```csharp
// 1. Check file path
var config = new ConfigBuilder()
    .AddJsonFile("appsettings.json") // Use absolute path if needed
    .Build();

// Verify file exists
if (!File.Exists("appsettings.json"))
{
    logger.Warning("Config file not found, using defaults");
}

// 2. Check JSON format
try
{
    var json = File.ReadAllText("appsettings.json");
    var test = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
}
catch (Exception ex)
{
    logger.Error("Invalid JSON in config file", ex);
}

// 3. Check environment variable prefix
var config = new ConfigBuilder()
    .AddEnvironmentVariables("APP_") // Must match env var names
    .Build();

// APP_Port=8080 -> config.Get<int>("Port")

// 4. Check command line format
// --Port=8080 or /Port=8080
var config = new ConfigBuilder()
    .AddCommandLine(args)
    .Build();
```

### Problem: Configuration Reload Not Working

**Symptoms**:
- Config changes not applied
- Must restart application
- Hot reload fails

**Solution**:
```csharp
// Enable reload
var config = new ConfigBuilder()
    .AddJsonFile("appsettings.json", reloadOnChange: true)
    .Build();

// Listen for changes
config.OnReload += (sender, e) =>
{
    logger.Info("Configuration reloaded");
    // Update services as needed
};

// Verify file watcher working
var watcher = new FileSystemWatcher(Path.GetDirectoryName("appsettings.json"));
watcher.Changed += (s, e) => logger.Info($"File changed: {e.Name}");
watcher.EnableRaisingEvents = true;
```

## Common Errors

### Error: "Object pool returned object not from pool"

**Cause**: Returning wrong object or returning twice

**Solution**:
```csharp
// Always pair Rent/Return correctly
var obj = pool.Rent();
try
{
    // Use obj
}
finally
{
    pool.Return(obj); // Return same object
}

// Or use auto-return
using var pooled = autoPool.Rent();
// Automatically returned on dispose
```

### Error: "Rate limit storage full"

**Cause**: Too many unique keys in rate limiter

**Solution**:
```csharp
// Clean up old entries periodically
var limiter = new SlidingWindowRateLimiter(100, TimeSpan.FromMinutes(1));

// Schedule cleanup
scheduler.ScheduleRecurring(TimeSpan.FromMinutes(5), async () =>
{
    limiter.Cleanup();
});

// Or use fixed window (less memory)
var limiter = new FixedWindowRateLimiter(100, TimeSpan.FromMinutes(1));
```

### Error: "Circuit breaker open"

**Cause**: Too many failures, circuit opened

**Solution**:
```csharp
var breaker = new SimpleCircuitBreaker(
    failureThreshold: 5,
    resetTimeout: TimeSpan.FromSeconds(30)
);

// Check state before calling
if (breaker.State == CircuitState.Open)
{
    logger.Warning("Circuit breaker open, using fallback");
    return fallbackValue;
}

// Or handle exception
try
{
    return await breaker.ExecuteAsync(async () => await ExternalCall());
}
catch (CircuitBreakerOpenException)
{
    return fallbackValue;
}

// Monitor circuit breaker state
monitor.RecordEvent("circuitbreaker", breaker.State.ToString());
```

### Error: "JWT token validation failed"

**Cause**: Token expired, invalid signature, or wrong secret

**Solution**:
```csharp
// 1. Check token expiration
var auth = new SimpleAuth(
    secret: "your-secret-key-32-chars-minimum!",
    expirationMinutes: 60 // Increase if needed
);

// 2. Verify secret matches
var (valid, principal) = auth.ValidateToken(token);
if (!valid)
{
    logger.Warning("Token validation failed");
    // Check if secret key matches between generation and validation
}

// 3. Check token format
if (!token.StartsWith("eyJ"))
{
    logger.Error("Invalid JWT format");
}

// 4. Add clock skew tolerance
var validationParams = new TokenValidationParameters
{
    ClockSkew = TimeSpan.FromMinutes(5) // Allow 5 min difference
};
```

## Debugging Tips

### Enable Detailed Logging

```csharp
// Set debug level
var logger = SimpleLoggerFactory.GetLogger("App");
logger.SetLevel(LogLevel.Debug);

// Log all operations
logger.Debug("Cache operation", new
{
    operation = "Get",
    key = cacheKey,
    hit = cached != null
});
```

### Monitor Key Metrics

```csharp
var monitor = new SimpleMonitor();

// Record everything
monitor.RecordMetric("operation.duration", duration.TotalMilliseconds);
monitor.Increment("operation.count");
monitor.RecordEvent("operation", "completed", new Dictionary<string, string>
{
    ["status"] = "success"
});

// Generate dashboard
var dashboard = new MonitorDashboard(monitor);
Console.WriteLine(dashboard.GenerateTextDashboard());
```

### Use Performance Profiling

```csharp
var perfMonitor = new PerformanceMonitor(monitor);

// Time operations
using var timer = perfMonitor.StartTimer("operation.name");
await DoWorkAsync();

// Check results
var snapshot = monitor.GetSnapshot();
var metric = snapshot.Metrics.First(m => m.Name == "operation.name");
logger.Info($"Average: {metric.Average}ms, P99: {metric.Max}ms");
```

### Dump Current State

```csharp
// Cache state
logger.Info("Cache state", new
{
    size = cache.Count,
    capacity = cache.Capacity,
    hitRate = cache.HitRate
});

// Queue state
logger.Info("Queue state", new
{
    count = queue.Count,
    capacity = queue.Capacity,
    utilization = queue.Count / (double)queue.Capacity
});

// Pool state
var (pooled, total, maxSize) = pool.GetStats();
logger.Info("Pool state", new
{
    pooled,
    total,
    maxSize,
    hitRate = pooled / (double)total
});
```

### Test Under Load

```csharp
// Simulate high load
var tasks = new List<Task>();
for (int i = 0; i < 1000; i++)
{
    tasks.Add(Task.Run(async () =>
    {
        await ProcessRequestAsync();
    }));
}

await Task.WhenAll(tasks);

// Check for issues
var snapshot = monitor.GetSnapshot();
var errors = snapshot.Metrics.FirstOrDefault(m => m.Name.Contains("error"));
if (errors != null && errors.Count > 0)
{
    logger.Error($"Found {errors.Count} errors during load test");
}
```

## Getting Help

If you encounter issues not covered here:

1. **Check Logs**: Enable debug logging first
2. **Check Metrics**: Look for anomalies in dashboard
3. **Check Resources**: CPU, memory, disk, network
4. **Simplify**: Remove patterns until issue disappears
5. **Isolate**: Test pattern in isolation
6. **Benchmark**: Compare with expected performance

## Common Performance Targets

- Cache: >10M ops/sec, <100ns latency
- Queue: >5M ops/sec, <1μs latency
- Logger: >1M ops/sec, <10μs latency
- HTTP: >50K req/sec, <2ms P50 latency
- Database: >10K queries/sec, <1ms latency
- Serialization: >100K ops/sec, <50μs latency

If your numbers are significantly lower, review this guide and check your configuration.

---

**Last Updated**: 2025-11-07
**Version**: 1.0
