import { useState, useEffect, lazy, Suspense } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';
import { useToast } from '@/contexts/ToastContext';
import {
  FolderOpen,
  Save,
  Download,
  Play,
  Plus,
  Settings,
  LayoutTemplate,
  Loader2,
} from 'lucide-react';
import { createWorkflow, updateWorkflow, executeWorkflow, workflowToCreateRequest } from '@/api/workflows';

// Lazy load TemplateGallery (large component with template data)
const TemplateGallery = lazy(() => import('@/components/TemplateGallery/TemplateGallery').then(module => ({
  default: module.TemplateGallery
})));

export function Toolbar() {
  const { workflow, newWorkflow, exportWorkflow, updateWorkflowMetadata } =
    useWorkflowStore();
  const toast = useToast();
  const [isEditingName, setIsEditingName] = useState(false);
  const [workflowName, setWorkflowName] = useState(workflow?.name || 'New Workflow');
  const [isTemplateGalleryOpen, setIsTemplateGalleryOpen] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isRunning, setIsRunning] = useState(false);

  // Listen for keyboard shortcut events
  useEffect(() => {
    const handleSaveEvent = () => {
      handleSaveWorkflow();
    };

    window.addEventListener('workflow:save', handleSaveEvent);
    return () => window.removeEventListener('workflow:save', handleSaveEvent);
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const handleNewWorkflow = () => {
    if (confirm('Create a new workflow? Current workflow will be cleared.')) {
      newWorkflow();
      setWorkflowName('New Workflow');
    }
  };

  const handleExportJSON = () => {
    const workflowData = exportWorkflow();
    const jsonString = JSON.stringify(workflowData, null, 2);
    const blob = new Blob([jsonString], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${workflow?.name || 'workflow'}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
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
            JSON.parse(event.target?.result as string);
            // TODO: Implement loadWorkflow when backend API is ready
            alert('Workflow imported successfully!');
          } catch (_error) {
            alert('Failed to import workflow: Invalid JSON');
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

        if (response.success && response.data) {
          console.log('Workflow created successfully:', response.data.id);
          toast.success(`Workflow "${workflowData.name}" created successfully!`);
          // TODO: Update workflow ID in store with the server-generated ID
        } else {
          console.error('Failed to create workflow:', response.error?.message);
          toast.error(`Failed to save workflow: ${response.error?.message || 'Unknown error'}`);
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

        if (response.success && response.data) {
          console.log('Workflow updated successfully:', response.data.id);
          toast.success(`Workflow "${workflowData.name}" saved successfully!`);
        } else {
          console.error('Failed to update workflow:', response.error?.message);
          toast.error(`Failed to save workflow: ${response.error?.message || 'Unknown error'}`);
        }
      }
    } catch (error) {
      console.error('Error saving workflow:', error);
      toast.error('An unexpected error occurred while saving the workflow');
    } finally {
      setIsSaving(false);
    }
  };

  const handleRunWorkflow = async () => {
    setIsRunning(true);
    try {
      const workflowData = exportWorkflow();

      // Check if workflow has a valid ID
      if (!workflowData.id || workflowData.id.startsWith('workflow-')) {
        toast.warning('Please save the workflow before running it');
        setIsRunning(false);
        return;
      }

      // Execute workflow
      const response = await executeWorkflow({
        workflowId: workflowData.id,
        input: {},
      });

      if (response.success && response.data) {
        console.log('Workflow execution started:', response.data.executionId);
        console.log('Status:', response.data.status);
        toast.success(`Workflow "${workflowData.name}" is running...`);
        toast.info(`Execution ID: ${response.data.executionId}`, 7000);
        // TODO: Show execution status in UI
        // TODO: Poll for execution status
      } else {
        console.error('Failed to execute workflow:', response.error?.message);
        toast.error(`Failed to run workflow: ${response.error?.message || 'Unknown error'}`);
      }
    } catch (error) {
      console.error('Error executing workflow:', error);
      toast.error('An unexpected error occurred while running the workflow');
    } finally {
      setIsRunning(false);
    }
  };

  const handleNameBlur = () => {
    setIsEditingName(false);
    if (workflowName.trim()) {
      updateWorkflowMetadata({ name: workflowName });
    } else {
      setWorkflowName(workflow?.name || 'New Workflow');
    }
  };

  return (
    <>
      <div className="h-16 bg-white border-b border-gray-200 flex items-center justify-between px-6">
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-2">
            <div className="w-10 h-10 bg-gradient-to-br from-loco-primary to-loco-secondary rounded-lg flex items-center justify-center text-white font-bold text-lg">
              L
            </div>
            <span className="text-xl font-bold text-gray-900">Loco</span>
          </div>

          <div className="h-8 w-px bg-gray-300"></div>

          {isEditingName ? (
            <input
              type="text"
              value={workflowName}
              onChange={(e) => setWorkflowName(e.target.value)}
              onBlur={handleNameBlur}
              onKeyDown={(e) => e.key === 'Enter' && handleNameBlur()}
              className="px-3 py-1 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
              autoFocus
            />
          ) : (
            <button
              onClick={() => setIsEditingName(true)}
              className="text-lg font-medium text-gray-900 hover:text-loco-primary transition-colors"
            >
              {workflowName}
            </button>
          )}
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={handleNewWorkflow}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="New Workflow"
          >
            <Plus className="w-4 h-4" />
            <span className="text-sm font-medium">New</span>
          </button>

          <button
            onClick={() => setIsTemplateGalleryOpen(true)}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Templates"
          >
            <LayoutTemplate className="w-4 h-4" />
            <span className="text-sm font-medium">Templates</span>
          </button>

          <button
            onClick={handleImportJSON}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Import JSON"
          >
            <FolderOpen className="w-4 h-4" />
            <span className="text-sm font-medium">Import</span>
          </button>

          <button
            onClick={handleExportJSON}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Export JSON"
          >
            <Download className="w-4 h-4" />
            <span className="text-sm font-medium">Export</span>
          </button>

          <button
            onClick={handleSaveWorkflow}
            disabled={isSaving}
            className="flex items-center gap-2 px-4 py-2 text-white bg-loco-primary hover:bg-blue-700 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            title="Save Workflow"
          >
            {isSaving ? (
              <Loader2 className="w-4 h-4 animate-spin" />
            ) : (
              <Save className="w-4 h-4" />
            )}
            <span className="text-sm font-medium">{isSaving ? 'Saving...' : 'Save'}</span>
          </button>

          <div className="h-8 w-px bg-gray-300 mx-2"></div>

          <button
            onClick={handleRunWorkflow}
            disabled={isRunning}
            className="flex items-center gap-2 px-4 py-2 text-white bg-loco-success hover:bg-green-700 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            title="Run Workflow"
          >
            {isRunning ? (
              <Loader2 className="w-4 h-4 animate-spin" />
            ) : (
              <Play className="w-4 h-4" />
            )}
            <span className="text-sm font-medium">{isRunning ? 'Running...' : 'Run'}</span>
          </button>

          <button
            className="p-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Settings"
          >
            <Settings className="w-4 h-4" />
          </button>
        </div>
      </div>

      {isTemplateGalleryOpen && (
        <Suspense fallback={<div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg p-6">
            <Loader2 className="w-8 h-8 animate-spin text-loco-primary" />
          </div>
        </div>}>
          <TemplateGallery
            isOpen={isTemplateGalleryOpen}
            onClose={() => setIsTemplateGalleryOpen(false)}
          />
        </Suspense>
      )}
    </>
  );
}
