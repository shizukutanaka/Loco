import { useState, useEffect, lazy, Suspense } from 'react';
import { Toolbar } from '@/components/Toolbar/Toolbar';
import { NodePalette } from '@/components/NodePalette/NodePalette';
import { WorkflowCanvasWrapper } from '@/components/Canvas/WorkflowCanvas';
import { PropertyPanel } from '@/components/PropertyPanel/PropertyPanel';
import { EdgeConditionPanel } from '@/components/EdgeConditionPanel/EdgeConditionPanel';
import { NodeSearch } from '@/components/NodeSearch/NodeSearch';
import { ToastContainer } from '@/components/Toast/Toast';
import { ExecutionPanel } from '@/components/ExecutionPanel/ExecutionPanel';
import { PerformanceMonitor } from '@/components/PerformanceMonitor/PerformanceMonitor';
import { AIAssistant } from '@/components/AIAssistant/AIAssistant';
import { Sparkles } from 'lucide-react';
import { useKeyboardShortcuts } from '@/hooks/useKeyboardShortcuts';
import { useAutoSave } from '@/hooks/useAutoSave';
import { useOfflineDetection } from '@/hooks/useOfflineDetection';
import { useWorkflowStore } from '@/store/workflowStore';
import { useExecutionStore } from '@/store/executionStore';
import { useToast } from '@/contexts/ToastContext';
import { exportWorkflowAsJson } from '@/utils/exportWorkflow';

// Lazy load ValidationPanel (heavy validation logic)
const ValidationPanel = lazy(() => import('@/components/ValidationPanel/ValidationPanel').then(module => ({
  default: module.ValidationPanel
})));

function App() {
  const [isNodeSearchOpen, setIsNodeSearchOpen] = useState(false);
  const [isAIAssistantOpen, setIsAIAssistantOpen] = useState(false);
  const { exportWorkflow, loadWorkflow } = useWorkflowStore();
  const { currentExecutionId, isExecutionPanelOpen, closeExecutionPanel } = useExecutionStore();
  const { loadDraft, clearDraft } = useAutoSave();
  const toast = useToast();
  useOfflineDetection(); // Detect offline/online status and show toast notifications

  // Load draft on mount
  useEffect(() => {
    const draft = loadDraft();
    if (draft) {
      const shouldRestore = window.confirm(
        'A saved workflow draft was found. Would you like to restore it?'
      );
      if (shouldRestore) {
        loadWorkflow(draft);
        toast.info('Workflow draft restored successfully');
      } else {
        clearDraft();
      }
    }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // Keyboard shortcuts
  useKeyboardShortcuts({
    onSave: () => {
      // Save is handled by Toolbar component
      // Trigger a custom event to notify Toolbar
      window.dispatchEvent(new CustomEvent('workflow:save'));
    },
    onExport: () => {
      const workflow = exportWorkflow();
      exportWorkflowAsJson(workflow);
      toast.success('Workflow exported successfully');
    },
    onSearch: () => {
      setIsNodeSearchOpen(true);
    },
  });

  return (
    <div className="h-screen flex flex-col">
      {/* Header: Contains toolbar and application controls */}
      <header className="bg-white border-b border-gray-200" role="banner">
        <Toolbar />
      </header>

      {/* Main content area */}
      <main className="flex-1 flex flex-col overflow-hidden">
        {/* Workflow editor layout with sidebars and canvas */}
        <div className="flex-1 flex overflow-hidden">
          {/* Left sidebar: Node palette for adding nodes */}
          <aside
            className="w-64 bg-gray-50 border-r border-gray-200 overflow-y-auto"
            aria-label="Node palette - Add integrations and components to workflow"
          >
            <NodePalette />
          </aside>

          {/* Center: Workflow canvas */}
          <section
            className="flex-1 overflow-hidden"
            aria-label="Workflow canvas - Design and manage workflow nodes"
          >
            <WorkflowCanvasWrapper />
          </section>

          {/* Right sidebar: Node properties, or connection routing when an edge is selected */}
          <aside
            className="w-96 bg-white border-l border-gray-200 overflow-y-auto"
            aria-label="Properties panel - Configure selected node or connection"
          >
            <PropertyPanel />
            <EdgeConditionPanel />
          </aside>
        </div>

        {/* Execution results panel */}
        {isExecutionPanelOpen && (
          <section
            className="border-t border-gray-200 overflow-y-auto"
            aria-label="Execution results - View workflow execution output and logs"
          >
            <ExecutionPanel
              executionId={currentExecutionId}
              onClose={closeExecutionPanel}
            />
          </section>
        )}
      </main>

      {/* Floating panels and utilities */}
      <Suspense fallback={null}>
        <ValidationPanel />
      </Suspense>

      {/* Modal dialogs and popups */}
      <NodeSearch isOpen={isNodeSearchOpen} onClose={() => setIsNodeSearchOpen(false)} />
      <AIAssistant isOpen={isAIAssistantOpen} onClose={() => setIsAIAssistantOpen(false)} />

      {/* Notifications and monitoring */}
      <ToastContainer />
      <PerformanceMonitor />

      {/* AI Assistant Toggle Button - Fixed position floating action button */}
      {!isAIAssistantOpen && (
        <button
          onClick={() => setIsAIAssistantOpen(true)}
          className="fixed bottom-20 right-4 p-3 bg-gradient-to-r from-blue-600 to-purple-600 text-white rounded-full shadow-lg hover:shadow-xl transition-shadow z-30"
          title="Open AI Assistant (discuss workflow and get help)"
          aria-label="Open AI Assistant - Get help with your workflow"
        >
          <Sparkles className="w-6 h-6" aria-hidden="true" />
        </button>
      )}
    </div>
  );
}

export default App;
