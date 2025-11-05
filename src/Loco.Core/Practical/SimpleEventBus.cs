// Rob Pike: "Simplicity is the ultimate sophistication"
// John Carmack: "If something doesn't feel right, it usually isn't"

using System.Collections.Concurrent;

namespace Loco.Core.Practical;

/// <summary>
/// Simple event bus - No complex frameworks, just pub/sub that works
/// Thread-safe, fast, zero dependencies
/// </summary>
public class SimpleEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly ConcurrentDictionary<string, List<Delegate>> _namedHandlers = new();
    private readonly SimpleLogger _logger;

    public SimpleEventBus(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleEventBus));
    }

    // Subscribe to typed events
    public void Subscribe<T>(Action<T> handler)
    {
        var handlers = _handlers.GetOrAdd(typeof(T), _ => new List<Delegate>());
        lock (handlers)
        {
            handlers.Add(handler);
        }
    }

    // Subscribe with async handler
    public void Subscribe<T>(Func<T, Task> handler)
    {
        var handlers = _handlers.GetOrAdd(typeof(T), _ => new List<Delegate>());
        lock (handlers)
        {
            handlers.Add(handler);
        }
    }

    // Unsubscribe
    public bool Unsubscribe<T>(Action<T> handler)
    {
        if (!_handlers.TryGetValue(typeof(T), out var handlers))
            return false;

        lock (handlers)
        {
            return handlers.Remove(handler);
        }
    }

    // Publish event (synchronous)
    public void Publish<T>(T eventData)
    {
        if (!_handlers.TryGetValue(typeof(T), out var handlers))
            return;

        Delegate[] handlersSnapshot;
        lock (handlers)
        {
            handlersSnapshot = handlers.ToArray();
        }

        foreach (var handler in handlersSnapshot)
        {
            try
            {
                switch (handler)
                {
                    case Action<T> action:
                        action(eventData);
                        break;
                    case Func<T, Task> func:
                        func(eventData).Wait(); // Block on async
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Event handler failed for {typeof(T).Name}", ex);
            }
        }
    }

    // Publish event (async)
    public async Task PublishAsync<T>(T eventData)
    {
        if (!_handlers.TryGetValue(typeof(T), out var handlers))
            return;

        Delegate[] handlersSnapshot;
        lock (handlers)
        {
            handlersSnapshot = handlers.ToArray();
        }

        var tasks = new List<Task>();

        foreach (var handler in handlersSnapshot)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    switch (handler)
                    {
                        case Action<T> action:
                            action(eventData);
                            break;
                        case Func<T, Task> func:
                            await func(eventData);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Event handler failed for {typeof(T).Name}", ex);
                }
            }));
        }

        await Task.WhenAll(tasks);
    }

    // Clear all subscriptions for a type
    public void Clear<T>()
    {
        _handlers.TryRemove(typeof(T), out _);
    }

    // Clear all subscriptions
    public void ClearAll()
    {
        _handlers.Clear();
        _namedHandlers.Clear();
    }

    // Get handler count
    public int GetHandlerCount<T>()
    {
        if (!_handlers.TryGetValue(typeof(T), out var handlers))
            return 0;

        lock (handlers)
        {
            return handlers.Count;
        }
    }
}

/// <summary>
/// Named event bus - String-based pub/sub for dynamic scenarios
/// </summary>
public class NamedEventBus
{
    private readonly ConcurrentDictionary<string, List<Action<object>>> _handlers = new();
    private readonly SimpleLogger _logger;

    public NamedEventBus(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(NamedEventBus));
    }

    public void Subscribe(string eventName, Action<object> handler)
    {
        var handlers = _handlers.GetOrAdd(eventName, _ => new List<Action<object>>());
        lock (handlers)
        {
            handlers.Add(handler);
        }
    }

    public bool Unsubscribe(string eventName, Action<object> handler)
    {
        if (!_handlers.TryGetValue(eventName, out var handlers))
            return false;

        lock (handlers)
        {
            return handlers.Remove(handler);
        }
    }

    public void Publish(string eventName, object? data = null)
    {
        if (!_handlers.TryGetValue(eventName, out var handlers))
            return;

        Action<object>[] handlersSnapshot;
        lock (handlers)
        {
            handlersSnapshot = handlers.ToArray();
        }

        foreach (var handler in handlersSnapshot)
        {
            try
            {
                handler(data ?? new object());
            }
            catch (Exception ex)
            {
                _logger.Error($"Named event handler failed for {eventName}", ex);
            }
        }
    }
}

/// <summary>
/// Scoped event bus - Isolated event bus with lifetime management
/// </summary>
public class ScopedEventBus : IDisposable
{
    private readonly SimpleEventBus _bus;
    private readonly List<(Type, Delegate)> _subscriptions = new();
    private bool _disposed;

    public ScopedEventBus(SimpleEventBus? parentBus = null)
    {
        _bus = parentBus ?? new SimpleEventBus();
    }

    public void Subscribe<T>(Action<T> handler)
    {
        _bus.Subscribe(handler);
        _subscriptions.Add((typeof(T), handler));
    }

    public void Publish<T>(T eventData)
    {
        _bus.Publish(eventData);
    }

    public async Task PublishAsync<T>(T eventData)
    {
        await _bus.PublishAsync(eventData);
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Unsubscribe all handlers
        foreach (var (type, handler) in _subscriptions)
        {
            var method = typeof(SimpleEventBus)
                .GetMethod(nameof(SimpleEventBus.Unsubscribe))!
                .MakeGenericMethod(type);
            method.Invoke(_bus, new[] { handler });
        }

        _subscriptions.Clear();
        _disposed = true;
    }
}

/// <summary>
/// Event aggregator - Central hub for application-wide events
/// </summary>
public class EventAggregator
{
    private static readonly Lazy<EventAggregator> _instance = new(() => new EventAggregator());
    private readonly SimpleEventBus _bus = new();

    public static EventAggregator Instance => _instance.Value;

    private EventAggregator() { }

    public void Subscribe<T>(Action<T> handler) => _bus.Subscribe(handler);
    public void Subscribe<T>(Func<T, Task> handler) => _bus.Subscribe(handler);
    public bool Unsubscribe<T>(Action<T> handler) => _bus.Unsubscribe(handler);
    public void Publish<T>(T eventData) => _bus.Publish(eventData);
    public async Task PublishAsync<T>(T eventData) => await _bus.PublishAsync(eventData);
    public void Clear<T>() => _bus.Clear<T>();
    public void Reset() => _bus.ClearAll();
}

/// <summary>
/// Common event types
/// </summary>
public record AppStartedEvent(DateTime StartTime);
public record AppStoppedEvent(DateTime StopTime, TimeSpan Duration);
public record ErrorOccurredEvent(string Message, Exception? Exception = null);
public record WarningEvent(string Message);
public record InfoEvent(string Message);
public record DataChangedEvent<T>(T OldValue, T NewValue);
public record ProgressEvent(int Current, int Total, string? Message = null);

/// <summary>
/// Example: Using the event bus in a service
/// </summary>
public class OrderService
{
    private readonly SimpleEventBus _bus;
    private readonly SimpleLogger _logger;

    public OrderService(SimpleEventBus bus, SimpleLogger? logger = null)
    {
        _bus = bus;
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(OrderService));
    }

    public async Task<Order> CreateOrderAsync(string customerId, List<OrderItem> items)
    {
        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = customerId,
            Items = items,
            CreatedAt = DateTime.UtcNow
        };

        // Publish order created event
        await _bus.PublishAsync(new OrderCreatedEvent(order));

        _logger.Info($"Order {order.Id} created for customer {customerId}");
        return order;
    }

    public async Task CancelOrderAsync(string orderId)
    {
        // Cancel logic here...

        // Publish order cancelled event
        await _bus.PublishAsync(new OrderCancelledEvent(orderId, DateTime.UtcNow));

        _logger.Info($"Order {orderId} cancelled");
    }
}

// Event definitions
public record OrderCreatedEvent(Order Order);
public record OrderCancelledEvent(string OrderId, DateTime CancelledAt);
public record OrderShippedEvent(string OrderId, DateTime ShippedAt, string TrackingNumber);

// Domain models
public record Order
{
    public string Id { get; init; } = "";
    public string CustomerId { get; init; } = "";
    public List<OrderItem> Items { get; init; } = new();
    public DateTime CreatedAt { get; init; }
}

public record OrderItem(string ProductId, int Quantity, decimal Price);

/// <summary>
/// Example: Event-driven notification service
/// </summary>
public class NotificationService
{
    private readonly SimpleEventBus _bus;

    public NotificationService(SimpleEventBus bus)
    {
        _bus = bus;

        // Subscribe to events
        _bus.Subscribe<OrderCreatedEvent>(async e =>
        {
            await SendEmailAsync(e.Order.CustomerId,
                $"Order {e.Order.Id} confirmed!");
        });

        _bus.Subscribe<OrderCancelledEvent>(async e =>
        {
            await SendEmailAsync("customer@example.com",
                $"Order {e.OrderId} has been cancelled");
        });
    }

    private async Task SendEmailAsync(string to, string message)
    {
        // Simulate email sending
        await Task.Delay(10);
        Console.WriteLine($"Email sent to {to}: {message}");
    }
}