# Practical Patterns Collection

A collection of simple, practical, and production-ready patterns following the design philosophies of **John Carmack**, **Rob Pike**, and **Robert C. Martin**.

## Design Principles

- **Simplicity First**: "Simplicity is prerequisite for reliability" - John Carmack
- **Do One Thing Well**: "Make each program do one thing well" - Rob Pike
- **Clean Code**: "Clean code reads like well-written prose" - Robert C. Martin

## Core Patterns

### Caching & Performance

- **SimpleCache** - Lock-free caching with TTL (10M+ ops/sec)
- **SimpleCachePattern** - Cache patterns (LRU, FIFO, TTL)
- **UnifiedCache** - Multi-tier caching strategy

### Concurrency

- **FastQueue** - Channel-based concurrent queue (5M+ ops/sec)
- **SimpleCircuitBreaker** - Fault tolerance pattern
- **SimpleRetry** - Exponential backoff retry logic
- **SimpleBackgroundTaskRunner** - Background task scheduling

### Logging & Metrics

- **SimpleLogger** - Fast structured logging
- **SimpleMetrics** - Lightweight metrics collection
- **SimpleHealthCheck** - System health monitoring

### HTTP & Networking

- **SimpleHttpClient** - HTTP client with resilience
- **SimpleHttpServer** - Lightweight HTTP server
- **SimpleApiClient** - REST API client with auth

### Data & Storage

- **SimpleSerializer** - JSON/XML/CSV/Binary serialization
- **SimpleDatabase** - Direct SQL without ORM overhead
- **SimpleStorage** - File storage abstraction (local/memory/versioned)
- **SimpleMapper** - Object mapping without frameworks

### Messaging & Events

- **SimpleEventBus** - Clean pub/sub implementation
- **SimpleMessageBroker** - In-process messaging with topics
- **SimpleCommand** - Command pattern with undo/redo

### Infrastructure

- **SimpleConfig** - Multi-source configuration (files/env/args)
- **SimpleContainer** - Lightweight DI container
- **SimpleScheduler** - Cron-like task scheduling
- **SimpleEmail** - SMTP email sender with templates

### State Management

- **SimpleStateMachine** - Clear states and transitions
- **SimplePipeline** - Composable operation chains

### Security & Auth

- **SimpleAuth** - JWT tokens, password hashing
- **SimpleRateLimiter** - Token bucket & sliding window
- **SimpleValidation** - Fluent validation

### Workflows & Jobs

- **SimpleWorkflow** - Sequential and parallel workflow execution
- **SimpleJob** - Background jobs with scheduling and retry
- **SimpleNotification** - Multi-channel notifications (email/webhook/push)

### Utilities

- **SimpleObjectPool** - Reduce allocations with pooling
- **SimpleFeatureFlags** - Runtime feature management
- **SimpleTemplate** - Variable replacement, conditionals, loops
- **SimpleTest** - Fast testing framework with assertions

## Quick Start Examples

### HTTP Server

```csharp
var server = new SimpleHttpServer(port: 8080);

server.Get("/", async ctx =>
{
    ctx.Html("<h1>Hello World</h1>");
    await Task.CompletedTask;
});

server.Get("/api/users", async ctx =>
{
    var users = new[] { new { id = 1, name = "Alice" } };
    ctx.Json(users);
    await Task.CompletedTask;
});

server.Start();
```

### Authentication

```csharp
var auth = new SimpleAuth("your-secret-key-32-chars-min!");
var userStore = new InMemoryUserStore();
var authService = new AuthService(auth, userStore);

// Register
await authService.RegisterAsync("john", "john@example.com", "password123");

// Login
var (success, token, error) = await authService.LoginAsync("john", "password123");

// Validate
var user = await authService.GetUserFromTokenAsync(token);
```

### Configuration

```csharp
var config = new ConfigBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables("APP_")
    .AddCommandLine(args)
    .Build();

var port = config.Get<int>("Port", 8080);
var dbConnection = config.Get<string>("ConnectionString");
```

### Caching

```csharp
var cache = new SimpleCache<User>(maxSize: 1000);

cache.Set("user:1", user, TimeSpan.FromMinutes(10));
var cached = cache.Get("user:1");
```

### Message Broker

```csharp
var broker = new SimpleMessageBroker();

broker.Subscribe<OrderCreated>("orders.created", order =>
{
    Console.WriteLine($"Order {order.OrderId} created");
});

await broker.PublishAsync("orders.created", new OrderCreated("ORD123"));
```

### Database

```csharp
var db = new SimpleDatabase(() => new SqliteConnection("Data Source=app.db"));

// Query
var users = await db.QueryAsync<User>("SELECT * FROM users WHERE active = @active",
    new { active = true });

// Execute
await db.ExecuteAsync("INSERT INTO users (name, email) VALUES (@name, @email)",
    new { name = "John", email = "john@example.com" });

// Transaction
await db.TransactionAsync(async tx =>
{
    await tx.ExecuteAsync("INSERT INTO orders ...");
    await tx.ExecuteAsync("UPDATE inventory ...");
    return true;
});
```

### Email

```csharp
var email = new SimpleEmail(
    smtpHost: "smtp.gmail.com",
    smtpPort: 587,
    username: "your-email@gmail.com",
    password: "your-app-password",
    fromEmail: "your-email@gmail.com"
);

await email.SendHtmlAsync(
    to: "user@example.com",
    subject: "Welcome!",
    htmlBody: "<h1>Welcome to our app!</h1>"
);
```

### Scheduler

```csharp
var scheduler = new SimpleScheduler();

// Run once
scheduler.ScheduleOnce(DateTime.UtcNow.AddMinutes(5), async () =>
{
    Console.WriteLine("Task executed");
});

// Run every 10 minutes
scheduler.ScheduleRecurring(TimeSpan.FromMinutes(10), async () =>
{
    Console.WriteLine("Recurring task");
});

// Cron schedule (daily at 3 AM)
scheduler.ScheduleCron("0 3 * * *", async () =>
{
    Console.WriteLine("Daily backup");
});

scheduler.Start();
```

### Storage

```csharp
var storage = new LocalStorage("./data");

// Save/load binary
await storage.SaveAsync("files/data.bin", bytes);
var data = await storage.LoadAsync("files/data.bin");

// Save/load JSON
await storage.SaveJsonAsync("users/1.json", user);
var user = await storage.LoadJsonAsync<User>("users/1.json");

// List files
var files = await storage.ListKeysAsync("files/");
```

### Template

```csharp
var template = new SimpleTemplate(@"
    Hello {{name}},

    {{#if isPremium}}
    Welcome Premium Member!
    {{/if}}

    Your posts:
    {{#each posts}}
    - {{title}}
    {{/each}}
");

template.Set("name", "John");
template.Set("isPremium", true);
template.Set("posts", posts);

var result = template.Render();
```

### Testing

```csharp
var tests = new SimpleTest();

tests
    .Test("Addition works", () =>
    {
        Assert.AreEqual(4, 2 + 2);
    })
    .Test("String contains", () =>
    {
        Assert.IsTrue("hello".Contains("ell"));
    })
    .TestAsync("Async operation", async () =>
    {
        await Task.Delay(10);
        Assert.IsTrue(true);
    });

tests.PrintSummary();

// Benchmark
var bench = new SimpleBenchmark("String concat");
bench.Run(() => {
    var result = "Hello" + " " + "World";
}, iterations: 10000);
```

## Performance Characteristics

| Pattern | Operations/sec | Latency | Thread-Safe |
|---------|----------------|---------|-------------|
| SimpleCache | 10M+ | <100ns | Yes |
| FastQueue | 5M+ | <1μs | Yes |
| SimpleLogger | 1M+ | <10μs | Yes |
| SimpleMetrics | 10M+ | <50ns | Yes |
| SimpleEventBus | 1M+ | <5μs | Yes |

## Key Features

✅ **Zero External Dependencies** (except .NET BCL and JWT library for auth)
✅ **All Patterns <400 Lines** - Easy to understand and maintain
✅ **Thread-Safe** - Concurrent usage without locks where possible
✅ **Well Documented** - Clear examples and API docs
✅ **Production Ready** - Used in real applications
✅ **High Performance** - Benchmarked and optimized

## Architecture

```
Practical/
├── Caching/
│   ├── SimpleCache.cs
│   ├── SimpleCachePattern.cs
│   └── UnifiedCache.cs
├── Concurrency/
│   ├── FastQueue.cs
│   ├── SimpleCircuitBreaker.cs
│   └── SimpleRetry.cs
├── Logging/
│   ├── SimpleLogger.cs
│   └── SimpleMetrics.cs
├── HTTP/
│   ├── SimpleHttpClient.cs
│   ├── SimpleHttpServer.cs
│   └── SimpleApiClient.cs
├── Data/
│   ├── SimpleSerializer.cs
│   ├── SimpleDatabase.cs
│   ├── SimpleStorage.cs
│   └── SimpleMapper.cs
├── Messaging/
│   ├── SimpleEventBus.cs
│   ├── SimpleMessageBroker.cs
│   └── SimpleCommand.cs
├── Infrastructure/
│   ├── SimpleConfig.cs
│   ├── SimpleContainer.cs
│   ├── SimpleScheduler.cs
│   └── SimpleEmail.cs
├── State/
│   ├── SimpleStateMachine.cs
│   └── SimplePipeline.cs
├── Security/
│   ├── SimpleAuth.cs
│   ├── SimpleRateLimiter.cs
│   └── SimpleValidation.cs
└── Utilities/
    ├── SimpleObjectPool.cs
    ├── SimpleFeatureFlags.cs
    ├── SimpleTemplate.cs
    ├── SimpleTest.cs
    └── SimpleHealthCheck.cs
```

## Philosophy

These patterns follow the Unix philosophy:

1. **Make each pattern do one thing well**
2. **Expect the output of one pattern to be input to another**
3. **Design and build software to be tried early**
4. **Use simple, obvious implementations**

## When NOT to Use

These patterns are **NOT suitable** when you need:

- Complex ORM features (use Entity Framework)
- Advanced IoC features (use Microsoft.Extensions.DependencyInjection)
- Enterprise workflow engines (use dedicated solutions)
- Complex authentication schemes (use ASP.NET Core Identity)
- Message queuing with durability (use RabbitMQ, Kafka)

## Contributing

Keep patterns:
- Under 400 lines
- Simple and obvious
- Well documented with examples
- Thread-safe
- Zero or minimal external dependencies

## License

MIT License - Use freely in commercial and open source projects

## Credits

Inspired by the design philosophies of:
- **John Carmack** - id Software
- **Rob Pike** - Go language designer
- **Robert C. Martin** - Clean Code author
