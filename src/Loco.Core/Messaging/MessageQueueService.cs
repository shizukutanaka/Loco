using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Loco.Core.Messaging
{
    public interface IMessageQueueService
    {
        Task PublishAsync<T>(string queue, T message, MessageOptions options = null, CancellationToken cancellationToken = default);
        Task PublishAsync(string exchange, string routingKey, object message, MessageOptions options = null, CancellationToken cancellationToken = default);
        Task<MessageSubscription> SubscribeAsync<T>(string queue, Func<T, MessageContext, Task<MessageResult>> handler, SubscriptionOptions options = null, CancellationToken cancellationToken = default);
        Task<MessageSubscription> SubscribeAsync(string exchange, string routingKey, Func<byte[], MessageContext, Task<MessageResult>> handler, SubscriptionOptions options = null, CancellationToken cancellationToken = default);
        Task<T> RequestAsync<T>(string queue, object request, TimeSpan timeout, CancellationToken cancellationToken = default);
        Task RespondAsync<TRequest, TResponse>(string queue, Func<TRequest, Task<TResponse>> handler, CancellationToken cancellationToken = default);
        Task<bool> DeleteQueueAsync(string queue, CancellationToken cancellationToken = default);
        Task<QueueInfo> GetQueueInfoAsync(string queue, CancellationToken cancellationToken = default);
    }

    public class RabbitMQMessageQueueService : IMessageQueueService, IDisposable
    {
        private readonly ILogger<RabbitMQMessageQueueService> _logger;
        private readonly RabbitMQOptions _options;
        private readonly IConnection _connection;
        private readonly Dictionary<string, IModel> _channels;
        private readonly Dictionary<string, MessageSubscription> _subscriptions;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _channelLock;

        public RabbitMQMessageQueueService(ILogger<RabbitMQMessageQueueService> logger, IOptions<RabbitMQOptions> options)
        {
            _logger = logger;
            _options = options?.Value ?? new RabbitMQOptions();
            _channels = new Dictionary<string, IModel>();
            _subscriptions = new Dictionary<string, MessageSubscription>();
            _channelLock = new SemaphoreSlim(1, 1);
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            _connection = CreateConnection();
        }

        private IConnection CreateConnection()
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                AutomaticRecoveryEnabled = _options.AutomaticRecoveryEnabled,
                NetworkRecoveryInterval = _options.NetworkRecoveryInterval,
                RequestedHeartbeat = _options.RequestedHeartbeat,
                DispatchConsumersAsync = true
            };

            var connection = factory.CreateConnection(_options.ClientName ?? "Loco");

            connection.ConnectionShutdown += (sender, args) =>
            {
                _logger.LogWarning("RabbitMQ connection shutdown: {Reason}", args.ReplyText);
            };

            connection.ConnectionBlocked += (sender, args) =>
            {
                _logger.LogWarning("RabbitMQ connection blocked: {Reason}", args.Reason);
            };

            connection.ConnectionUnblocked += (sender, args) =>
            {
                _logger.LogInformation("RabbitMQ connection unblocked");
            };

            return connection;
        }

        private async Task<IModel> GetOrCreateChannelAsync(string channelKey)
        {
            await _channelLock.WaitAsync();
            try
            {
                if (!_channels.ContainsKey(channelKey) || !_channels[channelKey].IsOpen)
                {
                    var channel = _connection.CreateModel();
                    
                    // Set QoS
                    channel.BasicQos(0, _options.PrefetchCount, false);
                    
                    _channels[channelKey] = channel;
                }

                return _channels[channelKey];
            }
            finally
            {
                _channelLock.Release();
            }
        }

        public async Task PublishAsync<T>(string queue, T message, MessageOptions options = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var channel = await GetOrCreateChannelAsync($"publish_{queue}");
                
                // Declare queue
                channel.QueueDeclare(
                    queue: queue,
                    durable: options?.Durable ?? true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var json = JsonSerializer.Serialize(message, _jsonOptions);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = CreateMessageProperties(channel, options);

                channel.BasicPublish(
                    exchange: "",
                    routingKey: queue,
                    basicProperties: properties,
                    body: body);

                _logger.LogDebug("Published message to queue {Queue}", queue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing message to queue {Queue}", queue);
                throw;
            }
        }

        public async Task PublishAsync(string exchange, string routingKey, object message, MessageOptions options = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var channel = await GetOrCreateChannelAsync($"publish_{exchange}");
                
                // Declare exchange
                channel.ExchangeDeclare(
                    exchange: exchange,
                    type: options?.ExchangeType ?? ExchangeType.Direct,
                    durable: options?.Durable ?? true,
                    autoDelete: false);

                var json = JsonSerializer.Serialize(message, _jsonOptions);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = CreateMessageProperties(channel, options);

                channel.BasicPublish(
                    exchange: exchange,
                    routingKey: routingKey,
                    basicProperties: properties,
                    body: body);

                _logger.LogDebug("Published message to exchange {Exchange} with routing key {RoutingKey}", exchange, routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing message to exchange {Exchange}", exchange);
                throw;
            }
        }

        public async Task<MessageSubscription> SubscribeAsync<T>(string queue, Func<T, MessageContext, Task<MessageResult>> handler, SubscriptionOptions options = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var channel = await GetOrCreateChannelAsync($"subscribe_{queue}");
                
                // Declare queue
                channel.QueueDeclare(
                    queue: queue,
                    durable: options?.Durable ?? true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var consumer = new AsyncEventingBasicConsumer(channel);
                
                consumer.Received += async (sender, args) =>
                {
                    var context = new MessageContext
                    {
                        MessageId = args.BasicProperties?.MessageId,
                        CorrelationId = args.BasicProperties?.CorrelationId,
                        Timestamp = args.BasicProperties?.Timestamp.UnixTime > 0 
                            ? DateTimeOffset.FromUnixTimeSeconds(args.BasicProperties.Timestamp.UnixTime).DateTime 
                            : DateTime.UtcNow,
                        Headers = args.BasicProperties?.Headers,
                        DeliveryTag = args.DeliveryTag,
                        Redelivered = args.Redelivered
                    };

                    try
                    {
                        var json = Encoding.UTF8.GetString(args.Body.ToArray());
                        var message = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                        
                        var result = await handler(message, context);
                        
                        switch (result)
                        {
                            case MessageResult.Ack:
                                channel.BasicAck(args.DeliveryTag, false);
                                break;
                            case MessageResult.Nack:
                                channel.BasicNack(args.DeliveryTag, false, false);
                                break;
                            case MessageResult.Requeue:
                                channel.BasicNack(args.DeliveryTag, false, true);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from queue {Queue}", queue);
                        
                        if (options?.RequeueOnError ?? true)
                        {
                            channel.BasicNack(args.DeliveryTag, false, true);
                        }
                        else
                        {
                            channel.BasicNack(args.DeliveryTag, false, false);
                        }
                    }
                };

                var consumerTag = channel.BasicConsume(
                    queue: queue,
                    autoAck: false,
                    consumer: consumer);

                var subscription = new MessageSubscription
                {
                    Id = Guid.NewGuid().ToString(),
                    Queue = queue,
                    ConsumerTag = consumerTag,
                    Channel = channel,
                    CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                };

                _subscriptions[subscription.Id] = subscription;
                
                _logger.LogInformation("Subscribed to queue {Queue} with consumer tag {ConsumerTag}", queue, consumerTag);
                
                return subscription;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to queue {Queue}", queue);
                throw;
            }
        }

        public async Task<MessageSubscription> SubscribeAsync(string exchange, string routingKey, Func<byte[], MessageContext, Task<MessageResult>> handler, SubscriptionOptions options = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var channel = await GetOrCreateChannelAsync($"subscribe_{exchange}_{routingKey}");
                
                // Declare exchange
                channel.ExchangeDeclare(
                    exchange: exchange,
                    type: options?.ExchangeType ?? ExchangeType.Direct,
                    durable: options?.Durable ?? true,
                    autoDelete: false);

                // Create queue
                var queueName = options?.QueueName ?? channel.QueueDeclare().QueueName;
                
                // Bind queue to exchange
                channel.QueueBind(
                    queue: queueName,
                    exchange: exchange,
                    routingKey: routingKey);

                var consumer = new AsyncEventingBasicConsumer(channel);
                
                consumer.Received += async (sender, args) =>
                {
                    var context = new MessageContext
                    {
                        MessageId = args.BasicProperties?.MessageId,
                        CorrelationId = args.BasicProperties?.CorrelationId,
                        Exchange = args.Exchange,
                        RoutingKey = args.RoutingKey,
                        DeliveryTag = args.DeliveryTag,
                        Redelivered = args.Redelivered
                    };

                    try
                    {
                        var result = await handler(args.Body.ToArray(), context);
                        
                        switch (result)
                        {
                            case MessageResult.Ack:
                                channel.BasicAck(args.DeliveryTag, false);
                                break;
                            case MessageResult.Nack:
                                channel.BasicNack(args.DeliveryTag, false, false);
                                break;
                            case MessageResult.Requeue:
                                channel.BasicNack(args.DeliveryTag, false, true);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from exchange {Exchange}", exchange);
                        channel.BasicNack(args.DeliveryTag, false, options?.RequeueOnError ?? true);
                    }
                };

                var consumerTag = channel.BasicConsume(
                    queue: queueName,
                    autoAck: false,
                    consumer: consumer);

                var subscription = new MessageSubscription
                {
                    Id = Guid.NewGuid().ToString(),
                    Exchange = exchange,
                    RoutingKey = routingKey,
                    Queue = queueName,
                    ConsumerTag = consumerTag,
                    Channel = channel,
                    CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                };

                _subscriptions[subscription.Id] = subscription;
                
                return subscription;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to exchange {Exchange}", exchange);
                throw;
            }
        }

        public async Task<T> RequestAsync<T>(string queue, object request, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString();
            var replyQueue = $"reply.{correlationId}";
            var tcs = new TaskCompletionSource<T>();
            
            try
            {
                var channel = await GetOrCreateChannelAsync($"rpc_{queue}");
                
                // Create reply queue
                channel.QueueDeclare(
                    queue: replyQueue,
                    durable: false,
                    exclusive: true,
                    autoDelete: true,
                    arguments: null);

                // Subscribe to reply queue
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (sender, args) =>
                {
                    if (args.BasicProperties.CorrelationId == correlationId)
                    {
                        try
                        {
                            var json = Encoding.UTF8.GetString(args.Body.ToArray());
                            var response = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                            tcs.SetResult(response);
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                    }
                    
                    await Task.CompletedTask;
                };

                channel.BasicConsume(queue: replyQueue, autoAck: true, consumer: consumer);

                // Send request
                var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
                var requestBody = Encoding.UTF8.GetBytes(requestJson);
                
                var properties = channel.CreateBasicProperties();
                properties.CorrelationId = correlationId;
                properties.ReplyTo = replyQueue;
                
                channel.BasicPublish(
                    exchange: "",
                    routingKey: queue,
                    basicProperties: properties,
                    body: requestBody);

                // Wait for response
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeout);
                
                return await tcs.Task.WaitAsync(cts.Token);
            }
            finally
            {
                // Clean up reply queue
                try
                {
                    var channel = await GetOrCreateChannelAsync($"rpc_{queue}");
                    channel.QueueDelete(replyQueue);
                }
                catch { }
            }
        }

        public async Task RespondAsync<TRequest, TResponse>(string queue, Func<TRequest, Task<TResponse>> handler, CancellationToken cancellationToken = default)
        {
            var channel = await GetOrCreateChannelAsync($"rpc_server_{queue}");
            
            channel.QueueDeclare(
                queue: queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var consumer = new AsyncEventingBasicConsumer(channel);
            
            consumer.Received += async (sender, args) =>
            {
                try
                {
                    var requestJson = Encoding.UTF8.GetString(args.Body.ToArray());
                    var request = JsonSerializer.Deserialize<TRequest>(requestJson, _jsonOptions);
                    
                    var response = await handler(request);
                    
                    var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                    var responseBody = Encoding.UTF8.GetBytes(responseJson);
                    
                    var replyProperties = channel.CreateBasicProperties();
                    replyProperties.CorrelationId = args.BasicProperties.CorrelationId;
                    
                    channel.BasicPublish(
                        exchange: "",
                        routingKey: args.BasicProperties.ReplyTo,
                        basicProperties: replyProperties,
                        body: responseBody);
                    
                    channel.BasicAck(args.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing RPC request");
                    channel.BasicNack(args.DeliveryTag, false, false);
                }
            };

            channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);
        }

        public async Task<bool> DeleteQueueAsync(string queue, CancellationToken cancellationToken = default)
        {
            try
            {
                var channel = await GetOrCreateChannelAsync($"admin_{queue}");
                channel.QueueDelete(queue);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting queue {Queue}", queue);
                return false;
            }
        }

        public async Task<QueueInfo> GetQueueInfoAsync(string queue, CancellationToken cancellationToken = default)
        {
            try
            {
                var channel = await GetOrCreateChannelAsync($"admin_{queue}");
                var result = channel.QueueDeclarePassive(queue);
                
                return new QueueInfo
                {
                    Name = queue,
                    MessageCount = result.MessageCount,
                    ConsumerCount = result.ConsumerCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting queue info for {Queue}", queue);
                return null;
            }
        }

        private IBasicProperties CreateMessageProperties(IModel channel, MessageOptions options)
        {
            var properties = channel.CreateBasicProperties();
            
            properties.MessageId = options?.MessageId ?? Guid.NewGuid().ToString();
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            properties.Persistent = options?.Persistent ?? true;
            
            if (!string.IsNullOrEmpty(options?.CorrelationId))
                properties.CorrelationId = options.CorrelationId;
            
            if (options?.Headers != null)
                properties.Headers = options.Headers;
            
            if (options?.Priority != null)
                properties.Priority = options.Priority.Value;
            
            if (options?.Expiration != null)
                properties.Expiration = options.Expiration.Value.TotalMilliseconds.ToString();
            
            return properties;
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions.Values)
            {
                subscription.Dispose();
            }
            
            foreach (var channel in _channels.Values)
            {
                channel?.Dispose();
            }
            
            _connection?.Dispose();
            _channelLock?.Dispose();
        }
    }

    // Supporting classes
    public class RabbitMQOptions
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string VirtualHost { get; set; } = "/";
        public string ClientName { get; set; }
        public bool AutomaticRecoveryEnabled { get; set; } = true;
        public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan RequestedHeartbeat { get; set; } = TimeSpan.FromSeconds(60);
        public ushort PrefetchCount { get; set; } = 10;
    }

    public class MessageOptions
    {
        public string MessageId { get; set; }
        public string CorrelationId { get; set; }
        public bool Persistent { get; set; } = true;
        public bool Durable { get; set; } = true;
        public string ExchangeType { get; set; } = ExchangeType.Direct;
        public IDictionary<string, object> Headers { get; set; }
        public byte? Priority { get; set; }
        public TimeSpan? Expiration { get; set; }
    }

    public class SubscriptionOptions
    {
        public bool Durable { get; set; } = true;
        public bool RequeueOnError { get; set; } = true;
        public string ExchangeType { get; set; } = ExchangeType.Direct;
        public string QueueName { get; set; }
    }

    public class MessageContext
    {
        public string MessageId { get; set; }
        public string CorrelationId { get; set; }
        public string Exchange { get; set; }
        public string RoutingKey { get; set; }
        public DateTime Timestamp { get; set; }
        public IDictionary<string, object> Headers { get; set; }
        public ulong DeliveryTag { get; set; }
        public bool Redelivered { get; set; }
    }

    public enum MessageResult
    {
        Ack,
        Nack,
        Requeue
    }

    public class MessageSubscription : IDisposable
    {
        public string Id { get; set; }
        public string Queue { get; set; }
        public string Exchange { get; set; }
        public string RoutingKey { get; set; }
        public string ConsumerTag { get; set; }
        public IModel Channel { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; }

        public void Dispose()
        {
            try
            {
                if (Channel?.IsOpen == true && !string.IsNullOrEmpty(ConsumerTag))
                {
                    Channel.BasicCancel(ConsumerTag);
                }
            }
            catch { }
            
            CancellationTokenSource?.Cancel();
            CancellationTokenSource?.Dispose();
        }
    }

    public class QueueInfo
    {
        public string Name { get; set; }
        public uint MessageCount { get; set; }
        public uint ConsumerCount { get; set; }
    }
}