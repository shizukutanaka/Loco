// Robert C. Martin: "States should be simple and transitions clear"
// John Carmack: "State machines eliminate entire categories of bugs"

using System.Collections.Concurrent;

namespace Loco.Core.Practical;

/// <summary>
/// Simple state machine - Clear states, explicit transitions, no magic
/// Thread-safe, easy to debug, zero dependencies
/// </summary>
public class SimpleStateMachine<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    private readonly Dictionary<TState, Dictionary<TTrigger, TState>> _transitions = new();
    private readonly Dictionary<TState, Action?> _onEnterActions = new();
    private readonly Dictionary<TState, Action?> _onExitActions = new();
    private TState _currentState;
    private readonly object _lock = new();
    private readonly SimpleLogger _logger;

    public TState CurrentState
    {
        get
        {
            lock (_lock)
            {
                return _currentState;
            }
        }
    }

    public SimpleStateMachine(TState initialState, SimpleLogger? logger = null)
    {
        _currentState = initialState;
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleStateMachine<TState, TTrigger>));
    }

    // Configure state transition
    public SimpleStateMachine<TState, TTrigger> Configure(TState state)
    {
        if (!_transitions.ContainsKey(state))
        {
            _transitions[state] = new Dictionary<TTrigger, TState>();
        }
        return this;
    }

    // Add transition
    public SimpleStateMachine<TState, TTrigger> Permit(TState from, TTrigger trigger, TState to)
    {
        if (!_transitions.ContainsKey(from))
        {
            _transitions[from] = new Dictionary<TTrigger, TState>();
        }
        _transitions[from][trigger] = to;
        return this;
    }

    // Add enter action
    public SimpleStateMachine<TState, TTrigger> OnEnter(TState state, Action action)
    {
        _onEnterActions[state] = action;
        return this;
    }

    // Add exit action
    public SimpleStateMachine<TState, TTrigger> OnExit(TState state, Action action)
    {
        _onExitActions[state] = action;
        return this;
    }

    // Fire trigger
    public bool Fire(TTrigger trigger)
    {
        lock (_lock)
        {
            if (!_transitions.TryGetValue(_currentState, out var stateTransitions))
            {
                _logger.Warning($"No transitions configured for state {_currentState}");
                return false;
            }

            if (!stateTransitions.TryGetValue(trigger, out var nextState))
            {
                _logger.Warning($"No transition for trigger {trigger} from state {_currentState}");
                return false;
            }

            // Execute exit action
            if (_onExitActions.TryGetValue(_currentState, out var exitAction))
            {
                exitAction?.Invoke();
            }

            var previousState = _currentState;
            _currentState = nextState;

            // Execute enter action
            if (_onEnterActions.TryGetValue(_currentState, out var enterAction))
            {
                enterAction?.Invoke();
            }

            _logger.Info($"State transition: {previousState} -> {_currentState} (trigger: {trigger})");
            return true;
        }
    }

    // Check if trigger is allowed
    public bool CanFire(TTrigger trigger)
    {
        lock (_lock)
        {
            if (!_transitions.TryGetValue(_currentState, out var stateTransitions))
            {
                return false;
            }
            return stateTransitions.ContainsKey(trigger);
        }
    }

    // Get permitted triggers for current state
    public IEnumerable<TTrigger> GetPermittedTriggers()
    {
        lock (_lock)
        {
            if (_transitions.TryGetValue(_currentState, out var stateTransitions))
            {
                return stateTransitions.Keys.ToList();
            }
            return Enumerable.Empty<TTrigger>();
        }
    }
}

/// <summary>
/// Async state machine with async actions
/// </summary>
public class AsyncStateMachine<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    private readonly Dictionary<TState, Dictionary<TTrigger, TState>> _transitions = new();
    private readonly Dictionary<TState, Func<Task>?> _onEnterActions = new();
    private readonly Dictionary<TState, Func<Task>?> _onExitActions = new();
    private readonly Dictionary<(TState from, TTrigger trigger, TState to), Func<Task>?> _transitionActions = new();
    private TState _currentState;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly SimpleLogger _logger;

    public TState CurrentState => _currentState;

    public AsyncStateMachine(TState initialState, SimpleLogger? logger = null)
    {
        _currentState = initialState;
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(AsyncStateMachine<TState, TTrigger>));
    }

    public AsyncStateMachine<TState, TTrigger> Permit(TState from, TTrigger trigger, TState to)
    {
        if (!_transitions.ContainsKey(from))
        {
            _transitions[from] = new Dictionary<TTrigger, TState>();
        }
        _transitions[from][trigger] = to;
        return this;
    }

    public AsyncStateMachine<TState, TTrigger> OnEnterAsync(TState state, Func<Task> action)
    {
        _onEnterActions[state] = action;
        return this;
    }

    public AsyncStateMachine<TState, TTrigger> OnExitAsync(TState state, Func<Task> action)
    {
        _onExitActions[state] = action;
        return this;
    }

    public AsyncStateMachine<TState, TTrigger> OnTransitionAsync(TState from, TTrigger trigger, TState to, Func<Task> action)
    {
        _transitionActions[(from, trigger, to)] = action;
        return this;
    }

    public async Task<bool> FireAsync(TTrigger trigger)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!_transitions.TryGetValue(_currentState, out var stateTransitions))
            {
                return false;
            }

            if (!stateTransitions.TryGetValue(trigger, out var nextState))
            {
                return false;
            }

            // Execute exit action
            if (_onExitActions.TryGetValue(_currentState, out var exitAction) && exitAction != null)
            {
                await exitAction();
            }

            var previousState = _currentState;

            // Execute transition action
            if (_transitionActions.TryGetValue((previousState, trigger, nextState), out var transitionAction) && transitionAction != null)
            {
                await transitionAction();
            }

            _currentState = nextState;

            // Execute enter action
            if (_onEnterActions.TryGetValue(_currentState, out var enterAction) && enterAction != null)
            {
                await enterAction();
            }

            _logger.Info($"Async state transition: {previousState} -> {_currentState} (trigger: {trigger})");
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

/// <summary>
/// Example: Order state machine
/// </summary>
public enum OrderState
{
    Pending,
    Confirmed,
    Processing,
    Shipped,
    Delivered,
    Cancelled,
    Refunded
}

public enum OrderTrigger
{
    Confirm,
    StartProcessing,
    Ship,
    Deliver,
    Cancel,
    Refund
}

public class OrderStateMachine
{
    private readonly SimpleStateMachine<OrderState, OrderTrigger> _machine;
    public OrderState CurrentState => _machine.CurrentState;

    public OrderStateMachine()
    {
        _machine = new SimpleStateMachine<OrderState, OrderTrigger>(OrderState.Pending);

        // Configure transitions
        _machine
            .Permit(OrderState.Pending, OrderTrigger.Confirm, OrderState.Confirmed)
            .Permit(OrderState.Pending, OrderTrigger.Cancel, OrderState.Cancelled)

            .Permit(OrderState.Confirmed, OrderTrigger.StartProcessing, OrderState.Processing)
            .Permit(OrderState.Confirmed, OrderTrigger.Cancel, OrderState.Cancelled)

            .Permit(OrderState.Processing, OrderTrigger.Ship, OrderState.Shipped)

            .Permit(OrderState.Shipped, OrderTrigger.Deliver, OrderState.Delivered)

            .Permit(OrderState.Cancelled, OrderTrigger.Refund, OrderState.Refunded);

        // Configure actions
        _machine
            .OnEnter(OrderState.Confirmed, () => Console.WriteLine("Order confirmed! Sending confirmation email..."))
            .OnEnter(OrderState.Shipped, () => Console.WriteLine("Order shipped! Sending tracking info..."))
            .OnEnter(OrderState.Delivered, () => Console.WriteLine("Order delivered! Request feedback..."))
            .OnEnter(OrderState.Cancelled, () => Console.WriteLine("Order cancelled! Process refund..."));
    }

    public bool ProcessTrigger(OrderTrigger trigger) => _machine.Fire(trigger);
    public bool CanProcess(OrderTrigger trigger) => _machine.CanFire(trigger);
    public IEnumerable<OrderTrigger> GetAvailableActions() => _machine.GetPermittedTriggers();
}

/// <summary>
/// Example: Connection state machine
/// </summary>
public class ConnectionStateMachine
{
    public enum State
    {
        Disconnected,
        Connecting,
        Connected,
        Disconnecting,
        Failed
    }

    public enum Trigger
    {
        Connect,
        ConnectionEstablished,
        ConnectionFailed,
        Disconnect,
        ConnectionLost,
        Retry
    }

    private readonly AsyncStateMachine<State, Trigger> _machine;
    private readonly Func<Task<bool>> _connectAction;
    private readonly Func<Task> _disconnectAction;
    private int _retryCount;

    public State CurrentState => _machine.CurrentState;

    public ConnectionStateMachine(
        Func<Task<bool>> connectAction,
        Func<Task> disconnectAction)
    {
        _connectAction = connectAction;
        _disconnectAction = disconnectAction;
        _machine = new AsyncStateMachine<State, Trigger>(State.Disconnected);

        ConfigureStateMachine();
    }

    private void ConfigureStateMachine()
    {
        _machine
            // From Disconnected
            .Permit(State.Disconnected, Trigger.Connect, State.Connecting)

            // From Connecting
            .Permit(State.Connecting, Trigger.ConnectionEstablished, State.Connected)
            .Permit(State.Connecting, Trigger.ConnectionFailed, State.Failed)

            // From Connected
            .Permit(State.Connected, Trigger.Disconnect, State.Disconnecting)
            .Permit(State.Connected, Trigger.ConnectionLost, State.Disconnected)

            // From Disconnecting
            .Permit(State.Disconnecting, Trigger.Disconnect, State.Disconnected)

            // From Failed
            .Permit(State.Failed, Trigger.Retry, State.Connecting)
            .Permit(State.Failed, Trigger.Connect, State.Connecting);

        // Configure async actions
        _machine
            .OnEnterAsync(State.Connecting, async () =>
            {
                Console.WriteLine("Attempting to connect...");
                var success = await _connectAction();

                if (success)
                {
                    _retryCount = 0;
                    await _machine.FireAsync(Trigger.ConnectionEstablished);
                }
                else
                {
                    _retryCount++;
                    await _machine.FireAsync(Trigger.ConnectionFailed);
                }
            })

            .OnEnterAsync(State.Connected, async () =>
            {
                Console.WriteLine("Successfully connected!");
                await Task.CompletedTask;
            })

            .OnEnterAsync(State.Disconnecting, async () =>
            {
                Console.WriteLine("Disconnecting...");
                await _disconnectAction();
                await _machine.FireAsync(Trigger.Disconnect);
            })

            .OnEnterAsync(State.Failed, async () =>
            {
                Console.WriteLine($"Connection failed. Retry count: {_retryCount}");
                if (_retryCount < 3)
                {
                    await Task.Delay(1000 * _retryCount); // Exponential backoff
                    await _machine.FireAsync(Trigger.Retry);
                }
            });
    }

    public async Task ConnectAsync() => await _machine.FireAsync(Trigger.Connect);
    public async Task DisconnectAsync() => await _machine.FireAsync(Trigger.Disconnect);
    public async Task HandleConnectionLostAsync() => await _machine.FireAsync(Trigger.ConnectionLost);
}

/// <summary>
/// Example: Workflow state machine
/// </summary>
public class WorkflowStateMachine
{
    public enum State
    {
        NotStarted,
        Running,
        Paused,
        Completed,
        Failed,
        Cancelled
    }

    public enum Trigger
    {
        Start,
        Pause,
        Resume,
        Complete,
        Fail,
        Cancel,
        Retry
    }

    private readonly SimpleStateMachine<State, Trigger> _machine;

    public State CurrentState => _machine.CurrentState;
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public TimeSpan? Duration => StartedAt.HasValue && CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;

    public WorkflowStateMachine()
    {
        _machine = new SimpleStateMachine<State, Trigger>(State.NotStarted);

        _machine
            .Permit(State.NotStarted, Trigger.Start, State.Running)

            .Permit(State.Running, Trigger.Pause, State.Paused)
            .Permit(State.Running, Trigger.Complete, State.Completed)
            .Permit(State.Running, Trigger.Fail, State.Failed)
            .Permit(State.Running, Trigger.Cancel, State.Cancelled)

            .Permit(State.Paused, Trigger.Resume, State.Running)
            .Permit(State.Paused, Trigger.Cancel, State.Cancelled)

            .Permit(State.Failed, Trigger.Retry, State.Running)
            .Permit(State.Failed, Trigger.Cancel, State.Cancelled);

        _machine
            .OnEnter(State.Running, () =>
            {
                if (!StartedAt.HasValue)
                    StartedAt = DateTime.UtcNow;
            })
            .OnEnter(State.Completed, () => CompletedAt = DateTime.UtcNow)
            .OnEnter(State.Failed, () => CompletedAt = DateTime.UtcNow)
            .OnEnter(State.Cancelled, () => CompletedAt = DateTime.UtcNow);
    }

    public bool Execute(Trigger trigger) => _machine.Fire(trigger);
    public bool CanExecute(Trigger trigger) => _machine.CanFire(trigger);
}