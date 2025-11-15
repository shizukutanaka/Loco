import { useState, useEffect, lazy, Suspense } from 'react';
import { Toolbar } from '@/components/Toolbar/Toolbar';
import { NodePalette } from '@/components/NodePalette/NodePalette';
import { WorkflowCanvasWrapper } from '@/components/Canvas/WorkflowCanvas';
import { PropertyPanel } from '@/components/PropertyPanel/PropertyPanel';
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
      const jsonString = JSON.stringify(workflow, null, 2);
      const blob = new Blob([jsonString], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${workflow.name || 'workflow'}.json`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    },
    onSearch: () => {
      setIsNodeSearchOpen(true);
    },
  });

  return (
    <div className="h-screen flex flex-col">
      <Toolbar />
      <div className="flex-1 flex flex-col overflow-hidden">
        <div className="flex-1 flex overflow-hidden">
          <NodePalette />
          <WorkflowCanvasWrapper />
          <PropertyPanel />
        </div>
        {isExecutionPanelOpen && (
          <ExecutionPanel
            executionId={currentExecutionId}
            onClose={closeExecutionPanel}
          />
        )}
      </div>
      <Suspense fallback={null}>
        <ValidationPanel />
      </Suspense>
      <NodeSearch isOpen={isNodeSearchOpen} onClose={() => setIsNodeSearchOpen(false)} />
      <ToastContainer />
      <PerformanceMonitor />

      {/* AI Assistant */}
      <AIAssistant isOpen={isAIAssistantOpen} onClose={() => setIsAIAssistantOpen(false)} />

      {/* AI Assistant Toggle Button */}
      {!isAIAssistantOpen && (
        <button
          onClick={() => setIsAIAssistantOpen(true)}
          className="fixed bottom-20 right-4 p-3 bg-gradient-to-r from-blue-600 to-purple-600 text-white rounded-full shadow-lg hover:shadow-xl transition-shadow z-30"
          title="Open AI Assistant"
        >
          <Sparkles className="w-6 h-6" />
        </button>
      )}
    </div>
  );
}

export default App;
