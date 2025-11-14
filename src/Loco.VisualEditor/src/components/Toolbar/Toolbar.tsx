import { useState, useEffect, lazy, Suspense } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';
import { useExecutionStore } from '@/store/executionStore';
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
  List,
  Calendar,
  Globe,
  GitCommit,
  History,
  BarChart3,
  Users,
  Package,
  CheckCircle,
  Keyboard,
} from 'lucide-react';
import { createWorkflow, updateWorkflow, executeWorkflow, workflowToCreateRequest } from '@/api/workflows';
import { WorkflowList } from '@/components/WorkflowList/WorkflowList';
import { TagEditor } from '@/components/TagEditor/TagEditor';
import { ScheduleManager } from '@/components/ScheduleManager/ScheduleManager';
import { WebhookManager } from '@/components/WebhookManager/WebhookManager';
import { CommitDialog } from '@/components/CommitDialog/CommitDialog';
import { VersionHistory } from '@/components/VersionHistory/VersionHistory';
import { MetricsDashboard } from '@/components/MetricsDashboard/MetricsDashboard';
import { CollaborationPanel } from '@/components/CollaborationPanel/CollaborationPanel';
import { NodePluginManager } from '@/components/NodePluginManager/NodePluginManager';
import { WorkflowTester } from '@/components/WorkflowTester/WorkflowTester';
import { KeyboardShortcuts } from '@/components/KeyboardShortcuts/KeyboardShortcuts';

// Lazy load TemplateGallery (large component with template data)
const TemplateGallery = lazy(() => import('@/components/TemplateGallery/TemplateGallery').then(module => ({
  default: module.TemplateGallery
})));

// Lazy load SettingsPanel (large component with settings management)
const SettingsPanel = lazy(() => import('@/components/SettingsPanel/SettingsPanel').then(module => ({
  default: module.SettingsPanel
})));

export function Toolbar() {
  const { workflow, newWorkflow, loadWorkflow, exportWorkflow, updateWorkflowMetadata } =
    useWorkflowStore();
  const { setCurrentExecution, addToHistory } = useExecutionStore();
  const toast = useToast();
  const [isEditingName, setIsEditingName] = useState(false);
  const [workflowName, setWorkflowName] = useState(workflow?.name || 'New Workflow');
  const [isTemplateGalleryOpen, setIsTemplateGalleryOpen] = useState(false);
  const [isWorkflowListOpen, setIsWorkflowListOpen] = useState(false);
  const [isSettingsPanelOpen, setIsSettingsPanelOpen] = useState(false);
  const [isScheduleManagerOpen, setIsScheduleManagerOpen] = useState(false);
  const [isWebhookManagerOpen, setIsWebhookManagerOpen] = useState(false);
  const [isCommitDialogOpen, setIsCommitDialogOpen] = useState(false);
  const [isVersionHistoryOpen, setIsVersionHistoryOpen] = useState(false);
  const [isMetricsDashboardOpen, setIsMetricsDashboardOpen] = useState(false);
  const [isCollaborationPanelOpen, setIsCollaborationPanelOpen] = useState(false);
  const [isNodePluginManagerOpen, setIsNodePluginManagerOpen] = useState(false);
  const [isWorkflowTesterOpen, setIsWorkflowTesterOpen] = useState(false);
  const [isKeyboardShortcutsOpen, setIsKeyboardShortcutsOpen] = useState(false);
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
          case 'h':
            if (!e.shiftKey) {
              e.preventDefault();
              setIsVersionHistoryOpen(true);
            }
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
            case 's':
              e.preventDefault();
              setIsScheduleManagerOpen(true);
              break;
            case 'w':
              e.preventDefault();
              setIsWebhookManagerOpen(true);
              break;
            case 'm':
              e.preventDefault();
              setIsMetricsDashboardOpen(true);
              break;
            case 'c':
              e.preventDefault();
              setIsCollaborationPanelOpen(true);
              break;
            case 'p':
              e.preventDefault();
              setIsNodePluginManagerOpen(true);
              break;
            case 'k':
              e.preventDefault();
              setIsCommitDialogOpen(true);
              break;
            case 'f':
              e.preventDefault();
              // TODO: Implement fit-to-view functionality
              console.log('Fit to view shortcut triggered');
              break;
            default:
              break;
          }
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
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
            toast.success(`Workflow "${workflowData.name}" imported successfully!`);
          } catch (error) {
            console.error('Failed to import workflow:', error);
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

        // Add to execution history
        addToHistory({
          executionId: response.data.executionId,
          workflowId: workflowData.id,
          workflowName: workflowData.name,
          status: response.data.status,
          startedAt: response.data.startedAt,
          completedAt: response.data.completedAt,
        });

        // Open execution panel
        setCurrentExecution(response.data.executionId);

        toast.success(`Workflow "${workflowData.name}" is running...`);
        toast.info(`Execution ID: ${response.data.executionId}`, 7000);
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
            onClick={() => setIsWorkflowListOpen(true)}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="My Workflows"
          >
            <List className="w-4 h-4" />
            <span className="text-sm font-medium">My Workflows</span>
          </button>

          <button
            onClick={() => setIsScheduleManagerOpen(true)}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Schedules"
          >
            <Calendar className="w-4 h-4" />
            <span className="text-sm font-medium">Schedules</span>
          </button>

          <button
            onClick={() => setIsWebhookManagerOpen(true)}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Webhooks"
          >
            <Globe className="w-4 h-4" />
            <span className="text-sm font-medium">Webhooks</span>
          </button>

          <button
            onClick={() => setIsMetricsDashboardOpen(true)}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Metrics Dashboard"
          >
            <BarChart3 className="w-4 h-4" />
            <span className="text-sm font-medium">Metrics</span>
          </button>

          <button
            onClick={() => setIsCollaborationPanelOpen(true)}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Collaboration"
          >
            <Users className="w-4 h-4" />
            <span className="text-sm font-medium">Collaborate</span>
          </button>

          <button
            onClick={() => setIsNodePluginManagerOpen(true)}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Plugin Manager"
          >
            <Package className="w-4 h-4" />
            <span className="text-sm font-medium">Plugins</span>
          </button>

          <div className="h-8 w-px bg-gray-300"></div>

          <button
            onClick={() => setIsCommitDialogOpen(true)}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Commit Changes"
          >
            <GitCommit className="w-4 h-4" />
            <span className="text-sm font-medium">Commit</span>
          </button>

          <button
            onClick={() => setIsVersionHistoryOpen(true)}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Version History"
          >
            <History className="w-4 h-4" />
            <span className="text-sm font-medium">History</span>
          </button>

          <div className="h-8 w-px bg-gray-300"></div>

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
            onClick={() => setIsWorkflowTesterOpen(true)}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 border border-gray-300 hover:bg-gray-50 rounded-lg transition-colors"
            title="Test Workflow"
          >
            <CheckCircle className="w-4 h-4" />
            <span className="text-sm font-medium">Test</span>
          </button>

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
            onClick={() => setIsSettingsPanelOpen(true)}
            className="p-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Settings"
          >
            <Settings className="w-4 h-4" />
          </button>

          <button
            onClick={() => setIsKeyboardShortcutsOpen(true)}
            className="p-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Keyboard Shortcuts (? or Ctrl+/)"
          >
            <Keyboard className="w-4 h-4" />
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

      <ScheduleManager
        isOpen={isScheduleManagerOpen}
        onClose={() => setIsScheduleManagerOpen(false)}
      />

      <WebhookManager
        isOpen={isWebhookManagerOpen}
        onClose={() => setIsWebhookManagerOpen(false)}
      />

      <CommitDialog
        workflowId={workflow?.id || ''}
        workflowName={workflow?.name || 'New Workflow'}
        isOpen={isCommitDialogOpen}
        onClose={() => setIsCommitDialogOpen(false)}
        onCommit={(message) => {
          console.log('Workflow committed:', message);
        }}
        changes={{
          nodesAdded: 0,
          nodesRemoved: 0,
          nodesModified: 0,
          edgesAdded: 0,
          edgesRemoved: 0,
        }}
      />

      <VersionHistory
        workflowId={workflow?.id || ''}
        workflowName={workflow?.name || 'New Workflow'}
        isOpen={isVersionHistoryOpen}
        onClose={() => setIsVersionHistoryOpen(false)}
        onRestore={(versionId) => {
          console.log('Restoring version:', versionId);
        }}
      />

      <MetricsDashboard
        isOpen={isMetricsDashboardOpen}
        onClose={() => setIsMetricsDashboardOpen(false)}
      />

      <CollaborationPanel
        workflowId={workflow?.id || ''}
        workflowName={workflow?.name || 'New Workflow'}
        isOpen={isCollaborationPanelOpen}
        onClose={() => setIsCollaborationPanelOpen(false)}
      />

      <NodePluginManager
        isOpen={isNodePluginManagerOpen}
        onClose={() => setIsNodePluginManagerOpen(false)}
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
