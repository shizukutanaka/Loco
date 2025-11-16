import { useState } from 'react';

interface ContextMenuState {
  isOpen: boolean;
  position: { x: number; y: number };
  nodeId: string | null;
  nodeType: string | null;
}

interface UseCanvasContextMenuReturn extends ContextMenuState {
  openContextMenu: (x: number, y: number, nodeId: string | null, nodeType: string | null) => void;
  closeContextMenu: () => void;
  openCanvasContextMenu: (x: number, y: number) => void;
}

/**
 * Custom hook for managing canvas context menu state
 * Handles both node context menus and canvas right-click context menus
 */
export function useCanvasContextMenu(): UseCanvasContextMenuReturn {
  const [contextMenu, setContextMenu] = useState<ContextMenuState>({
    isOpen: false,
    position: { x: 0, y: 0 },
    nodeId: null,
    nodeType: null,
  });

  const openContextMenu = (x: number, y: number, nodeId: string | null, nodeType: string | null) => {
    setContextMenu({
      isOpen: true,
      position: { x, y },
      nodeId,
      nodeType,
    });
  };

  const closeContextMenu = () => {
    setContextMenu((prev) => ({ ...prev, isOpen: false }));
  };

  const openCanvasContextMenu = (x: number, y: number) => {
    setContextMenu({
      isOpen: true,
      position: { x, y },
      nodeId: null,
      nodeType: null,
    });
  };

  return {
    ...contextMenu,
    openContextMenu,
    closeContextMenu,
    openCanvasContextMenu,
  };
}
