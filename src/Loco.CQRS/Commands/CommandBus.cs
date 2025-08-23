using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loco.CQRS.Commands;

/// <summary>
/// Base command interface
/// </summary>
public interface ICommand
{
    Guid CommandId { get; }
    DateTime Timestamp { get; }
    string UserId { get; }
}

/// <summary>
/// Command with result
/// </summary>
public interface ICommand<TResult> : ICommand
{
}

/// <summary>
/// Base command implementation
/// </summary>
public abstract class CommandBase : ICommand
{
    public Guid CommandId { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
}

/// <summary>
/// Base command with result implementation
/// </summary>
public abstract class CommandBase<TResult> : CommandBase, ICommand<TResult>
{
}

/// <summary>
/// Command handler interface
/// </summary>
public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Command handler with result interface
/// </summary>
public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Command bus interface
/// </summary>
public interface ICommandBus
{
    Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) 
        where TCommand : ICommand;
    Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Command bus implementation
/// </summary>
public class CommandBus : ICommandBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CommandBus> _logger;
    private readonly List<ICommandMiddleware> _middlewares;

    public CommandBus(
        IServiceProvider serviceProvider,
        ILogger<CommandBus> logger,
        IEnumerable<ICommandMiddleware>? middlewares = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _middlewares = middlewares?.ToList() ?? new List<ICommandMiddleware>();
    }

    public async Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) 
        where TCommand : ICommand
    {
        _logger.LogDebug("Sending command {CommandType} with ID {CommandId}",
            command.GetType().Name, command.CommandId);

        // Apply middlewares
        foreach (var middleware in _middlewares)
        {
            await middleware.ExecuteAsync(command, async () => await Task.CompletedTask, cancellationToken);
        }

        // Get handler
        var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
        var handler = _serviceProvider.GetService(handlerType);

        if (handler == null)
        {
            throw new InvalidOperationException(
                $"No handler registered for command type {command.GetType().Name}");
        }

        // Execute handler
        var handleMethod = handlerType.GetMethod("HandleAsync");
        if (handleMethod != null)
        {
            var task = (Task?)handleMethod.Invoke(handler, new object[] { command, cancellationToken });
            if (task != null)
            {
                await task;
            }
        }

        _logger.LogDebug("Command {CommandType} with ID {CommandId} handled successfully",
            command.GetType().Name, command.CommandId);
    }

    public async Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending command {CommandType} with ID {CommandId}",
            command.GetType().Name, command.CommandId);

        TResult? result = default;

        // Apply middlewares
        foreach (var middleware in _middlewares)
        {
            await middleware.ExecuteAsync(command, async () => await Task.CompletedTask, cancellationToken);
        }

        // Get handler
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
        var handler = _serviceProvider.GetService(handlerType);

        if (handler == null)
        {
            throw new InvalidOperationException(
                $"No handler registered for command type {command.GetType().Name}");
        }

        // Execute handler
        var handleMethod = handlerType.GetMethod("HandleAsync");
        if (handleMethod != null)
        {
            var task = handleMethod.Invoke(handler, new object[] { command, cancellationToken });
            if (task != null)
            {
                var taskType = task.GetType();
                var resultProperty = taskType.GetProperty("Result");
                if (resultProperty != null)
                {
                    await (Task)task;
                    result = (TResult?)resultProperty.GetValue(task);
                }
            }
        }

        _logger.LogDebug("Command {CommandType} with ID {CommandId} handled successfully",
            command.GetType().Name, command.CommandId);

        return result!;
    }
}

/// <summary>
/// Command middleware interface
/// </summary>
public interface ICommandMiddleware
{
    Task ExecuteAsync(ICommand command, Func<Task> next, CancellationToken cancellationToken = default);
}

/// <summary>
/// Logging middleware for commands
/// </summary>
public class LoggingCommandMiddleware : ICommandMiddleware
{
    private readonly ILogger<LoggingCommandMiddleware> _logger;

    public LoggingCommandMiddleware(ILogger<LoggingCommandMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(ICommand command, Func<Task> next, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing command {CommandType} by user {UserId}",
            command.GetType().Name, command.UserId);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            await next();
            
            _logger.LogInformation("Command {CommandType} executed successfully in {ElapsedMs}ms",
                command.GetType().Name, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command {CommandType} failed after {ElapsedMs}ms",
                command.GetType().Name, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

/// <summary>
/// Validation middleware for commands
/// </summary>
public class ValidationCommandMiddleware : ICommandMiddleware
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ValidationCommandMiddleware> _logger;

    public ValidationCommandMiddleware(
        IServiceProvider serviceProvider,
        ILogger<ValidationCommandMiddleware> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync(ICommand command, Func<Task> next, CancellationToken cancellationToken = default)
    {
        // Get validator
        var validatorType = typeof(ICommandValidator<>).MakeGenericType(command.GetType());
        var validator = _serviceProvider.GetService(validatorType);

        if (validator != null)
        {
            var validateMethod = validatorType.GetMethod("ValidateAsync");
            if (validateMethod != null)
            {
                var task = (Task<ValidationResult>?)validateMethod.Invoke(
                    validator, new object[] { command, cancellationToken });
                
                if (task != null)
                {
                    var result = await task;
                    if (!result.IsValid)
                    {
                        _logger.LogWarning("Command {CommandType} validation failed: {Errors}",
                            command.GetType().Name, string.Join(", ", result.Errors));
                        
                        throw new CommandValidationException(result.Errors);
                    }
                }
            }
        }

        await next();
    }
}

/// <summary>
/// Command validator interface
/// </summary>
public interface ICommandValidator<in TCommand> where TCommand : ICommand
{
    Task<ValidationResult> ValidateAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Validation result
/// </summary>
public class ValidationResult
{
    public bool IsValid => !Errors.Any();
    public List<string> Errors { get; } = new();

    public static ValidationResult Success() => new();
    
    public static ValidationResult Failure(params string[] errors)
    {
        var result = new ValidationResult();
        result.Errors.AddRange(errors);
        return result;
    }
}

/// <summary>
/// Command validation exception
/// </summary>
public class CommandValidationException : Exception
{
    public IEnumerable<string> Errors { get; }

    public CommandValidationException(IEnumerable<string> errors) 
        : base($"Command validation failed: {string.Join(", ", errors)}")
    {
        Errors = errors;
    }
}

/// <summary>
/// Transaction middleware for commands
/// </summary>
public class TransactionCommandMiddleware : ICommandMiddleware
{
    private readonly ILogger<TransactionCommandMiddleware> _logger;

    public TransactionCommandMiddleware(ILogger<TransactionCommandMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(ICommand command, Func<Task> next, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting transaction for command {CommandType}", command.GetType().Name);

        // In a real implementation, this would start a database transaction
        // using (var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            try
            {
                await next();
                // await transaction.CommitAsync(cancellationToken);
                _logger.LogDebug("Transaction committed for command {CommandType}", command.GetType().Name);
            }
            catch
            {
                // await transaction.RollbackAsync(cancellationToken);
                _logger.LogDebug("Transaction rolled back for command {CommandType}", command.GetType().Name);
                throw;
            }
        }
    }
}

/// <summary>
/// Retry middleware for commands
/// </summary>
public class RetryCommandMiddleware : ICommandMiddleware
{
    private readonly ILogger<RetryCommandMiddleware> _logger;
    private readonly int _maxRetries;
    private readonly TimeSpan _delay;

    public RetryCommandMiddleware(
        ILogger<RetryCommandMiddleware> logger,
        int maxRetries = 3,
        int delayMs = 100)
    {
        _logger = logger;
        _maxRetries = maxRetries;
        _delay = TimeSpan.FromMilliseconds(delayMs);
    }

    public async Task ExecuteAsync(ICommand command, Func<Task> next, CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        
        while (true)
        {
            try
            {
                await next();
                break;
            }
            catch (Exception ex) when (attempt < _maxRetries && IsRetryable(ex))
            {
                attempt++;
                _logger.LogWarning("Command {CommandType} failed on attempt {Attempt}, retrying...",
                    command.GetType().Name, attempt);
                
                await Task.Delay(_delay * attempt, cancellationToken);
            }
        }
    }

    private bool IsRetryable(Exception ex)
    {
        // Define which exceptions are retryable
        return ex is TimeoutException || 
               ex is TaskCanceledException ||
               (ex.InnerException is TimeoutException);
    }
}
