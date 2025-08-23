using System;
using System.CommandLine;
using System.Text.Json;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Loco.Llm;

namespace Loco.Cli.Commands;

public class LlmConfigCommand : Command
{
    public LlmConfigCommand()
        : base("llm", "LLM 設定の確認と管理コマンド")
    {
        var showCmd = new Command("config", "有効な LLM 設定を表示します（APIキーは一部マスク）");
        var jsonOption = new Option<bool>(name: "--json", description: "JSON形式で出力 / Output as JSON");
        showCmd.AddOption(jsonOption);
        showCmd.SetHandler((IHost host, bool json) =>
        {
            Handle(host, json, Console.Out);
        }, jsonOption);
        AddCommand(showCmd);
    }

    internal static void Handle(IHost host, bool json, TextWriter writer)
    {
        var opts = host.Services.GetRequiredService<IOptions<LlmConfiguration>>().Value;
        var presetEnv = Environment.GetEnvironmentVariable("LOCO_LLM__PRESET")
            ?? Environment.GetEnvironmentVariable("LOCO_LLM_PRESET");
        var hasApiKey = !string.IsNullOrWhiteSpace(opts.ApiKey);

        if (json)
        {
            var payload = new
            {
                provider = opts.Provider,
                model = opts.Model,
                apiEndpoint = opts.ApiEndpoint,
                maxTokens = opts.MaxTokens,
                temperature = opts.Temperature,
                httpTimeoutMs = opts.HttpTimeoutMs,
                apiKey = hasApiKey ? "redacted" : string.Empty,
                hasApiKey = hasApiKey,
                preset = presetEnv
            };
            writer.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        writer.WriteLine("LLM Configuration (effective):");
        writer.WriteLine($"  Provider     : {opts.Provider}");
        writer.WriteLine($"  Model        : {opts.Model}");
        writer.WriteLine($"  ApiEndpoint  : {opts.ApiEndpoint}");
        writer.WriteLine($"  MaxTokens    : {opts.MaxTokens}");
        writer.WriteLine($"  Temperature  : {opts.Temperature}");
        writer.WriteLine($"  HttpTimeoutMs: {opts.HttpTimeoutMs}");
        writer.WriteLine($"  ApiKey       : {Redact(opts.ApiKey)}");
        writer.WriteLine($"  HasApiKey    : {hasApiKey}");
        writer.WriteLine($"  Preset       : {presetEnv}");
    }

    private static string Redact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "(empty)";
        var visible = Math.Min(4, value.Length);
        return new string('*', Math.Max(0, value.Length - visible)) + value[^visible..];
    }
}
