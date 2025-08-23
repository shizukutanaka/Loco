using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Models;

namespace Loco.Core.Interfaces;

/// <summary>
/// Action interface - Rob Pike simplicity principle
/// </summary>
public interface IAction
{
    string Id { get; }
    Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default);
}
