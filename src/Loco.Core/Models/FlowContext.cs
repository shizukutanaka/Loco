using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Models;

public sealed class FlowContext
{
    public IDictionary<string, object?> Variables { get; set; } = new Dictionary<string, object?>();
    public string ExecutionId { get; set; }
    public ILogger Logger { get; set; }
}
