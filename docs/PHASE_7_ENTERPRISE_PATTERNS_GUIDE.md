# Phase 7: Enterprise Patterns & Distributed Systems Guide

> **Advanced Patterns for Production-Grade Microservices**
>
> This document covers Phase 7 implementations focusing on distributed systems reliability, consistency, and advanced architectural patterns.

## Overview

Phase 7 introduces **three critical enterprise patterns** essential for building reliable, scalable distributed systems:

1. **CQRS & Event Sourcing** - Separation of read/write operations with event-based architecture
2. **Resilience Patterns** - Circuit breaker, retry, bulkhead, timeout, and fallback mechanisms
3. **Webhook Reliability** - Guaranteed delivery with idempotency and outbox pattern

---

## 1. CQRS & Event Sourcing

### What is CQRS?

**Command Query Responsibility Segregation** separates read (query) and write (command) operations:

```
Traditional Architecture:
┌─────────────────┐
│   Database      │
│  (Single Path)  │
└─────────────────┘
     ↑ ↓ (same path)
┌─────────────────┐
│   Application   │
└─────────────────┘

CQRS Architecture:
┌──────────────────┐        ┌──────────────────┐
│  Write Database  │        │  Read Database   │
│  (Optimized)     │        │  (Optimized)     │
└────────┬─────────┘        └────────▲─────────┘
         │                           │
    Command Path              Query Path
         │                           │
┌────────▼─────────────────────────────────┐
│       Application (CQRS Handler)         │
└──────────────────────────────────────────┘
```

**Benefits**:
- Optimized read and write models independently
- Handles complex business requirements
- Scales reads and writes separately
- Better performance for read-heavy applications

### What is Event Sourcing?

**Event Sourcing** stores all changes as immutable events:

```
Traditional State Storage:
┌──────────────┐
│ Current State│  (only latest state)
└──────────────┘

Event Sourcing:
┌──────────────┬──────────────┬──────────────┬──────────────┐
│ Event 1      │ Event 2      │ Event 3      │ Event 4      │
│ Created      │ Started      │ Updated      │ Completed    │
└──────────────┴──────────────┴──────────────┴──────────────┘
                              ↓
                    Reconstruct State
                    (Replay All Events)
```

**Benefits**:
- Complete audit trail of all changes
- Time travel debugging (replay from any point)
- Natural event-driven architecture
- Strong consistency guarantees

### Implementation Example

**1. Define Events**:
```csharp
public class WorkflowCreated : DomainEvent
{
    public string? Name { get; set; }
    public override string EventType => "WorkflowCreated";
}

public class WorkflowStarted : DomainEvent
{
    public override string EventType => "WorkflowStarted";
}
```

**2. Define Aggregate (Write Model)**:
```csharp
public class WorkflowAggregate : AggregateRoot
{
    private bool _isActive;
    private string? _name;

    public void CreateWorkflow(string id, string name)
    {
        RaiseEvent(new WorkflowCreated { Name = name });
    }

    protected override void ApplyEvent(DomainEvent @event)
    {
        switch (@event)
        {
            case WorkflowCreated created:
                Id = created.EventId;
                _name = created.Name;
                _isActive = true;
                break;
        }
    }
}
```

**3. Create Projections (Read Models)**:
```csharp
public class WorkflowSummaryProjection : IProjection
{
    private Dictionary<string, WorkflowSummaryModel> _projections = new();

    public async Task HandleAsync(DomainEvent @event)
    {
        if (@event is WorkflowCreated created)
        {
            _projections[created.EventId] = new WorkflowSummaryModel
            {
                Name = created.Name,
                Status = "Created"
            };
        }
    }
}
```

**4. Repository Access**:
```csharp
public async Task<Workflow> GetWorkflowAsync(string id)
{
    // Loads aggregate from event stream
    var aggregate = await repository.GetAsync(id);
    return aggregate;
}

public async Task SaveWorkflowAsync(Workflow aggregate)
{
    // Appends new events to stream
    await repository.SaveAsync(aggregate);
}
```

### When to Use CQRS & Event Sourcing

**Use when**:
- ✅ Read/write workloads are significantly different
- ✅ Complex business domain with many rules
- ✅ Need complete audit trail
- ✅ Multiple bounded contexts in microservices
- ✅ Event-driven architecture is natural fit

**Avoid when**:
- ❌ Simple CRUD application
- ❌ Team unfamiliar with event-driven patterns
- ❌ Limited performance requirements
- ❌ Strong consistency more important than scalability

---

## 2. Resilience Patterns

### Pattern: Circuit Breaker

**Problem**: Cascading failures when one service fails

```
Without Circuit Breaker:
Request 1 → Service A (fails) → Retry → Retry → Timeout
Request 2 → Service A (fails) → Retry → Retry → Timeout
Request 3 → Service A (fails) → Retry → Retry → Timeout
(All threads blocked, system overloaded)

With Circuit Breaker:
Request 1 → Service A (fails) → Count failure
Request 2 → Service A (fails) → Count failure
Request 3 → FAIL FAST (Circuit Open) → Fallback response
(Fails immediately, prevents cascading)
```

**States**:
```
Closed (Normal)
  ↓ (failures exceed threshold)
Open (Failing, blocking all)
  ↓ (timeout expires)
HalfOpen (Testing recovery)
  ↓ (success) → Closed
  ↓ (failure) → Open
```

**Implementation**:
```csharp
var breaker = new CircuitBreaker(config, logger);

try
{
    var result = await breaker.ExecuteAsync(async () =>
        await externalService.GetDataAsync()
    );
}
catch (CircuitBreakerOpenException)
{
    // Use fallback response
    return GetCachedData();
}
```

### Pattern: Retry with Exponential Backoff

**Problem**: Transient failures (network glitch, temporary overload)

**Solution**: Retry with increasing delays

```
Attempt 1: Immediate (fails)
Attempt 2: After 100ms (fails)
Attempt 3: After 200ms (fails)
Attempt 4: After 400ms (succeeds!)

Backoff: 100 → 200 → 400 → 800 → 1600ms
```

**Benefits**:
- Handles transient failures automatically
- Prevents thundering herd (jitter)
- Exponential backoff reduces load on recovering service

**Implementation**:
```csharp
var retryPolicy = new RetryPolicy(config, logger);

var result = await retryPolicy.ExecuteAsync(
    async () => await externalService.GetDataAsync(),
    shouldRetry: ex => ex is HttpRequestException
);
```

### Pattern: Bulkhead Isolation

**Problem**: One slow operation consumes all threads

```
Without Bulkhead:
┌─────────────────────────────────────┐
│ Thread Pool (10 threads)            │
│ [Service A] [Service A] [Service A] │
│ [Service A] [Service A] [Service A] │
│ [Service B] [Service B] [Service B] │
│ [Service B] [Service B]             │
│ (Service B starves because A blocks)│
└─────────────────────────────────────┘

With Bulkhead:
┌──────────────────┐  ┌──────────────────┐
│ Service A Pool   │  │ Service B Pool   │
│ (6 threads)      │  │ (4 threads)      │
│ [A] [A] [A]      │  │ [B] [B] [B] [B]  │
│ [A] [A] [A]      │  │                  │
└──────────────────┘  └──────────────────┘
(Resources isolated, fair allocation)
```

**Implementation**:
```csharp
var bulkhead = new BulkheadIsolation(config, logger);

var result = await bulkhead.ExecuteAsync(async () =>
    await externalService.GetDataAsync()
);
```

### Pattern: Timeout

**Problem**: Indefinite blocking on slow operations

```csharp
var timeoutPolicy = new TimeoutPolicy(config, logger);

var result = await timeoutPolicy.ExecuteAsync(async ct =>
    await httpClient.GetAsync(url, ct) // Cancels after timeout
);
```

### Pattern: Fallback

**Problem**: Service failure means no response

```csharp
var fallback = new FallbackPolicy<Data>(
    ex => GetDefaultData(), // Fallback factory
    logger
);

var result = await fallback.ExecuteAsync(async () =>
    await externalService.GetDataAsync()
);
```

### Combined Resilience Policy

**Recommended Order**: Timeout → Retry → Circuit Breaker → Bulkhead → Fallback

```csharp
var policy = new CombinedResiliencePolicy<string>(
    timeoutPolicy,
    retryPolicy,
    circuitBreaker,
    bulkhead,
    fallback,
    logger
);

var result = await policy.ExecuteAsync(async ct =>
    await externalService.GetDataAsync(ct)
);
```

### Real-World Scenario

```
Scenario: Microservice A calls Microservice B

1. Timeout (5 seconds): Ensure operation doesn't hang
   ↓
2. Retry (3 times): Handle network glitches
   ↓
3. Circuit Breaker: Stop calling B after N failures
   ↓
4. Bulkhead: Don't exhaust thread pool
   ↓
5. Fallback: Return cached response or default

Result: System stays up even when B is down
```

---

## 3. Webhook Reliability

### Problem: Webhook Delivery

**Challenge**: How do you guarantee a webhook is delivered?

```
Normal Flow:
Client → POST /webhook → Our Service → External Webhook
                              ↓
                        What if external fails?
                        What if we crash before retry?
```

### Solution: Outbox Pattern

**Guarantee**: At-least-once delivery

```
Step 1: Store event in local database (same transaction)
┌─────────────────────┐
│ Database            │
│ ┌─────────────────┐ │
│ │ Outbox Table    │ │ (Event stored here)
│ │ event_id        │ │
│ │ payload         │ │
│ │ is_processed    │ │
│ └─────────────────┘ │
└─────────────────────┘

Step 2: Separate process picks up from outbox
┌─────────────────────┐         ┌──────────────────┐
│ Outbox Processor    │────────→│ External Webhook │
│ Polls every 30s     │         │ (retry until ok) │
└─────────────────────┘         └──────────────────┘

Step 3: Mark as processed after successful delivery
┌─────────────────────┐
│ Database            │
│ ┌─────────────────┐ │
│ │ is_processed=1  │ │ (Remove from outbox)
│ └─────────────────┘ │
└─────────────────────┘
```

**Guarantees**:
- If we crash after storing in outbox → retry later
- If external service fails → retry with backoff
- If external service is down → keep retrying

### Solution: Idempotency

**Challenge**: External service might receive duplicate webhook

```
Scenario:
1. Send webhook to external service
2. External service processes successfully
3. Network timeout (we don't know if succeeded)
4. We retry sending same webhook
5. External service processes again (duplicate!)
```

**Solution**: Idempotency Keys

```csharp
// First request
POST /webhook
X-Idempotency-Key: abc-123
Body: { event_id: 1, data: ... }

External Service:
- Checks if idempotency key processed
- If yes: return cached response
- If no: process and cache result

// Retry (same key)
POST /webhook
X-Idempotency-Key: abc-123
Body: { event_id: 1, data: ... }

External Service:
- Checks idempotency key
- Found in cache → return same response
- No duplicate processing
```

### Implementation

**1. Publish Event (adds to outbox)**:
```csharp
var webhookEvent = new WebhookEvent
{
    EventType = "workflow.completed",
    Data = new { WorkflowId = "123" }
};

await webhookService.PublishAsync(webhookEvent);
```

**2. Outbox Processor Delivers**:
```csharp
// Background job every 30 seconds
var pending = await outbox.GetPendingAsync();

foreach (var @event in pending)
{
    foreach (var subscription in subscriptions)
    {
        var success = await webhookService.DeliverAsync(
            @event,
            subscription,
            attemptNumber: 1
        );

        if (success)
            await outbox.MarkAsProcessedAsync(@event.Id);
    }
}
```

**3. Delivery Attempts**:
```
Attempt 1: Immediate (fails)
Attempt 2: After 30 seconds (fails)
Attempt 3: After 2 minutes (fails)
Attempt 4: After 8 minutes (fails)
Attempt 5: After 30 minutes (succeeds!)
```

**4. Signing for Security**:
```csharp
// Provider signs webhook with HMAC-SHA256
var signature = webhook.ComputeSignature(secret);

// Header
X-Webhook-Signature: base64(HMAC-SHA256(payload, secret))

// Consumer verifies
public bool VerifySignature(string payload, string signature, string secret)
{
    var computed = ComputeSignature(payload, secret);
    return computed == signature;
}
```

### Webhook Event Flow

```
Client Action:
    ↓
Application (WRITE):
  1. Save workflow to database
  2. Store event in Outbox table
  3. Return 200 OK

Background Job (Outbox Processor):
  1. Poll Outbox table every 30s
  2. Find pending events
  3. Match subscriptions by event type
  4. Deliver to each subscription
  5. Retry with exponential backoff
  6. Move to dead letter queue if all retries fail

Webhook Subscriber (External):
  1. Receive webhook with signature
  2. Verify signature with HMAC-SHA256
  3. Process event (idempotent)
  4. Return 200 OK
  5. We remove from outbox
```

### Dead Letter Queue

For webhooks that fail all retries:

```csharp
var dlq = new WebhookDeadLetterQueue(logger);

// Move to DLQ after max retries
if (attemptNumber >= config.MaxRetries && !success)
{
    dlq.Enqueue(webhookEvent, new Exception("Max retries exceeded"));
}

// Operator can investigate and retry manually
var deadLetters = dlq.GetAll();
```

---

## Comparing the Patterns

### CQRS & Event Sourcing

| Aspect | Benefit |
|--------|---------|
| **Data Consistency** | Complete history, audit trail |
| **Scalability** | Scale reads and writes separately |
| **Complexity** | Higher (eventual consistency) |
| **Use Case** | Complex domain, read-heavy apps |

### Resilience Patterns

| Pattern | Problem Solved | Tradeoff |
|---------|---|---|
| **Retry** | Transient failures | Increased latency on failure |
| **Circuit Breaker** | Cascading failures | Fails fast (might lose requests) |
| **Bulkhead** | Resource exhaustion | Lower throughput per service |
| **Timeout** | Indefinite blocking | Might timeout legitimate requests |
| **Fallback** | Total unavailability | Returns stale/default data |

### Webhook Reliability

| Aspect | Guarantee |
|--------|---|
| **Delivery** | At-least-once (with idempotency) |
| **Order** | No guaranteed order |
| **Consistency** | Eventual consistency |
| **Latency** | Eventual (up to 24+ hours with retries) |

---

## Integration Example

```csharp
public class WorkflowService
{
    private readonly EventSourcedRepository<WorkflowAggregate> _repo;
    private readonly WebhookDeliveryService _webhookService;
    private readonly CombinedResiliencePolicy<ExternalData> _resiliencePolicy;

    public async Task ExecuteWorkflowAsync(string workflowId)
    {
        // Load from event store
        var aggregate = await _repo.GetAsync(workflowId);

        // Call external API with resilience
        var externalData = await _resiliencePolicy.ExecuteAsync(async ct =>
            await externalService.GetDataAsync(ct)
        );

        // Update aggregate (raises events)
        aggregate.ProcessData(externalData);

        // Save to event store
        await _repo.SaveAsync(aggregate);

        // Publish webhook (outbox pattern)
        foreach (var @event in aggregate.GetUncommittedEvents())
        {
            await _webhookService.PublishAsync(new WebhookEvent
            {
                EventType = @event.EventType,
                Data = @event
            });
        }
    }
}
```

---

## Performance Considerations

### CQRS & Event Sourcing
- **Write Performance**: Appending events is fast (sequential I/O)
- **Read Performance**: Projections are optimized for queries
- **Projection Lag**: Eventual consistency (typically <100ms)

### Resilience Patterns
- **Timeout Overhead**: <5% (just CancellationToken check)
- **Circuit Breaker Overhead**: <1% (state check)
- **Bulkhead Overhead**: <5% (semaphore operation)
- **Combined**: Total overhead ~5-10% for all patterns

### Webhook Reliability
- **Publishing**: <1ms (just store in outbox)
- **Delivery**: 0-24+ hours (depends on retries)
- **Idempotency Check**: ~10ms (cache lookup)

---

## Deployment Recommendations

### For CQRS/Event Sourcing
1. Use **EventStoreDB** (purpose-built for event sourcing)
2. Or use **PostgreSQL** with custom event table
3. Implement **snapshot strategy** for large aggregates
4. Use **projection rebuilding** for cache invalidation

### For Resilience Patterns
1. Configure timeouts based on SLA
2. Set circuit breaker thresholds conservatively
3. Monitor circuit breaker state changes
4. Tune bulkhead sizes to application needs

### For Webhook Reliability
1. Process outbox asynchronously (don't block requests)
2. Store attempts for debugging
3. Implement dead letter queue monitoring
4. Set up alerts for high retry rates
5. Provide webhook replay API for manual recovery

---

## Common Pitfalls

### CQRS & Event Sourcing
❌ **Wrong**: Storing same event in multiple aggregates
✅ **Right**: Events raised from single aggregate only

❌ **Wrong**: Modifying past events
✅ **Right**: Append-only event store, use compensation events

❌ **Wrong**: Synchronous projection updates
✅ **Right**: Asynchronous eventually consistent projections

### Resilience Patterns
❌ **Wrong**: Not idempotent operations + retry
✅ **Right**: Only retry idempotent operations

❌ **Wrong**: Same timeout for all services
✅ **Right**: Tune timeout per service SLA

❌ **Wrong**: Circuit breaker threshold too high
✅ **Right**: Open after 50%+ failures in sample

### Webhook Reliability
❌ **Wrong**: Fire-and-forget webhook delivery
✅ **Right**: Use outbox pattern for guaranteed delivery

❌ **Wrong**: Same webhook sent multiple times without idempotency check
✅ **Right**: Consumer handles idempotency gracefully

❌ **Wrong**: No signature verification
✅ **Right**: Always verify HMAC-SHA256 signature

---

## Summary

Phase 7 introduces three complementary patterns:

1. **CQRS & Event Sourcing** - Handle complex domains with complete auditability
2. **Resilience Patterns** - Build fault-tolerant systems that degrade gracefully
3. **Webhook Reliability** - Guarantee event delivery across system boundaries

Together, they form the foundation of enterprise-grade distributed systems.
