/**
 * Execution Store
 *
 * Manages workflow execution history and current execution state.
 */

import { create } from 'zustand';
import type { WorkflowExecutionResponse } from '@/api/types';

// ============================================================================
// Types
// ============================================================================

interface ExecutionHistory {
  executionId: string;
  workflowId: string;
  workflowName: string;
  status: WorkflowExecutionResponse['status'];
  startedAt: string;
  completedAt?: string;
  duration?: number;
  error?: string;
}

interface ExecutionState {
  // Current execution
  currentExecutionId: string | null;
  isExecutionPanelOpen: boolean;

  // Execution history
  history: ExecutionHistory[];

  // Actions
  setCurrentExecution: (executionId: string | null) => void;
  openExecutionPanel: () => void;
  closeExecutionPanel: () => void;
  toggleExecutionPanel: () => void;

  addToHistory: (execution: ExecutionHistory) => void;
  updateHistory: (executionId: string, updates: Partial<ExecutionHistory>) => void;
  clearHistory: () => void;
  getHistoryByWorkflow: (workflowId: string) => ExecutionHistory[];
}

// ============================================================================
// Store
// ============================================================================

export const useExecutionStore = create<ExecutionState>((set, get) => ({
  // Initial state
  currentExecutionId: null,
  isExecutionPanelOpen: false,
  history: [],

  // Actions
  setCurrentExecution: (executionId) => {
    set({ currentExecutionId: executionId });
    if (executionId) {
      set({ isExecutionPanelOpen: true });
    }
  },

  openExecutionPanel: () => set({ isExecutionPanelOpen: true }),
  closeExecutionPanel: () => set({ isExecutionPanelOpen: false }),
  toggleExecutionPanel: () =>
    set((state) => ({ isExecutionPanelOpen: !state.isExecutionPanelOpen })),

  addToHistory: (execution) =>
    set((state) => ({
      history: [execution, ...state.history].slice(0, 50), // Keep last 50 executions
    })),

  updateHistory: (executionId, updates) =>
    set((state) => ({
      history: state.history.map((h) =>
        h.executionId === executionId ? { ...h, ...updates } : h
      ),
    })),

  clearHistory: () => set({ history: [] }),

  getHistoryByWorkflow: (workflowId) => {
    return get().history.filter((h) => h.workflowId === workflowId);
  },
}));
