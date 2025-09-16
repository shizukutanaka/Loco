using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Loco.Core.Models;
using Loco.Core.Validation;

namespace Loco.Core.Services
{
    /// <summary>
    /// Batch operations service for managing multiple automation rules
    /// Implements efficient bulk operations with transaction support
    /// </summary>
    public sealed class BatchOperationService
    {
        private readonly ILogger<BatchOperationService> _logger;
        private readonly IFlowValidator _validator;
        private readonly SemaphoreSlim _operationSemaphore;
        private readonly int _maxConcurrency;

        public BatchOperationService(ILogger<BatchOperationService> logger = null, IFlowValidator validator = null, int maxConcurrency = 5)
        {
            _logger = logger ?? NullLogger<BatchOperationService>.Instance;
            _validator = validator ?? new FlowValidator();
            _maxConcurrency = maxConcurrency;
            _operationSemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        }

        /// <summary>
        /// Enable multiple rules
        /// </summary>
        public async Task<BatchOperationResult> EnableRulesAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            BatchOperationOptions options = null)
        {
            options ??= BatchOperationOptions.Default;
            
            return await ExecuteBatchOperationAsync(
                rules,
                (rule) =>
                {
                    rule.Enabled = true;
                    if (rule.Metadata != null)
                        rule.Metadata.UpdatedAt = DateTime.UtcNow;
                    return Task.FromResult(true);
                },
                "Enable Rules",
                options);
        }

        /// <summary>
        /// Disable multiple rules
        /// </summary>
        public async Task<BatchOperationResult> DisableRulesAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            BatchOperationOptions options = null)
        {
            options ??= BatchOperationOptions.Default;
            
            return await ExecuteBatchOperationAsync(
                rules,
                (rule) =>
                {
                    rule.Enabled = false;
                    if (rule.Metadata != null)
                        rule.Metadata.UpdatedAt = DateTime.UtcNow;
                    return Task.FromResult(true);
                },
                "Disable Rules",
                options);
        }

        /// <summary>
        /// Delete multiple rules
        /// </summary>
        public async Task<BatchOperationResult> DeleteRulesAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            Func<AutomationDsl.Rule, Task<bool>> deleteFunction,
            BatchOperationOptions options = null)
        {
            options ??= BatchOperationOptions.Default;
            
            // Confirm deletion if required
            if (options.RequireConfirmation)
            {
                var confirmation = await options.ConfirmationCallback?.Invoke(
                    $"Delete {rules.Count()} rules?");
                if (!confirmation.GetValueOrDefault())
                {
                    return new BatchOperationResult
                    {
                        Success = false,
                        Message = "Operation cancelled by user"
                    };
                }
            }

            return await ExecuteBatchOperationAsync(
                rules,
                deleteFunction,
                "Delete Rules",
                options);
        }

        /// <summary>
        /// Update multiple rules with common changes
        /// </summary>
        public async Task<BatchOperationResult> UpdateRulesAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            Action<AutomationDsl.Rule> updateAction,
            BatchOperationOptions options = null)
        {
            options ??= BatchOperationOptions.Default;

            return await ExecuteBatchOperationAsync(
                rules,
                async (rule) =>
                {
                    try
                    {
                        updateAction(rule);
                        
                        if (rule.Metadata != null)
                            rule.Metadata.UpdatedAt = DateTime.UtcNow;
                        
                        // Validate after update if required
                        if (options.ValidateAfterOperation)
                        {
                            // Simplified validation
                            var validationResult = rule != null && !string.IsNullOrEmpty(rule.Name) ?
                                new RuleValidationResult { IsValid = true, RuleId = rule.Id, RuleName = rule.Name } :
                                RuleValidationResult.Fail(rule?.Id ?? "unknown", rule?.Name ?? "unknown", "Invalid rule structure");
                            return validationResult.IsValid;
                        }
                        
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to update rule {RuleId}", rule.Id);
                        return false;
                    }
                },
                "Update Rules",
                options);
        }

        /// <summary>
        /// Add tags to multiple rules
        /// </summary>
        public async Task<BatchOperationResult> AddTagsAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            IEnumerable<string> tags,
            BatchOperationOptions options = null)
        {
            options ??= BatchOperationOptions.Default;
            var tagList = tags.ToList();

            return await ExecuteBatchOperationAsync(
                rules,
                async (rule) =>
                {
                    if (rule.Metadata == null)
                        rule.Metadata = new AutomationDsl.RuleMetadata();
                    
                    if (rule.Metadata.Tags == null)
                        rule.Metadata.Tags = new List<string>();
                    
                    foreach (var tag in tagList)
                    {
                        if (!rule.Metadata.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                        {
                            rule.Metadata.Tags.Add(tag);
                        }
                    }
                    
                    rule.Metadata.UpdatedAt = DateTime.UtcNow;
                    return await Task.FromResult(true);
                },
                $"Add Tags: {string.Join(", ", tagList)}",
                options);
        }

        /// <summary>
        /// Remove tags from multiple rules
        /// </summary>
        public async Task<BatchOperationResult> RemoveTagsAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            IEnumerable<string> tags,
            BatchOperationOptions options = null)
        {
            options ??= BatchOperationOptions.Default;
            var tagList = tags.ToList();

            return await ExecuteBatchOperationAsync(
                rules,
                async (rule) =>
                {
                    if (rule.Metadata?.Tags != null)
                    {
                        foreach (var tag in tagList)
                        {
                            rule.Metadata.Tags.RemoveAll(t => 
                                t.Equals(tag, StringComparison.OrdinalIgnoreCase));
                        }
                        rule.Metadata.UpdatedAt = DateTime.UtcNow;
                    }
                    return await Task.FromResult(true);
                },
                $"Remove Tags: {string.Join(", ", tagList)}",
                options);
        }

        /// <summary>
        /// Duplicate multiple rules
        /// </summary>
        public async Task<BatchDuplicateResult> DuplicateRulesAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            DuplicateOptions duplicateOptions = null)
        {
            duplicateOptions ??= DuplicateOptions.Default;
            var duplicatedRules = new List<AutomationDsl.Rule>();
            var errors = new List<string>();

            foreach (var rule in rules)
            {
                try
                {
                    var duplicate = CloneRule(rule);
                    
                    // Generate new ID
                    duplicate.Id = Guid.NewGuid().ToString();
                    
                    // Update name
                    if (duplicateOptions.AddSuffix)
                    {
                        duplicate.Name = $"{rule.Name}{duplicateOptions.Suffix}";
                    }
                    
                    // Update metadata
                    if (duplicate.Metadata != null)
                    {
                        duplicate.Metadata.CreatedAt = DateTime.UtcNow;
                        duplicate.Metadata.UpdatedAt = DateTime.UtcNow;
                        duplicate.Metadata.Source = $"Duplicated from {rule.Id}";
                    }
                    
                    // Disable by default if specified
                    if (duplicateOptions.DisableByDefault)
                    {
                        duplicate.Enabled = false;
                    }
                    
                    // Validate if required
                    if (duplicateOptions.ValidateDuplicates)
                    {
                        // Simplified validation
                        var validationResult = duplicate != null && !string.IsNullOrEmpty(duplicate.Name) ?
                            new RuleValidationResult { IsValid = true, RuleId = duplicate.Id, RuleName = duplicate.Name } :
                            RuleValidationResult.Fail(duplicate?.Id ?? "unknown", duplicate?.Name ?? "unknown", "Invalid rule structure");
                        if (!validationResult.IsValid)
                        {
                            errors.Add($"Rule '{rule.Name}': {string.Join(", ", 
                                validationResult.Errors.Select(e => e.Message))}");
                            continue;
                        }
                    }
                    
                    duplicatedRules.Add(duplicate);
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to duplicate rule '{rule.Name}': {ex.Message}");
                }
            }

            return new BatchDuplicateResult
            {
                Success = errors.Count == 0,
                DuplicatedRules = duplicatedRules,
                DuplicatedCount = duplicatedRules.Count,
                Errors = errors,
                Message = errors.Count == 0 
                    ? $"Successfully duplicated {duplicatedRules.Count} rules"
                    : $"Duplicated {duplicatedRules.Count} rules with {errors.Count} errors"
            };
        }

        /// <summary>
        /// Validate multiple rules
        /// </summary>
        public async Task<BatchValidationResult> ValidateRulesAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            bool stopOnFirstError = false)
        {
            var results = new List<RuleValidationResult>();
            var validCount = 0;
            var invalidCount = 0;

            foreach (var rule in rules)
            {
                // Simplified validation
                var validationResult = rule != null && !string.IsNullOrEmpty(rule.Name) ?
                    new RuleValidationResult { IsValid = true, RuleId = rule.Id, RuleName = rule.Name } :
                    RuleValidationResult.Fail(rule?.Id ?? "unknown", rule?.Name ?? "unknown", "Invalid rule structure");
                
                results.Add(new RuleValidationResult
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    IsValid = validationResult.IsValid,
                    Errors = validationResult.Errors?.Select(e => $"{e.Field}: {e.Message}").ToList()
                });

                if (validationResult.IsValid)
                    validCount++;
                else
                    invalidCount++;

                if (!validationResult.IsValid && stopOnFirstError)
                    break;
            }

            return new BatchValidationResult
            {
                Success = invalidCount == 0,
                TotalValidated = results.Count,
                ValidCount = validCount,
                InvalidCount = invalidCount,
                Results = results,
                Message = invalidCount == 0
                    ? $"All {validCount} rules are valid"
                    : $"{invalidCount} of {results.Count} rules have validation errors"
            };
        }

        /// <summary>
        /// Execute batch operation with filter
        /// </summary>
        public async Task<BatchOperationResult> ExecuteFilteredOperationAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            Func<AutomationDsl.Rule, bool> filter,
            Func<AutomationDsl.Rule, Task<bool>> operation,
            string operationName,
            BatchOperationOptions options = null)
        {
            var filteredRules = rules.Where(filter);
            return await ExecuteBatchOperationAsync(filteredRules, operation, operationName, options);
        }

        /// <summary>
        /// Execute conditional batch operation
        /// </summary>
        public async Task<BatchOperationResult> ExecuteConditionalOperationAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            Func<AutomationDsl.Rule, Task<bool>> condition,
            Func<AutomationDsl.Rule, Task<bool>> operation,
            string operationName,
            BatchOperationOptions options = null)
        {
            options ??= BatchOperationOptions.Default;
            var rulesList = rules.ToList();
            var processedRules = new List<AutomationDsl.Rule>();
            var skippedRules = new List<AutomationDsl.Rule>();
            var errors = new List<string>();

            foreach (var rule in rulesList)
            {
                try
                {
                    if (await condition(rule))
                    {
                        var success = await operation(rule);
                        if (success)
                            processedRules.Add(rule);
                        else
                            errors.Add($"Operation failed for rule '{rule.Name}'");
                    }
                    else
                    {
                        skippedRules.Add(rule);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Error processing rule '{rule.Name}': {ex.Message}");
                    if (options.StopOnError)
                        break;
                }
            }

            return new BatchOperationResult
            {
                Success = errors.Count == 0,
                OperationName = operationName,
                TotalProcessed = processedRules.Count + errors.Count,
                SuccessCount = processedRules.Count,
                FailedCount = errors.Count,
                SkippedCount = skippedRules.Count,
                Errors = errors,
                ProcessedRules = processedRules,
                Message = BuildResultMessage(operationName, processedRules.Count, errors.Count, skippedRules.Count)
            };
        }

        // Private methods
        private async Task<BatchOperationResult> ExecuteBatchOperationAsync(
            IEnumerable<AutomationDsl.Rule> rules,
            Func<AutomationDsl.Rule, Task<bool>> operation,
            string operationName,
            BatchOperationOptions options)
        {
            var rulesList = rules.ToList();
            var startTime = DateTime.UtcNow;
            var processedRules = new List<AutomationDsl.Rule>();
            var errors = new List<string>();

            _logger?.LogInformation("Starting batch operation: {Operation} on {Count} rules", 
                operationName, rulesList.Count);

            // Progress reporting
            var progress = 0;
            var progressCallback = options.ProgressCallback;

            try
            {
                if (options.UseParallelProcessing)
                {
                    // Parallel processing with concurrency limit
                    var tasks = rulesList.Select(async rule =>
                    {
                        await _operationSemaphore.WaitAsync();
                        try
                        {
                            var success = await operation(rule);
                            if (success)
                            {
                                lock (processedRules)
                                {
                                    processedRules.Add(rule);
                                }
                            }
                            else
                            {
                                lock (errors)
                                {
                                    errors.Add($"Failed to process rule '{rule.Name}'");
                                }
                            }

                            // Report progress
                            Interlocked.Increment(ref progress);
                            progressCallback?.Invoke(progress, rulesList.Count);
                            
                            return success;
                        }
                        finally
                        {
                            _operationSemaphore.Release();
                        }
                    });

                    await Task.WhenAll(tasks);
                }
                else
                {
                    // Sequential processing
                    foreach (var rule in rulesList)
                    {
                        try
                        {
                            var success = await operation(rule);
                            if (success)
                            {
                                processedRules.Add(rule);
                            }
                            else
                            {
                                errors.Add($"Failed to process rule '{rule.Name}'");
                            }

                            progress++;
                            progressCallback?.Invoke(progress, rulesList.Count);

                            if (!success && options.StopOnError)
                                break;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Error processing rule '{rule.Name}': {ex.Message}");
                            if (options.StopOnError)
                                break;
                        }
                    }
                }

                var elapsed = DateTime.UtcNow - startTime;
                
                _logger?.LogInformation("Batch operation completed: {Operation}. " +
                    "Processed: {Processed}, Failed: {Failed}, Time: {Elapsed}ms",
                    operationName, processedRules.Count, errors.Count, elapsed.TotalMilliseconds);

                return new BatchOperationResult
                {
                    Success = errors.Count == 0,
                    OperationName = operationName,
                    TotalProcessed = processedRules.Count + errors.Count,
                    SuccessCount = processedRules.Count,
                    FailedCount = errors.Count,
                    ProcessingTime = elapsed,
                    Errors = errors,
                    ProcessedRules = processedRules,
                    Message = BuildResultMessage(operationName, processedRules.Count, errors.Count, 0)
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Batch operation failed: {Operation}", operationName);
                
                return new BatchOperationResult
                {
                    Success = false,
                    OperationName = operationName,
                    TotalProcessed = processedRules.Count,
                    SuccessCount = processedRules.Count,
                    FailedCount = errors.Count + 1,
                    Errors = errors.Concat(new[] { ex.Message }).ToList(),
                    Message = $"Batch operation failed: {ex.Message}"
                };
            }
        }

        private AutomationDsl.Rule CloneRule(AutomationDsl.Rule rule)
        {
            // Deep clone using JSON serialization
            var json = System.Text.Json.JsonSerializer.Serialize(rule);
            return System.Text.Json.JsonSerializer.Deserialize<AutomationDsl.Rule>(json);
        }

        private string BuildResultMessage(string operationName, int successCount, int failedCount, int skippedCount)
        {
            var parts = new List<string> { $"{operationName}:" };
            
            if (successCount > 0)
                parts.Add($"{successCount} succeeded");
            
            if (failedCount > 0)
                parts.Add($"{failedCount} failed");
            
            if (skippedCount > 0)
                parts.Add($"{skippedCount} skipped");
            
            if (successCount == 0 && failedCount == 0 && skippedCount == 0)
                parts.Add("No rules processed");
            
            return string.Join(", ", parts);
        }
    }

    // Supporting classes
    public class BatchOperationOptions
    {
        public bool UseParallelProcessing { get; set; } = true;
        public bool StopOnError { get; set; } = false;
        public bool ValidateAfterOperation { get; set; } = false;
        public bool RequireConfirmation { get; set; } = false;
        public Func<string, Task<bool?>> ConfirmationCallback { get; set; }
        public Action<int, int> ProgressCallback { get; set; }
        
        public static BatchOperationOptions Default => new BatchOperationOptions();
        
        public static BatchOperationOptions Safe => new BatchOperationOptions
        {
            UseParallelProcessing = false,
            StopOnError = true,
            ValidateAfterOperation = true,
            RequireConfirmation = true
        };
        
        public static BatchOperationOptions Fast => new BatchOperationOptions
        {
            UseParallelProcessing = true,
            StopOnError = false,
            ValidateAfterOperation = false,
            RequireConfirmation = false
        };
    }

    public class DuplicateOptions
    {
        public bool AddSuffix { get; set; } = true;
        public string Suffix { get; set; } = " (Copy)";
        public bool DisableByDefault { get; set; } = true;
        public bool ValidateDuplicates { get; set; } = true;
        
        public static DuplicateOptions Default => new DuplicateOptions();
    }

    public class BatchOperationResult
    {
        public bool Success { get; set; }
        public string OperationName { get; set; }
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public List<string> Errors { get; set; }
        public List<AutomationDsl.Rule> ProcessedRules { get; set; }
        public string Message { get; set; }
    }

    public class BatchDuplicateResult
    {
        public bool Success { get; set; }
        public List<AutomationDsl.Rule> DuplicatedRules { get; set; }
        public int DuplicatedCount { get; set; }
        public List<string> Errors { get; set; }
        public string Message { get; set; }
    }

    public class BatchValidationResult
    {
        public bool Success { get; set; }
        public int TotalValidated { get; set; }
        public int ValidCount { get; set; }
        public int InvalidCount { get; set; }
        public List<RuleValidationResult> Results { get; set; }
        public string Message { get; set; }
    }

}
