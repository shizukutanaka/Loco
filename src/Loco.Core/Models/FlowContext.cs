namespace Loco.Core.Models;

public sealed class FlowContext
{
    public IDictionary<string, object?> Variables { get; } = new Dictionary<string, object?>();
}
