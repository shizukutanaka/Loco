import { create } from 'zustand';
import { Workflow } from '@/types/workflow';

interface HistoryState {
  past: Workflow[];
  present: Workflow | null;
  future: Workflow[];

  // Actions
  set: (workflow: Workflow) => void;
  undo: () => Workflow | null;
  redo: () => Workflow | null;
  clear: () => void;
  canUndo: () => boolean;
  canRedo: () => boolean;
}

const MAX_HISTORY_SIZE = 50;

export const useHistoryStore = create<HistoryState>((set, get) => ({
  past: [],
  present: null,
  future: [],

  set: (workflow) => {
    const { present, past } = get();

    // Don't add to history if workflow is identical
    if (present && JSON.stringify(present) === JSON.stringify(workflow)) {
      return;
    }

    const newPast = present ? [...past, present] : past;

    // Limit history size
    if (newPast.length > MAX_HISTORY_SIZE) {
      newPast.shift();
    }

    set({
      past: newPast,
      present: workflow,
      future: [], // Clear future when new change is made
    });
  },

  undo: () => {
    const { past, present, future } = get();

    if (past.length === 0) {
      return null;
    }

    const previous = past[past.length - 1];
    const newPast = past.slice(0, past.length - 1);
    const newFuture = present ? [present, ...future] : future;

    set({
      past: newPast,
      present: previous,
      future: newFuture,
    });

    return previous;
  },

  redo: () => {
    const { past, present, future } = get();

    if (future.length === 0) {
      return null;
    }

    const next = future[0];
    const newFuture = future.slice(1);
    const newPast = present ? [...past, present] : past;

    set({
      past: newPast,
      present: next,
      future: newFuture,
    });

    return next;
  },

  clear: () => {
    set({
      past: [],
      present: null,
      future: [],
    });
  },

  canUndo: () => {
    return get().past.length > 0;
  },

  canRedo: () => {
    return get().future.length > 0;
  },
}));
