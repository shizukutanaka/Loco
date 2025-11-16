import { useEffect } from 'react';
import { useReactFlow } from 'reactflow';

/**
 * Custom hook for managing canvas zoom controls via keyboard shortcuts and window events
 * Handles: Ctrl+Plus/Minus for zoom, Ctrl+0 for reset, Ctrl+Shift+F for fit-view
 */
export function useCanvasZoomControls() {
  const reactFlowInstance = useReactFlow();

  useEffect(() => {
    const handleZoomIn = () => reactFlowInstance.zoomIn({ duration: 200 });
    const handleZoomOut = () => reactFlowInstance.zoomOut({ duration: 200 });
    const handleResetZoom = () => reactFlowInstance.setViewport({ x: 0, y: 0, zoom: 1 }, { duration: 400 });
    const handleFitView = () => reactFlowInstance.fitView({ padding: 0.2, duration: 400 });

    window.addEventListener('canvas:zoom-in', handleZoomIn);
    window.addEventListener('canvas:zoom-out', handleZoomOut);
    window.addEventListener('canvas:reset-zoom', handleResetZoom);
    window.addEventListener('canvas:fit-view', handleFitView);

    return () => {
      window.removeEventListener('canvas:zoom-in', handleZoomIn);
      window.removeEventListener('canvas:zoom-out', handleZoomOut);
      window.removeEventListener('canvas:reset-zoom', handleResetZoom);
      window.removeEventListener('canvas:fit-view', handleFitView);
    };
  }, [reactFlowInstance]);
}
