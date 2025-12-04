// Uncle Bob: "A class should have one, and only one, reason to change"
// John Carmack: "The obvious implementation is the best implementation"

using System.Collections.Concurrent;
using System.Diagnostics;
using Loco.Core.Practical;

namespace Loco.Core.Integrations.Core;

/// <summary>
/// Base class for all connectors with common functionality
/// Provides retry, circuit breaker, rate limiting, and logging
/// </summary>
public abstract class ConnectorBase : IConnector, IDisposable
{
    private readonly SimpleCircuitBreaker _circuitBreaker;
    private readonly ConnectorRateLimiter _rateLimiter;
    private readonly ConcurrentDictionary<string, TriggerRegistration> _registeredTriggers = new();

    protected readonly SimpleLogger Logger;
    protected ConnectorConfiguration? Configuration;
    protected bool IsInitialized;

    protected ConnectorBase()
    {
        Logger = SimpleLoggerFactory.GetLogger(GetType().Name);
        _circuitBreaker = new SimpleCircuitBreaker(
            failureThreshold: 5,
            openDuration: TimeSpan.FromMinutes(1));
        _rateLimiter = new ConnectorRateLimiter(Capabilities.RateLimitPerMinute);
    }

    // Abstract properties - must be implemented by each connector
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Version { get; }
    public abstract ConnectorCategory Category { get; }
    public virtual string IconUrl => $"/icons/{Id}.svg";
    public abstract ConnectorCapabilities Capabilities { get; }
    public abstract IReadOnlyList<ConnectorAction> Actions { get; }
    public virtual IReadOnlyList<ConnectorTrigger> Triggers => Array.Empty<ConnectorTrigger>();
    public abstract AuthenticationConfig AuthConfig { get; }
    public virtual IReadOnlyList<ConfigParameter> ConfigParameters => Array.Empty<ConfigParameter>();

    /// <summary>
    /// Test connection - override for specific implementation
    /// </summary>
    public virtual async Task<ConnectionTestResult> TestConnectionAsync(
        ConnectorConfiguration config,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await ValidateConfigurationAsync(config, ct);
            return ConnectionTestResult.Ok(responseTime: sw.Elapsed);
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Fail("Connection test failed", ex);
        }
    }

    /// <summary>
    /// Initialize the connector
    /// </summary>
    public virtual async Task InitializeAsync(
        ConnectorConfiguration config,
        CancellationToken ct = default)
    {
        await ValidateConfigurationAsync(config, ct);
        Configuration = config;
        IsInitialized = true;
        Logger.Info($"Connector {Id} initialized");
    }

    /// <summary>
    /// Execute an action with retry and circuit breaker
    /// </summary>
    public async Task<ActionResult> ExecuteAsync(
        string actionName,
        ActionParameters parameters,
        ExecutionContext context,
        CancellationToken ct = default)
    {
        EnsureInitialized();

        var action = Actions.FirstOrDefault(a =>
            a.Id.Equals(actionName, StringComparison.OrdinalIgnoreCase));

        if (action == null)
        {
            return ActionResult.Fail($"Action '{actionName}' not found", "ACTION_NOT_FOUND");
        }

        // Rate limiting
        if (!await _rateLimiter.AllowRequestAsync(ct))
        {
            return ActionResult.Fail("Rate limit exceeded", "RATE_LIMIT_EXCEEDED");
        }

        var sw = Stopwatch.StartNew();

        try
        {
            // Execute with circuit breaker
            var result = await _circuitBreaker.ExecuteAsync(async () =>
            {
                // Execute with retry if configured
                if (action.RetryConfig != null)
                {
                    return await ExecuteWithRetryAsync(
                        () => ExecuteActionCoreAsync(action, parameters, context, ct),
                        action.RetryConfig,
                        ct);
                }

                return await ExecuteActionCoreAsync(action, parameters, context, ct);
            });

            Logger.Debug($"Action {actionName} completed in {sw.ElapsedMilliseconds}ms");
            return result with { Duration = sw.Elapsed };
        }
        catch (CircuitBreakerOpenException)
        {
            return ActionResult.Fail("Service temporarily unavailable (circuit breaker open)", "CIRCUIT_OPEN");
        }
        catch (Exception ex)
        {
            Logger.Error($"Action {actionName} failed", ex);
            return ActionResult.Fail(ex.Message, "EXECUTION_ERROR");
        }
    }

    /// <summary>
    /// Core action execution - must be implemented by each connector
    /// </summary>
    protected abstract Task<ActionResult> ExecuteActionCoreAsync(
        ConnectorAction action,
        ActionParameters parameters,
        ExecutionContext context,
        CancellationToken ct);

    /// <summary>
    /// Register a trigger - override for webhook/polling support
    /// </summary>
    public virtual Task<TriggerRegistration> RegisterTriggerAsync(
        string triggerName,
        TriggerConfiguration config,
        CancellationToken ct = default)
    {
        if (!Capabilities.SupportsTriggers)
        {
            throw new NotSupportedException($"Connector {Id} does not support triggers");
        }

        var trigger = Triggers.FirstOrDefault(t =>
            t.Id.Equals(triggerName, StringComparison.OrdinalIgnoreCase));

        if (trigger == null)
        {
            throw new ArgumentException($"Trigger '{triggerName}' not found");
        }

        var registration = new TriggerRegistration
        {
            RegistrationId = Guid.NewGuid().ToString("N"),
            TriggerId = trigger.Id,
            WebhookEndpoint = config.WebhookUrl
        };

        _registeredTriggers[registration.RegistrationId] = registration;
        Logger.Info($"Trigger {triggerName} registered with ID {registration.RegistrationId}");

        return Task.FromResult(registration);
    }

    /// <summary>
    /// Unregister a trigger
    /// </summary>
    public virtual Task UnregisterTriggerAsync(
        string registrationId,
        CancellationToken ct = default)
    {
        if (_registeredTriggers.TryRemove(registrationId, out var registration))
        {
            Logger.Info($"Trigger {registration.TriggerId} unregistered");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleanup resources
    /// </summary>
    public virtual Task CleanupAsync(CancellationToken ct = default)
    {
        _registeredTriggers.Clear();
        IsInitialized = false;
        Logger.Info($"Connector {Id} cleaned up");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Validate configuration
    /// </summary>
    protected virtual Task ValidateConfigurationAsync(
        ConnectorConfiguration config,
        CancellationToken ct)
    {
        // Validate required credentials
        foreach (var cred in AuthConfig.RequiredCredentials.Where(c => c.Required))
        {
            if (!config.Credentials.ContainsKey(cred.Name) ||
                config.Credentials[cred.Name] == null ||
                string.IsNullOrWhiteSpace(config.Credentials[cred.Name]?.ToString()))
            {
                throw new ArgumentException($"Required credential '{cred.Name}' is missing");
            }
        }

        // Validate required config parameters
        foreach (var param in ConfigParameters.Where(p => p.Required))
        {
            if (!config.Settings.ContainsKey(param.Name) ||
                config.Settings[param.Name] == null)
            {
                throw new ArgumentException($"Required configuration '{param.Name}' is missing");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Ensure connector is initialized
    /// </summary>
    protected void EnsureInitialized()
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException($"Connector {Id} is not initialized. Call InitializeAsync first.");
        }
    }

    /// <summary>
    /// Execute with retry policy
    /// </summary>
    private async Task<ActionResult> ExecuteWithRetryAsync(
        Func<Task<ActionResult>> operation,
        RetryConfig config,
        CancellationToken ct)
    {
        ActionResult? lastResult = null;
        var delay = config.InitialDelay;

        for (int attempt = 0; attempt < config.MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            lastResult = await operation();

            if (lastResult.Success)
            {
                return lastResult;
            }

            // Check if error is retryable
            if (!IsRetryableError(lastResult, config))
            {
                return lastResult;
            }

            if (attempt < config.MaxAttempts - 1)
            {
                Logger.Debug($"Retry attempt {attempt + 1}/{config.MaxAttempts} after {delay.TotalMilliseconds}ms");
                await Task.Delay(delay, ct);

                if (config.UseExponentialBackoff)
                {
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * config.BackoffMultiplier);
                }
            }
        }

        return lastResult ?? ActionResult.Fail("Max retry attempts exceeded");
    }

    private static bool IsRetryableError(ActionResult result, RetryConfig config)
    {
        if (result.Success) return false;

        // Check for retryable error codes
        if (result.ErrorCode != null &&
            int.TryParse(result.ErrorCode, out var statusCode) &&
            config.RetryableStatusCodes.Contains(statusCode))
        {
            return true;
        }

        // Check for specific error messages that indicate transient failures
        var message = result.ErrorMessage?.ToLowerInvariant() ?? "";
        return message.Contains("timeout") ||
               message.Contains("connection") ||
               message.Contains("temporarily");
    }

    public virtual void Dispose()
    {
        _rateLimiter.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Simple circuit breaker for connector resilience
/// </summary>
internal sealed class SimpleCircuitBreaker
{
    private enum State { Closed, Open, HalfOpen }

    private State _state = State.Closed;
    private int _failureCount;
    private DateTime _lastFailureTime;
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly object _lock = new();

    public SimpleCircuitBreaker(int failureThreshold = 5, TimeSpan? openDuration = null)
    {
        _failureThreshold = failureThreshold;
        _openDuration = openDuration ?? TimeSpan.FromMinutes(1);
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        lock (_lock)
        {
            if (_state == State.Open)
            {
                if (DateTime.UtcNow - _lastFailureTime > _openDuration)
                {
                    _state = State.HalfOpen;
                }
                else
                {
                    throw new CircuitBreakerOpenException();
                }
            }
        }

        try
        {
            var result = await operation();
            OnSuccess();
            return result;
        }
        catch
        {
            OnFailure();
            throw;
        }
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = State.Closed;
        }
    }

    private void OnFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold)
            {
                _state = State.Open;
            }
        }
    }

    public string GetStatus() => _state.ToString();
}

/// <summary>
/// Circuit breaker open exception
/// </summary>
public sealed class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException()
        : base("Circuit breaker is open") { }
}

/// <summary>
/// Connector rate limiter
/// </summary>
internal sealed class ConnectorRateLimiter : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly int _requestsPerMinute;
    private readonly Queue<DateTime> _requestTimestamps = new();
    private readonly object _lock = new();

    public ConnectorRateLimiter(int requestsPerMinute)
    {
        _requestsPerMinute = requestsPerMinute;
        _semaphore = new SemaphoreSlim(1, 1);
    }

    public async Task<bool> AllowRequestAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            var windowStart = now.AddMinutes(-1);

            // Remove old timestamps
            while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < windowStart)
            {
                _requestTimestamps.Dequeue();
            }

            if (_requestTimestamps.Count >= _requestsPerMinute)
            {
                return false;
            }

            _requestTimestamps.Enqueue(now);
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose() => _semaphore.Dispose();
}
