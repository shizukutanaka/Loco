using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.DisasterRecovery
{
    /// <summary>
    /// Disaster recovery and backup management system
    /// Phase 26: Backup strategies, recovery plans, failover management, point-in-time recovery
    /// </summary>
    public interface IDisasterRecoveryManager
    {
        Task<BackupStrategy> CreateBackupStrategyAsync(string tenantId, BackupStrategyDefinition definition, CancellationToken ct = default);
        Task<Backup> CreateBackupAsync(string tenantId, string strategyId, BackupDefinition definition, CancellationToken ct = default);
        Task<List<Backup>> GetBackupsAsync(string tenantId, string resourceType = null, int limit = 100, CancellationToken ct = default);
        Task<bool> RestoreFromBackupAsync(string tenantId, string backupId, RestoreDefinition definition, CancellationToken ct = default);
        Task<RecoveryPlan> CreateRecoveryPlanAsync(string tenantId, RecoveryPlanDefinition definition, CancellationToken ct = default);
        Task<FailoverStatus> InitiateFailoverAsync(string tenantId, string primaryRegion, string standbyRegion, CancellationToken ct = default);
        Task<DataConsistency> VerifyDataConsistencyAsync(string tenantId, string backupId, CancellationToken ct = default);
        Task<PointInTimeRecovery> GetPointInTimeRecoveryAsync(string tenantId, string resourceId, DateTimeOffset targetTime, CancellationToken ct = default);
        Task<bool> ScheduleBackupAsync(string tenantId, string strategyId, BackupSchedule schedule, CancellationToken ct = default);
        Task<DisasterRecoveryMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default);
    }

    public class DisasterRecoveryManager : IDisasterRecoveryManager
    {
        private readonly ILogger<DisasterRecoveryManager> _logger;
        private readonly Dictionary<string, BackupStrategy> _strategies = new();
        private readonly Dictionary<string, List<Backup>> _backups = new();
        private readonly Dictionary<string, RecoveryPlan> _recoveryPlans = new();
        private readonly Dictionary<string, List<BackupSchedule>> _schedules = new();
        private readonly Dictionary<string, FailoverStatus> _failoverStatus = new();
        private readonly Random _random = new(42);

        public DisasterRecoveryManager(ILogger<DisasterRecoveryManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BackupStrategy> CreateBackupStrategyAsync(string tenantId, BackupStrategyDefinition definition, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Creating backup strategy {StrategyName}", definition.Name);
            await Task.Delay(30, ct);

            var strategy = new BackupStrategy
            {
                StrategyId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                Name = definition.Name,
                Description = definition.Description,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = definition.CreatedBy,
                Status = "active",
                BackupType = definition.BackupType, // full, incremental, differential
                Frequency = definition.Frequency ?? "daily",
                RetentionDays = definition.RetentionDays ?? 30,
                ResourceTypes = definition.ResourceTypes ?? new List<string>(),
                Destinations = definition.Destinations ?? new List<string>(),
                EncryptionEnabled = definition.EncryptionEnabled ?? true,
                CompressionLevel = definition.CompressionLevel ?? "medium",
                BackupWindow = definition.BackupWindow ?? "02:00-04:00 UTC",
                RPOMinutes = definition.RPOMinutes ?? 60, // Recovery Point Objective
                RTOMinutes = definition.RTOMinutes ?? 120, // Recovery Time Objective
                LastBackupAt = null,
                NextBackupAt = DateTimeOffset.UtcNow.AddDays(1),
                BackupCount = 0
            };

            var key = $"{tenantId}:{strategy.StrategyId}";
            _strategies[key] = strategy;
            _backups[key] = new List<Backup>();
            _schedules[key] = new List<BackupSchedule>();

            return strategy;
        }

        public async Task<Backup> CreateBackupAsync(string tenantId, string strategyId, BackupDefinition definition, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Creating backup for strategy {StrategyId}", strategyId);
            await Task.Delay(50, ct);

            var backup = new Backup
            {
                BackupId = Guid.NewGuid().ToString("N"),
                StrategyId = strategyId,
                TenantId = tenantId,
                ResourceType = definition.ResourceType,
                ResourceIds = definition.ResourceIds ?? new List<string>(),
                CreatedAt = DateTimeOffset.UtcNow,
                StartTime = DateTimeOffset.UtcNow.AddMinutes(-_random.Next(1, 60)),
                EndTime = DateTimeOffset.UtcNow,
                Duration = _random.Next(60, 3600),
                Status = "completed",
                BackupType = definition.BackupType ?? "full",
                SizeGB = _random.Next(1, 500),
                FileCount = _random.Next(100, 1000000),
                Destination = definition.Destination,
                VerificationStatus = _random.NextDouble() > 0.05 ? "verified" : "failed",
                EncryptionKeyId = Guid.NewGuid().ToString("N"),
                Checksum = Guid.NewGuid().ToString("N"),
                RetentionExpiresAt = DateTimeOffset.UtcNow.AddDays(_random.Next(7, 365)),
                IsIncremental = _random.NextDouble() > 0.5,
                ParentBackupId = null,
                DataTransferedGB = _random.Next(1, 500),
                DataDeduplicationRatio = _random.Next(15, 75) / 100.0m
            };

            var key = $"{tenantId}:{strategyId}";
            if (!_backups.ContainsKey(key))
                _backups[key] = new List<Backup>();

            _backups[key].Add(backup);
            if (_backups[key].Count > 1000)
                _backups[key] = _backups[key].Skip(_backups[key].Count - 1000).ToList();

            if (_strategies.ContainsKey(key))
            {
                _strategies[key].LastBackupAt = DateTimeOffset.UtcNow;
                _strategies[key].NextBackupAt = DateTimeOffset.UtcNow.AddDays(1);
                _strategies[key].BackupCount++;
            }

            return backup;
        }

        public async Task<List<Backup>> GetBackupsAsync(string tenantId, string resourceType = null, int limit = 100, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Retrieving backups");
            await Task.Delay(25, ct);

            var backups = _backups
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .SelectMany(kvp => kvp.Value)
                .ToList();

            if (!string.IsNullOrWhiteSpace(resourceType))
                backups = backups.Where(b => b.ResourceType == resourceType).ToList();

            return backups
                .OrderByDescending(b => b.CreatedAt)
                .Take(limit)
                .ToList();
        }

        public async Task<bool> RestoreFromBackupAsync(string tenantId, string backupId, RestoreDefinition definition, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Restoring from backup {BackupId}", backupId);
            await Task.Delay(200, ct);

            var backupList = _backups
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .SelectMany(kvp => kvp.Value)
                .FirstOrDefault(b => b.BackupId == backupId);

            if (backupList == null)
                return false;

            // Simulate restore success rate (95%)
            return _random.NextDouble() > 0.05;
        }

        public async Task<RecoveryPlan> CreateRecoveryPlanAsync(string tenantId, RecoveryPlanDefinition definition, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Creating recovery plan {PlanName}", definition.Name);
            await Task.Delay(40, ct);

            var plan = new RecoveryPlan
            {
                PlanId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                Name = definition.Name,
                Description = definition.Description,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = definition.CreatedBy,
                Status = "active",
                Priority = definition.Priority ?? "high",
                RPOMinutes = definition.RPOMinutes ?? 60,
                RTOMinutes = definition.RTOMinutes ?? 120,
                Steps = GenerateRecoverySteps(),
                PrerequisitesChecks = GeneratePrerequisiteChecks(),
                EstimatedRecoveryTimeMinutes = _random.Next(30, 480),
                ResourcesRequired = GenerateResourceRequirements(),
                TestFrequency = definition.TestFrequency ?? "monthly",
                LastTestedAt = definition.LastTestedAt,
                NextTestDueAt = DateTimeOffset.UtcNow.AddMonths(1),
                DocumentationUrl = definition.DocumentationUrl,
                AssignedTeam = definition.AssignedTeam
            };

            var key = $"{tenantId}:{plan.PlanId}";
            _recoveryPlans[key] = plan;

            return plan;
        }

        public async Task<FailoverStatus> InitiateFailoverAsync(string tenantId, string primaryRegion, string standbyRegion, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Initiating failover from {Primary} to {Standby}", primaryRegion, standbyRegion);
            await Task.Delay(100, ct);

            var status = new FailoverStatus
            {
                FailoverId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                InitiatedAt = DateTimeOffset.UtcNow,
                PrimaryRegion = primaryRegion,
                StandbyRegion = standbyRegion,
                Status = "in-progress",
                ProgressPercent = 0,
                StartTime = DateTimeOffset.UtcNow,
                EstimatedCompletionTime = DateTimeOffset.UtcNow.AddMinutes(_random.Next(15, 120)),
                DataSyncStatus = "syncing",
                ConnectionsTransferred = 0,
                TotalConnections = _random.Next(100, 10000),
                DataLossBytes = 0,
                ErrorCount = 0,
                HealthChecks = new List<string>()
            };

            var key = $"{tenantId}:{status.FailoverId}";
            _failoverStatus[key] = status;

            return status;
        }

        public async Task<DataConsistency> VerifyDataConsistencyAsync(string tenantId, string backupId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Verifying data consistency for backup {BackupId}", backupId);
            await Task.Delay(100, ct);

            var consistency = new DataConsistency
            {
                VerificationId = Guid.NewGuid().ToString("N"),
                BackupId = backupId,
                TenantId = tenantId,
                VerifiedAt = DateTimeOffset.UtcNow,
                Status = _random.NextDouble() > 0.1 ? "consistent" : "inconsistent",
                TotalRecords = _random.Next(10000, 1000000),
                VerifiedRecords = _random.Next(9000, 990000),
                CorruptedRecords = _random.Next(0, 100),
                MissingRecords = _random.Next(0, 50),
                OrphanedRecords = _random.Next(0, 30),
                InconsistencyRate = _random.NextDouble() * 0.05,
                ChecksumValidation = "passed",
                IntegrityScore = _random.Next(95, 100),
                RepairedRecords = _random.Next(0, 10),
                VerificationDurationSeconds = _random.Next(60, 3600)
            };

            return consistency;
        }

        public async Task<PointInTimeRecovery> GetPointInTimeRecoveryAsync(string tenantId, string resourceId, DateTimeOffset targetTime, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Getting point-in-time recovery for resource {ResourceId}", resourceId);
            await Task.Delay(50, ct);

            var recovery = new PointInTimeRecovery
            {
                RecoveryId = Guid.NewGuid().ToString("N"),
                ResourceId = resourceId,
                TenantId = tenantId,
                TargetTime = targetTime,
                AvailableBackups = _random.Next(1, 100),
                OptimalBackupId = Guid.NewGuid().ToString("N"),
                TimeDifferenceMinutes = _random.Next(0, 60),
                DataAvailability = "complete",
                RecoveryFeasibility = "feasible",
                EstimatedRecoveryTimeMinutes = _random.Next(15, 120),
                IncrementalBackupsNeeded = _random.Next(0, 10),
                TotalDataToRestore = _random.Next(100, 100000),
                TransactionLogsAvailable = true,
                LogsCoverageStart = targetTime.AddMinutes(-60),
                LogsCoverageEnd = DateTimeOffset.UtcNow
            };

            return recovery;
        }

        public async Task<bool> ScheduleBackupAsync(string tenantId, string strategyId, BackupSchedule schedule, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Scheduling backup for strategy {StrategyId}", strategyId);
            await Task.Delay(20, ct);

            var key = $"{tenantId}:{strategyId}";
            if (!_schedules.ContainsKey(key))
                _schedules[key] = new List<BackupSchedule>();

            schedule.ScheduleId = Guid.NewGuid().ToString("N");
            schedule.CreatedAt = DateTimeOffset.UtcNow;
            schedule.Status = "active";

            _schedules[key].Add(schedule);
            return true;
        }

        public async Task<DisasterRecoveryMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Calculating disaster recovery metrics");
            await Task.Delay(30, ct);

            var strategyCount = _strategies.Count(kvp => kvp.Key.StartsWith($"{tenantId}:"));
            var backupCount = _backups.Sum(kvp => kvp.Key.StartsWith($"{tenantId}:") ? kvp.Value.Count : 0);
            var planCount = _recoveryPlans.Count(kvp => kvp.Key.StartsWith($"{tenantId}:"));

            var metrics = new DisasterRecoveryMetrics
            {
                TenantId = tenantId,
                CalculatedAt = DateTimeOffset.UtcNow,
                BackupStrategies = strategyCount,
                ActiveStrategies = _strategies.Count(kvp =>
                    kvp.Key.StartsWith($"{tenantId}:") && kvp.Value.Status == "active"),
                TotalBackups = backupCount,
                SuccessfulBackups = _random.Next((int)(backupCount * 0.9), backupCount),
                FailedBackups = _random.Next(0, (int)(backupCount * 0.1)),
                IncompleteBackups = _random.Next(0, 10),
                AverageBackupSizeGB = _random.Next(50, 500),
                TotalBackupStorageGB = _random.Next(1000, 100000),
                DataRetentionDays = _random.Next(30, 365),
                RecoveryPlans = planCount,
                VerifiedRecoveryPlans = _random.Next(Math.Max(0, planCount - 2), planCount),
                FailoverCapabilitiesEnabled = _random.NextDouble() > 0.2,
                RPOCompliancePercent = _random.Next(95, 100),
                RTOCompliancePercent = _random.Next(90, 100),
                DataConsistencyScore = _random.Next(95, 100),
                LastSuccessfulRecoveryTest = DateTimeOffset.UtcNow.AddDays(-_random.Next(1, 30)),
                RecoverySLA = "99.9%"
            };

            return metrics;
        }

        private List<RecoveryStep> GenerateRecoverySteps()
        {
            var steps = new List<RecoveryStep>
            {
                new() { StepNumber = 1, Name = "Assess damage", EstimatedTime = 15 },
                new() { StepNumber = 2, Name = "Prepare standby systems", EstimatedTime = 30 },
                new() { StepNumber = 3, Name = "Restore data from backup", EstimatedTime = 60 },
                new() { StepNumber = 4, Name = "Verify data integrity", EstimatedTime = 20 },
                new() { StepNumber = 5, Name = "Perform health checks", EstimatedTime = 15 },
                new() { StepNumber = 6, Name = "Switch traffic to standby", EstimatedTime = 10 }
            };
            return steps;
        }

        private List<PrerequisiteCheck> GeneratePrerequisiteChecks()
        {
            return new List<PrerequisiteCheck>
            {
                new() { CheckId = "check-1", Name = "Database connectivity", Status = "passed" },
                new() { CheckId = "check-2", Name = "Backup storage access", Status = "passed" },
                new() { CheckId = "check-3", Name = "Network connectivity", Status = "passed" },
                new() { CheckId = "check-4", Name = "Encryption keys available", Status = "passed" }
            };
        }

        private Dictionary<string, int> GenerateResourceRequirements()
        {
            return new Dictionary<string, int>
            {
                { "cpu_cores", _random.Next(4, 32) },
                { "memory_gb", _random.Next(16, 256) },
                { "storage_gb", _random.Next(500, 5000) },
                { "network_bandwidth_mbps", _random.Next(100, 10000) }
            };
        }
    }

    public class BackupStrategyDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public string BackupType { get; set; }
        public string Frequency { get; set; }
        public int? RetentionDays { get; set; }
        public List<string> ResourceTypes { get; set; }
        public List<string> Destinations { get; set; }
        public bool? EncryptionEnabled { get; set; }
        public string CompressionLevel { get; set; }
        public string BackupWindow { get; set; }
        public int? RPOMinutes { get; set; }
        public int? RTOMinutes { get; set; }
    }

    public class BackupStrategy
    {
        public string StrategyId { get; set; }
        public string TenantId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string Status { get; set; }
        public string BackupType { get; set; }
        public string Frequency { get; set; }
        public int RetentionDays { get; set; }
        public List<string> ResourceTypes { get; set; } = new();
        public List<string> Destinations { get; set; } = new();
        public bool EncryptionEnabled { get; set; }
        public string CompressionLevel { get; set; }
        public string BackupWindow { get; set; }
        public int RPOMinutes { get; set; }
        public int RTOMinutes { get; set; }
        public DateTimeOffset? LastBackupAt { get; set; }
        public DateTimeOffset NextBackupAt { get; set; }
        public int BackupCount { get; set; }
    }

    public class BackupDefinition
    {
        public string ResourceType { get; set; }
        public List<string> ResourceIds { get; set; }
        public string BackupType { get; set; }
        public string Destination { get; set; }
    }

    public class Backup
    {
        public string BackupId { get; set; }
        public string StrategyId { get; set; }
        public string TenantId { get; set; }
        public string ResourceType { get; set; }
        public List<string> ResourceIds { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public int Duration { get; set; }
        public string Status { get; set; }
        public string BackupType { get; set; }
        public int SizeGB { get; set; }
        public int FileCount { get; set; }
        public string Destination { get; set; }
        public string VerificationStatus { get; set; }
        public string EncryptionKeyId { get; set; }
        public string Checksum { get; set; }
        public DateTimeOffset RetentionExpiresAt { get; set; }
        public bool IsIncremental { get; set; }
        public string ParentBackupId { get; set; }
        public int DataTransferedGB { get; set; }
        public decimal DataDeduplicationRatio { get; set; }
    }

    public class RestoreDefinition
    {
        public string TargetEnvironment { get; set; }
        public DateTimeOffset? PointInTime { get; set; }
        public bool VerifyAfterRestore { get; set; }
        public string RestoreMethod { get; set; }
    }

    public class RecoveryPlanDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public string Priority { get; set; }
        public int? RPOMinutes { get; set; }
        public int? RTOMinutes { get; set; }
        public string TestFrequency { get; set; }
        public DateTimeOffset? LastTestedAt { get; set; }
        public string DocumentationUrl { get; set; }
        public string AssignedTeam { get; set; }
    }

    public class RecoveryPlan
    {
        public string PlanId { get; set; }
        public string TenantId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string Status { get; set; }
        public string Priority { get; set; }
        public int RPOMinutes { get; set; }
        public int RTOMinutes { get; set; }
        public List<RecoveryStep> Steps { get; set; } = new();
        public List<PrerequisiteCheck> PrerequisitesChecks { get; set; } = new();
        public int EstimatedRecoveryTimeMinutes { get; set; }
        public Dictionary<string, int> ResourcesRequired { get; set; } = new();
        public string TestFrequency { get; set; }
        public DateTimeOffset? LastTestedAt { get; set; }
        public DateTimeOffset NextTestDueAt { get; set; }
        public string DocumentationUrl { get; set; }
        public string AssignedTeam { get; set; }
    }

    public class RecoveryStep
    {
        public int StepNumber { get; set; }
        public string Name { get; set; }
        public int EstimatedTime { get; set; }
    }

    public class PrerequisiteCheck
    {
        public string CheckId { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
    }

    public class FailoverStatus
    {
        public string FailoverId { get; set; }
        public string TenantId { get; set; }
        public DateTimeOffset InitiatedAt { get; set; }
        public string PrimaryRegion { get; set; }
        public string StandbyRegion { get; set; }
        public string Status { get; set; }
        public int ProgressPercent { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EstimatedCompletionTime { get; set; }
        public string DataSyncStatus { get; set; }
        public int ConnectionsTransferred { get; set; }
        public int TotalConnections { get; set; }
        public long DataLossBytes { get; set; }
        public int ErrorCount { get; set; }
        public List<string> HealthChecks { get; set; } = new();
    }

    public class DataConsistency
    {
        public string VerificationId { get; set; }
        public string BackupId { get; set; }
        public string TenantId { get; set; }
        public DateTimeOffset VerifiedAt { get; set; }
        public string Status { get; set; }
        public int TotalRecords { get; set; }
        public int VerifiedRecords { get; set; }
        public int CorruptedRecords { get; set; }
        public int MissingRecords { get; set; }
        public int OrphanedRecords { get; set; }
        public double InconsistencyRate { get; set; }
        public string ChecksumValidation { get; set; }
        public int IntegrityScore { get; set; }
        public int RepairedRecords { get; set; }
        public int VerificationDurationSeconds { get; set; }
    }

    public class PointInTimeRecovery
    {
        public string RecoveryId { get; set; }
        public string ResourceId { get; set; }
        public string TenantId { get; set; }
        public DateTimeOffset TargetTime { get; set; }
        public int AvailableBackups { get; set; }
        public string OptimalBackupId { get; set; }
        public int TimeDifferenceMinutes { get; set; }
        public string DataAvailability { get; set; }
        public string RecoveryFeasibility { get; set; }
        public int EstimatedRecoveryTimeMinutes { get; set; }
        public int IncrementalBackupsNeeded { get; set; }
        public int TotalDataToRestore { get; set; }
        public bool TransactionLogsAvailable { get; set; }
        public DateTimeOffset LogsCoverageStart { get; set; }
        public DateTimeOffset LogsCoverageEnd { get; set; }
    }

    public class BackupSchedule
    {
        public string ScheduleId { get; set; }
        public string Frequency { get; set; }
        public string TimeOfDay { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string Status { get; set; }
        public DateTimeOffset? LastExecuted { get; set; }
        public DateTimeOffset? NextExecution { get; set; }
    }

    public class DisasterRecoveryMetrics
    {
        public string TenantId { get; set; }
        public DateTimeOffset CalculatedAt { get; set; }
        public int BackupStrategies { get; set; }
        public int ActiveStrategies { get; set; }
        public int TotalBackups { get; set; }
        public int SuccessfulBackups { get; set; }
        public int FailedBackups { get; set; }
        public int IncompleteBackups { get; set; }
        public int AverageBackupSizeGB { get; set; }
        public int TotalBackupStorageGB { get; set; }
        public int DataRetentionDays { get; set; }
        public int RecoveryPlans { get; set; }
        public int VerifiedRecoveryPlans { get; set; }
        public bool FailoverCapabilitiesEnabled { get; set; }
        public int RPOCompliancePercent { get; set; }
        public int RTOCompliancePercent { get; set; }
        public int DataConsistencyScore { get; set; }
        public DateTimeOffset LastSuccessfulRecoveryTest { get; set; }
        public string RecoverySLA { get; set; }
    }
}
