using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Components;

/// <summary>
/// Base interface for all components
/// </summary>
public interface IComponent
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    ComponentType Type { get; }
    Dictionary<string, object> Configuration { get; set; }
}

/// <summary>
/// Trigger component interface
/// </summary>
public interface ITrigger : IComponent
{
    event EventHandler<TriggerEventArgs> Triggered;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    bool IsRunning { get; }
}

/// <summary>
/// Action component interface
/// </summary>
public interface IAction : IComponent
{
    Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Condition component interface
/// </summary>
public interface ICondition : IComponent
{
    Task<bool> EvaluateAsync(ConditionContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Component type enumeration
/// </summary>
public enum ComponentType
{
    Trigger,
    Action,
    Condition
}

/// <summary>
/// Trigger event arguments
/// </summary>
public class TriggerEventArgs : EventArgs
{
    public string TriggerId { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Data { get; set; }
    
    public TriggerEventArgs()
    {
        Timestamp = DateTime.UtcNow;
        Data = new Dictionary<string, object>();
    }
}

/// <summary>
/// Action execution context
/// </summary>
public class ActionContext
{
    public Dictionary<string, object> Variables { get; set; }
    public Dictionary<string, object> Input { get; set; }
    public object PreviousResult { get; set; }
    
    public ActionContext()
    {
        Variables = new Dictionary<string, object>();
        Input = new Dictionary<string, object>();
    }
}

/// <summary>
/// Action execution result
/// </summary>
public class ActionResult
{
    public bool Success { get; set; }
    public object Output { get; set; }
    public string Error { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    
    public ActionResult()
    {
        Success = true;
        Metadata = new Dictionary<string, object>();
    }
    
    public static ActionResult Ok(object output = null)
    {
        return new ActionResult
        {
            Success = true,
            Output = output
        };
    }
    
    public static ActionResult Fail(string error)
    {
        return new ActionResult
        {
            Success = false,
            Error = error
        };
    }
}

/// <summary>
/// Condition evaluation context
/// </summary>
public class ConditionContext
{
    public Dictionary<string, object> Variables { get; set; }
    public object Input { get; set; }
    
    public ConditionContext()
    {
        Variables = new Dictionary<string, object>();
    }
}

/// <summary>
/// Base component implementation
/// </summary>
public abstract class ComponentBase : IComponent
{
    public string Id { get; protected set; }
    public string Name { get; protected set; }
    public string Description { get; protected set; }
    public ComponentType Type { get; protected set; }
    public Dictionary<string, object> Configuration { get; set; }
    
    protected ComponentBase(string id, string name, string description, ComponentType type)
    {
        Id = id;
        Name = name;
        Description = description;
        Type = type;
        Configuration = new Dictionary<string, object>();
    }
    
    protected T GetConfig<T>(string key, T defaultValue = default)
    {
        if (Configuration.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
                return typedValue;
            
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        
        return defaultValue;
    }
}
