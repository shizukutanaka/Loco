using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Loco.Core.Interfaces;
using Loco.Core.Models;
using Loco.Automation.Interfaces;

namespace Loco.Automation.Services;

/// <summary>
/// Main automation service implementing John Carmack's performance focus
/// </summary>
public class AutomationService : IAutomationService
{
    private readonly ILogger<AutomationService> _logger;
    private readonly IAutomationRuleEngine _automationRuleEngine;
    private readonly IRuleStore _ruleStore;
    private readonly Dictionary<string, IFlow> _flows = new();
    private readonly SemaphoreSlim _flowLock = new(1, 1);
    private CancellationTokenSource? _cancellationTokenSource;

    public AutomationService(ILogger<AutomationService> logger, IAutomationRuleEngine automationRuleEngine, IRuleStore ruleStore)
    {
        _logger = logger;
        _automationRuleEngine = automationRuleEngine;
        _ruleStore = ruleStore ?? throw new ArgumentNullException(nameof(ruleStore));
    }
    
    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await LoadSavedRulesAsync(_cancellationTokenSource.Token);
            _logger.LogInformation("Automation service started");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start automation service");
            return false;
        }
    }
    
    public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _logger.LogInformation("Automation service stopped");
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop automation service");
            return false;
        }
    }
    
    public async Task<bool> RegisterFlowAsync(IFlow flow, CancellationToken cancellationToken = default)
    {
        await _flowLock.WaitAsync(cancellationToken);
        try
        {
            if (_flows.ContainsKey(flow.Id))
            {
                _logger.LogWarning("Flow {FlowId} already registered", flow.Id);
                return false;
            }
            
            _flows[flow.Id] = flow;
            _logger.LogInformation("Flow {FlowId} registered successfully", flow.Id);
            return true;
        }
        finally
        {
            _flowLock.Release();
        }
    }
    
    public async Task<bool> UnregisterFlowAsync(string flowId, CancellationToken cancellationToken = default)
    {
        await _flowLock.WaitAsync(cancellationToken);
        try
        {
            if (_flows.Remove(flowId))
            {
                _logger.LogInformation("Flow {FlowId} unregistered", flowId);
                return true;
            }
            
            _logger.LogWarning("Flow {FlowId} not found", flowId);
            return false;
        }
        finally
        {
            _flowLock.Release();
        }
    }
    
    public async Task<IEnumerable<IFlow>> GetActiveFlowsAsync(CancellationToken cancellationToken = default)
    {
        await _flowLock.WaitAsync(cancellationToken);
        try
        {
            return new List<IFlow>(_flows.Values);
        }
        finally
        {
            _flowLock.Release();
        }
    }
    
    // JSON-based rule APIs
    public Task<RuleValidationResult> ValidateRuleJsonAsync(string json, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Task.FromResult(RuleValidationResult.Fail("Invalid JSON"));
        }
        try
        {
            var node = JsonNode.Parse(json);
            if (node is null)
                return Task.FromResult(RuleValidationResult.Fail("Invalid JSON"));
            return ValidateRuleJsonAsync(node, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ValidateRuleJsonAsync parse error");
            return Task.FromResult(RuleValidationResult.Fail("Invalid JSON"));
        }
    }

    public Task<RuleValidationResult> ValidateRuleJsonAsync(JsonNode node, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        try
        {
            var id = node?["id"]?.GetValue<string>();
            var name = node?["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id)) errors.Add("'id' is required");
            if (string.IsNullOrWhiteSpace(name)) errors.Add("'name' is required");

            // Basic structural hints (optional fields are allowed)
            // triggers/actions can be absent for minimal rule validation in CLI

            return Task.FromResult(errors.Count == 0
                ? RuleValidationResult.Ok()
                : RuleValidationResult.Fail(errors.ToArray()));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ValidateRuleJsonAsync validation error");
            return Task.FromResult(RuleValidationResult.Fail("Validation error"));
        }
    }

        public async Task<bool> AddRuleFromJsonAsync(string json, CancellationToken cancellationToken = default)
    {
        var totalSw = Stopwatch.StartNew();
        _logger.LogInformation("AddRuleFromJsonAsync(start): jsonLength={Length}", json?.Length ?? 0);

        var validateSw = Stopwatch.StartNew();
        var validationResult = await ValidateRuleJsonAsync(json, cancellationToken);
        validateSw.Stop();
        _logger.LogInformation("AddRuleFromJsonAsync(validate): isValid={IsValid} durationMs={Ms}", validationResult.IsValid, validateSw.ElapsedMilliseconds);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Rule validation failed: {Errors}", string.Join(", ", validationResult.Errors));
            return false;
        }

        try
        {
            var deserSw = Stopwatch.StartNew();
            var rule = JsonSerializer.Deserialize<AutomationDsl.Rule>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            deserSw.Stop();
            _logger.LogInformation("AddRuleFromJsonAsync(deserialize): durationMs={Ms}", deserSw.ElapsedMilliseconds);
            if (rule == null)
            {
                _logger.LogError("Failed to deserialize rule from JSON.");
                return false;
            }

            var added = await AddRuleToEngineWithTimeoutAsync(rule, cancellationToken);
            if (added)
            {
                try
                {
                    var persistSw = Stopwatch.StartNew();
                    await _ruleStore.SaveRuleAsync(rule.Id, json, cancellationToken);
                    persistSw.Stop();
                    _logger.LogInformation("AddRuleFromJsonAsync(persist): durationMs={Ms}", persistSw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist rule {RuleId}", rule.Id);
                }
            }
            totalSw.Stop();
            _logger.LogInformation("AddRuleFromJsonAsync(total): durationMs={Ms}", totalSw.ElapsedMilliseconds);
            return added;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding rule from JSON.");
            return false;
        }
    }

        public async Task<bool> AddRuleFromJsonAsync(JsonNode node, CancellationToken cancellationToken = default)
    {
        var validationResult = await ValidateRuleJsonAsync(node, cancellationToken);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Rule validation failed: {Errors}", string.Join(", ", validationResult.Errors));
            return false;
        }
        return await AddRuleFromJsonAsync(node.ToJsonString(), cancellationToken);
    }

    public async Task<bool> DeleteRuleAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
        {
            _logger.LogWarning("DeleteRuleAsync called with empty ruleId.");
            return false;
        }

        try
        {
            _logger.LogInformation("Deleting rule {RuleId}", ruleId);

            // Remove from the engine first
            var removedFromEngine = await _automationRuleEngine.DeleteRuleAsync(ruleId);
            if (!removedFromEngine)
            {
                _logger.LogWarning("Rule {RuleId} not found in the engine, but attempting to delete from store anyway.", ruleId);
            }

            // Then remove from the store
            await _ruleStore.DeleteRuleAsync(ruleId, cancellationToken);

            _logger.LogInformation("Successfully processed deletion for rule {RuleId}", ruleId);
            return true;
        }
        catch (Exception ex)
        {            _logger.LogError(ex, "Error deleting rule {RuleId}", ruleId);
            return false;
        }
    }

    private async Task LoadSavedRulesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var savedRulesJson = await _ruleStore.LoadAllRulesAsync(cancellationToken);
            if (savedRulesJson == null || !savedRulesJson.Any())
            {
                _logger.LogInformation("No saved automation rules found to load.");
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int loaded = 0;
            foreach (var ruleJson in savedRulesJson)
            {
                try
                {
                    // Validate JSON before attempting to deserialize
                    var validation = await ValidateRuleJsonAsync(ruleJson, cancellationToken);
                    if (!validation.IsValid)
                    {
                        _logger.LogWarning("Skipping invalid saved rule: {Errors}", string.Join(", ", validation.Errors ?? Array.Empty<string>()));
                        continue;
                    }

                    var rule = JsonSerializer.Deserialize<AutomationDsl.Rule>(ruleJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (rule == null || string.IsNullOrWhiteSpace(rule.Id)) continue;
                    if (!seen.Add(rule.Id)) continue;

                    var added = await AddRuleToEngineWithTimeoutAsync(rule, cancellationToken);
                    if (added) loaded++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load saved rule from JSON.");
                }
            }

            _logger.LogInformation("Loaded {Count} saved automation rules.", loaded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading saved automation rules");
        }
    }
    
    private async Task<bool> AddRuleToEngineWithTimeoutAsync(AutomationDsl.Rule rule, CancellationToken cancellationToken)
    {
        const int engineAddTimeoutMs = 10000;
        var addSw = Stopwatch.StartNew();
        var addTask = _automationRuleEngine.AddRuleAsync(rule, cancellationToken);
        var completedTask = await Task.WhenAny(addTask, Task.Delay(engineAddTimeoutMs, cancellationToken));

        if (completedTask != addTask)
        {
            _logger.LogWarning("AddRuleToEngineWithTimeoutAsync: Timed out after {TimeoutMs} ms for ruleId={RuleId}", engineAddTimeoutMs, rule.Id);
            return false;
        }

        var added = await addTask;
        addSw.Stop();
        _logger.LogInformation("AddRuleToEngineWithTimeoutAsync: added={Added} durationMs={Ms} ruleId={RuleId}", added, addSw.ElapsedMilliseconds, rule.Id);
        return added;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancellationTokenSource?.Dispose();
            _flowLock?.Dispose();
        }
    }
}