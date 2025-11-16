import { useState } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';
import { useToast } from '@/contexts/ToastContext';
import { useLiveRegion } from '@/utils/ariaLiveRegion';
import { createWorkflow, updateWorkflow, workflowToCreateRequest } from '@/api/workflows';
import type { Workflow } from '@/types/workflow';
import { exportWorkflowAsJson } from '@/utils/exportWorkflow';
import { isApiSuccess, isApiError } from '@/utils/typeGuards';

interface WorkflowOperationsState {
  isSaving: boolean;
}

interface WorkflowOperationsActions {
  handleNewWorkflow: () => void;
  handleExportJSON: () => void;
  handleImportJSON: () => void;
  handleSaveWorkflow: () => Promise<void>;
}

/**
 * Custom hook for managing workflow operations: create, save, import, export
 * Handles API calls, toast notifications, and accessibility announcements
 */
export function useWorkflowOperations(): WorkflowOperationsState & WorkflowOperationsActions {
  const { workflow, newWorkflow, loadWorkflow, exportWorkflow } = useWorkflowStore();
  const toast = useToast();
  const [isSaving, setIsSaving] = useState(false);

  // Live regions for accessibility announcements
  const { announce: announceSave } = useLiveRegion('toolbar-save-status', 'assertive');
  const { announce: announceImport } = useLiveRegion('toolbar-import-status', 'polite');
  const { announce: announceExport } = useLiveRegion('toolbar-export-status', 'polite');

  const handleNewWorkflow = () => {
    if (confirm('Create a new workflow? Current workflow will be cleared.')) {
      newWorkflow();
    }
  };

  const handleExportJSON = () => {
    const workflowData = exportWorkflow();
    exportWorkflowAsJson(workflowData);
    announceExport(`Workflow "${workflowData.name}" exported successfully`);
    toast.success('Workflow exported successfully');
  };

  const handleImportJSON = () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json';
    input.onchange = (e) => {
      const file = (e.target as HTMLInputElement).files?.[0];
      if (file) {
        const reader = new FileReader();
        reader.onload = (event) => {
          try {
            const workflowData = JSON.parse(event.target?.result as string);

            // Validate workflow structure
            if (!workflowData.name || !Array.isArray(workflowData.nodes) || !Array.isArray(workflowData.edges)) {
              toast.error('Invalid workflow file: Missing required fields');
              return;
            }

            // Confirm import if current workflow has changes
            if (workflow && (workflow.nodes.length > 0 || workflow.edges.length > 0)) {
              const confirmed = confirm(
                `Import workflow "${workflowData.name}"? Current workflow will be replaced.`
              );
              if (!confirmed) return;
            }

            // Load the workflow
            loadWorkflow(workflowData);
            announceImport(`Workflow "${workflowData.name}" imported successfully`);
            toast.success(`Workflow "${workflowData.name}" imported successfully!`);
          } catch (error) {
            console.error('Failed to import workflow:', error);
            announceImport('Failed to import workflow: Invalid JSON format');
            toast.error('Failed to import workflow: Invalid JSON format');
          }
        };
        reader.readAsText(file);
      }
    };
    input.click();
  };

  const handleSaveWorkflow = async () => {
    setIsSaving(true);
    try {
      const workflowData = exportWorkflow();

      // Check if workflow has an ID (existing) or needs to be created (new)
      const isNewWorkflow = !workflowData.id || workflowData.id.startsWith('workflow-');

      if (isNewWorkflow) {
        // Create new workflow
        const request = workflowToCreateRequest(workflowData);
        const response = await createWorkflow(request);

        if (isApiSuccess(response)) {
          // Update workflow in store with server-generated ID
          const updatedWorkflow: Workflow = {
            ...workflowData,
            id: response.data.id,
            createdAt: response.data.createdAt,
            updatedAt: response.data.updatedAt,
          };
          loadWorkflow(updatedWorkflow);
          console.log('Workflow created successfully:', response.data.id);
          announceSave(`Workflow "${workflowData.name}" created and saved successfully`);
          toast.success(`Workflow "${workflowData.name}" created successfully!`);
        } else if (isApiError(response)) {
          console.error('Failed to create workflow:', response.error.message);
          announceSave(`Failed to save workflow: ${response.error.message || 'Unknown error'}`);
          toast.error(`Failed to save workflow: ${response.error.message || 'Unknown error'}`);
        }
      } else {
        // Update existing workflow
        const response = await updateWorkflow(workflowData.id, {
          id: workflowData.id,
          name: workflowData.name,
          description: workflowData.description,
          nodes: workflowData.nodes,
          edges: workflowData.edges,
          metadata: workflowData.metadata,
        });

        if (isApiSuccess(response)) {
          console.log('Workflow updated successfully:', response.data.id);
          announceSave(`Workflow "${workflowData.name}" saved successfully`);
          toast.success(`Workflow "${workflowData.name}" saved successfully!`);
        } else if (isApiError(response)) {
          console.error('Failed to update workflow:', response.error.message);
          announceSave(`Failed to save workflow: ${response.error.message || 'Unknown error'}`);
          toast.error(`Failed to save workflow: ${response.error.message || 'Unknown error'}`);
        }
      }
    } catch (error) {
      console.error('Error saving workflow:', error);
      announceSave('An unexpected error occurred while saving the workflow');
      toast.error('An unexpected error occurred while saving the workflow');
    } finally {
      setIsSaving(false);
    }
  };

  return {
    isSaving,
    handleNewWorkflow,
    handleExportJSON,
    handleImportJSON,
    handleSaveWorkflow,
  };
}
