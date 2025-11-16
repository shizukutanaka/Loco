/**
 * Error Boundary Component
 *
 * Catches React errors and displays a fallback UI instead of crashing the app.
 * Provides error details, recovery options, and error logging.
 */

import { Component, ReactNode, ErrorInfo } from 'react';
import { AlertTriangle, RefreshCw, Home, Copy, Check } from 'lucide-react';
import { logCriticalError } from '@/utils/errorLogger';

// ============================================================================
// Types
// ============================================================================

interface ErrorBoundaryProps {
  children: ReactNode;
  fallback?: ReactNode;
  onError?: (error: Error, errorInfo: ErrorInfo) => void;
  onReset?: () => void;
}

interface ErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
  errorInfo: ErrorInfo | null;
  errorId: string | null;
  copied: boolean;
}

// ============================================================================
// Error Boundary Class Component
// ============================================================================

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = {
      hasError: false,
      error: null,
      errorInfo: null,
      errorId: null,
      copied: false,
    };
  }

  static getDerivedStateFromError(error: Error): Partial<ErrorBoundaryState> {
    // Generate unique error ID
    const errorId = `err-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;

    return {
      hasError: true,
      error,
      errorId,
    };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    // Update state with error info
    this.setState({ errorInfo });

    // Log error using error logger
    logCriticalError('React component crashed', error, {
      errorId: this.state.errorId,
      componentStack: errorInfo.componentStack,
    });

    // Call custom error handler if provided
    if (this.props.onError) {
      this.props.onError(error, errorInfo);
    }

    // Log to external service
    this.logErrorToService(error, errorInfo);
  }

  logErrorToService(error: Error, errorInfo: ErrorInfo): void {
    const errorData = {
      errorId: this.state.errorId,
      message: error.message,
      stack: error.stack,
      componentStack: errorInfo.componentStack,
      timestamp: new Date().toISOString(),
      userAgent: navigator.userAgent,
      url: window.location.href,
    };

    // Log to console for development/debugging
    console.error('Application Error:', errorData);

    // Store in localStorage for error recovery/debugging
    try {
      const errorLog = JSON.parse(localStorage.getItem('app_errors') || '[]');
      errorLog.push(errorData);
      // Keep last 20 errors
      localStorage.setItem('app_errors', JSON.stringify(errorLog.slice(-20)));
    } catch (storageError) {
      console.error('Failed to store error log:', storageError);
    }

    // TODO: Send to backend error logging service when API endpoint is available
    // const response = await fetch('/api/v1/errors', {
    //   method: 'POST',
    //   headers: { 'Content-Type': 'application/json' },
    //   body: JSON.stringify(errorData),
    // });
  }

  handleReset = (): void => {
    this.setState({
      hasError: false,
      error: null,
      errorInfo: null,
      errorId: null,
      copied: false,
    });

    // Call custom reset handler if provided
    if (this.props.onReset) {
      this.props.onReset();
    }
  };

  handleReload = (): void => {
    window.location.reload();
  };

  handleGoHome = (): void => {
    window.location.href = '/';
  };

  handleCopyError = (): void => {
    const { error, errorInfo, errorId } = this.state;

    const errorText = `
Error ID: ${errorId}
Error: ${error?.message}
Stack: ${error?.stack}
Component Stack: ${errorInfo?.componentStack}
Timestamp: ${new Date().toISOString()}
URL: ${window.location.href}
User Agent: ${navigator.userAgent}
    `.trim();

    navigator.clipboard.writeText(errorText)
      .then(() => {
        this.setState({ copied: true });
        setTimeout(() => this.setState({ copied: false }), 2000);
      })
      .catch((err) => {
        console.error('Failed to copy error to clipboard:', err);
        // Fallback: show alert if clipboard fails
        if (confirm('Copy to clipboard failed. Would you like to see the error?')) {
          alert(errorText);
        }
      });
  };

  render(): ReactNode {
    if (this.state.hasError) {
      // Use custom fallback if provided
      if (this.props.fallback) {
        return this.props.fallback;
      }

      // Default fallback UI
      return (
        <div className="min-h-screen bg-gray-50 flex items-center justify-center p-6">
          <div className="max-w-2xl w-full bg-white rounded-xl shadow-lg p-8">
            {/* Error Icon */}
            <div className="flex items-center justify-center w-16 h-16 bg-red-100 rounded-full mx-auto mb-6">
              <AlertTriangle className="w-8 h-8 text-red-600" />
            </div>

            {/* Title */}
            <h1 className="text-2xl font-bold text-gray-900 text-center mb-2">
              Something went wrong
            </h1>

            {/* Error ID */}
            {this.state.errorId && (
              <p className="text-sm text-gray-500 text-center mb-6">
                Error ID: <code className="bg-gray-100 px-2 py-1 rounded">{this.state.errorId}</code>
              </p>
            )}

            {/* Description */}
            <p className="text-gray-600 text-center mb-8">
              An unexpected error occurred while rendering this component.
              Your work has been automatically saved to local storage.
            </p>

            {/* Error Details (collapsible) */}
            <details className="mb-8 bg-gray-50 rounded-lg p-4">
              <summary className="cursor-pointer font-medium text-gray-700 mb-2">
                Error Details
              </summary>
              <div className="mt-4 space-y-4">
                {/* Error Message */}
                <div>
                  <p className="text-xs font-semibold text-gray-600 mb-1">Error Message:</p>
                  <pre className="bg-white p-3 rounded border border-gray-200 text-xs text-red-600 overflow-x-auto">
                    {this.state.error?.message}
                  </pre>
                </div>

                {/* Stack Trace */}
                {this.state.error?.stack && (
                  <div>
                    <p className="text-xs font-semibold text-gray-600 mb-1">Stack Trace:</p>
                    <pre className="bg-white p-3 rounded border border-gray-200 text-xs text-gray-700 overflow-x-auto max-h-48 overflow-y-auto">
                      {this.state.error.stack}
                    </pre>
                  </div>
                )}

                {/* Component Stack */}
                {this.state.errorInfo?.componentStack && (
                  <div>
                    <p className="text-xs font-semibold text-gray-600 mb-1">Component Stack:</p>
                    <pre className="bg-white p-3 rounded border border-gray-200 text-xs text-gray-700 overflow-x-auto max-h-48 overflow-y-auto">
                      {this.state.errorInfo.componentStack}
                    </pre>
                  </div>
                )}
              </div>
            </details>

            {/* Action Buttons */}
            <div className="flex flex-col sm:flex-row gap-3 mb-4">
              <button
                onClick={this.handleReset}
                className="flex-1 flex items-center justify-center gap-2 px-6 py-3 bg-loco-primary text-white rounded-lg hover:bg-blue-700 transition-colors"
              >
                <RefreshCw className="w-4 h-4" />
                Try Again
              </button>

              <button
                onClick={this.handleReload}
                className="flex-1 flex items-center justify-center gap-2 px-6 py-3 bg-gray-600 text-white rounded-lg hover:bg-gray-700 transition-colors"
              >
                <RefreshCw className="w-4 h-4" />
                Reload Page
              </button>

              <button
                onClick={this.handleGoHome}
                className="flex-1 flex items-center justify-center gap-2 px-6 py-3 bg-gray-200 text-gray-700 rounded-lg hover:bg-gray-300 transition-colors"
              >
                <Home className="w-4 h-4" />
                Go Home
              </button>
            </div>

            {/* Copy Error Button */}
            <button
              onClick={this.handleCopyError}
              className="w-full flex items-center justify-center gap-2 px-6 py-2 bg-gray-100 text-gray-700 rounded-lg hover:bg-gray-200 transition-colors text-sm"
            >
              {this.state.copied ? (
                <>
                  <Check className="w-4 h-4 text-green-600" />
                  Copied to clipboard!
                </>
              ) : (
                <>
                  <Copy className="w-4 h-4" />
                  Copy Error Details
                </>
              )}
            </button>

            {/* Help Text */}
            <p className="text-xs text-gray-500 text-center mt-6">
              If this problem persists, please contact support with the error ID above.
            </p>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
