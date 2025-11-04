#nullable enable

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.DDD;

/// <summary>
/// Domain-Driven Design (DDD) Patterns
/// Bounded contexts, aggregates, domain events, repositories
/// </summary>

/// <summary>
/// Value object - immutable object with value semantics
/// </summary>
public abstract class ValueObject
{
    protected abstract IEnumerable<object> GetAtomicValues();

    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType())
            return false;

        var valueObject = (ValueObject)obj;
        return GetAtomicValues().SequenceEqual(valueObject.GetAtomicValues());
    }

    public override int GetHashCode()
    {
        return GetAtomicValues()
            .Aggregate(1, (current, obj) =>
                unchecked(current * 31 + (obj?.GetHashCode() ?? 0)));
    }
}

/// <summary>
/// Entity - object with unique identity
/// </summary>
public abstract class Entity
{
    [JsonPropertyName("id")]
    public string Id { get; protected set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; protected set; } = DateTime.UtcNow;

    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType())
            return false;

        var entity = (Entity)obj;
        return Id == entity.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}

/// <summary>
/// Domain event - represents something important that happened
/// </summary>
public abstract class DomainEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("aggregateId")]
    public string AggregateId { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Example domain event
/// </summary>
public class OrderCreatedEvent : DomainEvent
{
    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("items")]
    public List<OrderItem> Items { get; set; } = new();
}

/// <summary>
/// Order item value object
/// </summary>
public class OrderItem : ValueObject
{
    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return ProductId;
        yield return Quantity;
        yield return UnitPrice;
    }

    public decimal GetTotal() => Quantity * UnitPrice;
}

/// <summary>
/// Aggregate root - consistency boundary for related entities
/// </summary>
public abstract class AggregateRoot : Entity
{
    [JsonPropertyName("domainEvents")]
    protected readonly List<DomainEvent> _domainEvents = new();

    public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void AddDomainEvent(DomainEvent domainEvent)
    {
        domainEvent.AggregateId = Id;
        _domainEvents.Add(domainEvent);
    }
}

/// <summary>
/// Order aggregate root
/// </summary>
public class Order : AggregateRoot
{
    [JsonPropertyName("customerId")]
    public string CustomerId { get; private set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<OrderItem> Items { get; private set; } = new();

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; private set; }

    [JsonPropertyName("status")]
    public string Status { get; private set; } = "Pending"; // Pending, Confirmed, Shipped, Delivered, Cancelled

    [JsonPropertyName("version")]
    public int Version { get; private set; }

    /// <summary>
    /// Factory method to create order
    /// </summary>
    public static Order Create(string customerId, List<OrderItem> items)
    {
        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = customerId,
            Items = items,
            TotalAmount = items.Sum(i => i.GetTotal()),
            Status = "Pending"
        };

        // Raise domain event
        order.AddDomainEvent(new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = customerId,
            TotalAmount = order.TotalAmount,
            Items = items,
            Version = 1
        });

        return order;
    }

    /// <summary>
    /// Confirm order
    /// </summary>
    public void Confirm()
    {
        if (Status != "Pending")
            throw new InvalidOperationException("Can only confirm pending orders");

        Status = "Confirmed";
        UpdatedAt = DateTime.UtcNow;
        Version++;

        AddDomainEvent(new OrderConfirmedEvent
        {
            OrderId = Id,
            ConfirmedAt = DateTime.UtcNow,
            Version = Version
        });
    }

    /// <summary>
    /// Cancel order
    /// </summary>
    public void Cancel(string reason)
    {
        if (Status == "Shipped" || Status == "Delivered")
            throw new InvalidOperationException("Cannot cancel shipped/delivered orders");

        Status = "Cancelled";
        UpdatedAt = DateTime.UtcNow;
        Version++;

        AddDomainEvent(new OrderCancelledEvent
        {
            OrderId = Id,
            Reason = reason,
            Version = Version
        });
    }
}

/// <summary>
/// Domain event for order confirmed
/// </summary>
public class OrderConfirmedEvent : DomainEvent
{
    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("confirmedAt")]
    public DateTime ConfirmedAt { get; set; }
}

/// <summary>
/// Domain event for order cancelled
/// </summary>
public class OrderCancelledEvent : DomainEvent
{
    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Repository interface - abstraction for aggregate persistence
/// </summary>
public interface IRepository<T> where T : AggregateRoot
{
    Task<T?> GetByIdAsync(string id);
    Task SaveAsync(T aggregate);
    Task DeleteAsync(string id);
}

/// <summary>
/// In-memory order repository
/// </summary>
public class InMemoryOrderRepository : IRepository<Order>
{
    private readonly Dictionary<string, Order> _orders = new();
    private readonly ILogger<InMemoryOrderRepository> _logger;

    public InMemoryOrderRepository(ILogger<InMemoryOrderRepository> logger)
    {
        _logger = logger;
    }

    public async Task<Order?> GetByIdAsync(string id)
    {
        _orders.TryGetValue(id, out var order);
        return order;
    }

    public async Task SaveAsync(Order aggregate)
    {
        _orders[aggregate.Id] = aggregate;

        _logger.LogInformation(
            "Saved order {OrderId}: {Status} v{Version}",
            aggregate.Id,
            aggregate.Status,
            aggregate.Version);

        // Publish domain events
        foreach (var @event in aggregate.GetDomainEvents())
        {
            _logger.LogDebug("Domain event: {EventType}", @event.GetType().Name);
        }
    }

    public async Task DeleteAsync(string id)
    {
        if (_orders.Remove(id))
        {
            _logger.LogInformation("Deleted order {OrderId}", id);
        }
    }
}

/// <summary>
/// Bounded context - defines boundary of a domain model
/// </summary>
public class BoundedContext
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("aggregateRoots")]
    public List<string> AggregateRoots { get; set; } = new();

    [JsonPropertyName("entities")]
    public List<string> Entities { get; set; } = new();

    [JsonPropertyName("valueObjects")]
    public List<string> ValueObjects { get; set; } = new();

    [JsonPropertyName("integrationEvents")]
    public List<string> IntegrationEvents { get; set; } = new();

    [JsonPropertyName("externalDependencies")]
    public List<string> ExternalDependencies { get; set; } = new();
}

/// <summary>
/// Context map - shows relationships between bounded contexts
/// </summary>
public class ContextMap
{
    [JsonPropertyName("contexts")]
    public Dictionary<string, BoundedContext> Contexts { get; set; } = new();

    [JsonPropertyName("relationships")]
    public List<ContextRelationship> Relationships { get; set; } = new();
}

/// <summary>
/// Relationship between bounded contexts
/// </summary>
public class ContextRelationship
{
    [JsonPropertyName("upstream")]
    public string Upstream { get; set; } = string.Empty;

    [JsonPropertyName("downstream")]
    public string Downstream { get; set; } = string.Empty;

    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = string.Empty; // Conformist, Anticorruption, Partnership
}

/// <summary>
/// Domain service - coordinates between aggregates
/// </summary>
public class OrderService
{
    private readonly IRepository<Order> _repository;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IRepository<Order> repository, ILogger<OrderService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Create and save order
    /// </summary>
    public async Task<Order> CreateOrderAsync(string customerId, List<OrderItem> items)
    {
        var order = Order.Create(customerId, items);
        await _repository.SaveAsync(order);

        _logger.LogInformation(
            "Created order for customer {CustomerId} with {ItemCount} items",
            customerId,
            items.Count);

        return order;
    }

    /// <summary>
    /// Confirm order
    /// </summary>
    public async Task<Order?> ConfirmOrderAsync(string orderId)
    {
        var order = await _repository.GetByIdAsync(orderId);
        if (order == null)
            return null;

        order.Confirm();
        await _repository.SaveAsync(order);

        _logger.LogInformation("Confirmed order {OrderId}", orderId);

        return order;
    }

    /// <summary>
    /// Cancel order
    /// </summary>
    public async Task CancelOrderAsync(string orderId, string reason)
    {
        var order = await _repository.GetByIdAsync(orderId);
        if (order == null)
            return;

        order.Cancel(reason);
        await _repository.SaveAsync(order);

        _logger.LogInformation("Cancelled order {OrderId}: {Reason}", orderId, reason);
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class DddExtensions
{
    public static IServiceCollection AddDomainDrivenDesign(this IServiceCollection services)
    {
        services.AddSingleton<IRepository<Order>, InMemoryOrderRepository>();
        services.AddSingleton<OrderService>();
        return services;
    }
}
