/**
 * Retry Utility
 *
 * Provides retry logic for failed operations with exponential backoff.
 */

// ============================================================================
// Types
// ============================================================================

export interface RetryOptions {
  maxRetries?: number;
  initialDelay?: number;
  maxDelay?: number;
  backoffMultiplier?: number;
  shouldRetry?: (error: unknown) => boolean;
  onRetry?: (attempt: number, error: unknown) => void;
}

// ============================================================================
// Retry Function
// ============================================================================

/**
 * Retry an async operation with exponential backoff
 *
 * @param fn - The async function to retry
 * @param options - Retry configuration options
 * @returns Promise that resolves with the function result or rejects after all retries
 *
 * @example
 * const result = await retryOperation(
 *   () => apiClient.get('/workflows'),
 *   {
 *     maxRetries: 3,
 *     initialDelay: 1000,
 *     onRetry: (attempt) => console.log(`Retry attempt ${attempt}`)
 *   }
 * );
 */
export async function retryOperation<T>(
  fn: () => Promise<T>,
  options: RetryOptions = {}
): Promise<T> {
  const {
    maxRetries = 3,
    initialDelay = 1000,
    maxDelay = 10000,
    backoffMultiplier = 2,
    shouldRetry = () => true,
    onRetry,
  } = options;

  let lastError: unknown;
  let delay = initialDelay;

  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    try {
      // Try to execute the function
      return await fn();
    } catch (error) {
      lastError = error;

      // Check if we should retry
      if (attempt === maxRetries || !shouldRetry(error)) {
        throw error;
      }

      // Call retry callback
      if (onRetry) {
        onRetry(attempt + 1, error);
      }

      // Wait before retrying
      await sleep(delay);

      // Increase delay with backoff (capped at maxDelay)
      delay = Math.min(delay * backoffMultiplier, maxDelay);
    }
  }

  // Should never reach here, but TypeScript needs this
  throw lastError;
}

/**
 * Sleep for a specified duration
 */
function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

// ============================================================================
// Specialized Retry Functions
// ============================================================================

/**
 * Retry a network operation (e.g., API call)
 *
 * Automatically retries on network errors (5xx, timeout, network failure)
 * but not on client errors (4xx)
 */
export async function retryNetworkOperation<T>(
  fn: () => Promise<T>,
  options: Omit<RetryOptions, 'shouldRetry'> = {}
): Promise<T> {
  return retryOperation(fn, {
    ...options,
    shouldRetry: (error: unknown) => {
      // Retry on network errors but not on client errors
      if (error && typeof error === 'object' && 'code' in error) {
        const code = (error as { code: string }).code;

        // Don't retry client errors (4xx)
        if (code.startsWith('HTTP_4')) {
          return false;
        }

        // Retry network errors, timeouts, and server errors (5xx)
        return (
          code === 'NETWORK_ERROR' ||
          code === 'TIMEOUT' ||
          code.startsWith('HTTP_5')
        );
      }

      // Retry by default if we can't determine the error type
      return true;
    },
  });
}

/**
 * Retry an operation with a custom shouldRetry predicate
 */
export function createRetryFunction<T>(
  shouldRetryPredicate: (error: unknown) => boolean
) {
  return (fn: () => Promise<T>, options: Omit<RetryOptions, 'shouldRetry'> = {}) => {
    return retryOperation(fn, {
      ...options,
      shouldRetry: shouldRetryPredicate,
    });
  };
}
