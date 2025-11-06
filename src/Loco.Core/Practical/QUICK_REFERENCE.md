# Quick Reference Card - Loco Practical Patterns

## 🚀 One-Liners for Common Tasks

### Logging
```csharp
var logger = SimpleLoggerFactory.GetLogger("App");
logger.Info("Message", new { userId, timestamp });
logger.Error("Failed", exception);
```

### Caching
```csharp
var cache = new SimpleCache<User>(maxSize: 10000);
cache.Set("key", user, TimeSpan.FromMinutes(5));
var user = cache.Get("key");
```

### HTTP Server
```csharp
var server = new SimpleHttpServer(8080);
server.Get("/api/users", async ctx => ctx.Json(users));
server.Start();
```

### Database
```csharp
var db = new SimpleDatabase(() => new SqliteConnection(connStr));
var users = await db.QueryAsync<User>("SELECT * FROM users WHERE active = @active", new { active = true });
await db.ExecuteAsync("INSERT INTO users (name) VALUES (@name)", new { name });
```

### Background Jobs
```csharp
var jobs = new SimpleJobSystem(logger);
jobs.Enqueue("SendEmail", async () => await SendEmailAsync());
jobs.ScheduleRecurring("Cleanup", async () => await CleanupAsync(), TimeSpan.FromHours(1));
```

### Authentication
```csharp
var auth = new SimpleAuth("secret-key-32-chars-minimum!");
var token = auth.GenerateToken("user123", claims);
var (valid, principal) = auth.ValidateToken(token);
```

### Rate Limiting
```csharp
var limiter = new TokenBucketRateLimiter(100, 10, TimeSpan.FromSeconds(1));
if (await limiter.TryAcquireAsync(userId)) { /* allow */ }
```

### Validation
```csharp
var validator = new Validator<User>()
    .Rule(u => ValidationRules.NotEmpty(u.Email), "Email required")
    .Rule(u => ValidationRules.IsEmail(u.Email), "Invalid email");
var result = validator.Validate(user);
```

### Configuration
```csharp
var config = new ConfigBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables("APP_")
    .Build();
var port = config.Get<int>("Port", 8080);
```

### Monitoring
```csharp
var monitor = new SimpleMonitor();
monitor.Increment("requests.count");
monitor.RecordMetric("response.time", duration.TotalMilliseconds);
using var timer = new PerformanceMonitor(monitor).StartTimer("operation");
```

---

## 📊 Performance Quick Reference

| Pattern | Ops/Sec | Latency | Memory |
|---------|---------|---------|--------|
| SimpleCache | 10M+ | <100ns | ~24B/item |
| FastQueue | 5M+ | <1μs | ~16B/item |
| SimpleLogger | 1M+ | <10μs | ~200B/log |
| SimpleMetrics | 10M+ | <50ns | Minimal |
| SimpleHttpServer | 50K+ | <2ms | <5MB |
| SimpleDatabase | 10K+ | <1ms | ~5MB |
| SimpleEventBus | 1M+ | <5μs | ~32B/sub |
| SimpleObjectPool | 10M+ | <100ns | ~32B/obj |

---

## 🔧 Common Patterns

### DI Setup
```csharp
var container = new SimpleContainer(logger);
container.RegisterSingleton<IUserService, UserService>();
container.RegisterTransient<IOrderService, OrderService>();
var service = container.Resolve<IUserService>();
```

### Retry with Circuit Breaker
```csharp
var retry = new SimpleRetry(maxAttempts: 3, delay: TimeSpan.FromSeconds(1));
var breaker = new SimpleCircuitBreaker(failureThreshold: 5, resetTimeout: TimeSpan.FromSeconds(30));
var result = await retry.ExecuteAsync(() => breaker.ExecuteAsync(() => CallApiAsync()));
```

### Workflow
```csharp
var workflow = new WorkflowBuilder()
    .Step("Validate", async () => { /* validate */ return true; })
    .Step("Process", async () => { /* process */ return true; })
    .Build();
var success = await workflow.ExecuteAsync();
```

### Event Bus
```csharp
var bus = new SimpleEventBus();
bus.Subscribe<OrderCreated>(evt => Console.WriteLine($"Order: {evt.OrderId}"));
bus.Publish(new OrderCreated { OrderId = "123" });
```

### Storage
```csharp
var storage = new LocalStorage("./data", logger);
await storage.SaveJsonAsync("users/1.json", user);
var user = await storage.LoadJsonAsync<User>("users/1.json");
```

### Notifications
```csharp
var notifications = new SimpleNotificationService(logger);
notifications.RegisterChannel("email", new EmailNotificationChannel(email));
await notifications.SendToChannelAsync("email", "Subject", "Body");
```

### Feature Flags
```csharp
var flags = new SimpleFeatureFlags(logger);
flags.RegisterFlag("new-ui", enabled: false, percentageRollout: 10);
if (flags.IsEnabled("new-ui", userId)) { /* show new UI */ }
```

### Object Pool
```csharp
var pool = new SimpleObjectPool<StringBuilder>(() => new StringBuilder(), sb => sb.Clear());
var sb = pool.Rent();
try { sb.Append("data"); return sb.ToString(); }
finally { pool.Return(sb); }
```

### Pipeline
```csharp
var pipeline = Pipeline.Create<InputData, OutputData>()
    .AddStage("Validate", data => Validate(data))
    .AddStage("Transform", data => Transform(data))
    .AddStage("Process", data => Process(data));
var result = await pipeline.ExecuteAsync(input);
```

---

## 🎯 Decision Matrix

### When to use each pattern

| Need | Pattern | Alternative |
|------|---------|-------------|
| **Cache data** | SimpleCache | UnifiedCache (multi-tier) |
| **Log messages** | SimpleLogger | File logging built-in |
| **Track metrics** | SimpleMetrics | SimpleMonitoring (full stack) |
| **HTTP server** | SimpleHttpServer | ASP.NET Core (if heavy) |
| **Database** | SimpleDatabase | EF Core (if heavy ORM needed) |
| **Background jobs** | SimpleJob | Hangfire (if persistence needed) |
| **Pub/Sub** | SimpleEventBus | SimpleMessageBroker (topics) |
| **Queue** | FastQueue | System.Channel (if .NET 6+) |
| **Config** | SimpleConfig | appsettings.json only (simple) |
| **DI** | SimpleContainer | MS.DI (if heavy features) |
| **Auth** | SimpleAuth | Identity (if complex auth) |
| **Validation** | SimpleValidation | FluentValidation (if heavy) |
| **Rate limit** | SimpleRateLimiter | Third-party (if distributed) |
| **Workflow** | SimpleWorkflow | WorkflowCore (if heavy) |

---

## ⚡ Performance Tips

### Caching
```csharp
// ✅ Good: Reasonable cache size and TTL
cache.Set(key, value, TimeSpan.FromMinutes(5));

// ❌ Bad: Cache too long, memory leak risk
cache.Set(key, value, TimeSpan.FromDays(30));
```

### Logging
```csharp
// ✅ Good: Structured logging
logger.Info("User action", new { userId, action });

// ❌ Bad: String interpolation in hot path
logger.Info($"User {userId} did {action}");
```

### Database
```csharp
// ✅ Good: Parameterized query
await db.QueryAsync<User>("SELECT * FROM users WHERE id = @id", new { id });

// ❌ Bad: String concatenation (SQL injection!)
await db.QueryAsync<User>($"SELECT * FROM users WHERE id = {id}");
```

### Object Pool
```csharp
// ✅ Good: Always return to pool
var obj = pool.Rent();
try { /* use obj */ }
finally { pool.Return(obj); }

// ❌ Bad: Forget to return
var obj = pool.Rent();
// Memory leak!
```

---

## 🐛 Debugging Quick Fixes

### High Memory
```csharp
// Check cache size
monitor.RecordMetric("cache.size", cache.Count);

// Reduce cache size or TTL
var cache = new SimpleCache<T>(maxSize: 1000); // Smaller
cache.Set(key, value, TimeSpan.FromMinutes(1)); // Shorter
```

### Slow Response
```csharp
// Add performance monitoring
using var timer = perfMonitor.StartTimer("api.endpoint");

// Check metrics
var snapshot = monitor.GetSnapshot();
var slow = snapshot.Metrics.Where(m => m.Average > 100); // >100ms
```

### Deadlock
```csharp
// Always use async consistently
await queue.EnqueueAsync(item); // Don't use .Result or .Wait()

// Set timeouts
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await operation(cts.Token);
```

### Cache Miss
```csharp
// Monitor hit rate
monitor.Increment(cached != null ? "cache.hit" : "cache.miss");
var hitRate = hits / (double)(hits + misses);
if (hitRate < 0.7) logger.Warning($"Low hit rate: {hitRate:P}");
```

---

## 📦 Migration Quick Guide

### From Entity Framework
```csharp
// Before
var users = await context.Users.Where(u => u.Active).ToListAsync();

// After
var users = await db.QueryAsync<User>("SELECT * FROM users WHERE active = @active", new { active = true });
```

### From Serilog
```csharp
// Before
Log.Information("User {UserId} logged in", userId);

// After
logger.Info("User logged in", new { userId });
```

### From Hangfire
```csharp
// Before
BackgroundJob.Enqueue(() => SendEmail(userId));

// After
jobSystem.Enqueue("SendEmail", async () => await SendEmail(userId));
```

### From AutoMapper
```csharp
// Before
var dto = mapper.Map<UserDto>(user);

// After
var mapper = new SimpleMapper();
var dto = mapper.Map<User, UserDto>(user);
```

---

## 📖 Complete Documentation

- **[INDEX.md](INDEX.md)** - Master navigation guide
- **[README.md](README.md)** - All patterns overview
- **[EXAMPLES.md](EXAMPLES.md)** - Complete applications
- **[INTEGRATION_GUIDE.md](INTEGRATION_GUIDE.md)** - Combining patterns
- **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** - Problem solving
- **[MIGRATION_GUIDE.md](MIGRATION_GUIDE.md)** - From frameworks
- **[BENCHMARKS.md](BENCHMARKS.md)** - Performance data

---

## 🎯 Most Common Combinations

### Web API Stack
```csharp
SimpleHttpServer + SimpleAuth + SimpleCache + SimpleDatabase + SimpleLogger + SimpleMonitoring
```

### Worker Service Stack
```csharp
SimpleJobSystem + SimpleScheduler + SimpleStorage + SimpleMonitoring + SimpleLogger
```

### Microservice Stack
```csharp
All of the above + SimpleEventBus + SimpleNotification + SimpleFeatureFlags
```

---

**Print this for quick reference while coding!**

**Version**: 1.0 | **Last Updated**: 2025-11-07
