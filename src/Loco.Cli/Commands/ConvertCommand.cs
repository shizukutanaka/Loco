using System;
using System.CommandLine;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Loco.Core.Services;
using Microsoft.Extensions.Logging;

namespace Loco.Cli.Commands;

public class ConvertCommand : Command
{
    public ConvertCommand()
        : base("convert", "自然言語をフローに変換")
    {
        var textOption = new Option<string>(
            name: "--text",
            description: "変換する自然言語テキスト")
        {
            IsRequired = true
        };
        AddOption(textOption);

        this.SetHandler(async (INaturalLanguageRuleService converter, ILogger<ConvertCommand> logger, string text) =>
        {
            await HandleAsync(converter, logger, text);
        }, textOption);
    }

    private static async Task HandleAsync(INaturalLanguageRuleService converterService, ILogger<ConvertCommand> logger, string text)
    {
        try
        {
            logger.LogInformation("Converting natural language: \"{Text}\"", text);

            var json = await converterService.ConvertTextToRuleJsonAsync(text);
            if (string.IsNullOrWhiteSpace(json))
            {
                logger.LogError("Conversion failed. Please review the input text.");
                return;
            }

            try
            {
                var rule = JsonSerializer.Deserialize<AutomationDsl.Rule>(json);
                if (rule == null)
                {
                    logger.LogError("Generated JSON is invalid.");
                    return;
                }
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "JSON validation failed.");
                return;
            }

            var filename = $"nl_rule_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            await File.WriteAllTextAsync(filename, json);
            logger.LogInformation("Conversion complete. Saved to {FileName}", filename);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during conversion.");
        }
    }
}
