using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;
using Loco.Core.Interfaces;

namespace Loco.Core.Workflows
{
    /// <summary>
    /// Loads workflow definitions from JSON files and converts them to SimpleFlow objects.
    /// </summary>
    public class WorkflowLoader
    {
        private readonly ILogger? _logger;
        private readonly WorkflowValidator _validator;
        private readonly VariableResolver _variableResolver;
        private readonly string? _environment;

        public WorkflowLoader(ILogger? logger = null, Dictionary<string, string>? variables = null, string? environment = null)
        {
            _logger = logger;
            _validator = new WorkflowValidator(logger);
            _variableResolver = new VariableResolver(variables);
            _environment = environment;
        }

        /// <summary>
        /// Loads a workflow from a JSON file.
        /// </summary>
        public async Task<SimpleFlow?> LoadFromFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger?.LogError("Workflow file not found: {FilePath}", filePath);
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                return LoadFromJson(json);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load workflow from file: {FilePath}", filePath);
                return null;
            }
        }

        /// <summary>
        /// Parses workflow JSON and creates a SimpleFlow.
        /// </summary>
        public SimpleFlow? LoadFromJson(string json)
        {
            try
            {
                var definition = JsonSerializer.Deserialize<WorkflowDefinition>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (definition == null)
                {
                    _logger?.LogError("Failed to deserialize workflow JSON");
                    return null;
                }

                // Validate workflow
                var validationResult = _validator.Validate(definition);
                if (!validationResult.IsValid)
                {
                    _logger?.LogError("Workflow validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                    foreach (var error in validationResult.Errors)
                    {
                        Console.WriteLine($"  ✗ {error}");
                    }
                    return null;
                }

                if (validationResult.Warnings.Count > 0)
                {
                    foreach (var warning in validationResult.Warnings)
                    {
                        _logger?.LogWarning("Workflow warning: {Warning}", warning);
                        Console.WriteLine($"  ⚠ {warning}");
                    }
                }

                // Load environment preset variables if specified
                if (definition.Environments != null && definition.Environments.Count > 0)
                {
                    EnvironmentPreset? selectedEnv = null;

                    if (!string.IsNullOrEmpty(_environment))
                    {
                        // Use specified environment
                        selectedEnv = definition.Environments.FirstOrDefault(
                            e => e.Name.Equals(_environment, StringComparison.OrdinalIgnoreCase));

                        if (selectedEnv == null)
                        {
                            _logger?.LogWarning("Environment '{Environment}' not found, using default", _environment);
                        }
                    }

                    // Fall back to default if no environment specified or not found
                    if (selectedEnv == null)
                    {
                        selectedEnv = definition.Environments.FirstOrDefault(e => e.IsDefault)
                                      ?? definition.Environments.First();
                    }

                    if (selectedEnv != null)
                    {
                        _logger?.LogInformation("Using environment preset: {Environment}", selectedEnv.Name);
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"  Environment: {selectedEnv.Name}");
                        if (!string.IsNullOrEmpty(selectedEnv.Description))
                        {
                            Console.WriteLine($"  {selectedEnv.Description}");
                        }
                        Console.ResetColor();

                        // Merge environment variables with existing variables
                        foreach (var kvp in selectedEnv.Variables)
                        {
                            _variableResolver.SetVariable(kvp.Key, kvp.Value);
                        }
                    }
                }

                // Convert WorkflowDefinition to SimpleFlow
                var flow = new SimpleFlow(definition.Name, definition.Description ?? "", definition.Id);

                if (definition.Steps != null)
                {
                    foreach (var step in definition.Steps)
                    {
                        // Resolve variables in step properties
                        _variableResolver.ResolveStepVariables(step);

                        var action = ConvertStepToAction(step);

                        // Wrap with conditional execution if configured
                        if (!string.IsNullOrEmpty(step.RunIf) || !string.IsNullOrEmpty(step.SkipIf))
                        {
                            action = new SimpleConditionalAction(action, step.RunIf, step.SkipIf);
                        }

                        // Wrap with retry/timeout if configured
                        if (step.RetryCount.HasValue || step.TimeoutSeconds.HasValue)
                        {
                            var retryDelay = TimeSpan.FromSeconds(2);
                            if (!string.IsNullOrEmpty(step.RetryDelay) && TimeSpan.TryParse(step.RetryDelay, out var parsed))
                            {
                                retryDelay = parsed;
                            }

                            var timeout = step.TimeoutSeconds.HasValue
                                ? TimeSpan.FromSeconds(step.TimeoutSeconds.Value)
                                : (TimeSpan?)null;

                            action = new RetryableAction(
                                action,
                                step.RetryCount ?? 0,
                                retryDelay,
                                timeout);
                        }

                        flow.Actions.Add(action);
                    }
                }

                return flow;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to parse workflow JSON");
                return null;
            }
        }

        private IAction ConvertStepToAction(WorkflowStep step)
        {
            // For simple "log" actions, return a LogAction
            if (step.Type.Equals("log", StringComparison.OrdinalIgnoreCase))
            {
                return new LogAction(step.Id, step.Name, step.Message ?? "");
            }

            // For "delay" actions, return a DelayAction
            if (step.Type.Equals("delay", StringComparison.OrdinalIgnoreCase))
            {
                return new DelayAction(step.Id, step.Name, step.Duration ?? "00:00:01");
            }

            // For "file" actions, return a FileAction
            if (step.Type.Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                return new FileAction(step.Id, step.Name, step.Path ?? "", step.Source, step.Destination);
            }

            // For "process" or "command" actions, return a ProcessAction
            if (step.Type.Equals("process", StringComparison.OrdinalIgnoreCase) ||
                step.Type.Equals("command", StringComparison.OrdinalIgnoreCase))
            {
                return new ProcessAction(step.Id, step.Name, step.Command ?? "", step.SaveOutput);
            }

            // For "http" or "api" actions, return an HttpAction
            if (step.Type.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                step.Type.Equals("api", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpAction(step.Id, step.Name, step.Url ?? "", step.Method ?? "GET", step.SaveOutput);
            }

            // For other actions, return a generic wrapper action
            return new WorkflowStepAction(step);
        }
    }

    /// <summary>
    /// Represents a workflow definition loaded from JSON.
    /// </summary>
    public partial class WorkflowDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<WorkflowStep>? Steps { get; set; }
        public int? TimeoutSeconds { get; set; }
        public bool? ContinueOnError { get; set; }
    }

    /// <summary>
    /// Represents a single step in a workflow.
    /// </summary>
    public partial class WorkflowStep
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;

        // Common parameters
        public string? Message { get; set; }
        public string? Duration { get; set; }
        public string? Source { get; set; }
        public string? Destination { get; set; }
        public string? Path { get; set; }
        public string? Command { get; set; }
        public string? Url { get; set; }
        public string? Method { get; set; }
        public object? Body { get; set; }

        // Retry configuration
        public int? RetryCount { get; set; }
        public string? RetryDelay { get; set; } // e.g., "00:00:02" for 2 seconds

        // Timeout configuration
        public int? TimeoutSeconds { get; set; } // Step-specific timeout in seconds

        // Conditional execution
        public string? RunIf { get; set; } // Variable name or condition
        public string? SkipIf { get; set; } // Variable name or condition
        public bool? ContinueOnError { get; set; }

        // Output capture
        public string? SaveOutput { get; set; } // Variable name to save step output
        public string? ExpectedResult { get; set; } // Expected result for validation
    }

    /// <summary>
    /// Action that delays execution for a specified duration.
    /// </summary>
    public class DelayAction : IAction
    {
        public string Id { get; }
        public string Name { get; }
        public string Duration { get; }

        public DelayAction(string id, string name, string duration)
        {
            Id = id;
            Name = name;
            Duration = duration;
        }

        public async Task<bool> ExecuteAsync(IActionContext context)
        {
            try
            {
                if (TimeSpan.TryParse(Duration, out var delay))
                {
                    context.Logger?.LogInformation("DelayAction: Waiting for {Duration}", delay);
                    Console.WriteLine($"[DELAY] Waiting for {delay}...");
                    await Task.Delay(delay, context.CancellationToken);
                    return true;
                }
                else
                {
                    context.Logger?.LogWarning("DelayAction: Invalid duration format: {Duration}", Duration);
                    Console.WriteLine($"[DELAY] Invalid duration format: {Duration}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                context.Logger?.LogError(ex, "DelayAction: Failed");
                Console.WriteLine($"[DELAY] Failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Generic action wrapper for workflow steps.
    /// </summary>
    public class WorkflowStepAction : IAction
    {
        public string Id { get; }
        public string Name { get; }
        private readonly WorkflowStep _step;

        public WorkflowStepAction(WorkflowStep step)
        {
            _step = step;
            Id = step.Id;
            Name = step.Name;
        }

        public Task<bool> ExecuteAsync(IActionContext context)
        {
            context.Logger?.LogInformation("Executing step: {StepName} (Type: {StepType})", _step.Name, _step.Type);
            Console.WriteLine($"[{_step.Type.ToUpper()}] {_step.Name}");
            // For now, just log that we executed the step
            // In a full implementation, this would dispatch to appropriate action executors
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Action for file operations (copy, move, delete, check existence).
    /// </summary>
    public class FileAction : IAction
    {
        public string Id { get; }
        public string Name { get; }
        public string Path { get; }
        public string? Source { get; }
        public string? Destination { get; }

        public FileAction(string id, string name, string path, string? source = null, string? destination = null)
        {
            Id = id;
            Name = name;
            Path = path;
            Source = source;
            Destination = destination;
        }

        public Task<bool> ExecuteAsync(IActionContext context)
        {
            try
            {
                context.Logger?.LogInformation("FileAction: {Name}", Name);
                Console.WriteLine($"[FILE] {Name}");

                // Check if file exists
                if (!string.IsNullOrEmpty(Path) && File.Exists(Path))
                {
                    Console.WriteLine($"  ✓ File exists: {Path}");
                    return Task.FromResult(true);
                }

                // Copy operation
                if (!string.IsNullOrEmpty(Source) && !string.IsNullOrEmpty(Destination))
                {
                    if (File.Exists(Source))
                    {
                        var destDir = System.IO.Path.GetDirectoryName(Destination);
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }
                        File.Copy(Source, Destination, true);
                        Console.WriteLine($"  ✓ Copied: {Source} → {Destination}");
                        return Task.FromResult(true);
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ Source file not found: {Source}");
                        return Task.FromResult(false);
                    }
                }

                // Check directory existence
                if (!string.IsNullOrEmpty(Path) && Directory.Exists(Path))
                {
                    Console.WriteLine($"  ✓ Directory exists: {Path}");
                    return Task.FromResult(true);
                }

                Console.WriteLine($"  ⚠ Path not found: {Path}");
                return Task.FromResult(true); // Not a critical error
            }
            catch (Exception ex)
            {
                context.Logger?.LogError(ex, "FileAction failed");
                Console.WriteLine($"  ✗ Error: {ex.Message}");
                return Task.FromResult(false);
            }
        }
    }

    /// <summary>
    /// Action for executing system processes/commands.
    /// </summary>
    public class ProcessAction : IAction
    {
        public string Id { get; }
        public string Name { get; }
        public string Command { get; }
        public string? SaveOutput { get; }

        public ProcessAction(string id, string name, string command, string? saveOutput = null)
        {
            Id = id;
            Name = name;
            Command = command;
            SaveOutput = saveOutput;
        }

        public async Task<bool> ExecuteAsync(IActionContext context)
        {
            try
            {
                context.Logger?.LogInformation("ProcessAction: {Command}", Command);
                Console.WriteLine($"[PROCESS] {Name}");
                Console.WriteLine($"  Command: {Command}");

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("/c");
                startInfo.ArgumentList.Add(Command);

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process == null)
                {
                    Console.WriteLine("  ✗ Failed to start process");
                    return false;
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync(context.CancellationToken);

                // Save output to context variable if requested
                if (!string.IsNullOrEmpty(SaveOutput) && !string.IsNullOrEmpty(output))
                {
                    context.Variables[SaveOutput] = output.Trim();
                    Console.WriteLine($"  Saved output to variable: {SaveOutput}");
                }

                if (!string.IsNullOrEmpty(output))
                {
                    Console.WriteLine($"  Output: {output.Trim()}");
                }

                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"  ⚠ Error: {error.Trim()}");
                }

                var success = process.ExitCode == 0;
                Console.WriteLine(success ? "  ✓ Process completed successfully" : $"  ✗ Process failed with exit code {process.ExitCode}");

                // Save exit code to context
                context.Variables[$"{Id}_exitcode"] = process.ExitCode;

                return success;
            }
            catch (Exception ex)
            {
                context.Logger?.LogError(ex, "ProcessAction failed");
                Console.WriteLine($"  ✗ Exception: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Action for HTTP/API requests.
    /// </summary>
    public class HttpAction : IAction
    {
        public string Id { get; }
        public string Name { get; }
        public string Url { get; }
        public string Method { get; }
        public string? SaveOutput { get; }

        public HttpAction(string id, string name, string url, string method, string? saveOutput = null)
        {
            Id = id;
            Name = name;
            Url = url;
            Method = method;
            SaveOutput = saveOutput;
        }

        public async Task<bool> ExecuteAsync(IActionContext context)
        {
            try
            {
                context.Logger?.LogInformation("HttpAction: {Method} {Url}", Method, Url);
                Console.WriteLine($"[HTTP] {Name}");
                Console.WriteLine($"  {Method} {Url}");

                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                System.Net.Http.HttpResponseMessage response;
                if (Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
                    response = await httpClient.GetAsync(Url, context.CancellationToken);
                }
                else if (Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                {
                    response = await httpClient.PostAsync(Url, null, context.CancellationToken);
                }
                else if (Method.Equals("PUT", StringComparison.OrdinalIgnoreCase))
                {
                    response = await httpClient.PutAsync(Url, null, context.CancellationToken);
                }
                else if (Method.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
                {
                    response = await httpClient.DeleteAsync(Url, context.CancellationToken);
                }
                else
                {
                    Console.WriteLine($"  ⚠ Unsupported HTTP method: {Method}");
                    return false;
                }

                var statusCode = (int)response.StatusCode;
                var success = response.IsSuccessStatusCode;

                // Save response body if requested
                if (!string.IsNullOrEmpty(SaveOutput) && success)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(context.CancellationToken);
                    context.Variables[SaveOutput] = responseBody;
                    Console.WriteLine($"  Saved response to variable: {SaveOutput}");
                }

                // Save status code to context
                context.Variables[$"{Id}_statuscode"] = statusCode;

                Console.WriteLine($"  Status: {statusCode} {response.ReasonPhrase}");

                if (success)
                {
                    Console.WriteLine($"  ✓ Request completed successfully");
                }
                else
                {
                    Console.WriteLine($"  ✗ Request failed");
                }

                return success;
            }
            catch (Exception ex)
            {
                context.Logger?.LogError(ex, "HttpAction failed");
                Console.WriteLine($"  ✗ Exception: {ex.Message}");
                return false;
            }
        }
    }
}
