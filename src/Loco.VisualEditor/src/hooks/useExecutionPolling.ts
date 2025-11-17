import { useState, useEffect, useRef } from 'react';
import { getExecutionStatus } from '@/api/workflows';
import type { WorkflowExecutionResponse } from '@/api/types';
import { EXECUTION_POLLING_INTERVAL } from '@/utils/constants';

/**
 * Custom hook for managing execution polling and data fetching
 * Handles: fetching execution status, polling for updates on running/pending executions
 */
export function useExecutionPolling(executionId: string | null) {
  const [execution, setExecution] = useState<WorkflowExecutionResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const executionStatusRef = useRef<string | null>(null);

  useEffect(() => {
    if (!executionId) {
      setExecution(null);
      executionStatusRef.current = null;
      return;
    }

    const fetchExecution = async () => {
      setIsLoading(true);
      try {
        const response = await getExecutionStatus(executionId);
        if (response.success && response.data) {
          setExecution(response.data);
          executionStatusRef.current = response.data.status;
        }
      } catch (error) {
        console.error('Failed to fetch execution status:', error);
      } finally {
        setIsLoading(false);
      }
    };

    fetchExecution();

    // Poll for updates if execution is running or pending
    const interval = setInterval(() => {
      const status = executionStatusRef.current;
      if (status === 'running' || status === 'pending') {
        fetchExecution();
      }
    }, EXECUTION_POLLING_INTERVAL);

    return () => clearInterval(interval);
  }, [executionId]);

  return { execution, isLoading };
}
