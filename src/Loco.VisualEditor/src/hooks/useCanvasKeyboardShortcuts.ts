import { useEffect } from 'react';

interface UseCanvasKeyboardShortcutsOptions {
  selectedNodeIds: string[];
  onDeleteNodes: (nodeIds: string[]) => void;
  onDuplicateNode: () => void;
  onClearSelection: () => void;
}

/**
 * Custom hook for managing canvas-specific keyboard shortcuts
 * Handles: Delete/Backspace for node deletion, Ctrl+D for duplication
 */
export function useCanvasKeyboardShortcuts({
  selectedNodeIds,
  onDeleteNodes,
  onDuplicateNode,
  onClearSelection,
}: UseCanvasKeyboardShortcutsOptions) {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ignore if typing in input/textarea
      const target = e.target as HTMLElement;
      if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
        return;
      }

      const isMac = navigator.platform.toUpperCase().indexOf('MAC') >= 0;
      const ctrlKey = isMac ? e.metaKey : e.ctrlKey;

      // Delete or Backspace key
      if ((e.key === 'Delete' || e.key === 'Backspace') && selectedNodeIds.length > 0) {
        e.preventDefault();
        onDeleteNodes(selectedNodeIds);
        onClearSelection();
      }

      // Duplicate with Ctrl+D
      if (ctrlKey && e.key === 'd' && selectedNodeIds.length > 0) {
        e.preventDefault();
        onDuplicateNode();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [selectedNodeIds, onDeleteNodes, onDuplicateNode, onClearSelection]);
}
