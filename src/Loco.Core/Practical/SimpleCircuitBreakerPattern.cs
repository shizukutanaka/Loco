// Uncle Bob: "The first rule of functions is that they should be small"
// John Carmack: "If you're not measuring, you're not engineering"

namespace Loco.Core.Practical;

/// <summary>
/// Dead simple circuit breaker - no fancy features
/// Just prevents cascading failures in distributed systems
/// </summary>
public class SimpleCircuitBreaker
{
    private enum State { Closed, Open, HalfOpen }

    private State _state = State.Closed;
    private int _failureCount;
    private DateTime _lastFailureTime;
    private readonly int _threshold;
    private readonly TimeSpan _timeout;
    private readonly object _lock = new();

    public SimpleCircuitBreaker(int failureThreshold = 5, int timeoutSeconds = 30)
    {
        _threshold = failureThreshold;
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    public async Task<T?> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (!CanExecute())
            throw new InvalidOperationException("Circuit breaker is open");

        try
        {
            var result = await operation();
            OnSuccess();
            return result;
        }
        catch (Exception)
        {
            OnFailure();
            throw;
        }
    }

    private bool CanExecute()
    {
        lock (_lock)
        {
            switch (_state)
            {
                case State.Closed:
                    return true;

                case State.Open:
                    if (DateTime.UtcNow - _lastFailureTime > _timeout)
                    {
                        _state = State.HalfOpen;
                        return true;
                    }
                    return false;

                case State.HalfOpen:
                    return true;

                default:
                    return false;
            }
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

            if (_failureCount >= _threshold)
            {
                _state = State.Open;
            }
        }
    }

    public string GetStatus() => _state.ToString();
    public int GetFailureCount() => _failureCount;
}