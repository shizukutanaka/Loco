using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Interfaces
{
    /// <summary>
    /// Base interface for automation engines
    /// </summary>
    public interface IAutomationEngine : IDisposable
    {
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync(CancellationToken cancellationToken = default);
        Task<bool> ExecuteFlowAsync(string flowId, CancellationToken cancellationToken = default);
        void AddFlow(IFlow flow);
    }

    /// <summary>
    /// Base interface for flows
    /// </summary>
    public interface IFlow
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        bool IsEnabled { get; }
        Task ExecuteAsync(IActionContext context, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Base interface for action context
    /// </summary>
    public interface IActionContext
    {
        Dictionary<string, object?> Variables { get; }
        ILogger? Logger { get; }
        CancellationToken CancellationToken { get; }
        string? FlowId { get; set; }
        string? ActionId { get; set; }
    }

    /// <summary>
    /// Base interface for actions
    /// </summary>
    public interface IAction
    {
        string Id { get; }
        string Name { get; }
        Task<bool> ExecuteAsync(IActionContext context);
    }
}
