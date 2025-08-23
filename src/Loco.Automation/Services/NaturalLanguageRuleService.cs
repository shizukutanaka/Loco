using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Automation.Interfaces;
using Loco.Core.Models;

namespace Loco.Automation.Services;

/// <summary>
/// Natural language to rule JSON service.
/// Uses the simple keyword-based converter first; can be extended to use LLM.
/// Returns JSON in the Automation DSL schema (AutomationDsl.Rule).
/// </summary>
public class NaturalLanguageRuleService : INaturalLanguageRuleService
{
    private readonly ILogger<NaturalLanguageRuleService> _logger;
    private readonly NaturalLanguageToDslConverter _converter;

    public NaturalLanguageRuleService(
        ILogger<NaturalLanguageRuleService> logger,
        NaturalLanguageToDslConverter converter)
    {
        _logger = logger;
        _converter = converter;
    }

    public async Task<string> ConvertTextToRuleJsonAsync(string text, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Converting natural language to rule JSON");
        var result = await _converter.ConvertAsync(text);
        if (!result.Success || result.Rules == null || result.Rules.Length == 0)
        {
            _logger.LogWarning("Conversion failed");
            return string.Empty;
        }

        var rule = result.Rules[0];
        var json = JsonSerializer.Serialize(rule, new JsonSerializerOptions
        {
            WriteIndented = false
        });
        return json;
    }
}
