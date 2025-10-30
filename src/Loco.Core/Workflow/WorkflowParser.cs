using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Loco.Core.Workflow
{
    /// <summary>
    /// Parser for workflow definitions supporting JSON format.
    /// JSON形式をサポートするワークフロー定義パーサー
    /// </summary>
    public class WorkflowParser
    {
        private readonly JsonSerializerOptions _jsonOptions;

        public WorkflowParser()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        /// <summary>
        /// Parses a workflow from JSON string.
        /// </summary>
        public WorkflowDefinition ParseJson(string json)
        {
            try
            {
                var workflow = JsonSerializer.Deserialize<WorkflowDefinition>(json, _jsonOptions);
                if (workflow == null)
                {
                    throw new WorkflowParseException("Failed to deserialize workflow: result was null");
                }
                return workflow;
            }
            catch (JsonException ex)
            {
                throw new WorkflowParseException($"Invalid JSON format: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parses a workflow from a file.
        /// </summary>
        public async Task<WorkflowDefinition> ParseFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Workflow file not found: {filePath}");
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                return ParseJson(json);
            }
            catch (WorkflowParseException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new WorkflowParseException($"Failed to read workflow file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Serializes a workflow to JSON string.
        /// </summary>
        public string ToJson(WorkflowDefinition workflow)
        {
            try
            {
                return JsonSerializer.Serialize(workflow, _jsonOptions);
            }
            catch (Exception ex)
            {
                throw new WorkflowParseException($"Failed to serialize workflow: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Saves a workflow to a file.
        /// </summary>
        public async Task SaveFileAsync(WorkflowDefinition workflow, string filePath)
        {
            try
            {
                var json = ToJson(workflow);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (WorkflowParseException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new WorkflowParseException($"Failed to save workflow file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parses and validates a workflow in one step.
        /// </summary>
        public (WorkflowDefinition Workflow, ValidationResult Validation) ParseAndValidate(string json)
        {
            var workflow = ParseJson(json);
            var validator = new WorkflowValidator();
            var validation = validator.Validate(workflow);
            return (workflow, validation);
        }

        /// <summary>
        /// Parses and validates a workflow from a file.
        /// </summary>
        public async Task<(WorkflowDefinition Workflow, ValidationResult Validation)> ParseAndValidateFileAsync(string filePath)
        {
            var workflow = await ParseFileAsync(filePath);
            var validator = new WorkflowValidator();
            var validation = validator.Validate(workflow);
            return (workflow, validation);
        }
    }

    /// <summary>
    /// Exception thrown when workflow parsing fails.
    /// </summary>
    public class WorkflowParseException : Exception
    {
        public WorkflowParseException(string message) : base(message) { }
        public WorkflowParseException(string message, Exception innerException) : base(message, innerException) { }
    }
}
