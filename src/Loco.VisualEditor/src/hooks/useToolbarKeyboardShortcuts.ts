import { useEffect } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';

interface KeyboardShortcutHandlers {
  onNew: () => void;
  onSave: () => void;
  onImport: () => void;
  onExport: () => void;
  onOpenWorkflowList: () => void;
  onOpenTemplateGallery: () => void;
  onOpenWorkflowTester: () => void;
  onOpenSettings: () => void;
  onOpenKeyboardShortcuts: () => void;
  onRun: () => void;
}

/**
 * Custom hook for managing global keyboard shortcuts in the toolbar
 * Handles: Ctrl+S, Ctrl+N, Ctrl+O, Ctrl+E, Ctrl+K, Ctrl+T, Ctrl+,, Ctrl+Shift+F, ?, Ctrl+/
 */
export function useToolbarKeyboardShortcuts(handlers: KeyboardShortcutHandlers) {
  const { undo, redo, canUndo, canRedo } = useWorkflowStore();

  // Destructure handlers to use individual functions in dependency array instead of the entire object
  // This prevents effect recreation when handlers object is recreated but individual functions are memoized
  const {
    onNew,
    onSave,
    onImport,
    onExport,
    onOpenWorkflowList,
    onOpenTemplateGallery,
    onOpenWorkflowTester,
    onOpenSettings,
    onOpenKeyboardShortcuts,
    onRun,
  } = handlers;

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ignore if typing in input/textarea
      const target = e.target as HTMLElement;
      if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
        return;
      }

      const isMac = navigator.platform.toUpperCase().indexOf('MAC') >= 0;
      const ctrlKey = isMac ? e.metaKey : e.ctrlKey;

      // Help shortcuts: ? or Ctrl+/
      if (e.key === '?' || (ctrlKey && e.key === '/')) {
        e.preventDefault();
        onOpenKeyboardShortcuts();
        return;
      }

      // Ctrl shortcuts
      if (ctrlKey) {
        switch (e.key.toLowerCase()) {
          case 'z':
            e.preventDefault();
            if (e.shiftKey) {
              // Ctrl+Shift+Z = Redo
              if (canRedo) redo();
            } else {
              // Ctrl+Z = Undo
              if (canUndo) undo();
            }
            break;
          case 'y':
            // Ctrl+Y = Redo (alternative)
            e.preventDefault();
            if (canRedo) redo();
            break;
          case 'n':
            e.preventDefault();
            onNew();
            break;
          case 's':
            e.preventDefault();
            onSave();
            break;
          case 'o':
            e.preventDefault();
            onImport();
            break;
          case 'e':
            e.preventDefault();
            onExport();
            break;
          case 'k':
            e.preventDefault();
            onOpenWorkflowList();
            break;
          case 't':
            if (!e.shiftKey) {
              e.preventDefault();
              onOpenTemplateGallery();
            } else {
              e.preventDefault();
              onOpenWorkflowTester();
            }
            break;
          case ',':
            e.preventDefault();
            onOpenSettings();
            break;
          case 'enter':
            e.preventDefault();
            onRun();
            break;
          default:
            break;
        }

        // Ctrl+Shift shortcuts
        if (e.shiftKey) {
          switch (e.key.toLowerCase()) {
            case 'f':
              e.preventDefault();
              window.dispatchEvent(new CustomEvent('canvas:fit-view'));
              break;
            default:
              break;
          }
        }
      }

      // Zoom controls (not combined with shift)
      if (ctrlKey && !e.shiftKey) {
        switch (e.key) {
          case '+':
          case '=':
            e.preventDefault();
            window.dispatchEvent(new CustomEvent('canvas:zoom-in'));
            break;
          case '-':
          case '_':
            e.preventDefault();
            window.dispatchEvent(new CustomEvent('canvas:zoom-out'));
            break;
          case '0':
            e.preventDefault();
            window.dispatchEvent(new CustomEvent('canvas:reset-zoom'));
            break;
          default:
            break;
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [
    canUndo,
    canRedo,
    undo,
    redo,
    onNew,
    onSave,
    onImport,
    onExport,
    onOpenWorkflowList,
    onOpenTemplateGallery,
    onOpenWorkflowTester,
    onOpenSettings,
    onOpenKeyboardShortcuts,
    onRun,
  ]);
}
