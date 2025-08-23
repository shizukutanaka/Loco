using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Loco.Automation.Interfaces;
using Loco.Core.Repository;
using Microsoft.Extensions.Logging;

namespace Loco.Automation.Services;

/// <summary>
/// An implementation of IRuleStore that uses the IUnitOfWork repository pattern for persistence.
/// </summary>
public class DatabaseRuleStore : IRuleStore
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DatabaseRuleStore> _logger;

    public DatabaseRuleStore(IUnitOfWork unitOfWork, ILogger<DatabaseRuleStore> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> LoadAllRulesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading all rules from database.");
        var settings = await _unitOfWork.Settings.GetOrCreateAsync("automation");

        var collected = new List<string>();

        // Legacy: automation.savedRules (array of objects)
        if (settings.Value.TryGetProperty("savedRules", out var savedRules) && savedRules.ValueKind == JsonValueKind.Array)
        {
            foreach (var ruleElem in savedRules.EnumerateArray())
            {
                try
                {
                    collected.Add(ruleElem.GetRawText());
                }
                catch
                {
                    // ignore malformed entries
                }
            }
        }

        // Current: automation.savedRulesJson (array of strings)
        if (settings.Value.TryGetProperty("savedRulesJson", out var savedRulesJson) && savedRulesJson.ValueKind == JsonValueKind.Array)
        {
            foreach (var ruleJson in savedRulesJson.EnumerateArray())
            {
                collected.Add(ruleJson.GetString() ?? "");
            }
        }

        // De-duplicate by rule id (case-insensitive). If id is missing, fall back to content-based uniqueness.
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenContent = new HashSet<string>(StringComparer.Ordinal);
        var deduped = new List<string>();

        foreach (var r in collected)
        {
            if (string.IsNullOrWhiteSpace(r)) continue;

            string? id = null;
            try
            {
                using var doc = JsonDocument.Parse(r);
                if (doc.RootElement.TryGetProperty("id", out var idElem))
                {
                    id = idElem.GetString();
                }
            }
            catch
            {
                // ignore parse errors for id extraction; we'll do content-based dedupe
            }

            if (!string.IsNullOrWhiteSpace(id))
            {
                if (seenIds.Add(id))
                {
                    deduped.Add(r);
                }
            }
            else
            {
                if (seenContent.Add(r))
                {
                    deduped.Add(r);
                }
            }
        }

        _logger.LogInformation("Loaded {Count} rules from database (deduped).", deduped.Count);
        return deduped;
    }

    /// <inheritdoc />
    public async Task SaveRuleAsync(string ruleId, string ruleJson, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Saving rule {RuleId} to database.", ruleId);
        var settings = await _unitOfWork.Settings.GetOrCreateAsync("automation");

        var rules = settings.Value.TryGetProperty("savedRulesJson", out var savedRulesJson) && savedRulesJson.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<string>>(savedRulesJson.GetRawText()) ?? new List<string>()
            : new List<string>();

        // Remove existing rule with the same ID, if any
        rules.RemoveAll(r => 
        {
            try 
            {
                var doc = JsonDocument.Parse(r);
                return doc.RootElement.TryGetProperty("id", out var idElem) && idElem.GetString() == ruleId;
            }
            catch
            {
                return false; // Ignore invalid JSON entries
            }
        });

        rules.Add(ruleJson);

        var newJson = JsonSerializer.Serialize(rules);
        settings.Value = JsonDocument.Parse(JsonSerializer.Serialize(new { savedRulesJson = rules })).RootElement;

        await _unitOfWork.Settings.UpdateAsync(settings);
        await _unitOfWork.CompleteAsync();
        _logger.LogInformation("Rule {RuleId} saved successfully.", ruleId);
    }

    /// <inheritdoc />
    public async Task DeleteRuleAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting rule {RuleId} from database.", ruleId);
        var settings = await _unitOfWork.Settings.GetOrCreateAsync("automation");

        if (!settings.Value.TryGetProperty("savedRulesJson", out var savedRulesJson) || savedRulesJson.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("No rules found to delete.");
            return;
        }

        var rules = JsonSerializer.Deserialize<List<string>>(savedRulesJson.GetRawText()) ?? new List<string>();

        var removedCount = rules.RemoveAll(r =>
        {
            try
            {
                var doc = JsonDocument.Parse(r);
                return doc.RootElement.TryGetProperty("id", out var idElem) && idElem.GetString() == ruleId;
            }
            catch
            {
                return false;
            }
        });

        if (removedCount > 0)
        {
            settings.Value = JsonDocument.Parse(JsonSerializer.Serialize(new { savedRulesJson = rules })).RootElement;
            await _unitOfWork.Settings.UpdateAsync(settings);
            await _unitOfWork.CompleteAsync();
            _logger.LogInformation("Rule {RuleId} deleted successfully.", ruleId);
        }
        else
        {
            _logger.LogWarning("Rule {RuleId} not found for deletion.", ruleId);
        }
    }
}
