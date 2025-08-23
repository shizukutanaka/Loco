using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.WebSockets
{
    /// <summary>
    /// WebSocket connection manager for real-time communication
    /// Simple and efficient design following John Carmack's approach
    /// </summary>
    public interface IWebSocketManager
    {
        Task<string> AddConnectionAsync(WebSocket webSocket, string userId = null);
        Task RemoveConnectionAsync(string connectionId);
        Task SendMessageAsync(string connectionId, object message);
        Task SendMessageToUserAsync(string userId, object message);
        Task BroadcastAsync(object message, params string[] excludeConnectionIds);
        Task SendToGroupAsync(string groupName, object message);
        Task AddToGroupAsync(string connectionId, string groupName);
        Task RemoveFromGroupAsync(string connectionId, string groupName);
        WebSocketConnection GetConnection(string connectionId);
        int GetConnectionCount();
    }

    public class WebSocketManager : IWebSocketManager, IDisposable
    {
        private readonly ILogger<WebSocketManager> _logger;
        private readonly ConcurrentDictionary<string, WebSocketConnection> _connections;
        private readonly ConcurrentDictionary<string, HashSet<string>> _groups;
        private readonly ConcurrentDictionary<string, HashSet<string>> _userConnections;
        private readonly SemaphoreSlim _semaphore;
        private readonly Timer _pingTimer;

        public WebSocketManager(ILogger<WebSocketManager> logger)
        {
            _logger = logger;
            _connections = new ConcurrentDictionary<string, WebSocketConnection>();
            _groups = new ConcurrentDictionary<string, HashSet<string>>();
            _userConnections = new ConcurrentDictionary<string, HashSet<string>>();
            _semaphore = new SemaphoreSlim(1, 1);
            
            // Ping all connections every 30 seconds to keep them alive
            _pingTimer = new Timer(PingAllConnections, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public async Task<string> AddConnectionAsync(WebSocket webSocket, string userId = null)
        {
            var connectionId = Guid.NewGuid().ToString();
            var connection = new WebSocketConnection
            {
                Id = connectionId,
                WebSocket = webSocket,
                UserId = userId,
                ConnectedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow
            };

            _connections.TryAdd(connectionId, connection);

            if (!string.IsNullOrEmpty(userId))
            {
                await _semaphore.WaitAsync();
                try
                {
                    if (!_userConnections.ContainsKey(userId))
                    {
                        _userConnections[userId] = new HashSet<string>();
                    }
                    _userConnections[userId].Add(connectionId);
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            _logger.LogInformation("WebSocket connection established: {ConnectionId}, User: {UserId}", 
                connectionId, userId ?? "Anonymous");

            // Start listening for messages
            _ = Task.Run(async () => await ListenAsync(connection));

            return connectionId;
        }

        public async Task RemoveConnectionAsync(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var connection))
            {
                // Remove from user connections
                if (!string.IsNullOrEmpty(connection.UserId))
                {
                    await _semaphore.WaitAsync();
                    try
                    {
                        if (_userConnections.TryGetValue(connection.UserId, out var userConns))
                        {
                            userConns.Remove(connectionId);
                            if (userConns.Count == 0)
                            {
                                _userConnections.TryRemove(connection.UserId, out _);
                            }
                        }
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }

                // Remove from all groups
                foreach (var group in _groups.Values)
                {
                    group.Remove(connectionId);
                }

                // Close the WebSocket
                if (connection.WebSocket.State == WebSocketState.Open)
                {
                    await connection.WebSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Connection closed",
                        CancellationToken.None);
                }

                connection.Dispose();

                _logger.LogInformation("WebSocket connection removed: {ConnectionId}", connectionId);
            }
        }

        public async Task SendMessageAsync(string connectionId, object message)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                await SendToConnectionAsync(connection, message);
            }
        }

        public async Task SendMessageToUserAsync(string userId, object message)
        {
            if (_userConnections.TryGetValue(userId, out var connectionIds))
            {
                var tasks = new List<Task>();
                foreach (var connectionId in connectionIds)
                {
                    if (_connections.TryGetValue(connectionId, out var connection))
                    {
                        tasks.Add(SendToConnectionAsync(connection, message));
                    }
                }
                await Task.WhenAll(tasks);
            }
        }

        public async Task BroadcastAsync(object message, params string[] excludeConnectionIds)
        {
            var excludeSet = new HashSet<string>(excludeConnectionIds ?? Array.Empty<string>());
            var tasks = new List<Task>();

            foreach (var connection in _connections.Values)
            {
                if (!excludeSet.Contains(connection.Id))
                {
                    tasks.Add(SendToConnectionAsync(connection, message));
                }
            }

            await Task.WhenAll(tasks);
        }

        public async Task SendToGroupAsync(string groupName, object message)
        {
            if (_groups.TryGetValue(groupName, out var group))
            {
                var tasks = new List<Task>();
                foreach (var connectionId in group)
                {
                    if (_connections.TryGetValue(connectionId, out var connection))
                    {
                        tasks.Add(SendToConnectionAsync(connection, message));
                    }
                }
                await Task.WhenAll(tasks);
            }
        }

        public async Task AddToGroupAsync(string connectionId, string groupName)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (!_groups.ContainsKey(groupName))
                {
                    _groups[groupName] = new HashSet<string>();
                }
                _groups[groupName].Add(connectionId);
                
                _logger.LogDebug("Connection {ConnectionId} added to group {GroupName}", 
                    connectionId, groupName);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task RemoveFromGroupAsync(string connectionId, string groupName)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_groups.TryGetValue(groupName, out var group))
                {
                    group.Remove(connectionId);
                    if (group.Count == 0)
                    {
                        _groups.TryRemove(groupName, out _);
                    }
                }
                
                _logger.LogDebug("Connection {ConnectionId} removed from group {GroupName}", 
                    connectionId, groupName);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public WebSocketConnection GetConnection(string connectionId)
        {
            _connections.TryGetValue(connectionId, out var connection);
            return connection;
        }

        public int GetConnectionCount()
        {
            return _connections.Count;
        }

        private async Task SendToConnectionAsync(WebSocketConnection connection, object message)
        {
            if (connection.WebSocket.State != WebSocketState.Open)
            {
                return;
            }

            try
            {
                var json = JsonSerializer.Serialize(message);
                var bytes = Encoding.UTF8.GetBytes(json);
                var buffer = new ArraySegment<byte>(bytes);

                await connection.Semaphore.WaitAsync();
                try
                {
                    await connection.WebSocket.SendAsync(
                        buffer,
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                    
                    connection.LastActivity = DateTime.UtcNow;
                }
                finally
                {
                    connection.Semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to connection {ConnectionId}", connection.Id);
                await RemoveConnectionAsync(connection.Id);
            }
        }

        private async Task ListenAsync(WebSocketConnection connection)
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);
            
            try
            {
                while (connection.WebSocket.State == WebSocketState.Open)
                {
                    var result = await connection.WebSocket.ReceiveAsync(
                        buffer,
                        CancellationToken.None);

                    connection.LastActivity = DateTime.UtcNow;

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                        await HandleMessageAsync(connection, message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await RemoveConnectionAsync(connection.Id);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WebSocket listener for connection {ConnectionId}", connection.Id);
                await RemoveConnectionAsync(connection.Id);
            }
        }

        private async Task HandleMessageAsync(WebSocketConnection connection, string message)
        {
            try
            {
                // Parse and handle different message types
                var messageObj = JsonSerializer.Deserialize<WebSocketMessage>(message);
                
                switch (messageObj?.Type?.ToLower())
                {
                    case "ping":
                        await SendToConnectionAsync(connection, new { type = "pong", timestamp = DateTime.UtcNow });
                        break;
                        
                    case "subscribe":
                        if (!string.IsNullOrEmpty(messageObj.Channel))
                        {
                            await AddToGroupAsync(connection.Id, messageObj.Channel);
                        }
                        break;
                        
                    case "unsubscribe":
                        if (!string.IsNullOrEmpty(messageObj.Channel))
                        {
                            await RemoveFromGroupAsync(connection.Id, messageObj.Channel);
                        }
                        break;
                        
                    case "broadcast":
                        await BroadcastAsync(messageObj.Data, connection.Id);
                        break;
                        
                    default:
                        // Custom message handling
                        OnMessageReceived?.Invoke(connection, messageObj);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message from connection {ConnectionId}", connection.Id);
            }
        }

        private async void PingAllConnections(object state)
        {
            var staleTime = DateTime.UtcNow.AddMinutes(-2);
            var connectionsToRemove = new List<string>();

            foreach (var connection in _connections.Values)
            {
                if (connection.LastActivity < staleTime)
                {
                    connectionsToRemove.Add(connection.Id);
                }
                else if (connection.WebSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await SendToConnectionAsync(connection, new { type = "ping", timestamp = DateTime.UtcNow });
                    }
                    catch
                    {
                        connectionsToRemove.Add(connection.Id);
                    }
                }
            }

            foreach (var connectionId in connectionsToRemove)
            {
                await RemoveConnectionAsync(connectionId);
            }
        }

        public event Action<WebSocketConnection, WebSocketMessage> OnMessageReceived;

        public void Dispose()
        {
            _pingTimer?.Dispose();
            _semaphore?.Dispose();
            
            var tasks = new List<Task>();
            foreach (var connectionId in _connections.Keys)
            {
                tasks.Add(RemoveConnectionAsync(connectionId));
            }
            Task.WaitAll(tasks.ToArray());
            
            _connections.Clear();
            _groups.Clear();
            _userConnections.Clear();
        }
    }

    public class WebSocketConnection : IDisposable
    {
        public string Id { get; set; }
        public WebSocket WebSocket { get; set; }
        public string UserId { get; set; }
        public DateTime ConnectedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1, 1);
        public Dictionary<string, object> Metadata { get; set; } = new();

        public void Dispose()
        {
            Semaphore?.Dispose();
            if (WebSocket?.State == WebSocketState.Open)
            {
                WebSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, 
                    "Disposing", 
                    CancellationToken.None).Wait(1000);
            }
            WebSocket?.Dispose();
        }
    }

    public class WebSocketMessage
    {
        public string Type { get; set; }
        public string Channel { get; set; }
        public object Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// WebSocket hub for managing real-time events
    /// </summary>
    public class WebSocketHub
    {
        private readonly IWebSocketManager _manager;
        private readonly ILogger<WebSocketHub> _logger;

        public WebSocketHub(IWebSocketManager manager, ILogger<WebSocketHub> logger)
        {
            _manager = manager;
            _logger = logger;
        }

        public async Task NotifyRuleExecutedAsync(string ruleId, string ruleName, bool success)
        {
            await _manager.BroadcastAsync(new
            {
                type = "rule_executed",
                data = new
                {
                    ruleId,
                    ruleName,
                    success,
                    timestamp = DateTime.UtcNow
                }
            });
        }

        public async Task NotifyFlowStatusAsync(string flowId, string status)
        {
            await _manager.SendToGroupAsync($"flow_{flowId}", new
            {
                type = "flow_status",
                data = new
                {
                    flowId,
                    status,
                    timestamp = DateTime.UtcNow
                }
            });
        }

        public async Task NotifySystemEventAsync(string eventType, object eventData)
        {
            await _manager.BroadcastAsync(new
            {
                type = "system_event",
                eventType,
                data = eventData,
                timestamp = DateTime.UtcNow
            });
        }

        public async Task SendNotificationAsync(string userId, string title, string message, string severity = "info")
        {
            await _manager.SendMessageToUserAsync(userId, new
            {
                type = "notification",
                data = new
                {
                    title,
                    message,
                    severity,
                    timestamp = DateTime.UtcNow
                }
            });
        }
    }
}
