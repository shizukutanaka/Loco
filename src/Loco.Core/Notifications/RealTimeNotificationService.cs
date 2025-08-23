using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Loco.Core.Notifications
{
    public interface IRealTimeNotificationService
    {
        Task SendNotificationAsync(string userId, Notification notification);
        Task SendNotificationToGroupAsync(string groupId, Notification notification);
        Task BroadcastNotificationAsync(Notification notification);
        Task SubscribeToNotificationsAsync(string userId, string connectionId);
        Task UnsubscribeFromNotificationsAsync(string connectionId);
        Task<IEnumerable<Notification>> GetUnreadNotificationsAsync(string userId);
        Task MarkAsReadAsync(string userId, Guid notificationId);
        Task<NotificationStatistics> GetStatisticsAsync(string userId);
    }

    public class RealTimeNotificationService : IRealTimeNotificationService, IHostedService
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RealTimeNotificationService> _logger;
        private readonly INotificationStore _store;
        private readonly ConcurrentDictionary<string, HashSet<string>> _userConnections;
        private readonly Channel<NotificationMessage> _notificationChannel;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task _processingTask;

        public RealTimeNotificationService(
            IHubContext<NotificationHub> hubContext,
            IConnectionMultiplexer redis,
            ILogger<RealTimeNotificationService> logger,
            INotificationStore store)
        {
            _hubContext = hubContext;
            _redis = redis;
            _logger = logger;
            _store = store;
            _userConnections = new ConcurrentDictionary<string, HashSet<string>>();
            _notificationChannel = Channel.CreateUnbounded<NotificationMessage>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _processingTask = ProcessNotificationsAsync(_cancellationTokenSource.Token);
            await SubscribeToRedisChannelsAsync();
            _logger.LogInformation("Real-time notification service started");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource.Cancel();
            _notificationChannel.Writer.Complete();
            
            if (_processingTask != null)
            {
                await _processingTask;
            }
            
            _logger.LogInformation("Real-time notification service stopped");
        }

        public async Task SendNotificationAsync(string userId, Notification notification)
        {
            notification.Id = Guid.NewGuid();
            notification.UserId = userId;
            notification.CreatedAt = DateTime.UtcNow;
            notification.IsRead = false;

            // Store notification
            await _store.SaveNotificationAsync(notification);

            // Send via SignalR if user is connected
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                await _hubContext.Clients.Clients(connections.ToList())
                    .SendAsync("ReceiveNotification", notification);
                
                _logger.LogInformation("Notification sent to user {UserId} via SignalR", userId);
            }

            // Publish to Redis for distributed scenarios
            var subscriber = _redis.GetSubscriber();
            await subscriber.PublishAsync($"notifications:{userId}", 
                System.Text.Json.JsonSerializer.Serialize(notification));

            // Queue for processing (email, push, etc.)
            await _notificationChannel.Writer.WriteAsync(new NotificationMessage
            {
                Type = NotificationMessageType.User,
                TargetId = userId,
                Notification = notification
            });
        }

        public async Task SendNotificationToGroupAsync(string groupId, Notification notification)
        {
            notification.Id = Guid.NewGuid();
            notification.GroupId = groupId;
            notification.CreatedAt = DateTime.UtcNow;
            notification.IsRead = false;

            // Get group members
            var members = await _store.GetGroupMembersAsync(groupId);
            
            // Store notification for each member
            var tasks = members.Select(async memberId =>
            {
                var memberNotification = notification.Clone();
                memberNotification.UserId = memberId;
                await _store.SaveNotificationAsync(memberNotification);
            });
            await Task.WhenAll(tasks);

            // Send via SignalR
            await _hubContext.Clients.Group(groupId)
                .SendAsync("ReceiveNotification", notification);

            _logger.LogInformation("Notification sent to group {GroupId}", groupId);

            // Queue for processing
            await _notificationChannel.Writer.WriteAsync(new NotificationMessage
            {
                Type = NotificationMessageType.Group,
                TargetId = groupId,
                Notification = notification
            });
        }

        public async Task BroadcastNotificationAsync(Notification notification)
        {
            notification.Id = Guid.NewGuid();
            notification.CreatedAt = DateTime.UtcNow;
            notification.IsBroadcast = true;

            // Store as broadcast notification
            await _store.SaveBroadcastNotificationAsync(notification);

            // Send to all connected clients
            await _hubContext.Clients.All
                .SendAsync("ReceiveBroadcast", notification);

            _logger.LogInformation("Broadcast notification sent");

            // Publish to Redis
            var subscriber = _redis.GetSubscriber();
            await subscriber.PublishAsync("notifications:broadcast", 
                System.Text.Json.JsonSerializer.Serialize(notification));
        }

        public async Task SubscribeToNotificationsAsync(string userId, string connectionId)
        {
            _userConnections.AddOrUpdate(userId,
                new HashSet<string> { connectionId },
                (key, connections) =>
                {
                    connections.Add(connectionId);
                    return connections;
                });

            // Add to SignalR group
            await _hubContext.Groups.AddToGroupAsync(connectionId, $"user-{userId}");

            // Send any pending notifications
            var unreadNotifications = await GetUnreadNotificationsAsync(userId);
            if (unreadNotifications.Any())
            {
                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("ReceivePendingNotifications", unreadNotifications);
            }

            _logger.LogInformation("User {UserId} subscribed with connection {ConnectionId}", 
                userId, connectionId);
        }

        public async Task UnsubscribeFromNotificationsAsync(string connectionId)
        {
            foreach (var kvp in _userConnections)
            {
                if (kvp.Value.Remove(connectionId))
                {
                    if (kvp.Value.Count == 0)
                    {
                        _userConnections.TryRemove(kvp.Key, out _);
                    }

                    await _hubContext.Groups.RemoveFromGroupAsync(connectionId, $"user-{kvp.Key}");
                    
                    _logger.LogInformation("Connection {ConnectionId} unsubscribed from user {UserId}", 
                        connectionId, kvp.Key);
                    break;
                }
            }
        }

        public async Task<IEnumerable<Notification>> GetUnreadNotificationsAsync(string userId)
        {
            return await _store.GetUnreadNotificationsAsync(userId);
        }

        public async Task MarkAsReadAsync(string userId, Guid notificationId)
        {
            await _store.MarkAsReadAsync(userId, notificationId);
            
            // Notify connected clients
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                await _hubContext.Clients.Clients(connections.ToList())
                    .SendAsync("NotificationRead", notificationId);
            }
        }

        public async Task<NotificationStatistics> GetStatisticsAsync(string userId)
        {
            var notifications = await _store.GetUserNotificationsAsync(userId, DateTime.UtcNow.AddDays(-30));
            
            return new NotificationStatistics
            {
                UserId = userId,
                TotalNotifications = notifications.Count(),
                UnreadCount = notifications.Count(n => !n.IsRead),
                ReadCount = notifications.Count(n => n.IsRead),
                NotificationsByType = notifications.GroupBy(n => n.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                LastNotificationAt = notifications.MaxBy(n => n.CreatedAt)?.CreatedAt
            };
        }

        private async Task ProcessNotificationsAsync(CancellationToken cancellationToken)
        {
            await foreach (var message in _notificationChannel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await ProcessNotificationMessage(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing notification message");
                }
            }
        }

        private async Task ProcessNotificationMessage(NotificationMessage message)
        {
            // Process different notification delivery channels
            switch (message.Notification.DeliveryChannels)
            {
                case NotificationChannel.Email:
                    await SendEmailNotificationAsync(message);
                    break;
                case NotificationChannel.Push:
                    await SendPushNotificationAsync(message);
                    break;
                case NotificationChannel.SMS:
                    await SendSmsNotificationAsync(message);
                    break;
                case NotificationChannel.All:
                    await Task.WhenAll(
                        SendEmailNotificationAsync(message),
                        SendPushNotificationAsync(message),
                        SendSmsNotificationAsync(message)
                    );
                    break;
            }
        }

        private async Task SendEmailNotificationAsync(NotificationMessage message)
        {
            // Email implementation
            _logger.LogInformation("Sending email notification for {NotificationId}", 
                message.Notification.Id);
            await Task.Delay(100); // Simulate email sending
        }

        private async Task SendPushNotificationAsync(NotificationMessage message)
        {
            // Push notification implementation
            _logger.LogInformation("Sending push notification for {NotificationId}", 
                message.Notification.Id);
            await Task.Delay(50); // Simulate push sending
        }

        private async Task SendSmsNotificationAsync(NotificationMessage message)
        {
            // SMS implementation
            _logger.LogInformation("Sending SMS notification for {NotificationId}", 
                message.Notification.Id);
            await Task.Delay(150); // Simulate SMS sending
        }

        private async Task SubscribeToRedisChannelsAsync()
        {
            var subscriber = _redis.GetSubscriber();
            
            // Subscribe to broadcast channel
            await subscriber.SubscribeAsync("notifications:broadcast", async (channel, value) =>
            {
                try
                {
                    var notification = System.Text.Json.JsonSerializer.Deserialize<Notification>(value);
                    await _hubContext.Clients.All.SendAsync("ReceiveBroadcast", notification);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling broadcast notification from Redis");
                }
            });

            _logger.LogInformation("Subscribed to Redis notification channels");
        }
    }

    // SignalR Hub
    public class NotificationHub : Hub
    {
        private readonly IRealTimeNotificationService _notificationService;
        private readonly ILogger<NotificationHub> _logger;

        public NotificationHub(
            IRealTimeNotificationService notificationService,
            ILogger<NotificationHub> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier ?? Context.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(userId))
            {
                await _notificationService.SubscribeToNotificationsAsync(userId, Context.ConnectionId);
            }
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await _notificationService.UnsubscribeFromNotificationsAsync(Context.ConnectionId);
            
            if (exception != null)
            {
                _logger.LogError(exception, "Client disconnected with error");
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinGroup(string groupId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupId);
            _logger.LogInformation("Connection {ConnectionId} joined group {GroupId}", 
                Context.ConnectionId, groupId);
        }

        public async Task LeaveGroup(string groupId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId);
            _logger.LogInformation("Connection {ConnectionId} left group {GroupId}", 
                Context.ConnectionId, groupId);
        }

        public async Task MarkAsRead(Guid notificationId)
        {
            var userId = Context.UserIdentifier ?? Context.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(userId))
            {
                await _notificationService.MarkAsReadAsync(userId, notificationId);
            }
        }

        public async Task<IEnumerable<Notification>> GetUnreadNotifications()
        {
            var userId = Context.UserIdentifier ?? Context.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(userId))
            {
                return await _notificationService.GetUnreadNotificationsAsync(userId);
            }
            return Enumerable.Empty<Notification>();
        }
    }

    // Supporting classes
    public interface INotificationStore
    {
        Task SaveNotificationAsync(Notification notification);
        Task SaveBroadcastNotificationAsync(Notification notification);
        Task<IEnumerable<Notification>> GetUnreadNotificationsAsync(string userId);
        Task<IEnumerable<Notification>> GetUserNotificationsAsync(string userId, DateTime since);
        Task<IEnumerable<string>> GetGroupMembersAsync(string groupId);
        Task MarkAsReadAsync(string userId, Guid notificationId);
    }

    public class Notification
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }
        public string GroupId { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string ImageUrl { get; set; }
        public string ActionUrl { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public NotificationPriority Priority { get; set; }
        public NotificationChannel DeliveryChannels { get; set; }
        public bool IsRead { get; set; }
        public bool IsBroadcast { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime? ExpiresAt { get; set; }

        public Notification Clone()
        {
            return new Notification
            {
                Id = Id,
                GroupId = GroupId,
                Type = Type,
                Title = Title,
                Message = Message,
                ImageUrl = ImageUrl,
                ActionUrl = ActionUrl,
                Data = Data != null ? new Dictionary<string, object>(Data) : null,
                Priority = Priority,
                DeliveryChannels = DeliveryChannels,
                IsRead = IsRead,
                IsBroadcast = IsBroadcast,
                CreatedAt = CreatedAt,
                ReadAt = ReadAt,
                ExpiresAt = ExpiresAt
            };
        }
    }

    public enum NotificationPriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    [Flags]
    public enum NotificationChannel
    {
        InApp = 1,
        Email = 2,
        Push = 4,
        SMS = 8,
        All = InApp | Email | Push | SMS
    }

    public class NotificationMessage
    {
        public NotificationMessageType Type { get; set; }
        public string TargetId { get; set; }
        public Notification Notification { get; set; }
    }

    public enum NotificationMessageType
    {
        User,
        Group,
        Broadcast
    }

    public class NotificationStatistics
    {
        public string UserId { get; set; }
        public int TotalNotifications { get; set; }
        public int UnreadCount { get; set; }
        public int ReadCount { get; set; }
        public Dictionary<string, int> NotificationsByType { get; set; }
        public DateTime? LastNotificationAt { get; set; }
    }
}