using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Plugins.Samples
{
    /// <summary>
    /// Sample plugin demonstrating the plugin architecture
    /// </summary>
    public class SamplePlugin : IPlugin
    {
        private ILogger _logger;
        private readonly Dictionary<string, Func<Dictionary<string, object>, Task<object>>> _commands;

        public SamplePlugin()
        {
            Metadata = new PluginMetadata
            {
                Id = "sample-plugin-001",
                Name = "Sample Plugin",
                Description = "A sample plugin demonstrating the plugin architecture",
                Version = "1.0.0",
                Author = "Loco Team",
                Website = "https://github.com/loco",
                Dependencies = new string[] { },
                Properties = new Dictionary<string, string>
                {
                    ["Category"] = "Sample",
                    ["License"] = "MIT"
                }
            };

            _commands = new Dictionary<string, Func<Dictionary<string, object>, Task<object>>>
            {
                ["hello"] = HelloCommand,
                ["calculate"] = CalculateCommand,
                ["process"] = ProcessCommand
            };
        }

        public PluginMetadata Metadata { get; private set; }

        public async Task InitializeAsync(PluginInitializationContext context)
        {
            _logger = context.Logger;
            _logger?.LogInformation("Sample plugin initializing...");

            // Perform any initialization tasks
            await Task.Delay(100); // Simulate initialization

            _logger?.LogInformation("Sample plugin initialized successfully");
        }

        public async Task ShutdownAsync()
        {
            _logger?.LogInformation("Sample plugin shutting down...");

            // Cleanup resources
            await Task.Delay(50); // Simulate cleanup

            _logger?.LogInformation("Sample plugin shut down successfully");
        }

        public async Task<PluginCommandResult> ExecuteAsync(string command, Dictionary<string, object> parameters)
        {
            try
            {
                if (!_commands.TryGetValue(command, out var commandFunc))
                {
                    return new PluginCommandResult
                    {
                        Success = false,
                        Error = $"Unknown command: {command}"
                    };
                }

                var result = await commandFunc(parameters);

                return new PluginCommandResult
                {
                    Success = true,
                    Result = result
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing command {Command}", command);
                return new PluginCommandResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public IEnumerable<string> GetSupportedCommands()
        {
            return _commands.Keys;
        }

        // Command implementations
        private async Task<object> HelloCommand(Dictionary<string, object> parameters)
        {
            var name = parameters.ContainsKey("name") ? parameters["name"].ToString() : "World";
            await Task.Delay(10);
            return $"Hello, {name}! This is the Sample Plugin.";
        }

        private async Task<object> CalculateCommand(Dictionary<string, object> parameters)
        {
            if (!parameters.ContainsKey("operation") || !parameters.ContainsKey("a") || !parameters.ContainsKey("b"))
            {
                throw new ArgumentException("Missing required parameters: operation, a, b");
            }

            var operation = parameters["operation"].ToString();
            var a = Convert.ToDouble(parameters["a"]);
            var b = Convert.ToDouble(parameters["b"]);

            await Task.Delay(10);

            return operation switch
            {
                "add" => a + b,
                "subtract" => a - b,
                "multiply" => a * b,
                "divide" => b != 0 ? a / b : throw new DivideByZeroException(),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };
        }

        private async Task<object> ProcessCommand(Dictionary<string, object> parameters)
        {
            if (!parameters.ContainsKey("data"))
            {
                throw new ArgumentException("Missing required parameter: data");
            }

            var data = parameters["data"].ToString();
            
            // Simulate processing
            await Task.Delay(100);

            return new
            {
                Original = data,
                Processed = data.ToUpper(),
                Length = data.Length,
                ProcessedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Another sample plugin for testing multiple plugin loading
    /// </summary>
    public class AdvancedSamplePlugin : IPlugin
    {
        private ILogger _logger;

        public AdvancedSamplePlugin()
        {
            Metadata = new PluginMetadata
            {
                Id = "advanced-sample-plugin-001",
                Name = "Advanced Sample Plugin",
                Description = "An advanced sample plugin with more features",
                Version = "1.0.0",
                Author = "Loco Team",
                Website = "https://github.com/loco",
                Dependencies = new[] { "sample-plugin-001" },
                Properties = new Dictionary<string, string>
                {
                    ["Category"] = "Advanced",
                    ["License"] = "MIT",
                    ["RequiresAdmin"] = "false"
                }
            };
        }

        public PluginMetadata Metadata { get; private set; }

        public async Task InitializeAsync(PluginInitializationContext context)
        {
            _logger = context.Logger;
            _logger?.LogInformation("Advanced sample plugin initializing...");
            await Task.Delay(100);
            _logger?.LogInformation("Advanced sample plugin initialized");
        }

        public async Task ShutdownAsync()
        {
            _logger?.LogInformation("Advanced sample plugin shutting down...");
            await Task.Delay(50);
        }

        public async Task<PluginCommandResult> ExecuteAsync(string command, Dictionary<string, object> parameters)
        {
            try
            {
                object result = command switch
                {
                    "analyze" => await AnalyzeData(parameters),
                    "transform" => await TransformData(parameters),
                    "report" => await GenerateReport(parameters),
                    _ => throw new NotSupportedException($"Command not supported: {command}")
                };

                return new PluginCommandResult
                {
                    Success = true,
                    Result = result
                };
            }
            catch (Exception ex)
            {
                return new PluginCommandResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public IEnumerable<string> GetSupportedCommands()
        {
            return new[] { "analyze", "transform", "report" };
        }

        private async Task<object> AnalyzeData(Dictionary<string, object> parameters)
        {
            await Task.Delay(50);
            
            var data = parameters.ContainsKey("data") ? parameters["data"] : null;
            if (data == null) throw new ArgumentException("Data parameter is required");

            return new
            {
                DataType = data.GetType().Name,
                Size = data.ToString().Length,
                Hash = data.GetHashCode(),
                Analysis = "Complete"
            };
        }

        private async Task<object> TransformData(Dictionary<string, object> parameters)
        {
            await Task.Delay(50);
            
            var input = parameters.ContainsKey("input") ? parameters["input"].ToString() : "";
            var format = parameters.ContainsKey("format") ? parameters["format"].ToString() : "upper";

            return format switch
            {
                "upper" => input.ToUpper(),
                "lower" => input.ToLower(),
                "reverse" => new string(input.ToCharArray().Reverse().ToArray()),
                "base64" => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(input)),
                _ => input
            };
        }

        private async Task<object> GenerateReport(Dictionary<string, object> parameters)
        {
            await Task.Delay(100);
            
            var title = parameters.ContainsKey("title") ? parameters["title"].ToString() : "Report";
            var sections = parameters.ContainsKey("sections") ? (int)parameters["sections"] : 3;

            var report = new Dictionary<string, object>
            {
                ["Title"] = title,
                ["GeneratedAt"] = DateTime.UtcNow,
                ["Sections"] = new List<object>()
            };

            var sectionsList = new List<object>();
            for (int i = 1; i <= sections; i++)
            {
                sectionsList.Add(new
                {
                    Number = i,
                    Title = $"Section {i}",
                    Content = $"Content for section {i}"
                });
            }
            report["Sections"] = sectionsList;

            return report;
        }
    }
}
