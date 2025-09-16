using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Interfaces;
using Loco.Core.Models;

namespace Loco.Core
{
    /// <summary>
    /// Main flow engine implementation (wraps SimpleFlowEngine)
    /// </summary>
    public class FlowEngine : IFlowEngine
    {
        private readonly SimpleFlowEngine _engine;
        private readonly ILogger<FlowEngine> _logger;

        public FlowEngine(ILogger<FlowEngine> logger = null)
        {
            _logger = logger;
            _engine = new SimpleFlowEngine(logger as ILogger<SimpleFlowEngine>);
        }

        public Task RunAsync(IFlow flow, FlowContext context, CancellationToken cancellationToken = default)
        {
            return _engine.RunAsync(flow, context, cancellationToken);
        }

        public void Dispose()
        {
            _engine?.Dispose();
        }
    }
}