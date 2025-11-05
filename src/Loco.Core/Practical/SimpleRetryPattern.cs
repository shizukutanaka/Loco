// Rob Pike: "Errors are values"
// John Carmack: "Prefer simple, robust code over clever code"

namespace Loco.Core.Practical;

/// <summary>
/// Simple retry with exponential backoff
/// No complex policies, just what works
/// </summary>
public static class SimpleRetry
{
    public static async Task<T?> ExecuteAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        int baseDelayMs = 100)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt < maxAttempts - 1)
                {
                    // Simple exponential backoff: 100ms, 200ms, 400ms...
                    var delay = baseDelayMs * (1 << attempt);
                    await Task.Delay(delay);
                }
            }
        }

        throw new AggregateException($"Failed after {maxAttempts} attempts", lastException!);
    }

    // Synchronous version for simple cases
    public static T? Execute<T>(
        Func<T> operation,
        int maxAttempts = 3,
        int baseDelayMs = 100)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt < maxAttempts - 1)
                {
                    Thread.Sleep(baseDelayMs * (1 << attempt));
                }
            }
        }

        throw new AggregateException($"Failed after {maxAttempts} attempts", lastException!);
    }
}