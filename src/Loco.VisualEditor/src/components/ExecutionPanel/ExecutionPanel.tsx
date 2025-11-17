/**
 * Execution Panel Component
 *
 * Displays workflow execution results, logs, and status.
 * Provides real-time execution monitoring and historical results.
 */

import { useState, useCallback, memo } from 'react';
import {
  AlertCircle,
  ChevronDown,
  ChevronRight,
  Terminal,
  Activity,
  X,
  XCircle,
  Clock,
} from 'lucide-react';
import {
  getExecutionCompletionTime,
  getExecutionOutput,
  getExecutionError,
} from '@/utils/typeGuards';
import { Skeleton } from '@/components/Skeleton/Skeleton';
import {
  useExecutionPolling,
  useExecutionStatusHelpers,
  useExecutionAccessibility,
} from '@/hooks';

// ============================================================================
// Types
// ============================================================================

interface ExecutionPanelProps {
  executionId: string | null;
  onClose?: () => void;
}

// ============================================================================
// Constants (Memoized - prevent recreation on every render)
// ============================================================================

const LOG_LEVEL_STYLES = {
  error: 'bg-red-50 text-red-700',
  warn: 'bg-yellow-50 text-yellow-700',
  debug: 'bg-gray-50 text-gray-600',
  info: 'bg-blue-50 text-blue-700',
} as const;

// ============================================================================
// Execution Panel Component
// ============================================================================

function ExecutionPanelComponent({ executionId, onClose }: ExecutionPanelProps) {
  const [expandedLogs, setExpandedLogs] = useState(true);

  // Polling and data fetching
  const { execution, isLoading } = useExecutionPolling(executionId);

  // Status display helpers
  const { getStatusIcon, getStatusColor, getDuration } = useExecutionStatusHelpers(execution);

  // Accessibility announcements
  useExecutionAccessibility(execution);

  // Memoize toggle handler
  const toggleLogs = useCallback(() => {
    setExpandedLogs((prev) => !prev);
  }, []);

  if (!executionId) {
    return (
      <div className="h-64 bg-white border-t border-gray-200 flex items-center justify-center">
        <div className="text-center text-gray-500">
          <Terminal className="w-12 h-12 mx-auto mb-2 text-gray-400" />
          <p className="text-sm">No execution selected</p>
          <p className="text-xs mt-1">Run a workflow to see execution results</p>
        </div>
      </div>
    );
  }

  if (isLoading && !execution) {
    return (
      <div className="h-96 bg-white border-t border-gray-200 flex flex-col">
        {/* Header Skeleton */}
        <div className="px-4 py-3 border-b border-gray-200 flex items-center justify-between">
          <div className="flex items-center gap-3 flex-1">
            <Skeleton width="20px" height="20px" borderRadius="50%" />
            <div className="flex-1 space-y-1">
              <Skeleton width="150px" height="16px" />
              <Skeleton width="200px" height="12px" />
            </div>
          </div>
          <Skeleton width="32px" height="32px" borderRadius="6px" />
        </div>

        {/* Status Bar Skeleton */}
        <div className="px-4 py-3 bg-gray-50 border-b border-gray-200">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Skeleton width="20px" height="20px" borderRadius="50%" />
              <Skeleton width="100px" height="20px" borderRadius="4px" />
            </div>
            <div className="flex items-center gap-4">
              <Skeleton width="150px" height="16px" />
              <Skeleton width="150px" height="16px" />
            </div>
          </div>
        </div>

        {/* Content Skeleton */}
        <div className="flex-1 overflow-y-auto p-4 space-y-4">
          <div className="space-y-2">
            <Skeleton width="80px" height="14px" />
            <Skeleton height="60px" borderRadius="4px" />
          </div>
          <div className="space-y-2">
            <Skeleton width="80px" height="14px" />
            <Skeleton height="100px" borderRadius="4px" />
          </div>
        </div>
      </div>
    );
  }

  if (!execution) {
    return (
      <div className="h-64 bg-white border-t border-gray-200 flex items-center justify-center">
        <div className="text-center text-gray-500">
          <XCircle className="w-12 h-12 mx-auto mb-2 text-red-400" />
          <p className="text-sm">Failed to load execution</p>
        </div>
      </div>
    );
  }

  return (
    <div className="h-96 bg-white border-t border-gray-200 flex flex-col">
      {/* Header */}
      <div className="px-4 py-3 border-b border-gray-200 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Activity className="w-5 h-5 text-gray-600" />
          <div>
            <h3 className="text-sm font-semibold text-gray-900">Execution Results</h3>
            <p className="text-xs text-gray-500">ID: {execution.executionId}</p>
          </div>
        </div>

        {onClose && (
          <button
            onClick={onClose}
            className="p-1 hover:bg-gray-100 rounded transition-colors"
            title="Close"
          >
            <X className="w-4 h-4 text-gray-500" />
          </button>
        )}
      </div>

      {/* Status Bar */}
      <div className="px-4 py-3 bg-gray-50 border-b border-gray-200">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            {getStatusIcon()}
            <span className={`px-2 py-1 rounded text-xs font-medium ${getStatusColor()}`}>
              {execution.status.toUpperCase()}
            </span>
          </div>

          <div className="flex items-center gap-4">

            <div className="flex items-center gap-4 text-xs text-gray-600">
              <div className="flex items-center gap-1">
                <Clock className="w-3 h-3" />
                <span>Duration: {getDuration()}</span>
              </div>
              {(() => {
                const completionTime = getExecutionCompletionTime(execution);
                if (completionTime) {
                  return (
                    <div>
                      Completed: {new Date(completionTime).toLocaleTimeString()}
                    </div>
                  );
                }
              })()}
            </div>
          </div>
        </div>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto">
        {/* Error Section */}
        {(() => {
          const error = getExecutionError(execution);
          if (error) {
            return (
              <div className="p-4 bg-red-50 border-b border-red-100">
                <div className="flex items-start gap-2">
                  <AlertCircle className="w-5 h-5 text-red-500 flex-shrink-0 mt-0.5" />
                  <div className="flex-1">
                    <h4 className="text-sm font-semibold text-red-900 mb-1">Error</h4>
                    <p className="text-sm text-red-700 mb-2">{error.message}</p>
                    {error.nodeId && (
                      <p className="text-xs text-red-600">Node: {error.nodeId}</p>
                    )}
                    {error.stack && (
                      <details className="mt-2">
                        <summary className="text-xs text-red-600 cursor-pointer hover:underline">
                          Stack Trace
                        </summary>
                        <pre className="mt-2 text-xs text-red-800 bg-red-100 p-2 rounded overflow-x-auto">
                          {error.stack}
                        </pre>
                      </details>
                    )}
                  </div>
                </div>
              </div>
            );
          }
        })()}

        {/* Output Section */}
        {(() => {
          const output = getExecutionOutput(execution);
          if (output && Object.keys(output).length > 0) {
            return (
              <div className="p-4 border-b border-gray-200">
                <h4 className="text-sm font-semibold text-gray-900 mb-2">Output</h4>
                <pre className="text-xs text-gray-700 bg-gray-50 p-3 rounded overflow-x-auto">
                  {JSON.stringify(output, null, 2)}
                </pre>
              </div>
            );
          }
        })()}

        {/* Logs Section */}
        {execution.logs && execution.logs.length > 0 && (
          <div className="border-b border-gray-200">
            <button
              onClick={toggleLogs}
              className="w-full px-4 py-3 flex items-center justify-between hover:bg-gray-50 transition-colors"
            >
              <div className="flex items-center gap-2">
                {expandedLogs ? (
                  <ChevronDown className="w-4 h-4 text-gray-500" />
                ) : (
                  <ChevronRight className="w-4 h-4 text-gray-500" />
                )}
                <Terminal className="w-4 h-4 text-gray-600" />
                <h4 className="text-sm font-semibold text-gray-900">
                  Execution Logs ({execution.logs.length})
                </h4>
              </div>
            </button>

            {expandedLogs && (
              <div className="px-4 pb-4 space-y-2">
                {execution.logs.map((log, index) => (
                  <div
                    key={index}
                    className={`text-xs p-2 rounded ${
                      LOG_LEVEL_STYLES[log.level as keyof typeof LOG_LEVEL_STYLES] || LOG_LEVEL_STYLES.info
                    }`}
                  >
                    <div className="flex items-start gap-2">
                      <span className="font-mono text-[10px] text-gray-500 flex-shrink-0">
                        {new Date(log.timestamp).toLocaleTimeString()}
                      </span>
                      <span className="font-semibold uppercase flex-shrink-0">
                        {log.level}
                      </span>
                      {log.nodeId && (
                        <span className="text-gray-600 flex-shrink-0">
                          [{log.nodeId}]
                        </span>
                      )}
                      <span className="flex-1">{log.message}</span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Empty State */}
        {(() => {
          const hasError = getExecutionError(execution);
          const hasOutput = getExecutionOutput(execution);
          const hasLogs = execution.logs && execution.logs.length > 0;

          if (!hasError && (!hasOutput || Object.keys(hasOutput).length === 0) && !hasLogs) {
            return (
              <div className="p-8 text-center text-gray-500">
                <Terminal className="w-12 h-12 mx-auto mb-2 text-gray-400" />
                <p className="text-sm">No execution data available</p>
              </div>
            );
          }
        })()}
      </div>

    </div>
  );
}

export const ExecutionPanel = memo(ExecutionPanelComponent);
ExecutionPanel.displayName = 'ExecutionPanel';
