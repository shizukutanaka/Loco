import { useCallback } from 'react';
import {
  listWorkflows,
  deleteWorkflow,
  getWorkflow,
  createWorkflow,
  executeWorkflow,
  workflowToCreateRequest,
} from '@/api/workflows';
import { useWorkflowStore } from '@/store/workflowStore';
import { useExecutionStore } from '@/store/executionStore';
import { useToast } from '@/contexts/ToastContext';
import { TOAST_LONG_DURATION } from '@/utils/constants';
import { isApiSuccess, isApiError, getExecutionCompletionTime } from '@/utils/typeGuards';
import { WorkflowListItem } from './useWorkflowListData';

interface UseWorkflowListActionsOptions {
  workflows: WorkflowListItem[];
  sortBy: 'name' | 'created' | 'updated';
  onUpdateWorkflows: (items: WorkflowListItem[]) => void;
  onClose: () => void;
}

/**
 * Custom hook for managing workflow list actions
 * Handles: delete, edit, duplicate, run, create new workflow
 */
export function useWorkflowListActions({
  workflows,
  sortBy,
  onUpdateWorkflows,
  onClose,
}: UseWorkflowListActionsOptions) {
  const { newWorkflow, loadWorkflow } = useWorkflowStore();
  const { setCurrentExecution, addToHistory } = useExecutionStore();
  const toast = useToast();

  // Create new workflow
  const handleNew = useCallback(() => {
    newWorkflow();
    onClose();
    toast.info('New workflow created');
  }, [newWorkflow, onClose, toast]);

  // Delete workflow
  const handleDelete = useCallback(
    async (workflowId: string) => {
      if (!confirm('Are you sure you want to delete this workflow?')) return;

      try {
        const response = await deleteWorkflow(workflowId);
        if (response.success) {
          onUpdateWorkflows(workflows.filter((w) => w.id !== workflowId));
          toast.success('Workflow deleted successfully');
        } else {
          toast.error(`Failed to delete workflow: ${response.error?.message}`);
        }
      } catch (error) {
        console.error('Failed to delete workflow:', error);
        toast.error('An error occurred while deleting the workflow');
      }
    },
    [workflows, onUpdateWorkflows, toast]
  );

  // Load workflow for editing
  const handleEdit = useCallback(
    async (workflowId: string) => {
      try {
        const response = await getWorkflow(workflowId);
        if (isApiSuccess(response)) {
          loadWorkflow(response.data);
          onClose();
          toast.success(`Workflow "${response.data.name}" loaded successfully!`);
        } else if (isApiError(response)) {
          toast.error(`Failed to load workflow: ${response.error.message}`);
        }
      } catch (error) {
        console.error('Failed to load workflow:', error);
        toast.error('An error occurred while loading the workflow');
      }
    },
    [loadWorkflow, onClose, toast]
  );

  // Duplicate workflow
  const handleDuplicate = useCallback(
    async (workflowId: string) => {
      try {
        const response = await getWorkflow(workflowId);
        if (isApiSuccess(response)) {
          const workflow = response.data;

          // Create a copy with modified name and new ID
          const duplicatedWorkflow = {
            ...workflow,
            id: crypto.randomUUID(), // Generate new ID
            name: `${workflow.name} (Copy)`,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
          };

          // Create the new workflow
          const createResponse = await createWorkflow(workflowToCreateRequest(duplicatedWorkflow));

          if (isApiSuccess(createResponse)) {
            toast.success(`Workflow "${duplicatedWorkflow.name}" created successfully!`);

            // Refresh workflow list
            const listResponse = await listWorkflows({ sortBy, sortOrder: 'desc' });
            if (isApiSuccess(listResponse)) {
              const items: WorkflowListItem[] = listResponse.data.workflows.map((w) => ({
                id: w.id,
                name: w.name,
                description: w.description,
                nodeCount: w.nodes.length,
                edgeCount: w.edges.length,
                createdAt: w.createdAt,
                updatedAt: w.updatedAt,
              }));
              onUpdateWorkflows(items);
            }
          } else if (isApiError(createResponse)) {
            toast.error(`Failed to duplicate workflow: ${createResponse.error.message}`);
          }
        } else if (isApiError(response)) {
          toast.error(`Failed to load workflow: ${response.error.message}`);
        }
      } catch (error) {
        console.error('Failed to duplicate workflow:', error);
        toast.error('An error occurred while duplicating the workflow');
      }
    },
    [sortBy, onUpdateWorkflows, toast]
  );

  // Run workflow
  const handleRun = useCallback(
    async (workflowId: string, workflowName: string) => {
      try {
        const response = await executeWorkflow({
          workflowId,
          input: {},
        });

        if (isApiSuccess(response)) {
          const executionData = response.data;
          // Add to execution history
          addToHistory({
            executionId: executionData.executionId,
            workflowId,
            workflowName,
            status: executionData.status,
            startedAt: executionData.startedAt,
            completedAt: getExecutionCompletionTime(executionData) || undefined,
          });

          // Open execution panel
          setCurrentExecution(executionData.executionId);
          onClose();

          toast.success(`Workflow "${workflowName}" is running...`);
          toast.info(`Execution ID: ${executionData.executionId}`, TOAST_LONG_DURATION);
        } else if (isApiError(response)) {
          toast.error(`Failed to run workflow: ${response.error.message}`);
        }
      } catch (error) {
        console.error('Failed to run workflow:', error);
        toast.error('An error occurred while running the workflow');
      }
    },
    [addToHistory, setCurrentExecution, onClose, toast]
  );

  return {
    handleNew,
    handleDelete,
    handleEdit,
    handleDuplicate,
    handleRun,
  };
}
