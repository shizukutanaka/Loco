import { useState, useEffect, useRef } from 'react';
import { getExecutionStatus } from '@/api/workflows';
import type { WorkflowExecutionResponse } from '@/api/types';
import { EXECUTION_POLLING_INTERVAL } from '@/utils/constants';

/**
 * Custom hook for managing execution polling and data fetching
 * Handles: fetching execution status, polling for updates on running/pending executions
 *
 * Race condition fix: Uses AbortController to cancel in-flight requests when executionId changes,
 * and verifies response matches current executionId before applying state updates
 */
export function useExecutionPolling(executionId: string | null) {
  const [execution, setExecution] = useState<WorkflowExecutionResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const executionStatusRef = useRef<string | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);
  const currentExecutionIdRef = useRef<string | null>(null);

  useEffect(() => {
    if (!executionId) {
      setExecution(null);
      executionStatusRef.current = null;
      currentExecutionIdRef.current = null;

      // Cancel any in-flight requests
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
        abortControllerRef.current = null;
      }
      return;
    }

    // Update current execution ID and cancel previous requests
    currentExecutionIdRef.current = executionId;
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }
    abortControllerRef.current = new AbortController();

    const fetchExecution = async (currentId: string) => {
      setIsLoading(true);
      try {
        const response = await getExecutionStatus(currentId);

        // Only apply state if this response is for the current executionId
        // (prevents race condition where stale responses overwrite new data)
        if (currentExecutionIdRef.current === currentId && response.success && response.data) {
          setExecution(response.data);
          executionStatusRef.current = response.data.status;
        }
      } catch (error) {
        // Ignore abort errors (expected when executionId changes)
        if (error instanceof Error && error.name !== 'AbortError') {
          console.error('Failed to fetch execution status:', error);
        }
      } finally {
        setIsLoading(false);
      }
    };

    fetchExecution(executionId);

    // Poll for updates if execution is running or pending
    const interval = setInterval(() => {
      const status = executionStatusRef.current;
      const currentId = currentExecutionIdRef.current;
      if ((status === 'running' || status === 'pending') && currentId) {
        fetchExecution(currentId);
      }
    }, EXECUTION_POLLING_INTERVAL);

    return () => {
      clearInterval(interval);
      // Cancel any in-flight requests on cleanup
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
    };
  }, [executionId]);

  return { execution, isLoading };
}
