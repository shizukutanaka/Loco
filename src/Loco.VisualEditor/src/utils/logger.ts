/**
 * Structured Logging Utility
 *
 * Provides a centralized logging system with severity levels,
 * component tracking, and optional error reporting service integration.
 * Replaces scattered console.log/error statements throughout the app.
 */

export type LogLevel = 'debug' | 'info' | 'warn' | 'error';

interface LogEntry {
  level: LogLevel;
  message: string;
  timestamp: string;
  component?: string;
  data?: unknown;
  error?: Error;
}

// In-memory log buffer for debugging
const logBuffer: LogEntry[] = [];
const MAX_LOG_BUFFER_SIZE = 100;

/**
 * Add entry to in-memory log buffer (for debugging/crash reports)
 */
function addToBuffer(entry: LogEntry): void {
  logBuffer.push(entry);
  if (logBuffer.length > MAX_LOG_BUFFER_SIZE) {
    logBuffer.shift();
  }
}

/**
 * Get recent logs from buffer
 */
export function getRecentLogs(count: number = 20): LogEntry[] {
  return logBuffer.slice(-count);
}

/**
 * Clear log buffer
 */
export function clearLogs(): void {
  logBuffer.length = 0;
}

/**
 * Export logs as JSON for crash reports
 */
export function exportLogsAsJson(): string {
  return JSON.stringify(logBuffer, null, 2);
}

/**
 * Format log message for console output
 */
function formatMessage(level: LogLevel, component: string | undefined, message: string): string {
  const timestamp = new Date().toISOString().substring(11, 19); // HH:MM:SS
  const prefix = component ? `[${timestamp}] [${level.toUpperCase()}] ${component}` : `[${timestamp}] [${level.toUpperCase()}]`;
  return `${prefix}: ${message}`;
}

/**
 * Report error to external service (Sentry, LogRocket, etc.)
 * Hook this to your error reporting service
 */
function reportError(_entry: LogEntry): void {
  if (import.meta.env.PROD) {
    // TODO: Send to error reporting service
    // Example: Sentry.captureException(_entry.error, { extra: _entry.data });
    // Example: LogRocket.captureException(_entry.error);
  }
}

/**
 * Main logger object with methods for each log level
 */
export const logger = {
  /**
   * Debug level - Only logged in development
   */
  debug: (message: string, data?: unknown, component?: string): void => {
    if (import.meta.env.DEV) {
      const entry: LogEntry = {
        level: 'debug',
        message,
        timestamp: new Date().toISOString(),
        component,
        data,
      };
      addToBuffer(entry);
      console.debug(formatMessage('debug', component, message), data || '');
    }
  },

  /**
   * Info level - General information messages
   */
  info: (message: string, data?: unknown, component?: string): void => {
    const entry: LogEntry = {
      level: 'info',
      message,
      timestamp: new Date().toISOString(),
      component,
      data,
    };
    addToBuffer(entry);
    console.info(formatMessage('info', component, message), data || '');
  },

  /**
   * Warn level - Warning messages for potentially problematic situations
   */
  warn: (message: string, data?: unknown, component?: string): void => {
    const entry: LogEntry = {
      level: 'warn',
      message,
      timestamp: new Date().toISOString(),
      component,
      data,
    };
    addToBuffer(entry);
    console.warn(formatMessage('warn', component, message), data || '');
  },

  /**
   * Error level - Error messages with optional error object
   * Also reports to external service in production
   */
  error: (message: string, error?: unknown, component?: string): void => {
    const errorObj = error instanceof Error ? error : new Error(String(error));
    const entry: LogEntry = {
      level: 'error',
      message,
      timestamp: new Date().toISOString(),
      component,
      error: errorObj,
      data: {
        stack: errorObj.stack,
        name: errorObj.name,
      },
    };
    addToBuffer(entry);
    console.error(formatMessage('error', component, message), errorObj);

    // Report to external service
    reportError(entry);
  },

  /**
   * Get buffer for crash reports
   */
  getBuffer: getRecentLogs,

  /**
   * Clear buffer
   */
  clearBuffer: clearLogs,

  /**
   * Export logs
   */
  export: exportLogsAsJson,
};

/**
 * Create a component-scoped logger
 * All logs will automatically include the component name
 *
 * @example
 * const componentLogger = createComponentLogger('PropertyPanel');
 * componentLogger.info('User changed property'); // [INFO] PropertyPanel: User changed property
 */
export function createComponentLogger(componentName: string) {
  return {
    debug: (message: string, data?: unknown) => logger.debug(message, data, componentName),
    info: (message: string, data?: unknown) => logger.info(message, data, componentName),
    warn: (message: string, data?: unknown) => logger.warn(message, data, componentName),
    error: (message: string, error?: unknown) => logger.error(message, error, componentName),
  };
}
