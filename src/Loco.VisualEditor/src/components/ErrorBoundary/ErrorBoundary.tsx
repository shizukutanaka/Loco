/**
 * Error Boundary Component
 *
 * Catches and handles errors from child components with retry capability.
 * Provides user-friendly error UI with recovery options.
 */

import React, { ReactNode } from 'react';
import { AlertCircle, RotateCcw, X } from 'lucide-react';

// ============================================================================
// Types
// ============================================================================

interface ErrorBoundaryProps {
  children: ReactNode;
  onError?: (error: Error, errorInfo: ErrorInfo) => void;
  fallback?: (error: Error, retry: () => void) => ReactNode;
}

interface ErrorInfo {
  componentStack: string;
}

interface ErrorState {
  hasError: boolean;
  error: Error | null;
  retryCount: number;
}

// ============================================================================
// Error Boundary Component
// ============================================================================

export class ErrorBoundary extends React.Component<
  ErrorBoundaryProps,
  ErrorState
> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = {
      hasError: false,
      error: null,
      retryCount: 0,
    };
  }

  static getDerivedStateFromError(error: Error): Partial<ErrorState> {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    this.props.onError?.(error, errorInfo);
    console.error('Error caught by boundary:', error, errorInfo);
  }

  handleRetry = (): void => {
    this.setState((prevState) => ({
      hasError: false,
      error: null,
      retryCount: prevState.retryCount + 1,
    }));
  };

  render(): ReactNode {
    if (this.state.hasError && this.state.error) {
      if (this.props.fallback) {
        return this.props.fallback(this.state.error, this.handleRetry);
      }

      return (
        <div className="w-full h-full flex items-center justify-center p-6">
          <div className="max-w-md w-full bg-white rounded-lg shadow-lg border border-red-200 p-6">
            {/* Error Icon */}
            <div className="flex justify-center mb-4">
              <AlertCircle className="w-12 h-12 text-red-500" />
            </div>

            {/* Error Title */}
            <h2 className="text-lg font-bold text-gray-900 text-center mb-2">
              Something went wrong
            </h2>

            {/* Error Description */}
            <p className="text-sm text-gray-600 text-center mb-4">
              {this.state.error.message ||
                'An unexpected error occurred. Please try again.'}
            </p>

            {/* Error Details (Development Only) */}
            {import.meta.env.DEV && (
              <details className="mb-4 text-xs">
                <summary className="cursor-pointer text-gray-500 hover:text-gray-700 font-medium">
                  Error Details
                </summary>
                <pre className="mt-2 p-2 bg-gray-50 rounded overflow-auto text-red-600 max-h-40">
                  {this.state.error.stack}
                </pre>
              </details>
            )}

            {/* Retry Info */}
            {this.state.retryCount > 0 && (
              <div className="mb-4 p-3 bg-yellow-50 rounded border border-yellow-200">
                <p className="text-xs text-yellow-800">
                  <span className="font-medium">Retry attempt {this.state.retryCount}:</span>{' '}
                  If the error persists, please refresh the page or contact support.
                </p>
              </div>
            )}

            {/* Actions */}
            <div className="flex gap-2">
              <button
                onClick={this.handleRetry}
                className="flex-1 flex items-center justify-center gap-2 px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 transition-colors font-medium text-sm"
                aria-label="Retry the failed operation"
              >
                <RotateCcw className="w-4 h-4" />
                Try Again
              </button>
              <button
                onClick={() =>
                  this.setState({ hasError: false, error: null })
                }
                className="px-3 py-2 text-gray-600 hover:bg-gray-100 rounded-lg transition-colors"
                title="Dismiss error"
                aria-label="Dismiss this error"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {/* Support Link */}
            <p className="text-xs text-gray-500 text-center mt-4">
              Still having trouble?{' '}
              <a href="#" className="text-blue-600 hover:underline">
                Contact support
              </a>
            </p>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
