using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Interfaces;
using Loco.Core.Models;

namespace Loco.Core
{
    /// <summary>
    /// Simple flow execution engine
    /// </summary>
    public class SimpleFlowEngine : IFlowEngine
    {
        private readonly ILogger<SimpleFlowEngine> _logger;
        private readonly Dictionary<string, IFlow> _flows = new();
        private readonly Dictionary<string, CancellationTokenSource> _runningFlows = new();

        public SimpleFlowEngine(ILogger<SimpleFlowEngine> logger = null)
        {
            _logger = logger;
        }

        public async Task<bool> ExecuteFlowAsync(string flowId, Dictionary<string, object> context = null)
        {
            if (!_flows.TryGetValue(flowId, out var flow))
            {
                _logger?.LogWarning("Flow {FlowId} not found", flowId);
                return false;
            }

            var cts = new CancellationTokenSource();
            _runningFlows[flowId] = cts;

            try
            {
                _logger?.LogInformation("Starting flow {FlowId}", flowId);

                // Create action context
                var actionContext = new ActionContext
                {
                    Variables = context ?? new Dictionary<string, object>(),
                    Logger = _logger,
                    ExecutionId = Guid.NewGuid().ToString()
                };

                // Execute flow
                await flow.ExecuteAsync(actionContext, cts.Token);

                _logger?.LogInformation("Flow {FlowId} completed successfully", flowId);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Flow {FlowId} failed", flowId);
                return false;
            }
            finally
            {
                _runningFlows.Remove(flowId);
                cts.Dispose();
            }
        }

        public void RegisterFlow(IFlow flow)
        {
            if (flow == null) return;
            _flows[flow.Id] = flow;
            _logger?.LogInformation("Flow {FlowId} registered", flow.Id);
        }

        public void UnregisterFlow(string flowId)
        {
            if (_flows.Remove(flowId))
            {
                _logger?.LogInformation("Flow {FlowId} unregistered", flowId);
            }
        }

        public IEnumerable<string> GetRegisteredFlows()
        {
            return _flows.Keys;
        }

        public void StopFlow(string flowId)
        {
            if (_runningFlows.TryGetValue(flowId, out var cts))
            {
                cts.Cancel();
                _logger?.LogInformation("Flow {FlowId} stop requested", flowId);
            }
        }

        public void Dispose()
        {
            foreach (var cts in _runningFlows.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _runningFlows.Clear();
            _flows.Clear();
        }
    }

    /// <summary>
    /// Simple flow implementation
    /// </summary>
    public class SimpleFlow : IFlow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<IAction> Actions { get; set; } = new();

        public async Task<bool> ExecuteAsync(ActionContext context, CancellationToken cancellationToken = default)
        {
            foreach (var action in Actions)
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                var result = await action.ExecuteAsync(context, cancellationToken);
                if (!result.Success)
                {
                    return false;
                }

                // Merge output variables into context
                if (result.OutputVariables != null)
                {
                    foreach (var kvp in result.OutputVariables)
                    {
                        context.Variables[kvp.Key] = kvp.Value;
                    }
                }
            }

            return true;
        }
    }
}