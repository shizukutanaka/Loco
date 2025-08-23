# Loco Developer Quick Start Guide

## New Enterprise Features (2025-08-20)

### 1. AI-Powered Flow Optimization

```csharp
// Optimize a flow automatically
var optimizer = serviceProvider.GetService<FlowOptimizationEngine>();
var result = await optimizer.OptimizeFlowAsync(flow);

if (result.Success)
{
    Console.WriteLine($"Improvement: {result.ImprovementScore * 100}%");
    // Apply optimized flow
    flow = result.OptimizedFlow;
}
```

### 2. Real-time Metrics Collection

```csharp
// Record custom metrics
var metrics = serviceProvider.GetService<AdvancedMetricsService>();
metrics.RecordMetric("custom.metric", 42.5, new Dictionary<string, string> 
{
    ["environment"] = "production",
    ["region"] = "us-west"
});

// Get dashboard data
var dashboard = metrics.GetDashboardData();
Console.WriteLine($"System Health: {dashboard.SystemHealth}%");

// Analyze metrics
var analysis = await metrics.AnalyzeMetrics("response.time", TimeSpan.FromHours(1));
```

### 3. Security Audit System

```csharp
// Log security events
var audit = serviceProvider.GetService<SecurityAuditService>();
audit.LogSecurityEvent(
    SecurityEventType.DataAccess,
    "User accessed sensitive data",
    new SecurityContext { UserId = "user123" },
    SecuritySeverity.Info
);

// Perform security audit
var report = await audit.PerformAuditAsync();
Console.WriteLine($"Security Score: {report.OverallScore}/100");

// Validate log integrity
var integrity = await audit.ValidateLogIntegrity();
Console.WriteLine($"Logs Valid: {integrity.IsValid}");
```

### 4. Auto-Scaling Service

```csharp
// Create auto-scaled worker pool
var scaling = serviceProvider.GetService<AutoScalingService>();
var pool = scaling.CreateWorkerPool("api-handlers", new WorkerPoolOptions
{
    MinWorkers = 2,
    MaxWorkers = 20,
    AutoScaleEnabled = true
});

// Configure scaling policy
scaling.ConfigurePolicy(pool.Id, new ScalingPolicy
{
    ScaleUpCpuThreshold = 70,
    ScaleDownCpuThreshold = 30,
    CooldownPeriod = TimeSpan.FromMinutes(5)
});

// Get health report
var health = scaling.GetHealthReport();
Console.WriteLine($"Overall Health: {health.OverallHealth}%");
```

### 5. Disaster Recovery

```csharp
// Create recovery point
var dr = serviceProvider.GetService<DisasterRecoveryService>();
var recoveryPoint = await dr.CreateRecoveryPoint("manual-backup");

// Schedule automated backups
dr.ScheduleBackupJob(new BackupSchedule
{
    Name = "Daily Backup",
    Type = ScheduleType.Daily
});

// Test disaster recovery
var testResult = await dr.TestDisasterRecovery();
Console.WriteLine($"DR Score: {testResult.OverallScore}/100");

// Perform failover if needed
if (criticalFailure)
{
    var failoverResult = await dr.InitiateFailover(new FailoverOptions
    {
        Type = FailoverType.Automatic
    });
}
```

### 6. Performance Benchmarking

```csharp
// Run comprehensive benchmark
var benchmark = serviceProvider.GetService<PerformanceBenchmarkService>();
var report = await benchmark.RunComprehensiveBenchmark();

Console.WriteLine($"Performance Score: {report.OverallScore}/100");
Console.WriteLine($"CPU: {report.CpuResults.First().Value} MFLOPS");
Console.WriteLine($"Memory: {report.MemoryResults.First().Value} GB/s");

// Run stress test
var stressResult = await benchmark.RunStressTest(new StressTestOptions
{
    TestType = StressTestType.Combined,
    MaxLoad = 1000,
    TestDuration = TimeSpan.FromMinutes(5)
});

// Get performance trends
var trends = benchmark.GetPerformanceTrends(TimeSpan.FromDays(7));
```

## API Endpoints

### Advanced Features API

```http
# Flow Optimization
POST /api/advanced/optimize/{flowId}
POST /api/advanced/optimize/batch

# Metrics
GET  /api/advanced/metrics
GET  /api/advanced/dashboard
POST /api/advanced/metrics
GET  /api/advanced/metrics/analyze/{metricName}
GET  /api/advanced/metrics/export?format=json

# Security Audit
POST /api/advanced/audit
GET  /api/advanced/audit/statistics
POST /api/advanced/audit/validate
GET  /api/advanced/audit/export
POST /api/advanced/audit/log
```

## Configuration

### appsettings.json

```json
{
  "Loco": {
    "Optimization": {
      "EnableAutoOptimization": true,
      "OptimizationLevel": "Aggressive"
    },
    "Metrics": {
      "EnableRealTimeMonitoring": true,
      "RetentionDays": 30,
      "AnomalyDetectionSensitivity": 0.95
    },
    "Security": {
      "EnableAuditLogging": true,
      "ComplianceMode": "SOC2",
      "ThreatAnalysisEnabled": true
    },
    "DisasterRecovery": {
      "BackupInterval": "01:00:00",
      "RetentionPeriod": "30.00:00:00",
      "EnableReplication": true,
      "AutoFailoverEnabled": false
    },
    "AutoScaling": {
      "EnablePredictiveScaling": true,
      "CostOptimizationEnabled": true,
      "DefaultMinWorkers": 1,
      "DefaultMaxWorkers": 10
    },
    "Benchmarking": {
      "EnableScheduledBenchmarks": true,
      "BenchmarkInterval": "01:00:00",
      "PersistResults": true
    }
  }
}
```

## Dependency Injection Setup

```csharp
// Program.cs or Startup.cs
services.AddSingleton<FlowOptimizationEngine>();
services.AddSingleton<AdvancedMetricsService>();
services.AddSingleton<SecurityAuditService>();
services.AddSingleton<AutoScalingService>();
services.AddSingleton<DisasterRecoveryService>();
services.AddSingleton<PerformanceBenchmarkService>();

// Add configuration
services.Configure<DisasterRecoveryConfiguration>(
    Configuration.GetSection("Loco:DisasterRecovery"));
services.Configure<ScalingConfiguration>(
    Configuration.GetSection("Loco:AutoScaling"));
services.Configure<BenchmarkConfiguration>(
    Configuration.GetSection("Loco:Benchmarking"));
```

## Best Practices

### 1. Flow Optimization
- Run optimization during off-peak hours
- Test optimized flows in staging first
- Monitor performance metrics after optimization
- Keep original flows as backup

### 2. Metrics Collection
- Use consistent naming conventions for metrics
- Add meaningful tags for filtering
- Set up alerts for anomalies
- Export metrics regularly for analysis

### 3. Security Auditing
- Enable audit logging for all sensitive operations
- Regularly validate log integrity
- Review security reports weekly
- Implement recommended security improvements

### 4. Auto-Scaling
- Start with conservative scaling policies
- Monitor actual vs predicted scaling
- Optimize for cost during low-traffic periods
- Use predictive scaling for known patterns

### 5. Disaster Recovery
- Test DR procedures monthly
- Maintain multiple recovery points
- Document recovery procedures
- Train team on failover process

### 6. Performance Benchmarking
- Establish baselines before changes
- Run benchmarks consistently
- Compare results over time
- Act on regression detections

## Monitoring & Alerts

```csharp
// Set up monitoring
services.Configure<MonitoringOptions>(options =>
{
    options.EnableAlerts = true;
    options.AlertThresholds = new AlertThresholds
    {
        CpuUsage = 80,
        MemoryUsage = 90,
        ErrorRate = 5,
        ResponseTime = 1000
    };
});

// Subscribe to events
metricsService.AnomalyDetected += (sender, alert) =>
{
    Console.WriteLine($"Anomaly: {alert.MetricName} = {alert.Value}");
    // Send notification
};

auditService.ThreatDetected += (sender, threat) =>
{
    Console.WriteLine($"Threat: {threat.Description}");
    // Take action
};
```

## Testing

```csharp
[Fact]
public async Task OptimizeFlow_ShouldImprovePerformance()
{
    // Arrange
    var optimizer = new FlowOptimizationEngine(logger, llmService);
    var flow = CreateTestFlow();
    
    // Act
    var result = await optimizer.OptimizeFlowAsync(flow);
    
    // Assert
    Assert.True(result.Success);
    Assert.True(result.ImprovementScore > 0);
    Assert.NotNull(result.OptimizedFlow);
}

[Fact]
public async Task DisasterRecovery_ShouldCreateAndRestoreBackup()
{
    // Arrange
    var dr = new DisasterRecoveryService(logger);
    
    // Act
    var backup = await dr.CreateRecoveryPoint("test");
    var restore = await dr.RestoreFromRecoveryPoint(backup.Id);
    
    // Assert
    Assert.Equal(RecoveryPointStatus.Completed, backup.Status);
    Assert.True(restore.Success);
}
```

## Troubleshooting

### Common Issues

1. **High CPU Usage During Optimization**
   - Solution: Reduce optimization level or run during off-peak

2. **Metrics Not Recording**
   - Check: Service registration, connection strings, permissions

3. **Security Audit Failures**
   - Verify: File permissions, disk space, audit configuration

4. **Auto-Scaling Not Triggering**
   - Check: Scaling policies, cooldown periods, metrics collection

5. **Backup Failures**
   - Verify: Storage space, network connectivity, permissions

6. **Benchmark Inconsistencies**
   - Solution: Increase warmup iterations, check system load

## Performance Tips

1. **Enable caching** for frequently accessed data
2. **Use async/await** throughout the application
3. **Implement circuit breakers** for external services
4. **Pool database connections** for better throughput
5. **Enable compression** for network traffic
6. **Use batch operations** where possible
7. **Monitor and optimize** database queries
8. **Implement proper indexing** strategies

## Security Considerations

1. **Always encrypt** sensitive data at rest and in transit
2. **Implement proper** authentication and authorization
3. **Regular security** audits and penetration testing
4. **Keep dependencies** updated and patched
5. **Follow principle** of least privilege
6. **Implement rate limiting** and DDoS protection
7. **Log all security** events for audit trail
8. **Regular backup** and disaster recovery testing

## Support

- Documentation: `/docs`
- API Reference: `/api/docs`
- GitHub Issues: `https://github.com/shizukutanaka/Loco/issues`

---

**Happy Coding with Loco!** 🚀
