/**
 * Execution Panel Component
 *
 * Displays workflow execution results, logs, and status.
 * Provides real-time execution monitoring and historical results.
 */

import { useState, useEffect } from 'react';
import {
  PlayCircle,
  XCircle,
  CheckCircle,
  Clock,
  AlertCircle,
  ChevronDown,
  ChevronRight,
  Terminal,
  Activity,
  X,
} from 'lucide-react';
import { getExecutionStatus } from '@/api/workflows';
import type { WorkflowExecutionResponse } from '@/api/types';

// ============================================================================
// Types
// ============================================================================

interface ExecutionPanelProps {
  executionId: string | null;
  onClose?: () => void;
}

// ============================================================================
// Execution Panel Component
// ============================================================================

export function ExecutionPanel({ executionId, onClose }: ExecutionPanelProps) {
  const [execution, setExecution] = useState<WorkflowExecutionResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [expandedLogs, setExpandedLogs] = useState(true);

  // Fetch execution status
  useEffect(() => {
    if (!executionId) {
      setExecution(null);
      return;
    }

    const fetchExecution = async () => {
      setIsLoading(true);
      try {
        const response = await getExecutionStatus(executionId);
        if (response.success && response.data) {
          setExecution(response.data);
        }
      } catch (error) {
        console.error('Failed to fetch execution status:', error);
      } finally {
        setIsLoading(false);
      }
    };

    fetchExecution();

    // Poll for updates if execution is running
    const interval = setInterval(() => {
      if (execution?.status === 'running' || execution?.status === 'pending') {
        fetchExecution();
      }
    }, 2000); // Poll every 2 seconds

    return () => clearInterval(interval);
  }, [executionId, execution?.status]);

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
      <div className="h-64 bg-white border-t border-gray-200 flex items-center justify-center">
        <div className="text-center text-gray-500">
          <Activity className="w-8 h-8 mx-auto mb-2 animate-pulse" />
          <p className="text-sm">Loading execution...</p>
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

  // Status icon and color
  const getStatusIcon = () => {
    switch (execution.status) {
      case 'running':
        return <PlayCircle className="w-5 h-5 text-blue-500 animate-pulse" />;
      case 'completed':
        return <CheckCircle className="w-5 h-5 text-green-500" />;
      case 'failed':
        return <XCircle className="w-5 h-5 text-red-500" />;
      case 'cancelled':
        return <XCircle className="w-5 h-5 text-orange-500" />;
      case 'pending':
      default:
        return <Clock className="w-5 h-5 text-gray-500" />;
    }
  };

  const getStatusColor = () => {
    switch (execution.status) {
      case 'running':
        return 'bg-blue-100 text-blue-700';
      case 'completed':
        return 'bg-green-100 text-green-700';
      case 'failed':
        return 'bg-red-100 text-red-700';
      case 'cancelled':
        return 'bg-orange-100 text-orange-700';
      case 'pending':
      default:
        return 'bg-gray-100 text-gray-700';
    }
  };

  // Calculate duration
  const getDuration = () => {
    if (!execution.startedAt) return 'N/A';

    const start = new Date(execution.startedAt).getTime();
    const end = execution.completedAt
      ? new Date(execution.completedAt).getTime()
      : Date.now();

    const duration = end - start;
    const seconds = Math.floor(duration / 1000);
    const minutes = Math.floor(seconds / 60);

    if (minutes > 0) {
      return `${minutes}m ${seconds % 60}s`;
    }
    return `${seconds}s`;
  };

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

          <div className="flex items-center gap-4 text-xs text-gray-600">
            <div className="flex items-center gap-1">
              <Clock className="w-3 h-3" />
              <span>Duration: {getDuration()}</span>
            </div>
            {execution.completedAt && (
              <div>
                Completed: {new Date(execution.completedAt).toLocaleTimeString()}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto">
        {/* Error Section */}
        {execution.error && (
          <div className="p-4 bg-red-50 border-b border-red-100">
            <div className="flex items-start gap-2">
              <AlertCircle className="w-5 h-5 text-red-500 flex-shrink-0 mt-0.5" />
              <div className="flex-1">
                <h4 className="text-sm font-semibold text-red-900 mb-1">Error</h4>
                <p className="text-sm text-red-700 mb-2">{execution.error.message}</p>
                {execution.error.nodeId && (
                  <p className="text-xs text-red-600">Node: {execution.error.nodeId}</p>
                )}
                {execution.error.stack && (
                  <details className="mt-2">
                    <summary className="text-xs text-red-600 cursor-pointer hover:underline">
                      Stack Trace
                    </summary>
                    <pre className="mt-2 text-xs text-red-800 bg-red-100 p-2 rounded overflow-x-auto">
                      {execution.error.stack}
                    </pre>
                  </details>
                )}
              </div>
            </div>
          </div>
        )}

        {/* Output Section */}
        {execution.output && Object.keys(execution.output).length > 0 && (
          <div className="p-4 border-b border-gray-200">
            <h4 className="text-sm font-semibold text-gray-900 mb-2">Output</h4>
            <pre className="text-xs text-gray-700 bg-gray-50 p-3 rounded overflow-x-auto">
              {JSON.stringify(execution.output, null, 2)}
            </pre>
          </div>
        )}

        {/* Logs Section */}
        {execution.logs && execution.logs.length > 0 && (
          <div className="border-b border-gray-200">
            <button
              onClick={() => setExpandedLogs(!expandedLogs)}
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
                      log.level === 'error'
                        ? 'bg-red-50 text-red-700'
                        : log.level === 'warn'
                        ? 'bg-yellow-50 text-yellow-700'
                        : log.level === 'debug'
                        ? 'bg-gray-50 text-gray-600'
                        : 'bg-blue-50 text-blue-700'
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
        {!execution.error &&
         (!execution.output || Object.keys(execution.output).length === 0) &&
         (!execution.logs || execution.logs.length === 0) && (
          <div className="p-8 text-center text-gray-500">
            <Terminal className="w-12 h-12 mx-auto mb-2 text-gray-400" />
            <p className="text-sm">No execution data available</p>
          </div>
        )}
      </div>
    </div>
  );
}
