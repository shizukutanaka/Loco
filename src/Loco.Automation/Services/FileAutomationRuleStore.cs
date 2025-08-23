using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Loco.Automation.Interfaces;

namespace Loco.Automation.Services;

/// <summary>
/// File-based persistence for automation rules. Stores a JSON array of Rule objects.
/// Default path recommended: $(AppContext.BaseDirectory)/data/rules.json
/// </summary>
public sealed class FileAutomationRuleStore : IAutomationRuleStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public FileAutomationRuleStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("filePath is required", nameof(filePath));
        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<IReadOnlyList<AutomationDsl.Rule>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
            {
                return Array.Empty<AutomationDsl.Rule>();
            }

            await using var stream = File.OpenRead(_filePath);
            var rules = await JsonSerializer.DeserializeAsync<List<AutomationDsl.Rule>>(stream, _jsonOptions, cancellationToken)
                        ?? new List<AutomationDsl.Rule>();

            // De-duplicate by Id
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deduped = new List<AutomationDsl.Rule>();
            foreach (var r in rules)
            {
                if (r == null || string.IsNullOrWhiteSpace(r.Id)) continue;
                if (seen.Add(r.Id)) deduped.Add(r);
            }
            return deduped;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveOrUpdateAsync(AutomationDsl.Rule rule, CancellationToken cancellationToken = default)
    {
        if (rule == null) throw new ArgumentNullException(nameof(rule));
        if (string.IsNullOrWhiteSpace(rule.Id)) throw new ArgumentException("Rule.Id is required");

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Ensure directory exists
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            List<AutomationDsl.Rule> rules;
            if (File.Exists(_filePath))
            {
                await using var read = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                rules = await JsonSerializer.DeserializeAsync<List<AutomationDsl.Rule>>(read, _jsonOptions, cancellationToken) ?? new List<AutomationDsl.Rule>();
            }
            else
            {
                rules = new List<AutomationDsl.Rule>();
            }

            var idx = rules.FindIndex(r => string.Equals(r?.Id, rule.Id, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                rules[idx] = rule;
            }
            else
            {
                rules.Add(rule);
            }

            await using var write = File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(write, rules, _jsonOptions, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }
}
