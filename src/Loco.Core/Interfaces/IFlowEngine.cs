using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Models;

namespace Loco.Core.Interfaces;

public interface IFlowEngine
{
    Task RunAsync(IFlow flow, FlowContext context, CancellationToken cancellationToken = default);
}
