import { useState } from 'react';

interface ToolbarModalsState {
  isTemplateGalleryOpen: boolean;
  isConnectionsManagerOpen: boolean;
  isWorkflowListOpen: boolean;
  isSettingsPanelOpen: boolean;
  isWorkflowTesterOpen: boolean;
  isKeyboardShortcutsOpen: boolean;
  isEditingName: boolean;
}

interface ToolbarModalsActions {
  openTemplateGallery: () => void;
  closeTemplateGallery: () => void;
  openConnectionsManager: () => void;
  closeConnectionsManager: () => void;
  openWorkflowList: () => void;
  closeWorkflowList: () => void;
  openSettingsPanel: () => void;
  closeSettingsPanel: () => void;
  openWorkflowTester: () => void;
  closeWorkflowTester: () => void;
  openKeyboardShortcuts: () => void;
  closeKeyboardShortcuts: () => void;
  startEditingName: () => void;
  stopEditingName: () => void;
}

/**
 * Custom hook for managing toolbar modal and dialog open/close states
 */
export function useToolbarModals(): ToolbarModalsState & ToolbarModalsActions {
  const [isTemplateGalleryOpen, setIsTemplateGalleryOpen] = useState(false);
  const [isConnectionsManagerOpen, setIsConnectionsManagerOpen] = useState(false);
  const [isWorkflowListOpen, setIsWorkflowListOpen] = useState(false);
  const [isSettingsPanelOpen, setIsSettingsPanelOpen] = useState(false);
  const [isWorkflowTesterOpen, setIsWorkflowTesterOpen] = useState(false);
  const [isKeyboardShortcutsOpen, setIsKeyboardShortcutsOpen] = useState(false);
  const [isEditingName, setIsEditingName] = useState(false);

  return {
    // State
    isTemplateGalleryOpen,
    isConnectionsManagerOpen,
    isWorkflowListOpen,
    isSettingsPanelOpen,
    isWorkflowTesterOpen,
    isKeyboardShortcutsOpen,
    isEditingName,

    // Template Gallery actions
    openTemplateGallery: () => setIsTemplateGalleryOpen(true),
    closeTemplateGallery: () => setIsTemplateGalleryOpen(false),
    openConnectionsManager: () => setIsConnectionsManagerOpen(true),
    closeConnectionsManager: () => setIsConnectionsManagerOpen(false),

    // Workflow List actions
    openWorkflowList: () => setIsWorkflowListOpen(true),
    closeWorkflowList: () => setIsWorkflowListOpen(false),

    // Settings Panel actions
    openSettingsPanel: () => setIsSettingsPanelOpen(true),
    closeSettingsPanel: () => setIsSettingsPanelOpen(false),


    // Workflow Tester actions
    openWorkflowTester: () => setIsWorkflowTesterOpen(true),
    closeWorkflowTester: () => setIsWorkflowTesterOpen(false),

    // Keyboard Shortcuts actions
    openKeyboardShortcuts: () => setIsKeyboardShortcutsOpen(true),
    closeKeyboardShortcuts: () => setIsKeyboardShortcutsOpen(false),

    // Name editing actions
    startEditingName: () => setIsEditingName(true),
    stopEditingName: () => setIsEditingName(false),
  };
}
