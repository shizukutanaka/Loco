using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Models;

namespace Loco.Core.Interfaces;

public interface ICondition
{
    string Id { get; }
    string Name { get; }
    Task<bool> EvaluateAsync(FlowContext context, CancellationToken cancellationToken = default);
}
