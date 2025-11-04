using Microsoft.Extensions.Logging;

namespace Loco.Core.Sagas;

/// <summary>
/// Saga orchestrator implementation for managing distributed transactions
/// </summary>
public class SagaOrchestrator : ISagaOrchestrator
{
    private readonly ILogger<SagaOrchestrator> _logger;
    private readonly Dictionary<string, SagaExecutionResult> _executionHistory = new();

    public SagaOrchestrator(ILogger<SagaOrchestrator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SagaExecutionResult> ExecuteAsync(
        ISagaDefinition definition,
        Dictionary<string, object?> initialData,
        CancellationToken cancellationToken = default)
    {
        var context = new SagaContext
        {
            Data = initialData
        };

        var result = new SagaExecutionResult
        {
            SagaId = context.SagaId,
            SagaName = definition.Name,
            StartTime = DateTime.UtcNow,
            Status = SagaStatus.Running
        };

        try
        {
            _logger.LogInformation(
                "Starting saga execution: {SagaName} (ID: {SagaId})",
                definition.Name, context.SagaId);

            // Get starting step
            var step = definition.GetStartStep();
            if (step == null)
            {
                throw new InvalidOperationException("No start step defined for saga");
            }

            // Execute steps in sequence
            while (step != null && !cancellationToken.IsCancellationRequested)
            {
                context.CurrentStep = step.Name;
                result.CurrentStep = step.Name;

                _logger.LogInformation(
                    "Executing saga step: {StepName}",
                    step.Name);

                // Execute step with timeout and retry logic
                var stepResult = await ExecuteStepWithRetryAsync(step, context, cancellationToken);
                context.ExecutedSteps.Add(step.Name);
                context.StepResults[step.Name] = stepResult;
                result.StepResults[step.Name] = stepResult;

                if (!stepResult.Success)
                {
                    _logger.LogError(
                        "Saga step failed: {StepName}, Error: {Error}",
                        step.Name, stepResult.ErrorMessage);

                    // Compensate previous steps
                    await CompensateAsync(definition, context, cancellationToken);
                    result.Status = SagaStatus.Failed;
                    result.ErrorMessage = stepResult.ErrorMessage;
                    result.CompensationPerformed = true;
                    break;
                }

                // Determine next step
                step = definition.GetNextStep(step.Name, stepResult);
            }

            if (step == null)
            {
                result.Status = SagaStatus.Completed;
                result.Success = true;

                _logger.LogInformation(
                    "Saga completed successfully: {SagaName} (ID: {SagaId})",
                    definition.Name, context.SagaId);
            }

            result.ExecutedSteps = context.ExecutedSteps;
            result.Output = context.Data;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Saga cancelled: {SagaName} (ID: {SagaId})", definition.Name, context.SagaId);
            result.Status = SagaStatus.Cancelled;
            await CompensateAsync(definition, context, CancellationToken.None);
            result.CompensationPerformed = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga execution failed: {SagaName} (ID: {SagaId})", definition.Name, context.SagaId);
            result.Status = SagaStatus.Failed;
            result.ErrorMessage = ex.Message;
            await CompensateAsync(definition, context, CancellationToken.None);
            result.CompensationPerformed = true;
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
            _executionHistory[context.SagaId] = result;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<SagaExecutionResult?> GetStatusAsync(string sagaId)
    {
        _executionHistory.TryGetValue(sagaId, out var result);
        return await Task.FromResult(result);
    }

    /// <inheritdoc />
    public async Task<bool> CancelAsync(string sagaId)
    {
        if (_executionHistory.TryGetValue(sagaId, out var result))
        {
            if (result.Status == SagaStatus.Running)
            {
                result.Status = SagaStatus.Cancelled;
                _logger.LogInformation("Saga cancelled: {SagaId}", sagaId);
                return true;
            }
        }

        return await Task.FromResult(false);
    }

    private async Task<SagaStepResult> ExecuteStepWithRetryAsync(
        ISagaStep step,
        SagaContext context,
        CancellationToken cancellationToken)
    {
        var maxRetries = 3;
        var retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(step.GetTimeout());

                var result = await step.ExecuteAsync(context, cts.Token);
                result.RetryCount = retryCount;

                if (result.Success)
                {
                    return result;
                }

                if (!result.ShouldRetry || retryCount >= maxRetries - 1)
                {
                    return result;
                }

                retryCount++;
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new SagaStepResult
                {
                    Success = false,
                    ErrorMessage = $"Step execution timeout after {step.GetTimeout().TotalSeconds}s",
                    ShouldRetry = true,
                    RetryCount = retryCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing saga step: {StepName}", step.Name);

                return new SagaStepResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ShouldRetry = true,
                    RetryCount = retryCount
                };
            }
        }

        return new SagaStepResult
        {
            Success = false,
            ErrorMessage = "Max retries exceeded",
            ShouldRetry = false,
            RetryCount = retryCount
        };
    }

    private async Task CompensateAsync(
        ISagaDefinition definition,
        SagaContext context,
        CancellationToken cancellationToken)
    {
        context.IsCompensating = true;

        _logger.LogInformation(
            "Starting saga compensation: {SagaId}",
            context.SagaId);

        // Compensate in reverse order
        for (int i = context.ExecutedSteps.Count - 1; i >= 0; i--)
        {
            var stepName = context.ExecutedSteps[i];
            var step = definition.GetSteps().FirstOrDefault(s => s.Name == stepName);

            if (step == null)
                continue;

            try
            {
                _logger.LogInformation(
                    "Compensating saga step: {StepName}",
                    stepName);

                var success = await step.CompensateAsync(context, cancellationToken);

                if (!success)
                {
                    _logger.LogWarning(
                        "Saga step compensation failed: {StepName}",
                        stepName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compensating saga step: {StepName}", stepName);
            }
        }

        _logger.LogInformation(
            "Saga compensation completed: {SagaId}",
            context.SagaId);
    }
}

/// <summary>
/// Base implementation for saga steps
/// </summary>
public abstract class SagaStepBase : ISagaStep
{
    protected readonly ILogger<SagaStepBase> Logger;

    public abstract string Name { get; }
    public abstract string Description { get; }

    protected SagaStepBase(ILogger<SagaStepBase> logger)
    {
        Logger = logger;
    }

    public virtual TimeSpan GetTimeout() => TimeSpan.FromMinutes(5);

    public abstract Task<SagaStepResult> ExecuteAsync(SagaContext context, CancellationToken cancellationToken);

    public abstract Task<bool> CompensateAsync(SagaContext context, CancellationToken cancellationToken);

    protected void LogStepExecution(string message)
    {
        Logger.LogInformation("{StepName}: {Message}", Name, message);
    }

    protected void LogStepError(Exception ex, string message)
    {
        Logger.LogError(ex, "{StepName}: {Message}", Name, message);
    }
}
