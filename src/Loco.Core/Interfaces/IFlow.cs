using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Models;

namespace Loco.Core.Interfaces;

/// <summary>
/// Base flow interface
/// </summary>
public interface IFlow
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    bool Enabled { get; set; }
    Task ExecuteAsync(FlowContext context, CancellationToken cancellationToken = default);
    Task<RuleValidationResult> ValidateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Base flow implementation
/// </summary>
public abstract class FlowBase : IFlow
{
    public string Id { get; protected set; }
    public string Name { get; protected set; }
    public string Description { get; protected set; }
    public bool Enabled { get; set; } = true;
    
    protected FlowBase(string id, string name, string description = null)
    {
        Id = id;
        Name = name;
        Description = description;
    }
    
    public abstract Task ExecuteAsync(FlowContext context, CancellationToken cancellationToken = default);
    
    public virtual Task<RuleValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new RuleValidationResult
        {
            IsValid = true,
            Errors = new string[0]
        });
    }
}
