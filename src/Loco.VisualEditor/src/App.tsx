import { useState, useEffect } from 'react';
import { Toolbar } from '@/components/Toolbar/Toolbar';
import { NodePalette } from '@/components/NodePalette/NodePalette';
import { WorkflowCanvasWrapper } from '@/components/Canvas/WorkflowCanvas';
import { PropertyPanel } from '@/components/PropertyPanel/PropertyPanel';
import { ValidationPanel } from '@/components/ValidationPanel/ValidationPanel';
import { NodeSearch } from '@/components/NodeSearch/NodeSearch';
import { ToastContainer } from '@/components/Toast/Toast';
import { useKeyboardShortcuts } from '@/hooks/useKeyboardShortcuts';
import { useAutoSave } from '@/hooks/useAutoSave';
import { useWorkflowStore } from '@/store/workflowStore';
import { useToast } from '@/contexts/ToastContext';

function App() {
  const [isNodeSearchOpen, setIsNodeSearchOpen] = useState(false);
  const { exportWorkflow, loadWorkflow } = useWorkflowStore();
  const { loadDraft, clearDraft } = useAutoSave();
  const toast = useToast();

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
      <div className="flex-1 flex overflow-hidden">
        <NodePalette />
        <WorkflowCanvasWrapper />
        <PropertyPanel />
      </div>
      <ValidationPanel />
      <NodeSearch isOpen={isNodeSearchOpen} onClose={() => setIsNodeSearchOpen(false)} />
      <ToastContainer />
    </div>
  );
}

export default App;
