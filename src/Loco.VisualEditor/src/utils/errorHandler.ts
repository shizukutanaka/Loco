/**
 * Standardized Error Handling Utilities
 *
 * Provides unified error handling patterns across hooks and components.
 * Implements retry logic, logging, and user-friendly error messages.
 *
 * Follows patterns from modern React error handling:
 * - Error boundaries for rendering errors
 * - Try-catch for async/event handler errors
 * - Centralized logging for monitoring
 */

// ============================================================================
// Error Types
// ============================================================================

export interface AppError {
  code: string;
  message: string;
  userMessage: string;
  details?: Record<string, unknown>;
  timestamp: number;
  severity: 'error' | 'warning' | 'info';
  recoverable: boolean;
}

export interface ApiErrorResponse {
  code?: string;
  message?: string;
  details?: Record<string, unknown>;
  statusCode?: number;
}

export class ApplicationError extends Error implements AppError {
  code: string;
  message: string;
  userMessage: string;
  details?: Record<string, unknown>;
  timestamp: number;
  severity: 'error' | 'warning' | 'info';
  recoverable: boolean;

  constructor(options: Omit<AppError, 'timestamp'>) {
    super(options.message);
    this.name = 'ApplicationError';
    this.code = options.code;
    this.message = options.message;
    this.userMessage = options.userMessage;
    this.details = options.details;
    this.timestamp = Date.now();
    this.severity = options.severity;
    this.recoverable = options.recoverable;
  }
}

// ============================================================================
// Error Factory Functions
// ============================================================================

/**
 * Create a validation error
 */
export function createValidationError(
  field: string,
  value: unknown,
  reason: string
): ApplicationError {
  return new ApplicationError({
    code: 'VALIDATION_ERROR',
    message: `Validation failed for field "${field}": ${reason}`,
    userMessage: `Invalid ${field}: ${reason}`,
    details: { field, value, reason },
    severity: 'warning',
    recoverable: true,
  });
}

/**
 * Create an API error
 */
export function createApiError(
  endpoint: string,
  statusCode: number,
  response: ApiErrorResponse
): ApplicationError {
  const recoverable = statusCode >= 500 || statusCode === 408 || statusCode === 429;

  return new ApplicationError({
    code: response.code || `HTTP_${statusCode}`,
    message: `API request failed: ${endpoint} (${statusCode}) - ${response.message || 'Unknown error'}`,
    userMessage:
      statusCode >= 500
        ? 'Server error. Please try again later.'
        : statusCode === 429
          ? 'Too many requests. Please wait a moment and try again.'
          : statusCode === 401
            ? 'Authentication failed. Please log in again.'
            : statusCode === 403
              ? 'You do not have permission to perform this action.'
              : response.message || 'Failed to complete request',
    details: { endpoint, statusCode, response },
    severity: recoverable ? 'warning' : 'error',
    recoverable,
  });
}

/**
 * Create a network error
 */
export function createNetworkError(originalError: Error): ApplicationError {
  return new ApplicationError({
    code: 'NETWORK_ERROR',
    message: `Network request failed: ${originalError.message}`,
    userMessage: 'Network connection failed. Please check your internet connection.',
    details: { originalError: originalError.message },
    severity: 'warning',
    recoverable: true,
  });
}

/**
 * Create a workflow error
 */
export function createWorkflowError(
  operation: string,
  reason: string,
  recoverable: boolean = true
): ApplicationError {
  return new ApplicationError({
    code: 'WORKFLOW_ERROR',
    message: `Workflow ${operation} failed: ${reason}`,
    userMessage: `Could not ${operation} workflow: ${reason}`,
    details: { operation, reason },
    severity: recoverable ? 'warning' : 'error',
    recoverable,
  });
}

// ============================================================================
// Error Handling Wrappers
// ============================================================================

export interface RetryOptions {
  maxRetries?: number;
  initialDelay?: number;
  maxDelay?: number;
  backoffMultiplier?: number;
  shouldRetry?: (error: unknown) => boolean;
  onRetry?: (attempt: number, error: unknown) => void;
}

const DEFAULT_RETRY_OPTIONS: Required<RetryOptions> = {
  maxRetries: 3,
  initialDelay: 100,
  maxDelay: 5000,
  backoffMultiplier: 2,
  shouldRetry: (error) => {
    if (error instanceof ApplicationError) {
      return error.recoverable;
    }
    return true;
  },
  onRetry: () => {},
};

/**
 * Execute async function with automatic retry on failure
 * Implements exponential backoff strategy
 */
export async function executeWithRetry<T>(
  fn: () => Promise<T>,
  options: RetryOptions = {}
): Promise<T> {
  const config = { ...DEFAULT_RETRY_OPTIONS, ...options };
  let lastError: unknown;
  let delay = config.initialDelay;

  for (let attempt = 1; attempt <= config.maxRetries; attempt++) {
    try {
      return await fn();
    } catch (error) {
      lastError = error;

      const shouldRetry = config.shouldRetry(error);
      if (!shouldRetry || attempt === config.maxRetries) {
        throw error;
      }

      config.onRetry(attempt, error);

      // Wait before retrying with exponential backoff
      await new Promise((resolve) => setTimeout(resolve, delay));
      delay = Math.min(delay * config.backoffMultiplier, config.maxDelay);
    }
  }

  throw lastError;
}

/**
 * Safely execute async function with error handling
 * Converts unhandled exceptions to standardized ApplicationError
 */
export async function safeAsync<T>(
  fn: () => Promise<T>,
  context: string = 'async operation'
): Promise<[T | null, ApplicationError | null]> {
  try {
    const result = await fn();
    return [result, null];
  } catch (error) {
    const appError =
      error instanceof ApplicationError
        ? error
        : new ApplicationError({
            code: 'UNKNOWN_ERROR',
            message: `Unexpected error in ${context}: ${error instanceof Error ? error.message : String(error)}`,
            userMessage: 'An unexpected error occurred. Please try again.',
            details: { context, originalError: error },
            severity: 'error',
            recoverable: false,
          });

    return [null, appError];
  }
}

/**
 * Wrap sync function with error handling
 */
export function safeSync<T, Args extends unknown[]>(
  fn: (...args: Args) => T,
  context: string = 'sync operation'
): (...args: Args) => [T | null, ApplicationError | null] {
  return (...args: Args) => {
    try {
      const result = fn(...args);
      return [result, null];
    } catch (error) {
      const appError =
        error instanceof ApplicationError
          ? error
          : new ApplicationError({
              code: 'UNKNOWN_ERROR',
              message: `Unexpected error in ${context}: ${error instanceof Error ? error.message : String(error)}`,
              userMessage: 'An unexpected error occurred.',
              details: { context, originalError: error },
              severity: 'error',
              recoverable: false,
            });

      return [null, appError];
    }
  };
}

/**
 * Create error toast configuration
 * Use with toast notifications in components
 */
export function getErrorToastConfig(error: ApplicationError | Error) {
  const appError = error instanceof ApplicationError ? error : null;

  return {
    message: appError?.userMessage || 'An error occurred',
    description: appError?.details ? JSON.stringify(appError.details) : undefined,
    variant: appError?.severity === 'error' ? 'destructive' : 'default',
    duration: appError?.recoverable ? 5000 : 10000,
    action: appError?.recoverable
      ? {
          label: 'Retry',
          onClick: () => {
            // Implement retry logic in component
          },
        }
      : undefined,
  };
}
