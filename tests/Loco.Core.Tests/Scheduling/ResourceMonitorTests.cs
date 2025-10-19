using Xunit;

namespace Loco.Core.Tests.Scheduling;

/// <summary>
/// Tests for ResourceMonitor
/// Note: ResourceMonitor is internal, so we test it through IntelligentScheduler
/// </summary>
public class ResourceMonitorTests
{
    [Fact]
    public async Task GetUtilizationAsync_ReturnsValidMetrics()
    {
        // Arrange
        var scheduler = new Loco.Core.Scheduling.IntelligentScheduler();

        // Act - Access through reflection since ResourceMonitor is internal
        var schedulerType = scheduler.GetType();
        var resourceMonitorField = schedulerType.GetField("_resourceMonitor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(resourceMonitorField);
        var resourceMonitor = resourceMonitorField.GetValue(scheduler);
        Assert.NotNull(resourceMonitor);

        var method = resourceMonitor.GetType().GetMethod("GetUtilizationAsync");
        Assert.NotNull(method);

        dynamic task = method.Invoke(resourceMonitor, null)!;
        var utilization = await task;

        // Assert
        Assert.NotNull(utilization);
        var utilizationType = utilization.GetType();

        var memoryProp = utilizationType.GetProperty("MemoryUsedMB");
        var cpuProp = utilizationType.GetProperty("CpuPercent");
        var diskProp = utilizationType.GetProperty("DiskUsedMB");

        var memoryUsed = (int)memoryProp!.GetValue(utilization)!;
        var cpuPercent = (int)cpuProp!.GetValue(utilization)!;
        var diskUsed = (int)diskProp!.GetValue(utilization)!;

        // Memory should be positive
        Assert.True(memoryUsed > 0, "Memory usage should be positive");

        // CPU should be 0-100
        Assert.True(cpuPercent >= 0 && cpuPercent <= 100,
            $"CPU usage should be 0-100, got {cpuPercent}");

        // Disk usage should be non-negative
        Assert.True(diskUsed >= 0, "Disk usage should be non-negative");
    }

    [Fact]
    public async Task CheckResourcesAsync_WithNullRequirements_ReturnsAvailable()
    {
        // Arrange
        var scheduler = new Loco.Core.Scheduling.IntelligentScheduler();
        var schedulerType = scheduler.GetType();
        var resourceMonitorField = schedulerType.GetField("_resourceMonitor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(resourceMonitorField);
        var resourceMonitor = resourceMonitorField.GetValue(scheduler);
        Assert.NotNull(resourceMonitor);

        // Act
        var method = resourceMonitor.GetType().GetMethod("CheckResourcesAsync");
        Assert.NotNull(method);

        dynamic task = method.Invoke(resourceMonitor, new object?[] { null })!;
        var result = await task;

        // Assert
        Assert.NotNull(result);
        var availableProp = result.GetType().GetProperty("Available");
        var available = (bool)availableProp!.GetValue(result)!;
        Assert.True(available);
    }

    [Fact]
    public async Task EstimateResourceAvailabilityDelay_ReturnsValidTimeSpan()
    {
        // Arrange
        var scheduler = new Loco.Core.Scheduling.IntelligentScheduler();
        var schedulerType = scheduler.GetType();
        var resourceMonitorField = schedulerType.GetField("_resourceMonitor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(resourceMonitorField);
        var resourceMonitor = resourceMonitorField.GetValue(scheduler);
        Assert.NotNull(resourceMonitor);

        // Create ResourceRequirements
        var requirementsType = scheduler.GetType().Assembly.GetType(
            "Loco.Core.Scheduling.ResourceRequirements");
        Assert.NotNull(requirementsType);

        var requirements = Activator.CreateInstance(requirementsType);
        var memoryProp = requirementsType.GetProperty("MemoryMB");
        memoryProp?.SetValue(requirements, 100);

        // Act
        var method = resourceMonitor.GetType().GetMethod("EstimateResourceAvailabilityDelay");
        Assert.NotNull(method);

        var task = (Task<TimeSpan>)method.Invoke(resourceMonitor, new[] { requirements })!;
        var delay = await task;

        // Assert
        Assert.True(delay >= TimeSpan.Zero, "Delay should be non-negative");
        Assert.True(delay <= TimeSpan.FromMinutes(1),
            "Delay should be reasonable (less than 1 minute)");
    }

    [Fact]
    public async Task CalculateCpuUsageAsync_MultipleCallsWork()
    {
        // Arrange
        var scheduler = new Loco.Core.Scheduling.IntelligentScheduler();
        var schedulerType = scheduler.GetType();
        var resourceMonitorField = schedulerType.GetField("_resourceMonitor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(resourceMonitorField);
        var resourceMonitor = resourceMonitorField.GetValue(scheduler);
        Assert.NotNull(resourceMonitor);

        var method = resourceMonitor.GetType().GetMethod("CalculateCpuUsageAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        // Act - Call multiple times
        var task1 = (Task<double>)method.Invoke(resourceMonitor, null)!;
        var cpu1 = await task1;

        // Wait a bit
        await Task.Delay(100);

        var task2 = (Task<double>)method.Invoke(resourceMonitor, null)!;
        var cpu2 = await task2;

        // Assert
        Assert.True(cpu1 >= 0 && cpu1 <= 100, "First CPU reading should be 0-100");
        Assert.True(cpu2 >= 0 && cpu2 <= 100, "Second CPU reading should be 0-100");
    }

    [Fact]
    public void GetTotalSystemMemoryMB_ReturnsPositiveValue()
    {
        // Arrange
        var scheduler = new Loco.Core.Scheduling.IntelligentScheduler();
        var schedulerType = scheduler.GetType();
        var resourceMonitorField = schedulerType.GetField("_resourceMonitor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(resourceMonitorField);
        var resourceMonitor = resourceMonitorField.GetValue(scheduler);
        Assert.NotNull(resourceMonitor);

        var method = resourceMonitor.GetType().GetMethod("GetTotalSystemMemoryMB",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var totalMemory = (int)method.Invoke(resourceMonitor, null)!;

        // Assert
        Assert.True(totalMemory > 0, "Total system memory should be positive");
        Assert.True(totalMemory >= 1024,
            "Total system memory should be at least 1GB (1024 MB)");
    }

    [Fact]
    public void GetAvailableDiskSpaceMB_ReturnsPositiveValue()
    {
        // Arrange
        var scheduler = new Loco.Core.Scheduling.IntelligentScheduler();
        var schedulerType = scheduler.GetType();
        var resourceMonitorField = schedulerType.GetField("_resourceMonitor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(resourceMonitorField);
        var resourceMonitor = resourceMonitorField.GetValue(scheduler);
        Assert.NotNull(resourceMonitor);

        var method = resourceMonitor.GetType().GetMethod("GetAvailableDiskSpaceMB",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var availableDisk = (int)method.Invoke(resourceMonitor, null)!;

        // Assert
        Assert.True(availableDisk > 0, "Available disk space should be positive");
    }

    [Fact]
    public void GetDiskUsedMB_ReturnsNonNegativeValue()
    {
        // Arrange
        var scheduler = new Loco.Core.Scheduling.IntelligentScheduler();
        var schedulerType = scheduler.GetType();
        var resourceMonitorField = schedulerType.GetField("_resourceMonitor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(resourceMonitorField);
        var resourceMonitor = resourceMonitorField.GetValue(scheduler);
        Assert.NotNull(resourceMonitor);

        var method = resourceMonitor.GetType().GetMethod("GetDiskUsedMB",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var diskUsed = (int)method.Invoke(resourceMonitor, null)!;

        // Assert
        Assert.True(diskUsed >= 0, "Disk used should be non-negative");
    }

    [Fact]
    public async Task ResourcePressureCalculation_WorksCorrectly()
    {
        // Arrange
        var scheduler = new Loco.Core.Scheduling.IntelligentScheduler();
        var schedulerType = scheduler.GetType();
        var resourceMonitorField = schedulerType.GetField("_resourceMonitor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(resourceMonitorField);
        var resourceMonitor = resourceMonitorField.GetValue(scheduler);
        Assert.NotNull(resourceMonitor);

        // Create different resource requirement scenarios
        var requirementsType = scheduler.GetType().Assembly.GetType(
            "Loco.Core.Scheduling.ResourceRequirements");
        Assert.NotNull(requirementsType);

        // Low requirements
        var lowRequirements = Activator.CreateInstance(requirementsType);
        var memoryProp = requirementsType.GetProperty("MemoryMB");
        memoryProp?.SetValue(lowRequirements, 10); // 10MB

        // High requirements
        var highRequirements = Activator.CreateInstance(requirementsType);
        memoryProp?.SetValue(highRequirements, 4096); // 4GB

        var method = resourceMonitor.GetType().GetMethod("EstimateResourceAvailabilityDelay");
        Assert.NotNull(method);

        // Act
        var lowTask = (Task<TimeSpan>)method.Invoke(resourceMonitor, new[] { lowRequirements })!;
        var lowDelay = await lowTask;

        var highTask = (Task<TimeSpan>)method.Invoke(resourceMonitor, new[] { highRequirements })!;
        var highDelay = await highTask;

        // Assert
        // Both delays should be valid
        Assert.True(lowDelay >= TimeSpan.Zero);
        Assert.True(highDelay >= TimeSpan.Zero);

        // The actual relationship between delays depends on current system state,
        // so we just verify they're reasonable values
        Assert.True(lowDelay <= TimeSpan.FromMinutes(1));
        Assert.True(highDelay <= TimeSpan.FromMinutes(1));
    }
}
