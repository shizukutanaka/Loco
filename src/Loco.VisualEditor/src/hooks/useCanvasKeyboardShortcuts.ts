import { useEffect, useRef } from 'react';

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
  // Store selectedNodeIds in a ref to avoid re-registering the listener when selection changes
  const selectedNodeIdsRef = useRef<string[]>(selectedNodeIds);
  const onDeleteNodesRef = useRef(onDeleteNodes);
  const onDuplicateNodeRef = useRef(onDuplicateNode);
  const onClearSelectionRef = useRef(onClearSelection);

  // Update refs with current values without recreating the effect
  useEffect(() => {
    selectedNodeIdsRef.current = selectedNodeIds;
  }, [selectedNodeIds]);

  useEffect(() => {
    onDeleteNodesRef.current = onDeleteNodes;
  }, [onDeleteNodes]);

  useEffect(() => {
    onDuplicateNodeRef.current = onDuplicateNode;
  }, [onDuplicateNode]);

  useEffect(() => {
    onClearSelectionRef.current = onClearSelection;
  }, [onClearSelection]);

  // Event listener effect - stable with no dependencies
  // Uses refs to access current values without listener recreation
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ignore if typing in input/textarea
      const target = e.target as HTMLElement;
      if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
        return;
      }

      const isMac = navigator.platform.toUpperCase().indexOf('MAC') >= 0;
      const ctrlKey = isMac ? e.metaKey : e.ctrlKey;

      // Delete or Backspace key - use ref to access current selectedNodeIds
      if ((e.key === 'Delete' || e.key === 'Backspace') && selectedNodeIdsRef.current.length > 0) {
        e.preventDefault();
        onDeleteNodesRef.current(selectedNodeIdsRef.current);
        onClearSelectionRef.current();
      }

      // Duplicate with Ctrl+D - use ref to access current selectedNodeIds
      if (ctrlKey && e.key === 'd' && selectedNodeIdsRef.current.length > 0) {
        e.preventDefault();
        onDuplicateNodeRef.current();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []); // Empty dependency array - listener registered once, uses refs for current values
}
