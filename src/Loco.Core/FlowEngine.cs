using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Loco.Core.Interfaces;
using Loco.Core.Models;

namespace Loco.Core;

/// <summary>
/// Core flow engine implementation - John Carmack performance focus
/// Memory-efficient, lock-free where possible
/// </summary>
public sealed class FlowEngine : IFlowEngine
{
    private readonly ILogger<FlowEngine> _logger;
    private readonly ConcurrentDictionary<string, IFlow> _flows = new();
    private readonly SemaphoreSlim _executionLock = new(Environment.ProcessorCount * 2);
    
    public FlowEngine(ILogger<FlowEngine> logger)
    {
        _logger = logger;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> RegisterFlowAsync(IFlow flow, CancellationToken cancellationToken = default)
    {
        if (flow is null) throw new ArgumentNullException(nameof(flow));
        
        _flows.AddOrUpdate(flow.Id, flow, (_, _) => flow);
        
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Flow {FlowId} registered", flow.Id);
        
        return Task.FromResult(true);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> UnregisterFlowAsync(string flowId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_flows.TryRemove(flowId, out _));
    }
    
    public async Task<bool> ExecuteFlowAsync(string flowId, FlowContext context, CancellationToken cancellationToken = default)
    {
        if (!_flows.TryGetValue(flowId, out var flow))
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Flow {FlowId} not found", flowId);
            return false;
        }
        
        await _executionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await flow.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
                _logger.LogError(ex, "Error executing flow {FlowId}", flowId);
            return false;
        }
        finally
        {
            _executionLock.Release();
        }
    }
    
    public async Task<RuleValidationResult> ValidateFlowAsync(string flowId, CancellationToken cancellationToken = default)
    {
        if (!_flows.TryGetValue(flowId, out var flow))
        {
            return new RuleValidationResult
            {
                IsValid = false,
                Errors = new[] { $"Flow {flowId} not found" }
            };
        }
        
        return await flow.ValidateAsync(cancellationToken);
    }
    
    public async Task RunAsync(IFlow flow, FlowContext context, CancellationToken cancellationToken = default)
    {
        if (flow is null) throw new ArgumentNullException(nameof(flow));
        
        context ??= new FlowContext();
        
        await _executionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await flow.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (_logger.IsEnabled(LogLevel.Error))
        {
            _logger.LogError(ex, "Error running flow {FlowId}", flow.Id);
            throw;
        }
        finally
        {
            _executionLock.Release();
        }
    }
    
    public void Dispose()
    {
        _executionLock?.Dispose();
    }
}