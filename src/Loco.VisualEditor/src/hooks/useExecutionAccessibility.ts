import { useEffect } from 'react';
import type { WorkflowExecutionResponse } from '@/api/types';
import { useLiveRegion } from '@/utils/ariaLiveRegion';
import { getExecutionError, getExecutionCompletionTime } from '@/utils/typeGuards';

/**
 * Custom hook for managing execution accessibility announcements
 * Handles: screen reader announcements for status changes
 */
export function useExecutionAccessibility(execution: WorkflowExecutionResponse | null) {
  const { announce: announceStatus } = useLiveRegion('execution-status', 'assertive');

  // Announce status changes to screen readers
  useEffect(() => {
    if (!execution) return;

    const getDuration = (): string => {
      if (!execution.startedAt) return 'N/A';
      const start = new Date(execution.startedAt).getTime();
      const completionTime = getExecutionCompletionTime(execution);
      const end = completionTime ? new Date(completionTime).getTime() : Date.now();
      const duration = end - start;
      const seconds = Math.floor(duration / 1000);
      const minutes = Math.floor(seconds / 60);
      if (minutes > 0) {
        return `${minutes}m ${seconds % 60}s`;
      }
      return `${seconds}s`;
    };

    const statusMessages = {
      pending: 'Workflow execution is pending',
      running: 'Workflow execution is running',
      completed: `Workflow execution completed successfully. Duration: ${getDuration()}`,
      failed: `Workflow execution failed: ${getExecutionError(execution)?.message || 'Unknown error'}`,
      cancelled: 'Workflow execution was cancelled',
    };

    announceStatus(statusMessages[execution.status]);
  }, [execution?.status]); // eslint-disable-line react-hooks/exhaustive-deps
}
