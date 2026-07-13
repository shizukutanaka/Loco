> ⚠️ **NOT IMPLEMENTED — ASPIRATIONAL DESIGN DOC.** The features described
> below (distributed consensus, service mesh, quantum-ready / zero-knowledge
> crypto, cloud-native platform engineering, etc.) are **not present in this
> codebase**. Classes and subsystems referenced here do not exist in `src/`.
> This document is retained for historical/design-discussion purposes only and
> must not be read as a description of shipped functionality. See the root
> `README.md` (“Project status”) for what actually works.

# Phase 11: Platform Engineering & Modern Operations Guide

## Overview

Phase 11 implements critical platform engineering patterns, observability at scale, deployment strategies, and developer experience frameworks. These patterns bridge the gap between infrastructure and developer productivity, enabling organizations to scale engineering teams effectively.

---

## 1. Service Mesh Patterns (Istio & Ambient Mesh)

### 1.1 Service Mesh Architecture

**Problem**: Managing service-to-service communication, security, and observability is complex.

**Solution**: Service mesh provides infrastructure layer for inter-service communication without modifying application code.

#### Traditional Sidecar Mesh

```
Pod 1                  Pod 2
┌──────────┐          ┌──────────┐
│ App      │          │ App      │
├──────────┤          ├──────────┤
│ Sidecar  │──────────│ Sidecar  │
│ Proxy    │  mTLS    │ Proxy    │
└──────────┘          └──────────┘
```

#### Ambient Mesh (2025 Innovation)

```
Pod 1        Pod 2
┌────────┐  ┌────────┐
│ App    │  │ App    │
└────────┘  └────────┘
     │          │
     └─────┬────┘
         Waypoint Proxy
      (Single per namespace)
```

**Benefits of Ambient Mesh**:
- No sidecar injection (simplified operations)
- Reduced resource consumption
- Better performance
- Easier troubleshooting

### 1.2 Virtual Services & Traffic Management

```csharp
var virtualService = new VirtualService
{
    Name = "user-service",
    Hosts = new() { "user-service.default.svc.cluster.local" },
    Http = new()
    {
        new HttpRoute
        {
            Match = new()
            {
                new HttpRouteMatch
                {
                    Uri = new StringMatch { Prefix = "/v2/" }
                }
            },
            Route = new()
            {
                new HttpRouteDestination
                {
                    Destination = new() { Host = "user-service", Subset = "v2" },
                    Weight = 100
                }
            }
        }
    }
};
```

#### Canary Deployment with Virtual Service

```csharp
// Route 10% to canary, 90% to stable
var http = new HttpRoute
{
    Route = new()
    {
        new HttpRouteDestination
        {
            Destination = new() { Host = "service", Subset = "stable" },
            Weight = 90
        },
        new HttpRouteDestination
        {
            Destination = new() { Host = "service", Subset = "canary" },
            Weight = 10
        }
    }
};
```

### 1.3 mTLS & Security

#### Peer Authentication (Automatic mTLS)

```csharp
var peerAuth = new PeerAuthentication
{
    Name = "default",
    Namespace = "production",
    Mtls = MtlsMode.Strict  // Enforce mTLS
};

// All pods in namespace require mTLS
// Istio automatically manages certificates
```

**mTLS Certificate Flow**:
1. Pod wants to communicate with another pod
2. Sidecar intercepts connection
3. Performs TLS handshake with destination sidecar
4. Validates certificate using Istio's CA
5. Encrypts traffic between pods

### 1.4 Destination Rules & Load Balancing

```csharp
var destinationRule = new DestinationRule
{
    Name = "user-service",
    Host = "user-service",
    TrafficPolicy = new TrafficPolicy
    {
        LoadBalancer = new LoadBalancer
        {
            Simple = "LEAST_REQUEST"  // Route to least loaded instance
        },
        ConnectionPool = new ConnectionPool
        {
            MaxConnections = 100,
            Http = new HttpConnectionPool
            {
                Http1MaxPendingRequests = 100,
                Http2MaxRequests = 1000
            }
        },
        OutlierDetection = new OutlierDetection
        {
            ConsecutiveErrors = 5,
            BaseEjectionTime = TimeSpan.FromSeconds(30)
        }
    },
    Subsets = new()
    {
        new Subset { Name = "v1", Labels = new() { ["version"] = "v1" } },
        new Subset { Name = "v2", Labels = new() { ["version"] = "v2" } }
    }
};
```

**Load Balancing Algorithms**:
- **ROUND_ROBIN**: Sequential distribution
- **LEAST_REQUEST**: Route to least loaded
- **RANDOM**: Random selection
- **PASSTHROUGH**: No balancing (passthrough mode)

---

## 2. Observability Stack (OpenTelemetry, Prometheus, Loki, Tempo)

### 2.1 Three Pillars of Observability

```
       Observability
          /  |  \
    Metrics Logs Traces
      /       |     \
Prometheus  Loki   Tempo
      \      |      /
      Grafana Dashboard
```

### 2.2 Metrics Collection (Prometheus)

**Counter**: Monotonically increasing value
```csharp
metricsCollector.RecordCounter("http_requests_total", 1, new()
{
    ["method"] = "GET",
    ["path"] = "/api/users",
    ["status"] = "200"
});
```

**Gauge**: Can increase or decrease
```csharp
metricsCollector.RecordGauge("memory_usage_bytes", 256_000_000, new()
{
    ["pod"] = "user-service-123"
});
```

**Histogram**: Distribution of values
```csharp
metricsCollector.RecordHistogram("http_request_duration_ms", 145, new()
{
    ["endpoint"] = "/api/users"
});
```

### 2.3 Log Aggregation (Loki)

Loki uses labels instead of full-text indexing (more efficient).

```csharp
var logEntry = new LogEntry
{
    Timestamp = DateTime.UtcNow,
    Level = "INFO",
    Message = "User login successful",
    Labels = new()
    {
        ["job"] = "auth-service",
        ["instance"] = "pod-123",
        ["pod"] = "auth-service-456",
        ["namespace"] = "production"
    },
    Fields = new()
    {
        ["user_id"] = "user-789",
        ["ip_address"] = "192.168.1.100"
    },
    TraceId = traceId  // Link to trace
};
```

**Log Query (LogQL)**:
```
{job="auth-service", namespace="production"} |= "error" | json
```

### 2.4 Distributed Tracing (Tempo)

Tempo stores traces for long-term analysis.

```csharp
var trace = new OpenTelemetryTrace
{
    TraceId = "abc123def456",
    Spans = new()
    {
        new Span
        {
            SpanId = "span1",
            TraceId = "abc123def456",
            OperationName = "auth-login",
            ServiceName = "auth-service",
            DurationMs = 145,
            Tags = new()
            {
                ["user.id"] = "user-789",
                ["http.status_code"] = 200
            }
        },
        new Span
        {
            SpanId = "span2",
            TraceId = "abc123def456",
            ParentSpanId = "span1",
            OperationName = "db-query",
            ServiceName = "user-service",
            DurationMs = 120,
            Tags = new()
            {
                ["db.statement"] = "SELECT * FROM users WHERE id = ?",
                ["db.rows_affected"] = 1
            }
        }
    }
};
```

### 2.5 Correlation Across Three Pillars

```csharp
// User reports slow performance at 2:30 PM
var correlatedTelemetry = observabilityCorrelation.GetCorrelatedTelemetry(traceId);

// Returns:
// - Trace: Shows request path through services
// - Logs: All log entries for this trace
// - Metrics: CPU, memory during this time period
```

---

## 3. Advanced Autoscaling Patterns

### 3.1 Horizontal Pod Autoscaler (HPA)

**CPU-based scaling**:
```csharp
var hpa = new HorizontalPodAutoscaler
{
    Name = "user-service-hpa",
    MinReplicas = 2,
    MaxReplicas = 20,
    TargetCpuUtilizationPercent = 70,
    TargetMemoryUtilizationPercent = 80,
    ScaleDownBehavior = new ScalingBehavior
    {
        StabilizationWindow = TimeSpan.FromMinutes(5),
        Policies = new()
        {
            new ScalingPolicy
            {
                Type = "Percent",
                Value = 50,  // Max scale down 50%
                PeriodSeconds = 60
            }
        }
    }
};
```

**Scaling Decision Formula**:
```
desiredReplicas = ceil(currentMetric / targetMetric * currentReplicas)
```

Example:
- Current CPU: 85%
- Target CPU: 70%
- Current replicas: 4
- Desired: ceil(85/70 * 4) = ceil(4.86) = 5 replicas

### 3.2 Vertical Pod Autoscaler (VPA)

Adjusts CPU/memory requests based on historical usage.

```csharp
var vpa = new VerticalPodAutoscaler
{
    Name = "user-service-vpa",
    UpdatePolicy = new UpdatePolicy
    {
        UpdateMode = "Auto"  // Automatically restart pods with new resources
    },
    MinAllowed = new ResourceRequirements
    {
        Cpu = "100m",
        Memory = "128Mi"
    },
    MaxAllowed = new ResourceRequirements
    {
        Cpu = "4",
        Memory = "4Gi"
    }
};
```

### 3.3 KEDA (Event-driven Autoscaler)

```csharp
var kedaTrigger = new KedaTrigger
{
    Type = "kafka",
    Metadata = new()
    {
        ["bootstrapServers"] = "kafka:9092",
        ["consumerGroup"] = "user-service-group",
        ["topic"] = "user-events",
        ["lagThreshold"] = "100"  // Scale when lag > 100
    }
};

// Scales based on Kafka consumer lag
// 1 replica per 100 messages in queue
```

### 3.4 Predictive Autoscaling

```csharp
// Record metrics over time
predictor.RecordMetric(cpuUsage: 65.0, currentReplicas: 3);
predictor.RecordMetric(cpuUsage: 72.0, currentReplicas: 3);
predictor.RecordMetric(cpuUsage: 85.0, currentReplicas: 3);

// Predict trend: CPU increasing ~10% per minute
// In 5 minutes: 85 + (10 * 5) = 135%
// Required replicas: ceil(135/70 * 3) = 6 replicas

var required = predictor.PredictRequiredReplicas(
    targetCpuUtilization: 70,
    minReplicas: 2,
    maxReplicas: 20
); // Returns 6
```

---

## 4. Progressive Deployment Strategies

### 4.1 Canary Deployment

**Strategy**: Gradually shift traffic to new version.

```csharp
var canary = new CanaryDeployment
{
    Name = "user-service-canary",
    StableVersion = new() { Version = "1.0.0" },
    CanaryVersion = new() { Version = "1.1.0" },
    Traffic = new TrafficShift
    {
        Weight = 0,           // Start at 0%
        StepWeight = 10,      // Increase 10% per step
        StepDuration = TimeSpan.FromMinutes(5),
        MaxWeight = 50        // Pause at 50% for manual approval
    },
    Analysis = new CanaryAnalysis
    {
        Threshold = 95,       // 95% success rate required
        Interval = TimeSpan.FromSeconds(30),
        Metrics = new()
        {
            new CanaryMetric
            {
                Name = "error_rate",
                Query = "rate(errors_total[1m])",
                SuccessCriteria = "< 0.01",  // Less than 1% errors
                ThresholdRange = new() { Min = 0, Max = 0.01 }
            }
        }
    }
};

// Timeline:
// T+0m:  10% -> canary
// T+5m:  20% -> canary (if metrics good)
// T+10m: 30% -> canary
// T+15m: 40% -> canary
// T+20m: 50% -> canary (PAUSE - wait for approval)
// Manual approval...
// T+25m: 60% -> canary
// ... up to 100%
```

### 4.2 Blue-Green Deployment

**Strategy**: Switch all traffic instantly with fast rollback.

```csharp
var blueGreen = new BlueGreenDeployment
{
    Name = "user-service-bg",
    BlueVersion = new() { Version = "1.0.0" },  // Current production
    GreenVersion = new() { Version = "1.1.0" }, // New version
    ActiveSlot = "Blue",
    VerificationWaitTime = TimeSpan.FromMinutes(5)
};

// Timeline:
// T+0m:  Deploy green version (user-service-1.1.0)
// T+0m:  Run smoke tests
// T+5m:  If tests pass, switch routing to green
//        (Green is now active)
// T+10m: If no issues, decommission blue
//        (Rollback possible any time before this)
```

### 4.3 Flagger Integration

Flagger automates canary and blue-green deployments with service mesh.

```yaml
apiVersion: flagger.app/v1beta1
kind: Canary
metadata:
  name: user-service
spec:
  targetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: user-service
  progressDeadlineSeconds: 300
  service:
    port: 80
  analysis:
    interval: 1m
    threshold: 5
    maxWeight: 50
    stepWeight: 10
    metrics:
    - name: request-success-rate
      thresholdRange:
        min: 99
      interval: 1m
    - name: request-duration
      thresholdRange:
        max: 500
      interval: 1m
    webhooks:
    - name: smoke-tests
      url: http://flagger-loadtester/
      metadata:
        type: smoke
        cmd: "curl -s http://user-service:80/health"
```

---

## 5. Chaos Engineering Patterns

### 5.1 Chaos Experiment Types

#### Pod Fault - Terminate pod
```csharp
var experiment = new ChaosExperiment
{
    Name = "kill-random-pod",
    Target = new ChaosTarget
    {
        Namespace = "production",
        PodSelector = new() { ["app"] = "user-service" },
        Mode = "One"  // Kill one random pod
    },
    Fault = new PodFault { Action = "Kill" },
    Duration = TimeSpan.FromMinutes(5)
};

// Result: Service continues without disruption
// Tests: Load balancing, auto-restart, failover
```

#### Network Fault - Add latency
```csharp
var experiment = new ChaosExperiment
{
    Name = "add-latency",
    Fault = new NetworkFault()
};
((NetworkFault)experiment.Fault).SetLatency(
    delayMs: 200,
    jitterMs: 50
);

// Result: 200ms ± 50ms latency to all requests
// Tests: Timeout handling, circuit breakers
```

#### Resource Fault - Memory stress
```csharp
var experiment = new ChaosExperiment
{
    Name = "memory-stress",
    Fault = new ResourceFault()
};
((ResourceFault)experiment.Fault).SetMemoryStress("512MB");

// Result: Pod uses 512MB extra memory
// Tests: OOM handling, memory limits
```

### 5.2 Experiment Analysis

```csharp
var results = await chaosEngine.RunExperimentAsync(experimentId);

// Results show:
// - Did service remain available? (Recovery time)
// - How many requests failed? (Failure rate)
// - Did system auto-heal? (Self-healing capability)
// - What was performance impact? (Degradation)

var analysis = new ChaosResults
{
    Outcome = "Success",  // System recovered
    PodsAffected = 2,
    FailureRate = 0.02,   // 2% requests failed
    RecoveryTime = TimeSpan.FromSeconds(15),
    Observations = new()
    {
        "Circuit breaker activated correctly",
        "Load balanced to remaining pods",
        "Auto-restart brought pod back online"
    }
};
```

### 5.3 Recurring Chaos Experiments

```csharp
var schedule = new ChaosSchedule
{
    Name = "weekly-chaos-testing",
    Experiment = experiment,
    Schedule = "0 2 * * 0",  // Every Sunday at 2 AM
    Enabled = true
};

await chaosEngine.ScheduleExperimentAsync(schedule);

// Weekly validation of system resilience
// Prevents "surprise" failures in production
```

---

## 6. Domain-Driven Design (DDD) Patterns

### 6.1 Bounded Context Structure

```
Customer Bounded Context          Order Bounded Context
┌─────────────────────────┐       ┌──────────────────┐
│ Aggregate Root:         │       │ Aggregate Root:  │
│ Customer                │       │ Order            │
│ ├─ id                   │       │ ├─ id            │
│ ├─ email (ValueObject)  │       │ ├─ items         │
│ ├─ address (ValueObject)│       │ ├─ totalAmount   │
│ │                       │       │ ├─ status        │
│ └─ Repository           │       │ │                │
│    (IRepository<Customer>)       │ └─ Repository    │
│                         │       │    (IRepository<Order>)
│ Integration Event:      │       │                  │
│ CustomerCreatedEvent    │       │ Integration Event:
│                         │       │ OrderCreatedEvent
└─────────────────────────┘       └──────────────────┘
         │ subscribes                     │ publishes
         └─────────────────────────────────┘
              (Domain Events)
```

### 6.2 Aggregate Design

```csharp
// Order is the Aggregate Root
public class Order : AggregateRoot
{
    // Collection of OrderItems (part of aggregate)
    public List<OrderItem> Items { get; private set; } = new();

    // Business rule: Only confirm pending orders
    public void Confirm()
    {
        if (Status != "Pending")
            throw new InvalidOperationException();

        Status = "Confirmed";
        AddDomainEvent(new OrderConfirmedEvent { OrderId = Id });
    }

    // Domain event published on save
    // Other contexts subscribe (Payment, Inventory, Shipping)
}

// OrderItem is a Value Object (no separate identity)
public class OrderItem : ValueObject
{
    public string ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return ProductId;
        yield return Quantity;
        yield return UnitPrice;
    }
}
```

### 6.3 Domain Events for Integration

```csharp
// In Order Aggregate
public static Order Create(string customerId, List<OrderItem> items)
{
    var order = new Order { ... };

    // Publish domain event
    order.AddDomainEvent(new OrderCreatedEvent
    {
        OrderId = order.Id,
        CustomerId = customerId,
        TotalAmount = order.TotalAmount,
        Items = items
    });

    return order;
}

// In Infrastructure layer
await orderRepository.SaveAsync(order);

// Publish events to message bus
foreach (var @event in order.GetDomainEvents())
{
    await messageBus.PublishAsync(@event);
}

// Other bounded contexts listen
// - Payment context: Create payment for order
// - Inventory context: Reserve inventory
// - Shipping context: Prepare shipment
```

---

## 7. API Gateway Patterns (Kong, Tyk)

### 7.1 API Routes & Routing

```csharp
var route = new ApiRoute
{
    Name = "user-service",
    Paths = new() { "/api/users", "/api/users/*" },
    Methods = new() { "GET", "POST", "PUT", "DELETE" },
    Service = new ServiceEndpoint
    {
        Name = "user-service",
        Host = "user-service.default.svc.cluster.local",
        Port = 8080,
        Timeout = 30000
    },
    Plugins = new() { "rate-limiting", "authentication", "logging" },
    StripPath = true  // Remove /api prefix before forwarding
};

// Request: POST /api/users → user-service:8080/users
```

### 7.2 Rate Limiting Algorithms

**Token Bucket Algorithm**:
```
Requests:  ▯ ▯ ▯ ▯ ▯ (5 tokens available)
Rate:      +1 token per second
Burst:     5 tokens max

t=0s: Request uses 1 token → 4 remaining
t=0.5s: Request uses 1 token → 3 remaining
t=1s: +1 token added → 4 remaining
t=2s: +1 token added → 5 remaining (max)
```

```csharp
var rateLimit = new RateLimitPolicy
{
    Name = "user-api-limit",
    Algorithm = "Token Bucket",
    Requests = 100,        // 100 requests per window
    WindowSeconds = 60,    // per 60 seconds
    Scope = "Consumer"     // per API consumer
};
```

**Sliding Window Algorithm**:
```
Window: [T-60s ... T]

t=0-30s:  95 requests
t=30-60s: 5 requests
Total:    100 requests (at limit)

t=61s: Oldest request (t=1s) drops
       99 requests now (under limit)
```

### 7.3 Authentication & Consumers

```csharp
var consumer = new ApiConsumer
{
    Username = "mobile-app",
    ApiKeys = new() { "sk_live_abc123..." },
    Oauth2Clients = new() { "client_456..." },
    RateLimit = new RateLimitPolicy
    {
        Requests = 50,      // Lower limit for free tier
        WindowSeconds = 60
    },
    Metadata = new()
    {
        ["tier"] = "free",
        ["features"] = new[] { "read-users", "read-orders" }
    }
};

// Plugin processes request
// 1. Extract API key from header: X-API-Key
// 2. Look up consumer
// 3. Apply consumer's rate limit
// 4. Check scopes/permissions
// 5. Add consumer info to request context
```

---

## 8. Distributed Caching Strategies

### 8.1 Cache Eviction Policies

**LRU (Least Recently Used)**:
```csharp
// Track last access time
var cache = new DistributedCache(new CacheConfig
{
    MaxEntries = 1000,
    EvictionPolicy = "LRU"
});

// Recent accesses:
// Key A: accessed 1s ago  ← Keep
// Key B: accessed 5s ago
// Key C: accessed 30s ago ← Evict first
// Key D: accessed 60s ago ← Evict second
```

**LFU (Least Frequently Used)**:
```csharp
// Track access count
cache.EvictionPolicy = "LFU";

// Access counts:
// Key A: 100 accesses  ← Keep
// Key B: 50 accesses
// Key C: 10 accesses   ← Evict first
// Key D: 5 accesses    ← Evict second
```

### 8.2 Consistent Hashing

For distributed cache across multiple nodes:

```csharp
var hashRing = new ConsistentHashRing(
    virtualNodes: 150,  // 150 virtual nodes per physical node
    logger: logger
);

// Add cache nodes
hashRing.AddNode("cache-node-1");
hashRing.AddNode("cache-node-2");
hashRing.AddNode("cache-node-3");

// Key distribution (minimal redistribution on node changes)
var node = hashRing.GetNode("user:123");  // → cache-node-2
var node = hashRing.GetNode("order:456"); // → cache-node-1
var node = hashRing.GetNode("product:789"); // → cache-node-3

// Add new node
hashRing.AddNode("cache-node-4");
// Only ~1/150 keys rehashed (not all)
```

### 8.3 Cache Warming

Pre-load cache with frequently accessed data:

```csharp
var warmer = new CacheWarmer(cache, logger);

var initialData = new Dictionary<string, UserDto>
{
    ["user:1"] = user1,
    ["user:2"] = user2,
    ["user:3"] = user3,
    // ... 1000s of users
};

await warmer.WarmCacheAsync(initialData, ttl: TimeSpan.FromHours(1));

// Now all subsequent requests hit cache (no DB queries)
```

---

## 9. Platform Engineering & Developer Experience

### 9.1 Internal Developer Platform (IDP)

**Problem**: Developers need standardized, self-service tools to provision services.

**Solution**: IDP provides templates, components, and automation.

```csharp
var platform = new InternalDeveloperPlatform(logger);

// Register reusable component
var loggingComponent = new PlatformComponent
{
    Name = "Structured Logging",
    Type = "Logging",
    Version = "1.0.0",
    Documentation = "...",
    Examples = new()
    {
        new CodeExample
        {
            Title = "Basic Logging",
            Language = "csharp",
            Code = @"
var logger = ServiceProvider.GetService<ILogger>();
logger.LogInformation(""User {UserId} logged in"", userId);
            "
        }
    }
};

await platform.RegisterComponentAsync(loggingComponent);
```

### 9.2 Developer Templates

Self-service service creation:

```csharp
// Register template
var template = new DeveloperTemplate
{
    Name = ".NET Microservice",
    Language = "csharp",
    Framework = "ASP.NET Core 8",
    Description = "Standard template for microservices",
    Files = new()
    {
        ["Program.cs"] = "// Program setup...",
        ["Dockerfile"] = "FROM mcr.microsoft.com/dotnet/sdk:8.0...",
        ["docker-compose.yml"] = "version: '3'...",
        [".github/workflows/ci.yml"] = "name: CI..."
    },
    Variables = new()
    {
        ["SERVICE_NAME"] = "my-service",
        ["NAMESPACE"] = "production",
        ["PORT"] = "8080"
    }
};

await platform.RegisterTemplateAsync(template);

// Developer creates service
var service = await platform.CreateServiceFromTemplateAsync(
    serviceName: "user-service",
    templateId: template.Id,
    variables: new()
    {
        ["SERVICE_NAME"] = "user-service",
        ["PORT"] = "8080"
    }
);

// IDP automatically:
// - Creates Git repository
// - Sets up CI/CD pipeline
// - Provisions Kubernetes resources
// - Creates monitoring dashboards
```

### 9.3 Developer Experience Metrics

Track platform adoption and effectiveness:

```csharp
var metrics = new DeveloperExperienceMetrics
{
    TimeToFirstDeployment = TimeSpan.FromHours(2),  // From template to prod
    AverageDeploymentTime = TimeSpan.FromMinutes(8),
    RollbackFrequency = 0.05,  // 5 per 100 deployments (very low = good)
    PlatformAdoption = 0.92,   // 92% of teams use platform
    DocumentationQuality = 0.88,  // 88/100 quality rating
    SupportTickets = 12  // Per week
};
```

---

## Integration Example: Complete Workflow

### Deploy New Service with Progressive Rollout

```
1. Developer uses IDP
   └─ Selects ".NET Microservice" template
   └─ Fills in service name, namespace
   └─ IDP creates repository, CI/CD, Kubernetes manifests

2. Code merged to main
   └─ GitHub Actions CI/CD runs
   └─ Tests pass
   └─ Docker image built and pushed

3. Deployment initiated
   └─ Flagger creates Canary Deployment
   └─ Starts with 0% traffic to new version
   └─ Every 5 minutes: increase traffic 10%

4. Observability runs
   └─ Prometheus collects metrics
   └─ Loki aggregates logs
   └─ Tempo stores traces

5. Canary analysis
   └─ Every 30s: check error rate < 1%
   └─ Check latency p99 < 500ms
   └─ Check CPU < 70%

6. Progressive rollout
   ├─ If metrics good at 10%  → proceed to 20%
   ├─ If metrics good at 50%  → wait for manual approval
   ├─ After approval: 60%, 70%, ..., 100%
   └─ Complete: all traffic on new version

7. Service mesh
   └─ mTLS encrypts all inter-service traffic
   └─ Circuit breaker prevents cascading failures
   └─ Rate limiting protects downstream services

8. Auto-scaling
   └─ HPA monitors CPU/memory
   └─ KEDA monitors Kafka lag
   └─ Predictive scaler anticipates load
   └─ Scales from 2 to 10 replicas as needed

9. Monitoring continues
   └─ Chaos experiments run weekly
   └─ Verifies resilience to failures
   └─ Results feed back to platform team
```

---

## Best Practices

1. **Service Mesh**:
   - Start with Ambient Mesh in 2025 (simpler)
   - Enforce mTLS in strict mode
   - Use Istio for complex traffic management

2. **Observability**:
   - Correlation IDs link logs/traces/metrics
   - 95th/99th percentiles matter more than average
   - Alert on business metrics, not just infra

3. **Deployment**:
   - Canary for most changes (safer)
   - Blue-green for database migrations
   - Always have automated rollback

4. **Chaos**:
   - Start with weekly experiments
   - Measure resilience
   - Fix broken circuits, not patch symptoms

5. **DDD**:
   - One aggregate per transaction
   - Use domain events for inter-context communication
   - Repositories only for aggregate roots

6. **API Gateway**:
   - Rate limit by consumer tier
   - Validate API keys early
   - Log all requests

7. **Caching**:
   - LRU for general workloads
   - Warm cache at startup
   - Monitor hit rate > 80%

8. **Platform Engineering**:
   - Template every service pattern
   - Document common tasks
   - Measure time-to-deployment

---

## References

- Istio: https://istio.io/
- OpenTelemetry: https://opentelemetry.io/
- Prometheus: https://prometheus.io/
- Grafana Loki: https://grafana.com/loki/
- Flagger: https://flagger.app/
- Chaos Mesh: https://chaos-mesh.org/
- Kong Gateway: https://konghq.com/
- Domain-Driven Design: https://www.domainlanguage.com/

