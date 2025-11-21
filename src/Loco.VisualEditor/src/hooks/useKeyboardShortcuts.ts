import { useEffect } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';

interface KeyboardShortcutsOptions {
  onSave?: () => void;
  onExport?: () => void;
  onNew?: () => void;
  onUndo?: () => void;
  onRedo?: () => void;
  onDelete?: () => void;
  onSearch?: () => void;
  onTemplates?: () => void;
}

export function useKeyboardShortcuts(options: KeyboardShortcutsOptions = {}) {
  const { selectedNodeId, deleteNode, undo, redo, canUndo, canRedo } = useWorkflowStore();

  // Destructure callbacks to use in dependency array
  // This prevents the effect from re-running when the options object changes,
  // only when the actual callback functions change
  const {
    onSave,
    onExport,
    onNew,
    onUndo,
    onRedo,
    onDelete,
    onSearch,
    onTemplates,
  } = options;

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      const isMac = navigator.platform.toUpperCase().indexOf('MAC') >= 0;
      const ctrlKey = isMac ? event.metaKey : event.ctrlKey;

      // Ctrl/Cmd + S: Save
      if (ctrlKey && event.key === 's') {
        event.preventDefault();
        onSave?.();
      }

      // Ctrl/Cmd + E: Export
      if (ctrlKey && event.key === 'e') {
        event.preventDefault();
        onExport?.();
      }

      // Ctrl/Cmd + N: New workflow
      if (ctrlKey && event.key === 'n') {
        event.preventDefault();
        onNew?.();
      }

      // Ctrl/Cmd + Z: Undo
      if (ctrlKey && event.key === 'z' && !event.shiftKey) {
        event.preventDefault();
        if (canUndo && undo()) {
          onUndo?.();
        }
      }

      // Ctrl/Cmd + Shift + Z or Ctrl/Cmd + Y: Redo
      if ((ctrlKey && event.shiftKey && event.key === 'z') || (ctrlKey && event.key === 'y')) {
        event.preventDefault();
        if (canRedo && redo()) {
          onRedo?.();
        }
      }

      // Delete or Backspace: Delete selected node
      if ((event.key === 'Delete' || event.key === 'Backspace') && selectedNodeId) {
        event.preventDefault();
        if (onDelete) {
          onDelete();
        } else {
          deleteNode(selectedNodeId);
        }
      }

      // Ctrl/Cmd + K: Search
      if (ctrlKey && event.key === 'k') {
        event.preventDefault();
        onSearch?.();
      }

      // Ctrl/Cmd + T: Templates
      if (ctrlKey && event.key === 't') {
        event.preventDefault();
        onTemplates?.();
      }

      // Escape: Clear selection
      if (event.key === 'Escape') {
        // This is handled by React Flow by default
      }
    };

    window.addEventListener('keydown', handleKeyDown);

    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [
    selectedNodeId,
    deleteNode,
    undo,
    redo,
    canUndo,
    canRedo,
    onSave,
    onExport,
    onNew,
    onUndo,
    onRedo,
    onDelete,
    onSearch,
    onTemplates,
  ]);
}

// Hook to display keyboard shortcuts help
export function useKeyboardShortcutsHelp() {
  const isMac = navigator.platform.toUpperCase().indexOf('MAC') >= 0;
  const modKey = isMac ? '⌘' : 'Ctrl';

  return {
    shortcuts: [
      { key: `${modKey} + S`, description: 'Save workflow' },
      { key: `${modKey} + E`, description: 'Export workflow' },
      { key: `${modKey} + N`, description: 'New workflow' },
      { key: `${modKey} + T`, description: 'Open templates' },
      { key: `${modKey} + K`, description: 'Search nodes' },
      { key: `${modKey} + Z`, description: 'Undo' },
      { key: `${modKey} + Shift + Z`, description: 'Redo' },
      { key: 'Delete', description: 'Delete selected node' },
      { key: 'Escape', description: 'Clear selection' },
    ],
    modKey,
  };
}
