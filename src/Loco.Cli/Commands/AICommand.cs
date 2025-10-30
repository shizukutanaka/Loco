using System;
using System.CommandLine;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Loco.Core.AI;
using Loco.Core.Workflow;
using Microsoft.Extensions.Logging;

namespace Loco.Cli.Commands
{
    /// <summary>
    /// AI-powered workflow generation command.
    /// AI搭載ワークフロー生成コマンド
    ///
    /// Solves Issues from research:
    /// - #2: Steep learning curve → Generate workflows from natural language
    /// - #3: Lack of beginner guidance → AI provides step-by-step guidance
    /// - #11: No AI assistant → Full AI integration with explanations in Japanese/English
    /// - #18: Low readability → AI explains workflows in plain language
    /// </summary>
    public class AICommand : Command
    {
        public AICommand() : base("ai", "AI-powered workflow generation / AI搭載ワークフロー生成")
        {
            var descriptionArgument = new Argument<string>(
                name: "description",
                description: "Natural language description / 自然言語での説明",
                getDefaultValue: () => "");

            var outputOption = new Option<string?>(
                aliases: new[] { "--output", "-o" },
                description: "Output file path / 出力先ファイル");

            var platformOption = new Option<string[]?>(
                aliases: new[] { "--platform", "-p" },
                description: "Target platforms / 対象プラットフォーム");

            var languageOption = new Option<string>(
                aliases: new[] { "--language", "-l" },
                getDefaultValue: () => "en",
                description: "Output language (en/ja) / 出力言語");

            AddArgument(descriptionArgument);
            AddOption(outputOption);
            AddOption(platformOption);
            AddOption(languageOption);

            this.SetHandler(GenerateWorkflowAsync, descriptionArgument, outputOption, platformOption, languageOption);
        }

        private async Task<int> GenerateWorkflowAsync(
            string description,
            string? outputPath,
            string[]? platforms,
            string language)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(description))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(language == "ja"
                        ? "使用例: Loco.Cli.exe ai \"毎朝9時に通知を送る\" --output morning.json"
                        : "Usage: Loco.Cli.exe ai \"Send me a notification every morning at 9am\" --output morning.json");
                    Console.ResetColor();
                    return 0;
                }

                Console.WriteLine(language == "ja"
                    ? "🤖 AIがワークフローを生成しています..."
                    : "🤖 AI is generating workflow...");
                Console.WriteLine();

                // Create logger
                using var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole().SetMinimumLevel(LogLevel.Warning);
                });
                var logger = loggerFactory.CreateLogger<WorkflowGenerator>();

                // Generate workflow
                var generator = new WorkflowGenerator(logger);
                var platformsList = platforms != null && platforms.Length > 0
                    ? new System.Collections.Generic.List<string>(platforms)
                    : null;

                var result = await generator.GenerateFromNaturalLanguageAsync(
                    description,
                    platformsList);

                if (!result.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(language == "ja"
                        ? $"❌ 生成失敗: {result.ErrorMessage}"
                        : $"❌ Generation failed: {result.ErrorMessage}");
                    Console.ResetColor();

                    if (result.Suggestions.Count > 0)
                    {
                        Console.WriteLine();
                        Console.WriteLine(language == "ja" ? "💡 提案:" : "💡 Suggestions:");
                        foreach (var suggestion in result.Suggestions)
                        {
                            Console.WriteLine($"  • {suggestion}");
                        }
                    }
                    return 1;
                }

                // Display result
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(language == "ja"
                    ? "✅ ワークフロー生成成功!"
                    : "✅ Workflow generated successfully!");
                Console.ResetColor();
                Console.WriteLine();

                Console.WriteLine(language == "ja" ? "📋 ワークフロー情報:" : "📋 Workflow Information:");
                Console.WriteLine($"  Name: {result.Workflow!.Name}");
                Console.WriteLine($"  Platforms: {string.Join(", ", result.Workflow.Platforms)}");
                Console.WriteLine($"  Confidence: {result.Confidence:P0}");
                Console.WriteLine($"  Template: {result.UsedTemplate}");
                Console.WriteLine();

                Console.WriteLine(language == "ja" ? "📝 説明:" : "📝 Explanation:");
                Console.WriteLine(result.Explanation);
                Console.WriteLine();

                // Save to file
                var json = JsonSerializer.Serialize(result.Workflow, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                if (!string.IsNullOrEmpty(outputPath))
                {
                    await File.WriteAllTextAsync(outputPath, json);
                    Console.WriteLine(language == "ja"
                        ? $"💾 保存先: {outputPath}"
                        : $"💾 Saved to: {outputPath}");
                }
                else
                {
                    Console.WriteLine(language == "ja" ? "📄 JSON:" : "📄 JSON:");
                    Console.WriteLine(json);
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.ResetColor();
                return 1;
            }
        }
    }
}
