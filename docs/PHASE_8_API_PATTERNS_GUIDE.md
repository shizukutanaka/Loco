# Phase 8: Advanced API Patterns Guide

> **Idempotency, API Composition, and Feature-Driven Delivery**
>
> This document covers Phase 8 implementations for building reliable, flexible, and progressive APIs.

## Table of Contents

1. [Idempotent API Design](#idempotent-api-design)
2. [API Composition & BFF Pattern](#api-composition--bff-pattern)
3. [Feature Flags & A/B Testing](#feature-flags--ab-testing)
4. [Integration & Best Practices](#integration--best-practices)

---

## Idempotent API Design

### Problem: Retry Safety

When a client retries a failed request, we need to ensure it's processed only once:

```
Scenario: Create Order Request

Client (Attempt 1):
POST /orders
Body: { customerId: "123", amount: 100 }
─→ Server processes, saves to DB
─→ Network timeout (response lost)

Client (Attempt 2): Retries same request
POST /orders
Body: { customerId: "123", amount: 100 }
─→ Server processes again ❌ DUPLICATE
─→ Now 2 orders exist
```

### Solution: Idempotency Keys

Add a unique identifier for each operation:

```
Client (Attempt 1):
POST /orders
Idempotency-Key: abc-123-def-456
Body: { customerId: "123", amount: 100 }
─→ Server: Stores key + response
─→ Network timeout

Client (Attempt 2): Same key
POST /orders
Idempotency-Key: abc-123-def-456  (same)
Body: { customerId: "123", amount: 100 }
─→ Server: Finds key in cache
─→ Returns previous response ✅ NO DUPLICATE
```

### Implementation

**1. Define Idempotency Key Header**:
```csharp
// Client
var idempotencyKey = Guid.NewGuid().ToString(); // Generate once
var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/orders");
httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);
httpRequest.Content = new StringContent(JsonSerializer.Serialize(orderData));
```

**2. Server-Side Processing**:
```csharp
[HttpPost]
public async Task<IActionResult> CreateOrderAsync(
    [FromBody] OrderRequest request,
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
{
    // Check if request was processed before
    var cached = await idempotencyStore.GetAsync(idempotencyKey);
    if (cached != null)
    {
        // Return previous response
        return StatusCode(cached.StatusCode, cached.ResponseBody);
    }

    // Process order
    var order = await orderService.CreateAsync(request);

    // Cache response
    await idempotencyStore.StoreAsync(new IdempotencyResponse
    {
        IdempotencyKey = idempotencyKey,
        StatusCode = 201,
        ResponseBody = JsonSerializer.Serialize(order)
    });

    return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
}
```

**3. Middleware for Automatic Handling**:
```csharp
// Startup
app.UseIdempotency();

// Middleware automatically:
// - Validates Idempotency-Key format
// - Checks for cached responses
// - Caches successful responses
```

**4. Client with Automatic Retry**:
```csharp
public async Task<OrderResponse?> CreateOrderWithRetryAsync(OrderRequest request)
{
    var idempotencyKey = Guid.NewGuid().ToString();

    for (int attempt = 0; attempt < 3; attempt++)
    {
        try
        {
            var response = await httpClient.PostAsync(
                "/orders",
                new StringContent(JsonSerializer.Serialize(request)),
                idempotencyKey: idempotencyKey // Same key on retry!
            );

            if (response.IsSuccessStatusCode)
                return JsonSerializer.Deserialize<OrderResponse>(
                    await response.Content.ReadAsStringAsync());

            // Check if it was a replay
            if (response.Headers.Contains("X-Idempotency-Replay"))
                return JsonSerializer.Deserialize<OrderResponse>(
                    await response.Content.ReadAsStringAsync());
        }
        catch (HttpRequestException) when (attempt < 2)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
        }
    }

    return null;
}
```

### Request Signature Verification

Prevents clients from changing request parameters between retries:

```csharp
// Server validates request signature
var signature = RequestSignature.ComputeSignature(
    method: "POST",
    path: "/api/orders",
    body: JsonSerializer.Serialize(orderData)
);

var cached = await store.GetAsync(idempotencyKey);
if (cached != null && cached.RequestSignature != signature)
{
    throw new IdempotencyMismatchException(
        "Request parameters differ from original");
}
```

### Key Benefits

- ✅ **Safe Retries**: Retry indefinitely with same result
- ✅ **Network Resilience**: Tolerate timeout errors
- ✅ **Exactly-Once Semantics**: At-most-once guarantees
- ✅ **Client Simplicity**: Just add header, let server handle deduplication

---

## API Composition & BFF Pattern

### Problem: Multiple API Calls

Mobile app needs data from 3 different services:

```
Client Request:
"Give me workflow dashboard"

Traditional Approach:
Client → Call 1: Get workflows
       → Call 2: Get recent executions
       → Call 3: Get analytics

Problems:
- 3 round trips (latency!)
- Client logic to aggregate
- Handle 3 different failures
- Mobile bandwidth wasted
```

### Solution: Backend for Frontend (BFF)

Single endpoint aggregates all data:

```
Client Request:
GET /bff/dashboard

BFF Server (parallel):
  ├─→ Call WorkflowService (async)
  ├─→ Call ExecutionService (async)
  └─→ Call AnalyticsService (async)
       ↓ (wait for all)
  Aggregate response
       ↓
Return single JSON

Benefits:
- 1 round trip
- Server-side aggregation
- Server handles failures
- Optimized for mobile (smaller response)
```

### Implementation

**1. Composition Executor**:
```csharp
// Define downstream calls
var calls = new[]
{
    new DownstreamCall
    {
        ServiceName = "WorkflowService",
        Url = "https://workflow-service/api/workflows",
        Method = HttpMethod.Get,
        Timeout = TimeSpan.FromSeconds(5)
    },
    new DownstreamCall
    {
        ServiceName = "ExecutionService",
        Url = "https://execution-service/api/executions",
        Method = HttpMethod.Get
    }
};

// Execute in parallel
var result = await compositionExecutor.ExecuteAsync(
    calls,
    responses =>
    {
        return new
        {
            workflows = GetResponseData(responses, "WorkflowService"),
            executions = GetResponseData(responses, "ExecutionService")
        };
    }
);

return StatusCode(result.StatusCode, result.Data);
```

**2. BFF Controller**:
```csharp
[ApiController]
[Route("api/mobile/bff")]
public class MobileWorkflowBffController : BffBaseController
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardAsync()
    {
        var calls = new[]
        {
            new DownstreamCall { ServiceName = "Workflows", Url = "..." },
            new DownstreamCall { ServiceName = "Executions", Url = "..." },
            new DownstreamCall { ServiceName = "Analytics", Url = "...", Optional = true }
        };

        return await ComposeAsync(calls, AggregateResponse);
    }

    private object AggregateResponse(List<DownstreamResponse> responses)
    {
        return new
        {
            workflows = GetData(responses, "Workflows"),
            recentExecutions = GetData(responses, "Executions"),
            analytics = GetData(responses, "Analytics")
        };
    }
}
```

**3. Parallel Execution**:
```
Start: T=0ms

ServiceA: ████████ (200ms)
ServiceB: █████ (150ms)
ServiceC: ███████████ (300ms)

Wait for all: ┐
              ├─ Longest: 300ms
              ┘

Return: T=300ms
Response: [ServiceA, ServiceB, ServiceC]
```

### Failure Handling

**Optional Services**:
```csharp
new DownstreamCall
{
    ServiceName = "Analytics",
    Url = "...",
    Optional = true  // Failure doesn't block composition
}

// If fails:
// - Still return 200 OK
// - Include null for analytics
// - Include error in metadata
```

**Required Services**:
```csharp
new DownstreamCall
{
    ServiceName = "Workflows",
    Url = "...",
    Optional = false  // Failure returns 503
}

// If fails:
// - Return 503 Service Unavailable
// - Error in response
```

### Performance

- **Parallel Execution**: T = MAX(call durations)
- **Bandwidth**: Client receives only needed fields
- **Latency**: Single round trip instead of N calls
- **Example**: 3 services @ 200ms each = 200ms (parallel) vs 600ms (sequential)

---

## Feature Flags & A/B Testing

### Problem: Risky Deployments

Deploy new feature = affects all users immediately:

```
New Workflow UI (buggy):
- Deploy to production
- ALL users affected
- Bug discovered
- Rollback required (downtime)
```

### Solution: Feature Flags

Control feature visibility at runtime:

```
Feature Flag: new-workflow-ui = DISABLED

Deploy Code:
- Code deployed
- Feature disabled (off)
- Customers see old UI
- Monitor for errors

When Ready:
- Enable for 10% of users (beta)
- Monitor metrics
- Enable for 50% (wider test)
- Enable for 100% (full rollout)

If Issues:
- Disable immediately (no deploy)
- Investigate
- Fix
- Re-enable

Roll Back:
- Toggle OFF
- Zero downtime
- Users see old UI
```

### Flag Types

**1. Release Toggles** (Temporary):
```csharp
// New feature being completed
if (await flags.IsEnabledAsync("new-workflow-ui", context))
{
    return newUI;  // New code path
}
return oldUI;      // Fallback
```

**2. Experiment Toggles** (A/B Testing):
```csharp
// Testing which UI users prefer
var variant = await flags.IsEnabledAsync("experiment-ui-v2", context);
if (variant)
{
    return newDesign;  // Variant group
}
return currentDesign;  // Control group
```

**3. Permission Toggles** (Feature Gating):
```csharp
// Premium feature only
if (await flags.IsEnabledAsync("advanced-analytics", context))
{
    return advancedAnalytics;  // For premium users
}
return basicAnalytics;         // For free users
```

**4. Operations Toggles** (Infrastructure):
```csharp
// Circuit breaker
if (await flags.IsEnabledAsync("cache-enabled", context))
{
    return await cache.GetAsync(key);
}
return await database.GetAsync(key);  // Fallback
```

### Rollout Strategies

**1. Percentage-Based**:
```csharp
var flag = new FeatureFlag
{
    Name = "new-workflow-ui",
    Enabled = true,
    RolloutPercentage = 10  // 10% of users
};

// Consistent hashing ensures same user always sees same variant
var hash = context.GetConsistentHashCode();
var percentage = Math.Abs(hash % 100);
var enabled = percentage < 10;  // Deterministic per user
```

**2. User Allowlist**:
```csharp
var flag = new FeatureFlag
{
    Name = "early-access",
    AllowedUserIds = new() { "user-1", "user-2", "user-3" }
};

// Only listed users see feature
```

**3. Role-Based**:
```csharp
var flag = new FeatureFlag
{
    Name = "admin-dashboard",
    AllowedRoles = new() { "Admin", "SuperAdmin" }
};

// Only users with these roles see feature
```

**4. Scheduled**:
```csharp
var flag = new FeatureFlag
{
    Name = "black-friday-sale",
    ScheduledAt = DateTime.Parse("2025-11-28 00:00:00"),
    Enabled = true
};

// Automatically enables at scheduled time
```

### A/B Testing Example

```csharp
// Experiment: Test new checkout flow
var experiment = new Experiment
{
    Name = "checkout-v2-test",
    ControlFlagName = "checkout-current",  // Old design
    VariantFlagName = "checkout-new"       // New design
};

// User gets consistent assignment
var variant = await experimentService.GetVariantAsync(
    "checkout-v2-test",
    context  // Consistent hash per user
);

if (variant == "variant_treatment")
{
    // New checkout
    return newCheckout;
}
else
{
    // Old checkout (control)
    return oldCheckout;
}

// Track metrics for both:
// - Conversion rate
// - Avg order value
// - Completion time
```

### Implementation in Controller

**1. Using Attribute**:
```csharp
[HttpGet("advanced-analytics")]
[RequireFeatureFlag("advanced-analytics")]
public IActionResult GetAdvancedAnalytics()
{
    // Endpoint only exists if flag enabled
    return Ok(data);
}

// If flag disabled → 404 Not Found
```

**2. Using Service**:
```csharp
[HttpGet("workflows")]
public async Task<IActionResult> GetWorkflowsAsync()
{
    var context = HttpContext.Items["FeatureFlagContext"] as FeatureFlagContext;

    if (await flags.IsEnabledAsync("new-workflow-api", context))
    {
        return Ok(await newApi.GetWorkflows());
    }

    return Ok(await oldApi.GetWorkflows());
}
```

**3. Admin API**:
```csharp
[HttpPut("flags/{flagName}")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> UpdateFlagAsync(
    string flagName,
    [FromBody] FeatureFlag flag)
{
    // Admin can enable/disable in real-time
    await flagService.UpdateFlagAsync(flag);
    return Ok(flag);
}
```

### Progressive Rollout Plan

```
Day 1: Enable for 1% of users (beta testers)
  ├─ Monitor errors
  ├─ Check performance
  └─ Verify functionality

Day 2: 10% of users
  ├─ Same monitoring
  └─ Expand successful

Day 3: 25% of users
  └─ Continue monitoring

Day 4: 50% of users
  └─ Monitor A/B metrics

Day 5: 100% of users
  └─ Full rollout

Anytime: Disable immediately if issues
  └─ No deploy needed
```

---

## Integration & Best Practices

### Combining Patterns

```csharp
[HttpPost("orders")]
[RequireFeatureFlag("new-orders-api")]  // Feature flag
public async Task<IActionResult> CreateOrderAsync(
    [FromBody] OrderRequest request,
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
{
    // Validate idempotency key
    if (!Guid.TryParse(idempotencyKey, out _))
        return BadRequest("Invalid Idempotency-Key");

    // Check if already processed (idempotency)
    var cached = await idempotencyStore.GetAsync(idempotencyKey);
    if (cached != null)
        return StatusCode(cached.StatusCode, cached.ResponseBody);

    // Execute with resilience patterns
    var order = await resilientOrderService.CreateAsync(request);

    // Cache for idempotency
    await idempotencyStore.StoreAsync(new IdempotencyResponse
    {
        IdempotencyKey = idempotencyKey,
        StatusCode = 201,
        ResponseBody = JsonSerializer.Serialize(order)
    });

    return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
}
```

### Middleware Stack

```
Request
  ↓
Feature Flags Middleware (extract user context)
  ↓
Idempotency Middleware (check cache)
  ↓
Authorization Filter (check feature flag)
  ↓
API Composition (parallel calls if BFF)
  ↓
Resilience Patterns (retry, circuit breaker)
  ↓
Business Logic
  ↓
Idempotency Store (cache response)
  ↓
Response
```

### Configuration Best Practices

**1. Feature Flag Storage**:
- Development: In-memory (fast, no persistence)
- Staging: Redis or database (shared state)
- Production: Managed service (LaunchDarkly, Unleash)

**2. Idempotency Store**:
- Development: In-memory (24-hour TTL)
- Staging: Redis (24-hour TTL)
- Production: Redis or database (24-hour TTL minimum)

**3. API Composition Timeouts**:
- Per-service: 5 seconds default
- Critical services: 3 seconds (fail fast)
- Optional services: 10 seconds (wait longer)

### Monitoring & Observability

**Feature Flags**:
```csharp
// Log flag evaluations
_logger.LogInformation(
    "Feature flag evaluated: {Flag}={Enabled} ({User})",
    "new-workflow-ui",
    true,
    context.UserId
);

// Track rollout percentage
var enabledPercent = users.Where(u =>
    flags.IsEnabled("new-workflow-ui", u.Context)
).Count() * 100 / users.Count;

_logger.LogInformation("Rollout progress: {Feature}={Percent}%",
    "new-workflow-ui",
    enabledPercent);
```

**Idempotency**:
```csharp
// Track cache hits vs misses
_idempotencyMetrics.CacheHits++;  // Duplicate request
_idempotencyMetrics.CacheMisses++; // First request

// Monitor expiration
var expiredCount = store.GetExpiredCount();
_logger.LogInformation("Idempotency entries expired: {Count}", expiredCount);
```

**API Composition**:
```csharp
// Track call performance
var timing = new Dictionary<string, long>
{
    ["WorkflowService"] = 150,
    ["ExecutionService"] = 200,
    ["AnalyticsService"] = 250
};

var slowest = timing.OrderByDescending(t => t.Value).First();
_logger.LogWarning("Slow downstream service: {Service}={Time}ms",
    slowest.Key,
    slowest.Value);
```

---

## Summary

Phase 8 introduces three critical API patterns:

1. **Idempotent API Design** - Safe retries and exactly-once semantics
2. **API Composition & BFF** - Optimized endpoints for multiple clients
3. **Feature Flags** - Progressive delivery and experimentation

Together, they enable:
- ✅ Reliable, retry-safe APIs
- ✅ Optimized mobile/client experiences
- ✅ Zero-downtime feature rollout
- ✅ Safe A/B testing and experimentation
- ✅ Production-grade progressive delivery

All patterns are production-ready and follow industry best practices.
