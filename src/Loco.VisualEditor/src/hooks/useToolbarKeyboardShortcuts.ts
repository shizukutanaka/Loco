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
  onOpenCollaboration: () => void;
  onOpenKeyboardShortcuts: () => void;
  onRun: () => void;
}

/**
 * Custom hook for managing global keyboard shortcuts in the toolbar
 * Handles: Ctrl+S, Ctrl+N, Ctrl+O, Ctrl+E, Ctrl+K, Ctrl+T, Ctrl+,, Ctrl+Shift+C, Ctrl+Shift+F, ?, Ctrl+/
 */
export function useToolbarKeyboardShortcuts(handlers: KeyboardShortcutHandlers) {
  const { undo, redo, canUndo, canRedo } = useWorkflowStore();

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
        handlers.onOpenKeyboardShortcuts();
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
            handlers.onNew();
            break;
          case 's':
            e.preventDefault();
            handlers.onSave();
            break;
          case 'o':
            e.preventDefault();
            handlers.onImport();
            break;
          case 'e':
            e.preventDefault();
            handlers.onExport();
            break;
          case 'k':
            e.preventDefault();
            handlers.onOpenWorkflowList();
            break;
          case 't':
            if (!e.shiftKey) {
              e.preventDefault();
              handlers.onOpenTemplateGallery();
            } else {
              e.preventDefault();
              handlers.onOpenWorkflowTester();
            }
            break;
          case ',':
            e.preventDefault();
            handlers.onOpenSettings();
            break;
          case 'enter':
            e.preventDefault();
            handlers.onRun();
            break;
          default:
            break;
        }

        // Ctrl+Shift shortcuts
        if (e.shiftKey) {
          switch (e.key.toLowerCase()) {
            case 'c':
              e.preventDefault();
              handlers.onOpenCollaboration();
              break;
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
  }, [canUndo, canRedo, undo, redo, handlers]);
}
