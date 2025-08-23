using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loco.Core.CQRS
{
    // Command interfaces
    public interface ICommand
    {
        Guid Id { get; }
        DateTime Timestamp { get; }
        string UserId { get; }
    }

    public interface ICommand<TResult> : ICommand
    {
    }

    public interface ICommandHandler<TCommand> where TCommand : ICommand
    {
        Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
    }

    public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
    {
        Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
    }

    // Query interfaces
    public interface IQuery<TResult>
    {
        Guid Id { get; }
        DateTime Timestamp { get; }
    }

    public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
    {
        Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
    }

    // Mediator
    public interface IMediator
    {
        Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand;
        Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);
        Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
        Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IEvent;
    }

    public class Mediator : IMediator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<Mediator> _logger;
        private readonly IEventBus _eventBus;

        public Mediator(IServiceProvider serviceProvider, ILogger<Mediator> logger, IEventBus eventBus)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _eventBus = eventBus;
        }

        public async Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) 
            where TCommand : ICommand
        {
            _logger.LogDebug("Executing command {CommandType} with Id {CommandId}", 
                command.GetType().Name, command.Id);

            var handler = _serviceProvider.GetRequiredService<ICommandHandler<TCommand>>();
            await handler.HandleAsync(command, cancellationToken);

            _logger.LogDebug("Command {CommandType} executed successfully", command.GetType().Name);
        }

        public async Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Executing command {CommandType} with Id {CommandId}", 
                command.GetType().Name, command.Id);

            var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
            dynamic handler = _serviceProvider.GetRequiredService(handlerType);
            var result = await handler.HandleAsync((dynamic)command, cancellationToken);

            _logger.LogDebug("Command {CommandType} executed successfully", command.GetType().Name);
            return result;
        }

        public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Executing query {QueryType} with Id {QueryId}", 
                query.GetType().Name, query.Id);

            var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
            dynamic handler = _serviceProvider.GetRequiredService(handlerType);
            var result = await handler.HandleAsync((dynamic)query, cancellationToken);

            _logger.LogDebug("Query {QueryType} executed successfully", query.GetType().Name);
            return result;
        }

        public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
            where TEvent : IEvent
        {
            await _eventBus.PublishAsync(@event, cancellationToken);
        }
    }

    // Event interfaces
    public interface IEvent
    {
        Guid Id { get; }
        DateTime Timestamp { get; }
        string AggregateId { get; }
        int Version { get; }
    }

    public interface IEventHandler<TEvent> where TEvent : IEvent
    {
        Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
    }

    public interface IEventBus
    {
        Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IEvent;
        void Subscribe<TEvent, THandler>() 
            where TEvent : IEvent 
            where THandler : IEventHandler<TEvent>;
    }

    public class InMemoryEventBus : IEventBus
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<InMemoryEventBus> _logger;
        private readonly Dictionary<Type, List<Type>> _handlers = new();

        public InMemoryEventBus(IServiceProvider serviceProvider, ILogger<InMemoryEventBus> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
            where TEvent : IEvent
        {
            _logger.LogDebug("Publishing event {EventType} with Id {EventId}", 
                @event.GetType().Name, @event.Id);

            var eventType = @event.GetType();
            if (_handlers.TryGetValue(eventType, out var handlerTypes))
            {
                foreach (var handlerType in handlerTypes)
                {
                    var handler = _serviceProvider.GetService(handlerType);
                    if (handler != null)
                    {
                        await ((dynamic)handler).HandleAsync(@event, cancellationToken);
                    }
                }
            }

            // Also check for handlers registered via DI
            var handlers = _serviceProvider.GetServices<IEventHandler<TEvent>>();
            foreach (var handler in handlers)
            {
                await handler.HandleAsync(@event, cancellationToken);
            }

            _logger.LogDebug("Event {EventType} published successfully", @event.GetType().Name);
        }

        public void Subscribe<TEvent, THandler>() 
            where TEvent : IEvent 
            where THandler : IEventHandler<TEvent>
        {
            var eventType = typeof(TEvent);
            var handlerType = typeof(THandler);

            if (!_handlers.ContainsKey(eventType))
            {
                _handlers[eventType] = new List<Type>();
            }

            _handlers[eventType].Add(handlerType);
            _logger.LogDebug("Subscribed {HandlerType} to {EventType}", handlerType.Name, eventType.Name);
        }
    }

    // Base classes
    public abstract class CommandBase : ICommand
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public string UserId { get; set; }
    }

    public abstract class CommandBase<TResult> : ICommand<TResult>
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public string UserId { get; set; }
    }

    public abstract class QueryBase<TResult> : IQuery<TResult>
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime Timestamp { get; } = DateTime.UtcNow;
    }

    public abstract class EventBase : IEvent
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public string AggregateId { get; set; }
        public int Version { get; set; }
    }

    // Command/Query decorators
    public class LoggingCommandDecorator<TCommand> : ICommandHandler<TCommand> where TCommand : ICommand
    {
        private readonly ICommandHandler<TCommand> _handler;
        private readonly ILogger<LoggingCommandDecorator<TCommand>> _logger;

        public LoggingCommandDecorator(ICommandHandler<TCommand> handler, ILogger<LoggingCommandDecorator<TCommand>> logger)
        {
            _handler = handler;
            _logger = logger;
        }

        public async Task HandleAsync(TCommand command, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Handling command {CommandType} with Id {CommandId}", 
                typeof(TCommand).Name, command.Id);
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                await _handler.HandleAsync(command, cancellationToken);
                
                _logger.LogInformation("Command {CommandType} handled successfully in {ElapsedMs}ms", 
                    typeof(TCommand).Name, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling command {CommandType}", typeof(TCommand).Name);
                throw;
            }
        }
    }

    public class ValidationCommandDecorator<TCommand> : ICommandHandler<TCommand> where TCommand : ICommand
    {
        private readonly ICommandHandler<TCommand> _handler;
        private readonly IValidator<TCommand> _validator;

        public ValidationCommandDecorator(ICommandHandler<TCommand> handler, IValidator<TCommand> validator)
        {
            _handler = handler;
            _validator = validator;
        }

        public async Task HandleAsync(TCommand command, CancellationToken cancellationToken = default)
        {
            var validationResult = await _validator.ValidateAsync(command);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            await _handler.HandleAsync(command, cancellationToken);
        }
    }

    public class TransactionCommandDecorator<TCommand> : ICommandHandler<TCommand> where TCommand : ICommand
    {
        private readonly ICommandHandler<TCommand> _handler;
        private readonly ITransactionManager _transactionManager;

        public TransactionCommandDecorator(ICommandHandler<TCommand> handler, ITransactionManager transactionManager)
        {
            _handler = handler;
            _transactionManager = transactionManager;
        }

        public async Task HandleAsync(TCommand command, CancellationToken cancellationToken = default)
        {
            using var transaction = await _transactionManager.BeginTransactionAsync();
            
            try
            {
                await _handler.HandleAsync(command, cancellationToken);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }

    // Validation interfaces
    public interface IValidator<T>
    {
        Task<ValidationResult> ValidateAsync(T instance);
    }

    public class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<ValidationError> Errors { get; } = new();
    }

    public class ValidationError
    {
        public string PropertyName { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ValidationException : Exception
    {
        public List<ValidationError> Errors { get; }

        public ValidationException(List<ValidationError> errors) : base("Validation failed")
        {
            Errors = errors;
        }
    }

    // Transaction management
    public interface ITransactionManager
    {
        Task<ITransaction> BeginTransactionAsync();
    }

    public interface ITransaction : IDisposable
    {
        Task CommitAsync();
        Task RollbackAsync();
    }

    // Example implementations
    public class CreateUserCommand : CommandBase<Guid>
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, Guid>
    {
        private readonly ILogger<CreateUserCommandHandler> _logger;

        public CreateUserCommandHandler(ILogger<CreateUserCommandHandler> logger)
        {
            _logger = logger;
        }

        public async Task<Guid> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
        {
            // Implementation
            var userId = Guid.NewGuid();
            _logger.LogInformation("User created with Id {UserId}", userId);
            return await Task.FromResult(userId);
        }
    }

    public class GetUserByIdQuery : QueryBase<UserDto>
    {
        public Guid UserId { get; set; }
    }

    public class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, UserDto>
    {
        public async Task<UserDto> HandleAsync(GetUserByIdQuery query, CancellationToken cancellationToken = default)
        {
            // Implementation
            return await Task.FromResult(new UserDto 
            { 
                Id = query.UserId, 
                Username = "user", 
                Email = "user@example.com" 
            });
        }
    }

    public class UserCreatedEvent : EventBase
    {
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
    }

    public class UserDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
    }
}