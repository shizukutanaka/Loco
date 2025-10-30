using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.EdgeComputing
{
    /// <summary>
    /// Edge Synchronization Manager for syncing edge execution results with cloud
    /// Handles bidirectional sync, conflict resolution, and offline queue management
    /// </summary>
    public class EdgeSyncManager : IDisposable
    {
        private readonly EdgeConfiguration _config;
        private readonly ILogger<EdgeSyncManager> _logger;
        private readonly Dictionary<string, SyncQueue> _syncQueues = new();
        private readonly EdgeEncryptionService _encryptionService;
        private bool _disposed;

        public EdgeSyncManager(EdgeConfiguration config, ILogger<EdgeSyncManager> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _encryptionService = new EdgeEncryptionService(config);
        }

        /// <summary>
        /// Configures synchronization for a workflow-device pair
        /// </summary>
        public async Task ConfigureSyncAsync(
            string workflowId,
            string deviceId,
            EdgeSyncConfig syncConfig,
            CancellationToken cancellationToken = default)
        {
            var queueKey = $"{workflowId}:{deviceId}";

            if (!_syncQueues.TryGetValue(queueKey, out var queue))
            {
                queue = new SyncQueue
                {
                    WorkflowId = workflowId,
                    DeviceId = deviceId,
                    Config = syncConfig,
                    CreatedAt = DateTime.UtcNow
                };
                _syncQueues[queueKey] = queue;
            }

            queue.Config = syncConfig;
            queue.LastConfigUpdate = DateTime.UtcNow;

            _logger.LogInformation("Configured sync for workflow {WorkflowId} on device {DeviceId}", workflowId, deviceId);
        }

        /// <summary>
        /// Syncs edge execution results to cloud
        /// </summary>
        public async Task<SyncResult> SyncResultsAsync(
            EdgeExecutionResults edgeResults,
            EdgeSyncOptions options,
            CancellationToken cancellationToken = default)
        {
            var result = new SyncResult
            {
                WorkflowId = edgeResults.WorkflowId,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("Starting sync for workflow {WorkflowId} with {ExecutionCount} executions",
                    edgeResults.WorkflowId, edgeResults.ExecutionCount);

                // 1. Compress and encrypt data
                var compressedData = await CompressResultsAsync(edgeResults, options.Compression);
                var encryptedData = options.EncryptData ?
                    await _encryptionService.EncryptAsync(compressedData) : compressedData;

                // 2. Send to cloud endpoint
                var cloudResponse = await SendToCloudAsync(encryptedData, edgeResults.WorkflowId, cancellationToken);

                // 3. Handle response
                if (cloudResponse.Success)
                {
                    result.RecordCount = edgeResults.ExecutionCount;
                    result.DataSize = encryptedData.Length;
                    result.Status = SyncStatus.Success;

                    _logger.LogInformation("Successfully synced {RecordCount} records ({DataSize} bytes) for workflow {WorkflowId}",
                        result.RecordCount, result.DataSize, edgeResults.WorkflowId);
                }
                else
                {
                    result.Status = SyncStatus.Failed;
                    result.Error = cloudResponse.Error;
                    await QueueForRetryAsync(edgeResults, options, cancellationToken);
                }

                result.CompletedAt = DateTime.UtcNow;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync results for workflow {WorkflowId}", edgeResults.WorkflowId);

                result.Status = SyncStatus.Failed;
                result.Error = ex.Message;
                result.CompletedAt = DateTime.UtcNow;

                await QueueForRetryAsync(edgeResults, options, cancellationToken);
                return result;
            }
        }

        /// <summary>
        /// Gets cloud updates for a workflow since last sync
        /// </summary>
        public async Task<List<CloudUpdate>> GetCloudUpdatesAsync(
            string workflowId,
            DateTime since,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Query cloud API for updates since last sync
                var updates = await QueryCloudUpdatesAsync(workflowId, since, cancellationToken);

                _logger.LogDebug("Retrieved {UpdateCount} cloud updates for workflow {WorkflowId}",
                    updates.Count, workflowId);

                return updates;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get cloud updates for workflow {WorkflowId}", workflowId);
                return new List<CloudUpdate>();
            }
        }

        /// <summary>
        /// Updates cloud workflow state
        /// </summary>
        public async Task UpdateCloudStateAsync(
            CloudWorkflowState state,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await SendStateUpdateAsync(state, cancellationToken);
                _logger.LogDebug("Updated cloud state for workflow {WorkflowId}", state.WorkflowId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update cloud state for workflow {WorkflowId}", state.WorkflowId);
            }
        }

        /// <summary>
        /// Escalates critical alerts to cloud immediately
        /// </summary>
        public async Task EscalateAlertAsync(
            EdgeDevice device,
            EdgeAlert alert,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var escalation = new AlertEscalation
                {
                    DeviceId = device.DeviceId,
                    AlertId = alert.AlertId,
                    Severity = alert.Severity,
                    Message = alert.Message,
                    Context = alert.Context,
                    EscalatedAt = DateTime.UtcNow
                };

                await SendCriticalAlertAsync(escalation, cancellationToken);
                _logger.LogWarning("Escalated critical alert {AlertId} from device {DeviceId}", alert.AlertId, device.DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to escalate critical alert {AlertId} from device {DeviceId}",
                    alert.AlertId, device.DeviceId);
            }
        }

        /// <summary>
        /// Gets synchronization status for a device
        /// </summary>
        public async Task<SyncStatus> GetSyncStatusAsync(
            EdgeDevice device,
            CancellationToken cancellationToken = default)
        {
            var status = new SyncStatus();

            foreach (var queue in _syncQueues.Values.Where(q => q.DeviceId == device.DeviceId))
            {
                // Get queue status
                status.PendingRecords += queue.PendingItems.Count;
                status.PendingDataSize += queue.GetTotalDataSize();

                if (queue.LastSyncSuccess.HasValue)
                {
                    status.LastSyncSuccess = queue.LastSyncSuccess.Value;
                }

                if (queue.LastSyncFailure.HasValue && (!status.LastSyncFailure.HasValue || queue.LastSyncFailure.Value > status.LastSyncFailure.Value))
                {
                    status.LastSyncFailure = queue.LastSyncFailure.Value;
                    status.LastError = queue.LastError;
                }
            }

            return status;
        }

        public void Dispose()
        {
            if (_disposed) return;

            foreach (var queue in _syncQueues.Values)
            {
                queue.Dispose();
            }
            _syncQueues.Clear();

            _encryptionService.Dispose();
            _disposed = true;
        }

        private async Task<byte[]> CompressResultsAsync(EdgeExecutionResults results, CompressionLevel compression)
        {
            // Serialize results to JSON
            var json = System.Text.Json.JsonSerializer.Serialize(results);
            var data = System.Text.Encoding.UTF8.GetBytes(json);

            if (compression == CompressionLevel.None)
            {
                return data;
            }

            // Compress data
            using var output = new System.IO.MemoryStream();
            using var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionMode.Compress);

            await gzip.WriteAsync(data);
            await gzip.FlushAsync();
            gzip.Close();

            return output.ToArray();
        }

        private async Task<CloudResponse> SendToCloudAsync(
            byte[] data,
            string workflowId,
            CancellationToken cancellationToken)
        {
            try
            {
                // Simulate cloud API call
                await Task.Delay(100, cancellationToken); // Simulate network delay

                // In real implementation, this would make HTTP request to cloud endpoint
                return new CloudResponse
                {
                    Success = true,
                    RecordCount = 1,
                    Message = "Data synced successfully"
                };
            }
            catch (Exception ex)
            {
                return new CloudResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        private async Task<List<CloudUpdate>> QueryCloudUpdatesAsync(
            string workflowId,
            DateTime since,
            CancellationToken cancellationToken)
        {
            try
            {
                // Simulate cloud API call
                await Task.Delay(50, cancellationToken);

                // In real implementation, this would query cloud API for updates
                return new List<CloudUpdate>
                {
                    new CloudUpdate
                    {
                        UpdateId = Guid.NewGuid().ToString(),
                        Type = UpdateType.ConfigurationChange,
                        WorkflowId = workflowId,
                        Data = new { MaxRetries = 5 },
                        CreatedAt = DateTime.UtcNow
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying cloud updates for workflow {WorkflowId}", workflowId);
                return new List<CloudUpdate>();
            }
        }

        private async Task SendStateUpdateAsync(CloudWorkflowState state, CancellationToken cancellationToken)
        {
            // Send state update to cloud
            await Task.Delay(50, cancellationToken);
        }

        private async Task SendCriticalAlertAsync(AlertEscalation escalation, CancellationToken cancellationToken)
        {
            // Send critical alert to cloud immediately
            await Task.Delay(100, cancellationToken);
        }

        private async Task QueueForRetryAsync(
            EdgeExecutionResults results,
            EdgeSyncOptions options,
            CancellationToken cancellationToken)
        {
            var queueKey = $"{results.WorkflowId}:{results.Executions.FirstOrDefault()?.DeviceId ?? "unknown"}";

            if (!_syncQueues.TryGetValue(queueKey, out var queue))
            {
                queue = new SyncQueue
                {
                    WorkflowId = results.WorkflowId,
                    DeviceId = results.Executions.FirstOrDefault()?.DeviceId ?? "unknown",
                    Config = new EdgeSyncConfig(),
                    CreatedAt = DateTime.UtcNow
                };
                _syncQueues[queueKey] = queue;
            }

            // Add to retry queue
            var queueItem = new SyncQueueItem
            {
                Id = Guid.NewGuid().ToString(),
                Data = results,
                RetryCount = 0,
                FirstAttempt = DateTime.UtcNow,
                NextRetry = DateTime.UtcNow.Add(options.RetryDelay)
            };

            queue.PendingItems.Add(queueItem);
            queue.LastSyncFailure = DateTime.UtcNow;
            queue.LastError = "Sync failed, queued for retry";

            _logger.LogWarning("Queued sync for retry: workflow {WorkflowId}, attempt {RetryCount}",
                results.WorkflowId, queueItem.RetryCount + 1);
        }
    }

    /// <summary>
    /// IoT Device Manager for managing edge devices and their capabilities
    /// </summary>
    public class IoTDeviceManager : IDisposable
    {
        private readonly EdgeConfiguration _config;
        private readonly ILogger<IoTDeviceManager> _logger;
        private readonly Dictionary<string, EdgeDevice> _devices = new();
        private readonly Dictionary<string, List<SensorDataPoint>> _sensorHistory = new();
        private readonly Dictionary<string, List<EdgeAlert>> _alertHistory = new();
        private readonly Dictionary<string, List<EdgeMetrics>> _metricsHistory = new();
        private bool _disposed;

        public IoTDeviceManager(EdgeConfiguration config, ILogger<IoTDeviceManager> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Registers a new edge device
        /// </summary>
        public async Task RegisterDeviceAsync(EdgeDevice device, CancellationToken cancellationToken = default)
        {
            _devices[device.DeviceId] = device;
            _sensorHistory[device.DeviceId] = new List<SensorDataPoint>();
            _alertHistory[device.DeviceId] = new List<EdgeAlert>();
            _metricsHistory[device.DeviceId] = new List<EdgeMetrics>();

            _logger.LogInformation("Registered edge device {DeviceId} ({Name})", device.DeviceId, device.Name);
        }

        /// <summary>
        /// Gets connectivity status for a device
        /// </summary>
        public async Task<ConnectivityStatus> GetConnectivityStatusAsync(
            EdgeDevice device,
            CancellationToken cancellationToken = default)
        {
            var status = new ConnectivityStatus
            {
                IsOnline = device.HasNetworkConnectivity,
                LastConnected = device.LastSeen,
                NetworkType = "WiFi", // Simplified
                LatencyMs = 50, // Simulated
                BandwidthMbps = 10 // Simulated
            };

            // Update last seen time
            device.LastSeen = DateTime.UtcNow;
            _devices[device.DeviceId] = device;

            return status;
        }

        /// <summary>
        /// Gets current resource utilization for a device
        /// </summary>
        public async Task<ResourceUtilization> GetResourceUtilizationAsync(
            EdgeDevice device,
            CancellationToken cancellationToken = default)
        {
            // Simulate resource utilization
            var utilization = new ResourceUtilization
            {
                CpuPercent = new Random().NextDouble() * 100,
                MemoryPercent = new Random().NextDouble() * 100,
                StoragePercent = (device.AvailableStorageMB / (device.AvailableStorageMB + 1000)) * 100,
                NetworkPercent = new Random().NextDouble() * 100,
                MeasuredAt = DateTime.UtcNow
            };

            return utilization;
        }

        /// <summary>
        /// Processes sensor reading from device
        /// </summary>
        public async Task ProcessSensorReadingAsync(
            EdgeDevice device,
            SensorDataPoint dataPoint,
            CancellationToken cancellationToken = default)
        {
            if (!_sensorHistory.TryGetValue(device.DeviceId, out var history))
            {
                history = new List<SensorDataPoint>();
                _sensorHistory[device.DeviceId] = history;
            }

            history.Add(dataPoint);

            // Keep only recent history (last 1000 readings per device)
            if (history.Count > 1000)
            {
                history.RemoveRange(0, history.Count - 1000);
            }

            _logger.LogDebug("Processed sensor reading from device {DeviceId}: {SensorType} = {Value}",
                device.DeviceId, dataPoint.SensorType, dataPoint.Value);
        }

        /// <summary>
        /// Processes alert from device
        /// </summary>
        public async Task ProcessAlertAsync(
            EdgeDevice device,
            EdgeAlert alert,
            CancellationToken cancellationToken = default)
        {
            if (!_alertHistory.TryGetValue(device.DeviceId, out var history))
            {
                history = new List<EdgeAlert>();
                _alertHistory[device.DeviceId] = history;
            }

            history.Add(alert);

            // Keep only recent history (last 100 alerts per device)
            if (history.Count > 100)
            {
                history.RemoveRange(0, history.Count - 100);
            }

            _logger.LogInformation("Processed alert from device {DeviceId}: {Severity} - {Message}",
                device.DeviceId, alert.Severity, alert.Message);
        }

        /// <summary>
        /// Updates device state
        /// </summary>
        public async Task UpdateDeviceStateAsync(
            EdgeDevice device,
            DeviceState state,
            CancellationToken cancellationToken = default)
        {
            device.HasNetworkConnectivity = state.IsOnline;
            device.LastSeen = DateTime.UtcNow;

            // Update capabilities if provided
            if (state.Status != "Unknown")
            {
                device.Capabilities.Add(state.Status);
                device.Capabilities = device.Capabilities.Distinct().ToList();
            }

            _devices[device.DeviceId] = device;

            _logger.LogDebug("Updated state for device {DeviceId}: {Status}", device.DeviceId, state.Status);
        }

        /// <summary>
        /// Updates metrics for device
        /// </summary>
        public async Task UpdateMetricsAsync(
            EdgeDevice device,
            EdgeMetrics metrics,
            CancellationToken cancellationToken = default)
        {
            if (!_metricsHistory.TryGetValue(device.DeviceId, out var history))
            {
                history = new List<EdgeMetrics>();
                _metricsHistory[device.DeviceId] = history;
            }

            history.Add(metrics);

            // Keep only recent history (last 500 metrics per device)
            if (history.Count > 500)
            {
                history.RemoveRange(0, history.Count - 500);
            }

            _logger.LogDebug("Updated metrics for device {DeviceId}: {ExecutionTimeMs}ms execution time",
                device.DeviceId, metrics.ExecutionTimeMs);
        }

        /// <summary>
        /// Gets device by ID
        /// </summary>
        public EdgeDevice? GetDevice(string deviceId)
        {
            return _devices.TryGetValue(deviceId, out var device) ? device : null;
        }

        /// <summary>
        /// Gets all registered devices
        /// </summary>
        public List<EdgeDevice> GetAllDevices()
        {
            return _devices.Values.ToList();
        }

        /// <summary>
        /// Gets sensor history for device
        /// </summary>
        public List<SensorDataPoint> GetSensorHistory(string deviceId, TimeSpan period)
        {
            if (!_sensorHistory.TryGetValue(deviceId, out var history))
            {
                return new List<SensorDataPoint>();
            }

            var cutoff = DateTime.UtcNow - period;
            return history.Where(h => h.Timestamp >= cutoff).ToList();
        }

        /// <summary>
        /// Gets alert history for device
        /// </summary>
        public List<EdgeAlert> GetAlertHistory(string deviceId, TimeSpan period)
        {
            if (!_alertHistory.TryGetValue(deviceId, out var history))
            {
                return new List<EdgeAlert>();
            }

            var cutoff = DateTime.UtcNow - period;
            return history.Where(h => h.Timestamp >= cutoff).ToList();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _devices.Clear();
            _sensorHistory.Clear();
            _alertHistory.Clear();
            _metricsHistory.Clear();

            _disposed = true;
        }
    }

    // Supporting classes
    public class SyncQueue : IDisposable
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public EdgeSyncConfig Config { get; set; } = new();
        public List<SyncQueueItem> PendingItems { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastConfigUpdate { get; set; } = DateTime.UtcNow;
        public DateTime? LastSyncSuccess { get; set; }
        public DateTime? LastSyncFailure { get; set; }
        public string LastError { get; set; } = string.Empty;
        private bool _disposed;

        public long GetTotalDataSize()
        {
            return PendingItems.Sum(item => item.DataSize);
        }

        public void Dispose()
        {
            if (_disposed) return;
            PendingItems.Clear();
            _disposed = true;
        }
    }

    public class SyncQueueItem
    {
        public string Id { get; set; } = string.Empty;
        public EdgeExecutionResults Data { get; set; } = new();
        public int RetryCount { get; set; }
        public DateTime FirstAttempt { get; set; } = DateTime.UtcNow;
        public DateTime NextRetry { get; set; } = DateTime.UtcNow;
        public long DataSize => CalculateDataSize();

        private long CalculateDataSize()
        {
            // Estimate data size
            return Data.Executions.Count * 1000; // Approximate bytes
        }
    }

    public class SyncResult
    {
        public string WorkflowId { get; set; } = string.Empty;
        public SyncStatus Status { get; set; }
        public int RecordCount { get; set; }
        public long DataSize { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime CompletedAt { get; set; }
        public string? Error { get; set; }
    }

    public enum SyncStatus
    {
        Pending,
        InProgress,
        Success,
        Failed,
        Partial
    }

    public class CloudResponse
    {
        public bool Success { get; set; }
        public int RecordCount { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    public class AlertEscalation
    {
        public string DeviceId { get; set; } = string.Empty;
        public string AlertId { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object> Context { get; set; } = new();
        public DateTime EscalatedAt { get; set; } = DateTime.UtcNow;
    }

    public class EdgeEncryptionService : IDisposable
    {
        private readonly EdgeConfiguration _config;

        public EdgeEncryptionService(EdgeConfiguration config)
        {
            _config = config;
        }

        public async Task<byte[]> EncryptAsync(byte[] data)
        {
            if (!_config.EnableEncryption)
            {
                return data;
            }

            // Implement AES-256 encryption
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.KeySize = 256;
            aes.GenerateKey();
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            using var output = new System.IO.MemoryStream();

            await output.WriteAsync(aes.IV); // Write IV first

            using (var cryptoStream = new System.Security.Cryptography.CryptoStream(output, encryptor, System.Security.Cryptography.CryptoStreamMode.Write))
            {
                await cryptoStream.WriteAsync(data);
            }

            return output.ToArray();
        }

        public async Task<byte[]> DecryptAsync(byte[] encryptedData)
        {
            if (!_config.EnableEncryption)
            {
                return encryptedData;
            }

            // Implement AES-256 decryption
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.KeySize = 256;

            using var input = new System.IO.MemoryStream(encryptedData);
            var iv = new byte[16];
            await input.ReadAsync(iv);
            aes.IV = iv;

            // Note: In real implementation, key would be securely managed
            aes.Key = new byte[32]; // Would be loaded from secure storage

            using var decryptor = aes.CreateDecryptor();
            using var cryptoStream = new System.Security.Cryptography.CryptoStream(input, decryptor, System.Security.Cryptography.CryptoStreamMode.Read);
            using var output = new System.IO.MemoryStream();

            await cryptoStream.CopyToAsync(output);
            return output.ToArray();
        }

        public void Dispose()
        {
            // Cleanup encryption resources
        }
    }
}
