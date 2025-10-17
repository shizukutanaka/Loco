using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Interfaces;

namespace Loco.Core.Models
{
    // ============ Base Models ============

    /// <summary>
    /// Base class for models with common properties
    /// </summary>
    public abstract class BaseModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }

    /// <summary>
    /// Base class for parameterized models
    /// </summary>
    public class ParameterizedModel : BaseModel
    {
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Base class for typed models
    /// </summary>
    public class TypedModel : ParameterizedModel
    {
        public string Type { get; set; } = "";
    }

    // ============ Engine Status ============

    /// <summary>
    /// Status information for the engine
    /// </summary>
    public class EngineStatus
    {
        public int FlowCount { get; set; }
        public int RuleCount { get; set; }
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions * 100 : 0;
    }

    // ============ Triggers & Actions ============

    /// <summary>
    /// Simple trigger definition
    /// </summary>
    public class LightTrigger : TypedModel
    {
    }

    /// <summary>
    /// Simple action definition
    /// </summary>
    public class LightAction : TypedModel
    {
        public new Dictionary<string, object> Parameters
        {
            get => base.Parameters;
            set => base.Parameters = value;
        }
    }

    /// <summary>
    /// Simple automation rule
    /// </summary>
    public class SimpleRule : BaseModel
    {
        public LightTrigger Trigger { get; set; } = new();
        public LightAction[] Actions { get; set; } = Array.Empty<LightAction>();
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedUtc { get; set; }
        public DateTime? LastUpdatedUtc { get; set; }

        public SimpleRule()
        {
            CreatedUtc = DateTime.UtcNow;
        }

        public SimpleRule(string id, string name, LightTrigger trigger, LightAction[] actions, string? description = null, bool isEnabled = true)
        {
            Id = id;
            Name = name;
            Trigger = trigger;
            Actions = actions;
            Description = description ?? string.Empty;
            IsEnabled = isEnabled;
            CreatedUtc = DateTime.UtcNow;
        }
    }

    // ============ Flows ============

    /// <summary>
    /// Simple flow definition for automation engine
    /// </summary>
    public class SimpleFlow : IFlow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public List<IAction> Actions { get; set; } = new();

        public SimpleFlow(string name, string description, string? id = null)
        {
            Id = id ?? Guid.NewGuid().ToString();
            Name = name;
            Description = description;
        }

        public async Task ExecuteAsync(IActionContext context, CancellationToken cancellationToken = default)
        {
            context.FlowId = Id;
            context.Logger?.LogInformation("Executing flow: {FlowName}", Name);

            foreach (var action in Actions)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    context.Logger?.LogInformation("Flow execution cancelled: {FlowName}", Name);
                    break;
                }

                context.ActionId = action.Id;
                var success = await action.ExecuteAsync(context);

                if (!success)
                {
                    context.Logger?.LogWarning("Action failed in flow {FlowName}: {ActionName}", Name, action.Name);
                    break;
                }
            }

            context.Logger?.LogInformation("Flow completed: {FlowName}", Name);
        }
    }

    // ============ Context & Results ============

    /// <summary>
    /// Action context for flow execution
    /// </summary>
    public class ActionContext : IActionContext
    {
        public Dictionary<string, object?> Variables { get; set; } = new();
        public ILogger? Logger { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public string? FlowId { get; set; }
        public string? ActionId { get; set; }
    }

    /// <summary>
    /// Result of action execution
    /// </summary>
    public class ActionResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Dictionary<string, object?>? Outputs { get; set; }
        public TimeSpan Duration { get; set; }
    }

    // ============ Built-in Actions ============

    /// <summary>
    /// Simple log action for testing
    /// </summary>
    public class LogAction : IAction
    {
        public string Id { get; }
        public string Name { get; }
        public string Message { get; }

        public LogAction(string id, string name, string message)
        {
            Id = id;
            Name = name;
            Message = message;
        }

        public Task<bool> ExecuteAsync(IActionContext context)
        {
            context.Logger?.LogInformation("LogAction: {Message}", Message);
            return Task.FromResult(true);
        }
    }
}
