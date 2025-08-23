using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Models;

/// <summary>
/// Action execution context - Simple and efficient data container
/// </summary>
public class ActionContext
{
    public Dictionary<string, object> Variables { get; set; } = new();
    public Dictionary<string, object> TriggerContext { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
    public ILogger Logger { get; set; }
    public DateTime ExecutionStartTime { get; set; } = DateTime.UtcNow;
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Action execution result
/// </summary>
public class ActionResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public object Data { get; set; }
    public Dictionary<string, object> OutputVariables { get; set; } = new();
    public Exception Exception { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public long ExecutionTimeMs { get; set; }
}

/// <summary>
/// Trigger event arguments
/// </summary>
public class TriggerEventArgs : EventArgs
{
    public string TriggerId { get; set; }
    public string TriggerType { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
    public DateTime FiredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Execution request for sandbox
/// </summary>
public class ExecutionRequest
{
    public ExecutionType Type { get; set; }
    public string Command { get; set; }
    public string Arguments { get; set; }
    public string WorkingDirectory { get; set; }
    public Dictionary<string, string> Environment { get; set; } = new();
    public ExecutionPermissions Permissions { get; set; } = new();
    public ResourceLimits ResourceLimits { get; set; } = new();
    public string ExecutionId { get; set; }
}

/// <summary>
/// Execution result from sandbox
/// </summary>
public class ExecutionResult
{
    public bool Success { get; set; }
    public string Output { get; set; }
    public string Error { get; set; }
    public int? ExitCode { get; set; }
    public long ExecutionTimeMs { get; set; }
}

/// <summary>
/// Execution type
/// </summary>
public enum ExecutionType
{
    Process,
    Script,
    Shell
}

/// <summary>
/// Execution permissions
/// </summary>
public class ExecutionPermissions
{
    public bool Network { get; set; }
    public bool FileSystem { get; set; }
    public bool Shell { get; set; }
    public bool Llm { get; set; }
    public List<string> AllowedDomains { get; set; } = new();
    public List<string> AllowedPaths { get; set; } = new();
}

/// <summary>
/// Resource limits for execution
/// </summary>
public class ResourceLimits
{
    public int TimeoutMs { get; set; } = 30000; // 30 seconds default
    public long MaxMemoryMb { get; set; } = 512; // 512MB default
    public int MaxCpuPercent { get; set; } = 50; // 50% CPU default
    public long MaxDiskIoMbps { get; set; } = 10; // 10MB/s default
}

/// <summary>
/// System event types for triggers
/// </summary>
public enum SystemEventType
{
    Startup,
    Shutdown,
    Sleep,
    Resume,
    NetworkStatus,
    BatteryLow,
    DiskSpaceLow,
    UserLogin,
    UserLogout
}

/// <summary>
/// Component type for flow builder
/// </summary>
public enum ComponentType
{
    Trigger,
    Condition,
    Action
}

/// <summary>
/// Parameter type for component parameters
/// </summary>
public enum ParameterType
{
    String,
    Number,
    Boolean,
    Select,
    MultiSelect,
    Slider,
    DateTime,
    File,
    Directory,
    Json
}
