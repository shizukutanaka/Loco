/**
 * Canvas Controls Component
 *
 * Provides visual controls for canvas navigation and zoom:
 * - Zoom in/out buttons
 * - Fit to view button
 * - Reset zoom button
 * - Current zoom level display
 * - Minimap toggle
 *
 * Integrates with React Flow's viewport controls and keyboard shortcuts.
 */

import { useReactFlow } from 'reactflow';
import {
  ZoomIn,
  ZoomOut,
  Maximize2,
  RotateCcw,
  Map,
} from 'lucide-react';

// ============================================================================
// Types
// ============================================================================

interface CanvasControlsProps {
  showMinimap: boolean;
  onToggleMinimap: () => void;
}

// ============================================================================
// Canvas Controls Component
// ============================================================================

export function CanvasControls({ showMinimap, onToggleMinimap }: CanvasControlsProps) {
  const reactFlowInstance = useReactFlow();
  const viewport = reactFlowInstance.getViewport();
  const zoomPercentage = Math.round(viewport.zoom * 100);

  const handleZoomIn = () => {
    reactFlowInstance.zoomIn({ duration: 200 });
  };

  const handleZoomOut = () => {
    reactFlowInstance.zoomOut({ duration: 200 });
  };

  const handleFitView = () => {
    reactFlowInstance.fitView({ padding: 0.2, duration: 400 });
  };

  const handleResetZoom = () => {
    reactFlowInstance.setViewport({ x: 0, y: 0, zoom: 1 }, { duration: 400 });
  };

  return (
    <div className="fixed bottom-6 right-6 flex flex-col gap-2 z-10">
      {/* Zoom Controls */}
      <div className="bg-white rounded-lg shadow-lg border border-gray-200 overflow-hidden">
        {/* Zoom In */}
        <button
          onClick={handleZoomIn}
          className="w-10 h-10 flex items-center justify-center text-gray-700 hover:bg-gray-100 transition-colors border-b border-gray-200"
          title="Zoom In (Ctrl + +)"
          aria-label="Zoom in"
        >
          <ZoomIn className="w-4 h-4" />
        </button>

        {/* Zoom Level Display */}
        <div className="w-10 h-10 flex items-center justify-center text-xs font-medium text-gray-700 border-b border-gray-200 bg-gray-50">
          {zoomPercentage}%
        </div>

        {/* Zoom Out */}
        <button
          onClick={handleZoomOut}
          className="w-10 h-10 flex items-center justify-center text-gray-700 hover:bg-gray-100 transition-colors border-b border-gray-200"
          title="Zoom Out (Ctrl + -)"
          aria-label="Zoom out"
        >
          <ZoomOut className="w-4 h-4" />
        </button>

        {/* Reset Zoom */}
        <button
          onClick={handleResetZoom}
          className="w-10 h-10 flex items-center justify-center text-gray-700 hover:bg-gray-100 transition-colors border-b border-gray-200"
          title="Reset Zoom (Ctrl + 0)"
          aria-label="Reset zoom to 100%"
        >
          <RotateCcw className="w-4 h-4" />
        </button>

        {/* Fit to View */}
        <button
          onClick={handleFitView}
          className="w-10 h-10 flex items-center justify-center text-gray-700 hover:bg-gray-100 transition-colors"
          title="Fit to View (Ctrl + Shift + F)"
          aria-label="Fit workflow to view"
        >
          <Maximize2 className="w-4 h-4" />
        </button>
      </div>

      {/* Minimap Toggle */}
      <div className="bg-white rounded-lg shadow-lg border border-gray-200 overflow-hidden">
        <button
          onClick={onToggleMinimap}
          className={`w-10 h-10 flex items-center justify-center transition-colors ${
            showMinimap
              ? 'bg-loco-primary text-white hover:bg-blue-700'
              : 'text-gray-700 hover:bg-gray-100'
          }`}
          title={showMinimap ? 'Hide Minimap' : 'Show Minimap'}
          aria-label={showMinimap ? 'Hide minimap' : 'Show minimap'}
          aria-pressed={showMinimap}
        >
          <Map className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
}
