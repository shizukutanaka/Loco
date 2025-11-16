import { useState, lazy, Suspense } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';
import { useLiveRegion } from '@/utils/ariaLiveRegion';
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
import { useToolbarKeyboardShortcuts, useWorkflowOperations, useWorkflowExecution, useToolbarModals } from '@/hooks';
import { WorkflowList } from '@/components/WorkflowList/WorkflowList';
import { TagEditor } from '@/components/TagEditor/TagEditor';
import { CollaborationPanel } from '@/components/CollaborationPanel/CollaborationPanel';
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
  const { workflow, updateWorkflowMetadata, autoLayout } = useWorkflowStore();
  const { announce: announceLayout } = useLiveRegion('toolbar-layout-status', 'polite');

  // Custom hooks for separated concerns
  const modals = useToolbarModals();
  const operations = useWorkflowOperations();
  const execution = useWorkflowExecution();

  // Local state for workflow name editing
  const [workflowName, setWorkflowName] = useState(workflow?.name || 'New Workflow');

  // Setup keyboard shortcuts with handlers pointing to modal actions
  useToolbarKeyboardShortcuts({
    onNew: () => {
      operations.handleNewWorkflow();
      setWorkflowName('New Workflow');
    },
    onSave: operations.handleSaveWorkflow,
    onImport: operations.handleImportJSON,
    onExport: operations.handleExportJSON,
    onOpenWorkflowList: modals.openWorkflowList,
    onOpenTemplateGallery: modals.openTemplateGallery,
    onOpenWorkflowTester: modals.openWorkflowTester,
    onOpenSettings: modals.openSettingsPanel,
    onOpenCollaboration: modals.openCollaborationPanel,
    onOpenKeyboardShortcuts: modals.openKeyboardShortcuts,
    onRun: execution.handleRunWorkflow,
  });

  const handleNameBlur = () => {
    modals.stopEditingName();
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
            {modals.isEditingName ? (
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
                onClick={modals.startEditingName}
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
            onClick={() => {
              operations.handleNewWorkflow();
              setWorkflowName('New Workflow');
            }}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="New Workflow"
            aria-label="Create a new workflow (Ctrl+N)"
          >
            <Plus className="w-4 h-4" aria-hidden="true" />
            <span className="text-sm font-medium">New</span>
          </button>

          <button
            onClick={modals.openTemplateGallery}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Templates"
            aria-label="Browse workflow templates (Ctrl+T)"
          >
            <LayoutTemplate className="w-4 h-4" aria-hidden="true" />
            <span className="text-sm font-medium">Templates</span>
          </button>

          <button
            onClick={modals.openWorkflowList}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="My Workflows"
            aria-label="View saved workflows (Ctrl+K)"
          >
            <List className="w-4 h-4" aria-hidden="true" />
            <span className="text-sm font-medium">My Workflows</span>
          </button>

          <button
            onClick={modals.openCollaborationPanel}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Collaboration"
            aria-label="Open collaboration panel (Ctrl+Shift+C)"
          >
            <Users className="w-4 h-4" aria-hidden="true" />
            <span className="text-sm font-medium">Collaborate</span>
          </button>

          <div className="h-8 w-px bg-gray-300"></div>

          <button
            onClick={operations.handleImportJSON}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Import JSON"
            aria-label="Import workflow from JSON file (Ctrl+O)"
          >
            <FolderOpen className="w-4 h-4" aria-hidden="true" />
            <span className="text-sm font-medium">Import</span>
          </button>

          <button
            onClick={operations.handleExportJSON}
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
            onClick={operations.handleSaveWorkflow}
            disabled={operations.isSaving}
            className="flex items-center gap-2 px-4 py-2 text-white bg-loco-primary hover:bg-blue-700 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            title="Save Workflow"
            aria-label={operations.isSaving ? 'Saving workflow...' : 'Save workflow (Ctrl+S)'}
          >
            {operations.isSaving ? (
              <Loader2 className="w-4 h-4 animate-spin" aria-hidden="true" />
            ) : (
              <Save className="w-4 h-4" aria-hidden="true" />
            )}
            <span className="text-sm font-medium">{operations.isSaving ? 'Saving...' : 'Save'}</span>
          </button>

          <div className="h-8 w-px bg-gray-300 mx-2"></div>

          <button
            onClick={modals.openWorkflowTester}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 border border-gray-300 hover:bg-gray-50 rounded-lg transition-colors"
            title="Test Workflow"
            aria-label="Open workflow tester (Ctrl+Shift+T)"
          >
            <CheckCircle className="w-4 h-4" aria-hidden="true" />
            <span className="text-sm font-medium">Test</span>
          </button>

          <button
            onClick={execution.handleRunWorkflow}
            disabled={execution.isRunning}
            className="flex items-center gap-2 px-4 py-2 text-white bg-loco-success hover:bg-green-700 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            title="Run Workflow"
            aria-label={execution.isRunning ? 'Workflow running...' : 'Run workflow (Ctrl+Enter)'}
          >
            {execution.isRunning ? (
              <Loader2 className="w-4 h-4 animate-spin" aria-hidden="true" />
            ) : (
              <Play className="w-4 h-4" aria-hidden="true" />
            )}
            <span className="text-sm font-medium">{execution.isRunning ? 'Running...' : 'Run'}</span>
          </button>

          <button
            onClick={modals.openSettingsPanel}
            className="p-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Settings"
            aria-label="Open settings panel (Ctrl+,)"
          >
            <Settings className="w-4 h-4" aria-hidden="true" />
          </button>

          <button
            onClick={modals.openKeyboardShortcuts}
            className="p-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Keyboard Shortcuts (? or Ctrl+/)"
            aria-label="View keyboard shortcuts (? or Ctrl+/)"
          >
            <Keyboard className="w-4 h-4" aria-hidden="true" />
          </button>
        </div>
      </div>

      {modals.isTemplateGalleryOpen && (
        <Suspense fallback={<div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg p-6">
            <Loader2 className="w-8 h-8 animate-spin text-loco-primary" />
          </div>
        </div>}>
          <TemplateGallery
            isOpen={modals.isTemplateGalleryOpen}
            onClose={modals.closeTemplateGallery}
          />
        </Suspense>
      )}

      <WorkflowList
        isOpen={modals.isWorkflowListOpen}
        onClose={modals.closeWorkflowList}
      />

      {modals.isSettingsPanelOpen && (
        <Suspense fallback={<div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg p-6">
            <Loader2 className="w-8 h-8 animate-spin text-loco-primary" />
          </div>
        </div>}>
          <SettingsPanel
            isOpen={modals.isSettingsPanelOpen}
            onClose={modals.closeSettingsPanel}
          />
        </Suspense>
      )}

      <CollaborationPanel
        workflowId={workflow?.id || ''}
        isOpen={modals.isCollaborationPanelOpen}
        onClose={modals.closeCollaborationPanel}
      />

      <WorkflowTester
        workflowId={workflow?.id || ''}
        workflowName={workflow?.name || 'New Workflow'}
        isOpen={modals.isWorkflowTesterOpen}
        onClose={modals.closeWorkflowTester}
      />

      <KeyboardShortcuts
        isOpen={modals.isKeyboardShortcutsOpen}
        onClose={modals.closeKeyboardShortcuts}
      />
    </>
  );
}
