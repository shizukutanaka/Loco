import { Toolbar } from '@/components/Toolbar/Toolbar';
import { NodePalette } from '@/components/NodePalette/NodePalette';
import { WorkflowCanvasWrapper } from '@/components/Canvas/WorkflowCanvas';
import { PropertyPanel } from '@/components/PropertyPanel/PropertyPanel';

function App() {
  return (
    <div className="h-screen flex flex-col">
      <Toolbar />
      <div className="flex-1 flex overflow-hidden">
        <NodePalette />
        <WorkflowCanvasWrapper />
        <PropertyPanel />
      </div>
    </div>
  );
}

export default App;
