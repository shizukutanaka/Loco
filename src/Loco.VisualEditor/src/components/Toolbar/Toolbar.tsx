import { useState, useEffect, lazy, Suspense } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';
import { useExecutionStore } from '@/store/executionStore';
import { useToast } from '@/contexts/ToastContext';
import { useLiveRegion } from '@/utils/ariaLiveRegion';
import { TOAST_LONG_DURATION } from '@/utils/constants';
import {
  Save,
  Download,
  Play,
  Plus,
  Settings,
  LayoutTemplate,
  Loader2,
  List,
  Users,
  Keyboard,
  Shuffle,
  FolderOpen,
  CheckCircle,
} from 'lucide-react';
import { createWorkflow, updateWorkflow, executeWorkflow, workflowToCreateRequest } from '@/api/workflows';
import type { Workflow } from '@/types/workflow';
import { exportWorkflowAsJson } from '@/utils/exportWorkflow';
import { WorkflowList } from '@/components/WorkflowList/WorkflowList';
import { TagEditor } from '@/components/TagEditor/TagEditor';
import { CollaborationPanel } from '@/components/CollaborationPanel/CollaborationPanel';
import { WorkflowTester } from '@/components/WorkflowTester/WorkflowTester';
import { KeyboardShortcuts } from '@/components/KeyboardShortcuts/KeyboardShortcuts';
import { isApiSuccess, isApiError, getExecutionCompletionTime } from '@/utils/typeGuards';

// Lazy load TemplateGallery (large component with template data)
const TemplateGallery = lazy(() => import('@/components/TemplateGallery/TemplateGallery').then(module => ({
  default: module.TemplateGallery
})));

// Lazy load SettingsPanel (large component with settings management)
const SettingsPanel = lazy(() => import('@/components/SettingsPanel/SettingsPanel').then(module => ({
  default: module.SettingsPanel
})));

export function Toolbar() {
  const { workflow, newWorkflow, loadWorkflow, exportWorkflow, updateWorkflowMetadata, undo, redo, canUndo, canRedo, autoLayout } =
    useWorkflowStore();
  const { setCurrentExecution, addToHistory } = useExecutionStore();
  const toast = useToast();
  const [isEditingName, setIsEditingName] = useState(false);
  const [workflowName, setWorkflowName] = useState(workflow?.name || 'New Workflow');
  const [isTemplateGalleryOpen, setIsTemplateGalleryOpen] = useState(false);
  const [isWorkflowListOpen, setIsWorkflowListOpen] = useState(false);
  const [isSettingsPanelOpen, setIsSettingsPanelOpen] = useState(false);
  const [isCollaborationPanelOpen, setIsCollaborationPanelOpen] = useState(false);
  const [isWorkflowTesterOpen, setIsWorkflowTesterOpen] = useState(false);
  const [isKeyboardShortcutsOpen, setIsKeyboardShortcutsOpen] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isRunning, setIsRunning] = useState(false);

  // Live regions for accessibility announcements
  const { announce: announceSave } = useLiveRegion('toolbar-save-status', 'assertive');
  const { announce: announceRun } = useLiveRegion('toolbar-run-status', 'assertive');
  const { announce: announceImport } = useLiveRegion('toolbar-import-status', 'polite');
  const { announce: announceExport } = useLiveRegion('toolbar-export-status', 'polite');
  const { announce: announceLayout } = useLiveRegion('toolbar-layout-status', 'polite');

  // Listen for keyboard shortcut events
  useEffect(() => {
    const handleSaveEvent = () => {
      handleSaveWorkflow();
    };

    window.addEventListener('workflow:save', handleSaveEvent);
    return () => window.removeEventListener('workflow:save', handleSaveEvent);
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // Global keyboard shortcuts handler
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ignore if typing in input/textarea
      const target = e.target as HTMLElement;
      if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
        return;
      }

      const isMac = navigator.platform.toUpperCase().indexOf('MAC') >= 0;
      const ctrlKey = isMac ? e.metaKey : e.ctrlKey;

      // Help shortcuts: ? or Ctrl+/
      if (e.key === '?' || (ctrlKey && e.key === '/')) {
        e.preventDefault();
        setIsKeyboardShortcutsOpen(true);
        return;
      }

      // Ctrl shortcuts
      if (ctrlKey) {
        switch (e.key.toLowerCase()) {
          case 'z':
            e.preventDefault();
            if (e.shiftKey) {
              // Ctrl+Shift+Z = Redo
              if (canRedo) redo();
            } else {
              // Ctrl+Z = Undo
              if (canUndo) undo();
            }
            break;
          case 'y':
            // Ctrl+Y = Redo (alternative)
            e.preventDefault();
            if (canRedo) redo();
            break;
          case 'n':
            e.preventDefault();
            handleNewWorkflow();
            break;
          case 's':
            e.preventDefault();
            handleSaveWorkflow();
            break;
          case 'o':
            e.preventDefault();
            handleImportJSON();
            break;
          case 'e':
            e.preventDefault();
            handleExportJSON();
            break;
          case 'k':
            e.preventDefault();
            setIsWorkflowListOpen(true);
            break;
          case 't':
            if (!e.shiftKey) {
              e.preventDefault();
              setIsTemplateGalleryOpen(true);
            } else {
              e.preventDefault();
              setIsWorkflowTesterOpen(true);
            }
            break;
          case ',':
            e.preventDefault();
            setIsSettingsPanelOpen(true);
            break;
          case 'enter':
            e.preventDefault();
            handleRunWorkflow();
            break;
          default:
            break;
        }

        // Ctrl+Shift shortcuts
        if (e.shiftKey) {
          switch (e.key.toLowerCase()) {
            case 'c':
              e.preventDefault();
              setIsCollaborationPanelOpen(true);
              break;
            case 'f':
              e.preventDefault();
              window.dispatchEvent(new CustomEvent('canvas:fit-view'));
              break;
            default:
              break;
          }
        }
      }

      // Zoom controls (not combined with shift)
      if (ctrlKey && !e.shiftKey) {
        switch (e.key) {
          case '+':
          case '=':
            e.preventDefault();
            window.dispatchEvent(new CustomEvent('canvas:zoom-in'));
            break;
          case '-':
          case '_':
            e.preventDefault();
            window.dispatchEvent(new CustomEvent('canvas:zoom-out'));
            break;
          case '0':
            e.preventDefault();
            window.dispatchEvent(new CustomEvent('canvas:reset-zoom'));
            break;
          default:
            break;
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [canUndo, canRedo, undo, redo]);

  const handleNewWorkflow = () => {
    if (confirm('Create a new workflow? Current workflow will be cleared.')) {
      newWorkflow();
      setWorkflowName('New Workflow');
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
            setWorkflowName(workflowData.name);
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

  const handleNameBlur = () => {
    setIsEditingName(false);
    if (workflowName.trim()) {
      updateWorkflowMetadata({ name: workflowName });
    } else {
      setWorkflowName(workflow?.name || 'New Workflow');
    }
  };

  const handleTagsChange = (tags: string[]) => {
    updateWorkflowMetadata({
      metadata: {
        ...workflow?.metadata,
        version: workflow?.metadata?.version || '1.0',
        isPublic: workflow?.metadata?.isPublic || false,
        tags,
      },
    });
  };

  const handleAutoLayout = () => {
    autoLayout('TB'); // Top-to-bottom layout by default
    announceLayout('Workflow layout optimized and nodes automatically arranged');
    toast.success('Workflow layout optimized');
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

          <div className="flex flex-col gap-1">
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
                className="text-lg font-medium text-gray-900 hover:text-loco-primary transition-colors text-left"
                aria-label={`Edit workflow name: ${workflowName}`}
              >
                {workflowName}
              </button>
            )}

            <TagEditor
              tags={workflow?.metadata?.tags || []}
              onChange={handleTagsChange}
            />
          </div>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={handleNewWorkflow}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="New Workflow"
            aria-label="Create a new workflow (Ctrl+N)"
          >
            <Plus className="w-4 h-4" aria-hidden="true" />
            <span className="text-sm font-medium">New</span>
          </button>

          <button
            onClick={() => setIsTemplateGalleryOpen(true)}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Templates"
            aria-label="Browse workflow templates (Ctrl+T)"
          >
            <LayoutTemplate className="w-4 h-4" aria-hidden="true" />
            <span className="text-sm font-medium">Templates</span>
          </button>

          <button
            onClick={() => setIsWorkflowListOpen(true)}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="My Workflows"
            aria-label="View saved workflows (Ctrl+K)"
          >
            <List className="w-4 h-4" aria-hidden="true" />
            <span className="text-sm font-medium">My Workflows</span>
          </button>

          <button
            onClick={() => setIsCollaborationPanelOpen(true)}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Collaboration"
            aria-label="Open collaboration panel (Ctrl+Shift+C)"
          >
            <Users className="w-4 h-4" aria-hidden="true" />
            <span className="text-sm font-medium">Collaborate</span>
          </button>

          <div className="h-8 w-px bg-gray-300"></div>

          <button
            onClick={handleImportJSON}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Import JSON"
            aria-label="Import workflow from JSON file (Ctrl+O)"
          >
            <FolderOpen className="w-4 h-4" aria-hidden="true" />
            <span className="text-sm font-medium">Import</span>
          </button>

          <button
            onClick={handleExportJSON}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Export JSON"
            aria-label="Export workflow as JSON file (Ctrl+E)"
          >
            <Download className="w-4 h-4" aria-hidden="true" />
            <span className="text-sm font-medium">Export</span>
          </button>

          <div className="h-8 w-px bg-gray-300"></div>

          <button
            onClick={handleAutoLayout}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Auto-layout (Organize nodes automatically)"
            aria-label="Auto-layout workflow nodes (Ctrl+Shift+F)"
          >
            <Shuffle className="w-4 h-4" aria-hidden="true" />
            <span className="text-sm font-medium">Auto Layout</span>
          </button>

          <button
            onClick={handleSaveWorkflow}
            disabled={isSaving}
            className="flex items-center gap-2 px-4 py-2 text-white bg-loco-primary hover:bg-blue-700 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            title="Save Workflow"
            aria-label={isSaving ? 'Saving workflow...' : 'Save workflow (Ctrl+S)'}
          >
            {isSaving ? (
              <Loader2 className="w-4 h-4 animate-spin" aria-hidden="true" />
            ) : (
              <Save className="w-4 h-4" aria-hidden="true" />
            )}
            <span className="text-sm font-medium">{isSaving ? 'Saving...' : 'Save'}</span>
          </button>

          <div className="h-8 w-px bg-gray-300 mx-2"></div>

          <button
            onClick={() => setIsWorkflowTesterOpen(true)}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 border border-gray-300 hover:bg-gray-50 rounded-lg transition-colors"
            title="Test Workflow"
            aria-label="Open workflow tester (Ctrl+Shift+T)"
          >
            <CheckCircle className="w-4 h-4" aria-hidden="true" />
            <span className="text-sm font-medium">Test</span>
          </button>

          <button
            onClick={handleRunWorkflow}
            disabled={isRunning}
            className="flex items-center gap-2 px-4 py-2 text-white bg-loco-success hover:bg-green-700 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            title="Run Workflow"
            aria-label={isRunning ? 'Workflow running...' : 'Run workflow (Ctrl+Enter)'}
          >
            {isRunning ? (
              <Loader2 className="w-4 h-4 animate-spin" aria-hidden="true" />
            ) : (
              <Play className="w-4 h-4" aria-hidden="true" />
            )}
            <span className="text-sm font-medium">{isRunning ? 'Running...' : 'Run'}</span>
          </button>

          <button
            onClick={() => setIsSettingsPanelOpen(true)}
            className="p-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Settings"
            aria-label="Open settings panel (Ctrl+,)"
          >
            <Settings className="w-4 h-4" aria-hidden="true" />
          </button>

          <button
            onClick={() => setIsKeyboardShortcutsOpen(true)}
            className="p-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Keyboard Shortcuts (? or Ctrl+/)"
            aria-label="View keyboard shortcuts (? or Ctrl+/)"
          >
            <Keyboard className="w-4 h-4" aria-hidden="true" />
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

      <WorkflowList
        isOpen={isWorkflowListOpen}
        onClose={() => setIsWorkflowListOpen(false)}
      />

      {isSettingsPanelOpen && (
        <Suspense fallback={<div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg p-6">
            <Loader2 className="w-8 h-8 animate-spin text-loco-primary" />
          </div>
        </div>}>
          <SettingsPanel
            isOpen={isSettingsPanelOpen}
            onClose={() => setIsSettingsPanelOpen(false)}
          />
        </Suspense>
      )}

      <CollaborationPanel
        workflowId={workflow?.id || ''}
        isOpen={isCollaborationPanelOpen}
        onClose={() => setIsCollaborationPanelOpen(false)}
      />

      <WorkflowTester
        workflowId={workflow?.id || ''}
        workflowName={workflow?.name || 'New Workflow'}
        isOpen={isWorkflowTesterOpen}
        onClose={() => setIsWorkflowTesterOpen(false)}
      />

      <KeyboardShortcuts
        isOpen={isKeyboardShortcutsOpen}
        onClose={() => setIsKeyboardShortcutsOpen(false)}
      />
    </>
  );
}
