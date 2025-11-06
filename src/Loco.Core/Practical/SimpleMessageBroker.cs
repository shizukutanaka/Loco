// Rob Pike: "Don't communicate by sharing memory; share memory by communicating"
// John Carmack: "Simple message passing is better than complex synchronization"

using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Loco.Core.Practical;

/// <summary>
/// Simple message broker - In-process pub/sub with topics
/// Fast, type-safe, zero external dependencies
/// </summary>
public class SimpleMessageBroker
{
    private readonly ConcurrentDictionary<string, List<ISubscription>> _subscriptions = new();
    private readonly SimpleLogger _logger;
    private readonly SimpleMetrics _metrics;

    public SimpleMessageBroker(SimpleLogger? logger = null, SimpleMetrics? metrics = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleMessageBroker));
        _metrics = metrics ?? new SimpleMetrics();
    }

    // Subscribe to topic
    public string Subscribe<T>(string topic, Action<T> handler)
    {
        var subscription = new Subscription<T>(Guid.NewGuid().ToString(), topic, handler);
        var subs = _subscriptions.GetOrAdd(topic, _ => new List<ISubscription>());

        lock (subs)
        {
            subs.Add(subscription);
        }

        _logger.Info($"Subscribed to topic: {topic}");
        return subscription.Id;
    }

    // Subscribe with async handler
    public string SubscribeAsync<T>(string topic, Func<T, Task> handler)
    {
        var subscription = new AsyncSubscription<T>(Guid.NewGuid().ToString(), topic, handler);
        var subs = _subscriptions.GetOrAdd(topic, _ => new List<ISubscription>());

        lock (subs)
        {
            subs.Add(subscription);
        }

        _logger.Info($"Subscribed to topic (async): {topic}");
        return subscription.Id;
    }

    // Unsubscribe
    public bool Unsubscribe(string subscriptionId)
    {
        foreach (var kvp in _subscriptions)
        {
            lock (kvp.Value)
            {
                var sub = kvp.Value.FirstOrDefault(s => s.Id == subscriptionId);
                if (sub != null)
                {
                    kvp.Value.Remove(sub);
                    _logger.Info($"Unsubscribed from topic: {kvp.Key}");
                    return true;
                }
            }
        }
        return false;
    }

    // Publish message
    public void Publish<T>(string topic, T message)
    {
        if (!_subscriptions.TryGetValue(topic, out var subs))
        {
            _metrics.IncrementCounter("broker.no_subscribers");
            return;
        }

        ISubscription[] subsSnapshot;
        lock (subs)
        {
            subsSnapshot = subs.ToArray();
        }

        _metrics.IncrementCounter("broker.published");

        foreach (var sub in subsSnapshot)
        {
            try
            {
                sub.Handle(message!);
            }
            catch (Exception ex)
            {
                _logger.Error($"Subscription handler failed for topic {topic}", ex);
                _metrics.IncrementCounter("broker.errors");
            }
        }
    }

    // Publish message async
    public async Task PublishAsync<T>(string topic, T message)
    {
        if (!_subscriptions.TryGetValue(topic, out var subs))
        {
            _metrics.IncrementCounter("broker.no_subscribers");
            return;
        }

        ISubscription[] subsSnapshot;
        lock (subs)
        {
            subsSnapshot = subs.ToArray();
        }

        _metrics.IncrementCounter("broker.published");

        var tasks = subsSnapshot.Select(async sub =>
        {
            try
            {
                await sub.HandleAsync(message!);
            }
            catch (Exception ex)
            {
                _logger.Error($"Async subscription handler failed for topic {topic}", ex);
                _metrics.IncrementCounter("broker.errors");
            }
        });

        await Task.WhenAll(tasks);
    }

    // Get subscriber count for topic
    public int GetSubscriberCount(string topic)
    {
        if (_subscriptions.TryGetValue(topic, out var subs))
        {
            lock (subs)
            {
                return subs.Count;
            }
        }
        return 0;
    }

    // Get all topics
    public IEnumerable<string> GetTopics() => _subscriptions.Keys;

    private interface ISubscription
    {
        string Id { get; }
        string Topic { get; }
        void Handle(object message);
        Task HandleAsync(object message);
    }

    private class Subscription<T> : ISubscription
    {
        private readonly Action<T> _handler;

        public string Id { get; }
        public string Topic { get; }

        public Subscription(string id, string topic, Action<T> handler)
        {
            Id = id;
            Topic = topic;
            _handler = handler;
        }

        public void Handle(object message)
        {
            if (message is T typedMessage)
            {
                _handler(typedMessage);
            }
        }

        public Task HandleAsync(object message)
        {
            Handle(message);
            return Task.CompletedTask;
        }
    }

    private class AsyncSubscription<T> : ISubscription
    {
        private readonly Func<T, Task> _handler;

        public string Id { get; }
        public string Topic { get; }

        public AsyncSubscription(string id, string topic, Func<T, Task> handler)
        {
            Id = id;
            Topic = topic;
            _handler = handler;
        }

        public void Handle(object message)
        {
            HandleAsync(message).Wait();
        }

        public async Task HandleAsync(object message)
        {
            if (message is T typedMessage)
            {
                await _handler(typedMessage);
            }
        }
    }
}

/// <summary>
/// Channel-based message queue
/// </summary>
public class SimpleMessageQueue<T>
{
    private readonly Channel<T> _channel;
    private readonly SimpleLogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _consumers = new();

    public int QueuedCount => _channel.Reader.Count;

    public SimpleMessageQueue(int capacity = 1000, SimpleLogger? logger = null)
    {
        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleMessageQueue<T>));
    }

    // Enqueue message
    public async Task<bool> EnqueueAsync(T message, CancellationToken ct = default)
    {
        try
        {
            await _channel.Writer.WriteAsync(message, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to enqueue message", ex);
            return false;
        }
    }

    // Start consumer
    public void StartConsumer(Func<T, Task> handler, int consumerCount = 1)
    {
        for (int i = 0; i < consumerCount; i++)
        {
            var task = Task.Run(async () =>
            {
                await foreach (var message in _channel.Reader.ReadAllAsync(_cts.Token))
                {
                    try
                    {
                        await handler(message);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Consumer handler failed", ex);
                    }
                }
            });

            _consumers.Add(task);
        }

        _logger.Info($"Started {consumerCount} consumers");
    }

    // Stop consumers
    public async Task StopAsync()
    {
        _channel.Writer.Complete();
        _cts.Cancel();
        await Task.WhenAll(_consumers);
        _logger.Info("Stopped all consumers");
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}

/// <summary>
/// Request-response pattern
/// </summary>
public class RequestResponseBroker
{
    private readonly SimpleMessageBroker _broker;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _pending = new();

    public RequestResponseBroker(SimpleMessageBroker broker)
    {
        _broker = broker;
    }

    // Send request and wait for response
    public async Task<TResponse> RequestAsync<TRequest, TResponse>(
        string topic,
        TRequest request,
        TimeSpan? timeout = null)
    {
        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<object>();
        _pending[requestId] = tcs;

        var requestWithId = new RequestMessage<TRequest>
        {
            Id = requestId,
            Data = request,
            ReplyTo = $"{topic}.reply"
        };

        // Subscribe to reply
        var replyTopic = requestWithId.ReplyTo;
        var subId = _broker.Subscribe<ResponseMessage<TResponse>>(replyTopic, response =>
        {
            if (response.RequestId == requestId)
            {
                _pending.TryRemove(requestId, out var completionSource);
                completionSource?.SetResult(response.Data!);
            }
        });

        try
        {
            // Publish request
            await _broker.PublishAsync(topic, requestWithId);

            // Wait for response
            var timeoutTask = Task.Delay(timeout ?? TimeSpan.FromSeconds(30));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"Request timed out after {timeout}");
            }

            return (TResponse)await tcs.Task;
        }
        finally
        {
            _broker.Unsubscribe(subId);
            _pending.TryRemove(requestId, out _);
        }
    }

    // Handle requests
    public void HandleRequests<TRequest, TResponse>(
        string topic,
        Func<TRequest, Task<TResponse>> handler)
    {
        _broker.SubscribeAsync<RequestMessage<TRequest>>(topic, async request =>
        {
            try
            {
                var response = await handler(request.Data);

                await _broker.PublishAsync(request.ReplyTo, new ResponseMessage<TResponse>
                {
                    RequestId = request.Id,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                await _broker.PublishAsync(request.ReplyTo, new ResponseMessage<TResponse>
                {
                    RequestId = request.Id,
                    Error = ex.Message
                });
            }
        });
    }

    private class RequestMessage<T>
    {
        public string Id { get; set; } = "";
        public T Data { get; set; } = default!;
        public string ReplyTo { get; set; } = "";
    }

    private class ResponseMessage<T>
    {
        public string RequestId { get; set; } = "";
        public T? Data { get; set; }
        public string? Error { get; set; }
    }
}

/// <summary>
/// Example domain events
/// </summary>
public record OrderCreated(string OrderId, string CustomerId, decimal Total);
public record OrderShipped(string OrderId, string TrackingNumber);
public record OrderCancelled(string OrderId, string Reason);
public record UserRegistered(string UserId, string Email);
public record PaymentProcessed(string OrderId, decimal Amount, bool Success);

/// <summary>
/// Example usage
/// </summary>
public class MessageBrokerExamples
{
    public static async Task Examples()
    {
        var broker = new SimpleMessageBroker();

        // Simple pub/sub
        broker.Subscribe<OrderCreated>("orders.created", order =>
        {
            Console.WriteLine($"Order created: {order.OrderId} for ${order.Total}");
        });

        broker.SubscribeAsync<OrderCreated>("orders.created", async order =>
        {
            await Task.Delay(100); // Simulate async work
            Console.WriteLine($"Sending email for order {order.OrderId}");
        });

        await broker.PublishAsync("orders.created", new OrderCreated("ORD123", "CUST456", 99.99m));

        // Message queue with consumers
        var queue = new SimpleMessageQueue<OrderCreated>();

        queue.StartConsumer(async order =>
        {
            await Task.Delay(100);
            Console.WriteLine($"Processing order {order.OrderId}");
        }, consumerCount: 3);

        await queue.EnqueueAsync(new OrderCreated("ORD124", "CUST457", 149.99m));

        // Request-response
        var reqResp = new RequestResponseBroker(broker);

        reqResp.HandleRequests<string, int>("calculate.length", async text =>
        {
            await Task.Delay(10);
            return text.Length;
        });

        var length = await reqResp.RequestAsync<string, int>("calculate.length", "Hello World");
        Console.WriteLine($"Length: {length}");

        await queue.StopAsync();
        queue.Dispose();
    }
}

/// <summary>
/// Topic-based routing
/// </summary>
public class TopicRouter
{
    private readonly SimpleMessageBroker _broker;

    public TopicRouter(SimpleMessageBroker broker)
    {
        _broker = broker;
    }

    public void Route<T>(string pattern, Action<T> handler)
    {
        _broker.Subscribe<T>(pattern, handler);
    }

    public async Task PublishAsync<T>(string topic, T message)
    {
        await _broker.PublishAsync(topic, message);

        // Wildcard support
        var parts = topic.Split('.');
        for (int i = 0; i < parts.Length; i++)
        {
            var wildcard = string.Join(".", parts.Take(i)) + ".*";
            await _broker.PublishAsync(wildcard, message);
        }
    }
}