using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Loco.Core.IaC;
using Microsoft.Extensions.Logging;

namespace Loco.Cli.Commands;

/// <summary>
/// Command for Infrastructure as Code operations
/// </summary>
public class IacCommand : Command
{
    public IacCommand() : base("iac", "Infrastructure as Code operations")
    {
        // Deploy subcommand
        var deployCommand = new Command("deploy", "Deploy infrastructure from IaC file");
        var deployFileArg = new Argument<string>("file", "Path to infrastructure file (YAML or JSON)");
        var deployDryRunOption = new Option<bool>(new[] { "--dry-run", "-n" }, "Validate without deploying");
        var deployVerboseOption = new Option<bool>(new[] { "--verbose", "-v" }, "Show detailed output");
        deployCommand.AddArgument(deployFileArg);
        deployCommand.AddOption(deployDryRunOption);
        deployCommand.AddOption(deployVerboseOption);
        deployCommand.SetHandler(DeployAsync, deployFileArg, deployDryRunOption, deployVerboseOption);
        AddCommand(deployCommand);

        // Validate subcommand
        var validateCommand = new Command("validate", "Validate infrastructure definition");
        var validateFileArg = new Argument<string>("file", "Path to infrastructure file (YAML or JSON)");
        var validateDetailedOption = new Option<bool>(new[] { "--detailed", "-d" }, "Show detailed report");
        validateCommand.AddArgument(validateFileArg);
        validateCommand.AddOption(validateDetailedOption);
        validateCommand.SetHandler(ValidateAsync, validateFileArg, validateDetailedOption);
        AddCommand(validateCommand);

        // Generate subcommand
        var generateCommand = new Command("generate", "Generate IaC from existing workflows");
        var generateDirArg = new Argument<string>("directory", () => "workflows", "Directory to scan for workflows");
        var generateOutputOption = new Option<string>(new[] { "--output", "-o" }, "Output file path");
        var generateFormatOption = new Option<string>(new[] { "--format", "-f" }, () => "yaml", "Output format: yaml or json");
        generateCommand.AddArgument(generateDirArg);
        generateCommand.AddOption(generateOutputOption);
        generateCommand.AddOption(generateFormatOption);
        generateCommand.SetHandler(GenerateAsync, generateDirArg, generateOutputOption, generateFormatOption);
        AddCommand(generateCommand);

        // Convert subcommand
        var convertCommand = new Command("convert", "Convert between YAML and JSON");
        var convertInputArg = new Argument<string>("input", "Input file path");
        var convertOutputArg = new Argument<string>("output", "Output file path");
        convertCommand.AddArgument(convertInputArg);
        convertCommand.AddArgument(convertOutputArg);
        convertCommand.SetHandler(ConvertAsync, convertInputArg, convertOutputArg);
        AddCommand(convertCommand);
    }

    private async Task<int> DeployAsync(string filePath, bool dryRun, bool verbose)
    {
        try
        {
            Console.WriteLine($"Loading infrastructure from: {filePath}");
            Console.WriteLine();

            if (!File.Exists(filePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Infrastructure file not found: {filePath}");
                Console.ResetColor();
                return 1;
            }

            // Create logger
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
            });
            var logger = loggerFactory.CreateLogger<InfrastructureAsCode>();

            var iac = new InfrastructureAsCode(logger);

            // Load infrastructure
            var infrastructure = Path.GetExtension(filePath).ToLowerInvariant() == ".yaml" || Path.GetExtension(filePath).ToLowerInvariant() == ".yml"
                ? await iac.LoadFromYamlAsync(filePath)
                : await iac.LoadFromJsonAsync(filePath);

            // Validate
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Validating infrastructure...");
            Console.ResetColor();

            var validation = iac.Validate(infrastructure);

            if (!validation.IsValid)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n✗ Validation failed with {validation.Errors.Count} error(s):");
                foreach (var error in validation.Errors)
                {
                    Console.WriteLine($"  • {error}");
                }
                Console.ResetColor();
                return 1;
            }

            if (validation.Warnings.Any())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n⚠ Validation warnings ({validation.Warnings.Count}):");
                foreach (var warning in validation.Warnings)
                {
                    Console.WriteLine($"  • {warning}");
                }
                Console.ResetColor();
                Console.WriteLine();
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Validation passed");
            Console.ResetColor();
            Console.WriteLine();

            if (dryRun)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Dry run mode - skipping deployment");
                Console.ResetColor();

                // Show what would be deployed
                Console.WriteLine("\nResources to be deployed:");
                if (infrastructure.Workflows != null)
                {
                    Console.WriteLine($"  Workflows: {infrastructure.Workflows.Count}");
                    foreach (var workflow in infrastructure.Workflows)
                    {
                        Console.WriteLine($"    • {workflow.Name}");
                    }
                }

                if (infrastructure.Secrets != null)
                {
                    Console.WriteLine($"  Secrets: {infrastructure.Secrets.Count}");
                    foreach (var secret in infrastructure.Secrets)
                    {
                        Console.WriteLine($"    • {secret.Name}");
                    }
                }

                if (infrastructure.Monitoring?.Enabled == true)
                {
                    Console.WriteLine("  Monitoring: Enabled");
                }

                return 0;
            }

            // Deploy
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Deploying infrastructure...");
            Console.ResetColor();

            var result = await iac.ApplyAsync(infrastructure);

            if (!result.Success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n✗ Deployment failed:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"  • {error}");
                }
                Console.ResetColor();
                return 1;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n✓ Deployment successful!");
            Console.WriteLine($"  Duration: {result.Duration.TotalSeconds:F2}s");
            Console.WriteLine($"  Resources deployed: {result.DeployedResources.Count}");
            Console.ResetColor();

            if (verbose)
            {
                Console.WriteLine("\nDeployed resources:");
                foreach (var resource in result.DeployedResources)
                {
                    Console.WriteLine($"  • {resource}");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private async Task<int> ValidateAsync(string filePath, bool detailed)
    {
        try
        {
            Console.WriteLine($"Validating infrastructure: {filePath}");
            Console.WriteLine();

            if (!File.Exists(filePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Infrastructure file not found: {filePath}");
                Console.ResetColor();
                return 1;
            }

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Warning);
            });
            var logger = loggerFactory.CreateLogger<InfrastructureAsCode>();

            var iac = new InfrastructureAsCode(logger);

            // Load infrastructure
            var infrastructure = Path.GetExtension(filePath).ToLowerInvariant() == ".yaml" || Path.GetExtension(filePath).ToLowerInvariant() == ".yml"
                ? await iac.LoadFromYamlAsync(filePath)
                : await iac.LoadFromJsonAsync(filePath);

            // Validate
            var validation = iac.Validate(infrastructure);

            if (!validation.IsValid)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Validation failed with {validation.Errors.Count} error(s):");
                foreach (var error in validation.Errors)
                {
                    Console.WriteLine($"  • {error}");
                }
                Console.ResetColor();
                Console.WriteLine();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Validation passed");
                Console.ResetColor();
                Console.WriteLine();
            }

            if (validation.Warnings.Any())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠ Warnings ({validation.Warnings.Count}):");
                foreach (var warning in validation.Warnings)
                {
                    Console.WriteLine($"  • {warning}");
                }
                Console.ResetColor();
                Console.WriteLine();
            }

            if (detailed && validation.IsValid)
            {
                Console.WriteLine("Infrastructure summary:");
                Console.WriteLine($"  Version: {infrastructure.Version}");

                if (infrastructure.Workflows != null)
                {
                    Console.WriteLine($"\n  Workflows ({infrastructure.Workflows.Count}):");
                    foreach (var workflow in infrastructure.Workflows)
                    {
                        Console.WriteLine($"    • {workflow.Name}");
                        if (!string.IsNullOrEmpty(workflow.Description))
                            Console.WriteLine($"      {workflow.Description}");
                        Console.WriteLine($"      Steps: {workflow.Steps?.Count ?? 0}");
                        if (!string.IsNullOrEmpty(workflow.Schedule))
                            Console.WriteLine($"      Schedule: {workflow.Schedule}");
                    }
                }

                if (infrastructure.Secrets != null)
                {
                    Console.WriteLine($"\n  Secrets ({infrastructure.Secrets.Count}):");
                    foreach (var secret in infrastructure.Secrets)
                    {
                        Console.WriteLine($"    • {secret.Name}");
                        Console.WriteLine($"      Source: {secret.Source}");
                    }
                }

                if (infrastructure.Variables != null && infrastructure.Variables.Any())
                {
                    Console.WriteLine($"\n  Variables ({infrastructure.Variables.Count}):");
                    foreach (var variable in infrastructure.Variables)
                    {
                        Console.WriteLine($"    • {variable.Key} = {variable.Value}");
                    }
                }

                if (infrastructure.Monitoring != null)
                {
                    Console.WriteLine($"\n  Monitoring:");
                    Console.WriteLine($"    Enabled: {infrastructure.Monitoring.Enabled}");
                    if (infrastructure.Monitoring.Alerts != null)
                        Console.WriteLine($"    Alerts: {infrastructure.Monitoring.Alerts.Count}");
                }
            }

            return validation.IsValid ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private async Task<int> GenerateAsync(string directory, string? outputFile, string format)
    {
        try
        {
            Console.WriteLine($"Generating infrastructure definition from: {directory}");
            Console.WriteLine();

            if (!Directory.Exists(directory))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Directory not found: {directory}");
                Console.ResetColor();
                return 1;
            }

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            var logger = loggerFactory.CreateLogger<InfrastructureAsCode>();

            var iac = new InfrastructureAsCode(logger);

            // Generate infrastructure
            var infrastructure = iac.GenerateFromExisting(directory);

            // Determine output file
            var output = outputFile ?? (format.ToLowerInvariant() == "json" ? "infrastructure.json" : "infrastructure.yaml");

            // Save to file
            if (format.ToLowerInvariant() == "json")
            {
                var json = JsonSerializer.Serialize(infrastructure, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await File.WriteAllTextAsync(output, json);
            }
            else
            {
                await iac.SaveToYamlAsync(infrastructure, output);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Infrastructure definition generated: {output}");
            Console.WriteLine($"  Workflows: {infrastructure.Workflows?.Count ?? 0}");
            Console.ResetColor();

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private async Task<int> ConvertAsync(string inputFile, string outputFile)
    {
        try
        {
            Console.WriteLine($"Converting: {inputFile} → {outputFile}");
            Console.WriteLine();

            if (!File.Exists(inputFile))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Input file not found: {inputFile}");
                Console.ResetColor();
                return 1;
            }

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Warning);
            });
            var logger = loggerFactory.CreateLogger<InfrastructureAsCode>();

            var iac = new InfrastructureAsCode(logger);

            // Load from input file
            var inputExt = Path.GetExtension(inputFile).ToLowerInvariant();
            var outputExt = Path.GetExtension(outputFile).ToLowerInvariant();

            var infrastructure = (inputExt == ".yaml" || inputExt == ".yml")
                ? await iac.LoadFromYamlAsync(inputFile)
                : await iac.LoadFromJsonAsync(inputFile);

            // Save to output file
            if (outputExt == ".yaml" || outputExt == ".yml")
            {
                await iac.SaveToYamlAsync(infrastructure, outputFile);
            }
            else
            {
                var json = JsonSerializer.Serialize(infrastructure, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await File.WriteAllTextAsync(outputFile, json);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Converted successfully: {outputFile}");
            Console.ResetColor();

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    public async Task<int> InvokeAsync(string[] args)
    {
        // Use System.CommandLine's InvokeAsync method
        return await ((Command)this).InvokeAsync(args);
    }
}
