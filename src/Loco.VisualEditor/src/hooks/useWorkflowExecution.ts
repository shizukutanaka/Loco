import { useState } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';
import { useExecutionStore } from '@/store/executionStore';
import { useToast } from '@/contexts/ToastContext';
import { useLiveRegion } from '@/utils/ariaLiveRegion';
import { TOAST_LONG_DURATION } from '@/utils/constants';
import { executeWorkflow } from '@/api/workflows';
import { isApiSuccess, isApiError, getExecutionCompletionTime } from '@/utils/typeGuards';

interface WorkflowExecutionState {
  isRunning: boolean;
}

interface WorkflowExecutionActions {
  handleRunWorkflow: () => Promise<void>;
}

/**
 * Custom hook for managing workflow execution
 * Handles API calls, execution history, and accessibility announcements
 */
export function useWorkflowExecution(): WorkflowExecutionState & WorkflowExecutionActions {
  const { exportWorkflow } = useWorkflowStore();
  const { setCurrentExecution, addToHistory } = useExecutionStore();
  const toast = useToast();
  const [isRunning, setIsRunning] = useState(false);

  // Live region for accessibility announcements
  const { announce: announceRun } = useLiveRegion('toolbar-run-status', 'assertive');

  const handleRunWorkflow = async () => {
    setIsRunning(true);
    try {
      const workflowData = exportWorkflow();

      // Check if workflow has a valid ID
      if (!workflowData.id || workflowData.id.startsWith('workflow-')) {
        announceRun('Please save the workflow before running it');
        toast.warning('Please save the workflow before running it');
        setIsRunning(false);
        return;
      }

      // Execute workflow
      const response = await executeWorkflow({
        workflowId: workflowData.id,
        input: {},
      });

      if (isApiSuccess(response)) {
        const executionData = response.data;
        console.log('Workflow execution started:', executionData.executionId);
        console.log('Status:', executionData.status);

        // Add to execution history
        addToHistory({
          executionId: executionData.executionId,
          workflowId: workflowData.id,
          workflowName: workflowData.name,
          status: executionData.status,
          startedAt: executionData.startedAt,
          completedAt: getExecutionCompletionTime(executionData) || undefined,
        });

        // Open execution panel
        setCurrentExecution(executionData.executionId);

        announceRun(`Workflow "${workflowData.name}" started executing, execution ID: ${executionData.executionId}`);
        toast.success(`Workflow "${workflowData.name}" is running...`);
        toast.info(`Execution ID: ${executionData.executionId}`, TOAST_LONG_DURATION);
      } else if (isApiError(response)) {
        console.error('Failed to execute workflow:', response.error.message);
        announceRun(`Failed to run workflow: ${response.error.message || 'Unknown error'}`);
        toast.error(`Failed to run workflow: ${response.error.message || 'Unknown error'}`);
      }
    } catch (error) {
      console.error('Error executing workflow:', error);
      announceRun('An unexpected error occurred while running the workflow');
      toast.error('An unexpected error occurred while running the workflow');
    } finally {
      setIsRunning(false);
    }
  };

  return {
    isRunning,
    handleRunWorkflow,
  };
}
