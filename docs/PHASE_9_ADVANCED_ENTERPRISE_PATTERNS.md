# Phase 9: Advanced Enterprise Patterns & Cloud-Native Architecture

> **Production-Grade Distributed Systems, Cost Optimization, and Disaster Recovery**
>
> This document covers critical Phase 9 patterns for enterprise-scale cloud-native applications.

## Table of Contents

1. [Service Mesh & Cloud-Native Patterns](#service-mesh--cloud-native-patterns)
2. [Advanced Testing Strategies](#advanced-testing-strategies)
3. [Observability & Monitoring](#observability--monitoring)
4. [Database Scaling at Scale](#database-scaling-at-scale)
5. [Advanced Security (OAuth2/OIDC)](#advanced-security-oauth2oidc)
6. [Infrastructure as Code](#infrastructure-as-code)
7. [Cost Optimization](#cost-optimization)
8. [Disaster Recovery & Business Continuity](#disaster-recovery--business-continuity)
9. [Advanced Rate Limiting & Quotas](#advanced-rate-limiting--quotas)

---

## Service Mesh & Cloud-Native Patterns

### What is a Service Mesh?

A service mesh is an invisible layer of infrastructure between services that handles cross-cutting concerns:

```
Traditional Microservices:
┌──────────────┐         ┌──────────────┐         ┌──────────────┐
│  Service A   │────────→│  Service B   │────────→│  Service C   │
│              │         │              │         │              │
│ (handles own │         │ (handles own │         │ (handles own │
│  retry logic │         │  security)   │         │  logging)    │
│  security)   │         │              │         │              │
└──────────────┘         └──────────────┘         └──────────────┘

Service Mesh (Istio/Linkerd):
┌──────────────┐         ┌──────────────┐         ┌──────────────┐
│  Service A   │         │  Service B   │         │  Service C   │
│              │         │              │         │              │
└────────┬─────┘         └────────┬─────┘         └────────┬─────┘
         │                        │                        │
      ┌──▼──┐               ┌──▼──┐                  ┌──▼──┐
      │Proxy│               │Proxy│                  │Proxy│
      │     │───────────────│     │──────────────────│     │
      │     │ (managed by   │     │  (managed by     │     │
      │     │  Mesh)        │     │   Mesh)          │     │
      └──▼──┘               └──▼──┘                  └──▼──┘
         ↓                      ↓                        ↓
    [Control Plane - Centralized Management]
    ├─ Service Discovery
    ├─ Load Balancing
    ├─ Security (mTLS)
    ├─ Resilience (Retry, Timeout)
    ├─ Observability (Tracing, Metrics)
    └─ Traffic Management
```

### Istio in 2025

**Key Features**:
- **Ambient Mesh**: Sidecar-less architecture (reduced overhead)
- **Service Discovery**: Automatic service registration
- **Load Balancing**: Intelligent routing and load distribution
- **Security**: Automatic mTLS between services
- **Observability**: Metrics, traces, logs collection
- **Traffic Management**: Canary deployments, A/B testing
- **Resilience**: Automatic retries, circuit breakers, timeouts

### Dapr (Distributed Application Runtime)

**When to choose Dapr**:
- Need building blocks (state, pub/sub, service invocation)
- Want language-agnostic distributed patterns
- Prefer simpler setup than service mesh

**When to choose Istio**:
- Need advanced traffic management
- Want sophisticated observability
- Need strong security (mTLS) guarantees
- Running on Kubernetes at scale

### Implementation

```csharp
// Dapr state management
[HttpPost("state")]
public async Task<IActionResult> SaveStateAsync(
    [FromBody] StateData data)
{
    var httpClient = new HttpClient();

    // Call Dapr sidecar
    await httpClient.PostAsJsonAsync(
        "http://localhost:3500/v1.0/state/statestore",
        new[] {
            new { key = data.Id, value = data, ttlInSeconds = 3600 }
        }
    );

    return Ok();
}

// Dapr pub/sub messaging
[HttpPost("publish")]
public async Task<IActionResult> PublishEventAsync(
    [FromBody] WorkflowEvent @event)
{
    var httpClient = new HttpClient();

    // Dapr handles message routing
    await httpClient.PostAsJsonAsync(
        "http://localhost:3500/v1.0/publish/workflows",
        @event
    );

    return Ok();
}
```

---

## Advanced Testing Strategies

### Consumer-Driven Contract Testing (CDC)

**Problem**: Microservices must maintain compatibility

```
Service A (Consumer)                Service B (Provider)
expects:                           provides:
{                                  {
  "id": "123",                       "id": "123",
  "name": "John"                     "name": "John",
  "email": "john@ex.com"  (breaks)   "email": "john@ex.com",
                                     "phone": "555-1234"
}                                  }
```

**Solution**: Contract tests ensure compatibility

```csharp
// Consumer-side test
[TestFixture]
public class WorkflowServiceContractTests
{
    private PactBuilder _pactBuilder;

    [SetUp]
    public void Setup()
    {
        _pactBuilder = new PactBuilder();
    }

    [Test]
    public async Task GetWorkflow_WithValidId_ReturnsWorkflow()
    {
        // Arrange
        var expectedWorkflow = new
        {
            id = "123",
            name = "Test Workflow",
            status = "Active"
        };

        _pactBuilder
            .UponReceiving("a request for workflow 123")
            .WithRequest(HttpMethod.Get, "/workflows/123")
            .WillRespondWith(expectedWorkflow, 200);

        // Act & Assert
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync("http://localhost:8080/workflows/123");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var content = JsonSerializer.Deserialize<dynamic>(
            await response.Content.ReadAsStringAsync());
        Assert.That(content?.id, Is.EqualTo("123"));
    }

    [TearDown]
    public void Teardown()
    {
        // Publish contract
        _pactBuilder.Build().WriteToPactFile();
    }
}

// Provider-side test (verifies API respects contracts)
[TestFixture]
public class WorkflowServiceProviderTests
{
    [Test]
    public void VerifyContracts()
    {
        var verifier = new PactVerifier();

        verifier
            .ServiceProvider("WorkflowService", "http://localhost:8080")
            .HonoursPactWith("WorkflowConsumer")
            .Verify();
    }
}
```

**Benefits**:
- ✅ Catch breaking changes early
- ✅ Document API contracts
- ✅ Enable independent service deployment
- ✅ Reduce integration test complexity

---

## Observability & Monitoring

### Three Pillars: Logs, Metrics, Traces

```
┌─────────────────────────────────────────────────────┐
│           Observable Distributed System             │
├─────────────────────────────────────────────────────┤
│                                                     │
│  LOGS          METRICS          TRACES              │
│  └─ What       └─ How much      └─ How long        │
│     happened   └─ How fast      └─ Error path      │
│  └─ When       └─ Errors        └─ Latency        │
│  └─ Where      └─ Capacity      └─ Dependencies   │
│  └─ Why                                            │
│                                                     │
└─────────────────────────────────────────────────────┘
```

### Tool Stack

**Prometheus** (Metrics):
```
Scrapes metrics from apps every 15s
Stores time-series data
Provides query language (PromQL)

Example:
rate(http_request_duration_seconds_bucket[5m])
```

**ELK Stack** (Logs):
```
Elasticsearch: Search & store logs
Logstash: Parse & transform logs
Kibana: Visualize & explore

Example:
GET /logs/_search
{
  "query": { "match": { "level": "ERROR" } }
}
```

**Jaeger** (Traces):
```
Traces requests across services
Shows dependencies
Identifies bottlenecks

Example:
Trace ID: abc123
├─ Service A: 150ms
│  ├─ Query DB: 100ms
│  ├─ Call Service B: 50ms
│  └─ Serialize: 5ms
├─ Service B: 45ms
│  ├─ Process: 40ms
│  └─ Serialize: 5ms
└─ Total: 195ms
```

### OpenTelemetry Integration

```csharp
// Setup OpenTelemetry
var tracerProvider = new TracerProviderBuilder()
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddSqlClientInstrumentation()
    .AddJaegerExporter(options =>
    {
        options.AgentHost = "localhost";
        options.AgentPort = 6831;
    })
    .Build();

// Automatic instrumentation
var tracer = tracerProvider.GetTracer("MyApp");

using (var span = tracer.StartActiveSpan("ProcessWorkflow"))
{
    span.SetAttribute("workflow.id", workflowId);

    // Code execution is automatically timed
    await ProcessAsync(workflowId);

    span.SetAttribute("workflow.status", "completed");
}
```

---

## Database Scaling at Scale

### Sharding Strategy

```
User Sharding (by user ID):

User ID 1-1M    User ID 1M-2M    User ID 2M-3M
┌─────────────┐ ┌──────────────┐ ┌──────────────┐
│  Shard 1    │ │   Shard 2    │ │   Shard 3    │
│  DB1        │ │   DB2        │ │   DB3        │
│             │ │              │ │              │
│ 1M users    │ │ 1M users     │ │ 1M users     │
└─────────────┘ └──────────────┘ └──────────────┘

Benefits:
- Each shard: 1/3 the load
- Parallel queries across shards
- Independent scaling per shard

Challenges:
- Cross-shard queries are complex
- Rebalancing shards is expensive
- Hotspot detection needed
```

### CQRS for Read Scaling

```
┌─────────────────────────────────────┐
│        Write Database               │
│   (PostgreSQL - 1 Instance)         │
│   ├─ Orders table                   │
│   ├─ Customers table                │
│   └─ Products table                 │
└──────────┬──────────────────────────┘
           │
    [Event Stream]
           │
  ┌────────┴────────┬──────────────┐
  ▼                 ▼              ▼
Read DB1       Read DB2       Read DB3
(Optimized)   (Optimized)   (Optimized)
- Search index - Analytics   - Reporting
- Fast reads   - Aggregates  - Historical

Result:
- Writes: 1 server (consistent)
- Reads: Scaled horizontally (fast)
```

---

## Advanced Security (OAuth2/OIDC)

### Token Lifecycle

```
┌─────────────────────────────────────────────────┐
│         Token Lifecycle Management              │
└─────────────────────────────────────────────────┘

1. Initial Authentication
   User → OAuth Provider → Access Token (15 min)
                        → Refresh Token (30 days)

2. API Call
   Client: GET /api/data
   Header: Authorization: Bearer {access_token}
   Server: Validates & returns data

3. Token Expiration
   Client: GET /api/more
   Header: Authorization: Bearer {expired_token}
   Server: Returns 401 Unauthorized

4. Token Refresh
   Client: POST /token
   Body: refresh_token={refresh_token}
   Server: Returns NEW access_token

5. Refresh Token Rotation (Security)
   OLD refresh_token → invalidated
   NEW refresh_token → issued with new access_token

Benefits:
- Access tokens: Short-lived (less damage if leaked)
- Refresh tokens: Long-lived (reuse without login)
- Rotation: Detects token theft
```

### Scope-Based Authorization

```csharp
[HttpGet("admin")]
[Authorize(Policy = "AdminPolicy")]
public IActionResult AdminEndpoint()
{
    return Ok("Admin access granted");
}

// Startup configuration
services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy =>
        policy.RequireClaim("scope", "admin:read", "admin:write"));
});

// Token claims
{
  "sub": "user-123",
  "scope": "admin:read admin:write user:read",
  "aud": "api-audience",
  "iss": "https://auth-provider.com",
  "exp": 1234567890
}
```

---

## Infrastructure as Code

### Terraform Best Practices

```hcl
# Remote state for team collaboration
terraform {
  backend "s3" {
    bucket         = "loco-terraform-state"
    key            = "prod/terraform.tfstate"
    region         = "us-east-1"
    encrypt        = true
    dynamodb_table = "terraform-locks"
  }
}

# Variable for reusability
variable "environment" {
  description = "Environment name"
  type        = string
  default     = "prod"
}

# Module for abstraction
module "kubernetes" {
  source = "./modules/eks"

  cluster_name    = "loco-${var.environment}"
  node_group_size = var.environment == "prod" ? 5 : 2
  instance_type   = var.environment == "prod" ? "t3.large" : "t3.medium"
}

# Outputs for reference
output "kubernetes_endpoint" {
  value       = module.kubernetes.cluster_endpoint
  description = "EKS cluster endpoint"
}

# Version locking
terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
  required_version = "~> 1.0"
}
```

---

## Cost Optimization

### Container Image Optimization

```dockerfile
# BEFORE (Large, slow)
FROM node:20
WORKDIR /app
COPY . .
RUN npm install
RUN npm run build
EXPOSE 3000
CMD ["npm", "start"]

# Size: ~1.2 GB
# Build time: 5 minutes

# AFTER (Optimized)
FROM node:20-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm ci --only=production
COPY . .
RUN npm run build

FROM node:20-alpine
RUN apk add --no-cache dumb-init
USER node
WORKDIR /app
COPY --from=builder /app/node_modules ./node_modules
COPY --from=builder /app/dist ./dist
COPY --from=builder /app/package*.json ./
EXPOSE 3000
ENTRYPOINT ["dumb-init", "node", "dist/index.js"]

# Size: ~150 MB (87% reduction)
# Build time: 3 minutes
# Savings: ~$200/month with 100 images
```

### Image Layer Pruning

```
Registry Management Strategy:

Before Pruning:
├─ image:v1 → layers: A, B, C
├─ image:v2 → layers: A, B, D  (A, B shared)
├─ image:v3 → layers: A, B, E  (A, B shared)
├─ old-image:v1 → layers: F, G, H (unused)
└─ Total: 500 GB stored

After Pruning:
├─ image:v3 → layers: A, B, E (kept latest)
├─ Delete old-image completely
├─ Unreferenced layers F, G, H deleted
└─ Total: 150 GB stored (70% reduction!)

Cost Savings:
Before: $500/month
After: $150/month
Savings: $350/month ($4,200/year)
```

---

## Disaster Recovery & Business Continuity

### RTO vs RPO

```
┌────────────────────────────────────────────┐
│        Disaster Recovery Timeline           │
└────────────────────────────────────────────┘

Data Loss Occurs ─────── Last Backup ─── Now
         │                    │          │
         │                    │          │
         │◄──── RPO (max data loss) ────►│
         │
         │ <- RTO (max recovery time) -> │
         │                                │
         └─ Recovery Complete ────────────┘

Examples:

Critical System (Banking):
  RPO: 5 minutes (max 5 min of data loss)
  RTO: 30 minutes (max 30 min downtime)
  Strategy: Real-time replication + failover

Standard System:
  RPO: 1 hour
  RTO: 4 hours
  Strategy: Hourly snapshots + restore

Non-Critical:
  RPO: 1 day
  RTO: 1 day
  Strategy: Daily backups
```

### Backup Strategies

```csharp
// 3-2-1 Backup Rule:
// 3 copies of data
// 2 different media types
// 1 offsite location

public class BackupStrategy
{
    // Copy 1: Production database (live)
    private readonly IDataStore _productionDb;

    // Copy 2: Local backup (daily snapshots)
    private readonly IDataStore _localBackup;

    // Copy 3: Offsite backup (S3/Azure)
    private readonly ICloudStorage _offsiteBackup;

    public async Task BackupAsync()
    {
        // Step 1: Snapshot production
        var snapshot = await _productionDb.CreateSnapshotAsync();

        // Step 2: Store locally (fast restore)
        await _localBackup.RestoreAsync(snapshot);

        // Step 3: Archive offsite (disaster recovery)
        await _offsiteBackup.UploadAsync(snapshot, encryption: true);

        // Verify
        await VerifyBackupIntegrityAsync();
    }
}
```

---

## Advanced Rate Limiting & Quotas

### Customer Tier-Based Quotas

```csharp
public class AdvancedQuotaManager
{
    private readonly Dictionary<string, TierQuota> _tiers = new()
    {
        ["free"] = new TierQuota
        {
            RequestsPerSecond = 10,
            RequestsPerDay = 10_000,
            MaxConcurrent = 5,
            MaxResponseSize = "1 MB"
        },
        ["pro"] = new TierQuota
        {
            RequestsPerSecond = 100,
            RequestsPerDay = 1_000_000,
            MaxConcurrent = 50,
            MaxResponseSize = "100 MB"
        },
        ["enterprise"] = new TierQuota
        {
            RequestsPerSecond = 1_000,
            RequestsPerDay = long.MaxValue,
            MaxConcurrent = 500,
            MaxResponseSize = "1 GB"
        }
    };

    public async Task<QuotaCheckResult> CheckQuotaAsync(
        string customerId,
        string tier)
    {
        var quota = _tiers[tier];
        var usage = await GetCurrentUsageAsync(customerId);

        return new QuotaCheckResult
        {
            Allowed = usage.RequestsThisSecond < quota.RequestsPerSecond &&
                     usage.RequestsThisDay < quota.RequestsPerDay &&
                     usage.ConcurrentRequests < quota.MaxConcurrent,

            RateLimitHeaders = new Dictionary<string, string>
            {
                ["X-RateLimit-Limit"] = quota.RequestsPerSecond.ToString(),
                ["X-RateLimit-Remaining"] =
                    (quota.RequestsPerSecond - usage.RequestsThisSecond).ToString(),
                ["X-RateLimit-Reset"] =
                    (DateTime.UtcNow.AddSeconds(1).ToUnixTimeSeconds()).ToString()
            }
        };
    }
}
```

### Adaptive Rate Limiting

```csharp
public class AdaptiveRateLimiter
{
    public async Task<bool> IsAllowedAsync(
        string clientId,
        HttpContext context)
    {
        // Check static quotas
        if (!await _quotaManager.CheckAsync(clientId))
            return false;

        // Adaptive: Monitor error rates
        var errorRate = await _metrics.GetErrorRateAsync(clientId);
        if (errorRate > 0.5) // 50% error rate
        {
            // Temporarily reduce limits
            await _limiter.ReduceLimitAsync(clientId, percentage: 0.5);

            _logger.LogWarning(
                "Adaptive rate limiting triggered for {Client}: {ErrorRate}% errors",
                clientId,
                errorRate * 100);
        }

        // Check health of dependencies
        var serviceHealth = await _healthChecker.GetHealthAsync();
        if (serviceHealth.Status == HealthStatus.Degraded)
        {
            // Further reduce limits during degradation
            await _limiter.ReduceLimitAsync(clientId, percentage: 0.25);
        }

        return true;
    }
}
```

---

## Integration & Production Deployment

### Complete Observability Setup

```csharp
// Startup configuration
services
    .AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation()
        .AddJaegerExporter())
    .WithMetrics(builder => builder
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter())
    .WithLogging(builder => builder
        .AddConsoleExporter());

// Middleware
app.UseOpenTelemetryPrometheusScrapingEndpoint();
```

### Complete Security Stack

```csharp
services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://auth-provider.com";
        options.Audience = "loco-api";
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            RequireExpirationTime = true
        };

        // Token refresh handling
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = async context =>
            {
                if (context.Exception is SecurityTokenExpiredException)
                {
                    // Attempt refresh
                    var newToken = await _tokenManager.RefreshAsync(
                        context.Request.Headers["X-Refresh-Token"]);

                    if (newToken != null)
                    {
                        context.Principal = new ClaimsPrincipal(
                            new ClaimsIdentity(newToken.Claims));
                        context.Success();
                    }
                }
            }
        };
    });
```

---

## Summary

Phase 9 introduces production-grade patterns for:

- ✅ Service mesh orchestration (Istio/Dapr)
- ✅ Distributed systems testing (CDC)
- ✅ Comprehensive observability (Logs, Metrics, Traces)
- ✅ Database scaling strategies (Sharding, CQRS)
- ✅ Enterprise security (OAuth2, OIDC, Token Management)
- ✅ Infrastructure automation (Terraform, CDK)
- ✅ Cost optimization (Container images, registry management)
- ✅ Disaster recovery (RTO/RPO, 3-2-1 backups)
- ✅ Advanced rate limiting (Tiers, Quotas, Adaptive)

Together with Phases 1-8, Loco is now a **world-class, production-grade enterprise system** ready for global-scale deployment! 🚀
