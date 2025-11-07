/**
 * Error Logger Utility
 *
 * Provides centralized error logging with categorization, context,
 * and optional backend reporting.
 */

// ============================================================================
// Types
// ============================================================================

export type ErrorSeverity = 'low' | 'medium' | 'high' | 'critical';
export type ErrorCategory =
  | 'network'
  | 'api'
  | 'validation'
  | 'ui'
  | 'workflow'
  | 'storage'
  | 'unknown';

export interface LoggedError {
  id: string;
  timestamp: string;
  severity: ErrorSeverity;
  category: ErrorCategory;
  message: string;
  error?: Error;
  context?: Record<string, unknown>;
  stackTrace?: string;
  userAgent: string;
  url: string;
}

// ============================================================================
// Error Logger Class
// ============================================================================

class ErrorLogger {
  private errors: LoggedError[] = [];
  private maxStoredErrors = 100;
  private enableConsoleLog = true;
  private enableRemoteLog = false; // Set to true in production

  /**
   * Log an error with context
   */
  log(
    message: string,
    options: {
      error?: Error;
      severity?: ErrorSeverity;
      category?: ErrorCategory;
      context?: Record<string, unknown>;
    } = {}
  ): string {
    const {
      error,
      severity = 'medium',
      category = 'unknown',
      context = {},
    } = options;

    // Generate unique error ID
    const errorId = `err-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;

    // Create logged error object
    const loggedError: LoggedError = {
      id: errorId,
      timestamp: new Date().toISOString(),
      severity,
      category,
      message,
      error,
      context,
      stackTrace: error?.stack,
      userAgent: navigator.userAgent,
      url: window.location.href,
    };

    // Add to in-memory storage
    this.errors.push(loggedError);

    // Limit stored errors
    if (this.errors.length > this.maxStoredErrors) {
      this.errors.shift();
    }

    // Console logging
    if (this.enableConsoleLog) {
      this.logToConsole(loggedError);
    }

    // Remote logging (if enabled)
    if (this.enableRemoteLog) {
      this.logToRemote(loggedError);
    }

    return errorId;
  }

  /**
   * Log to browser console with formatting
   */
  private logToConsole(loggedError: LoggedError): void {
    const { severity, category, message, error, context } = loggedError;

    // Color coding by severity
    const severityColors: Record<ErrorSeverity, string> = {
      low: 'color: #4CAF50',
      medium: 'color: #FF9800',
      high: 'color: #F44336',
      critical: 'color: #D32F2F; font-weight: bold',
    };

    console.group(
      `%c[${severity.toUpperCase()}] ${category}: ${message}`,
      severityColors[severity]
    );
    console.log('Error ID:', loggedError.id);
    console.log('Timestamp:', loggedError.timestamp);

    if (error) {
      console.error('Error:', error);
    }

    if (context && Object.keys(context).length > 0) {
      console.log('Context:', context);
    }

    console.groupEnd();
  }

  /**
   * Send error to remote logging service
   */
  private async logToRemote(loggedError: LoggedError): Promise<void> {
    try {
      // In production, send to error logging service
      await fetch('/api/v1/errors', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(loggedError),
      });
    } catch (error) {
      // Silently fail if remote logging fails
      console.warn('Failed to send error to remote service:', error);
    }
  }

  /**
   * Get all logged errors
   */
  getErrors(options: {
    severity?: ErrorSeverity;
    category?: ErrorCategory;
    limit?: number;
  } = {}): LoggedError[] {
    const { severity, category, limit } = options;

    let filtered = [...this.errors];

    if (severity) {
      filtered = filtered.filter((e) => e.severity === severity);
    }

    if (category) {
      filtered = filtered.filter((e) => e.category === category);
    }

    if (limit) {
      filtered = filtered.slice(-limit);
    }

    return filtered;
  }

  /**
   * Clear all logged errors
   */
  clearErrors(): void {
    this.errors = [];
  }

  /**
   * Get error by ID
   */
  getErrorById(id: string): LoggedError | undefined {
    return this.errors.find((e) => e.id === id);
  }

  /**
   * Enable/disable console logging
   */
  setConsoleLogging(enabled: boolean): void {
    this.enableConsoleLog = enabled;
  }

  /**
   * Enable/disable remote logging
   */
  setRemoteLogging(enabled: boolean): void {
    this.enableRemoteLog = enabled;
  }

  /**
   * Export errors as JSON
   */
  exportErrors(): string {
    return JSON.stringify(this.errors, null, 2);
  }
}

// ============================================================================
// Singleton Instance
// ============================================================================

export const errorLogger = new ErrorLogger();

// ============================================================================
// Convenience Functions
// ============================================================================

/**
 * Log a network error
 */
export function logNetworkError(message: string, error?: Error, context?: Record<string, unknown>): string {
  return errorLogger.log(message, {
    error,
    severity: 'high',
    category: 'network',
    context,
  });
}

/**
 * Log an API error
 */
export function logApiError(message: string, error?: Error, context?: Record<string, unknown>): string {
  return errorLogger.log(message, {
    error,
    severity: 'medium',
    category: 'api',
    context,
  });
}

/**
 * Log a validation error
 */
export function logValidationError(message: string, context?: Record<string, unknown>): string {
  return errorLogger.log(message, {
    severity: 'low',
    category: 'validation',
    context,
  });
}

/**
 * Log a UI error
 */
export function logUiError(message: string, error?: Error, context?: Record<string, unknown>): string {
  return errorLogger.log(message, {
    error,
    severity: 'medium',
    category: 'ui',
    context,
  });
}

/**
 * Log a workflow error
 */
export function logWorkflowError(message: string, error?: Error, context?: Record<string, unknown>): string {
  return errorLogger.log(message, {
    error,
    severity: 'high',
    category: 'workflow',
    context,
  });
}

/**
 * Log a storage error
 */
export function logStorageError(message: string, error?: Error, context?: Record<string, unknown>): string {
  return errorLogger.log(message, {
    error,
    severity: 'medium',
    category: 'storage',
    context,
  });
}

/**
 * Log a critical error
 */
export function logCriticalError(message: string, error?: Error, context?: Record<string, unknown>): string {
  return errorLogger.log(message, {
    error,
    severity: 'critical',
    category: 'unknown',
    context,
  });
}
