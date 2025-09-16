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

        public async Task RunAsync(IFlow flow, FlowContext context, CancellationToken cancellationToken = default)
        {
            if (flow == null)
                throw new ArgumentNullException(nameof(flow));

            try
            {
                _logger?.LogInformation("Starting flow {FlowId}", flow.Id);
                await flow.ExecuteAsync(context, cancellationToken);
                _logger?.LogInformation("Flow {FlowId} completed successfully", flow.Id);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Flow {FlowId} failed", flow.Id);
                throw;
            }
        }

        public async Task<bool> ExecuteFlowAsync(string flowId, Dictionary<string, object> variables = null)
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
                var context = new FlowContext
                {
                    Variables = variables ?? new Dictionary<string, object>(),
                    ExecutionId = Guid.NewGuid().ToString(),
                    Logger = _logger
                };

                await RunAsync(flow, context, cts.Token);
                return true;
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
        public string Description { get; set; }
        public bool Enabled { get; set; } = true;
        public List<IAction> Actions { get; set; } = new();

        public async Task ExecuteAsync(FlowContext context, CancellationToken cancellationToken = default)
        {
            if (!Enabled)
            {
                context.Logger?.LogWarning("Flow {FlowId} is disabled", Id);
                return;
            }

            // Create action context from flow context
            var actionContext = new ActionContext
            {
                Variables = new Dictionary<string, object>(context.Variables.Count),
                Logger = context.Logger,
                ExecutionId = context.ExecutionId,
                Parameters = new Dictionary<string, object>()
            };

            // Copy variables
            foreach (var kvp in context.Variables)
            {
                actionContext.Variables[kvp.Key] = kvp.Value;
            }

            foreach (var action in Actions)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    context.Logger?.LogWarning("Flow {FlowId} cancelled", Id);
                    break;
                }

                var result = await action.ExecuteAsync(actionContext, cancellationToken);
                if (!result.Success)
                {
                    throw new Exception($"Action failed: {result.Message}");
                }

                // Merge output variables back into flow context
                if (result.OutputVariables != null)
                {
                    foreach (var kvp in result.OutputVariables)
                    {
                        context.Variables[kvp.Key] = kvp.Value;
                        actionContext.Variables[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        public Task<RuleValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(Id))
                errors.Add("Flow ID is required");

            if (string.IsNullOrEmpty(Name))
                errors.Add("Flow name is required");

            if (Actions == null || Actions.Count == 0)
                errors.Add("Flow must have at least one action");

            return Task.FromResult(new RuleValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors.ToArray()
            });
        }
    }
}