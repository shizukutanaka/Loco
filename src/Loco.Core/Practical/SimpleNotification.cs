// Rob Pike: "Simplicity is the key to clarity"
// John Carmack: "Make notifications simple and reliable"

using System.Collections.Concurrent;

namespace Loco.Core.Practical;

/// <summary>
/// Simple notification system - Multi-channel notifications (email, webhook, push)
/// Queue-based, retryable, templated
/// </summary>
public class SimpleNotificationService
{
    private readonly ConcurrentDictionary<string, INotificationChannel> _channels = new();
    private readonly SimpleMessageQueue<Notification> _queue;
    private readonly SimpleLogger _logger;

    public SimpleNotificationService(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleNotificationService));
        _queue = new SimpleMessageQueue<Notification>(capacity: 1000, logger: _logger);

        _queue.StartConsumer(async notification =>
        {
            await SendNotificationAsync(notification);
        }, consumerCount: 3);
    }

    // Register notification channel
    public void RegisterChannel(string name, INotificationChannel channel)
    {
        _channels[name] = channel;
        _logger.Info($"Registered notification channel: {name}");
    }

    // Send notification
    public async Task<bool> SendAsync(Notification notification)
    {
        notification.QueuedAt = DateTime.UtcNow;
        await _queue.EnqueueAsync(notification);
        _logger.Debug($"Notification queued: {notification.Id}");
        return true;
    }

    // Send to specific channel
    public async Task<bool> SendToChannelAsync(string channelName, string title, string message, Dictionary<string, string>? data = null)
    {
        var notification = new Notification
        {
            Channel = channelName,
            Title = title,
            Message = message,
            Data = data ?? new Dictionary<string, string>()
        };

        return await SendAsync(notification);
    }

    // Send to multiple channels
    public async Task<bool> BroadcastAsync(string title, string message, params string[] channels)
    {
        var tasks = channels.Select(channel => SendToChannelAsync(channel, title, message));
        await Task.WhenAll(tasks);
        return true;
    }

    private async Task SendNotificationAsync(Notification notification)
    {
        if (!_channels.TryGetValue(notification.Channel, out var channel))
        {
            _logger.Warning($"Channel not found: {notification.Channel}");
            return;
        }

        notification.Status = NotificationStatus.Sending;
        notification.SentAt = DateTime.UtcNow;

        try
        {
            await channel.SendAsync(notification);
            notification.Status = NotificationStatus.Sent;
            _logger.Info($"Notification sent via {notification.Channel}: {notification.Id}");
        }
        catch (Exception ex)
        {
            notification.Status = NotificationStatus.Failed;
            notification.Error = ex.Message;
            _logger.Error($"Failed to send notification via {notification.Channel}", ex);

            // Retry logic
            if (notification.RetryCount < notification.MaxRetries)
            {
                notification.RetryCount++;
                await Task.Delay(1000 * notification.RetryCount);
                await SendNotificationAsync(notification);
            }
        }
    }

    public async Task StopAsync()
    {
        await _queue.StopAsync();
    }

    public void Dispose()
    {
        _queue.Dispose();
    }
}

/// <summary>
/// Notification channel interface
/// </summary>
public interface INotificationChannel
{
    string Name { get; }
    Task SendAsync(Notification notification);
}

/// <summary>
/// Notification model
/// </summary>
public class Notification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Channel { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public Dictionary<string, string> Data { get; set; } = new();
    public NotificationStatus Status { get; set; } = NotificationStatus.Queued;
    public DateTime? QueuedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
}

public enum NotificationStatus
{
    Queued,
    Sending,
    Sent,
    Failed
}

/// <summary>
/// Email notification channel
/// </summary>
public class EmailNotificationChannel : INotificationChannel
{
    private readonly SimpleEmail _email;

    public string Name => "email";

    public EmailNotificationChannel(SimpleEmail email)
    {
        _email = email;
    }

    public async Task SendAsync(Notification notification)
    {
        var recipient = notification.Data.GetValueOrDefault("recipient", "");
        if (string.IsNullOrEmpty(recipient))
            throw new ArgumentException("Recipient email not specified");

        await _email.SendHtmlAsync(recipient, notification.Title, notification.Message);
    }
}

/// <summary>
/// Webhook notification channel
/// </summary>
public class WebhookNotificationChannel : INotificationChannel
{
    private readonly string _webhookUrl;
    private readonly HttpClient _httpClient = new();

    public string Name => "webhook";

    public WebhookNotificationChannel(string webhookUrl)
    {
        _webhookUrl = webhookUrl;
    }

    public async Task SendAsync(Notification notification)
    {
        var payload = new
        {
            id = notification.Id,
            title = notification.Title,
            message = notification.Message,
            data = notification.Data,
            timestamp = DateTime.UtcNow
        };

        var json = SimpleSerializer.ToJson(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_webhookUrl, content);
        response.EnsureSuccessStatusCode();
    }
}

/// <summary>
/// Console notification channel (for testing)
/// </summary>
public class ConsoleNotificationChannel : INotificationChannel
{
    public string Name => "console";

    public Task SendAsync(Notification notification)
    {
        Console.WriteLine($"[NOTIFICATION] {notification.Title}");
        Console.WriteLine($"Message: {notification.Message}");
        if (notification.Data.Any())
        {
            Console.WriteLine($"Data: {string.Join(", ", notification.Data.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Notification builder
/// </summary>
public class NotificationBuilder
{
    private readonly Notification _notification = new();

    public NotificationBuilder Channel(string channel)
    {
        _notification.Channel = channel;
        return this;
    }

    public NotificationBuilder Title(string title)
    {
        _notification.Title = title;
        return this;
    }

    public NotificationBuilder Message(string message)
    {
        _notification.Message = message;
        return this;
    }

    public NotificationBuilder Data(string key, string value)
    {
        _notification.Data[key] = value;
        return this;
    }

    public NotificationBuilder WithRetries(int maxRetries)
    {
        _notification.MaxRetries = maxRetries;
        return this;
    }

    public Notification Build() => _notification;
}

/// <summary>
/// Notification templates
/// </summary>
public static class NotificationTemplates
{
    public static Notification WelcomeNotification(string userEmail, string userName)
    {
        return new NotificationBuilder()
            .Channel("email")
            .Title("Welcome!")
            .Message($"<h1>Welcome {userName}!</h1><p>Thanks for signing up.</p>")
            .Data("recipient", userEmail)
            .Build();
    }

    public static Notification PasswordResetNotification(string userEmail, string resetLink)
    {
        return new NotificationBuilder()
            .Channel("email")
            .Title("Password Reset")
            .Message($"<p>Click here to reset your password: <a href='{resetLink}'>Reset</a></p>")
            .Data("recipient", userEmail)
            .Build();
    }

    public static Notification OrderConfirmationNotification(string userEmail, string orderId)
    {
        return new NotificationBuilder()
            .Channel("email")
            .Title("Order Confirmation")
            .Message($"<h2>Order #{orderId} Confirmed</h2><p>Thank you for your order!</p>")
            .Data("recipient", userEmail)
            .Data("orderId", orderId)
            .Build();
    }
}

/// <summary>
/// Notification preferences
/// </summary>
public class NotificationPreferences
{
    private readonly Dictionary<string, HashSet<string>> _userChannelPreferences = new();

    public void SetUserChannels(string userId, params string[] channels)
    {
        _userChannelPreferences[userId] = new HashSet<string>(channels);
    }

    public void AddUserChannel(string userId, string channel)
    {
        if (!_userChannelPreferences.ContainsKey(userId))
        {
            _userChannelPreferences[userId] = new HashSet<string>();
        }
        _userChannelPreferences[userId].Add(channel);
    }

    public void RemoveUserChannel(string userId, string channel)
    {
        if (_userChannelPreferences.TryGetValue(userId, out var channels))
        {
            channels.Remove(channel);
        }
    }

    public List<string> GetUserChannels(string userId)
    {
        return _userChannelPreferences.TryGetValue(userId, out var channels)
            ? channels.ToList()
            : new List<string>();
    }

    public bool IsChannelEnabled(string userId, string channel)
    {
        return _userChannelPreferences.TryGetValue(userId, out var channels) && channels.Contains(channel);
    }
}

/// <summary>
/// Notification aggregator - Batch notifications to reduce noise
/// </summary>
public class NotificationAggregator
{
    private readonly ConcurrentDictionary<string, List<Notification>> _pending = new();
    private readonly SimpleNotificationService _notificationService;
    private readonly TimeSpan _aggregationWindow;
    private readonly SimpleBackgroundTaskRunner _taskRunner;

    public NotificationAggregator(
        SimpleNotificationService notificationService,
        TimeSpan? aggregationWindow = null)
    {
        _notificationService = notificationService;
        _aggregationWindow = aggregationWindow ?? TimeSpan.FromMinutes(5);
        _taskRunner = new SimpleBackgroundTaskRunner();

        // Start periodic flush
        _taskRunner.RunPeriodic(async ct =>
        {
            await FlushAsync();
        }, _aggregationWindow, "NotificationAggregator");
    }

    public void Add(string userId, Notification notification)
    {
        var key = $"{userId}:{notification.Channel}";
        var notifications = _pending.GetOrAdd(key, _ => new List<Notification>());

        lock (notifications)
        {
            notifications.Add(notification);
        }
    }

    public async Task FlushAsync()
    {
        foreach (var kvp in _pending)
        {
            List<Notification> notifications;
            lock (kvp.Value)
            {
                notifications = kvp.Value.ToList();
                kvp.Value.Clear();
            }

            if (notifications.Any())
            {
                var aggregated = AggregateNotifications(notifications);
                await _notificationService.SendAsync(aggregated);
            }
        }
    }

    private Notification AggregateNotifications(List<Notification> notifications)
    {
        var first = notifications.First();
        var count = notifications.Count;

        return new NotificationBuilder()
            .Channel(first.Channel)
            .Title($"You have {count} new notifications")
            .Message(string.Join("<br>", notifications.Select(n => $"• {n.Title}: {n.Message}")))
            .Build();
    }

    public void Dispose()
    {
        _taskRunner.Dispose();
    }
}

/// <summary>
/// Example usage
/// </summary>
public class NotificationExamples
{
    public static async Task Examples()
    {
        var notificationService = new SimpleNotificationService();

        // Register channels
        notificationService.RegisterChannel("console", new ConsoleNotificationChannel());

        // If you have email configured:
        // var email = new SimpleEmail(...);
        // notificationService.RegisterChannel("email", new EmailNotificationChannel(email));

        // If you have webhook:
        // notificationService.RegisterChannel("webhook", new WebhookNotificationChannel("https://hooks.example.com"));

        // Send simple notification
        await notificationService.SendToChannelAsync(
            "console",
            "New Message",
            "You have a new message from Alice"
        );

        // Send using builder
        var notification = new NotificationBuilder()
            .Channel("console")
            .Title("System Alert")
            .Message("High CPU usage detected")
            .Data("severity", "warning")
            .Data("cpu", "85%")
            .WithRetries(3)
            .Build();

        await notificationService.SendAsync(notification);

        // Broadcast to multiple channels
        await notificationService.BroadcastAsync(
            "Maintenance Notice",
            "System will be down for maintenance at 2 AM",
            "console", "email", "webhook"
        );

        // Using templates
        var welcomeNotification = NotificationTemplates.WelcomeNotification(
            "user@example.com",
            "John Doe"
        );
        await notificationService.SendAsync(welcomeNotification);

        // User preferences
        var preferences = new NotificationPreferences();
        preferences.SetUserChannels("user123", "email", "webhook");

        if (preferences.IsChannelEnabled("user123", "email"))
        {
            Console.WriteLine("Email notifications enabled for user");
        }

        // Notification aggregation
        var aggregator = new NotificationAggregator(notificationService);

        // Add multiple notifications
        for (int i = 0; i < 5; i++)
        {
            aggregator.Add("user123", new NotificationBuilder()
                .Channel("console")
                .Title($"Update {i}")
                .Message($"Something happened {i}")
                .Build());
        }

        // Wait for aggregation window
        await Task.Delay(100);
        await aggregator.FlushAsync();

        aggregator.Dispose();
        await notificationService.StopAsync();
        notificationService.Dispose();
    }
}