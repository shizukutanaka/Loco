using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Loco.Automation.Interfaces;
using Loco.Core.FlowComposer;
using Loco.Llm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loco.Cli.Commands;

public class BuildCommand : Command
{
    public BuildCommand()
    {
        var outputOption = new Option<string>(name: "--output", description: "保存先ファイルパス (.json) / Output file path (.json)");
        AddOption(outputOption);

        this.SetHandler(async (
            FlowComposerBuilder composerBuilder,
            ILogger<BuildCommand> logger,
            IAutomationService automationService,
            IRuleManipulationService ruleService,
            IOptions<LlmConfiguration> llmOptions,
            string modelId,
            string output
        ) =>
        {
            await HandleAsync(composerBuilder, logger, automationService, ruleService, llmOptions, modelId, output);
        }, Program.ModelIdOption, outputOption);
    }

    private static async Task HandleAsync(
        FlowComposerBuilder composerBuilder,
        ILogger<BuildCommand> logger,
        IAutomationService automationService,
        IRuleManipulationService ruleService,
        IOptions<LlmConfiguration> llmOptions,
        string modelIdArg,
        string? outputPath)
    {
        try
        {
            logger.LogInformation("Welcome to the Flow Builder");
            logger.LogInformation("=====================================");

            Console.Write("Enter flow name: ");
            var flowName = Console.ReadLine() ?? "新規フロー";

            Console.Write("Enter description: ");
            var description = Console.ReadLine();

            // Use Flow Composer for building
            var flowBuilder = composerBuilder.StartFlow(flowName, description);

            // Display categories
            logger.LogInformation("\nAvailable Categories:");
            var categories = composerBuilder.GetCategories();
            for (int i = 0; i < categories.Count; i++)
            {
                var cat = categories[i];
                logger.LogInformation("  {Index}. {Icon} {Name} - {Description}", i + 1, cat.Icon, cat.Name, cat.Description);
            }

            bool addingComponents = true;
            while (addingComponents)
            {
                Console.Write("\nSelect a category number (or 0 to finish): ");
                if (int.TryParse(Console.ReadLine(), out int catIndex) && catIndex > 0 && catIndex <= categories.Count)
                {
                    var category = categories[catIndex - 1];
                    logger.LogInformation("\nComponents in {CategoryName}:", category.Name);

                    var components = category.Components.ToList();
                    for (int i = 0; i < components.Count; i++)
                    {
                        var comp = components[i];
                        logger.LogInformation("  {Index}. {Icon} {Name} - {Description}", i + 1, comp.Icon, comp.Name, comp.Description);
                    }

                    Console.Write("\nSelect a component number: ");
                    if (int.TryParse(Console.ReadLine(), out int compIndex) && compIndex > 0 && compIndex <= components.Count)
                    {
                        var component = components[compIndex - 1];
                        var parameters = new Dictionary<string, object>();

                        logger.LogInformation("\nConfiguring parameters for {ComponentName}:", component.Name);
                        foreach (var param in component.Parameters)
                        {
                            Console.Write($"  {param.DisplayName}");
                            if (param.Required) Console.Write(" (必須)");
                            if (param.Default != null) Console.Write($" [デフォルト: {param.Default}]");
                            Console.Write(": ");

                            var input = Console.ReadLine();
                            if (!string.IsNullOrEmpty(input))
                            {
                                parameters[param.Name] = ConvertParameterValue(input, param.Type);
                            }
                            else if (param.Default != null)
                            {
                                parameters[param.Name] = param.Default;
                            }
                            else if (param.Required)
                            {
                                logger.LogWarning("This parameter is required. Please provide a value.");
                                input = Console.ReadLine();
                                parameters[param.Name] = ConvertParameterValue(input, param.Type);
                            }
                        }

                        // Add component to flow
                        switch (component.Type)
                        {
                            case ComponentType.Trigger:
                                flowBuilder.AddTrigger(component.Id, parameters);
                                logger.LogInformation("Added trigger '{ComponentName}'", component.Name);
                                break;
                            case ComponentType.Condition:
                                flowBuilder.AddCondition(component.Id, parameters);
                                logger.LogInformation("Added condition '{ComponentName}'", component.Name);
                                break;
                            case ComponentType.Action:
                                flowBuilder.AddAction(component.Id, parameters);
                                logger.LogInformation("Added action '{ComponentName}'", component.Name);
                                break;
                        }
                    }
                }
                else if (catIndex == 0)
                {
                    addingComponents = false;
                }
            }

            // Build and save flow
            var flow = flowBuilder.Build();
            var json = flowBuilder.ToJson();

            // Inject stable model ID when provided
            var effectiveModelId = modelIdArg ?? llmOptions.Value.Model;
            if (!string.IsNullOrWhiteSpace(effectiveModelId))
            {
                json = ruleService.InjectModelId(json, effectiveModelId, logger);
            }

            // Validate generated JSON
            var validation = await automationService.ValidateRuleJsonAsync(json);
            if (!validation.IsValid)
            {
                var errors = validation.Errors is null ? "unknown error" : string.Join(", ", validation.Errors);
                logger.LogWarning("Generated flow is invalid: {Errors}", errors);
                Console.Write("Do you want to save it anyway? (y/N): ");
                var decision = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
                if (decision != "y" && decision != "yes")
                {
                    logger.LogInformation("Aborted saving due to validation failure.");
                    return;
                }
            }

            string? filename = outputPath;
            if (string.IsNullOrWhiteSpace(filename))
            {
                Console.Write("\nEnter filename to save (.json): ");
                filename = Console.ReadLine();
            }
            if (string.IsNullOrEmpty(filename))
            {
                filename = $"{flow.Name.Replace(" ", "_")}.json";
            }
            if (!filename.EndsWith(".json"))
            {
                filename += ".json";
            }

            await File.WriteAllTextAsync(filename, json);
            logger.LogInformation("Flow saved to: {FileName}", filename);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred in the flow builder.");
        }
    }

    private static object ConvertParameterValue(string input, string type)
    {
        return type.ToLower() switch
        {
            "number" => int.TryParse(input, out var num) ? num : 0,
            "boolean" => bool.TryParse(input, out var b) ? b : input.ToLower() == "yes" || input.ToLower() == "true",
            "slider" => double.TryParse(input, out var d) ? d : 0.0,
            _ => input
        };
    }
}
