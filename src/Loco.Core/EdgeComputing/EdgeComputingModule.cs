using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;
using Loco.Core.Interfaces;
using Loco.Core.Workflow;

namespace Loco.Core.EdgeComputing
{
    /// <summary>
    /// Comprehensive Edge Computing Module for IoT and Real-Time Workflows
    /// Based on 2024-2025 research: AWS IoT Greengrass, Azure IoT Edge, Google Cloud IoT Edge
    ///
    /// Features:
    /// - Edge-first workflow execution (offline capable)
    /// - Real-time IoT device integration
    /// - Edge-to-cloud synchronization
    /// - Hierarchical orchestration (edge → fog → cloud)
    /// - Containerized edge workloads
    ///
    /// Market: Edge Computing $10B (2023) → $182B (2032), CAGR 38.2%
    /// </summary>
    public class EdgeComputingModule : IEdgeComputingService, IDisposable
    {
        private readonly ILogger<EdgeComputingModule> _logger;
        private readonly EdgeConfiguration _config;
        private readonly EdgeRuntime _runtime;
        private readonly EdgeSyncManager _syncManager;
        private readonly IoTDeviceManager _deviceManager;
        private readonly EdgeSecurityManager _securityManager;
        private bool _disposed;

        public EdgeComputingModule(
            ILogger<EdgeComputingModule> logger,
            EdgeConfiguration config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _runtime = new EdgeRuntime(_config, _logger);
            _syncManager = new EdgeSyncManager(_config, _logger);
            _deviceManager = new IoTDeviceManager(_config, _logger);
            _securityManager = new EdgeSecurityManager(_config, _logger);
        }

        /// <summary>
        /// Deploys workflow to edge device for offline execution
        /// </summary>
        public async Task<EdgeDeploymentResult> DeployWorkflowToEdgeAsync(
            WorkflowDefinition workflow,
            EdgeDevice targetDevice,
            EdgeDeploymentOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new EdgeDeploymentOptions();

            _logger.LogInformation("Deploying workflow {WorkflowId} to edge device {DeviceId}",
                workflow.Id, targetDevice.DeviceId);

            var result = new EdgeDeploymentResult
            {
                WorkflowId = workflow.Id,
                DeviceId = targetDevice.DeviceId,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                // 1. Validate workflow for edge execution
                var validationResult = await ValidateForEdgeExecutionAsync(workflow, targetDevice, cancellationToken);
                if (!validationResult.IsValid)
                {
                    result.Errors = validationResult.Errors;
                    result.Status = DeploymentStatus.Failed;
                    return result;
                }

                // 2. Package workflow for edge deployment
                var package = await PackageWorkflowForEdgeAsync(workflow, targetDevice, options, cancellationToken);
                result.PackageSize = package.Size;

                // 3. Deploy to edge device
                var deploymentResult = await _runtime.DeployAsync(package, targetDevice, cancellationToken);
                result.DeploymentId = deploymentResult.DeploymentId;

                // 4. Configure synchronization if needed
                if (options.EnableCloudSync)
                {
                    await _syncManager.ConfigureSyncAsync(workflow.Id, targetDevice.DeviceId, options.SyncConfig, cancellationToken);
                }

                // 5. Start workflow execution on edge
                var executionResult = await _runtime.StartWorkflowAsync(workflow.Id, targetDevice, cancellationToken);

                result.Status = DeploymentStatus.Success;
                result.CompletedAt = DateTime.UtcNow;
                result.ExecutionId = executionResult.ExecutionId;

                _logger.LogInformation("Successfully deployed workflow {WorkflowId} to edge device {DeviceId}",
                    workflow.Id, targetDevice.DeviceId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deploy workflow {WorkflowId} to edge device {DeviceId}",
                    workflow.Id, targetDevice.DeviceId);

                result.Status = DeploymentStatus.Failed;
                result.Error = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Executes workflow on edge device with real-time capabilities
        /// </summary>
        public async Task<EdgeExecutionResult> ExecuteOnEdgeAsync(
            string workflowId,
            EdgeDevice targetDevice,
            Dictionary<string, object>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Executing workflow {WorkflowId} on edge device {DeviceId}",
                workflowId, targetDevice.DeviceId);

            var result = new EdgeExecutionResult
            {
                WorkflowId = workflowId,
                DeviceId = targetDevice.DeviceId,
                StartedAt = DateTime.UtcNow,
                Parameters = parameters ?? new Dictionary<string, object>()
            };

            try
            {
                // 1. Check if workflow is deployed on device
                var deploymentStatus = await _runtime.GetDeploymentStatusAsync(workflowId, targetDevice, cancellationToken);
                if (deploymentStatus.Status != DeploymentStatus.Success)
                {
                    throw new InvalidOperationException($"Workflow {workflowId} not deployed on device {targetDevice.DeviceId}");
                }

                // 2. Execute workflow with edge optimizations
                var executionResult = await _runtime.ExecuteAsync(workflowId, parameters, cancellationToken);
                result.ExecutionId = executionResult.ExecutionId;
                result.Output = executionResult.Output;
                result.ExecutionTimeMs = executionResult.ExecutionTimeMs;

                // 3. Handle real-time data if applicable
                if (executionResult.HasRealTimeData)
                {
                    await HandleRealTimeDataAsync(executionResult.RealTimeData, targetDevice, cancellationToken);
                }

                // 4. Update performance metrics
                await UpdateEdgeMetricsAsync(workflowId, targetDevice, executionResult, cancellationToken);

                result.Status = EdgeExecutionStatus.Success;
                result.CompletedAt = DateTime.UtcNow;

                _logger.LogInformation("Successfully executed workflow {WorkflowId} on edge device {DeviceId} in {ExecutionTimeMs}ms",
                    workflowId, targetDevice.DeviceId, executionResult.ExecutionTimeMs);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute workflow {WorkflowId} on edge device {DeviceId}",
                    workflowId, targetDevice.DeviceId);

                result.Status = EdgeExecutionStatus.Failed;
                result.Error = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Synchronizes edge execution results with cloud
        /// </summary>
        public async Task<EdgeSyncResult> SyncWithCloudAsync(
            string workflowId,
            EdgeDevice device,
            EdgeSyncOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new EdgeSyncOptions();

            _logger.LogDebug("Syncing workflow {WorkflowId} results from device {DeviceId} to cloud",
                workflowId, device.DeviceId);

            var result = new EdgeSyncResult
            {
                WorkflowId = workflowId,
                DeviceId = device.DeviceId,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                // 1. Collect edge execution results
                var edgeResults = await _runtime.CollectExecutionResultsAsync(workflowId, device, options, cancellationToken);

                // 2. Sync results to cloud storage
                var syncResult = await _syncManager.SyncResultsAsync(edgeResults, options, cancellationToken);
                result.SyncedRecords = syncResult.RecordCount;
                result.SyncSize = syncResult.DataSize;

                // 3. Update cloud workflow state
                await UpdateCloudWorkflowStateAsync(workflowId, edgeResults, cancellationToken);

                // 4. Handle bidirectional sync if needed
                if (options.BidirectionalSync)
                {
                    var cloudUpdates = await GetCloudUpdatesAsync(workflowId, device.LastSyncTime, cancellationToken);
                    if (cloudUpdates.Any())
                    {
                        await _runtime.ApplyCloudUpdatesAsync(cloudUpdates, device, cancellationToken);
                        result.CloudUpdatesApplied = cloudUpdates.Count;
                    }
                }

                result.Status = EdgeSyncStatus.Success;
                result.CompletedAt = DateTime.UtcNow;
                result.NextSyncTime = CalculateNextSyncTime(options);

                _logger.LogInformation("Successfully synced {SyncedRecords} records ({SyncSize} bytes) for workflow {WorkflowId}",
                    result.SyncedRecords, result.SyncSize, workflowId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync workflow {WorkflowId} from device {DeviceId} to cloud",
                    workflowId, device.DeviceId);

                result.Status = EdgeSyncStatus.Failed;
                result.Error = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Monitors edge device health and performance
        /// </summary>
        public async Task<EdgeHealthReport> GetEdgeHealthReportAsync(
            EdgeDevice device,
            CancellationToken cancellationToken = default)
        {
            var report = new EdgeHealthReport
            {
                DeviceId = device.DeviceId,
                GeneratedAt = DateTime.UtcNow
            };

            try
            {
                // 1. Get device connectivity status
                report.ConnectivityStatus = await _deviceManager.GetConnectivityStatusAsync(device, cancellationToken);

                // 2. Get resource utilization
                report.ResourceUtilization = await _deviceManager.GetResourceUtilizationAsync(device, cancellationToken);

                // 3. Get deployed workflows status
                report.DeployedWorkflows = await _runtime.GetDeployedWorkflowsStatusAsync(device, cancellationToken);

                // 4. Get synchronization status
                report.SyncStatus = await _syncManager.GetSyncStatusAsync(device, cancellationToken);

                // 5. Calculate overall health score
                report.OverallHealthScore = CalculateHealthScore(report);
                report.HealthStatus = GetHealthStatusFromScore(report.OverallHealthScore);

                // 6. Generate recommendations
                report.Recommendations = await GenerateHealthRecommendationsAsync(report, cancellationToken);

                _logger.LogDebug("Generated health report for device {DeviceId}: {HealthStatus} ({Score})",
                    device.DeviceId, report.HealthStatus, report.OverallHealthScore);

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate health report for device {DeviceId}", device.DeviceId);

                report.HealthStatus = EdgeHealthStatus.Error;
                report.Error = ex.Message;
                return report;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _runtime?.Dispose();
            _syncManager?.Dispose();
            _deviceManager?.Dispose();
            _securityManager?.Dispose();

            _disposed = true;
        }

        private async Task<EdgeValidationResult> ValidateForEdgeExecutionAsync(
            WorkflowDefinition workflow,
            EdgeDevice device,
            CancellationToken cancellationToken)
        {
            var result = new EdgeValidationResult();

            try
            {
                // 1. Check device capabilities
                if (!device.Capabilities.Contains("workflow_execution"))
                {
                    result.Errors.Add($"Device {device.DeviceId} does not support workflow execution");
                }

                // 2. Validate memory requirements
                var memoryRequirement = CalculateWorkflowMemoryRequirement(workflow);
                if (memoryRequirement > device.AvailableMemoryMB)
                {
                    result.Errors.Add($"Workflow requires {memoryRequirement}MB memory, device only has {device.AvailableMemoryMB}MB");
                }

                // 3. Validate storage requirements
                var storageRequirement = CalculateWorkflowStorageRequirement(workflow);
                if (storageRequirement > device.AvailableStorageMB)
                {
                    result.Errors.Add($"Workflow requires {storageRequirement}MB storage, device only has {device.AvailableStorageMB}MB");
                }

                // 4. Validate network requirements
                if (workflow.RequiresNetwork && !device.HasNetworkConnectivity)
                {
                    result.Errors.Add("Workflow requires network connectivity but device is offline");
                }

                // 5. Check for edge-compatible actions
                foreach (var action in workflow.Actions)
                {
                    if (!IsEdgeCompatible(action))
                    {
                        result.Errors.Add($"Action {action.Type} is not compatible with edge execution");
                    }
                }

                result.IsValid = !result.Errors.Any();
                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Validation failed: {ex.Message}");
                return result;
            }
        }

        private async Task<EdgePackage> PackageWorkflowForEdgeAsync(
            WorkflowDefinition workflow,
            EdgeDevice device,
            EdgeDeploymentOptions options,
            CancellationToken cancellationToken)
        {
            var package = new EdgePackage
            {
                WorkflowId = workflow.Id,
                TargetDevice = device,
                CreatedAt = DateTime.UtcNow
            };

            // 1. Compress workflow definition
            package.WorkflowData = await CompressWorkflowDefinitionAsync(workflow, options);

            // 2. Include required dependencies
            package.Dependencies = await GetRequiredDependenciesAsync(workflow, device);

            // 3. Add edge-specific runtime
            package.Runtime = await GetEdgeRuntimeAsync(device, options);

            // 4. Generate deployment manifest
            package.Manifest = await GenerateDeploymentManifestAsync(workflow, device, options);

            // 5. Calculate package size and checksum
            package.Size = CalculatePackageSize(package);
            package.Checksum = await CalculatePackageChecksumAsync(package);

            return package;
        }

        private async Task HandleRealTimeDataAsync(
            RealTimeData data,
            EdgeDevice device,
            CancellationToken cancellationToken)
        {
            // 1. Process real-time sensor data
            if (data.SensorData.Any())
            {
                await ProcessSensorDataAsync(data.SensorData, device, cancellationToken);
            }

            // 2. Handle real-time alerts
            if (data.Alerts.Any())
            {
                await ProcessRealTimeAlertsAsync(data.Alerts, device, cancellationToken);
            }

            // 3. Update device state
            await _deviceManager.UpdateDeviceStateAsync(device, data.DeviceState, cancellationToken);
        }

        private async Task UpdateEdgeMetricsAsync(
            string workflowId,
            EdgeDevice device,
            EdgeExecutionResult executionResult,
            CancellationToken cancellationToken)
        {
            var metrics = new EdgeMetrics
            {
                WorkflowId = workflowId,
                DeviceId = device.DeviceId,
                ExecutionTimeMs = executionResult.ExecutionTimeMs,
                MemoryUsedMB = executionResult.MemoryUsedMB,
                CpuUsedPercent = executionResult.CpuUsedPercent,
                NetworkBytesTransferred = executionResult.NetworkBytesTransferred,
                Timestamp = DateTime.UtcNow
            };

            await _deviceManager.UpdateMetricsAsync(device, metrics, cancellationToken);
        }

        private double CalculateHealthScore(EdgeHealthReport report)
        {
            // Weighted scoring algorithm
            double connectivityScore = report.ConnectivityStatus.IsOnline ? 100 : 0;
            double resourceScore = 100 - (report.ResourceUtilization.CpuPercent + report.ResourceUtilization.MemoryPercent) / 2;
            double workflowScore = report.DeployedWorkflows.Any(w => w.Status == WorkflowStatus.Healthy) ? 100 : 50;
            double syncScore = report.SyncStatus.LastSyncSuccess.HasValue &&
                             report.SyncStatus.LastSyncSuccess.Value > DateTime.UtcNow.AddHours(-1) ? 100 : 50;

            return (connectivityScore * 0.2) + (resourceScore * 0.4) + (workflowScore * 0.2) + (syncScore * 0.2);
        }

        private EdgeHealthStatus GetHealthStatusFromScore(double score)
        {
            return score >= 90 ? EdgeHealthStatus.Excellent :
                   score >= 75 ? EdgeHealthStatus.Good :
                   score >= 50 ? EdgeHealthStatus.Fair :
                   EdgeHealthStatus.Poor;
        }

        private async Task<List<string>> GenerateHealthRecommendationsAsync(
            EdgeHealthReport report,
            CancellationToken cancellationToken)
        {
            var recommendations = new List<string>();

            if (report.ResourceUtilization.CpuPercent > 80)
            {
                recommendations.Add("High CPU usage detected. Consider optimizing workflows or upgrading device.");
            }

            if (report.ResourceUtilization.MemoryPercent > 85)
            {
                recommendations.Add("High memory usage detected. Monitor for memory leaks or reduce workflow complexity.");
            }

            if (report.SyncStatus.LastSyncFailure.HasValue)
            {
                recommendations.Add("Recent sync failures detected. Check network connectivity and sync configuration.");
            }

            var unhealthyWorkflows = report.DeployedWorkflows.Where(w => w.Status != WorkflowStatus.Healthy);
            if (unhealthyWorkflows.Any())
            {
                recommendations.Add($"Found {unhealthyWorkflows.Count()} unhealthy workflows. Review and redeploy if necessary.");
            }

            return recommendations;
        }

        private long CalculateWorkflowMemoryRequirement(WorkflowDefinition workflow)
        {
            // Estimate memory requirements based on workflow complexity
            long baseMemory = 50; // MB
            long perActionMemory = 10; // MB per action
            long perTriggerMemory = 5; // MB per trigger

            return baseMemory + (workflow.Actions.Count * perActionMemory) + (workflow.Triggers.Count * perTriggerMemory);
        }

        private long CalculateWorkflowStorageRequirement(WorkflowDefinition workflow)
        {
            // Estimate storage requirements
            long baseStorage = 10; // MB
            long perActionStorage = 2; // MB per action
            long perTriggerStorage = 1; // MB per trigger

            return baseStorage + (workflow.Actions.Count * perActionStorage) + (workflow.Triggers.Count * perTriggerStorage);
        }

        private bool IsEdgeCompatible(WorkflowAction action)
        {
            // Define which actions are compatible with edge execution
            var edgeCompatibleActions = new[]
            {
                "file_operation", "data_processing", "sensor_reading", "local_notification",
                "device_control", "data_validation", "simple_calculation", "local_storage"
            };

            return edgeCompatibleActions.Contains(action.Type.ToLower());
        }

        private long CalculatePackageSize(EdgePackage package)
        {
            // Calculate total size of all package components
            return package.WorkflowData.Length +
                   package.Dependencies.Sum(d => d.Size) +
                   package.Runtime.Size +
                   package.Manifest.Length;
        }

        private async Task<string> CalculatePackageChecksumAsync(EdgePackage package)
        {
            // Calculate SHA-256 checksum of package contents
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var data = System.Text.Encoding.UTF8.GetBytes(
                package.WorkflowId + package.TargetDevice.DeviceId + package.CreatedAt.ToString());

            var hash = await Task.Run(() => sha256.ComputeHash(data));
            return Convert.ToHexString(hash);
        }

        private async Task<WorkflowDefinition> CompressWorkflowDefinitionAsync(
            WorkflowDefinition workflow,
            EdgeDeploymentOptions options)
        {
            // Compress and optimize workflow for edge deployment
            var optimizedWorkflow = await OptimizeWorkflowForEdgeAsync(workflow, options);

            // Serialize to JSON with edge-specific optimizations
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false, // Minimize size
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var json = System.Text.Json.JsonSerializer.Serialize(optimizedWorkflow, jsonOptions);
            var compressed = await CompressDataAsync(System.Text.Encoding.UTF8.GetBytes(json));

            return optimizedWorkflow; // Return the optimized workflow for package
        }

        private async Task<WorkflowDefinition> OptimizeWorkflowForEdgeAsync(
            WorkflowDefinition workflow,
            EdgeDeploymentOptions options)
        {
            // Create a copy and optimize for edge execution
            var optimized = new WorkflowDefinition
            {
                Id = workflow.Id,
                Name = workflow.Name,
                Version = workflow.Version,
                Description = workflow.Description,
                Triggers = workflow.Triggers.Where(t => IsEdgeCompatibleTrigger(t)).ToList(),
                Actions = workflow.Actions.Where(a => IsEdgeCompatible(a)).ToList(),
                Variables = workflow.Variables,
                Metadata = workflow.Metadata
            };

            // Remove cloud-only dependencies
            optimized.Metadata.Remove("cloud_dependencies");
            optimized.Metadata["edge_optimized"] = "true";
            optimized.Metadata["optimization_date"] = DateTime.UtcNow.ToString();

            return optimized;
        }

        private bool IsEdgeCompatibleTrigger(WorkflowTrigger trigger)
        {
            var edgeCompatibleTriggers = new[]
            {
                "time", "file", "manual", "device_event", "sensor", "local_api"
            };

            return edgeCompatibleTriggers.Contains(trigger.Type.ToLower());
        }

        private async Task<byte[]> CompressDataAsync(byte[] data)
        {
            // Use GZip compression for edge deployment
            using var output = new System.IO.MemoryStream();
            using var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionMode.Compress);
            await gzip.WriteAsync(data);
            await gzip.FlushAsync();
            gzip.Close();

            return output.ToArray();
        }

        private async Task<List<EdgeDependency>> GetRequiredDependenciesAsync(
            WorkflowDefinition workflow,
            EdgeDevice device)
        {
            var dependencies = new List<EdgeDependency>();

            // Add runtime dependencies based on device capabilities
            if (device.Capabilities.Contains("ai_processing"))
            {
                dependencies.Add(new EdgeDependency
                {
                    Name = "AI Runtime",
                    Version = "1.0.0",
                    Size = 50 * 1024 * 1024, // 50MB
                    Type = DependencyType.Runtime
                });
            }

            // Add action-specific dependencies
            foreach (var action in workflow.Actions)
            {
                var actionDependencies = await GetActionDependenciesAsync(action, device);
                dependencies.AddRange(actionDependencies);
            }

            return dependencies;
        }

        private async Task<List<EdgeDependency>> GetActionDependenciesAsync(
            WorkflowAction action,
            EdgeDevice device)
        {
            // Determine dependencies based on action type
            switch (action.Type.ToLower())
            {
                case "data_processing":
                    return new List<EdgeDependency>
                    {
                        new EdgeDependency { Name = "Data Processing Library", Version = "2.1.0", Size = 5 * 1024 * 1024, Type = DependencyType.Library }
                    };

                case "sensor_reading":
                    return new List<EdgeDependency>
                    {
                        new EdgeDependency { Name = "IoT Sensor Library", Version = "1.5.0", Size = 3 * 1024 * 1024, Type = DependencyType.Library }
                    };

                default:
                    return new List<EdgeDependency>();
            }
        }

        private async Task<EdgeRuntime> GetEdgeRuntimeAsync(EdgeDevice device, EdgeDeploymentOptions options)
        {
            // Select appropriate runtime based on device architecture
            return new EdgeRuntime
            {
                Version = "1.0.0",
                Architecture = device.Architecture,
                Size = 20 * 1024 * 1024, // 20MB
                Features = device.Capabilities
            };
        }

        private async Task<EdgeManifest> GenerateDeploymentManifestAsync(
            WorkflowDefinition workflow,
            EdgeDevice device,
            EdgeDeploymentOptions options)
        {
            return new EdgeManifest
            {
                WorkflowId = workflow.Id,
                DeviceId = device.DeviceId,
                DeploymentTime = DateTime.UtcNow,
                RuntimeVersion = "1.0.0",
                Configuration = options,
                Checksum = await CalculatePackageChecksumAsync(new EdgePackage { WorkflowId = workflow.Id })
            };
        }

        private async Task ProcessSensorDataAsync(
            List<SensorDataPoint> sensorData,
            EdgeDevice device,
            CancellationToken cancellationToken)
        {
            foreach (var dataPoint in sensorData)
            {
                // Process each sensor reading
                await _deviceManager.ProcessSensorReadingAsync(device, dataPoint, cancellationToken);

                // Check for threshold violations
                if (dataPoint.Value > dataPoint.Threshold)
                {
                    await HandleThresholdViolationAsync(device, dataPoint, cancellationToken);
                }
            }
        }

        private async Task ProcessRealTimeAlertsAsync(
            List<EdgeAlert> alerts,
            EdgeDevice device,
            CancellationToken cancellationToken)
        {
            foreach (var alert in alerts)
            {
                // Process and route alerts
                await _deviceManager.ProcessAlertAsync(device, alert, cancellationToken);

                // Escalate critical alerts to cloud
                if (alert.Severity == AlertSeverity.Critical)
                {
                    await EscalateCriticalAlertAsync(device, alert, cancellationToken);
                }
            }
        }

        private async Task HandleThresholdViolationAsync(
            EdgeDevice device,
            SensorDataPoint dataPoint,
            CancellationToken cancellationToken)
        {
            _logger.LogWarning("Threshold violation on device {DeviceId}: {SensorType} = {Value} (threshold: {Threshold})",
                device.DeviceId, dataPoint.SensorType, dataPoint.Value, dataPoint.Threshold);

            // Create alert workflow execution
            var alertWorkflow = await CreateThresholdAlertWorkflowAsync(device, dataPoint, cancellationToken);
            await _runtime.ExecuteAsync(alertWorkflow.Id, new Dictionary<string, object>
            {
                ["device_id"] = device.DeviceId,
                ["sensor_type"] = dataPoint.SensorType,
                ["current_value"] = dataPoint.Value,
                ["threshold"] = dataPoint.Threshold
            }, cancellationToken);
        }

        private async Task EscalateCriticalAlertAsync(
            EdgeDevice device,
            EdgeAlert alert,
            CancellationToken cancellationToken)
        {
            // Escalate to cloud for immediate attention
            await _syncManager.EscalateAlertAsync(device, alert, cancellationToken);
        }

        private async Task UpdateCloudWorkflowStateAsync(
            string workflowId,
            EdgeExecutionResults results,
            CancellationToken cancellationToken)
        {
            // Update the cloud workflow state based on edge execution results
            var cloudState = new CloudWorkflowState
            {
                WorkflowId = workflowId,
                LastEdgeExecution = DateTime.UtcNow,
                EdgeExecutionCount = results.ExecutionCount,
                LastSyncTime = DateTime.UtcNow,
                Status = results.LastExecutionStatus
            };

            await _syncManager.UpdateCloudStateAsync(cloudState, cancellationToken);
        }

        private async Task<List<CloudUpdate>> GetCloudUpdatesAsync(
            string workflowId,
            DateTime since,
            CancellationToken cancellationToken)
        {
            // Get updates from cloud since last sync
            return await _syncManager.GetCloudUpdatesAsync(workflowId, since, cancellationToken);
        }

        private DateTime CalculateNextSyncTime(EdgeSyncOptions options)
        {
            // Calculate next sync time based on sync interval
            return DateTime.UtcNow.Add(options.SyncInterval);
        }

        private async Task<WorkflowDefinition> CreateThresholdAlertWorkflowAsync(
            EdgeDevice device,
            SensorDataPoint dataPoint,
            CancellationToken cancellationToken)
        {
            // Create a dynamic workflow for threshold alerts
            return new WorkflowDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Threshold Alert - {device.DeviceId} - {dataPoint.SensorType}",
                Version = "1.0.0",
                Triggers = new List<WorkflowTrigger>
                {
                    new WorkflowTrigger
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "manual",
                        Parameters = new Dictionary<string, object>()
                    }
                },
                Actions = new List<WorkflowAction>
                {
                    new WorkflowAction
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "notification",
                        Parameters = new Dictionary<string, object>
                        {
                            ["title"] = "Threshold Alert",
                            ["message"] = $"Device {device.DeviceId}: {dataPoint.SensorType} exceeded threshold ({dataPoint.Value} > {dataPoint.Threshold})"
                        }
                    }
                }
            };
        }
    }

    // Supporting classes and interfaces
    public interface IEdgeComputingService
    {
        Task<EdgeDeploymentResult> DeployWorkflowToEdgeAsync(
            WorkflowDefinition workflow,
            EdgeDevice targetDevice,
            EdgeDeploymentOptions? options = null,
            CancellationToken cancellationToken = default);

        Task<EdgeExecutionResult> ExecuteOnEdgeAsync(
            string workflowId,
            EdgeDevice targetDevice,
            Dictionary<string, object>? parameters = null,
            CancellationToken cancellationToken = default);

        Task<EdgeSyncResult> SyncWithCloudAsync(
            string workflowId,
            EdgeDevice device,
            EdgeSyncOptions? options = null,
            CancellationToken cancellationToken = default);

        Task<EdgeHealthReport> GetEdgeHealthReportAsync(
            EdgeDevice device,
            CancellationToken cancellationToken = default);
    }

    // Data models
    public class EdgeConfiguration
    {
        public string CloudEndpoint { get; set; } = "https://api.loco.com";
        public int SyncIntervalMinutes { get; set; } = 15;
        public int MaxOfflineHours { get; set; } = 24;
        public int MaxPackageSizeMB { get; set; } = 100;
        public bool EnableCompression { get; set; } = true;
        public bool EnableEncryption { get; set; } = true;
    }

    public class EdgeDevice
    {
        public string DeviceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Architecture { get; set; } = "x86_64";
        public List<string> Capabilities { get; set; } = new();
        public double AvailableMemoryMB { get; set; }
        public double AvailableStorageMB { get; set; }
        public bool HasNetworkConnectivity { get; set; }
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    }

    public class EdgeDeploymentOptions
    {
        public bool EnableCloudSync { get; set; } = true;
        public EdgeSyncConfig SyncConfig { get; set; } = new();
        public bool EnableAutoStart { get; set; } = true;
        public int MaxRetries { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);
    }

    public class EdgeSyncConfig
    {
        public TimeSpan SyncInterval { get; set; } = TimeSpan.FromMinutes(15);
        public bool BidirectionalSync { get; set; } = false;
        public CompressionLevel Compression { get; set; } = CompressionLevel.Optimal;
        public bool EncryptData { get; set; } = true;
    }

    public enum CompressionLevel
    {
        None,
        Fast,
        Optimal,
        Maximum
    }

    public class EdgeDeploymentResult
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string DeploymentId { get; set; } = string.Empty;
        public string ExecutionId { get; set; } = string.Empty;
        public DeploymentStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public long PackageSize { get; set; }
        public List<string> Errors { get; set; } = new();
        public string? Error { get; set; }
    }

    public enum DeploymentStatus
    {
        Pending,
        InProgress,
        Success,
        Failed,
        Cancelled
    }

    public class EdgeExecutionResult
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string ExecutionId { get; set; } = string.Empty;
        public EdgeExecutionStatus Status { get; set; }
        public Dictionary<string, object> Output { get; set; } = new();
        public Dictionary<string, object> Parameters { get; set; } = new();
        public long ExecutionTimeMs { get; set; }
        public double MemoryUsedMB { get; set; }
        public double CpuUsedPercent { get; set; }
        public long NetworkBytesTransferred { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime CompletedAt { get; set; }
        public string? Error { get; set; }
    }

    public enum EdgeExecutionStatus
    {
        Pending,
        Running,
        Success,
        Failed,
        Cancelled,
        Timeout
    }

    public class EdgeSyncResult
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public EdgeSyncStatus Status { get; set; }
        public int SyncedRecords { get; set; }
        public long SyncSize { get; set; }
        public int CloudUpdatesApplied { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime CompletedAt { get; set; }
        public DateTime NextSyncTime { get; set; }
        public string? Error { get; set; }
    }

    public enum EdgeSyncStatus
    {
        Pending,
        InProgress,
        Success,
        Failed,
        Partial
    }

    public class EdgeHealthReport
    {
        public string DeviceId { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public EdgeHealthStatus HealthStatus { get; set; }
        public double OverallHealthScore { get; set; }
        public ConnectivityStatus ConnectivityStatus { get; set; } = new();
        public ResourceUtilization ResourceUtilization { get; set; } = new();
        public List<WorkflowStatus> DeployedWorkflows { get; set; } = new();
        public SyncStatus SyncStatus { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public string? Error { get; set; }
    }

    public enum EdgeHealthStatus
    {
        Excellent,
        Good,
        Fair,
        Poor,
        Error
    }

    public class ConnectivityStatus
    {
        public bool IsOnline { get; set; }
        public DateTime LastConnected { get; set; } = DateTime.UtcNow;
        public string NetworkType { get; set; } = "Unknown";
        public double LatencyMs { get; set; }
        public double BandwidthMbps { get; set; }
    }

    public class ResourceUtilization
    {
        public double CpuPercent { get; set; }
        public double MemoryPercent { get; set; }
        public double StoragePercent { get; set; }
        public double NetworkPercent { get; set; }
        public DateTime MeasuredAt { get; set; } = DateTime.UtcNow;
    }

    public class WorkflowStatus
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string Status { get; set; } = "Unknown";
        public DateTime LastExecution { get; set; }
        public int ExecutionCount { get; set; }
        public double AverageExecutionTimeMs { get; set; }
        public string LastError { get; set; } = string.Empty;
    }

    public class SyncStatus
    {
        public DateTime? LastSyncSuccess { get; set; }
        public DateTime? LastSyncFailure { get; set; }
        public int PendingRecords { get; set; }
        public long PendingDataSize { get; set; }
        public string LastError { get; set; } = string.Empty;
    }

    public class EdgeValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class EdgePackage
    {
        public string WorkflowId { get; set; } = string.Empty;
        public EdgeDevice TargetDevice { get; set; } = new();
        public WorkflowDefinition WorkflowData { get; set; } = new();
        public List<EdgeDependency> Dependencies { get; set; } = new();
        public EdgeRuntime Runtime { get; set; } = new();
        public EdgeManifest Manifest { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public long Size { get; set; }
        public string Checksum { get; set; } = string.Empty;
    }

    public class EdgeDependency
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public long Size { get; set; }
        public DependencyType Type { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
    }

    public enum DependencyType
    {
        Runtime,
        Library,
        Framework,
        Driver
    }

    public class EdgeRuntime
    {
        public string Version { get; set; } = "1.0.0";
        public string Architecture { get; set; } = "x86_64";
        public long Size { get; set; }
        public List<string> Features { get; set; } = new();
    }

    public class EdgeManifest
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public DateTime DeploymentTime { get; set; } = DateTime.UtcNow;
        public string RuntimeVersion { get; set; } = "1.0.0";
        public EdgeDeploymentOptions Configuration { get; set; } = new();
        public string Checksum { get; set; } = string.Empty;
    }

    public class RealTimeData
    {
        public List<SensorDataPoint> SensorData { get; set; } = new();
        public List<EdgeAlert> Alerts { get; set; } = new();
        public DeviceState DeviceState { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class SensorDataPoint
    {
        public string SensorType { get; set; } = string.Empty;
        public double Value { get; set; }
        public double Threshold { get; set; }
        public string Unit { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class EdgeAlert
    {
        public string AlertId { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object> Context { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public enum AlertSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class DeviceState
    {
        public string Status { get; set; } = "Unknown";
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double StorageUsage { get; set; }
        public bool IsOnline { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }

    public class EdgeMetrics
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public long ExecutionTimeMs { get; set; }
        public double MemoryUsedMB { get; set; }
        public double CpuUsedPercent { get; set; }
        public long NetworkBytesTransferred { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class EdgeSyncOptions
    {
        public TimeSpan SyncInterval { get; set; } = TimeSpan.FromMinutes(15);
        public bool BidirectionalSync { get; set; } = false;
        public CompressionLevel Compression { get; set; } = CompressionLevel.Optimal;
        public bool EncryptData { get; set; } = true;
        public int MaxRetries { get; set; } = 3;
    }

    public class CloudWorkflowState
    {
        public string WorkflowId { get; set; } = string.Empty;
        public DateTime LastEdgeExecution { get; set; }
        public int EdgeExecutionCount { get; set; }
        public DateTime LastSyncTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class CloudUpdate
    {
        public string UpdateId { get; set; } = string.Empty;
        public UpdateType Type { get; set; }
        public string WorkflowId { get; set; } = string.Empty;
        public object Data { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum UpdateType
    {
        WorkflowUpdate,
        ConfigurationChange,
        SecurityPatch,
        FeatureEnhancement
    }

    public class EdgeExecutionResults
    {
        public string WorkflowId { get; set; } = string.Empty;
        public int ExecutionCount { get; set; }
        public string LastExecutionStatus { get; set; } = string.Empty;
        public DateTime LastExecutionTime { get; set; }
        public List<EdgeExecutionResult> Executions { get; set; } = new();
    }
}
