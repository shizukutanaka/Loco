import { useState } from 'react';
import { Toolbar } from '@/components/Toolbar/Toolbar';
import { NodePalette } from '@/components/NodePalette/NodePalette';
import { WorkflowCanvasWrapper } from '@/components/Canvas/WorkflowCanvas';
import { PropertyPanel } from '@/components/PropertyPanel/PropertyPanel';
import { ValidationPanel } from '@/components/ValidationPanel/ValidationPanel';
import { NodeSearch } from '@/components/NodeSearch/NodeSearch';
import { useKeyboardShortcuts } from '@/hooks/useKeyboardShortcuts';
import { useWorkflowStore } from '@/store/workflowStore';

function App() {
  const [isNodeSearchOpen, setIsNodeSearchOpen] = useState(false);
  const { exportWorkflow } = useWorkflowStore();

  // Keyboard shortcuts
  useKeyboardShortcuts({
    onSave: () => {
      alert('Save functionality will be connected to backend API');
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
    </div>
  );
}

export default App;
